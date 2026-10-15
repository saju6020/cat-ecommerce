import { readFileSync } from 'node:fs';
import path from 'node:path';
import { afterEach, describe, expect, it, vi } from 'vitest';

/**
 * public/staff/sw.js navigation fallback (ADR-FE-139/140, CODE_REVIEW MEDIUM): only a REJECTED fetch
 * sends a person to the offline board. A slow answer is still an answer. The worker is evaluated
 * with stubbed globals and its `fetch` listener is driven directly.
 */
const source = readFileSync(path.resolve(__dirname, '../../../public/staff/sw.js'), 'utf8');

type Listener = (event: unknown) => void;
const SHELL = { shell: true };

function load(networkFetch: () => Promise<unknown>) {
  const listeners: Record<string, Listener> = {};
  const self = {
    location: { origin: 'http://localhost:3113' },
    addEventListener: (type: string, listener: Listener) => {
      listeners[type] = listener;
    },
    skipWaiting: () => undefined,
    clients: { claim: () => Promise.resolve() },
  };
  const caches = { match: async () => SHELL, open: async () => ({}), keys: async () => [] };
  const ResponseStub = {
    redirect: (url: string, status: number) => ({ redirectTo: url, status }),
    error: () => ({ error: true }),
  };
  new Function('self', 'caches', 'fetch', 'Response', source)(self, caches, networkFetch, ResponseStub);
  return (url: string, mode = 'navigate') => {
    let answer: Promise<unknown> | undefined;
    listeners.fetch!({
      request: { method: 'GET', url: `http://localhost:3113${url}`, mode },
      respondWith: (p: Promise<unknown>) => {
        answer = p;
      },
    });
    return answer;
  };
}

afterEach(() => vi.useRealTimers());

describe('public/staff/sw.js navigation fallback', () => {
  it('a navigation that is merely slow (10 s) is answered by the network, never redirected', async () => {
    vi.useFakeTimers();
    const live = { live: true };
    const navigate = load(() => new Promise((resolve) => setTimeout(() => resolve(live), 10_000)));
    const answer = navigate('/staff/front-desk');
    expect(answer).toBeDefined();
    await vi.advanceTimersByTimeAsync(10_000);
    await expect(answer).resolves.toBe(live);
  });

  it('a rejected fetch on a staff page redirects to the offline board with the path', async () => {
    const navigate = load(() => Promise.reject(new TypeError('Failed to fetch')));
    await expect(navigate('/staff/front-desk')).resolves.toEqual({
      redirectTo: '/staff/offline?from=' + encodeURIComponent('/staff/front-desk'),
      status: 302,
    });
  });

  it('a rejected fetch on the offline shell itself serves the cached shell', async () => {
    const navigate = load(() => Promise.reject(new TypeError('Failed to fetch')));
    await expect(navigate('/staff/offline')).resolves.toBe(SHELL);
  });

  it('never answers /api/ or the sign-in page', () => {
    const navigate = load(() => Promise.reject(new TypeError('Failed to fetch')));
    expect(navigate('/api/staff/offline-snapshot', 'cors')).toBeUndefined();
    expect(navigate('/staff/sign-in')).toBeUndefined();
  });
});
