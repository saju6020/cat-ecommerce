import type {
  BoardingCatHeader,
  BoardingDashboard,
  BoardingMoney,
  BoardingOccupancy,
  BoardingStayBase,
  BoardingStayStatus,
  CareTask,
  DaycareVisit,
  DiaryObservation,
  IncidentDetail,
  IncidentSummary,
  MyStayDetail,
  MyStaySummary,
  StaffStayDetail,
  StaffStaySummary,
  StayCatDetail,
  StayDeposit,
  TaskDayList,
  TaskUnitGroup,
  VetApprovalDetail,
} from '@/lib/api/types';
import { addDaysIso } from '@/lib/format/appointment';
import { localDateOf, todayInZone } from '@/lib/format/time';
import { zonedLocalToInstant } from '@/lib/format/appointment';
import { loadPersisted, savePersisted, clearPersisted } from '../admin/fixtures';
import {
  BILLING_LEDGER_STORAGE_KEY,
  BRANCH_INFO,
  boardingFixtureIds as ids,
  serverNow,
} from './fixtures';
import type {
  BoardingDb,
  StoredCat,
  StoredIncident,
  StoredObservation,
  StoredStay,
  StoredTask,
  StoredUnit,
} from './mock-types';
import {
  allowedTransitionsFor,
  CANCELLATION_WINDOW_HOURS,
  money,
  num,
  round2,
  TASK_OVERDUE_GRACE_MINUTES,
} from './rules';
import { cardValues, seedBoardingDb } from './seed';
import { stayToBuilt } from './wire';

/**
 * `mocks/handlers/boarding/store.ts` — HO-BRD-002 §12. One in-memory (sessionStorage-persisted, like
 * the other area mocks) database shared by the customer, staff and care handlers, plus the view
 * mappers that turn stored rows into HO-BRD-001 §12.4 shapes. The "server" rules (pricing,
 * availability, overdue, not-eating) live in `rules.ts` / here and ONLY here.
 */

const PERSIST_KEY = '__purrcare_boarding_mock_db__';

let db: BoardingDb | null = null;

export function getDb(): BoardingDb {
  if (!db) {
    db = loadPersisted<BoardingDb>(PERSIST_KEY);
    if (!db) {
      // Persist the seed straight away: its times are relative to "now", so a reload must not re-seed.
      db = seedBoardingDb();
      persist();
    }
  }
  return db;
}

export function persist(): void {
  if (db) savePersisted(PERSIST_KEY, db);
}

/** Test seam — drop the database (and its persisted copy) so the next read re-seeds. */
export function resetBoardingMockData(): void {
  db = null;
  clearPersisted(PERSIST_KEY);
  try {
    window.sessionStorage.removeItem(BILLING_LEDGER_STORAGE_KEY);
  } catch {
    // no sessionStorage (Vitest/node) — nothing to clear
  }
}

export function nextId(prefix: string): string {
  const d = getDb();
  d.seq += 1;
  return `${prefix}000000-0000-0000-0000-${String(d.seq).padStart(12, '0')}`;
}

// ---- branch and clock ----

export function branchIdOf(request: Request): string {
  return request.headers.get('x-branch-id') || ids.mainBranchId;
}

export function zoneOfBranch(branchId: string): string {
  return BRANCH_INFO[branchId]?.zone ?? 'Asia/Dhaka';
}

export function nowIso(): string {
  return new Date(serverNow()).toISOString();
}

export function todayOf(branchId: string): string {
  return todayInZone(zoneOfBranch(branchId));
}

// ---- Billing ledger (simulated: Billing records the deposit, Boarding only reads it) ----

export interface LedgerEntry {
  accepted: number;
  pending?: boolean;
}

const memoryLedger: Record<string, LedgerEntry> = {};

/** Test seam for Vitest, where sessionStorage persistence is off. */
export function setMockBillingLedger(stayId: string, entry: LedgerEntry): void {
  memoryLedger[stayId] = entry;
}

export function ledgerFor(stay: StoredStay): LedgerEntry {
  if (stay.depositState === 'Received') return { accepted: stay.depositRequired };
  let stored: Record<string, LedgerEntry> = {};
  try {
    stored = JSON.parse(
      window.sessionStorage.getItem(BILLING_LEDGER_STORAGE_KEY) ?? '{}',
    ) as Record<string, LedgerEntry>;
  } catch {
    stored = {};
  }
  return stored[stay.id] ?? memoryLedger[stay.id] ?? { accepted: 0 };
}

// ---- lookups ----

export function unitById(unitId: string | null): StoredUnit | undefined {
  return unitId ? getDb().units.find((u) => u.id === unitId) : undefined;
}

export function findStay(stayId: string): StoredStay | undefined {
  return getDb().stays[stayId];
}

export function staysOfBranch(branchId: string): StoredStay[] {
  return Object.values(getDb().stays).filter((s) => s.branchId === branchId);
}

export function isMine(stay: StoredStay | undefined): stay is StoredStay {
  return !!stay && stay.customerId === ids.signedInCustomerId;
}

/** §8.10 lapse: a `Reserved` stay past `payBy` with nothing accepted or pending is cancelled. */
export function sweepLapses(): void {
  const now = serverNow();
  let changed = false;
  for (const s of Object.values(getDb().stays)) {
    if (s.status !== 'Reserved' || !s.payBy) continue;
    const ledger = ledgerFor(s);
    if (new Date(s.payBy).getTime() > now) continue;
    if (
      s.depositState === 'PendingApproval' ||
      ledger.pending ||
      ledger.accepted >= s.depositRequired
    ) {
      continue;
    }
    s.status = 'Cancelled';
    s.depositState = 'Awaiting';
    s.allocations = s.allocations.map((a) => ({ ...a, isActive: false }));
    s.unitId = null;
    s.statusHistory.push({
      from: 'Reserved',
      to: 'Cancelled',
      changedOn: nowIso(),
      changedBy: 'System (reservation lapsed)',
    });
    s.attention = { needsAttention: false, reason: null, note: 'ReservationLapsed' };
    s.version += 1;
    changed = true;
  }
  if (changed) persist();
}

// ---- stay views ----

function catHeader(c: StoredCat): BoardingCatHeader {
  return {
    catId: c.catId,
    catName: c.catName,
    catPhotoUrl: null,
    catIsHighRisk: c.isHighRisk,
    handlingNote: c.handlingNote,
  };
}

function catDetail(c: StoredCat): StayCatDetail {
  return {
    ...catHeader(c),
    careProfile: c.careProfile,
    careProfileComplete: !!c.careProfile && c.careProfile.feedings.length > 0,
    gateStatus: c.gateStatus,
    gateReasons: c.gateReasons,
    gateOverriddenBy: c.gateOverriddenBy,
    gateOverrideReason: c.gateOverrideReason,
    suspectedInfectious: c.suspectedInfectious,
    awayFromUnit: c.awayFromUnit,
    awayReason: c.awayReason,
    checkedInWeightKg: c.checkIn?.weightKg ?? null,
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
  };
}

export function acceptedOf(stay: StoredStay): number {
  return ledgerFor(stay).accepted;
}

function depositView(stay: StoredStay): StayDeposit {
  const accepted = acceptedOf(stay);
  const outstanding = Math.max(0, round2(stay.depositRequired - accepted));
  return {
    required: money(stay.depositRequired),
    accepted: money(accepted),
    outstanding: money(outstanding),
    state: stay.depositState,
    payBy: stay.status === 'Reserved' ? stay.payBy : null,
    instructionsPath: '/pages/boarding-payment-instructions',
  };
}

function balanceOf(stay: StoredStay): BoardingMoney {
  if (stay.status === 'Cancelled' || stay.status === 'NoShow') return money(0);
  const total = stay.charges.reduce((sum, c) => sum + num(c.amount), 0);
  return money(Math.max(0, round2(total - acceptedOf(stay))));
}

function base(stay: StoredStay): BoardingStayBase {
  const unit = unitById(stay.unitId);
  const nights = Math.round(
    (new Date(`${stay.departureOn}T00:00:00Z`).getTime() -
      new Date(`${stay.arrivalOn}T00:00:00Z`).getTime()) /
      86_400_000,
  );
  return {
    id: stay.id,
    branchId: stay.branchId,
    branchTimeZoneId: zoneOfBranch(stay.branchId),
    version: stay.version,
    status: stay.status,
    allowedTransitions: allowedTransitionsFor(stay.status),
    arrivalOn: stay.arrivalOn,
    departureOn: stay.departureOn,
    nights,
    requestedUnitType: stay.requestedUnitType,
    unitName: unit?.name ?? null,
    unitBlock: unit?.block ?? null,
    cats: stay.cats.map(catDetail),
    // the built API has no quote and no balance on an Enquiry (the quote is on the enquiry answer)
    quote: stay.status === 'Enquiry' ? null : stay.quote,
    deposit: depositView(stay),
    balanceDue: stay.status === 'Enquiry' ? null : balanceOf(stay),
    charges: stay.charges,
    invoices: stay.invoices,
    occupiedFrom: zonedLocalToInstant(stay.arrivalOn, '14:00', zoneOfBranch(stay.branchId)),
    checkedInOn: stay.checkedInOn,
    checkedOutOn: stay.checkedOutOn,
  };
}

export function incidentsOf(stayId: string): StoredIncident[] {
  return getDb().incidents.filter((i) => i.stayId === stayId);
}

function incidentSummary(i: StoredIncident, stay: StoredStay): IncidentSummary {
  return {
    id: i.id,
    catName: stay.cats.find((c) => c.catId === i.catId)?.catName ?? 'Your cat',
    kind: i.kind,
    severity: i.severity,
    occurredOn: i.occurredOn,
    acknowledged: !!i.ownerAcknowledgedOn,
  };
}

/** The money a cancellation would leave the owner with, in the server's own words (§8.10, BR-12). */
export function depositOutcomeText(stay: StoredStay): string | null {
  const accepted = acceptedOf(stay);
  if (accepted <= 0) return 'No deposit has been paid, so there is nothing to keep or refund.';
  const zone = zoneOfBranch(stay.branchId);
  const from = new Date(zonedLocalToInstant(stay.arrivalOn, '14:00', zone)).getTime();
  const inside = serverNow() > from - CANCELLATION_WINDOW_HOURS * 3_600_000;
  return inside
    ? 'Your deposit will be kept under our cancellation policy, because the stay starts in less than 72 hours.'
    : 'Your deposit will be refunded by the method it was paid.';
}

export function toCustomerDetail(stay: StoredStay): MyStayDetail {
  const cancellable = ['Enquiry', 'Reserved', 'Confirmed'].includes(stay.status);
  return {
    ...base(stay),
    cancellation: cancellable
      ? { canCancel: true, depositOutcome: depositOutcomeText(stay) }
      : { canCancel: false, depositOutcome: null },
    incidents: incidentsOf(stay.id).map((i) => incidentSummary(i, stay)),
    pendingApprovals: stay.approvals
      .filter((a) => a.status === 'Pending')
      .map((a) => ({
        id: a.id,
        catName: a.catName,
        amount: a.amount,
        description: a.description,
        status: a.status,
      })),
  };
}

export function toCustomerSummary(stay: StoredStay): MyStaySummary {
  const detail = base(stay);
  return {
    id: stay.id,
    branchTimeZoneId: detail.branchTimeZoneId,
    status: stay.status,
    arrivalOn: stay.arrivalOn,
    departureOn: stay.departureOn,
    nights: detail.nights,
    catNames: stay.cats.map((c) => c.catName),
    unitType: stay.requestedUnitType,
    depositState: stay.depositState,
    balanceDue: detail.balanceDue,
    vaccinationBlocked: stay.cats.some((c) => c.gateStatus === 'Blocked'),
  };
}

export function toStaffDetail(stay: StoredStay): StaffStayDetail {
  return {
    ...base(stay),
    customerId: stay.customerId,
    customerName: stay.customerName,
    customerPhone: stay.customerPhone,
    channel: stay.channel,
    requiresIsolation: stay.cats.some((c) => c.suspectedInfectious),
    statusHistory: stay.statusHistory,
    allocations: stay.allocations,
    isolationEntries: stay.isolationEntries,
    referrals: stay.referrals,
    approvals: stay.approvals,
    incidents: incidentsOf(stay.id).map((i) => incidentSummary(i, stay)),
    attention: stay.attention,
    canOverrideGate: true,
  };
}

export function toStaffSummary(stay: StoredStay): StaffStaySummary {
  return {
    id: stay.id,
    branchTimeZoneId: zoneOfBranch(stay.branchId),
    status: stay.status,
    arrivalOn: stay.arrivalOn,
    departureOn: stay.departureOn,
    catNames: stay.cats.map((c) => c.catName),
    anyHighRisk: stay.cats.some((c) => c.isHighRisk),
    customerName: stay.customerName,
    unitName: unitById(stay.unitId)?.name ?? null,
    depositState: stay.depositState,
    balanceDue: balanceOf(stay),
    needsAttention: stay.attention.needsAttention,
    gateBlocked: stay.cats.some((c) => c.gateStatus === 'Blocked'),
    requiresIsolation: stay.cats.some((c) => c.suspectedInfectious),
  };
}

// ---- incidents ----

export function toIncidentDetail(i: StoredIncident, forStaff: boolean): IncidentDetail {
  const stay = findStay(i.stayId)!;
  const cat = stay.cats.find((c) => c.catId === i.catId);
  return {
    id: i.id,
    stayId: i.stayId,
    catId: i.catId,
    catName: cat?.catName ?? 'Your cat',
    catIsHighRisk: cat?.isHighRisk ?? false,
    handlingNote: cat?.handlingNote ?? null,
    kind: i.kind,
    severity: i.severity,
    description: i.description,
    immediateAction: i.immediateAction,
    occurredOn: i.occurredOn,
    reportedOn: i.reportedOn,
    reportedByName: forStaff ? i.reportedByName : null,
    ownerNotified: i.ownerNotified,
    ownerAcknowledgedOn: i.ownerAcknowledgedOn,
    acknowledgedVia: i.acknowledgedVia,
    escalatedOn: forStaff ? i.escalatedOn : null,
    phoneCallOn: forStaff ? i.phoneCallOn : null,
    phoneCallOutcome: forStaff ? i.phoneCallOutcome : null,
    status: i.resolvedOn ? 'Resolved' : 'Open',
    resolvedOn: i.resolvedOn,
    resolution: i.resolution,
    branchTimeZoneId: zoneOfBranch(stay.branchId),
    customerName: forStaff ? stay.customerName : null,
    customerPhone: forStaff ? stay.customerPhone : null,
    unitName: forStaff ? (unitById(stay.unitId)?.name ?? null) : null,
  };
}

export function toApprovalDetail(stay: StoredStay, approvalId: string): VetApprovalDetail | null {
  const approval = stay.approvals.find((a) => a.id === approvalId);
  if (!approval) return null;
  const cat = stay.cats.find((c) => c.catId === approval.catId);
  const head = headroomFor(stay, approval.catId);
  return {
    id: approval.id,
    stayId: stay.id,
    catName: approval.catName,
    catIsHighRisk: cat?.isHighRisk ?? false,
    handlingNote: cat?.handlingNote ?? null,
    amount: approval.amount,
    description: approval.description,
    status: approval.status,
    requestedOn: approval.requestedOn,
    decidedOn: approval.decidedOn,
    limit: money(head.limit),
    used: money(head.committed),
    remaining: money(head.limit - head.committed),
    branchTimeZoneId: zoneOfBranch(stay.branchId),
  };
}

/** §8.14 `VetSpendingHeadroom` — mock-server arithmetic (the real UI never does this). */
export function headroomFor(stay: StoredStay, catId: string) {
  const cat = stay.cats.find((c) => c.catId === catId);
  const limit = cat?.careProfile?.vetVisitAuthorised ? num(cat.careProfile.vetSpendingLimit) : 0;
  const committed = stay.approvals
    .filter((a) => a.catId === catId && a.status === 'Approved')
    .reduce((sum, a) => sum + num(a.amount), 0);
  return { limit, committed, vetVisitAuthorised: !!cat?.careProfile?.vetVisitAuthorised };
}

// ---- care ----

/** §8.9 `NotEatingRule` — the last two observations are both `None`. */
export function notEatingCats(stayId: string): Set<string> {
  const byCat = new Map<string, StoredObservation[]>();
  for (const o of getDb().observations.filter((x) => x.stayId === stayId)) {
    byCat.set(o.catId, [...(byCat.get(o.catId) ?? []), o]);
  }
  const flagged = new Set<string>();
  for (const [catId, list] of byCat) {
    const last = [...list].sort((a, b) => b.recordedOn.localeCompare(a.recordedOn)).slice(0, 2);
    if (last.length === 2 && last.every((o) => o.portionEaten === 'None')) flagged.add(catId);
  }
  return flagged;
}

export function toCareTask(t: StoredTask): CareTask {
  const now = serverNow();
  const due = new Date(t.dueOn).getTime();
  const overdue = t.status === 'Pending' && now > due + TASK_OVERDUE_GRACE_MINUTES * 60_000;
  const stay = findStay(t.stayId);
  const zone = zoneOfBranch(stay?.branchId ?? ids.mainBranchId);
  return {
    id: t.id,
    stayId: t.stayId,
    catId: t.catId,
    catName: t.catName,
    kind: t.kind,
    title: t.title,
    dueOn: t.dueOn,
    dueLocalTime: new Intl.DateTimeFormat('en-GB', {
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
      timeZone: zone,
    }).format(new Date(t.dueOn)),
    status: t.status,
    isOverdue: overdue,
    minutesLate: overdue ? Math.floor((now - due) / 60_000) : null,
    assignedToName: t.assignedToName,
    assignedToMe: t.assignedToMe,
    completedOn: t.completedOn,
    completedByName: t.completedByName,
    skipReason: t.skipReason,
  };
}

export function taskDayList(
  branchId: string,
  date: string,
  filters: { unitId?: string | null; mine?: boolean; status?: string | null },
): TaskDayList {
  const d = getDb();
  const zone = zoneOfBranch(branchId);
  const inHouse = staysOfBranch(branchId).filter((s) => s.status === 'CheckedIn');
  const groups: TaskUnitGroup[] = [];
  for (const unit of d.units.filter((u) => u.branchId === branchId)) {
    if (filters.unitId && unit.id !== filters.unitId) continue;
    const stays = inHouse.filter((s) => s.unitId === unit.id);
    if (stays.length === 0) continue;
    const cats = stays.flatMap((s) => {
      const flagged = notEatingCats(s.id);
      return s.cats.map((c) => ({ ...catHeader(c), notEating: flagged.has(c.catId) }));
    });
    const tasks = d.tasks
      .filter((t) => t.unitId === unit.id && localDateOf(t.dueOn, zone) === date)
      .map(toCareTask)
      .filter((t) => (filters.mine ? t.assignedToMe : true))
      .filter((t) => {
        if (!filters.status) return true;
        if (filters.status === 'Done') return t.status === 'Done';
        if (filters.status === 'Overdue') return t.status === 'Pending' && t.isOverdue;
        return t.status === 'Pending' && !t.isOverdue;
      })
      .sort((a, b) => a.dueOn.localeCompare(b.dueOn));
    groups.push({
      unitId: unit.id,
      unitCode: unit.code,
      unitName: unit.name,
      block: unit.block,
      isIsolation: unit.unitType === 'Isolation',
      isolationNote:
        unit.unitType === 'Isolation'
          ? 'Isolation — suspected infectious. Gloves and gown on entry; record entry and exit.'
          : null,
      cats,
      tasks,
    });
  }
  return { date, branchTimeZoneId: zone, generatedOn: nowIso(), units: groups };
}

export function diaryObservation(o: StoredObservation): DiaryObservation {
  return {
    id: o.id,
    catId: o.catId,
    catName: o.catName,
    recordedOn: o.recordedOn,
    portionEaten: o.portionEaten,
    waterIntake: o.waterIntake,
    litterOutput: o.litterOutput,
    mood: o.mood,
    notes: o.notes,
  };
}

// ---- dashboard ----

export function buildDashboard(branchId: string): BoardingDashboard {
  const d = getDb();
  const zone = zoneOfBranch(branchId);
  const today = todayOf(branchId);
  const stays = staysOfBranch(branchId);
  const inHouse = stays.filter((s) => s.status === 'CheckedIn');
  const activeUnits = d.units.filter((u) => u.branchId === branchId && u.isActive);
  const occupiedUnitIds = new Set(inHouse.map((s) => s.unitId).filter(Boolean));
  const row = (s: StoredStay) => ({
    stayId: s.id,
    catNames: s.cats.map((c) => c.catName),
    anyHighRisk: s.cats.some((c) => c.isHighRisk),
    unitName: unitById(s.unitId)?.name ?? null,
    customerName: s.customerName,
    depositState: s.depositState,
    balanceDue: balanceOf(s),
    status: s.status,
  });
  const taskRow = (t: StoredTask) => {
    const view = toCareTask(t);
    return {
      taskId: t.id,
      stayId: t.stayId,
      catName: t.catName,
      unitName: unitById(t.unitId)?.name ?? '',
      title: t.title,
      dueLocalTime: view.dueLocalTime,
      minutesLate: view.minutesLate,
    };
  };
  const taskViews = d.tasks
    .filter((t) => inHouse.some((s) => s.id === t.stayId))
    .map((t) => ({ t, view: toCareTask(t) }));
  const notEating = inHouse.flatMap((s) => {
    const flagged = notEatingCats(s.id);
    return s.cats
      .filter((c) => flagged.has(c.catId))
      .map((c) => ({
        stayId: s.id,
        catId: c.catId,
        catName: c.catName,
        unitName: unitById(s.unitId)?.name ?? '',
        lastEntries: d.observations
          .filter((o) => o.stayId === s.id && o.catId === c.catId)
          .sort((a, b) => b.recordedOn.localeCompare(a.recordedOn))
          .slice(0, 2)
          .map((o) => ({ recordedOn: o.recordedOn, portionEaten: o.portionEaten })),
      }));
  });
  const openIncidents = d.incidents.filter(
    (i) => !i.resolvedOn && inHouse.some((s) => s.id === i.stayId),
  );
  const catName = (i: StoredIncident) =>
    findStay(i.stayId)?.cats.find((c) => c.catId === i.catId)?.catName ?? 'Cat';
  return {
    generatedOn: nowIso(),
    branchTimeZoneId: zone,
    occupancy: {
      unitsOccupied: occupiedUnitIds.size,
      unitsActive: activeUnits.length,
      catsInHouse: inHouse.reduce((n, s) => n + s.cats.length, 0),
      byType: (['Standard', 'Deluxe', 'Family', 'Isolation'] as const).map((unitType) => ({
        unitType,
        occupied: activeUnits.filter((u) => u.unitType === unitType && occupiedUnitIds.has(u.id))
          .length,
        active: activeUnits.filter((u) => u.unitType === unitType).length,
      })),
    },
    arrivals: stays.filter((s) => s.status === 'Confirmed' && s.arrivalOn === today).map(row),
    departures: inHouse.filter((s) => s.departureOn === today).map(row),
    notEating,
    overdueTasks: taskViews
      .filter(({ view }) => view.status === 'Pending' && view.isOverdue)
      .map(({ t }) => taskRow(t)),
    unassignedTasks: taskViews
      .filter(({ t }) => t.status === 'Pending' && !t.assignedToName)
      .map(({ t }) => taskRow(t)),
    openIncidents: openIncidents.map((i) => ({
      incidentId: i.id,
      stayId: i.stayId,
      catName: catName(i),
      kind: i.kind,
      severity: i.severity,
      acknowledged: !!i.ownerAcknowledgedOn,
      escalated: !!i.escalatedOn,
      reportedOn: i.reportedOn,
    })),
    callOwner: openIncidents
      .filter((i) => i.escalatedOn && !i.phoneCallOn && !i.ownerAcknowledgedOn)
      .map((i) => ({
        incidentId: i.id,
        stayId: i.stayId,
        catName: catName(i),
        customerName: findStay(i.stayId)?.customerName ?? '',
        customerPhone: findStay(i.stayId)?.customerPhone ?? null,
        reportedOn: i.reportedOn,
      })),
    unpaidBalances: [
      ...stays
        .filter((s) => s.status === 'Reserved')
        .map((s) => ({
          stayId: s.id,
          customerName: s.customerName,
          catNames: s.cats.map((c) => c.catName),
          kind: 'DepositAwaiting' as const,
          amount: money(Math.max(0, round2(s.depositRequired - acceptedOf(s)))),
          depositDueBy: s.payBy,
        })),
      ...stays
        .filter(
          (s) => (s.status === 'CheckedIn' || s.status === 'CheckedOut') && num(balanceOf(s)) > 0,
        )
        .map((s) => ({
          stayId: s.id,
          customerName: s.customerName,
          catNames: s.cats.map((c) => c.catName),
          kind: 'BalanceOutstanding' as const,
          amount: balanceOf(s),
          depositDueBy: null,
        })),
    ],
    refundDue: stays
      .filter((s) => s.status === 'Cancelled' && acceptedOf(s) > 0 && s.depositForfeited === false)
      .map((s) => ({
        stayId: s.id,
        customerName: s.customerName,
        amount: money(acceptedOf(s)),
        reason: 'Cancelled outside the cancellation window',
      })),
    attention: stays
      .filter((s) => s.attention.needsAttention && s.attention.reason)
      .map((s) => ({
        stayId: s.id,
        catNames: s.cats.map((c) => c.catName),
        reason: s.attention.reason!,
        note: s.attention.note,
      })),
  };
}

// ---- occupancy board ----

export function buildOccupancy(branchId: string, from: string, to: string): BoardingOccupancy {
  const d = getDb();
  const days: string[] = [];
  for (let day = from; day <= to && days.length < 31; day = addDaysIso(day, 1)) days.push(day);
  const stays = staysOfBranch(branchId).filter((s) =>
    ['Reserved', 'Confirmed', 'CheckedIn'].includes(s.status),
  );
  return {
    from,
    to: days[days.length - 1] ?? to,
    branchTimeZoneId: zoneOfBranch(branchId),
    days,
    units: d.units
      .filter((u) => u.branchId === branchId)
      .sort((a, b) => (a.block ?? '').localeCompare(b.block ?? '') || a.sortOrder - b.sortOrder)
      .map((u) => {
        const own = stays.filter((s) => s.unitId === u.id);
        return {
          unitId: u.id,
          code: u.code,
          name: u.name,
          unitType: u.unitType,
          block: u.block,
          stays: own
            .filter(
              (s) => s.arrivalOn <= (days.at(-1) ?? to) && addDaysIso(s.departureOn, -1) >= from,
            )
            .map((s) => ({
              stayId: s.id,
              catNames: s.cats.map((c) => c.catName),
              status: s.status,
              fromOn: s.arrivalOn < from ? from : s.arrivalOn,
              toOn:
                addDaysIso(s.departureOn, -1) > (days.at(-1) ?? to)
                  ? (days.at(-1) ?? to)
                  : addDaysIso(s.departureOn, -1),
              awayInSurgery: s.cats.some((c) => c.awayFromUnit),
            })),
          maintenance: d.maintenance
            .filter(
              (m) =>
                m.unitId === u.id &&
                !m.isCancelled &&
                m.startsOn <= (days.at(-1) ?? to) &&
                m.endsOn >= from,
            )
            .map((m) => ({ blockId: m.id, fromOn: m.startsOn, toOn: m.endsOn, reason: m.reason })),
          turnaroundDays: own
            .map((s) => s.departureOn)
            .filter((day) => day >= from && day <= (days.at(-1) ?? to)),
        };
      }),
  };
}

// ---- daycare ----

export function toDaycareVisit(v: BoardingDb['daycare'][number], forStaff: boolean): DaycareVisit {
  return {
    id: v.id,
    branchTimeZoneId: zoneOfBranch(v.branchId),
    catId: v.catId,
    catName: v.catName,
    catIsHighRisk: v.catIsHighRisk,
    handlingNote: v.handlingNote,
    customerName: forStaff ? v.customerName : null,
    localDate: v.localDate,
    basis: v.basis,
    expectedHours: v.expectedHours,
    status: v.status,
    chargeAmount: v.chargeAmount == null ? null : money(v.chargeAmount),
    checkedInOn: v.checkedInOn,
    checkedOutOn: v.checkedOutOn,
  };
}

// ---- rate cards ----

export function currentCard(unitType: StoredUnit['unitType']) {
  const nowIso_ = new Date(serverNow()).toISOString();
  const found = [...getDb().rateCards]
    .filter((c) => c.unitType === unitType && c.effectiveFrom <= nowIso_)
    .sort((a, b) => b.effectiveFrom.localeCompare(a.effectiveFrom))[0];
  return found ?? null;
}

export function cardValuesFor(unitType: StoredUnit['unitType']) {
  const c = currentCard(unitType);
  return c ? cardValues(c) : null;
}

export type { BoardingStayStatus };

/** The stay views as the BUILT contract sends them (HO-BRD-001 1.1) — see `wire.ts`. */
export const wireCustomerDetail = (stay: StoredStay) => stayToBuilt(toCustomerDetail(stay));
export const wireStaffDetail = (stay: StoredStay) => stayToBuilt(toStaffDetail(stay));
