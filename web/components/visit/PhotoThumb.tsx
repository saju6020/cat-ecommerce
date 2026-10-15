'use client';

import { FileThumb } from '@/components/files/FileThumb';
import { formatDateOnly } from '@/lib/format/time';
import type { StoredFileViewModel } from '@/lib/api/files';
import type { VisitPhoto } from '@/lib/api/visits';

/**
 * A visit photo as the file module's `StoredFileViewModel`. The visit payload carries a signed `url`
 * and `thumbnailUrl` (the same file — there is no server thumbnail) but not the file id, so the
 * photo's own id stands in for it (it is only ever an identity for `FileThumb`'s refresh-once guard).
 */
export function photoAsFile(photo: VisitPhoto): StoredFileViewModel | null {
  if (!photo.url) return null;
  return {
    fileId: photo.id,
    url: photo.url,
    contentType: photo.contentType ?? 'image/jpeg',
    sizeInBytes: 0,
    originalFileName: `Photo taken ${photo.takenOn}`,
    urlExpiresOn: photo.urlExpiresOn ?? new Date(Date.now() + 60_000).toISOString(),
  };
}

export function photoCaption(photo: VisitPhoto): string {
  return `Photo taken ${formatDateOnly(photo.takenOn)}${photo.note ? `: ${photo.note}` : ''}`;
}

/**
 * `PhotoThumb` — HO-EMR-002 §11.3 (US-CLN-19). The signed thumbnail with its date and note beneath.
 * An `<img>` error triggers exactly ONE refetch of the visit for a fresh URL (`onRefresh`,
 * ADR-FE-154 via `FileThumb`) — the URL is never stored, logged or put in a query key (U-X4).
 */
export function PhotoThumb({
  photo,
  onRefresh,
  onOpen,
}: {
  photo: VisitPhoto;
  onRefresh?: () => Promise<VisitPhoto | undefined>;
  onOpen?: (photo: VisitPhoto) => void;
}) {
  const file = photoAsFile(photo);
  return (
    <figure className="m-0 flex flex-col gap-1">
      {file ? (
        <FileThumb
          file={file}
          alt={photoCaption(photo)}
          width={112}
          height={112}
          onOpen={onOpen ? () => onOpen(photo) : undefined}
          onRefresh={
            onRefresh
              ? async () => {
                  const fresh = (await onRefresh()) ?? undefined;
                  return fresh ? photoAsFile(fresh) : null;
                }
              : undefined
          }
        />
      ) : (
        <p className="flex h-28 w-28 items-center justify-center rounded-[var(--radius-sm)] border border-border bg-surface-sunken p-2 text-center text-xs text-ink-muted">
          Photo unavailable
        </p>
      )}
      <figcaption className="text-xs text-ink-muted">
        {formatDateOnly(photo.takenOn)}
        {photo.note && <span className="block whitespace-pre-wrap break-words">{photo.note}</span>}
      </figcaption>
    </figure>
  );
}
