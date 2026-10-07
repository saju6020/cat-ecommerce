import { test, expect, signInAsStaff } from './fixtures';
import { catProfilesFixtureIds } from '../../mocks/handlers/areas/cat-profiles';

// ARCH gate MEDIUM (first pass) — signs in as staff only (never both cookies at
// once, tests/e2e/fixtures.ts); every route this file visits is staff-audience.
test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

/** E6, E7, E8, E9, E10 (HO-CAT-002 §16) — US-CAT-05, US-CAT-07. */

test('E6 — recording a vaccination is reflected in its status, with a next-due date', async ({
  page,
}) => {
  const catId = catProfilesFixtureIds.catCurrentPlain; // Mittens
  await page.goto(`/staff/cats/${catId}/vaccinations`);

  await page
    .getByRole('combobox', { name: 'Vaccine' })
    .selectOption({ label: 'Rabies (RABIES) — core' });
  await page.getByLabel('Batch reference').fill('RB-NEW-01');
  await page.getByLabel('Date given').fill('2026-09-20');
  await page.getByLabel('Batch expiry').fill('2027-09-20');
  await page.getByLabel('Administering vet').fill('Dr. Patel');
  await page.getByRole('button', { name: 'Record vaccination' }).click();

  await expect(page.getByText(/vaccination recorded/i).first()).toBeVisible();
  // Rabies validity is 36 months (mock catalogue) — next due 2029-09-20.
  await expect(page.getByText(/next due 20 sep 2029/i)).toBeVisible();
});

test('E7 — an expired batch is rejected with a field error on batch expiry (US-CAT-05 AC2)', async ({
  page,
}) => {
  const catId = catProfilesFixtureIds.catCurrentPlain; // Mittens
  await page.goto(`/staff/cats/${catId}/vaccinations`);

  await page
    .getByRole('combobox', { name: 'Vaccine' })
    .selectOption({ label: 'Rabies (RABIES) — core' });
  await page.getByLabel('Batch reference').fill('RB-EXPIRED');
  await page.getByLabel('Date given').fill('2026-09-20');
  await page.getByLabel('Batch expiry').fill('2026-01-01'); // before the date given
  await page.getByLabel('Administering vet').fill('Dr. Patel');
  await page.getByRole('button', { name: 'Record vaccination' }).click();

  const batchExpiryField = page.getByLabel('Batch expiry');
  await expect(page.getByText(/expired before the date given/i)).toBeVisible();
  await expect(batchExpiryField).toHaveAttribute('aria-invalid', 'true');
});

test('E8 — a cat whose core vaccine is overdue shows Overdue and the boarding warning (US-CAT-07 AC3)', async ({
  page,
}) => {
  const catId = catProfilesFixtureIds.catHighRiskOverdue; // Tiger
  await page.goto(`/staff/cats/${catId}/vaccination-status`);

  await expect(page.getByText('Overdue').first()).toBeVisible();
  // Not getByRole('alert') alone — Next.js's route-announcer region also has role="alert".
  await expect(page.getByText(/boarding will be blocked/i)).toBeVisible();
});

test('E9 — after recording a vaccination, the status tab is refreshed, not stale', async ({
  page,
}) => {
  const catId = catProfilesFixtureIds.catNoAllergiesEstimatedDob; // Biscuit — starts DueSoon
  await page.goto(`/staff/cats/${catId}/vaccination-status`);
  await expect(page.getByText('Due soon').first()).toBeVisible();

  // Client-side (Link) navigation from here on — see cat-weights.spec.ts's comment on why not
  // page.goto after a mutation against the in-memory mock.
  await page.getByRole('link', { name: 'Vaccinations' }).click();
  await page
    .getByRole('combobox', { name: 'Vaccine' })
    .selectOption({ label: 'Rabies (RABIES) — core' });
  await page.getByLabel('Batch reference').fill('RB-FRESH');
  await page.getByLabel('Date given').fill('2026-09-20');
  await page.getByLabel('Batch expiry').fill('2027-09-20');
  await page.getByLabel('Administering vet').fill('Dr. Patel');
  await page.getByRole('button', { name: 'Record vaccination' }).click();
  await expect(page.getByText(/vaccination recorded/i).first()).toBeVisible();

  await page.getByRole('link', { name: 'Vaccination status' }).click();
  // Freshly vaccinated (36-month validity) — no longer Due soon overall.
  await expect(page.getByText('Due soon')).toHaveCount(0);
});

test('E10 — flagging high-risk without a reason is blocked with a field error', async ({
  page,
}) => {
  const catId = catProfilesFixtureIds.catCurrentPlain; // Mittens
  await page.goto(`/staff/cats/${catId}`);

  await page.getByRole('button', { name: 'Flag as high-risk' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('Handling note').fill('Test note');
  // Reason left empty.
  await dialog.getByRole('button', { name: 'Flag as high-risk' }).click();

  await expect(page.getByText(/enter a reason/i)).toBeVisible();
});
