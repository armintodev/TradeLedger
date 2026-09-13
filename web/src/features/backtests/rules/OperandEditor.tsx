import { Group, NumberInput, SegmentedControl, Select, Text, Tooltip } from '@mantine/core';
import { constOperand, indicatorOperand, priceOperand } from '@/lib/rules/tree';
import {
  INDICATOR_META,
  LIMITS,
  PRICE_FIELDS,
  type IndicatorDraft,
  type OperandDraft,
  type PriceField,
} from '@/lib/rules/types';
import type { Diagnostic } from '@/lib/rules/validate';

export interface OperandEditorProps {
  operand: OperandDraft;
  indicators: IndicatorDraft[];
  diagnostics: Map<string, Diagnostic[]>;
  onChange: (next: OperandDraft) => void;
  label?: string;
}

/**
 * One value in a condition: an indicator's output, a price field, or a
 * constant. The kind switch rebuilds the operand rather than carrying stale
 * fields across, because a `ref` left on a constant would serialise wrong.
 */
export function OperandEditor({
  operand,
  indicators,
  diagnostics,
  onChange,
  label,
}: OperandEditorProps) {
  const problems = diagnostics.get(operand.id) ?? [];
  const errorFor = (field: string) => problems.find((p) => p.field === field)?.message;

  const declared = indicators.filter((indicator) => indicator.ref.trim());
  const referenced = declared.find(
    (indicator) =>
      indicator.ref.toLowerCase() ===
      (operand.kind === 'indicator' ? operand.ref.toLowerCase() : ''),
  );

  const outputs = referenced ? INDICATOR_META[referenced.type].outputs : [];

  function switchKind(kind: string) {
    if (kind === 'indicator') {
      onChange(indicatorOperand(declared[0]?.ref ?? ''));
    } else if (kind === 'price') {
      onChange(priceOperand());
    } else {
      onChange(constOperand());
    }
  }

  return (
    <Group gap={6} wrap="wrap" align="flex-start">
      {label && (
        <Text size="xs" c="dimmed" mt={7} w={44}>
          {label}
        </Text>
      )}

      <SegmentedControl
        size="xs"
        value={operand.kind}
        onChange={switchKind}
        data={[
          { value: 'indicator', label: 'Indicator' },
          { value: 'price', label: 'Price' },
          { value: 'const', label: 'Number' },
        ]}
      />

      {operand.kind === 'indicator' && (
        <>
          <Select
            size="xs"
            placeholder={declared.length ? 'Pick one' : 'Declare an indicator first'}
            data={declared.map((indicator) => ({
              value: indicator.ref,
              label: `${indicator.ref} (${indicator.type} ${indicator.period ?? '?'})`,
            }))}
            value={operand.ref || null}
            onChange={(value) => onChange({ ...operand, ref: value ?? '', output: null })}
            error={errorFor('ref')}
            searchable={declared.length > 6}
            w={180}
          />

          {/* Only DMI has more than one output, and there it is mandatory. */}
          {outputs.length > 1 && (
            <Select
              size="xs"
              placeholder="Output"
              data={outputs}
              value={operand.output}
              onChange={(value) => onChange({ ...operand, output: value })}
              error={errorFor('output')}
              w={110}
            />
          )}
        </>
      )}

      {operand.kind === 'price' && (
        <Select
          size="xs"
          data={PRICE_FIELDS}
          value={operand.price}
          onChange={(value) => onChange({ ...operand, price: (value as PriceField) ?? 'Close' })}
          allowDeselect={false}
          w={110}
        />
      )}

      {operand.kind === 'const' && (
        <NumberInput
          size="xs"
          placeholder="Value"
          value={operand.value ?? ''}
          onChange={(value) =>
            onChange({
              ...operand,
              value: value === '' ? null : Number(value),
            })
          }
          error={errorFor('const')}
          hideControls
          decimalScale={8}
          w={110}
        />
      )}

      {operand.kind !== 'const' && (
        <Tooltip
          label="How many bars back to read this value. 0 is the current bar; a negative offset would read the future and is refused."
          withArrow
          multiline
          w={260}
        >
          <NumberInput
            size="xs"
            placeholder="0"
            suffix=" bars back"
            value={operand.offset}
            onChange={(value) => onChange({ ...operand, offset: Number(value) || 0 })}
            error={errorFor('offset')}
            min={0}
            max={LIMITS.maxOffset}
            w={120}
            hideControls
          />
        </Tooltip>
      )}
    </Group>
  );
}
