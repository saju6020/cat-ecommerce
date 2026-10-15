import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { ApiError } from '@/lib/api/client';
import { OfflineBoard } from '@/components/offline/OfflineBoard';
import { recordOutcome, resetConnectivityForTests } from '@/lib/connectivity/monitor';
import { DEFAULT_DEVICE_SETTINGS, isBoardLocked, readDeviceSettings } from '@/lib/offline/device';
import { syncSnapshot } from '@/lib/offline/sync';
import { resetXctMock, xctFixtureIds, xctMock } from '@/mocks/handlers/operations';
import {
  installMemoryBackend,
  recordFor,
  removeMemoryBackend,
  renderWithClient,
  seedDevice,
} from './offlineTestUtils';

const replace = vi.fn();
let search = '';
vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace, push: vi.fn() }),
  useSearchParams: () => new URLSearchParams(search),
  usePathname: () => '/staff/offline',
}));

let stores: ReturnType<typeof installMemoryBackend>;
beforeEach(() => {
  stores = installMemoryBackend();
  resetConnectivityForTests();
  resetXctMock();
  xctMock.pingMode = 'fail';
  replace.mockReset();
  search = '';
});
afterEach(() => {
  removeMemoryBackend();
  resetConnectivityForTests();
  vi.useRealTimers();
});

function seed(deviceOverrides: Parameters<typeof seedDevice>[1] = {}) {
  const record = recordFor();
  stores.snapshot.set(record.branchId, record);
  seedDevice(stores, deviceOverrides);
  return record;
}

const LOCKED_TEXT = 'Locked. Sign in again when the connection returns.';

describe('SECURITY HIGH: the board never trusts a cold load', () => {
  it('a fresh load while the server answers goes to the staff app and shows no data', async () => {
    seed();
    xctMock.pingMode = 'ok';
    renderWithClient(<OfflineBoard />);
    await waitFor(() => expect(replace).toHaveBeenCalledWith('/staff/front-desk'));
    expect(screen.queryByText('Luna')).toBeNull();
    expect(screen.queryByRole('heading', { name: /appointments/ })).toBeNull();
  });

  it('a degraded server (5xx) is still a server that answers: it leaves too', async () => {
    seed();
    xctMock.pingMode = 'serverError';
    renderWithClient(<OfflineBoard />);
    await waitFor(() => expect(replace).toHaveBeenCalled());
    expect(screen.queryByText('Luna')).toBeNull();
  });

  it('shows data only while offline', async () => {
    seed();
    renderWithClient(<OfflineBoard />);
    expect(await screen.findByText('Luna')).toBeInTheDocument();
    expect(replace).not.toHaveBeenCalled();
  });

  it('opens locked when the device has been idle longer than idleTimeoutMinutes', async () => {
    seed({ lastActivityAt: new Date(Date.now() - 31 * 60_000).toISOString() });
    renderWithClient(<OfflineBoard />);
    expect(await screen.findByText(LOCKED_TEXT)).toBeInTheDocument();
    expect(screen.queryByText('Luna')).toBeNull();
  });

  it('opens locked when no activity was ever recorded', async () => {
    seed({ lastActivityAt: null });
    renderWithClient(<OfflineBoard />);
    expect(await screen.findByText(LOCKED_TEXT)).toBeInTheDocument();
  });

  it('persists the lock, so a reload (a new mount) is still locked even if activity looks recent', async () => {
    seed({ lastActivityAt: new Date(Date.now() - 31 * 60_000).toISOString() });
    const first = renderWithClient(<OfflineBoard />);
    await screen.findByText(LOCKED_TEXT);
    await waitFor(async () =>
      expect((await readDeviceSettings()).lockedForStaffAccountId).toBe(xctFixtureIds.staffAccountId),
    );
    first.unmount();
    // The timestamp moves again without anyone signing in:
    const device = stores.device.get('settings') as Record<string, unknown>;
    stores.device.set('settings', { ...device, lastActivityAt: new Date().toISOString() });
    renderWithClient(<OfflineBoard />);
    expect(await screen.findByText(LOCKED_TEXT)).toBeInTheDocument();
    expect(screen.queryByText('Luna')).toBeNull();
  });

  it('the idle timer locks an open board and persists it', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    seed();
    renderWithClient(<OfflineBoard />);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(100);
    });
    expect(screen.getByText('Luna')).toBeInTheDocument();
    await act(async () => {
      await vi.advanceTimersByTimeAsync(31 * 60_000);
    });
    expect(screen.getByText(LOCKED_TEXT)).toBeInTheDocument();
    expect((await readDeviceSettings()).lockedForStaffAccountId).toBe(xctFixtureIds.staffAccountId);
  });

  it('counts idle time that passed before the board opened', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    seed({ lastActivityAt: new Date(Date.now() - 25 * 60_000).toISOString() });
    renderWithClient(<OfflineBoard />);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(100);
    });
    expect(screen.getByText('Luna')).toBeInTheDocument();
    await act(async () => {
      await vi.advanceTimersByTimeAsync(6 * 60_000); // 25 + 6 > 30
    });
    expect(screen.getByText(LOCKED_TEXT)).toBeInTheDocument();
  });

  it('a successful sync by anyone lifts the lock, so person B is never stuck on the previous lock', async () => {
    seed({ lockedForStaffAccountId: xctFixtureIds.staffAccountId });
    await syncSnapshot(xctFixtureIds.branchId, xctFixtureIds.otherStaffAccountId, {
      ignoreFreshness: true,
    });
    expect((await readDeviceSettings()).lockedForStaffAccountId).toBeNull();
  });

  it('a failed sync leaves the lock in place', async () => {
    seed({ lockedForStaffAccountId: xctFixtureIds.staffAccountId });
    // A plain network failure: unlike a 403 it does not switch the copy off or reset the settings,
    // so the only thing that could change the lock is the sync wrongly lifting it.
    server.use(http.get('*/api/staff/offline-snapshot', () => HttpResponse.error()));
    await expect(
      syncSnapshot(xctFixtureIds.branchId, xctFixtureIds.staffAccountId, { ignoreFreshness: true }),
    ).rejects.toBeInstanceOf(ApiError);
    const settings = await readDeviceSettings();
    expect(settings.offlineEnabled).toBe(true);
    expect(settings.lockedForStaffAccountId).toBe(xctFixtureIds.staffAccountId);
  });

  it('isBoardLocked: a lastActivityAt in the future is no activity; a little skew is allowed', () => {
    const now = Date.parse('2026-10-14T10:00:00Z');
    const at = (offsetMs: number) => ({
      ...DEFAULT_DEVICE_SETTINGS,
      offlineEnabled: true,
      lastActivityAt: new Date(now + offsetMs).toISOString(),
    });
    expect(isBoardLocked(at(3 * 3_600_000), 30, now)).toBe(true); // wound-back clock: hours ahead
    expect(isBoardLocked(at(5 * 60_000), 30, now)).toBe(true); // five minutes ahead
    expect(isBoardLocked(at(30_000), 30, now)).toBe(false); // within the one-minute skew allowance
    expect(isBoardLocked(at(-5 * 60_000), 30, now)).toBe(false); // recent past: unlocked
    expect(isBoardLocked(at(-31 * 60_000), 30, now)).toBe(true); // idle too long
  });

  it('treats a lastActivityAt in the future as no activity at all', async () => {
    seed({ lastActivityAt: new Date(Date.now() + 3 * 3_600_000).toISOString() });
    renderWithClient(<OfflineBoard />);
    expect(await screen.findByText(LOCKED_TEXT)).toBeInTheDocument();
  });

  it('an open board keeps the device marked active', async () => {
    seed({ lastActivityAt: new Date(Date.now() - 10 * 60_000).toISOString() });
    const lastActivity = () =>
      Date.parse((stores.device.get('settings') as { lastActivityAt: string }).lastActivityAt);
    const before = lastActivity();
    renderWithClient(<OfflineBoard />);
    await screen.findByText('Luna');
    await waitFor(() => expect(lastActivity()).toBeGreaterThan(before));
  });
});

describe('CODE_REVIEW HIGH: a second outage in one page load does not bounce the board', () => {
  it('outage, recovery, outage, open the board: it stays; a later recovery still leaves', async () => {
    seed();
    act(() => {
      recordOutcome('unreachable');
      recordOutcome('unreachable');
    });
    act(() => recordOutcome('reachable')); // recoveredAt is now set
    act(() => {
      recordOutcome('unreachable');
      recordOutcome('unreachable');
    });
    renderWithClient(<OfflineBoard />);
    expect(await screen.findByText('Luna')).toBeInTheDocument();
    expect(replace).not.toHaveBeenCalled();
    act(() => recordOutcome('reachable'));
    await waitFor(() => expect(replace).toHaveBeenCalledWith('/staff/front-desk'));
  });
});
