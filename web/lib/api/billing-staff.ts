import {
  ApiConflictError,
  ApiError,
  ApiForbiddenError,
  ApiNotFoundError,
  ApiRateLimitedError,
  ApiUnauthorizedError,
  ApiValidationError,
  apiFetch,
  getApiUrl,
  notifySessionError,
  type ForbiddenBody,
  type SessionErrorBody,
  type SignInFailedBody,
} from './client';
import { getActiveBranchId } from '@/lib/branch';
import type { PagedResult, ValidationResponse } from './types';
import type { Money } from '@/lib/format/money';
import { moneyFromWire, wireInteger, wireMoney, wireQuantity } from '@/lib/money';

/**
 * `lib/api/billing-staff.ts` — HO-BIL-002 §8.2. Every staff Billing endpoint, verified against
 * HO-BIL-001's real controllers (`Staff{Charges,Invoices,Payments,CreditNotes,Refunds,Receivables,
 * Loyalty}Controller`, `BillingReferenceController`) and their view models, not only §12.4.
 * Money is a string (`{ amount: "1500.00", currencyCode }`), quantities are strings (`"2.000"`),
 * dates are `yyyy-MM-dd`; request amounts go out as JSON number literals built from the string
 * (`lib/money/wire.ts`) — never through a JavaScript `number`. The branch is `X-Branch-Id`
 * (`client.ts`), never a parameter. No file in this module parses or computes an amount.
 */

export type { Money };

export type BusinessLine = 'Clinic' | 'Shop' | 'Boarding';
export const BUSINESS_LINES: BusinessLine[] = ['Clinic', 'Shop', 'Boarding'];

export type InvoiceStatus = 'Draft' | 'Issued' | 'PartPaid' | 'Paid' | 'Credited' | 'Discarded';
export type PaymentStatus = 'PendingApproval' | 'Accepted' | 'Rejected' | 'Reversed';

export function query(params: Record<string, string | number | undefined | null>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') search.set(key, String(value));
  }
  const text = search.toString();
  return text ? `?${text}` : '';
}

const BASE = '/api/staff/billing';
const id = encodeURIComponent;

// ---- payment methods ------------------------------------------------------------------------

export interface PaymentMethod {
  value: number;
  name: string;
  countsInTill: boolean;
  requiresReference: boolean;
  referenceLabel: string;
  requiresCollector: boolean;
  isOnline: boolean;
  allowedRefundMethods: string[];
}

export async function getPaymentMethods(): Promise<PaymentMethod[]> {
  return apiFetch<PaymentMethod[]>('/api/billing/payment-methods');
}

// ---- customer summary -----------------------------------------------------------------------

export interface EarmarkedCredit {
  paymentId: string;
  label: string;
  amount: Money;
}

export interface CustomerBillingSummary {
  outstanding: Money;
  overdue: Money;
  freeCredit: Money;
  earmarkedCredit: EarmarkedCredit[];
  unbilledTotal: Money;
  loyaltyPoints: number | null;
  loyaltyEnabled: boolean;
  oldestDueOn: string | null;
}

export async function getCustomerBillingSummary(customerId: string): Promise<CustomerBillingSummary> {
  return apiFetch<CustomerBillingSummary>(`${BASE}/customers/${id(customerId)}/summary`);
}

// ---- charges --------------------------------------------------------------------------------

export interface BilledOnInvoice {
  invoiceId: string;
  number: string | null;
  status: string;
}

export interface Charge {
  chargeId: string;
  customerId: string;
  customerName: string;
  branchId: string;
  catId: string | null;
  businessLine: BusinessLine;
  itemKind: string;
  sourceKind: string;
  sourceId: string;
  sourceLineRef: string;
  billingGroupKind: string | null;
  billingGroupId: string | null;
  description: string;
  quantity: string;
  unitPrice: Money;
  lineDiscount: Money;
  lineTotal: Money;
  taxClassId: string;
  serviceDate: string;
  status: string;
  recordedOn: string;
  voidReason: string | null;
  billedOnInvoice: BilledOnInvoice | null;
}

export interface ChargesParams {
  customerId?: string;
  status?: 'unbilled' | 'billed' | 'voided';
  sourceKind?: string;
  pageNumber?: number | string;
  pageSize?: number;
}

export async function getCharges(params: ChargesParams): Promise<PagedResult<Charge>> {
  return apiFetch<PagedResult<Charge>>(`${BASE}/charges${query({ ...params })}`);
}

export interface ManualChargeRequest {
  customerId: string;
  catId?: string;
  serviceId?: string;
  description?: string;
  unitPrice?: string;
  taxClassId?: string;
  quantity: string;
  lineDiscount?: string;
  serviceDate: string;
  businessLine?: BusinessLine;
}

export async function recordManualCharge(
  request: ManualChargeRequest,
): Promise<{ chargeId: string }> {
  return apiFetch<{ chargeId: string }>(`${BASE}/charges`, {
    method: 'POST',
    body: {
      customerId: request.customerId,
      catId: request.catId,
      serviceId: request.serviceId,
      description: request.description,
      unitPrice: request.unitPrice === undefined ? undefined : wireMoney(request.unitPrice),
      taxClassId: request.taxClassId,
      quantity: wireQuantity(request.quantity),
      lineDiscount: request.lineDiscount ? wireMoney(request.lineDiscount) : undefined,
      serviceDate: request.serviceDate,
      businessLine: request.businessLine,
    },
  });
}

export async function voidCharge(chargeId: string, reason: string): Promise<void> {
  await apiFetch<void>(`${BASE}/charges/${id(chargeId)}/void`, {
    method: 'POST',
    body: { reason },
  });
}

export interface UncapturedSource {
  sourceKind: string;
  sourceId: string;
  customerId: string;
  completedOn: string;
  label: string;
}

export async function getUncapturedSources(from: string, to: string): Promise<UncapturedSource[]> {
  return apiFetch<UncapturedSource[]>(`${BASE}/uncaptured-sources${query({ from, to })}`);
}

export interface CaptureResult {
  captured: number;
  skipped: Array<{ reason: string }>;
}

export async function captureSource(sourceKind: string, sourceId: string): Promise<CaptureResult> {
  return apiFetch<CaptureResult>(`${BASE}/sources/${id(sourceKind)}/${id(sourceId)}/capture`, {
    method: 'POST',
  });
}

// ---- invoices -------------------------------------------------------------------------------

export interface InvoiceTotals {
  net: Money;
  discount: Money;
  tax: Money;
  total: Money;
}

export interface InvoicePreviewLine {
  chargeId: string;
  businessLine: BusinessLine;
  itemKind: string;
  description: string;
  quantity: string;
  unitPrice: Money;
  lineDiscount: Money;
  invoiceDiscountShare: Money;
  taxClassCode: string | null;
  taxRatePercent: string | null;
  netAmount: Money;
  taxAmount: Money;
  grossAmount: Money;
}

export interface InvoicePreview {
  customerId: string;
  lines: InvoicePreviewLine[];
  totals: InvoiceTotals;
  isEstimate: boolean;
  problems: string[];
}

export interface PreviewRequest {
  customerId: string;
  chargeIds: string[];
  loyaltyPoints?: string;
}

export async function previewInvoice(request: PreviewRequest): Promise<InvoicePreview> {
  return apiFetch<InvoicePreview>(`${BASE}/invoices/preview`, {
    method: 'POST',
    body: {
      customerId: request.customerId,
      chargeIds: request.chargeIds,
      loyaltyPoints: request.loyaltyPoints ? wireInteger(request.loyaltyPoints) : undefined,
    },
  });
}

export interface BankAccount {
  bankName: string;
  accountName: string;
  accountNumber: string;
  branchName: string | null;
  routingNumber: string | null;
}

export interface Seller {
  legalName: string;
  taxRegistrationNumber: string;
  addressText: string;
  phone: string | null;
  email: string | null;
  invoiceFooter: string | null;
  bankAccounts: BankAccount[];
}

export interface BillTo {
  fullName: string;
  mobile: string | null;
  email: string | null;
  addressText: string | null;
}

export interface InvoiceLine {
  lineId: string;
  chargeId: string | null;
  businessLine: BusinessLine;
  itemKind: string;
  sourceKind: string;
  sourceId: string;
  catId: string | null;
  description: string;
  quantity: string;
  unitPrice: Money;
  lineDiscount: Money;
  invoiceDiscountShare: Money;
  taxClassId: string;
  taxClassCode: string | null;
  taxRatePercent: string;
  netAmount: Money;
  taxAmount: Money;
  grossAmount: Money;
  sortOrder: number;
}

export interface InvoicePaymentRow {
  paymentId: string;
  receiptNumber: string;
  method: string;
  amount: Money;
  on: string;
  status: string;
}

export interface InvoiceCreditNoteRow {
  creditNoteId: string;
  number: string;
  kind: string;
  total: Money;
  issueDate: string;
}

export interface LoyaltyRedemption {
  points: number;
  value: Money;
  loyaltyEntryId: string;
}

export interface InvoiceDetail {
  invoiceId: string;
  number: string | null;
  financialYear: string | null;
  status: InvoiceStatus;
  version: number;
  branchId: string;
  branchTimeZoneId: string;
  customerId: string;
  issuedOn: string | null;
  issueDate: string | null;
  dueOn: string | null;
  seller: Seller | null;
  billTo: BillTo | null;
  lines: InvoiceLine[];
  totals: InvoiceTotals;
  isEstimate: boolean;
  paid: Money;
  credited: Money;
  outstanding: Money;
  loyaltyRedemption: LoyaltyRedemption | null;
  notes: string | null;
  correctsInvoiceId: string | null;
  correctsCreditNoteId: string | null;
  correctedByInvoiceId: string | null;
  payments: InvoicePaymentRow[];
  creditNotes: InvoiceCreditNoteRow[];
  allowedActions: string[];
}

export interface InvoiceSummary {
  invoiceId: string;
  number: string | null;
  status: InvoiceStatus;
  customerId: string;
  customerName: string;
  branchId: string;
  issueDate: string | null;
  dueOn: string | null;
  total: Money;
  paid: Money;
  credited: Money;
  outstanding: Money;
  version: number;
}

export interface InvoicesParams {
  customerId?: string;
  status?: string;
  number?: string;
  from?: string;
  to?: string;
  businessLine?: string;
  pageNumber?: number | string;
  pageSize?: number;
}

export async function getInvoices(params: InvoicesParams): Promise<PagedResult<InvoiceSummary>> {
  return apiFetch<PagedResult<InvoiceSummary>>(`${BASE}/invoices${query({ ...params })}`);
}

export async function getInvoice(invoiceId: string): Promise<InvoiceDetail> {
  return apiFetch<InvoiceDetail>(`${BASE}/invoices/${id(invoiceId)}`);
}

export async function createDraftInvoice(request: {
  customerId: string;
  chargeIds: string[];
  notes?: string;
}): Promise<{ invoiceId: string }> {
  return apiFetch<{ invoiceId: string }>(`${BASE}/invoices`, { method: 'POST', body: request });
}

export async function setDraftCharges(
  invoiceId: string,
  request: { expectedVersion: number; chargeIds: string[] },
): Promise<InvoiceDetail> {
  return apiFetch<InvoiceDetail>(`${BASE}/invoices/${id(invoiceId)}/charges`, {
    method: 'PUT',
    body: request,
  });
}

export async function discardDraftInvoice(invoiceId: string): Promise<void> {
  await apiFetch<void>(`${BASE}/invoices/${id(invoiceId)}/discard`, { method: 'POST' });
}

export interface IssuedInvoice {
  invoiceId: string;
  number: string;
}

export async function issueInvoice(
  invoiceId: string,
  expectedVersion: number,
  idempotencyKey: string,
): Promise<IssuedInvoice> {
  return apiFetch<IssuedInvoice>(`${BASE}/invoices/${id(invoiceId)}/issue`, {
    method: 'POST',
    body: { expectedVersion },
    headers: { 'Idempotency-Key': idempotencyKey },
  });
}

export async function applyLoyaltyRedemption(
  invoiceId: string,
  points: string,
): Promise<{ invoice: InvoiceDetail }> {
  return apiFetch<{ invoice: InvoiceDetail }>(
    `${BASE}/invoices/${id(invoiceId)}/loyalty-redemption`,
    { method: 'POST', body: { points: wireInteger(points) } },
  );
}

export async function removeLoyaltyRedemption(
  invoiceId: string,
): Promise<{ invoice: InvoiceDetail }> {
  return apiFetch<{ invoice: InvoiceDetail }>(
    `${BASE}/invoices/${id(invoiceId)}/loyalty-redemption`,
    { method: 'DELETE' },
  );
}

export interface LoyaltyEntry {
  entryId: string;
  kind: string;
  points: number;
  invoiceId: string | null;
  invoiceNumber: string | null;
  creditNoteId: string | null;
  shortfall: number;
  occurredOn: string;
}

export interface LoyaltyLedger {
  enabled: boolean;
  balance: number;
  entries: PagedResult<LoyaltyEntry>;
}

export async function getCustomerLoyalty(
  customerId: string,
  pageNumber = 1,
): Promise<LoyaltyLedger> {
  return apiFetch<LoyaltyLedger>(
    `${BASE}/customers/${id(customerId)}/loyalty${query({ pageNumber, pageSize: 20 })}`,
  );
}

// ---- payments (list shape needed by the CSV links below) ------------------------------------

export interface PaymentsParams {
  customerId?: string;
  status?: string;
  method?: string;
  from?: string;
  to?: string;
  pageNumber?: number | string;
  pageSize?: number;
}

function readCsrfToken(): string | undefined {
  if (typeof document === 'undefined') return undefined;
  const match = document.cookie.split('; ').find((row) => row.startsWith('pc_csrf='));
  return match ? decodeURIComponent(match.slice('pc_csrf='.length)) : undefined;
}

function parseJson<T>(text: string): T | null {
  try {
    return JSON.parse(text) as T;
  } catch {
    return null;
  }
}

export interface XhrResult<T> {
  body: T;
  filename: string | null;
}

/**
 * One XHR call that sends the SAME headers `apiFetch` does (CSRF, `X-Branch-Id`) and maps every status
 * onto the SAME error classes — for the two things `apiFetch` cannot do (it only speaks JSON and
 * `client.ts` is outside this handover's scope): a multipart upload and a binary download. Like
 * `lib/api/files.ts` (ADR-FE-150) it uses `XMLHttpRequest`, so no second `fetch` call site exists. A
 * download is an authenticated request, so an anchor (which cannot add `X-Branch-Id`) is never used for
 * a branch-scoped file.
 */
export function xhrRequest<T>(
  path: string,
  options: { method?: string; body?: FormData; as: 'json' | 'blob' },
): Promise<XhrResult<T>> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open(options.method ?? 'GET', getApiUrl(path));
    xhr.withCredentials = true;
    xhr.responseType = 'blob';
    if (options.as === 'json') xhr.setRequestHeader('Accept', 'application/json');
    const csrf = readCsrfToken();
    if (csrf) xhr.setRequestHeader('X-CSRF-Token', csrf);
    const branchId = getActiveBranchId();
    if (branchId) xhr.setRequestHeader('X-Branch-Id', branchId);

    xhr.onerror = () => reject(new ApiError('Could not reach the server. Please try again.', 0));
    xhr.ontimeout = xhr.onerror;
    xhr.onload = async () => {
      const status = xhr.status;
      const blob = xhr.response as Blob;
      if (status === 200 || status === 201) {
        if (options.as === 'blob') {
          const disposition = xhr.getResponseHeader('Content-Disposition') ?? '';
          const match = /filename="?([^";]+)"?/i.exec(disposition);
          resolve({ body: blob as T, filename: match?.[1] ?? null });
        } else {
          const text = await blob.text();
          resolve({ body: (text ? parseJson<T>(text) : undefined) as T, filename: null });
        }
        return;
      }
      const text = await blob.text().catch(() => '');
      if (status === 400 || status === 413) {
        const body = parseJson<ValidationResponse>(text);
        reject(body && Array.isArray(body.errors) ? new ApiValidationError(body) : new ApiError('Something went wrong. Please try again.', status));
      } else if (status === 401) {
        const body = parseJson<SignInFailedBody | SessionErrorBody>(text) ?? { error: 'session_invalid' as const };
        if (body.error !== 'invalid_credentials') notifySessionError(body);
        reject(new ApiUnauthorizedError(body));
      } else if (status === 403) {
        reject(new ApiForbiddenError(parseJson<ForbiddenBody>(text) ?? {}));
      } else if (status === 404) {
        reject(new ApiNotFoundError());
      } else if (status === 409) {
        const body = parseJson<ValidationResponse>(text);
        reject(new ApiConflictError(body && Array.isArray(body.errors) ? body : undefined));
      } else if (status === 429) {
        reject(new ApiRateLimitedError(60));
      } else {
        reject(new ApiError('Something went wrong. Please try again.', status));
      }
    };
    xhr.send(options.body ?? null);
  });
}

/** Fetches a branch-scoped file with the CSRF and `X-Branch-Id` headers and hands the browser a saved
 *  file (`URL.createObjectURL` + a `download` anchor). Rejects with the same errors `apiFetch` throws. */
export async function downloadBillingFile(path: string, fallbackName: string): Promise<void> {
  const { body, filename } = await xhrRequest<Blob>(path, { as: 'blob' });
  const url = URL.createObjectURL(body);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename ?? fallbackName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}

export const invoicePdfPath = (invoiceId: string) => `${BASE}/invoices/${id(invoiceId)}.pdf`;
export const creditNotePdfPath = (creditNoteId: string) => `${BASE}/credit-notes/${id(creditNoteId)}.pdf`;
export function invoicesCsvPath(params: InvoicesParams): string {
  const { pageNumber: _page, pageSize: _size, ...filters } = params;
  void _page;
  void _size;
  return `${BASE}/invoices/export.csv${query({ ...filters })}`;
}
export function paymentsCsvPath(params: PaymentsParams): string {
  const { pageNumber: _page, pageSize: _size, ...filters } = params;
  void _page;
  void _size;
  return `${BASE}/payments/export.csv${query({ ...filters })}`;
}

// ---- credit notes and corrected re-issue ----------------------------------------------------

export type CreditNoteKind = 'Correction' | 'Return' | 'Cancellation' | 'Goodwill';
export const CREDIT_NOTE_KINDS: CreditNoteKind[] = [
  'Correction',
  'Return',
  'Cancellation',
  'Goodwill',
];

export interface CreditLineInput {
  invoiceLineId: string;
  quantity?: string;
  amount?: string;
}

export interface IssueCreditNoteRequest {
  kind: CreditNoteKind;
  reason: string;
  fullCredit: boolean;
  lines?: CreditLineInput[];
}

export interface IssuedCreditNote {
  creditNoteId: string;
  number: string;
  releasedToCustomerCredit: Money;
}

export async function issueCreditNote(
  invoiceId: string,
  request: IssueCreditNoteRequest,
  idempotencyKey: string,
): Promise<IssuedCreditNote> {
  const raw = await apiFetch<{ creditNoteId: string; number: string; releasedToCustomerCredit: unknown }>(`${BASE}/invoices/${id(invoiceId)}/credit-notes`, {
    method: 'POST',
    body: {
      kind: request.kind,
      reason: request.reason,
      fullCredit: request.fullCredit,
      lines: request.lines?.map((line) => ({
        invoiceLineId: line.invoiceLineId,
        quantity: line.quantity ? wireQuantity(line.quantity) : undefined,
        amount: line.amount ? wireMoney(line.amount) : undefined,
      })),
    },
    headers: { 'Idempotency-Key': idempotencyKey },
  });
  return { ...raw, releasedToCustomerCredit: moneyFromWire(raw.releasedToCustomerCredit) };
}

export interface CreditNoteLine {
  invoiceLineId: string;
  description: string;
  quantity: string | null;
  requestedAmount: Money | null;
  net: Money;
  taxRatePercent: string;
  tax: Money;
  gross: Money;
}

export interface CreditNoteDetail {
  creditNoteId: string;
  number: string;
  financialYear: string;
  kind: CreditNoteKind;
  reason: string;
  invoiceId: string;
  invoiceNumber: string | null;
  customerId: string;
  branchId: string;
  lines: CreditNoteLine[];
  net: Money;
  tax: Money;
  total: Money;
  releasedToCustomerCredit: Money;
  issuedOn: string;
  issueDate: string;
  issuedBy: string;
  issuedByName: string;
  correctedByInvoiceId: string | null;
}

export async function getCreditNote(creditNoteId: string): Promise<CreditNoteDetail> {
  return apiFetch<CreditNoteDetail>(`${BASE}/credit-notes/${id(creditNoteId)}`);
}

export async function createCorrectedDraft(
  invoiceId: string,
  creditNoteId: string,
): Promise<{ invoiceId: string }> {
  return apiFetch<{ invoiceId: string }>(`${BASE}/invoices/${id(invoiceId)}/corrected-draft`, {
    method: 'POST',
    body: { creditNoteId },
  });
}

export interface CorrectedLineInput {
  description: string;
  quantity: string;
  unitPrice: string;
  lineDiscount: string;
  taxClassId: string;
}

export async function setCorrectedLines(
  invoiceId: string,
  expectedVersion: number,
  lines: CorrectedLineInput[],
): Promise<void> {
  await apiFetch<void>(`${BASE}/invoices/${id(invoiceId)}/lines`, {
    method: 'PUT',
    body: {
      expectedVersion,
      lines: lines.map((line) => ({
        description: line.description,
        quantity: wireQuantity(line.quantity),
        unitPrice: wireMoney(line.unitPrice),
        lineDiscount: wireMoney(line.lineDiscount || '0.00'),
        taxClassId: line.taxClassId,
      })),
    },
  });
}

// ---- payments -------------------------------------------------------------------------------

export interface PaymentSummary {
  paymentId: string;
  receiptNumber: string | null;
  customerId: string;
  customerName: string;
  branchId: string;
  method: string;
  status: PaymentStatus;
  amount: Money;
  unallocated: Money;
  receivedOn: string;
  recordedOn: string;
  recordedBy: string;
  recordedByName: string;
  externalReference: string | null;
  version: number;
}

export async function getPayments(params: PaymentsParams): Promise<PagedResult<PaymentSummary>> {
  return apiFetch<PagedResult<PaymentSummary>>(`${BASE}/payments${query({ ...params })}`);
}

export interface PaymentCollector {
  kind: string;
  staffAccountId: string | null;
  staffName: string | null;
  courierCode: string | null;
  courierName: string | null;
}

export interface PaymentEarmark {
  kind: string;
  sourceId: string;
  label: string;
  isReleased: boolean;
  releasedOn: string | null;
  releaseReason: string | null;
}

export interface PaymentEntry {
  entryId: string;
  kind: string;
  invoiceId: string;
  invoiceNumber: string | null;
  amount: Money;
  occurredOn: string;
  by: string;
  releaseReason: string | null;
}

export interface PaymentRefundRow {
  refundId: string;
  method: string;
  amount: Money;
  businessDate: string;
  creditNoteId: string | null;
  creditNoteNumber: string | null;
}

export interface PaymentApprovalTrail {
  recordedBy: string;
  recordedByName: string;
  recordedOn: string;
  approvedBy: string | null;
  approvedByName: string | null;
  approvedOn: string | null;
  rejectedBy: string | null;
  rejectedByName: string | null;
  rejectedOn: string | null;
  rejectionReason: string | null;
  reversedBy: string | null;
  reversedByName: string | null;
  reversedOn: string | null;
  reversalReason: string | null;
}

export interface RequestedAllocation {
  invoiceId: string;
  invoiceNumber: string | null;
  amount: Money;
}

export interface PaymentDetail {
  summary: PaymentSummary;
  notes: string | null;
  acceptedOn: string | null;
  businessDate: string | null;
  collector: PaymentCollector | null;
  earmark: PaymentEarmark | null;
  entries: PaymentEntry[];
  refunds: PaymentRefundRow[];
  trail: PaymentApprovalTrail;
  refunded: Money;
  requestedAllocations: RequestedAllocation[];
  allowedActions: string[];
}

export async function getPayment(paymentId: string): Promise<PaymentDetail> {
  return apiFetch<PaymentDetail>(`${BASE}/payments/${id(paymentId)}`);
}

export interface PaymentReceipt {
  receiptNumber: string | null;
  seller: Seller | null;
  branchName: string;
  customerName: string;
  method: string;
  reference: string | null;
  amount: Money;
  receivedOn: string;
  acceptedOn: string | null;
  status: string;
  appliedTo: string[];
  recordedByName: string;
  branchTimeZoneId: string;
}

export async function getPaymentReceipt(paymentId: string): Promise<PaymentReceipt> {
  return apiFetch<PaymentReceipt>(`${BASE}/payments/${id(paymentId)}/receipt`);
}

export interface RecordPaymentRequest {
  customerId: string;
  method: string;
  amount: string;
  receivedOn: string;
  externalReference?: string;
  collector?: {
    kind: 'Staff' | 'Courier';
    staffAccountId?: string;
    courierCode?: string;
    courierName?: string;
  };
  earmark?: { kind: 'Order' | 'BoardingStay'; sourceId: string; label: string };
  allocations: Array<{ invoiceId: string; amount: string }>;
  notes?: string;
}

export interface AppliedAllocation {
  invoiceId: string;
  number: string | null;
  amount: Money;
}

export interface PaymentRecordResult {
  paymentId: string;
  status: PaymentStatus;
  receiptNumber: string | null;
  applied: AppliedAllocation[];
  unallocated: Money;
  invoiceOutstanding: Array<{
    invoiceId: string;
    number: string | null;
    outstanding: Money;
    status: string;
  }>;
}

export async function recordPayment(
  request: RecordPaymentRequest,
  idempotencyKey: string,
): Promise<PaymentRecordResult> {
  return apiFetch<PaymentRecordResult>(`${BASE}/payments`, {
    method: 'POST',
    body: {
      customerId: request.customerId,
      method: request.method,
      amount: wireMoney(request.amount),
      receivedOn: request.receivedOn,
      externalReference: request.externalReference,
      collector: request.collector,
      earmark: request.earmark,
      allocations: request.allocations.map((a) => ({
        invoiceId: a.invoiceId,
        amount: wireMoney(a.amount),
      })),
      notes: request.notes,
    },
    headers: { 'Idempotency-Key': idempotencyKey },
  });
}

export interface PendingApproval {
  paymentId: string;
  customerId: string;
  customerName: string;
  method: string;
  amount: Money;
  receivedOn: string;
  recordedOn: string;
  recordedBy: string;
  recordedByName: string;
  externalReference: string | null;
  canApprove: boolean;
  requestedAllocations: RequestedAllocation[];
}

export async function getApprovals(): Promise<PendingApproval[]> {
  return apiFetch<PendingApproval[]>(`${BASE}/approvals`);
}

export interface ApproveResult {
  applied: AppliedAllocation[];
  leftUnallocated: Money;
}

export async function approvePayment(paymentId: string): Promise<ApproveResult> {
  return apiFetch<ApproveResult>(`${BASE}/payments/${id(paymentId)}/approve`, { method: 'POST' });
}

export async function rejectPayment(paymentId: string, reason: string): Promise<void> {
  await apiFetch<void>(`${BASE}/payments/${id(paymentId)}/reject`, {
    method: 'POST',
    body: { reason },
  });
}

export interface AllocateResult {
  applied: Array<AppliedAllocation & { outstanding: Money }>;
  unallocated: Money;
}

export async function allocatePayment(
  paymentId: string,
  allocations: Array<{ invoiceId: string; amount: string }>,
): Promise<AllocateResult> {
  return apiFetch<AllocateResult>(`${BASE}/payments/${id(paymentId)}/allocations`, {
    method: 'POST',
    body: {
      allocations: allocations.map((a) => ({ invoiceId: a.invoiceId, amount: wireMoney(a.amount) })),
    },
  });
}

export async function releaseEarmark(paymentId: string, reason: string): Promise<void> {
  await apiFetch<void>(`${BASE}/payments/${id(paymentId)}/release-earmark`, {
    method: 'POST',
    body: { reason },
  });
}

export async function reversePayment(paymentId: string, reason: string): Promise<void> {
  await apiFetch<void>(`${BASE}/payments/${id(paymentId)}/reverse`, {
    method: 'POST',
    body: { reason },
  });
}

export interface ApplyCreditResult {
  applied: Array<{ invoiceId: string; number: string | null; amount: Money }>;
  freeCreditRemaining: Money;
  invoiceOutstanding: Money;
}

export async function applyCustomerCredit(
  customerId: string,
  request: { invoiceId: string; amount?: string },
): Promise<ApplyCreditResult> {
  const raw = await apiFetch<{
    applied: Array<{ invoiceId: string; number: string | null; amount: unknown }>;
    freeCreditRemaining: unknown;
    invoiceOutstanding: unknown;
  }>(`${BASE}/customers/${id(customerId)}/apply-credit`, {
    method: 'POST',
    body: {
      invoiceId: request.invoiceId,
      amount: request.amount ? wireMoney(request.amount) : undefined,
    },
  });
  return {
    applied: raw.applied.map((a) => ({ ...a, amount: moneyFromWire(a.amount) })),
    freeCreditRemaining: moneyFromWire(raw.freeCreditRemaining),
    invoiceOutstanding: moneyFromWire(raw.invoiceOutstanding),
  };
}

// ---- refunds --------------------------------------------------------------------------------

export interface IssueRefundRequest {
  paymentId: string;
  creditNoteId?: string;
  method: string;
  amount: string;
  reference?: string;
  reason: string;
}

export async function issueRefund(
  request: IssueRefundRequest,
  idempotencyKey: string,
): Promise<{ refundId: string }> {
  return apiFetch<{ refundId: string }>(`${BASE}/refunds`, {
    method: 'POST',
    body: {
      paymentId: request.paymentId,
      creditNoteId: request.creditNoteId,
      method: request.method,
      amount: wireMoney(request.amount),
      reference: request.reference,
      reason: request.reason,
    },
    headers: { 'Idempotency-Key': idempotencyKey },
  });
}

export interface RefundListItem {
  refundId: string;
  paymentId: string;
  receiptNumber: string | null;
  creditNoteId: string | null;
  creditNoteNumber: string | null;
  customerId: string;
  customerName: string;
  method: string;
  amount: Money;
  reference: string | null;
  reason: string;
  businessDate: string;
  issuedOn: string;
  issuedBy: string;
  issuedByName: string;
}

export async function getRefunds(params: {
  from?: string;
  to?: string;
  pageNumber?: number | string;
}): Promise<PagedResult<RefundListItem>> {
  return apiFetch<PagedResult<RefundListItem>>(`${BASE}/refunds${query({ ...params })}`);
}

// ---- receivables ----------------------------------------------------------------------------

export interface AgeingBuckets {
  current: Money;
  days1To30: Money;
  days31To60: Money;
  days61To90: Money;
  over90: Money;
}

export interface AgeingRow extends AgeingBuckets {
  customerId: string;
  customerName: string;
  total: Money;
  oldestDueOn: string;
}

export interface Ageing {
  asOf: string;
  totals: AgeingBuckets;
  rows: PagedResult<AgeingRow>;
}

export async function getAgeing(params: {
  asOf?: string;
  pageNumber?: number | string;
  pageSize?: number;
}): Promise<Ageing> {
  return apiFetch<Ageing>(`${BASE}/receivables/ageing${query({ ...params })}`);
}
