import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import { delay, http, HttpResponse } from 'msw';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { server } from '../../../mocks/server';
import { orderingIds as ids } from '../../../mocks/handlers/ordering/fixtures';
import { resetOrderingMockData } from '../../../mocks/handlers/ordering/store';
import { PosScreen } from '@/components/pos/PosScreen';
import { ReceiptPage } from '@/components/pos/ReceiptPage';
import { setUrl } from '../booking/nav-mock';

vi.mock('next/navigation', () => import('../booking/nav-mock'));

function renderWithClient(ui: ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 0 }, mutations: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

/** Barcodes of the shop mock: `89` + the SKU number padded to 11 digits. Whiskas Kitten 1 kg costs 780.00. */
const WHISKAS = '8900000000201';
const KITTEN_400 = '8900000000101';

beforeEach(() => {
  resetOrderingMockData();
  setUrl('/staff/pos');
});

async function scan(user: ReturnType<typeof userEvent.setup>, code: string) {
  const field = screen.getByLabelText('Scan a barcode');
  await user.click(field);
  await user.type(field, `${code}{Enter}`);
}

async function fillCustomer(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText(/Customer name/), 'Walk-in Buyer');
  await user.type(screen.getByLabelText(/Mobile number/), '01711112222');
}

async function takePayment(user: ReturnType<typeof userEvent.setup>, method: 'Cash' | 'InStoreCard', amount: string, slip?: string) {
  await user.selectOptions(screen.getByLabelText('Paid with'), method);
  await user.type(screen.getByLabelText(/handed over|charged/), amount);
  if (slip) await user.type(screen.getByLabelText('Terminal slip reference'), slip);
  await user.click(screen.getByRole('button', { name: 'Add payment' }));
}

async function completeSale(user: ReturnType<typeof userEvent.setup>) {
  const button = screen.getByRole('button', { name: 'Complete sale' });
  await waitFor(() => expect(button).toBeEnabled());
  await user.click(button);
  const dialog = await screen.findByRole('dialog');
  await user.click(within(dialog).getByRole('button', { name: 'Complete sale' }));
}

describe('Tender, change and refusals - U-F4', () => {
  it('cash over the total: the change comes from the sale response', async () => {
    const user = userEvent.setup();
    renderWithClient(<PosScreen />);
    await scan(user, WHISKAS);
    expect(await screen.findByRole('list', { name: 'Basket' })).toBeInTheDocument();
    await fillCustomer(user);
    await takePayment(user, 'Cash', '1000');
    await completeSale(user);
    expect(await screen.findByRole('heading', { name: /Sale complete/ })).toBeInTheDocument();
    expect(screen.getByText(/Change due:/)).toHaveTextContent(/220\.00/);
  });

  it('a card charge above what is owed is refused with the server message and nothing is recorded', async () => {
    const user = userEvent.setup();
    renderWithClient(<PosScreen />);
    await scan(user, WHISKAS);
    await screen.findByRole('list', { name: 'Basket' });
    await fillCustomer(user);
    await takePayment(user, 'InStoreCard', '1000', 'SLIP-1');
    await completeSale(user);
    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Card payments cannot be more than what is owed.');
    expect(screen.queryByRole('heading', { name: /Sale complete/ })).not.toBeInTheDocument();
    expect(document.body.textContent).not.toMatch(/TendersDoNotCoverTotal/);
  });

  it('cash short of the total is refused with the server message', async () => {
    const user = userEvent.setup();
    renderWithClient(<PosScreen />);
    await scan(user, WHISKAS);
    await screen.findByRole('list', { name: 'Basket' });
    await fillCustomer(user);
    await takePayment(user, 'Cash', '100');
    await completeSale(user);
    expect(await screen.findByRole('alert')).toHaveTextContent('The payments do not cover the total of the sale.');
  });

  it('a card payment without a slip reference is stopped on the screen before anything is sent', async () => {
    let sales = 0;
    server.events.on('request:start', ({ request }) => {
      if (request.method === 'POST' && request.url.endsWith('/api/staff/pos/sales')) sales += 1;
    });
    const user = userEvent.setup();
    renderWithClient(<PosScreen />);
    await scan(user, WHISKAS);
    await screen.findByRole('list', { name: 'Basket' });
    await takePayment(user, 'InStoreCard', '780');
    expect(await screen.findByText('Enter the card terminal slip reference.')).toBeInTheDocument();
    expect(screen.queryByRole('list', { name: 'Payments taken' })).not.toBeInTheDocument();
    expect(sales).toBe(0);
    server.events.removeAllListeners();
  });
});

describe('Receipt route - U-F6', () => {
  it('renders the 80 mm sheet from the server data, and switches to the server plain text', async () => {
    const user = userEvent.setup();
    renderWithClient(<ReceiptPage orderId={ids.orders.counterSale} />);
    const heading = await screen.findByText(/ORD-2026-000109/);
    expect(heading).toBeInTheDocument();
    expect(screen.getByRole('table', { name: 'Items sold' })).toBeInTheDocument();
    expect(screen.getByText('Change')).toBeInTheDocument();
    expect(document.querySelector('style')?.textContent).toContain('size: 80mm auto');

    const toggle = screen.getByRole('button', { name: 'Plain text' });
    expect(toggle).toHaveAttribute('aria-pressed', 'false');
    await user.click(toggle);
    const plain = await screen.findByLabelText('Receipt as plain text');
    expect(plain.tagName).toBe('PRE');
    expect(plain.textContent).toMatch(/Change/);
    expect(screen.queryByRole('table', { name: 'Items sold' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Show formatted' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('an unknown sale says the receipt was not found', async () => {
    renderWithClient(<ReceiptPage orderId="f0000000-0000-0000-0000-00000000dead" />);
    expect(await screen.findByText('Receipt not found')).toBeInTheDocument();
  });
});

describe('Unsaved basket - U-X5', () => {
  it('an empty basket does not block leaving; a basket with an item asks the browser to confirm', async () => {
    const user = userEvent.setup();
    renderWithClient(<PosScreen />);
    const leave = () => {
      const event = new Event('beforeunload', { cancelable: true });
      window.dispatchEvent(event);
      return event.defaultPrevented;
    };
    expect(leave()).toBe(false);
    await scan(user, WHISKAS);
    await screen.findByRole('list', { name: 'Basket' });
    await waitFor(() => expect(leave()).toBe(true));
  });

  it('voiding the basket clears it and stops guarding', async () => {
    const user = userEvent.setup();
    renderWithClient(<PosScreen />);
    await scan(user, WHISKAS);
    await screen.findByRole('list', { name: 'Basket' });
    await user.click(screen.getByRole('button', { name: 'Void basket' }));
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent('Nothing is recorded.');
    await user.click(within(dialog).getByRole('button', { name: 'Void basket' }));
    await waitFor(() => expect(screen.queryByRole('list', { name: 'Basket' })).not.toBeInTheDocument());
    const event = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(event);
    expect(event.defaultPrevented).toBe(false);
  });
});


describe('Scanning faster than the lookups - gate fix', () => {
  function slowLookups(ms: number) {
    server.use(
      http.get('*/api/staff/pos/sku', async () => {
        await delay(ms);
        return undefined; // then the mock answers
      }),
    );
  }

  it('two different scans before the first lookup returns are BOTH added', async () => {
    slowLookups(150);
    const user = userEvent.setup();
    renderWithClient(<PosScreen />);
    const field = screen.getByLabelText('Scan a barcode');
    await user.click(field);
    await user.type(field, `${WHISKAS}{Enter}${KITTEN_400}{Enter}`);
    const basket = await screen.findByRole('list', { name: 'Basket' });
    await waitFor(() => expect(within(basket).getAllByRole('listitem')).toHaveLength(2));
    expect(within(basket).getByText(/Whiskas/)).toBeInTheDocument();
    expect(within(basket).getByText(/Royal Canin Kitten/)).toBeInTheDocument();
    expect(field).toHaveValue('');
  });

  it('the same item scanned twice quickly ends at quantity 2, not 1', async () => {
    slowLookups(100);
    const user = userEvent.setup();
    renderWithClient(<PosScreen />);
    const field = screen.getByLabelText('Scan a barcode');
    await user.click(field);
    await user.type(field, `${WHISKAS}{Enter}${WHISKAS}{Enter}`);
    const qty = await screen.findByLabelText(/Quantity of Whiskas/);
    await waitFor(() => expect(qty).toHaveValue(2));
  });

  it('an unknown code is put back in the field for correction', async () => {
    const user = userEvent.setup();
    renderWithClient(<PosScreen />);
    await scan(user, '1234567890123');
    await waitFor(() => expect(screen.getByLabelText('Scan a barcode')).toHaveValue('1234567890123'));
    expect(await screen.findByText('No product has barcode 1234567890123. Check the number or search by name.')).toBeInTheDocument();
  });
});

describe('The quantity box - gate fix', () => {
  it('can be cleared and retyped without snapping back to 1', async () => {
    const user = userEvent.setup();
    renderWithClient(<PosScreen />);
    await scan(user, KITTEN_400);
    const qty = await screen.findByLabelText(/Quantity of Royal Canin Kitten/);
    await user.click(qty);
    await user.clear(qty);
    expect(qty).toHaveValue(null);
    await user.type(qty, '3');
    expect(qty).toHaveValue(3);
    await user.tab();
    expect(qty).toHaveValue(3);
    await waitFor(() => expect(document.body.textContent).toMatch(/1,?950\.00/));
  });

  it('an empty entry on blur keeps the old quantity', async () => {
    const user = userEvent.setup();
    renderWithClient(<PosScreen />);
    await scan(user, KITTEN_400);
    const qty = await screen.findByLabelText(/Quantity of Royal Canin Kitten/);
    await user.click(qty);
    await user.clear(qty);
    await user.tab();
    expect(qty).toHaveValue(1);
  });
});

describe('Review gating and keyboard path - gate fix', () => {
  it('Enter in the amount field does NOT open the review without a customer, and says what is missing', async () => {
    const user = userEvent.setup();
    renderWithClient(<PosScreen />);
    await scan(user, WHISKAS);
    await screen.findByRole('list', { name: 'Basket' });
    await user.keyboard('{F2}');
    expect(screen.getByLabelText(/handed over/)).toHaveFocus();
    await user.keyboard('1000{Enter}');
    expect(await screen.findByText(/Enter the customer.s name and mobile/)).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.getByRole('list', { name: 'Payments taken' })).toBeInTheDocument();
  });

  it('with everything in place F2, the amount, Enter opens the review with Complete sale focused and the settled total', async () => {
    const user = userEvent.setup();
    renderWithClient(<PosScreen />);
    await scan(user, WHISKAS);
    await screen.findByRole('list', { name: 'Basket' });
    await fillCustomer(user);
    await waitFor(() => expect(document.body.textContent).toMatch(/780\.00/));
    await user.keyboard('{F2}1000{Enter}');
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByRole('button', { name: 'Complete sale' })).toHaveFocus();
    expect(dialog).toHaveTextContent(/780\.00/);
  });

  it('a prescription line without a cat blocks the review', async () => {
    const user = userEvent.setup();
    renderWithClient(<PosScreen />);
    await scan(user, '8900000000601');
    await screen.findByRole('list', { name: 'Basket' });
    await fillCustomer(user);
    await user.keyboard('{F2}9999{Enter}');
    expect(await screen.findByText(/Choose a cat for the prescription item/)).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('a quote that says the sale cannot be made blocks the review and lists why', async () => {
    const zero = { amount: '0.00', currencyCode: 'BDT' };
    server.use(
      http.post('*/api/staff/pos/quote', () =>
        HttpResponse.json({
          lines: [],
          excludedLines: [],
          changes: [],
          discount: null,
          totals: { subtotal: zero, lineDiscountTotal: zero, orderDiscount: zero, deliveryCharge: zero, taxTotal: zero, grandTotal: zero },
          canSell: false,
          blockingReasons: ['NothingToPlace'],
          currencyCode: 'BDT',
        }),
      ),
    );
    const user = userEvent.setup();
    renderWithClient(<PosScreen />);
    await scan(user, WHISKAS);
    await screen.findByRole('list', { name: 'Basket' });
    await fillCustomer(user);
    const reasons = await screen.findByRole('list', { name: /Why this sale/ });
    expect(reasons).toHaveTextContent(/nothing left/i);
    await user.keyboard('{F2}1000{Enter}');
    await waitFor(() => expect(screen.getByRole('list', { name: 'Payments taken' })).toBeInTheDocument());
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Complete sale' })).toBeDisabled();
  });

  it('a basket of two items shows the server total of both', async () => {
    const user = userEvent.setup();
    renderWithClient(<PosScreen />);
    await scan(user, WHISKAS);
    await scan(user, KITTEN_400);
    const summary = await screen.findByRole('region', { name: 'Sale total' });
    await waitFor(() => expect(within(summary).getAllByRole('definition').at(-1)).toHaveTextContent(/1,?430\.00/));
  });
});

describe('Completing the sale sends one request, one key - gate fix', () => {
  function recordSales() {
    const keys: Array<string | null> = [];
    server.events.on('request:start', ({ request }) => {
      if (request.method === 'POST' && request.url.endsWith('/api/staff/pos/sales')) keys.push(request.headers.get('Idempotency-Key'));
    });
    return keys;
  }

  it('a double click on Complete sale in the review records one sale', async () => {
    const keys = recordSales();
    const user = userEvent.setup();
    renderWithClient(<PosScreen />);
    await scan(user, WHISKAS);
    await screen.findByRole('list', { name: 'Basket' });
    await fillCustomer(user);
    await takePayment(user, 'Cash', '1000');
    const button = screen.getByRole('button', { name: 'Complete sale' });
    await waitFor(() => expect(button).toBeEnabled());
    await user.click(button);
    const confirm = within(await screen.findByRole('dialog')).getByRole('button', { name: 'Complete sale' });
    await user.dblClick(confirm);
    await screen.findByRole('heading', { name: /Sale complete/ });
    expect(keys).toHaveLength(1);
    expect(keys[0]).toMatch(/[0-9a-f-]{36}/);
    server.events.removeAllListeners();
  });

  it('after a refusal the retry of the same sale carries a NEW key (the API replays a refused key)', async () => {
    const keys = recordSales();
    const user = userEvent.setup();
    renderWithClient(<PosScreen />);
    await scan(user, WHISKAS);
    await screen.findByRole('list', { name: 'Basket' });
    await fillCustomer(user);
    await takePayment(user, 'Cash', '100'); // does not cover the total: refused
    await completeSale(user);
    await screen.findByText('The payments do not cover the total of the sale.');
    await completeSale(user);
    await waitFor(() => expect(keys).toHaveLength(2));
    expect(keys[1]).not.toBe(keys[0]);
    server.events.removeAllListeners();
  });
});

describe('POS cats of the attached customer - gate fix B4', () => {
  it('the ordering-owned override answers for the accounts customer and lets every other owner through to cat-profiles', async () => {
    const own = await fetch(`http://localhost/api/cats?ownerId=${ids.customerId}&pageSize=50`).then((r) => r.json());
    expect(own.items.map((c: { name: string }) => c.name)).toEqual(['Whiskers', 'Mittens']);
    const other = await fetch('http://localhost/api/cats?ownerId=10000000-0000-0000-0000-000000000001&pageSize=50').then((r) => r.json());
    expect(other.items.length).toBeGreaterThan(0);
    expect(other.items.every((c: { ownerId: string }) => c.ownerId === '10000000-0000-0000-0000-000000000001')).toBe(true);
  });
});
