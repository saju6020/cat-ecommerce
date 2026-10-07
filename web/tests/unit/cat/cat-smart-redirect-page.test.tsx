import { Suspense } from 'react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import CatSmartRedirectPage from '@/app/cats/[id]/page';
import { ApiError, ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import type { CustomerSession } from '@/lib/api/auth';
import { StaffSessionIdleTimeoutError, type StaffSessionStatus } from '@/lib/api/staff-auth';

/**
 * `tests/unit/cat/cat-smart-redirect-page.test.tsx` — HO-CAT-002 §1 Amended (2026-09-27, both the
 * original row and the owner's 2026-09-27 revision), covers every branch the page's own doc
 * comment describes: staff, owner (customer), a signed-in non-owner customer, anonymous, staff
 * wins over a co-present customer session, a staff idle-timeout (still "staff"), loading, a real
 * session-check failure, a real ownership-check (`GET /api/cats/{id}`) failure, and an invalid id.
 * Mocks at the `lib/api/*` layer (same pattern as `tests/unit/lib/use-vaccines.test.tsx`) so the
 * real `useStaffSession`/`useCustomerSession`/`useCat` hooks — and the real
 * `sanitizeNextPath`/`isValidCatId` helpers the page calls — run unmodified; only the network edge
 * is faked. The real cookie-driven sign-in flow through MSW is covered by
 * `tests/e2e/cat-smart-redirect.spec.ts` instead, in a real browser.
 */

const routerReplace = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: routerReplace, push: vi.fn() }),
}));

const getCustomerSessionMock = vi.fn();
vi.mock('@/lib/api/auth', () => ({
  getCustomerSession: () => getCustomerSessionMock(),
}));

const getStaffSessionStatusMock = vi.fn();
vi.mock('@/lib/api/staff-auth', async () => {
  const actual =
    await vi.importActual<typeof import('@/lib/api/staff-auth')>('@/lib/api/staff-auth');
  return {
    ...actual,
    getStaffSessionStatus: () => getStaffSessionStatusMock(),
  };
});

// The ownership check (owner decision, 2026-09-27) reuses `useCat`, which calls `getCat` —
// mocked here the same way the session endpoints are above, so the real `useCat` (and its new
// `enabled` gate, `lib/hooks/useCat.ts`) runs unmodified.
const getCatMock = vi.fn();
vi.mock('@/lib/api/cats', () => ({
  getCat: () => getCatMock(),
}));

const CAT_ID = '3fa85f64-5717-4562-b3fc-2c963f66afa6';
const STAFF_SIGN_IN_NEXT_URL = `/staff/sign-in?next=${encodeURIComponent(`/staff/cats/${CAT_ID}`)}`;

const STAFF_SESSION: StaffSessionStatus = {
  staffAccountId: 'staff-1',
  fullName: 'Staff One',
  roleIds: ['vet'],
  issuedOn: new Date().toISOString(),
  lastActivityOn: new Date().toISOString(),
  idleExpiresOn: new Date(Date.now() + 100_000).toISOString(),
  absoluteExpiresOn: new Date(Date.now() + 100_000).toISOString(),
  idleTimeoutMinutes: 15,
  serverTime: new Date().toISOString(),
};

const CUSTOMER_SESSION: CustomerSession = {
  customerId: 'customer-1',
  fullName: 'Cat Owner',
  expiresOn: new Date(Date.now() + 100_000).toISOString(),
  recordStatus: 'Complete',
};

function renderPage(id: string, prime?: (client: QueryClient) => void) {
  // The app's own default `staleTime` (lib/query client, 30 s) is what let cached data look fresh.
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: 30_000 } },
  });
  prime?.(queryClient);
  // Same `use(params)`-suspends-once pattern as `edit-cat-page.test.tsx` — a route's `params` is a
  // Promise in Next.js 15, which real Next.js wraps in a Suspense boundary automatically.
  return render(
    <QueryClientProvider client={queryClient}>
      <Suspense fallback={<div>page loading…</div>}>
        <CatSmartRedirectPage params={Promise.resolve({ id })} />
      </Suspense>
    </QueryClientProvider>,
  );
}

function neverResolves(): Promise<never> {
  return new Promise(() => {});
}

beforeEach(() => {
  routerReplace.mockClear();
  getCustomerSessionMock.mockReset();
  getStaffSessionStatusMock.mockReset();
  getCatMock.mockReset();
});

describe('CatSmartRedirectPage — /cats/{id}', () => {
  it('invalid id: shows an inline message and never redirects, even though the two session requests still fire', async () => {
    getStaffSessionStatusMock.mockResolvedValue(null);
    getCustomerSessionMock.mockResolvedValue(null);

    await act(async () => {
      renderPage('not-a-guid');
    });

    expect(await screen.findByText(/link isn't valid/i)).toBeInTheDocument();
    expect(routerReplace).not.toHaveBeenCalled();

    // ARCH gate LOW — an invalid id does not, and cannot, stop `useStaffSession`/
    // `useCustomerSession` from being called (React hooks are unconditional); it only stops their
    // RESULTS from being used. Both queries still resolve in the background.
    await waitFor(() => expect(getStaffSessionStatusMock).toHaveBeenCalled());
    await waitFor(() => expect(getCustomerSessionMock).toHaveBeenCalled());
    // The ownership check is never reached for an invalid id, regardless of session state.
    expect(getCatMock).not.toHaveBeenCalled();
  });

  it('loading: shows an accessible loading state while both sessions resolve, and does not redirect yet', async () => {
    getStaffSessionStatusMock.mockReturnValue(neverResolves());
    getCustomerSessionMock.mockReturnValue(neverResolves());

    await act(async () => {
      renderPage(CAT_ID);
    });

    expect(screen.getByRole('status')).toHaveTextContent(/loading/i);
    expect(routerReplace).not.toHaveBeenCalled();
  });

  it('staff: redirects to the staff cat profile, with no ownership (cat) call at all', async () => {
    getStaffSessionStatusMock.mockResolvedValue(STAFF_SESSION);
    getCustomerSessionMock.mockResolvedValue(null);

    await act(async () => {
      renderPage(CAT_ID);
    });

    await waitFor(() => expect(routerReplace).toHaveBeenCalledWith(`/staff/cats/${CAT_ID}`));
    expect(getCatMock).not.toHaveBeenCalled();
  });

  it('CAT row 1: cached cat data does not make a customer the owner; only a fresh fetch does', async () => {
    getStaffSessionStatusMock.mockResolvedValue(null);
    getCustomerSessionMock.mockResolvedValue(CUSTOMER_SESSION);
    // The API (fresh) says this customer may not read the cat; the cache (stale-looking-fresh) says yes.
    getCatMock.mockRejectedValue(new ApiForbiddenError({}));

    await act(async () => {
      renderPage(CAT_ID, (client) => client.setQueryData(['cat', CAT_ID], { id: CAT_ID }));
    });

    await waitFor(() => expect(getCatMock).toHaveBeenCalled());
    await waitFor(() => expect(routerReplace).toHaveBeenCalledWith(STAFF_SIGN_IN_NEXT_URL));
    expect(routerReplace).not.toHaveBeenCalledWith(`/portal/cats/${CAT_ID}`);
  });

  it('staff idle-timeout: still treated as a signed-in staff member, not bounced to sign-in', async () => {
    getStaffSessionStatusMock.mockRejectedValue(new StaffSessionIdleTimeoutError(15));
    getCustomerSessionMock.mockResolvedValue(null);

    await act(async () => {
      renderPage(CAT_ID);
    });

    await waitFor(() => expect(routerReplace).toHaveBeenCalledWith(`/staff/cats/${CAT_ID}`));
    expect(getCatMock).not.toHaveBeenCalled();
  });

  it('owner (customer): a successful ownership check redirects to the portal cat profile', async () => {
    getStaffSessionStatusMock.mockResolvedValue(null);
    getCustomerSessionMock.mockResolvedValue(CUSTOMER_SESSION);
    getCatMock.mockResolvedValue({ id: CAT_ID });

    await act(async () => {
      renderPage(CAT_ID);
    });

    await waitFor(() => expect(routerReplace).toHaveBeenCalledWith(`/portal/cats/${CAT_ID}`));
  });

  it('non-owner customer: a 404 ownership check sends them to staff sign-in, not the portal', async () => {
    getStaffSessionStatusMock.mockResolvedValue(null);
    getCustomerSessionMock.mockResolvedValue(CUSTOMER_SESSION);
    getCatMock.mockRejectedValue(new ApiNotFoundError());

    await act(async () => {
      renderPage(CAT_ID);
    });

    await waitFor(() => expect(routerReplace).toHaveBeenCalledWith(STAFF_SIGN_IN_NEXT_URL));
    expect(routerReplace).not.toHaveBeenCalledWith(`/portal/cats/${CAT_ID}`);
  });

  it('ownership check 5xx: fails closed to the same staff sign-in destination as a non-owner', async () => {
    getStaffSessionStatusMock.mockResolvedValue(null);
    getCustomerSessionMock.mockResolvedValue(CUSTOMER_SESSION);
    getCatMock.mockRejectedValue(new ApiError('Internal error', 500));

    await act(async () => {
      renderPage(CAT_ID);
    });

    // `useCat`'s own retry function (`lib/hooks/useCat.ts`) retries a non-404 error once, with
    // TanStack Query's default backoff delay — the per-query `retry` function this page's own
    // ownership check reuses overrides this test's QueryClient-level `retry: false`, so this needs
    // a longer timeout than the default 1000ms.
    await waitFor(() => expect(routerReplace).toHaveBeenCalledWith(STAFF_SIGN_IN_NEXT_URL), {
      timeout: 3000,
    });
    expect(routerReplace).not.toHaveBeenCalledWith(`/portal/cats/${CAT_ID}`);
  });

  it('staff wins when the browser carries both a staff and a customer session, with no ownership call', async () => {
    getStaffSessionStatusMock.mockResolvedValue(STAFF_SESSION);
    getCustomerSessionMock.mockResolvedValue(CUSTOMER_SESSION);

    await act(async () => {
      renderPage(CAT_ID);
    });

    await waitFor(() => expect(routerReplace).toHaveBeenCalledWith(`/staff/cats/${CAT_ID}`));
    expect(routerReplace).not.toHaveBeenCalledWith(`/portal/cats/${CAT_ID}`);
    expect(getCatMock).not.toHaveBeenCalled();
  });

  it('anonymous: redirects to staff sign-in with a safe next path back to the staff cat, no ownership call', async () => {
    getStaffSessionStatusMock.mockResolvedValue(null);
    getCustomerSessionMock.mockResolvedValue(null);

    await act(async () => {
      renderPage(CAT_ID);
    });

    await waitFor(() => expect(routerReplace).toHaveBeenCalledWith(STAFF_SIGN_IN_NEXT_URL));
    expect(getCatMock).not.toHaveBeenCalled();
  });

  it('error: a real session-check failure fails closed to staff sign-in, the same as anonymous — never stuck loading', async () => {
    getStaffSessionStatusMock.mockRejectedValue(new ApiError('Internal error', 500));
    getCustomerSessionMock.mockRejectedValue(new ApiError('Internal error', 500));

    await act(async () => {
      renderPage(CAT_ID);
    });

    await waitFor(() => expect(routerReplace).toHaveBeenCalledWith(STAFF_SIGN_IN_NEXT_URL));
  });

  // TEST gate (QR follow-up), U19 — the literal "shared clinic device" case R2 is meant to cover:
  // staff has gone idle (still counted as signed-in staff, per the idle-timeout test above) on a
  // browser that ALSO carries a customer session. Staff must still win outright, with no ownership
  // call, exactly as the plain staff-wins case above — this covers the combination the two existing
  // tests only cover separately (idle-timeout alone; staff-wins with a non-idle staff session).
  it('staff wins even when the staff session is idle-timed-out and a customer session is also present', async () => {
    getStaffSessionStatusMock.mockRejectedValue(new StaffSessionIdleTimeoutError(15));
    getCustomerSessionMock.mockResolvedValue(CUSTOMER_SESSION);

    await act(async () => {
      renderPage(CAT_ID);
    });

    await waitFor(() => expect(routerReplace).toHaveBeenCalledWith(`/staff/cats/${CAT_ID}`));
    expect(routerReplace).not.toHaveBeenCalledWith(`/portal/cats/${CAT_ID}`);
    expect(getCatMock).not.toHaveBeenCalled();
  });

  // TEST gate (QR follow-up), U20 — the handover's own doc comment names three categories that all
  // resolve to "not confirmed as owner": "a `404`, a `403`, or any other response that can't tell
  // doesn't-exist apart from not-yours". The existing tests cover a `404` (non-owner) and a `500`
  // (the ownership-check-5xx case); this closes the explicitly-named `403` gap.
  it('ownership check 403 (ApiForbiddenError): treated as non-owner, same destination as a 404', async () => {
    getStaffSessionStatusMock.mockResolvedValue(null);
    getCustomerSessionMock.mockResolvedValue(CUSTOMER_SESSION);
    getCatMock.mockRejectedValue(new ApiForbiddenError({}));

    await act(async () => {
      renderPage(CAT_ID);
    });

    await waitFor(() => expect(routerReplace).toHaveBeenCalledWith(STAFF_SIGN_IN_NEXT_URL), {
      timeout: 3000,
    });
    expect(routerReplace).not.toHaveBeenCalledWith(`/portal/cats/${CAT_ID}`);
  });
});
