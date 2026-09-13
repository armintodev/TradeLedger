import {
  Alert,
  Badge,
  Button,
  Card,
  Group,
  Progress,
  Stack,
  Table,
  Text,
  Title,
} from '@mantine/core';
import { IconInfoCircle, IconX } from '@tabler/icons-react';
import { forgetBackfill, useBackfillJobs, useCancelBackfill } from '@/api/queries/marketData';
import { JobStatusBadge } from '@/components/Badges';
import { Instant } from '@/components/Instant';
import { formatInteger, formatPercent } from '@/lib/format';
import { INTERVAL_LABELS } from '@/lib/marketData';
import { notifySuccess } from '@/lib/notify';
import { ApiError } from '@/api/problem';

export interface BackfillJobListProps {
  jobIds: string[];
  onForget: (ids: string[]) => void;
  /** Ids this session has asked to cancel, since the response does not say. */
  cancelling: string[];
  onCancelRequested: (id: string) => void;
}

/**
 * Backfill history, such as it is.
 *
 * There is no `GET /api/market-data/backfill`, so a job is only reachable by an
 * id this browser kept. Each is fetched separately and polls only while it is
 * live. See `docs/backtest-impl.md` §2.
 */
export function BackfillJobList({
  jobIds,
  onForget,
  cancelling,
  onCancelRequested,
}: BackfillJobListProps) {
  const { jobs, missing } = useBackfillJobs(jobIds);
  const cancel = useCancelBackfill();

  if (jobIds.length === 0) {
    return null;
  }

  return (
    <Card padding="md">
      <Stack gap="sm">
        <Group justify="space-between" align="flex-start" wrap="wrap" gap="sm">
          <Stack gap={2}>
            <Title order={5}>Recent backfills</Title>
            <Text size="xs" c="dimmed">
              Kept in this browser only — the API has no endpoint that lists jobs.
            </Text>
          </Stack>

          <Button size="compact-xs" variant="subtle" onClick={() => onForget([])}>
            Clear list
          </Button>
        </Group>

        {missing > 0 && (
          <Alert color="gray" variant="light" icon={<IconInfoCircle size={16} />}>
            {missing} remembered job{missing === 1 ? '' : 's'} could not be loaded and may have been
            removed server-side.
          </Alert>
        )}

        <Table.ScrollContainer minWidth={780}>
          <Table>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Symbol</Table.Th>
                <Table.Th>Range</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th>Progress</Table.Th>
                <Table.Th ta="right">Candles</Table.Th>
                <Table.Th ta="right">Funding</Table.Th>
                <Table.Th />
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {jobs.map((job) => {
                const live = job.status === 'Queued' || job.status === 'Running';
                const asked = cancelling.includes(job.id);

                return (
                  <Table.Tr key={job.id}>
                    <Table.Td>
                      <Text size="sm" fw={600}>
                        {job.symbol}
                      </Text>
                      <Text size="xs" c="dimmed">
                        {INTERVAL_LABELS[job.interval]}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Group gap={4} wrap="nowrap">
                        <Instant value={job.from} dateOnly size="xs" />
                        <Text size="xs" c="dimmed">
                          →
                        </Text>
                        <Instant value={job.to} dateOnly size="xs" />
                      </Group>
                    </Table.Td>
                    <Table.Td>
                      <Group gap={4} wrap="nowrap">
                        <JobStatusBadge status={job.status} />
                        {/* The response carries no cancellationRequested, and a
                            queued job that was cancelled never transitions —
                            so this has to come from local memory. */}
                        {asked && live && (
                          <Badge size="sm" color="orange" variant="light">
                            {job.status === 'Queued' ? 'Will not start' : 'Cancelling…'}
                          </Badge>
                        )}
                      </Group>
                      {job.error && (
                        <Text size="xs" c="red" style={{ whiteSpace: 'pre-wrap' }}>
                          {job.error}
                        </Text>
                      )}
                    </Table.Td>
                    <Table.Td>
                      {live ? (
                        <Stack gap={2} w={110}>
                          <Progress value={job.progressPercent} size="sm" />
                          <Text size="xs" c="dimmed">
                            {formatPercent(job.progressPercent, 0)}
                          </Text>
                        </Stack>
                      ) : (
                        <Text size="xs" c="dimmed">
                          <Instant value={job.finishedAt} size="xs" />
                        </Text>
                      )}
                    </Table.Td>
                    <Table.Td ta="right">{formatInteger(job.candlesWritten)}</Table.Td>
                    <Table.Td ta="right">{formatInteger(job.fundingRatesWritten)}</Table.Td>
                    <Table.Td>
                      <Group gap={4} justify="flex-end" wrap="nowrap">
                        {live && !asked && (
                          <Button
                            size="compact-xs"
                            variant="subtle"
                            color="orange"
                            loading={cancel.isPending && cancel.variables === job.id}
                            onClick={() =>
                              cancel.mutate(job.id, {
                                onSuccess: () => {
                                  onCancelRequested(job.id);
                                  notifySuccess('Cancellation requested.');
                                },
                                onError: (error) => {
                                  // Already finished between render and click.
                                  if (
                                    error instanceof ApiError &&
                                    error.code === 'job_already_finished'
                                  ) {
                                    onCancelRequested(job.id);
                                  }
                                },
                              })
                            }
                          >
                            Cancel
                          </Button>
                        )}
                        {!live && (
                          <Button
                            size="compact-xs"
                            variant="subtle"
                            color="gray"
                            leftSection={<IconX size={12} />}
                            onClick={() => onForget(forgetBackfill(job.id))}
                          >
                            Forget
                          </Button>
                        )}
                      </Group>
                    </Table.Td>
                  </Table.Tr>
                );
              })}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      </Stack>
    </Card>
  );
}
