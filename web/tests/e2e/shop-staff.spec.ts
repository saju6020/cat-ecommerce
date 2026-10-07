import { shopFixtureIds } from '../../mocks/handlers/shop';
import { test, expect as baseExpect, signInAsStaff } from './fixtures';

// The first visit to each back-office route compiles it in `next dev`: allow for that.
const expect = baseExpect.configure({ timeout: 20_000 });

/**
 * HO-SHP-002 §16 E-C1, E-C2, E-C3, E-F1 (staff half) — run against MSW. In mock mode the browser
 * worker and the server-rendering `msw/node` hold SEPARATE in-memory databases, so a change staff
 * make in the browser is not visible to a server-rendered storefront page; the "visible in the shop"
 * halves are therefore recorded as real-API cases in TC-HO-SHP-002 (not runnable here).
 */

test.describe('catalogue back office (US-SHP-01…03)', () => {
  test('E-C1: create a food product, add a SKU and price, publish without origin (refused), add it, publish', async ({
    page,
  }) => {
    test.setTimeout(120_000);
    await signInAsStaff(page);
    const stamp = Date.now().toString().slice(-6);
    const name = `E2E Thai Kitten Food ${stamp}`;
    const slug = `e2e-thai-kitten-food-${stamp}`;

    await page.goto('/staff/shop/products/new');
    // A cold dev server can lose the mock worker's start race: the page then offers a retry.
    const heading = page.getByRole('heading', { level: 1, name: 'New product' });
    const retry = page.getByRole('button', { name: 'Try again' });
    await expect(heading.or(retry)).toBeVisible();
    if (await retry.isVisible()) await retry.click();
    await expect(heading).toBeVisible();
    await page.getByLabel('Name').fill(name);
    await page.getByLabel('Brand').fill('E2E Brand');
    await page.getByLabel('Category').selectOption('CatFood');
    await page.getByLabel('Tax class').selectOption({ label: 'Standard' });
    await expect(page.getByLabel(/Web address/)).toHaveValue(slug);
    await page.getByRole('button', { name: 'Create product' }).click();
    await expect(page.getByRole('heading', { level: 1, name })).toBeVisible();
    await expect(page.getByText('Draft', { exact: true }).first()).toBeVisible();

    // A SKU and its price.
    const editorUrl = page.url();
    await page.goto(`${editorUrl}/variants`);
    await page.getByRole('button', { name: 'Add SKU' }).click();
    const addDialog = page.getByRole('dialog', { name: 'Add SKU' });
    await addDialog.getByLabel('SKU code').fill(`E2E-${stamp}`);
    await addDialog.getByLabel('Size or weight').fill('1 kg');
    await addDialog.getByRole('button', { name: 'Add SKU' }).click();
    await expect(page.getByRole('rowheader', { name: new RegExp(`E2E-${stamp}`) })).toBeVisible();
    await page.getByRole('button', { name: `Prices for E2E-${stamp}` }).click();
    await page.getByRole('button', { name: 'Schedule a new price' }).click();
    const priceDialog = page.getByRole('dialog', { name: 'Schedule a new price' });
    await priceDialog.getByLabel('Amount (BDT)').fill('1200');
    await priceDialog.getByRole('button', { name: 'Schedule', exact: true }).click();
    await expect(page.getByRole('region', { name: 'Current' })).toContainText('৳1,200.00');

    // Food labelling with a life stage (no country of origin yet).
    await page.goto(`${editorUrl}/food`);
    await page.getByLabel('Life stage').selectOption('Kitten');
    await page.getByRole('button', { name: 'Save' }).click();

    // Publish without a country of origin: refused, naming it; the checklist takes focus.
    await page.getByRole('button', { name: 'Publish', exact: true }).click();
    await page
      .getByRole('dialog', { name: 'Publish this product?' })
      .getByRole('button', { name: 'Publish' })
      .click();
    await expect(page.getByRole('alert').filter({ hasText: /country of origin/i })).toBeVisible();
    await expect(page.getByRole('list', { name: 'Publish checklist' })).toBeFocused();
    await expect(page.getByText('Draft', { exact: true }).first()).toBeVisible();

    // Add the country on the Details section, then publish.
    await page.goto(editorUrl);
    await page.getByLabel('Country of origin').selectOption('TH');
    await page.getByRole('button', { name: 'Save' }).click();
    await expect(page.getByRole('list', { name: 'Publish checklist' })).toContainText('Done:');
    await page.getByRole('button', { name: 'Publish', exact: true }).click();
    await page
      .getByRole('dialog', { name: 'Publish this product?' })
      .getByRole('button', { name: 'Publish' })
      .click();
    await expect(page.getByText('Active (in the shop)')).toBeVisible();

    // The staff list shows it as Active; a draft's public URL is a not-found page.
    await page.goto(`/staff/shop/products?q=${encodeURIComponent(name)}`);
    await expect(page.getByRole('row', { name: new RegExp(name) })).toContainText('Active');
    const draft = await page.goto(`/shop/products/${shopFixtureIds.draftSlug}`);
    expect(draft?.status()).toBe(404);
  });

  test('E-C2: a price scheduled for tomorrow is listed under Scheduled; the storefront still shows today’s', async ({
    page,
  }) => {
    await signInAsStaff(page);
    await page.goto(`/staff/shop/products/${shopFixtureIds.multiVariantProductId}/variants`);
    await page.getByRole('button', { name: 'Prices for RC-K-400G' }).click();
    await page.getByRole('button', { name: 'Schedule a new price' }).click();
    const dialog = page.getByRole('dialog', { name: 'Schedule a new price' });
    await dialog.getByLabel('Amount (BDT)').fill('700');
    await dialog.getByRole('radio', { name: /future date/i }).check();
    const tomorrow = new Date(Date.now() + 24 * 3600 * 1000);
    const pad = (n: number) => String(n).padStart(2, '0');
    await dialog
      .getByLabel('Effective date and time')
      .fill(
        `${tomorrow.getFullYear()}-${pad(tomorrow.getMonth() + 1)}-${pad(tomorrow.getDate())}T10:00`,
      );
    await dialog.getByRole('button', { name: 'Schedule', exact: true }).click();
    await expect(page.getByRole('region', { name: 'Scheduled' })).toContainText('৳700.00');
    await expect(page.getByRole('region', { name: 'Current' })).toContainText('৳650.00');

    await page.goto(`/shop/products/${shopFixtureIds.multiVariantSlug}`);
    await expect(
      page.getByRole('region', { name: 'Price and availability' }).getByText('৳650.00'),
    ).toBeVisible();
  });

  test('E-C3: moderating a pending review publishes it, and it appears on the product page', async ({
    page,
  }) => {
    await signInAsStaff(page);
    await page.goto('/staff/shop/reviews');
    const card = page.getByRole('article').filter({ hasText: 'Wish the bag was bigger.' });
    await expect(card).toContainText('Awaiting approval');
    await card.getByRole('button', { name: /Publish review of/ }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Publish' }).click();
    await expect(card).toContainText('Published');

    await page.goto(`/shop/products/${shopFixtureIds.multiVariantSlug}`);
    await expect(page.getByText('Wish the bag was bigger.')).toBeVisible();
    // Review text from customers is shown literally (plain text).
    await expect(page.getByText('Good, but pricey')).toBeVisible();
    await expect(page.getByText(/<b>Not<\/b> cheap though \*\*really\*\*/)).toBeVisible();
  });

  test('E-C3: a rejected review needs a reason', async ({ page }) => {
    await signInAsStaff(page);
    await page.goto('/staff/shop/reviews');
    const pending = page.getByRole('article').filter({ hasText: 'Nice salmon smell' });
    await pending.getByRole('button', { name: /Reject review of/ }).click();
    const dialog = page.getByRole('dialog', { name: 'Reject this review?' });
    await dialog.getByRole('button', { name: 'Reject review' }).click();
    await expect(dialog.getByText('Choose a reason for rejecting this review.')).toBeVisible();
    await dialog.getByLabel('Reason').selectOption('Spam');
    await dialog.getByRole('button', { name: 'Reject review' }).click();
    await expect(pending).toContainText('Rejected');
  });
});

test.describe('content languages (US-SHP-14)', () => {
  test('E-F1: switch to bn on a product (English fallback where missing); staff edits the bn feeding guide', async ({
    page,
  }) => {
    await page.goto(`/shop/products/${shopFixtureIds.thaiKittenSlug}`);
    await page.getByLabel('Product language').selectOption('bn');
    await expect(page).toHaveURL(/lang=bn/);
    await expect(
      page.getByRole('heading', { level: 1, name: 'হুইস্কাস কিটেন স্যামন' }),
    ).toBeVisible();
    await page.getByRole('tab', { name: 'Feeding guide' }).click();
    const feeding = page.getByRole('tabpanel', { name: 'Feeding guide' });
    await expect(feeding).toContainText('Daily amounts'); // falls back to English
    await expect(feeding.locator('[lang="en"]')).toHaveCount(1);

    await page.goto('/staff/sign-in');
    await signInAsStaff(page);
    await page.goto(`/staff/shop/products/${shopFixtureIds.thaiKittenProductId}/translations`);
    await expect(page.getByRole('heading', { name: /Bangla \(bn\)/ })).toBeVisible();
    await page
      .getByLabel('Feeding guide', { exact: true })
      .fill('## দৈনিক পরিমাণ\n\nদিনে ৪০ গ্রাম।');
    await page.getByRole('button', { name: 'Save Bangla' }).click();
    await page.reload();
    await expect(page.getByLabel('Feeding guide', { exact: true })).toHaveValue(/দৈনিক পরিমাণ/);
  });
});

test.describe('origins (US-SHP-05, staff)', () => {
  test('staff add and edit a sourcing origin', async ({ page }) => {
    await signInAsStaff(page);
    await page.goto('/staff/shop/origins');
    await expect(page.getByRole('heading', { level: 1, name: 'Sourcing origins' })).toBeVisible();
    await page.getByRole('button', { name: 'Add origin' }).click();
    const dialog = page.getByRole('dialog', { name: 'Add an origin' });
    await dialog.getByLabel('Country').selectOption('JP');
    await dialog.getByLabel('Sourcing notes', { exact: true }).fill('Small-batch **tofu** litter.');
    await expect(dialog.getByLabel('Sourcing notes preview').locator('strong')).toHaveText('tofu');
    await dialog.getByRole('button', { name: 'Save origin' }).click();
    await expect(page.getByRole('rowheader', { name: 'Japan' })).toBeVisible();
  });
});
