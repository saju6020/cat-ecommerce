import { test, expect, signInAsStaff } from './fixtures';
import { catProfilesFixtureIds } from '../../mocks/handlers/areas/cat-profiles';

// ARCH gate MEDIUM (first pass) — signs in as staff only (never both cookies at
// once, tests/e2e/fixtures.ts); every route this file visits is staff-audience.
test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

/** E3, E4 (HO-CAT-002 §16) — UI-7 and US-CAT-03 AC2 hold on every tab, not just the overview. */
const TABS = [
  '',
  '/edit',
  '/identification',
  '/medical',
  '/weights',
  '/vaccinations',
  '/vaccination-status',
  '/parasite-control',
  '/photos',
  '/cage-card',
];

test('E3 — a high-risk cat shows the badge and handling note on every tab (US-CAT-04 AC1, UI-7)', async ({
  page,
}) => {
  // Ten full navigations, each a fresh page load (Next dev compiles routes on first hit) — well
  // past the 30s default.
  test.setTimeout(120_000);
  const catId = catProfilesFixtureIds.catHighRiskOverdue; // Tiger
  for (const tab of TABS) {
    await page.goto(`/staff/cats/${catId}${tab}`);
    // .first() — on the cage-card tab, CageCard.tsx carries its own (print-only, so hidden on
    // screen but still present in the DOM) copy of the same warning; see that component's doc
    // comment.
    await expect(page.getByText('High risk — handle with care').first()).toBeVisible();
    await expect(page.getByText(/muzzle required/i).first()).toBeVisible();
  }
});

test('E4 — a cat with no allergies shows "None recorded" on every tab (US-CAT-03 AC2)', async ({
  page,
}) => {
  test.setTimeout(120_000);
  const catId = catProfilesFixtureIds.catNoAllergiesEstimatedDob; // Biscuit
  for (const tab of TABS) {
    await page.goto(`/staff/cats/${catId}${tab}`);
    await expect(page.getByText('None recorded').first()).toBeVisible();
  }
});
