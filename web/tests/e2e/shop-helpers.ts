import type { Locator, Page } from '@playwright/test';

/** Below 768 px the filter panel is a sheet (UI-1). */
export function isMobile(page: Page): boolean {
  return (page.viewportSize()?.width ?? 1280) < 768;
}

/**
 * Applies filters in whichever form the viewport shows: inline checkboxes on desktop (applied on
 * change), or the **Filters (n)** sheet with **Show results** on a 360 px phone. `act` receives the
 * locator that scopes the filter groups.
 */
export async function withFilters(
  page: Page,
  act: (scope: Locator) => Promise<void>,
): Promise<void> {
  if (isMobile(page)) {
    await page.getByRole('button', { name: /^Filters \(\d+\)$/ }).click();
    const dialog = page.getByRole('dialog', { name: 'Filters' });
    await act(dialog);
    await dialog.getByRole('button', { name: 'Show results' }).click();
    await dialog.waitFor({ state: 'hidden' });
  } else {
    await act(page.getByRole('complementary', { name: 'Filters' }));
  }
}

/** Signs the storefront in/out of maintenance mode through the (mock) admin endpoint. */
export async function setMaintenance(page: Page, on: boolean, message = ''): Promise<void> {
  await page.evaluate(
    async ({ on, message }) => {
      const base = (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl ?? '';
      const response = await fetch(`${base}/api/admin/system/maintenance`, {
        method: 'PUT',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ maintenanceMode: on, message: message || null }),
      });
      if (!response.ok) throw new Error(`maintenance toggle failed: ${response.status}`);
    },
    { on, message },
  );
}
