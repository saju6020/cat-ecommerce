import { http, HttpResponse, type HttpHandler } from 'msw';
import { administrationFixtureIds } from './fixtures';

/** `mocks/handlers/admin/audit.ts` — HO-ADM-002 §12, HO-ADM-001 §12.4, ADR-051. Search and CSV
 *  export. §12's own fixture requirement: a before/after diff and a formula-looking reason;
 *  `413` on export via a reserved `actorUserId` trigger value. */

interface AuditEntry {
  occurredOn: string;
  actorUserId: string;
  actorDisplayName: string;
  action: string;
  category: string;
  entityType: string;
  entityId: string;
  reason: string | null;
  beforeValue: string | null;
  afterValue: string | null;
  ipAddress: string | null;
}

function seedEntries(): AuditEntry[] {
  return [
    {
      occurredOn: '2027-01-05T10:15:00Z',
      actorUserId: administrationFixtureIds.adminStaffId,
      actorDisplayName: 'Ada Administrator',
      action: 'ServicePriceSet',
      category: 'DataChange',
      entityType: 'ServicePriceAggregate',
      entityId: administrationFixtureIds.serviceConsultationId,
      // U-G2 — a formula-looking reason; the diff table renders it as plain text only.
      reason: '=HYPERLINK("https://example.com","click")',
      beforeValue: JSON.stringify({ amount: '1500.00' }),
      afterValue: JSON.stringify({ amount: '1800.00' }),
      ipAddress: '203.0.113.10',
    },
    {
      occurredOn: '2027-01-06T08:00:00Z',
      actorUserId: administrationFixtureIds.adminStaffId,
      actorDisplayName: 'Ada Administrator',
      action: 'MaintenanceModeChanged',
      category: 'BusinessEvent',
      entityType: 'SystemStatusAggregate',
      entityId: 'singleton',
      reason: null,
      beforeValue: null,
      afterValue: JSON.stringify({ maintenanceMode: true }),
      ipAddress: '203.0.113.10',
    },
  ];
}

let entries = seedEntries();

export function resetAdminAuditMockData(): void {
  entries = seedEntries();
}

function filterEntries(url: URL): AuditEntry[] {
  const actorUserId = url.searchParams.get('actorUserId');
  const entityType = url.searchParams.get('entityType');
  const entityId = url.searchParams.get('entityId');
  const action = url.searchParams.get('action');
  const category = url.searchParams.get('category');
  const from = url.searchParams.get('from');
  const to = url.searchParams.get('to');
  return entries.filter((e) => {
    if (actorUserId && e.actorUserId !== actorUserId) return false;
    if (entityType && e.entityType !== entityType) return false;
    if (entityId && e.entityId !== entityId) return false;
    if (action && e.action !== action) return false;
    if (category && e.category !== category) return false;
    if (from && e.occurredOn.slice(0, 10) < from) return false;
    if (to && e.occurredOn.slice(0, 10) > to) return false;
    return true;
  });
}

export const adminAuditHandlers: HttpHandler[] = [
  http.get('*/api/admin/audit-entries', ({ request }) => {
    const url = new URL(request.url);
    const pageNumber = Number(url.searchParams.get('pageNumber') ?? '1');
    const pageSize = Number(url.searchParams.get('pageSize') ?? '50');
    const filtered = filterEntries(url);
    const start = (pageNumber - 1) * pageSize;
    return HttpResponse.json({
      items: filtered.slice(start, start + pageSize),
      pageNumber,
      pageSize,
      totalCount: filtered.length,
    });
  }),

  http.get('*/api/admin/audit-entries/export.csv', ({ request }) => {
    const url = new URL(request.url);
    if (url.searchParams.get('actorUserId') === administrationFixtureIds.trigger413ActorId) {
      return HttpResponse.json(
        {
          errors: [
            {
              errorCode: 'ExportTooLarge',
              errorMessage: 'Too many entries to export — narrow the date range.',
            },
          ],
        },
        { status: 413 },
      );
    }
    const filtered = filterEntries(url);
    const header = 'occurredOn,actorDisplayName,action,category,entityType,entityId,ipAddress';
    const rows = filtered.map((e) =>
      [
        e.occurredOn,
        e.actorDisplayName,
        e.action,
        e.category,
        e.entityType,
        e.entityId,
        e.ipAddress ?? '',
      ].join(','),
    );
    const csv = [header, ...rows].join('\n');
    return new HttpResponse(csv, {
      headers: {
        'Content-Type': 'text/csv',
        // `Content-Disposition: attachment` — not the anchor's own `download` attribute, which the
        // HTML spec ignores for a cross-origin URL (this export URL always is, HO-CAT-002 §12.2) —
        // is what actually forces a download here rather than a same-tab navigation to raw CSV text.
        'Content-Disposition': 'attachment; filename="audit-log.csv"',
        'Cache-Control': 'no-store',
      },
    });
  }),
];
