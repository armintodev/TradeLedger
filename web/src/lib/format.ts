/**
 * Every number on screen goes through here. No component calls `toFixed`.
 *
 * `decimal` arrives as a JSON number, so these are float64 by the time they
 * reach the client. That is deliberate and safe only because the client never
 * does money arithmetic — the API computes every aggregate. See `web/SPEC.md` —
 * Decisions and Trade-offs.
 */

/** What a null, undefined or NaN value renders as. Never `0`, never `NaN`. */
export const DASH = '—';

/** U+2212, so a negative reads as a minus rather than a hyphen. */
const MINUS = '−';

export type Nullable = number | null | undefined;

function isBlank(value: Nullable): value is null | undefined {
  return value === null || value === undefined || Number.isNaN(value);
}

function group(
  value: number,
  minimumFractionDigits: number,
  maximumFractionDigits: number,
): string {
  return new Intl.NumberFormat('en-US', {
    minimumFractionDigits,
    maximumFractionDigits,
  }).format(value);
}

/** Money: balances, PnL, fees, funding. Two decimals unless the value is tiny. */
export function formatMoney(value: Nullable, options: { currency?: string } = {}): string {
  if (isBlank(value)) {
    return DASH;
  }

  const magnitude = Math.abs(value);
  const body =
    magnitude !== 0 && magnitude < 0.01 ? formatSignificant(value, 4) : group(value, 2, 2);
  const suffix = options.currency ? ` ${options.currency}` : '';

  return `${body}${suffix}`;
}

/**
 * Signed money. Requirement 66: the sign is explicit so colour is never the
 * only signal.
 */
export function formatPnl(value: Nullable, options: { currency?: string } = {}): string {
  if (isBlank(value)) {
    return DASH;
  }

  const rendered = formatMoney(Math.abs(value), options);

  if (value > 0) {
    return `+${rendered}`;
  }

  if (value < 0) {
    return `${MINUS}${rendered}`;
  }

  return rendered;
}

/**
 * Prices span sub-satoshi meme-coin quotes to five-figure BTC, so they are
 * formatted by significant digits rather than a fixed 2dp.
 */
export function formatPrice(value: Nullable): string {
  if (isBlank(value)) {
    return DASH;
  }

  if (value === 0) {
    return '0';
  }

  const magnitude = Math.abs(value);

  if (magnitude >= 1000) {
    return group(value, 2, 2);
  }

  if (magnitude >= 1) {
    return group(value, 2, 6);
  }

  return formatSignificant(value, 6);
}

/** Quantities: up to 12 fractional digits on the wire, trimmed here. */
export function formatQuantity(value: Nullable): string {
  if (isBlank(value)) {
    return DASH;
  }

  const magnitude = Math.abs(value);

  if (magnitude !== 0 && magnitude < 0.0001) {
    return formatSignificant(value, 6);
  }

  return group(value, 0, 8);
}

/** A percentage the API already multiplied by 100 (`winRate`, `maxDrawdownPercent`). */
export function formatPercent(value: Nullable, fractionDigits = 2): string {
  if (isBlank(value)) {
    return DASH;
  }

  return `${group(value, fractionDigits, fractionDigits)}%`;
}

/** A fraction the API sends as 0–1 (`riskFraction`, `averageFeeRate`). */
export function formatFractionAsPercent(value: Nullable, fractionDigits = 2): string {
  if (isBlank(value)) {
    return DASH;
  }

  return `${group(value * 100, fractionDigits, fractionDigits)}%`;
}

/** An R multiple. Signed, because a negative R is the whole point of the number. */
export function formatR(value: Nullable): string {
  if (isBlank(value)) {
    return DASH;
  }

  const body = group(Math.abs(value), 2, 2);

  if (value > 0) {
    return `+${body}R`;
  }

  if (value < 0) {
    return `${MINUS}${body}R`;
  }

  return `${body}R`;
}

/**
 * `profitFactor` is null when there are no losing trades. Requirement 13: that
 * renders as a dash, never as infinity and never as zero.
 */
export function formatRatio(value: Nullable, fractionDigits = 2): string {
  if (isBlank(value) || !Number.isFinite(value)) {
    return DASH;
  }

  return group(value, fractionDigits, fractionDigits);
}

export function formatInteger(value: Nullable): string {
  if (isBlank(value)) {
    return DASH;
  }

  return group(value, 0, 0);
}

/** Which side of zero a value falls on, for colour. */
export function signOf(value: Nullable): 'positive' | 'negative' | 'zero' {
  if (isBlank(value) || value === 0) {
    return 'zero';
  }

  return value > 0 ? 'positive' : 'negative';
}

function formatSignificant(value: number, digits: number): string {
  // `maximumSignificantDigits` keeps 0.0000000123 readable where a fixed 2dp
  // would render it as 0.00.
  return new Intl.NumberFormat('en-US', {
    maximumSignificantDigits: digits,
  }).format(value);
}
