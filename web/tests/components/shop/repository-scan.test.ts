import { readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';

/**
 * U-X3 — HO-SHP-002 §15: no `dangerouslySetInnerHTML`; no `fetch(` outside `client.ts`; no
 * `useQuery`/`useMutation` import outside `lib/hooks`. Scans the files this handover owns.
 */
const ROOT = path.resolve(import.meta.dirname, '../../..');
const OWNED = [
  'app/(shop)',
  'app/(portal)/portal/wishlist',
  'app/(portal)/portal/reviews',
  'app/(portal)/portal/cats/[catId]/food',
  'app/(staff)/staff/shop',
  'components/shop',
  'components/catalogue',
  'components/product',
  'components/prescription',
  'components/reviews',
  'components/wishlist',
  'components/recommendations',
  'components/staff-shop',
  'lib/shop',
  'lib/api/shop-catalogue.ts',
  'lib/api/shop-customer.ts',
  'lib/api/staff-shop.ts',
  'lib/format/shop.ts',
  'mocks/handlers/shop',
];

function files(target: string): string[] {
  const full = path.join(ROOT, target);
  let stat;
  try {
    stat = statSync(full);
  } catch {
    return [];
  }
  if (stat.isFile()) return [full];
  return readdirSync(full).flatMap((entry) => files(path.join(target, entry)));
}

const stripComments = (text: string) => text.replace(/\/\*[\s\S]*?\*\/|\/\/.*$/gm, '');

const sources = OWNED.flatMap(files)
  .filter((file) => /\.(ts|tsx)$/.test(file))
  .map((file) => ({ file, text: stripComments(readFileSync(file, 'utf8')) }));

describe('U-X3 repository scan — HO-SHP-002', () => {
  it('scans a non-trivial set of files', () => {
    expect(sources.length).toBeGreaterThan(5);
  });

  it('never uses dangerouslySetInnerHTML', () => {
    const offenders = sources.filter((s) => /dangerouslySetInnerHTML/.test(s.text));
    expect(offenders.map((s) => s.file)).toEqual([]);
  });

  it('never calls fetch( (only lib/api/client.ts does)', () => {
    const offenders = sources.filter((s) => /(?<![A-Za-z_.])fetch\(/.test(s.text));
    expect(offenders.map((s) => s.file)).toEqual([]);
  });

  it('declares useQuery/useMutation only under lib/hooks', () => {
    const offenders = sources.filter((s) =>
      /import\s*\{[^}]*\b(useQuery|useMutation)\b[^}]*\}\s*from\s*'@tanstack\/react-query'/.test(
        s.text,
      ),
    );
    expect(offenders.map((s) => s.file)).toEqual([]);
  });
});
