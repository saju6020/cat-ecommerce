import { MONEY_PATTERN, QUANTITY_PATTERN } from './moneyString';

/**
 * `lib/money/wire.ts` — HO-BIL-002 §8.4, ADR-FE-080. HO-BIL-001's request DTOs declare amounts as
 * .NET `decimal`, which System.Text.Json reads only from a JSON **number** (never from a string).
 * Responses carry money as strings; requests must carry a number literal. `JSON.rawJSON` writes the
 * digits of the string we already hold straight into the body, so the amount still never passes
 * through a JavaScript `number` (no float rounding, no `Number(`).
 */
type RawJson = { rawJSON: string };
const rawJSON = (JSON as unknown as { rawJSON?: (text: string) => RawJson }).rawJSON;

function emit(text: string): unknown {
  // Without `JSON.rawJSON` (an old browser) the string is sent as it is; the server then refuses it
  // and the form shows the server's message rather than writing a wrong amount.
  return rawJSON ? rawJSON(text) : text;
}

/** An amount string as a JSON number literal. A value that is not a money string is sent as the
 *  string it is, so the server's own validation reports it against the field. */
export function wireMoney(amount: string): unknown {
  return MONEY_PATTERN.test(amount) ? emit(amount) : amount;
}

export function wireQuantity(quantity: string): unknown {
  return QUANTITY_PATTERN.test(quantity) ? emit(quantity) : quantity;
}

/** A whole number such as loyalty points, from a digit string. */
export function wireInteger(digits: string): unknown {
  return /^\d{1,9}$/.test(digits) ? emit(digits) : digits;
}

/** The one currency of the platform (SRS C-6; HO-ADM-001 ADR-054). */
export const DEFAULT_CURRENCY = 'BDT';

/**
 * A few HO-BIL-001 responses carry a bare .NET `decimal` where §12.4 promises a money object — verified
 * against the real controllers: the credit-note result's `releasedToCustomerCredit`, the apply-credit
 * result's `applied[].amount`, `freeCreditRemaining` and `invoiceOutstanding`, and the remittance
 * result's `unapplied` (reported to the integrator as a §20.4 disagreement). This reads either shape
 * into a money object by writing the number's digits out as text (`1150.5` → `"1150.50"`) — no arithmetic.
 */
export function moneyFromWire(value: unknown, currencyCode: string = DEFAULT_CURRENCY): { amount: string; currencyCode: string } {
  if (value !== null && typeof value === 'object' && 'amount' in value) {
    const money = value as { amount: unknown; currencyCode?: unknown };
    return { amount: normaliseWire(String(money.amount)), currencyCode: String(money.currencyCode ?? currencyCode) };
  }
  return { amount: normaliseWire(String(value ?? '0')), currencyCode };
}

function normaliseWire(text: string): string {
  const [whole = '0', fraction = ''] = text.split('.');
  return `${whole}.${fraction.padEnd(2, '0').slice(0, 2)}`;
}
