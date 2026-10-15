import AxeBuilder from '@axe-core/playwright';
import type { Page } from '@playwright/test';
import { test, expect, signInAsStaff } from './fixtures';

/**
 * HO-XCT-002 section 18 A15 / section 16 E-X1 (backlog XCT row 106) -- axe over the offline board's
 * and the connectivity banner's own states, which the app-wide sweep cannot reach (a hard load of
 * `/staff/offline` while the mock ping answers shows only "Checking the connection…" and leaves).
 * Runs under MSW (`next dev`): the server is made unreachable through the mock's ping switch and
 * the board is reached by the banner's own link, as in `xct-offline.spec.ts`. Every state must have
 * zero serious or critical findings (the sweep's tags).
 */
test.describe.configure({ timeout: 120_000 });

interface XctMockWindow {
  __xctMock: {
    setPing: (mode: 'ok' | 'fail' | 'serverError') => void;
  };
}

async function seriousFindings(page: Page, state: string): Promise<void> {
  const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze();
  const serious = results.violations
    .filter((v) => v.impact === 'serious' || v.impact === 'critical')
    .map((v) => `${v.id} (${v.impact}): ${v.nodes.length} node(s), e.g. ${v.nodes[0]?.target.join(' ')}`);
  expect(serious, `${state}: serious or critical axe findings`).toEqual([]);
}

async function setPing(page: Page, mode: 'ok' | 'fail' | 'serverError') {
  await page.evaluate((m) => (window as unknown as XctMockWindow).__xctMock.setPing(m), mode);
}

async function optIn(page: Page) {
  await page.goto('/staff/offline/settings');
  const toggle = page.getByLabel(/Keep an offline copy of today/);
  await expect(toggle).toBeEnabled({ timeout: 20_000 });
  await toggle.click();
  await expect(toggle).toBeChecked();
}

async function shellReady(page: Page) {
  await expect(page.getByRole('button', { name: 'Sign out' })).toBeVisible({ timeout: 45_000 });
}

async function storedCopies(page: Page): Promise<number> {
  return page.evaluate(
    () =>
      new Promise<number>((resolve) => {
        const open = indexedDB.open('purrcare-offline');
        open.onerror = () => resolve(0);
        open.onsuccess = () => {
          const db = open.result;
          if (!db.objectStoreNames.contains('snapshot')) return resolve(0);
          const req = db.transaction('snapshot').objectStore('snapshot').count();
          req.onsuccess = () => resolve(req.result);
          req.onerror = () => resolve(0);
        };
      }),
  );
}

/** The dev server compiles the board on first use and may reload the page, resetting the ping switch. */
let boardCompiled = false;
async function compileBoardOnce(page: Page) {
  if (boardCompiled) return;
  await page.goto('/staff/offline');
  await expect(page).toHaveURL(/\/staff\/(front-desk|sign-in)/, { timeout: 60_000 });
  boardCompiled = true;
}

/** Front desk with a stored copy -> server down -> banner offline. */
async function frontDeskOffline(page: Page) {
  await compileBoardOnce(page);
  await page.goto('/staff/front-desk');
  await shellReady(page);
  await expect.poll(() => storedCopies(page), { timeout: 30_000 }).toBeGreaterThan(0);
  await setPing(page, 'fail');
  await expect(page.getByTestId('connectivity-banner')).toContainText(/You.re offline/, { timeout: 30_000 });
}

async function openBoardFromBanner(page: Page) {
  await page
    .getByTestId('connectivity-banner')
    .getByRole('link', { name: /Open today.s offline list/ })
    .click();
  await expect(page).toHaveURL(/\/staff\/offline\?from=/);
}

test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

test('A15 banner offline (with a saved copy), the board with data, then the locked board', async ({ page }) => {
  await optIn(page);
  await page.clock.install();
  await frontDeskOffline(page);
  await seriousFindings(page, 'banner offline, saved copy');

  await openBoardFromBanner(page);
  await expect(page.getByText('Offline · read-only')).toBeVisible({ timeout: 30_000 });
  await expect(page.getByRole('heading', { name: /Today.s appointments/ })).toBeVisible();
  await expect(page.getByRole('heading', { name: /boarding tasks/ })).toBeVisible();
  await expect(page.getByText('Luna')).toBeVisible();
  await seriousFindings(page, 'board with data');

  await page.clock.fastForward('31:00');
  await expect(page.getByText('Locked. Sign in again when the connection returns.')).toBeVisible();
  await expect(page.getByText('Luna')).toHaveCount(0);
  await seriousFindings(page, 'board locked');
});

test('A15 the board refuses a copy saved by someone else', async ({ page }) => {
  await optIn(page);
  await frontDeskOffline(page);
  // The copy on this device now belongs to another person (the mock's own switch resets on every
  // full load, so the stored record is re-attributed in place while the server is down).
  await page.evaluate(
    () =>
      new Promise<void>((resolve, reject) => {
        const open = indexedDB.open('purrcare-offline');
        open.onerror = () => reject(open.error);
        open.onsuccess = () => {
          const tx = open.result.transaction('snapshot', 'readwrite');
          const store = tx.objectStore('snapshot');
          const cursor = store.openCursor();
          cursor.onsuccess = () => {
            const c = cursor.result;
            if (!c) return;
            c.update({ ...c.value, staffAccountId: '00000000-0000-4000-8000-0000000000ee' });
            c.continue();
          };
          tx.oncomplete = () => resolve();
          tx.onerror = () => reject(tx.error);
        };
      }),
  );
  await openBoardFromBanner(page);
  await expect(page.getByRole('heading', { name: 'This copy was saved by someone else.' })).toBeVisible({
    timeout: 30_000,
  });
  await expect(page.getByText('Luna')).toHaveCount(0);
  await seriousFindings(page, 'board refusal (other person)');
});

test('A15 banner offline with no saved copy, then recovered', async ({ page }) => {
  await compileBoardOnce(page);
  await page.goto('/staff/front-desk');
  await shellReady(page);
  await setPing(page, 'fail');
  const banner = page.getByTestId('connectivity-banner');
  await expect(banner).toContainText(/no saved copy/, { timeout: 30_000 });
  await seriousFindings(page, 'banner offline, no saved copy');

  await setPing(page, 'ok');
  await expect(banner).toContainText('Back online', { timeout: 30_000 });
  await seriousFindings(page, 'banner recovered');
});

test('A15 banner degraded (the server answers with errors)', async ({ page }) => {
  await page.goto('/staff/front-desk');
  await shellReady(page);
  await setPing(page, 'serverError');
  await expect(page.getByTestId('connectivity-banner')).toContainText('The server is having trouble', {
    timeout: 30_000,
  });
  await seriousFindings(page, 'banner degraded');
});
