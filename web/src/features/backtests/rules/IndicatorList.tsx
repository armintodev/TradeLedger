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
import { intervalOptions, intervalRatio, INTERVAL_LABELS } from '@/lib/marketData';
import type { CandleInterval } from '@/api/types';
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
  /** The interval a run would trade. Only used to make the warmup badge truthful. */
  previewInterval?: CandleInterval;
  onChange: (indicators: IndicatorDraft[]) => void;
}

/**
 * The seven indicators the engine implements, in decimal arithmetic. A crossover
 * is two declarations plus a `CrossesAbove` condition — there is no combined
 * indicator, and `period` is the only parameter any of them takes.
 *
 * An indicator may name a timeframe above the one the run trades; leaving it at the
 * run's interval is the common case and is what keeps a document at rule version 1.
 */
export function IndicatorList({
  indicators,
  diagnostics,
  serverErrors,
  previewInterval,
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
        } else if (patch.type && INDICATOR_META[patch.type].takesSource) {
          // Switching type re-seeds the source, so changing Ema to Highest reads highs
          // rather than silently keeping Close and meaning the highest close.
          const previous = indicator.type;
          const reseed = !next.source || next.source === INDICATOR_META[previous].defaultSource;

          if (reseed) {
            next.source = INDICATOR_META[patch.type].defaultSource;
          }
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
        source: INDICATOR_META.Ema.defaultSource,
        period: 20,
        interval: null,
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

              <Select
                size="xs"
                label="Timeframe"
                data={[{ value: '', label: 'Run interval' }, ...intervalOptions()]}
                value={indicator.interval ?? ''}
                onChange={(value) =>
                  update(indicator.id, { interval: (value || null) as CandleInterval | null })
                }
                error={errorFor('interval')}
                allowDeselect={false}
                w={120}
              />

              <WarmupBadge indicator={indicator} previewInterval={previewInterval} />

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

/**
 * How much history one indicator needs. A higher timeframe multiplies it by the interval
 * ratio, which is the part that catches people out: a four-hour ADX(14) on a fifteen-minute
 * run wants 1,120 bars of warmup, not 70.
 */
function WarmupBadge({
  indicator,
  previewInterval,
}: {
  indicator: IndicatorDraft;
  previewInterval?: CandleInterval;
}) {
  const meta = INDICATOR_META[indicator.type];
  const own = (indicator.period ?? 0) * meta.warmupMultiplier;
  const timeframe = indicator.interval;

  if (!timeframe) {
    return (
      <Tooltip label={`Warmup: ${meta.warmupMultiplier}× the period`} withArrow>
        <Badge variant="light" color="blue" mt={22}>
          {own} bars
        </Badge>
      </Tooltip>
    );
  }

  const ratio = previewInterval ? intervalRatio(timeframe, previewInterval) : null;

  if (previewInterval && ratio === null) {
    return (
      <Tooltip
        label={`${INTERVAL_LABELS[timeframe]} bars cannot be built from ${INTERVAL_LABELS[previewInterval]} ones, so a run at that interval would be refused.`}
        withArrow
      >
        <Badge variant="light" color="red" mt={22}>
          {INTERVAL_LABELS[timeframe]} unusable
        </Badge>
      </Tooltip>
    );
  }

  const label = ratio
    ? `${own} ${INTERVAL_LABELS[timeframe]} bars, which is ${(own * ratio).toLocaleString()} bars at the run interval`
    : `Warmup: ${own} ${INTERVAL_LABELS[timeframe]} bars`;

  return (
    <Tooltip label={label} withArrow>
      <Badge variant="light" color="grape" mt={22}>
        {own} × {INTERVAL_LABELS[timeframe]}
      </Badge>
    </Tooltip>
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
