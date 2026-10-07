import AxeBuilder from '@axe-core/playwright';
import { test, expect, signInAsStaff, signInAsCustomer } from './fixtures';
import { administrationFixtureIds } from '../../mocks/handlers/admin';
import type { Page } from '@playwright/test';

/**
 * HO-ADM-002 §16 E-X1/E-X2, run against MSW. Every route this handover's own §8.1 route map adds,
 * dynamic segments resolved with seeded MSW ids (`administrationFixtureIds`) so each one renders
 * real data rather than a loading/404 state, which would otherwise dominate axe's own violation
 * count with irrelevant results (ACCEPTANCE-gate precedent, HO-ACC-002's own
 * `accounts-a11y-responsive.spec.ts`).
 *
 * ACCEPTANCE-gate precedent (HO-ACC-002, same file) — §16's own "clean, with errors, with the
 * policy dialog open" is run as the base clean sweep over EVERY route below, plus one
 * representative "with errors" case and one representative "with the policy dialog open" case
 * (E-X1's own three named states), rather than every route repeated in all three states.
 */

interface RouteCase {
  path: string;
  audience: 'none' | 'customer' | 'staff';
  /** A heading (any level) expected once the route has actually rendered — proves the route under
   *  test, not a loading skeleton or an error boundary, is what axe/overflow actually ran against. */
  heading: RegExp;
}

const { mainBranchId, serviceConsultationId, taxClassStandardId, contentPageId, policyKey } =
  administrationFixtureIds;

const ROUTES: RouteCase[] = [
  { path: '/staff/admin/branches', audience: 'staff', heading: /branches/i },
  { path: '/staff/admin/branches/new', audience: 'staff', heading: /new branch/i },
  { path: `/staff/admin/branches/${mainBranchId}`, audience: 'staff', heading: /main clinic/i },
  {
    path: `/staff/admin/branches/${mainBranchId}/hours`,
    audience: 'staff',
    heading: /opening hours/i,
  },
  {
    path: `/staff/admin/branches/${mainBranchId}/closures`,
    audience: 'staff',
    heading: /closures/i,
  },
  { path: `/staff/admin/branches/${mainBranchId}/rooms`, audience: 'staff', heading: /rooms/i },
  { path: `/staff/admin/branches/${mainBranchId}/roster`, audience: 'staff', heading: /roster/i },
  { path: '/staff/admin/leave', audience: 'staff', heading: /leave approvals/i },
  { path: '/staff/leave', audience: 'staff', heading: /my leave/i },
  { path: '/staff/admin/services', audience: 'staff', heading: /services/i },
  { path: '/staff/admin/services/new', audience: 'staff', heading: /new service/i },
  {
    path: `/staff/admin/services/${serviceConsultationId}`,
    audience: 'staff',
    heading: /general consultation/i,
  },
  { path: '/staff/admin/tax-classes', audience: 'staff', heading: /tax classes/i },
  {
    path: `/staff/admin/tax-classes/${taxClassStandardId}`,
    audience: 'staff',
    heading: /standard/i,
  },
  { path: '/staff/admin/settings', audience: 'staff', heading: /settings/i },
  { path: '/staff/admin/audit-log', audience: 'staff', heading: /audit log/i },
  { path: '/staff/admin/content', audience: 'staff', heading: /content/i },
  { path: '/staff/admin/content/new', audience: 'staff', heading: /new content/i },
  { path: `/staff/admin/content/${contentPageId}`, audience: 'staff', heading: /about us/i },
  { path: '/staff/admin/policies', audience: 'staff', heading: /policies/i },
  { path: `/staff/admin/policies/${policyKey}`, audience: 'staff', heading: /terms of service/i },
  { path: '/staff/admin/system', audience: 'staff', heading: /maintenance/i },
  {
    path: `/staff/admin/staff-accounts/${administrationFixtureIds.adminStaffId}`,
    audience: 'staff',
    heading: /ada administrator|administrator/i,
  },
  { path: '/pages/about-us', audience: 'none', heading: /about us/i },
  { path: '/faqs', audience: 'none', heading: /frequently asked questions/i },
  { path: `/policies/${policyKey}`, audience: 'none', heading: /terms of service/i },
];

async function signInForAudience(page: Page, audience: RouteCase['audience']): Promise<void> {
  if (audience === 'customer') await signInAsCustomer(page);
  else if (audience === 'staff') await signInAsStaff(page);
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

async function gotoAndVerifyRendered(page: Page, route: RouteCase): Promise<void> {
  await signInForAudience(page, route.audience);
  await page.goto(route.path);
  await page.waitForLoadState('networkidle');
  await expect(page).toHaveURL(new RegExp(route.path.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')));
  await expect(page.getByRole('heading', { name: route.heading }).first()).toBeVisible();
}

test.describe('E-X1: axe — zero serious/critical violations on every HO-ADM-002 route', () => {
  for (const route of ROUTES) {
    test(route.path, async ({ page }) => {
      await gotoAndVerifyRendered(page, route);
      await expectNoSeriousOrCriticalViolations(page);
    });
  }
});

test.describe('E-X2: no horizontal scroll at 360/768/1024/1440', () => {
  for (const route of ROUTES) {
    for (const viewport of VIEWPORTS) {
      test(`${route.path} at ${viewport.width}px`, async ({ page }) => {
        await signInForAudience(page, route.audience);
        await page.setViewportSize(viewport);
        await page.goto(route.path);
        await page.waitForLoadState('networkidle');
        await expect(page.getByRole('heading', { name: route.heading }).first()).toBeVisible();
        const hasHorizontalScroll = await page.evaluate(
          () => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1,
        );
        expect(hasHorizontalScroll, `${route.path} overflows at ${viewport.width}px`).toBe(false);
      });
    }
  }
});

test('E-X1 (with errors shown): submitting the new-branch form empty shows every inline error, still zero serious/critical', async ({
  page,
}) => {
  await signInAsStaff(page);
  await page.goto('/staff/admin/branches/new');
  await page.waitForLoadState('networkidle');
  await page.getByRole('button', { name: /create branch/i }).click();
  await page.getByRole('alert').first().waitFor();
  await expectNoSeriousOrCriticalViolations(page);
});

test('E-X1 (with the policy dialog open): axe over a real portal page, background inert, focus trapped', async ({
  page,
}) => {
  // Self-contained regardless of e2e execution order (this suite runs `--workers=1`, sharing one
  // dev-server process's in-memory mock state across every spec file): publishes a fresh,
  // uniquely-keyed policy requiring acceptance directly, rather than depending on the seeded
  // "terms" policy still being unaccepted by the time this test runs.
  const uniqueKey = `e2e-policy-${Date.now()}`;
  await signInAsStaff(page);
  await page.evaluate(
    async ({ key }) => {
      const apiBaseUrl = (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl ?? '';
      await fetch(`${apiBaseUrl}/api/admin/policies`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ key, title: 'E2E acceptance policy', requiresAcceptance: true }),
      });
      await fetch(`${apiBaseUrl}/api/admin/policies/${key}/versions`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ body: 'Body text.', changeSummary: 'Initial.' }),
      });
    },
    { key: uniqueKey },
  );
  await page.context().clearCookies();

  await signInAsCustomer(page);
  await page.goto('/portal/account/profile');
  await page.waitForLoadState('networkidle');
  const dialog = page.getByRole('dialog');
  await dialog.waitFor();
  await expect(dialog.getByText('E2E acceptance policy')).toBeVisible();
  await expectNoSeriousOrCriticalViolations(page);
});
