import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import {
  buildRun,
  buildSchedule,
  erasureRequests,
  xctFixtureIds,
} from '@/mocks/handlers/operations/fixtures';
import { resetXctMock } from '@/mocks/handlers/operations';
import { RetentionScheduleTable } from '@/components/retention/RetentionScheduleTable';
import { RunRetentionNow } from '@/components/retention/RunRetentionNow';
import { RetentionRunDetail } from '@/components/retention/RetentionRunDetail';
import { ErasureLedgerDetail } from '@/components/retention/ErasureLedgerDetail';
import { ErasureRequestsTable, summariseScopes } from '@/components/retention/ErasureRequestsTable';
import { ErasePersonalDataPanel } from '@/components/retention/ErasePersonalDataPanel';
import { ReapplyAfterRestoreForm, parseCustomerIds } from '@/components/retention/ReapplyAfterRestoreForm';
import { renderWithClient } from '../offline/offlineTestUtils';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
  usePathname: () => '/staff/admin/retention',
  useSearchParams: () => new URLSearchParams(),
}));
const toast = vi.fn();
vi.mock('@/components/ui/use-toast', () => ({ toast: (...args: unknown[]) => toast(...args) }));

beforeEach(() => {
  resetXctMock();
  toast.mockReset();
});

describe('U-B1 RetentionScheduleTable', () => {
  it('shows every class with its period and end action; clinical, financial, regulatory and audit are "Review only"', () => {
    renderWithClient(<RetentionScheduleTable schedule={buildSchedule()} />);
    const rows = screen.getAllByRole('row').slice(1);
    expect(rows).toHaveLength(11);
    const rowOf = (title: string) => rows.find((r) => within(r).queryByText(title))!;
    for (const title of ['Clinical records', 'Financial records', 'Regulatory records', 'Audit trail']) {
      expect(within(rowOf(title)).getByText('Review only')).toBeInTheDocument();
    }
    expect(within(rowOf('Clinical records')).getByText('7 years')).toBeInTheDocument();
    expect(within(rowOf('Clinical records')).getByRole('link', { name: 'Change in settings' })).toHaveAttribute('href', '/staff/admin/settings');
    expect(within(rowOf('Security artefacts')).getByText('Purge')).toBeInTheDocument();
    expect(within(rowOf('Customer personal data')).getByText('Anonymise on request')).toBeInTheDocument();
    expect(screen.getByText('audit.audit_entry')).toBeInTheDocument();
    expect(screen.getAllByText(/Nothing is deleted automatically/).length).toBeGreaterThan(0);
  });
});

describe('U-B2 RunRetentionNow', () => {
  it('the confirmation says what it will and will not do', async () => {
    const user = userEvent.setup();
    renderWithClient(<RunRetentionNow />);
    await user.click(screen.getByRole('button', { name: 'Run retention now' }));
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent('It will not delete clinical, financial, regulatory or audit records');
    await user.click(within(dialog).getByRole('button', { name: 'Run retention' }));
    await waitFor(() => expect(toast).toHaveBeenCalled());
  });

  it('a 409 reads "A retention run is already in progress."', async () => {
    server.use(
      http.post('*/api/admin/retention/runs', () =>
        HttpResponse.json(
          { isValid: false, errors: [{ propertyName: 'run', errorCode: 'RetentionRunAlreadyInProgress', errorMessage: 'x', resourceName: 'r' }] },
          { status: 409 },
        ),
      ),
    );
    const user = userEvent.setup();
    renderWithClient(<RunRetentionNow />);
    await user.click(screen.getByRole('button', { name: 'Run retention now' }));
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Run retention' }));
    expect(await screen.findByText('A retention run is already in progress.')).toBeInTheDocument();
    expect(screen.queryByText(/RetentionRunAlreadyInProgress/)).toBeNull();
  });
});

describe('U-B3 run detail', () => {
  it('glosses the error codes of a CompletedWithErrors run and never shows the codes', () => {
    renderWithClient(<RetentionRunDetail run={buildRun(xctFixtureIds.errorRunId)!} zone="Asia/Dhaka" />);
    expect(screen.getByText('Finished with problems')).toBeInTheDocument();
    expect(screen.getByText(/could not finish and will be tried again/)).toBeInTheDocument();
    expect(screen.getByText(/was not set up to be purged, so it was skipped/)).toBeInTheDocument();
    expect(screen.queryByText(/ErasureScopeFailing|PurgeSourceNotRegistered/)).toBeNull();
    expect(screen.getByText(/never deleted by a run/)).toBeInTheDocument();
  });
});

describe('U-B4 erasure ledger', () => {
  it('shows the held scope with its date and basis and no personal data anywhere', () => {
    const { container } = renderWithClient(
      <ErasureLedgerDetail request={erasureRequests[0]!} zone="Asia/Dhaka" />,
    );
    expect(screen.getByText('Held until 12 Mar 2032 — NFR-LEG-5 acceptance evidence')).toBeInTheDocument();
    expect(container.textContent).not.toMatch(/@|\+88|01[3-9]\d{8}|Farhana|Imran/);
  });

  it('summarises scopes as "2 of 3 complete, 1 held until 12 Mar 2032"', () => {
    expect(summariseScopes(erasureRequests[0]!)).toBe('2 of 3 complete, 1 held until 12 Mar 2032');
  });

  it('lists accounts by id only', () => {
    const { container } = renderWithClient(
      <ErasureRequestsTable requests={erasureRequests} totalCount={3} pageSize={20} status="" onStatusChange={() => {}} isLoading={false} isError={false} onRetry={() => {}} zone="Asia/Dhaka" />,
    );
    expect(screen.getByText(xctFixtureIds.erasedCustomerId)).toBeInTheDocument();
    expect(container.textContent).not.toMatch(/@|\+88/);
  });
});

describe('U-B5 / U-B6 ErasePersonalDataPanel', () => {
  const customer = { id: 'c0000000-0000-0000-0000-0000000000aa', fullName: 'Farhana Akter', status: 'Active' as const, mergedIntoCustomerId: null };

  it('lists what is erased and what is kept with its period, and requires channel and identity check', async () => {
    const user = userEvent.setup();
    renderWithClient(<ErasePersonalDataPanel customer={customer} />);
    expect(await screen.findByText(/Clinical records for 7 years and financial\s+records for 10 years/)).toBeInTheDocument();
    expect(screen.queryByLabelText(/reason/i)).toBeNull(); // no free text (HO-XCT-001 2026-10-12)
    expect(screen.queryByRole('textbox')).toBeNull();
    await user.click(screen.getByRole('button', { name: /Erase personal data/ }));
    expect(await screen.findByText('Choose how the request was made.')).toBeInTheDocument();
    expect(screen.getByText('Choose how identity was checked.')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('the confirmation names the customer once, says it cannot be undone, and sends exactly {channel, identityCheck}', async () => {
    const sent = vi.fn();
    server.use(
      http.post('*/api/admin/customers/:id/erasure', async ({ request }) => {
        sent(await request.json());
        return HttpResponse.json({ isReapplication: false });
      }),
    );
    const user = userEvent.setup();
    renderWithClient(<ErasePersonalDataPanel customer={customer} />);
    await user.selectOptions(screen.getByLabelText('How was the request made?'), 'Phone');
    await user.selectOptions(screen.getByLabelText('How was identity checked?'), 'InPersonPhotoId');
    await user.click(screen.getByRole('button', { name: /Erase personal data…/ }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getAllByText(/Farhana Akter/)).toHaveLength(1);
    expect(dialog).toHaveTextContent('This cannot be undone.');
    await user.click(within(dialog).getByRole('button', { name: 'Erase personal data' }));
    await waitFor(() => expect(sent).toHaveBeenCalledWith({ channel: 'Phone', identityCheck: 'InPersonPhotoId' }));
    await waitFor(() => expect(toast).toHaveBeenCalled());
  });

  it('an already anonymised customer gets the erased state and a ledger link, not the form', () => {
    renderWithClient(<ErasePersonalDataPanel customer={{ ...customer, status: 'Anonymised' }} />);
    expect(screen.getByText('Personal data was erased.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'erasure ledger' })).toHaveAttribute('href', `/staff/admin/retention/erasures/${customer.id}`);
    expect(screen.queryByRole('combobox')).toBeNull();
  });

  it('CustomerRecordMerged shows the surviving-record message with a link', async () => {
    const merged = { ...customer, id: xctFixtureIds.mergedCustomerId, mergedIntoCustomerId: 'c0000000-0000-0000-0000-0000000000bb' };
    const user = userEvent.setup();
    renderWithClient(<ErasePersonalDataPanel customer={merged} />);
    await user.selectOptions(screen.getByLabelText('How was the request made?'), 'Email');
    await user.selectOptions(screen.getByLabelText('How was identity checked?'), 'RegisteredEmailReply');
    await user.click(screen.getByRole('button', { name: /Erase personal data…/ }));
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Erase personal data' }));
    expect(await screen.findByText(/Erase the surviving record instead/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Open the surviving record' })).toHaveAttribute('href', '/staff/customers/c0000000-0000-0000-0000-0000000000bb');
  });
});

describe('U-B7 ReapplyAfterRestoreForm', () => {
  const uuid = (n: number) => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`;

  it('parses one id per line, ignoring blanks and duplicates', () => {
    expect(parseCustomerIds(`${uuid(1)}\n\n${uuid(1)}\nnot-an-id\n`)).toMatchObject({ count: 2, malformed: ['not-an-id'] });
  });

  it('blocks 501 ids before anything is sent, with the count', async () => {
    const posted = vi.fn();
    server.use(http.post('*/api/admin/retention/erasures/reapply', () => { posted(); return HttpResponse.json({}); }));
    const user = userEvent.setup();
    renderWithClient(<ReapplyAfterRestoreForm />);
    await user.click(screen.getByLabelText('Customer ids'));
    await user.paste(Array.from({ length: 501 }, (_, i) => uuid(i + 1)).join('\n'));
    await user.type(screen.getByLabelText('Restore reference'), 'drill-2026-10');
    await user.click(screen.getByRole('button', { name: 'Re-apply erasures' }));
    expect(await screen.findByText(/That's 501 customers; the limit is 500/)).toBeInTheDocument();
    expect(posted).not.toHaveBeenCalled();
  });

  it('blocks malformed ids with a count', async () => {
    const user = userEvent.setup();
    renderWithClient(<ReapplyAfterRestoreForm />);
    await user.type(screen.getByLabelText('Customer ids'), `${uuid(1)}{enter}hello`);
    await user.type(screen.getByLabelText('Restore reference'), 'ref');
    await user.click(screen.getByRole('button', { name: 'Re-apply erasures' }));
    expect(await screen.findByText(/1 of 2 lines are not customer ids/)).toBeInTheDocument();
  });

  it('confirms, then shows the four counts', async () => {
    const user = userEvent.setup();
    renderWithClient(<ReapplyAfterRestoreForm />);
    await user.click(screen.getByLabelText('Customer ids'));
    await user.paste(`${uuid(1)}\n${uuid(2)}\n${uuid(3)}`);
    await user.type(screen.getByLabelText('Restore reference'), 'ref');
    await user.click(screen.getByRole('button', { name: 'Re-apply erasures' }));
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent('re-anonymises 3 accounts that came back with the restore');
    await user.click(within(dialog).getByRole('button', { name: 'Re-apply erasures' }));
    expect(await screen.findByText('Erased again')).toBeInTheDocument();
    expect(screen.getByText('Already erased')).toBeInTheDocument();
    expect(screen.getByText('Not found')).toBeInTheDocument();
    expect(screen.getByText('Merged into another customer')).toBeInTheDocument();
  });
});
