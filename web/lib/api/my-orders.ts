import { apiFetch } from './client';
import type { Money } from '../format/money';
import type { PagedResult } from './types';
import type { CartCat } from './cart';
import type { OrderTotals, TransferInstructions } from './checkout';

/**
 * `lib/api/my-orders.ts` — HO-ORD-002 §8.2. The signed-in customer's orders, returns and re-order, as
 * `CustomerOrdersController` / `OrderViewModels.cs` send them. Every order is resolved by id and caller
 * server-side: someone else's order is a `404`.
 *
 * **HO-POR-001 integration note.** The portal dashboard (`ho/POR-001`, not on master yet) relies on the
 * list item gaining `canReorder` and `trackingNumber`, and on `POST …/reorder` for an expired order.
 * `reorderOrder` below already uses that endpoint (it is on master); when POR-001 merges, add the two
 * list-item fields to `CustomerOrderSummary` and nothing else in this file changes.
 */

export type OrderStatus =
  | 'PendingPayment'
  | 'Confirmed'
  | 'Packing'
  | 'Dispatched'
  | 'Delivered'
  | 'Cancelled'
  | 'Returned'
  | 'Refunded';
export type OrderFulfilment = 'Delivery' | 'Pickup' | 'Counter';
export type OrderPaymentMethod = 'CashOnDelivery' | 'BankTransfer' | 'PayOnCollection' | 'InStore';

export const ORDER_STATUSES: OrderStatus[] = [
  'PendingPayment',
  'Confirmed',
  'Packing',
  'Dispatched',
  'Delivered',
  'Cancelled',
  'Returned',
  'Refunded',
];

export interface CustomerOrderSummary {
  id: string;
  orderNumber: string;
  placedOn: string;
  status: OrderStatus;
  fulfilmentMethod: OrderFulfilment;
  paymentMethod: OrderPaymentMethod;
  grandTotal: Money;
  itemCount: number;
  branchTimeZoneId: string;
}

export interface OrderLine {
  lineId: string;
  lineNumber: number;
  skuId: string;
  productName: string;
  variantLabel: string;
  cat: CartCat | null;
  quantity: number;
  unitPrice: Money;
  lineSubtotal: Money;
  lineDiscount: Money;
  orderDiscountShare: Money;
  taxRatePercent: number;
  taxAmount: Money;
  lineTotal: Money;
  /** The product's current photo, a signed API-relative URL (customer views only; absent or null when none). */
  imagePath?: string | null;
}

export interface OrderFulfilmentView {
  recipientName: string;
  recipientPhone: string | null;
  line1: string | null;
  line2: string | null;
  city: string | null;
  district: string | null;
  postalCode: string | null;
  deliveryInstructions: string | null;
  zoneName: string | null;
  serviceLevel: string | null;
  slotDate: string | null;
  slotWindow: string | null;
  pickupBranchId: string | null;
  pickupBranchName: string | null;
  pickupBranchAddress: string | null;
}

export interface CustomerStatusChange {
  to: OrderStatus;
  changedOn: string;
}

export interface CustomerShipment {
  courierName: string;
  trackingNumber: string | null;
  trackingUrl: string | null;
  lastTrackingStatus: string | null;
}

export interface ReturnableLine {
  lineId: string;
  maxQuantity: number;
  returnClass: 'Food' | 'Litter' | 'Other';
}

export interface CustomerRefund {
  method: string;
  amount: Money;
  issuedOn: string;
}

export interface CustomerReturnSummary {
  id: string;
  status: string;
}

export interface CustomerOrderDetail {
  id: string;
  orderNumber: string;
  placedOn: string;
  status: OrderStatus;
  fulfilmentMethod: OrderFulfilment;
  paymentMethod: OrderPaymentMethod;
  branchTimeZoneId: string;
  lines: OrderLine[];
  totals: OrderTotals;
  fulfilment: OrderFulfilmentView;
  statusHistory: CustomerStatusChange[];
  paymentDueBy: string | null;
  transferInstructions: TransferInstructions | null;
  customerTransferReference: string | null;
  shipment: CustomerShipment | null;
  collectBy: string | null;
  invoiceNumber: string | null;
  amountPaid: Money;
  amountRefunded: Money;
  refundDue: Money;
  refunds: CustomerRefund[];
  returns: CustomerReturnSummary[];
  canCancel: boolean;
  canRequestReturn: boolean;
  returnWindowEndsOn: string | null;
  returnableLines: ReturnableLine[];
  canReorder: boolean;
}

export interface MyOrdersFilters {
  status?: string;
  pageNumber?: number;
  pageSize?: number;
}

function query(params: Record<string, string | number | undefined>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') search.set(key, String(value));
  }
  const text = search.toString();
  return text ? `?${text}` : '';
}

export function listMyOrders(filters: MyOrdersFilters): Promise<PagedResult<CustomerOrderSummary>> {
  return apiFetch(`/api/customers/me/orders${query({ ...filters })}`);
}

export function getMyOrder(orderId: string): Promise<CustomerOrderDetail> {
  return apiFetch(`/api/customers/me/orders/${encodeURIComponent(orderId)}`);
}

export function submitTransferReference(orderId: string, reference: string): Promise<void> {
  return apiFetch(`/api/customers/me/orders/${encodeURIComponent(orderId)}/transfer-reference`, {
    method: 'POST',
    body: { reference },
  });
}

export function cancelMyOrder(orderId: string): Promise<void> {
  return apiFetch(`/api/customers/me/orders/${encodeURIComponent(orderId)}/cancel`, {
    method: 'POST',
  });
}

export interface ReorderResult {
  addedCount: number;
  unavailable: Array<{ productName: string; reason: string }>;
}

/** `POST …/reorder` is idempotent at the edge (a key is required) — one key per click. */
export function reorderOrder(orderId: string, idempotencyKey: string): Promise<ReorderResult> {
  return apiFetch(`/api/customers/me/orders/${encodeURIComponent(orderId)}/reorder`, {
    method: 'POST',
    idempotencyKey,
  });
}

export type ReturnReason =
  'WrongItem' | 'Damaged' | 'QualityDefect' | 'ChangedMind' | 'Recalled' | 'Other';
export type ItemCondition = 'Sealed' | 'Opened' | 'Used';

export interface ReturnLineInput {
  orderLineId: string;
  quantity: number;
  reason: ReturnReason;
  condition: ItemCondition;
  customerNote?: string;
}

export function requestMyReturn(
  orderId: string,
  lines: ReturnLineInput[],
): Promise<{ returnRequestId: string; status: string }> {
  return apiFetch(`/api/customers/me/orders/${encodeURIComponent(orderId)}/returns`, {
    method: 'POST',
    body: { lines },
  });
}

export interface CustomerReturnLine {
  orderLineId: string;
  productName: string;
  variantLabel: string;
  quantity: number;
  reason: string;
  condition: string;
  customerNote: string | null;
  quantityReceived: number;
}

export interface CustomerReturn {
  id: string;
  orderId: string;
  orderNumber: string;
  kind: string;
  status: string;
  requestedOn: string;
  decisionNote: string | null;
  lines: CustomerReturnLine[];
}

export function getMyReturn(returnRequestId: string): Promise<CustomerReturn> {
  return apiFetch(`/api/customers/me/returns/${encodeURIComponent(returnRequestId)}`);
}
