import { http, type HttpHandler } from 'msw';
import { addDaysIso, dateRange } from '@/lib/format/appointment';
import { localDateOf, todayInZone } from '@/lib/format/time';
import type { CalendarAppointment } from '@/lib/api/staff-appointments';
import type { QueueEntry } from '@/lib/api/front-desk';
import {
  appointmentsFixtureIds as ids,
  apiUrl,
  json,
  noContent,
  notFound,
  serverNow,
  violation,
} from './fixtures';
import {
  allowedActions,
  BRANCH_INFO,
  computeSlots,
  findAppointment,
  findConflict,
  getDb,
  isClosedDate,
  NO_SHOW_GRACE,
  openingIntervals,
  performerName,
  performerOnShift,
  persist,
  PERFORMERS,
  ROOMS,
  serviceRules,
  setStatus,
  shiftIntervals,
  snapshotFor,
  sweepStaleHolds,
  toDetail,
  toSummary,
  zoneOf,
  type MockAppointment,
} from './store';
import { shouldLoseRace, takeSlotByOtherCustomer } from './store';

/**
 * `mocks/handlers/appointments/staff.ts` — HO-APT-002 §12, staff endpoints (HO-APT-001 §12.4):
 * staff slots, calendar, day list, detail, every command, the queue, walk-ins, flagged and the
 * customer summary. The branch comes from `X-Branch-Id` (main branch when absent). Move refusals
 * carry the specific reason; a stale `expectedVersion` is a `409`.
 */

function branchOf(request: Request): string {
  const header = request.headers.get('X-Branch-Id');
  return header && BRANCH_INFO[header] ? header : ids.mainBranchId;
}

async function lookupName(path: string, fallback: string): Promise<string> {
  try {
    const response = await fetch(apiUrl(path), { credentials: 'include' });
    if (response.ok) {
      const body = (await response.json()) as { name?: string; fullName?: string };
      return body.name ?? body.fullName ?? fallback;
    }
  } catch {
    // fall through
  }
  return fallback;
}

function toCalendarAppointment(a: MockAppointment): CalendarAppointment {
  return {
    id: a.id,
    version: a.version,
    catId: a.catId,
    catName: a.catName,
    customerId: a.customerId,
    customerName: a.customerName,
    serviceName: a.service.name,
    startsOn: a.startsOn,
    endsOn: a.endsOn,
    performerId: a.performerId,
    roomId: a.roomId,
    status: a.status,
    isWalkIn: a.isWalkIn,
    needsRescheduling: a.needsRescheduling,
    catIsHighRisk: a.catIsHighRisk,
  };
}

const VISIBLE_STATUSES = ['Booked', 'CheckedIn', 'InConsultation', 'Completed', 'NoShow'];

function inBranchRange(a: MockAppointment, branchId: string, from: string, to: string): boolean {
  if (a.branchId !== branchId || !a.startsOn || !VISIBLE_STATUSES.includes(a.status)) return false;
  const date = localDateOf(a.startsOn, zoneOf(branchId));
  return date >= from && date <= to;
}

const PRIORITY_RANK: Record<string, number> = { Emergency: 3, Urgent: 2, Routine: 1 };

function buildQueue(branchId: string, performerId: string | null) {
  sweepStaleHolds();
  const entries = getDb()
    .appointments.filter(
      (a) =>
        a.branchId === branchId &&
        ['CheckedIn', 'InConsultation'].includes(a.status) &&
        (!performerId || a.performerId === performerId || a.performerId === null),
    )
    .map((a) => ({ a, due: a.isWalkIn ? a.checkedInOn! : (a.startsOn ?? a.checkedInOn!) }));
  entries.sort(
    (x, y) =>
      (PRIORITY_RANK[y.a.triagePriority] ?? 0) - (PRIORITY_RANK[x.a.triagePriority] ?? 0) ||
      new Date(x.due).getTime() - new Date(y.due).getTime() ||
      new Date(x.a.checkedInOn ?? 0).getTime() - new Date(y.a.checkedInOn ?? 0).getTime(),
  );
  const performersOnShift = 2;
  const inConsultation = entries.filter((e) => e.a.status === 'InConsultation').length;
  let aheadMinutes = inConsultation * 15;
  const result: QueueEntry[] = entries.map(({ a, due }, index) => {
    const waiting = a.status === 'CheckedIn';
    const estimate = waiting ? Math.ceil(aheadMinutes / performersOnShift / 5) * 5 : null;
    if (waiting) aheadMinutes += a.service.durationMinutes;
    return {
      appointmentId: a.id,
      position: index + 1,
      catName: a.catName,
      catIsHighRisk: a.catIsHighRisk,
      customerName: a.customerName,
      service: a.service.name,
      triagePriority: a.triagePriority,
      isWalkIn: a.isWalkIn,
      dueOn: due,
      checkedInOn: a.checkedInOn,
      status: a.status,
      performerName: performerName(a.performerId),
      estimatedWaitMinutes: estimate,
    };
  });
  return { generatedOn: new Date(serverNow()).toISOString(), performersOnShift, entries: result };
}

function refuseTransition(a: MockAppointment, action: string, text: string) {
  if (allowedActions(a.status).includes(action as never)) return null;
  return violation(400, {
    propertyName: 'AppointmentId',
    errorCode: 'InvalidAppointmentTransition',
    errorMessage: text,
  });
}

export const appointmentsStaffHandlers: HttpHandler[] = [
  // GET /api/staff/appointments/slots
  http.get('*/api/staff/appointments/slots', ({ request }) => {
    const url = new URL(request.url);
    const branchId = branchOf(request);
    const slots = computeSlots({
      branchId,
      serviceId: url.searchParams.get('serviceId') ?? ids.consultationServiceId,
      date: url.searchParams.get('date') ?? '',
      performerId: url.searchParams.get('performerId'),
      catId: url.searchParams.get('catId'),
      mode: 'staff',
    });
    return json(
      slots.map((s) => ({
        startsOn: new Date(s.startMs).toISOString(),
        endsOn: new Date(s.endMs).toISOString(),
        performerId: s.performer.id,
        performerName: s.performer.name,
        roomId: s.room.id,
        roomName: s.room.name,
        outsideOpeningHours: s.outsideOpeningHours,
      })),
    );
  }),

  // GET /api/staff/appointments/calendar
  http.get('*/api/staff/appointments/calendar', ({ request }) => {
    const url = new URL(request.url);
    const branchId = branchOf(request);
    const zone = zoneOf(branchId);
    const from = url.searchParams.get('from') ?? todayInZone(zone);
    const to = url.searchParams.get('to') ?? from;
    const view = url.searchParams.get('view') ?? 'performer';
    const wanted = (url.searchParams.get('ids') ?? '').split(',').filter(Boolean);
    const performers = PERFORMERS.filter((p) => p.branchId === branchId);
    const rooms = ROOMS.filter((r) => r.branchId === branchId);
    const resources = (view === 'room' ? rooms : performers)
      .filter((r) => wanted.length === 0 || wanted.includes(r.id))
      .map((r) => ({ id: r.id, name: r.name }));

    const days = dateRange(from, Math.max(1, daysBetween(from, to) + 1));
    const availability = performers.flatMap((performer) =>
      days.flatMap((date) => {
        const opening = openingIntervals(branchId, date);
        return shiftIntervals(performer, date).flatMap((shift) => {
          const inside = opening
            .map((o) => ({
              start: Math.max(o.start, shift.start),
              end: Math.min(o.end, shift.end),
            }))
            .filter((i) => i.end > i.start);
          const pieces: Array<{ start: number; end: number; outside: boolean }> = inside.map(
            (i) => ({
              ...i,
              outside: false,
            }),
          );
          let cursor = shift.start;
          for (const i of [...inside].sort((a, b) => a.start - b.start)) {
            if (i.start > cursor) pieces.push({ start: cursor, end: i.start, outside: true });
            cursor = Math.max(cursor, i.end);
          }
          if (cursor < shift.end) pieces.push({ start: cursor, end: shift.end, outside: true });
          if (inside.length === 0)
            return [{ start: shift.start, end: shift.end, outside: true }].map(
              toAvailability(performer.id),
            );
          return pieces.map(toAvailability(performer.id));
        });
      }),
    );

    const closures = days
      .filter((date) => isClosedDate(branchId, date))
      .map((date) => ({
        startsOn: date,
        endsOn: date,
        kind: 'PublicHoliday',
        reason: 'Public holiday',
      }));

    sweepStaleHolds();
    const appointments = getDb()
      .appointments.filter((a) => inBranchRange(a, branchId, from, to))
      .map(toCalendarAppointment);
    return json({ gridMinutes: 15, resources, availability, closures, appointments });
  }),

  // GET /api/staff/appointments/day
  http.get('*/api/staff/appointments/day', ({ request }) => {
    const branchId = branchOf(request);
    const date = new URL(request.url).searchParams.get('date') ?? todayInZone(zoneOf(branchId));
    sweepStaleHolds();
    const rows = getDb()
      .appointments.filter((a) => inBranchRange(a, branchId, date, date))
      .sort((a, b) => new Date(a.startsOn!).getTime() - new Date(b.startsOn!).getTime());
    return json(rows.map(toCalendarAppointment));
  }),

  // GET /api/staff/appointments/flagged — registered before the :id route
  http.get('*/api/staff/appointments/flagged', ({ request }) => {
    const branchId = branchOf(request);
    const rows = getDb()
      .appointments.filter(
        (a) =>
          a.branchId === branchId &&
          a.needsRescheduling &&
          a.status === 'Booked' &&
          a.startsOn !== null &&
          new Date(a.startsOn).getTime() > serverNow(),
      )
      .sort((a, b) => new Date(a.startsOn!).getTime() - new Date(b.startsOn!).getTime());
    return json(
      rows.map((a) => ({
        id: a.id,
        catId: a.catId,
        catName: a.catName,
        startsOn: a.startsOn,
        rescheduleFlagReason: a.rescheduleFlagReason,
        rescheduleFlagSourceId: null,
      })),
    );
  }),

  // GET /api/staff/appointments/{id}
  http.get('*/api/staff/appointments/:appointmentId', ({ params }) => {
    sweepStaleHolds();
    const found = findAppointment(params.appointmentId as string);
    return found ? json(toDetail(found)) : notFound();
  }),

  // POST /api/staff/appointments — C6
  http.post('*/api/staff/appointments', async ({ request }) => {
    const branchId = branchOf(request);
    const body = (await request.json()) as {
      customerId: string;
      catId: string;
      serviceId: string;
      startsOn: string;
      performerId: string;
      roomId?: string | null;
      reasonForVisit?: string | null;
    };
    const rules = serviceRules(body.serviceId);
    const endsOn = new Date(
      new Date(body.startsOn).getTime() + rules.durationMinutes * 60_000,
    ).toISOString();
    if (shouldLoseRace(branchId, body.startsOn)) {
      takeSlotByOtherCustomer(branchId, body.startsOn, body.serviceId);
      return violation(409, {
        propertyName: 'StartsOn',
        errorCode: 'SlotNoLongerAvailable',
        errorMessage: 'That time is no longer available.',
      });
    }
    const roomId =
      body.roomId ??
      ROOMS.find((r) => r.branchId === branchId && r.kind === 'Consultation')?.id ??
      ids.roomAId;
    if (!performerOnShift(body.performerId, body.startsOn, endsOn)) {
      return violation(400, {
        propertyName: 'PerformerId',
        errorCode: 'PerformerNotAvailable',
        errorMessage: `${performerName(body.performerId) ?? 'This performer'} is not available at that time.`,
      });
    }
    const conflict = findConflict({
      branchId,
      performerId: body.performerId,
      roomId,
      catId: body.catId,
      startsOn: body.startsOn,
      endsOn,
    });
    if (conflict) {
      return violation(400, {
        propertyName: conflict.property,
        errorCode: conflict.code,
        errorMessage: conflict.message,
      });
    }
    const id = crypto.randomUUID();
    getDb().appointments.push({
      id,
      version: 1,
      branchId,
      customerId: body.customerId,
      customerName: await lookupName(`/api/staff/customers/${body.customerId}`, 'Customer'),
      catId: body.catId,
      catName: await lookupName(`/api/cats/${body.catId}`, 'Cat'),
      catIsHighRisk: false,
      serviceId: body.serviceId,
      service: snapshotFor(body.serviceId),
      performerId: body.performerId,
      roomId,
      startsOn: body.startsOn,
      endsOn,
      isWalkIn: false,
      channel: 'Reception',
      status: 'Booked',
      holdExpiresOn: null,
      isWaitlistOffer: false,
      waitlistEntryId: null,
      reasonForVisit: body.reasonForVisit?.trim() || null,
      triagePriority: 'Routine',
      presentingComplaint: null,
      needsRescheduling: false,
      rescheduleFlagReason: null,
      checkedInOn: null,
      noShowEligible: false,
      history: [],
    });
    persist();
    return json({ appointmentId: id }, 201);
  }),

  // POST /api/staff/appointments/{id}/move — C7
  http.post('*/api/staff/appointments/:appointmentId/move', async ({ params, request }) => {
    const found = findAppointment(params.appointmentId as string);
    if (!found) return notFound();
    const body = (await request.json()) as {
      expectedVersion: number;
      startsOn: string;
      performerId: string;
      roomId: string;
    };
    if (body.expectedVersion !== found.version) {
      return violation(409, {
        propertyName: 'ExpectedVersion',
        errorCode: 'StaleCalendarVersion',
        errorMessage: 'This appointment changed since you loaded the calendar.',
      });
    }
    const refused = refuseTransition(
      found,
      'Reschedule',
      'Only a booked appointment can be moved.',
    );
    if (refused) return refused;
    const endsOn = new Date(
      new Date(body.startsOn).getTime() + found.service.durationMinutes * 60_000,
    ).toISOString();
    if (!performerOnShift(body.performerId, body.startsOn, endsOn)) {
      return violation(400, {
        propertyName: 'PerformerId',
        errorCode: 'PerformerNotAvailable',
        errorMessage: `${performerName(body.performerId) ?? 'This performer'} is not on shift at that time.`,
      });
    }
    const conflict = findConflict({
      appointmentId: found.id,
      branchId: found.branchId,
      performerId: body.performerId,
      roomId: body.roomId,
      catId: found.catId,
      startsOn: body.startsOn,
      endsOn,
    });
    if (conflict) {
      return violation(400, {
        propertyName: conflict.property,
        errorCode: conflict.code,
        errorMessage: conflict.message,
      });
    }
    found.startsOn = body.startsOn;
    found.endsOn = endsOn;
    found.performerId = body.performerId;
    found.roomId = body.roomId;
    found.needsRescheduling = false;
    found.rescheduleFlagReason = null;
    found.version += 1;
    persist();
    return json(toDetail(found));
  }),

  // POST /api/staff/appointments/{id}/cancel — C5
  http.post('*/api/staff/appointments/:appointmentId/cancel', async ({ params, request }) => {
    const found = findAppointment(params.appointmentId as string);
    if (!found) return notFound();
    const body = (await request.json().catch(() => ({}))) as { reason?: string | null };
    if (!body.reason?.trim()) {
      return violation(400, {
        propertyName: 'Reason',
        errorCode: 'CancellationReasonRequired',
        errorMessage: 'Enter a reason for cancelling.',
      });
    }
    const refused = refuseTransition(
      found,
      'Cancel',
      `A ${found.status.toLowerCase()} appointment cannot be cancelled.`,
    );
    if (refused) return refused;
    setStatus(found, 'Cancelled');
    persist();
    return noContent();
  }),

  // POST /api/staff/appointments/{id}/check-in — C9
  http.post('*/api/staff/appointments/:appointmentId/check-in', async ({ params, request }) => {
    const found = findAppointment(params.appointmentId as string);
    if (!found) return notFound();
    const body = (await request.json().catch(() => ({}))) as { triagePriority?: string | null };
    const refused = refuseTransition(
      found,
      'CheckIn',
      `A ${found.status.toLowerCase()} appointment cannot be checked in.`,
    );
    if (refused) return refused;
    if (
      found.startsOn &&
      localDateOf(found.startsOn, zoneOf(found.branchId)) !== todayInZone(zoneOf(found.branchId))
    ) {
      return violation(400, {
        propertyName: 'AppointmentId',
        errorCode: 'CheckInNotToday',
        errorMessage: 'This appointment can only be checked in on the day it is booked for.',
      });
    }
    if (body.triagePriority) found.triagePriority = body.triagePriority as never;
    found.checkedInOn = new Date(serverNow()).toISOString();
    setStatus(found, 'CheckedIn');
    persist();
    return noContent();
  }),

  // PUT /api/staff/appointments/{id}/triage — C10
  http.put('*/api/staff/appointments/:appointmentId/triage', async ({ params, request }) => {
    const found = findAppointment(params.appointmentId as string);
    if (!found) return notFound();
    const body = (await request.json()) as { triagePriority: string };
    const refused = refuseTransition(
      found,
      'SetTriagePriority',
      `A ${found.status.toLowerCase()} appointment cannot have its triage changed.`,
    );
    if (refused) return refused;
    found.triagePriority = body.triagePriority as never;
    found.version += 1;
    persist();
    return noContent();
  }),

  // POST /api/staff/appointments/{id}/start — C11
  http.post('*/api/staff/appointments/:appointmentId/start', async ({ params, request }) => {
    const found = findAppointment(params.appointmentId as string);
    if (!found) return notFound();
    const body = (await request.json().catch(() => ({}))) as {
      performerId?: string | null;
      roomId?: string | null;
    };
    const refused = refuseTransition(
      found,
      'Start',
      `A ${found.status.toLowerCase()} appointment cannot be started.`,
    );
    if (refused) return refused;
    const performerId = body.performerId ?? found.performerId;
    const roomId = body.roomId ?? found.roomId;
    if (!performerId || !roomId) {
      return violation(400, {
        propertyName: 'PerformerId',
        errorCode: 'PerformerNotAvailable',
        errorMessage: 'Choose a vet and a room to start this walk-in.',
      });
    }
    const performer = PERFORMERS.find((p) => p.id === performerId);
    if (
      performer &&
      !performer.roleIds.some((r) => serviceRules(found.serviceId).roleIds.includes(r))
    ) {
      return violation(400, {
        propertyName: 'PerformerId',
        errorCode: 'PerformerNotQualified',
        errorMessage: `${performer.name} is not qualified for ${found.service.name}.`,
      });
    }
    const room = ROOMS.find((r) => r.id === roomId);
    if (room && room.kind !== 'Consultation') {
      return violation(400, {
        propertyName: 'RoomId',
        errorCode: 'RoomKindMismatch',
        errorMessage: `${room.name} is not the right kind of room for ${found.service.name}.`,
      });
    }
    found.performerId = performerId;
    found.roomId = roomId;
    setStatus(found, 'InConsultation');
    persist();
    return noContent();
  }),

  // POST /api/staff/appointments/{id}/complete — C12
  http.post('*/api/staff/appointments/:appointmentId/complete', ({ params }) => {
    const found = findAppointment(params.appointmentId as string);
    if (!found) return notFound();
    const refused = refuseTransition(
      found,
      'Complete',
      `A ${found.status.toLowerCase()} appointment cannot be completed.`,
    );
    if (refused) return refused;
    setStatus(found, 'Completed');
    persist();
    return noContent();
  }),

  // POST /api/staff/appointments/{id}/no-show — C13
  http.post('*/api/staff/appointments/:appointmentId/no-show', ({ params }) => {
    const found = findAppointment(params.appointmentId as string);
    if (!found) return notFound();
    const refused = refuseTransition(
      found,
      'RecordNoShow',
      `A ${found.status.toLowerCase()} appointment cannot be marked a no-show.`,
    );
    if (refused) return refused;
    const graceOver =
      found.noShowEligible ||
      (found.startsOn !== null &&
        serverNow() >= new Date(found.startsOn).getTime() + NO_SHOW_GRACE * 60_000);
    if (!graceOver) {
      return violation(400, {
        propertyName: 'AppointmentId',
        errorCode: 'NoShowTooEarly',
        errorMessage: `A no-show can only be recorded ${NO_SHOW_GRACE} minutes after the start time.`,
      });
    }
    setStatus(found, 'NoShow');
    persist();
    return noContent();
  }),

  // GET /api/staff/queue
  http.get('*/api/staff/queue', ({ request }) => {
    const branchId = branchOf(request);
    const requested = new URL(request.url).searchParams.get('performerId');
    const performerId = requested === 'me' ? ids.vetRahmanId : requested;
    return json(buildQueue(branchId, performerId));
  }),

  // POST /api/staff/walk-ins — C8
  http.post('*/api/staff/walk-ins', async ({ request }) => {
    const branchId = branchOf(request);
    const body = (await request.json()) as {
      customerId: string;
      catId: string;
      serviceId: string;
      triagePriority: string;
      presentingComplaint?: string | null;
    };
    const id = crypto.randomUUID();
    getDb().appointments.push({
      id,
      version: 1,
      branchId,
      customerId: body.customerId,
      customerName: await lookupName(`/api/staff/customers/${body.customerId}`, 'Walk-in customer'),
      catId: body.catId,
      catName: await lookupName(`/api/cats/${body.catId}`, 'Cat'),
      catIsHighRisk: false,
      serviceId: body.serviceId,
      service: snapshotFor(body.serviceId),
      performerId: null,
      roomId: null,
      startsOn: null,
      endsOn: null,
      isWalkIn: true,
      channel: 'WalkIn',
      status: 'CheckedIn',
      holdExpiresOn: null,
      isWaitlistOffer: false,
      waitlistEntryId: null,
      reasonForVisit: null,
      triagePriority: body.triagePriority as never,
      presentingComplaint: body.presentingComplaint?.trim() || null,
      needsRescheduling: false,
      rescheduleFlagReason: null,
      checkedInOn: new Date(serverNow()).toISOString(),
      noShowEligible: false,
      history: [],
    });
    persist();
    return json({ appointmentId: id }, 201);
  }),

  // GET /api/staff/customers/{id}/appointment-summary
  http.get('*/api/staff/customers/:customerId/appointment-summary', ({ params }) => {
    const customerId = params.customerId as string;
    const own = getDb().appointments.filter((a) => a.customerId === customerId);
    const completed = own
      .filter((a) => a.status === 'Completed' && a.startsOn)
      .sort((a, b) => new Date(b.startsOn!).getTime() - new Date(a.startsOn!).getTime());
    return json({
      noShowCount: own.filter((a) => a.status === 'NoShow').length,
      upcoming: own
        .filter(
          (a) =>
            a.status === 'Booked' && a.startsOn && new Date(a.startsOn).getTime() > serverNow(),
        )
        .sort((a, b) => new Date(a.startsOn!).getTime() - new Date(b.startsOn!).getTime())
        .map(toSummary),
      lastVisitOn: completed[0]?.startsOn ?? null,
    });
  }),
];

function daysBetween(from: string, to: string): number {
  let count = 0;
  let cursor = from;
  while (cursor < to && count < 14) {
    cursor = addDaysIso(cursor, 1);
    count += 1;
  }
  return count;
}

function toAvailability(staffAccountId: string) {
  return (piece: { start: number; end: number; outside: boolean }) => ({
    staffAccountId,
    start: new Date(piece.start).toISOString(),
    end: new Date(piece.end).toISOString(),
    outsideOpeningHours: piece.outside,
  });
}
