import { test, expect as baseExpect, signInAsCustomer } from './fixtures';
import { addAddress, control, ids } from './ordering-helpers';

const expect = baseExpect.configure({ timeout: 20_000 });
test.describe.configure({ timeout: 120_000, retries: 1 });

/**
 * HO-ORD-002 §16 customer E-series on MSW: the cart (E-A), checkout (E-B), bank transfer (E-C) and
 * cancel (E-D1). E-A2 and E-B7 need server state shared between browser contexts, which a
 * per-tab mock cannot provide — they are recorded NOT RUN (TC-HO-ORD-002). Staff-side flows are in
 * `ordering-staff.spec.ts`.
 */

const kitten = `/shop/products/${ids.multiVariantSlug}?sku=${ids.skuKitten2kg}`;
const whiskas = `/shop/products/${ids.thaiKittenSlug}`;

async function resetMock(page: import('@playwright/test').Page) {
  await page.goto('/sign-in');
  await control(page, 'reset');
}

async function addFromProduct(page: import('@playwright/test').Page, path: string, name: RegExp) {
  await page.goto(path);
  await page.getByRole('button', { name: 'Add to cart' }).click();
  await expect(page.getByRole('status').filter({ hasText: name })).toBeVisible();
}

async function startCheckoutDelivery(page: import('@playwright/test').Page) {
  await addAddress(page);
  await page.goto('/checkout');
  await expect(
    page.getByRole('heading', { level: 1, name: /How should we get it to you/ }),
  ).toBeVisible();
}

test.describe('cart (US-ORD-01)', () => {
  test('E-A1: a guest adds two items, signs in and finds them merged with a notice, no duplicates', async ({
    page,
  }) => {
    await resetMock(page);
    await addFromProduct(page, kitten, /Added 1 × Royal Canin Kitten/);
    await addFromProduct(page, whiskas, /Added 1 ×/);

    await signInAsCustomer(page);
    await page.goto('/cart');
    await expect(page.getByRole('heading', { level: 1, name: 'Your cart' })).toBeVisible();
    await expect(
      page.getByText(/We.ve added the items from your cart before you signed in./),
    ).toBeVisible();
    const items = page.getByRole('region', { name: 'Items in your cart' }).getByRole('listitem');
    await expect(items).toHaveCount(2);

    // Adding the same item again raises its quantity; it does not create a second line.
    await page.goto(kitten);
    await page.getByRole('button', { name: 'Add to cart' }).click();
    await expect(
      page.getByRole('status').filter({ hasText: /Added 1 × Royal Canin Kitten/ }),
    ).toBeVisible();
    await page.goto('/cart');
    await expect(items).toHaveCount(2);
    await expect(
      page.getByRole('spinbutton', { name: /Quantity of Royal Canin Kitten/ }),
    ).toHaveValue('2');
  });

  test('cart: remove offers an Undo, and Checkout while signed out goes to sign-in with ?next', async ({
    page,
  }) => {
    await resetMock(page);
    await addFromProduct(page, kitten, /Added 1 × Royal Canin Kitten/);
    await page.goto('/cart');
    await page
      .getByRole('button', { name: /Remove Royal Canin Kitten/ })
      .first()
      .click();
    await expect(
      page.getByText(/Removed Royal Canin Kitten .* from your cart./).first(),
    ).toBeVisible();
    await page.getByRole('button', { name: 'Undo' }).click();
    await expect(
      page.getByRole('spinbutton', { name: /Quantity of Royal Canin Kitten/ }),
    ).toHaveValue('1');
    await page.getByRole('link', { name: /Sign in to check out/ }).click();
    await expect(page).toHaveURL(/\/sign-in\?next=%2Fcheckout/);
  });
});

test.describe('checkout (US-ORD-02 to US-ORD-08)', () => {
  test('E-B1/E-B8: COD delivery order is placed once, even on a double click', async ({ page }) => {
    await resetMock(page);
    await signInAsCustomer(page);
    await addFromProduct(page, kitten, /Added 1 × Royal Canin Kitten/);
    await startCheckoutDelivery(page);

    // Step 1: delivery to the saved address.
    await expect(page.getByLabel('Home')).toBeChecked();
    await page.getByRole('button', { name: 'Continue' }).click();
    // Step 2: service level and a time window, in branch time.
    await expect(page.getByRole('radio', { name: /Standard delivery/ })).toBeChecked();
    await page.getByRole('radiogroup', { name: 'Time window' }).getByRole('radio').first().click();
    await page.getByRole('button', { name: 'Continue' }).click();
    // Step 3: payment — three options, no card.
    await page.getByRole('radio', { name: /Cash on delivery/ }).click();
    await expect(page.getByRole('radio', { name: /Pay when you collect/ })).toBeDisabled();
    await expect(page.locator('input[autocomplete^="cc-"]')).toHaveCount(0);
    await page.getByRole('button', { name: 'Continue' }).click();
    // Step 4: review — the six rows, then place with a double click.
    const summary = page.getByRole('region', { name: 'Order summary' });
    for (const row of [
      'Items subtotal',
      'Item discounts',
      'Order discount',
      'Delivery (Standard)',
      'Tax',
      'Total',
    ]) {
      await expect(summary.getByText(row, { exact: true })).toBeVisible();
    }
    await page.getByRole('button', { name: 'Place order' }).dblclick();
    await expect(page.getByRole('heading', { level: 1, name: /^Thank you/ })).toBeVisible();
    await expect(page.getByText('Pay the courier in cash when your order arrives.')).toBeVisible();

    await page.goto('/portal/orders');
    // Seeded orders are Sam's too; exactly one new order was created (numbers from ORD-2026-000200).
    await expect(page.getByRole('link', { name: /ORD-2026-000200/ })).toBeVisible();
    await expect(page.getByRole('link', { name: /ORD-2026-000201/ })).toHaveCount(0);
  });

  test('E-B2: a price change is listed and must be acknowledged before placing', async ({
    page,
  }) => {
    await resetMock(page);
    await signInAsCustomer(page);
    await addFromProduct(page, kitten, /Added 1 × Royal Canin Kitten/);
    await control(page, 'setPrice', ids.skuKitten2kg, '3000.00');
    await startCheckoutDelivery(page);
    await page.getByRole('button', { name: 'Continue' }).click();
    await page.getByRole('radiogroup', { name: 'Time window' }).getByRole('radio').first().click();
    await page.getByRole('button', { name: 'Continue' }).click();
    await page.getByRole('radio', { name: /Cash on delivery/ }).click();
    await page.getByRole('button', { name: 'Continue' }).click();

    const panel = page.getByRole('region', { name: 'What changed since you added these items' });
    await expect(panel.getByText(/The price of 'Royal Canin Kitten' has increased/)).toBeVisible();
    const place = page.getByRole('button', { name: 'Place order' });
    await expect(place).toBeDisabled();
    await panel.getByLabel(/I.ve reviewed these changes./).click();
    await expect(place).toBeEnabled();
    await place.click();
    await expect(page.getByRole('heading', { level: 1, name: /^Thank you/ })).toBeVisible();
  });

  test('E-B3: an address outside every zone offers collection instead, and pickup completes', async ({
    page,
  }) => {
    await resetMock(page);
    await signInAsCustomer(page);
    await addFromProduct(page, kitten, /Added 1 × Royal Canin Kitten/);
    await addAddress(page, { label: 'Gazipur home', district: 'Gazipur', postalCode: '1700' });
    await page.goto('/checkout');
    await expect(
      page.getByText(/We don.t deliver to Gazipur yet. You can collect your order instead./),
    ).toBeVisible();
    await expect(page.getByRole('radio', { name: /Collect from a branch/ })).toBeChecked();
    await expect(page.getByRole('radio', { name: /PurrCare Dhanmondi/ })).toBeChecked();
    await page.getByRole('button', { name: 'Continue' }).click();
    // Slot step is not needed for collection; payment is next.
    await page.getByRole('radio', { name: /Pay when you collect/ }).click();
    await expect(page.getByRole('radio', { name: /Cash on delivery/ })).toBeDisabled();
    await page.getByRole('button', { name: 'Continue' }).click();
    await page.getByRole('button', { name: 'Place order' }).click();
    await expect(page.getByRole('heading', { level: 1, name: /^Thank you/ })).toBeVisible();
    await expect(page.getByText(/Collect and pay at PurrCare Dhanmondi\./)).toBeVisible();
  });

  test('E-B4/E-C1: COD is disabled above the ceiling with its reason; bank transfer shows instructions', async ({
    page,
  }) => {
    await resetMock(page);
    await signInAsCustomer(page);
    // 4 × BDT 2,450 is above the BDT 5,000 COD ceiling.
    await page.goto(kitten);
    await page.getByLabel('Quantity').fill('4');
    await page.getByRole('button', { name: 'Add to cart' }).click();
    await expect(page.getByRole('status').filter({ hasText: /Added 4 ×/ })).toBeVisible();
    await startCheckoutDelivery(page);
    await page.getByRole('button', { name: 'Continue' }).click();
    await page.getByRole('radiogroup', { name: 'Time window' }).getByRole('radio').first().click();
    await page.getByRole('button', { name: 'Continue' }).click();
    const cod = page.getByRole('radio', { name: /Cash on delivery/ });
    await expect(cod).toBeDisabled();
    await expect(
      page.getByText(/Cash on delivery is available for orders up to BDT 5,000\.00/),
    ).toBeVisible();
    await page.getByRole('radio', { name: /Bank transfer/ }).click();
    await page.getByRole('button', { name: 'Continue' }).click();
    await page.getByRole('button', { name: 'Place order' }).click();

    await expect(page.getByRole('heading', { name: 'Pay by bank transfer' })).toBeVisible();
    await expect(page.getByText(/^ORD-2026-\d+$/).first()).toBeVisible();
    await expect(page.getByRole('timer')).toContainText('Time left');
    await expect(page.getByRole('button', { name: 'Copy account number' })).toBeVisible();
    await page.getByRole('button', { name: 'I’ve made the transfer' }).click();
    await page.getByLabel('Your bank’s reference').fill('TRF-77-A');
    await page.getByRole('button', { name: 'Send reference' }).click();
    await expect(page.getByText(/we.ll confirm within one working day/i)).toBeVisible();
  });

  test('E-B5: an unknown discount code shows the specific reason; a valid code shows the order discount', async ({
    page,
  }) => {
    await resetMock(page);
    await signInAsCustomer(page);
    await addFromProduct(page, kitten, /Added 1 × Royal Canin Kitten/);
    await startCheckoutDelivery(page);
    await page.getByRole('button', { name: 'Continue' }).click();
    await page.getByRole('radiogroup', { name: 'Time window' }).getByRole('radio').first().click();
    await page.getByRole('button', { name: 'Continue' }).click();
    await page.getByRole('radio', { name: /Cash on delivery/ }).click();
    await page.getByRole('button', { name: 'Continue' }).click();

    await page.getByLabel('Discount code', { exact: true }).fill('NOPE');
    await page.getByRole('button', { name: 'Apply' }).click();
    await expect(page.getByText(/We don.t recognise that code./)).toBeVisible();
    await page.getByRole('button', { name: /Remove discount code NOPE/ }).click();

    await page.getByLabel('Discount code', { exact: true }).fill('SAVE100');
    await page.getByRole('button', { name: 'Apply' }).click();
    await expect(page.getByText(/Spend BDT 5,000\.00 or more to use SAVE100\./)).toBeVisible();
    await page.getByRole('button', { name: /Remove discount code SAVE100/ }).click();

    await page.getByLabel('Discount code', { exact: true }).fill('WELCOME10');
    await page.getByRole('button', { name: 'Apply' }).click();
    await expect(page.getByText('Order discount (WELCOME10)')).toBeVisible();
  });

  test('E-B6: a prescription diet needs an authorised cat; a lapsed authorisation blocks the line', async ({
    page,
  }) => {
    await resetMock(page);
    await signInAsCustomer(page);
    await page.goto(`/shop/products/${ids.rxEligibleSlug}`);
    await page.getByRole('button', { name: 'Add to cart' }).click();
    const dialog = page.getByRole('dialog', { name: 'Which cat is this for?' });
    await expect(dialog.getByRole('radio', { name: 'Biscuit' })).toBeDisabled();
    await expect(dialog.getByText('No current authorisation for this food.').first()).toBeVisible();
    await dialog.getByRole('radio', { name: 'Whiskers' }).click();
    await dialog.getByRole('button', { name: 'Add to cart' }).click();
    await expect(page.getByRole('status').filter({ hasText: /Added 1 ×/ })).toBeVisible();

    await addAddress(page);
    await page.goto('/checkout');
    await page.getByRole('button', { name: 'Continue' }).click();
    await page.getByRole('radiogroup', { name: 'Time window' }).getByRole('radio').first().click();
    await page.getByRole('button', { name: 'Continue' }).click();
    await page.getByRole('radio', { name: /Cash on delivery/ }).click();
    await page.getByRole('button', { name: 'Continue' }).click();
    await expect(page.getByText('For Whiskers').first()).toBeVisible();
    await page.getByRole('button', { name: 'Place order' }).click();
    await expect(page.getByRole('heading', { level: 1, name: /^Thank you/ })).toBeVisible();

    // The authorisation lapses: the next attempt leaves the line out and says why.
    await control(page, 'lapseAuthorisation', ids.catAllergy);
    await page.goto(`/shop/products/${ids.rxEligibleSlug}`);
    await page.getByRole('button', { name: 'Add to cart' }).click();
    const again = page.getByRole('dialog', { name: 'Which cat is this for?' });
    await again.getByRole('radio', { name: 'Whiskers' }).click();
    await again.getByRole('button', { name: 'Add to cart' }).click();
    await expect(again.getByRole('alert')).toContainText(/no current vet authorisation/i);
  });
});

test.describe('bank transfer expiry and cancel (US-ORD-12, US-ORD-19)', () => {
  test('E-C2: an unconfirmed transfer expires; the order explains it and Order again fills the cart', async ({
    page,
  }) => {
    await resetMock(page);
    await signInAsCustomer(page);
    await control(page, 'setTransferWindowSeconds', 3);
    await addFromProduct(page, kitten, /Added 1 × Royal Canin Kitten/);
    await startCheckoutDelivery(page);
    await page.getByRole('button', { name: 'Continue' }).click();
    await page.getByRole('radiogroup', { name: 'Time window' }).getByRole('radio').first().click();
    await page.getByRole('button', { name: 'Continue' }).click();
    await page.getByRole('radio', { name: /Bank transfer/ }).click();
    await page.getByRole('button', { name: 'Continue' }).click();
    await page.getByRole('button', { name: 'Place order' }).click();
    await expect(page.getByRole('heading', { name: 'Pay by bank transfer' })).toBeVisible();
    await page.waitForTimeout(3500);
    await page.getByRole('link', { name: 'View your order' }).click();
    await expect(page.getByRole('heading', { name: 'This order has expired' })).toBeVisible();
    await expect(
      page.getByText(/We couldn.t confirm your transfer in time, so the items were released./),
    ).toBeVisible();
    await page.getByRole('button', { name: 'Order again' }).click();
    await expect(page.getByText(/1 item was added to your cart\./)).toBeVisible();
    await page.getByRole('link', { name: 'Go to your cart' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Your cart' })).toBeVisible();
    await expect(
      page.getByRole('spinbutton', { name: /Quantity of Royal Canin Kitten/ }),
    ).toBeVisible();
  });

  test('E-D1: cancel before packing asks to confirm; a packed order offers the contact card instead', async ({
    page,
  }) => {
    await resetMock(page);
    await signInAsCustomer(page);
    await page.goto('/portal/orders');
    await page.getByRole('link', { name: /ORD-2026-000104/ }).click();
    await page.getByRole('button', { name: 'Cancel order' }).click();
    const dialog = page.getByRole('dialog', { name: 'Cancel order ORD-2026-000104?' });
    await expect(dialog.getByText(/Your reserved items are released\./)).toBeVisible();
    await dialog.getByRole('button', { name: 'Cancel order' }).click();
    await expect(page.getByText('Cancelled').first()).toBeVisible();

    await page.goto('/portal/orders');
    await page.getByRole('link', { name: /ORD-2026-000106/ }).click();
    // Cancel stays on the page, disabled, with the reason and the branch contact beside it.
    await expect(page.getByRole('button', { name: 'Cancel order' })).toBeDisabled();
    await expect(page.getByRole('heading', { name: 'Contact the shop' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Track on the courier’s site' })).toBeVisible();
    await expect(page.getByText('PC-88213004').first()).toBeVisible();
  });
});
