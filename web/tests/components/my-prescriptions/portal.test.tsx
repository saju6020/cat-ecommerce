import * as React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import MyPrescriptionPage from '@/app/(portal)/portal/prescriptions/[prescriptionId]/page';
import PortalPrescriptionsPage from '@/app/(portal)/portal/prescriptions/page';
import { MyPrescriptionsList } from '@/components/my-prescriptions/MyPrescriptionsList';
import { listMyPrescriptions } from '@/lib/api/my-prescriptions';
import { pharmacyFixtureIds as ids, resetPharmacyMockData } from '@/mocks/handlers/pharmacy';
import { navigation, resetNavigation, resolvedParams } from '../common/pharmacyTestUtils';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.setConfig({ testTimeout: 20_000 });

beforeEach(() => {
  resetPharmacyMockData();
  resetNavigation();
  navigation.pathname = '/portal/prescriptions';
});

function renderPage(element: React.ReactElement) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <React.Suspense fallback={null}>{element}</React.Suspense>
    </QueryClientProvider>,
  );
}

const detail = (id: string) =>
  renderPage(<MyPrescriptionPage params={resolvedParams({ prescriptionId: id })} />);

describe('U-F1 — my prescriptions (US-PHM-07)', () => {
  it('each active row shows the medicine, dose and frequency, days left, the vet and the repeat status', async () => {
    renderPage(<PortalPrescriptionsPage />);
    const list = await screen.findByRole('list', { name: 'Prescriptions' });
    const rows = within(list).getAllByRole('listitem');
    const meloxicam = rows.find((row) => row.textContent?.includes('Meloxicam'))!;
    expect(meloxicam).toHaveTextContent('Mittens');
    expect(meloxicam).toHaveTextContent('1.5 mg per 1 ml');
    expect(meloxicam).toHaveTextContent('0.30 ml once daily');
    expect(within(meloxicam).getByText('5 days left')).toBeInTheDocument();
    expect(meloxicam).toHaveTextContent('Prescribed by: Dr Akter');
    expect(meloxicam).toHaveTextContent(/2 repeats left, until \d+ \w+ \d{4}/);
    const amoxicillin = rows.find((row) => row.textContent?.includes('Amoxicillin'))!;
    expect(amoxicillin).toHaveTextContent(/Repeat requested \d+ \w+ \d{4}/);
    // The ended prescription is not Active.
    expect(rows.some((row) => row.textContent?.includes('Fusidic'))).toBe(false);
    expect(screen.queryByText(/lot/i)).not.toBeInTheDocument();
  });

  it('says so when there is nothing active, and the Past tab pages 20 at a time', async () => {
    const rows = await listMyPrescriptions({ scope: 'active' });
    const many = Array.from({ length: 25 }, (_, index) => ({ ...rows[0]!, id: `p-${index}` }));
    const onPageChange = vi.fn();
    const { unmount } = render(
      <MyPrescriptionsList
        rows={many}
        zone="Asia/Dhaka"
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
        emptyTitle="x"
        emptyDescription="y"
        page={1}
        pageSize={20}
        onPageChange={onPageChange}
      />,
    );
    expect(screen.getAllByRole('listitem')).toHaveLength(20);
    await userEvent.setup().click(screen.getByRole('button', { name: 'Next' }));
    expect(onPageChange).toHaveBeenCalledWith(2);
    unmount();
    render(
      <MyPrescriptionsList
        rows={[]}
        zone="Asia/Dhaka"
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
        emptyTitle="No active prescriptions."
        emptyDescription="y"
        page={1}
        pageSize={20}
        onPageChange={vi.fn()}
      />,
    );
    expect(screen.getByText('No active prescriptions.')).toBeInTheDocument();
  });
});

describe('U-F2 — not possible, and why (US-PHM-06 AC2)', () => {
  it('an ended validity shows the server’s sentence verbatim and a consultation link', async () => {
    detail(ids.rxMittensEndedId);
    const reason = await screen.findByText(/This prescription could be repeated until/);
    expect(reason).toHaveTextContent(
      'A vet needs to authorise a new prescription -- please book a consultation or call the clinic.',
    );
    expect(screen.queryByRole('button', { name: /Request a repeat/ })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Book a consultation' })).toHaveAttribute(
      'href',
      `/book?catId=${ids.catMittensId}`,
    );
  });

  it('an open request shows Withdraw, not Request', async () => {
    detail(ids.rxMittensRequestedId);
    expect(await screen.findByRole('button', { name: 'Withdraw request' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Request a repeat/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Book a consultation' })).not.toBeInTheDocument();
  });
});

describe('U-F3 — asking for a repeat (US-PHM-06 AC1)', () => {
  it('opens a dialog, sends the branch and note, says it is requested and then offers Withdraw', async () => {
    const user = userEvent.setup();
    detail(ids.rxMittensActiveId);
    await user.click(await screen.findByRole('button', { name: /Request a repeat/ }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Request repeat' }));
    expect(
      await within(dialog).findByText('Choose where you will collect it.'),
    ).toBeInTheDocument();
    await waitFor(() => expect(within(dialog).getAllByRole('option').length).toBeGreaterThan(1));
    await user.selectOptions(
      within(dialog).getByLabelText(/collect from/i),
      within(dialog).getAllByRole('option')[1]!,
    );
    await user.type(within(dialog).getByLabelText(/note for the pharmacy/i), 'After 5 pm please');
    await user.click(within(dialog).getByRole('button', { name: 'Request repeat' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(
      await screen.findByText('Requested. We’ll message you when it’s ready to collect.'),
    ).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'Withdraw request' })).toBeInTheDocument();
  });

  it('a refusal at send time is shown inside the dialog in the server’s words', async () => {
    const user = userEvent.setup();
    detail(ids.rxMittensActiveId);
    await user.click(await screen.findByRole('button', { name: /Request a repeat/ }));
    const dialog = await screen.findByRole('dialog');
    await waitFor(() => expect(within(dialog).getAllByRole('option').length).toBeGreaterThan(1));
    await user.selectOptions(
      within(dialog).getByLabelText(/collect from/i),
      within(dialog).getAllByRole('option')[1]!,
    );
    // A request already exists by the time this one arrives.
    await fetch(
      'https://localhost:7099/api/customers/me/prescriptions/' +
        ids.rxMittensActiveId +
        '/repeat-requests',
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ preferredBranchId: ids.mainBranchId }),
      },
    );
    await user.click(within(dialog).getByRole('button', { name: 'Request repeat' }));
    expect(
      await within(dialog).findByText('A repeat has already been requested.'),
    ).toBeInTheDocument();
  });
});

describe('U-F4 — the refill reminder’s link (?action=repeat)', () => {
  it('opens the dialog on load when a repeat is allowed', async () => {
    navigation.search = new URLSearchParams('action=repeat');
    detail(ids.rxMittensActiveId);
    expect(await screen.findByRole('dialog')).toHaveTextContent(
      'Request a repeat of Meloxicam oral suspension',
    );
  });

  it('does not open when a repeat is refused', async () => {
    navigation.search = new URLSearchParams('action=repeat');
    detail(ids.rxMittensEndedId);
    await screen.findByText(/This prescription could be repeated until/);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});

describe('U-F6 — the owner never sees a batch', () => {
  it('shows when, where and how much was collected — not a lot number', async () => {
    detail(ids.rxMittensActiveId);
    const history = await screen.findByRole('region', { name: 'Collected so far' });
    expect(history).toHaveTextContent('Uttara');
    expect(history).toHaveTextContent('10.00 ml');
    expect(document.body).not.toHaveTextContent('L4551');
    expect(document.body).not.toHaveTextContent(/\blot\b/i);
  });
});

describe('U-F5 — authorised foods (US-PHM-08)', () => {
  it('shows the active food with where to buy it and the expired one with when it ended', async () => {
    navigation.search = new URLSearchParams('tab=diets');
    renderPage(<PortalPrescriptionsPage />);
    const list = await screen.findByRole('list', { name: 'Authorised foods' });
    await waitFor(() => expect(within(list).getAllByRole('listitem')).toHaveLength(2));
    const rows = within(list).getAllByRole('listitem');
    const active = rows.find((row) => row.textContent?.includes('Renal Care'))!;
    expect(active).toHaveTextContent('Active');
    expect(active).toHaveTextContent('Authorised until');
    expect(active).toHaveTextContent('You can buy this for Mittens in the shop.');
    const expired = rows.find((row) => row.textContent?.includes('Urinary Support'))!;
    expect(expired).toHaveTextContent('Expired');
    expect(expired).toHaveTextContent(/Ended on \d+ \w+ \d{4}/);
    expect(within(expired).getByRole('link', { name: 'Book a consultation' })).toHaveAttribute(
      'href',
      `/book?catId=${ids.catMittensId}`,
    );
  });
});
