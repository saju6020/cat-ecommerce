import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { reconcileServiceWorker, serviceWorkerAllowed } from '@/lib/offline/sw-register';

/** U-A17 -- the registration rules of ADR-FE-139/140 and §20.9. */
describe('serviceWorkerAllowed', () => {
  const on = { nodeEnv: 'production', useMocks: undefined, deviceEnabled: true };
  it('registers only in production, outside mock mode, with the device opted in', () => {
    expect(serviceWorkerAllowed(on)).toBe(true);
    expect(serviceWorkerAllowed({ ...on, nodeEnv: 'development' })).toBe(false);
    expect(serviceWorkerAllowed({ ...on, nodeEnv: 'test' })).toBe(false);
    expect(serviceWorkerAllowed({ ...on, useMocks: 'true' })).toBe(false);
    expect(serviceWorkerAllowed({ ...on, deviceEnabled: false })).toBe(false);
  });
});

describe('reconcileServiceWorker', () => {
  const register = vi.fn().mockResolvedValue({});
  const unregister = vi.fn().mockResolvedValue(true);
  const ours = { scope: 'http://localhost/staff/', active: { scriptURL: 'http://localhost/staff/sw.js', postMessage: vi.fn() }, unregister };
  const msw = { scope: 'http://localhost/', active: { scriptURL: 'http://localhost/mockServiceWorker.js', postMessage: vi.fn() }, unregister: vi.fn() };

  beforeEach(() => {
    register.mockClear();
    unregister.mockClear();
    msw.unregister.mockClear();
    Object.defineProperty(navigator, 'serviceWorker', {
      configurable: true,
      value: { register, getRegistrations: vi.fn().mockResolvedValue([ours, msw]) },
    });
  });
  afterEach(() => {
    Reflect.deleteProperty(navigator, 'serviceWorker');
  });

  it('never registers in mock mode, development, or with the flag off, and removes an existing worker', async () => {
    for (const input of [
      { nodeEnv: 'production', useMocks: 'true', deviceEnabled: true },
      { nodeEnv: 'development', useMocks: undefined, deviceEnabled: true },
      { nodeEnv: 'production', useMocks: undefined, deviceEnabled: false },
    ]) {
      unregister.mockClear();
      expect(await reconcileServiceWorker(input)).toBe('removed');
      expect(register).not.toHaveBeenCalled();
      expect(unregister).toHaveBeenCalledTimes(1);
    }
    expect(msw.unregister).not.toHaveBeenCalled(); // MSW's own worker is never touched
  });

  it('registers /staff/sw.js with scope /staff/ in production with the flag on', async () => {
    expect(await reconcileServiceWorker({ nodeEnv: 'production', useMocks: undefined, deviceEnabled: true })).toBe('registered');
    expect(register).toHaveBeenCalledWith('/staff/sw.js', { scope: '/staff/' });
  });
});

describe('public/staff/sw.js (ADR-FE-139/140)', () => {
  const source = readFileSync(path.resolve(__dirname, '../../../public/staff/sw.js'), 'utf8');
  it('never answers anything under /api/ and never widens its scope', () => {
    expect(source).toContain("url.pathname.startsWith('/api/')) return");
    expect(source).not.toMatch(/Service-Worker-Allowed/i);
  });
  it('caches only static assets and the offline shell', () => {
    const puts = source.match(/\.put\(/g) ?? [];
    expect(puts.length).toBe(3); // shell, asset (install), asset (runtime)
    expect(source).toContain("'/staff/offline'");
  });
});
