import { apiFetch } from './client';
import { buildQuery } from './inventory-lookup';
import type { Money } from '@/lib/format/money';

/**
 * `lib/api/inventory-reports.ts` — HO-IMP-002 §8.2, US-IMP-10. `StaffInventoryReportsController`
 * (needs `InventoryReports.View`). The real reports are NOT paged (the server caps what it returns);
 * their CSV forms come from the server — never generated here.
 */

export interface LocationQuantity {
  locationId: string;
  locationCode: string | null;
  onHand: string;
}

export interface NearExpiryRow {
  skuId: string;
  skuCode: string;
  displayName: string;
  batchId: string;
  lotNumber: string;
  expiresOn: string;
  daysRemaining: number;
  isExpired: boolean;
  status: string;
  quantity: string;
  locations: LocationQuantity[];
  unitCostBase: Money;
  costBasis: string;
  valueAtRisk: Money;
}

export interface NearExpiryReport {
  asOf: string;
  withinDays: number;
  includeExpired: boolean;
  totalValueAtRisk: Money;
  rows: NearExpiryRow[];
}

export interface NearExpiryParams {
  withinDays?: number;
  includeExpired?: boolean;
}

export async function getNearExpiryReport(params: NearExpiryParams): Promise<NearExpiryReport> {
  return apiFetch(`/api/staff/inventory/reports/near-expiry${buildQuery({ ...params })}`);
}

export function getNearExpiryCsvPath(params: NearExpiryParams): string {
  return `/api/staff/inventory/reports/near-expiry.csv${buildQuery({ ...params })}`;
}

export interface StockOnHandRow {
  skuId: string;
  skuCode: string;
  displayName: string;
  stockClass: string;
  batchId: string;
  lotNumber: string;
  expiresOn: string | null;
  batchStatus: string;
  locationId: string;
  locationCode: string | null;
  onHand: string;
  reserved: string;
  unitCostBase: Money;
  costBasis: string;
  value: Money;
}

export interface StockOnHandReport {
  asOf: string;
  totalValue: Money;
  rows: StockOnHandRow[];
}

export interface StockOnHandParams {
  locationId?: string;
  class?: string;
}

export async function getStockOnHandReport(params: StockOnHandParams): Promise<StockOnHandReport> {
  return apiFetch(`/api/staff/inventory/reports/stock-on-hand${buildQuery({ ...params })}`);
}

export function getStockOnHandCsvPath(params: StockOnHandParams): string {
  return `/api/staff/inventory/reports/stock-on-hand.csv${buildQuery({ ...params })}`;
}

export interface ReconciliationRow {
  batchId: string;
  locationId: string;
  onHand: string;
  expectedOnHand: string;
  reserved: string;
  expectedReserved: string;
  skuId: string;
  branchId: string;
}

/** Expected empty; the real API answers `{ items: [...] }`. */
export async function getReconciliation(): Promise<{ items: ReconciliationRow[] }> {
  return apiFetch('/api/staff/inventory/reports/reconciliation');
}
