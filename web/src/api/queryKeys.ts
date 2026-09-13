import type { AnalyticsFilters, TradeFilters } from '@/lib/filters';
import { dayEndIso, dayStartIso } from '@/lib/time';
import type { BreakdownDimension } from './types';

/**
 * Hierarchical so invalidation stays coarse: invalidating `['analytics']`
 * catches the summary, the curve, every breakdown and the mistake costs, which
 * is exactly what any write to a trade or a snapshot should do.
 */
export const queryKeys = {
  me: ['me'] as const,
  accounts: ['accounts'] as const,
  taxonomy: ['taxonomy'] as const,

  trades: ['trades'] as const,
  tradeList: (filters: TradeFilters) => ['trades', 'list', filters] as const,
  trade: (id: string) => ['trades', id] as const,
  inbox: ['inbox'] as const,

  analytics: ['analytics'] as const,
  summary: (filters: AnalyticsFilters) => ['analytics', 'summary', filters] as const,
  equity: (filters: AnalyticsFilters) => ['analytics', 'equity', filters] as const,
  breakdown: (dimension: BreakdownDimension, filters: AnalyticsFilters) =>
    ['analytics', 'breakdown', dimension, filters] as const,
  mistakes: (filters: AnalyticsFilters) => ['analytics', 'mistakes', filters] as const,

  holdings: (includeClosed: boolean) => ['holdings', { includeClosed }] as const,
  holdingsRoot: ['holdings'] as const,
  transfers: ['transfers'] as const,

  plans: ['plans'] as const,
  planList: (status?: string) => ['plans', 'list', status ?? 'all'] as const,

  sync: ['sync'] as const,
  syncStatus: ['sync', 'status'] as const,
  syncRuns: ['sync', 'runs'] as const,

  proxy: ['proxy'] as const,
};

/** The analytics query string every analytics endpoint shares. */
export function analyticsQuery(filters: AnalyticsFilters) {
  return {
    accountId: filters.accountId,
    from: dayStartIso(filters.from),
    to: dayEndIso(filters.to),
  };
}
