/**
 * Turning the engine's trend-strength reading into the trader's own vocabulary.
 *
 * The backend stores only the ADX number and the interval it was read on. The banding
 * lives here on purpose: where "ranging" ends and "trending" begins is a judgement that
 * moves as an eye changes, and a band stored alongside each trade would make every
 * finished run wrong the day one was retuned. Change the numbers below and every run
 * ever completed is relabelled — no re-run, no migration.
 */

import { INTERVAL_LABELS } from './marketData';
import type { CandleInterval } from '@/api/types';

export type MarketCycle = 'Low' | 'Medium' | 'High';

/** Lower bound of each band, in ADX. Below `medium` is a range; above `high` is a run. */
export const CYCLE_BANDS = {
  medium: 20,
  high: 40,
} as const;

export const CYCLE_LABELS: Record<MarketCycle, string> = {
  Low: 'LWC',
  Medium: 'MWC',
  High: 'HWC',
};

export const CYCLE_NAMES: Record<MarketCycle, string> = {
  Low: 'Low wave cycle',
  Medium: 'Medium wave cycle',
  High: 'High wave cycle',
};

export const CYCLE_COLORS: Record<MarketCycle, string> = {
  Low: 'gray',
  Medium: 'blue',
  High: 'grape',
};

export function cycleOf(adx: number | null | undefined): MarketCycle | null {
  if (adx === null || adx === undefined) {
    return null;
  }

  if (adx < CYCLE_BANDS.medium) {
    return 'Low';
  }

  return adx < CYCLE_BANDS.high ? 'Medium' : 'High';
}

/**
 * What the badge says on hover. The interval is never omitted: 18 on fifteen-minute bars
 * and 18 on four-hour bars describe different markets, so a reading without its timeframe
 * is worse than no reading.
 */
export function describeCycle(
  adx: number | null | undefined,
  interval: CandleInterval | null | undefined,
): string {
  const cycle = cycleOf(adx);

  if (cycle === null) {
    return interval
      ? `No ${INTERVAL_LABELS[interval]} ADX reading — the indicator was not warm when this opened.`
      : 'This run was completed before the engine measured the cycle. Re-run it to see one.';
  }

  const where = interval ? ` on ${INTERVAL_LABELS[interval]} bars` : '';

  return `${CYCLE_NAMES[cycle]} — ADX ${adx!.toFixed(1)}${where} at the bar that signalled the entry.`;
}
