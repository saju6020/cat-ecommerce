import { test, expect } from './fixtures';
import { warmRoutes } from './inventory-helpers';
import { expectAxeClean, expectNoHorizontalScroll, openReportsAs } from './reports-helpers';

/**
 * HO-RPT-002 §16 E-A1, E-A2, E-X1, E-X2 for the home dashboard, against MSW. E-A4 (a refresh past
 * the interval with a dialog open) is a real-API case in `reports-real-api.spec.ts`; the MSW
 * dashboard's refresh interval is its 60 s default.
 */
test.describe.configure({ mode: 'serial', timeout: 120_000 });

test.beforeAll(async ({ request }) => {
  await warmRoutes(request, ['/staff', '/staff/reports']);
});

test('E-A1: staff land on /staff and see their tiles for the active branch', async ({ page }) => {
  await openReportsAs(page, 'Reception', '/');
  await expect(page).toHaveURL(/\/staff$/);
  await expect(
    page.getByRole('heading', { level: 1, name: /^Today — .* · Main clinic$/ }),
  ).toBeVisible();
  await expect(page.getByRole('heading', { level: 2, name: 'Appointments today' })).toBeVisible();
  await expect(page.getByText(/Updated \d\d:\d\d \(Asia\/Dhaka\)/)).toBeVisible();
  await expect(page.getByRole('link', { name: /View report/ }).first()).toBeVisible();
});

test('E-A2: an accountant sees money tiles; a nurse sees none and gets no Reports page', async ({
  page,
}) => {
  await openReportsAs(page, 'Accountant', '/staff');
  await expect(page.getByRole('heading', { level: 2, name: 'Payments today' })).toBeVisible();

  await openReportsAs(page, 'Nurse', '/staff');
  await expect(page.getByRole('heading', { level: 2, name: 'Appointments today' })).toBeVisible();
  await expect(page.getByText(/payments|receivables|revenue/i)).toHaveCount(0);
  // The Reports entry is hidden from a Nurse (owner 2026-10-14); a direct URL is still refused.
  await expect(page.getByRole('link', { name: 'Reports' })).toHaveCount(0);
  await openReportsAs(page, 'Nurse', '/staff/reports');
  await expect(page.getByText(/have permission/i)).toBeVisible();
});

test('E-X1/E-X2: the dashboard is axe-clean and never scrolls sideways', async ({ page }) => {
  await openReportsAs(page, 'Administrator', '/staff');
  await expect(page.getByRole('heading', { level: 2 }).first()).toBeVisible();
  await expectAxeClean(page);
  for (const width of [360, 768, 1024, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await expectNoHorizontalScroll(page);
  }
});
