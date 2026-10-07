import { test, expect, signInAsStaff, signInAsCustomer } from './fixtures';
import { catProfilesFixtureIds } from '../../mocks/handlers/areas/cat-profiles';

/** E18, E19 (HO-CAT-002 §16) — UI-1. 360 px is the hard floor; 768/1024/1440 confirm the layout
 *  stays intact above it. Checked on a representative slice of screens (list, profile header +
 *  tabs, a long form, the portal) rather than every single tab, which would just repeat the same
 *  layout primitives (`CatProfileHeader`, `DataTable`) already checked elsewhere.
 *
 * ARCH gate MEDIUM (first pass) — staff and portal screens are now two separate test bodies, each
 * signing in for its own single audience (tests/e2e/fixtures.ts no longer signs in as both). */
const BREAKPOINTS = [360, 768, 1024, 1440];

const STAFF_SCREENS = (catId: string) => [
  '/staff/cats',
  '/staff/cats/new',
  `/staff/cats/${catId}`,
  `/staff/cats/${catId}/medical`,
  `/staff/cats/${catId}/weights`,
  // HO-CAT-002 §1 Amended (2026-09-27) — the smart-redirect page itself, not just its
  // destination: `expectFinalUrl` below proves the redirect actually landed before measuring.
  { path: `/cats/${catId}`, expectFinalUrl: `/staff/cats/${catId}` },
];

const PORTAL_SCREENS = (catId: string) => [
  '/portal/cats',
  `/portal/cats/${catId}`,
  { path: `/cats/${catId}`, expectFinalUrl: `/portal/cats/${catId}` },
];

async function assertNoHorizontalScroll(
  page: import('@playwright/test').Page,
  screen: string | { path: string; expectFinalUrl: string },
) {
  const path = typeof screen === 'string' ? screen : screen.path;
  await page.goto(path);
  if (typeof screen !== 'string') {
    await expect(page).toHaveURL(screen.expectFinalUrl);
  }
  // `networkidle` only waits for network activity to settle — it does not, and cannot, wait
  // for a JS effect that isn't a network request. This test used to catch a real 360px
  // overflow (HO-CAT-002 TEST gate, E18) because StaffShell's sidebar-collapse default was
  // decided by a mount-time `matchMedia` effect: on a full navigation, there was a window —
  // before that effect ran — where the sidebar rendered at its wide, un-collapsed width, and
  // dev-mode JS parse/hydrate time was sometimes long enough for `networkidle` to resolve
  // inside that window. StaffShell's default collapse state is now pure CSS (a `sm:` Tailwind
  // breakpoint pair, applied by the browser before any script runs), so there is no longer a
  // JS-timing race here to wait out — `networkidle` is only doing its ordinary job of letting
  // the page's own data fetch settle before measuring.
  await page.waitForLoadState('networkidle');
  const { scrollWidth, clientWidth } = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
  }));
  expect(scrollWidth, `${path} overflows horizontally`).toBeLessThanOrEqual(
    clientWidth + 1, // +1px tolerance for sub-pixel rounding
  );
}

for (const width of BREAKPOINTS) {
  test(`E18/E19 — staff screens have no horizontal scroll at ${width}px`, async ({ page }) => {
    await signInAsStaff(page);
    await page.setViewportSize({ width, height: 900 });
    for (const path of STAFF_SCREENS(catProfilesFixtureIds.catWithAllergiesExactDob)) {
      await assertNoHorizontalScroll(page, path);
    }
  });

  test(`E18/E19 — portal screens have no horizontal scroll at ${width}px`, async ({ page }) => {
    await signInAsCustomer(page);
    await page.setViewportSize({ width, height: 900 });
    for (const path of PORTAL_SCREENS(catProfilesFixtureIds.catWithAllergiesExactDob)) {
      await assertNoHorizontalScroll(page, path);
    }
  });
}
