import { beforeEach, describe, expect, it } from 'vitest';
import { parseRuleDocument } from '@/lib/rules/parse';
import { serialiseRule } from '@/lib/rules/serialise';
import { starterDraft } from '@/lib/rules/starter';
import { newId, resetIds } from '@/lib/rules/tree';
import { validateDraft } from '@/lib/rules/validate';
import { intervalRatio } from '@/lib/marketData';
import type { IndicatorDraft, RuleDraft } from '@/lib/rules/types';

beforeEach(() => resetIds());

type WireIndicator = Record<string, unknown>;

function wire(draft: RuleDraft) {
  return serialiseRule(draft).document as {
    version: number;
    indicators: WireIndicator[];
  };
}

function draftWith(indicators: IndicatorDraft[]): RuleDraft {
  return {
    ...starterDraft(),
    indicators,
  };
}

function indicator(patch: Partial<IndicatorDraft> = {}): IndicatorDraft {
  return {
    id: newId(),
    ref: 'fast',
    type: 'Ema',
    source: 'Close',
    period: 20,
    interval: null,
    ...patch,
  };
}

describe('the document version', () => {
  it('stays at 1 while every indicator reads the run interval', () => {
    // The hash-stability guard. Emitting 2 unconditionally would change the identity of
    // every existing strategy the first time someone opened and re-saved one.
    expect(wire(draftWith([indicator()])).version).toBe(1);
  });

  it('rises to 2 as soon as one indicator names a timeframe', () => {
    expect(wire(draftWith([indicator({ interval: 'FourHours' })])).version).toBe(2);
  });
});

describe('serialising an interval', () => {
  it('omits it when the indicator reads the run interval', () => {
    const [first] = wire(draftWith([indicator()])).indicators;

    expect(first).not.toHaveProperty('interval');
    expect(first).toEqual({ id: 'fast', type: 'Ema', params: { period: 20 } });
  });

  it('emits it when one is set', () => {
    const [first] = wire(draftWith([indicator({ ref: 'adx', type: 'Adx', interval: 'FourHours' })]))
      .indicators;

    expect(first).toEqual({
      id: 'adx',
      type: 'Adx',
      interval: 'FourHours',
      params: { period: 20 },
    });
  });
});

describe('the per-type default source', () => {
  it('omits High on a Highest and Low on a Lowest', () => {
    const { indicators } = wire(
      draftWith([
        indicator({ ref: 'swingHigh', type: 'Highest', source: 'High' }),
        indicator({ ref: 'swingLow', type: 'Lowest', source: 'Low' }),
      ]),
    );

    expect(indicators[0]).not.toHaveProperty('source');
    expect(indicators[1]).not.toHaveProperty('source');
  });

  it('emits Close on a Highest, because there it is not the default', () => {
    const [first] = wire(
      draftWith([indicator({ ref: 'swingHigh', type: 'Highest', source: 'Close' })]),
    ).indicators;

    expect(first).toHaveProperty('source', 'Close');
  });
});

describe('round-tripping', () => {
  it('survives a document carrying an interval', () => {
    const first = serialiseRule(
      draftWith([indicator({ ref: 'adx', type: 'Adx', interval: 'FourHours' })]),
    );

    const second = serialiseRule(parseRuleDocument(first.document));

    expect(second.text).toBe(first.text);
  });

  it('reads an unknown interval back as the run interval rather than inventing one', () => {
    const draft = parseRuleDocument({
      version: 2,
      indicators: [{ id: 'x', type: 'Ema', interval: 'ThirteenHours', params: { period: 20 } }],
      entry: { long: { op: 'GreaterThan', left: { price: 'Close' }, right: 0 } },
      stopLoss: { kind: 'Percent', percent: 2 },
    });

    expect(draft.indicators[0].interval).toBeNull();
  });
});

describe('validation', () => {
  it('refuses the drill-down interval', () => {
    const draft = draftWith([indicator({ interval: 'OneMinute' })]);

    const messages = validateDraft(draft).map((d) => d.message);

    expect(messages.some((m) => m.includes('One-minute'))).toBe(true);
  });

  it('accepts a tradeable interval without knowing what the run will be', () => {
    const draft = draftWith([indicator({ interval: 'OneWeek' })]);

    expect(validateDraft(draft).filter((d) => d.field === 'interval')).toHaveLength(0);
  });
});

describe('intervalRatio', () => {
  it('counts base bars per higher bar', () => {
    expect(intervalRatio('FourHours', 'FifteenMinutes')).toBe(16);
    expect(intervalRatio('OneWeek', 'FifteenMinutes')).toBe(672);
    expect(intervalRatio('OneHour', 'OneHour')).toBe(1);
  });

  it('refuses a pair whose grids do not nest', () => {
    expect(intervalRatio('SixHours', 'FourHours')).toBeNull();
    expect(intervalRatio('FifteenMinutes', 'FourHours')).toBeNull();
  });
});
