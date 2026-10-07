import { StrictMode } from 'react';
import { unstable_batchedUpdates } from 'react-dom';
import { describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { StaffBranchAssignmentPanel } from '@/components/branches/StaffBranchAssignmentPanel';

const BRANCHES = [
  { id: 'a', code: 'A', name: 'Main', timeZoneId: 'UTC', isActive: true },
  { id: 'b', code: 'B', name: 'London', timeZoneId: 'UTC', isActive: true },
];

function setup(onSave = vi.fn().mockResolvedValue(undefined)) {
  render(
    <StrictMode>
      <StaffBranchAssignmentPanel
        branches={{ data: BRANCHES, isLoading: false, isError: false, refetch: vi.fn() }}
        assignment={{
          data: { staffAccountId: 's1', branchIds: ['a', 'b'], primaryBranchId: 'a' },
          isLoading: false,
          isError: false,
          refetch: vi.fn(),
        }}
        onSave={onSave}
        isPending={false}
      />
    </StrictMode>,
  );
  return onSave;
}

describe('StaffBranchAssignmentPanel — ADM 30', () => {
  it('unticking the primary branch moves the primary to a remaining branch and saves that', async () => {
    const errors = vi.spyOn(console, 'error').mockImplementation(() => {});
    const onSave = setup();
    const user = userEvent.setup();
    await user.click(await screen.findByRole('checkbox', { name: 'Main' }));
    // no React warning about setting state from inside another state updater
    expect(errors).not.toHaveBeenCalled();
    errors.mockRestore();
    const radios = screen.getAllByRole('radio');
    expect(radios[1]).toBeChecked();
    await user.click(screen.getByRole('button', { name: /save branches/i }));
    await waitFor(() =>
      expect(onSave).toHaveBeenCalledWith({ branchIds: ['b'], primaryBranchId: 'b' }),
    );
  });

  it('several toggles batched into one render still leave a consistent primary (no stale closure)', async () => {
    const onSave = setup();
    const user = userEvent.setup();
    const main = await screen.findByRole('checkbox', { name: 'Main' });
    const london = screen.getByRole('checkbox', { name: 'London' });
    await act(async () => {
      unstable_batchedUpdates(() => {
        main.click(); // remove a (the primary)
        london.click(); // remove b
        main.click(); // add a back
      });
    });
    await user.click(screen.getByRole('button', { name: /save branches/i }));
    await waitFor(() =>
      expect(onSave).toHaveBeenCalledWith({ branchIds: ['a'], primaryBranchId: 'a' }),
    );
  });
});
