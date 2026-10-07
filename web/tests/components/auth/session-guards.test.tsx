import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { CustomerSessionGuard } from '@/components/portal/CustomerSessionGuard';
import { StaffSessionGuard } from '@/components/staff/StaffSessionGuard';
import { StaffLockProvider } from '@/components/staff/IdleLockContext';
import { apiFetch } from '@/lib/api/client';
import { accountsFixtureIds, resetAccountsMockData } from '@/mocks/handlers/areas/accounts';

const replace = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace }),
  usePathname: () => '/portal/account/profile',
}));

function renderWithClient(children: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}>{children}</QueryClientProvider>);
}

beforeEach(() => {
  resetAccountsMockData();
  replace.mockClear();
});

describe('CustomerSessionGuard — U-C3', () => {
  it('renders a loading state and never the children while pending, then redirects when null', async () => {
    renderWithClient(
      <CustomerSessionGuard>
        <p>Secret portal content</p>
      </CustomerSessionGuard>,
    );

    expect(screen.queryByText('Secret portal content')).not.toBeInTheDocument();

    await waitFor(() =>
      expect(replace).toHaveBeenCalledWith('/sign-in?next=%2Fportal%2Faccount%2Fprofile'),
    );
    expect(screen.queryByText('Secret portal content')).not.toBeInTheDocument();
  });
});

describe('CustomerSessionGuard — U-C5, ADR-FE-003', () => {
  it('a 401 session_invalid from any OTHER portal query clears the whole cache and routes to sign-in with next', async () => {
    // The handler reads window.location.pathname directly (not the mocked usePathname() above,
    // which only feeds the "no session at all" redirect effect) — matched here so the
    // ?next= assertion below reflects the actual code path being exercised.
    window.history.replaceState(null, '', '/portal/account/profile');
    server.use(
      http.get('*/api/auth/session', () =>
        HttpResponse.json({
          customerId: accountsFixtureIds.customerId,
          fullName: 'Jamie Owner',
          expiresOn: new Date(Date.now() + 60 * 60_000).toISOString(),
          recordStatus: 'Complete',
        }),
      ),
    );
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    // Stands in for "some other customer's cats/orders already in the cache" — ADR-FE-003's own
    // concern (SEC gate LOW, first pass): this must go too, not just the session key.
    queryClient.setQueryData(['some', 'unrelated', 'portal', 'data'], { secret: true });

    render(
      <QueryClientProvider client={queryClient}>
        <CustomerSessionGuard>
          <p>Secret portal content</p>
        </CustomerSessionGuard>
      </QueryClientProvider>,
    );
    await screen.findByText('Secret portal content');
    expect(queryClient.getQueryData(['some', 'unrelated', 'portal', 'data'])).toBeTruthy();

    server.use(
      http.get('*/api/customers/me', () =>
        HttpResponse.json({ error: 'session_invalid' }, { status: 401 }),
      ),
    );
    await apiFetch('/api/customers/me').catch(() => {});

    await waitFor(() =>
      expect(replace).toHaveBeenCalledWith('/sign-in?next=%2Fportal%2Faccount%2Fprofile'),
    );
    expect(queryClient.getQueryData(['some', 'unrelated', 'portal', 'data'])).toBeUndefined();
  });
});

describe('CustomerSessionGuard — W10', () => {
  it('a mid-session 401 keeps the /book?… query in next, as the first redirect does', async () => {
    window.history.replaceState(null, '', '/book?catId=c1&category=Vaccination');
    server.use(
      http.get('*/api/auth/session', () =>
        HttpResponse.json({
          customerId: accountsFixtureIds.customerId,
          fullName: 'Jamie Owner',
          expiresOn: new Date(Date.now() + 60 * 60_000).toISOString(),
          recordStatus: 'Complete',
        }),
      ),
    );
    renderWithClient(
      <CustomerSessionGuard>
        <p>Secret portal content</p>
      </CustomerSessionGuard>,
    );
    await screen.findByText('Secret portal content');
    server.use(
      http.get('*/api/customers/me', () =>
        HttpResponse.json({ error: 'session_invalid' }, { status: 401 }),
      ),
    );
    await apiFetch('/api/customers/me').catch(() => {});
    await waitFor(() =>
      expect(replace).toHaveBeenCalledWith(
        `/sign-in?next=${encodeURIComponent('/book?catId=c1&category=Vaccination')}`,
      ),
    );
    window.history.replaceState(null, '', '/');
  });
});

describe('StaffSessionGuard — U-G1', () => {
  it('redirects to /staff/sign-in?next=... when there is no staff session', async () => {
    // ARCH gate HIGH (second pass) — `locked` now lives in `StaffLockProvider`, one level above
    // this guard (`(staff)/layout.tsx`'s own composition).
    renderWithClient(
      <StaffLockProvider>
        <StaffSessionGuard>
          <p>Secret staff content</p>
        </StaffSessionGuard>
      </StaffLockProvider>,
    );

    expect(screen.queryByText('Secret staff content')).not.toBeInTheDocument();
    await waitFor(() =>
      expect(replace).toHaveBeenCalledWith('/staff/sign-in?next=%2Fportal%2Faccount%2Fprofile'),
    );
  });
});
