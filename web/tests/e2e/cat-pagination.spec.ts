import { test, expect, signInAsStaff } from './fixtures';

// ARCH gate MEDIUM (first pass) — signs in as staff only (never both cookies at
// once, tests/e2e/fixtures.ts); every route this file visits is staff-audience.
test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

/** E22 (HO-CAT-002 §16) — UI-4. The unfiltered staff cat list carries 25 pagination-fixture cats
 *  plus the named fixtures (>10 total), so pagination is real without needing an owner filter. */
test('E22 — pagination works, and the page survives a reload', async ({ page }) => {
  await page.goto('/staff/cats');

  // exact: true — Next.js dev mode's own floating "Open Next.js Dev Tools" button's accessible
  // name also contains "Next", which would otherwise collide in a substring match.
  const nextButton = page.getByRole('button', { name: 'Next', exact: true });
  await expect(nextButton).toBeEnabled();
  await nextButton.click();

  await expect(page).toHaveURL(/page=2/);
  await expect(page.getByRole('table')).toBeVisible(); // waits out the loading skeleton
  const rowsOnPage2 = await page.getByRole('row').count();
  expect(rowsOnPage2).toBeGreaterThan(1); // header row + at least one data row

  await page.reload();
  await expect(page).toHaveURL(/page=2/);
  await expect(page.getByRole('table')).toBeVisible();
  const rowsAfterReload = await page.getByRole('row').count();
  expect(rowsAfterReload).toBe(rowsOnPage2);
});
