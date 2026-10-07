import { test, expect, signInAsStaff } from './fixtures';
import { openSheetAt360 } from './staff-shell-sheet-helpers';

// ARCH gate MEDIUM (first pass) — signs in as staff only (never both cookies at
// once, tests/e2e/fixtures.ts); every route this file visits is staff-audience.
test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

/**
 * HO-CAT-002 TEST gate, fourth pass, rewritten for the 7th pass's Radix-`Dialog` rebuild.
 *
 * Original finding: a manual expand below `sm` made the in-flow `<aside>` `max-sm:fixed` at 240px
 * with no scrim, so it silently covered a band of `<main>` (e.g. half of "Register a cat") without
 * signalling that the content underneath was temporarily inert — a "clipped control" by UI-1's own
 * definition. Fixed at the time by adding a scrim (`8379121`), and superseded entirely by the 7th
 * pass: below `sm`, the in-flow `<aside>` can no longer widen at all (it's always the w-16 rail —
 * `staff-shell.tsx`'s own top comment), so there is no longer an in-flow overlay to reproduce this
 * against. "Expand menu" now opens a real Radix `Dialog` sheet instead.
 *
 * The equivalent, still-live contract: while that sheet is open, nothing behind its own overlay
 * is reachable by mouse, and both ways out (overlay click, `Escape`) leave "Register a cat" fully
 * clickable again. Radix's `DialogOverlay` (`components/ui/dialog.tsx`) is a real `fixed inset-0`
 * element above the rest of the page (`z-50`), so it — not a hand-rolled scrim — is what a pointer
 * hits everywhere outside the sheet's own content.
 *
 * `whatIsAtRegisterButtonEdge` locates "Register a cat" by its own text, not `getByRole` — while
 * the sheet is open, Radix's `modal` behaviour marks everything outside its portal
 * `aria-hidden="true"` (see `staff-shell-sheet-helpers.ts`'s own comment), which would make a role
 * query for it time out for exactly the open half of every test below.
 */
async function whatIsAtRegisterButtonEdge(page: import('@playwright/test').Page) {
  const box = await page.locator('button', { hasText: 'Register a cat' }).boundingBox();
  if (!box) throw new Error('Register a cat button has no bounding box');
  return page.evaluate(
    ([x, y]: [number, number]) => {
      const el = document.elementFromPoint(x, y);
      if (!el) return 'nothing';
      if (el.closest('[role="dialog"]')) return 'sheet';
      if (el.closest('button')?.textContent?.includes('Register a cat')) return 'button';
      return 'other';
    },
    [box.x + 2, box.y + box.height / 2] as [number, number],
  );
}

test.describe('StaffShell nav sheet at 360px (HO-CAT-002 TEST gate, 4th pass, rebuilt 7th pass)', () => {
  test('while open, its overlay covers the content; clicking outside the sheet closes it', async ({
    page,
  }) => {
    await openSheetAt360(page);
    expect(await whatIsAtRegisterButtonEdge(page)).not.toBe('button');

    // Click well clear of the sheet's own content (it's a left-docked w-60/85vw panel).
    await page.mouse.click(340, 450);
    await expect(page.getByRole('dialog')).toHaveCount(0);
    expect(await whatIsAtRegisterButtonEdge(page)).toBe('button');
  });

  test('Escape closes the sheet, returns focus to the trigger and unblocks the content', async ({
    page,
  }) => {
    const { trigger } = await openSheetAt360(page);
    await page.keyboard.press('Escape');
    await expect(page.getByRole('dialog')).toHaveCount(0);
    await expect(trigger).toBeFocused();
    expect(await whatIsAtRegisterButtonEdge(page)).toBe('button');
  });

  test('no sheet at 1440px, expanded or collapsed', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/staff/cats');
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('dialog')).toHaveCount(0);
    await page.getByRole('button', { name: /collapse menu/i }).click();
    await page.getByRole('button', { name: /expand menu/i }).click();
    await expect(page.getByRole('dialog')).toHaveCount(0);
    expect(await whatIsAtRegisterButtonEdge(page)).toBe('button');
  });
});
