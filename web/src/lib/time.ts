import { DASH } from './format';

/**
 * Instants and durations.
 *
 * Everything server-side is UTC and arrives as an ISO 8601 `DateTimeOffset`.
 * Durations are .NET `TimeSpan`s, which `new Date()` cannot parse at all.
 */

/** `[-][d.]hh:mm:ss[.fffffff]` — the shape `TimeSpan` serialises to. */
const TIMESPAN = /^(-)?(?:(\d+)\.)?(\d{1,2}):(\d{2}):(\d{2})(?:\.(\d{1,7}))?$/;

export interface ParsedTimeSpan {
  negative: boolean;
  days: number;
  hours: number;
  minutes: number;
  seconds: number;
  totalMilliseconds: number;
}

/**
 * `"02:14:33"` and `"1.03:20:00"` are both valid and mean very different
 * things. Returns null for anything unparseable rather than guessing.
 */
export function parseTimeSpan(value: string | null | undefined): ParsedTimeSpan | null {
  if (!value) {
    return null;
  }

  const match = TIMESPAN.exec(value.trim());

  if (!match) {
    return null;
  }

  const [, sign, days, hours, minutes, seconds, fraction] = match;

  const parsed: ParsedTimeSpan = {
    negative: sign === '-',
    days: days ? Number(days) : 0,
    hours: Number(hours),
    minutes: Number(minutes),
    seconds: Number(seconds),
    totalMilliseconds: 0,
  };

  const fractionalMs = fraction ? Number(`0.${fraction}`) * 1000 : 0;

  parsed.totalMilliseconds =
    (parsed.days * 86_400 + parsed.hours * 3_600 + parsed.minutes * 60 + parsed.seconds) * 1000 +
    fractionalMs;

  if (parsed.negative) {
    parsed.totalMilliseconds = -parsed.totalMilliseconds;
  }

  return parsed;
}

/**
 * `"1.03:20:00"` becomes `"1d 3h 20m"`. Two units at most: the third is noise
 * in a table and nobody reads seconds on a two-day swing.
 */
export function formatDuration(value: string | null | undefined): string {
  const parsed = parseTimeSpan(value);

  if (!parsed) {
    return DASH;
  }

  const units: string[] = [];

  if (parsed.days > 0) {
    units.push(`${parsed.days}d`);
  }

  if (parsed.hours > 0) {
    units.push(`${parsed.hours}h`);
  }

  if (parsed.minutes > 0 && units.length < 2) {
    units.push(`${parsed.minutes}m`);
  }

  if (units.length === 0) {
    units.push(`${parsed.seconds}s`);
  }

  const body = units.slice(0, 2).join(' ');

  return parsed.negative ? `-${body}` : body;
}

/**
 * The trader's own zone, so hour-of-day and session analysis agree with the
 * journal regardless of where they are. Falls back to the browser's.
 */
export function resolveTimeZone(timeZoneId?: string | null): string {
  const browser = Intl.DateTimeFormat().resolvedOptions().timeZone;

  if (!timeZoneId) {
    return browser;
  }

  try {
    // A Windows zone id like "Iran Standard Time" is valid server-side but not
    // in the browser, which only knows IANA names.
    new Intl.DateTimeFormat('en-US', { timeZone: timeZoneId }).format(new Date());
    return timeZoneId;
  } catch {
    return browser;
  }
}

export interface InstantOptions {
  timeZone?: string;
  /** Omit the time and render the date alone. */
  dateOnly?: boolean;
  /** Include seconds. Off by default: they are rarely what is being compared. */
  seconds?: boolean;
}

export function formatInstant(
  value: string | null | undefined,
  options: InstantOptions = {},
): string {
  const date = parseInstant(value);

  if (!date) {
    return DASH;
  }

  const timeZone = options.timeZone ?? Intl.DateTimeFormat().resolvedOptions().timeZone;

  const format: Intl.DateTimeFormatOptions = {
    timeZone,
    year: 'numeric',
    month: 'short',
    day: '2-digit',
  };

  if (!options.dateOnly) {
    format.hour = '2-digit';
    format.minute = '2-digit';
    format.hour12 = false;

    if (options.seconds) {
      format.second = '2-digit';
    }
  }

  return new Intl.DateTimeFormat('en-GB', format).format(date);
}

export function parseInstant(value: string | null | undefined): Date | null {
  if (!value) {
    return null;
  }

  const parsed = new Date(value);

  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

/** Elapsed time since an instant, for a sync run that has not finished. */
export function formatElapsed(since: string | null | undefined, now: Date = new Date()): string {
  const start = parseInstant(since);

  if (!start) {
    return DASH;
  }

  const seconds = Math.max(0, Math.floor((now.getTime() - start.getTime()) / 1000));

  if (seconds < 60) {
    return `${seconds}s`;
  }

  const minutes = Math.floor(seconds / 60);

  if (minutes < 60) {
    return `${minutes}m ${seconds % 60}s`;
  }

  return `${Math.floor(minutes / 60)}h ${minutes % 60}m`;
}

/**
 * Filters carry date-only strings (`YYYY-MM-DD`) so a URL stays readable. The
 * API wants instants, and the boundary is interpreted in the viewer's local
 * zone — a trader filtering "today" means their today.
 */
export function dayStartIso(date: string | null | undefined): string | undefined {
  if (!date) {
    return undefined;
  }

  const parsed = new Date(`${date}T00:00:00`);

  return Number.isNaN(parsed.getTime()) ? undefined : parsed.toISOString();
}

export function dayEndIso(date: string | null | undefined): string | undefined {
  if (!date) {
    return undefined;
  }

  const parsed = new Date(`${date}T23:59:59.999`);

  return Number.isNaN(parsed.getTime()) ? undefined : parsed.toISOString();
}

/** `YYYY-MM-DD` in local time, which is what the date pickers exchange. */
export function toDateString(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');

  return `${year}-${month}-${day}`;
}
