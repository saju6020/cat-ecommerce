'use client';

import { use } from 'react';
import Link from 'next/link';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { RestoreTestDetail } from '@/components/operations/RestoreTestDetail';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { useAdminZone } from '@/lib/hooks/useAdminZone';
import { useRestoreTest } from '@/lib/hooks/useOperations';

/** `/staff/admin/operations/backup/[restoreTestId]` — US-XCT-05, a restore test in full. */
export default function RestoreTestPage({
  params,
}: {
  params: Promise<{ restoreTestId: string }>;
}) {
  const { restoreTestId } = use(params);
  const test = useRestoreTest(restoreTestId);
  const zone = useAdminZone();
  if (test.isLoading) return <LoadingState variant="card" rows={6} label="Loading the test…" />;
  if (test.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (test.error instanceof ApiNotFoundError) {
    return <ErrorState title="Restore test not found" description="It may have been removed from the log." />;
  }
  if (!test.data) {
    return (
      <ErrorState
        description="We couldn't load this restore test just now."
        onRetry={() => void test.refetch()}
      />
    );
  }
  return (
    <div className="flex flex-col gap-3">
      <Link
        href="/staff/admin/operations/backup"
        className="text-sm font-semibold text-accent-ink hover:underline"
      >
        Backup &amp; restore
      </Link>
      <RestoreTestDetail test={test.data} zone={zone} />
    </div>
  );
}
