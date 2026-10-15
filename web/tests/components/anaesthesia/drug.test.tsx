import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { DrugEntryDialog, type DrugDialogContext } from '@/components/anaesthesia/DrugEntryDialog';
import { VitalsEntryDialog } from '@/components/anaesthesia/VitalsEntryDialog';
import { surgeryFixtureIds as ids } from '@/mocks/handlers/surgeries';
import { server } from '@/mocks/server';
import { renderWithClient, resetSurgeryTest, signInAs } from '../surgery/helpers';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.setConfig({ testTimeout: 40_000 });

beforeEach(() => {
  resetSurgeryTest();
  signInAs(['Vet']);
});

const context: DrugDialogContext = {
  surgeryId: ids.surgeryInTheatreId,
  catId: ids.catBiscuitId,
  zone: 'Asia/Dhaka',
  weight: { kilograms: 5.2, recordedOn: '2026-09-01' },
  administrators: [
    { id: ids.surgeonAkterId, name: 'Dr Akter' },
    { id: ids.surgeonRahmanId, name: 'Dr Rahman' },
  ],
};

async function pickMedicine(user: ReturnType<typeof userEvent.setup>, term: string, name: RegExp) {
  await user.type(await screen.findByRole('combobox', { name: /^medicine/i }), term);
  await user.click(await screen.findByRole('option', { name }));
}

async function fillControlled(user: ReturnType<typeof userEvent.setup>) {
  await pickMedicine(user, 'Bupre', /Buprenorphine/);
  await user.selectOptions(await screen.findByLabelText('Dose unit'), 'Mg');
  await user.type(screen.getByRole('textbox', { name: 'Dose' }), '0.02');
  await user.selectOptions(screen.getByLabelText('Route'), 'Intramuscular');
  const batch = (await screen.findAllByRole('radio'))[0]!;
  await user.click(batch);
  await user.type(await screen.findByLabelText(/^Quantity drawn/), '1');
}

describe('U-C3 — a controlled drug waits for its witness', () => {
  it('cannot go on until the wastage is entered, then asks the witness before saving', async () => {
    const user = userEvent.setup();
    const posts: Array<Record<string, unknown>> = [];
    server.events.on('request:start', async ({ request }) => {
      if (request.method === 'POST' && request.url.includes('/anaesthesia-log/drugs')) {
        posts.push((await request.clone().json()) as Record<string, unknown>);
      }
    });
    renderWithClient(<DrugEntryDialog context={context} onClose={vi.fn()} />);
    await fillControlled(user);
    const next = screen.getByRole('button', { name: 'Next: witness' });
    expect(next).toBeDisabled();
    expect(screen.getByText(/Still needed: the quantity wasted \(0 if none\)/)).toBeInTheDocument();
    await user.type(screen.getByLabelText(/^Quantity wasted/), '0.5');
    expect(next).toBeEnabled();
    expect(posts).toHaveLength(0);

    await user.click(next);
    const witness = await screen.findByRole('dialog', { name: /witness needed/i });
    expect(posts).toHaveLength(0); // nothing is saved before the witness confirms
    await user.type(
      within(witness).getByLabelText(/witness mobile number or email/i),
      'witness.nurse@purrcare.test',
    );
    await user.type(within(witness).getByLabelText(/witness password/i), 'WitnessPass123!');
    await user.click(within(witness).getByRole('button', { name: 'Confirm and save entry' }));
    await waitFor(() => expect(posts).toHaveLength(1));
    expect(posts[0]).toMatchObject({
      quantityWasted: 0.5,
      quantityDrawn: 1,
      witnessIdentifier: 'witness.nurse@purrcare.test',
      witnessPassword: 'WitnessPass123!',
    });
    expect(typeof posts[0]!.entryId).toBe('string'); // the idempotency key
    server.events.removeAllListeners();
  });
});

describe('U-C4 — the witness credential is never kept', () => {
  it('leaves no password in the DOM, the query cache or the mutation cache after saving', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    const view = renderWithClient(<DrugEntryDialog context={context} onClose={onClose} />);
    await fillControlled(user);
    await user.type(screen.getByLabelText(/^Quantity wasted/), '0');
    await user.click(screen.getByRole('button', { name: 'Next: witness' }));
    const witness = await screen.findByRole('dialog', { name: /witness needed/i });
    await user.type(
      within(witness).getByLabelText(/witness mobile number or email/i),
      'witness.vet@purrcare.test',
    );
    await user.type(within(witness).getByLabelText(/witness password/i), 'WitnessVet123!');
    await user.click(within(witness).getByRole('button', { name: 'Confirm and save entry' }));
    await waitFor(() => expect(onClose).toHaveBeenCalled());

    const cache = JSON.stringify({
      queries: view.queryClient
        .getQueryCache()
        .getAll()
        .map((q) => [q.queryKey, q.state.data]),
      mutations: view.queryClient
        .getMutationCache()
        .getAll()
        .map((m) => [m.options.mutationKey, m.state.variables]),
    });
    expect(cache).not.toContain('WitnessVet123!');
    expect(cache).not.toContain('witness.vet@purrcare.test');
    expect(view.queryClient.getMutationCache().getAll()).toHaveLength(0);
    expect(window.location.href).not.toContain('Witness');
    expect(JSON.stringify({ ...window.sessionStorage })).not.toContain('WitnessVet123!');
    expect(JSON.stringify({ ...window.localStorage })).not.toContain('WitnessVet123!');
  });

  it('clears the password when the witness cancels', async () => {
    const user = userEvent.setup();
    renderWithClient(<DrugEntryDialog context={context} onClose={vi.fn()} />);
    await fillControlled(user);
    await user.type(screen.getByLabelText(/^Quantity wasted/), '0');
    await user.click(screen.getByRole('button', { name: 'Next: witness' }));
    const witness = await screen.findByRole('dialog', { name: /witness needed/i });
    await user.type(within(witness).getByLabelText(/witness password/i), 'secret-typed');
    await user.click(within(witness).getByRole('button', { name: 'Cancel' }));
    await waitFor(() =>
      expect(screen.queryByRole('dialog', { name: /witness needed/i })).not.toBeInTheDocument(),
    );
    await user.click(screen.getByRole('button', { name: 'Next: witness' }));
    const again = await screen.findByRole('dialog', { name: /witness needed/i });
    expect(within(again).getByLabelText(/witness password/i)).toHaveValue('');
  });
});

describe('U-C7 — the server’s refusals, in plain words', () => {
  it('BatchNotUsable is shown by the batch and the other values are kept', async () => {
    const user = userEvent.setup();
    server.use(
      http.post('*/api/staff/surgeries/:id/anaesthesia-log/drugs', () =>
        HttpResponse.json(
          {
            isValid: false,
            errors: [
              {
                propertyName: 'BatchId',
                errorCode: 'BatchNotUsable',
                errorMessage:
                  'This batch can’t be used — it has expired or is quarantined. Choose another.',
                resourceName: 'Surgery',
              },
            ],
          },
          { status: 400 },
        ),
      ),
    );
    renderWithClient(<DrugEntryDialog context={context} onClose={vi.fn()} />);
    await pickMedicine(user, 'Melox', /Meloxicam/);
    await user.selectOptions(await screen.findByLabelText('Dose unit'), 'Mg');
    await user.type(screen.getByRole('textbox', { name: 'Dose' }), '0.2');
    const route = screen.getByLabelText('Route') as HTMLSelectElement;
    await user.selectOptions(route, route.options[1]!.value);
    await user.click((await screen.findAllByRole('radio'))[0]!);
    await user.type(await screen.findByLabelText(/^Quantity drawn/), '0.1');
    await user.click(screen.getByRole('button', { name: 'Save drug entry' }));
    expect(
      await screen.findByText(/This batch can’t be used — it has expired or is quarantined/),
    ).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Dose' })).toHaveValue('0.2');
    expect(screen.getByLabelText(/^Quantity drawn/)).toHaveValue('0.1');
    expect(screen.queryByText('BatchNotUsable')).not.toBeInTheDocument();
  });

  it('a witness refusal stays in the witness dialog and clears the password', async () => {
    const user = userEvent.setup();
    server.use(
      http.post('*/api/staff/surgeries/:id/anaesthesia-log/drugs', () =>
        HttpResponse.json(
          {
            isValid: false,
            errors: [
              {
                propertyName: 'WitnessIdentifier',
                errorCode: 'WitnessNotVerified',
                errorMessage:
                  'The witness could not be verified. Ask them to enter their own sign-in details.',
                resourceName: 'Surgery',
              },
            ],
          },
          { status: 400 },
        ),
      ),
    );
    renderWithClient(<DrugEntryDialog context={context} onClose={vi.fn()} />);
    await fillControlled(user);
    await user.type(screen.getByLabelText(/^Quantity wasted/), '0');
    await user.click(screen.getByRole('button', { name: 'Next: witness' }));
    const witness = await screen.findByRole('dialog', { name: /witness needed/i });
    await user.type(
      within(witness).getByLabelText(/witness mobile number or email/i),
      'someone@else.test',
    );
    await user.type(within(witness).getByLabelText(/witness password/i), 'wrong-password');
    await user.click(within(witness).getByRole('button', { name: 'Confirm and save entry' }));
    expect(
      await within(witness).findByText(/The witness could not be verified/),
    ).toBeInTheDocument();
    expect(within(witness).getByLabelText(/witness password/i)).toHaveValue('');
  });
});

describe('U-C6 — implausible vitals', () => {
  it('marks the field the server named and Save anyway resends with the flag', async () => {
    const user = userEvent.setup();
    const bodies: Array<Record<string, unknown>> = [];
    server.events.on('request:start', async ({ request }) => {
      if (request.method === 'POST' && request.url.endsWith('/anaesthesia-log/vitals')) {
        bodies.push((await request.clone().json()) as Record<string, unknown>);
      }
    });
    renderWithClient(
      <VitalsEntryDialog
        surgeryId={ids.surgeryInTheatreId}
        catId={ids.catBiscuitId}
        zone="Asia/Dhaka"
        onClose={vi.fn()}
      />,
    );
    await user.type(await screen.findByLabelText(/^Heart rate/), '400');
    await user.click(screen.getByRole('button', { name: 'Save vitals' }));
    expect(await screen.findByText(/Heart rate of 400 looks unlikely/)).toBeInTheDocument();
    const anyway = screen.getByRole('checkbox', { name: /Save anyway/ });
    expect(screen.getByRole('button', { name: 'Save vitals' })).toBeDisabled();
    await user.click(anyway);
    await user.click(screen.getByRole('button', { name: 'Save vitals' }));
    await waitFor(() => expect(bodies).toHaveLength(2));
    expect(bodies[0]!.confirmImplausible).toBeUndefined();
    expect(bodies[1]!.confirmImplausible).toBe(true);
    server.events.removeAllListeners();
  });

  it('shows each field’s unit beside it (UI-8)', async () => {
    renderWithClient(
      <VitalsEntryDialog
        surgeryId={ids.surgeryInTheatreId}
        catId={ids.catBiscuitId}
        zone="Asia/Dhaka"
        onClose={vi.fn()}
      />,
    );
    for (const [label, unit] of [
      ['Heart rate', 'bpm'],
      ['Temperature', '°C'],
      ['SpO2', '%'],
      ['Systolic pressure', 'mmHg'],
    ]) {
      expect(await screen.findByLabelText(new RegExp(`^${label}.*${unit}`))).toBeInTheDocument();
    }
  });
});
