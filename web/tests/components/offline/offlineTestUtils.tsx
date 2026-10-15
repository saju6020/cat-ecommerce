import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import type { ReactNode } from 'react';
import { StaffLockProvider } from '@/components/staff/IdleLockContext';
import { setOfflineBackendForTests, type OfflineBackend, type OfflineStoreName } from '@/lib/offline/db';
import { buildSnapshot, xctFixtureIds } from '@/mocks/handlers/operations/fixtures';
import type { SnapshotRecord } from '@/lib/offline/snapshot';
import { DEFAULT_DEVICE_SETTINGS, type DeviceSettings } from '@/lib/offline/device';
import type { SnapshotScenario } from '@/mocks/handlers/operations/state';

/** An in-memory stand-in for IndexedDB (jsdom has none; `fake-indexeddb` is not permitted, §10). */
export function installMemoryBackend() {
  const stores: Record<OfflineStoreName, Map<string, unknown>> = {
    snapshot: new Map(),
    device: new Map(),
  };
  const backend: OfflineBackend = {
    get: async (store, key) => stores[store].get(key),
    getAll: async (store) => [...stores[store].values()],
    put: async (store, key, value) => {
      stores[store].set(key, structuredClone(value));
    },
    clear: async (store) => {
      stores[store].clear();
    },
    update: async (store, key, change) => {
      const next = change(stores[store].get(key));
      if (next === undefined) return false;
      stores[store].set(key, structuredClone(next));
      return true;
    },
  };
  setOfflineBackendForTests(backend);
  return stores;
}

export function removeMemoryBackend() {
  setOfflineBackendForTests(undefined);
}

export function renderWithClient(ui: ReactNode, client?: QueryClient) {
  const queryClient =
    client ?? new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return {
    queryClient,
    ...render(
      <QueryClientProvider client={queryClient}>
        <StaffLockProvider>{ui}</StaffLockProvider>
      </QueryClientProvider>,
    ),
  };
}

export function recordFor(
  scenario: SnapshotScenario = 'both',
  overrides: Partial<SnapshotRecord> = {},
): SnapshotRecord {
  const body = buildSnapshot(scenario, xctFixtureIds.branchId, 1);
  return {
    schemaVersion: 1,
    branchId: body.branch.id,
    body,
    etag: '"etag-1"',
    fetchedAt: new Date().toISOString(),
    staffAccountId: body.staffAccountId,
    ...overrides,
  };
}

/** The device record as the staff app leaves it: opted in, used just now, not locked. */
export function seedDevice(
  stores: ReturnType<typeof installMemoryBackend>,
  overrides: Partial<DeviceSettings> = {},
) {
  stores.device.set('settings', {
    ...DEFAULT_DEVICE_SETTINGS,
    offlineEnabled: true,
    enabledAt: new Date().toISOString(),
    enabledByStaffAccountId: xctFixtureIds.staffAccountId,
    lastActivityAt: new Date().toISOString(),
    ...overrides,
  });
}
