import { test, expect, signInAsStaff } from './fixtures';
import { catProfilesFixtureIds } from '../../mocks/handlers/areas/cat-profiles';

// ARCH gate MEDIUM (first pass) — signs in as staff only (never both cookies at
// once, tests/e2e/fixtures.ts); every route this file visits is staff-audience.
test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

/** E20, E21 (HO-CAT-002 §16) — NFR-USA-4, and the 409 concurrent-edit contract. */

test('E20 — a simulated 409 tells the user the record changed, and keeps their input', async ({
  page,
}) => {
  const catId = catProfilesFixtureIds.catConflict; // Clover — 409s once, then succeeds
  await page.goto(`/staff/cats/${catId}/edit`);

  await page.getByLabel(/^Name/).fill('Clover the Second');
  await page.getByRole('button', { name: 'Save changes' }).click();

  await expect(page.getByText(/someone else changed this record/i)).toBeVisible();
  // The entered value was not discarded by the failed save.
  await expect(page.getByLabel(/^Name/)).toHaveValue('Clover the Second');

  // Retrying succeeds (the mock only 409s once per cat).
  await page.getByRole('button', { name: 'Save changes' }).click();
  await expect(page).toHaveURL(`/staff/cats/${catId}`);
});

test('E21 — a simulated 500 shows a plain-language message, no stack trace or GUID', async ({
  page,
}) => {
  const catId = catProfilesFixtureIds.catServerError;
  await page.goto(`/staff/cats/${catId}`);

  const body = await page.locator('body').innerText();
  expect(body).not.toMatch(/Exception|Stack ?Trace|System\./i);
  expect(body).not.toMatch(new RegExp(catId, 'i'));
  await expect(page.getByText(/couldn't load/i)).toBeVisible();
});
