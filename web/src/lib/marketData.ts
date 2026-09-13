import { dayEndIso, dayStartIso } from './time';
import type { GapQuery } from '@/api/queryKeys';
import type { CandleGapResponse, CandleInterval, CandleSource } from '@/api/types';

/**
 * Candle intervals and the two range conversions that everything else depends
 * on getting right.
 */

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

/** Bar duration in milliseconds, for turning a bar count into a span. */
export const INTERVAL_MS: Record<CandleInterval, number> = {
  OneMinute: MINUTE,
  FifteenMinutes: 15 * MINUTE,
  ThirtyMinutes: 30 * MINUTE,
  OneHour: HOUR,
  TwoHours: 2 * HOUR,
  FourHours: 4 * HOUR,
  SixHours: 6 * HOUR,
  TwelveHours: 12 * HOUR,
  OneDay: DAY,
  OneWeek: 7 * DAY,
};

/** The trader's vocabulary. The wire uses the member names; nobody says "FourHours". */
export const INTERVAL_LABELS: Record<CandleInterval, string> = {
  OneMinute: '1m',
  FifteenMinutes: '15m',
  ThirtyMinutes: '30m',
  OneHour: '1h',
  TwoHours: '2h',
  FourHours: '4h',
  SixHours: '6h',
  TwelveHours: '12h',
  OneDay: '1D',
  OneWeek: '1W',
};

/** Declaration order, which is also ascending duration. */
export const ALL_INTERVALS: CandleInterval[] = [
  'OneMinute',
  'FifteenMinutes',
  'ThirtyMinutes',
  'OneHour',
  'TwoHours',
  'FourHours',
  'SixHours',
  'TwelveHours',
  'OneDay',
  'OneWeek',
];

/**
 * What a backtest may run over. One-minute candles exist only to resolve which
 * of a stop or a target was hit first inside a larger bar, and the queue
 * endpoint rejects them outright.
 */
export const TRADEABLE_INTERVALS: CandleInterval[] = ALL_INTERVALS.filter(
  (interval) => interval !== 'OneMinute',
);

export function isTradeable(interval: CandleInterval): boolean {
  return interval !== 'OneMinute';
}

export const SOURCE_LABELS: Record<CandleSource, string> = {
  BinanceFutures: 'Binance futures',
  BinanceSpot: 'Binance spot',
  CsvImport: 'CSV import',
};

/**
 * Sources a backfill may fetch from. `CsvImport` is refused with a 422
 * (`csv_cannot_be_backfilled`) — there is nothing to fetch it from.
 */
export const BACKFILL_SOURCES: CandleSource[] = ['BinanceFutures', 'BinanceSpot'];

export function intervalOptions(intervals: CandleInterval[] = TRADEABLE_INTERVALS) {
  return intervals.map((interval) => ({
    value: interval,
    label: INTERVAL_LABELS[interval],
  }));
}

export function sourceOptions(sources: CandleSource[] = BACKFILL_SOURCES) {
  return sources.map((source) => ({ value: source, label: SOURCE_LABELS[source] }));
}

/**
 * The single conversion from a pair of date-picker values to the instants the
 * API wants.
 *
 * Both the gap pre-flight and the queue request go through here. If they each
 * converted independently — even by a few hours — the pre-flight would check a
 * different window than the server, and would report a range clean that the
 * queue endpoint then refuses with `candle_data_has_gaps`. That bug is
 * intermittent and nearly unfalsifiable, so there is exactly one function.
 */
export function rangeToInstants(
  from: string | null | undefined,
  to: string | null | undefined,
): { from?: string; to?: string } {
  return { from: dayStartIso(from), to: dayEndIso(to) };
}

/** True once a range is complete enough to probe or submit. */
export function isCompleteRange(from: string | null | undefined, to: string | null | undefined) {
  const range = rangeToInstants(from, to);

  return Boolean(range.from && range.to && Date.parse(range.from) < Date.parse(range.to));
}

/**
 * The backfill window that actually covers a gap.
 *
 * `gap.to` is the open time of the last **missing** bar, so a one-bar gap has
 * `from === to` — and `MarketDataBackfillJob.Queue` rejects `to <= from` with
 * `empty_backfill_window`. Sending a gap's own bounds is therefore a guaranteed
 * 422. One interval is added so the window ends after the last missing bar
 * closes.
 */
export function backfillWindowFor(
  gap: Pick<CandleGapResponse, 'from' | 'to'>,
  interval: CandleInterval,
): { from: string; to: string } {
  const end = new Date(Date.parse(gap.to) + INTERVAL_MS[interval]);

  return { from: gap.from, to: end.toISOString() };
}

/** The window covering every gap in a set, for a single one-click backfill. */
export function backfillWindowForAll(
  gaps: Pick<CandleGapResponse, 'from' | 'to'>[],
  interval: CandleInterval,
): { from: string; to: string } | null {
  if (gaps.length === 0) {
    return null;
  }

  const first = gaps.reduce((a, b) => (Date.parse(a.from) <= Date.parse(b.from) ? a : b));
  const last = gaps.reduce((a, b) => (Date.parse(a.to) >= Date.parse(b.to) ? a : b));

  return backfillWindowFor({ from: first.from, to: last.to }, interval);
}

/** Total bars missing across a set of gaps, for a one-line summary. */
export function totalMissing(gaps: Pick<CandleGapResponse, 'missingCount'>[]): number {
  return gaps.reduce((total, gap) => total + gap.missingCount, 0);
}

/**
 * How much history a warmup requirement actually costs, in plain terms. The
 * validate endpoint returns a bar count; "600 bars" means nothing until it is
 * "100 days on 4h candles".
 */
export function warmupSpanLabel(bars: number, interval: CandleInterval): string {
  const ms = bars * INTERVAL_MS[interval];
  const days = ms / DAY;

  if (days < 1) {
    return `${Math.round(ms / HOUR)} hours`;
  }

  // Days stay days up to a year — "100 days" is more useful than "0.3 years".
  if (days < 365) {
    return `${Math.round(days)} days`;
  }

  return `${(days / 365).toFixed(1)} years`;
}

/**
 * Build the five-parameter gap query, or null when the inputs are not yet
 * complete enough to ask.
 *
 * Both the standalone gap viewer and the queue form's pre-flight go through
 * here, so they produce an identical query key and TanStack de-duplicates them
 * — and, more importantly, they check the same window the queue endpoint will.
 */
export function buildGapQuery(inputs: {
  source: CandleSource | null;
  symbol: string;
  interval: CandleInterval | null;
  from: string | null;
  to: string | null;
}): GapQuery | null {
  const symbol = inputs.symbol.trim().toUpperCase();
  const range = rangeToInstants(inputs.from, inputs.to);

  if (!inputs.source || !inputs.interval || !symbol || !range.from || !range.to) {
    return null;
  }

  if (Date.parse(range.from) >= Date.parse(range.to)) {
    return null;
  }

  return {
    source: inputs.source,
    symbol,
    interval: inputs.interval,
    from: range.from,
    to: range.to,
  };
}
