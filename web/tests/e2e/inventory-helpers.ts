import { expect, type APIRequestContext, type Locator, type Page } from '@playwright/test';
import { accountsFixtureIds } from '../../mocks/handlers/areas/accounts';

/** `yyyy-MM-dd` for `offsetDays` from today (UTC date — good enough for a future-dated field). */
export function dateIn(offsetDays: number): string {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() + offsetDays);
  return d.toISOString().slice(0, 10);
}

/**
 * Keyboard-wedge input (ADR-FE-042, HW-2, E-A3): real key events back to back with no pause (a scanner is a very fast keyboard), then Enter.
 * A `delay` of 10 measures ~31 ms between keydowns on Windows, slower than a scanner and at the 30 ms line.
 */
export async function wedgeScan(page: Page, barcode: string): Promise<void> {
  const field = page.getByLabel(/scan a barcode/i);
  const live = page.locator('[id$="-scan-live"]');
  // A burst whose first keys land during a slow render is (correctly) not taken for a scan; a
  // scanner would simply be used again, so try again until the field has answered.
  for (let attempt = 0; attempt < 4; attempt += 1) {
    await field.fill('');
    await field.click();
    await page.keyboard.type(barcode);
    await page.keyboard.press('Enter');
    const answered = await live
      .filter({ hasText: /^(added|no sku has barcode)/i })
      .waitFor({ timeout: 2_000 })
      .then(() => true)
      .catch(() => false);
    if (answered) return;
  }
}

/** The dialog currently open, waited for. */
export async function dialog(page: Page): Promise<Locator> {
  const open = page.getByRole('dialog');
  await expect(open).toBeVisible();
  return open;
}

/** Picks an item from the SKU picker by typing part of its name and choosing the option. */
export async function pickSku(
  page: Page,
  pickerLabel: RegExp,
  term: string,
  optionName: RegExp,
): Promise<void> {
  await page.getByLabel(pickerLabel).fill(term);
  await page.getByRole('option', { name: optionName }).first().click();
}

/** Signs in as the seeded nurse — a role with `Stock.View` but not receiving or procurement. */
export async function signInAsNurse(page: Page): Promise<void> {
  if (page.url() === 'about:blank') await page.goto('/staff/sign-in');
  const result = await page.evaluate(
    async ({ identifier, password }) => {
      const apiBaseUrl = (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl ?? '';
      const response = await fetch(`${apiBaseUrl}/api/staff/auth/sign-in`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ identifier, password }),
      });
      return { ok: response.ok, status: response.status };
    },
    { identifier: accountsFixtureIds.nurseIdentifier, password: accountsFixtureIds.nursePassword },
  );
  if (!result.ok) throw new Error(`nurse sign-in fixture failed with ${result.status}`);
}

/**
 * A cold `next dev` compiles each route on its first request, which can take longer than an
 * assertion waits. Requesting every route once up front (no browser needed) moves that cost out of
 * the tests that follow.
 */
export async function warmRoutes(request: APIRequestContext, paths: string[]): Promise<void> {
  for (const path of paths) await request.get(path, { timeout: 120_000 }).catch(() => undefined);
}
