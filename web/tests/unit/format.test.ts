import { describe, expect, it } from 'vitest';
import {
  DASH,
  formatFractionAsPercent,
  formatInteger,
  formatMoney,
  formatPercent,
  formatPnl,
  formatPrice,
  formatQuantity,
  formatR,
  formatRatio,
  signOf,
} from '@/lib/format';

const MINUS = '−';

describe('formatMoney', () => {
  it('groups and fixes to two decimals', () => {
    expect(formatMoney(1234.5)).toBe('1,234.50');
    expect(formatMoney(0)).toBe('0.00');
  });

  it('keeps a tiny amount visible instead of rounding it to zero', () => {
    expect(formatMoney(0.00004)).not.toBe('0.00');
  });

  it('appends a currency when asked', () => {
    expect(formatMoney(10, { currency: 'USDT' })).toBe('10.00 USDT');
  });

  it('dashes a null', () => {
    expect(formatMoney(null)).toBe(DASH);
    expect(formatMoney(undefined)).toBe(DASH);
    expect(formatMoney(Number.NaN)).toBe(DASH);
  });
});

describe('formatPnl', () => {
  it('signs a profit and a loss explicitly, so colour is never the only signal', () => {
    expect(formatPnl(120.5)).toBe('+120.50');
    expect(formatPnl(-120.5)).toBe(`${MINUS}120.50`);
  });

  it('leaves zero unsigned', () => {
    expect(formatPnl(0)).toBe('0.00');
  });

  it('dashes a null rather than showing zero', () => {
    expect(formatPnl(null)).toBe(DASH);
  });
});

describe('formatPrice', () => {
  it('keeps a sub-satoshi quote readable', () => {
    // A fixed 2dp would render this as 0.00.
    expect(formatPrice(0.0000000123)).toBe('0.0000000123');
  });

  it('fixes a large price to two decimals', () => {
    expect(formatPrice(64231.5)).toBe('64,231.50');
  });

  it('renders zero as zero and null as a dash', () => {
    expect(formatPrice(0)).toBe('0');
    expect(formatPrice(null)).toBe(DASH);
  });
});

describe('formatQuantity', () => {
  it('does not force decimals on a whole number', () => {
    expect(formatQuantity(12)).toBe('12');
  });

  it('keeps a dust quantity visible', () => {
    expect(formatQuantity(0.000001234)).not.toBe('0');
  });
});

describe('percentages', () => {
  it('formats a value the API already scaled', () => {
    expect(formatPercent(62.5)).toBe('62.50%');
  });

  it('scales a 0–1 fraction', () => {
    expect(formatFractionAsPercent(0.015)).toBe('1.50%');
  });

  it('dashes a null', () => {
    expect(formatPercent(null)).toBe(DASH);
    expect(formatFractionAsPercent(null)).toBe(DASH);
  });
});

describe('formatR', () => {
  it('signs an R multiple', () => {
    expect(formatR(2.4)).toBe('+2.40R');
    expect(formatR(-1)).toBe(`${MINUS}1.00R`);
    expect(formatR(0)).toBe('0.00R');
  });

  it('dashes a null, which is what an unsized trade has', () => {
    expect(formatR(null)).toBe(DASH);
  });
});

describe('formatRatio', () => {
  it('renders a profit factor', () => {
    expect(formatRatio(1.8342)).toBe('1.83');
  });

  it('dashes a null profit factor rather than showing infinity or zero', () => {
    expect(formatRatio(null)).toBe(DASH);
    expect(formatRatio(Number.POSITIVE_INFINITY)).toBe(DASH);
  });
});

describe('formatInteger', () => {
  it('groups thousands', () => {
    expect(formatInteger(10000)).toBe('10,000');
  });

  it('dashes a null', () => {
    expect(formatInteger(null)).toBe(DASH);
  });
});

describe('signOf', () => {
  it('classifies a value for colour', () => {
    expect(signOf(1)).toBe('positive');
    expect(signOf(-1)).toBe('negative');
    expect(signOf(0)).toBe('zero');
    expect(signOf(null)).toBe('zero');
  });
});
