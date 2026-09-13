/**
 * Every failure from the API is a `ProblemDetails` shaped by
 * `src/TradeLedger.Api/Shared/Errors/ApiProblem.cs`: a stable machine-readable
 * `code`, a `traceId`, and a field-keyed `errors` object for validation.
 */

/** The subset of RFC 7807 the API actually sends, plus its extensions. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  code?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

/**
 * The codes `GlobalExceptionHandler` and `DomainExceptions` can produce.
 * `network_error` is the client's own: the request never reached the API.
 */
export type ApiErrorCode =
  | 'validation_failed'
  | 'malformed_request'
  | 'invalid_argument'
  | 'missing_reference'
  | 'not_authenticated'
  | 'invalid_credentials'
  | 'not_found'
  | 'duplicate_record'
  | 'concurrent_update'
  | 'request_cancelled'
  | 'engine_unavailable'
  | 'proxy_required'
  | 'internal_error'
  | 'network_error'
  | (string & {});

export class ApiError extends Error {
  readonly status: number;
  readonly code: ApiErrorCode;
  readonly title: string;
  readonly detail?: string;
  readonly traceId?: string;
  readonly errors?: Record<string, string[]>;

  constructor(init: {
    status: number;
    code: ApiErrorCode;
    title: string;
    detail?: string;
    traceId?: string;
    errors?: Record<string, string[]>;
  }) {
    super(init.detail ?? init.title);
    this.name = 'ApiError';
    this.status = init.status;
    this.code = init.code;
    this.title = init.title;
    this.detail = init.detail;
    this.traceId = init.traceId;
    this.errors = init.errors;
  }

  /** A 400 carrying field errors, which forms map onto their inputs. */
  get isValidation(): boolean {
    return this.code === 'validation_failed' && this.errors !== undefined;
  }

  /** A broken business rule. Its message is written for the user; show it verbatim. */
  get isDomainRule(): boolean {
    return this.status === 422;
  }

  /** The request never reached the API. */
  get isNetwork(): boolean {
    return this.code === 'network_error';
  }
}

/** Statuses with no title of their own worth showing. */
const FALLBACK_TITLES: Record<number, string> = {
  400: 'The request was rejected',
  401: 'Not authenticated',
  403: 'Not allowed',
  404: 'Not found',
  409: 'Conflict',
  422: 'That breaks a rule of the journal',
  500: 'Something went wrong',
  503: 'Unavailable',
};

/**
 * Build an `ApiError` from a response and whatever body came with it.
 *
 * Anything can arrive here: a well-shaped `ProblemDetails`, an empty body from
 * a proxy, or HTML from something in front of the API. The result is always a
 * renderable error rather than a parse failure.
 */
export function toApiError(status: number, body: unknown): ApiError {
  const problem = isProblemDetails(body) ? body : {};

  return new ApiError({
    status,
    code: problem.code ?? inferCode(status),
    title: problem.title ?? FALLBACK_TITLES[status] ?? `Request failed (${status})`,
    detail: problem.detail,
    traceId: problem.traceId,
    errors: problem.errors,
  });
}

/** The request never got a response: DNS, connection refused, CORS, offline. */
export function networkError(cause?: unknown): ApiError {
  return new ApiError({
    status: 0,
    code: 'network_error',
    title: 'Could not reach the API',
    detail:
      cause instanceof Error && cause.message
        ? cause.message
        : 'The request did not reach the server. Check that the API is running.',
  });
}

function isProblemDetails(body: unknown): body is ProblemDetails {
  return typeof body === 'object' && body !== null;
}

function inferCode(status: number): ApiErrorCode {
  switch (status) {
    case 400:
      return 'invalid_argument';
    case 401:
      return 'not_authenticated';
    case 404:
      return 'not_found';
    case 409:
      return 'duplicate_record';
    case 503:
      return 'proxy_required';
    case 500:
      return 'internal_error';
    default:
      return `http_${status}`;
  }
}
