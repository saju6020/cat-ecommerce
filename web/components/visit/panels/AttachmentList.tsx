import { Download, ExternalLink, Paperclip } from 'lucide-react';
import { buttonVariants } from '@/components/ui/button';
import { formatFileSize, humanizeContentType } from '@/lib/files/fileRules';
import { formatDateOnly } from '@/lib/format/time';
import type { DiagnosticAttachment } from '@/lib/api/diagnostics';

/**
 * `AttachmentList` — HO-EMR-002 §11.3 (US-CLN-14 AC3, U-C3). Lab attachments from their signed URLs.
 * A PDF or image has **Open** (a new tab, `noopener noreferrer`); a DICOM file has **Download** only —
 * DICOM is never rendered inline. The URL lives only in the fetched payload (U-X4): it is an `href`,
 * never stored, logged or put in a query key.
 */
export function AttachmentList({ attachments }: { attachments: DiagnosticAttachment[] }) {
  if (attachments.length === 0) return <p className="text-sm text-ink-muted">No files attached.</p>;
  return (
    <ul className="m-0 flex list-none flex-col gap-2 p-0">
      {attachments.map((attachment) => {
        const file = attachment.file;
        const isDicom = file?.contentType === 'application/dicom';
        return (
          <li
            key={attachment.id}
            className="flex flex-wrap items-center justify-between gap-2 rounded-[var(--radius-sm)] border border-border bg-surface p-2.5"
          >
            <div className="flex min-w-0 items-start gap-2 text-sm">
              <Paperclip size={14} className="mt-1 flex-none text-ink-muted" aria-hidden="true" />
              <div className="min-w-0">
                <p className="truncate font-semibold text-ink">
                  {file?.originalFileName ?? 'File unavailable'}
                </p>
                <p className="text-xs text-ink-muted">
                  {file
                    ? `${humanizeContentType(file.contentType)} · ${formatFileSize(file.sizeInBytes)} · `
                    : ''}
                  {formatDateOnly(attachment.resultDate)}
                </p>
                {attachment.note && (
                  <p className="whitespace-pre-wrap text-xs text-ink-muted">{attachment.note}</p>
                )}
              </div>
            </div>
            {file?.url &&
              (isDicom ? (
                <a
                  href={file.url}
                  download={file.originalFileName}
                  rel="noopener noreferrer"
                  className={buttonVariants({ variant: 'secondary', size: 'sm' })}
                  aria-label={`Download ${file.originalFileName}`}
                >
                  <Download size={14} aria-hidden="true" /> Download
                </a>
              ) : (
                <a
                  href={file.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className={buttonVariants({ variant: 'secondary', size: 'sm' })}
                  aria-label={`Open ${file.originalFileName} in a new tab`}
                >
                  <ExternalLink size={14} aria-hidden="true" /> Open
                </a>
              ))}
          </li>
        );
      })}
    </ul>
  );
}
