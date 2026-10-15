'use client';

import { use } from 'react';
import Link from 'next/link';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { buttonVariants } from '@/components/ui/button';
import { DischargeSummaryView } from '@/components/visit/DischargeSummaryView';
import { VisitCatHeader } from '@/components/visit/VisitCatHeader';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { getDischargeSummaryPdf } from '@/lib/api/visits';
import { useDischargeSummary } from '@/lib/hooks/useVisit';

/**
 * `/staff/visits/[visitId]/summary` — HO-EMR-002 §11.5 (US-CLN-16, ADR-FE-057): the printable discharge
 * summary. The compact cat header (UI-7) sits above it (hidden when printing); Print uses the print
 * stylesheet and Download PDF is the server's. A `404` is stated (an open visit has no summary yet).
 */
export default function VisitSummaryPage({ params }: { params: Promise<{ visitId: string }> }) {
  const { visitId } = use(params);
  const query = useDischargeSummary(visitId);

  if (query.isLoading) return <LoadingState variant="card" rows={8} label="Loading the summary…" />;
  if (query.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (query.error instanceof ApiNotFoundError) {
    return (
      <ErrorState
        headingLevel={1}
        title="There is no discharge summary yet."
        description="A summary is available once the visit has been finalised."
      />
    );
  }
  if (query.isError || !query.data)
    return <ErrorState headingLevel={1} onRetry={() => void query.refetch()} />;

  return (
    <div className="flex flex-col gap-4">
      <div className="no-print flex flex-col gap-3">
        <Link
          href={`/staff/visits/${visitId}`}
          className={buttonVariants({ variant: 'ghost', size: 'sm', className: 'w-fit' })}
        >
          Back to the visit
        </Link>
        <VisitCatHeader catId={query.data.catId} />
      </div>
      <DischargeSummaryView
        summary={query.data}
        loadPdf={() => getDischargeSummaryPdf(visitId)}
        showPrint
      />
    </div>
  );
}
