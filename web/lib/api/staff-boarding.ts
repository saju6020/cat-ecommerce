import { apiFetch } from './client';
import {
  careProfileToWire,
  normaliseDaycareVisit,
  normaliseEnquiry,
  normaliseStay,
  normaliseStaySummary,
  normaliseSummary,
} from './my-boarding';
import { formatDueTime } from '@/lib/format/boarding';
import type {
  BoardingAvailability,
  BoardingDashboard,
  BoardingMoney,
  BoardingOccupancy,
  BoardingRateCard,
  BoardingStayStatus,
  BoardingUnit,
  BoardingUnitType,
  BelongingKind,
  CareProfile,
  CareRoutine,
  CheckOutPreview,
  DaycareDayList,
  DaycareRateCard,
  EffectiveDatedHistory,
  EnquiryRequest,
  EnquiryResult,
  MaintenanceBlock,
  OccupancyUnitRow,
  PagedResult,
  ReserveResult,
  RoutineTaskKind,
  SignatureCapture,
  StaffStayDetail,
  StaffStaySummary,
  StaySummary,
  TriagePriority,
  VetHeadroom,
} from './types';

/**
 * `lib/api/staff-boarding.ts` — HO-BRD-002 §8.2: the staff Boarding endpoints (HO-BRD-001 §12.4,
 * branch from `X-Branch-Id`, which `client.ts` sends). Actions are offered only from the server's
 * `allowedTransitions[]` / capability flags (ADR-FE-107); there is no payment call anywhere in this
 * file (ADR-FE-106) — the deposit is recorded in Billing and `confirmStay` only asks Boarding to
 * re-read the ledger.
 */

const BASE = '/api/staff/boarding';
const enc = encodeURIComponent;

function query(params: Record<string, string | number | boolean | undefined | null>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') search.set(key, String(value));
  }
  return search.size > 0 ? `?${search.toString()}` : '';
}

// ---- units, maintenance, rate cards, routine (US-BRD-01, 03, 09) ----

export interface UnitInput {
  code: string;
  name: string;
  unitType: BoardingUnitType;
  capacityCats: number;
  block: string | null;
  sortOrder: number;
}

export async function getBoardingUnits(): Promise<BoardingUnit[]> {
  return apiFetch<BoardingUnit[]>(`${BASE}/units`);
}

export async function createBoardingUnit(input: UnitInput): Promise<{ unitId: string }> {
  return apiFetch<{ unitId: string }>(`${BASE}/units`, { method: 'POST', body: input });
}

export async function updateBoardingUnit(unitId: string, input: UnitInput): Promise<void> {
  // The built PUT takes no `code` (a unit's code is fixed once created - HO-BRD-001 1.1).
  const { code: _code, ...body } = input;
  void _code;
  await apiFetch<void>(`${BASE}/units/${enc(unitId)}`, { method: 'PUT', body });
}

export async function setBoardingUnitActivation(unitId: string, isActive: boolean): Promise<void> {
  await apiFetch<void>(`${BASE}/units/${enc(unitId)}/activation`, {
    method: 'PUT',
    body: { isActive },
  });
}

export async function getMaintenanceBlocks(unitId: string): Promise<MaintenanceBlock[]> {
  return apiFetch<MaintenanceBlock[]>(`${BASE}/units/${enc(unitId)}/maintenance`);
}

export interface MaintenanceInput {
  startsOn: string;
  endsOn: string;
  reason: string;
}

export async function createMaintenanceBlock(
  unitId: string,
  input: MaintenanceInput,
): Promise<{ blockId: string }> {
  return apiFetch<{ blockId: string }>(`${BASE}/units/${enc(unitId)}/maintenance`, {
    method: 'POST',
    body: input,
  });
}

export async function cancelMaintenanceBlock(unitId: string, blockId: string): Promise<void> {
  await apiFetch<void>(`${BASE}/units/${enc(unitId)}/maintenance/${enc(blockId)}`, {
    method: 'DELETE',
  });
}

export interface RateCardInput {
  unitType: BoardingUnitType;
  nightlyRate: BoardingMoney;
  weeklyRate: BoardingMoney;
  monthlyRate: BoardingMoney;
  additionalCatNightlyRate: BoardingMoney;
  taxClassId: string;
  effectiveFrom: string;
}

type WireRateCard = Omit<BoardingRateCard, 'taxClassName'> & {
  taxClassName?: string | null;
  status?: 'Current' | 'Scheduled' | 'Past';
};

/** HO-BRD-001 1.1: the built list is flat, each card carrying `status: Current|Scheduled|Past`. */
export async function getBoardingRateCards(
  unitType: BoardingUnitType,
): Promise<EffectiveDatedHistory<BoardingRateCard>> {
  const body = await apiFetch<WireRateCard[] | EffectiveDatedHistory<BoardingRateCard>>(
    `${BASE}/rate-cards${query({ unitType })}`,
  );
  if (!Array.isArray(body)) return body;
  const cards = body.map((c) => ({ ...c, taxClassName: c.taxClassName ?? null, status: c.status }));
  const sorted = [...cards].sort((x, y) => y.effectiveFrom.localeCompare(x.effectiveFrom));
  return {
    current: sorted.find((c) => c.status === 'Current') ?? null,
    scheduled: sorted.filter((c) => c.status === 'Scheduled'),
    past: sorted.filter((c) => c.status === 'Past'),
  };
}

/** The built POST takes the four rates as plain "1000.00" strings (HO-BRD-001 1.1). */
export async function scheduleBoardingRateCard(input: RateCardInput): Promise<void> {
  await apiFetch<void>(`${BASE}/rate-cards`, {
    method: 'POST',
    body: {
      unitType: input.unitType,
      nightlyRate: input.nightlyRate.amount,
      weeklyRate: input.weeklyRate.amount,
      monthlyRate: input.monthlyRate.amount,
      additionalCatNightlyRate: input.additionalCatNightlyRate.amount,
      taxClassId: input.taxClassId,
      effectiveFrom: input.effectiveFrom,
    },
  });
}

export interface DaycareRateCardInput {
  hourlyRate: BoardingMoney;
  dayRate: BoardingMoney;
  taxClassId: string;
  effectiveFrom: string;
}

/** HO-BRD-001 1.1: the built list is flat, each card carrying `status: Current|Scheduled|Past`. */
export async function getDaycareRateCards(): Promise<EffectiveDatedHistory<DaycareRateCard>> {
  const body = await apiFetch<
    | Array<Partial<DaycareRateCard> & { id: string; effectiveFrom: string; status?: string }>
    | EffectiveDatedHistory<DaycareRateCard>
  >(`${BASE}/daycare-rate-cards`);
  if (!Array.isArray(body)) return body;
  const cards = body
    .map((c) => ({ ...c, taxClassName: c.taxClassName ?? null, setBy: c.setBy ?? null }))
    .sort((x, y) => y.effectiveFrom.localeCompare(x.effectiveFrom)) as Array<
    DaycareRateCard & { status?: string }
  >;
  return {
    current: cards.find((c) => c.status === 'Current') ?? null,
    scheduled: cards.filter((c) => c.status === 'Scheduled'),
    past: cards.filter((c) => c.status === 'Past'),
  };
}

/** The built POST takes the two rates as plain "150.00" strings (HO-BRD-001 1.1). */
export async function scheduleDaycareRateCard(input: DaycareRateCardInput): Promise<void> {
  await apiFetch<void>(`${BASE}/daycare-rate-cards`, {
    method: 'POST',
    body: {
      hourlyRate: input.hourlyRate.amount,
      dayRate: input.dayRate.amount,
      taxClassId: input.taxClassId,
      effectiveFrom: input.effectiveFrom,
    },
  });
}

/** The built routine's times are `{ kind, time }` (HO-BRD-001 1.1); the screens use `localTime`. */
export async function getCareRoutine(): Promise<CareRoutine> {
  const body = await apiFetch<{
    isDefault: boolean;
    times: Array<{ kind: RoutineTaskKind; time?: string; localTime?: string }>;
  }>(`${BASE}/care-routine`);
  return {
    isDefault: body.isDefault,
    times: body.times.map((t) => ({ kind: t.kind, localTime: t.time ?? t.localTime ?? '' })),
  };
}

export async function setCareRoutine(
  times: Array<{ kind: RoutineTaskKind; localTime: string }>,
): Promise<void> {
  await apiFetch<void>(`${BASE}/care-routine`, {
    method: 'PUT',
    body: { times: times.map((t) => ({ kind: t.kind, time: t.localTime })) },
  });
}

// ---- availability and occupancy (US-BRD-02, 15) ----

export interface StaffAvailabilityParams {
  arrivalOn: string;
  departureOn: string;
  catCount: number;
  requiresIsolation?: boolean;
  excludeStayId?: string;
}

/** Staff mode: the built answer lists units flat (`units[{unitId,code,unitType,...,isFree}]`); the
 *  screens read the free ones of each type as `freeUnits`. */
export async function getStaffAvailability(
  params: StaffAvailabilityParams,
): Promise<BoardingAvailability> {
  const body = await apiFetch<
    BoardingAvailability & {
      units?: Array<{
        unitId: string;
        code: string;
        name?: string;
        unitType: BoardingUnitType;
        block?: string | null;
        isFree: boolean;
      }>;
    }
  >(`${BASE}/availability${query({ ...params })}`);
  const flat = body.units;
  if (!flat) return body;
  return {
    ...body,
    unitTypes: body.unitTypes.map((t) => ({
      ...t,
      freeUnits:
        t.freeUnits ??
        flat
          .filter((u) => u.unitType === t.unitType && u.isFree)
          .map((u) => ({
            unitId: u.unitId,
            code: u.code,
            name: u.name ?? u.code,
            block: u.block ?? null,
          })),
    })),
  };
}

interface WireOccupancy {
  from: string;
  to: string;
  branchTimeZoneId?: string;
  days?: string[];
  units: Array<
    Omit<OccupancyUnitRow, 'stays' | 'maintenance' | 'turnaroundDays'> & {
      stays?: OccupancyUnitRow['stays'];
      maintenance?: OccupancyUnitRow['maintenance'];
      turnaroundDays?: string[];
      cells?: Array<{
        date: string;
        state: string;
        stayId: string | null;
        label: string | null;
        catNames?: string[] | null;
        inTurnaround?: boolean;
      }>;
    }
  >;
}

/** HO-BRD-001 1.1 / the built read: each unit carries one `cell` per day (`Free` / `Occupied` /
 *  `Maintenance`, a stay id, a label). Consecutive cells of one stay become a bar; the cat names and a
 *  turnaround marker are not in the built answer (HO-BRD-002 1 Amended). */
export async function getBoardingOccupancy(from: string, to: string): Promise<BoardingOccupancy> {
  const body = await apiFetch<WireOccupancy>(`${BASE}/occupancy${query({ from, to })}`);
  const days = body.days ?? body.units[0]?.cells?.map((c) => c.date) ?? [];
  return {
    from: body.from,
    to: body.to,
    branchTimeZoneId: body.branchTimeZoneId ?? 'Asia/Dhaka',
    days,
    units: body.units.map((u) => {
      if (!u.cells) {
        return {
          ...u,
          stays: u.stays ?? [],
          maintenance: u.maintenance ?? [],
          turnaroundDays: u.turnaroundDays ?? [],
        };
      }
      const stays: OccupancyUnitRow['stays'] = [];
      const maintenance: OccupancyUnitRow['maintenance'] = [];
      const turnaroundDays: string[] = [];
      for (const cell of u.cells) {
        if (cell.inTurnaround) turnaroundDays.push(cell.date);
        if (cell.state === 'Occupied' && cell.stayId) {
          const last = stays.at(-1);
          if (last && last.stayId === cell.stayId) last.toOn = cell.date;
          else
            stays.push({
              stayId: cell.stayId,
              catNames: cell.catNames ?? [],
              status: (cell.label ?? 'Reserved') as OccupancyUnitRow['stays'][number]['status'],
              fromOn: cell.date,
              toOn: cell.date,
              awayInSurgery: false,
            });
        } else if (cell.state === 'Maintenance') {
          const last = maintenance.at(-1);
          if (
            last &&
            last.reason === (cell.label ?? '') &&
            last.toOn === days[days.indexOf(cell.date) - 1]
          )
            last.toOn = cell.date;
          else
            maintenance.push({
              blockId: `${u.unitId}-${cell.date}`,
              fromOn: cell.date,
              toOn: cell.date,
              reason: cell.label ?? 'Maintenance',
            });
        }
      }
      const { cells: _cells, ...rest } = u;
      void _cells;
      return { ...rest, stays, maintenance, turnaroundDays };
    }),
  };
}

// ---- booking on behalf of a customer ----

export async function createStaffEnquiry(request: EnquiryRequest): Promise<EnquiryResult> {
  return normaliseEnquiry(
    await apiFetch<EnquiryResult>(`${BASE}/enquiries`, { method: 'POST', body: request }),
  );
}

export async function changeStaffEnquiry(
  stayId: string,
  request: Omit<EnquiryRequest, 'customerId'>,
): Promise<EnquiryResult> {
  return normaliseEnquiry(
    await apiFetch<EnquiryResult>(`${BASE}/stays/${enc(stayId)}/enquiry`, {
      method: 'PUT',
      body: request,
    }),
  );
}

export async function saveStaffCareProfile(
  stayId: string,
  catId: string,
  profile: CareProfile,
  vetAuthorityChangeReason?: string,
): Promise<void> {
  // HO-BRD-001 fix round (fc6ef889): staff must give a reason when they change the vet authorisation or the
  // spending limit on a stay that is no longer an Enquiry (`VetAuthorityReasonRequired`).
  await apiFetch<void>(`${BASE}/stays/${enc(stayId)}/cats/${enc(catId)}/care-profile`, {
    method: 'PUT',
    body: {
      ...careProfileToWire(profile),
      ...(vetAuthorityChangeReason ? { vetAuthorityChangeReason } : {}),
    },
  });
}

export async function overrideVaccinationGate(
  stayId: string,
  catId: string,
  reason: string,
): Promise<void> {
  await apiFetch<void>(`${BASE}/stays/${enc(stayId)}/cats/${enc(catId)}/vaccination-override`, {
    method: 'POST',
    body: { reason },
  });
}

export async function reserveStaffStay(stayId: string): Promise<ReserveResult> {
  const result = await apiFetch<ReserveResult>(`${BASE}/stays/${enc(stayId)}/reserve`, {
    method: 'POST',
  });
  return { ...result, stay: normaliseStay(result.stay) };
}

export interface ConfirmStayResult {
  status: BoardingStayStatus;
  depositState: string;
  outstanding: BoardingMoney;
}

/** C9 — asks Boarding to re-read Billing's ledger. It records no payment (ADR-FE-106). */
export async function confirmStay(stayId: string): Promise<ConfirmStayResult> {
  return apiFetch<ConfirmStayResult>(`${BASE}/stays/${enc(stayId)}/confirm`, { method: 'POST' });
}

export async function cancelStaffStay(stayId: string, note: string): Promise<void> {
  await apiFetch<void>(`${BASE}/stays/${enc(stayId)}/cancel`, { method: 'POST', body: { note } });
}

export async function recordStayNoShow(stayId: string): Promise<void> {
  await apiFetch<void>(`${BASE}/stays/${enc(stayId)}/no-show`, { method: 'POST' });
}

export async function clearStayAttention(stayId: string, note: string): Promise<void> {
  await apiFetch<void>(`${BASE}/stays/${enc(stayId)}/attention/clear`, {
    method: 'POST',
    body: { note },
  });
}

// ---- stays ----

export interface StaffStaysParams {
  status?: BoardingStayStatus;
  from?: string;
  to?: string;
  customerId?: string;
  catId?: string;
  pageNumber?: number;
  pageSize?: number;
}

export async function getStaffStays(
  params: StaffStaysParams = {},
): Promise<PagedResult<StaffStaySummary>> {
  const page = await apiFetch<PagedResult<unknown>>(`${BASE}/stays${query({ ...params })}`);
  return { ...page, items: page.items.map((i) => normaliseSummary<StaffStaySummary>(i)) };
}

export async function getStaffStay(stayId: string): Promise<StaffStayDetail> {
  return normaliseStay<StaffStayDetail>(await apiFetch<unknown>(`${BASE}/stays/${enc(stayId)}`));
}

export async function getStaffStaySummary(stayId: string): Promise<StaySummary> {
  return normaliseStaySummary(await apiFetch<unknown>(`${BASE}/stays/${enc(stayId)}/summary`));
}

// ---- check-in / check-out (US-BRD-08, 16) ----

export interface CheckInBelongingInput {
  kind: BelongingKind;
  description: string;
  fileIds: string[];
}

export interface CheckInCatInput {
  catId: string;
  identityConfirmed: boolean;
  vaccinationProofVerified: boolean;
  weightKg: number;
  arrivalHealthObservation: string;
  noBelongings: boolean;
  belongings: CheckInBelongingInput[];
  ownerSignature: SignatureCapture;
}

export interface CheckOutCatInput {
  catId: string;
  weightKg: number;
  belongings: Array<{ belongingId: string; returned: boolean; note: string | null }>;
  ownerSignature: SignatureCapture;
}

/** The built check-in body (HO-BRD-001 1.1): `signature` (not `ownerSignature`), strokes as raw JSON, and
 *  belongings carrying `fileIds` -- the ids of photos uploaded through HO-FIL-002's uploader. */
export async function checkInStay(
  stayId: string,
  cats: CheckInCatInput[],
): Promise<{ stay: StaffStayDetail }> {
  const wireCats = cats.map(({ ownerSignature, belongings, ...rest }) => ({
    ...rest,
    belongings: belongings.map((b) => ({
      kind: b.kind,
      description: b.description,
      fileIds: b.fileIds,
    })),
    signature: ownerSignature,
  }));
  const result = await apiFetch<{ stay: unknown }>(`${BASE}/stays/${enc(stayId)}/check-in`, {
    method: 'POST',
    body: { cats: wireCats },
  });
  return { stay: normaliseStay<StaffStayDetail>(result.stay) };
}

/** The early check-out preview (HO-BRD-001 1.1, committed): writes nothing, runs the check-out's own code. */
export async function getCheckOutPreview(stayId: string): Promise<CheckOutPreview> {
  return apiFetch<CheckOutPreview>(`${BASE}/stays/${enc(stayId)}/check-out-preview`);
}

export async function checkOutStay(
  stayId: string,
  cats: CheckOutCatInput[],
): Promise<{ stay: StaffStayDetail }> {
  const wireCats = cats.map(({ ownerSignature, ...rest }) => ({
    ...rest,
    signature: ownerSignature,
  }));
  const result = await apiFetch<{ stay: unknown }>(`${BASE}/stays/${enc(stayId)}/check-out`, {
    method: 'POST',
    body: { cats: wireCats },
  });
  return { stay: normaliseStay<StaffStayDetail>(result.stay) };
}

// ---- extend / relocate (US-BRD-15) ----

export async function extendStay(
  stayId: string,
  newDepartureOn: string,
  expectedVersion: number,
): Promise<StaffStayDetail> {
  return normaliseStay<StaffStayDetail>(
    await apiFetch<unknown>(`${BASE}/stays/${enc(stayId)}/extend`, {
      method: 'POST',
      body: { newDepartureOn, expectedVersion },
    }),
  );
}

export interface RelocateInput {
  targetUnitId: string;
  effectiveFromLocalDate: string;
  reason: string;
  newDepartureOn?: string;
  expectedVersion: number;
}

export async function relocateStay(stayId: string, input: RelocateInput): Promise<StaffStayDetail> {
  return normaliseStay<StaffStayDetail>(
    await apiFetch<unknown>(`${BASE}/stays/${enc(stayId)}/relocate`, {
      method: 'POST',
      body: input,
    }),
  );
}

// ---- isolation (US-BRD-18) ----

export async function flagSuspectedInfectious(
  stayId: string,
  catId: string,
  note: string,
  targetIsolationUnitId?: string,
): Promise<void> {
  await apiFetch<void>(`${BASE}/stays/${enc(stayId)}/cats/${enc(catId)}/suspected-infectious`, {
    method: 'POST',
    body: { note, targetIsolationUnitId },
  });
}

export async function clearSuspectedInfectious(
  stayId: string,
  catId: string,
  note: string,
): Promise<void> {
  await apiFetch<void>(`${BASE}/stays/${enc(stayId)}/cats/${enc(catId)}/suspected-infectious`, {
    method: 'DELETE',
    body: { note },
  });
}

export async function recordIsolationEntry(stayId: string, purpose: string): Promise<void> {
  await apiFetch<void>(`${BASE}/stays/${enc(stayId)}/isolation-entries`, {
    method: 'POST',
    body: { purpose },
  });
}

export async function recordIsolationExit(stayId: string, entryId: string): Promise<void> {
  await apiFetch<void>(`${BASE}/stays/${enc(stayId)}/isolation-entries/${enc(entryId)}/exit`, {
    method: 'POST',
  });
}

// ---- referral and vet approvals (US-BRD-14) ----

export async function referToClinic(
  stayId: string,
  input: { catId: string; reason: string; urgency: TriagePriority },
): Promise<{ referralId: string }> {
  return apiFetch<{ referralId: string }>(`${BASE}/stays/${enc(stayId)}/referrals`, {
    method: 'POST',
    body: input,
  });
}

/** `POST …/referrals/{referralId}/return` (added by HO-BRD-001): the cat is back from the clinic. */
export async function returnFromReferral(stayId: string, referralId: string): Promise<void> {
  await apiFetch<void>(`${BASE}/stays/${enc(stayId)}/referrals/${enc(referralId)}/return`, {
    method: 'POST',
  });
}

export async function getVetHeadroom(stayId: string, catId: string): Promise<VetHeadroom> {
  const w = await apiFetch<Partial<VetHeadroom>>(
    `${BASE}/stays/${enc(stayId)}/cats/${enc(catId)}/vet-headroom`,
  );
  return { approvedIncrease: { amount: '0.00', currencyCode: 'BDT' }, ...w } as VetHeadroom;
}

export async function requestVetApproval(
  stayId: string,
  input: { catId: string; referralId?: string; amount: BoardingMoney; description: string },
): Promise<{ approvalId: string }> {
  return apiFetch<{ approvalId: string }>(`${BASE}/stays/${enc(stayId)}/vet-approvals`, {
    method: 'POST',
    body: { ...input, amount: input.amount.amount },
  });
}

export async function recordStaffApprovalAnswer(
  stayId: string,
  approvalId: string,
  answer: 'Approve' | 'Decline',
  note: string,
): Promise<void> {
  await apiFetch<void>(
    `${BASE}/stays/${enc(stayId)}/vet-approvals/${enc(approvalId)}/staff-answer`,
    { method: 'POST', body: { approve: answer === 'Approve', note } },
  );
}

// ---- dashboard and daycare ----

type DashWire = Record<string, unknown>;
const list = (value: unknown): DashWire[] => (value as DashWire[] | undefined) ?? [];

/** The built dashboard (HO-BRD-001 1.1): lean rows with `unitCode`, `dueOn` instants and money-less stay rows.
 *  Customer name/phone, risk flags, minutes late and the last portions are not on it (HO-BRD-002 1 Amended). */
export function normaliseDashboard(input: unknown, zone = 'Asia/Dhaka'): BoardingDashboard {
  const w = input as DashWire;
  const occupancy = (w.occupancy ?? {}) as DashWire;
  const stayRow = (r: DashWire) => ({
    stayId: String(r.stayId),
    catNames: (r.catNames as string[] | undefined) ?? [],
    anyHighRisk: Boolean(r.anyHighRisk),
    unitName: (r.unitName ?? r.unitCode ?? null) as string | null,
    customerName: (r.customerName as string | null | undefined) ?? null,
    depositState: r.depositState,
    balanceDue: (r.balanceDue as BoardingMoney | null | undefined) ?? null,
    status: r.status,
  });
  const taskRow = (r: DashWire) => ({
    taskId: String(r.taskId),
    stayId: String(r.stayId),
    catName: String(r.catName ?? ''),
    unitName: String(r.unitName ?? r.unitCode ?? ''),
    title: String(r.title ?? ''),
    dueLocalTime: String(r.dueLocalTime ?? (r.dueOn ? formatDueTime(String(r.dueOn), zone) : '')),
    minutesLate: (r.minutesLate as number | null | undefined) ?? null,
  });
  const balance = (r: DashWire) => ({
    stayId: String(r.stayId),
    customerName: (r.customerName as string | null | undefined) ?? null,
    catNames: (r.catNames as string[] | undefined) ?? [],
    kind: r.kind ?? (/deposit/i.test(String(r.reason)) ? 'DepositAwaiting' : 'BalanceOutstanding'),
    amount: r.amount as BoardingMoney,
    depositDueBy: (r.depositDueBy as string | null | undefined) ?? null,
    reason: String(r.reason ?? ''),
  });
  return {
    generatedOn: String(w.generatedOn),
    branchTimeZoneId: String(w.branchTimeZoneId ?? zone),
    occupancy: {
      unitsOccupied: Number(occupancy.unitsOccupied ?? 0),
      unitsActive: Number(occupancy.unitsActive ?? 0),
      catsInHouse: Number(occupancy.catsInHouse ?? 0),
      byType: list(occupancy.byType).map((t) => ({
        unitType: t.unitType,
        occupied: Number(t.occupied ?? t.unitsOccupied ?? 0),
        active: Number(t.active ?? t.unitsActive ?? 0),
      })),
    },
    arrivals: list(w.arrivals).map(stayRow),
    departures: list(w.departures).map(stayRow),
    notEating: list(w.notEating).map((r) => ({
      stayId: String(r.stayId),
      catId: String(r.catId),
      catName: String(r.catName),
      unitName: String(r.unitName ?? r.unitCode ?? ''),
      lastEntries:
        (r.lastEntries as Array<{ recordedOn: string; portionEaten: string }> | undefined) ?? [],
    })),
    overdueTasks: list(w.overdueTasks).map(taskRow),
    unassignedTasks: list(w.unassignedTasks).map(taskRow),
    openIncidents: list(w.openIncidents).map((r) => ({
      incidentId: String(r.incidentId),
      stayId: String(r.stayId),
      catName: String(r.catName),
      kind: r.kind,
      severity: r.severity,
      acknowledged: Boolean(r.acknowledged),
      escalated: Boolean(r.escalated),
      reportedOn: String(r.reportedOn),
    })),
    callOwner: list(w.callOwner).map((r) => ({
      incidentId: String(r.incidentId),
      stayId: String(r.stayId),
      catName: String(r.catName),
      customerName: (r.customerName as string | null | undefined) ?? null,
      customerPhone: (r.customerPhone as string | null | undefined) ?? null,
      reportedOn: String(r.reportedOn),
    })),
    unpaidBalances: list(w.unpaidBalances).map(balance),
    refundDue: list(w.refundDue).map(balance),
    attention: list(w.attention).map((r) => ({
      stayId: String(r.stayId),
      catNames: (r.catNames as string[] | undefined) ?? [],
      reason: r.reason,
      note: (r.note as string | null | undefined) ?? null,
    })),
  } as unknown as BoardingDashboard;
}

export async function getBoardingDashboard(): Promise<BoardingDashboard> {
  return normaliseDashboard(await apiFetch<unknown>(`${BASE}/dashboard`));
}

/** The built day list: `{ date, capacity, slotsTaken, offered, visits[] }`. */
export async function getDaycareDayList(date: string): Promise<DaycareDayList> {
  const w = await apiFetch<{
    date: string;
    capacity: number;
    offered: boolean;
    branchTimeZoneId?: string;
    visits: unknown[];
  }>(`/api/staff/daycare${query({ date })}`);
  const zone = w.branchTimeZoneId ?? 'Asia/Dhaka';
  return {
    date: w.date,
    branchTimeZoneId: zone,
    offered: w.offered,
    capacity: w.capacity,
    visits: w.visits.map((v) => normaliseDaycareVisit(v, zone)),
  };
}

export type DaycareAction = 'check-in' | 'check-out' | 'cancel' | 'no-show';

export async function actOnDaycareVisit(visitId: string, action: DaycareAction): Promise<void> {
  await apiFetch<void>(`/api/staff/daycare/${enc(visitId)}/${action}`, { method: 'POST' });
}
