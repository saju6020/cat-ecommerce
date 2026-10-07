import { http, HttpResponse, type HttpHandler } from 'msw';
import { resolveMockUploadOutcome } from './uploadBehavior';
import { fileStorageFixtureIds, onePixelPngBytes } from './fixtures';

/**
 * File Storage MSW request handlers (HO-FIL-002 §12), derived from HO-FIL-001 §12.4 as verified
 * against `FilesController`/`RegisterStoredFileCommandHandler` in `src/` (never invented —
 * HO-CAT-002 §5.1, §20.2): `POST /api/staff/files`, `POST /api/customers/me/files` (`201` with the
 * signed-file shape, `400` with a `ValidationResponse` whose `errors[0].errorCode` is one of
 * HO-FIL-001 §10.2's codes, `403` for `FilePurposeNotAllowedForCaller`) and
 * `GET /api/files/{fileId}/content` (a real, tiny image so a genuine `<img>` in a Playwright
 * browser has something to load or fail against).
 *
 * The per-filename outcome table (success, each violation code, the slow-upload gate) lives in
 * `uploadBehavior.ts`, shared with the Vitest-side fake-XHR harness (`tests/utils/fakeXhr.ts`) —
 * see that module's own doc comment for why Vitest cannot go through this real MSW handler.
 * "Aborted" needs no handler support at all: the browser's own `AbortController`/
 * `XMLHttpRequest.abort()` (`lib/api/files.ts`) cancels the request before any handler runs.
 */
async function handleUpload(request: Request): Promise<Response> {
  const formData = await request.formData();
  const file = formData.get('file');
  if (!(file instanceof File)) {
    return HttpResponse.json(
      {
        errors: [
          {
            propertyName: 'Request',
            errorCode: 'FileRejected',
            errorMessage: 'No file part was found in the request.',
            resourceName: 'StoredFile',
          },
        ],
        isValid: false,
      },
      { status: 400 },
    );
  }

  // The request's own origin, not `lib/api/client.ts`'s `getApiUrl` — see fixtures.ts's
  // `mockApiUrl` doc comment for why this handler module avoids that import (it would otherwise
  // be pulled in eagerly for every test via `tests/setup.ts` → `mocks/server.ts`).
  const origin = new URL(request.url).origin;
  const outcome = await resolveMockUploadOutcome(
    file,
    (fileId, exp) => `${origin}/api/files/${fileId}/content?exp=${exp}&disp=inline&sig=mock`,
  );
  return HttpResponse.json(outcome.body, { status: outcome.status });
}

export const fileStorageHandlers: HttpHandler[] = [
  http.post('*/api/staff/files', ({ request }) => handleUpload(request)),
  http.post('*/api/customers/me/files', ({ request }) => handleUpload(request)),

  // §8.6 — the signature is the authorisation; this mock only needs to reproduce the two outcomes
  // this slice's own components react to: a servable image, and a refused (expired/tampered) URL.
  http.get('*/api/files/:fileId/content', ({ params, request }) => {
    if (params.fileId === fileStorageFixtureIds.expiredImage) {
      return HttpResponse.json({}, { status: 403 });
    }

    const url = new URL(request.url);
    const exp = Number(url.searchParams.get('exp'));
    if (Number.isFinite(exp) && exp > 0 && exp < Math.floor(Date.now() / 1000)) {
      return HttpResponse.json({}, { status: 403 });
    }

    return new HttpResponse(onePixelPngBytes(), {
      status: 200,
      headers: { 'Content-Type': 'image/png' },
    });
  }),
];
