import { useMutation } from '@tanstack/react-query';
import {
  createSupplier,
  getSupplier,
  getSuppliers,
  setSupplierActivation,
  updateSupplier,
  type CreateSupplierRequest,
  type SupplierInput,
  type SupplierListParams,
} from '@/lib/api/procurement-suppliers';
import {
  cancelPurchaseOrder,
  closePurchaseOrder,
  createPurchaseOrder,
  getPurchaseOrder,
  getPurchaseOrders,
  issuePurchaseOrder,
  replacePurchaseOrderDraft,
  type PurchaseOrderListParams,
  type PurchaseOrderRequest,
} from '@/lib/api/procurement-orders';
import {
  addConsignmentDocument,
  advanceConsignmentStatus,
  allocateLandedCost,
  cancelConsignment,
  getConsignment,
  getConsignments,
  getInspectionPack,
  planConsignment,
  replaceConsignmentCosts,
  supersedeConsignmentDocument,
  updateConsignment,
  type ConsignmentCostsInput,
  type ConsignmentDetailsInput,
  type ConsignmentDocumentInput,
  type ConsignmentListParams,
  type ConsignmentStatus,
} from '@/lib/api/procurement-consignments';
import {
  getReorderPolicies,
  getReorderSuggestions,
  setReorderPolicy,
  type SetReorderPolicyRequest,
} from '@/lib/api/procurement-reorder';
import { useInventoryQuery, useInvalidateInventory } from './useInventoryQuery';

/** HO-IMP-002 §8.2 — suppliers, purchase orders, consignments, reorder. */

// ---- suppliers ------------------------------------------------------------------------------

export function useSuppliers(params: SupplierListParams) {
  return useInventoryQuery('suppliers', ['list', params], () => getSuppliers(params));
}

export function useSupplier(id: string) {
  return useInventoryQuery('suppliers', ['detail', id], () => getSupplier(id), { enabled: !!id });
}

export function useCreateSupplier() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: (request: CreateSupplierRequest) => createSupplier(request),
    onSuccess: () => invalidate('suppliers'),
  });
}

export function useUpdateSupplier(id: string) {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: (request: SupplierInput) => updateSupplier(id, request),
    onSuccess: () => invalidate('suppliers'),
  });
}

export function useSetSupplierActivation(id: string) {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: (isActive: boolean) => setSupplierActivation(id, isActive),
    onSuccess: () => invalidate('suppliers'),
  });
}

// ---- purchase orders ------------------------------------------------------------------------

export function usePurchaseOrders(params: PurchaseOrderListParams) {
  return useInventoryQuery('purchase-orders', ['list', params], () => getPurchaseOrders(params));
}

export function usePurchaseOrder(id: string | null) {
  return useInventoryQuery(
    'purchase-orders',
    ['detail', id],
    () => getPurchaseOrder(id as string),
    { enabled: !!id },
  );
}

export function useCreatePurchaseOrder() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: (request: PurchaseOrderRequest) => createPurchaseOrder(request),
    onSuccess: () => invalidate('purchase-orders', 'reorder'),
  });
}

export function useReplacePurchaseOrderDraft(id: string) {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: (request: PurchaseOrderRequest) => replacePurchaseOrderDraft(id, request),
    onSuccess: () => invalidate('purchase-orders'),
  });
}

export function usePurchaseOrderActions(id: string) {
  const invalidate = useInvalidateInventory();
  const done = () => invalidate('purchase-orders', 'reorder');
  return {
    issue: useMutation({ mutationFn: () => issuePurchaseOrder(id), onSuccess: done }),
    cancel: useMutation({
      mutationFn: (reason: string) => cancelPurchaseOrder(id, reason),
      onSuccess: done,
    }),
    close: useMutation({
      mutationFn: (reason: string) => closePurchaseOrder(id, reason),
      onSuccess: done,
    }),
  };
}

// ---- consignments ---------------------------------------------------------------------------

export function useConsignments(params: ConsignmentListParams) {
  return useInventoryQuery('consignments', ['list', params], () => getConsignments(params));
}

export function useConsignment(id: string) {
  return useInventoryQuery('consignments', ['detail', id], () => getConsignment(id), {
    enabled: !!id,
  });
}

export function useInspectionPack(id: string) {
  return useInventoryQuery('consignments', ['pack', id], () => getInspectionPack(id), {
    enabled: !!id,
  });
}

export function usePlanConsignment() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: (request: ConsignmentDetailsInput) => planConsignment(request),
    onSuccess: () => invalidate('consignments'),
  });
}

export function useConsignmentActions(id: string) {
  const invalidate = useInvalidateInventory();
  const done = () => invalidate('consignments');
  return {
    update: useMutation({
      mutationFn: (request: ConsignmentDetailsInput & { expectedVersion: number }) =>
        updateConsignment(id, request),
      onSuccess: done,
    }),
    advance: useMutation({
      mutationFn: (request: { to: ConsignmentStatus; on: string }) =>
        advanceConsignmentStatus(id, request),
      onSuccess: done,
    }),
    cancel: useMutation({
      mutationFn: (reason: string) => cancelConsignment(id, reason),
      onSuccess: done,
    }),
    addDocument: useMutation({
      mutationFn: (request: ConsignmentDocumentInput) => addConsignmentDocument(id, request),
      onSuccess: done,
    }),
    supersedeDocument: useMutation({
      mutationFn: (args: { documentId: string; request: ConsignmentDocumentInput }) =>
        supersedeConsignmentDocument(id, args.documentId, args.request),
      onSuccess: done,
    }),
    replaceCosts: useMutation({
      mutationFn: (request: ConsignmentCostsInput) => replaceConsignmentCosts(id, request),
      onSuccess: done,
    }),
    allocate: useMutation({
      mutationFn: (expectedVersion: number) => allocateLandedCost(id, expectedVersion),
      onSuccess: () => {
        done();
        invalidate('batches');
      },
    }),
  };
}

// ---- reorder --------------------------------------------------------------------------------

export function useReorderSuggestions(params: { locationId?: string; supplierId?: string }) {
  return useInventoryQuery('reorder', ['suggestions', params], () => getReorderSuggestions(params));
}

export function useReorderPolicies(params: {
  locationId?: string;
  pageNumber?: number;
  pageSize?: number;
}) {
  return useInventoryQuery('reorder', ['policies', params], () => getReorderPolicies(params));
}

export function useSetReorderPolicy() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: (args: { skuId: string; locationId: string; request: SetReorderPolicyRequest }) =>
      setReorderPolicy(args.skuId, args.locationId, args.request),
    onSuccess: () => invalidate('reorder', 'stock'),
  });
}
