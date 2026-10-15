import { clearPersisted, loadPersisted, savePersisted } from '../admin/fixtures';
import { findSku } from '../shop/store';
import { orderingIds as ids } from './fixtures';

/**
 * `mocks/handlers/ordering/store.ts` — HO-ORD-002 §12.1. One in-memory (sessionStorage-persisted, like
 * the other mock stores) Ordering database plus the *mock server's* rules. Amounts are integer minor
 * units here and ONLY here; every response renders them as the API does
 * (`{ amount: "1250.00", currencyCode: "BDT" }`). The real UI never computes any of this
 * (ADR-FE-092). The catalogue (names, prices, in-stock flags, prescription flag, barcodes) is the SHOP
 * mock's, so Add to cart on a real product page reaches a real cart line.
 */

const PERSIST_KEY = '__purrcare_ordering_mock_db__';
export const CURRENCY = 'BDT';
export const ZONE = ids.timeZoneId;
export const DEFAULT_STOCK = 25;
export const APPROVAL_THRESHOLD_CENTS = 5_000_000; // BDT 50,000.00 — a transfer above it needs a second person
export const MAX_QUANTITY_PER_LINE = 20;
export const MAX_CART_LINES = 50;

export type Cents = number;

export function money(cents: Cents): { amount: string; currencyCode: string } {
  const sign = cents < 0 ? '-' : '';
  const abs = Math.abs(cents);
  return {
    amount: `${sign}${Math.trunc(abs / 100)}.${String(abs % 100).padStart(2, '0')}`,
    currencyCode: CURRENCY,
  };
}

/** `"850.00"` → 85000 by string handling (a typed or wire amount). */
export function centsOfString(text: string): Cents {
  const [whole = '0', fraction = ''] = text.replace(/[^0-9.]/g, '').split('.');
  return Number(whole) * 100 + Number(fraction.padEnd(2, '0').slice(0, 2));
}

/** A request amount (a JSON number from `JSON.rawJSON`, or a string) → minor units. */
export function toCents(value: unknown): Cents {
  const n = typeof value === 'number' ? value : Number.parseFloat(String(value ?? '0'));
  return Number.isFinite(n) ? Math.round(n * 100) : 0;
}

export function formatCents(cents: Cents): string {
  const { amount } = money(cents);
  const [whole = '0', fraction = '00'] = amount.split('.');
  return `${CURRENCY} ${whole.replace(/\B(?=(\d{3})+(?!\d))/g, ',')}.${fraction}`;
}

// ---- database types ---------------------------------------------------------------------------------

export interface DbCartLine {
  lineId: string;
  skuId: string;
  quantity: number;
  catId: string | null;
  priceWhenAddedCents: Cents;
}

export interface DbAllocation {
  batchId: string;
  batchNumber: string;
  expiresOn: string | null;
  locationId: string;
  locationName: string;
  quantity: number;
}

export interface DbOrderLine {
  lineId: string;
  lineNumber: number;
  skuId: string;
  productName: string;
  variantLabel: string;
  catId: string | null;
  quantity: number;
  unitCents: Cents;
  subtotalCents: Cents;
  discountCents: Cents;
  shareCents: Cents;
  taxRatePercent: number;
  taxCents: Cents;
  totalCents: Cents;
  quantityReturned: number;
  returnClass: 'Food' | 'Litter' | 'Other';
  allocations: DbAllocation[];
}

export type OrderStatusName =
  | 'PendingPayment'
  | 'Confirmed'
  | 'Packing'
  | 'Dispatched'
  | 'Delivered'
  | 'Cancelled'
  | 'Returned'
  | 'Refunded';

export interface DbHistory {
  from: OrderStatusName | null;
  to: OrderStatusName;
  changedOn: string;
  changedBy: string;
  changedByName: string | null;
  note: string | null;
}

export interface DbFulfilment {
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

export interface DbOrder {
  id: string;
  version: number;
  orderNumber: string;
  placedOn: string;
  channel: 'Online' | 'Counter';
  customerId: string | null;
  customerName: string;
  customerMobile: string | null;
  status: OrderStatusName;
  fulfilmentMethod: 'Delivery' | 'Pickup' | 'Counter';
  paymentMethod: 'CashOnDelivery' | 'BankTransfer' | 'PayOnCollection' | 'InStore';
  cancellationReason: 'CustomerRequest' | 'StaffDecision' | 'PaymentWindowExpired' | null;
  lines: DbOrderLine[];
  subtotalCents: Cents;
  lineDiscountCents: Cents;
  orderDiscountCents: Cents;
  deliveryChargeCents: Cents;
  taxCents: Cents;
  grandTotalCents: Cents;
  fulfilment: DbFulfilment;
  history: DbHistory[];
  paymentDueBy: string | null;
  customerTransferReference: string | null;
  transferReportedOn: string | null;
  held: { amountCents: Cents; bankReference: string; recordedBy: string; recordedOn: string } | null;
  cod: {
    amountCents: Cents;
    collectorKind: 'Courier' | 'Staff';
    collectorName: string;
    collectedOn: string;
    recordedBy: string;
  } | null;
  shipment: {
    courierId: string;
    courierName: string;
    trackingNumber: string | null;
    trackingUrl: string | null;
    dispatchedOn: string;
    lastTrackingStatus: string | null;
  } | null;
  collectBy: string | null;
  invoiceNumber: string | null;
  amountPaidCents: Cents;
  amountRefundedCents: Cents;
  refundDueCents: Cents;
  discountCode: string | null;
  returnWindowEndsOn: string | null;
  courierReportsDelivered: boolean;
  tenders: Array<{ method: 'Cash' | 'InStoreCard'; amountCents: Cents; reference: string | null }>;
  changeDueCents: Cents;
  cashier: string | null;
}

export type ReturnStatusName = 'Requested' | 'Approved' | 'Rejected' | 'Received' | 'Closed';

export interface DbReturnLine {
  orderLineId: string;
  quantity: number;
  reason: string;
  condition: string;
  customerNote: string | null;
  defectConfirmed: boolean;
  defectNote: string | null;
  quantityReceived: number;
}

export interface DbReturn {
  id: string;
  version: number;
  orderId: string;
  kind: 'CustomerReturn' | 'DeliveryFailure';
  status: ReturnStatusName;
  requestedOn: string;
  requestedBy: string;
  decidedOn: string | null;
  decidedBy: string | null;
  decisionNote: string | null;
  receivedOn: string | null;
  lines: DbReturnLine[];
  recalledBatchLines: string[];
}

export interface DbRefund {
  id: string;
  orderId: string;
  returnRequestId: string | null;
  method: string;
  amountCents: Cents;
  reference: string | null;
  reason: string;
  issuedOn: string;
}

export interface DbIssue {
  id: string;
  orderId: string | null;
  kind: string;
  paymentMethod: string;
  amountCents: Cents;
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

export interface DbZoneService {
  serviceLevel: 'Standard' | 'Express';
  isActive: boolean;
  baseChargeCents: Cents;
  freeOverCents: Cents | null;
  leadTimeDays: number;
  weightBands: Array<{ upToGrams: number | null; surchargeCents: Cents }>;
  slotWindows: Array<{ dayOfWeek: string; start: string; end: string }>;
}

export interface DbZone {
  id: string;
  branchId: string;
  name: string;
  priority: number;
  isActive: boolean;
  codEnabled: boolean;
  codMaxCents: Cents | null;
  version: number;
  coverage: Array<{ district: string; postalCodes: string[] }>;
  services: DbZoneService[];
}

export interface DbCourier {
  id: string;
  name: string;
  trackingUrlTemplate: string | null;
  supportsCod: boolean;
  isActive: boolean;
}

export interface DbCode {
  id: string;
  code: string;
  description: string | null;
  kind: 'Percentage' | 'FixedAmount';
  percent: number | null;
  amountCents: Cents | null;
  minimumSpendCents: Cents | null;
  scope: 'Order' | 'Products' | 'Categories';
  productIds: string[];
  categoryIds: number[];
  firstOrderOnly: boolean;
  usageLimit: number | null;
  perCustomerLimit: number | null;
  validFrom: string | null;
  validTo: string | null;
  isActive: boolean;
  timesRedeemed: number;
  version: number;
}

export interface DbBank {
  configured: boolean;
  accountName: string | null;
  accountNumber: string | null;
  bankName: string | null;
  branchName: string | null;
  routingNumber: string | null;
  isActive: boolean;
}

export interface Db {
  guestLines: DbCartLine[];
  customerLines: DbCartLine[];
  orders: DbOrder[];
  returns: DbReturn[];
  refunds: DbRefund[];
  issues: DbIssue[];
  zones: DbZone[];
  couriers: DbCourier[];
  codes: DbCode[];
  bank: DbBank;
  stock: Record<string, number>;
  /** Cats whose prescription authorisation has lapsed (E-B6). */
  lapsedCats: string[];
  /** Batch ids a recall has been recorded against (BatchRecalled on dispatch, U-D3). */
  recalledBatches: string[];
  /** The staff member the mock acts as (`__orderingMock.actAs`). */
  actor: string;
  transferWindowSeconds: number;
  taxRatePercent: number;
  pendingPolicies: Array<{ key: string; versionNumber: number; title: string }>;
  nextOrderSeq: number;
  nextInvoiceSeq: number;
  /** key → the status and body the first call answered (a repeat replays it). */
  idempotency: Record<string, { status: number; body: unknown }>;
  inFlightKeys: string[];
}

// ---- time ---------------------------------------------------------------------------------------------

export const nowIso = (): string => new Date().toISOString();
export const minutesAgo = (minutes: number): string =>
  new Date(Date.now() - minutes * 60_000).toISOString();
export const hoursFromNow = (hours: number): string =>
  new Date(Date.now() + hours * 3_600_000).toISOString();

export function todayLocal(): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: ZONE }).format(new Date());
}

export function addDays(dateIso: string, days: number): string {
  const [y = 1970, m = 1, d = 1] = dateIso.split('-').map(Number);
  return new Date(Date.UTC(y, m - 1, d + days)).toISOString().slice(0, 10);
}

export function weekdayName(dateIso: string): string {
  const [y = 1970, m = 1, d = 1] = dateIso.split('-').map(Number);
  return new Intl.DateTimeFormat('en-US', { weekday: 'long', timeZone: 'UTC' }).format(
    new Date(Date.UTC(y, m - 1, d)),
  );
}

// ---- catalogue (the shop mock's) ----------------------------------------------------------------------

export interface SkuInfo {
  skuId: string;
  productId: string;
  productName: string;
  variantLabel: string;
  code: string;
  barcode: string | null;
  priceCents: Cents | null;
  purchasable: boolean;
  requiresPrescription: boolean;
  isBundle: boolean;
  returnClass: 'Food' | 'Litter' | 'Other';
  categoryId: number;
}

export function skuInfo(skuId: string): SkuInfo | undefined {
  const found = findSku(skuId);
  if (!found) return undefined;
  const { product, sku } = found;
  return {
    skuId,
    productId: product.productId,
    productName: product.name,
    variantLabel: sku.sizeOrWeight ?? sku.flavour ?? sku.colour ?? sku.code,
    code: sku.code,
    barcode: sku.barcode,
    priceCents: sku.price ? centsOfString(sku.price) : null,
    purchasable: product.status === 'Active' && sku.status === 'Active' && sku.price !== null,
    requiresPrescription: product.prescriptionRequired,
    isBundle: product.kind === 'Bundle',
    returnClass:
      product.category === 'CatFood' ? 'Food' : product.category === 'Litter' ? 'Litter' : 'Other',
    categoryId: 1,
  };
}

export function stockOf(skuId: string): number {
  const stored = db.stock[skuId];
  if (stored !== undefined) return stored;
  const found = findSku(skuId);
  return found && found.sku.inStock ? DEFAULT_STOCK : 0;
}

/** Cats the Rx products are authorised for (shop mock parity): the eligible-food SKU has two. */
export function authorisedCatsFor(skuId: string): string[] {
  if (skuId === ids.skuRxEligible) {
    // The cat-profiles mock's Whiskers and Mittens are the same cats under their own ids (POS picks from those).
    return [
      ids.catWhiskers,
      ids.catMittens,
      '20000000-0000-0000-0000-000000000001',
      '20000000-0000-0000-0000-000000000004',
    ].filter((id) => !db.lapsedCats.includes(id));
  }
  return [];
}

export const CAT_NAMES: Record<string, string> = {
  [ids.catWhiskers]: 'Whiskers',
  [ids.catMittens]: 'Mittens',
  [ids.catBiscuit]: 'Biscuit',
};

export const PICKUP_BRANCHES = [
  { id: ids.mainBranchId, name: 'PurrCare Dhanmondi', address: 'House 12, Road 5, Dhanmondi, Dhaka', phone: '+880 1700 000001' },
  { id: ids.secondBranchId, name: 'PurrCare Chattogram', address: '45 Agrabad Road, Chattogram', phone: '+880 1700 000002' },
];

export function batchFor(skuId: string, variant: 'A' | 'B' = 'A'): DbAllocation {
  const info = skuInfo(skuId);
  const code = info?.code ?? 'SKU';
  return {
    batchId: `ba000000-0000-0000-0000-${skuId.slice(-6)}00000${variant === 'A' ? 1 : 2}`,
    batchNumber: `LOT-${code}-${variant}`,
    expiresOn: variant === 'A' ? '2027-03-31' : '2027-09-30',
    locationId: ids.locationShelfId,
    locationName: 'Main shelf',
    quantity: 0,
  };
}

// ---- ids and numbering ----------------------------------------------------------------------------------

export const uuid = (): string => crypto.randomUUID();

export function nextOrderNumber(): string {
  const n = db.nextOrderSeq;
  db.nextOrderSeq += 1;
  return `ORD-2026-${String(n).padStart(6, '0')}`;
}

export function nextInvoiceNumber(): string {
  const n = db.nextInvoiceSeq;
  db.nextInvoiceSeq += 1;
  return `INV-MAIN-2627-${String(n).padStart(6, '0')}`;
}

// ---- seed -----------------------------------------------------------------------------------------------

function line(
  n: number,
  skuId: string,
  quantity: number,
  extra: Partial<DbOrderLine> = {},
): DbOrderLine {
  const info = skuInfo(skuId);
  const unit = info?.priceCents ?? 100_000;
  return {
    lineId: `fa000000-0000-0000-0000-${skuId.slice(-6)}00000${n}`,
    lineNumber: n,
    skuId,
    productName: info?.productName ?? 'Product',
    variantLabel: info?.variantLabel ?? '',
    catId: null,
    quantity,
    unitCents: unit,
    subtotalCents: unit * quantity,
    discountCents: 0,
    shareCents: 0,
    taxRatePercent: 0,
    taxCents: 0,
    totalCents: unit * quantity,
    quantityReturned: 0,
    returnClass: info?.returnClass ?? 'Other',
    allocations: [],
    ...extra,
  };
}

function allocated(l: DbOrderLine): DbOrderLine {
  return { ...l, allocations: [{ ...batchFor(l.skuId), quantity: l.quantity }] };
}

interface OrderSpec {
  id: string;
  seq: number;
  status: OrderStatusName;
  placedMinutesAgo: number;
  fulfilmentMethod?: DbOrder['fulfilmentMethod'];
  paymentMethod: DbOrder['paymentMethod'];
  lines: DbOrderLine[];
  channel?: 'Online' | 'Counter';
  customerName?: string;
  customerId?: string | null;
  deliveryChargeCents?: Cents;
  extra?: Partial<DbOrder>;
}

function orderFrom(spec: OrderSpec): DbOrder {
  const method = spec.fulfilmentMethod ?? 'Delivery';
  const subtotal = spec.lines.reduce((sum, l) => sum + l.subtotalCents, 0);
  const delivery = spec.deliveryChargeCents ?? (method === 'Delivery' ? 8000 : 0);
  const grand = subtotal + delivery;
  const placedOn = minutesAgo(spec.placedMinutesAgo);
  const customerName = spec.customerName ?? ids.customerName;
  const base: DbOrder = {
    id: spec.id,
    version: 1,
    orderNumber: `ORD-2026-${String(spec.seq).padStart(6, '0')}`,
    placedOn,
    channel: spec.channel ?? 'Online',
    customerId: spec.customerId === undefined ? ids.customerId : spec.customerId,
    customerName,
    customerMobile: ids.customerMobile,
    status: spec.status,
    fulfilmentMethod: method,
    paymentMethod: spec.paymentMethod,
    cancellationReason: null,
    lines: spec.lines,
    subtotalCents: subtotal,
    lineDiscountCents: 0,
    orderDiscountCents: 0,
    deliveryChargeCents: delivery,
    taxCents: 0,
    grandTotalCents: grand,
    fulfilment: {
      recipientName: customerName,
      recipientPhone: ids.customerMobile,
      line1: method === 'Delivery' ? 'House 7, Road 3, Dhanmondi' : null,
      line2: null,
      city: method === 'Delivery' ? 'Dhaka' : null,
      district: method === 'Delivery' ? 'Dhaka' : null,
      postalCode: method === 'Delivery' ? '1209' : null,
      deliveryInstructions: method === 'Delivery' ? 'Call from the gate.' : null,
      zoneName: method === 'Delivery' ? 'Dhaka City' : null,
      serviceLevel: method === 'Delivery' ? 'Standard' : null,
      slotDate: null,
      slotWindow: null,
      pickupBranchId: method === 'Pickup' ? ids.mainBranchId : null,
      pickupBranchName: method === 'Pickup' ? 'PurrCare Dhanmondi' : null,
      pickupBranchAddress: method === 'Pickup' ? 'House 12, Road 5, Dhanmondi, Dhaka' : null,
    },
    history: [
      {
        from: null,
        to: spec.paymentMethod === 'BankTransfer' ? 'PendingPayment' : 'Confirmed',
        changedOn: placedOn,
        changedBy: ids.customerId,
        changedByName: null,
        note: null,
      },
    ],
    paymentDueBy: null,
    customerTransferReference: null,
    transferReportedOn: null,
    held: null,
    cod: null,
    shipment: null,
    collectBy: null,
    invoiceNumber: null,
    amountPaidCents: 0,
    amountRefundedCents: 0,
    refundDueCents: 0,
    discountCode: null,
    returnWindowEndsOn: null,
    courierReportsDelivered: false,
    tenders: [],
    changeDueCents: 0,
    cashier: null,
  };
  return { ...base, ...spec.extra };
}

function step(order: DbOrder, to: OrderStatusName, minutes: number, by: string = ids.adminStaffId): void {
  const from = order.history[order.history.length - 1]?.to ?? null;
  order.history.push({
    from,
    to,
    changedOn: minutesAgo(minutes),
    changedBy: by,
    changedByName: by === ids.adminStaffId ? 'Amina Hossain' : 'Rahim Uddin',
    note: null,
  });
  order.status = to;
}

function seedOrders(): DbOrder[] {
  const kitten2 = ids.skuKitten2kg;
  const kitten400 = ids.skuKitten400;
  const whiskas = ids.skuWhiskas;

  const awaiting = orderFrom({
    id: ids.orders.awaitingTransfer,
    seq: 101,
    status: 'PendingPayment',
    placedMinutesAgo: 90,
    paymentMethod: 'BankTransfer',
    lines: [line(1, kitten2, 2)],
    extra: { paymentDueBy: hoursFromNow(30) },
  });

  const reported = orderFrom({
    id: ids.orders.transferReported,
    seq: 102,
    status: 'PendingPayment',
    placedMinutesAgo: 240,
    paymentMethod: 'BankTransfer',
    lines: [line(1, kitten400, 3)],
    customerName: 'Farhana Yasmin',
    customerId: 'c0000000-0000-0000-0000-0000000000a2',
    extra: {
      paymentDueBy: hoursFromNow(5.5),
      customerTransferReference: 'TRF-20261008-77',
      transferReportedOn: minutesAgo(30),
    },
  });

  const held = orderFrom({
    id: ids.orders.heldPayment,
    seq: 103,
    status: 'PendingPayment',
    placedMinutesAgo: 600,
    paymentMethod: 'BankTransfer',
    lines: [line(1, kitten2, 80)],
    customerName: 'Nusrat Jahan',
    customerId: 'c0000000-0000-0000-0000-0000000000a3',
    extra: {
      paymentDueBy: hoursFromNow(20),
      customerTransferReference: 'BRAC-55120',
      transferReportedOn: minutesAgo(200),
      invoiceNumber: 'INV-MAIN-2627-000412',
    },
  });
  held.held = {
    amountCents: held.grandTotalCents,
    bankReference: 'BRAC-55120',
    recordedBy: ids.otherStaffId,
    recordedOn: minutesAgo(45),
  };

  const confirmed = orderFrom({
    id: ids.orders.confirmedCod,
    seq: 104,
    status: 'Confirmed',
    placedMinutesAgo: 120,
    paymentMethod: 'CashOnDelivery',
    lines: [allocated(line(1, kitten2, 1)), allocated(line(2, whiskas, 2))],
  });

  const packing = orderFrom({
    id: ids.orders.packingPickup,
    seq: 105,
    status: 'Confirmed',
    placedMinutesAgo: 300,
    fulfilmentMethod: 'Pickup',
    paymentMethod: 'PayOnCollection',
    lines: [allocated(line(1, kitten400, 2))],
    extra: { collectBy: addDays(todayLocal(), 6) },
  });
  step(packing, 'Packing', 200);

  const dispatched = orderFrom({
    id: ids.orders.dispatchedCod,
    seq: 106,
    status: 'Confirmed',
    placedMinutesAgo: 1500,
    paymentMethod: 'CashOnDelivery',
    lines: [allocated(line(1, kitten2, 2))],
  });
  step(dispatched, 'Packing', 1400);
  step(dispatched, 'Dispatched', 1300);
  dispatched.shipment = {
    courierId: ids.courierPathaoId,
    courierName: 'Pathao Courier',
    trackingNumber: 'PC-88213004',
    trackingUrl: 'https://track.example/pathao/PC-88213004',
    dispatchedOn: minutesAgo(1300),
    lastTrackingStatus: 'Out for delivery',
  };

  const delivered = orderFrom({
    id: ids.orders.delivered,
    seq: 107,
    status: 'Confirmed',
    placedMinutesAgo: 4000,
    paymentMethod: 'CashOnDelivery',
    lines: [allocated(line(1, kitten2, 2)), allocated(line(2, kitten400, 1))],
  });
  step(delivered, 'Packing', 3900);
  step(delivered, 'Dispatched', 3800);
  step(delivered, 'Delivered', 3000);
  delivered.shipment = {
    courierId: ids.courierInHouseId,
    courierName: 'In-house rider',
    trackingNumber: null,
    trackingUrl: null,
    dispatchedOn: minutesAgo(3800),
    lastTrackingStatus: null,
  };
  delivered.cod = {
    amountCents: delivered.grandTotalCents,
    collectorKind: 'Courier',
    collectorName: 'Rafiq (rider)',
    collectedOn: minutesAgo(3000),
    recordedBy: ids.adminStaffId,
  };
  delivered.amountPaidCents = delivered.grandTotalCents;
  delivered.invoiceNumber = 'INV-MAIN-2627-000398';
  delivered.returnWindowEndsOn = addDays(todayLocal(), 5);

  const expired = orderFrom({
    id: ids.orders.expired,
    seq: 108,
    status: 'PendingPayment',
    placedMinutesAgo: 3200,
    paymentMethod: 'BankTransfer',
    lines: [line(1, kitten400, 2)],
  });
  step(expired, 'Cancelled', 100, ids.adminStaffId);
  expired.cancellationReason = 'PaymentWindowExpired';
  expired.paymentDueBy = minutesAgo(120);

  const counter = orderFrom({
    id: ids.orders.counterSale,
    seq: 109,
    status: 'Confirmed',
    placedMinutesAgo: 60,
    fulfilmentMethod: 'Counter',
    paymentMethod: 'InStore',
    channel: 'Counter',
    customerName: 'Walk-in customer',
    customerId: null,
    lines: [allocated(line(1, whiskas, 1))],
  });
  step(counter, 'Delivered', 59);
  counter.invoiceNumber = 'INV-MAIN-2627-000401';
  counter.amountPaidCents = counter.grandTotalCents;
  counter.tenders = [{ method: 'Cash', amountCents: 100_000, reference: null }];
  counter.changeDueCents = 100_000 - counter.grandTotalCents;
  counter.cashier = 'Amina Hossain';

  const openedFood = orderFrom({
    id: ids.orders.deliveredOpenedFood,
    seq: 110,
    status: 'Confirmed',
    placedMinutesAgo: 2500,
    paymentMethod: 'CashOnDelivery',
    lines: [allocated(line(1, whiskas, 1))],
  });
  step(openedFood, 'Packing', 2400);
  step(openedFood, 'Dispatched', 2300);
  step(openedFood, 'Delivered', 2000);
  openedFood.amountPaidCents = openedFood.grandTotalCents;
  openedFood.invoiceNumber = 'INV-MAIN-2627-000399';
  openedFood.returnWindowEndsOn = addDays(todayLocal(), 4);

  const cancelledPaid = orderFrom({
    id: ids.orders.cancelledPaid,
    seq: 111,
    status: 'PendingPayment',
    placedMinutesAgo: 900,
    paymentMethod: 'BankTransfer',
    lines: [line(1, kitten400, 1)],
    customerName: 'Tanvir Ahmed',
    customerId: 'c0000000-0000-0000-0000-0000000000a4',
  });
  step(cancelledPaid, 'Confirmed', 800);
  step(cancelledPaid, 'Cancelled', 700);
  cancelledPaid.cancellationReason = 'StaffDecision';
  cancelledPaid.amountPaidCents = cancelledPaid.grandTotalCents;
  cancelledPaid.refundDueCents = cancelledPaid.grandTotalCents;
  cancelledPaid.invoiceNumber = 'INV-MAIN-2627-000397';

  return [
    awaiting,
    reported,
    held,
    confirmed,
    packing,
    dispatched,
    delivered,
    expired,
    counter,
    openedFood,
    cancelledPaid,
  ];
}

function seedReturns(): DbReturn[] {
  const base = { version: 1, requestedBy: ids.customerId, decidedOn: null, decidedBy: null, receivedOn: null };
  return [
    {
      ...base,
      id: ids.returns.requested,
      orderId: ids.orders.delivered,
      kind: 'CustomerReturn',
      status: 'Requested',
      requestedOn: minutesAgo(180),
      decisionNote: null,
      recalledBatchLines: [],
      lines: [
        {
          orderLineId: seedOrders()[6]!.lines[1]!.lineId,
          quantity: 1,
          reason: 'WrongItem',
          condition: 'Sealed',
          customerNote: 'This is the 400 g, I ordered 2 kg.',
          defectConfirmed: false,
          defectNote: null,
          quantityReceived: 0,
        },
      ],
    },
    {
      ...base,
      id: ids.returns.requestedDefect,
      orderId: ids.orders.deliveredOpenedFood,
      kind: 'CustomerReturn',
      status: 'Requested',
      requestedOn: minutesAgo(90),
      decisionNote: null,
      recalledBatchLines: [],
      lines: [
        {
          orderLineId: seedOrders()[9]!.lines[0]!.lineId,
          quantity: 1,
          reason: 'QualityDefect',
          condition: 'Opened',
          customerNote: 'The kibble smelled stale.',
          defectConfirmed: false,
          defectNote: null,
          quantityReceived: 0,
        },
      ],
    },
  ];
}

function seedZones(): DbZone[] {
  const windows = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Saturday', 'Sunday'].flatMap(
    (dayOfWeek) => [
      { dayOfWeek, start: '10:00', end: '13:00' },
      { dayOfWeek, start: '14:00', end: '17:00' },
    ],
  );
  return [
    {
      id: ids.zoneDhakaId,
      branchId: ids.mainBranchId,
      name: 'Dhaka City',
      priority: 1,
      isActive: true,
      codEnabled: true,
      codMaxCents: 500_000,
      version: 1,
      coverage: [
        { district: 'Dhaka', postalCodes: [] },
        { district: 'Narayanganj', postalCodes: ['1400', '1410'] },
      ],
      services: [
        {
          serviceLevel: 'Standard',
          isActive: true,
          baseChargeCents: 8000,
          freeOverCents: 500_000,
          leadTimeDays: 1,
          weightBands: [{ upToGrams: 5000, surchargeCents: 0 }, { upToGrams: null, surchargeCents: 5000 }],
          slotWindows: windows,
        },
        {
          serviceLevel: 'Express',
          isActive: true,
          baseChargeCents: 15_000,
          freeOverCents: null,
          leadTimeDays: 0,
          weightBands: [],
          slotWindows: [],
        },
      ],
    },
    {
      id: ids.zoneInactiveId,
      branchId: ids.mainBranchId,
      name: 'Chattogram (paused)',
      priority: 2,
      isActive: false,
      codEnabled: false,
      codMaxCents: null,
      version: 1,
      coverage: [{ district: 'Chattogram', postalCodes: [] }],
      services: [
        {
          serviceLevel: 'Standard',
          isActive: true,
          baseChargeCents: 12_000,
          freeOverCents: null,
          leadTimeDays: 3,
          weightBands: [],
          slotWindows: [],
        },
      ],
    },
  ];
}

function seedCodes(): DbCode[] {
  const base = {
    description: null,
    productIds: [] as string[],
    categoryIds: [] as number[],
    firstOrderOnly: false,
    usageLimit: null,
    perCustomerLimit: null,
    validFrom: null,
    validTo: null,
    isActive: true,
    timesRedeemed: 0,
    version: 1,
  };
  return [
    {
      ...base,
      id: ids.codeWelcomeId,
      code: 'WELCOME10',
      description: '10% off your order',
      kind: 'Percentage',
      percent: 10,
      amountCents: null,
      minimumSpendCents: null,
      scope: 'Order',
      timesRedeemed: 12,
    },
    {
      ...base,
      id: ids.codeSaveId,
      code: 'SAVE100',
      description: 'BDT 100 off orders of BDT 5,000 or more',
      kind: 'FixedAmount',
      percent: null,
      amountCents: 10_000,
      minimumSpendCents: 500_000,
      scope: 'Order',
      perCustomerLimit: 1,
    },
    {
      ...base,
      id: ids.codeExpiredId,
      code: 'EARLYBIRD',
      kind: 'Percentage',
      percent: 15,
      amountCents: null,
      minimumSpendCents: null,
      scope: 'Order',
      validTo: '2026-08-31T00:00:00Z',
      timesRedeemed: 40,
    },
  ];
}

function seedIssues(): DbIssue[] {
  return [
    {
      id: ids.issueId,
      orderId: ids.orders.transferReported,
      kind: 'TransferConfirmationFailed',
      paymentMethod: 'BankTransfer',
      amountCents: 270_000,
      paymentReference: 'TRF-20261008-77',
      paymentReceivedOn: minutesAgo(60),
      errorSummary: 'Billing could not record the payment.',
      raisedBy: ids.adminStaffId,
      raisedOn: minutesAgo(55),
      status: 'Open',
      resolvedBy: null,
      resolvedOn: null,
      resolutionNote: null,
    },
  ];
}

function seed(): Db {
  return {
    guestLines: [],
    customerLines: [],
    orders: seedOrders(),
    returns: seedReturns(),
    refunds: [],
    issues: seedIssues(),
    zones: seedZones(),
    couriers: [
      { id: ids.courierPathaoId, name: 'Pathao Courier', trackingUrlTemplate: 'https://track.example/pathao/{tracking}', supportsCod: true, isActive: true },
      { id: ids.courierInHouseId, name: 'In-house rider', trackingUrlTemplate: null, supportsCod: true, isActive: true },
    ],
    codes: seedCodes(),
    bank: {
      configured: true,
      accountName: 'PurrCare Veterinary Ltd',
      accountNumber: '1234567890123',
      bankName: 'BRAC Bank',
      branchName: 'Gulshan',
      routingNumber: '060261726',
      isActive: true,
    },
    stock: { [ids.skuWhiskas]: 1 },
    lapsedCats: [],
    recalledBatches: [],
    actor: ids.adminStaffId,
    transferWindowSeconds: 48 * 3600,
    taxRatePercent: 0,
    pendingPolicies: [],
    nextOrderSeq: 200,
    nextInvoiceSeq: 500,
    idempotency: {},
    inFlightKeys: [],
  };
}

export const db: Db = loadPersisted<Db>(PERSIST_KEY) ?? seed();

export function persist(): void {
  savePersisted(PERSIST_KEY, db);
}

export function resetOrderingMockData(): void {
  Object.assign(db, seed());
  clearPersisted(PERSIST_KEY);
}

export function findOrder(orderId: string): DbOrder | undefined {
  return db.orders.find((o) => o.id === orderId);
}

export function findReturn(returnId: string): DbReturn | undefined {
  return db.returns.find((r) => r.id === returnId);
}

export function staffNameOf(staffId: string): string {
  return staffId === ids.otherStaffId ? 'Rahim Uddin' : 'Amina Hossain';
}

/** Lazy expiry (the worker's job on the real server): an unpaid transfer past its window is cancelled. */
export function expireOverdueTransfers(): void {
  const now = Date.now();
  let changed = false;
  for (const order of db.orders) {
    if (
      order.status === 'PendingPayment' &&
      order.paymentMethod === 'BankTransfer' &&
      order.paymentDueBy &&
      order.held === null &&
      Date.parse(order.paymentDueBy) <= now
    ) {
      step(order, 'Cancelled', 0, ids.adminStaffId);
      order.cancellationReason = 'PaymentWindowExpired';
      order.version += 1;
      changed = true;
    }
  }
  if (changed) persist();
}

export { step as pushStatus };

/** FEFO allocation, once an order is confirmed: each line takes its SKU's first batch. */
export function allocateOrder(order: DbOrder): void {
  for (const l of order.lines) l.allocations = [{ ...batchFor(l.skuId), quantity: l.quantity }];
}
