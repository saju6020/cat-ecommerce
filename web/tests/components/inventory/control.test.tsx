import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AdjustmentForm } from '@/components/inventory/AdjustmentForm';
import { AdjustmentDetail } from '@/components/inventory/AdjustmentDetail';
import { CountSheet } from '@/components/inventory/CountSheet';
import { CountVariance } from '@/components/inventory/CountVariance';
import { TransferForm } from '@/components/inventory/TransferForm';
import { ApiConflictError, ApiValidationError } from '@/lib/api/client';
import { inventoryFixtureIds as ids, resetInventoryMockData } from '@/mocks/handlers/inventory';
import { db } from '@/mocks/handlers/inventory/store';
import type {
  AdjustmentDetail as AdjustmentData,
  CountDetail,
  CountVariance as VarianceData,
} from '@/lib/api/inventory-control';
import type { StockLocation } from '@/lib/api/inventory-lookup';
import { renderWithClient } from './testUtils';

vi.mock('next/navigation', async () => {
  const { navigation } = await import('./testUtils');
  return {
    useRouter: () => ({ push: navigation.push, replace: navigation.replace }),
    usePathname: () => navigation.pathname,
    useSearchParams: () => navigation.search,
  };
});

const money = (amount: string) => ({ amount, currencyCode: 'BDT' });
const violation = (propertyName: string, errorCode: string, errorMessage: string) => ({
  propertyName,
  errorCode,
  errorMessage,
  resourceName: 'Inventory',
});

let locations: StockLocation[] = [];

beforeEach(() => {
  resetInventoryMockData();
  locations = db.locations
    .filter((l) => l.branchId === ids.mainBranchId)
    .map((l) => ({
      id: l.id,
      code: l.code,
      name: l.name,
      kind: l.kind,
      isActive: l.isActive,
      permittedMovements: l.permittedMovements,
    }));
});

async function addLineWithPicker(user: ReturnType<typeof userEvent.setup>, pickerLabel: RegExp) {
  await user.type(screen.getByLabelText(pickerLabel), 'kitten');
  await user.click(await screen.findByRole('option', { name: /kitten dry food/i }));
}

describe('U-E1 / U-E2 — AdjustmentForm', () => {
  it('U-E1: reason Theft presents the quantity as removing stock, needs a note, and the confirm states permanence', async () => {
    const user = userEvent.setup();
    const onRequest = vi
      .fn()
      .mockResolvedValue({
        adjustmentId: 'a1',
        number: 'ADJ-1',
        status: 'PendingApproval',
        valueBase: money('18500.00'),
        requiresApproval: true,
      });
    renderWithClient(
      <AdjustmentForm
        locations={locations}
        onScan={async () => null}
        onRequest={onRequest}
        isPending={false}
      />,
    );
    await user.selectOptions(screen.getByLabelText(/^location/i), ids.warehouseId);
    await user.click(screen.getByRole('radio', { name: /theft or loss/i }));
    await addLineWithPicker(user, /or find an item/i);
    expect((await screen.findAllByText(/\(removes stock\)/i)).length).toBeGreaterThan(0);
    await waitFor(() =>
      expect(
        within(screen.getByLabelText(/^batch/i)).getAllByRole('option').length,
      ).toBeGreaterThan(1),
    );
    await user.selectOptions(screen.getByLabelText(/^batch/i), ids.batchFoodFarId);
    await user.type(screen.getByLabelText(/quantity \(bags\)/i), '2');

    await user.click(screen.getByRole('button', { name: /review and request/i }));
    expect(await screen.findAllByText('A note is required — say what happened.')).not.toHaveLength(
      0,
    );
    expect(onRequest).not.toHaveBeenCalled();

    await user.type(screen.getByLabelText(/^note/i), 'Pallet short at the monthly check');
    await user.click(screen.getByRole('button', { name: /review and request/i }));
    const dialog = await screen.findByRole('dialog');
    expect(
      within(dialog).getByText(/cannot be undone — a correction is a new adjustment/i),
    ).toBeInTheDocument();
    expect(within(dialog).getByText(/reduces kitten dry food/i)).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: /request adjustment/i }));
    await waitFor(() => expect(onRequest).toHaveBeenCalled());
    expect(onRequest.mock.calls[0]![0].lines[0]).toMatchObject({
      batchId: ids.batchFoodFarId,
      quantityDelta: '-2',
    });
    expect(onRequest.mock.calls[0]![0]).toMatchObject({
      reason: 'Theft',
      note: 'Pallet short at the monthly check',
    });
  });

  it('U-E2: a result that requires approval says Pending approval and claims nothing was written', async () => {
    const user = userEvent.setup();
    const onRequest = vi
      .fn()
      .mockResolvedValue({
        adjustmentId: 'a1',
        number: 'ADJ-2026-000009',
        status: 'PendingApproval',
        valueBase: money('18500.00'),
        requiresApproval: true,
      });
    renderWithClient(
      <AdjustmentForm
        locations={locations}
        onScan={async () => null}
        onRequest={onRequest}
        isPending={false}
      />,
    );
    await user.selectOptions(screen.getByLabelText(/^location/i), ids.warehouseId);
    await user.click(screen.getByRole('radio', { name: /damaged/i }));
    await user.type(screen.getByLabelText(/^note/i), 'Split open');
    await addLineWithPicker(user, /or find an item/i);
    await waitFor(() =>
      expect(
        within(screen.getByLabelText(/^batch/i)).getAllByRole('option').length,
      ).toBeGreaterThan(1),
    );
    await user.selectOptions(screen.getByLabelText(/^batch/i), ids.batchFoodFarId);
    await user.type(screen.getByLabelText(/quantity \(bags\)/i), '1');
    await user.click(screen.getByRole('button', { name: /review and request/i }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', {
        name: /request adjustment/i,
      }),
    );
    const result = (await screen.findByText(/ADJ-2026-000009 — Pending approval/)).closest(
      '[role="status"]',
    )!;
    expect(result).toHaveTextContent(/needs approval from someone other than you/i);
    expect(result).toHaveTextContent(/nothing has been written to the ledger yet/i);
  });
});

function adjustment(overrides: Partial<AdjustmentData> = {}): AdjustmentData {
  return {
    id: 'a1',
    number: 'ADJ-2026-000002',
    branchId: 'b',
    locationId: 'l',
    locationCode: 'WH1',
    reason: 'Theft',
    note: 'Short at the check',
    status: 'PendingApproval',
    valueBase: money('18500.00'),
    requestedBy: 'u1',
    requestedOn: '2026-10-06T04:00:00Z',
    decidedBy: null,
    decidedOn: null,
    decisionNote: null,
    lines: [
      {
        lineId: 'l1',
        skuId: 'k',
        skuCode: 'FD-KDC-2KG',
        displayName: 'Kitten dry food',
        batchId: 'b1',
        lotNumber: 'L2408B',
        quantityDelta: '-8',
        stockUnit: 'bags',
        unitCostBase: money('462.5000'),
      },
    ],
    allowedActions: ['approve', 'reject', 'withdraw'],
    ...overrides,
  };
}

describe('U-E3 / U-E4 — AdjustmentDetail approvals', () => {
  it("U-E3: approving as the requester shows the server's refusal in plain words", async () => {
    const user = userEvent.setup();
    const onApprove = vi
      .fn()
      .mockRejectedValue(
        new ApiValidationError({
          isValid: false,
          errors: [
            violation(
              'AdjustmentId',
              'ApproverMustDiffer',
              'Someone else has to approve your request.',
            ),
          ],
        }),
      );
    renderWithClient(
      <AdjustmentDetail
        adjustment={adjustment()}
        timeZoneId="Asia/Dhaka"
        onApprove={onApprove}
        onReject={async () => undefined}
        onWithdraw={async () => undefined}
      />,
    );
    await user.click(screen.getByRole('button', { name: 'Approve' }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: /approve and post/i }),
    );
    expect(
      await screen.findByText('Someone else has to approve your request.'),
    ).toBeInTheDocument();
    expect(document.body).not.toHaveTextContent('ApproverMustDiffer');
  });

  it('U-E4: a 409 InsufficientStock on approval says to recount and raise a new adjustment', async () => {
    const user = userEvent.setup();
    const onApprove = vi
      .fn()
      .mockRejectedValue(
        new ApiConflictError({
          isValid: false,
          errors: [
            violation(
              'Quantity',
              'InsufficientStock',
              'That quantity is no longer available — someone may have just taken it.',
            ),
          ],
        }),
      );
    renderWithClient(
      <AdjustmentDetail
        adjustment={adjustment()}
        timeZoneId="Asia/Dhaka"
        onApprove={onApprove}
        onReject={async () => undefined}
        onWithdraw={async () => undefined}
      />,
    );
    await user.click(screen.getByRole('button', { name: 'Approve' }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: /approve and post/i }),
    );
    expect(
      await screen.findByText(
        'Stock has moved since this was requested — recount and raise a new adjustment.',
      ),
    ).toBeInTheDocument();
  });

  it('shows Approve and Reject only when the server allows them, and says nothing is in the ledger while pending', () => {
    renderWithClient(
      <AdjustmentDetail
        adjustment={adjustment({ allowedActions: [] })}
        timeZoneId="Asia/Dhaka"
        onApprove={async () => undefined}
        onReject={async () => undefined}
        onWithdraw={async () => undefined}
      />,
    );
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
    expect(screen.getByText(/nothing has been written to the ledger yet/i)).toBeInTheDocument();
  });
});

function openCount(): CountDetail {
  return {
    id: 'c1',
    number: 'CNT-2026-000001',
    branchId: 'b',
    locationId: 'l',
    locationCode: 'SHOP',
    scope: 'FullLocation',
    skuIds: [],
    status: 'Open',
    openedOn: '2026-10-07T02:00:00Z',
    openedBy: 'u',
    submittedOn: null,
    varianceValueBase: null,
    isBlind: true,
    lines: [
      {
        skuId: 'k',
        skuCode: 'FD-KDC-2KG',
        displayName: 'Kitten dry food',
        batchId: 'b1',
        lotNumber: 'L2409E',
        expiresOn: '2027-01-30',
        stockUnit: 'bags',
        countedQuantity: '6',
        bookQuantity: null,
        variance: null,
      },
    ],
    allowedActions: ['record-lines', 'submit', 'cancel'],
    version: 2,
  };
}

describe('U-E5 — CountSheet is blind', () => {
  it('shows no book quantity or variance anywhere, and each line shows its saved state', async () => {
    const user = userEvent.setup();
    const onRecordLine = vi.fn().mockResolvedValue(undefined);
    renderWithClient(
      <CountSheet
        count={openCount()}
        onScan={async () => null}
        onRecordLine={onRecordLine}
        onSubmit={async () => undefined}
      />,
    );
    expect(screen.getByText('Saved')).toBeInTheDocument();
    expect(document.body).not.toHaveTextContent(/book quantity|variance|on hand/i);

    // Adding a line: the lot chooser shows lot and expiry only — never what the books say.
    await user.type(screen.getByLabelText(/or find an item/i), 'litter');
    await user.click(await screen.findByRole('option', { name: /cat litter/i }));
    const chooser = await screen.findByLabelText(/which lot of cat litter/i);
    await waitFor(() => expect(within(chooser).getAllByRole('option').length).toBeGreaterThan(1));
    expect(chooser).not.toHaveTextContent(/on hand|\d+ bags/i);
    await user.selectOptions(chooser, ids.batchLitterId);
    await user.click(screen.getByRole('button', { name: /add to the sheet/i }));
    const counted = (await screen.findAllByLabelText(/counted \(bags\)/i)).at(-1)!;
    await user.type(counted, '38');
    await user.tab();
    await waitFor(() =>
      expect(onRecordLine).toHaveBeenCalledWith(ids.litterSkuId, ids.batchLitterId, '38'),
    );
    await waitFor(() => expect(screen.getAllByText('Saved')).toHaveLength(2));
    expect(document.body).not.toHaveTextContent(/book quantity|variance/i);
  });

  it('a line that failed to save says so and offers a retry', async () => {
    const user = userEvent.setup();
    const onRecordLine = vi.fn().mockRejectedValue(new Error('network'));
    renderWithClient(
      <CountSheet
        count={openCount()}
        onScan={async () => null}
        onRecordLine={onRecordLine}
        onSubmit={async () => undefined}
      />,
    );
    const counted = screen.getByLabelText(/counted \(bags\)/i);
    await user.clear(counted);
    await user.type(counted, '5');
    await user.tab();
    expect(await screen.findByText('Not saved')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /try again/i })).toBeInTheDocument();
  });
});

describe('U-E6 — CountVariance', () => {
  const variance: VarianceData = {
    countId: 'c2',
    number: 'CNT-2026-000002',
    status: 'Submitted',
    totalVarianceValueBase: money('-1850.00'),
    varianceLineCount: 2,
    willNeedApproval: false,
    lines: [
      {
        skuId: 'k',
        skuCode: 'FD-KDC-2KG',
        displayName: 'Kitten dry food',
        batchId: 'b1',
        lotNumber: 'L2408B',
        stockUnit: 'bags',
        bookQuantity: '10',
        countedQuantity: '8',
        variance: '-2',
        value: money('-925.00'),
      },
      {
        skuId: 'k',
        skuCode: 'FD-KDC-2KG',
        displayName: 'Kitten dry food',
        batchId: 'b2',
        lotNumber: 'L2409E',
        stockUnit: 'bags',
        bookQuantity: '18',
        countedQuantity: '16',
        variance: '-2',
        value: money('-925.00'),
      },
    ],
  };

  it('says nothing has been written yet, and the commit confirm names the number of variances and the value', async () => {
    const user = userEvent.setup();
    const onCommit = vi.fn().mockResolvedValue({ status: 'Committed' });
    renderWithClient(
      <CountVariance
        variance={variance}
        status="Submitted"
        allowedActions={['commit', 'cancel']}
        onCommit={onCommit}
        onApprove={async () => undefined}
        onReject={async () => undefined}
      />,
    );
    expect(screen.getByText('Nothing has been written to stock yet.')).toBeInTheDocument();
    expect(screen.getAllByText('10 bags').length).toBeGreaterThan(0);
    await user.click(screen.getByRole('button', { name: /commit the count/i }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/2 variances, .*1,850\.00/)).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: /^commit the count$/i }));
    await waitFor(() => expect(onCommit).toHaveBeenCalled());
  });

  it('a count over the approval limit ends as awaiting approval, explained', async () => {
    const user = userEvent.setup();
    renderWithClient(
      <CountVariance
        variance={{ ...variance, willNeedApproval: true }}
        status="Submitted"
        allowedActions={['commit']}
        onCommit={async () => ({ status: 'AwaitingApproval' })}
        onApprove={async () => undefined}
        onReject={async () => undefined}
      />,
    );
    await user.click(screen.getByRole('button', { name: /commit the count/i }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', {
        name: /^commit the count$/i,
      }),
    );
    expect(
      await screen.findByText(
        /above the approval limit, so a different person with approval rights must approve/i,
      ),
    ).toBeInTheDocument();
  });
});

describe('U-E7 — TransferForm', () => {
  it('blocks the same from and to location with the reason in text', async () => {
    const user = userEvent.setup();
    renderWithClient(
      <TransferForm
        locations={locations}
        onScan={async () => null}
        onPost={async () => undefined}
        isPending={false}
      />,
    );
    await user.selectOptions(screen.getByLabelText(/^from/i), ids.warehouseId);
    const toWarehouse = within(screen.getByLabelText(/^to/i)).getByRole('option', {
      name: /main warehouse/i,
    });
    expect(toWarehouse).toBeDisabled();
    expect(toWarehouse).toHaveTextContent(/it is the source/i);
  });

  it('the confirm names both locations', async () => {
    const user = userEvent.setup();
    const onPost = vi.fn().mockResolvedValue(undefined);
    renderWithClient(
      <TransferForm
        locations={locations}
        onScan={async () => null}
        onPost={onPost}
        isPending={false}
      />,
    );
    await user.selectOptions(screen.getByLabelText(/^from/i), ids.warehouseId);
    await user.selectOptions(screen.getByLabelText(/^to/i), ids.shopFloorId);
    await user.type(screen.getByLabelText(/or find an item/i), 'kitten');
    await user.click(await screen.findByRole('option', { name: /kitten dry food/i }));
    await waitFor(() =>
      expect(
        within(screen.getByLabelText(/^batch/i)).getAllByRole('option').length,
      ).toBeGreaterThan(1),
    );
    await user.selectOptions(screen.getByLabelText(/^batch/i), ids.batchFoodFarId);
    await user.type(screen.getByLabelText(/quantity \(bags\)/i), '3');
    await user.click(screen.getByRole('button', { name: /review and post/i }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/from main warehouse to shop floor/i)).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: /post transfer/i }));
    await waitFor(() => expect(onPost).toHaveBeenCalled());
    expect(onPost.mock.calls[0]![0]).toMatchObject({
      fromLocationId: ids.warehouseId,
      toLocationId: ids.shopFloorId,
    });
  });
});
