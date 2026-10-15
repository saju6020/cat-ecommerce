import fs from 'node:fs';
import { describe, expect, it } from 'vitest';
import { allUiSourceFiles, orderingSourceFiles, relativeToRoot } from './ordering-scope';

/**
 * U-X3 / C-5 / ADR-FE-092 — **no card UI**. HO-ORD-002 §12.3 rule 1: no `autocomplete="cc-*"` anywhere
 * in the UI, and no card-input identifier in the Ordering files. There is no payment gateway in v1
 * (COD, cash, in-store card terminal, bank transfer): "Card in store" records a terminal SLIP
 * REFERENCE only. The existing server-aligned guard (`lib/validation/cardNumberGuard.ts`) and its
 * message are the way a card number is *refused*, so those two names are not offences.
 */
const AUTOCOMPLETE_CC = /autocomplete\s*[=:]\s*[{"'`\s]*cc-/i;
const CARD_IDENTIFIER = /card\s?number|card-?num|cvv|cvc|expiry\s?date|\bpan\b/i;
const ALLOWED_TOKENS = [
  'looksLikeCardNumber',
  'CARD_NUMBER_MESSAGE',
  'cardNumberGuard',
  'card number',
  'card numbers',
];

function withoutAllowedTokens(source: string): string {
  let text = source;
  for (const token of ALLOWED_TOKENS) text = text.split(token).join(' ');
  return text;
}

describe('C-5 — no card UI (U-X3)', () => {
  it('no autocomplete="cc-*" anywhere in app/ or components/', () => {
    const offenders = allUiSourceFiles().filter((file) =>
      AUTOCOMPLETE_CC.test(fs.readFileSync(file, 'utf8')),
    );
    expect(offenders.map(relativeToRoot)).toEqual([]);
  });

  it('no card-input identifier in the Ordering files', () => {
    const offenders: string[] = [];
    for (const file of orderingSourceFiles()) {
      const lines = withoutAllowedTokens(fs.readFileSync(file, 'utf8')).split('\n');
      lines.forEach((line, index) => {
        // Comments may say "never a card number" — only code is an offence.
        const code = line.replace(/\/\/.*$/, '').replace(/\/\*.*?\*\//g, '');
        if (/^\s*\*/.test(line)) return;
        if (CARD_IDENTIFIER.test(code)) offenders.push(`${relativeToRoot(file)}:${index + 1}`);
      });
    }
    expect(offenders).toEqual([]);
  });

  it('the scan sees the Ordering files (a guard that scans nothing proves nothing)', () => {
    expect(orderingSourceFiles().map(relativeToRoot).includes('lib/api/checkout.ts')).toBe(true);
  });
});
