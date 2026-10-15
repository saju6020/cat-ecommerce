import { useMutation } from '@tanstack/react-query';
import {
  closeRecall,
  declareRecall,
  getRecall,
  getRecallTrace,
  getRecalls,
  notifyRecallRecipients,
  withdrawRecall,
  type DeclareRecallRequest,
} from '@/lib/api/inventory-recalls';
import {
  closeBreach,
  getBreach,
  getBreaches,
  quarantineBreachStock,
  reportBreach,
  type ReportBreachRequest,
} from '@/lib/api/inventory-breaches';
import { useInventoryQuery, useInvalidateInventory } from './useInventoryQuery';

/** HO-IMP-002 §8.2 — recalls (ADR-FE-046) and storage breaches (US-IMP-16). */

export function useRecalls(params: { status?: string; pageNumber?: number; pageSize?: number }) {
  return useInventoryQuery('recalls', ['list', params], () => getRecalls(params));
}

export function useRecall(batchId: string) {
  return useInventoryQuery('recalls', ['detail', batchId], () => getRecall(batchId), {
    enabled: !!batchId,
  });
}

/** The trace holds customers' contact details: fetched only on the recall console, only for a
 *  holder of `Recalls.Manage` (the server refuses anyone else), kept in the query cache only. */
export function useRecallTrace(batchId: string) {
  return useInventoryQuery('recalls', ['trace', batchId], () => getRecallTrace(batchId), {
    enabled: !!batchId,
  });
}

export function useDeclareRecall(batchId: string) {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: (request: DeclareRecallRequest) => declareRecall(batchId, request),
    onSuccess: () => invalidate('recalls', 'batches', 'stock', 'fefo'),
  });
}

export function useRecallActions(batchId: string) {
  const invalidate = useInvalidateInventory();
  const done = () => invalidate('recalls', 'batches', 'stock', 'fefo');
  return {
    notify: useMutation({
      mutationFn: () => notifyRecallRecipients(batchId),
      onSuccess: done,
    }),
    withdraw: useMutation({
      mutationFn: (reason: string) => withdrawRecall(batchId, reason),
      onSuccess: done,
    }),
    close: useMutation({
      mutationFn: (note: string) => closeRecall(batchId, note),
      onSuccess: done,
    }),
  };
}

export function useBreaches(params: { status?: string; pageNumber?: number; pageSize?: number }) {
  return useInventoryQuery('breaches', ['list', params], () => getBreaches(params));
}

export function useBreach(id: string) {
  return useInventoryQuery('breaches', ['detail', id], () => getBreach(id), { enabled: !!id });
}

export function useReportBreach() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: (request: ReportBreachRequest) => reportBreach(request),
    onSuccess: () => invalidate('breaches', 'batches', 'stock'),
  });
}

export function useBreachActions(id: string) {
  const invalidate = useInvalidateInventory();
  const done = () => invalidate('breaches', 'batches', 'stock', 'fefo', 'ledger');
  return {
    quarantine: useMutation({ mutationFn: () => quarantineBreachStock(id), onSuccess: done }),
    close: useMutation({ mutationFn: (note: string) => closeBreach(id, note), onSuccess: done }),
  };
}
