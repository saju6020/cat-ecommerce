import { test, expect as baseExpect, signInAsStaff } from './fixtures';
import { setMaintenance, withFilters } from './shop-helpers';

// First visits compile routes in `next dev`: allow for that.
const expect = baseExpect.configure({ timeout: 20_000 });

/**
 * HO-SHP-002 §16 E-A2, E-A3, E-A4, E-E1 — run against MSW (the shop mock serves BOTH the browser worker
 * and `msw/node` for server rendering). E-A1 (the AC-5 journey) lives in `shop-product.spec.ts`
 * because it ends on the product page.
 */

test.describe('catalogue state lives in the URL', () => {
  test('E-A2: filter, sort, go to page 2, reload, press Back', async ({ page }) => {
    await page.goto('/shop/products');
    await expect(page.getByRole('heading', { level: 1, name: 'All products' })).toBeVisible();

    await withFilters(page, async (scope) => {
      await scope.getByRole('checkbox', { name: /In stock only/ }).check();
    });
    await expect(page).toHaveURL(/availability=inStock/);

    await page.getByLabel('Sort by').selectOption('price_asc');
    await expect(page).toHaveURL(/sort=price_asc/);
    await expect(page).toHaveURL(/availability=inStock/);

    const firstNamePage1 = await page.getByRole('article').first().getByRole('heading').innerText();
    await page.getByRole('link', { name: 'Page 2', exact: true }).click();
    await expect(page).toHaveURL(/pageNumber=2/);
    await expect(page.getByRole('link', { name: 'Page 2', exact: true })).toHaveAttribute(
      'aria-current',
      'page',
    );
    const firstNamePage2 = await page.getByRole('article').first().getByRole('heading').innerText();
    expect(firstNamePage2).not.toBe(firstNamePage1);

    // Reload: filters, sort and page survive.
    await page.reload();
    await expect(page).toHaveURL(/availability=inStock/);
    await expect(page).toHaveURL(/sort=price_asc/);
    await expect(page).toHaveURL(/pageNumber=2/);
    await expect(page.getByLabel('Sort by')).toHaveValue('price_asc');

    // Back restores the previous state of the same filtered, sorted results. (Back immediately
    // after a hard reload bounces in the dev server even for plain links — a framework behaviour, see
    // TC-HO-SHP-002 — so Back is exercised from the reloaded page's next navigation.)
    const firstNamePage2Again = await page
      .getByRole('article')
      .first()
      .getByRole('heading')
      .innerText();
    await page.getByRole('link', { name: 'Page 1', exact: true }).click();
    await expect(page).not.toHaveURL(/pageNumber=2/);
    await expect(page.getByRole('article').first().getByRole('heading')).toHaveText(firstNamePage1);
    await page.goBack();
    await expect(page).toHaveURL(/pageNumber=2/);
    await expect(page).toHaveURL(/availability=inStock/);
    await expect(page).toHaveURL(/sort=price_asc/);
    await expect(page.getByRole('article').first().getByRole('heading')).toHaveText(
      firstNamePage2Again,
    );
  });

  test('E-A2: the result count is announced in a polite live region after a filter change', async ({
    page,
  }) => {
    await page.goto('/shop/products');
    const status = page.getByRole('status').filter({ hasText: /products?$/ });
    await expect(status).toBeVisible();
    const before = await status.innerText();
    await withFilters(page, async (scope) => {
      await scope.getByRole('checkbox', { name: /Thailand/ }).check();
    });
    await expect(status).not.toHaveText(before);
    await expect(status).toHaveAttribute('aria-live', 'polite');
  });
});

test.describe('search (US-SHP-06)', () => {
  test('E-A3: a misspelled brand still finds the brand; nonsense offers alternatives', async ({
    page,
  }) => {
    await page.goto('/shop');
    await page
      .getByRole('search', { name: 'Products' })
      .getByLabel('Search products')
      .fill('royal canan');
    await page.getByRole('button', { name: 'Search', exact: true }).click();
    await expect(page).toHaveURL(/q=royal([+]|%20)canan/);
    await expect(page.getByRole('heading', { level: 2, name: 'Royal Canin Kitten' })).toBeVisible();

    await page
      .getByRole('search', { name: 'Products' })
      .getByLabel('Search products')
      .fill('zzqqxx');
    await page.getByRole('button', { name: 'Search', exact: true }).click();
    await expect(page.getByText(/No products match/)).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Popular right now' })).toBeVisible();
    await expect(page.getByRole('article').first()).toBeVisible();
  });

  test('E-A3: a typo in a search offers a Did you mean link that keeps the filters', async ({
    page,
  }) => {
    await page.goto('/shop/products?q=royl%20canan&availability=inStock');
    await expect(page.getByText(/No products match/)).toBeVisible();
    const suggestion = page.getByRole('link', { name: 'royal canin' });
    await expect(suggestion).toBeVisible();
    await suggestion.click();
    await expect(page).toHaveURL(/availability=inStock/);
    await expect(page.getByRole('heading', { level: 2, name: 'Royal Canin Kitten' })).toBeVisible();
  });

  test('E-A4: an ingredient and a tag find matching products', async ({ page }) => {
    await page.goto('/shop/products?q=salmon');
    await expect(
      page.getByRole('heading', { level: 2, name: 'Whiskas Kitten Salmon' }),
    ).toBeVisible();
    await page.goto('/shop/products?q=grain-free');
    await expect(page.getByRole('heading', { level: 2, name: 'Royal Canin Kitten' })).toBeVisible();
  });
});

test.describe('maintenance (US-ADM-08)', () => {
  test('E-E1: maintenance replaces the storefront; the back office keeps working', async ({
    page,
  }) => {
    await signInAsStaff(page);
    await setMaintenance(page, true, 'Stock take until 6pm.');
    try {
      await page.goto('/shop/products');
      await expect(page.getByText('Stock take until 6pm.')).toBeVisible();
      await expect(
        page.getByText(/Clinic bookings and your account are still available/),
      ).toBeVisible();
      await expect(page.getByRole('link', { name: 'Go to your account' })).toHaveAttribute(
        'href',
        '/portal',
      );
      await expect(page.getByRole('article')).toHaveCount(0);

      await page.goto('/staff/shop/products');
      await expect(page.getByRole('heading', { level: 1, name: 'Products' })).toBeVisible();
    } finally {
      await setMaintenance(page, false);
    }
    await page.goto('/shop/products');
    await expect(page.getByRole('article').first()).toBeVisible();
  });
});

test.describe('the shell without JavaScript (S4)', () => {
  for (const path of ['/shop', '/shop/countries', '/shop/products']) {
    test(`S4: ${path} has its header, nav and search in the HTML`, async ({ browser, baseURL }) => {
      const context = await browser.newContext({ javaScriptEnabled: false, baseURL });
      const page = await context.newPage();
      await page.goto(path);
      await expect(page.getByRole('link', { name: 'PurrCare Shop' })).toBeVisible();
      await expect(page.getByRole('navigation', { name: 'Shop categories' })).toBeVisible();
      await expect(page.getByRole('search', { name: 'Products' })).toBeVisible();
      await expect(page.getByRole('searchbox', { name: 'Search products' })).toBeVisible();
      await context.close();
    });
  }
});
