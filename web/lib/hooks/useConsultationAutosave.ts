import { useCallback, useEffect, useRef, useState } from 'react';
import { saveDraft, type DraftRequest } from '@/lib/api/visits';

/**
 * `useConsultationAutosave` — HO-EMR-002 §8.3/§11.1, ADR-FE-050. Auto-saves the full form snapshot
 * to the SERVER's per-author working copy (`PUT …/draft`):
 *  - 3 s after typing stops (`AUTOSAVE_DEBOUNCE_MS`);
 *  - at most every 20 s while typing continuously (`AUTOSAVE_MAX_WAIT_MS`);
 *  - on `visibilitychange` (hidden), `pagehide` and unmount (route change) with
 *    `fetch(…, { keepalive: true })`, so the last words survive a closing tab.
 * It skips a send when the snapshot equals the last one the server accepted. **Nothing is written to
 * browser storage** — not here, not anywhere in this feature (ADR-FE-050, U-A9).
 *
 * Failure and the idle lock: a failed save (including the `401` the idle lock reports) sets status
 * `failed` and is **never replayed** — HO-ACC-002 ADR-FE-010's rule. The next edit after the person
 * signs in again changes the snapshot, which schedules a fresh save. Typed text is never touched by
 * this hook (it only reads the snapshot).
 */

export const AUTOSAVE_DEBOUNCE_MS = 3_000;
export const AUTOSAVE_MAX_WAIT_MS = 20_000;

export type AutosaveStatus = 'idle' | 'saving' | 'saved' | 'failed';

export interface AutosaveState {
  status: AutosaveStatus;
  savedAt: Date | null;
}

export function draftKey(snapshot: DraftRequest): string {
  return JSON.stringify([
    snapshot.subjective,
    snapshot.objective,
    snapshot.assessment,
    snapshot.plan,
    snapshot.dischargeInstructions,
    snapshot.problemCodeIds,
  ]);
}

export interface UseConsultationAutosaveOptions {
  visitId: string;
  /** The form's current values — change this and a save is scheduled. */
  snapshot: DraftRequest;
  /** What the server already holds (restored draft, else saved note, else template). */
  initialSnapshot: DraftRequest;
  /** False for a role that cannot edit or a finalised visit — nothing is ever sent. */
  enabled: boolean;
}

export function useConsultationAutosave({
  visitId,
  snapshot,
  initialSnapshot,
  enabled,
}: UseConsultationAutosaveOptions) {
  const [state, setState] = useState<AutosaveState>({ status: 'idle', savedAt: null });

  const latestRef = useRef(snapshot);
  const enabledRef = useRef(enabled);
  const lastSentKeyRef = useRef(draftKey(initialSnapshot));
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const maxWaitRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const inFlightRef = useRef<{ promise: Promise<void>; key: string } | null>(null);
  /** The payload whose save FAILED. It is never re-sent by an automatic path (unmount, pagehide,
   *  visibilitychange, timers): only a changed snapshot, or an explicit flush(), sends again. */
  const failedKeyRef = useRef<string | null>(null);
  /** Bumped by an explicit Save note: a draft save that completes after it must not reset the baseline. */
  const generationRef = useRef(0);

  const clearTimers = useCallback(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    if (maxWaitRef.current) clearTimeout(maxWaitRef.current);
    debounceRef.current = null;
    maxWaitRef.current = null;
  }, []);

  /**
   * `automatic`: a timer / lifecycle send. `keepalive`: the page may be going away, so the NEWEST payload is
   * sent at once (never queued behind an in-flight save, which the browser may kill).
   */
  const send = useCallback(
    async (options: { keepalive: boolean; automatic: boolean }): Promise<void> => {
      clearTimers();
      if (!options.keepalive) {
        while (inFlightRef.current) await inFlightRef.current.promise;
      }
      if (!enabledRef.current) return;
      const payload = latestRef.current;
      const key = draftKey(payload);
      if (key === lastSentKeyRef.current) return;
      if (options.automatic && key === failedKeyRef.current) return; // never replay a failure
      if (inFlightRef.current?.key === key) return; // this exact payload is already on its way

      const generation = generationRef.current;
      const run = (async () => {
        setState((previous) => ({ ...previous, status: 'saving' }));
        try {
          await saveDraft(visitId, payload, { keepalive: options.keepalive });
          if (generation === generationRef.current) lastSentKeyRef.current = key;
          failedKeyRef.current = null;
          setState({ status: 'saved', savedAt: new Date() });
        } catch {
          failedKeyRef.current = key;
          setState((previous) => ({ ...previous, status: 'failed' }));
        }
      })();
      const entry = { promise: run, key };
      inFlightRef.current = entry;
      await run;
      if (inFlightRef.current === entry) inFlightRef.current = null;
    },
    [clearTimers, visitId],
  );

  useEffect(() => {
    latestRef.current = snapshot;
    enabledRef.current = enabled;
  });

  const key = draftKey(snapshot);
  useEffect(() => {
    if (!enabled) {
      clearTimers();
      return;
    }
    if (key === lastSentKeyRef.current) {
      clearTimers();
      return;
    }
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(
      () => void send({ keepalive: false, automatic: true }),
      AUTOSAVE_DEBOUNCE_MS,
    );
    if (!maxWaitRef.current) {
      maxWaitRef.current = setTimeout(
        () => void send({ keepalive: false, automatic: true }),
        AUTOSAVE_MAX_WAIT_MS,
      );
    }
  }, [key, enabled, send, clearTimers]);

  useEffect(() => {
    function onVisibility() {
      if (document.visibilityState === 'hidden') void send({ keepalive: true, automatic: true });
    }
    function onPageHide() {
      void send({ keepalive: true, automatic: true });
    }
    document.addEventListener('visibilitychange', onVisibility);
    window.addEventListener('pagehide', onPageHide);
    return () => {
      document.removeEventListener('visibilitychange', onVisibility);
      window.removeEventListener('pagehide', onPageHide);
      // Route change / unmount: one last keepalive save of anything the server does not have yet.
      void send({ keepalive: true, automatic: true });
    };
  }, [send]);

  /** Sends now (before finalise) and resolves when the attempt has finished. User-initiated, so a
   *  previously failed payload may be tried again. */
  const flush = useCallback(() => send({ keepalive: false, automatic: false }), [send]);

  /** Resolves once any draft save already on its way has finished (so a note save cannot be followed
   *  by a LATER-landing older draft). */
  const settle = useCallback(async () => {
    clearTimers();
    while (inFlightRef.current) await inFlightRef.current.promise;
  }, [clearTimers]);

  /** After an explicit Save note: the record holds this text, so no draft is owed for it. */
  const markSaved = useCallback(
    (saved: DraftRequest) => {
      clearTimers();
      generationRef.current += 1;
      lastSentKeyRef.current = draftKey(saved);
      failedKeyRef.current = null;
      setState((previous) => ({ ...previous, status: previous.savedAt ? 'saved' : 'idle' }));
    },
    [clearTimers],
  );

  return { status: state.status, savedAt: state.savedAt, flush, settle, markSaved };
}
