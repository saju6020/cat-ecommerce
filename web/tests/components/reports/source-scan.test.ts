import { describe, expect, it } from 'vitest';
import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';

/**
 * U-X3 — a real source scan of components/{dashboard,reports,report-schedules}: the page computes
 * nothing (ADR-FE-131, ADR-FE-136) and renders no HTML from report data.
 */
const ROOT = path.resolve(import.meta.dirname, '../../..');

function sources(dir: string): Array<{ file: string; text: string }> {
  const full = path.join(ROOT, dir);
  return readdirSync(full)
    .filter((name) => /\.tsx?$/.test(name))
    .map((name) => ({ file: `${dir}/${name}`, text: readFileSync(path.join(full, name), 'utf8') }));
}

const files = [
  ...sources('components/dashboard'),
  ...sources('components/reports'),
  ...sources('components/report-schedules'),
];

/** Comments are prose, not code. */
function code(text: string): string {
  return text.replace(/\/\*[\s\S]*?\*\//g, '').replace(/^\s*\/\/.*$/gm, '');
}

describe('U-X3 — no figure is computed in the browser', () => {
  it('scans a real set of files', () => {
    expect(files.length).toBeGreaterThan(15);
  });

  it.each(files)('$file: no Number()/parseFloat/parseInt over report data', ({ text }) => {
    expect(code(text)).not.toMatch(/\bNumber\s*\(|parseFloat\s*\(|parseInt\s*\(/);
  });

  it.each(files)('$file: no arithmetic on amounts, totals or percentages', ({ text }) => {
    expect(code(text)).not.toMatch(/\.(amount|percent|total)\w*\s*[-+*/]\s*[\w(]/);
    expect(code(text)).not.toMatch(/\.reduce\s*\(/);
  });

  it.each(files)('$file: no recurrence, date maths or raw HTML', ({ text }) => {
    const stripped = code(text);
    expect(stripped).not.toMatch(/new Date\s*\(|setDate|setMonth|date-fns|getTimezoneOffset/);
    expect(stripped).not.toMatch(/dangerouslySetInnerHTML/);
  });
});

describe('ADR-FE-129 — no per-report component', () => {
  it('names no report key in components/reports', () => {
    for (const { file, text } of sources('components/reports')) {
      expect(code(text), file).not.toMatch(/['"](clinic-|stock-|revenue-|payment-|new-vs)/);
    }
  });
});
