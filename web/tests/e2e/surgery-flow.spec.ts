import { test, expect } from './fixtures';
import {
  WITNESS_NURSE,
  dayFromToday,
  gotoSigned,
  ids,
  openAs,
  openSurgery,
  recordConsents,
  recordWeight,
  stubPrint,
  surgeryFacts,
  witnessWith,
} from './surgery-helpers';

/**
 * HO-SRG-002 §16 against MSW: E-X1 (AC-3, the whole spay through the UI, E-A1 → E-E2), E-B3, E-B4, E-C1,
 * E-C2, E-D1, E-E1, E-E2 and E-F1. The mock is the server; the witness is the pharmacy mock's nurse
 * sign-in. Cases that need two real browser contexts or the real database are in the real-API run.
 */
test.setTimeout(240_000);

test('E-X1 / AC-3: a spay from the day list to the post-op sheet', async ({ page }) => {
  const id = ids.surgeryScheduledTodayId;

  // E-B1: the day list names what is outstanding.
  await openAs(page, 'Vet', '/staff/surgeries');
  await expect(
    page
      .getByText('5 outstanding: fasting instructions, blood work, weight, consent, ASA grade')
      .first(),
  ).toBeVisible();

  // Pre-op: admit, then try to start with nothing done (E-B2).
  await page.getByRole('link', { name: 'Pepper' }).first().click();
  await expect(page.getByRole('heading', { level: 1, name: /Spay — Pepper/ })).toBeVisible();
  await page.getByRole('button', { name: 'Admit' }).click();
  await expect(
    page.getByRole('list', { name: 'Progress' }).getByText('Current').locator('..'),
  ).toContainText('Pre-op');
  await expect(page.getByRole('button', { name: 'Start surgery' })).toBeDisabled();
  await expect(page.getByText(/Consent has not been recorded/)).toBeVisible();
  await expect(page.getByText(/pre-op checklist is not complete/)).toBeVisible();

  // Fasting, blood work (waived by the vet), then weight and consent through their own screens.
  await page.getByRole('button', { name: 'Mark issued' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Mark issued' }).click();
  await page.getByRole('button', { name: 'Waive' }).click();
  const waive = page.getByRole('dialog', { name: /Waive the blood work/ });
  await waive.getByLabel(/Reason for waiving/).fill('Young healthy cat, owner declined bloods');
  await waive.getByRole('button', { name: 'Waive blood work' }).click();
  await expect(
    page.getByText('Waived because: Young healthy cat, owner declined bloods'),
  ).toBeVisible();

  const { catId } = await surgeryFacts(page, id);
  await recordWeight(page, catId, 3.8);
  await recordConsents(page, id);
  await page.reload();
  await page.getByRole('radio', { name: 'III' }).check();
  await page.getByRole('button', { name: 'Save ASA grade' }).click();
  await expect(page.getByText('Everything is in place to start.')).toBeVisible();

  // Start.
  await page.getByRole('button', { name: 'Start surgery' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Start surgery' }).click();
  await expect(
    page.getByRole('list', { name: 'Progress' }).getByText('Current').locator('..'),
  ).toContainText('In theatre');

  // E-C1: a milestone, vitals and a controlled drug with its witness.
  await page.getByRole('tab', { name: 'Anaesthesia' }).click();
  await page.getByRole('button', { name: 'Add milestone' }).click();
  await page.getByLabel('Milestone', { exact: true }).selectOption('Premedication');
  await page.getByRole('button', { name: 'Save milestone' }).click();
  await expect(page.getByText('Premedication', { exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'Add vitals' }).click();
  await page.getByLabel(/^Heart rate/).fill('120');
  await page.getByLabel(/^Temperature/).fill('38.6');
  await page.getByRole('button', { name: 'Save vitals' }).click();
  await expect(page.getByText(/120\s?bpm/)).toBeVisible();
  await expect(page.getByText(/38\.6\s?°C/)).toBeVisible();

  await page.getByRole('button', { name: 'Add drug' }).click();
  await page.getByRole('combobox', { name: /^medicine/i }).fill('Bupre');
  await page
    .getByRole('option', { name: /Buprenorphine/ })
    .first()
    .click();
  await page.getByLabel('Dose unit').selectOption('Mg');
  await page.getByRole('textbox', { name: 'Dose', exact: true }).fill('0.02');
  await page.getByLabel('Route').selectOption('Intramuscular');
  await page.getByRole('radio').first().check();
  await page.getByLabel(/^Quantity drawn/).fill('1');
  await page.getByLabel(/^Quantity wasted/).fill('0.5');
  await page.getByRole('button', { name: 'Next: witness' }).click();
  await witnessWith(page, WITNESS_NURSE, 'Confirm and save entry');
  await expect(page.getByText(/Controlled — witnessed by Wasim Nurse/)).toBeVisible();
  await expect(page.getByText(/drew 1\s?ml/)).toBeVisible();

  // E-C2: correct it — the original stays, struck through, and a new witness is needed.
  await page.getByRole('button', { name: /^Correct the .* drug entry/ }).click();
  await page.getByLabel('Reason for the correction').fill('Dose was entered in the wrong unit');
  await page.getByRole('button', { name: 'Review correction' }).click();
  await page.getByRole('button', { name: 'Continue to the witness' }).click();
  await witnessWith(page, WITNESS_NURSE, 'Confirm and add correction');
  await expect(page.getByText('Corrected', { exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: 'See the correction' })).toBeVisible();
  await expect(page.getByRole('button', { name: /edit|delete/i })).toHaveCount(0);

  // E-D1: end the procedure, the report, the neuter status.
  await page.getByRole('tab', { name: 'Report' }).click();
  await page.getByRole('button', { name: 'End procedure' }).click();
  await expect(page.getByRole('dialog')).toContainText('sets Pepper’s neuter status');
  await page.getByRole('dialog').getByRole('button', { name: 'End procedure' }).click();
  await expect(page.getByText(/Neuter status set to Neutered on /)).toBeVisible();
  await page.getByLabel('Findings').fill('Routine. Uterus and ovaries normal.');
  await page.getByLabel('Technique').fill('Midline approach, three-clamp technique');
  await page.getByLabel('Suture material').fill('3-0 Monocryl, intradermal');
  await page.getByRole('button', { name: 'Finalise report' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Finalise report' }).click();
  await expect(page.getByText(/Finalised by Dr .* on /)).toBeVisible();
  await expect(page.getByLabel('Findings')).toHaveCount(0);
  await page.getByLabel('Add addendum').fill('Owner phoned at 17:00: eating well.');
  await page.getByRole('button', { name: 'Add addendum' }).click();
  await expect(page.getByText('Owner phoned at 17:00: eating well.')).toBeVisible();

  // E-E1: the recovery status, and the front desk's board.
  await page.getByRole('tab', { name: 'Recovery' }).click();
  await page
    .getByRole('radio', { name: 'Ready for discharge', exact: true })
    .check({ force: true });
  await page.getByRole('button', { name: 'Set status' }).click();
  await expect(page.getByText('Now: Ready for discharge')).toBeVisible();
  await gotoSigned(page, 'Vet', '/staff/front-desk?tab=recovery');
  const card = page.getByRole('listitem').filter({ hasText: 'Pepper' });
  await expect(card).toContainText('Ready for discharge');
  await expect(card).toContainText(/since \d\d:\d\d · set by /);

  // E-E2: discharge with the defaults, then the print sheet.
  await gotoSigned(page, 'Vet', `/staff/surgeries/${id}#discharge`);
  await page.getByRole('button', { name: /^Discharge Pepper/ }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Discharge' }).click();
  await expect(
    page.getByText(/Discharged\. The post-op instructions have been sent to/),
  ).toBeVisible();
  await page.getByRole('link', { name: 'Print post-op sheet' }).click();
  await expect(page.getByRole('article', { name: /Post-op sheet for Pepper/ })).toContainText(
    /Keep your cat indoors and warm/,
  );
  await stubPrint(page);
  await page.getByRole('button', { name: 'Print' }).click();
  expect(
    await page.evaluate(() => (window as unknown as { __printCount: number }).__printCount),
  ).toBe(1);
});

test('E-B3: a vet starts with an emergency override; the banner names the vet and the reason', async ({
  page,
}) => {
  await openSurgery(page, 'Vet', ids.surgeryPreOpConsentOnlyId);
  await page.getByRole('button', { name: 'Start with emergency override' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('Reason for the emergency override').fill('123456789');
  await expect(dialog.getByRole('button', { name: 'Continue' })).toBeDisabled();
  await dialog
    .getByLabel('Reason for the emergency override')
    .fill('Cat collapsing, owner unreachable');
  await dialog.getByRole('button', { name: 'Continue' }).click();
  await expect(dialog).toContainText('recorded permanently');
  await dialog.getByRole('button', { name: 'Start surgery without consent' }).click();
  await expect(
    page.getByText(
      /Started without recorded consent — emergency override by .* at \d\d:\d\d: Cat collapsing, owner unreachable/,
    ),
  ).toBeVisible();
  await expect(
    page.getByRole('list', { name: 'Progress' }).getByText('Current').locator('..'),
  ).toContainText('In theatre');
});

test('E-B2: a nurse cannot override', async ({ page }) => {
  await openSurgery(page, 'Nurse', ids.surgeryPreOpConsentOnlyId);
  await expect(page.getByRole('button', { name: 'Start surgery' })).toBeDisabled();
  await expect(page.getByText(/Consent has not been recorded/)).toBeVisible();
  await expect(page.getByRole('button', { name: /emergency override/i })).toHaveCount(0);
});

test('E-B4: reception sees no clinical write control', async ({ page }) => {
  await openSurgery(page, 'Reception', ids.surgeryInTheatreId, '#anaesthesia');
  await expect(page.getByText('Anaesthesia log')).toBeVisible();
  for (const name of [
    /Add drug/,
    /Add vitals/,
    /Add milestone/,
    /Start surgery/,
    /override/i,
    /End procedure/,
  ]) {
    await expect(page.getByRole('button', { name })).toHaveCount(0);
  }
  // A direct write is refused by the server (403, AC-18).
  const { api } = await import('./surgery-helpers');
  const refused = await api.post(
    page,
    `/api/staff/surgeries/${ids.surgeryInTheatreId}/anaesthesia-log/milestones`,
    {
      milestone: 'Induction',
      occurredOn: new Date().toISOString(),
    },
  );
  expect(refused.status).toBe(403);
});

test('E-F1: an administrator creates a campaign; reception books under it; the cases and totals list it', async ({
  page,
}) => {
  await openAs(page, 'Administrator', '/staff/surgeries/campaigns');
  await page.getByRole('button', { name: 'New campaign' }).click();
  const form = page.getByRole('dialog');
  await form.getByLabel('Code').fill('E2E-DRIVE');
  await form.getByLabel('Name').fill('E2E neuter drive');
  await form.getByLabel('First day').fill(dayFromToday(-1));
  await form.getByLabel('Last day').fill(dayFromToday(30));
  await form.getByLabel('Castration').fill('500');
  await form.getByRole('button', { name: 'Save campaign' }).click();
  await expect(page.getByText('E2E neuter drive')).toBeVisible();

  await openAs(page, 'Reception', '/staff/surgeries/new');
  await page.getByLabel(/find the customer/i).fill(ids.customerKamrulMobile);
  await page.keyboard.press('Enter');
  await page.getByRole('button', { name: /kamrul hasan/i }).click();
  await page.getByLabel('Cat', { exact: true }).selectOption({ label: 'Noodle' });
  await page.getByLabel('Procedure', { exact: true }).selectOption({ label: 'Castration' });
  await page.getByLabel(/^date/i).fill(dayFromToday(8));
  await page.getByLabel('Theatre').selectOption({ label: 'Theatre 1' });
  await page.getByLabel('Surgeon').selectOption({ label: 'Dr Rahman' });
  await page.getByLabel('Assistant').selectOption({ label: 'Nurse Tanvir' });
  await page
    .getByRole('group', { name: /available starts/i })
    .getByRole('button')
    .first()
    .click();
  await page.getByLabel('Price').selectOption({ label: 'E2E neuter drive (E2E-DRIVE)' });
  await page.getByRole('button', { name: 'Book surgery' }).click();
  await expect(page.getByText('Note why this cat is eligible for the campaign.')).toBeVisible();
  await page.getByLabel('Why is this cat eligible?').fill('Community cat from the Mirpur colony');
  await page.getByRole('button', { name: 'Book surgery' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Book surgery' }).click();
  await expect(page).toHaveURL(/\/staff\/surgeries\/[0-9a-f-]{36}$/, { timeout: 20_000 });
  await expect(page.getByText(/Campaign price, for information/)).toBeVisible();

  await openAs(page, 'Administrator', '/staff/surgeries/campaigns');
  await page.getByRole('link', { name: 'Cases for E2E-DRIVE' }).click();
  await expect(page.getByText('Noodle').first()).toBeVisible();
  await expect(page.getByLabel('Totals')).toContainText(/1 case · .*500.* campaign/);
});
