/**
 * The editor's model of a rule document.
 *
 * This deliberately does **not** mirror the wire shape. On the wire the child
 * key differs by operator family — `And` takes `operands`, `Not` takes
 * `operand`, a comparison takes `left`/`right`, `Between` takes `left`/`low`/
 * `high`, and `RisingFor` takes `operand` again but meaning a *value* rather
 * than a *condition*. Modelling that directly spreads the confusion through
 * every component.
 *
 * Here the `family` discriminant carries both the child arity and the child
 * type, so `Not`'s condition child (`child`) and `RisingFor`'s value child
 * (`value`) cannot be mixed up. Wire key names exist in exactly one file:
 * `serialise.ts`.
 */

/** Client-only, never serialised. Lets a card address itself without a path. */
export type NodeId = string;

export type PriceField = 'Close' | 'Open' | 'High' | 'Low' | 'Volume';

export type IndicatorType = 'Sma' | 'Ema' | 'Rsi' | 'Dmi' | 'Adx';

export type ComparisonOp =
  | 'GreaterThan'
  | 'GreaterOrEqual'
  | 'LessThan'
  | 'LessOrEqual'
  | 'EqualTo'
  | 'NotEqualTo'
  | 'CrossesAbove'
  | 'CrossesBelow';

export type LogicalOp = 'And' | 'Or';
export type TrendOp = 'RisingFor' | 'FallingFor';

export type RuleOperator = ComparisonOp | LogicalOp | TrendOp | 'Not' | 'Between';

export type ConditionFamily = 'logical' | 'not' | 'compare' | 'between' | 'trend';

export type OperandDraft =
  | { id: NodeId; kind: 'indicator'; ref: string; output: string | null; offset: number }
  | { id: NodeId; kind: 'price'; price: PriceField; offset: number }
  /** `null` while the input is empty — never silently substituted with 0. */
  | { id: NodeId; kind: 'const'; value: number | null };

export type ConditionDraft =
  | { id: NodeId; family: 'logical'; op: LogicalOp; children: ConditionDraft[] }
  | { id: NodeId; family: 'not'; op: 'Not'; child: ConditionDraft }
  | { id: NodeId; family: 'compare'; op: ComparisonOp; left: OperandDraft; right: OperandDraft }
  | {
      id: NodeId;
      family: 'between';
      op: 'Between';
      left: OperandDraft;
      low: OperandDraft;
      high: OperandDraft;
    }
  | { id: NodeId; family: 'trend'; op: TrendOp; value: OperandDraft; bars: number };

export interface IndicatorDraft {
  /** Editor row key. Distinct from `ref`, which is the DSL's own `id`. */
  id: NodeId;
  ref: string;
  type: IndicatorType;
  /** Null for Dmi and Adx, which derive from the whole bar and take no source. */
  source: PriceField | null;
  period: number | null;
}

export type StopLossDraft =
  | { kind: 'Percent'; percent: number | null }
  | {
      kind: 'IndicatorLevel';
      ref: string;
      output: string | null;
      offset: number;
      bufferPercent: number;
    };

export interface RuleDraft {
  version: 1;
  indicators: IndicatorDraft[];
  /** At least one side must be present: a strategy that cannot enter is not one. */
  entry: { long: ConditionDraft | null; short: ConditionDraft | null };
  stopLoss: StopLossDraft;
}

export type EntrySide = 'long' | 'short';

// ---------------------------------------------------------------- vocabulary

export const FAMILY_OF: Record<RuleOperator, ConditionFamily> = {
  And: 'logical',
  Or: 'logical',
  Not: 'not',
  GreaterThan: 'compare',
  GreaterOrEqual: 'compare',
  LessThan: 'compare',
  LessOrEqual: 'compare',
  EqualTo: 'compare',
  NotEqualTo: 'compare',
  CrossesAbove: 'compare',
  CrossesBelow: 'compare',
  Between: 'between',
  RisingFor: 'trend',
  FallingFor: 'trend',
};

export const OPERATOR_LABELS: Record<RuleOperator, string> = {
  And: 'All of',
  Or: 'Any of',
  Not: 'Not',
  GreaterThan: 'is greater than',
  GreaterOrEqual: 'is at least',
  LessThan: 'is less than',
  LessOrEqual: 'is at most',
  EqualTo: 'equals',
  NotEqualTo: 'does not equal',
  CrossesAbove: 'crosses above',
  CrossesBelow: 'crosses below',
  Between: 'is between',
  RisingFor: 'has been rising for',
  FallingFor: 'has been falling for',
};

export const ALL_OPERATORS = Object.keys(FAMILY_OF) as RuleOperator[];

export const PRICE_FIELDS: PriceField[] = ['Close', 'Open', 'High', 'Low', 'Volume'];

/**
 * Mirrors `IndicatorFactory.Definitions`. Hardcoded rather than read from
 * `GET /indicators` because client-side validation has to be synchronous —
 * and because the endpoint's own description says adding one is a code change,
 * not configuration. The endpoint is still used to show warmup multipliers.
 */
export const INDICATOR_META: Record<
  IndicatorType,
  { outputs: string[]; takesSource: boolean; warmupMultiplier: number }
> = {
  Sma: { outputs: ['Value'], takesSource: true, warmupMultiplier: 1 },
  Ema: { outputs: ['Value'], takesSource: true, warmupMultiplier: 3 },
  Rsi: { outputs: ['Value'], takesSource: true, warmupMultiplier: 5 },
  Dmi: { outputs: ['PlusDi', 'MinusDi'], takesSource: false, warmupMultiplier: 5 },
  Adx: { outputs: ['Value'], takesSource: false, warmupMultiplier: 5 },
};

export const INDICATOR_TYPES = Object.keys(INDICATOR_META) as IndicatorType[];

/** The parser's own limits, enforced here so the common mistakes cost no round trip. */
export const LIMITS = {
  maxDepth: 20,
  maxOffset: 500,
  minOperands: 2,
  maxOperands: 20,
  minPeriod: 1,
  maxPeriod: 1000,
  minBars: 1,
  maxBars: 500,
  minStopPercent: 0.1,
  maxStopPercent: 50,
  maxBufferPercent: 50,
} as const;

export const RULE_VERSION = 1;
