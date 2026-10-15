import { useMemo } from 'react';
import { useMutation } from '@tanstack/react-query';
import {
  getBatch,
  getBatches,
  getFefoPreview,
  getSkuStockMatrix,
  getStockSummary,
  quarantineBatch,
  releaseBatch,
  searchLedger,
  writeOffBatch,
  type BatchListParams,
  type FefoPurpose,
  type LedgerParams,
  type StockListParams,
} from '@/lib/api/inventory-stock';
import {
  getGoodsReceipt,
  getGoodsReceipts,
  postGoodsReceipt,
  type GoodsReceiptListParams,
  type PostGoodsReceiptRequest,
} from '@/lib/api/inventory-receipts';
import {
  approveAdjustment,
  approveCount,
  cancelCount,
  commitCount,
  getAdjustment,
  getAdjustments,
  getCount,
  getCounts,
  getCountVariance,
  getTransfer,
  getTransfers,
  openCount,
  postTransfer,
  recordCountLines,
  rejectAdjustment,
  rejectCount,
  requestAdjustment,
  submitCount,
  withdrawAdjustment,
  type CountScope,
  type PostTransferRequest,
  type RequestAdjustmentRequest,
} from '@/lib/api/inventory-control';
import { acknowledgeAlert, getAlertSummary, getAlerts } from '@/lib/api/inventory-alerts';
import {
  getNearExpiryReport,
  getReconciliation,
  getStockOnHandReport,
  type NearExpiryParams,
  type StockOnHandParams,
} from '@/lib/api/inventory-reports';
import { useInventoryQuery, useInvalidateInventory } from './useInventoryQuery';

/** HO-IMP-002 §8.2/§8.3 — receipts, stock, batches, ledger, control, alerts, reports. */

// ---- stock ----------------------------------------------------------------------------------

export function useStockSummary(params: StockListParams) {
  return useInventoryQuery('stock', ['list', params], () => getStockSummary(params));
}

/** Item id → stock unit, from the stock summary. Reports whose rows carry no unit use it, so a
 *  quantity is never shown bare (UI-8). Empty for a caller who cannot read stock. */
export function useUnitBySku(): Record<string, string> {
  const query = useStockSummary({ pageNumber: 1, pageSize: 100 });
  return useMemo(
    () => Object.fromEntries((query.data?.items ?? []).map((s) => [s.skuId, s.stockUnit])),
    [query.data],
  );
}

export function useSkuStockMatrix(skuId: string) {
  return useInventoryQuery('stock', ['matrix', skuId], () => getSkuStockMatrix(skuId), {
    enabled: !!skuId,
  });
}

export function useFefoPreview(
  params: { skuId: string; quantity: string; purpose: FefoPurpose; locationId?: string } | null,
) {
  return useInventoryQuery(
    'fefo',
    [params],
    () => getFefoPreview(params as NonNullable<typeof params>),
    { enabled: params !== null },
  );
}

export function useBatches(params: BatchListParams, enabled = true) {
  return useInventoryQuery('batches', ['list', params], () => getBatches(params), { enabled });
}

export function useBatch(id: string) {
  return useInventoryQuery('batches', ['detail', id], () => getBatch(id), { enabled: !!id });
}

export function useBatchActions(id: string) {
  const invalidate = useInvalidateInventory();
  const done = () => invalidate('batches', 'stock', 'fefo', 'recalls', 'ledger');
  return {
    quarantine: useMutation({
      mutationFn: (reason: string) => quarantineBatch(id, reason),
      onSuccess: done,
    }),
    release: useMutation({ mutationFn: () => releaseBatch(id), onSuccess: done }),
    writeOff: useMutation({
      mutationFn: (reason: string) => writeOffBatch(id, reason),
      onSuccess: done,
    }),
  };
}

export function useLedger(params: LedgerParams, enabled = true) {
  return useInventoryQuery('ledger', [params], () => searchLedger(params), { enabled });
}

// ---- receipts -------------------------------------------------------------------------------

export function useGoodsReceipts(params: GoodsReceiptListParams) {
  return useInventoryQuery('receipts', ['list', params], () => getGoodsReceipts(params));
}

export function useGoodsReceipt(id: string) {
  return useInventoryQuery('receipts', ['detail', id], () => getGoodsReceipt(id), {
    enabled: !!id,
  });
}

export function usePostGoodsReceipt() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: (request: PostGoodsReceiptRequest) => postGoodsReceipt(request),
    onSuccess: () =>
      invalidate(
        'receipts',
        'stock',
        'batches',
        'purchase-orders',
        'consignments',
        'reorder',
        'ledger',
      ),
  });
}

// ---- transfers / adjustments / counts -------------------------------------------------------

export function useTransfers(params: { pageNumber?: number; pageSize?: number }) {
  return useInventoryQuery('transfers', ['list', params], () => getTransfers(params));
}

export function useTransfer(id: string) {
  return useInventoryQuery('transfers', ['detail', id], () => getTransfer(id), { enabled: !!id });
}

export function usePostTransfer() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: (request: PostTransferRequest) => postTransfer(request),
    onSuccess: () => invalidate('transfers', 'stock', 'batches', 'ledger'),
  });
}

export function useAdjustments(params: {
  status?: string;
  pageNumber?: number;
  pageSize?: number;
}) {
  return useInventoryQuery('adjustments', ['list', params], () => getAdjustments(params));
}

export function useAdjustment(id: string) {
  return useInventoryQuery('adjustments', ['detail', id], () => getAdjustment(id), {
    enabled: !!id,
  });
}

export function useRequestAdjustment() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: (request: RequestAdjustmentRequest) => requestAdjustment(request),
    onSuccess: () => invalidate('adjustments', 'stock', 'batches', 'ledger'),
  });
}

export function useAdjustmentActions(id: string) {
  const invalidate = useInvalidateInventory();
  const done = () => invalidate('adjustments', 'stock', 'batches', 'ledger');
  return {
    approve: useMutation({
      mutationFn: (note: string | null) => approveAdjustment(id, note),
      onSuccess: done,
    }),
    reject: useMutation({
      mutationFn: (note: string) => rejectAdjustment(id, note),
      onSuccess: done,
    }),
    withdraw: useMutation({ mutationFn: () => withdrawAdjustment(id), onSuccess: done }),
  };
}

export function useCounts(params: {
  status?: string;
  locationId?: string;
  pageNumber?: number;
  pageSize?: number;
}) {
  return useInventoryQuery('counts', ['list', params], () => getCounts(params));
}

export function useCount(id: string) {
  return useInventoryQuery('counts', ['detail', id], () => getCount(id), { enabled: !!id });
}

export function useCountVariance(id: string, enabled: boolean) {
  return useInventoryQuery('counts', ['variance', id], () => getCountVariance(id), {
    enabled: !!id && enabled,
  });
}

export function useOpenCount() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: (request: { locationId: string; scope: CountScope; skuIds: string[] }) =>
      openCount(request),
    onSuccess: () => invalidate('counts'),
  });
}

export function useCountActions(id: string) {
  const invalidate = useInvalidateInventory();
  const done = () => invalidate('counts', 'stock', 'batches', 'adjustments', 'ledger');
  return {
    recordLines: useMutation({
      mutationFn: (lines: Array<{ skuId: string; batchId: string; countedQuantity: string }>) =>
        recordCountLines(id, lines),
      // A saved line needs no refetch: the sheet shows its own per-row saved state.
    }),
    submit: useMutation({ mutationFn: () => submitCount(id), onSuccess: done }),
    commit: useMutation({ mutationFn: () => commitCount(id), onSuccess: done }),
    cancel: useMutation({ mutationFn: () => cancelCount(id), onSuccess: done }),
    approve: useMutation({ mutationFn: () => approveCount(id), onSuccess: done }),
    reject: useMutation({ mutationFn: () => rejectCount(id), onSuccess: done }),
  };
}

// ---- alerts ---------------------------------------------------------------------------------

export function useAlerts(params: {
  status?: string;
  kind?: string;
  locationId?: string;
  pageNumber?: number;
  pageSize?: number;
}) {
  return useInventoryQuery('alerts', ['list', params], () => getAlerts(params));
}

/** ADR-FE-045 — the ONLY polling query: the nav badge, every 60 s. */
export const ALERT_BADGE_POLL_MS = 60_000;

export function useAlertSummary(enabled = true) {
  return useInventoryQuery('alerts', ['summary'], getAlertSummary, {
    refetchInterval: ALERT_BADGE_POLL_MS,
    enabled,
  });
}

export function useAcknowledgeAlert() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: (args: { id: string; note: string | null }) => acknowledgeAlert(args.id, args.note),
    onSuccess: () => invalidate('alerts'),
  });
}

// ---- reports --------------------------------------------------------------------------------

export function useNearExpiryReport(params: NearExpiryParams) {
  return useInventoryQuery('reports', ['near-expiry', params], () => getNearExpiryReport(params));
}

export function useStockOnHandReport(params: StockOnHandParams) {
  return useInventoryQuery('reports', ['stock-on-hand', params], () =>
    getStockOnHandReport(params),
  );
}

export function useReconciliation() {
  return useInventoryQuery('reports', ['reconciliation'], getReconciliation);
}
