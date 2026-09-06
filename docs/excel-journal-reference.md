# Source workbook reference

Extracted from `Trade City Pro Journal V1.51 Lite.xlsb` (Trade City Pro Journal
V1.51 Lite) and the owner's companion notes in `trade_journal.txt`.

This is the requirements source for the journal schema. When naming a field,
match the owner's vocabulary here rather than inventing a new term.

Workbook sheets: `Start Page`, `Calculator`, `Variables`, `Trade Log`, `Results`.

---

## Trade Log

70 columns in 8 banded groups. Header row is row 11; data starts row 12.

### Group: TRADE-Transfer (entry)

| # | Column | Notes |
|---|---|---|
| 0 | `#` | row index |
| 1 | `Tag` | free label |
| 2 | `W/L` | win / loss / breakeven — **derived** from net PnL |
| 3 | `Day of The Week` | derived from entry date |
| 4-6 | `D`, `M`, `Year` | entry date, split |
| 7-9 | `hh`, `:`, `mm` | entry time, split |
| 10 | `Exchange` | venue |
| 11 | `Entry Type` | e.g. futures vs spot |
| 12 | `Symbol` | trading pair |
| 13 | `Strategy` | from Variables |
| 14 | `Side` | long / short |
| 15 | `Entry Price` | |
| 16 | `Position Margin` | |
| 17 | `Leverage` | |
| 18 | `Order Value` | margin x leverage |
| 19 | `Qty` | |
| 20 | `Order Type` | market / limit |
| 21 | `Time Frame` | from Variables |
| 22 | `Entry Fee Rate` | |
| 23 | `Entry Mental State` | from Variables |
| 24 | `Entry ScreenShot` | image |

### Group: Check List (market context at entry)

| # | Column |
|---|---|
| 25 | `Total2` |
| 26 | `BTC.D` |
| 27 | `USDT.D` |
| 28 | `Market trend` |
| 29 | `SMA` |
| 30 | `Market Session` |
| 31 | `BTC Pair` |
| 32 | `RSI` |
| 33 | `Volume` |
| 34 | `Candle Shape` |

### Group: Trade Management (risk plan)

| # | Column | Notes |
|---|---|---|
| 35 | `Stop Loss Price` | |
| 36 | `Take Profit Price` | |
| 37 | `Position to ACC %` | order value / account balance |
| 38 | `Planned SL % (Trade)` | SL distance from entry |
| 39 | `% of ACC Risked` | the real risk number |
| 40 | `Planned Return (R)` | planned R:R |

### Group: Trade Exit

| # | Column | Notes |
|---|---|---|
| 41 | `Actual Exit Price` | |
| 42 | `% of Margin Closed` | supports partial closes / scale-outs |
| 43 | `Exit Type` | |
| 44 | `Exit Fee Rate` | |
| 45 | `Day of The Week` | derived from exit date |
| 46-48 | `D`, `M`, `Year` | exit date, split |
| 49-51 | `hh`, `:`, `MM` | exit time, split |
| 52 | `Exit Mental State` | from Variables |
| 54 | `Exit ScreenShot` | image |
| 55 | `PS Tag` | post-trade tag |

### Group: Trade Results (all derived)

| # | Column |
|---|---|
| 56 | `Actual Return` (achieved R) |
| 57 | `Gross Profit/Loss` |
| 58 | `Position Fees` |
| 59 | `Net Profit/Loss` |
| 60 | `Trade Gain %` |
| 61 | `Account Change %` |
| 62 | `New Balance` |
| 63 | `Trade Duration` |

### Groups: Memo, Tracking & Mistakes, Transfers

| # | Column |
|---|---|
| 64 | `Rating` |
| 66 | `Tracking` |
| 67 | `Mistake` |
| 68 | `Amount` (transfer) |
| 69 | `Transfer ScreenShot` |

### Header block above the table

`Start Date` (05/01/2023), `Starting Balance`, `Current Balance`,
`Risk/Trade`, `Risk/Loss`, `Realized PnL`, and counters for
`Total Trades`, `Winning Trades`, `Losing Trades`, `Breakeven Trades`,
`Open Positions`. Plus a `Quick Calc` panel: Entry Price, Margin, Leverage,
Stop Loss, `TP @ R:R`, R:R.

**Note:** funding fees have no column in the workbook. Bitunix reports them
separately (`funding` on a position), and they materially affect net PnL on
held positions — TradeLedger tracks them as a first-class field.

---

## Variables (dropdown vocabularies — seed data)

**Strategies** (12)
`All`, `1 Touch`, `2 Touch`, `3 Touch`, `Pre Breakout`, `Risky Breakout`,
`Reaction`, `Fakeout`, `News Trading`, `FOMO`, `Pattern`

**Mental States** (15)
`Calm`, `Angry`, `Happy`, `Stressfull`, `Doubt`, `Focused`, `Not Focused`,
`Confidence`, `Thrilled`, `Asleep`, `Tired`, `Stoploss pressure`, `Excited`,
`driving`, `boredo`

*(Owner's spellings preserved. Seed them verbatim; they are editable per user.)*

**Mistakes** (8)
`Fake Breakout`, `Others signal`, `Market Trend Analysis`, `feeling`,
`Before Candle Close`, `fomo`, `S/R analyse`, `First 5min`

**Trackings** (4)
`Entry Point stop`, `Closed by myself`, `FOMO close`, `Stopped`

**Check Items** (10)
`Total2`, `BTC.D`, `USDT.D`, `Market trend`, `SMA`, `Market Session`,
`BTC Pair`, `RSI`, `Volume`, `Candle Shape`

**Time Frames** (4)
`15m`, `1h`, `4h`, `1D`

**Other columns present:** `Pairs`, `Exchanges`, `Fees %`, `Years` (2024).

---

## Calculator (position-size calculator)

Inputs: `Current Balance`, `Entry Price`, `Average Fee`, `Stop Loss`,
`Risk (Loss)`, `Risk:Reward`, `Leverage`.

Derived: `SL to Entry Price Ratio`, `Position Size (QTY)`, `Order Value`,
`Margin $`, `Margin QTY`, `TP at R:R`, `Estimated Profit (-Fees)`,
`Fees Entry+TP`, `Estimated Loss (+Fees)`, `Fees Entry+SL`.

This becomes the `Plans` feature's calculator endpoint. It is the front door of
the pre-trade planning flow.

---

## Results

Per-trade summary feeding the equity curve:

`#`, `Tag`, `Entry D&T`, `Exit D&T`, `Entry Type`, `Position to Acc`,
`% of Acc Risked`, `Net PnL`, `Achieved R:R (R)`, `Account Change`,
`New Account Balance`, `Trade Duration`.

---

## Companion notes (`trade_journal.txt`)

Evidence for why coverage extends past the Bitunix API. The owner already tracks,
by hand, activity that no exchange endpoint reports:

- **Spot buys** with open and close dates and a free-text reason
  (AAVE, OP, FTM, TON) — sometimes held in an external wallet ("TON - in wallet").
- **Liquidity pools / farms** — `STON-TON` and `SHIT-TON`, recorded as LP token
  amount, USD value, entry date, farm APR (185%), and farmed/unfarmed status.
  Maps to `Holding`.
- **Losses that are not trades** — "$10 moved from TonKeeper to Nobitex on the
  wrong network". Maps to `Transfer` with `WriteOff = true`.
- **Transfers with fees** — "$1.46 to aeza.net, fee 0.29". Maps to `Transfer`.

These are why `Account.Kind` includes `ExternalWallet` and `ManualVenue`, and why
the equity curve is built from `BalanceSnapshot` rather than from trades alone.
