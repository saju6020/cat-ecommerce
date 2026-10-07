import type {
  AppointmentAction,
  AppointmentDetail,
  MyAppointmentDetail,
  AppointmentStatus,
  AppointmentSummary,
  ServiceSnapshot,
  TriagePriority,
} from '@/lib/api/types';
import { addDaysIso, zonedLocalToInstant } from '@/lib/format/appointment';
import { localDateOf, timeOnlyInZone, todayInZone } from '@/lib/format/time';
import { loadPersisted, savePersisted, clearPersisted } from '../admin/fixtures';
import { appointmentsFixtureIds as ids, RACE_LOCAL_TIME, serverNow } from './fixtures';

/**
 * `mocks/handlers/appointments/store.ts` — HO-APT-002 §12. One in-memory (sessionStorage-persisted,
 * like HO-ADM-002's stores) appointment database shared by the customer and staff handlers, plus
 * the *mock server's* slot rules. The slot rules live here and ONLY here — the real UI never
 * computes availability (ADR-069, HO-APT-002 §20.2); this file stands in for the API.
 */

const PERSIST_KEY = '__purrcare_appointments_mock_db__';
const CHANGE_CUTOFF_HOURS = 4;
const GRID_MINUTES = 15;
const MIN_NOTICE_MINUTES = 60;
const NO_SHOW_GRACE_MINUTES = 15;

// ---- the clinic ---------------------------------------------------------------------------

export interface MockPerformer {
  id: string;
  name: string;
  roleIds: string[];
  branchId: string;
  /** Local weekdays worked (0 = Sunday) and the shift's local start/end. */
  days: number[];
  from: string;
  to: string;
}

export interface MockRoom {
  id: string;
  name: string;
  kind: string;
  branchId: string;
}

export const BRANCH_INFO: Record<
  string,
  { name: string; zone: string; phone: string; hours: Record<number, Array<[string, string]>> }
> = {
  [ids.mainBranchId]: {
    name: 'Main clinic',
    zone: 'Asia/Dhaka',
    phone: '+880212345678',
    hours: {
      0: [],
      1: [
        ['09:00', '13:00'],
        ['14:00', '18:00'],
      ],
      2: [
        ['09:00', '13:00'],
        ['14:00', '18:00'],
      ],
      3: [
        ['09:00', '13:00'],
        ['14:00', '18:00'],
      ],
      4: [
        ['09:00', '13:00'],
        ['14:00', '18:00'],
      ],
      5: [['09:00', '13:00']],
      6: [['10:00', '14:00']],
    },
  },
  [ids.londonBranchId]: {
    name: 'London clinic',
    zone: 'Europe/London',
    phone: '+442071234567',
    hours: {
      0: [],
      1: [['08:00', '17:00']],
      2: [['08:00', '17:00']],
      3: [['08:00', '17:00']],
      4: [['08:00', '17:00']],
      5: [['08:00', '17:00']],
      6: [],
    },
  },
};

export const PERFORMERS: MockPerformer[] = [
  {
    id: ids.vetRahmanId,
    name: 'Dr Rahman',
    roleIds: ['Vet'],
    branchId: ids.mainBranchId,
    days: [1, 2, 3, 4, 5, 6],
    from: '09:00',
    to: '17:00',
  },
  {
    id: ids.vetAkterId,
    name: 'Dr Akter',
    roleIds: ['Vet'],
    branchId: ids.mainBranchId,
    days: [1, 2, 3, 4, 5],
    from: '08:00',
    to: '19:00',
  },
  {
    id: ids.nurseTanvirId,
    name: 'Tanvir (nurse)',
    roleIds: ['Nurse'],
    branchId: ids.mainBranchId,
    days: [1, 2, 3, 4, 5],
    from: '10:00',
    to: '16:00',
  },
  {
    id: ids.vetWilsonId,
    name: 'Dr Wilson',
    roleIds: ['Vet'],
    branchId: ids.londonBranchId,
    days: [1, 2, 3, 4, 5],
    from: '08:00',
    to: '17:00',
  },
];

export const ROOMS: MockRoom[] = [
  { id: ids.roomAId, name: 'Consultation 1', kind: 'Consultation', branchId: ids.mainBranchId },
  { id: ids.roomBId, name: 'Consultation 2', kind: 'Consultation', branchId: ids.mainBranchId },
  { id: ids.theatreRoomId, name: 'Theatre', kind: 'Theatre', branchId: ids.mainBranchId },
  { id: ids.londonRoomId, name: 'Room L1', kind: 'Consultation', branchId: ids.londonBranchId },
];

/** Services the booking mock knows by id; any other id (e.g. one an e2e creates through the admin
 *  mock) is treated as a 30-minute vet-only consultation-room service. */
export function serviceRules(serviceId: string): {
  durationMinutes: number;
  roleIds: string[];
  name: string;
  category: string;
  priceAmount: number;
} {
  if (serviceId === ids.consultationServiceId) {
    return {
      durationMinutes: 20,
      roleIds: ['Vet', 'Nurse'],
      name: 'General consultation',
      category: 'Consultation',
      priceAmount: 1500,
    };
  }
  return {
    durationMinutes: 30,
    roleIds: ['Vet'],
    name: 'Vaccination',
    category: 'Vaccination',
    priceAmount: 800,
  };
}

export function snapshotFor(serviceId: string, nameOverride?: string): ServiceSnapshot {
  const rules = serviceRules(serviceId);
  return {
    name: nameOverride ?? rules.name,
    category: rules.category,
    durationMinutes: rules.durationMinutes,
    priceAmount: rules.priceAmount,
    priceCurrencyCode: 'BDT',
  };
}

export function zoneOf(branchId: string): string {
  return (BRANCH_INFO[branchId] ?? BRANCH_INFO[ids.mainBranchId]!).zone;
}

// ---- the database --------------------------------------------------------------------------

export interface MockAppointment {
  id: string;
  version: number;
  branchId: string;
  customerId: string;
  customerName: string;
  catId: string;
  catName: string;
  catIsHighRisk: boolean;
  serviceId: string;
  service: ServiceSnapshot;
  performerId: string | null;
  roomId: string | null;
  startsOn: string | null;
  endsOn: string | null;
  isWalkIn: boolean;
  channel: string;
  status: AppointmentStatus;
  holdExpiresOn: string | null;
  isWaitlistOffer: boolean;
  waitlistEntryId: string | null;
  reasonForVisit: string | null;
  triagePriority: TriagePriority;
  presentingComplaint: string | null;
  needsRescheduling: boolean;
  rescheduleFlagReason: string | null;
  checkedInOn: string | null;
  /** Test affordance: the no-show grace is treated as elapsed (otherwise `NoShowTooEarly`). */
  noShowEligible: boolean;
  history: Array<{ from: string; to: string; changedOn: string; changedBy: string }>;
}

export interface MockWaitlistEntry {
  id: string;
  branchId: string;
  catId: string;
  catName: string;
  serviceId: string;
  serviceName: string;
  localDate: string;
  status: string;
  offeredAppointmentId: string | null;
  offerExpiresOn: string | null;
  createdOn: string;
}

interface Database {
  appointments: MockAppointment[];
  waitlist: MockWaitlistEntry[];
  /** Race slots already "taken by someone else", so the same slot is not lost twice. */
  racedKeys: string[];
}

function localInstant(branchId: string, dateIso: string, time: string): string {
  return zonedLocalToInstant(dateIso, time, zoneOf(branchId));
}

function plusMinutes(instant: string, minutes: number): string {
  return new Date(new Date(instant).getTime() + minutes * 60_000).toISOString();
}

function baseAppointment(
  partial: Partial<MockAppointment> &
    Pick<MockAppointment, 'id' | 'customerId' | 'customerName' | 'catId' | 'catName'>,
): MockAppointment {
  const serviceId = partial.serviceId ?? ids.consultationServiceId;
  return {
    version: 1,
    branchId: ids.mainBranchId,
    catIsHighRisk: false,
    serviceId,
    service: snapshotFor(serviceId),
    performerId: null,
    roomId: null,
    startsOn: null,
    endsOn: null,
    isWalkIn: false,
    channel: 'Online',
    status: 'Booked',
    holdExpiresOn: null,
    isWaitlistOffer: false,
    waitlistEntryId: null,
    reasonForVisit: null,
    triagePriority: 'Routine',
    presentingComplaint: null,
    needsRescheduling: false,
    rescheduleFlagReason: null,
    checkedInOn: null,
    noShowEligible: false,
    history: [],
    ...partial,
  };
}

function booked(
  partial: Parameters<typeof baseAppointment>[0],
  dateIso: string,
  time: string,
): MockAppointment {
  const base = baseAppointment(partial);
  const startsOn = localInstant(base.branchId, dateIso, time);
  return {
    ...base,
    startsOn,
    endsOn: plusMinutes(startsOn, base.service.durationMinutes),
    performerId: base.performerId ?? ids.vetRahmanId,
    roomId: base.roomId ?? ids.roomAId,
  };
}

/** Seeds are relative to "today" in the main branch's zone, so a fresh page load always has a
 *  coherent Today list, a week of calendar rows and upcoming/past customer appointments. */
function seed(): Database {
  const zone = zoneOf(ids.mainBranchId);
  const today = todayInZone(zone);
  const now = serverNow();
  const at = (days: number) => addDaysIso(today, days);
  // The first Monday-to-Thursday on or after `days` ahead, so the vets are always on shift.
  const workday = (days: number) => {
    let date = at(days);
    while (![1, 2, 3, 4].includes(weekdayOf(date))) date = addDaysIso(date, 1);
    return date;
  };
  const sam = { customerId: ids.signedInCustomerId, customerName: ids.signedInCustomerName };
  const rahim = { customerId: ids.rahimCustomerId, customerName: 'Rahim Uddin' };
  const farhana = { customerId: ids.farhanaCustomerId, customerName: 'Farhana Yasmin' };

  // Locked: ~2 h from now, still "Booked" — inside the 4 h cut-off.
  const lockedStart = new Date(now + 2 * 3_600_000).toISOString();
  const locked: MockAppointment = {
    ...baseAppointment({
      id: ids.lockedAppointmentId,
      ...sam,
      catId: ids.catMomoId,
      catName: 'Momo',
      performerId: ids.vetRahmanId,
      roomId: ids.roomAId,
    }),
    startsOn: lockedStart,
    endsOn: plusMinutes(lockedStart, 20),
  };

  const appointments: MockAppointment[] = [
    locked,
    booked(
      {
        id: ids.changeableAppointmentId,
        ...sam,
        catId: ids.catMomoId,
        catName: 'Momo',
        performerId: ids.vetRahmanId,
        reasonForVisit: 'Annual check-up',
      },
      at(9),
      '11:15',
    ),
    {
      ...booked(
        {
          id: ids.pastCompletedAppointmentId,
          ...sam,
          catId: ids.catMomoId,
          catName: 'Momo',
          status: 'Completed',
        },
        at(-14),
        '10:00',
      ),
    },
    {
      ...booked(
        {
          id: ids.pastCancelledAppointmentId,
          ...sam,
          catId: ids.catMomoId,
          catName: 'Momo',
          status: 'Cancelled',
        },
        at(-5),
        '15:00',
      ),
    },
    // No-show history for Sam (US-CLN-08).
    {
      ...booked(
        {
          id: 'a4000000-0000-0000-0000-0000000000f1',
          ...sam,
          catId: ids.catMomoId,
          catName: 'Momo',
          status: 'NoShow',
        },
        at(-60),
        '09:30',
      ),
    },
    {
      ...booked(
        {
          id: 'a4000000-0000-0000-0000-0000000000f2',
          ...sam,
          catId: ids.catMomoId,
          catName: 'Momo',
          status: 'NoShow',
        },
        at(-30),
        '09:30',
      ),
    },
    // Today.
    booked(
      {
        id: ids.noShowEligibleAppointmentId,
        ...rahim,
        catId: ids.catTigerId,
        catName: 'Tiger',
        catIsHighRisk: true,
        serviceId: 's9999999-0000-0000-0000-000000000001',
        performerId: ids.vetRahmanId,
        noShowEligible: true,
      },
      today,
      '09:00',
    ),
    booked(
      {
        id: ids.todayBookedAppointmentId,
        ...farhana,
        catId: ids.catWhiskersId,
        catName: 'Whiskers',
        performerId: ids.vetAkterId,
        roomId: ids.roomBId,
      },
      today,
      '10:15',
    ),
    {
      ...booked(
        {
          id: ids.queuedBookedAppointmentId,
          ...sam,
          catId: ids.catMittensId,
          catName: 'Mittens',
          performerId: ids.vetRahmanId,
          status: 'CheckedIn',
        },
        today,
        '11:00',
      ),
      checkedInOn: new Date(now - 20 * 60_000).toISOString(),
    },
    {
      ...booked(
        {
          id: ids.inConsultationAppointmentId,
          ...rahim,
          catId: ids.catWhiskersId,
          catName: 'Whiskers',
          performerId: ids.vetAkterId,
          roomId: ids.roomBId,
          status: 'InConsultation',
        },
        today,
        '10:45',
      ),
      checkedInOn: new Date(now - 40 * 60_000).toISOString(),
    },
    {
      ...baseAppointment({
        id: ids.waitingWalkInId,
        ...farhana,
        catId: ids.catMittensId,
        catName: 'Mittens',
        isWalkIn: true,
        channel: 'WalkIn',
        status: 'CheckedIn',
        triagePriority: 'Urgent',
        presentingComplaint: 'Vomiting since last night',
      }),
      checkedInOn: new Date(now - 10 * 60_000).toISOString(),
    },
    // The coming days (calendar rows, move targets, flagged).
    booked(
      {
        id: ids.akterMidMorningAppointmentId,
        ...farhana,
        catId: ids.catWhiskersId,
        catName: 'Whiskers',
        performerId: ids.vetAkterId,
        roomId: ids.roomBId,
      },
      workday(1),
      '11:30',
    ),
    booked(
      {
        id: ids.movableAppointmentId,
        ...rahim,
        catId: ids.catTigerId,
        catName: 'Tiger',
        catIsHighRisk: true,
        performerId: ids.vetRahmanId,
        roomId: ids.roomAId,
      },
      workday(1),
      '10:00',
    ),
    {
      ...booked(
        {
          id: ids.flaggedAppointmentId,
          ...farhana,
          catId: ids.catWhiskersId,
          catName: 'Whiskers',
          performerId: ids.vetRahmanId,
        },
        workday(3),
        '14:00',
      ),
      needsRescheduling: true,
      rescheduleFlagReason: 'PerformerOnLeave',
    },
  ];

  const offerStart = localInstant(ids.mainBranchId, at(4), '10:00');
  const offer: MockAppointment = {
    ...baseAppointment({
      id: ids.offerAppointmentId,
      ...sam,
      catId: ids.catMomoId,
      catName: 'Momo',
      status: 'Held',
      channel: 'Waitlist',
      performerId: ids.vetRahmanId,
      roomId: ids.roomAId,
      isWaitlistOffer: true,
      waitlistEntryId: ids.waitlistOfferedId,
    }),
    startsOn: offerStart,
    endsOn: plusMinutes(offerStart, 20),
    holdExpiresOn: new Date(now + 25 * 60_000).toISOString(),
  };
  const expiredOffer: MockAppointment = {
    ...offer,
    id: ids.expiredOfferAppointmentId,
    status: 'HoldExpired',
    holdExpiresOn: new Date(now - 5 * 60_000).toISOString(),
    waitlistEntryId: ids.expiredWaitlistOfferedId,
    startsOn: localInstant(ids.mainBranchId, at(5), '10:00'),
    endsOn: localInstant(ids.mainBranchId, at(5), '10:20'),
  };
  appointments.push(offer, expiredOffer);

  const waitlist: MockWaitlistEntry[] = [
    {
      id: ids.waitlistWaitingId,
      branchId: ids.mainBranchId,
      catId: ids.catMomoId,
      catName: 'Momo',
      serviceId: ids.consultationServiceId,
      serviceName: 'General consultation',
      localDate: at(6),
      status: 'Waiting',
      offeredAppointmentId: null,
      offerExpiresOn: null,
      createdOn: new Date(now - 3_600_000).toISOString(),
    },
    {
      id: ids.waitlistOfferedId,
      branchId: ids.mainBranchId,
      catId: ids.catMomoId,
      catName: 'Momo',
      serviceId: ids.consultationServiceId,
      serviceName: 'General consultation',
      localDate: at(4),
      status: 'Offered',
      offeredAppointmentId: ids.offerAppointmentId,
      offerExpiresOn: offer.holdExpiresOn,
      createdOn: new Date(now - 2 * 3_600_000).toISOString(),
    },
    {
      id: ids.waitlistLapsedId,
      branchId: ids.mainBranchId,
      catId: ids.catMomoId,
      catName: 'Momo',
      serviceId: ids.consultationServiceId,
      serviceName: 'General consultation',
      localDate: at(2),
      status: 'Lapsed',
      offeredAppointmentId: null,
      offerExpiresOn: null,
      createdOn: new Date(now - 24 * 3_600_000).toISOString(),
    },
    {
      id: ids.expiredWaitlistOfferedId,
      branchId: ids.mainBranchId,
      catId: ids.catMomoId,
      catName: 'Momo',
      serviceId: ids.consultationServiceId,
      serviceName: 'General consultation',
      localDate: at(5),
      status: 'Lapsed',
      offeredAppointmentId: ids.expiredOfferAppointmentId,
      offerExpiresOn: expiredOffer.holdExpiresOn,
      createdOn: new Date(now - 30 * 3_600_000).toISOString(),
    },
  ];

  return { appointments, waitlist, racedKeys: [] };
}

let db: Database = loadPersisted<Database>(PERSIST_KEY) ?? seed();

export function getDb(): Database {
  return db;
}

export function persist(): void {
  savePersisted(PERSIST_KEY, db);
}

export function resetAppointmentsMockData(): void {
  clearPersisted(PERSIST_KEY);
  db = seed();
}

export function findAppointment(id: string): MockAppointment | undefined {
  return db.appointments.find((a) => a.id === id);
}

// ---- derived views --------------------------------------------------------------------------

const ALLOWED: Record<string, AppointmentAction[]> = {
  Held: ['Confirm', 'ReleaseHold', 'ExpireHold'],
  Booked: ['Reschedule', 'Cancel', 'CheckIn', 'RecordNoShow'],
  CheckedIn: ['Cancel', 'SetTriagePriority', 'Start'],
  InConsultation: ['Complete'],
};

export function allowedActions(status: AppointmentStatus): AppointmentAction[] {
  return ALLOWED[status] ?? [];
}

export function isStaleHold(a: MockAppointment): boolean {
  return (
    a.status === 'Held' &&
    a.holdExpiresOn !== null &&
    new Date(a.holdExpiresOn).getTime() < serverNow()
  );
}

/** Occupies its performer, room and cat (HO-APT-001 §8.4). A stale hold does not. */
export function occupies(a: MockAppointment): boolean {
  if (a.isWalkIn || !a.startsOn || !a.endsOn) return false;
  if (a.status === 'Held') return !isStaleHold(a);
  return ['Booked', 'CheckedIn', 'InConsultation'].includes(a.status);
}

/** Lazily turns an expired `Held` appointment into `HoldExpired`, the way the API's read side does. */
export function sweepStaleHolds(): void {
  for (const a of db.appointments) {
    if (isStaleHold(a)) a.status = 'HoldExpired';
  }
}

export function cutoffFor(a: MockAppointment): {
  canChange: boolean;
  changeCutoffOn: string | null;
} {
  if (!a.startsOn) return { canChange: false, changeCutoffOn: null };
  const cutoff = new Date(a.startsOn).getTime() - CHANGE_CUTOFF_HOURS * 3_600_000;
  return {
    canChange: a.status === 'Booked' && serverNow() <= cutoff,
    changeCutoffOn: new Date(cutoff).toISOString(),
  };
}

export function performerName(id: string | null): string | null {
  return PERFORMERS.find((p) => p.id === id)?.name ?? null;
}

export function roomName(id: string | null): string | null {
  return ROOMS.find((r) => r.id === id)?.name ?? null;
}

export function toSummary(a: MockAppointment): AppointmentSummary {
  const cutoff = cutoffFor(a);
  return {
    id: a.id,
    catId: a.catId,
    catName: a.catName,
    service: a.service,
    startsOn: a.startsOn,
    endsOn: a.endsOn,
    performerName: performerName(a.performerId),
    branch: {
      id: a.branchId,
      name: BRANCH_INFO[a.branchId]?.name ?? 'Clinic',
      phoneNumber: BRANCH_INFO[a.branchId]?.phone ?? null,
    },
    branchTimeZoneId: zoneOf(a.branchId),
    status: a.status,
    ...cutoff,
  };
}

export function toDetail(a: MockAppointment): AppointmentDetail {
  const cutoff = cutoffFor(a);
  return {
    id: a.id,
    branchId: a.branchId,
    branchTimeZoneId: zoneOf(a.branchId),
    customerId: a.customerId,
    catId: a.catId,
    catName: a.catName,
    service: a.service,
    performerId: a.performerId,
    performerName: performerName(a.performerId),
    roomId: a.roomId,
    roomName: roomName(a.roomId),
    startsOn: a.startsOn,
    endsOn: a.endsOn,
    isWalkIn: a.isWalkIn,
    channel: a.channel,
    status: a.status,
    holdExpiresOn: a.holdExpiresOn,
    isWaitlistOffer: a.isWaitlistOffer,
    reasonForVisit: a.reasonForVisit,
    triagePriority: a.triagePriority,
    presentingComplaint: a.presentingComplaint,
    version: a.version,
    needsRescheduling: a.needsRescheduling,
    rescheduleFlagReason: a.rescheduleFlagReason,
    ...cutoff,
    statusHistory: a.history,
    allowedTransitions: allowedActions(a.status),
  };
}

/** The customer's own projection (backlog #26): `toDetail` minus every staff-only field. */
export function toCustomerDetail(a: MockAppointment): MyAppointmentDetail {
  const cutoff = cutoffFor(a);
  return {
    id: a.id,
    branchId: a.branchId,
    branchTimeZoneId: zoneOf(a.branchId),
    catId: a.catId,
    catName: a.catName,
    service: a.service,
    performerId: a.performerId,
    performerName: performerName(a.performerId),
    startsOn: a.startsOn,
    endsOn: a.endsOn,
    status: a.status,
    holdExpiresOn: a.holdExpiresOn,
    isWaitlistOffer: a.isWaitlistOffer,
    reasonForVisit: a.reasonForVisit,
    needsRescheduling: a.needsRescheduling,
    rescheduleFlagReason: a.rescheduleFlagReason,
    ...cutoff,
    statusHistory: a.history.map(({ from, to, changedOn }) => ({ from, to, changedOn })),
  };
}

export function setStatus(a: MockAppointment, to: AppointmentStatus): void {
  a.history.push({
    from: a.status,
    to,
    changedOn: new Date(serverNow()).toISOString(),
    changedBy: ids.signedInCustomerId,
  });
  a.status = to;
  a.version += 1;
}

// ---- the mock server's slot rules ------------------------------------------------------------

interface Interval {
  start: number;
  end: number;
}

function weekdayOf(dateIso: string): number {
  const [y, m, d] = dateIso.split('-').map(Number) as [number, number, number];
  return new Date(Date.UTC(y, m - 1, d)).getUTCDay();
}

/** A holiday and a full day, relative to today, for the main branch (§12 fixtures). The first
 *  open day at least three days ahead is "full"; the first open day at least five ahead is a
 *  public holiday. */
export function specialDays(): { fullDay: string; holiday: string } {
  const today = todayInZone(zoneOf(ids.mainBranchId));
  const open = (offset: number) => {
    let date = addDaysIso(today, offset);
    while ((BRANCH_INFO[ids.mainBranchId]!.hours[weekdayOf(date)] ?? []).length === 0) {
      date = addDaysIso(date, 1);
    }
    return date;
  };
  const fullDay = open(3);
  let holiday = open(5);
  if (holiday === fullDay) holiday = open(Math.max(6, 4 + (weekdayOf(fullDay) % 3)));
  if (holiday === fullDay) holiday = addDaysIso(fullDay, 1);
  return { fullDay, holiday };
}

function toInterval(branchId: string, dateIso: string, from: string, to: string): Interval {
  return {
    start: new Date(localInstant(branchId, dateIso, from)).getTime(),
    end: new Date(localInstant(branchId, dateIso, to)).getTime(),
  };
}

function intersect(a: Interval[], b: Interval[]): Interval[] {
  const out: Interval[] = [];
  for (const x of a) {
    for (const y of b) {
      const start = Math.max(x.start, y.start);
      const end = Math.min(x.end, y.end);
      if (end > start) out.push({ start, end });
    }
  }
  return out;
}

export function openingIntervals(branchId: string, dateIso: string): Interval[] {
  const info = BRANCH_INFO[branchId];
  if (!info) return [];
  return (info.hours[weekdayOf(dateIso)] ?? []).map(([from, to]) =>
    toInterval(branchId, dateIso, from, to),
  );
}

export function shiftIntervals(performer: MockPerformer, dateIso: string): Interval[] {
  if (!performer.days.includes(weekdayOf(dateIso))) return [];
  return [toInterval(performer.branchId, dateIso, performer.from, performer.to)];
}

export function isClosedDate(branchId: string, dateIso: string): boolean {
  if (branchId !== ids.mainBranchId) return false;
  return specialDays().holiday === dateIso;
}

export function isFullDate(branchId: string, dateIso: string): boolean {
  return branchId === ids.mainBranchId && specialDays().fullDay === dateIso;
}

export interface MockSlot {
  startMs: number;
  endMs: number;
  performer: MockPerformer;
  room: MockRoom;
  outsideOpeningHours: boolean;
}

export function computeSlots(input: {
  branchId: string;
  serviceId: string;
  date: string;
  performerId?: string | null;
  catId?: string | null;
  mode: 'online' | 'staff';
  excludeAppointmentId?: string;
}): MockSlot[] {
  sweepStaleHolds();
  const { branchId, serviceId, date, mode } = input;
  if (isClosedDate(branchId, date)) return [];
  if (mode === 'online' && isFullDate(branchId, date)) return [];
  const rules = serviceRules(serviceId);
  const duration = rules.durationMinutes * 60_000;
  const midnight = new Date(localInstant(branchId, date, '00:00')).getTime();
  const earliest = mode === 'online' ? serverNow() + MIN_NOTICE_MINUTES * 60_000 : serverNow();
  const opening = openingIntervals(branchId, date);

  const busy = db.appointments.filter(
    (a) => a.id !== input.excludeAppointmentId && occupies(a) && a.branchId === branchId,
  );
  const overlaps = (a: MockAppointment, start: number, end: number) =>
    new Date(a.startsOn!).getTime() < end && new Date(a.endsOn!).getTime() > start;

  const slots: MockSlot[] = [];
  for (const performer of PERFORMERS) {
    if (performer.branchId !== branchId) continue;
    if (input.performerId && performer.id !== input.performerId) continue;
    if (!performer.roleIds.some((role) => rules.roleIds.includes(role))) continue;
    const shift = shiftIntervals(performer, date);
    const within = mode === 'online' ? intersect(shift, opening) : shift;
    for (let minute = 0; minute < 24 * 60; minute += GRID_MINUTES) {
      const start = midnight + minute * 60_000;
      const end = start + duration;
      if (start < earliest) continue;
      if (!within.some((i) => start >= i.start && end <= i.end)) continue;
      if (busy.some((a) => a.performerId === performer.id && overlaps(a, start, end))) continue;
      if (input.catId && busy.some((a) => a.catId === input.catId && overlaps(a, start, end))) {
        continue;
      }
      const freeRoom = ROOMS.find(
        (r) =>
          r.branchId === branchId &&
          r.kind === 'Consultation' &&
          !busy.some((a) => a.roomId === r.id && overlaps(a, start, end)),
      );
      if (!freeRoom) continue;
      slots.push({
        startMs: start,
        endMs: end,
        performer,
        room: freeRoom,
        outsideOpeningHours: !opening.some((i) => start >= i.start && end <= i.end),
      });
    }
  }
  slots.sort((a, b) => a.startMs - b.startMs || a.performer.name.localeCompare(b.performer.name));
  return slots;
}

export function raceKey(branchId: string, startsOn: string): string {
  return `${branchId}|${new Date(startsOn).toISOString()}`;
}

/** The deterministic lost race: the first claim of a `RACE_LOCAL_TIME` slot books it for someone
 *  else (every free performer at that start), then reports it taken. */
export function shouldLoseRace(branchId: string, startsOn: string): boolean {
  const zone = zoneOf(branchId);
  if (timeOnlyInZone(startsOn, zone) !== RACE_LOCAL_TIME) return false;
  return !db.racedKeys.includes(raceKey(branchId, startsOn));
}

export function takeSlotByOtherCustomer(
  branchId: string,
  startsOn: string,
  serviceId: string,
): void {
  db.racedKeys.push(raceKey(branchId, startsOn));
  const date = localDateOf(startsOn, zoneOf(branchId));
  const startMs = new Date(startsOn).getTime();
  const slots = computeSlots({ branchId, serviceId, date, mode: 'online' }).filter(
    (s) => s.startMs === startMs,
  );
  for (const slot of slots) {
    db.appointments.push(
      baseAppointment({
        id: crypto.randomUUID(),
        customerId: ids.otherCustomerId,
        customerName: 'Another Customer',
        catId: crypto.randomUUID(),
        catName: 'Someone else’s cat',
        branchId,
        serviceId,
        service: snapshotFor(serviceId),
        performerId: slot.performer.id,
        roomId: slot.room.id,
        startsOn: new Date(slot.startMs).toISOString(),
        endsOn: new Date(slot.endMs).toISOString(),
      }),
    );
  }
  persist();
}

/** A performer/room/cat clash for a candidate range (staff routes name the conflicting time). */
export function findConflict(input: {
  appointmentId?: string;
  branchId: string;
  performerId: string;
  roomId: string;
  catId: string;
  startsOn: string;
  endsOn: string;
}): { code: string; message: string; property: string } | null {
  const zone = zoneOf(input.branchId);
  const start = new Date(input.startsOn).getTime();
  const end = new Date(input.endsOn).getTime();
  const clash = (a: MockAppointment) =>
    a.id !== input.appointmentId &&
    occupies(a) &&
    new Date(a.startsOn!).getTime() < end &&
    new Date(a.endsOn!).getTime() > start;
  const range = (a: MockAppointment) =>
    `${timeOnlyInZone(a.startsOn!, zone)}–${timeOnlyInZone(a.endsOn!, zone)}`;

  const performer = db.appointments.find((a) => clash(a) && a.performerId === input.performerId);
  if (performer) {
    return {
      code: 'PerformerBusy',
      property: 'PerformerId',
      message: `${performerName(input.performerId) ?? 'This performer'} already has an appointment ${range(performer)}.`,
    };
  }
  const room = db.appointments.find((a) => clash(a) && a.roomId === input.roomId);
  if (room) {
    return {
      code: 'RoomBusy',
      property: 'RoomId',
      message: `${roomName(input.roomId) ?? 'This room'} already has an appointment ${range(room)}.`,
    };
  }
  const cat = db.appointments.find((a) => clash(a) && a.catId === input.catId);
  if (cat) {
    return {
      code: 'CatAlreadyBooked',
      property: 'CatId',
      message: `This cat already has an appointment ${range(cat)}.`,
    };
  }
  return null;
}

/** Is the performer on shift for the whole range? (`PerformerNotAvailable` otherwise.) */
export function performerOnShift(performerId: string, startsOn: string, endsOn: string): boolean {
  const performer = PERFORMERS.find((p) => p.id === performerId);
  if (!performer) return false;
  const zone = zoneOf(performer.branchId);
  const date = localDateOf(startsOn, zone);
  const start = new Date(startsOn).getTime();
  const end = new Date(endsOn).getTime();
  return shiftIntervals(performer, date).some((i) => start >= i.start && end <= i.end);
}

export const NO_SHOW_GRACE = NO_SHOW_GRACE_MINUTES;
