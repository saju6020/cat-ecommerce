import { describe, expect, it } from 'vitest';
import { getApiUrl } from '@/lib/api/client';
import { resolveFileUrl, resolveStoredFile, type StoredFileViewModel } from '@/lib/api/files';

/** FIL backlog 25 / HO-FIL-002 §1 Amended 2026-10-09 -- the one place a signed url becomes loadable. */
describe('resolveFileUrl', () => {
  const signed = '/api/files/9f1c/content?exp=1999999999&disp=inline&sig=abc_-';

  it('anchors a path with exactly one leading slash on the API base url', () => {
    expect(resolveFileUrl(signed)).toBe(getApiUrl(signed));
    expect(resolveFileUrl(signed)).toMatch(/^https?:\/\/[^/]+\/api\/files\//);
  });

  it('passes absolute https and http urls through unchanged (production GCS, ADR-318)', () => {
    const gcs = 'https://storage.googleapis.com/bucket/obj?X-Goog-Signature=abc';
    expect(resolveFileUrl(gcs)).toBe(gcs);
    expect(resolveFileUrl('http://localhost:5010/api/files/x/content?sig=1')).toBe(
      'http://localhost:5010/api/files/x/content?sig=1',
    );
  });

  it.each([
    ['protocol-relative', '//evil.test/x.png'],
    ['backslash host', '/\\evil.test/x.png'],
    ['javascript scheme', 'javascript:alert(1)'],
    ['data scheme', 'data:image/png;base64,AAAA'],
    ['file scheme', 'file:///etc/passwd'],
    ['blob scheme', 'blob:http://x/1'],
    ['ftp scheme', 'ftp://host/file'],
    ['relative without a slash', 'api/files/x/content'],
    ['dot-relative', './x'],
    ['empty string', ''],
    ['null', null],
    ['undefined', undefined],
  ])('treats %s as unusable (null)', (_name, value) => {
    expect(resolveFileUrl(value as string | null | undefined)).toBeNull();
  });
});

describe('resolveStoredFile', () => {
  const file: StoredFileViewModel = {
    fileId: 'f',
    url: '/api/files/f/content?exp=1&disp=inline&sig=s',
    contentType: 'image/png',
    sizeInBytes: 1,
    originalFileName: 'a.png',
    urlExpiresOn: '2999-01-01T00:00:00.000Z',
  };

  it('resolves the url and keeps every other field', () => {
    expect(resolveStoredFile(file)).toEqual({ ...file, url: getApiUrl(file.url) });
  });
  it('maps an unusable url to an empty string', () => {
    expect(resolveStoredFile({ ...file, url: '//evil.test/x' }).url).toBe('');
  });
  it('passes null through', () => {
    expect(resolveStoredFile(null)).toBeNull();
  });
});
