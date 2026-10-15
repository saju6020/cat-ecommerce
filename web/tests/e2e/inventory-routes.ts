import { inventoryFixtureIds as ids } from '../../mocks/handlers/inventory';

/**
 * HO-IMP-002 §8.1 — every route this handover adds, dynamic segments resolved with seeded MSW ids, so
 * each renders real data (a loading skeleton or a 404 would hide the page under test from axe).
 * `heading` is a heading expected once the route has actually rendered.
 */
export interface InventoryRoute {
  path: string;
  heading: RegExp;
}

export const INVENTORY_ROUTES: InventoryRoute[] = [
  { path: '/staff/inventory', heading: /^inventory$/i },
  { path: '/staff/inventory/stock', heading: /^stock$/i },
  { path: `/staff/inventory/stock/${ids.foodSkuId}`, heading: /kitten dry food/i },
  { path: '/staff/inventory/batches', heading: /^batches$/i },
  { path: `/staff/inventory/batches/${ids.batchFoodFarId}`, heading: /lot L2409E/i },
  { path: `/staff/inventory/batches/${ids.batchFoodExpiredId}`, heading: /lot L2405Z/i },
  { path: '/staff/inventory/receipts', heading: /goods receipts/i },
  { path: '/staff/inventory/receipts/new', heading: /receive goods/i },
  { path: `/staff/inventory/receipts/${ids.receiptId}`, heading: /GRN-2026-000001/i },
  { path: '/staff/inventory/transfers', heading: /^transfers$/i },
  { path: '/staff/inventory/transfers/new', heading: /new transfer/i },
  { path: '/staff/inventory/adjustments', heading: /^adjustments$/i },
  { path: '/staff/inventory/adjustments/new', heading: /new adjustment/i },
  { path: `/staff/inventory/adjustments/${ids.adjustmentPendingId}`, heading: /ADJ-2026-000002/i },
  { path: '/staff/inventory/counts', heading: /stock counts/i },
  { path: '/staff/inventory/counts/new', heading: /open a count/i },
  { path: `/staff/inventory/counts/${ids.countOpenId}`, heading: /CNT-2026-000001/i },
  { path: `/staff/inventory/counts/${ids.countSubmittedId}`, heading: /CNT-2026-000002/i },
  { path: '/staff/inventory/ledger', heading: /stock ledger/i },
  { path: '/staff/inventory/alerts', heading: /^alerts$/i },
  { path: '/staff/inventory/reports/near-expiry', heading: /inventory reports/i },
  { path: '/staff/inventory/reports/stock-on-hand', heading: /inventory reports/i },
  { path: '/staff/inventory/reports/reconciliation', heading: /inventory reports/i },
  { path: '/staff/inventory/recalls', heading: /^recalls$/i },
  { path: `/staff/inventory/recalls/${ids.batchRecalledId}`, heading: /recall RCL-2026-000001/i },
  { path: '/staff/inventory/storage-breaches', heading: /storage breaches/i },
  { path: '/staff/inventory/storage-breaches/new', heading: /report a storage breach/i },
  { path: `/staff/inventory/storage-breaches/${ids.breachId}`, heading: /^storage breach$/i },
  { path: '/staff/inventory/storage-profiles', heading: /storage profiles/i },
  { path: '/staff/inventory/reorder-policies', heading: /reorder policies/i },
  { path: '/staff/inventory/locations', heading: /stock locations/i },
  { path: '/staff/procurement/suppliers', heading: /^suppliers$/i },
  { path: '/staff/procurement/suppliers/new', heading: /new supplier/i },
  { path: `/staff/procurement/suppliers/${ids.supplierId}`, heading: /meridian pet supplies/i },
  { path: '/staff/procurement/purchase-orders', heading: /purchase orders/i },
  { path: '/staff/procurement/purchase-orders/new', heading: /new purchase order/i },
  { path: `/staff/procurement/purchase-orders/${ids.poPartId}`, heading: /PO-2026-000001/i },
  {
    path: `/staff/procurement/purchase-orders/${ids.poDraftId}?edit=1`,
    heading: /edit PO-2026-000003/i,
  },
  { path: '/staff/procurement/consignments', heading: /import consignments/i },
  { path: '/staff/procurement/consignments/new', heading: /plan a consignment/i },
  {
    path: `/staff/procurement/consignments/${ids.consignmentClearedId}`,
    heading: /CN-2026-000004/i,
  },
  {
    path: `/staff/procurement/consignments/${ids.consignmentClearedId}/documents`,
    heading: /CN-2026-000004/i,
  },
  {
    path: `/staff/procurement/consignments/${ids.consignmentClearedId}/costs`,
    heading: /CN-2026-000004/i,
  },
  {
    path: `/staff/procurement/consignments/${ids.consignmentClearedId}/batches`,
    heading: /CN-2026-000004/i,
  },
  {
    path: `/staff/procurement/consignments/${ids.consignmentClearedId}/inspection-pack`,
    heading: /inspection pack/i,
  },
  { path: '/staff/procurement/reorder', heading: /reorder suggestions/i },
];
