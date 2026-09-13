import { useMutation, useQuery } from '@tanstack/react-query';
import { api, apiFetch } from '../client';
import { queryKeys } from '../queryKeys';
import type { LoginRequest, LoginResponse, MeResponse } from '../types';

/**
 * The one request that must not trigger the global 401 redirect — a bad
 * password here means "show the inline error", not "bounce to the login page
 * you are already on".
 */
export function useLogin() {
  return useMutation<LoginResponse, Error, LoginRequest>({
    mutationFn: (body) =>
      apiFetch<LoginResponse>('/api/auth/login', {
        method: 'POST',
        body,
        skipUnauthorizedRedirect: true,
      }),
    meta: { handlesValidation: true },
  });
}

export function useMe(enabled = true) {
  return useQuery({
    queryKey: queryKeys.me,
    queryFn: () => api.get<MeResponse>('/api/auth/me'),
    enabled,
    // The profile is server-configured and read-only this cycle; it will not
    // change under the user.
    staleTime: 10 * 60 * 1000,
  });
}
