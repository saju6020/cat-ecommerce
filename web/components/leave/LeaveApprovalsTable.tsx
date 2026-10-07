'use client';

import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { DataTable } from '@/components/common/DataTable';
import { formatDateOnly } from '@/lib/format/time';
import { describeApiError } from '@/lib/api/errors';
import type { LeaveRequest } from '@/lib/api/leave';

const STATUS_VARIANT = {
  Requested: 'neutral',
  Approved: 'success',
  Rejected: 'danger',
  Cancelled: 'neutral',
} as const;

/**
 * `components/leave/LeaveApprovalsTable.tsx` — HO-ADM-002 §11.4, US-ADM-03. Filters (status,
 * person, range) are the page's own URL-param state (§11.7's convention); this component takes
 * the already-filtered page of results plus the decision callbacks. Approve/reject/cancel each
 * confirm, naming the person and dates. The sole-administrator self-approval case shows the
 * server's own note (`decisionNote`), never re-explained here.
 */
export interface LeaveApprovalsTableProps {
  requests: LeaveRequest[] | undefined;
  totalCount: number | undefined;
  pageSize: number;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  onApprove: (request: LeaveRequest) => Promise<unknown>;
  onReject: (request: LeaveRequest, note?: string) => Promise<unknown>;
  onCancel: (request: LeaveRequest) => Promise<unknown>;
}

/** HO-ADM-001 `LeaveRules.DecisionNoteMaxLength`. */
const LEAVE_DECISION_NOTE_MAX_LENGTH = 500;

type PendingAction =
  | { kind: 'approve' | 'cancel'; request: LeaveRequest }
  | { kind: 'reject'; request: LeaveRequest; note: string };

export function LeaveApprovalsTable({
  requests,
  totalCount,
  pageSize,
  isLoading,
  isError,
  onRetry,
  onApprove,
  onReject,
  onCancel,
}: LeaveApprovalsTableProps) {
  const [pending, setPending] = useState<PendingAction | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleConfirm() {
    if (!pending) return;
    setError(null);
    try {
      if (pending.kind === 'approve') await onApprove(pending.request);
      else if (pending.kind === 'reject')
        await onReject(pending.request, pending.note.trim() || undefined);
      else await onCancel(pending.request);
      setPending(null);
    } catch (err) {
      setError(describeApiError(err));
    }
  }

  const summary = pending
    ? `${pending.kind === 'approve' ? 'Approve' : pending.kind === 'reject' ? 'Reject' : 'Cancel'} ${pending.request.staffFullName ?? pending.request.staffAccountId}'s ${pending.request.kind.toLowerCase()} leave for ${formatDateOnly(pending.request.startsOn)} – ${formatDateOnly(pending.request.endsOn)}?`
    : '';

  // §11.4 "reject (with note)": the note is optional server-side (HO-ADM-001, max 500 characters).
  const consequence =
    pending?.kind === 'reject' ? (
      <span className="flex flex-col gap-2">
        <span>{summary}</span>
        <label htmlFor="leave-reject-note" className="text-sm font-semibold text-ink">
          Note to the staff member (optional)
        </label>
        <textarea
          id="leave-reject-note"
          rows={3}
          maxLength={LEAVE_DECISION_NOTE_MAX_LENGTH}
          value={pending.note}
          onChange={(event) => setPending({ ...pending, note: event.target.value })}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        />
      </span>
    ) : (
      summary
    );

  return (
    <div className="flex flex-col gap-3">
      {error && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {error}
        </p>
      )}

      <DataTable
        caption="Leave requests"
        rows={requests}
        getRowId={(row) => row.id}
        isLoading={isLoading}
        isError={isError}
        onRetry={onRetry}
        emptyState={{ title: 'No leave requests match these filters' }}
        pagination={totalCount !== undefined ? { totalCount, pageSize } : undefined}
        columns={[
          {
            id: 'staff',
            header: 'Staff member',
            cell: (row) => row.staffFullName ?? row.staffAccountId,
          },
          {
            id: 'dates',
            header: 'Dates',
            cell: (row) => `${formatDateOnly(row.startsOn)} – ${formatDateOnly(row.endsOn)}`,
          },
          { id: 'kind', header: 'Kind', cell: (row) => row.kind },
          {
            id: 'status',
            header: 'Status',
            cell: (row) => <Badge variant={STATUS_VARIANT[row.status]}>{row.status}</Badge>,
          },
          { id: 'note', header: 'Note', cell: (row) => row.decisionNote ?? row.note ?? '' },
          {
            id: 'actions',
            header: 'Actions',
            cell: (row) => (
              <div className="flex flex-wrap gap-2">
                {row.status === 'Requested' && (
                  <>
                    <Button size="sm" onClick={() => setPending({ kind: 'approve', request: row })}>
                      Approve
                    </Button>
                    <Button
                      size="sm"
                      variant="secondary"
                      onClick={() => setPending({ kind: 'reject', request: row, note: '' })}
                    >
                      Reject
                    </Button>
                  </>
                )}
                {row.status === 'Approved' && (
                  <Button
                    size="sm"
                    variant="secondary"
                    onClick={() => setPending({ kind: 'cancel', request: row })}
                  >
                    Cancel
                  </Button>
                )}
              </div>
            ),
          },
        ]}
      />

      <ConfirmDialog
        open={!!pending}
        onOpenChange={(open) => !open && setPending(null)}
        title={
          pending?.kind === 'approve'
            ? 'Approve leave?'
            : pending?.kind === 'reject'
              ? 'Reject leave?'
              : 'Cancel approved leave?'
        }
        consequence={consequence}
        confirmLabel={
          pending?.kind === 'approve'
            ? 'Approve'
            : pending?.kind === 'reject'
              ? 'Reject'
              : 'Cancel leave'
        }
        variant={pending?.kind === 'approve' ? 'primary' : 'destructive'}
        onConfirm={handleConfirm}
      />
    </div>
  );
}
