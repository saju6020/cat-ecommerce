import {
  ApiConflictError,
  ApiError,
  ApiForbiddenError,
  ApiNotFoundError,
  ApiRateLimitedError,
  ApiUnauthorizedError,
  ApiValidationError,
  getApiUrl,
  notifySessionError,
  type ForbiddenBody,
  type SessionErrorBody,
  type SignInFailedBody,
} from '@/lib/api/client';
import type { ValidationResponse } from '@/lib/api/types';
import { getActiveBranchId } from '@/lib/branch';
import { saveBlob } from './saveBlob';

/**
 * `lib/download/reportExport.ts` — HO-RPT-002 ADR-FE-132. A report export is FETCHED as a blob (not
 * navigated to), so a refusal — `413 ReportTooLargeForPdf`, `400 ReportTimedOut`, a `403`, a `429`
 * with `Retry-After` — arrives as a typed error the page can explain instead of a raw JSON page.
 *
 * Like `lib/api/inventory-download.ts` it uses `XMLHttpRequest`, so `client.ts` stays the only
 * `fetch` call site (HO-CAT-002 §8.2 rule 2); it sends the same identity headers `apiFetch` does
 * (session cookie, `X-Branch-Id`). An export is a `GET`, so no CSRF header is needed (HO-RPT-001
 * §12.4).
 */
function parseJson<T>(text: string): T | null {
  try {
    return JSON.parse(text) as T;
  } catch {
    return null;
  }
}

/** A server filename reduced to a harmless basename; `null` when it is absent, malformed or empty. */
export function safeFilename(raw: string | undefined): string | null {
  if (!raw) return null;
  let decoded: string;
  try {
    decoded = decodeURIComponent(raw);
  } catch {
    return null;
  }
  const base = decoded.split(/[\\/]/).pop() ?? '';
  const cleaned = base.replace(/[^A-Za-z0-9._-]/g, '').replace(/^\.+/, '');
  return cleaned || null;
}

export interface ExportedFile {
  blob: Blob;
  filename: string | null;
}

/** Requests `path` as a blob and maps every non-200 onto `client.ts`'s error classes. */
export function requestReportFile(path: string): Promise<ExportedFile> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open('GET', getApiUrl(path));
    xhr.withCredentials = true;
    xhr.responseType = 'blob';
    const branchId = getActiveBranchId();
    if (branchId) xhr.setRequestHeader('X-Branch-Id', branchId);

    xhr.onerror = () => reject(new ApiError('Could not reach the server. Please try again.', 0));
    xhr.ontimeout = xhr.onerror;
    xhr.onload = async () => {
      const status = xhr.status;
      const blob = xhr.response as Blob;
      if (status === 200) {
        const disposition = xhr.getResponseHeader('Content-Disposition') ?? '';
        const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
        resolve({ blob, filename: safeFilename(match?.[1]) });
        return;
      }
      const text = await blob.text().catch(() => '');
      const validation = parseJson<ValidationResponse>(text);
      const hasErrors = !!validation && Array.isArray(validation.errors);
      if (status === 400 || status === 413) {
        reject(
          hasErrors
            ? new ApiValidationError(validation)
            : new ApiError('Something went wrong. Please try again.', status),
        );
      } else if (status === 401) {
        const body =
          parseJson<SignInFailedBody | SessionErrorBody>(text) ??
          ({ error: 'session_invalid' } as const);
        if (body.error !== 'invalid_credentials') notifySessionError(body);
        reject(new ApiUnauthorizedError(body));
      } else if (status === 403) {
        reject(new ApiForbiddenError(parseJson<ForbiddenBody>(text) ?? {}));
      } else if (status === 404) {
        reject(new ApiNotFoundError());
      } else if (status === 409) {
        reject(new ApiConflictError(hasErrors ? validation : undefined));
      } else if (status === 429) {
        const retryAfter = Number(xhr.getResponseHeader('Retry-After'));
        reject(
          new ApiRateLimitedError(
            Number.isFinite(retryAfter) && retryAfter > 0 ? retryAfter : 60,
            hasErrors ? validation : undefined,
          ),
        );
      } else {
        reject(new ApiError('Something went wrong. Please try again.', status));
      }
    };
    xhr.send(null);
  });
}

/** Fetches an export and saves it under the server's filename (or `fallbackName`). */
export async function downloadReportExport(path: string, fallbackName: string): Promise<void> {
  const { blob, filename } = await requestReportFile(path);
  saveBlob(blob, filename ?? fallbackName);
}
