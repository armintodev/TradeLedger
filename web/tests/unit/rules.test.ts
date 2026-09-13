import { beforeEach, describe, expect, it } from 'vitest';
import { parseRuleDocument } from '@/lib/rules/parse';
import { resolveServerPath, serialiseRule } from '@/lib/rules/serialise';
import { starterDraft } from '@/lib/rules/starter';
import {
  addChild,
  changeOperator,
  constOperand,
  depthOf,
  findNode,
  indicatorOperand,
  newCondition,
  newId,
  priceOperand,
  removeNodeById,
  replaceOperand,
  resetIds,
  wrapNode,
} from '@/lib/rules/tree';
import { byNode, documentDiagnostics, validateDraft } from '@/lib/rules/validate';
import {
  ALL_OPERATORS,
  FAMILY_OF,
  type ConditionDraft,
  type IndicatorDraft,
  type RuleDraft,
  type RuleOperator,
} from '@/lib/rules/types';

beforeEach(() => resetIds());

function draftWith(
  long: ConditionDraft | null,
  indicators: IndicatorDraft[] = defaultIndicators(),
): RuleDraft {
  return {
    version: 1,
    indicators,
    entry: { long, short: null },
    stopLoss: { kind: 'Percent', percent: 2 },
  };
}

function defaultIndicators(): IndicatorDraft[] {
  return [
    { id: newId(), ref: 'fast', type: 'Ema' as const, source: 'Close' as const, period: 20 },
    { id: newId(), ref: 'slow', type: 'Ema' as const, source: 'Close' as const, period: 55 },
  ];
}

describe('serialiseRule — the wire shape', () => {
  it('emits the compact forms, so a round trip does not change the rule hash', () => {
    // CanonicalHash is structural: `5` and `{"const":5}` hash differently, as do
    // an absent `offset` and `"offset": 0`. Expanding either would silently
    // change the identity by which two runs are judged to share a rule.
    const draft = draftWith({
      id: newId(),
      family: 'compare',
      op: 'GreaterThan',
      left: indicatorOperand('fast'),
      right: constOperand(5),
    });

    const { document } = serialiseRule(draft);
    const long = (document as { entry: { long: Record<string, unknown> } }).entry.long;

    expect(long.right).toBe(5);
    expect(long.left).toEqual({ ref: 'fast' });
    expect(long.left).not.toHaveProperty('offset');
    expect(long.left).not.toHaveProperty('output');
  });

  it('omits a Close source, which is the server default', () => {
    const { document } = serialiseRule(draftWith(null));
    const indicators = (document as { indicators: Record<string, unknown>[] }).indicators;

    expect(indicators[0]).not.toHaveProperty('source');
    expect(indicators[0]).toEqual({ id: 'fast', type: 'Ema', params: { period: 20 } });
  });

  it('emits a non-default source', () => {
    const draft = draftWith(null, [
      { id: newId(), ref: 'x', type: 'Ema', source: 'High', period: 10 },
    ]);

    const indicators = (serialiseRule(draft).document as { indicators: Record<string, unknown>[] })
      .indicators;

    expect(indicators[0].source).toBe('High');
  });

  it('never emits a source for an indicator that takes none', () => {
    const draft = draftWith(null, [
      // Dmi derives from the whole bar; a source here fails the whole document.
      { id: newId(), ref: 'dmi', type: 'Dmi', source: 'Close', period: 14 },
    ]);

    const indicators = (serialiseRule(draft).document as { indicators: Record<string, unknown>[] })
      .indicators;

    expect(indicators[0]).not.toHaveProperty('source');
  });

  it('emits null rather than substituting zero for an empty constant', () => {
    const draft = draftWith({
      id: newId(),
      family: 'compare',
      op: 'GreaterThan',
      left: indicatorOperand('fast'),
      right: constOperand(null),
    });

    const long = (serialiseRule(draft).document as { entry: { long: Record<string, unknown> } })
      .entry.long;

    // A zero here would be a silently different, *valid* rule. Null is invalid,
    // which is the truth, and the parser's message for it is precise.
    expect(long.right).toEqual({ const: null });
  });

  it('omits an entry side that is absent rather than sending null', () => {
    const entry = (serialiseRule(draftWith(null)).document as { entry: Record<string, unknown> })
      .entry;

    expect(entry).toEqual({});
  });

  it('uses the right child key for each operator family', () => {
    const shapes: Record<string, ConditionDraft> = {
      logical: {
        id: newId(),
        family: 'logical',
        op: 'And',
        children: [newCondition('fast'), newCondition('slow')],
      },
      not: { id: newId(), family: 'not', op: 'Not', child: newCondition('fast') },
      between: {
        id: newId(),
        family: 'between',
        op: 'Between',
        left: indicatorOperand('fast'),
        low: constOperand(30),
        high: constOperand(70),
      },
      trend: {
        id: newId(),
        family: 'trend',
        op: 'RisingFor',
        value: indicatorOperand('fast'),
        bars: 3,
      },
    };

    const long = (node: ConditionDraft) =>
      (serialiseRule(draftWith(node)).document as { entry: { long: Record<string, unknown> } })
        .entry.long;

    expect(long(shapes.logical)).toHaveProperty('operands');
    expect(long(shapes.not)).toHaveProperty('operand');
    expect(long(shapes.between)).toMatchObject({ op: 'Between', low: 30, high: 70 });
    expect(long(shapes.trend)).toMatchObject({ op: 'RisingFor', bars: 3 });
  });

  it('distinguishes Not.operand (a condition) from RisingFor.operand (a value)', () => {
    const notNode: ConditionDraft = {
      id: newId(),
      family: 'not',
      op: 'Not',
      child: newCondition('fast'),
    };

    const trendNode: ConditionDraft = {
      id: newId(),
      family: 'trend',
      op: 'FallingFor',
      value: indicatorOperand('fast'),
      bars: 4,
    };

    const notOperand = (
      serialiseRule(draftWith(notNode)).document as {
        entry: { long: { operand: Record<string, unknown> } };
      }
    ).entry.long.operand;

    const trendOperand = (
      serialiseRule(draftWith(trendNode)).document as {
        entry: { long: { operand: Record<string, unknown> } };
      }
    ).entry.long.operand;

    // Same key on the wire, different meanings — which is exactly why the draft
    // keeps them in differently-named fields.
    expect(notOperand).toHaveProperty('op');
    expect(trendOperand).toEqual({ ref: 'fast' });
  });

  it('omits a zero bufferPercent on an indicator-level stop', () => {
    const draft: RuleDraft = {
      ...draftWith(null),
      stopLoss: { kind: 'IndicatorLevel', ref: 'slow', output: null, offset: 0, bufferPercent: 0 },
    };

    const stop = (serialiseRule(draft).document as { stopLoss: Record<string, unknown> }).stopLoss;

    expect(stop).toEqual({ kind: 'IndicatorLevel', ref: 'slow' });
  });
});

describe('serialise ⇄ parse round-trip', () => {
  const cases: [string, ConditionDraft][] = [
    ['comparison', newCondition('fast')],
    [
      'logical',
      {
        id: 'a',
        family: 'logical',
        op: 'Or',
        children: [newCondition('fast'), newCondition('slow')],
      },
    ],
    ['not', { id: 'b', family: 'not', op: 'Not', child: newCondition('fast') }],
    [
      'between',
      {
        id: 'c',
        family: 'between',
        op: 'Between',
        left: indicatorOperand('fast'),
        low: constOperand(30),
        high: constOperand(70),
      },
    ],
    ['trend', { id: 'd', family: 'trend', op: 'RisingFor', value: priceOperand('High'), bars: 7 }],
  ];

  it.each(cases)('survives a round trip: %s', (_name, node) => {
    const original = draftWith(node);
    const first = serialiseRule(original);
    const reparsed = parseRuleDocument(first.document);
    const second = serialiseRule(reparsed);

    // Ids are regenerated on parse, so compare the wire form — which is the
    // thing that has to be stable.
    expect(second.text).toBe(first.text);
  });

  it('round-trips the starter document', () => {
    const first = serialiseRule(starterDraft());
    const second = serialiseRule(parseRuleDocument(first.document));

    expect(second.text).toBe(first.text);
  });

  it('reads the bare-number shorthand back as a constant', () => {
    const draft = parseRuleDocument({
      version: 1,
      indicators: [{ id: 'rsi', type: 'Rsi', params: { period: 14 } }],
      entry: { long: { op: 'LessThan', left: { ref: 'rsi' }, right: 30 } },
      stopLoss: { kind: 'Percent', percent: 2 },
    });

    const long = draft.entry.long;

    expect(long?.family).toBe('compare');
    expect(long?.family === 'compare' && long.right).toMatchObject({ kind: 'const', value: 30 });
  });

  it('opens a half-written document rather than throwing', () => {
    const draft = parseRuleDocument({ version: 1, indicators: 'nonsense' });

    expect(draft.indicators).toEqual([]);
    expect(draft.entry.long).toBeNull();
    expect(draft.stopLoss.kind).toBe('Percent');
  });

  it('promotes a logical node that was left with one child', () => {
    const draft = parseRuleDocument({
      version: 1,
      indicators: [{ id: 'fast', type: 'Ema', params: { period: 20 } }],
      entry: {
        long: { op: 'And', operands: [{ op: 'GreaterThan', left: { ref: 'fast' }, right: 1 }] },
      },
      stopLoss: { kind: 'Percent', percent: 2 },
    });

    // An `And` of one is not a legal document, so it collapses to the survivor.
    expect(draft.entry.long?.family).toBe('compare');
  });
});

describe('changeOperator', () => {
  const families = ['logical', 'not', 'compare', 'between', 'trend'] as const;

  function sample(family: (typeof families)[number]): ConditionDraft {
    switch (family) {
      case 'logical':
        return {
          id: 'root',
          family: 'logical',
          op: 'And',
          children: [newCondition('fast'), newCondition('slow')],
        };
      case 'not':
        return { id: 'root', family: 'not', op: 'Not', child: newCondition('fast') };
      case 'compare':
        return {
          id: 'root',
          family: 'compare',
          op: 'GreaterThan',
          left: indicatorOperand('fast'),
          right: constOperand(42),
        };
      case 'between':
        return {
          id: 'root',
          family: 'between',
          op: 'Between',
          left: indicatorOperand('fast'),
          low: constOperand(1),
          high: constOperand(2),
        };
      case 'trend':
        return {
          id: 'root',
          family: 'trend',
          op: 'RisingFor',
          value: indicatorOperand('fast'),
          bars: 5,
        };
    }
  }

  // Every family to every operator. This is where "my rule lost its left
  // operand" bugs come from, so it is a table rather than spot checks.
  const table = families.flatMap((from) => ALL_OPERATORS.map((to) => [from, to] as const));

  it.each(table)('%s → %s produces a well-formed node', (from, to) => {
    const result = changeOperator(sample(from), to as RuleOperator, 'fast');

    expect(result.family).toBe(FAMILY_OF[to as RuleOperator]);
    expect(result.op).toBe(to);
    // The node keeps its identity, so the card it was rendered in does not
    // unmount and lose focus.
    expect(result.id).toBe('root');
  });

  it('carries the left operand from a comparison into a between', () => {
    const result = changeOperator(sample('compare'), 'Between');

    expect(result.family === 'between' && result.left).toMatchObject({
      kind: 'indicator',
      ref: 'fast',
    });
  });

  it('carries the left operand from a comparison into a trend', () => {
    const result = changeOperator(sample('compare'), 'RisingFor');

    expect(result.family === 'trend' && result.value).toMatchObject({ ref: 'fast' });
  });

  it('carries the between left operand back into a comparison', () => {
    const result = changeOperator(sample('between'), 'LessThan');

    expect(result.family === 'compare' && result.left).toMatchObject({ ref: 'fast' });
  });

  it('preserves both operands when only the comparison changes', () => {
    const result = changeOperator(sample('compare'), 'CrossesBelow');

    expect(result.family === 'compare' && result.left).toMatchObject({ ref: 'fast' });
    expect(result.family === 'compare' && result.right).toMatchObject({ value: 42 });
  });

  it('keeps the existing node as the first child when wrapping in a logical', () => {
    const result = changeOperator(sample('compare'), 'Or');

    expect(result.family).toBe('logical');
    expect(result.family === 'logical' && result.children).toHaveLength(2);
    expect(result.family === 'logical' && result.children[0].op).toBe('GreaterThan');
  });
});

describe('tree surgery', () => {
  it('unwraps a logical node left with one child instead of dead-ending', () => {
    const keep = newCondition('fast');
    const drop = newCondition('slow');

    const root: ConditionDraft = {
      id: 'root',
      family: 'logical',
      op: 'And',
      children: [keep, drop],
    };

    // The parser requires at least two operands, so the alternative would be a
    // permanently disabled Remove button.
    expect(removeNodeById(root, drop.id)?.id).toBe(keep.id);
  });

  it('removes a Not along with its child', () => {
    const child = newCondition('fast');
    const root: ConditionDraft = { id: 'root', family: 'not', op: 'Not', child };

    expect(removeNodeById(root, child.id)).toBeNull();
  });

  it('returns null when the root itself is removed', () => {
    const root = newCondition('fast');

    expect(removeNodeById(root, root.id)).toBeNull();
  });

  it('wraps a node and keeps it as the first child', () => {
    const root = newCondition('fast');
    const wrapped = wrapNode(root, root.id, 'And', 'fast');

    expect(wrapped.family).toBe('logical');
    expect(wrapped.family === 'logical' && wrapped.children[0].id).toBe(root.id);
  });

  it('replaces an operand anywhere in the tree', () => {
    const leaf = newCondition('fast');
    const root: ConditionDraft = {
      id: 'root',
      family: 'logical',
      op: 'And',
      children: [leaf, newCondition('slow')],
    };

    const replacement = constOperand(99);
    const next =
      leaf.family === 'compare' ? replaceOperand(root, leaf.right.id, replacement) : root;

    const found = findNode(next, leaf.id);

    expect(found?.family === 'compare' && found.right).toMatchObject({ value: 99 });
  });

  it('counts depth from the root', () => {
    const root: ConditionDraft = {
      id: 'root',
      family: 'logical',
      op: 'And',
      children: [
        newCondition('fast'),
        { id: 'n', family: 'not', op: 'Not', child: newCondition('slow') },
      ],
    };

    expect(depthOf(root)).toBe(3);
    expect(depthOf(null)).toBe(0);
  });

  it('appends a child to a logical node', () => {
    const root: ConditionDraft = {
      id: 'root',
      family: 'logical',
      op: 'And',
      children: [newCondition('fast'), newCondition('slow')],
    };

    const next = addChild(root, 'root', 'fast');

    expect(next.family === 'logical' && next.children).toHaveLength(3);
  });
});

describe('validateDraft', () => {
  it('passes the starter document', () => {
    expect(validateDraft(starterDraft())).toEqual([]);
  });

  it('requires at least one indicator', () => {
    const diagnostics = validateDraft(draftWith(null, []));

    expect(documentDiagnostics(diagnostics).map((d) => d.message)).toContain(
      'At least one indicator must be declared.',
    );
  });

  it('requires at least one entry side', () => {
    const diagnostics = validateDraft(draftWith(null));

    expect(documentDiagnostics(diagnostics).some((d) => d.message.includes('long or short'))).toBe(
      true,
    );
  });

  it('catches a duplicate indicator id, case-insensitively', () => {
    const draft = draftWith(newCondition('fast'), [
      { id: 'a', ref: 'fast', type: 'Ema', source: 'Close', period: 20 },
      { id: 'b', ref: 'FAST', type: 'Sma', source: 'Close', period: 50 },
    ]);

    const diagnostics = byNode(validateDraft(draft)).get('b');

    expect(diagnostics?.[0].message).toContain('Duplicate indicator id');
  });

  it('lands an undeclared ref on the operand that used it', () => {
    const operand = indicatorOperand('missing');
    const node: ConditionDraft = {
      id: 'c',
      family: 'compare',
      op: 'GreaterThan',
      left: operand,
      right: constOperand(1),
    };

    const found = byNode(validateDraft(draftWith(node))).get(operand.id);

    expect(found?.[0].message).toContain("No indicator with id 'missing'");
  });

  it('requires an output when the indicator has more than one', () => {
    const operand = indicatorOperand('dmi');
    const node: ConditionDraft = {
      id: 'c',
      family: 'compare',
      op: 'GreaterThan',
      left: operand,
      right: constOperand(25),
    };

    const draft = draftWith(node, [{ id: 'a', ref: 'dmi', type: 'Dmi', source: null, period: 14 }]);

    expect(byNode(validateDraft(draft)).get(operand.id)?.[0].message).toContain(
      'has several outputs',
    );
  });

  it('rejects a source on an indicator that takes none', () => {
    const draft = draftWith(newCondition('adx'), [
      { id: 'a', ref: 'adx', type: 'Adx', source: 'Close', period: 14 },
    ]);

    expect(byNode(validateDraft(draft)).get('a')?.[0].message).toContain('takes no source');
  });

  it.each([
    [0, 'period'],
    [1001, 'period'],
  ])('rejects a period of %s', (period) => {
    const draft = draftWith(newCondition('fast'), [
      { id: 'a', ref: 'fast', type: 'Ema', source: 'Close', period },
    ]);

    expect(
      byNode(validateDraft(draft))
        .get('a')
        ?.some((d) => d.field === 'period'),
    ).toBe(true);
  });

  it('calls a negative offset what it is', () => {
    const operand = { ...indicatorOperand('fast'), offset: -1 };
    const node: ConditionDraft = {
      id: 'c',
      family: 'compare',
      op: 'GreaterThan',
      left: operand,
      right: constOperand(1),
    };

    expect(byNode(validateDraft(draftWith(node))).get(operand.id)?.[0].message).toContain(
      'would read the future',
    );
  });

  it('rejects an empty constant', () => {
    const right = constOperand(null);
    const node: ConditionDraft = {
      id: 'c',
      family: 'compare',
      op: 'GreaterThan',
      left: indicatorOperand('fast'),
      right,
    };

    expect(byNode(validateDraft(draftWith(node))).get(right.id)?.[0].message).toBe(
      'const must be a number.',
    );
  });

  it('rejects a trend bar count outside 1–500', () => {
    const node: ConditionDraft = {
      id: 'c',
      family: 'trend',
      op: 'RisingFor',
      value: indicatorOperand('fast'),
      bars: 501,
    };

    expect(byNode(validateDraft(draftWith(node))).get('c')?.[0].field).toBe('bars');
  });

  it('rejects a logical node with fewer than two children', () => {
    const node: ConditionDraft = {
      id: 'c',
      family: 'logical',
      op: 'And',
      children: [newCondition('fast')],
    };

    expect(byNode(validateDraft(draftWith(node))).get('c')?.[0].message).toContain('at least 2');
  });

  it.each([0.05, 51])('rejects a stop percentage of %s', (percent) => {
    const draft: RuleDraft = {
      ...draftWith(newCondition('fast')),
      stopLoss: { kind: 'Percent', percent },
    };

    expect(
      documentDiagnostics(validateDraft(draft)).some((d) => d.field === 'stopLoss.percent'),
    ).toBe(true);
  });

  it('requires an indicator-level stop to reference a declared indicator', () => {
    const draft: RuleDraft = {
      ...draftWith(newCondition('fast')),
      stopLoss: { kind: 'IndicatorLevel', ref: 'nope', output: null, offset: 0, bufferPercent: 0 },
    };

    expect(
      documentDiagnostics(validateDraft(draft)).some((d) => d.message.includes("'nope'")),
    ).toBe(true);
  });
});

describe('resolveServerPath', () => {
  it('maps every level of a real path back to a node', () => {
    const operand = indicatorOperand('fast');
    const node: ConditionDraft = {
      id: 'cmp',
      family: 'compare',
      op: 'GreaterThan',
      left: operand,
      right: constOperand(1),
    };

    const { pathToNode } = serialiseRule(draftWith(node));

    // The node itself.
    expect(resolveServerPath('$.entry.long', pathToNode)?.nodeId).toBe('cmp');

    // The operand, and the field of the operand.
    expect(resolveServerPath('$.entry.long.left', pathToNode)?.nodeId).toBe(operand.id);
    expect(resolveServerPath('$.entry.long.left.offset', pathToNode)).toEqual({
      nodeId: operand.id,
      field: 'offset',
    });
  });

  it('resolves an indicator parameter path onto its row', () => {
    const indicators = defaultIndicators();
    const { pathToNode } = serialiseRule(draftWith(null, indicators));

    expect(resolveServerPath('$.indicators[0].params.period', pathToNode)).toEqual({
      nodeId: indicators[0].id,
      field: 'params.period',
    });
  });

  it('resolves through an operands array', () => {
    const second = newCondition('slow');
    const node: ConditionDraft = {
      id: 'root',
      family: 'logical',
      op: 'And',
      children: [newCondition('fast'), second],
    };

    const { pathToNode } = serialiseRule(draftWith(node));

    expect(resolveServerPath('$.entry.long.operands[1]', pathToNode)?.nodeId).toBe(second.id);
  });

  it('returns null for a path that names nothing, so it can be surfaced raw', () => {
    const { pathToNode } = serialiseRule(starterDraft());

    expect(resolveServerPath('$.stopLoss.percent', pathToNode)).toBeNull();
    expect(resolveServerPath('$', pathToNode)).toBeNull();
    expect(resolveServerPath('$.version', pathToNode)).toBeNull();
  });
});
