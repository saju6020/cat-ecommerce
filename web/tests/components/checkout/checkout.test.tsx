import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { server } from '../../../mocks/server';
import { violation } from '../../../mocks/handlers/ordering/fixtures';
import { CheckoutScreen } from '@/components/checkout/CheckoutScreen';
import { ChangesPanel } from '@/components/checkout/ChangesPanel';
import { DiscountCodeField } from '@/components/checkout/DiscountCodeField';
import { PaymentStep } from '@/components/checkout/PaymentStep';
import { ReviewStep } from '@/components/checkout/ReviewStep';
import { SlotStep } from '@/components/checkout/SlotStep';
import { OrderSummary } from '@/components/orders/OrderSummary';
import type { CheckoutChange, CheckoutQuote, QuoteServiceLevel } from '@/lib/api/checkout';
import type { Cart } from '@/lib/api/cart';
import { pushed, setCheckoutUrl } from './nav';

vi.mock('next/navigation', () => import('./nav'));

const money = (amount: string) => ({ amount, currencyCode: 'BDT' });

function renderWithClient(ui: ReactElement) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: 0 }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const TOTALS = {
  subtotal: money('4900.00'),
  lineDiscountTotal: money('0.00'),
  orderDiscount: money('0.00'),
  deliveryCharge: money('0.00'),
  taxTotal: money('0.00'),
  grandTotal: money('4900.00'),
};

function quote(overrides: Partial<CheckoutQuote> = {}): CheckoutQuote {
  return {
    lines: [
      {
        lineRef: 'q1',
        skuId: 's1',
        productName: 'Royal Canin Kitten',
        variantLabel: '2 kg',
        cat: null,
        quantity: 2,
        unitPrice: money('2450.00'),
        lineSubtotal: money('4900.00'),
        lineDiscount: money('0.00'),
        orderDiscountShare: money('0.00'),
        taxRatePercent: 0,
        taxAmount: money('0.00'),
        lineTotal: money('4900.00'),
      },
    ],
    excludedLines: [],
    changes: [],
    delivery: {
      available: true,
      unavailableReason: null,
      zoneId: 'z1',
      zoneName: 'Dhaka',
      serviceLevels: [],
    },
    selectedServiceLevel: null,
    pickupBranches: [{ id: 'br1', name: 'Main clinic', address: 'Road 1', phone: null }],
    paymentOptions: [
      { method: 'PayOnCollection', available: true, unavailableReason: null, message: null },
      { method: 'BankTransfer', available: true, unavailableReason: null, message: null },
    ],
    discount: null,
    totals: TOTALS,
    pendingPolicies: [],
    canPlace: true,
    blockingReasons: [],
    fingerprint: 'F1',
    currencyCode: 'BDT',
    ...overrides,
  };
}

const CART: Cart = {
  cartId: 'c1',
  lines: [
    {
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
    },
  ],
  itemCount: 2,
  subtotal: money('4900.00'),
  maxQuantityPerLine: 20,
  guestCartPending: false,
};

/** Everything the checkout screen reads, with the quote supplied by the test. */
function serveCheckout(next: (url: URL) => CheckoutQuote) {
  const quoteUrls: URL[] = [];
  server.use(
    http.get('*/api/cart', () => HttpResponse.json(CART)),
    http.get('*/api/customers/me', () =>
      HttpResponse.json({
        fullName: 'Sam Rahman',
        mobileNumber: '01711111111',
        emailAddress: null,
        hasMobileNumber: true,
        hasEmailAddress: false,
        canEditEmail: false,
        additionalPhoneNumbers: [],
        registeredOn: '2026-01-01',
      }),
    ),
    http.get('*/api/customers/me/addresses', () =>
      HttpResponse.json({ addresses: [], maxAddresses: 5 }),
    ),
    http.get('*/api/cats', () =>
      HttpResponse.json({ items: [], pageNumber: 1, pageSize: 50, totalCount: 0, totalPages: 0 }),
    ),
    http.get('*/api/customers/me/checkout/quote', ({ request }) => {
      const url = new URL(request.url);
      quoteUrls.push(url);
      return HttpResponse.json(next(url));
    }),
  );
  return quoteUrls;
}

beforeEach(() => {
  pushed.length = 0;
  setCheckoutUrl('/checkout');
});

const REVIEW_URL =
  '/checkout?step=review&fulfilment=Pickup&pickupBranchId=br1&paymentMethod=PayOnCollection';

describe('SlotStep - U-B2', () => {
  const standard: QuoteServiceLevel = {
    level: 'Standard',
    charge: money('60.00'),
    freeOver: null,
    amountToFreeDelivery: null,
    leadTimeDays: 2,
    slots: [],
  };
  const express: QuoteServiceLevel = {
    level: 'Express',
    charge: money('150.00'),
    freeOver: null,
    amountToFreeDelivery: null,
    leadTimeDays: 0,
    slots: [
      { date: '2026-10-14', windowStart: '10:00', windowEnd: '12:00', label: '10:00-12:00' },
      { date: '2026-10-14', windowStart: '14:00', windowEnd: '16:00', label: '14:00-16:00' },
    ],
  };

  it('shows each level with the server charge and lead time, and the windows of the chosen level', async () => {
    const onSlot = vi.fn();
    render(
      <SlotStep
        levels={[standard, express]}
        selectedLevel="Express"
        onLevel={() => undefined}
        slotDate={undefined}
        slotWindowStart={undefined}
        onSlot={onSlot}
        zone="Asia/Dhaka"
        onContinue={() => undefined}
        continueDisabledReason="Choose a delivery time to continue."
      />,
    );
    expect(screen.getByText(/Delivery in about 2 days/)).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: /Standard/ }).closest('label')).toHaveTextContent(
      /60\.00/,
    );
    expect(screen.getByRole('radio', { name: /Express/ }).closest('label')).toHaveTextContent(
      /150\.00/,
    );
    const windows = screen.getByRole('radiogroup', { name: 'Time window' });
    expect(within(windows).getByRole('radio', { name: '10:00–12:00' })).toBeInTheDocument();
    expect(within(windows).getByRole('radio', { name: '14:00–16:00' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Continue' })).toBeDisabled();
    expect(screen.getByText('Choose a delivery time to continue.')).toBeInTheDocument();
    await userEvent.setup().click(within(windows).getByRole('radio', { name: '14:00–16:00' }));
    expect(onSlot).toHaveBeenCalledWith('2026-10-14', '14:00');
  });

  it('a level without windows asks for no time choice', () => {
    render(
      <SlotStep
        levels={[standard]}
        selectedLevel="Standard"
        onLevel={() => undefined}
        slotDate={undefined}
        slotWindowStart={undefined}
        onSlot={() => undefined}
        zone="Asia/Dhaka"
        onContinue={() => undefined}
        continueDisabledReason={null}
      />,
    );
    expect(screen.queryByRole('radiogroup', { name: 'Time window' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Continue' })).toBeEnabled();
  });

  it('on the checkout screen the slot step is marked not needed when there are no windows', async () => {
    serveCheckout(() =>
      quote({
        delivery: {
          available: true,
          unavailableReason: null,
          zoneId: 'z1',
          zoneName: 'Dhaka',
          serviceLevels: [standard],
        },
        selectedServiceLevel: 'Standard',
      }),
    );
    server.use(
      http.get('*/api/customers/me/addresses', () =>
        HttpResponse.json({
          addresses: [
            {
              id: 'a1',
              label: 'Home',
              line1: '1 Road',
              line2: null,
              city: 'Dhaka',
              district: 'Dhaka',
              postalCode: null,
              deliveryInstructions: null,
              isDefault: true,
            },
          ],
          maxAddresses: 5,
        }),
      ),
    );
    setCheckoutUrl('/checkout?step=payment&fulfilment=Delivery&addressId=a1');
    renderWithClient(<CheckoutScreen />);
    const steps = await screen.findByRole('list', { name: 'Checkout steps' });
    // The slot step is skipped: its pill says why and carries the server's lead time.
    const skipped = await within(steps).findByText(/Standard.*Delivery in about 2 days/);
    expect(skipped.closest('li')).toHaveAttribute('data-status', 'skipped');
    expect(skipped.closest('li')).toHaveTextContent('Delivery time');
  });
});

describe('Checkout has no card input - U-B4', () => {
  it('the payment step offers only the quote methods, and the review DOM has no card field or online-payment wording', async () => {
    serveCheckout(() => quote());
    setCheckoutUrl(REVIEW_URL);
    const { container } = renderWithClient(<CheckoutScreen />);
    await screen.findByRole('button', { name: /^Place order/ });
    expect(container.querySelector('input[autocomplete^="cc-"]')).toBeNull();
    expect(container.querySelector('input[name*="card" i]')).toBeNull();
    expect(container.textContent ?? '').not.toMatch(
      /pay online|credit card|debit card|card number|cvv/i,
    );
  });

  it('PaymentStep renders exactly the options it is given, disabled ones with the server message', () => {
    render(
      <PaymentStep
        options={[
          {
            method: 'CashOnDelivery',
            available: false,
            unavailableReason: 'AboveCeiling',
            message: 'Cash on delivery is available for orders up to BDT 5,000.00',
          },
          { method: 'BankTransfer', available: true, unavailableReason: null, message: null },
        ]}
        selected={undefined}
        onSelect={() => undefined}
        onContinue={() => undefined}
        continueDisabledReason="Choose how you will pay to continue."
      />,
    );
    expect(screen.getAllByRole('radio')).toHaveLength(2);
    expect(screen.getByRole('radio', { name: /Cash on delivery/ })).toBeDisabled();
    expect(
      screen.getByText('Cash on delivery is available for orders up to BDT 5,000.00'),
    ).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: /Bank transfer/ })).toBeEnabled();
  });
});

describe('ChangesPanel acknowledgement - U-B6', () => {
  const change: CheckoutChange = {
    kind: 'PriceIncreased',
    lineRef: 'q1',
    productName: 'Royal Canin Kitten',
    before: '2400.00',
    after: '2450.00',
    message: 'Royal Canin Kitten 2 kg now costs 2,450.00 (was 2,400.00).',
  };

  function Harness({ fingerprint }: { fingerprint: string }) {
    const [ack, setAck] = useState<string | null>(null);
    return (
      <ChangesPanel
        changes={[change]}
        fingerprint={fingerprint}
        acknowledgedFingerprint={ack}
        onAcknowledge={setAck}
        bookHref="/book"
        settling={false}
      />
    );
  }

  it('a new fingerprint clears the tick and announces it', async () => {
    const user = userEvent.setup();
    const { rerender } = render(<Harness fingerprint="F1" />);
    expect(screen.getByText(change.message)).toBeInTheDocument();
    const box = screen.getByRole('checkbox', { name: /reviewed these changes/ });
    await user.click(box);
    expect(box).toBeChecked();
    rerender(<Harness fingerprint="F2" />);
    await waitFor(() =>
      expect(screen.getByRole('checkbox', { name: /reviewed these changes/ })).not.toBeChecked(),
    );
    expect(screen.getByText('The order has changed — please review again.')).toBeInTheDocument();
  });

  it('the tick cannot be changed while the quote is settling', () => {
    render(
      <ChangesPanel
        changes={[change]}
        fingerprint="F1"
        acknowledgedFingerprint={null}
        onAcknowledge={() => undefined}
        bookHref="/book"
        settling
      />,
    );
    expect(screen.getByRole('checkbox', { name: /reviewed these changes/ })).toBeDisabled();
  });

  it('Place order stays disabled, with the reason listed, until the changes are acknowledged', () => {
    const base = {
      quote: quote({ changes: [change] }),
      fulfilment: 'Pickup',
      settling: false,
      rxPanel: null,
      changesPanel: null,
      policiesPanel: null,
      discountField: null,
      needsAcknowledgement: true,
      placing: false,
      inProgress: false,
      placeError: null,
      onPlace: () => undefined,
    };
    const { rerender } = render(<ReviewStep {...base} acknowledged={false} />);
    const button = screen.getByRole('button', { name: /^Place order/ });
    expect(button).toBeDisabled();
    expect(screen.getByText(/Tick .I.ve reviewed these changes\./)).toBeInTheDocument();
    rerender(<ReviewStep {...base} acknowledged />);
    expect(screen.getByRole('button', { name: /^Place order/ })).toBeEnabled();
  });

  it('Issues.md #9: when every line is left out, it says so instead of a summary of 0.00s', () => {
    render(
      <ReviewStep
        quote={quote({ lines: [], canPlace: false })}
        fulfilment="Pickup"
        settling={false}
        rxPanel={null}
        changesPanel={null}
        policiesPanel={null}
        discountField={<p>discount field</p>}
        needsAcknowledgement={false}
        acknowledged={false}
        placing={false}
        inProgress={false}
        placeError={null}
        onPlace={() => undefined}
      />,
    );
    expect(
      screen.getByText(/None of the items in your cart can be ordered right now/),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back to your cart' })).toHaveAttribute(
      'href',
      '/cart',
    );
    expect(screen.queryByRole('region', { name: 'Order summary' })).not.toBeInTheDocument();
    expect(screen.queryByText('discount field')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^Place order/ })).toBeDisabled();
  });
});

describe('OrderSummary - U-B7', () => {
  it('always shows the six rows, zero ones included, with the server strings', () => {
    render(<OrderSummary totals={TOTALS} fulfilment="Pickup" />);
    const rows = screen.getAllByRole('term').map((t) => t.textContent);
    expect(rows).toEqual([
      'Items subtotal',
      'Item discounts',
      'Order discount',
      'Delivery',
      'Tax',
      'Total',
    ]);
    const definitions = screen.getAllByRole('definition').map((d) => d.textContent ?? '');
    expect(definitions).toHaveLength(6);
    expect(definitions[1]).toMatch(/0\.00/);
    expect(definitions[0]).toMatch(/4,?900\.00/);
    expect(definitions[5]).toMatch(/4,?900\.00/);
  });

  it('names the code on the order-discount row and the level on the delivery row', () => {
    render(
      <OrderSummary
        totals={{ ...TOTALS, orderDiscount: money('100.00'), deliveryCharge: money('60.00') }}
        discountCode="WELCOME"
        serviceLevel="Standard"
        fulfilment="Delivery"
      />,
    );
    expect(screen.getByText('Order discount (WELCOME)')).toBeInTheDocument();
    expect(screen.getByText(/^Delivery \(Standard\)$/)).toBeInTheDocument();
  });
});

describe('DiscountCodeField minimum spend - U-B8', () => {
  it('shows the server reason inline for a rejected code and leaves the totals as the quote gave them', () => {
    const rejected = {
      code: 'BIG',
      applied: false,
      reasonCode: 'DiscountMinimumSpendNotMet',
      message: 'Spend BDT 6,000.00 or more to use BIG.',
    };
    render(
      <>
        <DiscountCodeField
          appliedCode="BIG"
          discount={rejected}
          onApply={() => undefined}
          onRemove={() => undefined}
          checking={false}
        />
        <OrderSummary totals={TOTALS} fulfilment="Pickup" />
      </>,
    );
    expect(screen.getByRole('alert')).toHaveTextContent('Spend BDT 6,000.00 or more to use BIG.');
    expect(screen.queryByText('Applied')).not.toBeInTheDocument();
    const total = screen.getAllByRole('definition').at(-1)!;
    expect(total).toHaveTextContent(/4,?900\.00/);
    expect(screen.getByText('Order discount')).toBeInTheDocument();
  });

  it('upper-cases the typed code and applies it on submit', async () => {
    const onApply = vi.fn();
    const user = userEvent.setup();
    render(
      <DiscountCodeField
        appliedCode={undefined}
        discount={null}
        onApply={onApply}
        onRemove={() => undefined}
        checking={false}
      />,
    );
    await user.type(screen.getByLabelText('Discount code'), 'welcome');
    await user.click(screen.getByRole('button', { name: 'Apply' }));
    expect(onApply).toHaveBeenCalledWith('WELCOME');
  });
});

describe('Checkout choices live in the URL - U-B10', () => {
  it('a reload on the review URL restores every choice: the quote is asked with them and the review step is open', async () => {
    const urls = serveCheckout(() =>
      quote({ discount: { code: 'WELCOME', applied: true, reasonCode: null, message: null } }),
    );
    setCheckoutUrl(`${REVIEW_URL}&discountCode=WELCOME`);
    renderWithClient(<CheckoutScreen />);
    expect(await screen.findByRole('button', { name: /^Place order/ })).toBeInTheDocument();
    const asked = urls.at(-1)!.searchParams;
    expect(asked.get('fulfilment')).toBe('Pickup');
    expect(asked.get('pickupBranchId')).toBe('br1');
    expect(asked.get('paymentMethod')).toBe('PayOnCollection');
    expect(asked.get('discountCode')).toBe('WELCOME');
    expect(screen.getByText('WELCOME')).toBeInTheDocument();
    expect(
      within(screen.getByRole('region', { name: 'Collection' })).getByText(
        /Collect from Main clinic/,
      ),
    ).toBeInTheDocument();
    expect(
      within(screen.getByRole('region', { name: 'Payment' })).getByText('Pay when you collect'),
    ).toBeInTheDocument();
  });
});

describe('Placing an order - U-B11, U-B13', () => {
  it('a 409 CheckoutQuoteChanged re-quotes, re-opens the acknowledgement and shows one message', async () => {
    let quotes = 0;
    const urls = serveCheckout(() => {
      quotes += 1;
      return quotes === 1
        ? quote()
        : quote({
            fingerprint: 'F2',
            changes: [
              {
                kind: 'PriceIncreased',
                lineRef: 'q1',
                productName: 'Royal Canin Kitten',
                before: '2400.00',
                after: '2450.00',
                message: 'Royal Canin Kitten 2 kg now costs 2,450.00.',
              },
            ],
          });
    });
    server.use(
      http.post('*/api/customers/me/orders', () =>
        violation(
          'quoteFingerprint',
          'CheckoutQuoteChanged',
          'Something changed while you were checking out.',
          409,
        ),
      ),
    );
    setCheckoutUrl(REVIEW_URL);
    const user = userEvent.setup();
    renderWithClient(<CheckoutScreen />);
    await user.click(await screen.findByRole('button', { name: /^Place order/ }));
    await waitFor(() => expect(urls.length).toBeGreaterThan(1));
    expect(
      await screen.findAllByText('Something changed while you were checking out.'),
    ).toHaveLength(1);
    expect(
      await screen.findByText('Royal Canin Kitten 2 kg now costs 2,450.00.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: /reviewed these changes/ })).not.toBeChecked();
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /^Place order/ })).toBeDisabled(),
    );
    expect(pushed).toEqual([]);
  });

  it('a 409 RequestInProgress shows its message, disables Place order and sends nothing more', async () => {
    let posts = 0;
    serveCheckout(() => quote());
    server.use(
      http.post('*/api/customers/me/orders', () => {
        posts += 1;
        return violation(
          'Idempotency-Key',
          'RequestInProgress',
          'This order is already being placed.',
          409,
        );
      }),
      http.get('*/api/customers/me/orders', () =>
        HttpResponse.json({ items: [], pageNumber: 1, pageSize: 10, totalCount: 0, totalPages: 0 }),
      ),
    );
    setCheckoutUrl(REVIEW_URL);
    const user = userEvent.setup();
    renderWithClient(<CheckoutScreen />);
    await user.click(await screen.findByRole('button', { name: /^Place order/ }));
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'This order is already being placed.',
    );
    const button = screen.getByRole('button', { name: /^Place order/ });
    expect(button).toBeDisabled();
    await user.click(button);
    expect(posts).toBe(1);
    expect(pushed).toEqual([]);
  });

  it('a successful placement goes to the completion page once', async () => {
    serveCheckout(() => quote());
    server.use(
      http.post('*/api/customers/me/orders', () =>
        HttpResponse.json({
          orderId: 'o1',
          orderNumber: 'ORD-1',
          status: 'Confirmed',
          grandTotal: money('4900.00'),
          paymentDueBy: null,
          transferInstructions: null,
        }),
      ),
    );
    setCheckoutUrl(REVIEW_URL);
    const user = userEvent.setup();
    renderWithClient(<CheckoutScreen />);
    await user.click(await screen.findByRole('button', { name: /^Place order/ }));
    await waitFor(() => expect(pushed).toEqual(['/portal/orders/o1?placed=1']));
  });
});

describe('Placing an order - gate fixes', () => {
  it('RequestInProgress is not a dead end: a way to My orders, then Place order again with the SAME key', async () => {
    const keys: Array<string | null> = [];
    serveCheckout(() => quote());
    server.use(
      http.post('*/api/customers/me/orders', ({ request }) => {
        keys.push(request.headers.get('Idempotency-Key'));
        return keys.length === 1
          ? violation(
              'Idempotency-Key',
              'RequestInProgress',
              'This order is already being placed.',
              409,
            )
          : HttpResponse.json({
              orderId: 'o9',
              orderNumber: 'ORD-9',
              status: 'Confirmed',
              grandTotal: money('4900.00'),
              paymentDueBy: null,
              transferInstructions: null,
            });
      }),
      http.get('*/api/customers/me/orders', () =>
        HttpResponse.json({ items: [], pageNumber: 1, pageSize: 10, totalCount: 0, totalPages: 0 }),
      ),
    );
    setCheckoutUrl(REVIEW_URL);
    const user = userEvent.setup();
    renderWithClient(<CheckoutScreen />);
    await user.click(await screen.findByRole('button', { name: /^Place order/ }));
    expect(await screen.findByRole('link', { name: 'Check my orders' })).toHaveAttribute(
      'href',
      '/portal/orders',
    );
    expect(screen.getByRole('button', { name: /^Place order/ })).toBeDisabled();
    // After the short wait the button comes back...
    await waitFor(
      () => expect(screen.getByRole('button', { name: /^Place order/ })).toBeEnabled(),
      { timeout: 8000 },
    );
    await user.click(screen.getByRole('button', { name: /^Place order/ }));
    await waitFor(() => expect(pushed).toEqual(['/portal/orders/o9?placed=1']));
    // ...and the retry reuses the key, so the API can replay the first attempt's result.
    expect(keys).toHaveLength(2);
    expect(keys[1]).toBe(keys[0]);
  }, 20_000);

  it('PolicyAcceptanceRequired re-reads the quote (so the policy to accept appears) and the retry uses a NEW key', async () => {
    const keys: Array<string | null> = [];
    let quotes = 0;
    serveCheckout(() => {
      quotes += 1;
      return quotes === 1
        ? quote()
        : quote({ pendingPolicies: [{ key: 'terms', versionNumber: 2, title: 'Terms of sale' }] });
    });
    server.use(
      http.post('*/api/customers/me/orders', ({ request }) => {
        keys.push(request.headers.get('Idempotency-Key'));
        return violation(
          'PolicyAcceptance',
          'PolicyAcceptanceRequired',
          'Please accept the updated policies.',
        );
      }),
    );
    setCheckoutUrl(REVIEW_URL);
    const user = userEvent.setup();
    renderWithClient(<CheckoutScreen />);
    await user.click(await screen.findByRole('button', { name: /^Place order/ }));
    expect(await screen.findByText('Terms of sale')).toBeInTheDocument();
    expect(quotes).toBeGreaterThan(1);
    await waitFor(() => expect(screen.getByRole('button', { name: /^Place order/ })).toBeEnabled());
    await user.click(screen.getByRole('button', { name: /^Place order/ }));
    await waitFor(() => expect(keys).toHaveLength(2));
    expect(keys[0]).toBeTruthy();
    expect(keys[1]).not.toBe(keys[0]);
  });

  it('labels a chosen delivery window in plain language in the branch zone, not as raw ISO text', async () => {
    const slots = [
      { date: '2026-10-14', windowStart: '10:00', windowEnd: '12:00', label: '10:00-12:00' },
    ];
    serveCheckout(() =>
      quote({
        delivery: {
          available: true,
          unavailableReason: null,
          zoneId: 'z1',
          zoneName: 'Dhaka',
          serviceLevels: [
            {
              level: 'Express',
              charge: money('150.00'),
              freeOver: null,
              amountToFreeDelivery: null,
              leadTimeDays: 0,
              slots,
            },
          ],
        },
        selectedServiceLevel: 'Express',
      }),
    );
    server.use(
      http.get('*/api/customers/me/addresses', () =>
        HttpResponse.json({
          addresses: [
            {
              id: 'a1',
              label: 'Home',
              line1: '1 Road',
              line2: null,
              city: 'Dhaka',
              district: 'Dhaka',
              postalCode: null,
              deliveryInstructions: null,
              isDefault: true,
            },
          ],
          maxAddresses: 5,
        }),
      ),
    );
    setCheckoutUrl(
      '/checkout?step=payment&fulfilment=Delivery&addressId=a1&serviceLevel=Express&slotDate=2026-10-14&slotWindowStart=10:00',
    );
    renderWithClient(<CheckoutScreen />);
    const steps = await screen.findByRole('list', { name: 'Checkout steps' });
    const summary = (await within(steps).findByText(/Express delivery/)).closest('li')!;
    expect(summary).toHaveAttribute('data-status', 'done');
    expect(summary).toHaveTextContent('Wed 14 Oct, 10:00–12:00 (Asia/Dhaka)');
    expect(summary).not.toHaveTextContent('2026-10-14');
  });
});

describe('Hearth checkout: greeting and the free-delivery card', () => {
  const address = {
    id: 'a1',
    label: 'Home',
    line1: '1 Road',
    line2: null,
    city: 'Dhaka',
    district: 'Dhaka',
    postalCode: null,
    deliveryInstructions: null,
    isDefault: true,
  };
  const withAmount = (
    amountToFreeDelivery: ReturnType<typeof money> | null,
  ): QuoteServiceLevel => ({
    level: 'Standard',
    charge: money('80.00'),
    freeOver: amountToFreeDelivery ? money('5000.00') : null,
    amountToFreeDelivery,
    leadTimeDays: 1,
    slots: [],
  });
  const DELIVERY_REVIEW =
    '/checkout?step=review&fulfilment=Delivery&addressId=a1&serviceLevel=Standard&paymentMethod=BankTransfer';

  function serveDelivery(level: QuoteServiceLevel) {
    serveCheckout(() =>
      quote({
        delivery: {
          available: true,
          unavailableReason: null,
          zoneId: 'z1',
          zoneName: 'Dhaka',
          serviceLevels: [level],
        },
        selectedServiceLevel: 'Standard',
      }),
    );
    server.use(
      http.get('*/api/customers/me/addresses', () =>
        HttpResponse.json({ addresses: [address], maxAddresses: 5 }),
      ),
    );
    setCheckoutUrl(DELIVERY_REVIEW);
  }

  it('greets the customer by first name and puts the quote total on Place order', async () => {
    serveCheckout(() => quote());
    setCheckoutUrl(REVIEW_URL);
    renderWithClient(<CheckoutScreen />);
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Almost there, Sam' }),
    ).toBeInTheDocument();
    const place = await screen.findByRole('button', { name: /^Place order/ });
    expect(place).toHaveTextContent('Place order · ৳4,900.00');
  });

  it('shows the amount the server says is still needed for free delivery on the review', async () => {
    serveDelivery(withAmount(money('250.00')));
    renderWithClient(<CheckoutScreen />);
    expect(await screen.findByText(/Add ৳250\.00 more/)).toBeInTheDocument();
  });

  it.each([
    ['no threshold (null)', null],
    ['a threshold already met (0.00)', money('0.00')],
  ])('shows no free-delivery card for %s', async (_name, amount) => {
    serveDelivery(withAmount(amount));
    renderWithClient(<CheckoutScreen />);
    await screen.findByRole('button', { name: /^Place order/ });
    expect(screen.queryByText(/more.*for free delivery/)).not.toBeInTheDocument();
  });

  it('shows no free-delivery card for collection', async () => {
    serveCheckout(() => quote());
    setCheckoutUrl(REVIEW_URL);
    renderWithClient(<CheckoutScreen />);
    await screen.findByRole('button', { name: /^Place order/ });
    expect(screen.queryByText(/for free delivery/)).not.toBeInTheDocument();
  });

  it('an earlier step can be re-opened from the strip under the pills', async () => {
    serveCheckout(() => quote());
    setCheckoutUrl('/checkout?step=payment&fulfilment=Pickup&pickupBranchId=br1');
    const user = userEvent.setup();
    renderWithClient(<CheckoutScreen />);
    await screen.findByRole('heading', { level: 1, name: 'How would you like to pay?' });
    await user.click(await screen.findByRole('button', { name: 'Change delivery or collection' }));
    await waitFor(() => expect(window.location.search).toMatch(/step=fulfilment/));
  });
});
