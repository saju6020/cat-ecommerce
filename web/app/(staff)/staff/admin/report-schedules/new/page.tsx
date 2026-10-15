'use client';

import { Suspense, useEffect, useMemo, useRef } from 'react';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { LoadingState } from '@/components/common/LoadingState';
import { ReportErrorView } from '@/components/reports/ReportErrorView';
import { ScheduleForm } from '@/components/report-schedules/ScheduleForm';
import { useBranchOptions } from '@/lib/hooks/useBranchOptions';
import { useReportCatalogue } from '@/lib/hooks/useReports';
import { useSaveSchedule } from '@/lib/hooks/useReportSchedules';
import {
  EMPTY_SCHEDULE_VALUES,
  toScheduleInput,
  type ReportScheduleFormValues,
} from '@/lib/validation/reportSchedule';

/** How long the unsaved-changes guard needs to pop its sentinel history entry after a save. */
const GUARD_SETTLE_MS = 400;

function NewScheduleContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const catalogue = useReportCatalogue();
  const { options, zoneFor, activeBranchId } = useBranchOptions();
  const save = useSaveSchedule();
  const navigateTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  // A user who leaves within the settle delay must not be pulled to the new schedule.
  useEffect(() => () => clearTimeout(navigateTimer.current), []);

  const reports = useMemo(
    () => (catalogue.data ?? []).filter((entry) => entry.canSchedule),
    [catalogue.data],
  );

  // Prefill from "Schedule this report": report, branch and the report's own parameters.
  const initial = useMemo<ReportScheduleFormValues>(() => {
    const report = reports.find((entry) => entry.key === searchParams.get('reportKey'));
    const parameters: Record<string, string> = {};
    for (const parameter of report?.parameters ?? []) {
      const value = searchParams.get(parameter.name);
      if (value) parameters[parameter.name] = value;
    }
    const requestedBranch = searchParams.get('branchId');
    // "all" is honoured only for a report that can run across branches.
    const branchId =
      requestedBranch === 'all' && report?.canUseAllBranches
        ? ''
        : requestedBranch && requestedBranch !== 'all'
          ? requestedBranch
          : (activeBranchId ?? options[0]?.id ?? '');
    return {
      ...EMPTY_SCHEDULE_VALUES,
      name: report ? report.title : '',
      reportKey: report?.key ?? '',
      period: report?.defaultPeriod ?? '',
      branchId,
      parameters,
    };
  }, [reports, searchParams, activeBranchId, options]);

  if (catalogue.isPending) return <LoadingState variant="card" rows={6} label="Loading…" />;
  if (catalogue.isError) {
    return <ReportErrorView error={catalogue.error} onRetry={() => void catalogue.refetch()} />;
  }

  return (
    <div className="flex flex-col gap-4">
      <Link
        href="/staff/admin/report-schedules"
        className="text-sm font-semibold text-accent-ink underline"
      >
        <span aria-hidden="true">← </span>Report schedules
      </Link>
      <h1 className="font-display text-2xl font-semibold text-ink">New report schedule</h1>
      <ScheduleForm
        key={`${initial.reportKey}|${initial.branchId}`}
        reports={reports}
        initial={initial}
        branches={options}
        zoneFor={zoneFor}
        isPending={save.isPending}
        submitLabel="Create schedule"
        onSubmit={async (values) => {
          const created = await save.mutateAsync(toScheduleInput(values));
          const destination = created.scheduleId
            ? `/staff/admin/report-schedules/${created.scheduleId}`
            : '/staff/admin/report-schedules';
          // After a save the unsaved-changes guard removes the history entry it pushed
          // (`history.back()` in UnsavedChangesGuard). A push made in the same tick is aborted by
          // that pop, so the navigation waits for it to settle.
          navigateTimer.current = setTimeout(() => router.push(destination), GUARD_SETTLE_MS);
        }}
      />
    </div>
  );
}

/** `/staff/admin/report-schedules/new` — HO-RPT-002 §11.5, US-RPT-06. */
export default function NewReportSchedulePage() {
  return (
    <Suspense fallback={<LoadingState variant="card" rows={6} label="Loading…" />}>
      <NewScheduleContent />
    </Suspense>
  );
}
