import { defineConfig, devices } from '@playwright/test';

/**
 * HO-XCT-002 section 11.6 / ADR-FE-147 -- the Playwright config for the budget and sweep specs
 * (`tests/perf/**`, `tests/a11y/**`). The default config (`web/playwright.config.ts`) only looks in
 * `tests/e2e/`, and it is outside this slice's scope, so these specs have their own.
 *
 *  - Sweeps (viewport, axe) run against `next dev` with the MSW mocks (the default), so every
 *    route has data. They are slow on a cold dev server: run one audience at a time with
 *    `SWEEP_AUDIENCE=public|customer|staff|none` and `--workers=1`.
 *  - The LCP lab budget needs a PRODUCTION build and the REAL API (mocks are dev-only), so set
 *    `PERF_SERVER=start` (and build with `NEXT_PUBLIC_USE_MOCKS=false`).
 *
 * `PORT` is honoured like the default config, so worktrees do not attach to each other's servers.
 */
const PORT = process.env.PORT ?? '3000';
const BASE_URL = `http://localhost:${PORT}`;
const command = process.env.PERF_SERVER === 'start' ? 'npm run start' : 'npm run dev';

export default defineConfig({
  testDir: '..',
  testMatch: ['perf/**/*.spec.ts', 'a11y/**/*.spec.ts'],
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 120_000,
  reporter: [['list']],
  use: { baseURL: BASE_URL, trace: 'off' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command,
    url: BASE_URL,
    reuseExistingServer: !process.env.CI,
    timeout: 180_000,
    env: { PORT },
  },
});
