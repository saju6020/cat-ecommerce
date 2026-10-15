/**
 * `lib/offline/events.ts` — HO-XCT-002 §8.3. A change signal for the offline store, so the banner,
 * the settings page and the board can re-read it after a write or a wipe. Module-level and
 * in-process only: it carries no data, just "something changed, read again".
 */
const listeners = new Set<() => void>();

export function subscribeOfflineStore(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function notifyOfflineStoreChanged(): void {
  for (const listener of [...listeners]) listener();
}
