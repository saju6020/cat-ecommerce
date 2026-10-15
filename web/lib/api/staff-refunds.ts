import { apiFetch } from './client';
import type { Money } from '../format/money';
import { wireMoney } from '../money';
import type { PagedResult } from './types';

/**
 * `lib/api/staff-refunds.ts` — HO-ORD-002 §8.2. Refunds due, refund options and issuing a refund, as
 * `StaffOrdersController.RefundsDue` and `StaffReturnsController` (`refund-options`, `refunds`) send
 * them. The suggested and maximum amounts are the server's; the form pre-fills them as typed text.
 */

export interface RefundDueOrder {
  orderId: string;
  orderNumber: string;
  customerName: string;
  status: string;
  amountPaid: Money;
  amountRefunded: Money;
  refundDue: Money;
  cancelledOn: string | null;
  branchTimeZoneId: string;
  /** Codes the server derived (why a refund is due); shown through `describeRefundReason`. */
  reasons: string[];
}

export function listRefundsDue(filters: {
  pageNumber?: number;
  pageSize?: number;
}): Promise<PagedResult<RefundDueOrder>> {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(filters)) {
    if (value !== undefined) search.set(key, String(value));
  }
  const text = search.toString();
  return apiFetch(`/api/staff/orders/refunds-due${text ? `?${text}` : ''}`);
}

export type RefundMethod = 'CashInStore' | 'BankTransfer' | 'StoreCredit';

export interface RefundMethodOption {
  method: RefundMethod;
  allowed: boolean;
  available: boolean;
  unavailableReason: string | null;
}

export interface RefundOptions {
  orderId: string;
  methods: RefundMethodOption[];
  maxRefundable: Money;
  suggestedForReturns: Array<{ returnRequestId: string; suggestedAmount: Money }>;
}

export function getRefundOptions(orderId: string): Promise<RefundOptions> {
  return apiFetch(`/api/staff/orders/${encodeURIComponent(orderId)}/refund-options`);
}

export interface IssueRefundInput {
  returnRequestId?: string;
  method: RefundMethod;
  /** The amount as the person typed it (a string); sent as a JSON number literal. */
  amount: string;
  reference?: string;
  reason: string;
}

export function issueRefund(
  orderId: string,
  input: IssueRefundInput,
  idempotencyKey: string,
): Promise<{ refundId: string; creditNoteId: string | null }> {
  return apiFetch(`/api/staff/orders/${encodeURIComponent(orderId)}/refunds`, {
    method: 'POST',
    body: { ...input, amount: wireMoney(input.amount) },
    idempotencyKey,
  });
}
