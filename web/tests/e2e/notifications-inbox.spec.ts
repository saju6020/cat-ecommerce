import { test, expect, signInAsCustomer } from './fixtures';

/**
 * HO-NTF-002 §16 — E-A1, E-A2, E-B1 against MSW.
 *
 * Real-API runs (HO-NTF-002 §16 "MSW first, then the real API"): E-A1 against the real API needs a
 * cat with a vaccination due in 14 days and an Api restart so the reminder sweep runs once at
 * start; that cannot be arranged from this worktree (no database, no API process), so the real-API
 * column of E-A1/E-A2/E-B1 is recorded as NOT RUNNABLE in `docs/test-cases/TC-HO-NTF-002.md`.
 * Against MSW the seeded inbox stands in for the swept reminder.
 */

test.beforeEach(async ({ page }) => {
  await signInAsCustomer(page);
});

test('E-A1: the inbox shows the reminder with a View link to the cat page, and the badge counts unread messages', async ({
  page,
}) => {
  await page.goto('/portal/inbox');
  await expect(page.getByRole('heading', { level: 1, name: 'Inbox' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Inbox, 3 unread messages' })).toBeVisible();

  await page.getByRole('button', { name: /Momo is due a vaccination/ }).click();
  const view = page.getByRole('link', { name: 'View', exact: true });
  await expect(view).toHaveAttribute('href', /^\/portal\/cats\//);
});

test('E-A2: opening an item clears it from the badge count', async ({ page }) => {
  await page.goto('/portal/inbox');
  await expect(page.getByRole('link', { name: 'Inbox, 3 unread messages' })).toBeVisible();
  await page.getByRole('button', { name: /Momo is due a vaccination/ }).click();
  await expect(page.getByRole('link', { name: 'Inbox, 2 unread messages' })).toBeVisible();

  // The Unread filter now omits the opened item and is kept in the URL.
  await page.getByRole('button', { name: 'Unread', exact: true }).click();
  await expect(page).toHaveURL(/filter=unread/);
  await expect(page.getByRole('button', { name: /Momo is due a vaccination/ })).toHaveCount(0);
});

test('an unsafe linkPath produces no View link and markup in a message is shown as text', async ({
  page,
}) => {
  await page.goto('/portal/inbox');
  await page.getByRole('button', { name: /A message with an unsafe link/ }).click();
  await expect(page.getByText('This item carries a protocol-relative link')).toBeVisible();
  await expect(page.getByRole('link', { name: 'View', exact: true })).toHaveCount(0);

  await page.getByRole('button', { name: /<b>Bold title<\/b>/ }).click();
  await expect(page.getByText('<b>hi</b> and **x**', { exact: false }).first()).toBeVisible();
  expect(await page.locator('main b, main strong').count()).toBe(0);
});

test('E-B1: changing the preferred channel to email and saving survives a reload', async ({
  page,
}) => {
  await page.goto('/portal/account/notifications');
  await expect(page.getByRole('radio', { name: 'Text message (SMS)' })).toBeChecked();
  await page.getByRole('radio', { name: 'Email' }).check();
  await page.getByRole('button', { name: 'Save changes' }).click();
  await expect(page.getByText('Your notification preferences have been saved.')).toBeVisible();

  await page.reload();
  await expect(page.getByRole('radio', { name: 'Email' })).toBeChecked();
});
