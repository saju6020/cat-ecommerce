import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { BusyExplainer } from '@/components/surgery/BusyExplainer';
import { SurgeryBookingForm } from '@/components/surgery/SurgeryBookingForm';
import { SurgeryResourcePicker } from '@/components/surgery/SurgeryResourcePicker';
import { addDaysIso } from '@/lib/format/appointment';
import { todayInZone } from '@/lib/format/time';
import { surgeryFixtureIds as ids } from '@/mocks/handlers/surgeries';
import { server } from '@/mocks/server';
import { navigation, renderWithClient, resetSurgeryTest, signInAs } from './helpers';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.setConfig({ testTimeout: 30_000 });

const ZONE = 'Asia/Dhaka';
const DAY = () => addDaysIso(todayInZone(ZONE), 5);

beforeEach(() => {
  resetSurgeryTest();
  signInAs(['Reception']);
});

function prefill(over: Record<string, string> = {}) {
  navigation.pathname = '/staff/surgeries/new';
  navigation.search = new URLSearchParams({
    customerId: ids.customerFarhanaId,
    catId: ids.catPepperId,
    serviceId: ids.serviceSpayId,
    date: DAY(),
    surgeonId: ids.surgeonRahmanId,
    assistantId: ids.nurseTanvirId,
    theatreId: ids.theatre1Id,
    ...over,
  });
}

async function chooseFirstStart(user: ReturnType<typeof userEvent.setup>) {
  const group = await screen.findByRole(
    'group',
    { name: /available starts/i },
    { timeout: 15_000 },
  );
  const first = within(group).getAllByRole('button')[0]!;
  await user.click(first);
  expect(first).toHaveAttribute('aria-pressed', 'true');
}

const clash = (propertyName: string, errorCode: string, errorMessage: string) => ({
  propertyName,
  errorCode,
  errorMessage,
  resourceName: 'Surgery',
});

describe('U-A1 — the assistant list', () => {
  it('leaves the chosen surgeon out of the assistant options', () => {
    renderWithClient(
      <SurgeryResourcePicker
        choices={{ date: DAY(), surgeonId: 's1', assistantId: '', theatreId: '', start: '' }}
        onChange={() => undefined}
        staff={{
          surgeons: [{ id: 's1', name: 'Dr Rahman', roleIds: ['Vet'] }],
          assistants: [
            { id: 's1', name: 'Dr Rahman', roleIds: ['Vet'] },
            { id: 'n1', name: 'Nurse Tanvir', roleIds: ['Nurse'] },
          ],
        }}
        theatres={[{ id: 't1', name: 'Theatre 1' }]}
        availability={undefined}
        availabilityLoading={false}
        availabilityError={false}
        onRetry={() => undefined}
        zone={ZONE}
        errors={{}}
        nextFreeDay={undefined}
        onUseDay={() => undefined}
      />,
    );
    const assistants = within(screen.getByLabelText('Assistant'));
    expect(assistants.getByRole('option', { name: 'Nurse Tanvir' })).toBeInTheDocument();
    expect(assistants.queryByRole('option', { name: 'Dr Rahman' })).not.toBeInTheDocument();
  });
});

describe('U-A2 — why a time is missing', () => {
  it('lists each resource’s busy intervals with the server’s labels', () => {
    renderWithClient(
      <BusyExplainer
        zone={ZONE}
        busy={{
          theatre: [
            {
              startsOn: '2026-10-20T03:00:00Z',
              endsOn: '2026-10-20T04:00:00Z',
              label: 'Theatre 1: Biscuit’s spay',
            },
          ],
          surgeon: [
            {
              startsOn: '2026-10-20T04:00:00Z',
              endsOn: '2026-10-20T05:00:00Z',
              label: 'Dr Rahman: in surgery',
            },
          ],
          assistant: [
            {
              startsOn: '2026-10-20T04:00:00Z',
              endsOn: '2026-10-20T04:30:00Z',
              label: 'Nurse Tanvir: vaccination',
            },
          ],
        }}
      />,
    );
    expect(screen.getByText(/Theatre 1: Biscuit’s spay/)).toBeInTheDocument();
    expect(screen.getByText(/Dr Rahman: in surgery/)).toBeInTheDocument();
    expect(screen.getByText(/Nurse Tanvir: vaccination/)).toBeInTheDocument();
    expect(screen.getByText('09:00–10:00')).toBeInTheDocument();
  });
});

describe('U-A3 — a booking refused for several clashes at once', () => {
  it('shows every clash beside its field and in a summary that takes focus', async () => {
    prefill();
    server.use(
      http.post('*/api/staff/surgeries', () =>
        HttpResponse.json(
          {
            isValid: false,
            errors: [
              clash('theatreId', 'TheatreBusy', 'Theatre 1 is already booked from 09:00 to 10:00.'),
              clash('surgeonId', 'SurgeonBusy', 'Dr Rahman is already booked from 09:00 to 10:00.'),
              clash(
                'assistantId',
                'AssistantBusy',
                'Nurse Tanvir is already booked from 09:30 to 10:00.',
              ),
            ],
          },
          { status: 400 },
        ),
      ),
    );
    const user = userEvent.setup();
    renderWithClient(<SurgeryBookingForm zone={ZONE} />);
    await chooseFirstStart(user);
    await user.click(screen.getByRole('button', { name: 'Book surgery' }));
    const confirm = await screen.findByRole('dialog');
    await user.click(within(confirm).getByRole('button', { name: 'Book surgery' }));

    const summary = await screen.findByRole('alert', { name: /3 things need fixing/i });
    await waitFor(() => expect(summary).toHaveFocus());
    expect(within(summary).getAllByRole('listitem')).toHaveLength(3);
    expect(document.getElementById('surgery-theatre-error')).toHaveTextContent(
      'Theatre 1 is already booked',
    );
    expect(document.getElementById('surgery-surgeon-error')).toHaveTextContent(
      'Dr Rahman is already booked',
    );
    expect(document.getElementById('surgery-assistant-error')).toHaveTextContent(
      'Nurse Tanvir is already booked',
    );
  });
});

describe('U-A4 — losing the race', () => {
  it('says so and refreshes the times', async () => {
    prefill();
    let availabilityCalls = 0;
    server.events.on('request:start', ({ request }) => {
      if (request.url.includes('/api/staff/surgeries/availability')) availabilityCalls += 1;
    });
    server.use(
      http.post('*/api/staff/surgeries', () =>
        HttpResponse.json(
          {
            isValid: false,
            errors: [clash('TheatreId', 'TheatreBusy', 'The theatre has just been booked.')],
          },
          { status: 409 },
        ),
      ),
    );
    const user = userEvent.setup();
    renderWithClient(<SurgeryBookingForm zone={ZONE} />);
    await chooseFirstStart(user);
    const before = availabilityCalls;
    await user.click(screen.getByRole('button', { name: 'Book surgery' }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: 'Book surgery' }),
    );
    expect(
      await screen.findByText(
        'Someone has just booked that time. Availability has been refreshed.',
      ),
    ).toBeInTheDocument();
    await waitFor(() => expect(availabilityCalls).toBeGreaterThan(before));
    server.events.removeAllListeners();
  });
});

describe('U-A5 — a cat recorded as already neutered', () => {
  it('offers Book anyway, which resubmits with the confirmation flag', async () => {
    prefill({ catId: ids.catBiscuitId });
    const bodies: Array<Record<string, unknown>> = [];
    server.use(
      http.post('*/api/staff/surgeries', async ({ request }) => {
        const body = (await request.json()) as Record<string, unknown>;
        bodies.push(body);
        if (!body.confirmNeuterStatusDiscrepancy) {
          return HttpResponse.json(
            {
              isValid: false,
              errors: [
                clash(
                  'ConfirmNeuterStatusDiscrepancy',
                  'CatAlreadyNeutered',
                  'Biscuit is recorded as already neutered.',
                ),
              ],
            },
            { status: 400 },
          );
        }
        return HttpResponse.json({ surgeryId: ids.surgeryTomorrowId }, { status: 201 });
      }),
    );
    const user = userEvent.setup();
    renderWithClient(<SurgeryBookingForm zone={ZONE} />);
    await chooseFirstStart(user);
    await user.click(screen.getByRole('button', { name: 'Book surgery' }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: 'Book surgery' }),
    );
    const book = await screen.findByRole('button', { name: /Book anyway/ });
    await user.click(book);
    await waitFor(() => expect(bodies).toHaveLength(2));
    expect(bodies[0]!.confirmNeuterStatusDiscrepancy).toBeUndefined();
    expect(bodies[1]!.confirmNeuterStatusDiscrepancy).toBe(true);
    await waitFor(() =>
      expect(navigation.push).toHaveBeenCalledWith(`/staff/surgeries/${ids.surgeryTomorrowId}`),
    );
  });
});

describe('U-A6 — a campaign needs its eligibility note', () => {
  it('blocks the booking with a field error until the note is written', async () => {
    prefill({
      serviceId: ids.serviceCastrationId,
      catId: ids.catNoodleId,
      customerId: ids.customerKamrulId,
    });
    let posted = 0;
    server.use(
      http.post('*/api/staff/surgeries', () => {
        posted += 1;
        return HttpResponse.json({ surgeryId: ids.surgeryTomorrowId }, { status: 201 });
      }),
    );
    const user = userEvent.setup();
    renderWithClient(<SurgeryBookingForm zone={ZONE} />);
    await chooseFirstStart(user);
    await user.selectOptions(await screen.findByLabelText('Price'), ids.campaignOutreachId);
    await user.click(screen.getByRole('button', { name: 'Book surgery' }));
    expect(
      await screen.findByText('Note why this cat is eligible for the campaign.'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(posted).toBe(0);
  });
});

describe('U-A7 — no payment step (ADR-170)', () => {
  it('shows the price for information and has no payment control', async () => {
    prefill();
    renderWithClient(<SurgeryBookingForm zone={ZONE} />);
    expect(await screen.findByText(/for information — paid at the clinic/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /pay|deposit|card/i })).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/deposit|card number/i)).not.toBeInTheDocument();
  });
});
