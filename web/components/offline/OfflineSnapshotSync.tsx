'use client';

import { useEffect, useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { toast } from '@/components/ui/use-toast';
import { useActiveBranchId } from '@/lib/hooks/useActiveBranch';
import { useStaffSession, STAFF_SESSION_QUERY_KEY } from '@/lib/hooks/useSession';
import {
  useDeviceSettings,
  useOfflineSnapshot,
  useStoredSnapshot,
} from '@/lib/hooks/useOfflineSnapshot';
import { useStaffLockState } from '@/components/staff/IdleLockContext';
import { lockOfflineBoard } from '@/lib/offline/device';
import { startActivityRecording } from '@/lib/offline/activity';
import { dropUnusableSnapshots, wipeOfflineSnapshots } from '@/lib/offline/snapshot';
import { removeOtherPeoplesSnapshots, SnapshotNotPermittedError } from '@/lib/offline/sync';

/**
 * `components/offline/OfflineSnapshotSync.tsx` — HO-XCT-002 §11.2, §8.5 (no UI). Mounted in the
 * `(staff)` layout. Syncs the snapshot only when the device flag is on, a staff session exists and
 * the active branch is known, and owns every wipe trigger that is observable from here:
 *
 *  - explicit sign-out: `SignOutButton` clears the query cache, which removes the staff session
 *    query — seen below as its `removed` event (this layout is still mounted at that moment);
 *  - the session turning out to be gone (`null`, or a `401 session_invalid` surfacing as a null
 *    read): the store is cleared. A `session_idle_timeout` is NOT a sign-out and clears nothing;
 *  - a person change: another person's records are removed before anything is fetched or written;
 *  - `validUntil` passing: expired records are dropped on mount, on every store change, and at the
 *    moment the current copy expires;
 *  - a `403` on the snapshot: the person may not hold one, so the copy and the flag are cleared and
 *    a toast says so.
 */
export function OfflineSnapshotSync() {
  const queryClient = useQueryClient();
  const session = useStaffSession();
  const branchId = useActiveBranchId();
  const { settings, loaded } = useDeviceSettings();
  const { record } = useStoredSnapshot();
  const staffAccountId = session.data?.staffAccountId ?? null;

  const snapshot = useOfflineSnapshot({
    enabled: loaded && settings.offlineEnabled,
    branchId,
    staffAccountId,
  });

  // Using the staff app is activity on the device (about once a minute). The offline board reads it
  // on a cold load to decide whether the device has been idle too long to show anything.
  // NOT while the HO-ACC-002 idle lock is showing: someone moving the mouse over the lock dialog is
  // not the signed-in person using the app, and must not mark the device active. The lock context is
  // only read here.
  const { locked } = useStaffLockState();
  const recording = loaded && settings.offlineEnabled && staffAccountId !== null && !locked;
  useEffect(() => {
    if (!recording) return;
    return startActivityRecording();
  }, [recording]);

  // The online idle lock fired: the offline board must open locked too, until a sync after
  // re-authentication lifts it.
  useEffect(() => {
    if (locked && staffAccountId && settings.offlineEnabled) void lockOfflineBoard(staffAccountId);
  }, [locked, staffAccountId, settings.offlineEnabled]);

  // Sign-out: the staff session query is removed from the cache.
  useEffect(() => {
    return queryClient.getQueryCache().subscribe((event) => {
      if (event.type === 'removed' && event.query.queryKey[0] === STAFF_SESSION_QUERY_KEY[0] &&
        event.query.queryKey[1] === STAFF_SESSION_QUERY_KEY[1]) {
        void wipeOfflineSnapshots('sign-out');
      }
    });
  }, [queryClient]);

  // The session is gone.
  useEffect(() => {
    if (session.isSuccess && session.data === null) void wipeOfflineSnapshots('session-invalid');
  }, [session.isSuccess, session.data]);

  // A person change: wipe before any new write, whether or not the flag is on.
  useEffect(() => {
    if (staffAccountId) void removeOtherPeoplesSnapshots(staffAccountId);
  }, [staffAccountId]);

  // Expiry: now, and at the moment the current copy expires.
  const validUntil = record?.body.validUntil;
  useEffect(() => {
    void dropUnusableSnapshots();
    if (!validUntil) return;
    const timeout = setTimeout(
      () => void dropUnusableSnapshots(),
      Math.max(0, Date.parse(validUntil) - Date.now()) + 1_000,
    );
    return () => clearTimeout(timeout);
  }, [validUntil]);

  // 403: this person may not hold a snapshot.
  const notified = useRef(false);
  useEffect(() => {
    if (snapshot.error instanceof SnapshotNotPermittedError && !notified.current) {
      notified.current = true;
      toast({
        title: 'Offline copy switched off',
        description:
          "Your role can't keep an offline copy of today's lists, so it was switched off and cleared on this device.",
      });
    }
  }, [snapshot.error]);

  return null;
}
