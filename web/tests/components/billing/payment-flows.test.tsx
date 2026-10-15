import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { server } from '../../../mocks/server';
import { billingIds as ids, resetBillingMockData } from '../../../mocks/handlers/billing';
import { RecordPaymentForm } from '@/components/billing/RecordPaymentForm';
import { ApprovalsQueue } from '@/components/billing/ApprovalsQueue';
import { InvoiceActions } from '@/components/billing/InvoiceActions';
import { setUrl } from '../booking/nav-mock';

vi.mock('next/navigation', () => import('../booking/nav-mock'));

function renderWithClient(ui: ReactElement) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: 0 }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const money = (amount: string) => ({ amount, currencyCode: 'BDT' });

async function ready() {
  await screen.findByRole('radio', { name: /Cash in store/i });
}

describe('RecordPaymentForm — HO-BIL-002 §11.4', () => {
  beforeEach(() => {
    resetBillingMockData();
    setUrl('/staff/billing/payments/new');
  });

  it('U-C1: card in store asks for the terminal slip number, no collector', async () => {
    const user = userEvent.setup();
    renderWithClient(<RecordPaymentForm customerId={ids.customerId} invoiceIds={[ids.partPaidInvoiceId]} />);
    await ready();
    await user.click(screen.getByRole('radio', { name: /Card in store/i }));
    expect(screen.getByLabelText(/Terminal slip number/i)).toBeRequired();
    expect(screen.queryByText(/Collected by/i)).not.toBeInTheDocument();
  });

  it('U-C2: cash on delivery requires a collector', async () => {
    const user = userEvent.setup();
    renderWithClient(<RecordPaymentForm customerId={ids.customerId} />);
    await ready();
    await user.click(screen.getByRole('radio', { name: /Cash on delivery/i }));
    expect(screen.getByText(/Collected by/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Courier code/i)).toBeInTheDocument();
  });

  it('U-C3: an unknown method renders from its flags; an online one is filtered out', async () => {
    server.use(
      http.get('*/api/billing/payment-methods', () =>
        HttpResponse.json([
          { value: 9, name: 'MobileWalletInStore', countsInTill: false, requiresReference: true, referenceLabel: 'Wallet transaction id', requiresCollector: false, isOnline: false, allowedRefundMethods: [] },
          { value: 10, name: 'OnlineCard', countsInTill: false, requiresReference: false, referenceLabel: 'Ref', requiresCollector: false, isOnline: true, allowedRefundMethods: [] },
        ]),
      ),
    );
    renderWithClient(<RecordPaymentForm customerId={ids.customerId} />);
    expect(await screen.findByRole('radio', { name: /Mobile wallet in store/i })).toBeInTheDocument();
    expect(screen.queryByRole('radio', { name: /Online card/i })).not.toBeInTheDocument();
    expect(screen.getByLabelText(/Wallet transaction id/i)).toBeInTheDocument();
  });

  it('U-C4: a card number in the reference blocks the submit and nothing is sent', async () => {
    const user = userEvent.setup();
    let sent = 0;
    server.use(http.post('*/api/staff/billing/payments', () => { sent += 1; return HttpResponse.json({}, { status: 201 }); }));
    renderWithClient(<RecordPaymentForm customerId={ids.customerId} invoiceIds={[ids.partPaidInvoiceId]} />);
    await ready();
    await user.click(screen.getByRole('radio', { name: /Card in store/i }));
    await user.type(screen.getByLabelText(/Terminal slip number/i), '4111 1111 1111 1111');
    expect(await screen.findByText(/looks like a card number/i)).toBeInTheDocument();
    expect(screen.queryByText(/4111/)).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Record payment' }));
    await new Promise((r) => setTimeout(r, 200));
    expect(sent).toBe(0);
  });

  it('U-C5/U-C6: a retry after a network error reuses the same key; success renews it', async () => {
    const user = userEvent.setup();
    const keys: Array<string | null> = [];
    server.use(
      http.post('*/api/staff/billing/payments', ({ request }) => {
        keys.push(request.headers.get('Idempotency-Key'));
        if (keys.length === 1) return HttpResponse.error();
        return HttpResponse.json(
          { paymentId: 'p1', status: 'Accepted', receiptNumber: 'R-000200', applied: [], unallocated: money('100.00'), invoiceOutstanding: [] },
          { status: 201 },
        );
      }),
    );
    renderWithClient(<RecordPaymentForm customerId={ids.customerId} />);
    await ready();
    await user.type(screen.getByLabelText(/^Amount \(BDT\)/), '100');
    await user.click(screen.getByRole('button', { name: 'Record payment' }));
    await screen.findByText(/Could not reach the server|Something went wrong/i);
    await user.click(screen.getByRole('button', { name: 'Record payment' }));
    await screen.findByText(/receipt R-000200/i);
    expect(keys).toHaveLength(2);
    expect(keys[0]).toBeTruthy();
    expect(keys[0]).toBe(keys[1]);
  });

  it('U-C8: the split-tender loop pre-fills the server outstanding and ends at Invoice paid', async () => {
    const user = userEvent.setup();
    renderWithClient(<RecordPaymentForm customerId={ids.customerId} invoiceIds={[ids.issuedInvoiceId]} />);
    await ready();
    const amount = screen.getByLabelText(/^Amount \(BDT\)/);
    await waitFor(() => expect(amount).toHaveValue('920.00'));
    await user.clear(amount);
    await user.type(amount, '400');
    const row = screen.getByLabelText(/Amount for INV-MAIN-2627-000101/i);
    await user.clear(row);
    await user.type(row, '400');
    await user.click(screen.getByRole('button', { name: 'Record payment' }));
    await user.click(await screen.findByRole('button', { name: 'Take another payment' }));
    await waitFor(() => expect(screen.getByLabelText(/^Amount \(BDT\)/)).toHaveValue('520.00'));
    await user.click(screen.getByRole('button', { name: 'Record payment' }));
    expect(await screen.findByText('Invoice paid.')).toBeInTheDocument();
  });

  it('U-C7: a payment above the limit is pending, with the explanation and no loop', async () => {
    const user = userEvent.setup();
    renderWithClient(<RecordPaymentForm customerId={ids.customerId} />);
    await ready();
    await user.type(screen.getByLabelText(/^Amount \(BDT\)/), '60000');
    await user.click(screen.getByRole('button', { name: 'Record payment' }));
    expect(await screen.findByText(/accountant must approve/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Take another payment' })).not.toBeInTheDocument();
  });
});

describe('ApprovalsQueue — U-C9', () => {
  beforeEach(() => {
    resetBillingMockData();
    setUrl('/staff/billing/approvals');
  });

  it('disables Approve with the reason for a payment the user recorded; a forced request is mapped', async () => {
    renderWithClient(<ApprovalsQueue />);
    expect(await screen.findByText('You recorded this payment')).toBeInTheDocument();
    const approves = screen.getAllByRole('button', { name: /^Approve/ });
    expect(approves.filter((b) => (b as HTMLButtonElement).disabled)).toHaveLength(1);
    const res = await fetch(`${process.env.NEXT_PUBLIC_API_BASE_URL}/api/staff/billing/payments/${ids.pendingOwnPaymentId}/approve`, { method: 'POST' });
    expect(res.status).toBe(400);
    const body = (await res.json()) as { errors: Array<{ errorCode: string }> };
    expect(body.errors[0]?.errorCode).toBe('ApproverMustDiffer');
  });
});

describe('InvoiceActions — U-B2 (ADR-FE-083)', () => {
  it('renders only the listed actions and never an edit control', () => {
    const invoice = {
      invoiceId: 'i1',
      customerId: 'c1',
      allowedActions: ['pdf', 'creditNote'],
      creditNotes: [],
      correctedByInvoiceId: null,
    } as never;
    render(<InvoiceActions invoice={invoice} onApplyCredit={() => undefined} onCorrectedReissue={() => undefined} />);
    expect(screen.getByRole('link', { name: /Credit note/ })).toBeInTheDocument();
    expect(screen.queryByText(/Record payment/)).not.toBeInTheDocument();
    expect(screen.queryByText(/beditb/i)).not.toBeInTheDocument();
  });
});

describe('RecordPaymentForm — allocations are keyed by invoice id (code review HIGH)', () => {
  beforeEach(() => {
    resetBillingMockData();
    setUrl('/staff/billing/payments/new');
  });

  it('after a refetch reorders or changes the list, the submitted ids are the ones the rows show', async () => {
    const user = userEvent.setup();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 0 }, mutations: { retry: false } } });
    let body: { allocations: Array<{ invoiceId: string; amount: unknown }> } | null = null;
    server.use(
      http.post('*/api/staff/billing/payments', async ({ request }) => {
        body = (await request.json()) as typeof body;
        return HttpResponse.json(
          { paymentId: 'p9', status: 'Accepted', receiptNumber: 'R-000900', applied: [], unallocated: money('0.00'), invoiceOutstanding: [] },
          { status: 201 },
        );
      }),
    );
    render(
      <QueryClientProvider client={client}>
        <RecordPaymentForm customerId={ids.customerId} />
      </QueryClientProvider>,
    );
    await ready();
    const first = await screen.findByRole('checkbox', { name: /INV-MAIN-2627-000102/ });
    await user.click(first);
    const amountInput = await screen.findByLabelText(/Amount for INV-MAIN-2627-000102/);
    await user.type(amountInput, '100');
    // another cashier changes things: the list now comes back reversed and without INV-…105
    const inv = (n: string, id: string) => ({
      invoiceId: id, number: n, status: 'Issued', customerId: ids.customerId, customerName: 'x', branchId: 'b', issueDate: '2026-10-01', dueOn: '2026-10-20',
      total: money('500.00'), paid: money('0.00'), credited: money('0.00'), outstanding: money('500.00'), version: 1,
    });
    server.use(
      http.get('*/api/staff/billing/invoices', () =>
        HttpResponse.json({
          items: [inv('INV-MAIN-2627-000101', ids.issuedInvoiceId), inv('INV-MAIN-2627-000102', ids.partPaidInvoiceId)],
          pageNumber: 1, pageSize: 100, totalCount: 2,
        }),
      ),
    );
    await client.invalidateQueries({ queryKey: ['billing', 'customer'] });
    await waitFor(() => expect(screen.getByRole('checkbox', { name: /INV-MAIN-2627-000102/ })).toBeChecked());
    await user.type(screen.getByLabelText(/^Amount \(BDT\)/), '100');
    await user.click(screen.getByRole('button', { name: 'Record payment' }));
    await screen.findByText(/receipt R-000900/i);
    expect(body!.allocations).toHaveLength(1);
    expect(body!.allocations[0]!.invoiceId).toBe(ids.partPaidInvoiceId);
  });
});
