import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { NeuterStatusForm } from '@/components/cat/NeuterStatusForm';

describe('NeuterStatusForm', () => {
  it('U20 — Neutered without a date is a field error', async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(<NeuterStatusForm onSubmit={onSubmit} />);

    await userEvent.selectOptions(screen.getByLabelText('Neuter status'), 'Neutered');
    await userEvent.click(screen.getByRole('button', { name: /save neuter status/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/enter the date/i);
    expect(onSubmit).not.toHaveBeenCalled();
  });
});
