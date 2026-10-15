import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PurchaseOrderDetail } from '@/components/procurement/PurchaseOrderDetail';
import { ConsignmentDocuments } from '@/components/procurement/ConsignmentDocuments';
import { LandedCostPanel } from '@/components/procurement/LandedCostPanel';
import { ConsignmentCosts } from '@/components/procurement/ConsignmentCosts';
import { InspectionPack } from '@/components/procurement/InspectionPack';
import { ReorderSuggestions } from '@/components/procurement/ReorderSuggestions';
import { ApiConflictError } from '@/lib/api/client';
import type { PurchaseOrderDetail as PoDetail } from '@/lib/api/procurement-orders';
import type {
  ConsignmentDocument,
  InspectionPack as Pack,
  LandedCost,
} from '@/lib/api/procurement-consignments';
import type { ReorderSuggestion } from '@/lib/api/procurement-reorder';

vi.mock('next/navigation', async () => {
  const { navigation } = await import('../inventory/testUtils');
  return {
    useRouter: () => ({ push: navigation.push, replace: navigation.replace }),
    usePathname: () => navigation.pathname,
    useSearchParams: () => navigation.search,
  };
});

const money = (amount: string, currencyCode = 'BDT') => ({ amount, currencyCode });

function order(overrides: Partial<PoDetail> = {}): PoDetail {
  return {
    id: 'po1',
    number: 'PO-2026-000001',
    supplierId: 's1',
    supplierName: 'Meridian Pet Supplies Ltd',
    branchId: 'b1',
    deliverToLocationId: 'l1',
    currencyCode: 'BDT',
    expectedDeliveryOn: '2026-10-20',
    notes: null,
    status: 'Issued',
    statusReason: null,
    receiptState: 'PartiallyReceived',
    issuedOn: '2026-10-01T04:00:00Z',
    totalAmount: money('45000.00'),
    lines: [
      {
        lineId: 'pl1',
        skuId: 'k1',
        skuCode: 'FD-KDC-2KG',
        displayName: 'Kitten dry food',
        stockUnit: 'bags',
        orderedQuantity: '100',
        acceptedQuantity: '60',
        rejectedQuantity: '10',
        outstandingQuantity: '40',
        unitCost: money('450.0000'),
        lineTotal: money('45000.00'),
      },
    ],
    receipts: [
      { goodsReceiptId: 'r1', number: 'GRN-2026-000001', receivedOn: '2026-10-05T04:00:00Z' },
    ],
    allowedActions: ['receive', 'close'],
    version: 3,
    ...overrides,
  };
}

const noop = () => Promise.resolve();

describe('U-B1 / U-B2 — PurchaseOrderDetail', () => {
  it('U-B1: ordered 100, accepted 60, rejected 10 shows outstanding 40 with units', () => {
    render(
      <PurchaseOrderDetail
        order={order()}
        deliverTo="Main warehouse"
        timeZoneId="Asia/Dhaka"
        onIssue={noop}
        onCancel={noop}
        onClose={noop}
      />,
    );
    const table = screen.getByRole('table', { name: /lines of PO-2026-000001/i });
    expect(within(table).getByText('100 bags')).toBeInTheDocument();
    expect(within(table).getByText('60 bags')).toBeInTheDocument();
    expect(within(table).getByText('10 bags')).toBeInTheDocument();
    expect(within(table).getByText('40 bags')).toBeInTheDocument();
    expect(within(table).getByRole('columnheader', { name: /outstanding/i })).toBeInTheDocument();
  });

  it('U-B2: Cancel is disabled with the explanation when the order has receipts', () => {
    render(
      <PurchaseOrderDetail
        order={order()}
        deliverTo="x"
        timeZoneId="Asia/Dhaka"
        onIssue={noop}
        onCancel={noop}
        onClose={noop}
      />,
    );
    const cancel = screen.getByRole('button', { name: /cancel order/i });
    expect(cancel).toBeDisabled();
    expect(cancel).toHaveAccessibleDescription(/already has receipts/i);
  });

  it('U-B2: the server 400 on a cancel attempt is rendered in the dialog', async () => {
    const user = userEvent.setup();
    const onCancel = vi.fn().mockRejectedValue(Object.assign(new Error('x'), {}));
    render(
      <PurchaseOrderDetail
        order={order({
          receipts: [],
          receiptState: 'NotReceived',
          allowedActions: ['receive', 'close', 'cancel'],
        })}
        deliverTo="x"
        timeZoneId="Asia/Dhaka"
        onIssue={noop}
        onCancel={onCancel}
        onClose={noop}
      />,
    );
    await user.click(screen.getByRole('button', { name: /cancel order/i }));
    const dialog = await screen.findByRole('dialog');
    await user.type(
      within(dialog).getByLabelText(/reason for cancelling/i),
      'Supplier cannot deliver',
    );
    await user.click(within(dialog).getByRole('button', { name: /^cancel order$/i }));
    await waitFor(() => expect(onCancel).toHaveBeenCalledWith('Supplier cannot deliver'));
    expect(await within(dialog).findByRole('alert')).toBeInTheDocument();
  });

  it('Close short needs a reason', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn().mockResolvedValue(undefined);
    render(
      <PurchaseOrderDetail
        order={order()}
        deliverTo="x"
        timeZoneId="Asia/Dhaka"
        onIssue={noop}
        onCancel={noop}
        onClose={onClose}
      />,
    );
    await user.click(screen.getByRole('button', { name: /close short/i }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: /close order short/i }));
    expect(onClose).not.toHaveBeenCalled();
    expect(await within(dialog).findByText(/enter the reason/i)).toBeInTheDocument();
  });
});

const documents: ConsignmentDocument[] = [
  {
    documentId: 'd1',
    kind: 'ImportPermit',
    documentNumber: 'IP-88000',
    issuedOn: '2026-09-01',
    fileName: 'permit-old.pdf',
    storageKey: 'import/d1.pdf',
    contentType: 'application/pdf',
    sizeInBytes: 184320,
    uploadedBy: 'u1',
    uploadedOn: '2026-09-02T04:00:00Z',
    isSuperseded: true,
    supersededOn: '2026-09-20T04:00:00Z',
    supersededBy: 'u1',
  },
  {
    documentId: 'd2',
    kind: 'ImportPermit',
    documentNumber: 'IP-88001',
    issuedOn: '2026-09-18',
    fileName: 'permit.pdf',
    storageKey: 'import/d2.pdf',
    contentType: 'application/pdf',
    sizeInBytes: 200000,
    uploadedBy: 'u1',
    uploadedOn: '2026-09-20T04:00:00Z',
    isSuperseded: false,
    supersededOn: null,
    supersededBy: null,
  },
];

describe('U-B3 / U-B4 — ConsignmentDocuments', () => {
  it('U-B3: a superseded document stays listed and labelled; there is no delete control', () => {
    render(
      <ConsignmentDocuments
        documents={documents}
        canAdd
        timeZoneId="Asia/Dhaka"
        onAdd={noop}
        onSupersede={noop}
      />,
    );
    expect(screen.getByText('IP-88000')).toBeInTheDocument();
    expect(screen.getByText(/superseded/i)).toBeInTheDocument();
    expect(screen.getByText('IP-88001')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /delete|remove/i })).not.toBeInTheDocument();
  });

  it('U-B4: the add form carries the honest storage-key note and no file input', () => {
    const { container } = render(
      <ConsignmentDocuments
        documents={[]}
        canAdd
        timeZoneId="Asia/Dhaka"
        onAdd={noop}
        onSupersede={noop}
      />,
    );
    expect(screen.getByText(/file upload is not available yet/i)).toBeInTheDocument();
    expect(container.querySelector('input[type="file"]')).toBeNull();
    expect(screen.getByLabelText(/stored at/i)).toBeInTheDocument();
  });

  it('records a document with its metadata', async () => {
    const user = userEvent.setup();
    const onAdd = vi.fn().mockResolvedValue(undefined);
    render(
      <ConsignmentDocuments
        documents={[]}
        canAdd
        timeZoneId="Asia/Dhaka"
        onAdd={onAdd}
        onSupersede={noop}
      />,
    );
    await user.selectOptions(screen.getByLabelText(/^kind/i), 'BillOfLading');
    await user.type(screen.getByLabelText(/file name/i), 'bol.pdf');
    await user.type(screen.getByLabelText(/size \(bytes\)/i), '1024');
    await user.type(screen.getByLabelText(/stored at/i), 'import/bol.pdf');
    await user.click(screen.getByRole('button', { name: /record document/i }));
    await waitFor(() => expect(onAdd).toHaveBeenCalled());
    expect(onAdd.mock.calls[0]![0]).toMatchObject({
      kind: 'BillOfLading',
      fileName: 'bol.pdf',
      sizeInBytes: 1024,
      storageKey: 'import/bol.pdf',
      contentType: 'application/pdf',
    });
  });
});

const landed: LandedCost = {
  allocatedOn: '2026-10-01T04:00:00Z',
  isStale: true,
  totalCostsBase: money('2250.00'),
  lines: [
    {
      batchId: 'b1',
      skuCode: 'FD-KDC-2KG',
      lotNumber: 'L2409E',
      quantity: '30',
      purchaseCostBase: money('13500.00'),
      allocatedCostBase: money('1125.00'),
      landedUnitCostBase: money('487.5000'),
    },
    {
      batchId: 'b2',
      skuCode: 'FD-KDC-2KG',
      lotNumber: 'L2408B',
      quantity: '30',
      purchaseCostBase: money('13500.00'),
      allocatedCostBase: money('1125.00'),
      landedUnitCostBase: money('487.5000'),
    },
  ],
};

describe('U-B5 — LandedCostPanel', () => {
  it('shows the stale banner, names the batch count in the confirm and shows 4-dp landed unit costs', async () => {
    const user = userEvent.setup();
    const onAllocate = vi.fn().mockResolvedValue(undefined);
    render(
      <LandedCostPanel
        landed={landed}
        unitsByBatch={{ b1: 'bags', b2: 'bags' }}
        batchCount={2}
        canAllocate
        timeZoneId="Asia/Dhaka"
        onAllocate={onAllocate}
      />,
    );
    expect(
      screen.getByText(/costs or batches changed since the last allocation/i),
    ).toBeInTheDocument();
    expect(screen.getAllByText(/487\.5000/).length).toBeGreaterThan(0);
    expect(screen.getAllByText('30 bags').length).toBe(2);
    await user.click(screen.getByRole('button', { name: /allocate again/i }));
    const dialog = await screen.findByRole('dialog');
    expect(
      within(dialog).getByText('This recalculates the landed unit cost of 2 batches.'),
    ).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: /allocate landed cost/i }));
    await waitFor(() => expect(onAllocate).toHaveBeenCalled());
  });
});

describe('U-B6 — ConsignmentCosts save with a 409', () => {
  it('says someone else changed the costs and keeps the typed values', async () => {
    const user = userEvent.setup();
    const onSave = vi.fn().mockRejectedValue(new ApiConflictError());
    render(
      <ConsignmentCosts
        costComponents={[{ kind: 'Freight', amount: money('1200.00', 'USD'), note: null }]}
        exchangeRates={[{ currencyCode: 'USD', rateToBase: '110', recordedOn: '2026-10-01' }]}
        version={4}
        baseCurrency="BDT"
        today="2026-10-07"
        readOnly={false}
        isPending={false}
        onSave={onSave}
        onReload={() => undefined}
      />,
    );
    const amount = screen.getByLabelText(/^amount/i);
    await user.clear(amount);
    await user.type(amount, '1300.00');
    await user.click(screen.getByRole('button', { name: /save costs and rates/i }));
    expect(
      await screen.findByText(/someone else changed these costs — reload/i),
    ).toBeInTheDocument();
    expect(onSave.mock.calls[0]![0]).toMatchObject({ expectedVersion: 4 });
    expect(screen.getByLabelText(/^amount/i)).toHaveValue('1300.00');
  });
});

describe('U-B7 — InspectionPack', () => {
  it('has details, POs, every document (superseded too) and the batches on one page', () => {
    const pack: Pack = {
      consignmentId: 'c1',
      reference: 'CN-2026-000004',
      status: 'Cleared',
      originCountryCode: 'DE',
      shipmentReference: 'SHP-004',
      importPermitNumber: 'IP-88001',
      healthCertificateNumber: 'HC-54001',
      customsDeclarationNumber: 'CD-2026-7781',
      expectedArrivalOn: '2026-09-30',
      clearedOn: '2026-10-01',
      hsCodes: [{ code: '2309.10', description: 'Cat food' }],
      purchaseOrders: [
        {
          id: 'po1',
          number: 'PO-2026-000001',
          supplierId: 's1',
          supplierName: 'Meridian',
          status: 'Issued',
        },
      ],
      documents,
      batches: [
        {
          batchId: 'b1',
          skuId: 'k1',
          skuCode: 'FD-KDC-2KG',
          displayName: 'Kitten dry food',
          lotNumber: 'L2409E',
          expiresOn: '2027-01-30',
          receivedQuantity: '30',
          stockUnit: 'bags',
          status: 'Available',
          goodsReceiptNumber: 'GRN-2026-000001',
        },
      ],
    };
    render(<InspectionPack pack={pack} />);
    expect(screen.getByText('IP-88001', { selector: 'dd' })).toBeInTheDocument();
    expect(screen.getByText('PO-2026-000001 — Meridian')).toBeInTheDocument();
    expect(screen.getByText('IP-88000')).toBeInTheDocument();
    expect(screen.getByText('Superseded')).toBeInTheDocument();
    expect(screen.getByText('30 bags')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /print this pack/i })).toBeInTheDocument();
  });
});

function suggestion(over: Partial<ReorderSuggestion>): ReorderSuggestion {
  return {
    skuId: 'k1',
    skuCode: 'FD-ADT-POUCH',
    displayName: 'Adult wet food',
    stockUnit: 'pouches',
    locationId: 'l1',
    locationCode: 'WH1',
    usable: '8',
    onOrder: '0',
    reorderPoint: '10',
    isDefaultedPoint: false,
    averageDailyConsumption: '1.5',
    leadTimeDays: 14,
    coverDays: 14,
    suggestedQuantity: '40',
    preferredSupplierId: 's1',
    preferredSupplierName: 'Meridian',
    ...over,
  };
}

describe('U-B8 — ReorderSuggestions', () => {
  it('renders the formula from the payload and builds one order per supplier', async () => {
    const user = userEvent.setup();
    render(
      <ReorderSuggestions
        items={[
          suggestion({}),
          suggestion({
            skuId: 'k2',
            skuCode: 'FD-KDC-2KG',
            displayName: 'Kitten food',
            stockUnit: 'bags',
            preferredSupplierId: 's2',
            preferredSupplierName: 'VetPharma',
            suggestedQuantity: '25',
          }),
        ]}
      />,
    );
    await user.click(screen.getByText(/how is the suggested quantity worked out/i));
    expect(
      screen.getByText(/1\.5 pouches a day, 14 days lead time plus 14 days cover/i),
    ).toBeInTheDocument();
    await user.click(screen.getByRole('checkbox', { name: /select adult wet food/i }));
    await user.click(screen.getByRole('checkbox', { name: /select kitten food/i }));
    const links = screen.getAllByRole('link', { name: /build the order for/i });
    expect(links).toHaveLength(2);
    expect(links[0]!.getAttribute('href')).toContain('lines=FD-ADT-POUCH%3A40');
    expect(screen.getByText(/2 purchase orders will be built/i)).toBeInTheDocument();
  });

  it('does not offer a second location of an item already selected', async () => {
    const user = userEvent.setup();
    render(
      <ReorderSuggestions
        items={[suggestion({}), suggestion({ locationId: 'l2', locationCode: 'SHOP' })]}
      />,
    );
    const boxes = screen.getAllByRole('checkbox');
    await user.click(boxes[0]!);
    expect(boxes[1]).toBeDisabled();
  });
});
