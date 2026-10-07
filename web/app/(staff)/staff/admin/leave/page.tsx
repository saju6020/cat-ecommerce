'use client';

import { Suspense } from 'react';
import { useRouter, usePathname, useSearchParams } from 'next/navigation';
import {
  useAdminLeaveRequests,
  useApproveLeaveRequest,
  useRejectLeaveRequest,
  useCancelLeaveRequest,
} from '@/lib/hooks/useLeave';
import { LeaveApprovalsTable } from '@/components/leave/LeaveApprovalsTable';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ApiForbiddenError } from '@/lib/api/client';
import { useStaffAccounts } from '@/lib/hooks/useStaffAdmin';
import type { LeaveStatus } from '@/lib/api/leave';

const PAGE_SIZE = 20;

/** `/staff/admin/leave` — US-ADM-03, `Leave.Decide`. Filters live in the URL (UI-4). */
function LeaveApprovalsContent() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const status = (searchParams.get('status') as LeaveStatus | null) ?? 'Requested';
  const staffAccountId = searchParams.get('staffAccountId') ?? undefined;
  const from = searchParams.get('from') ?? undefined;
  const to = searchParams.get('to') ?? undefined;
  const pageNumber = Number(searchParams.get('page') ?? '1') || 1;

  const { data, isLoading, isError, error, refetch } = useAdminLeaveRequests({
    status,
    staffAccountId,
    from,
    to,
    pageNumber,
    pageSize: PAGE_SIZE,
  });
  const approveMutation = useApproveLeaveRequest();
  const rejectMutation = useRejectLeaveRequest();
  const cancelMutation = useCancelLeaveRequest();
  const staffAccounts = useStaffAccounts({ pageSize: 100 });

  if (error instanceof ApiForbiddenError) return <PermissionDeniedState />;

  function setParam(name: string, value: string) {
    const params = new URLSearchParams(searchParams.toString());
    if (value) params.set(name, value);
    else params.delete(name);
    params.delete('page');
    router.replace(`${pathname}?${params.toString()}`);
  }

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-xl font-semibold text-ink">Leave approvals</h1>

      <div className="flex flex-wrap gap-3">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="leave-status-filter" className="text-sm font-semibold text-ink">
            Status
          </label>
          <select
            id="leave-status-filter"
            value={status}
            onChange={(e) => setParam('status', e.target.value)}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          >
            <option value="Requested">Requested</option>
            <option value="Approved">Approved</option>
            <option value="Rejected">Rejected</option>
            <option value="Cancelled">Cancelled</option>
          </select>
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="leave-person-filter" className="text-sm font-semibold text-ink">
            Person
          </label>
          <select
            id="leave-person-filter"
            value={staffAccountId ?? ''}
            onChange={(e) => setParam('staffAccountId', e.target.value)}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          >
            <option value="">Everyone</option>
            {(staffAccounts.data?.items ?? []).map((s) => (
              <option key={s.id} value={s.id}>
                {s.fullName}
              </option>
            ))}
          </select>
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="leave-from-filter" className="text-sm font-semibold text-ink">
            From
          </label>
          <input
            id="leave-from-filter"
            type="date"
            value={from ?? ''}
            onChange={(e) => setParam('from', e.target.value)}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="leave-to-filter" className="text-sm font-semibold text-ink">
            To
          </label>
          <input
            id="leave-to-filter"
            type="date"
            value={to ?? ''}
            onChange={(e) => setParam('to', e.target.value)}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          />
        </div>
      </div>

      <LeaveApprovalsTable
        requests={data?.items}
        totalCount={data?.totalCount}
        pageSize={PAGE_SIZE}
        isLoading={isLoading}
        isError={isError}
        onRetry={() => refetch()}
        onApprove={(request) => approveMutation.mutateAsync(request.id)}
        onReject={(request, note) =>
          rejectMutation.mutateAsync({ leaveRequestId: request.id, decisionNote: note })
        }
        onCancel={(request) => cancelMutation.mutateAsync(request.id)}
      />
    </div>
  );
}

export default function LeaveApprovalsPage() {
  return (
    <Suspense>
      <LeaveApprovalsContent />
    </Suspense>
  );
}
