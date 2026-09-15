import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RuleBuilder } from '@/features/backtests/rules/RuleBuilder';
import { useRuleEditor } from '@/features/backtests/rules/useRuleEditor';
import { renderWithProviders } from './render';
import { server } from './msw/server';

const BASE = 'http://localhost:5000';

function validates() {
  return http.post(`${BASE}/api/backtests/strategies/validate`, () =>
    HttpResponse.json({
      isValid: true,
      warmupBars: 60,
      indicators: ['fast (Ema 20)'],
      hasLongEntry: true,
      hasShortEntry: false,
      path: null,
      reason: null,
    }),
  );
}

function Harness() {
  const editor = useRuleEditor();

  return <RuleBuilder editor={editor} />;
}

/** The JSON tab's textarea carries no label, so it is addressed by its empty name. */
async function jsonText() {
  await userEvent.click(screen.getByRole('tab', { name: /JSON/ }));

  return ((await screen.findByRole('textbox', { name: '' })) as HTMLTextAreaElement).value;
}

/**
 * Every Select on the page renders its options into the document, so several share a
 * label like "4h". The open dropdown is the one the combobox points at, so pick from
 * there by wire value rather than by accessible name.
 */
async function choose(field: string, value: string) {
  const combobox = await screen.findByRole('combobox', { name: field });

  await userEvent.click(combobox);

  const dropdown = combobox.getAttribute('aria-controls');
  const option = document.querySelector(`#${dropdown} [value="${value}"]`);

  if (!option) {
    throw new Error(`The ${field} dropdown has no option with the value ${value}.`);
  }

  await userEvent.click(option);
}

describe('the per-indicator timeframe', () => {
  it('defaults to the run interval and keeps the document at version 1', async () => {
    server.use(validates());
    renderWithProviders(<Harness />);

    expect(await screen.findByRole('combobox', { name: 'Timeframe' })).toHaveValue('Run interval');

    // The hash-stability guard, seen from the UI: an untouched document must serialise
    // exactly as it did before multi-timeframe existed.
    const json = await jsonText();

    expect(json).toContain('"version": 1');
    expect(json).not.toContain('interval');
  });

  it('raises the document to version 2 once a timeframe is chosen', async () => {
    server.use(validates());
    renderWithProviders(<Harness />);

    await screen.findByRole('combobox', { name: 'Timeframe' });
    await choose('Timeframe', 'FourHours');

    await waitFor(async () => {
      const json = await jsonText();

      expect(json).toContain('"interval": "FourHours"');
      expect(json).toContain('"version": 2');
    });
  });

  it('shows warmup in the indicator own bars once it reads a higher timeframe', async () => {
    server.use(validates());
    renderWithProviders(<Harness />);

    // A 20-period Ema has a warmup multiplier of 3.
    expect(await screen.findByText('60 bars')).toBeInTheDocument();

    await choose('Timeframe', 'FourHours');

    expect(await screen.findByText(/60 × 4h/)).toBeInTheDocument();
  });

  it('marks a timeframe the previewed run interval cannot build', async () => {
    server.use(validates());
    renderWithProviders(<Harness />);

    // The preview defaults to 4h, and six-hour bars are not a whole number of four-hour
    // ones — which is the one pairing the queue refuses outright.
    await screen.findByRole('combobox', { name: 'Preview at' });
    await choose('Timeframe', 'SixHours');

    expect(await screen.findByText(/6h unusable/)).toBeInTheDocument();
  });
});
