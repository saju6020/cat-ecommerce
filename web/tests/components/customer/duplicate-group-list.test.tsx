import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import {
  DuplicateGroupList,
  DUPLICATE_GROUPS_PAGE_SIZE,
} from '@/components/customer/DuplicateGroupList';
import { useDuplicateCustomerGroups } from '@/lib/hooks/useStaffCustomers';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: vi.fn() }),
  usePathname: () => '/staff/customers/duplicates',
  useSearchParams: () => new URLSearchParams(),
}));

/** ARCH gate HIGH 3 (first pass) — stand-in for
 *  app/(staff)/staff/customers/duplicates/page.tsx; see register-form.test.tsx's own
 *  identical-purpose wrapper comment. */
function DuplicateGroupListWithHooks() {
  const { data, isLoading, isError, error, refetch } = useDuplicateCustomerGroups({
    pageNumber: 1,
    pageSize: DUPLICATE_GROUPS_PAGE_SIZE,
  });
  return (
    <DuplicateGroupList
      data={data}
      isLoading={isLoading}
      isError={isError}
      error={error}
      onRetry={() => refetch()}
      pageNumber={1}
      onPageChange={() => {}}
    />
  );
}

function renderWithClient(children: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}>{children}</QueryClientProvider>);
}

describe('DuplicateGroupList — US-ACC-11', () => {
  it('U-I1: lists cats including a deceased badge, shows NotYetRecordedNotice, no orders/balances columns', async () => {
    server.use(
      http.get('*/api/staff/customers/duplicates', () =>
        HttpResponse.json({
          items: [
            {
              members: [
                {
                  customerId: 'a',
                  fullName: 'Alpha',
                  mobileNumber: '017',
                  emailAddress: null,
                  cats: [{ catId: 'c1', name: 'Late Cat', isDeceased: true }],
                  mobileMatchedOn: 'Primary',
                  recordStatus: 'Complete',
                  registrationChannel: 'SelfService',
                },
                {
                  customerId: 'b',
                  fullName: 'Beta',
                  mobileNumber: '017',
                  emailAddress: null,
                  cats: [{ catId: 'c2', name: 'Live Cat', isDeceased: false }],
                  mobileMatchedOn: 'Primary',
                  recordStatus: 'Incomplete',
                  registrationChannel: 'Reception',
                },
              ],
            },
          ],
          pageNumber: 1,
          pageSize: 20,
          totalCount: 1,
        }),
      ),
    );
    renderWithClient(<DuplicateGroupListWithHooks />);

    expect(await screen.findByText('Late Cat')).toBeInTheDocument();
    expect(screen.getByText('Deceased')).toBeInTheDocument();
    expect(screen.getByText('Live Cat')).toBeInTheDocument();
    expect(screen.queryByText('Deceased')?.closest('li')?.textContent).not.toMatch(/Live Cat/);
    expect(
      screen.getByText(/orders, invoices and balances aren.t recorded in the system yet/i),
    ).toBeInTheDocument();
    expect(screen.queryByText(/orders total|balance due/i)).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: /review merge/i })).toHaveAttribute(
      'href',
      '/staff/customers/duplicates/review?ids=a%2Cb',
    );

    // CR MEDIUM 2 (first-pass gate fix round, item 13) — recordStatus/registrationChannel per
    // member, previously not carried by the real API at all (see DuplicateGroupList.tsx's own
    // doc comment).
    expect(screen.getByText('Complete')).toBeInTheDocument();
    expect(screen.getByText('Incomplete')).toBeInTheDocument();
    expect(screen.getByText(/self-service/i)).toBeInTheDocument();
    expect(screen.getByText(/walk-in/i)).toBeInTheDocument();
  });

  it('shows an empty state when there are no duplicates', async () => {
    server.use(
      http.get('*/api/staff/customers/duplicates', () =>
        HttpResponse.json({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0 }),
      ),
    );
    renderWithClient(<DuplicateGroupListWithHooks />);
    expect(await screen.findByText(/no duplicate customers found/i)).toBeInTheDocument();
  });
});
