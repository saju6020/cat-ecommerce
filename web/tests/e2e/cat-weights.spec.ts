import { test, expect, signInAsStaff } from './fixtures';
import { catProfilesFixtureIds } from '../../mocks/handlers/areas/cat-profiles';

// ARCH gate MEDIUM (first pass) — signs in as staff only (never both cookies at
// once, tests/e2e/fixtures.ts); every route this file visits is staff-audience.
test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

/** E5 (HO-CAT-002 §16) — record a weight; the point appears, and the header's latest weight
 *  updates (US-CAT-02 AC2 — proves the mutation invalidates ['cat', catId], §8.4). */
test('E5 — recording a weight updates the chart data and the header', async ({ page }) => {
  const catId = catProfilesFixtureIds.catCurrentPlain; // Mittens
  await page.goto(`/staff/cats/${catId}/weights`);

  await page.getByRole('button', { name: 'Record weight' }).click();
  await page.getByLabel(/^Weight \(kg\)/).fill('5.5');
  await page.getByLabel('Date recorded').fill('2026-09-20');
  await page.getByRole('button', { name: 'Record weight' }).click();

  // "5.5 kg" now appears in the visible history table and the chart's sr-only table — .first()
  // is enough to prove the point was recorded.
  await expect(page.getByText('5.5 kg').first()).toBeVisible();

  // A client-side (Link) navigation, not page.goto — the mock's in-memory data (mocks/handlers/
  // areas/cat-profiles.ts) lives in this page load's JS module instance and resets on a real
  // navigation, same as a real backend would reset if this were pointed at a fresh test database
  // per request. Client-side routing keeps the same module instance, which is what actually
  // proves the mutation is visible elsewhere, not just re-fetches fresh seed data that happens
  // to look right.
  await page.getByRole('link', { name: 'Overview' }).click();
  await expect(page.getByText('5.5 kg').first()).toBeVisible(); // header's "Latest weight"
});
