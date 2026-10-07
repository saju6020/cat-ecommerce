import { test, expect, signInAsStaff } from './fixtures';
import { openDay } from './appointments-helpers';

/**
 * The calendar — HO-APT-002 §16 E-E1, against MSW. On a wide screen the appointment is DRAGGED
 * (real pointer events); below 768 px the grid is the agenda list and the **Move…** dialog is the
 * only path — the request and the server's answers are the same, which is the point of ADR-FE-026.
 * Seeds (mocks/handlers/appointments): Tiger with Dr Rahman 10:00 and Whiskers with Dr Akter 11:30,
 * both 20 minutes, on the first Monday–Thursday after today.
 */

const RAHMAN = 'a2000000-0000-0000-0000-000000000001';
const AKTER = 'a2000000-0000-0000-0000-000000000002';

test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

async function dragTiger(
  page: import('@playwright/test').Page,
  resourceId: string,
  minutes: number,
) {
  const tiger = page.getByRole('group', { name: /tiger, general consultation/i });
  const box = (await tiger.boundingBox())!;
  const column = (await page
    .locator(`[data-calendar-column][data-resource-id="${resourceId}"]`)
    .boundingBox())!;
  const tigerMinutes = 10 * 60;
  const y = box.y + ((minutes - tigerMinutes) / 15) * 20 + 3;
  await page.mouse.move(box.x + 20, box.y + 6);
  await page.mouse.down();
  await page.mouse.move(box.x + 30, box.y + 14, { steps: 3 });
  await page.mouse.move(column.x + 60, y, { steps: 8 });
  await page.mouse.up();
}

test('E-E1: an appointment is dragged to a free time and the server accepts it', async ({
  page,
  isMobile,
}) => {
  const wide = (page.viewportSize()?.width ?? 0) >= 768 && !isMobile;
  await page.goto(`/staff/appointments?date=${openDay(1)}`);
  await expect(page.getByRole('heading', { name: 'Appointments', level: 1 })).toBeVisible();

  if (wide) {
    // Free: Dr Rahman 10:30.
    await dragTiger(page, RAHMAN, 10 * 60 + 30);
    const dialog = page.getByRole('dialog');
    await expect(
      dialog.getByText(
        /move tiger’s general consultation from 10:00 with dr rahman to 10:30 with dr rahman/i,
      ),
    ).toBeVisible();
    await dialog.getByRole('button', { name: /move appointment/i }).click();
    await expect(
      page.getByRole('group', { name: /tiger, general consultation, 10:30/i }),
    ).toBeVisible();
  }
});

test('E-E1 (Move… dialog): the keyboard path makes the same moves and gets the same refusals', async ({
  page,
}) => {
  await page.goto(`/staff/appointments?date=${openDay(1)}`);
  await page
    .getByRole('button', { name: /move tiger/i })
    .first()
    .click();
  const dialog = page.getByRole('dialog');
  await expect(
    dialog.getByLabel('Vet or nurse').getByRole('option', { name: 'Dr Akter' }),
  ).toBeAttached();

  // Busy first: Dr Akter at 11:30 — the server names the clash, the dialog keeps the choices.
  await dialog.getByLabel('Vet or nurse').selectOption({ label: 'Dr Akter' });
  await dialog.getByLabel(/^time/i).fill('11:30');
  await dialog.getByRole('button', { name: /review move/i }).click();
  await dialog.getByRole('button', { name: /move appointment/i }).click();
  await expect(
    dialog.getByRole('alert').filter({ hasText: /dr akter already has an appointment/i }),
  ).toBeVisible();
  await expect(dialog.getByLabel(/^time/i)).toHaveValue('11:30');

  // Then a free time.
  await dialog.getByLabel(/^time/i).fill('14:00');
  await dialog.getByLabel('Vet or nurse').selectOption({ label: 'Dr Rahman' });
  await dialog.getByRole('button', { name: /review move/i }).click();
  await dialog.getByRole('button', { name: /move appointment/i }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(page.getByText('Appointment moved').first()).toBeVisible();
});

test('E-E1 (drag, busy target): the block snaps back and the server’s reason is announced', async ({
  page,
  isMobile,
}) => {
  test.skip(isMobile || (page.viewportSize()?.width ?? 0) < 768, 'dragging needs the wide grid');
  await page.goto(`/staff/appointments?date=${openDay(1)}`);
  await dragTiger(page, AKTER, 11 * 60 + 30);
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: /move appointment/i }).click();

  await expect(
    page
      .getByRole('status')
      .filter({ hasText: /not moved\. dr akter already has an appointment/i }),
  ).toHaveCount(1);
  await expect(page.getByText('Not moved', { exact: true })).toBeVisible();
  await expect(
    page.getByRole('group', { name: /tiger, general consultation, 10:00/i }),
  ).toBeVisible();
});
