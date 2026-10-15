import { test, expect, signInAsStaff } from './fixtures';
import { inventoryFixtureIds as ids } from '../../mocks/handlers/inventory';
import { dateIn, dialog, pickSku, warmRoutes, wedgeScan } from './inventory-helpers';
import { INVENTORY_ROUTES } from './inventory-routes';

/**
 * HO-IMP-002 §16 E-A1…E-B2, run against MSW (the same flows are re-run against the real API by
 * `inventory-real-api.spec.ts`). Procurement and goods receipt.
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

test('E-A1: create a supplier, build and issue a PO, receive part of it — outstanding stays visible', async ({
  page,
}) => {
  await page.goto('/staff/procurement/suppliers/new');
  await page.getByLabel(/^code\b/i).fill('ACME');
  await page.getByLabel(/^name\b/i).fill('Acme Pet Supplies');
  await page.getByLabel(/^country\b/i).selectOption('DE');
  await page.getByLabel('Currency').fill('BDT');
  await page.getByLabel('Payment terms').fill('Net 30');
  await page.getByRole('button', { name: /create supplier/i }).click();
  await expect(page.getByRole('heading', { name: 'Acme Pet Supplies' })).toBeVisible();

  await page.goto('/staff/procurement/purchase-orders/new');
  await page.getByLabel(/^supplier\b/i).selectOption({ label: 'Acme Pet Supplies (BDT)' });
  await page.getByLabel(/^deliver to/i).selectOption(ids.warehouseId);
  await page.getByLabel(/^expected delivery/i).fill(dateIn(10));
  await pickSku(page, /add an item/i, 'kitten', /kitten dry food/i);
  await page.getByLabel(/^quantity/i).fill('100');
  await page.getByLabel(/^unit cost/i).fill('450');
  await page.getByRole('button', { name: /save draft/i }).click();
  await expect(page.getByRole('heading', { name: /^PO-/ })).toBeVisible();
  await expect(page.getByText('Draft', { exact: true }).first()).toBeVisible();

  await page.getByRole('button', { name: /issue order/i }).click();
  await (await dialog(page)).getByRole('button', { name: /issue order/i }).click();
  await expect(page.getByText('Issued', { exact: true }).first()).toBeVisible();

  // Receive part of it: the form arrives pre-filled with what the order still expects.
  await page.getByRole('link', { name: /receive goods/i }).click();
  await expect(page.getByRole('listitem', { name: /line 1: kitten dry food/i })).toBeVisible();
  await expect(page.getByLabel(/^accepted/i)).toHaveValue('100');
  await page.getByLabel(/^accepted/i).fill('60');
  await page.getByLabel(/^rejected/i).fill('10');
  await page.getByLabel(/why was it rejected/i).selectOption('Damaged');
  await page.getByLabel(/^lot number/i).fill('L-E2E-1');
  await page.getByLabel(/^expiry date/i).fill(dateIn(200));
  await page.getByRole('button', { name: /review and post/i }).click();
  const confirm = await dialog(page);
  await expect(confirm).toContainText('60 bags of Kitten dry food');
  await confirm.getByRole('button', { name: /post receipt/i }).click();
  await expect(page.getByRole('heading', { name: /^GRN-/ })).toBeVisible();
  await expect(page.getByRole('link', { name: /lot L-E2E-1/i })).toBeVisible();

  // The order shows 40 bags outstanding with its unit.
  await page.goBack();
  await page.goto('/staff/procurement/purchase-orders');
  await page.getByRole('link', { name: /^PO-/ }).first().click();
  await expect(page.locator('main')).toContainText('40 bags');
});

test('E-A2: a food SKU without an expiry date is blocked on the line’s expiry field; adding it posts', async ({
  page,
}) => {
  await page.goto('/staff/inventory/receipts/new');
  await page.getByLabel(/^direct/i).check();
  await wedgeScan(page, ids.foodBarcode);
  await expect(page.getByRole('listitem', { name: /line 1: kitten dry food/i })).toBeVisible();
  await expect(page.getByText(/added: kitten dry food.* — fd-kdc-2kg/i)).toBeVisible();
  await page.getByLabel(/^lot number/i).fill('L-E2E-2');
  await page.getByLabel(/^accepted/i).fill('5');
  await page.getByLabel(/^unit cost/i).fill('450');
  await page.getByRole('button', { name: /review and post/i }).click();
  const expiry = page.getByLabel(/^expiry date/i);
  await expect(expiry).toHaveAttribute('aria-invalid', 'true');
  await expect(
    page.getByText('Food and medicine must be received with an expiry date.').first(),
  ).toBeVisible();
  await expect(expiry).toBeFocused();

  await expiry.fill(dateIn(120));
  await page.getByRole('button', { name: /review and post/i }).click();
  await (await dialog(page)).getByRole('button', { name: /post receipt/i }).click();
  await expect(page.getByRole('heading', { name: /^GRN-/ })).toBeVisible();
});

test('E-A3: 20 lines by wedge-style scanning, a reload mid-entry, restore, post once — confirm shown first', async ({
  page,
}) => {
  await page.goto('/staff/inventory/receipts/new');
  await page.getByLabel(/^direct/i).check();
  for (let i = 0; i < 20; i += 1)
    await wedgeScan(page, i % 2 === 0 ? ids.foodBarcode : ids.vaccineBarcode);
  await expect(page.getByRole('listitem', { name: /^line 20:/i })).toBeVisible();

  // Fill the first ten lines, wait for the autosave, then reload mid-entry.
  const accepted = page.getByLabel(/^accepted/i);
  const lots = page.getByLabel(/^lot number/i);
  for (let i = 0; i < 10; i += 1) {
    await lots.nth(i).fill(`L-A3-${String(i + 1).padStart(2, '0')}`);
    await accepted.nth(i).fill('2');
  }
  await expect(page.getByText(/draft saved/i)).toBeVisible();
  await page.reload();
  await expect(page.getByText('Restore the receipt you were entering?')).toBeVisible();
  await page.getByRole('button', { name: /restore the draft/i }).click();
  await expect(page.getByRole('listitem', { name: /^line 20:/i })).toBeVisible();
  await expect(page.getByLabel(/^lot number/i).first()).toHaveValue('L-A3-01');

  // Finish the rest and post.
  const total = await page.getByLabel(/^lot number/i).count();
  expect(total).toBe(20);
  for (let i = 0; i < 20; i += 1) {
    if (i >= 10) {
      await page
        .getByLabel(/^lot number/i)
        .nth(i)
        .fill(`L-A3-${String(i + 1).padStart(2, '0')}`);
      await page
        .getByLabel(/^accepted/i)
        .nth(i)
        .fill('2');
    }
    await page
      .getByLabel(/^expiry date/i)
      .nth(i)
      .fill(dateIn(200 + i));
    await page
      .getByLabel(/^unit cost/i)
      .nth(i)
      .fill('10');
  }
  await page.getByRole('button', { name: /review and post/i }).click();
  const confirm = await dialog(page);
  await expect(confirm).toContainText('20 lines will be received');
  await expect(confirm).toContainText('A posted receipt cannot be edited');
  await confirm.getByRole('button', { name: /post receipt/i }).click();
  await expect(page.getByRole('heading', { name: /^GRN-/ })).toBeVisible();

  // Posted once: the draft is gone and the receipt list shows exactly one new receipt.
  await page.goto('/staff/inventory/receipts/new');
  await expect(page.getByText('Restore the receipt you were entering?')).toHaveCount(0);
  await page.goto('/staff/inventory/receipts');
  await expect(page.getByRole('link', { name: /^GRN-/ })).toHaveCount(2); // the seeded one plus this
});

test('E-B1: plan a consignment, add documents, clear it, receive against it, allocate landed cost', async ({
  page,
}) => {
  await page.goto('/staff/procurement/consignments/new');
  await page.getByLabel(/^country of origin/i).selectOption('DE');
  await page.getByLabel(/^shipment reference/i).fill('SHP-E2E-1');
  await page.getByLabel(/PO-2026-000002/).check();
  await page.getByRole('button', { name: /add an HS code/i }).click();
  await page.getByLabel('HS code 1', { exact: true }).fill('2309.10');
  await page.getByRole('button', { name: /plan consignment/i }).click();
  await expect(page.getByRole('heading', { name: /^CN-/ })).toBeVisible();

  // The three clearance numbers, then the three advances.
  await page.getByLabel(/^import permit number/i).fill('IP-E2E');
  await page.getByLabel(/^health certificate number/i).fill('HC-E2E');
  await page.getByLabel(/^customs declaration number/i).fill('CD-E2E');
  await page.getByRole('button', { name: /save details/i }).click();
  for (const next of [/mark as in transit/i, /mark as at customs/i, /mark as cleared/i]) {
    await page.getByRole('button', { name: next }).click();
    await (await dialog(page)).getByRole('button', { name: next }).click();
    await expect(page.getByRole('dialog')).toHaveCount(0);
  }
  await expect(page.locator('li[aria-current="step"]')).toContainText('Cleared');

  // Documents: add one; no file input; no delete.
  await page.getByRole('link', { name: 'Documents' }).click();
  await expect(page.locator('input[type="file"]')).toHaveCount(0);
  await page.getByLabel(/^kind/i).selectOption('BillOfLading');
  await page.getByLabel(/^file name/i).fill('bol-e2e.pdf');
  await page.getByLabel(/^size \(bytes\)/i).fill('2048');
  await page.getByLabel(/^stored at/i).fill('import/bol-e2e.pdf');
  await page.getByRole('button', { name: /record document/i }).click();
  await expect(page.getByText('bol-e2e.pdf')).toBeVisible();
  await expect(page.getByRole('button', { name: /delete/i })).toHaveCount(0);

  // Receive against the cleared consignment.
  const url = page.url();
  const consignmentId = url.match(/consignments\/([^/]+)/)![1]!;
  await page.goto(`/staff/inventory/receipts/new?consignmentId=${consignmentId}`);
  await wedgeScan(page, ids.foodBarcode);
  await page.getByLabel(/^lot number/i).fill('L-B1-1');
  await page.getByLabel(/^expiry date/i).fill(dateIn(300));
  await page.getByLabel(/^accepted/i).fill('30');
  await page.getByLabel(/^unit cost/i).fill('450');
  await page.getByLabel(/^cost currency/i).fill('BDT');
  await page.getByRole('button', { name: /review and post/i }).click();
  await (await dialog(page)).getByRole('button', { name: /post receipt/i }).click();
  await expect(page.getByRole('heading', { name: /^GRN-/ })).toBeVisible();

  // Costs and landed cost.
  await page.goto(`/staff/procurement/consignments/${consignmentId}/costs`);
  await page.getByRole('button', { name: /add a cost/i }).click();
  await page.getByLabel(/^amount/i).fill('900');
  await page.getByRole('button', { name: /save costs and rates/i }).click();
  await expect(page.getByText(/costs saved/i)).toBeVisible();
  await page.getByRole('button', { name: /allocate landed cost/i }).click();
  const confirm = await dialog(page);
  await expect(confirm).toContainText('This recalculates the landed unit cost of 1 batch.');
  await confirm.getByRole('button', { name: /allocate landed cost/i }).click();
  await expect(page.locator('main')).toContainText('480.0000');

  // The inspection pack shows everything; the batches tab shows the landed unit cost.
  await page.getByRole('link', { name: 'Inspection pack' }).click();
  await expect(page.getByRole('heading', { name: /inspection pack/i })).toBeVisible();
  await expect(page.getByText('IP-E2E').first()).toBeVisible();
  await expect(page.getByText('bol-e2e.pdf')).toBeVisible();
  await expect(page.getByText('L-B1-1')).toBeVisible();
  await page.getByRole('link', { name: 'Batches' }).click();
  await expect(page.getByText(/480\.0000.*\(landed\)/)).toBeVisible();
});

test('E-B2: receiving against an uncleared consignment is refused with the server’s plain-language reason', async ({
  page,
}) => {
  await page.goto(`/staff/inventory/receipts/new?consignmentId=${ids.consignmentPlannedId}`);
  await wedgeScan(page, ids.foodBarcode);
  await page.getByLabel(/^lot number/i).fill('L-B2');
  await page.getByLabel(/^expiry date/i).fill(dateIn(200));
  await page.getByLabel(/^accepted/i).fill('3');
  await page.getByLabel(/^unit cost/i).fill('450');
  await page.getByRole('button', { name: /review and post/i }).click();
  await (await dialog(page)).getByRole('button', { name: /post receipt/i }).click();
  await expect(
    page.getByText(/goods can only be received against a cleared consignment/i).first(),
  ).toBeVisible();
  await expect(page.getByRole('heading', { name: /^GRN-/ })).toHaveCount(0);
});
