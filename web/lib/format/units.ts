/**
 * `lib/format/units.ts` — HO-PHM-002 §8.4, ADR-FE-061. The ONE table of clinical unit labels and the
 * ONE way a dose, quantity, weight, strength or range becomes text. Every such value on a pharmacy
 * screen is rendered through `formatQuantity` (via `components/common/Quantity.tsx`), so a bare number
 * never reaches the screen (UI-8).
 *
 * The amount is shown exactly as the server sent it (a string — HO-PHM-001 §12.4): nothing here
 * rounds, converts or computes a dose. The unit strings are the C# `Enumeration.DisplayName`s
 * (`DoseUnit`, `DispensingUnit`) plus `Kg` for weights.
 */
export interface QuantityValue {
  /** Decimal string as the server sent it ("0.30", "4.10"). */
  amount: string;
  /** `DoseUnit` / `DispensingUnit` display name ("Ml", "Millilitre", "Tablet", "Kg"). */
  unit: string;
}

interface UnitLabel {
  /** Written after a single amount ("1 tablet"). */
  singular: string;
  /** Written after any other amount ("2 tablets"). Same as `singular` for symbols. */
  plural: string;
  /** What a screen reader should say for the symbol. */
  spoken: string;
  /** Countable things take a plural; symbols (mg, ml) never do. */
  countable: boolean;
}

function symbol(label: string, spoken: string): UnitLabel {
  return { singular: label, plural: label, spoken, countable: false };
}
function countable(singular: string, plural = `${singular}s`): UnitLabel {
  return { singular, plural, spoken: plural, countable: true };
}

export const UNIT_LABELS: Readonly<Record<string, UnitLabel>> = {
  Mg: symbol('mg', 'milligrams'),
  Mcg: symbol('mcg', 'micrograms'),
  G: symbol('g', 'grams'),
  Gram: symbol('g', 'grams'),
  Ml: symbol('ml', 'millilitres'),
  Millilitre: symbol('ml', 'millilitres'),
  Kg: symbol('kg', 'kilograms'),
  Iu: symbol('IU', 'international units'),
  Percent: symbol('%', 'percent'),
  Tablet: countable('tablet'),
  Capsule: countable('capsule'),
  Drop: countable('drop'),
  Application: countable('application'),
  Vial: countable('vial'),
  Ampoule: countable('ampoule'),
  Tube: countable('tube'),
  Bottle: countable('bottle'),
  Patch: countable('patch', 'patches'),
  Pipette: countable('pipette'),
  Dose: countable('dose'),
};

/** A unit the table does not know is shown as sent (lower-cased), never hidden. */
export function unitLabel(unit: string, amount?: string): string {
  const known = UNIT_LABELS[unit];
  if (!known) return unit.toLowerCase();
  return known.countable && amount !== undefined && !isOne(amount) ? known.plural : known.singular;
}

export function unitSpoken(unit: string): string {
  return UNIT_LABELS[unit]?.spoken ?? unit.toLowerCase();
}

/** "1", "1.0", "1.00" are one; anything else (including "0.50" and "10") takes the plural. */
function isOne(amount: string): boolean {
  return /^1(\.0+)?$/.test(amount.trim());
}

const NBSP = ' ';

/** "0.30 ml", "4.10 kg", "2.00 tablets" — one text run, a non-breaking space between number and unit. */
export function formatQuantity(value: QuantityValue): string {
  return `${value.amount}${NBSP}${unitLabel(value.unit, value.amount)}`;
}

/** "+10.00 ml" / "−2.00 ml" — a register movement always shows its sign (the server's amount is signed). */
export function formatSignedQuantity(value: QuantityValue): string {
  const negative = value.amount.startsWith('-');
  const magnitude = negative ? value.amount.slice(1) : value.amount;
  const zero = /^0+(\.0+)?$/.test(magnitude);
  const sign = zero ? '' : negative ? '−' : '+';
  return `${sign}${magnitude}${NBSP}${unitLabel(value.unit, magnitude)}`;
}

/** The accessible form: "0.30 millilitres". */
export function spokenQuantity(value: QuantityValue): string {
  return `${value.amount} ${unitSpoken(value.unit)}`;
}

/** Which dose units a medicine form accepts in the composer. Offered choices only — the server validates. */
const MASS_UNITS = ['Mg', 'Mcg', 'G'];
const DOSE_UNITS_BY_FORM: Readonly<Record<string, string[]>> = {
  Tablet: ['Tablet', ...MASS_UNITS],
  Capsule: ['Capsule', ...MASS_UNITS],
  OralLiquid: ['Ml', ...MASS_UNITS],
  OralPaste: ['Ml', ...MASS_UNITS],
  Powder: [...MASS_UNITS],
  Injection: ['Ml', ...MASS_UNITS, 'Iu'],
  Topical: ['Application', ...MASS_UNITS],
  EyeDrops: ['Drop', 'Ml'],
  EarDrops: ['Drop', 'Ml'],
  Transdermal: ['Application', ...MASS_UNITS],
  Other: [...MASS_UNITS, 'Ml', 'Tablet', 'Capsule', 'Drop', 'Application', 'Iu'],
};

export function doseUnitsForForm(form: string): string[] {
  return DOSE_UNITS_BY_FORM[form] ?? DOSE_UNITS_BY_FORM.Other!;
}

/** `StrengthUnit` values the strength editor offers (the formulary's strength unit, not a dose unit). */
export const STRENGTH_UNITS = ['Mg', 'Mcg', 'G', 'Iu', 'Percent'] as const;
/** `DispensingUnit` values (the formulary's "dispensing unit" and the strength's per-unit). */
export const DISPENSING_UNITS = [
  'Tablet',
  'Capsule',
  'Millilitre',
  'Gram',
  'Vial',
  'Ampoule',
  'Tube',
  'Bottle',
  'Patch',
  'Pipette',
  'Dose',
] as const;

/** "0.05–0.10 mg/kg (0.21–0.41 mg for 4.10 kg)" — assembled from the server's figures only. */
export interface DoseRangeValue {
  minMgPerKg: string;
  maxMgPerKg: string;
  minMg?: QuantityValue | null;
  maxMg?: QuantityValue | null;
}

export function formatDoseRange(range: DoseRangeValue, weight?: QuantityValue | null): string {
  const perKg = `${range.minMgPerKg}–${range.maxMgPerKg}${NBSP}mg/kg`;
  if (!range.minMg || !range.maxMg) return perKg;
  const mg = `${range.minMg.amount}–${range.maxMg.amount}${NBSP}${unitLabel(range.maxMg.unit)}`;
  return weight ? `${perKg} (${mg} for ${formatQuantity(weight)})` : `${perKg} (${mg})`;
}

/** "5 days left", "Last day today", "Course finished" — `days` is the server's `remainingDays`. */
export function formatRemaining(days: number): string {
  if (days <= 0) return 'Course finished';
  if (days === 1) return 'Last day today';
  return `${days} days left`;
}
