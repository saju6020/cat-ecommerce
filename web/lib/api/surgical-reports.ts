import { apiFetch } from './client';

/**
 * `lib/api/surgical-reports.ts` — HO-SRG-002 §8.2. Built to `StaffSurgeryReportsController`,
 * `SurgicalReportRequests.cs` and `SurgicalReportViewModels.cs`. A finalised report is immutable: a
 * `PUT` after finalisation is `409 ReportNotEditable` and the only addition is an addendum
 * (ADR-FE-074). `GET` answers `404` while no draft exists yet (the first `PUT` creates it).
 */

export interface ReportAddendum {
  id: string;
  text: string;
  addedBy: string;
  addedByName: string;
  addedOn: string;
}

export interface SurgicalReport {
  surgeryId: string;
  catId: string;
  catName: string;
  visitId: string;
  branchId: string;
  branchTimeZoneId: string;
  version: number;
  /** `Draft` or `Finalised`. */
  status: 'Draft' | 'Finalised';
  procedurePerformed: string;
  findings: string;
  technique: string;
  sutureMaterial: string;
  hadComplications: boolean;
  complications: string | null;
  surgeonId: string;
  surgeonName: string;
  assistantId: string;
  assistantName: string;
  finalisedOn: string | null;
  finalisedBy: string | null;
  finalisedByName: string | null;
  addenda: ReportAddendum[];
}

export interface SaveReportDraftRequest {
  /** Omitted on the very first save (no draft yet). */
  expectedVersion?: number;
  procedurePerformed: string;
  findings: string;
  technique: string;
  sutureMaterial: string;
  hadComplications: boolean;
  complications?: string;
  surgeonId: string;
  assistantId: string;
}

const base = (surgeryId: string) => `/api/staff/surgeries/${encodeURIComponent(surgeryId)}/report`;

export function getSurgicalReport(surgeryId: string): Promise<SurgicalReport> {
  return apiFetch(base(surgeryId));
}

export function saveReportDraft(
  surgeryId: string,
  body: SaveReportDraftRequest,
): Promise<{ version: number }> {
  return apiFetch(base(surgeryId), { method: 'PUT', body });
}

export function finaliseReport(surgeryId: string, expectedVersion: number): Promise<void> {
  return apiFetch(`${base(surgeryId)}/finalise`, { method: 'POST', body: { expectedVersion } });
}

export function addReportAddendum(
  surgeryId: string,
  text: string,
): Promise<{ addendumId: string }> {
  return apiFetch(`${base(surgeryId)}/addenda`, { method: 'POST', body: { text } });
}
