import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { QueryClient } from '@tanstack/react-query';
import { server } from '../../../mocks/server';
import { DownloadButton } from '@/components/billing/DownloadButton';
import { registerQueryClient } from '@/lib/branch';
import { ACTIVE_BRANCH_KEY } from '@/lib/hooks/useActiveBranch';

vi.mock('next/navigation', () => import('../booking/nav-mock'));

describe('staff downloads send the active branch (architecture HIGH)', () => {
  beforeEach(() => {
    const client = new QueryClient();
    client.setQueryData(ACTIVE_BRANCH_KEY, 'branch-london');
    registerQueryClient(client);
    URL.createObjectURL = vi.fn(() => 'blob:mock');
    URL.revokeObjectURL = vi.fn();
  });

  it('fetches with X-Branch-Id and saves the result', async () => {
    const user = userEvent.setup();
    let branch: string | null = null;
    server.use(
      http.get('*/api/staff/billing/invoices/i1.pdf', ({ request }) => {
        branch = request.headers.get('X-Branch-Id');
        return new HttpResponse('%PDF-1.4', { headers: { 'Content-Type': 'application/pdf' } });
      }),
    );
    render(<DownloadButton path="/api/staff/billing/invoices/i1.pdf" fallbackName="x.pdf" label="Download PDF" />);
    await user.click(screen.getByRole('button', { name: 'Download PDF' }));
    await vi.waitFor(() => expect(URL.createObjectURL).toHaveBeenCalled());
    expect(branch).toBe('branch-london');
  });

  it('shows a plain-language error when the server refuses', async () => {
    const user = userEvent.setup();
    server.use(http.get('*/api/staff/billing/invoices/gone.pdf', () => new HttpResponse(null, { status: 404 })));
    render(<DownloadButton path="/api/staff/billing/invoices/gone.pdf" fallbackName="x.pdf" label="Download PDF" />);
    await user.click(screen.getByRole('button', { name: 'Download PDF' }));
    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(URL.createObjectURL).not.toHaveBeenCalled();
  });
});

describe('card numbers never reach the URL via the filters (security LOW)', () => {
  it('the invoice-number filter refuses a card number and writes nothing', async () => {
    const { InvoiceListScreen } = await import('@/components/billing/InvoiceListScreen');
    const nav = await import('../booking/nav-mock');
    nav.setUrl('/staff/billing/invoices');
    const { QueryClientProvider } = await import('@tanstack/react-query');
    render(
      <QueryClientProvider client={new QueryClient()}>
        <InvoiceListScreen />
      </QueryClientProvider>,
    );
    const user = userEvent.setup();
    await user.type(screen.getByLabelText('Invoice number'), '4111111111111111');
    expect(await screen.findByText(/looks like a card number/)).toBeInTheDocument();
    expect(nav.currentUrl()).not.toContain('4111');
  });
});
