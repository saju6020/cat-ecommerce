import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RosterPatternEditor } from '@/components/roster/RosterPatternEditor';

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }));

describe('RosterPatternEditor — HO-ADM-002 §11.4, US-ADM-03', () => {
  it('U-D3: scheduling a next pattern requires an effective date after today in branch time', async () => {
    const onSetPattern = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(
      <RosterPatternEditor
        staffFullName="Nadia Nurse"
        timeZoneId="Asia/Dhaka"
        currentPattern={[]}
        nextPattern={null}
        nextPatternEffectiveFrom={null}
        rooms={[]}
        onSetPattern={onSetPattern}
        onCancelScheduled={vi.fn()}
        isPending={false}
      />,
    );

    await user.click(screen.getByRole('radio', { name: /schedule this as next pattern from/i }));
    const dateInput = screen.getByLabelText(/effective date/i);
    expect(dateInput).toHaveAttribute('min');

    await user.click(screen.getByRole('button', { name: /save pattern/i }));
    expect(await screen.findByRole('alert')).toHaveTextContent(/after today/i);
    expect(onSetPattern).not.toHaveBeenCalled();
  });

  it('"Apply now" replaces the current pattern with no effective date required', async () => {
    const onSetPattern = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(
      <RosterPatternEditor
        staffFullName="Nadia Nurse"
        timeZoneId="Asia/Dhaka"
        currentPattern={[]}
        nextPattern={null}
        nextPatternEffectiveFrom={null}
        rooms={[]}
        onSetPattern={onSetPattern}
        onCancelScheduled={vi.fn()}
        isPending={false}
      />,
    );

    await user.click(screen.getByRole('button', { name: /save pattern/i }));
    expect(onSetPattern).toHaveBeenCalledWith([], null);
  });

  it('offers "Cancel scheduled pattern" when one exists, naming the effective date in the confirm', async () => {
    const onCancelScheduled = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(
      <RosterPatternEditor
        staffFullName="Nadia Nurse"
        timeZoneId="Asia/Dhaka"
        currentPattern={[]}
        nextPattern={[{ dayOfWeek: 'Monday', starts: '09:00', ends: '17:00', defaultRoomId: null }]}
        nextPatternEffectiveFrom="2028-01-04"
        rooms={[]}
        onSetPattern={vi.fn()}
        onCancelScheduled={onCancelScheduled}
        isPending={false}
      />,
    );

    await user.click(screen.getByRole('button', { name: /cancel scheduled pattern/i }));
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent('Nadia Nurse');
    expect(dialog).toHaveTextContent(/4 Jan 2028|Jan 2028/);
  });
});
