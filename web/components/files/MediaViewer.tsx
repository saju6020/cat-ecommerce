'use client';

import * as React from 'react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  useDialogReturnFocus,
} from '@/components/ui/dialog';
import { humanizeContentType, isRenderableImageContentType } from '@/lib/files/fileRules';
import type { StoredFileViewModel } from '@/lib/api/files';

export interface MediaViewerProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** `null` while nothing is open — the dialog renders nothing until both `open` and `file` are
   *  set, so a caller can keep this mounted once and just flip `file`/`open` together. */
  file: StoredFileViewModel | null;
  /** Required — becomes the image's `alt` text / the video's accessible label, supplied by the
   *  consuming area (§8.3). */
  caption: string;
  /** Re-fetches the host's own record for a fresh signed URL (ADR-FE-154). See `FileThumb`'s own
   *  doc comment — same contract, same "at most once" behaviour. */
  onRefresh?: () => Promise<StoredFileViewModel | null | undefined>;
}

const EXPIRED_MESSAGE = 'This file could not be loaded — refresh the page.';

/**
 * `<MediaViewer>` — HO-FIL-002 §3/§8.3. An accessible modal dialog (built on
 * `components/ui/dialog.tsx`, per this handover's "build any dialog on `dialog.tsx`" rule):
 * Radix gives focus-trapping and `Escape`-to-close for free; `useDialogReturnFocus(open)` (the
 * same helper `ConfirmDialog` uses) returns focus to whatever was focused the instant `open`
 * flipped `true` — the thumb that opened it, with no extra wiring required from the caller (C7).
 *
 * Images and MP4 video render inline; a PDF renders via the browser's own viewer (an `<iframe>`
 * pointed at the signed URL — paging is the browser's, not this component's, to implement).
 * Anything else becomes a download link (ADR-FE-157) — this component never renders HTML or an
 * inlined SVG from the store, whatever the declared content type says (ADR-FE-158).
 */
export function MediaViewer({ open, onOpenChange, file, caption, onRefresh }: MediaViewerProps) {
  const [current, setCurrent] = React.useState(file);
  const [failed, setFailed] = React.useState(false);
  // Gate-fix 2026-09-28 (SECURITY MEDIUM) — see FileThumb.tsx's identical comment: keyed on
  // `fileId` alone, never reset by the effect below, so a host that re-fetches and passes a new
  // URL for the SAME file that also fails does not refresh again.
  const refreshAttemptedForFileIdRef = React.useRef<string | null>(null);
  const returnFocusRef = useDialogReturnFocus(open);

  // FIL backlog 17: gated on `open` — a closed viewer whose host keeps `file` set must not start
  // anything that ends in `onRefresh` (re-syncs when it opens, since `open` is a dependency).
  React.useEffect(() => {
    if (!open) return;
    setCurrent(file);
    setFailed(false);
    // Deliberately keyed on identity (fileId/url) — see FileThumb.tsx's identical comment.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, file?.fileId, file?.url]);

  const attemptRefresh = React.useCallback(
    async (fileId: string) => {
      if (refreshAttemptedForFileIdRef.current === fileId || !onRefresh) {
        setFailed(true);
        return;
      }
      refreshAttemptedForFileIdRef.current = fileId;
      try {
        const fresh = await onRefresh();
        if (fresh) {
          setCurrent(fresh);
          setFailed(false);
        } else {
          setFailed(true);
        }
      } catch {
        setFailed(true);
      }
    },
    [onRefresh],
  );

  // Gate-fix 2026-09-28 (ARCHITECTURE MEDIUM) — see FileThumb.tsx's identical comment: proactively
  // covers §8.4/ADR-FE-154's "or a past urlExpiresOn" trigger, not only "on an image error".
  React.useEffect(() => {
    if (!open) return; // FIL backlog 17
    if (current && new Date(current.urlExpiresOn).getTime() <= Date.now()) {
      void attemptRefresh(current.fileId);
    }
  }, [open, current, attemptRefresh]);

  function handleMediaError() {
    if (!current) return;
    void attemptRefresh(current.fileId);
  }
  const handleMediaErrorRef = React.useRef(handleMediaError);
  handleMediaErrorRef.current = handleMediaError;
  const pdfFrameRef = React.useCallback((node: HTMLIFrameElement | null) => {
    if (!node) return;
    const listener = () => handleMediaErrorRef.current();
    node.addEventListener('error', listener);
    return () => node.removeEventListener('error', listener);
  }, []);

  if (!current) return null;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent returnFocusRef={returnFocusRef} className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{caption}</DialogTitle>
        </DialogHeader>

        {failed ? (
          <p className="py-10 text-center text-sm text-ink-muted">{EXPIRED_MESSAGE}</p>
        ) : current.contentType === 'application/pdf' ? (
          // No `sandbox` here (SECURITY gate LOW) — Chromium refuses to render a PDF inside a
          // sandboxed frame at all. The content route itself already sends
          // `Content-Security-Policy: default-src 'none'; sandbox` on the framed response, so the
          // document inside this frame can neither execute script nor navigate the top frame
          // regardless of this element's own attributes; `referrerPolicy` is this element's own
          // defence-in-depth on top of that.
          <iframe
            src={current.url}
            title={caption}
            referrerPolicy="no-referrer"
            // FIL backlog 11: a failed frame load goes through the same refresh-once guard as
            // `<img>`/`<video>`. React does not wire `onError` for an `<iframe>`, so the native
            // `error` event is listened for via `pdfFrameRef`. (An expired URL that the store answers
            // with an error page fires no event at all; that case is the proactive `urlExpiresOn`
            // check above.)
            ref={pdfFrameRef}
            className="h-[70vh] w-full rounded-[var(--radius-sm)] border border-border"
          />
        ) : isRenderableImageContentType(current.contentType) ? (
          // Signed, short-lived URL; see FileThumb.tsx's identical comment.
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={current.url}
            alt={caption}
            referrerPolicy="no-referrer"
            onError={() => void handleMediaError()}
            className="max-h-[70vh] w-full rounded-[var(--radius-sm)] object-contain"
          />
        ) : current.contentType === 'video/mp4' ? (
          // No `referrerPolicy` here — unlike `<a>`/`<img>`/`<iframe>`, `referrerpolicy` is not a
          // defined content attribute for `<video>` per the HTML spec (confirmed: React's own
          // `VideoHTMLAttributes` type has no such member, and TypeScript refuses it). The content
          // route's own `Referrer-Policy: no-referrer` response header (HO-FIL-001 §8.6) still
          // applies regardless of what this element requests.
          <video
            src={current.url}
            controls
            aria-label={caption}
            onError={() => void handleMediaError()}
            className="max-h-[70vh] w-full rounded-[var(--radius-sm)]"
          >
            <track kind="captions" />
          </video>
        ) : (
          // ADR-FE-157: no preview for a type the browser cannot safely display inline (DICOM,
          // ZIP, CSV, ...) — a download link instead. ADR-FE-158: never rendered as HTML or an
          // inlined SVG from the store, whatever the declared content type says.
          <a
            href={current.url}
            download={current.originalFileName}
            referrerPolicy="no-referrer"
            className="inline-flex items-center gap-2 rounded-[var(--radius-sm)] border border-border-strong bg-surface px-4 py-2 text-sm font-semibold text-ink hover:bg-surface-sunken"
          >
            Download {current.originalFileName} ({humanizeContentType(current.contentType)})
          </a>
        )}
      </DialogContent>
    </Dialog>
  );
}
