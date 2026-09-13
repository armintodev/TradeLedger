import type { ReactNode } from 'react';
import { MantineProvider } from '@mantine/core';
import { Notifications } from '@mantine/notifications';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { theme } from '@/lib/theme';

/** Retries off: a test asserting on an error should not wait for backoff. */
export function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0, staleTime: 0 },
      mutations: { retry: false },
    },
  });
}

export function renderWithProviders(ui: ReactNode, client = createTestQueryClient()) {
  return {
    client,
    ...render(
      <MantineProvider theme={theme} defaultColorScheme="light">
        <QueryClientProvider client={client}>
          <Notifications />
          {ui}
        </QueryClientProvider>
      </MantineProvider>,
    ),
  };
}
