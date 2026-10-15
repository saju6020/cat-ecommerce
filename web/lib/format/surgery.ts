import { formatQuantity, unitLabel } from './units';

/**
 * `lib/format/surgery.ts` — HO-SRG-002 §8.4, ADR-FE-075. The ONE way a surgery dose, quantity, vital
 * or mg/kg figure becomes text, and the ONE table of plain-language labels for the surgery enumerations
 * (the wire carries the C# `Enumeration.DisplayName`). A bare number never reaches the screen (UI-8):
 * every value is a number WITH its unit, built here. Nothing in this file decides anything clinical.
 */

const NBSP = ' ';

function text(amount: number | string): string {
  return typeof amount === 'number' ? String(amount) : amount;
}

/** `0.05 ml`, `1 tablet` — a dose. The unit is the server's `DoseUnit` name. */
export function formatDose(amount: number | string, unit: string): string {
  return formatQuantity({ amount: text(amount), unit });
}

/** `1 vial`, `0.50 ml` — a quantity drawn or wasted, in the stock unit the server sent. */
export function formatDrawn(amount: number | string, unit: string | null | undefined): string {
  return unit ? formatQuantity({ amount: text(amount), unit }) : text(amount);
}

export type VitalKind =
  | 'heartRateBpm'
  | 'respiratoryRatePerMin'
  | 'spO2Percent'
  | 'etCo2MmHg'
  | 'temperatureCelsius'
  | 'systolicMmHg'
  | 'meanArterialMmHg';

export const VITAL_UNITS: Readonly<Record<VitalKind, string>> = {
  heartRateBpm: 'bpm',
  respiratoryRatePerMin: '/min',
  spO2Percent: '%',
  etCo2MmHg: 'mmHg',
  temperatureCelsius: '°C',
  systolicMmHg: 'mmHg',
  meanArterialMmHg: 'mmHg',
};

export const VITAL_LABELS: Readonly<Record<VitalKind, string>> = {
  heartRateBpm: 'Heart rate',
  respiratoryRatePerMin: 'Respiratory rate',
  spO2Percent: 'SpO2',
  etCo2MmHg: 'EtCO2',
  temperatureCelsius: 'Temperature',
  systolicMmHg: 'Systolic pressure',
  meanArterialMmHg: 'Mean arterial pressure',
};

export const VITAL_KINDS = Object.keys(VITAL_UNITS) as VitalKind[];

/** `120 bpm`, `38.6 °C`, `98 %` — a vital with its unit, never a bare number. */
export function formatVital(kind: VitalKind, value: number | string): string {
  return `${text(value)}${NBSP}${VITAL_UNITS[kind]}`;
}

const MG_PER_UNIT: Readonly<Record<string, number>> = { Mg: 1, Mcg: 0.001, G: 1000 };

function trimNumber(value: number, digits: number): string {
  return Number(value.toFixed(digits)).toString();
}

/**
 * `≈ 0.2 mg/kg at 4.2 kg (weighed 11 Oct)` — for INFORMATION only (ADR-FE-075; dose warnings are the
 * server's policy, HO-SRG-001 OQ-11). Only a mass unit has a mg/kg figure; anything else returns null
 * rather than guess. The result is labelled with the weight it was worked out at and the date.
 */
export function formatMgPerKg(
  amount: number,
  unit: string,
  weightKg: number,
  weighedOnLabel: string,
): string | null {
  const factor = MG_PER_UNIT[unit];
  if (factor === undefined || !Number.isFinite(amount) || amount <= 0 || weightKg <= 0) return null;
  const perKg = (amount * factor) / weightKg;
  return `≈ ${trimNumber(perKg, 3)} mg/kg at ${trimNumber(weightKg, 2)} kg (weighed ${weighedOnLabel})`;
}

/** The server's own figure (`effectiveDoseMgPerKg`), labelled. */
export function formatServerMgPerKg(
  mgPerKg: number,
  weightKg: number | null,
  weighedOnLabel: string | null,
): string {
  const base = `≈ ${trimNumber(mgPerKg, 3)} mg/kg`;
  return weightKg !== null && weighedOnLabel
    ? `${base} at ${trimNumber(weightKg, 2)} kg (weighed ${weighedOnLabel})`
    : base;
}

/** `4.2 kg` — a weight with its unit. */
export function formatWeightKg(kilograms: number): string {
  return `${trimNumber(kilograms, 2)}${NBSP}${unitLabel('Kg')}`;
}

// ---- labels ----------------------------------------------------------------------------------

export const SURGERY_STATUS_LABELS: Readonly<Record<string, string>> = {
  Scheduled: 'Scheduled',
  PreOp: 'Pre-op',
  InTheatre: 'In theatre',
  Recovery: 'Recovery',
  Discharged: 'Discharged',
  Cancelled: 'Cancelled',
  // the owner-facing status (HO-SRG-001 OQ-12)
  InCare: 'In our care',
};

export function surgeryStatusLabel(status: string): string {
  return SURGERY_STATUS_LABELS[status] ?? status;
}

export const CHECKLIST_LABELS: Readonly<Record<string, string>> = {
  FastingInstructionsIssued: 'Fasting instructions issued',
  PreAnaestheticBloodWork: 'Pre-anaesthetic blood work',
  WeightRecorded: 'Weight recorded',
  ConsentSigned: 'Consent signed',
  AsaGradeAssessed: 'ASA grade assessed',
};

/** Short names used in "3 outstanding: consent, weight, ASA grade". */
export const CHECKLIST_SHORT_LABELS: Readonly<Record<string, string>> = {
  FastingInstructionsIssued: 'fasting instructions',
  PreAnaestheticBloodWork: 'blood work',
  WeightRecorded: 'weight',
  ConsentSigned: 'consent',
  AsaGradeAssessed: 'ASA grade',
};

export function checklistLabel(kind: string): string {
  return CHECKLIST_LABELS[kind] ?? kind;
}

/** `Complete` or `3 outstanding: consent, weight, ASA grade` (US-SRG-02 AC2), as text. */
export function describeChecklist(summary: {
  isComplete: boolean;
  outstandingItems: string[];
}): string {
  if (summary.isComplete || summary.outstandingItems.length === 0) return 'Complete';
  const names = summary.outstandingItems.map((kind) => CHECKLIST_SHORT_LABELS[kind] ?? kind);
  return `${names.length} outstanding: ${names.join(', ')}`;
}

export const RECOVERY_LABELS: Readonly<Record<string, string>> = {
  Recovering: 'Recovering',
  Stable: 'Stable',
  ReadyForDischarge: 'Ready for discharge',
};

export function recoveryLabel(status: string): string {
  return RECOVERY_LABELS[status] ?? status;
}

/** Board order: Ready for discharge first, then Recovering, then Stable (ADR-FE-073). */
export const RECOVERY_ORDER = ['ReadyForDischarge', 'Recovering', 'Stable'] as const;

export const FOLLOW_UP_LABELS: Readonly<Record<string, string>> = {
  WoundCheck: 'Wound check',
  SutureRemoval: 'Suture removal',
  Recheck: 'Recheck',
};

export function followUpLabel(kind: string): string {
  return FOLLOW_UP_LABELS[kind] ?? kind;
}

export const MILESTONE_LABELS: Readonly<Record<string, string>> = {
  Premedication: 'Premedication',
  Induction: 'Induction',
  Intubation: 'Intubation',
  IncisionStart: 'Incision start',
  ProcedureEnd: 'Procedure end',
  Extubation: 'Extubation',
  Other: 'Other',
};

export const MILESTONES = Object.keys(MILESTONE_LABELS);

export function milestoneLabel(milestone: string): string {
  return MILESTONE_LABELS[milestone] ?? milestone;
}

export const ROUTE_ABBREVIATIONS: Readonly<Record<string, string>> = {
  Oral: 'PO',
  Subcutaneous: 'SC',
  Intramuscular: 'IM',
  Intravenous: 'IV',
  Topical: 'topical',
  Ophthalmic: 'ophthalmic',
  Otic: 'otic',
  Transdermal: 'transdermal',
  Inhaled: 'inhaled',
  Rectal: 'rectal',
};

export function routeLabel(route: string): string {
  return ROUTE_ABBREVIATIONS[route] ?? route.toLowerCase();
}

export const ASA_GRADES = ['I', 'II', 'III', 'IV', 'V'] as const;

export const PRICING_BASIS_LABELS: Readonly<Record<string, string>> = {
  Standard: 'Standard price',
  Campaign: 'Campaign price',
};

export function formatMinutes(minutes: number): string {
  if (minutes < 60) return `${minutes} min`;
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  return rest === 0 ? `${hours} h` : `${hours} h ${rest} min`;
}
