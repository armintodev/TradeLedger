import { useEffect, useMemo, useRef, useState } from 'react';
import {
  Accordion,
  Alert,
  Button,
  Card,
  Grid,
  Group,
  Kbd,
  MultiSelect,
  NumberInput,
  Rating,
  Select,
  Stack,
  Text,
  Textarea,
  TextInput,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconAlertTriangle, IconDeviceFloppy } from '@tabler/icons-react';
import { toOptions, useTaxonomy, type TaxonomyGroups } from '@/api/queries/taxonomy';
import { useJournalTrade } from '@/api/queries/trades';
import { ErrorState } from '@/components/States';
import { applyServerErrors, blankToNull, toNumberOrNull } from '@/lib/forms';
import type {
  JournalTradeRequest,
  MarketContextRequest,
  TaxonomyTermResponse,
  TradeDetailResponse,
} from '@/api/types';

interface JournalFormValues {
  strategyId: string | null;
  timeframeId: string | null;
  entryTypeId: string | null;
  exitTypeId: string | null;
  entryMentalStateId: string | null;
  exitMentalStateId: string | null;
  stopLossPrice: number | string;
  takeProfitPrice: number | string;
  mistakeIds: string[];
  trackingIds: string[];
  rating: number;
  memo: string;
  tag: string;
  postTradeTag: string;
  context: Record<keyof MarketContextRequest, string>;
}

const CONTEXT_FIELDS: { key: keyof MarketContextRequest; label: string }[] = [
  { key: 'total2', label: 'Total2' },
  { key: 'btcDominance', label: 'BTC.D' },
  { key: 'usdtDominance', label: 'USDT.D' },
  { key: 'marketTrend', label: 'Market trend' },
  { key: 'sma', label: 'SMA' },
  { key: 'marketSession', label: 'Session (as you saw it)' },
  { key: 'btcPair', label: 'BTC pair' },
  { key: 'rsi', label: 'RSI' },
  { key: 'volume', label: 'Volume' },
  { key: 'candleShape', label: 'Candle shape' },
];

export interface JournalFormProps {
  trade: TradeDetailResponse;
  /** Review sends `markReviewed: true` and advances; edit just saves. */
  mode: 'review' | 'edit';
  onSaved: (trade: TradeDetailResponse) => void;
  onCancel?: () => void;
  onSkip?: () => void;
  /** Ctrl/Cmd+Enter saves, Esc skips, 1–5 set the rating. Review queue only. */
  shortcuts?: boolean;
  submitLabel?: string;
}

export function JournalForm({
  trade,
  mode,
  onSaved,
  onCancel,
  onSkip,
  shortcuts = false,
  submitLabel,
}: JournalFormProps) {
  const taxonomy = useTaxonomy();
  const journal = useJournalTrade();
  const [unmatched, setUnmatched] = useState<string[]>([]);
  const formRef = useRef<HTMLFormElement>(null);

  const initialValues = useMemo<JournalFormValues>(
    () => toFormValues(trade, taxonomy.groups),
    [trade, taxonomy.groups],
  );

  const form = useForm<JournalFormValues>({ initialValues });

  // The queue swaps the trade under the same mounted form, so the values have
  // to follow it rather than staying on the trade that was there first. The
  // taxonomy is a dependency too: until it lands there is no way to turn the
  // response's term *names* back into the ids the pickers need.
  useEffect(() => {
    form.setValues(initialValues);
    form.resetDirty(initialValues);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trade.id, taxonomy.data]);

  useEffect(() => {
    if (!shortcuts) {
      return;
    }

    function onKeyDown(event: KeyboardEvent) {
      const target = event.target as HTMLElement | null;
      const typing =
        target instanceof HTMLInputElement ||
        target instanceof HTMLTextAreaElement ||
        target?.isContentEditable === true;

      if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') {
        event.preventDefault();
        formRef.current?.requestSubmit();
        return;
      }

      if (event.key === 'Escape' && onSkip) {
        event.preventDefault();
        onSkip();
        return;
      }

      // A bare digit must not hijack a number being typed into a field.
      if (!typing && /^[1-5]$/.test(event.key)) {
        event.preventDefault();
        form.setFieldValue('rating', Number(event.key));
      }
    }

    window.addEventListener('keydown', onKeyDown);

    return () => window.removeEventListener('keydown', onKeyDown);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [shortcuts, onSkip]);

  function handleSubmit(values: JournalFormValues) {
    setUnmatched([]);
    form.clearErrors();

    journal.mutate(
      { id: trade.id, body: toRequest(values, initialValues, mode === 'review') },
      {
        onSuccess: (saved) => onSaved(saved),
        onError: (error) => {
          // A 400 lands on the offending inputs; anything the form has no field
          // for is shown above it rather than disappearing.
          setUnmatched(
            applyServerErrors(form, error, {
              StopLossPrice: 'stopLossPrice',
              TakeProfitPrice: 'takeProfitPrice',
              Rating: 'rating',
              Memo: 'memo',
              Tag: 'tag',
              PostTradeTag: 'postTradeTag',
            }),
          );
        },
      },
    );
  }

  if (taxonomy.isError) {
    return <ErrorState error={taxonomy.error} onRetry={() => void taxonomy.refetch()} />;
  }

  const groups = taxonomy.groups;
  const loading = taxonomy.isPending;

  return (
    <form ref={formRef} onSubmit={form.onSubmit(handleSubmit)} noValidate>
      <Stack gap="md">
        {unmatched.length > 0 && (
          <Alert color="red" icon={<IconAlertTriangle size={18} />}>
            <Stack gap={2}>
              {unmatched.map((message) => (
                <Text key={message} size="sm">
                  {message}
                </Text>
              ))}
            </Stack>
          </Alert>
        )}

        <Card padding="md">
          <Grid gap="sm">
            <Grid.Col span={{ base: 12, sm: 6 }}>
              <Select
                label="Strategy"
                placeholder={loading ? 'Loading…' : 'Pick a strategy'}
                data={toOptions(groups.Strategy)}
                searchable
                disabled={loading}
                data-autofocus
                {...form.getInputProps('strategyId')}
              />
            </Grid.Col>

            <Grid.Col span={{ base: 12, sm: 6 }}>
              <Select
                label="Timeframe"
                placeholder={loading ? 'Loading…' : 'Pick a timeframe'}
                data={toOptions(groups.Timeframe)}
                disabled={loading}
                {...form.getInputProps('timeframeId')}
              />
            </Grid.Col>

            <Grid.Col span={{ base: 12, sm: 6 }}>
              <Select
                label="Entry type"
                placeholder="How you got in"
                data={toOptions(groups.EntryType)}
                disabled={loading}
                {...form.getInputProps('entryTypeId')}
              />
            </Grid.Col>

            <Grid.Col span={{ base: 12, sm: 6 }}>
              <Select
                label="Exit type"
                placeholder="How you got out"
                data={toOptions(groups.ExitType)}
                disabled={loading}
                {...form.getInputProps('exitTypeId')}
              />
            </Grid.Col>

            <Grid.Col span={{ base: 12, sm: 6 }}>
              <Select
                label="Entry mental state"
                placeholder="How you felt going in"
                data={toOptions(groups.MentalState)}
                disabled={loading}
                {...form.getInputProps('entryMentalStateId')}
              />
            </Grid.Col>

            <Grid.Col span={{ base: 12, sm: 6 }}>
              <Select
                label="Exit mental state"
                placeholder="How you felt coming out"
                data={toOptions(groups.MentalState)}
                disabled={loading}
                {...form.getInputProps('exitMentalStateId')}
              />
            </Grid.Col>

            <Grid.Col span={{ base: 12, sm: 6 }}>
              <NumberInput
                label="Stop loss"
                description="Setting this recomputes achieved R — a synced trade has no stop from the exchange."
                placeholder="Price"
                min={0}
                decimalScale={10}
                hideControls
                {...form.getInputProps('stopLossPrice')}
              />
            </Grid.Col>

            <Grid.Col span={{ base: 12, sm: 6 }}>
              <NumberInput
                label="Take profit"
                placeholder="Price"
                min={0}
                decimalScale={10}
                hideControls
                {...form.getInputProps('takeProfitPrice')}
              />
            </Grid.Col>

            <Grid.Col span={{ base: 12, sm: 6 }}>
              <MultiSelect
                label="Mistakes"
                placeholder="What went wrong"
                data={toOptions(groups.Mistake)}
                searchable
                clearable
                disabled={loading}
                {...form.getInputProps('mistakeIds')}
              />
            </Grid.Col>

            <Grid.Col span={{ base: 12, sm: 6 }}>
              <MultiSelect
                label="Trackings"
                placeholder="What you are watching for"
                data={toOptions(groups.Tracking)}
                searchable
                clearable
                disabled={loading}
                {...form.getInputProps('trackingIds')}
              />
            </Grid.Col>

            <Grid.Col span={{ base: 12, sm: 4 }}>
              <Text size="sm" fw={500} mb={6}>
                Rating
              </Text>
              <Group gap="sm">
                <Rating
                  value={form.values.rating}
                  onChange={(value) => form.setFieldValue('rating', value)}
                  count={5}
                />
                {shortcuts && (
                  <Text size="xs" c="dimmed">
                    <Kbd>1</Kbd>–<Kbd>5</Kbd>
                  </Text>
                )}
              </Group>
              {form.errors.rating && (
                <Text size="xs" c="red" mt={4}>
                  {form.errors.rating}
                </Text>
              )}
            </Grid.Col>

            <Grid.Col span={{ base: 6, sm: 4 }}>
              <TextInput label="Tag" placeholder="PS tag" {...form.getInputProps('tag')} />
            </Grid.Col>

            <Grid.Col span={{ base: 6, sm: 4 }}>
              <TextInput
                label="Post-trade tag"
                placeholder="After the fact"
                {...form.getInputProps('postTradeTag')}
              />
            </Grid.Col>

            <Grid.Col span={12}>
              <Textarea
                label="Memo"
                placeholder="What happened, and what you would do differently."
                autosize
                minRows={3}
                maxRows={10}
                {...form.getInputProps('memo')}
              />
            </Grid.Col>
          </Grid>
        </Card>

        <Accordion variant="contained" defaultValue={hasContext(trade) ? 'context' : null}>
          <Accordion.Item value="context">
            <Accordion.Control>
              <Text size="sm" fw={500}>
                Market context checklist
              </Text>
              <Text size="xs" c="dimmed">
                Your own notes from the time. Free text — kept exactly as typed.
              </Text>
            </Accordion.Control>
            <Accordion.Panel>
              <Grid gap="sm">
                {CONTEXT_FIELDS.map((field) => (
                  <Grid.Col key={field.key} span={{ base: 6, sm: 4, lg: 3 }}>
                    <TextInput
                      label={field.label}
                      size="sm"
                      {...form.getInputProps(`context.${field.key}`)}
                    />
                  </Grid.Col>
                ))}
              </Grid>
            </Accordion.Panel>
          </Accordion.Item>
        </Accordion>

        <Group justify="space-between" wrap="wrap" gap="sm">
          <Text size="xs" c="dimmed" maw={440}>
            The journal endpoint changes only the fields it is sent, so clearing a picker leaves the
            saved value alone — replace it instead. Mistakes and trackings are the exception: they
            are rewritten in full, so removing them all does clear them.
          </Text>

          <Group gap="sm">
            {onCancel && (
              <Button variant="default" onClick={onCancel} disabled={journal.isPending}>
                Cancel
              </Button>
            )}

            {onSkip && (
              <Button variant="default" onClick={onSkip} disabled={journal.isPending}>
                Skip {shortcuts && <Kbd ml={6}>Esc</Kbd>}
              </Button>
            )}

            <Button
              type="submit"
              loading={journal.isPending}
              leftSection={<IconDeviceFloppy size={16} />}
            >
              {submitLabel ?? (mode === 'review' ? 'Save & next' : 'Save')}
              {shortcuts && <Kbd ml={8}>Ctrl+↵</Kbd>}
            </Button>
          </Group>
        </Group>
      </Stack>
    </form>
  );
}

function hasContext(trade: TradeDetailResponse): boolean {
  return Boolean(
    trade.marketContext && Object.values(trade.marketContext).some((value) => value !== null),
  );
}

/**
 * `TradeDetailResponse` carries taxonomy *names*, not ids, so a picker cannot be
 * pre-selected directly from it. The term is found by name within its kind —
 * names are user-scoped and are the identity the trader actually uses. A term
 * renamed since the trade was journalled will not resolve, and the picker opens
 * empty rather than showing a wrong selection.
 */
function findTermId(terms: TaxonomyTermResponse[], name: string | null): string | null {
  if (!name) {
    return null;
  }

  return terms.find((term) => term.name === name)?.id ?? null;
}

function toFormValues(trade: TradeDetailResponse, groups: TaxonomyGroups): JournalFormValues {
  const context = {} as Record<keyof MarketContextRequest, string>;

  for (const field of CONTEXT_FIELDS) {
    context[field.key] = trade.marketContext?.[field.key] ?? '';
  }

  return {
    strategyId: findTermId(groups.Strategy, trade.strategyName),
    timeframeId: findTermId(groups.Timeframe, trade.timeframeName),
    entryTypeId: findTermId(groups.EntryType, trade.entryTypeName),
    exitTypeId: findTermId(groups.ExitType, trade.exitTypeName),
    entryMentalStateId: findTermId(groups.MentalState, trade.entryMentalStateName),
    exitMentalStateId: findTermId(groups.MentalState, trade.exitMentalStateName),
    stopLossPrice: trade.stopLossPrice ?? '',
    takeProfitPrice: trade.takeProfitPrice ?? '',
    mistakeIds: trade.mistakes.map((mistake) => mistake.termId),
    trackingIds: trade.trackings.map((tracking) => tracking.termId),
    rating: trade.rating ?? 0,
    memo: trade.memo ?? '',
    tag: trade.tag ?? '',
    postTradeTag: trade.postTradeTag ?? '',
    context,
  };
}

/** Only what actually changed is sent: an absent field means "leave alone". */
function toRequest(
  values: JournalFormValues,
  initial: JournalFormValues,
  markReviewed: boolean,
): JournalTradeRequest {
  const contextChanged = CONTEXT_FIELDS.some(
    (field) => values.context[field.key] !== initial.context[field.key],
  );

  const marketContext = contextChanged
    ? (Object.fromEntries(
        CONTEXT_FIELDS.map((field) => [field.key, blankToNull(values.context[field.key])]),
      ) as unknown as MarketContextRequest)
    : undefined;

  return {
    strategyId: values.strategyId ?? undefined,
    timeframeId: values.timeframeId ?? undefined,
    entryTypeId: values.entryTypeId ?? undefined,
    exitTypeId: values.exitTypeId ?? undefined,
    entryMentalStateId: values.entryMentalStateId ?? undefined,
    exitMentalStateId: values.exitMentalStateId ?? undefined,
    marketContext,
    stopLossPrice: toNumberOrNull(values.stopLossPrice) ?? undefined,
    takeProfitPrice: toNumberOrNull(values.takeProfitPrice) ?? undefined,
    mistakeIds: values.mistakeIds,
    trackingIds: values.trackingIds,
    rating: values.rating > 0 ? values.rating : undefined,
    memo: blankToNull(values.memo) ?? undefined,
    tag: blankToNull(values.tag) ?? undefined,
    postTradeTag: blankToNull(values.postTradeTag) ?? undefined,
    markReviewed: markReviewed ? true : undefined,
  };
}
