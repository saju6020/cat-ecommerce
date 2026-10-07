import { http, HttpResponse, type HttpHandler } from 'msw';
import {
  administrationFixtureIds,
  validationError,
  notFound,
  noContent,
  zonedTimeToUtcIso,
  loadPersisted,
  savePersisted,
  clearPersisted,
} from './fixtures';
import { getBranchSnapshot, getBranchClosures } from './branches';
import { getApprovedLeaveForStaff } from './leave';

const PERSIST_KEY = '__purrcare_admin_rosters_mock_db__';

/** `mocks/handlers/admin/rosters.ts` — HO-ADM-002 §12, HO-ADM-001 §8.4/§8.5/§12.4. Weekly
 *  patterns (current + a scheduled next), roster exceptions, and the availability calculation. */

interface ShiftSlot {
  dayOfWeek: string;
  starts: string;
  ends: string;
  defaultRoomId: string | null;
}

interface StaffRoster {
  staffAccountId: string;
  branchId: string;
  currentPattern: ShiftSlot[];
  nextPattern: ShiftSlot[] | null;
  nextPatternEffectiveFrom: string | null;
}

interface RosterException {
  id: string;
  staffAccountId: string;
  branchId: string;
  date: string;
  kind: 'ExtraShift' | 'ShiftCancelled';
  starts: string;
  ends: string;
  reason: string;
}

const WEEKDAY_NAMES = [
  'Sunday',
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
];

function rosterKey(staffAccountId: string, branchId: string) {
  return `${staffAccountId}::${branchId}`;
}

function seedRosters(): Map<string, StaffRoster> {
  const roster: StaffRoster = {
    staffAccountId: administrationFixtureIds.vetStaffId,
    branchId: administrationFixtureIds.mainBranchId,
    currentPattern: [
      {
        dayOfWeek: 'Monday',
        starts: '09:00',
        ends: '17:00',
        defaultRoomId: administrationFixtureIds.consultationRoomId,
      },
      {
        dayOfWeek: 'Tuesday',
        starts: '09:00',
        ends: '17:00',
        defaultRoomId: administrationFixtureIds.consultationRoomId,
      },
      {
        dayOfWeek: 'Wednesday',
        starts: '09:00',
        ends: '17:00',
        defaultRoomId: administrationFixtureIds.consultationRoomId,
      },
    ],
    // A scheduled next pattern (§12's own fixture requirement) — effective a year out so it never
    // promotes itself during a test run.
    nextPattern: [
      {
        dayOfWeek: 'Monday',
        starts: '10:00',
        ends: '18:00',
        defaultRoomId: administrationFixtureIds.consultationRoomId,
      },
      {
        dayOfWeek: 'Tuesday',
        starts: '10:00',
        ends: '18:00',
        defaultRoomId: administrationFixtureIds.consultationRoomId,
      },
    ],
    nextPatternEffectiveFrom: '2028-01-04',
  };
  return new Map([[rosterKey(roster.staffAccountId, roster.branchId), roster]]);
}

function seedExceptions(): Map<string, RosterException> {
  const extra: RosterException = {
    id: 'e0000000-0000-0000-0000-00000000a001',
    staffAccountId: administrationFixtureIds.vetStaffId,
    branchId: administrationFixtureIds.mainBranchId,
    date: '2027-01-06', // a Wednesday with no base pattern slot beyond the seeded ones
    kind: 'ExtraShift',
    starts: '09:00',
    ends: '12:00',
    reason: 'Cover for colleague',
  };
  const cancelled: RosterException = {
    id: 'e0000000-0000-0000-0000-00000000a002',
    staffAccountId: administrationFixtureIds.vetStaffId,
    branchId: administrationFixtureIds.mainBranchId,
    date: '2027-01-04', // a seeded Monday shift
    kind: 'ShiftCancelled',
    starts: '09:00',
    ends: '17:00',
    reason: 'Sick on the day',
  };
  return new Map([
    [extra.id, extra],
    [cancelled.id, cancelled],
  ]);
}

interface PersistedShape {
  rosters: Array<[string, StaffRoster]>;
  exceptions: Array<[string, RosterException]>;
}
const persisted = loadPersisted<PersistedShape>(PERSIST_KEY);

let rosters = persisted ? new Map(persisted.rosters) : seedRosters();
let exceptions = persisted ? new Map(persisted.exceptions) : seedExceptions();

function persist(): void {
  savePersisted(PERSIST_KEY, {
    rosters: [...rosters.entries()],
    exceptions: [...exceptions.entries()],
  });
}

export function resetAdminRostersMockData(): void {
  rosters = seedRosters();
  exceptions = seedExceptions();
  clearPersisted(PERSIST_KEY);
}

function computeAvailability(
  branchId: string,
  staffAccountId: string,
  from: string,
  to: string,
  restrictToOpeningHours: boolean,
) {
  const branch = getBranchSnapshot(branchId);
  const roster = rosters.get(rosterKey(staffAccountId, branchId));
  if (!branch || !roster) return [];

  const branchClosures = getBranchClosures(branchId);
  const approvedLeave = getApprovedLeaveForStaff(staffAccountId, from, to);
  const dayExceptions = [...exceptions.values()].filter(
    (e) =>
      e.staffAccountId === staffAccountId &&
      e.branchId === branchId &&
      e.date >= from &&
      e.date <= to,
  );

  const results: Array<{
    staffAccountId: string;
    branchId: string;
    start: string;
    end: string;
    defaultRoomId: string | null;
    outsideOpeningHours: boolean;
  }> = [];

  let cursor = new Date(`${from}T00:00:00Z`);
  const end = new Date(`${to}T00:00:00Z`);
  while (cursor <= end) {
    const dateStr = cursor.toISOString().slice(0, 10);
    const dayName = WEEKDAY_NAMES[cursor.getUTCDay()] ?? 'Monday';
    cursor = new Date(cursor.getTime() + 86_400_000);

    if (approvedLeave.some((l) => dateStr >= l.startsOn && dateStr <= l.endsOn)) continue; // rule 5

    const closedAllDay = branchClosures.some(
      (c) => dateStr >= c.startsOn && dateStr <= c.endsOn && !c.partialFrom,
    );
    if (closedAllDay) continue; // rule 2

    const pattern =
      roster.nextPattern &&
      roster.nextPatternEffectiveFrom &&
      dateStr >= roster.nextPatternEffectiveFrom
        ? roster.nextPattern
        : roster.currentPattern;

    let slots = pattern
      .filter((s) => s.dayOfWeek === dayName)
      .map((s) => ({ starts: s.starts, ends: s.ends, defaultRoomId: s.defaultRoomId }));

    const todaysExceptions = dayExceptions.filter((e) => e.date === dateStr);
    if (todaysExceptions.some((e) => e.kind === 'ShiftCancelled')) slots = []; // rule 4, simplified
    for (const extra of todaysExceptions.filter((e) => e.kind === 'ExtraShift')) {
      slots.push({ starts: extra.starts, ends: extra.ends, defaultRoomId: null });
    }

    const openIntervals = branch.weeklyHours.filter((h) => h.dayOfWeek === dayName);
    for (const slot of slots) {
      const isOutside = !openIntervals.some((o) => slot.starts >= o.opens && slot.ends <= o.closes);
      if (restrictToOpeningHours && isOutside) continue; // rule 6
      results.push({
        staffAccountId,
        branchId,
        start: zonedTimeToUtcIso(dateStr, slot.starts, branch.timeZoneId),
        end: zonedTimeToUtcIso(dateStr, slot.ends, branch.timeZoneId),
        defaultRoomId: slot.defaultRoomId,
        outsideOpeningHours: isOutside,
      });
    }
  }
  return results;
}

export const adminRostersHandlers: HttpHandler[] = [
  http.get('*/api/admin/branches/:branchId/rosters', ({ params }) => {
    const list = [...rosters.values()].filter((r) => r.branchId === params.branchId);
    return HttpResponse.json(list);
  }),

  http.put(
    '*/api/admin/branches/:branchId/rosters/:staffAccountId/pattern',
    async ({ request, params }) => {
      const body = (await request.json()) as { slots: ShiftSlot[]; effectiveFrom?: string | null };
      for (const day of new Set(body.slots.map((s) => s.dayOfWeek))) {
        if (body.slots.filter((s) => s.dayOfWeek === day).length > 3) {
          return validationError(
            'Slots',
            'InvalidShiftSlot',
            'At most 3 shifts are allowed per day.',
          );
        }
      }
      const key = rosterKey(params.staffAccountId as string, params.branchId as string);
      const existing = rosters.get(key) ?? {
        staffAccountId: params.staffAccountId as string,
        branchId: params.branchId as string,
        currentPattern: [],
        nextPattern: null,
        nextPatternEffectiveFrom: null,
      };
      if (body.effectiveFrom) {
        existing.nextPattern = body.slots;
        existing.nextPatternEffectiveFrom = body.effectiveFrom;
      } else {
        existing.currentPattern = body.slots;
      }
      rosters.set(key, existing);
      persist();
      return noContent();
    },
  ),

  http.delete(
    '*/api/admin/branches/:branchId/rosters/:staffAccountId/next-pattern',
    ({ params }) => {
      const key = rosterKey(params.staffAccountId as string, params.branchId as string);
      const roster = rosters.get(key);
      if (!roster) return notFound();
      roster.nextPattern = null;
      roster.nextPatternEffectiveFrom = null;
      persist();
      return noContent();
    },
  ),

  http.get('*/api/admin/branches/:branchId/roster-exceptions', ({ params, request }) => {
    const url = new URL(request.url);
    const from = url.searchParams.get('from');
    const to = url.searchParams.get('to');
    let list = [...exceptions.values()].filter((e) => e.branchId === params.branchId);
    if (from) list = list.filter((e) => e.date >= from);
    if (to) list = list.filter((e) => e.date <= to);
    return HttpResponse.json(list);
  }),

  http.post('*/api/admin/branches/:branchId/roster-exceptions', async ({ request, params }) => {
    const body = (await request.json()) as Omit<RosterException, 'id' | 'branchId'>;
    if (body.ends <= body.starts)
      return validationError('Ends', 'InvalidShiftSlot', 'End time must be after start time.');
    const id = crypto.randomUUID();
    exceptions.set(id, { ...body, id, branchId: params.branchId as string });
    persist();
    return HttpResponse.json({ rosterExceptionId: id }, { status: 201 });
  }),

  http.delete('*/api/admin/branches/:branchId/roster-exceptions/:id', ({ params }) => {
    if (!exceptions.has(params.id as string)) return notFound();
    exceptions.delete(params.id as string);
    persist();
    return noContent();
  }),

  http.get('*/api/admin/branches/:branchId/availability', ({ params, request }) => {
    const url = new URL(request.url);
    const from = url.searchParams.get('from') ?? '2027-01-01';
    const to = url.searchParams.get('to') ?? from;
    const staffAccountId = url.searchParams.get('staffAccountId');
    const restrictToOpeningHours = url.searchParams.get('restrictToOpeningHours') === 'true';
    const branchId = params.branchId as string;

    const staffIds = staffAccountId
      ? [staffAccountId]
      : [...rosters.values()].filter((r) => r.branchId === branchId).map((r) => r.staffAccountId);

    const all = staffIds.flatMap((id) =>
      computeAvailability(branchId, id, from, to, restrictToOpeningHours),
    );
    return HttpResponse.json(all);
  }),
];
