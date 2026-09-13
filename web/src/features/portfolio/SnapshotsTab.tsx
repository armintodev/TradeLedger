import { Alert, Button, Card, Grid, Group, NumberInput, Stack, Text, Title } from '@mantine/core';
import { DateTimePicker } from '@mantine/dates';
import { useForm } from '@mantine/form';
import { IconInfoCircle } from '@tabler/icons-react';
import { useNavigate } from 'react-router';
import { useCreateSnapshot } from '@/api/queries/portfolio';
import { AccountSelect } from '@/components/Filters';
import { applyServerErrors, blankToNull, toNumberOrNull } from '@/lib/forms';
import { notifySuccess } from '@/lib/notify';
import { TextInput } from '@mantine/core';

/**
 * Snapshots are the sole source of the equity curve — equity is never
 * recomputed by summing trades. There is no list endpoint for them, so this tab
 * records and the dashboard displays.
 */
export function SnapshotsTab() {
  const navigate = useNavigate();
  const create = useCreateSnapshot();

  const form = useForm({
    initialValues: {
      accountId: '',
      asset: 'USDT',
      walletBalance: '' as number | string,
      available: '' as number | string,
      unrealizedPnl: '' as number | string,
      capturedAt: new Date().toISOString(),
    },
    validate: {
      accountId: (value) => (value ? null : 'Pick an account'),
      walletBalance: (value) =>
        toNumberOrNull(value) !== null ? null : 'Enter the wallet balance',
    },
  });

  return (
    <Stack gap="md">
      <Alert icon={<IconInfoCircle size={18} />} color="blue" variant="light">
        <Stack gap="xs" align="flex-start">
          <Text size="sm">
            The equity curve is the view of your snapshots — the API records them but does not list
            them back. A Bitunix account can capture one automatically from Settings → Sync; this
            form is for the venues no API reaches.
          </Text>
          <Button size="xs" variant="light" onClick={() => void navigate('/')}>
            Open the equity curve
          </Button>
        </Stack>
      </Alert>

      <Card padding="md" maw={640}>
        <form
          onSubmit={form.onSubmit((values) =>
            create.mutate(
              {
                accountId: values.accountId,
                asset: blankToNull(values.asset),
                walletBalance: toNumberOrNull(values.walletBalance) ?? 0,
                available: toNumberOrNull(values.available),
                unrealizedPnl: toNumberOrNull(values.unrealizedPnl),
                capturedAt: values.capturedAt,
              },
              {
                onSuccess: () => {
                  notifySuccess('Snapshot recorded.');
                  form.setFieldValue('walletBalance', '');
                  form.setFieldValue('available', '');
                  form.setFieldValue('unrealizedPnl', '');
                },
                onError: (error) => applyServerErrors(form, error),
              },
            ),
          )}
        >
          <Stack gap="sm">
            <Title order={5}>Record a snapshot</Title>

            <AccountSelect
              value={form.values.accountId || undefined}
              onChange={(value) => form.setFieldValue('accountId', value ?? '')}
              placeholder="Which account"
              required
              error={form.errors.accountId as string | undefined}
            />

            <Grid gap="sm">
              <Grid.Col span={{ base: 12, sm: 6 }}>
                <TextInput label="Asset" {...form.getInputProps('asset')} />
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 6 }}>
                <DateTimePicker
                  label="Captured at"
                  value={form.values.capturedAt}
                  onChange={(value) =>
                    form.setFieldValue('capturedAt', value ?? new Date().toISOString())
                  }
                />
              </Grid.Col>

              <Grid.Col span={{ base: 12, sm: 4 }}>
                <NumberInput
                  label="Wallet balance"
                  hideControls
                  decimalScale={8}
                  {...form.getInputProps('walletBalance')}
                />
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 4 }}>
                <NumberInput
                  label="Available"
                  hideControls
                  decimalScale={8}
                  {...form.getInputProps('available')}
                />
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 4 }}>
                <NumberInput
                  label="Unrealised PnL"
                  hideControls
                  decimalScale={8}
                  {...form.getInputProps('unrealizedPnl')}
                />
              </Grid.Col>
            </Grid>

            <Group justify="flex-end">
              <Button type="submit" loading={create.isPending}>
                Record snapshot
              </Button>
            </Group>
          </Stack>
        </form>
      </Card>
    </Stack>
  );
}
