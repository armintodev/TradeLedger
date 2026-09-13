import { constOperand, indicatorOperand, newId, priceOperand } from './tree';
import {
  FAMILY_OF,
  INDICATOR_META,
  PRICE_FIELDS,
  type ComparisonOp,
  type ConditionDraft,
  type IndicatorDraft,
  type IndicatorType,
  type LogicalOp,
  type OperandDraft,
  type PriceField,
  type RuleDraft,
  type RuleOperator,
  type StopLossDraft,
  type TrendOp,
} from './types';

/**
 * Wire document → draft.
 *
 * Deliberately lenient. This runs on whatever the user pasted into the JSON tab,
 * and its job is to get them into the visual builder with as much of their
 * document intact as possible — not to reject. Real validation belongs to
 * `validateDraft` and to the server, both of which give better messages than a
 * parser failure would.
 *
 * Anything unrecognised degrades to a sensible placeholder rather than throwing,
 * so a half-written document still opens.
 */
export function parseRuleDocument(input: unknown): RuleDraft {
  const root = isRecord(input) ? input : {};

  const indicators = Array.isArray(root.indicators)
    ? root.indicators.map(parseIndicator).filter((i): i is IndicatorDraft => i !== null)
    : [];

  const entryRoot = isRecord(root.entry) ? root.entry : {};

  return {
    version: 1,
    indicators,
    entry: {
      long: parseCondition(entryRoot.long),
      short: parseCondition(entryRoot.short),
    },
    stopLoss: parseStop(root.stopLoss),
  };
}

function parseIndicator(input: unknown): IndicatorDraft | null {
  if (!isRecord(input)) {
    return null;
  }

  const type = asIndicatorType(input.type);
  const params = isRecord(input.params) ? input.params : {};

  return {
    id: newId(),
    ref: typeof input.id === 'string' ? input.id : '',
    type,
    // An indicator that takes no source must never carry one — the parser
    // rejects the document outright if it does.
    source: INDICATOR_META[type].takesSource ? (asPriceField(input.source) ?? 'Close') : null,
    period: typeof params.period === 'number' ? params.period : null,
  };
}

function parseCondition(input: unknown): ConditionDraft | null {
  if (!isRecord(input)) {
    return null;
  }

  const op = asOperator(input.op);

  if (!op) {
    return null;
  }

  switch (FAMILY_OF[op]) {
    case 'logical': {
      const operands = Array.isArray(input.operands) ? input.operands : [];
      const children = operands
        .map(parseCondition)
        .filter((child): child is ConditionDraft => child !== null);

      // A logical node needs two children to be legal. One is promoted rather
      // than left as an unrepresentable state.
      if (children.length === 1) {
        return children[0];
      }

      if (children.length === 0) {
        return null;
      }

      return { id: newId(), family: 'logical', op: op as LogicalOp, children };
    }

    case 'not': {
      const child = parseCondition(input.operand);

      return child === null ? null : { id: newId(), family: 'not', op: 'Not', child };
    }

    case 'between':
      return {
        id: newId(),
        family: 'between',
        op: 'Between',
        left: parseOperand(input.left),
        low: parseOperand(input.low),
        high: parseOperand(input.high),
      };

    case 'trend':
      // `operand` is a *value* here, unlike on a Not node.
      return {
        id: newId(),
        family: 'trend',
        op: op as TrendOp,
        value: parseOperand(input.operand),
        bars: typeof input.bars === 'number' ? input.bars : 3,
      };

    default:
      return {
        id: newId(),
        family: 'compare',
        op: op as ComparisonOp,
        left: parseOperand(input.left),
        right: parseOperand(input.right),
      };
  }
}

function parseOperand(input: unknown): OperandDraft {
  // The bare-number shorthand the parser accepts for a constant.
  if (typeof input === 'number') {
    return constOperand(input);
  }

  if (!isRecord(input)) {
    return constOperand();
  }

  if (typeof input.ref === 'string') {
    return {
      id: newId(),
      kind: 'indicator',
      ref: input.ref,
      output: typeof input.output === 'string' ? input.output : null,
      offset: typeof input.offset === 'number' ? input.offset : 0,
    };
  }

  const price = asPriceField(input.price);

  if (price) {
    return {
      id: newId(),
      kind: 'price',
      price,
      offset: typeof input.offset === 'number' ? input.offset : 0,
    };
  }

  if ('const' in input) {
    return constOperand(typeof input.const === 'number' ? input.const : null);
  }

  return constOperand();
}

function parseStop(input: unknown): StopLossDraft {
  if (!isRecord(input)) {
    return { kind: 'Percent', percent: 2 };
  }

  if (typeof input.kind === 'string' && input.kind.toLowerCase() === 'indicatorlevel') {
    return {
      kind: 'IndicatorLevel',
      ref: typeof input.ref === 'string' ? input.ref : '',
      output: typeof input.output === 'string' ? input.output : null,
      offset: typeof input.offset === 'number' ? input.offset : 0,
      bufferPercent: typeof input.bufferPercent === 'number' ? input.bufferPercent : 0,
    };
  }

  return {
    kind: 'Percent',
    percent: typeof input.percent === 'number' ? input.percent : null,
  };
}

// ---------------------------------------------------------------- coercion

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function asIndicatorType(value: unknown): IndicatorType {
  if (typeof value !== 'string') {
    return 'Ema';
  }

  const match = (Object.keys(INDICATOR_META) as IndicatorType[]).find(
    (type) => type.toLowerCase() === value.toLowerCase(),
  );

  return match ?? 'Ema';
}

function asPriceField(value: unknown): PriceField | null {
  if (typeof value !== 'string') {
    return null;
  }

  return PRICE_FIELDS.find((field) => field.toLowerCase() === value.toLowerCase()) ?? null;
}

function asOperator(value: unknown): RuleOperator | null {
  if (typeof value !== 'string') {
    return null;
  }

  return (
    (Object.keys(FAMILY_OF) as RuleOperator[]).find(
      (op) => op.toLowerCase() === value.toLowerCase(),
    ) ?? null
  );
}

/** Re-exported so the editor can build a starter without importing tree.ts. */
export { constOperand, indicatorOperand, priceOperand };
