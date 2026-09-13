import { expect, test, type Page } from '@playwright/test';

/**
 * The one flow whose breakage makes the product useless, and the only test that
 * proves CORS, auth, the contract and mutation invalidation all work together.
 *
 * It runs against a real API and database — nothing is mocked. It needs:
 *
 *   - the API running in Development on http://localhost:5000 with the CORS
 *     policy allowing http://localhost:5173
 *   - postgres and redis up (`docker compose up -d`)
 *   - the seeded owner's credentials in E2E_EMAIL and E2E_PASSWORD
 *   - at least one unreviewed closed trade in the journal
 *
 * Anything missing skips rather than fails: a red test should mean the app is
 * broken, not that the machine is not set up.
 */

const EMAIL = process.env.E2E_EMAIL;
const PASSWORD = process.env.E2E_PASSWORD;

test.describe('the review loop', () => {
  test.skip(
    !EMAIL || !PASSWORD,
    'Set E2E_EMAIL and E2E_PASSWORD to the seeded owner account (Seed:OwnerEmail / Seed:OwnerPassword).',
  );

  test.beforeEach(async ({ page }) => {
    await signIn(page, EMAIL!, PASSWORD!);
  });

  test('reviews a trade and it shows up reviewed in the journal', async ({ page }) => {
    const badge = page.getByRole('link', { name: /review/i }).getByText(/^\d+$/);

    const hasBacklog = await badge.isVisible().catch(() => false);

    test.skip(
      !hasBacklog,
      'The review inbox is empty. Sync an account, or add a closed manual trade, then re-run.',
    );

    const before = Number(await badge.innerText());
    expect(before).toBeGreaterThan(0);

    await page.getByRole('link', { name: /review/i }).click();
    await expect(page).toHaveURL(/\/review$/);

    // "1 of n — SYMBOL"
    const heading = page.getByText(/^\d+ of \d+ — /);
    await expect(heading).toBeVisible();

    const symbol = (await heading.innerText()).split('—')[1]?.trim() ?? '';
    expect(symbol).not.toBe('');

    const memo = `Reviewed by the e2e spec at ${new Date().toISOString()}`;

    await pickFirstOption(page, 'Strategy');
    await pickFirstOption(page, 'Entry mental state');

    // 1–5 set the rating whenever focus is not in a text input.
    await page.locator('body').press('4');

    await page.getByLabel('Memo').fill(memo);

    // Ctrl+Enter is the whole point of the queue: no reaching for the mouse.
    await page.getByLabel('Memo').press('Control+Enter');

    // The queue advances, and the badge drops by one.
    await expect(page.getByText(/^2 of \d+ — /)).toBeVisible({ timeout: 10_000 });
    await expect(badge).toHaveText(String(before - 1), { timeout: 10_000 });

    // The trade is now in the journal as reviewed.
    await page.goto('/trades?reviewState=Reviewed');

    const row = page.getByRole('row').filter({ hasText: symbol }).first();
    await expect(row).toBeVisible({ timeout: 10_000 });

    await row.click();
    await expect(page).toHaveURL(/\/trades\/[0-9a-f-]{36}$/);

    // And the subjective half it was given renders on the detail page.
    await expect(page.getByText(memo)).toBeVisible();
    await expect(page.getByText('Journal')).toBeVisible();
  });

  test('an expired session sends the user back to the login form', async ({ page }) => {
    await page.goto('/trades');
    await expect(page).toHaveURL(/\/trades/);

    await page.evaluate(() => {
      const raw = window.localStorage.getItem('tradeledger.auth');

      if (raw) {
        const auth = JSON.parse(raw) as { expiresAt: string };
        auth.expiresAt = '2020-01-01T00:00:00Z';
        window.localStorage.setItem('tradeledger.auth', JSON.stringify(auth));
      }
    });

    await page.goto('/analytics');

    await expect(page).toHaveURL(/\/login\?next=/);
  });
});

async function signIn(page: Page, email: string, password: string) {
  await page.goto('/login');

  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();

  // A CORS failure lands here: the request never reaches the API and the form
  // shows "Could not reach the API".
  await expect(page.getByText('Could not reach the API')).toHaveCount(0);
  await expect(page.getByRole('link', { name: /dashboard/i })).toBeVisible({ timeout: 15_000 });
}

/** Mantine's Select is a combobox, not a native <select>. */
async function pickFirstOption(page: Page, label: string) {
  const input = page.getByLabel(label, { exact: true });

  await input.click();

  const option = page.getByRole('option').first();

  if (await option.isVisible().catch(() => false)) {
    await option.click();
  } else {
    // No terms of this kind are seeded; leave the field empty rather than
    // failing the whole flow over a taxonomy that was never populated.
    await page.keyboard.press('Escape');
  }
}
