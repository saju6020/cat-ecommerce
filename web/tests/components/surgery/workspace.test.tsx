import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SurgeryWorkspace } from '@/components/surgery/SurgeryWorkspace';
import { surgeryFixtureIds as ids } from '@/mocks/handlers/surgeries';
import { renderWithClient, resetSurgeryTest, signInAs } from './helpers';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.setConfig({ testTimeout: 40_000 });

beforeEach(() => {
  resetSurgeryTest();
  window.location.hash = '';
});

async function open(id: string, roles: string[]) {
  signInAs(roles);
  renderWithClient(<SurgeryWorkspace surgeryId={id} />);
  await screen.findByRole('heading', { level: 1 });
}

describe('tabs follow the status, and the URL hash', () => {
  it.each([
    [ids.surgeryScheduledTodayId, 'Pre-op'],
    [ids.surgeryInTheatreId, 'Anaesthesia'],
    [ids.surgeryRecoveryId, 'Recovery'],
    [ids.surgeryOwnerAfterId, 'Discharge'],
  ])('opens %s on the %s tab', async (id, tab) => {
    await open(id, ['Vet']);
    expect(await screen.findByRole('tab', { name: tab, selected: true })).toBeInTheDocument();
  });

  it('a hash chooses the tab', async () => {
    window.location.hash = '#history';
    await open(ids.surgeryInTheatreId, ['Vet']);
    expect(await screen.findByRole('tab', { name: 'History', selected: true })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Status history' })).toBeInTheDocument();
  });
});

describe('the header', () => {
  it('shows the stepper with the current step in words', async () => {
    await open(ids.surgeryInTheatreId, ['Vet']);
    const stepper = screen.getByRole('list', { name: 'Progress' });
    expect(within(stepper).getByText('Current').closest('li')).toHaveTextContent('In theatre');
    expect(within(stepper).getAllByText('(done)').length).toBe(2);
  });

  it('shows the price as information with its basis', async () => {
    await open(ids.surgeryLockedPricesId, ['Reception']);
    expect(
      await screen.findByText(/Campaign price, for information — paid at the clinic/),
    ).toBeInTheDocument();
    expect(screen.getByText(/Eligibility: Community cat/)).toBeInTheDocument();
  });

  it('shows the emergency override banner with the vet, the time and the reason', async () => {
    await open(ids.surgeryOverriddenId, ['Nurse']);
    expect(
      await screen.findByText(
        /Started without recorded consent — emergency override by Dr Rahman at \d\d:\d\d: Owner unreachable/,
      ),
    ).toBeInTheDocument();
  });

  it('offers Reschedule and Cancel only while the server allows them', async () => {
    await open(ids.surgeryScheduledTodayId, ['Reception']);
    expect(await screen.findByRole('button', { name: 'Reschedule' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cancel surgery' })).toBeInTheDocument();
  });

  it('offers neither once the surgery is in theatre', async () => {
    await open(ids.surgeryInTheatreId, ['Reception']);
    await screen.findByRole('tab', { name: 'Anaesthesia' });
    expect(screen.queryByRole('button', { name: 'Reschedule' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Cancel surgery' })).not.toBeInTheDocument();
  });

  it('a nurse cannot reschedule or cancel (the server’s Surgery.Schedule)', async () => {
    await open(ids.surgeryScheduledTodayId, ['Nurse']);
    await screen.findByRole('tab', { name: 'Pre-op' });
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Mark issued' })).toBeInTheDocument(),
    );
    expect(screen.queryByRole('button', { name: 'Cancel surgery' })).not.toBeInTheDocument();
  });

  it('cancelling names what is affected, needs a reason, and says the owner is told', async () => {
    const user = userEvent.setup();
    await open(ids.surgeryScheduledTodayId, ['Reception']);
    await user.click(await screen.findByRole('button', { name: 'Cancel surgery' }));
    const dialog = await screen.findByRole('dialog', { name: 'Cancel this surgery?' });
    expect(dialog).toHaveTextContent(
      /Cancel Pepper’s spay on .* with Dr Rahman\? The owner will be told\./,
    );
    await user.click(within(dialog).getByRole('button', { name: 'Cancel surgery' }));
    expect(await within(dialog).findByText(/Enter the reason for cancelling/i)).toBeInTheDocument();
    await user.type(
      within(dialog).getByLabelText(/Reason for cancelling/),
      'Owner called to move it',
    );
    await user.click(within(dialog).getByRole('button', { name: 'Cancel surgery' }));
    expect(await screen.findByText(/This surgery was cancelled/)).toBeInTheDocument();
  });
});

describe('E-B4 at component level — reception sees no clinical write controls', () => {
  it('has no start, drug, report or override control on a surgery in theatre', async () => {
    await open(ids.surgeryInTheatreId, ['Reception']);
    await screen.findByText('Anaesthesia log');
    for (const name of [
      /Add drug/,
      /Add vitals/,
      /Add milestone/,
      /Start surgery/,
      /emergency override/i,
      /End procedure/,
    ]) {
      expect(screen.queryByRole('button', { name })).not.toBeInTheDocument();
    }
    expect(screen.queryByRole('button', { name: /^Correct/ })).not.toBeInTheDocument();
  });

  it('a nurse can log vitals and milestones but not a drug', async () => {
    await open(ids.surgeryInTheatreId, ['Nurse']);
    expect(await screen.findByRole('button', { name: /Add vitals/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Add milestone/ })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Add drug/ })).not.toBeInTheDocument();
  });

  it('a vet can log a drug', async () => {
    await open(ids.surgeryInTheatreId, ['Vet']);
    expect(await screen.findByRole('button', { name: /Add drug/ })).toBeInTheDocument();
  });
});

describe('states', () => {
  it('says plainly when the surgery cannot be found', async () => {
    signInAs(['Vet']);
    renderWithClient(<SurgeryWorkspace surgeryId="00000000-0000-4000-8000-00000000dead" />);
    expect(
      await screen.findByRole('heading', { name: /couldn’t find that surgery/ }),
    ).toBeInTheDocument();
  });

  it('a surgery of another branch is the same not-found', async () => {
    signInAs(['Vet']);
    renderWithClient(<SurgeryWorkspace surgeryId={ids.surgeryLondonId} />);
    expect(
      await screen.findByRole('heading', { name: /couldn’t find that surgery/ }),
    ).toBeInTheDocument();
  });
});
