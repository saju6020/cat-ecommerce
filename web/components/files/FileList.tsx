'use client';

import * as React from 'react';
import { Download, X } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { FileThumb } from './FileThumb';
import { formatFileSize, humanizeContentType } from '@/lib/files/fileRules';
import type { StoredFileViewModel } from '@/lib/api/files';

export interface FileListProps {
  /** The host's own controlled list of uploaded files (ADR-FE-156) — this component never
   *  fetches, stores, or mutates it. */
  files: StoredFileViewModel[];
  /** Removes a file from the host's list. Omit to render a read-only list (no remove control). */
  onRemove?: (file: StoredFileViewModel) => void;
  /** Opens a viewer for the file — wire this to your own `<MediaViewer>`'s `open`/`file` state.
   *  Omit to make the thumb non-interactive. */
  onView?: (file: StoredFileViewModel) => void;
  /** Re-fetches the host's own record for a fresh signed URL when a thumb's image fails to load
   *  (ADR-FE-154) — forwarded to each row's `<FileThumb>`. */
  onRefresh?: (file: StoredFileViewModel) => Promise<StoredFileViewModel | null | undefined>;
  /** The caption/alt text for each row's thumb — receives the file so a caller can build
   *  something like "Photo of Whiskers, added 2 Sept 2026". Defaults to the file's own name. */
  getCaption?: (file: StoredFileViewModel) => string;
  /** Shown instead of an empty `<ul>` when `files` is empty. Renders nothing if omitted — the
   *  host usually already has its own empty-state copy around this list. */
  emptyMessage?: string;
  className?: string;
}

/**
 * `<FileList>` — HO-FIL-002 §3: uploaded files with name, size, type icon and download/remove.
 * Purely a presentation of the host's own controlled `files` array (ADR-FE-156) — it fetches
 * nothing and keeps no state of its own beyond what `<FileThumb>` needs for the ADR-FE-154 refresh
 * dance.
 */
export function FileList({
  files,
  onRemove,
  onView,
  onRefresh,
  getCaption,
  emptyMessage,
  className,
}: FileListProps) {
  if (files.length === 0) {
    return emptyMessage ? (
      <p className={cn('text-sm text-ink-muted', className)}>{emptyMessage}</p>
    ) : null;
  }

  return (
    <ul className={cn('flex flex-col gap-2', className)}>
      {files.map((file) => {
        const caption = getCaption ? getCaption(file) : file.originalFileName;
        return (
          <li
            key={file.fileId}
            className="flex items-center gap-3 rounded-[var(--radius-sm)] border border-border bg-surface p-2.5"
          >
            <FileThumb
              file={file}
              alt={caption}
              width={48}
              height={48}
              onOpen={onView}
              onRefresh={onRefresh ? () => onRefresh(file) : undefined}
            />
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-semibold text-ink" title={file.originalFileName}>
                {file.originalFileName}
              </p>
              <p className="text-xs text-ink-muted">
                {humanizeContentType(file.contentType)} · {formatFileSize(file.sizeInBytes)}
              </p>
            </div>
            <div className="flex flex-none items-center gap-1">
              <a
                href={file.url}
                download={file.originalFileName}
                referrerPolicy="no-referrer"
                aria-label={`Download ${file.originalFileName}`}
                className="inline-flex h-8 w-8 items-center justify-center rounded-[var(--radius-sm)] text-ink-muted hover:bg-surface-sunken focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
              >
                <Download size={16} aria-hidden="true" />
              </a>
              {onRemove && (
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label={`Remove ${file.originalFileName}`}
                  onClick={() => onRemove(file)}
                >
                  <X size={16} aria-hidden="true" />
                </Button>
              )}
            </div>
          </li>
        );
      })}
    </ul>
  );
}
