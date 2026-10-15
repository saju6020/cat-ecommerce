import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render } from '@testing-library/react';
import { QueryClient, QueryClientProvider, onlineManager } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { ConnectivityProvider } from '@/components/offline/ConnectivityProvider';
import { ServiceWorkerCleanup } from '@/components/offline/ServiceWorkerCleanup';
import { apiFetch, setConnectivityReporter } from '@/lib/api/client';
import { startActivityRecording } from '@/lib/offline/activity';
import { readDeviceSettings } from '@/lib/offline/device';
import {
  enableOfflineCopy,
  readSnapshot,
  touchSnapshot,
  wipeOfflineSnapshots,
  writeSnapshot,
} from '@/lib/offline/snapshot';
import { syncSnapshot } from '@/lib/offline/sync';
import { resetConnectivityForTests } from '@/lib/connectivity/monitor';
import { buildSnapshot, xctFixtureIds } from '@/mocks/handlers/operations/fixtures';
import { resetXctMock, xctMock } from '@/mocks/handlers/operations';
import { installMemoryBackend, removeMemoryBackend } from './offlineTestUtils';

let stores: ReturnType<typeof installMemoryBackend>;
beforeEach(() => {
  stores = installMemoryBackend();
  resetXctMock();
  resetConnectivityForTests();
});
afterEach(() => {
  removeMemoryBackend();
  resetConnectivityForTests();
  setConnectivityReporter(null);
  vi.useRealTimers();
  Reflect.deleteProperty(navigator, 'serviceWorker');
});

describe('owner decision 2026-10-14: no customer phone number on the device', () => {
  it('the API still sends customerPhone, and it never reaches IndexedDB', async () => {
    const body = buildSnapshot('both', xctFixtureIds.branchId, 1);
    expect(JSON.stringify(body)).toContain('+8801711000111'); // the payload as the API sends it
    await enableOfflineCopy(xctFixtureIds.staffAccountId);
    await writeSnapshot(body, '"e"');
    const stored = JSON.stringify(await readSnapshot(xctFixtureIds.branchId));
    expect(stored).not.toMatch(/customerPhone|8801711/);
    expect(stored).toContain('Luna'); // the rest of the item is kept
  });

  it('also when the copy arrives through a sync', async () => {
    await enableOfflineCopy(xctFixtureIds.staffAccountId);
    await syncSnapshot(xctFixtureIds.branchId, xctFixtureIds.staffAccountId, { ignoreFreshness: true });
    expect(JSON.stringify([...stores.snapshot.values()])).not.toMatch(/customerPhone|8801711/);
  });
});

describe('only one tab refreshes the snapshot', () => {
  it('a copy another tab just confirmed is left alone; an old one is fetched', async () => {
    await enableOfflineCopy(xctFixtureIds.staffAccountId);
    const first = await syncSnapshot(xctFixtureIds.branchId, xctFixtureIds.staffAccountId);
    expect(first.outcome).toBe('written');
    const second = await syncSnapshot(xctFixtureIds.branchId, xctFixtureIds.staffAccountId);
    expect(second.outcome).toBe('fresh-elsewhere');
    expect(xctMock.snapshotRequests).toHaveLength(1); // one request, so one audit row, not two
    const later = await syncSnapshot(xctFixtureIds.branchId, xctFixtureIds.staffAccountId, {
      now: Date.now() + 120_000,
    });
    expect(later.outcome).not.toBe('fresh-elsewhere');
    expect(xctMock.snapshotRequests).toHaveLength(2);
  });

  it('another person never skips on the strength of someone else\'s copy', async () => {
    await enableOfflineCopy(xctFixtureIds.staffAccountId);
    await syncSnapshot(xctFixtureIds.branchId, xctFixtureIds.staffAccountId);
    const other = await syncSnapshot(xctFixtureIds.branchId, xctFixtureIds.otherStaffAccountId);
    expect(other.outcome).not.toBe('fresh-elsewhere');
  });
});

describe('touchSnapshot does not overwrite another tab\'s newer body', () => {
  it('skips when the stored ETag is not the one the 304 confirmed', async () => {
    await enableOfflineCopy(xctFixtureIds.staffAccountId);
    await writeSnapshot(buildSnapshot('both', xctFixtureIds.branchId, 1), '"old"', 1_000);
    await writeSnapshot(buildSnapshot('both', xctFixtureIds.branchId, 2), '"new"', 2_000); // another tab
    await touchSnapshot(xctFixtureIds.branchId, '"old"', 9_000);
    const stored = await readSnapshot(xctFixtureIds.branchId);
    expect(stored?.etag).toBe('"new"');
    expect(Date.parse(stored!.fetchedAt)).toBe(2_000);
    await touchSnapshot(xctFixtureIds.branchId, '"new"', 9_000);
    expect(Date.parse((await readSnapshot(xctFixtureIds.branchId))!.fetchedAt)).toBe(9_000);
  });
});

describe('activity recording is throttled and only while opted in', () => {
  it('writes at most once per interval, and nothing when the device is not opted in', async () => {
    let clock = 1_000_000;
    const stop = startActivityRecording(60_000, () => clock);
    await act(async () => {});
    expect(stores.device.size).toBe(0); // not opted in: nothing written
    stop();

    await enableOfflineCopy(xctFixtureIds.staffAccountId);
    const stop2 = startActivityRecording(60_000, () => clock);
    await act(async () => {});
    expect((await readDeviceSettings()).lastActivityAt).toBe(new Date(1_000_000).toISOString());
    clock += 10_000;
    window.dispatchEvent(new Event('keydown'));
    await act(async () => {});
    expect((await readDeviceSettings()).lastActivityAt).toBe(new Date(1_000_000).toISOString()); // throttled
    clock += 60_000;
    window.dispatchEvent(new Event('keydown'));
    await act(async () => {});
    expect((await readDeviceSettings()).lastActivityAt).toBe(new Date(1_070_000).toISOString());
    stop2();
  });
});

describe('service-worker clean-up (ADR-FE-140)', () => {
  function installWorkerApi() {
    const unregister = vi.fn().mockResolvedValue(true);
    const registration = {
      scope: 'http://localhost/staff/',
      active: { scriptURL: 'http://localhost/staff/sw.js', postMessage: vi.fn() },
      unregister,
    };
    Object.defineProperty(navigator, 'serviceWorker', {
      configurable: true,
      value: { register: vi.fn(), getRegistrations: vi.fn().mockResolvedValue([registration]) },
    });
    return unregister;
  }

  it('sign-out unregisters the worker, not just its caches', async () => {
    const unregister = installWorkerApi();
    await wipeOfflineSnapshots('sign-out');
    expect(unregister).toHaveBeenCalledTimes(1);
  });

  it('other wipes only purge', async () => {
    const unregister = installWorkerApi();
    await wipeOfflineSnapshots('cleared-by-user');
    expect(unregister).not.toHaveBeenCalled();
  });

  it('the root-layout cleanup removes a stale worker on load in development and test builds', async () => {
    const unregister = installWorkerApi();
    render(<ServiceWorkerCleanup />);
    await act(async () => {});
    expect(unregister).toHaveBeenCalledTimes(1);
  });
});

describe('ConnectivityProvider gives back what it changed', () => {
  it('restores the mutation defaults and the online manager on unmount', async () => {
    const client = new QueryClient({ defaultOptions: { mutations: { retry: 2 } } });
    const view = render(
      <QueryClientProvider client={client}>
        <ConnectivityProvider>
          <p>staff</p>
        </ConnectivityProvider>
      </QueryClientProvider>,
    );
    await act(async () => {});
    expect(client.getDefaultOptions().mutations?.networkMode).toBe('always');
    onlineManager.setOnline(false);
    view.unmount();
    expect(client.getDefaultOptions().mutations?.networkMode).toBeUndefined();
    expect(client.getDefaultOptions().mutations?.retry).toBe(2);
    expect(onlineManager.isOnline()).toBe(true);
  });
});

describe('a caller\'s own abort says nothing about the server', () => {
  it('is not reported as unreachable', async () => {
    const report = vi.fn();
    setConnectivityReporter({ isOffline: () => false, report });
    server.use(http.get('*/api/slow', async () => {
      await new Promise((resolve) => setTimeout(resolve, 200));
      return HttpResponse.json({});
    }));
    const controller = new AbortController();
    const pending = apiFetch('/api/slow', { signal: controller.signal });
    controller.abort();
    await expect(pending).rejects.toThrow();
    expect(report).not.toHaveBeenCalled();
  });

  it('but a real network failure still is', async () => {
    const report = vi.fn();
    setConnectivityReporter({ isOffline: () => false, report });
    server.use(http.get('*/api/down', () => HttpResponse.error()));
    await expect(apiFetch('/api/down')).rejects.toThrow();
    expect(report).toHaveBeenCalledWith('unreachable');
  });
});
