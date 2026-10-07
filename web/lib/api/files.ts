import { getApiUrl, notifySessionError, type SessionErrorBody } from './client';
import { getActiveBranchId } from '../branch';

/**
 * File storage upload contract (HO-FIL-001 §12.4 — this is HO-FIL-002's own pairing contract).
 * Read side (`GET /api/files/{fileId}/content`) is consumed indirectly: every consuming area's
 * own read already returns a signed `url`/`urlExpiresOn` pair per this same shape (§12.4), and
 * this slice never constructs or re-signs that URL itself (HO-FIL-001 §14, ADR-318) — it only
 * fetches a *new* one by asking the host to re-run its own read (ADR-FE-154), via a callback the
 * host supplies. `MediaViewer`/`FileThumb` (`components/files/`) hold that behaviour; nothing
 * here talks to `GET .../content` directly.
 *
 * `FilePurpose` mirrors `FilePurpose.cs` (`src/Shared/.../Enums/FilePurpose.cs`) — every member on
 * the backend today, `CatProfilePhoto` included (added there under integrator authorisation,
 * HO-FIL-001 §1 Amended 2026-09-26). Listing it here does not enable the Cat Profiles photo page
 * to consume this slice by itself — see this handover's own §1 Amended row on the cat-photo page
 * for what is and is not done.
 */
export type FilePurpose =
  | 'LabResult'
  | 'ClinicalPhoto'
  | 'ConsentForm'
  | 'ProductImage'
  | 'BoardingMedia'
  | 'BoardingBelongings'
  | 'ReportExport'
  | 'CatProfilePhoto';

/** Which upload route a caller uses — `POST /api/staff/files` or `POST /api/customers/me/files`
 *  (HO-FIL-001 §12). The purpose-access check differs per route; this slice never guesses which
 *  one to call — the host names it. */
export type FileUploadAudience = 'staff' | 'customer';

/** HO-FIL-001 §12.4's signed-file view model. Every consuming area's read returns this same
 *  shape for a file; the upload response (`201`) is also exactly this shape (`FilesController`'s
 *  `RegisterStoredFileCommandHandler` returns its own `Result` directly, gate-fix 2026-09-27). */
export interface StoredFileViewModel {
  fileId: string;
  url: string;
  contentType: string;
  sizeInBytes: number;
  originalFileName: string;
  /** ISO 8601. HO-FIL-001 §8.6 — 5 minutes from when the URL was signed. ADR-FE-154. */
  urlExpiresOn: string;
}

/** HO-FIL-001 §10.2's violation codes this slice ever needs to map to a sentence (§8.2). The two
 *  purpose-related codes (`FilePurposeNotAllowedForCaller`, `FilesInvalidPurpose`) are host
 *  configuration errors, not something a person caused by picking a file — `fileErrors.ts` maps
 *  them too, so a misconfigured purpose never reaches the UI as an unhandled exception. */
export type FileServerViolationCode =
  | 'FileTooLarge'
  | 'FileTypeNotAllowed'
  | 'FileContentMismatch'
  | 'FileRejected'
  | 'FileScanUnavailable'
  | 'FilePurposeNotAllowedForCaller'
  | 'FilesInvalidPurpose';

/** Every code this module will ever construct a `FileUploadValidationError` for. Gate-fix
 *  2026-09-28 (SECURITY LOW) — the server's `errorCode` used to be cast to `FileServerViolationCode`
 *  with no runtime check, so an unrecognised code (a FluentValidation default such as
 *  `MaximumLengthValidator` for a 256+ character filename, or any future addition to
 *  `BusinessViolationCodes` this slice hasn't been updated for) would be shown and announced to the
 *  user verbatim, unmapped. Anything not in this set becomes `'UnknownViolation'` instead — see
 *  `isKnownViolationCode` below. */
const KNOWN_VIOLATION_CODES: ReadonlySet<FileServerViolationCode> = new Set([
  'FileTooLarge',
  'FileTypeNotAllowed',
  'FileContentMismatch',
  'FileRejected',
  'FileScanUnavailable',
  'FilePurposeNotAllowedForCaller',
  'FilesInvalidPurpose',
]);

function isKnownViolationCode(code: string): code is FileServerViolationCode {
  return (KNOWN_VIOLATION_CODES as ReadonlySet<string>).has(code);
}

function tryParseJson<T>(text: string): T | null {
  try {
    return JSON.parse(text) as T;
  } catch {
    return null;
  }
}

/** The first `ValidationResponse` error, with its code checked against `KNOWN_VIOLATION_CODES` —
 *  `null` if the array is missing/empty. Shared by every status code that can carry this shape
 *  (`400`, `403`, `413`, `502`, …). */
function firstKnownOrUnknownViolation(
  errors: Array<{ errorCode: string; errorMessage: string }> | undefined,
): { code: FileServerViolationCode | 'UnknownViolation'; message: string } | null {
  const first = errors?.[0];
  if (!first) return null;
  return {
    code: isKnownViolationCode(first.errorCode) ? first.errorCode : 'UnknownViolation',
    message: first.errorMessage,
  };
}

/** A `400`/`403` from the upload endpoint, carrying the violation code — never the raw server
 *  message (§7 rule 3; the sentence a person sees comes from `fileErrors.ts`'s own table, not
 *  this field). `'UnknownViolation'` (gate-fix 2026-09-28, SECURITY LOW) stands in for any
 *  `errorCode` this module does not recognise — see `KNOWN_VIOLATION_CODES` above. */
export class FileUploadValidationError extends Error {
  readonly code: FileServerViolationCode | 'UnknownViolation';
  constructor(code: FileServerViolationCode | 'UnknownViolation', serverMessage: string) {
    super(serverMessage);
    this.name = 'FileUploadValidationError';
    this.code = code;
  }
}

/** `401` — the caller's session is no longer valid (idle timeout or otherwise). Gate-fix
 *  2026-09-28 (ARCHITECTURE MEDIUM): `uploadFile` now routes this through the exact same
 *  `notifySessionError`/`sessionErrorHandler` path `lib/api/client.ts`'s `apiFetch` uses, so a
 *  session that expires mid-upload reaches the HO-ACC-002 idle-lock/sign-in surface instead of
 *  silently reading as a plain "did not finish uploading". */
export class FileUploadSessionError extends Error {
  readonly body: SessionErrorBody;
  constructor(body: SessionErrorBody) {
    super('Session is not valid.');
    this.name = 'FileUploadSessionError';
    this.body = body;
  }
}

/** `403` with `{ error: 'csrf_token_invalid' }` (HO-ACC-001 R6/R7's CSRF refusal — the same shape
 *  `client.ts`'s `ApiForbiddenError` carries) — kept distinct from `FileUploadValidationError`
 *  (a `403` `FilePurposeNotAllowedForCaller` from `FilesController` itself, a genuine
 *  `ValidationResponse`) and from `FileUploadNetworkError` (gate-fix 2026-09-28, ARCHITECTURE
 *  MEDIUM: this used to fall through to the generic network error, indistinguishable from a
 *  dropped connection). */
export class FileUploadForbiddenError extends Error {
  constructor() {
    super('You are not permitted to perform this action.');
    this.name = 'FileUploadForbiddenError';
  }
}

/** `429` — HO-FIL-001 §12.7's `FileRateLimitPolicies.FileUpload`. */
export class FileUploadRateLimitedError extends Error {
  readonly retryAfterSeconds: number;
  constructor(retryAfterSeconds: number) {
    super('Too many uploads. Please wait and try again.');
    this.name = 'FileUploadRateLimitedError';
    this.retryAfterSeconds = retryAfterSeconds;
  }
}

/** The caller's own `AbortController.abort()` fired — a deliberate cancel, not a network fault.
 *  `useFileUpload.ts` relies on this being a distinct type from every other failure so a cancel
 *  removes the row (§8.2/C5) instead of showing an error (§8.2's "network / abort" row is a
 *  genuine transport failure, not this). */
export class FileUploadAbortedError extends Error {
  constructor() {
    super('Upload was cancelled.');
    this.name = 'FileUploadAbortedError';
  }
}

/** A transport failure — dropped connection, DNS failure, CORS rejection, or any non-2xx/4xx
 *  status this module does not otherwise recognise. Mapped to the §8.2 "did not finish
 *  uploading" sentence, with Retry offered (OQ-2). */
export class FileUploadNetworkError extends Error {
  constructor() {
    super('Could not reach the server.');
    this.name = 'FileUploadNetworkError';
  }
}

export interface UploadFileParams {
  file: File;
  purpose: FilePurpose;
  audience: FileUploadAudience;
  /** Fraction, `0`–`1`. ADR-FE-153: this is a **visual** signal only — never read out by the
   *  aria-live region (`useFileUpload.ts` never turns a progress tick into an announcement). */
  onProgress?: (fraction: number) => void;
  signal?: AbortSignal;
}

function readCookie(name: string): string | undefined {
  if (typeof document === 'undefined') return undefined;
  const match = document.cookie.split('; ').find((row) => row.startsWith(`${name}=`));
  return match ? decodeURIComponent(match.slice(name.length + 1)) : undefined;
}

/**
 * `uploadFile` — ADR-FE-150's documented exception to `lib/api/client.ts`'s "the ONLY place in
 * the app that calls `fetch`" rule (HO-CAT-002 §8.2 rule 2): that rule is about `fetch`
 * specifically, and progress reporting during an upload has no `fetch` equivalent in any browser
 * this product targets (`fetch`'s `ReadableStream` request body has no standardised progress
 * event; `XMLHttpRequest.upload.onprogress` is the only platform primitive that has one). This
 * function is the one place in the app that constructs an `XMLHttpRequest`, kept in `lib/api/`
 * (HO-FIL-002 §7/§11) rather than in `components/files/` or `lib/hooks/`, exactly like every
 * other area keeps its endpoint functions in `lib/api/<area>.ts` — `useFileUpload`
 * (`lib/hooks/useFileUpload.ts`) is the only caller.
 *
 * Duplicates a small, deliberately minimal slice of `client.ts`'s own request wiring — the CSRF
 * cookie echo and the `X-Branch-Id` header — because `XMLHttpRequest` cannot share `fetch`'s
 * plumbing. `credentials: 'include'`'s XHR equivalent is `withCredentials = true`.
 */
export function uploadFile({
  file,
  purpose,
  audience,
  onProgress,
  signal,
}: UploadFileParams): Promise<StoredFileViewModel> {
  return new Promise((resolve, reject) => {
    const path =
      audience === 'staff'
        ? `/api/staff/files?purpose=${encodeURIComponent(purpose)}`
        : `/api/customers/me/files?purpose=${encodeURIComponent(purpose)}`;

    const xhr = new XMLHttpRequest();
    xhr.open('POST', getApiUrl(path));
    xhr.withCredentials = true;

    const csrfToken = readCookie('pc_csrf');
    if (csrfToken) xhr.setRequestHeader('X-CSRF-Token', csrfToken);
    const branchId = getActiveBranchId();
    if (branchId) xhr.setRequestHeader('X-Branch-Id', branchId);
    xhr.setRequestHeader('Accept', 'application/json');

    if (signal) {
      if (signal.aborted) {
        reject(new FileUploadAbortedError());
        return;
      }
      signal.addEventListener('abort', () => xhr.abort(), { once: true });
    }

    xhr.upload.onprogress = (event) => {
      if (!onProgress || !event.lengthComputable) return;
      onProgress(event.loaded / event.total);
    };

    xhr.onabort = () => reject(new FileUploadAbortedError());
    xhr.onerror = () => reject(new FileUploadNetworkError());
    xhr.ontimeout = () => reject(new FileUploadNetworkError());

    xhr.onload = () => {
      const status = xhr.status;
      if (status === 200 || status === 201) {
        try {
          resolve(JSON.parse(xhr.responseText) as StoredFileViewModel);
        } catch {
          reject(new FileUploadNetworkError());
        }
        return;
      }

      // Gate-fix 2026-09-28 (ARCHITECTURE MEDIUM) — routed through the same session-error handler
      // `apiFetch` uses (`client.ts`'s `notifySessionError`), not folded into a generic network
      // failure. A file-upload `401` is always the session shape, never `invalid_credentials` (that
      // shape is sign-in-endpoint-only), so no check for it is needed here the way `client.ts` has
      // to for its own, more general callers.
      if (status === 401) {
        let body: SessionErrorBody = { error: 'session_invalid' };
        try {
          const parsed = JSON.parse(xhr.responseText) as Partial<SessionErrorBody> | null;
          if (parsed?.error === 'session_invalid' || parsed?.error === 'session_idle_timeout') {
            body = parsed as SessionErrorBody;
          }
        } catch {
          // keep the default session_invalid body — an unparsable 401 is still a session failure
        }
        notifySessionError(body);
        reject(new FileUploadSessionError(body));
        return;
      }

      // Gate-fix 2026-09-28 (ARCHITECTURE MEDIUM) — a `403` is either the CSRF refusal (`{ error:
      // 'csrf_token_invalid' }`, HO-ACC-001 R6/R7 — the SessionAuthenticationMiddleware, before the
      // request ever reaches FilesController) or FilesController's own `FilePurposeNotAllowedForCaller`
      // (a genuine `ValidationResponse`). Neither used to be distinguished from a dropped connection.
      if (status === 403) {
        const parsedBody = tryParseJson<{
          error?: string;
          errors?: Array<{ errorCode: string; errorMessage: string }>;
        }>(xhr.responseText);

        if (parsedBody?.error === 'csrf_token_invalid') {
          reject(new FileUploadForbiddenError());
          return;
        }

        const violation = firstKnownOrUnknownViolation(parsedBody?.errors);
        if (violation) {
          reject(new FileUploadValidationError(violation.code, violation.message));
          return;
        }

        reject(new FileUploadForbiddenError());
        return;
      }

      if (status === 429) {
        const retryAfterHeader = xhr.getResponseHeader('Retry-After');
        const retryAfterSeconds = retryAfterHeader ? Number(retryAfterHeader) : 60;
        reject(
          new FileUploadRateLimitedError(
            Number.isFinite(retryAfterSeconds) ? retryAfterSeconds : 60,
          ),
        );
        return;
      }

      // Gate-fix 2026-09-28 (ARCHITECTURE LOW) — `400`, and every OTHER status this module doesn't
      // special-case above (`FilesController` also answers `413` for an over-size/malformed
      // multipart body and `502` for a store failure, both via the same `Violation(...)` helper as
      // `400` — `FilesController.cs`), all carry the identical `ValidationResponse` shape. Trying to
      // parse that shape regardless of the exact status code means a `413`/`502` keeps its own
      // §8.2 sentence instead of reading as a generic "did not finish uploading".
      const body = tryParseJson<{ errors?: Array<{ errorCode: string; errorMessage: string }> }>(
        xhr.responseText,
      );
      const violation = firstKnownOrUnknownViolation(body?.errors);
      if (violation) {
        reject(new FileUploadValidationError(violation.code, violation.message));
        return;
      }

      reject(new FileUploadNetworkError());
    };

    const formData = new FormData();
    formData.append('file', file, file.name);
    xhr.send(formData);
  });
}
