// @vitest-environment node
import { describe, expect, it, vi } from 'vitest';
import { renderToStaticMarkup } from 'react-dom/server';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { CatalogueView } from '@/app/(shop)/_components/CatalogueView';
import { EMPTY_CATALOGUE_PARAMS } from '@/lib/shop/catalogueParams';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
  usePathname: () => '/shop/products',
  useSearchParams: () => new URLSearchParams(),
}));

describe('CatalogueView server rendering — HO-SHP-002 §8.2', () => {
  it('U-F1: a storefront 503 renders the maintenance notice instead of the page', async () => {
    server.use(http.get('*/api/shop/products', () => new HttpResponse(null, { status: 503 })));
    const html = renderToStaticMarkup(
      await CatalogueView({ params: EMPTY_CATALOGUE_PARAMS, basePath: '/shop/products' }),
    );
    expect(html).toContain('Clinic bookings and your account are still available');
    expect(html).toContain('href="/portal"');
  });

  it('renders real product links and pagination from the API (no client JS needed)', async () => {
    const html = renderToStaticMarkup(
      await CatalogueView({ params: EMPTY_CATALOGUE_PARAMS, basePath: '/shop/products' }),
    );
    expect(html).toContain('href="/shop/products/royal-canin-kitten"');
    expect(html).toContain('aria-current="page"');
    expect(html).toContain('href="/shop/products?pageNumber=2"');
  });
});
