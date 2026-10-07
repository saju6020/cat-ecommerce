import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { useUrlFilterState } from '@/lib/hooks/useUrlFilterState';

/**
 * TEST gate HIGH, second pass, 2026-09-28 (U-G6): the content list's `kind`/`status` `<select>`s
 * used to read `value={kind ?? ''}` straight from `useSearchParams()`; two quick, back-to-back
 * changes (no pause between them) silently lost the first — the second edit was computed from the
 * still-stale URL, before the first edit's async navigation had committed. This harness mirrors
 * `ContentListContent`'s own filter wiring (`app/(staff)/staff/admin/content/page.tsx`) — the real
 * `useUrlFilterState` hook driving real `<select>` elements — to prove the fix without importing
 * from `app/` (outside this handover's `tests/components/content/**` scope).
 *
 * Third pass: `useUrlFilterState`'s resync now tracks every one of its own pending writes (a list),
 * not a single "last write" string — see `useFilterUrlUpdate`'s own doc comment for why. Neither
 * field here is debounced (`kind`/`status` are selects, not free text), so this file exercises that
 * list directly against *immediate* writes, one edit per `setValue` call.
 */

const push = vi.fn();
const replace = vi.fn();
let currentSearch = '';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push, replace }),
  usePathname: () => '/staff/admin/content',
  useSearchParams: () => new URLSearchParams(currentSearch),
}));

const CONTENT_FIELDS = ['kind', 'status'] as const;

function Harness() {
  const { values, setValue } = useUrlFilterState(CONTENT_FIELDS);
  return (
    <div>
      <label htmlFor="content-kind-filter">Kind</label>
      <select
        id="content-kind-filter"
        value={values.kind}
        onChange={(e) => setValue('kind', e.target.value)}
      >
        <option value="">All</option>
        <option value="Page">Page</option>
        <option value="Faq">FAQ</option>
        <option value="Banner">Banner</option>
      </select>
      <label htmlFor="content-status-filter">Status</label>
      <select
        id="content-status-filter"
        value={values.status}
        onChange={(e) => setValue('status', e.target.value)}
      >
        <option value="">All</option>
        <option value="Draft">Draft</option>
        <option value="Published">Published</option>
        <option value="Unpublished">Unpublished</option>
      </select>
    </div>
  );
}

describe('Content-list filter selects stay correct under the URL-commit race — TEST gate HIGH (U-G6)', () => {
  beforeEach(() => {
    push.mockClear();
    replace.mockClear();
    currentSearch = '';
  });

  it('two quick, back-to-back select changes are both kept and both end up in the URL', () => {
    render(<Harness />);

    fireEvent.change(screen.getByLabelText('Kind'), { target: { value: 'Faq' } });
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'Draft' } });

    expect(screen.getByLabelText('Kind')).toHaveValue('Faq');
    expect(screen.getByLabelText('Status')).toHaveValue('Draft');
    expect(push).toHaveBeenCalledTimes(2);
    const lastUrl = push.mock.calls.at(-1)?.[0] as string;
    expect(lastUrl).toContain('kind=Faq');
    expect(lastUrl).toContain('status=Draft');
  });

  it('an external URL change (Back, Forward, a deep link) updates both visible selects', () => {
    const { rerender } = render(<Harness />);

    fireEvent.change(screen.getByLabelText('Kind'), { target: { value: 'Faq' } });
    // Let the first edit's own write actually settle (the mocked router "applies" it) before the
    // second, distinct edit — the same real-world separation `admin-filters.spec.ts`'s own
    // Back-twice case needs a settle gap for (Next's router can otherwise coalesce two close
    // pushes into one History entry, real browser, confirmed there). Without this, `kind=Faq`
    // would still be sitting in this hook's own pending-writes list when Back is simulated below,
    // making that specific query string genuinely ambiguous between "my own edit, still catching
    // up" and "Back landed here" — precisely the ambiguity `useFilterUrlUpdate`'s own doc comment
    // resolves by requiring a write to be either confirmed or superseded before treating that exact
    // query as available again.
    currentSearch = 'kind=Faq';
    rerender(<Harness />);
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'Draft' } });
    // …and let the second edit's write settle too, the same way — otherwise the Back press below
    // would be simulated by setting `currentSearch` back to a string it is *already* set to
    // (`'kind=Faq'`, unchanged since two renders ago), which is not a real change React's effect
    // dependency would ever see fire — a test-harness limitation, not a product one: a real
    // browser's `popstate` always fires as a genuine event, never a no-op re-render.
    currentSearch = 'kind=Faq&status=Draft';
    rerender(<Harness />);

    // A Back press restores the state after the first edit only (§8.3, U-G4/U-G5's own back-button
    // behaviour) — here simulated directly as the URL the browser restores, and the render that
    // follows any navigation. `kind=Faq` is no longer a pending write (settled above), so this is
    // unambiguously external.
    currentSearch = 'kind=Faq';
    rerender(<Harness />);

    expect(screen.getByLabelText('Kind')).toHaveValue('Faq');
    expect(screen.getByLabelText('Status')).toHaveValue('');
  });

  it('a deep link pre-fills both selects', () => {
    currentSearch = 'kind=Banner&status=Published';
    render(<Harness />);

    expect(screen.getByLabelText('Kind')).toHaveValue('Banner');
    expect(screen.getByLabelText('Status')).toHaveValue('Published');
  });

  it("TEST gate HIGH (third pass): an intermediate value from one of this hook's own earlier, already-superseded writes landing out of step never regresses local state", () => {
    const { rerender } = render(<Harness />);
    const kindSelect = screen.getByLabelText('Kind');

    // Three rapid edits to the same field, none of them observed by the mocked router in between
    // (it never applies any of them — `currentSearch` stays untouched) — the exact shape of the
    // race the third-pass TEST gate reproduced: several of this hook's own writes are in flight at
    // once, oldest first: "kind=Page", "kind=Faq", "kind=Banner".
    fireEvent.change(kindSelect, { target: { value: 'Page' } });
    fireEvent.change(kindSelect, { target: { value: 'Faq' } });
    fireEvent.change(kindSelect, { target: { value: 'Banner' } });

    // The mocked router's own `searchParams` now reports the *middle* one of those three writes —
    // an intermediate, already-superseded value of this hook's own, landing well after the local
    // state moved on past it. The second pass's single-"last write"-string comparison would see
    // "kind=Faq" not matching its own single expected "kind=Banner" and wrongly treat this as an
    // external navigation, regressing the field back to "Faq". The third-pass fix keeps every
    // pending write, so "kind=Faq" is still recognised as this hook's own second write, and both it
    // and the first ("kind=Page") are dropped without ever touching local state.
    currentSearch = 'kind=Faq';
    rerender(<Harness />);

    expect(kindSelect).toHaveValue('Banner');

    // The final write ("kind=Banner") then genuinely lands too — still no regression.
    currentSearch = 'kind=Banner';
    rerender(<Harness />);
    expect(kindSelect).toHaveValue('Banner');
  });
});
