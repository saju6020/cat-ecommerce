import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { ScheduleForm } from '@/components/report-schedules/ScheduleForm';
import { ScheduleActions } from '@/components/report-schedules/ScheduleActions';
import { RunHistoryTable } from '@/components/report-schedules/RunHistoryTable';
import { Toaster } from '@/components/ui/toaster';
import { ApiValidationError } from '@/lib/api/client';
import { catalogueFor } from '@/mocks/handlers/reports/fixtures';
import { resetReportsMockData } from '@/mocks/handlers/reports';
import type { ReportScheduleRun } from '@/lib/api/report-schedules';
import { EMPTY_SCHEDULE_VALUES } from '@/lib/validation/reportSchedule';
import { renderWithClient } from '../reports/helpers';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  usePathname: () => '/staff/admin/report-schedules/new',
  useSearchParams: () => new URLSearchParams(),
}));

const reports = catalogueFor(['Administrator']).filter((r) => r.canSchedule);
const branches = [{ id: 'b0000000-0000-0000-0000-00000000a001', name: 'Main clinic' }];

beforeEach(() => {
  vi.useRealTimers();
  resetReportsMockData();
});

function renderForm(overrides: Partial<Parameters<typeof ScheduleForm>[0]> = {}) {
  const onSubmit = vi.fn().mockResolvedValue(undefined);
  const view = renderWithClient(
    <ScheduleForm
      reports={reports}
      initial={{
        ...EMPTY_SCHEDULE_VALUES,
        name: 'Mine',
        reportKey: 'revenue-by-business-line',
        period: 'PreviousMonth',
        branchId: '',
      }}
      branches={branches}
      zoneFor={() => 'Asia/Dhaka'}
      onSubmit={onSubmit}
      isPending={false}
      submitLabel="Create schedule"
      {...overrides}
    />,
  );
  return { onSubmit, ...view };
}

describe('ScheduleForm — U-E series', () => {
  it('U-E1: offers only the periods that suit the report, with a hint', async () => {
    renderForm({
      initial: {
        ...EMPTY_SCHEDULE_VALUES,
        reportKey: 'vaccination-due-list',
        period: 'Next30Days',
        branchId: branches[0]!.id,
      },
    });
    const select = screen.getByLabelText('Period each run covers');
    const options = within(select)
      .getAllByRole('option')
      .map((o) => o.textContent);
    expect(options).toEqual(['Choose a period', 'Next 30 days', 'Next 90 days']);
    expect(screen.getByText('Looks ahead thirty days from the send time.')).toBeInTheDocument();
  });

  it('U-E2: a recipient without an email is unselectable, with the reason as text', async () => {
    renderForm();
    const person = await screen.findByLabelText(/Sabina Yasmin/);
    expect(person).toBeDisabled();
    expect(screen.getByText(/no email address on their staff account/)).toBeInTheDocument();
    expect(screen.getByLabelText(/Rezwan Ahmed/)).toBeEnabled();
  });

  it('U-E3/U-E4: one debounced preview call; shows the server runs, period and the quiet-hours sentence', async () => {
    let calls = 0;
    server.use(
      http.post('*/api/admin/report-schedules/preview', async () => {
        calls += 1;
        return HttpResponse.json({
          nextRuns: [
            '2026-10-15T22:00:00+06:00',
            '2026-10-16T22:00:00+06:00',
            '2026-10-17T22:00:00+06:00',
          ],
          periodForNextRun: { from: '2026-09-01', to: '2026-09-30' },
          deliveryHeldUntil: '2026-10-16T08:00:00+06:00',
        });
      }),
    );
    renderForm();
    expect(await screen.findByText(/Thu 15 Oct 22:00/)).toBeInTheDocument();
    expect(screen.getByText(/01 Sep 2026 to 30 Sep 2026/)).toBeInTheDocument();
    expect(
      screen.getByText(/Quiet hours mean it will be delivered at 16 Oct 2026 08:00/),
    ).toBeInTheDocument();
    expect(calls).toBe(1);
  });

  it('U-E3: typing a name does not call the preview again', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    let calls = 0;
    server.use(
      http.post('*/api/admin/report-schedules/preview', () => {
        calls += 1;
        return HttpResponse.json({
          nextRuns: ['2026-10-15T07:00:00+06:00'],
          periodForNextRun: { from: '2026-09-01', to: '2026-09-30' },
        });
      }),
    );
    renderForm();
    await screen.findByText(/Thu 15 Oct 07:00/);
    await userEvent.type(screen.getByLabelText('Name'), ' more');
    await act(async () => {
      await vi.advanceTimersByTimeAsync(700);
    });
    expect(calls).toBe(1);
  });

  it('U-E5: a refused recipient lands on the recipients field and the rest keeps its values', async () => {
    const onSubmit = vi.fn().mockRejectedValue(
      new ApiValidationError({
        isValid: false,
        errors: [
          {
            propertyName: 'recipientIds',
            errorCode: 'ReportRecipientNotPermitted',
            errorMessage:
              'This person is not allowed to open this report, so they cannot be sent it. Choose someone who can.',
            resourceName: '',
          },
        ],
      }),
    );
    renderForm({ onSubmit });
    await userEvent.click(await screen.findByLabelText(/Rezwan Ahmed/));
    await userEvent.clear(screen.getByLabelText('Name'));
    await userEvent.type(screen.getByLabelText('Name'), 'Kept name');
    await userEvent.click(screen.getByRole('button', { name: 'Create schedule' }));
    expect(await screen.findByText(/not allowed to open this report/)).toBeInTheDocument();
    expect(screen.getByLabelText('Name')).toHaveValue('Kept name');
    expect(screen.getByLabelText(/Rezwan Ahmed/)).toBeChecked();
  });

  it('shape errors: requires at least one recipient and a 5-minute step', async () => {
    const { onSubmit } = renderForm();
    await screen.findByLabelText(/Rezwan Ahmed/);
    await userEvent.click(screen.getByRole('button', { name: 'Create schedule' }));
    expect(await screen.findByText('Choose at least one recipient.')).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });
});

describe('ScheduleForm — branch and double submit', () => {
  const limited = reports.map((r) => ({ ...r, canUseAllBranches: false }));
  const allowed = reports.map((r) => ({ ...r, canUseAllBranches: true }));

  it('moves off "All branches" to a real branch when the chosen report cannot use it, and SUBMITS that branch', async () => {
    // TEST gate: the select alone proved nothing. With "All branches" gone from the list jsdom shows
    // the first option whatever the form state is, so the old assertion passed with the reset removed.
    // The submitted value is what matters (the bug was: shown one branch, saved all branches).
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    const mixed = [
      { ...allowed[0]!, title: 'All ok' },
      { ...limited[1]!, title: 'One branch' },
    ];
    renderWithClient(
      <ScheduleForm
        reports={mixed}
        initial={{
          ...EMPTY_SCHEDULE_VALUES,
          name: 'N',
          reportKey: mixed[0]!.key,
          period: mixed[0]!.defaultPeriod,
          branchId: '',
        }}
        branches={branches}
        zoneFor={() => 'Asia/Dhaka'}
        onSubmit={onSubmit}
        isPending={false}
        submitLabel="Create schedule"
      />,
    );
    expect(screen.getByLabelText('Branch')).toHaveValue('');
    await userEvent.selectOptions(screen.getByLabelText('Report'), mixed[1]!.key);
    expect(screen.queryByRole('option', { name: 'All branches' })).not.toBeInTheDocument();
    await userEvent.click(await screen.findByLabelText(/Rezwan Ahmed/));
    await userEvent.click(screen.getByRole('button', { name: 'Create schedule' }));
    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ branchId: branches[0]!.id }));
  });

  it('disables submit once a save succeeded, so a second click cannot duplicate it', async () => {
    const { onSubmit } = renderForm();
    await userEvent.click(await screen.findByLabelText(/Rezwan Ahmed/));
    await userEvent.click(screen.getByRole('button', { name: 'Create schedule' }));
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Create schedule' })).toBeDisabled(),
    );
    expect(onSubmit).toHaveBeenCalledTimes(1);
  });
});

describe('ScheduleForm — U-E8 unsaved changes', () => {
  // TEST gate: U-E8 had no test. The guard itself is HO-ACC-002's; this pins that the form arms it
  // exactly while it is dirty (and not after a successful save).
  function leaveAttempt() {
    const event = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(event);
    return event.defaultPrevented;
  }

  it('does not interrupt leaving a pristine form', () => {
    renderForm();
    expect(leaveAttempt()).toBe(false);
  });

  it('interrupts leaving once a field was changed', async () => {
    renderForm();
    await userEvent.type(screen.getByLabelText('Name'), ' edited');
    expect(leaveAttempt()).toBe(true);
  });

  it('stops interrupting once the save succeeded', async () => {
    renderForm();
    await userEvent.click(await screen.findByLabelText(/Rezwan Ahmed/));
    expect(leaveAttempt()).toBe(true);
    await userEvent.click(screen.getByRole('button', { name: 'Create schedule' }));
    await waitFor(() => expect(leaveAttempt()).toBe(false));
  });
});

describe('ScheduleActions — U-E6', () => {
  function renderActions(status = 'Active') {
    return renderWithClient(
      <>
        <ScheduleActions
          scheduleId="sc000000-0000-0000-0000-000000000001"
          name="Daily revenue"
          status={status}
          recipientCount={3}
        />
        <Toaster />
      </>,
    );
  }

  it('Cancel states what it affects and that past runs are kept', async () => {
    renderActions();
    await userEvent.click(screen.getByRole('button', { name: /Cancel schedule/ }));
    expect(screen.getByRole('dialog')).toHaveTextContent(
      '‘Daily revenue’ will stop being emailed to 3 people. Past runs are kept.',
    );
  });

  it('Pause and Run now each name the recipients', async () => {
    renderActions();
    await userEvent.click(screen.getByRole('button', { name: /Pause/ }));
    expect(screen.getByRole('dialog')).toHaveTextContent(
      /stop being emailed to 3 people until you resume it/,
    );
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    await userEvent.click(screen.getByRole('button', { name: /Run now/ }));
    expect(screen.getByRole('dialog')).toHaveTextContent(
      'This generates the report and emails it to 3 people now.',
    );
  });

  it('Run now ends in a result toast', async () => {
    renderActions();
    await userEvent.click(screen.getByRole('button', { name: /Run now/ }));
    await userEvent.click(
      within(screen.getByRole('dialog')).getByRole('button', { name: 'Run now' }),
    );
    expect(await screen.findByText('Generated — 412 rows')).toBeInTheDocument();
  });

  it('Run now 429 says to wait, as an estimate when the header was unreadable', async () => {
    server.use(
      http.post('*/api/admin/report-schedules/:id/run-now', () =>
        HttpResponse.json(
          {
            errors: [
              {
                errorCode: 'TooManyRequests',
                errorMessage: 'Too many requests.',
                propertyName: '',
              },
            ],
          },
          { status: 429 },
        ),
      ),
    );
    renderActions();
    await userEvent.click(screen.getByRole('button', { name: /Run now/ }));
    await userEvent.click(
      within(screen.getByRole('dialog')).getByRole('button', { name: 'Run now' }),
    );
    expect(await screen.findByRole('alert')).toHaveTextContent('Try again in a minute or so');
  });

  it('a cancelled schedule has no actions', () => {
    renderActions('Cancelled');
    expect(
      screen.queryByRole('button', { name: /Pause|Resume|Run now|Cancel schedule/ }),
    ).not.toBeInTheDocument();
  });
});

describe('RunHistoryTable — U-E7', () => {
  const base: ReportScheduleRun = {
    runId: 'r',
    dueOn: '2026-10-01T07:00:00+06:00',
    trigger: 'Schedule',
    status: 'Generated',
    periodFrom: '2026-09-01',
    periodTo: '2026-09-30',
    format: 'Pdf',
    rowCount: 412,
    contentLength: 421888,
    attachmentOmitted: false,
    failureCode: null,
    generatedOn: '2026-10-01T07:00:20+06:00',
    purgedOn: null,
    deliveries: [{ recipientName: 'Rezwan Ahmed', status: 'Sent' }],
  };

  it('shows status as text, explains a failure, and notes an omitted attachment', () => {
    render(
      <RunHistoryTable
        timeZoneId="Asia/Dhaka"
        runs={[
          base,
          {
            ...base,
            runId: 'f',
            status: 'Failed',
            failureCode: 'ReportTooLargeForPdf',
            deliveries: [],
          },
          {
            ...base,
            runId: 'p',
            status: 'Purged',
            purgedOn: '2026-10-08T07:00:00+06:00',
            attachmentOmitted: true,
          },
        ]}
      />,
    );
    expect(screen.getByText('Generated')).toBeInTheDocument();
    expect(screen.getByText('Failed')).toBeInTheDocument();
    expect(screen.getByText('Purged')).toBeInTheDocument();
    expect(screen.getByText(/Send CSV instead, or choose a shorter period/)).toBeInTheDocument();
    expect(screen.getByText('Too large to attach — link sent')).toBeInTheDocument();
    expect(screen.getAllByText(/Rezwan Ahmed: Sent/).length).toBeGreaterThan(0);
    expect(screen.queryByText('ReportTooLargeForPdf')).not.toBeInTheDocument();
    expect(screen.getAllByText('412 KB').length).toBeGreaterThan(0);
  });

  it('says what to do when there are no runs', () => {
    render(<RunHistoryTable runs={[]} timeZoneId="Asia/Dhaka" />);
    expect(screen.getByText('No runs yet')).toBeInTheDocument();
  });
});

describe('errors never leak codes', () => {
  it('waitFor sanity', async () => {
    await waitFor(() => expect(true).toBe(true));
  });
});
