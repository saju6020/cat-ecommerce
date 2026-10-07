import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MaintenanceSwitch } from '@/components/admin/MaintenanceSwitch';

describe('MaintenanceSwitch — HO-ADM-002 §11.8, US-ADM-08, ADR-053', () => {
  it('turning maintenance on is confirmed, stating the back office and sign-in are unaffected', async () => {
    const onSetMaintenanceMode = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(
      <MaintenanceSwitch
        status={{ maintenanceMode: false, message: null }}
        onSetMaintenanceMode={onSetMaintenanceMode}
        isPending={false}
      />,
    );

    await user.type(screen.getByLabelText(/message shown to customers/i), 'Back soon.');
    await user.click(screen.getByRole('button', { name: /turn maintenance on/i }));
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent(/back office and sign-in are not affected/i);
    await user.click(screen.getByRole('button', { name: /^turn on$/i }));
    expect(onSetMaintenanceMode).toHaveBeenCalledWith(true, 'Back soon.');
  });

  it('turning maintenance off needs no confirm', async () => {
    const onSetMaintenanceMode = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(
      <MaintenanceSwitch
        status={{ maintenanceMode: true, message: 'Down for a bit.' }}
        onSetMaintenanceMode={onSetMaintenanceMode}
        isPending={false}
      />,
    );
    await user.click(screen.getByRole('button', { name: /turn maintenance off/i }));
    expect(onSetMaintenanceMode).toHaveBeenCalledWith(false, 'Down for a bit.');
  });
});
