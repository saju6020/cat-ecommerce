'use client';

import { Suspense } from 'react';
import { useSearchParams } from 'next/navigation';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { LoadingState } from '@/components/common/LoadingState';
import { RetentionRunsTable } from '@/components/retention/RetentionRunsTable';
import { ApiForbiddenError } from '@/lib/api/client';
import { useAdminZone } from '@/lib/hooks/useAdminZone';
import { useRetentionRuns } from '@/lib/hooks/useRetention';

const PAGE_SIZE = 20;

function RunsContent() {
  const page = Number(useSearchParams().get('page') ?? '1') || 1;
  const runs = useRetentionRuns(page, PAGE_SIZE);
  const zone = useAdminZone();
  if (runs.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-xl font-semibold text-ink">Retention runs</h1>
      <RetentionRunsTable
        runs={runs.data?.items}
        totalCount={runs.data?.totalCount}
        pageSize={PAGE_SIZE}
        isLoading={runs.isLoading}
        isError={runs.isError}
        onRetry={() => void runs.refetch()}
        zone={zone}
      />
    </div>
  );
}

/** `/staff/admin/retention/runs` — US-XCT-06. */
export default function RetentionRunsPage() {
  return (
    <Suspense fallback={<LoadingState variant="table" rows={6} />}>
      <RunsContent />
    </Suspense>
  );
}
