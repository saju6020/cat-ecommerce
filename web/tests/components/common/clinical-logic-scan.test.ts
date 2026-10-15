import { readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';

/**
 * U-X5 (HO-PHM-002 §20.2, ADR-FE-060/062) — repository scan. The server is the only clinical
 * authority: nothing in this slice's components, pages or API modules may compute a dose, match an
 * allergy or interaction, order batches (FEFO) or compare an expiry date. The scan looks for the
 * shapes such logic would take and fails the build if one appears; the few legitimate sums (the
 * dispense screen's exact "8 of 10 ml" total) live in `lib/format/pharmacy.ts`, which the scan names.
 */
const root = path.resolve(__dirname, '../../..');
const targets = [
  'components/prescriptions',
  'components/pharmacy',
  'components/controlled-drugs',
  'components/formulary',
  'components/diet-authorisations',
  'components/my-prescriptions',
  'app/(staff)/staff/pharmacy',
  'app/(staff)/staff/admin/formulary',
  'app/(staff)/staff/cats/[catId]/prescriptions',
  'app/(staff)/staff/cats/[catId]/diet-authorisations',
  'app/(portal)/portal/prescriptions',
];

function walk(dir: string): string[] {
  return readdirSync(dir).flatMap((entry) => {
    const full = path.join(dir, entry);
    return statSync(full).isDirectory() ? walk(full) : [full];
  });
}

const sources = targets.flatMap((target) =>
  walk(path.join(root, target))
    .filter((file) => /\.tsx?$/.test(file))
    .map((file) => ({
      file: path.relative(root, file).replaceAll('\\', '/'),
      text: readFileSync(file, 'utf8'),
    })),
);

/** The code with comments, `import … from '…'` statements and JSX closing tags removed — what is left is logic. */
function logic(text: string): string {
  return text
    .replace(/\/\*[\s\S]*?\*\/|\/\/.*$/gm, '')
    .replace(/^import[\s\S]*?from\s+'[^']*';$/gm, '')
    .replace(/<\/[A-Za-z.]+>/g, '');
}

function offenders(pattern: RegExp): string[] {
  return sources.filter((s) => pattern.test(logic(s.text))).map((s) => s.file);
}

describe('U-X5 — no client-side clinical logic', () => {
  it('has something to scan', () => {
    expect(sources.length).toBeGreaterThan(40);
  });

  it('never divides or multiplies by a weight or a strength (no dose or mg/kg maths)', () => {
    expect(
      offenders(
        /(?:mgPerKg|doseMg|weightKg|latestKg|\w*Strength\w*|\w*Weight\w*)\s*[*/]|[*/]\s*(?:weightKg|latestKg|\w*Strength\w*|\w*Weight\w*)/,
      ),
    ).toEqual([]);
  });

  it('never matches an allergy or an interaction', () => {
    expect(
      offenders(
        /allerg\w*\.(includes|some|find|filter|match)\(|interaction\w*\.(includes|some|find|filter)\(/i,
      ),
    ).toEqual([]);
  });

  it('never orders batches or compares an expiry date (FEFO and expiry are the server’s)', () => {
    expect(
      offenders(
        /batch\w*\.sort\(|expiresOn\s*[<>]=?|[<>]=?\s*\w*\.expiresOn|new Date\([^)]*expires/i,
      ),
    ).toEqual([]);
  });

  it('never rounds a dose, quantity or weight', () => {
    expect(
      offenders(
        /toFixed\(|toPrecision\(|Math\.(round|floor|ceil)\([^)]*(dose|quantity|weight|amount|strength|mg)/i,
      ),
    ).toEqual([]);
  });

  it('never reads a unit conversion table (the server returns every figure in its unit)', () => {
    expect(offenders(/mcg\s*[*/]\s*1000|1000\s*[*/]\s*mcg|\*\s*1000\b/i)).toEqual([]);
  });
});
