import {
  FAMILY_OF,
  LIMITS,
  type ComparisonOp,
  type ConditionDraft,
  type LogicalOp,
  type NodeId,
  type OperandDraft,
  type PriceField,
  type RuleOperator,
  type TrendOp,
} from './types';

/**
 * Tree surgery, addressed by node id.
 *
 * Every operation returns a new tree; nothing is mutated. Components dispatch
 * with their own id and never compute a path, so removing a sibling re-keys
 * nothing.
 */

let counter = 0;

export function newId(): NodeId {
  counter += 1;
  return `n${counter}`;
}

/** Test seam — keeps generated ids predictable. */
export function resetIds(): void {
  counter = 0;
}

// ---------------------------------------------------------------- factories

export function constOperand(value: number | null = null): OperandDraft {
  return { id: newId(), kind: 'const', value };
}

export function priceOperand(price: PriceField = 'Close'): OperandDraft {
  return { id: newId(), kind: 'price', price, offset: 0 };
}

export function indicatorOperand(ref: string, output: string | null = null): OperandDraft {
  return { id: newId(), kind: 'indicator', ref, output, offset: 0 };
}

/**
 * A fresh comparison, referencing the first declared indicator when there is
 * one so the node starts meaningful rather than empty.
 */
export function newCondition(firstRef?: string): ConditionDraft {
  return {
    id: newId(),
    family: 'compare',
    op: 'GreaterThan',
    left: firstRef ? indicatorOperand(firstRef) : priceOperand(),
    right: constOperand(),
  };
}

// ---------------------------------------------------------------- traversal

/** Apply `fn` to the node with `id`; returns the tree unchanged when absent. */
export function mapNode(
  node: ConditionDraft,
  id: NodeId,
  fn: (node: ConditionDraft) => ConditionDraft,
): ConditionDraft {
  if (node.id === id) {
    return fn(node);
  }

  switch (node.family) {
    case 'logical':
      return { ...node, children: node.children.map((child) => mapNode(child, id, fn)) };
    case 'not':
      return { ...node, child: mapNode(node.child, id, fn) };
    default:
      return node;
  }
}

export function findNode(node: ConditionDraft | null, id: NodeId): ConditionDraft | null {
  if (!node) {
    return null;
  }

  if (node.id === id) {
    return node;
  }

  if (node.family === 'logical') {
    for (const child of node.children) {
      const found = findNode(child, id);

      if (found) {
        return found;
      }
    }
  }

  if (node.family === 'not') {
    return findNode(node.child, id);
  }

  return null;
}

/**
 * Remove a node, collapsing what would otherwise become invalid.
 *
 * An `And`/`Or` left with one child is replaced by that child — the parser
 * requires at least two operands, and unwrapping is friendlier than a Remove
 * button that dead-ends. A `Not` whose child is removed goes with it. Returns
 * null when the removal empties the tree.
 */
export function removeNodeById(node: ConditionDraft, id: NodeId): ConditionDraft | null {
  if (node.id === id) {
    return null;
  }

  if (node.family === 'logical') {
    const children = node.children
      .map((child) => removeNodeById(child, id))
      .filter((child): child is ConditionDraft => child !== null);

    if (children.length === 0) {
      return null;
    }

    if (children.length === 1) {
      return children[0];
    }

    return { ...node, children };
  }

  if (node.family === 'not') {
    const child = removeNodeById(node.child, id);

    return child === null ? null : { ...node, child };
  }

  return node;
}

/** Depth of the deepest branch, counting the root as 1. */
export function depthOf(node: ConditionDraft | null): number {
  if (!node) {
    return 0;
  }

  switch (node.family) {
    case 'logical':
      return 1 + Math.max(0, ...node.children.map(depthOf));
    case 'not':
      return 1 + depthOf(node.child);
    default:
      return 1;
  }
}

/** Depth at which `id` sits, or 0 when it is not in this tree. */
export function depthOfNode(node: ConditionDraft | null, id: NodeId, depth = 1): number {
  if (!node) {
    return 0;
  }

  if (node.id === id) {
    return depth;
  }

  if (node.family === 'logical') {
    for (const child of node.children) {
      const found = depthOfNode(child, id, depth + 1);

      if (found > 0) {
        return found;
      }
    }
  }

  if (node.family === 'not') {
    return depthOfNode(node.child, id, depth + 1);
  }

  return 0;
}

// ---------------------------------------------------------------- editing

/** Wrap a node in a logical operator, keeping it as the first child. */
export function wrapNode(
  node: ConditionDraft,
  id: NodeId,
  op: LogicalOp | 'Not',
  firstRef?: string,
): ConditionDraft {
  return mapNode(node, id, (target) =>
    op === 'Not'
      ? { id: newId(), family: 'not', op: 'Not', child: target }
      : {
          id: newId(),
          family: 'logical',
          op,
          children: [target, newCondition(firstRef)],
        },
  );
}

/**
 * Change a node's operator, preserving every operand the new family can hold.
 *
 * This is where "my rule lost its left operand" bugs come from, so the
 * preservation rules are explicit rather than emergent:
 *
 * - compare ⇄ between — `left` survives both ways; `low`/`high` are invented or
 *   dropped.
 * - compare/between → trend — `left` becomes the tracked value.
 * - trend → compare/between — the tracked value becomes `left`.
 * - anything → logical — the node becomes the first child of the new operator.
 * - logical → anything — the first child is kept if it fits, otherwise a fresh
 *   condition is made, because a comparison cannot hold two conditions.
 */
export function changeOperator(
  node: ConditionDraft,
  op: RuleOperator,
  firstRef?: string,
): ConditionDraft {
  const family = FAMILY_OF[op];

  if (family === node.family) {
    // Same shape — only the label changes.
    return { ...node, op } as ConditionDraft;
  }

  if (family === 'logical') {
    return {
      id: node.id,
      family: 'logical',
      op: op as LogicalOp,
      children: [{ ...node, id: newId() }, newCondition(firstRef)],
    };
  }

  if (family === 'not') {
    return { id: node.id, family: 'not', op: 'Not', child: { ...node, id: newId() } };
  }

  // Leaving a logical or not node for a leaf family: there is no operand to
  // carry over, so start from a fresh one.
  const carried =
    primaryOperandOf(node) ?? (firstRef ? indicatorOperand(firstRef) : priceOperand());

  if (family === 'compare') {
    return {
      id: node.id,
      family: 'compare',
      op: op as ComparisonOp,
      left: carried,
      right: secondaryOperandOf(node) ?? constOperand(),
    };
  }

  if (family === 'between') {
    return {
      id: node.id,
      family: 'between',
      op: 'Between',
      left: carried,
      low: constOperand(),
      high: constOperand(),
    };
  }

  return {
    id: node.id,
    family: 'trend',
    op: op as TrendOp,
    value: carried,
    bars: 3,
  };
}

/** The operand a node leads with, if its family has one. */
function primaryOperandOf(node: ConditionDraft): OperandDraft | null {
  switch (node.family) {
    case 'compare':
    case 'between':
      return node.left;
    case 'trend':
      return node.value;
    default:
      return null;
  }
}

function secondaryOperandOf(node: ConditionDraft): OperandDraft | null {
  return node.family === 'compare' ? node.right : null;
}

/** Replace an operand anywhere in the tree, addressed by its own id. */
export function replaceOperand(
  node: ConditionDraft,
  operandId: NodeId,
  next: OperandDraft,
): ConditionDraft {
  switch (node.family) {
    case 'logical':
      return {
        ...node,
        children: node.children.map((child) => replaceOperand(child, operandId, next)),
      };
    case 'not':
      return { ...node, child: replaceOperand(node.child, operandId, next) };
    case 'compare':
      return {
        ...node,
        left: node.left.id === operandId ? next : node.left,
        right: node.right.id === operandId ? next : node.right,
      };
    case 'between':
      return {
        ...node,
        left: node.left.id === operandId ? next : node.left,
        low: node.low.id === operandId ? next : node.low,
        high: node.high.id === operandId ? next : node.high,
      };
    case 'trend':
      return { ...node, value: node.value.id === operandId ? next : node.value };
  }
}

/** Append a child to a logical node. Refused past the operand ceiling. */
export function addChild(
  node: ConditionDraft,
  parentId: NodeId,
  firstRef?: string,
): ConditionDraft {
  return mapNode(node, parentId, (target) =>
    target.family === 'logical' && target.children.length < LIMITS.maxOperands
      ? { ...target, children: [...target.children, newCondition(firstRef)] }
      : target,
  );
}
