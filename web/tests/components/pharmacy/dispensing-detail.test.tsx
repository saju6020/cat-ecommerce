import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DispensingDetail } from '@/components/pharmacy/DispensingDetail';
import { getDispensing } from '@/lib/api/pharmacy';
import {
  pharmacyFixtureIds as ids,
  resetPharmacyMockData,
  setPharmacyCallerRoles,
} from '@/mocks/handlers/pharmacy';
import { renderWithClient, resetNavigation } from '../common/pharmacyTestUtils';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.setConfig({ testTimeout: 20_000 });

beforeEach(() => {
  resetPharmacyMockData();
  resetNavigation();
  setPharmacyCallerRoles(null);
});

async function detail(id: string, roles: string[]) {
  setPharmacyCallerRoles(roles);
  return getDispensing(id);
}

function view(
  dispensing: Awaited<ReturnType<typeof getDispensing>>,
  onReverse = vi.fn().mockResolvedValue(undefined),
) {
  renderWithClient(
    <DispensingDetail
      dispensing={dispensing}
      zone="Asia/Dhaka"
      catId={ids.catTigerId}
      onReverse={onReverse}
    />,
  );
  return onReverse;
}

describe('U-C11 — reversing a dispensing', () => {
  it('names the quantity, the batch and the supply; a controlled one then asks for a witness', async () => {
    const user = userEvent.setup();
    const onReverse = view(await detail(ids.dispensingControlledId, ['Vet']));
    await user.click(screen.getByRole('button', { name: /Reverse this supply/ }));
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent('Return 2.00 ml of batch BUP-77 to stock');
    expect(dialog).toHaveTextContent('undo supply 1 of RX-2026-000088');
    expect(dialog).toHaveTextContent('quarantine');
    await user.click(within(dialog).getByRole('button', { name: 'Continue to witness' }));
    expect(
      await within(dialog).findByText(/Enter the reason for the reversal/i),
    ).toBeInTheDocument();
    expect(onReverse).not.toHaveBeenCalled();

    await user.type(within(dialog).getByLabelText(/reason for the reversal/i), 'Wrong cat');
    await user.click(within(dialog).getByRole('button', { name: 'Continue to witness' }));
    const witness = await screen.findByRole('dialog', { name: /witness needed/i });
    expect(onReverse).not.toHaveBeenCalled();
    await user.type(
      within(witness).getByLabelText(/witness mobile number or email/i),
      'witness.vet@purrcare.test',
    );
    await user.type(within(witness).getByLabelText(/witness password/i), 'WitnessVet123!');
    await user.click(within(witness).getByRole('button', { name: 'Confirm and reverse' }));
    await waitFor(() => expect(onReverse).toHaveBeenCalledTimes(1));
    expect(onReverse.mock.calls[0]).toEqual([
      'Wrong cat',
      { identifier: 'witness.vet@purrcare.test', password: 'WitnessVet123!' },
    ]);
  });

  it('an administrator may reverse (owner decision 2026-10-10); reception may only reprint', async () => {
    view(await detail(ids.dispensingControlledId, ['Administrator']));
    expect(screen.getByRole('button', { name: /Reverse this supply/ })).toBeInTheDocument();
  });

  it('reception sees Reprint label but no Reverse, and there is no edit control anywhere', async () => {
    view(await detail(ids.dispensingControlledId, ['Reception']));
    expect(screen.getByRole('link', { name: /Reprint label/ })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Reverse/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /edit|delete|remove/i })).not.toBeInTheDocument();
  });

  it('shows the witness, the print count and the frozen label', async () => {
    view(await detail(ids.dispensingControlledId, ['Vet']));
    expect(screen.getByText('Wasim Nurse')).toBeInTheDocument();
    expect(screen.getByText('0 times')).toBeInTheDocument();
    expect(screen.getByRole('article', { name: /Medicine label for Tiger/ })).toBeInTheDocument();
  });
});
