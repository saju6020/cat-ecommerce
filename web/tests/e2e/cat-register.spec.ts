import { test, expect, signInAsStaff } from './fixtures';

// ARCH gate MEDIUM (first pass) — signs in as staff only (never both cookies at
// once, tests/e2e/fixtures.ts); every route this file visits is staff-audience.
test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

/** E1, E2 (HO-CAT-002 §16) — register a cat. Run against MSW (§5.1); re-run against the real API
 *  once HO-CAT-001 is deployed and reachable. */
test.describe('Register a cat', () => {
  test('E1 — registering lands on the profile, which shows the entered data', async ({ page }) => {
    await page.goto('/staff/cats/new');

    await page.getByLabel('Owner customer ID').fill('10000000-0000-0000-0000-000000000009');
    await page.getByLabel(/^Name/).fill('Nutmeg');
    await page.getByLabel('Breed').fill('Ragdoll');

    await page.getByRole('button', { name: 'Register cat' }).click();

    await expect(page).toHaveURL(/\/staff\/cats\/[0-9a-f-]+$/);
    await expect(page.getByRole('heading', { level: 1, name: 'Nutmeg' })).toBeVisible();
    await expect(page.getByText('Ragdoll')).toBeVisible();
  });

  test('E2 — an empty name blocks submission, keeps the page, and preserves entered data (NFR-USA-5)', async ({
    page,
  }) => {
    await page.goto('/staff/cats/new');

    await page.getByLabel('Owner customer ID').fill('10000000-0000-0000-0000-000000000009');
    await page.getByLabel('Breed').fill('Ragdoll');
    await page.getByRole('button', { name: 'Register cat' }).click();

    // Scoped to the field error specifically — the page also has an always-present
    // (empty) route-announcer `role="alert"` region Next.js renders for screen readers.
    await expect(page.locator('#name-error')).toContainText(/enter a name/i);
    await expect(page).toHaveURL('/staff/cats/new');
    // The rest of the form's entered data survived the failed validation.
    await expect(page.getByLabel('Breed')).toHaveValue('Ragdoll');
    await expect(page.getByLabel('Owner customer ID')).toHaveValue(
      '10000000-0000-0000-0000-000000000009',
    );
  });

  test('E16 — keyboard only, register a cat is completable with no mouse; focus stays visible', async ({
    page,
  }) => {
    await page.goto('/staff/cats/new');

    await page.getByLabel('Owner customer ID').click();
    await page.keyboard.type('10000000-0000-0000-0000-000000000009');
    await page.keyboard.press('Tab');
    await page.keyboard.type('Juniper'); // Name

    // Tab forward (Species, Sex, the date-of-birth radios, Breed, Colour, …) until focus reaches
    // the submit button — bounded, rather than a brittle exact Tab count, since it only proves
    // the button is *reachable* by keyboard, which is UI-6's actual requirement here.
    const submitButton = page.getByRole('button', { name: 'Register cat' });
    for (let i = 0; i < 15; i += 1) {
      if (await submitButton.evaluate((el) => el === document.activeElement)) break;
      await page.keyboard.press('Tab');
    }
    await expect(submitButton).toBeFocused();
    await page.keyboard.press('Enter');

    await expect(page).toHaveURL(/\/staff\/cats\/[0-9a-f-]+$/, { timeout: 10_000 });
  });
});
