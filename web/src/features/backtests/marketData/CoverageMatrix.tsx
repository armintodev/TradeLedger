import { useMemo, useState } from 'react';
import { Badge, Card, Group, Stack, Table, Text, TextInput, Title, Tooltip } from '@mantine/core';
import { IconSearch } from '@tabler/icons-react';
import { useCoverage } from '@/api/queries/marketData';
import { Instant } from '@/components/Instant';
import { EmptyState, ErrorState, LoadingTable } from '@/components/States';
import { formatInteger } from '@/lib/format';
import { ALL_INTERVALS, INTERVAL_LABELS, SOURCE_LABELS } from '@/lib/marketData';
import type { CandleInterval, MarketDataCoverageResponse } from '@/api/types';

type RowKey = string;

interface CoverageRow {
  key: RowKey;
  source: string;
  symbol: string;
  cells: Partial<Record<CandleInterval, MarketDataCoverageResponse>>;
}

/**
 * Every stored (source, symbol, interval) triple, pivoted into a grid. The
 * endpoint takes no filters and no paging — it returns the whole matrix — so
 * the search box filters client-side.
 *
 * Candles are not user-owned, so this is shared storage: what you see here is
 * what every user sees, and it is the only visibility into how much disk the
 * candle table is using.
 */
export function CoverageMatrix() {
  const coverage = useCoverage();
  const [search, setSearch] = useState('');

  const rows = useMemo(() => {
    const grouped = new Map<RowKey, CoverageRow>();

    for (const entry of coverage.data ?? []) {
      const key = `${entry.source}|${entry.symbol}`;
      const row = grouped.get(key) ?? {
        key,
        source: entry.source,
        symbol: entry.symbol,
        cells: {},
      };

      row.cells[entry.interval] = entry;
      grouped.set(key, row);
    }

    const needle = search.trim().toUpperCase();

    return [...grouped.values()]
      .filter((row) => !needle || row.symbol.includes(needle))
      .sort((a, b) => a.symbol.localeCompare(b.symbol) || a.source.localeCompare(b.source));
  }, [coverage.data, search]);

  // Only render columns that actually hold something — ten columns of mostly
  // empty cells is not a table anyone can read.
  const columns = useMemo(() => {
    const present = new Set((coverage.data ?? []).map((entry) => entry.interval));

    return ALL_INTERVALS.filter((interval) => present.has(interval));
  }, [coverage.data]);

  return (
    <Card padding="md">
      <Stack gap="sm">
        <Group justify="space-between" align="flex-start" wrap="wrap" gap="sm">
          <Stack gap={2}>
            <Title order={5}>Stored candles</Title>
            <Text size="xs" c="dimmed">
              What a backtest can actually run over. Shared storage — not per-user.
            </Text>
          </Stack>

          <TextInput
            placeholder="Filter by symbol"
            leftSection={<IconSearch size={14} />}
            value={search}
            onChange={(event) => setSearch(event.currentTarget.value)}
            w={200}
          />
        </Group>

        {coverage.isError && (
          <ErrorState error={coverage.error} onRetry={() => void coverage.refetch()} />
        )}

        {coverage.isPending && !coverage.data && <LoadingTable rows={4} columns={5} />}

        {coverage.data && rows.length === 0 && (
          <EmptyState
            title={search ? 'No symbol matches' : 'No candles stored'}
            description={
              search
                ? 'Nothing stored for that symbol yet.'
                : 'Backfill a range from Binance, or import a CSV export. Without candles there is nothing to backtest over.'
            }
          />
        )}

        {rows.length > 0 && (
          <Table.ScrollContainer minWidth={140 + columns.length * 110}>
            <Table withColumnBorders>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Symbol</Table.Th>
                  {columns.map((interval) => (
                    <Table.Th key={interval} ta="center">
                      <Group gap={4} justify="center" wrap="nowrap">
                        <span>{INTERVAL_LABELS[interval]}</span>
                        {interval === 'OneMinute' && (
                          <Tooltip
                            label="Drill-down only. One-minute candles resolve which of a stop or target was hit first inside a larger bar; they are not a tradeable interval."
                            withArrow
                            multiline
                            w={260}
                          >
                            <Badge size="xs" variant="light" color="gray">
                              1m
                            </Badge>
                          </Tooltip>
                        )}
                      </Group>
                    </Table.Th>
                  ))}
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {rows.map((row) => (
                  <Table.Tr key={row.key}>
                    <Table.Td>
                      <Text size="sm" fw={600}>
                        {row.symbol}
                      </Text>
                      <Text size="xs" c="dimmed">
                        {SOURCE_LABELS[row.source as keyof typeof SOURCE_LABELS] ?? row.source}
                      </Text>
                    </Table.Td>

                    {columns.map((interval) => {
                      const cell = row.cells[interval];

                      return (
                        <Table.Td key={interval} ta="center">
                          {cell ? (
                            <Tooltip
                              label={
                                <Stack gap={0}>
                                  <Text size="xs">{formatInteger(cell.rowCount)} candles</Text>
                                  <Text size="xs">
                                    {cell.firstOpenTime?.slice(0, 10)} →{' '}
                                    {cell.lastOpenTime?.slice(0, 10)}
                                  </Text>
                                </Stack>
                              }
                              withArrow
                            >
                              <Stack gap={0}>
                                <Text size="sm" ff="monospace">
                                  {formatInteger(cell.rowCount)}
                                </Text>
                                <Text size="xs" c="dimmed">
                                  <Instant value={cell.lastOpenTime} dateOnly size="xs" />
                                </Text>
                              </Stack>
                            </Tooltip>
                          ) : (
                            <Text size="sm" c="dimmed">
                              —
                            </Text>
                          )}
                        </Table.Td>
                      );
                    })}
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
