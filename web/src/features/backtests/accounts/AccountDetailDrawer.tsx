import { lazy, Suspense } from 'react';
import { Badge, Drawer, Group, Skeleton, SimpleGrid, Stack, Text, Title } from '@mantine/core';
import { useBacktestAccountEquity, useBacktestAccountSummary } from '@/api/queries/backtests';
import { Money, Pnl, RMultiple } from '@/components/Money';
import { StatTile } from '@/components/StatTile';
import { EmptyState, ErrorState } from '@/components/States';
import { formatInteger, formatPercent, formatRatio } from '@/lib/format';
import { formatDuration, formatInstant } from '@/lib/time';
import { useTimeZone } from '@/lib/timeZone';
import type { BacktestAccountResponse } from '@/api/types';

const EquityChart = lazy(() => import('@/components/EquityChart'));

/**
 * The stitched curve across every succeeded run on the account, and the same
 * performance metrics the live dashboard uses — which is what makes a backtest
 * comparable to real trading rather than merely adjacent to it.
 */
export function AccountDetailDrawer({
  account,
  onClose,
}: {
  account: BacktestAccountResponse | null;
  onClose: () => void;
}) {
  const timeZone = useTimeZone();
  const equity = useBacktestAccountEquity(account?.id);
  const summary = useBacktestAccountSummary(account?.id);

  const points = (equity.data?.points ?? []).map((point) => ({
    at: formatInstant(point.at, { timeZone, dateOnly: true }),
    equity: point.equity,
  }));

  return (
    <Drawer
      opened={account !== null}
      onClose={onClose}
      position="right"
      size="xl"
      title={
        <Group gap="xs">
          <Title order={4}>{account?.name}</Title>
          {account && (
            <Badge variant="light" color={account.mode === 'Sequential' ? 'indigo' : 'gray'}>
              {account.mode}
            </Badge>
          )}
        </Group>
      }
    >
      <Stack gap="md">
        {account && (
          <SimpleGrid cols={{ base: 2, sm: 4 }} spacing="sm">
            <StatTile
              label="Starting"
              value={<Money value={account.startingBalance} size="xl" />}
            />
            <StatTile label="Current" value={<Money value={account.currentBalance} size="xl" />} />
            <StatTile label="Net" value={<Pnl value={account.netProfitLoss} size="xl" />} />
            <StatTile label="Succeeded runs" value={formatInteger(account.succeededRuns)} />
          </SimpleGrid>
        )}

        {equity.isError && (
          <ErrorState error={equity.error} onRetry={() => void equity.refetch()} />
        )}

        {equity.data && points.length === 0 && (
          <EmptyState
            title="No equity curve yet"
            description="The curve is stitched from the per-trade equity points of every succeeded run. Queue one and it will appear here."
          />
        )}

        {points.length > 0 && (
          <Stack gap="xs">
            <Group justify="space-between" wrap="wrap">
              <Title order={5}>Equity</Title>
              <Group gap="lg">
                <Text size="xs" c="dimmed">
                  Peak <Money value={equity.data?.peakEquity} size="xs" />
                </Text>
                <Text size="xs" c="dimmed">
                  Max drawdown <Money value={equity.data?.maxDrawdown} size="xs" /> (
                  {formatPercent(equity.data?.maxDrawdownPercent)})
                </Text>
              </Group>
            </Group>

            <Suspense fallback={<Skeleton height={240} radius="md" />}>
              <EquityChart data={points} />
            </Suspense>
          </Stack>
        )}

        {summary.isError && (
          <ErrorState error={summary.error} onRetry={() => void summary.refetch()} />
        )}

        {summary.data && (
          <Stack gap="xs">
            <Title order={5}>Across every run</Title>
            <SimpleGrid cols={{ base: 2, sm: 3 }} spacing="sm">
              <StatTile label="Trades" value={formatInteger(summary.data.totalTrades)} />
              <StatTile label="Win rate" value={formatPercent(summary.data.winRate)} />
              <StatTile
                label="Profit factor"
                value={formatRatio(summary.data.profitFactor)}
                hint="Null — and shown as a dash — when there are no losing trades."
              />
              <StatTile
                label="Expectancy"
                value={<Pnl value={summary.data.expectancy} size="xl" />}
              />
              <StatTile
                label="Average R"
                value={<RMultiple value={summary.data.averageAchievedR} size="xl" />}
              />
              <StatTile
                label="Average duration"
                value={formatDuration(summary.data.averageDuration)}
              />
            </SimpleGrid>
          </Stack>
        )}
      </Stack>
    </Drawer>
  );
}
