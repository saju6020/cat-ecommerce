import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getErasureRequest,
  getErasureRequests,
  getRetentionReview,
  getRetentionRun,
  getRetentionRuns,
  getRetentionSchedule,
  reapplyErasures,
  requestRetentionRun,
  type ErasureRequestStatus,
  type ReapplyRequest,
} from '@/lib/api/retention';
import { eraseCustomer, type EraseCustomerRequest } from '@/lib/api/admin-erasure';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';

/** `lib/hooks/useRetention.ts` — HO-XCT-002 §8.4. One TanStack query per resource; a mutation
 *  invalidates its own resource. A `403`/`404` is final, so it is never retried. */
const retry = (failureCount: number, error: unknown) =>
  !(error instanceof ApiForbiddenError || error instanceof ApiNotFoundError) && failureCount < 1;

export const useRetentionSchedule = () =>
  useQuery({ queryKey: ['admin', 'retention', 'schedule'], queryFn: getRetentionSchedule, retry });

export const useRetentionReview = () =>
  useQuery({ queryKey: ['admin', 'retention', 'review'], queryFn: getRetentionReview, retry });

export const useRetentionRuns = (pageNumber: number, pageSize: number) =>
  useQuery({
    queryKey: ['admin', 'retention', 'runs', pageNumber, pageSize],
    queryFn: () => getRetentionRuns({ pageNumber, pageSize }),
    retry,
  });

export const useRetentionRun = (runId: string) =>
  useQuery({
    queryKey: ['admin', 'retention', 'run', runId],
    queryFn: () => getRetentionRun(runId),
    retry,
  });

export function useRequestRetentionRun() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: requestRetentionRun,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'retention'] }),
  });
}

export const useErasureRequests = (
  pageNumber: number,
  pageSize: number,
  status?: ErasureRequestStatus,
) =>
  useQuery({
    queryKey: ['admin', 'retention', 'erasures', pageNumber, pageSize, status ?? 'all'],
    queryFn: () => getErasureRequests({ pageNumber, pageSize, status }),
    retry,
  });

export const useErasureRequest = (customerAccountId: string) =>
  useQuery({
    queryKey: ['admin', 'retention', 'erasure', customerAccountId],
    queryFn: () => getErasureRequest(customerAccountId),
    retry,
  });

export function useEraseCustomer(customerAccountId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: EraseCustomerRequest) => eraseCustomer(customerAccountId, request),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['staff-customers'] });
      await queryClient.invalidateQueries({ queryKey: ['admin', 'retention'] });
    },
  });
}

export function useReapplyErasures() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ReapplyRequest) => reapplyErasures(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'retention'] }),
  });
}
