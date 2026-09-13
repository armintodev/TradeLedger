import { useState } from 'react';
import { Badge, Button, Card, Group, Select, Stack, Table, Text } from '@mantine/core';
import { IconPlus } from '@tabler/icons-react';
import { useNavigate } from 'react-router';
import { useAbandonPlan, usePlans } from '@/api/queries/plans';
import { SideBadge } from '@/components/Badges';
import { Instant } from '@/components/Instant';
import { Money, Price } from '@/components/Money';
import { PageHeader } from '@/components/PageHeader';
import { EmptyState, ErrorState, LoadingTable } from '@/components/States';
import { formatFractionAsPercent, formatRatio } from '@/lib/format';
import { notifySuccess } from '@/lib/notify';
import type { PlanStatus } from '@/api/types';

const STATUS_COLORS: Record<PlanStatus, string> = {
  Draft: 'gray',
  Active: 'blue',
  Linked: 'teal',
  Abandoned: 'red',
  Expired: 'orange',
};

const STATUSES: { value: PlanStatus; label: string }[] = [
  { value: 'Draft', label: 'Draft' },
  { value: 'Active', label: 'Active' },
  { value: 'Linked', label: 'Linked' },
  { value: 'Abandoned', label: 'Abandoned' },
  { value: 'Expired', label: 'Expired' },
];

export function PlansPage() {
  const navigate = useNavigate();
  const [status, setStatus] = useState<PlanStatus | undefined>(undefined);

  const plans = usePlans(status);
  const abandon = useAbandonPlan();

  const items = plans.data ?? [];

  return (
    <Stack gap="md">
      <PageHeader
        title="Plans"
        description="What you intended before the position existed. A plan that matches a filled trade is linked automatically."
        actions={
          <Button leftSection={<IconPlus size={16} />} onClick={() => void navigate('/plans/new')}>
            New plan
          </Button>
        }
      />

      <Card padding="sm">
        <Select
          label="Status"
          placeholder="Any"
          data={STATUSES}
          value={status ?? null}
          onChange={(value) => setStatus((value as PlanStatus) ?? undefined)}
          clearable
          w={200}
        />
      </Card>

      {plans.isError && <ErrorState error={plans.error} onRetry={() => void plans.refetch()} />}

      {plans.isPending && !plans.data && <LoadingTable rows={5} columns={7} />}

      {plans.data && items.length === 0 && (
        <EmptyState
          title={status ? `No ${status.toLowerCase()} plans` : 'No plans yet'}
          description="Sizing a trade before you take it is what turns an unplanned entry into a planned one — and planned versus unplanned is a headline metric on the dashboard."
          action={
            <Button variant="light" onClick={() => void navigate('/plans/new')}>
              Plan a trade
            </Button>
          }
        />
      )}

      {items.length > 0 && (
        <Card padding={0}>
          <Table.ScrollContainer minWidth={1000}>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Created</Table.Th>
                  <Table.Th>Symbol</Table.Th>
                  <Table.Th>Side</Table.Th>
                  <Table.Th>Status</Table.Th>
                  <Table.Th ta="right">Entry</Table.Th>
                  <Table.Th ta="right">Stop</Table.Th>
                  <Table.Th ta="right">Target</Table.Th>
                  <Table.Th ta="right">Risk</Table.Th>
                  <Table.Th ta="right">R:R</Table.Th>
                  <Table.Th ta="right">Size</Table.Th>
                  <Table.Th />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {items.map((plan) => (
                  <Table.Tr key={plan.id}>
                    <Table.Td>
                      <Instant value={plan.createdAt} />
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm" fw={600}>
                        {plan.symbol}
                      </Text>
                      {plan.strategyName && (
                        <Text size="xs" c="dimmed">
                          {plan.strategyName}
                        </Text>
                      )}
                    </Table.Td>
                    <Table.Td>
                      <SideBadge side={plan.side} />
                    </Table.Td>
                    <Table.Td>
                      <Group gap={4} wrap="nowrap">
                        <Badge size="sm" variant="light" color={STATUS_COLORS[plan.status]}>
                          {plan.status}
                        </Badge>
                        {plan.linkedTradeId && (
                          <Button
                            size="compact-xs"
                            variant="subtle"
                            onClick={() => void navigate(`/trades/${plan.linkedTradeId}`)}
                          >
                            Trade
                          </Button>
                        )}
                      </Group>
                    </Table.Td>
                    <Table.Td ta="right">
                      <Price value={plan.plannedEntryPrice} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Price value={plan.plannedStopLossPrice} />
                    </Table.Td>
                    <Table.Td ta="right">
                      <Price value={plan.plannedTakeProfitPrice} />
                    </Table.Td>
                    <Table.Td ta="right">{formatFractionAsPercent(plan.riskFraction)}</Table.Td>
                    <Table.Td ta="right">{formatRatio(plan.plannedRiskReward)}</Table.Td>
                    <Table.Td ta="right">
                      <Money value={plan.plannedOrderValue} />
                      <Text size="xs" c="dimmed">
                        {plan.leverage}× margin <Money value={plan.plannedMargin} size="xs" />
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      {/* Only a plan that has not yet been linked or abandoned can
                          be abandoned. */}
                      {(plan.status === 'Active' || plan.status === 'Draft') && (
                        <Button
                          size="compact-xs"
                          variant="subtle"
                          color="red"
                          loading={abandon.isPending && abandon.variables === plan.id}
                          onClick={() =>
                            abandon.mutate(plan.id, {
                              onSuccess: () => notifySuccess(`${plan.symbol} plan abandoned.`),
                            })
                          }
                        >
                          Abandon
                        </Button>
                      )}
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Card>
      )}
    </Stack>
  );
}
