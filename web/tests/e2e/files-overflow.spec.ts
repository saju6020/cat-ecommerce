import { test, expect } from './fixtures';
import { reachHarnessState, type FileHarnessState } from './files-harness';

/**
 * C10 (HO-FIL-002 §15) — no horizontal scroll, and the control stays operable, at narrow widths.
 * HO-FIL-002 §15 names 320px; this project's own established floor (UI-1, `cat-responsive.spec.ts`)
 * is 360px, matched by `playwright.config.ts`'s `mobile-360` project — checked here at that floor
 * plus 768/1024/1440, across every state, per the task brief's instruction.
 */
const BREAKPOINTS = [360, 768, 1024, 1440];

const STATES: FileHarnessState[] = ['idle', 'uploading', 'error', 'populated', 'viewer-open'];

async function assertNoHorizontalScroll(page: import('@playwright/test').Page) {
  const { scrollWidth, clientWidth } = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
  }));
  expect(
    scrollWidth,
    `overflows horizontally (scrollWidth=${scrollWidth}, clientWidth=${clientWidth})`,
  ).toBeLessThanOrEqual(
    clientWidth + 1, // +1px tolerance for sub-pixel rounding
  );
}

for (const width of BREAKPOINTS) {
  for (const state of STATES) {
    test(`C10 — the ${state} state has no horizontal scroll at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 });
      await reachHarnessState(page, state);
      await assertNoHorizontalScroll(page);
    });
  }

  test(`C10 — the drop-zone control is still operable by keyboard at ${width}px`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 900 });
    await reachHarnessState(page, 'idle');

    await page.keyboard.press('Tab');
    await expect(page.getByRole('button', { name: /add product photos/i })).toBeFocused();
  });
}
