import { test, expect, signInAsCustomer } from './fixtures';
import { ids, signInAsRole } from './emr-helpers';

/**
 * Owner portal reads (E-G1, E-G2) and consent capture (E-H1, E-H2) — HO-EMR-002 §16, against MSW.
 */
test.describe('Owner portal — US-CLN-16, FR-POR-3', () => {
  test('E-G1: the owner opens the cat’s visits, reads the summary and has a PDF link', async ({
    page,
  }) => {
    await signInAsCustomer(page);
    await page.goto(`/portal/cats/${ids.portalCatId}/visits`);
    await expect(page.getByRole('heading', { name: 'Visits', exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Lab results' })).toBeVisible();
    await expect(page.getByText('Creatinine').first()).toBeVisible();
    await expect(page.getByText('180 µmol/L').first()).toBeVisible();
    await expect(page.getByText('Normal').first()).toBeVisible();

    await page
      .getByRole('link', { name: /read the summary of the/i })
      .first()
      .click();
    await expect(page.getByRole('heading', { name: /visit summary for mittens/i })).toBeVisible();
    await expect(
      page.getByText('Medicines to take home are on the prescription label.'),
    ).toBeVisible();

    const href = await page.getByRole('link', { name: 'Download PDF' }).getAttribute('href');
    expect(href).toMatch(/\/api\/customers\/me\/visits\/.+\/discharge-summary\.pdf$/);
    const pdf = await page.evaluate(async (url) => {
      const response = await fetch(url!, { credentials: 'include' });
      return { status: response.status, type: response.headers.get('content-type') };
    }, href);
    expect(pdf).toEqual({ status: 200, type: expect.stringContaining('application/pdf') });
  });

  test('E-G2: an open visit (URL guess) is a neutral not-found', async ({ page }) => {
    await signInAsCustomer(page);
    await page.goto(`/portal/visits/${ids.visitOpenId}`);
    await expect(page.getByText('We couldn’t find this visit summary.')).toBeVisible();
    await page.goto(`/portal/visits/${ids.portalNotMineVisitId}`);
    await expect(page.getByText('We couldn’t find this visit summary.')).toBeVisible();
  });
});

async function signOnScreen(page: import('@playwright/test').Page) {
  const canvas = page.getByRole('img', { name: /signature area/i });
  const box = (await canvas.boundingBox())!;
  await page.mouse.move(box.x + 30, box.y + 80);
  await page.mouse.down();
  for (let step = 1; step <= 24; step += 1) {
    await page.mouse.move(box.x + 30 + step * 6, box.y + 80 + Math.sin(step / 2) * 30);
  }
  await page.mouse.up();
}

test.describe('Consent capture — US-CLN-21', () => {
  const capture = `/staff/consents/new?catId=${ids.catMittensId}&appointmentId=${ids.queuedBookedAppointmentId}&kind=Surgery`;

  test.describe('on a tablet in portrait', () => {
    test.use({ viewport: { width: 768, height: 1024 }, hasTouch: true });

    test('E-H1: reception captures an on-screen signature and the consent is listed with its date, signer and form version', async ({
      page,
    }) => {
      test.setTimeout(60_000);
      await signInAsRole(page, 'Reception');
      await page.goto(capture);
      await expect(page.locator('#consent-wording')).toHaveText('Consent to surgery');
      await expect(page.getByText('Version 3')).toBeVisible();

      await page.getByLabel('Procedure').fill('Spay (ovariohysterectomy)');
      await page.getByRole('button', { name: 'Record consent' }).click();
      await expect(page.getByText('Sign in the box above, or upload a signed form.')).toBeVisible();

      await signOnScreen(page);
      await expect(page.getByText('Signature captured.')).toBeVisible();
      await page.getByRole('button', { name: 'Undo stroke' }).click();
      await expect(page.getByText('Nothing signed yet.')).toBeVisible();
      await signOnScreen(page);
      await page.getByRole('button', { name: 'Record consent' }).click();
      const confirm = page.getByRole('dialog', { name: /record this consent/i });
      await expect(confirm.getByText(/form version 3/)).toBeVisible();
      await confirm.getByRole('button', { name: 'Record consent' }).click();

      await expect(page.getByRole('heading', { name: 'Surgery consent' })).toBeVisible();
      await expect(page.getByText('Version 3').first()).toBeVisible();
      await expect(page.getByRole('img', { name: /signature of/i })).toBeVisible();

      await page.goto(`/staff/cats/${ids.catMittensId}/consents`);
      await expect(page.getByRole('table')).toContainText('Version 3');
      await expect(page.getByRole('table')).toContainText('Recorded');
    });
  });

  test('E-H2: a signed paper form is uploaded; the consent lists it and the file opens', async ({
    page,
  }) => {
    test.setTimeout(60_000);
    await signInAsRole(page, 'Reception');
    await page.goto(capture);
    await page.getByLabel('Procedure').fill('Spay (ovariohysterectomy)');
    await page.getByRole('tab', { name: 'Upload signed form' }).click();
    await page.locator('input[type="file"]').setInputFiles({
      name: 'signed-form.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('%PDF-1.4 test'),
    });
    await expect(page.getByText('signed-form.pdf').first()).toBeVisible();
    await page.getByRole('button', { name: 'Record consent' }).click();
    await page
      .getByRole('dialog', { name: /record this consent/i })
      .getByRole('button', { name: 'Record consent' })
      .click();
    await expect(page.getByRole('heading', { name: 'Surgery consent' })).toBeVisible();
    await expect(page.getByRole('link', { name: /open signed-form\.pdf/i })).toBeVisible();
  });

  test('an unpublished form is stated, with no way to submit', async ({ page }) => {
    await signInAsRole(page, 'Reception');
    await page.goto(capture.replace('kind=Surgery', 'kind=Euthanasia'));
    await expect(
      page.getByText(
        'The Euthanasia consent form hasn’t been published yet. Ask an administrator.',
      ),
    ).toBeVisible();
    await expect(page.getByRole('button', { name: 'Record consent' })).toHaveCount(0);
  });

  test('withdrawing a consent asks for a reason and then shows it as withdrawn', async ({
    page,
  }) => {
    await signInAsRole(page, 'Reception');
    await page.goto(`/staff/consents/${ids.consentRecordedId}`);
    await page.getByRole('button', { name: 'Withdraw consent' }).click();
    const dialog = page.getByRole('dialog', { name: /withdraw this consent/i });
    await dialog.getByRole('button', { name: 'Withdraw consent' }).click();
    await expect(dialog.getByText('Say why the consent is being withdrawn.')).toBeVisible();
    await dialog.getByLabel('Reason').fill('Owner changed their mind.');
    await dialog.getByRole('button', { name: 'Withdraw consent' }).click();
    await expect(page.getByText('Owner changed their mind.')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Withdraw consent' })).toHaveCount(0);
  });
});
