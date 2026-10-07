import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { act } from 'react';
import { AuditLogSearch } from '@/components/audit/AuditLogSearch';
import { useUrlFilterState, FILTER_WRITE_DEBOUNCE_MS } from '@/lib/hooks/useUrlFilterState';

/**
 * TEST gate HIGH, second pass, 2026-09-28 (U-G6): `AuditLogSearch`'s text inputs used to be
 * `value={filters[field] ?? ''}`, sourced purely from `useSearchParams()`. Every edit becomes an
 * async client-side navigation, so a re-render happening before that navigation commits reset the
 * field to the stale URL value, corrupting fast typing and silently dropping quick successive
 * edits. This file exercises the fix — `useUrlFilterState` (local state as the display source of
 * truth) wired the same way `app/(staff)/staff/admin/audit-log/page.tsx` wires it — through the
 * real `AuditLogSearch` component, not just the underlying hook in isolation.
 *
 * Third pass, same date: the second pass's fix (a single "last write" string) could still mistake
 * an intermediate, already-superseded write of its own for an external navigation and silently
 * regress local state by one character — non-deterministic, ordinary typing speed. Fixed by
 * tracking every pending write (a list) and by debouncing free-text fields' URL writes
 * (`DEBOUNCED_FIELDS` below, mirroring the page's own `DEBOUNCED_FILTER_FIELDS`), flushed
 * immediately on blur/Enter (`AuditLogSearch`'s own `onFilterCommit`).
 */

const push = vi.fn();
const replace = vi.fn();
let currentSearch = '';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push, replace }),
  usePathname: () => '/staff/admin/audit-log',
  useSearchParams: () => new URLSearchParams(currentSearch),
}));

const AUDIT_FIELDS = [
  'actorUserId',
  'entityType',
  'entityId',
  'action',
  'category',
  'from',
  'to',
] as const;

const DEBOUNCED_FIELDS = ['actorUserId', 'entityType', 'entityId', 'action', 'category'] as const;

/** Mirrors `AuditLogContent`'s own wiring — real `AuditLogSearch`, real `useUrlFilterState`. */
function Harness() {
  const { values, setValue, flush } = useUrlFilterState(
    AUDIT_FIELDS,
    { from: '2027-01-01', to: '2027-01-07' },
    DEBOUNCED_FIELDS,
  );
  return (
    <AuditLogSearch
      filters={values}
      onFilterChange={setValue}
      onFilterCommit={flush}
      entries={[]}
      totalCount={0}
      pageSize={50}
      isLoading={false}
      isError={false}
      onRetry={vi.fn()}
      exportUrl="https://api.example.com/export.csv"
      timeZoneId="Asia/Dhaka"
    />
  );
}

describe('Audit-log filter fields stay correct under the URL-commit race — TEST gate HIGH (U-G6)', () => {
  beforeEach(() => {
    push.mockClear();
    replace.mockClear();
    currentSearch = '';
  });

  it('keeps every keystroke locally with no URL write yet, and blur flushes the full value immediately', () => {
    const { rerender } = render(<Harness />);
    const input = screen.getByLabelText('action');

    const target = 'FirstValue';
    let typedSoFar = '';
    for (const nextChar of target) {
      typedSoFar += nextChar;
      fireEvent.change(input, { target: { value: typedSoFar } });
      // Force a re-render "for some unrelated reason" (a sibling query resolving, etc.) while
      // `currentSearch` — what the mocked router "has applied" — never actually changes, exactly
      // the state a real browser is in until a `router.push`/`replace` navigation commits.
      rerender(<Harness />);
    }
    expect(input).toHaveValue('FirstValue');
    // `action` is debounced — no write has happened yet, on purpose (no keystroke and no timer
    // advance triggered one).
    expect(push).not.toHaveBeenCalled();
    expect(replace).not.toHaveBeenCalled();

    // Leaving the field (blur) flushes the debounced write immediately with the full typed value.
    fireEvent.blur(input);
    const calls = [...push.mock.calls, ...replace.mock.calls];
    expect(calls).toHaveLength(1);
    expect(calls.at(-1)?.[0]).toContain('action=FirstValue');
  });

  it('keystrokes typed into a second field right after the first are both kept, and both land in the URL once each is flushed', () => {
    render(<Harness />);
    const actionInput = screen.getByLabelText('action');
    const entityIdInput = screen.getByLabelText('entityId');

    fireEvent.change(actionInput, { target: { value: 'S' } });
    fireEvent.change(actionInput, { target: { value: 'Se' } });
    fireEvent.change(actionInput, { target: { value: 'Set' } });
    // Leaving `action` for `entityId` — a real user tabbing between fields — flushes `action`'s
    // debounced write immediately, exactly like the blur case above.
    fireEvent.blur(actionInput);
    fireEvent.change(entityIdInput, { target: { value: 'svc' } });
    fireEvent.blur(entityIdInput);

    expect(actionInput).toHaveValue('Set');
    expect(entityIdInput).toHaveValue('svc');
    // Each is a fresh field relative to the other → both `push`, merged onto the same URL.
    expect(push).toHaveBeenCalledTimes(2);
    expect(replace).not.toHaveBeenCalled();
    const lastUrl = push.mock.calls.at(-1)?.[0] as string;
    expect(lastUrl).toContain('action=Set');
    expect(lastUrl).toContain('entityId=svc');
  });

  it('an external URL change (Back, Forward, a deep link) updates the visible field, discarding an uncommitted debounced edit', () => {
    const { rerender } = render(<Harness />);
    const input = screen.getByLabelText('action');

    fireEvent.change(input, { target: { value: 'Typed' } });
    expect(input).toHaveValue('Typed');

    // Simulate the browser restoring an earlier history entry (a real Back/Forward press) — the
    // URL changes to something this hook never itself wrote — then the render that follows any
    // navigation. `action`'s debounced write never fired (no blur, no timer advance), so there is
    // nothing of this hook's own for the new URL to be confused with.
    currentSearch = 'action=RestoredByBack';
    rerender(<Harness />);

    expect(screen.getByLabelText('action')).toHaveValue('RestoredByBack');
  });

  describe("TEST gate HIGH (third pass) — rapid edits, an intermediate value from one of this hook's own earlier, already-superseded writes landing out of step", () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it('never regresses local state', () => {
      const { rerender } = render(<Harness />);
      const input = screen.getByLabelText('action');

      // Four edits, each separated by more than the debounce window so each is its own write —
      // the shape of a real 150ms/key typing burst once debounced: not every keystroke writes, but
      // several writes for the *same* field can still be in flight at once relative to however long
      // the router actually takes to commit each one (the third-pass TEST gate's own measured lag).
      fireEvent.change(input, { target: { value: 'F' } });
      act(() => {
        vi.advanceTimersByTime(FILTER_WRITE_DEBOUNCE_MS + 10);
      });
      fireEvent.change(input, { target: { value: 'Fi' } });
      act(() => {
        vi.advanceTimersByTime(FILTER_WRITE_DEBOUNCE_MS + 10);
      });
      fireEvent.change(input, { target: { value: 'Fir' } });
      act(() => {
        vi.advanceTimersByTime(FILTER_WRITE_DEBOUNCE_MS + 10);
      });
      fireEvent.change(input, { target: { value: 'First' } });
      act(() => {
        vi.advanceTimersByTime(FILTER_WRITE_DEBOUNCE_MS + 10);
      });

      // Four writes are now pending confirmation, oldest first: "action=F", "action=Fi",
      // "action=Fir", "action=First". The mocked router's own `searchParams` now reports the
      // *third* one — an intermediate, already-superseded value of this hook's own — landing well
      // after "First" was both typed and written. The second pass's single-"last write"-string
      // comparison would see "action=Fir" not matching its own single expected "action=First" and
      // wrongly treat this as an external navigation, regressing the field to "Fir" — exactly the
      // single-character-loss bug the third-pass TEST gate reproduced, non-deterministically, at
      // ordinary typing speed. The fix keeps every pending write, so "action=Fir" is still
      // recognised as this hook's own third write, and it and everything before it are dropped
      // without ever touching local state.
      currentSearch = 'action=Fir';
      rerender(<Harness />);

      expect(input).toHaveValue('First');

      // The final write ("action=First") then genuinely lands too — still no regression.
      currentSearch = 'action=First';
      rerender(<Harness />);
      expect(input).toHaveValue('First');
    });
  });

  describe('ADM 22 / 33 / 36', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });
    afterEach(() => {
      vi.useRealTimers();
    });

    it('ADM 22: Back/Forward cancels a pending debounced write — it never pushes the abandoned value', () => {
      const { rerender } = render(<Harness />);
      const input = screen.getByLabelText('action');
      fireEvent.change(input, { target: { value: 'Typed' } });

      currentSearch = 'action=RestoredByBack'; // native Back within the debounce window
      rerender(<Harness />);
      expect(screen.getByLabelText('action')).toHaveValue('RestoredByBack');

      act(() => {
        vi.advanceTimersByTime(FILTER_WRITE_DEBOUNCE_MS * 3);
      });
      expect(push).not.toHaveBeenCalled();
      expect(replace).not.toHaveBeenCalled();
      expect(screen.getByLabelText('action')).toHaveValue('RestoredByBack');
    });

    it('ADM 33: typing a character and deleting it within the debounce window writes nothing', () => {
      currentSearch = 'action=x';
      render(<Harness />);
      const input = screen.getByLabelText('action');
      fireEvent.change(input, { target: { value: 'xy' } });
      fireEvent.change(input, { target: { value: 'x' } });
      act(() => {
        vi.advanceTimersByTime(FILTER_WRITE_DEBOUNCE_MS + 10);
      });
      expect(push).not.toHaveBeenCalled();
      expect(replace).not.toHaveBeenCalled();
    });

    it('ADM 36: blur writes the value captured with the pending timer', () => {
      render(<Harness />);
      const input = screen.getByLabelText('action');
      fireEvent.change(input, { target: { value: 'Final' } });
      fireEvent.blur(input);
      expect([...push.mock.calls, ...replace.mock.calls].at(-1)?.[0]).toContain('action=Final');
    });
  });
});
