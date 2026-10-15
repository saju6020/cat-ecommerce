import AxeBuilder from '@axe-core/playwright';
import type { Page } from '@playwright/test';
import { test, expect } from './fixtures';
import { IDS, inDays, signInStaffEstablished } from './boarding-helpers';
import { signInEstablished } from './portal-helpers';

/**
 * HO-BRD-002 §16 E-X1 (axe: zero serious/critical) and E-X2 (no horizontal scroll, 360 / 768 / 1024 /
 * 1440) for the routes §8.1 adds, against MSW. One audience per test.
 */

interface Route {
  path: string;
  audience: 'customer' | 'staff';
}

const ROUTES: Route[] = [
  {
    path: `/boarding?arrivalOn=${inDays(70)}&departureOn=${inDays(90)}&catCount=1`,
    audience: 'customer',
  },
  {
    path: `/boarding?arrivalOn=${inDays(1)}&departureOn=${inDays(3)}&catCount=3`,
    audience: 'customer',
  },
  { path: `/boarding?stayId=${IDS.stayEnquiryBlocked}`, audience: 'customer' },
  { path: '/boarding/daycare', audience: 'customer' },
  { path: '/portal/boarding', audience: 'customer' },
  { path: `/portal/boarding/${IDS.stayReservedSoon}`, audience: 'customer' },
  { path: `/portal/boarding/${IDS.stayInHouseFamily}`, audience: 'customer' },
  { path: `/portal/boarding/${IDS.stayInHouseFamily}/diary`, audience: 'customer' },
  {
    path: `/portal/boarding/${IDS.stayInHouseFamily}/incidents/${IDS.incidentMedium}`,
    audience: 'customer',
  },
  {
    path: `/portal/boarding/${IDS.stayInHouseFamily}/approvals/${IDS.approvalPending}`,
    audience: 'customer',
  },
  { path: `/portal/boarding/${IDS.stayCheckedOut}/summary`, audience: 'customer' },
  { path: '/staff/boarding', audience: 'staff' },
  { path: '/staff/boarding/board', audience: 'staff' },
  { path: '/staff/boarding/stays', audience: 'staff' },
  { path: '/staff/boarding/stays/new', audience: 'staff' },
  { path: `/staff/boarding/stays/${IDS.stayInHouseFamily}`, audience: 'staff' },
  { path: `/staff/boarding/stays/${IDS.stayInHouseFamily}?tab=isolation`, audience: 'staff' },
  { path: `/staff/boarding/stays/${IDS.stayReservedSoon}`, audience: 'staff' },
  { path: '/staff/boarding/tasks', audience: 'staff' },
  { path: '/staff/boarding/incidents', audience: 'staff' },
  { path: '/staff/boarding/daycare', audience: 'staff' },
  { path: '/staff/admin/boarding/units', audience: 'staff' },
  { path: '/staff/admin/boarding/rate-cards', audience: 'staff' },
  { path: '/staff/admin/boarding/care-routine', audience: 'staff' },
];

test.describe.configure({ timeout: 90_000 });

const VIEWPORTS = [
  { width: 360, height: 780 },
  { width: 768, height: 1024 },
  { width: 1024, height: 768 },
  { width: 1440, height: 900 },
];

async function signIn(page: Page, audience: Route['audience']) {
  if (audience === 'customer') await signInEstablished(page);
  else await signInStaffEstablished(page);
}

async function noSerious(page: Page) {
  const results = await new AxeBuilder({ page }).analyze();
  const serious = results.violations.filter(
    (v) => v.impact === 'serious' || v.impact === 'critical',
  );
  expect(
    serious,
    JSON.stringify(
      serious.map((v) => ({ id: v.id, nodes: v.nodes.slice(0, 2).map((n) => n.html) })),
      null,
      2,
    ),
  ).toEqual([]);
}

test.describe('E-X1: axe — zero serious/critical violations', () => {
  for (const { path, audience } of ROUTES) {
    test(path, async ({ page }) => {
      await signIn(page, audience);
      await page.goto(path);
      await page.waitForLoadState('networkidle');
      await noSerious(page);
    });
  }

  test('the signature step, both paths', async ({ page }) => {
    await signIn(page, 'staff');
    await page.goto(`/staff/boarding/stays/${IDS.stayArrivingToday}`);
    await page.getByRole('button', { name: 'Check in' }).click();
    await noSerious(page);
    await page.getByLabel(/type your name instead/i).check();
    await noSerious(page);
  });

  test('the task board mid-completion and with the observation sheet open', async ({ page }) => {
    await signIn(page, 'staff');
    await page.goto('/staff/boarding/tasks');
    await page
      .locator('li', { hasText: 'Mid-morning check' })
      .getByRole('button', { name: /^Done/ })
      .click();
    await noSerious(page);
    await page
      .locator('li', { hasText: 'Give Metacam' })
      .getByRole('button', { name: 'Observe' })
      .click();
    await noSerious(page);
  });
});

test.describe('E-X2: no horizontal scroll at 360 / 768 / 1024 / 1440', () => {
  const sample = ROUTES.filter(
    (r) =>
      [
        '/staff/boarding/tasks',
        '/staff/boarding/board',
        '/staff/boarding',
        `/portal/boarding/${IDS.stayInHouseFamily}/diary`,
      ].includes(r.path) || r.path.startsWith('/boarding?arrivalOn'),
  );
  for (const { path, audience } of sample) {
    test(path, async ({ page }) => {
      await signIn(page, audience);
      for (const viewport of VIEWPORTS) {
        await page.setViewportSize(viewport);
        await page.goto(path);
        await page.waitForLoadState('networkidle');
        const overflow = await page.evaluate(() => {
          const el = document.scrollingElement!;
          return el.scrollWidth - el.clientWidth;
        });
        expect(overflow, `${path} at ${viewport.width}px`).toBeLessThanOrEqual(1);
      }
    });
  }
});
