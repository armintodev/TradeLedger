import { useState } from 'react';
import { BarChart } from '@mantine/charts';
import { Card, Group, Select, Stack, Table, Text, Title, UnstyledButton } from '@mantine/core';
import { IconArrowDown, IconArrowUp, IconArrowsSort } from '@tabler/icons-react';
import { useSearchParams } from 'react-router';
import { BREAKDOWN_DIMENSIONS, useBreakdown, useMistakeCosts } from '@/api/queries/analytics';
import { RangeControl } from '@/components/Filters';
import { Money, Pnl, RMultiple } from '@/components/Money';
import { PageHeader } from '@/components/PageHeader';
import { EmptyState, ErrorState, LoadingTable } from '@/components/States';
import { formatInteger, formatPercent } from '@/lib/format';
import { readAnalyticsFilters, writeAnalyticsFilters, type AnalyticsFilters } from '@/lib/filters';
import type { BreakdownDimension, BreakdownRow } from '@/api/types';

type SortKey = keyof Pick<
  BreakdownRow,
  'key' | 'tradeCount' | 'winCount' | 'winRate' | 'netProfitLoss' | 'averageR'
>;

export function AnalyticsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const filters = readAnalyticsFilters(searchParams);

  const dimension = (searchParams.get('dimension') as BreakdownDimension | null) ?? 'Strategy';

  const breakdown = useBreakdown(dimension, filters);
  const mistakes = useMistakeCosts(filters);

  const [sort, setSort] = useState<{ key: SortKey; desc: boolean }>({
    key: 'netProfitLoss',
    desc: true,
  });

  function update(next: AnalyticsFilters, nextDimension = dimension) {
    const params = writeAnalyticsFilters(next);

    if (nextDimension !== 'Strategy') {
      params.set('dimension', nextDimension);
    }

    setSearchParams(params, { replace: true });
  }

  const rows = sortRows(breakdown.data ?? [], sort);

  // Charting every hour of the day or every symbol produces an unreadable axis;
  // the table below still shows all of them.
  const chartData = rows.slice(0, 15).map((row) => ({
    key: row.key,
    netProfitLoss: row.netProfitLoss,
  }));

  return (
    <Stack gap="md">
      <PageHeader
        title="Analytics"
        description="Where the money actually comes from, and what it costs you."
      />

      <Card padding="sm">
        <Group gap="sm" align="flex-end" wrap="wrap">
          <Select
            label="Break down by"
            data={BREAKDOWN_DIMENSIONS}
            value={dimension}
            onChange={(value) => update(filters, (value as BreakdownDimension) ?? 'Strategy')}
            allowDeselect={false}
            w={200}
          />
          <RangeControl filters={filters} onChange={(next) => update(next)} />
        </Group>
      </Card>

      {breakdown.isError && (
        <ErrorState error={breakdown.error} onRetry={() => void breakdown.refetch()} />
      )}

      {breakdown.isPending && !breakdown.data && <LoadingTable rows={6} columns={6} />}

      {breakdown.data && rows.length === 0 && (
        <EmptyState
          title="Nothing to break down"
          description="No closed trades fall in this range. Widen the dates or pick another account."
        />
      )}

      {rows.length > 0 && (
        <Card padding="md">
          <Stack gap="md">
            <Title order={5}>Net PnL by {labelFor(dimension).toLowerCase()}</Title>

            <BarChart
              h={260}
              data={chartData}
              dataKey="key"
              series={[{ name: 'netProfitLoss', label: 'Net PnL', color: 'indigo.5' }]}
              valueFormatter={(value) => formatMoneyShort(value)}
              yAxisProps={{ width: 80 }}
              xAxisProps={{
                interval: 0,
                angle: chartData.length > 6 ? -25 : 0,
                textAnchor: 'end',
                height: 60,
              }}
              gridAxis="y"
              withLegend={false}
            />

            <Table.ScrollContainer minWidth={640}>
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <SortableHeader
                      label={labelFor(dimension)}
                      field="key"
                      sort={sort}
                      onSort={setSort}
                    />
                    <SortableHeader
                      label="Trades"
                      field="tradeCount"
                      sort={sort}
                      onSort={setSort}
                      align="right"
                    />
                    <SortableHeader
                      label="Wins"
                      field="winCount"
                      sort={sort}
                      onSort={setSort}
                      align="right"
                    />
                    <SortableHeader
                      label="Win rate"
                      field="winRate"
                      sort={sort}
                      onSort={setSort}
                      align="right"
                    />
                    <SortableHeader
                      label="Net PnL"
                      field="netProfitLoss"
                      sort={sort}
                      onSort={setSort}
                      align="right"
                    />
                    <SortableHeader
                      label="Avg R"
                      field="averageR"
                      sort={sort}
                      onSort={setSort}
                      align="right"
                    />
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {rows.map((row) => (
                    <Table.Tr key={row.key}>
                      <Table.Td>
                        <Text size="sm">{row.key}</Text>
                      </Table.Td>
                      <Table.Td ta="right">{formatInteger(row.tradeCount)}</Table.Td>
                      <Table.Td ta="right">{formatInteger(row.winCount)}</Table.Td>
                      <Table.Td ta="right">{formatPercent(row.winRate)}</Table.Td>
                      <Table.Td ta="right">
                        <Pnl value={row.netProfitLoss} />
                      </Table.Td>
                      <Table.Td ta="right">
                        <RMultiple value={row.averageR} />
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          </Stack>
        </Card>
      )}

      <Card padding="md">
        <Stack gap="sm">
          <Group justify="space-between" align="flex-start" wrap="wrap">
            <Title order={5}>What mistakes cost</Title>
            <Text size="xs" c="dimmed">
              Worst first, by the estimated cost recorded against each tag
            </Text>
          </Group>

          {mistakes.isError && (
            <ErrorState error={mistakes.error} onRetry={() => void mistakes.refetch()} />
          )}

          {mistakes.isPending && !mistakes.data && <LoadingTable rows={4} columns={3} />}

          {mistakes.data && mistakes.data.length === 0 && (
            <Text size="sm" c="dimmed">
              No mistakes have been tagged in this range. Tag them while reviewing and the cost
              shows up here.
            </Text>
          )}

          {mistakes.data && mistakes.data.length > 0 && (
            <Table.ScrollContainer minWidth={420}>
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Mistake</Table.Th>
                    <Table.Th ta="right">Occurrences</Table.Th>
                    <Table.Th ta="right">Estimated cost</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {mistakes.data.map((mistake) => (
                    <Table.Tr key={mistake.mistake}>
                      <Table.Td>{mistake.mistake}</Table.Td>
                      <Table.Td ta="right">{formatInteger(mistake.occurrences)}</Table.Td>
                      <Table.Td ta="right">
                        <Money value={mistake.totalCost} />
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          )}
        </Stack>
      </Card>
    </Stack>
  );
}

function SortableHeader({
  label,
  field,
  sort,
  onSort,
  align,
}: {
  label: string;
  field: SortKey;
  sort: { key: SortKey; desc: boolean };
  onSort: (sort: { key: SortKey; desc: boolean }) => void;
  align?: 'right';
}) {
  const active = sort.key === field;

  return (
    <Table.Th ta={align}>
      <UnstyledButton
        onClick={() => onSort({ key: field, desc: active ? !sort.desc : true })}
        style={{ fontSize: 'inherit', fontWeight: 'inherit' }}
      >
        <Group gap={4} justify={align === 'right' ? 'flex-end' : 'flex-start'} wrap="nowrap">
          <span>{label}</span>
          {active ? (
            sort.desc ? (
              <IconArrowDown size={13} />
            ) : (
              <IconArrowUp size={13} />
            )
          ) : (
            <IconArrowsSort size={13} opacity={0.35} />
          )}
        </Group>
      </UnstyledButton>
    </Table.Th>
  );
}

function sortRows(rows: BreakdownRow[], sort: { key: SortKey; desc: boolean }): BreakdownRow[] {
  const sorted = [...rows].sort((a, b) => {
    const left = a[sort.key];
    const right = b[sort.key];

    // A null `averageR` sorts last in either direction: it is "no data", not a
    // low value.
    if (left === null) {
      return 1;
    }

    if (right === null) {
      return -1;
    }

    if (typeof left === 'string' || typeof right === 'string') {
      return String(left).localeCompare(String(right));
    }

    return left - right;
  });

  return sort.desc ? sorted.reverse() : sorted;
}

function labelFor(dimension: BreakdownDimension): string {
  return BREAKDOWN_DIMENSIONS.find((entry) => entry.value === dimension)?.label ?? dimension;
}

/** Axis labels get the compact form; the table shows the full number. */
function formatMoneyShort(value: number): string {
  return new Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 1 }).format(
    value,
  );
}
