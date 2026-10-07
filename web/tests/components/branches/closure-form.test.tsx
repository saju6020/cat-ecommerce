import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ClosureForm } from '@/components/branches/ClosureForm';

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }));

describe('ClosureForm — HO-ADM-002 §11.2, US-ADM-01', () => {
  it('U-B3: half-day times are disabled and cleared once the range covers more than one day, with an explanation', async () => {
    const onCreate = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(<ClosureForm timeZoneId="Asia/Dhaka" onCreate={onCreate} isPending={false} />);

    // Defaults to a single day (start === end today), so half-day times start enabled.
    const partialFrom = screen.getByLabelText(/half-day from/i) as HTMLInputElement;
    const partialTo = screen.getByLabelText(/half-day to/i) as HTMLInputElement;
    expect(partialFrom).not.toBeDisabled();

    await user.type(partialFrom, '1300');
    await user.type(partialTo, '1800');

    // Widen the range to two days — half-day times are disabled and the note explains why.
    const startsOn = screen.getByLabelText(/start date/i) as HTMLInputElement;
    const endsOn = screen.getByLabelText(/end date/i) as HTMLInputElement;
    await user.clear(startsOn);
    await user.type(startsOn, '2027-05-01');
    await user.clear(endsOn);
    await user.type(endsOn, '2027-05-02');
    expect(partialFrom).toBeDisabled();
    expect(
      screen.getByText(/half-day times only apply to a single-day closure/i),
    ).toBeInTheDocument();

    await user.type(screen.getByLabelText(/reason/i), 'Renovation');
    await user.click(screen.getByRole('button', { name: /^add$/i }));

    expect(onCreate).toHaveBeenCalledWith(
      expect.objectContaining({
        startsOn: '2027-05-01',
        endsOn: '2027-05-02',
        partialFrom: null,
        partialTo: null,
      }),
    );
  });
});
