import { readFileSync } from 'node:fs';
import { test, expect } from './fixtures';
import { warmRoutes } from './inventory-helpers';
import { expectAxeClean, expectNoHorizontalScroll, openReportsAs } from './reports-helpers';

/**
 * HO-RPT-002 §16 E-B1, E-B2, E-B3, E-B4, E-C2, E-C3, E-X1, E-X2 against MSW. The mock keys its edge
 * cases on the requested range (an empty day, a timeout, an over-large PDF), as the real server
 * would on its data. The real-API column lives in `reports-real-api.spec.ts`.
 */
const reportBody = (page: import('@playwright/test').Page) =>
  page
    .getByRole('table')
    .or(page.getByRole('list', { name: / · / }))
    .first();

test.describe.configure({ mode: 'serial', timeout: 120_000 });

test.beforeAll(async ({ request }) => {
  await warmRoutes(request, [
    '/staff/reports',
    '/staff/reports/clinic-daily-schedule',
    '/staff/reports/revenue-by-business-line',
  ]);
});

const DAILY = '/staff/reports/clinic-daily-schedule';

test('E-B1: the catalogue lists reports by category and opens the generic viewer', async ({
  page,
}) => {
  await openReportsAs(page, 'Administrator', '/staff/reports');
  await expect(page.getByRole('heading', { level: 2, name: 'Clinic operations' })).toBeVisible();
  await page.getByRole('link', { name: /Daily schedule/ }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Daily schedule' })).toBeVisible();
  const body = page
    .getByRole('table', {
      name: /Daily schedule · 14 Oct 2026 to 14 Oct 2026 · Main clinic · times in Asia\/Dhaka/,
    })
    .or(
      page.getByRole('list', {
        name: /Daily schedule · 14 Oct 2026 to 14 Oct 2026 · Main clinic · times in Asia\/Dhaka/,
      }),
    );
  await expect(body).toBeVisible();
  await expect(page.getByText('Only confirmed bookings and walk-ins are listed.')).toBeVisible();
});

test('E-B2: a range over the limit is refused on the field; a valid one shows results', async ({
  page,
}) => {
  await openReportsAs(page, 'Administrator', DAILY);
  await page.getByLabel(/^From/).fill('2026-02-01');
  await page.getByLabel(/^To/).fill('2026-12-31');
  await page.getByRole('button', { name: 'Apply' }).click();
  await expect(page.getByText(/covers at most 31 days/)).toBeVisible();
  await expect(page).toHaveURL(/to=2026-12-31/);
  await page.getByLabel(/^To/).fill('2026-02-20');
  await page.getByRole('button', { name: 'Apply' }).click();
  await expect(page.getByText(/covers at most 31 days/)).toHaveCount(0);
  await expect(reportBody(page)).toBeVisible();
});

test('E-B3: CSV and PDF both download, non-empty, and a too-large PDF explains itself', async ({
  page,
}) => {
  await openReportsAs(page, 'Administrator', DAILY);
  await expect(reportBody(page)).toBeVisible();
  for (const [button, extension] of [
    ['Export CSV', 'csv'],
    ['Export PDF', 'pdf'],
  ] as const) {
    const [download] = await Promise.all([
      page.waitForEvent('download'),
      page.getByRole('button', { name: button }).click(),
    ]);
    expect(download.suggestedFilename()).toMatch(new RegExp(`\\.${extension}$`));
    const path = await download.path();
    expect(readFileSync(path).length).toBeGreaterThan(0);
  }
  await page.goto(`${DAILY}?from=2021-01-01&to=2021-01-02`);
  await page.getByRole('button', { name: 'Export PDF' }).click();
  await expect(
    page.getByRole('alert').filter({ hasText: /Too many rows for a PDF/ }),
  ).toBeVisible();
  await expect(page.getByRole('button', { name: 'Export PDF' })).toBeEnabled();
});

test('E-B4: a filtered report URL opens the same report in a second context', async ({
  page,
  browser,
}) => {
  await openReportsAs(page, 'Administrator', `${DAILY}?from=2026-10-10&to=2026-10-12&sort=-cat`);
  await expect(page.getByLabel(/^From/)).toHaveValue('2026-10-10');
  const url = page.url();
  const context = await browser.newContext();
  const other = await context.newPage();
  await openReportsAs(other, 'Administrator', new URL(url).pathname + new URL(url).search);
  await expect(other.getByLabel(/^From/)).toHaveValue('2026-10-10');
  await expect(other.getByLabel(/^To/)).toHaveValue('2026-10-12');
  await expect(
    other.getByLabel('Sort by').or(other.getByRole('columnheader', { name: /^Cat/ })),
  ).toBeVisible();
  await expect(other).toHaveURL(/sort=-cat/);
  await context.close();
});

test('E-C2: margin columns are visible to the accountant only', async ({ page }) => {
  const path = '/staff/reports/revenue-shop-by-category';
  await openReportsAs(page, 'Accountant', path);
  await expect(page.getByText('Margin %', { exact: true }).first()).toBeVisible();
  await expect(page.getByText(/Margin covers 83.0 %/)).toBeVisible();
  await openReportsAs(page, 'ShopManager', path);
  await expect(reportBody(page)).toBeVisible();
  await expect(page.getByText(/Margin/)).toHaveCount(0);
  await expect(page.getByText(/Margin covers/)).toHaveCount(0);
});

test('E-C3: the payment reconciliation flags a variance in words', async ({ page }) => {
  await openReportsAs(page, 'Accountant', '/staff/reports/payment-reconciliation');
  await expect(reportBody(page)).toBeVisible();
  for (const word of ['Short', 'Over', 'Matched']) {
    await expect(page.getByText(word, { exact: true }).first()).toBeVisible();
  }
  await expect(page.getByText(/Card in store shows what was expected only/)).toBeVisible();
});

test('E-X1/E-X2: the catalogue and a 12-column report are axe-clean, with the chart open, and never scroll the page sideways', async ({
  page,
}) => {
  await openReportsAs(page, 'Accountant', '/staff/reports');
  await expect(page.getByRole('heading', { level: 2 }).first()).toBeVisible();
  await expectAxeClean(page);

  await page.goto('/staff/reports/revenue-by-business-line');
  await expect(
    page.getByRole('table').or(page.getByRole('list', { name: /Revenue by business line/ })),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Show chart' }).click();
  await expect(page.getByTestId('chart-data-table')).toBeAttached();
  await expectAxeClean(page);
  for (const width of [360, 768, 1024, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await expectNoHorizontalScroll(page);
  }
  await page.setViewportSize({ width: 360, height: 900 });
  await expect(page.getByRole('list', { name: /Revenue by business line/ })).toBeVisible();
  await expect(page.locator('#main-content table:not(.sr-only)')).toHaveCount(0);
});
