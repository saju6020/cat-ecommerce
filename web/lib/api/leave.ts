import { apiFetch } from './client';
import type { PagedResult } from './types';

/** `lib/api/leave.ts` — HO-ADM-002 §8.2. Staff leave: self-service request/list/cancel, and the
 *  administrator decision queue (HO-ADM-001 §8.4/§12.4). */

export type LeaveKind = 'Annual' | 'Sick' | 'Training' | 'Other';
export const LEAVE_KINDS: LeaveKind[] = ['Annual', 'Sick', 'Training', 'Other'];

export type LeaveStatus = 'Requested' | 'Approved' | 'Rejected' | 'Cancelled';

export interface LeaveRequest {
  id: string;
  staffAccountId: string;
  staffFullName?: string;
  startsOn: string; // "yyyy-MM-dd"
  endsOn: string;
  kind: LeaveKind;
  note: string | null;
  status: LeaveStatus;
  decidedBy: string | null;
  decidedOn: string | null;
  decisionNote: string | null;
}

export async function getMyLeaveRequests(): Promise<LeaveRequest[]> {
  return apiFetch<LeaveRequest[]>('/api/staff/leave-requests');
}

export interface RequestLeaveRequest {
  startsOn: string;
  endsOn: string;
  kind: LeaveKind;
  note?: string | null;
  /** Set only by an administrator requesting on someone else's behalf (`/api/admin/leave-requests`
   *  below) — omit for "my leave" (the caller is implied server-side). */
  staffAccountId?: string;
}

export async function requestLeave(
  request: RequestLeaveRequest,
): Promise<{ leaveRequestId: string }> {
  return apiFetch<{ leaveRequestId: string }>('/api/staff/leave-requests', {
    method: 'POST',
    body: request,
  });
}

export async function cancelMyLeaveRequest(leaveRequestId: string): Promise<void> {
  await apiFetch<void>(`/api/staff/leave-requests/${encodeURIComponent(leaveRequestId)}/cancel`, {
    method: 'POST',
  });
}

export interface GetAdminLeaveRequestsParams {
  status?: LeaveStatus;
  staffAccountId?: string;
  from?: string;
  to?: string;
  pageNumber?: number;
  pageSize?: number;
}

const MAX_PAGE_SIZE = 100;
const MAX_PAGE_NUMBER = 1_000_000;

export async function getAdminLeaveRequests(
  params: GetAdminLeaveRequestsParams = {},
): Promise<PagedResult<LeaveRequest>> {
  const query = new URLSearchParams();
  if (params.status) query.set('status', params.status);
  if (params.staffAccountId) query.set('staffAccountId', params.staffAccountId);
  if (params.from) query.set('from', params.from);
  if (params.to) query.set('to', params.to);
  query.set('pageNumber', String(Math.min(params.pageNumber ?? 1, MAX_PAGE_NUMBER)));
  query.set('pageSize', String(Math.min(params.pageSize ?? 20, MAX_PAGE_SIZE)));
  return apiFetch<PagedResult<LeaveRequest>>(`/api/admin/leave-requests?${query.toString()}`);
}

export async function createLeaveRequestForStaff(
  request: RequestLeaveRequest,
): Promise<{ leaveRequestId: string }> {
  return apiFetch<{ leaveRequestId: string }>('/api/admin/leave-requests', {
    method: 'POST',
    body: request,
  });
}

export async function approveLeaveRequest(leaveRequestId: string): Promise<void> {
  await apiFetch<void>(`/api/admin/leave-requests/${encodeURIComponent(leaveRequestId)}/approve`, {
    method: 'POST',
  });
}

export async function rejectLeaveRequest(
  leaveRequestId: string,
  decisionNote?: string,
): Promise<void> {
  await apiFetch<void>(`/api/admin/leave-requests/${encodeURIComponent(leaveRequestId)}/reject`, {
    method: 'POST',
    body: decisionNote ? { decisionNote } : undefined,
  });
}

export async function cancelLeaveRequest(leaveRequestId: string): Promise<void> {
  await apiFetch<void>(`/api/admin/leave-requests/${encodeURIComponent(leaveRequestId)}/cancel`, {
    method: 'POST',
  });
}
