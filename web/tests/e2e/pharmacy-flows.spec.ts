import { test, expect } from './fixtures';
import { warmRoutes } from './inventory-helpers';
import {
  NOT_A_WITNESS,
  WITNESS_NURSE,
  WITNESS_VET,
  addAllergy,
  enterDose,
  fillComposer,
  ids,
  gotoSigned,
  openAs,
  pickMedicine,
  readPrescriptionNumber,
  stubPrint,
  tickAllWarnings,
  witnessWith,
} from './pharmacy-helpers';
import { PHARMACY_ROUTES } from './pharmacy-routes';

/**
 * HO-PHM-002 §16, run against MSW (the real-API re-run is separate). Roles: the mock knows an
 * administrator and a nurse; `signInAsRole` turns the nurse account into a Vet or a Receptionist.
 * Mittens is the owner's cat (Sam Rahman); Tiger's last weight is old (stale); Whiskers has an active
 * prednisolone course.
 */
test.setTimeout(120_000);

// A cold `next dev` compiles each route on its first request; do that once, up front, so a test is not
// racing a compile (and the mock's idle clock is not running while it waits).
test.beforeAll(async ({ request }) => {
  test.setTimeout(600_000);
  await warmRoutes(
    request,
    PHARMACY_ROUTES.map((route) => route.path),
  );
});

// The flows read tables by row; below 768 px those are cards, which pharmacy-a11y-responsive.spec.ts covers.
test.beforeEach(({}, testInfo) => {
  test.skip(
    testInfo.project.name !== 'chromium',
    'flows run on the desktop project; 360 px has its own spec',
  );
});

const OWNER_RX = (catId: string) => `/staff/cats/${catId}/prescriptions`;

test('E-A1 / AC-4: a vet prescribes, reception dispenses and prints the label, the owner sees it', async ({
  page,
}) => {
  // The vet reaches the composer through the cat's tab and its visit picker (OQ-5).
  await openAs(page, 'Vet', OWNER_RX(ids.catMittensId));
  await page.getByRole('button', { name: 'New prescription' }).click();
  const picker = page.getByRole('dialog', { name: /which visit/i });
  await picker.getByRole('radio').first().check();
  await picker.getByRole('button', { name: 'Continue' }).click();
  await expect(page.getByRole('form', { name: 'Prescription' })).toBeVisible();
  await expect(page.getByText('Visit open')).toBeVisible();

  // An above-range dose: warned, acknowledged, issued.
  await pickMedicine(page, 'melox', /Meloxicam oral suspension/);
  await fillComposer(page, {
    frequency: 'OnceDaily',
    duration: '7',
    quantity: '10',
    instructions: 'Give 0.4 ml by mouth once daily.',
  });
  await enterDose(page, '0.4', 'Ml');
  const panel = page.getByRole('complementary', { name: 'Safety check' });
  await expect(panel.getByText('Above range', { exact: true })).toBeVisible();
  await expect(panel.getByText(/mg\/kg/).first()).toBeVisible();
  await page.getByRole('button', { name: 'Issue prescription' }).click();
  await expect(page.getByText('Tick to confirm you have reviewed this warning.')).toBeVisible();
  await tickAllWarnings(page);
  await page.getByRole('button', { name: 'Issue prescription' }).click();
  await expect(page).toHaveURL(/\/prescriptions\/[0-9a-f-]{36}$/, { timeout: 20_000 });
  const meloxicamNumber = await readPrescriptionNumber(page);

  // Amoxicillin for the penicillin-allergic cat: a blocking warning that needs a reason.
  await gotoSigned(page, 'Vet', `${OWNER_RX(ids.catMittensId)}/new?visitId=${ids.visitTemplateId}`);
  // The cat mock keeps its state for the page's lifetime, so the allergy is recorded after this load.
  await addAllergy(page, ids.catMittensId, 'Penicillin');
  await pickMedicine(page, 'amox', /Amoxicillin tablets/);
  await fillComposer(page, {
    frequency: 'TwiceDaily',
    duration: '5',
    quantity: '10',
    instructions: 'One tablet twice daily.',
  });
  await enterDose(page, '1', 'Tablet');
  await expect(panel.getByText('Blocking')).toBeVisible();
  await expect(panel.getByRole('listitem').first()).toContainText('Allergy — Penicillin');
  await page.getByRole('checkbox', { name: /I have reviewed: Allergy/ }).check();
  await page.getByRole('button', { name: 'Issue prescription' }).click();
  await expect(page.getByText('Say why you are prescribing despite the allergy.')).toBeVisible();
  await page
    .getByLabel(/why are you prescribing despite the allergy/i)
    .fill('Culture-proven; no alternative.');
  await page.getByRole('button', { name: 'Issue prescription' }).click();
  await expect(page).toHaveURL(/\/prescriptions\/[0-9a-f-]{36}$/, { timeout: 20_000 });
  await expect(
    page.getByText('Reason given despite the allergy: “Culture-proven; no alternative.”'),
  ).toBeVisible();

  // Reception dispenses the meloxicam from the queue with the FEFO batch, then prints the label.
  await openAs(page, 'Reception', '/staff/pharmacy');
  await page.getByRole('link', { name: meloxicamNumber }).click();
  await expect(page.getByRole('heading', { name: 'Dispense' })).toBeVisible();
  await expect(page.getByRole('checkbox', { name: 'Use lot L4410' })).toBeChecked();
  await expect(page.getByRole('checkbox', { name: 'Use lot L4551' })).toBeChecked();
  await expect(page.getByText(/10\.00 of 10\.00 ml selected/)).toBeVisible();
  await page.getByRole('button', { name: 'Dispense' }).click();
  await expect(page.getByText('Dispensed — supply 1 of 1.')).toBeVisible();

  await page.getByRole('link', { name: 'Print label' }).click();
  await expect(page.getByRole('article', { name: /Medicine label for Mittens/ })).toBeVisible();
  await expect(page.getByRole('article')).toContainText('0.40 ml by mouth once daily');
  await stubPrint(page);
  await page.getByRole('button', { name: 'Print' }).click();
  await expect(page.getByText('Printed 1 time so far.')).toBeVisible();
  expect(
    await page.evaluate(() => (window as unknown as { __printCount: number }).__printCount),
  ).toBe(1);

  // E-B3: a reprint is counted.
  await page.getByRole('button', { name: 'Print' }).click();
  await expect(page.getByText('Printed 2 times so far.')).toBeVisible();
  await page.getByRole('link', { name: 'Back' }).click();
  await expect(page.getByText('2 times')).toBeVisible();

  // The owner sees it under Active, with its dose, how often and the vet.
  await openAs(page, 'Customer', '/portal/prescriptions');
  const row = page.getByRole('listitem').filter({ hasText: '0.40 ml once daily' });
  await expect(row).toContainText('Meloxicam oral suspension');
  await expect(row).toContainText('Prescribed by: Dr Akter');
  await expect(row).toContainText('days left');
});

test('E-A2: reception cannot prescribe — the page says so before anything is typed', async ({
  page,
}) => {
  await openAs(page, 'Reception', `${OWNER_RX(ids.catWhiskersId)}/new?visitId=${ids.visitOpenId}`);
  await expect(page.getByText(/You don.t have permission to do this/)).toBeVisible();
  await expect(page.getByRole('form', { name: 'Prescription' })).toHaveCount(0);
});

test('E-A3: a stale weight is recorded from the panel and the prescription is issued', async ({
  page,
}) => {
  await openAs(page, 'Vet', `${OWNER_RX(ids.catTigerId)}/new?visitId=${ids.visitDraftNewerId}`);
  await pickMedicine(page, 'melox', /Meloxicam oral suspension/);
  await fillComposer(page, {
    frequency: 'OnceDaily',
    duration: '5',
    quantity: '10',
    instructions: 'Give 0.3 ml once daily.',
  });
  await enterDose(page, '0.3', 'Ml');
  const panel = page.getByRole('complementary', { name: 'Safety check' });
  await expect(panel.getByText(/The latest weight is out of date/)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Issue prescription' })).toBeDisabled();

  await panel.getByRole('button', { name: 'Record weight' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel(/^weight/i).fill('5.9');
  await dialog.getByRole('button', { name: 'Record weight' }).click();
  await expect(panel.getByText(/Latest weight/)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Issue prescription' })).toBeEnabled();
  await page.getByRole('button', { name: 'Issue prescription' }).click();
  await expect(page).toHaveURL(/\/prescriptions\/[0-9a-f-]{36}$/, { timeout: 20_000 });
});

test('E-B1: only expired stock — no dispense button, the server’s message', async ({ page }) => {
  await openAs(page, 'Reception', '/staff/pharmacy');
  await page.getByRole('link', { name: 'RX-2026-000123' }).click();
  const notice = page.getByRole('alert', { name: 'This cannot be dispensed' });
  await expect(notice).toContainText('is expired');
  await expect(notice).toContainText('There is no way to override this here.');
  await expect(page.getByRole('button', { name: /dispense/i })).toHaveCount(0);
  await expect(page.getByRole('checkbox')).toHaveCount(0);
});

test('E-B2 / E-C1: a controlled drug needs a vet or nurse witness; the register shows it; a vet adds a correction', async ({
  page,
}) => {
  await openAs(page, 'Reception', '/staff/pharmacy');
  await page.getByRole('link', { name: 'RX-2026-000122' }).click();
  await page.getByRole('button', { name: /Dispense \(witness needed\)/ }).click();

  // A witness who is neither a vet nor a nurse is refused, in the dialog, with the password cleared.
  await witnessWith(page, NOT_A_WITNESS, 'Confirm and dispense');
  const dialog = page.getByRole('dialog', { name: /witness needed/i });
  await expect(
    dialog.getByText(
      'The witness could not be verified. Ask them to enter their own sign-in details.',
    ),
  ).toBeVisible();
  await expect(dialog.getByLabel(/witness password/i)).toHaveValue('');
  await expect(dialog.getByLabel(/witness mobile number or email/i)).toBeFocused();

  // A nurse witness is accepted.
  await witnessWith(page, WITNESS_NURSE, 'Confirm and dispense');
  await expect(page.getByText('Dispensed — supply 1 of 1.')).toBeVisible();

  // The register has the entry (a vet reads it).
  await openAs(page, 'Vet', '/staff/pharmacy/controlled-drugs');
  await expect(page.getByRole('link', { name: /Dispensing RX-2026-000122 #1/ })).toBeVisible();

  // E-C1: a correction is a new entry beneath the original; the original is unchanged.
  await page.getByRole('link', { name: '#1200' }).first().click();
  await expect(page.getByRole('heading', { name: 'Register entry #1200' })).toBeVisible();
  await expect(page.getByRole('region', { name: /Corrections \(1\)/ })).toBeVisible();
  await page.getByRole('link', { name: 'Add correction' }).click();
  await page.getByLabel(/corrected dose text/i).fill('0.2 ml twice daily for 5 days (corrected)');
  await page.getByLabel(/reason for the correction/i).fill('Dose text had the wrong duration.');
  await page.getByRole('button', { name: 'Review and add correction' }).click();
  await expect(page.getByRole('dialog')).toContainText(
    /This adds entry #\d+ correcting #1200; the original stays as it is\./,
  );
  await page.getByRole('button', { name: 'Continue to witness' }).click();
  await witnessWith(page, WITNESS_VET, 'Confirm and add correction');
  await expect(page).toHaveURL(/controlled-drugs\/[0-9a-f-]{36}$/, { timeout: 20_000 });
  await expect(page.getByRole('region', { name: /Corrections \(2\)/ })).toBeVisible();
  await expect(page.getByText('Dose text had the wrong duration.')).toBeVisible();
  // The original is untouched and still has no edit control.
  await expect(page.getByRole('button', { name: /edit|delete/i })).toHaveCount(0);
});

test('E-D1 / E-D2: the owner requests a repeat and staff dispense it; an ended prescription says why not', async ({
  page,
}) => {
  await openAs(page, 'Customer', `/portal/prescriptions/${ids.rxMittensActiveId}`);
  await page.getByRole('button', { name: /Request a repeat/ }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel(/collect from/i).selectOption(ids.mainBranchId);
  await dialog.getByLabel(/note for the pharmacy/i).fill('After 5 pm please');
  await dialog.getByRole('button', { name: 'Request repeat' }).click();
  await expect(
    page.getByText('Requested. We’ll message you when it’s ready to collect.'),
  ).toBeVisible();
  await expect(page.getByRole('button', { name: 'Withdraw request' })).toBeVisible();

  // The same prescription from the refill reminder opens straight on the dialog only when allowed.
  await gotoSigned(page, 'Customer', `/portal/prescriptions/${ids.rxMittensEndedId}?action=repeat`);
  await expect(page.getByText(/This prescription could be repeated until/)).toBeVisible();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Book a consultation' })).toHaveAttribute(
    'href',
    `/book?catId=${ids.catMittensId}`,
  );

  await openAs(page, 'Reception', '/staff/pharmacy');
  const repeats = page.getByRole('region', { name: 'Repeat requests' });
  await expect(repeats.getByText('After 5 pm please')).toBeVisible();
  await repeats.getByRole('link', { name: 'RX-2026-000101' }).click();
  await expect(page.getByText('Repeat request from the owner')).toBeVisible();
  await page.getByRole('button', { name: 'Dispense' }).click();
  await expect(page.getByText('Dispensed — supply 2 of 3.')).toBeVisible();
});

test('E-E1: a vet authorises a diet from a visit and the owner sees it under Authorised foods', async ({
  page,
}) => {
  await openAs(
    page,
    'Vet',
    `/staff/cats/${ids.catMittensId}/diet-authorisations/new?visitId=${ids.visitTemplateId}`,
  );
  await expect(page.getByText('Visit open')).toBeVisible();
  await page.getByRole('combobox', { name: /^food/i }).fill('urinary');
  await page.getByRole('option', { name: /Urinary Support/ }).click();
  await page.getByRole('button', { name: '90 days' }).click();
  await page.getByRole('button', { name: 'Authorise diet' }).click();
  await expect(page).toHaveURL(/diet-authorisations$/, { timeout: 20_000 });
  await expect(page.getByRole('row', { name: /Urinary Support/ }).first()).toContainText('Active');

  await openAs(page, 'Customer', '/portal/prescriptions?tab=diets');
  const foods = page.getByRole('list', { name: 'Authorised foods' });
  await expect(
    foods.getByRole('listitem').filter({ hasText: 'Urinary Support' }).first(),
  ).toContainText('Active');
  await expect(foods).toContainText('You can buy this for Mittens in the shop.');
});

test('E-F1: an administrator creates a per-kg medicine with an interaction and a SKU, then marks it controlled', async ({
  page,
}) => {
  await openAs(page, 'Administrator', '/staff/admin/formulary/new');
  await page.getByLabel(/^code/i).fill('TRM-TAB-50');
  await page.getByRole('textbox', { name: /^name/i }).fill('Tramadol tablets');
  await page.getByLabel(/^active ingredients/i).fill('tramadol');
  await page.getByLabel(/^active ingredients/i).press('Enter');
  await page.getByRole('combobox', { name: 'Form', exact: true }).selectOption('Tablet');
  await page.getByLabel(/dispensing unit/i).selectOption('Tablet');
  await page.getByRole('textbox', { name: /^strength/i }).fill('50');
  await page.getByRole('combobox', { name: 'Strength unit' }).selectOption('Mg');
  await page.getByRole('combobox', { name: 'Per unit' }).selectOption('Tablet');
  await expect(page.getByText(/Strength preview/)).toContainText('50 mg per 1 tablet');
  await page.getByRole('checkbox', { name: 'Oral' }).check();
  await page.getByLabel(/default route/i).selectOption('Oral');
  await page.getByRole('textbox', { name: /minimum dose/i }).fill('1');
  await page.getByRole('textbox', { name: /maximum dose/i }).fill('4');
  await page.getByRole('combobox', { name: /link a clinic sku/i }).fill('tramadol');
  await page.getByRole('option', { name: /Tramadol 50 mg tablets/ }).click();

  await page.getByRole('button', { name: /Add interaction/ }).click();
  await page.getByRole('combobox', { name: /other medicine/i }).fill('melox');
  await page.getByRole('option', { name: /Meloxicam oral suspension/ }).click();
  await page.getByLabel('Severity').selectOption('Major');
  await page
    .getByRole('textbox', { name: 'Note', exact: true })
    .fill('Raises seizure risk with some NSAIDs.');
  await page.getByRole('button', { name: 'Create medicine' }).click();
  await expect(page).toHaveURL(/\/staff\/admin\/formulary\/[0-9a-f-]{36}$/, { timeout: 20_000 });

  // Controlled is a separate, confirmed action.
  await page.getByRole('button', { name: /Mark as controlled/ }).click();
  const confirm = page.getByRole('dialog');
  await expect(confirm.getByRole('button', { name: 'Mark as controlled' })).toBeDisabled();
  await confirm.getByLabel(/schedule/i).fill('Schedule 4');
  await confirm.getByRole('button', { name: 'Mark as controlled' }).click();
  await expect(page.getByText('Controlled — Schedule 4')).toBeVisible();

  await gotoSigned(page, 'Administrator', '/staff/admin/formulary');
  const row = page.getByRole('row', { name: /Tramadol tablets/ });
  await expect(row).toContainText('Controlled');
  await expect(row).toContainText('Stocked');

  // Prescribable: it appears in the vet's picker with the Controlled badge.
  await openAs(page, 'Vet', `${OWNER_RX(ids.catWhiskersId)}/new?visitId=${ids.visitOpenId}`);
  await page.getByRole('combobox', { name: /^medicine/i }).fill('tramad');
  const option = page.getByRole('option', { name: /Tramadol tablets/ });
  await expect(option).toContainText('Controlled');
});
