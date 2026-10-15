import { apiFetch } from './client';
import type { Money } from '../format/money';
import { wireMoney } from '../money';
import type { PagedResult } from './types';
import type { CartCat } from './cart';
import type { OrderTotals } from './checkout';
import type {
  OrderFulfilment,
  OrderFulfilmentView,
  OrderPaymentMethod,
  OrderStatus,
} from './my-orders';
import type { Courier } from './ordering-admin';
import { downloadBillingFile } from './billing-staff';

/**
 * `lib/api/staff-orders.ts` — HO-ORD-002 §8.2. The staff side of orders and fulfilment, as the real
 * `StaffOrdersController`, `StaffFulfilmentController` and `StaffOrderReconciliationController` send
 * them. The branch is the active branch (`X-Branch-Id`, added by `client.ts`). Staff actions are
 * rendered only from `allowedActions` (ADR-FE-093); a `403` is the server's refusal.
 */

export type StaffOrderFlag =
  | 'AwaitingApproval'
  | 'TransferReported'
  | 'CourierReportsDelivered'
  | 'CollectionOverdue'
  | 'CustomerAnonymised';

export interface StaffOrderRow {
  id: string;
  orderNumber: string;
  placedOn: string;
  customerName: string;
  status: OrderStatus;
  fulfilmentMethod: OrderFulfilment;
  paymentMethod: OrderPaymentMethod;
  channel: 'Online' | 'Counter';
  grandTotal: Money;
  ageMinutes: number;
  flags: StaffOrderFlag[];
  branchTimeZoneId: string;
}

export interface StaffOrderFilters {
  status?: string;
  fulfilment?: string;
  paymentMethod?: string;
  channel?: string;
  from?: string;
  to?: string;
  search?: string;
  pageNumber?: number;
  pageSize?: number;
}

export interface StaffAllocation {
  batchId: string;
  batchNumber: string;
  expiresOn: string | null;
  locationId: string;
  locationName: string;
  quantity: number;
}

export interface StaffOrderLine {
  lineId: string;
  lineNumber: number;
  skuId: string;
  productName: string;
  variantLabel: string;
  cat: CartCat | null;
  quantity: number;
  unitPrice: Money;
  lineTotal: Money;
  quantityReturned: number;
  allocations: StaffAllocation[];
}

export interface StaffStatusChange {
  from: OrderStatus | null;
  to: OrderStatus;
  changedOn: string;
  changedBy: string;
  changedByName: string | null;
  note: string | null;
}

export interface HeldPayment {
  amount: Money;
  /** The recording staff member's id — the API sends no name for it. */
  recordedBy: string;
  recordedOn: string;
}

export interface StaffCodCollection {
  amount: Money;
  collectorKind: 'Courier' | 'Staff';
  collectorName: string;
  collectedOn: string;
  recordedBy: string;
}

export interface StaffShipment {
  courierId: string;
  courierName: string;
  trackingNumber: string | null;
  trackingUrl: string | null;
  dispatchedOn: string;
  lastTrackingStatus: string | null;
  lastTrackingEventOn: string | null;
}

export interface StaffPaymentRef {
  paymentId: string;
  method: string;
  amount: Money;
  recordedOn: string;
}

/** `OrderTransitionAction` names the API sends, filtered to what this caller may do. */
export type StaffOrderAction =
  | 'ConfirmPayment'
  | 'Cancel'
  | 'StartPacking'
  | 'Dispatch'
  | 'MarkReadyForCollection'
  | 'RecordDelivery'
  | 'RecordDeliveryFailure'
  | 'Refund';

export interface StaffOrderDetail {
  id: string;
  version: number;
  orderNumber: string;
  placedOn: string;
  channel: 'Online' | 'Counter';
  customerId: string | null;
  status: OrderStatus;
  fulfilmentMethod: OrderFulfilment;
  paymentMethod: OrderPaymentMethod;
  branchTimeZoneId: string;
  lines: StaffOrderLine[];
  totals: OrderTotals;
  fulfilment: OrderFulfilmentView;
  statusHistory: StaffStatusChange[];
  paymentDueBy: string | null;
  customerTransferReference: string | null;
  heldPayment: HeldPayment | null;
  codCollection: StaffCodCollection | null;
  shipment: StaffShipment | null;
  collectBy: string | null;
  invoiceId: string | null;
  invoiceNumber: string | null;
  payments: StaffPaymentRef[];
  creditNoteIds: string[];
  amountPaid: Money;
  amountRefunded: Money;
  refundDue: Money;
  discountCode: string | null;
  customerAnonymised: boolean;
  allowedActions: StaffOrderAction[];
}

function query(params: Record<string, string | number | undefined>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') search.set(key, String(value));
  }
  const text = search.toString();
  return text ? `?${text}` : '';
}

const orderPath = (orderId: string) => `/api/staff/orders/${encodeURIComponent(orderId)}`;

export function listStaffOrders(filters: StaffOrderFilters): Promise<PagedResult<StaffOrderRow>> {
  return apiFetch(`/api/staff/orders${query({ ...filters })}`);
}

/** One row of `GET /api/staff/customers/{customerId}/orders` (`StaffCustomerOrderRowViewModel`): no customer name, age or flags. */
export type StaffCustomerOrderRow = Omit<StaffOrderRow, 'customerName' | 'ageMinutes' | 'flags'>;

/** HO-ORD-001-FU2: the customer's orders at the active branch, newest first. `404` = no such customer. */
export function listStaffCustomerOrders(
  customerId: string,
  params: { pageNumber?: number; pageSize?: number } = {},
): Promise<PagedResult<StaffCustomerOrderRow>> {
  return apiFetch(`/api/staff/customers/${encodeURIComponent(customerId)}/orders${query({ ...params })}`);
}

export function getStaffOrder(orderId: string): Promise<StaffOrderDetail> {
  return apiFetch(orderPath(orderId));
}

// ---- payment ---------------------------------------------------------------------------------------

export interface AwaitingPaymentOrder {
  id: string;
  orderNumber: string;
  customerName: string;
  amount: Money;
  paymentDueBy: string | null;
  customerTransferReference: string | null;
  transferReportedOn: string | null;
  heldPayment: HeldPayment | null;
  version: number;
  branchTimeZoneId: string;
}

export function listAwaitingPayment(): Promise<AwaitingPaymentOrder[]> {
  return apiFetch('/api/staff/orders/awaiting-payment');
}

export interface ConfirmTransferInput {
  expectedVersion: number;
  /** The money string the person typed or the order's amount — sent as a JSON number literal. */
  amount: string;
  bankReference: string;
  /** ISO instant. */
  receivedOn: string;
}

export function confirmBankTransfer(
  orderId: string,
  input: ConfirmTransferInput,
  idempotencyKey: string,
): Promise<{ status: string; awaitingApproval: boolean }> {
  return apiFetch(`${orderPath(orderId)}/payment/bank-transfer`, {
    method: 'POST',
    body: { ...input, amount: wireMoney(input.amount) },
    idempotencyKey,
  });
}

export function approveHeldPayment(
  orderId: string,
  expectedVersion: number,
  idempotencyKey: string,
): Promise<{ status: string }> {
  return apiFetch(`${orderPath(orderId)}/payment/approve`, {
    method: 'POST',
    body: { expectedVersion },
    idempotencyKey,
  });
}

export function rejectHeldPayment(orderId: string, note: string): Promise<void> {
  return apiFetch(`${orderPath(orderId)}/payment/reject`, { method: 'POST', body: { note } });
}

export function cancelStaffOrder(orderId: string, note: string): Promise<void> {
  return apiFetch(`${orderPath(orderId)}/cancel`, { method: 'POST', body: { note } });
}

// ---- fulfilment ------------------------------------------------------------------------------------

export function startPacking(orderId: string): Promise<void> {
  return apiFetch(`${orderPath(orderId)}/packing`, { method: 'POST' });
}

export function dispatchOrder(
  orderId: string,
  input: { courierId: string; trackingNumber?: string },
): Promise<void> {
  return apiFetch(`${orderPath(orderId)}/dispatch`, { method: 'POST', body: input });
}

export function markReadyForCollection(orderId: string): Promise<void> {
  return apiFetch(`${orderPath(orderId)}/ready-for-collection`, { method: 'POST' });
}

export function recordTracking(orderId: string, trackingNumber: string): Promise<void> {
  return apiFetch(`${orderPath(orderId)}/tracking`, { method: 'PUT', body: { trackingNumber } });
}

export interface RecordDeliveryInput {
  expectedVersion: number;
  /** ISO instant. */
  deliveredOn: string;
  codCollection?: {
    amount: string;
    collectorKind: 'Courier' | 'Staff';
    collectorName?: string;
    collectorStaffId?: string;
    collectedOn: string;
  };
  collectionPayment?: { method: 'Cash' | 'InStoreCard'; amount: string; reference?: string };
}

export function recordDelivery(
  orderId: string,
  input: RecordDeliveryInput,
  idempotencyKey: string,
): Promise<void> {
  const body = {
    expectedVersion: input.expectedVersion,
    deliveredOn: input.deliveredOn,
    codCollection: input.codCollection
      ? { ...input.codCollection, amount: wireMoney(input.codCollection.amount) }
      : undefined,
    collectionPayment: input.collectionPayment
      ? { ...input.collectionPayment, amount: wireMoney(input.collectionPayment.amount) }
      : undefined,
  };
  return apiFetch(`${orderPath(orderId)}/delivery`, { method: 'POST', body, idempotencyKey });
}

export type DeliveryFailureReason = 'Refused' | 'Undeliverable' | 'Uncollected';

/** `201 { returnRequestId }` for a refused / undeliverable parcel; `204` for an uncollected pickup. */
export function recordDeliveryFailure(
  orderId: string,
  input: { reason: DeliveryFailureReason; note?: string },
): Promise<{ returnRequestId: string } | undefined> {
  return apiFetch(`${orderPath(orderId)}/delivery-failure`, { method: 'POST', body: input });
}

export function reallocateLine(
  orderId: string,
  lineId: string,
  input: { targetBatchId: string; reason: string },
): Promise<void> {
  return apiFetch(`${orderPath(orderId)}/lines/${encodeURIComponent(lineId)}/reallocate`, {
    method: 'POST',
    body: input,
  });
}

/** The active couriers a dispatch chooses from (`Orders.Fulfil`; the admin list is `Couriers.Manage`). */
export function listDispatchCouriers(): Promise<Courier[]> {
  return apiFetch('/api/staff/fulfilment/couriers');
}

// ---- pick list, packing slip, manifest ---------------------------------------------------------------

export interface PickListOrder {
  orderNumber: string;
  quantity: number;
}

export interface PickListBatch {
  batchId: string;
  batchNumber: string;
  expiresOn: string | null;
  skuId: string;
  productName: string;
  variantLabel: string;
  quantity: number;
  orders: PickListOrder[];
}

export interface PickListGroup {
  locationId: string;
  locationName: string;
  batches: PickListBatch[];
}

export interface PickList {
  generatedOn: string;
  groups: PickListGroup[];
}

export function getPickList(orderIds?: string[]): Promise<PickList> {
  return apiFetch(
    `/api/staff/fulfilment/pick-list${query({ orderIds: orderIds && orderIds.length > 0 ? orderIds.join(',') : undefined })}`,
  );
}

export interface PackingSlipLine {
  lineNumber: number;
  productName: string;
  variantLabel: string;
  quantity: number;
  catName: string | null;
  batchNumbers: string[];
}

export interface PackingSlip {
  orderId: string;
  orderNumber: string;
  placedOn: string;
  customerName: string;
  recipientName: string;
  recipientPhone: string | null;
  fulfilmentMethod: OrderFulfilment;
  addressText: string | null;
  pickupBranchName: string | null;
  slot: string | null;
  lines: PackingSlipLine[];
  codAmountDue: Money | null;
  deliveryInstructions: string | null;
  branchTimeZoneId: string;
}

export function getPackingSlip(orderId: string): Promise<PackingSlip> {
  return apiFetch(`${orderPath(orderId)}/packing-slip`);
}

/** Saves a branch-scoped file through the one XHR download path (CSRF and `X-Branch-Id` kept; no second fetch site). */
export function downloadOrderingFile(path: string, fallbackName: string): Promise<void> {
  return downloadBillingFile(path, fallbackName);
}

/** The path of the courier manifest CSV — downloaded through `downloadOrderingFile` (headers kept). */
export function manifestPath(courierId: string, date: string): string {
  return `/api/staff/fulfilment/manifest.csv${query({ courierId, date })}`;
}

// ---- reconciliation issues ---------------------------------------------------------------------------

export interface ReconciliationIssue {
  id: string;
  orderId: string | null;
  kind: string;
  paymentMethod: string;
  amount: Money;
  paymentReference: string | null;
  paymentReceivedOn: string | null;
  errorSummary: string;
  raisedBy: string;
  raisedOn: string;
  status: 'Open' | 'Resolved';
  resolvedBy: string | null;
  resolvedOn: string | null;
  resolutionNote: string | null;
}

export function listReconciliationIssues(filters: {
  status?: string;
  pageNumber?: number;
  pageSize?: number;
}): Promise<PagedResult<ReconciliationIssue>> {
  return apiFetch(`/api/staff/orders/reconciliation-issues${query({ ...filters })}`);
}

export function resolveReconciliationIssue(issueId: string, note: string): Promise<void> {
  return apiFetch(
    `/api/staff/orders/reconciliation-issues/${encodeURIComponent(issueId)}/resolve`,
    { method: 'POST', body: { note } },
  );
}
