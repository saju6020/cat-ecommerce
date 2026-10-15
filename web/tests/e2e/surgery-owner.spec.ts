import { test, expect } from './fixtures';
import { ids, openAs, stubPrint } from './surgery-helpers';

/** HO-SRG-002 §16 E-E2 (owner side, against MSW) and U-F1 in the browser. */
test.setTimeout(120_000);

test('an owner sees the surgery before, during and after, with nothing to pay', async ({
  page,
}) => {
  await openAs(page, 'Customer', '/portal/surgeries');
  await expect(page.getByRole('heading', { name: 'Surgeries', level: 1 })).toBeVisible();
  const before = page.locator(`a[href$="${ids.surgeryOwnerBeforeId}"]`);
  await expect(before).toContainText('Momo — Dental extractions');
  await expect(page.getByRole('link', { name: /Momo — Mass removal/ })).toBeVisible();

  await before.click();
  await expect(page.getByText(/Please bring Momo to Uttara by/)).toBeVisible();
  await expect(page.getByText(/No food after 10 pm/)).toBeVisible();
  await expect(page.getByRole('button', { name: /pay/i })).toHaveCount(0);

  await page.goto(`/portal/surgeries/${ids.surgeryRecoveryId}`);
  await expect(page.getByText(/Momo is in our care\. Call Uttara/)).toBeVisible();

  await page.goto('/portal/surgeries?tab=past');
  await page.getByRole('link', { name: /Momo — Spay/ }).click();
  await expect(page.getByText(/Keep your cat indoors and warm/)).toBeVisible();
  await expect(page.getByText(/Wound check — /)).toBeVisible();
  await expect(page.getByText(/Suture removal — /)).toBeVisible();
  await expect(page.getByRole('link', { name: 'Book follow-up' })).toHaveAttribute(
    'href',
    /\/book\?catId=.*FollowUp/,
  );
  await stubPrint(page);
  await page.getByRole('button', { name: 'Print' }).click();
  expect(
    await page.evaluate(() => (window as unknown as { __printCount: number }).__printCount),
  ).toBe(1);
});

test('another customer’s surgery is a plain not-found', async ({ page }) => {
  await openAs(page, 'Customer', `/portal/surgeries/${ids.surgeryScheduledTodayId}`);
  await expect(page.getByRole('heading', { name: /couldn’t find that surgery/ })).toBeVisible();
});
