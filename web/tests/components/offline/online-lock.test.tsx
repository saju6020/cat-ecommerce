import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, screen, waitFor } from '@testing-library/react';
import { QueryClient } from '@tanstack/react-query';
import { OfflineBoard } from '@/components/offline/OfflineBoard';
import { OfflineSnapshotSync } from '@/components/offline/OfflineSnapshotSync';
import { useStaffLockState } from '@/components/staff/IdleLockContext';
import { resetConnectivityForTests } from '@/lib/connectivity/monitor';
import { STAFF_SESSION_QUERY_KEY } from '@/lib/hooks/useSession';
import { readDeviceSettings } from '@/lib/offline/device';
import { readLatestSnapshot, enableOfflineCopy } from '@/lib/offline/snapshot';
import { recordFor, installMemoryBackend, removeMemoryBackend, renderWithClient } from './offlineTestUtils';
import { resetXctMock, xctFixtureIds, xctMock } from '@/mocks/handlers/operations';

const replace = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace, push: vi.fn() }),
  useSearchParams: () => new URLSearchParams(),
  usePathname: () => '/staff/front-desk',
}));
vi.mock('@/components/ui/use-toast', () => ({ toast: vi.fn() }));

let stores: ReturnType<typeof installMemoryBackend>;
beforeEach(() => {
  stores = installMemoryBackend();
  resetConnectivityForTests();
  resetXctMock();
  replace.mockReset();
});
afterEach(() => {
  vi.useRealTimers();
  removeMemoryBackend();
  resetConnectivityForTests();
});

const session = {
  staffAccountId: xctFixtureIds.staffAccountId,
  fullName: 'Admin',
  roleIds: ['Administrator'],
  issuedOn: '',
  lastActivityOn: '',
  idleExpiresOn: '',
  absoluteExpiresOn: '',
  idleTimeoutMinutes: 30,
  serverTime: '',
};

let lockTerminal: () => void = () => {};
function LockHandle() {
  lockTerminal = useStaffLockState().lock;
  return null;
}

function client() {
  const c = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  c.setQueryData(STAFF_SESSION_QUERY_KEY, session); // no active branch: no snapshot request
  return c;
}

const lastActivity = () =>
  (stores.device.get('settings') as { lastActivityAt: string } | undefined)?.lastActivityAt;

describe('ARCHITECTURE HIGH: the online idle lock must not feed the offline board', () => {
  it('records activity while unlocked, none while the HO-ACC-002 lock is showing, and saves the board lock', async () => {
    // Only the clock is faked, and BEFORE the recorder starts: it captures `Date.now` when it mounts.
    vi.useFakeTimers({ toFake: ['Date'], shouldAdvanceTime: true });
    await enableOfflineCopy(xctFixtureIds.staffAccountId);
    const old = new Date(Date.now() - 20 * 60_000).toISOString();
    stores.device.set('settings', { ...(stores.device.get('settings') as object), lastActivityAt: old });

    renderWithClient(
      <>
        <LockHandle />
        <OfflineSnapshotSync />
      </>,
      client(),
    );
    await waitFor(() => expect(lastActivity()).not.toBe(old)); // unlocked: mounting is activity

    // The online lock fires; the activity timestamp goes back to an old value to see what changes next.
    stores.device.set('settings', { ...(stores.device.get('settings') as object), lastActivityAt: old });
    act(() => lockTerminal());
    await waitFor(async () =>
      expect((await readDeviceSettings()).lockedForStaffAccountId).toBe(xctFixtureIds.staffAccountId),
    );

    // A passer-by moves the mouse and presses keys over the lock dialog, more than a minute after the
    // last write. Activity recording throttles to one write a minute, so inside that minute the events
    // would be ignored whether or not the `!locked` gate exists and the assertion below could not fail.
    vi.setSystemTime(Date.now() + 5 * 60_000);
    act(() => {
      window.dispatchEvent(new Event('pointermove'));
      window.dispatchEvent(new Event('keydown'));
      window.dispatchEvent(new Event('pointerdown'));
    });
    await act(async () => {});
    expect(lastActivity()).toBe(old);
  });

  it('after an online lock followed by an outage, the board opens locked and shows nothing', async () => {
    await enableOfflineCopy(xctFixtureIds.staffAccountId);
    const record = recordFor();
    stores.snapshot.set(record.branchId, record);

    const first = renderWithClient(
      <>
        <LockHandle />
        <OfflineSnapshotSync />
      </>,
      client(),
    );
    await act(async () => {});
    act(() => lockTerminal());
    await waitFor(async () =>
      expect((await readDeviceSettings()).lockedForStaffAccountId).not.toBeNull(),
    );
    act(() => {
      window.dispatchEvent(new Event('pointermove'));
    });
    first.unmount();

    xctMock.pingMode = 'fail'; // the cable is pulled
    renderWithClient(<OfflineBoard />);
    expect(await screen.findByText('Locked. Sign in again when the connection returns.')).toBeInTheDocument();
    expect(screen.queryByText('Luna')).toBeNull();
  });
});

describe('records written before phone numbers were dropped', () => {
  it('are cleaned when read, in memory and in the store', async () => {
    const record = recordFor();
    expect(JSON.stringify(record)).toContain('customerPhone'); // the fixture is the old shape
    stores.snapshot.set(record.branchId, record);
    const read = await readLatestSnapshot();
    expect(JSON.stringify(read)).not.toContain('customerPhone');
    await waitFor(() => expect(JSON.stringify([...stores.snapshot.values()])).not.toContain('customerPhone'));
  });
});
