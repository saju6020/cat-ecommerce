import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CatForm } from '@/components/cat/CatForm';

describe('CatForm', () => {
  it('U17 — precision Exact without a DOB blocks submit with a field error', async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(<CatForm mode="register" onSubmit={onSubmit} />);

    await userEvent.type(screen.getByLabelText(/^Name/), 'Whiskers');
    await userEvent.click(screen.getByRole('radio', { name: 'Exact date' }));
    await userEvent.click(screen.getByRole('button', { name: /register cat/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/date of birth/i);
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('U18 — precision Estimated without a DOB submits', async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(<CatForm mode="register" onSubmit={onSubmit} />);

    await userEvent.type(screen.getByLabelText(/^Name/), 'Biscuit');
    // "Estimated age" is the default selection — no DOB field renders at all.
    await userEvent.type(screen.getByLabelText('Owner customer ID'), 'owner-1');
    await userEvent.click(screen.getByRole('button', { name: /register cat/i }));

    await vi.waitFor(() => expect(onSubmit).toHaveBeenCalledOnce());
    expect(onSubmit.mock.calls[0]?.[0]).toMatchObject({
      name: 'Biscuit',
      dateOfBirthPrecision: 'Estimated',
    });
  });

  it('U19 — an empty name is a field error, announced', async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(<CatForm mode="register" onSubmit={onSubmit} />);

    await userEvent.click(screen.getByRole('button', { name: /register cat/i }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(/enter a name/i);
    expect(onSubmit).not.toHaveBeenCalled();
  });
});
