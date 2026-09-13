import { MantineProvider } from '@mantine/core';
import { Notifications } from '@mantine/notifications';
import { QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider } from 'react-router';
import { createQueryClient } from '@/api/queryClient';
import { readStoredTheme, theme } from '@/lib/theme';
import { router } from '@/routes';

const queryClient = createQueryClient();

export function App() {
  return (
    <MantineProvider theme={theme} defaultColorScheme={readStoredTheme()}>
      <QueryClientProvider client={queryClient}>
        <Notifications position="top-right" limit={4} />
        <RouterProvider router={router} />
      </QueryClientProvider>
    </MantineProvider>
  );
}
