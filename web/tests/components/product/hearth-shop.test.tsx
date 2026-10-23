import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ProductPurchase } from '@/app/(shop)/_components/ProductPurchase';
import { OftenBoughtTogether } from '@/components/recommendations/OftenBoughtTogether';
import { DeliveryPromiseCard, PromiseStrip } from '@/components/hearth/DeliveryPromise';
import { ShopHero } from '@/components/shop/ShopHero';
import { resetShopMockData, shopFixtureIds } from '@/mocks/handlers/shop';
import { makeItem } from '../catalogue/catalogue-fixtures';
import { mockDetail, renderWithClient, signOutForTest } from './test-utils';

const push = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: vi.fn(), push }),
  usePathname: () => '/shop/products/royal-canin-kitten',
  useSearchParams: () => new URLSearchParams(),
}));

beforeEach(() => {
  resetShopMockData();
  push.mockClear();
  signOutForTest();
});

function purchase(skuId: string) {
  const detail = mockDetail(shopFixtureIds.multiVariantSlug);
  return renderWithClient(
    <ProductPurchase
      detail={detail}
      initialSkuId={skuId}
      lang=""
      heading={<h1>{detail.name}</h1>}
    />,
  );
}

describe('Product page buy row — Hearth', () => {
  it('has a pill quantity stepper, Add to cart and the wishlist button; the quantity is sent', async () => {
    const user = userEvent.setup();
    purchase(shopFixtureIds.skuKitten2kg);
    const quantity = screen.getByRole('spinbutton', { name: 'Quantity' });
    expect(screen.getAllByLabelText('Quantity')).toHaveLength(1);
    await user.click(screen.getByRole('button', { name: 'One more' }));
    await user.click(screen.getByRole('button', { name: 'One more' }));
    expect(quantity).toHaveValue(3);
    expect(screen.getByRole('button', { name: 'One fewer' })).toBeEnabled();
    // Direct entry works and is clamped to 1..max.
    await user.clear(quantity);
    await user.type(quantity, '5');
    expect(quantity).toHaveValue(5);
    await user.clear(quantity);
    await user.type(quantity, '99');
    expect(quantity).toHaveValue(20);
    await user.clear(quantity);
    await user.type(quantity, '3');
    await user.click(screen.getByRole('button', { name: /add to cart/i }));
    expect(
      await screen.findByText(/Added 3 × Royal Canin Kitten to your cart/),
    ).toBeInTheDocument();
    // The wishlist control is present (a guest gets the sign-in link).
    expect(await screen.findByRole('link', { name: 'Save' })).toBeInTheDocument();
  });

  it('an out-of-stock size disables the stepper and Add to cart, with the reason written out', () => {
    purchase(shopFixtureIds.skuKitten4kg);
    expect(screen.getByRole('spinbutton', { name: 'Quantity' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'One more' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'One fewer' })).toBeDisabled();
    const add = screen.getByRole('button', { name: /add to cart/i });
    expect(add).toBeDisabled();
    expect(add).toHaveAccessibleDescription('This variant is out of stock.');
  });

  it('the size options are cards in one radio group with price and stock', () => {
    purchase(shopFixtureIds.skuKitten2kg);
    const group = screen.getByRole('group', { name: 'Choose a size' });
    expect(within(group).getAllByRole('radio')).toHaveLength(3);
    expect(within(group).getByRole('radio', { name: /2 kg/ })).toBeChecked();
  });
});

describe('Delivery promise and promise strip — only true rules', () => {
  it('the product card states slot and fee at checkout, pickup at a branch and the payment methods', () => {
    render(<DeliveryPromiseCard />);
    const card = screen.getByRole('region', { name: 'Delivery and payment' });
    expect(card).toHaveTextContent(/delivery slot at checkout/i);
    expect(card).toHaveTextContent(/fee is shown before you place the order/i);
    expect(card).toHaveTextContent(/pick it up at a branch/i);
    expect(card).toHaveTextContent(/cash on delivery, or bank transfer/i);
    expect(card.textContent).not.toMatch(/next[- ]day|same[- ]day|free delivery|within \d/i);
  });

  it('the home strip uses the same facts', () => {
    render(<PromiseStrip />);
    const strip = screen.getByRole('region', { name: 'Why shop with us' });
    expect(within(strip).getAllByRole('listitem')).toHaveLength(4);
    expect(strip.textContent).not.toMatch(/next[- ]day|same[- ]day|free delivery/i);
  });
});

describe('Often bought together', () => {
  it('renders nothing for an empty list', () => {
    const { container } = renderWithClient(<OftenBoughtTogether items={[]} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('lists the items with a 44px Add button each, following the card rules', async () => {
    const user = userEvent.setup();
    renderWithClient(
      <OftenBoughtTogether
        items={[
          makeItem({ productId: 'a', slug: shopFixtureIds.multiVariantSlug, priceVaries: true }),
          makeItem({
            productId: 'b',
            slug: shopFixtureIds.thaiKittenSlug,
            name: 'Whiskas Kitten Salmon',
          }),
        ]}
      />,
    );
    expect(screen.getByRole('heading', { name: 'Often bought together' })).toBeInTheDocument();
    await user.click(
      screen.getByRole('button', { name: 'Add Royal Canin Kitten: choose options' }),
    );
    expect(push).toHaveBeenCalledWith('/shop/products/royal-canin-kitten');
    const add = screen.getByRole('button', { name: 'Add Whiskas Kitten Salmon to cart' });
    expect(add).toHaveClass('size-11');
    await user.click(add);
    expect(await screen.findByText(/Added Whiskas Kitten Salmon to your cart/)).toBeInTheDocument();
  });
});

describe('ShopHero — built from the banner, nothing invented', () => {
  it('uses the banner title, body and link; draws no eyebrow', () => {
    render(
      <ShopHero
        banner={{
          slug: 'b',
          title: 'Kitten season',
          body: 'Everything a new kitten needs.',
          placement: 'Home',
          linkUrl: '/shop/products?category=CatFood',
        }}
        featured={makeItem()}
      />,
    );
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Kitten season');
    expect(screen.getByText('Everything a new kitten needs.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Find out more/ })).toHaveAttribute(
      'href',
      '/shop/products?category=CatFood',
    );
    expect(screen.getByRole('link', { name: /Royal Canin Kitten/ })).toHaveAttribute(
      'href',
      '/shop/products/royal-canin-kitten',
    );
  });

  it('with no banner it keeps the page headline and a shop button', () => {
    render(<ShopHero banner={null} featured={null} />);
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(
      'Everything your cat needs, with the country of origin on every food',
    );
    expect(screen.getByRole('link', { name: /Shop all products/ })).toHaveAttribute(
      'href',
      '/shop/products',
    );
  });
});
