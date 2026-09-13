import { useQuery } from '@tanstack/react-query';
import { api } from '../client';
import { analyticsQuery, queryKeys } from '../queryKeys';
import type { AnalyticsFilters } from '@/lib/filters';
import type {
  BreakdownDimension,
  BreakdownRow,
  EquityCurve,
  MistakeCost,
  PerformanceSummary,
} from '../types';

export function useSummary(filters: AnalyticsFilters) {
  return useQuery({
    queryKey: queryKeys.summary(filters),
    queryFn: () =>
      api.get<PerformanceSummary>('/api/analytics/summary', { query: analyticsQuery(filters) }),
  });
}

/**
 * The equity curve comes from `BalanceSnapshot`, never from summing trades —
 * `CLAUDE.md` §3. An account with no snapshots has no curve, which is an empty
 * state rather than a flat zero line.
 */
export function useEquityCurve(filters: AnalyticsFilters) {
  return useQuery({
    queryKey: queryKeys.equity(filters),
    queryFn: () =>
      api.get<EquityCurve>('/api/analytics/equity-curve', { query: analyticsQuery(filters) }),
  });
}

export function useBreakdown(dimension: BreakdownDimension, filters: AnalyticsFilters) {
  return useQuery({
    queryKey: queryKeys.breakdown(dimension, filters),
    queryFn: () =>
      api.get<BreakdownRow[]>(`/api/analytics/breakdown/${dimension}`, {
        query: analyticsQuery(filters),
      }),
    placeholderData: (previous) => previous,
  });
}

export function useMistakeCosts(filters: AnalyticsFilters) {
  return useQuery({
    queryKey: queryKeys.mistakes(filters),
    queryFn: () =>
      api.get<MistakeCost[]>('/api/analytics/mistakes', { query: analyticsQuery(filters) }),
  });
}

export const BREAKDOWN_DIMENSIONS: { value: BreakdownDimension; label: string }[] = [
  { value: 'Strategy', label: 'Strategy' },
  { value: 'Symbol', label: 'Symbol' },
  { value: 'Side', label: 'Side' },
  { value: 'Timeframe', label: 'Timeframe' },
  { value: 'MarketSession', label: 'Market session' },
  { value: 'EntryMentalState', label: 'Entry mental state' },
  { value: 'ExitMentalState', label: 'Exit mental state' },
  { value: 'DayOfWeek', label: 'Day of week' },
  { value: 'HourOfDay', label: 'Hour of day' },
  { value: 'Planned', label: 'Planned vs unplanned' },
];
