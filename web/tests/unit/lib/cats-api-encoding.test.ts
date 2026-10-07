import { describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { getCat } from '@/lib/api/cats';

/**
 * SECURITY gate LOW finding on HO-CAT-002 (fixed 241d310), regression test added at the TEST
 * gate — every `catId` route param is now wrapped in `encodeURIComponent` before being placed in
 * an API path (`lib/api/cats.ts`'s `catPath()`), closing a client-side path traversal: Next 15
 * URL-decodes dynamic route params, so an unencoded `catId` containing a `/` (e.g. from a crafted
 * link) would otherwise let a request path escape `/api/cats/{catId}` onto an arbitrary path on
 * the API origin.
 *
 * Proven here by using a `catId` containing a `/` and `?` — if it were not encoded, the request
 * would split across path segments and/or inject a query string, and would never reach a handler
 * registered on the single-segment `:catId` pattern below.
 */
describe('lib/api/cats.ts — catId path encoding', () => {
  it('encodes a catId containing "/" and "?" so it stays a single path segment', async () => {
    const trickyCatId = '../other?x=1';
    let capturedUrl: string | null = null;

    server.use(
      http.get('*/api/cats/:catId', ({ request, params }) => {
        capturedUrl = request.url;
        return HttpResponse.json({ id: params.catId, name: 'Whiskers' });
      }),
    );

    const cat = await getCat(trickyCatId);

    expect(capturedUrl).not.toBeNull();
    const path = new URL(capturedUrl!).pathname;
    // The whole tricky id lands inside one path segment, percent-encoded — never a literal "/" or
    // "?" that would otherwise re-route the request off /api/cats/{catId}.
    expect(path).toBe('/api/cats/..%2Fother%3Fx%3D1');
    // MSW decodes the matched :catId param back for us, confirming the round trip is lossless.
    expect(cat.id).toBe(trickyCatId);
  });
});
