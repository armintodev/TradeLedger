import type { ReactNode } from 'react';
import { Card, Divider, Grid, Group, Stack, Text, Title } from '@mantine/core';
import { OutcomeBadge, PlannedBadge, SessionChips, SideBadge } from '@/components/Badges';
import { Duration } from '@/components/Duration';
import { Instant } from '@/components/Instant';
import { Money, Pnl, Price, Quantity, RMultiple } from '@/components/Money';
import { formatPercent } from '@/lib/format';
import type { TradeDetailResponse } from '@/api/types';

/**
 * The mechanical half: everything Bitunix knows and the trader never types.
 * Shared by the trade detail page and the review queue, where it sits read-only
 * beside the journal form.
 */
export function TradeFacts({
  trade,
  compact = false,
}: {
  trade: TradeDetailResponse;
  compact?: boolean;
}) {
  return (
    <Card padding="md">
      <Stack gap="sm">
        <Group justify="space-between" align="flex-start" wrap="wrap" gap="xs">
          <Stack gap={4}>
            <Group gap="xs">
              <Title order={compact ? 4 : 3}>{trade.symbol}</Title>
              <SideBadge side={trade.side} />
              <OutcomeBadge outcome={trade.outcome} />
              <PlannedBadge isPlanned={trade.isPlanned} />
            </Group>
            <Group gap={6}>
              <Instant value={trade.openedAt} />
              <Text size="sm" c="dimmed">
                →
              </Text>
              <Instant value={trade.closedAt} />
            </Group>
          </Stack>

          <Stack gap={2} align="flex-end">
            <Pnl value={trade.netProfitLoss} size="xl" fw={700} />
            <Text size="xs" c="dimmed">
              net of fees and funding
            </Text>
          </Stack>
        </Group>

        <Divider />

        <Grid gap="xs">
          <Fact label="Entry" value={<Price value={trade.entryPrice} />} />
          <Fact label="Exit" value={<Price value={trade.exitPrice} />} />
          <Fact label="Quantity" value={<Quantity value={trade.quantity} />} />
          <Fact label="Leverage" value={`${trade.leverage}×`} />

          <Fact label="Gross PnL" value={<Pnl value={trade.grossProfitLoss} />} />
          <Fact label="Fees" value={<Money value={trade.fees} />} />
          <Fact label="Funding" value={<Money value={trade.funding} />} />
          <Fact label="Duration" value={<Duration value={trade.duration} />} />

          <Fact label="Achieved R" value={<RMultiple value={trade.achievedReturnR} />} />
          <Fact label="Planned R" value={<RMultiple value={trade.plannedReturnR} />} />
          <Fact label="Trade gain" value={formatPercent(trade.tradeGainPercent)} />
          <Fact label="Account change" value={formatPercent(trade.accountChangePercent)} />

          <Fact label="Margin" value={<Money value={trade.positionMargin} />} />
          <Fact label="Order value" value={<Money value={trade.orderValue} />} />
          <Fact label="Position / account" value={formatPercent(trade.positionToAccountPercent)} />
          <Fact label="Account risked" value={formatPercent(trade.accountRiskedPercent)} />

          <Fact label="Stop loss" value={<Price value={trade.stopLossPrice} />} />
          <Fact label="Take profit" value={<Price value={trade.takeProfitPrice} />} />
          <Fact label="Closed" value={formatPercent(trade.percentClosed)} />
          <Fact label="Balance after" value={<Money value={trade.balanceAfter} />} />

          <Fact label="Session" value={<SessionChips value={trade.marketSession} />} />
          <Fact label="Margin mode" value={trade.marginMode ?? '—'} />
          <Fact label="Position mode" value={trade.positionMode ?? '—'} />
          <Fact label="Order type" value={trade.orderType} />

          {(trade.liquidationPrice !== null || trade.liquidatedQuantity !== null) && (
            <>
              <Fact label="Liquidation price" value={<Price value={trade.liquidationPrice} />} />
              <Fact label="Liquidated qty" value={<Quantity value={trade.liquidatedQuantity} />} />
            </>
          )}
        </Grid>
      </Stack>
    </Card>
  );
}

function Fact({ label, value }: { label: string; value: ReactNode }) {
  return (
    <Grid.Col span={{ base: 6, sm: 4, lg: 3 }}>
      <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
        {label}
      </Text>
      <Text size="sm" component="div">
        {value}
      </Text>
    </Grid.Col>
  );
}
