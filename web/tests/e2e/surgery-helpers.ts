import { expect, type Page } from '@playwright/test';
import { signInAsCustomer } from './fixtures';
import { api, signInAsRole, type EmrRole } from './emr-helpers';
import { surgeryFixtureIds } from '../../mocks/handlers/surgeries';
import { pharmacyFixtureIds } from '../../mocks/handlers/pharmacy';

/**
 * Shared helpers for the surgery e2e specs (`surgery-*.spec.ts`, HO-SRG-002 §16). Plain functions, not a
 * spec. The mock knows two staff logins (administrator and nurse); a Vet or Receptionist is the nurse
 * account after the administrator changes its role (`signInAsRole`, EMR's helper). The witness step uses
 * the two extra sign-ins the pharmacy mock defines.
 */
export const ids = {
  ...surgeryFixtureIds,
  medBuprenorphineId: pharmacyFixtureIds.medBuprenorphineId,
};
export { api };

export type SurgeryRole = EmrRole | 'Customer';

export const WITNESS_NURSE = {
  identifier: surgeryFixtureIds.witnessNurseIdentifier,
  password: surgeryFixtureIds.witnessNursePassword,
};

/** A date `days` after today, as `yyyy-MM-dd` in Asia/Dhaka (the mock branch's zone). */
export function dayFromToday(days: number): string {
  const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Dhaka' }).format(new Date());
  const date = new Date(`${today}T12:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

async function landedOnSignIn(page: Page, role: SurgeryRole): Promise<boolean> {
  if (role === 'Customer') {
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(500);
    return /\/sign-in/.test(new URL(page.url()).pathname);
  }
  const shell = page.getByRole('navigation', { name: 'Main' });
  const form = page.getByRole('heading', { name: 'Staff sign in' });
  await shell.or(form).first().waitFor({ timeout: 30_000 });
  return form.isVisible();
}

/** Signs in as `role` and opens `path` (retrying through the documented MSW start-up race). */
export async function openAs(page: Page, role: SurgeryRole, path: string): Promise<void> {
  for (let attempt = 1; attempt <= 3; attempt += 1) {
    await page.goto(role === 'Customer' ? '/sign-in' : '/staff/sign-in');
    try {
      if (role === 'Customer') await signInAsCustomer(page);
      else await signInAsRole(page, role);
    } catch (error) {
      // The documented cold-start race: the in-browser mock is not answering yet. Try again.
      if (attempt === 3) throw error;
      await page.waitForTimeout(1000);
      continue;
    }
    await page.goto(path);
    if (!(await landedOnSignIn(page, role))) {
      await page.waitForLoadState('networkidle');
      return;
    }
  }
  throw new Error(`could not sign in as ${role} to open ${path}`);
}

/** `goto` in the middle of a test: if the page lands on the sign-in form, sign in again and retry. */
export async function gotoSigned(page: Page, role: SurgeryRole, path: string): Promise<void> {
  await page.goto(path);
  if (await landedOnSignIn(page, role)) {
    await openAs(page, role, path);
    return;
  }
  await page.waitForLoadState('networkidle');
}

/** Opens a seeded surgery by id and waits for its heading. */
export async function openSurgery(
  page: Page,
  role: SurgeryRole,
  id: string,
  hash = '',
): Promise<void> {
  await openAs(page, role, `/staff/surgeries/${id}${hash}`);
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
}

/** The cat id and the surgery's appointment id, read from the staff API (the mock is the server). */
export async function surgeryFacts(page: Page, id: string) {
  const result = await api.get(page, `/api/staff/surgeries/${id}`);
  const body = result.body as { appointmentId: string; catId: string };
  return { appointmentId: body.appointmentId, catId: body.catId };
}

/** Records both consents for a surgery through the EMR API (the consent screens are EMR's). */
export async function recordConsents(page: Page, id: string): Promise<void> {
  const { appointmentId, catId } = await surgeryFacts(page, id);
  for (const kind of ['Surgery', 'Anaesthesia']) {
    const form = await api.get(page, `/api/staff/consent-forms/${kind}`);
    const policyVersionNumber = (form.body as { versionNumber: number }).versionNumber;
    const result = await api.post(page, '/api/staff/consents', {
      catId,
      appointmentId,
      kind,
      procedureDescription: 'Procedure as discussed with the owner.',
      method: 'OnScreenSignature',
      signature: [
        [
          [0.1, 0.2],
          [0.4, 0.5],
          [0.8, 0.3],
        ],
      ],
      signedByName: 'Owner',
      signerRelationship: 'Owner',
      policyVersionNumber,
    });
    if (result.status >= 300) throw new Error(`could not record ${kind} consent: ${result.status}`);
  }
}

export async function recordWeight(page: Page, catId: string, kilograms: number): Promise<void> {
  const result = await api.post(page, `/api/cats/${catId}/weights`, {
    kilograms,
    recordedOn: dayFromToday(0),
    notes: null,
  });
  if (result.status >= 300) throw new Error(`could not record a weight: ${result.status}`);
}

export async function witnessWith(
  page: Page,
  credentials: { identifier: string; password: string },
  confirm: string | RegExp,
): Promise<void> {
  const dialog = page.getByRole('dialog', { name: /witness needed/i });
  await expect(dialog).toBeVisible();
  await dialog.getByLabel(/witness mobile number or email/i).fill(credentials.identifier);
  await dialog.getByLabel(/witness password/i).fill(credentials.password);
  await dialog.getByRole('button', { name: confirm }).click();
}

export async function stubPrint(page: Page): Promise<void> {
  await page.evaluate(() => {
    (window as unknown as { __printCount: number }).__printCount = 0;
    window.print = () => {
      (window as unknown as { __printCount: number }).__printCount += 1;
    };
  });
}
