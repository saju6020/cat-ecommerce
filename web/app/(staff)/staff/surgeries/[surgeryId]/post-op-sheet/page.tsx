'use client';

import { use } from 'react';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { Banner } from '@/components/inventory/fields';
import { PostOpSheet } from '@/components/recovery/PostOpSheet';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { useSurgery } from '@/lib/hooks/useSurgeries';

/** `/staff/surgeries/[surgeryId]/post-op-sheet` — HO-SRG-002 §11.6, ADR-FE-076: the printable post-op sheet. */
export default function PostOpSheetPage({ params }: { params: Promise<{ surgeryId: string }> }) {
  const { surgeryId } = use(params);
  const surgery = useSurgery(surgeryId);
  if (surgery.error instanceof ApiForbiddenError)
    return <AccessRefusedState error={surgery.error} />;
  if (surgery.error instanceof ApiNotFoundError) {
    return <ErrorState headingLevel={1} title="We couldn’t find that surgery." />;
  }
  if (surgery.isPending) return <LoadingState variant="card" rows={4} label="Loading the sheet…" />;
  if (!surgery.data) {
    return (
      <ErrorState
        headingLevel={1}
        title="We couldn’t load the sheet."
        onRetry={() => void surgery.refetch()}
      />
    );
  }
  if (surgery.data.status !== 'Discharged') {
    return (
      <Banner tone="neutral">
        The post-op sheet is available once {surgery.data.catName} has been discharged.
      </Banner>
    );
  }
  return <PostOpSheet surgery={surgery.data} />;
}
