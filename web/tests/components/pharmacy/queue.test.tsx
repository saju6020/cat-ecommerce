import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import PharmacyPage from '@/app/(staff)/staff/pharmacy/page';
import { DispensaryQueue } from '@/components/pharmacy/DispensaryQueue';
import { PrescriptionLookup } from '@/components/pharmacy/PrescriptionLookup';
import { getPharmacyQueue } from '@/lib/api/pharmacy';
import { resetPharmacyMockData } from '@/mocks/handlers/pharmacy';
import { navigation, renderWithClient, resetNavigation } from '../common/pharmacyTestUtils';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.setConfig({ testTimeout: 20_000 });

const API = 'https://localhost:7099';

beforeEach(() => {
  resetPharmacyMockData();
  resetNavigation();
});

describe('U-C13 — the dispensary queue', () => {
  it('has both sections, a compact cat line with the high-risk flag as text, controlled as a word and the time updated', async () => {
    const queue = await getPharmacyQueue();
    renderWithClient(
      <DispensaryQueue
        queue={queue}
        zone="Asia/Dhaka"
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
      />,
    );
    expect(screen.getByRole('region', { name: 'Awaiting first supply' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Repeat requests' })).toBeInTheDocument();
    expect(screen.getByText(/^Updated \d{2}:\d{2} \(Asia\/Dhaka\)$/)).toBeInTheDocument();

    const first = screen.getByRole('region', { name: 'Awaiting first supply' });
    expect(within(first).getByText('High risk')).toBeInTheDocument(); // Tiger
    expect(within(first).getByText('Controlled')).toBeInTheDocument();
    expect(within(first).getByRole('link', { name: /RX-2026-000121/ })).toHaveAttribute(
      'href',
      expect.stringMatching(/^\/staff\/pharmacy\/dispense\//),
    );

    const repeats = screen.getByRole('region', { name: 'Repeat requests' });
    expect(within(repeats).getByText('Please have it ready after 5 pm.')).toBeInTheDocument();
    expect(within(repeats).getByRole('link', { name: /RX-2026-000095/ })).toHaveAttribute(
      'href',
      expect.stringContaining('repeatRequestId='),
    );
  });

  it('says what an empty queue means', () => {
    renderWithClient(
      <DispensaryQueue
        queue={{
          generatedOn: new Date().toISOString(),
          awaitingFirstSupply: [],
          repeatRequests: [],
        }}
        zone="Asia/Dhaka"
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
      />,
    );
    expect(screen.getByText('Nothing is waiting')).toBeInTheDocument();
    expect(screen.getByText('No repeat requests')).toBeInTheDocument();
  });
});

describe('the prescription lookup (HW-2)', () => {
  it('Enter after a scan submits; not found says which number', async () => {
    const user = userEvent.setup();
    const onFind = vi
      .fn<(n: string) => Promise<'found' | 'notFound' | 'failed'>>()
      .mockResolvedValue('notFound');
    renderWithClient(<PrescriptionLookup onFind={onFind} />);
    await user.type(screen.getByLabelText('Prescription number'), 'rx-2026-999999{Enter}');
    expect(onFind).toHaveBeenCalledWith('RX-2026-999999');
    expect(
      await screen.findByText('No prescription RX-2026-999999 was found.'),
    ).toBeInTheDocument();
  });

  it('the page goes to the dispense screen when the server finds it', async () => {
    const user = userEvent.setup();
    renderWithClient(<PharmacyPage />);
    await user.type(await screen.findByLabelText('Prescription number'), 'RX-2026-000121{Enter}');
    await waitFor(() =>
      expect(navigation.push).toHaveBeenCalledWith(
        expect.stringMatching(/^\/staff\/pharmacy\/dispense\/.+/),
      ),
    );
  });
});

describe('U-X4 — a poll never disturbs an open dialog', () => {
  it('keeps what was typed in the lookup while the queue refreshes', async () => {
    const user = userEvent.setup();
    let reads = 0;
    server.use(
      http.get(`${API}/api/staff/pharmacy/queue`, () => {
        reads += 1;
        return HttpResponse.json({
          generatedOn: new Date().toISOString(),
          awaitingFirstSupply: [],
          repeatRequests: [],
        });
      }),
    );
    const { queryClient } = renderWithClient(<PharmacyPage />);
    const field = await screen.findByLabelText('Prescription number');
    await user.type(field, 'RX-2026-00');
    await waitFor(() => expect(reads).toBeGreaterThan(0));
    const before = reads;
    await queryClient.invalidateQueries({ queryKey: ['pharmacy', 'queue'] }); // what a poll does
    await waitFor(() => expect(reads).toBeGreaterThan(before));
    expect(screen.getByLabelText('Prescription number')).toHaveValue('RX-2026-00');
  });
});
