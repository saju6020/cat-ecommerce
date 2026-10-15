import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { StrictMode, type ReactElement } from 'react';
import { server } from '../../../mocks/server';
import { violation } from '../../../mocks/handlers/ordering/fixtures';
import { MyOrderDetail } from '@/components/orders/MyOrderDetail';
import { ReturnRequestScreen } from '@/components/orders/ReturnRequestForm';
import { TransferCountdown } from '@/components/orders/TransferCountdown';
import { TrackingPanel } from '@/components/orders/OrderPanels';
import type { CustomerOrderDetail } from '@/lib/api/my-orders';
import { CARD_NUMBER_MESSAGE } from '@/lib/validation/cardNumberGuard';

vi.mock('next/navigation', () => import('../booking/nav-mock'));

function renderWithClient(ui: ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 0 }, mutations: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const money = (amount: string) => ({ amount, currencyCode: 'BDT' });
const ZERO = money('0.00');
const TOTALS = {
  subtotal: money('1000.00'),
  lineDiscountTotal: ZERO,
  orderDiscount: ZERO,
  deliveryCharge: ZERO,
  taxTotal: ZERO,
  grandTotal: money('1000.00'),
};

function order(overrides: Partial<CustomerOrderDetail> = {}): CustomerOrderDetail {
  return {
    id: 'o1',
    orderNumber: 'ORD-2026-000200',
    placedOn: '2026-10-10T04:00:00Z',
    status: 'Confirmed',
    fulfilmentMethod: 'Delivery',
    paymentMethod: 'CashOnDelivery',
    branchTimeZoneId: 'Asia/Dhaka',
    lines: [
      {
        lineId: 'ln1',
        lineNumber: 1,
        skuId: 's1',
        productName: 'Whiskas Kitten',
        variantLabel: '1 kg',
        cat: null,
        quantity: 2,
        unitPrice: money('500.00'),
        lineSubtotal: money('1000.00'),
        lineDiscount: ZERO,
        orderDiscountShare: ZERO,
        taxRatePercent: 0,
        taxAmount: ZERO,
        lineTotal: money('1000.00'),
      },
    ],
    totals: TOTALS,
    fulfilment: {
      recipientName: 'Sam Rahman',
      recipientPhone: '01711111111',
      line1: '1 Road',
      line2: null,
      city: 'Dhaka',
      district: 'Dhaka',
      postalCode: null,
      deliveryInstructions: null,
      zoneName: 'Dhaka',
      serviceLevel: 'Standard',
      slotDate: null,
      slotWindow: null,
      pickupBranchId: null,
      pickupBranchName: null,
      pickupBranchAddress: null,
    },
    statusHistory: [{ to: 'Confirmed', changedOn: '2026-10-10T04:00:00Z' }],
    paymentDueBy: null,
    transferInstructions: null,
    customerTransferReference: null,
    shipment: null,
    collectBy: null,
    invoiceNumber: null,
    amountPaid: ZERO,
    amountRefunded: ZERO,
    refundDue: ZERO,
    refunds: [],
    returns: [],
    canCancel: false,
    canRequestReturn: false,
    returnWindowEndsOn: null,
    returnableLines: [],
    canReorder: false,
    ...overrides,
  };
}

function serveOrder(value: CustomerOrderDetail) {
  server.use(
    http.get('*/api/customers/me/orders/o1', () => HttpResponse.json(value)),
    http.get('*/api/branches', () => HttpResponse.json([])),
  );
}

const TRANSFER = {
  accountName: 'PurrCare Ltd',
  accountNumber: '1234567890',
  bankName: 'City Bank',
  branchName: 'Gulshan',
  routingNumber: null,
  paymentReference: 'ORD-2026-000200',
  amountDue: money('1000.00'),
};

describe('Pending bank-transfer order - U-C1', () => {
  it('shows the instructions, the reference prominently, a copy button per detail and the countdown', async () => {
    const dueBy = new Date(Date.now() + 5 * 3_600_000 + 30 * 60_000).toISOString();
    serveOrder(order({ status: 'PendingPayment', paymentMethod: 'BankTransfer', paymentDueBy: dueBy, transferInstructions: TRANSFER }));
    renderWithClient(<MyOrderDetail orderId="o1" />);
    expect(await screen.findByRole('heading', { name: 'Pay by bank transfer' })).toBeInTheDocument();
    expect(screen.getAllByText('ORD-2026-000200').length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: 'Copy payment reference' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Copy account number' })).toBeInTheDocument();
    expect(screen.getByText('1234567890')).toBeInTheDocument();
    expect(screen.getByText(/Transfer by /)).toBeInTheDocument();
    expect(screen.getByRole('timer')).toHaveTextContent(/Time left: 5 h/);
    expect(screen.getByRole('button', { name: /made the transfer/ })).toBeInTheDocument();
  });

  it('reports the customer reference and thanks them, refusing a card number', async () => {
    let body: unknown;
    server.use(
      http.post('*/api/customers/me/orders/o1/transfer-reference', async ({ request }) => {
        body = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    serveOrder(
      order({ status: 'PendingPayment', paymentMethod: 'BankTransfer', paymentDueBy: new Date(Date.now() + 3_600_000 * 20).toISOString(), transferInstructions: TRANSFER }),
    );
    const user = userEvent.setup();
    renderWithClient(<MyOrderDetail orderId="o1" />);
    await user.click(await screen.findByRole('button', { name: /made the transfer/ }));
    const dialog = await screen.findByRole('dialog');
    const input = within(dialog).getByRole('textbox');
    await user.type(input, '4111 1111 1111 1111');
    await user.click(within(dialog).getByRole('button', { name: /^(Send|Submit|Save|Tell us)/ }));
    expect(await within(dialog).findByText(CARD_NUMBER_MESSAGE)).toBeInTheDocument();
    expect(body).toBeUndefined();
    await user.clear(input);
    await user.type(input, 'TRX99812');
    await user.click(within(dialog).getByRole('button', { name: /^(Send|Submit|Save|Tell us)/ }));
    await waitFor(() => expect(body).toEqual({ reference: 'TRX99812' }));
    expect(await screen.findByText(/we.ll confirm within one working day/)).toBeInTheDocument();
  });
});

describe('Transfer countdown announcements - U-C2', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('announces at 6 h, 1 h and expiry only - and says nothing in between', () => {
    vi.useFakeTimers({ toFake: ['Date', 'setInterval', 'clearInterval'] });
    vi.setSystemTime(new Date('2026-10-12T00:00:00Z'));
    const dueBy = new Date('2026-10-12T07:00:00Z').toISOString();
    const onExpired = vi.fn();
    render(<TransferCountdown dueBy={dueBy} zone="Asia/Dhaka" onExpired={onExpired} />);
    const live = () => document.querySelector('[aria-live="polite"]')!.textContent;
    expect(screen.getByRole('timer')).toHaveTextContent('Time left: 7 h');
    expect(live()).toBe('');

    act(() => void vi.advanceTimersByTime(30 * 60_000)); // 6 h 30 left
    expect(live()).toBe('');
    act(() => void vi.advanceTimersByTime(30 * 60_000 + 1_000)); // just under 6 h
    expect(live()).toBe('About 6 hours left to make the transfer.');
    act(() => void vi.advanceTimersByTime(2 * 3_600_000)); // 4 h - still the same milestone
    expect(live()).toBe('About 6 hours left to make the transfer.');
    act(() => void vi.advanceTimersByTime(3 * 3_600_000)); // 1 h left or just under
    expect(live()).toBe('About 1 hour left to make the transfer.');
    expect(onExpired).not.toHaveBeenCalled();
    act(() => void vi.advanceTimersByTime(3_600_000 + 1_000));
    expect(live()).toBe('The time to make the transfer has passed.');
    expect(screen.getByRole('timer')).toHaveTextContent('The time to make the transfer has passed. We are checking the order');
    expect(onExpired).toHaveBeenCalledTimes(1);
  });

  it('keeps asking the server after the deadline, every 10 s, because the order is cancelled a moment later', () => {
    vi.useFakeTimers({ toFake: ['Date', 'setInterval', 'clearInterval'] });
    vi.setSystemTime(new Date('2026-10-12T00:00:00Z'));
    const onExpired = vi.fn();
    render(<TransferCountdown dueBy={new Date('2026-10-12T00:00:05Z').toISOString()} zone="Asia/Dhaka" onExpired={onExpired} />);
    act(() => void vi.advanceTimersByTime(6_000)); // the deadline passes
    expect(onExpired).toHaveBeenCalledTimes(1);
    act(() => void vi.advanceTimersByTime(10_000));
    expect(onExpired).toHaveBeenCalledTimes(2);
    act(() => void vi.advanceTimersByTime(30_000));
    expect(onExpired).toHaveBeenCalledTimes(5);
    // ...but not forever: it gives up after a few minutes.
    act(() => void vi.advanceTimersByTime(10 * 60_000));
    expect(onExpired.mock.calls.length).toBeLessThanOrEqual(20);
    const settled = onExpired.mock.calls.length;
    act(() => void vi.advanceTimersByTime(5 * 60_000));
    expect(onExpired).toHaveBeenCalledTimes(settled);
  });

  it('stops asking when the countdown goes away (the order left PendingPayment)', () => {
    vi.useFakeTimers({ toFake: ['Date', 'setInterval', 'clearInterval'] });
    vi.setSystemTime(new Date('2026-10-12T00:00:00Z'));
    const onExpired = vi.fn();
    const { unmount } = render(<TransferCountdown dueBy={new Date('2026-10-12T00:00:05Z').toISOString()} zone="Asia/Dhaka" onExpired={onExpired} />);
    act(() => void vi.advanceTimersByTime(6_000));
    unmount();
    act(() => void vi.advanceTimersByTime(60_000));
    expect(onExpired).toHaveBeenCalledTimes(1);
  });

  it('under React StrictMode, opening inside the last hour still does not announce on arrival', () => {
    render(
      <StrictMode>
        <TransferCountdown dueBy={new Date(Date.now() + 30 * 60_000).toISOString()} zone="Asia/Dhaka" />
      </StrictMode>,
    );
    expect(document.querySelector('[aria-live="polite"]')!.textContent).toBe('');
  });

  it('the timer region itself is not live', () => {
    render(<TransferCountdown dueBy={new Date(Date.now() + 9 * 3_600_000).toISOString()} zone="Asia/Dhaka" />);
    expect(screen.getByRole('timer')).not.toHaveAttribute('aria-live');
  });

  it('opening an order already inside the last hour does not announce on arrival', () => {
    render(<TransferCountdown dueBy={new Date(Date.now() + 30 * 60_000).toISOString()} zone="Asia/Dhaka" />);
    expect(document.querySelector('[aria-live="polite"]')!.textContent).toBe('');
  });
});

describe('Cancelling a paid order - U-C4', () => {
  it('the confirmation says the paid money is refunded', async () => {
    serveOrder(order({ canCancel: true, amountPaid: money('1000.00'), paymentMethod: 'BankTransfer' }));
    const user = userEvent.setup();
    renderWithClient(<MyOrderDetail orderId="o1" />);
    await user.click(await screen.findByRole('button', { name: 'Cancel order' }));
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent('Cancel order ORD-2026-000200?');
    expect(dialog).toHaveTextContent('Any money you have paid will be refunded by the shop.');
  });

  it('an unpaid order does not mention a refund', async () => {
    serveOrder(order({ canCancel: true }));
    const user = userEvent.setup();
    renderWithClient(<MyOrderDetail orderId="o1" />);
    await user.click(await screen.findByRole('button', { name: 'Cancel order' }));
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent('Your reserved items are released.');
    expect(dialog).not.toHaveTextContent(/refund/i);
  });
});

describe('Dispatched order tracking - U-C6', () => {
  it('shows the courier, the tracking number and a link only when the courier has a URL', () => {
    const { rerender } = render(
      <TrackingPanel shipment={{ courierName: 'Steadfast', trackingNumber: 'SF123', trackingUrl: 'https://track.example/SF123', lastTrackingStatus: 'In transit' }} />,
    );
    expect(screen.getByText('Steadfast')).toBeInTheDocument();
    expect(screen.getByText('SF123')).toBeInTheDocument();
    expect(screen.getByText('In transit')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Track on the courier/ })).toHaveAttribute('href', 'https://track.example/SF123');
    rerender(<TrackingPanel shipment={{ courierName: 'Local rider', trackingNumber: 'LR9', trackingUrl: null, lastTrackingStatus: null }} />);
    expect(screen.getByText('LR9')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Track on the courier/ })).not.toBeInTheDocument();
  });

  it('never makes a link of an unsafe URL', () => {
    render(<TrackingPanel shipment={{ courierName: 'X', trackingNumber: '1', trackingUrl: 'javascript:alert(1)', lastTrackingStatus: null }} />);
    expect(screen.queryByRole('link', { name: /Track on the courier/ })).not.toBeInTheDocument();
  });

  it('on a dispatched order the panel appears on the detail page', async () => {
    serveOrder(order({ status: 'Dispatched', shipment: { courierName: 'Steadfast', trackingNumber: 'SF123', trackingUrl: null, lastTrackingStatus: null } }));
    renderWithClient(<MyOrderDetail orderId="o1" />);
    expect(await screen.findByRole('heading', { name: 'Track your parcel' })).toBeInTheDocument();
    expect(screen.getByText('SF123')).toBeInTheDocument();
  });
});

describe('Return request - U-C7', () => {
  const delivered = order({
    status: 'Delivered',
    canRequestReturn: true,
    lines: [
      { ...order().lines[0]!, lineId: 'food', productName: 'Royal Canin Kitten' },
      { ...order().lines[0]!, lineId: 'litter', lineNumber: 2, productName: 'Tofu Litter' },
    ],
    returnableLines: [
      { lineId: 'food', maxQuantity: 2, returnClass: 'Food' },
      { lineId: 'litter', maxQuantity: 2, returnClass: 'Litter' },
    ],
  });

  it('shows the opened-food and used-litter policy once a line is chosen, and caps the quantity', async () => {
    serveOrder(delivered);
    const user = userEvent.setup();
    renderWithClient(<ReturnRequestScreen orderId="o1" />);
    await screen.findByRole('heading', { name: 'Request a return' });
    await user.click(screen.getAllByRole('checkbox')[0]!);
    expect(screen.getByText("Opened food can only be returned if there's a quality problem or a recall.")).toBeInTheDocument();
    expect(screen.getByText('Up to 2.')).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: /Sealed/ })).toBeChecked();
    await user.click(screen.getAllByRole('checkbox')[1]!);
    expect(screen.getByText("Used litter can't be returned.")).toBeInTheDocument();
  });

  it('renders the server 400 against the line it names, not at the top', async () => {
    serveOrder(delivered);
    server.use(
      http.post('*/api/customers/me/orders/o1/returns', () =>
        violation('lines[0].condition', 'OpenedFoodNotReturnable', 'Opened food can only be returned for a quality problem.'),
      ),
    );
    const user = userEvent.setup();
    renderWithClient(<ReturnRequestScreen orderId="o1" />);
    await screen.findByRole('heading', { name: 'Request a return' });
    await user.click(screen.getAllByRole('checkbox')[0]!);
    await user.click(screen.getByRole('radio', { name: /Opened/ }));
    await user.click(screen.getByRole('button', { name: 'Send return request' }));
    const message = await screen.findByText('Opened food can only be returned for a quality problem.');
    expect(message.closest('fieldset')).toHaveTextContent('Royal Canin Kitten');
    expect(message).toHaveAttribute('role', 'alert');
    expect(document.body.textContent).not.toMatch(/OpenedFoodNotReturnable/);
  });

  it('keeps Send disabled with its reason until a line is chosen', async () => {
    serveOrder(delivered);
    renderWithClient(<ReturnRequestScreen orderId="o1" />);
    const send = await screen.findByRole('button', { name: 'Send return request' });
    expect(send).toBeDisabled();
    expect(screen.getByText('Choose at least one item to return.')).toBeInTheDocument();
  });
});
