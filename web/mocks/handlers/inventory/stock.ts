import { http, HttpResponse, type HttpHandler } from 'msw';
import type {
  BatchDetail,
  BatchSummary,
  FefoPreviewLine,
  SkuStockMatrix,
  StockSummary,
} from '@/lib/api/inventory-stock';
import {
  MOCK_BRANCH_ZONE,
  ROLES,
  branchOf,
  daysBetween,
  guard,
  isAllowed,
  noContentResponse,
  notFoundResponse,
  nowIso,
  omit,
  paged,
  q,
  todayDate,
  unitCost,
  violation,
} from './fixtures';
import { ACTOR_ID, db, persist, staffNames, type MBatch, type MLedger } from './store';

/** `mocks/handlers/inventory/stock.ts` — `StaffStockController`, `StaffBatchesController`,
 *  `StaffLedgerController`, plus the helpers the other mocks use to move stock. */

export function skuOf(skuId: string) {
  return db.skus.find((s) => s.skuId === skuId)!;
}

export function locationOf(locationId: string) {
  return db.locations.find((l) => l.id === locationId);
}

export function isExpired(batch: MBatch): boolean {
  return batch.expiresOn !== null && batch.expiresOn < todayDate();
}

export function daysToExpiry(batch: MBatch): number | null {
  return batch.expiresOn === null ? null : daysBetween(todayDate(), batch.expiresOn);
}

export function branchBatches(branchId: string): MBatch[] {
  return db.batches.filter((b) => b.branchId === branchId);
}

export function positionsOf(batchId: string) {
  return db.positions.filter((p) => p.batchId === batchId);
}

/** Appends one ledger row (the mock's stand-in for `IStockLedger`). */
export function addLedger(entry: {
  branchId: string;
  kind: string;
  batchId: string;
  locationId: string;
  delta: number;
  referenceKind: string;
  referenceId: string;
  referenceLabel: string;
  note?: string | null;
}): MLedger {
  const batch = db.batches.find((b) => b.id === entry.batchId)!;
  const sku = skuOf(batch.skuId);
  const location = locationOf(entry.locationId);
  const row: MLedger = {
    sequence: db.ledger.reduce((max, e) => Math.max(max, e.sequence), 0) + 1,
    occurredOn: nowIso(),
    kind: entry.kind,
    quantityDelta: q(entry.delta),
    stockUnit: sku.stockUnit,
    locationId: entry.locationId,
    locationCode: location?.code ?? null,
    batchId: batch.id,
    lotNumber: batch.lotNumber,
    skuId: sku.skuId,
    skuCode: sku.skuCode,
    referenceKind: entry.referenceKind,
    referenceId: entry.referenceId,
    referenceLabel: entry.referenceLabel,
    fefoOverridden: false,
    overrideReason: null,
    performedBy: ACTOR_ID,
    performedByName: staffNames[ACTOR_ID] ?? null,
    note: entry.note ?? null,
    branchId: entry.branchId,
  };
  db.ledger.push(row);
  return row;
}

/** Changes a position's on-hand by `delta` (creating it if needed) and writes the ledger row. */
export function moveStock(entry: Parameters<typeof addLedger>[0]): void {
  let position = db.positions.find(
    (p) => p.batchId === entry.batchId && p.locationId === entry.locationId,
  );
  if (!position) {
    position = { batchId: entry.batchId, locationId: entry.locationId, onHand: 0, reserved: 0 };
    db.positions.push(position);
  }
  position.onHand = Math.round((position.onHand + entry.delta) * 1000) / 1000;
  addLedger(entry);
}

export function onHandOfBatch(batchId: string): number {
  return positionsOf(batchId).reduce((sum, p) => sum + p.onHand, 0);
}

function batchAllowedActions(batch: MBatch): string[] {
  switch (batch.status) {
    case 'Available':
      return ['quarantine', 'recall', 'write-off'];
    case 'Quarantined':
      return ['release', 'recall', 'write-off'];
    case 'Recalled':
      return ['withdraw-recall', 'write-off'];
    default:
      return [];
  }
}

function batchSummary(batch: MBatch): BatchSummary {
  const sku = skuOf(batch.skuId);
  return {
    id: batch.id,
    skuId: sku.skuId,
    skuCode: sku.skuCode,
    displayName: sku.displayName,
    lotNumber: batch.lotNumber,
    expiresOn: batch.expiresOn,
    isExpired: isExpired(batch),
    daysToExpiry: daysToExpiry(batch),
    branchTimeZoneId: MOCK_BRANCH_ZONE,
    status: batch.status,
    receivedQuantity: q(batch.receivedQuantity),
    onHand: q(onHandOfBatch(batch.id)),
    stockUnit: sku.stockUnit,
    consignmentId: batch.consignmentId,
  };
}

function batchDetail(batch: MBatch, includeCosts: boolean): BatchDetail {
  const sku = skuOf(batch.skuId);
  const supplier = db.suppliers.find((s) => s.id === batch.supplierId);
  const consignment = db.consignments.find((c) => c.id === batch.consignmentId);
  return {
    id: batch.id,
    skuId: sku.skuId,
    skuCode: sku.skuCode,
    displayName: sku.displayName,
    stockUnit: sku.stockUnit,
    stockClass: sku.stockClass,
    branchId: batch.branchId,
    lotNumber: batch.lotNumber,
    manufacturedOn: batch.manufacturedOn,
    expiresOn: batch.expiresOn,
    isExpired: isExpired(batch),
    daysToExpiry: daysToExpiry(batch),
    branchTimeZoneId: MOCK_BRANCH_ZONE,
    status: batch.status,
    statusReason: batch.statusReason,
    receivedOn: batch.receivedOn,
    receivedQuantity: q(batch.receivedQuantity),
    goodsReceiptId: batch.goodsReceiptId,
    goodsReceiptNumber: batch.goodsReceiptNumber,
    consignmentId: batch.consignmentId,
    consignmentReference: consignment?.reference ?? null,
    supplierId: supplier?.id ?? null,
    originCountryCode: batch.originCountryCode,
    purchaseUnitCost: includeCosts ? unitCost(batch.purchaseUnitCost) : null,
    unitCostBase: includeCosts ? unitCost(batch.unitCostBase) : null,
    costBasis: includeCosts ? batch.costBasis : null,
    positions: positionsOf(batch.id).map((p) => {
      const location = locationOf(p.locationId)!;
      return {
        locationId: p.locationId,
        locationCode: location.code,
        locationKind: location.kind,
        onHand: q(p.onHand),
        reserved: q(p.reserved),
      };
    }),
    recall: batch.recall,
    statusHistory: batch.statusHistory,
    allowedActions: batchAllowedActions(batch),
    storage: sku.storage,
    version: batch.version,
  };
}

/** The mock's single place that decides whether a position's stock counts as held, expired or free. */
function bucketOf(batch: MBatch, locationKind: string): 'held' | 'expired' | 'free' | 'none' {
  if (batch.status === 'WrittenOff') return 'none';
  if (
    batch.status === 'Quarantined' ||
    batch.status === 'Recalled' ||
    locationKind === 'Quarantine'
  )
    return 'held';
  return isExpired(batch) ? 'expired' : 'free';
}

function stockSummaries(branchId: string, locationId: string | null): StockSummary[] {
  const batches = branchBatches(branchId);
  const bySku = new Map<string, StockSummary & { _next: string | null }>();
  for (const batch of batches) {
    for (const position of positionsOf(batch.id)) {
      if (locationId && position.locationId !== locationId) continue;
      const location = locationOf(position.locationId)!;
      const sku = skuOf(batch.skuId);
      const row =
        bySku.get(sku.skuId) ??
        ({
          skuId: sku.skuId,
          skuCode: sku.skuCode,
          displayName: sku.displayName,
          stockUnit: sku.stockUnit,
          stockClass: sku.stockClass,
          onHand: '0',
          reserved: '0',
          usable: '0',
          sellable: '0',
          expiredQuantity: '0',
          heldQuantity: '0',
          nextExpiresOn: null,
          storage: sku.storage,
          _next: null,
        } as StockSummary & { _next: string | null });
      const add = (
        key: 'onHand' | 'reserved' | 'usable' | 'sellable' | 'expiredQuantity' | 'heldQuantity',
        amount: number,
      ) => {
        row[key] = q(parseFloat(row[key]) + amount);
      };
      add('onHand', position.onHand);
      add('reserved', position.reserved);
      const bucket = bucketOf(batch, location.kind);
      if (bucket === 'held') add('heldQuantity', position.onHand);
      else if (bucket === 'expired') add('expiredQuantity', position.onHand);
      else if (bucket === 'free') {
        add('usable', position.onHand - position.reserved);
        if (location.kind === 'ShopFloor' || location.kind === 'Warehouse')
          add('sellable', position.onHand - position.reserved);
        if (
          batch.expiresOn &&
          position.onHand > 0 &&
          (row._next === null || batch.expiresOn < row._next)
        )
          row._next = batch.expiresOn;
      }
      bySku.set(sku.skuId, row);
    }
  }
  return [...bySku.values()].map(({ _next, ...row }) => ({ ...row, nextExpiresOn: _next }));
}

export function effectiveReorderPoint(branchId: string, skuId: string, locationId: string): number {
  return (
    db.reorderPolicies.find(
      (p) => p.branchId === branchId && p.skuId === skuId && p.locationId === locationId,
    )?.reorderPoint ?? 10
  );
}

const PURPOSE_MOVEMENT: Record<string, string> = {
  Sale: 'Sale',
  Dispense: 'Dispense',
  ClinicalUse: 'ClinicalUse',
  BoardingUse: 'BoardingUse',
};

export const stockHandlers: HttpHandler[] = [
  http.get('*/api/staff/inventory/stock', ({ request }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    const url = new URL(request.url);
    const branchId = branchOf(request);
    const search = (url.searchParams.get('search') ?? '').toLowerCase();
    const cls = url.searchParams.get('class');
    const below = url.searchParams.get('belowReorderPoint') === 'true';
    const locationId = url.searchParams.get('locationId');
    let rows = stockSummaries(branchId, locationId)
      .filter(
        (s) =>
          !search ||
          s.displayName.toLowerCase().includes(search) ||
          s.skuCode.toLowerCase().includes(search),
      )
      .filter((s) => !cls || s.stockClass === cls);
    if (below) {
      rows = rows.filter((s) =>
        db.reorderPolicies.some(
          (p) =>
            p.branchId === branchId &&
            p.skuId === s.skuId &&
            (!locationId || p.locationId === locationId) &&
            parseFloat(s.usable) < p.reorderPoint,
        ),
      );
    }
    rows.sort((a, b) => a.displayName.localeCompare(b.displayName));
    return HttpResponse.json(paged(rows, request));
  }),

  http.get('*/api/staff/inventory/fefo-preview', ({ request }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    const url = new URL(request.url);
    const skuId = url.searchParams.get('skuId') ?? '';
    const quantity = parseFloat(url.searchParams.get('quantity') ?? '0');
    const purpose = url.searchParams.get('purpose') ?? '';
    const locationId = url.searchParams.get('locationId');
    const sku = db.skus.find((s) => s.skuId === skuId);
    if (!sku) return violation('skuId', 'SkuNotFound', 'That SKU does not exist.');
    if (!(quantity > 0))
      return violation('quantity', 'InvalidQuery', 'quantity must be greater than zero.');
    const movement = PURPOSE_MOVEMENT[purpose];
    if (!movement)
      return violation('purpose', 'InvalidQuery', 'purpose is not a recognised purpose.');
    const chosen = locationId ? locationOf(locationId) : null;
    if (chosen && !chosen.permittedMovements.includes(movement)) {
      return violation(
        'locationId',
        'MovementNotPermittedAtLocation',
        `${chosen.name} is a ${chosen.kind} — that movement isn't allowed there.`,
      );
    }
    const priority = (kind: string) => (kind === 'Warehouse' ? 1 : kind === 'ShopFloor' ? 2 : 3);
    const candidates = branchBatches(branchOf(request))
      .filter((b) => b.skuId === skuId && b.status === 'Available' && !isExpired(b))
      .flatMap((b) =>
        positionsOf(b.id)
          .map((p) => ({ batch: b, position: p, location: locationOf(p.locationId)! }))
          .filter(
            ({ position, location }) =>
              position.onHand - position.reserved > 0 &&
              location.permittedMovements.includes(movement) &&
              (!locationId || location.id === locationId),
          ),
      )
      .sort((a, b) => {
        const ea = a.batch.expiresOn ?? '9999-12-31';
        const eb = b.batch.expiresOn ?? '9999-12-31';
        return (
          ea.localeCompare(eb) ||
          priority(a.location.kind) - priority(b.location.kind) ||
          a.batch.lotNumber.localeCompare(b.batch.lotNumber)
        );
      });
    let remaining = quantity;
    const lines: FefoPreviewLine[] = [];
    for (const { batch, position, location } of candidates) {
      if (remaining <= 0) break;
      const take = Math.min(remaining, position.onHand - position.reserved);
      lines.push({
        batchId: batch.id,
        lotNumber: batch.lotNumber,
        expiresOn: batch.expiresOn,
        locationId: location.id,
        locationCode: location.code,
        quantity: q(take),
      });
      remaining -= take;
    }
    return HttpResponse.json({
      lines,
      shortfall: q(Math.max(0, remaining)),
      stockUnit: sku.stockUnit,
    });
  }),

  http.get('*/api/staff/inventory/stock/:skuId', ({ request, params }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    const sku = db.skus.find((s) => s.skuId === params.skuId);
    if (!sku) return notFoundResponse();
    const batches = branchBatches(branchOf(request)).filter((b) => b.skuId === sku.skuId);
    const matrix: SkuStockMatrix = {
      skuId: sku.skuId,
      skuCode: sku.skuCode,
      displayName: sku.displayName,
      stockUnit: sku.stockUnit,
      storage: sku.storage,
      branchTimeZoneId: MOCK_BRANCH_ZONE,
      batches: batches
        .sort((a, b) => (a.expiresOn ?? '9999').localeCompare(b.expiresOn ?? '9999'))
        .map((b) => ({
          batchId: b.id,
          lotNumber: b.lotNumber,
          expiresOn: b.expiresOn,
          isExpired: isExpired(b),
          daysToExpiry: daysToExpiry(b),
          status: b.status,
          locations: positionsOf(b.id).map((p) => {
            const location = locationOf(p.locationId)!;
            return {
              locationId: p.locationId,
              locationCode: location.code,
              locationKind: location.kind,
              onHand: q(p.onHand),
              reserved: q(p.reserved),
            };
          }),
        })),
    };
    return HttpResponse.json(matrix);
  }),

  http.get('*/api/staff/inventory/batches', ({ request }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    const url = new URL(request.url);
    const search = (url.searchParams.get('search') ?? '').toLowerCase();
    const skuId = url.searchParams.get('skuId');
    const status = url.searchParams.get('status');
    const within = url.searchParams.get('expiringWithinDays');
    const consignmentId = url.searchParams.get('consignmentId');
    const rows = branchBatches(branchOf(request))
      .filter((b) => !skuId || b.skuId === skuId)
      .filter((b) => !status || b.status === status)
      .filter((b) => !consignmentId || b.consignmentId === consignmentId)
      .filter(
        (b) =>
          within === null ||
          within === '' ||
          (b.expiresOn !== null && daysBetween(todayDate(), b.expiresOn) <= parseInt(within, 10)),
      )
      .filter((b) => {
        if (!search) return true;
        const sku = skuOf(b.skuId);
        return (
          b.lotNumber.toLowerCase().includes(search) ||
          sku.skuCode.toLowerCase().includes(search) ||
          sku.displayName.toLowerCase().includes(search)
        );
      })
      .sort((a, b) => (a.expiresOn ?? '9999').localeCompare(b.expiresOn ?? '9999'))
      .map(batchSummary);
    return HttpResponse.json(paged(rows, request));
  }),

  http.get('*/api/staff/inventory/batches/:id', ({ request, params }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    const batch = branchBatches(branchOf(request)).find((b) => b.id === params.id);
    return batch
      ? HttpResponse.json(batchDetail(batch, isAllowed(request, ROLES.reports)))
      : notFoundResponse();
  }),

  http.post('*/api/staff/inventory/batches/:id/quarantine', async ({ request, params }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const batch = branchBatches(branchOf(request)).find((b) => b.id === params.id);
    if (!batch) return notFoundResponse();
    const { reason } = (await request.json()) as { reason: string };
    if (batch.status !== 'Available') {
      return violation(
        'BatchId',
        'InvalidBatchTransition',
        `A ${batch.status} batch cannot be quarantined.`,
      );
    }
    batch.statusHistory.push({
      from: batch.status,
      to: 'Quarantined',
      reason,
      changedOn: nowIso(),
      changedBy: ACTOR_ID,
    });
    batch.status = 'Quarantined';
    batch.statusReason = reason;
    batch.version += 1;
    persist();
    return noContentResponse();
  }),

  http.post('*/api/staff/inventory/batches/:id/release', ({ request, params }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const batch = branchBatches(branchOf(request)).find((b) => b.id === params.id);
    if (!batch) return notFoundResponse();
    if (batch.status !== 'Quarantined') {
      return violation(
        'BatchId',
        'InvalidBatchTransition',
        `A ${batch.status} batch cannot be released.`,
      );
    }
    batch.statusHistory.push({
      from: batch.status,
      to: 'Available',
      reason: null,
      changedOn: nowIso(),
      changedBy: ACTOR_ID,
    });
    batch.status = 'Available';
    batch.statusReason = null;
    batch.version += 1;
    persist();
    return noContentResponse();
  }),

  http.post('*/api/staff/inventory/batches/:id/write-off', async ({ request, params }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const batch = branchBatches(branchOf(request)).find((b) => b.id === params.id);
    if (!batch) return notFoundResponse();
    const { reason } = (await request.json()) as { reason: string };
    if (batch.status === 'WrittenOff') {
      return violation(
        'BatchId',
        'InvalidBatchTransition',
        'This batch has already been written off.',
      );
    }
    if (reason.includes('ELSEWHERE')) {
      return violation(
        'BatchId',
        'StockLocationNotFound',
        'That location does not exist at your branches. A write-off needs you to be assigned to every branch that holds the batch.',
      );
    }
    for (const position of positionsOf(batch.id).filter((p) => p.onHand > 0)) {
      moveStock({
        branchId: batch.branchId,
        kind: 'Wastage',
        batchId: batch.id,
        locationId: position.locationId,
        delta: -position.onHand,
        referenceKind: 'BatchWriteOff',
        referenceId: batch.id,
        referenceLabel: `Write-off ${batch.lotNumber}`,
        note: reason,
      });
    }
    batch.statusHistory.push({
      from: batch.status,
      to: 'WrittenOff',
      reason,
      changedOn: nowIso(),
      changedBy: ACTOR_ID,
    });
    batch.status = 'WrittenOff';
    batch.statusReason = reason;
    batch.version += 1;
    persist();
    return noContentResponse();
  }),

  http.get('*/api/staff/inventory/ledger', ({ request }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    const url = new URL(request.url);
    const from = url.searchParams.get('from');
    const to = url.searchParams.get('to');
    if (!from || !to) return violation('from', 'InvalidQuery', 'from and to are required.');
    if (to < from) return violation('to', 'InvalidQuery', 'to cannot be before from.');
    if (daysBetween(from, to) + 1 > 366)
      return violation('to', 'InvalidQuery', 'The range cannot exceed 366 days.');
    const skuId = url.searchParams.get('skuId');
    const batchId = url.searchParams.get('batchId');
    const locationId = url.searchParams.get('locationId');
    const kind = url.searchParams.get('kind');
    const referenceKind = url.searchParams.get('referenceKind');
    const referenceId = url.searchParams.get('referenceId');
    const rows = db.ledger
      .filter((e) => e.branchId === branchOf(request))
      .filter((e) => e.occurredOn.slice(0, 10) >= from && e.occurredOn.slice(0, 10) <= to)
      .filter((e) => !skuId || e.skuId === skuId)
      .filter((e) => !batchId || e.batchId === batchId)
      .filter((e) => !locationId || e.locationId === locationId)
      .filter((e) => !kind || e.kind === kind)
      .filter((e) => !referenceKind || e.referenceKind === referenceKind)
      .filter((e) => !referenceId || e.referenceId === referenceId)
      .sort((a, b) => b.sequence - a.sequence)
      .map((entry) => omit(entry, 'branchId'));
    return HttpResponse.json(paged(rows, request));
  }),
];
