'use client';

import { useMemo } from 'react';
import Link from 'next/link';
import { EmptyState } from '@/components/common/EmptyState';
import { LoadingState } from '@/components/common/LoadingState';
import { SimplePager } from '@/components/notifications/SimplePager';
import { ApiNotFoundError, ApiValidationError } from '@/lib/api/client';
import type { ReportCatalogueEntry } from '@/lib/api/reports';
import { formatReportDate } from '@/lib/format/report';
import { useBranchOptions } from '@/lib/hooks/useBranchOptions';
import { useNarrowContainer } from '@/lib/hooks/useIsNarrow';
import { useReportFilters } from '@/lib/hooks/useReportFilters';
import { useReportCatalogue, useReportRun } from '@/lib/hooks/useReports';
import { ExportButtons } from './ExportButtons';
import { ReportCardList } from './ReportCardList';
import { ReportChart } from './ReportChart';
import { ReportErrorView } from './ReportErrorView';
import { ReportFilterBar } from './ReportFilterBar';
import { ReportNotes } from './ReportNotes';
import { ReportSkeleton } from './ReportSkeleton';
import { ReportSortSelect } from './ReportSortSelect';
import { ReportTable, nextSort } from './ReportTable';
import { ScheduleThisReport } from './ScheduleThisReport';
import { filterFieldErrors } from './filterErrors';

/** `purrcare-{key}-{from}-to-{to}`, from the server's normalised filters, in safe characters only. */
export function exportBaseName(key: string, filters: { from: string; to: string }): string {
  const safe = (value: string) => value.replace(/[^A-Za-z0-9-]/g, '');
  return `purrcare-${safe(key)}-${safe(filters.from)}-to-${safe(filters.to)}`;
}

/**
 * `components/reports/ReportViewer.tsx` — ⭐ HO-RPT-002 §11.3, ADR-FE-129. THE one screen for every
 * report: it takes a `reportKey` and draws everything from the catalogue entry and the result
 * alone. There is no per-report component. The definition and the rows are two queries (NFR-PRF-3).
 * A key the catalogue does not list (it is filtered to the caller) is asked of the server anyway, so
 * a direct URL shows the server's own `403` / `404` rather than a guess (ADR-FE-137).
 */
export function ReportViewer({ reportKey }: { reportKey: string }) {
  const catalogue = useReportCatalogue();
  const definition = catalogue.data?.find((entry) => entry.key === reportKey);
  const probe = useReportRun(reportKey, '', catalogue.isSuccess && !definition);

  if (catalogue.isPending) {
    return <LoadingState variant="card" rows={6} label="Loading report…" />;
  }
  if (catalogue.isError) {
    return <ReportErrorView error={catalogue.error} onRetry={() => void catalogue.refetch()} />;
  }
  if (!definition) {
    if (probe.isPending) return <LoadingState variant="card" rows={3} label="Loading report…" />;
    // A `400` here means the server knows the key but wants filters — still "not in your catalogue".
    const error = probe.error instanceof ApiValidationError ? new ApiNotFoundError() : probe.error;
    return <ReportErrorView error={error ?? new ApiNotFoundError()} />;
  }
  return <ReportViewerBody definition={definition} />;
}

function ReportViewerBody({ definition }: { definition: ReportCatalogueEntry }) {
  const { state, searchKey, apply, setSort, setPage, reset } = useReportFilters(definition);
  const { options: branchOptions, zone: branchZone } = useBranchOptions();
  const [wrapperRef, narrow] = useNarrowContainer<HTMLDivElement>();
  const run = useReportRun(definition.key, state?.query ?? '', !!state);

  const errors = useMemo(
    () => (run.isError ? filterFieldErrors(run.error, definition) : null),
    [definition, run.isError, run.error],
  );

  if (!state) return null;

  const result = run.data;
  const zone = result?.filters.timeZoneId ?? branchZone;
  const rangeText = `${formatReportDate(state.filters.from)} to ${formatReportDate(state.filters.to)}`;
  const caption = result
    ? `${result.title} · ${formatReportDate(result.filters.from)} to ${formatReportDate(result.filters.to)} · ${result.filters.branchName} · times in ${result.filters.timeZoneId}`
    : definition.title;
  const filtersInvalid = !!errors && Object.keys(errors.fields).length > 0;
  const showError =
    run.isError && (!(run.error instanceof ApiValidationError) || (errors?.page.length ?? 0) > 0);

  return (
    <div className="flex flex-col gap-4">
      <Link href="/staff/reports" className="text-sm font-semibold text-accent-ink underline">
        <span aria-hidden="true">← </span>All reports
      </Link>

      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h1 className="font-display text-2xl font-semibold text-ink">{definition.title}</h1>
          <p className="mt-1 max-w-prose text-sm text-ink-muted">{definition.description}</p>
        </div>
        <div className="flex flex-wrap items-start justify-end gap-2">
          {definition.canSchedule && (
            <ScheduleThisReport reportKey={definition.key} filters={state.filters} />
          )}
          <ExportButtons
            reportKey={definition.key}
            query={state.query}
            fallbackBaseName={exportBaseName(definition.key, result?.filters ?? state.filters)}
            disabled={filtersInvalid}
          />
        </div>
      </div>

      <ReportFilterBar
        key={searchKey}
        definition={definition}
        applied={state.filters}
        timeZoneId={zone}
        branches={branchOptions}
        errors={errors?.fields ?? {}}
        onApply={apply}
        onReset={reset}
      />

      {showError && <ReportErrorView error={run.error} onRetry={() => void run.refetch()} />}

      <div ref={wrapperRef} className="flex min-w-0 flex-col gap-4">
        {run.isPending && <ReportSkeleton definition={definition} />}

        {result && !filtersInvalid && (
          <>
            <ReportNotes notes={result.notes} />
            {definition.timeBucketed && result.rows.length > 0 && (
              <ReportChart
                columns={result.columns}
                rows={result.rows}
                timeZoneId={result.filters.timeZoneId}
                title={result.title}
              />
            )}
            {result.rows.length === 0 ? (
              <EmptyState
                title={`No results for ${result.title} from ${rangeText} at ${result.filters.branchName}`}
                description="Try a wider date range, or switch to another branch."
              />
            ) : narrow ? (
              <>
                <ReportSortSelect columns={result.columns} sort={state.sort} onSort={setSort} />
                <ReportCardList
                  label={caption}
                  columns={result.columns}
                  rows={result.rows}
                  totals={result.totals}
                  timeZoneId={result.filters.timeZoneId}
                />
              </>
            ) : (
              <ReportTable
                caption={caption}
                columns={result.columns}
                rows={result.rows}
                totals={result.totals}
                timeZoneId={result.filters.timeZoneId}
                sort={state.sort}
                onSort={(key) => setSort(nextSort(state.sort, key))}
              />
            )}
            <SimplePager
              pageNumber={result.pageNumber}
              pageSize={result.pageSize}
              totalCount={result.totalCount}
              onPageChange={setPage}
            />
          </>
        )}
      </div>
    </div>
  );
}
