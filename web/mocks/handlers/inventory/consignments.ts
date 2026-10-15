import { http, HttpResponse, type HttpHandler } from 'msw';
import type {
  ConsignmentBatch,
  ConsignmentDetail,
  ConsignmentDetailsInput,
  ConsignmentDocumentInput,
  ConsignmentStatus,
  ConsignmentSummary,
  InspectionPack,
} from '@/lib/api/procurement-consignments';
import {
  MOCK_BASE_CURRENCY,
  ROLES,
  branchOf,
  guard,
  money,
  nowIso,
  omit,
  noContentResponse,
  notFoundResponse,
  paged,
  q,
  violation,
  violations,
} from './fixtures';
import { ACTOR_ID, db, docNumber, newId, persist } from './store';

/** `mocks/handlers/inventory/consignments.ts` — `ProcurementConsignmentsController`. */

type MConsignment = (typeof db.consignments)[number];

const NEXT: Partial<Record<ConsignmentStatus, ConsignmentStatus>> = {
  Planned: 'InTransit',
  InTransit: 'AtCustoms',
  AtCustoms: 'Cleared',
};
const NEXT_ACTION: Partial<Record<ConsignmentStatus, string>> = {
  Planned: 'in-transit',
  InTransit: 'at-customs',
  AtCustoms: 'cleared',
};

function allowedActionsFor(status: ConsignmentStatus): string[] {
  if (status === 'Cancelled') return [];
  if (status === 'Cleared') return ['receive', 'documents', 'costs'];
  return ['update', `advance:${NEXT_ACTION[status]}`, 'cancel', 'documents', 'costs'];
}

function find(request: Request, id: unknown): MConsignment | undefined {
  return db.consignments.find((c) => c.id === id && c.branchId === branchOf(request));
}

function toDetail(c: MConsignment): ConsignmentDetail {
  return { ...omit(c, '_allocationBatchIds'), allowedActions: allowedActionsFor(c.status) };
}

function summary(c: MConsignment): ConsignmentSummary {
  return {
    id: c.id,
    reference: c.reference,
    status: c.status,
    originCountryCode: c.originCountryCode,
    shipmentReference: c.shipmentReference,
    expectedArrivalOn: c.expectedArrivalOn,
    purchaseOrderCount: c.purchaseOrders.length,
  };
}

function receivedBatches(c: MConsignment): ConsignmentBatch[] {
  return db.batches
    .filter((b) => b.consignmentId === c.id)
    .map((b) => {
      const sku = db.skus.find((s) => s.skuId === b.skuId)!;
      return {
        batchId: b.id,
        skuId: b.skuId,
        skuCode: sku.skuCode,
        displayName: sku.displayName,
        lotNumber: b.lotNumber,
        expiresOn: b.expiresOn,
        receivedQuantity: q(b.receivedQuantity),
        stockUnit: sku.stockUnit,
        status: b.status,
        goodsReceiptNumber: b.goodsReceiptNumber,
      };
    });
}

function applyDetails(c: MConsignment, body: ConsignmentDetailsInput): void {
  c.originCountryCode = body.originCountryCode;
  c.shipmentReference = body.shipmentReference;
  c.importPermitNumber = body.importPermitNumber;
  c.healthCertificateNumber = body.healthCertificateNumber;
  c.customsDeclarationNumber = body.customsDeclarationNumber;
  c.expectedArrivalOn = body.expectedArrivalOn;
  c.hsCodes = body.hsCodes;
  c.purchaseOrders = body.purchaseOrderIds
    .map((id) => db.purchaseOrders.find((po) => po.id === id))
    .filter((po): po is NonNullable<typeof po> => !!po)
    .map((po) => {
      const supplier = db.suppliers.find((s) => s.id === po.supplierId);
      return {
        id: po.id,
        number: po.number,
        supplierId: po.supplierId,
        supplierName: supplier?.name ?? 'Unknown supplier',
        status: po.status,
      };
    });
}

function validateDetails(body: ConsignmentDetailsInput, branch: string) {
  const problems = [];
  if (body.purchaseOrderIds.length === 0) {
    problems.push({
      propertyName: 'PurchaseOrderIds',
      errorCode: 'InvalidConsignment',
      errorMessage: 'Choose at least one purchase order.',
    });
  }
  for (const id of body.purchaseOrderIds) {
    const po = db.purchaseOrders.find((p) => p.id === id && p.branchId === branch);
    if (!po || po.status !== 'Issued') {
      problems.push({
        propertyName: 'PurchaseOrderIds',
        errorCode: 'ConsignmentPurchaseOrderMismatch',
        errorMessage: 'Every purchase order must be an issued order at this branch.',
      });
      break;
    }
  }
  body.hsCodes.forEach((h, index) => {
    if (!/^\d{4}(\.?\d{2}){0,3}$/.test(h.code)) {
      problems.push({
        propertyName: `HsCodes[${index}].Code`,
        errorCode: 'InvalidHsCode',
        errorMessage:
          'An HS code is four digits followed by up to three groups of two (for example 2309.10).',
      });
    }
  });
  return problems;
}

function conflict() {
  return violation(
    'ExpectedVersion',
    'ConsignmentVersionConflict',
    'Someone else changed this consignment. Reload to see the latest.',
    409,
  );
}

function validateDocument(body: ConsignmentDocumentInput) {
  const problems = [];
  if (!['application/pdf', 'image/jpeg', 'image/png'].includes(body.contentType)) {
    problems.push({
      propertyName: 'ContentType',
      errorCode: 'InvalidDocumentContentType',
      errorMessage: 'A document must be a PDF, JPEG or PNG.',
    });
  }
  if (body.sizeInBytes > 20 * 1024 * 1024) {
    problems.push({
      propertyName: 'SizeInBytes',
      errorCode: 'DocumentTooLarge',
      errorMessage: 'A document can be at most 20 MB.',
    });
  }
  return problems;
}

function baseCostTotal(c: MConsignment): number | null {
  let total = 0;
  for (const cost of c.costComponents) {
    const amount = parseFloat(cost.amount.amount);
    if (cost.amount.currencyCode === MOCK_BASE_CURRENCY) {
      total += amount;
      continue;
    }
    const rate = c.exchangeRates.find((r) => r.currencyCode === cost.amount.currencyCode);
    if (!rate) return null;
    total += amount * parseFloat(rate.rateToBase);
  }
  return total;
}

export const consignmentHandlers: HttpHandler[] = [
  http.get('*/api/staff/procurement/consignments', ({ request }) => {
    const denied = guard(request, ROLES.procurementView);
    if (denied) return denied;
    const url = new URL(request.url);
    const status = url.searchParams.get('status');
    const country = url.searchParams.get('originCountryCode');
    const from = url.searchParams.get('from');
    const to = url.searchParams.get('to');
    const rows = db.consignments
      .filter((c) => c.branchId === branchOf(request))
      .filter((c) => !status || c.status === status)
      .filter((c) => !country || c.originCountryCode === country)
      .filter((c) => !from || (c.expectedArrivalOn ?? '') >= from)
      .filter((c) => !to || (c.expectedArrivalOn ?? '9999') <= to)
      .sort((a, b) => b.reference.localeCompare(a.reference))
      .map(summary);
    return HttpResponse.json(paged(rows, request));
  }),

  http.post('*/api/staff/procurement/consignments', async ({ request }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const body = (await request.json()) as ConsignmentDetailsInput;
    const problems = validateDetails(body, branchOf(request));
    if (problems.length > 0) return violations(problems);
    const consignment: MConsignment = {
      id: newId('51'),
      reference: docNumber('CN'),
      branchId: branchOf(request),
      originCountryCode: body.originCountryCode,
      shipmentReference: body.shipmentReference,
      importPermitNumber: null,
      healthCertificateNumber: null,
      customsDeclarationNumber: null,
      expectedArrivalOn: null,
      status: 'Planned',
      cancellationReason: null,
      clearedOn: null,
      version: 1,
      hsCodes: [],
      purchaseOrders: [],
      costComponents: [],
      exchangeRates: [],
      documents: [],
      landedCost: { allocatedOn: null, isStale: false, totalCostsBase: null, lines: [] },
      statusHistory: [],
      allowedActions: [],
      _allocationBatchIds: [],
    };
    applyDetails(consignment, body);
    db.consignments.push(consignment);
    persist();
    return HttpResponse.json(
      { consignmentId: consignment.id, reference: consignment.reference },
      { status: 201 },
    );
  }),

  http.get('*/api/staff/procurement/consignments/:id/inspection-pack', ({ request, params }) => {
    const denied = guard(request, ROLES.procurementView);
    if (denied) return denied;
    const c = find(request, params.id);
    if (!c) return notFoundResponse();
    const pack: InspectionPack = {
      consignmentId: c.id,
      reference: c.reference,
      status: c.status,
      originCountryCode: c.originCountryCode,
      shipmentReference: c.shipmentReference,
      importPermitNumber: c.importPermitNumber,
      healthCertificateNumber: c.healthCertificateNumber,
      customsDeclarationNumber: c.customsDeclarationNumber,
      expectedArrivalOn: c.expectedArrivalOn,
      clearedOn: c.clearedOn,
      hsCodes: c.hsCodes,
      purchaseOrders: c.purchaseOrders,
      documents: c.documents,
      batches: receivedBatches(c),
    };
    return HttpResponse.json(pack);
  }),

  http.get('*/api/staff/procurement/consignments/:id', ({ request, params }) => {
    const denied = guard(request, ROLES.procurementView);
    if (denied) return denied;
    const c = find(request, params.id);
    return c ? HttpResponse.json(toDetail(c)) : notFoundResponse();
  }),

  http.put('*/api/staff/procurement/consignments/:id', async ({ request, params }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const c = find(request, params.id);
    if (!c) return notFoundResponse();
    const body = (await request.json()) as ConsignmentDetailsInput & { expectedVersion: number };
    if (body.expectedVersion !== c.version) return conflict();
    if (c.status === 'Cancelled' || c.status === 'Cleared') {
      return violation(
        'ConsignmentId',
        'InvalidConsignmentTransition',
        'A cleared or cancelled consignment cannot be edited.',
      );
    }
    const problems = validateDetails(body, branchOf(request));
    if (problems.length > 0) return violations(problems);
    applyDetails(c, body);
    c.version += 1;
    persist();
    return noContentResponse();
  }),

  http.post('*/api/staff/procurement/consignments/:id/status', async ({ request, params }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const c = find(request, params.id);
    if (!c) return notFoundResponse();
    const { to, on } = (await request.json()) as { to: ConsignmentStatus; on: string };
    if (NEXT[c.status] !== to) {
      return violation(
        'Status',
        'InvalidConsignmentTransition',
        `A consignment cannot move from ${c.status} to ${to}.`,
      );
    }
    if (
      to === 'Cleared' &&
      !(c.importPermitNumber && c.healthCertificateNumber && c.customsDeclarationNumber)
    ) {
      return violation(
        'ImportPermitNumber',
        'ClearanceDocumentsMissing',
        'The import permit, health certificate and customs declaration numbers are required to clear a consignment.',
      );
    }
    c.statusHistory.push({
      from: c.status,
      to,
      changedOn: nowIso(),
      changedBy: ACTOR_ID,
      effectiveOn: on,
    });
    c.status = to;
    if (to === 'Cleared') c.clearedOn = on;
    c.version += 1;
    persist();
    return noContentResponse();
  }),

  http.post('*/api/staff/procurement/consignments/:id/cancel', async ({ request, params }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const c = find(request, params.id);
    if (!c) return notFoundResponse();
    if (c.status === 'Cleared' || c.status === 'Cancelled') {
      return violation(
        'Status',
        'InvalidConsignmentTransition',
        'This consignment can no longer be cancelled.',
      );
    }
    c.cancellationReason = ((await request.json()) as { reason: string }).reason;
    c.status = 'Cancelled';
    c.version += 1;
    persist();
    return noContentResponse();
  }),

  http.post('*/api/staff/procurement/consignments/:id/documents', async ({ request, params }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const c = find(request, params.id);
    if (!c) return notFoundResponse();
    const body = (await request.json()) as ConsignmentDocumentInput;
    const problems = validateDocument(body);
    if (problems.length > 0) return violations(problems);
    const documentId = newId('d3');
    c.documents.push({
      documentId,
      kind: body.kind,
      documentNumber: body.documentNumber,
      issuedOn: body.issuedOn,
      fileName: body.fileName,
      storageKey: body.storageKey,
      contentType: body.contentType,
      sizeInBytes: body.sizeInBytes,
      uploadedBy: ACTOR_ID,
      uploadedOn: nowIso(),
      isSuperseded: false,
      supersededOn: null,
      supersededBy: null,
    });
    persist();
    return HttpResponse.json({ documentId }, { status: 201 });
  }),

  http.post(
    '*/api/staff/procurement/consignments/:id/documents/:documentId/supersede',
    async ({ request, params }) => {
      const denied = guard(request, ROLES.stockManage);
      if (denied) return denied;
      const c = find(request, params.id);
      const old = c?.documents.find((d) => d.documentId === params.documentId);
      if (!c || !old) return notFoundResponse();
      const body = (await request.json()) as ConsignmentDocumentInput;
      const problems = validateDocument(body);
      if (problems.length > 0) return violations(problems);
      old.isSuperseded = true;
      old.supersededOn = nowIso();
      old.supersededBy = ACTOR_ID;
      c.documents.push({
        documentId: newId('d3'),
        kind: body.kind,
        documentNumber: body.documentNumber,
        issuedOn: body.issuedOn,
        fileName: body.fileName,
        storageKey: body.storageKey,
        contentType: body.contentType,
        sizeInBytes: body.sizeInBytes,
        uploadedBy: ACTOR_ID,
        uploadedOn: nowIso(),
        isSuperseded: false,
        supersededOn: null,
        supersededBy: null,
      });
      persist();
      return noContentResponse();
    },
  ),

  http.put('*/api/staff/procurement/consignments/:id/costs', async ({ request, params }) => {
    const denied = guard(request, ROLES.costs);
    if (denied) return denied;
    const c = find(request, params.id);
    if (!c) return notFoundResponse();
    const body = (await request.json()) as {
      expectedVersion: number;
      costComponents: Array<{
        kind: ConsignmentDetail['costComponents'][number]['kind'];
        amount: string;
        currencyCode: string;
        note: string | null;
      }>;
      exchangeRates: Array<{ currencyCode: string; rateToBase: string; recordedOn: string }>;
    };
    if (body.expectedVersion !== c.version) return conflict();
    c.costComponents = body.costComponents.map((k) => ({
      kind: k.kind,
      amount: { amount: parseFloat(k.amount).toFixed(2), currencyCode: k.currencyCode },
      note: k.note,
    }));
    c.exchangeRates = body.exchangeRates.map((r) => ({
      currencyCode: r.currencyCode,
      rateToBase: r.rateToBase,
      recordedOn: r.recordedOn,
    }));
    if (c.landedCost.allocatedOn) c.landedCost.isStale = true;
    c.version += 1;
    persist();
    return noContentResponse();
  }),

  http.post('*/api/staff/procurement/consignments/:id/landed-cost', async ({ request, params }) => {
    const denied = guard(request, ROLES.costs);
    if (denied) return denied;
    const c = find(request, params.id);
    if (!c) return notFoundResponse();
    const { expectedVersion } = (await request.json()) as { expectedVersion: number };
    if (expectedVersion !== c.version) return conflict();
    const received = db.batches.filter((b) => b.consignmentId === c.id);
    const total = baseCostTotal(c);
    if (received.length === 0) {
      return violation(
        'ConsignmentId',
        'NoReceivedQuantityToAllocate',
        'Nothing has been received on this consignment yet.',
      );
    }
    if (total === null) {
      return violation(
        'ExchangeRates',
        'ExchangeRateMissing',
        'Add an exchange rate for every currency used in the costs.',
      );
    }
    const quantity = received.reduce((sum, b) => sum + b.receivedQuantity, 0);
    const lines = received.map((b) => {
      const sku = db.skus.find((s) => s.skuId === b.skuId)!;
      const purchase = b.receivedQuantity * b.purchaseUnitCost;
      const allocated = (total * b.receivedQuantity) / quantity;
      b.unitCostBase = (purchase + allocated) / b.receivedQuantity;
      b.costBasis = 'landed';
      return {
        batchId: b.id,
        skuCode: sku.skuCode,
        lotNumber: b.lotNumber,
        quantity: q(b.receivedQuantity),
        purchaseCostBase: money(purchase),
        allocatedCostBase: money(allocated),
        landedUnitCostBase: { amount: b.unitCostBase.toFixed(4), currencyCode: MOCK_BASE_CURRENCY },
      };
    });
    const allocatedOn = nowIso();
    c.landedCost = { allocatedOn, isStale: false, totalCostsBase: money(total), lines };
    c.version += 1;
    persist();
    return HttpResponse.json({
      lines: lines.map((l) => ({
        batchId: l.batchId,
        quantity: l.quantity,
        purchaseCostBase: l.purchaseCostBase,
        allocatedCostBase: l.allocatedCostBase,
        landedUnitCostBase: l.landedUnitCostBase,
      })),
      totalCostsBase: money(total),
      allocatedOn,
    });
  }),
];
