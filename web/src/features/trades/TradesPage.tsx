import {
  Button,
  Card,
  Group,
  Pagination,
  Rating,
  Select,
  Stack,
  Table,
  Text,
  TextInput,
} from '@mantine/core';
import { DatePickerInput } from '@mantine/dates';
import { useDebouncedCallback } from '@mantine/hooks';
import { IconFilterOff, IconPlus } from '@tabler/icons-react';
import { useNavigate, useSearchParams } from 'react-router';
import { useTrades } from '@/api/queries/trades';
import {
  OriginBadge,
  OutcomeBadge,
  ReviewStateBadge,
  SessionChips,
  SideBadge,
} from '@/components/Badges';
import { AccountSelect } from '@/components/Filters';
import { Duration } from '@/components/Duration';
import { Instant } from '@/components/Instant';
import { Pnl, Price, RMultiple } from '@/components/Money';
import { PageHeader } from '@/components/PageHeader';
import { EmptyState, ErrorState, LoadingTable } from '@/components/States';
import {
  DEFAULT_PAGE_SIZE,
  hasTradeFilters,
  readTradeFilters,
  writeTradeFilters,
  type TradeFilters,
} from '@/lib/filters';

export function TradesPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();

  const filters = readTradeFilters(searchParams);
  const trades = useTrades(filters);

  /** Any filter change resets to page 1 — page 7 of a different result set is meaningless. */
  function update(patch: Partial<TradeFilters>, keepPage = false) {
    const next: TradeFilters = {
      ...filters,
      ...patch,
      page: keepPage ? (patch.page ?? filters.page) : 1,
    };
    setSearchParams(writeTradeFilters(next), { replace: true });
  }

  const setSymbol = useDebouncedCallback((symbol: string) => {
    update({ symbol: symbol.trim() || undefined });
  }, 350);

  const items = trades.data?.items ?? [];
  const total = trades.data?.total ?? 0;
  const pageCount = Math.max(1, Math.ceil(total / filters.pageSize));

  return (
    <Stack gap="md">
      <PageHeader
        title="Journal"
        description={
          trades.data
            ? `${total.toLocaleString('en-US')} trade${total === 1 ? '' : 's'}`
            : 'Every round-trip, synced and manual.'
        }
        actions={
          <Button leftSection={<IconPlus size={16} />} onClick={() => void navigate('/trades/new')}>
            Manual trade
          </Button>
        }
      />

      <Card padding="sm">
        <Group gap="sm" align="flex-end" wrap="wrap">
          <AccountSelect
            value={filters.accountId}
            onChange={(accountId) => update({ accountId })}
            w={200}
          />

          <TextInput
            label="Symbol"
            placeholder="BTCUSDT"
            defaultValue={filters.symbol ?? ''}
            onChange={(event) => setSymbol(event.currentTarget.value)}
            w={150}
          />

          <Select
            label="Review"
            placeholder="Any"
            data={[
              { value: 'Unreviewed', label: 'Unreviewed' },
              { value: 'Reviewed', label: 'Reviewed' },
            ]}
            value={filters.reviewState ?? null}
            onChange={(value) =>
              update({ reviewState: (value as TradeFilters['reviewState']) ?? undefined })
            }
            clearable
            w={150}
          />

          <Select
            label="Session"
            placeholder="Any"
            data={[
              { value: 'Tokyo', label: 'Tokyo' },
              { value: 'London', label: 'London' },
              { value: 'NewYork', label: 'New York' },
            ]}
            value={filters.marketSession ?? null}
            onChange={(value) =>
              update({ marketSession: (value as TradeFilters['marketSession']) ?? undefined })
            }
            clearable
            w={150}
          />

          <DatePickerInput
            label="Opened from"
            placeholder="Any"
            clearable
            value={filters.from ?? null}
            onChange={(value) => update({ from: value ?? undefined })}
            w={160}
          />

          <DatePickerInput
            label="Opened to"
            placeholder="Any"
            clearable
            value={filters.to ?? null}
            onChange={(value) => update({ to: value ?? undefined })}
            w={160}
          />

          {hasTradeFilters(filters) && (
            <Button
              variant="subtle"
              leftSection={<IconFilterOff size={16} />}
              onClick={() =>
                setSearchParams(writeTradeFilters({ page: 1, pageSize: filters.pageSize }), {
                  replace: true,
                })
              }
            >
              Clear filters
            </Button>
          )}
        </Group>
      </Card>

      {trades.isError && <ErrorState error={trades.error} onRetry={() => void trades.refetch()} />}

      {trades.isPending && !trades.data && <LoadingTable rows={8} columns={7} />}

      {trades.data && items.length === 0 && (
        <EmptyState
          title={hasTradeFilters(filters) ? 'No trades match these filters' : 'No trades yet'}
          description={
            hasTradeFilters(filters)
              ? 'Widen the range, or clear the filters to see the whole journal.'
              : 'Trades arrive automatically once an account is synced, or add one by hand.'
          }
          action={
            hasTradeFilters(filters) ? (
              <Button
                variant="light"
                onClick={() =>
                  setSearchParams(writeTradeFilters({ page: 1, pageSize: DEFAULT_PAGE_SIZE }), {
                    replace: true,
                  })
                }
              >
                Clear filters
              </Button>
            ) : (
              <Button variant="light" onClick={() => void navigate('/settings')}>
                Set up an account
              </Button>
            )
          }
        />
      )}

      {items.length > 0 && (
        <Card padding={0}>
          <Table.ScrollContainer minWidth={1100}>
            <Table highlightOnHover>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Opened</Table.Th>
                  <Table.Th>Symbol</Table.Th>
                  <Table.Th>Side</Table.Th>
                  <Table.Th>Outcome</Table.Th>
                  <Table.Th ta="right">Net PnL</Table.Th>
                  <Table.Th ta="right">R</Table.Th>
                  <Table.Th>Strategy</Table.Th>
                  <Table.Th>Session</Table.Th>
                  <Table.Th>Review</Table.Th>
                  <Table.Th>Origin</Table.Th>
                  <Table.Th>Rating</Table.Th>
                  <Table.Th>Duration</Table.Th>
                </Table.Tr>
              </Table.Thead>

              <Table.Tbody>
                {items.map((trade) => (
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
                      <Text size="xs" c="dimmed">
                        <Price value={trade.entryPrice} size="xs" /> ×{trade.leverage}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <SideBadge side={trade.side} />
                    </Table.Td>
                    <Table.Td>
                      <OutcomeBadge outcome={trade.outcome} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Pnl value={trade.netProfitLoss} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <RMultiple value={trade.achievedReturnR} />
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm" c={trade.strategyName ? undefined : 'dimmed'}>
                        {trade.strategyName ?? '—'}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <SessionChips value={trade.marketSession} />
                    </Table.Td>
                    <Table.Td>
                      <ReviewStateBadge state={trade.reviewState} />
                    </Table.Td>
                    <Table.Td>
                      <OriginBadge origin={trade.origin} />
                    </Table.Td>
                    <Table.Td>
                      {trade.rating ? (
                        <Rating value={trade.rating} readOnly size="xs" />
                      ) : (
                        <Text size="sm" c="dimmed">
                          —
                        </Text>
                      )}
                    </Table.Td>
                    <Table.Td>
                      <Duration value={trade.duration} />
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Card>
      )}

      {pageCount > 1 && (
        <Group justify="space-between" wrap="wrap">
          <Text size="sm" c="dimmed">
            Page {filters.page} of {pageCount}
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
