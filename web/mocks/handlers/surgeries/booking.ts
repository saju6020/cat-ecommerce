import { http, type HttpHandler } from 'msw';
import type {
  BookSurgeryRequest,
  RescheduleSurgeryRequest,
  SurgerySummary,
} from '@/lib/api/surgeries';
import {
  bad,
  branchOf,
  conflict,
  money,
  noContent,
  notFound,
  ok,
  requireAction,
  surgeryFixtureIds as ids,
  violation,
  zoneOfBranch,
  type MockViolation,
} from './fixtures';
import {
  CHECKLIST_ORDER,
  SERVICES,
  STAFF,
  DEFAULT_FASTING,
  findCat,
  findService,
  findSurgery,
  getDb,
  instantAt,
  localDateOf,
  localTimeOf,
  newId,
  persist,
  plusMinutes,
  staffName,
  theatreName,
  todayIn,
  type MBusy,
  type MSurgery,
} from './store';
import { allowedTransitions, dayItemOf, detailOf } from './views';

/**
 * `mocks/handlers/surgeries/booking.ts` — the scheduling half of `StaffSurgeriesController`: services,
 * staff, availability, book, day list, list, detail, reschedule, cancel. The mock enforces what the real
 * handlers do: ONE violation PER CLASH in a single `400` (theatre, surgeon, assistant), a `409` for a lost
 * race, the sex and already-neutered answers, and the campaign's eligibility note (HO-SRG-001 §8.5).
 */
const OPENS = '08:00';
const CLOSES = '18:00';
const GRID_MINUTES = 30;

function overlaps(aStart: string, aEnd: string, bStart: string, bEnd: string): boolean {
  return new Date(aStart) < new Date(bEnd) && new Date(aEnd) > new Date(bStart);
}

function mine(s: MSurgery, branchId: string): boolean {
  return s.branchId === branchId;
}

/** Everything that occupies a theatre or a person on a day, with the reason (`label`). */
function busyFor(branchId: string, date: string, excludeId: string | null) {
  const zone = zoneOfBranch(branchId);
  const theatre: Array<MBusy & { surgeryId?: string }> = [];
  const people: Array<MBusy & { surgeryId?: string }> = [];
  for (const s of getDb().surgeries) {
    if (!mine(s, branchId) || s.status === 'Cancelled' || s.id === excludeId) continue;
    if (localDateOf(s.startsOn, zone) !== date) continue;
    const cat = findCat(s.catId)?.name ?? 'a cat';
    const procedure = findService(s.serviceId)?.name.toLowerCase() ?? 'surgery';
    theatre.push({
      branchId,
      resource: 'theatre',
      resourceId: s.theatreId,
      startsOn: s.startsOn,
      endsOn: s.endsOn,
      label: `${theatreName(s.theatreId)}: ${cat}’s ${procedure}`,
    });
    people.push({
      branchId,
      resource: 'surgeon',
      resourceId: s.surgeonId,
      startsOn: s.startsOn,
      endsOn: s.endsOn,
      label: `${staffName(s.surgeonId)}: in surgery (${cat}’s ${procedure})`,
    });
    people.push({
      branchId,
      resource: 'assistant',
      resourceId: s.assistantId,
      startsOn: s.startsOn,
      endsOn: s.endsOn,
      label: `${staffName(s.assistantId)}: assisting at ${cat}’s ${procedure}`,
    });
  }
  for (const extra of getDb().extraBusy) {
    if (extra.branchId !== branchId || localDateOf(extra.startsOn, zone) !== date) continue;
    if (extra.resource === 'theatre') theatre.push(extra);
    else people.push(extra);
  }
  return { theatre, people };
}

function toInterval(b: MBusy) {
  return { startsOn: b.startsOn, endsOn: b.endsOn, label: b.label };
}

function clashesOf(
  branchId: string,
  startsOn: string,
  endsOn: string,
  pick: { theatreId: string; surgeonId: string; assistantId: string },
  excludeId: string | null,
): MockViolation[] {
  const zone = zoneOfBranch(branchId);
  const date = localDateOf(startsOn, zone);
  const { theatre, people } = busyFor(branchId, date, excludeId);
  const out: MockViolation[] = [];
  if (pick.assistantId === pick.surgeonId) {
    out.push(
      bad(
        'assistantId',
        'AssistantSameAsSurgeon',
        'The assistant must be a different person from the surgeon.',
      ),
    );
  }
  if (new Date(startsOn).getTime() <= Date.now()) {
    out.push(bad('startsOn', 'SlotInPast', 'This time has already passed.'));
  }
  const range = (b: MBusy) =>
    `from ${localTimeOf(b.startsOn, zone)} to ${localTimeOf(b.endsOn, zone)}`;
  const t = theatre.find(
    (b) => b.resourceId === pick.theatreId && overlaps(startsOn, endsOn, b.startsOn, b.endsOn),
  );
  if (t)
    out.push(
      bad(
        'theatreId',
        'TheatreBusy',
        `${theatreName(pick.theatreId)} is already booked ${range(t)}.`,
      ),
    );
  const sg = people.find(
    (b) => b.resourceId === pick.surgeonId && overlaps(startsOn, endsOn, b.startsOn, b.endsOn),
  );
  if (sg)
    out.push(
      bad(
        'surgeonId',
        'SurgeonBusy',
        `${staffName(pick.surgeonId)} is already booked ${range(sg)}.`,
      ),
    );
  const as = people.find(
    (b) => b.resourceId === pick.assistantId && overlaps(startsOn, endsOn, b.startsOn, b.endsOn),
  );
  if (as)
    out.push(
      bad(
        'assistantId',
        'AssistantBusy',
        `${staffName(pick.assistantId)} is already booked ${range(as)}.`,
      ),
    );
  return out;
}

function campaignRemaining(campaignId: string): number | null {
  const campaign = getDb().campaigns.find((c) => c.id === campaignId);
  if (!campaign || campaign.maxCases === null) return null;
  const booked = getDb().surgeries.filter(
    (s) => s.campaignId === campaignId && s.status !== 'Cancelled',
  ).length;
  return Math.max(0, campaign.maxCases - booked);
}

export function applicableCampaigns(branchId: string, serviceId: string, date: string) {
  return getDb()
    .campaigns.filter(
      (c) =>
        c.isActive &&
        (c.branchIds.length === 0 || c.branchIds.includes(branchId)) &&
        c.validFrom <= date &&
        c.validTo >= date &&
        c.prices.some((p) => p.serviceId === serviceId),
    )
    .map((c) => ({
      id: c.id,
      code: c.code,
      name: c.name,
      validFrom: c.validFrom,
      validTo: c.validTo,
      price: money(c.prices.find((p) => p.serviceId === serviceId)!.amount),
      casesRemaining: campaignRemaining(c.id),
    }));
}

function pageOf<T>(rows: T[], url: URL) {
  const pageNumber = Math.max(1, Number(url.searchParams.get('pageNumber') ?? '1'));
  const pageSize = Math.min(100, Math.max(1, Number(url.searchParams.get('pageSize') ?? '20')));
  return {
    items: rows.slice((pageNumber - 1) * pageSize, pageNumber * pageSize),
    pageNumber,
    pageSize,
    totalCount: rows.length,
  };
}

export const bookingHandlers: HttpHandler[] = [
  http.get('*/api/staff/surgeries/services', ({ request }) => {
    const refused = requireAction(request, 'Surgery.View');
    if (refused) return refused;
    return ok(
      SERVICES.map((s) => ({
        id: s.id,
        name: s.name,
        category: s.category,
        durationMinutes: s.durationMinutes,
        price: money(s.price),
        isSterilisation: s.isSterilisation,
        qualifiedRoleIds: ['Vet'],
      })),
    );
  }),

  http.get('*/api/staff/surgeries/staff', ({ request }) => {
    const refused = requireAction(request, 'Surgery.View');
    if (refused) return refused;
    return ok({
      surgeons: STAFF.filter((s) => s.roleIds.includes('Vet')).map((s) => ({
        id: s.id,
        name: s.name,
      })),
      assistants: STAFF.map((s) => ({ id: s.id, name: s.name, roleIds: s.roleIds })),
    });
  }),

  http.get('*/api/staff/surgeries/availability', ({ request }) => {
    const refused = requireAction(request, 'Surgery.View');
    if (refused) return refused;
    const url = new URL(request.url);
    const branchId = branchOf(request);
    const zone = zoneOfBranch(branchId);
    const service = findService(url.searchParams.get('serviceId') ?? '');
    const date = url.searchParams.get('date') ?? todayIn(zone);
    if (!service) {
      return violation(
        bad('serviceId', 'ServiceNotSurgical', 'This service is not a theatre procedure.'),
      );
    }
    const theatreId = url.searchParams.get('theatreId');
    const surgeonId = url.searchParams.get('surgeonId');
    const assistantId = url.searchParams.get('assistantId');
    const exclude = url.searchParams.get('excludeSurgeryId');
    const { theatre, people } = busyFor(branchId, date, exclude);
    const busyTheatre = theatre.filter((b) => b.resourceId === theatreId);
    const busySurgeon = people.filter((b) => b.resourceId === surgeonId);
    const busyAssistant = people.filter((b) => b.resourceId === assistantId);
    const all = [...busyTheatre, ...busySurgeon, ...busyAssistant];

    const starts: Array<{ startsOn: string; endsOn: string; outsideOpeningHours: boolean }> = [];
    const [openH = 8] = OPENS.split(':').map(Number);
    const [closeH = 18] = CLOSES.split(':').map(Number);
    for (let minutes = openH * 60; minutes <= closeH * 60 - GRID_MINUTES; minutes += GRID_MINUTES) {
      const time = `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`;
      const startsOn = instantAt(date, time, zone);
      const endsOn = plusMinutes(startsOn, service.durationMinutes);
      if (new Date(startsOn).getTime() <= Date.now()) continue;
      if (all.some((b) => overlaps(startsOn, endsOn, b.startsOn, b.endsOn))) continue;
      // With no resource chosen yet, a day closed for maintenance has no starts at all.
      if (
        !theatreId &&
        theatre.some(
          (b) =>
            b.label.includes('closed for maintenance') &&
            overlaps(startsOn, endsOn, b.startsOn, b.endsOn),
        )
      )
        continue;
      starts.push({
        startsOn,
        endsOn,
        outsideOpeningHours: minutes + service.durationMinutes > closeH * 60,
      });
    }
    return ok({
      date,
      branchTimeZoneId: zone,
      gridMinutes: GRID_MINUTES,
      starts,
      busy: {
        theatre: busyTheatre.map(toInterval),
        surgeon: busySurgeon.map(toInterval),
        assistant: busyAssistant.map(toInterval),
      },
    });
  }),

  http.post('*/api/staff/surgeries', async ({ request }) => {
    const refused = requireAction(request, 'Surgery.Schedule');
    if (refused) return refused;
    const body = (await request.json()) as BookSurgeryRequest;
    const branchId = branchOf(request);
    const zone = zoneOfBranch(branchId);
    const service = findService(body.serviceId);
    if (!service) {
      return violation(
        bad('ServiceId', 'ServiceNotSurgical', 'This service is not a theatre procedure.'),
      );
    }
    const cat = findCat(body.catId);
    if (!cat) {
      return violation(bad('CatId', 'CatNotBookable', 'This cat cannot be booked.'));
    }
    if (service.requiresSex && cat.sex !== service.requiresSex) {
      return violation(
        bad(
          'ServiceId',
          'ProcedureSexMismatch',
          `${service.name} is a procedure for ${service.requiresSex === 'Female' ? 'female' : 'male'} cats, and ${cat.name} is ${cat.sex.toLowerCase()}.`,
        ),
      );
    }
    if (
      service.isSterilisation &&
      cat.neuterStatus === 'Neutered' &&
      !body.confirmNeuterStatusDiscrepancy
    ) {
      return violation(
        bad(
          'ConfirmNeuterStatusDiscrepancy',
          'CatAlreadyNeutered',
          `${cat.name} is recorded as already neutered. Check the record, or book anyway if it is wrong.`,
        ),
      );
    }
    const date = localDateOf(body.startsOn, zone);
    let priceAmount = service.price;
    let basis: 'Standard' | 'Campaign' = 'Standard';
    let eligibilityNote: string | null = null;
    if (body.campaignId) {
      const applicable = applicableCampaigns(branchId, body.serviceId, date).find(
        (c) => c.id === body.campaignId,
      );
      if (!applicable || applicable.casesRemaining === 0) {
        return violation(
          bad(
            'CampaignId',
            'CampaignNotApplicable',
            applicable
              ? 'That campaign has no places left.'
              : 'That campaign does not apply to this procedure and date.',
          ),
        );
      }
      if (!body.campaignEligibilityNote?.trim()) {
        return violation(
          bad(
            'CampaignEligibilityNote',
            'CampaignNotApplicable',
            'Note why this cat is eligible for the campaign.',
          ),
        );
      }
      priceAmount = Number(applicable.price.amount);
      basis = 'Campaign';
      eligibilityNote = body.campaignEligibilityNote.trim();
    }

    const endsOn = plusMinutes(body.startsOn, service.durationMinutes);
    const db = getDb();
    if (localTimeOf(body.startsOn, zone) === ids.raceStartTime && !db.raceTriggered) {
      db.raceTriggered = true;
      db.extraBusy.push({
        branchId,
        resource: 'theatre',
        resourceId: body.theatreId,
        startsOn: body.startsOn,
        endsOn,
        label: `${theatreName(body.theatreId)}: just booked by someone else`,
      });
      persist();
      return conflict(
        bad(
          'TheatreId',
          'TheatreBusy',
          'The theatre has just been booked. Check availability again.',
        ),
      );
    }
    const clashes = clashesOf(branchId, body.startsOn, endsOn, body, null);
    if (clashes.length > 0) return violation(...clashes);

    const id = newId();
    const created: MSurgery = {
      id,
      version: 1,
      branchId,
      status: 'Scheduled',
      catId: body.catId,
      customerId: body.customerId,
      appointmentId: newId(),
      visitId: newId(),
      serviceId: body.serviceId,
      theatreId: body.theatreId,
      surgeonId: body.surgeonId,
      assistantId: body.assistantId,
      startsOn: body.startsOn,
      endsOn,
      admitBy: plusMinutes(body.startsOn, -45),
      fastingInstructions: DEFAULT_FASTING,
      priceAmount,
      priceBasis: basis,
      campaignId: body.campaignId ?? null,
      eligibilityNote,
      asaGrade: null,
      asaIsEmergency: false,
      consentOverride: null,
      consentForced: false,
      checklist: CHECKLIST_ORDER.map((kind) => ({
        id: newId(),
        kind,
        status: 'Outstanding',
        completedBy: null,
        completedOn: null,
        note: null,
        waiverReason: null,
        referenceId: null,
      })),
      admittedOn: null,
      procedureStartedOn: null,
      procedureEndedOn: null,
      dischargedOn: null,
      outcome: null,
      recoveryStatus: null,
      recoveryHistory: [],
      statusHistory: [
        {
          from: null,
          to: 'Scheduled',
          changedOn: new Date().toISOString(),
          changedBy: ids.nurseTanvirId,
        },
      ],
      postOpInstructions: null,
      followUps: [],
      cancelledOn: null,
      cancellationReason: null,
      neuterApplied: null,
    };
    db.surgeries.push(created);
    persist();
    return ok({ surgeryId: id }, 201);
  }),

  http.get('*/api/staff/surgeries/day', ({ request }) => {
    const refused = requireAction(request, 'Surgery.View');
    if (refused) return refused;
    const branchId = branchOf(request);
    const zone = zoneOfBranch(branchId);
    const date = new URL(request.url).searchParams.get('date') ?? todayIn(zone);
    const rows = getDb()
      .surgeries.filter(
        (s) =>
          mine(s, branchId) && s.status !== 'Cancelled' && localDateOf(s.startsOn, zone) === date,
      )
      .sort(
        (a, b) =>
          theatreName(a.theatreId).localeCompare(theatreName(b.theatreId)) ||
          a.startsOn.localeCompare(b.startsOn),
      );
    return ok({ date, branchTimeZoneId: zone, surgeries: rows.map(dayItemOf) });
  }),

  http.get('*/api/staff/surgeries', ({ request }) => {
    const refused = requireAction(request, 'Surgery.View');
    if (refused) return refused;
    const url = new URL(request.url);
    const branchId = branchOf(request);
    const zone = zoneOfBranch(branchId);
    const status = url.searchParams.get('status');
    const rows: SurgerySummary[] = getDb()
      .surgeries.filter((s) => mine(s, branchId) && (!status || s.status === status))
      .sort((a, b) => b.startsOn.localeCompare(a.startsOn))
      .map((s) => ({
        id: s.id,
        startsOn: s.startsOn,
        endsOn: s.endsOn,
        status: s.status,
        catId: s.catId,
        catName: findCat(s.catId)?.name ?? 'Cat',
        procedure: findService(s.serviceId)!.name,
        pricingBasis: s.priceBasis,
        outcome: s.outcome,
        surgeonId: s.surgeonId,
        surgeonName: staffName(s.surgeonId),
        reportOutstanding: s.status === 'Recovery' || s.status === 'Discharged',
        branchTimeZoneId: zone,
      }));
    return ok(pageOf(rows, url));
  }),

  http.get('*/api/staff/surgeries/:id', ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.View');
    if (refused) return refused;
    const s = findSurgery(params.id as string);
    if (!s || !mine(s, branchOf(request))) return notFound();
    return ok(detailOf(s));
  }),

  http.post('*/api/staff/surgeries/:id/reschedule', async ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.Schedule');
    if (refused) return refused;
    const s = findSurgery(params.id as string);
    if (!s || !mine(s, branchOf(request))) return notFound();
    const body = (await request.json()) as RescheduleSurgeryRequest;
    if (body.expectedVersion !== s.version) {
      return conflict(
        bad(
          'ExpectedVersion',
          'StaleVersion',
          'This surgery has changed since you loaded it. Reload and try again.',
        ),
      );
    }
    if (!allowedTransitions(s.status).includes('Reschedule')) {
      return violation(
        bad(
          'Status',
          'InvalidSurgeryTransition',
          'A surgery that is not scheduled cannot be rescheduled.',
        ),
      );
    }
    const endsOn = plusMinutes(body.startsOn, findService(s.serviceId)!.durationMinutes);
    const clashes = clashesOf(s.branchId, body.startsOn, endsOn, body, s.id);
    if (clashes.length > 0) return violation(...clashes);
    s.startsOn = body.startsOn;
    s.endsOn = endsOn;
    s.admitBy = plusMinutes(body.startsOn, -45);
    s.theatreId = body.theatreId;
    s.surgeonId = body.surgeonId;
    s.assistantId = body.assistantId;
    s.version += 1;
    persist();
    return ok({ surgery: detailOf(s) });
  }),

  http.post('*/api/staff/surgeries/:id/cancel', async ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.Schedule');
    if (refused) return refused;
    const s = findSurgery(params.id as string);
    if (!s || !mine(s, branchOf(request))) return notFound();
    const body = (await request.json()) as { reason?: string };
    if (!body.reason?.trim()) {
      return violation(
        bad('Reason', 'CancellationReasonRequired', 'A reason is required to cancel a surgery.'),
      );
    }
    if (!allowedTransitions(s.status).includes('Cancel')) {
      return violation(
        bad(
          'Status',
          'InvalidSurgeryTransition',
          'A surgery in theatre or later cannot be cancelled.',
        ),
      );
    }
    s.status = 'Cancelled';
    s.cancelledOn = new Date().toISOString();
    s.cancellationReason = body.reason.trim();
    s.version += 1;
    persist();
    return noContent();
  }),
];
