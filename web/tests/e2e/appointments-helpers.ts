import type { Page } from '@playwright/test';
import { addDaysIso, formatBranchDateLong } from '../../lib/format/appointment';
import { todayInZone } from '../../lib/format/time';

/**
 * Shared helpers for the Appointments e2e specs (`appointments-*.spec.ts`, HO-APT-002 §16). Plain
 * functions — not a spec — so importing them never registers a test twice.
 */

export const MAIN_BRANCH_ZONE = 'Asia/Dhaka';

function weekdayOf(dateIso: string): number {
  return new Date(`${dateIso}T00:00:00Z`).getUTCDay();
}

/** A Monday-to-Thursday `minOffset`+ days ahead in the main branch's zone — open every week, so
 *  the mock reliably has times (and the 10:30 race slot) on it. Pure arithmetic on date strings. */
export function openDay(minOffset = 8): string {
  let date = addDaysIso(todayInZone(MAIN_BRANCH_ZONE), minOffset);
  for (let guard = 0; guard < 14; guard += 1) {
    const weekday = weekdayOf(date);
    if (weekday >= 1 && weekday <= 4) return date;
    date = addDaysIso(date, 1);
  }
  return date;
}

/** The strip/aria label of a day button, e.g. "Monday 12 October". */
export function dayLabel(dateIso: string): RegExp {
  return new RegExp(formatBranchDateLong(dateIso));
}

async function postJson(page: Page, path: string, body: unknown): Promise<unknown> {
  return page.evaluate(
    async ({ path, body }) => {
      const base = (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl ?? '';
      const response = await fetch(`${base}${path}`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      });
      return response.json();
    },
    { path, body },
  );
}

/**
 * The Administration mock ships one bookable service (a consultation). A reminder-style booking
 * needs a vaccination, so this creates one through the admin mock's own endpoints (the way an
 * administrator would) and prices it. The page must already be loaded (MSW running).
 */
export async function seedVaccinationService(page: Page): Promise<void> {
  const created = (await postJson(page, '/api/admin/services', {
    code: `VAX${Date.now() % 100000}`,
    name: 'Vaccination',
    description: 'Core vaccine booster.',
    category: 'Vaccination',
    durationMinutes: 30,
    qualifiedRoleIds: ['Vet'],
    requiredRoomKind: 'Consultation',
    taxClassId: 't0000000-0000-0000-0000-00000000a001',
    isBookableOnline: true,
  })) as { serviceId: string };
  await postJson(page, `/api/admin/services/${created.serviceId}/prices`, {
    amount: '800.00',
    currencyCode: 'BDT',
    effectiveFrom: new Date(Date.now() - 30_000).toISOString(),
  });
}
