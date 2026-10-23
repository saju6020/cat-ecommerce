#!/usr/bin/env node
// End-to-end dev database builder (docs/runbooks/dev-seed.md):
//   create the database (if missing) -> apply the EF migrations -> insert the one bootstrap
//   Administrator row -> start a private copy of the API on --port (default 5099) against it ->
//   run seed-dev.mjs (and its --verify checks) -> stop the API.
//
//   PGPASSWORD=... SEED_STAFF_PASSWORD=... SEED_CUSTOMER_PASSWORD=... \
//     node web/scripts/seed-dev-db.mjs --db catecommerce_seedcheck
//
// Options:
//   --db <name>        REQUIRED. The database to build. catecommerce_test is always refused;
//                      catecommerce_dev is refused unless --i-mean-dev is also passed.
//   --i-mean-dev       Allow --db catecommerce_dev (back it up first; see the runbook).
//   --port <n>         Port for the seeding API (default 5099). Must be free; 5005 and 3000 are refused.
//   --skip-build       Reuse the last Release build of the API (src/Api/.../bin/Release).
//   --no-verify        Skip the read-only checks after seeding.
//   --keep-log         Keep the API log (it contains used, single-use reset links) instead of deleting it.
//   PGHOST / PGPORT / PGUSER (default localhost / 5432 / postgres) and PGPASSWORD (required) locate
//   PostgreSQL; PSQL may point at psql(.exe) if it is not on PATH.
//
// Why a private API copy: the developer's own API usually runs from bin/Debug (which locks those
// files), and its appsettings.Development.json points at catecommerce_dev. This script builds in
// Release, publishes to a temp folder and deletes appsettings.Development.json from that copy, so
// the seeding API can only ever reach the database named here.

import { spawn, spawnSync } from 'node:child_process';
import { existsSync, mkdtempSync, openSync, closeSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repo = resolve(here, '..', '..');
const apiProject = join(repo, 'src', 'Api', 'CatEcommerce.Api');
const infraProject = join(repo, 'src', 'Infrastructure', 'CatEcommerce.Infrastructure');

function parseArgs(argv) {
  const out = {};
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (!a.startsWith('--')) continue;
    const next = argv[i + 1];
    if (next === undefined || next.startsWith('--')) out[a.slice(2)] = true;
    else {
      out[a.slice(2)] = next;
      i++;
    }
  }
  return out;
}

function fail(message) {
  console.error(`seed-dev-db: ${message}`);
  process.exit(1);
}
const log = (m) => console.log(`seed-dev-db: ${m}`);

const args = parseArgs(process.argv.slice(2));
const db = typeof args.db === 'string' ? args.db : '';
const port = Number(args.port ?? 5099);

// ── refusals first ───────────────────────────────────────────────────────────────────────────────
if (!db)
  fail('pass the target database explicitly: --db <name> (e.g. --db catecommerce_seedcheck).');
if (!/^[a-z][a-z0-9_]{0,62}$/.test(db)) fail(`"${db}" is not a plain lower-case database name.`);
if (db === 'catecommerce_test')
  fail('refusing catecommerce_test: it belongs to the integration tests.');
if (db === 'catecommerce_dev' && !args['i-mean-dev'])
  fail(
    'refusing catecommerce_dev without --i-mean-dev (back it up first, see docs/runbooks/dev-seed.md).',
  );
if (!Number.isInteger(port) || port < 1024 || port > 65535)
  fail('--port must be a number between 1024 and 65535.');
if (port === 5005 || port === 3000)
  fail(`refusing port ${port}: that is the developer's own API / web app.`);
if (!process.env.PGPASSWORD) fail('set PGPASSWORD (the PostgreSQL password; it is never printed).');
if (!process.env.SEED_STAFF_PASSWORD || !process.env.SEED_CUSTOMER_PASSWORD)
  fail('set SEED_STAFF_PASSWORD and SEED_CUSTOMER_PASSWORD (at least 10 characters each).');

const pg = {
  host: process.env.PGHOST ?? 'localhost',
  port: process.env.PGPORT ?? '5432',
  user: process.env.PGUSER ?? 'postgres',
};
const psql =
  process.env.PSQL ??
  (existsSync('C:/Program Files/PostgreSQL/17/bin/psql.exe')
    ? 'C:/Program Files/PostgreSQL/17/bin/psql.exe'
    : 'psql');
const connectionString = `Host=${pg.host};Port=${pg.port};Database=${db};Username=${pg.user};Password=${process.env.PGPASSWORD}`;
const apiBase = `http://localhost:${port}`;

// ── helpers ──────────────────────────────────────────────────────────────────────────────────────
function run(command, commandArgs, { label, quiet = false } = {}) {
  log(label ?? `${command} ${commandArgs[0] ?? ''}`);
  const r = spawnSync(command, commandArgs, {
    cwd: repo,
    stdio: quiet ? ['ignore', 'pipe', 'pipe'] : 'inherit',
    shell: false,
    encoding: 'utf8',
  });
  if (r.error) fail(`${label ?? command}: ${r.error.message}`);
  if (r.status !== 0) {
    if (quiet) process.stderr.write(redact(`${r.stdout ?? ''}${r.stderr ?? ''}`).slice(-4000));
    fail(`${label ?? command} failed (exit ${r.status}).`);
  }
  return r.stdout ?? '';
}

function redact(text) {
  return String(text).split(process.env.PGPASSWORD).join('***');
}

function sql(database, statement) {
  const r = spawnSync(
    psql,
    [
      '-h',
      pg.host,
      '-p',
      pg.port,
      '-U',
      pg.user,
      '-d',
      database,
      '-v',
      'ON_ERROR_STOP=1',
      '-q',
      '-At',
      '-c',
      statement,
    ],
    { env: process.env, encoding: 'utf8', shell: false },
  );
  if (r.error) fail(`psql: ${r.error.message} (set PSQL to the psql executable)`);
  if (r.status !== 0) fail(`psql failed: ${redact(r.stderr).trim()}`);
  return r.stdout.trim();
}

async function isListening(url) {
  try {
    await fetch(url, { signal: AbortSignal.timeout(1500) });
    return true;
  } catch {
    return false;
  }
}

// ── 1. database ──────────────────────────────────────────────────────────────────────────────────
if (await isListening(`${apiBase}/api/branches`))
  fail(`something already answers on ${apiBase}; stop it or pass another --port.`);

const exists = sql('postgres', `SELECT 1 FROM pg_database WHERE datname = '${db}'`) === '1';
if (exists) log(`database ${db} exists - migrating and seeding it in place`);
else {
  sql('postgres', `CREATE DATABASE "${db}"`);
  log(`created database ${db}`);
}

// ── 2. build + migrate ───────────────────────────────────────────────────────────────────────────
if (!args['skip-build'])
  run('dotnet', ['build', apiProject, '-c', 'Release', '-nologo', '-v', 'q'], {
    label: 'building the API (Release)',
    quiet: true,
  });
run(
  'dotnet',
  [
    'ef',
    'database',
    'update',
    '--project',
    infraProject,
    '--startup-project',
    apiProject,
    '--configuration',
    'Release',
    '--no-build',
    '--connection',
    connectionString,
  ],
  { label: `applying EF migrations to ${db}`, quiet: true },
);

// ── 3. the one row the API cannot create: the first Administrator (no password yet) ─────────────
// Every staff endpoint needs a signed-in Administrator, so the very first one has to exist before the
// API can be used at all. It is inserted exactly as CreateStaffAccountCommand would (no password);
// seed-dev.mjs then sets its password through the normal reset flow.
const bootstrap = sql(
  db,
  `
  INSERT INTO account.staff_account ("Id", full_name, mobile_number, email_address, failed_login_count, security_stamp, role_ids,
    "CreatedBy", "CreatedDate", "LastUpdatedDate", "LastUpdatedBy", "TenantId", "VerticalId", "IsMarkedToDelete", "Version")
  SELECT gen_random_uuid(), 'Farhana Akter', '+8801711000001', 'e2e.admin@purrcare.test', 0, gen_random_uuid(), ARRAY['Administrator']::varchar(50)[],
    '00000000-0000-0000-0000-000000000000', now(), now(), '00000000-0000-0000-0000-000000000000',
    '00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000000', false, 1
  WHERE NOT EXISTS (SELECT 1 FROM account.staff_account WHERE email_address = 'e2e.admin@purrcare.test')
  RETURNING 1`,
);
log(
  bootstrap === '1'
    ? 'inserted the bootstrap Administrator e2e.admin@purrcare.test (password set next, via reset)'
    : 'bootstrap Administrator already present',
);

// ── 4. private API copy ──────────────────────────────────────────────────────────────────────────
const work = mkdtempSync(join(tmpdir(), 'purrcare-seed-'));
const apiDir = join(work, 'api');
run(
  'dotnet',
  ['publish', apiProject, '-c', 'Release', '--no-build', '-nologo', '-v', 'q', '-o', apiDir],
  { label: 'publishing a private copy of the API', quiet: true },
);
rmSync(join(apiDir, 'appsettings.Development.json'), { force: true }); // its connection string names catecommerce_dev
const apiLog = join(work, 'api.log');
const exe = join(
  apiDir,
  process.platform === 'win32' ? 'CatEcommerce.Api.exe' : 'CatEcommerce.Api',
);

const apiEnv = {
  ...process.env,
  ASPNETCORE_ENVIRONMENT: 'Development',
  ASPNETCORE_URLS: apiBase,
  ConnectionStrings__CatEcommerce: connectionString,
  // Reset links must be absolute https URLs or the message is suppressed as RenderFailed.
  Account__WebBaseUrl: 'https://localhost:3000',
  PublicSite__WebBaseUrl: 'https://localhost:3000',
  Cors__AllowedOrigins__0: 'http://localhost:3000',
  Notifications__ApiBaseUrl: apiBase,
  Notifications__Channels__Email__Sender: 'Development',
  Notifications__Channels__Sms__Sender: 'Development',
  Captcha__TestAnswer: process.env.SEED_CAPTCHA_ANSWER ?? 'TEST1',
  FileStorage__UrlSigningKey: 'local-development-only-signing-key-not-a-secret',
  DevUser__Enabled: 'false',
  RateLimiting__PasswordReset__PermitLimit: '200',
  RateLimiting__SignIn__PermitLimit: '200',
  RateLimiting__Registration__PermitLimit: '50',
  RateLimiting__CaptchaIssue__PermitLimit: '200',
  RateLimiting__Checkout__PermitLimit: '200',
  RateLimiting__Cart__PermitLimit: '1000',
};

const logFd = openSync(apiLog, 'w');
log(`starting the seeding API on ${apiBase} (log: ${apiLog})`);
const api = spawn(exe, [], {
  cwd: apiDir,
  env: apiEnv,
  stdio: ['ignore', logFd, logFd],
  shell: false,
  windowsHide: true,
});
let apiExited = false;
api.on('exit', () => {
  apiExited = true;
});

function stopApi() {
  if (!apiExited) {
    try {
      api.kill();
    } catch {
      /* already gone */
    }
  }
}
process.on('SIGINT', () => {
  stopApi();
  process.exit(130);
});

let exitCode = 0;
try {
  const deadline = Date.now() + 120_000;
  for (;;) {
    if (apiExited) throw new Error(`the API exited during start-up; see ${apiLog}`);
    if (await isListening(`${apiBase}/api/branches`)) break;
    if (Date.now() > deadline)
      throw new Error(`the API did not answer on ${apiBase} within 120 s; see ${apiLog}`);
    await new Promise((r) => setTimeout(r, 1000));
  }

  const seed = (extra) =>
    spawnSync(
      process.execPath,
      [join(here, 'seed-dev.mjs'), '--api', apiBase, '--api-log', apiLog, ...extra],
      { stdio: 'inherit', env: process.env, shell: false },
    );

  const seeded = seed([]);
  if (seeded.status !== 0) throw new Error('seed-dev.mjs failed (see above).');
  if (!args['no-verify']) {
    const verified = seed(['--verify']);
    if (verified.status !== 0) throw new Error('verification failed (see above).');
  }
  log(`done: ${db} is seeded.`);
} catch (err) {
  console.error(`seed-dev-db: ${err.message}`);
  exitCode = 1;
} finally {
  stopApi();
  await new Promise((r) => setTimeout(r, 1500));
  closeSync(logFd);
  if (args['keep-log'] || exitCode !== 0) log(`API log kept at ${apiLog}`);
  else rmSync(work, { recursive: true, force: true });
}
process.exit(exitCode);
