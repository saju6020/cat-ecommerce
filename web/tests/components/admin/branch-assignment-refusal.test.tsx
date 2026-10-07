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
import ClosuresPage from '@/app/(staff)/staff/admin/branches/[branchId]/closures/page';
import RoomsPage from '@/app/(staff)/staff/admin/branches/[branchId]/rooms/page';
import SettingsPage from '@/app/(staff)/staff/admin/settings/page';
import SystemPage from '@/app/(staff)/staff/admin/system/page';
import MyLeavePage from '@/app/(staff)/staff/leave/page';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  usePathname: () => '/staff/admin',
  useSearchParams: () => new URLSearchParams(),
}));

/**
 * HO-ADM-002 §11.9, ADM backlog 34/41 — on the pages fa80c47 changed, a 403 whose code is
 * `BranchNotAssigned`/`BranchAssignmentRequired` shows `BranchAssignmentBanner` ("ask to be
 * assigned"), not `PermissionDeniedState`; any other 403 still shows `PermissionDeniedState`.
 */
const refusal = (errorCode: string) => () =>
  HttpResponse.json({ errors: [{ errorCode, errorMessage: 'Refused.' }] }, { status: 403 });

function resolvedParams<T>(value: T): Promise<T> {
  return Object.assign(Promise.resolve(value), { status: 'fulfilled', value }) as Promise<T>;
}

function renderPage(page: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <Suspense fallback={<p>suspended</p>}>{page}</Suspense>
    </QueryClientProvider>,
  );
}

const branchParams = () => resolvedParams({ branchId: administrationFixtureIds.mainBranchId });

const PAGES: Array<{ name: string; url: string; page: () => React.ReactNode }> = [
  {
    name: 'tax-class detail',
    url: '*/api/admin/tax-classes/:taxClassId',
    page: () => (
      <TaxClassDetailPage
        params={resolvedParams({ taxClassId: administrationFixtureIds.taxClassStandardId })}
      />
    ),
  },
  {
    name: 'service detail',
    url: '*/api/admin/services/:serviceId',
    page: () => (
      <ServiceDetailPage
        params={resolvedParams({ serviceId: administrationFixtureIds.serviceConsultationId })}
      />
    ),
  },
  {
    name: 'roster',
    url: '*/api/admin/branches/:branchId/rosters',
    page: () => <BranchRosterPage params={branchParams()} />,
  },
  {
    name: 'closures',
    url: '*/api/admin/branches/:branchId/closures',
    page: () => <ClosuresPage params={branchParams()} />,
  },
  {
    name: 'rooms',
    url: '*/api/admin/branches/:branchId/rooms',
    page: () => <RoomsPage params={branchParams()} />,
  },
  { name: 'settings', url: '*/api/admin/settings', page: () => <SettingsPage /> },
  { name: 'system', url: '*/api/system/status', page: () => <SystemPage /> },
  { name: 'my leave', url: '*/api/staff/leave-requests', page: () => <MyLeavePage /> },
];

describe('BranchNotAssigned / BranchAssignmentRequired show the assignment banner (ADM 34, 41)', () => {
  for (const { name, url, page } of PAGES) {
    it(`${name}: BranchAssignmentRequired asks to be assigned`, async () => {
      server.use(http.get(url, refusal('BranchAssignmentRequired')));
      renderPage(page());
      expect(
        await screen.findByText(/ask an administrator to assign you to a branch/i),
      ).toBeVisible();
      expect(screen.queryByText(/you don.t have permission to do this/i)).not.toBeInTheDocument();
    });

    it(`${name}: BranchNotAssigned points at the branch picker`, async () => {
      server.use(http.get(url, refusal('BranchNotAssigned')));
      renderPage(page());
      expect(await screen.findByText(/choose one of your assigned branches/i)).toBeVisible();
      expect(screen.queryByText(/you don.t have permission to do this/i)).not.toBeInTheDocument();
    });

    it(`${name}: any other 403 still shows PermissionDeniedState`, async () => {
      server.use(http.get(url, refusal('ActionNotPermitted')));
      renderPage(page());
      expect(await screen.findByText(/you don.t have permission to do this/i)).toBeVisible();
    });
  }
});
