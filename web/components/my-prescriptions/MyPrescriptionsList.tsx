'use client';

import Link from 'next/link';
import { Quantity } from '@/components/common/Quantity';
import { EmptyCatIllustration, EmptyState } from '@/components/common/EmptyState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { Button } from '@/components/ui/button';
import type { MyPrescription } from '@/lib/api/my-prescriptions';
import { formatDay, formatInstantInZone, formLabel } from '@/lib/format/pharmacy';
import { formatRemaining } from '@/lib/format/units';

/**
 * ⭐ `MyPrescriptionsList` — HO-PHM-002 §11.8 (US-PHM-07). One card per prescription: the cat, the
 * medicine with its strength and form, the **dose and how often** (*"0.30 ml once daily"*), how many
 * days are left, the vet who issued it and the repeat status in words (*"2 repeats left, until 31 Dec"*,
 * *"Repeat requested 20 Sep"*). Each card opens its own page. No batch or lot number ever appears here.
 * The owner's pages use the warm customer layer; the list is cards, not a table, on every width.
 */
export function repeatStatusText(prescription: MyPrescription, zone: string): string | null {
  const { repeat } = prescription;
  if (repeat.openRequest) {
    return `Repeat requested ${formatInstantInZone(repeat.openRequest.requestedOn, zone).split(',')[0]}`;
  }
  if (!repeat.isRepeatable) return null;
  if (repeat.suppliesLeft > 0) {
    return `${repeat.suppliesLeft} ${repeat.suppliesLeft === 1 ? 'repeat' : 'repeats'} left, until ${formatDay(repeat.validUntil)}`;
  }
  return 'No repeats left';
}

export function MyPrescriptionsList({
  rows,
  zone,
  isLoading,
  isError,
  onRetry,
  emptyTitle,
  emptyDescription,
  page,
  pageSize,
  onPageChange,
}: {
  rows: MyPrescription[] | undefined;
  zone: string;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  emptyTitle: string;
  emptyDescription: string;
  page: number;
  pageSize: number;
  onPageChange: (page: number) => void;
}) {
  if (isLoading) return <LoadingState variant="card" rows={3} label="Loading prescriptions…" />;
  if (isError || !rows)
    return <ErrorState title="We couldn’t load your prescriptions just now." onRetry={onRetry} />;
  if (rows.length === 0)
    return (
      <EmptyState
        icon={<EmptyCatIllustration />}
        title={emptyTitle}
        description={emptyDescription}
      />
    );
  const pages = Math.max(1, Math.ceil(rows.length / pageSize));
  const visible = rows.slice((page - 1) * pageSize, page * pageSize);
  return (
    <div className="flex flex-col gap-3">
      <ul aria-label="Prescriptions" className="m-0 flex list-none flex-col gap-3 p-0">
        {visible.map((prescription) => {
          const repeatText = repeatStatusText(prescription, zone);
          return (
            <li
              key={prescription.id}
              className="rounded-[var(--radius-md)] border border-border bg-surface p-4"
            >
              <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">
                {prescription.catName}
              </p>
              <h2 className="mt-0.5 text-base font-semibold text-ink">
                <Link
                  href={`/portal/prescriptions/${prescription.id}`}
                  className="text-ink underline decoration-border-strong underline-offset-2 hover:decoration-accent"
                >
                  {prescription.medicine.name}
                </Link>{' '}
                <span className="text-sm font-normal text-ink-muted">
                  <span data-clinical-value>{prescription.medicine.strength}</span>,{' '}
                  {formLabel(prescription.medicine.form)}
                </span>
              </h2>
              <p className="mt-1 text-sm text-ink">
                <Quantity value={prescription.dose} /> {prescription.frequency}
              </p>
              <dl className="mt-2 grid gap-x-4 gap-y-1 text-sm sm:grid-cols-2">
                <div>
                  <dt className="inline text-ink-muted">Left: </dt>
                  <dd className="inline text-ink">
                    {prescription.isActive
                      ? formatRemaining(prescription.remainingDays)
                      : 'Finished'}
                  </dd>
                </div>
                <div>
                  <dt className="inline text-ink-muted">Prescribed by: </dt>
                  <dd className="inline text-ink">{prescription.issuingVetName}</dd>
                </div>
                {repeatText && (
                  <div className="sm:col-span-2">
                    <dt className="inline text-ink-muted">Repeats: </dt>
                    <dd className="inline text-ink">{repeatText}</dd>
                  </div>
                )}
              </dl>
            </li>
          );
        })}
      </ul>
      {pages > 1 && (
        <nav
          aria-label="Pages"
          className="flex items-center justify-between gap-2 text-sm text-ink-muted"
        >
          <span>
            Page {page} of {pages}
          </span>
          <div className="flex gap-2">
            <Button
              variant="secondary"
              size="sm"
              disabled={page <= 1}
              onClick={() => onPageChange(page - 1)}
            >
              Previous
            </Button>
            <Button
              variant="secondary"
              size="sm"
              disabled={page >= pages}
              onClick={() => onPageChange(page + 1)}
            >
              Next
            </Button>
          </div>
        </nav>
      )}
    </div>
  );
}
