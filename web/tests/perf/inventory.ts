import { execFileSync } from 'node:child_process';
import path from 'node:path';

/** One route of `scripts/route-inventory.mjs` (HO-XCT-002 section 11.6). */
export interface InventoryRoute {
  route: string;
  sampleUrl: string;
  group: string;
  audience: 'public' | 'customer' | 'staff' | 'none';
  params: string[];
  file: string;
}

/** The inventory is generated from `app/**\/page.tsx`, never listed by hand, so a new page is in
 *  the sweeps the day it is added and no route can be excluded by omission. */
export function loadInventory(): InventoryRoute[] {
  const script = path.resolve(__dirname, '../../scripts/route-inventory.mjs');
  const json = execFileSync(process.execPath, [script], { encoding: 'utf8' });
  const all = JSON.parse(json) as InventoryRoute[];
  const audience = process.env.SWEEP_AUDIENCE;
  const only = process.env.SWEEP_ONLY; // a substring of the route, to re-run one page
  return all.filter(
    (r) => (!audience || r.audience === audience) && (!only || r.route.includes(only)),
  );
}

export const VIEWPORT_WIDTHS = [360, 768, 1024, 1440] as const;
