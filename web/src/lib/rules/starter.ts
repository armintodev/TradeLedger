import { constOperand, indicatorOperand, newId } from './tree';
import type { RuleDraft } from './types';

/**
 * What the editor opens on.
 *
 * An empty draft is not a legal document — the parser requires at least one
 * indicator and at least one entry side — so opening blank would greet the user
 * with a wall of red. This starter validates green immediately and is a real,
 * if unremarkable, strategy: buy when price crosses above a 20-period EMA, with
 * a 2 % stop.
 */
export function starterDraft(): RuleDraft {
  const fast = {
    id: newId(),
    ref: 'fast',
    type: 'Ema' as const,
    source: 'Close' as const,
    period: 20,
  };

  return {
    version: 1,
    indicators: [fast],
    entry: {
      long: {
        id: newId(),
        family: 'compare',
        op: 'CrossesAbove',
        left: { id: newId(), kind: 'price', price: 'Close', offset: 0 },
        right: indicatorOperand('fast'),
      },
      short: null,
    },
    stopLoss: { kind: 'Percent', percent: 2 },
  };
}

/** An empty condition for a side the user is switching on. */
export function starterCondition(firstRef?: string) {
  return {
    id: newId(),
    family: 'compare' as const,
    op: 'CrossesBelow' as const,
    left: { id: newId(), kind: 'price' as const, price: 'Close' as const, offset: 0 },
    right: firstRef ? indicatorOperand(firstRef) : constOperand(),
  };
}
