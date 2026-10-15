import { HttpResponse } from 'msw';
import type {
  BoardingUnitType,
  CareProfile,
  EnquiryRequest,
  GateReason,
  StayQuote,
} from '@/lib/api/types';
import { addDaysIso, zonedLocalToInstant } from '@/lib/format/appointment';
import { nightsBetween } from '@/lib/format/boarding';
import { formatDateOnly } from '@/lib/format/time';
import { apiUrl, boardingFixtureIds as ids, BRANCH_INFO, serverNow, violation } from './fixtures';
import type { StoredCat, StoredStay } from './mock-types';
import {
  BOOKING_HORIZON_DAYS,
  CHECK_IN_START,
  DEPOSIT_PERCENT,
  findAlternatives,
  findAvailability,
  MAX_STAY_NIGHTS,
  num,
  quoteStay,
  RESERVATION_HOLD_HOURS,
  round2,
  TYPE_CAPACITY,
  chargesFromQuote,
  type AvailabilityInputs,
  type BusyInterval,
} from './rules';
import {
  cardValuesFor,
  getDb,
  persist,
  todayOf,
  unitById,
  zoneOfBranch,
  staysOfBranch,
  nowIso,
} from './store';

/**
 * `mocks/handlers/boarding/actions.ts` — HO-BRD-002 §12. The commands both the customer and the
 * staff handlers share (enquiry, care profile, reserve, cancel) and the look-ups they need (a cat's
 * name and vaccination state from the Cat Profiles mock, availability from the stored stays). The
 * mock SERVER applies the rules here; the real UI never does (ADR-FE-105).
 */

export type Failure = { ok: false; response: Response };
export type Success<T> = { ok: true; value: T };
export type Result<T> = Failure | Success<T>;

const ok = <T>(value: T): Success<T> => ({ ok: true, value });
export const bad = (
  code: string,
  property: string,
  message: string,
  status: 400 | 409 = 400,
  extra: Record<string, unknown> = {},
): Failure => {
  const base = violation(status, {
    propertyName: property,
    errorCode: code,
    errorMessage: message,
  });
  if (Object.keys(extra).length === 0) return { ok: false, response: base };
  return {
    ok: false,
    response: HttpResponse.json(
      {
        errors: [
          {
            propertyName: property,
            errorCode: code,
            errorMessage: message,
            resourceName: 'Boarding',
          },
        ],
        isValid: false,
        ...extra,
      },
      { status, headers: base.headers },
    ),
  };
};

// ---- cats ----

export interface CatInfo {
  catId: string;
  name: string;
  isHighRisk: boolean;
  handlingNote: string | null;
}

export async function lookupCat(catId: string): Promise<CatInfo | null> {
  for (const s of Object.values(getDb().stays)) {
    const known = s.cats.find((c) => c.catId === catId);
    if (known) {
      return {
        catId,
        name: known.catName,
        isHighRisk: known.isHighRisk,
        handlingNote: known.handlingNote,
      };
    }
  }
  try {
    const response = await fetch(apiUrl(`/api/cats/${encodeURIComponent(catId)}`), {
      credentials: 'include',
    });
    if (!response.ok) return null;
    const cat = (await response.json()) as {
      name: string;
      isHighRisk: boolean;
      handlingNote: string | null;
    };
    return { catId, name: cat.name, isHighRisk: cat.isHighRisk, handlingNote: cat.handlingNote };
  } catch {
    return null;
  }
}

export interface GateResult {
  status: 'Passed' | 'Blocked';
  reasons: GateReason[];
}

/** §8.6 — passes when no core vaccine is due on or before the last day of the stay. */
export async function evaluateGate(catId: string, departureOn: string): Promise<GateResult> {
  if (catId.startsWith('c1000000')) return { status: 'Passed', reasons: [] };
  try {
    const response = await fetch(
      apiUrl(`/api/cats/${encodeURIComponent(catId)}/vaccination-status`),
      {
        credentials: 'include',
      },
    );
    if (!response.ok) return { status: 'Passed', reasons: [] };
    const body = (await response.json()) as {
      vaccines: Array<{
        vaccineName: string;
        status: string;
        nextDueOn: string | null;
        reason: string | null;
      }>;
    };
    const reasons: GateReason[] = [];
    for (const v of body.vaccines ?? []) {
      if (v.reason === 'NeverAdministered') {
        reasons.push({ reason: 'NeverAdministered', vaccineName: v.vaccineName, dueOn: null });
      } else if (v.status === 'Overdue' || (v.nextDueOn && v.nextDueOn <= departureOn)) {
        reasons.push({
          reason: 'DueBeforeDeparture',
          vaccineName: v.vaccineName,
          dueOn: v.nextDueOn,
        });
      }
    }
    return reasons.length > 0 ? { status: 'Blocked', reasons } : { status: 'Passed', reasons: [] };
  } catch {
    return { status: 'Passed', reasons: [] };
  }
}

export function bookingLinkFor(catId: string): string {
  return `/book?catId=${encodeURIComponent(catId)}&category=Vaccination`;
}

export function gateView(cats: StoredCat[]) {
  const blocked = cats.filter((c) => c.gateStatus === 'Blocked');
  return {
    passed: blocked.length === 0,
    blockedCats: blocked.map((c) => ({
      catId: c.catId,
      catName: c.catName,
      reasons: c.gateReasons,
      bookingLink: bookingLinkFor(c.catId),
    })),
  };
}

// ---- dates and availability ----

export function validateDates(
  branchId: string,
  arrivalOn: string,
  departureOn: string,
  mode: 'Online' | 'Staff',
): Failure | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(arrivalOn) || !/^\d{4}-\d{2}-\d{2}$/.test(departureOn)) {
    return bad('InvalidStayDates', 'ArrivalOn', 'Choose an arrival and a departure date.');
  }
  if (departureOn <= arrivalOn) {
    return bad('InvalidStayDates', 'DepartureOn', 'Departure must be after arrival.');
  }
  const nights = nightsBetween(arrivalOn, departureOn);
  if (nights > MAX_STAY_NIGHTS) {
    return bad(
      'StayTooLong',
      'DepartureOn',
      `We can take a stay of up to ${MAX_STAY_NIGHTS} nights in one booking.`,
    );
  }
  if (mode === 'Online') {
    const today = todayOf(branchId);
    if (arrivalOn < today) {
      return bad('InvalidStayDates', 'ArrivalOn', 'Arrival can’t be in the past.');
    }
    if (arrivalOn > addDaysIso(today, BOOKING_HORIZON_DAYS)) {
      return bad(
        'StayBeyondHorizon',
        'ArrivalOn',
        `You can book up to ${BOOKING_HORIZON_DAYS} days ahead.`,
      );
    }
  }
  return null;
}

export function busyIntervals(branchId: string, excludeStayId?: string): BusyInterval[] {
  const out: BusyInterval[] = [];
  for (const s of staysOfBranch(branchId)) {
    if (s.id === excludeStayId || !s.unitId) continue;
    if (!['Reserved', 'Confirmed', 'CheckedIn'].includes(s.status)) continue;
    out.push({ unitId: s.unitId, from: s.arrivalOn, to: s.departureOn });
  }
  for (const m of getDb().maintenance) {
    if (m.branchId !== branchId || m.isCancelled) continue;
    out.push({ unitId: m.unitId, from: m.startsOn, to: addDaysIso(m.endsOn, 1) });
  }
  return out;
}

export function availabilityInputs(
  branchId: string,
  arrivalOn: string,
  departureOn: string,
  catCount: number,
  mode: 'Online' | 'Staff',
  extra: { requiresIsolation?: boolean; excludeStayId?: string } = {},
): AvailabilityInputs {
  return {
    branchId,
    units: getDb().units.filter((u) => u.branchId === branchId),
    busy: busyIntervals(branchId, extra.excludeStayId),
    cardFor: cardValuesFor,
    arrivalOn,
    departureOn,
    catCount,
    mode,
    requiresIsolation: extra.requiresIsolation ?? false,
  };
}

export function availabilityResponse(inputs: AvailabilityInputs) {
  const result = findAvailability(inputs);
  const zone = zoneOfBranch(inputs.branchId);
  const from = zonedLocalToInstant(inputs.arrivalOn, CHECK_IN_START, zone);
  return {
    branchTimeZoneId: zone,
    nights: nightsBetween(inputs.arrivalOn, inputs.departureOn),
    occupiedFrom: from,
    occupiedTo: zonedLocalToInstant(inputs.departureOn, '14:00', zone),
    // Built shape (HO-BRD-001 1.1): staff mode lists units flat, with `isFree`; no per-type `freeUnits`.
    unitTypes: result.unitTypes.map(({ freeUnits: _free, ...type }) => {
      void _free;
      return type;
    }),
    ...(inputs.mode === 'Staff'
      ? {
          units: inputs.units
            .filter((u) => result.unitTypes.some((t) => t.unitType === u.unitType))
            .map((u) => ({
              unitId: u.id,
              code: u.code,
              name: u.name,
              unitType: u.unitType,
              capacityCats: u.capacityCats,
              block: u.block,
              isFree: !!result.unitTypes
                .find((t) => t.unitType === u.unitType)
                ?.freeUnits?.some((f) => f.unitId === u.id),
            })),
        }
      : {}),
    alternatives: result.total === 0 ? findAlternatives(inputs) : [],
    waitlistAvailable: result.total === 0,
  };
}

export function quoteFor(
  branchId: string,
  unitType: BoardingUnitType,
  arrivalOn: string,
  departureOn: string,
  catCount: number,
): StayQuote | null {
  const card = cardValuesFor(unitType);
  if (!card) return null;
  return quoteStay(
    unitType,
    nightsBetween(arrivalOn, departureOn),
    catCount,
    card,
    BRANCH_INFO[branchId]?.discountPercent ?? 0,
  );
}

// ---- enquiry ----

function newCat(info: CatInfo, gate: GateResult): StoredCat {
  return {
    catId: info.catId,
    catName: info.name,
    isHighRisk: info.isHighRisk,
    handlingNote: info.handlingNote,
    careProfile: null,
    gateStatus: gate.status,
    gateReasons: gate.reasons,
    gateOverriddenBy: null,
    gateOverrideReason: null,
    suspectedInfectious: false,
    awayFromUnit: false,
    awayReason: null,
    checkIn: null,
    checkOut: null,
  };
}

async function buildCats(
  request: EnquiryRequest,
  previous: StoredCat[],
): Promise<Result<StoredCat[]>> {
  const cats: StoredCat[] = [];
  for (const catId of request.catIds) {
    const info = await lookupCat(catId);
    if (!info) return bad('CatNotBoardable', 'CatIds', 'One of these cats could not be found.');
    const gate = await evaluateGate(catId, request.departureOn);
    const before = previous.find((c) => c.catId === catId);
    const fresh = newCat(info, gate);
    cats.push(
      before
        ? {
            ...fresh,
            careProfile: before.careProfile,
            ...(before.gateStatus === 'Overridden'
              ? {
                  gateStatus: 'Overridden' as const,
                  gateOverriddenBy: before.gateOverriddenBy,
                  gateOverrideReason: before.gateOverrideReason,
                }
              : {}),
          }
        : fresh,
    );
  }
  return ok(cats);
}

export async function createEnquiryAction(
  request: EnquiryRequest,
  who: { customerId: string; customerName: string; channel: 'Online' | 'Reception' },
  existing?: StoredStay,
): Promise<Result<StoredStay>> {
  const mode = who.channel === 'Online' ? 'Online' : 'Staff';
  const dateProblem = validateDates(request.branchId, request.arrivalOn, request.departureOn, mode);
  if (dateProblem) return dateProblem;
  if (!Array.isArray(request.catIds) || request.catIds.length === 0) {
    return bad('CatNotBoardable', 'CatIds', 'Choose at least one cat.');
  }
  if (new Set(request.catIds).size !== request.catIds.length) {
    return bad('DuplicateCatInStay', 'CatIds', 'Each cat can only be added once.');
  }
  const capacity = TYPE_CAPACITY[request.unitType];
  if (request.catIds.length > capacity) {
    return bad(
      'UnitCapacityExceeded',
      'CatIds',
      `A ${request.unitType} unit holds up to ${capacity} cat${capacity === 1 ? '' : 's'}. You have chosen ${request.catIds.length}.`,
    );
  }
  if (!cardValuesFor(request.unitType)) {
    return bad(
      'UnitTypeNotPriced',
      'UnitType',
      'This type of unit has no rate card yet, so it can’t be booked.',
    );
  }
  const cats = await buildCats(request, existing?.cats ?? []);
  if (!cats.ok) return cats;
  const quote = quoteFor(
    request.branchId,
    request.unitType,
    request.arrivalOn,
    request.departureOn,
    cats.value.length,
  );

  if (existing) {
    existing.arrivalOn = request.arrivalOn;
    existing.departureOn = request.departureOn;
    existing.requestedUnitType = request.unitType;
    existing.cats = cats.value;
    existing.quote = quote;
    existing.version += 1;
    persist();
    return ok(existing);
  }
  const id = `e9000000-0000-0000-0000-${String(getDb().seq + 1).padStart(12, '0')}`;
  getDb().seq += 1;
  const stay: StoredStay = {
    id,
    branchId: request.branchId,
    customerId: who.customerId,
    customerName: who.customerName,
    customerPhone: '01711000000',
    channel: who.channel,
    status: 'Enquiry',
    arrivalOn: request.arrivalOn,
    departureOn: request.departureOn,
    requestedUnitType: request.unitType,
    unitId: null,
    cats: cats.value,
    quote,
    depositRequired: 0,
    depositState: 'NotRequired',
    payBy: null,
    version: 1,
    statusHistory: [
      { from: 'None', to: 'Enquiry', changedOn: nowIso(), changedBy: who.customerName },
    ],
    allocations: [],
    isolationEntries: [],
    referrals: [],
    approvals: [],
    charges: [],
    invoices: [],
    attention: { needsAttention: false, reason: null, note: null },
    depositForfeited: null,
    checkedInOn: null,
    checkedOutOn: null,
    createdOn: nowIso(),
  };
  getDb().stays[id] = stay;
  persist();
  return ok(stay);
}

export function setCareProfileAction(
  stay: StoredStay,
  catId: string,
  profile: CareProfile,
): Result<true> {
  const cat = stay.cats.find((c) => c.catId === catId);
  if (!cat) return bad('InvalidCareProfile', 'CatId', 'That cat is not part of this stay.');
  if (!['Enquiry', 'Reserved', 'Confirmed', 'CheckedIn'].includes(stay.status)) {
    return bad(
      'InvalidStayTransition',
      'Status',
      'A care profile can’t be changed for this stay any more.',
    );
  }
  if (!profile.feedings || profile.feedings.length === 0 || profile.feedings.length > 6) {
    return bad('InvalidCareProfile', 'Feedings', 'Add between 1 and 6 feedings.');
  }
  if (
    !profile.vetSpendingLimit ||
    Number(profile.vetSpendingLimit.amount) < 0 ||
    Number.isNaN(Number(profile.vetSpendingLimit.amount))
  ) {
    return bad(
      'InvalidCareProfile',
      'VetSpendingLimit',
      'Enter a spending limit — 0 if nothing may be spent without asking.',
    );
  }
  if (!profile.emergencyContact?.name || !profile.emergencyContact?.phone) {
    return bad(
      'InvalidCareProfile',
      'EmergencyContact',
      'Enter an emergency contact with a phone number.',
    );
  }
  for (const med of profile.medications ?? []) {
    if (!med.times || med.times.length === 0) {
      return bad('InvalidCareProfile', 'Medications', `Add a time for ${med.name}.`);
    }
  }
  cat.careProfile = profile;
  stay.version += 1;
  persist();
  return ok(true);
}

// ---- reserve ----

export function overlappingStayOfCat(stay: StoredStay, catId: string): StoredStay | undefined {
  return Object.values(getDb().stays).find(
    (s) =>
      s.id !== stay.id &&
      ['Reserved', 'Confirmed', 'CheckedIn'].includes(s.status) &&
      s.cats.some((c) => c.catId === catId) &&
      s.arrivalOn < stay.departureOn &&
      stay.arrivalOn < s.departureOn,
  );
}

export async function reserveAction(stay: StoredStay): Promise<Result<StoredStay>> {
  if (stay.status !== 'Enquiry') {
    return bad('InvalidStayTransition', 'Status', 'Only a new enquiry can be reserved.');
  }
  // The gate is re-evaluated at reserve (§8.6): a vaccination recorded since the enquiry passes now.
  for (const cat of stay.cats) {
    if (cat.gateStatus === 'Overridden') continue;
    const gate = await evaluateGate(cat.catId, stay.departureOn);
    cat.gateStatus = gate.status;
    cat.gateReasons = gate.reasons;
  }
  const blocked = stay.cats.filter((c) => c.gateStatus === 'Blocked');
  if (blocked.length > 0) {
    persist();
    return bad(
      'VaccinationGateBlocked',
      'Cats',
      `${blocked.map((c) => c.catName).join(' and ')} can’t be booked in until the vaccination is up to date.`,
      400,
      { vaccinationGate: gateView(stay.cats) },
    );
  }
  const incomplete = stay.cats.find((c) => !c.careProfile);
  if (incomplete) {
    return bad(
      'CareProfileIncomplete',
      'Cats',
      `${incomplete.catName}’s care profile isn’t finished yet.`,
      400,
      {
        catId: incomplete.catId,
      },
    );
  }
  for (const cat of stay.cats) {
    const other = overlappingStayOfCat(stay, cat.catId);
    if (other) {
      return bad(
        'CatAlreadyBoarding',
        'CatIds',
        `${cat.catName} already has a stay from ${formatDateOnly(other.arrivalOn)} to ${formatDateOnly(other.departureOn)}.`,
        409,
      );
    }
  }
  const candidate = getDb()
    .units.filter(
      (u) =>
        u.branchId === stay.branchId &&
        u.isActive &&
        u.unitType === stay.requestedUnitType &&
        u.capacityCats >= stay.cats.length,
    )
    .find(
      (u) =>
        !busyIntervals(stay.branchId, stay.id).some(
          (b) => b.unitId === u.id && b.from < stay.departureOn && stay.arrivalOn < b.to,
        ),
    );
  if (!candidate) {
    return bad(
      'UnitNoLongerAvailable',
      'UnitType',
      'That unit has just been taken. Choose again from the list below.',
      409,
    );
  }
  const quote = quoteFor(
    stay.branchId,
    stay.requestedUnitType,
    stay.arrivalOn,
    stay.departureOn,
    stay.cats.length,
  )!;
  stay.status = 'Reserved';
  stay.unitId = candidate.id;
  stay.quote = quote;
  stay.charges = chargesFromQuote(quote, stay.id.slice(-6));
  stay.depositRequired = round2((num(quote.total) * DEPOSIT_PERCENT) / 100);
  stay.depositState = stay.depositRequired > 0 ? 'Awaiting' : 'NotRequired';
  stay.payBy = new Date(serverNow() + RESERVATION_HOLD_HOURS * 3_600_000).toISOString();
  stay.allocations = [
    {
      id: `alloc-${stay.id}`,
      unitName: candidate.name,
      unitType: candidate.unitType,
      from: zonedLocalToInstant(stay.arrivalOn, CHECK_IN_START, zoneOfBranch(stay.branchId)),
      to: zonedLocalToInstant(stay.departureOn, '14:00', zoneOfBranch(stay.branchId)),
      isActive: true,
      reason: null,
    },
  ];
  stay.statusHistory.push({
    from: 'Enquiry',
    to: 'Reserved',
    changedOn: nowIso(),
    changedBy: stay.customerName,
  });
  stay.version += 1;
  persist();
  return ok(stay);
}

// ---- cancel ----

export function cancelAction(
  stay: StoredStay,
  byName: string,
  forfeited: boolean,
): Result<StoredStay> {
  if (!['Enquiry', 'Reserved', 'Confirmed'].includes(stay.status)) {
    return bad(
      'InvalidStayTransition',
      'Status',
      'A stay that has started or ended can’t be cancelled.',
    );
  }
  const from = stay.status;
  stay.status = 'Cancelled';
  stay.depositForfeited = forfeited;
  stay.allocations = stay.allocations.map((a) => ({ ...a, isActive: false }));
  stay.unitId = null;
  stay.statusHistory.push({ from, to: 'Cancelled', changedOn: nowIso(), changedBy: byName });
  stay.version += 1;
  persist();
  return ok(stay);
}

export function unitNameOf(unitId: string | null): string {
  return unitById(unitId)?.name ?? '';
}

export { ids };
