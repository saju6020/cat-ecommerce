import { describe, expect, it, vi } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { apiFetch } from '@/lib/api/client';

/**
 * `X-Branch-Id` once a real branch source exists (HO-ADM-001 §8.3, ADR-046) — split into its
 * own file because `vi.mock('@/lib/branch', ...)` is hoisted and applies to every test in the
 * file it's written in, which would break `api-client.test.ts`'s "still null today" cases.
 *
 * Proves the header is sent on a read (GET) exactly the same as on a write (POST) — the gate
 * finding this addresses: `X-Branch-Id` was previously attached only inside the unsafe-methods
 * branch, so a GET could never carry it regardless of what `getActiveBranchId()` returned.
 */
vi.mock('@/lib/branch', () => ({ getActiveBranchId: () => 'branch-42' }));

describe('apiFetch identity headers — with a branch source present', () => {
  it('sends X-Branch-Id on a read (GET)', async () => {
    let capturedBranchHeader: string | null = null;

    server.use(
      http.get('*/api/echo-headers', ({ request }) => {
        capturedBranchHeader = request.headers.get('X-Branch-Id');
        return HttpResponse.json({ ok: true });
      }),
    );

    await apiFetch('/api/echo-headers');

    expect(capturedBranchHeader).toBe('branch-42');
  });

  it('sends X-Branch-Id on a write (POST)', async () => {
    let capturedBranchHeader: string | null = null;

    server.use(
      http.post('*/api/echo-headers', ({ request }) => {
        capturedBranchHeader = request.headers.get('X-Branch-Id');
        return HttpResponse.json({ ok: true });
      }),
    );

    await apiFetch('/api/echo-headers', { method: 'POST', body: {} });

    expect(capturedBranchHeader).toBe('branch-42');
  });
});
