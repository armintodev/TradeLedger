# TradeLedger

A personal crypto trade journal + portfolio dashboard, rebuilt from the owner's
Excel workbook (`Trade City Pro Journal V1.51 Lite.xlsb`) as an ASP.NET Core
backend that **pulls trades automatically from the Bitunix exchange**.

Primary user: the repo owner (solo trader). A public multi-user launch is a
possible later cycle, not this one — but the schema is built for it from day one.

---

## 1. What this project is

The Excel journal already works. It is the requirements document. It has two halves:

| Half | Content | Who fills it |
|---|---|---|
| **Mechanical** | entry/exit price, qty, leverage, margin, fees, funding, PnL, duration, balance | **Bitunix API — automatically** |
| **Subjective** | strategy, mental state, checklist/market context, mistakes, rating, screenshots, memo | **The trader — manually** |

The entire point of TradeLedger is that the mechanical half stops being typed by
hand. A closed Bitunix position becomes a journal row on its own; the trader only
supplies the part a machine cannot know.

See [`docs/excel-journal-reference.md`](docs/excel-journal-reference.md) for the
complete column-by-column inventory of the source workbook and the exact dropdown
vocabularies (strategies, mental states, mistakes, trackings, checklist items).

### Hard constraint: read-only against the exchange

**TradeLedger never places, modifies, or cancels an order. Ever.** It is a
journal, not a trading bot. Bitunix API keys are expected to be created
read-only, and no code path in this repo may call a Bitunix write endpoint
(`place_order`, `cancel_order`, `modify_position`, transfers, withdrawals).
If a task seems to require one, stop and ask — it is out of scope by design.

---

## 2. Decisions already made

These were settled with the owner. Do not re-litigate them; if something needs to
change, ask first.

| Area | Decision |
|---|---|
| **Framework** | .NET 10 (LTS). SDK 10.0.301 is installed locally. |
| **Structure** | **Vertical slice** — feature folders, each holding its endpoints, handlers and DTOs together. |
| **ORM / DB** | EF Core 10 + Npgsql on PostgreSQL. |
| **Cache** | Redis — used for sync locking, exchange rate-limit budgeting, and analytics caching. Not optional; the sync design depends on it. |
| **Tenancy** | Multi-tenant schema **now**, single user in practice. Every user-owned table carries `UserId`, enforced by EF Core global query filters. Public signup is disabled; the owner's account is seeded. |
| **Auth** | ASP.NET Core Identity + JWT. |
| **Plan to Trade** | **Both flows, plan optional.** A pre-trade `TradePlan` may exist and gets matched to the executed trade; if none exists the trade is auto-created and flagged `Unplanned`. "Planned vs unplanned performance" is itself a headline metric. |
| **Coverage** | Bitunix **futures + spot** synced automatically, **plus manual entries** for everything no API can see: external wallets (TonKeeper), other exchanges (Nobitex), LP/farm positions, transfers, and off-platform losses. Goal is one true equity curve. |

---

## 3. Domain model

### Ownership and venues

- **`User`** — Identity user. Root of every query filter.
- **`Account`** — a place where value lives. `Kind`: `ExchangeFutures | ExchangeSpot | ExchangeWallet | ExternalWallet | ManualVenue`. `SyncMode`: `Api | Manual`. Bitunix futures and Bitunix spot are two separate Accounts.
- **`ExchangeCredential`** — encrypted API key/secret bound to an Account. AES-GCM at rest; the encryption key comes from configuration (user-secrets / env), never from the database. Secrets are never returned by any endpoint, never logged, never included in a DTO.

### The journal

- **`Trade`** — one journal row = one **position round-trip**. Futures: one Bitunix `positionId`. Spot: a matched buy-to-sell round-trip. Carries the mechanical fields, the subjective fields, `Origin` (`Synced | Manual`), `ReviewState` (`Unreviewed | Reviewed`), and `IsPlanned`.
- **`Execution`** — an individual fill inside a Trade: price, qty, fee, timestamp, `Role` (`Open | Increase | Reduce | Close | Liquidation`), plus exchange order/trade ids. This is what makes scale-ins and partial closes representable — the workbook's `% of Margin Closed` column becomes derived, not entered.
- **`TradePlan`** — pre-trade intent: symbol, side, strategy, timeframe, planned entry / SL / TP, risk %, R:R, leverage, plus a `MarketContext` snapshot and entry mental state. Status: `Draft | Active | Linked | Abandoned | Expired`.
- **`MarketContext`** — the workbook's Check List, owned by a Trade and/or a Plan: Total2, BTC.D, USDT.D, market trend, SMA, session, BTC pair, RSI, volume, candle shape. These are the trader's own notes, typed by hand, and stay free text.
- **`MarketSession`** — which of Tokyo, London and New York was open when the trade was opened. A `[Flags]` enum, **derived** from `OpenedAt` by `MarketSessionCalendar` and persisted on the Trade, so it is filterable and groupable rather than retyped. Windows in UTC: Tokyo 00:00–09:00, London 07:00–16:00, New York 12:00–21:00; 21:00–00:00 is `None`. The overlaps fall out of the flags — 07:00–09:00 reads `Tokyo, London` and 12:00–16:00 reads `London, NewYork`, which is the window most traders care about. Distinct from the free-text `MarketContext.MarketSession` checklist note, which is what the trader *thought* at the time.
- **`Attachment`** — entry/exit/transfer screenshots.

### Taxonomies (user-scoped, seeded from the workbook's `Variables` sheet)

`Strategy`, `MentalState`, `Mistake`, `Tracking`, `ChecklistItem`, `Timeframe`.
Seeded with the owner's existing vocabulary, editable per user. A Trade links to
many `Mistake` and many `Tracking`.

### Money movement and balances

- **`FundingPayment`** — futures funding, attributed to a Trade where possible, otherwise to the Account.
- **`Transfer`** — deposit / withdrawal / internal move, with its own fee and a `WriteOff` flag for losses like a wrong-network send.
- **`Holding`** — a non-round-trip asset position: spot bags, wallet balances, and LP/farm positions (pool, LP token amount, entry value, APR, farm status).
- **`BalanceSnapshot`** — per Account, point-in-time `WalletBalance`, `UnrealizedPnl`, `Equity`. **This is the sole source of the equity curve and every drawdown number.** Never recompute equity by summing trades; snapshot it.

### Sync bookkeeping

- **`SyncCursor`** — per (Account, endpoint) high-water mark for incremental pulls.
- **`SyncRun`** — audit of each sync attempt: started/finished, records seen, records written, errors.
- **`RawExchangePayload`** — the untouched JSON as received, keyed by account + endpoint + external id. Keep it. When a mapping turns out to be wrong, this is what lets it be fixed retroactively without re-fetching.

---

## 4. Bitunix integration

Full endpoint and signing notes: [`docs/bitunix-api.md`](docs/bitunix-api.md).

**Base URL** `https://fapi.bitunix.com`

**Signing** — double SHA-256, hex-encoded:

```
digest = SHA256(nonce + timestamp + apiKey + sortedQueryParams + compactBody)
sign   = SHA256(digest + secretKey)
```

`sortedQueryParams` = params sorted ASCII-ascending by key, concatenated as
`keyvaluekeyvalue` with no separators. `compactBody` = JSON with all whitespace
removed, or an empty string for GETs. Headers: `api-key`, `sign`, `nonce`,
`timestamp`, `language`, `Content-Type: application/json`.

### Egress proxy — the exchange must only ever see one IP

The Bitunix API key is allowlisted to a static IP, so **every** request to the
exchange is routed through a proxy. This is a correctness constraint, not a
convenience: a single request from the wrong address can get the key flagged.

- The proxy is **per user**, stored on `AppUser` (`ProxyScheme`, `ProxyHost`, `ProxyPort`, `ProxyUsername`, `ProxyPasswordCipher`, `ProxyEnabled`). The password is AES-GCM encrypted with the same `ICredentialProtector` as the API secret, and is never returned by any endpoint.
- Resolution order: the user's enabled proxy → the `Proxy` configuration section → none.
- **`Proxy:Required` defaults to `true`.** When no proxy resolves, `BitunixHttpClientProvider` throws `ProxyRequiredException` *before opening a socket*. It never silently falls back to a direct connection.
- Schemes: `Http`, `Https`, `Socks5`, `Socks4`, `Socks4a`. The scheme must be in the proxy **URI** — `new WebProxy(host, port)` silently produces `http://` and SOCKS never engages.
- Credentials are set on both `WebProxy.Credentials` and `SocketsHttpHandler.DefaultProxyCredentials`. Verified against a live SOCKS5 server: either alone authenticates, and a wrong password fails the request rather than falling back to direct.
- `BypassProxyOnLocal` is forced false and no bypass list is set. An `IWebProxy` whose `GetProxy()` returns null means *go direct* — that is the silent-leak trap, so never introduce one.

`BitunixClient` takes no `HttpClient`: it resolves one per call from
`IBitunixHttpClientProvider`, keyed by proxy identity (`scheme|host|port|username`),
so accounts sharing an IP share a socket pool. Every call carries a
`BitunixConnection` (credentials + resolved proxy) rather than bare credentials.

**Credential verification goes through the proxy too.** `PUT /api/accounts/{id}/credentials`
calls Bitunix to validate the key, so the proxy must be configured *first* —
otherwise the key is verified from the wrong address.

Manage it with `GET|PUT|DELETE /api/proxy`. When the WebSocket live track lands,
give `ClientWebSocket.Options.Proxy` the same resolved endpoint.

### Two-track sync — WebSocket is a hint, REST is the truth

Never treat a WebSocket message as authoritative persistence.

1. **Live track** — private WS channels (Position, Order, Balance) drive
   near-real-time dashboard updates and mark affected entities dirty.
2. **Reconcile track** — a periodic REST sweep over `get_history_positions`,
   `get_history_trades`, `get_history_orders` and `GET /api/v1/futures/account`
   using `SyncCursor` watermarks. This writes the canonical record. A missed or
   duplicated WS frame must self-heal on the next sweep.
3. **Backfill** — a one-shot paginated walk from the account's start date, run on
   credential registration. `limit` maxes at 100; page with `skip`.

**Idempotency is structural, not defensive.** Unique indexes on
`(AccountId, ExchangePositionId)` and `(AccountId, ExchangeTradeId)`; all writes
are upserts. Re-running any sync must be a no-op.

**Rate limiting** — roughly 10 req/s per UID; WS accepts a maximum of 5
messages/s. Enforce with a Redis token bucket keyed by credential and shared
across all workers. Treat the limit as a hard budget, not a retry-on-429 target.

**Concurrency** — a Redis distributed lock per Account ensures only one sync runs
at a time even with multiple worker instances.

---

## 5. Analytics the dashboard must produce

Everything below already exists in, or is implied by, the workbook's
`Trade Log` and `Results` sheets.

- **Counts** — total, winning, losing, breakeven, open positions
- **Money** — gross PnL, fees, funding, net PnL, trade gain %, account change %
- **Risk** — planned R vs achieved R, % of account risked, position-to-account %, expectancy, profit factor, average win / average loss
- **Equity** — equity curve from `BalanceSnapshot`; **max drawdown** (peak-to-trough) and **current drawdown**; longest win/loss streak
- **Breakdowns** — by strategy, symbol, side, timeframe, market session, day of week, hour of day, entry/exit mental state
- **Discipline** — planned vs unplanned performance, mistake frequency and the realized cost of each mistake, checklist adherence vs outcome
- **Duration** — trade duration distribution and its correlation with outcome

Cache expensive aggregates in Redis, keyed by user + filter hash, invalidated on
any Trade or BalanceSnapshot write.

---

## 6. Coding rules

**Money and quantities**

- `decimal` in C#. **Never `double` or `float`, anywhere, for any monetary or quantity value.**
- Postgres precisions are declared once in `Shared/Precision.cs` and referenced from every EF configuration:

  | Constant | Column type | Integer / fractional digits | Used for |
  |---|---|---|---|
  | `Precision.Price` | `numeric(28,18)` | 10 / 18 | prices — covers sub-satoshi meme-coin quotes |
  | `Precision.Quantity` | `numeric(28,12)` | 16 / 12 | quantities, transfer amounts |
  | `Precision.Money` | `numeric(28,8)` | 20 / 8 | PnL, fees, funding, balances |
  | `Precision.Ratio` | `numeric(18,6)` | 12 / 6 | R multiples, percentages, fee rates |

  All four round-trip through `decimal`, which holds ~28 significant digits. A
  `numeric(38,18)` column would **not** — it exceeds `decimal`'s range and would
  silently truncate. Do not widen these without checking that constraint.
- Bitunix returns numbers as JSON *strings*. Parse with `CultureInfo.InvariantCulture` and an explicit `NumberStyles` — `BitunixJson.Options` already does this. Never rely on ambient culture.

**Time**

- Everything is UTC. `DateTimeOffset` in C#, `timestamptz` in Postgres.
- Also persist the exchange's raw millisecond epoch alongside the parsed value — it is the tiebreaker when ordering fills.
- The workbook split dates into `D` / `M` / `Year` / `hh` / `mm` columns. That was an Excel limitation. Store single instants; derive the parts in queries.

**The domain model is rich, not a property bag**

This is the rule the rest of the design hangs off. Entities own their invariants;
nothing outside the aggregate may put one into an invalid state.

- **Every setter is `private`.** Entities expose state for reading and behaviour for changing. There are no public setters on any entity, and no public parameterless constructors — EF materialises through the private one.
- **Construction goes through a named factory** that validates: `Trade.OpenManual`, `Trade.FromExchange`, `Account.Create`, `TradePlan.Create`, `Holding.Open`, `Transfer.Record`, `BalanceSnapshot.Capture`, `BacktestRun.Queue`, `BacktestTrade.Close`. Factories taking more than a handful of values take a `New*` spec record (`NewManualTrade`, `NewHolding`, `NewBacktestRun`) rather than a long parameter list.
- **Mutation goes through intention-revealing methods**: `trade.Journal(edit)`, `trade.MarkReviewed()`, `trade.AdoptPlan(plan)`, `run.Start(balance)`, `run.Succeed(...)`, `holding.Reprice(price, at)`, `user.ConfigureProxy(...)`. Never a setter, never a generic `Update(entity)`.
- **Derived fields are derived inside the aggregate.** `Trade.Recalculate()` is private and every mutator calls it; net PnL, outcome, duration, achieved R, market session and the account percentages are never assigned from outside. `BacktestTrade.Close` derives net, outcome, R multiples, MAE and MFE the same way.
- **Collections are read-only.** `IReadOnlyCollection<T>` over a private backing field, changed only through methods like `trade.ReplaceMistakes(ids)` or `trade.AddExecution(...)`. EF binds to the field by convention.
- **Invariants throw, they do not silently correct.** Use `Guard` (`Guard.Positive`, `Guard.NotBlank`, `Guard.InRange`, `Guard.Rule`). A stop loss on the wrong side of the entry, a deposit without a destination account, reviewing an open trade, deleting a synced trade, switching a backtest account with runs to sequential — all rejected at the domain boundary, not at the endpoint.
- **Domain services hold logic that spans aggregates or belongs to no single one**: `MarketSessionCalendar` (which session a UTC instant falls in), `TradePlanMatcher` (which plan a filled trade should adopt), `PositionSizeCalculator`. They are static or stateless and free of `DbContext`.
- **Mappers translate, they do not mutate.** `BitunixPositionMapper` produces an `ExchangePositionSnapshot`; the `Trade` applies it. An integration must never reach into an entity field by field.

**Errors**

- Failures are `ProblemDetails` with a stable machine-readable `code`, a `traceId`, and a field-keyed `errors` object for validation. Shaped once in `Api/Shared/Errors/`.
- Endpoints **throw** rather than hand-building problem responses: `ResourceNotFoundException`, `ResourceConflictException`, `DomainRuleException`, `DomainValidationException`, `NotAuthenticatedException`. `GlobalExceptionHandler` maps each to its status — 400 validation, 401 unauthenticated, 404 not found, 409 conflict, 422 broken business rule — and maps Postgres 23505/23503 too.
- Exception detail is only echoed for 4xx. A 500 returns the generic message outside Development.

**Persistence**

- Every user-owned entity gets `UserId` plus an EF Core global query filter. A query that can see another user's row is a bug, even today with one user. `IUserOwned.UserId` is read-only; the DbContext stamps it through the EF property entry so encapsulation survives.
- Migrations are checked in and reviewed. No `EnsureCreated`. Generated migrations are converted to file-scoped namespaces to satisfy `EnforceCodeStyleInBuild`.
- Index for the real access patterns: `(UserId, OpenedAt DESC)`, unique `(AccountId, ExchangePositionId)`, `(UserId, StrategyId)`, `(UserId, MarketSession)`, and `(AccountId, CapturedAt DESC)` on snapshots.
- **One `IEntityTypeConfiguration` per file**, under `Persistence/Configurations/<Aggregate>/`. Never a grab-bag file holding eight configurations.
- **Anything writing more than one row goes through `IUnitOfWork.ExecuteInTransactionAsync`.** It opens a transaction through the Npgsql execution strategy, saves, and commits; a throw rolls the whole unit back. A nested call joins the ambient transaction instead of opening a second. Journalling a trade and a sync page are both single units — a rejected tag must not leave a half-updated row.

**Secrets**

- API secrets never appear in logs, exceptions, DTOs, or telemetry. Redact at the HTTP-client layer, not at each call site.

**Async**

- **Do not write `.ConfigureAwait(false)` in new code.** There is no synchronization context in ASP.NET Core or the worker host, so it buys nothing and costs readability. Existing call sites stay as they are; they are not worth a refactor pass.

**Structure**

- Feature folders under `src/TradeLedger.Api/Features/<Feature>/`. **Endpoints, requests and responses live in separate files**: `XxxEndpoints.cs` holds only route mapping, `XxxRequests.cs` the inbound DTOs, `XxxResponses.cs` the outbound ones. An endpoint file that declares a record at the bottom is wrong.
- Request DTOs map themselves into the domain (`request.ToSpec()`, `request.ToEdit()`), so endpoints stay free of construction logic. **Never bind a request directly to a domain type** — `MarketContextRequest` exists so `MarketContext` does not have to be public-settable.
- All entities live under `Core/Domain/`, foldered by aggregate: `Common/`, `Accounts/`, `Journal/`, `Portfolio/`, `Sync/`, `Backtesting/`, `MarketData/`, `Services/`. Nothing that EF maps lives outside `Domain/` — backtesting and market-data entities are domain, their services are not.
- EF configuration in `Persistence/`; the Bitunix client and workers in `Integrations/Bitunix/`.
- Prefer minimal APIs with typed results over MVC controllers.

**Testing**

- Integration tests run against real Postgres and Redis via Testcontainers — not in-memory providers, which hide Npgsql behaviour.
- The Bitunix signing algorithm has unit tests pinned to the vendor's published example vector.
- Sync idempotency has an explicit test: run the same payload twice, assert one row.

---

## 7. Layout

```
src/
  TradeLedger.Core/          class library, shared by Api and Worker
    Domain/                  every mapped entity, foldered by aggregate
      Common/                IUserOwned, Guard, domain exceptions, MarketSession, enums
      Accounts/              AppUser, Account, ExchangeCredential
      Journal/               Trade, Execution, TradePlan, MarketContext, Taxonomy, specs
      Portfolio/             Holding, Transfer, BalanceSnapshot, FundingPayment, Attachment
      Sync/                  SyncCursor, SyncRun, RawExchangePayload
      Backtesting/           BacktestAccount, Strategy, Run, Trade, Execution, EquityPoint
      MarketData/            Candle, FundingRateHistory, aliases, imports, backfill jobs
      Services/              TradePlanMatcher and other cross-aggregate rules
    Persistence/             DbContext, UnitOfWork, migrations, seed
      Configurations/        one file per entity, foldered by aggregate
    Integrations/Bitunix/    signer, REST client, DTOs, mappers, sync service
    Analytics/               metrics, equity curve, position-size calculator
    Backtesting/             options, runner, engine, indicators, rules (services only)
    MarketData/              Binance client, repository, CSV import, backfill (services only)
    Shared/                  precision, tenancy, crypto, Redis lock, egress proxy
  TradeLedger.Api/           minimal APIs, vertical slices
    Features/
      Auth/                  login, current user
      Accounts/              venues, credentials
      Trades/                journal CRUD, review inbox, manual trades
      Plans/                 pre-trade plans, calculator
      Journal/               taxonomy pickers
      Portfolio/             holdings, transfers, manual snapshots
      Analytics/             summary, equity curve, breakdowns, mistakes
      Sync/                  status, manual trigger, backfill
      Proxy/                 per-user egress proxy
      MarketData/            candle backfill, CSV import, coverage and gaps
      Backtests/             simulated accounts, strategies, runs
    Shared/                  JWT, HTTP user context, startup seeder, paging
      Errors/                ProblemDetails shaping and the global exception handler
      OpenApi/               document, security and tag transformers
  TradeLedger.Worker/        SyncWorker, SnapshotWorker, MarketDataWorker, BacktestWorker
tests/
  TradeLedger.UnitTests/          signer, domain rules, calculations, indicators, rules engine
  TradeLedger.IntegrationTests/   tenancy, idempotency, precision, transactions (Testcontainers)
docs/
  excel-journal-reference.md
  bitunix-api.md
  market-data.md
compose.yaml               postgres + redis for local dev
```

Each `Features/<Feature>/` folder holds three files: `XxxEndpoints.cs`,
`XxxRequests.cs` and `XxxResponses.cs`. Routing, inbound shape and outbound
shape are separate concerns and separate files.

**Why a `Core` library rather than everything inside `Api`:** the Worker is a
separate process but needs Domain, Persistence and the Bitunix client. Having it
reference the web project would drag the whole ASP.NET stack into a background
service. Vertical slices still hold where they matter — the feature folders under
`Api/Features/` each own their endpoints, handlers and DTOs.

The frontend is a later cycle. Keep the API frontend-agnostic — no view-shaped
endpoints, no HTML concerns.

---

## 7a. Getting started

```bash
docker compose up -d
```

Configure the secrets (never in `appsettings.json`):

```bash
cd src/TradeLedger.Api
dotnet user-secrets set "Encryption:KeyBase64" "$(openssl rand -base64 32)"
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)"
dotnet user-secrets set "Seed:OwnerEmail" "you@example.com"
dotnet user-secrets set "Seed:OwnerPassword" "a-long-password"
```

The egress proxy can come from configuration instead of the database, which is
usually what you want for a single user:

```bash
dotnet user-secrets set "Proxy:Scheme" "Socks5"
dotnet user-secrets set "Proxy:Host" "your.proxy.host"
dotnet user-secrets set "Proxy:Port" "1080"
dotnet user-secrets set "Proxy:Username" "tunnel"
dotnet user-secrets set "Proxy:Password" "..."
```

`Proxy:Required` is true by default, so without either a per-user proxy or the
settings above every exchange request is refused rather than sent from your own
address.

Then run. Migrations apply and the owner account plus taxonomy seed on startup:

```bash
dotnet run --project src/TradeLedger.Api
```

The worker is a separate process and needs the same connection strings and
encryption key:

```bash
dotnet run --project src/TradeLedger.Worker
```

Tests — integration tests need Docker running for Testcontainers:

```bash
dotnet test
```

New migration after a model change:

```bash
dotnet ef migrations add <Name> -p src/TradeLedger.Core -s src/TradeLedger.Core -o Persistence/Migrations
```

---

## 7b. API documentation

Three surfaces, all served only in Development:

| Route | What it is |
|---|---|
| `/docs` | **Scalar** — the primary reference UI. Sidebar by tag, built-in auth panel, client-library code samples. `/` redirects here. |
| `/swagger` | **Swagger UI** — classic try-it-out, filter box, persisted authorization. |
| `/openapi/v1.json` | The raw OpenAPI 3.1 document. |

The document is generated by the built-in .NET 10 OpenAPI pipeline — there is no
Swashbuckle *generation*, only its UI. Everything is shaped in
`Api/Shared/OpenApi/OpenApiTransformers.cs`:

- `DocumentInfoTransformer` — title, version, the markdown overview, and the tag list with descriptions. **Tag order here is the order the UI renders groups in.**
- `BearerSecurityTransformer` — the JWT scheme behind the Authorize button.
- `SecurityRequirementTransformer` — marks every non-anonymous operation as secured and documents its 401.

**Endpoint documentation is fluent, never XML doc comments** — `.WithSummary()`,
`.WithDescription()`, `.Produces<T>()`, `.ProducesProblem(...)` on each endpoint.
A new endpoint without a summary is an incomplete endpoint.

Two rules that keep the document generating at all:

- **Never `.Produces<T>()` an EF entity.** Entities have circular navigation properties (`Trade → Account → Trades → …`) and schema generation recurses until it throws `CurrentDepth (64)`. Return a response DTO — `TradeDetailResponse`, `PlanResponse`, `HoldingResponse`, `TransferResponse`, `BalanceSnapshotResponse`, `SyncRunResponse`.
- **Optional query parameters must be nullable.** A non-nullable `int page` or `bool includeInactive` is *required* in minimal APIs: omitting it throws and the doc marks it required. Use `int?` / `bool?` with a default applied in the handler.

Enums are strings on the wire in both directions (`JsonStringEnumConverter`, registered in `Program.cs`), so schemas show `"Long"` / `"ExchangeFutures"` rather than magic numbers.

The exception handler maps failures to honest status codes rather than a blanket
500: `BadHttpRequestException` → 400, Postgres unique violation (23505) → 409,
foreign-key violation (23503) → 400.

### Postman

`docs/TradeLedger.postman_collection.json` and
`docs/TradeLedger.postman_environment.json` — 74 requests in 12 folders,
collection-level bearer auth, and scripts that capture ids as you go.

Import both, set `password` in the environment, run **Auth > Login**; the token
is saved automatically and everything else inherits it. Requests that need an
`accountId`, `tradeId`, `holdingId`, `backtestAccountId`, `backtestStrategyId`
or `backtestRunId` fetch one lazily in a pre-request script, so any request also
works on its own and the Collection Runner passes top to bottom.

**Runtime ids live in collection variables, not the environment** — the
environment holds only `baseUrl`, `email` and `password`. An empty environment
variable *shadows* a collection variable of the same name, which silently breaks
the token; keep config and runtime state in separate scopes.

**Write `url.path` by hand, never through `new URL()`.** The URL constructor
percent-encodes `{{`, so a path segment holding a variable is serialised as
`%7B%7BbacktestRunId%7D%7D` and Postman stops substituting it. The request still
returns 404 rather than failing loudly, so the breakage is easy to miss.

Two folders cannot pass cold, by design rather than by accident: **Backtests >
Queue a run** needs candle data and is refused with `candle_data_has_gaps` until
you backfill the symbol, and **Market Data > Import TradingView CSV** needs a
file attached to the form-data body. Everything downstream of a queued run then
404s on an empty id. That refusal is the feature — a missing bar silently skips
a signal, and a backtest you cannot trust is worse than none.

Verify the collection against a running API with:

```bash
npx newman run docs/TradeLedger.postman_collection.json -e docs/TradeLedger.postman_environment.json --env-var "password=..."
```

---

## 8. Working agreements

- The workbook is the spec. Before adding or renaming a journal field, check
  `docs/excel-journal-reference.md` — the owner has years of muscle memory in
  that vocabulary, so keep his names (`Tracking`, `PS Tag`, `Entry Type`) rather
  than inventing new ones.
- When a real Bitunix response disagrees with `docs/bitunix-api.md`, the live
  response wins — update the doc in the same change.
- Ask before: any exchange write call, any schema change that drops data, any
  change to the sync watermark semantics.
