import { describe, expect, it } from 'vitest';
import { renderHook } from '@testing-library/react';
import { ApiConflictError, ApiError, ApiForbiddenError, ApiValidationError } from '@/lib/api/client';
import { shouldRenewIdempotencyKey } from '@/lib/api/errors';
import { useIdempotencyKey } from '@/lib/hooks/useIdempotencyKey';
import { dayEndInstant, dayStartInstant } from '@/lib/format/orders';

const validation = (code: string) =>
  new ApiValidationError({ errors: [{ propertyName: 'X', errorCode: code, errorMessage: 'm', resourceName: 'Ordering' }], isValid: false });

describe('shouldRenewIdempotencyKey - the API replays every response below 500 for a key', () => {
  it('renews after a definitive refusal', () => {
    expect(shouldRenewIdempotencyKey(validation('PolicyAcceptanceRequired'))).toBe(true);
    expect(shouldRenewIdempotencyKey(new ApiError('reused', 422))).toBe(true);
    expect(shouldRenewIdempotencyKey(new ApiForbiddenError({} as never))).toBe(true);
  });

  it('keeps the key when no definitive answer arrived', () => {
    expect(shouldRenewIdempotencyKey(new Error('network down'))).toBe(false);
    expect(shouldRenewIdempotencyKey(new ApiError('boom', 500))).toBe(false);
    expect(shouldRenewIdempotencyKey(new ApiError('bad gateway', 502))).toBe(false);
  });

  it('keeps the key on 409 RequestInProgress (the same key replays the first attempt)', () => {
    expect(shouldRenewIdempotencyKey(new ApiConflictError({ errors: [{ propertyName: 'k', errorCode: 'RequestInProgress', errorMessage: 'm', resourceName: 'Ordering' }], isValid: false }))).toBe(false);
  });
});

describe('useIdempotencyKey.settleFailure', () => {
  it('hands out a new key after a refusal and the same key after a network error', () => {
    const { result } = renderHook(() => useIdempotencyKey());
    const first = result.current.keyFor('body');
    expect(result.current.keyFor('body')).toBe(first);
    result.current.settleFailure(new Error('network'));
    expect(result.current.keyFor('body')).toBe(first);
    result.current.settleFailure(validation('StockNoLongerAvailable'));
    expect(result.current.keyFor('body')).not.toBe(first);
  });
});

describe('calendar-day filter -> instants in the branch zone', () => {
  it('Asia/Dhaka (UTC+6): the from-day starts 18:00Z the day before, the to-day ends 17:59:59.999Z', () => {
    expect(dayStartInstant('2026-10-12', 'Asia/Dhaka')).toBe('2026-10-11T18:00:00.000Z');
    expect(dayEndInstant('2026-10-12', 'Asia/Dhaka')).toBe('2026-10-12T17:59:59.999Z');
  });

  it('rolls over a month end', () => {
    expect(dayEndInstant('2026-10-31', 'Asia/Dhaka')).toBe('2026-10-31T17:59:59.999Z');
    expect(dayStartInstant('2026-11-01', 'Asia/Dhaka')).toBe('2026-10-31T18:00:00.000Z');
  });

  it('a zone behind UTC moves the other way', () => {
    expect(dayStartInstant('2026-01-15', 'America/New_York')).toBe('2026-01-15T05:00:00.000Z');
  });
});
