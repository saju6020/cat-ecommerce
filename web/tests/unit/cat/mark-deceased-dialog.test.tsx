import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MarkDeceasedDialog } from '@/components/cat/MarkDeceasedDialog';

describe('MarkDeceasedDialog', () => {
  it('U26 — names the actual consequences, including that the record is kept (UI-3)', () => {
    render(
      <MarkDeceasedDialog
        catName="Whiskers"
        open
        onOpenChange={vi.fn()}
        onSubmit={vi.fn().mockResolvedValue(undefined)}
      />,
    );

    const description = screen.getByText(/reminders/i);
    expect(description).toHaveTextContent('Reminders');
    expect(description).toHaveTextContent(/hidden from booking and ordering/i);
    expect(description).toHaveTextContent(/record itself is kept/i);
    // Not a generic "Are you sure?" — UI-3.
    expect(screen.queryByText(/are you sure/i)).not.toBeInTheDocument();
  });
});
