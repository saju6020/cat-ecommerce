import { ApiForbiddenError, ApiUnauthorizedError } from '@/lib/api/client';
import { getOfflineSnapshot } from '@/lib/api/offline';
import { dbGetAll } from './db';
import { clearOfflineBoardLock } from './device';
import {
  disableOfflineCopy,
  readSnapshot,
  touchSnapshot,
  wipeOfflineSnapshots,
  writeSnapshot,
} from './snapshot';
import type { SnapshotRecord } from './snapshot';
import { warmOfflineShell } from './sw-register';

/**
 * `lib/offline/sync.ts` — HO-XCT-002 §11.2. One snapshot sync, called by `useOfflineSnapshot()`:
 *
 *  - `200` -> write the record (payload, etag, `fetchedAt`, `staffAccountId`);
 *  - `304` -> update `fetchedAt` only;
 *  - `401 session_invalid` -> clear the store; `401 session_idle_timeout` -> do nothing (the idle
 *    lock owns it, HO-ACC-002 §8.4);
 *  - `403` -> this person may not hold a snapshot: clear the store and turn the device flag off
 *    (the caller shows the toast);
 *  - a network failure -> leave the record alone, which is the whole point of it.
 *
 * A successful sync is proof of a live session, so it also lifts the offline board's lock for that
 * same person (ADR-FE-144).
 *
 * Only ONE tab refreshes: a record confirmed by any tab within the last `FRESH_FRACTION` of the
 * refresh period is left alone. With the ETag unreadable cross-origin (section 1 Amended) every
 * refresh is a full 200 and an audit row, so N open tabs would otherwise write N rows a minute.
 *
 * `If-None-Match` is sent only for a record that belongs to the person now signed in: the ETag
 * hashes the content, not the person (HO-XCT-001 section 12.4), so another person's tag would
 * otherwise turn into a `304` that keeps THEIR copy.
 */
export interface SyncResult {
  refreshAfterSeconds: number;
  outcome: 'written' | 'not-modified' | 'skipped' | 'fresh-elsewhere';
}

export class SnapshotNotPermittedError extends Error {
  constructor() {
    super('This person may not keep an offline copy.');
    this.name = 'SnapshotNotPermittedError';
  }
}

const DEFAULT_REFRESH_SECONDS = 60;
/** A copy confirmed less than this fraction of a refresh period ago was refreshed by another tab. */
const FRESH_FRACTION = 0.8;

export async function removeOtherPeoplesSnapshots(staffAccountId: string): Promise<void> {
  const all = await dbGetAll<SnapshotRecord>('snapshot');
  if (all.some((record) => record.staffAccountId !== staffAccountId)) {
    await wipeOfflineSnapshots('person-change');
  }
}

export interface SyncOptions {
  /** Fetch even when another tab just did (the first sync of a page load, and tests). */
  ignoreFreshness?: boolean;
  now?: number;
}

export async function syncSnapshot(
  branchId: string,
  staffAccountId: string,
  options: SyncOptions = {},
): Promise<SyncResult> {
  const now = options.now ?? Date.now();
  await removeOtherPeoplesSnapshots(staffAccountId);

  const stored = await readSnapshot(branchId);
  const refreshAfterSeconds = stored?.body.refreshAfterSeconds ?? DEFAULT_REFRESH_SECONDS;
  if (
    stored &&
    !options.ignoreFreshness &&
    stored.staffAccountId === staffAccountId &&
    now - Date.parse(stored.fetchedAt) < refreshAfterSeconds * 1000 * FRESH_FRACTION
  ) {
    return { refreshAfterSeconds, outcome: 'fresh-elsewhere' };
  }
  const ifNoneMatch = stored && stored.staffAccountId === staffAccountId ? stored.etag : null;

  try {
    const result = await getOfflineSnapshot(ifNoneMatch);
    if (result.kind === 'notModified') {
      await touchSnapshot(branchId, ifNoneMatch, now);
      await clearOfflineBoardLock(now);
      return { refreshAfterSeconds, outcome: 'not-modified' };
    }
    const written = await writeSnapshot(result.body, result.etag, now);
    if (written) {
      await clearOfflineBoardLock(now);
      await warmOfflineShell();
    }
    return {
      refreshAfterSeconds: result.body.refreshAfterSeconds,
      outcome: written ? 'written' : 'skipped',
    };
  } catch (error) {
    if (error instanceof ApiUnauthorizedError) {
      if (error.body.error === 'session_invalid') await wipeOfflineSnapshots('session-invalid');
      // `session_idle_timeout`: do nothing, the idle lock owns it.
    } else if (
      error instanceof ApiForbiddenError &&
      error.body.errors?.[0]?.errorCode === 'ActionNotPermitted'
    ) {
      // Only the action refusal means "this person may not hold a snapshot". A branch refusal
      // (a stale active branch) says nothing about the person and must not switch the copy off.
      await disableOfflineCopy('not-permitted');
      throw new SnapshotNotPermittedError();
    }
    throw error;
  }
}
