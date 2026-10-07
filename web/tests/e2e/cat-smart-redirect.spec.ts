import { test, expect, signInAsStaff, signInAsCustomer } from './fixtures';
import { catProfilesFixtureIds } from '../../mocks/handlers/areas/cat-profiles';
import { accountsFixtureIds } from '../../mocks/handlers/areas/accounts';

/**
 * `tests/e2e/cat-smart-redirect.spec.ts` — HO-CAT-002 §1 Amended (2026-09-27, both the original
 * row and the owner's later 2026-09-27 revision), the cage-card QR's `/cats/{id}` smart-redirect
 * page (HO-CAT-001 §1 Amended, same date, item (a)). Runs against the real cookie-driven sign-in
 * flow through MSW (`signInAsStaff`/`signInAsCustomer`, both projects — `chromium` and
 * `mobile-360`), unlike the unit tests in `tests/unit/cat/cat-smart-redirect-page.test.tsx`, which
 * mock the session and ownership-check endpoints directly. Every case asserts the FINAL url, and
 * that the intended screen actually rendered — not just that some navigation happened.
 */
const catId = catProfilesFixtureIds.catWithAllergiesExactDob;
const staffSignInWithNext = `/staff/sign-in?next=${encodeURIComponent(`/staff/cats/${catId}`)}`;

async function forceOwnershipCheckToReportNotOwned(page: import('@playwright/test').Page) {
  // `mocks/handlers/areas/cat-profiles.ts`'s own one-shot test hook — the mock-side stand-in for
  // "the signed-in customer does not own this cat" (a `404`, the same status the real,
  // session-filtered query would give a non-owner). `/cats/{id}`'s ownership check fires on first
  // mount, right after the FULL navigation that lands there, so a plain `page.evaluate` (which only
  // reaches the CURRENT page, before that navigation tears it down) cannot set this in time —
  // `addInitScript` runs in the NEXT document, before any of its own scripts, so the flag is
  // already on `window` by the time this module's fresh instance re-initialises there. See that
  // file's own doc comment for the rest of the reasoning.
  await page.addInitScript(() => {
    (window as unknown as { __forceCatQueryNotOwned?: boolean }).__forceCatQueryNotOwned = true;
  });
}

test.describe('smart redirect — /cats/{id}', () => {
  test('staff lands on the staff cat profile', async ({ page }) => {
    await signInAsStaff(page);
    await page.goto(`/cats/${catId}`);
    await expect(page).toHaveURL(`/staff/cats/${catId}`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  });

  test('a signed-in customer who owns the cat lands on the portal cat profile', async ({
    page,
  }) => {
    await signInAsCustomer(page);
    await page.goto(`/cats/${catId}`);
    await expect(page).toHaveURL(`/portal/cats/${catId}`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  });

  test('a signed-in customer who does NOT own the cat lands on staff sign-in, not the portal', async ({
    page,
  }) => {
    await signInAsCustomer(page);
    await forceOwnershipCheckToReportNotOwned(page);
    await page.goto(`/cats/${catId}`);
    await expect(page).toHaveURL(staffSignInWithNext);
    await expect(page.getByRole('heading', { name: /staff sign in/i })).toBeVisible();
  });

  test('a 5xx from the ownership check fails closed to the same staff sign-in destination', async ({
    page,
  }) => {
    await signInAsCustomer(page);
    // `catServerError` always 500s on GET (mocks/handlers/areas/cat-profiles.ts) — reused here as
    // the ownership check's own failure, rather than inventing a second force-flag for it.
    const serverErrorCatId = catProfilesFixtureIds.catServerError;
    await page.goto(`/cats/${serverErrorCatId}`);
    await expect(page).toHaveURL(
      `/staff/sign-in?next=${encodeURIComponent(`/staff/cats/${serverErrorCatId}`)}`,
    );
    await expect(page.getByRole('heading', { name: /staff sign in/i })).toBeVisible();
  });

  test('anyone not signed in lands on staff sign-in, and signing in as staff returns them to the cat they scanned', async ({
    page,
  }) => {
    await page.goto(`/cats/${catId}`);
    await expect(page).toHaveURL(staffSignInWithNext);
    // The intended screen (staff sign-in), not merely a matching URL — proves no cat data ever
    // rendered for an anonymous visitor, and that staff, not customer, sign-in is now the default.
    await expect(page.getByRole('heading', { name: /staff sign in/i })).toBeVisible();

    await page.getByLabel(/mobile number or email/i).fill(accountsFixtureIds.adminIdentifier);
    await page.getByLabel(/^password/i).fill(accountsFixtureIds.adminPassword);
    await page.getByRole('button', { name: /^sign in$/i }).click();

    await expect(page).toHaveURL(`/staff/cats/${catId}`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  });

  test('an id that is not a GUID never redirects and shows an inline message', async ({ page }) => {
    await signInAsStaff(page);
    await page.goto('/cats/not-a-real-id');
    await expect(page).toHaveURL('/cats/not-a-real-id');
    await expect(page.getByText(/link isn't valid/i)).toBeVisible();
  });

  test('staff wins when the browser carries both a staff and a customer session', async ({
    page,
  }) => {
    await signInAsStaff(page);
    await signInAsCustomer(page);
    await page.goto(`/cats/${catId}`);
    await expect(page).toHaveURL(`/staff/cats/${catId}`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  });
});
