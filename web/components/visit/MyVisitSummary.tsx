'use client';

import Link from 'next/link';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { buttonVariants } from '@/components/ui/button';
import { DischargeSummaryView } from './DischargeSummaryView';
import { ApiNotFoundError } from '@/lib/api/client';
import { getMyVisitSummaryPdfUrl } from '@/lib/api/my-visits';
import { useMyVisitSummary } from '@/lib/hooks/useMyVisits';

/**
 * ⭐ `MyVisitSummary` — HO-EMR-002 §11.5 (US-CLN-16), the owner's `/portal/visits/[visitId]`: the
 * discharge summary and its PDF. A `404` — not their cat, not finalised, or no such visit — is ONE
 * neutral message that hints at nothing: *"We couldn't find this visit summary."* (U-G2, E-G2).
 */
export function MyVisitSummary({ visitId }: { visitId: string }) {
  const query = useMyVisitSummary(visitId);
  if (query.isLoading) return <LoadingState variant="card" rows={6} label="Loading the summary…" />;
  if (query.error instanceof ApiNotFoundError) {
    return (
      <ErrorState
        headingLevel={1}
        title="We couldn’t find this visit summary."
        description="Check the link, or choose a visit from your cat’s list."
      />
    );
  }
  if (query.isError || !query.data) return <ErrorState onRetry={() => void query.refetch()} />;
  return (
    <div className="flex flex-col gap-4">
      <div className="no-print">
        <Link
          href={`/portal/cats/${query.data.catId}/visits`}
          className={buttonVariants({ variant: 'ghost', size: 'sm' })}
        >
          Back to {query.data.catName}’s visits
        </Link>
      </div>
      <DischargeSummaryView
        summary={query.data}
        pdfUrl={getMyVisitSummaryPdfUrl(visitId)}
        showPrint={false}
      />
    </div>
  );
}
