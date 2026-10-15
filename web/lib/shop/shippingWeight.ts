import { ApiValidationError } from '@/lib/api/client';
import { SHOP_CODE_MESSAGES } from '@/lib/api/errors';

/** HO-SHP-001-FU3 — the SKU shipping weight rules (`ProductRules.Min/MaxShippingWeightGrams`). */
export const MIN_SHIPPING_WEIGHT_GRAMS = 1;
export const MAX_SHIPPING_WEIGHT_GRAMS = 100000;
export const SHIPPING_WEIGHT_MESSAGE = `Enter a whole number of grams from ${MIN_SHIPPING_WEIGHT_GRAMS} to ${MAX_SHIPPING_WEIGHT_GRAMS}.`;

/** Empty = not set (`null`); otherwise a whole number 1..100000 or an error message. */
export function parseShippingWeight(
  text: string,
): { value: number | null; error?: undefined } | { value?: undefined; error: string } {
  const trimmed = text.trim();
  if (!trimmed) return { value: null };
  if (!/^\d+$/.test(trimmed)) return { error: SHIPPING_WEIGHT_MESSAGE };
  const value = Number(trimmed);
  if (value < MIN_SHIPPING_WEIGHT_GRAMS || value > MAX_SHIPPING_WEIGHT_GRAMS)
    return { error: SHIPPING_WEIGHT_MESSAGE };
  return { value };
}

/** The server's `InvalidShippingWeight` (or a violation on `ShippingWeightGrams`) as a field error;
 *  `null` when the error is something else. */
export function shippingWeightServerError(error: unknown): string | null {
  if (!(error instanceof ApiValidationError)) return null;
  const hit = error.response.errors.find(
    (v) => v.errorCode === 'InvalidShippingWeight' || v.propertyName === 'ShippingWeightGrams',
  );
  if (!hit) return null;
  return hit.errorMessage || SHOP_CODE_MESSAGES.InvalidShippingWeight || SHIPPING_WEIGHT_MESSAGE;
}
