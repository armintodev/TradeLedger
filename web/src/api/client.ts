import { ApiError, networkError, toApiError } from './problem';
import { clearAuth, readValidToken } from '@/auth/tokenStorage';

const BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000').replace(/\/+$/, '');

/**
 * Raised when a 401 arrives from anything other than the login form. The
 * AuthProvider listens for it and sends the user to `/login?next=…`; doing the
 * navigation here would need router context inside a plain module.
 */
export const UNAUTHORIZED_EVENT = 'tradeledger:unauthorized';

export type QueryValue = string | number | boolean | Date | null | undefined;

export interface ApiFetchInit extends Omit<RequestInit, 'body'> {
  query?: Record<string, QueryValue | QueryValue[]>;
  /** Serialised as JSON with the right content type. Use `rawBody` for anything else. */
  body?: unknown;
  rawBody?: BodyInit | null;
  /** The login form handles its own 401; everything else triggers the redirect. */
  skipUnauthorizedRedirect?: boolean;
}

export function buildQuery(query: Record<string, QueryValue | QueryValue[]> | undefined): string {
  if (!query) {
    return '';
  }

  const params = new URLSearchParams();

  for (const [key, value] of Object.entries(query)) {
    for (const item of Array.isArray(value) ? value : [value]) {
      // Dropped, not sent empty: the API's optional query parameters are
      // nullable and an empty string would bind as a value rather than absence.
      if (item === undefined || item === null || item === '') {
        continue;
      }

      params.append(key, item instanceof Date ? item.toISOString() : String(item));
    }
  }

  const serialised = params.toString();
  return serialised ? `?${serialised}` : '';
}

export async function apiFetch<T>(path: string, init: ApiFetchInit = {}): Promise<T> {
  const { query, body, rawBody, skipUnauthorizedRedirect, headers, ...rest } = init;

  const requestHeaders = new Headers(headers);
  requestHeaders.set('Accept', 'application/json');

  const token = readValidToken();

  if (token) {
    requestHeaders.set('Authorization', `Bearer ${token}`);
  }

  let payload: BodyInit | null | undefined = rawBody;

  if (body !== undefined) {
    requestHeaders.set('Content-Type', 'application/json');
    payload = JSON.stringify(body);
  }

  let response: Response;

  try {
    response = await fetch(`${BASE_URL}${path}${buildQuery(query)}`, {
      ...rest,
      headers: requestHeaders,
      body: payload,
    });
  } catch (cause) {
    // A CORS rejection is indistinguishable from the API being down: the
    // browser refuses to say which, and nothing is logged server-side.
    throw networkError(cause);
  }

  if (response.status === 401 && !skipUnauthorizedRedirect) {
    clearAuth();
    window.dispatchEvent(new CustomEvent(UNAUTHORIZED_EVENT));
  }

  if (!response.ok) {
    throw toApiError(response.status, await readBody(response));
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const parsed = await readBody(response);

  return parsed as T;
}

export const api = {
  get: <T>(path: string, init?: ApiFetchInit) => apiFetch<T>(path, { ...init, method: 'GET' }),
  post: <T>(path: string, body?: unknown, init?: ApiFetchInit) =>
    apiFetch<T>(path, { ...init, method: 'POST', body }),
  put: <T>(path: string, body?: unknown, init?: ApiFetchInit) =>
    apiFetch<T>(path, { ...init, method: 'PUT', body }),
  patch: <T>(path: string, body?: unknown, init?: ApiFetchInit) =>
    apiFetch<T>(path, { ...init, method: 'PATCH', body }),
  delete: <T>(path: string, init?: ApiFetchInit) =>
    apiFetch<T>(path, { ...init, method: 'DELETE' }),
};

async function readBody(response: Response): Promise<unknown> {
  const text = await response.text();

  if (!text) {
    return undefined;
  }

  try {
    return JSON.parse(text);
  } catch {
    // Not JSON: something in front of the API, or a bare 500 page.
    return { detail: text.slice(0, 500) };
  }
}

export { ApiError };
