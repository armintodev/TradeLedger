import {
  ActionIcon,
  Badge,
  Card,
  Group,
  NumberInput,
  Select,
  Stack,
  Text,
  TextInput,
  Title,
  Tooltip,
} from '@mantine/core';
import { IconPlus, IconTrash } from '@tabler/icons-react';
import { newId } from '@/lib/rules/tree';
import {
  INDICATOR_META,
  INDICATOR_TYPES,
  LIMITS,
  PRICE_FIELDS,
  type IndicatorDraft,
  type IndicatorType,
  type NodeId,
  type PriceField,
} from '@/lib/rules/types';
import type { Diagnostic } from '@/lib/rules/validate';

export interface IndicatorListProps {
  indicators: IndicatorDraft[];
  diagnostics: Map<NodeId, Diagnostic[]>;
  serverErrors: Map<NodeId, string>;
  onChange: (indicators: IndicatorDraft[]) => void;
}

/**
 * The five indicators the engine implements, in decimal arithmetic. A crossover
 * is two declarations plus a `CrossesAbove` condition — there is no combined
 * indicator, and `period` is the only parameter any of them takes.
 */
export function IndicatorList({
  indicators,
  diagnostics,
  serverErrors,
  onChange,
}: IndicatorListProps) {
  function update(id: NodeId, patch: Partial<IndicatorDraft>) {
    onChange(
      indicators.map((indicator) => {
        if (indicator.id !== id) {
          return indicator;
        }

        const next = { ...indicator, ...patch };

        // An indicator that derives from the whole bar must carry no source at
        // all — the parser rejects the document if it does.
        if (patch.type && !INDICATOR_META[patch.type].takesSource) {
          next.source = null;
        } else if (patch.type && INDICATOR_META[patch.type].takesSource && !next.source) {
          next.source = 'Close';
        }

        return next;
      }),
    );
  }

  function add() {
    onChange([
      ...indicators,
      {
        id: newId(),
        ref: nextRef(indicators),
        type: 'Ema',
        source: 'Close',
        period: 20,
      },
    ]);
  }

  return (
    <Card padding="md">
      <Stack gap="sm">
        <Group justify="space-between" align="flex-start" wrap="wrap" gap="sm">
          <Stack gap={2}>
            <Title order={5}>Indicators</Title>
            <Text size="xs" c="dimmed">
              Named here, referenced by name in the conditions below.
            </Text>
          </Stack>

          <ActionIcon variant="light" onClick={add} aria-label="Add an indicator">
            <IconPlus size={16} />
          </ActionIcon>
        </Group>

        {indicators.length === 0 && (
          <Text size="sm" c="red">
            At least one indicator must be declared.
          </Text>
        )}

        {indicators.map((indicator) => {
          const problems = diagnostics.get(indicator.id) ?? [];
          const errorFor = (field: string) => problems.find((p) => p.field === field)?.message;
          const meta = INDICATOR_META[indicator.type];
          const serverError = serverErrors.get(indicator.id);

          return (
            <Group key={indicator.id} gap="xs" align="flex-start" wrap="wrap">
              <Select
                size="xs"
                label="Type"
                data={INDICATOR_TYPES}
                value={indicator.type}
                onChange={(value) =>
                  update(indicator.id, { type: (value as IndicatorType) ?? 'Ema' })
                }
                allowDeselect={false}
                w={100}
              />

              <TextInput
                size="xs"
                label="Name"
                placeholder="fast"
                value={indicator.ref}
                onChange={(event) => update(indicator.id, { ref: event.currentTarget.value })}
                error={errorFor('ref') ?? serverError}
                w={110}
              />

              <NumberInput
                size="xs"
                label="Period"
                value={indicator.period ?? ''}
                onChange={(value) =>
                  update(indicator.id, { period: value === '' ? null : Number(value) })
                }
                error={errorFor('period')}
                min={LIMITS.minPeriod}
                max={LIMITS.maxPeriod}
                w={90}
                hideControls
              />

              {meta.takesSource ? (
                <Select
                  size="xs"
                  label="Source"
                  data={PRICE_FIELDS}
                  value={indicator.source ?? 'Close'}
                  onChange={(value) =>
                    update(indicator.id, { source: (value as PriceField) ?? 'Close' })
                  }
                  allowDeselect={false}
                  w={100}
                />
              ) : (
                <Tooltip label={`${indicator.type} derives from the whole bar.`} withArrow>
                  <Badge variant="light" color="gray" mt={22}>
                    whole bar
                  </Badge>
                </Tooltip>
              )}

              <Tooltip label={`Warmup: ${meta.warmupMultiplier}× the period`} withArrow>
                <Badge variant="light" color="blue" mt={22}>
                  {(indicator.period ?? 0) * meta.warmupMultiplier} bars
                </Badge>
              </Tooltip>

              <ActionIcon
                variant="subtle"
                color="red"
                mt={20}
                onClick={() => onChange(indicators.filter((other) => other.id !== indicator.id))}
                aria-label={`Remove ${indicator.ref}`}
              >
                <IconTrash size={14} />
              </ActionIcon>
            </Group>
          );
        })}
      </Stack>
    </Card>
  );
}

/** `fast`, `slow`, then `ema3`, `ema4`… — unique without making the user think. */
function nextRef(indicators: IndicatorDraft[]): string {
  const taken = new Set(indicators.map((indicator) => indicator.ref.toLowerCase()));

  for (const candidate of ['fast', 'slow', 'trend', 'filter']) {
    if (!taken.has(candidate)) {
      return candidate;
    }
  }

  let index = indicators.length + 1;

  while (taken.has(`ind${index}`)) {
    index += 1;
  }

  return `ind${index}`;
}
