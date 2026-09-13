import {
  Alert,
  Card,
  Group,
  NumberInput,
  SegmentedControl,
  Select,
  Stack,
  Text,
  Title,
} from '@mantine/core';
import { IconInfoCircle } from '@tabler/icons-react';
import { INDICATOR_META, LIMITS, type IndicatorDraft, type StopLossDraft } from '@/lib/rules/types';
import type { Diagnostic } from '@/lib/rules/validate';

export interface StopLossEditorProps {
  stop: StopLossDraft;
  indicators: IndicatorDraft[];
  diagnostics: Diagnostic[];
  onChange: (stop: StopLossDraft) => void;
}

/**
 * The stop is mandatory, and it is half of the exit story: a position closes on
 * its stop, on a target derived from the run's risk-to-reward ratio, or on
 * liquidation. There are no exit rules to write, which is worth saying plainly
 * rather than leaving the user to look for the missing section.
 */
export function StopLossEditor({ stop, indicators, diagnostics, onChange }: StopLossEditorProps) {
  const errorFor = (field: string) =>
    diagnostics.find((d) => d.field === `stopLoss.${field}`)?.message;

  const declared = indicators.filter((indicator) => indicator.ref.trim());
  const referenced =
    stop.kind === 'IndicatorLevel'
      ? declared.find((i) => i.ref.toLowerCase() === stop.ref.toLowerCase())
      : undefined;

  const outputs = referenced ? INDICATOR_META[referenced.type].outputs : [];

  return (
    <Card padding="md">
      <Stack gap="sm">
        <Stack gap={2}>
          <Title order={5}>Stop loss</Title>
          <Text size="xs" c="dimmed">
            Required. Position size is derived from the distance to it.
          </Text>
        </Stack>

        <SegmentedControl
          size="xs"
          value={stop.kind}
          onChange={(value) =>
            onChange(
              value === 'Percent'
                ? { kind: 'Percent', percent: 2 }
                : {
                    kind: 'IndicatorLevel',
                    ref: declared[0]?.ref ?? '',
                    output: null,
                    offset: 0,
                    bufferPercent: 0,
                  },
            )
          }
          data={[
            { value: 'Percent', label: 'A percentage' },
            { value: 'IndicatorLevel', label: 'An indicator level' },
          ]}
        />

        {stop.kind === 'Percent' ? (
          <NumberInput
            label="Distance from entry"
            suffix=" %"
            value={stop.percent ?? ''}
            onChange={(value) =>
              onChange({ kind: 'Percent', percent: value === '' ? null : Number(value) })
            }
            error={errorFor('percent')}
            min={LIMITS.minStopPercent}
            max={LIMITS.maxStopPercent}
            decimalScale={2}
            step={0.5}
            w={200}
          />
        ) : (
          <Group gap="sm" align="flex-start" wrap="wrap">
            <Select
              label="Indicator"
              placeholder={declared.length ? 'Pick one' : 'Declare an indicator first'}
              data={declared.map((indicator) => ({
                value: indicator.ref,
                label: `${indicator.ref} (${indicator.type} ${indicator.period ?? '?'})`,
              }))}
              value={stop.ref || null}
              onChange={(value) => onChange({ ...stop, ref: value ?? '', output: null })}
              error={errorFor('ref')}
              w={200}
            />

            {outputs.length > 1 && (
              <Select
                label="Output"
                data={outputs}
                value={stop.output}
                onChange={(value) => onChange({ ...stop, output: value })}
                error={errorFor('output')}
                w={130}
              />
            )}

            <NumberInput
              label="Bars back"
              value={stop.offset}
              onChange={(value) => onChange({ ...stop, offset: Number(value) || 0 })}
              min={0}
              max={LIMITS.maxOffset}
              w={110}
              hideControls
            />

            <NumberInput
              label="Buffer"
              description="Beyond the level"
              suffix=" %"
              value={stop.bufferPercent}
              onChange={(value) => onChange({ ...stop, bufferPercent: Number(value) || 0 })}
              error={errorFor('bufferPercent')}
              min={0}
              max={LIMITS.maxBufferPercent}
              decimalScale={2}
              step={0.1}
              w={130}
            />
          </Group>
        )}

        <Alert color="gray" variant="light" icon={<IconInfoCircle size={16} />}>
          There are no exit rules to write. Every position closes on this stop, on a target derived
          at the run&apos;s risk-to-reward ratio, or on liquidation.
        </Alert>
      </Stack>
    </Card>
  );
}
