'use client';

import { use, useId, useMemo, useState } from 'react';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { useAdminBranch, useBranchCalendar, useBranchClosures } from '@/lib/hooks/useAdminBranches';
import { BranchCalendarDayList } from '@/components/branches/BranchCalendarDayList';
import { BranchCalendarMonthNav } from '@/components/branches/BranchCalendarMonthNav';
import { BranchMonthCalendar } from '@/components/branches/BranchMonthCalendar';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { Button } from '@/components/ui/button';
import { ApiForbiddenError } from '@/lib/api/client';
import {
  buildBranchCalendarMonth,
  monthBounds,
  parseMonthParam,
  shiftMonth,
} from '@/lib/format/branch-calendar';
import { todayInZone } from '@/lib/format/time';

/** `/staff/admin/branches/[branchId]/calendar?month=yyyy-MM` — Calendar tab, HO-ADM-002-FU1 §11.3.
 *  Open/closed state and times come only from `GET …/calendar` (ADR-FE-170); the closure list and
 *  weekly hours only explain why a day is closed. */
export default function BranchCalendarPage({ params }: { params: Promise<{ branchId: string }> }) {
  const { branchId } = use(params);
  const router = useRouter();
  const rawMonth = useSearchParams().get('month');
  const headingId = useId();
  const [restoreFocusDay, setRestoreFocusDay] = useState<number | null>(null);

  const branchQuery = useAdminBranch(branchId);
  const branch = branchQuery.data;
  const today = branch ? todayInZone(branch.timeZoneId) : '';
  const month = branch ? parseMonthParam(rawMonth, branch.timeZoneId) : '';
  const { from, to } = month ? monthBounds(month) : { from: '', to: '' };
  const calendar = useBranchCalendar(branch ? branchId : '', from, to);
  const closures = useBranchClosures(branch ? branchId : '', { from, to });

  const days = useMemo(
    () =>
      branch && calendar.data
        ? buildBranchCalendarMonth({
            month,
            timeZoneId: branch.timeZoneId,
            weeklyHours: branch.weeklyHours,
            intervals: calendar.data,
            closures: closures.data,
            today,
          })
        : [],
    [branch, calendar.data, closures.data, month, today],
  );

  const forbidden = [branchQuery.error, calendar.error, closures.error].find(
    (e) => e instanceof ApiForbiddenError,
  );
  if (forbidden) return <AccessRefusedState error={forbidden} />;
  if (!branch || calendar.isLoading) return <LoadingState rows={6} label="Loading calendar…" />;
  if (calendar.isError || !calendar.data) {
    return (
      <ErrorState
        title="We couldn't load the calendar just now."
        description="This does not mean the branch is closed. Please try again."
        onRetry={() => calendar.refetch()}
        retryLabel="Retry"
      />
    );
  }

  const basePath = `/staff/admin/branches/${branchId}/calendar`;
  const currentMonth = today.slice(0, 7);

  return (
    <div className="flex flex-col gap-4">
      <h2 className="font-display text-lg font-semibold text-ink">Calendar</h2>

      {closures.isError && (
        <div
          role="status"
          className="flex flex-wrap items-center gap-2 rounded-[var(--radius-md)] border border-border bg-warning-bg p-3 text-sm text-warning-ink"
        >
          <span>Reasons for closed days couldn&apos;t be loaded.</span>
          <Button variant="ghost" size="sm" onClick={() => closures.refetch()}>
            Retry
          </Button>
        </div>
      )}
      {branch.weeklyHours.length === 0 && (
        <p
          role="status"
          className="m-0 rounded-[var(--radius-md)] border border-border bg-surface-sunken p-3 text-sm text-ink"
        >
          This branch has no opening hours yet, so every day shows as closed.{' '}
          <Link href={`/staff/admin/branches/${branchId}/hours`}>Set opening hours</Link>
        </p>
      )}

      <BranchCalendarMonthNav
        month={month}
        currentMonth={currentMonth}
        timeZoneId={branch.timeZoneId}
        basePath={basePath}
        headingId={headingId}
        hasPastDays={from < today}
      />

      <div className="hidden md:block">
        <BranchMonthCalendar
          days={days}
          month={month}
          monthHeadingId={headingId}
          restoreFocusDay={restoreFocusDay}
          onFocusRestored={() => setRestoreFocusDay(null)}
          onMonthChange={(delta, focusDay) => {
            setRestoreFocusDay(focusDay);
            router.push(`${basePath}?month=${shiftMonth(month, delta)}`);
          }}
        />
      </div>
      <div className="md:hidden">
        <BranchCalendarDayList days={days} monthHeadingId={headingId} />
      </div>
    </div>
  );
}
