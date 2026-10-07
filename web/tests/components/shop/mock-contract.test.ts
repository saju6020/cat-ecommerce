import { describe, expect, it } from 'vitest';
import type { CatalogueResult } from '@/lib/api/shop-catalogue';

/** The shop mock is generated from HO-SHP-001 §12.4 — spot-check the shapes the UI relies on. */
describe('shop mock contract — HO-SHP-002 §12', () => {
  const base = process.env.NEXT_PUBLIC_API_BASE_URL;

  it('answers the catalogue with the real wire shape (images always url: null)', async () => {
    const body = (await (await fetch(`${base}/api/shop/products`)).json()) as CatalogueResult;
    expect(body.totalCount).toBeGreaterThanOrEqual(40);
    expect(body.pageSize).toBe(24);
    expect(body.items.every((i) => i.primaryImage?.url === null)).toBe(true);
    expect(body.appliedFilters.sort).toBe('Newest');
  });

  it('searches with typos and answers a zero-result search with suggestions', async () => {
    const typo = (await (await fetch(`${base}/api/shop/products?q=royal%20canan`)).json()) as {
      totalCount: number;
    };
    expect(typo.totalCount).toBeGreaterThan(0);
    const none = (await (await fetch(`${base}/api/shop/products?q=zzqqxx`)).json()) as {
      totalCount: number;
      suggestions: { popular: unknown[] };
    };
    expect(none.totalCount).toBe(0);
    expect(none.suggestions.popular).toHaveLength(8);
  });
});
