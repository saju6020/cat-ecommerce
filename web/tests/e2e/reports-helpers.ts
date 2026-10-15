import AxeBuilder from '@axe-core/playwright';
import { expect, type Page } from '@playwright/test';
import { accountsFixtureIds } from '../../mocks/handlers/areas/accounts';
import { holdApiUntilMswReady } from './portal-helpers';

/**
 * Shared helpers for the Reporting e2e specs (HO-RPT-002 §16). Plain functions, not a spec.
 * The mock knows two staff logins (administrator and nurse), so every other role is the NURSE
 * account whose roles the administrator changes first (`PUT /api/staff-accounts/{id}/roles`) —
 * the same approach as `emr-helpers.ts`. Real-API runs sign in with seeded accounts instead
 * (`REPORTS_SEED`, see `reports-real-api.spec.ts`).
 */
export type ReportsRole =
  'Administrator' | 'Nurse' | 'Reception' | 'Accountant' | 'ShopManager' | 'Inventory';

async function call(page: Page, method: string, path: string, body?: unknown) {
  return page.evaluate(
    async ({ method, path, body }) => {
      const base = (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl ?? '';
      const csrf = document.cookie
        .split('; ')
        .find((row) => row.startsWith('pc_csrf='))
        ?.slice(8);
      const response = await fetch(`${base}${path}`, {
        method,
        credentials: 'include',
        headers: {
          ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
          ...(csrf ? { 'X-CSRF-Token': decodeURIComponent(csrf) } : {}),
        },
        body: body === undefined ? undefined : JSON.stringify(body),
      });
      return response.status;
    },
    { method, path, body },
  );
}

async function signIn(page: Page, identifier: string, password: string) {
  const status = await call(page, 'POST', '/api/staff/auth/sign-in', { identifier, password });
  if (status >= 300) throw new Error(`staff sign-in as ${identifier} failed: ${status}`);
}

/**
 * Waits until MSW's service worker controls the page and has answered a request, so the first
 * real fetch cannot beat it (the documented start-up race). A no-op against the real API.
 */
export async function waitForMswReady(page: Page): Promise<void> {
  if (process.env.NEXT_PUBLIC_USE_MOCKS === 'false') return;
  await page.waitForFunction(() => Boolean(navigator.serviceWorker?.controller), undefined, {
    timeout: 30_000,
  });
  await expect
    .poll(
      () =>
        page.evaluate(async () => {
          const base = (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl ?? '';
          try {
            await fetch(`${base}/api/staff/session`, { credentials: 'include' });
            return true;
          } catch {
            return false;
          }
        }),
      { timeout: 30_000 },
    )
    .toBe(true);
}

/** Signs the page in as the given MOCK role, from the quiet sign-in page, before any staff route. */
const held = new WeakSet<Page>();

export async function signInAsReportsRole(page: Page, role: ReportsRole): Promise<void> {
  // Close the documented MSW start-up race at the test boundary (no retry): every /api/ fetch
  // waits for the worker to report ready (see portal-helpers.ts).
  if (process.env.NEXT_PUBLIC_USE_MOCKS !== 'false' && !held.has(page)) {
    held.add(page);
    await holdApiUntilMswReady(page);
  }
  await page.goto('/staff/sign-in');
  await page.waitForFunction(
    () => (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl !== undefined,
  );
  await waitForMswReady(page);
  if (role === 'Administrator') {
    await signIn(page, accountsFixtureIds.adminIdentifier, accountsFixtureIds.adminPassword);
    return;
  }
  {
    // The nurse login is reused for every other role, so reset its roles each time (the mock keeps them for the tab).
    await signIn(page, accountsFixtureIds.adminIdentifier, accountsFixtureIds.adminPassword);
    const status = await call(
      page,
      'PUT',
      `/api/staff-accounts/${accountsFixtureIds.nurseStaffId}/roles`,
      {
        roleIds: [role],
      },
    );
    if (status >= 300) throw new Error(`could not switch the nurse account to ${role}`);
  }
  await signIn(page, accountsFixtureIds.nurseIdentifier, accountsFixtureIds.nursePassword);
}

/** Signs in as `role`, opens `path` and waits for the staff shell. One attempt: a failure is real. */
export async function openReportsAs(page: Page, role: ReportsRole, path: string): Promise<void> {
  await signInAsReportsRole(page, role);
  await page.goto(path);
  await expect(page.locator('#main-content')).toBeVisible({ timeout: 60_000 });
}

/** UI-1: no page-level horizontal scroll and no clipped control. */
export async function expectNoHorizontalScroll(page: Page): Promise<void> {
  // The staff rail animates its width after a resize (transition-[width]), so poll until it settles.
  await expect
    .poll(
      () =>
        page.evaluate(() => ({
          doc: document.documentElement.scrollWidth - document.documentElement.clientWidth,
          body: document.body.scrollWidth - document.body.clientWidth,
        })),
      { message: 'the page scrolls sideways', timeout: 5_000 },
    )
    .toEqual({ doc: 0, body: 0 });
}

/** Zero serious or critical axe violations (UI-6). */
export async function expectAxeClean(page: Page): Promise<void> {
  const results = await new AxeBuilder({ page }).analyze();
  const bad = results.violations.filter((v) => v.impact === 'serious' || v.impact === 'critical');
  expect(bad, JSON.stringify(bad.map((b) => [b.id, b.nodes.map((n) => n.target)]))).toEqual([]);
}
