import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { GoodsReceiptForm } from '@/components/inventory/GoodsReceiptForm';
import { ApiValidationError } from '@/lib/api/client';
import { receiptDraftKey } from '@/lib/drafts/receipt-draft';
import type { SkuLookup, StockLocation } from '@/lib/api/inventory-lookup';
import { renderWithClient } from './testUtils';

vi.mock('next/navigation', async () => {
  const { navigation } = await import('./testUtils');
  return {
    useRouter: () => ({ push: navigation.push, replace: navigation.replace }),
    usePathname: () => navigation.pathname,
    useSearchParams: () => navigation.search,
  };
});

const STAFF = 'staff-1';
const locations: StockLocation[] = [
  {
    id: 'wh',
    code: 'WH1',
    name: 'Main warehouse',
    kind: 'Warehouse',
    isActive: true,
    permittedMovements: ['Receipt', 'Sale'],
  },
  {
    id: 'qr',
    code: 'QRN',
    name: 'Quarantine shelf',
    kind: 'Quarantine',
    isActive: true,
    permittedMovements: ['Receipt'],
  },
];
const food: SkuLookup = {
  skuId: 'k1',
  productId: 'p1',
  skuCode: 'FD-KDC-2KG',
  displayName: 'Kitten dry food',
  barcode: '4006381333931',
  stockClass: 'Food',
  expiryRequired: true,
  stockUnit: 'bags',
  allowsFractionalQuantity: false,
  countryOfOriginCode: 'DE',
  isActive: true,
  storage: null,
};
const vaccine: SkuLookup = {
  skuId: 'k2',
  productId: 'p2',
  skuCode: 'MED-FVX-TRI',
  displayName: 'Tricat vaccine',
  barcode: '5012345678900',
  stockClass: 'Medicine',
  expiryRequired: true,
  stockUnit: 'ml',
  allowsFractionalQuantity: true,
  countryOfOriginCode: 'US',
  isActive: true,
  storage: {
    condition: 'Refrigerated',
    minTemperatureC: 2,
    maxTemperatureC: 8,
    handlingNotes: null,
  },
};

function setup(overrides: Partial<React.ComponentProps<typeof GoodsReceiptForm>> = {}) {
  const onPost = vi.fn().mockResolvedValue(undefined);
  const onScan = vi.fn(async (barcode: string) =>
    barcode === food.barcode ? food : barcode === vaccine.barcode ? vaccine : null,
  );
  const utils = renderWithClient(
    <GoodsReceiptForm
      staffAccountId={STAFF}
      locations={locations}
      purchaseOrders={[]}
      consignments={[]}
      purchaseOrderLines={null}
      onPurchaseOrderChange={() => undefined}
      onScan={onScan}
      onPost={onPost}
      isPosting={false}
      timeZoneLabel="Asia/Dhaka"
      {...overrides}
    />,
  );
  return { onPost, onScan, user: userEvent.setup({ delay: 0 }), ...utils };
}

async function scan(user: ReturnType<typeof userEvent.setup>, barcode: string) {
  await user.type(screen.getByLabelText(/scan a barcode/i), `${barcode}{Enter}`);
}

async function addFoodLine(user: ReturnType<typeof userEvent.setup>) {
  await scan(user, food.barcode!);
  await screen.findByRole('listitem', { name: /line 1: kitten dry food/i });
}

beforeEach(() => {
  window.sessionStorage.clear();
});

describe('U-C — goods receipt', () => {
  it('U-C3: a scan adds a line, announces it, and shows the storage requirement', async () => {
    const { user } = setup();
    await scan(user, vaccine.barcode!);
    expect(await screen.findByText(/added: tricat vaccine — med-fvx-tri/i)).toBeInTheDocument();
    const line = await screen.findByRole('listitem', { name: /line 1: tricat vaccine/i });
    expect(within(line).getByText(/store: refrigerated 2–8 °C/i)).toBeInTheDocument();
  });

  it('U-C1: a food line without an expiry is marked required and blocked on that field', async () => {
    const { user, onPost } = setup();
    await addFoodLine(user);
    expect(screen.getByLabelText(/expiry date/i)).toBeRequired();
    await user.type(screen.getByLabelText(/lot number/i), 'L2409E');
    await user.type(screen.getByLabelText(/accepted \(bags\)/i), '20');
    await user.click(screen.getByRole('button', { name: /review and post/i }));
    expect(
      await screen.findAllByText('Food and medicine must be received with an expiry date.'),
    ).not.toHaveLength(0);
    expect(onPost).not.toHaveBeenCalled();
    expect(screen.getByLabelText(/expiry date/i)).toHaveAttribute('aria-invalid', 'true');
  });

  it('U-C2: a rejected quantity asks for a reason, and only then', async () => {
    const { user } = setup();
    await addFoodLine(user);
    expect(screen.queryByLabelText(/why was it rejected/i)).not.toBeInTheDocument();
    await user.type(screen.getByLabelText(/rejected \(bags\)/i), '2');
    expect(await screen.findByLabelText(/why was it rejected/i)).toBeRequired();
  });

  it('U-C5: the post confirm names the line count, the quantity with its unit, the location and permanence', async () => {
    const { user, onPost } = setup();
    await addFoodLine(user);
    await user.type(screen.getByLabelText(/lot number/i), 'L2409E');
    await user.type(screen.getByLabelText(/expiry date/i), '2027-01-30');
    await user.type(screen.getByLabelText(/accepted \(bags\)/i), '20');
    await user.selectOptions(screen.getByLabelText(/^location/i), 'wh');
    await user.type(screen.getByLabelText(/^unit cost/i), '450');
    await user.click(screen.getByRole('button', { name: /review and post/i }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/1 line will be received/i)).toBeInTheDocument();
    expect(
      within(dialog).getByText(
        /20 bags of kitten dry food — fd-kdc-2kg, lot L2409E → main warehouse/i,
      ),
    ).toBeInTheDocument();
    expect(within(dialog).getByText(/a posted receipt cannot be edited/i)).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: /post receipt/i }));
    await waitFor(() => expect(onPost).toHaveBeenCalledTimes(1));
    expect(onPost.mock.calls[0]![0].lines[0]).toMatchObject({
      skuId: 'k1',
      lotNumber: 'L2409E',
      quantityAccepted: '20',
      expiresOn: '2027-01-30',
      unitCostAmount: '450',
    });
  });

  it('U-C4: the draft is autosaved, offered back after a reload, restored, and cleared by Discard', async () => {
    const first = setup();
    await addFoodLine(first.user);
    await first.user.type(screen.getByLabelText(/lot number/i), 'L-DRAFT');
    await waitFor(() =>
      expect(window.sessionStorage.getItem(receiptDraftKey(STAFF))).toContain('L-DRAFT'),
    );
    // The draft holds ids, lots, dates, quantities and costs — never anything personal.
    expect(window.sessionStorage.getItem(receiptDraftKey(STAFF))).not.toMatch(
      /customer|email|mobile/i,
    );
    first.unmount();

    const second = setup();
    expect(await screen.findByText('Restore the receipt you were entering?')).toBeInTheDocument();
    await second.user.click(screen.getByRole('button', { name: /restore the draft/i }));
    expect(await screen.findByLabelText(/lot number/i)).toHaveValue('L-DRAFT');

    await second.user.click(screen.getByRole('button', { name: /discard draft/i }));
    const dialog = await screen.findByRole('dialog');
    await second.user.click(within(dialog).getByRole('button', { name: /discard draft/i }));
    await waitFor(() => expect(window.sessionStorage.getItem(receiptDraftKey(STAFF))).toBeNull());
  });

  it('U-C4b: a line scanned while the restore prompt is open survives the restore', async () => {
    const first = setup();
    await addFoodLine(first.user);
    await waitFor(() => expect(window.sessionStorage.getItem(receiptDraftKey(STAFF))).not.toBeNull());
    first.unmount();

    const second = setup();
    await screen.findByText('Restore the receipt you were entering?');
    await scan(second.user, vaccine.barcode!);
    await screen.findByRole('listitem', { name: /line 1: tricat vaccine/i });
    await second.user.click(screen.getByRole('button', { name: /restore the draft/i }));
    expect(await screen.findByRole('listitem', { name: /line 1: kitten dry food/i })).toBeInTheDocument();
    expect(screen.getByRole('listitem', { name: /line 2: tricat vaccine/i })).toBeInTheDocument();
  });

  it('U-C6: a 400 with three line violations lands each on its own field, scrolls to the first, shows no error code', async () => {
    const violation = (propertyName: string, errorCode: string, errorMessage: string) => ({
      propertyName,
      errorCode,
      errorMessage,
      resourceName: 'Inventory',
    });
    const onPost = vi.fn().mockRejectedValue(
      new ApiValidationError({
        isValid: false,
        errors: [
          violation(
            'Lines[0].ExpiresOn',
            'ExpiryDateRequired',
            'Food and medicine must be received with an expiry date.',
          ),
          violation(
            'Lines[0].LotNumber',
            'LotNumberRequired',
            'A lot number is required for accepted stock.',
          ),
          violation(
            'Lines[1].QuantityAccepted',
            'ReceiptExceedsOutstanding',
            'Only 4 is still outstanding on that order line.',
          ),
        ],
      }),
    );
    const scrolled: string[] = [];
    Element.prototype.scrollIntoView = function scrollIntoView(this: Element) {
      scrolled.push(this.id);
    };
    const { user } = setup({ onPost });
    await addFoodLine(user);
    await scan(user, vaccine.barcode!);
    await screen.findByRole('listitem', { name: /line 2: tricat vaccine/i });
    for (const [index, qty] of ['20', '5'].entries()) {
      await user.type(screen.getAllByLabelText(/^accepted/i)[index]!, qty);
    }
    await user.type(screen.getAllByLabelText(/lot number/i)[0]!, 'L1');
    await user.type(screen.getAllByLabelText(/lot number/i)[1]!, 'L2');
    await user.type(screen.getAllByLabelText(/expiry date/i)[0]!, '2027-01-30');
    await user.type(screen.getAllByLabelText(/expiry date/i)[1]!, '2027-02-28');
    for (const unitCost of screen.getAllByLabelText(/^unit cost/i)) await user.type(unitCost, '10');
    await user.click(screen.getByRole('button', { name: /review and post/i }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: /post receipt/i }),
    );

    expect(
      await screen.findByText('Only 4 is still outstanding on that order line.', { selector: 'p' }),
    ).toBeInTheDocument();
    expect(
      screen.getAllByText('Food and medicine must be received with an expiry date.', {
        selector: 'p',
      }),
    ).not.toHaveLength(0);
    expect(screen.getAllByLabelText(/expiry date/i)[0]).toHaveAttribute('aria-invalid', 'true');
    expect(document.body).not.toHaveTextContent(
      /ExpiryDateRequired|LotNumberRequired|ReceiptExceedsOutstanding/,
    );
    await waitFor(() => expect(scrolled[0]).toBe('receipt-line-0-expiresOn'));
  });
});
