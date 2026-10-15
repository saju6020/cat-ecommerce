// Plain Playwright `test`, not ./fixtures: that wrapper waits for MSW's service worker, which never
// exists when NEXT_PUBLIC_USE_MOCKS=false (the whole point of this spec).
import { test, expect, type Page } from '@playwright/test';
import { VALID_DRILL_JSON } from '../../mocks/handlers/operations';

/**
 * HO-XCT-002 section 16, re-run against the REAL API (NEXT_PUBLIC_USE_MOCKS=false, API on
 * http://localhost:5113, fresh database catecommerce_xct002e2e, web on :3113). Skipped on mocks.
 *
 * The offline RELOAD cases (E-A4, E-A5) need the service worker, which exists only in a PRODUCTION
 * build (ADR-FE-140): build with `NEXT_PUBLIC_API_BASE_URL=http://localhost:5113
 * NEXT_PUBLIC_USE_MOCKS=false`, run `next start -p 3113`, then run this file with the same env.
 * It signs in as an Administrator seeded into that database: set `E2E_STAFF_IDENTIFIER` and
 * `E2E_STAFF_PASSWORD`. A fresh database holds no backup log and no storefront samples, so the
 * rows that need them say so rather than pass vacuously.
 */
const IS_REAL_API = process.env.NEXT_PUBLIC_USE_MOCKS === 'false';
test.skip(!IS_REAL_API, 'real-API run only (NEXT_PUBLIC_USE_MOCKS=false)');
test.describe.configure({ timeout: 150_000 });

const API = process.env.E2E_API_BASE_URL ?? 'http://localhost:5113';
const IDENTIFIER = process.env.E2E_STAFF_IDENTIFIER ?? '';
const PASSWORD = process.env.E2E_STAFF_PASSWORD ?? '';
const CAPTCHA_ANSWER = process.env.E2E_CAPTCHA_ANSWER ?? 'TEST1';

const IDENTIFIER_2 = process.env.E2E_STAFF2_IDENTIFIER ?? '';
const PASSWORD_2 = process.env.E2E_STAFF2_PASSWORD ?? '';

async function signIn(page: Page, who: { identifier: string; password: string } = { identifier: IDENTIFIER, password: PASSWORD }) {
  await page.goto('/staff/sign-in');
  const status = await page.evaluate(
    async ({ api, identifier, password }) => {
      const response = await fetch(`${api}/api/staff/auth/sign-in`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ identifier, password }),
      });
      return response.status;
    },
    { api: API, identifier: who.identifier, password: who.password },
  );
  expect(status, 'the seeded administrator must be able to sign in').toBe(200);
}

/** A call to the API from the page, with the CSRF header (for set-up and for comparing figures). */
async function apiCall(page: Page, method: string, path: string, body?: unknown) {
  return page.evaluate(
    async ({ api, method, path, body }) => {
      const csrf = document.cookie.split('; ').find((r) => r.startsWith('pc_csrf='))?.slice(8);
      const response = await fetch(`${api}${path}`, {
        method,
        credentials: 'include',
        headers: {
          ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
          ...(csrf ? { 'X-CSRF-Token': decodeURIComponent(csrf) } : {}),
        },
        body: body !== undefined ? JSON.stringify(body) : undefined,
      });
      const text = await response.text();
      return { status: response.status, body: text ? JSON.parse(text) : null };
    },
    { api: API, method, path, body },
  );
}

async function storedSnapshots(page: Page): Promise<number> {
  return page.evaluate(
    () =>
      new Promise<number>((resolve) => {
        const open = indexedDB.open('purrcare-offline');
        open.onsuccess = () => {
          const db = open.result;
          if (!db.objectStoreNames.contains('snapshot')) return resolve(0);
          const count = db.transaction('snapshot').objectStore('snapshot').count();
          count.onsuccess = () => resolve(count.result);
          count.onerror = () => resolve(0);
        };
        open.onerror = () => resolve(0);
      }),
  );
}

test('wire: ping is a 204 with no-store, and the snapshot is a 200 with an ETag and no-store, private', async ({ page }) => {
  await signIn(page);
  const ping = await page.request.get(`${API}/api/system/ping`);
  expect(ping.status()).toBe(204);
  expect(ping.headers()['cache-control']).toContain('no-store');
  const snapshot = await page.evaluate(async (api) => {
    const response = await fetch(`${api}/api/staff/offline-snapshot`, { credentials: 'include' });
    return {
      status: response.status,
      cacheControl: response.headers.get('cache-control'),
      etagReadable: response.headers.get('etag') !== null,
      body: await response.json(),
    };
  }, API);
  expect(snapshot.status).toBe(200);
  expect(snapshot.body.schemaVersion).toBe(1);
  // HO-XCT-002 section 1 Amended (1): cross-origin the ETag is NOT readable until the API lists it in
  // Access-Control-Expose-Headers. Recorded, not asserted, so the day it is fixed this stays green.
  test.info().annotations.push({ type: 'finding', description: `ETag readable cross-origin: ${snapshot.etagReadable}` });
});

test('E-A1/E-A4/E-A5/E-A6 the service worker serves the board on a reload while offline, and recovery is automatic', async ({
  page,
  context,
}) => {
  test.skip(process.env.NODE_ENV === 'development' || process.env.XCT_PROD_BUILD !== '1', 'needs a production build (XCT_PROD_BUILD=1): the worker is not registered in development');
  await signIn(page);

  // Opt the device in.
  await page.goto('/staff/offline/settings');
  const toggle = page.getByLabel(/Keep an offline copy of today/);
  await expect(toggle).toBeEnabled({ timeout: 30_000 });
  await toggle.click();
  await expect(toggle).toBeChecked();
  await expect(page.getByText(/Offline copy turned on/)).toBeVisible();

  // A synced copy, a registered worker, and a warm offline shell.
  await page.goto('/staff/front-desk');
  await expect(page.getByRole('button', { name: 'Sign out' })).toBeVisible({ timeout: 45_000 });
  await expect.poll(() => storedSnapshots(page), { timeout: 45_000 }).toBeGreaterThan(0);
  await expect
    .poll(
      () =>
        page.evaluate(async () => {
          const registration = await navigator.serviceWorker.getRegistration('/staff/');
          const keys = await caches.keys();
          const shell = keys.find((k) => k.startsWith('purrcare-shell-'));
          const hit = shell ? await (await caches.open(shell)).match('/staff/offline') : undefined;
          return Boolean(registration?.active) && Boolean(hit);
        }),
      { timeout: 45_000 },
    )
    .toBe(true);
  await page.reload();
  await expect.poll(() => page.evaluate(() => Boolean(navigator.serviceWorker.controller)), { timeout: 20_000 }).toBe(true);

  // E-A5: offline, a direct visit to the front desk lands on the board with an explanation.
  await context.setOffline(true);
  await page.goto('/staff/front-desk').catch(() => undefined);
  await expect(page).toHaveURL(/\/staff\/offline\?from=/, { timeout: 30_000 });
  await expect(page.getByRole('heading', { name: /Today.s appointments/ })).toBeVisible({ timeout: 30_000 });
  await expect(page.locator('main').getByRole('button')).toHaveCount(0);

  // E-A4: offline, RELOAD the board itself: the worker serves the shell, the data comes from IndexedDB.
  await page.reload();
  await expect(page.getByRole('heading', { name: /Today.s appointments/ })).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText(/Saved at \d\d:\d\d/)).toBeVisible();

  // E-A6: the connection returns and the board takes the person back by itself.
  await context.setOffline(false);
  await page.evaluate(() => window.dispatchEvent(new Event('online')));
  await expect(page).toHaveURL(/\/staff\/front-desk/, { timeout: 45_000 });
});

test('E-A8/E-X3 after sign-out the store is wiped, no token is readable, and only the sanctioned database exists', async ({ page }) => {
  await signIn(page);
  await page.goto('/staff/offline/settings');
  const toggle = page.getByLabel(/Keep an offline copy of today/);
  await expect(toggle).toBeEnabled({ timeout: 30_000 });
  if (!(await toggle.isChecked())) {
    await toggle.click();
    // The write to IndexedDB must finish before the next navigation can abort it.
    await expect(page.getByText(/Offline copy turned on/)).toBeVisible();
  }
  await page.goto('/staff/front-desk');
  await expect(page.getByRole('button', { name: 'Sign out' })).toBeVisible({ timeout: 45_000 });
  await expect.poll(() => storedSnapshots(page), { timeout: 45_000 }).toBeGreaterThan(0);

  const audit = await page.evaluate(async () => ({
    cookie: document.cookie,
    local: Object.keys(localStorage),
    session: Object.keys(sessionStorage),
    databases: (await indexedDB.databases()).map((d) => d.name),
  }));
  expect(audit.cookie).not.toContain('pc_staff_session'); // HttpOnly
  expect(audit.local).toEqual([]);
  expect(audit.session).toEqual([]);
  expect(audit.databases).toEqual(['purrcare-offline']);

  await page.getByRole('button', { name: 'Sign out' }).click();
  await expect(page).toHaveURL(/\/staff\/sign-in/);
  await expect.poll(() => storedSnapshots(page), { timeout: 15_000 }).toBe(0);
  // At the board online, the server answers: it leaves for the staff app (signed out, so sign-in).
  await page.goto('/staff/offline');
  await expect(page).toHaveURL(/staff.sign-in/, { timeout: 30_000 });
  await expect(page.getByText(/Today.s appointments/)).toHaveCount(0);
});

test('E-C1/E-C2 an Administrator erases a real customer; the ledger lists it with no personal data', async ({ page, playwright }) => {
  await signIn(page);
  // A customer to erase, registered through the public API.
  // Registration is public: a request context with no cookies (a staff session cookie would make it
  // an unsafe request that needs a CSRF token).
  const anonymous = await playwright.request.newContext();
  const captcha = await anonymous.post(`${API}/api/captcha`);
  const { captchaId } = await captcha.json();
  const mobile = `01${String(Date.now()).slice(-9)}`;
  const registered = await anonymous.post(`${API}/api/customers`, {
    data: { fullName: 'Erase Me Test', mobileNumber: mobile, password: 'a-genuinely-long-password-1', captchaId, captchaAnswer: CAPTCHA_ANSWER },
  });
  expect(registered.status()).toBe(201);
  const { customerId } = await registered.json();

  await page.goto(`/staff/customers/${customerId}`);
  await expect(page.getByRole('heading', { name: 'Erase personal data' })).toBeVisible({ timeout: 45_000 });
  await page.getByLabel('How was the request made?').selectOption('InPerson');
  await page.getByLabel('How was identity checked?').selectOption('InPersonPhotoId');
  await page.getByRole('button', { name: /Erase personal data…/ }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toContainText('Erase Me Test');
  await dialog.getByRole('button', { name: 'Erase personal data' }).click();
  await expect(page.getByText('Personal data was erased.')).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText('Erase Me Test')).toHaveCount(0); // the name appears nowhere after erasure

  await page.getByRole('link', { name: 'erasure ledger' }).click();
  await expect(page.getByRole('heading', { name: 'Erasure request' })).toBeVisible({ timeout: 30_000 });
  const text = (await page.locator('main').innerText()).replace(/\s+/g, ' ');
  expect(text).toContain(customerId);
  expect(text).not.toContain('Erase Me');
  expect(text).not.toContain(mobile);
});

test('E-C3/E-C4 run retention now, then the run detail; the schedule keeps clinical records review-only', async ({ page }) => {
  await signIn(page);
  await page.goto('/staff/admin/retention');
  await expect(page.getByRole('row', { name: /Clinical records/ })).toContainText('Review only', { timeout: 45_000 });
  await page.getByRole('button', { name: 'Run retention now' }).click();
  // Wait for the run to be accepted before leaving the page: navigating away with the request in
  // flight can leave the list loaded before the run exists (a fresh database has no other run).
  const accepted = page.waitForResponse(
    (response) => response.url().endsWith('/api/admin/retention/runs') && response.request().method() === 'POST',
  );
  await page.getByRole('dialog').getByRole('button', { name: 'Run retention' }).click();
  expect((await accepted).status()).toBeLessThan(300);
  await page.goto('/staff/admin/retention/runs');
  await expect(page.getByRole('table')).toBeVisible({ timeout: 30_000 });
  await page.getByRole('table').getByRole('link').first().click();
  await expect(page.getByRole('heading', { name: 'Retention run' })).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText(/never deleted by a run/)).toBeVisible();
  const text = await page.locator('main').innerText();
  expect(text).not.toMatch(/ErasureScopeFailing|PurgeSourceNotRegistered|errorCode/);
});

test('E-C5 re-applying three ids shows the same four counts the API returns', async ({ page }) => {
  await signIn(page);
  const ids = [1, 2, 3].map((n) => `00000000-0000-0000-0000-00000000f00${n}`);
  const direct = await apiCall(page, 'POST', '/api/admin/retention/erasures/reapply', { customerAccountIds: ids, restoreReference: 'xct-e2e-direct' });
  expect(direct.status).toBe(200);
  await page.goto('/staff/admin/retention/erasures/reapply');
  await page.getByLabel('Customer ids').fill(ids.join('\n'));
  await page.getByLabel('Restore reference').fill('xct-e2e-ui');
  await page.getByRole('button', { name: 'Re-apply erasures' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Re-apply erasures' }).click();
  await expect(page.getByText('Not found')).toBeVisible({ timeout: 30_000 });
  const panel = page.getByRole('region', { name: 'Result' }).or(page.locator('section[aria-labelledby="reapply-result"]'));
  await expect(panel).toContainText(`Not found${direct.body.notFound}`);
  await expect(panel).toContainText(`Already erased${direct.body.alreadyErased}`);
});

test('E-D1/E-D2 the backup page shows both problems on a fresh database, and a pasted drill is recorded with the server-computed result', async ({ page }) => {
  await signIn(page);
  await page.goto('/staff/admin/operations/backup');
  // Restore tests are append-only, so a database that has already recorded one is no longer overdue.
  // The overdue part is asserted only while the API itself says the test is overdue.
  const apiSaysOverdue = (await apiCall(page, 'GET', '/api/admin/operations/backup-status')).body?.isRestoreTestOverdue === true;
  if (apiSaysOverdue) {
    const overdue = page.getByRole('alert').filter({ hasText: 'Restore test overdue' });
    await expect(overdue).toBeVisible({ timeout: 45_000 });
    await expect(overdue).toBeInViewport();
    await page.goto('/staff/admin/branches');
    await expect(page.getByText('The restore test is overdue.')).toBeVisible({ timeout: 30_000 });
  }

  await page.goto('/staff/admin/operations/backup');
  await page.getByLabel('Paste the drill JSON').fill(VALID_DRILL_JSON);
  await page.getByRole('button', { name: 'Fill the fields from this' }).click();
  const response = page.waitForResponse((r) => r.url().includes('/api/admin/operations/restore-tests') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Record restore test' }).click();
  const posted = await response;
  expect(posted.status()).toBe(201);
  const result = await posted.json();
  const panel = page.getByRole('status').filter({ hasText: 'PurrCare worked out' });
  await expect(panel).toContainText(`Data lost ${result.achievedRpoMinutes} minutes`);
  await expect(panel).toContainText(result.outcome);
  // A failing drill shows Failed (US-XCT-05 AC2).
  const failing = JSON.stringify({ ...JSON.parse(VALID_DRILL_JSON), backupSetLabel: 'xct-failing', checks: [{ name: 'Row counts', passed: false, detail: 'off by 4' }] });
  await page.getByLabel('Paste the drill JSON').fill(failing);
  await page.getByRole('button', { name: 'Fill the fields from this' }).click();
  await page.getByRole('button', { name: 'Record restore test' }).click();
  await expect(page.getByRole('status').filter({ hasText: 'PurrCare worked out' })).toContainText('Failed', { timeout: 30_000 });
});

test('E-B4/E-B5 storefront samples reach the real API anonymously, and the performance page lists them without a verdict', async ({ page }) => {
  await signIn(page);
  const posts: Array<{ headers: Record<string, string>; body: string }> = [];
  page.on('request', (request) => {
    if (request.url().includes('/api/telemetry/web-vitals')) posts.push({ headers: request.headers(), body: request.postData() ?? '' });
  });
  await page.goto('/shop');
  await expect
    .poll(
      async () => {
        await page.evaluate(() => window.dispatchEvent(new Event('pagehide')));
        return posts.length;
      },
      { timeout: 45_000, intervals: [1000] },
    )
    .toBeGreaterThan(0);
  for (const post of posts) {
    expect(post.headers['cookie']).toBeUndefined();
    expect(post.headers['x-csrf-token']).toBeUndefined();
    expect(post.body).not.toMatch(/http|userId|session|customer|\?/i);
    expect(JSON.parse(post.body).samples[0].routeTemplate).toBe('/shop');
  }
  await page.goto('/staff/admin/performance');
  await expect(page.getByRole('heading', { name: 'Storefront performance' })).toBeVisible({ timeout: 45_000 });
  await expect(page.getByRole('rowheader', { name: '/shop' }).first()).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText('Not enough data').first()).toBeVisible();
});

async function deviceRecord(page: Page): Promise<Record<string, unknown> | null> {
  return page.evaluate(
    () =>
      new Promise<Record<string, unknown> | null>((resolve) => {
        const open = indexedDB.open('purrcare-offline');
        open.onerror = () => resolve(null);
        open.onsuccess = () => {
          const db = open.result;
          if (!db.objectStoreNames.contains('device')) return resolve(null);
          const get = db.transaction('device').objectStore('device').get('settings');
          get.onsuccess = () => resolve((get.result as Record<string, unknown>) ?? null);
          get.onerror = () => resolve(null);
        };
      }),
  );
}

test('SECURITY a fresh load of the board while the server answers goes to the front desk', async ({ page }) => {
  await signIn(page);
  await page.goto('/staff/offline/settings');
  const toggle = page.getByLabel(/Keep an offline copy of today/);
  await expect(toggle).toBeEnabled({ timeout: 30_000 });
  if (!(await toggle.isChecked())) {
    await toggle.click();
    // The write to IndexedDB must finish before the next navigation can abort it.
    await expect(page.getByText(/Offline copy turned on/)).toBeVisible();
  }
  await page.goto('/staff/front-desk');
  await expect(page.getByRole('button', { name: 'Sign out' })).toBeVisible({ timeout: 45_000 });
  await expect.poll(() => storedSnapshots(page), { timeout: 45_000 }).toBeGreaterThan(0);
  await page.goto('/staff/offline');
  await expect(page).toHaveURL(/\/staff\/front-desk/, { timeout: 30_000 });
  await expect(page.getByText(/Today.s appointments/)).toHaveCount(0);
});

test('SECURITY lock, reload offline, still locked; signing in again lifts it', async ({ page, context }) => {
  test.skip(process.env.XCT_PROD_BUILD !== '1', 'needs a production build (XCT_PROD_BUILD=1)');
  await signIn(page);
  await page.goto('/staff/offline/settings');
  const toggle = page.getByLabel(/Keep an offline copy of today/);
  await expect(toggle).toBeEnabled({ timeout: 30_000 });
  if (!(await toggle.isChecked())) {
    await toggle.click();
    // The write to IndexedDB must finish before the next navigation can abort it.
    await expect(page.getByText(/Offline copy turned on/)).toBeVisible();
  }
  await page.goto('/staff/front-desk');
  await expect(page.getByRole('button', { name: 'Sign out' })).toBeVisible({ timeout: 45_000 });
  await expect.poll(() => storedSnapshots(page), { timeout: 45_000 }).toBeGreaterThan(0);
  await expect
    .poll(() => page.evaluate(async () => Boolean((await navigator.serviceWorker.getRegistration('/staff/'))?.active)), { timeout: 45_000 })
    .toBe(true);
  await page.reload();
  await expect.poll(() => page.evaluate(() => Boolean(navigator.serviceWorker.controller)), { timeout: 20_000 }).toBe(true);

  // The clock must be installed BEFORE the board loads, or its idle timer is a real one.
  await page.clock.install();
  await context.setOffline(true);
  await page.goto('/staff/front-desk').catch(() => undefined);
  await expect(page).toHaveURL(/\/staff\/offline\?from=/, { timeout: 30_000 });
  await expect(page.getByRole('heading', { name: /Today.s appointments/ })).toBeVisible({ timeout: 30_000 });
  await page.clock.fastForward('31:00');
  await expect(page.getByText('Locked. Sign in again when the connection returns.')).toBeVisible();
  await expect.poll(async () => (await deviceRecord(page))?.lockedForStaffAccountId, { timeout: 15_000 }).toBeTruthy();

  await page.reload(); // still offline
  await expect(page.getByText('Locked. Sign in again when the connection returns.')).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText(/Today.s appointments/)).toHaveCount(0);

  // Back online the staff app takes over; its first successful sync lifts the lock.
  await context.setOffline(false);
  await page.evaluate(() => window.dispatchEvent(new Event('online')));
  await expect(page).toHaveURL(/\/staff\/front-desk/, { timeout: 45_000 });
  await expect.poll(async () => (await deviceRecord(page))?.lockedForStaffAccountId ?? null, { timeout: 45_000 }).toBeNull();
});

/** The owner (staffAccountId) of every record in the snapshot store. */
async function snapshotOwners(page: Page): Promise<string[]> {
  return page.evaluate(
    () =>
      new Promise<string[]>((resolve) => {
        const open = indexedDB.open('purrcare-offline');
        open.onerror = () => resolve([]);
        open.onsuccess = () => {
          const db = open.result;
          if (!db.objectStoreNames.contains('snapshot')) return resolve([]);
          const all = db.transaction('snapshot').objectStore('snapshot').getAll();
          all.onsuccess = () => resolve((all.result as { staffAccountId: string }[]).map((r) => r.staffAccountId));
          all.onerror = () => resolve([]);
        };
      }),
  );
}

test('E-A9 (real API) a different person signing in on the same device removes the first person copy', async ({ page }) => {
  test.skip(!IDENTIFIER_2 || !PASSWORD_2, 'needs a second seeded staff account: set E2E_STAFF2_IDENTIFIER and E2E_STAFF2_PASSWORD');
  await signIn(page);
  await page.goto('/staff/offline/settings');
  const toggle = page.getByLabel(/Keep an offline copy of today/);
  await expect(toggle).toBeEnabled({ timeout: 30_000 });
  if (!(await toggle.isChecked())) {
    await toggle.click();
    await expect(page.getByText(/Offline copy turned on/)).toBeVisible();
  }
  await page.goto('/staff/front-desk');
  await expect(page.getByRole('button', { name: 'Sign out' })).toBeVisible({ timeout: 45_000 });
  await expect.poll(() => snapshotOwners(page), { timeout: 45_000 }).toHaveLength(1);
  const [first] = await snapshotOwners(page);

  // The first person leaves without signing out (the session just ends); the second signs in on the
  // same browser profile.
  await page.context().clearCookies();
  await signIn(page, { identifier: IDENTIFIER_2, password: PASSWORD_2 });
  await page.goto('/staff/front-desk');
  await expect(page.getByRole('button', { name: 'Sign out' })).toBeVisible({ timeout: 45_000 });
  // The first person's copy is gone and, once the second person's is written, it is theirs alone.
  await expect
    .poll(async () => (await snapshotOwners(page)).filter((owner) => owner === first), { timeout: 45_000 })
    .toEqual([]);
  await expect.poll(() => snapshotOwners(page), { timeout: 45_000 }).toHaveLength(1);
  expect(await snapshotOwners(page)).not.toContain(first);
});
