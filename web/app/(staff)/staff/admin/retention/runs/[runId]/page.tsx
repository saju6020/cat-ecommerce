'use client';

import { use } from 'react';
import Link from 'next/link';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { RetentionRunDetail } from '@/components/retention/RetentionRunDetail';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { useAdminZone } from '@/lib/hooks/useAdminZone';
import { useRetentionRun } from '@/lib/hooks/useRetention';

/** `/staff/admin/retention/runs/[runId]` — US-XCT-06. */
export default function RetentionRunPage({ params }: { params: Promise<{ runId: string }> }) {
  const { runId } = use(params);
  const run = useRetentionRun(runId);
  const zone = useAdminZone();
  if (run.isLoading) return <LoadingState variant="card" rows={5} label="Loading the run…" />;
  if (run.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (run.error instanceof ApiNotFoundError) {
    return (
      <ErrorState title="Run not found" description="This run may be older than the log keeps." />
    );
  }
  if (!run.data) {
    return (
      <ErrorState
        description="We couldn't load this run just now."
        onRetry={() => void run.refetch()}
      />
    );
  }
  return (
    <div className="flex flex-col gap-3">
      <Link
        href="/staff/admin/retention/runs"
        className="text-sm font-semibold text-accent-ink hover:underline"
      >
        All runs
      </Link>
      <RetentionRunDetail run={run.data} zone={zone} />
    </div>
  );
}
