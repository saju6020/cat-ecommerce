'use client';

import Link from 'next/link';
import { ErrorState } from '@/components/common/ErrorState';
import { EmptyCatIllustration, EmptyState } from '@/components/common/EmptyState';
import { LoadingState } from '@/components/common/LoadingState';
import { buttonVariants } from '@/components/ui/button';
import { ApiNotFoundError } from '@/lib/api/client';
import { useMyCatVisits, useMyLabResults } from '@/lib/hooks/useMyVisits';
import { diagnosticKindLabel } from '@/lib/format/clinical';
import { formatDateOnly, formatInstantDate } from '@/lib/format/time';
import { AttachmentList } from './panels/AttachmentList';
import { ResultTable } from './panels/ResultTable';

/**
 * ⭐ `MyCatVisits` — HO-EMR-002 §11.5 (US-CLN-16, FR-POR-3), the owner's `/portal/cats/[catId]/visits`.
 * Finalised visits only (date, service, vet, diagnoses) each linking to its summary, and a **Lab
 * results** section with the values, UNITS, ranges and flags as text, with the attachments (Open for
 * PDF/images; DICOM never reaches the owner). Warm customer layer; plain language; every state
 * (loading, empty, error, not found) is stated.
 */
export function MyCatVisits({ catId }: { catId: string }) {
  const visits = useMyCatVisits(catId);
  const labs = useMyLabResults(catId);

  if (visits.isLoading) return <LoadingState variant="card" rows={4} label="Loading the visits…" />;
  if (visits.error instanceof ApiNotFoundError) {
    return (
      <ErrorState
        title="We couldn’t find this cat."
        description="Check the link, or go back to your cats."
      />
    );
  }
  if (visits.isError || !visits.data) return <ErrorState onRetry={() => void visits.refetch()} />;

  return (
    <div className="flex flex-col gap-8">
      <section aria-labelledby="my-visits" className="flex flex-col gap-3">
        <h2 id="my-visits" className="text-lg font-bold text-ink">
          Visits
        </h2>
        {visits.data.length === 0 ? (
          <EmptyState
            icon={<EmptyCatIllustration />}
            title="No visit summaries yet."
            description="After a consultation is finished, its summary appears here."
          />
        ) : (
          <ul className="m-0 flex list-none flex-col gap-3 p-0">
            {visits.data.map((visit) => (
              <li
                key={visit.id}
                className="flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4"
              >
                <div className="min-w-0">
                  <p className="font-semibold text-ink">
                    {formatDateOnly(visit.date)} · {visit.service}
                  </p>
                  <p className="text-sm text-ink-muted">With {visit.vet}</p>
                  <p className="text-sm text-ink">
                    {visit.problems.length > 0
                      ? visit.problems.join(', ')
                      : 'No diagnosis recorded'}
                  </p>
                </div>
                {visit.hasSummary && (
                  <Link
                    href={`/portal/visits/${visit.id}`}
                    className={buttonVariants({ variant: 'secondary', size: 'sm' })}
                    aria-label={`Read the summary of the ${formatDateOnly(visit.date)} visit`}
                  >
                    Read the summary
                  </Link>
                )}
              </li>
            ))}
          </ul>
        )}
      </section>

      <section aria-labelledby="my-labs" className="flex flex-col gap-3">
        <h2 id="my-labs" className="text-lg font-bold text-ink">
          Lab results
        </h2>
        {labs.isLoading ? (
          <LoadingState variant="card" rows={3} label="Loading the lab results…" />
        ) : labs.isError || !labs.data ? (
          <ErrorState
            title="We couldn’t load the lab results."
            onRetry={() => void labs.refetch()}
          />
        ) : labs.data.length === 0 ? (
          <EmptyState
            title="No lab results yet."
            description="Results from finished visits appear here."
          />
        ) : (
          labs.data.map((lab) => (
            <article
              key={lab.orderId}
              aria-label={`${diagnosticKindLabel(lab.kind)} ordered ${formatInstantDate(lab.orderedOn)}`}
              className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4"
            >
              <h3 className="font-semibold text-ink">
                {diagnosticKindLabel(lab.kind)} · {formatInstantDate(lab.orderedOn)}
              </h3>
              <ResultTable sets={lab.resultSets} zone="UTC" showRecorder={false} />
              {lab.attachments.length > 0 && <AttachmentList attachments={lab.attachments} />}
            </article>
          ))
        )}
      </section>
    </div>
  );
}
