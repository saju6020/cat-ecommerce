import type { Money } from '@/lib/format/money';

/**
 * `lib/money/a11yMoney.ts` — HO-BIL-002 §7 UI-6, U-X4. The accessible spelling of an amount:
 * `"BDT 1,500.00"` — the currency code is always in the text a screen reader gets. Built by
 * grouping the digits of the server's string; the amount is never parsed into a number.
 */
export function groupThousands(amount: string): string {
  const negative = amount.startsWith('-');
  const unsigned = negative ? amount.slice(1) : amount;
  const [whole = '', fraction] = unsigned.split('.');
  const grouped = whole.replace(/\B(?=(\d{3})+(?!\d))/g, ',');
  return `${negative ? '-' : ''}${grouped}${fraction !== undefined ? `.${fraction}` : ''}`;
}

export function a11yMoney({ amount, currencyCode }: Money): string {
  return `${currencyCode} ${groupThousands(amount)}`;
}

/** `"-10.00"` → `"negative"`, `"0.00"` → `"zero"`, otherwise `"positive"`. Reads the sign and the
 *  digits of the server's string; used only to word a server-computed variance (*Short / Over /
 *  Balanced*), never to decide anything about the money. */
export function moneySign(amount: string): 'negative' | 'zero' | 'positive' {
  if (/^-?0*(\.0*)?$/.test(amount)) return 'zero';
  return amount.startsWith('-') ? 'negative' : 'positive';
}

/** The amount without its sign — to say *"Short BDT 10.00"* from a variance of `"-10.00"`. */
export function withoutSign(money: Money): Money {
  return { ...money, amount: money.amount.replace(/^-/, '') };
}
