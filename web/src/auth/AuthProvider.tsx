import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useLocation, useNavigate } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';
import { UNAUTHORIZED_EVENT } from '@/api/client';
import type { LoginResponse } from '@/api/types';
import { AuthContext, type AuthContextValue } from './AuthContext';
import { clearAuth, isExpired, readAuth, writeAuth, type StoredAuth } from './tokenStorage';

export function AuthProvider({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  const location = useLocation();
  const queryClient = useQueryClient();

  const [auth, setAuth] = useState<StoredAuth | null>(() => {
    const stored = readAuth();

    // An expired token is the same as none: clear it now rather than letting
    // the first request 401 and bounce the user mid-render.
    if (isExpired(stored)) {
      clearAuth();
      return null;
    }

    return stored;
  });

  const signIn = useCallback((login: LoginResponse) => {
    setAuth(writeAuth(login));
  }, []);

  const signOut = useCallback(() => {
    clearAuth();
    setAuth(null);
    // Another user's data must never be served from this cache.
    queryClient.clear();
  }, [queryClient]);

  // Any 401 from any query or mutation lands here. `apiFetch` has already
  // cleared storage by the time the event fires.
  useEffect(() => {
    function onUnauthorized() {
      setAuth(null);
      queryClient.clear();

      const next = `${location.pathname}${location.search}`;

      void navigate(
        next === '/login' || next.startsWith('/login?')
          ? '/login'
          : `/login?next=${encodeURIComponent(next)}`,
        { replace: true },
      );
    }

    window.addEventListener(UNAUTHORIZED_EVENT, onUnauthorized);

    return () => window.removeEventListener(UNAUTHORIZED_EVENT, onUnauthorized);
  }, [navigate, location.pathname, location.search, queryClient]);

  const value = useMemo<AuthContextValue>(
    () => ({
      auth,
      isAuthenticated: auth !== null && !isExpired(auth),
      signIn,
      signOut,
    }),
    [auth, signIn, signOut],
  );

  return <AuthContext value={value}>{children}</AuthContext>;
}
