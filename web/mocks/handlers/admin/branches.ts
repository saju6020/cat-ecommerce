import { http, HttpResponse, type HttpHandler } from 'msw';
import {
  administrationFixtureIds,
  validationError,
  notFound,
  noContent,
  loadPersisted,
  savePersisted,
  clearPersisted,
  zonedTimeToUtcIso,
} from './fixtures';

const PERSIST_KEY = '__purrcare_admin_branches_mock_db__';

/** `mocks/handlers/admin/branches.ts` — HO-ADM-002 §12, HO-ADM-001 §12.4. Branches, opening
 *  hours, closures, rooms, staff branch assignment and the active-branch feed. Two branches in
 *  different zones (Asia/Dhaka, Europe/London) so zone rendering is exercised (§12). */

interface OpeningInterval {
  dayOfWeek: string;
  opens: string;
  closes: string;
}

interface Branch {
  id: string;
  code: string;
  name: string;
  timeZoneId: string;
  addressLine: string | null;
  phoneNumber: string | null;
  email: string | null;
  isActive: boolean;
  weeklyHours: OpeningInterval[];
}

interface Closure {
  id: string;
  branchId: string;
  startsOn: string;
  endsOn: string;
  kind: 'PublicHoliday' | 'Closure';
  reason: string;
  partialFrom: string | null;
  partialTo: string | null;
  isCancelled: boolean;
}

interface Room {
  id: string;
  branchId: string;
  name: string;
  kind: string;
  isActive: boolean;
}

const DAY_NAMES = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

/** Date-string arithmetic at UTC noon, never via the browser's zone (HO-ADM-002-FU1 §12.2 (e)). */
function dayNumber(dateIso: string): number {
  return Math.floor(new Date(`${dateIso}T12:00:00Z`).getTime() / 86_400_000);
}
function dateOfDayNumber(day: number): string {
  return new Date(day * 86_400_000 + 43_200_000).toISOString().slice(0, 10);
}
function toMinutes(time: string): number {
  const [h = '0', m = '0'] = time.split(':');
  return Number(h) * 60 + Number(m);
}
function fromMinutes(total: number): string {
  return `${String(Math.floor(total / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`;
}

function seedBranches(): Map<string, Branch> {
  const main: Branch = {
    id: administrationFixtureIds.mainBranchId,
    code: 'MAIN',
    name: 'Main clinic',
    timeZoneId: 'Asia/Dhaka',
    addressLine: '12 Gulshan Avenue, Dhaka',
    phoneNumber: '+880212345678',
    email: 'main@purrcare.test',
    isActive: true,
    weeklyHours: [
      { dayOfWeek: 'Monday', opens: '09:00', closes: '13:00' },
      { dayOfWeek: 'Monday', opens: '14:00', closes: '18:00' },
      { dayOfWeek: 'Tuesday', opens: '09:00', closes: '13:00' },
      { dayOfWeek: 'Tuesday', opens: '14:00', closes: '18:00' },
      { dayOfWeek: 'Wednesday', opens: '09:00', closes: '13:00' },
      { dayOfWeek: 'Wednesday', opens: '14:00', closes: '18:00' },
      { dayOfWeek: 'Thursday', opens: '09:00', closes: '13:00' },
      { dayOfWeek: 'Thursday', opens: '14:00', closes: '18:00' },
      { dayOfWeek: 'Friday', opens: '09:00', closes: '13:00' },
      { dayOfWeek: 'Saturday', opens: '10:00', closes: '14:00' },
    ],
  };
  const london: Branch = {
    id: administrationFixtureIds.londonBranchId,
    code: 'LDN',
    name: 'London clinic',
    timeZoneId: 'Europe/London',
    addressLine: '5 Baker Street, London',
    phoneNumber: '+442071234567',
    email: 'london@purrcare.test',
    isActive: true,
    weeklyHours: [
      { dayOfWeek: 'Monday', opens: '08:00', closes: '17:00' },
      { dayOfWeek: 'Tuesday', opens: '08:00', closes: '17:00' },
      { dayOfWeek: 'Wednesday', opens: '08:00', closes: '17:00' },
      { dayOfWeek: 'Thursday', opens: '08:00', closes: '17:00' },
      { dayOfWeek: 'Friday', opens: '08:00', closes: '17:00' },
    ],
  };
  return new Map([
    [main.id, main],
    [london.id, london],
  ]);
}

function seedClosures(): Map<string, Closure> {
  const holiday: Closure = {
    id: administrationFixtureIds.closurePublicHolidayId,
    branchId: administrationFixtureIds.mainBranchId,
    startsOn: '2027-01-01',
    endsOn: '2027-01-01',
    kind: 'PublicHoliday',
    reason: "New Year's Day",
    partialFrom: null,
    partialTo: null,
    isCancelled: false,
  };
  const halfDay: Closure = {
    id: administrationFixtureIds.closureHalfDayId,
    branchId: administrationFixtureIds.mainBranchId,
    startsOn: '2027-02-15',
    endsOn: '2027-02-15',
    kind: 'Closure',
    reason: 'Staff training (half day)',
    partialFrom: '13:00',
    partialTo: '18:00',
    isCancelled: false,
  };
  return new Map([
    [holiday.id, holiday],
    [halfDay.id, halfDay],
  ]);
}

function seedRooms(): Map<string, Room> {
  const consultation: Room = {
    id: administrationFixtureIds.consultationRoomId,
    branchId: administrationFixtureIds.mainBranchId,
    name: 'Consultation 1',
    kind: 'Consultation',
    isActive: true,
  };
  const theatre: Room = {
    id: administrationFixtureIds.theatreRoomId,
    branchId: administrationFixtureIds.mainBranchId,
    name: 'Theatre 1',
    kind: 'Theatre',
    isActive: true,
  };
  return new Map([
    [consultation.id, consultation],
    [theatre.id, theatre],
  ]);
}

type StaffBranchAssignment = { branchIds: string[]; primaryBranchId: string };

function seedStaffBranchAssignments(): Map<string, StaffBranchAssignment> {
  return new Map([
    [
      administrationFixtureIds.vetStaffId,
      {
        branchIds: [administrationFixtureIds.mainBranchId],
        primaryBranchId: administrationFixtureIds.mainBranchId,
      },
    ],
    [
      administrationFixtureIds.adminStaffId,
      {
        branchIds: [administrationFixtureIds.mainBranchId, administrationFixtureIds.londonBranchId],
        primaryBranchId: administrationFixtureIds.mainBranchId,
      },
    ],
  ]);
}

interface PersistedShape {
  branches: Array<[string, Branch]>;
  closures: Array<[string, Closure]>;
  rooms: Array<[string, Room]>;
  staffBranchAssignments: Array<[string, StaffBranchAssignment]>;
}
const persisted = loadPersisted<PersistedShape>(PERSIST_KEY);

let branches = persisted ? new Map(persisted.branches) : seedBranches();
let closures = persisted ? new Map(persisted.closures) : seedClosures();
let rooms = persisted ? new Map(persisted.rooms) : seedRooms();
let staffBranchAssignments = persisted
  ? new Map(persisted.staffBranchAssignments)
  : seedStaffBranchAssignments();

function persist(): void {
  savePersisted(PERSIST_KEY, {
    branches: [...branches.entries()],
    closures: [...closures.entries()],
    rooms: [...rooms.entries()],
    staffBranchAssignments: [...staffBranchAssignments.entries()],
  });
}

export function resetAdminBranchesMockData(): void {
  branches = seedBranches();
  closures = seedClosures();
  rooms = seedRooms();
  staffBranchAssignments = seedStaffBranchAssignments();
  clearPersisted(PERSIST_KEY);
}

/** Read-only accessor for other `mocks/handlers/admin/*.ts` files (rosters.ts's availability
 *  computation needs the branch's zone and weekly hours) — within this handover's own module
 *  boundary, unlike the cross-area `accounts.ts` cookie problem this file's own doc comment
 *  explains. */
export function getBranchSnapshot(branchId: string): Branch | undefined {
  return branches.get(branchId);
}

export function getBranchClosures(branchId: string): Closure[] {
  return [...closures.values()].filter((c) => c.branchId === branchId && !c.isCancelled);
}

function branchSummary(b: Branch) {
  return { id: b.id, code: b.code, name: b.name, timeZoneId: b.timeZoneId, isActive: b.isActive };
}

export const adminBranchesHandlers: HttpHandler[] = [
  http.get('*/api/branches', () => {
    const list = [...branches.values()]
      .filter((b) => b.isActive)
      .map((b) => ({
        id: b.id,
        name: b.name,
        addressLine: b.addressLine,
        phoneNumber: b.phoneNumber,
        email: b.email,
        timeZoneId: b.timeZoneId,
        weeklyHours: b.weeklyHours,
      }));
    return HttpResponse.json(list);
  }),

  http.get('*/api/admin/branches', () =>
    HttpResponse.json([...branches.values()].map(branchSummary)),
  ),

  http.get('*/api/admin/branches/:branchId', ({ params }) => {
    const branch = branches.get(params.branchId as string);
    if (!branch) return notFound();
    return HttpResponse.json(branch);
  }),

  http.post('*/api/admin/branches', async ({ request }) => {
    const body = (await request.json()) as {
      code: string;
      name: string;
      timeZoneId: string;
      addressLine?: string | null;
      phoneNumber?: string | null;
      email?: string | null;
    };
    if (!body.code?.trim()) return validationError('Code', 'RequiredField', 'Enter a code.');
    if ([...branches.values()].some((b) => b.code === body.code)) {
      return validationError(
        'Code',
        'BranchCodeAlreadyExists',
        'A branch with this code already exists.',
      );
    }
    const id = crypto.randomUUID();
    branches.set(id, {
      id,
      code: body.code,
      name: body.name,
      timeZoneId: body.timeZoneId,
      addressLine: body.addressLine ?? null,
      phoneNumber: body.phoneNumber ?? null,
      email: body.email ?? null,
      isActive: true,
      weeklyHours: [],
    });
    persist();
    return HttpResponse.json({ branchId: id }, { status: 201 });
  }),

  http.put('*/api/admin/branches/:branchId', async ({ request, params }) => {
    const branch = branches.get(params.branchId as string);
    if (!branch) return notFound();
    const body = (await request.json()) as Partial<Branch>;
    Object.assign(branch, {
      name: body.name,
      timeZoneId: body.timeZoneId,
      addressLine: body.addressLine ?? null,
      phoneNumber: body.phoneNumber ?? null,
      email: body.email ?? null,
    });
    persist();
    return noContent();
  }),

  http.put('*/api/admin/branches/:branchId/hours', async ({ request, params }) => {
    const branch = branches.get(params.branchId as string);
    if (!branch) return notFound();
    const body = (await request.json()) as { intervals: OpeningInterval[] };
    for (const day of new Set(body.intervals.map((i) => i.dayOfWeek))) {
      const dayIntervals = body.intervals.filter((i) => i.dayOfWeek === day);
      if (dayIntervals.length > 3) {
        return validationError(
          'Intervals',
          'InvalidOpeningInterval',
          'At most 3 intervals are allowed per day.',
        );
      }
    }
    branch.weeklyHours = body.intervals;
    persist();
    return noContent();
  }),

  http.put('*/api/admin/branches/:branchId/activation', async ({ request, params }) => {
    const branch = branches.get(params.branchId as string);
    if (!branch) return notFound();
    const body = (await request.json()) as { isActive: boolean };
    if (!body.isActive) {
      const otherActive = [...branches.values()].filter((b) => b.id !== branch.id && b.isActive);
      if (otherActive.length === 0) {
        return validationError(
          'IsActive',
          'LastActiveBranchCannotBeDeactivated',
          'At least one branch must stay active.',
        );
      }
    }
    branch.isActive = body.isActive;
    persist();
    return noContent();
  }),

  http.get('*/api/admin/branches/:branchId/closures', ({ params, request }) => {
    // HO-ADM-002-FU1 §12.2 — overlap filter as `GetBranchClosuresQueryHandler`: keep a closure
    // when `endsOn >= from` and `startsOn <= to`; cancelled closures are still returned.
    const url = new URL(request.url);
    const from = url.searchParams.get('from');
    const to = url.searchParams.get('to');
    const list = [...closures.values()].filter(
      (c) =>
        c.branchId === params.branchId && (!from || c.endsOn >= from) && (!to || c.startsOn <= to),
    );
    return HttpResponse.json(list);
  }),

  http.post('*/api/admin/branches/:branchId/closures', async ({ request, params }) => {
    const body = (await request.json()) as Omit<Closure, 'id' | 'branchId' | 'isCancelled'>;
    if (body.endsOn < body.startsOn) {
      return validationError(
        'EndsOn',
        'InvalidClosure',
        'End date must be on or after the start date.',
      );
    }
    const id = crypto.randomUUID();
    closures.set(id, { ...body, id, branchId: params.branchId as string, isCancelled: false });
    persist();
    return HttpResponse.json({ closureId: id }, { status: 201 });
  }),

  http.delete('*/api/admin/branches/:branchId/closures/:closureId', ({ params }) => {
    const closure = closures.get(params.closureId as string);
    if (!closure) return notFound();
    if (closure.startsOn < '2026-01-01') {
      return validationError(
        'ClosureId',
        'PastClosureCannotBeCancelled',
        'A past closure cannot be cancelled.',
      );
    }
    closure.isCancelled = true;
    persist();
    return noContent();
  }),

  http.get('*/api/admin/branches/:branchId/rooms', ({ params }) => {
    const list = [...rooms.values()].filter((r) => r.branchId === params.branchId);
    return HttpResponse.json(list);
  }),

  http.post('*/api/admin/branches/:branchId/rooms', async ({ request, params }) => {
    const body = (await request.json()) as { name: string; kind: string };
    const branchId = params.branchId as string;
    if ([...rooms.values()].some((r) => r.branchId === branchId && r.name === body.name)) {
      return validationError(
        'Name',
        'RoomNameAlreadyExists',
        'A room with this name already exists at this branch.',
      );
    }
    const id = crypto.randomUUID();
    rooms.set(id, { id, branchId, name: body.name, kind: body.kind, isActive: true });
    persist();
    return HttpResponse.json({ roomId: id }, { status: 201 });
  }),

  http.put('*/api/admin/branches/:branchId/rooms/:roomId', async ({ request, params }) => {
    const room = rooms.get(params.roomId as string);
    if (!room) return notFound();
    const body = (await request.json()) as { name: string; kind: string; isActive: boolean };
    Object.assign(room, body);
    persist();
    return noContent();
  }),

  http.get('*/api/admin/branches/:branchId/calendar', ({ params, request }) => {
    // HO-ADM-002-FU1 §12.2 — returns what the real `BranchCalendar.OpenIntervals` returns: an
    // unknown branch is `200 []`, a range over 62 days is `400`, and the open intervals are real
    // instants (branch wall-clock converted with the branch's own zone), part-day closures
    // subtracted, sorted and merged.
    const url = new URL(request.url);
    const from = url.searchParams.get('from') ?? '2027-01-01';
    const to = url.searchParams.get('to') ?? from;
    if (dayNumber(to) - dayNumber(from) > 62) {
      return HttpResponse.json({ error: 'The range cannot exceed 62 days.' }, { status: 400 });
    }
    const branch = branches.get(params.branchId as string);
    if (!branch) return HttpResponse.json([]);
    const branchClosures = [...closures.values()].filter(
      (c) => c.branchId === branch.id && !c.isCancelled,
    );
    const intervals: Array<{ start: string; end: string }> = [];
    for (let day = dayNumber(from); day <= dayNumber(to); day += 1) {
      const dateStr = dateOfDayNumber(day);
      const onDate = branchClosures.filter((c) => dateStr >= c.startsOn && dateStr <= c.endsOn);
      if (onDate.some((c) => !c.partialFrom)) continue;
      const dayName = DAY_NAMES[new Date(day * 86_400_000).getUTCDay()];
      let open: Array<[number, number]> = branch.weeklyHours
        .filter((h) => h.dayOfWeek === dayName)
        .map((h) => [toMinutes(h.opens), toMinutes(h.closes)]);
      for (const c of onDate) {
        const cutFrom = toMinutes(c.partialFrom as string);
        const cutTo = toMinutes(c.partialTo as string);
        open = open.flatMap(([a, b]): Array<[number, number]> => {
          if (cutTo <= a || cutFrom >= b) return [[a, b]];
          const kept: Array<[number, number]> = [];
          if (a < cutFrom) kept.push([a, cutFrom]);
          if (cutTo < b) kept.push([cutTo, b]);
          return kept;
        });
      }
      for (const [a, b] of open) {
        intervals.push({
          start: zonedTimeToUtcIso(dateStr, fromMinutes(a), branch.timeZoneId),
          end: zonedTimeToUtcIso(dateStr, fromMinutes(b), branch.timeZoneId),
        });
      }
    }
    intervals.sort((x, y) => x.start.localeCompare(y.start));
    const merged: Array<{ start: string; end: string }> = [];
    for (const interval of intervals) {
      const last = merged[merged.length - 1];
      if (last && interval.start <= last.end) {
        if (interval.end > last.end) last.end = interval.end;
      } else {
        merged.push({ ...interval });
      }
    }
    return HttpResponse.json(merged);
  }),

  http.get('*/api/admin/staff/:staffAccountId/branches', ({ params }) => {
    const assignment = staffBranchAssignments.get(params.staffAccountId as string) ?? {
      branchIds: [],
      primaryBranchId: null,
    };
    return HttpResponse.json({ staffAccountId: params.staffAccountId, ...assignment });
  }),

  http.put('*/api/admin/staff/:staffAccountId/branches', async ({ request, params }) => {
    const body = (await request.json()) as { branchIds: string[]; primaryBranchId: string };
    if (body.branchIds.length === 0)
      return validationError('BranchIds', 'RequiredField', 'Select at least one branch.');
    staffBranchAssignments.set(params.staffAccountId as string, body);
    persist();
    return noContent();
  }),

  http.get('*/api/staff/branches/mine', () => {
    // No real session resolution across module boundaries (this file's own doc comment) — the
    // administrator fixture's own assignment is returned unconditionally; e2e specs that need a
    // different signed-in staff member's branches override this endpoint with `server.use(...)`.
    const assignment = staffBranchAssignments.get(administrationFixtureIds.adminStaffId) ?? {
      branchIds: [administrationFixtureIds.mainBranchId],
      primaryBranchId: administrationFixtureIds.mainBranchId,
    };
    return HttpResponse.json({
      branchIds: assignment.branchIds,
      primaryBranchId: assignment.primaryBranchId,
      activeBranchId: assignment.primaryBranchId,
      activeBranchRefusal: null,
    });
  }),
];
