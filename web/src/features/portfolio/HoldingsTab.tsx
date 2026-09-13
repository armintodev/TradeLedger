import { useState } from 'react';
import {
  Badge,
  Button,
  Card,
  Grid,
  Group,
  Modal,
  NumberInput,
  Select,
  Stack,
  Switch,
  Table,
  Text,
  Textarea,
  TextInput,
} from '@mantine/core';
import { DateTimePicker } from '@mantine/dates';
import { useForm } from '@mantine/form';
import { IconPlus } from '@tabler/icons-react';
import {
  useCloseHolding,
  useCreateHolding,
  useHoldings,
  useRepriceHolding,
} from '@/api/queries/portfolio';
import { AccountSelect } from '@/components/Filters';
import { Instant } from '@/components/Instant';
import { Money, Pnl, Price, Quantity } from '@/components/Money';
import { EmptyState, ErrorState, LoadingTable } from '@/components/States';
import { applyServerErrors, blankToNull, toNumberOrNull } from '@/lib/forms';
import { formatFractionAsPercent } from '@/lib/format';
import { notifySuccess } from '@/lib/notify';
import type { HoldingKind, HoldingResponse } from '@/api/types';

const KINDS: { value: HoldingKind; label: string }[] = [
  { value: 'Spot', label: 'Spot' },
  { value: 'Wallet', label: 'Wallet' },
  { value: 'LiquidityPool', label: 'Liquidity pool' },
  { value: 'Farm', label: 'Farm' },
];

export function HoldingsTab() {
  const [includeClosed, setIncludeClosed] = useState(false);
  const holdings = useHoldings(includeClosed);

  const [creating, setCreating] = useState(false);
  const [repricing, setRepricing] = useState<HoldingResponse | null>(null);
  const [closing, setClosing] = useState<HoldingResponse | null>(null);

  const items = holdings.data ?? [];

  return (
    <Stack gap="md">
      <Group justify="space-between" wrap="wrap" gap="sm">
        <Switch
          label="Include closed"
          checked={includeClosed}
          onChange={(event) => setIncludeClosed(event.currentTarget.checked)}
        />
        <Button leftSection={<IconPlus size={16} />} onClick={() => setCreating(true)}>
          New holding
        </Button>
      </Group>

      {holdings.isError && (
        <ErrorState error={holdings.error} onRetry={() => void holdings.refetch()} />
      )}

      {holdings.isPending && !holdings.data && <LoadingTable rows={5} columns={6} />}

      {holdings.data && items.length === 0 && (
        <EmptyState
          title="No holdings"
          description="Spot bags, wallet balances and LP or farm positions live here — everything that is not a round-trip trade."
          action={
            <Button variant="light" onClick={() => setCreating(true)}>
              Add one
            </Button>
          }
        />
      )}

      {items.length > 0 && (
        <Card padding={0}>
          <Table.ScrollContainer minWidth={900}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Asset</Table.Th>
                  <Table.Th>Kind</Table.Th>
                  <Table.Th ta="right">Quantity</Table.Th>
                  <Table.Th ta="right">Entry</Table.Th>
                  <Table.Th ta="right">Current</Table.Th>
                  <Table.Th ta="right">Change</Table.Th>
                  <Table.Th>Priced</Table.Th>
                  <Table.Th />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {items.map((holding) => (
                  <Table.Tr key={holding.id}>
                    <Table.Td>
                      <Text size="sm" fw={600}>
                        {holding.asset}
                      </Text>
                      {holding.poolName && (
                        <Text size="xs" c="dimmed">
                          {holding.poolName}
                          {holding.farmApr !== null
                            ? ` · ${formatFractionAsPercent(holding.farmApr)} APR`
                            : ''}
                        </Text>
                      )}
                    </Table.Td>
                    <Table.Td>
                      <Group gap={4} wrap="nowrap">
                        <Badge size="sm" variant="light">
                          {KINDS.find((kind) => kind.value === holding.kind)?.label ?? holding.kind}
                        </Badge>
                        {holding.isFarmed && (
                          <Badge size="sm" variant="dot" color="teal">
                            Farmed
                          </Badge>
                        )}
                        {holding.closedAt && (
                          <Badge size="sm" variant="light" color="gray">
                            Closed
                          </Badge>
                        )}
                      </Group>
                    </Table.Td>
                    <Table.Td ta="right">
                      <Quantity value={holding.quantity} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Money value={holding.entryValueUsd} />
                      {holding.averageEntryPrice !== null && (
                        <Text size="xs" c="dimmed">
                          @ <Price value={holding.averageEntryPrice} size="xs" />
                        </Text>
                      )}
                    </Table.Td>
                    <Table.Td ta="right">
                      <Money value={holding.currentValueUsd} />
                      {holding.currentPrice !== null && (
                        <Text size="xs" c="dimmed">
                          @ <Price value={holding.currentPrice} size="xs" />
                        </Text>
                      )}
                    </Table.Td>
                    <Table.Td ta="right">
                      <Pnl
                        value={
                          holding.currentValueUsd !== null && holding.entryValueUsd !== null
                            ? holding.currentValueUsd - holding.entryValueUsd
                            : null
                        }
                      />
                    </Table.Td>
                    <Table.Td>
                      <Instant value={holding.pricedAt} dateOnly />
                    </Table.Td>
                    <Table.Td>
                      <Group gap={4} justify="flex-end" wrap="nowrap">
                        <Button
                          size="compact-xs"
                          variant="light"
                          onClick={() => setRepricing(holding)}
                        >
                          Reprice
                        </Button>
                        {!holding.closedAt && (
                          <Button
                            size="compact-xs"
                            variant="subtle"
                            color="red"
                            onClick={() => setClosing(holding)}
                          >
                            Close
                          </Button>
                        )}
                      </Group>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Card>
      )}

      <NewHoldingModal opened={creating} onClose={() => setCreating(false)} />
      <RepriceModal holding={repricing} onClose={() => setRepricing(null)} />
      <CloseModal holding={closing} onClose={() => setClosing(null)} />
    </Stack>
  );
}

function NewHoldingModal({ opened, onClose }: { opened: boolean; onClose: () => void }) {
  const create = useCreateHolding();

  const form = useForm({
    initialValues: {
      accountId: '',
      kind: 'Spot' as HoldingKind,
      asset: '',
      quantity: '' as number | string,
      averageEntryPrice: '' as number | string,
      entryValueUsd: '' as number | string,
      poolName: '',
      farmApr: '' as number | string,
      isFarmed: false,
      openedAt: new Date().toISOString(),
      note: '',
    },
    validate: {
      accountId: (value) => (value ? null : 'Pick an account'),
      asset: (value) => (value.trim() ? null : 'Enter an asset'),
      quantity: (value) => (toNumberOrNull(value) ? null : 'Enter a quantity'),
    },
  });

  const isPooled = form.values.kind === 'LiquidityPool' || form.values.kind === 'Farm';

  return (
    <Modal opened={opened} onClose={onClose} title="New holding" size="lg">
      <form
        onSubmit={form.onSubmit((values) =>
          create.mutate(
            {
              accountId: values.accountId,
              kind: values.kind,
              asset: values.asset.trim().toUpperCase(),
              quantity: toNumberOrNull(values.quantity) ?? 0,
              averageEntryPrice: toNumberOrNull(values.averageEntryPrice),
              entryValueUsd: toNumberOrNull(values.entryValueUsd),
              poolName: blankToNull(values.poolName),
              farmApr: toNumberOrNull(values.farmApr),
              isFarmed: values.isFarmed,
              openedAt: values.openedAt,
              note: blankToNull(values.note),
            },
            {
              onSuccess: () => {
                notifySuccess('Holding added.');
                form.reset();
                onClose();
              },
              onError: (error) => applyServerErrors(form, error),
            },
          ),
        )}
      >
        <Stack gap="sm">
          <AccountSelect
            value={form.values.accountId || undefined}
            onChange={(value) => form.setFieldValue('accountId', value ?? '')}
            placeholder="Where it lives"
            required
            error={form.errors.accountId as string | undefined}
          />

          <Grid gap="sm">
            <Grid.Col span={{ base: 12, sm: 6 }}>
              <Select
                label="Kind"
                data={KINDS}
                allowDeselect={false}
                {...form.getInputProps('kind')}
              />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 6 }}>
              <TextInput label="Asset" placeholder="TON" {...form.getInputProps('asset')} />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 4 }}>
              <NumberInput
                label="Quantity"
                hideControls
                decimalScale={12}
                {...form.getInputProps('quantity')}
              />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 4 }}>
              <NumberInput
                label="Average entry price"
                hideControls
                decimalScale={12}
                {...form.getInputProps('averageEntryPrice')}
              />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 4 }}>
              <NumberInput
                label="Entry value (USD)"
                hideControls
                decimalScale={8}
                {...form.getInputProps('entryValueUsd')}
              />
            </Grid.Col>

            {isPooled && (
              <>
                <Grid.Col span={{ base: 12, sm: 6 }}>
                  <TextInput
                    label="Pool"
                    placeholder="TON/USDT"
                    {...form.getInputProps('poolName')}
                  />
                </Grid.Col>
                <Grid.Col span={{ base: 12, sm: 3 }}>
                  <NumberInput
                    label="APR"
                    description="As a fraction: 0.25 is 25%"
                    hideControls
                    decimalScale={6}
                    {...form.getInputProps('farmApr')}
                  />
                </Grid.Col>
                <Grid.Col span={{ base: 12, sm: 3 }}>
                  <Switch
                    label="Farmed"
                    mt="xl"
                    {...form.getInputProps('isFarmed', { type: 'checkbox' })}
                  />
                </Grid.Col>
              </>
            )}

            <Grid.Col span={12}>
              <DateTimePicker
                label="Opened"
                value={form.values.openedAt}
                onChange={(value) =>
                  form.setFieldValue('openedAt', value ?? new Date().toISOString())
                }
              />
            </Grid.Col>

            <Grid.Col span={12}>
              <Textarea label="Note" autosize minRows={2} {...form.getInputProps('note')} />
            </Grid.Col>
          </Grid>

          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={create.isPending}>
              Add holding
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}

function RepriceModal({
  holding,
  onClose,
}: {
  holding: HoldingResponse | null;
  onClose: () => void;
}) {
  const reprice = useRepriceHolding();

  const form = useForm({
    initialValues: { price: '' as number | string, pricedAt: new Date().toISOString() },
    validate: { price: (value) => (toNumberOrNull(value) ? null : 'Enter a price') },
  });

  return (
    <Modal
      opened={holding !== null}
      onClose={onClose}
      title={`Reprice ${holding?.asset ?? ''}`}
      size="sm"
    >
      <form
        onSubmit={form.onSubmit((values) => {
          if (!holding) {
            return;
          }

          reprice.mutate(
            {
              id: holding.id,
              body: { price: toNumberOrNull(values.price) ?? 0, pricedAt: values.pricedAt },
            },
            {
              onSuccess: () => {
                notifySuccess(`${holding.asset} repriced.`);
                form.reset();
                onClose();
              },
              onError: (error) => applyServerErrors(form, error),
            },
          );
        })}
      >
        <Stack gap="sm">
          <NumberInput
            label="Price"
            hideControls
            decimalScale={12}
            {...form.getInputProps('price')}
          />
          <DateTimePicker
            label="Priced at"
            value={form.values.pricedAt}
            onChange={(value) => form.setFieldValue('pricedAt', value ?? new Date().toISOString())}
          />
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={reprice.isPending}>
              Reprice
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}

function CloseModal({
  holding,
  onClose,
}: {
  holding: HoldingResponse | null;
  onClose: () => void;
}) {
  const close = useCloseHolding();
  const [closedAt, setClosedAt] = useState<string>(new Date().toISOString());

  return (
    <Modal
      opened={holding !== null}
      onClose={onClose}
      title={`Close ${holding?.asset ?? ''}`}
      size="sm"
    >
      <Stack gap="sm">
        <Text size="sm" c="dimmed">
          Closing keeps the holding in the record; it stops counting towards the current portfolio.
        </Text>

        <DateTimePicker
          label="Closed at"
          value={closedAt}
          onChange={(value) => setClosedAt(value ?? new Date().toISOString())}
        />

        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>
            Cancel
          </Button>
          <Button
            color="red"
            loading={close.isPending}
            onClick={() => {
              if (!holding) {
                return;
              }

              close.mutate(
                { id: holding.id, body: { closedAt } },
                {
                  onSuccess: () => {
                    notifySuccess(`${holding.asset} closed.`);
                    onClose();
                  },
                },
              );
            }}
          >
            Close holding
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
