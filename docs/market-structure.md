# Market structure

Where the engine works out what price *did*, as opposed to what an indicator says about it.
Each stage consumes the one above and adds exactly one idea.

```
                   OHLCV candles
                        │
                        ▼
                ┌───────────────┐
                │ SwingDetector │        ← built
                └───────┬───────┘
                        │
                  Swing points
                        │
                        ▼
                ┌───────────────┐
                │ Wave analyser │        ← next
                └───────┬───────┘
                        │
          size / duration / velocity / ATR
                        │
                        ▼
                ┌───────────────┐
                │   Strategy    │
                └───────────────┘
```

The vocabulary, fixed here so the later stages do not have to redefine it:

| Term | Meaning |
|---|---|
| **Pivot** | The point. A single bar where a leg turned. |
| **Swing** | The movement between two pivots. |
| **Wave** | A swing segment you have chosen to analyse. |
| **Momentum** | Characteristics of that movement — speed, acceleration. |
| **Medium / major** | A classification of a swing under whatever criteria you set. |

Only the first two exist today. Nothing in `MarketStructure/` decides whether the market is
bullish, how big a wave is, whether momentum is strong, or whether to trade — those are
later stages reading this output.

---

## SwingDetector

`src/TradeLedger.Core/MarketStructure/SwingDetector.cs`

```
SwingDetector.Detect(IReadOnlyList<Candle>, IReversalThreshold) → SwingSeries
```

Static and stateless, computed over the whole range in one pass, like every other series the
engine builds. The state machine inside is advanced one bar at a time, so the live WebSocket
track can later wrap it in a streaming façade without the algorithm changing.

### The state machine

```
             ┌─────────────────┐
             │      START      │
             └────────┬────────┘
                      │
                      ▼
             ┌─────────────────┐
             │ FINDING INITIAL │   both candidates tracked;
             │      SWING      │   the first to confirm settles direction
             └────────┬────────┘
                 ↙         ↘
               ▼             ▼
       SEARCHING HIGH   SEARCHING LOW
               │             │
          reversal        reversal
          confirmed       confirmed
               │             │
               ▼             ▼
        HIGH CONFIRMED   LOW CONFIRMED
               │             │
               └──────┬──────┘
                      ▼
               switch direction
```

There are two separate questions per bar, and the order between them is the whole algorithm:

1. **Is this bar making a new extreme?** If so it becomes the candidate, and nothing else
   happens on this bar.
2. **Otherwise, has price reversed far enough to confirm the candidate?** If so the candidate
   becomes a swing point and the search flips.

```
SEARCHING HIGH:
    if bar.High > candidateHigh:
        candidateHigh = bar.High at this index      ← the threshold re-anchors here
    else if candidateHigh - bar.Low >= threshold(candidateHighIndex, candidateHigh):
        confirm candidateHigh as a swing high
        candidateLow = bar.Low at this index
        search = LOW

SEARCHING LOW: the mirror image.
```

The candidate is always *the extreme since the last confirmed pivot*; earlier values are
discarded as it extends.

### Decisions

**The reversal is measured from the bar's opposite extreme** — `Low` while searching for a
high, `High` while searching for a low — not from the close. Symmetric with the candidate,
which is built from `High`/`Low`, and it reacts on the bar the level is pierced rather than
waiting for a close.

**A bar that sets an extreme is never the bar that confirms it,** and its own opposite extreme
does not count toward the reversal either. On a wide outside bar — a new high *and* a low well
past the trigger level — the intrabar ordering is unknowable; the engine already models that
ambiguity in `IntrabarResolver`. Assuming the low came after the high would let one wide bar
manufacture a pivot out of nothing. So the reversal is measured only over bars strictly after
the candidate's own. Hence the invariant `ConfirmationIndex > CandleIndex`, always.

**That rule is what makes seeding the next candidate exact rather than approximate.** Every bar
between the pivot and the confirmation had a `Low` above the trigger level — otherwise it
would have confirmed first — and the confirming bar's `Low` is at or below it. So the
confirming bar's low *is* the minimum since the pivot, and `ConfirmationPrice` and the next
candidate's price are the same number. It shows in the output: a high confirmed at bar 23 at
59577.45 is followed by a low of 59577.45 anchored at bar 23.

**No heuristic picks the initial direction.** Both candidates are tracked from the first
detectable bar and the first to clear its threshold settles it. When one bar clears both, they
are necessarily anchored to the same bar — once the low has extended past its own anchor,
confirming the high needs price below that new low, which would have extended the low instead
— so the tiebreak is the larger reversal, with an exact tie going to the high for determinism.

**The first pivot is usually an artifact.** A range that opens at an extreme confirms that
extreme as soon as price moves a threshold away from it, which is a property of where the data
starts rather than a turn the market made. It is identifiable as
`CandleIndex == FirstDetectableIndex`, and only the first point can be.

### Causality — the trap in the output

A swing high at bar 183 confirmed at bar 190 **did not exist at bar 183**. Anything reading
swings while replaying bars must filter on `ConfirmationIndex`, never `CandleIndex`.
`SwingSeries.KnownAt(barIndex)` is that filter and is the only lookahead-free view;
`SwingSeries.Points` is for after-the-fact analysis and for drawing on a chart.

`BarWindow` throws `LookaheadException` for exactly this class of bug, but it cannot see
inside a `SwingPoint` list. Whoever wires swings into the rule engine owns this.

---

## Reversal threshold

`src/TradeLedger.Core/MarketStructure/Thresholds/`

The one thing the detector cannot work out for itself, so it asks. Volatility estimation stays
outside it — the two are tuned against different things and are worth testing apart.

```
OHLCV ──┬──► ATR calculator ──┐
        │                     ├──► reversal threshold
        └──► SwingDetector ◄──┘
```

```csharp
decimal? For(int anchorIndex, decimal anchorPrice);
int FirstAvailableIndex { get; }
```

The anchor is **the candidate** — its bar and its price — not the bar being evaluated. That is
what lets one seam serve every family: ATR reads the index, a percentage would read the price,
a fixed one reads neither. It also holds the threshold still for the life of a candidate, so a
pivot records the exact distance it had to beat, and volatility expanding during a pullback
cannot raise the bar retroactively.

| | |
|---|---|
| `AtrReversalThreshold` | `multiplier × ATR` at the candidate's bar. The one to reach for. |
| `FixedReversalThreshold` | A constant. Predictable, which is why the tests use it. |

A percentage threshold and an ATR/percentage hybrid are one small class each behind the same
interface; neither is written yet.

**Warmup.** ATR is null before bar `period`, so a candidate anchored earlier could never be
confirmed. The detector starts at `FirstAvailableIndex` and seeds both candidates there,
reporting it as `SwingSeries.FirstDetectableIndex`. This mirrors
`RuleSimulationCore.FirstWarmIndex`, which already refuses to trade before every series is
warm.

**ATR is engine-internal.** `IndicatorMath.Atr` exists but is deliberately absent from
`IndicatorFactory`'s dictionary: `web/SPEC.md` §6.7 states ATR is not a user-facing indicator
and that nothing in a rule document may reference it. A registry entry would break that and
would delay the first warm bar of every existing strategy. Same reasoning that keeps
`CycleReference` out of the dictionary.

---

## What a multiplier buys

1500 synthetic 15-minute bars, ATR period 14:

| Multiplier | Pivots | Avg bars between pivots | Avg bars to confirm |
|---|---|---|---|
| 0.5 | 697 | 2.2 | 1.0 |
| 1.0 | 485 | 3.1 | 1.2 |
| 1.5 | 235 | 6.4 | 2.3 |
| 3.0 | 35 | 42.9 | 14.9 |
| 6.0 | 6 | 250.0 | 88.7 |

The trade the multiplier makes is legible here: every step up buys cleaner structure and pays
for it in confirmation lag. At 6.0 a pivot is worth having but arrives 89 bars late.

---

## Output

```
SwingPoint
    Type                 High | Low
    Price                the extreme
    CandleIndex          the bar it printed on
    Time                 that bar's open time
    ConfirmationIndex    the bar it became a fact on      (always > CandleIndex)
    ConfirmationPrice    the price that confirmed it
    ConfirmationTime     that bar's open time
    Threshold            the distance it had to beat
    Reversal             the distance it actually moved   (always >= Threshold)
```

`SwingSeries` carries those in confirmation order — types strictly alternate, both index
columns strictly increase — plus `Pending`, the extreme of the leg still in progress that no
reversal has confirmed yet and may never.

---

## Next: the wave analyser

Consumes `SwingPoint[]` and adds size, duration, velocity and ATR-relative scale. It is also
where "medium" and "major" get defined, and where `CycleReference`'s wave-cycle banding
(`web/src/lib/marketCycle.ts`) should eventually be reconciled with real structure rather than
an ADX reading.
