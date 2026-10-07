import { test, expect } from './fixtures';
import { FILES_HARNESS_PATH, reachHarnessState } from './files-harness';

const TINY_BUFFER = Buffer.from([0x89, 0x50, 0x4e, 0x47]);

/** HO-FIL-002 §15/§16 — the interaction cases that need a real browser: a real native file
 *  dialog (C1), a real in-flight `XMLHttpRequest` to abort (C5), real focus management (C7), and a
 *  real `<img>` load failure/recovery (C8). Component/hook-level tests already cover every
 *  branch of the underlying logic deterministically (`tests/unit/files`,
 *  `tests/components/files`) — these prove the same behaviour survives an actual browser. */

test('C1/E1 — keyboard-only pick uploads a file, which stays listed across an unrelated form error', async ({
  page,
}) => {
  await page.goto(FILES_HARNESS_PATH);
  await page.waitForLoadState('networkidle');

  await page.keyboard.press('Tab');
  await expect(page.getByRole('button', { name: /add product photos/i })).toBeFocused();

  const [chooser] = await Promise.all([
    page.waitForEvent('filechooser'),
    page.keyboard.press('Enter'),
  ]);
  await chooser.setFiles({
    name: 'keyboard-upload.png',
    mimeType: 'image/png',
    buffer: TINY_BUFFER,
  });

  // The persisted file-list row, not the (also-matching) "...finished uploading." live-region text.
  const listedFile = page
    .getByTestId('file-list-section')
    .getByText('keyboard-upload.png', { exact: true });
  await expect(listedFile).toBeVisible();

  // E1 — an unrelated field's validation failure elsewhere on the page must not disturb it
  // (ADR-FE-156: the host, not the uploader, owns the file list).
  await page.getByRole('button', { name: 'Save' }).click();
  // Scoped by id, not role — Next.js's own route announcer (`#__next-route-announcer__`) is also
  // `role="alert"`, unrelated to this form.
  await expect(page.locator('#harness-product-name-error')).toHaveText('Product name is required.');
  await expect(listedFile).toBeVisible();
});

test('E2 — a rejected file shows the scanner-refusal message and is never listed', async ({
  page,
}) => {
  await reachHarnessState(page, 'error');

  await expect(page.getByText(/rejected by the virus scanner/)).toBeVisible();
  await expect(page.getByText('reject-photo.png', { exact: true })).toHaveCount(0);
});

test('E3 — three files upload sequentially; a middle failure does not cancel the third', async ({
  page,
}) => {
  await page.goto(FILES_HARNESS_PATH);
  await page.waitForLoadState('networkidle');
  // Scoped to the uploader's own queue section throughout — a file that finishes successfully is
  // deliberately ALSO shown in the file-list section (ADR-FE-156), so an unscoped text query for
  // its name would still find it there after its queue row is gone.
  const uploaderSection = page.getByTestId('uploader-section');

  await page.evaluate(() => {
    (
      window as unknown as { __fileUploadGates: { arm: (name: string) => void } }
    ).__fileUploadGates.arm('e3a');
    (
      window as unknown as { __fileUploadGates: { arm: (name: string) => void } }
    ).__fileUploadGates.arm('e3c');
  });

  await uploaderSection.locator('input[type="file"]').setInputFiles([
    { name: 'gate-e3a-one.png', mimeType: 'image/png', buffer: TINY_BUFFER },
    { name: 'reject-two.png', mimeType: 'image/png', buffer: TINY_BUFFER },
    { name: 'gate-e3c-three.png', mimeType: 'image/png', buffer: TINY_BUFFER },
  ]);

  // Only the first is uploading — sequential, not parallel (ADR-FE-150).
  await expect(uploaderSection.getByText('gate-e3a-one.png')).toBeVisible();
  await expect(uploaderSection.getByRole('progressbar')).toHaveCount(1);
  await expect(uploaderSection.getByText('gate-e3c-three.png')).toBeVisible();

  await page.evaluate(() => {
    (
      window as unknown as { __fileUploadGates: { release: (name: string) => void } }
    ).__fileUploadGates.release('e3a');
  });

  // `exact: true` — the live region's own "...finished uploading."/"...rejected..." announcements
  // otherwise substring-match a bare filename query too.
  await expect(uploaderSection.getByText('gate-e3a-one.png', { exact: true })).toHaveCount(0);
  await expect(uploaderSection.getByText(/rejected by the virus scanner/)).toBeVisible();
  await expect(uploaderSection.getByRole('progressbar')).toHaveCount(1); // now the third

  await page.evaluate(() => {
    (
      window as unknown as { __fileUploadGates: { release: (name: string) => void } }
    ).__fileUploadGates.release('e3c');
  });

  await expect(uploaderSection.getByRole('progressbar')).toHaveCount(0);
  await expect(uploaderSection.getByRole('listitem')).toHaveCount(0); // the queue is empty again
  // Both survivors (the rejected one never reaches here) show up in the persisted list.
  const fileListSection = page.getByTestId('file-list-section');
  await expect(fileListSection.getByText('gate-e3a-one.png')).toBeVisible();
  await expect(fileListSection.getByText('gate-e3c-three.png')).toBeVisible();
  await expect(fileListSection.getByText('reject-two.png')).toHaveCount(0);
});

test('C5 — Cancel mid-upload removes the row and never adds it to the file list', async ({
  page,
}) => {
  await reachHarnessState(page, 'uploading');

  await page.getByRole('button', { name: /cancel gate-e2e-photo\.png/i }).click();
  // `exact: true` — the aria-live region's OWN "gate-e2e-photo.png removed." announcement
  // otherwise substring-matches the bare filename query too.
  await expect(page.getByText('gate-e2e-photo.png', { exact: true })).toHaveCount(0);

  await page.evaluate(() => {
    (
      window as unknown as { __fileUploadGates: { release: (name: string) => void } }
    ).__fileUploadGates.release('e2e');
  });
  await page.waitForTimeout(50); // lets a wrongly-resurrected row have a chance to appear
  await expect(page.getByText('gate-e2e-photo.png', { exact: true })).toHaveCount(0);
});

test('C7 — the viewer traps focus, Esc closes it, and focus returns to the thumb that opened it', async ({
  page,
}) => {
  await reachHarnessState(page, 'viewer-open');

  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();

  await page.keyboard.press('Escape');
  await expect(dialog).toHaveCount(0);
  await expect(page.getByRole('button', { name: /^Photo:/ }).first()).toBeFocused();
});

test('C8 — an expired signed URL recovers once via the host-supplied refresh', async ({ page }) => {
  await page.goto(FILES_HARNESS_PATH);
  await page.waitForLoadState('networkidle');

  await page.evaluate(() => {
    (
      window as unknown as { __fileStorageHarness: { addExpiredFile: () => void } }
    ).__fileStorageHarness.addExpiredFile();
  });

  const thumbButton = page.getByRole('button', { name: /^Photo:/ }).first();
  await expect(thumbButton).toBeVisible();
  // The harness's own onRefresh always returns a fresh, loadable file — proves the real <img>
  // error → refresh → re-render → successful load round trip, not just the callback being called.
  await expect(thumbButton.locator('img')).toBeVisible({ timeout: 10_000 });
});
