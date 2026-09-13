import { expect, test, type Page } from '@playwright/test';

/**
 * The backtest loop: author a rule strategy, queue a run over stored candles,
 * read the result.
 *
 * Runs against a real API and database — nothing is mocked. It needs:
 *
 *   - the API running in Development on http://localhost:5000 with CORS
 *     allowing http://localhost:5173
 *   - postgres and redis up (`docker compose up -d`)
 *   - the worker running, or a queued run never starts
 *   - the seeded owner's credentials in E2E_EMAIL and E2E_PASSWORD
 *   - stored candles for E2E_SYMBOL (default BTCUSDT) — import a CSV if there
 *     is no egress proxy configured
 *
 * Anything missing skips rather than fails: a red test should mean the app is
 * broken, not that the machine is not set up.
 */

const EMAIL = process.env.E2E_EMAIL;
const PASSWORD = process.env.E2E_PASSWORD;
const SYMBOL = process.env.E2E_SYMBOL ?? 'BTCUSDT';

const STRATEGY_NAME = `e2e close over EMA ${Date.now()}`;

test.describe('the backtest loop', () => {
  test.skip(
    !EMAIL || !PASSWORD,
    'Set E2E_EMAIL and E2E_PASSWORD to the seeded owner account (Seed:OwnerEmail / Seed:OwnerPassword).',
  );

  test.beforeEach(async ({ page }) => {
    await signIn(page, EMAIL!, PASSWORD!);
  });

  test('authors a strategy in the builder and saves it', async ({ page }) => {
    await page.goto('/backtests/strategies/new');

    // The editor opens on a seeded starter, which must already be valid —
    // an empty document is not legal and would open as a wall of red.
    await expect(page.getByText('Valid', { exact: false })).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText(/bars of history before the first signal/)).toBeVisible();

    await page.getByLabel('Name', { exact: true }).first().fill('fast');
    await page.getByLabel('Period').first().fill('20');

    await page.getByLabel('Description').fill('Authored by the e2e spec.');

    // The strategy's own name field is the one inside the header card.
    await page.getByRole('textbox', { name: 'Name', exact: true }).first().fill(STRATEGY_NAME);

    await page.getByRole('button', { name: /Create strategy/ }).click();

    await expect(page).toHaveURL(/\/backtests\?tab=strategies/);
    await expect(page.getByText(STRATEGY_NAME)).toBeVisible({ timeout: 10_000 });
  });

  test('rejects a broken rule with the path that names the problem', async ({ page }) => {
    await page.goto('/backtests/strategies/new');

    await expect(page.getByText('Valid', { exact: false })).toBeVisible({ timeout: 15_000 });

    await page.getByRole('tab', { name: /JSON/ }).click();

    const editor = page.getByRole('textbox').last();
    await editor.fill(
      JSON.stringify(
        {
          version: 1,
          indicators: [{ id: 'fast', type: 'Ema', params: { period: 20 } }],
          // `slow` is never declared — the parser reports the exact path.
          entry: { long: { op: 'CrossesAbove', left: { ref: 'fast' }, right: { ref: 'slow' } } },
          stopLoss: { kind: 'Percent', percent: 2 },
        },
        null,
        2,
      ),
    );

    await expect(page.getByText(/No indicator with id 'slow' is declared/)).toBeVisible({
      timeout: 15_000,
    });
  });

  test('queues a run and reads its result', async ({ page }) => {
    // Candle data is a hard prerequisite and cannot be created from the UI
    // without either an egress proxy or a CSV to hand.
    await page.goto('/backtests?tab=market-data');

    const coverageRow = page.getByRole('row').filter({ hasText: SYMBOL });
    const hasCandles = await coverageRow
      .first()
      .isVisible()
      .catch(() => false);

    test.skip(
      !hasCandles,
      `No stored candles for ${SYMBOL}. Import a CSV from Backtests → Market data, or backfill with a proxy configured, then re-run.`,
    );

    // An account for the run to compound onto.
    await page.goto('/backtests?tab=accounts');

    const accountName = `e2e ${Date.now()}`;

    await page.getByRole('button', { name: 'New account' }).click();
    await page.getByRole('textbox', { name: 'Name', exact: true }).fill(accountName);
    await page.getByRole('button', { name: 'Create account' }).click();
    await expect(page.getByText(accountName)).toBeVisible({ timeout: 10_000 });

    await page.goto('/backtests/runs/new');

    await selectOption(page, 'Account', accountName);
    await selectFirstOption(page, 'Strategy');
    await page.getByLabel('Symbol').fill(SYMBOL);

    // A range the coverage row says exists. The pre-flight will say whether it
    // is complete.
    await page.getByLabel('From').fill('2025-01-01');
    await page.getByLabel('To').fill('2025-03-01');

    // Either the range is clean, or the gap panel appears and `allowGaps`
    // becomes available — both are acceptable outcomes for the spec.
    const gapWarning = page.getByText(/missing across/);

    if (await gapWarning.isVisible({ timeout: 10_000 }).catch(() => false)) {
      await page.getByLabel(/Run anyway over gapped data/).check();
    }

    await page.getByRole('button', { name: 'Queue run' }).click();

    await expect(page).toHaveURL(/\/backtests\/runs\/[0-9a-f-]{36}$/, { timeout: 15_000 });

    // Queued → Running → terminal. The worker has to be up for this to move.
    await expect(page.getByText(/Succeeded|Failed|Cancelled/).first()).toBeVisible({
      timeout: 120_000,
    });

    // Whatever the outcome, the run has to explain itself rather than sit blank.
    const succeeded = await page
      .getByText('Succeeded')
      .first()
      .isVisible()
      .catch(() => false);

    if (succeeded) {
      await expect(page.getByText('How the exits were decided')).toBeVisible();
      await expect(page.getByText('Engine counters')).toBeVisible();
    } else {
      await expect(page.getByText(/This run failed|Cancel/).first()).toBeVisible();
    }
  });
});

async function signIn(page: Page, email: string, password: string) {
  await page.goto('/login');

  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page.getByText('Could not reach the API')).toHaveCount(0);
  await expect(page.getByRole('link', { name: /backtests/i })).toBeVisible({ timeout: 15_000 });
}

/** Mantine's Select is a combobox, not a native <select>. */
async function selectOption(page: Page, label: string, optionText: string) {
  await page.getByLabel(label, { exact: true }).click();
  await page.getByRole('option').filter({ hasText: optionText }).first().click();
}

async function selectFirstOption(page: Page, label: string) {
  await page.getByLabel(label, { exact: true }).click();
  await page.getByRole('option').first().click();
}
