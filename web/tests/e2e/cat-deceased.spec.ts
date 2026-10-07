import { test, expect, signInAsStaff } from './fixtures';
import { catProfilesFixtureIds } from '../../mocks/handlers/areas/cat-profiles';

// ARCH gate MEDIUM (first pass) — signs in as staff only (never both cookies at
// once, tests/e2e/fixtures.ts); every route this file visits is staff-audience.
test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

/** E11, E12 (HO-CAT-002 §16) — US-CAT-09. */
test('E11, E12 — marking a cat deceased confirms, bans the cat from the default list, disables editing', async ({
  page,
}) => {
  const catId = catProfilesFixtureIds.catCurrentPlain; // Mittens
  await page.goto(`/staff/cats/${catId}`);

  await page.getByRole('button', { name: 'Mark as deceased' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toContainText(/reminders/i);
  await expect(dialog).toContainText(/hidden from booking and ordering/i);
  await expect(dialog).toContainText(/record itself is kept/i);
  await dialog.getByRole('button', { name: 'Mark as deceased' }).click();

  await expect(page.getByText(/marked as deceased/i).first()).toBeVisible();
  await expect(page.getByText(/recorded as deceased/i).first()).toBeVisible();

  // E12 — the cat list excludes it by default, and includes it with the filter on. Client-side
  // (Link) navigation throughout from here — see cat-weights.spec.ts's comment on why not
  // page.goto after a mutation against the in-memory mock.
  await page.getByRole('link', { name: 'Cats' }).click();
  await expect(page.getByRole('link', { name: 'Mittens' })).toHaveCount(0);
  // .click(), not .check() — the checkbox is controlled by the URL (?includeDeceased=true) via a
  // client-side router.replace, so its DOM `checked` can legitimately flicker for a tick before
  // that resolves; .check()'s built-in "did the state actually change" assertion is stricter
  // than this test needs; the real assertion is Mittens becoming visible below.
  await page.getByLabel('Include deceased cats').click();
  await expect(page.getByRole('link', { name: 'Mittens' })).toBeVisible();

  // E12 (continued, US-CAT-09 AC2) — still viewable, edit controls disabled with an explanation.
  await page.getByRole('link', { name: 'Mittens' }).click();
  await page.getByRole('link', { name: 'Edit' }).click();
  await expect(page.getByText(/recorded as deceased/i).first()).toBeVisible();
  await expect(page.getByLabel(/^Name/)).toBeDisabled();
});
