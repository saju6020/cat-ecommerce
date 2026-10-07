import { afterEach, describe, expect, it, vi } from 'vitest';
import { setSessionErrorHandler } from '@/lib/api/client';
import {
  FileUploadForbiddenError,
  FileUploadSessionError,
  FileUploadValidationError,
  uploadFile,
} from '@/lib/api/files';
import { installFakeXhr, restoreRealXhr } from '../../utils/fakeXhr';

/**
 * Gate-fix 2026-09-28 (ARCHITECTURE MEDIUM / SECURITY LOW) — `uploadFile`'s own response-status
 * handling: a `401` now reaches the same session-error surface `client.ts`'s `apiFetch` uses, a
 * CSRF `403` is distinguishable from both a purpose-refusal `403` and a generic network failure,
 * an unrecognised `errorCode` is never shown verbatim, and a `413`/`502` keeps its own violation
 * code instead of reading as a generic "did not finish uploading".
 */
function makeFile(name = 'photo.png'): File {
  return new File([new Uint8Array(4)], name, { type: 'image/png' });
}

afterEach(() => {
  restoreRealXhr();
  setSessionErrorHandler(null);
});

describe('uploadFile — 401 session routing', () => {
  it('notifies the registered session-error handler and rejects with FileUploadSessionError', async () => {
    installFakeXhr({
      respond: () => ({
        status: 401,
        body: { error: 'session_idle_timeout', idleTimeoutMinutes: 30 },
      }),
    });
    const handler = vi.fn();
    setSessionErrorHandler(handler);

    const error = await uploadFile({
      file: makeFile(),
      purpose: 'ProductImage',
      audience: 'staff',
    }).catch((e: unknown) => e);

    expect(handler).toHaveBeenCalledExactlyOnceWith({
      error: 'session_idle_timeout',
      idleTimeoutMinutes: 30,
    });
    expect(error).toBeInstanceOf(FileUploadSessionError);
    expect((error as FileUploadSessionError).body.error).toBe('session_idle_timeout');
  });

  it('still notifies the handler for an unparsable 401 body, defaulting to session_invalid', async () => {
    installFakeXhr({ respond: () => ({ status: 401, body: '' }) });
    const handler = vi.fn();
    setSessionErrorHandler(handler);

    await uploadFile({ file: makeFile(), purpose: 'ProductImage', audience: 'staff' }).catch(
      () => {},
    );

    expect(handler).toHaveBeenCalledExactlyOnceWith({ error: 'session_invalid' });
  });
});

describe('uploadFile — 403 routing', () => {
  it('a CSRF refusal rejects with FileUploadForbiddenError, distinct from a validation or network error', async () => {
    installFakeXhr({ respond: () => ({ status: 403, body: { error: 'csrf_token_invalid' } }) });

    const error = await uploadFile({
      file: makeFile(),
      purpose: 'ProductImage',
      audience: 'staff',
    }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(FileUploadForbiddenError);
  });

  it('FilePurposeNotAllowedForCaller (a genuine ValidationResponse) still rejects with FileUploadValidationError', async () => {
    installFakeXhr({
      respond: () => ({
        status: 403,
        body: {
          errors: [
            {
              propertyName: 'Purpose',
              errorCode: 'FilePurposeNotAllowedForCaller',
              errorMessage: 'not allowed',
              resourceName: 'StoredFile',
            },
          ],
          isValid: false,
        },
      }),
    });

    const error = await uploadFile({
      file: makeFile(),
      purpose: 'ProductImage',
      audience: 'staff',
    }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(FileUploadValidationError);
    expect((error as FileUploadValidationError).code).toBe('FilePurposeNotAllowedForCaller');
  });
});

describe('uploadFile — unrecognised errorCode (SECURITY LOW)', () => {
  it('maps an unknown code to UnknownViolation rather than the raw server code', async () => {
    installFakeXhr({
      respond: () => ({
        status: 400,
        body: {
          errors: [
            {
              propertyName: 'OriginalFileName',
              errorCode: 'MaximumLengthValidator',
              errorMessage: "'Original File Name' must be 255 characters or fewer.",
              resourceName: 'StoredFile',
            },
          ],
          isValid: false,
        },
      }),
    });

    const error = await uploadFile({
      file: makeFile(),
      purpose: 'ProductImage',
      audience: 'staff',
    }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(FileUploadValidationError);
    expect((error as FileUploadValidationError).code).toBe('UnknownViolation');
  });
});

describe('uploadFile — 413/502 keep their own violation code (ARCHITECTURE LOW)', () => {
  it('a 413 with FileTooLarge is a FileUploadValidationError, not a generic network failure', async () => {
    installFakeXhr({
      respond: () => ({
        status: 413,
        body: {
          errors: [
            {
              propertyName: 'Content',
              errorCode: 'FileTooLarge',
              errorMessage: 'The request body is too large or malformed.',
              resourceName: 'StoredFile',
            },
          ],
          isValid: false,
        },
      }),
    });

    const error = await uploadFile({
      file: makeFile(),
      purpose: 'ProductImage',
      audience: 'staff',
    }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(FileUploadValidationError);
    expect((error as FileUploadValidationError).code).toBe('FileTooLarge');
  });

  it('a 502 with FileRejected is a FileUploadValidationError, not a generic network failure', async () => {
    installFakeXhr({
      respond: () => ({
        status: 502,
        body: {
          errors: [
            {
              propertyName: 'Content',
              errorCode: 'FileRejected',
              errorMessage: 'Failed to store the file.',
              resourceName: 'StoredFile',
            },
          ],
          isValid: false,
        },
      }),
    });

    const error = await uploadFile({
      file: makeFile(),
      purpose: 'ProductImage',
      audience: 'staff',
    }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(FileUploadValidationError);
    expect((error as FileUploadValidationError).code).toBe('FileRejected');
  });
});
