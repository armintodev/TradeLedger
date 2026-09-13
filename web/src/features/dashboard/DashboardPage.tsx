import { lazy, Suspense } from 'react';
import {
  Button,
  Card,
  Grid,
  Group,
  Skeleton,
  SimpleGrid,
  Stack,
  Table,
  Text,
  Title,
} from '@mantine/core';
import { useNavigate, useSearchParams } from 'react-router';
import { useEquityCurve, useSummary } from '@/api/queries/analytics';
import { useAccounts } from '@/api/queries/accounts';
import { useTrades } from '@/api/queries/trades';
import { OutcomeBadge, SessionChips, SideBadge } from '@/components/Badges';
import { Duration } from '@/components/Duration';
import { RangeControl } from '@/components/Filters';
import { Instant } from '@/components/Instant';
import { Pnl, Price, RMultiple } from '@/components/Money';
import { PageHeader } from '@/components/PageHeader';
import { StatTile } from '@/components/StatTile';
import { EmptyState, ErrorState, LoadingCards } from '@/components/States';
import { formatInteger, formatMoney, formatPercent, formatRatio } from '@/lib/format';
import { readAnalyticsFilters, writeAnalyticsFilters, type AnalyticsFilters } from '@/lib/filters';
import type { TradeListItem } from '@/api/types';

const EquityChart = lazy(() => import('@/components/EquityChart'));
import { useTimeZone } from '@/lib/timeZone';
import { formatDuration, formatInstant } from '@/lib/time';

export function DashboardPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  const timeZone = useTimeZone();

  const filters = readAnalyticsFilters(searchParams);

  const accounts = useAccounts();
  const summary = useSummary(filters);
  const equity = useEquityCurve(filters);

  // The API has no "open positions" endpoint; open trades come back in the
  // journal like any other row and are separated here.
  const recent = useTrades({ accountId: filters.accountId, page: 1, pageSize: 50 });

  function update(next: AnalyticsFilters) {
    setSearchParams(writeAnalyticsFilters(next), { replace: true });
  }

  const open = (recent.data?.items ?? []).filter((trade) => trade.outcome === 'Open');
  const latest = (recent.data?.items ?? [])
    .filter((trade) => trade.outcome !== 'Open')
    .slice(0, 10);

  const noAccounts = accounts.data?.length === 0;

  return (
    <Stack gap="md">
      <PageHeader title="Dashboard" description="Equity, performance and what is still open." />

      <Card padding="sm">
        <RangeControl filters={filters} onChange={update} />
      </Card>

      {noAccounts && (
        <EmptyState
          title="No accounts yet"
          description="TradeLedger pulls trades from an account. Add one, attach its read-only API credentials, and the journal fills itself."
          action={
            <Button variant="light" onClick={() => void navigate('/settings')}>
              Set up an account
            </Button>
          }
        />
      )}

      <Equity filters={filters} query={equity} timeZone={timeZone} />

      {summary.isError && (
        <ErrorState error={summary.error} onRetry={() => void summary.refetch()} />
      )}

      {summary.isPending && !summary.data && <LoadingCards count={4} />}

      {summary.data && (
        <>
          <SimpleGrid cols={{ base: 2, sm: 3, lg: 6 }} spacing="sm">
            <StatTile
              label="Net PnL"
              value={<Pnl value={summary.data.netProfitLoss} size="xl" />}
            />
            <StatTile label="Win rate" value={formatPercent(summary.data.winRate)} />
            <StatTile
              label="Trades"
              value={formatInteger(summary.data.totalTrades)}
              sub={`${summary.data.winningTrades}W · ${summary.data.losingTrades}L · ${summary.data.breakevenTrades}BE`}
            />
            <StatTile
              label="Profit factor"
              value={formatRatio(summary.data.profitFactor)}
              hint="Gross win divided by gross loss. Null — and shown as a dash — when there are no losing trades, because the ratio is undefined rather than infinite."
            />
            <StatTile
              label="Expectancy"
              value={<Pnl value={summary.data.expectancy} size="xl" />}
              hint="Average net PnL per closed trade."
            />
            <StatTile label="Open positions" value={formatInteger(summary.data.openPositions)} />

            <StatTile label="Average win" value={formatMoney(summary.data.averageWin)} />
            <StatTile label="Average loss" value={formatMoney(summary.data.averageLoss)} />
            <StatTile
              label="Average R"
              value={<RMultiple value={summary.data.averageAchievedR} size="xl" />}
              sub={
                summary.data.averagePlannedR !== null
                  ? `planned ${summary.data.averagePlannedR.toFixed(2)}R`
                  : undefined
              }
            />
            <StatTile
              label="Longest win streak"
              value={formatInteger(summary.data.longestWinStreak)}
            />
            <StatTile
              label="Longest loss streak"
              value={formatInteger(summary.data.longestLossStreak)}
            />
            <StatTile
              label="Average duration"
              value={formatDuration(summary.data.averageDuration)}
            />
          </SimpleGrid>

          <Card padding="md">
            <Stack gap="sm">
              <Group justify="space-between">
                <Title order={5}>Planned vs unplanned</Title>
                <Text size="xs" c="dimmed">
                  Whether a pre-trade plan existed when the position opened
                </Text>
              </Group>

              <SimpleGrid cols={{ base: 1, sm: 2 }} spacing="sm">
                <PlanSplit
                  label="Planned"
                  count={summary.data.plannedTradeCount}
                  net={summary.data.plannedNetProfitLoss}
                />
                <PlanSplit
                  label="Unplanned"
                  count={summary.data.unplannedTradeCount}
                  net={summary.data.unplannedNetProfitLoss}
                />
              </SimpleGrid>
            </Stack>
          </Card>
        </>
      )}

      <Grid gap="md">
        <Grid.Col span={{ base: 12, lg: 6 }}>
          <TradeStrip
            title="Open positions"
            emptyMessage="Nothing is open right now."
            trades={open}
            query={recent}
            showOutcome={false}
          />
        </Grid.Col>

        <Grid.Col span={{ base: 12, lg: 6 }}>
          <TradeStrip
            title="Recent trades"
            emptyMessage="No closed trades in this range."
            trades={latest}
            query={recent}
            showOutcome
          />
        </Grid.Col>
      </Grid>
    </Stack>
  );
}

function PlanSplit({ label, count, net }: { label: string; count: number; net: number }) {
  return (
    <Card padding="sm">
      <Group justify="space-between" align="flex-start">
        <Stack gap={2}>
          <Text size="sm" fw={600}>
            {label}
          </Text>
          <Text size="xs" c="dimmed">
            {formatInteger(count)} trade{count === 1 ? '' : 's'}
          </Text>
        </Stack>
        <Pnl value={net} size="lg" />
      </Group>
    </Card>
  );
}

function Equity({
  filters,
  query,
  timeZone,
}: {
  filters: AnalyticsFilters;
  query: ReturnType<typeof useEquityCurve>;
  timeZone: string;
}) {
  const navigate = useNavigate();

  if (query.isError) {
    return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;
  }

  if (query.isPending && !query.data) {
    return <LoadingCards count={1} height={280} />;
  }

  const curve = query.data;

  if (!curve || curve.points.length === 0) {
    return (
      <EmptyState
        title="No equity curve yet"
        description="The curve is drawn from balance snapshots, never by summing trades. Capture one from Settings → Sync, or record it by hand under Portfolio."
        action={
          <Button variant="light" onClick={() => void navigate('/portfolio')}>
            Record a snapshot
          </Button>
        }
      />
    );
  }

  const data = curve.points.map((point) => ({
    at: formatInstant(point.at, { timeZone, dateOnly: true }),
    equity: point.equity,
  }));

  return (
    <Card padding="md">
      <Stack gap="md">
        <Group justify="space-between" align="flex-start" wrap="wrap" gap="sm">
          <Stack gap={2}>
            <Title order={5}>Equity</Title>
            <Text size="xs" c="dimmed">
              {curve.points.length} snapshot{curve.points.length === 1 ? '' : 's'}
              {filters.accountId ? '' : ' across all accounts'}
            </Text>
          </Stack>

          <Group gap="lg" wrap="wrap">
            <Figure label="Current" value={formatMoney(curve.currentEquity)} />
            <Figure label="Peak" value={formatMoney(curve.peakEquity)} />
            <Figure
              label="Max drawdown"
              value={formatMoney(curve.maxDrawdown)}
              sub={`${formatPercent(curve.maxDrawdownPercent)}${
                curve.maxDrawdownAt
                  ? ` · ${formatInstant(curve.maxDrawdownAt, { timeZone, dateOnly: true })}`
                  : ''
              }`}
              negative={curve.maxDrawdown > 0}
            />
            <Figure
              label="Current drawdown"
              value={formatMoney(curve.currentDrawdown)}
              sub={formatPercent(curve.currentDrawdownPercent)}
              negative={curve.currentDrawdown > 0}
            />
          </Group>
        </Group>

        <Suspense fallback={<Skeleton height={260} radius="md" />}>
          <EquityChart data={data} />
        </Suspense>
      </Stack>
    </Card>
  );
}

function Figure({
  label,
  value,
  sub,
  negative,
}: {
  label: string;
  value: string;
  sub?: string;
  negative?: boolean;
}) {
  return (
    <Stack gap={0}>
      <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
        {label}
      </Text>
      <Text size="md" fw={600} ff="monospace" c={negative ? 'red' : undefined}>
        {value}
      </Text>
      {sub && (
        <Text size="xs" c="dimmed">
          {sub}
        </Text>
      )}
    </Stack>
  );
}

function TradeStrip({
  title,
  trades,
  query,
  emptyMessage,
  showOutcome,
}: {
  title: string;
  trades: TradeListItem[];
  query: ReturnType<typeof useTrades>;
  emptyMessage: string;
  showOutcome: boolean;
}) {
  const navigate = useNavigate();

  return (
    <Card padding="md" h="100%">
      <Stack gap="sm">
        <Title order={5}>{title}</Title>

        {query.isError && <ErrorState error={query.error} onRetry={() => void query.refetch()} />}

        {query.isPending && !query.data && <LoadingCards count={1} height={120} />}

        {query.data && trades.length === 0 && (
          <Text size="sm" c="dimmed">
            {emptyMessage}
          </Text>
        )}

        {trades.length > 0 && (
          <Table.ScrollContainer minWidth={480}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Opened</Table.Th>
                  <Table.Th>Symbol</Table.Th>
                  <Table.Th>Side</Table.Th>
                  {showOutcome ? (
                    <Table.Th ta="right">Net PnL</Table.Th>
                  ) : (
                    <Table.Th ta="right">Entry</Table.Th>
                  )}
                  <Table.Th>{showOutcome ? 'Outcome' : 'Session'}</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {trades.map((trade) => (
                  <Table.Tr
                    key={trade.id}
                    style={{ cursor: 'pointer' }}
                    onClick={() => void navigate(`/trades/${trade.id}`)}
                  >
                    <Table.Td>
                      <Instant value={trade.openedAt} />
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm" fw={600}>
                        {trade.symbol}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <SideBadge side={trade.side} />
                    </Table.Td>
                    <Table.Td ta="right">
                      {showOutcome ? (
                        <Pnl value={trade.netProfitLoss} />
                      ) : (
                        <Price value={trade.entryPrice} />
                      )}
                    </Table.Td>
                    <Table.Td>
                      {showOutcome ? (
                        <Group gap="xs" wrap="nowrap">
                          <OutcomeBadge outcome={trade.outcome} />
                          <Duration value={trade.duration} />
                        </Group>
                      ) : (
                        <SessionChips value={trade.marketSession} />
                      )}
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        )}
      </Stack>
    </Card>
  );
}
