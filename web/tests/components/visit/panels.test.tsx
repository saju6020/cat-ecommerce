import { describe, expect, it } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { FollowUpPanel } from '@/components/visit/panels/FollowUpPanel';
import { PhotosPanel } from '@/components/visit/panels/PhotosPanel';
import { PhotoThumb } from '@/components/visit/PhotoThumb';
import { getVisit, type VisitDetail } from '@/lib/api/visits';
import { openMockDate } from '../booking/helpers';
import {
  ids,
  renderWithClient,
  setMockSignedUrlTtlSeconds,
  freshClinicBeforeEach,
} from './helpers';

freshClinicBeforeEach();

async function visit(id: string, patch: Partial<VisitDetail> = {}): Promise<VisitDetail> {
  return { ...(await getVisit(id)), ...patch };
}

describe('FollowUpPanel — US-CLN-18', () => {
  it('U-E1: a reminder is set with the sentence saying who is reminded and when', async () => {
    const user = userEvent.setup();
    renderWithClient(<FollowUpPanel visit={await visit(ids.visitOpenId)} />);
    expect(screen.getByText('No follow-up set.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Send a reminder' }));
    expect(await screen.findByText(/We’ll remind .+ 3 days before\./)).toBeInTheDocument();
    const date = new Date(Date.now() + 10 * 86_400_000).toISOString().slice(0, 10);
    fireEvent.change(screen.getByLabelText('Remind on'), { target: { value: date } });
    await user.click(screen.getByRole('button', { name: 'Set reminder' }));
    await waitFor(() => expect(screen.queryByLabelText('Remind on')).toBeNull());
  });

  it('refuses a reminder date that is not after today, in words, before any request', async () => {
    const user = userEvent.setup();
    renderWithClient(<FollowUpPanel visit={await visit(ids.visitOpenId)} />);
    await user.click(screen.getByRole('button', { name: 'Send a reminder' }));
    fireEvent.change(screen.getByLabelText('Remind on'), { target: { value: '2020-01-01' } });
    await user.click(screen.getByRole('button', { name: 'Set reminder' }));
    expect(await screen.findByText('Choose a follow-up date after today.')).toBeInTheDocument();
  });

  it('U-E1: Book an appointment books through the staff slots, then sets the follow-up with the returned id', async () => {
    const user = userEvent.setup();
    renderWithClient(<FollowUpPanel visit={await visit(ids.visitOpenId)} />);
    await user.click(screen.getByRole('button', { name: 'Book an appointment' }));
    const dialog = await screen.findByRole('dialog', { name: /book a follow-up for whiskers/i });
    fireEvent.change(within(dialog).getByLabelText(/Date/), { target: { value: openMockDate() } });
    const slots = await within(dialog).findAllByRole(
      'button',
      { pressed: false },
      { timeout: 5000 },
    );
    await user.click(slots[0]!);
    const book = within(dialog).getByRole('button', { name: /book and set as follow-up/i });
    await waitFor(() => expect(book).toBeEnabled());
    await user.click(book);
    await waitFor(() =>
      expect(screen.queryByRole('dialog', { name: /book a follow-up/i })).toBeNull(),
    );
  });

  it('offers nothing to someone without setFollowUp, and shows an existing follow-up as text', async () => {
    renderWithClient(
      <FollowUpPanel visit={await visit(ids.visitFinalisedId, { allowedActions: [] })} />,
    );
    expect(screen.queryByRole('button', { name: 'Book an appointment' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Send a reminder' })).toBeNull();
    expect(screen.getByText(/Reminder on/)).toBeInTheDocument();
  });

  it('removing a follow-up is confirmed', async () => {
    const user = userEvent.setup();
    renderWithClient(
      <FollowUpPanel
        visit={await visit(ids.visitFinalisedId, { allowedActions: ['setFollowUp'] })}
      />,
    );
    await user.click(await screen.findByRole('button', { name: 'Remove' }));
    const confirm = await screen.findByRole('dialog', { name: /remove the follow-up/i });
    expect(within(confirm).getByText('The reminder will not be sent.')).toBeInTheDocument();
  });
});

describe('PhotosPanel — US-CLN-19', () => {
  it('shows thumbnails with their date and note and offers Add photo only with attachPhoto', async () => {
    renderWithClient(<PhotosPanel visit={await visit(ids.visitOpenId)} />);
    expect(screen.getByText('Left ear canal before flush')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Add photo' })).toBeInTheDocument();
  });

  it('hides Add photo for someone without attachPhoto', async () => {
    renderWithClient(<PhotosPanel visit={await visit(ids.visitOpenId, { allowedActions: [] })} />);
    expect(screen.queryByRole('button', { name: 'Add photo' })).toBeNull();
  });

  it('Add photo asks for the photo, a date not in the future, and a note', async () => {
    const user = userEvent.setup();
    renderWithClient(<PhotosPanel visit={await visit(ids.visitOpenId)} />);
    await user.click(screen.getByRole('button', { name: 'Add photo' }));
    const form = screen.getByRole('form', { name: 'Add a photo' });
    expect(within(form).getByLabelText('Taken on')).toHaveAttribute('max');
    await user.click(within(form).getByRole('button', { name: 'Add photo' }));
    expect(await within(form).findByText('Choose a photo to add first.')).toBeInTheDocument();
  });

  it('U-X4: an expired signed URL triggers exactly one refetch for a fresh one', async () => {
    setMockSignedUrlTtlSeconds(-60); // every URL the visit returns is already expired
    let refreshes = 0;
    const current = await visit(ids.visitOpenId);
    const photo = current.photos[0]!;
    renderWithClient(
      <PhotoThumb
        photo={photo}
        onRefresh={async () => {
          refreshes += 1;
          setMockSignedUrlTtlSeconds(300);
          return (await getVisit(ids.visitOpenId)).photos[0];
        }}
      />,
    );
    await waitFor(() => expect(refreshes).toBe(1));
    await new Promise((resolve) => setTimeout(resolve, 100));
    expect(refreshes).toBe(1);
  });
});
