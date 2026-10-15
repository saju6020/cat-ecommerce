import { z } from 'zod';

/**
 * `lib/money/moneyString.ts` — HO-BIL-002 §8.4, ADR-FE-080. Money is a string end to end. Everything
 * here works on the characters of that string; nothing parses it into a JavaScript `number`, and
 * there is deliberately no comparison or arithmetic helper (the ESLint rule in `eslint.config.mjs`
 * forbids `Number(`, `parseFloat(`, `parseInt(` and unary `+` in this folder).
 */

/** Up to 10 integer digits and at most two decimals — the shape every Billing amount is sent in. */
export const MONEY_PATTERN = /^\d{1,10}(\.\d{1,2})?$/;
/** A quantity: up to 7 integer digits and at most three decimals. */
export const QUANTITY_PATTERN = /^\d{1,7}(\.\d{1,3})?$/;

/** `"12.5"` → `"12.50"`, `"12"` → `"12.00"`, `" 7.05 "` → `"7.05"`. Anything that is not a money
 *  string is returned trimmed and otherwise unchanged (so validation can still report it). */
export function normaliseMoney(raw: string): string {
  const text = raw.trim().replace(/^0+(?=\d)/, '');
  if (!MONEY_PATTERN.test(text)) return raw.trim();
  const [whole = '0', fraction = ''] = text.split('.');
  return `${whole}.${fraction.padEnd(2, '0')}`;
}

export const MONEY_FORMAT_MESSAGE = 'Enter an amount such as 1500 or 1500.50.';

/** zod: a required money string. Normalise with `normaliseMoney` when building the request. */
export const moneyString = z
  .string()
  .trim()
  .min(1, 'Enter an amount.')
  .regex(MONEY_PATTERN, MONEY_FORMAT_MESSAGE);

/** zod: an optional money string — empty is allowed. */
export const optionalMoneyString = z
  .string()
  .trim()
  .refine((value) => value === '' || MONEY_PATTERN.test(value), MONEY_FORMAT_MESSAGE);

export const quantityString = z
  .string()
  .trim()
  .min(1, 'Enter a quantity.')
  .regex(QUANTITY_PATTERN, 'Enter a quantity such as 2 or 1.5.');
