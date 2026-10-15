import { SNAPSHOT_SCHEMA_VERSION, type OfflineSnapshotBody } from '@/lib/api/offline';
import { dbClear, dbGet, dbGetAll, dbPut, dbUpdate } from './db';
import { DEFAULT_DEVICE_SETTINGS, readDeviceSettings, writeDeviceSettings } from './device';
import { notifyOfflineStoreChanged } from './events';
import { purgeOfflineServiceWorker, unregisterOfflineServiceWorker } from './sw-register';

/**
 * `lib/offline/snapshot.ts` — HO-XCT-002 §8.5, ADR-FE-141. The snapshot record: the HO-XCT-001
 * payload verbatim, plus `etag`, `fetchedAt` and the `staffAccountId` it came with. One record per
 * branch (key `branchId`), written only while the device flag is on, bound to one person, and
 * wiped on: explicit sign-out, any `401 session_invalid`, a different `staffAccountId`,
 * `validUntil` passing, the opt-in being turned off, "Clear this device", and a schema-version
 * mismatch. Never written from a portal or storefront route (only the `(staff)` layout syncs).
 */
export interface SnapshotRecord {
  /** The payload's `schemaVersion` at write time. */
  schemaVersion: number;
  branchId: string;
  body: OfflineSnapshotBody;
  etag: string | null;
  fetchedAt: string;
  staffAccountId: string;
}

export type WipeReason =
  | 'sign-out'
  | 'session-invalid'
  | 'person-change'
  | 'expired'
  | 'opt-out'
  | 'cleared-by-user'
  | 'schema-mismatch'
  | 'not-permitted';

/**
 * Owner decision 2026-10-14: customers' phone numbers are NOT kept on the device. The API still
 * sends `customerPhone` (HO-XCT-001 section 12.4, backlog row for the backend), so it is dropped here,
 * before anything is written to IndexedDB.
 */
export function withoutCustomerPhones(body: OfflineSnapshotBody): OfflineSnapshotBody {
  const appointments = body.sections.appointments;
  if (!appointments) return body;
  return {
    ...body,
    sections: {
      ...body.sections,
      appointments: {
        items: appointments.items.map((item) => {
          const { customerPhone, ...rest } = item as typeof item & { customerPhone?: unknown };
          void customerPhone;
          return rest;
        }),
      },
    },
  };
}

export function isSnapshotUsable(record: SnapshotRecord, now: number = Date.now()): boolean {
  return (
    record.schemaVersion === SNAPSHOT_SCHEMA_VERSION &&
    record.body.schemaVersion === SNAPSHOT_SCHEMA_VERSION &&
    Date.parse(record.body.validUntil) > now
  );
}

/** A record written before phone numbers were dropped (owner decision 2026-10-14) is cleaned when it
 *  is read, in memory and, once, in the store. */
function cleaned(record: SnapshotRecord | undefined): SnapshotRecord | undefined {
  if (!record || !JSON.stringify(record.body).includes('customerPhone')) return record;
  const clean = { ...record, body: withoutCustomerPhones(record.body) };
  void dbUpdate<SnapshotRecord>('snapshot', record.branchId, (current) =>
    current && JSON.stringify(current.body).includes('customerPhone') ? { ...current, body: clean.body } : undefined,
  );
  return clean;
}

export async function readSnapshot(branchId: string): Promise<SnapshotRecord | undefined> {
  return cleaned(await dbGet<SnapshotRecord>('snapshot', branchId));
}

/** The most recently saved record across branches (the board shows "the last active branch"). */
export async function readLatestSnapshot(): Promise<SnapshotRecord | undefined> {
  const all = await dbGetAll<SnapshotRecord>('snapshot');
  return cleaned(all.sort((a, b) => Date.parse(b.fetchedAt) - Date.parse(a.fetchedAt))[0]);
}

/**
 * Wipes the snapshot store completely and tells the service worker to drop its caches. The
 * worker stores no personal data (its caches hold hashed static assets and the offline shell), so
 * the purge is belt and braces; the wipe itself is what removes personal data.
 */
export async function wipeOfflineSnapshots(reason: WipeReason): Promise<void> {
  void reason; // Kept for the call sites and for tests; nothing about why is persisted.
  await dbClear('snapshot');
  notifyOfflineStoreChanged();
  // Sign-out removes the worker itself (it registers again at the next opted-in sign-in); every
  // other wipe only empties its caches.
  if (reason === 'sign-out') await unregisterOfflineServiceWorker();
  else await purgeOfflineServiceWorker();
}

/**
 * Writes a freshly fetched snapshot. Refused (resolves `false`) when the device flag is off, or the
 * body is of an unknown schema version. Another person's records are removed first, so one device
 * never holds two people's lists (E-A9).
 */
export async function writeSnapshot(
  incoming: OfflineSnapshotBody,
  etag: string | null,
  now: number = Date.now(),
): Promise<boolean> {
  let body = incoming;
  const device = await readDeviceSettings();
  if (!device.offlineEnabled) return false;
  if (body.schemaVersion !== SNAPSHOT_SCHEMA_VERSION) return false;

  body = withoutCustomerPhones(body);
  const existing = await dbGetAll<SnapshotRecord>('snapshot');
  if (existing.some((record) => record.staffAccountId !== body.staffAccountId)) {
    await wipeOfflineSnapshots('person-change');
  }

  const stored = await dbPut('snapshot', body.branch.id, {
    schemaVersion: body.schemaVersion,
    branchId: body.branch.id,
    body,
    etag,
    fetchedAt: new Date(now).toISOString(),
    staffAccountId: body.staffAccountId,
  } satisfies SnapshotRecord);
  notifyOfflineStoreChanged();
  return stored;
}

/** A `304`: the content is unchanged, only the time it was last confirmed moves. The change is made
 *  in one transaction and only to the record whose ETag the 304 confirmed, so another tab's newer
 *  body is never overwritten with an older one. */
export async function touchSnapshot(
  branchId: string,
  confirmedEtag: string | null,
  now: number = Date.now(),
): Promise<void> {
  const wrote = await dbUpdate<SnapshotRecord>('snapshot', branchId, (current) => {
    if (!current || current.etag !== confirmedEtag) return undefined;
    return { ...current, fetchedAt: new Date(now).toISOString() };
  });
  if (wrote) notifyOfflineStoreChanged();
}

/** Removes any record that is expired or of another schema version (run when the store is read). */
export async function dropUnusableSnapshots(now: number = Date.now()): Promise<void> {
  const all = await dbGetAll<SnapshotRecord>('snapshot');
  if (all.length > 0 && all.some((record) => !isSnapshotUsable(record, now))) {
    await wipeOfflineSnapshots(
      all.some((record) => record.schemaVersion !== SNAPSHOT_SCHEMA_VERSION)
        ? 'schema-mismatch'
        : 'expired',
    );
  }
}

/** The opt-in. Turning it off wipes the copy and the service worker's caches at once. */
export async function enableOfflineCopy(staffAccountId: string): Promise<boolean> {
  return writeDeviceSettings({
    ...DEFAULT_DEVICE_SETTINGS,
    offlineEnabled: true,
    enabledAt: new Date().toISOString(),
    enabledByStaffAccountId: staffAccountId,
    lastActivityAt: new Date().toISOString(),
  });
}

export async function disableOfflineCopy(reason: WipeReason = 'opt-out'): Promise<void> {
  await writeDeviceSettings(DEFAULT_DEVICE_SETTINGS);
  await wipeOfflineSnapshots(reason);
}
