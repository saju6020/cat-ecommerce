import { test, expect, signInAsCustomer } from './fixtures';

/** Addresses and notification preferences — HO-ACC-002 §16 E-E series, run against MSW. */

test.beforeEach(async ({ page }) => {
  await signInAsCustomer(page);
});

test('E-E1: add three addresses, change default, remove one — one default throughout', async ({
  page,
}) => {
  await page.goto('/portal/account/addresses');

  for (const label of ['Home', 'Office', 'Parents']) {
    await page.getByRole('button', { name: 'Add address' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel('Label').fill(label);
    await dialog.getByLabel('Address line 1').fill(`1 ${label} Road`);
    await dialog.getByLabel('City').fill('Dhaka');
    await dialog.getByLabel('District').fill('Dhaka');
    await dialog.getByRole('button', { name: 'Save address' }).click();
    await expect(dialog).not.toBeVisible();
  }

  // `exact: true` — the badge's own text is exactly "Default", but every ancestor <div> also
  // contains that substring once another element's own text (the address label, "Set as
  // default", …) is concatenated in, so a loose `getByText('Default')` over-matches ancestors.
  await expect(page.getByText('Default', { exact: true })).toHaveCount(1);

  // The innermost (deepest, `.last()` in document order) matching <div> is the AddressCard's own
  // root — every broader ancestor also matches both text filters, since text content bubbles up.
  const officeCard = page
    .locator('div')
    .filter({ hasText: 'Office' })
    .filter({ hasText: 'Set as default' })
    .last();
  await officeCard.getByRole('button', { name: 'Set as default' }).click();
  await expect(page.getByText('Default', { exact: true })).toHaveCount(1);

  const homeCard = page
    .locator('div')
    .filter({ hasText: 'Home' })
    .filter({ hasText: 'Remove' })
    .last();
  await homeCard.getByRole('button', { name: 'Remove' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Remove', exact: true }).click();
  await expect(page.getByText('Home')).toHaveCount(0);
  await expect(page.getByText('Default', { exact: true })).toHaveCount(1);
});

test('E-E2: opt into SMS marketing, save, reload — persisted; transactional cells locked', async ({
  page,
}) => {
  await page.goto('/portal/account/notifications');
  await expect(page.getByRole('table')).toBeVisible();

  const smsMarketing = page.getByLabel('Marketing by SMS');
  await smsMarketing.check();
  await page.getByRole('button', { name: 'Save changes' }).click();
  await expect(page.getByText(/preferences have been saved/i)).toBeVisible();

  await page.reload();
  await expect(page.getByLabel('Marketing by SMS')).toBeChecked();

  const alwaysOnCells = page.getByText('Always on');
  expect(await alwaysOnCells.count()).toBeGreaterThan(0);
  const firstAlwaysOnCheckbox = page.locator('input[type="checkbox"][disabled]').first();
  await expect(firstAlwaysOnCheckbox).toBeDisabled();
});

// ARCH gate MEDIUM (second pass) — UnsavedChangesGuard's sentinel-history-entry technique for the
// browser's own Back button, exercised end to end (the unit tests in
// unsaved-changes-guard.test.tsx simulate the popstate event directly; this is the real trigger).
test('unsaved changes on the profile form: the browser Back button is intercepted, and confirming Leave completes it', async ({
  page,
}) => {
  await page.goto('/portal/cats');
  await page.goto('/portal/account/profile');

  // Not an anchored regex: the label's accessible name carries trailing content from its own
  // `<span aria-hidden="true">*</span>` required-field marker (ProfileForm.tsx), so `/^full
  // name$/i` never matches and this locator would time out forever — plain-string `getByLabel`
  // (substring match) is what every other spec touching this exact field already uses
  // (accounts-auth.spec.ts's E-C2/E-C3).
  const fullNameField = page.getByLabel('Full name');
  await fullNameField.fill('Changed But Not Saved');

  await page.goBack();

  // Still on the profile page — the sentinel entry absorbed the Back press and re-pushed itself,
  // so the URL never actually changed; the dialog is what appeared instead.
  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await expect(dialog).toContainText(/leave this page/i);
  await expect(page).toHaveURL(/\/portal\/account\/profile/);
  await expect(fullNameField).toHaveValue('Changed But Not Saved');

  await dialog.getByRole('button', { name: /^leave$/i }).click();

  // The ORIGINAL Back intent is replayed for real now — lands on the page visited before.
  await expect(page).toHaveURL(/\/portal\/cats/);
});

test('unsaved changes on the profile form: Cancel after a Back press keeps the draft and stays put', async ({
  page,
}) => {
  await page.goto('/portal/cats');
  await page.goto('/portal/account/profile');

  // See the sibling test above for why this is a plain string, not an anchored regex.
  const fullNameField = page.getByLabel('Full name');
  await fullNameField.fill('Still Editing');

  await page.goBack();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await dialog.getByRole('button', { name: /cancel/i }).click();

  await expect(dialog).toBeHidden();
  await expect(page).toHaveURL(/\/portal\/account\/profile/);
  await expect(fullNameField).toHaveValue('Still Editing');
});

// CODE_REVIEW gate third pass, MEDIUM — an ordinary successful save (the form goes clean WITHOUT
// ever navigating away) used to leave the sentinel pushed while dirty sitting in history forever,
// which then swallowed exactly one Back press afterward (landing back on this same, already-saved
// page before a second press actually left). The fix pops that now-stray sentinel the moment the
// form goes clean; this proves it end to end, in a real browser, one Back press at a time.
test('unsaved changes on the profile form: after a successful save, one Back press goes straight to the previous page', async ({
  page,
}) => {
  await page.goto('/portal/cats');
  await page.goto('/portal/account/profile');

  const fullNameField = page.getByLabel('Full name');
  await fullNameField.fill('Saved For Real');
  await page.getByRole('button', { name: /^save$/i }).click();
  await expect(page.getByText(/profile has been saved/i)).toBeVisible();

  await page.goBack();

  // One press, straight through — no ConfirmDialog (the form is clean, nothing to guard) and no
  // stray sentinel left behind to absorb this press instead of actually leaving.
  await expect(page.getByRole('dialog')).not.toBeVisible();
  await expect(page).toHaveURL(/\/portal\/cats/);
});

// ARCHITECTURE gate third pass, LOW (a) — when the guarded page is the tab's very first history
// entry (a genuine deep link, e.g. a bookmark or an emailed link, has nothing behind it), no
// sentinel is pushed at all, so Back is left to the browser's own native handling — this proves
// that case does not hang, throw, or otherwise misbehave; the dialog correctly never appears, since
// there is no cancellable event for this component to have intercepted in the first place.
// Playwright's own new-page-then-goto always leaves a real about:blank entry behind any first
// `page.goto()`, which a genuine deep link would not — `history.length` is pinned to 1 via
// `addInitScript` to reproduce the true first-entry case deterministically rather than depend on
// browser-engine-specific about:blank counting.
test('unsaved changes on the profile form: a deep-linked first history entry does not break Back', async ({
  page,
}) => {
  await page.addInitScript(() => {
    Object.defineProperty(window.history, 'length', { value: 1, configurable: true });
  });
  await page.goto('/portal/account/profile');

  const fullNameField = page.getByLabel('Full name');
  await fullNameField.fill('Never Saved');

  await page.goBack();

  // No dialog — this component never had a cancellable event to intercept, by design, in this
  // case; the browser's own Back handling is simply left to run, unintercepted and unbroken.
  await expect(page.getByRole('dialog')).not.toBeVisible();
});
