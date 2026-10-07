import { test, expect, signInAsStaff } from './fixtures';
import { catProfilesFixtureIds } from '../../mocks/handlers/areas/cat-profiles';

// ARCH gate MEDIUM (first pass) — signs in as staff only (never both cookies at
// once, tests/e2e/fixtures.ts); every route this file visits is staff-audience.
test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

/**
 * A11 (HO-CAT-002 §18) — US-CAT-06: a parasite treatment records **and shows its next-due date**.
 * HO-CAT-001's C10 is amended (owner decision 2026-09-25) so its `201` body carries the
 * server-computed `nextDueOn`; the mock (`web/mocks/handlers/areas/cat-profiles.ts`) stands in for
 * that computation, and the page renders exactly the value returned, never its own
 * `administeredOn + intervalDays` (the ARCHITECTURE gate rejected that duplication of the
 * aggregate rule for the analogous vaccination case, fix `1a4cac4`).
 */
test('a parasite treatment records and shows its next-due date', async ({ page }) => {
  const catId = catProfilesFixtureIds.catCurrentPlain; // Mittens
  await page.goto(`/staff/cats/${catId}/parasite-control`);

  await page.getByLabel('Product').fill('Broadline');
  await page.getByLabel('Date given').fill('2026-09-20');
  await page.getByLabel('Interval (days)').fill('90');
  await page.getByLabel('Administered by').fill('Staff Member');
  await page.getByRole('button', { name: 'Record treatment' }).click();

  await expect(page.getByText('Treatment recorded').first()).toBeVisible();
  // 2026-09-20 + 90 days = 2026-12-19 (10 remaining in Sep + 31 Oct + 30 Nov + 19 Dec).
  await expect(page.getByRole('status').getByText('Next due 19 Dec 2026')).toBeVisible();
});
