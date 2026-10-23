import { Suspense } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { administrationFixtureIds } from '@/mocks/handlers/admin';
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn(), refresh: vi.fn() }),
  usePathname: () => '/staff/admin/branches',
  useSearchParams: () => new URLSearchParams(),
}));

import BranchRosterPage from '@/app/(staff)/staff/admin/branches/[branchId]/roster/page';

/** A route `params` promise React's `use()` reads synchronously (see detail-pages-403.test.tsx). */
function resolvedParams<T>(value: T): Promise<T> {
  return Object.assign(Promise.resolve(value), { status: 'fulfilled', value }) as Promise<T>;
}

// Owner, 2026-10-22: with no rosters yet, the staff list used to be empty, so nobody could ever be given shifts and
// online booking had no times. Anyone can now be picked and given a first pattern.
describe('Branch roster page — a first shift pattern', () => {
  it('offers a staff member with no roster yet and saves their first pattern', async () => {
    let putUrl: string | null = null;
    server.use(
      http.get('*/api/admin/branches/:branchId/rosters', () => HttpResponse.json([])),
      http.get('*/api/staff-accounts', () =>
        HttpResponse.json({
          items: [
            {
              id: 'vet-1',
              fullName: 'Vera Vet',
              mobileNumber: '01712345678',
              emailAddress: null,
              roleIds: ['Vet'],
              hasPassword: true,
            },
          ],
          pageNumber: 1,
          pageSize: 100,
          totalCount: 1,
        }),
      ),
      http.put('*/api/admin/branches/:branchId/rosters/:staffAccountId/pattern', ({ request }) => {
        putUrl = new URL(request.url).pathname;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const user = userEvent.setup();
    render(
      <QueryClientProvider client={queryClient}>
        <Suspense fallback={<p>suspended</p>}>
          <BranchRosterPage
            params={resolvedParams({ branchId: administrationFixtureIds.mainBranchId })}
          />
        </Suspense>
      </QueryClientProvider>,
    );

    const staffSelect = (await screen.findAllByLabelText('Staff member'))[0]!;
    await screen.findByRole('option', { name: 'Vera Vet (no shifts yet)' });
    await user.selectOptions(staffSelect, 'vet-1');
    await user.click(await screen.findByRole('button', { name: /save pattern/i }));

    await expect
      .poll(() => putUrl)
      .toBe(`/api/admin/branches/${administrationFixtureIds.mainBranchId}/rosters/vet-1/pattern`);
  });
});
