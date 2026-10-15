'use client';

import { LoadingState } from '@/components/common/LoadingState';
import { ReportCatalogue } from '@/components/reports/ReportCatalogue';
import { ReportErrorView } from '@/components/reports/ReportErrorView';
import { useReportCatalogue } from '@/lib/hooks/useReports';

/**
 * `/staff/reports` — HO-RPT-002 §11.3, US-RPT-01. The catalogue grouped by category. The server
 * returns only the reports the caller may open (ADR-FE-137); a `403` (a Nurse) renders
 * `PermissionDeniedState`.
 */
export default function ReportsPage() {
  const catalogue = useReportCatalogue();

  return (
    <div className="flex flex-col gap-5">
      <header>
        <h1 className="font-display text-2xl font-semibold text-ink">Reports</h1>
        <p className="mt-1 text-sm text-ink-muted">
          Every report filters by date range and branch, and exports to CSV or PDF.
        </p>
      </header>
      {catalogue.isPending ? (
        <LoadingState variant="card" rows={6} label="Loading reports…" />
      ) : catalogue.isError ? (
        <ReportErrorView error={catalogue.error} onRetry={() => void catalogue.refetch()} />
      ) : (
        <ReportCatalogue reports={catalogue.data} />
      )}
    </div>
  );
}
