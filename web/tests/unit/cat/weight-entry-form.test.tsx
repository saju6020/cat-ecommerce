import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { WeightEntryForm } from '@/components/cat/WeightEntryForm';

describe('WeightEntryForm', () => {
  it('U21 — the unit is visible, and both 0 and 31 kg are rejected', async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(<WeightEntryForm onSubmit={onSubmit} />);

    // FieldWithUnit folds the unit into the label's accessible name (UI-8). Regex, not an exact
    // string — the label's raw textContent also carries the required-marker "*" after "(kg)".
    const weightInput = screen.getByLabelText(/^Weight \(kg\)/);
    expect(weightInput).toBeInTheDocument();

    await userEvent.clear(weightInput);
    await userEvent.type(weightInput, '0');
    await userEvent.click(screen.getByRole('button', { name: /record weight/i }));
    expect(await screen.findByText(/between 0\.10 and 30\.00/i)).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();

    await userEvent.clear(weightInput);
    await userEvent.type(weightInput, '31');
    await userEvent.click(screen.getByRole('button', { name: /record weight/i }));
    expect(await screen.findByText(/between 0\.10 and 30\.00/i)).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });
});
