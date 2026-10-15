import { apiFetch } from './client';
import { resolveStoredFile, type StoredFileViewModel } from './files';
import type { PagedResult } from './types';
import type { VitalsEntry } from './visits';

/**
 * `lib/api/clinical-history.ts` — HO-EMR-002 §8.2 (US-CLN-13, ADR-FE-056). The summary is one call;
 * a section's first page is fetched only when its tab opens. Typed against
 * `ClinicalHistoryViewModels.cs` and `IClinicalHistorySectionProvider.cs` on master.
 */

/** `ClinicalHistorySections` keys, in the order the server declares them. */
export type HistorySectionKey =
  | 'timeline'
  | 'visits'
  | 'diagnoses'
  | 'lab-results'
  | 'photos'
  | 'vaccinations'
  | 'parasite-treatments'
  | 'weights'
  | 'prescriptions'
  | 'boarding-stays'
  | 'food-purchases';

export interface HistorySectionSummary {
  key: HistorySectionKey;
  available: boolean;
  /** The owning area's handover id on the wire — NEVER shown (the copy comes from the section key). */
  owner: string;
  count: number;
}

/** One item of a section. `fields` carries section-specific plain text; `file` a signed URL. */
export interface HistoryItem {
  id: string;
  kind: string;
  occurredOn: string;
  title: string;
  detail: string | null;
  fields: Record<string, string | null>;
  file: StoredFileViewModel | null;
}

export interface ClinicalHistorySummary {
  cat: {
    id: string;
    name: string;
    sex: string;
    dateOfBirth: string | null;
    breed: string | null;
    isHighRisk: boolean;
    lifeStatus: string;
  };
  activeProblems: Array<{
    diagnosisCodeId: string;
    code: string;
    name: string;
    certainty: string;
    lastSeen: string;
  }>;
  recentVisits: HistoryItem[];
  lastVitals: VitalsEntry | null;
  weightSeries: Array<{ recordedOn: string; kilograms: number }>;
  vaccinationStatus: {
    catId: string;
    overallStatus: string;
    earliestNextDueOn: string | null;
    bookingEligible: boolean;
    vaccines: Array<{
      vaccineId: string;
      vaccineCode: string;
      vaccineName: string;
      status: string;
      nextDueOn: string | null;
      reason: string | null;
    }>;
  } | null;
  pendingDiagnostics: Array<{
    id: string;
    visitId: string;
    catId: string;
    catName: string;
    kind: string;
    status: string;
    orderedOn: string;
    orderedBy: string;
  }>;
  sections: HistorySectionSummary[];
}

export interface HistorySectionPage extends PagedResult<HistoryItem> {
  key: HistorySectionKey;
  available: boolean;
  owner: string;
}

export function getClinicalHistory(catId: string): Promise<ClinicalHistorySummary> {
  return apiFetch(`/api/staff/cats/${catId}/clinical-history`);
}

export async function getHistorySection(
  catId: string,
  section: HistorySectionKey,
  pageNumber: number,
  pageSize = 20,
): Promise<HistorySectionPage> {
  const page = await apiFetch<HistorySectionPage>(
    `/api/staff/cats/${catId}/clinical-history/${section}?pageNumber=${pageNumber}&pageSize=${pageSize}`,
  );
  // The API signs a path relative to its own origin -- anchor it (FIL backlog 25).
  return { ...page, items: page.items.map((item) => ({ ...item, file: resolveStoredFile(item.file) })) };
}

export type { StoredFileViewModel };
