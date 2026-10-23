/**
 * Runs once before any Playwright test (Issues.md #2, 2026-10-15). Browser tests must never write into
 * `catecommerce_dev`, the database behind the developer's own app on :3000/:5005 — test rows left there are what
 * broke the shop's banners, brand filter, policy prompt and checkout prices.
 *
 * 1. Port 3000 is refused: `reuseExistingServer` would otherwise quietly run the suite against the developer's own
 *    `next dev`, which talks to the real API and dev database even when this run meant to use MSW mocks.
 * 2. A real-API run (`NEXT_PUBLIC_USE_MOCKS=false`) must name its API, and that API must confirm — through its
 *    Development-only `GET /api/dev/database` — that it is NOT on `catecommerce_dev`. If it cannot confirm, the run stops.
 */
const DEV_DATABASE = 'catecommerce_dev';

export default async function globalSetup() {
  const port = process.env.PORT ?? '3000';
  if (port === '3000') {
    throw new Error(
      'Playwright must run on its own PORT (e.g. PORT=3117): port 3000 is the developer’s own app, which uses catecommerce_dev.',
    );
  }

  if (process.env.NEXT_PUBLIC_USE_MOCKS !== 'false') return;

  const api = process.env.NEXT_PUBLIC_API_BASE_URL;
  if (!api)
    throw new Error('A real-API run needs NEXT_PUBLIC_API_BASE_URL (an API on its own database).');

  let database: string | undefined;
  try {
    const response = await fetch(`${api.replace(/\/$/, '')}/api/dev/database`);
    if (response.ok) database = ((await response.json()) as { database?: string }).database;
  } catch {
    // handled below
  }
  if (!database) {
    throw new Error(
      `Could not confirm which database ${api} uses (GET /api/dev/database). Refusing to run.`,
    );
  }
  if (database.toLowerCase() === DEV_DATABASE) {
    throw new Error(
      `${api} uses ${DEV_DATABASE}. Start a separate API on its own database (see docs/runbooks/dev-seed.md) and point NEXT_PUBLIC_API_BASE_URL at it.`,
    );
  }
}
