import { apiFetch } from './client';
import { buildQuery } from './inventory-lookup';
import type { PagedResult } from './types';
import type { Money } from '@/lib/format/money';

/**
 * `lib/api/procurement-consignments.ts` — HO-IMP-002 §8.2, US-IMP-03/05. `ProcurementConsignmentsController`:
 * the ACTIVE branch's consignments. Documents are METADATA plus a storage key — there is no upload
 * route anywhere (HO-IMP-001 §4.3, HO-IMP-002 §20.3).
 */

export type ConsignmentStatus = 'Planned' | 'InTransit' | 'AtCustoms' | 'Cleared' | 'Cancelled';

export type ImportDocumentKind =
  | 'ImportPermit'
  | 'HealthCertificate'
  | 'VeterinaryCertificate'
  | 'CustomsDeclaration'
  | 'BillOfLading'
  | 'CommercialInvoice'
  | 'PackingList'
  | 'CertificateOfOrigin'
  | 'Other';

export type CostComponentKind = 'Freight' | 'Duty' | 'Clearing' | 'Insurance' | 'Other';

export interface ConsignmentSummary {
  id: string;
  reference: string;
  status: ConsignmentStatus;
  originCountryCode: string;
  shipmentReference: string;
  expectedArrivalOn: string | null;
  purchaseOrderCount: number;
}

export interface HsCode {
  code: string;
  description: string | null;
}

export interface ConsignmentPurchaseOrder {
  id: string;
  number: string;
  supplierId: string;
  supplierName: string;
  status: string;
}

export interface CostComponent {
  kind: CostComponentKind;
  amount: Money;
  note: string | null;
}

export interface ExchangeRate {
  currencyCode: string;
  rateToBase: string;
  recordedOn: string;
}

export interface ConsignmentDocument {
  documentId: string;
  kind: ImportDocumentKind;
  documentNumber: string | null;
  issuedOn: string | null;
  fileName: string;
  storageKey: string;
  contentType: string;
  sizeInBytes: number;
  uploadedBy: string;
  uploadedOn: string;
  isSuperseded: boolean;
  supersededOn: string | null;
  supersededBy: string | null;
}

export interface ConsignmentStatusChange {
  from: string;
  to: string;
  changedOn: string;
  changedBy: string;
  effectiveOn: string | null;
}

export interface LandedCostLine {
  batchId: string;
  skuCode: string | null;
  lotNumber: string | null;
  quantity: string;
  purchaseCostBase: Money;
  allocatedCostBase: Money;
  landedUnitCostBase: Money;
}

export interface LandedCost {
  allocatedOn: string | null;
  isStale: boolean;
  totalCostsBase: Money | null;
  lines: LandedCostLine[];
}

/** `allowedActions`: `update`, `advance:{next}`, `cancel`, `documents`, `costs`, `receive`. */
export interface ConsignmentDetail {
  id: string;
  reference: string;
  branchId: string;
  originCountryCode: string;
  shipmentReference: string;
  importPermitNumber: string | null;
  healthCertificateNumber: string | null;
  customsDeclarationNumber: string | null;
  expectedArrivalOn: string | null;
  status: ConsignmentStatus;
  cancellationReason: string | null;
  clearedOn: string | null;
  version: number;
  hsCodes: HsCode[];
  purchaseOrders: ConsignmentPurchaseOrder[];
  costComponents: CostComponent[];
  exchangeRates: ExchangeRate[];
  documents: ConsignmentDocument[];
  landedCost: LandedCost;
  statusHistory: ConsignmentStatusChange[];
  allowedActions: string[];
}

export interface ConsignmentBatch {
  batchId: string;
  skuId: string;
  skuCode: string;
  displayName: string;
  lotNumber: string;
  expiresOn: string | null;
  receivedQuantity: string;
  stockUnit: string;
  status: string;
  goodsReceiptNumber: string;
}

export interface InspectionPack {
  consignmentId: string;
  reference: string;
  status: ConsignmentStatus;
  originCountryCode: string;
  shipmentReference: string;
  importPermitNumber: string | null;
  healthCertificateNumber: string | null;
  customsDeclarationNumber: string | null;
  expectedArrivalOn: string | null;
  clearedOn: string | null;
  hsCodes: HsCode[];
  purchaseOrders: ConsignmentPurchaseOrder[];
  documents: ConsignmentDocument[];
  batches: ConsignmentBatch[];
}

export interface ConsignmentListParams {
  status?: string;
  originCountryCode?: string;
  from?: string;
  to?: string;
  pageNumber?: number;
  pageSize?: number;
}

export async function getConsignments(
  params: ConsignmentListParams,
): Promise<PagedResult<ConsignmentSummary>> {
  return apiFetch(`/api/staff/procurement/consignments${buildQuery({ ...params })}`);
}

export async function getConsignment(id: string): Promise<ConsignmentDetail> {
  return apiFetch(`/api/staff/procurement/consignments/${encodeURIComponent(id)}`);
}

export async function getInspectionPack(id: string): Promise<InspectionPack> {
  return apiFetch(`/api/staff/procurement/consignments/${encodeURIComponent(id)}/inspection-pack`);
}

export interface ConsignmentDetailsInput {
  originCountryCode: string;
  shipmentReference: string;
  purchaseOrderIds: string[];
  hsCodes: HsCode[];
  importPermitNumber: string | null;
  healthCertificateNumber: string | null;
  customsDeclarationNumber: string | null;
  expectedArrivalOn: string | null;
}

export async function planConsignment(
  request: ConsignmentDetailsInput,
): Promise<{ consignmentId: string; reference: string }> {
  return apiFetch('/api/staff/procurement/consignments', { method: 'POST', body: request });
}

export async function updateConsignment(
  id: string,
  request: ConsignmentDetailsInput & { expectedVersion: number },
): Promise<void> {
  return apiFetch(`/api/staff/procurement/consignments/${encodeURIComponent(id)}`, { method: 'PUT', body: request });
}

/** `to` is the status NAME (`InTransit`, `AtCustoms`, `Cleared`); `on` the effective date. */
export async function advanceConsignmentStatus(
  id: string,
  request: { to: ConsignmentStatus; on: string },
): Promise<void> {
  return apiFetch(`/api/staff/procurement/consignments/${encodeURIComponent(id)}/status`, {
    method: 'POST',
    body: request,
  });
}

export async function cancelConsignment(id: string, reason: string): Promise<void> {
  return apiFetch(`/api/staff/procurement/consignments/${encodeURIComponent(id)}/cancel`, {
    method: 'POST',
    body: { reason },
  });
}

export interface ConsignmentDocumentInput {
  kind: ImportDocumentKind;
  documentNumber: string | null;
  issuedOn: string | null;
  fileName: string;
  storageKey: string;
  contentType: string;
  sizeInBytes: number;
}

export async function addConsignmentDocument(
  id: string,
  request: ConsignmentDocumentInput,
): Promise<{ documentId: string }> {
  return apiFetch(`/api/staff/procurement/consignments/${encodeURIComponent(id)}/documents`, {
    method: 'POST',
    body: request,
  });
}

export async function supersedeConsignmentDocument(
  id: string,
  documentId: string,
  request: ConsignmentDocumentInput,
): Promise<void> {
  return apiFetch(`/api/staff/procurement/consignments/${encodeURIComponent(id)}/documents/${encodeURIComponent(documentId)}/supersede`, {
    method: 'POST',
    body: request,
  });
}

export interface ConsignmentCostsInput {
  expectedVersion: number;
  costComponents: Array<{
    kind: CostComponentKind;
    amount: string;
    currencyCode: string;
    note: string | null;
  }>;
  exchangeRates: Array<{ currencyCode: string; rateToBase: string; recordedOn: string }>;
}

export async function replaceConsignmentCosts(
  id: string,
  request: ConsignmentCostsInput,
): Promise<void> {
  return apiFetch(`/api/staff/procurement/consignments/${encodeURIComponent(id)}/costs`, {
    method: 'PUT',
    body: request,
  });
}

/** The real allocation result (`AllocateLandedCost`) — NOT the `landedCost` block of the detail. */
export interface AllocatedLandedCost {
  lines: Array<{
    batchId: string;
    quantity: string;
    purchaseCostBase: Money;
    allocatedCostBase: Money;
    landedUnitCostBase: Money;
  }>;
  totalCostsBase: Money;
  allocatedOn: string;
}

export async function allocateLandedCost(
  id: string,
  expectedVersion: number,
): Promise<AllocatedLandedCost> {
  return apiFetch(`/api/staff/procurement/consignments/${encodeURIComponent(id)}/landed-cost`, {
    method: 'POST',
    body: { expectedVersion },
  });
}
