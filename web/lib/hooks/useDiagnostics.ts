import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  attachDiagnosticFile,
  cancelDiagnosticOrder,
  getDiagnosticOrder,
  getPendingDiagnostics,
  orderDiagnostic,
  recordResults,
  type OrderDiagnosticRequest,
  type RecordResultsRequest,
} from '@/lib/api/diagnostics';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { useBranchScope } from './useBranchScope';
import { clinicalHistoryKey, invalidateVisit } from './useVisit';

/**
 * `lib/hooks/useDiagnostics.ts` — HO-EMR-002 §8.3 (US-CLN-14). `['diagnostic', orderId]` holds
 * signed attachment URLs, so it has a 4-minute `staleTime` (URLs live 5); the pending list is
 * polled every 30 s without resetting anything (UI-5).
 */

const POLL_MS = 30_000;
const SIGNED_URL_STALE_MS = 4 * 60_000;

function noRetryOnRefusal(failureCount: number, error: unknown) {
  if (error instanceof ApiForbiddenError || error instanceof ApiNotFoundError) return false;
  return failureCount < 1;
}

export function diagnosticQueryKey(orderId: string) {
  return ['diagnostic', orderId] as const;
}

export function useDiagnosticOrder(orderId: string | null) {
  return useQuery({
    queryKey: diagnosticQueryKey(orderId ?? ''),
    queryFn: () => getDiagnosticOrder(orderId as string),
    enabled: !!orderId,
    staleTime: SIGNED_URL_STALE_MS,
    retry: noRetryOnRefusal,
  });
}

export function usePendingDiagnostics() {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['diagnostics', 'pending', branchId],
    queryFn: getPendingDiagnostics,
    enabled: ready,
    refetchInterval: POLL_MS,
    retry: noRetryOnRefusal,
  });
}

export function useOrderDiagnostic(visitId: string, catId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: OrderDiagnosticRequest) => orderDiagnostic(visitId, request),
    onSuccess: () => {
      invalidateVisit(queryClient, visitId, catId);
      void queryClient.invalidateQueries({ queryKey: ['diagnostics'] });
    },
  });
}

function useAfterOrderWrite(orderId: string, visitId: string, catId: string) {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: diagnosticQueryKey(orderId) });
    void queryClient.invalidateQueries({ queryKey: ['diagnostics'] });
    void queryClient.invalidateQueries({ queryKey: clinicalHistoryKey(catId) });
    void queryClient.invalidateQueries({ queryKey: ['visit', visitId] });
  };
}

export function useRecordResults(orderId: string, visitId: string, catId: string) {
  const after = useAfterOrderWrite(orderId, visitId, catId);
  return useMutation({
    mutationFn: (request: RecordResultsRequest) => recordResults(orderId, request),
    onSuccess: after,
  });
}

export function useAttachDiagnosticFile(orderId: string, visitId: string, catId: string) {
  const after = useAfterOrderWrite(orderId, visitId, catId);
  return useMutation({
    mutationFn: (request: { fileId: string; resultDate: string; note?: string }) =>
      attachDiagnosticFile(orderId, request),
    onSuccess: after,
  });
}

export function useCancelDiagnosticOrder(orderId: string, visitId: string, catId: string) {
  const after = useAfterOrderWrite(orderId, visitId, catId);
  return useMutation({
    mutationFn: (reason: string) => cancelDiagnosticOrder(orderId, reason),
    onSuccess: after,
  });
}
