import { formatInstantDate } from '@/lib/format/time';

/**
 * `lib/format/pharmacy.ts` — HO-PHM-002 §8.4. Plain-language labels for the pharmacy enumerations
 * (the wire carries C# `Enumeration.DisplayName`s) and a few date helpers. Labels only — nothing here
 * decides anything clinical.
 */
export const MEDICINE_FORM_LABELS: Readonly<Record<string, string>> = {
  Tablet: 'Tablet',
  Capsule: 'Capsule',
  OralLiquid: 'Oral liquid',
  OralPaste: 'Oral paste',
  Powder: 'Powder',
  Injection: 'Injection',
  Topical: 'Topical',
  EyeDrops: 'Eye drops',
  EarDrops: 'Ear drops',
  Transdermal: 'Transdermal',
  Other: 'Other',
};

export const ROUTE_LABELS: Readonly<Record<string, string>> = {
  Oral: 'Oral',
  Subcutaneous: 'Subcutaneous',
  Intramuscular: 'Intramuscular',
  Intravenous: 'Intravenous',
  Topical: 'Topical',
  Ophthalmic: 'Eye',
  Otic: 'Ear',
  Transdermal: 'Transdermal',
  Inhaled: 'Inhaled',
  Rectal: 'Rectal',
};

export const DOSING_BASIS_LABELS: Readonly<Record<string, string>> = {
  PerKg: 'Per kg of body weight',
  Fixed: 'Fixed dose',
};

/** The `DoseFrequency` choices: request NAME → label (the wording printed on the medicine label). */
export const FREQUENCY_OPTIONS: ReadonlyArray<{ name: string; label: string }> = [
  { name: 'Once', label: 'once' },
  { name: 'OnceDaily', label: 'once daily' },
  { name: 'TwiceDaily', label: 'twice daily' },
  { name: 'ThreeTimesDaily', label: 'three times daily' },
  { name: 'FourTimesDaily', label: 'four times daily' },
  { name: 'EveryOtherDay', label: 'every other day' },
  { name: 'Weekly', label: 'once weekly' },
  { name: 'AsNeeded', label: 'as needed' },
];

/** A read returns the LABEL ("twice daily"); a request needs the NAME ("TwiceDaily"). */
export function frequencyNameFromLabel(label: string): string {
  return FREQUENCY_OPTIONS.find((option) => option.label === label)?.name ?? '';
}

export const SEVERITY_ORDER = ['Contraindicated', 'Major', 'Moderate', 'Minor'] as const;

export function formLabel(form: string): string {
  return MEDICINE_FORM_LABELS[form] ?? form;
}

export function routeLabel(route: string): string {
  return ROUTE_LABELS[route] ?? route;
}

/** "3 Sep 2026" for a `yyyy-MM-dd` date, with no time-zone shift. */
export function formatDay(dateOnly: string | null | undefined, fallback = '—'): string {
  if (!dateOnly) return fallback;
  const date = new Date(`${dateOnly.slice(0, 10)}T00:00:00Z`);
  if (Number.isNaN(date.getTime())) return fallback;
  return date.toLocaleDateString('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    timeZone: 'UTC',
  });
}

/** An instant as a date plus a 24-hour time in the branch's zone: "22 Sep 2026, 10:14". */
export function formatInstantInZone(instant: string | null | undefined, zone: string): string {
  if (!instant) return '—';
  const date = new Date(instant);
  if (Number.isNaN(date.getTime())) return '—';
  const day = date.toLocaleDateString('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    timeZone: zone,
  });
  const time = date.toLocaleTimeString('en-GB', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
    timeZone: zone,
  });
  return `${day}, ${time}`;
}

/** "10:42" — the time of an instant in the given zone, 24-hour. */
export function formatTimeInZone(instant: string | null | undefined, zone: string): string {
  if (!instant) return '—';
  const date = new Date(instant);
  if (Number.isNaN(date.getTime())) return '—';
  return date.toLocaleTimeString('en-GB', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
    timeZone: zone,
  });
}

/**
 * Decimal text as an integer count of ten-thousandths ("0.30" -> 3000), so quantities can be summed and
 * compared EXACTLY (no floating point). `NaN` for anything that is not a plain non-negative decimal.
 * Used only for the dispense screen's "8 of 10 ml selected" total (U-C4) — the server still checks.
 */
export function toScaled(text: string): number {
  const match = /^(\d+)(?:\.(\d{1,4}))?$/.exec(text.trim());
  if (!match) return Number.NaN;
  return (
    Number.parseInt(match[1]!, 10) * 10000 + Number.parseInt((match[2] ?? '').padEnd(4, '0'), 10)
  );
}

/** The inverse of `toScaled`, shown with at least two and at most four decimals ("0.30", "8.00"). */
export function fromScaled(scaled: number): string {
  const whole = Math.trunc(scaled / 10000);
  const fraction = String(scaled % 10000)
    .padStart(4, '0')
    .replace(/0+$/, '')
    .padEnd(2, '0');
  return `${whole}.${fraction}`;
}

export function sumAmounts(amounts: string[]): string {
  return fromScaled(
    amounts.reduce(
      (sum, amount) => sum + (Number.isNaN(toScaled(amount)) ? 0 : toScaled(amount)),
      0,
    ),
  );
}

export function sameAmount(a: string, b: string): boolean {
  const left = toScaled(a);
  return !Number.isNaN(left) && left === toScaled(b);
}

/** Re-exported so screens import their date helpers from one place. */
export { formatInstantDate };
