import { useState } from 'react';
import {
  Badge,
  Button,
  Card,
  Code,
  Group,
  Modal,
  Stack,
  Switch,
  Table,
  Text,
  Tooltip,
} from '@mantine/core';
import { IconPlus } from '@tabler/icons-react';
import { useNavigate } from 'react-router';
import { useBacktestStrategies, useDeleteBacktestStrategy } from '@/api/queries/backtests';
import { Instant } from '@/components/Instant';
import { EmptyState, ErrorState, LoadingTable } from '@/components/States';
import { notifySuccess } from '@/lib/notify';
import type { BacktestStrategyResponse } from '@/api/types';

export function StrategiesTab() {
  const navigate = useNavigate();
  const [includeInactive, setIncludeInactive] = useState(false);
  const strategies = useBacktestStrategies(includeInactive);
  const remove = useDeleteBacktestStrategy();

  const [retiring, setRetiring] = useState<BacktestStrategyResponse | null>(null);

  const items = strategies.data ?? [];

  return (
    <Stack gap="md">
      <Group justify="space-between" wrap="wrap" gap="sm">
        <Switch
          label="Include retired"
          checked={includeInactive}
          onChange={(event) => setIncludeInactive(event.currentTarget.checked)}
        />
        <Button
          leftSection={<IconPlus size={16} />}
          onClick={() => void navigate('/backtests/strategies/new')}
        >
          New strategy
        </Button>
      </Group>

      {strategies.isError && (
        <ErrorState error={strategies.error} onRetry={() => void strategies.refetch()} />
      )}

      {strategies.isPending && !strategies.data && <LoadingTable rows={3} columns={5} />}

      {strategies.data && items.length === 0 && (
        <EmptyState
          title="No rule strategies"
          description="A rule strategy carries machine-readable entry conditions — distinct from the Strategy labels on your journal, which are just names. Build one and a backtest can run it."
          action={
            <Button variant="light" onClick={() => void navigate('/backtests/strategies/new')}>
              Build one
            </Button>
          }
        />
      )}

      {items.length > 0 && (
        <Card padding={0}>
          <Table.ScrollContainer minWidth={700}>
            <Table highlightOnHover>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Name</Table.Th>
                  <Table.Th ta="right">Version</Table.Th>
                  <Table.Th>Rule hash</Table.Th>
                  <Table.Th>Updated</Table.Th>
                  <Table.Th />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {items.map((strategy) => (
                  <Table.Tr
                    key={strategy.id}
                    style={{ cursor: 'pointer' }}
                    onClick={() => void navigate(`/backtests/strategies/${strategy.id}`)}
                  >
                    <Table.Td>
                      <Group gap={6} wrap="nowrap">
                        <Text size="sm" fw={600}>
                          {strategy.name}
                        </Text>
                        {!strategy.isActive && (
                          <Badge size="sm" color="gray" variant="light">
                            Retired
                          </Badge>
                        )}
                      </Group>
                      {strategy.description && (
                        <Text size="xs" c="dimmed" lineClamp={1}>
                          {strategy.description}
                        </Text>
                      )}
                    </Table.Td>
                    <Table.Td ta="right">v{strategy.version}</Table.Td>
                    <Table.Td>
                      {/* The list deliberately does not carry the rule tree —
                          only the by-id fetch does — so the hash is all that can
                          be shown here. */}
                      <Tooltip label={strategy.ruleHash} withArrow>
                        <Code>{strategy.ruleHash.slice(0, 8)}</Code>
                      </Tooltip>
                    </Table.Td>
                    <Table.Td>
                      <Instant value={strategy.updatedAt} />
                    </Table.Td>
                    <Table.Td onClick={(event) => event.stopPropagation()}>
                      <Group gap={4} justify="flex-end" wrap="nowrap">
                        {strategy.isActive && (
                          <Button
                            size="compact-xs"
                            variant="subtle"
                            color="red"
                            onClick={() => setRetiring(strategy)}
                          >
                            Retire
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

      <Modal
        opened={retiring !== null}
        onClose={() => setRetiring(null)}
        title="Retire this strategy?"
        size="sm"
      >
        <Stack gap="md">
          <Text size="sm">
            {retiring?.name} stops being offered for new runs. Finished runs keep their own copy of
            the rules, so their results stay readable either way.
          </Text>
          <Group justify="flex-end">
            <Button variant="default" onClick={() => setRetiring(null)}>
              Cancel
            </Button>
            <Button
              color="red"
              loading={remove.isPending}
              onClick={() => {
                if (!retiring) {
                  return;
                }

                remove.mutate(retiring.id, {
                  onSuccess: () => {
                    notifySuccess(`${retiring.name} retired.`);
                    setRetiring(null);
                  },
                });
              }}
            >
              Retire
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  );
}
