import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { orderingIds as ids } from '../../../mocks/handlers/ordering/fixtures';
import { resetOrderingMockData } from '../../../mocks/handlers/ordering/store';
import { PackingSlipPage } from '@/components/fulfilment/PackingSlipPage';
import { PickListPage } from '@/components/fulfilment/PickListPage';
import { setUrl } from '../booking/nav-mock';

vi.mock('next/navigation', () => import('../booking/nav-mock'));

function renderWithClient(ui: ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 0 }, mutations: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

beforeEach(() => {
  resetOrderingMockData();
  setUrl('/staff/fulfilment/pick-list');
});

describe('Pick list - U-E1', () => {
  it('groups the stock by location, then lists batches with expiry, quantity and the orders that need them', async () => {
    renderWithClient(<PickListPage />);
    const groups = await screen.findAllByRole('heading', { level: 2 });
    expect(groups.length).toBeGreaterThan(0);
    const firstGroup = groups[0]!.closest('section')!;
    const table = within(firstGroup).getByRole('table');
    const headers = within(table).getAllByRole('columnheader').map((h) => h.textContent);
    expect(headers).toEqual(['Product', 'Batch', 'Expires', 'Quantity', 'Orders']);
    const rows = within(table).getAllByRole('row').slice(1);
    expect(rows.length).toBeGreaterThan(0);
    expect(within(rows[0]!).getAllByRole('cell')[4]).toHaveTextContent(/ORD-2026-\d{6} \(\d+\)/);
    // The table is reachable for keyboard users when it scrolls.
    expect(within(firstGroup).getByRole('region', { name: /Batches to pick from/ })).toHaveAttribute('tabindex', '0');
  });

  it('Start packing names how many orders it will start and moves them on', async () => {
    const user = userEvent.setup();
    renderWithClient(<PickListPage />);
    const start = await screen.findByRole('button', { name: /Start packing \d+ order/ });
    await waitFor(() => expect(start).toBeEnabled());
    await user.click(start);
    expect(await screen.findByRole('status', {}, { timeout: 5000 })).toHaveTextContent(/of \d+ started/);
  });
});

describe('Packing slip - U-E2', () => {
  it('shows the COD amount to collect, the recipient and, per line, the batch to pack', async () => {
    renderWithClient(<PackingSlipPage orderId={ids.orders.confirmedCod} />);
    expect(await screen.findByRole('heading', { name: /Packing slip — ORD-2026-000104/ })).toBeInTheDocument();
    expect(screen.getByText(/Cash on delivery — collect/)).toHaveTextContent(/\d/);
    expect(screen.getByText('Customer')).toBeInTheDocument();
    expect(screen.getByText('Deliver to')).toBeInTheDocument();
    const table = screen.getByRole('table');
    expect(within(table).getAllByRole('columnheader').map((h) => h.textContent)).toContain('Batch');
    const cells = within(table).getAllByRole('cell').map((c) => c.textContent ?? '');
    expect(cells.some((text) => /\S/.test(text))).toBe(true);
  });

  it('a pickup order says where to collect instead of an address, and shows no COD banner', async () => {
    renderWithClient(<PackingSlipPage orderId={ids.orders.packingPickup} />);
    expect(await screen.findByText('Collect at')).toBeInTheDocument();
    expect(screen.queryByText(/Cash on delivery — collect/)).not.toBeInTheDocument();
  });
});
