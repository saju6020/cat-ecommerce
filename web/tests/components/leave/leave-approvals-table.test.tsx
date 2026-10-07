import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { LeaveApprovalsTable } from '@/components/leave/LeaveApprovalsTable';
import type { LeaveRequest } from '@/lib/api/leave';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: vi.fn() }),
  usePathname: () => '/staff/admin/leave',
  useSearchParams: () => new URLSearchParams(),
}));

const REQUEST: LeaveRequest = {
  id: 'l1',
  staffAccountId: 's1',
  staffFullName: 'Nadia Nurse',
  startsOn: '2027-03-10',
  endsOn: '2027-03-12',
  kind: 'Annual',
  note: null,
  status: 'Requested',
  decidedBy: null,
  decidedOn: null,
  decisionNote: null,
};

describe('LeaveApprovalsTable — HO-ADM-002 §11.4, US-ADM-03', () => {
  it('U-D4: approving confirms naming the person and dates, then the list refreshes', async () => {
    const onApprove = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(
      <LeaveApprovalsTable
        requests={[REQUEST]}
        totalCount={1}
        pageSize={20}
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
        onApprove={onApprove}
        onReject={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    await user.click(screen.getByRole('button', { name: /^approve$/i }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/nadia nurse/i)).toBeInTheDocument();
    expect(within(dialog).getByText(/10 mar 2027|mar 2027/i)).toBeInTheDocument();

    await user.click(within(dialog).getByRole('button', { name: /^approve$/i }));
    expect(onApprove).toHaveBeenCalledWith(REQUEST);
  });

  it('U-D4: rejecting sends the note the administrator typed', async () => {
    const onReject = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(
      <LeaveApprovalsTable
        requests={[REQUEST]}
        totalCount={1}
        pageSize={20}
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
        onApprove={vi.fn()}
        onReject={onReject}
        onCancel={vi.fn()}
      />,
    );

    await user.click(screen.getByRole('button', { name: /^reject$/i }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/nadia nurse/i)).toBeInTheDocument();
    await user.type(
      within(dialog).getByLabelText(/note to the staff member/i),
      'Clinic fully booked that week.',
    );
    await user.click(within(dialog).getByRole('button', { name: /^reject$/i }));

    expect(onReject).toHaveBeenCalledExactlyOnceWith(REQUEST, 'Clinic fully booked that week.');
  });

  it('rejecting without a note sends no note rather than an empty string', async () => {
    const onReject = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(
      <LeaveApprovalsTable
        requests={[REQUEST]}
        totalCount={1}
        pageSize={20}
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
        onApprove={vi.fn()}
        onReject={onReject}
        onCancel={vi.fn()}
      />,
    );

    await user.click(screen.getByRole('button', { name: /^reject$/i }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText(/note to the staff member/i), '   ');
    await user.click(within(dialog).getByRole('button', { name: /^reject$/i }));

    expect(onReject).toHaveBeenCalledExactlyOnceWith(REQUEST, undefined);
  });

  it('the sole-administrator self-approval note is shown verbatim from the server, never re-explained', () => {
    const decided: LeaveRequest = {
      ...REQUEST,
      status: 'Approved',
      decisionNote: '[self-approved: sole administrator]',
    };
    render(
      <LeaveApprovalsTable
        requests={[decided]}
        totalCount={1}
        pageSize={20}
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
        onApprove={vi.fn()}
        onReject={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByText('[self-approved: sole administrator]')).toBeInTheDocument();
  });
});
