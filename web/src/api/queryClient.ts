import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query';
import { ApiError } from './problem';
import { notifyError } from '@/lib/notify';

/**
 * Which failures are worth trying again. A 4xx will not fix itself, so only a
 * network failure or a server error gets a retry.
 */
function shouldRetry(failureCount: number, error: unknown): boolean {
  if (failureCount >= 2) {
    return false;
  }

  if (!(error instanceof ApiError)) {
    return false;
  }

  return error.isNetwork || error.status >= 500;
}

export function createQueryClient(): QueryClient {
  const client: QueryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: shouldRetry,
        retryDelay: (attempt) => Math.min(1000 * 2 ** attempt, 8000),
        staleTime: 30_000,
        refetchOnWindowFocus: false,
      },
      mutations: {
        retry: false,
      },
    },

    queryCache: new QueryCache({
      onError: (error) => {
        // A failed query renders its own error state with a retry control, so a
        // toast on top of that is duplication. The exception is losing the API
        // entirely: cached data stays on screen and nothing else would say why
        // it stopped updating.
        if (error instanceof ApiError && error.isNetwork) {
          notifyError(error);
        }
      },
    }),

    mutationCache: new MutationCache({
      onError: (error, _variables, _context, mutation) => {
        // A form that maps `errors` onto its own inputs sets this. Everything
        // else gets the toast.
        const handlesValidation = mutation.options.meta?.handlesValidation === true;

        notifyError(error, { handlesValidation });

        if (error instanceof ApiError && error.code === 'missing_reference') {
          // A taxonomy term was retired, or an account removed, while the form
          // was open. Refresh the pickers so the retry can succeed.
          void client.invalidateQueries({ queryKey: ['taxonomy'] });
          void client.invalidateQueries({ queryKey: ['accounts'] });
        }

        if (error instanceof ApiError && error.code === 'concurrent_update') {
          const invalidates = mutation.options.meta?.invalidateOnConflict;

          if (Array.isArray(invalidates)) {
            for (const key of invalidates) {
              void client.invalidateQueries({ queryKey: key as unknown[] });
            }
          }
        }
      },
    }),
  });

  return client;
}

declare module '@tanstack/react-query' {
  interface Register {
    defaultError: ApiError;
    mutationMeta: {
      /** The form renders field errors itself; do not toast a 400. */
      handlesValidation?: boolean;
      /** Query keys to refetch when the server reports a concurrent update. */
      invalidateOnConflict?: readonly unknown[][];
    };
  }
}
