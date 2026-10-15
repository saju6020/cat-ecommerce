import { test, expect } from './fixtures';
import { accountsFixtureIds } from '../../mocks/handlers/areas/accounts';
import { appStorageSnapshot, ids, signInAsRole } from './emr-helpers';

/**
 * Consultation workspace — HO-EMR-002 §16 E-A series, against MSW (the real-API re-run is
 * `emr-real-api.spec.ts`). Treatments (E-C2, and the consumable in E-A1) are deferred with the
 * backend's Phase F.
 */
test.describe('Consultation workspace — US-CLN-11, 12, 17, 22', () => {
  test('E-A1: nurse records vitals (one warning confirmed); vet consults, orders, results, follows up, finalises and completes', async ({
    page,
  }) => {
    test.setTimeout(120_000);
    // --- the nurse, from the queue card
    await signInAsRole(page, 'Nurse');
    await page.goto('/staff/front-desk?tab=queue');
    const bookedCard = page
      .getByRole('listitem')
      .filter({ hasText: /Booked/ })
      .filter({ hasText: 'Mittens' });
    await bookedCard.getByRole('button', { name: 'Record vitals for Mittens' }).click();
    const dialog = page.getByRole('dialog', { name: /record vitals — mittens/i });
    await expect(dialog.getByLabel('Weight, kilograms')).toBeVisible();
    await dialog.getByLabel('Weight, kilograms').fill('3.85');
    await dialog.getByLabel('Temperature, degrees Celsius').fill('42');
    await expect(dialog.getByText(/outside the usual range 35\.0–41\.5 °C/i)).toBeVisible();
    await dialog.getByRole('button', { name: 'Save vitals' }).click();
    const confirm = page.getByRole('dialog', { name: /outside the usual range/i });
    await expect(confirm.getByText(/Temperature/)).toBeVisible();
    await confirm.getByRole('button', { name: 'Confirm and save' }).click();
    await expect(bookedCard.getByText(/Vitals recorded/)).toBeVisible();

    // --- the vet opens the consultation
    await signInAsRole(page, 'Vet');
    await page.goto('/staff/front-desk?tab=queue');
    await page
      .getByRole('listitem')
      .filter({ hasText: /Booked/ })
      .filter({ hasText: 'Mittens' })
      .getByRole('button', { name: 'Open consultation for Mittens' })
      .click();
    await expect(page).toHaveURL(/\/staff\/visits\/[0-9a-f-]{36}$/);
    await expect(page.getByRole('heading', { name: /consultation — mittens/i })).toBeVisible();
    await expect(page.getByText('3.85 kg').first()).toBeVisible();
    await expect(page.getByText(/confirmed outside usual range/i)).toBeVisible();

    await page
      .getByLabel('Subjective')
      .fill('Vomiting twice. <script>alert(1)</script> **not bold**');
    await page.getByLabel('Assessment').fill('Mild gastritis.');
    await page.getByLabel('Plan').fill('Bland diet for three days.');
    await page.getByLabel('Home-care instructions').fill('Small frequent meals.');
    await page.getByRole('combobox', { name: /add a diagnosis/i }).fill('vomit');
    await page.getByRole('option', { name: /GIT-VOM/ }).click();
    await page.getByRole('button', { name: 'Save note' }).click();
    await expect(page.getByText('Note saved', { exact: true })).toBeVisible();

    // --- order blood work and enter results
    await page.getByRole('button', { name: 'Order test' }).click();
    const order = page.getByRole('dialog', { name: /order a test/i });
    await order.getByLabel('Creatinine').check();
    await order.getByRole('button', { name: 'Place order' }).click();
    await expect(page.getByRole('button', { name: /Blood work/ }).first()).toBeVisible();
    await page
      .getByRole('button', { name: /Blood work/ })
      .first()
      .click();
    const drawer = page.getByRole('dialog', { name: 'Diagnostic order' });
    await drawer.getByText('Enter results').click();
    await drawer.getByLabel('Result, µmol/L').first().fill('300');
    await expect(drawer.getByText('High ↑').first()).toBeVisible();
    await drawer.getByRole('button', { name: 'Save results' }).click();
    await expect(drawer.getByRole('table').getByText('High ↑')).toBeVisible();
    await page.keyboard.press('Escape');

    // --- follow-up reminder, then finalise
    await page.getByRole('button', { name: 'Send a reminder' }).click();
    const when = new Date(Date.now() + 14 * 86_400_000).toISOString().slice(0, 10);
    await page.getByLabel('Remind on').fill(when);
    await page.getByRole('button', { name: 'Set reminder' }).click();
    await expect(page.getByText(/Reminder on/)).toBeVisible();

    await page.getByRole('button', { name: 'Finalise', exact: true }).click();
    const finalise = page.getByRole('dialog', { name: /finalise mittens/i });
    await expect(finalise.getByText(/become permanent/)).toBeVisible();
    await finalise.getByRole('button', { name: 'Finalise visit' }).click();
    await expect(page.getByText(/Finalised by/)).toBeVisible();
    // The record shows the plain text literally and has no note inputs.
    await expect(
      page.getByText('<script>alert(1)</script> **not bold**', { exact: false }),
    ).toBeVisible();
    await expect(page.getByLabel('Subjective')).toHaveCount(0);

    await page.getByRole('button', { name: 'Complete appointment' }).click();
    await expect(page.getByText('Appointment completed', { exact: true })).toBeVisible();
    await page.getByRole('link', { name: 'Discharge summary' }).click();
    await expect(page.getByRole('heading', { name: /visit summary for mittens/i })).toBeVisible();
  });

  test('E-A2: the vet types, leaves, and the text is restored from the server working copy (never browser storage)', async ({
    page,
  }) => {
    await signInAsRole(page, 'Vet');
    await page.goto(`/staff/visits/${ids.visitEmptyId}`);
    await page.getByLabel('Plan').fill('Recheck the ear in two weeks.');
    // The 3 s debounce, then a reload stands in for closing the tab.
    await expect(page.getByText(/Draft saved/)).toBeVisible({ timeout: 10_000 });
    expect(await appStorageSnapshot(page)).not.toContain('Recheck the ear');

    await page.reload();
    await expect(page.getByLabel('Plan')).toHaveValue('Recheck the ear in two weeks.');
    await expect(
      page.getByText(/were restored from your working copy on the server/),
    ).toBeVisible();
    expect(await appStorageSnapshot(page)).not.toContain('Recheck the ear');

    await page.getByRole('button', { name: 'Revert to saved note' }).click();
    const confirm = page.getByRole('dialog', { name: /revert to the saved note/i });
    await confirm.getByRole('button', { name: 'Revert to saved note' }).click();
    await expect(page.getByLabel('Plan')).toHaveValue('');
  });

  test('E-A3: the idle lock closes over the page; the typed text is still on screen and the next edit auto-saves', async ({
    page,
  }) => {
    test.setTimeout(60_000);
    await signInAsRole(page, 'Vet');
    await page.goto(`/staff/visits/${ids.visitEmptyId}`);
    const plan = page.getByLabel('Plan');
    await plan.fill('Offer wet food only for a week.');

    const token = (await page.context().cookies()).find(
      (c) => c.name === 'pc_staff_session',
    )!.value;
    await page.evaluate((forced) => {
      (
        window as unknown as { __forceStaffIdleExpired?: (t: string) => void }
      ).__forceStaffIdleExpired?.(forced);
      (
        window as unknown as { __invalidateQueries?: (k: readonly unknown[]) => void }
      ).__invalidateQueries?.(['session', 'staff']);
    }, token);

    const lock = page.getByRole('dialog');
    await expect(lock.getByRole('heading')).toContainText(/signed out after/i);
    await lock.getByLabel(/mobile number or email/i).fill(accountsFixtureIds.nurseIdentifier);
    await lock.getByLabel(/^password/i).fill(accountsFixtureIds.nursePassword);
    await lock.getByRole('button', { name: /^sign in$/i }).click();
    await expect(lock).toBeHidden();

    await expect(page).toHaveURL(new RegExp(ids.visitEmptyId));
    await expect(plan).toHaveValue('Offer wet food only for a week.');
    await plan.fill('Offer wet food only for a week, then recheck.');
    await expect(page.getByText(/Draft saved/)).toBeVisible({ timeout: 10_000 });
    await page.reload();
    await expect(page.getByLabel('Plan')).toHaveValue(
      'Offer wet food only for a week, then recheck.',
    );
  });

  test('E-A4: reception is refused; an administrator reads but has no Finalise (AC-18)', async ({
    page,
  }) => {
    await signInAsRole(page, 'Reception');
    await page.goto(`/staff/visits/${ids.visitOpenId}`);
    await expect(page.getByText(/you don.t have permission to do this/i)).toBeVisible();
    await expect(page.getByRole('button', { name: 'Finalise' })).toHaveCount(0);

    await signInAsRole(page, 'Administrator');
    await page.goto(`/staff/visits/${ids.visitOpenId}`);
    await expect(page.getByRole('heading', { name: /consultation — whiskers/i })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Finalise' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Save note' })).toHaveCount(0);
    await expect(page.getByLabel('Subjective')).toHaveCount(0);
  });

  test('E-A5: after finalising, an addendum is added and the original text is unchanged', async ({
    page,
  }) => {
    // The mock clinic is per browser tab (sessionStorage), so this test owns its fixture.
    const original = 'Vomiting twice yesterday. Eating normally today.';
    const addendum = `Owner phoned again: eating well (${Date.now()}).`;
    await signInAsRole(page, 'Vet');
    await page.goto(`/staff/visits/${ids.visitFinalisedId}`);
    const subjective = page.getByText(original, { exact: true });
    await expect(subjective).toHaveCount(1);
    await page.getByLabel('Addendum', { exact: true }).fill(addendum);
    await page.getByRole('button', { name: 'Add addendum' }).click();
    const confirm = page.getByRole('dialog', { name: /add this addendum/i });
    await expect(confirm.getByText('Addenda cannot be edited or removed.')).toBeVisible();
    await confirm.getByRole('button', { name: 'Add addendum' }).click();
    const list = page.getByRole('heading', { name: 'Addenda' }).locator('xpath=ancestor::section[1]');
    await expect(list.getByText(addendum, { exact: true })).toBeVisible();
    await expect(subjective).toHaveCount(1);
    await expect(subjective).toBeVisible();
    // Still there, and still unchanged, after a reload.
    await page.reload();
    await expect(page.getByText(original, { exact: true })).toHaveCount(1);
    await expect(page.getByText(addendum, { exact: true })).toHaveCount(1);
  });
});
