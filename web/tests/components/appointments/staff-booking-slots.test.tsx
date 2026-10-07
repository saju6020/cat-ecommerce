import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '../../../mocks/server';
import { resetAppointmentsMockData } from '../../../mocks/handlers/appointments';
import { StaffBookingView } from '@/app/(staff)/staff/appointments/new/StaffBookingView';
import { StaffBookingForm } from '@/components/appointments/StaffBookingForm';
import type { StaffSlot } from '@/lib/api/staff-appointments';
import { serveStaffCustomers } from '../front-desk/helpers';
import { setUrl } from '../booking/nav-mock';
import { openMockDate, renderWithClient } from '../booking/helpers';

vi.mock('next/navigation', () => import('../booking/nav-mock'));

const CONSULT = 's0000000-0000-0000-0000-00000000a001';

describe('StaffBookingView — W7 a taken slot is deselected', () => {
  beforeEach(() => {
    resetAppointmentsMockData();
    serveStaffCustomers();
    setUrl('/staff/appointments/new');
  });

  it('after SlotNoLongerAvailable the time is cleared, so the next Book asks for a time instead of opening an empty confirm', async () => {
    const user = userEvent.setup();
    server.use(
      http.post('*/api/staff/appointments', () =>
        HttpResponse.json(
          {
            isValid: false,
            errors: [
              {
                propertyName: 'StartsOn',
                errorCode: 'SlotNoLongerAvailable',
                errorMessage: 'That time is no longer available.',
                resourceName: 'Clinic',
              },
            ],
          },
          { status: 409 },
        ),
      ),
    );
    renderWithClient(<StaffBookingView />);
    await user.type(screen.getByLabelText(/find the customer/i), '01799999999{Enter}');
    await user.click(await screen.findByRole('button', { name: /sam rahman/i }));
    await user.selectOptions(await screen.findByLabelText('Service'), CONSULT);
    fireEvent.change(screen.getByLabelText(/^date/i), { target: { value: openMockDate(8) } });
    await waitFor(() =>
      expect(screen.getByLabelText('Vet or nurse').children.length).toBeGreaterThan(1),
    );
    await user.selectOptions(screen.getByLabelText('Vet or nurse'), 'Dr Rahman');
    const slot = screen.getByLabelText(/^time/i) as HTMLSelectElement;
    await waitFor(() => expect(within(slot).getAllByRole('option').length).toBeGreaterThan(3));
    await user.selectOptions(slot, within(slot).getAllByRole('option')[3]!);
    expect(slot.value).not.toBe('');

    await user.click(screen.getByRole('button', { name: /^book$/i }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: /^book$/i }));
    expect(await screen.findByText(/someone else has just booked that time/i)).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await waitFor(() => expect(slot.value).toBe(''));

    await user.click(screen.getByRole('button', { name: /^book$/i }));
    expect(await screen.findByText('Choose a time.')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});

describe('StaffBookingForm — W18 slot keys include the vet', () => {
  const base: Omit<StaffSlot, 'performerId' | 'performerName' | 'endsOn'> = {
    startsOn: '2026-10-12T04:00:00Z',
    roomId: 'r1',
    roomName: 'Consultation 1',
    outsideOpeningHours: false,
  };
  const slots: StaffSlot[] = [
    { ...base, endsOn: '2026-10-12T04:30:00Z', performerId: 'p1', performerName: 'Dr Rahman' },
    { ...base, endsOn: '2026-10-12T04:45:00Z', performerId: 'p2', performerName: 'Dr Akter' },
  ];

  it('two vets offering the same start and room render without duplicate React keys, and each is selectable', async () => {
    const user = userEvent.setup();
    const onBook = vi.fn().mockResolvedValue(undefined);
    const errors = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    render(
      <StaffBookingForm
        customer={{
          id: 'cu1',
          fullName: 'Sam Rahman',
          mobileNumber: '01799999999',
          noShowCount: 0,
        }}
        onSelectCustomer={vi.fn()}
        onClearCustomer={vi.fn()}
        onSearch={vi.fn()}
        results={[]}
        isSearching={false}
        searchedTerm=""
        cats={[{ id: 'cat1', name: 'Mitu' }]}
        catHeader={null}
        services={[{ id: 'sv1', name: 'Consultation' }]}
        performers={[
          { id: 'p1', name: 'Dr Rahman' },
          { id: 'p2', name: 'Dr Akter' },
        ]}
        slots={slots}
        slotsLoading={false}
        slotsError={false}
        zone="Asia/Dhaka"
        initialDate="2026-10-12"
        initialPerformerId="p1"
        onQueryChange={vi.fn()}
        onSlotTaken={vi.fn()}
        onBook={onBook}
      />,
    );
    await user.selectOptions(screen.getByLabelText('Service'), 'sv1');
    const select = screen.getByLabelText(/^time/i) as HTMLSelectElement;
    const options = within(select).getAllByRole('option');
    expect(options).toHaveLength(3); // "Choose…" + one per slot
    await user.selectOptions(select, options[2]!);
    expect(select.selectedOptions[0]!.textContent).toMatch(/10:00–10:45/);

    const keyWarnings = errors.mock.calls.filter((call) =>
      /same key|unique "key"/i.test(String(call[0]) + String(call[1] ?? '')),
    );
    errors.mockRestore();
    expect(keyWarnings).toHaveLength(0);

    // The filter says Dr Rahman, but the slot picked is Dr Akter's: that vet is what is booked.
    await user.click(screen.getByRole('button', { name: /^book$/i }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: /^book$/i }));
    await waitFor(() => expect(onBook).toHaveBeenCalledTimes(1));
    expect(onBook).toHaveBeenCalledWith(expect.objectContaining({ performerId: 'p2' }));
  });
});
