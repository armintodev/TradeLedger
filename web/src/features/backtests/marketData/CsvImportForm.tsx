import { useState } from 'react';
import {
  Alert,
  Button,
  Card,
  FileInput,
  Grid,
  Group,
  List,
  Select,
  Stack,
  Text,
  TextInput,
  Title,
} from '@mantine/core';
import { IconAlertTriangle, IconFileSpreadsheet, IconUpload } from '@tabler/icons-react';
import { MAX_IMPORT_BYTES, useImportCandles } from '@/api/queries/marketData';
import { ApiError } from '@/api/problem';
import { formatInteger } from '@/lib/format';
import { ALL_INTERVALS, intervalOptions, sourceOptions } from '@/lib/marketData';
import { notifySuccess } from '@/lib/notify';
import type { CandleImportResponse, CandleInterval, CandleSource } from '@/api/types';

/**
 * The only way to get candles in without touching the network — a backfill goes
 * to Binance through the same per-user egress proxy as Bitunix and fails closed
 * when none is configured.
 */
export function CsvImportForm({ defaultSymbol }: { defaultSymbol?: string }) {
  const importCandles = useImportCandles();

  const [file, setFile] = useState<File | null>(null);
  const [source, setSource] = useState<CandleSource>('CsvImport');
  const [symbol, setSymbol] = useState(defaultSymbol ?? '');
  const [interval, setInterval] = useState<CandleInterval>('FourHours');
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<CandleImportResponse | null>(null);

  const tooLarge = file !== null && file.size > MAX_IMPORT_BYTES;

  function submit() {
    setError(null);
    setResult(null);

    if (!file || !symbol.trim()) {
      setError('Pick a file and enter the symbol it holds.');
      return;
    }

    if (tooLarge) {
      setError(
        `That file is ${(file.size / 1024 / 1024).toFixed(1)} MB. The practical limit is ${
          MAX_IMPORT_BYTES / 1024 / 1024
        } MB — split it and import the halves.`,
      );
      return;
    }

    importCandles.mutate(
      { file, source, symbol: symbol.trim().toUpperCase(), interval },
      {
        onSuccess: (response) => {
          setResult(response);
          setFile(null);
          notifySuccess(
            `${formatInteger(response.rowsInserted)} candle(s) imported.`,
            response.symbol,
          );
        },
        onError: (cause) => {
          if (cause instanceof ApiError) {
            // Kestrel rejects an oversized body before the endpoint runs, so
            // this arrives as a malformed request rather than a field error.
            setError(
              cause.code === 'malformed_request'
                ? 'The server rejected the upload before reading it — almost certainly because the file is too large. Split it and try again.'
                : (cause.errors?.file?.join(' ') ?? cause.detail ?? cause.title),
            );
          }
        },
      },
    );
  }

  return (
    <Card padding="md">
      <Stack gap="sm">
        <Stack gap={2}>
          <Title order={5}>Import a CSV</Title>
          <Text size="xs" c="dimmed">
            A TradingView-style export. Needs a time column plus open, high, low and close; volume
            is optional. Rows already stored are skipped, so re-importing the same file is a no-op.
          </Text>
        </Stack>

        {error && (
          <Alert color="red" variant="light" icon={<IconAlertTriangle size={18} />}>
            {error}
          </Alert>
        )}

        {result && (
          <Alert color="teal" variant="light" icon={<IconFileSpreadsheet size={18} />}>
            <Stack gap={4}>
              <Text size="sm">
                {formatInteger(result.rowsParsed)} row(s) parsed ·{' '}
                {formatInteger(result.rowsInserted)} inserted ·{' '}
                {formatInteger(result.rowsSkippedAsDuplicate)} already present
              </Text>
              {result.firstOpenTime && (
                <Text size="xs" c="dimmed">
                  {result.firstOpenTime.slice(0, 10)} → {result.lastOpenTime?.slice(0, 10)}
                </Text>
              )}
              {result.warnings.length > 0 && (
                <List size="xs" spacing={2}>
                  {result.warnings.map((warning) => (
                    <List.Item key={warning}>{warning}</List.Item>
                  ))}
                </List>
              )}
            </Stack>
          </Alert>
        )}

        <Grid gap="sm">
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <FileInput
              label="File"
              placeholder="Choose a .csv"
              accept=".csv,text/csv,text/plain"
              value={file}
              onChange={setFile}
              clearable
              error={tooLarge ? 'Too large' : undefined}
              leftSection={<IconFileSpreadsheet size={16} />}
            />
          </Grid.Col>
          <Grid.Col span={{ base: 6, sm: 2 }}>
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
          <Grid.Col span={{ base: 12, sm: 2 }}>
            <Select
              label="Store as"
              description="Source is part of a run's identity"
              data={sourceOptions(['CsvImport', 'BinanceFutures', 'BinanceSpot'])}
              value={source}
              onChange={(value) => setSource((value as CandleSource) ?? 'CsvImport')}
              allowDeselect={false}
            />
          </Grid.Col>
        </Grid>

        <Group justify="flex-end">
          <Button
            leftSection={<IconUpload size={16} />}
            loading={importCandles.isPending}
            disabled={!file || tooLarge}
            onClick={submit}
          >
            Import
          </Button>
        </Group>
      </Stack>
    </Card>
  );
}
