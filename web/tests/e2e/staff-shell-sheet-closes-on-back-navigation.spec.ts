import { test, expect, signInAsStaff } from './fixtures';

// ARCH gate MEDIUM (first pass) — signs in as staff only (never both cookies at
// once, tests/e2e/fixtures.ts); every route this file visits is staff-audience.
test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

/**
 * CODE_REVIEW gate MEDIUM finding (HO-CAT-002, 2nd pass): `StaffShell` is a persistent App Router
 * layout component, so its below-`sm` nav sheet's `sheetOpen` state used to survive any route
 * change that didn't come from one of the sheet's own `SheetNav` links — most notably the
 * browser's Back/Forward buttons, which are chrome, not page DOM, so neither the dialog's overlay
 * nor its focus trap could intercept them. A user could open the sheet, press Back, and land on
 * the previous page with the modal nav sheet still open on top of it, still trapping
 * Tab/Shift+Tab. Fixed by closing the sheet on every `usePathname()` change.
 *
 * **Rewritten (TEST gate, 8th pass — MEDIUM, test-suite quality):** the original version built its
 * "before" navigation history with two `page.goto()` calls. Request-tracing during `goBack()`
 * showed that produces a genuine hard document reload in Playwright, not the soft, client-side
 * transition the underlying bug is actually about — a hard reload remounts the whole app and
 * trivially resets `sheetOpen` to its initial `false` regardless of whether `f392132`'s
 * `usePathname()` effect exists at all, so the spec passed identically whether the fix was present
 * or reverted. Fixed here by reaching the second page via a real in-sheet `<Link>` click (exactly
 * how a user closes the sheet via `onNavigate`), so the resulting history entry is a genuine
 * Next.js client-side transition and `goBack()` exercises the router's own popstate handling
 * instead of the browser's native reload. Proof this now distinguishes fixed from unfixed code is
 * recorded in the commit message that introduces this rewrite, not in this file.
 */
test('the nav sheet closes on browser Back and does not trap focus afterwards', async ({
  page,
}) => {
  await page.setViewportSize({ width: 360, height: 900 });
  await page.goto('/staff/cats');
  await page.waitForLoadState('networkidle');

  const trigger = page.locator('[aria-label="Expand menu"]');

  // Reach /staff/vaccines via a real in-sheet link click — a soft, client-side transition, not a
  // second page.goto(). Clicking a SheetNav link also closes the sheet itself (onNavigate), which
  // is the correct, expected behaviour and not what this test is about.
  await trigger.focus();
  await trigger.press('Enter');
  await expect(page.getByRole('dialog')).toBeVisible();
  await page.getByRole('link', { name: 'Vaccine catalogue' }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await page.waitForURL('**/staff/vaccines');

  // Re-open the sheet on this second page, then go back with it open.
  await trigger.focus();
  await trigger.press('Enter');
  await expect(page.getByRole('dialog')).toBeVisible();

  await page.goBack();
  await page.waitForLoadState('networkidle');
  await expect(page).toHaveURL(/\/staff\/cats$/);

  await expect(page.getByRole('dialog')).toHaveCount(0);
  // Radix's `hideOthers` clears the rest of the document's `aria-hidden` the moment the dialog
  // unmounts — the rail's own trigger, and `<main>`, must both be reachable again.
  await expect(page.locator('#main-content')).not.toHaveAttribute('aria-hidden', 'true');
  await expect(trigger).not.toHaveAttribute('aria-hidden', 'true');

  // Focus is free to land somewhere in the page itself, not stuck on a node that no longer exists.
  await page.keyboard.press('Tab');
  const focusIsLive = await page.evaluate(
    () => document.activeElement !== null && document.activeElement !== document.body,
  );
  expect(focusIsLive).toBe(true);
});
