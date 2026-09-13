import type { MarketSessionFlag, ReviewState } from '@/api/types';
import { toDateString } from './time';
import type { BacktestRunFilters } from '@/api/queryKeys';

/**
 * Filters live in the URL query string, so a filtered view is linkable and
 * survives a reload. React Router has no typed search params, so the mapping
 * is done here by hand — see `web/SPEC.md` — Decisions and Trade-offs.
 */

export const DEFAULT_PAGE_SIZE = 50;
export const MAX_PAGE_SIZE = 200;

export interface TradeFilters {
  accountId?: string;
  symbol?: string;
  reviewState?: ReviewState;
  marketSession?: MarketSessionFlag;
  from?: string;
  to?: string;
  page: number;
  pageSize: number;
}

export interface AnalyticsFilters {
  accountId?: string;
  from?: string;
  to?: string;
  /** Which preset produced `from`/`to`, so the control can show it selected. */
  range: RangePreset;
}

export type RangePreset = '7d' | '30d' | '90d' | 'ytd' | 'all' | 'custom';

const REVIEW_STATES: ReviewState[] = ['Unreviewed', 'Reviewed'];
const SESSIONS: MarketSessionFlag[] = ['Tokyo', 'London', 'NewYork'];

export function readTradeFilters(params: URLSearchParams): TradeFilters {
  return {
    accountId: params.get('accountId') ?? undefined,
    symbol: params.get('symbol') ?? undefined,
    reviewState: oneOf(params.get('reviewState'), REVIEW_STATES),
    marketSession: oneOf(params.get('marketSession'), SESSIONS),
    from: params.get('from') ?? undefined,
    to: params.get('to') ?? undefined,
    page: positiveInt(params.get('page'), 1),
    pageSize: Math.min(positiveInt(params.get('pageSize'), DEFAULT_PAGE_SIZE), MAX_PAGE_SIZE),
  };
}

export function writeTradeFilters(filters: TradeFilters): URLSearchParams {
  const params = new URLSearchParams();

  set(params, 'accountId', filters.accountId);
  set(params, 'symbol', filters.symbol);
  set(params, 'reviewState', filters.reviewState);
  set(params, 'marketSession', filters.marketSession);
  set(params, 'from', filters.from);
  set(params, 'to', filters.to);

  // Defaults stay out of the URL so a plain /trades link is clean.
  if (filters.page > 1) {
    params.set('page', String(filters.page));
  }

  if (filters.pageSize !== DEFAULT_PAGE_SIZE) {
    params.set('pageSize', String(filters.pageSize));
  }

  return params;
}

/** True when anything other than paging is set — drives the Clear filters control. */
export function hasTradeFilters(filters: TradeFilters): boolean {
  return Boolean(
    filters.accountId ||
    filters.symbol ||
    filters.reviewState ||
    filters.marketSession ||
    filters.from ||
    filters.to,
  );
}

export function readAnalyticsFilters(params: URLSearchParams): AnalyticsFilters {
  const range = oneOf<RangePreset>(params.get('range'), [
    '7d',
    '30d',
    '90d',
    'ytd',
    'all',
    'custom',
  ]);

  const explicitFrom = params.get('from') ?? undefined;
  const explicitTo = params.get('to') ?? undefined;

  const resolved = range ?? (explicitFrom || explicitTo ? 'custom' : 'all');

  const bounds =
    resolved === 'custom' ? { from: explicitFrom, to: explicitTo } : rangeBounds(resolved);

  return {
    accountId: params.get('accountId') ?? undefined,
    from: bounds.from,
    to: bounds.to,
    range: resolved,
  };
}

export function writeAnalyticsFilters(filters: AnalyticsFilters): URLSearchParams {
  const params = new URLSearchParams();

  set(params, 'accountId', filters.accountId);

  if (filters.range !== 'all') {
    params.set('range', filters.range);
  }

  // A preset's bounds are derived on read, so only a custom range needs them
  // written out — otherwise a bookmarked "7d" would freeze to the day it was
  // bookmarked.
  if (filters.range === 'custom') {
    set(params, 'from', filters.from);
    set(params, 'to', filters.to);
  }

  return params;
}

/** The `from`/`to` a preset means, resolved against now. */
export function rangeBounds(
  range: RangePreset,
  now: Date = new Date(),
): { from?: string; to?: string } {
  if (range === 'all' || range === 'custom') {
    return {};
  }

  const from = new Date(now);

  switch (range) {
    case '7d':
      from.setDate(from.getDate() - 7);
      break;
    case '30d':
      from.setDate(from.getDate() - 30);
      break;
    case '90d':
      from.setDate(from.getDate() - 90);
      break;
    case 'ytd':
      from.setMonth(0, 1);
      break;
  }

  return { from: toDateString(from), to: undefined };
}

// ---------------------------------------------------------------- backtests

const RUN_STATUSES = ['Queued', 'Running', 'Succeeded', 'Failed', 'Cancelled'] as const;

export type RunStatus = (typeof RUN_STATUSES)[number];

export const DEFAULT_RUN_PAGE_SIZE = 50;

export function readRunFilters(params: URLSearchParams): BacktestRunFilters {
  return {
    accountId: params.get('accountId') ?? undefined,
    status: oneOf<RunStatus>(params.get('status'), [...RUN_STATUSES]),
    page: positiveInt(params.get('page'), 1),
    pageSize: Math.min(positiveInt(params.get('pageSize'), DEFAULT_RUN_PAGE_SIZE), 200),
  };
}

/**
 * The runs list lives behind `?tab=runs`, so its filters have to be written
 * alongside the tab rather than replacing the whole query string.
 */
export function writeRunFilters(
  filters: BacktestRunFilters,
  existing: URLSearchParams,
): URLSearchParams {
  const params = new URLSearchParams(existing);

  for (const key of ['accountId', 'status', 'page', 'pageSize']) {
    params.delete(key);
  }

  set(params, 'accountId', filters.accountId);
  set(params, 'status', filters.status);

  if (filters.page > 1) {
    params.set('page', String(filters.page));
  }

  if (filters.pageSize !== DEFAULT_RUN_PAGE_SIZE) {
    params.set('pageSize', String(filters.pageSize));
  }

  return params;
}

export function hasRunFilters(filters: BacktestRunFilters): boolean {
  return Boolean(filters.accountId || filters.status);
}

export const DEFAULT_POSITION_PAGE_SIZE = 100;
export const MAX_POSITION_PAGE_SIZE = 500;

export interface RunPositionFilters {
  page: number;
  pageSize: number;
  /**
   * The position whose detail panel is open. It lives in the URL rather than in
   * component state so a single simulated position is linkable — the whole point
   * of the page is being able to point at one trade and ask what the engine did.
   */
  position?: string;
}

export function readRunPositionFilters(params: URLSearchParams): RunPositionFilters {
  return {
    page: positiveInt(params.get('page'), 1),
    pageSize: Math.min(
      positiveInt(params.get('pageSize'), DEFAULT_POSITION_PAGE_SIZE),
      MAX_POSITION_PAGE_SIZE,
    ),
    position: params.get('position') ?? undefined,
  };
}

export function writeRunPositionFilters(filters: RunPositionFilters): URLSearchParams {
  const params = new URLSearchParams();

  if (filters.page > 1) {
    params.set('page', String(filters.page));
  }

  if (filters.pageSize !== DEFAULT_POSITION_PAGE_SIZE) {
    params.set('pageSize', String(filters.pageSize));
  }

  set(params, 'position', filters.position);

  return params;
}

export const RANGE_LABELS: Record<RangePreset, string> = {
  '7d': '7 days',
  '30d': '30 days',
  '90d': '90 days',
  ytd: 'YTD',
  all: 'All',
  custom: 'Custom',
};

function set(params: URLSearchParams, key: string, value: string | undefined): void {
  if (value) {
    params.set(key, value);
  }
}

function oneOf<T extends string>(value: string | null, allowed: T[]): T | undefined {
  return value && (allowed as string[]).includes(value) ? (value as T) : undefined;
}

function positiveInt(value: string | null, fallback: number): number {
  const parsed = Number(value);

  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
}
