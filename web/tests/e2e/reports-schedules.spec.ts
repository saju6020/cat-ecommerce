import { test, expect } from './fixtures';
import { warmRoutes } from './inventory-helpers';
import { expectAxeClean, expectNoHorizontalScroll, openReportsAs } from './reports-helpers';

/**
 * HO-RPT-002 §16 E-D1, E-D3, E-D4, E-X1, E-X2 against MSW. E-D2 (run now against the real API,
 * the capturing sender) is in `reports-real-api.spec.ts`.
 */
test.describe.configure({ mode: 'serial', timeout: 120_000 });

test.beforeAll(async ({ request }) => {
  await warmRoutes(request, [
    '/staff/admin/report-schedules',
    '/staff/admin/report-schedules/new',
    '/staff/admin/report-schedules/sc000000-0000-0000-0000-000000000001',
  ]);
});

const LIST = '/staff/admin/report-schedules';

test('E-D1: create a schedule with two recipients, see the server preview, find it in the list', async ({
  page,
}) => {
  await openReportsAs(page, 'Administrator', `${LIST}/new?reportKey=revenue-by-business-line`);
  await expect(page.getByRole('heading', { level: 1, name: 'New report schedule' })).toBeVisible();
  await page.getByLabel('Name').fill('Weekly revenue');
  await page.getByLabel('Period each run covers').selectOption('PreviousMonth');
  await expect(page.getByText(/The next one covers/)).toBeVisible();
  await expect(page.getByText(/Thu 15 Oct 07:00/)).toBeVisible();
  await expect(page.getByLabel(/Sabina Yasmin/)).toBeDisabled();
  await page.getByLabel(/Rezwan Ahmed/).check();
  await page.getByLabel(/Mahmud Hasan/).check();
  await page.getByRole('button', { name: 'Create schedule' }).click();
  await expect(page).toHaveURL(/report-schedules\/[\w-]+$/, { timeout: 30_000 });
  await expect(page.getByRole('heading', { level: 1, name: 'Weekly revenue' })).toBeVisible();
  // A client-side navigation: a full reload would reset the in-browser mock's schedules.
  await page
    .locator('#main-content')
    .getByRole('link', { name: 'Report schedules' })
    .first()
    .click();
  await expect(page.getByRole('link', { name: 'Weekly revenue' })).toBeVisible();
});

test('E-D3: pause, resume and cancel confirm in words and keep past runs', async ({ page }) => {
  await openReportsAs(page, 'Administrator', `${LIST}/sc000000-0000-0000-0000-000000000001`);
  await expect(page.getByRole('heading', { level: 2, name: 'Run history' })).toBeVisible();
  await expect(page.getByText('Too large to attach — link sent')).toBeVisible();

  await page.getByRole('button', { name: /Pause/ }).click();
  await expect(page.getByRole('dialog')).toContainText('Past runs are kept.');
  await page.getByRole('dialog').getByRole('button', { name: 'Pause' }).click();
  await expect(page.getByText('Paused').first()).toBeVisible();

  await page.getByRole('button', { name: /Resume/ }).click();
  await expect(page.getByText('Active').first()).toBeVisible();

  await page.getByRole('button', { name: /Run now/ }).click();
  await expect(page.getByRole('dialog')).toContainText('emails it to 2 people now');
  await page.getByRole('dialog').getByRole('button', { name: 'Run now' }).click();
  await expect(page.getByText('Generated — 412 rows', { exact: true })).toBeVisible();

  await page.getByRole('button', { name: /Cancel schedule/ }).click();
  await expect(page.getByRole('dialog')).toContainText('will stop being emailed to 2 people');
  await page.getByRole('dialog').getByRole('button', { name: 'Cancel schedule' }).click();
  await expect(page.getByText(/was cancelled and cannot be changed/)).toBeVisible();
  await expect(page.getByRole('heading', { level: 2, name: 'Run history' })).toBeVisible();
});

test('E-D4: a shop manager who opens the schedules directly is refused', async ({ page }) => {
  await openReportsAs(page, 'ShopManager', LIST);
  await expect(page.getByText(/have permission/i)).toBeVisible();
});

test('E-X1/E-X2: the schedule list and form are axe-clean and never scroll sideways', async ({
  page,
}) => {
  await openReportsAs(page, 'Administrator', LIST);
  await expect(page.getByRole('heading', { level: 1, name: 'Report schedules' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Monthly revenue by line' })).toBeVisible();
  await expectAxeClean(page);
  for (const width of [360, 768, 1024, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await expectNoHorizontalScroll(page);
  }
  await page.goto(`${LIST}/new?reportKey=revenue-by-business-line`);
  await expect(page.getByLabel(/Rezwan Ahmed/)).toBeVisible();
  await expectAxeClean(page);
  await page.setViewportSize({ width: 360, height: 900 });
  await expectNoHorizontalScroll(page);
});
