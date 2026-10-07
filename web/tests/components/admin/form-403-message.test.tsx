import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RoomForm } from '@/components/branches/RoomForm';
import { ApiForbiddenError } from '@/lib/api/client';
import { describeApiError, describeUnexpectedError } from '@/lib/api/errors';

/**
 * HO-ADM-002 §18 A3 (ACCEPTANCE gate F-H1) — a server `403` on a create/edit submit is shown as a
 * clear permission message, never "Something went wrong. Please try again." (a retry cannot help).
 */
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }));

const forbidden = (code = 'ActionNotPermitted') =>
  new ApiForbiddenError({ errors: [{ errorCode: code, errorMessage: 'Not permitted.' }] });

describe('form 403 handling', () => {
  it('RoomForm shows the permission message on a 403 submit, not the generic retry text', async () => {
    const onSave = vi.fn().mockRejectedValue(forbidden());
    const user = userEvent.setup();
    render(<RoomForm isPending={false} onSave={onSave} />);

    await user.type(screen.getByLabelText(/name/i), 'Theatre 2');
    await user.click(screen.getByRole('button', { name: /add room|save|create/i }));

    expect(await screen.findByText(/you don.t have permission to do this/i)).toBeInTheDocument();
    expect(screen.queryByText(/something went wrong/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/ActionNotPermitted/)).not.toBeInTheDocument();
  });

  it('a non-403 failure keeps the generic message', () => {
    expect(describeUnexpectedError(new Error('boom'))).toBe(
      'Something went wrong. Please try again.',
    );
  });

  it('describeApiError maps a 403 too, and the two branch-assignment codes get their own wording', () => {
    expect(describeApiError(forbidden())).toMatch(/don.t have permission/i);
    expect(describeApiError(forbidden('BranchNotAssigned'))).toMatch(/branch picker/i);
    expect(describeApiError(forbidden('BranchAssignmentRequired'))).toMatch(
      /assign you to a branch/i,
    );
  });
});
