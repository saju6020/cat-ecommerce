import { test, expect, signInAsStaff, signInAsCustomer } from './fixtures';
import AxeBuilder from '@axe-core/playwright';

// Smoke coverage for the foundation itself, ahead of any area's own E2E specs (HO-CAT-002 §16).
test('placeholder home page has no serious/critical accessibility violations', async ({ page }) => {
  await page.goto('/');
  // HO-SHP-002 OQ-1: the root now lands a signed-out visitor on the public shop.
  await page.waitForURL(/\/shop$/);
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

  const results = await new AxeBuilder({ page }).analyze();
  const serious = results.violations.filter(
    (v) => v.impact === 'serious' || v.impact === 'critical',
  );
  expect(serious).toEqual([]);
});

// SECURITY gate item 7 (first-pass gate fix round) — app/page.tsx no longer reads session cookies
// itself (a Server Component next/headers read, which never sees a cookie scoped to the API's own
// origin in a real cross-subdomain deployment); it asks the API client-side instead, the same way
// CustomerSessionGuard/StaffSessionGuard do. This MSW-backed run can't reproduce the actual
// cross-origin cookie-visibility bug (both the app and its mocks share one origin here regardless
// of which approach reads them) — it only proves the client-side rewrite still resolves to the
// right destination in every case, the same behaviour the old Server Component version had.
test.describe('root redirect by session (SECURITY gate item 7)', () => {
  test('signed out lands on the public shop (HO-SHP-002 OQ-1), never staff sign-in', async ({
    page,
  }) => {
    await page.goto('/');
    await expect(page).toHaveURL(/\/shop$/);
  });

  test('signed in as staff lands on /staff (the home dashboard, HO-RPT-002 ADR-FE-130)', async ({
    page,
  }) => {
    await signInAsStaff(page);
    await page.goto('/');
    await expect(page).toHaveURL(/\/staff$/);
  });

  test('signed in as a customer lands on /portal (owner, 2026-10-13)', async ({ page }) => {
    await signInAsCustomer(page);
    await page.goto('/');
    await expect(page).toHaveURL(/\/portal$/);
  });
});
