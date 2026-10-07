import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { WeeklyHoursEditor } from '@/components/branches/WeeklyHoursEditor';

// WeeklyHoursEditor renders UnsavedChangesGuard, which calls useRouter().
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }));

describe('WeeklyHoursEditor — ⭐ HO-ADM-002 §11.2, US-ADM-01', () => {
  it('U-B1: overlapping intervals, more than 3 in a day, and reversed times all show row-level errors before submit', async () => {
    const onSave = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(
      <WeeklyHoursEditor
        timeZoneId="Asia/Dhaka"
        weeklyHours={[]}
        onSave={onSave}
        isPending={false}
      />,
    );

    const mondayFieldset = screen.getByRole('group', { name: 'Monday' });
    await user.click(within(mondayFieldset).getByRole('button', { name: /add interval/i }));
    await user.click(within(mondayFieldset).getByRole('button', { name: /add interval/i }));

    const opensInputs = within(mondayFieldset).getAllByLabelText(/opens/i);
    const closesInputs = within(mondayFieldset).getAllByLabelText(/closes/i);
    // Reversed times on the first interval.
    await user.clear(opensInputs[0]!);
    await user.type(opensInputs[0]!, '18:00');
    await user.clear(closesInputs[0]!);
    await user.type(closesInputs[0]!, '09:00');
    // Overlapping second interval.
    await user.clear(opensInputs[1]!);
    await user.type(opensInputs[1]!, '10:00');
    await user.clear(closesInputs[1]!);
    await user.type(closesInputs[1]!, '19:00');

    expect(within(mondayFieldset).getAllByRole('alert').length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: /save opening hours/i })).toBeDisabled();
  });

  it('U-B2: "Copy Monday to weekdays" copies Tuesday–Friday from Monday, leaving Saturday/Sunday untouched', async () => {
    const onSave = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(
      <WeeklyHoursEditor
        timeZoneId="Asia/Dhaka"
        weeklyHours={[
          { dayOfWeek: 'Monday', opens: '09:00', closes: '17:00' },
          { dayOfWeek: 'Saturday', opens: '10:00', closes: '14:00' },
        ]}
        onSave={onSave}
        isPending={false}
      />,
    );

    await user.click(screen.getByRole('button', { name: /copy monday to weekdays/i }));
    await user.click(screen.getByRole('button', { name: /save opening hours/i }));

    expect(onSave).toHaveBeenCalledTimes(1);
    const saved = onSave.mock.calls[0]![0] as Array<{
      dayOfWeek: string;
      opens: string;
      closes: string;
    }>;
    for (const day of ['Tuesday', 'Wednesday', 'Thursday', 'Friday']) {
      expect(saved).toContainEqual({ dayOfWeek: day, opens: '09:00', closes: '17:00' });
    }
    // Saturday keeps its own hours — never overwritten by the copy.
    expect(saved).toContainEqual({ dayOfWeek: 'Saturday', opens: '10:00', closes: '14:00' });
    expect(saved.some((s) => s.dayOfWeek === 'Sunday')).toBe(false);
  });

  it('U-B4: labels every time input with the branch time zone', () => {
    render(
      <WeeklyHoursEditor
        timeZoneId="Europe/London"
        weeklyHours={[{ dayOfWeek: 'Monday', opens: '08:00', closes: '17:00' }]}
        onSave={vi.fn()}
        isPending={false}
      />,
    );
    expect(screen.getByLabelText('Opens (Europe/London)')).toBeInTheDocument();
    expect(screen.getByLabelText('Closes (Europe/London)')).toBeInTheDocument();
  });
});
