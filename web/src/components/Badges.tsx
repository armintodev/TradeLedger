import { Badge, Group, Tooltip } from '@mantine/core';
import type {
  BacktestExitReason,
  BacktestStatus,
  DataQuality,
  IntrabarResolution,
  MarketDataJobStatus,
  MarketSessionValue,
  ReviewState,
  TradeOrigin,
  TradeOutcome,
  TradeSide,
} from '@/api/types';

const OUTCOME_COLORS: Record<TradeOutcome, string> = {
  Open: 'blue',
  Win: 'teal',
  Loss: 'red',
  Breakeven: 'gray',
};

export function OutcomeBadge({ outcome }: { outcome: TradeOutcome }) {
  return (
    <Badge color={OUTCOME_COLORS[outcome]} variant="light" size="sm">
      {outcome}
    </Badge>
  );
}

export function SideBadge({ side }: { side: TradeSide }) {
  return (
    <Badge color={side === 'Long' ? 'teal' : 'red'} variant="outline" size="sm">
      {side}
    </Badge>
  );
}

export function ReviewStateBadge({ state }: { state: ReviewState }) {
  return (
    <Badge color={state === 'Reviewed' ? 'gray' : 'yellow'} variant="light" size="sm">
      {state}
    </Badge>
  );
}

export function OriginBadge({ origin }: { origin: TradeOrigin }) {
  return (
    <Badge color={origin === 'Synced' ? 'indigo' : 'grape'} variant="dot" size="sm">
      {origin}
    </Badge>
  );
}

const SESSION_COLORS: Record<string, string> = {
  Tokyo: 'orange',
  London: 'blue',
  NewYork: 'violet',
};

/**
 * `MarketSession` is a `[Flags]` enum, so an overlap arrives as
 * `"Tokyo, London"`. One chip per flag, because the overlap is the window the
 * trader actually cares about and both halves should be visible.
 */
export function SessionChips({ value }: { value: MarketSessionValue | null | undefined }) {
  if (!value || value === 'None') {
    return (
      <Badge color="gray" variant="light" size="sm">
        No session
      </Badge>
    );
  }

  const flags = value
    .split(',')
    .map((flag) => flag.trim())
    .filter(Boolean);

  return (
    <Group gap={4} wrap="nowrap">
      {flags.map((flag) => (
        <Badge key={flag} color={SESSION_COLORS[flag] ?? 'gray'} variant="light" size="sm">
          {flag === 'NewYork' ? 'New York' : flag}
        </Badge>
      ))}
    </Group>
  );
}

export function PlannedBadge({ isPlanned }: { isPlanned: boolean }) {
  return (
    <Tooltip
      label={isPlanned ? 'Matched to a pre-trade plan' : 'No plan existed when this was opened'}
      withArrow
    >
      <Badge color={isPlanned ? 'teal' : 'orange'} variant="light" size="sm">
        {isPlanned ? 'Planned' : 'Unplanned'}
      </Badge>
    </Tooltip>
  );
}

const RUN_STATUS_COLORS: Record<BacktestStatus, string> = {
  Queued: 'gray',
  Running: 'blue',
  Succeeded: 'teal',
  Failed: 'red',
  Cancelled: 'orange',
};

export function RunStatusBadge({
  status,
  cancellationRequested,
}: {
  status: BacktestStatus;
  cancellationRequested?: boolean;
}) {
  // A queued run whose cancellation was requested is excluded from the worker's
  // pickup query, so it will never start. Saying "Queued" would be a lie.
  if (cancellationRequested && status === 'Queued') {
    return (
      <Tooltip
        label="Cancellation was requested before this run started, so it will never begin."
        withArrow
      >
        <Badge color="orange" variant="light" size="sm">
          Cancelled before start
        </Badge>
      </Tooltip>
    );
  }

  if (cancellationRequested && status === 'Running') {
    return (
      <Badge color="orange" variant="light" size="sm">
        Cancelling…
      </Badge>
    );
  }

  return (
    <Badge color={RUN_STATUS_COLORS[status]} variant="light" size="sm">
      {status}
    </Badge>
  );
}

const JOB_STATUS_COLORS: Record<MarketDataJobStatus, string> = {
  Queued: 'gray',
  Running: 'blue',
  Succeeded: 'teal',
  Failed: 'red',
  Cancelled: 'orange',
};

export function JobStatusBadge({ status }: { status: MarketDataJobStatus }) {
  return (
    <Badge color={JOB_STATUS_COLORS[status]} variant="light" size="sm">
      {status}
    </Badge>
  );
}

/**
 * `DataQuality` is set from the request's `allowGaps` at queue time and never
 * corrected, so it records that gaps were *permitted* — not that any were
 * found. The label says exactly that much and no more.
 * See `docs/backtest-impl.md` §1.
 */
export function DataQualityBadge({ quality }: { quality: DataQuality }) {
  if (quality === 'Clean') {
    return null;
  }

  return (
    <Tooltip
      label="This run was queued with gaps permitted. The API records the permission, not whether candles were actually missing."
      withArrow
      multiline
      w={280}
    >
      <Badge color="yellow" variant="light" size="sm">
        Gaps permitted
      </Badge>
    </Tooltip>
  );
}

const EXIT_REASON_COLORS: Record<BacktestExitReason, string> = {
  StopLoss: 'red',
  TakeProfit: 'teal',
  Liquidation: 'grape',
  EndOfData: 'gray',
};

const EXIT_REASON_LABELS: Record<BacktestExitReason, string> = {
  StopLoss: 'Stop',
  TakeProfit: 'Target',
  Liquidation: 'Liquidated',
  EndOfData: 'End of data',
};

export function ExitReasonBadge({ reason }: { reason: BacktestExitReason | null }) {
  if (!reason) {
    return (
      <Badge color="gray" variant="light" size="sm">
        Open
      </Badge>
    );
  }

  return (
    <Badge color={EXIT_REASON_COLORS[reason]} variant="light" size="sm">
      {EXIT_REASON_LABELS[reason]}
    </Badge>
  );
}

const RESOLUTION_LABELS: Record<IntrabarResolution, string> = {
  Unambiguous: 'Unambiguous',
  ResolvedByMinute: 'By minute data',
  AssumedWithinMinute: 'Assumed',
  AssumedNoMinuteData: 'Assumed — no 1m data',
};

const RESOLUTION_HINTS: Record<IntrabarResolution, string> = {
  Unambiguous: 'The bar touched only one of the stop and the target.',
  ResolvedByMinute: 'The bar held both, and one-minute candles separated them.',
  AssumedWithinMinute:
    'The bar held both and one-minute data could not separate them, so the worse outcome was assumed.',
  AssumedNoMinuteData:
    'The bar held both and there were no one-minute candles to drill into, so the worse outcome was assumed. Backfill 1m for this range to get a trustworthy exit.',
};

/** How a trade's exit was decided. An assumed exit is a guess, and a pessimistic one. */
export function ResolutionBadge({ resolution }: { resolution: IntrabarResolution }) {
  const assumed = resolution.startsWith('Assumed');

  return (
    <Tooltip label={RESOLUTION_HINTS[resolution]} withArrow multiline w={280}>
      <Badge
        color={assumed ? 'orange' : resolution === 'ResolvedByMinute' ? 'blue' : 'gray'}
        variant="light"
        size="sm"
      >
        {RESOLUTION_LABELS[resolution]}
      </Badge>
    </Tooltip>
  );
}
