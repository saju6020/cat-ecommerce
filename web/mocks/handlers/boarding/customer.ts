import { http, HttpResponse, type HttpHandler } from 'msw';
import type { EnquiryRequest, DiaryDay, StayDiary, StaySummary } from '@/lib/api/types';
import { addDaysIso } from '@/lib/format/appointment';
import { evaluateGate } from './actions';
import {
  availabilityInputs,
  availabilityResponse,
  bad,
  bookingLinkFor,
  cancelAction,
  createEnquiryAction,
  gateView,
  lookupCat,
  reserveAction,
  setCareProfileAction,
  validateDates,
} from './actions';
import { boardingFixtureIds as ids, BRANCH_INFO, json, noContent, notFound } from './fixtures';
import { money } from './rules';
import { profileFromWire } from './wire';
import {
  acceptedOf,
  branchIdOf,
  currentCard,
  depositOutcomeText,
  diaryObservation,
  findStay,
  getDb,
  isMine,
  nowIso,
  persist,
  sweepLapses,
  todayOf,
  toApprovalDetail,
  toCustomerDetail,
  wireCustomerDetail,
  toCustomerSummary,
  toDaycareVisit,
  toIncidentDetail,
  zoneOfBranch,
  nextId,
} from './store';

/**
 * `mocks/handlers/boarding/customer.ts` — HO-BRD-002 §12: the public and customer endpoints of
 * HO-BRD-001 §12.4. A customer sees only their own rows; anyone else's is a `404`. Nothing here
 * takes a payment (ADR-FE-106).
 */

const BASE = '*/api/customers/me/boarding';
const customerWho = {
  customerId: ids.signedInCustomerId,
  customerName: ids.signedInCustomerName,
  channel: 'Online' as const,
};

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

export const boardingCustomerHandlers: HttpHandler[] = [
  // GET /api/boarding/availability — public
  http.get('*/api/boarding/availability', ({ request }) => {
    sweepLapses();
    const url = new URL(request.url);
    const branchId = url.searchParams.get('branchId') || ids.mainBranchId;
    const arrivalOn = url.searchParams.get('arrivalOn') ?? '';
    const departureOn = url.searchParams.get('departureOn') ?? '';
    const catCount = Number(url.searchParams.get('catCount') ?? 1) || 1;
    const problem = validateDates(branchId, arrivalOn, departureOn, 'Online');
    if (problem) return problem.response;
    return json(
      availabilityResponse(
        availabilityInputs(branchId, arrivalOn, departureOn, catCount, 'Online'),
      ),
    );
  }),

  // GET /api/boarding/unit-types — public
  http.get('*/api/boarding/unit-types', ({ request }) => {
    const branchId = branchIdOf(request);
    const types = (['Standard', 'Deluxe', 'Family'] as const).flatMap((unitType) => {
      const card = currentCard(unitType);
      if (!card) return [];
      return [
        {
          unitType,
          capacityCats: getDb().units.find((u) => u.unitType === unitType)?.capacityCats ?? 1,
          nightlyRate: money(card.nightly),
          weeklyRate: money(card.weekly),
          monthlyRate: money(card.monthly),
        },
      ];
    });
    const offered = (BRANCH_INFO[branchId]?.daycareCapacity ?? 0) > 0;
    const dc = getDb().daycareRateCards[0];
    return json({
      unitTypes: types,
      daycare:
        offered && dc
          ? { offered, hourlyRate: money(dc.hourly), dayRate: money(dc.day) }
          : { offered: false, hourlyRate: null, dayRate: null },
    });
  }),

  // GET /api/content/page/boarding-payment-instructions — HO-ADM-002's page, registered ahead of the admin mock
  http.get('*/api/content/page/boarding-payment-instructions', () =>
    HttpResponse.json({
      kind: 'Page',
      slug: 'boarding-payment-instructions',
      title: 'How to pay your boarding deposit',
      body: '## Pay at the clinic or by bank transfer\n\n- **In store:** pay in cash or by card at the front desk.\n- **Bank transfer:** send the deposit to PurrCare Ltd, account 0123456789, and quote your stay reference.\n\nOnce the clinic has recorded your payment we confirm the stay and message you.',
      publishedOn: '2026-09-01T00:00:00Z',
    }),
  ),

  // POST …/enquiries — C5
  http.post(`${BASE}/enquiries`, async ({ request }) => {
    const body = (await request.json()) as EnquiryRequest;
    const result = await createEnquiryAction(body, customerWho);
    if (!result.ok) return result.response;
    const stay = result.value;
    return json({ stayId: stay.id, vaccinationGate: gateView(stay.cats), quote: stay.quote }, 201);
  }),

  // PUT …/stays/{id}/enquiry — C5 change (re-evaluates the gate: "Resume")
  http.put(`${BASE}/stays/:stayId/enquiry`, async ({ request, params }) => {
    const stay = findStay(params.stayId as string);
    if (!isMine(stay)) return notFound();
    if (stay.status !== 'Enquiry') {
      return bad('InvalidStayTransition', 'Status', 'Only a new enquiry can be changed.').response;
    }
    const body = (await request.json()) as EnquiryRequest;
    const result = await createEnquiryAction(body, customerWho, stay);
    if (!result.ok) return result.response;
    return json({ stayId: stay.id, vaccinationGate: gateView(stay.cats), quote: stay.quote });
  }),

  // PUT …/stays/{id}/cats/{catId}/care-profile — C6
  http.put(`${BASE}/stays/:stayId/cats/:catId/care-profile`, async ({ request, params }) => {
    const stay = findStay(params.stayId as string);
    if (!isMine(stay)) return notFound();
    const result = setCareProfileAction(
      stay,
      params.catId as string,
      profileFromWire((await request.json()) as Record<string, unknown>),
    );
    return result.ok ? noContent() : result.response;
  }),

  // POST …/stays/{id}/reserve — C8
  http.post(`${BASE}/stays/:stayId/reserve`, async ({ params }) => {
    sweepLapses();
    const stay = findStay(params.stayId as string);
    if (!isMine(stay)) return notFound();
    const result = await reserveAction(stay);
    if (!result.ok) return result.response;
    return json({
      stay: wireCustomerDetail(stay),
      deposit: {
        amount: money(stay.depositRequired),
        payBy: stay.payBy,
        instructionsPath: '/pages/boarding-payment-instructions',
      },
    });
  }),

  // POST …/stays/{id}/cancel — C11
  http.post(`${BASE}/stays/:stayId/cancel`, ({ params }) => {
    const stay = findStay(params.stayId as string);
    if (!isMine(stay)) return notFound();
    const outcome = depositOutcomeText(stay) ?? '';
    const forfeited = outcome.includes('kept');
    const result = cancelAction(stay, ids.signedInCustomerName, forfeited);
    return result.ok ? json({ depositForfeited: forfeited }) : result.response;
  }),

  // GET …/stays?scope=
  http.get(`${BASE}/stays`, ({ request }) => {
    sweepLapses();
    const url = new URL(request.url);
    const scope = url.searchParams.get('scope') ?? 'upcoming';
    const mine = Object.values(getDb().stays).filter(
      (s) => s.customerId === ids.signedInCustomerId,
    );
    const pick = mine.filter((s) =>
      scope === 'current'
        ? s.status === 'CheckedIn'
        : scope === 'past'
          ? ['CheckedOut', 'Cancelled', 'NoShow'].includes(s.status)
          : ['Enquiry', 'Reserved', 'Confirmed'].includes(s.status),
    );
    pick.sort((a, b) => a.arrivalOn.localeCompare(b.arrivalOn));
    return json(pageOf(pick.map(toCustomerSummary), url));
  }),

  // GET …/stays/{id}
  http.get(`${BASE}/stays/:stayId`, ({ params }) => {
    sweepLapses();
    const stay = findStay(params.stayId as string);
    return isMine(stay) ? json(wireCustomerDetail(stay)) : notFound();
  }),

  // GET …/stays/{id}/diary
  http.get(`${BASE}/stays/:stayId/diary`, ({ params }) => {
    const stay = findStay(params.stayId as string);
    if (!isMine(stay)) return notFound();
    const d = getDb();
    const zone = zoneOfBranch(stay.branchId);
    const today = todayOf(stay.branchId);
    const last = stay.departureOn < today ? stay.departureOn : today;
    const days: DiaryDay[] = [];
    for (let day = stay.arrivalOn; day <= last; day = addDaysIso(day, 1)) {
      const obs = d.observations
        .filter(
          (o) =>
            o.stayId === stay.id &&
            new Intl.DateTimeFormat('en-CA', { timeZone: zone }).format(new Date(o.recordedOn)) ===
              day,
        )
        .map(diaryObservation);
      const media = d.media
        .filter(
          (m) => m.stayId === stay.id && m.status === 'Visible' && m.capturedOnLocalDate === day,
        )
        .map((m) => ({
          id: m.id,
          catId: m.catId,
          caption: m.caption,
          capturedOnLocalDate: m.capturedOnLocalDate,
          reference: m.reference,
        }));
      const completed = d.tasks.filter(
        (t) =>
          t.stayId === stay.id &&
          t.status === 'Done' &&
          t.completedOn &&
          new Intl.DateTimeFormat('en-CA', { timeZone: zone }).format(new Date(t.completedOn)) ===
            day,
      ).length;
      days.push({ date: day, observations: obs, media, completedTaskCount: completed });
    }
    const diary: StayDiary = {
      stayId: stay.id,
      branchTimeZoneId: zone,
      catHeaders: stay.cats.map((c) => ({
        catId: c.catId,
        catName: c.catName,
        catPhotoUrl: null,
        catIsHighRisk: c.isHighRisk,
        handlingNote: c.handlingNote,
      })),
      days,
    };
    return json(diary);
  }),

  // GET …/stays/{id}/incidents/{incidentId}
  http.get(`${BASE}/stays/:stayId/incidents/:incidentId`, ({ params }) => {
    const stay = findStay(params.stayId as string);
    if (!isMine(stay)) return notFound();
    const incident = getDb().incidents.find(
      (i) => i.id === params.incidentId && i.stayId === stay.id,
    );
    return incident ? json(toIncidentDetail(incident, false)) : notFound();
  }),

  // POST …/incidents/{id}/acknowledge — C19
  http.post(`${BASE}/stays/:stayId/incidents/:incidentId/acknowledge`, ({ params }) => {
    const stay = findStay(params.stayId as string);
    if (!isMine(stay)) return notFound();
    const incident = getDb().incidents.find(
      (i) => i.id === params.incidentId && i.stayId === stay.id,
    );
    if (!incident) return notFound();
    incident.ownerAcknowledgedOn ??= nowIso();
    incident.acknowledgedVia ??= 'Portal';
    persist();
    return noContent();
  }),

  // GET …/approvals/{id} · POST …/respond — C20
  http.get(`${BASE}/stays/:stayId/approvals/:approvalId`, ({ params }) => {
    const stay = findStay(params.stayId as string);
    if (!isMine(stay)) return notFound();
    const detail = toApprovalDetail(stay, params.approvalId as string);
    return detail ? json(detail) : notFound();
  }),
  http.post(`${BASE}/stays/:stayId/approvals/:approvalId/respond`, async ({ request, params }) => {
    const stay = findStay(params.stayId as string);
    if (!isMine(stay)) return notFound();
    const approval = stay.approvals.find((a) => a.id === params.approvalId);
    if (!approval) return notFound();
    if (approval.status !== 'Pending') {
      return bad('VetApprovalAlreadyDecided', 'Answer', 'This request has already been answered.')
        .response;
    }
    const body = (await request.json()) as { approve: boolean };
    approval.status = body.approve ? 'Approved' : 'Declined';
    approval.decidedOn = nowIso();
    approval.decidedVia = 'Portal';
    persist();
    return noContent();
  }),

  // GET …/stays/{id}/summary
  http.get(`${BASE}/stays/:stayId/summary`, ({ params }) => {
    const stay = findStay(params.stayId as string);
    if (!isMine(stay)) return notFound();
    return json(buildSummary(stay.id));
  }),

  // …/waitlist — C24
  http.get(`${BASE}/waitlist`, () => {
    const rows = getDb().waitlist.filter(
      (w) => w.customerId === ids.signedInCustomerId && ['Waiting', 'Notified'].includes(w.status),
    );
    return json(
      rows.map((w) => ({
        id: w.id,
        branchId: w.branchId,
        branchTimeZoneId: zoneOfBranch(w.branchId),
        arrivalOn: w.arrivalOn,
        departureOn: w.departureOn,
        catCount: w.catCount,
        unitType: w.unitType,
        status: w.status,
        notifiedOn: w.notifiedOn,
      })),
    );
  }),
  http.post(`${BASE}/waitlist`, async ({ request }) => {
    const body = (await request.json()) as {
      branchId: string;
      arrivalOn: string;
      departureOn: string;
      catCount: number;
      unitType?: 'Standard' | 'Deluxe' | 'Family';
    };
    const dateProblem = validateDates(body.branchId, body.arrivalOn, body.departureOn, 'Online');
    if (dateProblem) return dateProblem.response;
    const response = availabilityResponse(
      availabilityInputs(body.branchId, body.arrivalOn, body.departureOn, body.catCount, 'Online'),
    );
    if (response.unitTypes.some((t) => t.freeUnitCount > 0)) {
      return bad(
        'UnitsStillAvailable',
        'ArrivalOn',
        'Units have just become available — choose one from the results.',
      ).response;
    }
    const d = getDb();
    if (
      d.waitlist.some(
        (w) =>
          w.customerId === ids.signedInCustomerId &&
          w.arrivalOn === body.arrivalOn &&
          w.departureOn === body.departureOn &&
          w.status === 'Waiting',
      )
    ) {
      return bad(
        'AlreadyWaitlisted',
        'ArrivalOn',
        'You are already on the waitlist for those dates.',
      ).response;
    }
    const id = nextId('f4');
    d.waitlist.push({
      id,
      customerId: ids.signedInCustomerId,
      branchId: body.branchId,
      arrivalOn: body.arrivalOn,
      departureOn: body.departureOn,
      catCount: body.catCount,
      unitType: body.unitType ?? null,
      status: 'Waiting',
      notifiedOn: null,
    });
    persist();
    return json({ id }, 201);
  }),
  http.delete(`${BASE}/waitlist/:entryId`, ({ params }) => {
    const entry = getDb().waitlist.find(
      (w) => w.id === params.entryId && w.customerId === ids.signedInCustomerId,
    );
    if (!entry) return notFound();
    entry.status = 'Withdrawn';
    persist();
    return noContent();
  }),

  // …/daycare — C25
  http.get('*/api/customers/me/daycare', () =>
    json(
      getDb()
        .daycare.filter((v) => v.customerId === ids.signedInCustomerId)
        .sort((a, b) => b.localDate.localeCompare(a.localDate))
        .map((v) => toDaycareVisit(v, false)),
    ),
  ),
  http.post('*/api/customers/me/daycare', async ({ request }) => {
    const body = (await request.json()) as {
      branchId: string;
      catId: string;
      localDate: string;
      basis: 'Hourly' | 'Day';
      expectedHours?: number;
    };
    const capacity = BRANCH_INFO[body.branchId]?.daycareCapacity ?? 0;
    if (capacity === 0) {
      return bad('DaycareNotOffered', 'BranchId', 'Daycare isn’t offered at this branch.').response;
    }
    const d = getDb();
    const taken = d.daycare.filter(
      (v) =>
        v.branchId === body.branchId &&
        v.localDate === body.localDate &&
        ['Booked', 'CheckedIn'].includes(v.status),
    ).length;
    if (taken >= capacity) {
      return bad('DaycareFull', 'LocalDate', 'Daycare is full that day. Try another day.', 409)
        .response;
    }
    const info = await lookupCat(body.catId);
    if (!info) return notFound();
    const gate = await evaluateGate(body.catId, body.localDate);
    if (gate.status === 'Blocked') {
      return bad(
        'VaccinationGateBlocked',
        'CatId',
        `${info.name}’s vaccination is overdue, so ${info.name} can’t be booked in yet.`,
        400,
        {
          vaccinationGate: {
            passed: false,
            blockedCats: [
              {
                catId: info.catId,
                catName: info.name,
                reasons: gate.reasons,
                bookingLink: bookingLinkFor(info.catId),
              },
            ],
          },
        },
      ).response;
    }
    const id = nextId('f5');
    d.daycare.push({
      id,
      branchId: body.branchId,
      customerId: ids.signedInCustomerId,
      customerName: ids.signedInCustomerName,
      catId: info.catId,
      catName: info.name,
      catIsHighRisk: info.isHighRisk,
      handlingNote: info.handlingNote,
      localDate: body.localDate,
      basis: body.basis,
      expectedHours: body.expectedHours ?? null,
      status: 'Booked',
      chargeAmount: null,
      checkedInOn: null,
      checkedOutOn: null,
    });
    persist();
    return json({ id }, 201);
  }),
  http.post('*/api/customers/me/daycare/:visitId/cancel', ({ params }) => {
    const visit = getDb().daycare.find(
      (v) => v.id === params.visitId && v.customerId === ids.signedInCustomerId,
    );
    if (!visit) return notFound();
    visit.status = 'Cancelled';
    persist();
    return noContent();
  }),
];

/** §8.16 — the summary is a read, never stored. */
export function buildSummary(stayId: string): StaySummary {
  const stay = findStay(stayId)!;
  const d = getDb();
  const zone = zoneOfBranch(stay.branchId);
  const cats = stay.cats.map((c) => {
    const logs = new Map<
      string,
      StaySummary['cats'][number]['dailyLogs'][number]['observations']
    >();
    for (const o of d.observations.filter((x) => x.stayId === stay.id && x.catId === c.catId)) {
      const day = new Intl.DateTimeFormat('en-CA', { timeZone: zone }).format(
        new Date(o.recordedOn),
      );
      logs.set(day, [
        ...(logs.get(day) ?? []),
        {
          recordedOn: o.recordedOn,
          portionEaten: o.portionEaten,
          waterIntake: o.waterIntake,
          litterOutput: o.litterOutput,
          mood: o.mood,
          notes: o.notes,
        },
      ]);
    }
    const inW = c.checkIn?.weightKg ?? null;
    const outW = c.checkOut?.weightKg ?? null;
    const change = inW && outW ? Number(outW) - Number(inW) : null;
    return {
      catId: c.catId,
      catName: c.catName,
      catPhotoUrl: null,
      catIsHighRisk: c.isHighRisk,
      handlingNote: c.handlingNote,
      dailyLogs: [...logs.entries()]
        .sort(([a], [b]) => a.localeCompare(b))
        .map(([date, observations]) => ({ date, observations })),
      weightAtCheckInKg: inW,
      weightAtCheckOutKg: outW,
      weightChangeKg: change === null ? null : `${change >= 0 ? '+' : ''}${change.toFixed(1)}`,
      incidents: d.incidents
        .filter((i) => i.stayId === stay.id && i.catId === c.catId)
        .map((i) => ({
          id: i.id,
          catName: c.catName,
          kind: i.kind,
          severity: i.severity,
          occurredOn: i.occurredOn,
          acknowledged: !!i.ownerAcknowledgedOn,
        })),
      medicationsGiven: d.tasks
        .filter(
          (t) =>
            t.stayId === stay.id &&
            t.catId === c.catId &&
            t.kind === 'Medication' &&
            t.status === 'Done',
        )
        .map((t) => ({
          name: t.title.replace(/^Give /, ''),
          dose: '',
          givenOn: t.completedOn ?? t.dueOn,
          givenBy: t.completedByName ?? '',
        })),
      vetVisits: stay.referrals
        .filter((r) => r.catId === c.catId)
        .map((r) => ({
          referralId: r.id,
          reason: r.reason,
          visitId: r.visitId,
          referredOn: r.referredOn,
        })),
      belongings: (c.checkIn?.belongings ?? []).map((b) => {
        const ret = c.checkOut?.returns[b.id];
        return {
          id: b.id,
          kind: b.kind,
          description: b.description,
          returned: ret ? ret.returned : null,
          returnNote: ret?.note ?? null,
        };
      }),
      checkOutSignature: c.checkOut ? c.checkOut.signature : null,
    };
  });
  return {
    stayId: stay.id,
    branchTimeZoneId: zone,
    arrivalOn: stay.arrivalOn,
    departureOn: stay.departureOn,
    checkedOutOn: stay.checkedOutOn,
    cats,
    charges: stay.charges,
    depositReceived: money(acceptedOf(stay)),
    billing: {
      billedTotal: money(0),
      unbilledTotal: toCustomerDetail(stay).balanceDue ?? money(0),
      invoicedOutstanding: money(0),
    },
    invoices: stay.invoices,
  };
}
