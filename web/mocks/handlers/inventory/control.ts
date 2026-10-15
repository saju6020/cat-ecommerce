import { http, HttpResponse, type HttpHandler } from 'msw';
import type {
  AdjustmentDetail,
  AdjustmentReason,
  AdjustmentSummary,
  CountDetail,
  CountLine,
  CountScope,
  CountSummary,
  CountVariance,
  PostTransferRequest,
  RequestAdjustmentRequest,
  TransferSummary,
} from '@/lib/api/inventory-control';
import {
  MOCK_APPROVAL_THRESHOLD,
  MOCK_BASE_CURRENCY,
  ROLES,
  branchOf,
  guard,
  money,
  noContentResponse,
  omit,
  notFoundResponse,
  nowIso,
  paged,
  q,
  violation,
  violations,
  type Violation,
} from './fixtures';
import { ACTOR_ID, db, docNumber, newId, persist } from './store';
import { locationOf, moveStock, positionsOf, skuOf } from './stock';

/** `mocks/handlers/inventory/control.ts` — transfers, adjustments and counts
 *  (`StaffTransfersController`, `StaffAdjustmentsController`, `StaffCountsController`). */

const REMOVES: AdjustmentReason[] = [
  'Damage',
  'Theft',
  'Sampling',
  'InternalUse',
  'Expired',
  'RecallDisposal',
];

function batchFor(branchId: string, batchId: string, skuId: string) {
  const batch = db.batches.find((b) => b.id === batchId && b.branchId === branchId);
  return batch && batch.skuId === skuId ? batch : null;
}

function insufficient(index: number, available: number) {
  return {
    propertyName: `Lines[${index}].Quantity`,
    errorCode: 'InsufficientStock',
    errorMessage: `Only ${q(available)} is available. That quantity is no longer available — someone may have just taken it. Refresh and try again.`,
  };
}

// ---- transfers --------------------------------------------------------------------------------

function transferSummary(t: (typeof db.transfers)[number]): TransferSummary {
  return {
    id: t.id,
    number: t.number,
    fromLocationId: t.fromLocationId,
    fromLocationCode: t.fromLocationCode,
    toLocationId: t.toLocationId,
    toLocationCode: t.toLocationCode,
    transferredOn: t.transferredOn,
    transferredBy: t.transferredBy,
    lineCount: t.lines.length,
    reason: t.reason,
  };
}

// ---- adjustments ------------------------------------------------------------------------------

function adjustmentSummary(a: (typeof db.adjustments)[number]): AdjustmentSummary {
  return {
    id: a.id,
    number: a.number,
    locationId: a.locationId,
    locationCode: a.locationCode,
    reason: a.reason,
    status: a.status,
    valueBase: a.valueBase,
    requestedBy: a.requestedBy,
    requestedOn: a.requestedOn,
    lineCount: a.lines.length,
  };
}

function adjustmentDetail(a: (typeof db.adjustments)[number]): AdjustmentDetail {
  return omit(a, '_branchId', '_staleOnApprove', '_requesterId');
}

function applyAdjustment(a: (typeof db.adjustments)[number]): Violation[] {
  const problems: Violation[] = [];
  a.lines.forEach((line, index) => {
    const delta = parseFloat(line.quantityDelta);
    const position = positionsOf(line.batchId).find((p) => p.locationId === a.locationId);
    if (delta < 0 && (position?.onHand ?? 0) - (position?.reserved ?? 0) < -delta) {
      problems.push(insufficient(index, (position?.onHand ?? 0) - (position?.reserved ?? 0)));
    }
  });
  if (problems.length > 0) return problems;
  for (const line of a.lines) {
    const delta = parseFloat(line.quantityDelta);
    moveStock({
      branchId: a._branchId,
      kind: delta < 0 ? 'AdjustmentOut' : 'AdjustmentIn',
      batchId: line.batchId,
      locationId: a.locationId,
      delta,
      referenceKind: 'StockAdjustment',
      referenceId: a.id,
      referenceLabel: a.number,
      note: a.note,
    });
  }
  return [];
}

// ---- counts -----------------------------------------------------------------------------------

type MCount = (typeof db.counts)[number];

function countSummary(c: MCount): CountSummary {
  return {
    id: c.id,
    number: c.number,
    locationId: c.locationId,
    locationCode: c.locationCode,
    scope: c.scope,
    status: c.status,
    lineCount: c.lines.length,
    varianceValueBase: c.varianceValueBase,
    openedOn: c.openedOn,
    openedBy: c.openedBy,
  };
}

function countAllowed(status: MCount['status']): string[] {
  if (status === 'Open') return ['record-lines', 'submit', 'cancel'];
  if (status === 'Submitted') return ['commit', 'cancel'];
  if (status === 'AwaitingApproval') return ['approve', 'reject'];
  return [];
}

function countDetail(c: MCount): CountDetail {
  const blind = c.status === 'Open';
  return {
    ...omit(c, '_branchId', '_variance'),
    isBlind: blind,
    varianceValueBase: blind ? null : c.varianceValueBase,
    lines: c.lines.map((l) => (blind ? { ...l, bookQuantity: null, variance: null } : l)),
    allowedActions: countAllowed(c.status),
  };
}

function commitCountToLedger(c: MCount): void {
  for (const line of c._variance?.lines ?? []) {
    const variance = parseFloat(line.variance);
    if (variance === 0) continue;
    moveStock({
      branchId: c._branchId,
      kind: variance > 0 ? 'CountGain' : 'CountLoss',
      batchId: line.batchId,
      locationId: c.locationId,
      delta: variance,
      referenceKind: 'StockCount',
      referenceId: c.id,
      referenceLabel: c.number,
    });
  }
  c.status = 'Committed';
}

export const controlHandlers: HttpHandler[] = [
  // ---- transfers ----------------------------------------------------------------------------
  http.get('*/api/staff/inventory/transfers', ({ request }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    const rows = db.transfers
      .filter((t) => t._branchId === branchOf(request))
      .sort((a, b) => b.number.localeCompare(a.number))
      .map(transferSummary);
    return HttpResponse.json(paged(rows, request));
  }),

  http.get('*/api/staff/inventory/transfers/:id', ({ request, params }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    const transfer = db.transfers.find(
      (t) => t.id === params.id && t._branchId === branchOf(request),
    );
    if (!transfer) return notFoundResponse();
    return HttpResponse.json(omit(transfer, '_branchId'));
  }),

  http.post('*/api/staff/inventory/transfers', async ({ request }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const body = (await request.json()) as PostTransferRequest;
    const branchId = branchOf(request);
    const problems: Violation[] = [];
    if (body.fromLocationId === body.toLocationId) {
      problems.push({
        propertyName: 'ToLocationId',
        errorCode: 'TransferLocationsMustDiffer',
        errorMessage: 'The two locations must be different.',
      });
    }
    const from = locationOf(body.fromLocationId);
    const to = locationOf(body.toLocationId);
    if (!from || from.branchId !== branchId || !from.isActive) {
      problems.push({
        propertyName: 'FromLocationId',
        errorCode: 'StockLocationNotFound',
        errorMessage: 'That location does not exist at this branch.',
      });
    } else if (!from.permittedMovements.includes('TransferOut')) {
      problems.push({
        propertyName: 'FromLocationId',
        errorCode: 'MovementNotPermittedAtLocation',
        errorMessage: 'Stock cannot be transferred out of that location.',
      });
    }
    if (!to || !to.isActive) {
      problems.push({
        propertyName: 'ToLocationId',
        errorCode: 'StockLocationNotFound',
        errorMessage: 'That location does not exist.',
      });
    } else if (!to.permittedMovements.includes('TransferIn')) {
      problems.push({
        propertyName: 'ToLocationId',
        errorCode: 'MovementNotPermittedAtLocation',
        errorMessage: 'Stock cannot be transferred into that location.',
      });
    }
    if (body.lines.length === 0) {
      problems.push({
        propertyName: 'Lines',
        errorCode: 'GoodsReceiptLinesRequired',
        errorMessage: 'A transfer needs at least one line.',
      });
    }
    const stockProblems: Violation[] = [];
    body.lines.forEach((line, index) => {
      const batch = batchFor(branchId, line.batchId, line.skuId);
      if (!batch) {
        problems.push({
          propertyName: `Lines[${index}].BatchId`,
          errorCode: 'BatchNotFound',
          errorMessage: 'That batch does not exist for that SKU.',
        });
        return;
      }
      const sku = skuOf(line.skuId);
      if (!sku.allowsFractionalQuantity && line.quantity.includes('.')) {
        problems.push({
          propertyName: `Lines[${index}].Quantity`,
          errorCode: 'FractionalQuantityNotAllowed',
          errorMessage:
            'The quantity must be greater than zero, whole unless the item is counted in fractions, with at most three decimal places.',
        });
        return;
      }
      const position = positionsOf(batch.id).find((p) => p.locationId === body.fromLocationId);
      const free = (position?.onHand ?? 0) - (position?.reserved ?? 0);
      if (free < parseFloat(line.quantity))
        stockProblems.push(insufficient(index, Math.max(0, free)));
    });
    if (problems.length > 0) return violations(problems);
    if (stockProblems.length > 0) return violations(stockProblems, 409);

    const transferId = newId('71');
    const number = docNumber('TRF');
    for (const line of body.lines) {
      const delta = parseFloat(line.quantity);
      const common = {
        branchId,
        batchId: line.batchId,
        referenceKind: 'StockTransfer',
        referenceId: transferId,
        referenceLabel: number,
        note: body.reason,
      };
      moveStock({ ...common, kind: 'TransferOut', locationId: body.fromLocationId, delta: -delta });
      moveStock({ ...common, kind: 'TransferIn', locationId: body.toLocationId, delta });
    }
    db.transfers.push({
      _branchId: branchId,
      id: transferId,
      number,
      branchId,
      fromLocationId: body.fromLocationId,
      fromLocationCode: from!.code,
      toLocationId: body.toLocationId,
      toLocationCode: to!.code,
      reason: body.reason,
      transferredOn: nowIso(),
      transferredBy: ACTOR_ID,
      lines: body.lines.map((line) => {
        const batch = db.batches.find((b) => b.id === line.batchId)!;
        const sku = skuOf(line.skuId);
        return {
          lineId: newId('b3'),
          skuId: sku.skuId,
          skuCode: sku.skuCode,
          displayName: sku.displayName,
          batchId: batch.id,
          lotNumber: batch.lotNumber,
          quantity: q(parseFloat(line.quantity)),
          stockUnit: sku.stockUnit,
        };
      }),
    });
    persist();
    return HttpResponse.json({ transferId, number }, { status: 201 });
  }),

  // ---- adjustments --------------------------------------------------------------------------
  http.get('*/api/staff/inventory/adjustments', ({ request }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    const status = new URL(request.url).searchParams.get('status');
    const rows = db.adjustments
      .filter((a) => a._branchId === branchOf(request))
      .filter((a) => !status || a.status === status)
      .sort((a, b) => b.number.localeCompare(a.number))
      .map(adjustmentSummary);
    return HttpResponse.json(paged(rows, request));
  }),

  http.get('*/api/staff/inventory/adjustments/:id', ({ request, params }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    const adjustment = db.adjustments.find(
      (a) => a.id === params.id && a._branchId === branchOf(request),
    );
    return adjustment ? HttpResponse.json(adjustmentDetail(adjustment)) : notFoundResponse();
  }),

  http.post('*/api/staff/inventory/adjustments', async ({ request }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const body = (await request.json()) as RequestAdjustmentRequest;
    const branchId = branchOf(request);
    const problems: Violation[] = [];
    if (!body.reason)
      problems.push({
        propertyName: 'Reason',
        errorCode: 'AdjustmentReasonRequired',
        errorMessage: 'Choose a reason.',
      });
    if (!body.note?.trim())
      problems.push({
        propertyName: 'Note',
        errorCode: 'AdjustmentNoteRequired',
        errorMessage: 'A note is required.',
      });
    const location = locationOf(body.locationId);
    if (!location || location.branchId !== branchId || !location.isActive) {
      problems.push({
        propertyName: 'LocationId',
        errorCode: 'StockLocationNotFound',
        errorMessage: 'That location does not exist at this branch.',
      });
    }
    let value = 0;
    const lines: AdjustmentDetail['lines'] = [];
    body.lines.forEach((line, index) => {
      const batch = batchFor(branchId, line.batchId, line.skuId);
      if (!batch) {
        problems.push({
          propertyName: `Lines[${index}].BatchId`,
          errorCode: 'BatchNotFound',
          errorMessage: 'That batch does not exist for that SKU at this branch.',
        });
        return;
      }
      const delta = parseFloat(line.quantityDelta);
      if (!(delta !== 0)) {
        problems.push({
          propertyName: `Lines[${index}].QuantityDelta`,
          errorCode: 'InvalidQuantity',
          errorMessage: 'The quantity cannot be zero.',
        });
        return;
      }
      if ((REMOVES.includes(body.reason) && delta > 0) || (body.reason === 'Found' && delta < 0)) {
        problems.push({
          propertyName: `Lines[${index}].QuantityDelta`,
          errorCode: 'InvalidAdjustmentDirection',
          errorMessage: REMOVES.includes(body.reason)
            ? 'This reason can only remove stock.'
            : 'This reason can only add stock.',
        });
        return;
      }
      const sku = skuOf(line.skuId);
      value += Math.abs(delta) * batch.unitCostBase;
      lines.push({
        lineId: newId('b4'),
        skuId: sku.skuId,
        skuCode: sku.skuCode,
        displayName: sku.displayName,
        batchId: batch.id,
        lotNumber: batch.lotNumber,
        quantityDelta: q(delta),
        stockUnit: sku.stockUnit,
        unitCostBase: { amount: batch.unitCostBase.toFixed(4), currencyCode: MOCK_BASE_CURRENCY },
      });
    });
    if (body.lines.length === 0)
      problems.push({
        propertyName: 'Lines',
        errorCode: 'GoodsReceiptLinesRequired',
        errorMessage: 'An adjustment needs at least one line.',
      });
    if (problems.length > 0) return violations(problems);

    const requiresApproval = value > MOCK_APPROVAL_THRESHOLD;
    const adjustment: (typeof db.adjustments)[number] = {
      _branchId: branchId,
      _requesterId: ACTOR_ID,
      id: newId('81'),
      number: docNumber('ADJ'),
      branchId,
      locationId: body.locationId,
      locationCode: location!.code,
      reason: body.reason,
      note: body.note,
      status: requiresApproval ? 'PendingApproval' : 'Posted',
      valueBase: money(value),
      requestedBy: ACTOR_ID,
      requestedOn: nowIso(),
      decidedBy: null,
      decidedOn: null,
      decisionNote: null,
      lines,
      allowedActions: requiresApproval ? ['approve', 'reject', 'withdraw'] : [],
    };
    if (!requiresApproval) {
      const failures = applyAdjustment(adjustment);
      if (failures.length > 0) return violations(failures, 409);
    }
    db.adjustments.push(adjustment);
    persist();
    return HttpResponse.json(
      {
        adjustmentId: adjustment.id,
        number: adjustment.number,
        status: adjustment.status,
        valueBase: adjustment.valueBase,
        requiresApproval,
      },
      { status: 201 },
    );
  }),

  http.post('*/api/staff/inventory/adjustments/:id/approve', ({ request, params }) => {
    const denied = guard(request, ROLES.approve);
    if (denied) return denied;
    const adjustment = db.adjustments.find(
      (a) => a.id === params.id && a._branchId === branchOf(request),
    );
    if (!adjustment) return notFoundResponse();
    if (adjustment.status !== 'PendingApproval') {
      return violation(
        'AdjustmentId',
        'AdjustmentNotPending',
        'This adjustment is no longer waiting for a decision.',
      );
    }
    if (adjustment._requesterId === ACTOR_ID) {
      return violation(
        'AdjustmentId',
        'ApproverMustDiffer',
        'Someone else has to approve your request.',
      );
    }
    if (adjustment._staleOnApprove) {
      return violation(
        'Quantity',
        'InsufficientStock',
        'That quantity is no longer available — someone may have just taken it. Refresh and try again.',
        409,
      );
    }
    const failures = applyAdjustment(adjustment);
    if (failures.length > 0) return violations(failures, 409);
    adjustment.status = 'Posted';
    adjustment.decidedBy = ACTOR_ID;
    adjustment.decidedOn = nowIso();
    adjustment.allowedActions = [];
    persist();
    return noContentResponse();
  }),

  http.post('*/api/staff/inventory/adjustments/:id/reject', async ({ request, params }) => {
    const denied = guard(request, ROLES.approve);
    if (denied) return denied;
    const adjustment = db.adjustments.find(
      (a) => a.id === params.id && a._branchId === branchOf(request),
    );
    if (!adjustment) return notFoundResponse();
    const { note } = (await request.json()) as { note: string };
    if (!note?.trim())
      return violation('Note', 'AdjustmentNoteRequired', 'Say why you are rejecting this request.');
    if (adjustment.status !== 'PendingApproval') {
      return violation(
        'AdjustmentId',
        'AdjustmentNotPending',
        'This adjustment is no longer waiting for a decision.',
      );
    }
    adjustment.status = 'Rejected';
    adjustment.decidedBy = ACTOR_ID;
    adjustment.decidedOn = nowIso();
    adjustment.decisionNote = note;
    adjustment.allowedActions = [];
    persist();
    return noContentResponse();
  }),

  http.post('*/api/staff/inventory/adjustments/:id/withdraw', ({ request, params }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const adjustment = db.adjustments.find(
      (a) => a.id === params.id && a._branchId === branchOf(request),
    );
    if (!adjustment) return notFoundResponse();
    if (adjustment.status !== 'PendingApproval') {
      return violation(
        'AdjustmentId',
        'AdjustmentNotPending',
        'This adjustment is no longer waiting for a decision.',
      );
    }
    if (adjustment._requesterId !== ACTOR_ID) {
      return violation(
        'AdjustmentId',
        'AdjustmentWithdrawNotRequester',
        'Only the person who requested this can withdraw it.',
      );
    }
    adjustment.status = 'Withdrawn';
    adjustment.allowedActions = [];
    persist();
    return noContentResponse();
  }),

  // ---- counts -------------------------------------------------------------------------------
  http.get('*/api/staff/inventory/counts', ({ request }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    const url = new URL(request.url);
    const status = url.searchParams.get('status');
    const locationId = url.searchParams.get('locationId');
    const rows = db.counts
      .filter((c) => c._branchId === branchOf(request))
      .filter((c) => !status || c.status === status)
      .filter((c) => !locationId || c.locationId === locationId)
      .sort((a, b) => b.number.localeCompare(a.number))
      .map(countSummary);
    return HttpResponse.json(paged(rows, request));
  }),

  http.post('*/api/staff/inventory/counts', async ({ request }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const body = (await request.json()) as {
      locationId: string;
      scope: CountScope;
      skuIds: string[];
    };
    const branchId = branchOf(request);
    const location = locationOf(body.locationId);
    if (!location || location.branchId !== branchId || !location.isActive) {
      return violation(
        'LocationId',
        'StockLocationNotFound',
        'That location does not exist at this branch.',
      );
    }
    if (
      db.counts.some(
        (c) =>
          c.locationId === body.locationId &&
          ['Open', 'Submitted', 'AwaitingApproval'].includes(c.status),
      )
    ) {
      return violation(
        'LocationId',
        'CountLocationBusy',
        'A stock count is already in progress at this location.',
      );
    }
    if (body.scope === 'SelectedSkus' && body.skuIds.length === 0) {
      return violation('SkuIds', 'SkuNotFound', 'Choose at least one item to count.');
    }
    const count: MCount = {
      _branchId: branchId,
      _variance: null,
      id: newId('91'),
      number: docNumber('CNT'),
      branchId,
      locationId: body.locationId,
      locationCode: location.code,
      scope: body.scope,
      skuIds: body.scope === 'SelectedSkus' ? body.skuIds : [],
      status: 'Open',
      openedOn: nowIso(),
      openedBy: ACTOR_ID,
      submittedOn: null,
      varianceValueBase: null,
      isBlind: true,
      lines: [],
      allowedActions: [],
      version: 1,
    };
    db.counts.push(count);
    persist();
    return HttpResponse.json({ countId: count.id, number: count.number }, { status: 201 });
  }),

  http.get('*/api/staff/inventory/counts/:id/variance', ({ request, params }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    const count = db.counts.find((c) => c.id === params.id && c._branchId === branchOf(request));
    if (!count || !count._variance) return notFoundResponse();
    return HttpResponse.json({ ...count._variance, status: count.status } satisfies CountVariance);
  }),

  http.get('*/api/staff/inventory/counts/:id', ({ request, params }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    const count = db.counts.find((c) => c.id === params.id && c._branchId === branchOf(request));
    return count ? HttpResponse.json(countDetail(count)) : notFoundResponse();
  }),

  http.put('*/api/staff/inventory/counts/:id/lines', async ({ request, params }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const count = db.counts.find((c) => c.id === params.id && c._branchId === branchOf(request));
    if (!count) return notFoundResponse();
    if (count.status !== 'Open')
      return violation('CountId', 'InvalidCountTransition', 'This count is no longer open.');
    const { lines } = (await request.json()) as {
      lines: Array<{ skuId: string; batchId: string; countedQuantity: string }>;
    };
    const problems: Violation[] = [];
    lines.forEach((line, index) => {
      const batch = batchFor(count._branchId, line.batchId, line.skuId);
      if (!batch) {
        problems.push({
          propertyName: `Lines[${index}].BatchId`,
          errorCode: 'BatchNotFound',
          errorMessage: 'That batch does not exist for that SKU.',
        });
      } else if (count.scope === 'SelectedSkus' && !count.skuIds.includes(line.skuId)) {
        problems.push({
          propertyName: `Lines[${index}].SkuId`,
          errorCode: 'CountLineOutsideScope',
          errorMessage: 'That item is not part of this count.',
        });
      } else if (line.countedQuantity.trim() === '' || parseFloat(line.countedQuantity) < 0) {
        problems.push({
          propertyName: `Lines[${index}].CountedQuantity`,
          errorCode: 'InvalidQuantity',
          errorMessage: 'A counted quantity cannot be negative.',
        });
      }
    });
    if (problems.length > 0) return violations(problems);
    for (const line of lines) {
      const batch = db.batches.find((b) => b.id === line.batchId)!;
      const sku = skuOf(line.skuId);
      const existing = count.lines.find(
        (l) => l.batchId === line.batchId && l.skuId === line.skuId,
      );
      const next: CountLine = {
        skuId: sku.skuId,
        skuCode: sku.skuCode,
        displayName: sku.displayName,
        batchId: batch.id,
        lotNumber: batch.lotNumber,
        expiresOn: batch.expiresOn,
        stockUnit: sku.stockUnit,
        countedQuantity: q(parseFloat(line.countedQuantity)),
        bookQuantity: null,
        variance: null,
      };
      if (existing) Object.assign(existing, next);
      else count.lines.push(next);
    }
    count.version += 1;
    persist();
    return noContentResponse();
  }),

  http.post('*/api/staff/inventory/counts/:id/submit', ({ request, params }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const count = db.counts.find((c) => c.id === params.id && c._branchId === branchOf(request));
    if (!count) return notFoundResponse();
    if (count.status !== 'Open')
      return violation(
        'CountId',
        'InvalidCountTransition',
        'This count has already been submitted.',
      );
    const lines = [...count.lines];
    if (count.scope === 'FullLocation') {
      for (const position of db.positions.filter(
        (p) => p.locationId === count.locationId && p.onHand > 0,
      )) {
        if (lines.some((l) => l.batchId === position.batchId)) continue;
        const batch = db.batches.find((b) => b.id === position.batchId)!;
        const sku = skuOf(batch.skuId);
        lines.push({
          skuId: sku.skuId,
          skuCode: sku.skuCode,
          displayName: sku.displayName,
          batchId: batch.id,
          lotNumber: batch.lotNumber,
          expiresOn: batch.expiresOn,
          stockUnit: sku.stockUnit,
          countedQuantity: '0',
          bookQuantity: null,
          variance: null,
        });
      }
    }
    let total = 0;
    let varianceLines = 0;
    const variance: CountVariance['lines'] = [];
    for (const line of lines) {
      const batch = db.batches.find((b) => b.id === line.batchId)!;
      const book =
        positionsOf(line.batchId).find((p) => p.locationId === count.locationId)?.onHand ?? 0;
      const counted = parseFloat(line.countedQuantity);
      const diff = Math.round((counted - book) * 1000) / 1000;
      line.bookQuantity = q(book);
      line.variance = q(diff);
      if (diff !== 0) {
        varianceLines += 1;
        total += diff * batch.unitCostBase;
        variance.push({
          skuId: line.skuId,
          skuCode: line.skuCode,
          displayName: line.displayName,
          batchId: line.batchId,
          lotNumber: line.lotNumber,
          stockUnit: line.stockUnit,
          bookQuantity: q(book),
          countedQuantity: q(counted),
          variance: q(diff),
          value: money(diff * batch.unitCostBase),
        });
      }
    }
    count.lines = lines;
    count.status = 'Submitted';
    count.submittedOn = nowIso();
    count.varianceValueBase = money(total);
    count._variance = {
      countId: count.id,
      number: count.number,
      status: 'Submitted',
      totalVarianceValueBase: money(total),
      varianceLineCount: varianceLines,
      willNeedApproval: Math.abs(total) > MOCK_APPROVAL_THRESHOLD,
      lines: variance,
    };
    count.version += 1;
    persist();
    return noContentResponse();
  }),

  http.post('*/api/staff/inventory/counts/:id/commit', ({ request, params }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const count = db.counts.find((c) => c.id === params.id && c._branchId === branchOf(request));
    if (!count) return notFoundResponse();
    if (count.status !== 'Submitted' || !count._variance) {
      return violation(
        'CountId',
        'InvalidCountTransition',
        'Only a submitted count can be committed.',
      );
    }
    if (count._variance.willNeedApproval) count.status = 'AwaitingApproval';
    else commitCountToLedger(count);
    count.version += 1;
    persist();
    return HttpResponse.json({ status: count.status });
  }),

  http.post('*/api/staff/inventory/counts/:id/cancel', ({ request, params }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const count = db.counts.find((c) => c.id === params.id && c._branchId === branchOf(request));
    if (!count) return notFoundResponse();
    if (count.status !== 'Open' && count.status !== 'Submitted') {
      return violation(
        'CountId',
        'InvalidCountTransition',
        'This count can no longer be cancelled.',
      );
    }
    count.status = 'Cancelled';
    persist();
    return noContentResponse();
  }),

  http.post('*/api/staff/inventory/counts/:id/approve', ({ request, params }) => {
    const denied = guard(request, ROLES.approve);
    if (denied) return denied;
    const count = db.counts.find((c) => c.id === params.id && c._branchId === branchOf(request));
    if (!count) return notFoundResponse();
    if (count.status !== 'AwaitingApproval') {
      return violation(
        'CountId',
        'InvalidCountTransition',
        'This count is not waiting for approval.',
      );
    }
    if (count.openedBy === ACTOR_ID) {
      return violation(
        'CountId',
        'ApproverMustDiffer',
        'Someone else has to approve your request.',
      );
    }
    commitCountToLedger(count);
    persist();
    return noContentResponse();
  }),

  http.post('*/api/staff/inventory/counts/:id/reject', ({ request, params }) => {
    const denied = guard(request, ROLES.approve);
    if (denied) return denied;
    const count = db.counts.find((c) => c.id === params.id && c._branchId === branchOf(request));
    if (!count) return notFoundResponse();
    if (count.status !== 'AwaitingApproval') {
      return violation(
        'CountId',
        'InvalidCountTransition',
        'This count is not waiting for approval.',
      );
    }
    count.status = 'Cancelled';
    persist();
    return noContentResponse();
  }),
];
