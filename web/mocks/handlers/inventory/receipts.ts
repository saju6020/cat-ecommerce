import { http, HttpResponse, type HttpHandler } from 'msw';
import type {
  GoodsReceiptDetail,
  GoodsReceiptSummary,
  PostGoodsReceiptRequest,
} from '@/lib/api/inventory-receipts';
import {
  MOCK_BASE_CURRENCY,
  ROLES,
  branchOf,
  guard,
  isAllowed,
  nowIso,
  omit,
  notFoundResponse,
  paged,
  q,
  todayDate,
  unitCost,
  violations,
  type Violation,
} from './fixtures';
import { ACTOR_ID, db, docNumber, newId, persist, type MBatch } from './store';
import { locationOf, moveStock, skuOf } from './stock';

/** `mocks/handlers/inventory/receipts.ts` — `StaffGoodsReceiptsController`. The validation mirrors
 *  `GoodsReceiptAggregate.Validate`: EVERY line violation is returned, named `Lines[i].Field`. */

type MReceiptRecord = (typeof db.receipts)[number];

function summary(r: MReceiptRecord): GoodsReceiptSummary {
  return {
    id: r.id,
    number: r.number,
    receivedOn: r.receivedOn,
    supplierId: r.supplierId,
    supplierName: r.supplierName,
    purchaseOrderId: r.purchaseOrderId,
    purchaseOrderNumber: r.purchaseOrderNumber,
    consignmentId: r.consignmentId,
    deliveryReference: r.deliveryReference,
    lineCount: r.lines.length,
    batchCount: r.lines.filter((l) => l.batchId).length,
    receivedBy: r.receivedBy,
  };
}

function detail(r: MReceiptRecord, includeCosts: boolean): GoodsReceiptDetail {
  return {
    ...omit(r, '_poLines', '_branchId'),
    lines: r.lines.map((l) => ({ ...l, unitCost: includeCosts ? l.unitCost : null })),
  };
}

function validate(body: PostGoodsReceiptRequest, branchId: string): Violation[] {
  const problems: Violation[] = [];
  const add = (propertyName: string, errorCode: string, errorMessage: string) =>
    problems.push({ propertyName, errorCode, errorMessage });

  const po = body.purchaseOrderId
    ? db.purchaseOrders.find((p) => p.id === body.purchaseOrderId && p.branchId === branchId)
    : null;
  if (body.purchaseOrderId && (!po || po.status !== 'Issued')) {
    add(
      'PurchaseOrderId',
      'PurchaseOrderNotIssued',
      'Goods can only be received against an issued purchase order.',
    );
  }
  if (po && body.supplierId && body.supplierId !== po.supplierId) {
    add(
      'SupplierId',
      'ReceiptSupplierMismatch',
      "The supplier is not the purchase order's supplier.",
    );
  }
  const consignment = body.consignmentId
    ? db.consignments.find((c) => c.id === body.consignmentId && c.branchId === branchId)
    : null;
  if (body.consignmentId && (!consignment || consignment.status !== 'Cleared')) {
    add(
      'ConsignmentId',
      'ConsignmentNotCleared',
      'Goods can only be received against a cleared consignment.',
    );
  }
  if (problems.length > 0) return problems;

  if (body.lines.length === 0) {
    add('Lines', 'GoodsReceiptLinesRequired', 'A goods receipt has between 1 and 200 lines.');
    return problems;
  }

  const acceptedPerPoLine = new Map<string, number>();
  const seen = new Set<string>();
  body.lines.forEach((line, i) => {
    const prefix = `Lines[${i}]`;
    const sku = db.skus.find((s) => s.skuId === line.skuId);
    if (!sku) {
      add(`${prefix}.SkuId`, 'SkuNotFound', 'That SKU does not exist.');
      return;
    }
    const accepted = parseFloat(line.quantityAccepted || '0');
    const rejected = parseFloat(line.quantityRejected || '0');
    let quantitiesValid = true;
    for (const [value, raw, property] of [
      [accepted, line.quantityAccepted, 'QuantityAccepted'],
      [rejected, line.quantityRejected, 'QuantityRejected'],
    ] as const) {
      if (value < 0) {
        add(`${prefix}.${property}`, 'InvalidQuantity', 'A quantity cannot be negative.');
        quantitiesValid = false;
      } else if (value > 0 && !sku.allowsFractionalQuantity && String(raw).includes('.')) {
        add(
          `${prefix}.${property}`,
          'FractionalQuantityNotAllowed',
          'This item can only be counted in whole units.',
        );
        quantitiesValid = false;
      }
    }
    if (quantitiesValid && accepted + rejected <= 0) {
      add(
        `${prefix}.QuantityAccepted`,
        'InvalidQuantity',
        'A line must receive or reject a quantity.',
      );
    }
    if (rejected > 0 && !line.rejectionReason) {
      add(
        `${prefix}.RejectionReason`,
        'RejectionReasonRequired',
        'A rejected quantity needs a reason.',
      );
    }
    const lot = (line.lotNumber ?? '').trim();
    const takes = accepted > 0;
    if (takes && (lot.length === 0 || lot.length > 60)) {
      add(
        `${prefix}.LotNumber`,
        'LotNumberRequired',
        'A lot number (at most 60 characters) is required for accepted stock.',
      );
    }
    if (takes && sku.expiryRequired && !line.expiresOn) {
      add(
        `${prefix}.ExpiresOn`,
        'ExpiryDateRequired',
        'Food and medicine must be received with an expiry date.',
      );
    }
    if (takes && line.expiresOn && line.expiresOn < todayDate()) {
      add(
        `${prefix}.ExpiresOn`,
        'BatchAlreadyExpired',
        'Expired goods cannot be accepted -- reject them with the reason Expired.',
      );
    }
    if (
      line.manufacturedOn &&
      (line.manufacturedOn > todayDate() ||
        (line.expiresOn && line.manufacturedOn >= line.expiresOn))
    ) {
      add(
        `${prefix}.ManufacturedOn`,
        'InvalidBatchDates',
        'The manufacture date must be in the past and before the expiry date.',
      );
    }
    if (line.purchaseOrderLineId) {
      const poLine = po?.lines.find((l) => l.lineId === line.purchaseOrderLineId);
      if (!poLine || poLine.skuId !== line.skuId) {
        add(
          `${prefix}.PurchaseOrderLineId`,
          'PurchaseOrderLineMismatch',
          'That purchase order line does not exist for this SKU.',
        );
      } else if (takes) {
        const total = (acceptedPerPoLine.get(poLine.lineId) ?? 0) + accepted;
        acceptedPerPoLine.set(poLine.lineId, total);
        if (poLine.acceptedQuantity + total > poLine.orderedQuantity) {
          add(
            `${prefix}.QuantityAccepted`,
            'ReceiptExceedsOutstanding',
            `Only ${q(Math.max(0, poLine.orderedQuantity - poLine.acceptedQuantity))} is still outstanding on that order line.`,
          );
        }
      }
    } else if (takes && (!line.unitCostAmount || !line.unitCostCurrency)) {
      add(
        `${prefix}.UnitCostAmount`,
        'UnitCostRequired',
        'A unit cost is required when receiving without a purchase order.',
      );
    } else if (takes && line.unitCostCurrency !== MOCK_BASE_CURRENCY) {
      const rate = consignment?.exchangeRates.find((r) => r.currencyCode === line.unitCostCurrency);
      if (!rate && !line.exchangeRateToBase) {
        add(
          `${prefix}.ExchangeRateToBase`,
          'ExchangeRateMissing',
          `No exchange rate to ${MOCK_BASE_CURRENCY} is recorded for ${line.unitCostCurrency}.`,
        );
      }
    }
    const location = locationOf(line.locationId);
    if (!location || location.branchId !== branchId || !location.isActive) {
      add(
        `${prefix}.LocationId`,
        'StockLocationNotFound',
        'That location does not exist at this branch.',
      );
    } else if (!location.permittedMovements.includes('Receipt')) {
      add(
        `${prefix}.LocationId`,
        'MovementNotPermittedAtLocation',
        'Stock cannot be received at that location.',
      );
    }
    const key = `${line.skuId}|${lot}|${line.expiresOn ?? ''}`;
    if (takes) {
      if (seen.has(key))
        add(
          `${prefix}.LotNumber`,
          'DuplicateReceiptLine',
          'The same SKU, lot and expiry cannot appear twice in one receipt.',
        );
      seen.add(key);
    }
  });
  return problems;
}

export const receiptHandlers: HttpHandler[] = [
  http.get('*/api/staff/inventory/goods-receipts', ({ request }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    const url = new URL(request.url);
    const from = url.searchParams.get('from');
    const to = url.searchParams.get('to');
    const supplierId = url.searchParams.get('supplierId');
    const purchaseOrderId = url.searchParams.get('purchaseOrderId');
    const rows = db.receipts
      .filter((r) => r._branchId === branchOf(request))
      .filter((r) => !from || r.receivedOn.slice(0, 10) >= from)
      .filter((r) => !to || r.receivedOn.slice(0, 10) <= to)
      .filter((r) => !supplierId || r.supplierId === supplierId)
      .filter((r) => !purchaseOrderId || r.purchaseOrderId === purchaseOrderId)
      .sort((a, b) => b.number.localeCompare(a.number))
      .map(summary);
    return HttpResponse.json(paged(rows, request));
  }),

  http.get('*/api/staff/inventory/goods-receipts/:id', ({ request, params }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    const receipt = db.receipts.find(
      (r) => r.id === params.id && r._branchId === branchOf(request),
    );
    return receipt
      ? HttpResponse.json(detail(receipt, isAllowed(request, ROLES.reports)))
      : notFoundResponse();
  }),

  http.post('*/api/staff/inventory/goods-receipts', async ({ request }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const body = (await request.json()) as PostGoodsReceiptRequest;
    const branchId = branchOf(request);
    const problems = validate(body, branchId);
    if (problems.length > 0) return violations(problems);

    const po = body.purchaseOrderId
      ? db.purchaseOrders.find((p) => p.id === body.purchaseOrderId)!
      : null;
    const consignment = body.consignmentId
      ? db.consignments.find((c) => c.id === body.consignmentId)!
      : null;
    const supplier = db.suppliers.find((s) => s.id === (body.supplierId ?? po?.supplierId));
    const receiptId = newId('61');
    const number = docNumber('GRN');
    const receivedOn = nowIso();
    const batchIds: string[] = [];

    const lines: GoodsReceiptDetail['lines'] = body.lines.map((line) => {
      const sku = skuOf(line.skuId);
      const accepted = parseFloat(line.quantityAccepted || '0');
      const rejected = parseFloat(line.quantityRejected || '0');
      const poLine = po?.lines.find((l) => l.lineId === line.purchaseOrderLineId);
      const cost = poLine ? poLine.unitCost : parseFloat(line.unitCostAmount ?? '0');
      const currency = poLine ? po!.currencyCode : (line.unitCostCurrency ?? MOCK_BASE_CURRENCY);
      const rate =
        consignment?.exchangeRates.find((r) => r.currencyCode === currency)?.rateToBase ??
        line.exchangeRateToBase ??
        '1';
      let batchId: string | null = null;
      if (accepted > 0) {
        batchId = newId('c1');
        batchIds.push(batchId);
        const held = line.holdForInspection;
        const batch: MBatch = {
          id: batchId,
          branchId,
          skuId: sku.skuId,
          lotNumber: line.lotNumber.trim(),
          manufacturedOn: line.manufacturedOn,
          expiresOn: line.expiresOn,
          status: held ? 'Quarantined' : 'Available',
          statusReason: held ? 'Held for inspection' : null,
          receivedOn,
          receivedQuantity: accepted,
          goodsReceiptId: receiptId,
          goodsReceiptNumber: number,
          consignmentId: consignment?.id ?? null,
          supplierId: supplier?.id ?? null,
          originCountryCode: sku.countryOfOriginCode,
          purchaseUnitCost: cost,
          unitCostBase: currency === MOCK_BASE_CURRENCY ? cost : cost * parseFloat(rate),
          costBasis: 'purchase',
          statusHistory: held
            ? [
                {
                  from: 'Available',
                  to: 'Quarantined',
                  reason: 'Held for inspection',
                  changedOn: receivedOn,
                  changedBy: ACTOR_ID,
                },
              ]
            : [],
          recall: null,
          recallStatus: null,
          version: 1,
        };
        db.batches.push(batch);
        moveStock({
          branchId,
          kind: 'Receipt',
          batchId,
          locationId: line.locationId,
          delta: accepted,
          referenceKind: 'GoodsReceipt',
          referenceId: receiptId,
          referenceLabel: number,
        });
      }
      if (poLine) {
        poLine.acceptedQuantity += accepted;
        poLine.rejectedQuantity += rejected;
      }
      return {
        lineId: newId('b2'),
        skuId: sku.skuId,
        skuCode: sku.skuCode,
        displayName: sku.displayName,
        stockUnit: sku.stockUnit,
        lotNumber: line.lotNumber.trim(),
        manufacturedOn: line.manufacturedOn,
        expiresOn: line.expiresOn,
        quantityAccepted: q(accepted),
        quantityRejected: q(rejected),
        rejectionReason: rejected > 0 ? line.rejectionReason : null,
        rejectionNote: line.rejectionNote,
        locationId: line.locationId,
        locationCode: locationOf(line.locationId)?.code ?? null,
        holdForInspection: line.holdForInspection,
        unitCost: accepted > 0 ? { ...unitCost(cost, currency) } : null,
        batchId,
        batchStatus: batchId ? (line.holdForInspection ? 'Quarantined' : 'Available') : null,
        storage: sku.storage,
      };
    });

    db.receipts.push({
      _branchId: branchId,
      _poLines: body.lines.map((l) => l.purchaseOrderLineId),
      id: receiptId,
      number,
      branchId,
      receivedOn,
      receivedBy: ACTOR_ID,
      supplierId: supplier?.id ?? null,
      supplierName: supplier?.name ?? null,
      purchaseOrderId: po?.id ?? null,
      purchaseOrderNumber: po?.number ?? null,
      consignmentId: consignment?.id ?? null,
      consignmentReference: consignment?.reference ?? null,
      deliveryReference: body.deliveryReference,
      lines,
    });
    po?.receipts.push({ goodsReceiptId: receiptId, number, receivedOn });
    if (po) po.version += 1;
    persist();
    return HttpResponse.json({ goodsReceiptId: receiptId, number, batchIds }, { status: 201 });
  }),
];
