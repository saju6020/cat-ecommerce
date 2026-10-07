import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';

/** A trigger + ConfirmDialog pair, the shape every real caller uses (open state lives with the
 *  caller — HO-CAT-002 §8.4, "ephemeral UI... useState, local to the component"). */
function Harness({ onConfirm }: { onConfirm: () => void | Promise<void> }) {
  const [open, setOpen] = useState(false);
  return (
    <div>
      <button type="button" onClick={() => setOpen(true)}>
        Mark deceased
      </button>
      <ConfirmDialog
        open={open}
        onOpenChange={setOpen}
        title="Mark Whiskers as deceased"
        consequence="Reminders stop, Whiskers is hidden from booking and ordering, and the record is kept."
        onConfirm={onConfirm}
      />
    </div>
  );
}

describe('ConfirmDialog', () => {
  it('requires an explicit consequence to be stated, not a generic "Are you sure?"', async () => {
    render(<Harness onConfirm={vi.fn()} />);
    await userEvent.click(screen.getByRole('button', { name: 'Mark deceased' }));

    expect(
      screen.getByText(
        'Reminders stop, Whiskers is hidden from booking and ordering, and the record is kept.',
      ),
    ).toBeInTheDocument();
    expect(screen.queryByText(/are you sure/i)).not.toBeInTheDocument();
  });

  it('Escape closes the dialog and returns focus to its trigger', async () => {
    render(<Harness onConfirm={vi.fn()} />);
    const trigger = screen.getByRole('button', { name: 'Mark deceased' });
    await userEvent.click(trigger);

    expect(screen.getByRole('dialog')).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await waitFor(() => expect(trigger).toHaveFocus());
  });

  it('calls onConfirm only when the confirm button is clicked, never on open', async () => {
    const onConfirm = vi.fn().mockResolvedValue(undefined);
    render(<Harness onConfirm={onConfirm} />);
    await userEvent.click(screen.getByRole('button', { name: 'Mark deceased' }));

    expect(onConfirm).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Confirm' }));

    await waitFor(() => expect(onConfirm).toHaveBeenCalledOnce());
  });

  it('cancel closes without confirming', async () => {
    const onConfirm = vi.fn();
    render(<Harness onConfirm={onConfirm} />);
    await userEvent.click(screen.getByRole('button', { name: 'Mark deceased' }));
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(onConfirm).not.toHaveBeenCalled();
  });
});
