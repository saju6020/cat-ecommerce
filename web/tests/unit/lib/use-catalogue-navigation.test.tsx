import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useCatalogueNavigation } from '@/lib/hooks/useCatalogueNavigation';

const replace = vi.fn();
let search = '';
vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace, push: vi.fn() }),
  usePathname: () => '/shop/products',
  useSearchParams: () => new URLSearchParams(search),
}));

beforeEach(() => {
  replace.mockClear();
  search = '';
});

describe('useCatalogueNavigation pending value — S15', () => {
  it('is dropped in the same render the URL changes (no stale frame after Back)', () => {
    const seen: boolean[] = [];
    const { result, rerender } = renderHook(() => {
      const nav = useCatalogueNavigation();
      seen.push(nav.params.inStock);
      return nav;
    });

    act(() => result.current.apply({ inStock: true }));
    expect(result.current.params.inStock).toBe(true);

    // Back lands on a URL without the filter. Every render from here must already read it.
    seen.length = 0;
    search = 'sort=newest';
    rerender();
    expect(seen.length).toBeGreaterThan(0);
    expect(seen.every((inStock) => inStock === false)).toBe(true);
    expect(result.current.params.sort).toBe('newest');
  });
});
