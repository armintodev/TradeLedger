import {
  Button,
  Card,
  Group,
  Pagination,
  Progress,
  Select,
  Stack,
  Table,
  Text,
} from '@mantine/core';
import { IconFilterOff, IconPlayerPlay } from '@tabler/icons-react';
import { useNavigate, useSearchParams } from 'react-router';
import { useBacktestAccounts, useBacktestRuns } from '@/api/queries/backtests';
import { DataQualityBadge, RunStatusBadge } from '@/components/Badges';
import { Instant } from '@/components/Instant';
import { Money, Pnl } from '@/components/Money';
import { EmptyState, ErrorState, LoadingTable } from '@/components/States';
import { formatPercent } from '@/lib/format';
import {
  DEFAULT_RUN_PAGE_SIZE,
  hasRunFilters,
  readRunFilters,
  writeRunFilters,
} from '@/lib/filters';
import { INTERVAL_LABELS } from '@/lib/marketData';
import type { BacktestRunFilters } from '@/api/queryKeys';

const STATUSES = ['Queued', 'Running', 'Succeeded', 'Failed', 'Cancelled'];

export function RunsTab() {
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();

  const filters = readRunFilters(searchParams);
  const runs = useBacktestRuns(filters);
  const accounts = useBacktestAccounts(true);

  const names = new Map((accounts.data ?? []).map((account) => [account.id, account.name]));

  function update(patch: Partial<BacktestRunFilters>, keepPage = false) {
    const next: BacktestRunFilters = {
      ...filters,
      ...patch,
      page: keepPage ? (patch.page ?? filters.page) : 1,
    };

    setSearchParams(writeRunFilters(next, searchParams), { replace: true });
  }

  const items = runs.data?.items ?? [];
  const total = runs.data?.total ?? 0;
  const pageCount = Math.max(1, Math.ceil(total / filters.pageSize));

  return (
    <Stack gap="md">
      <Group justify="space-between" align="flex-end" wrap="wrap" gap="sm">
        <Group gap="sm" align="flex-end" wrap="wrap">
          <Select
            label="Account"
            placeholder="Any"
            data={(accounts.data ?? []).map((account) => ({
              value: account.id,
              label: account.name,
            }))}
            value={filters.accountId ?? null}
            onChange={(value) => update({ accountId: value ?? undefined })}
            clearable
            w={200}
          />

          <Select
            label="Status"
            placeholder="Any"
            data={STATUSES}
            value={filters.status ?? null}
            onChange={(value) => update({ status: value ?? undefined })}
            clearable
            w={150}
          />

          {hasRunFilters(filters) && (
            <Button
              variant="subtle"
              leftSection={<IconFilterOff size={16} />}
              onClick={() =>
                setSearchParams(
                  writeRunFilters({ page: 1, pageSize: DEFAULT_RUN_PAGE_SIZE }, searchParams),
                  { replace: true },
                )
              }
            >
              Clear
            </Button>
          )}
        </Group>

        <Button
          leftSection={<IconPlayerPlay size={16} />}
          onClick={() => void navigate('/backtests/runs/new')}
        >
          Queue a run
        </Button>
      </Group>

      {runs.isError && <ErrorState error={runs.error} onRetry={() => void runs.refetch()} />}

      {runs.isPending && !runs.data && <LoadingTable rows={5} columns={7} />}

      {runs.data && items.length === 0 && (
        <EmptyState
          title={hasRunFilters(filters) ? 'No runs match these filters' : 'No runs yet'}
          description={
            hasRunFilters(filters)
              ? 'Clear the filters to see every run on every account.'
              : 'A run needs an account, a strategy and candle data for the range. Check the Market data tab if you are not sure what is stored.'
          }
          action={
            <Button variant="light" onClick={() => void navigate('/backtests/runs/new')}>
              Queue the first run
            </Button>
          }
        />
      )}

      {items.length > 0 && (
        <Card padding={0}>
          <Table.ScrollContainer minWidth={980}>
            <Table highlightOnHover>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Queued</Table.Th>
                  <Table.Th>Symbol</Table.Th>
                  <Table.Th>Account</Table.Th>
                  <Table.Th>Range</Table.Th>
                  <Table.Th>Status</Table.Th>
                  <Table.Th ta="right">Opening</Table.Th>
                  <Table.Th ta="right">Net</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {items.map((run) => {
                  const net =
                    run.closingBalance === null ? null : run.closingBalance - run.openingBalance;

                  return (
                    <Table.Tr
                      key={run.id}
                      style={{ cursor: 'pointer' }}
                      onClick={() => void navigate(`/backtests/runs/${run.id}`)}
                    >
                      <Table.Td>
                        <Instant value={run.queuedAt} />
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm" fw={600}>
                          {run.symbol ?? '—'}
                        </Text>
                        <Text size="xs" c="dimmed">
                          {run.interval ? INTERVAL_LABELS[run.interval] : ''}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{names.get(run.backtestAccountId) ?? '—'}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Group gap={4} wrap="nowrap">
                          <Instant value={run.from} dateOnly size="xs" />
                          <Text size="xs" c="dimmed">
                            →
                          </Text>
                          <Instant value={run.to} dateOnly size="xs" />
                        </Group>
                      </Table.Td>
                      <Table.Td>
                        <Stack gap={4}>
                          <Group gap={4} wrap="nowrap">
                            <RunStatusBadge
                              status={run.status}
                              cancellationRequested={run.cancellationRequested}
                            />
                            <DataQualityBadge quality={run.dataQuality} />
                          </Group>

                          {run.status === 'Running' && (
                            <Group gap={6} wrap="nowrap">
                              <Progress value={run.progressPercent} size="xs" w={70} />
                              <Text size="xs" c="dimmed">
                                {formatPercent(run.progressPercent, 0)}
                              </Text>
                            </Group>
                          )}
                        </Stack>
                      </Table.Td>
                      <Table.Td ta="right">
                        <Money value={run.openingBalance} />
                      </Table.Td>
                      <Table.Td ta="right">
                        <Pnl value={net} />
                      </Table.Td>
                    </Table.Tr>
                  );
                })}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Card>
      )}

      {pageCount > 1 && (
        <Group justify="space-between" wrap="wrap">
          <Text size="sm" c="dimmed">
            {total.toLocaleString('en-US')} run{total === 1 ? '' : 's'}
          </Text>
          <Pagination
            value={filters.page}
            onChange={(page) => update({ page }, true)}
            total={pageCount}
            size="sm"
          />
        </Group>
      )}
    </Stack>
  );
}
