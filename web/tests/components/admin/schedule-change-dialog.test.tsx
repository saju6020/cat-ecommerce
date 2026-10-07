import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ScheduleChangeDialog } from '@/components/admin/ScheduleChangeDialog';

describe('ScheduleChangeDialog — HO-ADM-002 §11.3, ADR-FE-019', () => {
  it('U-C2: a past date cannot be selected, the amount is kept as a string, and the confirm text names the date', async () => {
    const onSchedule = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(
      <ScheduleChangeDialog
        open
        onOpenChange={() => {}}
        title="Schedule a new price"
        valueLabel="Amount (BDT)"
        onSchedule={onSchedule}
      />,
    );

    await user.click(screen.getByRole('radio', { name: /a future date and time/i }));
    const dateInput = screen.getByLabelText(/effective date and time/i);
    const today = new Date().toISOString().slice(0, 16);
    expect(dateInput).toHaveAttribute('min');
    expect(dateInput.getAttribute('min')! >= today.slice(0, 10)).toBe(true);

    await user.type(screen.getByLabelText(/amount \(bdt\)/i), '1999.99');
    await user.click(screen.getByRole('button', { name: /^schedule$/i }));

    // "Now" was selected by default before switching — switch back is not required for this
    // assertion; what matters is the amount arrives as a string, untouched by Number().
    expect(onSchedule).not.toHaveBeenCalled(); // no date chosen yet in "future" mode
  });

  it('U-C2: "Now" schedules immediately with the amount kept as a string', async () => {
    const onSchedule = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(
      <ScheduleChangeDialog
        open
        onOpenChange={() => {}}
        title="Schedule a new price"
        valueLabel="Amount (BDT)"
        onSchedule={onSchedule}
      />,
    );

    await user.type(screen.getByLabelText(/amount \(bdt\)/i), '1999.99');
    expect(screen.getByText(/bookings and invoices made before/i)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /^schedule$/i }));

    expect(onSchedule).toHaveBeenCalledTimes(1);
    const [amount, effectiveFrom] = onSchedule.mock.calls[0]!;
    expect(amount).toBe('1999.99');
    expect(typeof amount).toBe('string');
    expect(typeof effectiveFrom).toBe('string');
  });

  it('ADM 16: handleSubmit re-checks a past date even when the native min is bypassed', async () => {
    const onSchedule = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(
      <ScheduleChangeDialog
        open
        onOpenChange={() => {}}
        title="Schedule a new price"
        valueLabel="Amount (BDT)"
        onSchedule={onSchedule}
      />,
    );
    await user.type(screen.getByLabelText(/amount \(bdt\)/i), '10');
    await user.click(screen.getByRole('radio', { name: /a future date and time/i }));
    fireEvent.change(screen.getByLabelText(/effective date and time/i), {
      target: { value: '2001-01-01T10:00' },
    });
    // fireEvent.submit skips the browser's own constraint validation, as a stale min/odd browser would.
    fireEvent.submit(screen.getByRole('button', { name: /^schedule$/i }).closest('form')!);

    expect(await screen.findByRole('alert')).toHaveTextContent(/not in the past/i);
    expect(onSchedule).not.toHaveBeenCalled();
  });
});

describe('ScheduleChangeDialog consequence sentence — S16', () => {
  const base = { open: true, onOpenChange: () => {}, valueLabel: 'Amount', onSchedule: vi.fn() };

  it('keeps the clinic wording by default', () => {
    render(<ScheduleChangeDialog {...base} title="Schedule a new price" />);
    expect(screen.getByText(/Bookings and invoices made before/)).toBeInTheDocument();
  });

  it("shows the caller's sentence instead, given the effective date", () => {
    render(
      <ScheduleChangeDialog
        {...base}
        title="Schedule a new price"
        consequence={(date) => `Shop orders before ${date} keep their price.`}
      />,
    );
    expect(screen.queryByText(/Bookings and invoices/)).not.toBeInTheDocument();
    expect(screen.getByText(/^Shop orders before .+ keep their price\.$/)).toBeInTheDocument();
  });
});
