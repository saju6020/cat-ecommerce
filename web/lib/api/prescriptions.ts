import { ApiConflictError, ApiValidationError, apiFetch } from './client';
import { queryString } from './formulary';
import type { PagedResult, ValidationResponse } from './types';
import type { QuantityValue } from '@/lib/format/units';

/**
 * `lib/api/prescriptions.ts` — HO-PHM-002 §8.2. Built to the REAL controllers on master
 * (`StaffPrescriptionsController`) and `PharmacyPrescriptionViewModels.cs`. Differences from
 * HO-PHM-001 §12.4 / HO-PHM-002 are recorded in HO-PHM-002 §1 Amended. Quantities are `{ amount,
 * unit }` with the amount a string; the client never computes anything from them (ADR-FE-060).
 */

export type PrescriptionStatus = 'Issued' | 'Dispensed' | 'Completed' | 'Expired' | 'Cancelled';
export type PrescriptionAction = 'Dispense' | 'Cancel' | 'Renew' | 'DeclineRepeat';
export type DoseCheckOutcome = 'WithinRange' | 'BelowRange' | 'AboveRange' | 'NotCheckable';
export type WarningKind =
  'DoseBelowRange' | 'DoseAboveRange' | 'DoseNotCheckable' | 'AllergyConflict' | 'Interaction';

export interface PrescriptionMedicine {
  name: string;
  /** The medicine's strength text ("1.50 mg per 1.00 ml"). */
  strength: string;
  form: string;
  isControlled: boolean;
}

export interface PrescriptionSummary {
  id: string;
  prescriptionNumber: string;
  medicine: PrescriptionMedicine;
  dose: QuantityValue;
  route: string;
  /** The label wording ("twice daily"), not the request name ("TwiceDaily"). */
  frequency: string;
  durationDays: number;
  quantity: QuantityValue;
  status: PrescriptionStatus;
  isActive: boolean;
  courseEndsOn: string;
  remainingDays: number;
  suppliesDispensed: number;
  suppliesAuthorised: number;
  validUntil: string;
  prescriberName: string;
  issuedOn: string;
  hasAcknowledgedWarnings: boolean;
}

// ── the safety check ───────────────────────────────────────────────────────────────────────────

export interface ExpectedRange {
  minMgPerKg: string;
  maxMgPerKg: string;
  minMg: QuantityValue | null;
  maxMg: QuantityValue | null;
}

export interface SafetyWarning {
  kind: WarningKind;
  blocking: boolean;
  requiresAcknowledgement: boolean;
  severity: string | null;
  subject: string;
  detail: string;
}

export interface SafetyAssessment {
  weight: {
    /** `Satisfied`, `Missing` or `Stale`. */
    requirement: string;
    latest: { weight: QuantityValue; recordedOn: string; ageDays: number } | null;
  };
  doseCheck: {
    outcome: DoseCheckOutcome;
    doseMg: QuantityValue | null;
    doseMgPerKg: string | null;
    expectedRange: ExpectedRange | null;
  };
  warnings: SafetyWarning[];
  information: string[];
  contraindications: string;
  hasRecordedAllergies: boolean;
  /** What the issue call sends back to say "I have seen exactly these warnings" (ADR-139). */
  fingerprint: string;
}

export interface SafetyCheckRequest {
  medicineId: string;
  dose: { amount: number; unit: string };
  route: string;
  /** The enumeration NAME ("TwiceDaily"). */
  frequency: string;
}

// ── issuing, renewing ───────────────────────────────────────────────────────────────────────────

export interface PrescriptionFields extends SafetyCheckRequest {
  durationDays: number;
  quantity: number;
  instructions: string;
  startsOn?: string | null;
  isRepeatable: boolean;
  repeatsAuthorised: number;
  validUntil: string | null;
  acknowledgedWarningsFingerprint: string | null;
  allergyAcknowledgementReason: string | null;
  doseAcknowledgementNote: string | null;
}

export interface IssuePrescriptionRequest extends PrescriptionFields {
  visitId: string;
}

/** Remote renewal (owner decision 2026-10-10): `visitId` may be absent. */
export type RenewPrescriptionRequest = Omit<PrescriptionFields, 'medicineId'> & {
  visitId?: string | null;
};

export interface IssuedPrescription {
  prescriptionId: string;
  prescriptionNumber: string;
}

// ── detail ──────────────────────────────────────────────────────────────────────────────────────

export interface SafetyRecord {
  weightUsed: QuantityValue | null;
  weightRecordedOn: string | null;
  doseMgPerKg: string | null;
  expectedRange: ExpectedRange | null;
  doseCheckOutcome: DoseCheckOutcome;
  acknowledgedByName: string | null;
  acknowledgedOn: string | null;
  allergyAcknowledgementReason: string | null;
  doseAcknowledgementNote: string | null;
}

export interface RepeatRequest {
  id: string;
  requestedOn: string;
  preferredBranchId: string;
  note: string | null;
  /** `Requested`, `Dispensed`, `Declined`, `Withdrawn`. */
  status: string;
  decidedOn: string | null;
  declineReason: string | null;
  dispensingId: string | null;
}

export interface PrescriptionDispensingLine {
  lineId: string;
  lotNumber: string;
  expiresOn: string | null;
  quantity: QuantityValue;
}

export interface PrescriptionDispensing {
  id: string;
  supplyNumber: number;
  isRepeat: boolean;
  dispensedOn: string;
  dispensedByName: string;
  status: string;
  lines: PrescriptionDispensingLine[];
}

export interface PrescriptionReference {
  id: string;
  prescriptionNumber: string;
}

export interface PrescriptionDetail extends PrescriptionSummary {
  catId: string;
  catName: string;
  instructions: string;
  visitId: string;
  /** True for a renewal written without an open visit (owner decision 2026-10-10). */
  isRemoteRenewal: boolean;
  startsOn: string;
  isRepeatable: boolean;
  safetyRecord: SafetyRecord;
  warnings: SafetyWarning[];
  dispensings: PrescriptionDispensing[];
  repeatRequests: RepeatRequest[];
  renewedFrom: PrescriptionReference | null;
  renewedBy: PrescriptionReference | null;
  allowedActions: PrescriptionAction[];
}

export type PrescriptionScope = 'active' | 'all';

// ── calls ───────────────────────────────────────────────────────────────────────────────────────

const staff = '/api/staff';

export function checkPrescriptionSafety(
  catId: string,
  body: SafetyCheckRequest,
): Promise<SafetyAssessment> {
  return apiFetch(`${staff}/cats/${catId}/prescriptions/safety-check`, { method: 'POST', body });
}

export function issuePrescription(
  catId: string,
  body: IssuePrescriptionRequest,
): Promise<IssuedPrescription> {
  return apiFetch(`${staff}/cats/${catId}/prescriptions`, { method: 'POST', body });
}

export function renewPrescription(
  prescriptionId: string,
  body: RenewPrescriptionRequest,
): Promise<IssuedPrescription> {
  return apiFetch(`${staff}/prescriptions/${prescriptionId}/renew`, { method: 'POST', body });
}

export function listCatPrescriptions(
  catId: string,
  params: { scope: PrescriptionScope; pageNumber?: number; pageSize?: number },
): Promise<PagedResult<PrescriptionSummary>> {
  return apiFetch(`${staff}/cats/${catId}/prescriptions${queryString({ ...params })}`);
}

export function getPrescription(prescriptionId: string): Promise<PrescriptionDetail> {
  return apiFetch(`${staff}/prescriptions/${prescriptionId}`);
}

export function cancelPrescription(prescriptionId: string, reason: string): Promise<void> {
  return apiFetch(`${staff}/prescriptions/${prescriptionId}/cancel`, {
    method: 'POST',
    body: { reason },
  });
}

export function declineRepeatRequest(
  prescriptionId: string,
  requestId: string,
  reason: string,
): Promise<void> {
  return apiFetch(`${staff}/prescriptions/${prescriptionId}/repeat-requests/${requestId}/decline`, {
    method: 'POST',
    body: { reason },
  });
}

/** `GET /api/staff/visits/{visitId}/prescriptions` — HO-EMR-001's contract; the discharge preview uses it. */
export function getVisitPrescriptions(visitId: string): Promise<{
  prescriptions: PrescriptionSummary[];
  dietAuthorisations: Array<{
    id: string;
    productName: string;
    validFrom: string;
    validTo: string;
    status: string;
  }>;
}> {
  return apiFetch(`${staff}/visits/${visitId}/prescriptions`);
}

/**
 * The `400`/`409` bodies of issue and renew carry the fresh `assessment` next to `errors`
 * (`StaffPrescriptionsController.WithAssessment`). `client.ts` keeps the whole parsed body on
 * `error.response`, so this reads it back without a client change — and returns `null` for any other
 * failure, so a caller replaces its panel only when the server sent one (ADR-FE-060).
 */
export function assessmentFromError(error: unknown): SafetyAssessment | null {
  if (!(error instanceof ApiValidationError) && !(error instanceof ApiConflictError)) return null;
  const body = error.response as
    (ValidationResponse & { assessment?: SafetyAssessment }) | undefined;
  return body?.assessment ?? null;
}

/** First `errorCode` in a failed command's body — read only to choose a reaction, never shown. */
export function violationCodes(error: unknown): string[] {
  if (!(error instanceof ApiValidationError) && !(error instanceof ApiConflictError)) return [];
  return error.response?.errors?.map((entry) => entry.errorCode) ?? [];
}
