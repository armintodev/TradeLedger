import { describe, expect, it } from 'vitest';
import {
  ALL_INTERVALS,
  INTERVAL_MS,
  TRADEABLE_INTERVALS,
  backfillWindowFor,
  backfillWindowForAll,
  buildGapQuery,
  isCompleteRange,
  isTradeable,
  rangeToInstants,
  totalMissing,
  warmupSpanLabel,
} from '@/lib/marketData';
import { shouldPollJob } from '@/api/queries/marketData';
import { shouldPollRun } from '@/api/queries/backtests';
import type { BackfillJobResponse } from '@/api/types';

describe('rangeToInstants', () => {
  it('spans the whole local day at both ends', () => {
    const { from, to } = rangeToInstants('2026-03-15', '2026-03-15');

    expect(from).toBeDefined();
    expect(to).toBeDefined();
    expect(Date.parse(to!) - Date.parse(from!)).toBe(86_400_000 - 1);
  });

  it('is the single conversion both the gap probe and the queue body use', () => {
    // If these two ever diverged, the pre-flight would check a different window
    // than the server and would report clean a range the queue then refuses.
    const probe = buildGapQuery({
      source: 'BinanceFutures',
      symbol: 'btcusdt',
      interval: 'FourHours',
      from: '2026-01-01',
      to: '2026-02-01',
    });

    const submitted = rangeToInstants('2026-01-01', '2026-02-01');

    expect(probe?.from).toBe(submitted.from);
    expect(probe?.to).toBe(submitted.to);
  });

  it('passes an absent bound straight through', () => {
    expect(rangeToInstants(null, null)).toEqual({ from: undefined, to: undefined });
  });
});

describe('buildGapQuery', () => {
  const complete = {
    source: 'BinanceFutures' as const,
    symbol: 'BTCUSDT',
    interval: 'FourHours' as const,
    from: '2026-01-01',
    to: '2026-02-01',
  };

  it('upper-cases and trims the symbol, as the server does', () => {
    expect(buildGapQuery({ ...complete, symbol: '  btcusdt ' })?.symbol).toBe('BTCUSDT');
  });

  it('returns null until every one of the five parameters is present', () => {
    expect(buildGapQuery({ ...complete, symbol: '' })).toBeNull();
    expect(buildGapQuery({ ...complete, source: null })).toBeNull();
    expect(buildGapQuery({ ...complete, interval: null })).toBeNull();
    expect(buildGapQuery({ ...complete, from: null })).toBeNull();
    expect(buildGapQuery({ ...complete, to: null })).toBeNull();
  });

  it('returns null for an inverted range rather than asking and being refused', () => {
    expect(buildGapQuery({ ...complete, from: '2026-02-01', to: '2026-01-01' })).toBeNull();
  });

  it('accepts a complete query', () => {
    expect(buildGapQuery(complete)).toMatchObject({
      source: 'BinanceFutures',
      symbol: 'BTCUSDT',
      interval: 'FourHours',
    });
  });
});

describe('backfillWindowFor', () => {
  it('extends one interval past the last missing bar', () => {
    // A gap's `to` is the *open* time of the last missing bar, so a window
    // ending there would stop before that bar closes.
    const gap = { from: '2026-03-01T00:00:00Z', to: '2026-03-01T12:00:00Z' };
    const range = backfillWindowFor(gap, 'FourHours');

    expect(range.from).toBe(gap.from);
    expect(Date.parse(range.to) - Date.parse(gap.to)).toBe(INTERVAL_MS.FourHours);
  });

  it('turns a one-bar gap into a non-empty window', () => {
    // from === to for a single missing bar, and the API refuses to <= from with
    // `empty_backfill_window`. Sending a gap verbatim is a guaranteed 422.
    const gap = { from: '2026-03-01T00:00:00Z', to: '2026-03-01T00:00:00Z' };
    const range = backfillWindowFor(gap, 'OneHour');

    expect(Date.parse(range.to)).toBeGreaterThan(Date.parse(range.from));
  });

  it('spans every gap in a set', () => {
    const gaps = [
      { from: '2026-03-05T00:00:00Z', to: '2026-03-05T04:00:00Z' },
      { from: '2026-03-01T00:00:00Z', to: '2026-03-01T00:00:00Z' },
      { from: '2026-03-09T00:00:00Z', to: '2026-03-09T08:00:00Z' },
    ];

    const range = backfillWindowForAll(gaps, 'FourHours');

    expect(range?.from).toBe('2026-03-01T00:00:00Z');
    expect(Date.parse(range!.to)).toBe(Date.parse('2026-03-09T08:00:00Z') + INTERVAL_MS.FourHours);
  });

  it('has no window for an empty gap set', () => {
    expect(backfillWindowForAll([], 'OneHour')).toBeNull();
  });
});

describe('interval vocabulary', () => {
  it('excludes one minute from the tradeable set', () => {
    // It exists only to resolve which of a stop or target was hit first inside
    // a larger bar; the queue endpoint rejects it.
    expect(TRADEABLE_INTERVALS).not.toContain('OneMinute');
    expect(TRADEABLE_INTERVALS).toHaveLength(ALL_INTERVALS.length - 1);
    expect(isTradeable('OneMinute')).toBe(false);
    expect(isTradeable('FourHours')).toBe(true);
  });

  it('orders intervals by ascending duration', () => {
    const durations = ALL_INTERVALS.map((interval) => INTERVAL_MS[interval]);

    expect([...durations].sort((a, b) => a - b)).toEqual(durations);
  });
});

describe('warmupSpanLabel', () => {
  it('turns a bar count into something a person can act on', () => {
    expect(warmupSpanLabel(600, 'FourHours')).toBe('100 days');
    expect(warmupSpanLabel(12, 'OneHour')).toBe('12 hours');
    expect(warmupSpanLabel(1000, 'OneDay')).toBe('2.7 years');
  });
});

describe('totalMissing', () => {
  it('sums bar counts across gaps', () => {
    expect(totalMissing([{ missingCount: 39 }, { missingCount: 6 }, { missingCount: 12 }])).toBe(
      57,
    );
  });

  it('is zero for a clean range', () => {
    expect(totalMissing([])).toBe(0);
  });
});

describe('isCompleteRange', () => {
  it('needs both ends, in order', () => {
    expect(isCompleteRange('2026-01-01', '2026-02-01')).toBe(true);
    expect(isCompleteRange('2026-02-01', '2026-01-01')).toBe(false);
    expect(isCompleteRange('2026-01-01', null)).toBe(false);
  });
});

describe('shouldPollRun', () => {
  it('polls while a run is moving', () => {
    expect(shouldPollRun({ status: 'Queued', cancellationRequested: false })).toBe(true);
    expect(shouldPollRun({ status: 'Running', cancellationRequested: false })).toBe(true);
  });

  it('keeps polling a running run that was asked to cancel — it still has to finish', () => {
    expect(shouldPollRun({ status: 'Running', cancellationRequested: true })).toBe(true);
  });

  it('stops for a queued run that was cancelled, because it will never start', () => {
    // The worker's pickup query excludes it, so it stays Queued forever.
    // Polling would be a spinner that never resolves.
    expect(shouldPollRun({ status: 'Queued', cancellationRequested: true })).toBe(false);
  });

  it.each(['Succeeded', 'Failed', 'Cancelled'] as const)('stops once %s', (status) => {
    expect(shouldPollRun({ status, cancellationRequested: false })).toBe(false);
  });
});

describe('shouldPollJob', () => {
  const job = (status: BackfillJobResponse['status']) => ({ status }) as BackfillJobResponse;

  it('polls while a job is live', () => {
    expect(shouldPollJob(job('Queued'))).toBe(true);
    expect(shouldPollJob(job('Running'))).toBe(true);
  });

  it.each(['Succeeded', 'Failed', 'Cancelled'] as const)('stops once %s', (status) => {
    expect(shouldPollJob(job(status))).toBe(false);
  });

  it('does not poll a job that has not loaded', () => {
    expect(shouldPollJob(undefined)).toBe(false);
  });
});
