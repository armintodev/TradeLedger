import { useEffect, useRef, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Grid,
  Group,
  NumberInput,
  Select,
  Stack,
  Text,
  Textarea,
  TextInput,
  Title,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconArrowLeft, IconCalculator } from '@tabler/icons-react';
import { useNavigate } from 'react-router';
import { useMe } from '@/api/queries/auth';
import { useCalculatePosition, useCreatePlan } from '@/api/queries/plans';
import { toOptions, useTaxonomy } from '@/api/queries/taxonomy';
import { AccountSelect } from '@/components/Filters';
import { Money, Price, Quantity } from '@/components/Money';
import { PageHeader } from '@/components/PageHeader';
import { ApiError } from '@/api/problem';
import { applyServerErrors, blankToNull, toNumberOrNull } from '@/lib/forms';
import { formatFractionAsPercent } from '@/lib/format';
import { notifySuccess } from '@/lib/notify';
import type { PositionSizeResult, TradeSide } from '@/api/types';

const DEBOUNCE_MS = 300;

export function NewPlanPage() {
  const navigate = useNavigate();
  const me = useMe();
  const taxonomy = useTaxonomy();
  const calculate = useCalculatePosition();
  const create = useCreatePlan();

  const [result, setResult] = useState<PositionSizeResult | null>(null);
  const [calcError, setCalcError] = useState<string | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const form = useForm({
    initialValues: {
      accountId: '',
      symbol: '',
      side: 'Long' as TradeSide,
      balance: '' as number | string,
      entryPrice: '' as number | string,
      stopLossPrice: '' as number | string,
      takeProfitPrice: '' as number | string,
      riskFraction: '' as number | string,
      riskReward: 2 as number | string,
      leverage: 5 as number | string,
      averageFeeRate: 0.0006 as number | string,
      strategyId: null as string | null,
      timeframeId: null as string | null,
      entryMentalStateId: null as string | null,
      notes: '',
    },
    validate: {
      symbol: (value) => (value.trim() ? null : 'Enter a symbol'),
      entryPrice: (value) => (toNumberOrNull(value) ? null : 'Enter an entry price'),
      stopLossPrice: (value) => (toNumberOrNull(value) ? null : 'Enter a stop loss'),
      riskFraction: (value) => (toNumberOrNull(value) ? null : 'Enter the risk fraction'),
      balance: (value) => (toNumberOrNull(value) !== null ? null : 'Enter the account balance'),
    },
  });

  // Defaults come from the profile: the balance the journal started with and
  // the trader's usual risk per trade.
  const applied = useRef(false);

  useEffect(() => {
    if (applied.current || !me.data) {
      return;
    }

    applied.current = true;

    form.setValues((current) => ({
      ...current,
      balance: current.balance === '' ? me.data.startingBalance : current.balance,
      riskFraction:
        current.riskFraction === '' && me.data.defaultRiskPerTrade !== null
          ? me.data.defaultRiskPerTrade
          : current.riskFraction,
    }));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [me.data]);

  const { balance, entryPrice, stopLossPrice, riskFraction, riskReward, leverage, averageFeeRate } =
    form.values;

  const parsedBalance = toNumberOrNull(balance);
  const parsedEntry = toNumberOrNull(entryPrice);
  const parsedStop = toNumberOrNull(stopLossPrice);
  const parsedRisk = toNumberOrNull(riskFraction);

  // Whether there is enough to size anything at all is derived, not stored:
  // the panel shows its prompt until all four are filled in, so a stale result
  // from a previous set of inputs is never on screen.
  const sizable =
    parsedBalance !== null && parsedEntry !== null && parsedStop !== null && parsedRisk !== null;

  // Debounced so a keystroke does not become a request. The sizing is computed
  // server-side on purpose: it must be the same arithmetic the domain would do.
  useEffect(() => {
    if (
      parsedBalance === null ||
      parsedEntry === null ||
      parsedStop === null ||
      parsedRisk === null
    ) {
      return;
    }

    if (timer.current) {
      clearTimeout(timer.current);
    }

    timer.current = setTimeout(() => {
      calculate.mutate(
        {
          balance: parsedBalance,
          entryPrice: parsedEntry,
          stopLossPrice: parsedStop,
          riskFraction: parsedRisk,
          riskReward: toNumberOrNull(riskReward) ?? 2,
          leverage: toNumberOrNull(leverage) ?? 1,
          averageFeeRate: toNumberOrNull(averageFeeRate),
        },
        {
          onSuccess: (value) => {
            setResult(value);
            setCalcError(null);
          },
          onError: (error) => {
            setResult(null);
            setCalcError(
              error instanceof ApiError ? (error.detail ?? error.title) : 'The calculation failed.',
            );
          },
        },
      );
    }, DEBOUNCE_MS);

    return () => {
      if (timer.current) {
        clearTimeout(timer.current);
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [parsedBalance, parsedEntry, parsedStop, parsedRisk, riskReward, leverage, averageFeeRate]);

  return (
    <Stack gap="md">
      <Group>
        <Button
          variant="subtle"
          leftSection={<IconArrowLeft size={16} />}
          onClick={() => void navigate('/plans')}
        >
          Plans
        </Button>
      </Group>

      <PageHeader
        title="Plan a trade"
        description="Size the position before you take it. The numbers on the right come from the same calculator the API uses."
      />

      <form
        onSubmit={form.onSubmit((values) =>
          create.mutate(
            {
              accountId: blankToNull(values.accountId),
              symbol: values.symbol.trim().toUpperCase(),
              side: values.side,
              entryPrice: toNumberOrNull(values.entryPrice) ?? 0,
              stopLossPrice: toNumberOrNull(values.stopLossPrice) ?? 0,
              takeProfitPrice:
                toNumberOrNull(values.takeProfitPrice) ?? result?.takeProfitPrice ?? null,
              riskFraction: toNumberOrNull(values.riskFraction) ?? 0,
              riskReward: toNumberOrNull(values.riskReward) ?? 2,
              leverage: toNumberOrNull(values.leverage),
              averageFeeRate: toNumberOrNull(values.averageFeeRate),
              balance: toNumberOrNull(values.balance) ?? 0,
              strategyId: values.strategyId,
              timeframeId: values.timeframeId,
              entryMentalStateId: values.entryMentalStateId,
              notes: blankToNull(values.notes),
            },
            {
              onSuccess: (plan) => {
                notifySuccess(`${plan.symbol} plan created.`);
                void navigate('/plans');
              },
              onError: (error) => applyServerErrors(form, error),
            },
          ),
        )}
      >
        <Grid gap="md">
          <Grid.Col span={{ base: 12, lg: 7 }}>
            <Card padding="md">
              <Stack gap="sm">
                <Title order={5}>The trade</Title>

                <Grid gap="sm">
                  <Grid.Col span={{ base: 12, sm: 6 }}>
                    <TextInput
                      label="Symbol"
                      placeholder="BTCUSDT"
                      {...form.getInputProps('symbol')}
                    />
                  </Grid.Col>
                  <Grid.Col span={{ base: 12, sm: 6 }}>
                    <Select
                      label="Side"
                      data={[
                        { value: 'Long', label: 'Long' },
                        { value: 'Short', label: 'Short' },
                      ]}
                      allowDeselect={false}
                      {...form.getInputProps('side')}
                    />
                  </Grid.Col>

                  <Grid.Col span={12}>
                    <AccountSelect
                      value={form.values.accountId || undefined}
                      onChange={(value) => form.setFieldValue('accountId', value ?? '')}
                      placeholder="Optional"
                      error={form.errors.accountId as string | undefined}
                    />
                  </Grid.Col>

                  <Grid.Col span={{ base: 12, sm: 4 }}>
                    <NumberInput
                      label="Entry"
                      hideControls
                      decimalScale={12}
                      {...form.getInputProps('entryPrice')}
                    />
                  </Grid.Col>
                  <Grid.Col span={{ base: 12, sm: 4 }}>
                    <NumberInput
                      label="Stop loss"
                      description="Below entry for a long, above for a short"
                      hideControls
                      decimalScale={12}
                      {...form.getInputProps('stopLossPrice')}
                    />
                  </Grid.Col>
                  <Grid.Col span={{ base: 12, sm: 4 }}>
                    <NumberInput
                      label="Take profit"
                      description={
                        result ? `Calculated: ${result.takeProfitPrice}` : 'Leave blank to use R:R'
                      }
                      hideControls
                      decimalScale={12}
                      {...form.getInputProps('takeProfitPrice')}
                    />
                  </Grid.Col>

                  <Grid.Col span={{ base: 12, sm: 6 }}>
                    <NumberInput
                      label="Balance"
                      description="What the position is sized against"
                      hideControls
                      decimalScale={8}
                      {...form.getInputProps('balance')}
                    />
                  </Grid.Col>
                  <Grid.Col span={{ base: 6, sm: 2 }}>
                    <NumberInput
                      label="Risk"
                      description="0.01 = 1%"
                      hideControls
                      decimalScale={6}
                      step={0.005}
                      {...form.getInputProps('riskFraction')}
                    />
                  </Grid.Col>
                  <Grid.Col span={{ base: 6, sm: 2 }}>
                    <NumberInput
                      label="R:R"
                      hideControls
                      decimalScale={2}
                      {...form.getInputProps('riskReward')}
                    />
                  </Grid.Col>
                  <Grid.Col span={{ base: 6, sm: 2 }}>
                    <NumberInput
                      label="Leverage"
                      hideControls
                      min={1}
                      {...form.getInputProps('leverage')}
                    />
                  </Grid.Col>

                  <Grid.Col span={{ base: 12, sm: 4 }}>
                    <NumberInput
                      label="Fee rate"
                      description="0.0006 = 0.06% taker"
                      hideControls
                      decimalScale={6}
                      {...form.getInputProps('averageFeeRate')}
                    />
                  </Grid.Col>
                  <Grid.Col span={{ base: 12, sm: 4 }}>
                    <Select
                      label="Strategy"
                      data={toOptions(taxonomy.groups.Strategy)}
                      searchable
                      clearable
                      {...form.getInputProps('strategyId')}
                    />
                  </Grid.Col>
                  <Grid.Col span={{ base: 12, sm: 4 }}>
                    <Select
                      label="Timeframe"
                      data={toOptions(taxonomy.groups.Timeframe)}
                      clearable
                      {...form.getInputProps('timeframeId')}
                    />
                  </Grid.Col>

                  <Grid.Col span={{ base: 12, sm: 6 }}>
                    <Select
                      label="Entry mental state"
                      data={toOptions(taxonomy.groups.MentalState)}
                      clearable
                      {...form.getInputProps('entryMentalStateId')}
                    />
                  </Grid.Col>

                  <Grid.Col span={12}>
                    <Textarea
                      label="Notes"
                      placeholder="Why this trade, and what would invalidate it."
                      autosize
                      minRows={2}
                      {...form.getInputProps('notes')}
                    />
                  </Grid.Col>
                </Grid>
              </Stack>
            </Card>
          </Grid.Col>

          <Grid.Col span={{ base: 12, lg: 5 }}>
            <Card padding="md" h="100%">
              <Stack gap="sm">
                <Group gap="xs">
                  <IconCalculator size={18} />
                  <Title order={5}>Position size</Title>
                </Group>

                {sizable && calcError && (
                  <Alert color="orange" variant="light">
                    {calcError}
                  </Alert>
                )}

                {(!sizable || (!result && !calcError)) && (
                  <Text size="sm" c="dimmed">
                    Fill in balance, entry, stop and risk to size the position.
                  </Text>
                )}

                {sizable && result && !calcError && (
                  <Stack gap={6}>
                    <Line label="Direction" value={result.side} />
                    <Line
                      label="Stop distance"
                      value={formatFractionAsPercent(result.stopToEntryRatio)}
                    />
                    <Line label="Risk amount" value={<Money value={result.riskAmount} />} />
                    <Line label="Quantity" value={<Quantity value={result.quantity} />} />
                    <Line label="Order value" value={<Money value={result.orderValue} />} />
                    <Line label="Margin" value={<Money value={result.margin} />} />
                    <Line label="Take profit" value={<Price value={result.takeProfitPrice} />} />
                    <Line
                      label="Estimated profit"
                      value={<Money value={result.estimatedProfit} />}
                    />
                    <Line label="Estimated loss" value={<Money value={result.estimatedLoss} />} />
                    <Line
                      label="Fees to target"
                      value={<Money value={result.feesEntryPlusTakeProfit} />}
                    />
                    <Line label="Fees to stop" value={<Money value={result.feesEntryPlusStop} />} />
                  </Stack>
                )}

                <Button type="submit" loading={create.isPending} mt="auto">
                  Create plan
                </Button>
              </Stack>
            </Card>
          </Grid.Col>
        </Grid>
      </form>
    </Stack>
  );
}

function Line({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <Group justify="space-between" gap="sm" wrap="nowrap">
      <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
        {label}
      </Text>
      <Text size="sm" ff="monospace" component="div">
        {value}
      </Text>
    </Group>
  );
}
