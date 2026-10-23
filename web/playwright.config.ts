import { defineConfig, devices, type PlaywrightTestConfig } from '@playwright/test';

// HO-CAT-002 §16 — Playwright E2E, run against MSW first, then against the real API (§5.1).
// Each area adds its own specs under tests/e2e/; this config is shared and shouldn't need
// per-area edits.
//
// Port is read from `process.env.PORT` (default 3000, unchanged) so multiple worktrees on the
// same machine can each run their own dev server + Playwright pair without one worktree's
// `reuseExistingServer` silently attaching to another worktree's server on :3000 and producing
// results for the wrong build. `next dev` itself already honours `PORT`; `webServer.command`
// inherits the parent process's env by default, so setting `PORT` before `npx playwright test`
// is enough — no other file needs to change.
const PORT = process.env.PORT ?? '3000';
const BASE_URL = `http://localhost:${PORT}`;

// CODE_REVIEW suggestion (second pass) — `tests/e2e/real-api-verification.spec.ts` needs a live
// database and a live API process this repo cannot start on its own (its own doc comment), and
// asserts real-backend-specific behaviour that fails outright against MSW. It used to be excluded
// from the default MSW run only by a comment in that file; a project-level `testIgnore` on every
// project the default `npm run test:e2e` script runs makes that structural instead — the file is
// still runnable, deliberately, via the separate `real-api` project below. See the ARCHITECTURE
// gate third-pass comment just below this one for exactly how that project is reached (a bare
// `--project=real-api` is no longer enough on its own — `E2E_REAL_API=1` is required too).
const REAL_API_SPEC = 'real-api-verification.spec.ts';

// ARCHITECTURE gate third pass, MEDIUM — the comment above (and the one on the `real-api` project
// itself, below) used to claim this project is "not run at all unless `--project=real-api` is
// passed explicitly", but that was only true of this repo's own npm scripts: Playwright's own
// default behaviour, with NO `--project` filter at all (a bare `npx playwright test`), is to run
// EVERY project in this array, `real-api` included — which then fails outright, since nothing
// starts a real API process or points `NEXT_PUBLIC_USE_MOCKS=false` at one. Gating the project's
// own existence in this array on `E2E_REAL_API=1` (rather than trusting every possible invocation
// to remember `--project`) makes the comment's claim actually true: `real-api` simply is not one of
// the projects Playwright even knows about unless that variable is set, the same way `PORT` above
// is an opt-in env var a developer sets deliberately, never a default. `test:e2e:real-api`
// (`package.json`) sets it — via `scripts/run-real-api-e2e.mjs` (ARCHITECTURE gate fourth pass,
// LOW), not an inline `E2E_REAL_API=1 playwright test ...`, which is POSIX-only shell syntax that
// fails outright on this repo's own Windows dev hosts (`cmd.exe`, `npm`'s own default
// `script-shell` there).
const projects: NonNullable<PlaywrightTestConfig['projects']> = [
  {
    name: 'chromium',
    testIgnore: `**/${REAL_API_SPEC}`,
    use: { ...devices['Desktop Chrome'] },
  },
  {
    name: 'mobile-360',
    testIgnore: `**/${REAL_API_SPEC}`,
    use: { viewport: { width: 360, height: 780 } }, // UI-1 floor
  },
];

if (process.env.E2E_REAL_API === '1') {
  // Manual only (§5.1), opt-in only. Needs NEXT_PUBLIC_USE_MOCKS=false and a real API process
  // already running; see real-api-verification.spec.ts's own doc comment.
  projects.push({
    name: 'real-api',
    testMatch: `**/${REAL_API_SPEC}`,
    use: { ...devices['Desktop Chrome'] },
  });
}

export default defineConfig({
  testDir: './tests/e2e',
  // Issues.md #2: refuses port 3000 and any real API on catecommerce_dev before a single test runs.
  globalSetup: './tests/e2e/global-setup.ts',
  fullyParallel: true,
  retries: process.env.CI ? 1 : 0,
  reporter: [['html', { open: 'never' }]],
  use: {
    baseURL: BASE_URL,
    trace: 'on-first-retry',
  },
  projects,
  webServer: {
    command: 'npm run dev',
    url: BASE_URL,
    reuseExistingServer: !process.env.CI,
  },
});
