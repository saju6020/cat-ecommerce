import { test, expect } from '@playwright/test';

/**
 * HO-ACC-002 §5.1/§12.3 — the real-API pass. Run manually against the merged HO-ACC-001 backend
 * (`dotnet run --project src/Api/CatEcommerce.Api --launch-profile https` from this worktree,
 * with this worktree's own copied, git-ignored `appsettings.Development.json`) with
 * `NEXT_PUBLIC_USE_MOCKS=false` and `NEXT_PUBLIC_API_BASE_URL` pointing at it — needs a live
 * database and a live API process this repo cannot start on its own, see this handover's own
 * report for the case-by-case results.
 *
 * CODE_REVIEW suggestion (second pass) — structurally excluded from `npm run test:e2e` (and from
 * every other project) by `playwright.config.ts`'s own `real-api` project/`testIgnore` pair, not
 * only by this comment: `npm run test:e2e:real-api` (`--project=real-api`) is the only way to run
 * this file at all.
 *
 * `E2E_CAPTCHA_ANSWER` — the CAPTCHA test seam (HO-ACC-001 §12.6), read from
 * `Captcha:TestAnswer` in this worktree's `appsettings.Development.json`.
 */
const CAPTCHA_ANSWER = process.env.E2E_CAPTCHA_ANSWER ?? 'TEST1';
const unique = Date.now().toString().slice(-9);

test('register a customer, sign in, edit profile (email change + current password), sign out', async ({
  page,
}) => {
  // The real MobileNumber value object requires exactly 11 digits starting "01" (Domain/
  // ValueObjects/MobileNumber.cs) — confirmed by this very test catching a too-long fixture on
  // its first real-API run (§20.7-style finding, not a frontend bug: the server correctly
  // rejected it and the client correctly surfaced that rejection on the field).
  const mobile = `019${unique.slice(0, 8)}`;
  const email = `real-api-${unique}@example.com`;
  const password = 'a-genuinely-long-password-1';

  await page.goto('/register');
  await page.getByLabel('Full name').fill('Real API Customer');
  await page.getByLabel(/mobile number/i).fill(mobile);
  await page.getByLabel(/^password/i).fill(password);
  await expect(page.getByAltText(/security check/i)).toBeVisible();
  await page.getByLabel(/security check/i).fill(CAPTCHA_ANSWER);
  await page.getByRole('button', { name: 'Create account' }).click();
  await expect(page.getByRole('heading', { name: 'Your account is ready' })).toBeVisible({
    timeout: 15_000,
  });

  await page.getByRole('link', { name: 'Sign in' }).click();
  await page.getByLabel(/mobile number or email/i).fill(mobile);
  await page.getByLabel(/^password/i).fill(password);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page).toHaveURL(/\/portal\//, { timeout: 15_000 });

  // Profile edit — add an email (canEditEmail is true for a self-registered customer, so this
  // exercises the email-change + current-password step-up, HO-ACC-001 §1 Amended (17)).
  await page.goto('/portal/account/profile');
  await expect(page.getByLabel('Full name')).toHaveValue('Real API Customer');
  const emailInput = page.getByLabel(/^email$/i);
  await emailInput.fill(email);
  await expect(page.getByLabel(/current password/i)).toBeVisible();
  await page.getByLabel(/current password/i).fill(password);
  await page.getByRole('button', { name: /^save$/i }).click();
  await expect(page.getByText(/profile has been saved/i)).toBeVisible({ timeout: 10_000 });

  // Sign out.
  await page.getByRole('button', { name: 'Sign out' }).click();
  await expect(page).toHaveURL(/\/sign-in/);
});

test('request a password reset for an unknown identifier — neutral response either way', async ({
  page,
}) => {
  await page.goto('/forgot-password');
  await page.getByLabel(/mobile number or email/i).fill('01700000000');
  await page.getByRole('button', { name: /send reset link/i }).click();
  await expect(page.getByText(/if an account matches/i)).toBeVisible({ timeout: 10_000 });
});

test('staff sign in and sign out', async ({ page }) => {
  // Requires a staff administrator to already exist in catecommerce_dev with a password set —
  // see this handover's own report for how the seed account used here was created.
  const identifier = process.env.E2E_STAFF_IDENTIFIER;
  const password = process.env.E2E_STAFF_PASSWORD;
  test.skip(
    !identifier || !password,
    'Set E2E_STAFF_IDENTIFIER/E2E_STAFF_PASSWORD to run this case.',
  );

  await page.goto('/staff/sign-in');
  await page.getByLabel(/mobile number or email/i).fill(identifier!);
  await page.getByLabel(/^password/i).fill(password!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page).not.toHaveURL(/\/staff\/sign-in/, { timeout: 15_000 });

  await page.getByRole('button', { name: 'Sign out' }).click();
  await expect(page).toHaveURL(/\/staff\/sign-in/);
});
