import { Suspense } from 'react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import EditCatPage from '@/app/(staff)/staff/cats/[catId]/edit/page';
import {
  catProfilesFixtureIds,
  resetCatProfilesMockData,
} from '@/mocks/handlers/areas/cat-profiles';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  usePathname: () => '/staff/cats',
  useSearchParams: () => new URLSearchParams(),
}));

function renderWithClient(children: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  // A route's `params` prop is a Promise in Next.js 15 (app/(staff)/staff/cats/[catId]/edit/
  // page.tsx reads it with `use(params)`), which suspends the first render — real Next.js
  // supplies a Suspense boundary around every route automatically; this test has to supply one
  // itself, or React has nothing to catch the suspend and renders nothing at all.
  return render(
    <QueryClientProvider client={queryClient}>
      <Suspense fallback={<div>Loading…</div>}>{children}</Suspense>
    </QueryClientProvider>,
  );
}

beforeEach(() => resetCatProfilesMockData());

describe('EditCatPage (deceased cat) — U30', () => {
  it('shows the deceased explanation and disables edit controls, rather than hiding them', async () => {
    // `use(params)` resolving is one microtask tick outside of any React-scheduled work; without
    // an explicit `act` around it, React (correctly) warns that the resulting update wasn't
    // flushed inside an act scope, and the Suspense fallback can be left showing indefinitely in
    // a test renderer with no browser event loop driving further ticks.
    await act(async () => {
      renderWithClient(
        <EditCatPage params={Promise.resolve({ catId: catProfilesFixtureIds.catDeceased })} />,
      );
    });

    // "with an explanation" — the banner text, not just a disabled control with no reason.
    expect(
      await screen.findByText(/recorded as deceased/i, {}, { timeout: 3000 }),
    ).toBeInTheDocument();

    // "disabled ... not hidden" — the name field is still rendered, but not editable.
    const nameInput = await screen.findByLabelText(/^Name/);
    expect(nameInput).toBeInTheDocument();
    expect(nameInput).toBeDisabled();
  });
});
