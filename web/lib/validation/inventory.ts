import { z } from 'zod';

/**
 * `lib/validation/inventory.ts` — HO-IMP-002 §13, ADR-FE-041. Quantity, unit-cost, money, lot and
 * HS-code schemas. Every numeric field is validated as a DECIMAL STRING and never coerced: a
 * value that passes is sent exactly as typed.
 */

export const QUANTITY_PATTERN = /^\d+(\.\d{1,3})?$/;
export const WHOLE_QUANTITY_PATTERN = /^\d+$/;
export const UNIT_COST_PATTERN = /^\d+(\.\d{1,4})?$/;
export const MONEY_PATTERN = /^\d+(\.\d{1,2})?$/;
export const RATE_PATTERN = /^\d+(\.\d{1,6})?$/;
export const SIGNED_QUANTITY_PATTERN = /^-?\d+(\.\d{1,3})?$/;
export const TEMPERATURE_PATTERN = /^-?\d+(\.\d{1,2})?$/;
/** An HS code is four digits followed by up to three groups of two (HO-IMP-001 `IsValidHsCode`). */
export const HS_CODE_PATTERN = /^\d{4}(\.?\d{2}){0,3}$/;
export const LOT_MAX_LENGTH = 60;

/** True when the string is zero (`"0"`, `"0.00"`) — string inspection, not arithmetic. */
export function isZeroDecimal(value: string): boolean {
  return /^-?0*(\.0*)?$/.test(value.trim()) || value.trim() === '';
}

/** The unit-aware message for a quantity a SKU cannot hold (mirrors `FractionalQuantityNotAllowed`
 *  for immediacy; the server is the authority). */
export function quantityProblem(
  value: string,
  options: { stockUnit: string; allowsFractional: boolean },
): string | null {
  const trimmed = value.trim();
  if (trimmed === '') return null;
  if (!options.allowsFractional && trimmed.includes('.')) {
    return `This item is counted in whole ${options.stockUnit} — no decimals.`;
  }
  if (!QUANTITY_PATTERN.test(trimmed)) {
    return `Enter ${options.stockUnit} as a number with at most 3 decimal places.`;
  }
  return null;
}

export const quantityString = (label = 'Quantity') =>
  z
    .string()
    .trim()
    .min(1, `Enter the ${label.toLowerCase()}.`)
    .regex(QUANTITY_PATTERN, `${label} must be a number with at most 3 decimal places.`);

/** Optional quantity (blank allowed) — e.g. the rejected quantity on a receipt line. */
export const optionalQuantityString = (label = 'Quantity') =>
  z
    .string()
    .trim()
    .refine((v) => v === '' || QUANTITY_PATTERN.test(v), {
      message: `${label} must be a number with at most 3 decimal places.`,
    });

export const unitCostString = (label = 'Unit cost') =>
  z
    .string()
    .trim()
    .min(1, `Enter the ${label.toLowerCase()}.`)
    .regex(UNIT_COST_PATTERN, `${label} must be a number with at most 4 decimal places.`);

export const moneyString = (label = 'Amount') =>
  z
    .string()
    .trim()
    .min(1, `Enter the ${label.toLowerCase()}.`)
    .regex(MONEY_PATTERN, `${label} must be a number with at most 2 decimal places.`);

export const rateString = z
  .string()
  .trim()
  .min(1, 'Enter the rate.')
  .regex(RATE_PATTERN, 'The rate must be a number with at most 6 decimal places.');

export const lotNumberString = z
  .string()
  .trim()
  .max(LOT_MAX_LENGTH, `A lot number is at most ${LOT_MAX_LENGTH} characters.`);

export const hsCodeString = z
  .string()
  .trim()
  .regex(
    HS_CODE_PATTERN,
    'An HS code is four digits followed by up to three groups of two (for example 2309.10).',
  );
