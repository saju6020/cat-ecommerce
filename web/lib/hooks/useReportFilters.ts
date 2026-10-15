'use client';

import { useCallback, useMemo } from 'react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import { REPORT_PAGE_SIZE, type ReportCatalogueEntry } from '@/lib/api/reports';

/**
 * `lib/hooks/useReportFilters.ts` — HO-RPT-002 §8.3, ADR-FE-129. Every report filter lives in the
 * URL search params, so a filtered report is a shareable link that survives a reload (NFR-USA-5).
 *
 * An empty URL is SEEDED from the catalogue: `from`/`to` come from the server's `defaultRange`
 * (ADR-FE-131 — this file does no date arithmetic at all) and a blank parameter means "the
 * server's default", which the server fills in itself. A `from=` that is present but empty is kept
 * empty on purpose, so the server's `ReportRangeRequired` can answer and be shown on the field.
 */
export interface ReportFilterValues {
  from: string;
  to: string;
  /** '' = the active branch; `all` = every branch; otherwise a branch id. */
  branchId: string;
  groupBy: string;
  /** Parameter name -> raw text; '' = use the report's default. */
  parameters: Record<string, string>;
}

export interface ReportUrlState {
  filters: ReportFilterValues;
  sort: string;
  pageNumber: number;
  /** The query string sent to the server for the rows (and, minus paging, for an export). */
  query: string;
}

export function readReportUrl(
  searchParams: URLSearchParams,
  definition: ReportCatalogueEntry,
): ReportUrlState {
  const parameters: Record<string, string> = {};
  for (const parameter of definition.parameters) {
    parameters[parameter.name] = searchParams.get(parameter.name) ?? '';
  }
  const filters: ReportFilterValues = {
    from: searchParams.get('from') ?? definition.defaultRange.from,
    to: searchParams.get('to') ?? definition.defaultRange.to,
    branchId: searchParams.get('branchId') ?? '',
    groupBy: definition.timeBucketed ? (searchParams.get('groupBy') ?? '') : '',
    parameters,
  };
  const sort = searchParams.get('sort') ?? '';
  const pageNumber = Math.max(1, Number(searchParams.get('pageNumber') ?? '1') || 1);

  const params = new URLSearchParams();
  if (filters.from) params.set('from', filters.from);
  if (filters.to) params.set('to', filters.to);
  if (filters.branchId) params.set('branchId', filters.branchId);
  if (filters.groupBy) params.set('groupBy', filters.groupBy);
  for (const [name, value] of Object.entries(parameters)) {
    if (value !== '') params.set(name, value);
  }
  if (sort) params.set('sort', sort);
  params.set('pageNumber', String(pageNumber));
  params.set('pageSize', String(REPORT_PAGE_SIZE));
  return { filters, sort, pageNumber, query: params.toString() };
}

/** The URL query that stores `filters` (parameters equal to the report's default are left out). */
export function writeReportUrl(
  definition: ReportCatalogueEntry,
  filters: ReportFilterValues,
  sort: string,
  pageNumber: number,
): string {
  const params = new URLSearchParams();
  params.set('from', filters.from);
  params.set('to', filters.to);
  if (filters.branchId) params.set('branchId', filters.branchId);
  if (filters.groupBy) params.set('groupBy', filters.groupBy);
  for (const parameter of definition.parameters) {
    const value = filters.parameters[parameter.name] ?? '';
    if (value !== '' && value !== parameter.default) params.set(parameter.name, value);
  }
  if (sort) params.set('sort', sort);
  if (pageNumber > 1) params.set('pageNumber', String(pageNumber));
  return params.toString();
}

export function useReportFilters(definition: ReportCatalogueEntry | undefined) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const searchKey = searchParams.toString();

  const state = useMemo(
    () => (definition ? readReportUrl(new URLSearchParams(searchKey), definition) : undefined),
    [definition, searchKey],
  );

  const go = useCallback(
    (query: string) => router.push(query ? `${pathname}?${query}` : pathname),
    [router, pathname],
  );

  const apply = useCallback(
    (filters: ReportFilterValues) => {
      if (!definition || !state) return;
      go(writeReportUrl(definition, filters, state.sort, 1));
    },
    [definition, state, go],
  );

  const setSort = useCallback(
    (sort: string) => {
      if (!definition || !state) return;
      go(writeReportUrl(definition, state.filters, sort, 1));
    },
    [definition, state, go],
  );

  const setPage = useCallback(
    (pageNumber: number) => {
      if (!definition || !state) return;
      go(writeReportUrl(definition, state.filters, state.sort, pageNumber));
    },
    [definition, state, go],
  );

  /** Back to the server's defaults: an empty URL (the sort is kept). */
  const reset = useCallback(() => {
    if (!state) return;
    go(state.sort ? `sort=${encodeURIComponent(state.sort)}` : '');
  }, [state, go]);

  return { state, searchKey, apply, setSort, setPage, reset };
}
