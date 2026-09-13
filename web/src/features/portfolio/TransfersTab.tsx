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
import { useAccounts } from '@/api/queries/accounts';
import { useCreateTransfer, useTransfers } from '@/api/queries/portfolio';
import { AccountSelect } from '@/components/Filters';
import { Instant } from '@/components/Instant';
import { Money, Quantity } from '@/components/Money';
import { EmptyState, ErrorState, LoadingTable } from '@/components/States';
import { applyServerErrors, blankToNull, toNumberOrNull } from '@/lib/forms';
import { notifySuccess } from '@/lib/notify';
import type { TransferDirection } from '@/api/types';

const DIRECTIONS: { value: TransferDirection; label: string }[] = [
  { value: 'Deposit', label: 'Deposit' },
  { value: 'Withdrawal', label: 'Withdrawal' },
  { value: 'Internal', label: 'Internal move' },
];

export function TransfersTab() {
  const transfers = useTransfers();
  const accounts = useAccounts();
  const [creating, setCreating] = useState(false);

  const names = new Map((accounts.data ?? []).map((account) => [account.id, account.name]));
  const items = transfers.data ?? [];

  return (
    <Stack gap="md">
      <Group justify="flex-end">
        <Button leftSection={<IconPlus size={16} />} onClick={() => setCreating(true)}>
          New transfer
        </Button>
      </Group>

      {transfers.isError && (
        <ErrorState error={transfers.error} onRetry={() => void transfers.refetch()} />
      )}

      {transfers.isPending && !transfers.data && <LoadingTable rows={5} columns={6} />}

      {transfers.data && items.length === 0 && (
        <EmptyState
          title="No transfers"
          description="Deposits, withdrawals and moves between venues — including the ones that went to the wrong network and never arrived."
          action={
            <Button variant="light" onClick={() => setCreating(true)}>
              Record one
            </Button>
          }
        />
      )}

      {items.length > 0 && (
        <Card padding={0}>
          <Table.ScrollContainer minWidth={860}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>When</Table.Th>
                  <Table.Th>Direction</Table.Th>
                  <Table.Th>From</Table.Th>
                  <Table.Th>To</Table.Th>
                  <Table.Th ta="right">Amount</Table.Th>
                  <Table.Th ta="right">Fee</Table.Th>
                  <Table.Th ta="right">Value</Table.Th>
                  <Table.Th>Note</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {items.map((transfer) => (
                  <Table.Tr key={transfer.id}>
                    <Table.Td>
                      <Instant value={transfer.occurredAt} />
                    </Table.Td>
                    <Table.Td>
                      <Group gap={4} wrap="nowrap">
                        <Badge
                          size="sm"
                          variant="light"
                          color={
                            transfer.direction === 'Deposit'
                              ? 'teal'
                              : transfer.direction === 'Withdrawal'
                                ? 'orange'
                                : 'gray'
                          }
                        >
                          {transfer.direction}
                        </Badge>
                        {/* A write-off is money that left and never arrived. It has
                            to be visible or the equity curve looks like a mystery. */}
                        {transfer.writeOff && (
                          <Badge size="sm" color="red" variant="filled">
                            Write-off
                          </Badge>
                        )}
                      </Group>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm" c={transfer.fromAccountId ? undefined : 'dimmed'}>
                        {transfer.fromAccountId ? (names.get(transfer.fromAccountId) ?? '—') : '—'}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm" c={transfer.toAccountId ? undefined : 'dimmed'}>
                        {transfer.toAccountId ? (names.get(transfer.toAccountId) ?? '—') : '—'}
                      </Text>
                    </Table.Td>
                    <Table.Td ta="right">
                      <Quantity value={transfer.amount} /> {transfer.asset}
                    </Table.Td>
                    <Table.Td ta="right">
                      <Quantity value={transfer.fee} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Money value={transfer.valueUsd} />
                    </Table.Td>
                    <Table.Td>
                      <Text size="xs" c="dimmed" lineClamp={2}>
                        {transfer.note ?? transfer.network ?? '—'}
                      </Text>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Card>
      )}

      <NewTransferModal opened={creating} onClose={() => setCreating(false)} />
    </Stack>
  );
}

function NewTransferModal({ opened, onClose }: { opened: boolean; onClose: () => void }) {
  const create = useCreateTransfer();

  const form = useForm({
    initialValues: {
      direction: 'Deposit' as TransferDirection,
      fromAccountId: '',
      toAccountId: '',
      asset: 'USDT',
      amount: '' as number | string,
      fee: '' as number | string,
      valueUsd: '' as number | string,
      writeOff: false,
      network: '',
      txHash: '',
      counterparty: '',
      note: '',
      occurredAt: new Date().toISOString(),
    },
    validate: {
      asset: (value) => (value.trim() ? null : 'Enter an asset'),
      amount: (value) => (toNumberOrNull(value) ? null : 'Enter an amount'),
    },
  });

  return (
    <Modal opened={opened} onClose={onClose} title="New transfer" size="lg">
      <form
        onSubmit={form.onSubmit((values) =>
          create.mutate(
            {
              direction: values.direction,
              fromAccountId: blankToNull(values.fromAccountId),
              toAccountId: blankToNull(values.toAccountId),
              asset: values.asset.trim().toUpperCase(),
              amount: toNumberOrNull(values.amount) ?? 0,
              fee: toNumberOrNull(values.fee),
              valueUsd: toNumberOrNull(values.valueUsd),
              writeOff: values.writeOff,
              network: blankToNull(values.network),
              txHash: blankToNull(values.txHash),
              counterparty: blankToNull(values.counterparty),
              note: blankToNull(values.note),
              occurredAt: values.occurredAt,
            },
            {
              onSuccess: () => {
                notifySuccess('Transfer recorded.');
                form.reset();
                onClose();
              },
              onError: (error) => applyServerErrors(form, error),
            },
          ),
        )}
      >
        <Stack gap="sm">
          <Grid gap="sm">
            <Grid.Col span={{ base: 12, sm: 6 }}>
              <Select
                label="Direction"
                data={DIRECTIONS}
                allowDeselect={false}
                {...form.getInputProps('direction')}
              />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 6 }}>
              <DateTimePicker
                label="Occurred"
                value={form.values.occurredAt}
                onChange={(value) =>
                  form.setFieldValue('occurredAt', value ?? new Date().toISOString())
                }
              />
            </Grid.Col>

            <Grid.Col span={{ base: 12, sm: 6 }}>
              <AccountSelect
                label="From"
                placeholder="Outside TradeLedger"
                value={form.values.fromAccountId || undefined}
                onChange={(value) => form.setFieldValue('fromAccountId', value ?? '')}
                error={form.errors.fromAccountId as string | undefined}
              />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 6 }}>
              <AccountSelect
                label="To"
                placeholder="Outside TradeLedger"
                value={form.values.toAccountId || undefined}
                onChange={(value) => form.setFieldValue('toAccountId', value ?? '')}
                error={form.errors.toAccountId as string | undefined}
              />
            </Grid.Col>

            <Grid.Col span={{ base: 6, sm: 3 }}>
              <TextInput label="Asset" {...form.getInputProps('asset')} />
            </Grid.Col>
            <Grid.Col span={{ base: 6, sm: 3 }}>
              <NumberInput
                label="Amount"
                hideControls
                decimalScale={12}
                {...form.getInputProps('amount')}
              />
            </Grid.Col>
            <Grid.Col span={{ base: 6, sm: 3 }}>
              <NumberInput
                label="Fee"
                hideControls
                decimalScale={12}
                {...form.getInputProps('fee')}
              />
            </Grid.Col>
            <Grid.Col span={{ base: 6, sm: 3 }}>
              <NumberInput
                label="Value (USD)"
                hideControls
                decimalScale={8}
                {...form.getInputProps('valueUsd')}
              />
            </Grid.Col>

            <Grid.Col span={{ base: 12, sm: 4 }}>
              <TextInput label="Network" placeholder="TON" {...form.getInputProps('network')} />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 4 }}>
              <TextInput label="Counterparty" {...form.getInputProps('counterparty')} />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 4 }}>
              <TextInput label="Tx hash" {...form.getInputProps('txHash')} />
            </Grid.Col>

            <Grid.Col span={12}>
              <Switch
                label="Write-off — this never arrived"
                description="Wrong network, failed bridge, or anything else that left and did not land. Counted as a loss rather than a move."
                {...form.getInputProps('writeOff', { type: 'checkbox' })}
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
              Record transfer
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
