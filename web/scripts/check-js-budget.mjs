#!/usr/bin/env node
// HO-XCT-002 section 11.6 / ADR-FE-147 -- the first-load JavaScript budget. Reads the Next build
// output (.next/app-build-manifest.json and .next/build-manifest.json), gzips every JavaScript file
// a budget route loads on first visit (the shared runtime, plus the route's own chunks and those of
// each layout above it), and fails with a per-route table when a route exceeds `firstLoadJsKb` in
// web/perf-budgets.json. A route named in the budget file that no longer exists is also a failure,
// so the budget cannot quietly rot. Run `next build` first.
//
//   npm run build && npm run perf:js
//
// No dependency: node:fs, node:path, node:zlib. 1 kB = 1000 bytes, as Next prints it.
import { readFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { gzipSync } from 'node:zlib';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const nextDir = join(root, '.next');
const budgets = JSON.parse(readFileSync(join(root, 'perf-budgets.json'), 'utf8'));

function readManifest(name) {
  const file = join(nextDir, name);
  if (!existsSync(file)) {
    console.error(`Missing ${name}. Run "npm run build" first.`);
    process.exit(2);
  }
  return JSON.parse(readFileSync(file, 'utf8'));
}

const appManifest = readManifest('app-build-manifest.json').pages;
const buildManifest = readManifest('build-manifest.json');

/** '/(shop)/shop/page' -> '/shop'; the groups and the trailing page segment are not in the URL. */
function routeOfKey(key) {
  const path = key
    .split('/')
    .filter((s) => s && !(s.startsWith('(') && s.endsWith(')')))
    .filter((s) => s !== 'page')
    .join('/');
  return '/' + path;
}

const keyByRoute = new Map();
for (const key of Object.keys(appManifest)) {
  if (key.endsWith('/page')) keyByRoute.set(routeOfKey(key), key);
}

/** The layout keys above a page key: '/(shop)/shop/page' -> ['/(shop)/shop/layout', '/(shop)/layout', '/layout']. */
function layoutKeys(pageKey) {
  const segments = pageKey.split('/').slice(1, -1);
  const keys = [];
  for (let i = segments.length; i >= 0; i--) {
    keys.push('/' + [...segments.slice(0, i), 'layout'].join('/'));
  }
  return keys.map((k) => k.replace('//', '/'));
}

const gzipKb = (file) => gzipSync(readFileSync(join(nextDir, file)), { level: 9 }).length / 1000;

const rows = [];
let failed = false;
for (const route of budgets.routes) {
  const pageKey = keyByRoute.get(route);
  if (!pageKey) {
    rows.push({ route, kb: null, status: 'MISSING (no such route in the build)' });
    failed = true;
    continue;
  }
  const files = new Set(buildManifest.rootMainFiles);
  for (const key of [pageKey, ...layoutKeys(pageKey)]) {
    for (const file of appManifest[key] ?? []) files.add(file);
  }
  const js = [...files].filter((f) => f.endsWith('.js'));
  const kb = js.reduce((sum, f) => sum + gzipKb(f), 0);
  const over = kb > budgets.firstLoadJsKb;
  if (over) failed = true;
  rows.push({ route, kb, status: over ? 'OVER' : 'ok' });
}

console.log(`First-load JS budget: ${budgets.firstLoadJsKb} kB gzipped per route\n`);
const width = Math.max(...rows.map((r) => r.route.length), 5);
console.log(`${'Route'.padEnd(width)}  ${'gzip kB'.padStart(8)}  Status`);
for (const r of rows) {
  console.log(
    `${r.route.padEnd(width)}  ${(r.kb === null ? '-' : r.kb.toFixed(1)).padStart(8)}  ${r.status}`,
  );
}
if (failed) {
  console.error('\nBudget failed. Fix the owning route; do not raise the budget (HO-XCT-002 ADR-FE-148).');
  process.exit(1);
}
console.log('\nAll budget routes are within budget.');
