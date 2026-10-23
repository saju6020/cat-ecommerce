import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { resetShopMockData, shopFixtureIds } from '@/mocks/handlers/shop';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { renderWithClient, signInForTest, signOutForTest } from '../product/test-utils';
import { ProductCard } from '@/components/catalogue/ProductCard';
import { NoResults } from '@/components/catalogue/NoResults';
import { Pagination } from '@/components/catalogue/Pagination';
import { makeItem } from './catalogue-fixtures';

const push = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: vi.fn(), push }),
  usePathname: () => '/shop/products',
  useSearchParams: () => new URLSearchParams(),
}));

beforeEach(() => {
  resetShopMockData();
  push.mockClear();
  signOutForTest();
});

describe('ProductCard — HO-SHP-002 §11.2', () => {
  it('U-A3: varying price, out of stock, prescription, rated, no image URL', () => {
    renderWithClient(
      <ProductCard
        item={makeItem({
          priceVaries: true,
          availability: 'OutOfStock',
          prescriptionRequired: true,
          kind: 'Bundle',
        })}
      />,
    );
    const card = within(screen.getByRole('article'));
    expect(card.getByText('From ৳650.00')).toBeInTheDocument();
    expect(card.getByText('Out of stock')).toBeInTheDocument();
    expect(card.getByText('Vet authorisation needed')).toBeInTheDocument();
    expect(card.getByText('4.5 out of 5 (23 reviews)')).toBeInTheDocument();
    expect(card.getByText('Kit')).toBeInTheDocument();
    // No photo URL: the tinted pack drawing stands in, named by the API's alt text.
    expect(card.getByRole('img', { name: 'Royal Canin Kitten bag' })).toBeInTheDocument();
    expect(card.getByRole('heading', { level: 2 })).toHaveTextContent('Royal Canin Kitten');
    expect(card.getByRole('link', { name: 'Royal Canin Kitten' })).toHaveAttribute(
      'href',
      '/shop/products/royal-canin-kitten',
    );
  });

  it('renders names and brands as text, never markup', () => {
    renderWithClient(
      <ProductCard item={makeItem({ name: '<b>Bold</b> food', brand: '<i>x</i>' })} />,
    );
    expect(screen.getByText('<b>Bold</b> food')).toBeInTheDocument();
    expect(document.querySelector('article b, article i')).toBeNull();
  });

  it('carries the content language on the product link', () => {
    renderWithClient(<ProductCard item={makeItem()} lang="bn" />);
    expect(screen.getByRole('link', { name: 'Royal Canin Kitten' })).toHaveAttribute(
      'href',
      '/shop/products/royal-canin-kitten?lang=bn',
    );
  });
});

describe('ProductCard — wishlist heart and Add (owner decision 2026-10-16)', () => {
  const single = () =>
    makeItem({
      productId: 'p-2',
      slug: shopFixtureIds.thaiKittenSlug,
      name: 'Whiskas Kitten Salmon',
    });

  it('shows the 44px wishlist heart; a guest is sent to sign in and back to the product', async () => {
    renderWithClient(<ProductCard item={single()} />);
    const heart = await screen.findByRole('link', {
      name: 'Save Whiskas Kitten Salmon to wishlist: sign in first',
    });
    expect(heart).toHaveAttribute(
      'href',
      `/sign-in?next=${encodeURIComponent(`/shop/products/${shopFixtureIds.thaiKittenSlug}`)}`,
    );
    expect(heart).toHaveClass('size-11');
  });

  it('a signed-in customer toggles the heart (aria-pressed) for a single-size product', async () => {
    const user = userEvent.setup();
    signInForTest();
    renderWithClient(<ProductCard item={single()} />);
    const heart = await screen.findByRole('button', {
      name: 'Save Whiskas Kitten Salmon to wishlist',
    });
    expect(heart).toHaveAttribute('aria-pressed', 'false');
    await user.click(heart);
    await waitFor(() => expect(heart).toHaveAttribute('aria-pressed', 'true'));
    await user.click(heart);
    await waitFor(() => expect(heart).toHaveAttribute('aria-pressed', 'false'));
  });

  it('Add puts a single-size product straight in the cart', async () => {
    const user = userEvent.setup();
    renderWithClient(<ProductCard item={single()} />);
    await user.click(screen.getByRole('button', { name: 'Add Whiskas Kitten Salmon to cart' }));
    // No visible note (owner, 2026-10-18): only a screen-reader announcement, and no View cart link.
    expect(await screen.findByText(/Added Whiskas Kitten Salmon to your cart/)).toHaveClass('sr-only');
    expect(screen.queryByRole('link', { name: 'View cart' })).not.toBeInTheDocument();
    expect(push).not.toHaveBeenCalled();
  });

  it('Add on a product sold in several sizes opens the product page to choose', async () => {
    const user = userEvent.setup();
    renderWithClient(
      <ProductCard
        item={makeItem({ slug: shopFixtureIds.multiVariantSlug, priceVaries: true })}
        lang="bn"
      />,
    );
    await user.click(
      screen.getByRole('button', { name: 'Add Royal Canin Kitten: choose options' }),
    );
    expect(push).toHaveBeenCalledWith('/shop/products/royal-canin-kitten?lang=bn');
  });

  it('Add on a prescription diet goes to the product page, where the cat flow lives', async () => {
    const user = userEvent.setup();
    renderWithClient(
      <ProductCard
        item={makeItem({ slug: shopFixtureIds.rxEligibleSlug, prescriptionRequired: true })}
      />,
    );
    await user.click(
      screen.getByRole('button', { name: 'Add Royal Canin Kitten: choose options' }),
    );
    expect(push).toHaveBeenCalledWith(`/shop/products/${shopFixtureIds.rxEligibleSlug}`);
  });

  it('an out-of-stock card offers Alert me (the stock alert) instead of Add', () => {
    renderWithClient(<ProductCard item={makeItem({ availability: 'OutOfStock' })} />);
    expect(screen.queryByRole('button', { name: /^Add / })).not.toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: 'Tell me when Royal Canin Kitten is back' }),
    ).toHaveAttribute('href', '/shop/products/royal-canin-kitten#stock-alert');
  });
});

describe('NoResults — HO-SHP-002 §11.2, US-SHP-06 AC3', () => {
  it('U-A4: Did you mean links rewrite q; the popular grid renders', () => {
    renderWithClient(
      <NoResults
        q="royl canan"
        suggestions={{
          didYouMean: ['royal canin'],
          popular: [makeItem({ productId: 'p-2', name: 'Popular Pick' })],
        }}
        hrefForQuery={(q) => `/shop/products?q=${encodeURIComponent(q)}&brand=Whiskas`}
        clearHref="/shop/products?q=royl%20canan"
      />,
    );
    expect(screen.getByText(/No products match/)).toHaveTextContent('“royl canan”');
    expect(screen.getByRole('link', { name: 'royal canin' })).toHaveAttribute(
      'href',
      '/shop/products?q=royal%20canin&brand=Whiskas',
    );
    expect(screen.getByRole('heading', { name: 'Popular right now' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Popular Pick' })).toBeInTheDocument();
  });

  it('filters only (no q): says so and offers Clear filters', () => {
    render(
      <NoResults q="" suggestions={null} hrefForQuery={() => ''} clearHref="/shop/products" />,
    );
    expect(screen.getByText('Nothing matches these filters')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Clear filters' })).toHaveAttribute(
      'href',
      '/shop/products',
    );
  });
});

describe('Pagination — real links', () => {
  it('marks the current page and links the rest', () => {
    render(
      <Pagination
        pageNumber={2}
        pageSize={24}
        totalCount={100}
        hrefFor={(p) => `/shop/products?pageNumber=${p}`}
      />,
    );
    expect(screen.getByRole('link', { name: 'Page 2' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('link', { name: 'Page 3' })).toHaveAttribute(
      'href',
      '/shop/products?pageNumber=3',
    );
    expect(screen.getByRole('link', { name: 'Previous' })).toBeInTheDocument();
  });

  it('renders nothing for a single page', () => {
    const { container } = render(
      <Pagination pageNumber={1} pageSize={24} totalCount={5} hrefFor={() => '/x'} />,
    );
    expect(container).toBeEmptyDOMElement();
  });
});

// One card per size (owner, 2026-10-18).
describe('ProductCard — one card per size', () => {
  function serveCart(lines: Array<{ skuId: string; productId: string; quantity: number }>) {
    server.use(
      http.get('*/api/cart', () =>
        HttpResponse.json({
          cartId: 'cart-1',
          lines: lines.map((l, i) => ({ lineId: `l${i}`, ...l })),
          itemCount: lines.reduce((s, l) => s + l.quantity, 0),
          subtotal: { amount: '0.00', currencyCode: 'BDT' },
          maxQuantityPerLine: 10,
          guestCartPending: false,
        }),
      ),
    );
  }

  it('shows the size under the name, links to that size, and reads "<name> · <size>"', () => {
    serveCart([]);
    renderWithClient(<ProductCard item={makeItem({ skuId: 'sku-1kg', sizeLabel: '1 kg' })} />);
    const card = within(screen.getByRole('article'));
    expect(card.getByText('1 kg')).toBeInTheDocument();
    const link = card.getByRole('link', { name: /Royal Canin Kitten\s*·\s*1 kg/ });
    expect(link.getAttribute('href')).toContain('sku=sku-1kg');
  });

  it('Add puts exactly that size in the cart, without opening the product page', async () => {
    serveCart([]);
    const posted: unknown[] = [];
    server.use(
      http.post('*/api/cart/lines', async ({ request }) => {
        posted.push(await request.json());
        return HttpResponse.json({ lineId: 'l1' });
      }),
    );
    renderWithClient(
      <ProductCard item={makeItem({ skuId: 'sku-1kg', sizeLabel: '1 kg', priceVaries: false })} />,
    );
    await userEvent.click(
      screen.getByRole('button', { name: /^Add Royal Canin Kitten · 1 kg to cart/ }),
    );
    await waitFor(() => expect(posted).toEqual([{ skuId: 'sku-1kg', quantity: 1 }]));
    expect(push).not.toHaveBeenCalled();
  });

  it('shows how many of this size are in the cart: a bubble on Add, "2 in cart" and an In cart tag', async () => {
    serveCart([
      { skuId: 'sku-1kg', productId: 'p1', quantity: 2 },
      { skuId: 'sku-other', productId: 'p1', quantity: 5 },
    ]);
    renderWithClient(<ProductCard item={makeItem({ skuId: 'sku-1kg', sizeLabel: '1 kg' })} />);
    expect(await screen.findByTestId('card-in-cart-count')).toHaveTextContent('2');
    expect(screen.getByText('2 in cart')).toBeInTheDocument();
    expect(screen.getByText('In cart')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /\(2 in cart\)/ })).toBeInTheDocument();
  });

  it('shows no count when this size is not in the cart', async () => {
    serveCart([{ skuId: 'sku-other', productId: 'p1', quantity: 5 }]);
    renderWithClient(<ProductCard item={makeItem({ skuId: 'sku-1kg', sizeLabel: '1 kg' })} />);
    await waitFor(() => expect(screen.queryByTestId('card-in-cart-count')).not.toBeInTheDocument());
    expect(screen.queryByText(/in cart/)).not.toBeInTheDocument();
  });
});
