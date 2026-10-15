import { dbGet, dbPut, dbUpdate } from './db';
import { notifyOfflineStoreChanged } from './events';

/**
 * `lib/offline/device.ts` — HO-XCT-002 §8.5. The device record: the opt-in flag ("Keep an offline
 * copy of today's lists on this device") plus the two facts the offline board needs to be safe on a
 * cold load, when there is no session to ask (ADR-FE-144):
 *
 *  - `lastActivityAt`: when someone last used the staff app or the board on this device;
 *  - `lockedForStaffAccountId`: set once the board's idle lock has fired. It is cleared only by a
 *    successful snapshot sync for that same person, which needs a live session.
 *
 * It holds no personal data (opaque ids and timestamps). Off by default (OQ-1): a personal laptop
 * should not silently keep a patient list.
 */
export interface DeviceSettings {
  offlineEnabled: boolean;
  enabledAt: string | null;
  enabledByStaffAccountId: string | null;
  lastActivityAt: string | null;
  lockedForStaffAccountId: string | null;
}

const SETTINGS_KEY = 'settings';
const CLOCK_SKEW_MS = 60_000;

export const DEFAULT_DEVICE_SETTINGS: DeviceSettings = {
  offlineEnabled: false,
  enabledAt: null,
  enabledByStaffAccountId: null,
  lastActivityAt: null,
  lockedForStaffAccountId: null,
};

function normalise(stored: Partial<DeviceSettings> | undefined): DeviceSettings {
  if (!stored || typeof stored.offlineEnabled !== 'boolean') return DEFAULT_DEVICE_SETTINGS;
  return {
    offlineEnabled: stored.offlineEnabled,
    enabledAt: stored.enabledAt ?? null,
    enabledByStaffAccountId: stored.enabledByStaffAccountId ?? null,
    lastActivityAt: stored.lastActivityAt ?? null,
    lockedForStaffAccountId: stored.lockedForStaffAccountId ?? null,
  };
}

export async function readDeviceSettings(): Promise<DeviceSettings> {
  return normalise(await dbGet<Partial<DeviceSettings>>('device', SETTINGS_KEY));
}

/** Writes the whole record. Turning the flag OFF is the caller's cue to wipe the snapshot too
 *  (`disableOfflineCopy` in `lib/offline/snapshot.ts` does both). */
export async function writeDeviceSettings(settings: DeviceSettings): Promise<boolean> {
  const stored = await dbPut('device', SETTINGS_KEY, settings);
  notifyOfflineStoreChanged();
  return stored;
}

export async function isOfflineEnabledOnDevice(): Promise<boolean> {
  return (await readDeviceSettings()).offlineEnabled;
}

/** Atomic change to the record while the device is opted in; nothing is written when it is not. */
async function changeWhileEnabled(
  change: (current: DeviceSettings) => DeviceSettings | undefined,
): Promise<boolean> {
  const wrote = await dbUpdate<Partial<DeviceSettings>>('device', SETTINGS_KEY, (stored) => {
    const current = normalise(stored);
    if (!current.offlineEnabled) return undefined;
    return change(current);
  });
  if (wrote) notifyOfflineStoreChanged();
  return wrote;
}

/** Someone used the staff app or the board just now. Callers throttle (about once a minute). */
export function recordDeviceActivity(now: number = Date.now()): Promise<boolean> {
  return changeWhileEnabled((current) => ({ ...current, lastActivityAt: new Date(now).toISOString() }));
}

/** The board's idle lock fired (or it opened already stale): it stays locked across reloads. */
export function lockOfflineBoard(staffAccountId: string): Promise<boolean> {
  return changeWhileEnabled((current) =>
    current.lockedForStaffAccountId === staffAccountId
      ? undefined
      : { ...current, lockedForStaffAccountId: staffAccountId },
  );
}

/** A successful sync by ANYONE lifts the lock: it proves a live session, and the sync has already
 *  replaced another person's copy with the syncing person's own, so B is never stuck on A's lock. */
export function clearOfflineBoardLock(now: number = Date.now()): Promise<boolean> {
  return changeWhileEnabled((current) =>
    current.lockedForStaffAccountId !== null
      ? { ...current, lockedForStaffAccountId: null, lastActivityAt: new Date(now).toISOString() }
      : undefined,
  );
}

/** True when the board must not show data without a sign-in: locked before, or idle for too long
 *  (or never used since the opt-in, which cannot be told apart from a stale device). */
export function isBoardLocked(
  settings: DeviceSettings,
  idleTimeoutMinutes: number,
  now: number = Date.now(),
): boolean {
  if (settings.lockedForStaffAccountId !== null) return true;
  if (!settings.lastActivityAt) return true;
  const last = Date.parse(settings.lastActivityAt);
  // A moment in the future (a wound-back clock) is no activity at all; a minute of skew is allowed.
  if (Number.isNaN(last) || last > now + CLOCK_SKEW_MS) return true;
  return now - last > idleTimeoutMinutes * 60_000;
}

/** Milliseconds of idle time already used up, for the board's own idle timer. */
export function idleElapsedMs(settings: DeviceSettings, now: number = Date.now()): number {
  if (!settings.lastActivityAt) return Infinity;
  const last = Date.parse(settings.lastActivityAt);
  return Number.isNaN(last) || last > now + CLOCK_SKEW_MS ? Infinity : Math.max(0, now - last);
}
