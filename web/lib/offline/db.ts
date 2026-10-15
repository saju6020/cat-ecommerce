/**
 * `lib/offline/db.ts` — HO-XCT-002 §8.3/§8.5, ADR-FE-141. The ONE sanctioned use of IndexedDB in
 * this app, hand-written and deliberately tiny (open, get, getAll, put, clear). No `idb`, no
 * `dexie`. One database, `purrcare-offline`, version 1, two object stores and nothing else:
 *
 *  - `snapshot`, key `branchId`: the offline snapshot record (`lib/offline/snapshot.ts`);
 *  - `device`, fixed key `settings`: the device opt-in flag (`lib/offline/device.ts`), no personal
 *    data.
 *
 * No token, password, CAPTCHA answer or form draft ever goes into either store. When IndexedDB is
 * unavailable (private mode, SSR) every read is empty and every write reports `false`: the feature
 * quietly does nothing rather than falling back to `localStorage`.
 */

export const OFFLINE_DB_NAME = 'purrcare-offline';
export const OFFLINE_DB_VERSION = 1;
export const OFFLINE_STORES = ['snapshot', 'device'] as const;
export type OfflineStoreName = (typeof OFFLINE_STORES)[number];

/** The store contract. The IndexedDB implementation is the only one that ships; tests install an
 *  in-memory one with `setOfflineBackendForTests` (jsdom has no IndexedDB, and a new package such as
 *  `fake-indexeddb` is not permitted, HO-XCT-002 §10). */
export interface OfflineBackend {
  get(store: OfflineStoreName, key: string): Promise<unknown>;
  getAll(store: OfflineStoreName): Promise<unknown[]>;
  put(store: OfflineStoreName, key: string, value: unknown): Promise<void>;
  clear(store: OfflineStoreName): Promise<void>;
  /** Read-modify-write in ONE transaction, so two tabs cannot overwrite each other's change. The
   *  callback returns the new value, or `undefined` to leave the record as it is. */
  update(
    store: OfflineStoreName,
    key: string,
    change: (current: unknown) => unknown,
  ): Promise<boolean>;
}

let backendOverride: OfflineBackend | null | undefined;

export function setOfflineBackendForTests(backend: OfflineBackend | null | undefined): void {
  backendOverride = backend;
  databasePromise = null;
}

let databasePromise: Promise<IDBDatabase> | null = null;

function openDatabase(): Promise<IDBDatabase> {
  if (databasePromise) return databasePromise;
  databasePromise = new Promise<IDBDatabase>((resolve, reject) => {
    const request = indexedDB.open(OFFLINE_DB_NAME, OFFLINE_DB_VERSION);
    request.onupgradeneeded = () => {
      const db = request.result;
      for (const name of OFFLINE_STORES) {
        if (!db.objectStoreNames.contains(name)) db.createObjectStore(name);
      }
    };
    request.onsuccess = () => {
      const db = request.result;
      // A connection the browser closes, or one that must make way for an upgrade, is dropped so the
      // next call opens a fresh one instead of silently reading nothing until a reload.
      db.onclose = () => {
        databasePromise = null;
      };
      db.onversionchange = () => {
        db.close();
        databasePromise = null;
      };
      resolve(db);
    };
    request.onerror = () => reject(request.error ?? new Error('IndexedDB open failed'));
    request.onblocked = () => reject(new Error('IndexedDB open blocked'));
  });
  databasePromise.catch(() => {
    databasePromise = null;
  });
  return databasePromise;
}

function requestToPromise<T>(request: IDBRequest<T>): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error ?? new Error('IndexedDB request failed'));
  });
}

async function transact<T>(
  store: OfflineStoreName,
  mode: IDBTransactionMode,
  run: (objectStore: IDBObjectStore) => IDBRequest<T>,
): Promise<T> {
  const db = await openDatabase();
  const transaction = db.transaction(store, mode);
  const result = await requestToPromise(run(transaction.objectStore(store)));
  if (mode === 'readwrite') {
    await new Promise<void>((resolve, reject) => {
      transaction.oncomplete = () => resolve();
      transaction.onerror = () => reject(transaction.error ?? new Error('IndexedDB write failed'));
      transaction.onabort = () => reject(transaction.error ?? new Error('IndexedDB write aborted'));
    });
  }
  return result;
}

function updateInTransaction(
  store: OfflineStoreName,
  key: string,
  change: (current: unknown) => unknown,
): Promise<boolean> {
  return openDatabase().then(
    (db) =>
      new Promise<boolean>((resolve, reject) => {
        const transaction = db.transaction(store, 'readwrite');
        const objectStore = transaction.objectStore(store);
        let wrote = false;
        const read = objectStore.get(key);
        read.onsuccess = () => {
          const next = change(read.result);
          if (next !== undefined) {
            objectStore.put(next, key);
            wrote = true;
          }
        };
        transaction.oncomplete = () => resolve(wrote);
        transaction.onerror = () => reject(transaction.error ?? new Error('IndexedDB update failed'));
        transaction.onabort = () => reject(transaction.error ?? new Error('IndexedDB update aborted'));
      }),
  );
}

const indexedDbBackend: OfflineBackend = {
  update: (store, key, change) => updateInTransaction(store, key, change),
  get: (store, key) => transact(store, 'readonly', (s) => s.get(key)),
  getAll: (store) => transact(store, 'readonly', (s) => s.getAll()),
  put: async (store, key, value) => {
    await transact(store, 'readwrite', (s) => s.put(value, key));
  },
  clear: async (store) => {
    await transact(store, 'readwrite', (s) => s.clear());
  },
};

function activeBackend(): OfflineBackend | null {
  if (backendOverride !== undefined) return backendOverride;
  return typeof indexedDB === 'undefined' ? null : indexedDbBackend;
}

export function isOfflineStoreAvailable(): boolean {
  return activeBackend() !== null;
}

export async function dbGet<T>(store: OfflineStoreName, key: string): Promise<T | undefined> {
  try {
    return (await activeBackend()?.get(store, key)) as T | undefined;
  } catch {
    return undefined;
  }
}

export async function dbGetAll<T>(store: OfflineStoreName): Promise<T[]> {
  try {
    return ((await activeBackend()?.getAll(store)) ?? []) as T[];
  } catch {
    return [];
  }
}

/** Resolves `true` only when the value was really stored. */
export async function dbPut(store: OfflineStoreName, key: string, value: unknown): Promise<boolean> {
  const backend = activeBackend();
  if (!backend) return false;
  try {
    await backend.put(store, key, value);
    return true;
  } catch {
    return false;
  }
}

/** Atomic read-modify-write; resolves `true` only when something was written. */
export async function dbUpdate<T>(
  store: OfflineStoreName,
  key: string,
  change: (current: T | undefined) => T | undefined,
): Promise<boolean> {
  const backend = activeBackend();
  if (!backend) return false;
  try {
    return await backend.update(store, key, (current) => change(current as T | undefined));
  } catch {
    return false;
  }
}

export async function dbClear(store: OfflineStoreName): Promise<void> {
  try {
    await activeBackend()?.clear(store);
  } catch {
    // A store that cannot be cleared is also one that cannot be read; nothing more to do.
  }
}
