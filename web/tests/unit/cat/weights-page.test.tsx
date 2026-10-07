import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, render, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import WeightsPage from '@/app/(staff)/staff/cats/[catId]/weights/page';
import {
  catProfilesFixtureIds,
  resetCatProfilesMockData,
} from '@/mocks/handlers/areas/cat-profiles';

let search = '';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  usePathname: () => '/staff/cats/[catId]/weights',
  useSearchParams: () => new URLSearchParams(search),
}));

function renderWithClient(children: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}>{children}</QueryClientProvider>);
}

beforeEach(() => {
  resetCatProfilesMockData();
  search = '';
});

/**
 * CODE_REVIEW gate LOW finding on HO-CAT-002 (fixed ee59d2a), regression test added at the TEST
 * gate — `getRowId={(point) => point.recordedOn}` used the recorded date alone as the weight
 * history table's row key, so two weights recorded on the same calendar date (e.g. intake and a
 * same-day procedure) would collide. The fix falls back to `${recordedOn}-${index}` since
 * `CatWeightPoint` (HO-CAT-001 §12.3 Q3) carries no id. This proves both same-date rows are
 * rendered distinctly rather than one silently overwriting the other.
 *
 * TEST gate finding — the identical `key={point.recordedOn}` collision was found, unfixed, a
 * second time in `WeightTrendChart.tsx`'s own visually-hidden accessibility table (the one that
 * "actually satisfies UI-6" per that component's doc comment), fixed the same way. It was found
 * *because* this exact two-same-date fixture made React print "Encountered two children with the
 * same key, `2026-06-01`" unprompted while this page — which renders both tables at once — was
 * under test. This suite now asserts both tables render two distinct rows *and* that no such
 * `console.error` warning fires, so a regression in either row key is caught here rather than
 * noticed by chance again.
 */
describe('Weights page — same-date row-key collision (CODE_REVIEW LOW, ee59d2a)', () => {
  it('renders two weight entries recorded on the same date as two distinct rows in both tables, with no duplicate-key warning', async () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});

    const catId = catProfilesFixtureIds.catWithAllergiesExactDob;
    const weightPoints = [
      { recordedOn: '2026-06-01', kilograms: 4.0 },
      { recordedOn: '2026-06-01', kilograms: 4.5 },
    ];
    server.use(
      http.get('*/api/cats/:id/weights', () =>
        HttpResponse.json({
          items: weightPoints,
          pageNumber: 1,
          pageSize: 100,
          totalCount: weightPoints.length,
        }),
      ),
    );

    // `params` is a Promise in Next.js 15 (`use(params)` inside `WeightsPage`), which suspends the
    // first render — real Next.js wraps every route in a Suspense boundary automatically; here
    // that boundary is `WeightsPage`'s own internal `<Suspense>`, but resolving `use(params)`
    // still happens a microtask outside of React-scheduled work, so it needs an explicit `act`.
    let renderResult!: ReturnType<typeof render>;
    await act(async () => {
      renderResult = renderWithClient(<WeightsPage params={Promise.resolve({ catId })} />);
    });
    const { container } = renderResult;

    // Both `DataTable`'s weight-history table and `WeightTrendChart`'s visually-hidden
    // screen-reader table (U22) are captioned "Weight history" with overlapping date/weight
    // text, so scope to each one specifically rather than the page as a whole.
    let historyTable: HTMLTableElement | undefined;
    let hiddenTable: HTMLTableElement | undefined;
    await waitFor(() => {
      const tables = Array.from(container.querySelectorAll('table'));
      historyTable = tables.find((table) => !table.className.includes('sr-only'));
      hiddenTable = tables.find((table) => table.className.includes('sr-only'));
      expect(historyTable).toBeDefined();
      expect(hiddenTable).toBeDefined();
      expect(within(historyTable!).queryByText('4.5 kg')).not.toBeNull();
      expect(within(hiddenTable!).queryByText('4.5 kg')).not.toBeNull();
    });
    if (!historyTable) throw new Error('Visible weight-history table not found');
    if (!hiddenTable) throw new Error('Hidden WeightTrendChart accessibility table not found');

    expect(within(historyTable).getByText('4 kg')).toBeInTheDocument();
    expect(within(historyTable).getByText('4.5 kg')).toBeInTheDocument();

    // Both rows share a date cell — confirms neither entry was dropped/overwritten by a key clash.
    const dateCells = within(historyTable).getAllByText('01 Jun 2026');
    expect(dateCells).toHaveLength(2);

    // Same proof for `WeightTrendChart`'s own hidden table (U22) — both same-date rows present.
    expect(within(hiddenTable).getByText('4 kg')).toBeInTheDocument();
    expect(within(hiddenTable).getByText('4.5 kg')).toBeInTheDocument();
    const hiddenDateCells = within(hiddenTable).getAllByText('01 Jun 2026');
    expect(hiddenDateCells).toHaveLength(2);

    const duplicateKeyWarning = consoleError.mock.calls.find((args) =>
      args.some((arg) => typeof arg === 'string' && arg.includes('same key')),
    );
    expect(duplicateKeyWarning).toBeUndefined();

    consoleError.mockRestore();
  });
});
