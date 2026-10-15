import { http, HttpResponse, type HttpHandler } from 'msw';
import type { BreachDetail, BreachSummary } from '@/lib/api/inventory-breaches';
import type {
  RecallDetail,
  RecallSummary,
  RecallTrace,
  RecallTraceCustomer,
} from '@/lib/api/inventory-recalls';
import {
  ROLES,
  branchOf,
  guard,
  noContentResponse,
  notFoundResponse,
  nowIso,
  omit,
  paged,
  q,
  violation,
  violations,
  type Violation,
} from './fixtures';
import { ACTOR_ID, db, docNumber, newId, persist, type MBatch } from './store';
import { locationOf, moveStock, onHandOfBatch, positionsOf, skuOf } from './stock';

/** `mocks/handlers/inventory/recalls.ts` — `StaffRecallsController` and
 *  `StaffStorageBreachesController`. A recall applies to the batch at EVERY location and branch. */

function heldOf(batch: MBatch) {
  return positionsOf(batch.id)
    .filter((p) => p.onHand > 0)
    .map((p) => {
      const location = locationOf(p.locationId)!;
      return {
        locationId: p.locationId,
        locationCode: location.code,
        branchId: location.branchId,
        onHand: q(p.onHand),
      };
    });
}

function recallSummary(batch: MBatch): RecallSummary {
  const sku = skuOf(batch.skuId);
  const recall = batch.recall!;
  return {
    batchId: batch.id,
    recallNumber: recall.recallNumber,
    status: batch.recallStatus ?? 'Active',
    skuId: sku.skuId,
    skuCode: sku.skuCode,
    displayName: sku.displayName,
    lotNumber: batch.lotNumber,
    expiresOn: batch.expiresOn,
    declaredOn: recall.declaredOn,
    notifiedRecipientCount: recall.notifiedRecipientCount,
    notificationRuns: recall.notificationRuns,
    heldQuantity: q(onHandOfBatch(batch.id)),
  };
}

const TRACE_CUSTOMERS: RecallTraceCustomer[] = [
  {
    customerId: 'c2000000-0000-4000-8000-000000000001',
    name: 'Nusrat Jahan',
    mobileNumber: '01711000001',
    email: 'nusrat@example.com',
    isAnonymised: false,
    originalCustomerId: null,
  },
  {
    customerId: 'c2000000-0000-4000-8000-000000000002',
    name: 'Karim Uddin',
    mobileNumber: '01711000002',
    email: 'karim@example.com',
    isAnonymised: false,
    originalCustomerId: null,
  },
  {
    customerId: 'c2000000-0000-4000-8000-000000000003',
    name: 'Anika Rahman',
    mobileNumber: '01711000003',
    email: null,
    isAnonymised: false,
    originalCustomerId: null,
  },
];

/** A trace for a recall declared in this session: one purchase, one dispense, one boarding feed,
 *  one unshipped reservation and some unidentified counter sales (US-IMP-11 AC2). */
function buildTrace(batch: MBatch): RecallTrace {
  const sku = skuOf(batch.skuId);
  const [a, b, c] = TRACE_CUSTOMERS as [
    RecallTraceCustomer,
    RecallTraceCustomer,
    RecallTraceCustomer,
  ];
  const row = (kind: string, customer: RecallTraceCustomer, label: string, hoursAgo: number) => ({
    occurredOn: new Date(Date.now() - hoursAgo * 3_600_000).toISOString(),
    kind,
    quantity: '1',
    locationId: positionsOf(batch.id)[0]?.locationId ?? '',
    locationCode: locationOf(positionsOf(batch.id)[0]?.locationId ?? '')?.code ?? null,
    referenceLabel: label,
    customer,
    cat: {
      catId: `c3-${customer.customerId.slice(-3)}`,
      name: `${customer.name.split(' ')[0]}'s cat`,
      currentOwner: customer,
    },
    householdCats: [] as string[],
  });
  return {
    batchId: batch.id,
    recallNumber: batch.recall!.recallNumber,
    skuCode: sku.skuCode,
    displayName: sku.displayName,
    lotNumber: batch.lotNumber,
    batchStatus: batch.status,
    received: [
      row('Sale', a, 'Order PC-001301', 72),
      row('Dispense', b, 'RX-2026-000071', 48),
      row('BoardingUse', c, 'Stay BR-2026-000021', 24),
    ],
    returned: [],
    unshippedDispatchBlocked: [
      {
        referenceKind: 'CustomerOrder',
        referenceId: 'a3000000-0000-4000-8000-0000000000aa',
        quantity: '2',
        customer: a,
        cat: { catId: 'c3-unshipped', name: 'Mishti', currentOwner: a },
      },
    ],
    unidentified: { count: 2, quantity: '3' },
    stockHeld: heldOf(batch),
  };
}

function traceOf(batch: MBatch): RecallTrace {
  return db.recallTraces[batch.id] ?? buildTrace(batch);
}

function distinctCustomerCount(trace: RecallTrace): number {
  const ids = new Set<string>();
  for (const r of trace.received)
    if (r.customer && !r.customer.isAnonymised) ids.add(r.customer.customerId);
  for (const r of trace.returned)
    if (r.customer && !r.customer.isAnonymised) ids.add(r.customer.customerId);
  for (const r of trace.unshippedDispatchBlocked)
    if (r.customer && !r.customer.isAnonymised) ids.add(r.customer.customerId);
  return ids.size;
}

function findBatch(batchId: unknown): MBatch | undefined {
  return db.batches.find((b) => b.id === batchId);
}

// ---- breaches ---------------------------------------------------------------------------------

type MBreachRecord = (typeof db.breaches)[number];

function breachSummary(b: MBreachRecord): BreachSummary {
  return b.summary;
}

function breachDetail(b: MBreachRecord): BreachDetail {
  return omit(b, 'branchId');
}

export const recallHandlers: HttpHandler[] = [
  http.get('*/api/staff/inventory/recalls', ({ request }) => {
    const denied = guard(request, ROLES.recalls);
    if (denied) return denied;
    const status = new URL(request.url).searchParams.get('status');
    const rows = db.batches
      .filter((b) => b.recall !== null)
      .map(recallSummary)
      .filter((r) => !status || r.status === status)
      .sort((a, b) => b.declaredOn.localeCompare(a.declaredOn));
    return HttpResponse.json(paged(rows, request));
  }),

  http.get('*/api/staff/inventory/recalls/:batchId/trace', ({ request, params }) => {
    const denied = guard(request, ROLES.recalls);
    if (denied) return denied;
    const batch = findBatch(params.batchId);
    if (!batch?.recall) return notFoundResponse();
    return HttpResponse.json(traceOf(batch));
  }),

  http.get('*/api/staff/inventory/recalls/:batchId', ({ request, params }) => {
    const denied = guard(request, ROLES.recalls);
    if (denied) return denied;
    const batch = findBatch(params.batchId);
    if (!batch?.recall) return notFoundResponse();
    const detail: RecallDetail = {
      summary: recallSummary(batch),
      recall: batch.recall,
      held: heldOf(batch),
    };
    return HttpResponse.json(detail);
  }),

  http.post('*/api/staff/inventory/batches/:batchId/recall', async ({ request, params }) => {
    const denied = guard(request, ROLES.recalls);
    if (denied) return denied;
    const batch = findBatch(params.batchId);
    if (!batch) return notFoundResponse();
    const body = (await request.json()) as {
      confirmLotNumber: string;
      reason: string;
      customerAdvice: string;
    };
    const problems: Violation[] = [];
    if (body.confirmLotNumber !== batch.lotNumber) {
      problems.push({
        propertyName: 'ConfirmLotNumber',
        errorCode: 'RecallConfirmationMismatch',
        errorMessage: "The lot number you typed doesn't match.",
      });
    }
    if (!body.reason?.trim())
      problems.push({
        propertyName: 'Reason',
        errorCode: 'RecallNotActive',
        errorMessage: 'Give the reason for the recall.',
      });
    if (!body.customerAdvice?.trim())
      problems.push({
        propertyName: 'CustomerAdvice',
        errorCode: 'RecallNotActive',
        errorMessage: 'Write the advice customers will receive.',
      });
    if (problems.length > 0) return violations(problems);
    if (batch.status === 'Recalled' || batch.status === 'WrittenOff') {
      return violation(
        'BatchId',
        'InvalidBatchTransition',
        `A ${batch.status} batch cannot be recalled.`,
      );
    }
    const recallNumber = docNumber('RCL');
    batch.statusHistory.push({
      from: batch.status,
      to: 'Recalled',
      reason: `Recall ${recallNumber}`,
      changedOn: nowIso(),
      changedBy: ACTOR_ID,
    });
    batch.status = 'Recalled';
    batch.statusReason = 'Recalled';
    batch.recallStatus = 'Active';
    batch.recall = {
      recallNumber,
      reason: body.reason,
      customerAdvice: body.customerAdvice,
      declaredOn: nowIso(),
      declaredBy: ACTOR_ID,
      notifiedOn: null,
      notifiedBy: null,
      notifiedRecipientCount: 0,
      notificationRuns: 0,
      closedOn: null,
      closingNote: null,
    };
    batch.version += 1;
    persist();
    return HttpResponse.json({ recallNumber }, { status: 201 });
  }),

  http.post('*/api/staff/inventory/recalls/:batchId/notify', ({ request, params }) => {
    const denied = guard(request, ROLES.recalls);
    if (denied) return denied;
    const batch = findBatch(params.batchId);
    if (!batch?.recall) return notFoundResponse();
    if (batch.recallStatus !== 'Active')
      return violation('BatchId', 'RecallNotActive', 'This recall is no longer active.');
    const total = distinctCustomerCount(traceOf(batch));
    const already = batch.recall.notifiedRecipientCount;
    const requested = Math.max(0, total - already);
    batch.recall.notifiedRecipientCount = already + requested;
    batch.recall.notificationRuns += 1;
    batch.recall.notifiedOn = nowIso();
    batch.recall.notifiedBy = ACTOR_ID;
    persist();
    return HttpResponse.json({ requested, alreadyNotified: already, remaining: 0 });
  }),

  http.post('*/api/staff/inventory/recalls/:batchId/withdraw', async ({ request, params }) => {
    const denied = guard(request, ROLES.recalls);
    if (denied) return denied;
    const batch = findBatch(params.batchId);
    if (!batch?.recall) return notFoundResponse();
    const { reason } = (await request.json()) as { reason: string };
    if (batch.recallStatus !== 'Active')
      return violation('BatchId', 'RecallNotActive', 'This recall is no longer active.');
    batch.statusHistory.push({
      from: 'Recalled',
      to: 'Quarantined',
      reason,
      changedOn: nowIso(),
      changedBy: ACTOR_ID,
    });
    batch.status = 'Quarantined';
    batch.statusReason = `Recall withdrawn: ${reason}`;
    batch.recallStatus = 'Withdrawn';
    batch.recall.closedOn = nowIso();
    batch.recall.closingNote = `Withdrawn: ${reason}`;
    batch.version += 1;
    persist();
    return noContentResponse();
  }),

  http.post('*/api/staff/inventory/recalls/:batchId/close', async ({ request, params }) => {
    const denied = guard(request, ROLES.recalls);
    if (denied) return denied;
    const batch = findBatch(params.batchId);
    if (!batch?.recall) return notFoundResponse();
    const { note } = (await request.json()) as { note: string };
    if (batch.recallStatus !== 'Active')
      return violation('BatchId', 'RecallNotActive', 'This recall is no longer active.');
    const held = onHandOfBatch(batch.id);
    if (held > 0) {
      const sku = skuOf(batch.skuId);
      return violation(
        'BatchId',
        'RecallStockStillHeld',
        `Write off or return the remaining ${q(held)} ${sku.stockUnit} first.`,
      );
    }
    batch.recallStatus = 'Closed';
    batch.recall.closedOn = nowIso();
    batch.recall.closingNote = note;
    persist();
    return noContentResponse();
  }),

  // ---- storage breaches ---------------------------------------------------------------------
  http.get('*/api/staff/inventory/storage-breaches', ({ request }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    const status = new URL(request.url).searchParams.get('status');
    const rows = db.breaches
      .filter((b) => b.branchId === branchOf(request))
      .map(breachSummary)
      .filter((b) => !status || b.status === status)
      .sort((a, b) => b.reportedOn.localeCompare(a.reportedOn));
    return HttpResponse.json(paged(rows, request));
  }),

  http.post('*/api/staff/inventory/storage-breaches', async ({ request }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    const body = (await request.json()) as {
      locationId: string;
      observedOn: string;
      condition: string;
      temperatureC: string | null;
      durationMinutes: number | null;
    };
    const branchId = branchOf(request);
    const location = locationOf(body.locationId);
    const problems: Violation[] = [];
    if (!location || location.branchId !== branchId) {
      problems.push({
        propertyName: 'LocationId',
        errorCode: 'StockLocationNotFound',
        errorMessage: 'That location does not exist at this branch.',
      });
    }
    if (new Date(body.observedOn).getTime() > Date.now() + 60_000) {
      problems.push({
        propertyName: 'ObservedOn',
        errorCode: 'InvalidDateRange',
        errorMessage: 'The time it was observed cannot be in the future.',
      });
    }
    if (!body.condition?.trim()) {
      problems.push({
        propertyName: 'Condition',
        errorCode: 'InvalidStorageBreachTransition',
        errorMessage: 'Say what happened.',
      });
    }
    if (problems.length > 0) return violations(problems);
    const affected = db.batches
      .filter((b) => b.branchId === branchId && b.status !== 'WrittenOff')
      .filter((b) =>
        positionsOf(b.id).some((p) => p.locationId === body.locationId && p.onHand > 0),
      );
    const breachId = newId('b1');
    db.breaches.push({
      branchId,
      summary: {
        id: breachId,
        locationId: body.locationId,
        locationCode: location!.code,
        observedOn: body.observedOn,
        condition: body.condition,
        status: 'Open',
        affectedBatchCount: affected.length,
        reportedBy: ACTOR_ID,
        reportedOn: nowIso(),
      },
      temperatureC: body.temperatureC ? parseFloat(body.temperatureC) : null,
      durationMinutes: body.durationMinutes,
      affectedBatches: affected.map((b) => {
        const sku = skuOf(b.skuId);
        const here = positionsOf(b.id).find((p) => p.locationId === body.locationId)!;
        return {
          batchId: b.id,
          skuCode: sku.skuCode,
          displayName: sku.displayName,
          lotNumber: b.lotNumber,
          heldAtLocation: q(here.onHand),
        };
      }),
      quarantinedOn: null,
      quarantinedBy: null,
      closedOn: null,
      closedBy: null,
      closingNote: null,
      allowedActions: ['quarantine', 'close'],
    });
    db.alerts.push({
      id: newId('a2'),
      branchId,
      kind: 'StorageBreach',
      status: 'Open',
      skuId: null,
      skuCode: null,
      displayName: null,
      batchId: null,
      lotNumber: null,
      locationId: body.locationId,
      locationCode: location!.code,
      thresholdDays: null,
      quantityAtRaise: null,
      raisedOn: nowIso(),
      acknowledgedBy: null,
      acknowledgedOn: null,
      resolvedOn: null,
      resolutionNote: null,
    });
    persist();
    return HttpResponse.json({ breachId, affectedBatchCount: affected.length }, { status: 201 });
  }),

  http.get('*/api/staff/inventory/storage-breaches/:id', ({ request, params }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    const breach = db.breaches.find(
      (b) => b.summary.id === params.id && b.branchId === branchOf(request),
    );
    return breach ? HttpResponse.json(breachDetail(breach)) : notFoundResponse();
  }),

  http.post('*/api/staff/inventory/storage-breaches/:id/quarantine', ({ request, params }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const breach = db.breaches.find(
      (b) => b.summary.id === params.id && b.branchId === branchOf(request),
    );
    if (!breach) return notFoundResponse();
    if (breach.summary.status !== 'Open') {
      return violation(
        'BreachId',
        'InvalidStorageBreachTransition',
        'The stock for this report has already been dealt with.',
      );
    }
    const destination = db.locations.find(
      (l) => l.branchId === breach.branchId && l.kind === 'Quarantine' && l.isActive,
    );
    if (!destination)
      return violation(
        'BreachId',
        'StockLocationNotFound',
        'This branch has no quarantine location.',
      );
    for (const affected of breach.affectedBatches) {
      const position = positionsOf(affected.batchId).find(
        (p) => p.locationId === breach.summary.locationId,
      );
      if (!position || position.onHand <= 0) continue;
      const amount = position.onHand;
      const common = {
        branchId: breach.branchId,
        batchId: affected.batchId,
        referenceKind: 'StorageBreach',
        referenceId: breach.summary.id,
        referenceLabel: 'Storage breach',
      };
      moveStock({
        ...common,
        kind: 'TransferOut',
        locationId: breach.summary.locationId,
        delta: -amount,
      });
      moveStock({ ...common, kind: 'TransferIn', locationId: destination.id, delta: amount });
    }
    breach.summary.status = 'Quarantined';
    breach.quarantinedOn = nowIso();
    breach.quarantinedBy = ACTOR_ID;
    breach.allowedActions = ['close'];
    persist();
    return noContentResponse();
  }),

  http.post('*/api/staff/inventory/storage-breaches/:id/close', async ({ request, params }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const breach = db.breaches.find(
      (b) => b.summary.id === params.id && b.branchId === branchOf(request),
    );
    if (!breach) return notFoundResponse();
    const { note } = (await request.json()) as { note: string };
    if (breach.summary.status === 'Closed') {
      return violation(
        'BreachId',
        'InvalidStorageBreachTransition',
        'This report is already closed.',
      );
    }
    breach.summary.status = 'Closed';
    breach.closedOn = nowIso();
    breach.closedBy = ACTOR_ID;
    breach.closingNote = note;
    breach.allowedActions = [];
    persist();
    return noContentResponse();
  }),
];
