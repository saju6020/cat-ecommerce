'use client';

import { LoadingState } from '@/components/common/LoadingState';
import { DashboardView } from '@/components/dashboard/DashboardView';
import { ReportErrorView } from '@/components/reports/ReportErrorView';
import { useDashboard } from '@/lib/hooks/useReports';

/**
 * `/staff` — HO-RPT-002 §11.2, US-RPT-05, ADR-FE-130. The staff home: today's figures for the
 * signed-in role at the active branch, polled on the payload's `refreshSeconds` while the tab is
 * visible (ADR-FE-135). The active-branch picker is rendered by the staff layout above this page.
 * A `403` renders `PermissionDeniedState`; the nav entry is presentation only.
 */
export default function StaffHomePage() {
  const dashboard = useDashboard();

  // Keep showing the last good tiles when a background refresh fails (ADR-FE-135) — an error banner
  // must not replace the page under an open dialog.
  if (dashboard.data) {
    return (
      <DashboardView
        dashboard={dashboard.data}
        updatedAt={dashboard.dataUpdatedAt}
        refreshFailed={dashboard.isRefetchError}
      />
    );
  }

  if (dashboard.isPending) {
    return (
      <div className="flex flex-col gap-4">
        <h1 className="font-display text-2xl font-semibold text-ink">Today</h1>
        <LoadingState variant="card" rows={5} label="Loading today's figures…" />
      </div>
    );
  }

  if (dashboard.isError) {
    return (
      <div className="flex flex-col gap-4">
        <h1 className="font-display text-2xl font-semibold text-ink">Today</h1>
        <ReportErrorView error={dashboard.error} onRetry={() => void dashboard.refetch()} />
      </div>
    );
  }

  return null;
}
