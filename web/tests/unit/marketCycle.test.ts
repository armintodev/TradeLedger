import { describe, expect, it } from 'vitest';
import { CYCLE_BANDS, cycleOf, describeCycle } from '@/lib/marketCycle';

describe('banding an ADX reading', () => {
  it('puts the boundary value in the higher band', () => {
    // Inclusive lower bounds, so a reading of exactly 20 is already out of the range.
    expect(cycleOf(CYCLE_BANDS.medium - 0.1)).toBe('Low');
    expect(cycleOf(CYCLE_BANDS.medium)).toBe('Medium');
    expect(cycleOf(CYCLE_BANDS.high - 0.1)).toBe('Medium');
    expect(cycleOf(CYCLE_BANDS.high)).toBe('High');
  });

  it('reads zero as a range rather than as no reading', () => {
    // The falsy trap: 0 is a real ADX value and must not collapse to null.
    expect(cycleOf(0)).toBe('Low');
  });

  it('has no band for an absent reading', () => {
    expect(cycleOf(null)).toBeNull();
    expect(cycleOf(undefined)).toBeNull();
  });
});

describe('describing a reading', () => {
  it('always names the timeframe it was taken on', () => {
    // A bare "ADX 18" invites reading it as the run's own interval, which is the whole
    // thing the stored interval exists to prevent.
    expect(describeCycle(18, 'FourHours')).toContain('4h');
    expect(describeCycle(18, 'FourHours')).toContain('Low wave cycle');
  });

  it('separates an unwarm reading from a run that never measured one', () => {
    expect(describeCycle(null, 'FourHours')).toContain('not warm');
    expect(describeCycle(null, null)).toContain('Re-run');
  });
});
