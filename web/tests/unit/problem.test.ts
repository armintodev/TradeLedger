import { describe, expect, it } from 'vitest';
import { ApiError, networkError, toApiError } from '@/api/problem';

/**
 * One case per row of the error table in `web/SPEC.md`, taken from
 * `GlobalExceptionHandler.cs` and `DomainExceptions.cs`.
 */
describe('toApiError', () => {
  it('maps a validation failure — 400, not 422 — with its field errors', () => {
    const error = toApiError(400, {
      title: 'One or more fields are invalid',
      status: 400,
      code: 'validation_failed',
      traceId: '0HN1',
      errors: { Rating: ['Rating must be between 1 and 5.'] },
    });

    expect(error.status).toBe(400);
    expect(error.code).toBe('validation_failed');
    expect(error.isValidation).toBe(true);
    expect(error.errors?.Rating).toEqual(['Rating must be between 1 and 5.']);
  });

  it('does not call a 400 without field errors a validation failure', () => {
    expect(toApiError(400, { code: 'malformed_request' }).isValidation).toBe(false);
  });

  it('maps a broken business rule to 422', () => {
    const error = toApiError(422, {
      title: 'The request breaks a rule of the journal',
      code: 'trade_still_open',
      detail: 'An open trade cannot be reviewed.',
    });

    expect(error.isDomainRule).toBe(true);
    // The message is written for the trader, so it is shown verbatim.
    expect(error.message).toBe('An open trade cannot be reviewed.');
  });

  it.each([
    ['not_found', 404],
    ['duplicate_record', 409],
    ['concurrent_update', 409],
    ['missing_reference', 400],
    ['proxy_required', 503],
    ['internal_error', 500],
    ['request_cancelled', 499],
    ['engine_unavailable', 501],
    ['not_authenticated', 401],
  ])('carries the %s code through untouched', (code, status) => {
    expect(toApiError(status, { code }).code).toBe(code);
  });

  it('handles the login 401, which shapes its own problem', () => {
    const error = toApiError(401, {
      title: 'Invalid credentials',
      status: 401,
      code: 'invalid_credentials',
      detail: 'That email and password combination was not recognised.',
    });

    expect(error.status).toBe(401);
    expect(error.code).toBe('invalid_credentials');
  });

  it('survives an empty body from something in front of the API', () => {
    const error = toApiError(502, undefined);

    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(502);
    expect(error.title).toBe('Request failed (502)');
  });

  it('infers a code when the body carries none', () => {
    expect(toApiError(404, {}).code).toBe('not_found');
    expect(toApiError(418, {}).code).toBe('http_418');
  });
});

describe('networkError', () => {
  it('is distinguishable from anything the server sent', () => {
    const error = networkError(new TypeError('Failed to fetch'));

    expect(error.isNetwork).toBe(true);
    expect(error.status).toBe(0);
    expect(error.title).toBe('Could not reach the API');
  });
});
