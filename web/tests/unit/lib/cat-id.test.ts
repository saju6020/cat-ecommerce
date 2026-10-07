import { describe, expect, it } from 'vitest';
import { isValidCatId } from '@/lib/validation/catId';

describe('isValidCatId — HO-CAT-002 §1 Amended (2026-09-27), /cats/{id}', () => {
  it('accepts a well-formed GUID', () => {
    expect(isValidCatId('3fa85f64-5717-4562-b3fc-2c963f66afa6')).toBe(true);
  });

  it('accepts a well-formed GUID regardless of letter case', () => {
    expect(isValidCatId('3FA85F64-5717-4562-B3FC-2C963F66AFA6')).toBe(true);
  });

  it('rejects an empty string', () => {
    expect(isValidCatId('')).toBe(false);
  });

  it('rejects a non-GUID word', () => {
    expect(isValidCatId('not-a-guid')).toBe(false);
  });

  it('rejects a GUID missing a segment', () => {
    expect(isValidCatId('3fa85f64-5717-4562-b3fc')).toBe(false);
  });

  it('rejects a GUID with an extra character', () => {
    expect(isValidCatId('3fa85f64-5717-4562-b3fc-2c963f66afa6a')).toBe(false);
  });

  it('rejects a value carrying a path segment (traversal attempt)', () => {
    expect(isValidCatId('../../staff/cats')).toBe(false);
  });

  it('rejects a value with surrounding whitespace', () => {
    expect(isValidCatId(' 3fa85f64-5717-4562-b3fc-2c963f66afa6 ')).toBe(false);
  });
});
