'use client';

import { use } from 'react';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { ConsignmentDocuments } from '@/components/procurement/ConsignmentDocuments';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useConsignment, useConsignmentActions } from '@/lib/hooks/useProcurement';

/** `/staff/procurement/consignments/[consignmentId]/documents` — US-IMP-03, NFR-LEG-2. */
export default function ConsignmentDocumentsPage({ params }: { params: Promise<{ consignmentId: string }> }) {
  const { consignmentId } = use(params);
  const query = useConsignment(consignmentId);
  const actions = useConsignmentActions(consignmentId);
  const zone = useBranchTimeZone();
  return (
    <QueryBoundary query={query}>
      {(c) => (
        <ConsignmentDocuments
          documents={c.documents}
          canAdd={c.allowedActions.includes('documents')}
          timeZoneId={zone}
          onAdd={(request) => actions.addDocument.mutateAsync(request)}
          onSupersede={(documentId, request) => actions.supersedeDocument.mutateAsync({ documentId, request })}
        />
      )}
    </QueryBoundary>
  );
}
