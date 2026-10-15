import { keepPreviousData, useQuery } from '@tanstack/react-query';
import {
  getAgeing,
  getApprovals,
  getCharges,
  getCreditNote,
  getCustomerBillingSummary,
  getCustomerLoyalty,
  getInvoice,
  getInvoices,
  getPayment,
  getPaymentMethods,
  getPaymentReceipt,
  getPayments,
  getRefunds,
  getUncapturedSources,
  previewInvoice,
  type ChargesParams,
  type InvoicesParams,
  type PaymentsParams,
} from '@/lib/api/billing-staff';
import { getCashUp } from '@/lib/api/billing-cashup';
import {
  getBankReconciliation,
  getBankStatements,
  getCodCollections,
  getCodReconciliation,
  getCodRemittances,
  type CodCollectionsParams,
} from '@/lib/api/billing-reconciliation';
import { getExportRuns } from '@/lib/api/billing-export';
import { getBillingProfile } from '@/lib/api/billing-profile';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { useBranchScope } from '@/lib/hooks/useBranchScope';
import { useDebouncedValue } from '@/lib/hooks/useDebouncedValue';

/**
 * `lib/hooks/billing/useBillingQueries.ts` — HO-BIL-002 §8.3. Every Billing read. Key families:
 * `['billing','customer',id,…]`, `['billing','invoices',…]`, `['billing','invoice',id]`,
 * `['billing','payments',…]`, `['billing','approvals']`, `['billing','cash-up',branch,date]`,
 * `['billing','bank',statementId]`; each branch-scoped key carries the active branch so a branch
 * switch refetches. A refusal (`403`/`404`) is never retried — the page renders it.
 */

export const PREVIEW_DEBOUNCE_MS = 400;

function noRetryOnRefusal(failureCount: number, error: unknown) {
  if (error instanceof ApiForbiddenError || error instanceof ApiNotFoundError) return false;
  return failureCount < 1;
}

const QUERY_DEFAULTS = { retry: noRetryOnRefusal } as const;

export function usePaymentMethods() {
  return useQuery({
    queryKey: ['billing', 'payment-methods'],
    queryFn: getPaymentMethods,
    staleTime: 5 * 60_000,
    ...QUERY_DEFAULTS,
  });
}

export function useCustomerBillingSummary(customerId: string) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['billing', 'customer', customerId, 'summary', branchId],
    queryFn: () => getCustomerBillingSummary(customerId),
    enabled: ready && !!customerId,
    ...QUERY_DEFAULTS,
  });
}

export function useCharges(params: ChargesParams, enabled = true) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['billing', 'charges', branchId, params],
    queryFn: () => getCharges(params),
    enabled: ready && enabled,
    ...QUERY_DEFAULTS,
  });
}

export function useCustomerCharges(customerId: string, status: ChargesParams['status']) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['billing', 'customer', customerId, 'charges', status, branchId],
    queryFn: () => getCharges({ customerId, status, pageSize: 200 }),
    enabled: ready && !!customerId,
    ...QUERY_DEFAULTS,
  });
}

export function useUncapturedSources(from: string, to: string, enabled = true) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['billing', 'home', 'uncaptured', branchId, from, to],
    queryFn: () => getUncapturedSources(from, to),
    enabled: ready && enabled,
    ...QUERY_DEFAULTS,
  });
}

/**
 * The composer's server preview (ADR-FE-080): keyed on the SORTED charge ids plus the points,
 * debounced 400 ms, previous totals kept while a new one loads (`keepPreviousData`).
 */
export function useInvoicePreview(customerId: string, chargeIds: string[], loyaltyPoints: string) {
  const { branchId, ready } = useBranchScope();
  const sortedIds = [...chargeIds].sort();
  const debounced = useDebouncedValue(
    { ids: sortedIds.join(','), points: loyaltyPoints },
    PREVIEW_DEBOUNCE_MS,
  );
  const ids = debounced.ids === '' ? [] : debounced.ids.split(',');
  const query = useQuery({
    queryKey: ['billing', 'preview', branchId, customerId, debounced.ids, debounced.points],
    queryFn: () =>
      previewInvoice({ customerId, chargeIds: ids, loyaltyPoints: debounced.points || undefined }),
    enabled: ready && !!customerId && ids.length > 0,
    placeholderData: keepPreviousData,
    ...QUERY_DEFAULTS,
  });
  const settled = debounced.ids === sortedIds.join(',') && debounced.points === loyaltyPoints;
  return { ...query, isRecomputing: !settled || query.isFetching };
}

export function useInvoices(params: InvoicesParams) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['billing', 'invoices', branchId, params],
    queryFn: () => getInvoices(params),
    enabled: ready,
    ...QUERY_DEFAULTS,
  });
}

export function useCustomerInvoices(customerId: string) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['billing', 'customer', customerId, 'invoices', branchId],
    queryFn: () => getInvoices({ customerId, pageSize: 100 }),
    enabled: ready && !!customerId,
    ...QUERY_DEFAULTS,
  });
}

export function useInvoice(invoiceId: string) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['billing', 'invoice', invoiceId, branchId],
    queryFn: () => getInvoice(invoiceId),
    enabled: ready && !!invoiceId,
    ...QUERY_DEFAULTS,
  });
}

export function usePayments(params: PaymentsParams) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['billing', 'payments', branchId, params],
    queryFn: () => getPayments(params),
    enabled: ready,
    ...QUERY_DEFAULTS,
  });
}

export function useCustomerPayments(customerId: string) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['billing', 'customer', customerId, 'payments', branchId],
    queryFn: () => getPayments({ customerId, pageSize: 100 }),
    enabled: ready && !!customerId,
    ...QUERY_DEFAULTS,
  });
}

export function usePayment(paymentId: string) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['billing', 'payment', paymentId, branchId],
    queryFn: () => getPayment(paymentId),
    enabled: ready && !!paymentId,
    ...QUERY_DEFAULTS,
  });
}

export function usePaymentReceipt(paymentId: string) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['billing', 'payment', paymentId, 'receipt', branchId],
    queryFn: () => getPaymentReceipt(paymentId),
    enabled: ready && !!paymentId,
    ...QUERY_DEFAULTS,
  });
}

export function useApprovals(enabled = true) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['billing', 'approvals', branchId],
    queryFn: getApprovals,
    enabled: ready && enabled,
    ...QUERY_DEFAULTS,
  });
}

export function useCreditNote(creditNoteId: string) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['billing', 'credit-note', creditNoteId, branchId],
    queryFn: () => getCreditNote(creditNoteId),
    enabled: ready && !!creditNoteId,
    ...QUERY_DEFAULTS,
  });
}

export function useRefunds(params: { from?: string; to?: string; pageNumber?: number }) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['billing', 'refunds', branchId, params],
    queryFn: () => getRefunds(params),
    enabled: ready,
    ...QUERY_DEFAULTS,
  });
}

export function useAgeing(params: { asOf?: string; pageNumber?: number | string; pageSize?: number }) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['billing', 'receivables', branchId, params],
    queryFn: () => getAgeing(params),
    enabled: ready,
    ...QUERY_DEFAULTS,
  });
}

export function useCustomerLoyalty(customerId: string, enabled: boolean) {
  return useQuery({
    queryKey: ['billing', 'customer', customerId, 'loyalty'],
    queryFn: () => getCustomerLoyalty(customerId),
    enabled: enabled && !!customerId,
    ...QUERY_DEFAULTS,
  });
}

export function useCashUp(date: string) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['billing', 'cash-up', branchId, date],
    queryFn: () => getCashUp(date),
    enabled: ready && !!date,
    ...QUERY_DEFAULTS,
  });
}

export function useCodCollections(params: CodCollectionsParams) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['billing', 'cod', 'collections', branchId, params],
    queryFn: () => getCodCollections(params),
    enabled: ready,
    ...QUERY_DEFAULTS,
  });
}

export function useCodReconciliation(params: { courierCode?: string; from?: string; to?: string }) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['billing', 'cod', 'reconciliation', branchId, params],
    queryFn: () => getCodReconciliation(params),
    enabled: ready,
    ...QUERY_DEFAULTS,
  });
}

export function useCodRemittances(courierCode: string | undefined) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['billing', 'cod', 'remittances', branchId, courierCode ?? ''],
    queryFn: () => getCodRemittances({ courierCode }),
    enabled: ready,
    ...QUERY_DEFAULTS,
  });
}

export function useBankStatements(pageNumber: number | string) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['billing', 'bank', 'statements', branchId, pageNumber],
    queryFn: () => getBankStatements(pageNumber),
    enabled: ready,
    ...QUERY_DEFAULTS,
  });
}

export function useBankReconciliation(statementId: string) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['billing', 'bank', statementId, branchId],
    queryFn: () => getBankReconciliation(statementId),
    enabled: ready && !!statementId,
    ...QUERY_DEFAULTS,
  });
}

export function useExportRuns(pageNumber: number | string) {
  return useQuery({
    queryKey: ['billing', 'exports', pageNumber],
    queryFn: () => getExportRuns(pageNumber),
    ...QUERY_DEFAULTS,
  });
}

export function useBillingProfile(branchId: string | null) {
  return useQuery({
    queryKey: ['billing', 'profile', branchId],
    queryFn: () => getBillingProfile(branchId ?? ''),
    enabled: !!branchId,
    ...QUERY_DEFAULTS,
  });
}
