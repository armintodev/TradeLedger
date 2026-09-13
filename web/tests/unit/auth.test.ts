import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiFetch, UNAUTHORIZED_EVENT } from '@/api/client';
import { ApiError } from '@/api/problem';
import {
  AUTH_STORAGE_KEY,
  clearAuth,
  isExpired,
  readAuth,
  readValidToken,
  writeAuth,
} from '@/auth/tokenStorage';
import { server } from './msw/server';

const BASE = 'http://localhost:5000';

function store(expiresAt: string, token = 'jwt-token') {
  writeAuth({ token, expiresAt, email: 'owner@example.com', displayName: 'Owner' });
}

describe('tokenStorage', () => {
  beforeEach(() => window.localStorage.clear());

  it('round-trips a login response', () => {
    store('2099-01-01T00:00:00Z');

    expect(readAuth()).toEqual({
      token: 'jwt-token',
      expiresAt: '2099-01-01T00:00:00Z',
      email: 'owner@example.com',
      displayName: 'Owner',
    });
  });

  it('treats an expired token as no token — there is no refresh token to fall back on', () => {
    store('2020-01-01T00:00:00Z');

    expect(isExpired(readAuth())).toBe(true);
    expect(readValidToken()).toBeNull();
  });

  it('treats a live token as usable', () => {
    store('2099-01-01T00:00:00Z');

    expect(readValidToken()).toBe('jwt-token');
  });

  it('rejects a corrupted entry rather than throwing', () => {
    window.localStorage.setItem(AUTH_STORAGE_KEY, '{not json');

    expect(readAuth()).toBeNull();
  });

  it('rejects a well-formed object missing the token', () => {
    window.localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify({ email: 'a@b.c' }));

    expect(readAuth()).toBeNull();
  });

  it('treats an unparseable expiry as expired', () => {
    store('whenever');

    expect(isExpired(readAuth())).toBe(true);
  });

  it('clears', () => {
    store('2099-01-01T00:00:00Z');
    clearAuth();

    expect(readAuth()).toBeNull();
  });
});

describe('apiFetch', () => {
  beforeEach(() => window.localStorage.clear());

  it('attaches a live bearer token', async () => {
    store('2099-01-01T00:00:00Z');

    let authorization: string | null = null;

    server.use(
      http.get(`${BASE}/api/trades/inbox`, ({ request }) => {
        authorization = request.headers.get('Authorization');
        return HttpResponse.json([]);
      }),
    );

    await apiFetch('/api/trades/inbox');

    expect(authorization).toBe('Bearer jwt-token');
  });

  it('sends no token once it has expired', async () => {
    store('2020-01-01T00:00:00Z');

    let authorization: string | null = 'unset';

    server.use(
      http.get(`${BASE}/api/trades/inbox`, ({ request }) => {
        authorization = request.headers.get('Authorization');
        return HttpResponse.json([]);
      }),
    );

    await apiFetch('/api/trades/inbox');

    expect(authorization).toBeNull();
  });

  it('clears storage and announces a 401 so the app can redirect', async () => {
    store('2099-01-01T00:00:00Z');

    const listener = vi.fn();
    window.addEventListener(UNAUTHORIZED_EVENT, listener);

    server.use(
      http.get(`${BASE}/api/trades`, () =>
        HttpResponse.json(
          { title: 'Not authenticated', code: 'not_authenticated' },
          { status: 401 },
        ),
      ),
    );

    await expect(apiFetch('/api/trades')).rejects.toBeInstanceOf(ApiError);

    expect(readAuth()).toBeNull();
    expect(listener).toHaveBeenCalledOnce();

    window.removeEventListener(UNAUTHORIZED_EVENT, listener);
  });

  it('leaves the login form to handle its own 401', async () => {
    const listener = vi.fn();
    window.addEventListener(UNAUTHORIZED_EVENT, listener);

    server.use(
      http.post(`${BASE}/api/auth/login`, () =>
        HttpResponse.json(
          { title: 'Invalid credentials', code: 'invalid_credentials' },
          { status: 401 },
        ),
      ),
    );

    await expect(
      apiFetch('/api/auth/login', {
        method: 'POST',
        body: { email: 'a@b.c', password: 'wrong' },
        skipUnauthorizedRedirect: true,
      }),
    ).rejects.toMatchObject({ status: 401, code: 'invalid_credentials' });

    expect(listener).not.toHaveBeenCalled();

    window.removeEventListener(UNAUTHORIZED_EVENT, listener);
  });

  it('drops empty query values instead of binding them as blanks', async () => {
    let url = '';

    server.use(
      http.get(`${BASE}/api/trades`, ({ request }) => {
        url = request.url;
        return HttpResponse.json({ items: [], page: 1, pageSize: 50, total: 0 });
      }),
    );

    await apiFetch('/api/trades', {
      query: { symbol: '', accountId: undefined, reviewState: null, page: 1 },
    });

    expect(url).toContain('page=1');
    expect(url).not.toContain('symbol=');
    expect(url).not.toContain('accountId=');
    expect(url).not.toContain('reviewState=');
  });

  it('resolves a 204 as undefined', async () => {
    server.use(http.delete(`${BASE}/api/proxy`, () => new HttpResponse(null, { status: 204 })));

    await expect(apiFetch('/api/proxy', { method: 'DELETE' })).resolves.toBeUndefined();
  });

  it('turns an unreachable API into a network ApiError, not a raw TypeError', async () => {
    server.use(http.get(`${BASE}/api/trades`, () => HttpResponse.error()));

    await expect(apiFetch('/api/trades')).rejects.toMatchObject({
      code: 'network_error',
      status: 0,
    });
  });
});
