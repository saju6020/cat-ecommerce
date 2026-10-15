import { http, HttpResponse, type HttpHandler } from 'msw';
import {
  hasCustomerSession,
  noContent,
  orderingIds as ids,
  paged,
  pageParams,
  unauthorised,
  notFound,
  violation,
  violations,
} from './fixtures';
import {
  CAT_NAMES,
  MAX_CART_LINES,
  MAX_QUANTITY_PER_LINE,
  PICKUP_BRANCHES,
  addDays,
  allocateOrder,
  authorisedCatsFor,
  db,
  expireOverdueTransfers,
  findOrder,
  findReturn,
  hoursFromNow,
  money,
  nextInvoiceNumber,
  nextOrderNumber,
  nowIso,
  persist,
  pushStatus,
  skuInfo,
  stockOf,
  todayLocal,
  uuid,
  type DbCartLine,
  type DbOrder,
  type DbOrderLine,
} from './store';
import { computeQuote, type QuoteChoicesInput, type QuoteLineInput } from './quote';
import { testControl } from './control';

/**
 * `mocks/handlers/ordering/customer.ts` — HO-ORD-002 §12.1. The cart, checkout and customer-order
 * endpoints of HO-ORD-001 §12.4, shaped like `CartController`, `CustomerCheckoutController`,
 * `CustomerOrdersController` and `CustomerReturnsController`. The signed-in customer is the
 * `pc_session` cookie the accounts mock sets; the guest cart is one shared slot (the real API keys
 * it on the `HttpOnly` `pc_cart` cookie, which a mock cannot model across contexts).
 */

const API = '*/api';

interface AddressRecord {
  id: string;
  label: string;
  line1: string;
  line2: string | null;
  city: string;
  district: string;
  postalCode: string | null;
  deliveryInstructions: string | null;
}

async function fetchAddress(request: Request, addressId: string): Promise<AddressRecord | null> {
  try {
    const origin = new URL(request.url).origin;
    const response = await fetch(`${origin}/api/customers/me/addresses`, { credentials: 'include' });
    if (!response.ok) return null;
    const book = (await response.json()) as { addresses: AddressRecord[] };
    return book.addresses.find((a) => a.id === addressId) ?? null;
  } catch {
    return null;
  }
}

function myLines(request: Request): DbCartLine[] {
  return hasCustomerSession(request) ? db.customerLines : db.guestLines;
}

function cartView(request: Request) {
  const lines = myLines(request);
  const views = lines.map((l) => {
    const info = skuInfo(l.skuId);
    const stock = stockOf(l.skuId);
    const purchasable = !!info && info.purchasable && info.priceCents !== null && stock > 0;
    const unit = info?.priceCents ?? null;
    let prescriptionStatus: string | null = null;
    if (info?.requiresPrescription) {
      prescriptionStatus = !l.catId
        ? 'CatRequired'
        : authorisedCatsFor(l.skuId).includes(l.catId)
          ? 'Authorised'
          : 'NotAuthorised';
    }
    return {
      lineId: l.lineId,
      skuId: l.skuId,
      productId: info?.productId ?? l.skuId,
      productName: info?.productName ?? 'Unavailable product',
      variantLabel: info?.variantLabel ?? '',
      imagePath: null,
      quantity: l.quantity,
      cat: l.catId ? { id: l.catId, name: CAT_NAMES[l.catId] ?? 'Your cat' } : null,
      unitPrice: unit !== null ? money(unit) : null,
      priceWhenAdded: money(l.priceWhenAddedCents),
      priceChanged: unit !== null && unit !== l.priceWhenAddedCents,
      availability: !purchasable ? 'Unavailable' : stock < l.quantity ? 'Limited' : 'InStock',
      availableQuantity: purchasable && stock < l.quantity ? stock : null,
      requiresPrescription: info?.requiresPrescription ?? false,
      prescriptionStatus,
      lineSubtotal: purchasable && unit !== null ? money(unit * l.quantity) : null,
    };
  });
  const subtotal = lines.reduce((sum, l) => {
    const info = skuInfo(l.skuId);
    return info?.priceCents && stockOf(l.skuId) > 0 ? sum + info.priceCents * l.quantity : sum;
  }, 0);
  return {
    cartId: lines.length > 0 ? 'ca000000-0000-0000-0000-000000000001' : null,
    lines: views,
    itemCount: lines.reduce((sum, l) => sum + l.quantity, 0),
    subtotal: money(subtotal),
    maxQuantityPerLine: MAX_QUANTITY_PER_LINE,
    guestCartPending: hasCustomerSession(request) && db.guestLines.length > 0,
  };
}

function addToCart(lines: DbCartLine[], skuId: string, quantity: number, catId: string | null) {
  const info = skuInfo(skuId);
  if (!info || !info.purchasable || info.priceCents === null) {
    return violation('SkuId', 'SkuUnavailable', "This item isn't available right now.");
  }
  if (stockOf(skuId) <= 0) {
    return violation('SkuId', 'SkuUnavailable', `'${info.productName}' is out of stock.`);
  }
  if (!Number.isInteger(quantity) || quantity < 1 || quantity > MAX_QUANTITY_PER_LINE) {
    return violation(
      'Quantity',
      'QuantityOutOfRange',
      `You can buy between 1 and ${MAX_QUANTITY_PER_LINE} of an item at a time.`,
    );
  }
  if (info.requiresPrescription) {
    if (!catId) {
      return violation('CatId', 'PrescriptionCatRequired', `Choose which cat '${info.productName}' is for.`);
    }
    if (!authorisedCatsFor(skuId).includes(catId)) {
      return violation(
        'CatId',
        'PrescriptionNotAuthorised',
        `${CAT_NAMES[catId] ?? 'That cat'} has no current vet authorisation for '${info.productName}'.`,
      );
    }
  }
  const existing = lines.find((l) => l.skuId === skuId && l.catId === (info.requiresPrescription ? catId : null));
  if (existing) {
    if (existing.quantity + quantity > MAX_QUANTITY_PER_LINE) {
      return violation('Quantity', 'QuantityOutOfRange', `You can buy at most ${MAX_QUANTITY_PER_LINE} of an item at a time.`);
    }
    existing.quantity += quantity;
    persist();
    return existing;
  }
  if (lines.length >= MAX_CART_LINES) {
    return violation('SkuId', 'CartFull', 'Your cart is full. Remove something before adding more.');
  }
  const line: DbCartLine = {
    lineId: uuid(),
    skuId,
    quantity,
    catId: info.requiresPrescription ? catId : null,
    priceWhenAddedCents: info.priceCents,
  };
  lines.push(line);
  persist();
  return line;
}

function quoteLines(request: Request): QuoteLineInput[] {
  return myLines(request).map((l) => ({
    lineId: l.lineId,
    skuId: l.skuId,
    quantity: l.quantity,
    catId: l.catId,
    priceWhenAddedCents: l.priceWhenAddedCents,
  }));
}

async function choicesFrom(
  request: Request,
  source: URLSearchParams | Record<string, string | null | undefined>,
): Promise<QuoteChoicesInput> {
  const get = (key: string): string | undefined => {
    const value = source instanceof URLSearchParams ? source.get(key) : source[key];
    return value ? value : undefined;
  };
  const fulfilment = get('fulfilment') === 'Pickup' ? 'Pickup' : 'Delivery';
  const addressId = get('addressId') ?? get('deliveryAddressId');
  let address: QuoteChoicesInput['address'];
  if (addressId) {
    const found = await fetchAddress(request, addressId);
    address = found ? { district: found.district, postalCode: found.postalCode } : null;
  }
  return {
    fulfilment,
    address,
    serviceLevel: get('serviceLevel'),
    slotDate: get('slotDate'),
    slotWindowStart: get('slotWindowStart'),
    pickupBranchId: get('pickupBranchId'),
    paymentMethod: get('paymentMethod'),
    discountCode: get('discountCode'),
    customerHasOrders: db.orders.some((o) => o.customerId === ids.customerId && o.channel === 'Online'),
  };
}

// ---- customer order views ---------------------------------------------------------------------------

const REORDERABLE = new Set(['Delivered', 'Cancelled', 'Returned', 'Refunded']);

function returnedQuantityOpen(order: DbOrder, lineId: string): number {
  return db.returns
    .filter((r) => r.orderId === order.id && r.status !== 'Rejected')
    .flatMap((r) => r.lines)
    .filter((l) => l.orderLineId === lineId)
    .reduce((sum, l) => sum + l.quantity, 0);
}

export function orderLineView(l: DbOrderLine) {
  return {
    lineId: l.lineId,
    lineNumber: l.lineNumber,
    skuId: l.skuId,
    productName: l.productName,
    variantLabel: l.variantLabel,
    cat: l.catId ? { id: l.catId, name: CAT_NAMES[l.catId] ?? 'Your cat' } : null,
    quantity: l.quantity,
    unitPrice: money(l.unitCents),
    lineSubtotal: money(l.subtotalCents),
    lineDiscount: money(l.discountCents),
    orderDiscountShare: money(l.shareCents),
    taxRatePercent: l.taxRatePercent,
    taxAmount: money(l.taxCents),
    lineTotal: money(l.totalCents),
  };
}

export function totalsView(o: DbOrder) {
  return {
    subtotal: money(o.subtotalCents),
    lineDiscountTotal: money(o.lineDiscountCents),
    orderDiscount: money(o.orderDiscountCents),
    deliveryCharge: money(o.deliveryChargeCents),
    taxTotal: money(o.taxCents),
    grandTotal: money(o.grandTotalCents),
  };
}

export function transferInstructionsOf(o: DbOrder) {
  if (o.status !== 'PendingPayment' || o.paymentMethod !== 'BankTransfer') return null;
  return {
    accountName: db.bank.accountName ?? '',
    accountNumber: db.bank.accountNumber ?? '',
    bankName: db.bank.bankName ?? '',
    branchName: db.bank.branchName ?? '',
    routingNumber: db.bank.routingNumber,
    paymentReference: o.orderNumber,
    amountDue: money(o.grandTotalCents),
  };
}

function customerSummary(o: DbOrder) {
  return {
    id: o.id,
    orderNumber: o.orderNumber,
    placedOn: o.placedOn,
    status: o.status,
    fulfilmentMethod: o.fulfilmentMethod,
    paymentMethod: o.paymentMethod,
    grandTotal: money(o.grandTotalCents),
    itemCount: o.lines.reduce((sum, l) => sum + l.quantity, 0),
    branchTimeZoneId: ids.timeZoneId,
  };
}

function customerDetail(o: DbOrder) {
  const returnable =
    o.status === 'Delivered' && o.returnWindowEndsOn !== null && o.returnWindowEndsOn >= todayLocal()
      ? o.lines
          .map((l) => ({
            lineId: l.lineId,
            maxQuantity: l.quantity - l.quantityReturned - returnedQuantityOpen(o, l.lineId),
            returnClass: l.returnClass,
          }))
          .filter((l) => l.maxQuantity > 0)
      : [];
  return {
    id: o.id,
    orderNumber: o.orderNumber,
    placedOn: o.placedOn,
    status: o.status,
    fulfilmentMethod: o.fulfilmentMethod,
    paymentMethod: o.paymentMethod,
    branchTimeZoneId: ids.timeZoneId,
    lines: o.lines.map(orderLineView),
    totals: totalsView(o),
    fulfilment: o.fulfilment,
    statusHistory: o.history.map((h) => ({ to: h.to, changedOn: h.changedOn })),
    paymentDueBy: o.status === 'PendingPayment' ? o.paymentDueBy : o.paymentMethod === 'BankTransfer' ? o.paymentDueBy : null,
    transferInstructions: transferInstructionsOf(o),
    customerTransferReference: o.customerTransferReference,
    shipment: o.shipment
      ? {
          courierName: o.shipment.courierName,
          trackingNumber: o.shipment.trackingNumber,
          trackingUrl: o.shipment.trackingUrl,
          lastTrackingStatus: o.shipment.lastTrackingStatus,
        }
      : null,
    collectBy: o.collectBy,
    invoiceNumber: o.invoiceNumber,
    amountPaid: money(o.amountPaidCents),
    amountRefunded: money(o.amountRefundedCents),
    refundDue: money(o.refundDueCents),
    refunds: db.refunds
      .filter((r) => r.orderId === o.id)
      .map((r) => ({ method: r.method, amount: money(r.amountCents), issuedOn: r.issuedOn })),
    returns: db.returns.filter((r) => r.orderId === o.id).map((r) => ({ id: r.id, status: r.status })),
    canCancel: o.status === 'PendingPayment' || o.status === 'Confirmed',
    canRequestReturn: returnable.length > 0,
    returnWindowEndsOn: o.status === 'Delivered' ? o.returnWindowEndsOn : null,
    returnableLines: returnable,
    canReorder: REORDERABLE.has(o.status),
  };
}

function releaseStock(order: DbOrder): void {
  for (const l of order.lines) db.stock[l.skuId] = stockOf(l.skuId) + l.quantity;
}


const mine = (orderId: string | readonly string[] | undefined): DbOrder | undefined => {
  const order = findOrder(String(orderId));
  return order && order.customerId === ids.customerId ? order : undefined;
};

// ---- handlers ------------------------------------------------------------------------------------------

export const customerOrderingHandlers: HttpHandler[] = [
  http.get(`${API}/cart`, ({ request }) =>
    HttpResponse.json(cartView(request), { headers: { 'Cache-Control': 'no-store' } }),
  ),

  http.post(`${API}/cart/lines`, async ({ request }) => {
    const body = (await request.json()) as { skuId: string; quantity?: number; catId?: string | null };
    const result = addToCart(myLines(request), body.skuId, body.quantity ?? 1, body.catId ?? null);
    if (result instanceof Response) return result;
    return HttpResponse.json({ lineId: result.lineId, cartId: 'ca000000-0000-0000-0000-000000000001' }, { status: 201 });
  }),

  http.put(`${API}/cart/lines/:lineId`, async ({ request, params }) => {
    const line = myLines(request).find((l) => l.lineId === params.lineId);
    if (!line) return notFound();
    const body = (await request.json()) as { quantity?: number | null; catId?: string | null };
    if (body.quantity !== undefined && body.quantity !== null) {
      if (!Number.isInteger(body.quantity) || body.quantity < 1 || body.quantity > MAX_QUANTITY_PER_LINE) {
        return violation('Quantity', 'QuantityOutOfRange', `You can buy between 1 and ${MAX_QUANTITY_PER_LINE} of an item at a time.`);
      }
      line.quantity = body.quantity;
    }
    if (body.catId) {
      if (!authorisedCatsFor(line.skuId).includes(body.catId)) {
        return violation('CatId', 'PrescriptionNotAuthorised', `${CAT_NAMES[body.catId] ?? 'That cat'} has no current vet authorisation for this food.`);
      }
      line.catId = body.catId;
    }
    persist();
    return noContent();
  }),

  http.delete(`${API}/cart/lines/:lineId`, ({ request, params }) => {
    const lines = myLines(request);
    const index = lines.findIndex((l) => l.lineId === params.lineId);
    if (index === -1) return notFound();
    lines.splice(index, 1);
    persist();
    return noContent();
  }),

  http.post(`${API}/customers/me/cart/merge`, ({ request }) => {
    if (!hasCustomerSession(request)) return unauthorised();
    const dropped: Array<{ productName: string; reason: string }> = [];
    let merged = 0;
    for (const guest of db.guestLines) {
      const existing = db.customerLines.find((l) => l.skuId === guest.skuId && l.catId === guest.catId);
      if (existing) {
        existing.quantity = Math.min(existing.quantity + guest.quantity, MAX_QUANTITY_PER_LINE);
        merged += 1;
      } else if (db.customerLines.length >= MAX_CART_LINES) {
        dropped.push({ productName: skuInfo(guest.skuId)?.productName ?? 'An item', reason: 'CartFull' });
      } else {
        db.customerLines.push({ ...guest });
        merged += 1;
      }
    }
    db.guestLines = [];
    persist();
    return HttpResponse.json({ mergedLineCount: merged, droppedLines: dropped });
  }),

  http.get(`${API}/customers/me/checkout/quote`, async ({ request }) => {
    if (!hasCustomerSession(request)) return unauthorised();
    const url = new URL(request.url);
    const fulfilment = url.searchParams.get('fulfilment');
    if (fulfilment !== 'Delivery' && fulfilment !== 'Pickup') {
      return HttpResponse.json({ error: 'fulfilment must be Delivery or Pickup.' }, { status: 400 });
    }
    const choices = await choicesFrom(request, url.searchParams);
    const result = computeQuote(quoteLines(request), choices);
    return HttpResponse.json(result.wire, { headers: { 'Cache-Control': 'no-store' } });
  }),

  http.post(`${API}/customers/me/orders`, async ({ request }) => {
    if (!hasCustomerSession(request)) return unauthorised();
    const key = request.headers.get('Idempotency-Key');
    if (!key) return violation('Idempotency-Key', 'IdempotencyKeyRequired', 'An Idempotency-Key header is required.');
    const replay = db.idempotency[key];
    if (replay) return HttpResponse.json(replay.body as object, { status: replay.status });
    if (db.inFlightKeys.includes(key) || testControl.failNextPlacement === 'RequestInProgress') {
      testControl.failNextPlacement = null;
      return violation('Idempotency-Key', 'RequestInProgress', 'This order is already being placed.', 409);
    }
    const body = (await request.json()) as Record<string, string | undefined>;
    if (testControl.failNextPlacement === 'StockNoLongerAvailable') {
      testControl.failNextPlacement = null;
      return violation(
        'Lines',
        'StockNoLongerAvailable',
        "Sorry — 'Royal Canin Kitten' has just sold out. It has been removed from your order; please review and place it again.",
        409,
      );
    }
    const choices = await choicesFrom(request, body as Record<string, string | undefined>);
    const result = computeQuote(quoteLines(request), choices);
    if (testControl.failNextPlacement === 'CheckoutQuoteChanged' || result.fingerprint !== body.quoteFingerprint) {
      testControl.failNextPlacement = null;
      return violation(
        'QuoteFingerprint',
        'CheckoutQuoteChanged',
        'Something changed while you were checking out — please review and place the order again.',
        409,
      );
    }
    if (!result.canPlace) {
      const code = result.blocking[0] ?? 'NothingToPlace';
      const property =
        code === 'PaymentMethodUnavailable' ? 'PaymentMethod' : code === 'PolicyAcceptanceRequired' ? 'Policies' : code === 'PrescriptionCatRequired' ? 'Lines' : 'FulfilmentMethod';
      return violation(property, code, '');
    }

    const method = choices.fulfilment;
    const payment = (body.paymentMethod ?? 'CashOnDelivery') as DbOrder['paymentMethod'];
    const address = body.deliveryAddressId ? await fetchAddress(request, body.deliveryAddressId) : null;
    const branch = PICKUP_BRANCHES.find((b) => b.id === body.pickupBranchId);
    const slot = result.zone && result.level && body.slotDate
      ? `${body.slotDate} ${body.slotWindowStart ?? ''}`.trim()
      : null;
    const lines: DbOrderLine[] = result.priced.map((p, index) => ({
      lineId: uuid(),
      lineNumber: index + 1,
      skuId: p.skuId,
      productName: p.productName,
      variantLabel: p.variantLabel,
      catId: p.catId,
      quantity: p.quantity,
      unitCents: p.unitCents,
      subtotalCents: p.subtotalCents,
      discountCents: p.discountCents,
      shareCents: p.shareCents,
      taxRatePercent: p.taxRatePercent,
      taxCents: p.taxCents,
      totalCents: p.totalCents,
      quantityReturned: 0,
      returnClass: p.returnClass,
      allocations: [],
    }));
    const placedOn = nowIso();
    const transfer = payment === 'BankTransfer';
    const order: DbOrder = {
      id: uuid(),
      version: 1,
      orderNumber: nextOrderNumber(),
      placedOn,
      channel: 'Online',
      customerId: ids.customerId,
      customerName: ids.customerName,
      customerMobile: ids.customerMobile,
      status: transfer ? 'PendingPayment' : 'Confirmed',
      fulfilmentMethod: method,
      paymentMethod: payment,
      cancellationReason: null,
      lines,
      subtotalCents: result.totals.subtotal,
      lineDiscountCents: result.totals.lineDiscount,
      orderDiscountCents: result.totals.orderDiscount,
      deliveryChargeCents: result.totals.delivery,
      taxCents: result.totals.tax,
      grandTotalCents: result.totals.grand,
      fulfilment: {
        recipientName: body.recipientName || ids.customerName,
        recipientPhone: body.recipientPhone || ids.customerMobile,
        line1: address?.line1 ?? null,
        line2: address?.line2 ?? null,
        city: address?.city ?? null,
        district: address?.district ?? null,
        postalCode: address?.postalCode ?? null,
        deliveryInstructions: address?.deliveryInstructions ?? null,
        zoneName: result.zone?.name ?? null,
        serviceLevel: method === 'Delivery' ? result.level : null,
        slotDate: body.slotDate ?? null,
        slotWindow: slot,
        pickupBranchId: method === 'Pickup' ? (branch?.id ?? null) : null,
        pickupBranchName: method === 'Pickup' ? (branch?.name ?? null) : null,
        pickupBranchAddress: method === 'Pickup' ? (branch?.address ?? null) : null,
      },
      history: [{ from: null, to: transfer ? 'PendingPayment' : 'Confirmed', changedOn: placedOn, changedBy: ids.customerId, changedByName: null, note: null }],
      paymentDueBy: transfer ? new Date(Date.now() + db.transferWindowSeconds * 1000).toISOString() : null,
      customerTransferReference: null,
      transferReportedOn: null,
      held: null,
      cod: null,
      shipment: null,
      collectBy: method === 'Pickup' ? addDays(todayLocal(), 7) : null,
      invoiceNumber: nextInvoiceNumber(),
      amountPaidCents: 0,
      amountRefundedCents: 0,
      refundDueCents: 0,
      discountCode: result.discountApplied ? (body.discountCode?.toUpperCase() ?? null) : null,
      returnWindowEndsOn: null,
      courierReportsDelivered: false,
      tenders: [],
      changeDueCents: 0,
      cashier: null,
    };
    for (const l of order.lines) db.stock[l.skuId] = stockOf(l.skuId) - l.quantity;
    if (!transfer) allocateOrder(order);
    db.orders.unshift(order);
    myLines(request).length = 0;
    const response = {
      orderId: order.id,
      orderNumber: order.orderNumber,
      status: order.status,
      grandTotal: money(order.grandTotalCents),
      paymentDueBy: order.paymentDueBy,
      transferInstructions: transferInstructionsOf(order),
    };
    db.idempotency[key] = { status: 201, body: response };
    persist();
    return HttpResponse.json(response, { status: 201 });
  }),

  http.get(`${API}/customers/me/orders`, ({ request }) => {
    if (!hasCustomerSession(request)) return unauthorised();
    expireOverdueTransfers();
    const url = new URL(request.url);
    const status = url.searchParams.get('status');
    const { pageNumber, pageSize } = pageParams(url, 20);
    const rows = db.orders
      .filter((o) => o.customerId === ids.customerId)
      .filter((o) => !status || o.status === status)
      .sort((a, b) => b.placedOn.localeCompare(a.placedOn))
      .map(customerSummary);
    return HttpResponse.json(paged(rows, pageNumber, pageSize));
  }),

  http.get(`${API}/customers/me/orders/:orderId`, ({ request, params }) => {
    if (!hasCustomerSession(request)) return unauthorised();
    expireOverdueTransfers();
    const order = mine(params.orderId);
    return order ? HttpResponse.json(customerDetail(order)) : notFound();
  }),

  http.post(`${API}/customers/me/orders/:orderId/transfer-reference`, async ({ request, params }) => {
    if (!hasCustomerSession(request)) return unauthorised();
    const order = mine(params.orderId);
    if (!order) return notFound();
    const { reference } = (await request.json()) as { reference?: string };
    if (order.status !== 'PendingPayment' || order.paymentMethod !== 'BankTransfer') {
      return violation('OrderId', 'OrderNotAwaitingPayment', 'This order is not waiting for a payment.');
    }
    const text = (reference ?? '').trim();
    if (!text) return violation('Reference', 'InvalidReference', 'Enter the reference your bank gave the transfer.');
    if (text.length > 100) return violation('Reference', 'InvalidReference', 'The reference can be up to 100 characters.');
    order.customerTransferReference = text;
    order.transferReportedOn = nowIso();
    persist();
    return noContent();
  }),

  http.post(`${API}/customers/me/orders/:orderId/cancel`, ({ request, params }) => {
    if (!hasCustomerSession(request)) return unauthorised();
    const order = mine(params.orderId);
    if (!order) return notFound();
    if (order.status !== 'PendingPayment' && order.status !== 'Confirmed') {
      return violation(
        'OrderId',
        'CancellationNotAvailable',
        "This order can't be cancelled online any more. Please contact the shop.",
      );
    }
    pushStatus(order, 'Cancelled', 0, ids.customerId);
    order.cancellationReason = 'CustomerRequest';
    if (order.amountPaidCents > 0) order.refundDueCents = order.amountPaidCents - order.amountRefundedCents;
    releaseStock(order);
    order.version += 1;
    persist();
    return noContent();
  }),

  http.post(`${API}/customers/me/orders/:orderId/reorder`, ({ request, params }) => {
    if (!hasCustomerSession(request)) return unauthorised();
    const key = request.headers.get('Idempotency-Key');
    if (!key) return violation('Idempotency-Key', 'IdempotencyKeyRequired', 'An Idempotency-Key header is required.');
    const replay = db.idempotency[key];
    if (replay) return HttpResponse.json(replay.body as object, { status: replay.status });
    const order = mine(params.orderId);
    if (!order) return notFound();
    const unavailable: Array<{ productName: string; reason: string }> = [];
    let added = 0;
    for (const l of order.lines) {
      const info = skuInfo(l.skuId);
      if (!info || !info.purchasable) {
        unavailable.push({ productName: l.productName, reason: 'NotPurchasable' });
      } else if (stockOf(l.skuId) <= 0) {
        unavailable.push({ productName: l.productName, reason: 'OutOfStock' });
      } else if (info.requiresPrescription && (!l.catId || !authorisedCatsFor(l.skuId).includes(l.catId))) {
        unavailable.push({ productName: l.productName, reason: 'PrescriptionNotAuthorised' });
      } else {
        const result = addToCart(db.customerLines, l.skuId, l.quantity, l.catId);
        if (result instanceof Response) unavailable.push({ productName: l.productName, reason: 'SkuUnavailable' });
        else added += 1;
      }
    }
    const response = { addedCount: added, unavailable };
    db.idempotency[key] = { status: 200, body: response };
    persist();
    return HttpResponse.json(response);
  }),

  http.post(`${API}/customers/me/orders/:orderId/returns`, async ({ request, params }) => {
    if (!hasCustomerSession(request)) return unauthorised();
    const order = mine(params.orderId);
    if (!order) return notFound();
    const body = (await request.json()) as {
      lines: Array<{ orderLineId: string; quantity: number; reason: string; condition: string; customerNote?: string }>;
    };
    const detail = customerDetail(order);
    if (!detail.canRequestReturn) {
      return violation('OrderId', 'ReturnWindowClosed', 'The return window for this order has closed.');
    }
    const problems: Array<{ propertyName: string; errorCode: string; errorMessage: string }> = [];
    body.lines.forEach((line, index) => {
      const orderLine = order.lines.find((l) => l.lineId === line.orderLineId);
      const allowed = detail.returnableLines.find((l) => l.lineId === line.orderLineId);
      if (!orderLine || !allowed) {
        problems.push({ propertyName: `Lines[${index}]`, errorCode: 'ReturnQuantityExceeded', errorMessage: "This item can't be returned." });
      } else if (line.quantity > allowed.maxQuantity) {
        problems.push({ propertyName: `Lines[${index}]`, errorCode: 'ReturnQuantityExceeded', errorMessage: `You can return at most ${allowed.maxQuantity} of '${orderLine.productName}'.` });
      } else if (
        orderLine.returnClass === 'Food' &&
        line.condition !== 'Sealed' &&
        line.reason !== 'QualityDefect' &&
        line.reason !== 'Recalled'
      ) {
        problems.push({ propertyName: `Lines[${index}]`, errorCode: 'ReturnNotEligibleOpenedFood', errorMessage: "Opened food can only be returned if there's a quality problem or a recall." });
      } else if (orderLine.returnClass === 'Litter' && line.condition === 'Used') {
        problems.push({ propertyName: `Lines[${index}]`, errorCode: 'ReturnNotEligibleUsedLitter', errorMessage: 'Used litter cannot be returned.' });
      }
    });
    if (problems.length > 0) return violations(problems);
    const created = {
      id: uuid(),
      version: 1,
      orderId: order.id,
      kind: 'CustomerReturn' as const,
      status: 'Requested' as const,
      requestedOn: nowIso(),
      requestedBy: ids.customerId,
      decidedOn: null,
      decidedBy: null,
      decisionNote: null,
      receivedOn: null,
      recalledBatchLines: [],
      lines: body.lines.map((l) => ({
        orderLineId: l.orderLineId,
        quantity: l.quantity,
        reason: l.reason,
        condition: l.condition,
        customerNote: l.customerNote ?? null,
        defectConfirmed: false,
        defectNote: null,
        quantityReceived: 0,
      })),
    };
    db.returns.unshift(created);
    persist();
    return HttpResponse.json({ returnRequestId: created.id, status: created.status }, { status: 201 });
  }),

  http.get(`${API}/customers/me/returns/:returnRequestId`, ({ request, params }) => {
    if (!hasCustomerSession(request)) return unauthorised();
    const ret = findReturn(String(params.returnRequestId));
    const order = ret ? mine(ret.orderId) : undefined;
    if (!ret || !order) return notFound();
    return HttpResponse.json({
      id: ret.id,
      orderId: order.id,
      orderNumber: order.orderNumber,
      kind: ret.kind,
      status: ret.status,
      requestedOn: ret.requestedOn,
      decisionNote: ret.decisionNote,
      lines: ret.lines.map((l) => {
        const orderLine = order.lines.find((x) => x.lineId === l.orderLineId);
        return {
          orderLineId: l.orderLineId,
          productName: orderLine?.productName ?? '',
          variantLabel: orderLine?.variantLabel ?? '',
          quantity: l.quantity,
          reason: l.reason,
          condition: l.condition,
          customerNote: l.customerNote,
          quantityReceived: l.quantityReceived,
        };
      }),
    });
  }),
];

export { hoursFromNow };
