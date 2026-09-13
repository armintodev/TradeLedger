import {
  Alert,
  Button,
  Card,
  Grid,
  Group,
  NumberInput,
  Select,
  Stack,
  Textarea,
  TextInput,
  Title,
} from '@mantine/core';
import { DateTimePicker } from '@mantine/dates';
import { useForm } from '@mantine/form';
import { IconArrowLeft, IconInfoCircle } from '@tabler/icons-react';
import { useNavigate } from 'react-router';
import { useCreateManualTrade } from '@/api/queries/trades';
import { toOptions, useTaxonomy } from '@/api/queries/taxonomy';
import { AccountSelect } from '@/components/Filters';
import { PageHeader } from '@/components/PageHeader';
import { applyServerErrors, blankToNull, toNumberOrNull } from '@/lib/forms';
import { notifySuccess } from '@/lib/notify';
import type { TradeSide } from '@/api/types';

/**
 * For the venues no API reaches: external wallets, other exchanges, on-chain
 * swaps. Gross PnL, outcome, achieved R, market session and duration are all
 * derived server-side from what is entered here, exactly as they are for a
 * synced trade.
 */
export function NewTradePage() {
  const navigate = useNavigate();
  const taxonomy = useTaxonomy();
  const create = useCreateManualTrade();

  const form = useForm({
    initialValues: {
      accountId: '',
      symbol: '',
      side: 'Long' as TradeSide,
      openedAt: new Date().toISOString(),
      closedAt: null as string | null,
      entryPrice: '' as number | string,
      exitPrice: '' as number | string,
      quantity: '' as number | string,
      leverage: 1 as number | string,
      positionMargin: '' as number | string,
      fees: '' as number | string,
      funding: '' as number | string,
      stopLossPrice: '' as number | string,
      takeProfitPrice: '' as number | string,
      strategyId: null as string | null,
      memo: '',
    },
    validate: {
      accountId: (value) => (value ? null : 'Pick an account'),
      symbol: (value) => (value.trim() ? null : 'Enter a symbol'),
      entryPrice: (value) => (toNumberOrNull(value) ? null : 'Enter an entry price'),
      quantity: (value) => (toNumberOrNull(value) ? null : 'Enter a quantity'),
    },
  });

  return (
    <Stack gap="md">
      <Group>
        <Button
          variant="subtle"
          leftSection={<IconArrowLeft size={16} />}
          onClick={() => void navigate('/trades')}
        >
          Journal
        </Button>
      </Group>

      <PageHeader
        title="Manual trade"
        description="One journal row for a position that no sync will ever see."
      />

      <Alert icon={<IconInfoCircle size={18} />} color="gray" variant="light">
        Leave the exit blank to record a position that is still open. A stop or target on the wrong
        side of the entry is rejected rather than silently producing a nonsense R multiple.
      </Alert>

      <Card padding="md" maw={860}>
        <form
          onSubmit={form.onSubmit((values) =>
            create.mutate(
              {
                accountId: values.accountId,
                symbol: values.symbol.trim().toUpperCase(),
                side: values.side,
                openedAt: values.openedAt,
                closedAt: values.closedAt,
                entryPrice: toNumberOrNull(values.entryPrice) ?? 0,
                exitPrice: toNumberOrNull(values.exitPrice),
                quantity: toNumberOrNull(values.quantity) ?? 0,
                leverage: toNumberOrNull(values.leverage),
                positionMargin: toNumberOrNull(values.positionMargin),
                fees: toNumberOrNull(values.fees),
                funding: toNumberOrNull(values.funding),
                stopLossPrice: toNumberOrNull(values.stopLossPrice),
                takeProfitPrice: toNumberOrNull(values.takeProfitPrice),
                strategyId: values.strategyId,
                memo: blankToNull(values.memo),
              },
              {
                onSuccess: (trade) => {
                  notifySuccess(`${trade.symbol} recorded.`);
                  void navigate(`/trades/${trade.id}`, { replace: true });
                },
                onError: (error) => applyServerErrors(form, error),
              },
            ),
          )}
        >
          <Stack gap="sm">
            <Title order={5}>The position</Title>

            <Grid gap="sm">
              <Grid.Col span={{ base: 12, sm: 6 }}>
                <AccountSelect
                  value={form.values.accountId || undefined}
                  onChange={(value) => form.setFieldValue('accountId', value ?? '')}
                  placeholder="Where it happened"
                  required
                  error={form.errors.accountId as string | undefined}
                />
              </Grid.Col>
              <Grid.Col span={{ base: 6, sm: 3 }}>
                <TextInput label="Symbol" placeholder="BTCUSDT" {...form.getInputProps('symbol')} />
              </Grid.Col>
              <Grid.Col span={{ base: 6, sm: 3 }}>
                <Select
                  label="Side"
                  data={[
                    { value: 'Long', label: 'Long' },
                    { value: 'Short', label: 'Short' },
                  ]}
                  allowDeselect={false}
                  {...form.getInputProps('side')}
                />
              </Grid.Col>

              <Grid.Col span={{ base: 12, sm: 6 }}>
                <DateTimePicker
                  label="Opened"
                  value={form.values.openedAt}
                  onChange={(value) =>
                    form.setFieldValue('openedAt', value ?? new Date().toISOString())
                  }
                />
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 6 }}>
                <DateTimePicker
                  label="Closed"
                  description="Blank leaves the position open"
                  clearable
                  value={form.values.closedAt}
                  onChange={(value) => form.setFieldValue('closedAt', value ?? null)}
                />
              </Grid.Col>

              <Grid.Col span={{ base: 6, sm: 3 }}>
                <NumberInput
                  label="Entry"
                  hideControls
                  decimalScale={12}
                  {...form.getInputProps('entryPrice')}
                />
              </Grid.Col>
              <Grid.Col span={{ base: 6, sm: 3 }}>
                <NumberInput
                  label="Exit"
                  hideControls
                  decimalScale={12}
                  {...form.getInputProps('exitPrice')}
                />
              </Grid.Col>
              <Grid.Col span={{ base: 6, sm: 3 }}>
                <NumberInput
                  label="Quantity"
                  hideControls
                  decimalScale={12}
                  {...form.getInputProps('quantity')}
                />
              </Grid.Col>
              <Grid.Col span={{ base: 6, sm: 3 }}>
                <NumberInput
                  label="Leverage"
                  hideControls
                  min={1}
                  {...form.getInputProps('leverage')}
                />
              </Grid.Col>

              <Grid.Col span={{ base: 6, sm: 3 }}>
                <NumberInput
                  label="Margin"
                  hideControls
                  decimalScale={8}
                  {...form.getInputProps('positionMargin')}
                />
              </Grid.Col>
              <Grid.Col span={{ base: 6, sm: 3 }}>
                <NumberInput
                  label="Fees"
                  hideControls
                  decimalScale={8}
                  {...form.getInputProps('fees')}
                />
              </Grid.Col>
              <Grid.Col span={{ base: 6, sm: 3 }}>
                <NumberInput
                  label="Funding"
                  hideControls
                  decimalScale={8}
                  {...form.getInputProps('funding')}
                />
              </Grid.Col>
              <Grid.Col span={{ base: 6, sm: 3 }}>
                <Select
                  label="Strategy"
                  data={toOptions(taxonomy.groups.Strategy)}
                  searchable
                  clearable
                  {...form.getInputProps('strategyId')}
                />
              </Grid.Col>

              <Grid.Col span={{ base: 6, sm: 3 }}>
                <NumberInput
                  label="Stop loss"
                  hideControls
                  decimalScale={12}
                  {...form.getInputProps('stopLossPrice')}
                />
              </Grid.Col>
              <Grid.Col span={{ base: 6, sm: 3 }}>
                <NumberInput
                  label="Take profit"
                  hideControls
                  decimalScale={12}
                  {...form.getInputProps('takeProfitPrice')}
                />
              </Grid.Col>

              <Grid.Col span={12}>
                <Textarea label="Memo" autosize minRows={2} {...form.getInputProps('memo')} />
              </Grid.Col>
            </Grid>

            <Group justify="flex-end">
              <Button variant="default" onClick={() => void navigate('/trades')}>
                Cancel
              </Button>
              <Button type="submit" loading={create.isPending}>
                Record trade
              </Button>
            </Group>
          </Stack>
        </form>
      </Card>
    </Stack>
  );
}
