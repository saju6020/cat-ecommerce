import { expect, type Page } from '@playwright/test';
import { signInAsCustomer } from './fixtures';
import { pharmacyFixtureIds } from '../../mocks/handlers/pharmacy';
import { emrFixtureIds } from '../../mocks/handlers/visits';
import { api, signInAsRole } from './emr-helpers';

/**
 * Shared helpers for the pharmacy e2e specs (`pharmacy-*.spec.ts`, HO-PHM-002 §16). Plain functions,
 * not a spec. The mock knows two staff logins (administrator and nurse); a Vet or Receptionist is the
 * nurse account after the administrator changes its role (`signInAsRole`, EMR's helper). The witness
 * step uses the two extra sign-ins only the pharmacy mock knows (`witness.nurse@…`, `witness.vet@…`);
 * the administrator's own sign-in is the stand-in for "a witness who is not a vet or a nurse".
 */
export const ids = { ...pharmacyFixtureIds, ...emrFixtureIds };
export { api, signInAsRole };

export const WITNESS_NURSE = {
  identifier: pharmacyFixtureIds.witnessNurseIdentifier,
  password: pharmacyFixtureIds.witnessNursePassword,
};
export const WITNESS_VET = {
  identifier: pharmacyFixtureIds.witnessVetIdentifier,
  password: pharmacyFixtureIds.witnessVetPassword,
};
/** A real, valid account whose role is neither vet nor nurse — the server must refuse it as a witness. */
export const NOT_A_WITNESS = { identifier: 'admin@purrcare.test', password: 'AdminPass123!' };

/** Adds an allergy to a cat through the cat API (the cat mock keeps it for the tab). */
export async function addAllergy(page: Page, catId: string, allergen: string): Promise<void> {
  const cat = await api.get(page, `/api/cats/${catId}`);
  const existing = (cat.body as { allergies: unknown[] }).allergies;
  const saved = await api.put(page, `/api/cats/${catId}/medical-profile`, {
    allergies: [
      ...existing,
      { allergen, reaction: 'Hives', severity: 'Severe', recordedOn: '2026-01-01' },
    ],
    chronicConditions: [],
    medications: [],
    dietaryNotes: null,
    behaviouralNotes: null,
  });
  expect(saved.status).toBeLessThan(300);
}

export async function pickMedicine(page: Page, term: string, name: RegExp): Promise<void> {
  await page.getByRole('combobox', { name: /^medicine/i }).fill(term);
  await page.getByRole('option', { name }).first().click();
}

/** Everything but the dose for a per-ml suspension (the dose goes last so one safety check follows). */
export async function fillComposer(
  page: Page,
  fields: { frequency: string; duration: string; quantity: string; instructions: string },
): Promise<void> {
  await page.getByLabel(/^frequency/i).selectOption(fields.frequency);
  await page.getByRole('textbox', { name: /^duration/i }).fill(fields.duration);
  await page.getByRole('textbox', { name: /^quantity per supply/i }).fill(fields.quantity);
  await page.getByLabel(/^instructions/i).fill(fields.instructions);
}

export async function enterDose(page: Page, amount: string, unit: string): Promise<void> {
  await page.getByRole('combobox', { name: 'Dose unit' }).selectOption(unit);
  await page.getByRole('textbox', { name: /^dose/i }).fill(amount);
}

export async function tickAllWarnings(page: Page): Promise<void> {
  for (const box of await page.getByRole('checkbox', { name: /I have reviewed/i }).all())
    await box.check();
}

/** Counts `window.print()` calls (a headless browser cannot show its print dialog). */
export async function stubPrint(page: Page): Promise<void> {
  await page.evaluate(() => {
    (window as unknown as { __printCount: number }).__printCount = 0;
    window.print = () => {
      (window as unknown as { __printCount: number }).__printCount += 1;
    };
  });
}

/** The prescription number on a detail page (`RX-2026-…`). */
export async function readPrescriptionNumber(page: Page): Promise<string> {
  const heading = page.getByRole('heading', { name: /^RX-\d{4}-\d{6}$/ }).first();
  await expect(heading).toBeVisible();
  return (await heading.innerText()).trim();
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

export type PharmacyRole = 'Administrator' | 'Vet' | 'Reception' | 'Customer';

/** True when the page ended on a sign-in form — the staff shell's navigation never appeared. */
async function landedOnSignIn(page: Page, role: PharmacyRole): Promise<boolean> {
  if (role === 'Customer') {
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(500); // the portal guard redirects after its session read
    return /\/sign-in/.test(new URL(page.url()).pathname);
  }
  const shell = page.getByRole('navigation', { name: 'Main' });
  const form = page.getByRole('heading', { name: 'Staff sign in' });
  await shell.or(form).first().waitFor({ timeout: 30_000 });
  return form.isVisible();
}

/**
 * Signs in as `role` and opens `path`. Two things make a sign-in unreliable under a cold `next dev`
 * and the in-browser mock, so both are handled here and not in every test: (1) changing the role of
 * the mock's nurse account rotates its security stamp, and a page that is still open (a queue poll, a
 * refetch) answers its own `401` by signing out — so the role is changed from the quiet sign-in page,
 * not from a page that is still talking to the API; (2) if the shell still never appears (the
 * documented MSW start-up race in `fixtures.ts`), sign in and navigate again, up to three times.
 */
export async function openAs(page: Page, role: PharmacyRole, path: string): Promise<void> {
  for (let attempt = 1; attempt <= 3; attempt += 1) {
    await page.goto(role === 'Customer' ? '/sign-in' : '/staff/sign-in');
    if (role === 'Customer') await signInAsCustomer(page);
    else await signInAsRole(page, role);
    await page.goto(path);
    if (!(await landedOnSignIn(page, role))) {
      await page.waitForLoadState('networkidle');
      return;
    }
  }
  throw new Error(`could not sign in as ${role} to open ${path}`);
}

/** `goto` for the middle of a test: if the page lands on the sign-in form, sign in again and retry. */
export async function gotoSigned(page: Page, role: PharmacyRole, path: string): Promise<void> {
  await page.goto(path);
  if (await landedOnSignIn(page, role)) {
    await openAs(page, role, path);
    return;
  }
  await page.waitForLoadState('networkidle');
}
