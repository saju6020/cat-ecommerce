import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import { AnaesthesiaTimeline } from '@/components/anaesthesia/AnaesthesiaTimeline';
import { getAnaesthesiaLog } from '@/lib/api/anaesthesia-log';
import { surgeryFixtureIds as ids } from '@/mocks/handlers/surgeries';
import { renderWithClient, resetSurgeryTest, signInAs } from '../surgery/helpers';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.setConfig({ testTimeout: 30_000 });

beforeEach(() => {
  resetSurgeryTest();
  signInAs(['Vet']);
});

async function show() {
  const log = await getAnaesthesiaLog(ids.surgeryInTheatreId);
  const onCorrect = vi.fn();
  renderWithClient(<AnaesthesiaTimeline log={log} canCorrect={() => true} onCorrect={onCorrect} />);
  return { log, onCorrect };
}

describe('U-C1 — an append-only, ordered timeline', () => {
  it('lists entries by time, with a corrected entry struck through, labelled and linked', async () => {
    const { log } = await show();
    const items = screen.getAllByRole('listitem').filter((li) => li.id.startsWith('entry-'));
    expect(items).toHaveLength(log.entries.length);
    const times = items.map((li) => within(li).getByText(/^\d\d:\d\d$/).textContent!);
    expect([...times].sort()).toEqual(times);

    const corrected = log.entries.find((e) => e.isCorrected)!;
    const item = document.getElementById(`entry-${corrected.id}`)!;
    expect(within(item).getByText('Corrected')).toBeInTheDocument();
    expect(item.querySelector('.line-through')).not.toBeNull();
    const link = within(item).getByRole('link', { name: 'See the correction' });
    expect(link).toHaveAttribute('href', `#entry-${corrected.correctedByEntryId}`);
    expect(
      within(item).getByText(/Corrected at \d\d:\d\d by Dr Akter — Entered 0\.5 by mistake/),
    ).toBeInTheDocument();

    // The correction points back and carries the reason.
    const correction = document.getElementById(`entry-${corrected.correctedByEntryId}`)!;
    expect(within(correction).getByRole('link', { name: 'an earlier entry' })).toHaveAttribute(
      'href',
      `#entry-${corrected.id}`,
    );
  });

  it('says a controlled drug was witnessed, and that stock ran short', async () => {
    await show();
    expect(screen.getByText(/Controlled — witnessed by Nurse Tanvir/)).toBeInTheDocument();
    expect(screen.getByText('Stock shortfall recorded')).toBeInTheDocument();
  });
});

describe('U-C2 — nothing here edits or deletes an entry', () => {
  it('has Correct… and no edit or delete control', async () => {
    const { log } = await show();
    expect(screen.queryByRole('button', { name: /edit|delete|remove/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /edit|delete|remove/i })).not.toBeInTheDocument();
    // Every entry that has not been corrected can be corrected; a corrected one cannot be again.
    const correctable = log.entries.filter((e) => !e.isCorrected).length;
    expect(screen.getAllByRole('button', { name: /^Correct the/ })).toHaveLength(correctable);
  });
});

describe('U-C5 — units on every figure (UI-8)', () => {
  it('writes doses, quantities and vitals with their units', async () => {
    await show();
    expect(screen.getByText(/0\.05\s?mg/)).toBeInTheDocument();
    expect(screen.getByText(/0\.02\s?mg/)).toBeInTheDocument();
    expect(screen.getByText(/drew 1\s?ml/)).toBeInTheDocument();
    expect(screen.getByText(/wasted 0\.5\s?ml/)).toBeInTheDocument();
    expect(screen.getByText(/120\s?bpm/)).toBeInTheDocument();
    expect(screen.getByText(/38\.6\s?°C/)).toBeInTheDocument();
    expect(screen.getAllByText(/98\s?%/).length).toBeGreaterThan(0);
    expect(screen.getByText(/36\s?mmHg/)).toBeInTheDocument();
    // The mg/kg figure is labelled, with the weight it was worked out at.
    expect(
      screen.getAllByText(/mg\/kg at .* kg \(weighed .*\) \(for information\)/).length,
    ).toBeGreaterThan(0);
  });
});
