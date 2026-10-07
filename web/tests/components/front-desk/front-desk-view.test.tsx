import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  resetAppointmentsMockData,
  appointmentsFixtureIds,
} from '../../../mocks/handlers/appointments';
import { FrontDeskView } from '@/app/(staff)/staff/front-desk/FrontDeskView';
import { WalkInSection } from '@/app/(staff)/staff/front-desk/WalkInSection';
import { getApiUrl } from '@/lib/api/client';
import { setUrl } from '../booking/nav-mock';
import { renderWithClient } from '../booking/helpers';
import { serveStaffCustomers } from './helpers';

vi.mock('next/navigation', () => import('../booking/nav-mock'));

describe('FrontDeskView — HO-APT-002 §11.5', () => {
  beforeEach(() => {
    resetAppointmentsMockData();
    setUrl('/staff/front-desk?tab=queue');
  });

  it('U-D2: a poll while StartDialog is open keeps the chosen values and updates the data behind it', async () => {
    const user = userEvent.setup();
    const { client } = renderWithClient(<FrontDeskView />);

    // The waiting walk-in (Urgent, no vet yet) is the first Mittens on the queue.
    const starts = await screen.findAllByRole(
      'button',
      { name: /^start mittens/i },
      { timeout: 4000 },
    );
    await user.click(starts[0]!);
    const dialog = await screen.findByRole('dialog');
    await user.selectOptions(
      await within(dialog).findByLabelText('Vet or nurse'),
      appointmentsFixtureIds.vetAkterId,
    );
    await user.selectOptions(
      within(dialog).getByLabelText('Room'),
      appointmentsFixtureIds.theatreRoomId,
    );

    // Meanwhile an emergency is registered elsewhere, and the 30 s poll lands.
    await fetch(getApiUrl('/api/staff/walk-ins'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        customerId: appointmentsFixtureIds.rahimCustomerId,
        catId: appointmentsFixtureIds.catTigerId,
        serviceId: appointmentsFixtureIds.consultationServiceId,
        triagePriority: 'Emergency',
        presentingComplaint: 'Hit by a car',
      }),
    });
    await client.invalidateQueries({ queryKey: ['staff', 'queue'] });

    await waitFor(() =>
      expect(screen.getAllByText('Emergency', { exact: true }).length).toBeGreaterThan(0),
    );
    // The data behind the dialog changed, and the dialog kept both choices.
    expect(within(dialog).getByLabelText('Vet or nurse')).toHaveValue(
      appointmentsFixtureIds.vetAkterId,
    );
    expect(within(dialog).getByLabelText('Room')).toHaveValue(appointmentsFixtureIds.theatreRoomId);
    expect(screen.getByRole('dialog')).toBe(dialog);

    // The server's refusal (a theatre is the wrong kind of room) is shown inside the dialog.
    await user.click(within(dialog).getByRole('button', { name: /start visit/i }));
    expect(await within(dialog).findByText(/not the right kind of room/i)).toBeInTheDocument();
    expect(within(dialog).getByLabelText('Vet or nurse')).toHaveValue(
      appointmentsFixtureIds.vetAkterId,
    );
  });

  it('shows Today with Check in only where the server allows it, and records a no-show behind a confirm', async () => {
    const user = userEvent.setup();
    setUrl('/staff/front-desk');
    renderWithClient(<FrontDeskView />);

    expect(
      await screen.findByRole('heading', { name: /today’s appointments/i }),
    ).toBeInTheDocument();
    const checkIns = await screen.findAllByRole('button', { name: /^check in/i });
    expect(checkIns.length).toBeGreaterThan(0);
    // The checked-in cat is on the queue, so it has no Check in button of its own.
    expect(screen.queryByRole('button', { name: /check in mittens/i })).not.toBeInTheDocument();

    // A no-show before the grace period is refused by the server, with its own message.
    const noShows = await screen.findAllByRole('button', { name: /^no-show/i });
    await user.click(noShows[noShows.length - 1]!);
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/this counts against/i)).toBeInTheDocument();
  });
});

describe('WalkInSection — HO-APT-002 §11.5, NFR-USA-2 (U-D3)', () => {
  beforeEach(() => {
    resetAppointmentsMockData();
    serveStaffCustomers();
  });

  it('U-D3: completes with Tab, Enter and Space only, and focus returns to the search field', async () => {
    const user = userEvent.setup();
    const onQueued = vi.fn();
    renderWithClient(<WalkInSection onQueued={onQueued} />);

    const tabUntil = async (matches: (el: Element | null) => boolean) => {
      for (let guard = 0; guard < 25 && !matches(document.activeElement); guard += 1) {
        await user.tab();
      }
      expect(matches(document.activeElement)).toBe(true);
    };

    // Customer search: type, Enter, Tab to the result, Enter.
    await tabUntil((el) => el?.id === 'customer-search');
    await user.keyboard('01799999999{Enter}');
    await screen.findByRole('button', { name: /sam rahman/i });
    await tabUntil(
      (el) => el?.textContent?.includes('Sam Rahman') === true && el.tagName === 'BUTTON',
    );
    await user.keyboard('{Enter}');

    // Focus moves on to the cat (the only one, Momo, is preselected) — then the service.
    await waitFor(() => expect(document.activeElement?.id).toBe('walkin-cat'));
    expect(screen.getByLabelText('Cat')).toHaveValue(appointmentsFixtureIds.catMomoId);
    await tabUntil((el) => el?.id === 'walkin-service');
    await user.selectOptions(
      screen.getByLabelText('Service'),
      appointmentsFixtureIds.consultationServiceId,
    );

    // Priority: Space on the Urgent radio (arrow to it from the focused Routine one).
    await tabUntil((el) => el instanceof HTMLInputElement && el.type === 'radio');
    await user.keyboard('{ArrowDown}');
    await user.keyboard(' ');
    expect(screen.getByRole('radio', { name: /urgent/i })).toBeChecked();

    // Complaint, then Enter on the submit button.
    await tabUntil((el) => el?.id === 'walkin-complaint');
    await user.keyboard('Sneezing');
    await tabUntil((el) => el?.textContent === 'Add to queue');
    await user.keyboard('{Enter}');

    await waitFor(() => expect(onQueued).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(document.activeElement?.id).toBe('customer-search'));
    expect(screen.getByLabelText(/find the customer/i)).toHaveValue('');
  });

  it('shows the customer’s no-show count inline as text once chosen (US-CLN-08)', async () => {
    const user = userEvent.setup();
    renderWithClient(<WalkInSection onQueued={vi.fn()} />);
    await user.type(screen.getByLabelText(/find the customer/i), '01799999999{Enter}');
    await user.click(await screen.findByRole('button', { name: /sam rahman/i }));
    expect(await screen.findByText('2 no-shows')).toBeInTheDocument();
  });

  it('registers a new customer inline, adds their first cat and queues them', async () => {
    const user = userEvent.setup();
    const onQueued = vi.fn();
    renderWithClient(<WalkInSection onQueued={onQueued} />);

    await user.click(screen.getByRole('button', { name: /new customer/i }));
    await user.type(screen.getByLabelText(/full name/i), 'Nadia Hossain');
    await user.type(screen.getByRole('textbox', { name: /^mobile number/i }), '01700000999');
    await user.click(screen.getByRole('button', { name: /register customer/i }));

    expect(await screen.findByText('Nadia Hossain')).toBeInTheDocument();
    await user.click(await screen.findByRole('button', { name: /add a cat/i }));
    await user.type(screen.getByLabelText(/cat’s name/i), 'Pepper{Enter}');
    await waitFor(() => expect(screen.getByLabelText('Cat')).toHaveDisplayValue('Pepper'));
    await user.selectOptions(
      screen.getByLabelText('Service'),
      appointmentsFixtureIds.consultationServiceId,
    );
    await user.click(screen.getByRole('button', { name: /add to queue/i }));
    await waitFor(() => expect(onQueued).toHaveBeenCalledTimes(1));
  });
});
