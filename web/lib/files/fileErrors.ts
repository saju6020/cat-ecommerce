import type { FilePurpose, FileServerViolationCode } from '@/lib/api/files';
import {
  describeAllowedTypes,
  findFileRule,
  formatFileSize,
  humanizeContentType,
} from './fileRules';

/**
 * **Location, gate-fix 2026-09-28 (ARCHITECTURE MEDIUM).** HO-FIL-002 §11 names this file
 * `components/files/fileErrors.ts`. It lives in `lib/files/` instead, alongside `fileRules.ts` —
 * see that file's own doc comment for why (this module's main caller, `lib/hooks/useFileUpload.ts`,
 * must not import from `components/`).
 */

/**
 * Every code this slice ever shows a person, plus two purely-client conditions that have no
 * `FileServerViolationCode` of their own (HO-FIL-001 §10.2):
 *  - `'NetworkFailure'` — a transport failure, a session/CSRF refusal (both notified through their
 *    own surface separately — see `lib/api/files.ts`), or a deliberate-but-unexpected abort the
 *    caller didn't ask for (HO-FIL-002 §8.2's "network / abort" row).
 *  - `'UnknownViolation'` (gate-fix 2026-09-28, SECURITY LOW) — a server `errorCode` this module
 *    does not recognise. Never shown verbatim; see `lib/api/files.ts`'s `KNOWN_VIOLATION_CODES`.
 */
export type FileQueueErrorCode = FileServerViolationCode | 'NetworkFailure' | 'UnknownViolation';

export interface FileErrorInput {
  code: FileQueueErrorCode;
  fileName: string;
  /** For `FileTooLarge` — the file's actual size, so the sentence names it (§8.2). */
  fileSizeInBytes?: number;
  /** For `FileTypeNotAllowed`/`FileContentMismatch` — the file's declared MIME type. */
  fileContentType?: string;
  purpose: FilePurpose;
}

/**
 * HO-FIL-002 §8.2 (ADR-FE-151) — every violation code becomes a sentence a person can act on. The
 * server's own `errorMessage` is never shown (§7 rule 3): `FileUploadValidationError.message`
 * carries it only for logs, and the caller of this function never reads it.
 */
export function describeFileError(input: FileErrorInput): string {
  switch (input.code) {
    case 'FileTooLarge': {
      const max = formatFileSize(findFileRule(input.purpose).maxSizeInBytes);
      const size =
        input.fileSizeInBytes !== undefined ? formatFileSize(input.fileSizeInBytes) : 'too large';
      return `That file is ${size}. The largest allowed here is ${max}.`;
    }
    case 'FileTypeNotAllowed': {
      const type = input.fileContentType
        ? humanizeContentType(input.fileContentType)
        : 'an unsupported';
      const list = describeAllowedTypes(input.purpose);
      return `${input.fileName} is a ${type} file. You can upload ${list}.`;
    }
    case 'FileContentMismatch': {
      const type = input.fileContentType
        ? humanizeContentType(input.fileContentType)
        : 'the declared';
      return `${input.fileName} does not look like a ${type} file. It may be renamed or damaged.`;
    }
    case 'FileRejected':
      return `${input.fileName} was rejected by the virus scanner and has not been saved.`;
    case 'FileScanUnavailable':
      return 'The virus scanner is unavailable, so files cannot be uploaded right now. Please try again shortly.';
    case 'FilePurposeNotAllowedForCaller':
      return `${input.fileName} could not be uploaded — you're not permitted to upload this kind of file here.`;
    case 'FilesInvalidPurpose':
      return `${input.fileName} could not be uploaded — this screen isn't set up correctly. Tell an administrator.`;
    case 'NetworkFailure':
      return `${input.fileName} did not finish uploading.`;
    case 'UnknownViolation':
      return `${input.fileName} could not be uploaded. Please try again or choose a different file.`;
    default: {
      const exhaustive: never = input.code;
      return exhaustive;
    }
  }
}

/** OQ-2: manual retry only. `FileScanUnavailable` is a service state, not the person's mistake
 *  (ADR-FE-151/A4) — the file stays queued and Retry is offered (C4). A transport failure is
 *  retryable for the same "not the person's fault" reason (§8.2's own "network / abort" row).
 *  Every other code describes something wrong with the file itself, which retrying cannot fix. */
export function isRetryableFileError(code: FileQueueErrorCode): boolean {
  return code === 'FileScanUnavailable' || code === 'NetworkFailure';
}

/** OQ-1's recorded default: a scanner-rejected file is removed once its message has been
 *  announced, not left as a lingering red row — a rejected file was never stored, so leaving a
 *  row implies otherwise. */
export function isAutoRemovedFileError(code: FileQueueErrorCode): boolean {
  return code === 'FileRejected';
}
