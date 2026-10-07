'use client';

import * as React from 'react';
import { Upload, X, RotateCcw } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { useFileUpload } from '@/lib/hooks/useFileUpload';
import { describeAllowedTypes, findFileRule, formatFileSize } from '@/lib/files/fileRules';
import type { FileUploadAudience, FilePurpose, StoredFileViewModel } from '@/lib/api/files';

export interface FileUploaderProps {
  /** Used to associate the hint text; also becomes the hidden `<input>`'s `id`. */
  id: string;
  /** The visible/accessible name of the drop-zone button, e.g. "Add product photos". */
  label: string;
  purpose: FilePurpose;
  audience: FileUploadAudience;
  /** Called once per file that finishes uploading — add it to your own controlled list
   *  (ADR-FE-156). This component never keeps a finished upload in its own state. */
  onUploaded: (result: StoredFileViewModel, sourceFile: File) => void;
  multiple?: boolean;
  disabled?: boolean;
  /** Overrides the default "<allowed types>, up to <cap>." hint derived from `fileRules.ts`. */
  hint?: string;
  className?: string;
}

/**
 * `<FileUploader>` — HO-FIL-002 §3, the **one** upload control (§2). Renders into whatever page
 * hosts it (§2's "deliberately unopinionated about layout") — no page, route or purpose-specific
 * copy lives here.
 *
 * **The control (ADR-FE-152).** The drop zone is a real `<button>` — natively focusable,
 * natively activated by both `Enter` and `Space` — wrapping a visually hidden `<input
 * type="file">`. The button's own click handler (which also fires for a keyboard activation,
 * since browsers dispatch a `click` event for both) opens the input's native file dialog
 * imperatively; the input itself is `tabIndex={-1}`/`aria-hidden` so there is exactly one tab
 * stop here, not two. Drag-and-drop handlers live on the same button — an enhancement layered on
 * top of the same control, never a second, drag-only way to reach it (C1: keyboard-only, no
 * pointer, reaches everything this component can do).
 *
 * **Progress and announcements (§8.3, ADR-FE-153).** Each queued file gets its own `<progress>`
 * with an accessible name — visual only, never read into the live region. Exactly one
 * `aria-live="polite"` announcement fires per completion, failure or removal (C6); the region is
 * keyed on `announcement.id` so two consecutive, identically-worded announcements are still each
 * treated as fresh content by assistive tech.
 */
export function FileUploader({
  id,
  label,
  purpose,
  audience,
  onUploaded,
  multiple = true,
  disabled = false,
  hint,
  className,
}: FileUploaderProps) {
  const inputRef = React.useRef<HTMLInputElement>(null);
  const { queue, announcement, addFiles, retry, remove } = useFileUpload({
    purpose,
    audience,
    onUploaded,
  });
  const rule = findFileRule(purpose);
  const resolvedHint =
    hint ?? `${describeAllowedTypes(purpose)}, up to ${formatFileSize(rule.maxSizeInBytes)}.`;
  const hintId = `${id}-hint`;

  // FIL backlog 18: a clock that only ticks while some row is waiting out a `429`'s Retry-After.
  const latestRetryAt = queue.reduce(
    (latest, item) => Math.max(latest, item.error?.retryAvailableAt ?? 0),
    0,
  );
  const [now, setNow] = React.useState(() => Date.now());
  React.useEffect(() => {
    if (latestRetryAt <= Date.now()) return;
    setNow(Date.now());
    const timer = setInterval(() => {
      const current = Date.now();
      setNow(current);
      if (current >= latestRetryAt) clearInterval(timer);
    }, 500);
    return () => clearInterval(timer);
  }, [latestRetryAt]);

  function handleFiles(fileList: FileList | null) {
    if (!fileList || fileList.length === 0) return;
    // FIL backlog 16: `multiple={false}` only limits the native picker; a drop can carry several
    // files, so cap it here too.
    addFiles(multiple ? fileList : [fileList[0]!]);
  }

  function handleDrop(event: React.DragEvent<HTMLButtonElement>) {
    event.preventDefault();
    if (disabled) return;
    handleFiles(event.dataTransfer.files);
  }

  return (
    <div className={cn('flex flex-col gap-2', className)}>
      {/*
        The hidden `<input>` is a SIBLING of the button below, not a DOM descendant — deliberately,
        even though ADR-FE-152 describes "a button wrapping a visually hidden input": calling the
        input's own `.click()` from the button's handler dispatches a real click event that bubbles
        back up through whatever contains it, and if the input were nested INSIDE the button, that
        bubbled event would re-enter the button's own click handler and call `.click()` again
        (observed directly: exactly one extra, spurious re-entry before the platform's own
        reentrancy guard on `.click()` stops it going further). Keeping them siblings under this
        same wrapping `<div>` gets the identical result for every user — one real control, keyboard
        and screen-reader operable, drag-and-drop layered on top — without that bubbling hazard;
        nothing about this changes what a keyboard or screen-reader user can reach (C1).
      */}
      <button
        type="button"
        disabled={disabled}
        onClick={() => inputRef.current?.click()}
        onDragOver={(event) => event.preventDefault()}
        onDrop={handleDrop}
        aria-describedby={hintId}
        className={cn(
          'flex w-full items-center gap-2 rounded-[var(--radius-sm)] border border-dashed',
          'border-border-strong bg-surface px-4 py-3 text-left text-sm font-semibold text-ink',
          'hover:bg-surface-sunken focus-visible:outline-2 focus-visible:outline-offset-2',
          'focus-visible:outline-accent disabled:pointer-events-none disabled:opacity-50',
        )}
      >
        <Upload size={16} aria-hidden="true" className="flex-none" />
        <span>{label}</span>
      </button>
      <input
        ref={inputRef}
        id={id}
        type="file"
        multiple={multiple}
        disabled={disabled}
        accept={rule.allowedContentTypes.join(',')}
        tabIndex={-1}
        aria-hidden="true"
        className="sr-only"
        onChange={(event) => {
          handleFiles(event.target.files);
          // Lets the exact same file be re-picked after it was removed or errored — the browser
          // never fires `change` a second time for an unchanged file list otherwise.
          event.target.value = '';
        }}
      />
      <p id={hintId} className="text-xs text-ink-muted">
        {resolvedHint}
      </p>

      {queue.length > 0 && (
        <ul className="flex flex-col gap-2">
          {queue.map((item) => (
            <li
              key={item.id}
              className="flex flex-col gap-1.5 rounded-[var(--radius-sm)] border border-border bg-surface p-2.5 text-sm"
            >
              <div className="flex items-center justify-between gap-2">
                <span className="truncate text-ink" title={item.file.name}>
                  {item.file.name}
                </span>
                <span className="flex-none text-xs text-ink-muted">
                  {formatFileSize(item.file.size)}
                </span>
              </div>

              {item.status === 'uploading' && (
                <progress
                  aria-label={`Uploading ${item.file.name}`}
                  value={Math.round(item.progress * 100)}
                  max={100}
                  className="h-1.5 w-full accent-[var(--color-accent)]"
                />
              )}

              {item.status === 'error' && item.error && (
                <p className="text-xs font-semibold text-danger-ink">{item.error.message}</p>
              )}
              {item.status === 'error' && item.error?.retryAvailableAt !== undefined && (
                <RetryCountdown availableAt={item.error.retryAvailableAt} now={now} />
              )}

              <div className="flex justify-end gap-2">
                {item.status === 'error' && item.error?.retryable && (
                  <Button
                    type="button"
                    variant="secondary"
                    size="sm"
                    onClick={() => retry(item.id)}
                    aria-label={`Retry ${item.file.name}`}
                    disabled={
                      item.error.retryAvailableAt !== undefined && item.error.retryAvailableAt > now
                    }
                  >
                    <RotateCcw size={14} aria-hidden="true" />
                    Retry
                  </Button>
                )}
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  onClick={() => remove(item.id)}
                  aria-label={`${item.status === 'error' ? 'Remove' : 'Cancel'} ${item.file.name}`}
                >
                  <X size={14} aria-hidden="true" />
                  {item.status === 'error' ? 'Remove' : 'Cancel'}
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}

      <div aria-live="polite" role="status" className="sr-only" key={announcement.id}>
        {announcement.text}
      </div>
    </div>
  );
}

/** "Retry available in 12 seconds." until the `429`'s Retry-After has passed, then nothing. */
function RetryCountdown({ availableAt, now }: { availableAt: number; now: number }) {
  const remaining = Math.ceil((availableAt - now) / 1000);
  if (remaining <= 0) return null;
  return (
    <p className="text-xs text-ink-muted">
      You can retry in {remaining} {remaining === 1 ? 'second' : 'seconds'}.
    </p>
  );
}
