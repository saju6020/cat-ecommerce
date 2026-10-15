'use client';

import { use } from 'react';
import Link from 'next/link';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { ErasureLedgerDetail } from '@/components/retention/ErasureLedgerDetail';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { useAdminZone } from '@/lib/hooks/useAdminZone';
import { useErasureRequest } from '@/lib/hooks/useRetention';

/** `/staff/admin/retention/erasures/[customerAccountId]` — US-XCT-06. */
export default function ErasureLedgerPage({
  params,
}: {
  params: Promise<{ customerAccountId: string }>;
}) {
  const { customerAccountId } = use(params);
  const request = useErasureRequest(customerAccountId);
  const zone = useAdminZone();
  if (request.isLoading) return <LoadingState variant="card" rows={5} label="Loading the ledger…" />;
  if (request.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (request.error instanceof ApiNotFoundError) {
    return (
      <ErrorState
        title="No erasure request"
        description="There is no erasure request for this account."
      />
    );
  }
  if (!request.data) {
    return (
      <ErrorState
        description="We couldn't load this ledger just now."
        onRetry={() => void request.refetch()}
      />
    );
  }
  return (
    <div className="flex flex-col gap-3">
      <Link
        href="/staff/admin/retention/erasures"
        className="text-sm font-semibold text-accent-ink hover:underline"
      >
        All erasure requests
      </Link>
      <ErasureLedgerDetail request={request.data} zone={zone} />
    </div>
  );
}
