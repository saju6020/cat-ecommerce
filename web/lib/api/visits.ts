import {
  ApiError,
  ApiForbiddenError,
  ApiNotFoundError,
  ApiUnauthorizedError,
  apiFetch,
  getApiUrl,
  notifySessionError,
} from './client';
import { getActiveBranchId } from '../branch';
import type { AppointmentStatus } from './types';
import { resolveFileUrl } from './files';

/**
 * `lib/api/visits.ts` — HO-EMR-002 §8.2. The visit and consultation endpoints, typed against the
 * REAL API on master (`StaffVisitsController`, `Read/ViewModels/VisitViewModels.cs`), which is the
 * contract HO-EMR-001 §12.4 describes with the amendments recorded in HO-EMR-002's report:
 *  - a visit of another branch is a `404` (never a `403`);
 *  - `treatments` is always `[]` and there is no dispensary endpoint (Phase F is not built);
 *  - a photo's `thumbnailUrl` equals its `url` (no server-side thumbnail);
 *  - vitals `measuredOn` is normalised to UTC by the server.
 * Enum-shaped strings are the C# `Enumeration.DisplayName` values.
 */

export interface Measurement {
  /** The value as a string at the sign's stated precision (UI-8) — never re-formatted client-side. */
  value: string;
  unit: string;
}

export type VisitStatus = 'Open' | 'Finalised';

/** `VisitAllowedActions` — what this caller may do with this visit right now (ADR-FE-051). */
export type VisitAction =
  | 'recordVitals'
  | 'editNote'
  | 'orderDiagnostic'
  | 'attachPhoto'
  | 'setFollowUp'
  | 'finalise'
  | 'addAddendum';

export type ProblemCertainty = 'Provisional' | 'Confirmed';
export type FollowUpKind = 'Appointment' | 'Reminder';
/** `VitalSign` display names — what `confirmedOutOfRange` and `outOfRange[].sign` carry. */
export type VitalSign =
  'Weight' | 'Temperature' | 'HeartRate' | 'RespiratoryRate' | 'BodyConditionScore' | 'PainScore';

export interface VisitAppointmentRef {
  id: string;
  status: AppointmentStatus;
  startsOn: string | null;
  performerName: string | null;
}

export interface VisitNote {
  subjective: string;
  objective: string;
  assessment: string;
  plan: string;
  dischargeInstructions: string;
  savedOn: string | null;
  savedBy: string | null;
  appliedTemplateId: string | null;
  appliedTemplateVersion: number | null;
}

export interface VisitProblem {
  id: string;
  diagnosisCodeId: string;
  code: string;
  name: string;
  certainty: ProblemCertainty;
  note: string | null;
}

export interface VitalsEntry {
  id: string;
  measuredOn: string;
  weight: Measurement | null;
  temperature: Measurement | null;
  heartRate: Measurement | null;
  respiratoryRate: Measurement | null;
  bodyConditionScore: Measurement | null;
  painScore: Measurement | null;
  confirmedOutOfRange: VitalSign[];
  note: string | null;
  recordedBy: string;
  recordedOn: string;
}

export interface VisitPhoto {
  id: string;
  takenOn: string;
  note: string | null;
  addedOn: string;
  /** Short-lived signed URLs (5 min) — only ever inside this payload (U-X4). */
  url: string | null;
  thumbnailUrl: string | null;
  contentType: string | null;
  urlExpiresOn: string | null;
}

export interface VisitFollowUp {
  kind: FollowUpKind;
  dueOn: string;
  appointmentId: string | null;
  setOn: string;
}

export interface VisitAddendum {
  id: string;
  text: string;
  reason: string | null;
  author: string;
  addedOn: string;
}

export interface VisitDiagnosticSummary {
  id: string;
  kind: string;
  status: string;
  orderedOn: string;
  resultSetCount: number;
  hasAbnormal: boolean;
}

export interface VisitTemplate {
  id: string;
  version: number;
  name: string;
  subjective: string;
  objective: string;
  assessment: string;
  plan: string;
  dischargeInstructions: string;
}

/** The caller's own auto-saved working copy — only while open and only when newer than the note. */
export interface VisitDraft {
  subjective: string;
  objective: string;
  assessment: string;
  plan: string;
  dischargeInstructions: string;
  problemCodeIds: string[];
  savedOn: string;
}

export interface VisitDetail {
  id: string;
  version: number;
  status: VisitStatus;
  catId: string;
  catName: string;
  appointment: VisitAppointmentRef;
  service: { id: string; name: string; category: string };
  openedOn: string;
  openedBy: string;
  branchTimeZoneId: string;
  allowedActions: VisitAction[];
  note: VisitNote;
  problems: VisitProblem[];
  vitals: VitalsEntry[];
  /** Always `[]` until Phase F (treatments and stock) is built — not rendered. */
  treatments: unknown[];
  photos: VisitPhoto[];
  followUp: VisitFollowUp | null;
  addenda: VisitAddendum[];
  diagnostics: VisitDiagnosticSummary[];
  template: VisitTemplate | null;
  draft: VisitDraft | null;
  finalisedOn: string | null;
  finalisedBy: string | null;
}

export interface OpenVisitListItem {
  id: string;
  appointmentId: string;
  cat: { id: string; name: string; isHighRisk: boolean };
  service: string;
  openedOn: string;
  status: VisitStatus;
  hasVitals: boolean;
  hasNote: boolean;
}

export interface VisitReference {
  visitId: string | null;
  status: VisitStatus | null;
}

/** `GET /api/staff/clinical/vital-sign-rules` — the server's range table (§8.3). */
export interface VitalSignRule {
  sign: VitalSign;
  unit: string;
  absoluteMin: string;
  absoluteMax: string;
  plausibleMin: string | null;
  plausibleMax: string | null;
  decimalPlaces: number;
}

export interface RecordVitalsRequest {
  /** ISO 8601 WITH an offset — the server normalises to UTC. */
  measuredOn?: string;
  weightKg?: number;
  temperatureC?: number;
  heartRateBpm?: number;
  respiratoryRatePerMin?: number;
  bodyConditionScore?: number;
  painScore?: number;
  confirmedOutOfRange?: VitalSign[];
  note?: string;
}

/** `400 VitalsOutsidePlausibleRange` carries one of these per unconfirmed sign. */
export interface OutOfRangeSign {
  sign: VitalSign;
  value: string;
  unit: string;
  plausibleMin: string;
  plausibleMax: string;
}

export interface SoapPayload {
  subjective: string;
  objective: string;
  assessment: string;
  plan: string;
  dischargeInstructions: string;
}

export interface DraftRequest extends SoapPayload {
  problemCodeIds: string[];
}

export interface ProblemRequest {
  problemId?: string;
  diagnosisCodeId: string;
  certainty: ProblemCertainty;
  note?: string;
}

export interface SaveNoteRequest extends SoapPayload {
  expectedVersion: number;
  problems: ProblemRequest[];
  appliedTemplateId?: string;
  appliedTemplateVersion?: number;
}

export interface SetFollowUpRequest {
  kind: FollowUpKind;
  dueOn: string;
  appointmentId?: string;
}

const base = '/api/staff';

export function openVisit(appointmentId: string): Promise<{ visitId: string }> {
  return apiFetch(`${base}/visits`, { method: 'POST', body: { appointmentId } });
}

export function getVisitByAppointment(appointmentId: string): Promise<VisitReference> {
  return apiFetch(`${base}/appointments/${appointmentId}/visit`);
}

export function getOpenVisits(status: VisitStatus = 'Open'): Promise<OpenVisitListItem[]> {
  return apiFetch(`${base}/visits?status=${status}`);
}

export async function getVisit(visitId: string): Promise<VisitDetail> {
  const visit = await apiFetch<VisitDetail>(`${base}/visits/${visitId}`);
  // Photo `url`/`thumbnailUrl` are signed relative to the API origin -- anchor them (FIL backlog 25).
  return {
    ...visit,
    photos: visit.photos.map((photo) => ({
      ...photo,
      url: resolveFileUrl(photo.url),
      thumbnailUrl: resolveFileUrl(photo.thumbnailUrl),
    })),
  };
}

export function getVitalSignRules(): Promise<VitalSignRule[]> {
  return apiFetch(`${base}/clinical/vital-sign-rules`);
}

export function recordVitals(
  visitId: string,
  request: RecordVitalsRequest,
): Promise<{ vitalsEntryId: string }> {
  return apiFetch(`${base}/visits/${visitId}/vitals`, { method: 'POST', body: request });
}

/**
 * ADR-FE-050 — the working-copy auto-save. `keepalive` lets the request outlive the page when it is
 * sent on `visibilitychange`/route change (body well under the 64 KB keepalive cap for a note).
 */
export function saveDraft(
  visitId: string,
  request: DraftRequest,
  options: { keepalive?: boolean } = {},
): Promise<void> {
  return apiFetch(`${base}/visits/${visitId}/draft`, {
    method: 'PUT',
    body: request,
    ...(options.keepalive ? { keepalive: true } : {}),
  });
}

export function saveNote(
  visitId: string,
  request: SaveNoteRequest,
): Promise<{ version: number; savedOn: string }> {
  return apiFetch(`${base}/visits/${visitId}/note`, { method: 'PUT', body: request });
}

export function finaliseVisit(visitId: string, expectedVersion: number): Promise<void> {
  return apiFetch(`${base}/visits/${visitId}/finalise`, {
    method: 'POST',
    body: { expectedVersion },
  });
}

export function addAddendum(
  visitId: string,
  request: { text: string; reason?: string },
): Promise<{ addendumId: string }> {
  return apiFetch(`${base}/visits/${visitId}/addenda`, { method: 'POST', body: request });
}

export function attachPhoto(
  visitId: string,
  request: { fileId: string; takenOn: string; note?: string },
): Promise<{ photoId: string }> {
  return apiFetch(`${base}/visits/${visitId}/photos`, { method: 'POST', body: request });
}

export function setFollowUp(visitId: string, request: SetFollowUpRequest): Promise<void> {
  return apiFetch(`${base}/visits/${visitId}/follow-up`, { method: 'PUT', body: request });
}

export function clearFollowUp(visitId: string): Promise<void> {
  return apiFetch(`${base}/visits/${visitId}/follow-up`, { method: 'DELETE' });
}

// ---- discharge summary (staff) ------------------------------------------------------------

export interface DischargeSummary {
  visitId: string;
  branchName: string;
  branchAddress: string;
  branchPhone: string | null;
  branchTimeZoneId: string;
  catId: string;
  catName: string;
  catSex: string;
  catAge: string;
  weightAtVisit: Measurement | null;
  visitDate: string;
  attendingVet: string;
  problems: Array<{ name: string; code: string; certainty: ProblemCertainty }>;
  treatments: string[];
  medicinesAdministered: string[];
  homeCareInstructions: string;
  followUp: { kind: FollowUpKind; dueOn: string; appointmentStartsOn: string | null } | null;
  corrections: Array<{ addedOn: string; text: string; reason: string | null }>;
  extraSections: Array<{ title: string; lines: string[] }>;
  prescriptionsAvailable: boolean;
}

export function getDischargeSummary(visitId: string): Promise<DischargeSummary> {
  return apiFetch(`${base}/visits/${visitId}/discharge-summary`);
}

// ---- binary reads (staff PDF, consent signature) -------------------------------------------

/**
 * A staff GET that returns BYTES (the discharge-summary PDF, a consent's signature SVG). A plain
 * `<a href>` / `<img src>` cannot send `X-Branch-Id`, so a multi-branch caller would be answered for
 * the wrong branch (or refused); this sends it, with the session cookie, and maps failures exactly as
 * `apiFetch` does (`401` -> the session handler, `403`, `404`, anything else a plain `ApiError`).
 * GET needs no CSRF token. The caller turns the blob into an object URL and revokes it.
 */
export async function fetchStaffBlob(path: string): Promise<Blob> {
  const headers: Record<string, string> = {};
  const branchId = getActiveBranchId();
  if (branchId) headers['X-Branch-Id'] = branchId;
  let response: Response;
  try {
    response = await fetch(getApiUrl(path), { credentials: 'include', headers });
  } catch {
    throw new ApiError('Could not reach the server. Please try again.', 0);
  }
  if (response.ok) return response.blob();
  if (response.status === 404) throw new ApiNotFoundError();
  if (response.status === 403) {
    throw new ApiForbiddenError((await response.json().catch(() => null)) ?? {});
  }
  if (response.status === 401) {
    const body = (await response.json().catch(() => null)) ?? { error: 'session_invalid' };
    notifySessionError(body);
    throw new ApiUnauthorizedError(body);
  }
  throw new ApiError('Something went wrong. Please try again.', response.status);
}

export function getDischargeSummaryPdf(visitId: string): Promise<Blob> {
  return fetchStaffBlob(`${base}/visits/${visitId}/discharge-summary.pdf`);
}
