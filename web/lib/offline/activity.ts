import { recordDeviceActivity } from './device';

/**
 * `lib/offline/activity.ts` — HO-XCT-002 §8.5, ADR-FE-144. Records "someone is using this device" in
 * the device store, at most once per `minIntervalMs` (about a minute), so the offline board can tell
 * on a cold load whether the device has been idle too long. Returns the listener to attach to the
 * activity events and a stop function.
 */
const ACTIVITY_EVENTS = ['keydown', 'pointerdown', 'pointermove', 'touchstart', 'wheel'] as const;
export const ACTIVITY_WRITE_INTERVAL_MS = 60_000;

export function startActivityRecording(
  minIntervalMs: number = ACTIVITY_WRITE_INTERVAL_MS,
  now: () => number = Date.now,
): () => void {
  let lastWrite = 0;
  const write = () => {
    const at = now();
    if (at - lastWrite < minIntervalMs) return;
    lastWrite = at;
    void recordDeviceActivity(at);
  };
  write(); // being mounted at all is activity
  for (const name of ACTIVITY_EVENTS) window.addEventListener(name, write, { passive: true });
  return () => {
    for (const name of ACTIVITY_EVENTS) window.removeEventListener(name, write);
  };
}
