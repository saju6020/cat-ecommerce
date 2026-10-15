import { http, HttpResponse, type HttpHandler } from 'msw';
import type { SupplierDetail, SupplierSummary } from '@/lib/api/procurement-suppliers';
import type {
  PurchaseOrderDetail,
  PurchaseOrderReceiptState,
  PurchaseOrderRequest,
  PurchaseOrderSummary,
} from '@/lib/api/procurement-orders';
import {
  MOCK_BASE_CURRENCY,
  ROLES,
  branchOf,
  guard,
  inventoryTriggers,
  money,
  noContentResponse,
  notFoundResponse,
  paged,
  q,
  todayDate,
  unitCost,
  violation,
  violations,
  type Violation,
} from './fixtures';
import { db, docNumber, newId, persist, type MPo } from './store';

/** `mocks/handlers/inventory/procurement.ts` — suppliers and purchase orders
 *  (`ProcurementSuppliersController`, `ProcurementPurchaseOrdersController`). */

function supplierSummary(s: SupplierDetail): SupplierSummary {
  return {
    id: s.id,
    code: s.code,
    name: s.name,
    countryCode: s.countryCode,
    currencyCode: s.currencyCode,
    leadTimeDays: s.leadTimeDays,
    paymentTerms: s.paymentTerms,
    isActive: s.isActive,
  };
}

function receiptState(po: MPo): PurchaseOrderReceiptState {
  const accepted = po.lines.reduce((sum, l) => sum + l.acceptedQuantity, 0);
  if (accepted === 0) return 'NotReceived';
  return po.lines.every((l) => l.acceptedQuantity >= l.orderedQuantity)
    ? 'FullyReceived'
    : 'PartiallyReceived';
}

function poTotal(po: MPo): number {
  return po.lines.reduce((sum, l) => sum + l.orderedQuantity * l.unitCost, 0);
}

function poSummary(po: MPo): PurchaseOrderSummary {
  const supplier = db.suppliers.find((s) => s.id === po.supplierId);
  return {
    id: po.id,
    number: po.number,
    supplierId: po.supplierId,
    supplierName: supplier?.name ?? 'Unknown supplier',
    status: po.status,
    receiptState: receiptState(po),
    expectedDeliveryOn: po.expectedDeliveryOn,
    totalAmount: money(poTotal(po), po.currencyCode),
  };
}

function allowedActions(po: MPo): string[] {
  if (po.status === 'Draft') return ['edit', 'issue', 'cancel'];
  if (po.status === 'Issued') {
    return receiptState(po) === 'NotReceived'
      ? ['receive', 'close', 'cancel']
      : ['receive', 'close'];
  }
  return [];
}

export function poDetail(po: MPo): PurchaseOrderDetail {
  const supplier = db.suppliers.find((s) => s.id === po.supplierId);
  return {
    id: po.id,
    number: po.number,
    supplierId: po.supplierId,
    supplierName: supplier?.name ?? 'Unknown supplier',
    branchId: po.branchId,
    deliverToLocationId: po.deliverToLocationId,
    currencyCode: po.currencyCode,
    expectedDeliveryOn: po.expectedDeliveryOn,
    notes: po.notes,
    status: po.status,
    statusReason: po.statusReason,
    receiptState: receiptState(po),
    issuedOn: po.issuedOn,
    totalAmount: money(poTotal(po), po.currencyCode),
    lines: po.lines.map((l) => {
      const sku = db.skus.find((s) => s.skuId === l.skuId)!;
      const outstanding = Math.max(0, l.orderedQuantity - l.acceptedQuantity);
      return {
        lineId: l.lineId,
        skuId: l.skuId,
        skuCode: sku.skuCode,
        displayName: sku.displayName,
        stockUnit: sku.stockUnit,
        orderedQuantity: q(l.orderedQuantity),
        acceptedQuantity: q(l.acceptedQuantity),
        rejectedQuantity: q(l.rejectedQuantity),
        outstandingQuantity: q(outstanding),
        unitCost: unitCost(l.unitCost, po.currencyCode),
        lineTotal: money(l.orderedQuantity * l.unitCost, po.currencyCode),
      };
    }),
    receipts: po.receipts,
    allowedActions: allowedActions(po),
    version: po.version,
  };
}

function validatePo(body: PurchaseOrderRequest): Violation[] {
  const problems: Violation[] = [];
  const supplier = db.suppliers.find((s) => s.id === body.supplierId);
  if (!supplier)
    problems.push({
      propertyName: 'SupplierId',
      errorCode: 'SupplierNotFound',
      errorMessage: 'That supplier does not exist.',
    });
  else if (!supplier.isActive)
    problems.push({
      propertyName: 'SupplierId',
      errorCode: 'SupplierInactive',
      errorMessage: 'That supplier is not active.',
    });
  if (body.expectedDeliveryOn < todayDate()) {
    problems.push({
      propertyName: 'ExpectedDeliveryOn',
      errorCode: 'ExpectedDeliveryInPast',
      errorMessage: 'The expected delivery date cannot be in the past.',
    });
  }
  if (body.lines.length === 0) {
    problems.push({
      propertyName: 'Lines',
      errorCode: 'PurchaseOrderLinesRequired',
      errorMessage: 'A purchase order needs at least one line.',
    });
  }
  const seen = new Set<string>();
  body.lines.forEach((line, index) => {
    const sku = db.skus.find((s) => s.skuId === line.skuId);
    if (!sku) {
      problems.push({
        propertyName: `Lines[${index}].SkuId`,
        errorCode: 'SkuNotFound',
        errorMessage: 'That SKU does not exist.',
      });
      return;
    }
    if (seen.has(line.skuId)) {
      problems.push({
        propertyName: `Lines[${index}].SkuId`,
        errorCode: 'DuplicatePurchaseOrderSku',
        errorMessage: 'A SKU can appear on a purchase order only once.',
      });
    }
    seen.add(line.skuId);
    if (!sku.allowsFractionalQuantity && line.orderedQuantity.includes('.')) {
      problems.push({
        propertyName: `Lines[${index}].OrderedQuantity`,
        errorCode: 'FractionalQuantityNotAllowed',
        errorMessage: `${sku.displayName} is counted in whole ${sku.stockUnit} — no decimals.`,
      });
    }
  });
  return problems;
}

function applyBody(po: MPo, body: PurchaseOrderRequest): void {
  const supplier = db.suppliers.find((s) => s.id === body.supplierId);
  po.supplierId = body.supplierId;
  po.deliverToLocationId = body.deliverToLocationId;
  po.currencyCode = body.currencyCode ?? supplier?.currencyCode ?? MOCK_BASE_CURRENCY;
  po.expectedDeliveryOn = body.expectedDeliveryOn;
  po.notes = body.notes;
  po.lines = body.lines.map((l) => ({
    lineId: newId('d2'),
    skuId: l.skuId,
    orderedQuantity: parseFloat(l.orderedQuantity),
    acceptedQuantity: 0,
    rejectedQuantity: 0,
    unitCost: parseFloat(l.unitCostAmount),
  }));
}

function findPo(request: Request, id: unknown): MPo | undefined {
  return db.purchaseOrders.find((po) => po.id === id && po.branchId === branchOf(request));
}

export const procurementHandlers: HttpHandler[] = [
  // ---- suppliers ------------------------------------------------------------------------------
  http.get('*/api/staff/procurement/suppliers', ({ request }) => {
    const denied = guard(request, ROLES.procurementView);
    if (denied) return denied;
    const url = new URL(request.url);
    const search = (url.searchParams.get('search') ?? '').toLowerCase();
    const country = url.searchParams.get('countryCode');
    const active = url.searchParams.get('isActive');
    const rows = db.suppliers
      .filter(
        (s) =>
          !search || s.name.toLowerCase().includes(search) || s.code.toLowerCase().includes(search),
      )
      .filter((s) => !country || s.countryCode === country)
      .filter((s) => active === null || active === '' || String(s.isActive) === active)
      .sort((a, b) => a.name.localeCompare(b.name))
      .map(supplierSummary);
    return HttpResponse.json(paged(rows, request));
  }),

  http.post('*/api/staff/procurement/suppliers', async ({ request }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const body = (await request.json()) as SupplierDetail;
    if (
      body.code.toUpperCase() === inventoryTriggers.supplierCodeTaken ||
      db.suppliers.some((s) => s.code.toLowerCase() === body.code.toLowerCase())
    ) {
      return violation(
        'Code',
        'SupplierCodeAlreadyExists',
        'This supplier code is already in use.',
      );
    }
    const supplierId = newId('a1');
    db.suppliers.push({ ...body, id: supplierId, isActive: true, version: 1 });
    persist();
    return HttpResponse.json({ supplierId }, { status: 201 });
  }),

  http.get('*/api/staff/procurement/suppliers/:id', ({ request, params }) => {
    const denied = guard(request, ROLES.procurementView);
    if (denied) return denied;
    const supplier = db.suppliers.find((s) => s.id === params.id);
    return supplier ? HttpResponse.json(supplier) : notFoundResponse();
  }),

  http.put('*/api/staff/procurement/suppliers/:id', async ({ request, params }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const supplier = db.suppliers.find((s) => s.id === params.id);
    if (!supplier) return notFoundResponse();
    const body = (await request.json()) as SupplierDetail;
    Object.assign(supplier, {
      ...body,
      id: supplier.id,
      code: supplier.code,
      isActive: supplier.isActive,
      version: supplier.version + 1,
    });
    persist();
    return noContentResponse();
  }),

  http.put('*/api/staff/procurement/suppliers/:id/activation', async ({ request, params }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const supplier = db.suppliers.find((s) => s.id === params.id);
    if (!supplier) return notFoundResponse();
    supplier.isActive = ((await request.json()) as { isActive: boolean }).isActive;
    persist();
    return noContentResponse();
  }),

  // ---- purchase orders ------------------------------------------------------------------------
  http.get('*/api/staff/procurement/purchase-orders', ({ request }) => {
    const denied = guard(request, ROLES.procurementView);
    if (denied) return denied;
    const url = new URL(request.url);
    const status = url.searchParams.get('status');
    const supplierId = url.searchParams.get('supplierId');
    const state = url.searchParams.get('receiptState');
    const from = url.searchParams.get('from');
    const to = url.searchParams.get('to');
    const rows = db.purchaseOrders
      .filter((po) => po.branchId === branchOf(request))
      .map(poSummary)
      .filter((po) => !status || po.status === status)
      .filter((po) => !supplierId || po.supplierId === supplierId)
      .filter((po) => !state || po.receiptState === state)
      .filter((po) => !from || po.expectedDeliveryOn >= from)
      .filter((po) => !to || po.expectedDeliveryOn <= to)
      .sort((a, b) => b.number.localeCompare(a.number));
    return HttpResponse.json(paged(rows, request));
  }),

  http.post('*/api/staff/procurement/purchase-orders', async ({ request }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const body = (await request.json()) as PurchaseOrderRequest;
    const problems = validatePo(body);
    if (problems.length > 0) return violations(problems);
    const po: MPo = {
      id: newId('d1'),
      number: docNumber('PO'),
      branchId: branchOf(request),
      supplierId: body.supplierId,
      deliverToLocationId: body.deliverToLocationId,
      currencyCode: MOCK_BASE_CURRENCY,
      expectedDeliveryOn: body.expectedDeliveryOn,
      notes: null,
      status: 'Draft',
      statusReason: null,
      issuedOn: null,
      version: 1,
      lines: [],
      receipts: [],
    };
    applyBody(po, body);
    db.purchaseOrders.push(po);
    persist();
    return HttpResponse.json({ purchaseOrderId: po.id, number: po.number }, { status: 201 });
  }),

  http.get('*/api/staff/procurement/purchase-orders/:id', ({ request, params }) => {
    const denied = guard(request, ROLES.procurementView);
    if (denied) return denied;
    const po = findPo(request, params.id);
    return po ? HttpResponse.json(poDetail(po)) : notFoundResponse();
  }),

  http.put('*/api/staff/procurement/purchase-orders/:id', async ({ request, params }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const po = findPo(request, params.id);
    if (!po) return notFoundResponse();
    if (po.status !== 'Draft') {
      return violation(
        'PurchaseOrderId',
        'InvalidPurchaseOrderTransition',
        'Only a draft purchase order can be edited.',
      );
    }
    const body = (await request.json()) as PurchaseOrderRequest;
    const problems = validatePo(body);
    if (problems.length > 0) return violations(problems);
    applyBody(po, body);
    po.version += 1;
    persist();
    return noContentResponse();
  }),

  http.post('*/api/staff/procurement/purchase-orders/:id/issue', ({ request, params }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const po = findPo(request, params.id);
    if (!po) return notFoundResponse();
    if (po.status !== 'Draft') {
      return violation(
        'PurchaseOrderId',
        'InvalidPurchaseOrderTransition',
        'Only a draft purchase order can be issued.',
      );
    }
    po.status = 'Issued';
    po.issuedOn = new Date().toISOString();
    po.version += 1;
    persist();
    return noContentResponse();
  }),

  http.post('*/api/staff/procurement/purchase-orders/:id/cancel', async ({ request, params }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const po = findPo(request, params.id);
    if (!po) return notFoundResponse();
    const { reason } = (await request.json()) as { reason: string };
    if (po.receipts.length > 0) {
      return violation(
        'PurchaseOrderId',
        'PurchaseOrderHasReceipts',
        'This order already has receipts, so it cannot be cancelled. Close it short instead.',
      );
    }
    if (po.status !== 'Draft' && po.status !== 'Issued') {
      return violation(
        'PurchaseOrderId',
        'InvalidPurchaseOrderTransition',
        'This purchase order can no longer be cancelled.',
      );
    }
    po.status = 'Cancelled';
    po.statusReason = reason;
    po.version += 1;
    persist();
    return noContentResponse();
  }),

  http.post('*/api/staff/procurement/purchase-orders/:id/close', async ({ request, params }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const po = findPo(request, params.id);
    if (!po) return notFoundResponse();
    const { reason } = (await request.json()) as { reason: string };
    if (!reason?.trim())
      return violation(
        'Reason',
        'CloseReasonRequired',
        'Give a reason for closing the order short.',
      );
    if (po.status !== 'Issued') {
      return violation(
        'PurchaseOrderId',
        'InvalidPurchaseOrderTransition',
        'Only an issued purchase order can be closed short.',
      );
    }
    po.status = 'Closed';
    po.statusReason = reason;
    po.version += 1;
    persist();
    return noContentResponse();
  }),
];
