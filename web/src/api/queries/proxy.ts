import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../client';
import { queryKeys } from '../queryKeys';
import type { ProxyResponse, ProxyUpdatedResponse, SetProxyRequest } from '../types';

/**
 * The per-user egress proxy. The password is AES-GCM encrypted at rest and is
 * never returned by any endpoint — `hasPassword` is all the client ever learns
 * about it, and the field on the form is write-only.
 */
export function useProxy() {
  return useQuery({
    queryKey: queryKeys.proxy,
    queryFn: () => api.get<ProxyResponse>('/api/proxy'),
  });
}

export function useSetProxy() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: SetProxyRequest) => api.put<ProxyUpdatedResponse>('/api/proxy', body),
    meta: { handlesValidation: true },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.proxy });
      void queryClient.invalidateQueries({ queryKey: queryKeys.accounts });
    },
  });
}

export function useDeleteProxy() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => api.delete<void>('/api/proxy'),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.proxy });
      void queryClient.invalidateQueries({ queryKey: queryKeys.accounts });
    },
  });
}
