import { apiFetch } from './client';
import type { PagedResult } from './types';
import type { ReportRelativePeriod } from './reports';

/**
 * `lib/api/report-schedules.ts` — HO-RPT-002 §8.2, US-RPT-06. Shapes verified against
 * `AdminReportSchedulesController`, `ReportScheduleRequests.cs` and `ReportScheduleViewModels.cs`
 * (HO-RPT-001 §12.4). Enumerations travel by name; `sendAtLocal` is `HH:mm`, branch-local. A
 * schedule's next runs, covered period and quiet-hours hold come from `…/preview` (ADR-FE-136):
 * the client never computes a recurrence.
 */
export type ReportScheduleStatus = 'Active' | 'Paused' | 'Cancelled';
export type ReportFrequency = 'Daily' | 'Weekly' | 'Monthly';
export type ReportScheduleFormat = 'Csv' | 'Pdf';
export type ScheduledRunStatus = 'Generated' | 'Failed' | 'Purged';
export type ReportRunTrigger = 'Schedule' | 'Manual';

export const SCHEDULE_STATUSES: readonly ReportScheduleStatus[] = ['Active', 'Paused', 'Cancelled'];
export const REPORT_FREQUENCIES: readonly ReportFrequency[] = ['Daily', 'Weekly', 'Monthly'];
export const SCHEDULE_FORMATS: readonly ReportScheduleFormat[] = ['Csv', 'Pdf'];
export const SCHEDULE_PAGE_SIZE = 20;
export const RUN_PAGE_SIZE = 20;
/** `DayOfWeek` names the server parses. */
export const DAYS_OF_WEEK = [
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
  'Sunday',
] as const;

export interface ReportScheduleListItem {
  id: string;
  name: string;
  reportKey: string;
  reportTitle: string;
  branchName: string;
  frequencyLabel: string;
  sendAtLocal: string;
  timeZoneId: string;
  format: ReportScheduleFormat | (string & {});
  recipientCount: number;
  status: ReportScheduleStatus | (string & {});
  nextRunOn: string | null;
  lastRunOn: string | null;
  lastRunStatus: ScheduledRunStatus | (string & {}) | null;
}

export interface ReportScheduleRecipient {
  id: string;
  name: string;
  emailPresent: boolean;
  stillPermitted: boolean;
}

export interface ReportScheduleDetail {
  id: string;
  version: number;
  name: string;
  reportKey: string;
  reportTitle: string;
  branchId: string | null;
  branchName: string;
  period: ReportRelativePeriod | (string & {});
  parameters: Record<string, string>;
  format: ReportScheduleFormat | (string & {});
  frequency: ReportFrequency | (string & {});
  dayOfWeek: string | null;
  dayOfMonth: number | null;
  lastDayOfMonth: boolean;
  frequencyLabel: string;
  sendAtLocal: string;
  timeZoneId: string;
  recipients: ReportScheduleRecipient[];
  status: ReportScheduleStatus | (string & {});
  nextRunOn: string | null;
  lastRunOn: string | null;
}

export interface ReportScheduleDelivery {
  recipientName: string;
  status: string;
}

export interface ReportScheduleRun {
  runId: string;
  dueOn: string;
  trigger: ReportRunTrigger | (string & {});
  status: ScheduledRunStatus | (string & {});
  periodFrom: string;
  periodTo: string;
  format: ReportScheduleFormat | (string & {});
  rowCount: number;
  contentLength: number;
  attachmentOmitted: boolean;
  failureCode: string | null;
  generatedOn: string;
  purgedOn: string | null;
  deliveries: ReportScheduleDelivery[];
}

export interface EligibleRecipient {
  id: string;
  name: string;
  roles: string[];
  emailPresent: boolean;
}

/** The body of create and update (`expectedVersion` is required on update). */
export interface ReportScheduleInput {
  name: string;
  reportKey: string;
  branchId: string | null;
  period: string;
  parameters: Record<string, string>;
  format: ReportScheduleFormat;
  frequency: ReportFrequency;
  dayOfWeek: string | null;
  dayOfMonth: number | null;
  lastDayOfMonth: boolean;
  sendAtLocal: string;
  recipientIds: string[];
  expectedVersion?: number;
}

/** The body of the preview: a draft schedule — nothing is saved. */
export interface SchedulePreviewInput {
  reportKey: string;
  branchId: string | null;
  period: string;
  frequency: ReportFrequency;
  dayOfWeek: string | null;
  dayOfMonth: number | null;
  lastDayOfMonth: boolean;
  sendAtLocal: string;
}

export interface SchedulePreview {
  nextRuns: string[];
  periodForNextRun: { from: string; to: string };
  deliveryHeldUntil?: string | null;
}

export interface RunNowResult {
  runId: string;
  status: ScheduledRunStatus | (string & {});
  rowCount: number;
  failureCode: string | null;
}

export interface ScheduleListQuery {
  status?: string;
  reportKey?: string;
  pageNumber?: number;
  pageSize?: number;
}

const BASE = '/api/admin/report-schedules';

function qs(values: Record<string, string | number | undefined>): string {
  const params = new URLSearchParams();
  for (const [name, value] of Object.entries(values)) {
    if (value !== undefined && value !== '') params.set(name, String(value));
  }
  const text = params.toString();
  return text ? `?${text}` : '';
}

export function listSchedules(
  query: ScheduleListQuery,
): Promise<PagedResult<ReportScheduleListItem>> {
  return apiFetch(`${BASE}${qs({ ...query })}`);
}

export function getSchedule(id: string): Promise<ReportScheduleDetail> {
  return apiFetch(`${BASE}/${encodeURIComponent(id)}`);
}

export function createSchedule(
  input: ReportScheduleInput,
): Promise<{ scheduleId: string; nextRunOn: string | null }> {
  return apiFetch(BASE, { method: 'POST', body: input });
}

export function updateSchedule(
  id: string,
  input: ReportScheduleInput,
): Promise<{ nextRunOn: string | null }> {
  return apiFetch(`${BASE}/${encodeURIComponent(id)}`, { method: 'PUT', body: input });
}

export function transitionSchedule(
  id: string,
  action: 'pause' | 'resume' | 'cancel',
): Promise<void> {
  return apiFetch(`${BASE}/${encodeURIComponent(id)}/${action}`, { method: 'POST' });
}

export function runScheduleNow(id: string): Promise<RunNowResult> {
  return apiFetch(`${BASE}/${encodeURIComponent(id)}/run-now`, { method: 'POST' });
}

export function previewSchedule(input: SchedulePreviewInput): Promise<SchedulePreview> {
  return apiFetch(`${BASE}/preview`, { method: 'POST', body: input });
}

export function listScheduleRuns(
  id: string,
  pageNumber: number,
): Promise<PagedResult<ReportScheduleRun>> {
  return apiFetch(
    `${BASE}/${encodeURIComponent(id)}/runs${qs({ pageNumber, pageSize: RUN_PAGE_SIZE })}`,
  );
}

export function listEligibleRecipients(
  reportKey: string,
  branchId: string | null,
): Promise<EligibleRecipient[]> {
  return apiFetch(`${BASE}/recipients${qs({ reportKey, branchId: branchId ?? undefined })}`);
}
