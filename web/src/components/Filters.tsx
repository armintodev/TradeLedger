import { Group, SegmentedControl, Select } from '@mantine/core';
import { DatePickerInput } from '@mantine/dates';
import { useAccounts } from '@/api/queries/accounts';
import { RANGE_LABELS, type AnalyticsFilters, type RangePreset } from '@/lib/filters';

export function AccountSelect({
  value,
  onChange,
  label = 'Account',
  placeholder = 'All accounts',
  clearable = true,
  required = false,
  error,
  w,
}: {
  value: string | undefined;
  onChange: (value: string | undefined) => void;
  label?: string | null;
  placeholder?: string;
  clearable?: boolean;
  required?: boolean;
  error?: string;
  w?: number | string;
}) {
  const accounts = useAccounts();

  const data = (accounts.data ?? []).map((account) => ({
    value: account.id,
    label: `${account.name} · ${account.quoteAsset}`,
  }));

  return (
    <Select
      label={label ?? undefined}
      placeholder={accounts.isPending ? 'Loading…' : placeholder}
      data={data}
      value={value ?? null}
      onChange={(next) => onChange(next ?? undefined)}
      clearable={clearable}
      required={required}
      error={error}
      searchable={data.length > 6}
      disabled={accounts.isPending}
      w={w}
    />
  );
}

const PRESETS: RangePreset[] = ['7d', '30d', '90d', 'ytd', 'all', 'custom'];

/**
 * Drives `from`/`to` on all four analytics queries at once. A preset is stored
 * as the preset, not as the dates it resolved to, so a bookmarked "30 days"
 * still means the last thirty days tomorrow.
 */
export function RangeControl({
  filters,
  onChange,
}: {
  filters: AnalyticsFilters;
  onChange: (next: AnalyticsFilters) => void;
}) {
  return (
    <Group gap="sm" align="flex-end" wrap="wrap">
      <SegmentedControl
        size="xs"
        value={filters.range}
        onChange={(value) => onChange({ ...filters, range: value as RangePreset })}
        data={PRESETS.map((preset) => ({ value: preset, label: RANGE_LABELS[preset] }))}
      />

      {filters.range === 'custom' && (
        <>
          <DatePickerInput
            size="xs"
            label="From"
            placeholder="Start"
            clearable
            value={filters.from ?? null}
            onChange={(value) => onChange({ ...filters, from: value ?? undefined })}
            w={150}
          />
          <DatePickerInput
            size="xs"
            label="To"
            placeholder="End"
            clearable
            value={filters.to ?? null}
            onChange={(value) => onChange({ ...filters, to: value ?? undefined })}
            w={150}
          />
        </>
      )}

      <AccountSelect
        label={null}
        value={filters.accountId}
        onChange={(accountId) => onChange({ ...filters, accountId })}
        w={200}
      />
    </Group>
  );
}
