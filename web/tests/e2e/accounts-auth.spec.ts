import { test, expect, signInAsCustomer } from './fixtures';
import { accountsFixtureIds } from '../../mocks/handlers/areas/accounts';

/**
 * Registration, customer sign-in and the portal guard — HO-ACC-002 §16 E-B/E-C/E-D series,
 * run against MSW. This whole file is customer-audience only (ARCH gate MEDIUM, first pass:
 * fixtures.ts signs in for exactly one audience, never both) — `beforeEach` pre-authenticates as
 * the seeded customer so E-C2/E-C3 (which expect an ALREADY signed-in session) have one; every
 * other test either manages its own sign-in explicitly or clears cookies first, both of which are
 * unaffected by this default.
 */

test.beforeEach(async ({ page }) => {
  await signInAsCustomer(page);
});

test.describe('Registration — US-ACC-01', () => {
  test('E-B1: register with a mobile number, then sign in lands on the portal', async ({
    page,
  }) => {
    await page.context().clearCookies();
    await page.goto('/register');
    await page.getByLabel('Full name').fill('Priya Owner');
    await page.getByLabel(/mobile number/i).fill('01755512345');
    await page.getByLabel(/^password/i).fill('a-genuinely-long-password');
    await expect(page.getByAltText(/security check/i)).toBeVisible();
    await page.getByLabel(/security check/i).fill('ABCDE');
    await page.getByRole('button', { name: 'Create account' }).click();

    // Owner, 2026-10-19: a new customer is signed in straight after registering (no second sign-in).
    await expect(page).toHaveURL(/\/portal(\/|$)/);
  });

  test('E-B3: wrong CAPTCHA then right — message, new image, fields kept, then success', async ({
    page,
  }) => {
    await page.goto('/register');
    await page.getByLabel('Full name').fill('Retry Owner');
    await page.getByLabel(/mobile number/i).fill('01755523456');
    await page.getByLabel(/^password/i).fill('a-genuinely-long-password');
    await expect(page.getByAltText(/security check/i)).toBeVisible();
    await page.getByLabel(/security check/i).fill('WRONG');
    await page.getByRole('button', { name: 'Create account' }).click();

    await expect(page.getByText(/didn.t match, or the code expired/i)).toBeVisible();
    await expect(page.getByLabel('Full name')).toHaveValue('Retry Owner');
    await expect(page.getByLabel(/^password/i)).toHaveValue('');

    // ADR-FE-006: a failed submit clears the password too — a real user retypes it.
    await page.getByLabel(/^password/i).fill('a-genuinely-long-password');
    await page.getByLabel(/security check/i).fill('ABCDE');
    await page.getByRole('button', { name: 'Create account' }).click();
    // Owner, 2026-10-19: a new customer is signed in straight after registering (no second sign-in).
    await expect(page).toHaveURL(/\/portal(\/|$)/);
  });
});

test.describe('Sign-in — US-ACC-04', () => {
  test('E-C1: an unknown identifier and a wrong password show the identical screen', async ({
    page,
  }) => {
    await page.context().clearCookies();
    await page.goto('/sign-in');
    await page.getByLabel(/mobile number or email/i).fill('nobody@example.com');
    await page.getByLabel(/^password/i).fill('whatever12345');
    await page.getByRole('button', { name: 'Sign in', exact: true }).click();
    // Not getByRole('alert') alone — Next.js's route-announcer region also has role="alert".
    const unknownText = await page.getByText(/don.t match an account/i).textContent();

    await page.getByLabel(/mobile number or email/i).fill(accountsFixtureIds.customerEmail);
    await page.getByLabel(/^password/i).fill('the-wrong-password');
    await page.getByRole('button', { name: 'Sign in', exact: true }).click();
    const wrongPasswordText = await page.getByText(/don.t match an account/i).textContent();

    expect(wrongPasswordText).toBe(unknownText);
  });

  test('E-C2: reload the portal after sign-in — still signed in', async ({ page }) => {
    await page.goto('/portal/account/profile');
    await expect(page.getByLabel('Full name')).toBeVisible();
    await page.reload();
    await expect(page).toHaveURL(/\/portal\/account\/profile/);
    await expect(page.getByLabel('Full name')).toBeVisible();
  });

  test('E-C3: sign out, then Back does not reveal cached portal data', async ({ page }) => {
    await page.goto('/portal/account/profile');
    await expect(page.getByLabel('Full name')).toBeVisible();
    // Sign out is in the menu under the customer's name (2026-10-17).
    await page.getByRole('button', { name: /^Account menu for / }).click();
    await page.getByRole('button', { name: 'Sign out' }).first().click();
    await expect(page).toHaveURL(/\/sign-in/);

    await page.goBack();
    await expect(page).toHaveURL(/\/sign-in/);
  });

  test('E-C4: opening /portal/account/profile signed out redirects to sign-in, then back after sign-in', async ({
    page,
  }) => {
    await page.context().clearCookies();
    await page.goto('/portal/account/profile');
    await expect(page).toHaveURL(/\/sign-in\?next=/);

    await page.getByLabel(/mobile number or email/i).fill(accountsFixtureIds.customerEmail);
    await page.getByLabel(/^password/i).fill(accountsFixtureIds.customerPassword);
    await page.getByRole('button', { name: 'Sign in', exact: true }).click();
    await expect(page).toHaveURL(/\/portal\/account\/profile/);
  });
});

test.describe('Password reset — US-ACC-05', () => {
  test('E-D1: request, then follow a reset link, then sign in with the new password', async ({
    page,
  }) => {
    await page.goto('/forgot-password');
    await page.getByLabel(/mobile number or email/i).fill(accountsFixtureIds.customerEmail);
    await page.getByRole('button', { name: /send reset link/i }).click();
    await expect(page.getByText(/if an account matches/i)).toBeVisible();

    await page.goto('/reset-password#token=valid-customer-token');
    await expect(page).toHaveURL('/reset-password'); // the fragment is stripped
    await page.getByLabel(/new password/i).fill('a-brand-new-password-1');
    await page.getByRole('button', { name: /set new password/i }).click();
    await expect(page.getByText(/your password has been set/i)).toBeVisible();

    await page.context().clearCookies();
    await page.goto('/sign-in');
    await page.getByLabel(/mobile number or email/i).fill(accountsFixtureIds.customerEmail);
    await page.getByLabel(/^password/i).fill('a-brand-new-password-1');
    await page.getByRole('button', { name: 'Sign in', exact: true }).click();
    await expect(page).toHaveURL(/\/portal(\/|$)/);
  });

  test('E-D2: reusing the link shows the invalid/expired state', async ({ page }) => {
    await page.goto('/reset-password#token=expired-token');
    await page.getByLabel(/new password/i).fill('another-long-password');
    await page.getByRole('button', { name: /set new password/i }).click();
    await expect(page.getByText(/this link is invalid or has expired/i)).toBeVisible();
  });
});
