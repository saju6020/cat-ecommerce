import { z } from 'zod';
import { CARD_NUMBER_MESSAGE, looksLikeCardNumber } from '@/lib/validation/cardNumberGuard';

/**
 * `components/billing/schemas.ts` — shared zod pieces for Billing forms. `guardedText` is the C-5
 * client guard of ADR-FE-084: the same Luhn rule as the server, run on change (forms use
 * `mode: 'onChange'`) and on submit — a match is a field error, blocks the submit (so nothing is
 * sent), clears nothing else, and never repeats the value.
 */
export function guardedText(max: number) {
  return z
    .string()
    .trim()
    .max(max, `Use at most ${max} characters.`)
    .refine((value) => !looksLikeCardNumber(value), { message: CARD_NUMBER_MESSAGE });
}

export function requiredGuardedText(max: number, message: string) {
  return guardedText(max).refine((value) => value.length > 0, { message });
}

export const dateOnlyString = z
  .string()
  .regex(/^\d{4}-\d{2}-\d{2}$/, 'Enter a date.');
