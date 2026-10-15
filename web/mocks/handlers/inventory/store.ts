import { loadPersisted, savePersisted, clearPersisted } from '../admin/fixtures';
import { accountsFixtureIds } from '../areas/accounts';
import type { SkuLookup, StockLocation, StorageProfile } from '@/lib/api/inventory-lookup';
import type { SupplierDetail } from '@/lib/api/procurement-suppliers';
import type { PurchaseOrderStatus } from '@/lib/api/procurement-orders';
import type { ConsignmentDetail } from '@/lib/api/procurement-consignments';
import type {
  BatchRecall,
  BatchStatus,
  BatchStatusChange,
  LedgerEntry,
} from '@/lib/api/inventory-stock';
import type { GoodsReceiptDetail } from '@/lib/api/inventory-receipts';
import type {
  AdjustmentDetail,
  CountDetail,
  CountVariance,
  TransferDetail,
} from '@/lib/api/inventory-control';
import type { InventoryAlert } from '@/lib/api/inventory-alerts';
import type { BreachDetail } from '@/lib/api/inventory-breaches';
import type { RecallTrace } from '@/lib/api/inventory-recalls';
import {
  MOCK_BASE_CURRENCY,
  dateOffset,
  instantOffset,
  inventoryFixtureIds as ids,
  money,
  q,
} from './fixtures';

/**
 * `mocks/handlers/inventory/store.ts` — HO-IMP-002 §12. The in-memory "database" behind the
 * Inventory/Procurement mocks. Quantities are plain numbers INSIDE the mock (it plays the server);
 * every response goes through `q()` / `money()` so the wire carries strings, exactly as the real
 * API does. Persisted to `sessionStorage` in a real browser (a `page.goto` re-runs every module),
 * kept simple and always-fresh in Vitest.
 */

const PERSIST_KEY = '__purrcare_inventory_mock_db__';

export const staffNames: Record<string, string> = {
  [accountsFixtureIds.adminStaffId]: 'Rezwan Ahmed',
  [accountsFixtureIds.nurseStaffId]: 'Nadia Karim',
};
export const ACTOR_ID = accountsFixtureIds.adminStaffId;

export interface MPoLine {
  lineId: string;
  skuId: string;
  orderedQuantity: number;
  acceptedQuantity: number;
  rejectedQuantity: number;
  unitCost: number;
}

export interface MPo {
  id: string;
  number: string;
  branchId: string;
  supplierId: string;
  deliverToLocationId: string;
  currencyCode: string;
  expectedDeliveryOn: string;
  notes: string | null;
  status: PurchaseOrderStatus;
  statusReason: string | null;
  issuedOn: string | null;
  version: number;
  lines: MPoLine[];
  receipts: Array<{ goodsReceiptId: string; number: string; receivedOn: string }>;
}

export interface MBatch {
  id: string;
  branchId: string;
  skuId: string;
  lotNumber: string;
  manufacturedOn: string | null;
  expiresOn: string | null;
  status: BatchStatus;
  statusReason: string | null;
  receivedOn: string;
  receivedQuantity: number;
  goodsReceiptId: string;
  goodsReceiptNumber: string;
  consignmentId: string | null;
  supplierId: string | null;
  originCountryCode: string | null;
  purchaseUnitCost: number;
  unitCostBase: number;
  costBasis: 'purchase' | 'landed';
  statusHistory: BatchStatusChange[];
  recall: BatchRecall | null;
  recallStatus: 'Active' | 'Closed' | 'Withdrawn' | null;
  version: number;
}

export interface MPosition {
  batchId: string;
  locationId: string;
  onHand: number;
  reserved: number;
}

export interface MReceipt extends GoodsReceiptDetail {
  /** Keeps the PO line each receipt line was posted against (not on the wire). */
  _poLines: Array<string | null>;
}

export interface MBreach extends BreachDetail {
  branchId: string;
}

export interface MAlert extends InventoryAlert {
  branchId: string;
}

export interface MLedger extends LedgerEntry {
  branchId: string;
}

export interface MockDb {
  seq: number;
  locations: Array<StockLocation & { branchId: string }>;
  skus: SkuLookup[];
  storageProfiles: Record<string, StorageProfile>;
  suppliers: SupplierDetail[];
  purchaseOrders: MPo[];
  consignments: Array<ConsignmentDetail & { _allocationBatchIds: string[] }>;
  batches: MBatch[];
  positions: MPosition[];
  receipts: Array<MReceipt & { _branchId: string }>;
  transfers: Array<TransferDetail & { _branchId: string }>;
  adjustments: Array<
    AdjustmentDetail & { _branchId: string; _staleOnApprove?: boolean; _requesterId: string }
  >;
  counts: Array<CountDetail & { _branchId: string; _variance: CountVariance | null }>;
  ledger: MLedger[];
  alerts: MAlert[];
  breaches: MBreach[];
  recallTraces: Record<string, RecallTrace>;
  notifiedRuns: Record<string, number>;
  reorderPolicies: Array<{
    branchId: string;
    skuId: string;
    locationId: string;
    reorderPoint: number;
    reorderQuantity: number | null;
    preferredSupplierId: string | null;
    isDefaulted: boolean;
  }>;
}

const LOCATION_MOVEMENTS: Record<StockLocation['kind'], string[]> = {
  ShopFloor: [
    'Receipt',
    'TransferIn',
    'TransferOut',
    'AdjustmentIn',
    'AdjustmentOut',
    'CountGain',
    'CountLoss',
    'Wastage',
    'Sale',
  ],
  Warehouse: [
    'Receipt',
    'TransferIn',
    'TransferOut',
    'AdjustmentIn',
    'AdjustmentOut',
    'CountGain',
    'CountLoss',
    'Wastage',
    'Sale',
  ],
  ClinicDispensary: [
    'Receipt',
    'TransferIn',
    'TransferOut',
    'AdjustmentIn',
    'AdjustmentOut',
    'CountGain',
    'CountLoss',
    'Wastage',
    'Dispense',
    'ClinicalUse',
  ],
  BoardingStore: [
    'Receipt',
    'TransferIn',
    'TransferOut',
    'AdjustmentIn',
    'AdjustmentOut',
    'CountGain',
    'CountLoss',
    'Wastage',
    'BoardingUse',
  ],
  Quarantine: [
    'Receipt',
    'TransferIn',
    'TransferOut',
    'AdjustmentIn',
    'AdjustmentOut',
    'CountGain',
    'CountLoss',
    'Wastage',
    'Return',
  ],
};

function location(
  branchId: string,
  id: string,
  code: string,
  name: string,
  kind: StockLocation['kind'],
): StockLocation & { branchId: string } {
  return {
    id,
    code,
    name,
    kind,
    isActive: true,
    permittedMovements: LOCATION_MOVEMENTS[kind],
    branchId,
  };
}

function seed(): MockDb {
  const none = null;
  const ambient = {
    condition: 'Ambient' as const,
    minTemperatureC: none,
    maxTemperatureC: none,
    handlingNotes: none,
  };
  const fridge = {
    condition: 'Refrigerated' as const,
    minTemperatureC: 2,
    maxTemperatureC: 8,
    handlingNotes: 'Keep in the dispensary fridge. Do not freeze.',
  };

  const skus: SkuLookup[] = [
    {
      skuId: ids.foodSkuId,
      productId: 'f2000000-0000-4000-8000-000000000001',
      skuCode: 'FD-KDC-2KG',
      displayName: 'Kitten dry food, chicken 2 kg',
      barcode: ids.foodBarcode,
      stockClass: 'Food',
      expiryRequired: true,
      stockUnit: 'bags',
      allowsFractionalQuantity: false,
      countryOfOriginCode: 'DE',
      isActive: true,
      storage: ambient,
    },
    {
      skuId: ids.vaccineSkuId,
      productId: 'f2000000-0000-4000-8000-000000000002',
      skuCode: 'MED-FVX-TRI',
      displayName: 'Feline tricat vaccine',
      barcode: ids.vaccineBarcode,
      stockClass: 'Medicine',
      expiryRequired: true,
      stockUnit: 'ml',
      allowsFractionalQuantity: true,
      countryOfOriginCode: 'US',
      isActive: true,
      storage: fridge,
    },
    {
      skuId: ids.litterSkuId,
      productId: 'f2000000-0000-4000-8000-000000000003',
      skuCode: 'GEN-LTR-5L',
      displayName: 'Cat litter, clumping 5 L',
      barcode: '8901234567890',
      stockClass: 'General',
      expiryRequired: false,
      stockUnit: 'bags',
      allowsFractionalQuantity: false,
      countryOfOriginCode: 'TH',
      isActive: true,
      storage: none,
    },
    {
      skuId: ids.wetFoodSkuId,
      productId: 'f2000000-0000-4000-8000-000000000004',
      skuCode: 'FD-ADT-POUCH',
      displayName: 'Adult wet food pouches, tuna',
      barcode: '4000000000017',
      stockClass: 'Food',
      expiryRequired: true,
      stockUnit: 'pouches',
      allowsFractionalQuantity: false,
      countryOfOriginCode: 'TH',
      isActive: true,
      storage: ambient,
    },
  ];

  const batch = (
    id: string,
    skuId: string,
    lot: string,
    expiresOffset: number | null,
    status: BatchStatus,
    extra: Partial<MBatch> = {},
  ): MBatch => ({
    id,
    branchId: ids.mainBranchId,
    skuId,
    lotNumber: lot,
    manufacturedOn: dateOffset(-60),
    expiresOn: expiresOffset === null ? null : dateOffset(expiresOffset),
    status,
    statusReason: null,
    receivedOn: instantOffset(-30),
    receivedQuantity: 30,
    goodsReceiptId: ids.receiptId,
    goodsReceiptNumber: 'GRN-2026-000001',
    consignmentId: null,
    supplierId: ids.supplierId,
    originCountryCode: 'DE',
    purchaseUnitCost: 450,
    unitCostBase: 450,
    costBasis: 'purchase',
    statusHistory: [],
    recall: null,
    recallStatus: null,
    version: 1,
    ...extra,
  });

  const recall: BatchRecall = {
    recallNumber: 'RCL-2026-000001',
    reason: 'Supplier notice: possible contamination in lot L7702R.',
    customerAdvice: 'Stop using this vaccine lot and contact the clinic for a replacement dose.',
    declaredOn: instantOffset(-2, 10),
    declaredBy: ACTOR_ID,
    notifiedOn: none,
    notifiedBy: none,
    notifiedRecipientCount: 0,
    notificationRuns: 0,
    closedOn: none,
    closingNote: none,
  };

  const batches: MBatch[] = [
    batch(ids.batchFoodFarId, ids.foodSkuId, 'L2409E', 120, 'Available', {
      consignmentId: ids.consignmentClearedId,
      unitCostBase: 462.5,
      costBasis: 'landed',
      purchaseUnitCost: 450,
    }),
    batch(ids.batchFoodSoonId, ids.foodSkuId, 'L2408B', 45, 'Available', {
      consignmentId: ids.consignmentClearedId,
      unitCostBase: 462.5,
      costBasis: 'landed',
      purchaseUnitCost: 450,
    }),
    batch(ids.batchFoodTodayId, ids.foodSkuId, 'L2406X', 0, 'Available'),
    batch(ids.batchFoodExpiredId, ids.foodSkuId, 'L2405Z', -3, 'Available'),
    batch(ids.batchFoodQuarantinedId, ids.foodSkuId, 'L2407Q', 200, 'Quarantined', {
      statusReason: 'Damaged packaging on three bags',
      statusHistory: [
        {
          from: 'Available',
          to: 'Quarantined',
          reason: 'Damaged packaging on three bags',
          changedOn: instantOffset(-5),
          changedBy: ACTOR_ID,
        },
      ],
    }),
    batch(ids.batchVaccineId, ids.vaccineSkuId, 'L7701V', 60, 'Available', {
      purchaseUnitCost: 30.5,
      unitCostBase: 30.5,
      originCountryCode: 'US',
    }),
    batch(ids.batchRecalledId, ids.vaccineSkuId, 'L7702R', 30, 'Recalled', {
      statusReason: 'Recalled',
      recall,
      recallStatus: 'Active',
      purchaseUnitCost: 30.5,
      unitCostBase: 30.5,
      originCountryCode: 'US',
      statusHistory: [
        {
          from: 'Available',
          to: 'Recalled',
          reason: 'Recall RCL-2026-000001',
          changedOn: instantOffset(-2, 10),
          changedBy: ACTOR_ID,
        },
      ],
    }),
    batch(ids.batchLitterId, ids.litterSkuId, 'LTR-0912', null, 'Available', {
      purchaseUnitCost: 800,
      unitCostBase: 800,
      originCountryCode: 'TH',
    }),
    batch(ids.batchWrittenOffId, ids.litterSkuId, 'LTR-0801', null, 'WrittenOff', {
      statusReason: 'Water damage',
      purchaseUnitCost: 800,
      unitCostBase: 800,
      statusHistory: [
        {
          from: 'Available',
          to: 'WrittenOff',
          reason: 'Water damage',
          changedOn: instantOffset(-9),
          changedBy: ACTOR_ID,
        },
      ],
    }),
    batch(ids.batchWetFoodId, ids.wetFoodSkuId, 'P5501', 90, 'Available', {
      purchaseUnitCost: 95,
      unitCostBase: 95,
      originCountryCode: 'TH',
    }),
    {
      ...batch(ids.batchLondonId, ids.foodSkuId, 'L2409L', 100, 'Available'),
      branchId: ids.londonBranchId,
    },
  ];

  const positions: MPosition[] = [
    { batchId: ids.batchFoodFarId, locationId: ids.warehouseId, onHand: 18, reserved: 2 },
    { batchId: ids.batchFoodFarId, locationId: ids.shopFloorId, onHand: 6, reserved: 0 },
    { batchId: ids.batchFoodSoonId, locationId: ids.warehouseId, onHand: 10, reserved: 0 },
    { batchId: ids.batchFoodTodayId, locationId: ids.shopFloorId, onHand: 3, reserved: 0 },
    { batchId: ids.batchFoodExpiredId, locationId: ids.warehouseId, onHand: 4, reserved: 0 },
    { batchId: ids.batchFoodQuarantinedId, locationId: ids.warehouseId, onHand: 5, reserved: 0 },
    { batchId: ids.batchVaccineId, locationId: ids.dispensaryId, onHand: 50.5, reserved: 5 },
    { batchId: ids.batchRecalledId, locationId: ids.dispensaryId, onHand: 20, reserved: 0 },
    { batchId: ids.batchRecalledId, locationId: ids.boardingStoreId, onHand: 10, reserved: 0 },
    { batchId: ids.batchLitterId, locationId: ids.shopFloorId, onHand: 40, reserved: 0 },
    { batchId: ids.batchWetFoodId, locationId: ids.warehouseId, onHand: 8, reserved: 0 },
    { batchId: ids.batchWetFoodId, locationId: ids.quarantineId, onHand: 6, reserved: 0 },
    { batchId: ids.batchLondonId, locationId: ids.londonWarehouseId, onHand: 12, reserved: 0 },
  ];

  const detailBase = (
    id: string,
    reference: string,
    status: ConsignmentDetail['status'],
    extra: Partial<ConsignmentDetail & { _allocationBatchIds: string[] }> = {},
  ): ConsignmentDetail & { _allocationBatchIds: string[] } => ({
    id,
    reference,
    branchId: ids.mainBranchId,
    originCountryCode: 'DE',
    shipmentReference: `SHP-${reference.slice(-3)}`,
    importPermitNumber: null,
    healthCertificateNumber: null,
    customsDeclarationNumber: null,
    expectedArrivalOn: dateOffset(14),
    status,
    cancellationReason: null,
    clearedOn: null,
    version: 1,
    hsCodes: [{ code: '2309.10', description: 'Dog or cat food, retail packs' }],
    purchaseOrders: [
      {
        id: ids.poPartId,
        number: 'PO-2026-000001',
        supplierId: ids.supplierId,
        supplierName: 'Meridian Pet Supplies Ltd',
        status: 'Issued',
      },
    ],
    costComponents: [],
    exchangeRates: [],
    documents: [],
    landedCost: { allocatedOn: null, isStale: false, totalCostsBase: null, lines: [] },
    statusHistory: [],
    allowedActions: [],
    _allocationBatchIds: [],
    ...extra,
  });

  const doc = (
    documentId: string,
    kind: ConsignmentDetail['documents'][number]['kind'],
    number: string | null,
    superseded: boolean,
  ): ConsignmentDetail['documents'][number] => ({
    documentId,
    kind,
    documentNumber: number,
    issuedOn: dateOffset(-20),
    fileName: `${kind.toLowerCase()}-${number ?? 'x'}.pdf`,
    storageKey: `import/${documentId}.pdf`,
    contentType: 'application/pdf',
    sizeInBytes: 184_320,
    uploadedBy: ACTOR_ID,
    uploadedOn: instantOffset(-19),
    isSuperseded: superseded,
    supersededOn: superseded ? instantOffset(-10) : null,
    supersededBy: superseded ? ACTOR_ID : null,
  });

  const clearedLanded = {
    allocatedOn: instantOffset(-8),
    isStale: false,
    totalCostsBase: money(2250),
    lines: [
      {
        batchId: ids.batchFoodFarId,
        skuCode: 'FD-KDC-2KG',
        lotNumber: 'L2409E',
        quantity: '30',
        purchaseCostBase: money(13500),
        allocatedCostBase: money(1125),
        landedUnitCostBase: { amount: '487.5000', currencyCode: MOCK_BASE_CURRENCY },
      },
      {
        batchId: ids.batchFoodSoonId,
        skuCode: 'FD-KDC-2KG',
        lotNumber: 'L2408B',
        quantity: '30',
        purchaseCostBase: money(13500),
        allocatedCostBase: money(1125),
        landedUnitCostBase: { amount: '487.5000', currencyCode: MOCK_BASE_CURRENCY },
      },
    ],
  };

  const consignments = [
    detailBase(ids.consignmentPlannedId, 'CN-2026-000001', 'Planned', {
      allowedActions: ['update', 'advance:in-transit', 'cancel', 'documents', 'costs'],
    }),
    detailBase(ids.consignmentTransitId, 'CN-2026-000002', 'InTransit', {
      importPermitNumber: 'IP-88121',
      allowedActions: ['update', 'advance:at-customs', 'cancel', 'documents', 'costs'],
      statusHistory: [
        {
          from: 'Planned',
          to: 'InTransit',
          changedOn: instantOffset(-6),
          changedBy: ACTOR_ID,
          effectiveOn: dateOffset(-6),
        },
      ],
    }),
    detailBase(ids.consignmentCustomsId, 'CN-2026-000003', 'AtCustoms', {
      importPermitNumber: 'IP-88122',
      healthCertificateNumber: 'HC-55012',
      customsDeclarationNumber: null,
      allowedActions: ['update', 'advance:cleared', 'cancel', 'documents', 'costs'],
    }),
    detailBase(ids.consignmentClearedId, 'CN-2026-000004', 'Cleared', {
      importPermitNumber: 'IP-88001',
      healthCertificateNumber: 'HC-54001',
      customsDeclarationNumber: 'CD-2026-7781',
      clearedOn: dateOffset(-12),
      version: 4,
      allowedActions: ['receive', 'documents', 'costs'],
      documents: [
        doc('d3000000-0000-4000-8000-000000000001', 'ImportPermit', 'IP-88000', true),
        doc('d3000000-0000-4000-8000-000000000002', 'ImportPermit', 'IP-88001', false),
        doc('d3000000-0000-4000-8000-000000000003', 'HealthCertificate', 'HC-54001', false),
        doc('d3000000-0000-4000-8000-000000000004', 'CustomsDeclaration', 'CD-2026-7781', false),
      ],
      costComponents: [
        {
          kind: 'Freight',
          amount: { amount: '1200.00', currencyCode: 'USD' },
          note: 'Sea freight',
        },
        { kind: 'Duty', amount: { amount: '800.00', currencyCode: 'BDT' }, note: null },
        {
          kind: 'Clearing',
          amount: { amount: '150.00', currencyCode: 'USD' },
          note: 'Clearing agent',
        },
      ],
      exchangeRates: [{ currencyCode: 'USD', rateToBase: '110', recordedOn: dateOffset(-12) }],
      landedCost: clearedLanded,
      statusHistory: [
        {
          from: 'Planned',
          to: 'InTransit',
          changedOn: instantOffset(-25),
          changedBy: ACTOR_ID,
          effectiveOn: dateOffset(-25),
        },
        {
          from: 'InTransit',
          to: 'AtCustoms',
          changedOn: instantOffset(-15),
          changedBy: ACTOR_ID,
          effectiveOn: dateOffset(-15),
        },
        {
          from: 'AtCustoms',
          to: 'Cleared',
          changedOn: instantOffset(-12),
          changedBy: ACTOR_ID,
          effectiveOn: dateOffset(-12),
        },
      ],
      _allocationBatchIds: [ids.batchFoodFarId, ids.batchFoodSoonId],
    }),
    detailBase(ids.consignmentStaleId, 'CN-2026-000005', 'Cleared', {
      importPermitNumber: 'IP-88500',
      healthCertificateNumber: 'HC-54500',
      customsDeclarationNumber: 'CD-2026-7790',
      clearedOn: dateOffset(-3),
      version: 6,
      allowedActions: ['receive', 'documents', 'costs'],
      costComponents: [
        { kind: 'Freight', amount: { amount: '900.00', currencyCode: 'USD' }, note: null },
      ],
      exchangeRates: [{ currencyCode: 'USD', rateToBase: '111.5', recordedOn: dateOffset(-3) }],
      landedCost: {
        allocatedOn: instantOffset(-3),
        isStale: true,
        totalCostsBase: money(100350),
        lines: [],
      },
    }),
    detailBase(ids.consignmentCancelledId, 'CN-2026-000006', 'Cancelled', {
      cancellationReason: 'Supplier could not ship',
      allowedActions: [],
    }),
  ];

  const supplier = (
    id: string,
    code: string,
    name: string,
    country: string,
    currency: string,
    lead: number,
    active: boolean,
  ): SupplierDetail => ({
    id,
    code,
    name,
    countryCode: country,
    contactName: 'Sales desk',
    phone: '+49 30 555 0100',
    email: `orders@${code.toLowerCase()}.example.com`,
    address: 'Hafenstrasse 4, Hamburg',
    currencyCode: currency,
    leadTimeDays: lead,
    paymentTerms: 'Net 30',
    paymentTermDays: 30,
    notes: null,
    isActive: active,
    version: 1,
  });

  const purchaseOrders: MPo[] = [
    {
      id: ids.poPartId,
      number: 'PO-2026-000001',
      branchId: ids.mainBranchId,
      supplierId: ids.supplierId,
      deliverToLocationId: ids.warehouseId,
      currencyCode: MOCK_BASE_CURRENCY,
      expectedDeliveryOn: dateOffset(7),
      notes: null,
      status: 'Issued',
      statusReason: null,
      issuedOn: instantOffset(-20),
      version: 3,
      lines: [
        {
          lineId: ids.poPartLine1Id,
          skuId: ids.foodSkuId,
          orderedQuantity: 100,
          acceptedQuantity: 60,
          rejectedQuantity: 10,
          unitCost: 450,
        },
        {
          lineId: ids.poPartLine2Id,
          skuId: ids.litterSkuId,
          orderedQuantity: 50,
          acceptedQuantity: 0,
          rejectedQuantity: 0,
          unitCost: 800,
        },
      ],
      receipts: [
        {
          goodsReceiptId: ids.receiptId,
          number: 'GRN-2026-000001',
          receivedOn: instantOffset(-10),
        },
      ],
    },
    {
      id: ids.poOpenId,
      number: 'PO-2026-000002',
      branchId: ids.mainBranchId,
      supplierId: ids.supplier2Id,
      deliverToLocationId: ids.warehouseId,
      currencyCode: 'USD',
      expectedDeliveryOn: dateOffset(20),
      notes: 'Vaccine order, cold chain',
      status: 'Issued',
      statusReason: null,
      issuedOn: instantOffset(-2),
      version: 2,
      lines: [
        {
          lineId: 'd2000000-0000-4000-8000-000000000003',
          skuId: ids.vaccineSkuId,
          orderedQuantity: 200.5,
          acceptedQuantity: 0,
          rejectedQuantity: 0,
          unitCost: 0.28,
        },
      ],
      receipts: [],
    },
    {
      id: ids.poDraftId,
      number: 'PO-2026-000003',
      branchId: ids.mainBranchId,
      supplierId: ids.supplierId,
      deliverToLocationId: ids.warehouseId,
      currencyCode: MOCK_BASE_CURRENCY,
      expectedDeliveryOn: dateOffset(30),
      notes: null,
      status: 'Draft',
      statusReason: null,
      issuedOn: null,
      version: 1,
      lines: [
        {
          lineId: 'd2000000-0000-4000-8000-000000000004',
          skuId: ids.litterSkuId,
          orderedQuantity: 60,
          acceptedQuantity: 0,
          rejectedQuantity: 0,
          unitCost: 800,
        },
      ],
      receipts: [],
    },
    {
      id: ids.poClosedId,
      number: 'PO-2026-000004',
      branchId: ids.mainBranchId,
      supplierId: ids.supplierId,
      deliverToLocationId: ids.warehouseId,
      currencyCode: MOCK_BASE_CURRENCY,
      expectedDeliveryOn: dateOffset(-30),
      notes: null,
      status: 'Closed',
      statusReason: 'Supplier short-shipped; remainder cancelled.',
      issuedOn: instantOffset(-50),
      version: 5,
      lines: [
        {
          lineId: 'd2000000-0000-4000-8000-000000000005',
          skuId: ids.litterSkuId,
          orderedQuantity: 40,
          acceptedQuantity: 30,
          rejectedQuantity: 0,
          unitCost: 800,
        },
      ],
      receipts: [],
    },
  ];

  const ledgerBase = (
    sequence: number,
    kind: string,
    batchId: string,
    skuId: string,
    skuCode: string,
    lot: string,
    locationId: string,
    locationCode: string,
    delta: number,
    unit: string,
    extra: Partial<MLedger> = {},
  ): MLedger => ({
    sequence,
    occurredOn: instantOffset(-10 + sequence),
    kind,
    quantityDelta: q(delta),
    stockUnit: unit,
    locationId,
    locationCode,
    batchId,
    lotNumber: lot,
    skuId,
    skuCode,
    referenceKind: 'GoodsReceipt',
    referenceId: ids.receiptId,
    referenceLabel: 'GRN-2026-000001',
    fefoOverridden: false,
    overrideReason: null,
    performedBy: ACTOR_ID,
    performedByName: 'Rezwan Ahmed',
    note: null,
    branchId: ids.mainBranchId,
    ...extra,
  });

  const ledger: MLedger[] = [
    ledgerBase(
      1,
      'Receipt',
      ids.batchFoodFarId,
      ids.foodSkuId,
      'FD-KDC-2KG',
      'L2409E',
      ids.warehouseId,
      'WH1',
      30,
      'bags',
    ),
    ledgerBase(
      2,
      'Receipt',
      ids.batchFoodSoonId,
      ids.foodSkuId,
      'FD-KDC-2KG',
      'L2408B',
      ids.warehouseId,
      'WH1',
      30,
      'bags',
    ),
    ledgerBase(
      3,
      'Sale',
      ids.batchFoodFarId,
      ids.foodSkuId,
      'FD-KDC-2KG',
      'L2409E',
      ids.shopFloorId,
      'SHOP',
      -2,
      'bags',
      {
        referenceKind: 'CustomerOrder',
        referenceId: 'a3000000-0000-4000-8000-000000000001',
        referenceLabel: 'Order PC-001234',
        fefoOverridden: true,
        overrideReason: 'Customer asked for the newest lot for a gift hamper.',
      },
    ),
    ledgerBase(
      4,
      'Dispense',
      ids.batchVaccineId,
      ids.vaccineSkuId,
      'MED-FVX-TRI',
      'L7701V',
      ids.dispensaryId,
      'DISP',
      -1.5,
      'ml',
      {
        referenceKind: 'Prescription',
        referenceId: 'a3000000-0000-4000-8000-000000000002',
        referenceLabel: 'RX-2026-000045',
      },
    ),
  ];

  const alert = (
    id: string,
    kind: InventoryAlert['kind'],
    status: InventoryAlert['status'],
    extra: Partial<MAlert> = {},
  ): MAlert => ({
    id,
    kind,
    status,
    skuId: null,
    skuCode: null,
    displayName: null,
    batchId: null,
    lotNumber: null,
    locationId: null,
    locationCode: null,
    thresholdDays: null,
    quantityAtRaise: null,
    raisedOn: instantOffset(-3),
    acknowledgedBy: null,
    acknowledgedOn: null,
    resolvedOn: null,
    resolutionNote: null,
    branchId: ids.mainBranchId,
    ...extra,
  });

  const alerts: MAlert[] = [
    alert(ids.alertExpiryId, 'ExpiryThreshold', 'Open', {
      skuId: ids.foodSkuId,
      skuCode: 'FD-KDC-2KG',
      displayName: 'Kitten dry food, chicken 2 kg',
      batchId: ids.batchFoodSoonId,
      lotNumber: 'L2408B',
      thresholdDays: 60,
      quantityAtRaise: '10',
    }),
    alert('a2000000-0000-4000-8000-000000000002', 'Expired', 'Open', {
      skuId: ids.foodSkuId,
      skuCode: 'FD-KDC-2KG',
      displayName: 'Kitten dry food, chicken 2 kg',
      batchId: ids.batchFoodExpiredId,
      lotNumber: 'L2405Z',
      quantityAtRaise: '4',
    }),
    alert('a2000000-0000-4000-8000-000000000003', 'LowStock', 'Open', {
      skuId: ids.wetFoodSkuId,
      skuCode: 'FD-ADT-POUCH',
      displayName: 'Adult wet food pouches, tuna',
      locationId: ids.warehouseId,
      locationCode: 'WH1',
      quantityAtRaise: '8',
    }),
    alert('a2000000-0000-4000-8000-000000000004', 'Shortfall', 'Acknowledged', {
      skuId: ids.foodSkuId,
      skuCode: 'FD-KDC-2KG',
      displayName: 'Kitten dry food, chicken 2 kg',
      locationId: ids.shopFloorId,
      locationCode: 'SHOP',
      acknowledgedBy: ACTOR_ID,
      acknowledgedOn: instantOffset(-2),
    }),
    alert('a2000000-0000-4000-8000-000000000005', 'StorageBreach', 'Open', {
      locationId: ids.dispensaryId,
      locationCode: 'DISP',
    }),
    alert('a2000000-0000-4000-8000-000000000006', 'LedgerMismatch', 'Resolved', {
      resolvedOn: instantOffset(-1),
      resolutionNote: 'Recounted: a late sale was posted twice and reversed.',
    }),
  ];

  const breaches: MBreach[] = [
    {
      branchId: ids.mainBranchId,
      summary: {
        id: ids.breachId,
        locationId: ids.dispensaryId,
        locationCode: 'DISP',
        observedOn: instantOffset(-1, 7),
        condition: 'Dispensary fridge door left open overnight',
        status: 'Open',
        affectedBatchCount: 2,
        reportedBy: ACTOR_ID,
        reportedOn: instantOffset(-1, 8),
      },
      temperatureC: 11.5,
      durationMinutes: 240,
      affectedBatches: [
        {
          batchId: ids.batchVaccineId,
          skuCode: 'MED-FVX-TRI',
          displayName: 'Feline tricat vaccine',
          lotNumber: 'L7701V',
          heldAtLocation: '50.5',
        },
        {
          batchId: ids.batchRecalledId,
          skuCode: 'MED-FVX-TRI',
          displayName: 'Feline tricat vaccine',
          lotNumber: 'L7702R',
          heldAtLocation: '20',
        },
      ],
      quarantinedOn: null,
      quarantinedBy: null,
      closedOn: null,
      closedBy: null,
      closingNote: null,
      allowedActions: ['quarantine', 'close'],
    },
  ];

  const customer = (
    id: string,
    name: string,
    mobile: string | null,
    email: string | null,
    extra: Record<string, unknown> = {},
  ) => ({
    customerId: id,
    name,
    mobileNumber: mobile,
    email,
    isAnonymised: false,
    originalCustomerId: null,
    ...extra,
  });

  const nusrat = customer(
    'c2000000-0000-4000-8000-000000000001',
    'Nusrat Jahan',
    '01711000001',
    'nusrat@example.com',
  );
  const karim = customer(
    'c2000000-0000-4000-8000-000000000002',
    'Karim Uddin',
    '01711000002',
    'karim@example.com',
  );
  const anika = customer(
    'c2000000-0000-4000-8000-000000000003',
    'Anika Rahman',
    '01711000003',
    null,
  );
  const survivor = customer(
    'c2000000-0000-4000-8000-000000000004',
    'Farhana Akter',
    '01711000004',
    'farhana@example.com',
    {
      originalCustomerId: 'c2000000-0000-4000-8000-0000000000ff',
    },
  );
  const anonymised = customer(
    'c2000000-0000-4000-8000-000000000005',
    'Anonymised customer',
    null,
    null,
    { isAnonymised: true },
  );
  const cat = (id: string, name: string, owner: ReturnType<typeof customer>) => ({
    catId: id,
    name,
    currentOwner: owner,
  });

  const recallTrace: RecallTrace = {
    batchId: ids.batchRecalledId,
    recallNumber: 'RCL-2026-000001',
    skuCode: 'MED-FVX-TRI',
    displayName: 'Feline tricat vaccine',
    lotNumber: 'L7702R',
    batchStatus: 'Recalled',
    received: [
      {
        occurredOn: instantOffset(-12),
        kind: 'Sale',
        quantity: '2',
        locationId: ids.dispensaryId,
        locationCode: 'DISP',
        referenceLabel: 'Order PC-001234',
        customer: nusrat,
        cat: cat('c3000000-0000-4000-8000-000000000001', 'Mishti', nusrat),
        householdCats: [],
      },
      {
        occurredOn: instantOffset(-9),
        kind: 'Dispense',
        quantity: '1.5',
        locationId: ids.dispensaryId,
        locationCode: 'DISP',
        referenceLabel: 'RX-2026-000045',
        customer: karim,
        cat: cat('c3000000-0000-4000-8000-000000000002', 'Tiger', karim),
        householdCats: [],
      },
      {
        occurredOn: instantOffset(-6),
        kind: 'BoardingUse',
        quantity: '1',
        locationId: ids.boardingStoreId,
        locationCode: 'BOARD',
        referenceLabel: 'Stay BR-2026-000009',
        customer: anika,
        cat: cat('c3000000-0000-4000-8000-000000000003', 'Luna', anika),
        householdCats: [],
      },
      {
        occurredOn: instantOffset(-5),
        kind: 'Sale',
        quantity: '3',
        locationId: ids.dispensaryId,
        locationCode: 'DISP',
        referenceLabel: 'Order PC-001240',
        customer: survivor,
        cat: null,
        householdCats: ['Pepper', 'Biscuit'],
      },
      {
        occurredOn: instantOffset(-4),
        kind: 'Sale',
        quantity: '1',
        locationId: ids.dispensaryId,
        locationCode: 'DISP',
        referenceLabel: 'Order PC-001241',
        customer: anonymised,
        cat: null,
        householdCats: [],
      },
    ],
    returned: [
      {
        referenceKind: 'CustomerReturn',
        referenceId: 'a3000000-0000-4000-8000-000000000005',
        referenceLabel: 'Return RT-2026-000002',
        quantity: '1',
        customer: nusrat,
      },
    ],
    unshippedDispatchBlocked: [
      {
        referenceKind: 'CustomerOrder',
        referenceId: 'a3000000-0000-4000-8000-000000000006',
        quantity: '2',
        customer: karim,
        cat: cat('c3000000-0000-4000-8000-000000000002', 'Tiger', karim),
      },
    ],
    unidentified: { count: 3, quantity: '6' },
    stockHeld: [
      {
        locationId: ids.dispensaryId,
        locationCode: 'DISP',
        branchId: ids.mainBranchId,
        onHand: '20',
      },
      {
        locationId: ids.boardingStoreId,
        locationCode: 'BOARD',
        branchId: ids.mainBranchId,
        onHand: '10',
      },
    ],
  };

  const receipt: MReceipt & { _branchId: string } = {
    _branchId: ids.mainBranchId,
    _poLines: [ids.poPartLine1Id, ids.poPartLine1Id],
    id: ids.receiptId,
    number: 'GRN-2026-000001',
    branchId: ids.mainBranchId,
    receivedOn: instantOffset(-10),
    receivedBy: ACTOR_ID,
    supplierId: ids.supplierId,
    supplierName: 'Meridian Pet Supplies Ltd',
    purchaseOrderId: ids.poPartId,
    purchaseOrderNumber: 'PO-2026-000001',
    consignmentId: ids.consignmentClearedId,
    consignmentReference: 'CN-2026-000004',
    deliveryReference: 'DN-88412',
    lines: [
      {
        lineId: 'b2000000-0000-4000-8000-000000000001',
        skuId: ids.foodSkuId,
        skuCode: 'FD-KDC-2KG',
        displayName: 'Kitten dry food, chicken 2 kg',
        stockUnit: 'bags',
        lotNumber: 'L2409E',
        manufacturedOn: dateOffset(-60),
        expiresOn: dateOffset(120),
        quantityAccepted: '30',
        quantityRejected: '10',
        rejectionReason: 'Damaged',
        rejectionNote: 'Ten bags split in transit',
        locationId: ids.warehouseId,
        locationCode: 'WH1',
        holdForInspection: false,
        unitCost: unitCost4(450),
        batchId: ids.batchFoodFarId,
        batchStatus: 'Available',
        storage: ambient,
      },
      {
        lineId: 'b2000000-0000-4000-8000-000000000002',
        skuId: ids.foodSkuId,
        skuCode: 'FD-KDC-2KG',
        displayName: 'Kitten dry food, chicken 2 kg',
        stockUnit: 'bags',
        lotNumber: 'L2408B',
        manufacturedOn: dateOffset(-70),
        expiresOn: dateOffset(45),
        quantityAccepted: '30',
        quantityRejected: '0',
        rejectionReason: null,
        rejectionNote: null,
        locationId: ids.warehouseId,
        locationCode: 'WH1',
        holdForInspection: false,
        unitCost: unitCost4(450),
        batchId: ids.batchFoodSoonId,
        batchStatus: 'Available',
        storage: ambient,
      },
    ],
  };

  const transfer: TransferDetail & { _branchId: string } = {
    _branchId: ids.mainBranchId,
    id: ids.transferId,
    number: 'TRF-2026-000001',
    branchId: ids.mainBranchId,
    fromLocationId: ids.warehouseId,
    fromLocationCode: 'WH1',
    toLocationId: ids.shopFloorId,
    toLocationCode: 'SHOP',
    reason: 'Restock the shop floor',
    transferredOn: instantOffset(-7),
    transferredBy: ACTOR_ID,
    lines: [
      {
        lineId: 'b3000000-0000-4000-8000-000000000001',
        skuId: ids.foodSkuId,
        skuCode: 'FD-KDC-2KG',
        displayName: 'Kitten dry food, chicken 2 kg',
        batchId: ids.batchFoodFarId,
        lotNumber: 'L2409E',
        quantity: '6',
        stockUnit: 'bags',
      },
    ],
  };

  const adjLine = (batchId: string, sku: SkuLookup, lot: string, delta: string, cost: number) => ({
    lineId: `b4000000-0000-4000-8000-${batchId.slice(-12)}`,
    skuId: sku.skuId,
    skuCode: sku.skuCode,
    displayName: sku.displayName,
    batchId,
    lotNumber: lot,
    quantityDelta: delta,
    stockUnit: sku.stockUnit,
    unitCostBase: { amount: cost.toFixed(4), currencyCode: MOCK_BASE_CURRENCY },
  });
  const [foodSku, vaccineSku] = [skus[0]!, skus[1]!];

  const adjustments: MockDb['adjustments'] = [
    {
      _branchId: ids.mainBranchId,
      _requesterId: ACTOR_ID,
      id: ids.adjustmentPostedId,
      number: 'ADJ-2026-000001',
      branchId: ids.mainBranchId,
      locationId: ids.warehouseId,
      locationCode: 'WH1',
      reason: 'Damage',
      note: 'Two bags split open when a pallet tipped.',
      status: 'Posted',
      valueBase: money(925),
      requestedBy: ACTOR_ID,
      requestedOn: instantOffset(-6),
      decidedBy: null,
      decidedOn: null,
      decisionNote: null,
      lines: [adjLine(ids.batchFoodFarId, foodSku, 'L2409E', '-2', 462.5)],
      allowedActions: [],
    },
    {
      _branchId: ids.mainBranchId,
      _requesterId: accountsFixtureIds.nurseStaffId,
      id: ids.adjustmentPendingId,
      number: 'ADJ-2026-000002',
      branchId: ids.mainBranchId,
      locationId: ids.warehouseId,
      locationCode: 'WH1',
      reason: 'Theft',
      note: 'Pallet short on the monthly check; security informed.',
      status: 'PendingApproval',
      valueBase: money(18500),
      requestedBy: accountsFixtureIds.nurseStaffId,
      requestedOn: instantOffset(-1),
      decidedBy: null,
      decidedOn: null,
      decisionNote: null,
      lines: [adjLine(ids.batchFoodSoonId, foodSku, 'L2408B', '-8', 462.5)],
      allowedActions: ['approve', 'reject', 'withdraw'],
    },
    {
      _branchId: ids.mainBranchId,
      _staleOnApprove: true,
      _requesterId: accountsFixtureIds.nurseStaffId,
      id: ids.adjustmentStaleId,
      number: 'ADJ-2026-000003',
      branchId: ids.mainBranchId,
      locationId: ids.dispensaryId,
      locationCode: 'DISP',
      reason: 'Expired',
      note: 'Opened vials past their date.',
      status: 'PendingApproval',
      valueBase: money(12200),
      requestedBy: accountsFixtureIds.nurseStaffId,
      requestedOn: instantOffset(-2),
      decidedBy: null,
      decidedOn: null,
      decisionNote: null,
      lines: [adjLine(ids.batchVaccineId, vaccineSku, 'L7701V', '-40', 30.5)],
      allowedActions: ['approve', 'reject', 'withdraw'],
    },
  ];

  const countLine = (
    sku: SkuLookup,
    batchId: string,
    lot: string,
    expiresOn: string | null,
    counted: string,
    book: string | null,
    variance: string | null,
  ) => ({
    skuId: sku.skuId,
    skuCode: sku.skuCode,
    displayName: sku.displayName,
    batchId,
    lotNumber: lot,
    expiresOn,
    stockUnit: sku.stockUnit,
    countedQuantity: counted,
    bookQuantity: book,
    variance,
  });

  const counts: MockDb['counts'] = [
    {
      _branchId: ids.mainBranchId,
      _variance: null,
      id: ids.countOpenId,
      number: 'CNT-2026-000001',
      branchId: ids.mainBranchId,
      locationId: ids.shopFloorId,
      locationCode: 'SHOP',
      scope: 'FullLocation',
      skuIds: [],
      status: 'Open',
      openedOn: instantOffset(0, 8),
      openedBy: ACTOR_ID,
      submittedOn: null,
      varianceValueBase: null,
      isBlind: true,
      lines: [countLine(foodSku, ids.batchFoodFarId, 'L2409E', dateOffset(120), '6', null, null)],
      allowedActions: ['record-lines', 'submit', 'cancel'],
      version: 2,
    },
    {
      _branchId: ids.mainBranchId,
      _variance: {
        countId: ids.countSubmittedId,
        number: 'CNT-2026-000002',
        status: 'Submitted',
        totalVarianceValueBase: money(-1850),
        varianceLineCount: 2,
        willNeedApproval: false,
        lines: [
          {
            skuId: ids.foodSkuId,
            skuCode: 'FD-KDC-2KG',
            displayName: foodSku.displayName,
            batchId: ids.batchFoodSoonId,
            lotNumber: 'L2408B',
            stockUnit: 'bags',
            bookQuantity: '10',
            countedQuantity: '8',
            variance: '-2',
            value: money(-925),
          },
          {
            skuId: ids.foodSkuId,
            skuCode: 'FD-KDC-2KG',
            displayName: foodSku.displayName,
            batchId: ids.batchFoodFarId,
            lotNumber: 'L2409E',
            stockUnit: 'bags',
            bookQuantity: '18',
            countedQuantity: '16',
            variance: '-2',
            value: money(-925),
          },
        ],
      },
      id: ids.countSubmittedId,
      number: 'CNT-2026-000002',
      branchId: ids.mainBranchId,
      locationId: ids.warehouseId,
      locationCode: 'WH1',
      scope: 'SelectedSkus',
      skuIds: [ids.foodSkuId],
      status: 'Submitted',
      openedOn: instantOffset(-1, 8),
      openedBy: ACTOR_ID,
      submittedOn: instantOffset(-1, 11),
      varianceValueBase: money(-1850),
      isBlind: false,
      lines: [
        countLine(foodSku, ids.batchFoodSoonId, 'L2408B', dateOffset(45), '8', '10', '-2'),
        countLine(foodSku, ids.batchFoodFarId, 'L2409E', dateOffset(120), '16', '18', '-2'),
      ],
      allowedActions: ['commit', 'cancel'],
      version: 4,
    },
    {
      _branchId: ids.mainBranchId,
      _variance: {
        countId: ids.countApprovalId,
        number: 'CNT-2026-000003',
        status: 'AwaitingApproval',
        totalVarianceValueBase: money(-9150),
        varianceLineCount: 1,
        willNeedApproval: true,
        lines: [
          {
            skuId: ids.vaccineSkuId,
            skuCode: 'MED-FVX-TRI',
            displayName: vaccineSku.displayName,
            batchId: ids.batchVaccineId,
            lotNumber: 'L7701V',
            stockUnit: 'ml',
            bookQuantity: '50.5',
            countedQuantity: '20.5',
            variance: '-30',
            value: money(-9150),
          },
        ],
      },
      id: ids.countApprovalId,
      number: 'CNT-2026-000003',
      branchId: ids.mainBranchId,
      locationId: ids.dispensaryId,
      locationCode: 'DISP',
      scope: 'FullLocation',
      skuIds: [],
      status: 'AwaitingApproval',
      openedOn: instantOffset(-3, 8),
      openedBy: accountsFixtureIds.nurseStaffId,
      submittedOn: instantOffset(-3, 12),
      varianceValueBase: money(-9150),
      isBlind: false,
      lines: [
        countLine(vaccineSku, ids.batchVaccineId, 'L7701V', dateOffset(60), '20.5', '50.5', '-30'),
      ],
      allowedActions: ['approve', 'reject'],
      version: 5,
    },
  ];

  return {
    seq: 100,
    locations: [
      location(ids.mainBranchId, ids.warehouseId, 'WH1', 'Main warehouse', 'Warehouse'),
      location(ids.mainBranchId, ids.shopFloorId, 'SHOP', 'Shop floor', 'ShopFloor'),
      location(ids.mainBranchId, ids.dispensaryId, 'DISP', 'Clinic dispensary', 'ClinicDispensary'),
      location(ids.mainBranchId, ids.boardingStoreId, 'BOARD', 'Boarding store', 'BoardingStore'),
      location(ids.mainBranchId, ids.quarantineId, 'QRN', 'Quarantine shelf', 'Quarantine'),
      location(ids.londonBranchId, ids.londonWarehouseId, 'LWH', 'London warehouse', 'Warehouse'),
      location(
        ids.londonBranchId,
        ids.londonQuarantineId,
        'LQRN',
        'London quarantine',
        'Quarantine',
      ),
    ],
    skus,
    storageProfiles: {
      [skus[0]!.productId]: { productId: skus[0]!.productId, storage: ambient, version: 1 },
      [skus[1]!.productId]: { productId: skus[1]!.productId, storage: fridge, version: 2 },
    },
    suppliers: [
      supplier(ids.supplierId, 'MERIDIAN', 'Meridian Pet Supplies Ltd', 'DE', 'BDT', 14, true),
      supplier(ids.supplier2Id, 'VETPHARMA', 'VetPharma Distribution', 'US', 'USD', 21, true),
      supplier(ids.inactiveSupplierId, 'OLDCO', 'Old Co Imports', 'TH', 'BDT', 30, false),
    ],
    purchaseOrders,
    consignments,
    batches,
    positions,
    receipts: [receipt],
    transfers: [transfer],
    adjustments,
    counts,
    ledger,
    alerts,
    breaches,
    recallTraces: { [ids.batchRecalledId]: recallTrace },
    notifiedRuns: {},
    reorderPolicies: [
      {
        branchId: ids.mainBranchId,
        skuId: ids.wetFoodSkuId,
        locationId: ids.warehouseId,
        reorderPoint: 10,
        reorderQuantity: 40,
        preferredSupplierId: ids.supplierId,
        isDefaulted: false,
      },
      {
        branchId: ids.mainBranchId,
        skuId: ids.foodSkuId,
        locationId: ids.shopFloorId,
        reorderPoint: 12,
        reorderQuantity: null,
        preferredSupplierId: ids.supplier2Id,
        isDefaulted: true,
      },
      {
        branchId: ids.mainBranchId,
        skuId: ids.foodSkuId,
        locationId: ids.warehouseId,
        reorderPoint: 20,
        reorderQuantity: null,
        preferredSupplierId: null,
        isDefaulted: true,
      },
    ],
  };
}

function unitCost4(amount: number) {
  return { amount: amount.toFixed(4), currencyCode: MOCK_BASE_CURRENCY };
}

const persisted = loadPersisted<MockDb>(PERSIST_KEY);
export const db: MockDb = persisted ?? seed();

/** Writes the whole store to `sessionStorage` (a no-op outside a real browser). */
export function persist(): void {
  savePersisted(PERSIST_KEY, db);
}

export function resetInventoryMockData(): void {
  clearPersisted(PERSIST_KEY);
  Object.assign(db, seed());
}

/** A monotonically increasing number for new ids and document numbers. */
export function nextSeq(): number {
  db.seq += 1;
  return db.seq;
}

export function newId(prefix: string): string {
  return `${prefix}0000000-0000-4000-8000-${String(nextSeq()).padStart(12, '0')}`;
}

export function docNumber(prefix: string): string {
  return `${prefix}-${new Date().getFullYear()}-${String(nextSeq()).padStart(6, '0')}`;
}
