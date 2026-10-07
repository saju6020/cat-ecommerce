import { shopFixtureIds } from '../../mocks/handlers/shop';
import { test, expect as baseExpect, signInAsCustomer } from './fixtures';
// The first visit to each route compiles it in `next dev` (slow under load): allow for that.
const expect = baseExpect.configure({ timeout: 20_000 });
test.describe.configure({ timeout: 90_000 });

/** HO-SHP-002 §16 E-D1, E-D3 and the review journey (US-SHP-09) — run against MSW. E-D2 needs the
 *  real API's workers and is recorded not runnable (TC-HO-SHP-002). */

test.describe('wishlist and stock alerts (US-SHP-13)', () => {
  test('E-D1: save to the wishlist; request alerts with and without marketing consent', async ({
    page,
  }) => {
    await signInAsCustomer(page);
    await page.goto(
      `/shop/products/${shopFixtureIds.multiVariantSlug}?sku=${shopFixtureIds.skuKitten2kg}`,
    );

    const buyBox = page.getByRole('region', { name: 'Price and availability' });
    await buyBox.getByRole('button', { name: 'Save' }).click();
    // The label carries the state (no aria-pressed as well: it announced the state twice, S15).
    const saved = buyBox.getByRole('button', { name: 'Saved' });
    await expect(saved).toBeVisible();
    await expect(saved).not.toHaveAttribute('aria-pressed');

    // An out-of-stock size whose alert WILL be delivered: names the channels.
    await page.getByRole('radio', { name: /4 kg/ }).check();
    // The seeded session already has a pending alert here; cancel it, then ask again.
    await buyBox.getByRole('button', { name: 'Cancel alert' }).click();
    await buyBox.getByRole('button', { name: /Tell me when it.s back/ }).click();
    await expect(buyBox.getByRole('status')).toContainText(
      /We.ll send one message by in-app notification and SMS/,
    );

    // A customer who agreed to nothing: the alert will NOT be delivered, and the page says so.
    await page.goto(`/shop/products/${shopFixtureIds.noConsentSlug}`);
    await page.getByRole('button', { name: /Tell me when it.s back/ }).click();
    const status = page.getByRole('status').filter({ hasText: /haven.t agreed to receive offers/ });
    await expect(status).toBeVisible();
    await expect(status.getByRole('link', { name: 'notification settings' })).toHaveAttribute(
      'href',
      '/portal/account/notifications',
    );

    // The wishlist shows the saved item, and the alerts show their status.
    await page.goto('/portal/wishlist');
    await expect(page.getByRole('heading', { level: 1, name: 'Your wishlist' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Royal Canin Kitten' }).first()).toBeVisible();
    await expect(page.getByText('No longer sold', { exact: true })).toBeVisible();
    await expect(page.getByText('Waiting').first()).toBeVisible();
    await expect(page.getByText(/Notified on 3 Oct 2026/)).toBeVisible();
    await page.getByRole('button', { name: /Remove Royal Canin Kitten \(2 kg\)/ }).click();
    await expect(
      page.getByRole('button', { name: /Remove Royal Canin Kitten \(2 kg\)/ }),
    ).toHaveCount(0);
  });
});

test.describe('food suggestions (US-SHP-11)', () => {
  test('E-D3: a cat with a chicken allergy never sees chicken products and is told why', async ({
    page,
  }) => {
    await signInAsCustomer(page);
    await page.goto(`/portal/cats/${shopFixtureIds.catAllergy}/food`);
    await expect(
      page.getByRole('heading', { level: 1, name: 'Food suggestions for Whiskers' }),
    ).toBeVisible();
    await expect(
      page.getByText(/We left out \d+ products? containing chicken \(recorded allergy\)\./),
    ).toBeVisible();
    const cards = page.getByRole('article');
    await expect(cards.first()).toBeVisible();
    await expect(page.getByText('Purina Pro Plan Adult Chicken')).toHaveCount(0);
    await expect(page.getByText('Reflex Adult Chicken')).toHaveCount(0);
    await expect(page.getByText('Suits adult cats').first()).toBeVisible();
    await expect(page.getByText(/General guidance/)).toBeVisible();
  });

  test('E-D3: a cat with no date of birth gets the add-date hint; the profile links here', async ({
    page,
  }) => {
    await signInAsCustomer(page);
    await page.goto(`/portal/cats/${shopFixtureIds.catNoDob}/food`);
    await expect(page.getByRole('link', { name: /Go to Mittens.s profile/ })).toBeVisible();
    await page.goto(`/portal/cats/${shopFixtureIds.catNoDob}`);
    await page.getByRole('link', { name: 'Food suggestions' }).click();
    await expect(page).toHaveURL(/\/food$/);
  });
});

test.describe('reviews (US-SHP-09)', () => {
  test('a customer who bought the product writes a review that waits for approval', async ({
    page,
  }) => {
    await signInAsCustomer(page);
    await page.goto(`/shop/products/${shopFixtureIds.reviewableSlug}`);
    await page.getByRole('link', { name: 'Write a review' }).click();
    await expect(page).toHaveURL(/\/portal\/reviews\/new\?product=royal-canin-kitten/);
    await page.getByRole('radio', { name: '5 out of 5' }).check();
    await page.getByLabel(/^Title/).fill('Shiny coat');
    await page.getByLabel('Your review').fill('She eats it all. <b>Great</b>.');
    await page.getByRole('button', { name: 'Send review' }).click();

    await expect(page).toHaveURL(/\/portal\/reviews\?submitted=1/);
    await expect(
      page.getByText("Thanks — your review will appear once it's approved."),
    ).toBeVisible();
    // Plain text: the markup is shown literally.
    await expect(page.getByText('She eats it all. <b>Great</b>.')).toBeVisible();
    await expect(page.getByText('Awaiting approval').first()).toBeVisible();
    await expect(page.getByText('Not published')).toBeVisible();
  });

  test('a product the customer has not bought says why it cannot be reviewed', async ({ page }) => {
    await signInAsCustomer(page);
    await page.goto('/shop/products/animonda-carny-adult-beef');
    await expect(
      page.getByText('Only customers who bought this product can review it.'),
    ).toBeVisible();
  });
});
