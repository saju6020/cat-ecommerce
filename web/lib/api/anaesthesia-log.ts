import { apiFetch } from './client';

/**
 * `lib/api/anaesthesia-log.ts` — HO-SRG-002 §8.2. Built to `StaffSurgeryAnaesthesiaController`,
 * `AnaesthesiaRequests.cs` and `AnaesthesiaLogViewModels.cs`. There is NO `PUT`/`PATCH`/`DELETE` here
 * and there never will be: an entry is created once, and a mistake is a linked correction (ADR-FE-070,
 * US-SRG-08 AC2, HO-SRG-002 §20.6).
 *
 * **Witness mechanism (HO-SRG-001 §1 Amended (16)).** The backend does NOT take a witness-confirmation
 * id from HO-PHM-001; the witness types their own identifier and password into the SAME request and the
 * handler verifies them in-request. The credentials therefore appear in `LogDrugRequest` /
 * `CorrectEntryRequest`. They are sent once and are never stored, logged or echoed; the callers in
 * `lib/hooks/useAnaesthesiaLog.ts` are plain functions (not TanStack mutations) so a password is never
 * retained in a mutation cache (ADR-FE-071).
 */

export type EntryKind = 'Drug' | 'Vitals' | 'Milestone' | 'Correction';

export interface EntryValues {
  medicineId: string | null;
  medicineName: string | null;
  isControlled: boolean;
  doseAmount: number | null;
  doseUnit: string | null;
  route: string | null;
  administeredById: string | null;
  administeredByName: string | null;
  batchNumber: string | null;
  quantityDrawn: number | null;
  quantityWasted: number | null;
  stockUnit: string | null;
  witnessId: string | null;
  witnessName: string | null;
  registerEntryId: string | null;
  stockShortfall: boolean;
  heartRateBpm: number | null;
  respiratoryRatePerMin: number | null;
  spO2Percent: number | null;
  etCo2MmHg: number | null;
  temperatureCelsius: number | null;
  systolicMmHg: number | null;
  meanArterialMmHg: number | null;
  implausibleConfirmed: boolean;
  milestone: string | null;
  note: string | null;
}

export interface AnaesthesiaEntry {
  id: string;
  kind: EntryKind;
  occurredOn: string;
  loggedOn: string;
  loggedBy: string;
  loggedByName: string;
  /** What THIS entry itself recorded. */
  values: EntryValues;
  /** The values after every correction of it (the leaf of its chain). */
  effective: EntryValues;
  isCorrected: boolean;
  correctedByEntryId: string | null;
  correctsEntryId: string | null;
  /** For a `Correction` entry: the kind of the entry it corrects. */
  correctedKind: string | null;
  correctionReason: string | null;
  effectiveDoseMgPerKg: number | null;
}

export interface AnaesthesiaLog {
  surgeryId: string;
  catId: string;
  branchTimeZoneId: string;
  isOpen: boolean;
  latestWeight: { kilograms: number; recordedOn: string } | null;
  entries: AnaesthesiaEntry[];
}

export interface LogDrugRequest {
  /** The idempotency key: generate ONE per drug entry and reuse it on every retry (HO-SRG-001 §1 Amended (18)). */
  entryId: string;
  medicineId: string;
  doseAmount: number;
  doseUnit: string;
  route: string;
  occurredOn: string;
  administeredById: string;
  batchId?: string;
  quantityDrawn?: number;
  quantityWasted?: number;
  witnessIdentifier?: string;
  witnessPassword?: string;
  fefoOverrideReason?: string;
}

export interface VitalsFields {
  heartRateBpm?: number;
  respiratoryRatePerMin?: number;
  spO2Percent?: number;
  etCo2MmHg?: number;
  temperatureCelsius?: number;
  systolicMmHg?: number;
  meanArterialMmHg?: number;
  note?: string;
  confirmImplausible?: boolean;
}

export interface LogVitalsRequest extends VitalsFields {
  occurredOn: string;
}

export interface LogMilestoneRequest {
  milestone: string;
  occurredOn: string;
  note?: string;
}

export interface CorrectEntryRequest {
  reason: string;
  occurredOn: string;
  drug?: {
    doseAmount: number;
    doseUnit: string;
    route: string;
    administeredById: string;
    quantityDrawn?: number;
    quantityWasted?: number;
  };
  vitals?: VitalsFields;
  milestone?: { milestone: string; note?: string };
  witnessIdentifier?: string;
  witnessPassword?: string;
  fefoOverrideReason?: string;
}

const base = (surgeryId: string) =>
  `/api/staff/surgeries/${encodeURIComponent(surgeryId)}/anaesthesia-log`;

export function getAnaesthesiaLog(surgeryId: string): Promise<AnaesthesiaLog> {
  return apiFetch(base(surgeryId));
}

export function logDrug(
  surgeryId: string,
  body: LogDrugRequest,
): Promise<{ entryId: string; registerEntryId: string | null; stockShortfall: boolean }> {
  return apiFetch(`${base(surgeryId)}/drugs`, { method: 'POST', body });
}

export function logVitals(surgeryId: string, body: LogVitalsRequest): Promise<{ entryId: string }> {
  return apiFetch(`${base(surgeryId)}/vitals`, { method: 'POST', body });
}

export function logMilestone(
  surgeryId: string,
  body: LogMilestoneRequest,
): Promise<{ entryId: string }> {
  return apiFetch(`${base(surgeryId)}/milestones`, { method: 'POST', body });
}

export function correctEntry(
  surgeryId: string,
  entryId: string,
  body: CorrectEntryRequest,
): Promise<{ entryId: string }> {
  return apiFetch(`${base(surgeryId)}/${encodeURIComponent(entryId)}/corrections`, {
    method: 'POST',
    body,
  });
}
