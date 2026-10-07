import { test, expect, signInAsStaff } from './fixtures';
import { openSheetAt360 } from './staff-shell-sheet-helpers';

// ARCH gate MEDIUM (first pass) — signs in as staff only (never both cookies at
// once, tests/e2e/fixtures.ts); every route this file visits is staff-audience.
test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

/**
 * HO-CAT-002 TEST gate, fifth pass, rewritten for the 7th pass's Radix-`Dialog` rebuild.
 *
 * Original finding: the hand-rolled scrim (`8379121`) blocked pointer clicks but nothing removed
 * the rest of the page from the *keyboard* tab order, so Tab could walk past the drawer's own nav
 * links straight into `<main>` and Enter could activate "Register a cat" while the drawer was still
 * open and the page still dimmed. Fixed at the time with `inert` on the header/`<main>` column
 * (`bb24673`), and superseded entirely by the 7th pass: the below-`sm` control now opens a real
 * Radix `Dialog`, whose `FocusScope` traps Tab/Shift+Tab at the document level — intercepting the
 * keydown itself rather than relying on `inert`/`tabindex` sweeps discovering every sibling that
 * needs marking (exactly the structural gap the 6th pass's toast finding named). This is the
 * "shadcn/ui's Radix primitives give this... hand-rolled dialogs do not" guarantee HO-CAT-002 §8.5
 * names directly.
 *
 * Same contract as before, checked against the sheet instead of the old hand-rolled drawer.
 */
test.describe('StaffShell nav sheet at 360px keeps focus out of covered content (HO-CAT-002 TEST gate, 5th pass, rebuilt 7th pass)', () => {
  test('Tab cannot reach, and Enter cannot activate, "Register a cat" while the sheet is open', async ({
    page,
  }) => {
    const { sheet } = await openSheetAt360(page);

    // Cats, Vaccine catalogue — the sheet's own two nav links on this fixture — then the sheet's
    // own close button. A trapped-focus dialog would never let a further Tab leave it while open.
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');

    const focusOutsideSheet = await page.evaluate(
      () => !document.activeElement?.closest('[role="dialog"]'),
    );
    expect(focusOutsideSheet, 'Tab escaped the sheet while it was open').toBe(false);
    await expect(sheet).toBeVisible();

    await page.keyboard.press('Enter');

    // With focus trapped inside the sheet, Enter can't reach "Register a cat" (whatever it landed
    // on, several Tabs in, is inside the sheet itself), so the page must not navigate.
    await page.waitForURL(/\/staff\/cats\/new$/, { timeout: 2000 }).catch(() => {});
    expect(page.url()).not.toContain('/staff/cats/new');
  });
});
