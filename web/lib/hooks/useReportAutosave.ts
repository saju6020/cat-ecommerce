'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { ApiConflictError } from '@/lib/api/client';
import { describeSurgeryError, surgeryCode } from '@/lib/api/errors';
import { saveReportDraftRequest } from './useSurgicalReport';
import { useBranchScope } from './useBranchScope';
import type { SaveReportDraftRequest, SurgicalReport } from '@/lib/api/surgical-reports';

export type AutosaveStatus = 'idle' | 'saving' | 'saved' | 'error' | 'conflict' | 'finalised';

export const REPORT_AUTOSAVE_MS = 5_000;

/**
 * Saves that have been SENT (or queued behind one) and have not answered yet, by surgery. The editor's
 * unmount flush is fire-and-forget, so a tab switched away and straight back can mount a new editor
 * while the flush is still on the wire; the Report tab waits for the entry here before it reads the
 * report, so it never shows the text from before the flush.
 */
const inFlightSaves = new Map<string, Promise<unknown>>();

function track(surgeryId: string, saved: Promise<unknown>): void {
  const entry = saved.catch(() => undefined);
  inFlightSaves.set(surgeryId, entry);
  void entry.then(() => {
    if (inFlightSaves.get(surgeryId) === entry) inFlightSaves.delete(surgeryId);
  });
}

/** The save still in flight for this surgery, if any (resolves when it has answered, whatever the answer). */
export function pendingReportSave(surgeryId: string): Promise<unknown> | null {
  return inFlightSaves.get(surgeryId) ?? null;
}

/**
 * `lib/hooks/useReportAutosave.ts` — HO-SRG-002 §8.3, ADR-FE-074. The surgical report draft saves itself
 * 5 s after the last change, always with the `expectedVersion` of the last save it knows (none on the very
 * first save — that creates the draft). Saves are serialised, so two never race each other. A `409` stops
 * the autosave and reports `conflict` (someone else changed the draft) or `finalised` (it was finalised
 * meanwhile); the caller keeps the local text. The hook never reads or resets the form — a poll or a
 * refetch cannot touch what is being typed.
 */
export function useReportAutosave(
  surgeryId: string,
  initialVersion: number | undefined,
  delayMs = REPORT_AUTOSAVE_MS,
) {
  const queryClient = useQueryClient();
  const { branchId } = useBranchScope();
  const version = useRef<number | undefined>(initialVersion);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const build = useRef<(() => SaveReportDraftRequest) | null>(null);
  const chain = useRef<Promise<boolean>>(Promise.resolve(true));
  const stopped = useRef(false);
  const [pending, setPending] = useState(false);
  const [state, setState] = useState<{
    status: AutosaveStatus;
    savedAt: string | null;
    message: string | null;
  }>({ status: 'idle', savedAt: null, message: null });

  const run = useCallback(async (): Promise<boolean> => {
    if (stopped.current) return false;
    if (!build.current) return true; // nothing has changed: nothing to save
    const body = { ...build.current(), expectedVersion: version.current };
    setState((s) => ({ ...s, status: 'saving', message: null }));
    try {
      const result = await saveReportDraftRequest(surgeryId, body);
      version.current = result.version;
      // Keep the report query in step with what was just saved. The editor is unmounted by a tab switch and
      // the query is never refetched on its own (staleTime Infinity): without this, coming back within the
      // cache time would show the OLD text and start from the OLD version, so the next keystroke would be a 409.
      const key = ['surgeries', surgeryId, 'report', branchId];
      const cached = queryClient.getQueryData<SurgicalReport | null>(key);
      if (cached) {
        queryClient.setQueryData<SurgicalReport>(key, {
          ...cached,
          procedurePerformed: body.procedurePerformed,
          findings: body.findings,
          technique: body.technique,
          sutureMaterial: body.sutureMaterial,
          hadComplications: body.hadComplications,
          complications: body.hadComplications ? (body.complications ?? '') : null,
          surgeonId: body.surgeonId,
          assistantId: body.assistantId,
          version: result.version,
        });
      } else {
        // The first save created the draft: there is nothing to patch, so mark the (empty) cache stale
        // without refetching under the open editor; the next mount reads the draft.
        void queryClient.invalidateQueries({ queryKey: key, refetchType: 'none' });
      }
      setPending(timer.current !== null);
      setState({ status: 'saved', savedAt: new Date().toISOString(), message: null });
      return true;
    } catch (error) {
      if (error instanceof ApiConflictError) {
        stopped.current = true;
        setState((s) => ({
          ...s,
          status: surgeryCode(error, 'ReportNotEditable', 'ReportAlreadyFinalised')
            ? 'finalised'
            : 'conflict',
          message: describeSurgeryError(error),
        }));
      } else {
        setState((s) => ({ ...s, status: 'error', message: describeSurgeryError(error) }));
      }
      return false;
    }
  }, [surgeryId, branchId, queryClient]);

  const saveNow = useCallback((): Promise<boolean> => {
    if (timer.current) clearTimeout(timer.current);
    timer.current = null;
    chain.current = chain.current.then(run);
    track(surgeryId, chain.current);
    return chain.current;
  }, [run, surgeryId]);

  /** Call on every change with a function that reads the form's CURRENT values. */
  const schedule = useCallback(
    (read: () => SaveReportDraftRequest) => {
      if (stopped.current) return;
      build.current = read;
      setPending(true);
      if (timer.current) clearTimeout(timer.current);
      timer.current = setTimeout(() => {
        timer.current = null;
        void saveNow();
      }, delayMs);
    },
    [delayMs, saveNow],
  );

  // Leaving the editor (a tab switch unmounts it) must not lose what is still waiting for its 5 s: flush it.
  useEffect(
    () => () => {
      if (!timer.current) return;
      clearTimeout(timer.current);
      timer.current = null;
      chain.current = chain.current.then(run);
      // Registered NOW, not when the save starts: a remount in the same tick must find it.
      track(surgeryId, chain.current);
    },
    [run, surgeryId],
  );

  return {
    ...state,
    schedule,
    saveNow,
    /** True while a change has not been saved yet. */
    pending,
    currentVersion: () => version.current,
  };
}
