import { describe, expect, it } from 'vitest';
import { sanitizeNextPath } from '@/lib/validation/nextPath';

describe('sanitizeNextPath — ARCH gate LOW (first pass)', () => {
  it('accepts an exact prefix match', () => {
    expect(sanitizeNextPath('/book', '/book', '/portal/cats')).toBe('/book');
  });

  it('accepts a prefix followed by a /-boundary', () => {
    expect(sanitizeNextPath('/book/appointment', '/book', '/portal/cats')).toBe(
      '/book/appointment',
    );
  });

  it('accepts a prefix followed by a query string', () => {
    expect(sanitizeNextPath('/book?slot=1', '/book', '/portal/cats')).toBe('/book?slot=1');
  });

  it('rejects a longer, unrelated path segment that merely starts with the prefix', () => {
    expect(sanitizeNextPath('/bookXYZ', '/book', '/portal/cats')).toBe('/portal/cats');
  });

  it('rejects a longer, unrelated path segment even when the allowed prefix has a trailing slash', () => {
    expect(sanitizeNextPath('/staffing', '/staff/', '/staff/cats')).toBe('/staff/cats');
  });

  it('still accepts a trailing-slash prefix at an exact match and at a /-boundary', () => {
    expect(sanitizeNextPath('/staff', '/staff/', '/staff/cats')).toBe('/staff');
    expect(sanitizeNextPath('/staff/customers', '/staff/', '/staff/cats')).toBe('/staff/customers');
  });

  it('falls back on a protocol-relative //-prefixed value', () => {
    expect(sanitizeNextPath('//evil.example', ['/portal/', '/book'], '/portal/cats')).toBe(
      '/portal/cats',
    );
  });

  it('falls back on an absolute URL (U-C2 — a ?next=https://evil.example is ignored)', () => {
    expect(sanitizeNextPath('https://evil.example', '/portal/', '/portal/cats')).toBe(
      '/portal/cats',
    );
    expect(sanitizeNextPath('https://evil.example/portal/cats', '/portal/', '/portal/cats')).toBe(
      '/portal/cats',
    );
  });

  it('falls back on a null/undefined/empty next', () => {
    expect(sanitizeNextPath(null, '/portal/', '/portal/cats')).toBe('/portal/cats');
    expect(sanitizeNextPath(undefined, '/portal/', '/portal/cats')).toBe('/portal/cats');
    expect(sanitizeNextPath('', '/portal/', '/portal/cats')).toBe('/portal/cats');
  });

  it('checks every prefix in a list', () => {
    expect(sanitizeNextPath('/book/1', ['/portal/', '/book'], '/portal/cats')).toBe('/book/1');
    expect(sanitizeNextPath('/other', ['/portal/', '/book'], '/portal/cats')).toBe('/portal/cats');
  });
});

describe('sanitizeNextPath — A-APT1 normalisation hardening', () => {
  const fb = '/portal/cats';
  const BS = String.fromCharCode(92);
  it.each([
    ['/book/..//evil'],
    ['/portal/../..//evil'],
    ['/book/%2e%2e//evil'],
    ['/book/%2E%2E/%2e%2e//evil'],
    ['/book/.%2e//evil'],
    ['/book' + BS + 'evil'],
    ['/' + BS + 'evil'],
    ['/book/' + BS + '..' + BS + 'evil'],
    ['/\t/evil'],
    ['/book/\t/evil'],
    ['/book\n/x'],
    ['/book\r/x'],
    ['/book/\u0000x'],
  ])('falls back on %j', (value) => {
    expect(sanitizeNextPath(value, ['/portal/', '/book'], fb)).toBe(fb);
  });

  it('still returns a valid path (with dot-segments that stay inside the area, and a query) unchanged', () => {
    expect(sanitizeNextPath('/book/a/../b?x=1', '/book', fb)).toBe('/book/a/../b?x=1');
    expect(sanitizeNextPath('/portal/cats/1#top', '/portal/', fb)).toBe('/portal/cats/1#top');
  });
});
