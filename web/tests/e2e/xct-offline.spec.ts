import type { Page } from '@playwright/test';
import { accountsFixtureIds } from '../../mocks/handlers/areas/accounts';
import { test, expect, signInAsStaff } from './fixtures';

/**
 * HO-XCT-002 section 16 E-A cases, run against MSW. The offline cases that need a RELOAD while the
 * network is down (E-A4, E-A5, "lock, reload, still locked") need the real service worker, which is
 * never registered in mock mode (ADR-FE-140, section 5.2), so they run in `xct-real-api.spec.ts`
 * against a production build. Here the server is made unreachable through the mock's ping switch,
 * and the offline board is reached by following the banner's link (a client-side navigation): a
 * hard load of `/staff/offline` while the server answers goes to the front desk by design.
 */
test.describe.configure({ timeout: 90_000 });

interface XctMockWindow {
  __xctMock: {
    setPing: (mode: 'ok' | 'fail' | 'serverError') => void;
    setSnapshotStaff: (id: string | null) => void;
  };
}

async function optIn(page: Page) {
  await page.goto('/staff/offline/settings');
  const toggle = page.getByLabel(/Keep an offline copy of today/);
  await expect(toggle).toBeEnabled({ timeout: 20_000 });
  await toggle.click();
  await expect(toggle).toBeChecked();
  await expect(page.getByText(/Offline copy turned on/)).toBeVisible();
}

/** The staff shell is up (the session guard has resolved). */
async function shellReady(page: Page) {
  await expect(page.getByRole('button', { name: 'Sign out' })).toBeVisible({ timeout: 45_000 });
}

/** The server stops answering: two failed checks in a row, a few seconds apart, flip the banner. */
async function serverDown(page: Page) {
  await page.evaluate(() => (window as unknown as XctMockWindow).__xctMock.setPing('fail'));
}

async function serverBack(page: Page) {
  await page.evaluate(() => (window as unknown as XctMockWindow).__xctMock.setPing('ok'));
}

async function readStore(page: Page) {
  return page.evaluate(
    () =>
      new Promise<{ snapshots: Array<{ staffAccountId: string }>; device: Record<string, unknown> | null }>(
        (resolve) => {
          const open = indexedDB.open('purrcare-offline');
          open.onerror = () => resolve({ snapshots: [], device: null });
          open.onsuccess = () => {
            const db = open.result;
            if (!db.objectStoreNames.contains('snapshot')) return resolve({ snapshots: [], device: null });
            const tx = db.transaction(['snapshot', 'device']);
            const snapshots = tx.objectStore('snapshot').getAll();
            const device = tx.objectStore('device').get('settings');
            tx.oncomplete = () =>
              resolve({ snapshots: snapshots.result, device: (device.result as Record<string, unknown>) ?? null });
          };
        },
      ),
  );
}

async function waitForStoredCopy(page: Page) {
  await expect.poll(async () => (await readStore(page)).snapshots.length, { timeout: 30_000 }).toBeGreaterThan(0);
}

/** The dev server compiles a route on first use and may reload the page when it does, which would reset
 *  the mock's "server down" switch. Visit the board once (it leaves for the front desk at once while the
 *  server answers) so that compile happens before any test relies on the switch. */
let boardCompiled = false;
async function compileBoardOnce(page: Page) {
  if (boardCompiled) return;
  await page.goto('/staff/offline');
  await expect(page).toHaveURL(/\/staff\/(front-desk|sign-in)/, { timeout: 60_000 });
  boardCompiled = true;
}

/** Front desk -> server down -> banner -> the board, by the banner's own link. */
async function openBoardFromBanner(page: Page) {
  await compileBoardOnce(page);
  await page.goto('/staff/front-desk');
  await shellReady(page);
  await waitForStoredCopy(page);
  await serverDown(page);
  const banner = page.getByTestId('connectivity-banner');
  await expect(banner).toContainText(/You.re offline/, { timeout: 30_000 });
  await banner.getByRole('link', { name: /Open today.s offline list/ }).click();
  await expect(page).toHaveURL(/\/staff\/offline\?from=/);
}

test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

test('E-A1/E-A2/E-A6 the banner appears, the board shows both lists read-only, and recovery is automatic', async ({
  page,
}) => {
  await optIn(page);
  await openBoardFromBanner(page);
  await expect(page.getByText('Offline · read-only')).toBeVisible({ timeout: 30_000 });
  await expect(page.getByRole('heading', { name: /Today.s appointments/ })).toBeVisible();
  await expect(page.getByRole('heading', { name: /boarding tasks/ })).toBeVisible();
  await expect(page.getByText('Luna')).toBeVisible();
  await expect(page.locator('main').getByRole('button')).toHaveCount(0); // read-only: no action button at all
  await expect(page.getByText('High risk')).toBeVisible();
  // Owner decision 2026-10-14: no phone number on the board, though the mock API still sends one.
  expect(await page.locator('main').innerText()).not.toMatch(/8801711|tel:/);
  await expect(page.locator('main a[href^="tel:"]')).toHaveCount(0);

  // E-A6: the connection returns and the board takes the person back by itself.
  await serverBack(page);
  await expect(page).toHaveURL(/\/staff\/front-desk/, { timeout: 30_000 });
});

test('E-A3 a write is refused before any request while offline, and nothing is queued', async ({ page }) => {
  await page.goto('/staff/front-desk');
  await shellReady(page);
  await serverDown(page);
  await expect(page.getByTestId('connectivity-banner')).toContainText(/You.re offline/, { timeout: 30_000 });
  const writes: string[] = [];
  page.on('request', (r) => {
    if (r.method() === 'POST') writes.push(r.url());
  });
  const result = await page.evaluate(async () => {
    const w = window as unknown as { __apiFetch: (p: string, i?: object) => Promise<unknown> };
    try {
      await w.__apiFetch('/api/staff/appointments/x/check-in', { method: 'POST', body: {} });
      return 'sent';
    } catch (e) {
      return (e as Error).name;
    }
  });
  expect(result).toBe('OfflineError');
  expect(writes).toEqual([]);
  await serverBack(page);
  await page.waitForTimeout(500);
  expect(writes).toEqual([]); // nothing was queued
});

test('E-A7 the board locks after the idle timeout, the lock is saved on the device, and input does not unlock it', async ({
  page,
}) => {
  await optIn(page);
  await page.clock.install();
  await openBoardFromBanner(page);
  await expect(page.getByText('Luna')).toBeVisible({ timeout: 30_000 });
  await page.clock.fastForward('31:00');
  await expect(page.getByText('Locked. Sign in again when the connection returns.')).toBeVisible();
  await page.keyboard.press('Space');
  await page.mouse.move(10, 10);
  await expect(page.getByText('Luna')).toHaveCount(0);
  // Persisted: a reload cannot lift it (the offline reload itself is in xct-real-api.spec.ts).
  await expect
    .poll(async () => (await readStore(page)).device?.lockedForStaffAccountId, { timeout: 15_000 })
    .toBe(accountsFixtureIds.adminStaffId);
});

test('SECURITY: a fresh load of the board while the server answers goes to the front desk and shows no data', async ({
  page,
}) => {
  await optIn(page);
  await page.goto('/staff/front-desk');
  await shellReady(page);
  await waitForStoredCopy(page);
  await page.goto('/staff/offline');
  await expect(page).toHaveURL(/\/staff\/front-desk/, { timeout: 30_000 });
  await expect(page.getByText('Luna')).toHaveCount(0);
});

test('E-A8 after sign-out the saved copy is gone, and the board does not show it', async ({ page }) => {
  await optIn(page);
  await page.goto('/staff/front-desk');
  await shellReady(page);
  await waitForStoredCopy(page);
  await page.getByRole('button', { name: 'Sign out' }).click();
  await expect(page).toHaveURL(/\/staff\/sign-in/);
  await expect.poll(async () => (await readStore(page)).snapshots.length, { timeout: 15_000 }).toBe(0);
  await page.goto('/staff/offline');
  await expect(page.getByText('Luna')).toHaveCount(0);
});

test('E-A9 a different person signing in on the same device removes the first person\'s copy', async ({ page }) => {
  await optIn(page);
  await page.goto('/staff/front-desk');
  await shellReady(page);
  await waitForStoredCopy(page);
  expect((await readStore(page)).snapshots.map((s) => s.staffAccountId)).toEqual([accountsFixtureIds.adminStaffId]);

  // The first person leaves without signing out; the second signs in on the same browser profile.
  await page.evaluate((id) => (window as unknown as XctMockWindow).__xctMock.setSnapshotStaff(id), accountsFixtureIds.nurseStaffId);
  const status = await page.evaluate(
    async ({ identifier, password }) => {
      const base = (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl ?? '';
      const response = await fetch(`${base}/api/staff/auth/sign-in`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ identifier, password }),
      });
      return response.status;
    },
    { identifier: accountsFixtureIds.nurseIdentifier, password: accountsFixtureIds.nursePassword },
  );
  expect(status).toBe(200);
  await page.goto('/staff/front-desk');
  await shellReady(page);
  await expect
    .poll(async () => (await readStore(page)).snapshots.map((s) => s.staffAccountId), { timeout: 30_000 })
    .toEqual([accountsFixtureIds.nurseStaffId]);
});

test('E-A10 with the device opt-in off there is no snapshot request and the banner offers no board', async ({
  page,
}) => {
  const snapshotCalls: string[] = [];
  page.on('request', (r) => {
    if (r.url().includes('/api/staff/offline-snapshot')) snapshotCalls.push(r.url());
  });
  await page.goto('/staff/front-desk');
  await shellReady(page);
  await page.waitForTimeout(2500);
  expect(snapshotCalls).toEqual([]);
  await serverDown(page);
  const banner = page.getByTestId('connectivity-banner');
  await expect(banner).toContainText(/no saved copy/, { timeout: 30_000 });
  await expect(banner.getByRole('link')).toHaveCount(0);
});

test('E-X3 storage audit: nothing of this feature in localStorage, and only the sanctioned IndexedDB database', async ({
  page,
}) => {
  await optIn(page);
  await page.goto('/staff/front-desk');
  await shellReady(page);
  await waitForStoredCopy(page);
  const audit = await page.evaluate(async () => {
    const dbs = await indexedDB.databases();
    return {
      ls: Object.keys(localStorage).filter((k) => /offline|snapshot|token|session/i.test(k)),
      databases: dbs.map((d) => d.name).filter((n) => n && !/msw/i.test(n)),
    };
  });
  expect(audit.ls).toEqual([]);
  expect(audit.databases).toEqual(['purrcare-offline']);
  // pc_session is HttpOnly only on the real API; the MSW mock sets its cookie from the page, so that
  // half of E-X3 is asserted in xct-real-api.spec.ts.
});
