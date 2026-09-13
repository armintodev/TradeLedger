import { Fragment } from 'react';
import { Badge, Button, Card, Code, Group, Loader, Stack, Table, Text, Title } from '@mantine/core';
import { IconCamera, IconHistory, IconRefresh } from '@tabler/icons-react';
import { useAccounts } from '@/api/queries/accounts';
import { useCaptureSnapshot, useSyncRuns, useSyncStatus, useTriggerSync } from '@/api/queries/sync';
import { Instant } from '@/components/Instant';
import { EmptyState, ErrorState, LoadingTable } from '@/components/States';
import { formatInteger } from '@/lib/format';
import { formatElapsed } from '@/lib/time';
import { notifyInfo, notifySuccess } from '@/lib/notify';
import type { SyncRunStatus } from '@/api/types';

const STATUS_COLORS: Record<SyncRunStatus, string> = {
  Running: 'blue',
  Succeeded: 'teal',
  Failed: 'red',
};

export function SyncTab() {
  const accounts = useAccounts();
  const status = useSyncStatus();
  const runs = useSyncRuns();
  const trigger = useTriggerSync();
  const snapshot = useCaptureSnapshot();

  const names = new Map((accounts.data ?? []).map((account) => [account.id, account.name]));
  const syncable = (accounts.data ?? []).filter((account) => account.syncMode === 'Api');
  const running = (runs.data ?? []).some((run) => run.status === 'Running');

  return (
    <Stack gap="md">
      {syncable.length === 0 && accounts.data && (
        <EmptyState
          title="No API accounts"
          description="Only an account with sync mode API and verified credentials can be synced."
        />
      )}

      {syncable.map((account) => (
        <Card key={account.id} padding="md">
          <Group justify="space-between" wrap="wrap" gap="sm">
            <Stack gap={2}>
              <Text size="sm" fw={600}>
                {account.name}
              </Text>
              <Text size="xs" c="dimmed">
                {account.credentialEnabled
                  ? `Key ${account.apiKeyHint}`
                  : 'No credentials — attach a key first'}
              </Text>
            </Stack>

            <Group gap="xs">
              <Button
                size="xs"
                variant="light"
                leftSection={<IconRefresh size={14} />}
                disabled={!account.credentialEnabled}
                loading={trigger.isPending && trigger.variables?.accountId === account.id}
                onClick={() =>
                  trigger.mutate(
                    { accountId: account.id, backfill: false },
                    {
                      onSuccess: (outcome) =>
                        outcome.ran
                          ? notifySuccess(
                              `${outcome.recordsWritten} of ${outcome.recordsSeen} record(s) written.`,
                              account.name,
                            )
                          : notifyInfo('Nothing to do — the sync was skipped.', account.name),
                    },
                  )
                }
              >
                Run
              </Button>

              <Button
                size="xs"
                variant="light"
                leftSection={<IconHistory size={14} />}
                disabled={!account.credentialEnabled}
                onClick={() =>
                  trigger.mutate(
                    { accountId: account.id, backfill: true },
                    {
                      onSuccess: (outcome) =>
                        notifySuccess(
                          `Backfill wrote ${outcome.recordsWritten} of ${outcome.recordsSeen} record(s).`,
                          account.name,
                        ),
                    },
                  )
                }
              >
                Backfill
              </Button>

              <Button
                size="xs"
                variant="light"
                leftSection={<IconCamera size={14} />}
                disabled={!account.credentialEnabled}
                loading={snapshot.isPending && snapshot.variables === account.id}
                onClick={() =>
                  snapshot.mutate(account.id, {
                    onSuccess: (captured) =>
                      notifySuccess(`Equity recorded at ${captured.equity}.`, account.name),
                  })
                }
              >
                Snapshot
              </Button>
            </Group>
          </Group>
        </Card>
      ))}

      <Card padding="md">
        <Stack gap="sm">
          <Title order={5}>Watermarks</Title>

          {status.isError && (
            <ErrorState error={status.error} onRetry={() => void status.refetch()} />
          )}
          {status.isPending && !status.data && <LoadingTable rows={3} columns={4} />}

          {status.data && status.data.length === 0 && (
            <Text size="sm" c="dimmed">
              Nothing has synced yet. The first run sets the watermarks.
            </Text>
          )}

          {status.data && status.data.length > 0 && (
            <Table.ScrollContainer minWidth={640}>
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Account</Table.Th>
                    <Table.Th>Endpoint</Table.Th>
                    <Table.Th>Last synced</Table.Th>
                    <Table.Th>Last record</Table.Th>
                    <Table.Th>Backfill</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {status.data.map((cursor) => (
                    <Table.Tr key={`${cursor.accountId}-${cursor.endpoint}`}>
                      <Table.Td>{names.get(cursor.accountId) ?? cursor.accountId}</Table.Td>
                      <Table.Td>
                        <Code>{cursor.endpoint}</Code>
                      </Table.Td>
                      <Table.Td>
                        <Instant value={cursor.lastSyncedAt} />
                      </Table.Td>
                      <Table.Td>
                        <Instant value={cursor.lastRecordAt} />
                      </Table.Td>
                      <Table.Td>
                        <Badge
                          size="sm"
                          variant="light"
                          color={cursor.backfillComplete ? 'teal' : 'orange'}
                        >
                          {cursor.backfillComplete ? 'Complete' : 'Incomplete'}
                        </Badge>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          )}
        </Stack>
      </Card>

      <Card padding="md">
        <Stack gap="sm">
          <Group justify="space-between">
            <Title order={5}>Recent runs</Title>
            {/* Polling runs only while something is actually running — there is no
                WebSocket track yet, and a fixed interval would poll all day. */}
            {running && (
              <Group gap={6}>
                <Loader size="xs" />
                <Text size="xs" c="dimmed">
                  Refreshing every 3s while a run is active
                </Text>
              </Group>
            )}
          </Group>

          {runs.isError && <ErrorState error={runs.error} onRetry={() => void runs.refetch()} />}
          {runs.isPending && !runs.data && <LoadingTable rows={4} columns={6} />}

          {runs.data && runs.data.length === 0 && (
            <Text size="sm" c="dimmed">
              No runs recorded yet.
            </Text>
          )}

          {runs.data && runs.data.length > 0 && (
            <Table.ScrollContainer minWidth={840}>
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Started</Table.Th>
                    <Table.Th>Account</Table.Th>
                    <Table.Th>Endpoint</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th ta="right">Seen</Table.Th>
                    <Table.Th ta="right">Written</Table.Th>
                    <Table.Th ta="right">Requests</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {runs.data.map((run) => (
                    <Fragment key={run.id}>
                      <Table.Tr>
                        <Table.Td>
                          <Instant value={run.startedAt} seconds />
                        </Table.Td>
                        <Table.Td>{names.get(run.accountId) ?? run.accountId}</Table.Td>
                        <Table.Td>
                          <Group gap={4} wrap="nowrap">
                            <Code>{run.endpoint}</Code>
                            {run.isBackfill && (
                              <Badge size="xs" variant="light">
                                backfill
                              </Badge>
                            )}
                          </Group>
                        </Table.Td>
                        <Table.Td>
                          <Group gap={6} wrap="nowrap">
                            <Badge size="sm" variant="light" color={STATUS_COLORS[run.status]}>
                              {run.status}
                            </Badge>
                            {run.status === 'Running' && (
                              <Text size="xs" c="dimmed">
                                {formatElapsed(run.startedAt)}
                              </Text>
                            )}
                          </Group>
                        </Table.Td>
                        <Table.Td ta="right">{formatInteger(run.recordsSeen)}</Table.Td>
                        <Table.Td ta="right">{formatInteger(run.recordsWritten)}</Table.Td>
                        <Table.Td ta="right">{formatInteger(run.requestsMade)}</Table.Td>
                      </Table.Tr>

                      {/* Shown in full, never truncated: the tail of an exchange
                          error is usually the part that says what went wrong. */}
                      {run.error && (
                        <Table.Tr>
                          <Table.Td colSpan={7}>
                            <Text
                              size="xs"
                              c="red"
                              ff="monospace"
                              style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}
                            >
                              {run.error}
                            </Text>
                          </Table.Td>
                        </Table.Tr>
                      )}
                    </Fragment>
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          )}
        </Stack>
      </Card>
    </Stack>
  );
}
