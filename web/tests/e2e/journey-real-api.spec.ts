import {
  test,
  expect as baseExpect,
  type Browser,
  type BrowserContext,
  type Page,
} from '@playwright/test';

/**
 * The end-to-end customer and staff journey against the REAL API (no MSW). It exists because screen tests run against
 * mocks whose JSON drifted from the real API, and nobody had walked shop -> cart -> sign-up/sign-in -> checkout -> order
 * on the real backend. Each step asserts what a person would see.
 *
 * Skipped unless `JOURNEY_REAL_API=1`. Do not run it by hand: `npm run test:e2e:journey` (scripts/run-journey-e2e.mjs)
 * seeds a scratch database, starts the API on :5098 and the web app on :3098 from a private copy, runs this file with
 * --workers=1, and stops both. A journey registers a customer and places an order, so it must never touch a shared
 * database; playwright's global setup already refuses port 3000 and an API on catecommerce_dev.
 *
 * Environment: JOURNEY_CUSTOMER_EMAIL / JOURNEY_CUSTOMER_PASSWORD (the seeded Ayesha Rahman, with her saved Dhaka
 * address), E2E_STAFF_ADMIN / E2E_STAFF_PASSWORD (e2e.admin), NEXT_PUBLIC_API_BASE_URL (the API), and optionally
 * JOURNEY_CAPTCHA_ANSWER (default TEST1: the seeding API runs with Captcha:TestAnswer=TEST1).
 *
 * The registration endpoint is rate limited (5 per 10 minutes per IP in production); the journey registers exactly one
 * new customer per run, with a unique email.
 *
 * Also fails on any browser console error and any /api/ response >= 500 seen during the journey. A console line that is
 * only the browser reporting a 4xx on a fetch ("Failed to load resource ... 401") is not counted: a signed-out visitor
 * legitimately gets a 401 from the session check, and the 5xx rule covers the server-fault cases.
 */

const expect = baseExpect.configure({ timeout: 30_000 });
const ENABLED = process.env.JOURNEY_REAL_API === '1';
const API = (process.env.NEXT_PUBLIC_API_BASE_URL ?? '').replace(/\/+$/, '');
const CUSTOMER = {
  email: process.env.JOURNEY_CUSTOMER_EMAIL ?? '',
  password: process.env.JOURNEY_CUSTOMER_PASSWORD ?? '',
};
const STAFF = {
  identifier: process.env.E2E_STAFF_ADMIN ?? '',
  password: process.env.E2E_STAFF_PASSWORD ?? '',
};
const CAPTCHA_ANSWER = process.env.JOURNEY_CAPTCHA_ANSWER ?? 'TEST1';
const SEEDED_BRANDS = ['Whiskas', 'Royal Canin', 'Me-O', 'Purina Friskies', 'Catsan', 'Kit Cat'];

test.skip(!ENABLED, 'Set JOURNEY_REAL_API=1 (use npm run test:e2e:journey).');
test.describe.configure({ mode: 'serial', timeout: 180_000 });
test.use({ actionTimeout: 30_000 });

// ---- problems seen anywhere in the journey -------------------------------------------------------------------

const problems: string[] = [];
// APP BUG (reported): every staff page logs React Query's "No queryFn was passed ... overdue-banner-dismissed" error
// (lib/hooks/useOperations.ts reads that key with useQuery and no queryFn). Kept apart so it cannot hide other errors;
// the last test asserts it is empty and is marked test.fail() until the app is fixed.
const knownQueryFnErrors: string[] = [];
const isKnownQueryFnError = (text: string) =>
  text.includes('overdue-banner-dismissed') && text.includes('No queryFn was passed');

function watch(page: Page, label: string) {
  page.on('console', (message) => {
    if (message.type() !== 'error') return;
    const text = message.text();
    if (/Failed to load resource: the server responded with a status of 4\d\d/.test(text)) return;
    if (isKnownQueryFnError(text)) {
      knownQueryFnErrors.push(`[${label}] ${text.slice(0, 120)}`);
      return;
    }
    problems.push(`[${label}] console error: ${text.slice(0, 300)}`);
  });
  page.on('pageerror', (error) =>
    problems.push(`[${label}] uncaught page error: ${error.message.slice(0, 300)}`),
  );
  page.on('response', (response) => {
    if (response.status() >= 500 && response.url().includes('/api/')) {
      problems.push(
        `[${label}] ${response.status()} from ${response.request().method()} ${response.url()}`,
      );
    }
  });
}

async function newPage(
  browser: Browser,
  label: string,
): Promise<{ context: BrowserContext; page: Page }> {
  const context = await browser.newContext();
  // Records whether the top progress bar ever appeared (it can be on screen for only a moment).
  await context.addInitScript(() => {
    (window as unknown as { __progressSeen: boolean }).__progressSeen = false;
    const look = () => {
      if (document.querySelector('[data-testid="navigation-progress"]')) {
        (window as unknown as { __progressSeen: boolean }).__progressSeen = true;
      }
    };
    new MutationObserver(look).observe(document, {
      childList: true,
      subtree: true,
      attributes: true,
    });
  });
  const page = await context.newPage();
  watch(page, label);
  return { context, page };
}

async function expectNoErrorState(page: Page) {
  await expect(page.getByText(/couldn.t load/i)).toHaveCount(0);
  await expect(page.getByText(/something went wrong/i)).toHaveCount(0);
}

async function acceptPoliciesIfAsked(page: Page) {
  const dialog = page.getByRole('dialog', { name: 'Updated policies' });
  if (await dialog.isVisible().catch(() => false)) {
    for (let guard = 0; guard < 5 && (await dialog.isVisible().catch(() => false)); guard++) {
      await dialog.getByRole('button', { name: 'Accept' }).first().click();
      await page.waitForTimeout(500);
    }
  }
}

/** Fills a sign-in form once the page has hydrated (a fill that lands before hydration is wiped by React). */
async function fillSignIn(page: Page, identifier: string, password: string) {
  await page.waitForLoadState('networkidle');
  const id = page.getByLabel(/Mobile number or email/);
  await expect(async () => {
    await id.fill(identifier);
    await page.getByLabel(/^Password/).fill(password);
    await expect(id).toHaveValue(identifier);
  }).toPass({ timeout: 30_000 });
}

async function signInCustomer(page: Page, email: string, password: string, next: string) {
  await page.goto(`/sign-in?next=${encodeURIComponent(next)}`);
  await fillSignIn(page, email, password);
  await page.getByRole('button', { name: 'Sign in' }).click();
}

// ---- 1-3: a new visitor ---------------------------------------------------------------------------------------

test.describe('a new visitor', () => {
  let visitor: Page;
  let visitorContext: BrowserContext;
  const email = `journey.${Date.now()}@purrcare.test`;
  const password = 'Journey-Visitor-2026';

  test.beforeAll(async ({ browser }) => {
    ({ context: visitorContext, page: visitor } = await newPage(browser, 'visitor'));
  });
  test.afterAll(async () => {
    await visitorContext?.close();
  });

  test('1. guest shop: one banner, Cart in the header, seeded brands, progress bar, add to cart', async () => {
    await visitor.goto('/shop');
    await expect(visitor.getByRole('heading', { level: 1 }).first()).toBeVisible();

    // Exactly one banner (a duplicate strip was one of the owner's bugs).
    await expect(visitor.getByRole('region', { name: 'Announcements' })).toHaveCount(1);
    await expect(visitor.getByText('Free delivery in Dhaka on orders over ৳2,000')).toHaveCount(1);

    // The header carries the Cart link for a guest.
    await expect(visitor.getByRole('banner').getByRole('link', { name: /^Cart/ })).toBeVisible();

    // Following a link shows the top progress bar. Wait for hydration first: a click before React has attached the
    // listener is a plain full-page load (the browser shows its own loading indicator then).
    await visitor.waitForLoadState('networkidle');
    const catalogueLink = visitor
      .getByRole('link', { name: /all products|shop all|browse/i })
      .first();
    const target =
      (await catalogueLink.count()) > 0
        ? catalogueLink
        : visitor.locator('a[href^="/shop/products"]').first();
    await target.click();
    await expect(visitor).toHaveURL(/\/shop\/products/);
    await expect
      .poll(() =>
        visitor.evaluate(() => (window as unknown as { __progressSeen: boolean }).__progressSeen),
      )
      .toBe(true);

    // The brand filter lists the seeded brands, and only those.
    const brandGroup = visitor.getByRole('group', { name: 'Brand' }).first();
    await expect(brandGroup).toBeVisible();
    const showAll = brandGroup.getByRole('button', { name: /Show all \d+ brands/ });
    if (await showAll.isVisible().catch(() => false)) await showAll.click();
    for (const brand of SEEDED_BRANDS) {
      await expect(
        brandGroup.getByRole('checkbox', { name: new RegExp(`^${brand}`) }),
      ).toBeVisible();
    }
    await expect(brandGroup.getByRole('checkbox')).toHaveCount(SEEDED_BRANDS.length);

    // Open a product and add it to the cart: the count appears in the header.
    await visitor
      // Anchored: the card's guest wishlist heart is also a link whose name mentions the product.
      .getByRole('link', { name: /^Whiskas Adult Ocean Fish/ })
      .first()
      .click();
    await expect(visitor).toHaveURL(/\/shop\/products\/whiskas-adult-ocean-fish-dry/);
    await expect(
      visitor.getByRole('heading', { level: 1, name: /Whiskas Adult Ocean Fish/ }),
    ).toBeVisible();
    await visitor.getByRole('button', { name: 'Add to cart' }).click();
    await expect(visitor.getByRole('status').filter({ hasText: /Added 1 ×/ })).toBeVisible();
    await expect(visitor.getByTestId('cart-count').first()).toHaveText('1');
  });

  test('2. checkout as a new visitor: sign-in with ?next, create an account, sign in, land on checkout', async () => {
    await visitor.getByRole('banner').getByRole('link', { name: /^Cart/ }).click();
    await expect(visitor.getByRole('heading', { level: 1, name: 'Your cart' })).toBeVisible();
    await expect(
      visitor.getByRole('region', { name: 'Items in your cart' }).getByRole('listitem'),
    ).toHaveCount(1);
    // Signed out, the Checkout pill reads "Sign in to check out" and goes to sign-in first.
    await visitor.getByRole('link', { name: /Sign in to check out/ }).click();
    await expect(visitor).toHaveURL(/\/sign-in\?next=%2Fcheckout/);

    // "Create an account" keeps the destination.
    const create = visitor.getByRole('link', { name: 'Create an account' });
    await expect(create).toBeVisible();
    await expect(create).toHaveAttribute('href', /next=(%2F|\/)checkout/);
    await create.click();
    await expect(visitor).toHaveURL(/\/register\?next=/);

    // Wait for hydration and the security-check image before typing: a fill that lands first is wiped, and in dev
    // (Strict Mode) the challenge can be fetched twice, so check every value is still there before submitting.
    await visitor.waitForLoadState('networkidle');
    await expect(visitor.getByAltText(/Security check/)).toBeVisible();
    await expect(async () => {
      await visitor.getByLabel(/Full name/).fill('Journey Visitor');
      await visitor.getByLabel(/^Email/).fill(email);
      await visitor.getByLabel(/^Password/).fill(password);
      await visitor.getByLabel(/Security check/).fill(CAPTCHA_ANSWER);
      await expect(visitor.getByLabel(/Security check/)).toHaveValue(CAPTCHA_ANSWER, {
        timeout: 1000,
      });
      await expect(visitor.getByLabel(/^Password/)).toHaveValue(password, { timeout: 1000 });
    }).toPass({ timeout: 15_000 });
    await visitor.getByRole('button', { name: 'Create account' }).click();
    // Owner, 2026-10-19: the new customer is signed in straight away and sent on to checkout (no second sign-in),
    // with the guest cart merged in.
    await expect(visitor).toHaveURL(/\/checkout/);
    await expect(
      visitor.getByRole('heading', { level: 1, name: /How should we get it to you/ }),
    ).toBeVisible();
    await expect(visitor.getByRole('list', { name: 'Checkout steps' })).toBeVisible();
  });

  test('3. policy popup: Privacy Policy, "Read the full text" opens the real text, Accept closes it', async () => {
    const dialog = visitor.getByRole('dialog', { name: 'Updated policies' });
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText('Privacy Policy')).toBeVisible();

    const link = dialog.getByRole('link', { name: 'Read the full text' });
    await expect(link).toHaveAttribute('href', '/policies/privacy');
    const policyPage = await visitorContext.newPage();
    watch(policyPage, 'policy page');
    await policyPage.goto('/policies/privacy');
    await expect(policyPage.getByRole('heading', { name: 'Privacy Policy' }).first()).toBeVisible();
    await expect(policyPage.getByText(/respects your privacy/)).toBeVisible();
    await policyPage.close();

    await dialog.getByRole('button', { name: 'Accept' }).click();
    await expect(dialog).toBeHidden();
  });
});

// ---- 4: the seeded customer places a delivery order -----------------------------------------------------------

let orderNumber = '';

test.describe('the seeded customer', () => {
  let page: Page;
  let context: BrowserContext;

  test.beforeAll(async ({ browser }) => {
    ({ context, page } = await newPage(browser, 'customer'));
  });
  test.afterAll(async () => {
    await context?.close();
  });

  test('4. delivery checkout: Dhaka address, Standard, cash on delivery, priced review, order placed and listed', async () => {
    await signInCustomer(page, CUSTOMER.email, CUSTOMER.password, '/cart');
    await expect(page).toHaveURL(/\/cart/);
    await acceptPoliciesIfAsked(page);

    // Start from an empty cart (an earlier run may have left items, and the delivery fee depends on the total).
    const csrf = async () =>
      (await context.cookies()).find((c) => c.name === 'pc_csrf')?.value ?? '';
    const cart = (await (await context.request.get(`${API}/api/cart`)).json()) as {
      lines: Array<{ lineId: string }>;
    };
    for (const line of cart.lines) {
      const removed = await context.request.delete(`${API}/api/cart/lines/${line.lineId}`, {
        headers: { 'X-CSRF-Token': await csrf() },
      });
      expect(removed.status()).toBeLessThan(300);
    }

    // Add one ৳850 item through the product page.
    await page.goto('/shop/products/catsan-hygiene-plus-litter');
    await expect(
      page.getByRole('heading', { level: 1, name: /Catsan Hygiene Plus Litter/ }),
    ).toBeVisible();
    await page.getByRole('button', { name: 'Add to cart' }).click();
    await expect(page.getByRole('status').filter({ hasText: /Added 1 ×/ })).toBeVisible();
    await page.goto('/cart');
    await expect(
      page.getByRole('region', { name: 'Items in your cart' }).getByRole('listitem'),
    ).toHaveCount(1);
    await page.getByRole('link', { name: 'Checkout' }).click();
    await expect(
      page.getByRole('heading', { level: 1, name: /How should we get it to you/ }),
    ).toBeVisible();
    await expect(page.getByRole('list', { name: 'Checkout steps' })).toBeVisible();
    await acceptPoliciesIfAsked(page);

    // Delivery -> the saved Dhaka address.
    await expect(page.getByLabel(/Home/)).toBeChecked();
    await page.getByRole('button', { name: 'Continue' }).click();

    // Standard delivery, and a delivery window if asked.
    await expect(page.getByRole('radio', { name: /Standard delivery/ })).toBeChecked();
    const windows = page.getByRole('radiogroup', { name: 'Time window' });
    if (await windows.isVisible().catch(() => false))
      await windows.getByRole('radio').first().click();
    await page.getByRole('button', { name: 'Continue' }).click();

    // Cash on delivery.
    await page.getByRole('radio', { name: /Cash on delivery/ }).click();
    await page.getByRole('button', { name: 'Continue' }).click();

    // Review: every line priced, the six summary rows, delivery ৳60 below ৳2,000, tax ৳0.00.
    const lines = page.getByRole('list', { name: 'Items in this order' }).getByRole('listitem');
    await expect(lines).toHaveCount(1);
    for (const line of await lines.all()) await expect(line).toContainText(/৳\s?[\d,]+\.\d{2}/);
    const summary = page.getByRole('region', { name: 'Order summary' });
    await expect(page.getByText('Updating your total…')).toHaveCount(0);
    const rowValue = async (label: string | RegExp) =>
      (
        await summary
          .locator('dt', { hasText: label })
          .locator('xpath=following-sibling::dd')
          .first()
          .locator('[aria-hidden="true"]')
          .innerText()
      ).trim();
    const toNumber = (text: string) => Number(text.replace(/[^\d.]/g, ''));
    const subtotal = toNumber(await rowValue('Items subtotal'));
    expect(subtotal).toBeGreaterThan(0);
    expect(subtotal).toBeLessThan(2000);
    expect(await rowValue(/^Delivery \(Standard\)$/)).toMatch(/৳60(\.00)?$/);
    expect(await rowValue(/^Tax$/)).toMatch(/৳0(\.00)?$/);
    expect(toNumber(await rowValue(/^Total$/))).toBe(subtotal + 60);

    await page.getByRole('button', { name: 'Place order' }).click();
    await expect(page.getByRole('heading', { level: 1, name: /^Thank you/ })).toBeVisible();
    // The order number is in the hero's sentence: "Order ORD-... is placed."
    const placed = page.getByText(/Order ORD-[\w-]+ is placed\./);
    await expect(placed).toBeVisible();
    orderNumber = ((await placed.innerText()).match(/ORD-[\w-]+/) ?? [''])[0];
    expect(orderNumber).not.toBe('');

    await page.goto('/portal/orders');
    await expect(page.getByRole('link', { name: new RegExp(orderNumber) })).toBeVisible();
  });
});

// ---- 5: staff -------------------------------------------------------------------------------------------------

test.describe('staff', () => {
  let page: Page;
  let context: BrowserContext;

  test.beforeAll(async ({ browser }) => {
    ({ context, page } = await newPage(browser, 'staff'));
  });
  test.afterAll(async () => {
    await context?.close();
  });

  test('5a. staff sign-in lands on the staff home', async () => {
    await page.goto('/staff/sign-in');
    await fillSignIn(page, STAFF.identifier, STAFF.password);
    await page.getByRole('button', { name: 'Sign in' }).click();
    await expect(page).toHaveURL(/\/staff\/?$/);
    await expect(page.getByRole('heading', { level: 1, name: 'Today' })).toBeVisible();
    await expectNoErrorState(page);
  });

  test('5b. the order just placed is in the staff order list', async () => {
    expect(orderNumber, 'step 4 must have placed an order').not.toBe('');
    await page.goto('/staff/orders');
    await expect(page.getByText(orderNumber).first()).toBeVisible();
    await expectNoErrorState(page);
  });

  test('5c. delivery zones list Dhaka', async () => {
    await page.goto('/staff/admin/delivery-zones');
    await expect(page.getByRole('heading', { name: 'Delivery zones' }).first()).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Dhaka' }).first()).toBeVisible();
    await expectNoErrorState(page);
  });

  test('5d. tax classes load with the Standard class', async () => {
    await page.goto('/staff/admin/tax-classes');
    await expect(page.getByRole('heading', { level: 1, name: 'Tax classes' })).toBeVisible();
    await expect(page.getByText('Standard').first()).toBeVisible();
    await expectNoErrorState(page);
  });

  // Fixed 2026-10-15: GET /api/admin/policies now sends each version's `body`, which the policy detail screen renders
  // (it used to crash, "Application error: a client-side exception"; the mock sent `body`, so no screen test saw it).
  test('5e. policies load, and the privacy policy opens with its text', async () => {
    await page.goto('/staff/admin/policies');
    await expect(page.getByRole('heading', { level: 1, name: 'Policies' })).toBeVisible();
    await expect(page.getByText('Privacy Policy').first()).toBeVisible();
    await expectNoErrorState(page);
    await page
      .getByRole('link', { name: /Privacy Policy/ })
      .first()
      .click();
    await expect(page.getByRole('heading', { name: /Privacy Policy/ }).first()).toBeVisible();
    // The version's own text must be shown, not an empty box (each version's text sits behind "Read text").
    await page.getByText('Read text').first().click();
    await expect(page.getByText(/respects your privacy/).first()).toBeVisible();
  });

  test('5f. branches load with Main clinic', async () => {
    await page.goto('/staff/admin/branches');
    await expect(page.getByText('Main clinic').first()).toBeVisible();
    await expectNoErrorState(page);
  });
});

test('no console errors (other than the known one below) and no 5xx from the API during the whole journey', () => {
  expect(problems).toEqual([]);
});

test('no "No queryFn was passed" console error from the staff overdue-restore banner', () => {
  expect(knownQueryFnErrors).toEqual([]);
});
