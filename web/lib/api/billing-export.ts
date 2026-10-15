import { apiFetch, getApiUrl } from './client';
import { query } from './billing-staff';
import type { PagedResult } from './types';

/**
 * `lib/api/billing-export.ts` — HO-BIL-002 §11.9, verified against `StaffAccountingExportsController`.
 * The download is a plain link so the browser streams the ZIP; every run is recorded by the server
 * (who, range, counts, SHA-256), and the same range gives the same bytes (ADR-190).
 *
 * Known gap (owner decision pending, HO-BIL-001 gate XI-15): the endpoint currently ignores the
 * caller's branch assignment. The branch selector below is built as the handover specifies.
 */

export interface AccountingExportRun {
  runId: string;
  from: string;
  to: string;
  branchId: string | null;
  invoiceCount: number;
  invoiceLineCount: number;
  paymentCount: number;
  allocationCount: number;
  creditNoteCount: number;
  refundCount: number;
  sha256: string;
  runBy: string;
  runByName: string;
  runOn: string;
}

export async function getExportRuns(pageNumber: number | string = 1): Promise<PagedResult<AccountingExportRun>> {
  return apiFetch<PagedResult<AccountingExportRun>>(
    `/api/staff/billing/accounting-exports${query({ pageNumber, pageSize: 20 })}`,
  );
}

export function exportDownloadUrl(params: { from: string; to: string; branchId?: string }): string {
  return getApiUrl(`/api/staff/billing/accounting-exports/download${query({ ...params })}`);
}

/** The first 12 hex characters of a SHA-256, for the run history (the full hash stays in the title). */
export function shortHash(sha256: string): string {
  return sha256.slice(0, 12);
}
