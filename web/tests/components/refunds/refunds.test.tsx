import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { server } from '../../../mocks/server';
import { RefundDialog } from '@/components/refunds/RefundDialog';
import type { RefundOptions } from '@/lib/api/staff-refunds';

vi.mock('next/navigation', () => import('../booking/nav-mock'));

function renderWithClient(ui: ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 0 }, mutations: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const money = (amount: string) => ({ amount, currencyCode: 'BDT' });

const OPTIONS: RefundOptions = {
  orderId: 'o1',
  maxRefundable: money('1500.00'),
  methods: [
    { method: 'CashInStore', allowed: false, available: true, unavailableReason: null },
    { method: 'BankTransfer', allowed: true, available: true, unavailableReason: null },
    { method: 'StoreCredit', allowed: true, available: false, unavailableReason: 'StoreCreditNotAvailable' },
  ],
  suggestedForReturns: [{ returnRequestId: 'r1', suggestedAmount: money('900.00') }],
};

function serveOptions(options: RefundOptions = OPTIONS) {
  server.use(http.get('*/api/staff/orders/o1/refund-options', () => HttpResponse.json(options)));
}

function open(extra: Partial<Parameters<typeof RefundDialog>[0]> = {}) {
  return renderWithClient(<RefundDialog orderId="o1" orderNumber="ORD-2026-000150" open onOpenChange={() => undefined} {...extra} />);
}

describe('RefundDialog - U-E6', () => {
  it('offers only the allowed methods, with an allowed-but-unavailable one disabled and explained', async () => {
    serveOptions();
    open();
    const dialog = await screen.findByRole('dialog', { name: 'Issue a refund' });
    await within(dialog).findByText(/Most that can be refunded/);
    expect(within(dialog).queryByRole('radio', { name: /Cash/ })).not.toBeInTheDocument();
    expect(within(dialog).getByRole('radio', { name: /Bank transfer/ })).toBeEnabled();
    const credit = within(dialog).getByRole('radio', { name: /Store credit/ });
    expect(credit).toBeDisabled();
    expect(credit.closest('label')).toHaveTextContent(/—\s*\S+/);
  });

  it('pre-fills the amount from the server: the maximum, or the suggestion for a chosen return', async () => {
    serveOptions();
    const user = userEvent.setup();
    open();
    const dialog = await screen.findByRole('dialog');
    const amount = await within(dialog).findByLabelText(/^Amount/);
    await waitFor(() => expect(amount).toHaveValue('1500.00'));
    await user.selectOptions(within(dialog).getByLabelText('For a received return'), 'r1');
    await waitFor(() => expect(amount).toHaveValue('900.00'));
  });

  it('asks for a method and a reason, then restates the method and amount before sending', async () => {
    serveOptions();
    let body: unknown;
    let key: string | null = null;
    server.use(
      http.post('*/api/staff/orders/o1/refunds', async ({ request }) => {
        body = await request.json();
        key = request.headers.get('Idempotency-Key');
        return HttpResponse.json({ refundId: 'rf1', creditNoteId: null });
      }),
    );
    const onDone = vi.fn();
    const user = userEvent.setup();
    open({ onDone });
    const dialog = await screen.findByRole('dialog');
    await within(dialog).findByText(/Most that can be refunded/);
    await user.click(within(dialog).getByRole('button', { name: 'Review' }));
    expect(await within(dialog).findByText('Choose how the refund is paid.')).toBeInTheDocument();

    await user.click(within(dialog).getByRole('radio', { name: /Bank transfer/ }));
    await user.click(within(dialog).getByRole('button', { name: 'Review' }));
    expect(await within(dialog).findByText('Enter a reason for the refund.')).toBeInTheDocument();
    expect(body).toBeUndefined();

    await user.type(within(dialog).getByLabelText(/^Reason/), 'Damaged on arrival');
    await user.click(within(dialog).getByRole('button', { name: 'Review' }));
    expect(await within(dialog).findByText(/Refund .*1,?500\.00 by bank transfer for ORD-2026-000150\?/)).toBeInTheDocument();
    expect(body).toBeUndefined();
    await user.click(within(dialog).getByRole('button', { name: 'Issue refund' }));
    await waitFor(() => expect(onDone).toHaveBeenCalled());
    expect(body).toMatchObject({ method: 'BankTransfer', reason: 'Damaged on arrival' });
    expect(key).toMatch(/[0-9a-f-]{36}/);
  });

  it('says so when no method is allowed for the order', async () => {
    serveOptions({ ...OPTIONS, methods: OPTIONS.methods.map((m) => ({ ...m, allowed: false })) });
    open();
    expect(await screen.findByText('No refund method is allowed for this order.')).toBeInTheDocument();
  });

  it('a failed options load says so instead of showing an empty form', async () => {
    server.use(http.get('*/api/staff/orders/o1/refund-options', () => HttpResponse.json({ title: 'x' }, { status: 500 })));
    open();
    expect(await screen.findByText(/We couldn.t load the refund options/, undefined, { timeout: 5000 })).toBeInTheDocument();
  });
});
