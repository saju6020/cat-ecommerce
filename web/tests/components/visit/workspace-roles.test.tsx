import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import { VisitView } from '@/app/(staff)/staff/visits/[visitId]/VisitView';
import { ids, renderWithClient, setMockCallerRoles, freshClinicBeforeEach } from './helpers';

vi.mock('next/navigation', () => import('../booking/nav-mock'));

freshClinicBeforeEach();

async function open(visitId: string) {
  renderWithClient(<VisitView visitId={visitId} />);
  return screen.findByRole('heading', { level: 1 }, { timeout: 5000 });
}

describe('VisitWorkspace — what each role gets (HO-EMR-002 §11.1, ADR-FE-051)', () => {
  it('U-A1: an open visit as a vet has the editor, the panels, Save note and Finalise', async () => {
    await open(ids.visitOpenId);
    expect(await screen.findByLabelText('Subjective')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Save note' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Finalise' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Record vitals' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Order test' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Add photo' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Send a reminder' })).toBeInTheDocument();
    // There is no prescribe button anywhere on a clinical screen (HO-PHM-002's).
    expect(screen.queryByRole('button', { name: /prescribe/i })).toBeNull();
  });

  it('U-A2: the same visit as a nurse — vitals writable, the note is text, no Finalise', async () => {
    setMockCallerRoles(['Nurse']);
    await open(ids.visitOpenId);
    expect(await screen.findByRole('button', { name: 'Record vitals' })).toBeInTheDocument();
    expect(screen.queryByLabelText('Subjective')).toBeNull();
    expect(screen.getByText(/Left otitis externa/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Finalise' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Save note' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Order test' })).toBeNull();
  });

  it('U-A3: the same visit as an administrator — read-only everywhere, no Finalise (US-CLN-22)', async () => {
    setMockCallerRoles(['Administrator']);
    await open(ids.visitOpenId);
    await screen.findByText(/Left otitis externa/);
    expect(screen.queryByRole('button', { name: 'Record vitals' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Finalise' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Save note' })).toBeNull();
    expect(screen.queryByLabelText('Subjective')).toBeNull();
    // Still allowed to read: the History button and the record.
    expect(screen.getByRole('button', { name: 'History' })).toBeInTheDocument();
  });

  it('reception gets the permission-denied state, not a form', async () => {
    setMockCallerRoles(['Reception']);
    renderWithClient(<VisitView visitId={ids.visitOpenId} />);
    expect(await screen.findByText(/you don.t have permission to do this/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Finalise' })).toBeNull();
  });

  it('a visit of another branch is the same plain not-found as one that does not exist', async () => {
    renderWithClient(<VisitView visitId={ids.visitOtherBranchId} />);
    expect(await screen.findByText('We couldn’t find this visit.')).toBeInTheDocument();
  });

  it('U-A4: a finalised visit is a record — no inputs for the note; the addendum form only with addAddendum', async () => {
    await open(ids.visitFinalisedId);
    expect(
      await screen.findByText('Vomiting twice yesterday. Eating normally today.'),
    ).toBeInTheDocument();
    expect(screen.queryByLabelText('Subjective')).toBeNull();
    expect(screen.queryByRole('textbox', { name: 'Plan' })).toBeNull();
    expect(screen.getByText(/Finalised by Dr Akter on/)).toBeInTheDocument();
    expect(screen.getByLabelText('Addendum')).toBeInTheDocument();
    expect(screen.getByText('Owner phoned: vomiting has stopped.')).toBeInTheDocument();
  });

  it('U-A4: a nurse on a finalised visit sees the addenda but no addendum form', async () => {
    setMockCallerRoles(['Nurse']);
    await open(ids.visitFinalisedId);
    expect(await screen.findByText('Owner phoned: vomiting has stopped.')).toBeInTheDocument();
    expect(screen.queryByLabelText('Addendum')).toBeNull();
  });

  it('shows the cat’s allergies, written out, in the compact header (UI-7)', async () => {
    await open(ids.visitOpenId);
    const header = await screen.findByRole('region', { name: /cat — whiskers/i });
    await waitFor(() => expect(within(header).getByText('Allergies')).toBeInTheDocument());
  });
});
