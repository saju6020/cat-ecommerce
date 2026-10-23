#!/usr/bin/env node
// Runs the real-API customer + staff journey (tests/e2e/journey-real-api.spec.ts) end to end on its OWN database:
//
//   PGPASSWORD=... SEED_STAFF_PASSWORD=... SEED_CUSTOMER_PASSWORD=... \
//     npm run test:e2e:journey -- --db catecommerce_journey
//
//   1. refuses catecommerce_dev / catecommerce_test and the developer's ports (5005 API, 3000 web);
//   2. makes sure the database is migrated and seeded (seed-dev-db.mjs, idempotent) - skipped when the seeded
//      customer and staff administrator can already sign in; force with --reseed;
//   3. starts a private copy of the API on --api-port (default 5098) with the seeding overrides (TEST1 captcha);
//   4. starts `next dev` on --web-port (default 3098) from a PRIVATE COPY of the web sources with its own `.next`
//      (node_modules is a directory junction/symlink to this folder's), so it can never share or clobber the
//      `web/.next` of a developer's own `next dev` running from this folder;
//   5. warms the pages the journey uses, runs the spec with --workers=1, and stops everything it started.
//
// Options: --db <name> (required), --api-port <n>, --web-port <n>, --reseed, --skip-build (reuse the last Release
// build of the API), --keep-copy (keep the web copy and API log for inspection), and any further arguments are passed
// to `playwright test` (for example `-g "4\."`).
//
// A journey registers a customer and places an order, so it must never run against a shared database.

import { spawn, spawnSync } from 'node:child_process';
import {
  cpSync,
  existsSync,
  mkdirSync,
  mkdtempSync,
  rmSync,
  rmdirSync,
  symlinkSync,
} from 'node:fs';
import { createRequire } from 'node:module';
import { tmpdir } from 'node:os';
import { dirname, join, parse, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { assertSafeTarget, startSeededApi } from './start-seeded-api.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const webRoot = resolve(here, '..');
const require = createRequire(import.meta.url);

function parseArgs(argv) {
  const out = { rest: [] };
  const flags = new Set(['reseed', 'skip-build', 'keep-copy']);
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (!a.startsWith('--')) {
      out.rest.push(a);
      continue;
    }
    const key = a.slice(2);
    if (['db', 'api-port', 'web-port'].includes(key)) {
      out[key] = argv[++i];
      continue;
    }
    if (flags.has(key)) {
      out[key] = true;
      continue;
    }
    out.rest.push(a); // not ours: hand to playwright
  }
  return out;
}

const log = (m) => console.log(`journey: ${m}`);
const args = parseArgs(process.argv.slice(2));
const db = String(args.db ?? '');
const apiPort = Number(args['api-port'] ?? 5098);
const webPort = Number(args['web-port'] ?? 3098);

try {
  assertSafeTarget({ db, port: apiPort });
  if (!Number.isInteger(webPort) || webPort === 3000 || webPort === 5005 || webPort === apiPort) {
    throw new Error(`refusing web port ${webPort} (3000 is the developer's app).`);
  }
  if (!process.env.SEED_STAFF_PASSWORD || !process.env.SEED_CUSTOMER_PASSWORD) {
    throw new Error(
      'set SEED_STAFF_PASSWORD and SEED_CUSTOMER_PASSWORD (at least 10 characters each; never printed).',
    );
  }
} catch (err) {
  console.error(`journey: ${err.message}`);
  process.exit(1);
}

const apiBase = `http://localhost:${apiPort}`;
const webBase = `http://localhost:${webPort}`;
const customer = {
  email: process.env.JOURNEY_CUSTOMER_EMAIL ?? 'customer@purrcare.test',
  password: process.env.SEED_CUSTOMER_PASSWORD,
};
const staff = {
  identifier: process.env.E2E_STAFF_ADMIN ?? 'e2e.admin@purrcare.test',
  password: process.env.SEED_STAFF_PASSWORD,
};

// ---- helpers --------------------------------------------------------------------------------------------------

function killTree(child) {
  if (!child || child.exitCode !== null || child.pid === undefined) return;
  if (process.platform === 'win32')
    spawnSync('taskkill', ['/pid', String(child.pid), '/T', '/F'], { stdio: 'ignore' });
  else {
    try {
      process.kill(-child.pid, 'SIGTERM');
    } catch {
      try {
        child.kill();
      } catch {
        /* gone */
      }
    }
  }
}

async function canSignIn(path, body) {
  try {
    const response = await fetch(`${apiBase}${path}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
      signal: AbortSignal.timeout(15_000),
    });
    return response.status === 200;
  } catch {
    return false;
  }
}

async function seededAlready(apiHandle) {
  if (!apiHandle) return false;
  return (
    (await canSignIn('/api/auth/sign-in', {
      identifier: customer.email,
      password: customer.password,
    })) &&
    (await canSignIn('/api/staff/auth/sign-in', {
      identifier: staff.identifier,
      password: staff.password,
    }))
  );
}

/** A private copy of the web sources, so `next dev` gets its own `.next`. */
function makeWebCopy() {
  // Next resolves its own files relative to the project folder and breaks when node_modules (reached through the
  // junction) is on another drive ("Can't resolve './D:/...'"), so the copy lives on the same drive as web/: in the OS
  // temp folder when that is the same drive, otherwise in a hidden folder at the drive root.
  const sameDrive = parse(tmpdir()).root.toLowerCase() === parse(webRoot).root.toLowerCase();
  const base = sameDrive ? tmpdir() : join(parse(webRoot).root, '.purrcare-journey-tmp');
  mkdirSync(base, { recursive: true });
  const dir = mkdtempSync(join(base, 'purrcare-journey-web-'));
  const keep = [
    'app',
    'components',
    'lib',
    'mocks',
    'nav',
    'public',
    'instrumentation.ts',
    'next.config.ts',
    'package.json',
    'postcss.config.mjs',
    'tsconfig.json',
    'next-env.d.ts',
    'perf-budgets.json',
  ];
  for (const name of keep) {
    const from = join(webRoot, name);
    if (existsSync(from)) cpSync(from, join(dir, name), { recursive: true });
  }
  mkdirSync(join(dir, '.next'), { recursive: true });
  symlinkSync(join(webRoot, 'node_modules'), join(dir, 'node_modules'), 'junction');
  return dir;
}

function removeWebCopy(dir) {
  // Remove the link to node_modules FIRST, without recursion, so nothing can ever follow it into the real folder.
  try {
    rmdirSync(join(dir, 'node_modules'));
  } catch {
    /* already gone */
  }
  if (!existsSync(join(webRoot, 'node_modules'))) {
    console.error('journey: WARNING - web/node_modules is missing after cleanup; run npm install.');
    return;
  }
  try {
    rmSync(dir, { recursive: true, force: true });
  } catch {
    /* a locked file: leave it in tmp */
  }
}

async function waitFor(url, ms, what) {
  const deadline = Date.now() + ms;
  for (;;) {
    try {
      const r = await fetch(url, { signal: AbortSignal.timeout(20_000) });
      if (r.status < 500) return;
    } catch {
      /* not yet */
    }
    if (Date.now() > deadline)
      throw new Error(`${what} did not answer on ${url} within ${Math.round(ms / 1000)} s`);
    await new Promise((r) => setTimeout(r, 1500));
  }
}

// ---- the run --------------------------------------------------------------------------------------------------

let api = null;
let web = null;
let webCopy = null;
let exitCode = 1;

async function cleanup() {
  killTree(web);
  if (api) await api.stop();
  if (webCopy && !args['keep-copy']) {
    await new Promise((r) => setTimeout(r, 1500));
    removeWebCopy(webCopy);
  }
}
process.on('SIGINT', async () => {
  await cleanup();
  process.exit(130);
});

try {
  // 1. database + API
  const dbExists =
    spawnSync(
      process.env.PSQL ??
        (existsSync('C:/Program Files/PostgreSQL/17/bin/psql.exe')
          ? 'C:/Program Files/PostgreSQL/17/bin/psql.exe'
          : 'psql'),
      [
        '-h',
        process.env.PGHOST ?? 'localhost',
        '-p',
        process.env.PGPORT ?? '5432',
        '-U',
        process.env.PGUSER ?? 'postgres',
        '-d',
        'postgres',
        '-At',
        '-c',
        `SELECT 1 FROM pg_database WHERE datname='${db}'`,
      ],
      { encoding: 'utf8', env: process.env },
    ).stdout?.trim() === '1';

  if (dbExists && !args.reseed) {
    try {
      api = await startSeededApi({
        db,
        port: apiPort,
        webPort,
        skipBuild: Boolean(args['skip-build']),
      });
    } catch (err) {
      log(
        `could not start the API on the existing database (${err.message.split('\n')[0]}); will (re)seed it`,
      );
    }
  }
  if (!(await seededAlready(api))) {
    if (api) {
      await api.stop();
      api = null;
    }
    log(`seeding ${db} (this builds and migrates; a few minutes the first time)`);
    const seeded = spawnSync(
      process.execPath,
      [
        join(here, 'seed-dev-db.mjs'),
        '--db',
        db,
        '--port',
        String(apiPort),
        ...(args['skip-build'] ? ['--skip-build'] : []),
      ],
      { stdio: 'inherit', env: process.env },
    );
    if (seeded.status !== 0) throw new Error('seeding failed (see above).');
    api = await startSeededApi({ db, port: apiPort, webPort, skipBuild: true });
  }
  const whichDb = await (await fetch(`${apiBase}/api/dev/database`)).json().catch(() => ({}));
  if (String(whichDb.database).toLowerCase() !== db)
    throw new Error(`the API reports database "${whichDb.database}", expected ${db}; stopping.`);
  log(`API up on ${apiBase} (database ${db})`);

  // 2. web app from a private copy
  webCopy = makeWebCopy();
  log(`starting next dev on ${webBase} from a private copy (${webCopy})`);
  const nextBin = join(
    dirname(require.resolve('next/package.json', { paths: [webRoot] })),
    'dist',
    'bin',
    'next',
  );
  web = spawn(process.execPath, [nextBin, 'dev', '-p', String(webPort)], {
    cwd: webCopy,
    env: {
      ...process.env,
      PORT: String(webPort),
      NEXT_PUBLIC_USE_MOCKS: 'false',
      NEXT_PUBLIC_API_BASE_URL: apiBase,
      NEXT_TELEMETRY_DISABLED: '1',
    },
    stdio: ['ignore', 'ignore', 'inherit'],
    shell: false,
    windowsHide: true,
  });
  await waitFor(`${webBase}/sign-in`, 240_000, 'next dev');
  log('warming pages (the first compile of each route is slow)');
  for (const path of [
    '/shop',
    '/shop/products',
    '/shop/products/whiskas-adult-ocean-fish-dry',
    '/cart',
    '/checkout',
    '/register',
    '/portal/orders',
    '/policies/privacy',
    '/staff/sign-in',
    '/staff',
    '/staff/orders',
    '/staff/admin/delivery-zones',
    '/staff/admin/tax-classes',
    '/staff/admin/policies',
    '/staff/admin/branches',
  ]) {
    await waitFor(`${webBase}${path}`, 240_000, `page ${path}`).catch(() => undefined);
  }

  // 3. the journey
  const cli = join(
    dirname(require.resolve('@playwright/test/package.json', { paths: [webRoot] })),
    require('@playwright/test/package.json').bin.playwright,
  );
  const env = {
    ...process.env,
    JOURNEY_REAL_API: '1',
    PORT: String(webPort),
    NEXT_PUBLIC_USE_MOCKS: 'false',
    NEXT_PUBLIC_API_BASE_URL: apiBase,
    JOURNEY_CUSTOMER_EMAIL: customer.email,
    JOURNEY_CUSTOMER_PASSWORD: customer.password,
    E2E_STAFF_ADMIN: staff.identifier,
    E2E_STAFF_PASSWORD: staff.password,
  };
  delete env.CI; // playwright.config.ts would otherwise try to start its own `npm run dev` here
  const result = spawnSync(
    process.execPath,
    [
      cli,
      'test',
      'journey-real-api',
      '--project=chromium',
      '--workers=1',
      '--reporter=list',
      ...args.rest,
    ],
    { cwd: webRoot, stdio: 'inherit', env, shell: false },
  );
  exitCode = result.status ?? 1;
} catch (err) {
  console.error(`journey: ${err.message}`);
  exitCode = 1;
} finally {
  await cleanup();
}
process.exit(exitCode);
