import { http, HttpResponse, type HttpHandler } from 'msw';
import { forbidden, noContent, notFound, validationError } from '../admin/fixtures';
import {
  buildBackupStatus,
  buildRun,
  buildSchedule,
  buildSnapshot,
  buildWebVitals,
  erasureRequests,
  retentionReviewEmpty,
  retentionReviewWithRows,
  runSummaries,
  snapshotEtag,
  xctFixtureIds,
} from './fixtures';
import { restoreTestStore, xctMock, resetXctMock } from './state';

export { xctFixtureIds, VALID_DRILL_JSON, MALFORMED_DRILL_JSON } from './fixtures';
export { xctMock, resetXctMock } from './state';

/**
 * `mocks/handlers/operations/index.ts` — HO-XCT-002 §12.3. MSW handlers generated from HO-XCT-001
 * section 12.4 (as merged). Role enforcement is not modelled (precedent: `mocks/handlers/admin`);
 * a `403` is produced with `server.use(...)` in the test that needs it, except the snapshot, which
 * has a `forbidden` scenario because the offline sync's `403` branch is a rule of its own.
 */

function paged<T>(items: T[], url: URL) {
  const pageNumber = Number(url.searchParams.get('pageNumber') ?? '1') || 1;
  const pageSize = Number(url.searchParams.get('pageSize') ?? '20') || 20;
  const start = (pageNumber - 1) * pageSize;
  return { items: items.slice(start, start + pageSize), pageNumber, pageSize, totalCount: items.length };
}

export const operationsAreaHandlers: HttpHandler[] = [
  // ---- US-XCT-01 ---------------------------------------------------------------------------
  http.get('*/api/system/ping', () => {
    if (xctMock.pingMode === 'fail') return HttpResponse.error();
    if (xctMock.pingMode === 'serverError') return new HttpResponse(null, { status: 503 });
    return new HttpResponse(null, { status: 204, headers: { 'Cache-Control': 'no-store' } });
  }),

  http.get('*/api/staff/offline-snapshot', ({ request }) => {
    const ifNoneMatch = request.headers.get('If-None-Match');
    const scenario = xctMock.snapshotScenario;
    if (scenario === 'forbidden') {
      xctMock.snapshotRequests.push({ ifNoneMatch, status: 403 });
      return forbidden();
    }
    const etag = snapshotEtag(scenario, xctMock.snapshotVersion);
    if (ifNoneMatch === etag) {
      xctMock.snapshotRequests.push({ ifNoneMatch, status: 304 });
      return new HttpResponse(null, {
        status: 304,
        headers: { ETag: etag, 'Cache-Control': 'no-store, private' },
      });
    }
    xctMock.snapshotRequests.push({ ifNoneMatch, status: 200 });
    const branchId = request.headers.get('X-Branch-Id') ?? xctFixtureIds.branchId;
    const body = buildSnapshot(scenario, branchId, xctMock.snapshotVersion);
    if (xctMock.snapshotStaffAccountId) body.staffAccountId = xctMock.snapshotStaffAccountId;
    return HttpResponse.json(body, {
      headers: { ETag: etag, 'Cache-Control': 'no-store, private' },
    });
  }),

  // ---- US-XCT-02 ---------------------------------------------------------------------------
  http.post('*/api/telemetry/web-vitals', async ({ request }) => {
    const text = await request.text();
    let body: unknown = null;
    try {
      body = JSON.parse(text);
    } catch {
      return validationError('payload', 'InvalidWebVitalsPayload', 'That batch was not accepted.');
    }
    xctMock.telemetry.push({
      contentType: request.headers.get('Content-Type'),
      hasCookie: request.headers.has('Cookie'),
      hasCsrf: request.headers.has('X-CSRF-Token'),
      body,
    });
    return noContent();
  }),

  http.get('*/api/admin/web-vitals/summary', ({ request }) => {
    const url = new URL(request.url);
    const from = url.searchParams.get('from');
    const to = url.searchParams.get('to');
    if (!from || !to) {
      return validationError('from', 'InvalidDateRange', 'Give a from and to date.');
    }
    return HttpResponse.json(buildWebVitals(from, to));
  }),

  // ---- US-XCT-06 ---------------------------------------------------------------------------
  http.get('*/api/admin/retention/schedule', () => HttpResponse.json(buildSchedule())),
  http.get('*/api/admin/retention/review', () => HttpResponse.json(retentionReviewWithRows)),
  http.get('*/api/admin/retention/runs', ({ request }) => {
    const url = new URL(request.url);
    const status = url.searchParams.get('status');
    const rows = status ? runSummaries.filter((r) => r.status === status) : runSummaries;
    return HttpResponse.json(paged(rows, url));
  }),
  http.get('*/api/admin/retention/runs/:runId', ({ params }) => {
    const run = buildRun(String(params.runId));
    return run ? HttpResponse.json(run) : notFound();
  }),
  http.post('*/api/admin/retention/runs', () => {
    if (xctMock.retentionRunInProgress) {
      return HttpResponse.json(
        {
          isValid: false,
          errors: [
            {
              propertyName: 'run',
              errorCode: 'RetentionRunAlreadyInProgress',
              errorMessage: 'A retention run is already in progress.',
              resourceName: 'AdminRetention',
            },
          ],
        },
        { status: 409 },
      );
    }
    return HttpResponse.json(
      { retentionRunId: 'f0000000-0000-0000-0000-0000000000ff', status: 'Requested' },
      { status: 201 },
    );
  }),
  http.get('*/api/admin/retention/erasure-requests', ({ request }) => {
    const url = new URL(request.url);
    const status = url.searchParams.get('status');
    const rows = status ? erasureRequests.filter((r) => r.status === status) : erasureRequests;
    return HttpResponse.json(paged(rows, url));
  }),
  http.get('*/api/admin/retention/erasure-requests/:id', ({ params }) => {
    const found = erasureRequests.find((r) => r.customerAccountId === params.id);
    return found ? HttpResponse.json(found) : notFound();
  }),
  http.post('*/api/admin/customers/:customerId/erasure', async ({ params, request }) => {
    const body = (await request.json()) as { channel?: string; identityCheck?: string; reason?: string };
    if (body.reason !== undefined) {
      return validationError('reason', 'Unknown', 'Free text is not accepted.');
    }
    if (!body.channel || !body.identityCheck) {
      return validationError('identityCheck', 'IdentityCheckRequired', 'Choose how identity was checked.');
    }
    const id = String(params.customerId);
    if (id === xctFixtureIds.mergedCustomerId) {
      return validationError(
        'customerAccountId',
        'CustomerRecordMerged',
        'This record was merged into another customer record. Erase the surviving record instead.',
        'AdminCustomerErasure',
      );
    }
    xctMock.erasedCustomerIds.add(id);
    return HttpResponse.json({ isReapplication: false });
  }),
  http.post('*/api/admin/retention/erasures/reapply', async ({ request }) => {
    const body = (await request.json()) as { customerAccountIds: string[]; restoreReference: string };
    if (body.customerAccountIds.length > 500) {
      return validationError('customerAccountIds', 'ReapplyListTooLarge', 'That is more than 500 customers.');
    }
    return HttpResponse.json({
      processed: body.customerAccountIds.length > 1 ? body.customerAccountIds.length - 1 : 0,
      alreadyErased: body.customerAccountIds.length > 1 ? 1 : 0,
      notFound: 0,
      merged: 0,
      failed: 0,
    });
  }),

  // An erased customer reads back as anonymised (E-C1: "the record shows the placeholder"). Falls
  // through to the Accounts mock for every other id.
  http.get('*/api/staff/customers/:customerId', ({ params }) => {
    const id = String(params.customerId);
    if (!xctMock.erasedCustomerIds.has(id)) return undefined;
    return HttpResponse.json({
      id,
      fullName: 'Erased customer',
      mobileNumber: null,
      emailAddress: null,
      recordStatus: 'Complete',
      registrationChannel: 'SelfService',
      catCount: 0,
      cats: [],
      status: 'Anonymised',
      mergedIntoCustomerId: null,
    });
  }),

  // ---- US-XCT-05 ---------------------------------------------------------------------------
  http.get('*/api/admin/operations/backup-status', () =>
    HttpResponse.json(buildBackupStatus(xctMock.backupScenario), {
      headers: { 'Cache-Control': 'no-store' },
    }),
  ),
  http.get('*/api/admin/operations/restore-tests', ({ request }) => {
    const url = new URL(request.url);
    const rows = restoreTestStore
      .slice()
      .sort((a, b) => b.performedOn.localeCompare(a.performedOn))
      .map((row) => ({ ...row, detail: undefined }));
    return HttpResponse.json(paged(rows, url));
  }),
  http.get('*/api/admin/operations/restore-tests/:id', ({ params }) => {
    const found = restoreTestStore.find((r) => r.id === params.id);
    return found ? HttpResponse.json(found.detail) : notFound();
  }),
  http.post('*/api/admin/operations/restore-tests', async ({ request }) => {
    const body = (await request.json()) as Record<string, unknown> & {
      checks?: Array<{ name: string; passed: boolean; detail: string | null }>;
      simulatedFailureAt: string;
      recoveredTo: string;
      restoreStartedAt: string;
      restoreCompletedAt: string;
      performedOn: string;
      backupSetLabel: string;
      environment: string;
      supersedesId?: string;
    };
    if (!body.checks || body.checks.length === 0) {
      return validationError('checks', 'RestoreTestChecksRequired', 'Add at least one check.');
    }
    const failureMs = Date.parse(body.simulatedFailureAt);
    const recoveredMs = Date.parse(body.recoveredTo);
    const startedMs = Date.parse(body.restoreStartedAt);
    const completedMs = Date.parse(body.restoreCompletedAt);
    if (!(recoveredMs <= failureMs && startedMs <= completedMs)) {
      return validationError('restoreCompletedAt', 'InvalidRestoreTestTimes', 'The times do not run in order.');
    }
    // The server's own computation (HO-XCT-001 ADR-313): the form never supplies these.
    const rpo = Math.round(((failureMs - recoveredMs) / 60_000) * 10) / 10;
    const rto = Math.round(((completedMs - failureMs) / 60_000) * 10) / 10;
    const passed = body.checks.every((c) => c.passed) && rpo <= 15 && rto <= 240;
    const id = `e1000000-0000-0000-0000-${String(restoreTestStore.length + 10).padStart(12, '0')}`;
    const outcome = passed ? ('Passed' as const) : ('Failed' as const);
    restoreTestStore.push({
      id,
      performedOn: body.performedOn,
      outcome,
      achievedRpoMinutes: rpo,
      achievedRtoMinutes: rto,
      environment: body.environment,
      backupSetLabel: body.backupSetLabel,
      supersedesId: body.supersedesId ?? null,
      detail: { ...body, id, outcome, achievedRpoMinutes: rpo, achievedRtoMinutes: rto, supersededByIds: [], notes: null },
    });
    return HttpResponse.json(
      { restoreTestId: id, outcome, achievedRpoMinutes: rpo, achievedRtoMinutes: rto },
      { status: 201 },
    );
  }),
];

export { retentionReviewEmpty };

/** Test seam. */
export function resetOperationsMockData(): void {
  resetXctMock();
}
