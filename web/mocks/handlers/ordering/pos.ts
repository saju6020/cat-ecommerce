import { http, HttpResponse, type HttpHandler } from 'msw';
import { db as shopDb } from '../shop/store';
import { noContent, notFound, orderingIds as ids, violation } from './fixtures';
import {
  CAT_NAMES,
  allocateOrder,
  authorisedCatsFor,
  batchFor,
  db,
  findOrder,
  formatCents,
  money,
  nextInvoiceNumber,
  nextOrderNumber,
  nowIso,
  persist,
  skuInfo,
  stockOf,
  uuid,
  type DbOrder,
  type DbOrderLine,
} from './store';
import { computeQuote, type QuoteLineInput } from './quote';
import { orderLineView, totalsView } from './customer';

/**
 * `mocks/handlers/ordering/pos.ts` — HO-ORD-002 §12.1. The point of sale as `StaffPosController`
 * really answers it: the flat `PosSku` (with `sellable` and `fefoBatches`), a quote WITHOUT any tender
 * evaluation, a sale that needs `customerName` + `customerMobile` (ADR-216; a refused sale still stores
 * the walk-in), `TendersDoNotCoverTotal` / `TerminalReferenceRequired` from the tender rules, and the
 * `changeDue` only in the sale response.
 */
const API = '*/api/staff/pos';

function posSku(skuId: string) {
  const info = skuInfo(skuId);
  if (!info) return null;
  const sellable = stockOf(skuId);
  const batch = batchFor(skuId);
  return {
    skuId,
    productId: info.productId,
    productName: info.productName,
    variantLabel: info.variantLabel,
    barcode: info.barcode,
    unitPrice: info.priceCents !== null ? money(info.priceCents) : null,
    sellable,
    requiresPrescription: info.requiresPrescription,
    isBundle: info.isBundle,
    fefoBatches:
      sellable > 0 && !info.isBundle
        ? [
            {
              batchId: batch.batchId,
              lotNumber: batch.batchNumber,
              expiresOn: batch.expiresOn,
              locationId: ids.locationCounterId,
              quantity: `${sellable}.000`,
            },
          ]
        : [],
  };
}

const allSkus = () => shopDb.products.flatMap((p) => p.skus.map((s) => ({ product: p, sku: s })));

export const posOrderingHandlers: HttpHandler[] = [
  http.get(`${API}/sku`, ({ request }) => {
    const barcode = new URL(request.url).searchParams.get('barcode')?.trim();
    if (!barcode) return violation('barcode', 'InvalidQuery', 'barcode is required (up to 64 characters).');
    const found = allSkus().find(({ sku }) => sku.barcode === barcode && sku.status === 'Active');
    const view = found ? posSku(found.sku.skuId) : null;
    return view ? HttpResponse.json(view) : notFound();
  }),

  http.get(`${API}/skus`, ({ request }) => {
    const q = new URL(request.url).searchParams.get('q')?.trim().toLowerCase() ?? '';
    if (q.length < 2) return violation('q', 'InvalidQuery', 'q must be 2 to 100 characters.');
    const rows = allSkus()
      .filter(
        ({ product, sku }) =>
          sku.status === 'Active' &&
          product.status === 'Active' &&
          (product.name.toLowerCase().includes(q) ||
            sku.code.toLowerCase().includes(q) ||
            (sku.barcode ?? '').includes(q)),
      )
      .slice(0, 20)
      .map(({ sku }) => posSku(sku.skuId))
      .filter((x) => x !== null);
    return HttpResponse.json(rows);
  }),

  http.post(`${API}/quote`, async ({ request }) => {
    const body = (await request.json()) as {
      customerId?: string | null;
      lines: Array<{ skuId: string; quantity: number; catId?: string | null }>;
      discountCode?: string | null;
    };
    if (!body.lines || body.lines.length === 0 || body.lines.some((l) => l.quantity < 1)) {
      return violation('Lines', 'InvalidQuery', 'lines must have 1 to 50 items, each with a quantity of at least 1.');
    }
    const lines: QuoteLineInput[] = body.lines.map((l) => ({
      lineId: uuid(),
      skuId: l.skuId,
      quantity: l.quantity,
      catId: l.catId ?? null,
      priceWhenAddedCents: skuInfo(l.skuId)?.priceCents ?? 0,
    }));
    const result = computeQuote(lines, {
      fulfilment: 'Counter',
      discountCode: body.discountCode ?? undefined,
      customerHasOrders: !!body.customerId,
    });
    return HttpResponse.json(result.wire);
  }),

  http.post(`${API}/sales`, async ({ request }) => {
    const key = request.headers.get('Idempotency-Key');
    if (!key) return violation('Idempotency-Key', 'IdempotencyKeyRequired', 'An Idempotency-Key header is required.');
    const replay = db.idempotency[key];
    if (replay) return HttpResponse.json(replay.body as object, { status: replay.status });
    const body = (await request.json()) as {
      customerName?: string;
      customerMobile?: string;
      lines: Array<{ skuId: string; quantity: number; catId?: string | null }>;
      discountCode?: string | null;
      tenders: Array<{ method: string; amount: unknown; reference?: string | null }>;
    };
    const name = body.customerName?.trim() ?? '';
    const mobile = body.customerMobile?.trim() ?? '';
    const problems: Array<{ propertyName: string; errorCode: string; errorMessage: string }> = [];
    if (!name) problems.push({ propertyName: 'CustomerName', errorCode: 'NotEmptyValidator', errorMessage: "Enter the customer's name." });
    if (!/^\+?[0-9]{8,15}$/.test(mobile.replace(/[\s-]/g, ''))) {
      problems.push({ propertyName: 'CustomerMobile', errorCode: 'InvalidMobile', errorMessage: "Enter the customer's mobile number." });
    }
    if (problems.length > 0) {
      return HttpResponse.json(
        { errors: problems.map((p) => ({ ...p, resourceName: 'Ordering' })), isValid: false },
        { status: 400 },
      );
    }
    if (mobile === '01700000000') {
      return violation('CustomerMobile', 'AccountNotActive', "This customer's account is closed, so a sale can't be recorded against it.");
    }
    const lines: QuoteLineInput[] = body.lines.map((l) => ({
      lineId: uuid(),
      skuId: l.skuId,
      quantity: l.quantity,
      catId: l.catId ?? null,
      priceWhenAddedCents: skuInfo(l.skuId)?.priceCents ?? 0,
    }));
    const result = computeQuote(lines, {
      fulfilment: 'Counter',
      discountCode: body.discountCode ?? undefined,
      customerHasOrders: true,
    });
    // A prescription line needs an existing customer who owns an authorised cat.
    for (const l of lines) {
      const info = skuInfo(l.skuId);
      if (info?.requiresPrescription) {
        const isKnown = mobile.endsWith(ids.customerMobile.slice(-9));
        if (!l.catId || !isKnown || !authorisedCatsFor(l.skuId).includes(l.catId)) {
          return violation(
            'Lines',
            'PrescriptionCatRequired',
            `'${info.productName}' needs an existing customer and one of their cats with a current vet authorisation.`,
          );
        }
      }
    }
    if (!result.canPlace && result.blocking.some((c) => c !== 'PaymentMethodUnavailable')) {
      return violation('Lines', result.blocking[0] ?? 'NothingToPlace', '');
    }
    if (result.priced.length === 0 || result.priced.length !== lines.length) {
      return violation('Lines', 'StockNoLongerAvailable', 'An item has just sold out. Remove it and try again.');
    }

    // Tenders (TenderCalculator): card at face value, cash up to the balance, change from the surplus.
    const tenders = body.tenders ?? [];
    const refuse = (code: string, message: string) => violation('Tenders', code, message);
    if (tenders.length === 0 || tenders.length > 6) return refuse('TendersDoNotCoverTotal', 'Take at least one payment, each above zero.');
    const cents = tenders.map((t) => Math.round(Number(t.amount) * 100));
    if (cents.some((c) => !(c > 0))) return refuse('TendersDoNotCoverTotal', 'Take at least one payment, each above zero.');
    const cards = tenders.filter((t) => t.method === 'InStoreCard');
    if (cards.some((c) => !c.reference?.trim())) return refuse('TerminalReferenceRequired', 'Enter the card terminal slip reference.');
    const cardTotal = tenders.reduce((sum, t, i) => (t.method === 'InStoreCard' ? sum + (cents[i] ?? 0) : sum), 0);
    const grand = result.totals.grand;
    if (cardTotal > grand) return refuse('TendersDoNotCoverTotal', 'Card payments cannot be more than what is owed.');
    const cashDue = grand - cardTotal;
    const cashHanded = tenders.reduce((sum, t, i) => (t.method === 'Cash' ? sum + (cents[i] ?? 0) : sum), 0);
    if (cashHanded < cashDue) return refuse('TendersDoNotCoverTotal', 'The payments do not cover the total of the sale.');

    const placedOn = nowIso();
    const orderLines: DbOrderLine[] = result.priced.map((p, index) => ({
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
    const known = mobile.endsWith(ids.customerMobile.slice(-9));
    const order: DbOrder = {
      id: uuid(),
      version: 3,
      orderNumber: nextOrderNumber(),
      placedOn,
      channel: 'Counter',
      customerId: known ? ids.customerId : null,
      customerName: name,
      customerMobile: mobile,
      status: 'Delivered',
      fulfilmentMethod: 'Counter',
      paymentMethod: 'InStore',
      cancellationReason: null,
      lines: orderLines,
      subtotalCents: result.totals.subtotal,
      lineDiscountCents: result.totals.lineDiscount,
      orderDiscountCents: result.totals.orderDiscount,
      deliveryChargeCents: 0,
      taxCents: result.totals.tax,
      grandTotalCents: grand,
      fulfilment: {
        recipientName: name,
        recipientPhone: mobile,
        line1: null,
        line2: null,
        city: null,
        district: null,
        postalCode: null,
        deliveryInstructions: null,
        zoneName: null,
        serviceLevel: null,
        slotDate: null,
        slotWindow: null,
        pickupBranchId: null,
        pickupBranchName: null,
        pickupBranchAddress: null,
      },
      history: [
        { from: null, to: 'PendingPayment', changedOn: placedOn, changedBy: db.actor, changedByName: 'Amina Hossain', note: null },
        { from: 'PendingPayment', to: 'Confirmed', changedOn: placedOn, changedBy: db.actor, changedByName: 'Amina Hossain', note: null },
        { from: 'Confirmed', to: 'Delivered', changedOn: placedOn, changedBy: db.actor, changedByName: 'Amina Hossain', note: null },
      ],
      paymentDueBy: null,
      customerTransferReference: null,
      transferReportedOn: null,
      held: null,
      cod: null,
      shipment: null,
      collectBy: null,
      invoiceNumber: nextInvoiceNumber(),
      amountPaidCents: grand,
      amountRefundedCents: 0,
      refundDueCents: 0,
      discountCode: result.discountApplied ? (body.discountCode?.toUpperCase() ?? null) : null,
      returnWindowEndsOn: null,
      courierReportsDelivered: false,
      tenders: tenders.map((t, i) => ({
        method: t.method === 'InStoreCard' ? 'InStoreCard' : 'Cash',
        amountCents: t.method === 'InStoreCard' ? (cents[i] ?? 0) : cashDue,
        reference: t.reference?.trim() ?? null,
      })),
      changeDueCents: cashHanded - cashDue,
      cashier: 'Amina Hossain',
    };
    allocateOrder(order);
    for (const l of order.lines) db.stock[l.skuId] = stockOf(l.skuId) - l.quantity;
    db.orders.unshift(order);
    const response = {
      orderId: order.id,
      orderNumber: order.orderNumber,
      invoiceNumber: order.invoiceNumber,
      grandTotal: money(grand),
      changeDue: money(order.changeDueCents),
    };
    db.idempotency[key] = { status: 201, body: response };
    persist();
    return HttpResponse.json(response, { status: 201 });
  }),

  http.get(`${API}/sales/:orderId/receipt`, ({ params }) => {
    const order = findOrder(String(params.orderId));
    if (!order || order.channel !== 'Counter') return notFound();
    const lines = order.lines.map((l) => {
      const view = orderLineView(l);
      return {
        description: `${l.productName} ${l.variantLabel}`.trim(),
        quantity: l.quantity,
        unitPrice: view.unitPrice,
        lineDiscount: view.lineDiscount,
        taxRatePercent: l.taxRatePercent,
        taxAmount: view.taxAmount,
        lineTotal: view.lineTotal,
      };
    });
    const pad = (left: string, right: string) => `${left}${' '.repeat(Math.max(1, 48 - left.length - right.length))}${right}`;
    const text = [
      'PurrCare Dhanmondi'.padStart(33),
      'House 12, Road 5, Dhanmondi, Dhaka',
      '='.repeat(48),
      pad('Order', order.orderNumber),
      pad('Invoice', order.invoiceNumber ?? ''),
      '-'.repeat(48),
      ...order.lines.map((l) => pad(`${l.quantity} x ${l.productName}`.slice(0, 34), formatCents(l.totalCents))),
      '-'.repeat(48),
      pad('TOTAL', formatCents(order.grandTotalCents)),
      ...order.tenders.map((t) => pad(t.method === 'Cash' ? 'Cash' : 'Card in store', formatCents(t.amountCents))),
      pad('Change', formatCents(order.changeDueCents)),
    ].join('\n');
    return HttpResponse.json({
      orderId: order.id,
      orderNumber: order.orderNumber,
      invoiceNumber: order.invoiceNumber ?? '',
      branchName: 'PurrCare Dhanmondi',
      branchAddress: 'House 12, Road 5, Dhanmondi, Dhaka',
      branchPhone: '+880 1700 000001',
      legalName: 'PurrCare Veterinary Ltd',
      taxRegistrationNumber: 'BIN 000123456-0101',
      soldOn: order.placedOn,
      soldOnLocal: new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short', timeZone: ids.timeZoneId }).format(new Date(order.placedOn)),
      branchTimeZoneId: ids.timeZoneId,
      cashier: order.cashier ?? 'Amina Hossain',
      customerName: order.customerId ? order.customerName : null,
      lines,
      totals: totalsView(order),
      tenders: order.tenders.map((t) => ({ method: t.method, amount: money(t.amountCents) })),
      changeDue: money(order.changeDueCents),
      text,
    });
  }),
];

export { CAT_NAMES, noContent };
