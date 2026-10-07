import { test, expect, signInAsStaff } from './fixtures';

// ARCH gate MEDIUM (first pass) — signs in as staff only (never both cookies at
// once, tests/e2e/fixtures.ts); every route this file visits is staff-audience.
test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

/**
 * HO-CAT-002 TEST gate, second pass, updated for the 7th pass's Radix-`Dialog` rebuild of the
 * below-`sm` drawer.
 *
 * Original finding (`dbba453` era): the toggle's first click unmounted a two-button `<Fragment>`
 * and mounted a single `<Button>` in its place, so the browser dropped keyboard focus to `<body>`.
 * Fixed by making both per-viewport controls always-mounted, CSS-hidden rather than swapped —
 * unchanged by the 7th pass's rebuild, and still the reason this file exists.
 *
 * What each control *does* on click did change in the 7th pass: below `sm`, `StaffShell`'s
 * `<aside>` can no longer widen at all (it's always the w-16 rail — see `staff-shell.tsx`'s own
 * top comment), so the below-`sm` control now opens a full Radix `Dialog` sheet instead of
 * expanding the aside in place. At `sm` and up, the control is exactly what it always was: the
 * in-flow collapse/expand toggle. The contract this file checks — first activation must not drop
 * keyboard focus to `<body>` — holds, and is if anything strengthened at 360px: Radix moves focus
 * into the sheet's own content rather than leaving it on the toggle or losing it altogether.
 */
test.describe('StaffShell sidebar toggle — keyboard focus (HO-CAT-002 TEST gate, 2nd pass)', () => {
  for (const width of [360, 1440]) {
    test(`first activation of the toggle keeps keyboard focus @ ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 });
      await page.goto('/staff/cats');
      await page.waitForLoadState('networkidle');

      // Tab until focus reaches whichever toggle is visible/focusable at this width — CSS hides
      // its sibling (`display: none`), which also removes it from the tab order.
      let reachedToggle = false;
      for (let i = 0; i < 15 && !reachedToggle; i++) {
        await page.keyboard.press('Tab');
        const label = await page.evaluate(
          () => (document.activeElement as HTMLElement | null)?.getAttribute('aria-label') ?? null,
        );
        reachedToggle = label === 'Expand menu' || label === 'Collapse menu';
      }
      expect(reachedToggle).toBe(true);

      await page.keyboard.press('Enter');

      // This is the assertion that failed before `7304f85`: focus should still be on *some*
      // element that is part of the page's normal interactive flow (at minimum, not <body> — the
      // browser's "nothing is focused" fallback), so a keyboard/screen-reader user doesn't lose
      // their place. Below sm this is now Radix moving focus into the opened sheet; at sm+ it's
      // still the same toggle button, unmoved.
      const activeIsBody = await page.evaluate(() => document.activeElement === document.body);
      expect(activeIsBody, "focus fell back to <body> after the toggle's first activation").toBe(
        false,
      );

      if (width < 640) {
        await expect(page.getByRole('dialog')).toBeVisible();
      } else {
        // Its accessible name just flipped ("Collapse menu" -> "Expand menu"), so re-querying by
        // the pre-click name would find nothing — check the live focused element directly instead.
        const stillOnToggle = await page.evaluate(() => {
          const active = document.activeElement;
          return active instanceof HTMLElement && active.hasAttribute('aria-expanded');
        });
        expect(stillOnToggle, 'focus should stay on the toggle itself, not move elsewhere').toBe(
          true,
        );
      }
    });
  }
});
