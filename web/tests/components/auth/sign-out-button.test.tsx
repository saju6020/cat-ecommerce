import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { SignOutButton } from '@/components/auth/SignOutButton';
import { resetAccountsMockData } from '@/mocks/handlers/areas/accounts';

const replace = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace }),
  usePathname: () => '/portal/account/profile',
}));

function renderWithClient(children: React.ReactNode, queryClient: QueryClient) {
  return render(<QueryClientProvider client={queryClient}>{children}</QueryClientProvider>);
}

beforeEach(() => {
  resetAccountsMockData();
  replace.mockClear();
});

describe('SignOutButton — U-C4', () => {
  it('customer audience: calls the sign-out endpoint, clears the WHOLE cache (not just the session key), and routes to /sign-in', async () => {
    let signOutCalls = 0;
    server.use(
      http.post('*/api/auth/sign-out', () => {
        signOutCalls += 1;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    // Stands in for another customer's cached data that must not survive for the next person on
    // this browser (SignOutButton's own doc comment) — a bare session-key removal would miss this.
    queryClient.setQueryData(['some', 'unrelated', 'portal', 'data'], { secret: true });

    const user = userEvent.setup();
    renderWithClient(<SignOutButton audience="customer" />, queryClient);

    await user.click(screen.getByRole('button', { name: /sign out/i }));

    await waitFor(() => expect(replace).toHaveBeenCalledWith('/sign-in'));
    expect(signOutCalls).toBe(1);
    expect(queryClient.getQueryData(['some', 'unrelated', 'portal', 'data'])).toBeUndefined();
  });

  it('staff audience: routes to /staff/sign-in, not the customer sign-in page', async () => {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const user = userEvent.setup();
    renderWithClient(<SignOutButton audience="staff" />, queryClient);

    await user.click(screen.getByRole('button', { name: /sign out/i }));

    await waitFor(() => expect(replace).toHaveBeenCalledWith('/staff/sign-in'));
  });

  it('ADM 12: a staff sign-out clears everything, the active branch included', async () => {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    queryClient.setQueryData(['staff', 'active-branch'], 'branch-london');
    queryClient.setQueryData(['some', 'unrelated', 'data'], { secret: true });
    const user = userEvent.setup();
    renderWithClient(<SignOutButton audience="staff" />, queryClient);

    await user.click(screen.getByRole('button', { name: /sign out/i }));

    await waitFor(() => expect(replace).toHaveBeenCalledWith('/staff/sign-in'));
    expect(queryClient.getQueryData(['some', 'unrelated', 'data'])).toBeUndefined();
    expect(queryClient.getQueryData(['staff', 'active-branch'])).toBeUndefined();
  });

  it('ADM 12: a customer sign-out still clears everything, branch key included', async () => {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    queryClient.setQueryData(['staff', 'active-branch'], 'branch-london');
    const user = userEvent.setup();
    renderWithClient(<SignOutButton audience="customer" />, queryClient);
    await user.click(screen.getByRole('button', { name: /sign out/i }));
    await waitFor(() => expect(replace).toHaveBeenCalledWith('/sign-in'));
    expect(queryClient.getQueryData(['staff', 'active-branch'])).toBeUndefined();
  });
});
