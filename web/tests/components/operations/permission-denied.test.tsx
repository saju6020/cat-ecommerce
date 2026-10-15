import { Suspense } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import RetentionPage from '@/app/(staff)/staff/admin/retention/page';
import RetentionRunsPage from '@/app/(staff)/staff/admin/retention/runs/page';
import RetentionRunPage from '@/app/(staff)/staff/admin/retention/runs/[runId]/page';
import ErasureRequestsPage from '@/app/(staff)/staff/admin/retention/erasures/page';
import ErasureLedgerPage from '@/app/(staff)/staff/admin/retention/erasures/[customerAccountId]/page';
import BackupPage from '@/app/(staff)/staff/admin/operations/backup/page';
import RestoreTestPage from '@/app/(staff)/staff/admin/operations/backup/[restoreTestId]/page';
import PerformancePage from '@/app/(staff)/staff/admin/performance/page';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
  usePathname: () => '/staff/admin',
  useSearchParams: () => new URLSearchParams(),
}));

const forbidden = () =>
  HttpResponse.json({ errors: [{ errorCode: 'ActionNotPermitted', errorMessage: 'Not permitted.' }] }, { status: 403 });

const uuid = '00000000-0000-0000-0000-000000000009';

/** U-B13 -- every admin screen of this slice, when the server answers 403: PermissionDeniedState and
 *  no partial data (no table, no form, no figures). */
describe('U-B13 a 403 renders PermissionDeniedState on every new admin screen', () => {
  const cases: Array<[string, () => React.ReactElement, string[]]> = [
    ['retention', () => <RetentionPage />, ['*/api/admin/retention/schedule', '*/api/admin/retention/review']],
    ['runs', () => <RetentionRunsPage />, ['*/api/admin/retention/runs']],
    ['run detail', () => <RetentionRunPage params={Promise.resolve({ runId: uuid })} />, ['*/api/admin/retention/runs/:id']],
    ['erasure requests', () => <ErasureRequestsPage />, ['*/api/admin/retention/erasure-requests']],
    ['erasure ledger', () => <ErasureLedgerPage params={Promise.resolve({ customerAccountId: uuid })} />, ['*/api/admin/retention/erasure-requests/:id']],
    ['backup', () => <BackupPage />, ['*/api/admin/operations/backup-status', '*/api/admin/operations/restore-tests']],
    ['restore test', () => <RestoreTestPage params={Promise.resolve({ restoreTestId: uuid })} />, ['*/api/admin/operations/restore-tests/:id']],
    ['performance', () => <PerformancePage />, ['*/api/admin/web-vitals/summary']],
  ];

  for (const [name, element, routes] of cases) {
    it(`${name}`, async () => {
      server.use(...routes.map((route) => http.get(route, forbidden)));
      const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
      await act(async () => {
        render(
          <QueryClientProvider client={client}>
            <Suspense fallback={<p>loading</p>}>{element()}</Suspense>
          </QueryClientProvider>,
        );
      });
      expect(await screen.findByText(/you don.t have permission to do this/i)).toBeInTheDocument();
      expect(screen.queryByRole('table')).toBeNull();
      expect(screen.queryByRole('form')).toBeNull();
      expect(screen.queryByText(/ActionNotPermitted/)).toBeNull();
    });
  }
});
