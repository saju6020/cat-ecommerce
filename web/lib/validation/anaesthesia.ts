import { surgeryViolations } from '@/lib/api/errors';
import { zonedLocalToInstant } from '@/lib/format/appointment';
import { timeOnlyInZone } from '@/lib/format/time';
import type { VitalKind } from '@/lib/format/surgery';

/**
 * `lib/validation/anaesthesia.ts` — HO-SRG-002 §13. Helpers for the anaesthesia log's entry dialogs:
 * parsing a typed number, the idempotency fingerprint, and mapping a refusal back to the field it
 * belongs to. The server validates everything again; nothing here judges whether a dose is safe.
 */

/** A typed decimal ("0.05", "0,05" is NOT accepted), or `null` when it is not a number. */
export function parseDecimal(text: string): number | null {
  const trimmed = text.trim();
  if (!/^\d+(\.\d+)?$/.test(trimmed)) return null;
  const value = Number(trimmed);
  return Number.isFinite(value) ? value : null;
}

/**
 * The values a repeated request with the SAME `entryId` must match: exactly the four fields the server
 * compares before it treats the request as a replay of the first (HO-SRG-001 §1 Amended (18)) — the
 * medicine, the batch, the quantity drawn and the dose. The server returns the first result for a matching
 * request WITHOUT looking at anything else, so a lost-response resend after editing the time, route or vet
 * must keep the same id (a new one would deduct stock and write a register line a second time). Only a
 * change to one of these four needs a new id, or the server answers `AnaesthesiaEntryIdMismatch`.
 */
export function drugFingerprint(input: {
  medicineId: string;
  batchId?: string;
  quantityDrawn?: number;
  doseAmount: number;
}): string {
  return JSON.stringify([
    input.medicineId,
    input.batchId ?? null,
    input.quantityDrawn ?? null,
    input.doseAmount,
  ]);
}

export type DrugField =
  'medicine' | 'dose' | 'route' | 'administeredBy' | 'batch' | 'drawn' | 'wasted' | 'time';

const DRUG_FIELDS: Readonly<Record<string, DrugField>> = {
  medicineId: 'medicine',
  doseAmount: 'dose',
  doseUnit: 'dose',
  'drug.doseAmount': 'dose',
  'drug.doseUnit': 'dose',
  route: 'route',
  'drug.route': 'route',
  administeredById: 'administeredBy',
  'drug.administeredById': 'administeredBy',
  batchId: 'batch',
  drawn: 'drawn',
  quantityDrawn: 'drawn',
  'drug.quantityDrawn': 'drawn',
  wasted: 'wasted',
  quantityWasted: 'wasted',
  'drug.quantityWasted': 'wasted',
  occurredOn: 'time',
};

/** `Drug.DoseAmount` → `drug.doseAmount` (every segment lower-camel). */
export function camelPath(propertyName: string): string {
  return propertyName
    .split('.')
    .map((part) => part.charAt(0).toLowerCase() + part.slice(1))
    .join('.');
}

/** Codes that belong to the witness step and are shown in the witness dialog. */
export const WITNESS_CODES = [
  'WitnessRequired',
  'WitnessNotVerified',
  'WitnessSameAsAdministrator',
  'WitnessConfirmationInvalid',
] as const;

export interface MappedDrugErrors {
  summary: string[];
  byField: Partial<Record<DrugField, string[]>>;
  /** The witness's own failure, for the witness dialog. */
  witness: string | null;
}

export function mapDrugViolations(error: unknown): MappedDrugErrors {
  const out: MappedDrugErrors = { summary: [], byField: {}, witness: null };
  for (const violation of surgeryViolations(error)) {
    if ((WITNESS_CODES as readonly string[]).includes(violation.errorCode)) {
      out.witness = violation.message;
      continue;
    }
    const field = DRUG_FIELDS[camelPath(violation.propertyName)];
    if (field) (out.byField[field] ??= []).push(violation.message);
    else out.summary.push(violation.message);
  }
  return out;
}

/** The vitals a server refusal names: its `propertyName` (`HeartRateBpm`) → the field (`heartRateBpm`). */
export function implausibleVitalKinds(error: unknown): VitalKind[] {
  return surgeryViolations(error)
    .filter((v) => v.errorCode === 'ImplausibleVitalValue')
    .map((v) => v.propertyName.replace(/^vitals\./, '') as VitalKind);
}

/** The current wall-clock time in the branch zone as `HH:mm` (the server's clock offset applied). */
export function nowTimeInZone(zone: string, nowMs = Date.now()): string {
  return timeOnlyInZone(new Date(nowMs).toISOString(), zone);
}

/**
 * A typed `HH:mm` as the instant the server wants: the branch-local day (today's, from the branch zone so a
 * phone in another zone does not shift the day, ADR-FE-015) among the day before, the day itself and the
 * day after that puts the time NEAREST to `referenceMs`. For a new entry the reference is now, so logging
 * 23:55 at 00:05 is last night, and a time a few hours ahead stays today. For a correction the reference is
 * the entry being corrected, so 23:50 corrected to 00:10 is the next day. The server still decides whether
 * the time is acceptable.
 */
export function instantFromTime(time: string, zone: string, referenceMs = Date.now()): string {
  const day = new Intl.DateTimeFormat('en-CA', { timeZone: zone }).format(new Date(referenceMs));
  let best = '';
  let bestGap = Number.POSITIVE_INFINITY;
  for (const offset of [-1, 0, 1]) {
    const d = new Date(`${day}T12:00:00Z`);
    d.setUTCDate(d.getUTCDate() + offset);
    const candidate = zonedLocalToInstant(d.toISOString().slice(0, 10), time, zone);
    const gap = candidate ? Math.abs(new Date(candidate).getTime() - referenceMs) : Infinity;
    if (gap < bestGap) {
      best = candidate;
      bestGap = gap;
    }
  }
  return best || new Date(referenceMs).toISOString();
}

export const TIME_PATTERN = /^([01]\d|2[0-3]):[0-5]\d$/;
