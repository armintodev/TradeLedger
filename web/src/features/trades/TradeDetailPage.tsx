import { useState } from 'react';
import {
  Badge,
  Button,
  Card,
  Grid,
  Group,
  Modal,
  Rating,
  Stack,
  Table,
  Text,
  Title,
} from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { IconArrowLeft, IconEdit, IconTrash } from '@tabler/icons-react';
import { useNavigate, useParams } from 'react-router';
import { useDeleteTrade, useTrade } from '@/api/queries/trades';
import { Instant } from '@/components/Instant';
import { Money, Price, Quantity } from '@/components/Money';
import { EmptyState, ErrorState, LoadingCards } from '@/components/States';
import { notifySuccess } from '@/lib/notify';
import { ApiError } from '@/api/problem';
import { JournalForm } from './JournalForm';
import { TradeFacts } from './TradeFacts';
import type { TradeDetailResponse } from '@/api/types';

export function TradeDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const trade = useTrade(id);
  const remove = useDeleteTrade();

  const [editing, { open: startEditing, close: stopEditing }] = useDisclosure(false);
  const [confirmingDelete, setConfirmingDelete] = useState(false);

  if (trade.isPending) {
    return <LoadingCards count={3} height={140} />;
  }

  if (trade.isError) {
    const missing = trade.error instanceof ApiError && trade.error.status === 404;

    return missing ? (
      <EmptyState
        title="That trade does not exist"
        description="It may have been deleted, or the link is wrong."
        action={
          <Button variant="light" onClick={() => void navigate('/trades')}>
            Back to the journal
          </Button>
        }
      />
    ) : (
      <ErrorState error={trade.error} onRetry={() => void trade.refetch()} />
    );
  }

  const data = trade.data;

  function handleDelete() {
    remove.mutate(data.id, {
      onSuccess: () => {
        notifySuccess('Trade deleted.');
        void navigate('/trades', { replace: true });
      },
      onSettled: () => setConfirmingDelete(false),
    });
  }

  return (
    <Stack gap="md">
      <Group justify="space-between" wrap="wrap" gap="sm">
        <Button
          variant="subtle"
          leftSection={<IconArrowLeft size={16} />}
          onClick={() => void navigate('/trades')}
        >
          Journal
        </Button>

        <Group gap="xs">
          <Button variant="light" leftSection={<IconEdit size={16} />} onClick={startEditing}>
            Edit journal
          </Button>

          {/* Never rendered for a synced trade: the domain refuses to delete one,
              so offering the control at all would be a lie. */}
          {data.origin === 'Manual' && (
            <Button
              variant="light"
              color="red"
              leftSection={<IconTrash size={16} />}
              onClick={() => setConfirmingDelete(true)}
            >
              Delete
            </Button>
          )}
        </Group>
      </Group>

      <TradeFacts trade={data} />

      <Grid gap="md">
        <Grid.Col span={{ base: 12, lg: 7 }}>
          <Executions trade={data} />
        </Grid.Col>

        <Grid.Col span={{ base: 12, lg: 5 }}>
          <Stack gap="md">
            <Subjective trade={data} />
            <Context trade={data} />
            <Terms trade={data} />
          </Stack>
        </Grid.Col>
      </Grid>

      <Modal
        opened={editing}
        onClose={stopEditing}
        title={`Journal — ${data.symbol}`}
        size="xl"
        fullScreen={false}
      >
        <JournalForm
          trade={data}
          mode="edit"
          onCancel={stopEditing}
          onSaved={() => {
            notifySuccess('Journal saved.');
            stopEditing();
          }}
        />
      </Modal>

      <Modal
        opened={confirmingDelete}
        onClose={() => setConfirmingDelete(false)}
        title="Delete this trade?"
        size="sm"
      >
        <Stack gap="md">
          <Text size="sm">
            {data.symbol} opened <Instant value={data.openedAt} /> will be removed permanently.
          </Text>
          <Group justify="flex-end">
            <Button variant="default" onClick={() => setConfirmingDelete(false)}>
              Cancel
            </Button>
            <Button color="red" loading={remove.isPending} onClick={handleDelete}>
              Delete
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  );
}

function Executions({ trade }: { trade: TradeDetailResponse }) {
  return (
    <Card padding="md" h="100%">
      <Stack gap="sm">
        <Group justify="space-between">
          <Title order={5}>Fills</Title>
          <Text size="xs" c="dimmed">
            {trade.executions.length} execution{trade.executions.length === 1 ? '' : 's'}
          </Text>
        </Group>

        {trade.executions.length === 0 ? (
          <Text size="sm" c="dimmed">
            No individual fills were recorded for this trade. The position totals above are still
            correct — a manual entry has no executions.
          </Text>
        ) : (
          <Table.ScrollContainer minWidth={560}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>When</Table.Th>
                  <Table.Th>Role</Table.Th>
                  <Table.Th ta="right">Price</Table.Th>
                  <Table.Th ta="right">Quantity</Table.Th>
                  <Table.Th ta="right">Fee</Table.Th>
                  <Table.Th ta="right">Realised</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {trade.executions.map((execution) => (
                  <Table.Tr key={execution.id}>
                    <Table.Td>
                      <Instant value={execution.executedAt} seconds />
                    </Table.Td>
                    <Table.Td>
                      <Badge
                        size="sm"
                        variant="light"
                        color={execution.role === 'Liquidation' ? 'red' : 'gray'}
                      >
                        {execution.role}
                      </Badge>
                    </Table.Td>
                    <Table.Td ta="right">
                      <Price value={execution.price} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Quantity value={execution.quantity} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Money value={execution.fee} currency={execution.feeAsset} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Money value={execution.realizedProfitLoss} />
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

function Subjective({ trade }: { trade: TradeDetailResponse }) {
  const rows: [string, string | null][] = [
    ['Strategy', trade.strategyName],
    ['Timeframe', trade.timeframeName],
    ['Entry type', trade.entryTypeName],
    ['Exit type', trade.exitTypeName],
    ['Entry mental state', trade.entryMentalStateName],
    ['Exit mental state', trade.exitMentalStateName],
    ['Tag', trade.tag],
    ['Post-trade tag', trade.postTradeTag],
  ];

  return (
    <Card padding="md">
      <Stack gap="sm">
        <Group justify="space-between">
          <Title order={5}>Journal</Title>
          {trade.rating ? <Rating value={trade.rating} readOnly size="sm" /> : null}
        </Group>

        <Stack gap={6}>
          {rows.map(([label, value]) => (
            <Group key={label} justify="space-between" gap="sm" wrap="nowrap">
              <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                {label}
              </Text>
              <Text size="sm" ta="right" c={value ? undefined : 'dimmed'}>
                {value ?? '—'}
              </Text>
            </Group>
          ))}
        </Stack>

        {trade.memo && (
          <Stack gap={4}>
            <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
              Memo
            </Text>
            <Text size="sm" style={{ whiteSpace: 'pre-wrap' }}>
              {trade.memo}
            </Text>
          </Stack>
        )}
      </Stack>
    </Card>
  );
}

function Context({ trade }: { trade: TradeDetailResponse }) {
  const context = trade.marketContext;

  const entries: [string, string | null][] = [
    ['Total2', context?.total2 ?? null],
    ['BTC.D', context?.btcDominance ?? null],
    ['USDT.D', context?.usdtDominance ?? null],
    ['Market trend', context?.marketTrend ?? null],
    ['SMA', context?.sma ?? null],
    ['Session note', context?.marketSession ?? null],
    ['BTC pair', context?.btcPair ?? null],
    ['RSI', context?.rsi ?? null],
    ['Volume', context?.volume ?? null],
    ['Candle shape', context?.candleShape ?? null],
  ];

  const filled = entries.filter(([, value]) => value !== null && value !== '');

  return (
    <Card padding="md">
      <Stack gap="sm">
        <Title order={5}>Market context</Title>

        {filled.length === 0 ? (
          <Text size="sm" c="dimmed">
            The checklist was not filled in for this trade.
          </Text>
        ) : (
          <Grid gap="xs">
            {filled.map(([label, value]) => (
              <Grid.Col key={label} span={6}>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  {label}
                </Text>
                <Text size="sm">{value}</Text>
              </Grid.Col>
            ))}
          </Grid>
        )}
      </Stack>
    </Card>
  );
}

function Terms({ trade }: { trade: TradeDetailResponse }) {
  return (
    <Card padding="md">
      <Stack gap="md">
        <Stack gap="xs">
          <Title order={5}>Mistakes</Title>
          {trade.mistakes.length === 0 ? (
            <Text size="sm" c="dimmed">
              None tagged.
            </Text>
          ) : (
            trade.mistakes.map((mistake) => (
              <Group key={mistake.termId} justify="space-between" gap="sm" wrap="nowrap">
                <Stack gap={0}>
                  <Text size="sm">{mistake.name}</Text>
                  {mistake.note && (
                    <Text size="xs" c="dimmed">
                      {mistake.note}
                    </Text>
                  )}
                </Stack>
                <Money value={mistake.estimatedCost} />
              </Group>
            ))
          )}
        </Stack>

        <Stack gap="xs">
          <Title order={5}>Trackings</Title>
          {trade.trackings.length === 0 ? (
            <Text size="sm" c="dimmed">
              None tagged.
            </Text>
          ) : (
            <Group gap="xs">
              {trade.trackings.map((tracking) => (
                <Badge key={tracking.termId} variant="light" size="sm">
                  {tracking.name}
                </Badge>
              ))}
            </Group>
          )}
        </Stack>
      </Stack>
    </Card>
  );
}
