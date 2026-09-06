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
- **`MarketContext`** — the workbook's Check List, owned by a Trade and/or a Plan: Total2, BTC.D, USDT.D, market trend, SMA, session, BTC pair, RSI, volume, candle shape.
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

**Persistence**

- Every user-owned entity gets `UserId` plus an EF Core global query filter. A query that can see another user's row is a bug, even today with one user.
- Migrations are checked in and reviewed. No `EnsureCreated`.
- Index for the real access patterns: `(UserId, OpenedAt DESC)`, unique `(AccountId, ExchangePositionId)`, `(UserId, StrategyId)`, and `(AccountId, CapturedAt DESC)` on snapshots.

**Secrets**

- API secrets never appear in logs, exceptions, DTOs, or telemetry. Redact at the HTTP-client layer, not at each call site.

**Structure**

- Feature folders under `src/TradeLedger.Api/Features/<Feature>/`. Endpoint, handler, request/response DTOs and validator live together.
- Cross-feature domain types go in `Domain/`; EF configuration in `Persistence/`; the Bitunix client and workers in `Integrations/Bitunix/`.
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
    Domain/                  entities and enums
    Persistence/             DbContext, configurations, migrations, seed
    Integrations/Bitunix/    signer, REST client, DTOs, mappers, sync service
    Analytics/               metrics, equity curve, position-size calculator
    Shared/                  precision, tenancy, crypto, Redis lock
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
    Shared/                  JWT, HTTP user context, startup seeder
  TradeLedger.Worker/        SyncWorker + SnapshotWorker
tests/
  TradeLedger.UnitTests/          signer, calculations, calculator, mapping
  TradeLedger.IntegrationTests/   tenancy, idempotency, precision (Testcontainers)
docs/
  excel-journal-reference.md
  bitunix-api.md
compose.yaml               postgres + redis for local dev
```

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

Configure the three secrets (never in `appsettings.json`):

```bash
cd src/TradeLedger.Api
dotnet user-secrets set "Encryption:KeyBase64" "$(openssl rand -base64 32)"
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)"
dotnet user-secrets set "Seed:OwnerEmail" "you@example.com"
dotnet user-secrets set "Seed:OwnerPassword" "a-long-password"
```

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
`docs/TradeLedger.postman_environment.json` — 42 requests in 9 folders,
collection-level bearer auth, and scripts that capture ids as you go.

Import both, set `password` in the environment, run **Auth > Login**; the token
is saved automatically and everything else inherits it. Requests that need an
`accountId`, `strategyId` or `mistakeId` fetch one lazily in a pre-request
script, so any request also works on its own and the Collection Runner passes
top to bottom.

**Runtime ids live in collection variables, not the environment** — the
environment holds only `baseUrl`, `email` and `password`. An empty environment
variable *shadows* a collection variable of the same name, which silently breaks
the token; keep config and runtime state in separate scopes.

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
