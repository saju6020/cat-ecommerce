import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PreOpTab } from '@/components/surgery/PreOpTab';
import { StartPanel } from '@/components/surgery/StartPanel';
import { SurgeryDayList } from '@/components/surgery/SurgeryDayList';
import { getSurgeryDay } from '@/lib/api/surgeries';
import { todayInZone } from '@/lib/format/time';
import { surgeryFixtureIds as ids } from '@/mocks/handlers/surgeries';
import {
  renderWithClient,
  rerenderWithClient,
  resetSurgeryTest,
  signInAs,
  surgeryAs,
} from './helpers';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.setConfig({ testTimeout: 30_000 });

beforeEach(() => resetSurgeryTest());

describe('U-B1 — the day list names what is outstanding', () => {
  it('writes the checklist as text, with the items by name', async () => {
    signInAs(['Reception']);
    const day = await getSurgeryDay(todayInZone('Asia/Dhaka'));
    renderWithClient(
      <SurgeryDayList
        surgeries={day.surgeries}
        zone={day.branchTimeZoneId}
        isLoading={false}
        isError={false}
        onRetry={() => undefined}
      />,
    );
    expect(screen.getByText('3 outstanding: weight, consent, ASA grade')).toBeInTheDocument();
    expect(screen.getAllByText('Complete').length).toBeGreaterThan(0);
    // The flags are words, and the cat links to its surgery.
    expect(screen.getAllByText('High risk').length).toBeGreaterThan(0);
    expect(screen.getAllByRole('link', { name: /Shadow/ })[0]).toHaveAttribute(
      'href',
      expect.stringContaining('/staff/surgeries/'),
    );
  });
});

describe('U-B2 — checklist actions by role', () => {
  async function checklistFor(roles: string[]) {
    const surgery = await surgeryAs(ids.surgeryScheduledTodayId, roles);
    renderWithClient(<PreOpTab surgery={surgery} />);
    await screen.findByRole('heading', { name: 'Pre-op checklist' });
    // roles come from the session query — wait for them to land
    if (roles.includes('Vet')) await screen.findByRole('button', { name: 'Waive' });
    if (roles.includes('Nurse')) await screen.findByRole('button', { name: 'Mark issued' });
    if (roles.includes('Reception'))
      await screen.findByRole('link', { name: 'Record surgery consent' });
  }

  it('a nurse can mark items but cannot waive or grade', async () => {
    await checklistFor(['Nurse']);
    expect(screen.getByRole('button', { name: 'Mark issued' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Mark done' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Record weight' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Waive' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Save ASA grade' })).not.toBeInTheDocument();
  });

  it('a vet can also waive the blood work and grade the cat', async () => {
    await checklistFor(['Vet']);
    expect(screen.getAllByRole('button', { name: 'Waive' })).toHaveLength(1);
    expect(screen.getByRole('radio', { name: 'III' })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'Emergency (E)' })).toBeInTheDocument();
  });

  it('reception sees the consent links and admit, and nothing clinical', async () => {
    await checklistFor(['Reception']);
    expect(screen.getByRole('link', { name: 'Record anaesthesia consent' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Admit' })).toBeInTheDocument();
    for (const name of ['Mark issued', 'Mark done', 'Waive', 'Save ASA grade', 'Start surgery']) {
      expect(screen.queryByRole('button', { name })).not.toBeInTheDocument();
    }
    expect(screen.queryByRole('link', { name: 'Record weight' })).not.toBeInTheDocument();
  });

  it('Waive is offered on the blood work only, with a reason', async () => {
    await checklistFor(['Vet']);
    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: 'Waive' }));
    const dialog = await screen.findByRole('dialog', { name: /Waive the blood work/ });
    await user.click(within(dialog).getByRole('button', { name: 'Waive blood work' }));
    expect(await within(dialog).findByText(/Enter the reason for waiving/i)).toBeInTheDocument();
  });
});

describe('U-B3 — the start gate reads the server’s blockers', () => {
  it('as a nurse, consent is listed and there is no override', async () => {
    const surgery = await surgeryAs(ids.surgeryPreOpConsentOnlyId, ['Nurse']);
    renderWithClient(<StartPanel surgery={surgery} />);
    expect(await screen.findByRole('button', { name: 'Start surgery' })).toBeDisabled();
    expect(screen.getByText(/Consent has not been recorded/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /emergency override/i })).not.toBeInTheDocument();
  });

  it('as a vet, consent being the ONLY blocker offers the emergency override', async () => {
    const surgery = await surgeryAs(ids.surgeryPreOpConsentOnlyId, ['Vet']);
    renderWithClient(<StartPanel surgery={surgery} />);
    expect(
      await screen.findByRole('button', { name: 'Start with emergency override' }),
    ).toBeInTheDocument();
  });

  it('with the checklist ALSO incomplete there is no override, even for a vet', async () => {
    const surgery = await surgeryAs(ids.surgeryPreOpIncompleteId, ['Vet']);
    renderWithClient(<StartPanel surgery={surgery} />);
    await screen.findByText(/pre-op checklist is not complete/i);
    expect(screen.queryByRole('button', { name: /emergency override/i })).not.toBeInTheDocument();
  });
});

describe('U-B4 — the override reason', () => {
  it('keeps Continue disabled below 10 characters and shows the counter', async () => {
    const surgery = await surgeryAs(ids.surgeryPreOpConsentOnlyId, ['Vet']);
    renderWithClient(<StartPanel surgery={surgery} />);
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'Start with emergency override' }));
    const dialog = await screen.findByRole('dialog');
    const reason = within(dialog).getByLabelText('Reason for the emergency override');
    await user.type(reason, '123456789');
    expect(within(dialog).getByRole('button', { name: 'Continue' })).toBeDisabled();
    expect(within(dialog).getByText(/9\/1000 · at least 10 characters/)).toBeInTheDocument();
    await user.type(reason, '0');
    expect(within(dialog).getByRole('button', { name: 'Continue' })).toBeEnabled();
    await user.click(within(dialog).getByRole('button', { name: 'Continue' }));
    // The final confirm names the vet and says the override is permanent.
    expect(await within(dialog).findByText(/Dr Akter, you are starting/)).toBeInTheDocument();
    expect(within(dialog).getByText(/recorded permanently/)).toBeInTheDocument();
  });
});

describe('U-B5 — Start only from the server’s say-so', () => {
  it('is enabled only with Start allowed and no blockers', async () => {
    const base = await surgeryAs(ids.surgeryPreOpConsentOnlyId, ['Nurse']);
    const view = renderWithClient(<StartPanel surgery={{ ...base, startBlockers: [] }} />);
    expect(await screen.findByRole('button', { name: 'Start surgery' })).toBeEnabled();
    rerenderWithClient(
      view,
      <StartPanel surgery={{ ...base, allowedTransitions: ['Cancel'], startBlockers: [] }} />,
    );
    expect(screen.queryByRole('button', { name: 'Start surgery' })).not.toBeInTheDocument();
  });
});

describe('U-B6 — a poll never clears typed text', () => {
  it('keeps the override reason across a refetch', async () => {
    const base = await surgeryAs(ids.surgeryPreOpConsentOnlyId, ['Vet']);
    const view = renderWithClient(<StartPanel surgery={base} />);
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'Start with emergency override' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(
      within(dialog).getByLabelText('Reason for the emergency override'),
      'Cat is collapsing, owner unreachable',
    );
    // A poll lands: a new object with the same facts (and a changed version).
    rerenderWithClient(
      view,
      <StartPanel
        surgery={{ ...base, version: base.version + 1, startBlockers: [...base.startBlockers] }}
      />,
    );
    expect(
      within(screen.getByRole('dialog')).getByLabelText('Reason for the emergency override'),
    ).toHaveValue('Cat is collapsing, owner unreachable');
  });
});
