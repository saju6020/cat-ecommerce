import { format } from 'date-fns';
import { formatMoney, type Money } from './money';
import { parseDateOnly } from '@/lib/dateOnly';

/**
 * `lib/format/shop.ts` — HO-SHP-002 §8.5, ADR-FE-036. Money and tax display for the storefront.
 * Amounts stay strings end to end; `parseMoneyForCompare` exists only for the min <= max check on
 * the price filter, never for display arithmetic.
 */

/** `৳1,200.00`, or `From ৳1,200.00` when the variants' prices differ. `null` = no current price. */
export function formatPriceRange(
  fromPrice: Money | null | undefined,
  priceVaries: boolean,
): string {
  if (!fromPrice) return 'Price not available';
  const formatted = formatMoney(fromPrice);
  return priceVaries ? `From ${formatted}` : formatted;
}

/** OQ-5 wording, pending SRS Q-5. */
export function taxLabel(pricesIncludeTax: boolean): string {
  return pricesIncludeTax ? 'incl. VAT' : '+ VAT';
}

/** `4.5 out of 5 (23 reviews)`; `No reviews yet` when there are none. */
export function formatRating(average: number | null | undefined, count: number): string {
  if (count <= 0 || average === null || average === undefined) return 'No reviews yet';
  const value = Number.isInteger(average) ? String(average) : average.toFixed(1);
  return `${value} out of 5 (${count} ${count === 1 ? 'review' : 'reviews'})`;
}

/** `12 Nov 2026` — a date-only value, never shifted by a time zone (HO-CAT-002 §9). */
export function formatBestBefore(dateOnly: string): string {
  if (!dateOnly) return '';
  return format(parseDateOnly(dateOnly), 'd MMM yyyy');
}

/** `3 Oct 2026` for an instant (review dates, alert dates). */
export function formatShortInstant(instant: string): string {
  const date = new Date(instant);
  if (Number.isNaN(date.getTime())) return '';
  return format(date, 'd MMM yyyy');
}

const FLAG_CODES = new Set(['de', 'th', 'tr', 'us', 'kr']);

/** The in-repo SVG for the launch origins (OQ-2); `null` shows the globe icon. The country name is
 *  always shown as text and the flag image is decorative (`alt=""`). */
export function countryFlag(code: string | null | undefined): string | null {
  const lower = (code ?? '').toLowerCase();
  return FLAG_CODES.has(lower) ? `/flags/${lower}.svg` : null;
}

/** ONLY for comparing the two price-filter inputs (min <= max); never for display or arithmetic on
 *  amounts. `NaN` for anything that is not a plain decimal. */
export function parseMoneyForCompare(amount: string): number {
  return /^\d+(\.\d+)?$/.test(amount.trim()) ? Number(amount) : Number.NaN;
}
