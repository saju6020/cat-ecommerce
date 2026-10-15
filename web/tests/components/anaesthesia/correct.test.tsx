import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CorrectEntryDialog } from '@/components/anaesthesia/CorrectEntryDialog';
import type { DrugDialogContext } from '@/components/anaesthesia/DrugEntryDialog';
import { getAnaesthesiaLog } from '@/lib/api/anaesthesia-log';
import { surgeryFixtureIds as ids } from '@/mocks/handlers/surgeries';
import { server } from '@/mocks/server';
import { renderWithClient, resetSurgeryTest, signInAs } from '../surgery/helpers';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.setConfig({ testTimeout: 40_000 });

beforeEach(() => {
  resetSurgeryTest();
  signInAs(['Vet']);
});

const context: DrugDialogContext = {
  surgeryId: ids.surgeryInTheatreId,
  catId: ids.catBiscuitId,
  zone: 'Asia/Dhaka',
  weight: null,
  administrators: [{ id: ids.surgeonAkterId, name: 'Dr Akter' }],
};

async function entryWhere(
  pick: (e: Awaited<ReturnType<typeof getAnaesthesiaLog>>['entries'][number]) => boolean,
) {
  const log = await getAnaesthesiaLog(ids.surgeryInTheatreId);
  return log.entries.find(pick)!;
}

describe('correcting an entry (ADR-FE-070)', () => {
  it('needs a reason of at least 10 characters, and the confirm says the original stays', async () => {
    const entry = await entryWhere((e) => e.kind === 'Vitals' && !e.isCorrected);
    const user = userEvent.setup();
    renderWithClient(<CorrectEntryDialog entry={entry} context={context} onClose={vi.fn()} />);
    await user.type(await screen.findByLabelText('Reason for the correction'), 'too short');
    await user.click(screen.getByRole('button', { name: 'Review correction' }));
    expect(await screen.findByText('Give at least 10 characters.')).toBeInTheDocument();

    await user.type(screen.getByLabelText('Reason for the correction'), ' — typed the wrong rate');
    await user.click(screen.getByRole('button', { name: 'Review correction' }));
    const confirm = await screen.findByRole('dialog', { name: 'Add this correction?' });
    expect(confirm).toHaveTextContent('The original entry stays in the log.');
  });

  it('is prefilled from the entry, with units beside the vitals', async () => {
    const entry = await entryWhere((e) => e.kind === 'Vitals' && !e.isCorrected);
    renderWithClient(<CorrectEntryDialog entry={entry} context={context} onClose={vi.fn()} />);
    expect(await screen.findByLabelText(/^Heart rate/)).toHaveValue(entry.values.heartRateBpm);
    expect(screen.getByLabelText(/^Heart rate.*bpm/)).toBeInTheDocument();
  });

  it('a controlled drug correction asks for a NEW witness before anything is sent', async () => {
    const entry = await entryWhere((e) => e.kind === 'Drug' && e.values.isControlled);
    const user = userEvent.setup();
    const posts: Array<Record<string, unknown>> = [];
    server.events.on('request:start', async ({ request }) => {
      if (request.method === 'POST' && request.url.includes('/corrections')) {
        posts.push((await request.clone().json()) as Record<string, unknown>);
      }
    });
    renderWithClient(<CorrectEntryDialog entry={entry} context={context} onClose={vi.fn()} />);
    await user.type(
      await screen.findByLabelText('Reason for the correction'),
      'Dose was entered in the wrong unit',
    );
    await user.click(screen.getByRole('button', { name: 'Review correction' }));
    await user.click(await screen.findByRole('button', { name: 'Continue to the witness' }));
    const witness = await screen.findByRole('dialog', { name: /witness needed/i });
    expect(posts).toHaveLength(0);
    await user.type(
      within(witness).getByLabelText(/witness mobile number or email/i),
      'witness.nurse@purrcare.test',
    );
    await user.type(within(witness).getByLabelText(/witness password/i), 'WitnessPass123!');
    await user.click(within(witness).getByRole('button', { name: 'Confirm and add correction' }));
    await waitFor(() => expect(posts).toHaveLength(1));
    expect(posts[0]).toMatchObject({ witnessIdentifier: 'witness.nurse@purrcare.test' });
    server.events.removeAllListeners();
  });
});
