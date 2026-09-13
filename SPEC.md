# SPEC — Backtesting

Status: **Approved for implementation.** Revision 2. Supersedes nothing; this is a new feature area.

This spec adds two engines to TradeLedger:

| Engine | Question it answers |
|---|---|
| **Rule engine** | "If I had mechanically traded *these rules* on this symbol, what would have happened?" |
| **What-if** | "If I had traded *my own past trades* with different sizing, filters, costs or exits, what would have happened?" |

They share storage, the backtest account, the run lifecycle, the intrabar resolver and the metrics code. They share no signal logic.

---

## 0. Decisions already made

Settled with the owner. Do not re-litigate; ask before changing.

| Question | Decision |
|---|---|
| What is a backtest here | **Both** a declarative rule engine **and** what-if replay of real trades |
| Rule format | **Declarative JSON rule tree**, interpreted. No scripting, no compiler, no code execution. |
| Price data | **Binance public klines** (automated) + **TradingView CSV import** (manual). Not Bitunix. |
| Storage | **Separate entities entirely.** Backtest output never touches `Trade`. |
| Fill fidelity | **Full intrabar modelling**, resolved by drilling into finer candles |
| Universe | **One symbol per run** |
| Parameter sweeps | **Not in v1** |
| Metrics | **Reuse the existing metric definitions**, refactored so live and simulated trades are scored identically |
| Kline egress | **The same per-user proxy as Bitunix** |
| **Minimum timeframe** | **15 minutes.** Nothing below it is tradeable. See §4.1. |
| **Indicators** | **`Sma`, `Ema`, `Rsi`, `Dmi`, `Adx`. Nothing else.** |
| **Position risk** | **Static, from configuration, 1–5% per position.** Not computed per trade. |
| **Exits** | **Stop-loss or take-profit only.** No discretionary exit, no signal exit, no time stop. |
| **Risk / reward** | **Never below 2.** Take-profit is always an R multiple ≥ 2, default exactly 2. |
| **Balance** | **A persistent backtest account** that many runs compound onto, not a per-run starting balance. |
| What-if scope | **All transformations in one release** — sizing, filters, costs and exit rules together. |

### Two facts that shaped these

**TradingView has no public market-data API.** Programmatic extraction of their chart data is not permitted by their terms. The only legitimate path is the owner exporting CSV themselves, which this spec supports as an import format. Do not implement a TradingView scraper.

**`Strategy` in the existing domain is a `TaxonomyTerm`** — a name, a colour, a description. It carries no machine-readable rules and must not be extended to. Backtest strategies are a new, separate concept (`BacktestStrategy`). A run may *label* itself with an existing `Strategy` term for comparison against live trades, but rules live only on `BacktestStrategy`.

---

## 1. Non-goals

- **No order placement.** This feature is simulation only. The project rule stands: TradeLedger never places, modifies or cancels an order. The backtest engine must not gain a code path to any exchange write endpoint.
- No parameter optimisation or grid search.
- No multi-symbol / portfolio-level runs.
- No walk-forward analysis, Monte Carlo, or machine learning.
- No live/paper forward-testing.
- No timeframe below 15 minutes as a tradeable interval.
- **Backtest results never appear in live analytics.** `GET /api/analytics/*` must continue to read only `Trade`. There is no flag to merge them.

---

## 2. Architecture overview

```
src/TradeLedger.Core/
  Analytics/
    PerformanceMetrics.cs        NEW  pure metric computation (extracted)
    EquityMath.cs                NEW  pure drawdown computation (extracted)
    AnalyticsService.cs          MOD  becomes a thin DB wrapper over the above
  MarketData/                    NEW
    Candle.cs                    entity (shared, not user-owned)
    FundingRateHistory.cs        entity (shared)
    MarketSymbolAlias.cs         entity (shared)
    CandleImport.cs              entity (user-owned audit)
    IKlineSource.cs
    BinanceKlineClient.cs
    MarketDataOptions.cs
    CsvKlineImporter.cs
    CandleRepository.cs          gap detection, range queries
    MarketDataBackfillService.cs
  Backtesting/                   NEW
    Domain/                      BacktestAccount, BacktestStrategy, BacktestRun,
                                 BacktestTrade, BacktestExecution,
                                 BacktestEquityPoint, enums
    Rules/                       RuleDocument, RuleValidator, RuleEvaluator
    Indicators/                  Sma, Ema, Rsi, Dmi, Adx (decimal) + registry
    Engine/                      RuleEngineSimulator, IntrabarResolver,
                                 LiquidationModel, CostModel, PositionSizer
    WhatIf/                      WhatIfSimulator, transformations
    BacktestAccountService.cs    chaining, balance, stitched equity curve
    BacktestRunner.cs            dispatches a queued run to the right engine
  Shared/Proxy/
    ProxyEndpoint.cs             unchanged
    IProxiedHttpClientProvider   NEW  generalised from IBitunixHttpClientProvider

src/TradeLedger.Api/Features/
  Backtests/                     NEW  accounts, strategies, runs, results
  MarketData/                    NEW  backfill, import, coverage

src/TradeLedger.Worker/
  BacktestWorker.cs              NEW  drains the run queue
  MarketDataWorker.cs            NEW  drains the backfill queue
```

Two new Postgres schemas, following the existing `core` / `identities` convention:

- **`market`** — candles, funding rates, symbol aliases. Shared reference data, **not** user-owned.
- **`backtest`** — accounts, strategies, runs, simulated trades, equity points. All user-owned.

---

## 3. Phase 0 — metrics refactor (prerequisite, no new feature)

Backtests are scored by the same code as live trades. `AnalyticsService` currently queries `db.Trades` inline, so it cannot score anything else. Extract the arithmetic; leave behaviour identical.

### 3.1 `PerformanceMetrics`

```csharp
namespace TradeLedger.Core.Analytics;

public readonly record struct TradeMetricInput(
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    decimal GrossProfitLoss,
    decimal Fees,
    decimal Funding,
    decimal NetProfitLoss,
    TradeOutcome Outcome,
    decimal? AchievedReturnR,
    decimal? PlannedReturnR,
    bool IsPlanned,
    TimeSpan? Duration);

public static class PerformanceMetrics
{
    public static PerformanceSummary Compute(IReadOnlyList<TradeMetricInput> trades);
}
```

Move the body of `AnalyticsService.GetSummaryAsync` — everything after the `ToListAsync` — into `Compute`, unchanged. `LongestStreak` moves with it and takes `IEnumerable<TradeMetricInput>`.

### 3.2 `EquityMath`

```csharp
public static class EquityMath
{
    public static EquityCurve Analyse(IReadOnlyList<EquityPoint> points);
}
```

Move the peak / max-drawdown / current-drawdown loop out of `GetEquityCurveAsync`. `AnalyticsService` keeps the `BalanceSnapshot` query and the `GroupBy(CapturedAt)` collapse, then calls `Analyse`. The backtest engine builds its points from simulated balances and calls the same method. So does the account-level stitched curve (§8.1).

### 3.3 Acceptance

- `AnalyticsService` public signatures unchanged.
- A golden test fixture of ~30 trades produces byte-identical `PerformanceSummary` and `EquityCurve` JSON before and after. Write this test **against the current code first**, then refactor.
- `PerformanceMetrics` and `EquityMath` are static, allocation-light, and take no `DbContext`.

---

## 4. Market data

### 4.1 `Candle` and the 15-minute floor

Shared reference data in schema `market`. **Not `IUserOwned`** — market data is public and identical for every user; duplicating it per tenant would multiply millions of rows for no benefit. The global query filter therefore does not apply.

```csharp
public sealed class Candle
{
    public long Id { get; set; }
    public CandleSource Source { get; set; }
    public required string Symbol { get; set; }
    public CandleInterval Interval { get; set; }
    public DateTimeOffset OpenTime { get; set; }
    public long OpenTimeRawMs { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
    public decimal? QuoteVolume { get; set; }
    public int? TradeCount { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
}

public enum CandleSource { BinanceFutures = 1, BinanceSpot = 2, CsvImport = 3 }

public enum CandleInterval
{
    OneMinute = 1,
    FifteenMinutes = 4,
    ThirtyMinutes = 5,
    OneHour = 6,
    TwoHours = 7,
    FourHours = 8,
    SixHours = 9,
    TwelveHours = 10,
    OneDay = 11,
    OneWeek = 12,
}
```

**Three-minute and five-minute candles are gone entirely.** They serve neither trading nor resolution.

> ### Decision to confirm: 1-minute candles are kept, but only as drill-down data
>
> `OneMinute` is **not a tradeable interval**. `POST /api/backtests` rejects it, the run-interval picker excludes it, and no strategy can be written against it. Its only use is answering "which was hit first, the stop or the target?" inside a 15-minute-or-larger bar (§6.3).
>
> This is deliberate, because the two decisions collide: "full intrabar modelling, resolved by drilling into a lower timeframe" needs data finer than the run interval, and "nothing under 15 minutes" removes it. Keeping 1m as invisible resolution data honours both — you never trade it, never see it, never write a rule against it.
>
> **Confirmed by the owner: 1-minute candles are kept.** The alternative was raising the drill-down floor to `FifteenMinutes`, which would have cost intrabar resolution on 15-minute runs entirely.

- `Id` is `long` (identity), not `Guid` — this table reaches millions of rows and the index size matters.
- OHLC use `Precision.Price`. `Volume` and `QuoteVolume` use `Precision.Quantity`. **No `double` anywhere**, per the project rule.
- Unique index: `(Source, Symbol, Interval, OpenTime)`. All writes are upserts; re-fetching a range must be a no-op.
- `OpenTime` is the candle's **open**, always UTC, always aligned to the interval boundary. Store the raw epoch ms alongside, per the project rule.
- Only **closed** candles are stored. An in-progress candle is never persisted — it would change under the engine's feet and break determinism.

**Sizing note.** One symbol of 1-minute candles is ~525,600 rows/year, roughly 60–80 MB/year with indexes. Tradeable intervals are far smaller: 15m is ~35,000 rows/year, 4h is ~2,200. The drill-down set dominates storage. `GET /api/market-data/coverage` must report row counts and on-disk size per interval so the growth is visible, and deleting a `(source, symbol, interval)` range must be supported.

### 4.2 `FundingRateHistory`

Shared, schema `market`. Needed for honest futures PnL.

```csharp
public sealed class FundingRateHistory
{
    public long Id { get; set; }
    public CandleSource Source { get; set; }
    public required string Symbol { get; set; }
    public DateTimeOffset FundingTime { get; set; }
    public long FundingTimeRawMs { get; set; }
    public decimal FundingRate { get; set; }
}
```

`FundingRate` uses `Precision.Ratio`. Unique index `(Source, Symbol, FundingTime)`.

### 4.3 `MarketSymbolAlias`

Bitunix and Binance mostly agree on USDT-perp symbols, but not always. A run stores a canonical symbol; this maps it per source.

```csharp
public sealed class MarketSymbolAlias
{
    public int Id { get; set; }
    public required string CanonicalSymbol { get; set; }
    public CandleSource Source { get; set; }
    public required string SourceSymbol { get; set; }
}
```

Unique index `(CanonicalSymbol, Source)`. Resolution falls back to the canonical symbol unchanged when no alias row exists.

### 4.4 `CandleImport`

User-owned audit row, schema `backtest`, written on every CSV import: file name, byte size, SHA-256 of the content, source, symbol, interval, row count, first and last `OpenTime`, rows inserted vs. skipped-as-duplicate, and any validation warnings. Candles themselves stay shared; this records who put them there.

### 4.5 Binance client

```csharp
public interface IKlineSource
{
    Task<IReadOnlyList<Candle>> FetchAsync(
        CandleSource source, string symbol, CandleInterval interval,
        DateTimeOffset from, DateTimeOffset to,
        ProxyEndpoint? proxy, CancellationToken ct);
}
```

Endpoints (**verify against live responses before relying on them, and update the market-data doc in the same change**, per the project's working agreement that the live response wins):

| Purpose | Endpoint |
|---|---|
| USD-M futures klines | `GET https://fapi.binance.com/fapi/v1/klines` |
| Spot klines | `GET https://api.binance.com/api/v3/klines` |
| Funding rate history | `GET https://fapi.binance.com/fapi/v1/fundingRate` |

- Parameters: `symbol`, `interval`, `startTime`, `endTime`, `limit`. Futures `limit` maxes at 1500, spot at 1000. Page forward by setting `startTime` to the last returned `openTime + 1ms`.
- Keyless and public — **no credentials, no signing**. Do not reuse `BitunixSigner`.
- Response is an array of arrays. Deserialise positionally: `[openTime, open, high, low, close, volume, closeTime, quoteVolume, tradeCount, takerBuyBase, takerBuyQuote, ignore]`. Numbers arrive as JSON **strings** — parse to `decimal` with `CultureInfo.InvariantCulture` and explicit `NumberStyles`, exactly as `BitunixJson` already does.
- **Futures klines for a futures backtest.** The owner trades perpetual futures on Bitunix; spot candles differ (no funding, different liquidity). Default `CandleSource.BinanceFutures`.
- Interval mapping is explicit: `15m`, `30m`, `1h`, `2h`, `4h`, `6h`, `12h`, `1d`, `1w`, plus `1m` for drill-down.
- Rate limiting: Binance is weight-based per IP. Implement `IKlineRateLimiter` mirroring the existing `IBitunixRateLimiter` Redis token-bucket pattern, budgeted conservatively (default 20 requests/second, configurable), keyed by the **proxy exit identity** rather than by user — the limit applies to the IP, and that is the proxy's.
- Retry `429` and `418` by honouring `Retry-After`; treat repeated `418` as fatal for the job and surface it on the run.

### 4.6 Proxy generalisation

Binance goes through the same per-user proxy as Bitunix. `IBitunixHttpClientProvider` is hardcoded to Bitunix's base address and options, so generalise it rather than copying it:

```csharp
public interface IProxiedHttpClientProvider : IDisposable
{
    HttpClient GetClient(string poolName, Uri baseAddress, ProxyEndpoint? proxy);
    void Remove(ProxyEndpoint proxy);
}
```

- Cache key becomes `poolName + "|" + (proxy?.CacheKey ?? "direct")`, so Bitunix and Binance keep separate socket pools through the same proxy.
- **`ProxyRequiredException` semantics are preserved unchanged.** If `Proxy:Required` is true and no proxy resolves, the market-data fetch is refused before a socket opens, exactly like Bitunix. The existing tests covering this must keep passing.
- `IBitunixHttpClientProvider` is reimplemented as a thin adapter over the generalised provider, or retired and its call site updated. Either way the Bitunix behaviour and its existing test suite are unchanged.
- `Remove(proxy)` must evict every pool for that proxy, not just one, so `PUT /api/proxy` still drops all stale connections.

### 4.7 Gap detection — the engine fails closed

A backtest over gapped candles silently lies. Detection is mandatory and refusal is the default, matching the `Proxy:Required` philosophy already in the project.

`CandleRepository.FindGapsAsync(source, symbol, interval, from, to)` returns the missing `OpenTime` ranges by walking expected interval boundaries against what is stored.

- `POST /api/backtests` validates coverage **before** queueing and returns `400` listing the gaps if any exist. Coverage is checked for the run interval **and** for the drill-down interval over the same span.
- The run re-checks at execution time, because data can be deleted between queueing and running.
- `BacktestRun.AllowGaps` (default `false`) overrides this. When true, gaps are recorded on the run, surfaced in the result, and the result is stamped `DataQuality = Gapped`. A gapped result must be visibly marked everywhere it is displayed.
- Missing **drill-down** candles are not a hard failure — they degrade resolution to the pessimistic assumption, which is recorded per trade (§6.3) and counted on the run.
- Exchange downtime produces real gaps that will never fill. `AllowGaps` is the escape hatch for those; it is not a default.

### 4.8 CSV import

`POST /api/market-data/import` — multipart upload.

- Required columns, case-insensitive, order-independent: a time column (`time`, `date`, `datetime`, `open time`, or `timestamp`), plus `open`, `high`, `low`, `close`. `volume` optional, defaults to 0.
- Time parsing accepts ISO-8601, `yyyy-MM-dd HH:mm:ss`, and epoch seconds/milliseconds. Values without an explicit offset are **assumed UTC** and the response says so explicitly.
- Caller supplies `symbol` and `interval`; they are not inferred from the file. `interval` must be a permitted value — 15m or larger, or 1m for drill-down.
- Validation, all of which reject the file: non-monotonic timestamps, timestamps not aligned to the interval, `high < low`, `open`/`close` outside `[low, high]`, negative volume, more than 1000 duplicate timestamps.
- Duplicates against existing rows are skipped, not overwritten, and counted in the response.
- Max upload 50 MB.
- Imported rows get `Source = CsvImport` and are thereby never mixed with Binance data in a single run — `CandleSource` is part of the run's identity.

### 4.9 Market data API

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/market-data/coverage` | Per `(source, symbol, interval)`: first and last candle, row count, gap count, approximate size |
| `POST` | `/api/market-data/backfill` | Queue a fetch. Body: source, symbol, interval, from, to. Returns `202` + job id |
| `GET` | `/api/market-data/backfill/{id}` | Job status and progress |
| `POST` | `/api/market-data/import` | CSV upload |
| `GET` | `/api/market-data/gaps` | Gaps for a `(source, symbol, interval, from, to)` |
| `DELETE` | `/api/market-data/candles` | Delete a `(source, symbol, interval)` range. Requires explicit `from`/`to`. |

Every endpoint carries `.WithSummary()`, `.WithDescription()` and `.Produces<T>()` per the project's OpenAPI rules, returns a response DTO rather than an entity, and uses nullable types for optional query parameters.

---

## 5. Rule definition

### 5.1 `BacktestStrategy`

```csharp
public sealed class BacktestStrategy : IUserOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string RuleJson { get; set; }
    public required string RuleHash { get; set; }
    public int Version { get; set; } = 1;
    public Guid? StrategyTermId { get; set; }
    public TaxonomyTerm? StrategyTerm { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

- `RuleJson` is `jsonb`.
- `RuleHash` is SHA-256 of the canonicalised (key-sorted, whitespace-stripped) rule JSON. A run records the hash it executed, so a result stays explainable after the strategy is edited.
- **Editing a strategy bumps `Version` and never mutates past runs.** Runs reference the strategy id *and* store their own copy of `RuleJson` + `RuleHash`.
- `StrategyTermId` optionally links to the existing `Strategy` taxonomy so a backtest can be compared against live trades carrying the same label.

### 5.2 Rule document schema

Version 1. A strategy is: a set of named indicators, an entry condition per side, and a stop-loss definition. **There is no sizing block and no take-profit block** — position size comes from configuration (§5.5) and the target is always R:R 2 (§5.4).

```json
{
  "version": 1,
  "indicators": [
    { "id": "emaFast", "type": "Ema", "source": "Close", "params": { "period": 21 } },
    { "id": "emaSlow", "type": "Ema", "source": "Close", "params": { "period": 55 } },
    { "id": "rsi",     "type": "Rsi", "source": "Close", "params": { "period": 14 } },
    { "id": "dmi",     "type": "Dmi",                    "params": { "period": 14 } },
    { "id": "adx",     "type": "Adx",                    "params": { "period": 14 } }
  ],
  "entry": {
    "long": {
      "op": "And",
      "operands": [
        { "op": "CrossesAbove", "left": { "ref": "emaFast" }, "right": { "ref": "emaSlow" } },
        { "op": "GreaterThan",  "left": { "ref": "adx" },     "right": { "const": 20 } },
        { "op": "GreaterThan",  "left": { "ref": "dmi", "output": "PlusDi" },
                                "right": { "ref": "dmi", "output": "MinusDi" } },
        { "op": "LessThan",     "left": { "ref": "rsi" },     "right": { "const": 70 } }
      ]
    },
    "short": {
      "op": "And",
      "operands": [
        { "op": "CrossesBelow", "left": { "ref": "emaFast" }, "right": { "ref": "emaSlow" } },
        { "op": "GreaterThan",  "left": { "ref": "adx" },     "right": { "const": 20 } },
        { "op": "GreaterThan",  "left": { "ref": "dmi", "output": "MinusDi" },
                                "right": { "ref": "dmi", "output": "PlusDi" } },
        { "op": "GreaterThan",  "left": { "ref": "rsi" },     "right": { "const": 30 } }
      ]
    }
  },
  "stopLoss": { "kind": "Percent", "percent": 1.5 }
}
```

**Operands**

| Form | Meaning |
|---|---|
| `{ "ref": "emaFast" }` | Current bar's value of that indicator |
| `{ "ref": "dmi", "output": "PlusDi" }` | A named sub-output of a multi-output indicator |
| `{ "ref": "emaFast", "offset": 1 }` | Value `offset` bars back. `offset` must be ≥ 0 and ≤ 500. |
| `{ "price": "Close" }` | `Open` / `High` / `Low` / `Close` / `Volume`, `offset` allowed |
| `{ "const": 70 }` | A `decimal` literal |

**Operators**

- Logical: `And`, `Or`, `Not` (`Not` takes a single `operand`; `And`/`Or` take `operands`, minimum 2)
- Comparison: `GreaterThan`, `GreaterOrEqual`, `LessThan`, `LessOrEqual`, `EqualTo`, `NotEqualTo`
- Cross: `CrossesAbove`, `CrossesBelow` — true when `left` was on the other side of `right` on the previous bar and is on this side now. Requires both operands to have a previous value; false on the first evaluable bar.
- Range: `Between` (`left`, `low`, `high`, inclusive)
- Trend: `RisingFor` (`operand`, `bars`), `FallingFor` (`operand`, `bars`) — strictly monotonic over that many bars

### 5.3 Indicators

**Exactly five: `Sma`, `Ema`, `Rsi`, `Dmi`, `Adx`.** No others are implemented, and adding one is a spec change, not a config change.

| Type | Params | Outputs | Warmup bars |
|---|---|---|---|
| `Sma` | `period`, `source` | single | `period` |
| `Ema` | `period`, `source` | single | `3 × period` |
| `Rsi` | `period`, `source` | single | `5 × period` |
| `Dmi` | `period` | `PlusDi`, `MinusDi` | `5 × period` |
| `Adx` | `period` | single | `5 × period` |

- `source` is `Open` / `High` / `Low` / `Close` / `Volume`, default `Close`. `Dmi` and `Adx` derive from the full OHLC bar and take no `source`.
- All computed in **`decimal`**, per the project's prohibition on `double`/`float` for prices. This rules out `Skender.Stock.Indicators` and comparable libraries, which return `double` series — **implement these five in-house**. With only five indicators this is a contained amount of work.
- `Rsi`, `Dmi` and `Adx` use **Wilder's smoothing**, not a simple average. Their warmup is generous because Wilder recursion converges slowly; these figures are conservative by design.
- `Dmi` and `Adx` both need True Range internally. Compute it as a shared private helper. **ATR is not exposed as a user-facing indicator** — nothing in the rule schema can reference it.
- **No square root is required by any of the five.** The `DecimalMath.Sqrt` helper from the previous revision is no longer needed and must not be written.
- Each indicator gets a unit test against a published reference vector.
- The engine does not evaluate entry conditions until every referenced indicator is warm. Warmup candles are fetched **before** the run's `From` date and excluded from results — a run from 1 January with a 55-period EMA on 4h candles silently needs 165 prior bars, and the coverage check must account for it.

### 5.4 Exits — stop-loss and take-profit only

A position is closed by exactly one of four things:

| Reason | Cause |
|---|---|
| `StopLoss` | Price reached the stop |
| `TakeProfit` | Price reached the target |
| `Liquidation` | Margin exhausted (§6.4) — involuntary, still modelled |
| `EndOfData` | The run's date range ended with the position open |

There is **no signal exit, no time stop and no discretionary exit.** A position opened by the rules stays open until the stop or target is hit. An opposite entry signal while in position is ignored.

**Stop-loss** is declared per strategy. Two kinds:

| Kind | Fields | Meaning |
|---|---|---|
| `Percent` | `percent` | Stop at `entry × (1 ∓ percent/100)` |
| `IndicatorLevel` | `ref`, optional `offset`, optional `bufferPercent` | Stop at an indicator's value, e.g. below the slow EMA |

`IndicatorLevel` must resolve to a level on the correct side of entry — below for a long, above for a short. If it does not at entry time, the entry is **skipped** and counted in `SkippedInvalidStop` on the run. This is the honest outcome; silently substituting a different stop would misrepresent the strategy.

**Take-profit is not declared.** It is always derived:

```
target = entry ± (|entry − stop| × RiskRewardRatio)
```

`RiskRewardRatio` comes from configuration, **minimum 2**, default exactly 2 (§5.5). A run records the value it used.

### 5.5 Position sizing — static, from configuration

Risk per position is a **fixed setting, not a per-trade computation and not part of the rule document.**

```
riskAmount = accountEquityAtEntry × (RiskPercentPerPosition / 100)
quantity   = riskAmount / |entry − stop|
notional   = quantity × entry
margin     = notional / leverage
```

- `RiskPercentPerPosition` comes from `Backtest:RiskPercentPerPosition` in configuration. **Validated to the 1–5 range**; a value outside it fails startup rather than being clamped silently.
- `Leverage` comes from `Backtest:DefaultLeverage`, capped by `Backtest:MaxLeverage`. It does not change position size — size is set by risk and stop distance — but it determines margin and therefore the liquidation price.
- `accountEquityAtEntry` is the running balance of the **backtest account** (§8.1), so risk compounds across a chain of runs exactly as it would in reality.
- If the computed margin exceeds available equity, the entry is **skipped** and counted in `SkippedInsufficientMargin`. It is never silently shrunk.

> ### Per-run override
>
> **Confirmed by the owner.** A run may override `RiskPercentPerPosition`, still bounded to 1–5, and records the value it used. Configuration remains the source of the default.

### 5.6 Validation

`RuleValidator` runs on every create/update and again before execution. Rejections return `400` with a field path and message.

Checks: schema version supported; indicator ids unique and non-empty; every indicator `type` is one of the five; every `ref` resolves to a declared indicator; `output` valid for that type (`PlusDi`/`MinusDi` only on `Dmi`, and required there); `period` ≥ 1 and ≤ 1000; `offset` ≥ 0 and ≤ 500; `source` not supplied for `Dmi`/`Adx`; at least one of `entry.long` / `entry.short` present; every condition tree well-formed and no deeper than 20 levels; `stopLoss` present and its kind valid; `Percent` stop between 0.1 and 50; `IndicatorLevel` stop references a declared indicator.

There are no cycles to detect — indicators reference candle data, never each other.

---

## 6. Rule engine execution

### 6.1 No lookahead — the single most important rule

**Signals are evaluated only on closed bars, and entries fill at the next bar's open.**

A condition referencing bar *n* may use only data from bars ≤ *n*. The resulting order is placed at the open of bar *n+1*. Any implementation that fills at bar *n*'s close, or consults bar *n+1* while evaluating bar *n*, is wrong and makes every result worthless.

This must be enforced structurally, not by care: the evaluator receives a bar-indexed view that **cannot** address beyond the current index. The test suite includes a strategy that is only profitable with lookahead, asserting it produces the honest result.

### 6.2 Simulation loop

For each closed bar after warmup:

1. **If in position** — resolve stop, target and liquidation for this bar in intrabar order (§6.3). Close if any is hit.
2. **If flat** — evaluate `entry.long` and `entry.short` on this bar. If exactly one is true, queue an entry for the next bar's open. If both are true, take neither and count it as `AmbiguousSignal` on the run.
3. **Apply any queued entry** at this bar's open: resolve the stop level, derive the target at R:R (§5.4), size the position from account equity (§5.5), deduct the entry fee. Skip and count if the stop is invalid or margin is insufficient.
4. **Accrue funding** if a funding timestamp falls inside this bar and a position is open (§6.5).
5. **Record equity** (§6.7).

One position at a time. No pyramiding, no scale-ins, no reversal within a bar: if an exit fires on a bar, the earliest possible new entry is the following bar's open.

### 6.3 Intrabar resolution

For each bar while in position, determine which of stop, target and liquidation was touched first.

1. Compute which levels lie within `[Low, High]` of the bar.
2. **Zero touched** — no exit; continue.
3. **Exactly one touched** — fill at that level. Record `IntrabarResolution = Unambiguous`. No drill-down.
4. **Two or more touched** — drill down:
   - Fetch the **1-minute** candles covering this bar's span for the same `(source, symbol)`.
   - Walk them in order; the first minute whose range contains a level decides. Record `ResolvedByMinute`.
   - If one minute contains several levels, 1m is the floor — apply pessimistic ordering and record `AssumedWithinMinute`.
   - If 1-minute data is unavailable for that window, apply pessimistic ordering and record `AssumedNoMinuteData`.
5. **Pessimistic ordering** is: liquidation, then stop, then target. Always the worst outcome for the trader.

The run reports a count per `IntrabarResolution` value. A run where most exits were assumed rather than resolved is a weak result and must say so.

Because R:R is fixed at 2 and the stop is typically 1–2%, a single 15-minute bar containing both levels is uncommon but far from rare on volatile symbols — this path matters.

If the drill-down floor is later raised to 15m (§4.1), step 4 drills to 15m instead and a 15-minute run always records `AssumedNoMinuteData` on ambiguous bars.

### 6.4 Liquidation

Modelled, because leverage without liquidation overstates results badly.

Approximate linear USDT-perpetual liquidation price, isolated margin:

- Long: `entry × (1 − 1/leverage + maintenanceMarginRate)`
- Short: `entry × (1 + 1/leverage − maintenanceMarginRate)`

`maintenanceMarginRate` is a run parameter, default `0.005` (0.5%).

**This is an approximation and must be labelled as one.** Real exchanges apply tiered maintenance margin that rises with position size, and include unrealised PnL from other positions under cross margin. Neither is modelled. Runs are isolated-margin only; `BacktestRun.MarginMode` is fixed to `Isolated` with the field present for future use.

Note that with risk-based sizing at 1–5% and a stop of 1–2%, the stop is normally far closer than the liquidation price, so liquidation should be rare. When it is *not* rare, that is a signal the leverage setting is too high — and the run reporting it is exactly the point.

A liquidation closes the position at the liquidation price, zeroes the margin, sets `ExitReason = Liquidation` and `WasLiquidated = true`.

### 6.5 Costs

All configurable per run:

| Parameter | Default | Applies |
|---|---|---|
| `TakerFeeRate` | `0.0006` | Market entries, stop fills, liquidations |
| `MakerFeeRate` | `0.0002` | Limit fills (none in v1; present for later) |
| `SlippageRate` | `0.0005` | Adverse price adjustment on market and stop fills |
| `IncludeFunding` | `true` | Funding accrual |
| `MaintenanceMarginRate` | `0.005` | Liquidation price |

Defaults approximate Bitunix USDT-perp public rates. **Verify against the owner's actual fee tier before trusting a result**; they are run parameters precisely so they can be corrected without a code change.

Slippage is applied against the trader: entry long fills higher, entry short fills lower, stop-outs fill worse. Take-profit fills receive no favourable slippage.

Funding: at each funding timestamp inside an open position, `payment = positionNotional × fundingRate`, signed so longs pay a positive rate. Accumulates into `BacktestTrade.Funding` and flows into net PnL through the same `NetProfitLoss = Gross − Fees + Funding` identity that live `Trade.Recalculate` uses. If `FundingRateHistory` has no row for a timestamp in range, record a warning on the run and treat that interval as zero rather than aborting.

### 6.6 Determinism

Given identical `RuleJson`, date range, candle rows, run parameters, opening balance and `EngineVersion`, a run produces byte-identical results.

- `EngineVersion` is a constant bumped whenever simulation semantics change. Stored on every run.
- No wall-clock reads, no RNG, no parallelism inside a single run's bar loop.
- A test runs the same fixture twice and asserts identical output. Ordering and comparison are by `Sequence`, never by id.

### 6.7 Equity and drawdown

Two granularities, deliberately:

- **Per-closed-trade equity points** are persisted as `BacktestEquityPoint` rows and returned as the curve. One row per closed trade, plus an opening point. Bounded and cheap.
- **Intrabar peak-to-trough drawdown** is tracked during the run as a running scalar, marked to market against each bar's adverse extreme (`Low` for a long, `High` for a short), and stored as `MaxIntrabarDrawdown` / `MaxIntrabarDrawdownPercent`.

A per-bar equity series would be millions of rows per run; per-trade points alone would understate drawdown by ignoring open-position excursions. This gives an honest maximum without the storage.

`MaxDrawdown` is computed by `EquityMath.Analyse` over the per-trade points, defined identically to the live one. `MaxIntrabarDrawdown` is reported alongside and is always ≥ it.

Per trade, also record `MaeR` and `MfeR` — maximum adverse and favourable excursion in R multiples.

---

## 7. What-if engine

### 7.1 Scope — all transformations, one release

All four transformation groups ship together. This means the what-if engine depends on the market-data stack and the intrabar resolver, because exit-rule replay cannot be answered without candles. It is not separately shippable ahead of them, and the delivery order in §12 reflects that.

### 7.2 Transformations

**Sizing** — one of:

| Kind | Effect |
|---|---|
| `ScaleBy { factor }` | Multiply every position's quantity |
| `StaticRisk { riskPercent }` | Re-size each trade so its stop distance risks that percentage of running equity. 1–5. |
| `CapRiskAt { maxPercent }` | Leave trades alone unless they risked more, then scale down |

Recomputation: gross PnL is linear in quantity for a fixed entry and exit, so `newGross = oldGross × (newQty / oldQty)`. Fees and funding scale with notional the same way. `StaticRisk` and `CapRiskAt` require `StopLossPrice` and `Quantity`; trades lacking a stop are passed through unchanged and counted in `SkippedForMissingStop`.

**Filters** — include or exclude, combined with AND:

`strategyId`, `symbol`, `side`, `timeframeId`, `entryMentalStateId`, `exitMentalStateId`, `dayOfWeek`, `hourOfDayRange`, `mistakeTermId`, `isPlanned`, `minRating`, `accountId`, date range.

**Costs**: `FeeRateOverride` (recompute fees from notional at a different rate), `IncludeFunding`, `IncludeFees`.

**Exit rules** — requires candles for the trade's symbol and window:

| Kind | Question |
|---|---|
| `FixedRMultiple { multiple }` | "What if I had always taken exactly 2R?" |
| `MoveStopToBreakeven { atR }` | "What if I had moved the stop to entry once 1R was reached?" |
| `EnforceMinimumRr { ratio }` | "What if I had only taken trades offering R:R ≥ 2, and held them to that target?" |

Each resolves against real candles from `OpenedAt` forward, using the **same `IntrabarResolver`** as the rule engine (§6.3) — so a what-if exit and a rule-engine exit are decided by identical logic, including the pessimistic fallback.

Trades whose symbol has no candle coverage are **passed through unchanged** and counted in `SkippedNoCandleData`. The result reports that count; a what-if where most trades were skipped is a weak result and must say so.

Symbol mapping matters here: a Bitunix symbol must resolve through `MarketSymbolAlias` to a Binance symbol, and a failure to map is a skip, not an error.

### 7.3 The honest caveat

**Resizing does not model liquidation or margin.** A trade scaled up 4× might have been liquidated before reaching its actual exit, and this engine will happily report the 4× profit. That is the main way a what-if lies.

Mitigation, required: for each resized trade, compute the approximate liquidation price from the new leverage and margin (§6.4) and compare it against the trade's recorded stop. When the liquidation price would sit between entry and stop, count the trade in `LiquidationRiskCount` and flag it. The result must surface this count prominently. Where candle data exists for that symbol and window, also check the trade's actual adverse excursion against the liquidation price and report a confirmed count separately from the suspected one.

`ScaleBy { factor: 1 }` with no filters, no cost overrides and no exit rules must reproduce the live `PerformanceSummary` exactly. That is a test.

### 7.4 Equity curve

Rebuilt forward from the backtest account's opening balance, applying transformed trades in `ClosedAt` order, then fed to `EquityMath.Analyse` — giving drawdown defined identically to live.

---

## 8. Backtest storage

Schema `backtest`. All user-owned (`IUserOwned`), so the existing global query filter applies automatically.

### 8.1 `BacktestAccount`

A persistent simulated balance that many runs compound onto. This is what makes "one strategy over thousands of trades" a single continuous curve rather than a pile of unrelated runs.

**It is deliberately not an `AccountKind` on the live `Account` entity** — the segregation decision was separate entities entirely, and adding a `Backtest` kind would put simulated balances one forgotten filter away from the real equity curve.

```csharp
public sealed class BacktestAccount : IUserOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }

    public required string Name { get; set; }
    public string? Description { get; set; }

    public decimal StartingBalance { get; set; }
    public string Currency { get; set; } = "USDT";

    public BacktestAccountMode Mode { get; set; } = BacktestAccountMode.Sequential;

    public Guid? BacktestStrategyId { get; set; }
    public BacktestStrategy? BacktestStrategy { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<BacktestRun> Runs { get; set; } = [];
}

public enum BacktestAccountMode { Sequential = 1, Independent = 2 }
```

**`Sequential`** (default) — runs on the account are chained in date order. Each run opens at the previous run's closing balance, so position sizing compounds. To keep this honest, **a run whose `[From, To]` overlaps an existing succeeded run on the same account is rejected with `409`.** Compounding overlapping periods would double-count the same market twice and is fiction.

**`Independent`** — every run opens at `StartingBalance`. The account is a folder for comparing variants of one strategy. No compounding, overlap permitted.

Derived, never stored denormalised:

- `CurrentBalance` = `StartingBalance` + sum of `NetProfitLoss` across succeeded runs (Sequential), or `StartingBalance` (Independent).
- The **stitched equity curve** concatenates each succeeded run's `BacktestEquityPoint` rows in date order and passes them to `EquityMath.Analyse`. One drawdown figure across the whole history, defined identically to the live one.

Optionally bound to a `BacktestStrategy`, so "this account *is* this strategy's track record" is expressible. Leaving it null allows a mixed account.

Index: `(UserId, CreatedAt DESC)`.

### 8.2 `BacktestRun`

```csharp
public sealed class BacktestRun : IUserOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }

    public Guid BacktestAccountId { get; set; }
    public BacktestAccount? BacktestAccount { get; set; }

    public BacktestKind Kind { get; set; }
    public BacktestStatus Status { get; set; } = BacktestStatus.Queued;

    public Guid? BacktestStrategyId { get; set; }
    public BacktestStrategy? BacktestStrategy { get; set; }
    public string? RuleJson { get; set; }
    public string? RuleHash { get; set; }

    public string? Symbol { get; set; }
    public CandleSource? Source { get; set; }
    public CandleInterval? Interval { get; set; }
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }

    public decimal OpeningBalance { get; set; }
    public decimal? ClosingBalance { get; set; }

    public decimal RiskPercentPerPosition { get; set; }
    public decimal RiskRewardRatio { get; set; }
    public int Leverage { get; set; }

    public string ParametersJson { get; set; } = "{}";
    public string? WhatIfJson { get; set; }

    public bool AllowGaps { get; set; }
    public DataQuality DataQuality { get; set; } = DataQuality.Clean;

    public int EngineVersion { get; set; }
    public int TotalBars { get; set; }
    public int BarsProcessed { get; set; }
    public decimal ProgressPercent { get; set; }
    public bool CancellationRequested { get; set; }

    public DateTimeOffset QueuedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? Error { get; set; }

    public string? ResultJson { get; set; }
    public string? WarningsJson { get; set; }

    public ICollection<BacktestTrade> Trades { get; set; } = [];
    public ICollection<BacktestEquityPoint> EquityPoints { get; set; } = [];
}

public enum BacktestKind { RuleEngine = 1, WhatIf = 2 }
public enum BacktestStatus { Queued = 0, Running = 1, Succeeded = 2, Failed = 3, Cancelled = 4 }
public enum DataQuality { Clean = 0, Gapped = 1 }
```

- `OpeningBalance` is resolved **at execution time**, not at queue time — two runs queued together on a sequential account must chain correctly.
- `RiskPercentPerPosition`, `RiskRewardRatio` and `Leverage` are **snapshotted onto the run** from configuration. Changing the config later must not silently change what an old result meant.
- `Interval` must not be `OneMinute`; validation rejects it.
- `ResultJson` holds the computed `PerformanceSummary` plus engine-specific counters, so a finished run reads back without recomputation.
- Money columns use `Precision.Money`; `ProgressPercent`, `RiskPercentPerPosition` and `RiskRewardRatio` use `Precision.Ratio`.
- Indexes: `(UserId, QueuedAt DESC)`, `(Status, QueuedAt)` for the worker queue scan, and `(BacktestAccountId, From)` for overlap checks and curve stitching.

### 8.3 `BacktestTrade`

Mirrors the mechanical half of `Trade` — deliberately **not** sharing the entity — plus engine-specific fields:

`Id`, `UserId`, `BacktestRunId`, `Sequence`, `Symbol`, `Side`, `OpenedAt`, `ClosedAt`, `EntryBarIndex`, `ExitBarIndex`, `EntryPrice`, `ExitPrice`, `Quantity`, `Leverage`, `PositionMargin`, `OrderValue`, `StopLossPrice`, `TakeProfitPrice`, `LiquidationPrice`, `GrossProfitLoss`, `Fees`, `Funding`, `NetProfitLoss`, `AchievedReturnR`, `PlannedReturnR`, `TradeGainPercent`, `BalanceAfter`, `Duration`, `BarsInTrade`, `Outcome`, `ExitReason`, `IntrabarResolution`, `WasLiquidated`, `MaeR`, `MfeR`, `SourceTradeId` (what-if only — the real `Trade.Id` it derives from), `Notes`.

`PlannedReturnR` is the run's `RiskRewardRatio` for every rule-engine trade, by construction.

```csharp
public enum BacktestExitReason
{
    StopLoss = 1, TakeProfit = 2, Liquidation = 3, EndOfData = 4,
}

public enum IntrabarResolution
{
    Unambiguous = 0, ResolvedByMinute = 1,
    AssumedWithinMinute = 2, AssumedNoMinuteData = 3,
}
```

`EndOfData` closes any position still open at the run's `To` at the final close. Those trades are reported separately so they can be excluded from judgement, and — importantly for a sequential account — their PnL still affects the next run's opening balance.

Index: `(BacktestRunId, Sequence)`.

### 8.4 `BacktestExecution`

Individual simulated fills: `Id`, `UserId`, `BacktestRunId`, `BacktestTradeId`, `Role`, `Price`, `Quantity`, `Fee`, `ExecutedAt`, `BarIndex`. In v1 each trade has exactly one entry and one exit, but the shape is here so scale-ins can be added later without a migration that rewrites history.

### 8.5 `BacktestEquityPoint`

`Id`, `UserId`, `BacktestRunId`, `Sequence`, `At`, `Equity`, `Drawdown`, `DrawdownPercent`. Index `(BacktestRunId, Sequence)`.

### 8.6 Deletion

- Deleting a `BacktestRun` cascades to its trades, executions and equity points. On a **sequential** account, deleting a run that is not the most recent invalidates the chain — the API rejects it and requires deleting later runs first, or converting the account to `Independent`.
- Deleting a `BacktestStrategy` does **not** cascade to runs — runs keep their own `RuleJson` copy and become orphaned-but-readable, with `BacktestStrategyId` set null.
- Deleting a `BacktestAccount` cascades to every run on it, behind an explicit confirmation flag.

---

## 9. Run lifecycle and API

### 9.1 Queue and worker

Both kinds go through the same background queue. What-if runs without exit rules finish in well under a second, so the API returns `202` and the first status poll usually already has the result; that latency is accepted in exchange for one execution model rather than two.

- `POST /api/backtests` validates, checks candle coverage for the run interval and the drill-down interval, checks sequential-overlap on the account, writes a `Queued` run, returns `202` with the id and a `Location` header.
- `BacktestWorker` polls for `Queued` runs on an interval (default 2s), oldest first.
- It takes a Redis distributed lock per **account** id — not per run — using the existing `IDistributedLock`, so two runs on one sequential account cannot interleave and corrupt the balance chain.
- Progress is written at most every 2 seconds or every 1000 bars, whichever is less frequent.
- `CancellationRequested` is re-read on the same cadence; when set, the run stops, is marked `Cancelled`, and partial output is deleted so the chain stays clean.
- A worker crash leaves a run `Running` forever. The worker reclaims runs whose `StartedAt` is older than a configurable stale timeout (default 2 hours) and whose lock is gone, marking them `Failed` with a "worker lost" error rather than silently retrying.
- Concurrency: one run at a time per account, and `Backtest:MaxConcurrentRunsPerUser` (default 1) overall.

### 9.2 Endpoints

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/backtests/accounts` | List backtest accounts with current balance |
| `POST` | `/api/backtests/accounts` | Create one |
| `GET` | `/api/backtests/accounts/{id}` | One account: balance, run count, aggregate summary |
| `PUT` | `/api/backtests/accounts/{id}` | Rename, change mode, toggle active |
| `DELETE` | `/api/backtests/accounts/{id}` | Delete, cascading to runs, behind a confirmation flag |
| `GET` | `/api/backtests/accounts/{id}/equity-curve` | Stitched curve across every succeeded run |
| `GET` | `/api/backtests/accounts/{id}/summary` | `PerformanceMetrics` over every trade on the account |
| `GET` | `/api/backtests/strategies` | List rule strategies |
| `POST` | `/api/backtests/strategies` | Create; validates the rule tree |
| `GET` | `/api/backtests/strategies/{id}` | One strategy with its rules |
| `PUT` | `/api/backtests/strategies/{id}` | Update; bumps `Version`, re-validates |
| `DELETE` | `/api/backtests/strategies/{id}` | Soft delete via `IsActive` |
| `POST` | `/api/backtests/strategies/validate` | Validate a rule tree without saving |
| `GET` | `/api/backtests` | List runs, paginated, filterable by account, kind, status |
| `POST` | `/api/backtests` | Queue a rule-engine run |
| `POST` | `/api/backtests/what-if` | Queue a what-if run |
| `GET` | `/api/backtests/{id}` | Status, progress, result summary when finished |
| `POST` | `/api/backtests/{id}/cancel` | Request cancellation |
| `DELETE` | `/api/backtests/{id}` | Delete a run and its output |
| `GET` | `/api/backtests/{id}/trades` | Simulated trades, paginated |
| `GET` | `/api/backtests/{id}/equity-curve` | Equity points + drawdown stats |
| `GET` | `/api/backtests/{id}/breakdown/{dimension}` | Breakdown over simulated trades |
| `GET` | `/api/backtests/compare?ids=a,b,c` | Summaries side by side, max 5 |
| `GET` | `/api/backtests/{id}/compare-to-live` | Run summary against live `PerformanceSummary` over the same date range |

`/compare-to-live` and `/accounts/{id}/summary` are the payoff of the metrics refactor: every side comes from `PerformanceMetrics.Compute`, so comparisons are genuinely like-for-like.

### 9.3 API conventions

Non-negotiable, from the project's existing rules:

- Every endpoint has `.WithSummary()`, `.WithDescription()`, `.Produces<T>()` and the relevant `.ProducesProblem(...)`. An endpoint without a summary is incomplete.
- **Never `.Produces<T>()` an entity** — `BacktestAccount` and `BacktestRun` have navigation collections and would recurse the schema generator to `CurrentDepth (64)`. Return `BacktestAccountResponse`, `BacktestRunResponse`, `BacktestTradeResponse`, `BacktestStrategyResponse`, `BacktestEquityCurveResponse`, `MarketDataCoverageResponse`.
- **Optional query parameters are nullable** (`int?`, `bool?`) with defaults applied in the handler, or they become required and 500 when omitted.
- Enums are strings on the wire, already handled by the registered `JsonStringEnumConverter`.
- New tags `Backtests` and `Market Data` are added to `OpenApiTags.All`; **tag order there is UI render order**, so place them after `Analytics`.
- No code comments anywhere in the implementation, per the standing instruction.
- `decimal` for every price, quantity and money value. No `double`, no `float` — including inside indicator maths.
- All times `DateTimeOffset` UTC, `timestamptz` in Postgres, raw epoch ms stored alongside for exchange-sourced values.

---

## 10. Configuration

```jsonc
{
  "Backtest": {
    "EngineVersion": 1,
    "RiskPercentPerPosition": 2.0,
    "RiskRewardRatio": 2.0,
    "DefaultLeverage": 5,
    "MaxLeverage": 25,
    "MaxConcurrentRunsPerUser": 1,
    "QueuePollInterval": "00:00:02",
    "StaleRunTimeout": "02:00:00",
    "MaxBarsPerRun": 2000000,
    "DefaultTakerFeeRate": 0.0006,
    "DefaultMakerFeeRate": 0.0002,
    "DefaultSlippageRate": 0.0005,
    "DefaultMaintenanceMarginRate": 0.005
  },
  "MarketData": {
    "BinanceFuturesBaseUrl": "https://fapi.binance.com",
    "BinanceSpotBaseUrl": "https://api.binance.com",
    "RequestsPerSecond": 20,
    "MaxPageSize": 1500,
    "HttpTimeout": "00:00:30",
    "MaxImportBytes": 52428800
  }
}
```

Validated at startup, failing fast rather than clamping:

- `RiskPercentPerPosition` must be **≥ 1 and ≤ 5**.
- `RiskRewardRatio` must be **≥ 2**.
- `DefaultLeverage` must be ≥ 1 and ≤ `MaxLeverage`.

`Proxy:Required` continues to govern egress. With it true and no proxy resolved, a Binance fetch is refused before a socket opens, exactly as a Bitunix call is.

---

## 11. Testing

**Unit**

- Each of the five indicators against published reference vectors, in `decimal`. `Rsi`, `Dmi` and `Adx` specifically against Wilder-smoothed references, not simple-average ones — this is the most common way these three are implemented wrongly.
- Rule evaluator: each operator, `offset` handling, cross detection on the first evaluable bar, deep nesting, short-circuit behaviour, `Dmi` sub-output access.
- `RuleValidator`: every rejection path returns the right field path, including an indicator type outside the five and a `source` supplied to `Dmi`.
- `IntrabarResolver`: a bar containing only a stop; only a target; both, with 1m data; both, without; both inside a single minute. Assert the recorded `IntrabarResolution` in each case.
- `LiquidationModel`: long and short, isolated, across leverage values.
- `PositionSizer`: risk percent honoured exactly; target derived at exactly R:R; entry skipped on invalid stop; entry skipped on insufficient margin.
- Take-profit derivation: assert `|target − entry| == |entry − stop| × ratio` for both sides.
- What-if sizing arithmetic, including the identity case.
- `PerformanceMetrics` and `EquityMath` golden tests.

**The lookahead test.** A strategy formulated so it is profitable only if the engine peeks at the next bar. Assert the engine reports the honest result. This is the guard on the single most damaging class of bug in the feature.

**Integration** (Testcontainers Postgres, as the project already does)

- Full rule-engine run over a fixture candle set with a hand-computed expected outcome.
- Determinism: the same run twice, byte-identical output.
- **Sequential chaining**: three runs on one account chain balances correctly, and the stitched curve equals the concatenation.
- **Overlap rejection**: a run overlapping an existing one on a sequential account returns `409`; the same run on an independent account succeeds.
- **Compounding**: with `StaticRisk`, position size grows as the account grows across chained runs.
- Deleting a middle run on a sequential account is rejected.
- Gap refusal: a range with a hole returns `400` and does not queue; with `AllowGaps` it runs and is stamped `Gapped`.
- Missing drill-down data degrades resolution rather than failing the run.
- Warmup: a 55-period EMA starting at the range boundary fetches prior candles and does not evaluate before warm.
- `OneMinute` rejected as a run interval.
- Cancellation mid-run leaves `Cancelled` and no orphaned trades.
- Tenancy: accounts, runs, trades and equity points are invisible to another user. Candles, being shared, are visible to both — assert that explicitly so the intent is recorded.
- Candle upsert idempotency: fetching the same range twice writes once.
- What-if identity: no transformations reproduces the live summary exactly.
- What-if exit rules skip trades with no candle coverage and report the count.
- Proxy: a market-data fetch with `Proxy:Required` and no proxy throws `ProxyRequiredException` before connecting.
- **The existing Bitunix proxy tests must still pass unchanged** after the provider generalisation.

---

## 12. Delivery order

Because what-if is no longer split, it inherits the market-data and intrabar dependencies, so there is no early standalone win. Phases 0–2 build machinery; the first user-visible result arrives at Phase 3.

| Phase | Contents | Depends on | Status |
|---|---|---|---|
| **0** | Metrics refactor (§3) | — | **Done** |
| **1** | Market data: Binance client, proxy generalisation, gap detection, CSV import, coverage API (§4) | — | **Done** — integration tests now run and pass |
| **2** | Backtest accounts, runs, storage, queue and worker lifecycle (§8, §9) + engine core: five indicators, intrabar resolver, liquidation, cost model, position sizer (§5.3, §6.3–6.5) | 0, 1 | **Done** |
| **3** | Rule engine: rule document, validator, evaluator, simulator (§5, §6) | 2 | **Done** — validation lives inside `RuleDocumentParser` rather than a separate `RuleValidator`, so every rejection carries a JSON path; simulation core extracted as a pure class so the no-lookahead test needs no database |
| **4** | What-if engine, complete: sizing, filters, costs, exit rules (§7) | 3 | **Deferred at the owner request.** The `WhatIf` kind is rejected at queue time with `engine_unavailable` (501) rather than creating a run that could never finish. |

After Phase 3 the codebase was reworked against `docs/claude-fixes.md`: every entity encapsulated behind factories and behaviour methods, EF configurations split one per file, backtesting and market-data entities moved under `Core/Domain/`, request and response DTOs split out of the endpoint files, a `IUnitOfWork` wrapping multi-row writes in a transaction, structured `ProblemDetails` errors with stable codes, the Swagger bearer button fixed, `MarketSession` added as a derived domain concept, and the Postman collection brought up to 74 requests. See CLAUDE.md §6 for the rules that came out of it.

Phases 0 and 1 are independent and can proceed in parallel.

---

## 13. Open items

Flagged rather than guessed.

1. **Binance endpoint shapes are specified from prior knowledge, not from live calls.** Verify paths, parameter names, limits and the positional array layout during Phase 1, and update the market-data doc in the same change — the project's rule is that the live response wins.
2. **Default fee rates are approximations** of Bitunix public USDT-perp rates. Confirm against the owner's actual fee tier; they are run parameters so a correction needs no code change.
3. **Maintenance margin is flat, real exchanges tier it** by position size. Accepted for v1 and labelled in the result.
4. **Cross margin is not modelled.** Runs are isolated-margin only.
5. ~~1-minute drill-down candles~~ — **confirmed kept.** Not tradeable, drill-down only.
6. ~~Per-run risk override~~ — **confirmed kept.** Bounded 1–5, configuration is the default.
7. **Candle retention** has no policy. Growth is visible through `/api/market-data/coverage` and ranges can be deleted manually; revisit if it becomes a problem.
8. **`docs/` needs a `market-data.md`** alongside `bitunix-api.md`, written during Phase 1, covering the Binance endpoints and the CSV format.
9. **CLAUDE.md needs a backtesting section** once Phase 2 lands, covering the separate-entity rule, the no-lookahead rule, the fail-closed gap policy and the 15-minute floor.
