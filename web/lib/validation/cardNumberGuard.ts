/**
 * `lib/validation/cardNumberGuard.ts` — HO-BIL-002 §11.12, ADR-FE-084 (C-5). The same rule as
 * HO-BIL-001's `CardNumberGuard.LooksLikeCardNumber` (§8.8.2 and its 2026-10-07 delta), so a value the
 * server would refuse never leaves the browser:
 *
 * - every Unicode decimal digit counts as its ASCII digit; whitespace, dashes, format characters and
 *   `. / _ , : ; | * ( ) [ ]` are separators; any other character (a letter included) ends a run;
 * - within one run of digit groups, a stretch of whole groups (or a group less its first digit) of
 *   13–19 digits that passes Luhn is a card number;
 * - two everyday numbers are left alone: a Bangladeshi mobile number with its country code, and one
 *   of our own document numbers (`INV-DHK001-2627-000123`).
 *
 * Pure. The value is never logged, never put in a URL and never sent when this returns `true`.
 */
export const CARD_NUMBER_MESSAGE =
  'This looks like a card number. Card numbers must never be entered here — for a card payment, enter only the terminal slip number.';

const MIN_DIGITS = 13;
const MAX_DIGITS = 19;
const MOBILE_WITH_COUNTRY_CODE = /^8801[3-9][0-9]{8}$/;
const OWN_DOCUMENT = /\b(?:INV|CN)-[A-Za-z]{1,8}[0-9]{0,4}-([0-9]{2})([0-9]{2})-[0-9]{6}\b/g;
const SEPARATOR = /[\s.\/_,:;|*()[\]\u2212\p{Pd}\p{Cf}\p{Zs}]/u;
const DECIMAL_DIGIT = /\p{Nd}/u;

/** Value of a Unicode decimal digit: digits come in contiguous blocks of ten starting at zero. */
function digitValue(codePoint: number): number {
  let base = codePoint;
  while (base > 0 && DECIMAL_DIGIT.test(String.fromCodePoint(base - 1))) base -= 1;
  // Walk back to the start of the contiguous run of digit code points; each block in it is exactly
  // ten long (the mathematical digits are five blocks back to back), so the offset modulo ten is
  // the digit.
  return (codePoint - base) % 10;
}

/** One entry per code point: an ASCII digit, a space (separator) or the character itself. */
function normalise(text: string): string[] {
  const output: string[] = [];
  for (const char of text) {
    if (DECIMAL_DIGIT.test(char)) {
      output.push(String(digitValue(char.codePointAt(0) ?? 0)));
    } else if (SEPARATOR.test(char)) {
      output.push(' ');
    } else {
      output.push(char);
    }
  }
  // Our own document numbers are blanked to a letter so they can never join a run.
  const source = Array.from(text);
  OWN_DOCUMENT.lastIndex = 0;
  const flat = source.join('');
  for (const match of flat.matchAll(OWN_DOCUMENT)) {
    const first = Number(match[1]);
    const second = Number(match[2]);
    if (second !== (first + 1) % 100) continue; // consecutive two-digit years only
    const start = Array.from(flat.slice(0, match.index)).length;
    const length = Array.from(match[0]).length;
    for (let i = start; i < start + length; i += 1) output[i] = 'x';
  }
  return output;
}

function passesLuhn(digits: string): boolean {
  let sum = 0;
  let alternate = false;
  for (let i = digits.length - 1; i >= 0; i -= 1) {
    let n = digits.charCodeAt(i) - 48;
    if (alternate) {
      n *= 2;
      if (n > 9) n -= 9;
    }
    sum += n;
    alternate = !alternate;
  }
  return sum % 10 === 0;
}

function isCardLike(digits: string): boolean {
  return (
    digits.length >= MIN_DIGITS &&
    digits.length <= MAX_DIGITS &&
    passesLuhn(digits) &&
    !MOBILE_WITH_COUNTRY_CODE.test(digits)
  );
}

/** Splits the normalised characters into maximal runs of digits separated by spaces. */
function digitRuns(chars: string[]): string[][] {
  const runs: string[][] = [];
  let current: string[] = [];
  for (const char of chars) {
    if (char === ' ' || (char >= '0' && char <= '9' && char.length === 1)) {
      current.push(char);
    } else {
      if (current.length > 0) runs.push(current);
      current = [];
    }
  }
  if (current.length > 0) runs.push(current);
  return runs;
}

function hasCardWindow(run: string[]): boolean {
  const groups: string[] = [];
  let group = '';
  for (const char of run) {
    if (char === ' ') {
      if (group) groups.push(group);
      group = '';
    } else {
      group += char;
    }
  }
  if (group) groups.push(group);

  for (let g = 0; g < groups.length; g += 1) {
    // One stray digit glued to the front is looked for only at the very start of the run.
    const trims = g === 0 ? [0, 1] : [0];
    for (const trim of trims) {
      if (trim >= groups[g]!.length) continue;
      for (let last = groups.length - 1; last >= g; last -= 1) {
        const parts = groups.slice(g, last + 1);
        parts[0] = parts[0]!.slice(trim);
        if (isCardLike(parts.join(''))) return true;
      }
    }
  }
  return false;
}

export function looksLikeCardNumber(text: string | null | undefined): boolean {
  if (!text || text.trim().length === 0) return false;
  return digitRuns(normalise(text)).some(hasCardWindow);
}

/** zod/react-hook-form `validate` helper: `undefined` when fine, the message when not. */
export function cardNumberError(text: string | null | undefined): string | undefined {
  return looksLikeCardNumber(text) ? CARD_NUMBER_MESSAGE : undefined;
}
