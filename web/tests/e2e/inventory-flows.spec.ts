import { test, expect, signInAsStaff } from './fixtures';
import { inventoryFixtureIds as ids } from '../../mocks/handlers/inventory';
import { dialog, pickSku, signInAsNurse, warmRoutes } from './inventory-helpers';
import { INVENTORY_ROUTES } from './inventory-routes';

/**
 * HO-IMP-002 §16 E-C1…E-G1 and E-X3, run against MSW. Stock, transfers, adjustments, counts,
 * alerts, reorder, the recall console and storage breaches.
 */
test.beforeAll(async ({ request }) => {
  test.setTimeout(600_000);
  await warmRoutes(
    request,
    INVENTORY_ROUTES.map((r) => r.path),
  );
});

test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

async function preview(page: import('@playwright/test').Page, quantity: string) {
  await page.getByLabel(/^quantity/i).fill(quantity);
  await page.getByRole('button', { name: /^preview$/i }).click();
}

test('E-C1: the FEFO preview changes to the next batch once the soonest-expiring one is quarantined', async ({
  page,
}) => {
  await page.goto(`/staff/inventory/stock/${ids.foodSkuId}`);
  await preview(page, '1');
  const planOf = () => page.getByText('This is what would be picked next.').locator('xpath=..');
  const plan = planOf();
  await expect(plan).toContainText('L2406X'); // expires today: still usable, soonest first
  await expect(page.getByText('This is what would be picked next.')).toBeVisible();

  // Quarantine it from its own page (reason, confirm naming the lot).
  await page.goto(`/staff/inventory/batches/${ids.batchFoodTodayId}`);
  await page.getByRole('button', { name: 'Quarantine', exact: true }).click();
  const confirm = await dialog(page);
  await expect(confirm).toContainText('Quarantine lot L2406X of Kitten dry food');
  await confirm.getByLabel(/reason/i).fill('Suspected spoilage');
  await confirm.getByRole('button', { name: /quarantine this batch/i }).click();
  await expect(page.getByText('Quarantined', { exact: true }).first()).toBeVisible();

  await page.goto(`/staff/inventory/stock/${ids.foodSkuId}`);
  await preview(page, '1');
  await expect(planOf()).toContainText('L2408B');
  await expect(planOf()).not.toContainText('L2406X');
  // The quarantined batch is labelled with its reason in the matrix.
  await expect(page.locator('main')).toContainText('Quarantined');
  // An expired batch is unusable and says so.
  await expect(page.getByText(/expired 3 days ago/i).first()).toBeVisible();
});

test('E-C2: a transfer writes both ends to the ledger with its reference', async ({ page }) => {
  await page.goto('/staff/inventory/transfers/new');
  await page.getByLabel(/^from\b/i).selectOption(ids.warehouseId);
  await page.getByLabel(/^to\b/i).selectOption(ids.shopFloorId);
  await pickSku(page, /or find an item/i, 'kitten', /kitten dry food/i);
  await page.getByLabel(/^batch/i).selectOption(ids.batchFoodFarId);
  await page.getByLabel(/^quantity/i).fill('3');
  await page.getByRole('button', { name: /review and post/i }).click();
  const confirm = await dialog(page);
  await expect(confirm).toContainText('from Main warehouse to Shop floor');
  await confirm.getByRole('button', { name: /post transfer/i }).click();
  await expect(page.getByRole('heading', { name: /^transfers$/i })).toBeVisible();
  await expect(page.getByRole('cell', { name: /^TRF-/ }).first()).toBeVisible();

  await page.goto('/staff/inventory/ledger');
  await expect(page.getByRole('cell', { name: 'Transferred out' }).first()).toBeVisible();
  await expect(page.getByRole('cell', { name: 'Transferred in' }).first()).toBeVisible();
  await expect(page.getByText(/Transfer TRF-/).first()).toBeVisible();
  await expect(page.getByText('-3 bags').first()).toBeVisible();
  await expect(page.getByText('+3 bags').first()).toBeVisible();
});

test('E-D1: an adjustment above the limit waits for approval; the requester cannot approve it; a second person can', async ({
  page,
}) => {
  await page.goto('/staff/inventory/adjustments/new');
  await page.getByLabel(/^location/i).selectOption(ids.warehouseId);
  await page.getByRole('radio', { name: /theft or loss/i }).check();
  await page.getByLabel(/^note/i).fill('Pallet short at the monthly check');
  await pickSku(page, /or find an item/i, 'kitten', /kitten dry food/i);
  // 12 bags at 462.5 is above the approval limit; 10 would not be.
  await page.getByLabel(/^batch/i).selectOption(ids.batchFoodFarId);
  await page.getByLabel(/^quantity/i).fill('12');
  await page.getByRole('button', { name: /review and request/i }).click();
  const confirm = await dialog(page);
  await expect(confirm).toContainText('cannot be undone — a correction is a new adjustment');
  await confirm.getByRole('button', { name: /request adjustment/i }).click();
  await expect(page.getByText(/pending approval/i).first()).toBeVisible();
  await expect(page.getByText(/nothing has been written to the ledger yet/i)).toBeVisible();

  // The requester (this same account) cannot approve their own request.
  await page.getByRole('link', { name: /view the adjustment/i }).click();
  await page.getByRole('button', { name: 'Approve' }).click();
  await (await dialog(page)).getByRole('button', { name: /approve and post/i }).click();
  await expect(page.getByText('Someone else has to approve your request.')).toBeVisible();
  await page.keyboard.press('Escape');

  // A request made by another person (the nurse, seeded) is approved and posted.
  await page.goto(`/staff/inventory/adjustments/${ids.adjustmentPendingId}`);
  await page.getByRole('button', { name: 'Approve' }).click();
  await (await dialog(page)).getByRole('button', { name: /approve and post/i }).click();
  await expect(page.getByText('Posted', { exact: true }).first()).toBeVisible();

  // …and one whose stock has moved says to recount.
  await page.goto(`/staff/inventory/adjustments/${ids.adjustmentStaleId}`);
  await page.getByRole('button', { name: 'Approve' }).click();
  await (await dialog(page)).getByRole('button', { name: /approve and post/i }).click();
  await expect(
    page.getByText(
      'Stock has moved since this was requested — recount and raise a new adjustment.',
    ),
  ).toBeVisible();
});

test('E-D2: a blind count — no book quantity while counting, a variance report before anything is written, then commit', async ({
  page,
}) => {
  await page.goto(`/staff/inventory/counts/${ids.countOpenId}`);
  await expect(page.getByText(/this count is blind/i)).toBeVisible();
  await pickSku(page, /or find an item/i, 'litter', /cat litter/i);
  await page.getByLabel(/which lot of cat litter/i).selectOption(ids.batchLitterId);
  await page.getByRole('button', { name: /add to the sheet/i }).click();
  await page
    .getByLabel(/counted \(bags\)/i)
    .last()
    .fill('38');
  await page
    .getByLabel(/counted \(bags\)/i)
    .last()
    .blur();
  await expect(page.getByText('Saved')).toHaveCount(2);
  const body = page.locator('main');
  await expect(body).not.toContainText(/book quantity|variance|on hand/i);

  await page.getByRole('button', { name: /submit the count/i }).click();
  await (await dialog(page)).getByRole('button', { name: /submit the count/i }).click();
  await expect(page.getByText('Nothing has been written to stock yet.')).toBeVisible();
  await expect(page.locator('main')).toContainText('-2 bags');
  await page.getByRole('button', { name: /commit the count/i }).click();
  const confirm = await dialog(page);
  await expect(confirm).toContainText(/\d+ variances?, /);
  await confirm.getByRole('button', { name: /^commit the count$/i }).click();
  await expect(page.getByText(/this count was committed/i)).toBeVisible();

  await page.goto('/staff/inventory/ledger');
  await expect(page.getByRole('cell', { name: 'Count loss' }).first()).toBeVisible();
});

test('E-E1: an expiry alert is visible without looking for it, and the near-expiry report shows days and value at risk', async ({
  page,
}) => {
  await page.goto('/staff/inventory');
  await expect(page.getByRole('link', { name: /alerts.*open/i })).toBeVisible();
  await page.goto('/staff/inventory/alerts');
  await expect(page.getByText(/expires in 60 days or less/i)).toBeVisible();
  await page.goto('/staff/inventory/reports/near-expiry');
  const table = page.locator('main');
  await expect(table).toContainText('45 days');
  await page.getByLabel('Include expired stock').check();
  await expect(page.locator('main')).toContainText('expired 3 days ago');
  await expect(page.getByText(/total value at risk/i).first()).toBeVisible();
  await expect(page.getByRole('button', { name: /export csv/i })).toBeVisible();
});

test('E-E2: a low-stock alert and a reorder suggestion lead to a pre-filled purchase order', async ({
  page,
}) => {
  await page.goto('/staff/inventory/alerts');
  await expect(page.getByRole('cell', { name: /below reorder point/i }).first()).toBeVisible();
  await page.goto('/staff/procurement/reorder');
  await expect(page.locator('main')).toContainText('Adult wet food pouches, tuna');
  await page.getByRole('checkbox', { name: /select adult wet food/i }).check();
  await page.getByRole('link', { name: /build the order for meridian/i }).click();
  await expect(page.getByRole('heading', { name: /new purchase order/i })).toBeVisible();
  await expect(page.getByLabel('Supplier')).toHaveValue(ids.supplierId);
  await expect(page.getByLabel(/^quantity/i)).toHaveValue('40');
});

test('E-F1: declare a recall, read the trace, export it, notify, then notify again', async ({
  page,
}) => {
  await page.goto(`/staff/inventory/batches/${ids.batchFoodFarId}`);
  await page.getByRole('button', { name: /recall…/i }).click();
  const declare = await dialog(page);
  await expect(declare).toContainText(
    'All remaining stock of lot L2409E becomes unusable immediately, everywhere.',
  );
  await declare.getByLabel(/reason \(internal\)/i).fill('Supplier contamination notice');
  await declare.getByLabel(/advice to customers/i).fill('Stop feeding it and contact us.');
  const button = declare.getByRole('button', { name: /declare recall/i });
  await declare.getByLabel(/type the lot number/i).fill('L2409');
  await expect(button).toBeDisabled();
  await declare.getByLabel(/type the lot number/i).fill('L2409E');
  await expect(button).toBeEnabled();
  await button.click();

  // The console: the batch is shown recalled and held; the trace covers the four cases; CSV is a server link.
  await expect(page.getByRole('heading', { name: /^recall RCL-/i })).toBeVisible();
  await expect(page.getByText(/declaring a recall messages no one/i)).toBeVisible();
  await expect(page.locator('main')).toContainText('Bought');
  await expect(page.locator('main')).toContainText('Dispensed');
  await expect(page.locator('main')).toContainText('Fed during boarding');
  await page.getByRole('tab', { name: /unshipped/i }).click();
  await expect(page.getByText(/must not be dispatched while the lot is recalled/i)).toBeVisible();
  await expect(page.getByRole('link', { name: /export trace as csv/i })).toHaveAttribute(
    'href',
    /trace\.csv/,
  );

  await page.getByRole('button', { name: /notify everyone/i }).click();
  const notify = await dialog(page);
  await expect(notify).toContainText(/\d+ customers will be messaged/);
  await notify.getByRole('button', { name: /^notify \d+ customers?$/i }).click();
  await expect(
    page.getByText(/\d+ messaged, 0 previously, 0 left\. every message is logged/i),
  ).toBeVisible();
  await page.getByRole('button', { name: /notify everyone/i }).click();
  await (await dialog(page)).getByRole('button', { name: /^notify \d+ customers?$/i }).click();
  await expect(page.getByText(/0 messaged, \d+ previously, 0 left/i)).toBeVisible();

  // The batch itself is now recalled and held.
  await page.goto(`/staff/inventory/batches/${ids.batchFoodFarId}`);
  await expect(page.getByText('Recalled — held').first()).toBeVisible();
});

test('E-F2: withdrawing a recall quarantines the batch and says unsent messages will not be sent', async ({
  page,
}) => {
  await page.goto(`/staff/inventory/recalls/${ids.batchRecalledId}`);
  await page.getByRole('button', { name: /withdraw the recall/i }).click();
  const confirm = await dialog(page);
  await expect(confirm).toContainText(
    'The batch goes back to quarantine, not on sale. Messages not yet sent will not be sent.',
  );
  await confirm.getByLabel(/why is the recall being withdrawn/i).fill('Declared by mistake');
  await confirm.getByRole('button', { name: /^withdraw the recall$/i }).click();
  await expect(page.getByText('Withdrawn', { exact: true }).first()).toBeVisible();
  await page.goto(`/staff/inventory/batches/${ids.batchRecalledId}`);
  await expect(page.getByText('Quarantined', { exact: true }).first()).toBeVisible();
});

test('E-G1: report a storage breach, quarantine the affected stock, close it', async ({ page }) => {
  await page.goto('/staff/inventory/storage-breaches/new');
  await page.getByLabel('Where did it happen?').selectOption(ids.dispensaryId);
  await page.getByLabel('What happened?').fill('Fridge door left open overnight');
  await page.getByLabel('Temperature reached (°C)').fill('11.5');
  await page.getByRole('button', { name: /report the breach/i }).click();
  await expect(page.getByText(/batches were flagged|batch was flagged/i)).toBeVisible();
  await expect(page.locator('main')).toContainText('L7701V');

  await page.getByRole('button', { name: /quarantine affected stock/i }).click();
  const confirm = await dialog(page);
  await expect(confirm).toContainText('pending a decision');
  await expect(confirm).toContainText('Quarantine shelf');
  await confirm.getByRole('button', { name: /^quarantine affected stock$/i }).click();
  await expect(page.getByText('Stock quarantined', { exact: true }).first()).toBeVisible();

  await page.goto(`/staff/inventory/stock/${ids.vaccineSkuId}`);
  await expect(page.locator('main')).toContainText('in quarantine location');

  await page.goto('/staff/inventory/storage-breaches');
  await page
    .getByRole('row', { name: /fridge door left open/i })
    .first()
    .getByRole('link')
    .click();
  await page.getByRole('button', { name: /close the report/i }).click();
  const close = await dialog(page);
  await close.getByLabel(/closing note/i).fill('Stock checked and recorded');
  await close.getByRole('button', { name: /^close the report$/i }).click();
  await expect(page.getByText('Closed', { exact: true }).first()).toBeVisible();
});

test('E-X3: a nurse gets the server’s permission state on receiving and on suppliers', async ({
  page,
}) => {
  await page.context().clearCookies();
  await signInAsNurse(page);
  await page.goto('/staff/inventory/receipts/new');
  await expect(page.getByText(/you don.t have permission to do this/i)).toBeVisible();
  await page.goto('/staff/procurement/suppliers');
  await expect(page.getByText(/you don.t have permission to do this/i)).toBeVisible();
  // …but stock is theirs to read.
  await page.goto('/staff/inventory/stock');
  await expect(page.getByRole('heading', { name: /^stock$/i })).toBeVisible();
});
