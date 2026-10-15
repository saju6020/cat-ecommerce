import type { Page } from '@playwright/test';
import { accountsFixtureIds } from '../../mocks/handlers/areas/accounts';
import { emrFixtureIds } from '../../mocks/handlers/visits/fixtures';
import { appointmentsFixtureIds } from '../../mocks/handlers/appointments/fixtures';

/**
 * Shared helpers for the clinical records e2e specs (`emr-*.spec.ts`, HO-EMR-002 §16). Plain
 * functions, not a spec. The mock only knows two staff logins (administrator and nurse), so a Vet or a
 * Receptionist is the NURSE account whose roles the administrator changes first
 * (`PUT /api/staff-accounts/{id}/roles`, which the Accounts mock persists for the tab).
 */
export const ids = { ...emrFixtureIds, ...appointmentsFixtureIds };
export type EmrRole = 'Administrator' | 'Nurse' | 'Vet' | 'Reception';

export const IS_REAL_API = process.env.NEXT_PUBLIC_USE_MOCKS === 'false';

async function fetchJson(page: Page, method: string, path: string, body?: unknown) {
  return page.evaluate(
    async ({ method, path, body }) => {
      const base = (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl ?? '';
      const csrf = document.cookie
        .split('; ')
        .find((r) => r.startsWith('pc_csrf='))
        ?.slice(8);
      const response = await fetch(`${base}${path}`, {
        method,
        credentials: 'include',
        headers: {
          ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
          ...(csrf ? { 'X-CSRF-Token': decodeURIComponent(csrf) } : {}),
        },
        body: body !== undefined ? JSON.stringify(body) : undefined,
      });
      const text = await response.text();
      return { status: response.status, body: text ? safeParse(text) : null };
      function safeParse(value: string) {
        try {
          return JSON.parse(value);
        } catch {
          return value;
        }
      }
    },
    { method, path, body },
  );
}

export const api = {
  get: (page: Page, path: string) => fetchJson(page, 'GET', path),
  post: (page: Page, path: string, body?: unknown) => fetchJson(page, 'POST', path, body ?? {}),
  put: (page: Page, path: string, body?: unknown) => fetchJson(page, 'PUT', path, body ?? {}),
};

async function signIn(page: Page, identifier: string, password: string) {
  const result = await fetchJson(page, 'POST', '/api/staff/auth/sign-in', { identifier, password });
  if (result.status >= 300)
    throw new Error(`staff sign-in as ${identifier} failed: ${result.status}`);
}

/**
 * Signs the page in as the given MOCK role. Call once, before the first navigation to a staff route
 * (the page must be on a real origin first). Real-API runs sign in with `E2E_STAFF_*` env instead
 * (see `signInReal`).
 */
export async function signInAsRole(page: Page, role: EmrRole): Promise<void> {
  if (page.url() === 'about:blank') await page.goto('/staff/sign-in');
  if (role === 'Administrator') {
    await signIn(page, accountsFixtureIds.adminIdentifier, accountsFixtureIds.adminPassword);
    return;
  }
  if (role !== 'Nurse') {
    await signIn(page, accountsFixtureIds.adminIdentifier, accountsFixtureIds.adminPassword);
    const changed = await api.put(
      page,
      `/api/staff-accounts/${accountsFixtureIds.nurseStaffId}/roles`,
      {
        roleIds: [role],
      },
    );
    if (changed.status >= 300) throw new Error(`could not switch the nurse account to ${role}`);
  }
  await signIn(page, accountsFixtureIds.nurseIdentifier, accountsFixtureIds.nursePassword);
}

/** Collects every key/value written to localStorage, sessionStorage and IndexedDB database names,
 *  ignoring the Mock Service Worker's own stand-in for the server's database. */
export async function appStorageSnapshot(page: Page): Promise<string> {
  return page.evaluate(async () => {
    const entries: string[] = [];
    for (const store of [window.localStorage, window.sessionStorage]) {
      for (let i = 0; i < store.length; i += 1) {
        const key = store.key(i)!;
        if (/^__purrcare_.*mock/.test(key)) continue;
        entries.push(`${key}=${store.getItem(key)}`);
      }
    }
    const dbs = (await indexedDB.databases?.()) ?? [];
    entries.push(...dbs.map((d) => `idb:${d.name}`));
    const caches = await window.caches?.keys?.();
    entries.push(...(caches ?? []).map((c) => `cache:${c}`));
    return entries.join('\n');
  });
}
