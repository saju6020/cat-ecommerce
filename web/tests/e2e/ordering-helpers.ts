import type { Page } from '@playwright/test';
import { shopFixtureIds } from '../../mocks/handlers/shop';
import type { OrderingMockControls } from '../../mocks/handlers/ordering/control';

/**
 * Helpers for the HO-ORD-002 specs (MSW mode). The Ordering mock server runs in the page; a few
 * things a test cannot cause through the UI ("the price changed", "an authorisation lapsed", "the
 * transfer window is 30 seconds", "someone else is the staff member") go through
 * `window.__orderingMock` (`mocks/handlers/ordering/control.ts`) — test controls of the mock server,
 * never app code.
 */
export type ControlName = keyof OrderingMockControls;

export async function control<K extends ControlName>(
  page: Page,
  name: K,
  ...args: Parameters<OrderingMockControls[K]>
): Promise<void> {
  await page.evaluate(
    ({ name, args }) => {
      const controls = (window as unknown as { __orderingMock?: Record<string, (...a: unknown[]) => void> })
        .__orderingMock;
      if (!controls) throw new Error('window.__orderingMock is not installed (are mocks on?)');
      controls[name]!(...args);
    },
    { name, args: args as unknown[] },
  );
}

/** Saves an address for the signed-in customer through the (mock) API. Returns its id. */
export async function addAddress(
  page: Page,
  address: { label?: string; district?: string; postalCode?: string } = {},
): Promise<string> {
  return page.evaluate(async (input) => {
    const base = (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl ?? '';
    const response = await fetch(`${base}/api/customers/me/addresses`, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        label: input.label ?? 'Home',
        line1: 'House 7, Road 3, Dhanmondi',
        city: input.district ?? 'Dhaka',
        district: input.district ?? 'Dhaka',
        postalCode: input.postalCode ?? '1209',
      }),
    });
    if (!response.ok) throw new Error(`address fixture failed: ${response.status}`);
    return ((await response.json()) as { addressId: string }).addressId;
  }, address);
}

export const ids = shopFixtureIds;

/** Below 768 px the staff queue is cards. */
export function isMobile(page: Page): boolean {
  return (page.viewportSize()?.width ?? 1280) < 768;
}
