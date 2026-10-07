#!/usr/bin/env node
// ARCHITECTURE gate fourth pass, LOW — `"test:e2e:real-api": "E2E_REAL_API=1 playwright test
// --project=real-api"` is POSIX-only `VAR=value command` syntax. On this repo's own Windows dev
// hosts, `npm` runs scripts through `cmd.exe` (`npm config get script-shell` returns `null`, the
// default), which has no such syntax at all — the whole command fails outright there, not just the
// env var silently missing. This is a tiny, dependency-free (`node:child_process`/`node:module`/
// `node:path` only — no `cross-env`, no new package) cross-platform replacement: set the env var in
// THIS process (Node's own `process.env`, which every OS understands identically) before spawning
// Playwright's own CLI directly as a child, inheriting stdio so output/exit code both behave exactly
// as the direct command would.
//
// `playwright.config.ts`'s own `E2E_REAL_API` check is what actually adds the `real-api` project to
// its `projects` array at all — this script exists purely to set that variable portably, not to
// duplicate any of that config's own logic.
//
// ARCHITECTURE gate fourth pass (CODE_REVIEW note) / fifth pass, MEDIUM — the original version of
// this script spawned `npx` with `shell: true`, which passes the forwarded CLI args (`extraArgs`,
// below) through a shell UNQUOTED: a value containing a shell metacharacter (a space, `&`, `|`,
// `;`, ...) would be re-split or re-interpreted by the shell rather than reaching `playwright` as
// the single argument `spawnSync`'s own array otherwise guarantees. `shell: false` avoids that
// entirely — no shell ever sees these strings, each array element reaches the child process as
// exactly one argument, unconditionally. Spawning `npx` itself without a shell turned out not to be
// reliably possible cross-platform, though: on Windows `npx` resolves to a `.cmd` shim (a batch
// script, not a native executable) that Windows' own process-creation API cannot launch directly at
// all without a shell to interpret it (confirmed empirically — `spawnSync('npx.cmd', ..., { shell:
// false })` fails outright with `EINVAL`, on every Windows install checked, regardless of which
// `npx.cmd` a given machine's `PATH` happens to resolve first). `npx` is only ever a thin wrapper
// around the target package's own declared `bin` entry point anyway, so this resolves and spawns
// THAT directly instead, via `createRequire` (works from an ESM file, unlike a bare `require`) and
// the current process's own `execPath` (guaranteed to be a real, directly-launchable executable on
// every platform, with no shim, shell, or `PATH` lookup involved at all) — `node <resolved
// cli.js path> test --project=real-api ...` is exactly what `npx playwright test
// --project=real-api ...` would have run under the hood.
import { spawnSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { dirname, join } from 'node:path';

const require = createRequire(import.meta.url);
const playwrightPackageJsonPath = require.resolve('@playwright/test/package.json');
const playwrightPackageJson = require('@playwright/test/package.json');
const playwrightCliPath = join(
  dirname(playwrightPackageJsonPath),
  playwrightPackageJson.bin.playwright,
);

// Forwards any extra CLI args (e.g. `--list`, `-g <pattern>`) exactly the way the direct
// `E2E_REAL_API=1 playwright test --project=real-api` command this replaces already did —
// `npm run test:e2e:real-api -- --list` reaches this as `process.argv.slice(2)`.
const extraArgs = process.argv.slice(2);

const result = spawnSync(
  process.execPath,
  [playwrightCliPath, 'test', '--project=real-api', ...extraArgs],
  {
    stdio: 'inherit',
    shell: false,
    env: { ...process.env, E2E_REAL_API: '1' },
  },
);

if (result.error) {
  console.error(result.error);
  process.exit(1);
}
process.exit(result.status ?? 1);
