import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { server } from '../../../mocks/server';
import { violation } from '../../../mocks/handlers/ordering/fixtures';
import { CartScreen } from '@/components/cart/CartScreen';
import { AddToCartButton } from '@/components/cart/AddToCartButton';
import { PrescriptionCatDialog } from '@/components/cart/PrescriptionCatDialog';
import type { Cart, CartLine } from '@/lib/api/cart';

vi.mock('next/navigation', () => import('../booking/nav-mock'));

function renderWithClient(ui: ReactElement) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: 0 }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const money = (amount: string) => ({ amount, currencyCode: 'BDT' });

function line(overrides: Partial<CartLine> = {}): CartLine {
  return {
    lineId: 'l1',
    skuId: 's1',
    productId: 'p1',
    productName: 'Royal Canin Kitten',
    variantLabel: '2 kg',
    imagePath: null,
    quantity: 2,
    cat: null,
    unitPrice: money('2450.00'),
    priceWhenAdded: money('2450.00'),
    priceChanged: false,
    availability: 'InStock',
    availableQuantity: null,
    requiresPrescription: false,
    prescriptionStatus: null,
    lineSubtotal: money('4900.00'),
    ...overrides,
  };
}

function serveCart(lines: CartLine[], extra: Partial<Cart> = {}) {
  const cart: Cart = {
    cartId: 'c1',
    lines,
    itemCount: lines.reduce((n, l) => n + l.quantity, 0),
    subtotal: money('4900.00'),
    maxQuantityPerLine: 20,
    guestCartPending: false,
    ...extra,
  };
  server.use(http.get('*/api/cart', () => HttpResponse.json(cart)));
}

describe('CartScreen lines - HO-ORD-002 U-A1', () => {
  it('states a changed price, a limited line and an unavailable line as text; the unavailable one blocks Checkout and offers removal', async () => {
    serveCart([
      line({
        lineId: 'a',
        productName: 'Whiskas',
        variantLabel: '1 kg',
        priceChanged: true,
        unitPrice: money('800.00'),
        priceWhenAdded: money('780.00'),
      }),
      line({
        lineId: 'b',
        productName: 'Meow Mix',
        variantLabel: '1 kg',
        availability: 'Limited',
        availableQuantity: 3,
      }),
      line({
        lineId: 'c',
        productName: 'Hills k/d',
        variantLabel: '1.5 kg',
        availability: 'Unavailable',
        lineSubtotal: null,
      }),
    ]);
    renderWithClient(<CartScreen />);
    expect(await screen.findByText(/Price is now .*800\.00 \(was .*780\.00\)/)).toBeInTheDocument();
    expect(screen.getByText('Only 3 left')).toBeInTheDocument();
    expect(screen.getByText('Unavailable')).toBeInTheDocument();
    expect(screen.getByText(/This item can.t be ordered right now/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /check ?out/i })).toBeDisabled();
    expect(screen.queryByRole('link', { name: /check ?out/i })).not.toBeInTheDocument();
    expect(screen.getByText('Disabled: Hills k/d is unavailable.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Remove Hills k/d 1.5 kg from cart' })).toBeEnabled();
  });

  it('offers Checkout as a link once nothing is unavailable', async () => {
    serveCart([line()]);
    renderWithClient(<CartScreen />);
    const link = await screen.findByRole('link', { name: /check ?out/i });
    expect(link.getAttribute('href')).toMatch(/^\/(checkout|sign-in\?next=%2Fcheckout)$/);
  });

  it('shows the empty state, and an error with a retry', async () => {
    serveCart([], { itemCount: 0, subtotal: money('0.00') });
    const { unmount } = renderWithClient(<CartScreen />);
    expect(await screen.findByText('Your cart is empty.')).toBeInTheDocument();
    unmount();
    server.use(http.get('*/api/cart', () => HttpResponse.json({ title: 'x' }, { status: 500 })));
    renderWithClient(<CartScreen />);
    expect(await screen.findByText("We couldn't load your cart.")).toBeInTheDocument();
  });
});

describe('CartScreen stepper - U-A2', () => {
  it('stops at the quantity in stock and says why', async () => {
    serveCart([line({ quantity: 3, availability: 'Limited', availableQuantity: 3 })]);
    renderWithClient(<CartScreen />);
    const plus = await screen.findByRole('button', {
      name: 'Increase quantity of Royal Canin Kitten 2 kg',
    });
    expect(plus).toBeDisabled();
    expect(screen.getByText("You can't add more — only 3 are in stock.")).toBeInTheDocument();
  });

  it('below the limit, + is enabled and sends the next quantity', async () => {
    let body: unknown;
    server.use(
      http.put('*/api/cart/lines/l1', async ({ request }) => {
        body = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    serveCart([line({ quantity: 4 })], { maxQuantityPerLine: 5 });
    const user = userEvent.setup();
    renderWithClient(<CartScreen />);
    const plus = await screen.findByRole('button', {
      name: 'Increase quantity of Royal Canin Kitten 2 kg',
    });
    expect(plus).toBeEnabled();
    await user.click(plus);
    await waitFor(() => expect(body).toEqual({ quantity: 5 }));
  });

  it('at maxQuantityPerLine, + is disabled with its own message', async () => {
    serveCart([line({ quantity: 5 })], { maxQuantityPerLine: 5 });
    renderWithClient(<CartScreen />);
    expect(
      await screen.findByText('You can buy at most 5 of an item at a time.'),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Increase quantity of Royal Canin Kitten 2 kg' }),
    ).toBeDisabled();
  });

  it('a refused change shows a sentence, never the error code', async () => {
    server.use(
      http.put('*/api/cart/lines/l1', () =>
        violation('quantity', 'QuantityOutOfRange', 'Choose between 1 and 20.'),
      ),
    );
    serveCart([line({ quantity: 2 })]);
    const user = userEvent.setup();
    renderWithClient(<CartScreen />);
    await user.click(
      await screen.findByRole('button', { name: 'Increase quantity of Royal Canin Kitten 2 kg' }),
    );
    const alert = await screen.findByRole('alert');
    expect(alert.textContent).not.toMatch(/QuantityOutOfRange/);
    expect((alert.textContent ?? '').length).toBeGreaterThan(5);
  });
});

describe('Prescription add to cart - U-A4', () => {
  it('signed out: replaces the button with a sign-in link', () => {
    renderWithClient(
      <AddToCartButton skuId="s" productName="Hills k/d" requiresPrescription signedIn={false} />,
    );
    expect(screen.getByRole('link', { name: 'Sign in to buy prescription food' })).toHaveAttribute(
      'href',
      '/sign-in',
    );
    expect(screen.queryByRole('button', { name: 'Add to cart' })).not.toBeInTheDocument();
  });

  it('the cat dialog disables an unauthorised cat and links to booking a consultation', async () => {
    const onChoose = vi.fn();
    render(
      <PrescriptionCatDialog
        open
        onOpenChange={() => undefined}
        productName="Hills k/d"
        cats={[
          { id: 'cat-a', name: 'Whiskers', authorised: true },
          { id: 'cat-b', name: 'Mittens', authorised: false },
        ]}
        isLoading={false}
        isError={false}
        pending={false}
        error={null}
        onChoose={onChoose}
      />,
    );
    const dialog = screen.getByRole('dialog', { name: 'Which cat is this for?' });
    expect(within(dialog).getByRole('radio', { name: 'Mittens' })).toBeDisabled();
    expect(within(dialog).getByRole('radio', { name: 'Whiskers' })).toBeEnabled();
    expect(within(dialog).getByText(/No current authorisation for this food/)).toBeInTheDocument();
    expect(within(dialog).getByRole('link', { name: 'Book a consultation' })).toHaveAttribute(
      'href',
      '/book?catId=cat-b&category=Consultation',
    );
    const user = userEvent.setup();
    await user.click(within(dialog).getByRole('radio', { name: 'Whiskers' }));
    const confirm = within(dialog)
      .getAllByRole('button')
      .find((b) => /add to cart/i.test(b.textContent ?? ''));
    expect(confirm).toBeDefined();
    await user.click(confirm!);
    expect(onChoose).toHaveBeenCalledWith('cat-a');
  });

  it('signed in: Add to cart opens the dialog instead of posting', async () => {
    let posted = 0;
    server.use(
      http.post('*/api/cart/lines', () => {
        posted += 1;
        return HttpResponse.json({ lineId: 'x', cartId: 'y' });
      }),
    );
    const user = userEvent.setup();
    renderWithClient(
      <AddToCartButton
        skuId="s"
        productName="Hills k/d"
        requiresPrescription
        signedIn
        eligibleCatIds={[]}
      />,
    );
    await user.click(screen.getByRole('button', { name: 'Add to cart' }));
    expect(
      await screen.findByRole('dialog', { name: 'Which cat is this for?' }),
    ).toBeInTheDocument();
    expect(posted).toBe(0);
  });
});
