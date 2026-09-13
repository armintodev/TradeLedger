import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../client';
import { queryKeys } from '../queryKeys';
import { dayEndIso, dayStartIso } from '@/lib/time';
import type { TradeFilters } from '@/lib/filters';
import type {
  CreateManualTradeRequest,
  Guid,
  JournalTradeRequest,
  PagedResult,
  TradeDetailResponse,
  TradeListItem,
} from '../types';

export function useTrades(filters: TradeFilters) {
  return useQuery({
    queryKey: queryKeys.tradeList(filters),
    queryFn: () =>
      api.get<PagedResult<TradeListItem>>('/api/trades', {
        query: {
          accountId: filters.accountId,
          symbol: filters.symbol,
          reviewState: filters.reviewState,
          marketSession: filters.marketSession,
          from: dayStartIso(filters.from),
          to: dayEndIso(filters.to),
          page: filters.page,
          pageSize: filters.pageSize,
        },
      }),
    // Keeps the previous page on screen while the next one loads instead of
    // collapsing the table to a skeleton on every page change.
    placeholderData: (previous) => previous,
  });
}

export function useTrade(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.trade(id ?? ''),
    queryFn: () => api.get<TradeDetailResponse>(`/api/trades/${id}`),
    enabled: Boolean(id),
  });
}

/**
 * The review queue. Loaded once and held in memory for the session: a refetch
 * mid-review would reorder the list under the user's cursor.
 */
export function useInbox(options: { enabled?: boolean; refetchOnMount?: boolean | 'always' } = {}) {
  return useQuery({
    queryKey: queryKeys.inbox,
    queryFn: () => api.get<TradeListItem[]>('/api/trades/inbox'),
    enabled: options.enabled ?? true,
    refetchOnMount: options.refetchOnMount,
  });
}

export function useJournalTrade() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, body }: { id: Guid; body: JournalTradeRequest }) =>
      api.patch<TradeDetailResponse>(`/api/trades/${id}/journal`, body),
    meta: { handlesValidation: true },
    onSuccess: (trade) => {
      queryClient.setQueryData(queryKeys.trade(trade.id), trade);
      void queryClient.invalidateQueries({ queryKey: queryKeys.trades });
      void queryClient.invalidateQueries({ queryKey: queryKeys.inbox });
      void queryClient.invalidateQueries({ queryKey: queryKeys.analytics });
    },
  });
}

export function useCreateManualTrade() {
  const queryClient = useQueryClient();

  return useMutation({
    // 201 Created returns the list projection, not the full detail.
    mutationFn: (body: CreateManualTradeRequest) => api.post<TradeListItem>('/api/trades', body),
    meta: { handlesValidation: true },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.trades });
      void queryClient.invalidateQueries({ queryKey: queryKeys.analytics });
    },
  });
}

/** Only ever offered for `origin === "Manual"`; the domain refuses synced rows. */
export function useDeleteTrade() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: Guid) => api.delete<void>(`/api/trades/${id}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.trades });
      void queryClient.invalidateQueries({ queryKey: queryKeys.analytics });
    },
  });
}
