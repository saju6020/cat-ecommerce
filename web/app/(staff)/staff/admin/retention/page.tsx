'use client';

import Link from 'next/link';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { ReviewEligiblePanel } from '@/components/retention/ReviewEligiblePanel';
import { RetentionScheduleTable } from '@/components/retention/RetentionScheduleTable';
import { RunRetentionNow } from '@/components/retention/RunRetentionNow';
import { ApiForbiddenError } from '@/lib/api/client';
import { useRetentionReview, useRetentionSchedule } from '@/lib/hooks/useRetention';
import { useStaffSession } from '@/lib/hooks/useSession';

/** `/staff/admin/retention` — US-XCT-06. The schedule, the review-eligible counts and "run now". */
export default function RetentionPage() {
  const schedule = useRetentionSchedule();
  const review = useRetentionReview();
  const session = useStaffSession();
  const canRun = (session.data?.roleIds ?? []).includes('Administrator'); // presentation only

  if (schedule.error instanceof ApiForbiddenError) return <PermissionDeniedState />;

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="font-display text-xl font-semibold text-ink">Data retention</h1>
        <nav aria-label="Retention sections" className="flex gap-3 text-sm font-semibold">
          <Link href="/staff/admin/retention/runs" className="text-accent-ink hover:underline">
            Runs
          </Link>
          <Link href="/staff/admin/retention/erasures" className="text-accent-ink hover:underline">
            Erasure requests
          </Link>
          <Link
            href="/staff/admin/retention/erasures/reapply"
            className="text-accent-ink hover:underline"
          >
            After a restore
          </Link>
        </nav>
      </div>
      <p className="text-sm text-ink-muted">
        Clinical, financial, regulatory and audit records are never deleted automatically. They are
        counted for review when their period ends.
      </p>
      {review.isLoading ? (
        <LoadingState rows={3} label="Loading records eligible for review…" />
      ) : review.data ? (
        <ReviewEligiblePanel review={review.data} />
      ) : (
        <ErrorState
          description="We couldn't load the review counts just now."
          onRetry={() => void review.refetch()}
        />
      )}
      {canRun && <RunRetentionNow />}
      {schedule.isLoading ? (
        <LoadingState variant="table" rows={6} label="Loading the retention schedule…" />
      ) : schedule.data ? (
        <RetentionScheduleTable schedule={schedule.data} />
      ) : (
        <ErrorState
          description="We couldn't load the retention schedule just now."
          onRetry={() => void schedule.refetch()}
        />
      )}
    </div>
  );
}
