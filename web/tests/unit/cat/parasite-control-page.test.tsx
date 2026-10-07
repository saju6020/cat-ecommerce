import { Suspense } from 'react';
import { describe, expect, it, beforeEach } from 'vitest';
import { act, fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import ParasiteControlPage from '@/app/(staff)/staff/cats/[catId]/parasite-control/page';
import {
  catProfilesFixtureIds,
  resetCatProfilesMockData,
} from '@/mocks/handlers/areas/cat-profiles';

function renderWithClient(children: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}>{children}</QueryClientProvider>);
}

beforeEach(() => {
  resetCatProfilesMockData();
});

/**
 * U33 (HO-CAT-002 §15) — A11's "shows its next-due date" half. HO-CAT-001's C10 is amended
 * (owner decision 2026-09-25) so its `201` body carries the server-computed `nextDueOn`; this
 * proves the page renders exactly that value as visible, accessible text once the mutation
 * resolves, not only in the toast — and never recomputes `administeredOn + intervalDays` itself
 * (the ARCHITECTURE gate rejected that duplication of the aggregate rule for the analogous
 * vaccination case, fix `1a4cac4`).
 */
describe('Parasite control page — shows the server-returned next-due date (A11, U33)', () => {
  it('renders "Next due <date>" as visible status text after a successful record', async () => {
    const user = userEvent.setup();
    const catId = catProfilesFixtureIds.catCurrentPlain;

    await act(async () => {
      renderWithClient(
        <Suspense>
          <ParasiteControlPage params={Promise.resolve({ catId })} />
        </Suspense>,
      );
    });

    expect(screen.queryByRole('status')).not.toBeInTheDocument();

    // Each label's accessible name also carries the required-marker "*" text node
    // (`ParasiteTreatmentForm`'s own `<span aria-hidden="true"> *</span>`), so these match by
    // prefix — the same pattern `weight-entry-form.test.tsx` uses for `FieldWithUnit`'s required
    // fields.
    const productInput = await screen.findByLabelText(/^Product/);
    await user.type(productInput, 'Broadline');
    // `type="date"` inputs aren't reliably typeable via simulated keystrokes in jsdom (no native
    // segmented picker); set the value directly, the same way a browser's own date picker would.
    fireEvent.change(screen.getByLabelText(/^Date given/), { target: { value: '2026-09-20' } });
    const intervalInput = screen.getByLabelText(/^Interval \(days\)/);
    await user.clear(intervalInput);
    await user.type(intervalInput, '90');
    await user.type(screen.getByLabelText(/^Administered by/), 'Staff Member');
    await user.click(screen.getByRole('button', { name: 'Record treatment' }));

    const status = await screen.findByRole('status');
    // 2026-09-20 + 90 days = 2026-12-19.
    expect(status).toHaveTextContent('Next due 19 Dec 2026');
  });
});

/**
 * CODE_REVIEW gate HIGH (third pass) — the recorded next-due date is local `useState`, and nothing
 * in `layout.tsx` → `CatProfileDataShell` → `ParasiteControlPage` keys the page by `catId`, so a
 * client-side navigation from one cat's `/parasite-control` tab to another's used to reuse the
 * same component instance and its stale state: cat B's page would render cat A's "Next due"
 * banner until cat B got its own treatment recorded. Proves the fix — the recorded date carries
 * its `catId` and renders only for that cat — by recording for cat A, then re-rendering the same tree for cat B
 * without an intervening unmount (exactly what an App Router layout does on a same-segment route
 * change), and asserting the banner is gone.
 */
describe('Parasite control page — resets the banner when catId changes (CODE_REVIEW HIGH, third pass)', () => {
  it("never shows cat A's next-due date on cat B's page", async () => {
    const user = userEvent.setup();
    const catIdA = catProfilesFixtureIds.catCurrentPlain;
    const catIdB = catProfilesFixtureIds.catNoAllergiesEstimatedDob;
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    function renderFor(catId: string) {
      return (
        <QueryClientProvider client={queryClient}>
          <Suspense>
            <ParasiteControlPage params={Promise.resolve({ catId })} />
          </Suspense>
        </QueryClientProvider>
      );
    }

    let renderResult!: ReturnType<typeof render>;
    await act(async () => {
      renderResult = render(renderFor(catIdA));
    });

    await screen.findByLabelText(/^Product/);
    await user.type(screen.getByLabelText(/^Product/), 'Broadline');
    fireEvent.change(screen.getByLabelText(/^Date given/), { target: { value: '2026-09-20' } });
    const intervalInput = screen.getByLabelText(/^Interval \(days\)/);
    await user.clear(intervalInput);
    await user.type(intervalInput, '90');
    await user.type(screen.getByLabelText(/^Administered by/), 'Staff Member');
    await user.click(screen.getByRole('button', { name: 'Record treatment' }));

    await screen.findByRole('status');

    // A same-segment client-side navigation to cat B: the same tree re-renders with a new catId,
    // never unmounting ParasiteControlPage.
    await act(async () => {
      renderResult.rerender(renderFor(catIdB));
    });

    // Wait for cat B's own data to actually load (proves this isn't just an empty intermediate
    // render), then assert cat A's banner did not survive onto cat B's page.
    await screen.findByLabelText(/^Product/);
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });
});
