import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HighRiskFlagDialog } from '@/components/cat/HighRiskFlagDialog';

/**
 * TEST gate gap-fill (HO-CAT-002 §15 U31, §18 A21) — `HighRiskFlagDialog` (§11.7) had zero
 * component-level coverage: every other form/dialog in §11.7 has at least one dedicated unit
 * test (`MarkDeceasedDialog`, `TransferOwnershipDialog`), and it is exercised at the component
 * level indirectly via `forms-accessibility.test.tsx`'s label sweep, but not by name. This closes
 * that gap for both directions (flag and clear) without touching any passing test.
 */
describe('HighRiskFlagDialog', () => {
  it('flag mode — every field has an associated label, and a blank reason blocks submit with an announced error', async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(
      <HighRiskFlagDialog
        mode="flag"
        catName="Whiskers"
        open
        onOpenChange={vi.fn()}
        onSubmit={onSubmit}
      />,
    );

    const handlingNote = screen.getByLabelText(/handling note/i);
    const reason = screen.getByLabelText(/^reason/i);
    expect(handlingNote).toBeInTheDocument();
    expect(reason).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /flag as high-risk/i }));

    const reasonError = await screen.findByText(/enter a reason/i);
    expect(reasonError).toHaveAttribute('role', 'alert');
    expect(reason).toHaveAttribute('aria-invalid', 'true');
    expect(reason).toHaveAttribute('aria-describedby', reasonError.id);
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('flag mode — a handling note and a reason submit successfully', async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(
      <HighRiskFlagDialog
        mode="flag"
        catName="Whiskers"
        open
        onOpenChange={vi.fn()}
        onSubmit={onSubmit}
      />,
    );

    await userEvent.type(screen.getByLabelText(/handling note/i), 'Bites when restrained.');
    await userEvent.type(screen.getByLabelText(/^reason/i), 'Bit a vet nurse on 2026-09-01.');
    await userEvent.click(screen.getByRole('button', { name: /flag as high-risk/i }));

    expect(onSubmit).toHaveBeenCalledWith({
      mode: 'flag',
      handlingNote: 'Bites when restrained.',
      reason: 'Bit a vet nurse on 2026-09-01.',
    });
  });

  it('clear mode — a blank reason blocks submit with an announced error (UI-3, UI-6)', async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(
      <HighRiskFlagDialog
        mode="clear"
        catName="Whiskers"
        open
        onOpenChange={vi.fn()}
        onSubmit={onSubmit}
      />,
    );

    // Clear mode has no handling-note field — only the reason.
    expect(screen.queryByLabelText(/handling note/i)).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /clear high-risk flag/i }));

    const reasonError = await screen.findByText(/enter a reason/i);
    expect(reasonError).toHaveAttribute('role', 'alert');
    expect(onSubmit).not.toHaveBeenCalled();
  });
});
