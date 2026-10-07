import { test, expect, signInAsStaff, signInAsCustomer } from './fixtures';
import AxeBuilder from '@axe-core/playwright';
import { catProfilesFixtureIds } from '../../mocks/handlers/areas/cat-profiles';

/** E17 (HO-CAT-002 §16) — UI-6. Zero serious/critical axe violations on every screen in §8.3. */
const catId = catProfilesFixtureIds.catWithAllergiesExactDob; // has the most content: allergies,
// conditions, medications, photos, high-risk off, deceased off — a representative "full" profile

const SCREENS = [
  '/staff/cats',
  '/staff/cats/new',
  `/staff/cats/${catId}`,
  `/staff/cats/${catId}/edit`,
  `/staff/cats/${catId}/identification`,
  `/staff/cats/${catId}/medical`,
  `/staff/cats/${catId}/weights`,
  `/staff/cats/${catId}/vaccinations`,
  `/staff/cats/${catId}/vaccination-status`,
  `/staff/cats/${catId}/parasite-control`,
  `/staff/cats/${catId}/photos`,
  `/staff/cats/${catId}/cage-card`,
  '/staff/vaccines',
  '/staff/vaccines/new',
  '/portal/cats',
  `/portal/cats/${catId}`,
  `/portal/cats/${catId}/medical-history`,
];

for (const path of SCREENS) {
  test(`E17 — ${path} has no serious/critical accessibility violations`, async ({ page }) => {
    // ARCH gate MEDIUM (first pass) — one audience per test, never both (tests/e2e/fixtures.ts).
    if (path.startsWith('/portal/')) await signInAsCustomer(page);
    else await signInAsStaff(page);
    await page.goto(path);
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page }).analyze();
    const serious = results.violations.filter(
      (v) => v.impact === 'serious' || v.impact === 'critical',
    );
    expect(serious, JSON.stringify(serious, null, 2)).toEqual([]);
  });
}

/**
 * `/cats/{id}` (HO-CAT-002 §1 Amended, 2026-09-27, both the original row and the owner's later
 * 2026-09-27 revision) redirects by audience — and, for a signed-in customer, by ownership — so it
 * can't join the generic `SCREENS` loop above (which picks staff-vs-customer sign-in from the
 * path prefix alone — `/cats/{id}` has neither). Each case here asserts the FINAL, intended screen
 * actually rendered before running axe against it, rather than axing whatever the loading state
 * happened to be.
 */
test.describe('smart redirect — /cats/{id}', () => {
  async function assertNoSeriousAxeViolations(page: import('@playwright/test').Page) {
    await page.waitForLoadState('networkidle');
    const results = await new AxeBuilder({ page }).analyze();
    const serious = results.violations.filter(
      (v) => v.impact === 'serious' || v.impact === 'critical',
    );
    expect(serious, JSON.stringify(serious, null, 2)).toEqual([]);
  }

  test('staff: redirects to the staff cat profile with no accessibility violations', async ({
    page,
  }) => {
    await signInAsStaff(page);
    await page.goto(`/cats/${catId}`);
    await expect(page).toHaveURL(`/staff/cats/${catId}`);
    await assertNoSeriousAxeViolations(page);
  });

  test('customer who owns the cat: redirects to the portal cat profile with no accessibility violations', async ({
    page,
  }) => {
    await signInAsCustomer(page);
    await page.goto(`/cats/${catId}`);
    await expect(page).toHaveURL(`/portal/cats/${catId}`);
    await assertNoSeriousAxeViolations(page);
  });

  test('customer who does NOT own the cat: redirects to staff sign-in with no accessibility violations', async ({
    page,
  }) => {
    await signInAsCustomer(page);
    // See tests/e2e/cat-smart-redirect.spec.ts's own helper doc comment — `addInitScript`, not
    // `page.evaluate`, because the ownership check fires right after the FULL navigation below,
    // which tears down whatever `page.evaluate` would have set on the CURRENT page first.
    await page.addInitScript(() => {
      (window as unknown as { __forceCatQueryNotOwned?: boolean }).__forceCatQueryNotOwned = true;
    });
    await page.goto(`/cats/${catId}`);
    await expect(page).toHaveURL(
      `/staff/sign-in?next=${encodeURIComponent(`/staff/cats/${catId}`)}`,
    );
    await assertNoSeriousAxeViolations(page);
  });

  test('anonymous: redirects to staff sign-in with no accessibility violations', async ({
    page,
  }) => {
    await page.goto(`/cats/${catId}`);
    await expect(page).toHaveURL(
      `/staff/sign-in?next=${encodeURIComponent(`/staff/cats/${catId}`)}`,
    );
    await assertNoSeriousAxeViolations(page);
  });
});
