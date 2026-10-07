import AxeBuilder from '@axe-core/playwright';
import type { Page } from '@playwright/test';
import { test, expect, signInAsCustomer, signInAsStaff } from './fixtures';
import { appointmentsFixtureIds as ids } from '../../mocks/handlers/appointments';
import { accountsFixtureIds } from '../../mocks/handlers/areas/accounts';
import { openDay } from './appointments-helpers';

/**
 * HO-APT-002 §16 E-X1 (axe) and E-X2 (no horizontal scroll / clipped control at 360, 768, 1024,
 * 1440), against MSW, for every route §8.1 adds — plus the three states §16 names: the hold
 * countdown, a move in progress (the Move… dialog, the pointer-free path of a drag) and the
 * calendar with an error showing. One audience per test (fixtures.ts).
 */

interface RouteCase {
  path: string;
  audience: 'customer' | 'staff';
}

const BOOK = `catId=20000000-0000-0000-0000-000000000001&serviceId=${ids.consultationServiceId}&branchId=${ids.mainBranchId}&date=${openDay()}`;

const ROUTES: RouteCase[] = [
  { path: `/book?${BOOK}`, audience: 'customer' },
  { path: '/book', audience: 'customer' },
  { path: '/portal/appointments', audience: 'customer' },
  { path: `/portal/appointments/${ids.changeableAppointmentId}`, audience: 'customer' },
  { path: `/portal/appointments/${ids.lockedAppointmentId}`, audience: 'customer' },
  { path: `/portal/appointments/${ids.offerAppointmentId}`, audience: 'customer' },
  { path: '/staff/front-desk', audience: 'staff' },
  { path: '/staff/front-desk?tab=queue', audience: 'staff' },
  { path: `/staff/appointments?date=${openDay(1)}`, audience: 'staff' },
  { path: `/staff/appointments?date=${openDay(1)}&range=week`, audience: 'staff' },
  { path: '/staff/appointments/new', audience: 'staff' },
  { path: `/staff/appointments/${ids.flaggedAppointmentId}`, audience: 'staff' },
  { path: '/staff/appointments/flagged', audience: 'staff' },
  { path: `/staff/customers/${accountsFixtureIds.customerId}`, audience: 'staff' },
];

// Dev-server routes compile on first visit; give each case room for that.
test.describe.configure({ timeout: 60_000 });

const VIEWPORTS = [
  { width: 360, height: 780 },
  { width: 768, height: 1024 },
  { width: 1024, height: 768 },
  { width: 1440, height: 900 },
];

async function signIn(page: Page, audience: RouteCase['audience']) {
  if (audience === 'customer') await signInAsCustomer(page);
  else await signInAsStaff(page);
}

async function expectNoSeriousOrCriticalViolations(page: Page): Promise<void> {
  const results = await new AxeBuilder({ page }).analyze();
  const serious = results.violations.filter(
    (v) => v.impact === 'serious' || v.impact === 'critical',
  );
  expect(serious, JSON.stringify(serious, null, 2)).toEqual([]);
}

test.describe('E-X1: axe — zero serious/critical violations', () => {
  for (const { path, audience } of ROUTES) {
    test(path, async ({ page }) => {
      await signIn(page, audience);
      await page.goto(path);
      await page.waitForLoadState('networkidle');
      await expectNoSeriousOrCriticalViolations(page);
    });
  }

  test('E-X1 (countdown): the confirm step with the hold timer running', async ({ page }) => {
    await signInAsCustomer(page);
    await page.goto(`/book?${BOOK}`);
    await page
      .getByRole('group', { name: /times on/i })
      .getByRole('button', { name: /^11:00/ })
      .click();
    await expect(page.getByRole('timer')).toBeVisible();
    await expectNoSeriousOrCriticalViolations(page);
  });

  test('E-X1 (move in progress): the Move… dialog open on the review step', async ({ page }) => {
    await signInAsStaff(page);
    await page.goto(`/staff/appointments?date=${openDay(1)}`);
    await page
      .getByRole('button', { name: /move tiger/i })
      .first()
      .click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel(/^time/i).fill('10:30');
    await dialog.getByRole('button', { name: /review move/i }).click();
    await expect(dialog.getByRole('button', { name: /move appointment/i })).toBeVisible();
    await expectNoSeriousOrCriticalViolations(page);
  });

  test('E-X1 (calendar with errors): a refused move showing its reason', async ({ page }) => {
    await signInAsStaff(page);
    await page.goto(`/staff/appointments?date=${openDay(1)}`);
    await page
      .getByRole('button', { name: /move tiger/i })
      .first()
      .click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel('Vet or nurse').selectOption({ label: 'Dr Akter' });
    await dialog.getByLabel(/^time/i).fill('11:30');
    await dialog.getByRole('button', { name: /review move/i }).click();
    await dialog.getByRole('button', { name: /move appointment/i }).click();
    await expect(
      dialog.getByRole('alert').filter({ hasText: /already has an appointment/i }),
    ).toBeVisible();
    await expectNoSeriousOrCriticalViolations(page);
  });
});

test.describe('E-X2: no horizontal scroll, no clipped control at 360/768/1024/1440', () => {
  for (const { path, audience } of ROUTES) {
    for (const viewport of VIEWPORTS) {
      test(`${path} at ${viewport.width}px`, async ({ page }) => {
        await signIn(page, audience);
        await page.setViewportSize(viewport);
        await page.goto(path);
        await page.waitForLoadState('networkidle');
        // The week/day grid scrolls INSIDE its own container on a wide screen (it is the only place
        // a wide table may); the page itself must never scroll sideways, and below 768 px the grid
        // is replaced by the agenda list.
        const overflow = await page.evaluate(
          () => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1,
        );
        expect(overflow, `${path} overflows at ${viewport.width}px`).toBe(false);
        if (viewport.width < 768 && path.startsWith('/staff/appointments?')) {
          await expect(page.locator('[data-calendar-column]').first()).toBeHidden();
        }
        // No visible button or link is wider than the viewport (a clipped control).
        const clipped = await page.evaluate((width) => {
          return [...document.querySelectorAll('button, a[href], input, select, textarea')]
            .filter((el) => {
              const rect = el.getBoundingClientRect();
              return rect.width > 0 && (rect.right > width + 1 || rect.left < -1);
            })
            .filter((el) => !el.closest('[data-calendar-column], .overflow-x-auto'))
            .map((el) => el.outerHTML.slice(0, 80));
        }, viewport.width);
        expect(clipped, `${path} has clipped controls at ${viewport.width}px`).toEqual([]);
      });
    }
  }
});
