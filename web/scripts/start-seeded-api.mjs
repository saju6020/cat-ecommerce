#!/usr/bin/env node
// Starts a private copy of the API on an already-seeded scratch database and leaves it running.
// Used by run-journey-e2e.mjs; also handy by hand:
//
//   PGPASSWORD=... node web/scripts/start-seeded-api.mjs --db catecommerce_journey --port 5098 [--web-port 3098] [--skip-build]
//
// It applies the same safety refusals as seed-dev-db.mjs (never catecommerce_dev / catecommerce_test, never
// ports 5005 / 3000) and, like it, deletes appsettings.Development.json from the published copy so the process
// can only reach the database named here. The database must already exist and be seeded
// (`node web/scripts/seed-dev-db.mjs --db <name> --port <n>`).
//
// Rate limits are raised the same way the seeder raises them, because a journey signs in and registers many times
// from one IP; the CAPTCHA test answer is TEST1 (Captcha:TestAnswer, override with SEED_CAPTCHA_ANSWER).

import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, openSync, closeSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repo = resolve(here, '..', '..');
const apiProject = join(repo, 'src', 'Api', 'CatEcommerce.Api');

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

/** Throws (never exits) so callers can clean up. */
export function assertSafeTarget({ db, port }) {
  if (!db) throw new Error('pass --db <name> (a scratch database such as catecommerce_journey).');
  if (!/^[a-z][a-z0-9_]{0,62}$/.test(db))
    throw new Error(`"${db}" is not a plain lower-case database name.`);
  if (db === 'catecommerce_test')
    throw new Error('refusing catecommerce_test: it belongs to the integration tests.');
  if (db === 'catecommerce_dev')
    throw new Error(
      'refusing catecommerce_dev: it is the developer database; a journey registers customers and places orders.',
    );
  if (!Number.isInteger(port) || port < 1024 || port > 65535)
    throw new Error('--port must be a number between 1024 and 65535.');
  if (port === 5005 || port === 3000)
    throw new Error(`refusing port ${port}: that is the developer's own API / web app.`);
  if (!process.env.PGPASSWORD)
    throw new Error('set PGPASSWORD (the PostgreSQL password; it is never printed).');
}

async function isListening(url) {
  try {
    await fetch(url, { signal: AbortSignal.timeout(1500) });
    return true;
  } catch {
    return false;
  }
}

/**
 * Builds (unless skipBuild), publishes and starts the API. Resolves to { stop, apiBase, logFile } once it answers.
 */
export async function startSeededApi({ db, port, webPort = 3098, skipBuild = false }) {
  assertSafeTarget({ db, port });
  const pg = {
    host: process.env.PGHOST ?? 'localhost',
    port: process.env.PGPORT ?? '5432',
    user: process.env.PGUSER ?? 'postgres',
  };
  const connectionString = `Host=${pg.host};Port=${pg.port};Database=${db};Username=${pg.user};Password=${process.env.PGPASSWORD}`;
  const apiBase = `http://localhost:${port}`;
  if (await isListening(`${apiBase}/api/branches`))
    throw new Error(`something already answers on ${apiBase}; stop it or pass another --port.`);

  if (!skipBuild) {
    const build = spawnSync(
      'dotnet',
      ['build', apiProject, '-c', 'Release', '-nologo', '-v', 'q'],
      { cwd: repo, encoding: 'utf8', shell: false },
    );
    if (build.status !== 0)
      throw new Error(`dotnet build failed:\n${String(build.stdout ?? '').slice(-3000)}`);
  }
  const work = mkdtempSync(join(tmpdir(), 'purrcare-journey-'));
  const apiDir = join(work, 'api');
  const publish = spawnSync(
    'dotnet',
    ['publish', apiProject, '-c', 'Release', '--no-build', '-nologo', '-v', 'q', '-o', apiDir],
    { cwd: repo, encoding: 'utf8', shell: false },
  );
  if (publish.status !== 0)
    throw new Error(`dotnet publish failed:\n${String(publish.stdout ?? '').slice(-3000)}`);
  rmSync(join(apiDir, 'appsettings.Development.json'), { force: true }); // its connection string names catecommerce_dev

  const logFile = join(work, 'api.log');
  const exe = join(
    apiDir,
    process.platform === 'win32' ? 'CatEcommerce.Api.exe' : 'CatEcommerce.Api',
  );
  const env = {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: 'Development',
    ASPNETCORE_URLS: apiBase,
    ConnectionStrings__CatEcommerce: connectionString,
    Account__WebBaseUrl: `http://localhost:${webPort}`,
    PublicSite__WebBaseUrl: `http://localhost:${webPort}`,
    Cors__AllowedOrigins__0: `http://localhost:${webPort}`,
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
  const fd = openSync(logFile, 'w');
  const child = spawn(exe, [], {
    cwd: apiDir,
    env,
    stdio: ['ignore', fd, fd],
    shell: false,
    windowsHide: true,
  });
  let exited = false;
  child.on('exit', () => {
    exited = true;
  });

  const stop = async () => {
    if (!exited) {
      try {
        child.kill();
      } catch {
        /* already gone */
      }
    }
    await new Promise((r) => setTimeout(r, 1500));
    try {
      closeSync(fd);
    } catch {
      /* closed */
    }
    try {
      rmSync(work, { recursive: true, force: true });
    } catch {
      /* still locked: leave it in tmp */
    }
  };

  const deadline = Date.now() + 120_000;
  for (;;) {
    if (exited) throw new Error(`the API exited during start-up; see ${logFile}`);
    if (await isListening(`${apiBase}/api/branches`)) break;
    if (Date.now() > deadline) {
      await stop();
      throw new Error(`the API did not answer on ${apiBase} within 120 s`);
    }
    await new Promise((r) => setTimeout(r, 1000));
  }
  return { stop, apiBase, logFile };
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  const args = parseArgs(process.argv.slice(2));
  try {
    const api = await startSeededApi({
      db: typeof args.db === 'string' ? args.db : '',
      port: Number(args.port ?? 5098),
      webPort: Number(args['web-port'] ?? 3098),
      skipBuild: Boolean(args['skip-build']),
    });
    console.log(
      `start-seeded-api: ${api.apiBase} is up on ${args.db} (log ${api.logFile}). Ctrl+C stops it.`,
    );
    const bye = async () => {
      await api.stop();
      process.exit(0);
    };
    process.on('SIGINT', bye);
    process.on('SIGTERM', bye);
    setInterval(() => {}, 1 << 30);
  } catch (err) {
    console.error(`start-seeded-api: ${err.message}`);
    process.exit(1);
  }
}
