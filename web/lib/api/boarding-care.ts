import { apiFetch } from './client';
import { normaliseIncident } from './my-boarding';
import type {
  CareTask,
  CatMood,
  IncidentDetail,
  IncidentKind,
  IncidentSeverity,
  IncidentStatus,
  LitterOutput,
  ObservationList,
  PortionEaten,
  StayMedia,
  TaskDayList,
  WaterIntake,
} from './types';

/**
 * `lib/api/boarding-care.ts` — HO-BRD-002 §8.2: the caretaker's endpoints (task day list, complete,
 * skip, observations, media references, incidents; HO-BRD-001 §12.4). `completeTask` sends an empty
 * body: the tablet's one tap is the whole request (NFR-USA-3, ADR-FE-100). Overdue and "not eating"
 * arrive computed (ADR-FE-105).
 */

const BASE = '/api/staff/boarding';
const enc = encodeURIComponent;

export interface TaskDayParams {
  date?: string;
  unitId?: string;
  assignedTo?: 'me';
  status?: 'Due' | 'Overdue' | 'Done';
}

export async function getTaskDayList(params: TaskDayParams = {}): Promise<TaskDayList> {
  const search = new URLSearchParams();
  // The built read requires `date` (HO-BRD-001 1.1): the caller passes the branch's today.
  if (params.date) search.set('date', params.date);
  if (params.unitId) search.set('unitId', params.unitId);
  if (params.assignedTo) search.set('assignedTo', params.assignedTo);
  if (params.status) search.set('status', params.status);
  const suffix = search.size > 0 ? `?${search.toString()}` : '';
  const body = await apiFetch<unknown>(`${BASE}/tasks${suffix}`);
  return normaliseTaskDayList(body);
}

type WireTask = Omit<
  CareTask,
  'assignedToName' | 'assignedToMe' | 'completedByName' | 'minutesLate'
> & {
  catIsHighRisk: boolean;
  handlingNote?: string | null;
  assignedCaretakerName?: string | null;
  assignedToName?: string | null;
  assignedToMe?: boolean;
  completedByName?: string | null;
  minutesLate?: number | null;
};

interface WireTaskDayList {
  date: string;
  branchTimeZoneId: string;
  generatedOn?: string;
  units: Array<{
    unitId: string;
    code?: string;
    name?: string;
    unitCode?: string;
    unitName?: string;
    unitType?: string;
    block?: string | null;
    isIsolation?: boolean;
    isolationNote?: string | null;
    cats?: TaskDayList['units'][number]['cats'];
    tasks: WireTask[];
  }>;
}

/** HO-BRD-001 1.1 / the built day list: groups are `{ unitId, code, name, unitType, block, tasks }` and each
 *  task carries its cat's `catIsHighRisk`/`handlingNote`. The cats of a unit are read off its tasks; the
 *  built list has no per-cat not-eating flag (the dashboard and the observations carry it). */
export function normaliseTaskDayList(input: unknown): TaskDayList {
  const raw = input as WireTaskDayList;
  return {
    date: raw.date,
    branchTimeZoneId: raw.branchTimeZoneId,
    generatedOn: raw.generatedOn ?? new Date().toISOString(),
    units: raw.units.map((u) => {
      const cats =
        u.cats ??
        [...new Map(u.tasks.map((t) => [t.catId, t])).values()].map((t) => ({
          catId: t.catId,
          catName: t.catName,
          catIsHighRisk: t.catIsHighRisk,
          handlingNote: t.handlingNote ?? null,
          notEating: null,
        }));
      return {
        unitId: u.unitId,
        unitCode: u.unitCode ?? u.code ?? '',
        unitName: u.unitName ?? u.name ?? '',
        block: u.block ?? null,
        isIsolation: u.isIsolation ?? u.unitType === 'Isolation',
        isolationNote:
          u.isolationNote ??
          (u.unitType === 'Isolation'
            ? 'Isolation — gloves and gown on entry; record entry and exit.'
            : null),
        cats,
        tasks: u.tasks.map((t) => ({
          ...t,
          assignedToName: t.assignedToName ?? t.assignedCaretakerName ?? null,
          assignedToMe: t.assignedToMe ?? false,
          completedByName: t.completedByName ?? null,
          minutesLate: t.minutesLate ?? null,
        })) as CareTask[],
      };
    }),
  };
}

/** `signal` lets the card's **Undo** abandon the request while it is still in flight (ADR-FE-100). */
export async function completeTask(taskId: string, signal?: AbortSignal): Promise<void> {
  await apiFetch<void>(`${BASE}/tasks/${enc(taskId)}/complete`, { method: 'POST', signal });
}

export async function skipTask(taskId: string, reason: string): Promise<void> {
  await apiFetch<void>(`${BASE}/tasks/${enc(taskId)}/skip`, { method: 'POST', body: { reason } });
}

export interface ObservationInput {
  catId: string;
  portionEaten: PortionEaten;
  waterIntake: WaterIntake;
  litterOutput: LitterOutput;
  mood: CatMood;
  notes: string | null;
}

export async function recordObservation(stayId: string, input: ObservationInput): Promise<void> {
  await apiFetch<void>(`${BASE}/stays/${enc(stayId)}/observations`, {
    method: 'POST',
    body: input,
  });
}

interface WireObservations {
  stayId?: string;
  notEating?: string[];
  items: Array<ObservationList['items'][number] & { recordedBy?: string }>;
  cats?: ObservationList['cats'];
}

/** The built observations answer lists `notEating` as cat ids beside the items (HO-BRD-001 1.1). */
export async function getStayObservations(stayId: string): Promise<ObservationList> {
  const body = await apiFetch<WireObservations>(`${BASE}/stays/${enc(stayId)}/observations`);
  const flagged = new Set(body.notEating ?? []);
  const cats =
    body.cats ??
    [...new Map(body.items.map((o) => [o.catId, o.catName])).entries()].map(([catId, catName]) => ({
      catId,
      catName,
      notEating: flagged.has(catId),
    }));
  return { items: body.items.map((o) => ({ ...o, recordedByName: o.recordedByName ?? '' })), cats };
}

export interface MediaInput {
  /** The id returned by HO-FIL-002's uploader (purpose `BoardingMedia`). */
  fileId: string;
  catId: string | null;
  incidentId: string | null;
  caption: string | null;
  capturedOnLocalDate: string;
}

export async function recordStayMedia(stayId: string, input: MediaInput): Promise<void> {
  await apiFetch<void>(`${BASE}/stays/${enc(stayId)}/media`, { method: 'POST', body: input });
}

export async function getStayMedia(stayId: string): Promise<StayMedia[]> {
  const rows = await apiFetch<Array<Record<string, unknown>>>(`${BASE}/stays/${enc(stayId)}/media`);
  return rows.map((m) => ({
    ...m,
    reference: (m.reference as string | null | undefined) ?? null,
    uploadedByName: String(m.uploadedByName ?? ''),
  })) as unknown as StayMedia[];
}

export async function hideStayMedia(
  stayId: string,
  mediaId: string,
  reason: string,
): Promise<void> {
  await apiFetch<void>(`${BASE}/stays/${enc(stayId)}/media/${enc(mediaId)}/hide`, {
    method: 'POST',
    body: { reason },
  });
}

export interface IncidentInput {
  catId: string;
  kind: IncidentKind;
  severity: IncidentSeverity;
  description: string;
  immediateAction: string;
  /** Instant the incident happened (required by the built endpoint); defaults to now. */
  occurredOn?: string;
}

export async function recordIncident(
  stayId: string,
  input: IncidentInput,
): Promise<{ incidentId: string }> {
  return apiFetch<{ incidentId: string }>(`${BASE}/stays/${enc(stayId)}/incidents`, {
    method: 'POST',
    body: { ...input, occurredOn: input.occurredOn ?? new Date().toISOString() },
  });
}

/** The built queue is `GET incidents?status&stayId` (there is no per-stay incidents route). */
export async function getStayIncidents(stayId: string): Promise<IncidentDetail[]> {
  const rows = await apiFetch<unknown[]>(`${BASE}/incidents?stayId=${enc(stayId)}`);
  return rows.map((r) => normaliseIncident(r));
}

export async function getIncidentQueue(status?: IncidentStatus): Promise<IncidentDetail[]> {
  const suffix = status ? `?status=${enc(status)}` : '';
  const rows = await apiFetch<unknown[]>(`${BASE}/incidents${suffix}`);
  return rows.map((r) => normaliseIncident(r));
}

export async function recordIncidentAcknowledgement(incidentId: string): Promise<void> {
  await apiFetch<void>(`${BASE}/incidents/${enc(incidentId)}/acknowledgement`, { method: 'POST' });
}

export async function recordIncidentPhoneCall(
  incidentId: string,
  outcome: string,
  ownerReached: boolean,
): Promise<void> {
  await apiFetch<void>(`${BASE}/incidents/${enc(incidentId)}/phone-call`, {
    method: 'POST',
    body: { outcome, ownerReached },
  });
}

export async function resolveIncident(incidentId: string, resolution: string): Promise<void> {
  await apiFetch<void>(`${BASE}/incidents/${enc(incidentId)}/resolve`, {
    method: 'POST',
    body: { resolution },
  });
}
