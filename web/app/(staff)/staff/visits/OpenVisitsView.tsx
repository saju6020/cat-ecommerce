'use client';

import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { OpenVisitsTable } from '@/components/visit/OpenVisitsTable';
import { ApiForbiddenError } from '@/lib/api/client';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useOpenVisits } from '@/lib/hooks/useVisit';

/** Container for `/staff/visits` (HO-EMR-002 §11.9). The poll replaces `data`; nothing is reset. */
export function OpenVisitsView() {
  const visits = useOpenVisits();
  const zone = useBranchTimeZone();
  if (visits.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-2xl font-semibold text-ink">Open visits</h1>
      <p className="text-sm text-ink-muted">
        Consultations in progress at this branch. The list refreshes every 30 seconds.
      </p>
      <OpenVisitsTable
        rows={visits.data}
        zone={zone}
        isLoading={visits.isLoading}
        isError={visits.isError}
        onRetry={() => void visits.refetch()}
      />
    </div>
  );
}
