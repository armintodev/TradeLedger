import { useState } from 'react';
import { Button, Card, Group, Pagination, Stack, Table, Text, Title } from '@mantine/core';
import { IconArrowRight } from '@tabler/icons-react';
import { useNavigate } from 'react-router';
import { TRADES_PAGE_SIZE, useBacktestRunTrades } from '@/api/queries/backtests';
import { ExitReasonBadge, OutcomeBadge, ResolutionBadge, SideBadge } from '@/components/Badges';
import { Duration } from '@/components/Duration';
import { Instant } from '@/components/Instant';
import { Money, Pnl, Price, Quantity, RMultiple } from '@/components/Money';
import { EmptyState, ErrorState, LoadingTable } from '@/components/States';
import { formatInteger } from '@/lib/format';

/**
 * The run's ledger, inline. Paging state is local because this is a panel inside
 * a page that already owns the query string; the full positions view at
 * `/backtests/runs/:id/trades` is the one that keeps its page in the URL.
 */
export function RunTradesTable({ runId }: { runId: string }) {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const trades = useBacktestRunTrades(runId, page);

  const items = trades.data?.items ?? [];
  const total = trades.data?.total ?? 0;
  const pageCount = Math.max(1, Math.ceil(total / TRADES_PAGE_SIZE));

  /** Opening a position hands off to the full view, which can give it room. */
  function open(tradeId: string) {
    void navigate(`/backtests/runs/${runId}/trades?position=${tradeId}`);
  }

  return (
    <Card padding="md">
      <Stack gap="sm">
        <Group justify="space-between" align="flex-start" wrap="wrap" gap="sm">
          <Stack gap={2}>
            <Title order={5}>Simulated trades</Title>
            {trades.data && (
              <Text size="xs" c="dimmed">
                {formatInteger(total)} position{total === 1 ? '' : 's'} opened and closed.
              </Text>
            )}
          </Stack>

          <Button
            size="compact-sm"
            variant="light"
            rightSection={<IconArrowRight size={14} />}
            onClick={() => void navigate(`/backtests/runs/${runId}/trades`)}
          >
            Every position
          </Button>
        </Group>

        {trades.isError && (
          <ErrorState error={trades.error} onRetry={() => void trades.refetch()} />
        )}

        {trades.isPending && !trades.data && <LoadingTable rows={6} columns={8} />}

        {trades.data && items.length === 0 && (
          <EmptyState
            title="No trades"
            description="The rules never fired over this range, or every signal was skipped. The engine counters above say which."
          />
        )}

        {items.length > 0 && (
          <Table.ScrollContainer minWidth={1100}>
            <Table highlightOnHover>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>#</Table.Th>
                  <Table.Th>Opened</Table.Th>
                  <Table.Th>Closed</Table.Th>
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
                  <Table.Tr
                    key={trade.id}
                    style={{ cursor: 'pointer' }}
                    onClick={() => open(trade.id)}
                  >
                    <Table.Td>
                      <Text size="xs" c="dimmed">
                        {trade.sequence}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Instant value={trade.openedAt} />
                    </Table.Td>
                    <Table.Td>
                      <Instant value={trade.closedAt} />
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

        {pageCount > 1 && (
          <Group justify="flex-end">
            <Pagination value={page} onChange={setPage} total={pageCount} size="sm" />
          </Group>
        )}
      </Stack>
    </Card>
  );
}
