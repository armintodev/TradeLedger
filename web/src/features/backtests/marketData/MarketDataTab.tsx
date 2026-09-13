import { useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Grid,
  Group,
  Modal,
  Select,
  Stack,
  Switch,
  Text,
  TextInput,
  Title,
} from '@mantine/core';
import { DatePickerInput } from '@mantine/dates';
import { useDebouncedValue } from '@mantine/hooks';
import { IconAlertTriangle, IconDownload, IconTrash } from '@tabler/icons-react';
import {
  readRememberedBackfills,
  useDeleteCandles,
  useGaps,
  useQueueBackfill,
} from '@/api/queries/marketData';
import { formatInteger } from '@/lib/format';
import { buildGapQuery, intervalOptions, sourceOptions, ALL_INTERVALS } from '@/lib/marketData';
import { notifySuccess } from '@/lib/notify';
import type { CandleInterval, CandleSource } from '@/api/types';
import { BackfillJobList } from './BackfillJobList';
import { CoverageMatrix } from './CoverageMatrix';
import { CsvImportForm } from './CsvImportForm';
import { GapPanel } from './GapPanel';

/**
 * Coverage, gaps, backfill, import and delete.
 *
 * One range selector drives the gap check, the backfill and the delete, because
 * they are three things you do to the same window — and re-typing a symbol
 * three times is how you end up deleting the wrong one.
 */
export function MarketDataTab() {
  const [source, setSource] = useState<CandleSource>('BinanceFutures');
  const [symbol, setSymbol] = useState('');
  const [interval, setInterval] = useState<CandleInterval>('FourHours');
  const [from, setFrom] = useState<string | null>(null);
  const [to, setTo] = useState<string | null>(null);
  const [includeFunding, setIncludeFunding] = useState(false);
  const [deleting, setDeleting] = useState(false);

  const [jobIds, setJobIds] = useState<string[]>(() => readRememberedBackfills());
  const [cancelling, setCancelling] = useState<string[]>([]);

  const backfill = useQueueBackfill();

  // Debounced so typing a symbol does not fire a probe per keystroke.
  const [debouncedSymbol] = useDebouncedValue(symbol, 500);
  const gapQuery = buildGapQuery({ source, symbol: debouncedSymbol, interval, from, to });
  const gaps = useGaps(gapQuery);

  function rememberJob(id: string) {
    setJobIds((current) => [id, ...current.filter((known) => known !== id)].slice(0, 20));
  }

  function queueBackfill() {
    if (!gapQuery) {
      return;
    }

    backfill.mutate(
      {
        source: gapQuery.source as CandleSource,
        symbol: gapQuery.symbol,
        interval: gapQuery.interval as CandleInterval,
        from: gapQuery.from,
        to: gapQuery.to,
        includeFundingRates: includeFunding,
      },
      {
        onSuccess: (job) => {
          rememberJob(job.id);
          notifySuccess('Backfill queued.', job.symbol);
        },
      },
    );
  }

  return (
    <Stack gap="md">
      <CoverageMatrix />

      <Card padding="md">
        <Stack gap="sm">
          <Stack gap={2}>
            <Title order={5}>A range</Title>
            <Text size="xs" c="dimmed">
              Check it for holes, fetch it from the exchange, or delete it. One-minute candles are
              legal here — they are what give a run trustworthy intrabar exits.
            </Text>
          </Stack>

          <Grid gap="sm">
            <Grid.Col span={{ base: 12, sm: 3 }}>
              <Select
                label="Source"
                data={sourceOptions(['BinanceFutures', 'BinanceSpot', 'CsvImport'])}
                value={source}
                onChange={(value) => setSource((value as CandleSource) ?? 'BinanceFutures')}
                allowDeselect={false}
              />
            </Grid.Col>
            <Grid.Col span={{ base: 6, sm: 3 }}>
              <TextInput
                label="Symbol"
                placeholder="BTCUSDT"
                value={symbol}
                onChange={(event) => setSymbol(event.currentTarget.value)}
              />
            </Grid.Col>
            <Grid.Col span={{ base: 6, sm: 2 }}>
              <Select
                label="Interval"
                data={intervalOptions(ALL_INTERVALS)}
                value={interval}
                onChange={(value) => setInterval((value as CandleInterval) ?? 'FourHours')}
                allowDeselect={false}
              />
            </Grid.Col>
            <Grid.Col span={{ base: 6, sm: 2 }}>
              <DatePickerInput
                label="From"
                placeholder="Start"
                value={from}
                onChange={setFrom}
                clearable
              />
            </Grid.Col>
            <Grid.Col span={{ base: 6, sm: 2 }}>
              <DatePickerInput label="To" placeholder="End" value={to} onChange={setTo} clearable />
            </Grid.Col>
          </Grid>

          <GapPanel
            query={gaps}
            source={source}
            symbol={debouncedSymbol}
            interval={interval}
            onBackfillQueued={rememberJob}
          />

          <Group justify="space-between" wrap="wrap" gap="sm">
            <Switch
              label="Also fetch funding rates"
              description="Binance futures only, and fetched after every candle."
              checked={includeFunding}
              onChange={(event) => setIncludeFunding(event.currentTarget.checked)}
              disabled={source !== 'BinanceFutures'}
            />

            <Group gap="xs">
              <Button
                variant="light"
                leftSection={<IconDownload size={16} />}
                loading={backfill.isPending}
                // CsvImport has nothing to fetch from; the API refuses it.
                disabled={!gapQuery || source === 'CsvImport'}
                onClick={queueBackfill}
              >
                Backfill the whole range
              </Button>

              <Button
                variant="subtle"
                color="red"
                leftSection={<IconTrash size={16} />}
                disabled={!gapQuery}
                onClick={() => setDeleting(true)}
              >
                Delete candles
              </Button>
            </Group>
          </Group>

          {source === 'CsvImport' && (
            <Text size="xs" c="dimmed">
              CSV-imported candles cannot be backfilled — there is nothing to fetch them from.
              Import the missing range instead.
            </Text>
          )}
        </Stack>
      </Card>

      <BackfillJobList
        jobIds={jobIds}
        onForget={setJobIds}
        cancelling={cancelling}
        onCancelRequested={(id) => setCancelling((current) => [...current, id])}
      />

      <CsvImportForm defaultSymbol={debouncedSymbol} />

      <DeleteCandlesModal
        opened={deleting}
        onClose={() => setDeleting(false)}
        query={gapQuery}
        onDeleted={() => void gaps.refetch()}
      />
    </Stack>
  );
}

/**
 * Both bounds are required by the API precisely so a mistyped request cannot
 * empty the table, and this asks for the symbol back before it will proceed.
 */
function DeleteCandlesModal({
  opened,
  onClose,
  query,
  onDeleted,
}: {
  opened: boolean;
  onClose: () => void;
  query: ReturnType<typeof buildGapQuery>;
  onDeleted: () => void;
}) {
  const remove = useDeleteCandles();
  const [typed, setTyped] = useState('');

  const confirmed = query !== null && typed.trim().toUpperCase() === query.symbol;

  function close() {
    setTyped('');
    onClose();
  }

  return (
    <Modal opened={opened} onClose={close} title="Delete stored candles" size="md">
      <Stack gap="md">
        <Alert color="red" variant="light" icon={<IconAlertTriangle size={18} />}>
          This removes the candles permanently. A finished backtest keeps its stored results, but it
          becomes unreproducible.
        </Alert>

        {query && (
          <Stack gap={4}>
            <Text size="sm">
              {query.symbol} · {query.interval} · {query.source}
            </Text>
            <Text size="xs" c="dimmed">
              {query.from.slice(0, 10)} → {query.to.slice(0, 10)}
            </Text>
          </Stack>
        )}

        <TextInput
          label={`Type ${query?.symbol ?? 'the symbol'} to confirm`}
          value={typed}
          onChange={(event) => setTyped(event.currentTarget.value)}
        />

        <Group justify="flex-end">
          <Button variant="default" onClick={close}>
            Cancel
          </Button>
          <Button
            color="red"
            disabled={!confirmed}
            loading={remove.isPending}
            onClick={() => {
              if (!query) {
                return;
              }

              remove.mutate(query, {
                onSuccess: (response) => {
                  notifySuccess(`${formatInteger(response.deleted)} candle(s) deleted.`);
                  onDeleted();
                  close();
                },
              });
            }}
          >
            Delete
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
