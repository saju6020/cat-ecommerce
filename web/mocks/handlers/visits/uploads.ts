import { http, HttpResponse, type HttpHandler } from 'msw';
import { resolveMockUploadOutcome } from '../files/uploadBehavior';
import { rememberFile } from './store';

/**
 * `mocks/handlers/visits/uploads.ts` — HO-EMR-002 §12. The clinical screens attach an uploaded file
 * by id and the server answers with its name, type and size, so the mock has to REMEMBER what it
 * accepted. This is the File Storage mock's own `POST /api/staff/files` outcome table
 * (`resolveMockUploadOutcome`: success, each violation code incl. `FileScanUnavailable`, the slow
 * gate) plus that one registry — registered before `fileStorageHandlers`, so it wins for staff
 * uploads; `GET /api/files/{id}/content` and the customer upload stay the File Storage mock's.
 */
export const clinicalUploadHandlers: HttpHandler[] = [
  http.post('*/api/staff/files', async ({ request }) => {
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
    const origin = new URL(request.url).origin;
    const outcome = await resolveMockUploadOutcome(
      file,
      (fileId, exp) => `${origin}/api/files/${fileId}/content?exp=${exp}&disp=inline&sig=mock`,
    );
    if (outcome.status === 201 || outcome.status === 200) {
      rememberFile(String(outcome.body.fileId), {
        name: file.name,
        type: file.type || 'application/octet-stream',
        size: file.size,
      });
    }
    return HttpResponse.json(outcome.body, { status: outcome.status });
  }),
];
