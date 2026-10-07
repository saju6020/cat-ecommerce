import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getMyLeaveRequests,
  requestLeave,
  cancelMyLeaveRequest,
  getAdminLeaveRequests,
  createLeaveRequestForStaff,
  approveLeaveRequest,
  rejectLeaveRequest,
  cancelLeaveRequest,
  type RequestLeaveRequest,
  type GetAdminLeaveRequestsParams,
} from '@/lib/api/leave';
import { ApiForbiddenError } from '@/lib/api/client';

function noRetryOn403(failureCount: number, error: unknown) {
  return !(error instanceof ApiForbiddenError) && failureCount < 1;
}

export function useMyLeaveRequests() {
  return useQuery({
    queryKey: ['admin', 'my-leave-requests'],
    queryFn: getMyLeaveRequests,
  });
}

export function useRequestLeave() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: RequestLeaveRequest) => requestLeave(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'my-leave-requests'] }),
  });
}

export function useCancelMyLeaveRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (leaveRequestId: string) => cancelMyLeaveRequest(leaveRequestId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'my-leave-requests'] }),
  });
}

export function useAdminLeaveRequests(params: GetAdminLeaveRequestsParams) {
  return useQuery({
    queryKey: ['admin', 'leave-requests', params],
    queryFn: () => getAdminLeaveRequests(params),
    retry: noRetryOn403,
  });
}

export function useCreateLeaveRequestForStaff() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: RequestLeaveRequest) => createLeaveRequestForStaff(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'leave-requests'] }),
  });
}

function invalidateLeaveLists(queryClient: ReturnType<typeof useQueryClient>) {
  void queryClient.invalidateQueries({ queryKey: ['admin', 'leave-requests'] });
  void queryClient.invalidateQueries({ queryKey: ['admin', 'my-leave-requests'] });
  // Approving/rejecting/cancelling leave changes what the roster view shows as "On leave".
  void queryClient.invalidateQueries({ predicate: (q) => q.queryKey.includes('availability') });
}

export function useApproveLeaveRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (leaveRequestId: string) => approveLeaveRequest(leaveRequestId),
    onSuccess: () => invalidateLeaveLists(queryClient),
  });
}

export function useRejectLeaveRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      leaveRequestId,
      decisionNote,
    }: {
      leaveRequestId: string;
      decisionNote?: string;
    }) => rejectLeaveRequest(leaveRequestId, decisionNote),
    onSuccess: () => invalidateLeaveLists(queryClient),
  });
}

export function useCancelLeaveRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (leaveRequestId: string) => cancelLeaveRequest(leaveRequestId),
    onSuccess: () => invalidateLeaveLists(queryClient),
  });
}
