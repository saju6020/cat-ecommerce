import { describe, expect, it } from 'vitest';
import {
  buildAddCatHref,
  resolveAddCatReturn,
  safeBookingPath,
} from '@/lib/validation/bookingReturnPath';

const CAT = '11111111-1111-1111-1111-111111111111';
const CAT_PAGE = `/portal/cats/${CAT}`;

describe('resolveAddCatReturn — open-redirect guard', () => {
  it.each([
    '/book/..//evil',
    '/book/%2e%2e//evil',
    '/book/.%2e//evil',
    '/book/./..//evil',
    '/book/..' + String.fromCharCode(92) + '/evil',
    '/book/../portal/account',
    '//evil.example/book',
    'https://evil.example/book',
    '/bookXYZ',
  ])('falls back to the cat page for %s', (payload) => {
    expect(safeBookingPath(payload)).toBeNull();
    expect(resolveAddCatReturn(payload, CAT)).toBe(CAT_PAGE);
  });

  it('falls back when next is missing', () => {
    expect(resolveAddCatReturn(null, CAT)).toBe(CAT_PAGE);
  });

  it('returns to /book with the booking parameters and the new cat', () => {
    expect(resolveAddCatReturn('/book?category=Vaccination', CAT)).toBe(
      `/book?category=Vaccination&catId=${CAT}`,
    );
  });

  it('replaces an old catId', () => {
    expect(resolveAddCatReturn('/book?catId=old', CAT)).toBe(`/book?catId=${CAT}`);
  });

  it('accepts a plain /book', () => {
    expect(resolveAddCatReturn('/book', CAT)).toBe(`/book?catId=${CAT}`);
  });
});

describe('buildAddCatHref', () => {
  it('carries every booking parameter in next and round-trips through the return path', () => {
    const query = 'category=Vaccination&serviceId=s1&branchId=b1&date=2026-10-07&performerId=p1';
    const href = buildAddCatHref(query);
    const next = new URL(href, 'http://local').searchParams.get('next');
    expect(next).toBe(`/book?${query}`);
    expect(resolveAddCatReturn(next, CAT)).toBe(`/book?${query}&catId=${CAT}`);
  });

  it('uses plain /book when there are no parameters', () => {
    expect(buildAddCatHref('')).toBe('/portal/cats/new?next=%2Fbook');
  });
});
