'use client';

import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import {
  approveReturn,
  getStaffReturn,
  listStaffReturns,
  receiveReturn,
  rejectReturn,
  requestReturnForCustomer,
  type ApproveReturnInput,
} from '@/lib/api/staff-returns';
import type { ReturnLineInput } from '@/lib/api/my-orders';
import {
  getRefundOptions,
  issueRefund,
  listRefundsDue,
  type IssueRefundInput,
} from '@/lib/api/staff-refunds';
import { useBranchScope } from './useBranchScope';
import { STAFF_ORDER_POLL_MS } from './useStaffOrders';

/**
 * `lib/hooks/useStaffReturns.ts` — HO-ORD-002 §8.3. Returns and refunds for staff. There is no
 * stock-decision hook: restock / write-off left Ordering (HO-ORD-001 gate fix 3).
 */
function noRetryOnRefusal(failureCount: number, error: unknown) {
  if (error instanceof ApiForbiddenError || error instanceof ApiNotFoundError) return false;
  return failureCount < 1;
}

export function useStaffReturns(filters: { status?: string; pageNumber?: number; pageSize?: number }) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['staff', 'returns', branchId, filters],
    queryFn: () => listStaffReturns(filters),
    enabled: ready,
    placeholderData: keepPreviousData,
    retry: noRetryOnRefusal,
    refetchInterval: STAFF_ORDER_POLL_MS,
  });
}

export function useStaffReturn(returnRequestId: string) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['staff', 'return', branchId, returnRequestId],
    queryFn: () => getStaffReturn(returnRequestId),
    enabled: ready && !!returnRequestId,
    retry: noRetryOnRefusal,
  });
}

function useRefreshReturns() {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: ['staff', 'returns'] });
    void queryClient.invalidateQueries({ queryKey: ['staff', 'return'] });
    void queryClient.invalidateQueries({ queryKey: ['staff', 'refunds-due'] });
    void queryClient.invalidateQueries({ queryKey: ['staff', 'order'] });
  };
}

export function useApproveReturn(returnRequestId: string) {
  const refresh = useRefreshReturns();
  return useMutation({
    mutationFn: (input: ApproveReturnInput) => approveReturn(returnRequestId, input),
    onSuccess: refresh,
  });
}

export function useRejectReturn(returnRequestId: string) {
  const refresh = useRefreshReturns();
  return useMutation({
    mutationFn: (note: string) => rejectReturn(returnRequestId, note),
    onSuccess: refresh,
  });
}

export function useReceiveReturn(returnRequestId: string) {
  const refresh = useRefreshReturns();
  return useMutation({
    mutationFn: (lines: Array<{ orderLineId: string; quantityReceived: number }>) =>
      receiveReturn(returnRequestId, lines),
    onSuccess: refresh,
  });
}

export function useRequestReturnForCustomer(orderId: string) {
  const refresh = useRefreshReturns();
  return useMutation({
    mutationFn: (lines: ReturnLineInput[]) => requestReturnForCustomer(orderId, lines),
    onSuccess: refresh,
  });
}

// ---- refunds ---------------------------------------------------------------------------------------

export function useRefundsDue(filters: { pageNumber?: number; pageSize?: number }) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['staff', 'refunds-due', branchId, filters],
    queryFn: () => listRefundsDue(filters),
    enabled: ready,
    placeholderData: keepPreviousData,
    retry: noRetryOnRefusal,
    refetchInterval: STAFF_ORDER_POLL_MS,
  });
}

export function useRefundOptions(orderId: string, enabled: boolean) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['staff', 'refund-options', branchId, orderId],
    queryFn: () => getRefundOptions(orderId),
    enabled: ready && enabled && !!orderId,
    staleTime: 0,
    retry: noRetryOnRefusal,
  });
}

export function useIssueRefund(orderId: string) {
  const refresh = useRefreshReturns();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { request: IssueRefundInput; idempotencyKey: string }) =>
      issueRefund(orderId, input.request, input.idempotencyKey),
    retry: false,
    onSuccess: () => {
      refresh();
      void queryClient.invalidateQueries({ queryKey: ['staff', 'orders'] });
      void queryClient.invalidateQueries({ queryKey: ['staff', 'refund-options'] });
    },
  });
}
