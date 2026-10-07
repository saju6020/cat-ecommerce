import { http, type HttpHandler } from 'msw';
import { localDateOf } from '@/lib/format/time';
import {
  appointmentsFixtureIds as ids,
  apiUrl,
  json,
  MOCK_CLAIM_WINDOW_MINUTES,
  MOCK_HOLD_SECONDS,
  noContent,
  notFound,
  serverNow,
  violation,
} from './fixtures';
import {
  computeSlots,
  findAppointment,
  getDb,
  isFullDate,
  persist,
  PERFORMERS,
  serviceRules,
  setStatus,
  shouldLoseRace,
  snapshotFor,
  sweepStaleHolds,
  takeSlotByOtherCustomer,
  toCustomerDetail,
  toSummary,
  zoneOf,
  cutoffFor,
  type MockAppointment,
} from './store';

/**
 * `mocks/handlers/appointments/customer.ts` — HO-APT-002 §12, public and customer endpoints
 * (HO-APT-001 §12.4). A customer sees only their own rows; anyone else's is a `404`.
 */

const SLOT_TAKEN = {
  propertyName: 'StartsOn',
  errorCode: 'SlotNoLongerAvailable',
  errorMessage: 'That time is no longer available.',
};

async function lookupCatName(catId: string): Promise<string> {
  try {
    const response = await fetch(apiUrl(`/api/cats/${encodeURIComponent(catId)}`), {
      credentials: 'include',
    });
    if (response.ok) return ((await response.json()) as { name: string }).name;
  } catch {
    // fall through
  }
  return 'Your cat';
}

function mine(id: string): MockAppointment | undefined {
  const found = findAppointment(id);
  return found && found.customerId === ids.signedInCustomerId ? found : undefined;
}

function pageOf<T>(rows: T[], url: URL) {
  const pageNumber = Number(url.searchParams.get('pageNumber') ?? 1) || 1;
  const pageSize = Number(url.searchParams.get('pageSize') ?? 20) || 20;
  return {
    items: rows.slice((pageNumber - 1) * pageSize, pageNumber * pageSize),
    pageNumber,
    pageSize,
    totalCount: rows.length,
  };
}

export const appointmentsCustomerHandlers: HttpHandler[] = [
  // GET /api/booking/slots — public
  http.get('*/api/booking/slots', ({ request }) => {
    const url = new URL(request.url);
    const branchId = url.searchParams.get('branchId') ?? ids.mainBranchId;
    const serviceId = url.searchParams.get('serviceId') ?? ids.consultationServiceId;
    const date = url.searchParams.get('date') ?? '';
    const slots = computeSlots({
      branchId,
      serviceId,
      date,
      performerId: url.searchParams.get('performerId'),
      catId: url.searchParams.get('catId'),
      mode: 'online',
    });
    const grouped = new Map<
      number,
      { endMs: number; performers: Array<{ id: string; name: string }> }
    >();
    for (const slot of slots) {
      const entry = grouped.get(slot.startMs) ?? { endMs: slot.endMs, performers: [] };
      entry.performers.push({ id: slot.performer.id, name: slot.performer.name });
      grouped.set(slot.startMs, entry);
    }
    return json({
      date,
      branchTimeZoneId: zoneOf(branchId),
      slots: [...grouped.entries()].map(([startMs, entry]) => ({
        startsOn: new Date(startMs).toISOString(),
        endsOn: new Date(entry.endMs).toISOString(),
        performers: entry.performers,
      })),
    });
  }),

  // GET /api/booking/performers — public
  http.get('*/api/booking/performers', ({ request }) => {
    const url = new URL(request.url);
    const branchId = url.searchParams.get('branchId') ?? ids.mainBranchId;
    const rules = serviceRules(url.searchParams.get('serviceId') ?? ids.consultationServiceId);
    return json(
      PERFORMERS.filter(
        (p) => p.branchId === branchId && p.roleIds.some((role) => rules.roleIds.includes(role)),
      ).map((p) => ({ id: p.id, name: p.name })),
    );
  }),

  // POST /api/customers/me/appointment-holds — C1
  http.post('*/api/customers/me/appointment-holds', async ({ request }) => {
    const body = (await request.json()) as {
      branchId: string;
      serviceId: string;
      catId: string;
      startsOn: string;
      performerId?: string | null;
    };
    if (shouldLoseRace(body.branchId, body.startsOn)) {
      takeSlotByOtherCustomer(body.branchId, body.startsOn, body.serviceId);
      return violation(409, SLOT_TAKEN);
    }
    const date = localDateOf(body.startsOn, zoneOf(body.branchId));
    const startMs = new Date(body.startsOn).getTime();
    const slot = computeSlots({
      branchId: body.branchId,
      serviceId: body.serviceId,
      date,
      performerId: body.performerId,
      catId: body.catId,
      mode: 'online',
    }).find((s) => s.startMs === startMs);
    if (!slot) return violation(409, SLOT_TAKEN);

    // One hold per customer (ADR-070): release the caller's previous plain hold.
    const database = getDb();
    for (const previous of database.appointments) {
      if (
        previous.customerId === ids.signedInCustomerId &&
        previous.status === 'Held' &&
        !previous.isWaitlistOffer
      ) {
        setStatus(previous, 'HoldReleased');
      }
    }

    const id = crypto.randomUUID();
    const holdExpiresOn = new Date(serverNow() + MOCK_HOLD_SECONDS * 1000).toISOString();
    database.appointments.push({
      id,
      version: 1,
      branchId: body.branchId,
      customerId: ids.signedInCustomerId,
      customerName: ids.signedInCustomerName,
      catId: body.catId,
      catName: await lookupCatName(body.catId),
      catIsHighRisk: false,
      serviceId: body.serviceId,
      service: snapshotFor(body.serviceId),
      performerId: slot.performer.id,
      roomId: slot.room.id,
      startsOn: new Date(slot.startMs).toISOString(),
      endsOn: new Date(slot.endMs).toISOString(),
      isWalkIn: false,
      channel: 'Online',
      status: 'Held',
      holdExpiresOn,
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
    });
    persist();
    return json(
      {
        appointmentId: id,
        startsOn: new Date(slot.startMs).toISOString(),
        endsOn: new Date(slot.endMs).toISOString(),
        performerId: slot.performer.id,
        roomId: slot.room.id,
        holdExpiresOn,
      },
      201,
    );
  }),

  // DELETE /api/customers/me/appointment-holds/{id} — C2
  http.delete('*/api/customers/me/appointment-holds/:appointmentId', ({ params }) => {
    const held = mine(params.appointmentId as string);
    if (held && held.status === 'Held') {
      setStatus(held, 'HoldReleased');
      persist();
    }
    return noContent();
  }),

  // POST /api/customers/me/appointments/{id}/confirm — C3
  http.post(
    '*/api/customers/me/appointments/:appointmentId/confirm',
    async ({ params, request }) => {
      const held = mine(params.appointmentId as string);
      if (!held || !['Held', 'HoldExpired'].includes(held.status)) return notFound();
      const body = (await request.json().catch(() => ({}))) as { reasonForVisit?: string | null };
      sweepStaleHolds();
      if (held.status === 'HoldExpired') {
        persist();
        return violation(400, {
          propertyName: 'AppointmentId',
          errorCode: 'SlotHoldExpired',
          errorMessage: 'This hold has expired. Please choose a time again.',
        });
      }
      held.reasonForVisit = body.reasonForVisit?.trim() || null;
      held.holdExpiresOn = null;
      setStatus(held, 'Booked');
      const entry = getDb().waitlist.find((w) => w.id === held.waitlistEntryId);
      if (entry) entry.status = 'Claimed';
      persist();
      return json(toCustomerDetail(held));
    },
  ),

  // GET /api/customers/me/appointments?scope=
  http.get('*/api/customers/me/appointments', ({ request }) => {
    const url = new URL(request.url);
    const scope = url.searchParams.get('scope') ?? 'upcoming';
    sweepStaleHolds();
    const now = serverNow();
    const rows = getDb().appointments.filter((a) => {
      if (a.customerId !== ids.signedInCustomerId || a.status === 'Held') return false;
      if (['HoldExpired', 'HoldReleased'].includes(a.status)) return false;
      const upcoming =
        ['Booked', 'CheckedIn', 'InConsultation'].includes(a.status) &&
        a.endsOn !== null &&
        new Date(a.endsOn).getTime() > now;
      return scope === 'past' ? !upcoming : upcoming;
    });
    rows.sort((a, b) => {
      const diff = new Date(a.startsOn ?? 0).getTime() - new Date(b.startsOn ?? 0).getTime();
      return scope === 'past' ? -diff : diff;
    });
    return json(pageOf(rows.map(toSummary), url));
  }),

  // GET /api/customers/me/appointments/{id}
  http.get('*/api/customers/me/appointments/:appointmentId', ({ params }) => {
    sweepStaleHolds();
    const found = mine(params.appointmentId as string);
    return found ? json(toCustomerDetail(found)) : notFound();
  }),

  // POST /api/customers/me/appointments/{id}/reschedule — C4
  http.post(
    '*/api/customers/me/appointments/:appointmentId/reschedule',
    async ({ params, request }) => {
      const found = mine(params.appointmentId as string);
      if (!found || found.status !== 'Booked') return notFound();
      const body = (await request.json()) as { startsOn: string; performerId?: string | null };
      if (!cutoffFor(found).canChange) {
        return violation(400, {
          propertyName: 'AppointmentId',
          errorCode: 'ChangeCutoffPassed',
          errorMessage: 'It is too close to the appointment to change it online.',
        });
      }
      if (shouldLoseRace(found.branchId, body.startsOn)) {
        takeSlotByOtherCustomer(found.branchId, body.startsOn, found.serviceId);
        return violation(409, SLOT_TAKEN);
      }
      const date = localDateOf(body.startsOn, zoneOf(found.branchId));
      const startMs = new Date(body.startsOn).getTime();
      const slot = computeSlots({
        branchId: found.branchId,
        serviceId: found.serviceId,
        date,
        performerId: body.performerId,
        catId: found.catId,
        mode: 'online',
        excludeAppointmentId: found.id,
      }).find((s) => s.startMs === startMs);
      if (!slot) return violation(409, SLOT_TAKEN);
      found.startsOn = new Date(slot.startMs).toISOString();
      found.endsOn = new Date(slot.endMs).toISOString();
      found.performerId = slot.performer.id;
      found.roomId = slot.room.id;
      found.needsRescheduling = false;
      found.rescheduleFlagReason = null;
      found.version += 1;
      persist();
      return json(toCustomerDetail(found));
    },
  ),

  // POST /api/customers/me/appointments/{id}/cancel — C5
  http.post('*/api/customers/me/appointments/:appointmentId/cancel', ({ params }) => {
    const found = mine(params.appointmentId as string);
    if (!found || found.status !== 'Booked') return notFound();
    if (!cutoffFor(found).canChange) {
      return violation(400, {
        propertyName: 'AppointmentId',
        errorCode: 'ChangeCutoffPassed',
        errorMessage: 'It is too close to the appointment to cancel it online.',
      });
    }
    setStatus(found, 'Cancelled');
    persist();
    return noContent();
  }),

  // GET /api/customers/me/waitlist
  http.get('*/api/customers/me/waitlist', () => {
    sweepStaleHolds();
    const now = serverNow();
    const rows = getDb().waitlist.map((w) =>
      w.status === 'Offered' && w.offerExpiresOn && new Date(w.offerExpiresOn).getTime() < now
        ? { ...w, status: 'Lapsed' }
        : w,
    );
    return json(rows);
  }),

  // POST /api/customers/me/waitlist — C14
  http.post('*/api/customers/me/waitlist', async ({ request }) => {
    const body = (await request.json()) as {
      branchId: string;
      serviceId: string;
      catId: string;
      localDate: string;
      preferredPerformerId?: string | null;
    };
    const database = getDb();
    if (
      database.waitlist.some(
        (w) =>
          w.catId === body.catId &&
          w.serviceId === body.serviceId &&
          w.localDate === body.localDate &&
          ['Waiting', 'Offered'].includes(w.status),
      )
    ) {
      return violation(400, {
        propertyName: 'CatId',
        errorCode: 'AlreadyWaitlisted',
        errorMessage: 'This cat is already on the waitlist for that day.',
      });
    }
    const stillAvailable =
      computeSlots({
        branchId: body.branchId,
        serviceId: body.serviceId,
        date: body.localDate,
        mode: 'online',
      }).length > 0 && !isFullDate(body.branchId, body.localDate);
    if (stillAvailable) {
      return violation(400, {
        propertyName: 'LocalDate',
        errorCode: 'SlotsStillAvailable',
        errorMessage: 'There are still times available on that day — please book one instead.',
      });
    }
    const id = crypto.randomUUID();
    database.waitlist.push({
      id,
      branchId: body.branchId,
      catId: body.catId,
      catName: await lookupCatName(body.catId),
      serviceId: body.serviceId,
      serviceName: serviceRules(body.serviceId).name,
      localDate: body.localDate,
      status: 'Waiting',
      offeredAppointmentId: null,
      offerExpiresOn: null,
      createdOn: new Date(serverNow()).toISOString(),
    });
    persist();
    return json({ waitlistEntryId: id, claimWindowMinutes: MOCK_CLAIM_WINDOW_MINUTES }, 201);
  }),

  // DELETE /api/customers/me/waitlist/{id}
  http.delete('*/api/customers/me/waitlist/:waitlistEntryId', ({ params }) => {
    const entry = getDb().waitlist.find((w) => w.id === params.waitlistEntryId);
    if (entry && ['Waiting', 'Offered'].includes(entry.status)) {
      entry.status = 'Withdrawn';
      persist();
    }
    return noContent();
  }),
];
