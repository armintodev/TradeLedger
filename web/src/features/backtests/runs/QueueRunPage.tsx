import { useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Checkbox,
  Collapse,
  Grid,
  Group,
  NumberInput,
  Select,
  Stack,
  Switch,
  Text,
  TextInput,
  Title,
} from '@mantine/core';
import { DatePickerInput } from '@mantine/dates';
import { useForm } from '@mantine/form';
import { useDebouncedValue } from '@mantine/hooks';
import {
  IconAlertTriangle,
  IconArrowLeft,
  IconChevronDown,
  IconChevronRight,
  IconPlayerPlay,
} from '@tabler/icons-react';
import { useNavigate } from 'react-router';
import {
  RUN_LIMITS,
  useBacktestAccounts,
  useBacktestStrategies,
  useQueueBacktest,
} from '@/api/queries/backtests';
import { useGaps } from '@/api/queries/marketData';
import { ApiError } from '@/api/problem';
import { PageHeader } from '@/components/PageHeader';
import { applyServerErrors, toNumberOrNull } from '@/lib/forms';
import { buildGapQuery, intervalOptions, rangeToInstants, sourceOptions } from '@/lib/marketData';
import { notifySuccess } from '@/lib/notify';
import type { CandleInterval, CandleSource } from '@/api/types';
import { GapPanel } from '../marketData/GapPanel';

export function QueueRunPage() {
  const navigate = useNavigate();
  const accounts = useBacktestAccounts();
  const strategies = useBacktestStrategies();
  const queue = useQueueBacktest();

  const [advanced, setAdvanced] = useState(false);
  const [refusal, setRefusal] = useState<{ code: string; message: string } | null>(null);

  const form = useForm({
    initialValues: {
      backtestAccountId: '',
      backtestStrategyId: '',
      symbol: '',
      source: 'BinanceFutures' as CandleSource,
      interval: 'FourHours' as CandleInterval,
      from: null as string | null,
      to: null as string | null,
      riskPercentPerPosition: 2 as number | string,
      riskRewardRatio: 2 as number | string,
      leverage: 5 as number | string,
      takerFeeRate: 0.0006 as number | string,
      slippageRate: 0.0005 as number | string,
      maintenanceMarginRate: 0.005 as number | string,
      includeFunding: true,
      allowGaps: false,
    },
    validate: {
      backtestAccountId: (value) => (value ? null : 'Pick an account'),
      // Optional on the wire, but a run without a strategy is accepted and then
      // fails a couple of seconds later with nothing to simulate.
      backtestStrategyId: (value) => (value ? null : 'Pick a strategy'),
      symbol: (value) => (value.trim() ? null : 'Enter a symbol'),
      from: (value) => (value ? null : 'Pick a start date'),
      to: (value, values) =>
        value && values.from && value > values.from ? null : 'The end must be after the start',
      riskPercentPerPosition: (value) => {
        const parsed = toNumberOrNull(value);

        return parsed !== null &&
          parsed >= RUN_LIMITS.riskPercent.min &&
          parsed <= RUN_LIMITS.riskPercent.max
          ? null
          : `Between ${RUN_LIMITS.riskPercent.min} and ${RUN_LIMITS.riskPercent.max} percent`;
      },
      riskRewardRatio: (value) =>
        (toNumberOrNull(value) ?? 0) >= RUN_LIMITS.riskReward.min
          ? null
          : `At least ${RUN_LIMITS.riskReward.min} to 1`,
      leverage: (value) => {
        const parsed = toNumberOrNull(value);

        return parsed !== null &&
          parsed >= RUN_LIMITS.leverage.min &&
          parsed <= RUN_LIMITS.leverage.max
          ? null
          : `Between ${RUN_LIMITS.leverage.min} and ${RUN_LIMITS.leverage.max}`;
      },
    },
  });

  const { symbol, source, interval, from, to } = form.values;

  const [debouncedSymbol] = useDebouncedValue(symbol, 500);

  // The same builder the Market Data tab uses, so the pre-flight checks exactly
  // the window the queue endpoint will.
  const gapQuery = buildGapQuery({ source, symbol: debouncedSymbol, interval, from, to });
  const gaps = useGaps(gapQuery);

  const hasGaps = (gaps.data?.length ?? 0) > 0;

  function submit(values: typeof form.values) {
    setRefusal(null);

    const range = rangeToInstants(values.from, values.to);

    if (!range.from || !range.to) {
      return;
    }

    queue.mutate(
      {
        backtestAccountId: values.backtestAccountId,
        backtestStrategyId: values.backtestStrategyId,
        symbol: values.symbol.trim().toUpperCase(),
        source: values.source,
        interval: values.interval,
        from: range.from,
        to: range.to,
        riskPercentPerPosition: toNumberOrNull(values.riskPercentPerPosition),
        riskRewardRatio: toNumberOrNull(values.riskRewardRatio),
        leverage: toNumberOrNull(values.leverage),
        takerFeeRate: toNumberOrNull(values.takerFeeRate),
        slippageRate: toNumberOrNull(values.slippageRate),
        maintenanceMarginRate: toNumberOrNull(values.maintenanceMarginRate),
        includeFunding: values.includeFunding,
        allowGaps: values.allowGaps,
      },
      {
        onSuccess: (run) => {
          notifySuccess('Run queued.');
          void navigate(`/backtests/runs/${run.id}`);
        },
        onError: (error) => {
          if (error instanceof ApiError) {
            // The pre-flight is a convenience, not a guarantee — the range can
            // change between the probe and the submit.
            if (
              error.code === 'candle_data_has_gaps' ||
              error.code === 'run_range_overlaps' ||
              error.code === 'backtest_account_inactive'
            ) {
              setRefusal({ code: error.code, message: error.detail ?? error.title });

              if (error.code === 'candle_data_has_gaps') {
                void gaps.refetch();
              }

              return;
            }
          }

          applyServerErrors(form, error);
        },
      },
    );
  }

  return (
    <Stack gap="md">
      <Group>
        <Button
          variant="subtle"
          leftSection={<IconArrowLeft size={16} />}
          onClick={() => void navigate('/backtests')}
        >
          Backtests
        </Button>
      </Group>

      <PageHeader
        title="Queue a run"
        description="One position at a time, entered on the bar after a signal, closed on the stop, the target or liquidation."
      />

      <form onSubmit={form.onSubmit(submit)}>
        <Stack gap="md">
          <Card padding="md">
            <Stack gap="sm">
              <Title order={5}>What to run</Title>

              <Grid gap="sm">
                <Grid.Col span={{ base: 12, sm: 6 }}>
                  <Select
                    label="Account"
                    placeholder={accounts.isPending ? 'Loading…' : 'Where the balance compounds'}
                    data={(accounts.data ?? []).map((account) => ({
                      value: account.id,
                      label: `${account.name} · ${account.mode}`,
                    }))}
                    searchable
                    {...form.getInputProps('backtestAccountId')}
                  />
                </Grid.Col>

                <Grid.Col span={{ base: 12, sm: 6 }}>
                  <Select
                    label="Strategy"
                    placeholder={strategies.isPending ? 'Loading…' : 'The rules to simulate'}
                    data={(strategies.data ?? []).map((strategy) => ({
                      value: strategy.id,
                      label: `${strategy.name} (v${strategy.version})`,
                    }))}
                    searchable
                    {...form.getInputProps('backtestStrategyId')}
                  />
                </Grid.Col>

                <Grid.Col span={{ base: 6, sm: 3 }}>
                  <TextInput
                    label="Symbol"
                    placeholder="BTCUSDT"
                    {...form.getInputProps('symbol')}
                  />
                </Grid.Col>
                <Grid.Col span={{ base: 6, sm: 3 }}>
                  <Select
                    label="Source"
                    data={sourceOptions(['BinanceFutures', 'BinanceSpot', 'CsvImport'])}
                    allowDeselect={false}
                    {...form.getInputProps('source')}
                  />
                </Grid.Col>
                <Grid.Col span={{ base: 6, sm: 2 }}>
                  <Select
                    label="Interval"
                    // One minute is not tradeable; it exists to resolve exits
                    // inside a larger bar.
                    data={intervalOptions()}
                    allowDeselect={false}
                    {...form.getInputProps('interval')}
                  />
                </Grid.Col>
                <Grid.Col span={{ base: 6, sm: 2 }}>
                  <DatePickerInput
                    label="From"
                    placeholder="Start"
                    {...form.getInputProps('from')}
                  />
                </Grid.Col>
                <Grid.Col span={{ base: 6, sm: 2 }}>
                  <DatePickerInput label="To" placeholder="End" {...form.getInputProps('to')} />
                </Grid.Col>
              </Grid>
            </Stack>
          </Card>

          <Card padding="md">
            <Stack gap="sm">
              <Title order={5}>Candle data</Title>

              <GapPanel
                query={gaps}
                source={source}
                symbol={debouncedSymbol}
                interval={interval}
                idleMessage="Fill in the symbol, interval and dates above and the range will be checked for missing candles."
              />

              <Checkbox
                label="Run anyway over gapped data"
                description="The result will be stamped as having permitted gaps. A backtest across a hole in the data silently lies, so this is a deliberate choice."
                disabled={!hasGaps}
                {...form.getInputProps('allowGaps', { type: 'checkbox' })}
              />
            </Stack>
          </Card>

          <Card padding="md">
            <Stack gap="sm">
              <Group
                justify="space-between"
                style={{ cursor: 'pointer' }}
                onClick={() => setAdvanced((open) => !open)}
              >
                <Title order={5}>Sizing and costs</Title>
                {advanced ? <IconChevronDown size={16} /> : <IconChevronRight size={16} />}
              </Group>

              {!advanced && (
                <Text size="xs" c="dimmed">
                  {form.values.riskPercentPerPosition}% risk · {form.values.riskRewardRatio}:1 ·{' '}
                  {form.values.leverage}× · taker {form.values.takerFeeRate}
                </Text>
              )}

              <Collapse expanded={advanced}>
                <Grid gap="sm">
                  <Grid.Col span={{ base: 6, sm: 3 }}>
                    <NumberInput
                      label="Risk per position"
                      description={`${RUN_LIMITS.riskPercent.min}–${RUN_LIMITS.riskPercent.max}%`}
                      suffix=" %"
                      decimalScale={2}
                      step={0.5}
                      {...form.getInputProps('riskPercentPerPosition')}
                    />
                  </Grid.Col>
                  <Grid.Col span={{ base: 6, sm: 3 }}>
                    <NumberInput
                      label="Risk to reward"
                      description={`At least ${RUN_LIMITS.riskReward.min}`}
                      decimalScale={2}
                      step={0.5}
                      {...form.getInputProps('riskRewardRatio')}
                    />
                  </Grid.Col>
                  <Grid.Col span={{ base: 6, sm: 3 }}>
                    <NumberInput
                      label="Leverage"
                      description={`${RUN_LIMITS.leverage.min}–${RUN_LIMITS.leverage.max}`}
                      {...form.getInputProps('leverage')}
                    />
                  </Grid.Col>
                  <Grid.Col span={{ base: 6, sm: 3 }}>
                    <Switch
                      label="Include funding"
                      mt="xl"
                      {...form.getInputProps('includeFunding', { type: 'checkbox' })}
                    />
                  </Grid.Col>

                  <Grid.Col span={{ base: 6, sm: 4 }}>
                    <NumberInput
                      label="Taker fee"
                      description="Every simulated fill is a taker"
                      decimalScale={6}
                      hideControls
                      {...form.getInputProps('takerFeeRate')}
                    />
                  </Grid.Col>
                  <Grid.Col span={{ base: 6, sm: 4 }}>
                    <NumberInput
                      label="Slippage"
                      decimalScale={6}
                      hideControls
                      {...form.getInputProps('slippageRate')}
                    />
                  </Grid.Col>
                  <Grid.Col span={{ base: 6, sm: 4 }}>
                    <NumberInput
                      label="Maintenance margin"
                      decimalScale={6}
                      hideControls
                      {...form.getInputProps('maintenanceMarginRate')}
                    />
                  </Grid.Col>
                </Grid>
              </Collapse>
            </Stack>
          </Card>

          {refusal && (
            <Alert
              color={refusal.code === 'candle_data_has_gaps' ? 'orange' : 'red'}
              icon={<IconAlertTriangle size={18} />}
              title={
                refusal.code === 'candle_data_has_gaps'
                  ? 'The range has missing candles'
                  : refusal.code === 'run_range_overlaps'
                    ? 'That range overlaps an existing run'
                    : 'This account will not accept new runs'
              }
            >
              <Stack gap="xs" align="flex-start">
                <Text size="sm">{refusal.message}</Text>
                {refusal.code === 'candle_data_has_gaps' && (
                  <Text size="xs" c="dimmed">
                    Backfill from the panel above, or tick &ldquo;run anyway&rdquo; and accept a
                    result stamped as having permitted gaps.
                  </Text>
                )}
              </Stack>
            </Alert>
          )}

          <Group justify="flex-end">
            <Button variant="default" onClick={() => void navigate('/backtests')}>
              Cancel
            </Button>
            <Button
              type="submit"
              leftSection={<IconPlayerPlay size={16} />}
              loading={queue.isPending}
            >
              Queue run
            </Button>
          </Group>
        </Stack>
      </form>
    </Stack>
  );
}
