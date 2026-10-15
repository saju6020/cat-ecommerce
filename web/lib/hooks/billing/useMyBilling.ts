import { useQuery } from '@tanstack/react-query';
import {
  getMyBillingSummary,
  getMyInvoice,
  getMyInvoices,
  getMyLoyalty,
  getMyPayments,
  getMyReceipt,
} from '@/lib/api/my-billing';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';

/** `lib/hooks/billing/useMyBilling.ts` — HO-BIL-002 §11.11. The customer portal's reads
 *  (`['me','billing',…]`). Someone else's document is a `404` — never retried, rendered as not found. */
function noRetryOnRefusal(failureCount: number, error: unknown) {
  if (error instanceof ApiForbiddenError || error instanceof ApiNotFoundError) return false;
  return failureCount < 1;
}

export function useMyBillingSummary() {
  return useQuery({
    queryKey: ['me', 'billing', 'summary'],
    queryFn: getMyBillingSummary,
    retry: noRetryOnRefusal,
  });
}

export function useMyInvoices(pageNumber: number | string) {
  return useQuery({
    queryKey: ['me', 'billing', 'invoices', pageNumber],
    queryFn: () => getMyInvoices(pageNumber),
    retry: noRetryOnRefusal,
  });
}

export function useMyInvoice(invoiceId: string) {
  return useQuery({
    queryKey: ['me', 'billing', 'invoice', invoiceId],
    queryFn: () => getMyInvoice(invoiceId),
    enabled: !!invoiceId,
    retry: noRetryOnRefusal,
  });
}

export function useMyPayments(pageNumber: number | string) {
  return useQuery({
    queryKey: ['me', 'billing', 'payments', pageNumber],
    queryFn: () => getMyPayments(pageNumber),
    retry: noRetryOnRefusal,
  });
}

export function useMyReceipt(paymentId: string) {
  return useQuery({
    queryKey: ['me', 'billing', 'receipt', paymentId],
    queryFn: () => getMyReceipt(paymentId),
    enabled: !!paymentId,
    retry: noRetryOnRefusal,
  });
}

export function useMyLoyalty(enabled: boolean) {
  return useQuery({
    queryKey: ['me', 'billing', 'loyalty'],
    queryFn: () => getMyLoyalty(),
    enabled,
    retry: noRetryOnRefusal,
  });
}
