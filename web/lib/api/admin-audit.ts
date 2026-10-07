import { format, subDays } from 'date-fns';
import { apiFetch, getApiUrl } from './client';
import type { PagedResult } from './types';

/** `lib/api/admin-audit.ts` — HO-ADM-002 §8.2/§11.7. Audit-log search and CSV export (HO-ADM-001
 *  §12.4, ADR-051). */

export interface AuditEntry {
  occurredOn: string; // instant
  actorUserId: string;
  actorDisplayName: string;
  action: string;
  category: string;
  entityType: string;
  entityId: string;
  reason: string | null;
  beforeValue: string | null;
  afterValue: string | null;
  ipAddress: string | null;
}

export interface AuditSearchParams {
  actorUserId?: string;
  entityType?: string;
  entityId?: string;
  action?: string;
  category?: string;
  from: string; // "yyyy-MM-dd" — required
  to: string;
  pageNumber?: number;
  pageSize?: number;
}

/** The range used when a date is unset or cleared: the last `AUDIT_DEFAULT_RANGE_DAYS` days. */
export function defaultAuditRange(now: Date = new Date()): { from: string; to: string } {
  return {
    from: format(subDays(now, AUDIT_DEFAULT_RANGE_DAYS), 'yyyy-MM-dd'),
    to: format(now, 'yyyy-MM-dd'),
  };
}

function buildAuditQuery(params: AuditSearchParams): URLSearchParams {
  const query = new URLSearchParams();
  if (params.actorUserId) query.set('actorUserId', params.actorUserId);
  if (params.entityType) query.set('entityType', params.entityType);
  if (params.entityId) query.set('entityId', params.entityId);
  if (params.action) query.set('action', params.action);
  if (params.category) query.set('category', params.category);
  // ADM backlog 31: a cleared (empty) date means the default range, never `from=`/`to=`.
  const defaults = defaultAuditRange();
  query.set('from', params.from || defaults.from);
  query.set('to', params.to || defaults.to);
  if (params.pageNumber) query.set('pageNumber', String(params.pageNumber));
  if (params.pageSize) query.set('pageSize', String(params.pageSize));
  return query;
}

export async function searchAuditEntries(
  params: AuditSearchParams,
): Promise<PagedResult<AuditEntry>> {
  const query = buildAuditQuery(params);
  return apiFetch<PagedResult<AuditEntry>>(`/api/admin/audit-entries?${query.toString()}`);
}

/** A navigable download URL, not a `fetch` call (HO-CAT-002 §8.2 rule 2's own exception for a
 *  binary/browser-navigated download — see `client.ts`'s `getApiUrl` doc comment). The caller
 *  navigates the browser to this (`window.location.href = ...` or an `<a href>`), so the session
 *  cookie rides along the same way it would for any other same-site navigation, and a `413` is
 *  shown by the browser's own download failure rather than parsed here. */
export function getAuditExportUrl(params: AuditSearchParams): string {
  const query = buildAuditQuery(params);
  return getApiUrl(`/api/admin/audit-entries/export.csv?${query.toString()}`);
}

export const AUDIT_MAX_RANGE_DAYS = 366;
export const AUDIT_DEFAULT_RANGE_DAYS = 7;
