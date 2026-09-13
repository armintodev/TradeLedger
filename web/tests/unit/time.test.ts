import { describe, expect, it } from 'vitest';
import {
  dayEndIso,
  dayStartIso,
  formatDuration,
  formatElapsed,
  formatInstant,
  parseTimeSpan,
  resolveTimeZone,
  toDateString,
} from '@/lib/time';

describe('parseTimeSpan', () => {
  it('parses hours, minutes and seconds', () => {
    expect(parseTimeSpan('02:14:33')).toMatchObject({
      negative: false,
      days: 0,
      hours: 2,
      minutes: 14,
      seconds: 33,
    });
  });

  it('parses the day-prefixed form, which is the one new Date() cannot', () => {
    const parsed = parseTimeSpan('1.03:20:00');

    expect(parsed).toMatchObject({ days: 1, hours: 3, minutes: 20, seconds: 0 });
    expect(parsed?.totalMilliseconds).toBe(((24 + 3) * 3600 + 20 * 60) * 1000);
  });

  it('is needed because the Date constructor misreads a TimeSpan without failing', () => {
    // `new Date('02:14:33')` is at least Invalid Date. `new Date('1.03:20:00')`
    // is worse: it parses as a date in 2001 and never tells you.
    expect(Number.isNaN(new Date('02:14:33').getTime())).toBe(true);
    expect(new Date('1.03:20:00').getFullYear()).toBe(2001);
  });

  it('parses a short duration', () => {
    expect(parseTimeSpan('00:05:00')?.totalMilliseconds).toBe(300_000);
  });

  it('parses fractional seconds', () => {
    expect(parseTimeSpan('00:00:01.5000000')?.totalMilliseconds).toBe(1500);
  });

  it('parses a negative span', () => {
    const parsed = parseTimeSpan('-00:10:00');

    expect(parsed?.negative).toBe(true);
    expect(parsed?.totalMilliseconds).toBe(-600_000);
  });

  it('returns null rather than guessing', () => {
    expect(parseTimeSpan(null)).toBeNull();
    expect(parseTimeSpan(undefined)).toBeNull();
    expect(parseTimeSpan('')).toBeNull();
    expect(parseTimeSpan('not a duration')).toBeNull();
    expect(parseTimeSpan('2026-09-12T00:00:00Z')).toBeNull();
  });
});

describe('formatDuration', () => {
  it('renders at most two units', () => {
    expect(formatDuration('1.03:20:00')).toBe('1d 3h');
    expect(formatDuration('02:14:33')).toBe('2h 14m');
    expect(formatDuration('00:05:00')).toBe('5m');
  });

  it('falls through to seconds for a very short trade', () => {
    expect(formatDuration('00:00:42')).toBe('42s');
  });

  it('dashes a null', () => {
    expect(formatDuration(null)).toBe('—');
  });
});

describe('formatInstant', () => {
  const instant = '2026-03-15T14:30:00+00:00';

  it('honours an explicit time zone', () => {
    const utc = formatInstant(instant, { timeZone: 'UTC' });
    const tokyo = formatInstant(instant, { timeZone: 'Asia/Tokyo' });

    expect(utc).toContain('14:30');
    expect(tokyo).toContain('23:30');
  });

  it('renders a date alone when asked', () => {
    expect(formatInstant(instant, { timeZone: 'UTC', dateOnly: true })).not.toContain(':');
  });

  it('dashes a null', () => {
    expect(formatInstant(null)).toBe('—');
    expect(formatInstant('nonsense')).toBe('—');
  });
});

describe('resolveTimeZone', () => {
  const browser = Intl.DateTimeFormat().resolvedOptions().timeZone;

  it('uses a valid IANA id', () => {
    expect(resolveTimeZone('Asia/Tehran')).toBe('Asia/Tehran');
  });

  it('falls back when the id is one only the server understands', () => {
    // Valid server-side; a browser only knows IANA names.
    expect(resolveTimeZone('Iran Standard Time')).toBe(browser);
  });

  it('falls back on null', () => {
    expect(resolveTimeZone(null)).toBe(browser);
    expect(resolveTimeZone(undefined)).toBe(browser);
  });
});

describe('day boundaries', () => {
  it('turns a date string into instants spanning the local day', () => {
    const start = dayStartIso('2026-03-15');
    const end = dayEndIso('2026-03-15');

    expect(start).toBeDefined();
    expect(end).toBeDefined();
    expect(new Date(end!).getTime() - new Date(start!).getTime()).toBe(86_400_000 - 1);
  });

  it('passes undefined straight through', () => {
    expect(dayStartIso(undefined)).toBeUndefined();
    expect(dayEndIso(null)).toBeUndefined();
    expect(dayStartIso('not a date')).toBeUndefined();
  });

  it('round-trips a local date', () => {
    const date = new Date(2026, 2, 5);

    expect(toDateString(date)).toBe('2026-03-05');
  });
});

describe('formatElapsed', () => {
  const now = new Date('2026-03-15T12:00:00Z');

  it('counts seconds, then minutes, then hours', () => {
    expect(formatElapsed('2026-03-15T11:59:30Z', now)).toBe('30s');
    expect(formatElapsed('2026-03-15T11:55:30Z', now)).toBe('4m 30s');
    expect(formatElapsed('2026-03-15T09:30:00Z', now)).toBe('2h 30m');
  });

  it('dashes a null', () => {
    expect(formatElapsed(null, now)).toBe('—');
  });
});
