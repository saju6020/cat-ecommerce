import { test, expect, signInAsStaff } from './fixtures';
import { administrationFixtureIds } from '../../mocks/handlers/admin';
import { test as plainTest, expect as baseExpect, type Page } from '@playwright/test';

/**
 * HO-ADM-002-FU1 §16 — the branch Calendar tab, run against MSW (E-CAL2, E-CAL3, E-CAL4, E-CAL6,
 * E-CAL7; E-B1's calendar half is appended to `admin-functional.spec.ts`, and E-CAL5/E-CAL6's
 * axe and overflow sweeps are the calendar route added to `admin-a11y-responsive.spec.ts`).
 * Run with `--workers=1`: one dev server, one shared mock store.
 */
const { mainBranchId, londonBranchId } = administrationFixtureIds;
const calendarUrl = (branchId: string, month: string) =>
  `/staff/admin/branches/${branchId}/calendar?month=${month}`;

const grid = (page: Page) => page.getByRole('grid');
const cell = (page: Page, name: RegExp) => grid(page).getByRole('gridcell', { name });

/** The day's status sentence, from the grid cell (768 px and wider) or the day list item (below
 *  768 px) - the page shows exactly one of the two. `name` is the long date, e.g.
 *  `Friday 1 January 2027`; the list shows it as `Fri 1 Jan`. */
async function expectDay(page: Page, name: string, text: RegExp): Promise<void> {
  const narrow = (page.viewportSize()?.width ?? 1280) < 768;
  if (!narrow) {
    await expect(cell(page, new RegExp(name))).toHaveAccessibleName(text);
    return;
  }
  const [weekday, day, month] = name.split(' ');
  const short = `${weekday!.slice(0, 3)} ${day} ${month!.slice(0, 3)}`;
  await expect(
    page.getByRole('listitem').filter({ hasText: new RegExp(`^${short}`) }),
  ).toContainText(text);
}

async function openCalendar(page: Page, branchId: string, month: string): Promise<void> {
  await page.goto(calendarUrl(branchId, month));
  await expect(page.getByRole('heading', { name: 'Calendar' })).toBeVisible();
}

test('E-CAL2: seeded main branch shows the holiday, weekly closures, Saturday hours and the half-day, and Next month updates the URL', async ({
  page,
}) => {
  await signInAsStaff(page);
  await openCalendar(page, mainBranchId, '2027-01');
  await expect(page.getByText(/Times in Asia\/Dhaka\./)).toBeVisible();
  await expectDay(page, 'Friday 1 January 2027', /Closed — Public holiday: New Year's Day/);
  await expectDay(page, 'Sunday 3 January 2027', /Closed \(regular weekly closure\)/);
  await expectDay(page, 'Saturday 2 January 2027', /Open 10:00–14:00/);

  await page.getByRole('link', { name: 'Next month' }).click();
  await expect(page).toHaveURL(/month=2027-02$/);
  await expect(page.getByRole('heading', { name: 'February 2027' })).toBeVisible();
  await expectDay(
    page,
    'Monday 15 February 2027',
    /Reduced hours: 09:00–13:00\. Closed 13:00–18:00: Staff training \(half day\)/,
  );
});

test.describe('E-CAL3: London across DST, with the browser in another zone', () => {
  test.use({ timezoneId: 'America/Los_Angeles' });

  test('Fri 29 Oct (BST) and Mon 1 Nov (GMT) both read Open 08:00–17:00', async ({ page }) => {
    await signInAsStaff(page);
    await openCalendar(page, londonBranchId, '2027-10');
    await expect(page.getByText(/Times in Europe\/London\./)).toBeVisible();
    await expectDay(page, 'Friday 29 October 2027', /Open 08:00–17:00/);
    await page.getByRole('link', { name: 'Next month' }).click();
    await expect(page).toHaveURL(/month=2027-11$/);
    await expectDay(page, 'Monday 1 November 2027', /Open 08:00–17:00/);
  });
});

test.describe('E-CAL4: keyboard at 1024 px', () => {
  test.use({ viewport: { width: 1024, height: 768 } });

  test('one Tab stop, arrows / End / PageDown, visible focus, focus lands on the same day next month', async ({
    page,
  }) => {
    test.setTimeout(60_000);
    await signInAsStaff(page);
    await openCalendar(page, mainBranchId, '2027-02');
    await expect(cell(page, /Monday 1 February 2027/)).toBeVisible();

    const focusedDate = () =>
      page.evaluate(() => document.activeElement?.getAttribute('data-date') ?? null);

    // Tab from the month links into the grid: it lands on the 1st (today is not in this month).
    await page.getByRole('link', { name: 'Next month' }).focus();
    for (let i = 0; i < 3 && (await focusedDate()) === null; i += 1)
      await page.keyboard.press('Tab');
    expect(await focusedDate()).toBe('2027-02-01');
    const outline = await page.evaluate(
      () => getComputedStyle(document.activeElement as Element).outlineStyle,
    );
    expect(outline).not.toBe('none');

    await page.keyboard.press('ArrowRight');
    expect(await focusedDate()).toBe('2027-02-02');
    await page.keyboard.press('ArrowDown');
    expect(await focusedDate()).toBe('2027-02-09');
    await page.keyboard.press('End');
    expect(await focusedDate()).toBe('2027-02-14');

    await page.keyboard.press('PageDown');
    await expect(page).toHaveURL(/month=2027-03$/);
    await expect(cell(page, /Sunday 14 March 2027/)).toBeFocused();

    // One Tab stop: a single Tab leaves the grid.
    await page.keyboard.press('Tab');
    expect(await focusedDate()).toBeNull();
    await expect(grid(page).locator(':focus')).toHaveCount(0);
  });
});

test.describe('E-CAL6: list below 768 px, grid from 768 px', () => {
  test('at 360 the day list is visible and the grid hidden', async ({ page }) => {
    await signInAsStaff(page);
    await page.setViewportSize({ width: 360, height: 780 });
    await openCalendar(page, mainBranchId, '2027-02');
    const list = page.getByRole('list', { name: 'February 2027' });
    await expect(list).toBeVisible();
    await expect(list.getByRole('listitem')).toHaveCount(28);
    await expect(grid(page)).toBeHidden();
  });

  test('at 768 and wider the grid is visible and the list hidden', async ({ page }) => {
    await signInAsStaff(page);
    await page.setViewportSize({ width: 768, height: 1024 });
    await openCalendar(page, mainBranchId, '2027-02');
    await expect(grid(page)).toBeVisible();
    await expect(page.getByRole('list', { name: 'February 2027' })).toBeHidden();
  });
});

test('E-CAL7: a new closure closes the day, cancelling it reopens the day, without a reload', async ({
  page,
}) => {
  test.setTimeout(60_000);
  await signInAsStaff(page);
  const reason = `E2E calendar closure ${Date.now().toString().slice(-6)}`;
  // Wednesday 2 May 2029 — far from any seeded or other test's data.
  await openCalendar(page, londonBranchId, '2029-05');
  await expectDay(page, 'Wednesday 2 May 2029', /Open 08:00–17:00/);

  await page.getByRole('link', { name: 'Closures' }).click();
  await page.getByLabel(/start date/i).fill('2029-05-02');
  await page.getByLabel(/end date/i).fill('2029-05-02');
  await page.getByLabel(/^kind$/i).selectOption('Closure');
  await page.getByLabel(/reason/i).fill(reason);
  await page.getByRole('button', { name: /^add$/i }).click();
  await expect(page.getByText(reason)).toBeVisible();

  await page.goBack();
  await expect(page).toHaveURL(/month=2029-05$/);
  await expectDay(page, 'Wednesday 2 May 2029', new RegExp(`Closed — ${reason}`));

  await page.goForward();
  await page
    .getByRole('listitem')
    .filter({ hasText: reason })
    .getByRole('button', { name: 'Cancel' })
    .click();
  await page.getByRole('button', { name: 'Cancel closure' }).click();
  await expect(
    page.getByRole('listitem').filter({ hasText: reason }).getByText('Cancelled'),
  ).toBeVisible();

  await page.goBack();
  // The Closures form's unsaved-changes guard may ask before leaving (its start/end dates are
  // pre-filled); confirm it if it does.
  await page
    .getByRole('button', { name: 'Leave' })
    .click({ timeout: 2000 })
    .catch(() => undefined);
  await expectDay(page, 'Wednesday 2 May 2029', /Open 08:00–17:00/);
});

/**
 * E-CAL8 - the real-API run (HO-ADM-002-FU1 §16): E-B1 and E-CAL2's 1 Jan / Sunday / Saturday checks
 * against a branch this test creates itself (a fresh database has no hours on the seeded MAIN), then
 * deactivates. Opt-in: needs the API running, `NEXT_PUBLIC_USE_MOCKS=false`, and the e2e administrator in
 * `catecommerce_dev` (`E2E_STAFF_ADMIN_IDENTIFIER` / `E2E_STAFF_PASSWORD` from git-ignored
 * `web/.env.e2e.local`, exported to the shell).
 *
 *   E2E_REAL_CAL8=1 PORT=3114 npx playwright test tests/e2e/admin-branch-calendar.spec.ts -g E-CAL8 --project=chromium --workers=1
 */
plainTest.describe('E-CAL8: real API', () => {
  const identifier = process.env.E2E_STAFF_ADMIN_IDENTIFIER ?? '';
  const password = process.env.E2E_STAFF_PASSWORD ?? '';
  plainTest.skip(
    process.env.E2E_REAL_CAL8 !== '1' || !identifier || !password,
    'Set E2E_REAL_CAL8=1 with E2E_STAFF_ADMIN_IDENTIFIER and E2E_STAFF_PASSWORD (real API).',
  );
  const expectReal = baseExpect.configure({ timeout: 20_000 });

  plainTest(
    'E-B1 and the E-CAL2 checks on a branch the run creates, then deactivate it',
    async ({ page }) => {
      plainTest.setTimeout(240_000);
      await page.goto('/staff/sign-in');
      await page.waitForLoadState('networkidle');
      await page.getByLabel(/mobile number or email/i).fill(identifier);
      await page.getByLabel(/^password/i).fill(password);
      await page.getByRole('button', { name: 'Sign in', exact: true }).click();
      await expectReal(page).not.toHaveURL(/\/staff\/sign-in/);

      const stamp = Date.now().toString(36).toUpperCase();
      await page.goto('/staff/admin/branches/new');
      await page.getByLabel('Code').fill(`E2ECAL${stamp}`);
      await page.getByLabel('Name').fill(`E2E calendar branch ${stamp}`);
      await page.getByLabel(/time zone/i).selectOption('Europe/London');
      const created = page.waitForResponse(
        (r) => r.url().includes('/api/admin/branches') && r.request().method() === 'POST',
      );
      await page.getByRole('button', { name: /create branch/i }).click();
      const response = await created;
      expect(response.status()).toBeLessThan(300);
      const { branchId } = (await response.json()) as { branchId: string };
      const base = `/staff/admin/branches/${branchId}`;
      let deactivated = false;
      // The create form redirects to the new branch; let that settle before navigating on (an
      // in-flight redirect aborts a parallel goto in dev).
      await page.waitForURL(new RegExp(`${branchId}$`), { timeout: 20_000 }).catch(() => undefined);

      try {
        await page.goto(`${base}/hours`);
        for (const [day, opens, closes] of [
          ['Monday', '08:00', '17:00'],
          ['Friday', '08:00', '17:00'],
          ['Saturday', '10:00', '14:00'],
        ] as const) {
          const group = page.getByRole('group', { name: day });
          await group.getByRole('button', { name: /add interval/i }).click();
          await group.getByLabel(/opens \(europe\/london\)/i).fill(opens);
          await group.getByLabel(/closes \(europe\/london\)/i).fill(closes);
        }
        await page.getByRole('button', { name: /save opening hours/i }).click();
        await expectReal(page.getByText('Opening hours saved.')).toBeVisible();

        await page.goto(`${base}/closures`);
        for (const [date, reason] of [
          ['2028-12-25', 'Christmas Day'],
          ['2027-01-01', "New Year's Day"],
        ] as const) {
          await page.getByLabel(/start date/i).fill(date);
          await page.getByLabel(/end date/i).fill(date);
          await page.getByLabel(/^kind$/i).selectOption('PublicHoliday');
          await page.getByLabel(/reason/i).fill(reason);
          await page.getByRole('button', { name: /^add$/i }).click();
          await expectReal(page.getByText(reason, { exact: true })).toBeVisible();
        }

        // E-B1 calendar half.
        await openCalendar(page, branchId, '2028-12');
        await expectReal(page.getByText(/Times in Europe\/London/)).toBeVisible();
        await expectReal(cell(page, /Monday 25 December 2028/)).toHaveAccessibleName(
          /Closed — Public holiday: Christmas Day/,
        );
        await expectReal(cell(page, /Monday 18 December 2028/)).toHaveAccessibleName(
          /Open 08:00–17:00/,
        );
        await expectReal(cell(page, /Tuesday 26 December 2028/)).toHaveAccessibleName(
          /Closed \(regular weekly closure\)/,
        );

        // E-CAL2's 1 Jan / Sunday / Saturday checks.
        await openCalendar(page, branchId, '2027-01');
        await expectReal(cell(page, /Friday 1 January 2027/)).toHaveAccessibleName(
          /Closed — Public holiday: New Year's Day/,
        );
        await expectReal(cell(page, /Saturday 2 January 2027/)).toHaveAccessibleName(
          /Open 10:00–14:00/,
        );
        await expectReal(cell(page, /Sunday 3 January 2027/)).toHaveAccessibleName(
          /Closed \(regular weekly closure\)/,
        );
      } finally {
        // Deactivate the branch this run created, through the UI (the API only).
        await page.goto(base);
        await page.getByRole('button', { name: 'Deactivate' }).click();
        await page.getByRole('dialog').getByRole('button', { name: 'Deactivate' }).click();
        await expectReal(page.getByRole('button', { name: 'Reactivate' })).toBeVisible();
        deactivated = true;
      }
      expect(deactivated).toBe(true);
    },
  );
});
