# Bitunix API notes

Compiled from the vendor documentation at <https://www.bitunix.com/api-docs/>
(the older `openapidoc.bitunix.com` host 301-redirects there).

**When a live response disagrees with this document, the live response wins —
correct this file in the same change.**

---

## Authentication

**Base URL:** `https://fapi.bitunix.com` (HTTPS only)

**Headers on every private request**

| Header | Value |
|---|---|
| `api-key` | the API key |
| `nonce` | 32-bit random string, generated per request |
| `timestamp` | current time, milliseconds, UTC |
| `sign` | see below |
| `language` | e.g. `en-US` |
| `Content-Type` | `application/json` |

**Signature — double SHA-256, lowercase hex**

```
digest = SHA256(nonce + timestamp + apiKey + queryParams + body)
sign   = SHA256(digest + secretKey)
```

- `queryParams` — all query parameters sorted ASCII-ascending by key, then
  concatenated key-then-value with no separators and no spaces.
  Example: params `{uid: 200, id: 1}` becomes `id1uid200`.
- `body` — the JSON body serialized with **all whitespace removed**. Empty
  string for requests without a body.

**Vendor's reference vector** (pin the unit test to this):

```python
nonce        = "123456"
timestamp    = "20241120123045"
api_key      = "yourApiKey"
secret_key   = "yourSecretKey"
query_params = "id1uid200"
body         = '{"uid":"2899","arr":[{"id":1,"name":"maple"},{"id":2,"name":"lily"}]}'

digest_input = nonce + timestamp + api_key + query_params + body
digest       = sha256_hex(digest_input)
sign         = sha256_hex(digest + secret_key)
```

Note the vendor's sample `timestamp` is formatted `yyyyMMddHHmmss` while the
header is documented as epoch milliseconds. Verify empirically against a live
key before trusting either; whichever authenticates is correct, and record the
answer here.

**Public endpoints** (market data, trading pairs, config) need no signature.

---

## Rate limits

- REST: approximately **10 requests/second per UID** on the endpoints checked
  (`get_history_positions`, `futures/account`). Assume per-endpoint limits vary;
  budget conservatively.
- WebSocket: server accepts a maximum of **5 messages/second** from the client.

Enforce with a Redis token bucket keyed by credential so all workers share one
budget.

---

## Endpoints used by TradeLedger

All read-only. **No write endpoint may be called from this repo** — see the hard
constraint in `CLAUDE.md`.

### `GET /api/v1/futures/position/get_history_positions`

The primary source of journal rows. Rate limit 10/s per UID.

**Request**

| Param | Type | Req | Notes |
|---|---|---|---|
| `symbol` | string | no | trading pair |
| `positionId` | string | no | specific position |
| `startTime` | int64 | no | position create time, epoch ms |
| `endTime` | int64 | no | epoch ms |
| `skip` | int64 | no | records to skip, default 0 |
| `limit` | int64 | no | **max 100**, default 10 |
| `subAccountId` | int64 | no | filter to a sub-account |

**Response — `positionList[]`**

| Field | Type | Maps to |
|---|---|---|
| `positionId` | string | `Trade.ExchangePositionId` (unique with AccountId) |
| `symbol` | string | `Trade.Symbol` |
| `maxQty` | string | `Trade.Quantity` |
| `entryPrice` | string | `Trade.EntryPrice` (average) |
| `closePrice` | string | `Trade.ExitPrice` (average) |
| `liqQty` | string | liquidated qty; non-zero implies `Role = Liquidation` |
| `side` | string | `LONG` / `SHORT` |
| `marginMode` | string | `ISOLATION` / `CROSS` |
| `positionMode` | string | `ONE_WAY` / `HEDGE` |
| `leverage` | int32 | `Trade.Leverage` |
| `fee` | string | `Trade.Fees` (trading fees over the position's life) |
| `funding` | string | `Trade.Funding` — **no equivalent column in the workbook** |
| `realizedPNL` | string | gross PnL, **excludes funding and fees** |
| `liqPrice` | string | estimated liquidation price |
| `ctime` | int64 | `Trade.OpenedAt` (epoch ms) |
| `mtime` | int64 | `Trade.ClosedAt` (epoch ms, last modification) |
| `subAccountId` | int64 | |
| `total` | int64 | total matching count, for pagination |

**Careful:** `realizedPNL` is gross. Net PnL — the number the journal reports —
is `realizedPNL - fee + funding` (funding is signed; verify the sign convention
against live data and note it here).

### `GET /api/v1/futures/trade/get_history_trades`

Individual fills. Source for `Execution` rows, which is what makes partial
closes and scale-ins representable (the workbook's `% of Margin Closed`).

### `GET /api/v1/futures/trade/get_history_orders`

Order-level history — order type, requested vs filled quantity. Feeds
`Order Type` and distinguishes market from limit entries.

### `GET /api/v1/futures/position/get_pending_positions`

Currently open positions — drives the `Open Positions` counter and unrealized
PnL on the dashboard.

### `GET /api/v1/futures/account`

Futures wallet. Rate limit 10/s per UID.

**Request:** `marginCoin` (string, required)

**Response**

| Field | Meaning |
|---|---|
| `marginCoin` | queried coin |
| `available` | available quantity; `available + crossUnrealizedPNL` = max open amount |
| `frozen` | locked by active orders |
| `margin` | locked in positions |
| `transfer` | max withdrawable |
| `positionMode` | `ONE_WAY` / `HEDGE` |
| `crossUnrealizedPNL` | unrealized PnL, cross positions |
| `isolationUnrealizedPNL` | unrealized PnL, isolated positions |
| `bonus` | futures bonus balance |

Feeds `BalanceSnapshot`:
`Equity = available + frozen + margin + crossUnrealizedPNL + isolationUnrealizedPNL`.
Confirm empirically whether `bonus` should be included — it should probably be
excluded from the equity curve since it is not withdrawable.

### Market data (public)

`get_trading_pairs`, tickers, klines, funding rates, depth. Used for instrument
metadata (tick size, step size, contract multiplier) and for valuing `Holding`
rows.

### Spot API

Documented as a separate section alongside Futures. Endpoints for spot orders,
fills, and balances follow the same signing scheme. **Fill in the exact spot
paths and response shapes here when the spot client is implemented** — they were
not captured during initial research.

---

## WebSocket

Private channels, used as a live hint only — never as the persistence source of
truth:

- **Position Channel** — position opened / changed / closed
- **Order Channel** — order lifecycle events
- **Balance Channel** — wallet changes

Public channels carry ticker, depth and kline streams.

Client message rate is capped at 5/s. Expect reconnects; on reconnect, trigger a
REST reconcile sweep rather than assuming continuity.

---

## Implementation reminders

- Every numeric field arrives as a **JSON string**. Deserialize to `decimal`
  with `CultureInfo.InvariantCulture`.
- Timestamps are epoch **milliseconds**. Store both the parsed
  `DateTimeOffset` and the raw long.
- Persist the raw response JSON in `RawExchangePayload` before mapping. Mapping
  bugs are then fixable without re-fetching history.
- `positionId` is unique per account — the anchor for idempotent upserts.

---

## Sources

- [Bitunix API docs (index)](https://www.bitunix.com/api-docs/)
- [Introduction](https://www.bitunix.com/api-docs/futures/common/introduction.html)
- [Signature algorithm](https://www.bitunix.com/api-docs/futures/common/sign.html)
- [Get History Positions](https://www.bitunix.com/api-docs/futures/position/get_history_positions.html)
- [Get History Orders](https://www.bitunix.com/api-docs/futures/trade/get_history_orders.html)
- [Get History Trades](https://www.bitunix.com/api-docs/futures/trade/get_history_trades.html)
- [Get Pending Positions](https://www.bitunix.com/api-docs/futures/position/get_pending_positions.html)
- [Get Single Account](https://www.bitunix.com/api-docs/futures/account/get_single_account.html)
- [WebSocket overview](https://www.bitunix.com/api-docs/futures/websocket/prepare/WebSocket.html)
- [Order Channel (private WS)](https://www.bitunix.com/api-docs/futures/websocket/private/Order%20Channel.html)
