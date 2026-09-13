import { lazy, Suspense, useState } from 'react';
import {
  Alert,
  Badge,
  Button,
  Card,
  Code,
  Grid,
  Group,
  Modal,
  Progress,
  Skeleton,
  Stack,
  Text,
  Title,
  Tooltip,
} from '@mantine/core';
import { IconAlertTriangle, IconArrowLeft, IconPlayerStop, IconTrash } from '@tabler/icons-react';
import { useNavigate, useParams } from 'react-router';
import {
  useBacktestAccount,
  useBacktestRun,
  useBacktestRunEquity,
  useBacktestStrategy,
  useCancelBacktestRun,
  useDeleteBacktestRun,
} from '@/api/queries/backtests';
import { ApiError } from '@/api/problem';
import { DataQualityBadge, RunStatusBadge } from '@/components/Badges';
import { Instant } from '@/components/Instant';
import { Money, Pnl } from '@/components/Money';
import { EmptyState, ErrorState, LoadingCards } from '@/components/States';
import { formatInteger, formatPercent } from '@/lib/format';
import { INTERVAL_LABELS, SOURCE_LABELS } from '@/lib/marketData';
import { formatInstant } from '@/lib/time';
import { useTimeZone } from '@/lib/timeZone';
import { notifySuccess } from '@/lib/notify';
import type { BacktestRunResponse } from '@/api/types';
import { RunResultPanel } from './RunResultPanel';
import { RunTradesTable } from './RunTradesTable';

const EquityChart = lazy(() => import('@/components/EquityChart'));

export function RunDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const run = useBacktestRun(id);
  const cancel = useCancelBacktestRun();
  const remove = useDeleteBacktestRun();

  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  if (run.isPending) {
    return <LoadingCards count={3} height={140} />;
  }

  if (run.isError) {
    // This endpoint is the one 404 in the API with no ProblemDetails body, so
    // an ErrorState here would render an empty box.
    const missing = run.error instanceof ApiError && run.error.status === 404;

    return missing ? (
      <EmptyState
        title="That run no longer exists"
        description="It may have been deleted, or the link is wrong."
        action={
          <Button variant="light" onClick={() => void navigate('/backtests')}>
            Back to runs
          </Button>
        }
      />
    ) : (
      <ErrorState error={run.error} onRetry={() => void run.refetch()} />
    );
  }

  const data = run.data;
  const live = data.status === 'Queued' || data.status === 'Running';
  const net = data.closingBalance === null ? null : data.closingBalance - data.openingBalance;

  return (
    <Stack gap="md">
      <Group justify="space-between" wrap="wrap" gap="sm">
        <Button
          variant="subtle"
          leftSection={<IconArrowLeft size={16} />}
          onClick={() => void navigate('/backtests')}
        >
          Runs
        </Button>

        <Group gap="xs">
          {live && !data.cancellationRequested && (
            <Button
              variant="light"
              color="orange"
              leftSection={<IconPlayerStop size={16} />}
              loading={cancel.isPending}
              onClick={() =>
                cancel.mutate(data.id, {
                  onSuccess: () => notifySuccess('Cancellation requested.'),
                })
              }
            >
              Cancel
            </Button>
          )}

          {!live && (
            <Button
              variant="light"
              color="red"
              leftSection={<IconTrash size={16} />}
              onClick={() => {
                setDeleteError(null);
                setDeleting(true);
              }}
            >
              Delete
            </Button>
          )}
        </Group>
      </Group>

      <RunHeader run={data} net={net} />

      {data.status === 'Failed' && data.error && (
        <Alert color="red" icon={<IconAlertTriangle size={18} />} title="This run failed">
          <Text size="sm" style={{ whiteSpace: 'pre-wrap' }}>
            {data.error}
          </Text>
        </Alert>
      )}

      {data.cancellationRequested && data.status === 'Queued' && (
        <Alert color="orange" variant="light" icon={<IconAlertTriangle size={18} />}>
          Cancellation was requested before this run started. The worker skips a queued run that has
          been cancelled, so it will stay in this state — delete it when you are done with it.
        </Alert>
      )}

      {live && <RunProgress run={data} />}

      {data.result && <RunResultPanel result={data.result} warnings={data.warnings} />}

      {data.status === 'Succeeded' && <RunEquity runId={data.id} />}

      {data.status === 'Succeeded' && <RunTradesTable runId={data.id} />}

      <Modal
        opened={deleting}
        onClose={() => setDeleting(false)}
        title="Delete this run?"
        size="md"
      >
        <Stack gap="md">
          {deleteError ? (
            <Alert color="red" icon={<IconAlertTriangle size={18} />}>
              {deleteError}
            </Alert>
          ) : (
            <Text size="sm">
              The run and all of its simulated trades will be removed permanently.
            </Text>
          )}

          <Group justify="flex-end">
            <Button variant="default" onClick={() => setDeleting(false)}>
              Cancel
            </Button>
            <Button
              color="red"
              loading={remove.isPending}
              disabled={deleteError !== null}
              onClick={() =>
                remove.mutate(data.id, {
                  onSuccess: () => {
                    notifySuccess('Run deleted.');
                    void navigate('/backtests', { replace: true });
                  },
                  onError: (error) => {
                    if (error instanceof ApiError && error.code === 'run_not_most_recent') {
                      setDeleteError(error.detail ?? error.title);
                    }
                  },
                })
              }
            >
              Delete
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  );
}

function RunHeader({ run, net }: { run: BacktestRunResponse; net: number | null }) {
  const account = useBacktestAccount(run.backtestAccountId);
  const strategy = useBacktestStrategy(run.backtestStrategyId ?? undefined);

  return (
    <Card padding="md">
      <Stack gap="sm">
        <Group justify="space-between" align="flex-start" wrap="wrap" gap="sm">
          <Stack gap={4}>
            <Group gap="xs">
              <Title order={3}>{run.symbol}</Title>
              <Badge variant="light">{run.interval ? INTERVAL_LABELS[run.interval] : ''}</Badge>
              <RunStatusBadge
                status={run.status}
                cancellationRequested={run.cancellationRequested}
              />
              <DataQualityBadge quality={run.dataQuality} />
            </Group>

            <Group gap={6}>
              <Instant value={run.from} dateOnly />
              <Text size="sm" c="dimmed">
                →
              </Text>
              <Instant value={run.to} dateOnly />
              <Text size="sm" c="dimmed">
                · {run.source ? SOURCE_LABELS[run.source] : ''}
              </Text>
            </Group>
          </Stack>

          <Stack gap={2} align="flex-end">
            <Pnl value={net} size="xl" fw={700} />
            <Text size="xs" c="dimmed">
              <Money value={run.openingBalance} size="xs" /> →{' '}
              <Money value={run.closingBalance} size="xs" />
            </Text>
          </Stack>
        </Group>

        <Grid gap="xs">
          <Fact label="Account" value={account.data?.name ?? '—'} />
          <Fact
            label="Strategy"
            value={strategy.data ? `${strategy.data.name} (now v${strategy.data.version})` : '—'}
          />
          <Fact
            label="Rule hash"
            value={
              run.ruleHash ? (
                // The run's own frozen rule is not exposed over HTTP, so the
                // hash is the only proof of what it executed — the strategy may
                // have been revised since. See `docs/backtest-impl.md` §4.
                <Tooltip
                  label={`${run.ruleHash} — the rules this run executed are not retrievable; the strategy may have changed since.`}
                  withArrow
                  multiline
                  w={280}
                >
                  <Code>{run.ruleHash.slice(0, 8)}</Code>
                </Tooltip>
              ) : (
                '—'
              )
            }
          />
          <Fact label="Risk" value={`${run.riskPercentPerPosition}%`} />
          <Fact label="Risk to reward" value={`${run.riskRewardRatio} : 1`} />
          <Fact label="Leverage" value={`${run.leverage}×`} />
          <Fact label="Taker fee" value={String(run.takerFeeRate)} />
          <Fact label="Slippage" value={String(run.slippageRate)} />
          <Fact label="Funding" value={run.includeFunding ? 'Included' : 'Ignored'} />
          <Fact label="Engine" value={`v${run.engineVersion}`} />
          <Fact label="Queued" value={<Instant value={run.queuedAt} />} />
          <Fact label="Finished" value={<Instant value={run.finishedAt} />} />
        </Grid>
      </Stack>
    </Card>
  );
}

function Fact({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <Grid.Col span={{ base: 6, sm: 4, lg: 3 }}>
      <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
        {label}
      </Text>
      <Text size="sm" component="div">
        {value}
      </Text>
    </Grid.Col>
  );
}

function RunProgress({ run }: { run: BacktestRunResponse }) {
  return (
    <Card padding="md">
      <Stack gap="xs">
        <Group justify="space-between" wrap="wrap">
          <Text size="sm" fw={500}>
            {run.status === 'Queued' ? 'Waiting for a worker' : 'Simulating'}
          </Text>
          <Text size="sm" c="dimmed">
            {formatInteger(run.barsProcessed)} of {formatInteger(run.totalBars)} bars ·{' '}
            {formatPercent(run.progressPercent, 1)}
          </Text>
        </Group>

        <Progress
          value={run.progressPercent}
          animated={run.status === 'Running'}
          striped={run.status === 'Running'}
        />

        <Text size="xs" c="dimmed">
          {/* The worker writes progress at most once every two seconds, so
              polling faster than three would only add load. */}
          Refreshing every 3 seconds.
        </Text>
      </Stack>
    </Card>
  );
}

function RunEquity({ runId }: { runId: string }) {
  const timeZone = useTimeZone();
  const equity = useBacktestRunEquity(runId);

  if (equity.isError) {
    return <ErrorState error={equity.error} onRetry={() => void equity.refetch()} />;
  }

  const points = (equity.data?.points ?? []).map((point) => ({
    at: formatInstant(point.at, { timeZone, dateOnly: true }),
    equity: point.equity,
  }));

  if (equity.data && points.length === 0) {
    return null;
  }

  return (
    <Card padding="md">
      <Stack gap="sm">
        <Group justify="space-between" align="flex-start" wrap="wrap" gap="sm">
          <Stack gap={2}>
            <Title order={5}>Equity</Title>
            <Text size="xs" c="dimmed">
              One point per closed trade. Drawdown here is measured between closed trades — the
              intrabar figure above is always the larger of the two.
            </Text>
          </Stack>

          <Group gap="lg">
            <Text size="xs" c="dimmed">
              Peak <Money value={equity.data?.peakEquity} size="xs" />
            </Text>
            <Text size="xs" c="dimmed">
              Max drawdown <Money value={equity.data?.maxDrawdown} size="xs" /> (
              {formatPercent(equity.data?.maxDrawdownPercent)})
            </Text>
          </Group>
        </Group>

        <Suspense fallback={<Skeleton height={260} radius="md" />}>
          <EquityChart data={points} />
        </Suspense>
      </Stack>
    </Card>
  );
}
