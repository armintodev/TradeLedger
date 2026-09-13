import { Alert, Card, Group, List, SimpleGrid, Stack, Text, Title, Tooltip } from '@mantine/core';
import { IconAlertTriangle } from '@tabler/icons-react';
import { Money, Pnl, RMultiple } from '@/components/Money';
import { StatTile } from '@/components/StatTile';
import { formatInteger, formatPercent, formatRatio } from '@/lib/format';
import { formatDuration } from '@/lib/time';
import type { BacktestResultSummary } from '@/api/types';

/**
 * What a finished run produced, and how much of it the engine had to invent.
 *
 * `skippedNoCandleData` and `liquidationRiskCount` are deliberately absent: the
 * API declares them but never assigns them, so they are always zero. A tile
 * reading 0 is a claim, and there it would be a false one — same reasoning as
 * rendering a null profit factor as a dash. See `docs/backtest-impl.md` §5.
 */
export function RunResultPanel({
  result,
  warnings,
}: {
  result: BacktestResultSummary;
  warnings: string[];
}) {
  const performance = result.performance;

  const resolution = {
    unambiguous: result.resolvedUnambiguous,
    byMinute: result.resolvedByMinute,
    assumed: result.assumedWithinMinute + result.assumedNoMinuteData,
  };

  const decided = resolution.unambiguous + resolution.byMinute + resolution.assumed;

  // Integer display arithmetic over counts the server already computed — not
  // money arithmetic, and not an aggregate the client is inventing.
  const assumedShare = decided > 0 ? (resolution.assumed / decided) * 100 : 0;

  return (
    <Stack gap="md">
      {warnings.length > 0 && (
        <Alert color="orange" variant="light" icon={<IconAlertTriangle size={18} />}>
          <List size="sm" spacing={4}>
            {warnings.map((warning) => (
              <List.Item key={warning}>{warning}</List.Item>
            ))}
          </List>
        </Alert>
      )}

      {performance && (
        <SimpleGrid cols={{ base: 2, sm: 3, lg: 6 }} spacing="sm">
          <StatTile label="Net PnL" value={<Pnl value={performance.netProfitLoss} size="xl" />} />
          <StatTile
            label="Trades"
            value={formatInteger(performance.totalTrades)}
            sub={`${performance.winningTrades}W · ${performance.losingTrades}L`}
          />
          <StatTile label="Win rate" value={formatPercent(performance.winRate)} />
          <StatTile
            label="Profit factor"
            value={formatRatio(performance.profitFactor)}
            hint="Null — and shown as a dash — when there were no losing trades."
          />
          <StatTile label="Expectancy" value={<Pnl value={performance.expectancy} size="xl" />} />
          <StatTile
            label="Average R"
            value={<RMultiple value={performance.averageAchievedR} size="xl" />}
          />

          <StatTile
            label="Average win"
            value={<Money value={performance.averageWin} size="xl" />}
          />
          <StatTile
            label="Average loss"
            value={<Money value={performance.averageLoss} size="xl" />}
          />
          <StatTile label="Fees" value={<Money value={performance.totalFees} size="xl" />} />
          <StatTile label="Funding" value={<Money value={performance.totalFunding} size="xl" />} />
          <StatTile
            label="Longest win streak"
            value={formatInteger(performance.longestWinStreak)}
          />
          <StatTile label="Average duration" value={formatDuration(performance.averageDuration)} />
        </SimpleGrid>
      )}

      <Card padding="md">
        <Stack gap="sm">
          <Group justify="space-between" align="flex-start" wrap="wrap" gap="sm">
            <Stack gap={2}>
              <Title order={5}>How the exits were decided</Title>
              <Text size="xs" c="dimmed">
                When a single bar held both the stop and the target, the engine drills into
                one-minute candles. When it cannot separate them, it assumes the worse outcome.
              </Text>
            </Stack>

            <Tooltip
              label="An assumed exit is a pessimistic guess, not a measurement. Backfill one-minute candles for this range to replace them with real resolutions."
              withArrow
              multiline
              w={280}
            >
              <Stack gap={0} align="flex-end">
                <Text size="xl" fw={700} ff="monospace" c={assumedShare > 10 ? 'orange' : 'teal'}>
                  {formatPercent(assumedShare, 0)}
                </Text>
                <Text size="xs" c="dimmed">
                  assumed
                </Text>
              </Stack>
            </Tooltip>
          </Group>

          <SimpleGrid cols={{ base: 2, sm: 4 }} spacing="sm">
            <StatTile label="Unambiguous" value={formatInteger(resolution.unambiguous)} />
            <StatTile label="By minute data" value={formatInteger(resolution.byMinute)} />
            <StatTile
              label="Assumed within minute"
              value={formatInteger(result.assumedWithinMinute)}
            />
            <StatTile
              label="Assumed — no 1m data"
              value={formatInteger(result.assumedNoMinuteData)}
              hint="There were no one-minute candles to drill into. Backfill them for this range and re-run."
            />
          </SimpleGrid>
        </Stack>
      </Card>

      <Card padding="md">
        <Stack gap="sm">
          <Title order={5}>Engine counters</Title>

          <SimpleGrid cols={{ base: 2, sm: 3, lg: 5 }} spacing="sm">
            <StatTile
              label="Max intrabar drawdown"
              value={<Money value={result.maxIntrabarDrawdown} size="xl" />}
              sub={formatPercent(result.maxIntrabarDrawdownPercent)}
              hint="Measured against each bar's adverse extreme while a position was open — always larger than the between-trades figure on the equity curve."
            />
            <StatTile
              label="Ambiguous signals"
              value={formatInteger(result.ambiguousSignals)}
              hint="Both the long and short rules fired on the same bar, so the engine took neither."
            />
            <StatTile
              label="Skipped — invalid stop"
              value={formatInteger(result.skippedInvalidStop)}
              hint="The stop landed on the wrong side of the entry, so the trade was skipped rather than sized nonsensically."
            />
            <StatTile
              label="Skipped — margin"
              value={formatInteger(result.skippedInsufficientMargin)}
            />
            <StatTile
              label="Open at end of data"
              value={formatInteger(result.openAtEndOfData)}
              hint="Positions still open when the range ran out."
            />
          </SimpleGrid>
        </Stack>
      </Card>
    </Stack>
  );
}
