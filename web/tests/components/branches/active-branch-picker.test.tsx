import { describe, expect, it } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { ActiveBranchPicker } from '@/components/branches/ActiveBranchPicker';
import { ACTIVE_BRANCH_KEY } from '@/lib/hooks/useActiveBranch';

// §8.3: the active branch is TanStack Query state under `ACTIVE_BRANCH_KEY`, not a module-level
// store — each test below builds its own `QueryClient` (the same one `ActiveBranchPicker` reads
// and writes through `useQueryClient()`), so there is nothing to reset between tests.
function renderWithClient(children: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const { container } = render(
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>,
  );
  return { queryClient, container };
}

describe('ActiveBranchPicker — HO-ADM-002 §11.2, ADR-FE-016', () => {
  it('U-B5: hidden with one branch', async () => {
    server.use(
      http.get('*/api/staff/branches/mine', () =>
        HttpResponse.json({
          branchIds: ['b1'],
          primaryBranchId: 'b1',
          activeBranchId: 'b1',
          activeBranchRefusal: null,
        }),
      ),
      http.get('*/api/admin/branches', () =>
        HttpResponse.json([
          { id: 'b1', code: 'MAIN', name: 'Main clinic', timeZoneId: 'Asia/Dhaka', isActive: true },
        ]),
      ),
    );
    const { queryClient, container } = renderWithClient(<ActiveBranchPicker />);
    await waitFor(() => expect(queryClient.getQueryData(ACTIVE_BRANCH_KEY)).toBe('b1')); // the seed effect has now run
    expect(container).toBeEmptyDOMElement();
  });

  it('U-B5: shown with two branches, and changing it updates the value client.ts reads for X-Branch-Id', async () => {
    server.use(
      http.get('*/api/staff/branches/mine', () =>
        HttpResponse.json({
          branchIds: ['b1', 'b2'],
          primaryBranchId: 'b1',
          activeBranchId: 'b1',
          activeBranchRefusal: null,
        }),
      ),
      http.get('*/api/admin/branches', () =>
        HttpResponse.json([
          { id: 'b1', code: 'MAIN', name: 'Main clinic', timeZoneId: 'Asia/Dhaka', isActive: true },
          {
            id: 'b2',
            code: 'LDN',
            name: 'London clinic',
            timeZoneId: 'Europe/London',
            isActive: true,
          },
        ]),
      ),
    );
    const user = userEvent.setup();
    const { queryClient } = renderWithClient(<ActiveBranchPicker />);

    const select = await screen.findByLabelText(/branch/i);
    await waitFor(() => expect(queryClient.getQueryData(ACTIVE_BRANCH_KEY)).toBe('b1'));

    await user.selectOptions(select, 'b2');
    expect(queryClient.getQueryData(ACTIVE_BRANCH_KEY)).toBe('b2');
  });
});
