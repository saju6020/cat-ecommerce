'use client';

import { useMyLeaveRequests, useRequestLeave, useCancelMyLeaveRequest } from '@/lib/hooks/useLeave';
import { LeaveRequestForm } from '@/components/leave/LeaveRequestForm';
import { MyLeaveList } from '@/components/leave/MyLeaveList';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { ApiForbiddenError } from '@/lib/api/client';

/** `/staff/leave` — US-ADM-03, every staff member. */
export default function MyLeavePage() {
  const requests = useMyLeaveRequests();
  const requestMutation = useRequestLeave();
  const cancelMutation = useCancelMyLeaveRequest();

  if (requests.error instanceof ApiForbiddenError)
    return <AccessRefusedState error={requests.error} />;

  return (
    <div className="flex flex-col gap-8">
      <h1 className="font-display text-xl font-semibold text-ink">My leave</h1>
      <LeaveRequestForm
        isPending={requestMutation.isPending}
        onSubmitRequest={(request) => requestMutation.mutateAsync(request)}
      />
      <MyLeaveList
        requests={requests.data}
        isLoading={requests.isLoading}
        isError={requests.isError}
        onRetry={() => requests.refetch()}
        onCancel={(id) => cancelMutation.mutateAsync(id)}
      />
    </div>
  );
}
