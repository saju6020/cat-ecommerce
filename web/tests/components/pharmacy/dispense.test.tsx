import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DispenseScreen } from '@/components/pharmacy/DispenseScreen';
import { getDispensePlan, type DispenseRequest, type DispenseResult } from '@/lib/api/pharmacy';
import { pharmacyFixtureIds as ids, resetPharmacyMockData } from '@/mocks/handlers/pharmacy';
import { renderWithClient, resetNavigation } from '../common/pharmacyTestUtils';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.setConfig({ testTimeout: 20_000 });

beforeEach(() => {
  resetPharmacyMockData();
  resetNavigation();
});

const RESULT: DispenseResult = {
  dispensingId: ids.dispensingMittensId,
  supplyNumber: 2,
  unpricedLines: [],
  label: {} as DispenseResult['label'],
};

async function screenFor(
  prescriptionId: string,
  onDispense = vi.fn<(r: DispenseRequest) => Promise<DispenseResult>>().mockResolvedValue(RESULT),
) {
  const plan = await getDispensePlan(prescriptionId);
  renderWithClient(
    <DispenseScreen
      plan={plan}
      repeatRequest={null}
      zone="Asia/Dhaka"
      onDispense={onDispense}
      onPlanChanged={vi.fn()}
    />,
  );
  return { plan, onDispense };
}

const row = (lot: string) => screen.getByRole('checkbox', { name: `Use lot ${lot}` });
const qty = (lot: string) =>
  screen.getByRole('textbox', { name: new RegExp(`Quantity from lot ${lot}`) });

describe('U-C1 — the server’s plan is followed', () => {
  it('preselects the suggested batches and lists the unusable ones disabled, with the reason as text', async () => {
    await screenFor(ids.rxMittensActiveId);
    expect(row('L4410')).toBeChecked();
    expect(row('L4551')).toBeChecked();
    expect(qty('L4410')).toHaveValue('6.00');
    expect(qty('L4551')).toHaveValue('4.00');
    // Expired: disabled, and the words say why.
    expect(row('L3302')).toBeDisabled();
    expect(screen.getByText(/Expired \d+ \w+ \d{4} — cannot be dispensed/)).toBeInTheDocument();
    // Quarantined: disabled, with Inventory's reason.
    expect(row('L4600')).toBeDisabled();
    expect(
      screen.getByText('Quarantined by Inventory pending a storage check.'),
    ).toBeInTheDocument();
    expect(screen.getAllByText('Suggested')).toHaveLength(2);
    expect(screen.getByText(/10\.00 of 10\.00 ml/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Dispense' })).toBeEnabled();
  });
});

describe('U-C2 — only expired stock (US-PHM-05 AC2)', () => {
  it('shows a blocking notice and has no dispense control in the DOM', async () => {
    await screenFor(ids.rxExpiredOnlyQueueId);
    const notice = screen.getByRole('alert');
    expect(notice).toHaveTextContent(/The only Maropitant tablets in the dispensary is expired/);
    expect(notice).toHaveTextContent('There is no way to override this here.');
    expect(screen.queryByRole('button', { name: /dispense/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
  });
});

describe('U-C3 — choosing away from the suggestion', () => {
  it('reveals a required reason and sends it with the batches chosen', async () => {
    const user = userEvent.setup();
    const { onDispense } = await screenFor(ids.rxMittensActiveId);
    expect(
      screen.queryByLabelText(/reason for not using the earliest-expiring batch/i),
    ).not.toBeInTheDocument();

    await user.click(row('L4410')); // drop the earliest-expiring lot
    await user.clear(qty('L4551'));
    await user.type(qty('L4551'), '10');
    const reason = await screen.findByLabelText(
      /reason for not using the earliest-expiring batch/i,
    );
    expect(screen.getByRole('button', { name: 'Dispense' })).toBeDisabled();
    expect(
      screen.getByText('Say why you are not using the earliest-expiring batch.'),
    ).toBeInTheDocument();

    await user.type(reason, 'Short-dated lot reserved for the clinic');
    await user.click(screen.getByRole('button', { name: 'Dispense' }));
    await waitFor(() => expect(onDispense).toHaveBeenCalledTimes(1));
    const request = onDispense.mock.calls[0]![0];
    expect(request.lines).toEqual([expect.objectContaining({ quantity: 10 })]);
    expect(request.fefoOverrideReason).toBe('Short-dated lot reserved for the clinic');
    expect(await screen.findByText('Dispensed — supply 2 of 3.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Print label/ })).toHaveAttribute(
      'href',
      `/staff/pharmacy/dispensings/${ids.dispensingMittensId}/label`,
    );
  });
});

describe('U-C4 — the total', () => {
  it('says how much is selected and refuses a supply that does not add up', async () => {
    const user = userEvent.setup();
    const { onDispense } = await screenFor(ids.rxMittensActiveId);
    await user.clear(qty('L4551'));
    await user.type(qty('L4551'), '2');
    expect(screen.getByText(/8\.00 of 10\.00 ml/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Dispense' })).toBeDisabled();
    expect(screen.getByText('Choose batches that add up to 10.00 ml.')).toBeInTheDocument();
    expect(onDispense).not.toHaveBeenCalled();
  });
});

describe('U-C5 — scanning a batch (HW-2)', () => {
  it('a typed lot number plus Enter selects the matching batch; an unknown or expired lot says so', async () => {
    const user = userEvent.setup();
    await screenFor(ids.rxMittensActiveId);
    const field = screen.getByLabelText('Scan batch');

    await user.type(field, 'L4551{Enter}');
    expect(await screen.findByText('Selected lot L4551.')).toBeInTheDocument();
    expect(row('L4551')).toBeChecked();
    expect(row('L4410')).not.toBeChecked();
    expect(qty('L4551')).toHaveValue('10.00');

    await user.type(field, 'ZZZ-1{Enter}');
    expect(
      await screen.findByText('Lot ZZZ-1 is not in this prescription’s batch list.'),
    ).toBeInTheDocument();

    await user.type(field, 'L3302{Enter}');
    expect(
      await screen.findByText(/Lot L3302 expired .* and cannot be dispensed\./),
    ).toBeInTheDocument();
    expect(row('L3302')).not.toBeChecked();
  });

  it('matches a barcode too', async () => {
    const user = userEvent.setup();
    const { plan } = await screenFor(ids.rxMittensActiveId);
    const barcode = plan.batches.find((b) => b.lotNumber === 'L4551')!.barcode!;
    await user.type(screen.getByLabelText('Scan batch'), `${barcode}{Enter}`);
    expect(await screen.findByText('Selected lot L4551.')).toBeInTheDocument();
  });
});

describe('a repeat request', () => {
  it('is named on the screen and sent with the supply', async () => {
    const plan = await getDispensePlan(ids.rxMittensRequestedId);
    const onDispense = vi
      .fn<(r: DispenseRequest) => Promise<DispenseResult>>()
      .mockResolvedValue(RESULT);
    const user = userEvent.setup();
    renderWithClient(
      <DispenseScreen
        plan={plan}
        repeatRequest={{
          id: ids.repeatRequestMittensId,
          note: 'Please have it ready after 5 pm.',
          requestedOn: new Date().toISOString(),
        }}
        zone="Asia/Dhaka"
        onDispense={onDispense}
        onPlanChanged={vi.fn()}
      />,
    );
    const note = screen.getByText('Repeat request from the owner');
    expect(note.closest('p')).toHaveTextContent('Please have it ready after 5 pm.');
    await user.click(screen.getByRole('button', { name: 'Dispense' }));
    await waitFor(() => expect(onDispense).toHaveBeenCalled());
    expect(onDispense.mock.calls[0]![0].repeatRequestId).toBe(ids.repeatRequestMittensId);
    expect(
      within(await screen.findByRole('status', { name: 'Dispensed' })).getByText(
        /Dispensed — supply 2 of 2\./,
      ),
    ).toBeInTheDocument();
  });
});
