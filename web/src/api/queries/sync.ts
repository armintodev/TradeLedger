import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../client';
import { queryKeys } from '../queryKeys';
import type {
  BalanceSnapshotResponse,
  Guid,
  SyncOutcome,
  SyncRunResponse,
  SyncStatusResponse,
} from '../types';

export const SYNC_POLL_INTERVAL_MS = 3000;

export function useSyncStatus() {
  return useQuery({
    queryKey: queryKeys.syncStatus,
    queryFn: () => api.get<SyncStatusResponse[]>('/api/sync/status'),
  });
}

/**
 * Polls only while something is actually running. There is no WebSocket track
 * in the backend yet, so this is the whole liveness story — and a fixed
 * interval would mean constant background traffic for a tool that is idle most
 * of the time.
 */
export function useSyncRuns() {
  return useQuery({
    queryKey: queryKeys.syncRuns,
    queryFn: () => api.get<SyncRunResponse[]>('/api/sync/runs'),
    refetchInterval: (query) => {
      const runs = query.state.data;

      return runs?.some((run) => run.status === 'Running') ? SYNC_POLL_INTERVAL_MS : false;
    },
  });
}

export function useTriggerSync() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ accountId, backfill }: { accountId: Guid; backfill: boolean }) =>
      api.post<SyncOutcome>(`/api/sync/accounts/${accountId}/${backfill ? 'backfill' : 'run'}`),
    onSettled: () => invalidateAfterSync(queryClient),
  });
}

export function useCaptureSnapshot() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (accountId: Guid) =>
      api.post<BalanceSnapshotResponse>(`/api/sync/accounts/${accountId}/snapshot`),
    onSettled: () => invalidateAfterSync(queryClient),
  });
}

function invalidateAfterSync(queryClient: ReturnType<typeof useQueryClient>) {
  void queryClient.invalidateQueries({ queryKey: queryKeys.sync });
  void queryClient.invalidateQueries({ queryKey: queryKeys.trades });
  void queryClient.invalidateQueries({ queryKey: queryKeys.inbox });
  void queryClient.invalidateQueries({ queryKey: queryKeys.analytics });
}
