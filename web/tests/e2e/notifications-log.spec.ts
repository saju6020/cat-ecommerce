import { test, expect, signInAsStaff } from './fixtures';
import { notificationFixtureIds as ids } from '../../mocks/handlers/notifications';
import { accountsFixtureIds } from '../../mocks/handlers/areas/accounts';

/**
 * HO-NTF-002 §16 — E-E1, E-E2 against MSW (real-API column in `docs/test-cases/TC-HO-NTF-002.md`).
 *
 * E-E1 is run as the seeded **Administrator**: the mock accounts store seeds no Reception account
 * that can sign in (`noPasswordStaffId` has no password), and `NotificationLog.View/Resend` are
 * granted to Reception AND Administrator (`ActionRoles.Notifications.cs`), so the flow is identical.
 * The mock log's rows are generated relative to "now" so the default 7-day range always shows them.
 */

test("E-E1: open a customer's messages from their page, filter to failures, open a failed SMS, resend by email, follow the chain", async ({
  page,
}) => {
  await signInAsStaff(page);

  // From the customer page: Messages -> the log filtered to that customer.
  await page.goto(`/staff/customers/${accountsFixtureIds.customerId}`);
  // (The staff rail also has a "Messages" link; this one is inside the page content.)
  await page.locator('#main-content').getByRole('link', { name: 'Messages' }).click();
  await expect(page).toHaveURL(new RegExp(`recipientId=${ids.customerId}`));
  await expect(page.getByRole('heading', { level: 1, name: 'Messages' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Clear customer' })).toBeVisible();

  // The provider warning is visible, not silent.
  await expect(
    page.getByText(/SMS is not connected to a provider\. Text messages/).first(),
  ).toBeVisible();

  // Filter by status: only failures remain (and the choice is in the URL).
  await page.getByLabel('Status').selectOption('Undeliverable');
  await expect(page).toHaveURL(/status=Undeliverable/);
  const failed = page.locator(`a[href="/staff/notifications/${ids.failedSmsId}"]:visible`).first();
  await expect(failed).toBeVisible();
  await expect(page.locator(`a[href="/staff/notifications/${ids.sentSmsId}"]:visible`)).toHaveCount(
    0,
  );

  // Open the failed SMS: every attempt is listed.
  await failed.click();
  await expect(page.getByRole('heading', { level: 1, name: 'Vaccination due' })).toBeVisible();
  await expect(page.getByRole('table', { name: 'Delivery attempts' }).getByRole('row')).toHaveCount(
    4,
  );
  await expect(page.getByText('+8801799999999')).toBeVisible();

  // Resend by email, confirming channel and destination first.
  await page.getByLabel('Resend by').selectOption('Email');
  await page.getByRole('button', { name: 'Resend', exact: true }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toContainText(
    "Send this message again by email to Sam Rahman's email address on file?",
  );
  await dialog.getByRole('button', { name: 'Resend' }).click();
  await expect(page.getByRole('status').filter({ hasText: /^Queued/ })).toBeVisible();

  // The new notification is linked in the chain, and points back to the original.
  await page.getByRole('link', { name: 'View the new notification' }).click();
  await expect(page.getByText('Resend of')).toBeVisible();
  await page.getByRole('link', { name: 'View message' }).first().click();
  await expect(page.getByText('Resent as')).toBeVisible();
});

test('a held row shows its release time, a redacted reset cannot be resent, markup is plain text', async ({
  page,
}) => {
  await signInAsStaff(page);
  await page.goto('/staff/notifications');
  // (Table and card list both exist in the DOM; CSS shows one — pick the visible one.)
  await expect(
    page.locator('span:visible', { hasText: /Held until \d\d:\d\d/ }).first(),
  ).toBeVisible();

  await page.goto(`/staff/notifications/${ids.redactedResetId}`);
  await expect(
    page.getByText('Content removed — it contained a password-reset link'),
  ).toBeVisible();
  await expect(page.getByTestId('cannot-resend')).toContainText("can't be resent");
  await expect(page.getByRole('button', { name: 'Resend', exact: true })).toHaveCount(0);

  await page.goto(`/staff/notifications/${ids.markupId}`);
  await expect(page.getByTestId('notification-body')).toContainText('Hello <i>there</i>');
  expect(await page.locator('main i, main script, main b').count()).toBe(0);
});

test('a range over 93 days is refused before any request is made', async ({ page }) => {
  await signInAsStaff(page);
  const logRequests: string[] = [];
  page.on('request', (request) => {
    if (/\/api\/admin\/notifications\?/.test(request.url())) logRequests.push(request.url());
  });
  await page.goto('/staff/notifications?from=2026-01-01&to=2026-06-30');
  await expect(page.getByRole('alert').filter({ hasText: '93' })).toBeVisible();
  await page.waitForTimeout(500);
  expect(logRequests).toHaveLength(0);
});

test('E-E2: a nurse opening /staff/notifications gets PermissionDeniedState from the server 403', async ({
  page,
}) => {
  await page.context().clearCookies();
  await page.goto('/staff/sign-in');
  await page.getByLabel(/mobile number or email/i).fill(accountsFixtureIds.nurseIdentifier);
  await page.getByLabel(/^password/i).fill(accountsFixtureIds.nursePassword);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page).toHaveURL(/\/staff\//);

  await page.goto('/staff/notifications');
  await expect(page.getByText(/you don.t have permission to do this/i)).toBeVisible();
  await expect(page.getByRole('table', { name: 'Message log' })).toHaveCount(0);

  // The templates screen is refused the same way for a nurse.
  await page.goto('/staff/admin/notification-templates');
  await expect(page.getByText(/you don.t have permission to do this/i)).toBeVisible();
});
