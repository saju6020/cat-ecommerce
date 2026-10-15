import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import NewReportSchedulePage from '@/app/(staff)/staff/admin/report-schedules/new/page';
import { resetReportsMockData } from '@/mocks/handlers/reports';
import { asAdministrator, renderWithClient } from '../reports/helpers';

// TEST gate (HO-RPT-002): the post-save navigation waits 400 ms for UnsavedChangesGuard to pop its
// history entry. A user who leaves inside that window must not be pulled to the new schedule
// (CODE_REVIEW 68). Mutation check: removing the unmount cleanup in `new/page.tsx` survived the
// builder's tests; this pins it.
const push = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push, replace: vi.fn() }),
  usePathname: () => '/staff/admin/report-schedules/new',
  useSearchParams: () => new URLSearchParams('reportKey=revenue-by-business-line'),
}));

beforeEach(() => {
  push.mockReset();
  resetReportsMockData();
  asAdministrator();
});

async function saveTheForm() {
  renderWithClient(<NewReportSchedulePage />);
  await userEvent.click(await screen.findByLabelText(/Rezwan Ahmed/));
  await userEvent.click(screen.getByRole('button', { name: 'Create schedule' }));
}

describe('new schedule page: post-save navigation timer', () => {
  it('navigates to the saved schedule once the settle delay has passed (control)', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      await saveTheForm();
      await waitFor(() =>
        expect(screen.getByRole('button', { name: 'Create schedule' })).toBeDisabled(),
      );
      expect(push).not.toHaveBeenCalled();
      await act(async () => {
        await vi.advanceTimersByTimeAsync(500);
      });
      expect(push).toHaveBeenCalledTimes(1);
      expect(String(push.mock.calls[0]![0])).toMatch(/^\/staff\/admin\/report-schedules/);
    } finally {
      vi.useRealTimers();
    }
  });

  it('does not navigate when the page is left inside the settle delay', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      const { unmount } = await (async () => {
        const view = renderWithClient(<NewReportSchedulePage />);
        await userEvent.click(await screen.findByLabelText(/Rezwan Ahmed/));
        await userEvent.click(screen.getByRole('button', { name: 'Create schedule' }));
        return view;
      })();
      await waitFor(() =>
        expect(screen.getByRole('button', { name: 'Create schedule' })).toBeDisabled(),
      );
      unmount();
      await act(async () => {
        await vi.advanceTimersByTimeAsync(1000);
      });
      expect(push).not.toHaveBeenCalled();
    } finally {
      vi.useRealTimers();
    }
  });
});
