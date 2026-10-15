import { http, HttpResponse, type HttpHandler } from 'msw';
import type {
  EligibleRecipient,
  ReportScheduleDetail,
  ReportScheduleInput,
  ReportScheduleListItem,
  ReportScheduleRun,
  SchedulePreviewInput,
} from '@/lib/api/report-schedules';
import { BRANCHES, findEntry, forbidden, reportFixtureIds, rolesOf, violation } from './fixtures';

const RECIPIENTS: EligibleRecipient[] = [
  {
    id: 'p0000000-0000-0000-0000-000000000001',
    name: 'Rezwan Ahmed',
    roles: ['Administrator'],
    emailPresent: true,
  },
  {
    id: 'p0000000-0000-0000-0000-000000000002',
    name: 'Mahmud Hasan',
    roles: ['Accountant'],
    emailPresent: true,
  },
  {
    id: 'p0000000-0000-0000-0000-000000000003',
    name: 'Sabina Yasmin',
    roles: ['Accountant'],
    emailPresent: false,
  },
];

interface StoredSchedule {
  detail: ReportScheduleDetail;
  runs: ReportScheduleRun[];
}

const store = new Map<string, StoredSchedule>();
let counter = 0;

const zoneOf = (branchId: string | null) =>
  branchId ? (BRANCHES[branchId]?.timeZoneId ?? 'Asia/Dhaka') : 'Asia/Dhaka';

function frequencyLabel(input: ReportScheduleInput): string {
  if (input.frequency === 'Weekly') return `Weekly on ${input.dayOfWeek}`;
  if (input.frequency === 'Monthly') {
    return input.lastDayOfMonth ? 'Monthly on the last day' : `Monthly on day ${input.dayOfMonth}`;
  }
  return 'Daily';
}

function nextRunFor(sendAt: string, zone: string): string {
  const offset = zone === 'Europe/London' ? '+01:00' : '+06:00';
  return `2026-10-15T${sendAt}:00${offset}`;
}

function seed(): void {
  store.clear();
  counter = 0;
  const make = (id: string, status: string, name: string, runs: ReportScheduleRun[] = []) => {
    const detail: ReportScheduleDetail = {
      id,
      version: 1,
      name,
      reportKey: 'revenue-by-business-line',
      reportTitle: 'Revenue by business line',
      branchId: null,
      branchName: 'All branches',
      period: 'PreviousMonth',
      parameters: {},
      format: 'Pdf',
      frequency: 'Monthly',
      dayOfWeek: null,
      dayOfMonth: 1,
      lastDayOfMonth: false,
      frequencyLabel: 'Monthly on day 1',
      sendAtLocal: '07:00',
      timeZoneId: 'Asia/Dhaka',
      recipients: RECIPIENTS.slice(0, 2).map((r) => ({
        id: r.id,
        name: r.name,
        emailPresent: true,
        stillPermitted: true,
      })),
      status,
      nextRunOn: status === 'Active' ? '2026-11-01T07:00:00+06:00' : null,
      lastRunOn: runs[0]?.generatedOn ?? null,
    };
    store.set(id, { detail, runs });
  };
  make('sc000000-0000-0000-0000-000000000001', 'Active', 'Monthly revenue by line', [
    {
      runId: 'ru000000-0000-0000-0000-000000000001',
      dueOn: '2026-10-01T07:00:00+06:00',
      trigger: 'Schedule',
      status: 'Generated',
      periodFrom: '2026-09-01',
      periodTo: '2026-09-30',
      format: 'Pdf',
      rowCount: 412,
      contentLength: 421888,
      attachmentOmitted: false,
      failureCode: null,
      generatedOn: '2026-10-01T07:00:20+06:00',
      purgedOn: null,
      deliveries: [
        { recipientName: 'Rezwan Ahmed', status: 'Sent' },
        { recipientName: 'Mahmud Hasan', status: 'Queued' },
      ],
    },
    {
      runId: 'ru000000-0000-0000-0000-000000000002',
      dueOn: '2026-09-01T07:00:00+06:00',
      trigger: 'Schedule',
      status: 'Failed',
      periodFrom: '2026-08-01',
      periodTo: '2026-08-31',
      format: 'Pdf',
      rowCount: 0,
      contentLength: 0,
      attachmentOmitted: false,
      failureCode: 'ReportTooLargeForPdf',
      generatedOn: '2026-09-01T07:00:12+06:00',
      purgedOn: null,
      deliveries: [],
    },
    {
      runId: 'ru000000-0000-0000-0000-000000000003',
      dueOn: '2026-08-01T07:00:00+06:00',
      trigger: 'Manual',
      status: 'Purged',
      periodFrom: '2026-07-01',
      periodTo: '2026-07-31',
      format: 'Csv',
      rowCount: 380,
      contentLength: 9_400_000,
      attachmentOmitted: true,
      failureCode: null,
      generatedOn: '2026-08-01T07:00:30+06:00',
      purgedOn: '2026-08-08T07:00:00+06:00',
      deliveries: [{ recipientName: 'Rezwan Ahmed', status: 'Sent' }],
    },
  ]);
  make('sc000000-0000-0000-0000-000000000002', 'Paused', 'Weekly stock expiry');
  make('sc000000-0000-0000-0000-000000000003', 'Cancelled', 'Old daily schedule');
}
seed();

export function resetSchedulesMock(): void {
  seed();
}

function listItem(s: StoredSchedule): ReportScheduleListItem {
  const d = s.detail;
  return {
    id: d.id,
    name: d.name,
    reportKey: d.reportKey,
    reportTitle: d.reportTitle,
    branchName: d.branchName,
    frequencyLabel: d.frequencyLabel,
    sendAtLocal: d.sendAtLocal,
    timeZoneId: d.timeZoneId,
    format: d.format,
    recipientCount: d.recipients.length,
    status: d.status,
    nextRunOn: d.nextRunOn,
    lastRunOn: d.lastRunOn,
    lastRunStatus: s.runs[0]?.status ?? null,
  };
}

function notAdmin(request: Request): boolean {
  return !rolesOf(request).includes('Administrator');
}

function checkInput(input: ReportScheduleInput) {
  const entry = findEntry(input.reportKey);
  if (!entry) return violation(404, 'UnknownReport', 'This report does not exist.', 'reportKey');
  const picked = RECIPIENTS.filter((r) => input.recipientIds.includes(r.id));
  if (input.recipientIds.length === 0) {
    return violation(
      400,
      'ReportRecipientRequired',
      'Choose at least one recipient.',
      'recipientIds',
    );
  }
  if (picked.some((r) => !r.emailPresent)) {
    return violation(
      400,
      'ReportRecipientHasNoEmail',
      'This person has no email address, so they cannot receive a scheduled report. Choose someone else or add an email address to their account.',
      'recipientIds',
    );
  }
  if (input.name.includes('refuse-recipient')) {
    return violation(
      400,
      'ReportRecipientNotPermitted',
      'This person is not allowed to open this report, so they cannot be sent it. Choose someone who can.',
      'recipientIds',
    );
  }
  return null;
}

export const scheduleHandlers: HttpHandler[] = [
  http.get('*/api/admin/report-schedules/recipients', ({ request }) => {
    if (notAdmin(request)) return forbidden();
    return HttpResponse.json(RECIPIENTS);
  }),

  http.post('*/api/admin/report-schedules/preview', async ({ request }) => {
    if (notAdmin(request)) return forbidden();
    const input = (await request.json()) as SchedulePreviewInput;
    const zone = zoneOf(input.branchId);
    const hour = Number(input.sendAtLocal.slice(0, 2));
    const offset = zone === 'Europe/London' ? '+01:00' : '+06:00';
    return HttpResponse.json({
      nextRuns: ['2026-10-15', '2026-10-16', '2026-10-17'].map(
        (d) => `${d}T${input.sendAtLocal}:00${offset}`,
      ),
      periodForNextRun: { from: '2026-09-01', to: '2026-09-30' },
      deliveryHeldUntil: hour >= 22 || hour < 7 ? `2026-10-16T08:00:00${offset}` : null,
    });
  }),

  http.get('*/api/admin/report-schedules', ({ request }) => {
    if (notAdmin(request)) return forbidden();
    const url = new URL(request.url);
    const status = url.searchParams.get('status');
    const reportKey = url.searchParams.get('reportKey');
    const items = [...store.values()]
      .filter(
        (s) =>
          (!status || s.detail.status === status) &&
          (!reportKey || s.detail.reportKey === reportKey),
      )
      .map(listItem);
    return HttpResponse.json({ items, pageNumber: 1, pageSize: 20, totalCount: items.length });
  }),

  http.post('*/api/admin/report-schedules', async ({ request }) => {
    if (notAdmin(request)) return forbidden();
    const input = (await request.json()) as ReportScheduleInput;
    const refused = checkInput(input);
    if (refused) return refused;
    counter += 1;
    const id = `sc000000-0000-0000-0000-0000000001${String(counter).padStart(2, '0')}`;
    const zone = zoneOf(input.branchId);
    const entry = findEntry(input.reportKey)!;
    store.set(id, {
      runs: [],
      detail: {
        id,
        version: 1,
        name: input.name,
        reportKey: input.reportKey,
        reportTitle: entry.title,
        branchId: input.branchId,
        branchName: input.branchId ? (BRANCHES[input.branchId]?.name ?? 'Branch') : 'All branches',
        period: input.period,
        parameters: input.parameters,
        format: input.format,
        frequency: input.frequency,
        dayOfWeek: input.dayOfWeek,
        dayOfMonth: input.dayOfMonth,
        lastDayOfMonth: input.lastDayOfMonth,
        frequencyLabel: frequencyLabel(input),
        sendAtLocal: input.sendAtLocal,
        timeZoneId: zone,
        recipients: RECIPIENTS.filter((r) => input.recipientIds.includes(r.id)).map((r) => ({
          id: r.id,
          name: r.name,
          emailPresent: true,
          stillPermitted: true,
        })),
        status: 'Active',
        nextRunOn: nextRunFor(input.sendAtLocal, zone),
        lastRunOn: null,
      },
    });
    return HttpResponse.json(
      { scheduleId: id, nextRunOn: nextRunFor(input.sendAtLocal, zone) },
      { status: 201 },
    );
  }),

  http.get('*/api/admin/report-schedules/:id/runs', ({ request, params }) => {
    if (notAdmin(request)) return forbidden();
    const found = store.get(String(params.id));
    if (!found) return new HttpResponse(null, { status: 404 });
    return HttpResponse.json({
      items: found.runs,
      pageNumber: 1,
      pageSize: 20,
      totalCount: found.runs.length,
    });
  }),

  http.get('*/api/admin/report-schedules/:id', ({ request, params }) => {
    if (notAdmin(request)) return forbidden();
    const found = store.get(String(params.id));
    return found ? HttpResponse.json(found.detail) : new HttpResponse(null, { status: 404 });
  }),

  http.put('*/api/admin/report-schedules/:id', async ({ request, params }) => {
    if (notAdmin(request)) return forbidden();
    const found = store.get(String(params.id));
    if (!found) return new HttpResponse(null, { status: 404 });
    const input = (await request.json()) as ReportScheduleInput;
    if (input.expectedVersion !== found.detail.version) {
      return violation(409, 'StaleVersion', 'This schedule was changed by someone else.', null);
    }
    const refused = checkInput(input);
    if (refused) return refused;
    const zone = zoneOf(input.branchId);
    found.detail = {
      ...found.detail,
      version: found.detail.version + 1,
      name: input.name,
      period: input.period,
      format: input.format,
      frequency: input.frequency,
      dayOfWeek: input.dayOfWeek,
      dayOfMonth: input.dayOfMonth,
      lastDayOfMonth: input.lastDayOfMonth,
      frequencyLabel: frequencyLabel(input),
      sendAtLocal: input.sendAtLocal,
      timeZoneId: zone,
      parameters: input.parameters,
      recipients: RECIPIENTS.filter((r) => input.recipientIds.includes(r.id)).map((r) => ({
        id: r.id,
        name: r.name,
        emailPresent: true,
        stillPermitted: true,
      })),
    };
    return HttpResponse.json({ nextRunOn: nextRunFor(input.sendAtLocal, zone) });
  }),

  http.post('*/api/admin/report-schedules/:id/:action', ({ request, params }) => {
    if (notAdmin(request)) return forbidden();
    const found = store.get(String(params.id));
    if (!found) return new HttpResponse(null, { status: 404 });
    const action = String(params.action);
    if (action === 'run-now') {
      if (found.detail.status === 'Cancelled') {
        return violation(
          400,
          'ReportScheduleCancelled',
          'This schedule was cancelled and cannot be changed. Create a new schedule instead.',
          'scheduleId',
        );
      }
      const runId = `ru000000-0000-0000-0000-00000000${String(found.runs.length + 10).padStart(4, '0')}`;
      found.runs.unshift({
        runId,
        dueOn: '2026-10-14T09:42:00+06:00',
        trigger: 'Manual',
        status: 'Generated',
        periodFrom: '2026-09-01',
        periodTo: '2026-09-30',
        format: found.detail.format === 'Csv' ? 'Csv' : 'Pdf',
        rowCount: 412,
        contentLength: 421888,
        attachmentOmitted: false,
        failureCode: null,
        generatedOn: '2026-10-14T09:42:10+06:00',
        purgedOn: null,
        deliveries: found.detail.recipients.map((r) => ({
          recipientName: r.name,
          status: 'Queued',
        })),
      });
      found.detail.lastRunOn = '2026-10-14T09:42:10+06:00';
      return HttpResponse.json({ runId, status: 'Generated', rowCount: 412, failureCode: null });
    }
    if (action === 'pause')
      found.detail = {
        ...found.detail,
        status: 'Paused',
        nextRunOn: null,
        version: found.detail.version + 1,
      };
    else if (action === 'resume')
      found.detail = {
        ...found.detail,
        status: 'Active',
        nextRunOn: '2026-11-01T07:00:00+06:00',
        version: found.detail.version + 1,
      };
    else if (action === 'cancel')
      found.detail = {
        ...found.detail,
        status: 'Cancelled',
        nextRunOn: null,
        version: found.detail.version + 1,
      };
    else return new HttpResponse(null, { status: 404 });
    return new HttpResponse(null, { status: 204 });
  }),
];

export { reportFixtureIds };
