import type { Page } from '@playwright/test';

/** HO-FIL-002's own test-only harness route — see `app/dev/file-storage-harness/page.tsx`'s doc
 *  comment and HO-FIL-002 §1 Amended (2026-09-27, "test-only harness"). */
export const FILES_HARNESS_PATH = '/dev/file-storage-harness';

export type FileHarnessState = 'idle' | 'uploading' | 'error' | 'populated' | 'viewer-open';

const TINY_BUFFER = Buffer.from([0x89, 0x50, 0x4e, 0x47]);

/**
 * Drives the harness page to one of §16/§18's screens/states, using the real `<FileUploader>` UI
 * (a real `page.setInputFiles` pick, never a shortcut that bypasses it) so the axe/overflow specs
 * exercise exactly what a person would see. Each call starts from a fresh navigation — MSW's
 * browser-mode module state (the upload gates) lives in the page's own JS realm and is reset by a
 * full navigation, so tests never leak a gate between each other.
 */
export async function reachHarnessState(page: Page, state: FileHarnessState): Promise<void> {
  await page.goto(FILES_HARNESS_PATH);
  await page.waitForLoadState('networkidle');

  switch (state) {
    case 'idle':
      return;

    case 'uploading': {
      await page.evaluate(() => {
        (
          window as unknown as { __fileUploadGates: { arm: (name: string) => void } }
        ).__fileUploadGates.arm('e2e');
      });
      await page
        .locator('input[type="file"]')
        .setInputFiles({ name: 'gate-e2e-photo.png', mimeType: 'image/png', buffer: TINY_BUFFER });
      await page.getByRole('progressbar').waitFor({ state: 'visible' });
      return;
    }

    case 'error': {
      await page
        .locator('input[type="file"]')
        .setInputFiles({ name: 'reject-photo.png', mimeType: 'image/png', buffer: TINY_BUFFER });
      await page.getByText(/rejected by the virus scanner/).waitFor({ state: 'visible' });
      return;
    }

    case 'populated': {
      await page.evaluate(() => {
        (
          window as unknown as { __fileStorageHarness: { addFreshFile: () => void } }
        ).__fileStorageHarness.addFreshFile();
      });
      await page.getByRole('listitem').first().waitFor({ state: 'visible' });
      return;
    }

    case 'viewer-open': {
      await page.evaluate(() => {
        (
          window as unknown as { __fileStorageHarness: { addFreshFile: () => void } }
        ).__fileStorageHarness.addFreshFile();
      });
      await page
        .getByRole('button', { name: /^Photo:/ })
        .first()
        .click();
      await page.getByRole('dialog').waitFor({ state: 'visible' });
      return;
    }

    default: {
      const exhaustive: never = state;
      throw new Error(`Unhandled harness state: ${String(exhaustive)}`);
    }
  }
}
