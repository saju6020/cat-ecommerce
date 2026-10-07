import { apiFetch } from './client';
import type { MyAppointmentDetail } from './types';
import { getPublicServices, type PublicService } from './admin-catalogue';
import { getPublicBranches } from './admin-branches';

/**
 * `lib/api/booking.ts` — HO-APT-002 §8.2: slots, performers, holds and confirm, for customers
 * (HO-APT-001 §12.4, verified against `BookingSlotsController` / `CustomerAppointmentsController`
 * and `ClinicViewModels.cs` — where the handover's prose and the code differ, the code is used:
 * the hold `201` carries `performerId`/`roomId`, and confirm returns the appointment detail
 * itself, not `{ appointment }`). Slot availability is computed by the server only (ADR-069).
 */

export interface SlotPerformer {
  id: string;
  name: string;
}

/** One start time; `performers` lists everyone free to take it (grouped by start, §12.4). */
export interface SlotGroup {
  startsOn: string;
  endsOn: string;
  performers: SlotPerformer[];
}

export interface AvailableSlots {
  date: string; // yyyy-MM-dd, branch-local
  branchTimeZoneId: string;
  slots: SlotGroup[];
}

export interface GetSlotsParams {
  branchId: string;
  serviceId: string;
  date: string;
  performerId?: string;
  catId?: string;
}

export async function getBookingSlots(params: GetSlotsParams): Promise<AvailableSlots> {
  const query = new URLSearchParams({
    branchId: params.branchId,
    serviceId: params.serviceId,
    date: params.date,
  });
  if (params.performerId) query.set('performerId', params.performerId);
  if (params.catId) query.set('catId', params.catId);
  return apiFetch<AvailableSlots>(`/api/booking/slots?${query.toString()}`);
}

export async function getBookingPerformers(
  branchId: string,
  serviceId: string,
): Promise<SlotPerformer[]> {
  const query = new URLSearchParams({ branchId, serviceId });
  return apiFetch<SlotPerformer[]>(`/api/booking/performers?${query.toString()}`);
}

export interface PlaceHoldRequest {
  branchId: string;
  serviceId: string;
  catId: string;
  startsOn: string;
  performerId?: string;
}

export interface SlotHold {
  appointmentId: string;
  startsOn: string;
  endsOn: string;
  performerId: string | null;
  roomId: string | null;
  holdExpiresOn: string;
}

export async function placeSlotHold(request: PlaceHoldRequest): Promise<SlotHold> {
  return apiFetch<SlotHold>('/api/customers/me/appointment-holds', {
    method: 'POST',
    body: request,
  });
}

/** `keepalive` lets the request outlive the page (`pagehide`, OQ-3) — best effort; the server's
 *  expiry is the guarantee. */
export async function releaseSlotHold(
  appointmentId: string,
  options: { keepalive?: boolean } = {},
): Promise<void> {
  await apiFetch<void>(`/api/customers/me/appointment-holds/${encodeURIComponent(appointmentId)}`, {
    method: 'DELETE',
    keepalive: options.keepalive,
  });
}

export async function confirmAppointment(
  appointmentId: string,
  reasonForVisit: string | null,
): Promise<MyAppointmentDetail> {
  return apiFetch<MyAppointmentDetail>(
    `/api/customers/me/appointments/${encodeURIComponent(appointmentId)}/confirm`,
    { method: 'POST', body: { reasonForVisit } },
  );
}

/** A public service as the booking flow reads it (`PublicService` now carries `description`). */
export type BookableService = PublicService;

export async function getBookableServices(): Promise<BookableService[]> {
  return getPublicServices();
}

/** The clinic as a customer sees it. `GET /api/branches` sends `addressLine`/`phoneNumber`
 *  (`BranchSummaryViewModel`), which is what `PublicBranch` and the shared admin mock now use too;
 *  this maps them to the shorter `address`/`phone` the booking UI reads. */
export interface ClinicBranch {
  id: string;
  name: string;
  address: string | null;
  phone: string | null;
  email: string | null;
  timeZoneId: string;
  /** Weekdays (0 = Sunday) with at least one opening interval — display only, never availability. */
  openWeekdays: number[];
}

const WEEKDAY_INDEX: Record<string, number> = {
  Sunday: 0,
  Monday: 1,
  Tuesday: 2,
  Wednesday: 3,
  Thursday: 4,
  Friday: 5,
  Saturday: 6,
};

export async function getClinicBranches(): Promise<ClinicBranch[]> {
  const rows = await getPublicBranches();
  return rows.map((row) => ({
    id: row.id,
    name: row.name,
    address: row.addressLine ?? null,
    phone: row.phoneNumber ?? null,
    email: row.email ?? null,
    timeZoneId: row.timeZoneId,
    openWeekdays: [
      ...new Set(
        (row.weeklyHours ?? [])
          .map((interval) => WEEKDAY_INDEX[interval.dayOfWeek])
          .filter((day): day is number => day !== undefined),
      ),
    ],
  }));
}

/**
 * The booking payloads of an existing appointment (HO-APT-001 `AppointmentSummary`/`Detail`) carry
 * the service SNAPSHOT (name, category, duration, price) but not the service id, and the slot
 * endpoints need one. This is the only bridge available without a new field: an exact
 * name + category + duration match against the public list. Returns null when there is not exactly
 * one match (an unbookable or renamed service) — the caller then falls back to the clinic's contact
 * details rather than guessing. Recorded as an open contract gap in HO-APT-002 §1 Amended.
 */
export function findServiceIdForSnapshot(
  services: BookableService[],
  snapshot: { name: string; category: string; durationMinutes: number },
): string | null {
  const matches = services.filter(
    (service) =>
      service.name === snapshot.name &&
      service.category === snapshot.category &&
      service.durationMinutes === snapshot.durationMinutes,
  );
  return matches.length === 1 ? matches[0]!.id : null;
}
