import { test, expect, signInAsCustomer, signInAsStaff } from './fixtures';
import { billingIds as ids } from '../../mocks/handlers/billing';
import { addDaysIso } from '../../lib/format/appointment';
import { todayInZone } from '../../lib/format/time';

/**
 * HO-BIL-002 §16 E-series against MSW (the real-API re-run is `billing-real-api.spec.ts`). One
 * audience per test (fixtures.ts); the mock store resets on every page load.
 */
test.describe.configure({ timeout: 90_000 });

const C = ids.customerId;

test.describe('staff — invoices and payments', () => {
  test.beforeEach(async ({ page }) => {
    await signInAsStaff(page);
  });

  test('E-A1: charges from three business lines combine into one numbered invoice', async ({ page }) => {
    await page.goto(`/staff/billing/invoices/new?customerId=${C}`);
    await page.getByLabel(/Consultation — Momo/).check();
    await page.getByRole('button', { name: 'Select all in Shop' }).click();
    await page.getByRole('button', { name: 'Select all in Boarding' }).click();
    await expect(page.getByText(/Estimate at today.s tax rates/)).toBeVisible();
    const issue = page.getByRole('button', { name: 'Issue invoice' });
    await expect(issue).toBeEnabled();
    await issue.click();
    await expect(page.getByRole('dialog')).toContainText('can no longer be changed');
    await page.getByRole('dialog').getByRole('button', { name: 'Issue invoice' }).click();
    await expect(page.getByRole('heading', { level: 1 })).toContainText(/INV-MAIN-2627-\d{6}/, { timeout: 20_000 });
    const lines = page.getByLabel('Invoice lines').first();
    await expect(lines).toContainText('Clinic');
    await expect(lines).toContainText('Shop');
    await expect(lines).toContainText('Boarding');
  });

  test('E-A2: a draft issued elsewhere reads as already issued', async ({ page }) => {
    await page.goto(`/staff/billing/invoices/${ids.draftInvoiceId}`);
    await expect(page.getByRole('button', { name: 'Issue invoice' })).toBeEnabled({ timeout: 20_000 });
    await page.evaluate(async (id) => {
      const base = (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl ?? '';
      await fetch(`${base}/api/staff/billing/invoices/${id}/issue`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json', 'Idempotency-Key': 'other-tab' },
        body: JSON.stringify({ expectedVersion: 3 }),
      });
    }, ids.draftInvoiceId);
    await page.getByRole('button', { name: 'Issue invoice' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Issue invoice' }).click();
    await expect(page.getByText(/already been issued/).first()).toBeVisible();
    await expect(page.getByRole('heading', { level: 1 })).toContainText('INV-MAIN-2627');
  });

  test('E-B1: split tender — cash, card slip, transfer — clears the invoice', async ({ page }) => {
    await page.goto(`/staff/billing/payments/new?customerId=${C}&invoiceId=${ids.issuedInvoiceId}`);
    const amount = page.getByLabel(/^Amount \(BDT\)/);
    await expect(amount).toHaveValue('920.00');
    await amount.fill('400');
    await page.getByLabel(/Amount for INV-MAIN-2627-000101/).fill('400');
    await page.getByRole('button', { name: 'Record payment' }).click();
    await expect(page.getByText(/receipt R-\d+/)).toBeVisible();
    await page.getByRole('button', { name: 'Take another payment' }).click();
    await expect(amount).toHaveValue('520.00');
    await page.getByRole('radio', { name: /Card in store/ }).check();
    await amount.fill('350');
    await page.getByLabel(/Amount for INV-MAIN-2627-000101/).fill('350');
    await page.getByLabel('Terminal slip number').fill('SLIP-1001');
    await page.getByRole('button', { name: 'Record payment' }).click();
    await page.getByRole('button', { name: 'Take another payment' }).click();
    await expect(amount).toHaveValue('170.00');
    await page.getByRole('radio', { name: /Bank transfer/ }).check();
    await page.getByLabel('Transfer reference').fill('TRF-7788');
    await page.getByRole('button', { name: 'Record payment' }).click();
    await expect(page.getByText('Invoice paid.')).toBeVisible();
  });

  test('E-B3: a card number as the reference is blocked and nothing is sent', async ({ page }) => {
    const posts: string[] = [];
    page.on('request', (r) => {
      if (r.method() === 'POST' && r.url().includes('/payments')) posts.push(r.url());
    });
    await page.goto(`/staff/billing/payments/new?customerId=${C}&invoiceId=${ids.issuedInvoiceId}`);
    await page.getByRole('radio', { name: /Card in store/ }).check();
    await page.getByLabel('Terminal slip number').fill('4111 1111 1111 1111');
    await expect(page.getByText(/looks like a card number/)).toBeVisible();
    await page.getByRole('button', { name: 'Record payment' }).click();
    await page.waitForTimeout(500);
    expect(posts).toHaveLength(0);
  });

  test('E-B2: a payment above the limit is pending; approval by another user works', async ({ page }) => {
    await page.goto('/staff/billing/approvals');
    await expect(page.getByText('You recorded this payment')).toBeVisible();
    await page.locator('button:not([disabled])', { hasText: /^Approve/ }).first().click();
    await expect(page.getByText(/approved\./)).toBeVisible();
  });

  test('E-B4: reverse a payment', async ({ page }) => {
    await page.goto(`/staff/billing/payments/${ids.cashPaymentId}`);
    await page.getByRole('button', { name: 'Reverse', exact: true }).click();
    await expect(page.getByRole('dialog')).toContainText("It will be removed from INV-MAIN-2627-000103 and the day’s cash-up");
    await page.getByLabel('Reason').fill('Entered against the wrong customer');
    await page.getByRole('button', { name: 'Reverse payment' }).click();
    await expect(page.getByText('Reversed').first()).toBeVisible();
  });

  test('E-C1/E-C2: credit note on a paid invoice releases credit; corrected re-issue links both ways', async ({ page }) => {
    await page.goto(`/staff/billing/invoices/${ids.paidInvoiceId}/credit`);
    await page.getByRole('textbox', { name: 'Reason' }).fill('Wrong vaccine billed');
    await page.getByRole('button', { name: 'Continue' }).click();
    await page.getByRole('button', { name: 'Continue' }).click();
    await page.getByRole('button', { name: 'Issue credit note' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Issue credit note' }).click();
    await expect(page.getByText(/Credit note CN-MAIN-2627-\d+ issued/)).toBeVisible();
    await expect(page.getByText('Released to customer credit')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Refund now' })).toBeVisible();
    await page.getByRole('button', { name: 'Corrected re-issue' }).click();
    await expect(page.getByRole('heading', { level: 1 })).toContainText('Corrected draft invoice');
    await expect(page.getByText(/Corrects INV-MAIN-2627-000103/)).toBeVisible();
  });

  test('E-C3: refund cash from free credit', async ({ page }) => {
    await page.goto(`/staff/billing/payments/${ids.freeCreditPaymentId}`);
    await page.getByRole('button', { name: 'Refund', exact: true }).click();
    await page.getByLabel(/^Amount/).fill('200');
    await page.getByLabel('Reason').fill('Customer changed their mind');
    await page.getByRole('dialog').getByRole('button', { name: 'Refund', exact: true }).click();
    await expect(page.getByText('Refund recorded.')).toBeVisible();
  });

  test('E-C1b: apply customer credit to another invoice shows the remaining credit', async ({ page }) => {
    await page.goto(`/staff/billing/customers/${C}`);
    await page.getByRole('button', { name: 'Apply credit' }).click();
    await page.getByLabel(/^Amount/).fill('100');
    await page.getByRole('dialog').getByRole('button', { name: 'Apply credit' }).click();
    await expect(page.getByText(/Free credit remaining/)).toBeVisible();
  });

  test('E-E1: redeem loyalty points on a draft', async ({ page }) => {
    await page.goto(`/staff/billing/invoices/${ids.draftInvoiceId}`);
    await page.getByLabel('Points to redeem').fill('100');
    await page.getByRole('button', { name: 'Redeem points' }).click();
    await expect(page.getByText(/Redeeming 100 points/)).toBeVisible();
  });
});

test.describe('staff — close and reconcile', () => {
  test.beforeEach(async ({ page }) => {
    await signInAsStaff(page);
  });

  test('E-D1: a short count needs an explanation before the day closes', async ({ page }) => {
    // decide the two pending payments first so only the variance blocks the close
    await page.goto('/staff/billing/approvals');
    await page.locator('button:not([disabled])', { hasText: /^Approve/ }).first().click();
    await page.getByRole('button', { name: /^Reject Sam Rahman/ }).first().click();
    await page.getByLabel('Reason').fill('Duplicate entry');
    await page.getByRole('button', { name: 'Reject payment' }).click();
    await page.goto(`/staff/billing/cash-up?date=${todayInZone('Asia/Dhaka')}`);
    await expect(page.getByText(/Expected cash in the till/)).toBeVisible();
    await page.getByLabel(/Counted cash/).fill('1');
    await page.getByRole('button', { name: 'Save count' }).click();
    await expect(page.getByText(/Short BDT/).first()).toBeVisible();
    await expect(page.getByLabel(/^Explanation/)).toHaveAttribute('required', '');
    await page.getByRole('button', { name: 'Close day' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Close day' }).click();
    await expect(page.getByText(/Enter an explanation before closing/)).toBeVisible();
    await page.getByLabel(/^Explanation/).fill('Coins miscounted');
    await page.getByRole('button', { name: 'Save count' }).click();
    await expect(page.getByRole('button', { name: 'Close day' })).toBeEnabled();
    await page.getByRole('button', { name: 'Close day' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Close day' }).click();
    await expect(page.getByText(/closed and read-only/)).toBeVisible();
  });

  test('E-D2: a COD remittance with a shortfall is listed', async ({ page }) => {
    await page.goto('/staff/billing/reconciliation/cod');
    await expect(page.getByText(/Shortfall lines/)).toBeVisible();
    await page.getByLabel(/Courier/).first().waitFor();
    await page.getByLabel('Total received').fill('1000');
    await page.getByLabel(/R-000110/).check();
    await page.getByLabel(/Remitted for R-000110/).fill('1000');
    await page.getByRole('button', { name: 'Record remittance' }).click();
    await expect(page.getByText(/Remittance recorded/)).toBeVisible();
  });

  test('E-D3: import, auto-match, then match the rest by hand', async ({ page }) => {
    await page.goto('/staff/billing/reconciliation/bank');
    await page.getByLabel('Bank account').fill('Test account');
    await page.getByLabel('Period from').fill(addDaysIso(todayInZone('Asia/Dhaka'), -10));
    await page.getByLabel('Period to').fill(todayInZone('Asia/Dhaka'));
    await page.getByLabel(/Statement file/).setInputFiles({
      name: 'stmt.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from('date,amount,direction,reference\n2026-10-04,320.00,C,TRF-X\n'),
    });
    await page.getByRole('button', { name: 'Import statement' }).click();
    await expect(page).toHaveURL(/reconciliation\/bank\/[0-9a-f-]+$/, { timeout: 20_000 });
    await page.getByRole('button', { name: 'Auto-match' }).click();
    await expect(page.getByText(/Auto-match finished: 1 matched/)).toBeVisible();
    await expect(page.getByText(/Unmatched statement lines \(0\)/)).toBeVisible();
  });

  test('E-D4: the export link is a plain download link', async ({ page }) => {
    await page.goto('/staff/billing/exports?from=2026-09-01&to=2026-09-30');
    const link = page.getByRole('link', { name: 'Download ZIP' });
    await expect(link).toHaveAttribute('href', /accounting-exports\/download\?from=2026-09-01&to=2026-09-30/);
    await page.goto('/staff/billing/exports?from=2025-01-01&to=2026-09-30');
    await expect(page.getByText(/at most 366 days/)).toBeVisible();
  });
});

test.describe('customer portal', () => {
  test.beforeEach(async ({ page }) => {
    await signInAsCustomer(page);
  });

  test('E-F1: own invoice shows how to pay; another customer’s is not found', async ({ page }) => {
    await page.goto(`/portal/billing/invoices/${ids.issuedInvoiceId}`);
    await expect(page.getByRole('heading', { name: 'How to pay' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Download PDF' })).toBeVisible();
    await page.goto(`/portal/billing/invoices/${ids.otherCustomerInvoiceId}`);
    await expect(page.getByText('Invoice not found')).toBeVisible();
  });
});
