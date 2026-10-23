import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { CheckoutFreeDelivery } from '@/components/checkout/CheckoutFreeDelivery';
import { computeQuote } from '../../../mocks/handlers/ordering/quote';
import { orderingIds } from '../../../mocks/handlers/ordering/fixtures';
import { skuInfo } from '../../../mocks/handlers/ordering/store';
import type { CheckoutQuote, QuoteServiceLevel } from '@/lib/api/checkout';

const money = (amount: string) => ({ amount, currencyCode: 'BDT' });

function level(overrides: Partial<QuoteServiceLevel> = {}): QuoteServiceLevel {
  return {
    level: 'Standard',
    charge: money('80.00'),
    freeOver: money('5000.00'),
    amountToFreeDelivery: money('250.00'),
    leadTimeDays: 1,
    slots: [],
    ...overrides,
  };
}

/** Only the parts of the quote the free-delivery card reads. */
function quoteWith(
  levels: QuoteServiceLevel[],
  selected: 'Standard' | 'Express' | null = 'Standard',
): CheckoutQuote {
  return {
    delivery: {
      available: true,
      unavailableReason: null,
      zoneId: 'z1',
      zoneName: 'Dhaka',
      serviceLevels: levels,
    },
    selectedServiceLevel: selected,
  } as unknown as CheckoutQuote;
}

describe('CheckoutFreeDelivery - the free-delivery card is fed only by the server', () => {
  it('shows the amount the server says is still needed, for the selected service level', () => {
    render(<CheckoutFreeDelivery quote={quoteWith([level()])} />);
    expect(screen.getByText(/Add ৳250\.00 more/)).toBeInTheDocument();
    expect(screen.getByText(/for free delivery in/)).toHaveTextContent('Dhaka');
    expect(screen.getByRole('link', { name: 'Keep shopping' })).toHaveAttribute('href', '/shop');
  });

  it('uses the SELECTED level, not the first one', () => {
    const express = level({
      level: 'Express',
      amountToFreeDelivery: money('900.00'),
      freeOver: money('5650.00'),
    });
    render(<CheckoutFreeDelivery quote={quoteWith([level(), express], 'Express')} />);
    expect(screen.getByText(/Add ৳900\.00 more/)).toBeInTheDocument();
  });

  it.each([
    ['the zone has no threshold (null)', level({ freeOver: null, amountToFreeDelivery: null })],
    ['the threshold is already met (0.00)', level({ amountToFreeDelivery: money('0.00') })],
    ['the threshold is already met (0)', level({ amountToFreeDelivery: money('0') })],
  ])('renders nothing when %s', (_name, value) => {
    const { container } = render(<CheckoutFreeDelivery quote={quoteWith([value])} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('renders nothing for collection (no service level is selected)', () => {
    const { container } = render(<CheckoutFreeDelivery quote={quoteWith([level()], null)} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('renders nothing when an older server omits the fields', () => {
    const old = {
      level: 'Standard',
      charge: money('80.00'),
      leadTimeDays: 1,
      slots: [],
    } as unknown as QuoteServiceLevel;
    const { container } = render(<CheckoutFreeDelivery quote={quoteWith([old])} />);
    expect(container).toBeEmptyDOMElement();
  });
});

describe('the MSW quote computes the two new fields like the server', () => {
  it('threshold minus goods value, floored at zero, and null where a level has no threshold', () => {
    const sku = skuInfo(orderingIds.skuKitten400)!;
    const at = (quantity: number) =>
      computeQuote(
        [
          {
            lineId: 'a',
            skuId: orderingIds.skuKitten400,
            quantity,
            catId: null,
            priceWhenAddedCents: sku.priceCents!,
          },
        ],
        {
          fulfilment: 'Delivery',
          address: { district: 'Dhaka', postalCode: null },
          customerHasOrders: false,
        },
      ).wire as {
        delivery: { serviceLevels: Array<Record<string, { amount: string } | null | string>> };
      };

    const small = at(1).delivery.serviceLevels;
    const standard = small.find((l) => l.level === 'Standard')!;
    const express = small.find((l) => l.level === 'Express')!;
    expect(standard.freeOver).toEqual({ amount: '5000.00', currencyCode: 'BDT' });
    const goods = sku.priceCents!;
    expect(standard.amountToFreeDelivery).toEqual({
      amount: ((500_000 - goods) / 100).toFixed(2),
      currencyCode: 'BDT',
    });
    expect(express.freeOver).toBeNull();
    expect(express.amountToFreeDelivery).toBeNull();

    // Far past the threshold: the amount is zero, never negative.
    const big = at(Math.ceil(500_000 / goods) + 1).delivery.serviceLevels.find(
      (l) => l.level === 'Standard',
    )!;
    expect(big.amountToFreeDelivery).toEqual({ amount: '0.00', currencyCode: 'BDT' });
  });
});
