import {
  Badge,
  Button,
  Card,
  Group,
  Pagination,
  Select,
  SimpleGrid,
  Stack,
  Table,
  Text,
} from '@mantine/core';
import { IconArrowLeft } from '@tabler/icons-react';
import { useNavigate, useParams, useSearchParams } from 'react-router';
import { useBacktestRun, useBacktestRunTrades } from '@/api/queries/backtests';
import {
  CycleBadge,
  DataQualityBadge,
  ExitReasonBadge,
  OutcomeBadge,
  ResolutionBadge,
  RunStatusBadge,
  SideBadge,
} from '@/components/Badges';
import { Duration } from '@/components/Duration';
import { Instant } from '@/components/Instant';
import { Money, Pnl, Price, Quantity, RMultiple } from '@/components/Money';
import { PageHeader } from '@/components/PageHeader';
import { StatTile } from '@/components/StatTile';
import { EmptyState, ErrorState, LoadingTable } from '@/components/States';
import { formatInteger, formatPercent } from '@/lib/format';
import { INTERVAL_LABELS } from '@/lib/marketData';
import {
  MAX_POSITION_PAGE_SIZE,
  readRunPositionFilters,
  writeRunPositionFilters,
  type RunPositionFilters,
} from '@/lib/filters';
import { PositionDetailDrawer } from './PositionDetailDrawer';

const PAGE_SIZES = ['50', '100', '250', String(MAX_POSITION_PAGE_SIZE)];

/**
 * Every position a run opened and closed, in the engine's own sequence, with the
 * timestamps and levels each one was decided on. The run detail page embeds the
 * same ledger; this is where it gets room to be read, and where a single position
 * can be opened and linked to.
 */
export function RunPositionsPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();

  const filters = readRunPositionFilters(searchParams);
  const run = useBacktestRun(id);
  const trades = useBacktestRunTrades(id, filters.page, filters.pageSize);

  function update(patch: Partial<RunPositionFilters>) {
    setSearchParams(writeRunPositionFilters({ ...filters, ...patch }), { replace: true });
  }

  const items = trades.data?.items ?? [];
  const total = trades.data?.total ?? 0;
  const pageCount = Math.max(1, Math.ceil(total / filters.pageSize));
  const performance = run.data?.result?.performance ?? null;

  return (
    <Stack gap="md">
      <PageHeader
        title="Positions"
        description={
          trades.data
            ? `${formatInteger(total)} position${total === 1 ? '' : 's'} opened and closed during this run.`
            : 'Every position the engine opened and closed during this run.'
        }
        actions={
          <Button
            variant="subtle"
            leftSection={<IconArrowLeft size={16} />}
            onClick={() => void navigate(`/backtests/runs/${id}`)}
          >
            Back to run
          </Button>
        }
      />

      {run.data && (
        <Card padding="sm">
          <Group gap="xs" wrap="wrap">
            <Text fw={600}>{run.data.symbol}</Text>
            <Badge variant="light">
              {run.data.interval ? INTERVAL_LABELS[run.data.interval] : ''}
            </Badge>
            <RunStatusBadge
              status={run.data.status}
              cancellationRequested={run.data.cancellationRequested}
            />
            <DataQualityBadge quality={run.data.dataQuality} />
            <Text size="sm" c="dimmed">
              <Instant value={run.data.from} dateOnly /> → <Instant value={run.data.to} dateOnly />
            </Text>
          </Group>
        </Card>
      )}

      {performance && (
        // Run-wide figures, from the run's own summary rather than from the page
        // on screen — a page of 50 rows out of 300 would add up to a lie.
        <SimpleGrid cols={{ base: 2, sm: 3, lg: 6 }} spacing="sm">
          <StatTile label="Positions" value={formatInteger(performance.totalTrades)} />
          <StatTile
            label="Won"
            value={formatInteger(performance.winningTrades)}
            sub={formatPercent(performance.winRate)}
          />
          <StatTile label="Lost" value={formatInteger(performance.losingTrades)} />
          <StatTile
            label="Net"
            value={<Pnl value={performance.netProfitLoss} size="xl" fw={600} />}
          />
          <StatTile
            label="Fees"
            value={<Money value={performance.totalFees} size="xl" fw={600} />}
          />
          <StatTile
            label="Funding"
            value={<Money value={performance.totalFunding} size="xl" fw={600} />}
          />
        </SimpleGrid>
      )}

      {trades.isError && <ErrorState error={trades.error} onRetry={() => void trades.refetch()} />}

      {trades.isPending && !trades.data && <LoadingTable rows={10} columns={9} />}

      {trades.data && items.length === 0 && (
        <EmptyState
          title={filters.page > 1 ? 'Nothing on this page' : 'No positions'}
          description={
            filters.page > 1
              ? 'That page is past the end of the run. Go back to the first page.'
              : 'The rules never fired over this range, or every signal was skipped. The engine counters on the run page say which.'
          }
          action={
            filters.page > 1 ? (
              <Button variant="light" onClick={() => update({ page: 1 })}>
                First page
              </Button>
            ) : (
              <Button variant="light" onClick={() => void navigate(`/backtests/runs/${id}`)}>
                Back to run
              </Button>
            )
          }
        />
      )}

      {items.length > 0 && (
        <Card padding={0}>
          <Table.ScrollContainer minWidth={1400}>
            <Table highlightOnHover>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>#</Table.Th>
                  <Table.Th>Opened</Table.Th>
                  <Table.Th>Closed</Table.Th>
                  <Table.Th>Held</Table.Th>
                  <Table.Th>Side</Table.Th>
                  <Table.Th>Cycle</Table.Th>
                  <Table.Th ta="right">Entry</Table.Th>
                  <Table.Th ta="right">Exit</Table.Th>
                  <Table.Th ta="right">Stop</Table.Th>
                  <Table.Th ta="right">Target</Table.Th>
                  <Table.Th ta="right">Qty</Table.Th>
                  <Table.Th ta="right">Gross</Table.Th>
                  <Table.Th ta="right">Fees</Table.Th>
                  <Table.Th ta="right">Net</Table.Th>
                  <Table.Th ta="right">R</Table.Th>
                  <Table.Th>Outcome</Table.Th>
                  <Table.Th>Exit</Table.Th>
                  <Table.Th>Resolution</Table.Th>
                  <Table.Th ta="right">Balance</Table.Th>
                </Table.Tr>
              </Table.Thead>

              <Table.Tbody>
                {items.map((trade) => (
                  <Table.Tr
                    key={trade.id}
                    style={{ cursor: 'pointer' }}
                    onClick={() => update({ position: trade.id })}
                  >
                    <Table.Td>
                      <Text size="xs" c="dimmed">
                        {trade.sequence}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Instant value={trade.openedAt} seconds />
                    </Table.Td>
                    <Table.Td>
                      <Instant value={trade.closedAt} seconds />
                    </Table.Td>
                    <Table.Td>
                      <Duration value={trade.duration} />
                    </Table.Td>
                    <Table.Td>
                      <SideBadge side={trade.side} />
                    </Table.Td>
                    <Table.Td>
                      <CycleBadge adx={trade.cycleAdx} interval={trade.cycleInterval} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Price value={trade.entryPrice} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Price value={trade.exitPrice} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Price value={trade.stopLossPrice} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Price value={trade.takeProfitPrice} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Quantity value={trade.quantity} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Pnl value={trade.grossProfitLoss} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Money value={trade.fees} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Pnl value={trade.netProfitLoss} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <RMultiple value={trade.achievedReturnR} />
                    </Table.Td>
                    <Table.Td>
                      <OutcomeBadge outcome={trade.outcome} />
                    </Table.Td>
                    <Table.Td>
                      <ExitReasonBadge reason={trade.exitReason} />
                    </Table.Td>
                    <Table.Td>
                      <ResolutionBadge resolution={trade.intrabarResolution} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Money value={trade.balanceAfter} />
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Card>
      )}

      {items.length > 0 && (
        <Group justify="space-between" wrap="wrap" gap="sm">
          <Group gap="xs">
            <Text size="sm" c="dimmed">
              Per page
            </Text>
            <Select
              data={PAGE_SIZES}
              value={String(filters.pageSize)}
              onChange={(value) => update({ pageSize: Number(value ?? filters.pageSize), page: 1 })}
              w={90}
              size="xs"
              allowDeselect={false}
            />
          </Group>

          {pageCount > 1 && (
            <Pagination
              value={filters.page}
              onChange={(page) => update({ page })}
              total={pageCount}
              size="sm"
            />
          )}
        </Group>
      )}

      {id && (
        <PositionDetailDrawer
          runId={id}
          tradeId={filters.position ?? null}
          onClose={() => update({ position: undefined })}
        />
      )}
    </Stack>
  );
}
