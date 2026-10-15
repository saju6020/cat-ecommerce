import { test, expect } from './fixtures';
import { IDS } from './boarding-helpers';
import { signInEstablished } from './portal-helpers';

/**
 * The owner's side of a stay — HO-BRD-002 §16 (U-B series as e2e, E-B2, E-D2, E-E1 portal half),
 * against MSW. The seeded stays are `mocks/handlers/boarding/seed.ts`.
 */

test.beforeEach(async ({ page }) => {
  await signInEstablished(page);
});

test.describe('My stays — US-BRD-04 AC2, US-BRD-06', () => {
  test('lists upcoming stays and enquiries with Resume, the in-house tab, and the waitlist', async ({
    page,
  }) => {
    await page.goto('/portal/boarding');
    await expect(page.getByRole('heading', { name: 'Enquiries to finish' })).toBeVisible();
    await expect(
      page.getByRole('listitem').filter({ hasText: 'Tiger' }).getByRole('link', { name: 'Resume' }),
    ).toBeVisible();
    await expect(page.getByRole('link', { name: /book a stay/i })).toBeVisible();
    // The other customer's stay is never listed.
    await expect(page.getByText('Ghost')).toHaveCount(0);

    await page.getByRole('tab', { name: 'In house' }).click();
    await expect(page.getByRole('link', { name: /noodle and pepper/i })).toBeVisible();

    await expect(page.getByRole('heading', { name: 'Waitlist' })).toBeVisible();
    await page.getByRole('button', { name: 'Withdraw' }).click();
    await expect(page.getByRole('dialog')).toContainText(/stop being told/i);
    await page.getByRole('dialog').getByRole('button', { name: 'Withdraw' }).click();
    await expect(page.getByRole('heading', { name: 'Waitlist' })).toHaveCount(0);
  });

  test('U-A6: Resume from a blocked enquiry lands on the vaccination step', async ({ page }) => {
    await page.goto('/portal/boarding');
    await page
      .getByRole('listitem')
      .filter({ hasText: 'Tiger' })
      .getByRole('link', { name: 'Resume' })
      .click();
    await expect(page).toHaveURL(new RegExp(`stayId=${IDS.stayEnquiryBlocked}`));
    await expect(page.getByRole('heading', { name: /vaccination needs attention/i })).toBeVisible();
  });
});

test.describe('The stay page — U-B1, U-B6', () => {
  test('a reserved stay shows the deposit, the countdown, how to pay and no payment control', async ({
    page,
  }) => {
    await page.goto(`/portal/boarding/${IDS.stayReservedSoon}`);
    await expect(page.getByText(/awaiting payment/i).first()).toBeVisible();
    await expect(page.getByTestId('pay-by-left')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'How to pay' })).toBeVisible();
    await expect(page.getByRole('button', { name: /pay now|pay deposit|pay online/i })).toHaveCount(
      0,
    );
    await expect(page.getByText(/reserved — not yet confirmed/i).first()).toBeVisible();
  });

  test('the deposit states read differently: payment being checked', async ({ page }) => {
    await page.goto(`/portal/boarding/${IDS.stayPendingApproval}`);
    await expect(page.getByText('Payment being checked').first()).toBeVisible();
    await expect(page.getByText(/will not expire while it is/i)).toBeVisible();
    await expect(page.getByTestId('pay-by-left')).toHaveCount(0);
  });

  test('Cancel states the deposit outcome the server returned and cancels', async ({ page }) => {
    await page.goto(`/portal/boarding/${IDS.stayReservedSoon}`);
    await page.getByRole('button', { name: /cancel this stay/i }).click();
    await expect(page.getByRole('dialog')).toContainText(/no deposit has been paid/i);
    await page.getByRole('dialog').getByRole('button', { name: 'Cancel the stay' }).click();
    await expect(page.getByText('Cancelled').first()).toBeVisible();
  });

  test("another customer's stay is a plain not-found, never inferred data", async ({ page }) => {
    await page.goto(`/portal/boarding/${IDS.stayOtherCustomer}`);
    await expect(page.getByText(/we can’t find that stay/i)).toBeVisible();
    await expect(page.getByText('Ghost')).toHaveCount(0);
  });
});

test.describe('Diary, incident, approval, summary — US-BRD-12, 13, 14, 17', () => {
  test('U-B2: the diary groups entries by day and shows an honest media placeholder', async ({
    page,
  }) => {
    await page.goto(`/portal/boarding/${IDS.stayInHouseFamily}/diary`);
    await expect(page.getByRole('heading', { name: 'Diary' })).toBeVisible();
    await expect(page.getByRole('list', { name: /days, newest first/i })).toBeVisible();
    await expect(page.getByText(/recorded without a photo you can open/i).first()).toBeVisible();
    await expect(page.locator('main').getByRole('img')).toHaveCount(0);
  });

  test('U-B3: I’ve seen this acknowledges the incident and it reads Acknowledged', async ({
    page,
  }) => {
    await page.goto(`/portal/boarding/${IDS.stayInHouseFamily}/incidents/${IDS.incidentMedium}`);
    await page.getByRole('button', { name: /i’ve seen this/i }).click();
    await expect(page.getByText(/acknowledged at \d\d:\d\d\./i)).toBeVisible();
  });

  test('U-B4: a vet cost shows the limit, what is used and what remains, and Approve asks for a confirm', async ({
    page,
  }) => {
    await page.goto(`/portal/boarding/${IDS.stayInHouseFamily}/approvals/${IDS.approvalPending}`);
    await expect(page.getByText(/the vet would like to treat pepper/i)).toBeVisible();
    await expect(page.getByText(/your limit for this stay is/i)).toBeVisible();
    await page.getByRole('button', { name: 'Approve' }).click();
    await expect(page.getByRole('dialog')).toContainText(/added to your stay/i);
    await page.getByRole('dialog').getByRole('button', { name: 'Approve' }).click();
    await expect(page.getByText(/you approved this/i)).toBeVisible();
  });

  test('U-B5: the summary shows the weight change, belongings, the signature and the charges without a payment control', async ({
    page,
  }) => {
    await page.goto(`/portal/boarding/${IDS.stayCheckedOut}/summary`);
    await expect(page.getByRole('heading', { name: 'Stay summary' })).toBeVisible();
    await expect(page.getByText(/change \+0\.2 kg/i)).toBeVisible();
    await expect(page.getByText(/not returned \(left at home/i)).toBeVisible();
    await expect(page.getByText(/sam rahman,/i)).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Charges' })).toBeVisible();
    await expect(page.getByRole('link', { name: /your invoice inv-brd-0048/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /pay/i })).toHaveCount(0);
  });
});

test.describe('Reservation lapse — E-B2', () => {
  test('an unpaid reservation past its time is cancelled and the unit is released', async ({
    page,
  }) => {
    test.setTimeout(180_000);
    await page.goto(`/portal/boarding/${IDS.stayReservedSoon}`);
    await expect(page.getByTestId('pay-by-left')).toBeVisible();
    // The seeded deposit is due in ~70 s; the countdown announces expiry, and the next read shows the lapse.
    await expect(page.getByTestId('pay-by-left')).toContainText(/time is up/i, {
      timeout: 120_000,
    });
    await page.goto('/portal/boarding');
    await page.getByRole('tab', { name: 'Past' }).click();
    await expect(page.getByRole('link', { name: /bean/i })).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText('Cancelled').first()).toBeVisible();
  });
});
