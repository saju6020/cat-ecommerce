/**
 * HO-FIL-002 §12 — a manually-released gate for the "slow upload" fixture, so a test can hold an
 * upload in flight (to assert the `uploading` state, or to abort mid-upload, C5) deterministically
 * — never a real `sleep` (task rule: "use promises you release manually"). Named so more than one
 * in-flight upload can be controlled independently if a future test needs it.
 *
 * Used from Vitest specs (same Node process as `msw/node`'s handlers). The dev-only harness page
 * that once exposed it to Playwright was deleted when the cat photo page became a real consumer
 * of the uploader (owner decision 2026-09-28; HO-CAT-002 follow-up 2026-10-09).
 */
const gates = new Map<string, { promise: Promise<void>; release: () => void }>();

export function armFileUploadGate(name: string): void {
  let release!: () => void;
  const promise = new Promise<void>((resolve) => {
    release = resolve;
  });
  gates.set(name, { promise, release });
}

export function releaseFileUploadGate(name: string): void {
  gates.get(name)?.release();
  gates.delete(name);
}

export async function waitForFileUploadGate(name: string): Promise<void> {
  const gate = gates.get(name);
  if (gate) await gate.promise;
}
