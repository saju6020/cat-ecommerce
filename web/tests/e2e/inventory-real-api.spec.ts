import { test, expect as baseExpect, type Locator, type Page } from '@playwright/test';
import { dateIn, dialog, pickSku, wedgeScan } from './inventory-helpers';

/**
 * HO-IMP-002 §5.1 / §16 - the E-cases re-run against the REAL API (no MSW). Opt-in only: it needs a
 * live API, `NEXT_PUBLIC_USE_MOCKS=false`, the e2e staff in `catecommerce_dev` and four SKUs created
 * through the staff shop API (E2E-FOOD-1, E2E-FOOD-2, E2E-LITTER-1, E2E-MED-1; every real SKU is
 * `each` and whole-only, OQ-19). Credentials come from `E2E_STAFF_*` (git-ignored `web/.env.e2e.local`).
 *
 *   E2E_REAL_INVENTORY=1 E2E_API_URL=http://localhost:5006 PORT=3111 npx playwright test <this file> --workers=1
 */
// A cold dev server compiles each route on first visit, so assertions wait longer than the mock suite's 5 s.
const expect = baseExpect.configure({ timeout: 20_000 });
const run = process.env.E2E_REAL_INVENTORY === '1';
const API = process.env.E2E_API_URL ?? 'http://localhost:5006';
const PASSWORD = process.env.E2E_STAFF_PASSWORD ?? '';
const stamp = Date.now().toString(36).toUpperCase();
const lot = (suffix: string) => `E2E-${stamp}-${suffix}`;

test.describe.configure({ mode: 'serial' });
test.skip(!run, 'Set E2E_REAL_INVENTORY=1 (and see the header) to run against the real API.');

async function signIn(page: Page, identifier: string): Promise<void> {
  await page.goto('/staff/sign-in');
  // A cold dev server hydrates late and wipes what was typed before it: wait for the page to settle.
  await page.waitForLoadState('networkidle');
  await page.getByLabel(/mobile number or email/i).fill(identifier);
  await page.getByLabel(/^password/i).fill(PASSWORD);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page).not.toHaveURL(/\/staff\/sign-in/, { timeout: 20_000 });
}

/** Chooses the option whose text matches, by value (the labels carry the location kind). */
async function selectByText(select: Locator, text: RegExp): Promise<void> {
  const value = await select.locator('option').filter({ hasText: text }).first().getAttribute('value');
  if (!value) throw new Error(`no option matching ${text}`);
  await select.selectOption(value);
}

/**
 * A location allows one active count (the server refuses a second), and an earlier run may have left one
 * open or waiting for approval. Close it through the UI first - which also exercises Cancel and the
 * maker-checker Approve (a count above the approval limit needs someone other than the person who counted).
 */
const STALE_COUNT = /Counting|Submitted|Awaiting approval/;

async function closeStaleCounts(page: Page): Promise<void> {
  for (let guard = 0; guard < 5; guard += 1) {
    await page.goto('/staff/inventory/counts');
    await expect(page.getByRole('heading', { name: /counts/i }).first()).toBeVisible();
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('row').nth(1).or(page.getByText(/no counts yet/i))).toBeVisible();
    const stale = page.getByRole('row').filter({ hasText: STALE_COUNT }).getByRole('link').first();
    if ((await stale.count()) === 0) return;
    const href = (await stale.getAttribute('href')) ?? '/staff/inventory/counts';
    await stale.click();
    await page.waitForURL(`**${href}`);
    const cancel = page.getByRole('button', { name: /^cancel the count$/i });
    const approve = page.getByRole('button', { name: /^approve$/i });
    // The server refuses a count's own counter as its approver (a 400), although it lists Approve for them.
    await expect(cancel.or(approve).or(page.getByText(/waiting for approval/i)).first()).toBeVisible();
    if (await cancel.isVisible()) {
      await cancel.click();
      await (await dialog(page)).getByRole('button', { name: /^cancel the count$/i }).click();
    } else {
      const approver = process.env.E2E_STAFF_APPROVER;
      test.skip(!approver, 'A count above the approval limit is waiting for a second administrator; set E2E_STAFF_APPROVER (same password) to close it.');
      if (!approver) return;
      // Only a different person may approve: sign in as a second administrator (E2E_STAFF_APPROVER), close it, then come back.
      await page.context().clearCookies();
      await signIn(page, approver);
      await page.goto(href);
      await page.waitForLoadState('networkidle');
      await page.getByRole('button', { name: /^approve$/i }).click();
      await (await dialog(page)).getByRole('button', { name: /approve/i }).click();
      await expect(page.getByText(/committed/i).first()).toBeVisible();
      await page.context().clearCookies();
      await signIn(page, process.env.E2E_STAFF_ADMIN ?? '');
      continue;
    }
    await expect(page.getByText(/committed|cancelled/i).first()).toBeVisible();
  }
}

let foodSkuId = '';
let dispensaryLot = '';

test.beforeEach(async ({ page }) => {
  test.setTimeout(180_000);
  await signIn(page, process.env.E2E_STAFF_ADMIN ?? '');
});

test('R-A1: supplier, purchase order with decimal cost, partial receipt - 40 outstanding', async ({ page }) => {
  const code = `AC${stamp}`;
  await page.goto('/staff/procurement/suppliers/new');
  await page.getByLabel(/^code\b/i).fill(code);
  await page.getByLabel(/^name\b/i).fill(`Acme ${stamp}`);
  await page.getByLabel(/^country\b/i).selectOption('DE');
  await page.getByLabel('Currency').fill('BDT');
  await page.getByLabel('Payment terms').fill('Net 30');
  await page.getByRole('button', { name: /create supplier/i }).click();
  await expect(page.getByRole('heading', { name: `Acme ${stamp}` })).toBeVisible();

  await page.goto('/staff/procurement/purchase-orders/new');
  await selectByText(page.getByLabel(/^supplier\b/i), new RegExp(`Acme ${stamp}`));
  await selectByText(page.getByLabel(/^deliver to/i), /dispensary/i);
  await page.getByLabel(/^expected delivery/i).fill(dateIn(10));
  await pickSku(page, /add an item/i, 'E2E Kitten', /e2e kitten/i);
  await page.getByLabel(/^quantity/i).fill('100');
  await page.getByLabel(/^unit cost/i).fill('450.50');
  await page.getByRole('button', { name: /save draft/i }).click();
  await expect(page.getByRole('heading', { name: /^PO-/ })).toBeVisible();

  await page.getByRole('button', { name: /issue order/i }).click();
  await (await dialog(page)).getByRole('button', { name: /issue order/i }).click();
  await expect(page.getByText('Issued', { exact: true }).first()).toBeVisible();

  await page.getByRole('link', { name: /receive goods/i }).click();
  await expect(page.getByLabel(/^accepted/i)).toHaveValue('100');
  await page.getByLabel(/^accepted/i).fill('60');
  await page.getByLabel(/^rejected/i).fill('10');
  await page.getByLabel(/why was it rejected/i).selectOption('Damaged');
  await page.getByLabel(/^lot number/i).fill(lot('PO'));
  await page.getByLabel(/^expiry date/i).fill(dateIn(200));
  await page.getByRole('button', { name: /review and post/i }).click();
  await (await dialog(page)).getByRole('button', { name: /post receipt/i }).click();
  await expect(page.getByRole('heading', { name: /^GRN-/ })).toBeVisible();
  await expect(page.getByRole('link', { name: new RegExp(`lot ${lot('PO')}`, 'i') })).toBeVisible();

  await page.goto('/staff/procurement/purchase-orders');
  await page.getByRole('link', { name: /^PO-/ }).first().click();
  await expect(page.locator('main')).toContainText('40 each');
});

test('R-A2: a food SKU scanned by wedge needs an expiry date; adding it posts', async ({ page }) => {
  dispensaryLot = lot('DIRECT');
  await page.goto('/staff/inventory/receipts/new');
  await page.getByLabel(/^direct/i).check();
  await wedgeScan(page, '9900000000011');
  await expect(page.getByRole('listitem', { name: /line 1: e2e adult dry cat food/i })).toBeVisible();
  await selectByText(page.getByLabel(/^receive into|^location/i).first(), /dispensary/i);
  await page.getByLabel(/^lot number/i).fill(dispensaryLot);
  await page.getByLabel(/^accepted/i).fill('20');
  await page.getByLabel(/^unit cost/i).fill('1250.75');
  await page.getByRole('button', { name: /review and post/i }).click();
  const expiry = page.getByLabel(/^expiry date/i);
  await expect(expiry).toHaveAttribute('aria-invalid', 'true');
  await expiry.fill(dateIn(120));
  await page.getByRole('button', { name: /review and post/i }).click();
  await (await dialog(page)).getByRole('button', { name: /post receipt/i }).click();
  await expect(page.getByRole('heading', { name: /^GRN-/ })).toBeVisible();
});

test('R-C0: an unknown barcode says so and keeps the field', async ({ page }) => {
  await page.goto('/staff/inventory/receipts/new');
  await page.getByLabel(/^direct/i).check();
  await wedgeScan(page, '0000000000000');
  await expect(page.locator('[id$="-scan-live"]')).toContainText(/no sku has barcode/i);
});

test('R-C1: stock matrix, FEFO preview and batch detail for the received lot', async ({ page }) => {
  const found = await page.request.get(`${API}/api/staff/inventory/skus?search=E2E-FOOD-1`);
  foodSkuId = (await found.json())[0].skuId;
  await page.goto(`/staff/inventory/stock/${foodSkuId}`);
  await expect(page.locator('main')).toContainText(dispensaryLot);
  await page.getByLabel(/^quantity/i).fill('1');
  await page.getByRole('button', { name: /^preview$/i }).click();
  await expect(page.getByText('This is what would be picked next.')).toBeVisible();
  await page.getByRole('link', { name: new RegExp(dispensaryLot) }).first().click();
  await expect(page.locator('main')).toContainText(dispensaryLot);
});

test('R-C2: a transfer writes both ends to the ledger', async ({ page }) => {
  await page.goto('/staff/inventory/transfers/new');
  await selectByText(page.getByLabel(/^from\b/i), /dispensary/i);
  await selectByText(page.getByLabel(/^to\b/i), /boarding/i);
  await pickSku(page, /or find an item/i, 'E2E Adult', /e2e adult/i);
  await selectByText(page.getByLabel(/^batch/i), new RegExp(dispensaryLot));
  await page.getByLabel(/^quantity/i).fill('3');
  await page.getByRole('button', { name: /review and post/i }).click();
  await (await dialog(page)).getByRole('button', { name: /post transfer/i }).click();
  await expect(page.getByRole('cell', { name: /^TRF-/ }).first()).toBeVisible();
  await page.goto('/staff/inventory/ledger');
  await expect(page.getByRole('cell', { name: 'Transferred out' }).first()).toBeVisible();
  await expect(page.getByRole('cell', { name: 'Transferred in' }).first()).toBeVisible();
});

test('R-D1: a small adjustment posts; a decimal quantity for a whole-only item is refused in words', async ({ page }) => {
  await page.goto('/staff/inventory/adjustments/new');
  await selectByText(page.getByLabel(/^location/i), /dispensary/i);
  await page.getByRole('radio', { name: /damaged|theft or loss|wastage/i }).first().check();
  await page.getByLabel(/^note/i).fill('Tin dented while shelving');
  await pickSku(page, /or find an item/i, 'E2E Adult', /e2e adult/i);
  await selectByText(page.getByLabel(/^batch/i), new RegExp(dispensaryLot));
  await page.getByLabel(/^quantity/i).fill('1.5');
  await page.getByRole('button', { name: /review and request|review and post/i }).click();
  await expect(page.getByText(/whole|how many/i).first()).toBeVisible();
  await page.getByLabel(/^quantity/i).fill('1');
  await page.getByRole('button', { name: /review and request|review and post/i }).click();
  await (await dialog(page)).getByRole('button', { name: /request adjustment|post adjustment/i }).click();
  await expect(page.getByText(/posted|pending approval/i).first()).toBeVisible();
});

test('R-D2: a blind count, a variance report, then commit', async ({ page }) => {
  await closeStaleCounts(page);
  await page.goto('/staff/inventory/counts/new');
  await selectByText(page.getByLabel(/^location/i), /dispensary/i);
  // Selected items only: a whole-location count would record every other dispensary line as zero, and a
  // variance above the approval limit then waits for a second person.
  await page.getByRole('radio', { name: /selected items only/i }).check();
  await pickSku(page, /add an item/i, 'E2E Adult', /e2e adult/i);
  await page.getByRole('button', { name: /open (the )?count|start/i }).click();
  await expect(page.getByText(/this count is blind/i)).toBeVisible();
  await pickSku(page, /or find an item/i, 'E2E Adult', /e2e adult/i);
  await selectByText(page.getByLabel(/which lot of/i), new RegExp(dispensaryLot));
  await page.getByRole('button', { name: /add to the sheet/i }).click();
  const counted = page.getByLabel(/counted \(each\)/i).last();
  await counted.fill('15');
  await counted.blur();
  await expect(page.getByText('Saved')).toHaveCount(1);
  await expect(page.locator('main')).not.toContainText(/book quantity|on hand/i);
  await page.getByRole('button', { name: /submit the count/i }).click();
  await (await dialog(page)).getByRole('button', { name: /submit the count/i }).click();
  await expect(page.getByText('Nothing has been written to stock yet.')).toBeVisible();
  await page.getByRole('button', { name: /commit the count/i }).click();
  await (await dialog(page)).getByRole('button', { name: /^commit the count$/i }).click();
  await expect(page.getByText(/this count was committed|awaiting approval/i).first()).toBeVisible();
});

test('R-E1: near-expiry, stock-on-hand and ledger CSVs are fetched with X-Branch-Id and the CSRF token', async ({ page }) => {
  const seen: { url: string; branch: string | undefined; csrf: string | undefined }[] = [];
  page.on('request', (request) => {
    if (request.url().includes('.csv')) {
      const headers = request.headers();
      seen.push({ url: request.url(), branch: headers['x-branch-id'], csrf: headers['x-csrf-token'] });
    }
  });
  for (const path of ['/staff/inventory/reports/near-expiry', '/staff/inventory/reports/stock-on-hand', '/staff/inventory/ledger']) {
    await page.goto(path);
    const download = page.waitForEvent('download', { timeout: 30_000 });
    await page.getByRole('button', { name: /export csv/i }).click();
    expect((await download).suggestedFilename()).toMatch(/\.csv$/i);
  }
  expect(seen).toHaveLength(3);
  for (const request of seen) {
    expect(request.csrf, request.url).toBeTruthy();
    expect(request.branch, request.url).toBeTruthy();
  }
});

test('R-E2: the alerts page and the reorder suggestions load', async ({ page }) => {
  await page.goto('/staff/inventory/alerts');
  await expect(page.getByRole('heading', { name: /alerts/i }).first()).toBeVisible();
  await page.goto('/staff/procurement/reorder');
  await expect(page.getByRole('heading', { name: /reorder/i }).first()).toBeVisible();
});

test('R-F1: declare a recall on the lot, read the trace, then withdraw it', async ({ page }) => {
  await page.goto(`/staff/inventory/stock/${foodSkuId}`);
  await page.getByRole('link', { name: new RegExp(dispensaryLot) }).first().click();
  await page.getByRole('button', { name: /recall…/i }).click();
  const declare = await dialog(page);
  await declare.getByLabel(/reason \(internal\)/i).fill('E2E drill');
  await declare.getByLabel(/advice to customers/i).fill('Stop feeding it and contact us.');
  await declare.getByLabel(/type the lot number/i).fill(dispensaryLot);
  await declare.getByRole('button', { name: /declare recall/i }).click();
  await expect(page.getByRole('heading', { name: /^recall RCL-/i })).toBeVisible();
  await expect(page.getByRole('link', { name: /export trace as csv/i })).toBeVisible();
  await page.getByRole('button', { name: /withdraw the recall/i }).click();
  const confirm = await dialog(page);
  await confirm.getByLabel(/why is the recall being withdrawn/i).fill('E2E drill over');
  await confirm.getByRole('button', { name: /^withdraw the recall$/i }).click();
  await expect(page.getByText('Withdrawn', { exact: true }).first()).toBeVisible();
});

test('R-G1: report a storage breach at the dispensary', async ({ page }) => {
  await page.goto('/staff/inventory/storage-breaches/new');
  await selectByText(page.getByLabel('Where did it happen?'), /dispensary/i);
  await page.getByLabel('What happened?').fill('Fridge door left open overnight (E2E)');
  await page.getByLabel('Temperature reached (°C)').fill('11.5');
  await page.getByRole('button', { name: /report the breach/i }).click();
  await expect(page.getByText(/batch(es)? (was|were) flagged|no batches/i).first()).toBeVisible();
});

test('R-X3: a nurse reads stock but gets the server permission state on receiving and suppliers', async ({ page }) => {
  await page.context().clearCookies();
  await signIn(page, process.env.E2E_STAFF_NURSE ?? '');
  await page.goto('/staff/inventory/receipts/new');
  await expect(page.getByText(/you don.t have permission to do this/i)).toBeVisible();
  await page.goto('/staff/procurement/suppliers');
  await expect(page.getByText(/you don.t have permission to do this/i)).toBeVisible();
  await page.goto('/staff/inventory/stock');
  await expect(page.getByRole('heading', { name: /^stock$/i })).toBeVisible();
});
