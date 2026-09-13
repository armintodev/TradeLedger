import { useState } from 'react';
import {
  Alert,
  Badge,
  Button,
  Card,
  Grid,
  Group,
  Modal,
  PasswordInput,
  Select,
  Stack,
  Table,
  Text,
  TextInput,
} from '@mantine/core';
import { DateTimePicker } from '@mantine/dates';
import { useForm } from '@mantine/form';
import { IconAlertTriangle, IconKey, IconPlus, IconShieldLock } from '@tabler/icons-react';
import {
  useAccounts,
  useCreateAccount,
  useRemoveCredentials,
  useSetCredentials,
} from '@/api/queries/accounts';
import { Instant } from '@/components/Instant';
import { EmptyState, ErrorState, LoadingTable } from '@/components/States';
import { ApiError } from '@/api/problem';
import { applyServerErrors, blankToNull } from '@/lib/forms';
import { notifySuccess } from '@/lib/notify';
import type { AccountKind, AccountResponse, SyncMode, Venue } from '@/api/types';

const KINDS: { value: AccountKind; label: string }[] = [
  { value: 'ExchangeFutures', label: 'Exchange — futures' },
  { value: 'ExchangeSpot', label: 'Exchange — spot' },
  { value: 'ExchangeWallet', label: 'Exchange — wallet' },
  { value: 'ExternalWallet', label: 'External wallet' },
  { value: 'ManualVenue', label: 'Manual venue' },
];

export function AccountsTab({ onOpenProxy }: { onOpenProxy: () => void }) {
  const accounts = useAccounts();
  const [creating, setCreating] = useState(false);
  const [credentialing, setCredentialing] = useState<AccountResponse | null>(null);
  const [removing, setRemoving] = useState<AccountResponse | null>(null);
  const removeCredentials = useRemoveCredentials();

  const items = accounts.data ?? [];

  return (
    <Stack gap="md">
      <Group justify="flex-end">
        <Button leftSection={<IconPlus size={16} />} onClick={() => setCreating(true)}>
          Add account
        </Button>
      </Group>

      {accounts.isError && (
        <ErrorState error={accounts.error} onRetry={() => void accounts.refetch()} />
      )}

      {accounts.isPending && !accounts.data && <LoadingTable rows={3} columns={6} />}

      {accounts.data && items.length === 0 && (
        <EmptyState
          title="No accounts"
          description="An account is a place where value lives — Bitunix futures, Bitunix spot, a TonKeeper wallet, another exchange. Bitunix futures and spot are two separate accounts."
          action={
            <Button variant="light" onClick={() => setCreating(true)}>
              Add the first one
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
                  <Table.Th>Name</Table.Th>
                  <Table.Th>Kind</Table.Th>
                  <Table.Th>Venue</Table.Th>
                  <Table.Th>Sync</Table.Th>
                  <Table.Th>Tracked from</Table.Th>
                  <Table.Th>Credentials</Table.Th>
                  <Table.Th />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {items.map((account) => (
                  <Table.Tr key={account.id}>
                    <Table.Td>
                      <Group gap={6} wrap="nowrap">
                        <Text size="sm" fw={600}>
                          {account.name}
                        </Text>
                        {!account.isActive && (
                          <Badge size="sm" color="gray" variant="light">
                            Inactive
                          </Badge>
                        )}
                      </Group>
                      <Text size="xs" c="dimmed">
                        {account.quoteAsset}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm">
                        {KINDS.find((kind) => kind.value === account.kind)?.label ?? account.kind}
                      </Text>
                    </Table.Td>
                    <Table.Td>{account.venue}</Table.Td>
                    <Table.Td>
                      <Badge
                        size="sm"
                        variant="light"
                        color={account.syncMode === 'Api' ? 'indigo' : 'gray'}
                      >
                        {account.syncMode}
                      </Badge>
                    </Table.Td>
                    <Table.Td>
                      <Instant value={account.trackedFrom} dateOnly />
                    </Table.Td>
                    <Table.Td>
                      {account.apiKeyHint ? (
                        <Stack gap={0}>
                          <Group gap={6} wrap="nowrap">
                            <Text size="sm" ff="monospace">
                              {account.apiKeyHint}
                            </Text>
                            {!account.credentialEnabled && (
                              <Badge size="sm" color="orange" variant="light">
                                Disabled
                              </Badge>
                            )}
                          </Group>
                          <Text size="xs" c="dimmed">
                            verified <Instant value={account.lastVerifiedAt} dateOnly />
                            {account.verifiedViaEgress ? ` via ${account.verifiedViaEgress}` : ''}
                          </Text>
                        </Stack>
                      ) : (
                        <Text size="sm" c="dimmed">
                          None
                        </Text>
                      )}
                    </Table.Td>
                    <Table.Td>
                      <Group gap={4} justify="flex-end" wrap="nowrap">
                        <Button
                          size="compact-xs"
                          variant="light"
                          leftSection={<IconKey size={13} />}
                          onClick={() => setCredentialing(account)}
                        >
                          {account.apiKeyHint ? 'Replace' : 'Set'} key
                        </Button>
                        {account.apiKeyHint && (
                          <Button
                            size="compact-xs"
                            variant="subtle"
                            color="red"
                            onClick={() => setRemoving(account)}
                          >
                            Remove
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

      <Alert
        icon={<IconShieldLock size={18} />}
        color="gray"
        variant="light"
        title="Names, activity and tracked-from are read-only"
      >
        No endpoint writes them yet. They are set when the account is created; changing one means a
        database edit until the settings endpoints land.
      </Alert>

      <NewAccountModal opened={creating} onClose={() => setCreating(false)} />

      <CredentialModal
        account={credentialing}
        onClose={() => setCredentialing(null)}
        onOpenProxy={onOpenProxy}
      />

      <Modal
        opened={removing !== null}
        onClose={() => setRemoving(null)}
        title="Remove credentials?"
        size="sm"
      >
        <Stack gap="md">
          <Text size="sm">
            {removing?.name} will stop syncing. The trades already pulled stay in the journal.
          </Text>
          <Group justify="flex-end">
            <Button variant="default" onClick={() => setRemoving(null)}>
              Cancel
            </Button>
            <Button
              color="red"
              loading={removeCredentials.isPending}
              onClick={() => {
                if (!removing) {
                  return;
                }

                removeCredentials.mutate(removing.id, {
                  onSuccess: () => {
                    notifySuccess('Credentials removed.');
                    setRemoving(null);
                  },
                });
              }}
            >
              Remove
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  );
}

function NewAccountModal({ opened, onClose }: { opened: boolean; onClose: () => void }) {
  const create = useCreateAccount();

  const form = useForm({
    initialValues: {
      name: '',
      kind: 'ExchangeFutures' as AccountKind,
      venue: 'Bitunix' as Venue,
      syncMode: 'Api' as SyncMode,
      quoteAsset: 'USDT',
      trackedFrom: null as string | null,
    },
    validate: { name: (value) => (value.trim() ? null : 'Name the account') },
  });

  return (
    <Modal opened={opened} onClose={onClose} title="Add account" size="md">
      <form
        onSubmit={form.onSubmit((values) =>
          create.mutate(
            {
              name: values.name.trim(),
              kind: values.kind,
              venue: values.venue,
              syncMode: values.syncMode,
              quoteAsset: blankToNull(values.quoteAsset),
              trackedFrom: values.trackedFrom,
            },
            {
              onSuccess: () => {
                notifySuccess('Account created.');
                form.reset();
                onClose();
              },
              onError: (error) => applyServerErrors(form, error),
            },
          ),
        )}
      >
        <Stack gap="sm">
          <TextInput label="Name" placeholder="Bitunix futures" {...form.getInputProps('name')} />

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
              <Select
                label="Venue"
                data={[
                  { value: 'Bitunix', label: 'Bitunix' },
                  { value: 'Manual', label: 'Manual' },
                ]}
                allowDeselect={false}
                {...form.getInputProps('venue')}
              />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 6 }}>
              <Select
                label="Sync mode"
                data={[
                  { value: 'Api', label: 'API' },
                  { value: 'Manual', label: 'Manual' },
                ]}
                allowDeselect={false}
                {...form.getInputProps('syncMode')}
              />
            </Grid.Col>
            <Grid.Col span={{ base: 12, sm: 6 }}>
              <TextInput label="Quote asset" {...form.getInputProps('quoteAsset')} />
            </Grid.Col>
            <Grid.Col span={12}>
              <DateTimePicker
                label="Tracked from"
                description="The floor for backfill. Leave blank to take everything the exchange will give."
                clearable
                value={form.values.trackedFrom}
                onChange={(value) => form.setFieldValue('trackedFrom', value ?? null)}
              />
            </Grid.Col>
          </Grid>

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

function CredentialModal({
  account,
  onClose,
  onOpenProxy,
}: {
  account: AccountResponse | null;
  onClose: () => void;
  onOpenProxy: () => void;
}) {
  const setCredentials = useSetCredentials();
  const [proxyRequired, setProxyRequired] = useState(false);

  const form = useForm({
    initialValues: { apiKey: '', apiSecret: '', label: '' },
    validate: {
      apiKey: (value) => (value.trim() ? null : 'Paste the API key'),
      apiSecret: (value) => (value.trim() ? null : 'Paste the API secret'),
    },
  });

  return (
    <Modal
      opened={account !== null}
      onClose={() => {
        setProxyRequired(false);
        onClose();
      }}
      title={`API credentials — ${account?.name ?? ''}`}
      size="md"
    >
      <Stack gap="md">
        <Alert color="yellow" variant="light" icon={<IconAlertTriangle size={18} />}>
          Saving verifies the key against Bitunix, and that call goes out through the egress proxy.
          Configure the proxy <strong>first</strong> — otherwise the key is verified from your own
          address, which is exactly what the IP allowlist is there to prevent. The key should be
          created read-only: TradeLedger never places, modifies or cancels an order.
        </Alert>

        {proxyRequired && (
          <Alert color="orange" icon={<IconAlertTriangle size={18} />} title="No proxy resolved">
            <Stack gap="xs" align="flex-start">
              <Text size="sm">
                The request was refused before a socket was opened, so nothing reached Bitunix and
                the key was not verified.
              </Text>
              <Button
                size="xs"
                variant="light"
                onClick={() => {
                  setProxyRequired(false);
                  onClose();
                  onOpenProxy();
                }}
              >
                Configure the proxy
              </Button>
            </Stack>
          </Alert>
        )}

        <form
          onSubmit={form.onSubmit((values) => {
            if (!account) {
              return;
            }

            setProxyRequired(false);

            setCredentials.mutate(
              {
                id: account.id,
                body: {
                  apiKey: values.apiKey.trim(),
                  apiSecret: values.apiSecret.trim(),
                  label: blankToNull(values.label),
                },
              },
              {
                onSuccess: () => {
                  notifySuccess('Key verified and saved.');
                  form.reset();
                  onClose();
                },
                onError: (error) => {
                  if (error instanceof ApiError && error.code === 'proxy_required') {
                    setProxyRequired(true);
                    return;
                  }

                  applyServerErrors(form, error);
                },
              },
            );
          })}
        >
          <Stack gap="sm">
            <TextInput
              label="API key"
              autoComplete="off"
              spellCheck={false}
              {...form.getInputProps('apiKey')}
            />
            <PasswordInput
              label="API secret"
              description="Encrypted at rest and never returned by any endpoint."
              autoComplete="new-password"
              {...form.getInputProps('apiSecret')}
            />
            <TextInput label="Label" placeholder="Optional" {...form.getInputProps('label')} />

            <Group justify="flex-end">
              <Button variant="default" onClick={onClose}>
                Cancel
              </Button>
              <Button type="submit" loading={setCredentials.isPending}>
                Verify and save
              </Button>
            </Group>
          </Stack>
        </form>
      </Stack>
    </Modal>
  );
}
