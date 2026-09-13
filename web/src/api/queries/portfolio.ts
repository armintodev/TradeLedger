import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../client';
import { queryKeys } from '../queryKeys';
import type {
  BalanceSnapshotResponse,
  CloseHoldingRequest,
  CreateHoldingRequest,
  CreateSnapshotRequest,
  CreateTransferRequest,
  Guid,
  HoldingResponse,
  RepriceHoldingRequest,
  TransferResponse,
} from '../types';

export function useHoldings(includeClosed = false) {
  return useQuery({
    queryKey: queryKeys.holdings(includeClosed),
    queryFn: () => api.get<HoldingResponse[]>('/api/holdings', { query: { includeClosed } }),
  });
}

export function useCreateHolding() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: CreateHoldingRequest) => api.post<HoldingResponse>('/api/holdings', body),
    meta: { handlesValidation: true },
    onSuccess: () => invalidatePortfolio(queryClient),
  });
}

export function useRepriceHolding() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, body }: { id: Guid; body: RepriceHoldingRequest }) =>
      api.post<HoldingResponse>(`/api/holdings/${id}/reprice`, body),
    meta: { handlesValidation: true },
    onSuccess: () => invalidatePortfolio(queryClient),
  });
}

export function useCloseHolding() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, body }: { id: Guid; body: CloseHoldingRequest }) =>
      api.post<HoldingResponse>(`/api/holdings/${id}/close`, body),
    meta: { handlesValidation: true },
    onSuccess: () => invalidatePortfolio(queryClient),
  });
}

export function useTransfers() {
  return useQuery({
    queryKey: queryKeys.transfers,
    queryFn: () => api.get<TransferResponse[]>('/api/transfers'),
  });
}

export function useCreateTransfer() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: CreateTransferRequest) => api.post<TransferResponse>('/api/transfers', body),
    meta: { handlesValidation: true },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.transfers });
      void queryClient.invalidateQueries({ queryKey: queryKeys.analytics });
    },
  });
}

/**
 * A manual equity point. There is no list endpoint for snapshots — the equity
 * curve on the dashboard is the only view of them.
 */
export function useCreateSnapshot() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: CreateSnapshotRequest) =>
      api.post<BalanceSnapshotResponse>('/api/snapshots', body),
    meta: { handlesValidation: true },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.analytics }),
  });
}

function invalidatePortfolio(queryClient: ReturnType<typeof useQueryClient>) {
  void queryClient.invalidateQueries({ queryKey: queryKeys.holdingsRoot });
  void queryClient.invalidateQueries({ queryKey: queryKeys.analytics });
}
