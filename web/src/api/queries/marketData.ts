import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, apiFetch } from '../client';
import { queryKeys, type GapQuery } from '../queryKeys';
import type {
  BackfillJobResponse,
  BackfillRequest,
  CandleGapResponse,
  CandleImportResponse,
  DeleteCandlesResponse,
  Guid,
  MarketDataCoverageResponse,
  MarketDataJobStatus,
} from '../types';

export const BACKFILL_POLL_INTERVAL_MS = 3000;

/**
 * Kestrel's default request body limit is ~30 MB and nothing in the API raises
 * it, so the configured `MarketData:MaxImportBytes` of 50 MB is unreachable for
 * its top 40 % — a file in between fails as `malformed_request` before the
 * endpoint's own check runs. Guard below Kestrel's limit, not at the configured
 * one. See `docs/backtest-impl.md` §9.
 */
export const MAX_IMPORT_BYTES = 28 * 1024 * 1024;

/** The whole matrix in one call — the endpoint takes no filters and no paging. */
export function useCoverage() {
  return useQuery({
    queryKey: queryKeys.coverage,
    queryFn: () => api.get<MarketDataCoverageResponse[]>('/api/market-data/coverage'),
  });
}

/**
 * The gap probe. All five parameters are required, so this stays disabled until
 * the caller has a complete query — which is what `isCompleteRange` decides.
 */
export function useGaps(query: GapQuery | null) {
  return useQuery({
    queryKey: queryKeys.gaps(query),
    queryFn: () =>
      api.get<CandleGapResponse[]>('/api/market-data/gaps', {
        query: query as unknown as Record<string, string>,
      }),
    enabled: query !== null,
    // A gap set only changes when candles are written, and every writer in this
    // app invalidates the key.
    staleTime: 60_000,
  });
}

export function useQueueBackfill() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: BackfillRequest) =>
      api.post<BackfillJobResponse>('/api/market-data/backfill', body),
    meta: { handlesValidation: true },
    onSuccess: (job) => {
      rememberBackfill(job.id);
      queryClient.setQueryData(queryKeys.backfillJob(job.id), job);
    },
  });
}

export function shouldPollJob(job: BackfillJobResponse | undefined): boolean {
  return job !== undefined && (job.status === 'Queued' || job.status === 'Running');
}

export function useBackfillJob(id: string | null) {
  return useQuery({
    queryKey: queryKeys.backfillJob(id ?? ''),
    queryFn: () => api.get<BackfillJobResponse>(`/api/market-data/backfill/${id}`),
    enabled: Boolean(id),
    refetchInterval: (query) =>
      shouldPollJob(query.state.data) ? BACKFILL_POLL_INTERVAL_MS : false,
  });
}

/**
 * There is no list endpoint, so history is whatever ids this browser kept. Each
 * is fetched on its own and polls only while it is live.
 */
export function useBackfillJobs(ids: string[]) {
  return useQueries({
    queries: ids.map((id) => ({
      queryKey: queryKeys.backfillJob(id),
      queryFn: () => api.get<BackfillJobResponse>(`/api/market-data/backfill/${id}`),
      refetchInterval: (query: { state: { data: BackfillJobResponse | undefined } }) =>
        shouldPollJob(query.state.data) ? BACKFILL_POLL_INTERVAL_MS : (false as const),
      // A job that has been pruned server-side should drop out quietly rather
      // than filling the panel with 404s.
      retry: false,
    })),
    combine: (results) => ({
      jobs: results
        .map((result) => result.data)
        .filter((job): job is BackfillJobResponse => job !== undefined),
      isPending: results.some((result) => result.isPending),
      missing: results.filter((result) => result.isError).length,
    }),
  });
}

export function useCancelBackfill() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: Guid) =>
      api.post<BackfillJobResponse>(`/api/market-data/backfill/${id}/cancel`),
    onSuccess: (job) => queryClient.setQueryData(queryKeys.backfillJob(job.id), job),
  });
}

export interface ImportCandlesVariables {
  file: File;
  source: string;
  symbol: string;
  interval: string;
}

/**
 * Multipart, with the field names the endpoint binds: `file`, `source`,
 * `symbol`, `interval`. The content type is deliberately not set — the browser
 * has to add its own multipart boundary.
 */
export function useImportCandles() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ file, source, symbol, interval }: ImportCandlesVariables) => {
      const body = new FormData();

      body.append('file', file);
      body.append('source', source);
      body.append('symbol', symbol);
      body.append('interval', interval);

      return apiFetch<CandleImportResponse>('/api/market-data/import', {
        method: 'POST',
        rawBody: body,
      });
    },
    meta: { handlesValidation: true },
    onSuccess: () => invalidateCandles(queryClient),
  });
}

export function useDeleteCandles() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (query: GapQuery) =>
      api.delete<DeleteCandlesResponse>('/api/market-data/candles', {
        query: query as unknown as Record<string, string>,
      }),
    onSuccess: () => invalidateCandles(queryClient),
  });
}

function invalidateCandles(queryClient: ReturnType<typeof useQueryClient>) {
  void queryClient.invalidateQueries({ queryKey: queryKeys.marketData });
}

// ---------------------------------------------------------------- job history

const BACKFILL_STORAGE_KEY = 'tradeledger.backfills';
const MAX_REMEMBERED = 20;

/**
 * The API has no `GET /api/market-data/backfill`, so a job is reachable only by
 * the id its 202 returned. Keeping them here is what stops backfill history
 * dying on reload. Per-browser, and the UI says so. See
 * `docs/backtest-impl.md` §2.
 */
export function readRememberedBackfills(): string[] {
  try {
    const raw = window.localStorage.getItem(BACKFILL_STORAGE_KEY);

    if (!raw) {
      return [];
    }

    const parsed: unknown = JSON.parse(raw);

    return Array.isArray(parsed) ? parsed.filter((id): id is string => typeof id === 'string') : [];
  } catch {
    return [];
  }
}

export function rememberBackfill(id: string): string[] {
  const next = [id, ...readRememberedBackfills().filter((known) => known !== id)].slice(
    0,
    MAX_REMEMBERED,
  );

  try {
    window.localStorage.setItem(BACKFILL_STORAGE_KEY, JSON.stringify(next));
  } catch {
    // Storage blocked — the job is still pollable for this page's lifetime.
  }

  return next;
}

export function forgetBackfill(id: string): string[] {
  const next = readRememberedBackfills().filter((known) => known !== id);

  try {
    window.localStorage.setItem(BACKFILL_STORAGE_KEY, JSON.stringify(next));
  } catch {
    // Ignore.
  }

  return next;
}

export const TERMINAL_JOB_STATUSES: MarketDataJobStatus[] = ['Succeeded', 'Failed', 'Cancelled'];
