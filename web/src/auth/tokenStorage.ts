import type { LoginResponse } from '@/api/types';

export const AUTH_STORAGE_KEY = 'tradeledger.auth';

export interface StoredAuth {
  token: string;
  /** ISO 8601, straight from `LoginResponse.expiresAt`. */
  expiresAt: string;
  email: string;
  displayName: string | null;
}

/**
 * The JWT lives in localStorage. It survives a refresh, which matters most on a
 * phone, and the XSS risk is bounded by this being a single-user tool that
 * renders no untrusted content and loads no third-party scripts. See
 * `web/SPEC.md` — Constraints.
 */
export function readAuth(): StoredAuth | null {
  let raw: string | null;

  try {
    raw = window.localStorage.getItem(AUTH_STORAGE_KEY);
  } catch {
    // Private mode, or storage blocked by policy. Treat it as signed out.
    return null;
  }

  if (!raw) {
    return null;
  }

  try {
    const parsed = JSON.parse(raw) as Partial<StoredAuth>;

    if (
      typeof parsed?.token !== 'string' ||
      typeof parsed?.expiresAt !== 'string' ||
      typeof parsed?.email !== 'string'
    ) {
      return null;
    }

    return {
      token: parsed.token,
      expiresAt: parsed.expiresAt,
      email: parsed.email,
      displayName: parsed.displayName ?? null,
    };
  } catch {
    return null;
  }
}

export function writeAuth(login: LoginResponse): StoredAuth {
  const stored: StoredAuth = {
    token: login.token,
    expiresAt: login.expiresAt,
    email: login.email,
    displayName: login.displayName,
  };

  try {
    window.localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(stored));
  } catch {
    // Nothing to do: the session still works until the tab is closed.
  }

  return stored;
}

export function clearAuth(): void {
  try {
    window.localStorage.removeItem(AUTH_STORAGE_KEY);
  } catch {
    // Ignore.
  }
}

/**
 * There is no refresh token — `JwtTokenService` issues a 12h bearer and that is
 * the whole session. An expired token is treated as absent so the guard can
 * redirect before a request is sent rather than after a 401 comes back.
 */
export function isExpired(auth: StoredAuth | null, now: Date = new Date()): boolean {
  if (!auth) {
    return true;
  }

  const expiresAt = Date.parse(auth.expiresAt);

  if (Number.isNaN(expiresAt)) {
    return true;
  }

  return expiresAt <= now.getTime();
}

/** The token to send, or null when there is nothing usable stored. */
export function readValidToken(now: Date = new Date()): string | null {
  const auth = readAuth();
  return auth && !isExpired(auth, now) ? auth.token : null;
}
