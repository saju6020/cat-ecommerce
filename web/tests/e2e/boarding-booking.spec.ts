import { test, expect as baseExpect } from './fixtures';
import { IDS, inDays, recordCoreVaccinations } from './boarding-helpers';
import { signInEstablished } from './portal-helpers';

// A cold `next dev` compiles /boarding on the first visit; give the first assertions room for that.
const expect = baseExpect.configure({ timeout: 15_000 });

/**
 * Boarding booking flow — HO-BRD-002 §16 E-A series, against MSW. The mock is the HO-BRD-001
 * §12.4 contract (mocks/handlers/boarding/**); the real-API re-run is `boarding-real-api.spec.ts`.
 */

test.beforeEach(async ({ page }) => {
  await signInEstablished(page);
});

async function fillCareProfile(page: import('@playwright/test').Page, catName: string) {
  const form = page.getByRole('form', { name: new RegExp(`care profile for ${catName}`, 'i') });
  await form.getByLabel('Portion').first().fill('⅓ cup dry');
  await form.getByLabel('What is it?').first().fill('Chicken dry food');
  await form.getByLabel(/yes, treat if needed/i).check();
  await form.getByLabel(/veterinary spending limit/i).fill('2000');
  await form.getByLabel(/^name/i).fill('Sam Rahman');
  await form.getByLabel('Phone').fill('01711000000');
  await form.getByRole('button', { name: /save .* care profile/i }).click();
  await expect(form.getByText(/care profile is saved/i)).toBeVisible();
}

test.describe('Booking flow — US-BRD-02, 03, 05, 06', () => {
  test('E-A2: a 20-night stay shows the weekly basis with the discount itemised, and the review total matches', async ({
    page,
  }) => {
    await page.goto(`/boarding?arrivalOn=${inDays(70)}&departureOn=${inDays(90)}&catCount=1`);
    await expect(page.getByRole('heading', { name: /available units · 20 nights/i })).toBeVisible();
    const standard = page
      .getByRole('listitem')
      .filter({ has: page.getByRole('heading', { name: 'Standard' }) });
    await expect(standard.getByText('Weekly rate', { exact: true })).toBeVisible();
    await expect(standard.getByText(/long-stay discount/i).first()).toBeVisible();
    const totalText = await standard
      .getByText(/^Total/)
      .locator('..')
      .textContent();
    expect(totalText).toMatch(/৳/);

    await standard.getByRole('button', { name: /^choose standard/i }).click();
    await page.getByLabel(/mittens/i).check();
    await page.getByRole('button', { name: 'Continue' }).click();

    await expect(page.getByRole('heading', { name: 'Care profiles' })).toBeVisible();
    await fillCareProfile(page, 'Mittens');
    await page.getByRole('button', { name: /continue to review/i }).click();

    await expect(page.getByRole('heading', { name: 'Review and reserve' })).toBeVisible();
    const reviewTotal = await page
      .getByLabel('Price breakdown')
      .locator('..')
      .getByText(/^Total/)
      .locator('..')
      .textContent();
    expect(reviewTotal).toBe(totalText);

    await page.getByRole('button', { name: 'Reserve', exact: true }).click();
    await expect(page.getByRole('dialog')).toContainText(/reserve standard for mittens/i);
    await page.getByRole('dialog').getByRole('button', { name: 'Reserve' }).click();

    // E-A / U-A9: reserved — not yet confirmed; deposit, countdown, how to pay; no payment control.
    await expect(
      page.getByRole('heading', { name: /reserved — not yet confirmed/i }),
    ).toBeVisible();
    await expect(page.getByText(/deposit to pay/i)).toBeVisible();
    await expect(page.getByTestId('pay-by-left')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'How to pay' })).toBeVisible();
    await expect(page.getByText(/bank transfer/i).first()).toBeVisible();
    await expect(
      page.locator('input[type="tel"][autocomplete*="cc"], input[autocomplete^="cc-"]'),
    ).toHaveCount(0);
    await expect(page.getByRole('button', { name: /pay now|pay deposit|pay online/i })).toHaveCount(
      0,
    );
  });

  test('U-A4: more cats than the unit holds is blocked with the capacity stated', async ({
    page,
  }) => {
    await page.goto(`/boarding?arrivalOn=${inDays(70)}&departureOn=${inDays(72)}&catCount=1`);
    await page.getByRole('button', { name: /^choose standard/i }).click();
    await page.getByLabel(/mittens/i).check();
    await page.getByLabel(/biscuit/i).check();
    await expect(page.getByRole('alert').filter({ hasText: /holds up to 1 cat/i })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Continue' })).toBeDisabled();
  });

  test('E-A4 (first half): a full range offers nearest dates and the waitlist', async ({
    page,
  }) => {
    // Three cats need the Family unit, which the in-house family holds today.
    await page.goto(`/boarding?arrivalOn=${inDays(1)}&departureOn=${inDays(3)}&catCount=3`);
    await expect(page.getByText('No units free for those dates.')).toBeVisible();
    await expect(
      page.getByRole('list', { name: 'Nearest dates' }).getByRole('listitem').first(),
    ).toBeVisible();
    await page.getByRole('button', { name: 'Join the waitlist' }).first().click();
    await expect(page.getByRole('dialog')).toContainText(/first come, first served/i);
    await page.getByRole('dialog').getByRole('button', { name: 'Join the waitlist' }).click();
    await expect(page.getByText(/you’re on the waitlist/i)).toBeVisible();
  });
});

test.describe('Vaccination gate — US-BRD-04', () => {
  test('E-A1: a blocked cat is explained, the booking is kept, and it resumes after the vaccination', async ({
    page,
  }) => {
    await page.goto(`/boarding?arrivalOn=${inDays(80)}&departureOn=${inDays(83)}&catCount=1`);
    await page.getByRole('button', { name: /^choose deluxe/i }).click();
    await page.getByLabel(/tiger/i).check();
    await page.getByRole('button', { name: 'Continue' }).click();

    await expect(page.getByRole('heading', { name: /vaccination needs attention/i })).toBeVisible();
    await expect(page.getByText(/rabies vaccination/i).first()).toBeVisible();
    await expect(page.getByText(/we’ve kept your booking/i)).toBeVisible();
    // The step is not lost: stayId is in the URL.
    await expect(page).toHaveURL(/stayId=/);

    // "Book a vaccination" (HO-APT-002 /book) — then the vet records it.
    await page.getByRole('link', { name: /book a vaccination for tiger/i }).click();
    await expect(page).toHaveURL(/\/book\?catId=/);
    await recordCoreVaccinations(page, IDS.catTigerId);
    await page.goBack();

    await page.getByRole('button', { name: 'Resume' }).click();
    await expect(page.getByRole('heading', { name: 'Care profiles' })).toBeVisible();
  });

  test('U-A6: an enquiry blocked earlier resumes from My stays at the gate step', async ({
    page,
  }) => {
    await page.goto('/portal/boarding');
    const draft = page.getByRole('listitem').filter({ hasText: 'Tiger' });
    await expect(draft.getByText(/vaccination/i).first()).toBeVisible();
    await draft.getByRole('link', { name: /resume/i }).click();
    await expect(page).toHaveURL(new RegExp(`stayId=${IDS.stayEnquiryBlocked}`));
    await expect(page.getByRole('heading', { name: /vaccination needs attention/i })).toBeVisible();
  });
});

test.describe('Sign-in return — owner decision 2026-10-14', () => {
  test('a signed-out visitor to /boarding returns to the same search after signing in', async ({
    page,
    context,
  }) => {
    await context.clearCookies();
    await page.goto(`/boarding?arrivalOn=${inDays(70)}&departureOn=${inDays(72)}&catCount=1`);
    await expect(page).toHaveURL(/\/sign-in\?next=/);
    await page.getByLabel(/mobile number or email/i).fill('signed-in-customer@example.com');
    await page.getByLabel(/^password/i).fill('Password123!');
    await page.getByRole('button', { name: 'Sign in', exact: true }).click();
    await expect(page).toHaveURL(/\/boarding\?arrivalOn=/);
    await expect(page.getByRole('heading', { name: /available units/i })).toBeVisible();
  });
});
