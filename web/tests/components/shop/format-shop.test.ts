import { describe, expect, it } from 'vitest';
import {
  countryFlag,
  formatBestBefore,
  formatPriceRange,
  formatRating,
  parseMoneyForCompare,
  taxLabel,
} from '@/lib/format/shop';

describe('lib/format/shop — HO-SHP-002 §8.5, ADR-FE-036', () => {
  it('formats a price and "From" when variants differ', () => {
    const price = { amount: '1200.00', currencyCode: 'BDT' };
    expect(formatPriceRange(price, false)).toMatch(/^৳1,200\.00$/);
    expect(formatPriceRange(price, true)).toMatch(/^From ৳1,200\.00$/);
    expect(formatPriceRange(null, false)).toBe('Price not available');
  });

  it('labels tax by pricesIncludeTax', () => {
    expect(taxLabel(true)).toBe('incl. VAT');
    expect(taxLabel(false)).toBe('+ VAT');
  });

  it('formats ratings as text', () => {
    expect(formatRating(4.5, 23)).toBe('4.5 out of 5 (23 reviews)');
    expect(formatRating(5, 1)).toBe('5 out of 5 (1 review)');
    expect(formatRating(null, 0)).toBe('No reviews yet');
  });

  it('formats a best-before date without a time-zone shift', () => {
    expect(formatBestBefore('2026-11-12')).toBe('12 Nov 2026');
    expect(formatBestBefore('2026-01-01')).toBe('1 Jan 2026');
  });

  it('finds flags only for the countries that have one drawn', () => {
    expect(countryFlag('TH')).toBe('/flags/th.svg');
    expect(countryFlag('de')).toBe('/flags/de.svg');
    // 2026-10-16: every origin the AmarPet import brought has a flag (Turkey, China, India, ...).
    for (const code of ['TR', 'CN', 'IN', 'BD', 'FR', 'AT', 'BE', 'MY', 'JP'])
      expect(countryFlag(code)).toBe(`/flags/${code.toLowerCase()}.svg`);
    expect(countryFlag('BR')).toBeNull();
  });

  it('compares decimals only for the price filter', () => {
    expect(parseMoneyForCompare('500')).toBeLessThan(parseMoneyForCompare('1200.50'));
    expect(Number.isNaN(parseMoneyForCompare('abc'))).toBe(true);
  });
});
