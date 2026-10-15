'use client';

import * as React from 'react';
import { FileText, Film, File as FileIcon } from 'lucide-react';
import { cn } from '@/lib/utils';
import { humanizeContentType, isRenderableImageContentType } from '@/lib/files/fileRules';
import type { StoredFileViewModel } from '@/lib/api/files';

export interface FileThumbProps {
  file: StoredFileViewModel;
  /** Required — becomes the image's `alt` text, the caption the consuming area supplies (§8.3). */
  alt: string;
  width?: number;
  height?: number;
  className?: string;
  /**
   * Re-fetches the host's own record to get a fresh signed URL (ADR-FE-154) — this component
   * never talks to `GET /api/files/{id}/content` or signs anything itself (HO-FIL-001 §14). Called
   * at most once per mounted `file`; omit when the host has no read to re-run, in which case a
   * load failure just shows the plain message.
   */
  onRefresh?: () => Promise<StoredFileViewModel | null | undefined>;
  /** If provided, the thumb becomes a `<button>` that calls this on click/activation — typically
   *  to open `<MediaViewer>`. Omit for a purely decorative thumb (e.g. inside `<FileList>`). */
  onOpen?: (file: StoredFileViewModel) => void;
}

const EXPIRED_MESSAGE = 'This file could not be loaded — refresh the page.';

function fallbackIcon(contentType: string) {
  if (contentType === 'application/pdf') return <FileText size={20} aria-hidden="true" />;
  if (contentType.startsWith('video/')) return <Film size={20} aria-hidden="true" />;
  return <FileIcon size={20} aria-hidden="true" />;
}

/**
 * `<FileThumb>` — HO-FIL-002 §3/§8.5 (ADR-FE-155). No thumbnails exist server-side (HO-FIL-001
 * stores originals only) — this renders the full image at display size with `loading="lazy"`,
 * `decoding="async"` and explicit `width`/`height` (no layout shift), rather than pretending a
 * smaller derivative exists. A non-image file gets a type-appropriate icon and label, never a
 * broken-image icon (§3).
 */
export function FileThumb({
  file,
  alt,
  width = 96,
  height = 96,
  className,
  onRefresh,
  onOpen,
}: FileThumbProps) {
  const [current, setCurrent] = React.useState(file);
  const [failed, setFailed] = React.useState(false);
  // Gate-fix 2026-09-28 (SECURITY MEDIUM) — keyed on `fileId` ALONE, and never reset by the effect
  // below. The bug this replaces: keying (or resetting) the guard on `file.url` meant a host that
  // re-fetches its own record and passes a NEW url for the SAME file (ADR-FE-154's own prescribed
  // "re-run their own read") cleared the guard, and a persistently bad object (a corrupt body that
  // fails to decode, a 404/429) refreshed forever — each refresh hammering the host's read and the
  // signed-URL-serving endpoint. A `fileId` that has already had its one attempt never gets
  // another; only a genuinely different file does.
  const refreshAttemptedForFileIdRef = React.useRef<string | null>(null);

  React.useEffect(() => {
    setCurrent(file);
    setFailed(false);
    // Deliberately keyed on identity (fileId/url), not the whole `file` object — a host that
    // passes a fresh object literal with the SAME fileId/url on every render must not spuriously
    // clear an already-`failed` display each render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [file.fileId, file.url]);

  const attemptRefresh = React.useCallback(
    async (fileId: string) => {
      // ADR-FE-154: one re-fetch of the host's own read, then the plain message — never a retry
      // loop, and an expired URL is never mistaken for a missing file.
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

  // Gate-fix 2026-09-28 (ARCHITECTURE MEDIUM) — §8.4/ADR-FE-154 triggers a refresh "on an image
  // error OR a past urlExpiresOn". This proactively covers the second case: a file whose signed
  // URL has already expired by the time it renders (a tab left open past the 5-minute lifetime)
  // is refreshed before ever asking the browser to load a URL known to be dead, rather than
  // relying solely on the resulting `403` reaching `onError`.
  React.useEffect(() => {
    if (new Date(current.urlExpiresOn).getTime() <= Date.now()) {
      void attemptRefresh(current.fileId);
    }
  }, [current, attemptRefresh]);

  function handleImageError() {
    void attemptRefresh(current.fileId);
  }

  // An empty `url` is `resolveFileUrl`'s "unusable" (protocol-relative, script scheme): no <img>.
  const content = failed || !current.url ? (
    <div
      className="flex items-center justify-center bg-surface-sunken p-1 text-center text-[10px] text-ink-muted"
      style={{ width, height }}
    >
      {EXPIRED_MESSAGE}
    </div>
  ) : isRenderableImageContentType(current.contentType) ? (
    // A signed, short-lived, cross-origin GCS-backed URL (ADR-318) is not something
    // `next/image`'s remote-pattern allowlist can name once per file; a plain `<img>` is the
    // correct tool here, matching PhotoField's precedent.
    // eslint-disable-next-line @next/next/no-img-element
    <img
      src={current.url}
      alt={alt}
      width={width}
      height={height}
      loading="lazy"
      decoding="async"
      referrerPolicy="no-referrer"
      onError={() => void handleImageError()}
      className="rounded-[var(--radius-sm)] object-cover"
      style={{ width, height }}
    />
  ) : (
    <div
      className="flex flex-col items-center justify-center gap-1 rounded-[var(--radius-sm)] border border-border bg-surface-sunken text-ink-muted"
      style={{ width, height }}
    >
      {fallbackIcon(current.contentType)}
      <span className="px-1 text-center text-[10px] leading-tight">
        {humanizeContentType(current.contentType)}
      </span>
    </div>
  );

  if (!onOpen) return <div className={className}>{content}</div>;

  return (
    <button
      type="button"
      onClick={() => onOpen(current)}
      className={cn(
        'block rounded-[var(--radius-sm)] focus-visible:outline-2 focus-visible:outline-offset-2',
        'focus-visible:outline-accent',
        className,
      )}
    >
      {content}
    </button>
  );
}
