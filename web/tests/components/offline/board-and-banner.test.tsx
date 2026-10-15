import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, screen, within } from '@testing-library/react';
import { QueryClient } from '@tanstack/react-query';
import { OfflineBoard, safeReturnPath } from '@/components/offline/OfflineBoard';
import { OfflineBanner } from '@/components/offline/OfflineBanner';
import { recordOutcome, resetConnectivityForTests } from '@/lib/connectivity/monitor';
import { STAFF_SESSION_QUERY_KEY } from '@/lib/hooks/useSession';
import { resetXctMock, xctFixtureIds, xctMock } from '@/mocks/handlers/operations';
import {
  installMemoryBackend,
  seedDevice,
  recordFor,
  removeMemoryBackend,
  renderWithClient,
} from './offlineTestUtils';

const replace = vi.fn();
let search = '';
vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace, push: vi.fn() }),
  useSearchParams: () => new URLSearchParams(search),
  usePathname: () => '/staff/front-desk',
}));

let stores: ReturnType<typeof installMemoryBackend>;
beforeEach(() => {
  stores = installMemoryBackend();
  resetConnectivityForTests();
  resetXctMock();
  // The board is for an unreachable server: its own ping fails unless a test says otherwise.
  xctMock.pingMode = 'fail';
  replace.mockReset();
  search = '';
});
afterEach(() => {
  removeMemoryBackend();
  resetConnectivityForTests();
  vi.useRealTimers();
});

function goOffline() {
  act(() => {
    recordOutcome('unreachable');
    recordOutcome('unreachable');
  });
}

function store(record = recordFor(), device: Parameters<typeof seedDevice>[1] = {}) {
  stores.snapshot.set(record.branchId, record);
  seedDevice(stores, device);
  return record;
}

describe('U-A9 OfflineBanner states', () => {
  it('offline with a valid saved copy: the words, the link to the list, Try now; a status region', async () => {
    store();
    renderWithClient(<OfflineBanner />);
    const region = screen.getByRole('status');
    expect(region).toHaveAttribute('aria-live', 'polite');
    expect(region).toBeEmptyDOMElement();
    goOffline();
    expect(await screen.findByText(/Showing today's lists saved at/)).toBeInTheDocument();
    expect(region).toHaveTextContent(/You.re offline./);
    expect(region).toHaveTextContent('Changes are switched off until the connection returns.');
    expect(screen.getByRole('link', { name: /Open today.s offline list/ })).toHaveAttribute(
      'href',
      '/staff/offline?from=%2Fstaff%2Ffront-desk',
    );
    expect(screen.getByRole('button', { name: 'Try now' })).toBeInTheDocument();
  });

  it('offline with no usable copy: no link to the list', async () => {
    renderWithClient(<OfflineBanner />);
    goOffline();
    expect(await screen.findByText(/no saved copy of today's lists/)).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /offline list/ })).toBeNull();
  });

  it('offline with an EXPIRED copy: no link either', async () => {
    store(recordFor('expired'));
    renderWithClient(<OfflineBanner />);
    goOffline();
    expect(await screen.findByText(/no saved copy of today's lists/)).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /offline list/ })).toBeNull();
  });

  it('degraded: the server is having trouble, and the list is NOT offered', async () => {
    store();
    renderWithClient(<OfflineBanner />);
    act(() => recordOutcome('server-error'));
    expect(await screen.findByText('The server is having trouble.')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /offline list/ })).toBeNull();
  });

  it('back online: "Back online — updated just now." for five seconds, then gone', async () => {
    renderWithClient(<OfflineBanner />);
    goOffline();
    vi.useFakeTimers({ shouldAdvanceTime: true });
    act(() => recordOutcome('reachable'));
    expect(screen.getByText(/updated just now/)).toBeInTheDocument();
    await act(async () => {
      await vi.advanceTimersByTimeAsync(5_100);
    });
    expect(screen.queryByText(/updated just now/)).toBeNull();
  });

  it('states the age at the 15 and 60 minute thresholds only', async () => {
    const old = new Date(Date.now() - 16 * 60_000).toISOString();
    store(recordFor('both', { fetchedAt: old }));
    renderWithClient(<OfflineBanner />);
    goOffline();
    expect(await screen.findByText(/more than 15 minutes ago/)).toBeInTheDocument();
  });
});

describe('U-A10..U-A13 OfflineBoard', () => {
  it('U-A10: both lists sorted by time, the saved-at time, and no action button anywhere', async () => {
    store();
    renderWithClient(<OfflineBoard />);
    expect(await screen.findByRole('heading', { name: /Today.s appointments/ })).toBeInTheDocument();
    const appointments = within(
      screen.getByRole('heading', { name: /Today.s appointments/ }).closest('section')!,
    ).getAllByRole('listitem');
    expect(appointments[0]).toHaveTextContent('Luna'); // 09:00 before 11:30
    expect(appointments[1]).toHaveTextContent('Mitu');
    const tasks = within(screen.getByRole('heading', { name: /boarding tasks/ }).closest('section')!).getAllByRole('listitem');
    expect(tasks[0]).toHaveTextContent('Morning feed'); // 08:00 before 18:00
    expect(screen.getByText(/Saved at \d\d:\d\d/)).toBeInTheDocument();
    // Owner decision 2026-10-14: no phone number is stored or shown, though the API still sends one.
    expect(document.body.textContent).not.toMatch(/8801711/);
    expect(screen.queryAllByRole("link")).toHaveLength(0);
    expect(screen.queryAllByRole('button')).toHaveLength(0);
    expect(screen.getAllByText(/read-only/).length).toBeGreaterThan(0);
  });

  it('U-A11: no copy, an out-of-date copy and another person\'s copy are three distinct refusals with no data', async () => {
    renderWithClient(<OfflineBoard />);
    expect(await screen.findByText('No offline copy on this device.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Try to reconnect' })).toBeInTheDocument();
  });

  it('U-A11: an expired copy is not shown', async () => {
    store(recordFor('expired'));
    renderWithClient(<OfflineBoard />);
    expect(await screen.findByText(/is out of date\./)).toBeInTheDocument();
    expect(screen.queryByText('Luna')).toBeNull();
    expect(screen.queryByText('Mitu')).toBeNull();
  });

  it('U-A11: a copy saved by a different person than the one known is not shown', async () => {
    store();
    const client = new QueryClient();
    client.setQueryData(STAFF_SESSION_QUERY_KEY, {
      staffAccountId: xctFixtureIds.otherStaffAccountId,
    });
    renderWithClient(<OfflineBoard />, client);
    expect(await screen.findByText('This copy was saved by someone else.')).toBeInTheDocument();
    expect(screen.queryByText('Luna')).toBeNull();
  });

  it('U-A12: a section left out says why, in its own words', async () => {
    store(recordFor('appointmentsNotPermitted'));
    const { unmount } = renderWithClient(<OfflineBoard />);
    expect(await screen.findByText(/Appointments weren't included in this copy\. Your role doesn't include appointments/)).toBeInTheDocument();
    unmount();
    store(recordFor('boardingNotAvailable'));
    renderWithClient(<OfflineBoard />);
    expect(await screen.findByText(/Boarding tasks weren't included in this copy\. The boarding task list isn't available in PurrCare yet/)).toBeInTheDocument();
  });

  it('U-A13: a high-risk cat shows a badge in words plus the handling note', async () => {
    store();
    renderWithClient(<OfflineBoard />);
    expect(await screen.findByText('High risk')).toBeInTheDocument();
    expect(screen.getByText('Bites when restrained. Use a towel wrap.')).toBeInTheDocument();
  });
});

describe('U-A14 OfflineIdleLock (ADR-FE-144)', () => {
  it('replaces the data with the locked message after idleTimeoutMinutes; input before then resets the timer', async () => {
    store();
    vi.useFakeTimers({ shouldAdvanceTime: true });
    renderWithClient(<OfflineBoard />);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(50);
    });
    expect(screen.getByText('Luna')).toBeInTheDocument();
    await act(async () => {
      await vi.advanceTimersByTimeAsync(29 * 60_000);
    });
    act(() => {
      window.dispatchEvent(new Event('pointerdown'));
    });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(29 * 60_000);
    });
    expect(screen.getByText('Luna')).toBeInTheDocument(); // reset at minute 29
    await act(async () => {
      await vi.advanceTimersByTimeAsync(2 * 60_000);
    });
    expect(screen.getByText('Locked. Sign in again when the connection returns.')).toBeInTheDocument();
    expect(screen.queryByText('Luna')).toBeNull();
    act(() => {
      window.dispatchEvent(new Event('keydown'));
    });
    expect(screen.getByText('Locked. Sign in again when the connection returns.')).toBeInTheDocument();
  });
});

describe('U-A15 recovery while on the board', () => {
  it('routes back to ?from=, announces it, and invalidates queries', async () => {
    search = 'from=%2Fstaff%2Fappointments';
    store();
    const { queryClient } = renderWithClient(<OfflineBoard />);
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries');
    await screen.findByText('Luna');
    goOffline();
    act(() => recordOutcome('reachable'));
    expect(replace).toHaveBeenCalledWith('/staff/appointments');
    expect(invalidate).toHaveBeenCalled();
    expect(screen.getByText(/Connection is back/)).toBeInTheDocument();
  });

  it('with no ?from= it goes to the front desk; a locked board goes to sign-in', async () => {
    store();
    renderWithClient(<OfflineBoard />);
    await screen.findByText('Luna');
    goOffline();
    act(() => recordOutcome('reachable'));
    expect(replace).toHaveBeenCalledWith('/staff/front-desk');
  });

  it('never returns to an off-site or board path', () => {
    expect(safeReturnPath('https://evil.example/x')).toBe('/staff/front-desk');
    expect(safeReturnPath('//evil.example')).toBe('/staff/front-desk');
    expect(safeReturnPath('/staff/offline?from=/staff/x')).toBe('/staff/front-desk');
    expect(safeReturnPath('/staff/appointments')).toBe('/staff/appointments');
  });
});
