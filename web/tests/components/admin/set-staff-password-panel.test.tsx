import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SetStaffPasswordPanel } from '@/components/admin/SetStaffPasswordPanel';
import { ApiValidationError } from '@/lib/api/client';

// Owner, 2026-10-19: an administrator sets a staff member's password and shares it with them.
describe('SetStaffPasswordPanel', () => {
  it('sets the new password and says to share it', async () => {
    const onSetPassword = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(
      <SetStaffPasswordPanel
        staffName="Rina Das"
        hasPassword
        onSetPassword={onSetPassword}
        isPending={false}
      />,
    );

    expect(screen.getByRole('heading', { name: 'Set a new password' })).toBeInTheDocument();
    await user.type(
      screen.getByLabelText(/new password/i, { selector: 'input' }),
      'a-long-shared-password',
    );
    await user.click(screen.getByRole('button', { name: 'Set password' }));

    expect(onSetPassword).toHaveBeenCalledWith('a-long-shared-password');
    expect(await screen.findByRole('status')).toHaveTextContent(
      'Password set. Share it with Rina Das.',
    );
    expect(screen.getByLabelText(/new password/i, { selector: 'input' })).toHaveValue('');
  });

  it('for someone with no password yet, says "Set a password"', () => {
    render(
      <SetStaffPasswordPanel
        staffName="Rina Das"
        hasPassword={false}
        onSetPassword={vi.fn()}
        isPending={false}
      />,
    );
    expect(screen.getByRole('heading', { name: 'Set a password' })).toBeInTheDocument();
  });

  it('refuses a short password without calling the server', async () => {
    const onSetPassword = vi.fn();
    const user = userEvent.setup();
    render(
      <SetStaffPasswordPanel
        staffName="Rina Das"
        hasPassword
        onSetPassword={onSetPassword}
        isPending={false}
      />,
    );

    await user.type(screen.getByLabelText(/new password/i, { selector: 'input' }), 'short');
    await user.click(screen.getByRole('button', { name: 'Set password' }));

    expect(await screen.findByText(/use at least 10 characters/i)).toBeInTheDocument();
    expect(onSetPassword).not.toHaveBeenCalled();
  });

  it("shows the server's reason (a breached password) on the field", async () => {
    const onSetPassword = vi.fn().mockRejectedValue(
      new ApiValidationError({
        errors: [
          {
            propertyName: 'Password',
            errorCode: 'BreachedPassword',
            errorMessage:
              'This password has appeared in a known data breach. Choose a different one.',
          },
        ],
      } as never),
    );
    const user = userEvent.setup();
    render(
      <SetStaffPasswordPanel
        staffName="Rina Das"
        hasPassword
        onSetPassword={onSetPassword}
        isPending={false}
      />,
    );

    await user.type(
      screen.getByLabelText(/new password/i, { selector: 'input' }),
      'password123456',
    );
    await user.click(screen.getByRole('button', { name: 'Set password' }));

    expect(await screen.findByText(/known data breach/i)).toBeInTheDocument();
  });
});
