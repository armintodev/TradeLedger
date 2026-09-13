import type { ReactNode } from 'react';
import {
  Alert,
  Badge,
  Card,
  Drawer,
  Grid,
  Group,
  Skeleton,
  Stack,
  Table,
  Text,
} from '@mantine/core';
import { IconAlertTriangle } from '@tabler/icons-react';
import { useBacktestRunTrade } from '@/api/queries/backtests';
import { ExitReasonBadge, OutcomeBadge, ResolutionBadge, SideBadge } from '@/components/Badges';
import { Duration } from '@/components/Duration';
import { Instant } from '@/components/Instant';
import { Money, Pnl, Price, Quantity, RMultiple } from '@/components/Money';
import { ErrorState } from '@/components/States';
import { formatInteger, formatPercent } from '@/lib/format';
import type { BacktestTradeDetailResponse } from '@/api/types';

interface PositionDetailDrawerProps {
  runId: string;
  /** The open position, or null when the drawer is closed. Comes from the URL. */
  tradeId: string | null;
  onClose: () => void;
}

/**
 * One simulated position, end to end. The fills come folded into the same fetch
 * rather than as a second round trip, because a position is explained by what it
 * was entered on, not by a row of totals.
 */
export function PositionDetailDrawer({ runId, tradeId, onClose }: PositionDetailDrawerProps) {
  const trade = useBacktestRunTrade(runId, tradeId ?? undefined);

  return (
    <Drawer
      opened={tradeId !== null}
      onClose={onClose}
      position="right"
      size="xl"
      padding="md"
      title={
        // Plain Text, not Title: Mantine renders the drawer title as an <h2>
        // already, and a heading nested in a heading is invalid HTML.
        trade.data ? (
          <Group gap="xs">
            <Text fw={700} size="lg">
              #{trade.data.sequence} {trade.data.symbol}
            </Text>
            <SideBadge side={trade.data.side} />
            <OutcomeBadge outcome={trade.data.outcome} />
          </Group>
        ) : (
          <Text fw={700} size="lg">
            Position
          </Text>
        )
      }
    >
      {trade.isError && <ErrorState error={trade.error} onRetry={() => void trade.refetch()} />}

      {trade.isPending && <Skeleton height={420} radius="md" />}

      {trade.data && <PositionBody trade={trade.data} />}
    </Drawer>
  );
}

function PositionBody({ trade }: { trade: BacktestTradeDetailResponse }) {
  return (
    <Stack gap="md">
      <Group gap="xs">
        <ExitReasonBadge reason={trade.exitReason} />
        <ResolutionBadge resolution={trade.intrabarResolution} />
        {trade.wasLiquidated && (
          <Badge color="red" variant="filled" size="sm">
            Liquidated
          </Badge>
        )}
      </Group>

      {trade.exitWasAssumed && (
        <Alert color="orange" variant="light" icon={<IconAlertTriangle size={18} />}>
          This bar held both the stop and the target, and finer data could not say which came first
          — so the worse of the two was assumed. The exit price below is a pessimistic guess, not a
          fill the engine could prove.
        </Alert>
      )}

      <Card padding="sm" withBorder>
        <Group justify="space-between" wrap="wrap" gap="xs">
          <Stack gap={2}>
            <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
              Net
            </Text>
            <Pnl value={trade.netProfitLoss} size="xl" fw={700} />
          </Stack>
          <Stack gap={2} align="flex-end">
            <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
              Balance after
            </Text>
            <Money value={trade.balanceAfter} size="lg" fw={600} />
          </Stack>
        </Group>
      </Card>

      <Section title="When">
        <Fact label="Opened" value={<Instant value={trade.openedAt} seconds />} />
        <Fact label="Closed" value={<Instant value={trade.closedAt} seconds />} />
        <Fact label="Held" value={<Duration value={trade.duration} />} />
        <Fact
          label="Bars"
          value={`${formatInteger(trade.barsInTrade)} (#${formatInteger(
            trade.entryBarIndex,
          )} → #${formatInteger(trade.exitBarIndex)})`}
        />
      </Section>

      <Section title="Levels">
        <Fact label="Entry" value={<Price value={trade.entryPrice} />} />
        <Fact label="Exit" value={<Price value={trade.exitPrice} />} />
        <Fact label="Stop loss" value={<Price value={trade.stopLossPrice} />} />
        <Fact label="Take profit" value={<Price value={trade.takeProfitPrice} />} />
        <Fact label="Liquidation" value={<Price value={trade.liquidationPrice} />} />
      </Section>

      <Section title="Size">
        <Fact label="Quantity" value={<Quantity value={trade.quantity} />} />
        <Fact label="Leverage" value={`${trade.leverage}×`} />
        <Fact label="Margin" value={<Money value={trade.positionMargin} />} />
        <Fact label="Order value" value={<Money value={trade.orderValue} />} />
      </Section>

      <Section title="Money">
        <Fact label="Gross" value={<Pnl value={trade.grossProfitLoss} />} />
        <Fact label="Fees" value={<Money value={trade.fees} />} />
        <Fact label="Funding" value={<Money value={trade.funding} />} />
        <Fact label="Net" value={<Pnl value={trade.netProfitLoss} />} />
        <Fact label="Gain on margin" value={formatPercent(trade.tradeGainPercent)} />
      </Section>

      <Section title="Risk">
        <Fact label="Achieved R" value={<RMultiple value={trade.achievedReturnR} />} />
        <Fact label="Planned R" value={<RMultiple value={trade.plannedReturnR} />} />
        <Fact
          label="MAE"
          hint="How far the position went against the entry before it resolved, in R."
          value={<RMultiple value={trade.maeR} />}
        />
        <Fact
          label="MFE"
          hint="How far it went in favour before it resolved, in R."
          value={<RMultiple value={trade.mfeR} />}
        />
      </Section>

      {trade.notes && (
        <Stack gap={4}>
          <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
            Engine note
          </Text>
          <Text size="sm" style={{ whiteSpace: 'pre-wrap' }}>
            {trade.notes}
          </Text>
        </Stack>
      )}

      <Stack gap="xs">
        <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
          Fills
        </Text>

        {trade.executions.length === 0 ? (
          <Text size="sm" c="dimmed">
            No fills were recorded for this position.
          </Text>
        ) : (
          <Table.ScrollContainer minWidth={520}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Role</Table.Th>
                  <Table.Th>At</Table.Th>
                  <Table.Th ta="right">Bar</Table.Th>
                  <Table.Th ta="right">Price</Table.Th>
                  <Table.Th ta="right">Qty</Table.Th>
                  <Table.Th ta="right">Notional</Table.Th>
                  <Table.Th ta="right">Fee</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {trade.executions.map((execution) => (
                  <Table.Tr key={execution.id}>
                    <Table.Td>
                      <Badge
                        variant="light"
                        size="sm"
                        color={execution.role === 'Liquidation' ? 'red' : 'gray'}
                      >
                        {execution.role}
                      </Badge>
                    </Table.Td>
                    <Table.Td>
                      <Instant value={execution.executedAt} seconds />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Text size="sm" c="dimmed">
                        #{formatInteger(execution.barIndex)}
                      </Text>
                    </Table.Td>
                    <Table.Td ta="right">
                      <Price value={execution.price} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Quantity value={execution.quantity} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Money value={execution.notional} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Money value={execution.fee} />
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        )}
      </Stack>
    </Stack>
  );
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <Stack gap={6}>
      <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
        {title}
      </Text>
      <Grid gap="xs">{children}</Grid>
    </Stack>
  );
}

function Fact({ label, value, hint }: { label: string; value: ReactNode; hint?: string }) {
  return (
    <Grid.Col span={{ base: 6, sm: 4 }}>
      <Text size="xs" c="dimmed" title={hint}>
        {label}
      </Text>
      <Text size="sm" component="div">
        {value}
      </Text>
    </Grid.Col>
  );
}
