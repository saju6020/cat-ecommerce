import { apiFetch } from './client';
import type { Money } from '../format/money';
import type { CartCat } from './cart';

/**
 * `lib/api/checkout.ts` — HO-ORD-002 §8.2. The checkout quote (`CheckoutQuoteViewModels.cs`) and
 * placement (`CustomerOrdersController.Place`). **The quote is the single source of truth**
 * (ADR-FE-090): prices, charges, slots, payment availability and `canPlace` are the server's; the
 * browser sends the customer's choices and the quote's `fingerprint` back, never a price or total.
 */

export type FulfilmentChoice = 'Delivery' | 'Pickup';
export type PaymentMethodChoice = 'CashOnDelivery' | 'BankTransfer' | 'PayOnCollection';
export type ServiceLevel = 'Standard' | 'Express';

/** The customer's choices — the checkout URL's search params (ADR-FE-090), sent as the quote's query. */
export interface CheckoutChoices {
  fulfilment: FulfilmentChoice;
  addressId?: string;
  serviceLevel?: ServiceLevel;
  slotDate?: string;
  slotWindowStart?: string;
  pickupBranchId?: string;
  paymentMethod?: PaymentMethodChoice;
  discountCode?: string;
}

export interface QuoteLine {
  lineRef: string;
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
}

export interface ExcludedLine {
  lineRef: string;
  skuId: string;
  productName: string;
  cat: CartCat | null;
  quantity: number;
  reasonCode: string;
}

export type CheckoutChangeKind =
  | 'PriceIncreased'
  | 'PriceDecreased'
  | 'QuantityReduced'
  | 'NowUnavailable'
  | 'PrescriptionAuthorisationExpired'
  | 'PrescriptionCatRequired';

export interface CheckoutChange {
  kind: CheckoutChangeKind;
  lineRef: string;
  productName: string;
  before: string | null;
  after: string | null;
  /** A full sentence written by the server, naming the product — rendered as given. */
  message: string;
}

export interface DeliverySlot {
  date: string;
  windowStart: string;
  windowEnd: string;
  label: string;
}

export interface QuoteServiceLevel {
  level: ServiceLevel;
  charge: Money;
  /**
   * The order value above which this level is free in the zone, and what is still needed to get there
   * (never below zero). Both `null` when the level has no free-delivery threshold. The browser never works
   * these out (ADR-FE-105).
   */
  freeOver: Money | null;
  amountToFreeDelivery: Money | null;
  leadTimeDays: number;
  slots: DeliverySlot[];
}

export interface QuoteDelivery {
  available: boolean;
  unavailableReason: string | null;
  zoneId: string | null;
  zoneName: string | null;
  serviceLevels: QuoteServiceLevel[];
}

export interface PickupBranch {
  id: string;
  name: string;
  address: string | null;
  phone: string | null;
}

export interface PaymentOption {
  method: PaymentMethodChoice;
  available: boolean;
  unavailableReason: string | null;
  message: string | null;
}

export interface QuoteDiscount {
  code: string;
  applied: boolean;
  reasonCode: string | null;
  message: string | null;
}

/** The six components of every total (checkout, order detail, receipt) — server strings. */
export interface OrderTotals {
  subtotal: Money;
  lineDiscountTotal: Money;
  orderDiscount: Money;
  deliveryCharge: Money;
  taxTotal: Money;
  grandTotal: Money;
}

export interface PendingPolicy {
  key: string;
  versionNumber: number;
  title: string;
}

export interface CheckoutQuote {
  lines: QuoteLine[];
  excludedLines: ExcludedLine[];
  changes: CheckoutChange[];
  delivery: QuoteDelivery;
  selectedServiceLevel: ServiceLevel | null;
  pickupBranches: PickupBranch[];
  paymentOptions: PaymentOption[];
  discount: QuoteDiscount | null;
  totals: OrderTotals;
  pendingPolicies: PendingPolicy[];
  canPlace: boolean;
  /** Violation codes — mapped to sentences by `describeBlockingReason`, never rendered. */
  blockingReasons: string[];
  fingerprint: string;
  currencyCode: string;
}

export function quoteQueryString(choices: CheckoutChoices): string {
  const params = new URLSearchParams();
  params.set('fulfilment', choices.fulfilment);
  const optional: Array<[string, string | undefined]> = [
    ['addressId', choices.fulfilment === 'Delivery' ? choices.addressId : undefined],
    ['serviceLevel', choices.fulfilment === 'Delivery' ? choices.serviceLevel : undefined],
    ['slotDate', choices.fulfilment === 'Delivery' ? choices.slotDate : undefined],
    ['slotWindowStart', choices.fulfilment === 'Delivery' ? choices.slotWindowStart : undefined],
    ['pickupBranchId', choices.fulfilment === 'Pickup' ? choices.pickupBranchId : undefined],
    ['paymentMethod', choices.paymentMethod],
    ['discountCode', choices.discountCode],
  ];
  for (const [key, value] of optional) if (value) params.set(key, value);
  return params.toString();
}

export function getCheckoutQuote(choices: CheckoutChoices): Promise<CheckoutQuote> {
  return apiFetch<CheckoutQuote>(`/api/customers/me/checkout/quote?${quoteQueryString(choices)}`);
}

export interface PlaceOrderRequest extends CheckoutChoices {
  recipientName?: string;
  recipientPhone?: string;
  quoteFingerprint: string;
}

export interface TransferInstructions {
  accountName: string;
  accountNumber: string;
  bankName: string;
  branchName: string;
  routingNumber: string | null;
  paymentReference: string;
  amountDue: Money;
}

export interface PlacedOrder {
  orderId: string;
  orderNumber: string;
  status: string;
  grandTotal: Money;
  paymentDueBy: string | null;
  transferInstructions: TransferInstructions | null;
}

/** `POST /api/customers/me/orders` — one `Idempotency-Key` per attempt (ADR-FE-097). No retry here. */
export function placeOrder(
  request: PlaceOrderRequest,
  idempotencyKey: string,
): Promise<PlacedOrder> {
  const body = {
    fulfilment: request.fulfilment,
    deliveryAddressId: request.fulfilment === 'Delivery' ? request.addressId : undefined,
    serviceLevel: request.fulfilment === 'Delivery' ? request.serviceLevel : undefined,
    slotDate: request.fulfilment === 'Delivery' ? request.slotDate : undefined,
    slotWindowStart: request.fulfilment === 'Delivery' ? request.slotWindowStart : undefined,
    pickupBranchId: request.fulfilment === 'Pickup' ? request.pickupBranchId : undefined,
    paymentMethod: request.paymentMethod,
    discountCode: request.discountCode || undefined,
    recipientName: request.recipientName || undefined,
    recipientPhone: request.recipientPhone || undefined,
    quoteFingerprint: request.quoteFingerprint,
  };
  return apiFetch<PlacedOrder>('/api/customers/me/orders', {
    method: 'POST',
    body,
    idempotencyKey,
  });
}
