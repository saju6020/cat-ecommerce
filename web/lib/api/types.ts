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

// ---------------------------------------------------------------------------------------------
// HO-BRD-002 §13 — Boarding shapes shared by the four `lib/api/*boarding*` modules.
//
// Derived from HO-BRD-001 §12.4 (routes), §8 (fields), §10.1 (enumerations) and §11 (commands).
// §12.4 names routes and the top-level members of a few responses but not every DTO, so where it is
// silent the field names below follow the aggregate's field names in §8 (camelCase). **The built
// backend wins**: every field here that is not spelled out in §12.4 is recorded in HO-BRD-002 §1
// Amended and is re-checked against the controllers/DTOs before merge. Money is HO-ADM-001's
// `{ amount: string, currencyCode }` end to end (ADR-FE-105 — no arithmetic in the browser); local
// dates are `yyyy-MM-dd`, times `HH:mm`, instants ISO-8601 with offset. Enum-shaped strings are the
// C# `Enumeration.DisplayName` values (§10.1).
// ---------------------------------------------------------------------------------------------

export interface BoardingMoney {
  amount: string;
  currencyCode: string;
}

export type BoardingUnitType = 'Standard' | 'Deluxe' | 'Family' | 'Isolation';
export type BoardingStayStatus =
  | 'Enquiry'
  | 'Reserved'
  | 'Confirmed'
  | 'CheckedIn'
  | 'CheckedOut'
  | 'Cancelled'
  | 'NoShow';
export type DepositState = 'NotRequired' | 'Awaiting' | 'PendingApproval' | 'Received';
export type StayRateBasis = 'Nightly' | 'Weekly' | 'Monthly';
export type RateUnitKind = 'Month' | 'Week' | 'Night' | 'AdditionalCat';
export type StayChargeKind =
  | 'RateUnit'
  | 'AdditionalCat'
  | 'LongStayDiscount'
  | 'Adjustment'
  | 'DepositRefundDue'
  | 'EarlyCheckoutCredit';
export type FoodSource = 'OwnFood' | 'HouseFood';
export type LitterType = 'Clumping' | 'Silica' | 'Tofu' | 'Bentonite' | 'Paper' | 'Other';
export type VaccinationGateStatus = 'Passed' | 'Blocked' | 'Overridden';
export type BelongingKind =
  | 'Carrier'
  | 'Bowl'
  | 'Toy'
  | 'Bedding'
  | 'Food'
  | 'Medication'
  | 'Other';
export type SignatureMethod = 'Drawn' | 'Typed';
export type CareTaskKind =
  | 'Feed'
  | 'Medication'
  | 'LitterChange'
  | 'Enrichment'
  | 'ObservationCheck';
export type CareTaskStatus = 'Pending' | 'Done' | 'Skipped' | 'Voided';
export type PortionEaten = 'None' | 'Quarter' | 'Half' | 'ThreeQuarters' | 'All';
export type WaterIntake = 'None' | 'Low' | 'Normal' | 'High';
export type LitterOutput = 'Normal' | 'None' | 'Abnormal';
export type CatMood = 'Relaxed' | 'Playful' | 'Anxious' | 'Withdrawn' | 'Aggressive';
export type IncidentKind =
  | 'Injury'
  | 'Illness'
  | 'EscapeAttempt'
  | 'Fight'
  | 'RefusalToEat'
  | 'Other';
export type IncidentSeverity = 'Low' | 'Medium' | 'High';
export type IncidentStatus = 'Open' | 'Resolved';
export type VetApprovalStatus = 'Pending' | 'Approved' | 'Declined';
export type StayAttentionReason =
  | 'CatDeceased'
  | 'CatOwnerChanged'
  | 'MaintenanceOverlap'
  | 'IsolationUnavailable'
  | 'VaccinationGateBlocked'
  | 'UnitOverstayed';
export type BoardingWaitlistStatus = 'Waiting' | 'Notified' | 'Withdrawn' | 'Closed';
export type DaycareBasis = 'Hourly' | 'Day';
export type DaycareVisitStatus = 'Booked' | 'CheckedIn' | 'CheckedOut' | 'Cancelled' | 'NoShow';
export type RoutineTaskKind = 'LitterChange' | 'Enrichment' | 'ObservationCheck';

/** `StayTransitionRules` action names — what `allowedTransitions[]` contains (ADR-FE-107). */
export type StayAction =
  | 'Reserve'
  | 'Confirm'
  | 'Cancel'
  | 'CheckIn'
  | 'CheckOut'
  | 'RecordNoShow'
  | 'Extend'
  | 'Relocate';

export interface QuoteRateUnit {
  sequence: number;
  kind: RateUnitKind;
  fromNight: number;
  toNight: number;
  description: string;
  amount: BoardingMoney;
}

export interface QuoteDiscountLine {
  description: string;
  /** Shown exactly as sent (the server sends a negative amount). */
  amount: BoardingMoney;
}

/** §8.8 — the same shape for availability, enquiry, reserve, extension and early check-out. */
export interface StayQuote {
  basis: StayRateBasis;
  units: QuoteRateUnit[];
  discountLines: QuoteDiscountLine[];
  total: BoardingMoney;
}

export interface MediaRef {
  storageKey: string;
  contentType: string;
  sizeInBytes: number;
  fileName: string;
}

export interface SignatureCapture {
  signerName: string;
  method: SignatureMethod;
  /** `Drawn` only: strokes of `[x, y]` integer pairs, 0…10 000 (HO-BRD-001 ADR-236). */
  strokes?: Array<Array<[number, number]>>;
  confirmedStatement: boolean;
}

export interface CareFeeding {
  localTime: string;
  portion: string;
  foodSource: FoodSource;
  foodDescription: string;
  houseFoodSkuId?: string | null;
}

export interface CareMedication {
  name: string;
  dose: string;
  route: string;
  times: string[];
  instructions?: string | null;
  fromOn?: string | null;
  toOn?: string | null;
}

/** §8.7 — FR-BRD-6. */
export interface CareProfile {
  feedings: CareFeeding[];
  medications: CareMedication[];
  litterType: LitterType;
  litterNote?: string | null;
  vetVisitAuthorised: boolean;
  vetSpendingLimit: BoardingMoney;
  emergencyContact: { name: string; phone: string; relationship?: string | null };
  notes?: string | null;
}

export interface GateReason {
  /** `NeverAdministered` | `DueBeforeDeparture` (HO-BRD-001 §1.1: the member is `reason`). */
  reason: string;
  vaccineCode?: string | null;
  vaccineName?: string | null;
  dueOn?: string | null;
  message?: string | null;
}

export interface BlockedCat {
  catId: string;
  catName: string;
  reasons: GateReason[];
  /** HO-APT-001's `/book?catId&category=Vaccination` — followed through `isSafeInternalPath`. */
  bookingLink: string;
}

export interface VaccinationGate {
  passed: boolean;
  blockedCats: BlockedCat[];
}

/** Every stay view carries these per cat (§12.4 header). */
export interface BoardingCatHeader {
  catId: string;
  catName: string;
  catPhotoUrl?: string | null;
  catIsHighRisk: boolean;
  handlingNote?: string | null;
}

export interface StayCatDetail extends BoardingCatHeader {
  careProfile: CareProfile | null;
  careProfileComplete: boolean;
  gateStatus: VaccinationGateStatus;
  gateReasons: GateReason[];
  gateOverriddenBy?: string | null;
  gateOverrideReason?: string | null;
  gateOverriddenOn?: string | null;
  suspectedInfectious: boolean;
  /** What was handed in at check-in, with ids (the check-out lists them) -- from `checkIn.belongings`. */
  belongings?: Array<{
    id: string;
    kind: BelongingKind;
    description: string;
    returned: boolean | null;
    returnNote: string | null;
  }>;
  /** Set while the cat is at the clinic (a referral or a surgery): "Away — in surgery". */
  awayFromUnit?: boolean;
  awayReason?: string | null;
  checkedInWeightKg?: string | null;
}

export interface StayChargeLine {
  id: string;
  kind: StayChargeKind;
  description: string;
  amount: BoardingMoney;
  fromNight?: number | null;
  toNight?: number | null;
  invoicedOn?: string | null;
  invoiceId?: string | null;
}

export interface StayDeposit {
  required: BoardingMoney;
  accepted: BoardingMoney;
  outstanding: BoardingMoney;
  state: DepositState;
  /** Instant; only while `Reserved`. */
  payBy: string | null;
  instructionsPath?: string | null;
}

/** The built stay carries no invoices; the summary lists `billing.invoiceIds` only, so the number, kind and
 *  total are optional (HO-BRD-002 1 Amended). */
export interface StayInvoiceRef {
  id: string;
  number?: string;
  kind?: 'Cycle' | 'Final';
  total?: BoardingMoney;
  issuedOn?: string;
}

/** The fields every stay view shares (customer and staff). */
export interface BoardingStayBase {
  id: string;
  branchId: string;
  branchTimeZoneId: string;
  version: number;
  status: BoardingStayStatus;
  allowedTransitions: StayAction[];
  arrivalOn: string;
  departureOn: string;
  nights: number;
  requestedUnitType: BoardingUnitType;
  unitName: string | null;
  unitBlock: string | null;
  cats: StayCatDetail[];
  quote: StayQuote | null;
  deposit: StayDeposit;
  /** Null when the server sends none (a stay without a quote): shown as absent, never as 0.00. */
  balanceDue: BoardingMoney | null;
  charges: StayChargeLine[];
  invoices: StayInvoiceRef[];
  occupiedFrom?: string | null;
  checkedInOn?: string | null;
  checkedOutOn?: string | null;
}

export interface IncidentSummary {
  id: string;
  catName: string;
  kind: IncidentKind;
  severity: IncidentSeverity;
  occurredOn: string;
  acknowledged: boolean;
}

export interface VetApprovalSummary {
  id: string;
  catName: string;
  amount: BoardingMoney;
  description: string;
  status: VetApprovalStatus;
}

/** `GET /api/customers/me/boarding/stays/{id}` and the reserve/cancel responses. */
export interface MyStayDetail extends BoardingStayBase {
  cancellation: { canCancel: boolean; depositOutcome: string | null } | null;
  incidents: IncidentSummary[];
  pendingApprovals: VetApprovalSummary[];
}

export interface MyStaySummary {
  id: string;
  branchTimeZoneId: string;
  status: BoardingStayStatus;
  arrivalOn: string;
  departureOn: string;
  nights: number;
  catNames: string[];
  unitType: BoardingUnitType;
  depositState: DepositState;
  balanceDue: BoardingMoney | null;
  /** `Enquiry` rows are drafts to resume; `vaccinationBlocked` says why one is waiting. */
  vaccinationBlocked: boolean;
}

export interface StaySearchParams {
  branchId: string;
  arrivalOn: string;
  departureOn: string;
  catCount: number;
}

export interface AvailableUnitType {
  unitType: BoardingUnitType;
  capacityCats: number;
  freeUnitCount: number;
  quote: StayQuote;
  /** Staff mode only: the free units of this type. */
  freeUnits?: Array<{ unitId: string; code: string; name: string; block: string | null }>;
}

export interface AvailabilityAlternative {
  arrivalOn: string;
  departureOn: string;
  unitType: BoardingUnitType;
  total: BoardingMoney;
}

export interface BoardingAvailability {
  branchTimeZoneId: string;
  nights: number;
  occupiedFrom: string;
  occupiedTo: string;
  unitTypes: AvailableUnitType[];
  alternatives: AvailabilityAlternative[];
  waitlistAvailable: boolean;
}

export interface EnquiryResult {
  stayId: string;
  vaccinationGate: VaccinationGate;
  quote: StayQuote | null;
}

export interface EnquiryRequest {
  branchId: string;
  arrivalOn: string;
  departureOn: string;
  unitType: BoardingUnitType;
  catIds: string[];
  /** Staff only. */
  customerId?: string;
}

export interface ReserveResult {
  stay: MyStayDetail;
  deposit: { amount: BoardingMoney; payBy: string; instructionsPath: string };
}

export interface StaffStayStatusChange {
  from: string;
  to: string;
  changedOn: string;
  changedBy: string;
}

export interface StayAllocation {
  id: string;
  unitName: string;
  unitType: BoardingUnitType;
  from: string;
  to: string;
  isActive: boolean;
  reason: string | null;
}

export interface IsolationEntry {
  id: string;
  unitName: string;
  staffName: string;
  enteredOn: string;
  leftOn: string | null;
  purpose: string;
}

export interface VetReferral {
  id: string;
  catId: string;
  catName: string;
  reason: string;
  urgency: TriagePriority;
  referredOn: string;
  /** Not on the built wire: shown only when sent. */
  referredBy?: string | null;
  /** Present once HO-EMR-001 has opened the visit. */
  visitId: string | null;
  /** Set when the cat is back from the clinic (`POST …/referrals/{id}/return`). */
  returnedOn?: string | null;
}

export interface VetApproval {
  id: string;
  catId: string;
  catName: string;
  amount: BoardingMoney;
  description: string;
  status: VetApprovalStatus;
  requestedOn: string;
  decidedOn: string | null;
  decidedVia: 'Portal' | 'Staff' | null;
}

export interface StaffStayDetail extends BoardingStayBase {
  customerId: string;
  customerName: string;
  customerPhone: string | null;
  channel: 'Online' | 'Reception';
  requiresIsolation: boolean;
  statusHistory: StaffStayStatusChange[];
  allocations: StayAllocation[];
  isolationEntries: IsolationEntry[];
  referrals: VetReferral[];
  approvals: VetApproval[];
  incidents: IncidentSummary[];
  attention: { needsAttention: boolean; reason: StayAttentionReason | null; note: string | null };
  /** True when the caller may override the gate (server capability, ADR-FE-107). */
  canOverrideGate: boolean;
}

export interface StaffStaySummary {
  id: string;
  branchTimeZoneId: string;
  status: BoardingStayStatus;
  arrivalOn: string;
  departureOn: string;
  catNames: string[];
  anyHighRisk: boolean;
  customerName: string;
  unitName: string | null;
  depositState: DepositState;
  balanceDue: BoardingMoney | null;
  needsAttention: boolean;
  gateBlocked: boolean;
  requiresIsolation: boolean;
}

export interface VetHeadroom {
  limit: BoardingMoney;
  committed: BoardingMoney;
  approvedIncrease: BoardingMoney;
  headroom: BoardingMoney;
  vetVisitAuthorised: boolean;
}

// ---- early check-out preview (owner decision 2026-10-14; shape expected, pending alignment) ----

/** `GET /api/staff/boarding/stays/{id}/check-out-preview` (HO-BRD-001 1.1, committed) -- writes nothing; runs
 *  the very code the check-out runs. */
export interface CheckOutPreviewLine {
  lineRef: string;
  description: string;
  net: BoardingMoney;
  /** `Keep` (stays as it is), `Void` (an uninvoiced charge that no longer applies) or `Record` (a new charge). */
  action: 'Keep' | 'Void' | 'Record';
}

export interface CheckOutPreview {
  isEarly: boolean;
  /** Past the booked departure (HO-BRD-001 fc6ef889): the extra nights are charged. Absent on older builds. */
  isLate?: boolean;
  nightsBooked: number;
  nightsCharged: number;
  quote: StayQuote | null;
  lines: CheckOutPreviewLine[];
  credit: BoardingMoney;
  creditOutcome: 'None' | 'Credit' | 'Refund';
  today: string;
  bookedDepartureOn: string;
  chargedDepartureOn: string;
  unitFreeFrom: string;
  previousTotal: BoardingMoney;
  newTotal: BoardingMoney;
}

// ---- stay summary (§8.16) ----

export interface SummaryObservation {
  recordedOn: string;
  portionEaten: PortionEaten;
  waterIntake: WaterIntake;
  litterOutput: LitterOutput;
  mood: CatMood;
  notes: string | null;
}

export interface StaySummaryCat extends BoardingCatHeader {
  dailyLogs: Array<{ date: string; observations: SummaryObservation[] }>;
  weightAtCheckInKg: string | null;
  weightAtCheckOutKg: string | null;
  /** Server-computed (ADR-FE-105), e.g. `+0.2`. */
  weightChangeKg: string | null;
  incidents: IncidentSummary[];
  medicationsGiven: Array<{ name: string; dose: string; givenOn: string; givenBy: string }>;
  vetVisits: Array<{
    referralId: string;
    reason: string;
    visitId: string | null;
    referredOn: string;
  }>;
  belongings: Array<{
    id: string;
    kind: BelongingKind;
    description: string;
    returned: boolean | null;
    returnNote: string | null;
  }>;
  /** `method` is not on the built report: shown only when sent. */
  checkOutSignature: { signerName: string; method: SignatureMethod | null; signedOn: string } | null;
}

export interface StaySummary {
  stayId: string;
  branchTimeZoneId: string;
  arrivalOn: string;
  departureOn: string;
  checkedOutOn: string | null;
  cats: StaySummaryCat[];
  charges: StayChargeLine[];
  depositReceived: BoardingMoney;
  /** The server's own billing totals (the report has no single balance): shown under their own labels. */
  billing: {
    billedTotal: BoardingMoney;
    unbilledTotal: BoardingMoney;
    invoicedOutstanding: BoardingMoney;
  } | null;
  invoices: StayInvoiceRef[];
}

// ---- diary (§8.17, FR-POR-4) ----

export interface DiaryMedia {
  id: string;
  catId: string | null;
  caption: string | null;
  capturedOnLocalDate: string;
  /** The recorded reference. */
  reference: string | null;
  /** A signed link to the photo, when the built diary sends the file (it expires: re-read to refresh). */
  fileUrl?: string | null;
}

export interface DiaryObservation extends SummaryObservation {
  id: string;
  catId: string;
  catName: string;
}

export interface DiaryDay {
  date: string;
  observations: DiaryObservation[];
  media: DiaryMedia[];
  completedTaskCount: number;
}

export interface StayDiary {
  stayId: string;
  branchTimeZoneId: string;
  catHeaders: BoardingCatHeader[];
  days: DiaryDay[];
}

// ---- incidents and approvals ----

export interface IncidentDetail {
  id: string;
  stayId: string;
  catId: string;
  catName: string;
  catIsHighRisk: boolean;
  handlingNote?: string | null;
  kind: IncidentKind;
  severity: IncidentSeverity;
  description: string;
  immediateAction: string;
  occurredOn: string;
  reportedOn: string;
  reportedByName?: string | null;
  ownerNotified: boolean;
  ownerAcknowledgedOn: string | null;
  acknowledgedVia: 'Portal' | 'Staff' | null;
  escalatedOn: string | null;
  phoneCallOn: string | null;
  phoneCallOutcome: string | null;
  status: IncidentStatus;
  resolvedOn: string | null;
  resolution: string | null;
  branchTimeZoneId: string;
  /** Staff queue only. */
  customerName?: string | null;
  customerPhone?: string | null;
  unitName?: string | null;
}

export interface VetApprovalDetail {
  id: string;
  stayId: string;
  catName: string;
  catIsHighRisk: boolean;
  handlingNote?: string | null;
  amount: BoardingMoney;
  description: string;
  status: VetApprovalStatus;
  requestedOn: string;
  decidedOn: string | null;
  /** Absent in the built owner view (the limit is on the staff `headroom[]`): shown only when sent. */
  limit?: BoardingMoney;
  used?: BoardingMoney;
  remaining?: BoardingMoney;
  branchTimeZoneId: string;
}

// ---- care tasks (§8.9) ----

export interface CareTask {
  id: string;
  stayId: string;
  catId: string;
  catName: string;
  kind: CareTaskKind;
  /** e.g. `Feed ⅓ cup dry — own food` — written by the server. */
  title: string;
  dueOn: string;
  dueLocalTime: string;
  status: CareTaskStatus;
  /** Derived by the server (ADR-FE-105): never computed in the browser. */
  isOverdue: boolean;
  minutesLate: number | null;
  assignedToName: string | null;
  assignedToMe: boolean;
  completedOn: string | null;
  completedByName: string | null;
  skipReason: string | null;
}

export interface TaskUnitCat extends BoardingCatHeader {
  /** The server's `NotEatingRule` result — never derived (U-D5). `null` = the task list does not carry it (the
   *  built list has none): the board then takes it from the dashboard's `notEating[]`. */
  notEating: boolean | null;
}

export interface TaskUnitGroup {
  unitId: string;
  unitCode: string;
  unitName: string;
  block: string | null;
  isIsolation: boolean;
  isolationNote: string | null;
  cats: TaskUnitCat[];
  tasks: CareTask[];
}

export interface TaskDayList {
  date: string;
  branchTimeZoneId: string;
  generatedOn: string;
  units: TaskUnitGroup[];
}

export interface CareObservation extends DiaryObservation {
  recordedByName: string;
}

export interface ObservationList {
  items: CareObservation[];
  cats: Array<{ catId: string; catName: string; notEating: boolean }>;
}

export interface StayMedia {
  id: string;
  catId: string | null;
  incidentId: string | null;
  caption: string | null;
  capturedOnLocalDate: string;
  reference: string | null;
  status: 'Visible' | 'Hidden' | 'Purged';
  uploadedByName: string;
}

// ---- dashboard (§12.6) ----

export interface DashboardOccupancy {
  unitsOccupied: number;
  unitsActive: number;
  catsInHouse: number;
  byType: Array<{ unitType: BoardingUnitType; occupied: number; active: number }>;
}

export interface DashboardStayRow {
  stayId: string;
  catNames: string[];
  anyHighRisk: boolean;
  unitName: string | null;
  /** Not on the built dashboard rows (HO-BRD-002 1 Amended): absent -> null. */
  customerName: string | null;
  depositState: DepositState | null;
  balanceDue: BoardingMoney | null;
  status: BoardingStayStatus;
}

export interface DashboardTaskRow {
  taskId: string;
  stayId: string;
  catName: string;
  unitName: string;
  title: string;
  dueLocalTime: string;
  minutesLate: number | null;
}

export interface BoardingDashboard {
  generatedOn: string;
  branchTimeZoneId: string;
  occupancy: DashboardOccupancy;
  arrivals: DashboardStayRow[];
  departures: DashboardStayRow[];
  notEating: Array<{
    stayId: string;
    catId: string;
    catName: string;
    unitName: string;
    lastEntries: Array<{ recordedOn: string; portionEaten: PortionEaten }>;
  }>;
  overdueTasks: DashboardTaskRow[];
  unassignedTasks: DashboardTaskRow[];
  openIncidents: Array<{
    incidentId: string;
    stayId: string;
    catName: string;
    kind?: IncidentKind;
    severity: IncidentSeverity;
    acknowledged: boolean;
    escalated: boolean;
    reportedOn: string;
  }>;
  callOwner: Array<{
    incidentId: string;
    stayId: string;
    catName: string;
    customerName: string | null;
    customerPhone: string | null;
    reportedOn: string;
  }>;
  unpaidBalances: Array<{
    stayId: string;
    customerName: string | null;
    catNames: string[];
    kind: 'DepositAwaiting' | 'BalanceOutstanding';
    amount: BoardingMoney;
    depositDueBy: string | null;
  }>;
  refundDue: Array<{
    stayId: string;
    customerName: string | null;
    amount: BoardingMoney;
    reason: string;
  }>;
  attention: Array<{
    stayId: string;
    catNames: string[];
    reason?: StayAttentionReason;
    note: string | null;
  }>;
}

// ---- occupancy board (§12.4) ----

export interface OccupancyStayBar {
  stayId: string;
  catNames: string[];
  status: BoardingStayStatus;
  /** Branch-local, inclusive: the first and last day the bar covers on this unit. */
  fromOn: string;
  toOn: string;
  awayInSurgery: boolean;
}

export interface OccupancyUnitRow {
  unitId: string;
  code: string;
  name: string;
  unitType: BoardingUnitType;
  block: string | null;
  stays: OccupancyStayBar[];
  maintenance: Array<{ blockId: string; fromOn: string; toOn: string; reason: string }>;
  /** Days kept free for cleaning after a departure. */
  turnaroundDays: string[];
}

export interface BoardingOccupancy {
  from: string;
  to: string;
  branchTimeZoneId: string;
  days: string[];
  units: OccupancyUnitRow[];
}

// ---- admin ----

export interface BoardingUnit {
  id: string;
  code: string;
  name: string;
  unitType: BoardingUnitType;
  capacityCats: number;
  block: string | null;
  sortOrder: number;
  isActive: boolean;
  /** From the type's current rate card (HO-BRD-001 ADR-221) — read-only here. */
  nightlyRate: BoardingMoney | null;
}

export interface MaintenanceBlock {
  id: string;
  unitId: string;
  startsOn: string;
  endsOn: string;
  reason: string;
  isCancelled: boolean;
}

export interface BoardingRateCard {
  id: string;
  unitType: BoardingUnitType;
  nightlyRate: BoardingMoney;
  weeklyRate: BoardingMoney;
  monthlyRate: BoardingMoney;
  additionalCatNightlyRate: BoardingMoney;
  taxClassId: string;
  taxClassName: string | null;
  effectiveFrom: string;
  setBy: string | null;
}

export interface DaycareRateCard {
  id: string;
  hourlyRate: BoardingMoney;
  dayRate: BoardingMoney;
  taxClassId: string;
  taxClassName: string | null;
  effectiveFrom: string;
  setBy: string | null;
}

export interface EffectiveDatedHistory<T> {
  current: T | null;
  scheduled: T[];
  past: T[];
}

export interface CareRoutine {
  times: Array<{ kind: RoutineTaskKind; localTime: string }>;
  isDefault: boolean;
}

// ---- waitlist and daycare ----

export interface WaitlistEntryDto {
  id: string;
  branchId: string;
  branchTimeZoneId: string;
  arrivalOn: string;
  departureOn: string;
  catCount: number;
  unitType: BoardingUnitType | null;
  status: BoardingWaitlistStatus;
  notifiedOn: string | null;
}

export interface DaycareVisit {
  id: string;
  branchTimeZoneId: string;
  catId: string;
  catName: string;
  catIsHighRisk: boolean;
  handlingNote?: string | null;
  customerName?: string | null;
  localDate: string;
  basis: DaycareBasis;
  expectedHours: number | null;
  status: DaycareVisitStatus;
  chargeAmount: BoardingMoney | null;
  checkedInOn: string | null;
  checkedOutOn: string | null;
}

export interface DaycareDayList {
  date: string;
  branchTimeZoneId: string;
  offered: boolean;
  capacity: number;
  visits: DaycareVisit[];
}

export interface DaycareOffer {
  offered: boolean;
  hourlyRate: BoardingMoney | null;
  dayRate: BoardingMoney | null;
}

export interface UnitTypeInfo {
  unitType: BoardingUnitType;
  capacityCats: number;
  nightlyRate: BoardingMoney;
  weeklyRate: BoardingMoney;
  monthlyRate: BoardingMoney;
}
