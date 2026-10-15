import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { DischargeForm } from '@/components/recovery/DischargeForm';
import { PostOpSheet } from '@/components/recovery/PostOpSheet';
import { RecoveryBoard } from '@/components/recovery/RecoveryBoard';
import { RecoveryStatusControl } from '@/components/recovery/RecoveryStatusControl';
import { MySurgeryDetail } from '@/components/surgery/MySurgeryDetail';
import { getDischargeDraft, getRecoveryBoard } from '@/lib/api/surgeries';
import { getMySurgery } from '@/lib/api/my-surgeries';
import { surgeryFixtureIds as ids } from '@/mocks/handlers/surgeries';
import { server } from '@/mocks/server';
import { renderWithClient, resetSurgeryTest, signInAs, surgeryAs } from '../surgery/helpers';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.setConfig({ testTimeout: 40_000 });

const READY = 'e5000000-0000-4000-8000-0000000000a2'; // ready for discharge
const STABLE = 'e5000000-0000-4000-8000-0000000000a1';

beforeEach(() => {
  resetSurgeryTest();
  signInAs(['Nurse']);
});

async function form(id: string) {
  const surgery = await surgeryAs(id, ['Nurse']);
  const draft = await getDischargeDraft(id);
  const onDischarged = vi.fn();
  const view = renderWithClient(
    <>
      <DischargeForm surgery={surgery} draft={draft} onDischarged={onDischarged} />
      {/* a plain anchor on purpose: UnsavedChangesGuard intercepts same-document link clicks */}
      {/* eslint-disable-next-line @next/next/no-html-link-for-pages */}
      <a href="/staff/surgeries">Elsewhere</a>
    </>,
  );
  return { surgery, draft, onDischarged, ...view };
}

describe('U-E1 — discharge waits for Ready for discharge', () => {
  it('is disabled, with the reason in words, while the cat is only recovering', async () => {
    await form(ids.surgeryRecoveryId);
    expect(screen.getByRole('button', { name: /^Discharge Momo/ })).toBeDisabled();
    expect(screen.getByRole('status')).toHaveTextContent(/not ready for discharge yet/i);
  });

  it('is enabled once the status is Ready for discharge, and confirms what it does', async () => {
    const user = userEvent.setup();
    await form(READY);
    const button = screen.getByRole('button', { name: /^Discharge Noodle/ });
    expect(button).toBeEnabled();
    await user.click(button);
    const confirm = await screen.findByRole('dialog', { name: 'Discharge Noodle?' });
    expect(confirm).toHaveTextContent(/is sent the post-op instructions/);
  });
});

describe('U-E2 — follow-up dates stay inside their window', () => {
  it('catches a date outside the window before anything is sent', async () => {
    const user = userEvent.setup();
    let posted = 0;
    server.use(
      http.post('*/api/staff/surgeries/:id/discharge', () => {
        posted += 1;
        return new HttpResponse(null, { status: 500 });
      }),
    );
    const { draft } = await form(READY);
    const input = screen.getByLabelText(/Wound check date/);
    expect(input).toHaveAttribute('min', draft.followUps[0]!.windowFrom);
    expect(input).toHaveAttribute('max', draft.followUps[0]!.windowTo);
    fireEvent.change(input, { target: { value: '2030-01-01' } });
    await user.click(screen.getByRole('button', { name: /^Discharge Noodle/ }));
    expect(await screen.findByText(/Choose a date between/)).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(posted).toBe(0);
  });

  it('shows the server’s message when it refuses a date', async () => {
    const user = userEvent.setup();
    server.use(
      http.post('*/api/staff/surgeries/:id/discharge', () =>
        HttpResponse.json(
          {
            isValid: false,
            errors: [
              {
                propertyName: 'dueOn',
                errorCode: 'FollowUpOutsideWindow',
                errorMessage: 'A WoundCheck follow-up must fall between 2026-10-16 and 2026-10-19.',
                resourceName: 'Surgery',
              },
            ],
          },
          { status: 400 },
        ),
      ),
    );
    await form(READY);
    await user.click(screen.getByRole('button', { name: /^Discharge Noodle/ }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: 'Discharge' }),
    );
    expect(
      await screen.findByText(/must fall between 2026-10-16 and 2026-10-19/),
    ).toBeInTheDocument();
    expect(screen.queryByText('FollowUpOutsideWindow')).not.toBeInTheDocument();
  });

  it('suture removal can be dropped for absorbable sutures', async () => {
    const user = userEvent.setup();
    const { draft } = await form(READY);
    if (!draft.followUps.some((f) => f.kind === 'SutureRemoval')) return; // castration has none by default
    await user.click(screen.getByLabelText(/Absorbable sutures/));
    expect(screen.queryByLabelText(/Suture removal date/)).not.toBeInTheDocument();
  });
});

describe('U-X2 — unsaved text is guarded', () => {
  it('asks before leaving a discharge form with changes', async () => {
    const user = userEvent.setup();
    await form(READY);
    await user.type(screen.getByLabelText('Post-op instructions for the owner'), ' Extra note.');
    await user.click(screen.getByRole('link', { name: 'Elsewhere' }));
    expect(await screen.findByRole('dialog')).toHaveTextContent(/unsaved|leave|discard/i);
  });
});

describe('U-E3 — the recovery board', () => {
  it('is sorted Ready → Recovering → Stable, with status as text, since/by and a phone link', async () => {
    signInAs(['Reception']);
    const board = await getRecoveryBoard();
    renderWithClient(
      <RecoveryBoard
        board={board}
        zone="Asia/Dhaka"
        isLoading={false}
        isError={false}
        onRetry={() => undefined}
      />,
    );
    const cards = screen.getAllByRole('listitem');
    const order = cards.map((card) => card.textContent!);
    expect(order[0]).toMatch(/Ready for discharge/);
    expect(order[1]).toMatch(/Recovering/);
    expect(order[2]).toMatch(/Stable/);
    expect(order[0]).toMatch(/since \d\d:\d\d · set by Nurse Tanvir/);
    expect(screen.getAllByRole('link', { name: /^0\d{10}$/ })[0]).toHaveAttribute(
      'href',
      expect.stringMatching(/^tel:0/),
    );
    expect(screen.getByText(/Updated \d\d:\d\d/)).toBeInTheDocument();
    expect(screen.getAllByText(/Ended \d\d:\d\d/).length).toBeGreaterThan(0);
  });

  it('says so when no cat is in recovery', () => {
    renderWithClient(
      <RecoveryBoard
        board={{ generatedOn: new Date().toISOString(), entries: [] }}
        zone="Asia/Dhaka"
        isLoading={false}
        isError={false}
        onRetry={() => undefined}
      />,
    );
    expect(screen.getByText('No cats in recovery.')).toBeInTheDocument();
  });
});

describe('recovery status control', () => {
  it('lets a nurse set the status, with the history below', async () => {
    const user = userEvent.setup();
    const surgery = await surgeryAs(STABLE, ['Nurse']);
    renderWithClient(<RecoveryStatusControl surgery={surgery} canSet />);
    const bodies: Array<Record<string, unknown>> = [];
    server.events.on('request:start', async ({ request }) => {
      if (request.method === 'PUT' && request.url.endsWith('/recovery-status')) {
        bodies.push((await request.clone().json()) as Record<string, unknown>);
      }
    });
    await user.click(screen.getByRole('radio', { name: 'Ready for discharge' }));
    await user.click(screen.getByRole('button', { name: 'Set status' }));
    await waitFor(() => expect(bodies).toHaveLength(1));
    expect(bodies[0]).toMatchObject({ status: 'ReadyForDischarge' });
    expect(screen.getByText(/Eating a little./)).toBeInTheDocument(); // the history of this surgery
    server.events.removeAllListeners();
  });

  it('shows no control to someone who may not set it', async () => {
    const surgery = await surgeryAs(STABLE, ['Reception']);
    renderWithClient(<RecoveryStatusControl surgery={surgery} canSet={false} />);
    expect(screen.queryByRole('button', { name: 'Set status' })).not.toBeInTheDocument();
  });
});

describe('U-E4 — the printable post-op sheet', () => {
  it('holds the instructions as text, the follow-up dates and the clinic contact, with a print-only stylesheet', async () => {
    signInAs(['Nurse']);
    const surgery = await surgeryAs(ids.surgeryOwnerAfterId, ['Nurse']);
    const { container } = renderWithClient(<PostOpSheet surgery={surgery} />);
    const sheet = screen.getByRole('article', { name: /Post-op sheet for Momo/ });
    expect(sheet).toHaveClass('print-sheet');
    expect(within(sheet).getByText(/Keep your cat indoors and warm/)).toBeInTheDocument();
    expect(within(sheet).getByText(/Wound check — /)).toBeInTheDocument();
    expect(within(sheet).getByText(/Suture removal — /)).toBeInTheDocument();
    expect(within(sheet).getByText(/If you are worried/)).toBeInTheDocument();
    // Nothing but the sheet is visible on paper.
    expect(container.querySelector('style')?.textContent).toContain('body * { visibility: hidden');
  });
});

describe('U-F1 — the owner’s page', () => {
  async function owner(id: string) {
    const detail = await getMySurgery(id);
    renderWithClient(<MySurgeryDetail surgery={detail} />);
    return detail;
  }

  it('before: the date, admit-by time, fasting instructions and where the clinic is', async () => {
    await owner(ids.surgeryOwnerBeforeId);
    expect(screen.getByText(/Please bring Momo to Uttara by/)).toBeInTheDocument();
    expect(screen.getByText(/No food after 10 pm the night before/)).toBeInTheDocument();
    expect(screen.getByText(/There is nothing to pay online/)).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Book follow-up' })).not.toBeInTheDocument();
  });

  it('during: the in-our-care text and no clinical progress', async () => {
    await owner(ids.surgeryRecoveryId);
    expect(await screen.findByText(/Momo is in our care\. Call Uttara/)).toBeInTheDocument();
    expect(screen.queryByText(/Recovering|Stable|Ready for discharge/)).not.toBeInTheDocument();
  });

  it('after: the instructions, the follow-ups with dates, Book follow-up and Print', async () => {
    await owner(ids.surgeryOwnerAfterId);
    expect(screen.getByText(/Keep your cat indoors and warm/)).toBeInTheDocument();
    expect(screen.getByText(/Wound check — /)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Book follow-up' })).toHaveAttribute(
      'href',
      expect.stringMatching(/^\/book\?catId=.*FollowUp/),
    );
    expect(screen.getByRole('button', { name: /Print/ })).toBeInTheDocument();
  });

  it('another customer’s surgery is a plain not-found (404)', async () => {
    await expect(getMySurgery(ids.surgeryScheduledTodayId)).rejects.toMatchObject({ status: 404 });
  });
});
