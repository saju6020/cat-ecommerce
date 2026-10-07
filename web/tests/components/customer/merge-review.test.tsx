import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { MergeReview } from '@/components/customer/MergeReview';
import { useStaffCustomersByIds } from '@/lib/hooks/useStaffCustomers';
import { useMergeCustomer } from '@/lib/hooks/useStaffCustomerMutations';
import type { StaffCustomer } from '@/lib/api/staff-customers';

/** ARCH gate HIGH 3 (first pass) — stand-in for
 *  app/(staff)/staff/customers/duplicates/review/page.tsx; see register-form.test.tsx's own
 *  identical-purpose wrapper comment. */
function MergeReviewWithHooks({ customerIds }: { customerIds: string[] }) {
  const queries = useStaffCustomersByIds(customerIds);
  const mergeMutation = useMergeCustomer();
  const isLoading = queries.some((q) => q.isLoading);
  const isError = queries.some((q) => q.isError);
  const customers = queries.map((q) => q.data).filter((c): c is StaffCustomer => !!c);
  return (
    <MergeReview
      customerIds={customerIds}
      isLoading={isLoading}
      isError={isError}
      customers={customers}
      onMerge={(duplicateCustomerId, request) =>
        mergeMutation.mutateAsync({ duplicateCustomerId, request })
      }
      isMergePending={mergeMutation.isPending}
    />
  );
}

function renderWithClient(children: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}>{children}</QueryClientProvider>);
}

// CR MEDIUM 2 (first-pass gate fix round, item 13) — `catNames` builds `cats` (the fresh read's
// own per-cat list, now what MergeConfirmDialog actually renders) alongside the unchanged
// `catCount`, kept in sync with it.
function customerResponse(id: string, fullName: string, catNames: string[]) {
  return {
    id,
    fullName,
    mobileNumber: '017',
    emailAddress: null,
    recordStatus: 'Complete',
    registrationChannel: 'SelfService',
    catCount: catNames.length,
    cats: catNames.map((name, index) => ({ catId: `${id}-cat-${index}`, name, isDeceased: false })),
    status: 'Active',
    mergedIntoCustomerId: null,
  };
}

describe('MergeReview — US-ACC-11, ADR-FE-011', () => {
  it('U-I2: fetches fresh on mount, pre-selects no radio, and requires a reason before reviewing', async () => {
    let getCallCount = 0;
    server.use(
      http.get('*/api/staff/customers/:id', ({ params }) => {
        getCallCount += 1;
        return HttpResponse.json(
          customerResponse(params.id as string, params.id === 'a' ? 'Alpha' : 'Beta', ['Whiskers']),
        );
      }),
    );
    renderWithClient(<MergeReviewWithHooks customerIds={['a', 'b']} />);

    await screen.findByText('Alpha (017)');
    expect(getCallCount).toBe(2);
    const radios = screen.getAllByRole('radio');
    expect(radios.every((radio) => !(radio as HTMLInputElement).checked)).toBe(true);

    const reviewButton = screen.getByRole('button', { name: /review merge/i });
    expect(reviewButton).toBeDisabled();
  });

  it('U-I2: the confirmation names the survivor and the retired record before the one POST', async () => {
    server.use(
      http.get('*/api/staff/customers/:id', ({ params }) =>
        HttpResponse.json(
          customerResponse(params.id as string, params.id === 'a' ? 'Alpha' : 'Beta', [
            'Whiskers',
            'Mittens',
          ]),
        ),
      ),
    );
    let mergeCalls = 0;
    server.use(
      http.post('*/api/staff/customers/:id/merge', () => {
        mergeCalls += 1;
        return HttpResponse.json({ survivingCustomerId: 'a', isReapplication: false });
      }),
    );
    const user = userEvent.setup();
    renderWithClient(<MergeReviewWithHooks customerIds={['a', 'b']} />);

    await screen.findByText('Alpha (017)');
    await user.click(screen.getByRole('radio', { name: /^Alpha/ }));
    await user.type(screen.getByLabelText(/reason/i), 'Same person, confirmed by phone.');
    await user.click(screen.getByRole('button', { name: /review merge/i }));

    const dialog = await screen.findByRole('dialog');
    expect(dialog.textContent).toMatch(/alpha will be kept/i);
    expect(dialog.textContent).toMatch(/beta will be retired/i);
    expect(within(dialog).getByText(/whiskers, mittens will move from beta/i)).toBeInTheDocument();

    await user.click(within(dialog).getByRole('button', { name: /^merge$/i }));

    await waitFor(() => expect(mergeCalls).toBe(1));
    expect(await screen.findByText(/the records have been merged/i)).toBeInTheDocument();
  });

  it('U-I3: a 5xx shows "may be partly complete" with a Run again button that resends the identical request', async () => {
    server.use(
      http.get('*/api/staff/customers/:id', ({ params }) =>
        HttpResponse.json(
          customerResponse(params.id as string, params.id === 'a' ? 'Alpha' : 'Beta', ['Whiskers']),
        ),
      ),
    );
    let attempt = 0;
    const capturedBodies: unknown[] = [];
    server.use(
      http.post('*/api/staff/customers/:id/merge', async ({ request }) => {
        attempt += 1;
        capturedBodies.push(await request.json());
        if (attempt === 1) return HttpResponse.json({ error: 'internal' }, { status: 500 });
        return HttpResponse.json({ survivingCustomerId: 'a', isReapplication: false });
      }),
    );
    const user = userEvent.setup();
    renderWithClient(<MergeReviewWithHooks customerIds={['a', 'b']} />);

    await screen.findByText('Alpha (017)');
    await user.click(screen.getByRole('radio', { name: /^Alpha/ }));
    await user.type(screen.getByLabelText(/reason/i), 'Same person.');
    await user.click(screen.getByRole('button', { name: /review merge/i }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: /^merge$/i }),
    );

    expect(await screen.findByText(/may be partly complete/i)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /run again/i }));

    expect(await screen.findByText(/the records have been merged/i)).toBeInTheDocument();
    expect(capturedBodies).toEqual([
      { survivingCustomerId: 'a', reason: 'Same person.' },
      { survivingCustomerId: 'a', reason: 'Same person.' },
    ]);
  });

  it('U-I4: never calls a cat endpoint', async () => {
    server.use(
      http.get('*/api/staff/customers/:id', ({ params }) =>
        HttpResponse.json(
          customerResponse(params.id as string, params.id === 'a' ? 'Alpha' : 'Beta', ['Whiskers']),
        ),
      ),
      http.post('*/api/staff/customers/:id/merge', () =>
        HttpResponse.json({ survivingCustomerId: 'a', isReapplication: false }),
      ),
    );
    const catSpy = vi.fn();
    server.use(http.all('*/api/cats*', () => (catSpy(), new HttpResponse(null, { status: 200 }))));

    const user = userEvent.setup();
    renderWithClient(<MergeReviewWithHooks customerIds={['a', 'b']} />);
    await screen.findByText('Alpha (017)');
    await user.click(screen.getByRole('radio', { name: /^Alpha/ }));
    await user.type(screen.getByLabelText(/reason/i), 'Same person.');
    await user.click(screen.getByRole('button', { name: /review merge/i }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: /^merge$/i }),
    );

    await screen.findByText(/the records have been merged/i);
    expect(catSpy).not.toHaveBeenCalled();
  });

  // ARCHITECTURE gate third pass, HIGH — the root-cause part of this fix (StaffLockProvider
  // pausing query refetch while locked) is exercised in idle-lock-context.test.tsx/
  // idle-lock.test.tsx; this is the page-level half, directly on `MergeReview` itself, which takes
  // `isError`/`customers` as plain props (ARCH gate HIGH 3, first pass) rather than calling a query
  // hook of its own — no lock/session machinery needed to prove this one guard.
  it('ARCHITECTURE gate third pass, HIGH: an isError on one query does not blank the whole review while every member still has its own cached data', () => {
    const customers: StaffCustomer[] = [
      {
        id: 'a',
        fullName: 'Alpha',
        mobileNumber: '017',
        emailAddress: null,
        recordStatus: 'Complete',
        registrationChannel: 'SelfService',
        catCount: 0,
        cats: [],
        status: 'Active',
        mergedIntoCustomerId: null,
      },
      {
        id: 'b',
        fullName: 'Beta',
        mobileNumber: '018',
        emailAddress: null,
        recordStatus: 'Complete',
        registrationChannel: 'SelfService',
        catCount: 0,
        cats: [],
        status: 'Active',
        mergedIntoCustomerId: null,
      },
    ];

    render(
      <MergeReview
        customerIds={['a', 'b']}
        isLoading={false}
        // isError=true (one member's query independently refetch-failed) but BOTH members still
        // have their own cached data (`customers.length === customerIds.length`) — the survivor
        // selection, reason and any in-progress partial-merge state must all survive this, not be
        // blanked by ErrorState the way `isError` alone used to trigger.
        isError
        customers={customers}
        onMerge={vi.fn()}
        isMergePending={false}
      />,
    );

    expect(screen.getByText('Alpha (017)')).toBeInTheDocument();
    expect(screen.getByText('Beta (018)')).toBeInTheDocument();
    expect(screen.queryByText(/couldn't load one or more/i)).not.toBeInTheDocument();
  });

  it('ARCHITECTURE gate third pass, HIGH: isError WITH a member genuinely missing its data still shows ErrorState', () => {
    render(
      <MergeReview
        customerIds={['a', 'b']}
        isLoading={false}
        isError
        customers={[]}
        onMerge={vi.fn()}
        isMergePending={false}
      />,
    );

    expect(screen.getByText(/couldn't load one or more/i)).toBeInTheDocument();
  });
});
