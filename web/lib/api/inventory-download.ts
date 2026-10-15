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
} from './client';
import type { ValidationResponse } from './types';
import { getActiveBranchId } from '../branch';

/**
 * `lib/api/inventory-download.ts` — HO-IMP-002 §3.1, ARCHITECTURE gate fix round 1. The ledger,
 * near-expiry and stock-on-hand CSVs are branch-scoped, and an anchor cannot send `X-Branch-Id`, so
 * a plain link would hand a member of staff at a non-primary branch the PRIMARY branch's data. This
 * downloads with the same headers `apiFetch` sends (CSRF, `X-Branch-Id`), maps every status onto the
 * same error classes, and saves the file the SERVER produced (`URL.createObjectURL` + a `download`
 * anchor, revoked afterwards) — nothing is generated in the browser. Like `lib/api/files.ts` it uses
 * `XMLHttpRequest`, so `client.ts` stays the only `fetch` call site.
 */
function readCsrfToken(): string | undefined {
  if (typeof document === 'undefined') return undefined;
  const match = document.cookie.split('; ').find((row) => row.startsWith('pc_csrf='));
  return match ? decodeURIComponent(match.slice('pc_csrf='.length)) : undefined;
}

function parseJson<T>(text: string): T | null {
  try {
    return JSON.parse(text) as T;
  } catch {
    return null;
  }
}

export interface DownloadedFile {
  blob: Blob;
  filename: string | null;
}

export function requestFile(path: string): Promise<DownloadedFile> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open('GET', getApiUrl(path));
    xhr.withCredentials = true;
    xhr.responseType = 'blob';
    const csrf = readCsrfToken();
    if (csrf) xhr.setRequestHeader('X-CSRF-Token', csrf);
    const branchId = getActiveBranchId();
    if (branchId) xhr.setRequestHeader('X-Branch-Id', branchId);

    xhr.onerror = () => reject(new ApiError('Could not reach the server. Please try again.', 0));
    xhr.ontimeout = xhr.onerror;
    xhr.onload = async () => {
      const status = xhr.status;
      const blob = xhr.response as Blob;
      if (status === 200) {
        const disposition = xhr.getResponseHeader('Content-Disposition') ?? '';
        const match = /filename="?([^";]+)"?/i.exec(disposition);
        resolve({ blob, filename: match?.[1] ?? null });
        return;
      }
      const text = await blob.text().catch(() => '');
      if (status === 400 || status === 413) {
        const body = parseJson<ValidationResponse>(text);
        reject(
          body && Array.isArray(body.errors)
            ? new ApiValidationError(body)
            : new ApiError('Something went wrong. Please try again.', status),
        );
      } else if (status === 401) {
        const body =
          parseJson<SignInFailedBody | SessionErrorBody>(text) ?? ({ error: 'session_invalid' } as const);
        if (body.error !== 'invalid_credentials') notifySessionError(body);
        reject(new ApiUnauthorizedError(body));
      } else if (status === 403) {
        reject(new ApiForbiddenError(parseJson<ForbiddenBody>(text) ?? {}));
      } else if (status === 404) {
        reject(new ApiNotFoundError());
      } else if (status === 409) {
        const body = parseJson<ValidationResponse>(text);
        reject(new ApiConflictError(body && Array.isArray(body.errors) ? body : undefined));
      } else if (status === 429) {
        reject(new ApiRateLimitedError(60));
      } else {
        reject(new ApiError('Something went wrong. Please try again.', status));
      }
    };
    xhr.send(null);
  });
}

/** Fetches a branch-scoped file and hands the browser a saved copy. Rejects with `apiFetch`'s errors
 *  (a `413` asking for a narrower range arrives as an `ApiValidationError` or `ApiError`). */
export async function downloadInventoryFile(path: string, fallbackName: string): Promise<void> {
  const { blob, filename } = await requestFile(path);
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename ?? fallbackName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}
