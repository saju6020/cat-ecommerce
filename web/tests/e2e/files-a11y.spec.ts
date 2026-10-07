import { test, expect } from './fixtures';
import AxeBuilder from '@axe-core/playwright';
import { reachHarnessState, type FileHarnessState } from './files-harness';

/**
 * C9 (HO-FIL-002 §15) — axe on the uploader (idle, uploading, error) and the viewer, no
 * violations. Extended to every screen/state this slice has, per the task brief's own instruction:
 * checked at 360/768/1024/1440 (not just the UI-1 floor), covering the error and progress variants
 * explicitly, and asserting the intended state actually rendered before scanning it — a false
 * "no violations" from a state that silently failed to reach would be worse than no test at all.
 */
const BREAKPOINTS = [360, 768, 1024, 1440];

const STATES: Array<{
  state: FileHarnessState;
  assertRendered: (page: import('@playwright/test').Page) => Promise<void>;
}> = [
  {
    state: 'idle',
    assertRendered: async (page) => {
      await expect(page.getByRole('button', { name: /add product photos/i })).toBeVisible();
    },
  },
  {
    state: 'uploading',
    assertRendered: async (page) => {
      await expect(page.getByRole('progressbar')).toBeVisible();
    },
  },
  {
    state: 'error',
    assertRendered: async (page) => {
      await expect(page.getByText(/rejected by the virus scanner/)).toBeVisible();
    },
  },
  {
    state: 'populated',
    assertRendered: async (page) => {
      await expect(page.getByRole('listitem')).toHaveCount(1);
    },
  },
  {
    state: 'viewer-open',
    assertRendered: async (page) => {
      await expect(page.getByRole('dialog')).toBeVisible();
    },
  },
];

for (const width of BREAKPOINTS) {
  for (const { state, assertRendered } of STATES) {
    test(`C9 — the ${state} state has no serious/critical accessibility violations at ${width}px`, async ({
      page,
    }) => {
      await page.setViewportSize({ width, height: 900 });
      await reachHarnessState(page, state);
      await assertRendered(page);

      const results = await new AxeBuilder({ page }).analyze();
      const serious = results.violations.filter(
        (v) => v.impact === 'serious' || v.impact === 'critical',
      );
      expect(serious, JSON.stringify(serious, null, 2)).toEqual([]);
    });
  }
}
