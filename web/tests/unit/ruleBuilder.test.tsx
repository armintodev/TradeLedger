import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RuleBuilder } from '@/features/backtests/rules/RuleBuilder';
import { useRuleEditor } from '@/features/backtests/rules/useRuleEditor';
import { serialiseRule } from '@/lib/rules/serialise';
import { starterDraft } from '@/lib/rules/starter';
import { renderWithProviders } from './render';
import { server } from './msw/server';

const BASE = 'http://localhost:5000';

function validates(response: Partial<Record<string, unknown>> = {}) {
  return http.post(`${BASE}/api/backtests/strategies/validate`, () =>
    HttpResponse.json({
      isValid: true,
      warmupBars: 60,
      indicators: ['fast (Ema 20)'],
      hasLongEntry: true,
      hasShortEntry: false,
      path: null,
      reason: null,
      ...response,
    }),
  );
}

/** The builder needs a host to own the editor state. */
function Harness({ serverErrors }: { serverErrors?: Record<string, string[]> }) {
  const editor = useRuleEditor();

  return <RuleBuilder editor={editor} serverErrors={serverErrors} />;
}

describe('RuleBuilder', () => {
  it('opens on a starter document that is already valid', async () => {
    server.use(validates());

    renderWithProviders(<Harness />);

    // An empty draft is not a legal document, so opening blank would greet the
    // user with a wall of red.
    expect(await screen.findByLabelText('Name')).toHaveValue('fast');
    expect(await screen.findByText(/Valid/)).toBeInTheDocument();
    expect(
      await screen.findByText(/needs 60 bars of history before the first signal/),
    ).toBeInTheDocument();
  });

  it('does not ask the server while the document is structurally broken', async () => {
    const user = userEvent.setup();

    let asked = 0;

    server.use(
      http.post(`${BASE}/api/backtests/strategies/validate`, () => {
        asked += 1;
        return HttpResponse.json({
          isValid: true,
          warmupBars: 60,
          indicators: [],
          hasLongEntry: true,
          hasShortEntry: false,
          path: null,
          reason: null,
        });
      }),
    );

    renderWithProviders(<Harness />);

    await screen.findByLabelText('Name');

    // Emptying the indicator's name makes both the declaration and the
    // reference invalid.
    await user.clear(screen.getByLabelText('Name'));

    expect(await screen.findByText(/problem/)).toBeInTheDocument();

    const beforeWait = asked;
    await new Promise((resolve) => setTimeout(resolve, 600));

    // A round trip to be told what is already marked in the tree teaches nothing.
    expect(asked).toBe(beforeWait);
  });

  it('surfaces the server verdict when a rule is rejected', async () => {
    server.use(
      validates({
        isValid: false,
        warmupBars: null,
        indicators: null,
        hasLongEntry: null,
        hasShortEntry: null,
        path: '$.entry.long.right.ref',
        reason: "No indicator with id 'slow' is declared.",
      }),
    );

    renderWithProviders(<Harness />);

    expect(await screen.findByText("No indicator with id 'slow' is declared.")).toBeInTheDocument();
    expect(await screen.findByText('$.entry.long.right.ref')).toBeInTheDocument();
  });

  it('projects the draft into the JSON tab', async () => {
    const user = userEvent.setup();
    server.use(validates());

    renderWithProviders(<Harness />);

    await screen.findByLabelText('Name');
    await user.click(screen.getByRole('tab', { name: /JSON/ }));

    const textarea = await screen.findByRole('textbox', { name: '' });
    const text = (textarea as HTMLTextAreaElement).value;

    expect(text).toBe(serialiseRule(starterDraft()).text);
    expect(text).toContain('"CrossesAbove"');
    // The compact forms that keep the rule hash stable.
    expect(text).not.toContain('"offset": 0');
    expect(text).not.toContain('"source"');
  });

  it('takes the JSON tab as authoritative once it is edited', async () => {
    const user = userEvent.setup();
    server.use(validates());

    renderWithProviders(<Harness />);

    await screen.findByLabelText('Name');
    await user.click(screen.getByRole('tab', { name: /JSON/ }));

    const textarea = (await screen.findByRole('textbox', { name: '' })) as HTMLTextAreaElement;

    // `user.type` reads a brace as a key descriptor, so JSON has to be pasted.
    await user.clear(textarea);
    await user.click(textarea);
    await user.paste(
      JSON.stringify({
        version: 1,
        indicators: [{ id: 'rsi', type: 'Rsi', params: { period: 14 } }],
        entry: { long: { op: 'LessThan', left: { ref: 'rsi' }, right: 30 } },
        stopLoss: { kind: 'Percent', percent: 2 },
      }),
    );

    await user.click(screen.getByRole('tab', { name: /Builder/ }));

    // The tree followed the text.
    await waitFor(() => expect(screen.getByLabelText('Name')).toHaveValue('rsi'));

    // And the warning that saving from here will rewrite — and re-hash — it.
    expect(await screen.findByText(/will be saved exactly as written there/)).toBeInTheDocument();
  });

  it('keeps the tree when the JSON stops parsing, rather than destroying it', async () => {
    const user = userEvent.setup();
    server.use(validates());

    renderWithProviders(<Harness />);

    await screen.findByLabelText('Name');
    await user.click(screen.getByRole('tab', { name: /JSON/ }));

    const textarea = (await screen.findByRole('textbox', { name: '' })) as HTMLTextAreaElement;

    await user.clear(textarea);
    await user.click(textarea);
    await user.paste('{ "version": 1, ');

    await waitFor(() =>
      expect(
        screen.getByText(/the builder still holds the last version that parsed/),
      ).toBeInTheDocument(),
    );

    await user.click(screen.getByRole('tab', { name: /Builder/ }));

    expect(await screen.findByLabelText('Name')).toHaveValue('fast');
  });

  it('lands a save-time JSON path error on the node it names', async () => {
    server.use(validates());

    renderWithProviders(
      // `applyServerErrors` could never match this key — it is a path into the
      // rule tree, not a field name.
      <Harness
        serverErrors={{ '$.indicators[0].params.period': ['Period must be between 1 and 1000.'] }}
      />,
    );

    const period = await screen.findByLabelText('Period');
    const row = period.closest('.mantine-Group-root') ?? document.body;

    expect(
      within(row as HTMLElement).getByText(/Period must be between 1 and 1000/),
    ).toBeInTheDocument();
  });

  it('reports a path it cannot resolve rather than swallowing it', async () => {
    server.use(validates());

    renderWithProviders(
      <Harness serverErrors={{ '$.version': ['Version 2 is not supported.'] }} />,
    );

    expect(
      await screen.findByText(/\$\.version: Version 2 is not supported\./),
    ).toBeInTheDocument();
  });

  it('toggles the short entry on and off', async () => {
    const user = userEvent.setup();
    server.use(validates());

    renderWithProviders(<Harness />);

    expect(
      await screen.findByText('This strategy will not open short positions.'),
    ).toBeInTheDocument();

    await user.click(screen.getByLabelText('Enable the short entry'));

    await waitFor(() =>
      expect(
        screen.queryByText('This strategy will not open short positions.'),
      ).not.toBeInTheDocument(),
    );
  });
});
