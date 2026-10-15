import AxeBuilder from '@axe-core/playwright';
import type { Page } from '@playwright/test';
import { test, expect, signInAsCustomer, signInAsStaff } from './fixtures';
import { billingIds as ids } from '../../mocks/handlers/billing';
import { todayInZone } from '../../lib/format/time';

/**
 * HO-BIL-002 §16 E-X1 (axe: zero serious/critical) and E-X2 (no horizontal scroll at 360, 768, 1024,
 * 1440) for every route of §8.1, plus the dialogs, the print view and the match view. Against MSW.
 */
test.describe.configure({ timeout: 120_000 });

const C = ids.customerId;
const STAFF = [
  '/staff/billing',
  `/staff/billing/customers/${C}`,
  `/staff/billing/customers/${C}?tab=invoices`,
  `/staff/billing/customers/${C}?tab=payments`,
  '/staff/billing/invoices',
  `/staff/billing/invoices/new?customerId=${C}&charges=${ids.chargeConsultId}`,
  `/staff/billing/invoices/${ids.draftInvoiceId}`,
  `/staff/billing/invoices/${ids.partPaidInvoiceId}`,
  `/staff/billing/invoices/${ids.correctedInvoiceId}`,
  `/staff/billing/invoices/${ids.paidInvoiceId}/credit`,
  `/staff/billing/credit-notes/${ids.creditNoteId}`,
  '/staff/billing/payments',
  `/staff/billing/payments/new?customerId=${C}&invoiceId=${ids.issuedInvoiceId}`,
  `/staff/billing/payments/${ids.cashPaymentId}`,
  `/staff/billing/payments/${ids.cashPaymentId}/receipt`,
  `/staff/billing/payments/${ids.cashPaymentId}/receipt?width=80`,
  '/staff/billing/approvals',
  '/staff/billing/receivables',
  `/staff/billing/cash-up?date=${todayInZone('Asia/Dhaka')}`,
  '/staff/billing/reconciliation/cod',
  '/staff/billing/reconciliation/bank',
  `/staff/billing/reconciliation/bank/${ids.statementId}`,
  '/staff/billing/exports',
  '/staff/admin/billing-profile',
  `/staff/customers/${C}`,
];
const PORTAL = [
  '/portal/billing',
  `/portal/billing/invoices/${ids.issuedInvoiceId}`,
  `/portal/billing/invoices/${ids.paidInvoiceId}`,
  `/portal/billing/payments/${ids.cashPaymentId}`,
  `/portal/billing/invoices/${ids.otherCustomerInvoiceId}`,
];
const VIEWPORTS = [360, 768, 1024, 1440];

async function noSeriousViolations(page: Page) {
  const results = await new AxeBuilder({ page }).analyze();
  const bad = results.violations.filter((v) => v.impact === 'serious' || v.impact === 'critical');
  expect(bad, JSON.stringify(bad.map((b) => [b.id, b.nodes.map((n) => n.target)]), null, 1)).toEqual([]);
}

async function noOverflow(page: Page, label: string) {
  await page.waitForTimeout(600);
  const over = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(over, `${label} overflows horizontally`).toBeLessThanOrEqual(0);
}

for (const [audience, routes] of [['staff', STAFF], ['customer', PORTAL]] as const) {
  test.describe(`${audience} routes`, () => {
    for (const route of routes) {
      test(`${route}`, async ({ page }) => {
        if (audience === 'staff') await signInAsStaff(page);
        else await signInAsCustomer(page);
        for (const width of VIEWPORTS) {
          await page.setViewportSize({ width, height: 900 });
          await page.goto(route);
          await page.waitForLoadState('networkidle');
          await page.waitForTimeout(800);
          await noOverflow(page, `${route} @${width}`);
          if (width === 360 || width === 1440) await noSeriousViolations(page);
        }
      });
    }
  });
}

test.describe('dialogs', () => {
  test.beforeEach(async ({ page }) => {
    await signInAsStaff(page);
  });

  test('manual charge, reverse and apply-credit dialogs are accessible and fit 360', async ({ page }) => {
    await page.setViewportSize({ width: 360, height: 780 });
    await page.goto(`/staff/billing/customers/${C}`);
    await page.getByRole('button', { name: 'Add manual charge' }).click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await noSeriousViolations(page);
    await noOverflow(page, 'manual charge dialog');
    await page.keyboard.press('Escape');
    await page.getByRole('button', { name: 'Apply credit' }).click();
    await noSeriousViolations(page);
    await page.keyboard.press('Escape');
    await page.goto(`/staff/billing/payments/${ids.cashPaymentId}`);
    await page.getByRole('button', { name: 'Reverse', exact: true }).click();
    await noSeriousViolations(page);
    await noOverflow(page, 'reverse dialog');
  });
});
