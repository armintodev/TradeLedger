import { Alert, Badge, Button, Group, Loader, Stack, Text } from '@mantine/core';
import { IconAlertTriangle, IconCircleCheck, IconDownload, IconRefresh } from '@tabler/icons-react';
import type { UseQueryResult } from '@tanstack/react-query';
import { useQueueBackfill } from '@/api/queries/marketData';
import { Instant } from '@/components/Instant';
import { ErrorState } from '@/components/States';
import { backfillWindowForAll, INTERVAL_LABELS, totalMissing } from '@/lib/marketData';
import { notifyInfo, notifySuccess } from '@/lib/notify';
import type { CandleGapResponse, CandleInterval, CandleSource } from '@/api/types';

export interface GapPanelProps {
  query: UseQueryResult<CandleGapResponse[], Error>;
  source: CandleSource | null;
  symbol: string;
  interval: CandleInterval | null;
  /** Rendered when the inputs are not complete enough to probe. */
  idleMessage?: string;
  onBackfillQueued?: (jobId: string) => void;
}

/**
 * The pre-flight for a backtest, and the gap viewer on the Market Data tab —
 * one component, because they are the same question.
 *
 * A run over gapped data silently lies, which is why the engine refuses it. So
 * this is not an error state: it is the step that tells you what to backfill.
 */
export function GapPanel({
  query,
  source,
  symbol,
  interval,
  idleMessage = 'Pick a source, symbol, interval and date range to check for missing candles.',
  onBackfillQueued,
}: GapPanelProps) {
  const backfill = useQueueBackfill();

  if (query.isError) {
    return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;
  }

  if (query.fetchStatus === 'idle' && !query.data) {
    return (
      <Text size="sm" c="dimmed">
        {idleMessage}
      </Text>
    );
  }

  if (query.isPending) {
    return (
      <Group gap="xs">
        <Loader size="xs" />
        <Text size="sm" c="dimmed">
          Checking for missing candles…
        </Text>
      </Group>
    );
  }

  const gaps = query.data ?? [];

  if (gaps.length === 0) {
    return (
      <Alert color="teal" variant="light" icon={<IconCircleCheck size={18} />}>
        <Group justify="space-between" wrap="wrap" gap="sm">
          <Text size="sm">Every candle in this range is present.</Text>
          <Button
            size="compact-xs"
            variant="subtle"
            leftSection={<IconRefresh size={13} />}
            onClick={() => void query.refetch()}
          >
            Re-check
          </Button>
        </Group>
      </Alert>
    );
  }

  const missing = totalMissing(gaps);
  const label = interval ? INTERVAL_LABELS[interval] : '';

  function queueBackfill() {
    if (!source || !interval) {
      return;
    }

    // A gap's `to` is the open time of its last *missing* bar, so the range has
    // to extend one interval past it or the API refuses it as empty.
    const range = backfillWindowForAll(gaps, interval);

    if (!range) {
      return;
    }

    backfill.mutate(
      { source, symbol: symbol.trim().toUpperCase(), interval, ...range },
      {
        onSuccess: (job) => {
          notifySuccess('Backfill queued. The gaps will clear as it runs.', symbol);
          onBackfillQueued?.(job.id);
          // Re-check once the worker has had a chance to write something.
          setTimeout(() => void query.refetch(), 4000);
        },
      },
    );
  }

  return (
    <Alert color="orange" variant="light" icon={<IconAlertTriangle size={18} />}>
      <Stack gap="sm">
        <Text size="sm">
          <strong>
            {missing.toLocaleString('en-US')} {label} candle{missing === 1 ? '' : 's'}
          </strong>{' '}
          missing across {gaps.length} gap{gaps.length === 1 ? '' : 's'}. A backtest over a hole in
          the data silently lies, so a run is refused unless gaps are explicitly permitted.
        </Text>

        <Stack gap={4}>
          {gaps.slice(0, 6).map((gap) => (
            <Group key={`${gap.from}-${gap.to}`} gap="xs" wrap="nowrap">
              <Instant value={gap.from} size="xs" />
              <Text size="xs" c="dimmed">
                →
              </Text>
              <Instant value={gap.to} size="xs" />
              <Badge size="xs" variant="light" color="orange">
                {gap.missingCount} bar{gap.missingCount === 1 ? '' : 's'}
              </Badge>
            </Group>
          ))}

          {gaps.length > 6 && (
            <Text size="xs" c="dimmed">
              and {gaps.length - 6} more
            </Text>
          )}
        </Stack>

        <Group gap="xs">
          <Button
            size="compact-sm"
            variant="light"
            leftSection={<IconDownload size={14} />}
            loading={backfill.isPending}
            disabled={!source || !interval}
            onClick={queueBackfill}
          >
            Backfill this range
          </Button>
          <Button
            size="compact-sm"
            variant="subtle"
            leftSection={<IconRefresh size={14} />}
            loading={query.isFetching}
            onClick={() => {
              void query.refetch();
              notifyInfo('Re-checking…');
            }}
          >
            Re-check
          </Button>
        </Group>
      </Stack>
    </Alert>
  );
}
