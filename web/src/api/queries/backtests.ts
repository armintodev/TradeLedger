import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../client';
import { queryKeys, type BacktestRunFilters } from '../queryKeys';
import type {
  BacktestAccountResponse,
  BacktestRunResponse,
  BacktestStrategyResponse,
  BacktestTradeResponse,
  CreateBacktestAccountRequest,
  EquityCurve,
  Guid,
  IndicatorDescriptionResponse,
  PagedResult,
  PerformanceSummary,
  QueueBacktestRequest,
  RuleValidationResponse,
  SaveBacktestStrategyRequest,
  UpdateBacktestAccountRequest,
} from '../types';

/**
 * The worker throttles progress writes to one per two seconds, so polling
 * faster than this buys nothing but load.
 */
export const RUN_POLL_INTERVAL_MS = 3000;

/** Trades page but return a bare array, so there is no total to page against. */
export const TRADES_PAGE_SIZE = 100;

/**
 * Bounds the API enforces but does not expose. Hardcoded here deliberately —
 * without them the user meets each one as a 400 after pressing Queue.
 */
export const RUN_LIMITS = {
  riskPercent: { min: 1, max: 5 },
  riskReward: { min: 2 },
  leverage: { min: 1, max: 25 },
} as const;

// ---------------------------------------------------------------- runs

export function useBacktestRuns(filters: BacktestRunFilters) {
  return useQuery({
    queryKey: queryKeys.backtestRunList(filters),
    queryFn: () =>
      api.get<PagedResult<BacktestRunResponse>>('/api/backtests', {
        query: {
          accountId: filters.accountId,
          status: filters.status,
          page: filters.page,
          pageSize: filters.pageSize,
        },
      }),
    placeholderData: (previous) => previous,
    // A queued or running row on this page should tick without the user
    // opening it.
    refetchInterval: (query) =>
      query.state.data?.items.some(shouldPollRun) ? RUN_POLL_INTERVAL_MS : false,
  });
}

/**
 * Whether a run is still moving.
 *
 * A queued run whose cancellation was requested is the exception: the worker's
 * pickup query excludes it, so it will never start and never transition. Polling
 * it forever would be a spinner that never stops.
 */
export function shouldPollRun(run: Pick<BacktestRunResponse, 'status' | 'cancellationRequested'>) {
  if (run.status === 'Queued' && run.cancellationRequested) {
    return false;
  }

  return run.status === 'Queued' || run.status === 'Running';
}

export function useBacktestRun(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.backtestRun(id ?? ''),
    queryFn: () => api.get<BacktestRunResponse>(`/api/backtests/${id}`),
    enabled: Boolean(id),
    refetchInterval: (query) =>
      query.state.data && shouldPollRun(query.state.data) ? RUN_POLL_INTERVAL_MS : false,
  });
}

export function useQueueBacktest() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: QueueBacktestRequest) =>
      api.post<BacktestRunResponse>('/api/backtests', body),
    // The gap refusal, the overlap conflict and the field errors are all
    // rendered by the form.
    meta: { handlesValidation: true },
    onSuccess: (run) => {
      queryClient.setQueryData(queryKeys.backtestRun(run.id), run);
      void queryClient.invalidateQueries({ queryKey: queryKeys.backtestRuns });
      void queryClient.invalidateQueries({ queryKey: queryKeys.backtestAccounts });
    },
  });
}

export function useCancelBacktestRun() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: Guid) => api.post<BacktestRunResponse>(`/api/backtests/${id}/cancel`),
    onSuccess: (run) => {
      // Cancellation is cooperative: this usually comes back still Running with
      // cancellationRequested set. The poll carries it the rest of the way.
      queryClient.setQueryData(queryKeys.backtestRun(run.id), run);
      void queryClient.invalidateQueries({ queryKey: queryKeys.backtestRuns });
    },
  });
}

export function useDeleteBacktestRun() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: Guid) => api.delete<void>(`/api/backtests/${id}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.backtestRuns });
      void queryClient.invalidateQueries({ queryKey: queryKeys.backtestAccounts });
    },
  });
}

export function useBacktestRunTrades(id: string | undefined, page: number) {
  return useQuery({
    queryKey: queryKeys.backtestRunTrades(id ?? '', page),
    queryFn: () =>
      api.get<BacktestTradeResponse[]>(`/api/backtests/${id}/trades`, {
        query: { page, pageSize: TRADES_PAGE_SIZE },
      }),
    enabled: Boolean(id),
    placeholderData: (previous) => previous,
  });
}

export function useBacktestRunEquity(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.backtestRunEquity(id ?? ''),
    queryFn: () => api.get<EquityCurve>(`/api/backtests/${id}/equity-curve`),
    enabled: Boolean(id),
  });
}

// ---------------------------------------------------------------- accounts

export function useBacktestAccounts(includeInactive = false) {
  return useQuery({
    queryKey: queryKeys.backtestAccountList(includeInactive),
    queryFn: () =>
      api.get<BacktestAccountResponse[]>('/api/backtests/accounts', { query: { includeInactive } }),
  });
}

export function useBacktestAccount(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.backtestAccount(id ?? ''),
    queryFn: () => api.get<BacktestAccountResponse>(`/api/backtests/accounts/${id}`),
    enabled: Boolean(id),
  });
}

export function useCreateBacktestAccount() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: CreateBacktestAccountRequest) =>
      api.post<BacktestAccountResponse>('/api/backtests/accounts', body),
    meta: { handlesValidation: true },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.backtestAccounts }),
  });
}

export function useUpdateBacktestAccount() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, body }: { id: Guid; body: UpdateBacktestAccountRequest }) =>
      api.put<BacktestAccountResponse>(`/api/backtests/accounts/${id}`, body),
    meta: { handlesValidation: true },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.backtestAccounts }),
  });
}

/** Refused with `account_still_holds_runs` (409) unless `confirm` is true. */
export function useDeleteBacktestAccount() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, confirm }: { id: Guid; confirm: boolean }) =>
      api.delete<void>(`/api/backtests/accounts/${id}`, { query: { confirm } }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.backtestAccounts });
      void queryClient.invalidateQueries({ queryKey: queryKeys.backtestRuns });
    },
  });
}

export function useBacktestAccountEquity(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.backtestAccountEquity(id ?? ''),
    queryFn: () => api.get<EquityCurve>(`/api/backtests/accounts/${id}/equity-curve`),
    enabled: Boolean(id),
  });
}

export function useBacktestAccountSummary(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.backtestAccountSummary(id ?? ''),
    queryFn: () => api.get<PerformanceSummary>(`/api/backtests/accounts/${id}/summary`),
    enabled: Boolean(id),
  });
}

// ---------------------------------------------------------------- strategies

export function useBacktestStrategies(includeInactive = false) {
  return useQuery({
    queryKey: queryKeys.backtestStrategyList(includeInactive),
    queryFn: () =>
      api.get<BacktestStrategyResponse[]>('/api/backtests/strategies', {
        query: { includeInactive },
      }),
  });
}

/** The only call that returns the rule tree — the list never carries it. */
export function useBacktestStrategy(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.backtestStrategy(id ?? ''),
    queryFn: () => api.get<BacktestStrategyResponse>(`/api/backtests/strategies/${id}`),
    enabled: Boolean(id),
  });
}

export function useIndicators() {
  return useQuery({
    queryKey: queryKeys.indicators,
    queryFn: () => api.get<IndicatorDescriptionResponse[]>('/api/backtests/strategies/indicators'),
    // A pure projection of a hard-coded table. It cannot change without a deploy.
    staleTime: Infinity,
  });
}

export function useSaveBacktestStrategy() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, body }: { id?: Guid; body: SaveBacktestStrategyRequest }) =>
      id
        ? api.put<BacktestStrategyResponse>(`/api/backtests/strategies/${id}`, body)
        : api.post<BacktestStrategyResponse>('/api/backtests/strategies', body),
    // The rule editor maps the JSON-path errors itself; `applyServerErrors`
    // cannot match a key like `$.indicators[0].params.period`.
    meta: { handlesValidation: true },
    onSuccess: (strategy) => {
      queryClient.setQueryData(queryKeys.backtestStrategy(strategy.id), strategy);
      void queryClient.invalidateQueries({ queryKey: queryKeys.backtestStrategies });
    },
  });
}

export function useDeleteBacktestStrategy() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: Guid) => api.delete<void>(`/api/backtests/strategies/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.backtestStrategies }),
  });
}

/**
 * Rule validation is a POST that always answers 200, and a given document's
 * verdict is immutable — so it is modelled as a query keyed on the document
 * text, not a mutation. Identical documents de-duplicate for free, which makes
 * undo back to a previously-checked shape instant.
 */
export function useRuleValidation(json: string, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.ruleValidation(json),
    queryFn: () => {
      const rule: unknown = JSON.parse(json);

      return api.post<RuleValidationResponse>('/api/backtests/strategies/validate', { rule });
    },
    enabled: enabled && json.length > 0,
    staleTime: Infinity,
    gcTime: 5 * 60_000,
    retry: false,
  });
}
