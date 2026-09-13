import { describe, expect, it } from 'vitest';
import {
  DEFAULT_PAGE_SIZE,
  MAX_PAGE_SIZE,
  hasTradeFilters,
  rangeBounds,
  readAnalyticsFilters,
  readTradeFilters,
  writeAnalyticsFilters,
  writeTradeFilters,
} from '@/lib/filters';

describe('trade filters', () => {
  it('round-trips through the query string', () => {
    const filters = {
      accountId: 'e9a1b0c4-0000-4000-8000-000000000001',
      symbol: 'BTCUSDT',
      reviewState: 'Unreviewed' as const,
      marketSession: 'London' as const,
      from: '2026-01-01',
      to: '2026-02-01',
      page: 3,
      pageSize: 100,
    };

    expect(readTradeFilters(writeTradeFilters(filters))).toEqual(filters);
  });

  it('omits defaults so a plain link stays clean', () => {
    const params = writeTradeFilters({ page: 1, pageSize: DEFAULT_PAGE_SIZE });

    expect(params.toString()).toBe('');
  });

  it('omits empty values rather than sending them blank', () => {
    const params = writeTradeFilters({
      symbol: undefined,
      accountId: undefined,
      page: 1,
      pageSize: DEFAULT_PAGE_SIZE,
    });

    expect(params.has('symbol')).toBe(false);
    expect(params.has('accountId')).toBe(false);
  });

  it('ignores a value outside the enum rather than sending it to the API', () => {
    const filters = readTradeFilters(
      new URLSearchParams('reviewState=Whatever&marketSession=Mars'),
    );

    expect(filters.reviewState).toBeUndefined();
    expect(filters.marketSession).toBeUndefined();
  });

  it('caps the page size at the API maximum', () => {
    expect(readTradeFilters(new URLSearchParams('pageSize=5000')).pageSize).toBe(MAX_PAGE_SIZE);
  });

  it('falls back to page 1 on nonsense', () => {
    expect(readTradeFilters(new URLSearchParams('page=-4')).page).toBe(1);
    expect(readTradeFilters(new URLSearchParams('page=abc')).page).toBe(1);
  });

  it('knows when only paging is set', () => {
    expect(hasTradeFilters({ page: 2, pageSize: 50 })).toBe(false);
    expect(hasTradeFilters({ page: 1, pageSize: 50, symbol: 'ETHUSDT' })).toBe(true);
  });
});

describe('analytics filters', () => {
  it('defaults to the whole history', () => {
    const filters = readAnalyticsFilters(new URLSearchParams());

    expect(filters.range).toBe('all');
    expect(filters.from).toBeUndefined();
    expect(filters.to).toBeUndefined();
  });

  it('stores the preset rather than the dates it resolved to', () => {
    const params = writeAnalyticsFilters({ range: '30d', from: '2026-01-01', to: '2026-02-01' });

    // A bookmarked "30 days" has to still mean the last thirty days tomorrow.
    expect(params.get('range')).toBe('30d');
    expect(params.has('from')).toBe(false);
    expect(params.has('to')).toBe(false);
  });

  it('writes explicit bounds for a custom range', () => {
    const params = writeAnalyticsFilters({ range: 'custom', from: '2026-01-01', to: '2026-02-01' });

    expect(params.get('from')).toBe('2026-01-01');
    expect(params.get('to')).toBe('2026-02-01');
  });

  it('treats bare dates as a custom range', () => {
    const filters = readAnalyticsFilters(new URLSearchParams('from=2026-01-01'));

    expect(filters.range).toBe('custom');
    expect(filters.from).toBe('2026-01-01');
  });

  it('resolves a preset to a date-only lower bound', () => {
    const bounds = rangeBounds('7d', new Date('2026-03-15T12:00:00Z'));

    expect(bounds.from).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(bounds.to).toBeUndefined();
  });

  it('leaves "all" unbounded', () => {
    expect(rangeBounds('all')).toEqual({});
  });
});
