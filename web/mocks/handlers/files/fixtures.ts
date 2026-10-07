import type { StoredFileViewModel } from '@/lib/api/files';

/**
 * Deliberately NOT `lib/api/client.ts`'s `getApiUrl` — this mock module is loaded eagerly by
 * every test via `tests/setup.ts` → `mocks/server.ts` (`setupServer(...handlers)` runs for the
 * whole suite, not just this area's own tests). Importing `client.ts` here pulls in its own
 * `../branch` import at MODULE-EVALUATION time, before any given test file's own `vi.mock(
 * '@/lib/branch', ...)` (e.g. `tests/unit/lib/api-client-with-branch.test.ts`) has been hoisted
 * and registered — `client.ts` then binds to the REAL `getActiveBranchId` for that test file's
 * whole run, silently defeating the mock (reproduced directly: that test passed with this file
 * absent and failed once it existed, with no other change). A mock server's own URLs have nothing
 * to do with the real frontend client's base-URL helper anyway — this local helper reads the same
 * env var without the transitive import.
 */
function mockApiUrl(path: string): string {
  return `${process.env.NEXT_PUBLIC_API_BASE_URL ?? 'http://localhost:5005'}${path}`;
}

/**
 * File Storage MSW fixtures (HO-FIL-002 §12) — `FileScanUnavailable` and an expired signed URL are
 * the two the consuming areas' own tests will lean on most, per that section's own note, so they
 * are named fixtures here rather than inline literals in a handler.
 */
export const fileStorageFixtureIds = {
  /** A `GET .../content` request for this id always serves a real, tiny PNG — a fresh,
   *  never-expiring `<img>` source for a real browser to actually load. */
  freshImage: '90000000-0000-0000-0000-000000000001',
  /** A `GET .../content` request for this id always answers `403` — HO-FIL-001 §8.6's "expired or
   *  tampered URL", used to drive ADR-FE-154's refresh-once behaviour (C8). */
  expiredImage: '90000000-0000-0000-0000-000000000002',
} as const;

/** A 1×1 transparent PNG (67 bytes) — small enough to inline, real enough for a browser's `<img>`/
 *  `decoding="async"` machinery to genuinely decode rather than error on malformed bytes. */
const ONE_PIXEL_PNG_BASE64 =
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=';

export function onePixelPngBytes(): Uint8Array {
  const binary = atob(ONE_PIXEL_PNG_BASE64);
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index += 1) bytes[index] = binary.charCodeAt(index);
  return bytes;
}

function contentUrl(fileId: string, expiresInSeconds: number): string {
  const exp = Math.floor(Date.now() / 1000) + expiresInSeconds;
  return mockApiUrl(`/api/files/${fileId}/content?exp=${exp}&disp=inline&sig=mock`);
}

/** A ready-to-use `StoredFileViewModel` (HO-FIL-001 §12.4 shape) for component/e2e tests that need
 *  one without driving a real upload first. */
export function buildStoredFileViewModel(
  overrides: Partial<StoredFileViewModel> = {},
): StoredFileViewModel {
  return {
    fileId: fileStorageFixtureIds.freshImage,
    url: contentUrl(fileStorageFixtureIds.freshImage, 5 * 60),
    contentType: 'image/png',
    sizeInBytes: onePixelPngBytes().byteLength,
    originalFileName: 'whiskers.png',
    urlExpiresOn: new Date(Date.now() + 5 * 60 * 1000).toISOString(),
    ...overrides,
  };
}

/** The same shape, but already-expired — `urlExpiresOn` is in the past AND the URL's own `exp`
 *  points at `expiredImage`, so both the client-side ADR-FE-154 check and a real `GET` against it
 *  agree the link is dead. */
export function buildExpiredStoredFileViewModel(
  overrides: Partial<StoredFileViewModel> = {},
): StoredFileViewModel {
  return {
    fileId: fileStorageFixtureIds.expiredImage,
    url: contentUrl(fileStorageFixtureIds.expiredImage, -60),
    contentType: 'image/png',
    sizeInBytes: 12_345,
    originalFileName: 'stale-photo.png',
    urlExpiresOn: new Date(Date.now() - 60_000).toISOString(),
    ...overrides,
  };
}
