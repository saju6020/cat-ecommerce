import { Suspense } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { administrationFixtureIds } from '@/mocks/handlers/admin';
import TaxClassDetailPage from '@/app/(staff)/staff/admin/tax-classes/[taxClassId]/page';
import ServiceDetailPage from '@/app/(staff)/staff/admin/services/[serviceId]/page';
import BranchRosterPage from '@/app/(staff)/staff/admin/branches/[branchId]/roster/page';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  usePathname: () => '/staff/admin',
  useSearchParams: () => new URLSearchParams(),
}));

/**
 * HO-ADM-002 §18 A3 (ACCEPTANCE gate F-H1) — every permission-denied case shows
 * `PermissionDeniedState`. These render the real page modules (not a stand-in): a server `403`
 * must never leave a detail page spinning, or fall into a generic load error.
 */
const FORBIDDEN = {
  errors: [{ errorCode: 'ActionNotPermitted', errorMessage: 'Not permitted.' }],
};
const forbidden = () => HttpResponse.json(FORBIDDEN, { status: 403 });

function renderPage(page: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <Suspense fallback={<p>suspended</p>}>{page}</Suspense>
    </QueryClientProvider>,
  );
}

/** A route `params` promise React's `use()` can read synchronously. A bare `Promise.resolve` needs an
 *  extra Suspense tick that never lands under this test environment (see idle-lock.test.tsx's own
 *  note on the same friction); React treats a thenable already marked `fulfilled` as settled. */
function resolvedParams<T>(value: T): Promise<T> {
  return Object.assign(Promise.resolve(value), { status: 'fulfilled', value }) as Promise<T>;
}

/** The detail hooks retry a non-403 failure once (`noRetryOn403`), after TanStack's 1s back-off. */
const RETRY_WAIT = { timeout: 5000 };

const PERMISSION_TEXT = /you don.t have permission to do this/i;

describe('§18 A3 — detail pages render PermissionDeniedState on a server 403', () => {
  it('tax-class detail: a 403 on the tax class', async () => {
    server.use(http.get('*/api/admin/tax-classes/:taxClassId', forbidden));
    renderPage(
      <TaxClassDetailPage
        params={resolvedParams({ taxClassId: administrationFixtureIds.taxClassStandardId })}
      />,
    );
    expect(await screen.findByText(PERMISSION_TEXT)).toBeInTheDocument();
    expect(screen.queryByText(/ActionNotPermitted/)).not.toBeInTheDocument();
  });

  it('tax-class detail: a non-403 error shows the error state with retry, not an endless spinner', async () => {
    server.use(
      http.get('*/api/admin/tax-classes/:taxClassId', () => HttpResponse.json({}, { status: 500 })),
    );
    renderPage(
      <TaxClassDetailPage
        params={resolvedParams({ taxClassId: administrationFixtureIds.taxClassStandardId })}
      />,
    );
    expect(
      await screen.findByText(/couldn.t load this tax class/i, {}, RETRY_WAIT),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /try again/i })).toBeInTheDocument();
    expect(screen.queryByText(PERMISSION_TEXT)).not.toBeInTheDocument();
  });

  it('service detail: a 403 on the service', async () => {
    server.use(http.get('*/api/admin/services/:serviceId', forbidden));
    renderPage(
      <ServiceDetailPage
        params={resolvedParams({ serviceId: administrationFixtureIds.serviceConsultationId })}
      />,
    );
    expect(await screen.findByText(PERMISSION_TEXT)).toBeInTheDocument();
  });

  it('service detail: a non-403 error shows the error state, not an endless spinner', async () => {
    server.use(
      http.get('*/api/admin/services/:serviceId', () => HttpResponse.json({}, { status: 500 })),
    );
    renderPage(
      <ServiceDetailPage
        params={resolvedParams({ serviceId: administrationFixtureIds.serviceConsultationId })}
      />,
    );
    expect(
      await screen.findByText(/couldn.t load this service/i, {}, RETRY_WAIT),
    ).toBeInTheDocument();
  });

  it('roster: a 403 on rosters', async () => {
    server.use(http.get('*/api/admin/branches/:branchId/rosters', forbidden));
    renderPage(
      <BranchRosterPage
        params={resolvedParams({ branchId: administrationFixtureIds.mainBranchId })}
      />,
    );
    expect(await screen.findByText(PERMISSION_TEXT)).toBeInTheDocument();
  });

  it('roster: a 403 on availability', async () => {
    server.use(http.get('*/api/admin/branches/:branchId/availability', forbidden));
    renderPage(
      <BranchRosterPage
        params={resolvedParams({ branchId: administrationFixtureIds.mainBranchId })}
      />,
    );
    expect(await screen.findByText(PERMISSION_TEXT)).toBeInTheDocument();
  });
});
