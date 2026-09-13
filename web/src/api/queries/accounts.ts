import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../client';
import { queryKeys } from '../queryKeys';
import type {
  AccountResponse,
  CreateAccountRequest,
  CreatedAccountResponse,
  CredentialResponse,
  Guid,
  SetCredentialRequest,
} from '../types';

export function useAccounts() {
  return useQuery({
    queryKey: queryKeys.accounts,
    queryFn: () => api.get<AccountResponse[]>('/api/accounts'),
    staleTime: 5 * 60 * 1000,
  });
}

export function useCreateAccount() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: CreateAccountRequest) =>
      api.post<CreatedAccountResponse>('/api/accounts', body),
    meta: { handlesValidation: true },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.accounts }),
  });
}

/**
 * Setting a credential calls Bitunix to verify the key, so it goes out through
 * the egress proxy. With no proxy resolved it fails with `proxy_required` (503)
 * before a socket is opened — configure the proxy first or the key is verified
 * from the wrong address. `CLAUDE.md` §4.
 */
export function useSetCredentials() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, body }: { id: Guid; body: SetCredentialRequest }) =>
      api.put<CredentialResponse>(`/api/accounts/${id}/credentials`, body),
    meta: { handlesValidation: true },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.accounts }),
  });
}

export function useRemoveCredentials() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: Guid) => api.delete<void>(`/api/accounts/${id}/credentials`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.accounts }),
  });
}
