import { readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { SupplierForm } from '@/components/procurement/SupplierForm';
import { StorageProfileForm } from '@/components/inventory/StorageProfileForm';
import { StockTable } from '@/components/stock/StockTable';
import { ReceiptDetail } from '@/components/inventory/ReceiptDetail';
import { PurchaseOrderDetail } from '@/components/procurement/PurchaseOrderDetail';
import { CountVariance } from '@/components/inventory/CountVariance';
import { SkuStockMatrix } from '@/components/stock/SkuStockMatrix';
import SuppliersPage from '@/app/(staff)/staff/procurement/suppliers/page';
import NewReceiptPage from '@/app/(staff)/staff/inventory/receipts/new/page';
import { ApiValidationError } from '@/lib/api/client';
import { resetInventoryMockData } from '@/mocks/handlers/inventory';
import { renderWithClient } from './testUtils';

vi.mock('next/navigation', async () => {
  const { navigation } = await import('./testUtils');
  return {
    useRouter: () => ({ push: navigation.push, replace: navigation.replace }),
    usePathname: () => navigation.pathname,
    useSearchParams: () => navigation.search,
  };
});

beforeEach(() => resetInventoryMockData());

const violation = (propertyName: string, errorCode: string, errorMessage: string) => ({
  propertyName,
  errorCode,
  errorMessage,
  resourceName: 'Inventory',
});

describe('U-X1 — a server 400 is mapped to its field and no errorCode reaches the DOM', () => {
  it('supplier form: a taken code lands on the code field', async () => {
    const user = userEvent.setup();
    const onSubmit = vi
      .fn()
      .mockRejectedValue(
        new ApiValidationError({
          isValid: false,
          errors: [
            violation('Code', 'SupplierCodeAlreadyExists', 'This supplier code is already in use.'),
          ],
        }),
      );
    renderWithClient(
      <SupplierForm submitLabel="Create supplier" isPending={false} onSubmit={onSubmit} />,
    );
    await user.type(screen.getByLabelText(/^code/i), 'DUPLICATE');
    await user.type(screen.getByLabelText(/^name/i), 'Dup Ltd');
    await user.selectOptions(screen.getByLabelText(/^country/i), 'DE');
    await user.type(screen.getByLabelText(/payment terms/i), 'Net 30');
    await user.click(screen.getByRole('button', { name: /create supplier/i }));
    const code = screen.getByLabelText(/^code/i);
    await waitFor(() => expect(code).toHaveAttribute('aria-invalid', 'true'));
    expect(document.getElementById('supplier-code-error')).toHaveTextContent(
      'This supplier code is already in use.',
    );
    expect(document.body).not.toHaveTextContent('SupplierCodeAlreadyExists');
    expect(screen.getByLabelText(/^name/i)).toHaveValue('Dup Ltd'); // typed values are kept (NFR-USA-5)
  });

  it('storage profile: InvalidTemperatureRange lands on the minimum field', async () => {
    const user = userEvent.setup();
    const onSave = vi
      .fn()
      .mockRejectedValue(
        new ApiValidationError({
          isValid: false,
          errors: [
            violation(
              'MinTemperatureC',
              'InvalidTemperatureRange',
              'The minimum temperature must be lower than the maximum.',
            ),
          ],
        }),
      );
    renderWithClient(
      <StorageProfileForm
        productLabel="Vaccine"
        initial={null}
        onSave={onSave}
        isPending={false}
      />,
    );
    await user.type(screen.getByLabelText(/minimum temperature/i), '9');
    await user.type(screen.getByLabelText(/maximum temperature/i), '4');
    await user.click(screen.getByRole('button', { name: /save storage profile/i }));
    await waitFor(() =>
      expect(screen.getByLabelText(/minimum temperature/i)).toHaveAttribute('aria-invalid', 'true'),
    );
    expect(document.getElementById('storage-min-error')).toHaveTextContent(
      'The minimum temperature must be lower than the maximum.',
    );
    expect(document.body).not.toHaveTextContent('InvalidTemperatureRange');
  });
});

describe('U-X2 — a dirty form asks before it is left', () => {
  it('supplier form: typing then following a link opens the unsaved-changes confirm', async () => {
    const user = userEvent.setup();
    render(
      <>
        <a href="/staff/inventory">Elsewhere</a>
        <SupplierForm
          submitLabel="Create supplier"
          isPending={false}
          onSubmit={async () => undefined}
        />
      </>,
    );
    await user.type(screen.getByLabelText(/^name/i), 'Meridian');
    await user.click(screen.getByRole('link', { name: 'Elsewhere' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('Leave this page?')).toBeInTheDocument();
  });
});

describe('U-X3 — no quantity without its unit', () => {
  function bareNumbers(container: HTMLElement): string[] {
    // A bare figure inside a data cell or definition (never a row/column header index or a count).
    return [...container.querySelectorAll('td, dd, li, strong')]
      .map((el) => (el.children.length === 0 ? (el.textContent ?? '').trim() : ''))
      .filter((text) => /^[+-]?\d+(\.\d+)?$/.test(text));
  }

  it('the stock table, receipt detail, order detail, variance report and matrix show units on every figure', () => {
    stubNarrow(false);
    const stock = render(
      <StockTable
        rows={[
          {
            skuId: 'k',
            skuCode: 'FD-KDC-2KG',
            displayName: 'Kitten dry food',
            stockUnit: 'bags',
            stockClass: 'Food',
            onHand: '37',
            reserved: '2',
            usable: '30',
            sellable: '24',
            expiredQuantity: '4',
            heldQuantity: '5',
            nextExpiresOn: '2026-11-21',
            storage: null,
          },
        ]}
        totalCount={1}
        pageSize={25}
        isLoading={false}
        isError={false}
        onRetry={() => undefined}
      />,
    );
    expect(bareNumbers(stock.container)).toEqual([]);
    expect(stock.container).toHaveTextContent('37 bags');
    stock.unmount();

    const receipt = renderWithClient(
      <ReceiptDetail
        timeZoneId="Asia/Dhaka"
        receipt={{
          id: 'r',
          number: 'GRN-1',
          branchId: 'b',
          receivedOn: '2026-10-05T04:00:00Z',
          receivedBy: 'u',
          supplierId: null,
          supplierName: null,
          purchaseOrderId: null,
          purchaseOrderNumber: null,
          consignmentId: null,
          consignmentReference: null,
          deliveryReference: null,
          lines: [
            {
              lineId: 'l',
              skuId: 'k',
              skuCode: 'FD-KDC-2KG',
              displayName: 'Kitten dry food',
              stockUnit: 'bags',
              lotNumber: 'L1',
              manufacturedOn: null,
              expiresOn: '2027-01-01',
              quantityAccepted: '30',
              quantityRejected: '2',
              rejectionReason: 'Damaged',
              rejectionNote: null,
              locationId: 'w',
              locationCode: 'WH1',
              holdForInspection: false,
              unitCost: { amount: '450.0000', currencyCode: 'BDT' },
              batchId: 'b1',
              batchStatus: 'Available',
              storage: null,
            },
          ],
        }}
      />,
    );
    expect(bareNumbers(receipt.container)).toEqual([]);
    receipt.unmount();

    const po = render(
      <PurchaseOrderDetail
        deliverTo="x"
        timeZoneId="Asia/Dhaka"
        onIssue={async () => undefined}
        onCancel={async () => undefined}
        onClose={async () => undefined}
        order={{
          id: 'p',
          number: 'PO-1',
          supplierId: 's',
          supplierName: 'S',
          branchId: 'b',
          deliverToLocationId: 'l',
          currencyCode: 'BDT',
          expectedDeliveryOn: '2026-10-20',
          notes: null,
          status: 'Issued',
          statusReason: null,
          receiptState: 'PartiallyReceived',
          issuedOn: null,
          totalAmount: { amount: '100.00', currencyCode: 'BDT' },
          receipts: [],
          allowedActions: [],
          version: 1,
          lines: [
            {
              lineId: 'x',
              skuId: 'k',
              skuCode: 'C',
              displayName: 'Item',
              stockUnit: 'pouches',
              orderedQuantity: '10',
              acceptedQuantity: '4',
              rejectedQuantity: '0',
              outstandingQuantity: '6',
              unitCost: { amount: '10.0000', currencyCode: 'BDT' },
              lineTotal: { amount: '100.00', currencyCode: 'BDT' },
            },
          ],
        }}
      />,
    );
    expect(bareNumbers(po.container)).toEqual([]);
    po.unmount();

    const variance = renderWithClient(
      <CountVariance
        status="Submitted"
        allowedActions={[]}
        onCommit={async () => ({ status: 'Committed' })}
        onApprove={async () => undefined}
        onReject={async () => undefined}
        variance={{
          countId: 'c',
          number: 'CNT-1',
          status: 'Submitted',
          totalVarianceValueBase: { amount: '-925.00', currencyCode: 'BDT' },
          varianceLineCount: 1,
          willNeedApproval: false,
          lines: [
            {
              skuId: 'k',
              skuCode: 'C',
              displayName: 'Item',
              batchId: 'b',
              lotNumber: 'L',
              stockUnit: 'bags',
              bookQuantity: '10',
              countedQuantity: '8',
              variance: '-2',
              value: { amount: '-925.00', currencyCode: 'BDT' },
            },
          ],
        }}
      />,
    );
    expect(bareNumbers(variance.container)).toEqual([]);
    variance.unmount();

    const matrix = render(
      <SkuStockMatrix
        matrix={{
          skuId: 'k',
          skuCode: 'C',
          displayName: 'Item',
          stockUnit: 'bags',
          storage: null,
          branchTimeZoneId: 'Asia/Dhaka',
          batches: [
            {
              batchId: 'b',
              lotNumber: 'L',
              expiresOn: null,
              isExpired: false,
              daysToExpiry: null,
              status: 'Available',
              locations: [
                {
                  locationId: 'w',
                  locationCode: 'WH1',
                  locationKind: 'Warehouse',
                  onHand: '18',
                  reserved: '2',
                },
              ],
            },
          ],
        }}
      />,
    );
    expect(bareNumbers(matrix.container)).toEqual([]);
  });
});

function stubNarrow(narrow: boolean) {
  class FakeObserver {
    constructor(private readonly callback: () => void) {}
    observe() {
      this.callback();
    }
    disconnect() {}
  }
  vi.stubGlobal('ResizeObserver', FakeObserver);
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockReturnValue({
    width: narrow ? 300 : 1000,
    height: 0,
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    x: 0,
    y: 0,
    toJSON: () => ({}),
  });
}

describe('U-X4 — no arithmetic on quantities or money in the slice', () => {
  const root = path.resolve(import.meta.dirname, '../../..');
  const targets = [
    'components/inventory',
    'components/procurement',
    'components/stock',
    'components/recall',
    'lib/api',
    'app/(staff)/staff/inventory',
    'app/(staff)/staff/procurement',
  ];
  const only = /(inventory|procurement)[^/\\]*\.ts$|\.tsx?$/;

  function walk(dir: string): string[] {
    return readdirSync(dir).flatMap((entry) => {
      const full = path.join(dir, entry);
      return statSync(full).isDirectory() ? walk(full) : [full];
    });
  }

  it('has no Number( or parseFloat in the slice’s components, pages or API modules', () => {
    const offenders: string[] = [];
    for (const target of targets) {
      for (const file of walk(path.join(root, target))) {
        const relative = path.relative(root, file).replaceAll('\\', '/');
        if (target === 'lib/api' && !/lib\/api\/(inventory|procurement)-[^/]+\.ts$/.test(relative))
          continue;
        if (!only.test(file)) continue;
        const source = readFileSync(file, 'utf8');
        if (/\bNumber\(|\bparseFloat\(/.test(source)) offenders.push(relative);
      }
    }
    expect(offenders).toEqual([]);
  });
});

describe('U-X5 — a 403 renders PermissionDeniedState and nothing else is fetched', () => {
  it('the suppliers page', async () => {
    const calls: string[] = [];
    server.use(
      http.get('*/api/staff/procurement/suppliers', ({ request }) => {
        calls.push(new URL(request.url).pathname);
        return HttpResponse.json(
          { errors: [{ errorCode: 'ActionNotPermitted', errorMessage: 'no' }] },
          { status: 403 },
        );
      }),
    );
    server.events.on('request:start', ({ request }) => {
      const pathName = new URL(request.url).pathname;
      if (pathName.includes('/inventory/') || pathName.includes('/procurement/'))
        calls.push(pathName);
    });
    renderWithClient(<SuppliersPage />);
    expect(await screen.findByText(/you don.t have permission to do this/i)).toBeInTheDocument();
    server.events.removeAllListeners();
    expect(new Set(calls)).toEqual(new Set(['/api/staff/procurement/suppliers']));
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('the new-receipt page, whose gate is the server’s 403 on the order list', async () => {
    server.use(
      http.get('*/api/staff/procurement/purchase-orders', () =>
        HttpResponse.json(
          { errors: [{ errorCode: 'ActionNotPermitted', errorMessage: 'no' }] },
          { status: 403 },
        ),
      ),
      http.get('*/api/staff/session', () =>
        HttpResponse.json({
          staffAccountId: 's2',
          fullName: 'Nadia',
          roleIds: ['Nurse'],
          issuedOn: '2026-10-07T00:00:00Z',
          lastActivityOn: '2026-10-07T00:00:00Z',
          idleExpiresOn: '2026-10-07T01:00:00Z',
          absoluteExpiresOn: '2026-10-07T08:00:00Z',
          idleTimeoutMinutes: 30,
          serverTime: '2026-10-07T00:00:00Z',
        }),
      ),
    );
    renderWithClient(<NewReceiptPage />);
    expect(await screen.findByText(/you don.t have permission to do this/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /review and post/i })).not.toBeInTheDocument();
  });
});
