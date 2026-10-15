import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test, expect } from '@playwright/test';
import { loadInventory } from './inventory';

/**
 * HO-XCT-002 section 11.6, E-B1, NFR-PRF-1 -- the lab LCP budget. Per budget route: a fresh
 * context, CDP `Network.emulateNetworkConditions` (Slow 4G: 1.6 Mbps down, 150 ms RTT) and
 * `Emulation.setCPUThrottlingRate(4)`, a `PerformanceObserver` for `largest-contentful-paint`, five
 * runs, and the MEDIAN compared with `lcpMs` from `perf-budgets.json`. Chromium only.
 *
 * Needs a PRODUCTION build against the REAL API (`PERF_SERVER=start`, built with
 * `NEXT_PUBLIC_USE_MOCKS=false`); the throttled numbers from `next dev` mean nothing. `/portal` and
 * `/checkout` need a signed-in customer: set `E2E_CUSTOMER_IDENTIFIER` and `E2E_CUSTOMER_PASSWORD`
 * (a customer who has cats, orders and invoices); without them those rows are skipped, with the
 * reason. The budget is never relaxed to make the run green (ADR-FE-148).
 */
const budgets = JSON.parse(
  readFileSync(path.resolve(__dirname, '../../perf-budgets.json'), 'utf8'),
) as { lcpMs: number; routes: string[] };
const RUNS = 5;
const SLOW_4G = {
  offline: false,
  latency: 150,
  downloadThroughput: (1.6 * 1024 * 1024) / 8,
  uploadThroughput: (750 * 1024) / 8,
};

const inventory = loadInventory();

function median(values: number[]): number {
  const sorted = [...values].sort((a, b) => a - b);
  return sorted[Math.floor(sorted.length / 2)] as number;
}

test('the budget names only routes that exist', () => {
  const known = new Set(inventory.map((r) => r.route));
  const missing = budgets.routes.filter((route) => !known.has(route));
  expect(missing, 'perf-budgets.json names routes that no longer exist').toEqual([]);
});

for (const route of budgets.routes) {
  test(`E-B1 median LCP of ${route} is within ${budgets.lcpMs} ms on Slow 4G + 4x CPU`, async ({
    browser,
    browserName,
  }) => {
    test.skip(browserName !== 'chromium', 'CDP throttling is Chromium only');
    const entry = inventory.find((r) => r.route === route);
    // A budget route that no longer exists FAILS (section 11.6); it is never skipped.
    expect(entry, `${route} is named in perf-budgets.json but is not in the route inventory`).toBeDefined();
    const needsCustomer = entry?.audience === 'customer';
    const identifier = process.env.E2E_CUSTOMER_IDENTIFIER;
    const password = process.env.E2E_CUSTOMER_PASSWORD;
    test.skip(
      needsCustomer && (!identifier || !password),
      'BLOCKED: needs E2E_CUSTOMER_IDENTIFIER / E2E_CUSTOMER_PASSWORD (a customer with cats, orders and invoices) on the real API',
    );
    test.setTimeout(RUNS * 60_000);

    const samples: number[] = [];
    for (let run = 0; run < RUNS; run++) {
      const context = await browser.newContext();
      const page = await context.newPage();
      if (needsCustomer && identifier && password) {
        const api = process.env.NEXT_PUBLIC_API_BASE_URL ?? 'http://localhost:5005';
        await page.goto('/sign-in');
        await page.evaluate(
          async ({ api, identifier, password }) => {
            await fetch(`${api}/api/auth/sign-in`, {
              method: 'POST',
              credentials: 'include',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ identifier, password }),
            });
          },
          { api, identifier, password },
        );
      }
      const client = await context.newCDPSession(page);
      await client.send('Network.enable');
      await client.send('Network.emulateNetworkConditions', SLOW_4G);
      await client.send('Emulation.setCPUThrottlingRate', { rate: 4 });
      await page.addInitScript(() => {
        (window as unknown as { __lcp: number }).__lcp = 0;
        new PerformanceObserver((list) => {
          for (const e of list.getEntries()) {
            (window as unknown as { __lcp: number }).__lcp = e.startTime;
          }
        }).observe({ type: 'largest-contentful-paint', buffered: true });
      });
      await page.goto(entry?.sampleUrl ?? route, { waitUntil: 'load', timeout: 60_000 });
      await page.waitForTimeout(1500);
      // LCP is final on first input or when the page is hidden.
      await page.keyboard.press('Tab');
      samples.push(await page.evaluate(() => (window as unknown as { __lcp: number }).__lcp));
      await context.close();
    }
    const result = median(samples);
    test.info().annotations.push({
      type: 'lcp',
      description: `${route}: median ${Math.round(result)} ms of [${samples.map((s) => Math.round(s)).join(', ')}]`,
    });
    console.log(`LCP ${route}: median ${Math.round(result)} ms of [${samples.map((v) => Math.round(v)).join(", ")}]`);
    expect(result, `${route}: median LCP of ${samples.map(Math.round).join(', ')} ms`).toBeGreaterThan(0);
    expect(result).toBeLessThanOrEqual(budgets.lcpMs);
  });
}
