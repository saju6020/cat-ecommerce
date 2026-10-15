'use client';

import { Suspense } from 'react';
import { useSearchParams } from 'next/navigation';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { BackupStatusCard } from '@/components/operations/BackupStatusCard';
import { RecordRestoreTestForm } from '@/components/operations/RecordRestoreTestForm';
import { RestoreTestDueCard } from '@/components/operations/RestoreTestDueCard';
import { RestoreTestsTable } from '@/components/operations/RestoreTestsTable';
import { ApiForbiddenError } from '@/lib/api/client';
import { useAdminZone } from '@/lib/hooks/useAdminZone';
import { useBackupStatus, useRestoreTests } from '@/lib/hooks/useOperations';

const PAGE_SIZE = 10;

function BackupContent() {
  const page = Number(useSearchParams().get('page') ?? '1') || 1;
  const status = useBackupStatus();
  const tests = useRestoreTests(page, PAGE_SIZE);
  const zone = useAdminZone();

  if (status.error instanceof ApiForbiddenError) return <PermissionDeniedState />;

  return (
    <div className="flex flex-col gap-5">
      <h1 className="font-display text-xl font-semibold text-ink">Backup &amp; restore</h1>
      {status.isLoading ? (
        <LoadingState rows={5} label="Loading backup status…" />
      ) : status.data ? (
        <>
          {/* Both problems (a stale backup, an overdue test) are above the fold (E-D1). */}
          <RestoreTestDueCard status={status.data} />
          <BackupStatusCard status={status.data} zone={zone} />
        </>
      ) : (
        <ErrorState
          description="We couldn't load the backup status just now."
          onRetry={() => void status.refetch()}
        />
      )}
      <section aria-labelledby="tests-title" className="flex flex-col gap-3">
        <h2 id="tests-title" className="text-[15px] font-semibold text-ink">
          Restore tests
        </h2>
        <RestoreTestsTable
          tests={tests.data?.items}
          totalCount={tests.data?.totalCount}
          pageSize={PAGE_SIZE}
          isLoading={tests.isLoading}
          isError={tests.isError}
          onRetry={() => void tests.refetch()}
        />
      </section>
      <RecordRestoreTestForm previousTests={tests.data?.items ?? []} />
    </div>
  );
}

/** `/staff/admin/operations/backup` — US-XCT-05. */
export default function BackupPage() {
  return (
    <Suspense fallback={<LoadingState rows={6} />}>
      <BackupContent />
    </Suspense>
  );
}
