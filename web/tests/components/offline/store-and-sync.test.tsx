import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, screen, waitFor } from '@testing-library/react';
import { QueryClient } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { OfflineSnapshotSync } from '@/components/offline/OfflineSnapshotSync';
import { enableOfflineCopy, disableOfflineCopy, readLatestSnapshot, readSnapshot, wipeOfflineSnapshots, writeSnapshot, isSnapshotUsable } from '@/lib/offline/snapshot';
import { readDeviceSettings } from '@/lib/offline/device';
import { SnapshotNotPermittedError, syncSnapshot } from '@/lib/offline/sync';
import { ApiError, ApiForbiddenError, ApiUnauthorizedError } from '@/lib/api/client';
import { buildSnapshot, xctFixtureIds } from '@/mocks/handlers/operations/fixtures';
import { resetXctMock, xctMock } from '@/mocks/handlers/operations';
import { STAFF_SESSION_QUERY_KEY } from '@/lib/hooks/useSession';
import { dbGetAll, OFFLINE_STORES } from '@/lib/offline/db';
import { installMemoryBackend, recordFor, removeMemoryBackend, renderWithClient } from './offlineTestUtils';

vi.mock('@/components/ui/use-toast', () => ({ toast: vi.fn() }));

const staffSession = {
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

/** Every call fetches; the cross-tab guard has its own tests below. */
const sync = (branchId: string, staffAccountId: string) =>
  syncSnapshot(branchId, staffAccountId, { ignoreFreshness: true });

let stores: ReturnType<typeof installMemoryBackend>;
beforeEach(() => {
  stores = installMemoryBackend();
  resetXctMock();
});
afterEach(() => removeMemoryBackend());

function syncClient(branchId: string | null = xctFixtureIds.branchId) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  client.setQueryData(STAFF_SESSION_QUERY_KEY, staffSession);
  client.setQueryData(['staff', 'active-branch'], branchId);
  return client;
}

describe('U-A4 no request and nothing written while the device flag is off', () => {
  it('OfflineSnapshotSync stays silent', async () => {
    renderWithClient(<OfflineSnapshotSync />, syncClient());
    await new Promise((r) => setTimeout(r, 150));
    expect(xctMock.snapshotRequests).toHaveLength(0);
    expect(await dbGetAll('snapshot')).toHaveLength(0);
  });

  it('writeSnapshot itself refuses without the flag', async () => {
    expect(await writeSnapshot(buildSnapshot('both', xctFixtureIds.branchId, 1), '"e"')).toBe(false);
    expect(stores.snapshot.size).toBe(0);
  });
});

describe('U-A5 200 then 304', () => {
  it('writes once, then only moves fetchedAt, sending the stored ETag', async () => {
    await enableOfflineCopy(xctFixtureIds.staffAccountId);
    // Cross-origin the API must expose ETag; the mock does, as a fixed API would.
    await sync(xctFixtureIds.branchId, xctFixtureIds.staffAccountId);
    const first = await readSnapshot(xctFixtureIds.branchId);
    expect(first?.etag).toMatch(/xct-both-v1/);
    expect(xctMock.snapshotRequests[0]).toEqual({ ifNoneMatch: null, status: 200 });

    await new Promise((r) => setTimeout(r, 20));
    await sync(xctFixtureIds.branchId, xctFixtureIds.staffAccountId);
    const second = await readSnapshot(xctFixtureIds.branchId);
    expect(xctMock.snapshotRequests[1]).toEqual({ ifNoneMatch: first?.etag, status: 304 });
    expect(second?.body).toEqual(first?.body);
    expect(Date.parse(second!.fetchedAt)).toBeGreaterThan(Date.parse(first!.fetchedAt));

    xctMock.snapshotVersion += 1; // a status change: a new ETag
    await sync(xctFixtureIds.branchId, xctFixtureIds.staffAccountId);
    expect(xctMock.snapshotRequests[2]?.status).toBe(200);
  });

  it('never sends another person\'s ETag (it hashes content, not the person)', async () => {
    await enableOfflineCopy(xctFixtureIds.staffAccountId);
    await sync(xctFixtureIds.branchId, xctFixtureIds.staffAccountId);
    await sync(xctFixtureIds.branchId, xctFixtureIds.otherStaffAccountId);
    expect(xctMock.snapshotRequests[1]?.ifNoneMatch).toBeNull();
  });
});

describe('U-A6 401 during sync', () => {
  it('session_invalid clears the store; session_idle_timeout leaves it alone', async () => {
    await enableOfflineCopy(xctFixtureIds.staffAccountId);
    await sync(xctFixtureIds.branchId, xctFixtureIds.staffAccountId);
    expect(stores.snapshot.size).toBe(1);

    server.use(http.get('*/api/staff/offline-snapshot', () => HttpResponse.json({ error: 'session_idle_timeout', idleTimeoutMinutes: 30 }, { status: 401 })));
    await expect(sync(xctFixtureIds.branchId, xctFixtureIds.staffAccountId)).rejects.toBeInstanceOf(ApiUnauthorizedError);
    expect(stores.snapshot.size).toBe(1);

    server.use(http.get('*/api/staff/offline-snapshot', () => HttpResponse.json({ error: 'session_invalid' }, { status: 401 })));
    await expect(sync(xctFixtureIds.branchId, xctFixtureIds.staffAccountId)).rejects.toBeInstanceOf(ApiUnauthorizedError);
    expect(stores.snapshot.size).toBe(0);
  });

  it('a network failure leaves the record alone', async () => {
    await enableOfflineCopy(xctFixtureIds.staffAccountId);
    await sync(xctFixtureIds.branchId, xctFixtureIds.staffAccountId);
    server.use(http.get('*/api/staff/offline-snapshot', () => HttpResponse.error()));
    await expect(sync(xctFixtureIds.branchId, xctFixtureIds.staffAccountId)).rejects.toBeInstanceOf(ApiError);
    expect(stores.snapshot.size).toBe(1);
  });

  it('403 (not permitted) clears the copy and switches the device flag off', async () => {
    await enableOfflineCopy(xctFixtureIds.staffAccountId);
    await sync(xctFixtureIds.branchId, xctFixtureIds.staffAccountId);
    xctMock.snapshotScenario = 'forbidden';
    await expect(sync(xctFixtureIds.branchId, xctFixtureIds.staffAccountId)).rejects.toBeInstanceOf(SnapshotNotPermittedError);
    expect(stores.snapshot.size).toBe(0);
    expect((await readDeviceSettings()).offlineEnabled).toBe(false);
  });

  it('a branch refusal (403 without ActionNotPermitted) does not switch the copy off', async () => {
    await enableOfflineCopy(xctFixtureIds.staffAccountId);
    server.use(http.get('*/api/staff/offline-snapshot', () => HttpResponse.json({ error: 'BranchNotAssigned' }, { status: 403 })));
    await expect(sync(xctFixtureIds.branchId, xctFixtureIds.staffAccountId)).rejects.toBeInstanceOf(ApiForbiddenError);
    expect((await readDeviceSettings()).offlineEnabled).toBe(true);
  });
});

describe('U-A16 / E-A8 / E-A9 every wipe path', () => {
  async function seed() {
    await enableOfflineCopy(xctFixtureIds.staffAccountId);
    await sync(xctFixtureIds.branchId, xctFixtureIds.staffAccountId);
    expect(stores.snapshot.size).toBe(1);
  }

  it('sign-out (the staff session query is removed from the cache) wipes the store', async () => {
    await seed();
    const client = syncClient();
    renderWithClient(<OfflineSnapshotSync />, client);
    await act(async () => {
      client.clear();
    });
    await waitFor(() => expect(stores.snapshot.size).toBe(0));
  });

  it('a person change: another person\'s record is removed before theirs is written', async () => {
    await seed();
    await sync(xctFixtureIds.branchId, xctFixtureIds.otherStaffAccountId);
    const all = await dbGetAll<{ staffAccountId: string }>('snapshot');
    expect(all.map((r) => r.staffAccountId)).toEqual([xctFixtureIds.staffAccountId]);
  });

  it('the opt-out wipes the copy', async () => {
    await seed();
    await disableOfflineCopy();
    expect(stores.snapshot.size).toBe(0);
    expect((await readDeviceSettings()).offlineEnabled).toBe(false);
  });

  it('"Clear this device" wipes the copy and keeps the flag', async () => {
    await seed();
    await wipeOfflineSnapshots('cleared-by-user');
    expect(stores.snapshot.size).toBe(0);
    expect((await readDeviceSettings()).offlineEnabled).toBe(true);
  });

  it('an expired record is dropped by the sync component on mount', async () => {
    await enableOfflineCopy(xctFixtureIds.staffAccountId);
    const expired = recordFor('expired');
    stores.snapshot.set(expired.branchId, expired);
    renderWithClient(<OfflineSnapshotSync />, syncClient(null));
    await waitFor(() => expect(stores.snapshot.size).toBe(0));
  });

  it('a record of another schema version is not usable and is dropped', async () => {
    const odd = recordFor('both', { schemaVersion: 2 });
    expect(isSnapshotUsable(odd)).toBe(false);
    await enableOfflineCopy(xctFixtureIds.staffAccountId);
    stores.snapshot.set(odd.branchId, odd);
    renderWithClient(<OfflineSnapshotSync />, syncClient(null));
    await waitFor(() => expect(stores.snapshot.size).toBe(0));
  });

  it('the person-change effect clears a stranger\'s record on mount even with the flag off', async () => {
    const stranger = recordFor('both', { staffAccountId: xctFixtureIds.otherStaffAccountId });
    stores.snapshot.set(stranger.branchId, stranger);
    renderWithClient(<OfflineSnapshotSync />, syncClient(null));
    await waitFor(() => expect(stores.snapshot.size).toBe(0));
  });
});

describe('U-X2 the store holds only the two sanctioned object stores and no secrets', () => {
  it('has exactly snapshot and device, and the snapshot carries no token, password or draft', async () => {
    expect([...OFFLINE_STORES]).toEqual(['snapshot', 'device']);
    await enableOfflineCopy(xctFixtureIds.staffAccountId);
    await sync(xctFixtureIds.branchId, xctFixtureIds.staffAccountId);
    const dump = JSON.stringify([...stores.snapshot.values(), ...stores.device.values()]);
    expect(dump).not.toMatch(/password|token|csrf|captcha|pc_session|draft/i);
    expect(await readLatestSnapshot()).toBeTruthy();
    expect(window.localStorage.length).toBe(0);
    expect(window.sessionStorage.length).toBe(0);
  });
});

describe('the sync component syncs when the flag, session and branch are all present', () => {
  it('fetches and stores a copy', async () => {
    await enableOfflineCopy(xctFixtureIds.staffAccountId);
    renderWithClient(<OfflineSnapshotSync />, syncClient());
    await waitFor(() => expect(stores.snapshot.size).toBe(1));
    expect(screen.queryByRole('button')).toBeNull();
  });
});
