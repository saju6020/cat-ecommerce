import { describe, expect, it } from 'vitest';
import {
  activeFilterCount,
  clearFilters,
  parseCatalogueParams,
  toApiSearchParams,
  toQueryString,
  toSearchParams,
  toggleValue,
  withFilterChange,
} from '@/lib/shop/catalogueParams';

describe('catalogueParams — HO-SHP-002 §8.4, ADR-FE-031', () => {
  it('U-A6: round-trips, drops unknown params and keeps repeated brand values in order', () => {
    const input = new URLSearchParams(
      'brand=Whiskas&brand=Me-O&utm_source=mail&origin=TH&sort=price_asc&q=salmon&bogus=1&brand=Whiskas',
    );
    const parsed = parseCatalogueParams(input);
    expect(parsed.brand).toEqual(['Whiskas', 'Me-O']);
    expect(parsed.origin).toEqual(['TH']);
    expect(parsed.sort).toBe('price_asc');
    expect(toSearchParams(parsed).toString()).toBe(
      'q=salmon&brand=Whiskas&brand=Me-O&origin=TH&sort=price_asc',
    );
    expect(toSearchParams(parseCatalogueParams(toSearchParams(parsed))).toString()).toBe(
      toSearchParams(parsed).toString(),
    );
  });

  it('reads Next 15 searchParams objects (string or string[])', () => {
    const parsed = parseCatalogueParams({ brand: ['A', 'B'], origin: 'TH', pageNumber: '3' });
    expect(parsed.brand).toEqual(['A', 'B']);
    expect(parsed.origin).toEqual(['TH']);
    expect(parsed.pageNumber).toBe(3);
  });

  it('rejects invalid sort, rating, page and price values', () => {
    const parsed = parseCatalogueParams(
      new URLSearchParams('sort=hax&minRating=9&pageNumber=-2&minPrice=abc&maxPrice=12.5'),
    );
    expect(parsed.sort).toBeNull();
    expect(parsed.minRating).toBeNull();
    expect(parsed.pageNumber).toBe(1);
    expect(parsed.minPrice).toBe('');
    expect(parsed.maxPrice).toBe('12.5');
  });

  it('U-A1: a filter change resets the page; sorting keeps every filter', () => {
    const start = parseCatalogueParams(new URLSearchParams('pageNumber=4&sort=newest'));
    const withOrigin = withFilterChange(start, { origin: ['TH'] });
    expect(withOrigin.pageNumber).toBe(1);
    const withBrand = withFilterChange(withOrigin, {
      brand: toggleValue(withOrigin.brand, 'Whiskas'),
    });
    const sorted = { ...withBrand, sort: 'price_desc' as const };
    expect(toQueryString(sorted)).toBe('?brand=Whiskas&origin=TH&sort=price_desc');
  });

  it('maps lang to the API locale and counts active filters', () => {
    const parsed = parseCatalogueParams(
      new URLSearchParams('lang=bn&brand=A&minPrice=100&availability=inStock&q=x&pageNumber=2'),
    );
    expect(toApiSearchParams(parsed).get('locale')).toBe('bn');
    expect(toApiSearchParams(parsed).get('lang')).toBeNull();
    expect(toApiSearchParams(parsed).get('pageNumber')).toBe('2');
    expect(activeFilterCount(parsed)).toBe(3);
  });

  it('clear all keeps the search text, sort and language', () => {
    const parsed = parseCatalogueParams(
      new URLSearchParams('q=salmon&brand=A&origin=TH&sort=rating&lang=bn&pageNumber=3'),
    );
    const cleared = clearFilters(parsed);
    expect(toSearchParams(cleared).toString()).toBe('q=salmon&sort=rating&lang=bn');
  });
});
