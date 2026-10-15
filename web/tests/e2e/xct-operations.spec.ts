import type { Page } from '@playwright/test';
import { accountsFixtureIds } from '../../mocks/handlers/areas/accounts';
import { test, expect, signInAsStaff } from './fixtures';
import { MALFORMED_DRILL_JSON, VALID_DRILL_JSON, xctFixtureIds } from '../../mocks/handlers/operations';

/**
 * HO-XCT-002 section 16 E-C and E-D cases, run against MSW. The same cases are re-run against the
 * real API in `xct-real-api.spec.ts`.
 */
test.describe.configure({ timeout: 90_000 });

interface XctMockWindow {
  __xctMock: { setBackupScenario: (s: string) => void; telemetry: () => Array<Record<string, unknown>> };
  __invalidateQueries: (key: readonly unknown[]) => void;
}

test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

test('E-C1/E-C2 an Administrator erases a customer; the record shows the placeholder and the ledger holds no personal data', async ({
  page,
}) => {
  await page.goto(`/staff/customers/${accountsFixtureIds.customerId}`);
  await expect(page.getByRole('heading', { name: 'Erase personal data' })).toBeVisible({ timeout: 20_000 });
  await expect(page.getByText(/Clinical records for 7 years/)).toBeVisible();
  await page.getByLabel('How was the request made?').selectOption('InPerson');
  await page.getByLabel('How was identity checked?').selectOption('InPersonPhotoId');
  await page.getByRole('button', { name: /Erase personal data…/ }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toContainText('This cannot be undone.');
  await dialog.getByRole('button', { name: 'Erase personal data' }).click();
  await expect(page.getByText('Personal data was erased.')).toBeVisible();

  await page.goto(`/staff/admin/retention/erasures/${xctFixtureIds.erasedCustomerId}`);
  await expect(page.getByText('Held until 12 Mar 2032 — NFR-LEG-5 acceptance evidence')).toBeVisible({ timeout: 30_000 });
  const text = (await page.locator('main').innerText()).replace(/\s+/g, ' ');
  expect(text).not.toMatch(/@|\+88|01[3-9]\d{8}/);
});

test('E-C3/E-C4 the schedule says nothing clinical is deleted; run retention now confirms what it will not do', async ({
  page,
}) => {
  await page.goto('/staff/admin/retention');
  await expect(page.getByRole('heading', { name: 'Data retention' })).toBeVisible({ timeout: 20_000 });
  await expect(page.getByText('Eligible for review. Nothing is deleted automatically.')).toBeVisible();
  await expect(page.getByRole('row', { name: /Clinical records/ })).toContainText('Review only');
  await page.getByRole('button', { name: 'Run retention now' }).click();
  await expect(page.getByRole('dialog')).toContainText('It will not delete clinical, financial, regulatory or audit records');
  await page.getByRole('dialog').getByRole('button', { name: 'Run retention' }).click();
  await expect(page.getByText('Retention run requested').first()).toBeVisible();

  await page.goto(`/staff/admin/retention/runs/${xctFixtureIds.errorRunId}`);
  await expect(page.getByText('Finished with problems')).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText(/could not finish and will be tried again/)).toBeVisible();
  await expect(page.getByText(/ErasureScopeFailing/)).toHaveCount(0);
});

test('E-C5 re-applying three ids shows the four counts', async ({ page }) => {
  await page.goto('/staff/admin/retention/erasures/reapply');
  await page.getByLabel('Customer ids').fill(
    ['00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000003'].join('\n'),
  );
  await page.getByLabel('Restore reference').fill('drill-2026-10');
  await page.getByRole('button', { name: 'Re-apply erasures' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Re-apply erasures' }).click();
  await expect(page.getByText('Erased again')).toBeVisible();
  await expect(page.getByText('Already erased')).toBeVisible();
});

async function setBackup(page: Page, scenario: string) {
  await page.evaluate((s) => (window as unknown as XctMockWindow).__xctMock.setBackupScenario(s), scenario);
  await page.evaluate(() => (window as unknown as XctMockWindow).__invalidateQueries(['admin']));
}

test('E-D1 a stale backup and no restore test are both visible without scrolling, and the overdue banner shows on admin routes', async ({
  page,
}) => {
  await page.goto('/staff/admin/operations/backup');
  await expect(page.getByRole('heading', { name: 'Backup & restore' })).toBeVisible({ timeout: 20_000 });
  await setBackup(page, 'neverTested');
  const overdue = page.getByRole('alert').filter({ hasText: 'Restore test overdue' });
  await expect(overdue).toBeVisible({ timeout: 20_000 });
  await expect(overdue).toBeInViewport();
  await expect(page.getByText(/Backups are stale/)).toBeInViewport();
  await page.goto('/staff/admin/branches');
  await expect(page.getByRole('navigation', { name: 'Administration' })).toBeVisible({ timeout: 30_000 });
  await setBackup(page, 'overdue');
  await expect(page.getByText('The restore test is overdue.')).toBeVisible({ timeout: 20_000 });
  await page.getByRole('button', { name: 'Dismiss for now' }).click();
  await expect(page.getByText('The restore test is overdue.')).toHaveCount(0);
});

test('E-D2 a restore test is recorded from the pasted drill JSON, with the server-computed result', async ({ page }) => {
  await page.goto('/staff/admin/operations/backup');
  await page.getByLabel('Paste the drill JSON').fill(MALFORMED_DRILL_JSON);
  await page.getByRole('button', { name: 'Fill the fields from this' }).click();
  await expect(page.getByRole('alert').filter({ hasText: /isn.t readable JSON/ })).toBeVisible();
  await page.getByLabel('Paste the drill JSON').fill(VALID_DRILL_JSON);
  await page.getByRole('button', { name: 'Fill the fields from this' }).click();
  await expect(page.getByLabel('Backup set label')).toHaveValue('20261010-020000F');
  await page.getByRole('button', { name: 'Record restore test' }).click();
  await expect(page.getByText(/PurrCare worked out/)).toBeVisible();
  await expect(page.getByRole('row', { name: /20261010-020000F/ })).toBeVisible();
});

test('E-B4/E-B5 storefront samples are anonymous, and the performance page gives no verdict below 50 samples', async ({
  page,
}) => {
  await page.goto('/shop');
  // FCP/TTFB arrive a few seconds after a cold dev-server load; flush (pagehide) until they have.
  await expect
    .poll(
      async () => {
        await page.evaluate(() => window.dispatchEvent(new Event('pagehide')));
        return (await page.evaluate(() => (window as unknown as XctMockWindow).__xctMock.telemetry())).length;
      },
      { timeout: 45_000, intervals: [1000] },
    )
    .toBeGreaterThan(0);
  const sent = await page.evaluate(() => (window as unknown as XctMockWindow).__xctMock.telemetry());
  for (const batch of sent) {
    expect(batch.hasCookie).toBe(false);
    expect(batch.hasCsrf).toBe(false);
    const json = JSON.stringify(batch.body);
    expect(json).not.toMatch(/http|\?|userId|session|customer/i);
  }
  await page.goto('/staff/admin/performance');
  await expect(page.getByRole('heading', { name: 'Storefront performance' })).toBeVisible({ timeout: 20_000 });
  await expect(page.getByText('Not enough data')).toBeVisible();
  await expect(page.getByText('Pass', { exact: true }).first()).toBeVisible();
});
