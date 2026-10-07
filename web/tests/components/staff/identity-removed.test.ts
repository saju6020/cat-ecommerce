import { describe, expect, it } from 'vitest';
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';

/**
 * U-G6 — ADR-FE-004: `lib/identity.ts` does not exist, and nothing imports it. A grep-style test,
 * not a component render, so it can assert on the whole tree rather than one module graph.
 */
const ROOT = join(import.meta.dirname, '..', '..', '..');
const SCAN_DIRS = ['app', 'components', 'lib', 'nav', 'mocks'];
const SOURCE_EXTENSIONS = ['.ts', '.tsx'];

function walk(dir: string, files: string[] = []): string[] {
  for (const entry of readdirSync(dir)) {
    if (entry === 'node_modules' || entry === '.next') continue;
    const full = join(dir, entry);
    const stats = statSync(full);
    if (stats.isDirectory()) walk(full, files);
    else if (SOURCE_EXTENSIONS.includes(entry.slice(entry.lastIndexOf('.')))) files.push(full);
  }
  return files;
}

describe('lib/identity.ts — U-G6', () => {
  it('does not exist', () => {
    expect(existsSync(join(ROOT, 'lib', 'identity.ts'))).toBe(false);
  });

  it('is imported nowhere in app/, components/, lib/, nav/ or mocks/', () => {
    const offenders: string[] = [];
    for (const dir of SCAN_DIRS) {
      const dirPath = join(ROOT, dir);
      if (!existsSync(dirPath)) continue;
      for (const file of walk(dirPath)) {
        const content = readFileSync(file, 'utf8');
        if (
          /from ['"]@\/lib\/identity['"]/.test(content) ||
          /from ['"]\.\.?\/.*lib\/identity['"]/.test(content)
        ) {
          offenders.push(file);
        }
      }
    }
    expect(offenders).toEqual([]);
  });
});
