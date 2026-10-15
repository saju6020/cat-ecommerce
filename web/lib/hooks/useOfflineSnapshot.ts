'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { syncSnapshot, type SyncResult } from '@/lib/offline/sync';
import { DEFAULT_DEVICE_SETTINGS, readDeviceSettings, type DeviceSettings } from '@/lib/offline/device';
import { readLatestSnapshot, type SnapshotRecord } from '@/lib/offline/snapshot';
import { subscribeOfflineStore } from '@/lib/offline/events';
import { ApiForbiddenError, ApiUnauthorizedError } from '@/lib/api/client';
import { SnapshotNotPermittedError } from '@/lib/offline/sync';
import { STAFF_SESSION_QUERY_KEY } from '@/lib/hooks/useSession';
import { getStaffSessionStatus, type StaffSessionStatus } from '@/lib/api/staff-auth';

/**
 * `lib/hooks/useOfflineSnapshot.ts` — HO-XCT-002 §8.4/§11.2. TanStack Query
 * `['offline','snapshot', branchId]`, `refetchInterval` = the payload's `refreshAfterSeconds`,
 * `refetchOnWindowFocus`. Writing to IndexedDB happens inside the query function (TanStack v5 has
 * no `onSuccess`); the offline BOARD never mounts this query (it reads the store once).
 */
export function useOfflineSnapshot(options: {
  enabled: boolean;
  branchId: string | null;
  staffAccountId: string | null;
}) {
  const { enabled, branchId, staffAccountId } = options;
  // The first sync of a page load always fetches; later ones leave a copy another tab just
  // refreshed alone (lib/offline/sync.ts), so N open tabs do not write N audit rows a minute.
  const firstRun = useRef(true);
  return useQuery<SyncResult>({
    queryKey: ['offline', 'snapshot', branchId],
    enabled: enabled && branchId !== null && staffAccountId !== null,
    queryFn: () => {
      const ignoreFreshness = firstRun.current;
      firstRun.current = false;
      return syncSnapshot(branchId as string, staffAccountId as string, { ignoreFreshness });
    },
    refetchInterval: (query) => (query.state.data?.refreshAfterSeconds ?? 60) * 1000,
    refetchOnWindowFocus: true,
    staleTime: 0,
    gcTime: 0,
    retry: (failureCount, error) =>
      !(
        error instanceof ApiUnauthorizedError ||
        error instanceof ApiForbiddenError ||
        error instanceof SnapshotNotPermittedError
      ) && failureCount < 1,
  });
}

/** The device opt-in, read from IndexedDB and re-read whenever the store changes. `loaded` is
 *  false until the first read, so a screen never flashes "off" for a device that is on. */
export function useDeviceSettings(): { settings: DeviceSettings; loaded: boolean } {
  const [settings, setSettings] = useState<DeviceSettings>(DEFAULT_DEVICE_SETTINGS);
  const [loaded, setLoaded] = useState(false);

  const refresh = useCallback(() => {
    void readDeviceSettings().then((next) => {
      setSettings(next);
      setLoaded(true);
    });
  }, []);

  useEffect(() => {
    refresh();
    return subscribeOfflineStore(refresh);
  }, [refresh]);

  return { settings, loaded };
}

/** The latest stored record (any branch), re-read whenever the store changes. */
export function useStoredSnapshot(): { record: SnapshotRecord | undefined; loaded: boolean } {
  const [record, setRecord] = useState<SnapshotRecord | undefined>(undefined);
  const [loaded, setLoaded] = useState(false);

  const refresh = useCallback(() => {
    void readLatestSnapshot().then((next) => {
      setRecord(next);
      setLoaded(true);
    });
  }, []);

  useEffect(() => {
    refresh();
    return subscribeOfflineStore(refresh);
  }, [refresh]);

  return { record, loaded };
}

/** The signed-in person's id if the staff session is already in the cache. Never fetches: the
 *  offline board must not start a request of its own (HO-XCT-002 §11.4). */
export function useKnownStaffAccountId(): string | null {
  const { data } = useQuery<StaffSessionStatus | null>({
    queryKey: STAFF_SESSION_QUERY_KEY,
    queryFn: getStaffSessionStatus,
    enabled: false,
  });
  return data?.staffAccountId ?? null;
}
