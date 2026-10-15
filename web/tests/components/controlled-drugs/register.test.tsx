import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BalancesTable, IntegrityPanel } from '@/components/controlled-drugs/BalancesAndIntegrity';
import { CorrectionForm } from '@/components/controlled-drugs/CorrectionForm';
import { RegisterEntryDetail } from '@/components/controlled-drugs/RegisterEntryDetail';
import { RegisterTable } from '@/components/controlled-drugs/RegisterTable';
import {
  getRegister,
  getRegisterBalances,
  getRegisterEntry,
  getRegisterVerification,
} from '@/lib/api/controlled-drugs';
import { resetPharmacyMockData, setPharmacyCallerRoles } from '@/mocks/handlers/pharmacy';
import { renderWithClient, resetNavigation } from '../common/pharmacyTestUtils';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.setConfig({ testTimeout: 20_000 });

const ENTRY_1200 = 'eb000000-0000-4000-8000-000000000003';
const pager = { pageNumber: 1, pageSize: 25, totalCount: 7, onPageChange: vi.fn() };
const originalMatchMedia = window.matchMedia;

beforeEach(() => {
  resetPharmacyMockData();
  resetNavigation();
  setPharmacyCallerRoles(null);
});
afterEach(() => {
  window.matchMedia = originalMatchMedia;
});

describe('U-D1 — the register has no edit or delete control (ADR-FE-065)', () => {
  it('lists every kind with signed quantities and running balances and offers nothing to change', async () => {
    const page = await getRegister({ pageNumber: 1, pageSize: 25 });
    render(
      <RegisterTable
        rows={page.items}
        zone="Asia/Dhaka"
        pager={pager}
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
      />,
    );
    expect(
      screen.getByRole('table', { name: /Controlled-drug register entries/ }),
    ).toBeInTheDocument();
    for (const kind of [
      'Opening Balance',
      'Receipt',
      'Dispensed',
      'Administered',
      'Wastage',
      'Returned',
      'Correction',
    ]) {
      expect(screen.getAllByText(kind).length).toBeGreaterThan(0);
    }
    // A signed quantity and a running balance, each with its unit.
    expect(
      screen.getAllByText('−2.00 ml', { selector: '[data-clinical-value]' }).length,
    ).toBeGreaterThan(0);
    expect(
      screen.getAllByText('+10.00 ml', { selector: '[data-clinical-value]' }).length,
    ).toBeGreaterThan(0);
    expect(document.querySelectorAll('[data-clinical-value]').length).toBeGreaterThan(10);
    expect(screen.getAllByText('Sealed').length).toBeGreaterThan(0);
    expect(screen.getByText('Awaiting seal')).toBeInTheDocument();
    expect(screen.getAllByText(/Corrected by #1203/).length).toBeGreaterThan(0);
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: /edit|delete|remove|change/i }),
    ).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /edit|delete/i })).not.toBeInTheDocument();
    // The dispensing source is a link to that dispensing.
    expect(screen.getByRole('link', { name: /Dispensing RX-2026-000088 #1/ })).toHaveAttribute(
      'href',
      expect.stringMatching(/^\/staff\/pharmacy\/dispensings\//),
    );
  });
});

describe('U-D5 — below 768 px every entry is a card', () => {
  it('renders a list of cards and no table', async () => {
    window.matchMedia = ((query: string) => ({
      matches: false,
      media: query,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
    })) as unknown as typeof window.matchMedia;
    const page = await getRegister({ pageNumber: 1, pageSize: 25 });
    render(
      <RegisterTable
        rows={page.items}
        zone="Asia/Dhaka"
        pager={pager}
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
      />,
    );
    await waitFor(() => expect(screen.queryByRole('table')).not.toBeInTheDocument());
    const cards = screen.getByRole('list', { name: /Controlled-drug register entries/ });
    expect(within(cards).getAllByRole('listitem').length).toBeGreaterThanOrEqual(7);
  });
});

describe('U-D2 — the correction chain sits beneath the original, which stays as it was', () => {
  it('shows the entry, its seal and each correction with its reason; a vet may add another', async () => {
    const detail = await getRegisterEntry(ENTRY_1200);
    render(<RegisterEntryDetail detail={detail} zone="Asia/Dhaka" canCorrect />);
    expect(screen.getByRole('heading', { name: 'Register entry #1200' })).toBeInTheDocument();
    expect(screen.getByText('−2.00 ml', { selector: '[data-clinical-value]' })).toBeInTheDocument();
    expect(screen.getByText(/Chain position/)).toHaveTextContent('Chain position 3');
    const chain = screen.getByRole('region', { name: /Corrections \(1\)/ });
    expect(within(chain).getByText('Reason:')).toBeInTheDocument();
    expect(chain).toHaveTextContent('Duration was recorded as 3 days; the prescription says 5.');
    expect(within(chain).getByRole('link', { name: 'Entry #1203' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Add correction/ })).toHaveAttribute(
      'href',
      `/staff/pharmacy/controlled-drugs/${ENTRY_1200}/correct`,
    );
    expect(screen.queryByRole('button', { name: /edit|delete/i })).not.toBeInTheDocument();
  });

  it('an administrator (not a vet) is offered no Add correction', async () => {
    render(
      <RegisterEntryDetail
        detail={await getRegisterEntry(ENTRY_1200)}
        zone="Asia/Dhaka"
        canCorrect={false}
      />,
    );
    expect(screen.queryByRole('link', { name: /Add correction/ })).not.toBeInTheDocument();
  });
});

describe('U-D3 — adding a correction', () => {
  it('a non-zero quantity says it also adjusts stock; the confirmation names both entry numbers; a witness comes next', async () => {
    const user = userEvent.setup();
    const detail = await getRegisterEntry(ENTRY_1200);
    const onSubmit = vi.fn().mockResolvedValue({ entryId: 'x', entryNumber: 1206 });
    const onDone = vi.fn();
    renderWithClient(
      <CorrectionForm
        original={detail.entry}
        zone="Asia/Dhaka"
        nextEntryNumber={1206}
        cats={[]}
        onSearchCats={vi.fn()}
        onSubmit={onSubmit}
        onDone={onDone}
      />,
    );
    expect(
      screen.queryByText('This also adjusts stock by the same amount.'),
    ).not.toBeInTheDocument();
    const quantity = screen.getByRole('textbox', { name: /quantity change/i });
    await user.clear(quantity);
    await user.type(quantity, '-1');
    expect(screen.getByText('This also adjusts stock by the same amount.')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Review and add correction' }));
    expect(await screen.findByText('Give the reason for the correction.')).toBeInTheDocument();
    await user.type(screen.getByLabelText(/reason for the correction/i), 'Typo in the dose text');
    await user.click(screen.getByRole('button', { name: 'Review and add correction' }));

    const confirm = await screen.findByRole('dialog');
    expect(confirm).toHaveTextContent(
      'This adds entry #1206 correcting #1200; the original stays as it is.',
    );
    await user.click(within(confirm).getByRole('button', { name: 'Continue to witness' }));
    const witness = await screen.findByRole('dialog', { name: /witness needed/i });
    expect(onSubmit).not.toHaveBeenCalled();
    await user.type(
      within(witness).getByLabelText(/witness mobile number or email/i),
      'witness.nurse@purrcare.test',
    );
    await user.type(within(witness).getByLabelText(/witness password/i), 'WitnessPass123!');
    await user.click(within(witness).getByRole('button', { name: 'Confirm and add correction' }));
    await waitFor(() => expect(onDone).toHaveBeenCalledWith(1206));
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({
        quantityDelta: -1,
        reason: 'Typo in the dose text',
        witnessIdentifier: 'witness.nurse@purrcare.test',
      }),
    );
  });
});

describe('U-D4 — integrity, balances', () => {
  it('a broken chain is an error sentence with the position — not colour alone', () => {
    render(
      <IntegrityPanel
        zone="Asia/Dhaka"
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
        data={{
          isIntact: false,
          entriesChecked: 1204,
          firstBrokenPosition: 311,
          staleUnsealed: [],
          checkedOn: new Date().toISOString(),
        }}
      />,
    );
    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Integrity check failed at entry position 311');
  });

  it('an intact register says how many entries were checked and lists entries still awaiting a seal', async () => {
    render(
      <IntegrityPanel
        zone="Asia/Dhaka"
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
        data={await getRegisterVerification()}
      />,
    );
    expect(screen.getByRole('status')).toHaveTextContent(
      /Register intact — \d+ entries checked \d{2}:\d{2}/,
    );
    expect(screen.getByText(/#1204 — recorded .* not yet sealed/)).toBeInTheDocument();
  });

  it('a balance that differs from stock is a Mismatch in words', async () => {
    render(
      <BalancesTable
        rows={await getRegisterBalances()}
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
      />,
    );
    expect(screen.getByText('Mismatch')).toBeInTheDocument();
    expect(screen.getByText('Matches')).toBeInTheDocument();
    expect(
      screen.getAllByText('8.00 ml', { selector: '[data-clinical-value]' }).length,
    ).toBeGreaterThan(0);
  });
});
