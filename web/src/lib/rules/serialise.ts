import {
  INDICATOR_META,
  MULTI_TIMEFRAME_VERSION,
  RULE_VERSION,
  type ConditionDraft,
  type NodeId,
  type OperandDraft,
  type RuleDraft,
  type RuleVersion,
} from './types';

/**
 * Draft → wire document, plus the path maps that let a server error find the
 * node it is about.
 *
 * Two rules govern the output.
 *
 * **Compact by default.** `RuleDocumentParser.CanonicalHash` hashes the
 * document's *structure*, so key order and whitespace are free — but expanding
 * `5` into `{"const": 5}`, or adding an explicit `"offset": 0`, produces a
 * different hash. That hash is the identity by which two runs are judged to have
 * used the same rule. So every defaultable field is omitted at its default, and
 * a constant is emitted as a bare number, which keeps a hand-written document
 * hash-stable through a round trip.
 *
 * **Never refuse, never substitute.** An empty period or constant serialises as
 * `null`, not as `0`. Substituting a zero would produce a silently *different,
 * valid* rule; emitting null produces an invalid one, which is the truth — and
 * the server's own message for it is better than anything invented here.
 */

export interface SerialisedRule {
  document: unknown;
  text: string;
  /** `"$.entry.long.operands[1].left"` → the node that owns it. */
  pathToNode: Map<string, NodeId>;
  nodeToPath: Map<NodeId, string>;
}

export function serialiseRule(draft: RuleDraft): SerialisedRule {
  const pathToNode = new Map<string, NodeId>();
  const nodeToPath = new Map<NodeId, string>();

  function record(path: string, id: NodeId) {
    pathToNode.set(path, id);
    nodeToPath.set(id, path);
  }

  const indicators = draft.indicators.map((indicator, index) => {
    record(`$.indicators[${index}]`, indicator.id);

    const meta = INDICATOR_META[indicator.type];
    const takesSource = meta?.takesSource ?? false;

    return {
      id: indicator.ref,
      type: indicator.type,
      // Omitted at the default, and never emitted for an indicator that derives
      // from the whole bar — the parser rejects it outright there. The default is
      // per type, so Highest omits `High` where Ema omits `Close`.
      ...(takesSource && indicator.source && indicator.source !== meta?.defaultSource
        ? { source: indicator.source }
        : {}),
      // Omitted when the indicator reads the run's own interval, which is what keeps
      // an untouched single-timeframe document hashing exactly as it did before.
      ...(indicator.interval ? { interval: indicator.interval } : {}),
      params: { period: indicator.period },
    };
  });

  const entry: Record<string, unknown> = {};

  if (draft.entry.long) {
    entry.long = serialiseCondition(draft.entry.long, '$.entry.long', record);
  }

  if (draft.entry.short) {
    entry.short = serialiseCondition(draft.entry.short, '$.entry.short', record);
  }

  // Raised only by a document that actually needs it. Emitting 2 unconditionally would
  // change the hash of every existing strategy the first time someone re-saved one.
  const version: RuleVersion = draft.indicators.some((i) => i.interval)
    ? MULTI_TIMEFRAME_VERSION
    : RULE_VERSION;

  const document = {
    version,
    indicators,
    entry,
    stopLoss: serialiseStop(draft),
  };

  return {
    document,
    text: JSON.stringify(document, null, 2),
    pathToNode,
    nodeToPath,
  };
}

function serialiseStop(draft: RuleDraft): unknown {
  const stop = draft.stopLoss;

  if (stop.kind === 'Percent') {
    return { kind: 'Percent', percent: stop.percent };
  }

  return {
    kind: 'IndicatorLevel',
    ref: stop.ref,
    ...(stop.output ? { output: stop.output } : {}),
    ...(stop.offset ? { offset: stop.offset } : {}),
    // Defaults to 0 server-side, so omitting it keeps the hash compact.
    ...(stop.bufferPercent ? { bufferPercent: stop.bufferPercent } : {}),
  };
}

function serialiseCondition(
  node: ConditionDraft,
  path: string,
  record: (path: string, id: NodeId) => void,
): unknown {
  record(path, node.id);

  switch (node.family) {
    case 'logical':
      return {
        op: node.op,
        operands: node.children.map((child, index) =>
          serialiseCondition(child, `${path}.operands[${index}]`, record),
        ),
      };

    case 'not':
      // `operand` here is a condition. `RisingFor` uses the same key for a
      // value — which is exactly why the draft keeps them apart.
      return { op: 'Not', operand: serialiseCondition(node.child, `${path}.operand`, record) };

    case 'compare':
      return {
        op: node.op,
        left: serialiseOperand(node.left, `${path}.left`, record),
        right: serialiseOperand(node.right, `${path}.right`, record),
      };

    case 'between':
      return {
        op: 'Between',
        left: serialiseOperand(node.left, `${path}.left`, record),
        low: serialiseOperand(node.low, `${path}.low`, record),
        high: serialiseOperand(node.high, `${path}.high`, record),
      };

    case 'trend':
      return {
        op: node.op,
        operand: serialiseOperand(node.value, `${path}.operand`, record),
        bars: node.bars,
      };
  }
}

function serialiseOperand(
  operand: OperandDraft,
  path: string,
  record: (path: string, id: NodeId) => void,
): unknown {
  record(path, operand.id);

  switch (operand.kind) {
    case 'const':
      // The bare-number shorthand is what a person writes, and what the parser
      // canonicalises differently from `{"const": n}`.
      return operand.value === null ? { const: null } : operand.value;

    case 'price':
      return {
        price: operand.price,
        ...(operand.offset ? { offset: operand.offset } : {}),
      };

    case 'indicator':
      return {
        ref: operand.ref,
        ...(operand.output ? { output: operand.output } : {}),
        ...(operand.offset ? { offset: operand.offset } : {}),
      };
  }
}

/**
 * Resolve a server-supplied JSON path onto a node.
 *
 * The parser reports the deepest path it can — `$.entry.long.operands[1].left.offset`
 * names a field of an operand, not the operand itself. Trailing segments are
 * stripped until something matches, and the stripped remainder comes back as
 * the field name so the right input can be marked.
 *
 * Returns null when nothing matches, so the caller can surface the raw path
 * rather than swallowing the error.
 */
export function resolveServerPath(
  path: string,
  pathToNode: Map<string, NodeId>,
): { nodeId: NodeId; field?: string } | null {
  let candidate = path;
  const stripped: string[] = [];

  while (candidate.length > 0 && candidate !== '$') {
    const nodeId = pathToNode.get(candidate);

    if (nodeId) {
      return stripped.length > 0 ? { nodeId, field: stripped.join('.') } : { nodeId };
    }

    const arrayMatch = /\[\d+\]$/.exec(candidate);

    if (arrayMatch) {
      stripped.unshift(arrayMatch[0]);
      candidate = candidate.slice(0, -arrayMatch[0].length);
      continue;
    }

    const dot = candidate.lastIndexOf('.');

    if (dot <= 0) {
      break;
    }

    stripped.unshift(candidate.slice(dot + 1));
    candidate = candidate.slice(0, dot);
  }

  return null;
}
