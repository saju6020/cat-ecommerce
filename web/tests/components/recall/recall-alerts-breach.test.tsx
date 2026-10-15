import * as React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { AlertList } from '@/components/inventory/AlertList';
import { ReasonDialog } from '@/components/inventory/ReasonDialog';
import { StorageBreachDetailView } from '@/components/inventory/StorageBreachDetail';
import { StorageBreachForm } from '@/components/inventory/StorageBreachForm';
import { NearExpiryReport } from '@/components/stock/NearExpiryReport';
import { DeclareRecallDialog } from '@/components/recall/DeclareRecallDialog';
import { RecallConsole } from '@/components/recall/RecallConsole';
import { RecallTraceTable } from '@/components/recall/RecallTraceTable';
import InventoryLayout from '@/app/(staff)/staff/inventory/layout';
import { inventoryFixtureIds as ids, resetInventoryMockData } from '@/mocks/handlers/inventory';
import { db } from '@/mocks/handlers/inventory/store';
import type { InventoryAlert } from '@/lib/api/inventory-alerts';
import type { BatchDetail } from '@/lib/api/inventory-stock';
import type { NearExpiryReport as Report } from '@/lib/api/inventory-reports';
import type { RecallDetail, RecallTrace } from '@/lib/api/inventory-recalls';
import type { BreachDetail } from '@/lib/api/inventory-breaches';
import { renderWithClient } from '../inventory/testUtils';

vi.mock('next/navigation', async () => {
  const { navigation } = await import('../inventory/testUtils');
  return {
    useRouter: () => ({ push: navigation.push, replace: navigation.replace }),
    usePathname: () => navigation.pathname,
    useSearchParams: () => navigation.search,
  };
});

const money = (amount: string) => ({ amount, currencyCode: 'BDT' });
beforeEach(() => resetInventoryMockData());

function alert(overrides: Partial<InventoryAlert>): InventoryAlert {
  return {
    id: 'a',
    kind: 'LowStock',
    status: 'Open',
    skuId: 'k',
    skuCode: 'FD-ADT-POUCH',
    displayName: 'Adult wet food',
    batchId: null,
    lotNumber: null,
    locationId: 'l',
    locationCode: 'WH1',
    thresholdDays: null,
    quantityAtRaise: '8',
    raisedOn: '2026-10-05T04:00:00Z',
    acknowledgedBy: null,
    acknowledgedOn: null,
    resolvedOn: null,
    resolutionNote: null,
    ...overrides,
  };
}

describe('U-F1 — AlertList', () => {
  it('renders every kind in plain words and posts an acknowledge with its note', async () => {
    const user = userEvent.setup();
    const onAcknowledge = vi.fn().mockResolvedValue(undefined);
    renderWithClient(
      <AlertList
        rows={[
          alert({
            id: '1',
            kind: 'ExpiryThreshold',
            thresholdDays: 60,
            batchId: 'b',
            lotNumber: 'L2408B',
          }),
          alert({ id: '2', kind: 'Expired' }),
          alert({ id: '3', kind: 'LowStock' }),
          alert({ id: '4', kind: 'Shortfall', status: 'Acknowledged' }),
          alert({ id: '5', kind: 'StorageBreach' }),
          alert({
            id: '6',
            kind: 'LedgerMismatch',
            status: 'Resolved',
            resolutionNote: 'Recounted and corrected.',
          }),
        ]}
        totalCount={6}
        pageSize={25}
        isLoading={false}
        isError={false}
        onRetry={() => undefined}
        timeZoneId="Asia/Dhaka"
        onAcknowledge={onAcknowledge}
      />,
    );
    for (const text of [
      /expires in 60 days or less/i,
      /expired — cannot be sold, dispensed or fed/i,
      /below reorder point/i,
      /shortfall recorded/i,
      /storage breach/i,
      /ledger mismatch/i,
    ]) {
      expect(screen.getAllByText(text).length).toBeGreaterThan(0);
    }
    expect(screen.getByText(/resolved: recounted and corrected/i)).toBeInTheDocument();
    await user.click(
      screen.getAllByRole('button', { name: /^acknowledge: below reorder point/i })[0]!,
    );
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText(/note/i), 'Ordered today');
    await user.click(within(dialog).getByRole('button', { name: /^acknowledge$/i }));
    await waitFor(() => expect(onAcknowledge).toHaveBeenCalledWith('3', 'Ordered today'));
  });
});

describe('U-F2 — the alert badge poll never touches an open dialog (ADR-FE-045)', () => {
  it('the badge count updates while the dialog and what is typed in it stay as they were', async () => {
    const user = userEvent.setup();
    let open = 3;
    server.use(
      http.get('*/api/staff/session', () =>
        HttpResponse.json({
          staffAccountId: 's1',
          fullName: 'Rezwan',
          roleIds: ['Administrator'],
          issuedOn: '2026-10-07T00:00:00Z',
          lastActivityOn: '2026-10-07T00:00:00Z',
          idleExpiresOn: '2026-10-07T01:00:00Z',
          absoluteExpiresOn: '2026-10-07T08:00:00Z',
          idleTimeoutMinutes: 30,
          serverTime: '2026-10-07T00:00:00Z',
        }),
      ),
      http.get('*/api/staff/inventory/alerts/summary', () =>
        HttpResponse.json({ open: { total: open, byKind: {} } }),
      ),
    );
    function Page() {
      const [dialogOpen, setDialogOpen] = React.useState(true);
      return (
        <ReasonDialog
          open={dialogOpen}
          onOpenChange={setDialogOpen}
          title="Quarantine a batch"
          consequence="Held back."
          confirmLabel="Quarantine"
          onConfirm={async () => undefined}
        />
      );
    }
    const { queryClient } = renderWithClient(
      <InventoryLayout>
        <Page />
      </InventoryLayout>,
    );
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText(/reason/i), 'Torn bag');
    // The nav behind a modal is hidden from assistive tech; read the count from the DOM text.
    await waitFor(() => expect(document.body).toHaveTextContent('3 open'));
    open = 7;
    await act(async () => {
      await queryClient.invalidateQueries({ queryKey: ['inventory', 'alerts', 'summary'] });
    });
    await waitFor(() => expect(document.body).toHaveTextContent('7 open'));
    expect(screen.getByRole('dialog')).toBe(dialog);
    expect(within(dialog).getByLabelText(/reason/i)).toHaveValue('Torn bag');
  });
});

describe('U-F3 — NearExpiryReport', () => {
  const report: Report = {
    asOf: '2026-10-07',
    withinDays: 90,
    includeExpired: true,
    totalValueAtRisk: money('13875.00'),
    rows: [
      {
        skuId: 'k',
        skuCode: 'FD-KDC-2KG',
        displayName: 'Kitten dry food',
        batchId: 'b1',
        lotNumber: 'L2408B',
        expiresOn: '2026-11-21',
        daysRemaining: 45,
        isExpired: false,
        status: 'Available',
        quantity: '10',
        locations: [{ locationId: 'l', locationCode: 'WH1', onHand: '10' }],
        unitCostBase: { amount: '462.5000', currencyCode: 'BDT' },
        costBasis: 'landed',
        valueAtRisk: money('4625.00'),
      },
      {
        skuId: 'k',
        skuCode: 'FD-KDC-2KG',
        displayName: 'Kitten dry food',
        batchId: 'b2',
        lotNumber: 'L2405Z',
        expiresOn: '2026-10-04',
        daysRemaining: -3,
        isExpired: true,
        status: 'Available',
        quantity: '4',
        locations: [{ locationId: 'l', locationCode: 'WH1', onHand: '4' }],
        unitCostBase: { amount: '462.5000', currencyCode: 'BDT' },
        costBasis: 'landed',
        valueAtRisk: money('1850.00'),
      },
    ],
  };

  it('shows days remaining and value at risk per row, the server total, and a server CSV button', () => {
    renderWithClient(
      <NearExpiryReport
        report={report}
        unitBySku={{ k: 'bags' }}
        csvPath="/api/staff/inventory/reports/near-expiry.csv?withinDays=90"
      />,
    );
    expect(screen.getByText('45 days')).toBeInTheDocument();
    expect(screen.getByText('expired 3 days ago')).toBeInTheDocument();
    expect(screen.getByText('10 bags')).toBeInTheDocument();
    expect(screen.getAllByText(/13,875\.00/).length).toBeGreaterThan(0);
    // A button, not a link: the CSV is fetched with X-Branch-Id (tests/components/stock/export-button).
    expect(screen.getByRole('button', { name: /export csv/i })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /export csv/i })).not.toBeInTheDocument();
  });

  it('a row whose unit cannot be resolved leaves the quantity out rather than showing it bare', () => {
    renderWithClient(<NearExpiryReport report={report} unitBySku={{}} csvPath="x" />);
    expect(screen.queryByText('10 bags')).not.toBeInTheDocument();
    expect(screen.queryByText(/^10$/)).not.toBeInTheDocument();
  });
});

function recalledBatch(): BatchDetail {
  return {
    id: 'rb',
    skuId: 'k2',
    skuCode: 'MED-FVX-TRI',
    displayName: 'Feline tricat vaccine',
    stockUnit: 'ml',
    stockClass: 'Medicine',
    branchId: 'br',
    lotNumber: 'L7702R',
    manufacturedOn: null,
    expiresOn: '2026-11-06',
    isExpired: false,
    daysToExpiry: 30,
    branchTimeZoneId: 'Asia/Dhaka',
    status: 'Available',
    statusReason: null,
    receivedOn: '2026-09-01T04:00:00Z',
    receivedQuantity: '30',
    goodsReceiptId: 'g',
    goodsReceiptNumber: 'GRN-1',
    consignmentId: null,
    consignmentReference: null,
    supplierId: 's',
    originCountryCode: 'US',
    purchaseUnitCost: null,
    unitCostBase: null,
    costBasis: null,
    positions: [
      {
        locationId: 'd',
        locationCode: 'DISP',
        locationKind: 'ClinicDispensary',
        onHand: '20',
        reserved: '0',
      },
      {
        locationId: 'bs',
        locationCode: 'BOARD',
        locationKind: 'BoardingStore',
        onHand: '10',
        reserved: '0',
      },
    ],
    recall: null,
    statusHistory: [],
    allowedActions: ['recall'],
    storage: null,
    version: 1,
  };
}

describe('U-F4 — DeclareRecallDialog (ADR-FE-046)', () => {
  it('stays disabled until the exact lot is typed, then states immediacy and irreversibility', async () => {
    const user = userEvent.setup();
    const onDeclare = vi.fn().mockResolvedValue(undefined);
    renderWithClient(
      <DeclareRecallDialog
        open
        onOpenChange={() => undefined}
        batch={recalledBatch()}
        onDeclare={onDeclare}
      />,
    );
    const dialog = screen.getByRole('dialog');
    expect(
      within(dialog).getByText(
        /becomes unusable immediately, everywhere\. this cannot be undone — only withdrawn/i,
      ),
    ).toBeInTheDocument();
    expect(within(dialog).getByText(/20 ml/)).toBeInTheDocument();
    await user.type(within(dialog).getByLabelText(/reason \(internal\)/i), 'Supplier notice');
    await user.type(within(dialog).getByLabelText(/advice to customers/i), 'Stop using it');
    const declare = within(dialog).getByRole('button', { name: /declare recall/i });
    await user.type(within(dialog).getByLabelText(/type the lot number/i), 'L7702');
    expect(declare).toBeDisabled();
    expect(within(dialog).getByText("The lot number you typed doesn't match.")).toBeInTheDocument();
    await user.type(within(dialog).getByLabelText(/type the lot number/i), 'R');
    expect(declare).toBeEnabled();
    await user.click(declare);
    await waitFor(() =>
      expect(onDeclare).toHaveBeenCalledWith({
        confirmLotNumber: 'L7702R',
        reason: 'Supplier notice',
        customerAdvice: 'Stop using it',
      }),
    );
    expect(
      within(dialog).getByText(/no one is messaged by declaring a recall/i),
    ).toBeInTheDocument();
  });
});

const customer = (id: string, name: string, extra: Record<string, unknown> = {}) => ({
  customerId: id,
  name,
  mobileNumber: '01711000001',
  email: `${id}@example.com`,
  isAnonymised: false,
  originalCustomerId: null,
  ...extra,
});

function trace(): RecallTrace {
  const nusrat = customer('c1', 'Nusrat Jahan');
  return {
    batchId: 'rb',
    recallNumber: 'RCL-2026-000001',
    skuCode: 'MED-FVX-TRI',
    displayName: 'Feline tricat vaccine',
    lotNumber: 'L7702R',
    batchStatus: 'Recalled',
    received: [
      {
        occurredOn: '2026-09-25T04:00:00Z',
        kind: 'Sale',
        quantity: '2',
        locationId: 'd',
        locationCode: 'DISP',
        referenceLabel: 'Order PC-001234',
        customer: nusrat,
        cat: { catId: 'k1', name: 'Mishti', currentOwner: nusrat },
        householdCats: [],
      },
      {
        occurredOn: '2026-09-28T04:00:00Z',
        kind: 'Dispense',
        quantity: '1.5',
        locationId: 'd',
        locationCode: 'DISP',
        referenceLabel: 'RX-2026-000045',
        customer: customer('c2', 'Karim Uddin'),
        cat: null,
        householdCats: ['Tiger'],
      },
      {
        occurredOn: '2026-10-01T04:00:00Z',
        kind: 'BoardingUse',
        quantity: '1',
        locationId: 'bs',
        locationCode: 'BOARD',
        referenceLabel: 'Stay BR-9',
        customer: customer('c3', 'Anika Rahman'),
        cat: null,
        householdCats: [],
      },
      {
        occurredOn: '2026-10-02T04:00:00Z',
        kind: 'Sale',
        quantity: '3',
        locationId: 'd',
        locationCode: 'DISP',
        referenceLabel: 'Order PC-001240',
        customer: customer('c4', 'Farhana Akter', { originalCustomerId: 'old' }),
        cat: null,
        householdCats: [],
      },
    ],
    returned: [
      {
        referenceKind: 'CustomerReturn',
        referenceId: 'r1',
        referenceLabel: 'Return RT-2',
        quantity: '1',
        customer: nusrat,
      },
    ],
    unshippedDispatchBlocked: [
      {
        referenceKind: 'CustomerOrder',
        referenceId: 'o1',
        quantity: '2',
        customer: customer('c2', 'Karim Uddin'),
        cat: { catId: 'k2', name: 'Tiger', currentOwner: null },
      },
    ],
    unidentified: { count: 3, quantity: '6' },
    stockHeld: [{ locationId: 'd', locationCode: 'DISP', branchId: 'br', onHand: '20' }],
  };
}

describe('U-F5 — RecallTraceTable', () => {
  it('has all four tabs populated: purchase, dispense, boarding feed, a merged survivor, unshipped, unidentified', async () => {
    const user = userEvent.setup();
    renderWithClient(
      <RecallTraceTable
        trace={trace()}
        stockUnit="ml"
        csvUrl="http://api.test/api/staff/inventory/recalls/rb/trace.csv"
        timeZoneId="Asia/Dhaka"
      />,
    );
    expect(screen.getAllByText('Bought').length).toBeGreaterThan(0);
    expect(screen.getByText('Dispensed')).toBeInTheDocument();
    expect(screen.getByText('Fed during boarding')).toBeInTheDocument();
    expect(screen.getAllByText('01711000001').length).toBeGreaterThan(0);
    expect(
      screen.getByText(/merged account — shown as the surviving customer/i),
    ).toBeInTheDocument();
    expect(screen.getByText(/household cats — not specifically recorded/i)).toBeInTheDocument();
    expect(screen.getByText('1.5 ml')).toBeInTheDocument();
    await user.click(screen.getByRole('tab', { name: /returned/i }));
    expect(screen.getByText('Return RT-2')).toBeInTheDocument();
    await user.click(screen.getByRole('tab', { name: /unshipped/i }));
    expect(
      screen.getByText(/must not be dispatched while the lot is recalled/i),
    ).toBeInTheDocument();
    await user.click(screen.getByRole('tab', { name: /unidentified/i }));
    expect(
      screen.getByText(/3 counter sales \(6 ml\) have no customer recorded/i),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /export trace as csv/i })).toHaveAttribute(
      'href',
      expect.stringContaining('trace.csv'),
    );
  });
});

function recallDetail(): RecallDetail {
  return {
    summary: {
      batchId: 'rb',
      recallNumber: 'RCL-2026-000001',
      status: 'Active',
      skuId: 'k2',
      skuCode: 'MED-FVX-TRI',
      displayName: 'Feline tricat vaccine',
      lotNumber: 'L7702R',
      expiresOn: '2026-11-06',
      declaredOn: '2026-10-05T04:00:00Z',
      notifiedRecipientCount: 0,
      notificationRuns: 0,
      heldQuantity: '30',
    },
    recall: {
      recallNumber: 'RCL-2026-000001',
      reason: 'Supplier notice',
      customerAdvice: 'Stop using it',
      declaredOn: '2026-10-05T04:00:00Z',
      declaredBy: 'u',
      notifiedOn: null,
      notifiedBy: null,
      notifiedRecipientCount: 0,
      notificationRuns: 0,
      closedOn: null,
      closingNote: null,
    },
    held: [{ locationId: 'd', locationCode: 'DISP', branchId: 'br', onHand: '20' }],
  };
}

describe('U-F6 / U-F7 — RecallConsole notify and withdraw', () => {
  it('U-F6: the confirm names the count; the result and the log link are shown; a repeat run reports none left', async () => {
    const user = userEvent.setup();
    const onNotify = vi
      .fn()
      .mockResolvedValueOnce({ requested: 4, alreadyNotified: 0, remaining: 0 })
      .mockResolvedValueOnce({ requested: 0, alreadyNotified: 4, remaining: 0 });
    renderWithClient(
      <RecallConsole
        detail={recallDetail()}
        trace={trace()}
        stockUnit="ml"
        csvUrl="x"
        timeZoneId="Asia/Dhaka"
        onNotify={onNotify}
        onWithdraw={async () => undefined}
        onClose={async () => undefined}
      />,
    );
    expect(
      screen.getByText(/customers have not yet been messaged/i).closest('p'),
    ).toHaveTextContent(/^4 customers have not yet been messaged/);
    expect(screen.getByRole('link', { name: /open the notification log/i })).toHaveAttribute(
      'href',
      expect.stringContaining('/staff/notifications'),
    );
    await user.click(screen.getByRole('button', { name: /notify everyone/i }));
    let dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/4 customers will be messaged/i)).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: /notify 4 customers/i }));
    expect(
      await screen.findByText(/4 messaged, 0 previously, 0 left\. every message is logged/i),
    ).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /notify everyone/i }));
    dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: /notify/i, hidden: false }));
    await waitFor(() => expect(onNotify).toHaveBeenCalledTimes(2));
    expect(await screen.findByText(/0 messaged, 4 previously, 0 left/i)).toBeInTheDocument();
  });

  it('declaring does not message anyone: nothing is notified until Notify everyone is chosen', () => {
    const onNotify = vi.fn();
    renderWithClient(
      <RecallConsole
        detail={recallDetail()}
        trace={trace()}
        stockUnit="ml"
        csvUrl="x"
        timeZoneId="Asia/Dhaka"
        onNotify={onNotify}
        onWithdraw={async () => undefined}
        onClose={async () => undefined}
      />,
    );
    expect(onNotify).not.toHaveBeenCalled();
    expect(screen.getByText(/declaring a recall messages no one/i)).toBeInTheDocument();
  });

  it('U-F7: withdrawing says the batch returns to quarantine, not to sale', async () => {
    const user = userEvent.setup();
    const onWithdraw = vi.fn().mockResolvedValue(undefined);
    renderWithClient(
      <RecallConsole
        detail={recallDetail()}
        trace={trace()}
        stockUnit="ml"
        csvUrl="x"
        timeZoneId="Asia/Dhaka"
        onNotify={vi.fn()}
        onWithdraw={onWithdraw}
        onClose={async () => undefined}
      />,
    );
    await user.click(screen.getByRole('button', { name: /withdraw the recall/i }));
    const dialog = await screen.findByRole('dialog');
    expect(
      within(dialog).getByText(
        /the batch goes back to quarantine, not on sale\. messages not yet sent will not be sent/i,
      ),
    ).toBeInTheDocument();
    await user.type(
      within(dialog).getByLabelText(/why is the recall being withdrawn/i),
      'Declared by mistake',
    );
    await user.click(within(dialog).getByRole('button', { name: /^withdraw the recall$/i }));
    await waitFor(() => expect(onWithdraw).toHaveBeenCalledWith('Declared by mistake'));
  });

  it("closing while stock is still held shows the server's sentence", async () => {
    const user = userEvent.setup();
    const { ApiValidationError } = await import('@/lib/api/client');
    const onClose = vi
      .fn()
      .mockRejectedValue(
        new ApiValidationError({
          isValid: false,
          errors: [
            {
              propertyName: 'BatchId',
              errorCode: 'RecallStockStillHeld',
              errorMessage: 'Write off or return the remaining 30 ml first.',
              resourceName: 'Inventory',
            },
          ],
        }),
      );
    renderWithClient(
      <RecallConsole
        detail={recallDetail()}
        trace={trace()}
        stockUnit="ml"
        csvUrl="x"
        timeZoneId="Asia/Dhaka"
        onNotify={vi.fn()}
        onWithdraw={async () => undefined}
        onClose={onClose}
      />,
    );
    await user.click(screen.getByRole('button', { name: /close the recall/i }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText(/closing note/i), 'All done');
    await user.click(within(dialog).getByRole('button', { name: /^close the recall$/i }));
    expect(
      await within(dialog).findByText('Write off or return the remaining 30 ml first.'),
    ).toBeInTheDocument();
  });
});

function breach(overrides: Partial<BreachDetail> = {}): BreachDetail {
  return {
    summary: {
      id: 'br1',
      locationId: 'd',
      locationCode: 'DISP',
      observedOn: '2026-10-06T01:00:00Z',
      condition: 'Fridge door left open',
      status: 'Open',
      affectedBatchCount: 2,
      reportedBy: 'u',
      reportedOn: '2026-10-06T02:00:00Z',
    },
    temperatureC: 11.5,
    durationMinutes: 240,
    affectedBatches: [
      {
        batchId: 'b1',
        skuCode: 'MED-FVX-TRI',
        displayName: 'Feline tricat vaccine',
        lotNumber: 'L7701V',
        heldAtLocation: '50.5',
      },
      {
        batchId: 'b2',
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
    ...overrides,
  };
}

describe('U-F8 — storage breach', () => {
  it('lists the affected batches with units and the quarantine confirm names the destination', async () => {
    const user = userEvent.setup();
    const onQuarantine = vi.fn().mockResolvedValue(undefined);
    renderWithClient(
      <StorageBreachDetailView
        breach={breach()}
        unitBySku={{ 'MED-FVX-TRI': 'ml' }}
        quarantineName="Quarantine shelf (QRN)"
        justReported={2}
        timeZoneId="Asia/Dhaka"
        onQuarantine={onQuarantine}
        onClose={async () => undefined}
      />,
    );
    expect(screen.getByText(/2 batches were flagged/i)).toBeInTheDocument();
    expect(screen.getByText('50.5 ml')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /quarantine affected stock/i }));
    const dialog = await screen.findByRole('dialog');
    expect(
      within(dialog).getByText(/moves to quarantine shelf \(qrn\) pending a decision/i),
    ).toBeInTheDocument();
    expect(
      within(dialog).getByText(/50\.5 ml of feline tricat vaccine, lot L7701V → quarantine shelf/i),
    ).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: /^quarantine affected stock$/i }));
    await waitFor(() => expect(onQuarantine).toHaveBeenCalled());
  });

  it('a breach that cannot be quarantined offers only what the server allows', () => {
    renderWithClient(
      <StorageBreachDetailView
        breach={breach({ allowedActions: ['close'] })}
        unitBySku={{}}
        quarantineName={null}
        justReported={null}
        timeZoneId="Asia/Dhaka"
        onQuarantine={async () => undefined}
        onClose={async () => undefined}
      />,
    );
    expect(
      screen.queryByRole('button', { name: /quarantine affected stock/i }),
    ).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /close the report/i })).toBeInTheDocument();
  });

  it('the report form refuses a time in the future and sends an ISO instant otherwise', async () => {
    const user = userEvent.setup();
    const onReport = vi.fn().mockResolvedValue(undefined);
    const locations = db.locations
      .filter((l) => l.branchId === ids.mainBranchId)
      .map((l) => ({
        id: l.id,
        code: l.code,
        name: l.name,
        kind: l.kind,
        isActive: l.isActive,
        permittedMovements: l.permittedMovements,
      }));
    renderWithClient(
      <StorageBreachForm
        locations={locations}
        nowLocal="2026-10-07T10:00"
        timeZoneId="Asia/Dhaka"
        onReport={onReport}
        isPending={false}
      />,
    );
    await user.selectOptions(screen.getByLabelText(/where did it happen/i), ids.dispensaryId);
    await user.type(screen.getByLabelText(/what happened/i), 'Fridge door open');
    const observed = screen.getByLabelText(/when was it observed/i);
    await user.clear(observed);
    await user.type(observed, '2026-10-08T09:00');
    await user.click(screen.getByRole('button', { name: /report the breach/i }));
    expect(
      await screen.findByText('The time it was observed cannot be in the future.'),
    ).toBeInTheDocument();
    expect(onReport).not.toHaveBeenCalled();
    await user.clear(observed);
    await user.type(observed, '2026-10-07T09:30');
    await user.click(screen.getByRole('button', { name: /report the breach/i }));
    await waitFor(() => expect(onReport).toHaveBeenCalled());
    // 09:30 in Asia/Dhaka (UTC+6) is 03:30 UTC.
    expect(onReport.mock.calls[0]![0].observedOn).toBe('2026-10-07T03:30:00.000Z');
  });
});
