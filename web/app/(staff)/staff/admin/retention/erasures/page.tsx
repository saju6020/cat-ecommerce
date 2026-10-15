'use client';

import { Suspense, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { LoadingState } from '@/components/common/LoadingState';
import { ErasureRequestsTable } from '@/components/retention/ErasureRequestsTable';
import { ApiForbiddenError } from '@/lib/api/client';
import type { ErasureRequestStatus } from '@/lib/api/retention';
import { useAdminZone } from '@/lib/hooks/useAdminZone';
import { useErasureRequests } from '@/lib/hooks/useRetention';

const PAGE_SIZE = 20;

function ErasuresContent() {
  const page = Number(useSearchParams().get('page') ?? '1') || 1;
  const [status, setStatus] = useState<ErasureRequestStatus | ''>('');
  const requests = useErasureRequests(page, PAGE_SIZE, status || undefined);
  const zone = useAdminZone();
  if (requests.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-xl font-semibold text-ink">Erasure requests</h1>
      <p className="text-sm text-ink-muted">
        The ledger shows account ids only. A customer&rsquo;s name, phone and email no longer exist
        once they are erased.
      </p>
      <ErasureRequestsTable
        requests={requests.data?.items}
        totalCount={requests.data?.totalCount}
        pageSize={PAGE_SIZE}
        status={status}
        onStatusChange={setStatus}
        isLoading={requests.isLoading}
        isError={requests.isError}
        onRetry={() => void requests.refetch()}
        zone={zone}
      />
    </div>
  );
}

/** `/staff/admin/retention/erasures` — US-XCT-06. */
export default function ErasureRequestsPage() {
  return (
    <Suspense fallback={<LoadingState variant="table" rows={6} />}>
      <ErasuresContent />
    </Suspense>
  );
}
