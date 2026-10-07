import type { Page } from '@playwright/test';
import { expect } from './fixtures';

/**
 * Shared by every `staff-shell-drawer-*.spec.ts` / `staff-shell-manual-expand-*.spec.ts` file
 * (HO-CAT-002 TEST gate, 7th pass) — opens the below-`sm` nav sheet the same way a keyboard user
 * would (focus the rail's trigger, then activate it), and waits for Radix to actually finish
 * opening it before returning, so callers never race the mount.
 *
 * `trigger` is resolved by its `aria-label` attribute, not `getByRole` — deliberately. Once the
 * sheet is open, Radix's own `modal` behaviour (HO-CAT-002 §8.5 — "hiding siblings from assistive
 * tech") marks everything outside its portal `aria-hidden="true"`, including the trigger itself:
 * exactly the intended fix for the 6th pass's toast finding, applied to the whole document rather
 * than one hand-picked subtree. `getByRole` respects that (as a screen reader would) and simply
 * won't find an `aria-hidden` element, so a role query for the trigger would time out for as long
 * as the sheet stays open — this attribute selector matches the same DOM node regardless.
 */
export async function openSheetAt360(page: Page, path = '/staff/cats') {
  await page.setViewportSize({ width: 360, height: 900 });
  await page.goto(path);
  await page.waitForLoadState('networkidle');

  const trigger = page.locator('[aria-label="Expand menu"]');
  await trigger.focus();
  await trigger.press('Enter');

  const sheet = page.getByRole('dialog');
  await expect(sheet).toBeVisible();
  return { trigger, sheet };
}
