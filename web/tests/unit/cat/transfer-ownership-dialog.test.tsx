import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { TransferOwnershipDialog } from '@/components/cat/TransferOwnershipDialog';

describe('TransferOwnershipDialog', () => {
  it('U27 — states history transfers and the current owner loses access (UI-3)', () => {
    render(
      <TransferOwnershipDialog
        catName="Whiskers"
        open
        onOpenChange={vi.fn()}
        onSubmit={vi.fn().mockResolvedValue(undefined)}
      />,
    );

    const description = screen.getByText(/medical history/i);
    expect(description).toHaveTextContent(/moves to the new owner/i);
    expect(description).toHaveTextContent(/no longer see this record/i);
  });
});
