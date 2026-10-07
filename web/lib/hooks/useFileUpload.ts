'use client';

import * as React from 'react';
import {
  uploadFile,
  FileUploadAbortedError,
  FileUploadValidationError,
  FileUploadRateLimitedError,
  type FileUploadAudience,
  type FilePurpose,
  type StoredFileViewModel,
} from '@/lib/api/files';
import { validateFileClientSide } from '@/lib/files/fileRules';
import {
  describeFileError,
  isAutoRemovedFileError,
  isRetryableFileError,
  type FileQueueErrorCode,
} from '@/lib/files/fileErrors';

/**
 * `useFileUpload` — HO-FIL-002 §3/§11/§17 step 2, the hook behind `<FileUploader>`: queue, abort,
 * progress and error mapping.
 *
 * **Location deviation, recorded here and in HO-FIL-002 §1 Amended.** HO-FIL-002 §11 names this
 * file `web/components/files/useFileUpload.ts`. It lives in `lib/hooks/` instead, per this
 * codebase's own established convention (`web/README.md`: "lib/hooks/ ... the only place
 * `useQuery`/`useMutation` may be called" — this hook calls neither, so that ESLint rule itself
 * does not reach it, but the same underlying principle does) generalised to the one other place
 * this app makes a network call outside `lib/api/client.ts` — a hook that owns request state
 * belongs in `lib/hooks/`, not `components/`, regardless of which library's request primitive it
 * happens to call.
 *
 * **Gate-fix 2026-09-28 (ARCHITECTURE MEDIUM).** This relocation on its own made this the only
 * `lib/` → `components/` import in the app (it used to import `@/components/files/fileRules` and
 * `@/components/files/fileErrors`). Fixed by moving those two modules to `lib/files/` instead —
 * see `lib/files/fileRules.ts`'s own doc comment — so every dependency in this slice now points
 * the one direction `web/README.md`'s layering describes.
 *
 * One request in flight at a time, sequential, each independently abortable (ADR-FE-150) —
 * `runningRef` is the single-flight guard. Every file is validated client-side, before it is ever
 * queued to upload (ADR-FE-149) — a file that fails that check is never sent (C2).
 */

export type FileUploadRowStatus = 'queued' | 'uploading' | 'error';

export interface FileUploadQueueItem {
  id: string;
  file: File;
  status: FileUploadRowStatus;
  /** `0`–`1`. Only meaningful while `status === 'uploading'`. Never read into the aria-live
   *  region (ADR-FE-153) — `<FileUploader>`'s `<progress>` shows it visually only. */
  progress: number;
  error?: {
    code: FileQueueErrorCode;
    message: string;
    retryable: boolean;
    /** FIL backlog 18 — set for a `429`: the server's `Retry-After`, and the epoch-ms moment before
     *  which Retry stays disabled. */
    retryAfterSeconds?: number;
    retryAvailableAt?: number;
  };
}

export interface UseFileUploadOptions {
  purpose: FilePurpose;
  audience: FileUploadAudience;
  /** Called once per file that finishes uploading. The HOST keeps the result in its own
   *  controlled state (ADR-FE-156, `<FileList>`'s own `files` prop) — this hook removes a
   *  finished row from its own queue right after calling this, so `<FileUploader>` only ever
   *  shows in-flight/errored rows, never finished ones. */
  onUploaded: (result: StoredFileViewModel, sourceFile: File) => void;
}

export interface FileUploadAnnouncement {
  /** Increments on every announcement, including a repeated identical message, so
   *  `<FileUploader>` can key its live region on it — forcing assistive tech to treat unchanged
   *  text as a fresh node worth (re-)announcing (ADR-FE-153/C6: exactly one announcement per
   *  terminal event). */
  id: number;
  text: string;
}

export interface UseFileUploadResult {
  queue: FileUploadQueueItem[];
  announcement: FileUploadAnnouncement;
  addFiles: (files: FileList | File[]) => void;
  retry: (id: string) => void;
  /** Aborts an in-flight upload, or drops a queued/errored row — either way the row disappears
   *  and exactly one "removed" announcement fires (C5/C6). */
  remove: (id: string) => void;
}

let nextRowId = 0;
function makeRowId(): string {
  nextRowId += 1;
  return `file-upload-row-${nextRowId}`;
}

export function useFileUpload({
  purpose,
  audience,
  onUploaded,
}: UseFileUploadOptions): UseFileUploadResult {
  const [queue, setQueue] = React.useState<FileUploadQueueItem[]>([]);
  const [announcement, setAnnouncement] = React.useState<FileUploadAnnouncement>({
    id: 0,
    text: '',
  });
  const controllersRef = React.useRef(new Map<string, AbortController>());
  const runningRef = React.useRef(false);
  const queueRef = React.useRef<FileUploadQueueItem[]>(queue);

  /**
   * React batches a `setState` call made outside a DOM event handler (a promise callback, e.g.
   * every `.then`/`.catch`/`.finally` in `processNext` below) into a microtask-scheduled
   * re-render — `queueRef.current` would NOT reflect it yet if this hook only refreshed the ref
   * during render. `processNext`'s own `.finally()` calls itself again immediately, in the same
   * promise-chain tick as the `setQueue` call that just changed what "next" means — reading a ref
   * that render hasn't refreshed yet would re-pick an item this same turn just finished with
   * (reproduced directly: it re-uploaded an already-rejected file dozens of times a second until
   * the process ran out of memory). Updating `queueRef.current` HERE, synchronously, the instant a
   * change is made — never waiting for the next render to do it — closes that gap.
   */
  function updateQueue(updater: (current: FileUploadQueueItem[]) => FileUploadQueueItem[]): void {
    const next = updater(queueRef.current);
    queueRef.current = next;
    setQueue(next);
  }

  const announce = React.useCallback((text: string) => {
    setAnnouncement((previous) => ({ id: previous.id + 1, text }));
  }, []);

  const processNext = React.useCallback(() => {
    if (runningRef.current) return;
    const next = queueRef.current.find((item) => item.status === 'queued');
    if (!next) return;

    runningRef.current = true;
    updateQueue((current) =>
      current.map((item) =>
        item.id === next.id ? { ...item, status: 'uploading', progress: 0 } : item,
      ),
    );

    const controller = new AbortController();
    controllersRef.current.set(next.id, controller);

    uploadFile({
      file: next.file,
      purpose,
      audience,
      signal: controller.signal,
      onProgress: (fraction) => {
        updateQueue((current) =>
          current.map((item) => (item.id === next.id ? { ...item, progress: fraction } : item)),
        );
      },
    })
      .then((result) => {
        updateQueue((current) => current.filter((item) => item.id !== next.id));
        announce(`${next.file.name} finished uploading.`);
        onUploaded(result, next.file);
      })
      .catch((error: unknown) => {
        if (error instanceof FileUploadAbortedError) {
          // A deliberate cancel — `remove()` already filtered the row and announced it (C5/C6).
          return;
        }

        // `FileUploadRateLimitedError`, `FileUploadNetworkError`, `FileUploadSessionError`
        // (gate-fix 2026-09-28: the session/idle-lock surface is notified separately, inside
        // `uploadFile` itself — see `lib/api/client.ts`'s `notifySessionError`) and
        // `FileUploadForbiddenError` (a CSRF refusal) all read as the same "did not finish
        // uploading" service state to a person (§8.2) — none of them is something they caused by
        // picking a file, so all fall through to the same `NetworkFailure` code.
        const code: FileQueueErrorCode =
          error instanceof FileUploadValidationError ? error.code : 'NetworkFailure';

        const retryAfterSeconds =
          error instanceof FileUploadRateLimitedError
            ? Math.max(0, Math.ceil(error.retryAfterSeconds))
            : undefined;
        const message =
          retryAfterSeconds !== undefined
            ? 'Too many uploads just now. Please wait, then retry.'
            : describeFileError({
                code,
                fileName: next.file.name,
                fileSizeInBytes: next.file.size,
                fileContentType: next.file.type,
                purpose,
              });

        announce(
          retryAfterSeconds !== undefined
            ? `${next.file.name}: too many uploads. Wait ${retryAfterSeconds} ${retryAfterSeconds === 1 ? 'second' : 'seconds'}, then retry.`
            : message,
        );

        if (isAutoRemovedFileError(code)) {
          updateQueue((current) => current.filter((item) => item.id !== next.id));
        } else {
          updateQueue((current) =>
            current.map((item) =>
              item.id === next.id
                ? {
                    ...item,
                    status: 'error',
                    error: {
                      code,
                      message,
                      retryable: isRetryableFileError(code),
                      ...(retryAfterSeconds !== undefined && {
                        retryAfterSeconds,
                        retryAvailableAt: Date.now() + retryAfterSeconds * 1000,
                      }),
                    },
                  }
                : item,
            ),
          );
        }
      })
      .finally(() => {
        controllersRef.current.delete(next.id);
        runningRef.current = false;
        processNext();
      });
  }, [purpose, audience, onUploaded, announce]);

  // Kicks the runner whenever the queue changes shape (a file added, a retry re-queued). Mostly
  // a belt-and-braces trigger for state changes made OUTSIDE `updateQueue` (there are none today,
  // but a future change is one accidental `setQueue` call away) — `processNext`'s own guard makes
  // this a no-op the rest of the time.
  React.useEffect(() => {
    processNext();
  }, [queue, processNext]);

  const addFiles = React.useCallback(
    (files: FileList | File[]) => {
      const incoming = Array.from(files);
      const newItems: FileUploadQueueItem[] = incoming.map((file) => {
        const failure = validateFileClientSide(file, purpose);
        const id = makeRowId();
        if (!failure) {
          return { id, file, status: 'queued', progress: 0 };
        }
        const message = describeFileError({
          code: failure.code,
          fileName: file.name,
          fileSizeInBytes: file.size,
          fileContentType: file.type,
          purpose,
        });
        return {
          id,
          file,
          status: 'error',
          progress: 0,
          error: { code: failure.code, message, retryable: false },
        };
      });

      for (const item of newItems) {
        if (item.error) announce(item.error.message);
      }
      updateQueue((current) => [...current, ...newItems]);
      processNext();
    },
    [purpose, announce, processNext],
  );

  const retry = React.useCallback(
    (id: string) => {
      // A `429` row can't be retried before its `Retry-After` has passed (the button is disabled
      // too; this keeps a stray call honest).
      const waiting = queueRef.current.find((item) => item.id === id)?.error?.retryAvailableAt;
      if (waiting !== undefined && waiting > Date.now()) return;
      updateQueue((current) =>
        current.map((item) =>
          item.id === id ? { id: item.id, file: item.file, status: 'queued', progress: 0 } : item,
        ),
      );
      processNext();
    },
    [processNext],
  );

  const remove = React.useCallback(
    (id: string) => {
      const item = queueRef.current.find((candidate) => candidate.id === id);
      if (!item) return;

      updateQueue((current) => current.filter((candidate) => candidate.id !== id));
      announce(`${item.file.name} removed.`);

      const controller = controllersRef.current.get(id);
      controller?.abort(); // lets processNext's own catch/finally continue the queue naturally
    },
    [announce],
  );

  React.useEffect(() => {
    const controllers = controllersRef.current;
    return () => {
      for (const controller of controllers.values()) controller.abort();
      controllers.clear();
    };
  }, []);

  return { queue, announcement, addFiles, retry, remove };
}
