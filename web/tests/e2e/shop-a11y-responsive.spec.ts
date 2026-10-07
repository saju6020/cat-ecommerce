import AxeBuilder from '@axe-core/playwright';
import type { Page } from '@playwright/test';
import { shopFixtureIds } from '../../mocks/handlers/shop';
import { test, expect, signInAsCustomer, signInAsStaff } from './fixtures';
import { isMobile } from './shop-helpers';

/**
 * HO-SHP-002 §16 E-X1 (axe on every §8.1 route, including the open filter sheet, the prescription
 * gate, the review form and the editor with errors) and E-X2 (every storefront route at
 * 360 / 768 / 1024 / 1440 px without horizontal scroll), run against MSW.
 */

interface RouteCase {
  path: string;
  audience: 'none' | 'customer' | 'staff';
  heading: RegExp;
}

const p = shopFixtureIds;
const EDITOR = `/staff/shop/products/${p.multiVariantProductId}`;

const ROUTES: RouteCase[] = [
  { path: '/shop', audience: 'none', heading: /everything your cat needs/i },
  { path: '/shop/products', audience: 'none', heading: /all products/i },
  { path: '/shop/products?q=zzqqxx', audience: 'none', heading: /results for/i },
  {
    path: '/shop/products?brand=Whiskas&availability=inStock&sort=price_asc',
    audience: 'none',
    heading: /all products/i,
  },
  { path: '/shop/countries', audience: 'none', heading: /shop by country/i },
  { path: '/shop/countries/TH', audience: 'none', heading: /thailand/i },
  {
    path: `/shop/products/${p.multiVariantSlug}`,
    audience: 'none',
    heading: /royal canin kitten/i,
  },
  {
    path: `/shop/products/${p.multiVariantSlug}?sku=${p.skuKitten4kg}`,
    audience: 'none',
    heading: /royal canin kitten/i,
  },
  { path: `/shop/products/${p.kitSlug}`, audience: 'none', heading: /kitten starter kit/i },
  { path: `/shop/products/${p.rxNoAuthSlug}`, audience: 'none', heading: /urinary care/i },
  { path: `/shop/products/${p.rxNoAuthSlug}`, audience: 'customer', heading: /urinary care/i },
  { path: `/shop/products/${p.rxEligibleSlug}`, audience: 'customer', heading: /renal care/i },
  { path: `/shop/products/${p.thaiKittenSlug}?lang=bn`, audience: 'none', heading: /হুইস্কাস/ },
  { path: '/portal/wishlist', audience: 'customer', heading: /your wishlist/i },
  { path: '/portal/reviews', audience: 'customer', heading: /my reviews/i },
  {
    path: `/portal/reviews/new?product=${p.reviewableSlug}`,
    audience: 'customer',
    heading: /write a review/i,
  },
  {
    path: `/portal/cats/${p.catAllergy}/food`,
    audience: 'customer',
    heading: /food suggestions for whiskers/i,
  },
  { path: '/staff/shop/products', audience: 'staff', heading: /^products$/i },
  { path: '/staff/shop/products/new', audience: 'staff', heading: /new product/i },
  { path: EDITOR, audience: 'staff', heading: /royal canin kitten/i },
  { path: `${EDITOR}/food`, audience: 'staff', heading: /royal canin kitten/i },
  { path: `${EDITOR}/variants`, audience: 'staff', heading: /royal canin kitten/i },
  { path: `${EDITOR}/images`, audience: 'staff', heading: /royal canin kitten/i },
  { path: `${EDITOR}/related`, audience: 'staff', heading: /royal canin kitten/i },
  { path: `${EDITOR}/translations`, audience: 'staff', heading: /royal canin kitten/i },
  {
    path: `/staff/shop/products/${p.kitProductId}/bundle`,
    audience: 'staff',
    heading: /kitten starter kit/i,
  },
  { path: '/staff/shop/origins', audience: 'staff', heading: /sourcing origins/i },
  { path: '/staff/shop/reviews', audience: 'staff', heading: /review moderation/i },
];

async function signIn(page: Page, audience: RouteCase['audience']) {
  if (audience === 'customer') await signInAsCustomer(page);
  if (audience === 'staff') await signInAsStaff(page);
}

async function serious(page: Page) {
  const results = await new AxeBuilder({ page }).analyze();
  return results.violations
    .filter((v) => v.impact === 'serious' || v.impact === 'critical')
    .map((v) => `${v.id}: ${v.nodes.map((n) => n.target.join(' ')).join(' | ')}`);
}

test.describe('E-X1 — axe on every route (zero serious/critical)', () => {
  // The first visit to each route compiles it in `next dev` (slow under load): allow for that.
  test.describe.configure({ timeout: 90_000 });
  for (const route of ROUTES) {
    test(`${route.audience} ${route.path}`, async ({ page }) => {
      test.setTimeout(60_000);
      await page.goto(route.audience === 'staff' ? '/staff/sign-in' : '/');
      await signIn(page, route.audience);
      await page.goto(route.path);
      await expect(page.getByRole('heading', { name: route.heading }).first()).toBeVisible();
      // Let client islands (session, purchasability, recommendations) settle before scanning.
      await page.waitForLoadState('networkidle');
      expect(await serious(page)).toEqual([]);
    });
  }

  test('open filter sheet (mobile) / filter panel with active chips', async ({ page }) => {
    await page.goto('/shop/products?brand=Whiskas&origin=TH');
    // The chips are checked before the modal sheet opens (an open dialog hides the page behind it).
    await expect(page.getByRole('list', { name: 'Active filters' })).toBeVisible();
    if (isMobile(page)) {
      await page.getByRole('button', { name: /^Filters \(\d+\)$/ }).click();
      await expect(page.getByRole('dialog', { name: 'Filters' })).toBeVisible();
    }
    expect(await serious(page)).toEqual([]);
  });

  test('prescription gate with eligible cats and the stock-alert confirmation', async ({
    page,
  }) => {
    test.setTimeout(90_000);
    await signInAsCustomer(page);
    await page.goto(`/shop/products/${p.rxEligibleSlug}`);
    await expect(page.getByText('Authorised for Whiskers until 12 Dec 2026')).toBeVisible();
    expect(await serious(page)).toEqual([]);
    await page.goto(`/shop/products/${p.noConsentSlug}`);
    await page.getByRole('button', { name: /Tell me when it.s back/ }).click();
    await expect(page.getByText(/haven.t agreed to receive offers/)).toBeVisible();
    expect(await serious(page)).toEqual([]);
  });

  test('review form with validation errors', async ({ page }) => {
    await signInAsCustomer(page);
    await page.goto(`/portal/reviews/new?product=${p.reviewableSlug}`);
    await page.getByRole('button', { name: 'Send review' }).click();
    await expect(page.getByText('Choose a rating from 1 to 5.')).toBeVisible();
    expect(await serious(page)).toEqual([]);
  });

  test('product editor with errors (blank name, refused publish)', async ({ page }) => {
    await signInAsStaff(page);
    await page.goto(`/staff/shop/products/${p.unmetChecklistProductId}`);
    await page.getByLabel('Name').fill('');
    await page.getByRole('button', { name: 'Save' }).click();
    await expect(page.getByText('Enter the product name.')).toBeVisible();
    await page.getByRole('button', { name: 'Publish', exact: true }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Publish' }).click();
    await expect(
      page.getByRole('alert').filter({ hasText: /can.t be published yet/ }),
    ).toBeVisible();
    expect(await serious(page)).toEqual([]);
  });
});

const WIDTHS = [360, 768, 1024, 1440];
const STOREFRONT = ROUTES.filter((r) => r.audience === 'none');

test.describe('E-X2 — storefront at 360 / 768 / 1024 / 1440 px', () => {
  for (const route of STOREFRONT.slice(0, 9)) {
    test(`no horizontal scroll: ${route.path}`, async ({ page }) => {
      test.setTimeout(90_000);
      for (const width of WIDTHS) {
        await page.setViewportSize({ width, height: 900 });
        await page.goto(route.path);
        await expect(page.getByRole('heading', { name: route.heading }).first()).toBeVisible();
        const overflow = await page.evaluate(() => ({
          scroll: document.documentElement.scrollWidth,
          client: document.documentElement.clientWidth,
        }));
        expect(overflow.scroll, `${route.path} at ${width}px`).toBeLessThanOrEqual(overflow.client);
      }
    });
  }

  test('catalogue grid is 2 columns at 360, 3 at 768, 4 at 1024 and up', async ({ page }) => {
    const expected: Record<number, number> = { 360: 2, 768: 3, 1024: 4, 1440: 4 };
    for (const width of WIDTHS) {
      await page.setViewportSize({ width, height: 900 });
      await page.goto('/shop/products');
      const tops = await page
        .getByRole('article')
        .evaluateAll((els) =>
          els.slice(0, 8).map((el) => Math.round(el.getBoundingClientRect().top)),
        );
      const columns = tops.filter((top) => top === tops[0]).length;
      expect(columns, `columns at ${width}px`).toBe(expected[width]);
    }
  });
});
