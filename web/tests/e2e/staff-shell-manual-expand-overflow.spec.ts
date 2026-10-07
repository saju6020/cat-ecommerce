import { test, expect, signInAsStaff } from './fixtures';
import { catProfilesFixtureIds } from '../../mocks/handlers/areas/cat-profiles';
import { openSheetAt360 } from './staff-shell-sheet-helpers';

// ARCH gate MEDIUM (first pass) — signs in as staff only (never both cookies at
// once, tests/e2e/fixtures.ts); every route this file visits is staff-audience.
test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

/**
 * HO-CAT-002 TEST gate, third pass, rewritten for the 7th pass's Radix-`Dialog` rebuild.
 *
 * Original finding: a manual "Expand menu" click below `sm` put the in-flow `<aside>` at its
 * `sm`-and-up width (240px) regardless of viewport, which — being in-flow — pushed
 * `document.documentElement`'s scroll width past 360px on every screen tried. Fixed at the time by
 * making a manual expand an overlay instead of in-flow (`7f758b3`), and superseded entirely by the
 * 7th pass: below `sm`, the in-flow `<aside>` can no longer widen at all under any state — it is
 * always `w-16`, a pure-CSS fact with no `override`/`collapsed` branch that ever changes it below
 * `sm` (`staff-shell.tsx`'s own top comment). There is no more "manual expand" of the in-flow aside
 * to reproduce this against; "Expand menu" now opens a Radix `Dialog` sheet, which is `fixed`
 * (removed from normal flow) regardless of its own width, so it cannot itself add to
 * `document.documentElement`'s scroll width either.
 *
 * The equivalent, still-live contract — E18's actual concern, "does interacting with the nav
 * control introduce horizontal overflow below the 360px floor" — is checked against the sheet
 * instead, on the same representative `/staff/**` screens this file always has.
 */
test.describe('StaffShell nav sheet — opening it at 360px overflows? (HO-CAT-002 TEST gate, 3rd pass, rebuilt 7th pass)', () => {
  const catId = catProfilesFixtureIds.catWithAllergiesExactDob;
  const SCREENS = [
    '/staff/cats',
    `/staff/cats/${catId}`,
    `/staff/cats/${catId}/medical`,
    `/staff/cats/${catId}/weights`,
  ];

  for (const path of SCREENS) {
    test(`opening the nav sheet introduces no horizontal scroll @ ${path}`, async ({ page }) => {
      await openSheetAt360(page, path);

      const { scrollWidth, clientWidth } = await page.evaluate(() => ({
        scrollWidth: document.documentElement.scrollWidth,
        clientWidth: document.documentElement.clientWidth,
      }));
      expect(
        scrollWidth,
        `${path} overflows horizontally after opening the nav sheet at 360px`,
      ).toBeLessThanOrEqual(clientWidth + 1);
    });
  }
});
