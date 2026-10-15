import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { server } from '../../../mocks/server';
import { billingIds as ids, resetBillingMockData } from '../../../mocks/handlers/billing';
import { InvoiceComposer } from '@/components/billing/InvoiceComposer';
import { InvoiceDetailScreen } from '@/components/billing/InvoiceDetailScreen';
import { CashUpScreen } from '@/components/billing/CashUpScreen';
import { BankMatchView } from '@/components/billing/BankMatchView';
import { StatementUpload } from '@/components/billing/StatementUpload';
import { MyInvoiceDetail } from '@/components/billing/MyInvoiceDetail';
import { ApiValidationError } from '@/lib/api/client';
import { setUrl } from '../booking/nav-mock';
import { todayInZone } from '@/lib/format/time';

vi.mock('next/navigation', () => import('../booking/nav-mock'));

function renderWithClient(ui: ReactElement) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: 0 }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

beforeEach(() => {
  resetBillingMockData();
  setUrl('/staff/billing');
});

describe('InvoiceComposer — U-A', () => {
  it('U-A1/U-A3: groups by business line, selects a group, shows the estimate banner and tax rate', async () => {
    const user = userEvent.setup();
    renderWithClient(<InvoiceComposer customerId={ids.customerId} />);
    expect(await screen.findByRole('heading', { name: 'Clinic' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Shop' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Boarding' })).toBeInTheDocument();
    await user.click(await screen.findByRole('button', { name: 'Select all in Shop' }));
    expect(screen.getByLabelText(/Royal Canin/)).toBeChecked();
    expect(screen.getByLabelText(/Consultation/)).not.toBeChecked();
    expect(await screen.findByText(/Estimate at today.s tax rates/)).toBeInTheDocument();
    expect(await screen.findByText('15.00%')).toBeInTheDocument();
  });

  it('U-A5: the issue confirm names the customer and total and says it cannot change', async () => {
    const user = userEvent.setup();
    renderWithClient(<InvoiceComposer customerId={ids.customerId} initialSelected={[ids.chargeConsultId]} />);
    const issue = await screen.findByRole('button', { name: 'Issue invoice' });
    await waitFor(() => expect(issue).toBeEnabled());
    await user.click(issue);
    expect(await screen.findByText(/Issue invoice for BDT 920\.00 to .* It gets the next number and can no longer be changed\./)).toBeInTheDocument();
  });

  it('U-A4: a charge taken meanwhile is marked and unticked', async () => {
    const user = userEvent.setup();
    renderWithClient(<InvoiceComposer customerId={ids.customerId} initialSelected={[ids.chargeRacyId]} />);
    const save = await screen.findByRole('button', { name: 'Save draft' });
    await waitFor(() => expect(save).toBeEnabled());
    await user.click(save);
    expect(await screen.findByText('Already on another invoice')).toBeInTheDocument();
    expect(screen.getByLabelText(/Nail trim/)).not.toBeChecked();
  });
});

describe('InvoiceDetailScreen — U-B1', () => {
  it('shows the seller snapshot, tax class, rate, net, tax and gross per line', async () => {
    renderWithClient(<InvoiceDetailScreen invoiceId={ids.partPaidInvoiceId} />);
    expect(await screen.findByText(/BIN 004512345-0101/)).toBeInTheDocument();
    const table = screen.getByRole('table', { name: 'Invoice lines' });
    for (const header of ['Business line', 'Tax class', 'Rate %', 'Net', 'Tax', 'Gross']) {
      expect(within(table).getByRole('columnheader', { name: header })).toBeInTheDocument();
    }
    expect(within(table).getByText('15.00%')).toBeInTheDocument();
  });
});

describe('CashUpScreen — U-D', () => {
  it('U-D1/U-D2: a short count needs an explanation; closing is refused with pending approvals', async () => {
    const user = userEvent.setup();
    setUrl(`/staff/billing/cash-up?date=${todayInZone('Asia/Dhaka')}`);
    renderWithClient(<CashUpScreen />);
    expect(await screen.findByText(/still waiting for approval/)).toBeInTheDocument();
    await user.type(await screen.findByLabelText(/Counted cash/), '2940');
    await user.click(screen.getByRole('button', { name: 'Save count' }));
    expect(await screen.findByText(/Short BDT 10\.00/)).toBeInTheDocument();
    expect(screen.getByLabelText(/^Explanation/)).toBeRequired();
    await user.click(screen.getByRole('button', { name: 'Close day' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Close day' }));
    await waitFor(() => expect(screen.getAllByText(/waiting for approval/).length).toBeGreaterThanOrEqual(2));
  });

  it('U-D3: a closed day is read-only with who and when', async () => {
    const d = new Date();
    d.setUTCDate(d.getUTCDate() - 1);
    setUrl(`/staff/billing/cash-up?date=${todayInZone('Asia/Dhaka').replace(/\d{2}$/, '')}`);
    const { addDaysIso } = await import('@/lib/format/appointment');
    setUrl(`/staff/billing/cash-up?date=${addDaysIso(todayInZone('Asia/Dhaka'), -1)}`);
    renderWithClient(<CashUpScreen />);
    expect(await screen.findByText(/closed and read-only/i)).toBeInTheDocument();
    expect(screen.getByText(/Rezwan Ahmed/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Save count' })).not.toBeInTheDocument();
  });
});

describe('Bank — U-E', () => {
  it('U-E3: select a line, select a payment, Match — by keyboard — and the lists shrink', { timeout: 30000 }, async () => {
    const user = userEvent.setup();
    renderWithClient(<BankMatchView statementId={ids.statementId} />);
    const lineButton = await screen.findByRole('button', { name: /Selects*statement line 2/ }, { timeout: 5000 });
    lineButton.focus();
    await user.keyboard('{Enter}');
    expect(screen.getByText(/Selected statement line 2, BDT 1,000\.00/)).toBeInTheDocument();
    const payButtons = screen.getAllByRole('button', { name: /Selects*payment R-000120/ });
    payButtons[0]!.focus();
    await user.keyboard('{Enter}');
    await user.click(screen.getByRole('button', { name: /Match the selected pair/ }));
    expect(await screen.findByText(/Matched line 2/)).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText(/Unmatched statement lines \(2\)/)).toBeInTheDocument());
  });

  it('U-E2: malformed rows are listed by row number', async () => {
    const user = userEvent.setup();
    server.use(
      http.post('*/api/staff/billing/bank-statements', () =>
        HttpResponse.json(
          { errors: [3, 5].map((n) => ({ propertyName: 'file', errorCode: 'StatementMalformed', errorMessage: `Row ${n}: The date must be written yyyy-MM-dd.`, resourceName: 'Billing' })), isValid: false },
          { status: 400 },
        ),
      ),
    );
    const onImport = vi.fn().mockImplementation(async () => {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_BASE_URL}/api/staff/billing/bank-statements`, { method: 'POST' });
      throw new ApiValidationError(await res.json());
    });
    render(<StatementUpload onImport={onImport} onImported={() => undefined} />);
    await user.type(screen.getByLabelText(/Bank account/), 'City');
    await user.type(screen.getByLabelText(/Period from/), '2026-10-01');
    await user.type(screen.getByLabelText(/Period to/), '2026-10-31');
    await user.upload(screen.getByLabelText(/Statement file/), new File(['date,amount,direction\nx'], 's.csv', { type: 'text/csv' }));
    await user.click(screen.getByRole('button', { name: 'Import statement' }));
    expect(await screen.findByText(/Row 3:/)).toBeInTheDocument();
    expect(screen.getByText(/Row 5:/)).toBeInTheDocument();
  });
});

describe('Portal — U-F1', () => {
  it('shows How to pay with the bank accounts and reference, and no pay-online control', async () => {
    renderWithClient(<MyInvoiceDetail invoiceId={ids.issuedInvoiceId} />);
    expect(await screen.findByRole('heading', { name: 'How to pay' })).toBeInTheDocument();
    expect(screen.getByText(/Use your invoice number INV-MAIN-2627-000101 as the payment reference/)).toBeInTheDocument();
    expect(screen.getByText('City Bank')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /pay now|pay online/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /pay now|pay online/i })).not.toBeInTheDocument();
  });

  it('E-F1: someone else\'s invoice is a not-found state', async () => {
    renderWithClient(<MyInvoiceDetail invoiceId={ids.otherCustomerInvoiceId} />);
    expect(await screen.findByText('Invoice not found')).toBeInTheDocument();
  });
});

describe('InvoiceComposer retry and edits — code review HIGH', () => {
  function record() {
    const calls: Array<{ method: string; path: string; body: string }> = [];
    const listener = async ({ request }: { request: Request }) => {
      if (!request.url.includes('/api/staff/billing/invoices')) return;
      calls.push({ method: request.method, path: new URL(request.url).pathname, body: await request.clone().text() });
    };
    server.events.on('request:start', listener);
    return { calls, stop: () => server.events.removeListener('request:start', listener) };
  }

  it('create succeeds, issue fails, the selection changes, issue again: ONE draft, its charges updated, the real version issued', async () => {
    const user = userEvent.setup();
    const rec = record();
    let issues = 0;
    server.use(
      http.post('*/api/staff/billing/invoices/:id/issue', ({ params }) => {
        issues += 1;
        if (issues === 1) return HttpResponse.error();
        return HttpResponse.json({ invoiceId: params.id, number: 'INV-MAIN-2627-000900' });
      }),
    );
    renderWithClient(<InvoiceComposer customerId={ids.customerId} initialSelected={[ids.chargeConsultId]} />);
    const issue = await screen.findByRole('button', { name: 'Issue invoice' });
    await waitFor(() => expect(issue).toBeEnabled());
    await user.click(issue);
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Issue invoice' }));
    await screen.findByRole('alert');
    // the selection changes after the failed issue
    await user.click(await screen.findByLabelText(/Vaccination/));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Issue invoice' })).toBeEnabled());
    await user.click(screen.getByRole('button', { name: 'Issue invoice' }));
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Issue invoice' }));
    await waitFor(() => expect(issues).toBe(2));
    rec.stop();
    const creates = rec.calls.filter((c) => c.method === 'POST' && c.path.endsWith('/invoices'));
    const puts = rec.calls.filter((c) => c.method === 'PUT' && c.path.endsWith('/charges'));
    expect(creates).toHaveLength(1);
    expect(puts).toHaveLength(1);
    expect(JSON.parse(puts[0]!.body).chargeIds.sort()).toEqual([ids.chargeConsultId, ids.chargeVaccineId].sort());
    const issueCalls = rec.calls.filter((c) => c.path.endsWith('/issue'));
    expect(JSON.parse(issueCalls[1]!.body).expectedVersion).toBe(2); // the version AFTER setCharges, not a guess
  });

  it('a saved draft saves the changed selection', async () => {
    const user = userEvent.setup();
    const rec = record();
    renderWithClient(<InvoiceDetailScreen invoiceId={ids.draftInvoiceId} />);
    await user.click(await screen.findByLabelText(/Consultation/));
    await user.click(screen.getByRole('button', { name: 'Save draft' }));
    await waitFor(() => expect(rec.calls.some((c) => c.method === 'PUT')).toBe(true));
    rec.stop();
    const put = rec.calls.find((c) => c.method === 'PUT')!;
    expect(JSON.parse(put.body).chargeIds).toContain(ids.chargeConsultId);
    expect(JSON.parse(put.body).chargeIds).toContain(ids.chargeDentalId);
  });
});

describe('U-A2 / U-X2', () => {
  it('U-A2: ticking charges makes ONE preview request after the debounce and keeps the previous totals meanwhile', async () => {
    const user = userEvent.setup();
    let previews = 0;
    server.events.on('request:start', ({ request }) => {
      if (request.url.endsWith('/invoices/preview')) previews += 1;
    });
    renderWithClient(<InvoiceComposer customerId={ids.customerId} />);
    await user.click(await screen.findByLabelText(/Consultation/));
    await user.click(screen.getByLabelText(/Vaccination/));
    await user.click(screen.getByLabelText(/Royal Canin/));
    expect(previews).toBe(0); // debounced: nothing sent while still ticking
    await screen.findByText(/Estimated total/, undefined, { timeout: 5000 });
    expect(previews).toBe(1);
    expect(screen.getByText(/Totals are up to date|Updating the totals/)).toBeInTheDocument();
    server.events.removeAllListeners();
  });

  it('U-X2: a dirty composer asks before a link navigates away', async () => {
    const user = userEvent.setup();
    renderWithClient(<InvoiceComposer customerId={ids.customerId} />);
    await user.click(await screen.findByLabelText(/Consultation/));
    await user.click(screen.getByRole('link', { name: 'Back to the account' }));
    expect(await screen.findByText(/Leave this page\?/)).toBeInTheDocument();
  });
});

describe('InvoiceComposer — unsaved ticks survive a draft refetch (code review MEDIUM)', () => {
  it('keeps the ticks and says so when the draft changes underneath', async () => {
    const user = userEvent.setup();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 0 }, mutations: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <InvoiceDetailScreen invoiceId={ids.draftInvoiceId} />
      </QueryClientProvider>,
    );
    await user.click(await screen.findByLabelText(/Consultation/));
    // someone else adds the vaccination to the draft
    const res = await fetch(`${process.env.NEXT_PUBLIC_API_BASE_URL}/api/staff/billing/invoices/${ids.draftInvoiceId}`);
    const detail = await res.json();
    const extra = { ...detail.lines[0], lineId: 'zz', chargeId: ids.chargeVaccineId, description: 'Vaccination (FVRCP) — Momo' };
    server.use(
      http.get('*/api/staff/billing/invoices/:id', () => HttpResponse.json({ ...detail, version: detail.version + 1, lines: [...detail.lines, extra] })),
    );
    await client.invalidateQueries({ queryKey: ['billing', 'invoice'] });
    expect(await screen.findByText(/Your unsaved selection is kept/)).toBeInTheDocument();
    expect(screen.getByLabelText(/Consultation/)).toBeChecked();
  });
});
