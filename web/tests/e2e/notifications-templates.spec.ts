import { test, expect, signInAsStaff } from './fixtures';

/**
 * HO-NTF-002 §16 — E-C1, E-C2 against MSW (the real-API column is recorded in
 * `docs/test-cases/TC-HO-NTF-002.md`). The email preview iframe is proved inert in a real browser
 * here: the mock's preview HTML carries a `<script>` canary that would set
 * `window.__previewScriptRan` if it ever ran outside the `sandbox=""` frame.
 */

const EDITOR = '/staff/admin/notification-templates/care.vaccination-due';

test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

test('the template list links to the editor from the admin navigation', async ({ page }) => {
  await page.goto('/staff/admin/branches');
  await page.getByRole('link', { name: 'Message templates' }).click();
  await expect(page).toHaveURL(/\/staff\/admin\/notification-templates$/);
  await expect(page.getByRole('heading', { level: 1, name: 'Message templates' })).toBeVisible();
  await expect(page.getByRole('cell', { name: 'Customised' })).toBeVisible();

  await page.getByRole('link', { name: 'Vaccination due' }).click();
  await expect(page).toHaveURL(/care\.vaccination-due$/);
  await expect(page.getByRole('heading', { level: 1, name: 'Vaccination due' })).toBeVisible();
});

test('the email preview is a sandbox="" iframe and its script never runs', async ({ page }) => {
  await page.goto(EDITOR);
  const frame = page.getByTitle('Email preview');
  await expect(frame).toBeVisible();
  await expect(frame).toHaveAttribute('sandbox', '');
  expect(await frame.getAttribute('srcdoc')).toContain('<script>');
  // Give any (wrongly) executing script a moment to fire, then check the canary.
  await page.waitForTimeout(500);
  expect(
    await page.evaluate(
      () => (window as unknown as { __previewScriptRan?: boolean }).__previewScriptRan,
    ),
  ).toBeUndefined();
});

test('E-C1: {{catname}} is refused with the variable named, in the preview and on save', async ({
  page,
}) => {
  await page.goto(EDITOR);
  await page.getByRole('tab', { name: /Text message/ }).click();
  await page.getByLabel('SMS message').fill('Hello {{catname}}, book at {{bookingLink}}');

  // The preview (server-rendered, debounced) names the variable, in the panel and on the field.
  const panelError = page.getByTestId('preview-error');
  await expect(panelError).toContainText('{{catname}} is not a variable of this template.');
  await expect(page.locator('#tpl-1-body-error')).toContainText(
    '{{catname}} is not a variable of this template.',
  );
  // Typing was not blocked.
  await page.getByLabel('SMS message').press('End');
  await page.getByLabel('SMS message').pressSequentially(' ok');
  await expect(page.getByLabel('SMS message')).toHaveValue(/ ok$/);

  // Save also refuses, with the same named variable.
  const saveResponse = page.waitForResponse(
    (r) => r.url().includes('/api/admin/notification-templates/') && r.request().method() === 'PUT',
  );
  await page.getByRole('button', { name: 'Save changes' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Save changes' }).click();
  expect((await saveResponse).status()).toBe(400);
  await expect(page.locator('#tpl-1-body-error')).toContainText('{{catname}}');
  // The typed value is still there.
  await expect(page.getByLabel('SMS message')).toHaveValue(/\{\{catname\}\}.* ok$/);
});

test('E-C2: fix it, save, then reset to default (Customised -> Default)', async ({ page }) => {
  await page.goto(EDITOR);
  await expect(page.getByText('Customised', { exact: true })).toBeVisible();

  await page.getByRole('tab', { name: /Text message/ }).click();
  await page
    .getByLabel('SMS message')
    .fill(
      'PurrCare: time for {{catName}} to have the {{vaccineName}} vaccine. Book: {{bookingLink}}',
    );
  // The preview shows the filled-in sample ("Momo"), rendered by the server.
  await expect(page.getByTestId('sms-preview-text')).toContainText('time for Momo to have');
  await expect(page.getByText(/characters · \d+ SMS · (GSM-7|Unicode)/)).toBeVisible();

  await page.getByRole('button', { name: 'Save changes' }).click();
  const dialog = page.getByRole('dialog');
  await expect(
    dialog.getByText(
      'Messages sent from now on will use this wording. Messages already sent keep theirs.',
    ),
  ).toBeVisible();
  await dialog.getByRole('button', { name: 'Save changes' }).click();
  await expect(page.getByText(/Saved\. Messages sent from now on/)).toBeVisible();

  await page.getByRole('button', { name: 'Reset to default' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Reset to default' }).click();
  await expect(page.getByText('Reset to the default wording.')).toBeVisible();
  await expect(page.getByText('Default', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Reset to default' })).toHaveCount(0);

  // The shipped SMS wording is back.
  await page.getByRole('tab', { name: /Text message/ }).click();
  await expect(page.getByLabel('SMS message')).toHaveValue(/vaccine is due on \{\{dueOn\}\}/);
});

test('placeholder palette inserts at the caret in a real browser', async ({ page }) => {
  await page.goto('/staff/admin/notification-templates/care.deworming-due');
  await page.getByRole('tab', { name: /Text message/ }).click();
  const sms = page.getByLabel('SMS message');
  await sms.fill('Hello  there');
  await sms.evaluate((el: HTMLTextAreaElement) => {
    el.focus();
    el.setSelectionRange(6, 6);
  });
  await page.getByRole('button', { name: 'Insert {{catName}}' }).click();
  await expect(sms).toHaveValue('Hello {{catName}} there');
  const caret = await sms.evaluate((el: HTMLTextAreaElement) => el.selectionStart);
  expect(caret).toBe(6 + '{{catName}}'.length);
});
