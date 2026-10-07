/**
 * `lib/format/money.ts` — HO-ADM-002 §8.4. Amounts are strings end to end on the wire (HO-ADM-001
 * §12.4, `Money`); this is the ONLY place an amount is passed through `Number`, and only for
 * *display* — never for arithmetic, and never to round-trip back into a request body.
 */
export interface Money {
  amount: string;
  currencyCode: string;
}

export function formatMoney({ amount, currencyCode }: Money): string {
  const numeric = Number(amount);
  if (!Number.isFinite(numeric)) return `${amount} ${currencyCode}`;
  try {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: currencyCode,
      currencyDisplay: 'narrowSymbol',
    }).format(numeric);
  } catch {
    // An unrecognised ISO code (shouldn't happen — the server validates it) falls back to plain text
    // rather than throwing out of a render.
    return `${amount} ${currencyCode}`;
  }
}
