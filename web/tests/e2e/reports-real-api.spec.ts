import fs from 'node:fs';
import { readFileSync } from 'node:fs';
// Plain Playwright `test`, not ./fixtures: that wrapper waits for MSW's service worker, which never
// exists when NEXT_PUBLIC_USE_MOCKS=false (the whole point of this spec).
import { test, expect, type Page } from '@playwright/test';
import { expectAxeClean, expectNoHorizontalScroll } from './reports-helpers';

/**
 * HO-RPT-002 §16 — the E-cases re-run against the REAL API (HO-RPT-001 on master). Manual: needs
 * the API on :5114 on the throwaway database `catecommerce_rpt002e2e` (with
 * `Cors__AllowedOrigins__0` = this app's origin), the web app on :3114 started with
 * `NEXT_PUBLIC_USE_MOCKS=false NEXT_PUBLIC_API_BASE_URL=http://localhost:5114`, and `REPORTS_SEED`
 * pointing at the JSON a throwaway seeder wrote (one branch; an administrator, two accountants, a
 * shop manager, a reception and a nurse account, each with a mobile + password; the accountants
 * have an email address). Skipped otherwise. Serial: the cases share one branch and one schedule.
 */
const SEED_PATH = process.env.REPORTS_SEED;
const enabled = process.env.NEXT_PUBLIC_USE_MOCKS === 'false' && !!SEED_PATH;
const seed = enabled
  ? (JSON.parse(fs.readFileSync(SEED_PATH!, 'utf8')) as {
      branchId: string;
      people: Record<string, { mobile: string; password: string; name?: string }>;
    })
  : null;

// A cold `next dev` compiles each route on its first visit, which can outlast the default 5 s.
const slow = expect.configure({ timeout: 30_000 });

test.describe.configure({ mode: 'serial', timeout: 180_000 });
test.skip(!enabled, 'real-API run only (NEXT_PUBLIC_USE_MOCKS=false, REPORTS_SEED=<seed.json>)');

async function signInAs(page: Page, person: string): Promise<void> {
  await page.context().clearCookies();
  await page.goto('/staff/sign-in');
  await page.waitForFunction(
    () => (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl !== undefined,
  );
  const { mobile, password } = seed!.people[person]!;
  const status = await page.evaluate(
    async ({ mobile, password }) => {
      const base = (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl ?? '';
      const response = await fetch(`${base}/api/staff/auth/sign-in`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ identifier: mobile, password }),
      });
      return response.status;
    },
    { mobile, password },
  );
  slow(status).toBe(200);
}

async function open(page: Page, person: string, path: string): Promise<void> {
  await signInAs(page, person);
  await page.goto(path);
  await slow(page.locator('#main-content')).toBeVisible({ timeout: 60_000 });
}

test('E-A1/E-A2 real: reception lands on a dashboard; an accountant sees money; a nurse has no Reports', async ({
  page,
}) => {
  await open(page, 'Reception', '/');
  await slow(page).toHaveURL(/\/staff$/);
  await slow(page.getByRole('heading', { level: 1, name: /^Today — / })).toBeVisible();
  await slow(page.getByText(/Updated \d\d:\d\d/)).toBeVisible();
  await slow(page.getByRole('heading', { level: 2 }).first()).toBeVisible();

  await open(page, 'Accountant', '/staff');
  await slow(page.getByRole('heading', { level: 2, name: 'Payments today' })).toBeVisible();
  await slow(page.getByRole('heading', { level: 2, name: 'Receivables' })).toBeVisible();

  await open(page, 'Nurse', '/staff/reports');
  await slow(page.getByText(/have permission/i)).toBeVisible();
});

test('E-A4 real: the dashboard refreshes by itself past the interval (the dialog case is U-A5)', async ({
  page,
}) => {
  await open(page, 'Administrator', '/staff');
  const updated = page.getByText(/Updated \d\d:\d\d/);
  await slow(updated).toBeVisible();
  const before = await updated.textContent();
  // The server default is 60 s (Reporting:DashboardRefreshSeconds); wait one interval and a bit.
  await page.waitForTimeout(65_000);
  await slow(updated).not.toHaveText(before ?? '');
});

test('E-B real: the catalogue, a report, a range limit, and CSV + PDF downloads', async ({
  page,
}) => {
  await open(page, 'Administrator', '/staff/reports');
  await slow(page.getByRole('heading', { level: 2, name: 'Clinic operations' })).toBeVisible();
  const link = page.getByRole('link', { name: /Daily clinic schedule/ });
  await link.click();
  await slow(page.getByRole('heading', { level: 1, name: 'Daily clinic schedule' })).toBeVisible();
  await slow(page.getByLabel(/^From/)).toHaveValue(/^\d{4}-\d\d-\d\d$/);
  const body = page
    .getByRole('table')
    .or(page.getByRole('list', { name: / · / }))
    .or(page.getByText(/^No results for Daily clinic schedule/));
  await slow(body.first()).toBeVisible({ timeout: 30_000 });

  await page.getByLabel(/^From/).fill('2020-01-01');
  await page.getByLabel(/^To/).fill('2026-12-31');
  await page.getByRole('button', { name: 'Apply' }).click();
  await slow(page.getByText(/covers at most \d+ days/)).toBeVisible();

  await page.getByRole('button', { name: 'Reset' }).click();
  await slow(body.first()).toBeVisible({ timeout: 30_000 });
  for (const [button, extension] of [
    ['Export CSV', 'csv'],
    ['Export PDF', 'pdf'],
  ] as const) {
    const [download] = await Promise.all([
      page.waitForEvent('download'),
      page.getByRole('button', { name: button }).click(),
    ]);
    slow(download.suggestedFilename()).toMatch(new RegExp(`\\.${extension}$`));
    slow(readFileSync((await download.path())!).length).toBeGreaterThan(0);
  }
});

test('E-C real: stock, revenue and growth reports open; margin is for the accountant only', async ({
  page,
}) => {
  for (const key of ['stock-position', 'stock-near-expiry', 'stock-low-reorder']) {
    await open(page, 'Administrator', `/staff/reports/${key}`);
    await slow(page.getByRole('heading', { level: 1 })).toBeVisible();
    await slow(page.locator('#main-content').getByRole('alert')).toHaveCount(0);
  }
  await open(page, 'Accountant', '/staff/reports/revenue-by-business-line');
  await slow(
    page.getByRole('heading', { level: 1, name: 'Revenue by business line' }),
  ).toBeVisible();
  await slow(page.getByText(/Margin/).first()).toBeVisible({ timeout: 30_000 });
  await open(page, 'ShopManager', '/staff/reports/revenue-by-business-line');
  await slow(page.getByText(/have permission/i)).toBeVisible();
  await open(page, 'Accountant', '/staff/reports/payment-reconciliation');
  await slow(page.getByRole('heading', { level: 1, name: /Payment reconciliation/ })).toBeVisible();
  await open(page, 'Administrator', '/staff/reports/customers-new-vs-returning');
  await slow(page.getByRole('heading', { level: 1 })).toBeVisible();
});

test('E-D real: create a schedule, run it now, see the run and its deliveries, then pause, resume and cancel', async ({
  page,
}) => {
  await open(
    page,
    'Administrator',
    '/staff/admin/report-schedules/new?reportKey=revenue-by-business-line',
  );
  await page.getByLabel('Name').fill('E2E revenue');
  await slow(page.getByText(/The next one covers/)).toBeVisible({ timeout: 30_000 });
  await page.getByLabel(/Aisha Accountant/).check();
  await page.getByLabel(/Mahmud Accountant/).check();
  await page.getByRole('button', { name: 'Create schedule' }).click();
  await slow(page.getByRole('heading', { level: 1, name: 'E2E revenue' })).toBeVisible({
    timeout: 60_000,
  });

  await page.getByRole('button', { name: /Run now/ }).click();
  await slow(page.getByRole('dialog')).toContainText('emails it to 2 people now');
  await page.getByRole('dialog').getByRole('button', { name: 'Run now' }).click();
  await slow(page.getByText(/^Generated — \d+ rows$/).first()).toBeVisible({ timeout: 90_000 });
  await slow(page.getByRole('heading', { level: 2, name: 'Run history' })).toBeVisible();
  await slow(page.getByText('Generated', { exact: true }).first()).toBeVisible();

  await page.getByRole('button', { name: /Pause/ }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Pause' }).click();
  await slow(page.getByText('Paused').first()).toBeVisible();
  await page.getByRole('button', { name: /Resume/ }).click();
  await slow(page.getByText('Active').first()).toBeVisible();
  await page.getByRole('button', { name: /Cancel schedule/ }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Cancel schedule' }).click();
  await slow(page.getByText(/was cancelled and cannot be changed/)).toBeVisible();
  await slow(page.getByRole('heading', { level: 2, name: 'Run history' })).toBeVisible();
});

test('E-D4 real: a shop manager who opens the schedules directly is refused', async ({ page }) => {
  await open(page, 'ShopManager', '/staff/admin/report-schedules');
  await slow(page.getByText(/have permission/i)).toBeVisible();
});

test('E-X real: the real pages are axe-clean and never scroll sideways', async ({ page }) => {
  let signedInAs = '';
  for (const [person, path] of [
    ['Administrator', '/staff'],
    ['Administrator', '/staff/admin/report-schedules'],
    ['Administrator', '/staff/reports'],
    ['Accountant', '/staff/reports/revenue-by-business-line'],
    ['Accountant', '/staff/reports/payment-reconciliation'],
  ] as const) {
    // Sign in once per person: the sign-in endpoint is rate limited.
    if (person !== signedInAs) {
      await signInAs(page, person);
      signedInAs = person;
    }
    await page.goto(path);
    await slow(page.locator('#main-content')).toBeVisible({ timeout: 60_000 });
    await slow(page.getByRole('heading').first()).toBeVisible();
    await page.waitForTimeout(1500);
    await expectAxeClean(page);
    for (const width of [360, 768, 1024, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      await expectNoHorizontalScroll(page);
    }
  }
});
