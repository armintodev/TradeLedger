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

  // Backtests. Three roots rather than one, so invalidating a run does not
  // needlessly refetch every strategy.
  backtestRuns: ['backtest-runs'] as const,
  backtestRunList: (filters: BacktestRunFilters) => ['backtest-runs', 'list', filters] as const,
  backtestRun: (id: string) => ['backtest-runs', id] as const,
  backtestRunTrades: (id: string, page: number) => ['backtest-runs', id, 'trades', page] as const,
  backtestRunEquity: (id: string) => ['backtest-runs', id, 'equity'] as const,

  backtestAccounts: ['backtest-accounts'] as const,
  backtestAccountList: (includeInactive: boolean) =>
    ['backtest-accounts', 'list', { includeInactive }] as const,
  backtestAccount: (id: string) => ['backtest-accounts', id] as const,
  backtestAccountEquity: (id: string) => ['backtest-accounts', id, 'equity'] as const,
  backtestAccountSummary: (id: string) => ['backtest-accounts', id, 'summary'] as const,

  backtestStrategies: ['backtest-strategies'] as const,
  backtestStrategyList: (includeInactive: boolean) =>
    ['backtest-strategies', 'list', { includeInactive }] as const,
  backtestStrategy: (id: string) => ['backtest-strategies', id] as const,
  indicators: ['backtest-strategies', 'indicators'] as const,

  /**
   * Keyed on the document text. A given document's verdict cannot change, so
   * this caches forever and undo back to a checked shape is instant.
   */
  ruleValidation: (json: string) => ['rule-validation', json] as const,

  marketData: ['market-data'] as const,
  coverage: ['market-data', 'coverage'] as const,
  gaps: (query: GapQuery | null) => ['market-data', 'gaps', query] as const,
  backfillJob: (id: string) => ['market-data', 'backfill', id] as const,
};

export interface BacktestRunFilters {
  accountId?: string;
  status?: string;
  page: number;
  pageSize: number;
}

export interface GapQuery {
  source: string;
  symbol: string;
  interval: string;
  from: string;
  to: string;
}

/** The analytics query string every analytics endpoint shares. */
export function analyticsQuery(filters: AnalyticsFilters) {
  return {
    accountId: filters.accountId,
    from: dayStartIso(filters.from),
    to: dayEndIso(filters.to),
  };
}
