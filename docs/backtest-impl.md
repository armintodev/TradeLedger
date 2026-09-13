# Backend changes required by the backtest and market-data screens

Companion to [`web/SPEC.md`](../web/SPEC.md), written the same way as
[`backend-changes-for-web.md`](backend-changes-for-web.md): what the frontend
wants, what the code does today, and what it costs the user in the meantime.

**Nothing here blocks the frontend.** Both feature areas are built and shipping
against the API exactly as it stands; every item below has a workaround already
in the client. This is a list of things that are wrong or missing, ordered by
what they cost the trader, not by effort.

Scope rule inherited from `CLAUDE.md` §1: none of this touches a Bitunix write
endpoint, and none of it changes sync watermark semantics.

---

## 1. `DataQuality` never reflects reality — defect

**Problem.** `src/TradeLedger.Core/Domain/Backtesting/BacktestRun.cs:141`, inside
`Queue`:

```csharp
DataQuality = spec.AllowGaps ? DataQuality.Gapped : DataQuality.Clean,
```

That is set **at queue time, from the request flag alone**, before any candle is
looked at. The endpoint's gap check runs separately and only decides whether to
throw; it never feeds back into this field.

The correction path exists and is dead. `BacktestRun.cs:224`:

```csharp
public void MarkDataQuality(DataQuality quality)
{
    DataQuality = quality;
}
```

`grep -rn "MarkDataQuality" src/` returns **one line — its own declaration**.
Nothing in the API, the runner, the engine or the worker calls it.

Consequences today:

- A run with `allowGaps: true` over **perfectly complete data** is stamped
  `Gapped` permanently.
- A run forced through over real holes is stamped `Gapped` too, so the two are
  indistinguishable.
- A run with `allowGaps: false` is always `Clean` — which is true, but only
  because the endpoint refused every alternative.

So the field carries one bit of information — *"was allowGaps set"* — under a
name that claims something else. `docs/market-data.md` §4 says a gapped result
"must be displayed as such", and this is the field that would do it.

**Change.** Have the queue endpoint pass what it actually found. It already
computes the gaps at `BacktestRunEndpoints.cs:309-318`, so the information is in
hand:

```csharp
var gaps = await candles.FindGapsAsync(source, symbol, interval, from, to, ct);

if (gaps.Count > 0)
{
    if (request.AllowGaps != true)
    {
        throw new DomainRuleException("candle_data_has_gaps", BuildGapDetail(gaps, interval));
    }

    run.MarkDataQuality(DataQuality.Gapped);   // <-- only when gaps are real
}
```

and initialise the field to `Clean` in `Queue` rather than deriving it from
`AllowGaps`. `AllowGaps` is already persisted and already on the response, so
nothing is lost — the two facts simply stop being conflated.

**Test** — unit test in `tests/TradeLedger.UnitTests`: a run queued with
`allowGaps: true` over a complete range ends up `Clean`; the same flag over a
range with one hole ends up `Gapped`.

**Frontend workaround in place:** the run badge reads **"Gaps permitted"**, never
"Ran over gaps", because the latter would be a claim the data cannot support.
Once this merges the badge can tell the truth and the distinction becomes worth
surfacing on the runs list.

---

## 2. No way to list backfill jobs — missing endpoint

**Problem.** `POST /api/market-data/backfill` returns 202 with a job id, and
`GET /api/market-data/backfill/{id}` fetches one by that id. There is no list
endpoint. A job is reachable only by an id the client happened to keep.

So there is no backfill history. Close the tab and every job you queued becomes
unreachable — including a failed one whose `error` string is the only record of
why it failed.

**Change.**

```
GET /api/market-data/backfill?status=&symbol=&limit=
```

returning `List<BackfillJobResponse>` newest first, filtered by the same global
user query filter every other `IUserOwned` list uses. `MarketDataBackfillJob` is
already `IUserOwned`, so this is a projection over an existing table with an
existing filter.

**Frontend workaround in place:** job ids are kept in `localStorage` under
`tradeledger.backfills` (capped at 20, newest first) and re-fetched one by one on
mount. It is per-browser, it does not survive a different device, and it shows a
note saying so.

---

## 3. `cancellationRequested` is missing from `BackfillJobResponse` — defect

**Problem.** `POST /api/market-data/backfill/{id}/cancel` sets the flag and
returns 200. For a **running** job that is fine — it stops at its next 30-day
chunk boundary and becomes `Cancelled`.

For a **queued** job it is a dead end. The worker's pickup query excludes it:

```csharp
.Where(j => j.Status == MarketDataJobStatus.Queued && !j.CancellationRequested)
```

so the job is never picked up, never starts, and **never transitions**. It stays
`Queued` forever. And because `BackfillJobResponse` does not carry
`CancellationRequested`, nothing in the response says why it is sitting there.

**Change.** Add the field:

```csharp
public sealed record BackfillJobResponse(
    ...
    bool CancellationRequested,
    ...);
```

`BacktestRunResponse` already carries exactly this field for exactly this reason,
so the shape is precedented. Optionally also transition a cancelled-while-queued
job straight to `Cancelled` at cancel time, since it is knowable immediately.

**Frontend workaround in place:** the client remembers locally which job ids it
asked to cancel and renders "Cancelling…" from that. It is lost on reload, after
which the job reads as a permanently queued one.

---

## 4. A run's frozen `ruleJson` is not returned — missing field

**Problem.** Every run stores its own copy of the rule it executed, precisely so
an old result stays explainable after the strategy moves on. `PUT /strategies/{id}`
says so in its own description:

> "Bumps the version. Finished runs are untouched: each stores its own copy of
> the rule JSON and its hash, so an old result stays explainable after the
> strategy moves on."

`BacktestRunResponse` exposes `ruleHash` but not `ruleJson`. So the guarantee is
real in the database and unreachable over HTTP. A finished run can only link to
the live strategy — which may be several versions ahead, and whose current rule
tree is *not* what produced the result on screen.

**Change.** Add the rule to the run response, the way `BacktestStrategyResponse`
already does it:

```csharp
JsonElement? Rule,     // from BacktestRun.RuleJson
```

The column exists and is populated. Gate it behind the by-id fetch only
(`GET /api/backtests/{id}`), not the list, exactly as `BacktestStrategyResponse.From`
gates its own `includeRule`.

**Frontend workaround in place:** the run detail shows `ruleHash` (first eight
characters, full value on hover) and links to the strategy, with a note that the
strategy may have been revised since the run. It cannot show the rules that
produced the result.

---

## 5. Two result counters are declared but never assigned — defect

**Problem.** `src/TradeLedger.Core/Backtesting/BacktestRunner.cs:23-24`:

```csharp
public int SkippedNoCandleData { get; init; }
public int LiquidationRiskCount { get; init; }
```

`grep -rn "SkippedNoCandleData\|LiquidationRiskCount" src/ --include=*.cs`
returns **only those two declaration lines**. `RuleSimulationCore.Build` omits
both, so they serialise as `0` on every run that has ever executed.

A counter reading `0` is a claim: *nothing was skipped for want of candle data,
no position ever approached liquidation.* Neither is verified.

**Change.** Either assign them in `RuleSimulationCore.Build` alongside the
counters that are real (`AmbiguousSignals`, `SkippedInvalidStop`,
`SkippedInsufficientMargin`, `OpenAtEndOfData`), or delete them from
`BacktestResultSummary`. Both are honest; leaving them is not.

**Frontend workaround in place:** neither is rendered. This follows the same rule
as `profitFactor: null → "—"` in `web/SPEC.md` — a number that cannot be trusted
is not shown as a number.

---

## 6. No endpoint returns candles — missing endpoint

**Problem.** `/api/market-data/candles` is **DELETE only**. Nothing anywhere in
the API serves OHLC bars.

So no price chart is possible on any screen. The gap viewer can draw missing
ranges as bands on an empty time axis, but not against the data they are holes
in, which is the rendering that would make a gap obvious at a glance. A backtest
result likewise cannot plot its trades against price.

**Change, when picked up.**

```
GET /api/market-data/candles?source=&symbol=&interval=&from=&to=
```

returning open time, OHLC and volume. It needs a hard cap — a year of 1m candles
is ~525,600 rows — so either a `limit` with a documented maximum, or server-side
downsampling to a target point count. The latter is what a chart actually wants
and avoids shipping half a million rows to draw 800 pixels.

**Frontend workaround in place:** no price chart anywhere. Gaps render as bands
on a bare axis, labelled with their bar counts.

---

## 7. Two error responses break the envelope — nits

Every failure in the API is shaped by `ApiProblem` with a stable `code` and a
`traceId`. These two are not.

**7.1 `GET /api/backtests/{id}` returns a bodyless 404.**
`BacktestRunEndpoints.cs:103`:

```csharp
return run is null ? Results.NotFound() : Results.Ok(BacktestRunResponse.Of(run));
```

Every other 404 in the feature throws `ResourceNotFoundException` and comes back
as `ProblemDetails`. Fix by throwing it here too:

```csharp
var run = await ... ?? throw new ResourceNotFoundException("Backtest run", id);
```

**7.2 `DELETE /api/market-data/candles` hand-builds its 400.**
`MarketDataEndpoints.cs:203-207`:

```csharp
return Results.Problem(
    title: "Invalid range",
    detail: "from must be earlier than to.",
    statusCode: StatusCodes.Status400BadRequest);
```

`GET /gaps` validates the identical condition by throwing
`DomainValidationException`, so the same mistake returns two different shapes
depending on which verb you used. Fix by throwing there too.

This is the same class of nit as §4 of `backend-changes-for-web.md`, which was
the login 401 — and which has since been fixed.

**Frontend workaround in place:** `toApiError` already tolerates an empty body and
a missing `code`, so both degrade to a generic message. The run detail page
renders its own "this run no longer exists" state rather than an `ErrorState`
with nothing in it.

---

## 8. `GET /api/backtests/{id}/trades` pages but has no total — done

**Was.** The endpoint accepted `page` and `pageSize` (default 100, clamped
1–500) but returned a bare `List<BacktestTradeResponse>`. Without a total a page
count cannot be derived, so the client could only offer Next/Previous with Next
disabled once a page came back short, and could display no total at all.

**Now.** It returns `PagedResult<BacktestTradeResponse>`, matching
`ListBacktestRuns`, and `PagedResult` computes `TotalPages` and `HasMore`.

The workaround is retired. `RunTradesTable` and the positions page at
`/backtests/runs/:id/trades` both page against the real total, and the run's
trade count is now stated rather than left unknowable.

---

## 9. The CSV import size limits disagree — defect

**Problem.** `MarketDataEndpoints.cs:135`:

```csharp
if (file.Length > options.Value.MaxImportBytes)
```

with `MarketData:MaxImportBytes` = `52428800` (50 MB) in `appsettings.json`.

But nothing in the repo sets `MaxRequestBodySize`, `RequestSizeLimit` or
`MultipartBodyLengthLimit`, and there is no `Kestrel` section in configuration.
Kestrel's default request body limit is ~30 MB. So a file between roughly 30 MB
and 50 MB is rejected by the server **before the endpoint runs**, as a
`BadHttpRequestException` → `malformed_request`, rather than by the check that
was written for it.

The configured limit is therefore unreachable for its top 40 %, and the error the
user gets is the wrong one.

**Change.** Either raise Kestrel's limit to match `MaxImportBytes`:

```csharp
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = marketData.MaxImportBytes);
```

or lower `MaxImportBytes` to sit under Kestrel's default and delete the
ambiguity. Whichever, the two numbers should be derived from one source.

**Frontend workaround in place:** the upload guards client-side at **28 MB** —
below Kestrel's default, not at the configured 50 MB — and handles
`malformed_request` separately from `validation_failed` in case a file slips past.

---

## 10. `BacktestExecution` rows have no endpoint — done

**Problem.** The engine writes two `BacktestExecution` rows per simulated trade
(`Open` plus `Close` or `Liquidation`) with price, quantity, fee, timestamp and
bar index. Nothing exposed them.

The real journal has the same entity and the same story — `Execution` is what
makes scale-ins and partial closes representable — and `TradeDetailResponse`
returns them. The backtest side persisted them and stopped.

**Change, as shipped.** Both halves of the suggestion, because they answer
different questions. `GET /api/backtests/{id}/trades/{tradeId}/executions`
returns the fills alone, and `GET /api/backtests/{id}/trades/{tradeId}` returns
the position in full — bar indices, the engine's note and the fills folded in as
`executions` — so a single position is explained without a second round trip.
The list at `GET /api/backtests/{id}/trades` is still the ledger view: every
position of a run with its open and close instants, paged.

---

## Order of work

1. **§1 `DataQuality`** — the only item on this list that makes the UI state
   something untrue. Small, self-contained, and it retires a frontend caveat.
2. **§5 dead counters** — same class of problem, same size. Assign them or
   delete them.
3. **§7 error envelope** — a few lines each, removes two client special cases.
4. **§3 `cancellationRequested`** — one field; makes a stuck job explicable.
5. **§9 import size limits** — configuration, not code, but it is a real trap.
6. **§8 paged trades** — done: `PagedResult`, matching the sibling endpoint.
7. **§2 backfill list** — turns backfill history from per-browser into real data.
8. **§4 run `ruleJson`** — makes an old result explainable, as intended.
9. **§6 candle GET** — the largest, and the one that unlocks charting.
10. **§10 executions** — done: fills alone, plus a by-id trade that folds them in.
