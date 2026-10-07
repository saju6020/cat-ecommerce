import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { FILTER_TYPING_BURST_MS, useFilterUrlUpdate } from '@/lib/hooks/useFilterUrlUpdate';

const push = vi.fn();
const replace = vi.fn();
let currentSearch = '';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push, replace }),
  usePathname: () => '/staff/admin/audit-log',
  useSearchParams: () => new URLSearchParams(currentSearch),
}));

describe('useFilterUrlUpdate — HO-ADM-002 §8.3 back-button-safe filters (U-G4)', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    push.mockClear();
    replace.mockClear();
    currentSearch = 'from=2027-01-01&page=3';
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('a filter change adds a history entry and resets paging', () => {
    const { result } = renderHook(() => useFilterUrlUpdate());

    result.current('action', 'ServicePriceSet');

    expect(push).toHaveBeenCalledExactlyOnceWith(
      '/staff/admin/audit-log?from=2027-01-01&action=ServicePriceSet',
    );
    expect(replace).not.toHaveBeenCalled();
  });

  it('keystrokes in one field within a burst replace their own entry instead of adding one each', () => {
    const { result } = renderHook(() => useFilterUrlUpdate());

    result.current('entityId', 'a');
    vi.advanceTimersByTime(200);
    result.current('entityId', 'ab');
    vi.advanceTimersByTime(200);
    result.current('entityId', 'abc');

    expect(push).toHaveBeenCalledTimes(1);
    expect(replace).toHaveBeenCalledTimes(2);
  });

  it('a different field, or the same field after a pause, adds a new entry', () => {
    const { result } = renderHook(() => useFilterUrlUpdate());

    result.current('entityId', 'abc');
    result.current('action', 'ServicePriceSet');
    vi.advanceTimersByTime(FILTER_TYPING_BURST_MS + 1);
    result.current('action', 'ServicePriceCleared');

    expect(push).toHaveBeenCalledTimes(3);
    expect(replace).not.toHaveBeenCalled();
  });

  it('clearing a filter removes its param', () => {
    currentSearch = 'kind=Page&status=Published';
    const { result } = renderHook(() => useFilterUrlUpdate());

    result.current('kind', '');

    expect(push).toHaveBeenCalledExactlyOnceWith('/staff/admin/audit-log?status=Published');
  });

  it('TEST gate HIGH (third pass): an intermediate value from one of its own earlier, already-superseded writes never triggers a resync', () => {
    currentSearch = '';
    const { result, rerender } = renderHook(() => useFilterUrlUpdate());

    result.current('action', 'F');
    vi.advanceTimersByTime(50);
    result.current('action', 'Fi');
    vi.advanceTimersByTime(50);
    result.current('action', 'Fir');
    vi.advanceTimersByTime(50);
    result.current('action', 'First');

    // Four writes are now pending confirmation, oldest first: "action=F", "action=Fi",
    // "action=Fir", "action=First". The mocked router's own `searchParams` now reports the
    // *third* one — an intermediate, already-superseded write of this hook's own — landing well
    // after "action=First" was both computed and written. A single-"last write"-string comparison
    // (the second pass's own fix) would see "action=Fir" not matching its single expected
    // "action=First" and wrongly treat this as an external navigation, resetting `pendingParams`
    // back to "action=Fir" — losing the "st" a user had already typed. Tracking every pending
    // write instead means "action=Fir" is still recognised as this hook's own third write.
    currentSearch = 'action=Fir';
    rerender();

    // Provable only indirectly (`pendingParams` isn't exposed): the *next* edit must merge onto
    // the latest state ("action=First"), not the stale intermediate one that just "arrived".
    result.current('entityId', 'svc1');
    expect(push).toHaveBeenLastCalledWith('/staff/admin/audit-log?action=First&entityId=svc1');
  });

  it('ADM 33: a write whose query equals the URL neither navigates nor adds history, but still drops page', () => {
    currentSearch = 'action=x';
    const { result } = renderHook(() => useFilterUrlUpdate());
    expect(result.current('action', 'x')).toBe('action=x');
    expect(push).not.toHaveBeenCalled();
    expect(replace).not.toHaveBeenCalled();

    // With a page in the URL the same value is NOT a no-op: it resets paging.
    currentSearch = 'action=x&page=3';
    const second = renderHook(() => useFilterUrlUpdate());
    second.result.current('action', 'x');
    expect(push).toHaveBeenCalledExactlyOnceWith('/staff/admin/audit-log?action=x');
  });
});
