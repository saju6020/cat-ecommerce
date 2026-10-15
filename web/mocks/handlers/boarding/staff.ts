import { http, type HttpHandler } from 'msw';
import type {
  BoardingUnitType,
  EnquiryRequest,
  RoutineTaskKind,
  SignatureCapture,
  StayChargeLine,
} from '@/lib/api/types';
import { addDaysIso, zonedLocalToInstant } from '@/lib/format/appointment';
import { nightsBetween } from '@/lib/format/boarding';
import { formatDateOnly } from '@/lib/format/time';
import {
  availabilityInputs,
  availabilityResponse,
  bad,
  busyIntervals,
  cancelAction,
  createEnquiryAction,
  evaluateGate,
  gateView,
  quoteFor,
  reserveAction,
  setCareProfileAction,
  validateDates,
} from './actions';
import { boardingFixtureIds as ids, json, noContent, notFound } from './fixtures';
import type { StoredBelonging, StoredStay } from './mock-types';
import { MAX_STAY_NIGHTS, chargesFromQuote, freeUnits, money, num, round2 } from './rules';
import {
  branchIdOf,
  buildDashboard,
  buildOccupancy,
  currentCard,
  findStay,
  getDb,
  headroomFor,
  ledgerFor,
  nextId,
  nowIso,
  persist,
  staysOfBranch,
  sweepLapses,
  todayOf,
  toDaycareVisit,
  wireStaffDetail,
  toStaffSummary,
  unitById,
  zoneOfBranch,
} from './store';
import { buildSummary } from './customer';
import { profileFromWire } from './wire';

/**
 * `mocks/handlers/boarding/staff.ts` — HO-BRD-002 §12: the staff endpoints of HO-BRD-001 §12.4
 * (branch from `X-Branch-Id`). The mock "server" applies the transition table and the checklist
 * rules; it resolves no session (a `403` is produced with `server.use(...)`).
 */

const BASE = '*/api/staff/boarding';
const staffName = 'Maryam Sultana';

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

interface UnitBody {
  code: string;
  name: string;
  unitType: BoardingUnitType;
  capacityCats: number;
  block: string | null;
  sortOrder: number;
}

interface CheckInBody {
  cats: Array<{
    catId: string;
    identityConfirmed: boolean;
    vaccinationProofVerified: boolean;
    weightKg: number;
    arrivalHealthObservation: string;
    noBelongings: boolean;
    belongings: Array<{
      kind: StoredBelonging['kind'];
      description: string;
      fileIds: string[];
    }>;
    signature: SignatureCapture;
  }>;
}

interface CheckOutBody {
  cats: Array<{
    catId: string;
    weightKg: number;
    belongings: Array<{ belongingId: string; returned: boolean; note: string | null }>;
    signature: SignatureCapture;
  }>;
}

const requirePhotos = () => {
  try {
    return window.sessionStorage.getItem('__purrcare_brd_mock_require_photos__') === 'true';
  } catch {
    return false;
  }
};

function signatureProblem(sig: SignatureCapture | undefined): string | null {
  if (!sig || !sig.signerName?.trim()) return 'the signer’s name';
  if (!sig.confirmedStatement) return 'the confirmation';
  if (sig.method === 'Drawn') {
    const strokes = sig.strokes ?? [];
    const good =
      strokes.length > 0 &&
      strokes.every((s) =>
        s.every(
          ([x, y]) =>
            Number.isInteger(x) &&
            Number.isInteger(y) &&
            x >= 0 &&
            y >= 0 &&
            x <= 10000 &&
            y <= 10000,
        ),
      );
    if (!good) return 'a drawn signature';
  }
  return null;
}

function checklistFailure(code: string, errors: Array<{ property: string; message: string }>) {
  return bad(code, errors[0]!.property, errors[0]!.message, 400, {
    errors: errors.map((e) => ({
      propertyName: e.property,
      errorCode: code,
      errorMessage: e.message,
      resourceName: 'Boarding',
    })),
  });
}

/** Mock-server early check-out rule (§8.15): charge the nights used, on the basis for that length. */
function recalcForEarlyCheckOut(stay: StoredStay, usedNights: number) {
  const quote = quoteFor(
    stay.branchId,
    stay.requestedUnitType,
    stay.arrivalOn,
    addDaysIso(stay.arrivalOn, usedNights),
    stay.cats.length,
  )!;
  const kept: StayChargeLine[] = stay.charges.filter((c) => c.invoiceId);
  const invoicedTotal = kept.reduce((sum, c) => sum + num(c.amount), 0);
  const fresh = chargesFromQuote(quote, `${stay.id.slice(-6)}e`);
  const freshTotal = fresh.reduce((sum, c) => sum + num(c.amount), 0);
  const lines: StayChargeLine[] = kept.length > 0 ? [...kept] : [...fresh];
  if (kept.length > 0 && invoicedTotal > freshTotal) {
    lines.push({
      id: `${stay.id.slice(-6)}-credit`,
      kind: 'EarlyCheckoutCredit',
      description: `Early check-out — ${usedNights} nights used; the rest is credited`,
      amount: money(-round2(invoicedTotal - freshTotal)),
    });
  }
  return { quote, lines };
}

export const boardingStaffHandlers: HttpHandler[] = [
  // ---- units ----
  http.get(`${BASE}/units`, ({ request }) => {
    const branchId = branchIdOf(request);
    return json(
      getDb()
        .units.filter((u) => u.branchId === branchId)
        .sort((a, b) => a.sortOrder - b.sortOrder)
        .map((u) => {
          const card = currentCard(u.unitType);
          return {
            id: u.id,
            code: u.code,
            name: u.name,
            unitType: u.unitType,
            capacityCats: u.capacityCats,
            block: u.block,
            sortOrder: u.sortOrder,
            isActive: u.isActive,
            nightlyRate: card ? money(card.nightly) : null,
          };
        }),
    );
  }),
  http.post(`${BASE}/units`, async ({ request }) => {
    const branchId = branchIdOf(request);
    const body = (await request.json()) as UnitBody;
    const d = getDb();
    if (
      d.units.some(
        (u) => u.branchId === branchId && u.code.toLowerCase() === body.code.toLowerCase(),
      )
    ) {
      return bad(
        'UnitCodeAlreadyExists',
        'Code',
        'A unit with this code already exists at this branch.',
      ).response;
    }
    const id = nextId('d3');
    d.units.push({ id, branchId, isActive: true, ...body });
    persist();
    return json({ unitId: id }, 201);
  }),
  http.put(`${BASE}/units/:unitId/activation`, async ({ request, params }) => {
    const unit = unitById(params.unitId as string);
    if (!unit) return notFound();
    const body = (await request.json()) as { isActive: boolean };
    if (
      !body.isActive &&
      Object.values(getDb().stays).some(
        (s) => s.unitId === unit.id && ['Reserved', 'Confirmed', 'CheckedIn'].includes(s.status),
      )
    ) {
      return bad(
        'UnitHasFutureStays',
        'IsActive',
        'This unit has upcoming stays, so it can’t be switched off.',
      ).response;
    }
    unit.isActive = body.isActive;
    persist();
    return noContent();
  }),
  http.put(`${BASE}/units/:unitId`, async ({ request, params }) => {
    const unit = unitById(params.unitId as string);
    if (!unit) return notFound();
    const body = (await request.json()) as UnitBody;
    if (
      body.unitType !== unit.unitType &&
      Object.values(getDb().stays).some(
        (s) => s.unitId === unit.id && ['Reserved', 'Confirmed', 'CheckedIn'].includes(s.status),
      )
    ) {
      return bad(
        'UnitHasFutureStays',
        'UnitType',
        'This unit has upcoming stays, so its type can’t change.',
      ).response;
    }
    Object.assign(unit, body);
    persist();
    return noContent();
  }),
  http.get(`${BASE}/units/:unitId/maintenance`, ({ params }) =>
    json(getDb().maintenance.filter((m) => m.unitId === params.unitId && !m.isCancelled)),
  ),
  http.post(`${BASE}/units/:unitId/maintenance`, async ({ request, params }) => {
    const unit = unitById(params.unitId as string);
    if (!unit) return notFound();
    const body = (await request.json()) as { startsOn: string; endsOn: string; reason: string };
    const clash = Object.values(getDb().stays).filter(
      (s) =>
        s.unitId === unit.id &&
        ['Reserved', 'Confirmed', 'CheckedIn'].includes(s.status) &&
        s.arrivalOn <= body.endsOn &&
        addDaysIso(s.departureOn, -1) >= body.startsOn,
    );
    if (clash.length > 0) {
      return bad(
        'MaintenanceOverlapsStays',
        'StartsOn',
        `That overlaps ${clash.length === 1 ? 'a stay' : 'stays'} arriving ${clash
          .map((s) => formatDateOnly(s.arrivalOn))
          .join(', ')} in this unit. Move ${clash.length === 1 ? 'it' : 'them'} first.`,
      ).response;
    }
    if (
      getDb().maintenance.some(
        (m) =>
          m.unitId === unit.id &&
          !m.isCancelled &&
          m.startsOn <= body.endsOn &&
          m.endsOn >= body.startsOn,
      )
    ) {
      return bad(
        'MaintenanceOverlapsMaintenance',
        'StartsOn',
        'This unit is already on maintenance during those dates.',
      ).response;
    }
    const id = nextId('d4');
    getDb().maintenance.push({
      id,
      unitId: unit.id,
      branchId: unit.branchId,
      isCancelled: false,
      ...body,
    });
    persist();
    return json({ blockId: id }, 201);
  }),
  http.delete(`${BASE}/units/:unitId/maintenance/:blockId`, ({ params }) => {
    const block = getDb().maintenance.find(
      (m) => m.id === params.blockId && m.unitId === params.unitId,
    );
    if (!block) return notFound();
    block.isCancelled = true;
    persist();
    return noContent();
  }),

  // ---- rate cards ----
  http.get(`${BASE}/rate-cards`, ({ request }) => {
    const unitType = new URL(request.url).searchParams.get('unitType') as BoardingUnitType;
    const now = nowIso();
    const toDto = (c: ReturnType<typeof getDb>['rateCards'][number]) => ({
      id: c.id,
      unitType: c.unitType,
      nightlyRate: money(c.nightly),
      weeklyRate: money(c.weekly),
      monthlyRate: money(c.monthly),
      additionalCatNightlyRate: money(c.additionalCat),
      taxClassId: c.taxClassId,
      taxClassName: 'Standard rate',
      effectiveFrom: c.effectiveFrom,
      setBy: c.setBy,
    });
    const cards = getDb()
      .rateCards.filter((c) => c.unitType === unitType)
      .sort((a, b) => b.effectiveFrom.localeCompare(a.effectiveFrom));
    const current = cards.find((c) => c.effectiveFrom <= now) ?? null;
    // HO-BRD-001 1.1: a flat list, each card with `status`.
    return json(
      cards.map((c) => ({
        ...toDto(c),
        status: c.effectiveFrom > now ? 'Scheduled' : c === current ? 'Current' : 'Past',
      })),
    );
  }),
  http.post(`${BASE}/rate-cards`, async ({ request }) => {
    const body = (await request.json()) as {
      unitType: BoardingUnitType;
      nightlyRate: string;
      weeklyRate: string;
      monthlyRate: string;
      additionalCatNightlyRate: string;
      taxClassId: string;
      effectiveFrom: string;
    };
    const effective = new Date(body.effectiveFrom).toISOString();
    if (effective < nowIso()) {
      return bad(
        'RateCardCannotBeBackdated',
        'EffectiveFrom',
        'A new rate card can’t start in the past.',
      ).response;
    }
    const d = getDb();
    if (d.rateCards.some((c) => c.unitType === body.unitType && c.effectiveFrom === effective)) {
      return bad(
        'DuplicateRateCardEffectiveDate',
        'EffectiveFrom',
        'A rate card already starts at that time.',
      ).response;
    }
    d.rateCards.push({
      id: nextId('c9'),
      unitType: body.unitType,
      nightly: Number(body.nightlyRate),
      weekly: Number(body.weeklyRate),
      monthly: Number(body.monthlyRate),
      additionalCat: Number(body.additionalCatNightlyRate),
      taxClassId: body.taxClassId,
      effectiveFrom: effective,
      setBy: staffName,
    });
    persist();
    return noContent();
  }),
  http.get(`${BASE}/daycare-rate-cards`, () => {
    const now = nowIso();
    const cards = [...getDb().daycareRateCards].sort((a, b) =>
      b.effectiveFrom.localeCompare(a.effectiveFrom),
    );
    const toDto = (c: (typeof cards)[number]) => ({
      id: c.id,
      hourlyRate: money(c.hourly),
      dayRate: money(c.day),
      taxClassId: c.taxClassId,
      taxClassName: 'Standard rate',
      effectiveFrom: c.effectiveFrom,
      setBy: c.setBy,
    });
    const current = cards.find((c) => c.effectiveFrom <= now) ?? null;
    return json({
      current: current ? toDto(current) : null,
      scheduled: cards.filter((c) => c.effectiveFrom > now).map(toDto),
      past: cards.filter((c) => c !== current && c.effectiveFrom <= now).map(toDto),
    });
  }),
  http.post(`${BASE}/daycare-rate-cards`, async ({ request }) => {
    const body = (await request.json()) as {
      hourlyRate: { amount: string };
      dayRate: { amount: string };
      taxClassId: string;
      effectiveFrom: string;
    };
    const effective = new Date(body.effectiveFrom).toISOString();
    if (effective < nowIso()) {
      return bad(
        'RateCardCannotBeBackdated',
        'EffectiveFrom',
        'A new rate card can’t start in the past.',
      ).response;
    }
    getDb().daycareRateCards.push({
      id: nextId('c8'),
      hourly: num(body.hourlyRate),
      day: num(body.dayRate),
      taxClassId: body.taxClassId,
      effectiveFrom: effective,
      setBy: staffName,
    });
    persist();
    return noContent();
  }),

  // ---- care routine ----
  http.get(`${BASE}/care-routine`, () => {
    const routine = getDb().routine;
    return json({
      branchId: ids.mainBranchId,
      isDefault: routine.isDefault,
      times: routine.times.map((t) => ({ kind: t.kind, time: t.localTime })),
    });
  }),
  http.put(`${BASE}/care-routine`, async ({ request }) => {
    const body = (await request.json()) as {
      times: Array<{ kind: RoutineTaskKind; time: string }>;
    };
    for (const kind of ['LitterChange', 'Enrichment', 'ObservationCheck'] as const) {
      if (body.times.filter((t) => t.kind === kind).length > 8) {
        return bad('InvalidCareProfile', 'Times', 'Up to 8 times for each kind of task.').response;
      }
    }
    getDb().routine = {
      isDefault: false,
      times: body.times.map((t) => ({ kind: t.kind, localTime: t.time })),
    };
    persist();
    return noContent();
  }),

  // ---- availability and occupancy ----
  http.get(`${BASE}/availability`, ({ request }) => {
    sweepLapses();
    const url = new URL(request.url);
    const branchId = branchIdOf(request);
    const arrivalOn = url.searchParams.get('arrivalOn') ?? '';
    const departureOn = url.searchParams.get('departureOn') ?? '';
    const problem = validateDates(branchId, arrivalOn, departureOn, 'Staff');
    if (problem) return problem.response;
    return json(
      availabilityResponse(
        availabilityInputs(
          branchId,
          arrivalOn,
          departureOn,
          Number(url.searchParams.get('catCount') ?? 1) || 1,
          'Staff',
          {
            requiresIsolation: url.searchParams.get('requiresIsolation') === 'true',
            excludeStayId: url.searchParams.get('excludeStayId') ?? undefined,
          },
        ),
      ),
    );
  }),
  http.get(`${BASE}/occupancy`, ({ request }) => {
    sweepLapses();
    const url = new URL(request.url);
    const branchId = branchIdOf(request);
    const from = url.searchParams.get('from') ?? todayOf(branchId);
    const to = url.searchParams.get('to') ?? addDaysIso(from, 13);
    return json(buildOccupancy(branchId, from, to));
  }),

  // ---- booking on behalf of a customer ----
  http.post(`${BASE}/enquiries`, async ({ request }) => {
    const body = (await request.json()) as EnquiryRequest;
    let name = 'Customer';
    try {
      const response = await fetch(
        `${process.env.NEXT_PUBLIC_API_BASE_URL ?? ''}/api/staff/customers/${encodeURIComponent(body.customerId ?? '')}`,
        { credentials: 'include' },
      );
      if (response.ok) name = ((await response.json()) as { fullName: string }).fullName;
    } catch {
      // keep the fallback name
    }
    const result = await createEnquiryAction(
      { ...body, branchId: body.branchId || branchIdOf(request) },
      {
        customerId: body.customerId ?? ids.signedInCustomerId,
        customerName: name,
        channel: 'Reception',
      },
    );
    if (!result.ok) return result.response;
    return json(
      {
        stayId: result.value.id,
        vaccinationGate: gateView(result.value.cats),
        quote: result.value.quote,
      },
      201,
    );
  }),
  http.put(`${BASE}/stays/:stayId/enquiry`, async ({ request, params }) => {
    const stay = findStay(params.stayId as string);
    if (!stay) return notFound();
    const body = (await request.json()) as EnquiryRequest;
    const result = await createEnquiryAction(
      body,
      { customerId: stay.customerId, customerName: stay.customerName, channel: 'Reception' },
      stay,
    );
    if (!result.ok) return result.response;
    return json({ stayId: stay.id, vaccinationGate: gateView(stay.cats), quote: stay.quote });
  }),
  http.put(`${BASE}/stays/:stayId/cats/:catId/care-profile`, async ({ request, params }) => {
    const stay = findStay(params.stayId as string);
    if (!stay) return notFound();
    const result = setCareProfileAction(
      stay,
      params.catId as string,
      profileFromWire((await request.json()) as Record<string, unknown>),
    );
    return result.ok ? noContent() : result.response;
  }),
  http.post(
    `${BASE}/stays/:stayId/cats/:catId/vaccination-override`,
    async ({ request, params }) => {
      const stay = findStay(params.stayId as string);
      if (!stay) return notFound();
      const cat = stay.cats.find((c) => c.catId === params.catId);
      if (!cat) return notFound();
      const body = (await request.json()) as { reason: string };
      if (!body.reason?.trim()) {
        return bad('OverrideReasonRequired', 'Reason', 'Enter the reason for the override.')
          .response;
      }
      cat.gateStatus = 'Overridden';
      cat.gateOverriddenBy = staffName;
      cat.gateOverrideReason = body.reason.trim();
      persist();
      return noContent();
    },
  ),
  http.post(`${BASE}/stays/:stayId/reserve`, async ({ params }) => {
    const stay = findStay(params.stayId as string);
    if (!stay) return notFound();
    const result = await reserveAction(stay);
    if (!result.ok) return result.response;
    return json({
      stay: wireStaffDetail(stay),
      deposit: {
        amount: money(stay.depositRequired),
        payBy: stay.payBy,
        instructionsPath: '/pages/boarding-payment-instructions',
      },
    });
  }),
  // C9 — Boarding re-reads Billing's ledger; it records nothing (ADR-FE-106).
  http.post(`${BASE}/stays/:stayId/confirm`, ({ params }) => {
    sweepLapses();
    const stay = findStay(params.stayId as string);
    if (!stay) return notFound();
    if (stay.status !== 'Reserved') {
      return bad('InvalidStayTransition', 'Status', 'Only a reserved stay can be confirmed.')
        .response;
    }
    const ledger = ledgerFor(stay);
    if (ledger.accepted >= stay.depositRequired) {
      stay.status = 'Confirmed';
      stay.depositState = 'Received';
      stay.payBy = null;
      stay.statusHistory.push({
        from: 'Reserved',
        to: 'Confirmed',
        changedOn: nowIso(),
        changedBy: staffName,
      });
      stay.version += 1;
      persist();
      return json({ status: 'Confirmed', depositState: 'Received', outstanding: money(0) });
    }
    if (ledger.pending || stay.depositState === 'PendingApproval') {
      stay.depositState = 'PendingApproval';
      persist();
      return json({
        status: 'Reserved',
        depositState: 'PendingApproval',
        outstanding: money(Math.max(0, round2(stay.depositRequired - ledger.accepted))),
      });
    }
    const outstanding = Math.max(0, round2(stay.depositRequired - ledger.accepted));
    return bad(
      'DepositNotYetReceived',
      'Deposit',
      `Billing shows ${money(ledger.accepted).amount} received against this stay; ${money(outstanding).amount} is still outstanding.`,
    ).response;
  }),
  http.post(`${BASE}/stays/:stayId/cancel`, async ({ request, params }) => {
    const stay = findStay(params.stayId as string);
    if (!stay) return notFound();
    const body = (await request.json().catch(() => ({}))) as { note?: string };
    if (!body.note?.trim()) {
      return bad('CancellationReasonRequired', 'Note', 'Enter a reason for cancelling.').response;
    }
    const result = cancelAction(stay, staffName, false);
    return result.ok ? noContent() : result.response;
  }),
  http.post(`${BASE}/stays/:stayId/no-show`, ({ params }) => {
    const stay = findStay(params.stayId as string);
    if (!stay) return notFound();
    if (stay.status !== 'Confirmed') {
      return bad('InvalidStayTransition', 'Status', 'Only a confirmed stay can be a no-show.')
        .response;
    }
    stay.status = 'NoShow';
    stay.allocations = stay.allocations.map((a) => ({ ...a, isActive: false }));
    stay.unitId = null;
    stay.statusHistory.push({
      from: 'Confirmed',
      to: 'NoShow',
      changedOn: nowIso(),
      changedBy: staffName,
    });
    stay.version += 1;
    persist();
    return noContent();
  }),
  http.post(`${BASE}/stays/:stayId/attention/clear`, ({ params }) => {
    const stay = findStay(params.stayId as string);
    if (!stay) return notFound();
    stay.attention = { needsAttention: false, reason: null, note: null };
    persist();
    return noContent();
  }),

  // ---- stays ----
  http.get(`${BASE}/stays`, ({ request }) => {
    sweepLapses();
    const url = new URL(request.url);
    const branchId = branchIdOf(request);
    const status = url.searchParams.get('status');
    const customerId = url.searchParams.get('customerId');
    const catId = url.searchParams.get('catId');
    const from = url.searchParams.get('from');
    const to = url.searchParams.get('to');
    const rows = staysOfBranch(branchId)
      .filter((s) => (status ? s.status === status : true))
      .filter((s) => (customerId ? s.customerId === customerId : true))
      .filter((s) => (catId ? s.cats.some((c) => c.catId === catId) : true))
      .filter((s) => (from ? s.departureOn >= from : true))
      .filter((s) => (to ? s.arrivalOn <= to : true))
      .sort((a, b) => b.arrivalOn.localeCompare(a.arrivalOn));
    return json(pageOf(rows.map(toStaffSummary), url));
  }),
  http.get(`${BASE}/stays/:stayId`, ({ params }) => {
    sweepLapses();
    const stay = findStay(params.stayId as string);
    return stay ? json(wireStaffDetail(stay)) : notFound();
  }),
  http.get(`${BASE}/stays/:stayId/summary`, ({ params }) => {
    const stay = findStay(params.stayId as string);
    return stay ? json(buildSummary(stay.id)) : notFound();
  }),

  // ---- check-in (C12) ----
  http.post(`${BASE}/stays/:stayId/check-in`, async ({ request, params }) => {
    const stay = findStay(params.stayId as string);
    if (!stay) return notFound();
    if (stay.status !== 'Confirmed') {
      return bad('InvalidStayTransition', 'Status', 'Only a confirmed stay can be checked in.')
        .response;
    }
    const today = todayOf(stay.branchId);
    if (today !== stay.arrivalOn && today !== addDaysIso(stay.arrivalOn, 1)) {
      return bad(
        'CheckInNotOnArrivalDate',
        'ArrivalOn',
        'A stay can only be checked in on its arrival day, or the day after.',
      ).response;
    }
    const body = (await request.json()) as CheckInBody;
    const missing: Array<{ property: string; message: string }> = [];
    stay.cats.forEach((cat, index) => {
      const input = body.cats?.find((c) => c.catId === cat.catId);
      const at = (field: string) => `Cats[${index}].${field}`;
      if (!input) {
        missing.push({ property: at('CatId'), message: `${cat.catName}: nothing was submitted.` });
        return;
      }
      if (!input.identityConfirmed)
        missing.push({
          property: at('IdentityConfirmed'),
          message: `${cat.catName}: confirm the cat’s identity.`,
        });
      if (!input.vaccinationProofVerified)
        missing.push({
          property: at('VaccinationProofVerified'),
          message: `${cat.catName}: confirm the vaccination proof was checked.`,
        });
      if (!(input.weightKg >= 0.1 && input.weightKg <= 30))
        missing.push({
          property: at('WeightKg'),
          message: `${cat.catName}: the weight is missing.`,
        });
      if (!input.arrivalHealthObservation?.trim())
        missing.push({
          property: at('ArrivalHealthObservation'),
          message: `${cat.catName}: describe how the cat looks on arrival.`,
        });
      if (!input.noBelongings && (input.belongings?.length ?? 0) === 0)
        missing.push({
          property: at('Belongings'),
          message: `${cat.catName}: list the belongings, or say there are none.`,
        });
      if (
        requirePhotos() &&
        !input.noBelongings &&
        (input.belongings ?? []).length > 0 &&
        !(input.belongings ?? []).some((b) => (b.fileIds?.length ?? 0) > 0)
      ) {
        missing.push({
          property: at('Belongings'),
          message: `${cat.catName}: add a photo reference for the belongings — this branch requires one.`,
        });
      }
      const sig = signatureProblem(input.signature);
      if (sig)
        missing.push({
          property: at('OwnerSignature'),
          message: `${cat.catName}: ${sig} is missing.`,
        });
    });
    if (missing.length > 0) return checklistFailure('CheckInChecklistIncomplete', missing).response;
    // The gate is re-evaluated at check-in: an overdue vaccination holds the cat even if booked.
    for (const cat of stay.cats) {
      if (cat.gateStatus === 'Overridden') continue;
      const gate = await evaluateGate(cat.catId, stay.departureOn);
      cat.gateStatus = gate.status;
      cat.gateReasons = gate.reasons;
    }
    const held = stay.cats.filter((c) => c.gateStatus === 'Blocked');
    if (held.length > 0) {
      persist();
      return bad(
        'VaccinationGateBlocked',
        'Cats',
        `${held.map((c) => c.catName).join(' and ')} can’t be checked in: a vaccination is overdue.`,
      ).response;
    }
    const at = new Date().toISOString();
    stay.cats.forEach((cat) => {
      const input = body.cats.find((c) => c.catId === cat.catId)!;
      cat.checkIn = {
        weightKg: Number(input.weightKg).toFixed(1),
        observation: input.arrivalHealthObservation.trim(),
        belongings: (input.belongings ?? []).map((b, i) => ({
          id: `bel-${stay.id.slice(-4)}-${cat.catId.slice(-3)}-${i + 1}`,
          kind: b.kind,
          description: b.description,
          reference: b.fileIds?.[0] ?? null,
        })),
        signature: {
          signerName: input.signature.signerName,
          method: input.signature.method,
          signedOn: at,
        },
      };
    });
    stay.status = 'CheckedIn';
    stay.checkedInOn = at;
    stay.statusHistory.push({
      from: 'Confirmed',
      to: 'CheckedIn',
      changedOn: at,
      changedBy: staffName,
    });
    stay.version += 1;
    // The worker's next pass generates today's tasks; the mock does it straight away.
    const d = getDb();
    for (const cat of stay.cats) {
      for (const feeding of cat.careProfile?.feedings ?? []) {
        d.tasks.push({
          id: nextId('f3'),
          stayId: stay.id,
          catId: cat.catId,
          catName: cat.catName,
          unitId: stay.unitId ?? '',
          kind: 'Feed',
          title: `Feed ${feeding.portion} — ${feeding.foodSource === 'OwnFood' ? 'own food' : 'house food'}`,
          dueOn: new Date(Date.now() + 3_600_000).toISOString(),
          status: 'Pending',
          assignedToName: 'Tanvir Islam',
          assignedToMe: true,
          completedOn: null,
          completedByName: null,
          skipReason: null,
        });
      }
    }
    persist();
    return json({ stay: wireStaffDetail(stay) });
  }),

  // ---- early check-out preview (HO-BRD-001 1.1, committed shape) ----
  http.get(`${BASE}/stays/:stayId/check-out-preview`, ({ params }) => {
    const stay = findStay(params.stayId as string);
    if (!stay) return notFound();
    const today = todayOf(stay.branchId);
    const booked = nightsBetween(stay.arrivalOn, stay.departureOn);
    const total = (lines: StayChargeLine[]) => lines.reduce((sum, c) => sum + num(c.amount), 0);
    const common = {
      today,
      bookedDepartureOn: stay.departureOn,
    };
    if (today >= stay.departureOn) {
      return json({
        ...common,
        isEarly: false,
        nightsBooked: booked,
        nightsCharged: booked,
        quote: null,
        lines: [],
        credit: money(0),
        creditOutcome: 'None',
        chargedDepartureOn: stay.departureOn,
        unitFreeFrom: new Date().toISOString(),
        previousTotal: money(total(stay.charges)),
        newTotal: money(total(stay.charges)),
      });
    }
    const used = Math.max(1, nightsBetween(stay.arrivalOn, today));
    const recalculated = recalcForEarlyCheckOut(stay, used);
    const oldIds = new Set(stay.charges.map((c) => c.id));
    const newIds = new Set(recalculated.lines.map((c) => c.id));
    const lines = [
      ...stay.charges.map((c) => ({
        lineRef: c.id,
        description: c.description,
        net: c.amount,
        action: newIds.has(c.id) ? 'Keep' : 'Void',
      })),
      ...recalculated.lines
        .filter((c) => !oldIds.has(c.id))
        .map((c) => ({
          lineRef: c.id,
          description: c.description,
          net: c.amount,
          action: 'Record',
        })),
    ];
    const before = total(stay.charges);
    const after = total(recalculated.lines);
    const credit = Math.max(0, round2(before - after));
    return json({
      ...common,
      isEarly: true,
      nightsBooked: booked,
      nightsCharged: used,
      quote: recalculated.quote,
      lines,
      credit: money(credit),
      creditOutcome: credit > 0 ? 'Credit' : 'None',
      chargedDepartureOn: addDaysIso(stay.arrivalOn, used),
      unitFreeFrom: new Date().toISOString(),
      previousTotal: money(before),
      newTotal: money(after),
    });
  }),

  // ---- check-out (C22) ----
  http.post(`${BASE}/stays/:stayId/check-out`, async ({ request, params }) => {
    const stay = findStay(params.stayId as string);
    if (!stay) return notFound();
    if (stay.status !== 'CheckedIn') {
      return bad(
        'InvalidStayTransition',
        'Status',
        'Only a stay that is in the care centre can be checked out.',
      ).response;
    }
    const body = (await request.json()) as CheckOutBody;
    const missing: Array<{ property: string; message: string }> = [];
    stay.cats.forEach((cat, index) => {
      const input = body.cats?.find((c) => c.catId === cat.catId);
      const at = (field: string) => `Cats[${index}].${field}`;
      if (!input) {
        missing.push({ property: at('CatId'), message: `${cat.catName}: nothing was submitted.` });
        return;
      }
      if (!(input.weightKg >= 0.1 && input.weightKg <= 30))
        missing.push({
          property: at('WeightKg'),
          message: `${cat.catName}: the weight is missing.`,
        });
      for (const b of cat.checkIn?.belongings ?? []) {
        const r = input.belongings?.find((x) => x.belongingId === b.id);
        if (!r)
          missing.push({
            property: at('Belongings'),
            message: `${cat.catName}: say whether “${b.description}” was returned.`,
          });
        else if (!r.returned && !r.note?.trim())
          missing.push({
            property: at('Belongings'),
            message: `${cat.catName}: say why “${b.description}” was not returned.`,
          });
      }
      if (signatureProblem(input.signature))
        missing.push({
          property: at('OwnerSignature'),
          message: `${cat.catName}: the signature is missing.`,
        });
    });
    if (missing.length > 0)
      return checklistFailure('CheckOutChecklistIncomplete', missing).response;
    const today = todayOf(stay.branchId);
    const at = new Date().toISOString();
    if (today < stay.departureOn) {
      const used = Math.max(1, nightsBetween(stay.arrivalOn, today));
      const recalculated = recalcForEarlyCheckOut(stay, used);
      stay.quote = recalculated.quote;
      stay.charges = recalculated.lines;
    }
    stay.cats.forEach((cat) => {
      const input = body.cats.find((c) => c.catId === cat.catId)!;
      cat.checkOut = {
        weightKg: Number(input.weightKg).toFixed(1),
        returns: Object.fromEntries(
          (input.belongings ?? []).map((b) => [
            b.belongingId,
            { returned: b.returned, note: b.note },
          ]),
        ),
        signature: {
          signerName: input.signature.signerName,
          method: input.signature.method,
          signedOn: at,
        },
      };
    });
    stay.status = 'CheckedOut';
    stay.checkedOutOn = at;
    stay.allocations = stay.allocations.map((a) => ({ ...a, isActive: true }));
    stay.statusHistory.push({
      from: 'CheckedIn',
      to: 'CheckedOut',
      changedOn: at,
      changedBy: staffName,
    });
    stay.version += 1;
    getDb().tasks = getDb().tasks.map((t) =>
      t.stayId === stay.id && t.status === 'Pending' ? { ...t, status: 'Voided' as const } : t,
    );
    persist();
    return json({ stay: wireStaffDetail(stay) });
  }),

  // ---- extend / relocate (C21) ----
  http.post(`${BASE}/stays/:stayId/extend`, async ({ request, params }) => {
    const stay = findStay(params.stayId as string);
    if (!stay) return notFound();
    const body = (await request.json()) as { newDepartureOn: string; expectedVersion: number };
    if (body.expectedVersion !== stay.version)
      return bad('StaleVersion', 'ExpectedVersion', 'This stay was changed by someone else.', 409)
        .response;
    if (!['Confirmed', 'CheckedIn'].includes(stay.status)) {
      return bad(
        'InvalidStayTransition',
        'Status',
        'Only a confirmed or in-house stay can be extended.',
      ).response;
    }
    if (body.newDepartureOn <= stay.departureOn) {
      return bad(
        'InvalidStayDates',
        'NewDepartureOn',
        'The new departure must be after the current one.',
      ).response;
    }
    if (nightsBetween(stay.arrivalOn, body.newDepartureOn) > MAX_STAY_NIGHTS) {
      return bad(
        'StayTooLong',
        'NewDepartureOn',
        `We can take a stay of up to ${MAX_STAY_NIGHTS} nights in one booking.`,
      ).response;
    }
    const busy = busyIntervals(stay.branchId, stay.id);
    const myUnit = unitById(stay.unitId);
    const free = myUnit
      ? !busy.some(
          (b) => b.unitId === myUnit.id && b.from < body.newDepartureOn && stay.departureOn < b.to,
        )
      : false;
    if (!free) {
      const from = stay.status === 'CheckedIn' ? todayOf(stay.branchId) : stay.arrivalOn;
      const options = getDb()
        .units.filter(
          (u) =>
            u.branchId === stay.branchId &&
            u.isActive &&
            u.id !== stay.unitId &&
            (stay.cats.some((c) => c.suspectedInfectious)
              ? u.unitType === 'Isolation'
              : u.unitType !== 'Isolation') &&
            freeUnits([u], busy, from, body.newDepartureOn, stay.cats.length, u.unitType).length >
              0,
        )
        .map((u) => ({
          unitId: u.id,
          code: u.code,
          name: u.name,
          unitType: u.unitType,
          block: u.block,
        }));
      return bad(
        'UnitNotFreeForExtension',
        'NewDepartureOn',
        `${myUnit?.name ?? 'The unit'} isn’t free until ${formatDateOnly(body.newDepartureOn)}.`,
        400,
        { relocationOptions: options },
      ).response;
    }
    applyExtension(stay, body.newDepartureOn);
    persist();
    return json(wireStaffDetail(stay));
  }),
  http.post(`${BASE}/stays/:stayId/relocate`, async ({ request, params }) => {
    const stay = findStay(params.stayId as string);
    if (!stay) return notFound();
    const body = (await request.json()) as {
      targetUnitId: string;
      effectiveFromLocalDate: string;
      reason: string;
      newDepartureOn?: string;
      expectedVersion: number;
    };
    if (body.expectedVersion !== stay.version)
      return bad('StaleVersion', 'ExpectedVersion', 'This stay was changed by someone else.', 409)
        .response;
    if (!body.reason?.trim())
      return bad('RelocationReasonRequired', 'Reason', 'Enter the reason for moving the cat.')
        .response;
    const target = unitById(body.targetUnitId);
    if (!target) return notFound();
    const newDeparture = body.newDepartureOn ?? stay.departureOn;
    const busy = busyIntervals(stay.branchId, stay.id);
    if (
      freeUnits(
        [target],
        busy,
        body.effectiveFromLocalDate,
        newDeparture,
        stay.cats.length,
        target.unitType,
      ).length === 0
    ) {
      return bad(
        'UnitNoLongerAvailable',
        'TargetUnitId',
        `${target.name} isn’t free for those dates.`,
        409,
      ).response;
    }
    if (stay.cats.some((c) => c.suspectedInfectious) && target.unitType !== 'Isolation') {
      return bad(
        'IsolationUnitRequired',
        'TargetUnitId',
        'A cat suspected of being infectious must be in an isolation unit.',
      ).response;
    }
    moveStay(stay, target.id, body.reason.trim(), body.effectiveFromLocalDate);
    if (body.newDepartureOn && body.newDepartureOn > stay.departureOn)
      applyExtension(stay, body.newDepartureOn);
    persist();
    return json(wireStaffDetail(stay));
  }),

  // ---- isolation (C17) ----
  http.post(
    `${BASE}/stays/:stayId/cats/:catId/suspected-infectious`,
    async ({ request, params }) => {
      const stay = findStay(params.stayId as string);
      if (!stay) return notFound();
      const cat = stay.cats.find((c) => c.catId === params.catId);
      if (!cat) return notFound();
      const body = (await request.json()) as { note: string; targetIsolationUnitId?: string };
      if (!body.note?.trim())
        return bad('InvalidCareProfile', 'Note', 'Enter a note for the flag.').response;
      const current = unitById(stay.unitId);
      if (current?.unitType !== 'Isolation') {
        const target = unitById(body.targetIsolationUnitId ?? null);
        if (!target)
          return bad(
            'IsolationUnitRequired',
            'TargetIsolationUnitId',
            'Choose the isolation unit to move the cat to.',
          ).response;
        const busy = busyIntervals(stay.branchId, stay.id);
        const from = todayOf(stay.branchId);
        if (
          target.unitType !== 'Isolation' ||
          freeUnits([target], busy, from, stay.departureOn, stay.cats.length, 'Isolation')
            .length === 0
        ) {
          return bad(
            'NoIsolationUnitAvailable',
            'TargetIsolationUnitId',
            'No isolation unit is free, so the cat has not been moved.',
          ).response;
        }
        moveStay(stay, target.id, 'Suspected infectious', from);
      }
      cat.suspectedInfectious = true;
      stay.version += 1;
      persist();
      return noContent();
    },
  ),
  http.delete(`${BASE}/stays/:stayId/cats/:catId/suspected-infectious`, ({ params }) => {
    const stay = findStay(params.stayId as string);
    const cat = stay?.cats.find((c) => c.catId === params.catId);
    if (!stay || !cat) return notFound();
    cat.suspectedInfectious = false;
    stay.version += 1;
    persist();
    return noContent();
  }),
  http.post(`${BASE}/stays/:stayId/isolation-entries`, async ({ request, params }) => {
    const stay = findStay(params.stayId as string);
    if (!stay) return notFound();
    const unit = unitById(stay.unitId);
    if (unit?.unitType !== 'Isolation')
      return bad('NotInIsolation', 'StayId', 'This cat isn’t in an isolation unit.').response;
    const body = (await request.json()) as { purpose: string };
    stay.isolationEntries.push({
      id: nextId('b1'),
      unitName: unit.name,
      staffName: 'Tanvir Islam',
      enteredOn: nowIso(),
      leftOn: null,
      purpose: body.purpose?.trim() || 'Care visit',
    });
    persist();
    return noContent();
  }),
  http.post(`${BASE}/stays/:stayId/isolation-entries/:entryId/exit`, ({ params }) => {
    const stay = findStay(params.stayId as string);
    const entry = stay?.isolationEntries.find((e) => e.id === params.entryId);
    if (!stay || !entry) return notFound();
    entry.leftOn ??= nowIso();
    persist();
    return noContent();
  }),

  // ---- referral, headroom, approvals (C20) ----
  http.post(`${BASE}/stays/:stayId/referrals`, async ({ request, params }) => {
    const stay = findStay(params.stayId as string);
    if (!stay) return notFound();
    if (stay.status !== 'CheckedIn')
      return bad(
        'ReferralStayNotInHouse',
        'StayId',
        'The cat is not in the care centre, so it cannot be referred.',
      ).response;
    const body = (await request.json()) as {
      catId: string;
      reason: string;
      urgency: 'Routine' | 'Urgent' | 'Emergency';
    };
    const cat = stay.cats.find((c) => c.catId === body.catId);
    if (!cat) return notFound();
    const id = nextId('a7');
    stay.referrals.push({
      id,
      catId: cat.catId,
      catName: cat.catName,
      reason: body.reason,
      urgency: body.urgency,
      referredOn: nowIso(),
      referredBy: 'Tanvir Islam',
      visitId: null,
    });
    persist();
    return json({ referralId: id }, 201);
  }),
  http.post(`${BASE}/stays/:stayId/referrals/:referralId/return`, ({ params }) => {
    const stay = findStay(params.stayId as string);
    if (!stay) return notFound();
    const referral = stay.referrals.find((r) => r.id === params.referralId);
    if (!referral) return notFound();
    referral.returnedOn = nowIso();
    const cat = stay.cats.find((c) => c.catId === referral.catId);
    if (cat) {
      cat.awayFromUnit = false;
      cat.awayReason = null;
    }
    persist();
    return noContent();
  }),
  http.get(`${BASE}/stays/:stayId/cats/:catId/vet-headroom`, ({ params }) => {
    const stay = findStay(params.stayId as string);
    if (!stay) return notFound();
    const h = headroomFor(stay, params.catId as string);
    return json({
      limit: money(h.limit),
      committed: money(h.committed),
      approvedIncrease: money(0),
      headroom: money(h.limit - h.committed),
      vetVisitAuthorised: h.vetVisitAuthorised,
    });
  }),
  http.post(`${BASE}/stays/:stayId/vet-approvals`, async ({ request, params }) => {
    const stay = findStay(params.stayId as string);
    if (!stay) return notFound();
    const body = (await request.json()) as {
      catId: string;
      amount: string;
      description: string;
    };
    const cat = stay.cats.find((c) => c.catId === body.catId);
    if (!cat) return notFound();
    const id = nextId('f2');
    stay.approvals.push({
      id,
      catId: cat.catId,
      catName: cat.catName,
      amount: money(num({ amount: body.amount })),
      description: body.description,
      status: 'Pending',
      requestedOn: nowIso(),
      decidedOn: null,
      decidedVia: null,
    });
    persist();
    return json({ approvalId: id }, 201);
  }),
  http.post(
    `${BASE}/stays/:stayId/vet-approvals/:approvalId/staff-answer`,
    async ({ request, params }) => {
      const stay = findStay(params.stayId as string);
      const approval = stay?.approvals.find((a) => a.id === params.approvalId);
      if (!stay || !approval) return notFound();
      if (approval.status !== 'Pending')
        return bad('VetApprovalAlreadyDecided', 'Answer', 'This request has already been answered.')
          .response;
      const body = (await request.json()) as { approve: boolean; note: string };
      approval.status = body.approve ? 'Approved' : 'Declined';
      approval.decidedOn = nowIso();
      approval.decidedVia = 'Staff';
      persist();
      return noContent();
    },
  ),

  // ---- dashboard ----
  http.get(`${BASE}/dashboard`, ({ request }) => {
    sweepLapses();
    return json(buildDashboard(branchIdOf(request)));
  }),

  // ---- daycare (C25) ----
  http.get('*/api/staff/daycare', ({ request }) => {
    const branchId = branchIdOf(request);
    const date = new URL(request.url).searchParams.get('date') ?? todayOf(branchId);
    const capacity = branchId === ids.mainBranchId ? 3 : 0;
    return json({
      date,
      branchTimeZoneId: zoneOfBranch(branchId),
      offered: capacity > 0,
      capacity,
      visits: getDb()
        .daycare.filter((v) => v.branchId === branchId && v.localDate === date)
        .map((v) => toDaycareVisit(v, true)),
    });
  }),
  ...(['check-in', 'check-out', 'cancel', 'no-show'] as const).map((action) =>
    http.post(`*/api/staff/daycare/:visitId/${action}`, ({ params }) => {
      const visit = getDb().daycare.find((v) => v.id === params.visitId);
      if (!visit) return notFound();
      const at = nowIso();
      if (action === 'check-in') {
        visit.status = 'CheckedIn';
        visit.checkedInOn = at;
      } else if (action === 'check-out') {
        visit.status = 'CheckedOut';
        visit.checkedOutOn = at;
        const day = getDb().daycareRateCards[0]?.day ?? 800;
        const hourly = getDb().daycareRateCards[0]?.hourly ?? 150;
        visit.chargeAmount =
          visit.basis === 'Day' ? day : Math.min(day, Math.ceil(visit.expectedHours ?? 1) * hourly);
      } else if (action === 'cancel') visit.status = 'Cancelled';
      else visit.status = 'NoShow';
      persist();
      return noContent();
    }),
  ),
];

function applyExtension(stay: StoredStay, newDepartureOn: string) {
  const oldTotal = stay.charges.reduce((sum, c) => sum + num(c.amount), 0);
  stay.departureOn = newDepartureOn;
  const quote = quoteFor(
    stay.branchId,
    stay.requestedUnitType,
    stay.arrivalOn,
    newDepartureOn,
    stay.cats.length,
  );
  if (quote) {
    stay.quote = quote;
    if (stay.charges.some((c) => c.invoiceId)) {
      // Invoiced lines are never changed — the difference becomes a new line (§8.15).
      stay.charges = [
        ...stay.charges,
        {
          id: `${stay.id.slice(-6)}-ext${stay.version}`,
          kind: 'Adjustment',
          description: `Extension to ${formatDateOnly(newDepartureOn)}`,
          amount: money(round2(num(quote.total) - oldTotal)),
        },
      ];
    } else {
      stay.charges = chargesFromQuote(quote, `${stay.id.slice(-6)}x${stay.version}`);
    }
  }
  stay.allocations = stay.allocations.map((a, i, all) =>
    i === all.length - 1
      ? { ...a, to: zonedLocalToInstant(newDepartureOn, '14:00', zoneOfBranch(stay.branchId)) }
      : a,
  );
  stay.version += 1;
}

function moveStay(stay: StoredStay, targetUnitId: string, reason: string, effectiveFrom: string) {
  const target = unitById(targetUnitId)!;
  const cut = zonedLocalToInstant(effectiveFrom, '00:00', zoneOfBranch(stay.branchId));
  stay.allocations = [
    ...stay.allocations.map((a, i, all) =>
      i === all.length - 1 ? { ...a, to: cut, isActive: false } : a,
    ),
    {
      id: nextId('a1'),
      unitName: target.name,
      unitType: target.unitType,
      from: cut,
      to: zonedLocalToInstant(stay.departureOn, '14:00', zoneOfBranch(stay.branchId)),
      isActive: true,
      reason,
    },
  ];
  stay.unitId = target.id;
  stay.version += 1;
  // Tasks continue on the new unit (E-E2).
  const d = getDb();
  d.tasks = d.tasks.map((t) =>
    t.stayId === stay.id && t.status === 'Pending' ? { ...t, unitId: target.id } : t,
  );
}
