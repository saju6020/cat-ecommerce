import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import CatListRoute from '@/app/(staff)/staff/cats/page';
import { resetCatProfilesMockData } from '@/mocks/handlers/areas/cat-profiles';

let search = '';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  usePathname: () => '/staff/cats',
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

/** U32 — every list renders each of loading, empty and error. */
describe('Cat list page', () => {
  it('renders the loading skeleton, then the loaded rows', async () => {
    renderWithClient(<CatListRoute />);
    expect(screen.getByRole('status')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText('Whiskers')).toBeInTheDocument());
  });

  it('renders ErrorState when the request fails', async () => {
    server.use(http.get('*/api/cats', () => HttpResponse.json({ error: 'boom' }, { status: 500 })));
    renderWithClient(<CatListRoute />);
    expect(await screen.findByRole('button', { name: /try again/i })).toBeInTheDocument();
  });

  it('renders EmptyState when there are no living cats', async () => {
    server.use(
      http.get('*/api/cats', () =>
        HttpResponse.json({ items: [], pageNumber: 1, pageSize: 10, totalCount: 0 }),
      ),
    );
    renderWithClient(<CatListRoute />);
    expect(await screen.findByText(/no living cats/i)).toBeInTheDocument();
  });

  it('renders a different EmptyState title when including deceased cats finds none', async () => {
    server.use(
      http.get('*/api/cats', () =>
        HttpResponse.json({ items: [], pageNumber: 1, pageSize: 10, totalCount: 0 }),
      ),
    );
    search = 'includeDeceased=true';
    renderWithClient(<CatListRoute />);
    expect(await screen.findByText(/no cats registered yet/i)).toBeInTheDocument();
  });

  /** Owner decision, 2026-09-27 (HO-CAT-002 §1 Amended, supersedes §11.4's compact-only design
   *  for this list): the handling note must be visible in the staff list, not just the badge. */
  it('shows the handling note for a high-risk cat in the list', async () => {
    const handlingNote = 'Bites and scratches when restrained. Two handlers and a muzzle required.';
    server.use(
      http.get('*/api/cats', () =>
        HttpResponse.json({
          items: [
            {
              id: 'cat-high-risk-1',
              ownerId: 'owner-1',
              name: 'Tiger',
              species: 'Cat',
              breed: null,
              sex: 'Male',
              isHighRisk: true,
              handlingNote,
              lifeStatus: 'Alive',
            },
          ],
          pageNumber: 1,
          pageSize: 10,
          totalCount: 1,
        }),
      ),
    );
    renderWithClient(<CatListRoute />);
    expect(await screen.findByText(handlingNote)).toBeInTheDocument();
  });

  it('renders no handling note text for a high-risk cat with none recorded', async () => {
    server.use(
      http.get('*/api/cats', () =>
        HttpResponse.json({
          items: [
            {
              id: 'cat-high-risk-2',
              ownerId: 'owner-1',
              name: 'Panther',
              species: 'Cat',
              breed: null,
              sex: 'Female',
              isHighRisk: true,
              handlingNote: null,
              lifeStatus: 'Alive',
            },
          ],
          pageNumber: 1,
          pageSize: 10,
          totalCount: 1,
        }),
      ),
    );
    renderWithClient(<CatListRoute />);
    const row = await screen.findByRole('row', { name: /Panther/i });
    expect(within(row).getByText('High risk')).toBeInTheDocument();
  });
});
