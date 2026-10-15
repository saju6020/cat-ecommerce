import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '../../../mocks/server';
import { DiagnosisCodeManager } from '@/components/clinical-catalogue/DiagnosisCodeManager';
import { LabAnalyteManager } from '@/components/clinical-catalogue/LabAnalyteManager';
import { NoteTemplateManager } from '@/components/clinical-catalogue/NoteTemplateManager';
import { renderWithClient, freshClinicBeforeEach } from '../visit/helpers';
import { signedInAs } from '../visit/session';
import { setUrl } from '../booking/nav-mock';

vi.mock('next/navigation', () => import('../booking/nav-mock'));

freshClinicBeforeEach();

describe('Diagnosis codes — HO-EMR-002 §11.8', () => {
  it('lists codes, searches through the URL, and a Vet can add one (code upper-cased)', async () => {
    const user = userEvent.setup();
    signedInAs(['Vet']);
    setUrl('/staff/admin/clinical/diagnosis-codes');
    renderWithClient(<DiagnosisCodeManager />);
    expect(await screen.findByRole('button', { name: 'Edit EAR-OTE' })).toBeInTheDocument();
    expect(screen.getByText('Built in')).toBeInTheDocument();

    await user.click(await screen.findByRole('button', { name: 'Add a code' }));
    const dialog = await screen.findByRole('dialog', { name: /add a diagnosis code/i });
    await user.click(within(dialog).getByRole('button', { name: 'Save' }));
    expect(await within(dialog).findByText('Enter a code.')).toBeInTheDocument();
    await user.type(within(dialog).getByLabelText('Code'), 'der-atp');
    expect(within(dialog).getByLabelText('Code')).toHaveValue('DER-ATP');
    await user.type(within(dialog).getByLabelText('Name'), 'Atopic dermatitis');
    await user.click(within(dialog).getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(await screen.findByRole('button', { name: 'Edit DER-ATP' })).toBeInTheDocument();
  });

  it('maps a duplicate code to its field; the code is locked once created and a built-in code is read-only', async () => {
    const user = userEvent.setup();
    signedInAs(['Administrator']);
    renderWithClient(<DiagnosisCodeManager />);
    await user.click(await screen.findByRole('button', { name: 'Add a code' }));
    let dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Code'), 'EAR-OTE');
    await user.type(within(dialog).getByLabelText('Name'), 'Another otitis');
    await user.click(within(dialog).getByRole('button', { name: 'Save' }));
    expect(await within(dialog).findByText(/already exists/i)).toBeInTheDocument();
    expect(within(dialog).queryByText('DiagnosisCodeAlreadyExists')).toBeNull();
    await user.keyboard('{Escape}');

    await user.click(await screen.findByRole('button', { name: 'Edit EAR-OTE' }));
    dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByLabelText('Code')).toBeDisabled();
    expect(within(dialog).getByText(/locked once created/i)).toBeInTheDocument();
    await user.keyboard('{Escape}');

    await user.click(await screen.findByRole('button', { name: 'Edit SYS-NAD' }));
    dialog = await screen.findByRole('dialog');
    expect(
      within(dialog).getByText('This is a built-in code and cannot be changed.'),
    ).toBeInTheDocument();
    expect(within(dialog).getByLabelText('Name')).toBeDisabled();
  });

  it('retires and reactivates a code; a non-writer sees the list but no write controls', async () => {
    const user = userEvent.setup();
    signedInAs(['Nurse']);
    const view = renderWithClient(<DiagnosisCodeManager />);
    await screen.findByRole('button', { name: 'Edit EAR-OTE' });
    expect(screen.queryByRole('button', { name: 'Add a code' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Retire EAR-OTE' })).toBeNull();
    view.unmount();

    signedInAs(['Vet']);
    renderWithClient(<DiagnosisCodeManager />);
    await user.click(await screen.findByRole('button', { name: 'Retire EAR-OTE' }));
    expect(await screen.findByRole('button', { name: 'Reactivate EAR-OTE' })).toBeInTheDocument();
  });

  it('a server 403 is the permission state', async () => {
    server.use(
      http.get('*/api/staff/diagnosis-codes', () =>
        HttpResponse.json({ errors: [] }, { status: 403 }),
      ),
    );
    renderWithClient(<DiagnosisCodeManager />);
    expect(await screen.findByText(/you don.t have permission to do this/i)).toBeInTheDocument();
  });
});

describe('Lab analytes — HO-EMR-002 §11.8', () => {
  it('refuses low > high on the field before posting, and shows the range with its unit', async () => {
    const user = userEvent.setup();
    signedInAs(['Vet']);
    renderWithClient(<LabAnalyteManager />);
    expect(await screen.findByText('71–212 µmol/L')).toBeInTheDocument();
    await user.click(await screen.findByRole('button', { name: 'Add an analyte' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Code'), 'bun');
    await user.type(within(dialog).getByLabelText('Name'), 'Blood urea nitrogen');
    await user.type(within(dialog).getByLabelText('Unit'), 'mg/dL');
    await user.type(within(dialog).getByLabelText('Range low, mg/dL'), '30');
    await user.type(within(dialog).getByLabelText('Range high, mg/dL'), '20');
    await user.click(within(dialog).getByRole('button', { name: 'Save' }));
    expect(
      await within(dialog).findByText('The low value must not be higher than the high value.'),
    ).toBeInTheDocument();
    await user.clear(within(dialog).getByLabelText('Range low, mg/dL'));
    await user.type(within(dialog).getByLabelText('Range low, mg/dL'), '10');
    await user.click(within(dialog).getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(await screen.findByText('10–20 mg/dL')).toBeInTheDocument();
  });
});

describe('Note templates — HO-EMR-002 §11.8 (US-CLN-20)', () => {
  it('offers only services without an active template when adding', async () => {
    const user = userEvent.setup();
    signedInAs(['Vet']);
    server.use(
      http.get('*/api/admin/services', () =>
        HttpResponse.json([
          {
            id: 's0000000-0000-0000-0000-00000000a001',
            name: 'General consultation',
            isActive: true,
            isPriced: true,
          },
          {
            id: 's0000000-0000-0000-0000-00000000a0b2',
            name: 'Spay',
            isActive: true,
            isPriced: true,
          },
        ]),
      ),
    );
    renderWithClient(<NoteTemplateManager />);
    expect(
      await screen.findByRole('button', { name: /edit the general consultation template/i }),
    ).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Add a template' }));
    const dialog = await screen.findByRole('dialog');
    const select = within(dialog).getByLabelText('Service');
    await waitFor(() =>
      expect(within(select).getByRole('option', { name: 'Spay' })).toBeInTheDocument(),
    );
    expect(within(select).queryByRole('option', { name: 'General consultation' })).toBeNull();
  });

  it('editing shows the template version and saving creates the next one', async () => {
    const user = userEvent.setup();
    signedInAs(['Vet']);
    renderWithClient(<NoteTemplateManager />);
    expect(await screen.findByText('Version 2')).toBeInTheDocument();
    await user.click(
      screen.getByRole('button', { name: /edit the general consultation template/i }),
    );
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/Template version 2/)).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(await screen.findByText('Version 3')).toBeInTheDocument();
  });
});
