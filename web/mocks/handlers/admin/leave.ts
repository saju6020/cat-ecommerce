import { http, HttpResponse, type HttpHandler } from 'msw';
import {
  administrationFixtureIds,
  validationError,
  notFound,
  noContent,
  loadPersisted,
  savePersisted,
  clearPersisted,
} from './fixtures';

const PERSIST_KEY = '__purrcare_admin_leave_mock_db__';

/** `mocks/handlers/admin/leave.ts` — HO-ADM-002 §12, HO-ADM-001 §8.4/§12.4. Self-service leave and
 *  the administrator decision queue. */

type LeaveStatus = 'Requested' | 'Approved' | 'Rejected' | 'Cancelled';

interface LeaveRequest {
  id: string;
  staffAccountId: string;
  staffFullName: string;
  startsOn: string;
  endsOn: string;
  kind: 'Annual' | 'Sick' | 'Training' | 'Other';
  note: string | null;
  status: LeaveStatus;
  decidedBy: string | null;
  decidedOn: string | null;
  decisionNote: string | null;
}

function seed(): Map<string, LeaveRequest> {
  const requested: LeaveRequest = {
    id: administrationFixtureIds.leaveRequestId,
    staffAccountId: administrationFixtureIds.vetStaffId,
    staffFullName: 'Nadia Nurse',
    startsOn: '2027-03-10',
    endsOn: '2027-03-12',
    kind: 'Annual',
    note: 'Family trip',
    status: 'Requested',
    decidedBy: null,
    decidedOn: null,
    decisionNote: null,
  };
  return new Map([[requested.id, requested]]);
}

const persisted = loadPersisted<Array<[string, LeaveRequest]>>(PERSIST_KEY);
let leaveRequests = persisted ? new Map(persisted) : seed();

function persist(): void {
  savePersisted(PERSIST_KEY, [...leaveRequests.entries()]);
}

export function resetAdminLeaveMockData(): void {
  leaveRequests = seed();
  clearPersisted(PERSIST_KEY);
}

/** Read-only accessor for `rosters.ts`'s availability computation (§8.5 rule 5 — approved leave
 *  removes the whole day). */
export function getApprovedLeaveForStaff(staffAccountId: string, from: string, to: string) {
  return [...leaveRequests.values()].filter(
    (l) =>
      l.staffAccountId === staffAccountId &&
      l.status === 'Approved' &&
      l.endsOn >= from &&
      l.startsOn <= to,
  );
}

function paged<T>(items: T[], pageNumber: number, pageSize: number) {
  const start = (pageNumber - 1) * pageSize;
  return {
    items: items.slice(start, start + pageSize),
    pageNumber,
    pageSize,
    totalCount: items.length,
  };
}

export const adminLeaveHandlers: HttpHandler[] = [
  http.get('*/api/staff/leave-requests', () => {
    const mine = [...leaveRequests.values()].filter(
      (l) => l.staffAccountId === administrationFixtureIds.vetStaffId,
    );
    return HttpResponse.json(mine);
  }),

  http.post('*/api/staff/leave-requests', async ({ request }) => {
    const body = (await request.json()) as {
      startsOn: string;
      endsOn: string;
      kind: LeaveRequest['kind'];
      note?: string | null;
      staffAccountId?: string;
    };
    if (body.endsOn < body.startsOn)
      return validationError(
        'EndsOn',
        'InvalidLeavePeriod',
        'End date must be on or after the start date.',
      );
    const overlap = [...leaveRequests.values()].some(
      (l) =>
        l.staffAccountId === (body.staffAccountId ?? administrationFixtureIds.vetStaffId) &&
        (l.status === 'Requested' || l.status === 'Approved') &&
        l.endsOn >= body.startsOn &&
        l.startsOn <= body.endsOn,
    );
    if (overlap)
      return validationError(
        'StartsOn',
        'LeaveOverlapsExistingLeave',
        'This overlaps an existing leave request.',
      );
    const id = crypto.randomUUID();
    leaveRequests.set(id, {
      id,
      staffAccountId: body.staffAccountId ?? administrationFixtureIds.vetStaffId,
      staffFullName: 'Nadia Nurse',
      startsOn: body.startsOn,
      endsOn: body.endsOn,
      kind: body.kind,
      note: body.note ?? null,
      status: 'Requested',
      decidedBy: null,
      decidedOn: null,
      decisionNote: null,
    });
    persist();
    return HttpResponse.json({ leaveRequestId: id }, { status: 201 });
  }),

  http.post('*/api/staff/leave-requests/:id/cancel', ({ params }) => {
    const leave = leaveRequests.get(params.id as string);
    if (!leave) return notFound();
    if (leave.status !== 'Requested')
      return validationError(
        'Status',
        'LeaveCannotBeCancelled',
        'Only a requested leave can be cancelled this way.',
      );
    leave.status = 'Cancelled';
    persist();
    return noContent();
  }),

  http.get('*/api/admin/leave-requests', ({ request }) => {
    const url = new URL(request.url);
    const status = url.searchParams.get('status');
    const staffAccountId = url.searchParams.get('staffAccountId');
    const from = url.searchParams.get('from');
    const to = url.searchParams.get('to');
    const pageNumber = Number(url.searchParams.get('pageNumber') ?? '1');
    const pageSize = Number(url.searchParams.get('pageSize') ?? '20');
    let list = [...leaveRequests.values()];
    if (status) list = list.filter((l) => l.status === status);
    if (staffAccountId) list = list.filter((l) => l.staffAccountId === staffAccountId);
    if (from) list = list.filter((l) => l.endsOn >= from);
    if (to) list = list.filter((l) => l.startsOn <= to);
    return HttpResponse.json(paged(list, pageNumber, pageSize));
  }),

  http.post('*/api/admin/leave-requests', async ({ request }) => {
    const body = (await request.json()) as {
      startsOn: string;
      endsOn: string;
      kind: LeaveRequest['kind'];
      note?: string | null;
      staffAccountId: string;
    };
    const id = crypto.randomUUID();
    leaveRequests.set(id, {
      id,
      staffAccountId: body.staffAccountId,
      staffFullName: 'Nadia Nurse',
      startsOn: body.startsOn,
      endsOn: body.endsOn,
      kind: body.kind,
      note: body.note ?? null,
      status: 'Requested',
      decidedBy: null,
      decidedOn: null,
      decisionNote: null,
    });
    persist();
    return HttpResponse.json({ leaveRequestId: id }, { status: 201 });
  }),

  http.post('*/api/admin/leave-requests/:id/approve', ({ params }) => {
    const leave = leaveRequests.get(params.id as string);
    if (!leave) return notFound();
    leave.status = 'Approved';
    leave.decidedBy = administrationFixtureIds.adminStaffId;
    leave.decidedOn = new Date().toISOString();
    persist();
    return noContent();
  }),

  http.post('*/api/admin/leave-requests/:id/reject', async ({ request, params }) => {
    const leave = leaveRequests.get(params.id as string);
    if (!leave) return notFound();
    const body = (await request.json().catch(() => ({}))) as { decisionNote?: string };
    leave.status = 'Rejected';
    leave.decidedBy = administrationFixtureIds.adminStaffId;
    leave.decidedOn = new Date().toISOString();
    leave.decisionNote = body.decisionNote ?? null;
    persist();
    return noContent();
  }),

  http.post('*/api/admin/leave-requests/:id/cancel', ({ params }) => {
    const leave = leaveRequests.get(params.id as string);
    if (!leave) return notFound();
    leave.status = 'Cancelled';
    persist();
    return noContent();
  }),
];
