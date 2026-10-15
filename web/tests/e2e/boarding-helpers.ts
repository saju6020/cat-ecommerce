import { expect, type Page } from '@playwright/test';
import { signInAsStaff } from './fixtures';
import { holdApiUntilMswReady } from './portal-helpers';
import { addDaysIso } from '../../lib/format/appointment';
import { todayInZone } from '../../lib/format/time';

/**
 * Shared helpers for the Boarding e2e specs (`boarding-*.spec.ts`, HO-BRD-002 §16). Plain functions
 * — not a spec — so importing them never registers a test twice.
 */

export const BOARDING_ZONE = 'Asia/Dhaka';

/** A branch-local date `days` from today in the main branch's zone. */
export function inDays(days: number): string {
  return addDaysIso(todayInZone(BOARDING_ZONE), days);
}

/** The mock's seeded fixtures (mocks/handlers/boarding/fixtures.ts). Copied, not imported, so a spec
 *  never loads the mock module graph into Node. */
export const IDS = {
  stayInHouseFamily: 'e1000000-0000-0000-0000-000000000001',
  stayIsolation: 'e1000000-0000-0000-0000-000000000002',
  stayLong: 'e1000000-0000-0000-0000-000000000003',
  stayReservedSoon: 'e1000000-0000-0000-0000-000000000004',
  stayArrivingToday: 'e1000000-0000-0000-0000-000000000005',
  stayPendingApproval: 'e1000000-0000-0000-0000-000000000006',
  stayLapsed: 'e1000000-0000-0000-0000-000000000007',
  stayOtherCustomer: 'e1000000-0000-0000-0000-000000000008',
  stayCheckedOut: 'e1000000-0000-0000-0000-000000000009',
  stayDeparting: 'e1000000-0000-0000-0000-00000000000a',
  stayEnquiryBlocked: 'e1000000-0000-0000-0000-00000000000b',
  incidentMedium: 'f1000000-0000-0000-0000-000000000001',
  incidentHigh: 'f1000000-0000-0000-0000-000000000002',
  approvalPending: 'f2000000-0000-0000-0000-000000000001',
  catTigerId: '20000000-0000-0000-0000-000000000003',
  vaccineRabies: '30000000-0000-0000-0000-000000000001',
  vaccineFvrcp: '30000000-0000-0000-0000-000000000002',
} as const;

export const LEDGER_KEY = '__purrcare_brd_mock_ledger__';

/** Simulates Billing recording a deposit (the mock's confirm reads this "ledger"). */
export async function setBillingLedger(
  page: Page,
  stayId: string,
  entry: { accepted: number; pending?: boolean },
): Promise<void> {
  await page.evaluate(
    ({ key, stayId, entry }) => {
      const current = JSON.parse(window.sessionStorage.getItem(key) ?? '{}') as Record<
        string,
        unknown
      >;
      current[stayId] = entry;
      window.sessionStorage.setItem(key, JSON.stringify(current));
    },
    { key: LEDGER_KEY, stayId, entry },
  );
}

/** Records both core vaccinations for a cat through the Cat Profiles mock, as a vet would. */
export async function recordCoreVaccinations(page: Page, catId: string): Promise<void> {
  const administeredOn = todayInZone(BOARDING_ZONE);
  const expires = addDaysIso(administeredOn, 365);
  await page.evaluate(
    async ({ catId, vaccines, administeredOn, expires }) => {
      const base = (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl ?? '';
      for (const vaccineId of vaccines) {
        const response = await fetch(`${base}/api/cats/${catId}/vaccinations`, {
          method: 'POST',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            vaccineId,
            batchReference: `B-${vaccineId.slice(-2)}`,
            batchExpiresOn: expires,
            administeredOn,
            administeredByVetId: 'a2000000-0000-0000-0000-000000000001',
            notes: null,
          }),
        });
        if (!response.ok) throw new Error(`vaccination failed: ${response.status}`);
      }
    },
    { catId, vaccines: [IDS.vaccineRabies, IDS.vaccineFvrcp], administeredOn, expires },
  );
}

/** Switches the mock server's "belongings photos are required" rule on (U-E3). */
export async function requireBelongingsPhotos(page: Page, on: boolean): Promise<void> {
  await page.evaluate((value) => {
    window.sessionStorage.setItem('__purrcare_brd_mock_require_photos__', String(value));
  }, on);
}

/**
 * Signs in as the seeded staff administrator and waits until the staff session answers for this tab.
 * Same cold-start diagnosis as `portal-helpers.ts` (the MSW worker starts after the first `/api/`
 * fetch on a busy machine): every page fetch is held until the worker reports ready, so no retry
 * hides a lost session and no app code is changed.
 */
export async function signInStaffEstablished(page: Page): Promise<void> {
  await holdApiUntilMswReady(page);
  if (page.url() === 'about:blank') await page.goto('/staff/sign-in');
  await page.waitForFunction(
    () =>
      Boolean((window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl) &&
      (window as unknown as { __mswReady?: boolean }).__mswReady === true,
    undefined,
    { timeout: 60_000 },
  );
  await signInAsStaff(page);
  await expect
    .poll(
      () =>
        page.evaluate(async () => {
          const base = (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl ?? '';
          const response = await fetch(`${base}/api/staff/session`, { credentials: 'include' });
          return response.status;
        }),
      { message: 'staff session established', timeout: 10_000 },
    )
    .toBe(200);
}
