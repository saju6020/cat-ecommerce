import { test, expect } from './fixtures';
import { dayFromToday, ids, openAs } from './surgery-helpers';

/**
 * HO-SRG-002 §16 E-A1, E-A2 and E-A3 (the browser-side parts), against MSW. E-A1's calendar block and
 * E-A3's two real browser contexts need the real API (the mock keeps each browser's own state) — they
 * are in the real-API run recorded in docs/test-cases/TC-HO-SRG-002.md.
 */
test.setTimeout(120_000);

async function fillBooking(
  page: import('@playwright/test').Page,
  options: { date: string; assistant?: string },
) {
  await page.getByLabel(/find the customer/i).fill(ids.customerFarhanaMobile);
  await page.keyboard.press('Enter');
  await page.getByRole('button', { name: /farhana yasmin/i }).click();
  await page.getByLabel('Cat', { exact: true }).selectOption({ label: 'Pepper' });
  await page.getByLabel('Procedure', { exact: true }).selectOption({ label: 'Spay' });
  await page.getByLabel(/^date/i).fill(options.date);
  await page.getByLabel('Theatre').selectOption({ label: 'Theatre 1' });
  await page.getByLabel('Surgeon').selectOption({ label: 'Dr Rahman' });
  await page.getByLabel('Assistant').selectOption({ label: options.assistant ?? 'Nurse Tanvir' });
}

test('E-A1: reception books a spay; the surgery page opens', async ({ page }) => {
  await openAs(page, 'Reception', '/staff/surgeries/new');
  await fillBooking(page, { date: dayFromToday(5) });
  const starts = page.getByRole('group', { name: /available starts/i });
  await starts.getByRole('button').first().click();
  await expect(page.getByText(/for information — paid at the clinic/).first()).toBeVisible();
  await page.getByRole('button', { name: 'Book surgery' }).click();
  const confirm = page.getByRole('dialog');
  await expect(confirm).toContainText(
    /Book Pepper’s spay on .*Theatre 1, Dr Rahman with Nurse Tanvir\?/,
  );
  await confirm.getByRole('button', { name: 'Book surgery' }).click();
  await expect(page).toHaveURL(/\/staff\/surgeries\/[0-9a-f-]{36}$/, { timeout: 20_000 });
  await expect(page.getByRole('heading', { level: 1, name: /Spay — Pepper/ })).toBeVisible();
  await expect(page.getByRole('list', { name: 'Progress' })).toContainText('Scheduled');
});

test('E-A2: an assistant who is busy is explained, and another start books', async ({ page }) => {
  await openAs(page, 'Reception', '/staff/surgeries/new');
  // Nurse Sadia has a vaccination 10:00-10:30 two days from now.
  await fillBooking(page, { date: dayFromToday(2), assistant: 'Nurse Sadia' });
  const explainer = page.getByRole('heading', { name: 'Why some times are missing' }).locator('..');
  await expect(explainer).toContainText('Nurse Sadia: vaccination appointment');
  await expect(explainer).toContainText('10:00–10:30');
  const starts = page.getByRole('group', { name: /available starts/i });
  await expect(starts.getByRole('button', { name: /^10:00–11:00/ })).toHaveCount(0);
  await expect(starts.getByRole('button', { name: /^09:30–10:30/ })).toHaveCount(0);
  await expect(starts.getByRole('button', { name: /^10:30–11:30/ })).toHaveCount(1); // free: it starts as she finishes
  await starts.getByRole('button').first().click();
  await page.getByRole('button', { name: 'Book surgery' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Book surgery' }).click();
  await expect(page).toHaveURL(/\/staff\/surgeries\/[0-9a-f-]{36}$/, { timeout: 20_000 });
});

test('E-A3: a lost race is explained, and the time is gone after the refresh', async ({ page }) => {
  await openAs(page, 'Reception', '/staff/surgeries/new');
  await fillBooking(page, { date: dayFromToday(3) });
  const starts = page.getByRole('group', { name: /available starts/i });
  const race = starts.getByRole('button', { name: /^16:30–17:30/ });
  await race.click();
  await page.getByRole('button', { name: 'Book surgery' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Book surgery' }).click();
  const summary = page.getByRole('alert').filter({ hasText: 'Someone has just booked that time.' });
  await expect(summary).toBeVisible();
  await expect(starts.getByRole('button', { name: /^16:30–17:30/ })).toHaveCount(0);
});

test('a day with no common start offers the next free day', async ({ page }) => {
  await openAs(page, 'Reception', '/staff/surgeries/new');
  await fillBooking(page, { date: dayFromToday(ids.noStartDayOffset) });
  await expect(page.getByText(/No time on .* when all three are free\./)).toBeVisible();
  await page.getByRole('button', { name: 'Use that day' }).click();
  await expect(page.getByRole('group', { name: /available starts/i })).toBeVisible();
});
