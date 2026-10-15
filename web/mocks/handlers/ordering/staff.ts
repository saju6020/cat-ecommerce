import { http, HttpResponse, type HttpHandler } from 'msw';
import {
  forbidden,
  noContent,
  notFound,
  orderingIds as ids,
  paged,
  pageParams,
  violation,
  violations,
} from './fixtures';
import {
  addDays,
  allocateOrder,
  batchFor,
  db,
  expireOverdueTransfers,
  findOrder,
  findReturn,
  formatCents,
  money,
  nextInvoiceNumber,
  nowIso,
  persist,
  pushStatus,
  skuInfo,
  staffNameOf,
  stockOf,
  todayLocal,
  uuid,
  APPROVAL_THRESHOLD_CENTS,
  type DbOrder,
  type DbReturn,
} from './store';
import { orderLineView, totalsView } from './customer';

/**
 * `mocks/handlers/ordering/staff.ts` — HO-ORD-002 §12.1. The staff endpoints of HO-ORD-001 §12.4 as
 * `StaffOrdersController`, `StaffFulfilmentController`, `StaffReturnsController` and
 * `StaffOrderReconciliationController` really send them (restock / write-off is gone; the held-payment
 * approver may not be the recorder; COD and counter payments never wait for approval — only a
 * bank-transfer confirmation above the threshold does). Role checks are not modelled — a test that
 * needs a `403` overrides the route with `server.use(...)`.
 */

const API = '*/api/staff';

/**
 * How the API binds `from` / `to` (`DateTimeOffset?`): a value with an offset or `Z` is that instant; one
 * without - a bare date, or a date-time with no zone - is read in the server's zone, UTC. (JavaScript's own
 * `Date.parse` would read a zone-less date-time in the machine's zone, which is not what the backend does.)
 */
function parseQueryInstant(text: string): number {
  const hasZone = /(?:Z|[+-]\d{2}:?\d{2})$/i.test(text);
  if (hasZone) return Date.parse(text);
  const withTime = text.includes('T') ? text : `${text}T00:00:00`;
  return Date.parse(`${withTime}Z`);
}

const fulfilmentSummary = (o: DbOrder) => o.fulfilment;

function flagsOf(o: DbOrder): string[] {
  const flags: string[] = [];
  if (o.held) flags.push('AwaitingApproval');
  if (o.customerTransferReference && o.status === 'PendingPayment' && !o.held) flags.push('TransferReported');
  if (o.courierReportsDelivered && o.status === 'Dispatched') flags.push('CourierReportsDelivered');
  if (o.fulfilmentMethod === 'Pickup' && o.status === 'Dispatched' && o.collectBy && o.collectBy < todayLocal()) {
    flags.push('CollectionOverdue');
  }
  return flags;
}

function queueRow(o: DbOrder) {
  return {
    id: o.id,
    orderNumber: o.orderNumber,
    placedOn: o.placedOn,
    customerName: o.customerName,
    status: o.status,
    fulfilmentMethod: o.fulfilmentMethod,
    paymentMethod: o.paymentMethod,
    channel: o.channel,
    grandTotal: money(o.grandTotalCents),
    ageMinutes: Math.max(0, Math.round((Date.now() - Date.parse(o.placedOn)) / 60_000)),
    flags: flagsOf(o),
    branchTimeZoneId: ids.timeZoneId,
  };
}

function allowedActionsOf(o: DbOrder): string[] {
  switch (o.status) {
    case 'PendingPayment':
      return o.paymentMethod === 'BankTransfer' ? ['ConfirmPayment', 'Cancel'] : ['Cancel'];
    case 'Confirmed':
      return o.fulfilmentMethod === 'Counter' ? [] : ['StartPacking', 'Cancel'];
    case 'Packing':
      return [o.fulfilmentMethod === 'Pickup' ? 'MarkReadyForCollection' : 'Dispatch', 'Cancel'];
    case 'Dispatched':
      return ['RecordDelivery', 'RecordDeliveryFailure'];
    case 'Delivered':
    case 'Returned':
      return ['Refund'];
    default:
      return [];
  }
}

function staffDetail(o: DbOrder) {
  return {
    id: o.id,
    version: o.version,
    orderNumber: o.orderNumber,
    placedOn: o.placedOn,
    channel: o.channel,
    customerId: o.customerId,
    status: o.status,
    fulfilmentMethod: o.fulfilmentMethod,
    paymentMethod: o.paymentMethod,
    branchTimeZoneId: ids.timeZoneId,
    lines: o.lines.map((l) => ({
      lineId: l.lineId,
      lineNumber: l.lineNumber,
      skuId: l.skuId,
      productName: l.productName,
      variantLabel: l.variantLabel,
      cat: orderLineView(l).cat,
      quantity: l.quantity,
      unitPrice: money(l.unitCents),
      lineTotal: money(l.totalCents),
      quantityReturned: l.quantityReturned,
      allocations: l.allocations,
    })),
    totals: totalsView(o),
    fulfilment: fulfilmentSummary(o),
    statusHistory: o.history,
    paymentDueBy: o.paymentDueBy,
    customerTransferReference: o.customerTransferReference,
    heldPayment: o.held
      ? { amount: money(o.held.amountCents), recordedBy: o.held.recordedBy, recordedOn: o.held.recordedOn }
      : null,
    codCollection: o.cod
      ? {
          amount: money(o.cod.amountCents),
          collectorKind: o.cod.collectorKind,
          collectorName: o.cod.collectorName,
          collectedOn: o.cod.collectedOn,
          recordedBy: o.cod.recordedBy,
        }
      : null,
    shipment: o.shipment
      ? { ...o.shipment, lastTrackingEventOn: o.shipment.lastTrackingStatus ? o.shipment.dispatchedOn : null }
      : null,
    collectBy: o.collectBy,
    invoiceId: o.invoiceNumber ? `1${o.id.slice(1)}` : null,
    invoiceNumber: o.invoiceNumber,
    payments:
      o.amountPaidCents > 0
        ? [
            {
              paymentId: `9${o.id.slice(1)}`,
              method: o.paymentMethod === 'BankTransfer' ? 'BankTransfer' : o.paymentMethod === 'InStore' ? 'Cash' : 'CashInStore',
              amount: money(o.amountPaidCents),
              recordedOn: o.history[o.history.length - 1]?.changedOn ?? o.placedOn,
            },
          ]
        : [],
    creditNoteIds: db.refunds.filter((r) => r.orderId === o.id).map((r) => `c${r.id.slice(1)}`),
    amountPaid: money(o.amountPaidCents),
    amountRefunded: money(o.amountRefundedCents),
    refundDue: money(o.refundDueCents),
    discountCode: o.discountCode,
    customerAnonymised: false,
    allowedActions: allowedActionsOf(o),
  };
}

function staffId(): string {
  return db.actor;
}

function releaseStock(order: DbOrder): void {
  for (const l of order.lines) db.stock[l.skuId] = stockOf(l.skuId) + l.quantity;
}

function notFoundOrder(orderId: string | readonly string[] | undefined): DbOrder | undefined {
  return findOrder(String(orderId));
}

function bump(order: DbOrder): void {
  order.version += 1;
  persist();
}

function returnRow(r: DbReturn) {
  const order = findOrder(r.orderId);
  return {
    id: r.id,
    orderId: r.orderId,
    orderNumber: order?.orderNumber ?? '',
    customerName: order?.customerName ?? '',
    kind: r.kind,
    status: r.status,
    requestedOn: r.requestedOn,
    lineCount: r.lines.length,
    quantity: r.lines.reduce((sum, l) => sum + l.quantity, 0),
  };
}

function requiresDefect(returnClass: string, condition: string, reason: string): boolean {
  return returnClass === 'Food' && condition !== 'Sealed' && reason === 'QualityDefect';
}

function returnDetail(r: DbReturn) {
  const order = findOrder(r.orderId);
  return {
    id: r.id,
    version: r.version,
    orderId: r.orderId,
    orderNumber: order?.orderNumber ?? '',
    customerName: order?.customerName ?? '',
    kind: r.kind,
    status: r.status,
    requestedOn: r.requestedOn,
    requestedBy: r.requestedBy,
    decidedOn: r.decidedOn,
    decidedBy: r.decidedBy,
    decisionNote: r.decisionNote,
    receivedOn: r.receivedOn,
    branchTimeZoneId: ids.timeZoneId,
    lines: r.lines.map((l) => {
      const orderLine = order?.lines.find((x) => x.lineId === l.orderLineId);
      const batches = orderLine?.allocations ?? [];
      return {
        orderLineId: l.orderLineId,
        productName: orderLine?.productName ?? '',
        variantLabel: orderLine?.variantLabel ?? '',
        quantity: l.quantity,
        reason: l.reason,
        condition: l.condition,
        customerNote: l.customerNote,
        requiresDefectConfirmation: requiresDefect(orderLine?.returnClass ?? 'Other', l.condition, l.reason),
        defectConfirmed: l.defectConfirmed,
        defectNote: l.defectNote,
        quantityReceived: l.quantityReceived,
        anyBatchRecalled: batches.some((b) => db.recalledBatches.includes(b.batchId)),
        batchNumbers: batches.map((b) => b.batchNumber),
      };
    }),
  };
}

function suggestedFor(order: DbOrder) {
  return db.returns
    .filter(
      (r) =>
        r.orderId === order.id &&
        (r.status === 'Received' || r.status === 'Closed') &&
        !db.refunds.some((f) => f.returnRequestId === r.id),
    )
    .map((r) => ({
      returnRequestId: r.id,
      suggestedAmount: money(
        r.lines.reduce((sum, l) => {
          const orderLine = order.lines.find((x) => x.lineId === l.orderLineId);
          return sum + (orderLine ? orderLine.unitCents * l.quantityReceived : 0);
        }, 0),
      ),
    }));
}

function csvCell(value: string | number): string {
  const text = String(value);
  return /^[=+\-@]/.test(text) ? `'${text}` : /[",\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}

export const staffOrderingHandlers: HttpHandler[] = [
  // ---- queue and detail -----------------------------------------------------------------------------
  http.get(`${API}/orders/awaiting-payment`, () => {
    expireOverdueTransfers();
    return HttpResponse.json(
      db.orders
        .filter((o) => o.status === 'PendingPayment' && o.paymentMethod === 'BankTransfer')
        .sort((a, b) => (a.paymentDueBy ?? '').localeCompare(b.paymentDueBy ?? ''))
        .map((o) => ({
          id: o.id,
          orderNumber: o.orderNumber,
          customerName: o.customerName,
          amount: money(o.grandTotalCents),
          paymentDueBy: o.paymentDueBy,
          customerTransferReference: o.customerTransferReference,
          transferReportedOn: o.transferReportedOn,
          heldPayment: o.held
            ? { amount: money(o.held.amountCents), recordedBy: o.held.recordedBy, recordedOn: o.held.recordedOn }
            : null,
          version: o.version,
          branchTimeZoneId: ids.timeZoneId,
        })),
    );
  }),

  http.get(`${API}/orders/refunds-due`, ({ request }) => {
    const url = new URL(request.url);
    const { pageNumber, pageSize } = pageParams(url, 20);
    const rows = db.orders
      .filter((o) => o.refundDueCents > 0 || (o.amountPaidCents > o.amountRefundedCents && suggestedFor(o).length > 0))
      .map((o) => ({
        orderId: o.id,
        orderNumber: o.orderNumber,
        customerName: o.fulfilment.recipientName,
        status: o.status,
        amountPaid: money(o.amountPaidCents),
        amountRefunded: money(o.amountRefundedCents),
        refundDue: money(o.refundDueCents),
        cancelledOn: o.status === 'Cancelled' ? (o.history[o.history.length - 1]?.changedOn ?? null) : null,
        branchTimeZoneId: ids.timeZoneId,
        reasons: [o.refundDueCents > 0 ? 'MoneyOwedBack' : null, suggestedFor(o).length > 0 ? 'ReturnReceived' : null].filter(
          (x): x is string => x !== null,
        ),
      }));
    return HttpResponse.json(paged(rows, pageNumber, pageSize));
  }),

  http.get(`${API}/orders/reconciliation-issues`, ({ request }) => {
    const url = new URL(request.url);
    const status = url.searchParams.get('status');
    const { pageNumber, pageSize } = pageParams(url, 20);
    const rows = db.issues
      .filter((i) => !status || i.status === status)
      .map((i) => ({
        id: i.id,
        orderId: i.orderId,
        kind: i.kind,
        paymentMethod: i.paymentMethod,
        amount: money(i.amountCents),
        paymentReference: i.paymentReference,
        paymentReceivedOn: i.paymentReceivedOn,
        errorSummary: i.errorSummary,
        raisedBy: i.raisedBy,
        raisedOn: i.raisedOn,
        status: i.status,
        resolvedBy: i.resolvedBy,
        resolvedOn: i.resolvedOn,
        resolutionNote: i.resolutionNote,
      }));
    return HttpResponse.json(paged(rows, pageNumber, pageSize));
  }),

  http.post(`${API}/orders/reconciliation-issues/:issueId/resolve`, async ({ request, params }) => {
    const issue = db.issues.find((i) => i.id === params.issueId);
    if (!issue) return notFound();
    const { note } = (await request.json()) as { note?: string };
    if (!note?.trim()) return violation('Note', 'ResolutionNoteRequired', 'Enter a note saying how this was resolved.');
    issue.status = 'Resolved';
    issue.resolvedBy = staffId();
    issue.resolvedOn = nowIso();
    issue.resolutionNote = note.trim();
    persist();
    return noContent();
  }),

  http.get(`${API}/orders`, ({ request }) => {
    expireOverdueTransfers();
    const url = new URL(request.url);
    const q = (key: string) => url.searchParams.get(key) || undefined;
    const search = q('search')?.toLowerCase();
    const from = q('from');
    const to = q('to');
    const { pageNumber, pageSize } = pageParams(url, 25);
    const rows = db.orders
      .filter((o) => !q('status') || o.status === q('status'))
      .filter((o) => !q('fulfilment') || o.fulfilmentMethod === q('fulfilment'))
      .filter((o) => !q('paymentMethod') || o.paymentMethod === q('paymentMethod'))
      .filter((o) => !q('channel') || o.channel === q('channel'))
      .filter((o) => !from || Date.parse(o.placedOn) >= parseQueryInstant(from))
      .filter((o) => !to || Date.parse(o.placedOn) <= parseQueryInstant(to))
      .filter(
        (o) =>
          !search ||
          o.orderNumber.toLowerCase().includes(search) ||
          o.customerName.toLowerCase().includes(search) ||
          (o.customerMobile ?? '').includes(search),
      )
      .sort((a, b) => b.placedOn.localeCompare(a.placedOn))
      .map(queueRow);
    return HttpResponse.json(paged(rows, pageNumber, pageSize));
  }),

  // HO-ORD-001-FU2: the customer's own orders (matched by customer id, never by text), newest first.
  http.get(`${API}/customers/:customerId/orders`, ({ params, request }) => {
    const { pageNumber, pageSize } = pageParams(new URL(request.url), 20);
    // The real endpoint answers 404 when no customer account has that id; the mock knows the customers that have orders.
    if (params.customerId !== ids.customerId && !db.orders.some((o) => o.customerId === params.customerId)) return notFound();
    const rows = db.orders
      .filter((o) => o.customerId === params.customerId)
      .sort((a, b) => b.placedOn.localeCompare(a.placedOn))
      .map((o) => {
        const row: Partial<ReturnType<typeof queueRow>> = queueRow(o);
        delete row.customerName;
        delete row.ageMinutes;
        delete row.flags;
        return row;
      });
    return HttpResponse.json(paged(rows, pageNumber, pageSize));
  }),

  http.get(`${API}/orders/:orderId/packing-slip`, ({ params }) => {
    const order = notFoundOrder(params.orderId);
    if (!order) return notFound();
    return HttpResponse.json({
      orderId: order.id,
      orderNumber: order.orderNumber,
      placedOn: order.placedOn,
      customerName: order.customerName,
      recipientName: order.fulfilment.recipientName,
      recipientPhone: order.fulfilment.recipientPhone,
      fulfilmentMethod: order.fulfilmentMethod,
      addressText: order.fulfilmentMethod === 'Delivery'
        ? [order.fulfilment.line1, order.fulfilment.line2, order.fulfilment.city, order.fulfilment.district, order.fulfilment.postalCode].filter(Boolean).join(', ')
        : null,
      pickupBranchName: order.fulfilment.pickupBranchName,
      slot: order.fulfilment.slotWindow,
      lines: order.lines.map((l) => ({
        lineNumber: l.lineNumber,
        productName: l.productName,
        variantLabel: l.variantLabel,
        quantity: l.quantity,
        catName: orderLineView(l).cat?.name ?? null,
        batchNumbers: l.allocations.map((a) => a.batchNumber),
      })),
      codAmountDue: order.paymentMethod === 'CashOnDelivery' ? money(order.grandTotalCents) : null,
      deliveryInstructions: order.fulfilment.deliveryInstructions,
      branchTimeZoneId: ids.timeZoneId,
    });
  }),

  http.get(`${API}/orders/:orderId`, ({ params }) => {
    expireOverdueTransfers();
    const order = notFoundOrder(params.orderId);
    return order ? HttpResponse.json(staffDetail(order)) : notFound();
  }),

  // ---- payment ---------------------------------------------------------------------------------------
  http.post(`${API}/orders/:orderId/payment/bank-transfer`, async ({ request, params }) => {
    const order = notFoundOrder(params.orderId);
    if (!order) return notFound();
    const key = request.headers.get('Idempotency-Key');
    if (!key) return violation('Idempotency-Key', 'IdempotencyKeyRequired', 'An Idempotency-Key header is required.');
    const replay = db.idempotency[key];
    if (replay) return HttpResponse.json(replay.body as object, { status: replay.status });
    const body = (await request.json()) as { expectedVersion: number; amount: unknown; bankReference?: string; receivedOn?: string };
    if (order.version !== body.expectedVersion) {
      return violation('ExpectedVersion', 'StaleVersion', 'This order changed while you were working.', 409);
    }
    if (order.status !== 'PendingPayment' || order.paymentMethod !== 'BankTransfer') {
      return violation('OrderId', 'OrderNotAwaitingPayment', 'This order is not waiting for a payment.');
    }
    if (order.held) {
      return violation('OrderId', 'PaymentAlreadyAwaitingApproval', 'A payment for this order is already waiting for approval.');
    }
    const cents = Math.round(Number(body.amount) * 100);
    if (cents !== order.grandTotalCents) {
      return violation('Amount', 'TransferAmountMismatch', `The amount must match the order total of ${formatCents(order.grandTotalCents)}.`);
    }
    if (!body.bankReference?.trim()) {
      return violation('BankReference', 'BankReferenceRequired', 'Enter the reference from the bank record.');
    }
    let response: { status: string; awaitingApproval: boolean };
    if (order.grandTotalCents > APPROVAL_THRESHOLD_CENTS) {
      order.held = {
        amountCents: cents,
        bankReference: body.bankReference.trim(),
        recordedBy: staffId(),
        recordedOn: nowIso(),
      };
      order.invoiceNumber ??= nextInvoiceNumber();
      response = { status: 'PendingPayment', awaitingApproval: true };
    } else {
      pushStatus(order, 'Confirmed', 0, staffId());
      order.amountPaidCents = cents;
      allocateOrder(order);
      response = { status: 'Confirmed', awaitingApproval: false };
    }
    bump(order);
    db.idempotency[key] = { status: 200, body: response };
    persist();
    return HttpResponse.json(response);
  }),

  http.post(`${API}/orders/:orderId/payment/approve`, async ({ request, params }) => {
    const order = notFoundOrder(params.orderId);
    if (!order) return notFound();
    const key = request.headers.get('Idempotency-Key');
    if (!key) return violation('Idempotency-Key', 'IdempotencyKeyRequired', 'An Idempotency-Key header is required.');
    const replay = db.idempotency[key];
    if (replay) return HttpResponse.json(replay.body as object, { status: replay.status });
    const body = (await request.json()) as { expectedVersion: number };
    if (order.version !== body.expectedVersion) {
      return violation('ExpectedVersion', 'StaleVersion', 'This order changed while you were working.', 409);
    }
    if (!order.held) return violation('OrderId', 'NoPaymentAwaitingApproval', 'No payment is waiting for approval on this order.');
    if (order.held.recordedBy === staffId()) {
      return violation('OrderId', 'SecondApproverMustDiffer', 'Someone else must approve a payment you recorded.');
    }
    pushStatus(order, 'Confirmed', 0, staffId());
    order.amountPaidCents = order.held.amountCents;
    order.held = null;
    allocateOrder(order);
    bump(order);
    const response = { status: 'Confirmed' };
    db.idempotency[key] = { status: 200, body: response };
    persist();
    return HttpResponse.json(response);
  }),

  http.post(`${API}/orders/:orderId/payment/reject`, async ({ request, params }) => {
    const order = notFoundOrder(params.orderId);
    if (!order) return notFound();
    const { note } = (await request.json()) as { note?: string };
    if (!order.held) return violation('OrderId', 'NoPaymentAwaitingApproval', 'No payment is waiting for approval on this order.');
    if (order.held.recordedBy === staffId()) {
      return violation('OrderId', 'ApproverMustDiffer', 'Someone else must reject a payment you recorded.');
    }
    if (!note?.trim()) return violation('Note', 'NoteRequired', 'Enter a note saying why you are rejecting it.');
    order.held = null;
    bump(order);
    return noContent();
  }),

  http.post(`${API}/orders/:orderId/cancel`, async ({ request, params }) => {
    const order = notFoundOrder(params.orderId);
    if (!order) return notFound();
    const { note } = (await request.json()) as { note?: string };
    if (!note?.trim()) return violation('Note', 'CancellationReasonRequired', 'Enter a reason for cancelling.');
    if (!['PendingPayment', 'Confirmed', 'Packing'].includes(order.status)) {
      return violation('OrderId', 'InvalidOrderTransition', `A ${order.status.toLowerCase()} order cannot be cancelled.`);
    }
    pushStatus(order, 'Cancelled', 0, staffId());
    order.history[order.history.length - 1]!.note = note.trim();
    order.cancellationReason = 'StaffDecision';
    order.held = null;
    if (order.amountPaidCents > 0) order.refundDueCents = order.amountPaidCents - order.amountRefundedCents;
    releaseStock(order);
    bump(order);
    return noContent();
  }),

  // ---- fulfilment -------------------------------------------------------------------------------------
  http.post(`${API}/orders/:orderId/packing`, ({ params }) => {
    const order = notFoundOrder(params.orderId);
    if (!order) return notFound();
    if (order.status !== 'Confirmed') {
      return violation('OrderId', 'InvalidOrderTransition', `A ${order.status.toLowerCase()} order cannot be packed.`);
    }
    pushStatus(order, 'Packing', 0, staffId());
    bump(order);
    return noContent();
  }),

  http.post(`${API}/orders/:orderId/dispatch`, async ({ request, params }) => {
    const order = notFoundOrder(params.orderId);
    if (!order) return notFound();
    const body = (await request.json()) as { courierId: string; trackingNumber?: string | null };
    const courier = db.couriers.find((c) => c.id === body.courierId && c.isActive);
    if (!courier) return violation('CourierId', 'CourierNotFound', 'Choose a courier.');
    if (order.status !== 'Packing') {
      return violation('OrderId', 'InvalidOrderTransition', `A ${order.status.toLowerCase()} order cannot be dispatched.`);
    }
    if (order.lines.some((l) => l.allocations.length === 0)) {
      return violation('OrderId', 'OrderNotFullyAllocated', "This order's items are not all allocated to stock yet.");
    }
    const recalled = order.lines.find((l) => l.allocations.some((a) => db.recalledBatches.includes(a.batchId)));
    if (recalled) {
      const batch = recalled.allocations.find((a) => db.recalledBatches.includes(a.batchId));
      return violation(
        'OrderId',
        'BatchRecalled',
        `Batch ${batch?.batchNumber ?? ''} of '${recalled.productName}' has been recalled and cannot be sent. Move that line to another batch first.`,
      );
    }
    const tracking = body.trackingNumber?.trim() || null;
    order.shipment = {
      courierId: courier.id,
      courierName: courier.name,
      trackingNumber: tracking,
      trackingUrl: tracking && courier.trackingUrlTemplate ? courier.trackingUrlTemplate.replace('{tracking}', tracking) : null,
      dispatchedOn: nowIso(),
      lastTrackingStatus: null,
    };
    pushStatus(order, 'Dispatched', 0, staffId());
    bump(order);
    return noContent();
  }),

  http.post(`${API}/orders/:orderId/ready-for-collection`, ({ params }) => {
    const order = notFoundOrder(params.orderId);
    if (!order) return notFound();
    if (order.status !== 'Packing' || order.fulfilmentMethod !== 'Pickup') {
      return violation('OrderId', 'InvalidOrderTransition', 'This order cannot be marked ready for collection.');
    }
    order.collectBy = addDays(todayLocal(), 7);
    pushStatus(order, 'Dispatched', 0, staffId());
    bump(order);
    return noContent();
  }),

  http.put(`${API}/orders/:orderId/tracking`, async ({ request, params }) => {
    const order = notFoundOrder(params.orderId);
    if (!order) return notFound();
    const { trackingNumber } = (await request.json()) as { trackingNumber?: string };
    if (!order.shipment) return violation('OrderId', 'InvalidOrderTransition', 'This order has not been dispatched.');
    if (!trackingNumber?.trim()) return violation('TrackingNumber', 'TrackingNumberRequired', 'Enter the tracking number.');
    const courier = db.couriers.find((c) => c.id === order.shipment!.courierId);
    order.shipment.trackingNumber = trackingNumber.trim();
    order.shipment.trackingUrl = courier?.trackingUrlTemplate
      ? courier.trackingUrlTemplate.replace('{tracking}', trackingNumber.trim())
      : null;
    bump(order);
    return noContent();
  }),

  http.post(`${API}/orders/:orderId/delivery`, async ({ request, params }) => {
    const order = notFoundOrder(params.orderId);
    if (!order) return notFound();
    const key = request.headers.get('Idempotency-Key');
    if (!key) return violation('Idempotency-Key', 'IdempotencyKeyRequired', 'An Idempotency-Key header is required.');
    if (db.idempotency[key]) return noContent();
    const body = (await request.json()) as {
      expectedVersion: number;
      deliveredOn: string;
      codCollection?: { amount: unknown; collectorKind: string; collectorName?: string; collectorStaffId?: string; collectedOn: string };
      collectionPayment?: { method: string; amount: unknown; reference?: string };
    };
    if (order.version !== body.expectedVersion) {
      return violation('ExpectedVersion', 'StaleVersion', 'This order changed while you were working.', 409);
    }
    if (order.status !== 'Dispatched') {
      return violation('OrderId', 'InvalidOrderTransition', `A ${order.status.toLowerCase()} order cannot be marked delivered.`);
    }
    if (order.paymentMethod === 'CashOnDelivery') {
      if (!body.codCollection) return violation('CodCollection', 'CodCollectionRequired', 'Record the cash collected on delivery.');
      if (body.codCollection.collectorKind === 'Courier' && !body.codCollection.collectorName?.trim()) {
        return violation('CodCollection.CollectorName', 'CodCollectionRequired', 'Enter the rider who collected the cash.');
      }
      const cents = Math.round(Number(body.codCollection.amount) * 100);
      order.cod = {
        amountCents: cents,
        collectorKind: body.codCollection.collectorKind === 'Staff' ? 'Staff' : 'Courier',
        collectorName: body.codCollection.collectorName?.trim() || staffNameOf(staffId()),
        collectedOn: body.codCollection.collectedOn,
        recordedBy: staffId(),
      };
      order.amountPaidCents = cents;
    } else if (order.paymentMethod === 'PayOnCollection') {
      const payment = body.collectionPayment;
      if (!payment) return violation('CollectionPayment', 'CollectionPaymentMismatch', 'Record the payment taken at collection.');
      if (payment.method === 'InStoreCard' && !payment.reference?.trim()) {
        return violation('CollectionPayment.Reference', 'TerminalReferenceRequired', 'Enter the card terminal slip reference.');
      }
      const cents = Math.round(Number(payment.amount) * 100);
      if (cents !== order.grandTotalCents) {
        return violation('CollectionPayment.Amount', 'CollectionPaymentMismatch', `The amount must be the balance due, ${formatCents(order.grandTotalCents)}.`);
      }
      order.amountPaidCents = cents;
    }
    pushStatus(order, 'Delivered', 0, staffId());
    order.returnWindowEndsOn = addDays(todayLocal(), 7);
    order.courierReportsDelivered = false;
    bump(order);
    db.idempotency[key] = { status: 204, body: null };
    persist();
    return noContent();
  }),

  http.post(`${API}/orders/:orderId/delivery-failure`, async ({ request, params }) => {
    const order = notFoundOrder(params.orderId);
    if (!order) return notFound();
    const body = (await request.json()) as { reason: string; note?: string };
    if (order.status !== 'Dispatched') {
      return violation('OrderId', 'InvalidOrderTransition', `A ${order.status.toLowerCase()} order cannot be marked undelivered.`);
    }
    const allowed = order.fulfilmentMethod === 'Pickup' ? ['Uncollected'] : ['Refused', 'Undeliverable'];
    if (!allowed.includes(body.reason)) {
      return violation('Reason', 'DeliveryFailureReasonInvalid', "That reason doesn't apply to this order.");
    }
    pushStatus(order, 'Returned', 0, staffId());
    if (body.note) order.history[order.history.length - 1]!.note = body.note;
    bump(order);
    if (body.reason === 'Uncollected') {
      releaseStock(order);
      persist();
      return noContent();
    }
    const created: DbReturn = {
      id: uuid(),
      version: 1,
      orderId: order.id,
      kind: 'DeliveryFailure',
      status: 'Approved',
      requestedOn: nowIso(),
      requestedBy: staffId(),
      decidedOn: nowIso(),
      decidedBy: staffId(),
      decisionNote: null,
      receivedOn: null,
      recalledBatchLines: [],
      lines: order.lines.map((l) => ({
        orderLineId: l.lineId,
        quantity: l.quantity,
        reason: 'Other',
        condition: 'Sealed',
        customerNote: null,
        defectConfirmed: false,
        defectNote: null,
        quantityReceived: 0,
      })),
    };
    db.returns.unshift(created);
    persist();
    return HttpResponse.json({ returnRequestId: created.id }, { status: 201 });
  }),

  http.post(`${API}/orders/:orderId/lines/:lineId/reallocate`, async ({ request, params }) => {
    const order = notFoundOrder(params.orderId);
    if (!order) return notFound();
    const line = order.lines.find((l) => l.lineId === params.lineId);
    if (!line) return notFound();
    const body = (await request.json()) as { targetBatchId: string; reason?: string };
    if (!['Confirmed', 'Packing'].includes(order.status)) {
      return violation('OrderId', 'InvalidOrderTransition', `A ${order.status.toLowerCase()} order cannot be reallocated.`);
    }
    if (!body.reason?.trim()) {
      return violation('Reason', 'ReallocationReasonRequired', 'Enter a reason for moving this line to another batch.');
    }
    if (db.recalledBatches.includes(body.targetBatchId)) {
      return violation('TargetBatchId', 'BatchRecalled', 'That batch has been recalled and cannot be used.');
    }
    const variantB = batchFor(line.skuId, 'B');
    line.allocations = [
      {
        ...(body.targetBatchId === variantB.batchId ? variantB : batchFor(line.skuId, 'A')),
        batchId: body.targetBatchId,
        quantity: line.quantity,
      },
    ];
    bump(order);
    return noContent();
  }),

  // ---- pick list, couriers, manifest -------------------------------------------------------------------
  http.get(`${API}/fulfilment/pick-list`, ({ request }) => {
    const wanted = new URL(request.url).searchParams.get('orderIds')?.split(',').filter(Boolean);
    const orders = db.orders.filter(
      (o) => (o.status === 'Confirmed' || o.status === 'Packing') && (!wanted || wanted.includes(o.id)),
    );
    const groups = new Map<string, Map<string, { alloc: ReturnType<typeof batchFor>; name: string; label: string; skuId: string; quantity: number; orders: Array<{ orderNumber: string; quantity: number }> }>>();
    for (const order of orders) {
      for (const line of order.lines) {
        for (const a of line.allocations) {
          const location = groups.get(a.locationId) ?? new Map();
          groups.set(a.locationId, location);
          const batch = location.get(a.batchId) ?? { alloc: a, name: line.productName, label: line.variantLabel, skuId: line.skuId, quantity: 0, orders: [] };
          batch.quantity += a.quantity;
          batch.orders.push({ orderNumber: order.orderNumber, quantity: a.quantity });
          location.set(a.batchId, batch);
        }
      }
    }
    return HttpResponse.json({
      generatedOn: nowIso(),
      groups: [...groups.entries()].map(([locationId, batches]) => ({
        locationId,
        locationName: [...batches.values()][0]?.alloc.locationName ?? 'Main shelf',
        batches: [...batches.entries()].map(([batchId, b]) => ({
          batchId,
          batchNumber: b.alloc.batchNumber,
          expiresOn: b.alloc.expiresOn,
          skuId: b.skuId,
          productName: b.name,
          variantLabel: b.label,
          quantity: b.quantity,
          orders: b.orders,
        })),
      })),
    });
  }),

  http.get(`${API}/fulfilment/couriers`, () => HttpResponse.json(db.couriers.filter((c) => c.isActive))),

  http.get(`${API}/fulfilment/manifest.csv`, ({ request }) => {
    const url = new URL(request.url);
    const courierId = url.searchParams.get('courierId');
    const date = url.searchParams.get('date');
    if (!courierId || !date) return violation('date', 'InvalidQuery', 'courierId and date are required.');
    const rows = db.orders.filter(
      (o) =>
        o.shipment?.courierId === courierId &&
        new Intl.DateTimeFormat('en-CA', { timeZone: ids.timeZoneId }).format(new Date(o.shipment.dispatchedOn)) === date,
    );
    const lines = [
      ['Order number', 'Recipient', 'Phone', 'Address', 'District', 'Postal code', 'Parcels', 'Weight (g)', 'COD amount', 'Tracking number'].join(','),
      ...rows.map((o) =>
        [
          o.orderNumber,
          o.fulfilment.recipientName,
          o.fulfilment.recipientPhone ?? '',
          o.fulfilment.line1 ?? '',
          o.fulfilment.district ?? '',
          o.fulfilment.postalCode ?? '',
          1,
          500,
          o.paymentMethod === 'CashOnDelivery' ? money(o.grandTotalCents).amount : '0.00',
          o.shipment?.trackingNumber ?? '',
        ]
          .map(csvCell)
          .join(','),
      ),
    ];
    return new HttpResponse(lines.join('\r\n'), {
      headers: {
        'Content-Type': 'text/csv',
        'Content-Disposition': `attachment; filename="manifest-${date}.csv"`,
      },
    });
  }),

  // ---- returns ---------------------------------------------------------------------------------------------
  http.get(`${API}/returns`, ({ request }) => {
    const url = new URL(request.url);
    const status = url.searchParams.get('status');
    const { pageNumber, pageSize } = pageParams(url, 20);
    const rows = db.returns
      .filter((r) => !status || r.status === status)
      .sort((a, b) => b.requestedOn.localeCompare(a.requestedOn))
      .map(returnRow);
    return HttpResponse.json(paged(rows, pageNumber, pageSize));
  }),

  http.get(`${API}/returns/:returnRequestId`, ({ params }) => {
    const ret = findReturn(String(params.returnRequestId));
    return ret ? HttpResponse.json(returnDetail(ret)) : notFound();
  }),

  http.post(`${API}/orders/:orderId/returns`, async ({ request, params }) => {
    const order = notFoundOrder(params.orderId);
    if (!order) return notFound();
    const body = (await request.json()) as {
      lines: Array<{ orderLineId: string; quantity: number; reason: string; condition: string; customerNote?: string }>;
    };
    const created: DbReturn = {
      id: uuid(),
      version: 1,
      orderId: order.id,
      kind: 'CustomerReturn',
      status: 'Requested',
      requestedOn: nowIso(),
      requestedBy: staffId(),
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

  http.post(`${API}/returns/:returnRequestId/approve`, async ({ request, params }) => {
    const ret = findReturn(String(params.returnRequestId));
    if (!ret) return notFound();
    const body = (await request.json()) as {
      note?: string;
      lines: Array<{ orderLineId: string; defectConfirmed: boolean; defectNote?: string }>;
    };
    if (ret.status !== 'Requested') {
      return violation('ReturnRequestId', 'InvalidReturnTransition', "That isn't possible for a return in its current state.");
    }
    const order = findOrder(ret.orderId);
    const problems: Array<{ propertyName: string; errorCode: string; errorMessage: string }> = [];
    ret.lines.forEach((l, index) => {
      const orderLine = order?.lines.find((x) => x.lineId === l.orderLineId);
      const sent = body.lines.find((x) => x.orderLineId === l.orderLineId);
      if (requiresDefect(orderLine?.returnClass ?? 'Other', l.condition, l.reason) && (!sent?.defectConfirmed || !sent.defectNote?.trim())) {
        problems.push({
          propertyName: `Lines[${index}].DefectNote`,
          errorCode: 'DefectConfirmationRequired',
          errorMessage: `Confirm the defect, with a note, for '${orderLine?.productName ?? 'this item'}'.`,
        });
      }
    });
    if (problems.length > 0) return violations(problems);
    for (const l of ret.lines) {
      const sent = body.lines.find((x) => x.orderLineId === l.orderLineId);
      l.defectConfirmed = sent?.defectConfirmed ?? false;
      l.defectNote = sent?.defectNote ?? null;
    }
    ret.status = 'Approved';
    ret.decidedOn = nowIso();
    ret.decidedBy = staffId();
    ret.decisionNote = body.note ?? null;
    ret.version += 1;
    persist();
    return noContent();
  }),

  http.post(`${API}/returns/:returnRequestId/reject`, async ({ request, params }) => {
    const ret = findReturn(String(params.returnRequestId));
    if (!ret) return notFound();
    const { note } = (await request.json()) as { note?: string };
    if (!note?.trim()) return violation('Note', 'NoteRequired', 'Enter a note saying why you are rejecting it.');
    if (ret.status !== 'Requested') {
      return violation('ReturnRequestId', 'InvalidReturnTransition', "That isn't possible for a return in its current state.");
    }
    ret.status = 'Rejected';
    ret.decidedOn = nowIso();
    ret.decidedBy = staffId();
    ret.decisionNote = note.trim();
    ret.version += 1;
    persist();
    return noContent();
  }),

  http.post(`${API}/returns/:returnRequestId/receive`, async ({ request, params }) => {
    const ret = findReturn(String(params.returnRequestId));
    if (!ret) return notFound();
    const body = (await request.json()) as { lines: Array<{ orderLineId: string; quantityReceived: number }> };
    if (ret.status !== 'Approved') {
      return violation('ReturnRequestId', 'InvalidReturnTransition', "That isn't possible for a return in its current state.");
    }
    const problems: Array<{ propertyName: string; errorCode: string; errorMessage: string }> = [];
    body.lines.forEach((sent, index) => {
      const line = ret.lines.find((l) => l.orderLineId === sent.orderLineId);
      if (!line || sent.quantityReceived > line.quantity || sent.quantityReceived < 0) {
        problems.push({
          propertyName: `Lines[${index}].QuantityReceived`,
          errorCode: 'ReturnReceiptExceedsApproved',
          errorMessage: "You can't receive more than was approved.",
        });
      }
    });
    if (problems.length > 0) return violations(problems);
    const order = findOrder(ret.orderId);
    for (const sent of body.lines) {
      const line = ret.lines.find((l) => l.orderLineId === sent.orderLineId);
      if (!line) continue;
      line.quantityReceived = sent.quantityReceived;
      const orderLine = order?.lines.find((l) => l.lineId === sent.orderLineId);
      if (orderLine) orderLine.quantityReturned += sent.quantityReceived;
    }
    ret.status = 'Received';
    ret.receivedOn = nowIso();
    ret.version += 1;
    persist();
    return noContent();
  }),

  // ---- refunds -------------------------------------------------------------------------------------------------
  http.get(`${API}/orders/:orderId/refund-options`, ({ params }) => {
    const order = notFoundOrder(params.orderId);
    if (!order) return notFound();
    const any = order.amountPaidCents > order.amountRefundedCents;
    return HttpResponse.json({
      orderId: order.id,
      methods: [
        { method: 'CashInStore', allowed: any && order.paymentMethod !== 'BankTransfer', available: true, unavailableReason: null },
        { method: 'BankTransfer', allowed: any, available: true, unavailableReason: null },
        { method: 'StoreCredit', allowed: any, available: false, unavailableReason: 'RefundMethodUnavailable' },
      ],
      maxRefundable: money(Math.max(order.amountPaidCents - order.amountRefundedCents, 0)),
      suggestedForReturns: suggestedFor(order),
    });
  }),

  http.post(`${API}/orders/:orderId/refunds`, async ({ request, params }) => {
    const order = notFoundOrder(params.orderId);
    if (!order) return notFound();
    const key = request.headers.get('Idempotency-Key');
    if (!key) return violation('Idempotency-Key', 'IdempotencyKeyRequired', 'An Idempotency-Key header is required.');
    const replay = db.idempotency[key];
    if (replay) return HttpResponse.json(replay.body as object, { status: replay.status });
    const body = (await request.json()) as {
      returnRequestId?: string;
      method: string;
      amount: unknown;
      reference?: string;
      reason?: string;
    };
    if (body.method === 'StoreCredit') {
      return violation('Method', 'RefundMethodUnavailable', "Store credit isn't available yet.");
    }
    if (body.method === 'CashInStore' && order.paymentMethod === 'BankTransfer') {
      return violation('Method', 'RefundMethodNotAllowed', "That refund method isn't allowed for how this order was paid.");
    }
    if (!body.reference?.trim()) {
      return violation('Reference', 'RefundReferenceRequired', body.method === 'BankTransfer' ? 'Enter the transfer reference.' : 'Enter the receipt number.');
    }
    if (!body.reason?.trim()) return violation('Reason', 'ReasonRequired', 'Enter a reason for the refund.');
    const cents = Math.round(Number(body.amount) * 100);
    const max = Math.max(order.amountPaidCents - order.amountRefundedCents, 0);
    if (cents <= 0 || cents > max) {
      return violation('Amount', 'RefundExceedsAmountPaid', `You can refund at most ${formatCents(max)} on this order.`);
    }
    const refund = {
      id: uuid(),
      orderId: order.id,
      returnRequestId: body.returnRequestId ?? null,
      method: body.method,
      amountCents: cents,
      reference: body.reference.trim(),
      reason: body.reason.trim(),
      issuedOn: nowIso(),
    };
    db.refunds.push(refund);
    order.amountRefundedCents += cents;
    order.refundDueCents = Math.max(order.refundDueCents - cents, 0);
    if (order.amountRefundedCents === order.amountPaidCents && ['Delivered', 'Returned'].includes(order.status)) {
      pushStatus(order, 'Refunded', 0, staffId());
    }
    bump(order);
    const response = { refundId: refund.id, creditNoteId: uuid() };
    db.idempotency[key] = { status: 201, body: response };
    persist();
    return HttpResponse.json(response, { status: 201 });
  }),

  // The reallocation dialog reads the SKU's batches from Inventory; for the shop mock's SKUs
  // (which Inventory's mock does not know) the Ordering mock answers, otherwise it falls through.
  http.get('*/api/staff/inventory/batches', ({ request }) => {
    const skuId = new URL(request.url).searchParams.get('skuId');
    if (!skuId || !skuInfo(skuId)) return undefined;
    const info = skuInfo(skuId)!;
    const rows = (['A', 'B'] as const).map((variant) => {
      const batch = batchFor(skuId, variant);
      return {
        id: batch.batchId,
        skuId,
        skuCode: info.code,
        displayName: `${info.productName} ${info.variantLabel}`,
        lotNumber: batch.batchNumber,
        expiresOn: batch.expiresOn,
        isExpired: false,
        daysToExpiry: 160,
        branchTimeZoneId: ids.timeZoneId,
        status: db.recalledBatches.includes(batch.batchId) ? 'Recalled' : 'Available',
        receivedQuantity: '40.000',
        onHand: '25.000',
        stockUnit: 'unit',
        consignmentId: null,
      };
    });
    return HttpResponse.json(paged(rows, 1, 50));
  }),
];

export { forbidden };
