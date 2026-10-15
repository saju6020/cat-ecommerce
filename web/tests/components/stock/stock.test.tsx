import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SkuStockMatrix } from '@/components/stock/SkuStockMatrix';
import { FefoPreviewPanel } from '@/components/stock/FefoPreviewPanel';
import { BatchDetail } from '@/components/stock/BatchDetail';
import { LedgerSearch } from '@/components/stock/LedgerSearch';
import { ApiValidationError } from '@/lib/api/client';
import type {
  BatchDetail as BatchDetailData,
  FefoPreview,
  LedgerEntry,
  SkuStockMatrix as Matrix,
} from '@/lib/api/inventory-stock';
import { renderWithClient } from '../inventory/testUtils';

vi.mock('next/navigation', async () => {
  const { navigation } = await import('../inventory/testUtils');
  return {
    useRouter: () => ({ push: navigation.push, replace: navigation.replace }),
    usePathname: () => navigation.pathname,
    useSearchParams: () => navigation.search,
  };
});

const date = (offset: number) => {
  const d = new Date();
  d.setDate(d.getDate() + offset);
  return d.toISOString().slice(0, 10);
};
const cell = (code: string, kind: string, onHand: string, reserved = '0') => ({
  locationId: code,
  locationCode: code,
  locationKind: kind,
  onHand,
  reserved,
});

const matrix: Matrix = {
  skuId: 'k1',
  skuCode: 'FD-KDC-2KG',
  displayName: 'Kitten dry food',
  stockUnit: 'bags',
  storage: null,
  branchTimeZoneId: 'Asia/Dhaka',
  batches: [
    {
      batchId: 'a',
      lotNumber: 'L-GOOD',
      expiresOn: date(120),
      isExpired: false,
      daysToExpiry: 120,
      status: 'Available',
      locations: [cell('WH1', 'Warehouse', '18', '2'), cell('SHOP', 'ShopFloor', '6')],
    },
    {
      batchId: 'b',
      lotNumber: 'L-OLD',
      expiresOn: date(-3),
      isExpired: true,
      daysToExpiry: -3,
      status: 'Available',
      locations: [cell('WH1', 'Warehouse', '4')],
    },
    {
      batchId: 'c',
      lotNumber: 'L-HOLD',
      expiresOn: date(200),
      isExpired: false,
      daysToExpiry: 200,
      status: 'Quarantined',
      locations: [cell('WH1', 'Warehouse', '5')],
    },
    {
      batchId: 'd',
      lotNumber: 'L-RCL',
      expiresOn: date(30),
      isExpired: false,
      daysToExpiry: 30,
      status: 'Recalled',
      locations: [cell('QRN', 'Quarantine', '2')],
    },
  ],
};

function stubWidth(width: number) {
  class FakeObserver {
    constructor(private readonly callback: () => void) {}
    observe() {
      this.callback();
    }
    disconnect() {}
    unobserve() {}
  }
  vi.stubGlobal('ResizeObserver', FakeObserver);
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockReturnValue({
    width,
    height: 0,
    top: 0,
    left: 0,
    right: width,
    bottom: 0,
    x: 0,
    y: 0,
    toJSON: () => ({}),
  });
}

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('U-D1 / U-D2 — SkuStockMatrix', () => {
  it('U-D1: at desktop width it is a real table with row and column headers', () => {
    stubWidth(1024);
    render(<SkuStockMatrix matrix={matrix} />);
    const table = screen.getByRole('table', {
      name: /stock of kitten dry food by batch and location/i,
    });
    expect(
      within(table)
        .getAllByRole('columnheader')
        .map((h) => h.textContent),
    ).toEqual(
      expect.arrayContaining([
        expect.stringMatching(/WH1/),
        expect.stringMatching(/SHOP/),
        expect.stringMatching(/QRN/),
      ]),
    );
    expect(within(table).getAllByRole('rowheader')).toHaveLength(4);
    expect(within(table).getByText('18 bags on hand, 2 bags reserved')).toBeInTheDocument();
  });

  it('U-D1: at 360 px it becomes a per-batch list and the table is gone', () => {
    stubWidth(340);
    render(<SkuStockMatrix matrix={matrix} />);
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.getByRole('list', { name: /batches of kitten dry food/i })).toBeInTheDocument();
    expect(screen.getAllByRole('listitem')).toHaveLength(4);
  });

  it('U-D2: an expired, a quarantined and a recalled batch are each labelled with their reason', () => {
    stubWidth(1024);
    render(<SkuStockMatrix matrix={matrix} />);
    expect(screen.getAllByText(/expired 3 days ago/i).length).toBeGreaterThan(0);
    expect(screen.getByText('Quarantined')).toBeInTheDocument();
    expect(screen.getByText('Recalled — held')).toBeInTheDocument();
    expect(screen.getAllByText(/cannot be sold, dispensed or fed/i).length).toBeGreaterThan(0);
    expect(screen.getByText(/in quarantine location/i)).toBeInTheDocument();
    // No action that would use an unusable batch is offered — the only controls are links to the batch.
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});

describe('U-D3 — FefoPreviewPanel', () => {
  const plan: FefoPreview = {
    stockUnit: 'bags',
    shortfall: '0',
    lines: [
      {
        batchId: 'z',
        lotNumber: 'L-LATER',
        expiresOn: date(90),
        locationId: 'w',
        locationCode: 'WH1',
        quantity: '4',
      },
      {
        batchId: 'y',
        lotNumber: 'L-SOONER',
        expiresOn: date(10),
        locationId: 's',
        locationCode: 'SHOP',
        quantity: '2',
      },
    ],
  };

  it('lists the plan exactly as the server returned it — never re-sorted', () => {
    render(
      <FefoPreviewPanel
        stockUnit="bags"
        allowsFractional={false}
        locations={[]}
        preview={plan}
        isLoading={false}
        error={null}
        onPreview={() => undefined}
      />,
    );
    const table = screen.getByRole('table', { name: /pick order/i });
    const lots = within(table)
      .getAllByRole('row')
      .slice(1)
      .map((r) => r.textContent ?? '');
    expect(lots[0]).toMatch(/L-LATER/);
    expect(lots[1]).toMatch(/L-SOONER/);
    expect(screen.getByText('This is what would be picked next.')).toBeInTheDocument();
    expect(screen.getByText('4 bags')).toBeInTheDocument();
  });

  it('states a shortfall with its unit', () => {
    render(
      <FefoPreviewPanel
        stockUnit="bags"
        allowsFractional={false}
        locations={[]}
        preview={{ ...plan, shortfall: '3' }}
        isLoading={false}
        error={null}
        onPreview={() => undefined}
      />,
    );
    expect(screen.getByText(/short by 3 bags/i)).toBeInTheDocument();
  });

  it("shows the server's refusal in its own words", () => {
    const error = new ApiValidationError({
      isValid: false,
      errors: [
        {
          propertyName: 'locationId',
          errorCode: 'MovementNotPermittedAtLocation',
          errorMessage:
            "Clinic dispensary is a ClinicDispensary — that movement isn't allowed there.",
          resourceName: 'Inventory',
        },
      ],
    });
    render(
      <FefoPreviewPanel
        stockUnit="bags"
        allowsFractional={false}
        locations={[]}
        preview={undefined}
        isLoading={false}
        error={error}
        onPreview={() => undefined}
      />,
    );
    expect(screen.getByRole('alert')).toHaveTextContent(/that movement isn't allowed there/i);
    expect(document.body).not.toHaveTextContent('MovementNotPermittedAtLocation');
  });

  it('asks for a valid quantity before previewing', async () => {
    const user = userEvent.setup();
    const onPreview = vi.fn();
    render(
      <FefoPreviewPanel
        stockUnit="bags"
        allowsFractional={false}
        locations={[]}
        preview={undefined}
        isLoading={false}
        error={null}
        onPreview={onPreview}
      />,
    );
    const quantity = screen.getByLabelText(/quantity/i);
    await user.clear(quantity);
    await user.type(quantity, '1.5');
    await user.click(screen.getByRole('button', { name: /preview/i }));
    expect(onPreview).not.toHaveBeenCalled();
    await user.clear(quantity);
    await user.type(quantity, '4');
    await user.click(screen.getByRole('button', { name: /preview/i }));
    expect(onPreview).toHaveBeenCalledWith({
      quantity: '4',
      purpose: 'Sale',
      locationId: undefined,
    });
  });
});

function batch(overrides: Partial<BatchDetailData> = {}): BatchDetailData {
  return {
    id: 'b1',
    skuId: 'k1',
    skuCode: 'FD-KDC-2KG',
    displayName: 'Kitten dry food',
    stockUnit: 'bags',
    stockClass: 'Food',
    branchId: 'br',
    lotNumber: 'L2409E',
    manufacturedOn: date(-60),
    expiresOn: date(120),
    isExpired: false,
    daysToExpiry: 120,
    branchTimeZoneId: 'Asia/Dhaka',
    status: 'Available',
    statusReason: null,
    receivedOn: '2026-09-01T04:00:00Z',
    receivedQuantity: '30',
    goodsReceiptId: 'r1',
    goodsReceiptNumber: 'GRN-2026-000001',
    consignmentId: null,
    consignmentReference: null,
    supplierId: 's1',
    originCountryCode: 'DE',
    purchaseUnitCost: { amount: '450.0000', currencyCode: 'BDT' },
    unitCostBase: { amount: '462.5000', currencyCode: 'BDT' },
    costBasis: 'landed',
    positions: [
      {
        locationId: 'l1',
        locationCode: 'WH1',
        locationKind: 'Warehouse',
        onHand: '18',
        reserved: '2',
      },
    ],
    recall: null,
    statusHistory: [],
    allowedActions: ['quarantine', 'write-off'],
    storage: null,
    version: 1,
    ...overrides,
  };
}
const noopAsync = () => Promise.resolve();

describe('U-D4 / U-D5 — BatchDetail', () => {
  it('U-D4: only the actions in allowedActions render', () => {
    renderWithClient(
      <BatchDetail
        batch={batch()}
        timeZoneId="Asia/Dhaka"
        onQuarantine={noopAsync}
        onRelease={noopAsync}
        onWriteOff={noopAsync}
        onDeclareRecall={noopAsync}
      />,
    );
    expect(screen.getByRole('button', { name: 'Quarantine' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Write off' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Release' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /recall/i })).not.toBeInTheDocument();
  });

  it('U-D4: an expired batch says why and offers no action to use it', () => {
    renderWithClient(
      <BatchDetail
        batch={batch({
          isExpired: true,
          daysToExpiry: -3,
          expiresOn: date(-3),
          allowedActions: ['write-off'],
        })}
        timeZoneId="Asia/Dhaka"
        onQuarantine={noopAsync}
        onRelease={noopAsync}
        onWriteOff={noopAsync}
        onDeclareRecall={noopAsync}
      />,
    );
    expect(
      screen.getAllByText(/expired — cannot be sold, dispensed or fed/i).length,
    ).toBeGreaterThan(0);
    expect(screen.getAllByRole('button').map((b) => b.textContent)).toEqual(['Write off']);
  });

  it('U-D4: quarantine confirm names the batch and lot, and posts the reason', async () => {
    const user = userEvent.setup();
    const onQuarantine = vi.fn().mockResolvedValue(undefined);
    renderWithClient(
      <BatchDetail
        batch={batch()}
        timeZoneId="Asia/Dhaka"
        onQuarantine={onQuarantine}
        onRelease={noopAsync}
        onWriteOff={noopAsync}
        onDeclareRecall={noopAsync}
      />,
    );
    await user.click(screen.getByRole('button', { name: 'Quarantine' }));
    const dialog = await screen.findByRole('dialog');
    expect(
      within(dialog).getByText(/quarantine lot L2409E of Kitten dry food/i),
    ).toBeInTheDocument();
    await user.type(within(dialog).getByLabelText(/reason/i), 'Torn packaging');
    await user.click(within(dialog).getByRole('button', { name: /quarantine this batch/i }));
    await waitFor(() => expect(onQuarantine).toHaveBeenCalledWith('Torn packaging'));
  });

  it('U-D5: without cost access (null cost fields) the cost panel is absent and the page still renders', () => {
    renderWithClient(
      <BatchDetail
        batch={batch({ purchaseUnitCost: null, unitCostBase: null, costBasis: null })}
        timeZoneId="Asia/Dhaka"
        onQuarantine={noopAsync}
        onRelease={noopAsync}
        onWriteOff={noopAsync}
        onDeclareRecall={noopAsync}
      />,
    );
    expect(screen.queryByRole('heading', { name: /^cost$/i })).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /lot L2409E/i })).toBeInTheDocument();
  });
});

describe('U-D6 — LedgerSearch', () => {
  const entry = (over: Partial<LedgerEntry>): LedgerEntry => ({
    sequence: 1,
    occurredOn: '2026-10-05T04:00:00Z',
    kind: 'Sale',
    quantityDelta: '-2',
    stockUnit: 'bags',
    locationId: 'l',
    locationCode: 'SHOP',
    batchId: 'b',
    lotNumber: 'L1',
    skuId: 'k',
    skuCode: 'FD-KDC-2KG',
    referenceKind: 'CustomerOrder',
    referenceId: 'o',
    referenceLabel: 'Order PC-001234',
    fefoOverridden: false,
    overrideReason: null,
    performedBy: 'u',
    performedByName: 'Rezwan Ahmed',
    note: null,
    ...over,
  });

  it('shows a FEFO override with its reason, signed quantities with units, and no edit or delete control', () => {
    render(
      <LedgerSearch
        rows={[
          entry({ fefoOverridden: true, overrideReason: 'Customer asked for the newest lot.' }),
          entry({ sequence: 2, kind: 'Receipt', quantityDelta: '30' }),
        ]}
        totalCount={2}
        pageSize={50}
        isLoading={false}
        isError={false}
        onRetry={() => undefined}
        rangeProblem={null}
        exportPath="/api/staff/inventory/ledger/export.csv?from=a&to=b"
        timeZoneId="Asia/Dhaka"
        idFilters={[]}
      />,
    );
    expect(screen.getByText(/overridden — customer asked for the newest lot/i)).toBeInTheDocument();
    expect(screen.getByText('-2 bags')).toBeInTheDocument();
    expect(screen.getByText('+30 bags')).toBeInTheDocument();
    expect(screen.getByText(/ledger entries are permanent/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /edit|delete|remove/i })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /export csv/i })).toBeInTheDocument();
  });

  it('a range the server would refuse is stated, and no table is shown', () => {
    render(
      <LedgerSearch
        rows={undefined}
        totalCount={0}
        pageSize={50}
        isLoading={false}
        isError={false}
        onRetry={() => undefined}
        rangeProblem="The range cannot exceed 366 days."
        exportPath="x"
        timeZoneId="Asia/Dhaka"
        idFilters={[]}
      />,
    );
    expect(screen.getByRole('alert')).toHaveTextContent('The range cannot exceed 366 days.');
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });
});
