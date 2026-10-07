import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RoomForm } from '@/components/branches/RoomForm';
import type { Room } from '@/lib/api/admin-branches';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  usePathname: () => '/staff/admin/branches/b1/rooms',
}));

const ROOM: Room = { id: 'r1', branchId: 'b1', name: 'Theatre 1', kind: 'Theatre', isActive: true };

describe('RoomForm — ADM 28 deactivation confirm', () => {
  it('unticking Active asks for confirmation first; Cancel saves nothing, Deactivate saves isActive=false', async () => {
    const onSave = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(<RoomForm room={ROOM} onSave={onSave} isPending={false} />);

    await user.click(screen.getByRole('checkbox', { name: /active/i }));
    await user.click(screen.getByRole('button', { name: /^save$/i }));

    let dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/will stop being offered/i)).toBeInTheDocument();
    expect(onSave).not.toHaveBeenCalled();

    await user.click(within(dialog).getByRole('button', { name: /^cancel$/i }));
    expect(onSave).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: /^save$/i }));
    dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: /^deactivate$/i }));
    await waitFor(() => expect(onSave).toHaveBeenCalledOnce());
    expect(onSave).toHaveBeenCalledWith({ name: 'Theatre 1', kind: 'Theatre', isActive: false });
  });

  it('saving an active room without unticking Active needs no confirmation', async () => {
    const onSave = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(<RoomForm room={ROOM} onSave={onSave} isPending={false} />);
    await user.click(screen.getByRole('button', { name: /^save$/i }));
    await waitFor(() => expect(onSave).toHaveBeenCalledOnce());
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
