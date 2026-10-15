import { http, HttpResponse, type HttpHandler } from 'msw';
import type { ReportCell, ReportColumn, ReportNote, ReportRow } from '@/lib/api/reports';
import {
  branchFor,
  catalogueFor,
  categoriesFor,
  dayDiffInclusive,
  findEntry,
  forbidden,
  reportFixtureIds,
  rolesOf,
  violation,
} from './fixtures';

const money = (amount: string) => ({ amount, currencyCode: 'BDT' });

/** Row sets per report. `null` cells are deliberate: "not recorded" is never 0. */
function rowsFor(key: string, columns: ReportColumn[], total: number): ReportRow[] {
  const keys = new Set(columns.map((c) => c.key));
  const pick = (row: ReportRow): ReportRow =>
    Object.fromEntries(Object.entries(row).filter(([k]) => keys.has(k)));
  const rows: ReportRow[] = [];
  for (let i = 0; i < total; i += 1) {
    const hour = String(9 + (i % 8)).padStart(2, '0');
    switch (key) {
      case 'clinic-daily-schedule':
        rows.push(
          pick({
            start: `2026-10-14T${hour}:${i % 2 ? '30' : '00'}:00+06:00`,
            cat: ['Mittens', 'Bagha', 'Tuni', 'Lalu'][i % 4]!,
            customer: ['Farhana Yasmin', 'Kamrul Hasan', 'Nafisa Rahman'][i % 3]!,
            service: 'Standard consultation',
            performer: 'Dr. Nusrat Jahan',
            room: 'Consult 1',
            channel: i % 3 === 0 ? 'Online' : 'Reception',
            walkIn: i % 5 === 0,
            status: i === 3 ? 'No-show' : 'Completed',
            checkedInOn: i === 3 ? null : `2026-10-14T${hour}:05:00+06:00`,
            waitMinutes: i === 3 ? null : 5 + i,
          }),
        );
        break;
      case 'payment-reconciliation':
        rows.push(
          pick({
            businessDate: '2026-10-13',
            method: ['CashOnDelivery', 'CashInStore', 'CardInStore', 'BankTransfer'][i % 4]!,
            expected: money('1500.00'),
            received: i % 4 === 2 ? null : money('1500.00'),
            // The sign of `variance` is deliberately inconsistent (as on the real R13); the
            // direction is the server's word, and null for card in store (nothing to compare).
            variance: i % 4 === 2 ? null : money(['350.00', '0.00', '', '-120.00'][i % 4]!),
            varianceDirection: ['Short', 'Matched', null, 'Over'][i % 4] ?? null,
            matched: i % 4 === 0 ? 0 : 1,
            mismatchesOpen: i % 4 === 0 ? 1 : 0,
          }),
        );
        break;
      case 'revenue-by-business-line':
      case 'revenue-shop-by-category':
        rows.push(
          pick({
            bucket: `2026-10-${String(1 + (i % 14)).padStart(2, '0')}`,
            line: ['Clinic', 'Shop'][i % 2]!,
            invoices: 10 + i,
            gross: money('12500.00'),
            discounts: money('250.00'),
            tax: money('0.00'),
            net: money('12250.00'),
            cost: money('7000.00'),
            margin: i % 2 ? null : money('5250.00'),
            marginPercent: i % 2 ? null : '42.9',
            refunds: money('0.00'),
            creditNotes: money('0.00'),
          }),
        );
        break;
      case 'stock-near-expiry':
        rows.push(
          pick({
            sku: `SKU-${i + 1}`,
            lot: `L${100 + i}`,
            expiresOn: '2026-11-02',
            quantity: '12.5',
            valueAtRisk: money('840.00'),
          }),
        );
        break;
      case 'new-vs-returning':
        rows.push(
          pick({
            bucket: `2026-10-${String(1 + (i % 14)).padStart(2, '0')}`,
            newCustomers: 4 + i,
            returning: 9 + i,
            returnRate: '69.2',
          }),
        );
        break;
      default:
        rows.push(
          pick({ cat: 'Mittens', vaccine: 'FVRCP', dueOn: '2026-10-20', owner: 'Farhana Yasmin' }),
        );
    }
  }
  return rows;
}

function totalsFor(key: string, columns: ReportColumn[]): ReportRow | null {
  const keys = new Set(columns.map((c) => c.key));
  const pick = (row: ReportRow) =>
    Object.fromEntries(Object.entries(row).filter(([k]) => keys.has(k)));
  if (key === 'revenue-by-business-line' || key === 'revenue-shop-by-category') {
    return pick({
      invoices: 200,
      gross: money('175000.00'),
      net: money('171500.00'),
      cost: money('98000.00'),
      margin: money('73500.00'),
    });
  }
  if (key === 'payment-reconciliation') {
    return pick({
      expected: money('6000.00'),
      received: money('4500.00'),
      variance: money('350.00'),
    });
  }
  return null;
}

function notesFor(key: string, roles: string[]): ReportNote[] {
  const notes: ReportNote[] = [];
  if (key === 'clinic-daily-schedule') {
    notes.push({
      code: 'HoldsExcluded',
      text: 'Only confirmed bookings and walk-ins are listed. Provisional holds from online booking never appear.',
    });
    notes.push({ code: 'WalkInOrder', text: 'Walk-ins are ordered by check-in time.' });
  }
  if (
    (key === 'revenue-by-business-line' || key === 'revenue-shop-by-category') &&
    (roles.includes('Accountant') || roles.includes('Administrator'))
  ) {
    notes.push({
      code: 'MarginCoverage',
      text: 'Margin covers 83.0 % of shop revenue. 17.0 % came from batches with no landed cost yet.',
    });
  }
  if (key === 'payment-reconciliation') {
    notes.push({
      code: 'CardExpectedOnly',
      text: 'Card in store shows what was expected only, until a terminal settlement report is available.',
    });
  }
  return notes;
}

const PAGED_TOTAL: Record<string, number> = {
  'payment-reconciliation': 500,
  'clinic-daily-schedule': 11,
};

export const reportsHandlers: HttpHandler[] = [
  http.get('*/api/staff/reports', ({ request }) => {
    const roles = rolesOf(request);
    if (categoriesFor(roles).length === 0) return forbidden();
    return HttpResponse.json(catalogueFor(roles), { headers: { 'Cache-Control': 'no-store' } });
  }),

  http.get('*/api/staff/reports/:reportKey/:file', ({ request, params }) => {
    const file = String(params.file);
    if (!file.startsWith('export.')) return undefined;
    const roles = rolesOf(request);
    const entry = findEntry(String(params.reportKey));
    if (!entry) return violation(404, 'UnknownReport', 'This report does not exist.', null);
    if (!catalogueFor(roles).some((e) => e.key === entry.key)) return forbidden();
    const url = new URL(request.url);
    const from = url.searchParams.get('from') ?? '';
    if (from === reportFixtureIds.timeoutFrom) {
      return violation(
        400,
        'ReportTimedOut',
        'This report took too long to run. Choose a shorter date range or a single branch.',
        null,
      );
    }
    if (from === reportFixtureIds.rateLimitedFrom) {
      return HttpResponse.json(
        {
          errors: [
            { errorCode: 'TooManyRequests', errorMessage: 'Too many requests.', propertyName: '' },
          ],
        },
        { status: 429, headers: { 'Retry-After': '30' } },
      );
    }
    const format = file.slice('export.'.length);
    if (format === 'pdf' && from === reportFixtureIds.tooLargePdfFrom) {
      return violation(
        413,
        'ReportTooLargeForPdf',
        'Too many rows for a PDF -- export CSV or narrow the filters.',
        null,
      );
    }
    const name = `purrcare-${entry.key}-${from}-to-${url.searchParams.get('to')}.${format}`;
    const body =
      format === 'csv' ? `${entry.columns.map((c) => c.label).join(',')}\n` : '%PDF-1.4 mock';
    return new HttpResponse(body, {
      headers: {
        'Content-Type': format === 'csv' ? 'text/csv; charset=utf-8' : 'application/pdf',
        'Content-Disposition': `attachment; filename="${name}"`,
      },
    });
  }),

  http.get('*/api/staff/reports/:reportKey', ({ request, params }) => {
    const roles = rolesOf(request);
    const key = String(params.reportKey);
    const full = findEntry(key);
    if (!full) return violation(404, 'UnknownReport', 'This report does not exist.', null);
    const entry = catalogueFor(roles).find((e) => e.key === key);
    if (!entry) return forbidden();

    const url = new URL(request.url);
    const from = url.searchParams.get('from');
    const to = url.searchParams.get('to');
    if (!from || !to) {
      return violation(
        400,
        'ReportRangeRequired',
        'Choose a date range: both a start date and an end date.',
        'from',
      );
    }
    if (to < from) {
      return violation(
        400,
        'ReportRangeInverted',
        'The end date is before the start date. Choose an end date on or after the start date.',
        'to',
      );
    }
    if (dayDiffInclusive(from, to) > entry.maxRangeDays) {
      return violation(
        400,
        'ReportRangeTooLong',
        `This report covers at most ${entry.maxRangeDays} days. Choose a shorter date range.`,
        'to',
      );
    }
    if (from === reportFixtureIds.timeoutFrom) {
      return violation(
        400,
        'ReportTimedOut',
        'This report took too long to run. Choose a shorter date range or a single branch.',
        null,
      );
    }
    if (from === reportFixtureIds.rateLimitedFrom) {
      return HttpResponse.json(
        {
          errors: [
            { errorCode: 'TooManyRequests', errorMessage: 'Too many requests.', propertyName: '' },
          ],
        },
        { status: 429, headers: { 'Retry-After': '30' } },
      );
    }
    if (from === reportFixtureIds.clinicalFailFrom) {
      return HttpResponse.json({ error: 'The request could not be completed.' }, { status: 500 });
    }
    const category = url.searchParams.get('category');
    if (category && entry.parameters.length > 0) {
      const parameter = entry.parameters.find((p) => p.name === 'category');
      if (parameter?.allowedValues && !parameter.allowedValues.includes(category)) {
        return violation(
          400,
          'InvalidReportParameter',
          "The value given for 'category' is not allowed. Choose one of the listed values.",
          'category',
        );
      }
    }
    const requestedBranch = url.searchParams.get('branchId');
    if (requestedBranch === 'all' && !entry.canUseAllBranches) {
      return violation(
        403,
        'CrossBranchReportNotPermitted',
        'Only an administrator or an accountant can run a report across all branches. Choose one branch.',
        'branchId',
      );
    }

    const branch = branchFor(request);
    const pageNumber = Number(url.searchParams.get('pageNumber') ?? '1');
    const pageSize = Number(url.searchParams.get('pageSize') ?? '50');
    const empty = from === reportFixtureIds.emptyFrom;
    const total = empty ? 0 : (PAGED_TOTAL[key] ?? 6);
    const count = Math.max(0, Math.min(pageSize, total - (pageNumber - 1) * pageSize));
    let rows = rowsFor(key, entry.columns, count);
    const sort = url.searchParams.get('sort');
    if (sort) {
      const sortKey = sort.replace(/^-/, '');
      const dir = sort.startsWith('-') ? -1 : 1;
      rows = [...rows].sort(
        (a, b) => String(a[sortKey] ?? '').localeCompare(String(b[sortKey] ?? '')) * dir,
      );
    }
    const parameters: Record<string, ReportCell> = {};
    for (const parameter of entry.parameters) {
      parameters[parameter.name] = url.searchParams.get(parameter.name) ?? parameter.default;
    }

    return HttpResponse.json(
      {
        reportKey: key,
        title: entry.title,
        generatedOn: '2026-10-14T09:42:00+00:00',
        generatedBy: 'Ada Administrator',
        filters: {
          from,
          to,
          branchId: requestedBranch === 'all' ? null : branch.id,
          branchName: requestedBranch === 'all' ? 'All branches' : branch.name,
          timeZoneId: requestedBranch === 'all' ? 'Asia/Dhaka' : branch.timeZoneId,
          groupBy: url.searchParams.get('groupBy'),
          parameters,
        },
        columns: entry.columns,
        rows,
        totals: empty ? null : totalsFor(key, entry.columns),
        notes: empty ? [] : notesFor(key, roles),
        coverage: null,
        pageNumber,
        pageSize,
        totalCount: total,
      },
      { headers: { 'Cache-Control': 'no-store' } },
    );
  }),
];
