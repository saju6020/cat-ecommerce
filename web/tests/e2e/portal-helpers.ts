import { expect, type Page } from '@playwright/test';
import { signInAsCustomer } from './fixtures';

/**
 * HO-POR-002 e2e helpers (plain functions, not a spec).
 *
 * Diagnosis of the cold-start `/sign-in` redirect and the one `POST /api/auth/sign-in ... 404`:
 * `app/providers.tsx` starts MSW's worker in an effect AFTER first paint, while children (the
 * session guard, the dashboard query) fire their first `/api/...` fetch in their own effects --
 * which run first. Normally the guard's two 300 ms retries cover the gap; on a busy machine or a
 * cold dev-server compile the worker needs longer, the session read fails as a network error, and
 * the guard sends the customer to `/sign-in`. In the fixtures' own sign-in the same gap shows as a
 * request that reaches Next.js instead of MSW (a 404), because `window.__apiBaseUrl` is not set yet
 * and `fixtures.ts` waits for `__mswReady` only best-effort (it swallows its own 10 s timeout).
 * The race is in shared files (`providers.tsx`, `fixtures.ts`) outside HO-POR-002 section 13, so it is
 * closed here at the test boundary instead: an init script holds every page `fetch` to `/api/`
 * until the worker reports ready. No app code is changed and no retry hides a lost session.
 */
export async function holdApiUntilMswReady(page: Page): Promise<void> {
  await page.addInitScript(() => {
    const original = window.fetch.bind(window);
    window.fetch = async (...args: Parameters<typeof fetch>) => {
      const input = args[0];
      const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
      if (/\/api\//.test(url)) {
        const started = Date.now();
        while (
          (window as unknown as { __mswReady?: boolean }).__mswReady !== true &&
          Date.now() - started < 30_000
        ) {
          await new Promise((resolve) => setTimeout(resolve, 20));
        }
      }
      return original(...args);
    };
  });
}

/** Signs in and waits until the session endpoint answers 200 for this tab before returning. */
export async function signInEstablished(page: Page): Promise<void> {
  await holdApiUntilMswReady(page);
  if (page.url() === 'about:blank') await page.goto('/sign-in');
  await page.waitForFunction(
    () =>
      Boolean((window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl) &&
      (window as unknown as { __mswReady?: boolean }).__mswReady === true,
    undefined,
    { timeout: 60_000 },
  );
  await signInAsCustomer(page);
  await expect
    .poll(
      () =>
        page.evaluate(async () => {
          const base = (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl ?? '';
          const response = await fetch(`${base}/api/auth/session`, { credentials: 'include' });
          return response.status;
        }),
      { message: 'customer session established', timeout: 10_000 },
    )
    .toBe(200);
}

/** Selects the dashboard the mock answers with (the handlers read this cookie in the page). */
export async function selectPortalScenario(page: Page, name: string): Promise<void> {
  await page
    .context()
    .addCookies([{ name: 'pc_mock_portal', value: name, domain: 'localhost', path: '/' }]);
}
