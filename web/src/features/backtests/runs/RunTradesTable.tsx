import { useState } from 'react';
import { Button, Card, Group, Stack, Table, Text, Title } from '@mantine/core';
import { IconChevronLeft, IconChevronRight } from '@tabler/icons-react';
import { TRADES_PAGE_SIZE, useBacktestRunTrades } from '@/api/queries/backtests';
import { ExitReasonBadge, OutcomeBadge, ResolutionBadge, SideBadge } from '@/components/Badges';
import { Duration } from '@/components/Duration';
import { Instant } from '@/components/Instant';
import { Money, Pnl, Price, Quantity, RMultiple } from '@/components/Money';
import { EmptyState, ErrorState, LoadingTable } from '@/components/States';

/**
 * The endpoint pages but returns a bare array with no total, so a page count
 * cannot be derived and a normal pagination control is impossible. Next is
 * disabled as soon as a page comes back short. No total is displayed, because
 * none can be known. See `docs/backtest-impl.md` §8.
 */
export function RunTradesTable({ runId }: { runId: string }) {
  const [page, setPage] = useState(1);
  const trades = useBacktestRunTrades(runId, page);

  const items = trades.data ?? [];
  const atEnd = items.length < TRADES_PAGE_SIZE;

  return (
    <Card padding="md">
      <Stack gap="sm">
        <Group justify="space-between" align="flex-start" wrap="wrap" gap="sm">
          <Title order={5}>Simulated trades</Title>

          {(page > 1 || !atEnd) && (
            <Group gap="xs">
              <Button
                size="compact-xs"
                variant="light"
                leftSection={<IconChevronLeft size={13} />}
                disabled={page === 1 || trades.isFetching}
                onClick={() => setPage((current) => Math.max(1, current - 1))}
              >
                Previous
              </Button>
              <Text size="xs" c="dimmed">
                Page {page}
              </Text>
              <Button
                size="compact-xs"
                variant="light"
                rightSection={<IconChevronRight size={13} />}
                disabled={atEnd || trades.isFetching}
                onClick={() => setPage((current) => current + 1)}
              >
                Next
              </Button>
            </Group>
          )}
        </Group>

        {trades.isError && (
          <ErrorState error={trades.error} onRetry={() => void trades.refetch()} />
        )}

        {trades.isPending && !trades.data && <LoadingTable rows={6} columns={8} />}

        {trades.data && items.length === 0 && (
          <EmptyState
            title={page > 1 ? 'No more trades' : 'No trades'}
            description={
              page > 1
                ? 'That was the last page.'
                : 'The rules never fired over this range, or every signal was skipped. The engine counters above say which.'
            }
          />
        )}

        {items.length > 0 && (
          <Table.ScrollContainer minWidth={1100}>
            <Table highlightOnHover>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>#</Table.Th>
                  <Table.Th>Opened</Table.Th>
                  <Table.Th>Side</Table.Th>
                  <Table.Th ta="right">Entry</Table.Th>
                  <Table.Th ta="right">Exit</Table.Th>
                  <Table.Th ta="right">Qty</Table.Th>
                  <Table.Th ta="right">Net</Table.Th>
                  <Table.Th ta="right">R</Table.Th>
                  <Table.Th>Outcome</Table.Th>
                  <Table.Th>Exit</Table.Th>
                  <Table.Th>Resolution</Table.Th>
                  <Table.Th>Held</Table.Th>
                  <Table.Th ta="right">Balance</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {items.map((trade) => (
                  <Table.Tr key={trade.id}>
                    <Table.Td>
                      <Text size="xs" c="dimmed">
                        {trade.sequence}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Instant value={trade.openedAt} />
                    </Table.Td>
                    <Table.Td>
                      <SideBadge side={trade.side} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Price value={trade.entryPrice} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Price value={trade.exitPrice} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Quantity value={trade.quantity} />
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
                    <Table.Td>
                      <Duration value={trade.duration} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Money value={trade.balanceAfter} />
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
