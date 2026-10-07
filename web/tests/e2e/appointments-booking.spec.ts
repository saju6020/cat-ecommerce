import { test, expect, signInAsCustomer } from './fixtures';
import { dayLabel, openDay, seedVaccinationService } from './appointments-helpers';

/**
 * Online booking and "My appointments" — HO-APT-002 §16 E-A/E-B series, run against MSW
 * (`NEXT_PUBLIC_USE_MOCKS=true`). The real-API re-runs (a genuine two-customer race, the inbox
 * messages, a clinic pass offering a freed slot) cannot run here and are recorded in the gate
 * hand-off, not faked.
 *
 * The mock's rules this relies on (mocks/handlers/appointments): holds last 70 s; the first claim
 * of any 10:30 time loses to "someone else"; Mon–Thu are open; the cat list is the shared Cat
 * Profiles mock (Whiskers, Tiger, Mittens, …).
 */

const WHISKERS = '20000000-0000-0000-0000-000000000001';

test.describe('Online booking — US-CLN-02, 03', () => {
  test.beforeEach(async ({ page }) => {
    await signInAsCustomer(page);
  });

  test('E-A1: a new customer registers, books a vaccination and sees it under Upcoming within the NFR-USA-1 budget', async ({
    page,
  }) => {
    const budgetMs = 3 * 60_000;
    const started = Date.now();

    await page.context().clearCookies();
    await page.goto('/register');
    await page.getByLabel('Full name').fill('Priya Owner');
    await page.getByLabel(/mobile number/i).fill('01755500111');
    await page.getByLabel(/^password/i).fill('a-genuinely-long-password');
    await expect(page.getByAltText(/security check/i)).toBeVisible();
    await page.getByLabel(/security check/i).fill('ABCDE');
    await page.getByRole('button', { name: 'Create account' }).click();
    await expect(page.getByRole('heading', { name: 'Your account is ready' })).toBeVisible();
    await page.getByRole('link', { name: 'Sign in' }).click();
    await page.getByLabel(/mobile number or email/i).fill('01755500111');
    await page.getByLabel(/^password/i).fill('a-genuinely-long-password');
    await page.getByRole('button', { name: 'Sign in', exact: true }).click();
    await expect(page).toHaveURL(/\/portal\//);

    // A vaccination reminder link: /book?category=Vaccination.
    await seedVaccinationService(page);
    await page.goto('/book?category=Vaccination');
    await page.getByRole('button', { name: /choose whiskers/i }).click();
    await expect(page.getByRole('heading', { name: 'Vaccination services' })).toBeVisible();
    await expect(page.getByText('Pay at the clinic').first()).toBeVisible();
    await page.getByRole('button', { name: /choose vaccination/i }).click();
    await page.getByRole('button', { name: /choose main clinic/i }).click();

    await page.getByRole('button', { name: dayLabel(openDay()) }).click();
    const slotGroup = page.getByRole('group', { name: /times on/i });
    await expect(slotGroup.getByRole('button').first()).toBeVisible();
    await slotGroup.getByRole('button', { name: /^11:00/ }).click();

    await expect(page.getByRole('timer')).toBeVisible();
    await page.getByLabel(/reason for visit/i).fill('Annual booster');
    await page.getByRole('button', { name: /confirm booking/i }).click();
    await expect(page.getByRole('heading', { name: 'Booked.' })).toBeVisible();
    expect(Date.now() - started).toBeLessThan(budgetMs);

    await page.getByRole('link', { name: /view appointment/i }).click();
    await expect(page.getByRole('heading', { name: /vaccination · whiskers/i })).toBeVisible();
    await page.goto('/portal/appointments');
    await expect(page.getByRole('link', { name: /vaccination · whiskers/i }).first()).toBeVisible();
  });

  test('E-A2: a lost race is explained and the customer recovers with another time; a second customer reaches confirm', async ({
    page,
    browser,
  }) => {
    const date = openDay();
    const url = `/book?catId=${WHISKERS}&serviceId=s0000000-0000-0000-0000-00000000a001&branchId=b0000000-0000-0000-0000-00000000a001&date=${date}`;

    // Customer A holds 11:00 and reaches the confirm step.
    await page.goto(url);
    await page
      .getByRole('group', { name: /times on/i })
      .getByRole('button', { name: /^11:00/ })
      .click();
    await expect(page.getByRole('timer')).toBeVisible();

    // Customer B (a separate browser context) goes for the time that is taken.
    const other = await browser.newContext();
    const otherPage = await other.newPage();
    // A page from a hand-made context is not wrapped by fixtures.ts, so wait for MSW itself.
    await otherPage.goto('/sign-in');
    await otherPage.waitForFunction(
      () => (window as unknown as { __mswReady?: boolean }).__mswReady === true,
    );
    await signInAsCustomer(otherPage);
    await otherPage.goto(url);
    await otherPage.waitForFunction(
      () => (window as unknown as { __mswReady?: boolean }).__mswReady === true,
    );
    const list = otherPage.getByRole('group', { name: /times on/i });
    await list.getByRole('button', { name: /^10:30/ }).click();
    await expect(
      otherPage.getByText('Someone has just taken that time. Please choose another.'),
    ).toBeVisible();
    await expect(list).toBeFocused();
    await expect(list.getByRole('button', { name: /^10:30 · Dr Akter/ })).toHaveCount(0);
    await list.getByRole('button', { name: /^11:15/ }).click();
    await expect(otherPage.getByRole('timer')).toBeVisible();
    await other.close();
  });

  test('E-A3: a hold that runs out sends the customer back to the slots with a clear message', async ({
    page,
  }) => {
    await page.clock.install();
    const date = openDay();
    await page.goto(
      `/book?catId=${WHISKERS}&serviceId=s0000000-0000-0000-0000-00000000a001&branchId=b0000000-0000-0000-0000-00000000a001&date=${date}`,
    );
    const list = page.getByRole('group', { name: /times on/i });
    await list.getByRole('button', { name: /^11:00/ }).click();
    await expect(page.getByRole('timer')).toBeVisible();

    await page.clock.fastForward(75_000); // the mock hold lasts 70 s
    await expect(page.getByText('Your hold has ended — please choose a time again.')).toBeVisible();
    await expect(page.getByRole('button', { name: /confirm booking/i })).toHaveCount(0);
    await expect(list.getByRole('button', { name: /^11:00/ })).toBeVisible();
  });
});

test.describe('My appointments — US-CLN-04, 09', () => {
  test.beforeEach(async ({ page }) => {
    await signInAsCustomer(page);
  });

  test('E-B1: reschedule before the cut-off moves it; after the cut-off the clinic contact card replaces the buttons', async ({
    page,
  }) => {
    await page.goto('/portal/appointments');
    await page
      .getByRole('link', { name: /general consultation · momo/i })
      .nth(1)
      .click();
    await page.getByRole('button', { name: 'Reschedule' }).click();
    await page.getByRole('button', { name: dayLabel(openDay(1)) }).click();
    await page
      .getByRole('group', { name: /times on/i })
      .getByRole('button', { name: /^14:00/ })
      .click();
    await page
      .getByRole('dialog')
      .getByRole('button', { name: /move appointment/i })
      .click();
    await expect(page.getByText('Booked').first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Reschedule' })).toBeVisible();

    // The appointment starting in ~2 h is inside the 4 h cut-off.
    await page.goto('/portal/appointments');
    await page
      .getByRole('link', { name: /general consultation · momo/i })
      .first()
      .click();
    await expect(page.getByRole('heading', { name: /can’t be changed online/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /reschedule|cancel appointment/i })).toHaveCount(
      0,
    );
    await expect(page.getByRole('link', { name: /^call/i })).toHaveAttribute('href', /^tel:/);
  });

  test('E-B2: cancelling shows the status as Cancelled', async ({ page }) => {
    await page.goto('/portal/appointments');
    await page
      .getByRole('link', { name: /general consultation · momo/i })
      .nth(1)
      .click();
    await page.getByRole('button', { name: /cancel appointment/i }).click();
    await page
      .getByRole('dialog')
      .getByRole('button', { name: /^cancel appointment$/i })
      .click();
    await expect(page.getByText('Cancelled').first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Reschedule' })).toHaveCount(0);
  });

  test('E-B3 (MSW part): a full day offers the waitlist; an offered time is claimed from its detail page', async ({
    page,
  }) => {
    await page.goto('/portal/appointments');
    await expect(page.getByRole('heading', { name: 'Waitlist' })).toBeVisible();
    await expect(page.getByText(/a time is available — claim by/i)).toBeVisible();
    await page.getByRole('link', { name: /view the offer/i }).click();
    await expect(page.getByRole('timer')).toBeVisible();
    await page.getByRole('button', { name: /confirm booking/i }).click();
    await expect(page.getByText('Booked').first()).toBeVisible();
  });
});
