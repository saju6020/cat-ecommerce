import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DispenseScreen } from '@/components/pharmacy/DispenseScreen';
import { WitnessDialog } from '@/components/pharmacy/WitnessDialog';
import { ApiValidationError } from '@/lib/api/client';
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
  window.sessionStorage.clear();
  window.localStorage.clear();
});

const RESULT: DispenseResult = {
  dispensingId: ids.dispensingControlledId,
  supplyNumber: 1,
  unpricedLines: [],
  label: {} as DispenseResult['label'],
};

const notVerified = new ApiValidationError({
  isValid: false,
  errors: [
    {
      propertyName: 'WitnessIdentifier',
      errorCode: 'WitnessNotVerified',
      errorMessage:
        'The witness could not be verified. Ask them to enter their own sign-in details.',
      resourceName: 'Pharmacy',
    },
  ],
});

async function controlled(onDispense: (r: DispenseRequest) => Promise<DispenseResult>) {
  const plan = await getDispensePlan(ids.rxTigerControlledQueueId);
  return renderWithClient(
    <DispenseScreen
      plan={plan}
      repeatRequest={null}
      zone="Asia/Dhaka"
      onDispense={onDispense}
      onPlanChanged={vi.fn()}
    />,
  );
}

describe('U-C6 — a controlled drug asks for a witness before any request', () => {
  it('opens the witness dialog first; its fields refuse autofill and it names the roles', async () => {
    const user = userEvent.setup();
    const onDispense = vi
      .fn<(r: DispenseRequest) => Promise<DispenseResult>>()
      .mockResolvedValue(RESULT);
    await controlled(onDispense);
    await user.click(await screen.findByRole('button', { name: /Dispense \(witness needed\)/ }));

    const dialog = await screen.findByRole('dialog');
    expect(onDispense).not.toHaveBeenCalled();
    expect(dialog).toHaveTextContent('Controlled drug — a second member of staff must witness.');
    expect(dialog).toHaveTextContent('enter your own sign-in details');
    // U-C12 — the owner decision, said out loud.
    expect(dialog).toHaveTextContent('vet or nurse');
    expect(dialog).toHaveTextContent('must not be the person dispensing');
    expect(within(dialog).getByLabelText(/witness mobile number or email/i)).toHaveAttribute(
      'autocomplete',
      'off',
    );
    expect(within(dialog).getByLabelText(/witness password/i)).toHaveAttribute(
      'autocomplete',
      'off',
    );
    // No override, no skip.
    expect(
      within(dialog).queryByRole('button', { name: /skip|override|without/i }),
    ).not.toBeInTheDocument();
  });
});

describe('U-C7 — a refused witness', () => {
  it('shows the server’s message in the dialog, clears the password and refocuses the identifier', async () => {
    const user = userEvent.setup();
    const onDispense = vi
      .fn<(r: DispenseRequest) => Promise<DispenseResult>>()
      .mockRejectedValue(notVerified);
    await controlled(onDispense);
    await user.click(await screen.findByRole('button', { name: /Dispense \(witness needed\)/ }));
    const dialog = await screen.findByRole('dialog');
    await user.type(
      within(dialog).getByLabelText(/witness mobile number or email/i),
      'someone@purrcare.test',
    );
    await user.type(within(dialog).getByLabelText(/witness password/i), 'not-the-password');
    await user.click(within(dialog).getByRole('button', { name: 'Confirm and dispense' }));

    expect(
      await within(dialog).findByText(
        'The witness could not be verified. Ask them to enter their own sign-in details.',
      ),
    ).toBeInTheDocument();
    expect(within(dialog).getByLabelText(/witness password/i)).toHaveValue('');
    await waitFor(() =>
      expect(within(dialog).getByLabelText(/witness mobile number or email/i)).toHaveFocus(),
    );
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });
});

describe('U-C8 — nothing about the witness is kept', () => {
  it('cancel clears both fields; success clears them and leaves no trace in storage, the URL or the query cache', async () => {
    const user = userEvent.setup();
    const onDispense = vi
      .fn<(r: DispenseRequest) => Promise<DispenseResult>>()
      .mockResolvedValue(RESULT);
    const { queryClient } = await controlled(onDispense);

    await user.click(await screen.findByRole('button', { name: /Dispense \(witness needed\)/ }));
    let dialog = await screen.findByRole('dialog');
    await user.type(
      within(dialog).getByLabelText(/witness mobile number or email/i),
      'witness.nurse@purrcare.test',
    );
    await user.type(within(dialog).getByLabelText(/witness password/i), 'WitnessPass123!');
    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /Dispense \(witness needed\)/ }));
    dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByLabelText(/witness mobile number or email/i)).toHaveValue('');
    expect(within(dialog).getByLabelText(/witness password/i)).toHaveValue('');

    await user.type(
      within(dialog).getByLabelText(/witness mobile number or email/i),
      'witness.nurse@purrcare.test',
    );
    await user.type(within(dialog).getByLabelText(/witness password/i), 'WitnessPass123!');
    await user.click(within(dialog).getByRole('button', { name: 'Confirm and dispense' }));
    await waitFor(() => expect(onDispense).toHaveBeenCalledTimes(1));
    expect(onDispense.mock.calls[0]![0]).toMatchObject({
      witnessIdentifier: 'witness.nurse@purrcare.test',
      witnessPassword: 'WitnessPass123!',
    });
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());

    const secrets = ['WitnessPass123!', 'witness.nurse@purrcare.test'];
    const where = [
      JSON.stringify({ ...window.sessionStorage }),
      JSON.stringify({ ...window.localStorage }),
      window.location.href,
      JSON.stringify(
        queryClient
          .getQueryCache()
          .getAll()
          .map((q) => [q.queryKey, q.state.data]),
      ),
      JSON.stringify(
        queryClient
          .getMutationCache()
          .getAll()
          .map((m) => m.state.variables),
      ),
      document.body.innerHTML,
    ].join('\n');
    for (const secret of secrets) expect(where).not.toContain(secret);
  });

  it('closing the dialog from outside (unmount) leaves nothing behind', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    const { rerender, queryClient } = renderWithClient(
      <WitnessDialog
        open
        witnessRoles={['Vet', 'Nurse']}
        busy={false}
        onCancel={vi.fn()}
        onSubmit={onSubmit}
      />,
    );
    await user.type(screen.getByLabelText(/witness password/i), 'secret-secret');
    rerender(
      <WitnessDialog
        open={false}
        witnessRoles={['Vet', 'Nurse']}
        busy={false}
        onCancel={vi.fn()}
        onSubmit={onSubmit}
      />,
    );
    rerender(
      <WitnessDialog
        open
        witnessRoles={['Vet', 'Nurse']}
        busy={false}
        onCancel={vi.fn()}
        onSubmit={onSubmit}
      />,
    );
    expect(screen.getByLabelText(/witness password/i)).toHaveValue('');
    expect(JSON.stringify(queryClient.getQueryCache().getAll())).not.toContain('secret-secret');
  });
});
