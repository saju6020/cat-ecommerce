import { test, expect, signInAsStaff } from './fixtures';

/**
 * Staff account administration — HO-ACC-002 §16 E-G1/E-F1, run against MSW. E-G1's full
 * "member sets their own password via a real reset link" round trip needs the API's Development
 * log (§12.3) and is exercised in the real-API pass instead — this covers the part reachable
 * without it: an administrator creates the account and sees the hand-off message.
 *
 * ARCH gate MEDIUM (first pass) — E-G1 signs in as the admin explicitly; E-F1 manages its own
 * sign-in (as a nurse, after clearing cookies) and needs no pre-authentication at all.
 */

test('E-G1 (partial): an administrator creates a staff account, pointed at "Set or reset password"', async ({
  page,
}) => {
  await signInAsStaff(page);
  await page.goto('/staff/admin/staff-accounts/new');
  await page.getByLabel(/full name/i).fill('New Nurse');
  await page.getByLabel(/mobile number/i).fill('01788877766');
  await page.getByRole('checkbox', { name: 'Nurse' }).check();
  await page.getByRole('button', { name: 'Create account' }).click();

  await expect(page.getByText(/ask new nurse to choose .set or reset password./i)).toBeVisible();

  await page.goto('/staff/admin/staff-accounts');
  await expect(page.getByText('New Nurse')).toBeVisible();
  await expect(page.getByRole('link', { name: /new nurse/i })).toBeVisible();
});

test('E-F1: a nurse opening /staff/admin/staff-accounts directly gets PermissionDeniedState', async ({
  page,
}) => {
  await page.context().clearCookies();
  await page.goto('/staff/sign-in');
  await page.getByLabel(/mobile number or email/i).fill('nurse@purrcare.test');
  await page.getByLabel(/^password/i).fill('NursePass123!');
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page).toHaveURL(/\/staff\/cats|\/staff\//);

  await page.goto('/staff/admin/staff-accounts');
  await expect(page.getByText(/you don.t have permission to do this/i)).toBeVisible();
});
