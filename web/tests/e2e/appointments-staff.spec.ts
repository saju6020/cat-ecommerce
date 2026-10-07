import { test, expect, signInAsStaff } from './fixtures';
import { accountsFixtureIds } from '../../mocks/handlers/areas/accounts';
import { openDay } from './appointments-helpers';

/**
 * Staff booking, appointment detail and the flagged list — HO-APT-002 §16 E-C1 and E-E2, against
 * MSW. E-E2's first step (approving a vet's leave in HO-ADM-002 so the server flags the appointment)
 * needs the real API; here the mock ships an appointment already flagged for leave.
 */

test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

test('E-C1: reception books by phone for a customer with a no-show; the badge is seen first', async ({
  page,
}) => {
  await page.goto('/staff/appointments/new');
  await page.getByLabel(/find the customer/i).fill(accountsFixtureIds.customerMobile);
  await page.keyboard.press('Enter');
  await page.getByRole('button', { name: /sam rahman/i }).click();
  await expect(page.getByText('2 no-shows')).toBeVisible();

  await expect(page.getByLabel('Cat', { exact: true })).toHaveValue(/.+/);
  await page.getByLabel('Service').selectOption({ label: 'General consultation' });
  await page.getByLabel(/^date/i).fill(openDay(8));
  await expect(
    page.getByLabel('Vet or nurse').getByRole('option', { name: 'Dr Rahman' }),
  ).toBeAttached();
  await page.getByLabel('Vet or nurse').selectOption({ label: 'Dr Rahman' });
  const time = page.getByLabel(/^time/i);
  await expect(time.getByRole('option')).not.toHaveCount(1);
  await time.selectOption({ index: 3 });
  await page.getByLabel(/reason for visit/i).fill('Phone booking: sneezing');
  await page.getByRole('button', { name: /^book$/i }).click();
  await page
    .getByRole('dialog')
    .getByRole('button', { name: /^book$/i })
    .click();

  await expect(page).toHaveURL(/\/staff\/appointments\/[0-9a-f-]{36}$/, { timeout: 20_000 });
  await expect(page.getByRole('heading', { name: 'General consultation' })).toBeVisible();
  await expect(page.getByText('Phone booking: sneezing')).toBeVisible();
});

test('E-E2: a leave-flagged appointment is on the Flagged list with its reason, and the banner is on its detail', async ({
  page,
}) => {
  await page.goto('/staff/appointments/flagged');
  // A table from md up, a card list below it — either way the cat, the reason and the actions.
  await expect(
    page
      .getByText(/is on leave that day/i)
      .filter({ visible: true })
      .first(),
  ).toBeVisible();
  await page.getByRole('link', { name: 'Whiskers' }).filter({ visible: true }).first().click();
  await expect(page.getByText('This appointment needs a new time')).toBeVisible();
  await expect(page.getByText(/is on leave that day/i)).toBeVisible();
  // Only the actions the server allows for a booked appointment.
  await expect(page.getByRole('button', { name: 'Move…' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Complete' })).toHaveCount(0);

  // Reschedule from the list: the Move… dialog states old and new, then the row leaves the list.
  await page.goto('/staff/appointments/flagged');
  await page.getByRole('button', { name: /reschedule whiskers/i }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel(/^time/i).fill('15:30');
  await dialog.getByRole('button', { name: /review move/i }).click();
  await expect(dialog.getByText(/move whiskers’s general/i)).toBeVisible();
  await dialog.getByRole('button', { name: /move appointment/i }).click();
  await expect(page.getByText('Nothing needs rescheduling.')).toBeVisible();
});
