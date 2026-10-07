import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { ProductCard } from '@/components/catalogue/ProductCard';
import { NoResults } from '@/components/catalogue/NoResults';
import { Pagination } from '@/components/catalogue/Pagination';
import { makeItem } from './catalogue-fixtures';

describe('ProductCard — HO-SHP-002 §11.2', () => {
  it('U-A3: varying price, out of stock, prescription, rated, no image URL', () => {
    render(
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
    const image = card.getByRole('img', { name: 'Royal Canin Kitten bag' });
    expect(image).toHaveAttribute('src', '/images/product-placeholder.svg');
    expect(image).toHaveAttribute('loading', 'lazy');
    expect(card.getByRole('heading', { level: 2 })).toHaveTextContent('Royal Canin Kitten');
    expect(card.getByRole('link', { name: 'Royal Canin Kitten' })).toHaveAttribute(
      'href',
      '/shop/products/royal-canin-kitten',
    );
  });

  it('renders names and brands as text, never markup', () => {
    render(<ProductCard item={makeItem({ name: '<b>Bold</b> food', brand: '<i>x</i>' })} />);
    expect(screen.getByText('<b>Bold</b> food')).toBeInTheDocument();
    expect(document.querySelector('article b, article i')).toBeNull();
  });

  it('carries the content language on the product link', () => {
    render(<ProductCard item={makeItem()} lang="bn" />);
    expect(screen.getByRole('link', { name: 'Royal Canin Kitten' })).toHaveAttribute(
      'href',
      '/shop/products/royal-canin-kitten?lang=bn',
    );
  });
});

describe('NoResults — HO-SHP-002 §11.2, US-SHP-06 AC3', () => {
  it('U-A4: Did you mean links rewrite q; the popular grid renders', () => {
    render(
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
