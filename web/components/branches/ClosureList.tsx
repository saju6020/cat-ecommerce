'use client';

import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { EmptyState } from '@/components/common/EmptyState';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { formatDateOnly, todayInZone } from '@/lib/format/time';
import { describeApiError } from '@/lib/api/errors';
import type { BranchClosure } from '@/lib/api/admin-branches';

/** `components/branches/ClosureList.tsx` — HO-ADM-002 §11.2, US-ADM-01. Upcoming closures can be
 *  cancelled (confirm names the dates); past closures are read-only. */
export interface ClosureListProps {
  timeZoneId: string;
  closures: BranchClosure[] | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  onCancel: (closureId: string) => Promise<unknown>;
}

export function ClosureList({
  timeZoneId,
  closures,
  isLoading,
  isError,
  onRetry,
  onCancel,
}: ClosureListProps) {
  const [pendingCancel, setPendingCancel] = useState<BranchClosure | null>(null);
  const [cancelError, setCancelError] = useState<string | null>(null);
  const today = todayInZone(timeZoneId);

  if (isLoading) return <LoadingState variant="table" rows={4} />;
  if (isError) return <ErrorState onRetry={onRetry} />;
  if (!closures || closures.length === 0) {
    return <EmptyState title="No closures or holidays recorded" />;
  }

  const upcoming = closures.filter((c) => !c.isCancelled && c.endsOn >= today);
  const past = closures.filter((c) => c.isCancelled || c.endsOn < today);

  async function handleConfirm() {
    if (!pendingCancel) return;
    setCancelError(null);
    try {
      await onCancel(pendingCancel.id);
      setPendingCancel(null);
    } catch (error) {
      setCancelError(describeApiError(error));
    }
  }

  function renderRow(closure: BranchClosure, isPast: boolean) {
    const dateLabel =
      closure.startsOn === closure.endsOn
        ? formatDateOnly(closure.startsOn)
        : `${formatDateOnly(closure.startsOn)} – ${formatDateOnly(closure.endsOn)}`;
    return (
      <li
        key={closure.id}
        className="flex flex-wrap items-center justify-between gap-2 rounded-[var(--radius-md)] border border-border p-3"
      >
        <div>
          <p className="text-sm font-semibold text-ink">
            {dateLabel} · {closure.kind === 'PublicHoliday' ? 'Public holiday' : 'Closure'}
          </p>
          <p className="text-sm text-ink-muted">{closure.reason}</p>
          {closure.partialFrom && closure.partialTo && (
            <p className="text-xs text-ink-muted">
              Half-day: {closure.partialFrom}–{closure.partialTo} ({timeZoneId})
            </p>
          )}
        </div>
        {closure.isCancelled && <Badge variant="neutral">Cancelled</Badge>}
        {!isPast && !closure.isCancelled && (
          <Button variant="secondary" size="sm" onClick={() => setPendingCancel(closure)}>
            Cancel
          </Button>
        )}
      </li>
    );
  }

  return (
    <div className="flex flex-col gap-5">
      <div>
        <h2 className="mb-2 text-sm font-semibold text-ink">Upcoming</h2>
        {upcoming.length === 0 ? (
          <p className="text-sm text-ink-muted">None scheduled.</p>
        ) : (
          <ul className="flex flex-col gap-2">{upcoming.map((c) => renderRow(c, false))}</ul>
        )}
      </div>
      <div>
        <h2 className="mb-2 text-sm font-semibold text-ink">Past</h2>
        {past.length === 0 ? (
          <p className="text-sm text-ink-muted">None recorded.</p>
        ) : (
          <ul className="flex flex-col gap-2">{past.map((c) => renderRow(c, true))}</ul>
        )}
      </div>

      {cancelError && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {cancelError}
        </p>
      )}

      <ConfirmDialog
        open={!!pendingCancel}
        onOpenChange={(open) => !open && setPendingCancel(null)}
        title="Cancel this closure?"
        consequence={
          pendingCancel
            ? `The branch will be open as usual on ${
                pendingCancel.startsOn === pendingCancel.endsOn
                  ? formatDateOnly(pendingCancel.startsOn)
                  : `${formatDateOnly(pendingCancel.startsOn)} – ${formatDateOnly(pendingCancel.endsOn)}`
              }.`
            : ''
        }
        confirmLabel="Cancel closure"
        variant="destructive"
        onConfirm={handleConfirm}
      />
    </div>
  );
}
