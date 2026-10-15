import { apiFetch } from './client';

/**
 * `lib/api/reports.ts` — HO-RPT-002 §8.2, ADR-FE-129. Shapes verified against
 * `StaffReportsController`, `ReportResultFormatter` and `ReportCatalogueEntryViewModel`
 * (HO-RPT-001 §12.4 and its Amended rows). Enum-shaped strings are the C# `Enumeration.DisplayName`.
 *
 * The catalogue drives the whole viewer: filter controls come from `parameters`, columns from
 * `columns`, the default range from `defaultRange`. Nothing here is per report (ADR-FE-129), and
 * nothing is computed (ADR-FE-131): a total, a coverage percentage and a default range all arrive
 * from the server.
 */
export type ReportCategory =
  'ClinicOperations' | 'Clinical' | 'Boarding' | 'Stock' | 'Sales' | 'Financial' | 'Growth';

export type ReportKind = 'Period' | 'AsAt';

export type ReportColumnType =
  'Text' | 'Integer' | 'Decimal' | 'Money' | 'Percent' | 'Date' | 'Instant' | 'Boolean';

export type ReportParameterType = 'Enum' | 'Guid' | 'Integer' | 'Boolean';

/** `ReportRelativePeriod` display names (the server's `defaultPeriod` and a schedule's `period`). */
export type ReportRelativePeriod =
  | 'Today'
  | 'PreviousDay'
  | 'PreviousWeek'
  | 'PreviousMonth'
  | 'MonthToDate'
  | 'Last7Days'
  | 'Last30Days'
  | 'Next30Days'
  | 'Next90Days';

export interface ReportParameter {
  name: string;
  label: string;
  /** Typed as `string` so an unknown future type never crashes the page (U-C1). */
  type: ReportParameterType | (string & {});
  allowedValues?: string[] | null;
  min?: number | null;
  max?: number | null;
  default: string;
}

export interface ReportColumn {
  key: string;
  label: string;
  type: ReportColumnType | (string & {});
  unit?: string | null;
  sortable: boolean;
  /** Optional trailing meaning of a column (HO-RPT-001-FU1); `null` on every column but `varianceDirection`. */
  description?: string | null;
}

export interface ReportCatalogueEntry {
  key: string;
  title: string;
  description: string;
  category: ReportCategory | (string & {});
  kind: ReportKind | (string & {});
  timeBucketed: boolean;
  defaultPeriod: ReportRelativePeriod | (string & {});
  defaultRange: { from: string; to: string };
  maxRangeDays: number;
  formats: string[];
  parameters: ReportParameter[];
  columns: ReportColumn[];
  canUseAllBranches: boolean;
  canSchedule: boolean;
}

export interface ReportMoneyCell {
  amount: string;
  currencyCode: string;
}

/** A cell as the server sends it: money `{ amount, currencyCode }`, a percent/decimal/date/instant as
 *  a string, an integer as a number, a boolean, or `null` for "not recorded" (never 0). */
export type ReportCell = string | number | boolean | ReportMoneyCell | null;

export type ReportRow = Record<string, ReportCell>;

export interface ReportNote {
  code: string;
  text: string;
}

export interface ReportCoverage {
  costedRevenue: ReportMoneyCell;
  totalRevenue: ReportMoneyCell;
  percent: string;
}

export interface ReportResult {
  reportKey: string;
  title: string;
  generatedOn: string;
  generatedBy: string;
  filters: {
    from: string;
    to: string;
    branchId: string | null;
    branchName: string;
    timeZoneId: string;
    groupBy?: string | null;
    parameters: Record<string, string | number | boolean | null>;
  };
  columns: ReportColumn[];
  rows: ReportRow[];
  totals: ReportRow | null;
  notes: ReportNote[];
  coverage: ReportCoverage | null;
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}

/** The URL/query fields the server reserves; everything else is a report parameter. */
export const RESERVED_REPORT_FIELDS = [
  'from',
  'to',
  'branchId',
  'groupBy',
  'sort',
  'pageNumber',
  'pageSize',
] as const;

export const REPORT_PAGE_SIZE = 50;

/** `branchId=all` is the server's token for "every branch" (`ReportRules.AllBranches`). */
export const ALL_BRANCHES_TOKEN = 'all';

/** `TimeBucket` display names the server accepts for `groupBy`. */
export const GROUP_BY_OPTIONS = ['Day', 'Week', 'Month'] as const;

export function getReportCatalogue(): Promise<ReportCatalogueEntry[]> {
  return apiFetch<ReportCatalogueEntry[]>('/api/staff/reports');
}

/** Builds `from=…&to=…&{parameters}` from plain URL search params, dropping empty values. */
export function buildReportQuery(
  source: URLSearchParams | Record<string, string | undefined>,
  extra: Record<string, string | undefined> = {},
): string {
  const params = new URLSearchParams();
  const entries =
    source instanceof URLSearchParams ? [...source.entries()] : Object.entries(source);
  for (const [name, value] of entries) {
    if (value !== undefined && value !== '') params.set(name, value);
  }
  for (const [name, value] of Object.entries(extra)) {
    if (value === undefined || value === '') params.delete(name);
    else params.set(name, value);
  }
  return params.toString();
}

export function runReport(reportKey: string, query: string): Promise<ReportResult> {
  const suffix = query ? `?${query}` : '';
  return apiFetch<ReportResult>(`/api/staff/reports/${encodeURIComponent(reportKey)}${suffix}`);
}

export type ReportExportFormat = 'csv' | 'pdf';

/** The path of an export of the same filters (no paging fields — the server exports every row). */
export function reportExportPath(
  reportKey: string,
  format: ReportExportFormat,
  query: string,
): string {
  const params = new URLSearchParams(query);
  params.delete('pageNumber');
  params.delete('pageSize');
  const suffix = params.toString();
  return `/api/staff/reports/${encodeURIComponent(reportKey)}/export.${format}${suffix ? `?${suffix}` : ''}`;
}
