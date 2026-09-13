import { useState } from 'react';
import {
  Alert,
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
  Tooltip,
} from '@mantine/core';
import { IconAlertTriangle, IconPlus } from '@tabler/icons-react';
import {
  useBacktestAccounts,
  useCreateBacktestAccount,
  useDeleteBacktestAccount,
  useUpdateBacktestAccount,
} from '@/api/queries/backtests';
import { ApiError } from '@/api/problem';
import { Instant } from '@/components/Instant';
import { Money, Pnl } from '@/components/Money';
import { EmptyState, ErrorState, LoadingTable } from '@/components/States';
import { applyServerErrors, blankToNull, toNumberOrNull } from '@/lib/forms';
import { notifySuccess } from '@/lib/notify';
import { useForm } from '@mantine/form';
import type { BacktestAccountMode, BacktestAccountResponse } from '@/api/types';
import { AccountDetailDrawer } from './AccountDetailDrawer';

const MODES: { value: BacktestAccountMode; label: string }[] = [
  { value: 'Sequential', label: 'Sequential — runs compound' },
  { value: 'Independent', label: 'Independent — each run starts fresh' },
];

export function AccountsTab() {
  const [includeInactive, setIncludeInactive] = useState(false);
  const accounts = useBacktestAccounts(includeInactive);

  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<BacktestAccountResponse | null>(null);
  const [deleting, setDeleting] = useState<BacktestAccountResponse | null>(null);
  const [viewing, setViewing] = useState<BacktestAccountResponse | null>(null);

  const items = accounts.data ?? [];

  return (
    <Stack gap="md">
      <Group justify="space-between" wrap="wrap" gap="sm">
        <Switch
          label="Include archived"
          checked={includeInactive}
          onChange={(event) => setIncludeInactive(event.currentTarget.checked)}
        />
        <Button leftSection={<IconPlus size={16} />} onClick={() => setCreating(true)}>
          New account
        </Button>
      </Group>

      {accounts.isError && (
        <ErrorState error={accounts.error} onRetry={() => void accounts.refetch()} />
      )}

      {accounts.isPending && !accounts.data && <LoadingTable rows={3} columns={6} />}

      {accounts.data && items.length === 0 && (
        <EmptyState
          title="No backtest accounts"
          description="An account is a simulated balance that runs compound onto, so one strategy over hundreds of trades reads as a single curve rather than a pile of unrelated runs. It is entirely separate from your real accounts."
          action={
            <Button variant="light" onClick={() => setCreating(true)}>
              Create one
            </Button>
          }
        />
      )}

      {items.length > 0 && (
        <Card padding={0}>
          <Table.ScrollContainer minWidth={820}>
            <Table highlightOnHover>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Name</Table.Th>
                  <Table.Th>Mode</Table.Th>
                  <Table.Th ta="right">Starting</Table.Th>
                  <Table.Th ta="right">Current</Table.Th>
                  <Table.Th ta="right">Net</Table.Th>
                  <Table.Th ta="right">Runs</Table.Th>
                  <Table.Th>Created</Table.Th>
                  <Table.Th />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {items.map((account) => (
                  <Table.Tr
                    key={account.id}
                    style={{ cursor: 'pointer' }}
                    onClick={() => setViewing(account)}
                  >
                    <Table.Td>
                      <Group gap={6} wrap="nowrap">
                        <Text size="sm" fw={600}>
                          {account.name}
                        </Text>
                        {!account.isActive && (
                          <Badge size="sm" color="gray" variant="light">
                            Archived
                          </Badge>
                        )}
                      </Group>
                      {account.description && (
                        <Text size="xs" c="dimmed" lineClamp={1}>
                          {account.description}
                        </Text>
                      )}
                    </Table.Td>
                    <Table.Td>
                      <Badge
                        size="sm"
                        variant="light"
                        color={account.mode === 'Sequential' ? 'indigo' : 'gray'}
                      >
                        {account.mode}
                      </Badge>
                    </Table.Td>
                    <Table.Td ta="right">
                      <Money value={account.startingBalance} currency={account.currency} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Money value={account.currentBalance} currency={account.currency} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Pnl value={account.netProfitLoss} />
                    </Table.Td>
                    <Table.Td ta="right">{account.succeededRuns}</Table.Td>
                    <Table.Td>
                      <Instant value={account.createdAt} dateOnly />
                    </Table.Td>
                    <Table.Td onClick={(event) => event.stopPropagation()}>
                      <Group gap={4} justify="flex-end" wrap="nowrap">
                        <Button
                          size="compact-xs"
                          variant="light"
                          onClick={() => setEditing(account)}
                        >
                          Edit
                        </Button>
                        <Button
                          size="compact-xs"
                          variant="subtle"
                          color="red"
                          onClick={() => setDeleting(account)}
                        >
                          Delete
                        </Button>
                      </Group>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Card>
      )}

      <CreateAccountModal opened={creating} onClose={() => setCreating(false)} />
      <EditAccountModal account={editing} onClose={() => setEditing(null)} />
      <DeleteAccountModal account={deleting} onClose={() => setDeleting(null)} />
      <AccountDetailDrawer account={viewing} onClose={() => setViewing(null)} />
    </Stack>
  );
}

function CreateAccountModal({ opened, onClose }: { opened: boolean; onClose: () => void }) {
  const create = useCreateBacktestAccount();

  const form = useForm({
    initialValues: {
      name: '',
      startingBalance: 10000 as number | string,
      description: '',
      currency: 'USDT',
      mode: 'Sequential' as BacktestAccountMode,
    },
    validate: {
      name: (value) => (value.trim() ? null : 'Name the account'),
      startingBalance: (value) =>
        (toNumberOrNull(value) ?? 0) > 0 ? null : 'Starting balance must be greater than zero',
    },
  });

  return (
    <Modal opened={opened} onClose={onClose} title="New backtest account" size="md">
      <form
        onSubmit={form.onSubmit((values) =>
          create.mutate(
            {
              name: values.name.trim(),
              startingBalance: toNumberOrNull(values.startingBalance) ?? 0,
              description: blankToNull(values.description),
              currency: blankToNull(values.currency),
              mode: values.mode,
            },
            {
              onSuccess: () => {
                notifySuccess('Backtest account created.');
                form.reset();
                onClose();
              },
              onError: (error) => applyServerErrors(form, error),
            },
          ),
        )}
      >
        <Stack gap="sm">
          <TextInput label="Name" placeholder="EMA cross — 4h" {...form.getInputProps('name')} />

          <Grid gap="sm">
            <Grid.Col span={{ base: 12, sm: 8 }}>
              <NumberInput
                label="Starting balance"
                hideControls
                decimalScale={8}
                {...form.getInputProps('startingBalance')}
              />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 4 }}>
              <TextInput label="Currency" {...form.getInputProps('currency')} />
            </Grid.Col>
          </Grid>

          <Select
            label="Mode"
            description="Sequential cannot be switched back on once the account has runs."
            data={MODES}
            allowDeselect={false}
            {...form.getInputProps('mode')}
          />

          <Textarea
            label="Description"
            autosize
            minRows={2}
            {...form.getInputProps('description')}
          />

          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={create.isPending}>
              Create account
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}

function EditAccountModal({
  account,
  onClose,
}: {
  account: BacktestAccountResponse | null;
  onClose: () => void;
}) {
  const update = useUpdateBacktestAccount();
  const [conflict, setConflict] = useState<string | null>(null);

  const form = useForm({
    initialValues: {
      name: account?.name ?? '',
      description: account?.description ?? '',
      mode: account?.mode ?? ('Sequential' as BacktestAccountMode),
      isActive: account?.isActive ?? true,
    },
  });

  // Re-seed whenever a different account opens the modal.
  const [seeded, setSeeded] = useState<string | null>(null);

  if (account && seeded !== account.id) {
    setSeeded(account.id);
    setConflict(null);
    form.setValues({
      name: account.name,
      description: account.description ?? '',
      mode: account.mode,
      isActive: account.isActive,
    });
  }

  // The domain refuses a switch *to* sequential once any run exists, in any
  // status. The response only reports succeeded runs, so this catches the clear
  // case and the 409 below catches the rest.
  const lockedToIndependent = (account?.succeededRuns ?? 0) > 0 && account?.mode === 'Independent';

  return (
    <Modal
      opened={account !== null}
      onClose={onClose}
      title={`Edit ${account?.name ?? ''}`}
      size="md"
    >
      <form
        onSubmit={form.onSubmit((values) => {
          if (!account) {
            return;
          }

          setConflict(null);

          update.mutate(
            {
              id: account.id,
              body: {
                name: values.name.trim(),
                description: values.description,
                mode: values.mode,
                isActive: values.isActive,
              },
            },
            {
              onSuccess: () => {
                notifySuccess('Account updated.');
                onClose();
              },
              onError: (error) => {
                if (error instanceof ApiError && error.code === 'cannot_switch_to_sequential') {
                  setConflict(error.detail ?? error.title);
                  form.setFieldValue('mode', account.mode);
                  return;
                }

                applyServerErrors(form, error);
              },
            },
          );
        })}
      >
        <Stack gap="sm">
          {conflict && (
            <Alert color="orange" icon={<IconAlertTriangle size={18} />}>
              {conflict}
            </Alert>
          )}

          <TextInput label="Name" {...form.getInputProps('name')} />
          <Textarea
            label="Description"
            autosize
            minRows={2}
            {...form.getInputProps('description')}
          />

          <Tooltip
            label="This account already has runs. Chaining them would compound the same market twice, so it cannot become sequential."
            disabled={!lockedToIndependent}
            withArrow
            multiline
            w={280}
          >
            <Select
              label="Mode"
              data={MODES}
              allowDeselect={false}
              disabled={lockedToIndependent}
              {...form.getInputProps('mode')}
            />
          </Tooltip>

          <Switch
            label="Active"
            description="An archived account will not accept new runs."
            {...form.getInputProps('isActive', { type: 'checkbox' })}
          />

          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={update.isPending}>
              Save
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}

/**
 * Deleting cascades to every run, trade, execution and equity point. The API
 * refuses the first attempt with a 409 carrying the run count, which is exactly
 * the confirmation prompt — so the message is shown rather than invented.
 */
function DeleteAccountModal({
  account,
  onClose,
}: {
  account: BacktestAccountResponse | null;
  onClose: () => void;
}) {
  const remove = useDeleteBacktestAccount();
  const [warning, setWarning] = useState<string | null>(null);

  function close() {
    setWarning(null);
    onClose();
  }

  function attempt(confirm: boolean) {
    if (!account) {
      return;
    }

    remove.mutate(
      { id: account.id, confirm },
      {
        onSuccess: () => {
          notifySuccess(`${account.name} deleted.`);
          close();
        },
        onError: (error) => {
          if (error instanceof ApiError && error.code === 'account_still_holds_runs') {
            setWarning(error.detail ?? error.title);
          }
        },
      },
    );
  }

  return (
    <Modal opened={account !== null} onClose={close} title="Delete this account?" size="md">
      <Stack gap="md">
        {warning ? (
          <Alert color="red" icon={<IconAlertTriangle size={18} />}>
            {warning}
          </Alert>
        ) : (
          <Text size="sm">{account?.name} and everything on it will be removed permanently.</Text>
        )}

        <Group justify="flex-end">
          <Button variant="default" onClick={close}>
            Cancel
          </Button>
          <Button color="red" loading={remove.isPending} onClick={() => attempt(warning !== null)}>
            {warning ? 'Delete it anyway' : 'Delete'}
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
