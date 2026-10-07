import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { server } from '../../../mocks/server';
import {
  appointmentsFixtureIds as ids,
  resetAppointmentsMockData,
} from '../../../mocks/handlers/appointments';
import { findAppointment } from '../../../mocks/handlers/appointments/store';
import { MyAppointmentView } from '@/app/(portal)/portal/appointments/[appointmentId]/MyAppointmentView';
import { FlaggedView } from '@/app/(staff)/staff/appointments/flagged/FlaggedView';
import { CalendarView } from '@/app/(staff)/staff/appointments/CalendarView';
import { FrontDeskView } from '@/app/(staff)/staff/front-desk/FrontDeskView';
import { WaitlistSection } from '@/components/appointments/WaitlistSection';
import { zonedLocalToInstant } from '@/lib/format/appointment';
import type { WaitlistEntry } from '@/lib/api/my-appointments';
import { openMockDate, renderWithClient } from '../booking/helpers';
import { setUrl } from '../booking/nav-mock';

vi.mock('next/navigation', () => import('../booking/nav-mock'));

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-/i;

describe('MyAppointmentView — W5, W16', () => {
  beforeEach(() => resetAppointmentsMockData());

  it('W5: the reschedule picker opens on the BRANCH-local day of the appointment, not its UTC day', async () => {
    const user = userEvent.setup();
    // 02:00 in Dhaka on `day` is 20:00 UTC on the day BEFORE: `slice(0, 10)` would pick that day.
    const day = openMockDate(10);
    const row = findAppointment(ids.changeableAppointmentId)!;
    row.startsOn = zonedLocalToInstant(day, '02:00', 'Asia/Dhaka');
    row.endsOn = zonedLocalToInstant(day, '02:30', 'Asia/Dhaka');
    const askedDates: string[] = [];
    server.use(
      http.get('*/api/booking/slots', ({ request }) => {
        askedDates.push(new URL(request.url).searchParams.get('date') ?? '');
      }),
    );
    renderWithClient(<MyAppointmentView appointmentId={ids.changeableAppointmentId} />);
    await user.click(await screen.findByRole('button', { name: 'Reschedule' }));
    await waitFor(() => expect(askedDates.length).toBeGreaterThan(0));
    expect(askedDates[0]).toBe(day);
  });

  it('W16: while the services list loads Reschedule is disabled and never claims "can’t be rescheduled online"', async () => {
    server.use(
      http.get('*/api/services', async () => {
        await delay('infinite');
      }),
    );
    renderWithClient(<MyAppointmentView appointmentId={ids.changeableAppointmentId} />);
    const button = await screen.findByRole('button', { name: 'Reschedule' });
    expect(button).toBeDisabled();
    expect(screen.queryByText(/can’t be rescheduled online/i)).not.toBeInTheDocument();
  });

  it('W16: when the services list fails the message says the check failed, not that the service is unsupported', async () => {
    const user = userEvent.setup();
    server.use(http.get('*/api/services', () => HttpResponse.json({}, { status: 500 })));
    renderWithClient(<MyAppointmentView appointmentId={ids.changeableAppointmentId} />);
    const button = await screen.findByRole('button', { name: 'Reschedule' });
    await waitFor(() => expect(button).toBeEnabled());
    await user.click(button);
    expect(
      await screen.findByText(/couldn’t check whether this can be rescheduled/i),
    ).toBeVisible();
    expect(screen.queryByText(/can’t be rescheduled online/i)).not.toBeInTheDocument();
  });
});

describe('WaitlistSection — W16', () => {
  const offered: WaitlistEntry = {
    id: 'w1',
    branchId: 'b1',
    catId: 'c1',
    catName: 'Mitu',
    serviceId: 's1',
    serviceName: 'Vaccination',
    localDate: '2026-10-12',
    status: 'Offered',
    offeredAppointmentId: 'a1',
    offerExpiresOn: '2026-10-12T04:30:00Z',
    createdOn: '2026-10-01T00:00:00Z',
  };

  it('shows no UTC claim-by time before the branch zone is known, and the zoned one after', () => {
    const { rerender } = render(
      <WaitlistSection
        entries={[offered]}
        zoneByBranchId={{}}
        onWithdraw={vi.fn()}
        withdrawingId={null}
      />,
    );
    expect(screen.queryByText(/UTC/)).not.toBeInTheDocument();
    expect(screen.queryByText(/04:30/)).not.toBeInTheDocument();
    expect(screen.getByText(/checking the claim-by time/i)).toBeInTheDocument();

    rerender(
      <WaitlistSection
        entries={[offered]}
        zoneByBranchId={{ b1: 'Asia/Dhaka' }}
        onWithdraw={vi.fn()}
        withdrawingId={null}
      />,
    );
    expect(screen.getByText(/claim by 10:30 \(Asia\/Dhaka\)/)).toBeInTheDocument();
  });
});

describe('FlaggedView — W16', () => {
  beforeEach(() => {
    resetAppointmentsMockData();
    setUrl('/staff/appointments/flagged');
  });

  it('shows "Loading…" instead of a UTC time, and disables Reschedule, until the detail (and its zone) arrives', async () => {
    server.use(
      http.get('*/api/staff/appointments/:appointmentId', async ({ params }) => {
        if (UUID.test(String(params.appointmentId))) await delay('infinite');
      }),
    );
    renderWithClient(<FlaggedView />);
    const reschedule = await screen.findAllByRole('button', { name: /^reschedule /i });
    reschedule.forEach((button) => expect(button).toBeDisabled());
    expect(screen.queryByText('UTC')).not.toBeInTheDocument();
    expect(screen.queryByText(/\(UTC\)/)).not.toBeInTheDocument();
  });

  it('enables Reschedule and shows the branch zone once the detail is in', async () => {
    renderWithClient(<FlaggedView />);
    await waitFor(() => expect(screen.getAllByText('Asia/Dhaka').length).toBeGreaterThan(0));
    const first = screen.getAllByRole('button', { name: /^reschedule /i })[0]!;
    expect(first).toBeEnabled();
  });
});

describe('FlaggedView — detail failure and the version rule', () => {
  beforeEach(() => {
    resetAppointmentsMockData();
    setUrl('/staff/appointments/flagged');
  });

  it('a row whose detail fails shows an error with Retry instead of "Loading…" forever, and Retry recovers it', async () => {
    const user = userEvent.setup();
    let fail = true;
    server.use(
      http.get('*/api/staff/appointments/:appointmentId', ({ params }) => {
        if (fail && UUID.test(String(params.appointmentId)))
          return HttpResponse.json({}, { status: 404 });
      }),
    );
    renderWithClient(<FlaggedView />);
    const retry = (
      await screen.findAllByRole('button', { name: /retry loading details for whiskers/i })
    )[0]!;
    expect(screen.queryByText('Loading…')).not.toBeInTheDocument();
    fail = false;
    await user.click(retry);
    await waitFor(() => expect(screen.getAllByText('Asia/Dhaka').length).toBeGreaterThan(0));
    expect(
      screen.queryByRole('button', { name: /retry loading details/i }),
    ).not.toBeInTheDocument();
  });

  it('the move sends the version the dialog opened with; only after a 409 does the retry send the fresh one', async () => {
    const user = userEvent.setup();
    const posts: Array<Record<string, unknown>> = [];
    server.use(
      http.post('*/api/staff/appointments/:id/move', async ({ request }) => {
        posts.push((await request.clone().json()) as Record<string, unknown>);
      }),
    );
    const { client } = renderWithClient(<FlaggedView />);
    const button = (await screen.findAllByRole('button', { name: /reschedule whiskers/i }))[0]!;
    await waitFor(() => expect(button).toBeEnabled());
    await user.click(button);
    const dialog = await screen.findByRole('dialog');
    await waitFor(() =>
      expect(within(dialog).getByLabelText('Vet or nurse').children.length).toBeGreaterThan(1),
    );
    const row = findAppointment(ids.flaggedAppointmentId)!;
    const original = row.version;
    row.version = original + 4; // changed elsewhere; a poll brings it in while the dialog is open
    await client.invalidateQueries({ queryKey: ['staff', 'appointment'] });
    await waitFor(() => expect(client.isFetching()).toBe(0));

    await user.clear(within(dialog).getByLabelText(/^time/i));
    await user.type(within(dialog).getByLabelText(/^time/i), '15:30');
    await user.click(within(dialog).getByRole('button', { name: /review move/i }));
    await user.click(within(dialog).getByRole('button', { name: /move appointment/i }));
    expect(
      await within(dialog).findByText('This appointment changed since you loaded the calendar.'),
    ).toBeInTheDocument();
    expect(posts[0]).toMatchObject({ expectedVersion: original });
    await waitFor(() => expect(client.isFetching()).toBe(0));

    await user.click(within(dialog).getByRole('button', { name: /review move/i }));
    await user.click(within(dialog).getByRole('button', { name: /move appointment/i }));
    await waitFor(() => expect(posts).toHaveLength(2));
    expect(posts[1]).toMatchObject({ expectedVersion: original + 4 });
  });
});

describe('FlaggedView — moving the last flagged row (closing the dialog must not need a zone)', () => {
  beforeEach(() => {
    resetAppointmentsMockData();
    setUrl('/staff/appointments/flagged');
  });

  it('moves the row, empties the list and does not throw while the dialog closes', async () => {
    const user = userEvent.setup();
    renderWithClient(<FlaggedView />);
    const button = (await screen.findAllByRole('button', { name: /reschedule whiskers/i }))[0]!;
    await waitFor(() => expect(button).toBeEnabled());
    await user.click(button);
    const dialog = await screen.findByRole('dialog');
    await waitFor(() =>
      expect(within(dialog).getByLabelText('Vet or nurse').children.length).toBeGreaterThan(1),
    );
    await user.clear(within(dialog).getByLabelText(/^time/i));
    await user.type(within(dialog).getByLabelText(/^time/i), '15:30');
    await user.click(within(dialog).getByRole('button', { name: /review move/i }));
    await user.click(within(dialog).getByRole('button', { name: /move appointment/i }));
    expect(await screen.findByText('Nothing needs rescheduling.')).toBeInTheDocument();
  });
});

describe('CalendarView / FrontDeskView — W16 branch zone', () => {
  const noActiveBranch = () =>
    http.get('*/api/staff/branches/mine', () =>
      HttpResponse.json({
        branchIds: [],
        primaryBranchId: null,
        activeBranchId: null,
        activeBranchRefusal: null,
      }),
    );

  beforeEach(() => {
    resetAppointmentsMockData();
    setUrl('/staff/appointments');
  });

  it('the calendar asks for a branch instead of falling back to the first one', async () => {
    const calendarGets: string[] = [];
    server.use(
      noActiveBranch(),
      http.get('*/api/staff/appointments/calendar', ({ request }) => {
        calendarGets.push(request.url);
      }),
    );
    renderWithClient(<CalendarView />);
    expect(await screen.findByText(/choose a branch to continue/i)).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Appointments' })).not.toBeInTheDocument();
    expect(calendarGets).toHaveLength(0);
  });

  it('the front desk asks for a branch instead of falling back to the first one', async () => {
    setUrl('/staff/front-desk');
    server.use(noActiveBranch());
    renderWithClient(<FrontDeskView />);
    expect(await screen.findByText(/choose a branch to continue/i)).toBeInTheDocument();
    expect(screen.queryByRole('tab')).not.toBeInTheDocument();
  });

  it('while the branch list loads the calendar shows a loading state, not another branch’s day', async () => {
    const calendarGets: string[] = [];
    server.use(
      http.get('*/api/admin/branches', async () => {
        await delay('infinite');
      }),
      http.get('*/api/staff/appointments/calendar', ({ request }) => {
        calendarGets.push(request.url);
      }),
    );
    renderWithClient(<CalendarView />);
    expect(await screen.findByText(/loading the branch/i)).toBeInTheDocument();
    expect(calendarGets).toHaveLength(0);
  });

  it('with an active branch the calendar renders in that branch’s zone', async () => {
    renderWithClient(<CalendarView />);
    const heading = await screen.findByRole('heading', { name: 'Appointments' });
    expect(heading).toBeInTheDocument();
    expect(within(document.body).queryByText(/choose a branch/i)).not.toBeInTheDocument();
  });
});
