'use client';

import { use } from 'react';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { MySurgeryDetail } from '@/components/surgery/MySurgeryDetail';
import { ApiNotFoundError } from '@/lib/api/client';
import { useMySurgery } from '@/lib/hooks/useSurgeryCatalogue';

/** `/portal/surgeries/[surgeryId]` — HO-SRG-002 §11.9: before, during and after a surgery. Another customer's id is a plain "not found". */
export default function MySurgeryPage({ params }: { params: Promise<{ surgeryId: string }> }) {
  const { surgeryId } = use(params);
  const surgery = useMySurgery(surgeryId);
  if (surgery.isLoading)
    return <LoadingState variant="card" rows={3} label="Loading your surgery…" />;
  if (surgery.error instanceof ApiNotFoundError) {
    return (
      <ErrorState
        headingLevel={1}
        title="We couldn’t find that surgery."
        description="Check the link, or go back to your surgeries."
      />
    );
  }
  if (!surgery.data) {
    return (
      <ErrorState
        headingLevel={1}
        title="We couldn’t load this surgery."
        onRetry={() => void surgery.refetch()}
      />
    );
  }
  return <MySurgeryDetail surgery={surgery.data} />;
}
