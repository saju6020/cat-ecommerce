'use client';

import Link from 'next/link';
import { EmptyState } from '@/components/common/EmptyState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { Button } from '@/components/ui/button';
import { formatAppointmentWhen } from '@/lib/format/appointment';
import type { MySurgerySummary } from '@/lib/api/my-surgeries';
import { SurgeryStatusBadge } from './SurgeryStatusBadge';

/**
 * `MySurgeriesList` — HO-SRG-002 §11.9, US-SRG-07. The owner's surgeries (upcoming or past): cat,
 * procedure, date in the branch's zone and the status as text (*In our care* covers pre-op, theatre and
 * recovery — the owner is never shown clinical progress). Nothing here asks for a payment.
 */
export function MySurgeriesList({
  rows,
  isLoading,
  isError,
  onRetry,
  scope,
  hasMore,
  onMore,
}: {
  rows: MySurgerySummary[] | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  scope: 'upcoming' | 'past';
  hasMore: boolean;
  onMore: () => void;
}) {
  if (isLoading) return <LoadingState variant="card" rows={3} label="Loading your surgeries…" />;
  if (isError && !rows)
    return <ErrorState title="We couldn’t load your surgeries." onRetry={onRetry} />;
  if (!rows || rows.length === 0) {
    return (
      <EmptyState
        title={scope === 'upcoming' ? 'No surgeries coming up.' : 'No past surgeries.'}
        description={
          scope === 'upcoming'
            ? 'When the clinic books a surgery for one of your cats, it will appear here with what to do before the day.'
            : undefined
        }
      />
    );
  }
  return (
    <div className="flex flex-col gap-3">
      <ul
        aria-label={scope === 'upcoming' ? 'Upcoming surgeries' : 'Past surgeries'}
        className="flex flex-col gap-3"
      >
        {rows.map((row) => (
          <li
            key={row.id}
            className="flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4"
          >
            <div className="min-w-0">
              <Link
                href={`/portal/surgeries/${row.id}`}
                className="font-display text-lg font-semibold text-ink hover:underline"
              >
                {row.catName} — {row.procedure}
              </Link>
              <p className="text-sm text-ink-muted">
                {formatAppointmentWhen(row.startsOn, row.branchTimeZoneId)} · {row.branch}
              </p>
            </div>
            <SurgeryStatusBadge status={row.status} />
          </li>
        ))}
      </ul>
      {hasMore && (
        <div>
          <Button variant="secondary" onClick={onMore}>
            Show more
          </Button>
        </div>
      )}
    </div>
  );
}
