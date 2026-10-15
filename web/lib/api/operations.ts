import { apiFetch } from './client';
import type { PagedResult } from './types';
import type { PageParams } from './retention';

/** `lib/api/operations.ts` — HO-XCT-002 §8.2. Backup health, restore tests and the web-vitals
 *  summary. Shapes verified against `AdminOperationsController.cs`, `AdminWebVitalsController.cs`
 *  and `src/Read/.../ViewModels/Operations{Status,WebVital}ViewModels.cs`. */

export interface BackupRunStatus {
  finishedOn: string;
  succeeded: boolean;
  repository: string;
  label: string | null;
  sizeBytes: number | null;
}

export type RestoreTestOutcome = 'Passed' | 'Failed';

export interface LastRestoreTest {
  id: string;
  performedOn: string;
  outcome: RestoreTestOutcome;
  achievedRpoMinutes: number;
  achievedRtoMinutes: number;
}

export interface BackupStatus {
  lastFull: BackupRunStatus | null;
  lastDiff: BackupRunStatus | null;
  lastArchiveCheck: BackupRunStatus | null;
  lastErasureJournal: BackupRunStatus | null;
  walArchivedUntil: string | null;
  isBackupStale: boolean;
  isArchiveStale: boolean;
  lastRestoreTest: LastRestoreTest | null;
  lastPassedRestoreTestOn: string | null;
  nextRestoreTestDueOn: string;
  isRestoreTestOverdue: boolean;
  objectives: { rpoMinutes: number; rtoMinutes: number };
}

export const getBackupStatus = () => apiFetch<BackupStatus>('/api/admin/operations/backup-status');

export interface RestoreTestSummary {
  id: string;
  performedOn: string;
  outcome: RestoreTestOutcome;
  achievedRpoMinutes: number;
  achievedRtoMinutes: number;
  environment: string;
  backupSetLabel: string;
  supersedesId: string | null;
}

export interface RestoreCheck {
  name: string;
  passed: boolean;
  detail: string | null;
}

export interface RestoreTest {
  id: string;
  performedOn: string;
  performedByStaffId: string;
  environment: string;
  backupSetLabel: string;
  simulatedFailureAt: string;
  recoveredTo: string;
  restoreStartedAt: string;
  restoreCompletedAt: string;
  achievedRpoMinutes: number;
  achievedRtoMinutes: number;
  outcome: RestoreTestOutcome;
  checks: RestoreCheck[];
  /** Text, to be shown as text and never as markup. */
  drillOutput: string;
  notes: string | null;
  supersedesId: string | null;
  supersededByIds: string[];
}

export function getRestoreTests(params: PageParams) {
  const query = new URLSearchParams();
  if (params.pageNumber) query.set('pageNumber', String(params.pageNumber));
  if (params.pageSize) query.set('pageSize', String(params.pageSize));
  const text = query.toString();
  return apiFetch<PagedResult<RestoreTestSummary>>(
    `/api/admin/operations/restore-tests${text ? `?${text}` : ''}`,
  );
}

export const getRestoreTest = (id: string) =>
  apiFetch<RestoreTest>(`/api/admin/operations/restore-tests/${id}`);

/** No outcome, RPO, RTO or performer: the server computes the first three (HO-XCT-001 ADR-313)
 *  and takes the performer from the signed-in caller. */
export interface RecordRestoreTestRequest {
  performedOn: string;
  environment: string;
  backupSetLabel: string;
  simulatedFailureAt: string;
  recoveredTo: string;
  restoreStartedAt: string;
  restoreCompletedAt: string;
  checks: RestoreCheck[];
  drillOutput: string;
  notes?: string;
  supersedesId?: string;
}

export interface RecordRestoreTestResult {
  restoreTestId: string;
  outcome: RestoreTestOutcome;
  achievedRpoMinutes: number;
  achievedRtoMinutes: number;
}

export const recordRestoreTest = (request: RecordRestoreTestRequest) =>
  apiFetch<RecordRestoreTestResult>('/api/admin/operations/restore-tests', {
    method: 'POST',
    body: request,
  });

export type WebVitalMetric = 'LCP' | 'INP' | 'CLS' | 'FCP' | 'TTFB';
export type WebVitalVerdict = 'Pass' | 'Fail' | 'InsufficientData';

export interface WebVitalSummaryRow {
  routeTemplate: string;
  viewportClass: 'Narrow' | 'Medium' | 'Wide';
  metric: WebVitalMetric;
  sampleCount: number;
  p75: number;
  verdict: WebVitalVerdict;
}

export interface WebVitalsSummary {
  from: string;
  to: string;
  budgets: { lcpMs: number; inpMs: number; cls: number };
  rows: WebVitalSummaryRow[];
}

export interface WebVitalsSummaryParams {
  /** `yyyy-MM-dd`, required, range at most 93 days. */
  from: string;
  to: string;
  routeTemplate?: string;
  viewportClass?: string;
}

export function getWebVitalsSummary(params: WebVitalsSummaryParams) {
  const query = new URLSearchParams({ from: params.from, to: params.to });
  if (params.routeTemplate) query.set('routeTemplate', params.routeTemplate);
  if (params.viewportClass) query.set('viewportClass', params.viewportClass);
  return apiFetch<WebVitalsSummary>(`/api/admin/web-vitals/summary?${query.toString()}`);
}
