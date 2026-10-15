import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { getApiUrl } from '@/lib/api/client';
import { getCat } from '@/lib/api/cats';
import { getVisit } from '@/lib/api/visits';
import { getConsent } from '@/lib/api/consents';
import { getDiagnosticOrder } from '@/lib/api/diagnostics';
import { getHistorySection } from '@/lib/api/clinical-history';
import { uploadFile } from '@/lib/api/files';
import { installFakeXhr, restoreRealXhr } from '../../utils/fakeXhr';

/**
 * FIL backlog 25 -- every reader that returns a server-signed file url maps a RELATIVE
 * `/api/files/{id}/content?...` to an absolute API-origin url (and an unusable one to empty/null),
 * so no component has to. Relative in -> absolute out, per reader.
 */
const REL = '/api/files/9f1c/content?exp=1999999999&disp=inline&sig=abc';
const file = (url: string) => ({
  fileId: '9f1c',
  url,
  contentType: 'image/png',
  sizeInBytes: 10,
  originalFileName: 'a.png',
  urlExpiresOn: '2999-01-01T00:00:00.000Z',
});

describe('signed file urls are anchored at the mapping layer', () => {
  it('getCat: photos[].url', async () => {
    server.use(
      http.get('*/api/cats/:id', () =>
        HttpResponse.json({
          photos: [
            { id: 'p1', ...file(REL) },
            { id: 'p2', ...file('//evil.test/x.png') },
          ],
        }),
      ),
    );
    const cat = await getCat('c1');
    expect(cat.photos[0]!.url).toBe(getApiUrl(REL));
    expect(cat.photos[1]!.url).toBe('');
  });

  it('getVisit: photos[].url and thumbnailUrl', async () => {
    server.use(
      http.get('*/api/staff/visits/:id', () =>
        HttpResponse.json({
          photos: [
            { id: 'p1', url: REL, thumbnailUrl: REL, contentType: 'image/png' },
            { id: 'p2', url: null, thumbnailUrl: null, contentType: null },
            { id: 'p3', url: 'javascript:alert(1)', thumbnailUrl: '//evil.test/t', contentType: null },
          ],
        }),
      ),
    );
    const visit = await getVisit('v1');
    expect(visit.photos[0]).toMatchObject({ url: getApiUrl(REL), thumbnailUrl: getApiUrl(REL) });
    expect(visit.photos[1]).toMatchObject({ url: null, thumbnailUrl: null });
    expect(visit.photos[2]).toMatchObject({ url: null, thumbnailUrl: null });
  });

  it('getConsent: file.url (and a null file stays null)', async () => {
    server.use(
      http.get('*/api/staff/consents/:id', ({ params }) =>
        HttpResponse.json({
          id: params.id,
          file: params.id === 'with' ? file(REL) : null,
        }),
      ),
    );
    expect((await getConsent('with')).file?.url).toBe(getApiUrl(REL));
    expect((await getConsent('none')).file).toBeNull();
  });

  it('getDiagnosticOrder: attachments[].file.url', async () => {
    server.use(
      http.get('*/api/staff/diagnostics/:id', () =>
        HttpResponse.json({
          resultSets: [],
          attachments: [
            { id: 'a1', file: file(REL) },
            { id: 'a2', file: null },
          ],
        }),
      ),
    );
    const order = await getDiagnosticOrder('d1');
    expect(order.attachments[0]!.file?.url).toBe(getApiUrl(REL));
    expect(order.attachments[1]!.file).toBeNull();
  });

  it('getHistorySection: items[].file.url', async () => {
    server.use(
      http.get('*/api/staff/cats/:id/clinical-history/:section', () =>
        HttpResponse.json({
          key: 'lab-results',
          available: true,
          owner: 'x',
          items: [
            { id: 'i1', file: file(REL) },
            { id: 'i2', file: null },
          ],
          pageNumber: 1,
          pageSize: 20,
          totalCount: 2,
        }),
      ),
    );
    const page = await getHistorySection('c1', 'lab-results', 1);
    expect(page.items[0]!.file?.url).toBe(getApiUrl(REL));
    expect(page.items[1]!.file).toBeNull();
    expect(page.totalCount).toBe(2);
  });
});

describe('uploadFile response', () => {
  beforeEach(() =>
    installFakeXhr({ respond: () => ({ status: 201, body: file(REL) }) }),
  );
  afterEach(() => restoreRealXhr());

  it('anchors the url of the freshly uploaded file', async () => {
    const result = await uploadFile({
      file: new File([new Uint8Array(4)], 'a.png', { type: 'image/png' }),
      purpose: 'CatProfilePhoto',
      audience: 'staff',
    } as Parameters<typeof uploadFile>[0]);
    expect(result.url).toBe(getApiUrl(REL));
  });
});
