// @vitest-environment node
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { server } from '@/mocks/server';
import { ApiError, ApiNotFoundError, getPublic } from '@/lib/api/client';
import {
  getCatalogueFiltersServer,
  getCatalogueServer,
  getCategoriesServer,
  getProductDetailServer,
  getProductReviewsServer,
  clampReviewPage,
} from '@/lib/api/shop-catalogue';
import { EMPTY_CATALOGUE_PARAMS } from '@/lib/shop/catalogueParams';

describe('getPublic — HO-SHP-002 §8.2, ADR-FE-030', () => {
  it('reads a public endpoint without credentials or cookies', async () => {
    let seen: Request | undefined;
    server.use(
      http.get('*/api/shop/locales', ({ request }) => {
        seen = request;
        return HttpResponse.json({ defaultLocale: 'en', locales: ['en'] });
      }),
    );
    const result = await getPublic<{ defaultLocale: string }>('/api/shop/locales');
    expect(result.defaultLocale).toBe('en');
    expect(seen?.headers.get('cookie')).toBeNull();
    expect(seen?.headers.get('x-csrf-token')).toBeNull();
  });

  it('maps 404 and 503', async () => {
    server.use(
      http.get('*/api/shop/products/nope', () => new HttpResponse(null, { status: 404 })),
      http.get('*/api/shop/categories', () => new HttpResponse(null, { status: 503 })),
    );
    await expect(getPublic('/api/shop/products/nope')).rejects.toBeInstanceOf(ApiNotFoundError);
    await expect(getPublic('/api/shop/categories')).rejects.toMatchObject({ status: 503 });
    await expect(getPublic('/api/shop/categories')).rejects.toBeInstanceOf(ApiError);
  });
});

describe('getPublic — S11 and S12', () => {
  it('S11: ignores globalThis.__mswFetch outside next dev', async () => {
    const hook = vi.fn();
    (globalThis as { __mswFetch?: unknown }).__mswFetch = hook;
    server.use(
      http.get('*/api/shop/locales', () =>
        HttpResponse.json({ defaultLocale: 'en', locales: ['en'] }),
      ),
    );
    try {
      vi.stubEnv('NODE_ENV', 'production');
      await getPublic('/api/shop/locales');
      expect(hook).not.toHaveBeenCalled();
      vi.stubEnv('NODE_ENV', 'development');
      hook.mockResolvedValue(Response.json({ defaultLocale: 'en', locales: ['en'] }));
      await getPublic('/api/shop/locales');
      expect(hook).toHaveBeenCalledTimes(1);
    } finally {
      vi.unstubAllEnvs();
      delete (globalThis as { __mswFetch?: unknown }).__mswFetch;
    }
  });

  it('S12: catalogue search/filter reads are not cached; fixed shell reads are', async () => {
    const calls: Array<{ url: string; revalidate: unknown }> = [];
    const spy = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      calls.push({
        url: String(input),
        revalidate: (init as { next?: { revalidate?: number } } | undefined)?.next?.revalidate,
      });
      return Response.json({});
    });
    try {
      await getCatalogueServer({ ...EMPTY_CATALOGUE_PARAMS, q: 'a unique query' });
      await getCatalogueFiltersServer('SomeJunk');
      await getCatalogueFiltersServer();
      await getCategoriesServer();
    } finally {
      spy.mockRestore();
    }
    expect(calls.map((c) => c.revalidate)).toEqual([0, 0, 60, 60]);
  });

  it('the product detail is not cached: its signed photo URLs expire (2026-10-16)', async () => {
    const calls: Array<{ url: string; revalidate: unknown }> = [];
    const spy = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      calls.push({
        url: String(input),
        revalidate: (init as { next?: { revalidate?: number } } | undefined)?.next?.revalidate,
      });
      return Response.json({});
    });
    try {
      await getProductDetailServer('kitten');
    } finally {
      spy.mockRestore();
    }
    expect(calls).toEqual([
      { url: expect.stringContaining('/api/shop/products/kitten'), revalidate: 0 },
    ]);
  });

  it('S12: a visitor-supplied lang reaches the API only if it is an enabled locale', async () => {
    const seen: string[] = [];
    server.use(
      http.get('*/api/shop/locales', () =>
        HttpResponse.json({ defaultLocale: 'en', locales: ['en', 'bn'] }),
      ),
      http.get('*/api/shop/products/kitten', ({ request }) => {
        seen.push(new URL(request.url).search);
        return HttpResponse.json({});
      }),
    );
    await getProductDetailServer('kitten', 'bn');
    await getProductDetailServer('kitten', 'zz-random-1');
    await getProductDetailServer('kitten');
    expect(seen).toEqual(['?locale=bn', '', '']);
  });

  it('S12: the review page number is clamped to 1..500', async () => {
    expect([0, -5, 1.9, 42, 9999, NaN, Infinity].map(clampReviewPage)).toEqual([
      1, 1, 1, 42, 500, 1, 1,
    ]);
    const seen: string[] = [];
    server.use(
      http.get('*/api/shop/products/kitten/reviews', ({ request }) => {
        seen.push(new URL(request.url).search);
        return HttpResponse.json({});
      }),
    );
    await getProductReviewsServer('kitten', 99999);
    await getProductReviewsServer('kitten', -3);
    expect(seen).toEqual(['?pageNumber=500', '']);
  });
});
