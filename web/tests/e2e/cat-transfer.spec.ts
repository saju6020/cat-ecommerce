import { test, expect, signInAsStaff } from './fixtures';
import { catProfilesFixtureIds } from '../../mocks/handlers/areas/cat-profiles';

// ARCH gate MEDIUM (first pass) — signs in as staff only (never both cookies at
// once, tests/e2e/fixtures.ts); every route this file visits is staff-audience.
test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

/** E13 (HO-CAT-002 §16) — US-CAT-08 AC1, UI-3. */
test('E13 — transferring ownership confirms what transfers and succeeds', async ({ page }) => {
  const catId = catProfilesFixtureIds.catCurrentPlain; // Mittens
  await page.goto(`/staff/cats/${catId}`);

  await page.getByRole('button', { name: 'Transfer ownership' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toContainText(/medical history/i);
  await expect(dialog).toContainText(/moves to the new owner/i);
  await expect(dialog).toContainText(/no longer see this record/i);

  await dialog.getByLabel('New owner customer ID').fill('10000000-0000-0000-0000-000000000099');
  await dialog.getByLabel('Reason').fill('Cat rehomed to a family member.');
  await dialog.getByRole('button', { name: 'Transfer ownership' }).click();

  await expect(page.getByText(/ownership transferred/i).first()).toBeVisible();
});
