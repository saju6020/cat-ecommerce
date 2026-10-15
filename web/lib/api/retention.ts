import { apiFetch } from './client';
import type { PagedResult } from './types';

/** `lib/api/retention.ts` — HO-XCT-002 §8.2. Shapes verified against `AdminRetentionController.cs`,
 *  `AdminCustomerErasureController.cs` and `src/Read/.../ViewModels/Operations{Retention,Erasure}
 *  ViewModels.cs` (HO-XCT-001 section 12.4). */

export type RetentionActionAtEnd =
  | 'ReviewOnly'
  | 'Purge'
  | 'Redact'
  | 'AnonymiseOnRequest'
  | 'Keep';

export interface RetentionScheduleTable {
  schema: string;
  table: string;
  hardDeletePolicy: string;
  basis: string;
}

export interface RetentionScheduleClass {
  recordClass: string;
  hardDeletePolicies: string[];
  bases: string[];
  /** Null where the class has no period (kept indefinitely, or erased on request). */
  effectivePeriod: number | null;
  periodUnit: 'years' | 'months' | 'days' | null;
  actionAtEnd: RetentionActionAtEnd;
  tableCount: number;
  tables: RetentionScheduleTable[];
}

export interface RetentionSchedule {
  classes: RetentionScheduleClass[];
}

export const getRetentionSchedule = () =>
  apiFetch<RetentionSchedule>('/api/admin/retention/schedule');

export type RetentionRunStatus = 'Requested' | 'Running' | 'Succeeded' | 'CompletedWithErrors';

export interface RetentionRunSummary {
  retentionRunId: string;
  trigger: 'Scheduled' | 'Manual';
  status: RetentionRunStatus;
  requestedOn: string;
  startedOn: string | null;
  finishedOn: string | null;
  passCount: number;
  totalAffected: number;
  totalFailed: number;
}

export interface RetentionPass {
  kind: string;
  cutoff: string | null;
  examined: number;
  affected: number;
  held: number;
  failed: number;
  errorCodes: string[];
  durationMs: number;
}

export interface RetentionRun {
  retentionRunId: string;
  trigger: 'Scheduled' | 'Manual';
  status: RetentionRunStatus;
  requestedOn: string;
  startedOn: string | null;
  finishedOn: string | null;
  passes: RetentionPass[];
}

export interface PageParams {
  pageNumber?: number;
  pageSize?: number;
}

function pageQuery(params: PageParams, extra: Record<string, string | undefined> = {}): string {
  const query = new URLSearchParams();
  if (params.pageNumber) query.set('pageNumber', String(params.pageNumber));
  if (params.pageSize) query.set('pageSize', String(params.pageSize));
  for (const [key, value] of Object.entries(extra)) if (value) query.set(key, value);
  const text = query.toString();
  return text ? `?${text}` : '';
}

export const getRetentionRuns = (params: PageParams) =>
  apiFetch<PagedResult<RetentionRunSummary>>(`/api/admin/retention/runs${pageQuery(params)}`);

export const getRetentionRun = (runId: string) =>
  apiFetch<RetentionRun>(`/api/admin/retention/runs/${runId}`);

export const requestRetentionRun = () =>
  apiFetch<{ retentionRunId: string; status: RetentionRunStatus }>('/api/admin/retention/runs', {
    method: 'POST',
  });

export interface RetentionReviewRow {
  recordClass: string;
  recordKind: string;
  count: number;
  oldestAnchor: string | null;
  sourceKey: string;
}

export interface RetentionReview {
  asOf: string;
  rows: RetentionReviewRow[];
  note: string;
}

export const getRetentionReview = () => apiFetch<RetentionReview>('/api/admin/retention/review');

export type ErasureRequestStatus = 'Open' | 'Completed';
export type ErasureScopeStatus = 'Pending' | 'Held' | 'Completed' | 'Failing';
export type ErasureRequestSource = 'SelfService' | 'StaffOnBehalf' | 'RestoreReconciliation';

export interface ErasureScope {
  contextKey: string;
  status: ErasureScopeStatus;
  heldUntil: string | null;
  basis: string | null;
  attempts: number;
  lastErrorCode: string | null;
  completedOn: string | null;
}

export interface ErasureRequest {
  customerAccountId: string;
  requestedOn: string;
  source: ErasureRequestSource;
  status: ErasureRequestStatus;
  completedOn: string | null;
  scopes: ErasureScope[];
}

export const getErasureRequests = (params: PageParams & { status?: ErasureRequestStatus }) =>
  apiFetch<PagedResult<ErasureRequest>>(
    `/api/admin/retention/erasure-requests${pageQuery(params, { status: params.status })}`,
  );

export const getErasureRequest = (customerAccountId: string) =>
  apiFetch<ErasureRequest>(`/api/admin/retention/erasure-requests/${customerAccountId}`);

export interface ReapplyRequest {
  customerAccountIds: string[];
  restoreReference: string;
}

/** `failed` is delivered by the API (`ReapplyErasuresResult`) but absent from HO-XCT-001 section
 *  12.4's four-count list; see HO-XCT-002 §1 Amended. */
export interface ReapplyResult {
  processed: number;
  alreadyErased: number;
  notFound: number;
  merged: number;
  failed?: number;
}

export const reapplyErasures = (request: ReapplyRequest) =>
  apiFetch<ReapplyResult>('/api/admin/retention/erasures/reapply', {
    method: 'POST',
    body: request,
  });
