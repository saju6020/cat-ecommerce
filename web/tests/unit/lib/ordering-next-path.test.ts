import { describe, expect, it } from 'vitest';
import { sanitizeNextPath } from '@/lib/validation/nextPath';

/** U-A5 (pure half) - the sign-in page's allow-list as HO-ORD-002 ADR-FE-098 amends it. */
const ALLOWED = ['/portal/', '/book', '/shop', '/cart', '/checkout'];
const FALLBACK = '/portal/cats';

describe('sign-in next path with /cart and /checkout allowed (U-A5)', () => {
  it.each([
    '/checkout',
    '/cart',
    '/checkout?step=payment',
    '/cart#undo',
    '/portal/orders',
    '/shop/products/x',
  ])('keeps %s', (path) => {
    expect(sanitizeNextPath(path, ALLOWED, FALLBACK)).toBe(path);
  });

  it.each([
    ['another host', 'https://evil.example/checkout'],
    ['protocol-relative', '//evil.example/checkout'],
    ['a backslash trick', '/checkout\..\evil'],
    ['a prefix lookalike', '/checkout-evil'],
    ['an unlisted staff route', '/staff/orders'],
    ['an empty value', ''],
  ])('falls back for %s', (_label, path) => {
    expect(sanitizeNextPath(path, ALLOWED, FALLBACK)).toBe(FALLBACK);
  });

  it('falls back when there is no next parameter', () => {
    expect(sanitizeNextPath(null, ALLOWED, FALLBACK)).toBe(FALLBACK);
  });

  it('the sign-in page really lists /cart and /checkout', async () => {
    // Issues.md #5: the list moved to lib/routing/portal-links.ts, shared with the register page.
    const { CUSTOMER_RETURN_PREFIXES } = await import('@/lib/routing/portal-links');
    expect(CUSTOMER_RETURN_PREFIXES).toEqual(expect.arrayContaining(['/cart', '/checkout']));
    const { readFileSync } = await import('node:fs');
    const source = readFileSync('app/(auth)/sign-in/page.tsx', 'utf8');
    expect(source).toContain('CUSTOMER_RETURN_PREFIXES');
  });
});
