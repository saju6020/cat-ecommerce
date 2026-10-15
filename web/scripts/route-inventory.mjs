#!/usr/bin/env node
// HO-XCT-002 section 11.6 / ADR-FE-147 -- the route inventory. Walks web/app/**/page.tsx and prints
// every route as JSON, with its route group, the audience that must be signed in to see it, and a
// sample URL (every dynamic segment is replaced by a sample value). The 360 px sweep, the axe sweep
// and the budget script all read this one list, so a new page is covered the day it is added and a
// route cannot be excluded by being forgotten.
//
//   node scripts/route-inventory.mjs            -> JSON array on stdout
//   node scripts/route-inventory.mjs --table    -> a readable table
//
// Read-only and dependency-free (node:fs, node:path only).
import { readdirSync, statSync } from 'node:fs';
import { dirname, join, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const appDir = join(here, '..', 'app');

const SAMPLE_UUID = '00000000-0000-0000-0000-000000000001';
// Known sample values, from the MSW fixtures. Anything else gets SAMPLE_UUID: the page then shows
// its own not-found or empty state, which is still a page the sweeps must cover.
const SAMPLE_PARAMS = {
  slug: 'royal-canin-indoor-adult',
  token: 'sample-token',
  page: 'terms',
  key: 'terms',
};

function walk(dir, out = []) {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) walk(full, out);
    else if (name === 'page.tsx') out.push(full);
  }
  return out;
}

function audienceOf(group) {
  if (group === 'staff') return 'staff';
  if (group === 'portal') return 'customer';
  if (group === 'offline') return 'none';
  return 'public';
}

export function buildInventory() {
  const entries = walk(appDir).map((file) => {
    const segments = relative(appDir, dirname(file)).split(sep).filter(Boolean);
    const groups = segments.filter((s) => s.startsWith('(') && s.endsWith(')')).map((s) => s.slice(1, -1));
    const urlSegments = segments.filter((s) => !(s.startsWith('(') && s.endsWith(')')));
    const params = urlSegments.filter((s) => s.startsWith('[')).map((s) => s.replace(/[[\].]/g, ''));
    const route = '/' + urlSegments.join('/');
    const sample =
      '/' +
      urlSegments
        .map((s) => {
          if (!s.startsWith('[')) return s;
          const name = s.replace(/[[\].]/g, '');
          return SAMPLE_PARAMS[name] ?? SAMPLE_UUID;
        })
        .join('/');
    const group = groups[0] ?? 'root';
    // `(auth)/staff/sign-in` is a public page even though its URL starts with /staff.
    const audience = audienceOf(group);
    return {
      route: route === '/' ? '/' : route.replace(/\/$/, ''),
      sampleUrl: sample === '/' ? '/' : sample.replace(/\/$/, ''),
      group,
      audience,
      params,
      file: relative(join(here, '..'), file).split(sep).join('/'),
    };
  });
  return entries.sort((a, b) => a.route.localeCompare(b.route));
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const inventory = buildInventory();
  if (process.argv.includes('--table')) {
    for (const e of inventory) console.log(`${e.group.padEnd(8)} ${e.audience.padEnd(8)} ${e.route}`);
    console.log(`\n${inventory.length} routes`);
  } else {
    console.log(JSON.stringify(inventory, null, 2));
  }
}
