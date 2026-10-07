import { test, expect, signInAsCustomer } from './fixtures';

/**
 * HO-NTF-002 §16 — E-D1 against MSW only. No marketing template ships in this slice, so the
 * real-API column is recorded as NOT RUNNABLE until the first one exists (HO-SHP back-in-stock);
 * HO-NTF-001 I-E1…I-E3 prove the server half.
 */

test('E-D1: an unsubscribe link works, never acts on page load, and the matrix then shows email marketing off', async ({
  page,
}) => {
  await signInAsCustomer(page);

  // Start with email marketing ON so the unsubscribe has something to turn off.
  await page.goto('/portal/account/notifications');
  const emailMarketing = page.getByRole('checkbox', { name: /Marketing by Email/ });
  await emailMarketing.check();
  await page.getByRole('button', { name: 'Save changes' }).click();
  await expect(page.getByText('Your notification preferences have been saved.')).toBeVisible();

  // Open the link from a "marketing email". Nothing may be POSTed until the click.
  const posts: string[] = [];
  page.on('request', (request) => {
    if (request.method() === 'POST' && request.url().includes('/api/notifications/unsubscribe')) {
      posts.push(request.url());
    }
  });
  await page.goto('/unsubscribe#token=valid-email-token');
  await expect(page.getByRole('heading', { name: 'Stop marketing messages?' })).toBeVisible();
  await page.waitForTimeout(500);
  expect(posts).toHaveLength(0);
  // The token is gone from the address bar as soon as the page has read it.
  expect(page.url()).not.toContain('valid-email-token');
  expect(new URL(page.url()).hash).toBe('');

  await page.getByRole('button', { name: 'Unsubscribe' }).click();
  await expect(
    page.getByText("Done. You won't receive marketing emails from us any more."),
  ).toBeVisible();
  expect(posts).toHaveLength(1);

  await page.getByRole('link', { name: 'Manage all your preferences' }).click();
  await expect(page).toHaveURL(/\/portal\/account\/notifications|\/sign-in/);
  await page.goto('/portal/account/notifications');
  await expect(page.getByRole('checkbox', { name: /Marketing by Email/ })).not.toBeChecked();
});

test('an invalid or missing link is explained, with no Unsubscribe button', async ({ page }) => {
  await page.goto('/unsubscribe#token=invalid-token');
  await page.getByRole('button', { name: 'Unsubscribe' }).click();
  await expect(page.getByText(/This link isn.t valid any more/)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Unsubscribe' })).toHaveCount(0);

  await page.goto('/unsubscribe');
  await expect(page.getByText(/This link isn.t valid any more/)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Unsubscribe' })).toHaveCount(0);
});
