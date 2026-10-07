import { afterEach, describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { apiFetch } from '@/lib/api/client';

function clearCookies() {
  document.cookie.split(';').forEach((cookie) => {
    const name = cookie.split('=')[0]?.trim();
    if (name) document.cookie = `${name}=;expires=Thu, 01 Jan 1970 00:00:00 GMT;path=/`;
  });
}

afterEach(() => clearCookies());

/**
 * SECURITY gate LOW finding on HO-CAT-002 (fixed 241d310), regression test added at the TEST
 * gate — `identityHeaders` used to be spread before `...headers` in `apiFetch`'s request init, so
 * a caller-supplied header of the same name could silently overwrite the client's own
 * `X-CSRF-Token`. No caller does this today, but nothing stopped a future one from doing so by
 * accident. The fix spreads `identityHeaders` last; this proves a caller's own `X-CSRF-Token`
 * value never reaches the wire once the real cookie-derived one exists.
 */
describe('apiFetch — identity headers cannot be overridden by a caller (241d310)', () => {
  it('keeps the cookie-derived X-CSRF-Token even when the caller passes its own', async () => {
    document.cookie = 'pc_csrf=real-token';
    let capturedCsrfHeader: string | null = null;

    server.use(
      http.post('*/api/echo-headers', ({ request }) => {
        capturedCsrfHeader = request.headers.get('X-CSRF-Token');
        return HttpResponse.json({ ok: true });
      }),
    );

    await apiFetch('/api/echo-headers', {
      method: 'POST',
      headers: { 'X-CSRF-Token': 'attacker-supplied-token' },
      body: { ok: true },
    });

    expect(capturedCsrfHeader).toBe('real-token');
  });
});
