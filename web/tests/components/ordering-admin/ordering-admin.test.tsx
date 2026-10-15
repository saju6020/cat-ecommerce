import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { server } from '../../../mocks/server';
import { violations } from '../../../mocks/handlers/ordering/fixtures';
import { resetOrderingMockData } from '../../../mocks/handlers/ordering/store';
import { ZoneForm } from '@/components/ordering-admin/ZoneForm';
import { TaxClassForm } from '@/components/admin/TaxClassForm';
import { setUrl } from '../booking/nav-mock';

vi.mock('next/navigation', () => import('../booking/nav-mock'));

function renderWithClient(ui: ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 0 }, mutations: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

beforeEach(() => {
  resetOrderingMockData();
  setUrl('/staff/admin/delivery-zones/new');
});

async function readyForm() {
  const branch = await screen.findByLabelText(/^Branch/);
  await waitFor(() => expect(within(branch).getAllByRole('option').length).toBeGreaterThan(1));
  return branch;
}

describe('Delivery zone form - U-G1', () => {
  it('adds and removes coverage rows, and edits postal codes as a comma-separated list', async () => {
    const user = userEvent.setup();
    renderWithClient(<ZoneForm zone={null} />);
    await readyForm();
    expect(screen.getByLabelText('Area 1: district')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Add an area' }));
    await user.type(screen.getByLabelText('Area 2: district'), 'Narayanganj');
    await user.type(screen.getByLabelText('Area 2: postal codes'), '1400, 1401');
    expect(screen.getByLabelText('Area 2: postal codes')).toHaveValue('1400, 1401');
    await user.click(screen.getByRole('button', { name: 'Remove area 1' }));
    expect(screen.queryByLabelText('Area 2: district')).not.toBeInTheDocument();
    expect(screen.getByLabelText('Area 1: district')).toHaveValue('Narayanganj');
  });

  it('offers both service levels, with weight bands and delivery windows added and removed per level', async () => {
    const user = userEvent.setup();
    renderWithClient(<ZoneForm zone={null} />);
    await readyForm();
    expect(screen.getAllByLabelText(/Base charge/)).toHaveLength(2);
    expect(screen.getAllByLabelText(/Lead time/)).toHaveLength(2);
    expect(screen.queryByLabelText('Band 1: up to (grams)')).not.toBeInTheDocument();

    await user.click(screen.getAllByRole('button', { name: 'Add a weight band' })[0]!);
    await user.type(screen.getByLabelText('Band 1: up to (grams)'), '5000');
    await user.clear(screen.getByLabelText(/Band 1: extra charge/));
    await user.type(screen.getByLabelText(/Band 1: extra charge/), '40.00');
    expect(screen.getByLabelText('Band 1: up to (grams)')).toHaveValue(5000);

    await user.click(screen.getAllByRole('button', { name: 'Add a delivery window' })[1]!);
    expect(screen.getByLabelText('Window 1: from')).toHaveValue('10:00');
    expect(screen.getByLabelText('Window 1: to')).toHaveValue('13:00');
    await user.click(screen.getByRole('button', { name: 'Remove window 1' }));
    expect(screen.queryByLabelText('Window 1: from')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Remove band 1' }));
    expect(screen.queryByLabelText('Band 1: up to (grams)')).not.toBeInTheDocument();
  });

  it('sends everything typed on save', async () => {
    let body: Record<string, unknown> | undefined;
    server.use(
      http.post('*/api/admin/delivery-zones', async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({ id: 'z-new' }, { status: 201 });
      }),
    );
    const user = userEvent.setup();
    renderWithClient(<ZoneForm zone={null} />);
    const branch = await readyForm();
    await user.type(screen.getByLabelText(/^Name/), 'Gulshan');
    await user.selectOptions(branch, within(branch).getAllByRole('option')[1]!);
    await user.type(screen.getByLabelText('Area 1: district'), 'Dhaka');
    await user.click(screen.getAllByRole('button', { name: 'Add a weight band' })[0]!);
    await user.type(screen.getByLabelText('Band 1: up to (grams)'), '2000');
    await user.click(screen.getByRole('button', { name: 'Save zone' }));
    await waitFor(() => expect(body).toBeDefined());
    expect(body).toMatchObject({ name: 'Gulshan', coverage: [{ district: 'Dhaka' }] });
    const services = body!.services as Array<{ serviceLevel: string; weightBands: Array<{ upToGrams: number }> }>;
    expect(services.map((s) => s.serviceLevel)).toEqual(['Standard', 'Express']);
    expect(services[0]!.weightBands[0]!.upToGrams).toBe(2000);
  });

  it('shows a server rejection on its field and keeps everything typed (U-X1)', async () => {
    server.use(
      http.post('*/api/admin/delivery-zones', () =>
        violations([
          { propertyName: 'Name', errorCode: 'DeliveryZoneNameTaken', errorMessage: 'A zone with this name already exists.' },
          { propertyName: 'Coverage', errorCode: 'DeliveryZoneCoverageRequired', errorMessage: 'Cover at least one district.' },
        ]),
      ),
    );
    const user = userEvent.setup();
    renderWithClient(<ZoneForm zone={null} />);
    const branch = await readyForm();
    await user.type(screen.getByLabelText(/^Name/), 'Dhaka central');
    await user.selectOptions(branch, within(branch).getAllByRole('option')[1]!);
    await user.click(screen.getByRole('button', { name: 'Save zone' }));
    expect(await screen.findByText('A zone with this name already exists.')).toBeInTheDocument();
    expect(screen.getByText('Cover at least one district.')).toBeInTheDocument();
    expect(screen.getByLabelText(/^Name/)).toHaveValue('Dhaka central');
    expect(document.body.textContent).not.toMatch(/DeliveryZoneNameTaken|DeliveryZoneCoverageRequired/);
  });

  it('Test an address names the serving zone, or says none serves it', async () => {
    server.use(
      http.get('*/api/admin/delivery-zones/resolve', ({ request }) => {
        const district = new URL(request.url).searchParams.get('district');
        return district === 'Dhaka'
          ? HttpResponse.json({ matched: true, zoneId: 'z1', zoneName: 'Dhaka central' })
          : HttpResponse.json({ matched: false, zoneId: null, zoneName: null });
      }),
    );
    const user = userEvent.setup();
    renderWithClient(<ZoneForm zone={null} />);
    await readyForm();
    const test = screen.getByRole('button', { name: 'Test' });
    expect(test).toBeDisabled();
    await user.type(screen.getByLabelText(/^District/), 'Dhaka');
    await user.click(test);
    expect(await screen.findByText(/Served by .Dhaka central./)).toBeInTheDocument();
    await user.clear(screen.getByLabelText(/^District/));
    await user.type(screen.getByLabelText(/^District/), 'Atlantis');
    await user.click(test);
    expect(await screen.findByText(/No zone serves this address/)).toBeInTheDocument();
  });
});

describe('Tax-class screen - U-G5', () => {
  const save = vi.fn(async () => undefined);

  it('the DELIVERY class cannot be deactivated: the toggle is disabled, ticked and explained', async () => {
    save.mockClear();
    const user = userEvent.setup();
    render(<TaxClassForm taxClass={{ id: 't1', code: 'DELIVERY', name: 'Delivery', isActive: true }} onSave={save} isPending={false} />);
    const toggle = screen.getByRole('checkbox', { name: 'Active' });
    expect(toggle).toBeDisabled();
    expect(toggle).toBeChecked();
    expect(toggle).toHaveAccessibleDescription(/cannot be deactivated\. Change its rate instead\./);
    await user.click(toggle);
    expect(toggle).toBeChecked();
    await user.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(save).toHaveBeenCalledWith({ code: undefined, name: 'Delivery', isActive: true }));
  });

  it('any other class keeps a working toggle', async () => {
    save.mockClear();
    const user = userEvent.setup();
    render(<TaxClassForm taxClass={{ id: 't2', code: 'FOOD', name: 'Food', isActive: true }} onSave={save} isPending={false} />);
    const toggle = screen.getByRole('checkbox', { name: 'Active' });
    expect(toggle).toBeEnabled();
    expect(screen.queryByText(/cannot be deactivated/)).not.toBeInTheDocument();
    await user.click(toggle);
    await user.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(save).toHaveBeenCalledWith({ code: undefined, name: 'Food', isActive: false }));
  });
});
