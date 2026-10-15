import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { NeuterStatusNotice } from '@/components/surgical-report/NeuterStatusNotice';
import { ReportTab } from '@/components/surgical-report/ReportTab';
import { SurgicalReportEditor } from '@/components/surgical-report/SurgicalReportEditor';
import { SurgicalReportView } from '@/components/surgical-report/SurgicalReportView';
import { getSurgicalReport } from '@/lib/api/surgical-reports';
import { surgeryFixtureIds as ids } from '@/mocks/handlers/surgeries';
import { server } from '@/mocks/server';
import { renderWithClient, resetSurgeryTest, signInAs, surgeryAs } from '../surgery/helpers';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.setConfig({ testTimeout: 40_000 });

beforeEach(() => {
  resetSurgeryTest();
  signInAs(['Vet']);
});

const editorProps = {
  surgeryId: ids.surgeryRecoveryId,
  zone: 'Asia/Dhaka',
  report: null,
  procedureName: 'Mass removal',
  surgeonId: ids.surgeonRahmanId,
  assistantId: ids.nurseTanvirId,
  surgeonName: 'Dr Rahman',
  assistantName: 'Nurse Tanvir',
  canFinalise: true,
  onReload: vi.fn(),
};

describe('U-D1 — the draft saves itself, and a conflict keeps the text', () => {
  it('autosaves 5 seconds after the last change', async () => {
    const user = userEvent.setup();
    renderWithClient(<SurgicalReportEditor {...editorProps} />);
    expect(screen.getByLabelText('Procedure performed')).toHaveValue('Mass removal'); // prefilled
    await user.type(screen.getByLabelText('Findings'), 'Small lipoma, left flank');
    expect(screen.getByText('Not saved yet')).toBeInTheDocument();
    expect(
      await screen.findByText(/Draft saved \d\d:\d\d/, undefined, { timeout: 12_000 }),
    ).toBeInTheDocument();
  });

  it('a 409 shows a reload prompt and keeps the local text for copying', async () => {
    const user = userEvent.setup();
    server.use(
      http.put('*/api/staff/surgeries/:id/report', () =>
        HttpResponse.json(
          {
            isValid: false,
            errors: [
              {
                propertyName: 'ExpectedVersion',
                errorCode: 'StaleVersion',
                errorMessage: 'Someone else changed this report.',
                resourceName: 'Surgery',
              },
            ],
          },
          { status: 409 },
        ),
      ),
    );
    renderWithClient(<SurgicalReportEditor {...editorProps} />);
    await user.type(screen.getByLabelText('Findings'), 'My careful findings');
    await user.click(screen.getByRole('button', { name: 'Save draft now' }));
    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(
      'Someone else changed this report, so your changes were not saved.',
    );
    expect(
      (screen.getByLabelText('Your text, for copying') as HTMLTextAreaElement).value,
    ).toContain('My careful findings');
    expect(screen.getByRole('button', { name: 'Reload the latest report' })).toBeInTheDocument();
    expect(screen.getByLabelText('Findings')).toHaveValue('My careful findings'); // untouched
  });
});

describe('U-D2 — a finalised report cannot be edited', () => {
  it('is read-only text with its addenda; only a vet can add one', async () => {
    const report = await getSurgicalReport(ids.surgeryOwnerAfterId);
    expect(report.status).toBe('Finalised');
    renderWithClient(
      <SurgicalReportView report={report} surgeryId={ids.surgeryOwnerAfterId} canAddAddendum />,
    );
    expect(screen.getByText(/Finalised by Dr Rahman on/)).toBeInTheDocument();
    expect(screen.getByText('Ovariohysterectomy (spay)')).toBeInTheDocument();
    expect(screen.getByText(/Owner reported mild swelling at day 2/)).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: /save|finalise|edit|delete/i }),
    ).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Add addendum' })).toBeInTheDocument();
    expect(screen.getAllByRole('textbox')).toHaveLength(1); // only the addendum box
  });

  it('shows no addendum control to a nurse', async () => {
    signInAs(['Nurse']);
    const surgery = await surgeryAs(ids.surgeryOwnerAfterId, ['Nurse']);
    renderWithClient(<ReportTab surgery={surgery} />);
    await screen.findByText(/Finalised by Dr Rahman/);
    expect(screen.queryByRole('button', { name: 'Add addendum' })).not.toBeInTheDocument();
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
  });

  it('adds an addendum and lists it', async () => {
    const user = userEvent.setup();
    const surgery = await surgeryAs(ids.surgeryOwnerAfterId, ['Vet']);
    renderWithClient(<ReportTab surgery={surgery} />);
    await user.type(
      await screen.findByLabelText('Add addendum'),
      'Checked the wound at day 5: healed.',
    );
    await user.click(screen.getByRole('button', { name: 'Add addendum' }));
    expect(await screen.findByText('Checked the wound at day 5: healed.')).toBeInTheDocument();
  });
});

describe('U-D3 — an incomplete report names what is missing', () => {
  it('lists the missing parts and focuses the first', async () => {
    const user = userEvent.setup();
    const surgery = await surgeryAs(ids.surgeryRecoveryId, ['Vet']);
    renderWithClient(<ReportTab surgery={surgery} />);
    await user.type(await screen.findByLabelText('Technique'), 'Midline approach');
    await user.click(screen.getByRole('button', { name: 'Finalise report' }));
    const confirm = await screen.findByRole('dialog', { name: 'Finalise this report?' });
    expect(confirm).toHaveTextContent(
      'After finalising, the report can only be changed by addendum.',
    );
    await user.click(within(confirm).getByRole('button', { name: 'Finalise report' }));
    expect(
      await screen.findByText(/The report is not complete\. Fill in: Findings, Suture material\./),
    ).toBeInTheDocument();
    await waitFor(() => expect(screen.getByLabelText('Findings')).toHaveFocus());
    expect(screen.getByLabelText('Findings')).toHaveAttribute('aria-invalid', 'true');
  });
});

describe('U-D4 — the neuter status after a completed sterilisation', () => {
  it('says it was set, and when', async () => {
    const surgery = await surgeryAs(ids.surgeryOwnerAfterId, ['Vet']);
    renderWithClient(<NeuterStatusNotice surgery={surgery} canApply />);
    expect(screen.getByRole('status')).toHaveTextContent(/Neuter status set to Neutered on /);
  });

  it('when it was not applied, says so and offers the repair action', async () => {
    const user = userEvent.setup();
    const surgery = await surgeryAs(ids.surgeryOwnerAfterId, ['Vet']);
    renderWithClient(
      <NeuterStatusNotice surgery={{ ...surgery, neuterStatusApplied: false }} canApply />,
    );
    expect(screen.getByRole('alert')).toHaveTextContent(/neuter status has not been updated/);
    const apply = vi.fn();
    server.use(
      http.post('*/api/staff/surgeries/:id/apply-sterilisation-outcome', () => {
        apply();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    await user.click(screen.getByRole('button', { name: 'Apply neuter status' }));
    await waitFor(() => expect(apply).toHaveBeenCalledTimes(1));
  });

  it('shows nothing for a surgery that is not a completed sterilisation', async () => {
    const surgery = await surgeryAs(ids.surgeryRecoveryId, ['Vet']);
    renderWithClient(<NeuterStatusNotice surgery={surgery} canApply />);
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
