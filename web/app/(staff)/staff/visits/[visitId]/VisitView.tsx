'use client';

import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { VisitWorkspace } from '@/components/visit/VisitWorkspace';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { useVisit } from '@/lib/hooks/useVisit';

/**
 * Container for the consultation route (HO-EMR-002 §11.1, ADR-FE-049). Loading, `403`
 * (`PermissionDeniedState` — reception is not a clinical reader), `404` (also what another branch's
 * visit answers) and error are each stated. Once a visit has loaded it stays mounted through
 * refetches: only the first load shows the skeleton, so a poll can never unmount the note form.
 */
export function VisitView({ visitId }: { visitId: string }) {
  const query = useVisit(visitId);
  if (query.data) return <VisitWorkspace visit={query.data} />;
  if (query.isLoading) return <LoadingState variant="card" rows={8} label="Loading the visit…" />;
  if (query.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (query.error instanceof ApiNotFoundError) {
    return (
      <ErrorState
        headingLevel={1}
        title="We couldn’t find this visit."
        description="It may have been removed, or it belongs to another branch."
      />
    );
  }
  return <ErrorState headingLevel={1} onRetry={() => void query.refetch()} />;
}
