import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { server } from '../../../mocks/server';
import { forbidden, orderingIds as ids, violation } from '../../../mocks/handlers/ordering/fixtures';
import { findOrder, resetOrderingMockData } from '../../../mocks/handlers/ordering/store';
import { StaffOrderDetail } from '@/components/orders/StaffOrderDetail';
import { StaffOrderQueue } from '@/components/orders/StaffOrderQueue';
import { CustomerOrdersPanel } from '@/components/orders/CustomerOrdersPanel';
import { ConfirmTransferDialog } from '@/components/orders/ConfirmTransferDialog';
import { ShipmentPanel } from '@/components/orders/StaffOrderPanels';
import { AwaitingPaymentList } from '@/components/orders/AwaitingPaymentList';
import { ReconciliationIssuesList } from '@/components/orders/ReconciliationIssuesList';
import { DispatchDialog } from '@/components/orders/DispatchDialog';
import { DeliveryFailureDialog } from '@/components/orders/DeliveryFailureDialog';
import { RecordDeliveryDialog } from '@/components/orders/RecordDeliveryDialog';
import { PickListPage } from '@/components/fulfilment/PickListPage';
import { ReturnsList } from '@/components/returns/ReturnsList';
import { RefundsDueList } from '@/components/refunds/RefundsDueList';
import { DeliveryZonesPage } from '@/components/ordering-admin/DeliveryZonesPage';
import { DiscountCodesPage } from '@/components/ordering-admin/DiscountCodesPage';
import { CouriersPage } from '@/components/ordering-admin/CouriersPage';
import { PaymentSettingsPage } from '@/components/ordering-admin/PaymentSettingsPage';
import { getStaffOrder } from '@/lib/api/staff-orders';
import { STAFF_ORDER_POLL_MS } from '@/lib/hooks/useStaffOrders';
import { setUrl } from '../booking/nav-mock';

vi.mock('next/navigation', () => import('../booking/nav-mock'));

function renderWithClient(ui: ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 0 }, mutations: { retry: false } } });
  return { client, ...render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>) };
}

beforeEach(() => {
  resetOrderingMockData();
  setUrl('/staff/orders');
});

describe('Polling does not disturb an open dialog - U-D2', () => {
  it('refetches every 30 s, keeps what was typed in the open dialog and updates the page behind it', async () => {
    const user = userEvent.setup();
    const { client } = renderWithClient(<StaffOrderDetail orderId={ids.orders.dispatchedCod} />);
    await user.click(await screen.findByRole('button', { name: 'Record delivery', hidden: false }));
    const dialog = await screen.findByRole('dialog');
    const rider = within(dialog).getByLabelText(/Rider/);
    await user.clear(rider);
    await user.type(rider, 'Rafiq');

    const intervals = client
      .getQueryCache()
      .findAll({ queryKey: ['staff', 'order'] })
      .flatMap((q) => q.observers.map((o) => o.options.refetchInterval));
    expect(intervals).toContain(STAFF_ORDER_POLL_MS);

    // The next poll brings a new courier status.
    findOrder(ids.orders.dispatchedCod)!.shipment!.lastTrackingStatus = 'Out for delivery';
    await client.invalidateQueries({ queryKey: ['staff', 'order'] });

    await waitFor(() => expect(screen.getByText('Out for delivery', { ignore: 'script' })).toBeInTheDocument());
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(within(screen.getByRole('dialog')).getByLabelText(/Rider/)).toHaveValue('Rafiq');
  });
});

describe('DispatchDialog - U-D3', () => {
  it('a BatchRecalled refusal shows the server message and a Reallocate shortcut', async () => {
    server.use(
      http.post('*/api/staff/orders/:orderId/dispatch', () =>
        violation('orderId', 'BatchRecalled', 'Batch B-2231 has been recalled and cannot be dispatched.'),
      ),
    );
    const onReallocate = vi.fn();
    const user = userEvent.setup();
    renderWithClient(<DispatchDialog orderId={ids.orders.confirmedCod} open onOpenChange={() => undefined} onReallocate={onReallocate} />);
    const dialog = await screen.findByRole('dialog');
    await waitFor(() => expect(within(dialog).getByRole('option', { name: 'Pathao Courier' })).toBeInTheDocument());
    await user.selectOptions(within(dialog).getByLabelText(/Courier/), 'Pathao Courier');
    await user.click(within(dialog).getByRole('button', { name: 'Dispatch' }));
    expect(await within(dialog).findByText('Batch B-2231 has been recalled and cannot be dispatched.')).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Reallocate a line' }));
    expect(onReallocate).toHaveBeenCalledTimes(1);
    expect(dialog.textContent).not.toMatch(/BatchRecalled/);
  });

  it('a refusal that is not about stock offers no Reallocate shortcut', async () => {
    server.use(
      http.post('*/api/staff/orders/:orderId/dispatch', () => violation('orderId', 'InvalidTransition', 'This order cannot be dispatched now.')),
    );
    const user = userEvent.setup();
    renderWithClient(<DispatchDialog orderId={ids.orders.confirmedCod} open onOpenChange={() => undefined} onReallocate={() => undefined} />);
    const dialog = await screen.findByRole('dialog');
    await waitFor(() => expect(within(dialog).getByRole('option', { name: 'Pathao Courier' })).toBeInTheDocument());
    await user.selectOptions(within(dialog).getByLabelText(/Courier/), 'Pathao Courier');
    await user.click(within(dialog).getByRole('button', { name: 'Dispatch' }));
    expect(await within(dialog).findByText('This order cannot be dispatched now.')).toBeInTheDocument();
    expect(within(dialog).queryByRole('button', { name: 'Reallocate a line' })).not.toBeInTheDocument();
  });

  it('asks for a courier before sending anything', async () => {
    let posted = 0;
    server.use(
      http.post('*/api/staff/orders/:orderId/dispatch', () => {
        posted += 1;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const user = userEvent.setup();
    renderWithClient(<DispatchDialog orderId={ids.orders.confirmedCod} open onOpenChange={() => undefined} onReallocate={() => undefined} />);
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Dispatch' }));
    expect(await within(dialog).findByText('Choose a courier.')).toBeInTheDocument();
    expect(posted).toBe(0);
  });
});

describe('Pay on collection by card - U-D5', () => {
  async function collectionOrder() {
    const base = await getStaffOrder(ids.orders.confirmedCod);
    return { ...base, fulfilmentMethod: 'Pickup' as const, paymentMethod: 'PayOnCollection' as const };
  }

  it('blocks a card tender without the terminal slip reference, then restates the amount', async () => {
    let posted = 0;
    server.use(
      http.post('*/api/staff/orders/:orderId/delivery', () => {
        posted += 1;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const order = await collectionOrder();
    const user = userEvent.setup();
    renderWithClient(<RecordDeliveryDialog order={order} open onOpenChange={() => undefined} />);
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('radio', { name: 'Card in store' }));
    const slip = within(dialog).getByLabelText(/Terminal slip reference/);
    expect(slip).toBeRequired();
    await user.click(within(dialog).getByRole('button', { name: 'Review' }));
    expect(await within(dialog).findByText('Enter the card terminal slip reference.')).toBeInTheDocument();
    expect(posted).toBe(0);
    expect(within(dialog).queryByRole('button', { name: 'Confirm' })).not.toBeInTheDocument();

    await user.type(slip, 'SLIP-77');
    await user.click(within(dialog).getByRole('button', { name: 'Review' }));
    expect(await within(dialog).findByText(new RegExp(`by card in store for ${order.orderNumber}\\?`))).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Confirm' })).toBeInTheDocument();
  });

  it('a cash tender needs no slip reference', async () => {
    const order = await collectionOrder();
    const user = userEvent.setup();
    renderWithClient(<RecordDeliveryDialog order={order} open onOpenChange={() => undefined} />);
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).queryByLabelText(/Terminal slip reference/)).not.toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Review' }));
    expect(await within(dialog).findByText(new RegExp(`in cash for ${order.orderNumber}\\?`))).toBeInTheDocument();
  });
});

describe('DeliveryFailureDialog - U-D6', () => {
  it('a delivery offers Refused and Undeliverable and says the parcel must be received into quarantine', async () => {
    renderWithClient(<DeliveryFailureDialog orderId={ids.orders.dispatchedCod} fulfilmentMethod="Delivery" open onOpenChange={() => undefined} />);
    const dialog = await screen.findByRole('dialog', { name: 'Record delivery failure' });
    expect(within(dialog).getAllByRole('radio')).toHaveLength(2);
    expect(within(dialog).queryByRole('radio', { name: /Not collected|Uncollected/i })).not.toBeInTheDocument();
    expect(dialog).toHaveTextContent('A return will be opened and the parcel must be received into quarantine when it comes back.');
  });

  it('a pickup offers only Not collected and says the items go back to stock', async () => {
    renderWithClient(<DeliveryFailureDialog orderId={ids.orders.packingPickup} fulfilmentMethod="Pickup" open onOpenChange={() => undefined} />);
    const dialog = await screen.findByRole('dialog', { name: 'Record not collected' });
    expect(within(dialog).getAllByRole('radio')).toHaveLength(1);
    expect(dialog).toHaveTextContent('The items are released back to stock and the order is closed.');
  });

  it('recording a refusal sends the reason and hands over the return that was opened', async () => {
    let body: unknown;
    server.use(
      http.post('*/api/staff/orders/:orderId/delivery-failure', async ({ request }) => {
        body = await request.json();
        return HttpResponse.json({ returnRequestId: 'ret-1' });
      }),
    );
    const onReturnOpened = vi.fn();
    const user = userEvent.setup();
    renderWithClient(
      <DeliveryFailureDialog orderId={ids.orders.dispatchedCod} fulfilmentMethod="Delivery" open onOpenChange={() => undefined} onReturnOpened={onReturnOpened} />,
    );
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Record' }));
    await waitFor(() => expect(onReturnOpened).toHaveBeenCalledWith('ret-1'));
    expect(body).toMatchObject({ reason: 'Refused' });
  });
});

describe('A 403 on any Ordering staff screen shows the permission state - U-X2', () => {
  const screens: Array<[string, () => ReactElement]> = [
    ['order queue', () => <StaffOrderQueue />],
    ['order detail', () => <StaffOrderDetail orderId={ids.orders.confirmedCod} />],
    ['awaiting payment', () => <AwaitingPaymentList />],
    ['reconciliation issues', () => <ReconciliationIssuesList />],
    ['pick list', () => <PickListPage />],
    ['returns', () => <ReturnsList />],
    ['refunds due', () => <RefundsDueList />],
    ['delivery zones', () => <DeliveryZonesPage />],
    ['discount codes', () => <DiscountCodesPage />],
    ['couriers', () => <CouriersPage />],
    ['payment settings', () => <PaymentSettingsPage />],
  ];

  it.each(screens)('%s', async (_name, make) => {
    server.use(
      http.get(/\/api\/(staff|admin)\//, ({ request }) => {
        const path = new URL(request.url).pathname;
        // The branch and session lookups the shell makes are not what is under test.
        if (/\/branches|\/session|\/me\b/.test(path)) return undefined;
        return forbidden();
      }),
    );
    renderWithClient(make());
    expect(await screen.findByText(/You don.t have permission to do this/)).toBeInTheDocument();
    expect(screen.getByRole('alert')).not.toHaveTextContent(/ActionNotPermitted/);
  });
});

describe('Order queue date filter sends instants in the branch zone', () => {
  it('"12 Oct to 12 Oct" asks for 18:00Z the day before to 17:59:59.999Z, not bare dates read as UTC midnight', async () => {
    const seen: URL[] = [];
    server.use(
      http.get('*/api/staff/orders', ({ request }) => {
        seen.push(new URL(request.url));
        return HttpResponse.json({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 });
      }),
    );
    setUrl('/staff/orders?from=2026-10-12&to=2026-10-12');
    renderWithClient(<StaffOrderQueue />);
    await waitFor(() => expect(seen.length).toBeGreaterThan(0));
    const last = seen.at(-1)!.searchParams;
    expect(last.get('from')).toBe('2026-10-11T18:00:00.000Z');
    expect(last.get('to')).toBe('2026-10-12T17:59:59.999Z');
  });

  it('sends nothing for an empty filter', async () => {
    const seen: URL[] = [];
    server.use(
      http.get('*/api/staff/orders', ({ request }) => {
        seen.push(new URL(request.url));
        return HttpResponse.json({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 });
      }),
    );
    renderWithClient(<StaffOrderQueue />);
    await waitFor(() => expect(seen.length).toBeGreaterThan(0));
    expect(seen.at(-1)!.searchParams.has('from')).toBe(false);
    expect(seen.at(-1)!.searchParams.has('to')).toBe(false);
  });

  it('the mock reads the instants the way the API does: an order placed on the chosen day (Dhaka) is inside the window', async () => {
    // 2026-10-12 03:00 Dhaka = 2026-10-11T21:00Z - on the 12th in Dhaka, but the 11th in UTC.
    const early = findOrder(ids.orders.confirmedCod)!;
    early.placedOn = '2026-10-11T21:00:00Z';
    setUrl('/staff/orders?from=2026-10-12&to=2026-10-12');
    renderWithClient(<StaffOrderQueue />);
    expect((await screen.findAllByText(early.orderNumber)).length).toBeGreaterThan(0);
  });
});

describe('Customer orders panel (staff customer page) - ORD-82 frontend, HO-ORD-002-FU1', () => {
  const counterSale = () => screen.getByRole('link', { name: 'New counter sale for this customer' });

  it("lists the customer's own orders from the customer endpoint, never the queue", async () => {
    const paths: string[] = [];
    server.events.on('request:start', ({ request }) => {
      paths.push(new URL(request.url).pathname);
    });
    const own = findOrder(ids.orders.transferReported)!;
    renderWithClient(<CustomerOrdersPanel customerId={own.customerId!} />);
    expect(counterSale()).toHaveAttribute('href', `/staff/pos?customerId=${own.customerId}`);
    const link = await screen.findByRole('link', { name: own.orderNumber });
    expect(link).toHaveAttribute('href', `/staff/orders/${own.id}`);
    expect(screen.queryByRole('link', { name: findOrder(ids.orders.heldPayment)!.orderNumber })).not.toBeInTheDocument();
    expect(paths.some((p) => p === `/api/staff/customers/${own.customerId}/orders`)).toBe(true);
    expect(paths.some((p) => p === '/api/staff/orders')).toBe(false);
    server.events.removeAllListeners();
  });

  it('shows a loading state, then an empty state with the counter sale kept', async () => {
    server.use(
      http.get('*/api/staff/customers/:customerId/orders', () => HttpResponse.json({ items: [], pageNumber: 1, pageSize: 5, totalCount: 0 })),
    );
    renderWithClient(<CustomerOrdersPanel customerId="no-orders" />);
    expect(screen.getByText(/Loading Customer orders/)).toBeInTheDocument();
    expect(await screen.findByText('No orders yet.')).toBeInTheDocument();
    expect(counterSale()).toBeInTheDocument();
  });

  it('shows an error state with Try again that recovers', async () => {
    let fail = true;
    server.use(
      http.get('*/api/staff/customers/:customerId/orders', () =>
        fail ? new HttpResponse(null, { status: 500 }) : HttpResponse.json({ items: [], pageNumber: 1, pageSize: 5, totalCount: 0 }),
      ),
    );
    renderWithClient(<CustomerOrdersPanel customerId="c-1" />);
    const retry = await screen.findByRole('button', { name: /try again|retry/i }, { timeout: 4000 });
    fail = false;
    await userEvent.click(retry);
    expect(await screen.findByText('No orders yet.')).toBeInTheDocument();
  });

  it('a 403 hides the list and keeps the counter sale', async () => {
    server.use(http.get('*/api/staff/customers/:customerId/orders', () => forbidden()));
    renderWithClient(<CustomerOrdersPanel customerId="c-1" />);
    expect(await screen.findByText(/You cannot view this customer’s order history right now/)).toBeInTheDocument();
    expect(counterSale()).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('the mock answers 404 for an unknown customer, as the endpoint does', async () => {
    renderWithClient(<CustomerOrdersPanel customerId="c0000000-0000-0000-0000-00000000ffff" />);
    expect(await screen.findByText('This customer was not found.')).toBeInTheDocument();
  });

  it('a status change on an order refreshes the panel (invalidated under the staff orders prefix)', async () => {
    const own = findOrder(ids.orders.transferReported)!;
    const { client } = renderWithClient(<CustomerOrdersPanel customerId={own.customerId!} />);
    expect(await screen.findByRole('link', { name: own.orderNumber })).toBeInTheDocument();
    expect(screen.getAllByText('Waiting for payment').length).toBeGreaterThan(0);
    own.status = 'Cancelled';
    await client.invalidateQueries({ queryKey: ['staff', 'orders'] });
    await waitFor(() => expect(screen.queryByText('Waiting for payment')).not.toBeInTheDocument());
  });

  it('a 404 says the customer was not found', async () => {
    server.use(http.get('*/api/staff/customers/:customerId/orders', () => new HttpResponse(null, { status: 404 })));
    renderWithClient(<CustomerOrdersPanel customerId="c-1" />);
    expect(await screen.findByText('This customer was not found.')).toBeInTheDocument();
  });

  it('pages with the server: asks for page 2 when Next is pressed', async () => {
    const pages: string[] = [];
    const row = (n: number) => ({
      id: `o-${n}`, orderNumber: `ORD-${n}`, placedOn: '2026-10-12T05:00:00Z', status: 'Confirmed', fulfilmentMethod: 'Delivery',
      paymentMethod: 'CashOnDelivery', channel: 'Online', grandTotal: { amount: '100.00', currencyCode: 'BDT' }, branchTimeZoneId: ids.timeZoneId,
    });
    server.use(
      http.get('*/api/staff/customers/:customerId/orders', ({ request }) => {
        const n = Number(new URL(request.url).searchParams.get('pageNumber') ?? 1);
        pages.push(String(n));
        return HttpResponse.json({ items: [row(n)], pageNumber: n, pageSize: 5, totalCount: 7 });
      }),
    );
    renderWithClient(<CustomerOrdersPanel customerId="c-1" />);
    expect(await screen.findByText('ORD-1')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /next/i }));
    expect(await screen.findByText('ORD-2')).toBeInTheDocument();
    expect(pages).toContain('2');
  });
});

describe('Staff tracking link - SEC LOW', () => {
  async function shipmentOrder(trackingUrl: string | null) {
    const base = await getStaffOrder(ids.orders.dispatchedCod);
    return { ...base, shipment: { ...base.shipment!, trackingNumber: 'TRK-1', trackingUrl } };
  }

  it('links an http(s) tracking URL', async () => {
    renderWithClient(<ShipmentPanel order={await shipmentOrder('https://track.example/TRK-1')} onEditTracking={() => undefined} />);
    expect(screen.getByRole('link', { name: 'TRK-1' })).toHaveAttribute('href', 'https://track.example/TRK-1');
  });

  it.each(['javascript:alert(1)', 'data:text/html,x', 'ftp://x.example/a'])('shows %s as plain text, never as a link', async (url) => {
    renderWithClient(<ShipmentPanel order={await shipmentOrder(url)} onEditTracking={() => undefined} />);
    expect(screen.getByText('TRK-1')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'TRK-1' })).not.toBeInTheDocument();
  });
});

describe('Money dialogs renew the Idempotency-Key after a definitive refusal', () => {
  it('Confirm transfer: refused once, retried with the same body, sent with a different key', async () => {
    const keys: Array<string | null> = [];
    server.use(
      http.post('*/api/staff/orders/:orderId/payment/bank-transfer', ({ request }) => {
        keys.push(request.headers.get('Idempotency-Key'));
        return violation('Amount', 'AmountMismatch', 'The amount does not match.');
      }),
    );
    const user = userEvent.setup();
    renderWithClient(
      <ConfirmTransferDialog
        target={{ orderId: ids.orders.awaitingTransfer, orderNumber: 'ORD-2026-000101', version: 1, amount: { amount: '1000.00', currencyCode: 'BDT' }, zone: 'Asia/Dhaka' }}
        open
        onOpenChange={() => undefined}
        onDone={() => undefined}
      />,
    );
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText(/Bank reference/), 'BK-1');
    for (let attempt = 0; attempt < 2; attempt += 1) {
      await user.click(within(dialog).getByRole('button', { name: /^(Review|Back)$|Review/ }));
      await user.click(await within(dialog).findByRole('button', { name: 'Confirm' }));
      await waitFor(() => expect(keys).toHaveLength(attempt + 1));
      await within(dialog).findByText('The amount does not match.');
    }
    expect(keys[0]).toBeTruthy();
    expect(keys[1]).not.toBe(keys[0]);
  });
});

describe('Entering the tracking number later - US-ORD-11 AC2', () => {
  it('a dispatched order with no tracking number offers "Enter tracking number"; saving sends PUT .../tracking and the panel shows it', async () => {
    findOrder(ids.orders.dispatchedCod)!.shipment!.trackingNumber = null;
    let seen: { method: string; path: string; body: unknown } | undefined;
    server.events.on('request:start', async ({ request }) => {
      if (request.method === 'PUT' && request.url.endsWith(`/api/staff/orders/${ids.orders.dispatchedCod}/tracking`)) {
        seen = { method: request.method, path: new URL(request.url).pathname, body: await request.clone().json() };
      }
    });
    const user = userEvent.setup();
    renderWithClient(<StaffOrderDetail orderId={ids.orders.dispatchedCod} />);
    expect(await screen.findByText('Not entered yet')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Enter tracking number' }));
    const dialog = await screen.findByRole('dialog', { name: 'Enter tracking number' });
    await user.click(within(dialog).getByRole('button', { name: 'Save tracking number' }));
    expect(await within(dialog).findByText('Enter the tracking number.')).toBeInTheDocument();
    expect(seen).toBeUndefined();
    await user.type(within(dialog).getByLabelText(/Tracking number/), '  TRK-2026-77  ');
    await user.click(within(dialog).getByRole('button', { name: 'Save tracking number' }));
    await waitFor(() => expect(seen).toBeDefined());
    expect(seen).toMatchObject({ method: 'PUT', body: { trackingNumber: 'TRK-2026-77' } });
    expect(await screen.findByText('TRK-2026-77')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Change tracking number' })).toBeInTheDocument();
    server.events.removeAllListeners();
  });

  it('a server refusal is shown in the dialog and the number typed is kept', async () => {
    findOrder(ids.orders.dispatchedCod)!.shipment!.trackingNumber = null;
    server.use(
      http.put('*/api/staff/orders/:orderId/tracking', () => violation('TrackingNumber', 'TrackingNumberInvalid', 'That tracking number is not valid for this courier.')),
    );
    const user = userEvent.setup();
    renderWithClient(<StaffOrderDetail orderId={ids.orders.dispatchedCod} />);
    await user.click(await screen.findByRole('button', { name: 'Enter tracking number' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText(/Tracking number/), 'BAD');
    await user.click(within(dialog).getByRole('button', { name: 'Save tracking number' }));
    expect(await within(dialog).findByText('That tracking number is not valid for this courier.')).toBeInTheDocument();
    expect(within(dialog).getByLabelText(/Tracking number/)).toHaveValue('BAD');
    expect(dialog.textContent).not.toMatch(/TrackingNumberInvalid/);
  });
});
