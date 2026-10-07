/**
 * Shared, area-agnostic API contract shapes (HO-CAT-002 §11.1 / §11.2).
 *
 * These two are given verbatim by HO-CAT-002 as "the error shape every 400 returns" and are
 * genuinely shared — every area's mutations go through the same client and the same shape.
 *
 * Domain types (CatProfile, CatListItem, an area's own paged-list shape, ...) belong to the
 * area that owns the endpoint they describe. Add them in that area's own `lib/api/<area>.ts`,
 * mirroring that area's handover §12.3 view models exactly — same field names, same
 * nullability. Do not add domain types here; this file is imported by every area.
 */

export interface ValidationError {
  propertyName: string;
  errorCode: string; // HO-CAT-001 §10-style violation code — never shown to the user
  errorMessage: string;
  resourceName: string;
}

export interface ValidationResponse {
  errors: ValidationError[];
  isValid: boolean;
}

/**
 * ARCH gate LOW (first pass) — a generic pagination wrapper, not itself a domain type (`T` is
 * whatever the area's own endpoint returns), so it belongs here rather than in `staff-admin.ts`
 * (where it previously lived and was re-exported from `staff-customers.ts` for that area to use
 * too) — every area with a paginated list endpoint needs this same shape.
 */
export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}

// ---------------------------------------------------------------------------------------------
// HO-APT-002 §13 — Appointments shapes shared by more than one `lib/api/*` module (the customer
// and staff detail are the same view model on the wire, HO-APT-001 §12.4/`ClinicViewModels.cs`).
// Enum-shaped strings are the C# `Enumeration.DisplayName` values.
// ---------------------------------------------------------------------------------------------

export type AppointmentStatus =
  | 'Held'
  | 'HoldExpired'
  | 'HoldReleased'
  | 'Booked'
  | 'CheckedIn'
  | 'InConsultation'
  | 'Completed'
  | 'Cancelled'
  | 'NoShow';

export type TriagePriority = 'Routine' | 'Urgent' | 'Emergency';

/** `AppointmentTransitionRules` action names — what `allowedTransitions[]` contains (ADR-FE-028). */
export type AppointmentAction =
  | 'Confirm'
  | 'ReleaseHold'
  | 'ExpireHold'
  | 'Reschedule'
  | 'Cancel'
  | 'CheckIn'
  | 'SetTriagePriority'
  | 'Start'
  | 'Complete'
  | 'RecordNoShow';

/** `priceAmount` is a JSON number here (a C# `decimal`), unlike HO-ADM-001's string `Money`. */
export interface ServiceSnapshot {
  name: string;
  category: string;
  durationMinutes: number;
  priceAmount: number;
  priceCurrencyCode: string;
}

export interface BranchRef {
  id: string;
  name: string;
  phoneNumber: string | null;
}

export interface AppointmentStatusChange {
  from: string;
  to: string;
  changedOn: string;
  changedBy: string;
}

export interface AppointmentSummary {
  id: string;
  catId: string;
  catName: string;
  service: ServiceSnapshot;
  startsOn: string | null;
  endsOn: string | null;
  performerName: string | null;
  branch: BranchRef;
  branchTimeZoneId: string;
  status: AppointmentStatus;
  canChange: boolean;
  changeCutoffOn: string | null;
}

/** A status change as the customer sees it — no staff account id (backlog #26). */
export interface MyAppointmentStatusChange {
  from: string;
  to: string;
  changedOn: string;
}

/**
 * `GET /api/customers/me/appointments/{id}` and the confirm/reschedule responses (backlog #26). The
 * customer's own projection: it has none of the staff-only fields (`customerId`, room, triage
 * priority, presenting complaint, walk-in flag, channel, `version`, `allowedTransitions`, and the
 * staff ids on the history). A customer changes an appointment through `canChange` /
 * `changeCutoffOn`; reschedule and cancel send no version, so none is needed here.
 */
export interface MyAppointmentDetail {
  id: string;
  branchId: string;
  branchTimeZoneId: string;
  catId: string;
  catName: string;
  service: ServiceSnapshot;
  performerId: string | null;
  performerName: string | null;
  startsOn: string | null;
  endsOn: string | null;
  status: AppointmentStatus;
  holdExpiresOn: string | null;
  isWaitlistOffer: boolean;
  reasonForVisit: string | null;
  needsRescheduling: boolean;
  rescheduleFlagReason: string | null;
  canChange: boolean;
  changeCutoffOn: string | null;
  statusHistory: MyAppointmentStatusChange[];
}

/** The staff view of an appointment (`GET /api/staff/appointments/{id}`). */
export interface AppointmentDetail {
  id: string;
  branchId: string;
  branchTimeZoneId: string;
  customerId: string;
  catId: string;
  catName: string;
  service: ServiceSnapshot;
  performerId: string | null;
  performerName: string | null;
  roomId: string | null;
  roomName: string | null;
  startsOn: string | null;
  endsOn: string | null;
  isWalkIn: boolean;
  channel: string;
  status: AppointmentStatus;
  holdExpiresOn: string | null;
  isWaitlistOffer: boolean;
  reasonForVisit: string | null;
  triagePriority: TriagePriority;
  presentingComplaint: string | null;
  version: number;
  needsRescheduling: boolean;
  rescheduleFlagReason: string | null;
  canChange: boolean;
  changeCutoffOn: string | null;
  statusHistory: AppointmentStatusChange[];
  allowedTransitions: AppointmentAction[];
}
