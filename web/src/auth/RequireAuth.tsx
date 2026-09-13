import { Navigate, Outlet, useLocation } from 'react-router';
import { useAuth } from './useAuth';

/**
 * Every route except /login sits behind this. No token, or one whose
 * `expiresAt` has passed, goes to the login form with the attempted location in
 * `next` so the user lands where they meant to.
 */
export function RequireAuth() {
  const { isAuthenticated } = useAuth();
  const location = useLocation();

  if (!isAuthenticated) {
    const next = `${location.pathname}${location.search}`;

    return (
      <Navigate to={next === '/' ? '/login' : `/login?next=${encodeURIComponent(next)}`} replace />
    );
  }

  return <Outlet />;
}
