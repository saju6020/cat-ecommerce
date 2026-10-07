import AxeBuilder from '@axe-core/playwright';
import { test, expect, signInAsStaff, signInAsCustomer } from './fixtures';
import { accountsFixtureIds } from '../../mocks/handlers/areas/accounts';
import type { Page } from '@playwright/test';

/**
 * HO-ACC-002 §16 E-X1/E-X2/E-X3, run against MSW.
 *
 * ACCEPTANCE gate HIGH (this pass) — this used to run against a 7-route "representative sample",
 * narrowed only in this file's own header comment and disclosed nowhere else (not §1 Amended, not
 * the TC sheet, not any gate file). `ROUTES` now covers every §8.3 screen this handover owns (19
 * routes: the full `(auth)`/`(portal)`/`(staff)` set — HO-CAT-002's own cat/vaccine routes are out
 * of scope, §4.2). Each entry carries its own `audience`, rather than the old prefix guess
 * (`route.startsWith('/staff/')`), because two `(auth)` pages — `/staff/sign-in` and
 * `/staff/forgot-password` — live under a `/staff/` path without needing a staff session (§8.3's
 * own note: route groups, not path prefixes, gate a session). Dynamic routes use seeded MSW ids
 * (`accountsFixtureIds`) so `GET` resolves real data instead of a 404/`ErrorState`, which would
 * otherwise dominate axe's own violation count with irrelevant results.
 *
 * ARCH gate MEDIUM (first pass) — one audience per test, never both (tests/e2e/fixtures.ts); the
 * `(auth)` routes need neither.
 */

interface RouteCase {
  path: string;
  audience: 'none' | 'customer' | 'staff';
}

const ROUTES: RouteCase[] = [
  // (auth) — no session.
  { path: '/register', audience: 'none' },
  { path: '/sign-in', audience: 'none' },
  { path: '/forgot-password', audience: 'none' },
  { path: '/staff/forgot-password', audience: 'none' },
  { path: '/reset-password', audience: 'none' },
  { path: '/account-deleted', audience: 'none' },
  { path: '/staff/sign-in', audience: 'none' },
  // (portal) — customer session.
  { path: '/portal/account/profile', audience: 'customer' },
  { path: '/portal/account/addresses', audience: 'customer' },
  { path: '/portal/account/notifications', audience: 'customer' },
  { path: '/portal/account/data', audience: 'customer' },
  { path: '/portal/account/data/delete', audience: 'customer' },
  // (staff) — staff session.
  { path: '/staff/admin/staff-accounts', audience: 'staff' },
  { path: '/staff/admin/staff-accounts/new', audience: 'staff' },
  { path: `/staff/admin/staff-accounts/${accountsFixtureIds.nurseStaffId}`, audience: 'staff' },
  { path: '/staff/customers/new', audience: 'staff' },
  { path: `/staff/customers/${accountsFixtureIds.customerId}`, audience: 'staff' },
  { path: '/staff/customers/duplicates', audience: 'staff' },
  {
    path: `/staff/customers/duplicates/review?ids=${accountsFixtureIds.duplicateGroupCustomerA},${accountsFixtureIds.duplicateGroupCustomerB}`,
    audience: 'staff',
  },
];

async function signInForAudience(page: Page, audience: RouteCase['audience']): Promise<void> {
  if (audience === 'customer') await signInAsCustomer(page);
  else if (audience === 'staff') await signInAsStaff(page);
  // else: 'none' — an (auth) route, no session needed.
}

const VIEWPORTS = [
  { width: 360, height: 780 },
  { width: 768, height: 1024 },
  { width: 1024, height: 768 },
  { width: 1440, height: 900 },
];

async function expectNoSeriousOrCriticalViolations(page: Page): Promise<void> {
  const results = await new AxeBuilder({ page }).analyze();
  const serious = results.violations.filter(
    (v) => v.impact === 'serious' || v.impact === 'critical',
  );
  expect(serious, JSON.stringify(serious, null, 2)).toEqual([]);
}

test.describe('E-X1: axe — zero serious/critical violations', () => {
  for (const { path, audience } of ROUTES) {
    test(`${path}`, async ({ page }) => {
      await signInForAudience(page, audience);
      await page.goto(path);
      await page.waitForLoadState('networkidle');
      await expectNoSeriousOrCriticalViolations(page);
    });
  }
});

test.describe('E-X2: no horizontal scroll at 360/768/1024/1440', () => {
  for (const { path, audience } of ROUTES) {
    for (const viewport of VIEWPORTS) {
      test(`${path} at ${viewport.width}px`, async ({ page }) => {
        await signInForAudience(page, audience);
        await page.setViewportSize(viewport);
        await page.goto(path);
        await page.waitForLoadState('networkidle');
        const hasHorizontalScroll = await page.evaluate(
          () => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1,
        );
        expect(hasHorizontalScroll, `${path} overflows at ${viewport.width}px`).toBe(false);
      });
    }
  }
});

// ACCEPTANCE gate HIGH (this pass) — §16 E-X1 also asks for "with errors" and "idle-lock-open"
// states, which no run covered at all. Both are single, representative cases (§2: the CAPTCHA, the
// idle lock and the preference matrix are the three places WCAG is hardest) rather than every
// route repeated in both states, matching the finding's own fix direction.
test('E-X1 (with errors shown): /register after an empty submit, with every inline error visible', async ({
  page,
}) => {
  await page.goto('/register');
  await page.waitForLoadState('networkidle');
  await page.getByRole('button', { name: 'Create account' }).click();
  // RegisterForm.tsx renders every field-level and form-level error as `role="alert"` — waiting
  // for at least one confirms the errored state has actually rendered before axe runs, rather than
  // racing the client-side zod validation.
  await page.getByRole('alert').first().waitFor();
  await expectNoSeriousOrCriticalViolations(page);
});

test('E-X1 (idle lock open): axe on the staff idle-lock dialog, background inert, over a real page', async ({
  page,
}) => {
  test.setTimeout(45_000);
  await signInAsStaff(page);
  await page.goto('/staff/customers/new');
  await page.waitForLoadState('networkidle');

  const cookies = await page.context().cookies();
  const staffSessionCookie = cookies.find((c) => c.name === 'pc_staff_session');
  expect(
    staffSessionCookie,
    'pc_staff_session cookie must exist after signing in as staff',
  ).toBeTruthy();
  const token = staffSessionCookie!.value;

  // Same mechanism as `staff-auth.spec.ts`'s E-G2/ARCH gate HIGH 1 tests — see their own comments.
  await page.evaluate((forcedToken) => {
    (
      window as unknown as { __forceStaffIdleExpired?: (t: string) => void }
    ).__forceStaffIdleExpired?.(forcedToken);
  }, token);
  await page.evaluate(() => {
    (
      window as unknown as { __invalidateQueries?: (queryKey: readonly unknown[]) => void }
    ).__invalidateQueries?.(['session', 'staff']);
  });

  await page.getByRole('dialog').waitFor();
  await expectNoSeriousOrCriticalViolations(page);
});

test('E-X3: after signing out, no pc_session cookie is readable and web storage is empty', async ({
  page,
}) => {
  await signInAsCustomer(page);
  await page.goto('/portal/account/profile');
  await page.getByRole('button', { name: 'Sign out' }).click();
  await expect(page).toHaveURL(/\/sign-in/);

  const cookies = await page.context().cookies();
  const sessionCookie = cookies.find((c) => c.name === 'pc_session');
  expect(sessionCookie === undefined || sessionCookie.value === '').toBe(true);

  // Not a blanket `localStorage.length`/`sessionStorage.length` toBe(0) — this app itself never
  // writes to `localStorage` (confirmed: no call site in `lib`/`components`/`app`), but MSW's own
  // interceptor library keeps an internal `__msw-cookie-store__` bookkeeping entry there as soon as
  // ANY cookie has ever been set in this browser context (present even for the STAFF session this
  // fixture also signs in as, and for this cookie's own now-expired entry) — mock-harness plumbing,
  // not this app's state, so asserting it away would test MSW's implementation rather than this
  // app's. `sessionStorage` genuinely IS used deliberately (`mocks/handlers/areas/accounts.ts`'s
  // own persisted mock "database", the counterpart to the real API's Postgres) and is expected to
  // keep customer/staff records after a sign-out, exactly as a real database would. What actually
  // matters — the one thing sign-out promises — is that the SESSION itself is gone from the one
  // place this mock (like the real cookie-based transport) treats as authoritative for it.
  const documentCookie = await page.evaluate(() => document.cookie);
  expect(documentCookie).not.toMatch(/pc_session=/);

  // MEDIUM/LOW gate item 12 (first pass) — A26/E-X3's own wording ("No token, password, answer or
  // draft in browser storage") is restored here for the one part of it this mock's persisted
  // "database" (the comment above) previously couldn't satisfy: `mocks/handlers/areas/accounts.ts`
  // used to persist every registered/reset password as PLAINTEXT into this same `sessionStorage`
  // entry. It now stores `hashPassword()`'s output instead (see that function's own comment) — this
  // asserts the actual plaintext password this test's own sign-in used is nowhere in the serialised
  // mock "database", not merely that the field is absent by name.
  const sessionStorageDump = await page.evaluate(() => JSON.stringify(window.sessionStorage));
  expect(sessionStorageDump).not.toContain(accountsFixtureIds.customerPassword);
});
