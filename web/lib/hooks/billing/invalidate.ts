import type { QueryClient } from '@tanstack/react-query';

/**
 * `lib/hooks/billing/invalidate.ts` — HO-BIL-002 §8.3. After ANY money-moving mutation: the
 * customer's summary and account tabs, the invoice(s) touched, the payment, the approvals queue and
 * the day's cash-up are refetched — one helper, so no screen ever shows a stale balance. Every
 * mutation hook calls it; nothing else invalidates Billing queries by hand.
 */
export interface PostingResult {
  customerId?: string | null;
  invoiceIds?: ReadonlyArray<string | null | undefined>;
  paymentId?: string | null;
}

export function invalidateAfterPosting(queryClient: QueryClient, result: PostingResult = {}): void {
  const invalidate = (queryKey: readonly unknown[]) =>
    void queryClient.invalidateQueries({ queryKey });

  if (result.customerId) invalidate(['billing', 'customer', result.customerId]);
  else invalidate(['billing', 'customer']);
  for (const invoiceId of result.invoiceIds ?? []) {
    if (invoiceId) invalidate(['billing', 'invoice', invoiceId]);
  }
  if (!result.invoiceIds || result.invoiceIds.length === 0) invalidate(['billing', 'invoice']);
  if (result.paymentId) invalidate(['billing', 'payment', result.paymentId]);
  else invalidate(['billing', 'payment']);
  invalidate(['billing', 'invoices']);
  invalidate(['billing', 'payments']);
  invalidate(['billing', 'approvals']);
  invalidate(['billing', 'cash-up']);
  invalidate(['billing', 'receivables']);
  invalidate(['billing', 'home']);
  invalidate(['billing', 'charges']);
}
