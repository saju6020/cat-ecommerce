'use client';

import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { EmptyState } from '@/components/common/EmptyState';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { formatDateOnly } from '@/lib/format/time';
import { describeApiError } from '@/lib/api/errors';
import type { LeaveRequest } from '@/lib/api/leave';

const STATUS_VARIANT = {
  Requested: 'neutral',
  Approved: 'success',
  Rejected: 'danger',
  Cancelled: 'neutral',
} as const;

/** `components/leave/MyLeaveList.tsx` — HO-ADM-002 §11.4, US-ADM-03. Every staff member's own
 *  leave requests; cancel while still `Requested`. */
export interface MyLeaveListProps {
  requests: LeaveRequest[] | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  onCancel: (leaveRequestId: string) => Promise<unknown>;
}

export function MyLeaveList({ requests, isLoading, isError, onRetry, onCancel }: MyLeaveListProps) {
  const [pendingCancel, setPendingCancel] = useState<LeaveRequest | null>(null);
  const [error, setError] = useState<string | null>(null);

  if (isLoading) return <LoadingState variant="table" rows={3} />;
  if (isError) return <ErrorState onRetry={onRetry} />;
  if (!requests || requests.length === 0) return <EmptyState title="No leave requested yet" />;

  async function handleConfirm() {
    if (!pendingCancel) return;
    setError(null);
    try {
      await onCancel(pendingCancel.id);
      setPendingCancel(null);
    } catch (err) {
      setError(describeApiError(err));
    }
  }

  return (
    <div className="flex flex-col gap-3">
      {error && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {error}
        </p>
      )}
      <ul className="flex flex-col gap-2">
        {requests.map((request) => (
          <li
            key={request.id}
            className="flex flex-wrap items-center justify-between gap-2 rounded-[var(--radius-md)] border border-border p-3"
          >
            <div>
              <p className="text-sm font-semibold text-ink">
                {formatDateOnly(request.startsOn)} – {formatDateOnly(request.endsOn)} ·{' '}
                {request.kind}
              </p>
              {request.note && <p className="text-sm text-ink-muted">{request.note}</p>}
              {request.decisionNote && (
                <p className="text-xs text-ink-muted">{request.decisionNote}</p>
              )}
            </div>
            <div className="flex items-center gap-2">
              <Badge variant={STATUS_VARIANT[request.status]}>{request.status}</Badge>
              {request.status === 'Requested' && (
                <Button variant="secondary" size="sm" onClick={() => setPendingCancel(request)}>
                  Cancel
                </Button>
              )}
            </div>
          </li>
        ))}
      </ul>

      <ConfirmDialog
        open={!!pendingCancel}
        onOpenChange={(open) => !open && setPendingCancel(null)}
        title="Cancel this leave request?"
        consequence={
          pendingCancel
            ? `Your ${pendingCancel.kind.toLowerCase()} leave request for ${formatDateOnly(pendingCancel.startsOn)} – ${formatDateOnly(pendingCancel.endsOn)} will be cancelled.`
            : ''
        }
        confirmLabel="Cancel request"
        variant="destructive"
        onConfirm={handleConfirm}
      />
    </div>
  );
}
