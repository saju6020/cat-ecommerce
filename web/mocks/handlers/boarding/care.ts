import { http, type HttpHandler } from 'msw';
import type { IncidentKind, IncidentSeverity, PortionEaten } from '@/lib/api/types';
import { bad } from './actions';
import { boardingFixtureIds as ids, json, noContent, notFound } from './fixtures';
import {
  branchIdOf,
  findStay,
  getDb,
  nextId,
  nowIso,
  notEatingCats,
  persist,
  staysOfBranch,
  sweepLapses,
  taskDayList,
  todayOf,
  toIncidentDetail,
} from './store';

/**
 * `mocks/handlers/boarding/care.ts` — HO-BRD-002 §12: the caretaker's endpoints (HO-BRD-001 §12.4):
 * the task day list, one-tap completion, skip, observations, media references and incidents.
 */

const BASE = '*/api/staff/boarding';
const me = 'Tanvir Islam';

export const boardingCareHandlers: HttpHandler[] = [
  // GET …/tasks?date&unitId&assignedTo=me&status
  http.get(`${BASE}/tasks`, ({ request }) => {
    const url = new URL(request.url);
    const branchId = branchIdOf(request);
    return json(
      taskDayList(branchId, url.searchParams.get('date') || todayOf(branchId), {
        unitId: url.searchParams.get('unitId'),
        mine: url.searchParams.get('assignedTo') === 'me',
        status: url.searchParams.get('status'),
      }),
    );
  }),

  // POST …/tasks/{id}/complete — C15; an empty body is the whole request (NFR-USA-3)
  http.post(`${BASE}/tasks/:taskId/complete`, ({ params }) => {
    const task = getDb().tasks.find((t) => t.id === params.taskId);
    if (!task) return bad('CareTaskNotFound', 'TaskId', 'This task could not be found.').response;
    if (task.id === ids.taskFailsToComplete) {
      return bad(
        'CareTaskNotPending',
        'TaskId',
        'This task was already completed by Rafiq Uddin at 10:32. Your tap was undone.',
      ).response;
    }
    if (task.status === 'Done') return noContent(); // a double tap is harmless
    if (task.status !== 'Pending') {
      return bad('CareTaskNotPending', 'TaskId', 'This task has already been dealt with.').response;
    }
    task.status = 'Done';
    task.completedOn = nowIso();
    task.completedByName = me;
    persist();
    return noContent();
  }),
  http.post(`${BASE}/tasks/:taskId/skip`, async ({ request, params }) => {
    const task = getDb().tasks.find((t) => t.id === params.taskId);
    if (!task) return bad('CareTaskNotFound', 'TaskId', 'This task could not be found.').response;
    const body = (await request.json()) as { reason: string };
    if (!body.reason?.trim())
      return bad('CancellationReasonRequired', 'Reason', 'Enter a reason for skipping.').response;
    if (task.status !== 'Pending') {
      return bad('CareTaskNotPending', 'TaskId', 'This task has already been dealt with.').response;
    }
    task.status = 'Skipped';
    task.skipReason = body.reason.trim();
    task.completedOn = nowIso();
    task.completedByName = me;
    persist();
    return noContent();
  }),

  // POST / GET …/stays/{id}/observations — C16
  http.post(`${BASE}/stays/:stayId/observations`, async ({ request, params }) => {
    const stay = findStay(params.stayId as string);
    if (!stay) return notFound();
    const body = (await request.json()) as {
      catId: string;
      portionEaten: PortionEaten;
      waterIntake: 'None' | 'Low' | 'Normal' | 'High';
      litterOutput: 'Normal' | 'None' | 'Abnormal';
      mood: 'Relaxed' | 'Playful' | 'Anxious' | 'Withdrawn' | 'Aggressive';
      notes: string | null;
    };
    const cat = stay.cats.find((c) => c.catId === body.catId);
    if (stay.status !== 'CheckedIn' || !cat) {
      return bad(
        'ObservationNotAllowed',
        'CatId',
        'An observation can’t be recorded for this cat right now.',
      ).response;
    }
    getDb().observations.push({
      id: nextId('a9'),
      stayId: stay.id,
      catId: cat.catId,
      catName: cat.catName,
      recordedOn: nowIso(),
      portionEaten: body.portionEaten,
      waterIntake: body.waterIntake,
      litterOutput: body.litterOutput,
      mood: body.mood,
      notes: body.notes?.trim() || null,
      recordedByName: me,
    });
    persist();
    return json({ ok: true }, 201);
  }),
  http.get(`${BASE}/stays/:stayId/observations`, ({ params }) => {
    const stay = findStay(params.stayId as string);
    if (!stay) return notFound();
    const flagged = notEatingCats(stay.id);
    return json({
      items: getDb()
        .observations.filter((o) => o.stayId === stay.id)
        .sort((a, b) => b.recordedOn.localeCompare(a.recordedOn))
        .map((o) => ({ ...o })),
      cats: stay.cats.map((c) => ({
        catId: c.catId,
        catName: c.catName,
        notEating: flagged.has(c.catId),
      })),
    });
  }),

  // POST / GET …/stays/{id}/media · POST …/media/{id}/hide — C18 (references; nothing uploads)
  http.post(`${BASE}/stays/:stayId/media`, async ({ request, params }) => {
    const stay = findStay(params.stayId as string);
    if (!stay) return notFound();
    if (!['CheckedIn', 'CheckedOut'].includes(stay.status)) {
      return bad(
        'MediaStayNotInHouse',
        'StayId',
        'Media can only be recorded for a cat that is in the care centre.',
      ).response;
    }
    const body = (await request.json()) as {
      fileId: string;
      catId: string | null;
      incidentId: string | null;
      caption: string | null;
      capturedOnLocalDate: string;
    };
    if (!body.fileId?.trim()) {
      return bad('InvalidMediaReference', 'FileId', 'Upload a photo first.').response;
    }
    const id = nextId('b2');
    getDb().media.push({
      id,
      stayId: stay.id,
      catId: body.catId,
      incidentId: body.incidentId,
      caption: body.caption,
      capturedOnLocalDate: body.capturedOnLocalDate,
      reference: body.fileId,
      status: 'Visible',
      uploadedByName: me,
    });
    persist();
    return json({ mediaId: id }, 201);
  }),
  http.get(`${BASE}/stays/:stayId/media`, ({ params }) => {
    const stay = findStay(params.stayId as string);
    return stay ? json(getDb().media.filter((m) => m.stayId === stay.id)) : notFound();
  }),
  http.post(`${BASE}/stays/:stayId/media/:mediaId/hide`, ({ params }) => {
    const media = getDb().media.find((m) => m.id === params.mediaId && m.stayId === params.stayId);
    if (!media) return notFound();
    media.status = 'Hidden';
    persist();
    return noContent();
  }),

  // POST / GET …/stays/{id}/incidents · GET …/incidents?status — C19
  http.post(`${BASE}/stays/:stayId/incidents`, async ({ request, params }) => {
    const stay = findStay(params.stayId as string);
    if (!stay) return notFound();
    if (stay.status !== 'CheckedIn') {
      return bad(
        'IncidentStayNotInHouse',
        'StayId',
        'The cat is not in the care centre, so an incident cannot be recorded.',
      ).response;
    }
    const body = (await request.json()) as {
      catId: string;
      kind: IncidentKind;
      severity: IncidentSeverity;
      description: string;
      immediateAction: string;
      occurredOn?: string;
    };
    if (!stay.cats.some((c) => c.catId === body.catId)) return notFound();
    const id = nextId('f1');
    getDb().incidents.push({
      id,
      stayId: stay.id,
      catId: body.catId,
      kind: body.kind,
      severity: body.severity,
      description: body.description,
      immediateAction: body.immediateAction,
      occurredOn: body.occurredOn ?? nowIso(),
      reportedOn: nowIso(),
      reportedByName: me,
      ownerNotified: body.severity !== 'Low',
      ownerAcknowledgedOn: null,
      acknowledgedVia: null,
      escalatedOn: null,
      phoneCallOn: null,
      phoneCallOutcome: null,
      resolvedOn: null,
      resolution: null,
    });
    persist();
    return json({ incidentId: id }, 201);
  }),
  http.get(`${BASE}/stays/:stayId/incidents`, ({ params }) => {
    const stay = findStay(params.stayId as string);
    if (!stay) return notFound();
    return json(
      getDb()
        .incidents.filter((i) => i.stayId === stay.id)
        .map((i) => toIncidentDetail(i, true)),
    );
  }),
  http.get(`${BASE}/incidents`, ({ request }) => {
    sweepLapses();
    const status = new URL(request.url).searchParams.get('status');
    const stayFilter = new URL(request.url).searchParams.get('stayId');
    const branchStayIds = new Set(staysOfBranch(branchIdOf(request)).map((s) => s.id));
    const rank = { High: 0, Medium: 1, Low: 2 } as const;
    return json(
      getDb()
        .incidents.filter((i) => branchStayIds.has(i.stayId))
        .filter((i) => !stayFilter || i.stayId === stayFilter)
        .filter((i) =>
          status === 'Open' ? !i.resolvedOn : status === 'Resolved' ? !!i.resolvedOn : true,
        )
        .sort(
          (a, b) =>
            Number(!!a.resolvedOn) - Number(!!b.resolvedOn) ||
            rank[a.severity] - rank[b.severity] ||
            b.reportedOn.localeCompare(a.reportedOn),
        )
        .map((i) => toIncidentDetail(i, true)),
    );
  }),
  http.post(`${BASE}/incidents/:incidentId/acknowledgement`, ({ params }) => {
    const incident = getDb().incidents.find((i) => i.id === params.incidentId);
    if (!incident) return notFound();
    incident.ownerAcknowledgedOn ??= nowIso();
    incident.acknowledgedVia ??= 'Staff';
    persist();
    return noContent();
  }),
  http.post(`${BASE}/incidents/:incidentId/phone-call`, async ({ request, params }) => {
    const incident = getDb().incidents.find((i) => i.id === params.incidentId);
    if (!incident) return notFound();
    const body = (await request.json()) as { outcome: string; ownerReached: boolean };
    if (!body.outcome?.trim()) {
      return bad('PhoneCallOutcomeRequired', 'Outcome', 'Enter what happened on the call.')
        .response;
    }
    incident.phoneCallOn = nowIso();
    incident.phoneCallOutcome = body.outcome.trim();
    if (body.ownerReached) {
      incident.ownerAcknowledgedOn ??= nowIso();
      incident.acknowledgedVia ??= 'Staff';
    }
    persist();
    return noContent();
  }),
  http.post(`${BASE}/incidents/:incidentId/resolve`, async ({ request, params }) => {
    const incident = getDb().incidents.find((i) => i.id === params.incidentId);
    if (!incident) return notFound();
    const body = (await request.json()) as { resolution: string };
    if (!body.resolution?.trim()) {
      return bad('CancellationReasonRequired', 'Resolution', 'Enter how this was resolved.')
        .response;
    }
    incident.resolvedOn = nowIso();
    incident.resolution = body.resolution.trim();
    persist();
    return noContent();
  }),
];
