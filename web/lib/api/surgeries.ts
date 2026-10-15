import { apiFetch } from './client';
import { queryString } from './formulary';
import type { PagedResult } from './types';
import type { Money } from '@/lib/format/money';

/**
 * `lib/api/surgeries.ts` — HO-SRG-002 §8.2. Built to the REAL controllers on master
 * (`StaffSurgeriesController`, `SurgeryControllerBase`) and `SurgeryViewModels.cs` /
 * `SurgeryRecoveryViewModels.cs`, not only HO-SRG-001 §12.4 (HO-SRG-002 §20.4). Enum-shaped strings are
 * the C# `Enumeration.DisplayName` (`PreOp`, `InTheatre`, `ReadyForDischarge`). Instants are ISO-8601 with
 * offset; local dates are `yyyy-MM-dd`; money is `{ amount: string, currencyCode }`.
 *
 * Every function is a plain request: the SERVER is the only authority on clashes, checklist state, the
 * start gate and what a surgery may do next (ADR-FE-069, ADR-FE-072) — nothing here computes any of it.
 */

export type SurgeryStatus =
  'Scheduled' | 'PreOp' | 'InTheatre' | 'Recovery' | 'Discharged' | 'Cancelled';
export type ChecklistKind =
  | 'FastingInstructionsIssued'
  | 'PreAnaestheticBloodWork'
  | 'WeightRecorded'
  | 'ConsentSigned'
  | 'AsaGradeAssessed';
export type ChecklistStatus = 'Outstanding' | 'Done' | 'Waived';
export type RecoveryStatus = 'Recovering' | 'Stable' | 'ReadyForDischarge';
export type ProcedureOutcome = 'Completed' | 'Abandoned';
export type SurgeryFollowUpKind = 'WoundCheck' | 'SutureRemoval' | 'Recheck';
export type AsaGrade = 'I' | 'II' | 'III' | 'IV' | 'V';
/** `SurgeryTransitionRules` action names — status-only, NOT permission-aware (ADR-FE-028). */
export type SurgeryAction =
  | 'Reschedule'
  | 'Cancel'
  | 'Admit'
  | 'Start'
  | 'EndProcedure'
  | 'SetRecoveryStatus'
  | 'Discharge'
  | 'CloseFollowUp';

export interface NamedRef {
  id: string;
  name: string;
}

// ---- booking picker --------------------------------------------------------------------------

export interface SurgeryService {
  id: string;
  name: string;
  category: string;
  durationMinutes: number;
  price: Money;
  isSterilisation: boolean;
  qualifiedRoleIds: string[];
}

export interface SurgeryStaffOption {
  id: string;
  name: string;
  roleIds: string[];
}

export interface SurgeryStaff {
  surgeons: SurgeryStaffOption[];
  assistants: SurgeryStaffOption[];
}

export interface SurgeryStart {
  startsOn: string;
  endsOn: string;
  outsideOpeningHours: boolean;
}

export interface BusyInterval {
  startsOn: string;
  endsOn: string;
  label: string;
}

export interface SurgeryAvailability {
  date: string;
  branchTimeZoneId: string;
  gridMinutes: number;
  starts: SurgeryStart[];
  busy: { theatre: BusyInterval[]; surgeon: BusyInterval[]; assistant: BusyInterval[] };
}

export interface AvailabilityParams {
  serviceId: string;
  date: string;
  surgeonId?: string;
  assistantId?: string;
  theatreId?: string;
  catId?: string;
  excludeSurgeryId?: string;
}

export interface BookSurgeryRequest {
  customerId: string;
  catId: string;
  serviceId: string;
  startsOn: string;
  theatreId: string;
  surgeonId: string;
  assistantId: string;
  campaignId?: string;
  campaignEligibilityNote?: string;
  confirmNeuterStatusDiscrepancy?: boolean;
}

export interface RescheduleSurgeryRequest {
  expectedVersion: number;
  startsOn: string;
  theatreId: string;
  surgeonId: string;
  assistantId: string;
}

// ---- day list / list -------------------------------------------------------------------------

export interface ChecklistSummary {
  isComplete: boolean;
  /** `ChecklistKind` names, in checklist order. */
  outstandingItems: string[];
}

export interface SurgeryDayItem {
  surgeryId: string;
  version: number;
  startsOn: string;
  endsOn: string;
  admitBy: string;
  status: SurgeryStatus;
  theatre: NamedRef;
  surgeon: NamedRef;
  assistant: NamedRef;
  cat: { id: string; name: string; isHighRisk: boolean; isDeceased: boolean };
  customer: { id: string; name: string; phone: string | null };
  procedure: string;
  checklist: ChecklistSummary;
  consentRecorded: boolean;
  needsRescheduling: boolean;
  recoveryStatus: RecoveryStatus | null;
}

export interface SurgeryDayList {
  date: string;
  branchTimeZoneId: string;
  surgeries: SurgeryDayItem[];
}

// ---- detail ----------------------------------------------------------------------------------

export interface ChecklistItem {
  id: string;
  kind: ChecklistKind;
  status: ChecklistStatus;
  completedBy: string | null;
  completedOn: string | null;
  note: string | null;
  waiverReason: string | null;
  referenceId: string | null;
}

export interface StartBlocker {
  code: string;
  message: string;
}

export interface SurgeryStatusChange {
  from: string | null;
  to: string;
  changedOn: string;
  changedBy: string;
}

export interface SurgeryRecoveryChange {
  from: string | null;
  to: string;
  changedOn: string;
  changedBy: string;
  changedByName: string | null;
  note: string | null;
}

export interface SurgeryFollowUp {
  id: string;
  kind: SurgeryFollowUpKind;
  dueOn: string;
  windowFrom: string;
  windowTo: string;
  /** `Pending`, `Done` or `Cancelled`. */
  status: string;
  closedOn: string | null;
}

export interface SurgeryDetail {
  id: string;
  version: number;
  branchId: string;
  branchTimeZoneId: string;
  status: SurgeryStatus;
  catId: string;
  catName: string;
  catIsDeceased: boolean;
  customerId: string;
  customerName: string;
  customerPhone: string | null;
  visitId: string;
  appointmentId: string;
  procedure: { name: string; category: string; durationMinutes: number; isSterilisation: boolean };
  priceSnapshot: {
    amount: Money;
    basis: 'Standard' | 'Campaign';
    campaignId: string | null;
    campaignEligibilityNote: string | null;
  };
  theatre: NamedRef;
  surgeon: NamedRef;
  assistant: NamedRef;
  startsOn: string;
  endsOn: string;
  admitBy: string;
  fastingInstructions: string;
  asaGrade: AsaGrade | null;
  asaIsEmergency: boolean;
  consentOverride: {
    overriddenBy: string;
    overriddenByName: string | null;
    overriddenOn: string;
    reason: string;
  } | null;
  admittedOn: string | null;
  procedureStartedOn: string | null;
  procedureEndedOn: string | null;
  dischargedOn: string | null;
  outcome: ProcedureOutcome | null;
  recoveryStatus: RecoveryStatus | null;
  postOpInstructions: string | null;
  cancelledOn: string | null;
  cancellationReason: string | null;
  checklist: ChecklistItem[];
  checklistSummary: ChecklistSummary;
  consentRecorded: boolean;
  startBlockers: StartBlocker[];
  allowedTransitions: SurgeryAction[];
  statusHistory: SurgeryStatusChange[];
  recoveryHistory: SurgeryRecoveryChange[];
  followUps: SurgeryFollowUp[];
  /** `None`, `Draft` or `Finalised`. */
  reportStatus: 'None' | 'Draft' | 'Finalised';
  neuterStatusApplied: boolean | null;
  latestWeight: { kilograms: number; recordedOn: string } | null;
}

export interface SurgerySummary {
  id: string;
  startsOn: string;
  endsOn: string;
  status: SurgeryStatus;
  catId: string;
  catName: string;
  procedure: string;
  pricingBasis: string;
  outcome: string | null;
  surgeonId: string;
  surgeonName: string;
  reportOutstanding: boolean;
  branchTimeZoneId: string;
}

export interface SurgeryListParams {
  from?: string;
  to?: string;
  status?: string;
  surgeonId?: string;
  pricingBasis?: string;
  reportOutstanding?: boolean;
  pageNumber?: number;
  pageSize?: number;
}

// ---- recovery and discharge -----------------------------------------------------------------

export interface RecoveryBoardEntry {
  surgeryId: string;
  catId: string;
  catName: string;
  catIsHighRisk: boolean;
  customerName: string;
  customerPhone: string | null;
  procedure: string;
  procedureEndedOn: string | null;
  recoveryStatus: RecoveryStatus;
  statusChangedOn: string | null;
  statusChangedBy: string | null;
  statusChangedByName: string | null;
}

export interface RecoveryBoard {
  generatedOn: string;
  entries: RecoveryBoardEntry[];
}

export interface DischargeDraftFollowUp {
  kind: SurgeryFollowUpKind;
  dueOn: string;
  windowFrom: string;
  windowTo: string;
}

export interface DischargeDraft {
  surgeryId: string;
  status: SurgeryStatus;
  recoveryStatus: RecoveryStatus | null;
  catIsDeceased: boolean;
  postOpInstructions: string;
  followUps: DischargeDraftFollowUp[];
}

export interface DischargeRequest {
  postOpInstructions?: string;
  followUps?: Array<{ kind: SurgeryFollowUpKind; dueOn: string }>;
}

// ---- requests --------------------------------------------------------------------------------

const base = '/api/staff/surgeries';

export function getSurgeryServices(): Promise<SurgeryService[]> {
  return apiFetch(`${base}/services`);
}

export function getSurgeryStaff(serviceId: string): Promise<SurgeryStaff> {
  return apiFetch(`${base}/staff${queryString({ serviceId })}`);
}

export function getSurgeryAvailability(params: AvailabilityParams): Promise<SurgeryAvailability> {
  return apiFetch(`${base}/availability${queryString({ ...params })}`);
}

export function bookSurgery(body: BookSurgeryRequest): Promise<{ surgeryId: string }> {
  return apiFetch(base, { method: 'POST', body });
}

export function listSurgeries(params: SurgeryListParams): Promise<PagedResult<SurgerySummary>> {
  return apiFetch(`${base}${queryString({ ...params })}`);
}

export function getSurgeryDay(date: string): Promise<SurgeryDayList> {
  return apiFetch(`${base}/day${queryString({ date })}`);
}

export function getSurgery(id: string): Promise<SurgeryDetail> {
  return apiFetch(`${base}/${encodeURIComponent(id)}`);
}

export function rescheduleSurgery(
  id: string,
  body: RescheduleSurgeryRequest,
): Promise<{ surgery: SurgeryDetail }> {
  return apiFetch(`${base}/${encodeURIComponent(id)}/reschedule`, { method: 'POST', body });
}

export function cancelSurgery(id: string, reason: string): Promise<void> {
  return apiFetch(`${base}/${encodeURIComponent(id)}/cancel`, { method: 'POST', body: { reason } });
}

export function completeChecklistItem(
  id: string,
  kind: ChecklistKind,
  body: { note?: string; weightRecordId?: string },
): Promise<void> {
  return apiFetch(
    `${base}/${encodeURIComponent(id)}/checklist/${encodeURIComponent(kind)}/complete`,
    { method: 'POST', body },
  );
}

export function waiveChecklistItem(id: string, kind: ChecklistKind, reason: string): Promise<void> {
  return apiFetch(`${base}/${encodeURIComponent(id)}/checklist/${encodeURIComponent(kind)}/waive`, {
    method: 'POST',
    body: { reason },
  });
}

export function recordAsaGrade(
  id: string,
  body: { grade: AsaGrade; isEmergency: boolean },
): Promise<void> {
  return apiFetch(`${base}/${encodeURIComponent(id)}/asa-grade`, { method: 'PUT', body });
}

export function admitSurgery(id: string): Promise<void> {
  return apiFetch(`${base}/${encodeURIComponent(id)}/admit`, { method: 'POST' });
}

/** The ordinary start carries no body, so it can never carry an override (the server ignores one). */
export function startSurgery(id: string): Promise<void> {
  return apiFetch(`${base}/${encodeURIComponent(id)}/start`, { method: 'POST' });
}

export function startSurgeryWithConsentOverride(
  id: string,
  reason: string,
): Promise<{ consentWasOverridden: boolean; overrideIgnored: boolean } | undefined> {
  return apiFetch(`${base}/${encodeURIComponent(id)}/start-with-consent-override`, {
    method: 'POST',
    body: { reason },
  });
}

export function endProcedure(id: string, outcome: ProcedureOutcome): Promise<void> {
  return apiFetch(`${base}/${encodeURIComponent(id)}/end-procedure`, {
    method: 'POST',
    body: { outcome },
  });
}

export function applySterilisationOutcome(id: string): Promise<void> {
  return apiFetch(`${base}/${encodeURIComponent(id)}/apply-sterilisation-outcome`, {
    method: 'POST',
  });
}

export function setRecoveryStatus(
  id: string,
  body: { status: RecoveryStatus; note?: string },
): Promise<void> {
  return apiFetch(`${base}/${encodeURIComponent(id)}/recovery-status`, { method: 'PUT', body });
}

export function getRecoveryBoard(): Promise<RecoveryBoard> {
  return apiFetch(`${base}/recovery-board`);
}

export function getDischargeDraft(id: string): Promise<DischargeDraft> {
  return apiFetch(`${base}/${encodeURIComponent(id)}/discharge-draft`);
}

export function dischargeSurgery(
  id: string,
  body: DischargeRequest,
): Promise<{ surgery: SurgeryDetail }> {
  return apiFetch(`${base}/${encodeURIComponent(id)}/discharge`, { method: 'POST', body });
}

export function completeFollowUp(id: string, followUpId: string): Promise<void> {
  return apiFetch(
    `${base}/${encodeURIComponent(id)}/follow-ups/${encodeURIComponent(followUpId)}/complete`,
    { method: 'POST' },
  );
}

export function cancelFollowUp(id: string, followUpId: string, reason: string): Promise<void> {
  return apiFetch(
    `${base}/${encodeURIComponent(id)}/follow-ups/${encodeURIComponent(followUpId)}/cancel`,
    {
      method: 'POST',
      body: { reason },
    },
  );
}
