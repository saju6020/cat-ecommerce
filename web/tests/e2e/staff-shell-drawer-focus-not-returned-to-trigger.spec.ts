import { test, expect, signInAsStaff } from './fixtures';
import { openSheetAt360 } from './staff-shell-sheet-helpers';

// ARCH gate MEDIUM (first pass) — signs in as staff only (never both cookies at
// once, tests/e2e/fixtures.ts); every route this file visits is staff-audience.
test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

/**
 * HO-CAT-002 TEST gate, sixth pass — MEDIUM, now fixed by the 7th pass's Radix-`Dialog` rebuild.
 *
 * Original finding: neither the hand-rolled drawer's `Escape` `keydown` handler nor its scrim's
 * `onClick` ever called `.focus()` on the toggle — both closed the drawer but left focus wherever
 * it already was (a nav link after Tab had moved it there, or `document.body` entirely after a
 * scrim click), never actually "returning" it as HO-CAT-002 §8.5 requires. Every existing
 * `Escape`/scrim-click case only ever closed the drawer *immediately* after opening it, before Tab
 * had moved focus anywhere, so none of them exercised a genuine return — this file's own two cases
 * were written specifically to move focus first, which is what made the gap reproducible.
 *
 * The 7th pass didn't patch the old handlers — it replaced the whole hand-rolled drawer with a real
 * Radix `Dialog`, whose `<DialogTrigger>` already restores focus to itself on close, for both
 * `Escape` and an outside (overlay) click, with no extra code in `StaffShell` at all. This file is
 * kept, updated to the new selectors, as the permanent regression test for exactly the behaviour
 * the original finding described — it now watches the fix hold, rather than watching the bug fail.
 */
test.describe('StaffShell nav sheet at 360px returns focus to its trigger on close (HO-CAT-002 TEST gate, 6th pass, fixed 7th pass)', () => {
  test('Escape returns focus to the trigger even after Tab moved focus onto a sheet nav link', async ({
    page,
  }) => {
    const { trigger } = await openSheetAt360(page);

    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');
    // Sanity: focus really did move off the trigger before Escape, so this isn't the trivial
    // "never left" case a shallower test would coincidentally pass on.
    const movedAway = await trigger.evaluate((el) => el !== document.activeElement);
    expect(movedAway, 'setup: focus should have moved off the trigger before Escape').toBe(true);

    await page.keyboard.press('Escape');
    await expect(page.getByRole('dialog')).toHaveCount(0);

    await expect(
      trigger,
      '§8.5: closing must return focus to the trigger, not leave it on a link',
    ).toBeFocused();
  });

  test('an outside (overlay) click returns focus to the trigger, not document.body', async ({
    page,
  }) => {
    const { trigger } = await openSheetAt360(page);
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');

    await page.mouse.click(340, 450); // clear of the sheet's own left-docked content
    await expect(page.getByRole('dialog')).toHaveCount(0);

    await expect(
      trigger,
      '§8.5: closing must return focus to the trigger, not drop it to <body>',
    ).toBeFocused();
  });
});
