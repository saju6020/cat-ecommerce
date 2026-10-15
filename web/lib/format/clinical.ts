import type { Measurement, VitalSign } from '@/lib/api/visits';
import type { DiagnosticKind, DiagnosticOrderStatus, ResultFlag } from '@/lib/api/diagnostics';
import type { ConsentKind, ConsentMethod, ConsentStatus } from '@/lib/api/consents';

/**
 * `lib/format/clinical.ts` — HO-EMR-002 §8.4 (ADR-FE-053, UI-8). The one place a clinical number
 * becomes text. A value is the SERVER'S STRING at the sign's stated precision — never parsed to a
 * float and re-formatted — and always travels with its unit.
 */

/** `"4.20 kg"`, `"38.6 °C"`, `"180 bpm"`, `"5 /9"`. A unit that starts with `/` keeps a space. */
export function formatMeasurement(measurement: Measurement | null | undefined): string {
  if (!measurement) return 'Not recorded';
  return `${measurement.value} ${measurement.unit}`;
}

/** A value and unit given separately (a result row, a range bound). */
export function formatValueWithUnit(value: string, unit: string | null | undefined): string {
  return unit ? `${value} ${unit}` : value;
}

/** A reference range with its unit, e.g. `"35.0–41.5 °C"`; `null` when there is none. */
export function formatRange(
  low: string | null,
  high: string | null,
  unit: string | null,
): string | null {
  if (low === null && high === null) return null;
  const text =
    low !== null && high !== null ? `${low}–${high}` : low !== null ? `≥ ${low}` : `≤ ${high}`;
  return formatValueWithUnit(text, unit);
}

export const FLAG_LABELS: Readonly<Record<ResultFlag, string>> = {
  High: 'High ↑',
  Low: 'Low ↓',
  Abnormal: 'Abnormal',
  Normal: 'Normal',
  NotAssessed: 'Not assessed',
};

/** `"High ↑"`, `"Low ↓"`, `"Abnormal"`, `"Normal"`, `"Not assessed"` — text, never colour alone (UI-6). */
export function formatFlag(flag: ResultFlag): string {
  return FLAG_LABELS[flag] ?? flag;
}

export const VITAL_SIGN_LABELS: Readonly<Record<VitalSign, string>> = {
  Weight: 'Weight',
  Temperature: 'Temperature',
  HeartRate: 'Heart rate',
  RespiratoryRate: 'Respiratory rate',
  BodyConditionScore: 'Body condition score',
  PainScore: 'Pain score',
};

export function vitalSignLabel(sign: VitalSign): string {
  return VITAL_SIGN_LABELS[sign] ?? sign;
}

const DIAGNOSTIC_KIND_LABELS: Readonly<Record<DiagnosticKind, string>> = {
  BloodWork: 'Blood work',
  Urinalysis: 'Urinalysis',
  FelvFivTest: 'FeLV/FIV test',
  Imaging: 'Imaging',
  Other: 'Other',
};

export function diagnosticKindLabel(kind: string): string {
  return DIAGNOSTIC_KIND_LABELS[kind as DiagnosticKind] ?? kind;
}

export const DIAGNOSTIC_KINDS = Object.keys(DIAGNOSTIC_KIND_LABELS) as DiagnosticKind[];

const ORDER_STATUS_LABELS: Readonly<Record<DiagnosticOrderStatus, string>> = {
  Pending: 'Waiting for results',
  Resulted: 'Results in',
  Cancelled: 'Cancelled',
};

export function diagnosticStatusLabel(status: string): string {
  return ORDER_STATUS_LABELS[status as DiagnosticOrderStatus] ?? status;
}

export function consentKindLabel(kind: ConsentKind | string): string {
  return kind === 'Anaesthesia' ? 'Anaesthesia' : kind;
}

export function consentMethodLabel(method: ConsentMethod | string): string {
  return method === 'OnScreenSignature' ? 'Signed on screen' : 'Signed paper form uploaded';
}

export function consentStatusLabel(status: ConsentStatus | string): string {
  return status === 'Withdrawn' ? 'Withdrawn' : 'Recorded';
}

/** `"2 High, 1 Abnormal"` from the `flags` field of a lab history item (`"High:2,Abnormal:1"`). */
export function summariseFlagsField(flags: string | null | undefined): string {
  if (!flags) return '';
  return flags
    .split(',')
    .map((part) => part.split(':'))
    .filter(([name, count]) => name && count && name !== 'Normal' && name !== 'NotAssessed')
    .map(([name, count]) => `${count} ${formatFlag(name as ResultFlag).replace(/ [↑↓]$/, '')}`)
    .join(', ');
}

/** A character count against the server's limit, e.g. `"128 / 20000"`. */
export function formatCount(length: number, max: number): string {
  return `${length} / ${max}`;
}

/**
 * The sentence the printed summary carries when no prescription contributor is registered
 * (`ClinicalMessages.PrescriptionsOnLabel` on the server, which puts it in the PDF; the JSON summary
 * has only the `prescriptionsAvailable: false` flag). Kept here so staff and portal say the same words.
 */
export const PRESCRIPTIONS_ON_LABEL = 'Medicines to take home are on the prescription label.';
