import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { WishlistButton } from '@/components/wishlist/WishlistButton';
import { ProductPurchase } from '@/app/(shop)/_components/ProductPurchase';
import { BundleContents } from '@/components/product/BundleContents';
import { ProductDetailsTabs } from '@/components/product/ProductDetailsTabs';
import { ProductHeading } from '@/components/product/ProductHeading';
import { resetShopMockData, shopFixtureIds } from '@/mocks/handlers/shop';
import { mockDetail, renderWithClient, signInForTest, signOutForTest } from './test-utils';

const replace = vi.fn();
let search = '';
vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace, push: vi.fn() }),
  usePathname: () => '/shop/products/royal-canin-kitten',
  useSearchParams: () => new URLSearchParams(search),
}));

beforeEach(() => {
  resetShopMockData();
  replace.mockClear();
  search = '';
  signOutForTest();
});
afterEach(signOutForTest);

function purchase(slug = shopFixtureIds.multiVariantSlug, initialSkuId?: string, lang = '') {
  const detail = mockDetail(slug);
  return renderWithClient(
    <ProductPurchase
      detail={detail}
      initialSkuId={initialSkuId ?? detail.skus[0]!.skuId}
      lang={lang}
      heading={<h1>{detail.name}</h1>}
    />,
  );
}

describe('ProductPurchase — HO-SHP-002 §11.3', () => {
  it('U-B1: selecting the second variant updates ?sku=, the price, availability and images', async () => {
    const user = userEvent.setup();
    purchase();
    const buyBox = () => within(screen.getByRole('region', { name: 'Price and availability' }));
    expect(buyBox().getByText('৳650.00')).toBeInTheDocument();

    await user.click(screen.getByRole('radio', { name: /2 kg/ }));
    expect(replace).toHaveBeenCalledWith(
      `/shop/products/royal-canin-kitten?sku=${shopFixtureIds.skuKitten2kg}`,
      {
        scroll: false,
      },
    );
    expect(buyBox().getByText('৳2,450.00')).toBeInTheDocument();

    await user.click(screen.getByRole('radio', { name: /4 kg/ }));
    expect(buyBox().getByText('৳4,400.00')).toBeInTheDocument();
    // The 4 kg variant has its own image, shown first.
    expect(screen.getAllByRole('img')[0]).toHaveAccessibleName('Royal Canin Kitten 4 kg bag');
  });

  it('U-B2: an out-of-stock variant offers the alert while the others still read In stock', async () => {
    const user = userEvent.setup();
    purchase();
    const selector = within(screen.getByRole('group', { name: 'Choose a size' }));
    await user.click(screen.getByRole('radio', { name: /4 kg/ }));

    const buyBox = within(screen.getByRole('region', { name: 'Price and availability' }));
    expect(buyBox.getByText('Out of stock')).toBeInTheDocument();
    // Guests are sent to sign in to ask for the alert.
    expect(await buyBox.findByRole('link', { name: /Tell me when it.s back/ })).toBeInTheDocument();
    // The other variants are still in stock in the selector, in text.
    expect(selector.getByRole('radio', { name: /400 g/ })).toHaveAccessibleName(/In stock/);
    expect(selector.getByRole('radio', { name: /2 kg/ })).toHaveAccessibleName(/In stock/);
    expect(selector.getByRole('radio', { name: /4 kg/ })).toHaveAccessibleName(/Out of stock/);
  });

  it('U-B2 (signed in): the alert is a button, and the request shows the channel', async () => {
    const user = userEvent.setup();
    signInForTest();
    purchase(shopFixtureIds.multiVariantSlug, shopFixtureIds.skuKitten4kg);
    // The seeded session already has a PENDING alert for the 4 kg size.
    expect(await screen.findByText(/You.ll be told when it.s back/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Cancel alert' }));
    await user.click(await screen.findByRole('button', { name: /Tell me when it.s back/ }));
    expect(
      await screen.findByText(/We.ll send one message by in-app notification and SMS/),
    ).toBeInTheDocument();
  });

  it('U-B3: a SKU with expiryNotice shows the best-before notice; one without does not', async () => {
    const user = userEvent.setup();
    purchase();
    expect(screen.queryByText(/Best before/)).not.toBeInTheDocument();
    await user.click(screen.getByRole('radio', { name: /2 kg/ }));
    expect(
      screen.getByText(
        /Best before 12 Nov 2026 — this is the date on the stock we would send you\./,
      ),
    ).toBeInTheDocument();
  });

  it('U-B11: a guest clicking Save is sent to sign in with ?next= back to the same variant', async () => {
    purchase(shopFixtureIds.multiVariantSlug, shopFixtureIds.skuKitten2kg);
    const link = await screen.findByRole('link', { name: 'Save' });
    expect(link).toHaveAttribute(
      'href',
      `/sign-in?next=${encodeURIComponent(`/shop/products/royal-canin-kitten?sku=${shopFixtureIds.skuKitten2kg}`)}`,
    );
  });

  it('a signed-in customer toggles Save / Saved (the label carries the state)', async () => {
    const user = userEvent.setup();
    signInForTest();
    purchase(shopFixtureIds.multiVariantSlug, shopFixtureIds.skuKitten2kg);
    const button = await screen.findByRole('button', { name: 'Save' });
    // S15: the state is announced once, by the label, not also by aria-pressed.
    expect(button).not.toHaveAttribute('aria-pressed');
    await user.click(button);
    const saved = await screen.findByRole('button', { name: 'Saved' });
    expect(saved).not.toHaveAttribute('aria-pressed');
  });

  it('S15: the wishlist button stays focusable while saving and ignores a second click', async () => {
    const onToggle = vi.fn();
    const user = userEvent.setup();
    render(<WishlistButton saved={false} pending onToggle={onToggle} />);
    const button = screen.getByRole('button', { name: 'Save' });
    expect(button).not.toBeDisabled();
    expect(button).toHaveAttribute('aria-disabled', 'true');
    button.focus();
    await user.click(button);
    expect(onToggle).not.toHaveBeenCalled();
    expect(button).toHaveFocus();
  });

  it('S15: a wishlist error is cleared when the shopper switches variant', async () => {
    const user = userEvent.setup();
    signInForTest();
    server.use(
      http.put('*/api/customers/me/wishlist/items/:skuId', () =>
        HttpResponse.json({ title: 'boom' }, { status: 500 }),
      ),
    );
    purchase(shopFixtureIds.multiVariantSlug, shopFixtureIds.skuKitten2kg);
    await user.click(await screen.findByRole('button', { name: 'Save' }));
    expect(await screen.findByRole('alert')).toBeInTheDocument();

    await user.click(screen.getByRole('radio', { name: /400 g/ }));
    await waitFor(() => expect(screen.queryByRole('alert')).not.toBeInTheDocument());
  });

  it('S8: Back to a URL without ?sku= returns to the default variant', () => {
    search = `sku=${shopFixtureIds.skuKitten2kg}`;
    const detail = mockDetail(shopFixtureIds.multiVariantSlug);
    const { rerender } = renderWithClient(
      <ProductPurchase
        detail={detail}
        initialSkuId={shopFixtureIds.skuKitten2kg}
        lang=""
        heading={<h1>{detail.name}</h1>}
      />,
    );
    const price = () => within(screen.getByRole('region', { name: 'Price and availability' }));
    expect(price().getByText('৳2,450.00')).toBeInTheDocument();

    search = '';
    rerender(
      <ProductPurchase
        detail={detail}
        initialSkuId={shopFixtureIds.skuKitten2kg}
        lang=""
        heading={<h1>{detail.name}</h1>}
      />,
    );
    expect(price().getByText('৳650.00')).toBeInTheDocument();
  });

  it('leaves the action slot empty for HO-ORD-002 (no cart here)', () => {
    const { container } = purchase();
    const slot = container.querySelector('[data-slot="buy-box-actions"]');
    expect(slot).toBeInTheDocument();
    expect(slot).toBeEmptyDOMElement();
    expect(screen.queryByRole('button', { name: /add to cart/i })).not.toBeInTheDocument();
  });

  it('handles every SKU being out of stock (fail-closed availability) with honest messaging', () => {
    const detail = mockDetail(shopFixtureIds.multiVariantSlug);
    const failClosed = {
      ...detail,
      skus: detail.skus.map((s) => ({
        ...s,
        availability: 'OutOfStock' as const,
        expiryNotice: null,
      })),
    };
    renderWithClient(
      <ProductPurchase
        detail={failClosed}
        initialSkuId={failClosed.skus[0]!.skuId}
        lang=""
        heading={<h1>x</h1>}
      />,
    );
    const group = within(screen.getByRole('group', { name: 'Choose a size' }));
    expect(group.getAllByText('Out of stock')).toHaveLength(3);
    expect(screen.getAllByText('Out of stock').length).toBeGreaterThan(3);
    expect(screen.queryByRole('button', { name: /add to cart/i })).not.toBeInTheDocument();
  });
});

describe('ProductDetailsTabs — US-SHP-03 AC1, US-SHP-14 AC2', () => {
  it('U-B4: ingredients as text, the analysis table with % in every cell, feeding guide, storage, origin', async () => {
    const user = userEvent.setup();
    render(
      <ProductDetailsTabs
        detail={mockDetail(shopFixtureIds.multiVariantSlug)}
        originNotes={'Notes **here**'}
      />,
    );

    await user.click(screen.getByRole('tab', { name: 'Ingredients & analysis' }));
    const panel = screen.getByRole('tabpanel', { name: 'Ingredients & analysis' });
    expect(within(panel).getByText(/Dehydrated poultry protein/)).toBeInTheDocument();
    const table = within(panel).getByRole('table', { name: 'Guaranteed analysis' });
    const cells = within(table).getAllByRole('cell');
    expect(cells).toHaveLength(4);
    for (const cell of cells) expect(cell.textContent).toMatch(/%$/);

    await user.click(screen.getByRole('tab', { name: 'Feeding guide' }));
    const feeding = screen.getByRole('tabpanel', { name: 'Feeding guide' });
    expect(within(feeding).getByRole('heading', { name: 'Daily amounts' })).toBeInTheDocument();
    expect(within(feeding).getByText('40 g').tagName).toBe('STRONG');

    await user.click(screen.getByRole('tab', { name: 'Storage' }));
    expect(screen.getByRole('tabpanel', { name: 'Storage' })).toHaveTextContent(/cool, dry place/);

    await user.click(screen.getByRole('tab', { name: 'Origin' }));
    const origin = screen.getByRole('tabpanel', { name: 'Origin' });
    expect(origin).toHaveTextContent('Country of origin: Thailand');
    expect(origin).toHaveTextContent('Manufacturer: PurrCare Test Foods Ltd');
    expect(within(origin).getByText('here').tagName).toBe('STRONG');
  });

  it('tabs are keyboard operable (arrows, Home, End)', async () => {
    const user = userEvent.setup();
    render(<ProductDetailsTabs detail={mockDetail(shopFixtureIds.multiVariantSlug)} />);
    screen.getByRole('tab', { name: 'Description' }).focus();
    await user.keyboard('{ArrowRight}');
    expect(screen.getByRole('tab', { name: 'Ingredients & analysis' })).toHaveFocus();
    expect(screen.getByRole('tab', { name: 'Ingredients & analysis' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
    await user.keyboard('{End}');
    expect(screen.getByRole('tab', { name: 'Origin' })).toHaveFocus();
    await user.keyboard('{Home}');
    expect(screen.getByRole('tab', { name: 'Description' })).toHaveFocus();
  });

  it('renders ingredient and storage text literally (plain text, never HTML)', () => {
    const detail = mockDetail(shopFixtureIds.multiVariantSlug);
    detail.food!.ingredientList = '<script>alert(1)</script> chicken';
    detail.food!.storageInstructions = '<b>cool</b>';
    const { container } = render(<ProductDetailsTabs detail={detail} />);
    expect(container.querySelector('script, b')).toBeNull();
    expect(container.textContent).toContain('<script>alert(1)</script> chicken');
    expect(container.textContent).toContain('<b>cool</b>');
  });

  it('U-F3: a field that fell back to English carries lang="en"; the others lang="bn"', () => {
    const detail = mockDetail(shopFixtureIds.thaiKittenSlug, 'bn');
    expect(detail.locale).toBe('bn');
    expect(detail.fieldLocales.feedingGuide).toBe('en');
    const { container } = render(
      <>
        <ProductHeading
          name={detail.name}
          nameLang={detail.fieldLocales.name!}
          brand={detail.brand}
          rating={detail.rating}
          country={detail.countryOfOrigin}
        />
        <ProductDetailsTabs detail={detail} />
      </>,
    );
    expect(screen.getByRole('heading', { level: 1 })).toHaveAttribute('lang', 'bn');
    expect(container.querySelector('[role="tabpanel"] [lang="bn"]')).not.toBeNull(); // description
    // The feeding-guide element carries lang="en".
    const feeding = container.querySelector('[role="tabpanel"][id$="panel-feeding"]')!;
    expect(feeding.querySelector('[lang="en"]')).not.toBeNull();
    expect(feeding.querySelector('[lang="bn"]')).toBeNull();
    // Ingredients are in bn.
    const ingredients = container.querySelector('[role="tabpanel"][id$="panel-ingredients"]')!;
    expect(ingredients.querySelector('p[lang="bn"]')).not.toBeNull();
  });
});

describe('BundleContents — US-SHP-12', () => {
  it('U-B5: components with quantities and availability text; the kit is unavailable with a component out', () => {
    const detail = mockDetail(shopFixtureIds.kitSlug);
    expect(detail.skus[0]!.availability).toBe('OutOfStock');
    render(<BundleContents components={detail.bundle!.components} />);
    const items = screen.getAllByRole('listitem');
    expect(items).toHaveLength(2);
    expect(items[0]).toHaveTextContent('1 × Royal Canin Kitten (400 g)');
    expect(within(items[0]!).getByText('In stock')).toBeInTheDocument();
    expect(items[1]).toHaveTextContent('2 × Catnip Mouse Set');
    expect(within(items[1]!).getByText('Out of stock')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Royal Canin Kitten' })).toHaveAttribute(
      'href',
      '/shop/products/royal-canin-kitten',
    );
  });
});
