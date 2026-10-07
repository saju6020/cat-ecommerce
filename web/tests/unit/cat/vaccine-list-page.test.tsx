import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import VaccineListRoute from '@/app/(staff)/staff/vaccines/page';
import { resetCatProfilesMockData } from '@/mocks/handlers/areas/cat-profiles';

let search = '';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  usePathname: () => '/staff/vaccines',
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
 * CODE_REVIEW gate MEDIUM finding on HO-CAT-002 — `VaccineListPage` called `useVaccines({
 * pageSize: 20 })` with no `pageNumber`, so it always fetched page 1 regardless of the URL's
 * `?page=` — "Next" changed the URL and re-rendered the same rows. This proves `?page=2` fetches
 * different rows, the way `cat-pagination.spec.ts` (E22) proves it for the cat list.
 */
describe('Vaccine catalogue list page — pagination', () => {
  it('fetches different rows for page 1 and page 2', async () => {
    search = '';
    const { unmount } = renderWithClient(<VaccineListRoute />);
    await waitFor(() => expect(screen.getByText('Rabies')).toBeInTheDocument());
    expect(screen.queryByText('Zz Pagination Vaccine 20')).not.toBeInTheDocument();
    unmount();

    search = 'page=2';
    renderWithClient(<VaccineListRoute />);
    await waitFor(() => expect(screen.getByText('Zz Pagination Vaccine 20')).toBeInTheDocument());
    expect(screen.queryByText('Rabies')).not.toBeInTheDocument();
  });
});
