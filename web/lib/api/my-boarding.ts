import { apiFetch } from './client';
import { resolveFileUrl } from './files';
import type {
  BoardingUnitType,
  BoardingMoney,
  CareProfile,
  DaycareBasis,
  DaycareVisit,
  EnquiryRequest,
  EnquiryResult,
  IncidentDetail,
  MyStayDetail,
  MyStaySummary,
  PagedResult,
  ReserveResult,
  StayDiary,
  VaccinationGate,
  StaySummary,
  VetApprovalDetail,
  WaitlistEntryDto,
} from './types';

/**
 * `lib/api/my-boarding.ts` — HO-BRD-002 §8.2: a customer's own enquiry, care profiles, reserve,
 * cancel, stays, diary, incidents, approvals, summary, waitlist and daycare (HO-BRD-001 §12.4).
 * Someone else's stay, incident or approval is a `404`, never inferred data (HO-BRD-002 §19 gate 2).
 * Nothing here takes a payment (ADR-FE-106): reserve returns the amount to pay offline, no more.
 */

const BASE = '/api/customers/me/boarding';
const enc = encodeURIComponent;

// ---------------------------------------------------------------------------------------------
// Wire adapters — HO-BRD-001 §1.1 Amended (the BUILT contract wins; HO-BRD-002 §1 Amended lists
// each difference from §12.4). The screens work with one internal shape; these functions are the
// only place that knows the wire's. Each reads the built member first and tolerates the older one.
// ---------------------------------------------------------------------------------------------

type Wire = Record<string, unknown>;
const asWire = (value: unknown): Wire => (value ?? {}) as Wire;

/** The care profile on the wire: feedings carry `time`, the spending limit is a plain `"0.00"`. */
export function careProfileToWire(profile: CareProfile): Wire {
  return {
    ...profile,
    feedings: profile.feedings.map(({ localTime, ...rest }) => ({ time: localTime, ...rest })),
    vetSpendingLimit: profile.vetSpendingLimit.amount,
  };
}

export function careProfileFromWire(raw: unknown, currencyCode: string): CareProfile | null {
  if (!raw) return null;
  const w = asWire(raw);
  const feedings = ((w.feedings as Wire[] | undefined) ?? []).map((f) => ({
    localTime: String(f.time ?? f.localTime ?? ''),
    portion: String(f.portion ?? ''),
    foodSource: f.foodSource as CareProfile['feedings'][number]['foodSource'],
    foodDescription: String(f.foodDescription ?? ''),
    houseFoodSkuId: (f.houseFoodSkuId as string | null | undefined) ?? null,
  }));
  const limit = w.vetSpendingLimit;
  return {
    feedings,
    medications: (w.medications as CareProfile['medications'] | undefined) ?? [],
    litterType: w.litterType as CareProfile['litterType'],
    litterNote: (w.litterNote as string | null | undefined) ?? null,
    vetVisitAuthorised: Boolean(w.vetVisitAuthorised),
    vetSpendingLimit:
      typeof limit === 'object' && limit !== null
        ? (limit as BoardingMoney)
        : { amount: String(limit ?? '0.00'), currencyCode },
    emergencyContact: w.emergencyContact as CareProfile['emergencyContact'],
    notes: (w.notes as string | null | undefined) ?? null,
  };
}

/** A stay view (customer or staff) from the built shape: `unit`, `deposit.dueBy/received`, per-cat
 *  `gate`, `needsAttention`/`attentionReason`; collections the server has not sent become empty. */
const CHARGE_KIND_BY_REF: Array<[RegExp, string]> = [
  [/AdditionalCat/i, 'AdditionalCat'],
  [/Discount/i, 'LongStayDiscount'],
  [/EarlyCheck|Credit/i, 'EarlyCheckoutCredit'],
  [/Refund|DepositDue/i, 'DepositRefundDue'],
  [/Adjust/i, 'Adjustment'],
];

/** `allowedTransitions` on the wire: `NoShow` is the internal `RecordNoShow`; `Lapse`/`ExpireEnquiry` are
 *  system moves (not buttons); Extend and Relocate are offered while the stay can be checked in or out. */
export function mapTransitions(raw: unknown): string[] {
  const wire = (raw as string[] | undefined) ?? [];
  const out = new Set<string>();
  for (const t of wire) {
    if (t === 'Lapse' || t === 'ExpireEnquiry') continue;
    out.add(t === 'NoShow' ? 'RecordNoShow' : t);
  }
  if (out.has('CheckIn') || out.has('CheckOut')) {
    out.add('Extend');
    out.add('Relocate');
  }
  return [...out];
}

/** An instant of `0001-01-01` is the server's "unset": fall back to the other date. */
const realInstant = (value: unknown, fallback: unknown): string | null => {
  const text = typeof value === 'string' ? value : '';
  return text && !text.startsWith('0001') ? text : ((fallback as string | undefined) ?? null);
};

export function normaliseStay<T>(raw: unknown): T {
  const w = asWire(raw);
  const deposit = asWire(w.deposit);
  const unit = w.unit as Wire | null | undefined;
  const currency =
    ((w.balanceDue as BoardingMoney | undefined) ?? (deposit.required as BoardingMoney | undefined))
      ?.currencyCode ?? 'BDT';
  const zero = { amount: '0.00', currencyCode: currency };
  const cats: Wire[] = ((w.cats as Wire[] | undefined) ?? []).map((c) => {
    const gate = asWire(c.gate);
    const profile = careProfileFromWire(c.careProfile, currency);
    const checkIn = asWire(c.checkIn);
    const belongings = ((checkIn.belongings as Wire[] | undefined) ?? []).map((b) => ({
      id: String(b.id),
      kind: b.kind,
      description: String(b.description ?? ''),
      returned: (b.returned as boolean | null | undefined) ?? null,
      returnNote: (b.returnNote as string | null | undefined) ?? null,
    }));
    return {
      ...c,
      careProfile: profile,
      careProfileComplete: (c.careProfileComplete as boolean | undefined) ?? profile !== null,
      // the built gate has `overridden` (and `overrideReason`), but no `overriddenBy`
      gateStatus: c.gateStatus ?? (gate.overridden ? 'Overridden' : gate.status) ?? 'Passed',
      gateReasons: c.gateReasons ?? gate.reasons ?? [],
      gateOverriddenBy: c.gateOverriddenBy ?? gate.overriddenBy ?? null,
      gateOverrideReason: c.gateOverrideReason ?? gate.overrideReason ?? null,
      gateOverriddenOn: c.gateOverriddenOn ?? gate.overriddenOn ?? null,
      suspectedInfectious: Boolean(c.suspectedInfectious ?? c.isSuspectedInfectious),
      checkedInWeightKg:
        c.checkedInWeightKg ?? (checkIn.weightKg != null ? String(checkIn.weightKg) : null),
      awayFromUnit: Boolean(c.awayFromUnit ?? c.awayReason),
      awayReason: (c.awayReason as string | null | undefined) ?? null,
      belongings: c.belongings ?? belongings,
    };
  });
  const catName = (catId: unknown) => cats.find((c) => c.catId === catId)?.catName ?? 'Your cat';
  const charges = ((w.charges as Wire[] | undefined) ?? [])
    .filter((c) => !c.voided)
    .map((c) => {
      const lineRef = String(c.lineRef ?? '');
      return {
        id: String(c.chargeId ?? c.id),
        kind: c.kind ?? CHARGE_KIND_BY_REF.find(([re]) => re.test(lineRef))?.[1] ?? 'RateUnit',
        description: String(c.description ?? ''),
        amount: (c.amount ?? c.net ?? zero) as BoardingMoney,
        fromNight: (c.fromNight as number | null | undefined) ?? null,
        toNight: (c.toNight as number | null | undefined) ?? null,
        invoicedOn: (c.invoicedOn as string | null | undefined) ?? null,
        invoiceId: c.invoiceId ?? (c.billed ? 'billed' : null),
      };
    });
  const approvalsWire = (w.approvals as Wire[] | undefined) ?? [];
  const approvals: Wire[] = approvalsWire.map((a) => ({
    ...a,
    catName: a.catName ?? catName(a.catId),
    decidedVia: a.decidedVia ?? null,
  }));
  const incidents = ((w.incidents as Wire[] | undefined) ?? []).map((i) => ({
    ...i,
    catName: i.catName ?? catName(i.catId),
    occurredOn: realInstant(i.occurredOn, i.reportedOn),
    acknowledged: Boolean(i.acknowledged ?? i.ownerAcknowledged),
  }));
  const referrals = ((w.referrals as Wire[] | undefined) ?? []).map((r) => ({
    ...r,
    returnedOn: (r.returnedOn as string | null | undefined) ?? null,
  }));
  const allocations = ((w.allocations as Wire[] | undefined) ?? []).map((a, index) => ({
    ...a,
    id: String(a.id ?? `${String(a.unitId ?? 'unit')}-${index}`),
    unitName: String(a.unitName ?? a.unitId ?? ''),
    from: String(a.from ?? a.occupiedFrom ?? ''),
    to: String(a.to ?? a.occupiedTo ?? ''),
    reason: (a.reason as string | null | undefined) ?? null,
  }));
  const history = ((w.statusHistory as Wire[] | undefined) ?? []).map((h) => ({
    ...h,
    changedBy: String(h.changedBy ?? ''),
  }));
  return {
    ...w,
    unitName: w.unitName ?? unit?.name ?? null,
    unitBlock: w.unitBlock ?? unit?.block ?? null,
    cats,
    deposit: {
      ...deposit,
      payBy: deposit.payBy ?? deposit.dueBy ?? null,
      accepted: deposit.accepted ?? deposit.received ?? zero,
      outstanding: deposit.outstanding ?? zero,
    },
    balanceDue: (w.balanceDue as BoardingMoney | null | undefined) ?? null,
    attention: w.attention ?? {
      needsAttention: Boolean(w.needsAttention),
      reason: w.attentionReason ?? null,
      note: null,
    },
    requiresIsolation: Boolean(w.requiresIsolation) || cats.some((c) => c.suspectedInfectious),
    canOverrideGate: w.canOverrideGate ?? cats.some((c) => c.gateStatus === 'Blocked'),
    // The built stay carries no `cancellation` member: Cancel is offered from `allowedTransitions`, and
    // the deposit outcome is only known once the server answers (`depositForfeited`) - HO-BRD-002 1 Amended.
    cancellation: w.cancellation ?? {
      canCancel: ((w.allowedTransitions as string[] | undefined) ?? []).includes('Cancel'),
      depositOutcome: null,
    },
    allowedTransitions: mapTransitions(w.allowedTransitions),
    charges,
    invoices: w.invoices ?? [],
    allocations,
    isolationEntries: w.isolationEntries ?? [],
    referrals,
    approvals,
    incidents,
    pendingApprovals:
      w.pendingApprovals ??
      approvals
        .filter((a) => a.status === 'Pending')
        .map((a) => ({
          id: a.id,
          catName: a.catName,
          amount: a.amount,
          description: a.description,
          status: a.status,
        })),
    statusHistory: history,
  } as T;
}

/** The built gate's `bookingLink` is an absolute URL on the web app's own origin; only its path and query are
 *  followed (`isSafeInternalPath`), so another host can never be linked to (HO-BRD-002 1 Amended). */
export function toInternalLink(link: string): string {
  try {
    const url = new URL(link, 'http://placeholder.invalid');
    return link.startsWith('/') ? link : `${url.pathname}${url.search}`;
  } catch {
    return link;
  }
}

/** Every gate the API sends (enquiry, reserve and daycare refusals) goes through the same link rewrite. */
export function normaliseGate(gate: VaccinationGate): VaccinationGate {
  return {
    ...gate,
    blockedCats: gate.blockedCats.map((c) => ({
      ...c,
      bookingLink: toInternalLink(c.bookingLink),
    })),
  };
}

export function normaliseEnquiry(result: EnquiryResult): EnquiryResult {
  return { ...result, vaccinationGate: normaliseGate(result.vaccinationGate) };
}

export async function createEnquiry(request: EnquiryRequest): Promise<EnquiryResult> {
  return normaliseEnquiry(
    await apiFetch<EnquiryResult>(`${BASE}/enquiries`, { method: 'POST', body: request }),
  );
}

/** `PUT …/stays/{id}/enquiry` — C5 change; allowed while the stay is still an `Enquiry`. */
export async function changeEnquiry(
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

export async function saveCareProfile(
  stayId: string,
  catId: string,
  profile: CareProfile,
): Promise<void> {
  await apiFetch<void>(`${BASE}/stays/${enc(stayId)}/cats/${enc(catId)}/care-profile`, {
    method: 'PUT',
    body: careProfileToWire(profile),
  });
}

export async function reserveStay(stayId: string): Promise<ReserveResult> {
  const result = await apiFetch<ReserveResult>(`${BASE}/stays/${enc(stayId)}/reserve`, {
    method: 'POST',
  });
  return { ...result, stay: normaliseStay<MyStayDetail>(result.stay) };
}

export async function cancelMyStay(
  stayId: string,
  note: string | null,
): Promise<{ depositForfeited: boolean }> {
  return apiFetch<{ depositForfeited: boolean }>(`${BASE}/stays/${enc(stayId)}/cancel`, {
    method: 'POST',
    body: { note },
  });
}

export type MyStayScope = 'upcoming' | 'current' | 'past';

/** A stay summary row: the built member names are `requestedUnitType`, `unitCode`, `customerName`. */
export function normaliseSummary<T>(raw: unknown): T {
  const w = asWire(raw);
  return {
    ...w,
    unitType: w.unitType ?? w.requestedUnitType,
    unitName: w.unitName ?? w.unitCode ?? null,
    balanceDue: (w.balanceDue as BoardingMoney | null | undefined) ?? null,
    branchTimeZoneId: w.branchTimeZoneId ?? 'Asia/Dhaka',
    vaccinationBlocked: Boolean(w.vaccinationBlocked ?? w.gateBlocked),
    anyHighRisk: Boolean(w.anyHighRisk),
    needsAttention: Boolean(w.needsAttention),
    gateBlocked: Boolean(w.gateBlocked ?? w.vaccinationBlocked),
    requiresIsolation: Boolean(w.requiresIsolation),
  } as T;
}

export async function getMyStays(
  scope: MyStayScope,
  pageNumber = 1,
  pageSize = 20,
): Promise<PagedResult<MyStaySummary>> {
  const query = new URLSearchParams({
    scope,
    pageNumber: String(pageNumber),
    pageSize: String(pageSize),
  });
  const page = await apiFetch<PagedResult<unknown>>(`${BASE}/stays?${query.toString()}`);
  return { ...page, items: page.items.map((i) => normaliseSummary<MyStaySummary>(i)) };
}

export async function getMyStay(stayId: string): Promise<MyStayDetail> {
  return normaliseStay<MyStayDetail>(await apiFetch<unknown>(`${BASE}/stays/${enc(stayId)}`));
}

/** The built diary (HO-BRD-001 1.1): `{ stayId, from, to, days:[{ date, observations, media, completedTasks }] }`.
 *  Observations carry no id and media carry a `file` (signed URL view model); the cat headers are read
 *  off the observations. */
export function normaliseDiary(input: unknown, zone = 'Asia/Dhaka'): StayDiary {
  const w = asWire(input);
  const headers = new Map<string, string>();
  const days = ((w.days as Wire[] | undefined) ?? []).map((d) => ({
    date: String(d.date),
    observations: ((d.observations as Wire[] | undefined) ?? []).map((o, index) => {
      headers.set(String(o.catId), String(o.catName ?? ''));
      return {
        ...o,
        id: String(o.id ?? `${String(d.date)}-${String(o.catId)}-${index}`),
        recordedOn: String(o.recordedOn ?? o.date ?? d.date),
      };
    }),
    media: ((d.media as Wire[] | undefined) ?? []).map((m) => {
      const file = m.file as Wire | null | undefined;
      return {
        id: String(m.id),
        catId: (m.catId as string | null | undefined) ?? null,
        caption: (m.caption as string | null | undefined) ?? null,
        capturedOnLocalDate: String(m.capturedOnLocalDate ?? d.date),
        reference: (m.reference as string | null | undefined) ?? null,
        // the API signs a path on ITS origin: anchor it there (a bare path would 404 on the web origin)
        fileUrl: resolveFileUrl(file?.url as string | undefined),
      };
    }),
    completedTaskCount: Number(d.completedTaskCount ?? d.completedTasks ?? 0),
  }));
  return {
    stayId: String(w.stayId),
    branchTimeZoneId: String(w.branchTimeZoneId ?? zone),
    catHeaders:
      (w.catHeaders as StayDiary['catHeaders'] | undefined) ??
      [...headers.entries()].map(([catId, catName]) => ({ catId, catName, catIsHighRisk: false })),
    days,
  } as unknown as StayDiary;
}

export async function getMyStayDiary(
  stayId: string,
  from?: string,
  to?: string,
): Promise<StayDiary> {
  const query = new URLSearchParams();
  if (from) query.set('from', from);
  if (to) query.set('to', to);
  const suffix = query.size > 0 ? `?${query.toString()}` : '';
  return normaliseDiary(await apiFetch<unknown>(`${BASE}/stays/${enc(stayId)}/diary${suffix}`));
}

/** An incident as the built read sends it. `occurredOn` of `0001-01-01` is "not given": use `reportedOn`. */
export function normaliseIncident(input: unknown, zone = 'Asia/Dhaka'): IncidentDetail {
  const w = asWire(input);
  return {
    ...w,
    catIsHighRisk: Boolean(w.catIsHighRisk),
    occurredOn: realInstant(w.occurredOn, w.reportedOn) ?? '',
    branchTimeZoneId: String(w.branchTimeZoneId ?? zone),
    customerName: (w.customerName as string | null | undefined) ?? null,
    customerPhone: (w.customerPhone as string | null | undefined) ?? null,
    unitName: (w.unitName as string | null | undefined) ?? null,
  } as unknown as IncidentDetail;
}

export async function getMyIncident(stayId: string, incidentId: string): Promise<IncidentDetail> {
  return normaliseIncident(
    await apiFetch<unknown>(`${BASE}/stays/${enc(stayId)}/incidents/${enc(incidentId)}`),
  );
}

export async function acknowledgeIncident(stayId: string, incidentId: string): Promise<void> {
  await apiFetch<void>(`${BASE}/stays/${enc(stayId)}/incidents/${enc(incidentId)}/acknowledge`, {
    method: 'POST',
  });
}

/** The built owner approval carries no limit/used/remaining and no cat flags (HO-BRD-002 1 Amended). */
export async function getMyApproval(
  stayId: string,
  approvalId: string,
): Promise<VetApprovalDetail> {
  const w = asWire(
    await apiFetch<unknown>(`${BASE}/stays/${enc(stayId)}/approvals/${enc(approvalId)}`),
  );
  return {
    ...w,
    stayId: String(w.stayId ?? stayId),
    catIsHighRisk: Boolean(w.catIsHighRisk),
    branchTimeZoneId: String(w.branchTimeZoneId ?? 'Asia/Dhaka'),
  } as unknown as VetApprovalDetail;
}

export type ApprovalAnswer = 'Approve' | 'Decline';

export async function respondToApproval(
  stayId: string,
  approvalId: string,
  answer: ApprovalAnswer,
  note: string | null,
): Promise<void> {
  await apiFetch<void>(`${BASE}/stays/${enc(stayId)}/approvals/${enc(approvalId)}/respond`, {
    method: 'POST',
    body: { approve: answer === 'Approve', note },
  });
}

/** The built report (HO-BRD-001 1.1): daily logs are a flat `observations[]` per cat, weights are numbers
 *  (`weightIn/Out/Change`), the invoices are `billing.invoiceIds` only. */
export function normaliseStaySummary(input: unknown, zone = 'Asia/Dhaka'): StaySummary {
  const w = asWire(input);
  const money = (value: unknown): BoardingMoney =>
    (value as BoardingMoney | null | undefined) ?? { amount: '0.00', currencyCode: 'BDT' };
  const kg = (value: unknown): string | null => (value == null ? null : String(value));
  const billing = asWire(w.billing);
  const cats = ((w.cats as Wire[] | undefined) ?? []).map((c) => {
    const byDate = new Map<string, Wire[]>();
    for (const o of (c.observations as Wire[] | undefined) ?? []) {
      const date = String(o.date);
      byDate.set(date, [...(byDate.get(date) ?? []), { ...o, recordedOn: o.recordedOn ?? date }]);
    }
    const signedOn = c.checkOutSignedOn as string | null | undefined;
    return {
      catId: String(c.catId),
      catName: String(c.catName),
      catIsHighRisk: Boolean(c.catIsHighRisk),
      dailyLogs:
        c.dailyLogs ??
        [...byDate.entries()].map(([date, observations]) => ({ date, observations })),
      weightAtCheckInKg: kg(c.weightAtCheckInKg ?? c.weightIn),
      weightAtCheckOutKg: kg(c.weightAtCheckOutKg ?? c.weightOut),
      weightChangeKg: kg(c.weightChangeKg ?? c.weightChange),
      incidents: ((c.incidents as Wire[] | undefined) ?? []).map((i) => ({
        ...i,
        catName: i.catName ?? c.catName,
        occurredOn: realInstant(i.occurredOn, null),
        acknowledged: Boolean(i.acknowledged),
      })),
      medicationsGiven:
        ((c.medicationsGiven ?? c.medications) as Wire[] | undefined)?.map((m) => ({
          name: String(m.name ?? m.title ?? ''),
          dose: String(m.dose ?? ''),
          givenOn: String(m.givenOn ?? ''),
          givenBy: String(m.givenBy ?? ''),
        })) ?? [],
      vetVisits: (c.vetVisits as StaySummary['cats'][number]['vetVisits'] | undefined) ?? [],
      belongings: ((c.belongings as Wire[] | undefined) ?? []).map((b, index) => ({
        id: String(b.id ?? index),
        kind: b.kind,
        description: String(b.description ?? ''),
        returned: (b.returned as boolean | null | undefined) ?? null,
        returnNote: (b.returnNote ?? b.note ?? null) as string | null,
      })),
      checkOutSignature:
        c.checkOutSignature ??
        (c.checkOutSignerName
          ? {
              signerName: String(c.checkOutSignerName),
              method: (c.checkOutSignatureMethod as string | undefined) ?? null,
              signedOn: signedOn ?? '',
            }
          : null),
    };
  });
  const ids = (billing.invoiceIds as string[] | undefined) ?? [];
  return {
    stayId: String(w.stayId),
    branchTimeZoneId: String(w.branchTimeZoneId ?? zone),
    arrivalOn: String(w.arrivalOn),
    departureOn: String(w.departureOn),
    checkedOutOn: (w.checkedOutOn as string | null | undefined) ?? null,
    cats,
    charges: ((w.charges as Wire[] | undefined) ?? []).map((c) => ({
      id: String(c.id ?? c.lineRef),
      kind: 'RateUnit',
      description: String(c.description ?? ''),
      amount: money(c.amount ?? c.net),
      invoiceId: c.billed ? 'billed' : null,
    })),
    depositReceived: money(w.depositReceived),
    billing: w.billing
      ? {
          billedTotal: money(billing.billedTotal),
          unbilledTotal: money(billing.unbilledTotal),
          invoicedOutstanding: money(billing.invoicedOutstanding),
        }
      : null,
    invoices: (w.invoices as StaySummary['invoices'] | undefined) ?? ids.map((id) => ({ id })),
  } as StaySummary;
}

export async function getMyStaySummary(stayId: string): Promise<StaySummary> {
  return normaliseStaySummary(await apiFetch<unknown>(`${BASE}/stays/${enc(stayId)}/summary`));
}

// ---- waitlist (C24) ----

export interface JoinWaitlistRequest {
  branchId: string;
  arrivalOn: string;
  departureOn: string;
  catCount: number;
  unitType?: BoardingUnitType;
}

export async function getMyBoardingWaitlist(): Promise<WaitlistEntryDto[]> {
  const rows = await apiFetch<Wire[]>(`${BASE}/waitlist`);
  return rows.map((r) => ({
    ...r,
    branchTimeZoneId: String(r.branchTimeZoneId ?? 'Asia/Dhaka'),
    notifiedOn: (r.notifiedOn as string | null | undefined) ?? null,
    unitType: (r.unitType as BoardingUnitType | null | undefined) ?? null,
  })) as unknown as WaitlistEntryDto[];
}

export async function joinBoardingWaitlist(request: JoinWaitlistRequest): Promise<{ id: string }> {
  const result = await apiFetch<{ id?: string; entryId?: string; waitlistEntryId?: string }>(
    `${BASE}/waitlist`,
    { method: 'POST', body: request },
  );
  return { id: result.id ?? result.entryId ?? result.waitlistEntryId ?? '' };
}

export async function withdrawFromBoardingWaitlist(entryId: string): Promise<void> {
  await apiFetch<void>(`${BASE}/waitlist/${enc(entryId)}`, { method: 'DELETE' });
}

// ---- daycare (C25) ----

export interface BookDaycareRequest {
  branchId: string;
  catId: string;
  localDate: string;
  basis: DaycareBasis;
  expectedHours?: number;
}

/** A daycare visit as the built read sends it: no zone, no cat risk flags (HO-BRD-002 1 Amended). */
export function normaliseDaycareVisit(input: unknown, zone = 'Asia/Dhaka'): DaycareVisit {
  const w = asWire(input);
  return {
    ...w,
    branchTimeZoneId: String(w.branchTimeZoneId ?? zone),
    catIsHighRisk: Boolean(w.catIsHighRisk),
    handlingNote: (w.handlingNote as string | null | undefined) ?? null,
    expectedHours: (w.expectedHours as number | null | undefined) ?? null,
    chargeAmount: (w.chargeAmount as BoardingMoney | null | undefined) ?? null,
    checkedInOn: (w.checkedInOn as string | null | undefined) ?? null,
    checkedOutOn: (w.checkedOutOn as string | null | undefined) ?? null,
  } as unknown as DaycareVisit;
}

export async function getMyDaycareVisits(): Promise<DaycareVisit[]> {
  const rows = await apiFetch<unknown[]>('/api/customers/me/daycare');
  return rows.map((r) => normaliseDaycareVisit(r));
}

export async function bookDaycare(request: BookDaycareRequest): Promise<{ id: string }> {
  const result = await apiFetch<{ id?: string; visitId?: string }>('/api/customers/me/daycare', {
    method: 'POST',
    body: request,
  });
  return { id: result.id ?? result.visitId ?? '' };
}

export async function cancelDaycare(visitId: string): Promise<void> {
  await apiFetch<void>(`/api/customers/me/daycare/${enc(visitId)}/cancel`, { method: 'POST' });
}
