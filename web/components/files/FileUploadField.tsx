'use client';

import { useState } from 'react';
import { Paperclip, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { FileUploader } from './FileUploader';
import { formatFileSize } from '@/lib/files/fileRules';
import type { FilePurpose, StoredFileViewModel } from '@/lib/api/files';

/**
 * `FileUploadField` — HO-EMR-002 §11.7, ADR-FE-055. ONE file for a clinical purpose (`LabResult`,
 * `ClinicalPhoto`, `ConsentForm`), built on the File Storage module's `FileUploader` (HO-FIL-002) so
 * the type/size pre-check mirrors `FileRules` and a refusal (`FileTooLarge`, `FileTypeNotAllowed`,
 * `FileContentMismatch`, `FileRejected`, `FileScanUnavailable`) is shown as its plain-language
 * sentence with progress and cancel — never reimplemented here. After upload this shows a chip with
 * the file name and size and hands the `fileId` to the form, which posts the attach command. The
 * file is displayed later ONLY from a signed URL in a server payload (U-X4).
 */
export function FileUploadField({
  id,
  label,
  purpose,
  value,
  onChange,
  disabled,
  hint,
}: {
  id: string;
  label: string;
  purpose: FilePurpose;
  value: StoredFileViewModel | null;
  onChange: (file: StoredFileViewModel | null) => void;
  disabled?: boolean;
  hint?: string;
}) {
  // A fresh uploader per picked file: `FileUploader` keeps no finished upload (ADR-FE-156).
  const [uploaderKey, setUploaderKey] = useState(0);

  if (value) {
    return (
      <div className="flex flex-col gap-1.5">
        <p className="text-sm font-semibold text-ink">{label}</p>
        <div className="flex items-center justify-between gap-2 rounded-[var(--radius-sm)] border border-border bg-surface-sunken p-2.5 text-sm">
          <span className="flex min-w-0 items-center gap-2">
            <Paperclip size={14} className="flex-none text-ink-muted" aria-hidden="true" />
            <span className="truncate text-ink">{value.originalFileName}</span>
            <span className="flex-none text-xs text-ink-muted">
              {formatFileSize(value.sizeInBytes)}
            </span>
          </span>
          <Button
            variant="ghost"
            size="sm"
            disabled={disabled}
            aria-label={`Remove ${value.originalFileName}`}
            onClick={() => {
              onChange(null);
              setUploaderKey((k) => k + 1);
            }}
          >
            <X size={14} aria-hidden="true" /> Remove
          </Button>
        </div>
      </div>
    );
  }

  return (
    <FileUploader
      key={uploaderKey}
      id={id}
      label={label}
      purpose={purpose}
      audience="staff"
      multiple={false}
      disabled={disabled}
      hint={hint}
      onUploaded={(file) => onChange(file)}
    />
  );
}
