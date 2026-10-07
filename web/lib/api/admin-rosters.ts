import { apiFetch } from './client';
import type { DayOfWeekName } from './admin-branches';

/** `lib/api/admin-rosters.ts` — HO-ADM-002 §8.2. Weekly patterns, roster exceptions and staff
 *  availability (HO-ADM-001 §8.4/§8.5/§12.4). */

export interface ShiftSlot {
  dayOfWeek: DayOfWeekName;
  starts: string; // "HH:mm"
  ends: string;
  defaultRoomId: string | null;
}

export interface StaffRoster {
  staffAccountId: string;
  branchId: string;
  currentPattern: ShiftSlot[];
  nextPattern: ShiftSlot[] | null;
  nextPatternEffectiveFrom: string | null; // "yyyy-MM-dd"
}

export async function getBranchRosters(branchId: string): Promise<StaffRoster[]> {
  return apiFetch<StaffRoster[]>(`/api/admin/branches/${encodeURIComponent(branchId)}/rosters`);
}

export interface SetRosterPatternRequest {
  slots: ShiftSlot[];
  effectiveFrom?: string | null; // null/omitted = replace the current pattern now
}

export async function setRosterPattern(
  branchId: string,
  staffAccountId: string,
  request: SetRosterPatternRequest,
): Promise<void> {
  await apiFetch<void>(
    `/api/admin/branches/${encodeURIComponent(branchId)}/rosters/${encodeURIComponent(staffAccountId)}/pattern`,
    { method: 'PUT', body: request },
  );
}

export async function cancelScheduledRosterPattern(
  branchId: string,
  staffAccountId: string,
): Promise<void> {
  await apiFetch<void>(
    `/api/admin/branches/${encodeURIComponent(branchId)}/rosters/${encodeURIComponent(staffAccountId)}/next-pattern`,
    { method: 'DELETE' },
  );
}

export type RosterExceptionKind = 'ExtraShift' | 'ShiftCancelled';

export interface RosterException {
  id: string;
  staffAccountId: string;
  branchId: string;
  date: string; // "yyyy-MM-dd"
  kind: RosterExceptionKind;
  starts: string;
  ends: string;
  reason: string;
}

export async function getRosterExceptions(
  branchId: string,
  params: { from?: string; to?: string } = {},
): Promise<RosterException[]> {
  const query = new URLSearchParams();
  if (params.from) query.set('from', params.from);
  if (params.to) query.set('to', params.to);
  const qs = query.toString();
  return apiFetch<RosterException[]>(
    `/api/admin/branches/${encodeURIComponent(branchId)}/roster-exceptions${qs ? `?${qs}` : ''}`,
  );
}

export interface CreateRosterExceptionRequest {
  staffAccountId: string;
  date: string;
  kind: RosterExceptionKind;
  starts: string;
  ends: string;
  reason: string;
}

export async function createRosterException(
  branchId: string,
  request: CreateRosterExceptionRequest,
): Promise<{ rosterExceptionId: string }> {
  return apiFetch<{ rosterExceptionId: string }>(
    `/api/admin/branches/${encodeURIComponent(branchId)}/roster-exceptions`,
    { method: 'POST', body: request },
  );
}

export async function cancelRosterException(
  branchId: string,
  rosterExceptionId: string,
): Promise<void> {
  await apiFetch<void>(
    `/api/admin/branches/${encodeURIComponent(branchId)}/roster-exceptions/${encodeURIComponent(rosterExceptionId)}`,
    { method: 'DELETE' },
  );
}

export interface AvailabilityInterval {
  staffAccountId: string;
  branchId: string;
  start: string; // instant
  end: string;
  defaultRoomId: string | null;
  outsideOpeningHours: boolean;
}

export interface GetAvailabilityParams {
  from: string;
  to: string;
  staffAccountId?: string;
  restrictToOpeningHours?: boolean;
}

export async function getAvailability(
  branchId: string,
  params: GetAvailabilityParams,
): Promise<AvailabilityInterval[]> {
  const query = new URLSearchParams({ from: params.from, to: params.to });
  if (params.staffAccountId) query.set('staffAccountId', params.staffAccountId);
  if (params.restrictToOpeningHours !== undefined) {
    query.set('restrictToOpeningHours', String(params.restrictToOpeningHours));
  }
  return apiFetch<AvailabilityInterval[]>(
    `/api/admin/branches/${encodeURIComponent(branchId)}/availability?${query.toString()}`,
  );
}
