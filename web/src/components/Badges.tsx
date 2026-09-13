import { Badge, Group, Tooltip } from '@mantine/core';
import type {
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
