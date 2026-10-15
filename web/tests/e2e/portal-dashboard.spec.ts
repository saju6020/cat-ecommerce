import type { Page } from '@playwright/test';
import { test, expect } from './fixtures';
import { holdApiUntilMswReady, selectPortalScenario, signInEstablished } from './portal-helpers';
import { accountsFixtureIds } from '../../mocks/handlers/areas/accounts';
import { portalIds } from '../../mocks/handlers/portal/fixtures';

/**
 * HO-POR-002 §16 — the portal home and Orders' re-order, against MSW. The mock answers with the
 * dashboard named by the `pc_mock_portal` cookie (the handlers run inside the page, so a module
 * variable would not survive a navigation). Stays and subscriptions have no tile (owner skipped
 * BRD and SUB, 2026-10-10).
 */
test.describe.configure({ timeout: 60_000 });

async function open(page: Page, name = 'full') {
  await signInEstablished(page);
  await selectPortalScenario(page, name);
  await page.goto('/portal');
}

const tile = (page: Page, id: string) => page.getByTestId(`tile-${id}`);

test.describe('Portal home — US-POR-01', () => {
  test('E-A1: a new customer sees the no-cats invitation and Add a cat reaches the cat form', async ({
    page,
  }) => {
    await open(page, 'new');
    await expect(page.getByRole('heading', { name: /Welcome back, Nusrat/ })).toBeVisible();
    await expect(page.getByText('Add your first cat')).toBeVisible();
    await expect(tile(page, 'orders').getByText(/haven’t placed an order/)).toBeVisible();
    await page.getByRole('link', { name: 'Add a cat' }).click();
    await expect(page).toHaveURL(/\/portal\/cats\/new/);
  });

  test('E-A2: a full customer sees six tiles on one screen and no stays or subscriptions tile', async ({
    page,
  }) => {
    await open(page);
    for (const heading of [
      'My cats',
      'Upcoming appointments',
      'Recent orders',
      'Balances',
      'Recent visits',
      'Prescriptions',
    ]) {
      await expect(page.getByRole('heading', { level: 2, name: heading })).toBeVisible();
    }
    await expect(page.getByRole('heading', { name: /stay|boarding|subscription/i })).toHaveCount(0);
    await expect(tile(page, 'balances').getByTestId('outstanding-BDT')).toContainText('3,250');
    await expect(tile(page, 'balances').getByTestId('outstanding-USD')).toContainText('40.00');
    await expect(tile(page, 'orders')).toContainText('Tracking DHK-88213-BD');
    await expect(tile(page, 'visits')).toContainText('Follow-up due by 14 Oct 2026');
    await expect(tile(page, 'appointments')).toContainText('(Europe/London)');
    // no payment control anywhere on the page
    await expect(page.getByRole('button', { name: /\bpay/i })).toHaveCount(0);
    await expect(page.getByRole('link', { name: /\bpay/i })).toHaveCount(0);
  });

  test('E-A3: a degraded dashboard explains the unavailable tiles; Try again fills them after the backend recovers', async ({
    page,
  }) => {
    await open(page, 'degraded');
    await expect(tile(page, 'balances')).toContainText('couldn’t load your balances');
    await expect(tile(page, 'visits')).toContainText('couldn’t load your visits');
    await expect(tile(page, 'balances')).not.toContainText('Nothing to pay');
    await expect(tile(page, 'cats')).toContainText('Noodle');

    await selectPortalScenario(page, 'full');
    await tile(page, 'balances').getByRole('button', { name: 'Try again' }).click();
    await expect(tile(page, 'balances').getByTestId('outstanding-BDT')).toBeVisible();
    await expect(tile(page, 'visits')).toContainText('Follow-up due by');
  });

  test('E-A3b: an unavailable cats tile never shows the no-cats invitation', async ({ page }) => {
    await open(page, 'catsUnavailable');
    await expect(tile(page, 'cats')).toContainText('couldn’t load your cats');
    await expect(page.getByText('Add your first cat')).toHaveCount(0);
  });

  test('E-A3c: a section the backend has not delivered reads "Not available yet"', async ({
    page,
  }) => {
    await open(page, 'ordersNotDelivered');
    await expect(tile(page, 'orders')).toContainText('Not available yet');
    await expect(tile(page, 'orders').getByRole('button', { name: 'Try again' })).toHaveCount(0);
  });

  test('E-A4: tile links reach the owning areas; links to unbuilt areas are asserted by href only', async ({
    page,
  }) => {
    await open(page);
    // built targets
    await tile(page, 'cats').getByRole('link', { name: 'Book a vaccination for Noodle' }).click();
    await expect(page).toHaveURL(
      new RegExp(`/book\\?catId=${portalIds.cats.noodle}&category=Vaccination`),
    );
    await page.goBack();
    await tile(page, 'appointments')
      .getByRole('link', { name: /All appointments/ })
      .click();
    await expect(page).toHaveURL(/\/portal\/appointments$/);
    await page.goBack();
    await tile(page, 'balances')
      .getByRole('link', { name: /All invoices/ })
      .click();
    await expect(page).toHaveURL(/\/portal\/billing$/);
    await page.goBack();
    await tile(page, 'cats')
      .getByRole('link', { name: /All cats/ })
      .click();
    await expect(page).toHaveURL(/\/portal\/cats$/);
    await page.goBack();

    // unbuilt (HO-ORD-002, HO-PHM-002) — href only, recorded in TC-HO-POR-002
    await expect(tile(page, 'orders').getByRole('link', { name: /All orders/ })).toHaveAttribute(
      'href',
      '/portal/orders',
    );
    await expect(
      tile(page, 'prescriptions').getByRole('link', { name: /All prescriptions/ }),
    ).toHaveAttribute('href', '/portal/prescriptions');
  });

  test('E-A5: the portal nav carries a Home entry', async ({ page }) => {
    await open(page);
    await expect(
      page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Home' }),
    ).toHaveAttribute('href', '/portal');
  });
});

test.describe('Re-order through Orders — US-POR-02', () => {
  test('E-B1: ordering a delivered order again lists the result and offers the basket', async ({
    page,
  }) => {
    await open(page);
    await page.getByRole('button', { name: 'Order SO-2026-00042 again' }).click();
    const dialog = page.getByRole('dialog', { name: 'Added to your basket' });
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText('3 items from order SO-2026-00042 were added');
    await expect(dialog.getByRole('link', { name: 'View basket' })).toHaveAttribute(
      'href',
      '/cart',
    );
    await dialog.getByRole('button', { name: 'Close' }).click();
    await expect(page.getByRole('button', { name: 'Order SO-2026-00042 again' })).toBeFocused();
  });

  test('E-B2: an order with unavailable lines lists each with its reason', async ({ page }) => {
    await open(page);
    await page.getByRole('button', { name: 'Order SO-2026-00027 again' }).click();
    const dialog = page.getByRole('dialog', { name: 'Some items were added' });
    await expect(dialog.getByText('Feliway Classic diffuser refill')).toBeVisible();
    await expect(dialog.getByText('Out of stock right now.')).toBeVisible();
    await expect(dialog.getByText(/Needs a prescription or vet authorisation/)).toBeVisible();
    await expect(dialog.getByText('Mystery item')).toBeVisible();
    await expect(dialog).not.toContainText('PrescriptionNotAuthorised');
  });

  test('E-B3: ordering the same order again is a second request and the dialog reports what the server returns', async ({
    page,
  }) => {
    // Key uniqueness between two deliberate re-orders is proved in the component test (the service
    // worker answers requests under MSW, so Playwright cannot see their headers here).
    await open(page);
    const button = page.getByRole('button', { name: 'Order SO-2026-00042 again' });
    await button.click();
    await page.getByRole('dialog').getByRole('button', { name: 'Close' }).click();
    await button.click();
    await expect(page.getByRole('dialog', { name: 'Added to your basket' })).toBeVisible();
  });

  test('E-B4: an order awaiting payment has no active re-order and says why', async ({ page }) => {
    await open(page);
    const button = page.getByRole('button', { name: 'Order SO-2026-00061 again' });
    await expect(button).toBeDisabled();
    await expect(tile(page, 'orders')).toContainText(
      'once this order has been delivered or closed',
    );
  });
});

test.describe('Sign-in landing — owner decision 2026-10-13 ("Land on /portal")', () => {
  async function signInThroughTheForm(page: Page, path: string) {
    await holdApiUntilMswReady(page);
    await page.goto(path);
    await page.waitForFunction(
      () => (window as unknown as { __mswReady?: boolean }).__mswReady === true,
      undefined,
      { timeout: 60_000 },
    );
    await page.getByLabel(/mobile number or email/i).fill(accountsFixtureIds.customerEmail);
    await page.getByLabel(/^password/i).fill(accountsFixtureIds.customerPassword);
    await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  }

  test('E-A1: signing in with no ?next= lands on /portal', async ({ page }) => {
    await signInThroughTheForm(page, '/sign-in');
    await expect(page).toHaveURL(/\/portal$/);
    await expect(page.getByRole('heading', { level: 1, name: /Welcome back/ })).toBeVisible();
  });

  test('an explicit ?next= still wins', async ({ page }) => {
    await signInThroughTheForm(page, '/sign-in?next=%2Fportal%2Fcats');
    await expect(page).toHaveURL(/\/portal\/cats$/);
  });
});
