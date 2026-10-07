import { apiFetch } from './client';
import type {
  AppointmentDetail,
  AppointmentStatus,
  AppointmentSummary,
  TriagePriority,
} from './types';

/**
 * `lib/api/staff-appointments.ts` — HO-APT-002 §8.2: staff slots, calendar, day list, detail and
 * every staff command (HO-APT-001 §12.4, verified against `StaffAppointmentsController`). The
 * branch comes from `X-Branch-Id` (sent by `client.ts`), never a parameter.
 */

export interface StaffSlot {
  startsOn: string;
  endsOn: string;
  performerId: string;
  performerName: string;
  roomId: string;
  roomName: string;
  outsideOpeningHours: boolean;
}

export interface StaffSlotsParams {
  serviceId: string;
  date: string;
  performerId?: string;
  catId?: string;
}

export async function getStaffSlots(params: StaffSlotsParams): Promise<StaffSlot[]> {
  const query = new URLSearchParams({ serviceId: params.serviceId, date: params.date });
  if (params.performerId) query.set('performerId', params.performerId);
  if (params.catId) query.set('catId', params.catId);
  return apiFetch<StaffSlot[]>(`/api/staff/appointments/slots?${query.toString()}`);
}

// ---- calendar / day list -------------------------------------------------------------------

export type CalendarView = 'performer' | 'room';

export interface CalendarResource {
  id: string;
  name: string;
}

export interface CalendarAvailability {
  staffAccountId: string;
  start: string;
  end: string;
  outsideOpeningHours: boolean;
}

export interface CalendarClosure {
  startsOn: string;
  endsOn: string;
  kind: string;
  reason: string;
}

export interface CalendarAppointment {
  id: string;
  version: number;
  catId: string;
  catName: string;
  customerId: string;
  customerName: string;
  serviceName: string;
  startsOn: string | null;
  endsOn: string | null;
  performerId: string | null;
  roomId: string | null;
  status: AppointmentStatus;
  isWalkIn: boolean;
  needsRescheduling: boolean;
  catIsHighRisk: boolean;
}

export interface CalendarPayload {
  gridMinutes: number;
  resources: CalendarResource[];
  availability: CalendarAvailability[];
  closures: CalendarClosure[];
  appointments: CalendarAppointment[];
}

export interface CalendarParams {
  from: string;
  to: string;
  view: CalendarView;
  ids?: string[];
}

export async function getCalendar(params: CalendarParams): Promise<CalendarPayload> {
  const query = new URLSearchParams({ from: params.from, to: params.to, view: params.view });
  if (params.ids && params.ids.length > 0) query.set('ids', params.ids.join(','));
  return apiFetch<CalendarPayload>(`/api/staff/appointments/calendar?${query.toString()}`);
}

export async function getDayList(date: string): Promise<CalendarAppointment[]> {
  return apiFetch<CalendarAppointment[]>(
    `/api/staff/appointments/day?date=${encodeURIComponent(date)}`,
  );
}

export async function getStaffAppointment(appointmentId: string): Promise<AppointmentDetail> {
  return apiFetch<AppointmentDetail>(
    `/api/staff/appointments/${encodeURIComponent(appointmentId)}`,
  );
}

// ---- commands -------------------------------------------------------------------------------

export interface StaffBookRequest {
  customerId: string;
  catId: string;
  serviceId: string;
  startsOn: string;
  performerId: string;
  roomId?: string;
  reasonForVisit?: string;
}

export async function bookStaffAppointment(
  request: StaffBookRequest,
): Promise<{ appointmentId: string }> {
  return apiFetch<{ appointmentId: string }>('/api/staff/appointments', {
    method: 'POST',
    body: request,
  });
}

export interface MoveRequest {
  expectedVersion: number;
  startsOn: string;
  performerId: string;
  roomId: string;
}

export async function moveAppointment(
  appointmentId: string,
  request: MoveRequest,
): Promise<AppointmentDetail> {
  return apiFetch<AppointmentDetail>(
    `/api/staff/appointments/${encodeURIComponent(appointmentId)}/move`,
    { method: 'POST', body: request },
  );
}

export async function cancelStaffAppointment(appointmentId: string, reason: string): Promise<void> {
  await apiFetch<void>(`/api/staff/appointments/${encodeURIComponent(appointmentId)}/cancel`, {
    method: 'POST',
    body: { reason },
  });
}

export async function checkInAppointment(
  appointmentId: string,
  triagePriority?: TriagePriority,
): Promise<void> {
  await apiFetch<void>(`/api/staff/appointments/${encodeURIComponent(appointmentId)}/check-in`, {
    method: 'POST',
    body: { triagePriority: triagePriority ?? null },
  });
}

export async function setTriagePriority(
  appointmentId: string,
  triagePriority: TriagePriority,
): Promise<void> {
  await apiFetch<void>(`/api/staff/appointments/${encodeURIComponent(appointmentId)}/triage`, {
    method: 'PUT',
    body: { triagePriority },
  });
}

export interface StartRequest {
  performerId?: string;
  roomId?: string;
}

export async function startAppointment(
  appointmentId: string,
  request: StartRequest = {},
): Promise<void> {
  await apiFetch<void>(`/api/staff/appointments/${encodeURIComponent(appointmentId)}/start`, {
    method: 'POST',
    body: request,
  });
}

export async function completeAppointment(appointmentId: string): Promise<void> {
  await apiFetch<void>(`/api/staff/appointments/${encodeURIComponent(appointmentId)}/complete`, {
    method: 'POST',
  });
}

export async function recordNoShow(appointmentId: string): Promise<void> {
  await apiFetch<void>(`/api/staff/appointments/${encodeURIComponent(appointmentId)}/no-show`, {
    method: 'POST',
  });
}

// ---- flagged / customer summary --------------------------------------------------------------

export interface FlaggedAppointment {
  id: string;
  catId: string;
  catName: string;
  startsOn: string;
  rescheduleFlagReason: string;
  rescheduleFlagSourceId: string | null;
}

export async function getFlaggedAppointments(): Promise<FlaggedAppointment[]> {
  return apiFetch<FlaggedAppointment[]>('/api/staff/appointments/flagged');
}

export interface CustomerAppointmentSummary {
  noShowCount: number;
  upcoming: AppointmentSummary[];
  lastVisitOn: string | null;
}

export async function getCustomerAppointmentSummary(
  customerId: string,
): Promise<CustomerAppointmentSummary> {
  return apiFetch<CustomerAppointmentSummary>(
    `/api/staff/customers/${encodeURIComponent(customerId)}/appointment-summary`,
  );
}
