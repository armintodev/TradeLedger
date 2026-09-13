import type { ReactNode } from 'react';
import {
  Alert,
  Button,
  Card,
  Center,
  Code,
  Group,
  Skeleton,
  Stack,
  Table,
  Text,
  Title,
} from '@mantine/core';
import { IconAlertTriangle, IconInbox, IconRefresh } from '@tabler/icons-react';
import { ApiError } from '@/api/problem';

/**
 * Every list view has three of these: a skeleton shaped like the final layout,
 * an explicit empty state naming the action that would produce data, and an
 * error state with a retry. A query that fails must never leave a spinner
 * turning forever.
 */

interface EmptyStateProps {
  title: string;
  description?: string;
  action?: ReactNode;
  icon?: ReactNode;
}

export function EmptyState({ title, description, action, icon }: EmptyStateProps) {
  return (
    <Card padding="xl">
      <Center>
        <Stack align="center" gap="xs" maw={460}>
          {icon ?? <IconInbox size={36} opacity={0.5} />}
          <Title order={4} ta="center">
            {title}
          </Title>
          {description && (
            <Text c="dimmed" size="sm" ta="center">
              {description}
            </Text>
          )}
          {action && <Group mt="sm">{action}</Group>}
        </Stack>
      </Center>
    </Card>
  );
}

interface ErrorStateProps {
  error: unknown;
  onRetry?: () => void;
  title?: string;
}

export function ErrorState({ error, onRetry, title }: ErrorStateProps) {
  const apiError = error instanceof ApiError ? error : null;

  return (
    <Alert
      color={apiError?.isNetwork ? 'orange' : 'red'}
      icon={<IconAlertTriangle size={18} />}
      title={title ?? apiError?.title ?? 'Something went wrong'}
    >
      <Stack gap="xs" align="flex-start">
        <Text size="sm">
          {apiError?.detail ??
            (error instanceof Error ? error.message : 'The request did not complete.')}
        </Text>

        {apiError?.errors && (
          <Stack gap={2}>
            {Object.entries(apiError.errors).map(([field, messages]) => (
              <Text key={field} size="xs">
                <Text component="span" fw={600}>
                  {field}
                </Text>
                : {messages.join(' ')}
              </Text>
            ))}
          </Stack>
        )}

        {apiError?.traceId && (
          <Text size="xs" c="dimmed">
            Trace <Code>{apiError.traceId}</Code>
          </Text>
        )}

        {onRetry && (
          <Button
            size="xs"
            variant="light"
            leftSection={<IconRefresh size={14} />}
            onClick={onRetry}
          >
            Retry
          </Button>
        )}
      </Stack>
    </Alert>
  );
}

/** A skeleton shaped like the table it replaces, so the layout does not jump. */
export function LoadingTable({ rows = 6, columns = 5 }: { rows?: number; columns?: number }) {
  return (
    <Table>
      <Table.Tbody>
        {Array.from({ length: rows }).map((_, rowIndex) => (
          <Table.Tr key={rowIndex}>
            {Array.from({ length: columns }).map((__, columnIndex) => (
              <Table.Td key={columnIndex}>
                <Skeleton height={14} radius="sm" />
              </Table.Td>
            ))}
          </Table.Tr>
        ))}
      </Table.Tbody>
    </Table>
  );
}

export function LoadingCards({ count = 4, height = 96 }: { count?: number; height?: number }) {
  return (
    <Group grow align="stretch">
      {Array.from({ length: count }).map((_, index) => (
        <Skeleton key={index} height={height} radius="md" />
      ))}
    </Group>
  );
}
