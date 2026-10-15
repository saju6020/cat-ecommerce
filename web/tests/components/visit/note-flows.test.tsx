import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '../../../mocks/server';
import { getApiUrl } from '@/lib/api/client';
import { VisitView } from '@/app/(staff)/staff/visits/[visitId]/VisitView';
import { ids, renderWithClient, freshClinicBeforeEach } from './helpers';

vi.mock('next/navigation', () => import('../booking/nav-mock'));

freshClinicBeforeEach();
afterEach(() => vi.restoreAllMocks());

async function openVisit(visitId: string) {
  renderWithClient(<VisitView visitId={visitId} />);
  return screen.findByLabelText('Subjective', {}, { timeout: 5000 });
}

describe('Restored draft and template — US-CLN-12 AC2, US-CLN-20', () => {
  it('U-A5: a newer draft is restored with a notice and a Revert; reverting is confirmed', async () => {
    const user = userEvent.setup();
    const subjective = await openVisit(ids.visitDraftNewerId);
    expect(subjective).toHaveValue(
      'Saved earlier: limping on the left hind leg. Draft addition: worse after jumping.',
    );
    expect(
      screen.getByText(/were restored from your working copy on the server/),
    ).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Revert to saved note' }));
    const confirm = await screen.findByRole('dialog', { name: /revert to the saved note/i });
    expect(within(confirm).getByText(/will be discarded/)).toBeInTheDocument();
    await user.click(within(confirm).getByRole('button', { name: 'Revert to saved note' }));
    await waitFor(() =>
      expect(subjective).toHaveValue('Saved earlier: limping on the left hind leg.'),
    );
    expect(screen.queryByText(/were restored from your working copy/)).toBeNull();
  });

  it('U-A5: with no newer draft the saved note is shown and there is no notice', async () => {
    const subjective = await openVisit(ids.visitOpenId);
    expect(subjective).toHaveValue(
      'Owner reports head shaking and scratching at the left ear for 5 days. Eating and drinking normally.',
    );
    expect(screen.queryByText(/were restored from your working copy/)).toBeNull();
    expect(screen.queryByText(/Pre-filled from/)).toBeNull();
  });

  it('U-A6: a service with a template and no saved note is pre-filled, editable, with the notice', async () => {
    const user = userEvent.setup();
    const subjective = await openVisit(ids.visitTemplateId);
    expect(subjective).toHaveValue('Presenting complaint:\nDuration:\nAppetite / water / litter:');
    expect(
      screen.getByText('Pre-filled from the General consultation template. Edit freely.'),
    ).toBeInTheDocument();
    await user.type(subjective, ' extra');
    expect(subjective).toHaveValue(
      'Presenting complaint:\nDuration:\nAppetite / water / litter: extra',
    );
  });
});

describe('Saving — U-A9, U-A10', () => {
  it('U-A9: across a full editing session nothing is written to browser storage', async () => {
    const setItem = vi.spyOn(Storage.prototype, 'setItem');
    const cacheOpen = vi.fn();
    vi.stubGlobal('caches', { open: cacheOpen });
    const user = userEvent.setup();
    await openVisit(ids.visitEmptyId);
    await user.type(screen.getByLabelText('Plan'), 'Confidential clinical text');
    // The auto-save (3 s) to the SERVER's working copy, then an explicit Save note.
    await screen.findByText(/Draft saved/, {}, { timeout: 6000 });
    await user.click(screen.getByRole('button', { name: 'Save note' }));
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Save note' })).not.toHaveAttribute(
        'aria-busy',
        'true',
      ),
    );
    const written = setItem.mock.calls.map(([key, value]) => `${key}=${value}`).join('\n');
    expect(written).not.toContain('Confidential clinical text');
    expect(cacheOpen).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
  }, 15_000);

  it('U-A10: a 409 on Save note keeps the local text and offers Reload with their version beside it', async () => {
    const user = userEvent.setup();
    await openVisit(ids.visitOpenId);
    const plan = screen.getByLabelText('Plan');
    await user.clear(plan);
    await user.type(plan, 'My careful plan');
    server.use(
      http.put('*/api/staff/visits/:visitId/note', () =>
        HttpResponse.json(
          { error: 'This visit was changed by someone else. Reload and try again.' },
          { status: 409 },
        ),
      ),
    );
    await user.click(screen.getByRole('button', { name: 'Save note' }));
    expect(
      await screen.findByText(
        'Someone else saved this note since you opened it. Your text is kept below — reload to see their version.',
      ),
    ).toBeInTheDocument();
    expect(plan).toHaveValue('My careful plan');
    await user.click(screen.getByRole('button', { name: 'Reload' }));
    expect(await screen.findByText('Their saved note')).toBeInTheDocument();
    expect(screen.getByText('Your text (read-only copy)')).toBeInTheDocument();
    expect(plan).toHaveValue('My careful plan');
  });

  it('a stale version really is a 409 against the mock, and the vet’s own save then works', async () => {
    const user = userEvent.setup();
    await openVisit(ids.visitOpenId);
    // Someone else saves first (the version moves on).
    await fetch(getApiUrl(`/api/staff/visits/${ids.visitOpenId}/vitals`), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ heartRateBpm: 150 }),
    });
    await user.click(screen.getByRole('button', { name: 'Save note' }));
    expect(await screen.findByText(/Someone else saved this note/)).toBeInTheDocument();
  });
});

describe('Problems — US-CLN-12', () => {
  it('adds a coded problem from the keyboard, refuses a duplicate in place, and removes one', async () => {
    const user = userEvent.setup();
    await openVisit(ids.visitEmptyId);
    const search = screen.getByRole('combobox', { name: /add a diagnosis/i });
    await user.type(search, 'otitis');
    const option = await screen.findByRole('option', { name: /EAR-OTE/ });
    expect(option).toBeInTheDocument();
    await user.keyboard('{ArrowDown}{Enter}');
    expect(await screen.findByText(/EAR-OTE — Otitis externa/)).toBeInTheDocument();

    await user.type(search, 'otitis');
    const taken = await screen.findByRole('option', { name: /EAR-OTE/ });
    expect(taken).toHaveAttribute('aria-disabled', 'true');
    expect(taken).toHaveTextContent('already on the list');
    await user.keyboard('{Escape}');

    await user.click(screen.getByRole('radio', { name: 'Confirmed' }));
    expect(screen.getByRole('radio', { name: 'Confirmed' })).toBeChecked();
    await user.click(screen.getByRole('button', { name: /remove otitis externa/i }));
    expect(screen.queryByText(/EAR-OTE — Otitis externa/)).toBeNull();
  });

  it('never offers a retired code, and says when nothing matches', async () => {
    const user = userEvent.setup();
    await openVisit(ids.visitEmptyId);
    await user.type(screen.getByRole('combobox', { name: /add a diagnosis/i }), 'gingivitis');
    expect(await screen.findByText(/No diagnosis matches/)).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: /OLD-GEN/ })).toBeNull();
  });
});

describe('Finalise and addenda — US-CLN-17, 22', () => {
  it('U-A11: with no problem and a blank Plan the checklist blocks, and its links focus the field', async () => {
    const user = userEvent.setup();
    await openVisit(ids.visitEmptyId);
    await user.click(screen.getByRole('button', { name: 'Finalise' }));
    const dialog = await screen.findByRole('dialog', { name: /finalise biscuit/i });
    expect(within(dialog).getByRole('button', { name: 'Finalise visit' })).toBeDisabled();
    expect(within(dialog).getByText(/become permanent/)).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: /Plan written \(missing/ }));
    await waitFor(() => expect(screen.getByLabelText('Plan')).toHaveFocus());
  });

  it('U-A11: a server ProblemRequired is also mapped, in words', async () => {
    const user = userEvent.setup();
    await openVisit(ids.visitOpenId);
    server.use(
      http.post('*/api/staff/visits/:id/finalise', () =>
        HttpResponse.json(
          {
            errors: [
              {
                propertyName: 'Problems',
                errorCode: 'ProblemRequired',
                errorMessage: '',
                resourceName: 'Visit',
              },
            ],
            isValid: false,
          },
          { status: 400 },
        ),
      ),
    );
    await user.click(screen.getByRole('button', { name: 'Finalise' }));
    const dialog = await screen.findByRole('dialog', { name: /finalise whiskers/i });
    await user.click(within(dialog).getByRole('button', { name: 'Finalise visit' }));
    expect(
      await within(dialog).findByText(/Add at least one diagnosis before finalising/),
    ).toBeInTheDocument();
    expect(within(dialog).queryByText('ProblemRequired')).toBeNull();
  });

  it('U-A12: finalising with unsaved changes saves the note first, then finalises with the new version', async () => {
    const user = userEvent.setup();
    const calls: string[] = [];
    const bodies: Record<string, unknown> = {};
    server.events.on('request:start', async ({ request }) => {
      if (request.method !== 'GET' && /visits\/.+\/(note|finalise)$/.test(request.url)) {
        const key = request.url.endsWith('/note') ? 'note' : 'finalise';
        calls.push(key);
        bodies[key] = await request.clone().json();
      }
    });
    await openVisit(ids.visitOpenId);
    const plan = screen.getByLabelText('Plan');
    await user.clear(plan);
    await user.type(plan, 'Changed plan, not yet saved');
    await user.click(screen.getByRole('button', { name: 'Finalise' }));
    const dialog = await screen.findByRole('dialog', { name: /finalise whiskers/i });
    expect(within(dialog).getByText(/unsaved changes will be saved first/i)).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Finalise visit' }));
    await screen.findByText(/Finalised by/);
    server.events.removeAllListeners();
    expect(calls).toEqual(['note', 'finalise']);
    expect((bodies.note as { expectedVersion: number }).expectedVersion).toBe(4);
    expect((bodies.finalise as { expectedVersion: number }).expectedVersion).toBe(5);
  }, 15_000);

  it('U-A13: an addendum is confirmed ("cannot be edited or removed"), then listed with author and time', async () => {
    const user = userEvent.setup();
    renderWithClient(<VisitView visitId={ids.visitFinalisedId} />);
    const field = await screen.findByLabelText('Addendum');
    await user.type(field, 'Owner phoned again.');
    await user.click(screen.getByRole('button', { name: 'Add addendum' }));
    const confirm = await screen.findByRole('dialog', { name: /add this addendum/i });
    expect(within(confirm).getByText('Addenda cannot be edited or removed.')).toBeInTheDocument();
    await user.click(within(confirm).getByRole('button', { name: 'Add addendum' }));
    expect(await screen.findByText('Owner phoned again.')).toBeInTheDocument();
    const list = screen.getByRole('heading', { name: 'Addenda' }).closest('section')!;
    expect(within(list).getAllByText(/Dr Akter ·/).length).toBeGreaterThanOrEqual(2);
    expect(
      screen.getByText('Vomiting twice yesterday. Eating normally today.'),
    ).toBeInTheDocument();
  });
});
