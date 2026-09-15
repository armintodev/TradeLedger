import { depthOf } from './tree';
import { isTradeable } from '../marketData';
import {
  INDICATOR_META,
  LIMITS,
  type ConditionDraft,
  type NodeId,
  type OperandDraft,
  type RuleDraft,
} from './types';

/**
 * A client-side mirror of the parser's structural rules.
 *
 * The server is still the authority — `POST /validate` is the only thing that
 * proves the serialiser and the real parser agree, and the only source of the
 * warmup count. This exists so the mistakes a person actually makes while
 * building a tree (a typo'd ref, a duplicate id, a missing output on a DMI)
 * appear instantly and against the right node, rather than after a round trip
 * as a JSON path.
 */

export interface Diagnostic {
  /** Null when the problem is the document as a whole. */
  nodeId: NodeId | null;
  field?: string;
  message: string;
  severity: 'error' | 'warning';
}

export function validateDraft(draft: RuleDraft): Diagnostic[] {
  const diagnostics: Diagnostic[] = [];
  const declared = new Map<string, string>();

  // ---- indicators

  if (draft.indicators.length === 0) {
    diagnostics.push({
      nodeId: null,
      message: 'At least one indicator must be declared.',
      severity: 'error',
    });
  }

  for (const indicator of draft.indicators) {
    const meta = INDICATOR_META[indicator.type];

    if (!indicator.ref.trim()) {
      diagnostics.push({
        nodeId: indicator.id,
        field: 'ref',
        message: 'Give the indicator a name so a condition can refer to it.',
        severity: 'error',
      });
    } else if (declared.has(indicator.ref.toLowerCase())) {
      diagnostics.push({
        nodeId: indicator.id,
        field: 'ref',
        message: `Duplicate indicator id '${indicator.ref}'.`,
        severity: 'error',
      });
    } else {
      declared.set(indicator.ref.toLowerCase(), indicator.type);
    }

    if (indicator.period === null) {
      diagnostics.push({
        nodeId: indicator.id,
        field: 'period',
        message: 'A whole number is required.',
        severity: 'error',
      });
    } else if (
      !Number.isInteger(indicator.period) ||
      indicator.period < LIMITS.minPeriod ||
      indicator.period > LIMITS.maxPeriod
    ) {
      diagnostics.push({
        nodeId: indicator.id,
        field: 'period',
        message: `Period must be a whole number between ${LIMITS.minPeriod} and ${LIMITS.maxPeriod}.`,
        severity: 'error',
      });
    }

    if (indicator.source && !meta.takesSource) {
      diagnostics.push({
        nodeId: indicator.id,
        field: 'source',
        message: `${indicator.type} derives from the whole bar and takes no source.`,
        severity: 'error',
      });
    }

    // Whether the interval fits the run's own is deliberately not checked here: a
    // strategy is authored without knowing what interval it will be run at. The queue
    // refuses an incompatible pairing, and the builder warns against its preview.
    if (indicator.interval && !isTradeable(indicator.interval)) {
      diagnostics.push({
        nodeId: indicator.id,
        field: 'interval',
        message: 'One-minute candles resolve intrabar fills and cannot be read by an indicator.',
        severity: 'error',
      });
    }
  }

  // ---- entries

  if (!draft.entry.long && !draft.entry.short) {
    diagnostics.push({
      nodeId: null,
      message:
        'At least one of the long or short entries must be present; a strategy that cannot enter is not a strategy.',
      severity: 'error',
    });
  }

  for (const side of ['long', 'short'] as const) {
    const root = draft.entry[side];

    if (!root) {
      continue;
    }

    if (depthOf(root) > LIMITS.maxDepth) {
      diagnostics.push({
        nodeId: root.id,
        message: `Condition nesting is deeper than ${LIMITS.maxDepth} levels.`,
        severity: 'error',
      });
    }

    walkCondition(root, declared, diagnostics);
  }

  // ---- stop loss

  const stop = draft.stopLoss;

  if (stop.kind === 'Percent') {
    if (stop.percent === null) {
      diagnostics.push({
        nodeId: null,
        field: 'stopLoss.percent',
        message: 'A percentage stop needs a percentage.',
        severity: 'error',
      });
    } else if (stop.percent < LIMITS.minStopPercent || stop.percent > LIMITS.maxStopPercent) {
      diagnostics.push({
        nodeId: null,
        field: 'stopLoss.percent',
        message: `A percentage stop must be between ${LIMITS.minStopPercent} and ${LIMITS.maxStopPercent}.`,
        severity: 'error',
      });
    }
  } else {
    if (!stop.ref.trim()) {
      diagnostics.push({
        nodeId: null,
        field: 'stopLoss.ref',
        message: 'An indicator-level stop must reference an indicator.',
        severity: 'error',
      });
    } else if (!declared.has(stop.ref.toLowerCase())) {
      diagnostics.push({
        nodeId: null,
        field: 'stopLoss.ref',
        message: `No indicator with id '${stop.ref}' is declared.`,
        severity: 'error',
      });
    } else {
      const type = declared.get(stop.ref.toLowerCase());
      const outputs = type ? INDICATOR_META[type as keyof typeof INDICATOR_META].outputs : [];

      if (outputs.length > 1 && !stop.output) {
        diagnostics.push({
          nodeId: null,
          field: 'stopLoss.output',
          message: `${type} has several outputs, so one must be named: ${outputs.join(', ')}.`,
          severity: 'error',
        });
      }
    }

    if (stop.bufferPercent < 0 || stop.bufferPercent > LIMITS.maxBufferPercent) {
      diagnostics.push({
        nodeId: null,
        field: 'stopLoss.bufferPercent',
        message: `bufferPercent must be between 0 and ${LIMITS.maxBufferPercent}.`,
        severity: 'error',
      });
    }
  }

  return diagnostics;
}

function walkCondition(
  node: ConditionDraft,
  declared: Map<string, string>,
  diagnostics: Diagnostic[],
): void {
  switch (node.family) {
    case 'logical':
      if (node.children.length < LIMITS.minOperands) {
        diagnostics.push({
          nodeId: node.id,
          message: `${node.op} needs at least ${LIMITS.minOperands} conditions.`,
          severity: 'error',
        });
      }

      if (node.children.length > LIMITS.maxOperands) {
        diagnostics.push({
          nodeId: node.id,
          message: `${node.op} takes at most ${LIMITS.maxOperands} conditions.`,
          severity: 'error',
        });
      }

      for (const child of node.children) {
        walkCondition(child, declared, diagnostics);
      }

      return;

    case 'not':
      walkCondition(node.child, declared, diagnostics);
      return;

    case 'compare':
      checkOperand(node.left, declared, diagnostics);
      checkOperand(node.right, declared, diagnostics);
      return;

    case 'between':
      checkOperand(node.left, declared, diagnostics);
      checkOperand(node.low, declared, diagnostics);
      checkOperand(node.high, declared, diagnostics);
      return;

    case 'trend':
      checkOperand(node.value, declared, diagnostics);

      if (
        !Number.isInteger(node.bars) ||
        node.bars < LIMITS.minBars ||
        node.bars > LIMITS.maxBars
      ) {
        diagnostics.push({
          nodeId: node.id,
          field: 'bars',
          message: `bars must be between ${LIMITS.minBars} and ${LIMITS.maxBars}.`,
          severity: 'error',
        });
      }
  }
}

function checkOperand(
  operand: OperandDraft,
  declared: Map<string, string>,
  diagnostics: Diagnostic[],
): void {
  if (operand.kind === 'const') {
    if (operand.value === null) {
      diagnostics.push({
        nodeId: operand.id,
        field: 'const',
        message: 'const must be a number.',
        severity: 'error',
      });
    }

    return;
  }

  if (operand.offset < 0 || operand.offset > LIMITS.maxOffset) {
    diagnostics.push({
      nodeId: operand.id,
      field: 'offset',
      message:
        operand.offset < 0
          ? `offset must be between 0 and ${LIMITS.maxOffset}. A negative offset would read the future.`
          : `offset must be between 0 and ${LIMITS.maxOffset}.`,
      severity: 'error',
    });
  }

  if (operand.kind !== 'indicator') {
    return;
  }

  if (!operand.ref.trim()) {
    diagnostics.push({
      nodeId: operand.id,
      field: 'ref',
      message: 'ref must name an indicator.',
      severity: 'error',
    });

    return;
  }

  const type = declared.get(operand.ref.toLowerCase());

  if (!type) {
    diagnostics.push({
      nodeId: operand.id,
      field: 'ref',
      message: `No indicator with id '${operand.ref}' is declared.`,
      severity: 'error',
    });

    return;
  }

  const outputs = INDICATOR_META[type as keyof typeof INDICATOR_META].outputs;

  if (outputs.length > 1 && !operand.output) {
    diagnostics.push({
      nodeId: operand.id,
      field: 'output',
      message: `${type} has several outputs, so one must be named: ${outputs.join(', ')}.`,
      severity: 'error',
    });
  }

  if (operand.output && !outputs.includes(operand.output)) {
    diagnostics.push({
      nodeId: operand.id,
      field: 'output',
      message: `${type} has no output '${operand.output}'. Supported: ${outputs.join(', ')}.`,
      severity: 'error',
    });
  }
}

/** Diagnostics indexed by node, so a card is a single Map lookup. */
export function byNode(diagnostics: Diagnostic[]): Map<NodeId, Diagnostic[]> {
  const map = new Map<NodeId, Diagnostic[]>();

  for (const diagnostic of diagnostics) {
    if (diagnostic.nodeId === null) {
      continue;
    }

    const existing = map.get(diagnostic.nodeId);

    if (existing) {
      existing.push(diagnostic);
    } else {
      map.set(diagnostic.nodeId, [diagnostic]);
    }
  }

  return map;
}

/** Problems with the document as a whole, which belong above the tree. */
export function documentDiagnostics(diagnostics: Diagnostic[]): Diagnostic[] {
  return diagnostics.filter((diagnostic) => diagnostic.nodeId === null);
}
