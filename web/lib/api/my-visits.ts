import { apiFetch, getApiUrl } from './client';
import type { DischargeSummary } from './visits';
import type { DiagnosticAttachment, DiagnosticResultSet } from './diagnostics';

/**
 * `lib/api/my-visits.ts` — HO-EMR-002 §8.2 (US-CLN-16, FR-POR-3). The owner's reads
 * (`CustomerVisitsController`). A visit that is open, or not the caller's cat's, is a `404` —
 * the same neutral answer either way.
 */

export interface MyVisitListItem {
  id: string;
  date: string;
  service: string;
  vet: string;
  problems: string[];
  hasSummary: boolean;
}

export interface MyLabResult {
  orderId: string;
  visitId: string;
  kind: string;
  orderedOn: string;
  resultSets: DiagnosticResultSet[];
  attachments: DiagnosticAttachment[];
}

export function getMyCatVisits(catId: string): Promise<MyVisitListItem[]> {
  return apiFetch(`/api/customers/me/cats/${catId}/visits`);
}

export function getMyVisitSummary(visitId: string): Promise<DischargeSummary> {
  return apiFetch(`/api/customers/me/visits/${visitId}`);
}

export function getMyLabResults(catId: string): Promise<MyLabResult[]> {
  return apiFetch(`/api/customers/me/cats/${catId}/lab-results`);
}

export function getMyVisitSummaryPdfUrl(visitId: string): string {
  return getApiUrl(`/api/customers/me/visits/${visitId}/discharge-summary.pdf`);
}
