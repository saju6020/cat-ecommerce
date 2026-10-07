'use client';

import * as React from 'react';
import { ImageOff } from 'lucide-react';
import { CAT_PHOTO_RULES, type CatPhotoContentType } from '@/lib/catPhotoRules';
import { cn } from '@/lib/utils';

/**
 * `components/cat/PhotoField.tsx` — US-CAT-01 AC3, under the §4.2/§11.6 constraint: the API
 * takes a storage key + content type + size, not a file, because object storage is not
 * provisioned (§20.4).
 *
 * What this component actually does, per §11.6:
 *  - a real file picker, because that's what the criterion describes;
 *  - **validates the chosen file in the browser** against the same limits the API enforces
 *    (`lib/catPhotoRules.ts`, mirroring HO-CAT-001's `CatPhotoRules`) and rejects with a
 *    plain-language reason naming the actual limit — component tests U14/U15;
 *  - on a valid file, calls `onFileValidated` with the `File` so the caller can derive metadata
 *    (name, type, size) plus a dev-only storage key, and shows an honest inline note that image
 *    storage isn't connected yet rather than pretending an upload happened;
 *  - `serverError` renders the same way a client rejection does, for the `400` the real API
 *    still returns if this validation is ever bypassed (§11.6's "client validation is a
 *    courtesy, never the guarantee").
 *
 * Does **not** upload anything, add an object-storage client, or write a file anywhere — that is
 * backend scope (§20.4).
 */
export interface PhotoFieldProps {
  id: string;
  label?: string;
  onFileValidated: (file: File) => void;
  /** A `400` mapped back onto this field by the caller (HO-CAT-002 §11.8). */
  serverError?: string;
  className?: string;
}

export function PhotoField({
  id,
  label = 'Photo',
  onFileValidated,
  serverError,
  className,
}: PhotoFieldProps) {
  const [clientError, setClientError] = React.useState<string | null>(null);
  const [selectedFile, setSelectedFile] = React.useState<File | null>(null);
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  const shownError = clientError ?? serverError ?? null;
  const maxMb = CAT_PHOTO_RULES.maxSizeInBytes / (1024 * 1024);

  function handleChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    setSelectedFile(null);
    setClientError(null);
    if (!file) return;

    if (!(CAT_PHOTO_RULES.allowedContentTypes as readonly string[]).includes(file.type)) {
      setClientError("That file type isn't supported. Accepted types are JPEG, PNG and WebP.");
      event.target.value = '';
      return;
    }
    if (file.size > CAT_PHOTO_RULES.maxSizeInBytes) {
      const sizeMb = (file.size / (1024 * 1024)).toFixed(1);
      setClientError(`That image is ${sizeMb} MB. The maximum is ${maxMb} MB.`);
      event.target.value = '';
      return;
    }

    setSelectedFile(file);
    onFileValidated(file);
  }

  return (
    <div className={cn('flex flex-col gap-1.5', className)}>
      <label htmlFor={id} className="text-sm font-semibold text-ink">
        {label}
      </label>
      <input
        id={id}
        type="file"
        accept={(CAT_PHOTO_RULES.allowedContentTypes as readonly CatPhotoContentType[]).join(',')}
        onChange={handleChange}
        aria-describedby={
          [hintId, shownError ? errorId : undefined].filter(Boolean).join(' ') || undefined
        }
        aria-invalid={!!shownError}
        className="text-sm text-ink file:mr-3 file:rounded-[var(--radius-sm)] file:border file:border-border-strong file:bg-surface file:px-3 file:py-1.5 file:text-sm file:font-semibold file:text-ink"
      />
      <p id={hintId} className="text-xs text-ink-muted">
        JPEG, PNG or WebP, up to {maxMb} MB.
      </p>
      {shownError && (
        <p id={errorId} role="alert" className="text-xs font-semibold text-danger-ink">
          {shownError}
        </p>
      )}
      {selectedFile && !shownError && (
        <div className="flex items-start gap-2 rounded-[var(--radius-sm)] border border-border bg-surface-sunken p-2.5 text-xs text-ink-muted">
          <ImageOff size={14} className="mt-0.5 flex-none" aria-hidden="true" />
          <span>
            &ldquo;{selectedFile.name}&rdquo; passed validation. Image storage isn&rsquo;t connected
            yet, so this record keeps the file&rsquo;s details, not the picture itself.
          </span>
        </div>
      )}
    </div>
  );
}
