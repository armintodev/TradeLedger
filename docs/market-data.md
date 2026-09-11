# Market data notes

Historical candles for backtesting. Sibling of [`bitunix-api.md`](bitunix-api.md) —
same rule applies: **when a live response disagrees with this file, the live
response wins, and this file is updated in the same change.**

Bitunix is the exchange TradeLedger journals. It is **not** the candle source.
Candles come from Binance, which has a free, keyless, well-documented klines API
covering far more history.

---

## 1. The 15-minute floor

The owner does not trade anything below 15 minutes, so nothing below it is a
tradeable interval:

| Interval | Tradeable | Purpose |
|---|---|---|
| `OneMinute` | **No** | Drill-down only — deciding whether a stop or a target was hit first inside a larger bar |
| `FifteenMinutes` … `OneWeek` | Yes | Run intervals |

`OneMinute` is rejected by `POST /api/backtests` and excluded from
`CandleIntervals.Tradeable`. It is still fetchable and importable, because
without it a 15-minute bar containing both the stop and the target can only be
resolved by assuming the worst.

Three- and five-minute candles do not exist in the enum at all: they serve
neither trading nor resolution.

### Alignment

`OpenTime` is always the candle's **open**, always UTC, always on an interval
boundary.

- Sub-day intervals floor against the Unix epoch, which matches Binance (4h
  candles open at 00, 04, 08, 12, 16, 20 UTC).
- `OneDay` floors to 00:00 UTC.
- `OneWeek` floors to **Monday** 00:00 UTC — not to the epoch, which is a
  Thursday. `CandleIntervals.AlignFloor` special-cases this.

Only **closed** candles are stored. An in-progress candle would change under the
engine and break determinism.

---

## 2. Binance endpoints

> Specified from prior knowledge and **not yet verified against live responses.**
> Verify paths, parameter names, limits and the array layout before trusting a
> backtest, and correct this file in the same change.

| Purpose | Endpoint |
|---|---|
| USD-M futures klines | `GET https://fapi.binance.com/fapi/v1/klines` |
| Spot klines | `GET https://api.binance.com/api/v3/klines` |
| Funding rate history | `GET https://fapi.binance.com/fapi/v1/fundingRate` |

**Futures, not spot, by default.** The owner trades perpetual futures; spot
candles have no funding and different liquidity.

Parameters: `symbol`, `interval`, `startTime`, `endTime`, `limit`. Futures
`limit` maxes at 1500, spot at 1000. Page forward by setting `startTime` to the
last returned `openTime + 1ms`; `BinanceKlineClient` advances by
`lastOpenTime + interval` and stops when a page comes back short.

Keyless and public — **no credentials and no signing**. Do not reuse
`BitunixSigner`.

### Kline response shape

An array of arrays, read positionally:

| Index | Field | Type on the wire |
|---|---|---|
| 0 | openTime | number (epoch ms) |
| 1–5 | open, high, low, close, volume | **string** |
| 6 | closeTime | number |
| 7 | quoteAssetVolume | string |
| 8 | numberOfTrades | number |
| 9–10 | takerBuyBase, takerBuyQuote | string |
| 11 | ignore | — |

Prices arrive as JSON **strings**, like Bitunix. Parse to `decimal` with
`CultureInfo.InvariantCulture` and an explicit `NumberStyles`. Never `double`.

`BinanceKlineClient.ParseDecimal` accepts both string and number forms, so a
change in Binance's encoding degrades into a working parse rather than a crash.

### Rate limits

Binance budgets by **request weight per IP**, and the IP is the proxy's, not the
host's. `IKlineRateLimiter` is a Redis token bucket keyed by **proxy exit
identity** for exactly that reason — two users behind one proxy share one budget.

Default 20 requests/second (`MarketData:RequestsPerSecond`), deliberately well
under the published ceiling. `429` and `418` are retried honouring `Retry-After`;
repeated `418` fails the job rather than hammering on.

---

## 3. Egress — Binance goes through the same proxy as Bitunix

`IProxiedHttpClientProvider` generalises what was `IBitunixHttpClientProvider`.
Pools are keyed `poolName|proxyIdentity`, so `bitunix`, `binance-futures` and
`binance-spot` keep separate socket pools through one proxy.

**`Proxy:Required` still governs.** With it true and no proxy resolved, a kline
fetch throws `ProxyRequiredException` *before a socket opens* — the same
fail-closed guarantee the exchange client has. A backfill job fails with that
message rather than reaching Binance from the host address.

Two reasons this matters even though Binance market data is public:

1. Binance geo-blocks a number of regions, so the fetch may not work at all
   without the proxy.
2. It keeps the invariant simple — **nothing in this repo talks to the internet
   directly.** One exception would be one too many.

`PUT /api/proxy` evicts every pool for the previous proxy, Binance included.

---

## 4. Gaps — the engine fails closed

A backtest over gapped candles silently lies, so gap detection is mandatory.

`CandleRepository.FindGapsAsync` walks every expected interval boundary between
`from` and `to` and returns the missing runs. It takes a cheap path first —
compare row count and first/last against what a complete range would hold — and
only materialises timestamps when those disagree. That keeps the common case
fast even over a million 1-minute rows.

Policy:

- `POST /api/backtests` refuses to queue a run whose range has gaps, in the run
  interval **or** the drill-down interval.
- `BacktestRun.AllowGaps` overrides it; the result is then stamped
  `DataQuality = Gapped` and must be displayed as such.
- Missing **drill-down** candles are not a hard failure. They degrade fill
  resolution to the pessimistic assumption, recorded per trade as
  `AssumedNoMinuteData`.

Exchange downtime produces real gaps that will never fill. `AllowGaps` is the
escape hatch for those; it is not a default.

---

## 5. CSV import

For symbols or ranges Binance does not serve, and for TradingView data.

**TradingView has no public market-data API** and their terms do not permit
scraping chart data. The only legitimate path is exporting CSV yourself. Do not
implement a TradingView client.

Accepted format:

- A time column named any of `time`, `date`, `datetime`, `open time`, `opentime`,
  `timestamp`, plus `open`, `high`, `low`, `close`. `volume` is optional and
  defaults to 0 with a warning.
- Column order and casing are free. Comma, semicolon and tab separators all work.
- Timestamps: ISO-8601, `yyyy-MM-dd HH:mm:ss`, or epoch seconds/milliseconds
  (13+ digits reads as milliseconds). **A value with no offset is read as UTC**
  and the response says so explicitly.

Rejected outright — the whole file, not the row:

- timestamps that go backwards
- timestamps not aligned to the declared interval
- `high < low`
- `open` or `close` outside `[low, high]`
- negative volume
- more than 1000 duplicate timestamps

Rows already stored are skipped rather than overwritten, so re-importing the same
file is a no-op. Imported candles get `Source = CsvImport` and never mix with
Binance data in one run, because `CandleSource` is part of a run's identity.

Every import writes a `CandleImport` audit row: file name, size, SHA-256, row
counts and warnings. The candles are shared; the audit row is user-owned.

---

## 6. Storage

Schema `market` — `candles`, `funding_rates`, `market_symbol_aliases`.

**Candles are shared reference data, not user-owned.** They carry no `UserId` and
no global query filter: market data is public and identical for everyone, and
duplicating millions of rows per tenant would buy nothing. `CandleImport` and
`MarketDataBackfillJob` *are* user-owned and live in the `backtest` schema.

`Candle.Id` is a `long` identity rather than a `Guid` — this table reaches
millions of rows and index size matters.

Unique index `(source, symbol, interval, open_time)`. Every write is an upsert;
re-fetching a range must be a no-op.

Precision follows `Shared/Precision.cs`: OHLC at `Precision.Price`
(`numeric(28,18)`), volumes at `Precision.Quantity`, funding rates at
`Precision.Ratio`.

### Growth

| Interval | Rows per symbol-year | Rough size |
|---|---|---|
| 1m | ~525,600 | 60–80 MB |
| 15m | ~35,000 | 4–6 MB |
| 4h | ~2,200 | under 1 MB |

The drill-down set dominates. `GET /api/market-data/coverage` reports row counts
so growth stays visible, and `DELETE /api/market-data/candles` removes a range —
both bounds required, so a mistyped request cannot empty the table.

There is no retention policy yet.

### Symbol aliases

Bitunix and Binance mostly agree on USDT-perp symbols but not always.
`market_symbol_aliases` maps a canonical symbol to a per-source symbol;
resolution falls back to the canonical name when no row exists.

---

## 7. Endpoints

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/market-data/coverage` | What is stored, per source/symbol/interval |
| `GET` | `/api/market-data/gaps` | Missing candles in a range |
| `POST` | `/api/market-data/backfill` | Queue a Binance fetch, returns `202` |
| `GET` | `/api/market-data/backfill/{id}` | Job status and progress |
| `POST` | `/api/market-data/backfill/{id}/cancel` | Stop at the next chunk boundary |
| `POST` | `/api/market-data/import` | CSV upload |
| `DELETE` | `/api/market-data/candles` | Delete a range |

Backfill runs in `TradeLedger.Worker` (`MarketDataWorker`), one job at a time
under a Redis lock, fetching in 30-day chunks and writing progress after each.
Candles already written by a cancelled job are kept — they are valid on their own.
