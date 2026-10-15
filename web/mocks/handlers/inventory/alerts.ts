import { http, HttpResponse, type HttpHandler } from 'msw';
import type { AlertKind, AlertSummary, InventoryAlert } from '@/lib/api/inventory-alerts';
import type { ReorderPolicy, ReorderSuggestion } from '@/lib/api/procurement-reorder';
import type { NearExpiryReport, StockOnHandReport } from '@/lib/api/inventory-reports';
import {
  ROLES,
  branchOf,
  guard,
  money,
  noContentResponse,
  omit,
  notFoundResponse,
  paged,
  q,
  todayDate,
  unitCost,
  violation,
} from './fixtures';
import { db, persist } from './store';
import {
  branchBatches,
  daysToExpiry,
  effectiveReorderPoint,
  isExpired,
  locationOf,
  positionsOf,
  skuOf,
} from './stock';

/** `mocks/handlers/inventory/alerts.ts` — `StaffAlertsController`, `StaffInventoryReportsController`
 *  and `StaffReorderPoliciesController`. */

const LARGEST_THRESHOLD_DAYS = 90;

function publicAlert(alert: (typeof db.alerts)[number]): InventoryAlert {
  return omit(alert, 'branchId');
}

function usableAt(branchId: string, skuId: string, locationId: string): number {
  return branchBatches(branchId)
    .filter((b) => b.skuId === skuId && b.status === 'Available' && !isExpired(b))
    .reduce((sum, b) => {
      const p = positionsOf(b.id).find((pos) => pos.locationId === locationId);
      return sum + (p ? p.onHand - p.reserved : 0);
    }, 0);
}

function onOrder(branchId: string, skuId: string): number {
  return db.purchaseOrders
    .filter((po) => po.branchId === branchId && po.status === 'Issued')
    .flatMap((po) => po.lines)
    .filter((l) => l.skuId === skuId)
    .reduce((sum, l) => sum + Math.max(0, l.orderedQuantity - l.acceptedQuantity), 0);
}

function policyPairs(branchId: string): Array<{ skuId: string; locationId: string }> {
  const pairs = new Map<string, { skuId: string; locationId: string }>();
  for (const policy of db.reorderPolicies.filter((p) => p.branchId === branchId)) {
    pairs.set(`${policy.skuId}|${policy.locationId}`, {
      skuId: policy.skuId,
      locationId: policy.locationId,
    });
  }
  for (const batch of branchBatches(branchId)) {
    for (const position of positionsOf(batch.id)) {
      if (position.onHand > 0)
        pairs.set(`${batch.skuId}|${position.locationId}`, {
          skuId: batch.skuId,
          locationId: position.locationId,
        });
    }
  }
  return [...pairs.values()];
}

export const alertHandlers: HttpHandler[] = [
  http.get('*/api/staff/inventory/alerts/summary', ({ request }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    const open = db.alerts.filter((a) => a.branchId === branchOf(request) && a.status === 'Open');
    const byKind: Partial<Record<AlertKind, number>> = {};
    for (const alert of open) byKind[alert.kind] = (byKind[alert.kind] ?? 0) + 1;
    const body: AlertSummary = { open: { total: open.length, byKind } };
    return HttpResponse.json(body);
  }),

  http.get('*/api/staff/inventory/alerts', ({ request }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    const url = new URL(request.url);
    const status = url.searchParams.get('status');
    const kind = url.searchParams.get('kind');
    const locationId = url.searchParams.get('locationId');
    const rows = db.alerts
      .filter((a) => a.branchId === branchOf(request))
      .filter((a) => !status || a.status === status)
      .filter((a) => !kind || a.kind === kind)
      .filter((a) => !locationId || a.locationId === locationId)
      .sort((a, b) => b.raisedOn.localeCompare(a.raisedOn))
      .map(publicAlert);
    return HttpResponse.json(paged(rows, request));
  }),

  http.post('*/api/staff/inventory/alerts/:id/acknowledge', async ({ request, params }) => {
    const denied = guard(request, ROLES.alertAck);
    if (denied) return denied;
    const alert = db.alerts.find((a) => a.id === params.id && a.branchId === branchOf(request));
    if (!alert) return notFoundResponse();
    if (alert.status !== 'Open')
      return violation('AlertId', 'StockAlertNotOpen', 'This alert is no longer open.');
    alert.status = 'Acknowledged';
    alert.acknowledgedOn = new Date().toISOString();
    alert.acknowledgedBy = 's0000000-0000-0000-0000-000000000001';
    persist();
    return noContentResponse();
  }),

  // ---- reports ------------------------------------------------------------------------------
  http.get('*/api/staff/inventory/reports/near-expiry', ({ request }) => {
    const denied = guard(request, ROLES.reports);
    if (denied) return denied;
    const url = new URL(request.url);
    const within = parseInt(
      url.searchParams.get('withinDays') ?? String(LARGEST_THRESHOLD_DAYS),
      10,
    );
    const includeExpired = url.searchParams.get('includeExpired') === 'true';
    if (within < 0 || within > 3650)
      return violation('withinDays', 'InvalidQuery', 'withinDays must be between 0 and 3650.');
    let total = 0;
    const rows: NearExpiryReport['rows'] = [];
    for (const batch of branchBatches(branchOf(request))) {
      if (!batch.expiresOn || batch.status === 'WrittenOff') continue;
      const days = daysToExpiry(batch)!;
      if (days > within || (isExpired(batch) && !includeExpired)) continue;
      const quantity = positionsOf(batch.id).reduce((s, p) => s + p.onHand, 0);
      if (quantity <= 0) continue;
      const sku = skuOf(batch.skuId);
      const value = quantity * batch.unitCostBase;
      total += value;
      rows.push({
        skuId: sku.skuId,
        skuCode: sku.skuCode,
        displayName: sku.displayName,
        batchId: batch.id,
        lotNumber: batch.lotNumber,
        expiresOn: batch.expiresOn,
        daysRemaining: days,
        isExpired: isExpired(batch),
        status: batch.status,
        quantity: q(quantity),
        locations: positionsOf(batch.id)
          .filter((p) => p.onHand > 0)
          .map((p) => ({
            locationId: p.locationId,
            locationCode: locationOf(p.locationId)?.code ?? null,
            onHand: q(p.onHand),
          })),
        unitCostBase: unitCost(batch.unitCostBase),
        costBasis: batch.costBasis,
        valueAtRisk: money(value),
      });
    }
    rows.sort((a, b) => a.daysRemaining - b.daysRemaining);
    const report: NearExpiryReport = {
      asOf: todayDate(),
      withinDays: within,
      includeExpired,
      totalValueAtRisk: money(total),
      rows,
    };
    return HttpResponse.json(report);
  }),

  http.get('*/api/staff/inventory/reports/stock-on-hand', ({ request }) => {
    const denied = guard(request, ROLES.reports);
    if (denied) return denied;
    const url = new URL(request.url);
    const locationId = url.searchParams.get('locationId');
    const cls = url.searchParams.get('class');
    let total = 0;
    const rows: StockOnHandReport['rows'] = [];
    for (const batch of branchBatches(branchOf(request))) {
      const sku = skuOf(batch.skuId);
      if (cls && sku.stockClass !== cls) continue;
      for (const position of positionsOf(batch.id)) {
        if (position.onHand <= 0 || (locationId && position.locationId !== locationId)) continue;
        const value = position.onHand * batch.unitCostBase;
        total += value;
        rows.push({
          skuId: sku.skuId,
          skuCode: sku.skuCode,
          displayName: sku.displayName,
          stockClass: sku.stockClass,
          batchId: batch.id,
          lotNumber: batch.lotNumber,
          expiresOn: batch.expiresOn,
          batchStatus: batch.status,
          locationId: position.locationId,
          locationCode: locationOf(position.locationId)?.code ?? null,
          onHand: q(position.onHand),
          reserved: q(position.reserved),
          unitCostBase: unitCost(batch.unitCostBase),
          costBasis: batch.costBasis,
          value: money(value),
        });
      }
    }
    return HttpResponse.json({
      asOf: todayDate(),
      totalValue: money(total),
      rows,
    } satisfies StockOnHandReport);
  }),

  http.get('*/api/staff/inventory/reports/reconciliation', ({ request }) => {
    const denied = guard(request, ROLES.reports);
    if (denied) return denied;
    return HttpResponse.json({ items: [] });
  }),

  // ---- reorder ------------------------------------------------------------------------------
  http.get('*/api/staff/inventory/reorder-policies', ({ request }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    const branchId = branchOf(request);
    const locationId = new URL(request.url).searchParams.get('locationId');
    const rows: ReorderPolicy[] = policyPairs(branchId)
      .filter((pair) => !locationId || pair.locationId === locationId)
      .map((pair) => {
        const policy = db.reorderPolicies.find(
          (p) =>
            p.branchId === branchId && p.skuId === pair.skuId && p.locationId === pair.locationId,
        );
        const sku = skuOf(pair.skuId);
        const supplier = db.suppliers.find((s) => s.id === policy?.preferredSupplierId);
        return {
          skuId: sku.skuId,
          skuCode: sku.skuCode,
          displayName: sku.displayName,
          locationId: pair.locationId,
          locationCode: locationOf(pair.locationId)?.code ?? null,
          reorderPoint: q(
            policy?.reorderPoint ?? effectiveReorderPoint(branchId, pair.skuId, pair.locationId),
          ),
          isDefaulted: policy ? policy.isDefaulted : true,
          reorderQuantity:
            policy?.reorderQuantity === null || policy === undefined
              ? null
              : q(policy.reorderQuantity),
          preferredSupplierId: policy?.preferredSupplierId ?? null,
          preferredSupplierName: supplier?.name ?? null,
        };
      })
      .sort(
        (a, b) =>
          a.displayName.localeCompare(b.displayName) ||
          (a.locationCode ?? '').localeCompare(b.locationCode ?? ''),
      );
    return HttpResponse.json(paged(rows, request));
  }),

  http.put(
    '*/api/staff/inventory/reorder-policies/:skuId/:locationId',
    async ({ request, params }) => {
      const denied = guard(request, ROLES.stockManage);
      if (denied) return denied;
      const body = (await request.json()) as {
        reorderPoint: string;
        reorderQuantity: string | null;
        preferredSupplierId: string | null;
      };
      if (!(parseFloat(body.reorderPoint) >= 0)) {
        return violation(
          'ReorderPoint',
          'InvalidReorderPolicy',
          'The reorder point cannot be negative.',
        );
      }
      const branchId = branchOf(request);
      const existing = db.reorderPolicies.find(
        (p) =>
          p.branchId === branchId && p.skuId === params.skuId && p.locationId === params.locationId,
      );
      const next = {
        branchId,
        skuId: String(params.skuId),
        locationId: String(params.locationId),
        reorderPoint: parseFloat(body.reorderPoint),
        reorderQuantity: body.reorderQuantity ? parseFloat(body.reorderQuantity) : null,
        preferredSupplierId: body.preferredSupplierId,
        isDefaulted: false,
      };
      if (existing) Object.assign(existing, next);
      else db.reorderPolicies.push(next);
      persist();
      return noContentResponse();
    },
  ),

  http.get('*/api/staff/procurement/reorder-suggestions', ({ request }) => {
    const denied = guard(request, ROLES.procurementView);
    if (denied) return denied;
    const url = new URL(request.url);
    const locationId = url.searchParams.get('locationId');
    const supplierId = url.searchParams.get('supplierId');
    const branchId = branchOf(request);
    const items: ReorderSuggestion[] = [];
    for (const policy of db.reorderPolicies.filter((p) => p.branchId === branchId)) {
      if (locationId && policy.locationId !== locationId) continue;
      if (supplierId && policy.preferredSupplierId !== supplierId) continue;
      const usable = usableAt(branchId, policy.skuId, policy.locationId);
      const incoming = onOrder(branchId, policy.skuId);
      if (usable + incoming >= policy.reorderPoint) continue;
      const sku = skuOf(policy.skuId);
      const supplier = db.suppliers.find((s) => s.id === policy.preferredSupplierId);
      const suggested =
        policy.reorderQuantity ?? Math.ceil(policy.reorderPoint * 2 - usable - incoming);
      items.push({
        skuId: sku.skuId,
        skuCode: sku.skuCode,
        displayName: sku.displayName,
        stockUnit: sku.stockUnit,
        locationId: policy.locationId,
        locationCode: locationOf(policy.locationId)?.code ?? null,
        usable: q(usable),
        onOrder: q(incoming),
        reorderPoint: q(policy.reorderPoint),
        isDefaultedPoint: policy.isDefaulted,
        averageDailyConsumption: '1.5',
        leadTimeDays: supplier?.leadTimeDays ?? 14,
        coverDays: 14,
        suggestedQuantity: q(suggested),
        preferredSupplierId: policy.preferredSupplierId,
        preferredSupplierName: supplier?.name ?? null,
      });
    }
    return HttpResponse.json({ items });
  }),
];
