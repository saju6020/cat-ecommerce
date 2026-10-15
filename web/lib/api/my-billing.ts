import { apiFetch, getApiUrl } from './client';
import { query } from './billing-staff';
import type { InvoiceDetail, InvoiceSummary, LoyaltyLedger, PaymentReceipt, PaymentSummary, BankAccount } from './billing-staff';
import type { CustomerBillingSummary } from './billing-staff';
import type { PagedResult } from './types';

/**
 * `lib/api/my-billing.ts` — HO-BIL-002 §11.11, verified against `CustomerBillingController`
 * (`/api/customers/me/...`). Every document is resolved by id AND caller — someone else's is a
 * `404`, never a `403`; drafts, discarded invoices and pending/rejected payments are never returned.
 */

export type MyBillingSummary = CustomerBillingSummary;

export interface HowToPay {
  bankAccounts: BankAccount[];
  referenceInstruction: string;
}

export interface MyInvoiceDetail {
  invoice: InvoiceDetail;
  howToPay: HowToPay;
}

const BASE = '/api/customers/me';

export async function getMyBillingSummary(): Promise<MyBillingSummary> {
  return apiFetch<MyBillingSummary>(`${BASE}/billing/summary`);
}

export async function getMyInvoices(pageNumber: number | string = 1): Promise<PagedResult<InvoiceSummary>> {
  return apiFetch<PagedResult<InvoiceSummary>>(`${BASE}/invoices${query({ pageNumber, pageSize: 20 })}`);
}

export async function getMyInvoice(invoiceId: string): Promise<MyInvoiceDetail> {
  return apiFetch<MyInvoiceDetail>(`${BASE}/invoices/${encodeURIComponent(invoiceId)}`);
}

export function myInvoicePdfUrl(invoiceId: string): string {
  return getApiUrl(`${BASE}/invoices/${encodeURIComponent(invoiceId)}.pdf`);
}

export async function getMyPayments(pageNumber: number | string = 1): Promise<PagedResult<PaymentSummary>> {
  return apiFetch<PagedResult<PaymentSummary>>(`${BASE}/payments${query({ pageNumber, pageSize: 20 })}`);
}

export async function getMyReceipt(paymentId: string): Promise<PaymentReceipt> {
  return apiFetch<PaymentReceipt>(`${BASE}/payments/${encodeURIComponent(paymentId)}`);
}

export async function getMyLoyalty(pageNumber = 1): Promise<LoyaltyLedger> {
  return apiFetch<LoyaltyLedger>(`${BASE}/loyalty${query({ pageNumber, pageSize: 20 })}`);
}
