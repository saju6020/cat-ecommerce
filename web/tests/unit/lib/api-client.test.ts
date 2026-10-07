import { afterEach, describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { apiFetch } from '@/lib/api/client';

/**
 * `lib/api/client.ts`'s identity headers (HO-CAT-002 §11.2, per the Accounts contract —
 * HO-ACC-001 §12.5 ADR-031 rule R7, HO-ADM-001 §8.3 ADR-046):
 *  - `X-CSRF-Token` from the non-HttpOnly `pc_csrf` cookie, on unsafe methods only.
 *  - `X-Branch-Id` on every request, reads included — sent whenever `lib/branch.ts`'s
 *    `getActiveBranchId()` returns one; today that is never, since no branch source exists yet
 *    (a documented no-op hook point, not an invented one). The "once a branch source exists"
 *    case lives in `api-client-with-branch.test.ts` — `vi.mock('@/lib/branch', ...)` is
 *    module-wide per test file, so it can't coexist here with these "still null" cases.
 *  - `credentials: 'include'` always, so `pc_session`/`pc_staff_session` ride along.
 *
 * Captured via an echo handler rather than mocking `fetch` directly, since MSW's own
 * interceptors already sit at the network layer `tests/setup.ts` wires up — asserting through
 * that layer is what a real request would actually carry.
 */
function clearCookies() {
  document.cookie.split(';').forEach((cookie) => {
    const name = cookie.split('=')[0]?.trim();
    if (name) document.cookie = `${name}=;expires=Thu, 01 Jan 1970 00:00:00 GMT;path=/`;
  });
}

afterEach(() => clearCookies());

describe('apiFetch identity headers', () => {
  it('sends X-CSRF-Token (from the pc_csrf cookie) on an unsafe method (POST)', async () => {
    document.cookie = 'pc_csrf=test-csrf-token-123';
    let capturedCsrfHeader: string | null = null;

    server.use(
      http.post('*/api/echo-headers', ({ request }) => {
        capturedCsrfHeader = request.headers.get('X-CSRF-Token');
        return HttpResponse.json({ ok: true });
      }),
    );

    await apiFetch('/api/echo-headers', { method: 'POST', body: { x: 1 } });

    expect(capturedCsrfHeader).toBe('test-csrf-token-123');
  });

  it('does not send X-CSRF-Token on a safe method (GET), even with the cookie present', async () => {
    document.cookie = 'pc_csrf=test-csrf-token-123';
    let capturedCsrfHeader: string | null = 'not-checked-yet';

    server.use(
      http.get('*/api/echo-headers', ({ request }) => {
        capturedCsrfHeader = request.headers.get('X-CSRF-Token');
        return HttpResponse.json({ ok: true });
      }),
    );

    await apiFetch('/api/echo-headers');

    expect(capturedCsrfHeader).toBeNull();
  });

  it('omits X-CSRF-Token on an unsafe method when there is no pc_csrf cookie', async () => {
    let capturedCsrfHeader: string | null = 'not-checked-yet';

    server.use(
      http.put('*/api/echo-headers', ({ request }) => {
        capturedCsrfHeader = request.headers.get('X-CSRF-Token');
        return HttpResponse.json({ ok: true });
      }),
    );

    await apiFetch('/api/echo-headers', { method: 'PUT', body: {} });

    expect(capturedCsrfHeader).toBeNull();
  });

  it('does not send X-Branch-Id yet on a write — getActiveBranchId is a documented no-op hook point', async () => {
    let capturedBranchHeader: string | null = 'not-checked-yet';

    server.use(
      http.post('*/api/echo-headers', ({ request }) => {
        capturedBranchHeader = request.headers.get('X-Branch-Id');
        return HttpResponse.json({ ok: true });
      }),
    );

    await apiFetch('/api/echo-headers', { method: 'POST', body: {} });

    expect(capturedBranchHeader).toBeNull();
  });

  it('does not send X-Branch-Id yet on a read either — it is not gated by method', async () => {
    let capturedBranchHeader: string | null = 'not-checked-yet';

    server.use(
      http.get('*/api/echo-headers', ({ request }) => {
        capturedBranchHeader = request.headers.get('X-Branch-Id');
        return HttpResponse.json({ ok: true });
      }),
    );

    await apiFetch('/api/echo-headers');

    expect(capturedBranchHeader).toBeNull();
  });

  it('sends credentials: include so pc_session/pc_staff_session ride along automatically', async () => {
    let capturedCredentials: RequestCredentials | undefined;

    // MSW's request handler doesn't expose the *fetch call's* `credentials` option (only the
    // resulting Request, whose `credentials` reflects the same value) — read it off the Request.
    server.use(
      http.get('*/api/echo-headers', ({ request }) => {
        capturedCredentials = request.credentials;
        return HttpResponse.json({ ok: true });
      }),
    );

    await apiFetch('/api/echo-headers');

    expect(capturedCredentials).toBe('include');
  });
});
