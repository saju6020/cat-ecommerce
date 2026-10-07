import { describe, expect, it } from 'vitest';
import { isSafeLink } from '@/lib/shop/helpers';

describe('isSafeLink — S5', () => {
  it.each(['/shop', '/shop/products?x=1', '/', 'https://example.com/a'])('accepts %s', (url) => {
    expect(isSafeLink(url)).toBe(true);
  });

  it.each([
    ['backslash after the slash', '/\\phish.example'],
    ['tab between the slashes', '/\t/phish.example'],
    ['newline in the path', '/\n/phish.example'],
    ['carriage return in the path', '/\r/phish.example'],
    ['protocol-relative', '//phish.example'],
    ['http', 'http://example.com'],
    ['javascript', 'javascript:alert(1)'],
    ['empty', ''],
    ['null', null],
  ])('rejects %s', (_label, url) => {
    expect(isSafeLink(url)).toBe(false);
  });
});
