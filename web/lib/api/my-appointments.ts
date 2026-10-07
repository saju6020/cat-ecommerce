import { apiFetch } from './client';
import type { MyAppointmentDetail, AppointmentSummary, PagedResult } from './types';

/**
 * `lib/api/my-appointments.ts` — HO-APT-002 §8.2: a customer's own appointments, reschedule,
 * cancel and waitlist (HO-APT-001 §12.4). Someone else's appointment is a `404`, never a `403`.
 */

export type AppointmentScope = 'upcoming' | 'past';

export async function getMyAppointments(
  scope: AppointmentScope,
  pageNumber = 1,
  pageSize = 20,
): Promise<PagedResult<AppointmentSummary>> {
  const query = new URLSearchParams({
    scope,
    pageNumber: String(pageNumber),
    pageSize: String(pageSize),
  });
  return apiFetch<PagedResult<AppointmentSummary>>(
    `/api/customers/me/appointments?${query.toString()}`,
  );
}

export async function getMyAppointment(appointmentId: string): Promise<MyAppointmentDetail> {
  return apiFetch<MyAppointmentDetail>(
    `/api/customers/me/appointments/${encodeURIComponent(appointmentId)}`,
  );
}

export interface RescheduleRequest {
  startsOn: string;
  performerId?: string;
}

export async function rescheduleMyAppointment(
  appointmentId: string,
  request: RescheduleRequest,
): Promise<MyAppointmentDetail> {
  return apiFetch<MyAppointmentDetail>(
    `/api/customers/me/appointments/${encodeURIComponent(appointmentId)}/reschedule`,
    { method: 'POST', body: request },
  );
}

export async function cancelMyAppointment(
  appointmentId: string,
  reason: string | null,
): Promise<void> {
  await apiFetch<void>(
    `/api/customers/me/appointments/${encodeURIComponent(appointmentId)}/cancel`,
    { method: 'POST', body: { reason } },
  );
}

export type WaitlistStatus = 'Waiting' | 'Offered' | 'Claimed' | 'Lapsed' | 'Withdrawn' | 'Closed';

export interface WaitlistEntry {
  id: string;
  branchId: string;
  catId: string;
  catName: string;
  serviceId: string;
  serviceName: string;
  localDate: string; // yyyy-MM-dd, branch-local
  status: WaitlistStatus;
  offeredAppointmentId: string | null;
  offerExpiresOn: string | null;
  createdOn: string;
}

export async function getMyWaitlist(): Promise<WaitlistEntry[]> {
  return apiFetch<WaitlistEntry[]>('/api/customers/me/waitlist');
}

export interface JoinWaitlistRequest {
  branchId: string;
  serviceId: string;
  catId: string;
  localDate: string;
  preferredPerformerId?: string;
}

export async function joinWaitlist(
  request: JoinWaitlistRequest,
): Promise<{ waitlistEntryId: string; claimWindowMinutes: number }> {
  return apiFetch<{ waitlistEntryId: string; claimWindowMinutes: number }>(
    '/api/customers/me/waitlist',
    { method: 'POST', body: request },
  );
}

export async function withdrawFromWaitlist(waitlistEntryId: string): Promise<void> {
  await apiFetch<void>(`/api/customers/me/waitlist/${encodeURIComponent(waitlistEntryId)}`, {
    method: 'DELETE',
  });
}
