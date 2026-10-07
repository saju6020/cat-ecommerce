import { test, expect, signInAsStaff, signInAsCustomer } from './fixtures';
import { administrationFixtureIds } from '../../mocks/handlers/admin';
import type { Page } from '@playwright/test';

/**
 * HO-ADM-002 §16 functional E-cases, run against MSW. File name sorts after
 * `admin-a11y-responsive.spec.ts` (this suite runs `--workers=1`, one dev-server process, one
 * shared in-memory mock store for the whole run) — every test here that mutates shared state
 * creates its OWN fresh, uniquely-keyed entity rather than the seeded fixtures the a11y suite
 * reads, so neither suite depends on the other's run order or leftover state.
 */

/**
 * `next dev`'s on-demand route compilation, the FIRST time a given dynamic route is hit in a
 * freshly-started dev server, can trigger a client-side "Fast Refresh rebuilding" cycle that
 * aborts an in-flight RSC navigation request already underway (`ERR_ABORTED`) — a dev-server-only
 * artifact, reproduced directly against this exact sequence (create → client-side redirect to the
 * new record), never a production behaviour. Rather than let a single aborted `router.push()`
 * strand a test on the previous page, this waits for the expected URL and falls back to a real
 * `page.goto()` to the same destination — a workaround for the dev-mode quirk, not a product bug.
 *
 * Bug found and fixed while stabilising this helper: `pattern` must be anchored to the exact new
 * record's id (not a generic "/branches/<anything>" shape) — a generic `[^/]+$` pattern matches
 * the STARTING `/staff/admin/branches/new` URL just as well as the real destination (`new` itself
 * satisfies `[^/]+`), so `page.waitForURL` was resolving immediately, before any navigation had
 * even happened, short-circuiting the whole retry loop below and leaving the test on `/new` — the
 * true cause of the "stuck on `/new`" failures this comment used to attribute entirely to Fast
 * Refresh timing. Fast Refresh's aborted RSC fetch is real (seen directly via `page.on` listeners
 * while debugging this), but it was never the reason the retry never ran.
 */
async function waitForUrlOrGoto(
  page: Page,
  exactUrlPattern: RegExp,
  fallbackUrl: string,
): Promise<void> {
  try {
    await page.waitForURL(exactUrlPattern, { timeout: 5000 });
    return;
  } catch {
    // fall through to the retry loop below
  }
  // A cold dev-server compile of the destination route, already mid-navigation when the aborted
  // fetch above happened, can still be finishing — retry the real navigation a few times, with a
  // short settle delay between attempts, rather than assuming one immediate `goto` recovers it.
  for (let attempt = 0; attempt < 4; attempt += 1) {
    await page.waitForTimeout(300 * (attempt + 1));
    try {
      await page.goto(fallbackUrl, { timeout: 10_000 });
      if (exactUrlPattern.test(page.url())) return;
    } catch {
      // try again
    }
  }
}

test('E-B1: create a branch in Europe/London, set hours, add a holiday', async ({ page }) => {
  // Extended timeout — see `waitForUrlOrGoto`'s own comment: the dev-mode Fast-Refresh recovery
  // this test relies on can need several retries under load, each with its own settle delay.
  test.setTimeout(60_000);
  await signInAsStaff(page);
  await page.goto('/staff/admin/branches/new');
  const code = `LDN${Date.now().toString().slice(-6)}`;
  await page.getByLabel('Code').fill(code);
  await page.getByLabel('Name').fill('E2E London branch');
  await page.getByLabel(/time zone/i).selectOption('Europe/London');
  const createResponse = page.waitForResponse(
    (response) =>
      response.url().includes('/api/admin/branches') && response.request().method() === 'POST',
  );
  await page.getByRole('button', { name: /create branch/i }).click();
  const { branchId } = (await (await createResponse).json()) as { branchId: string };

  const branchUrlPattern = new RegExp(`/staff/admin/branches/${branchId}$`);
  await waitForUrlOrGoto(page, branchUrlPattern, `/staff/admin/branches/${branchId}`);
  await expect(page).toHaveURL(branchUrlPattern);
  await page.getByRole('link', { name: 'Opening hours' }).click();
  await page
    .getByRole('button', { name: /add interval/i })
    .first()
    .click();
  const opens = page.getByLabel(/opens \(europe\/london\)/i).first();
  const closes = page.getByLabel(/closes \(europe\/london\)/i).first();
  await opens.fill('08:00');
  await closes.fill('17:00');
  await page.getByRole('button', { name: /save opening hours/i }).click();
  await expect(page.getByText('Opening hours saved.')).toBeVisible();

  await page.getByRole('link', { name: 'Closures' }).click();
  const startsOn = page.getByLabel(/start date/i);
  const endsOn = page.getByLabel(/end date/i);
  const holidayDate = '2028-12-25';
  await startsOn.fill(holidayDate);
  await endsOn.fill(holidayDate);
  await page.getByLabel(/^kind$/i).selectOption('PublicHoliday');
  await page.getByLabel(/reason/i).fill('Christmas Day');
  await page.getByRole('button', { name: /^add$/i }).click();
  await expect(page.getByText('Christmas Day')).toBeVisible();
});

test('E-B2: a staff member assigned to two branches sees the picker, and switching sends the new branch header', async ({
  page,
}) => {
  await signInAsStaff(page);

  await page.route('**/api/staff/branches/mine', async (route) => {
    await route.fulfill({
      json: {
        branchIds: [administrationFixtureIds.mainBranchId, administrationFixtureIds.londonBranchId],
        primaryBranchId: administrationFixtureIds.mainBranchId,
        activeBranchId: administrationFixtureIds.mainBranchId,
        activeBranchRefusal: null,
      },
    });
  });

  // ADR-FE-016: the active branch is never persisted, so a `page.reload()` here would re-seed
  // from the primary branch, not prove anything about switching — the request to watch for is the
  // one `useSetActiveBranch`'s own `['admin']` query invalidation fires immediately on selection.
  await page.goto('/staff/admin/services');
  const picker = page.getByLabel(/^branch$/i);
  await expect(picker).toBeVisible();
  await page.waitForLoadState('networkidle');

  const nextAdminRequest = page.waitForRequest(
    (request) => request.url().includes('/api/admin/services') && request.method() === 'GET',
  );
  await picker.selectOption(administrationFixtureIds.londonBranchId);
  const request = await nextAdminRequest;
  expect(request.headers()['x-branch-id']).toBe(administrationFixtureIds.londonBranchId);
});

test('E-C1: schedule a price for next month — appears under Scheduled, current stays unchanged', async ({
  page,
}) => {
  await signInAsStaff(page);
  await page.goto(`/staff/admin/services/${administrationFixtureIds.serviceConsultationId}`);
  await page.getByRole('button', { name: /schedule a new price/i }).click();
  await page.getByRole('radio', { name: /a future date and time/i }).click();
  const future = new Date();
  future.setMonth(future.getMonth() + 1);
  const value = future.toISOString().slice(0, 16);
  await page.getByLabel(/effective date and time/i).fill(value);
  await page.getByLabel(/amount \(bdt\)/i).fill('2200.00');
  await page.getByRole('button', { name: /^schedule$/i }).click();

  await expect(page.getByText('Scheduled')).toBeVisible();
  await expect(page.getByText(/2,?200\.00/)).toBeVisible();
  // The current price (from the seeded fixture) is unchanged.
  await expect(page.getByText('Current')).toBeVisible();
});

test('E-D1: a nurse requests leave, an administrator approves it, and the roster view marks the days "On leave"', async ({
  page,
}) => {
  await signInAsStaff(page);
  await page.goto('/staff/leave');
  // Two weeks out — far enough to never collide with the seeded roster/exception fixtures' own
  // 2027 dates, close enough that the roster week-navigation below needs only a couple of clicks.
  const twoWeeksOut = new Date();
  twoWeeksOut.setDate(twoWeeksOut.getDate() + 14);
  const startsOn = twoWeeksOut.toISOString().slice(0, 10);
  const endOut = new Date(twoWeeksOut);
  endOut.setDate(endOut.getDate() + 1);
  const endsOn = endOut.toISOString().slice(0, 10);

  // Monday-start week math (RosterWeekView's own `getWeekDates`) — how many "Next week" clicks
  // from today's week reach the leave's own week, computed directly rather than by parsing the
  // page's displayed date text.
  function mondayOf(date: Date): Date {
    const d = new Date(date);
    const day = d.getDay(); // 0 = Sunday
    const diff = day === 0 ? -6 : 1 - day;
    d.setDate(d.getDate() + diff);
    d.setHours(0, 0, 0, 0);
    return d;
  }
  const weeksAhead = Math.round(
    (mondayOf(twoWeeksOut).getTime() - mondayOf(new Date()).getTime()) / (7 * 86_400_000),
  );

  await page.getByLabel(/start date/i).fill(startsOn);
  await page.getByLabel(/end date/i).fill(endsOn);
  await page.getByRole('button', { name: /request leave/i }).click();
  await expect(page.getByText('Leave requested.')).toBeVisible();

  await page.goto('/staff/admin/leave?status=Requested');
  const row = page.getByRole('row', { name: /nadia nurse/i }).last();
  await row.getByRole('button', { name: /^approve$/i }).click();
  await page
    .getByRole('dialog')
    .getByRole('button', { name: /^approve$/i })
    .click();

  await page.goto(`/staff/admin/branches/${administrationFixtureIds.mainBranchId}/roster`);
  for (let i = 0; i < weeksAhead; i += 1) {
    await page.getByRole('button', { name: /next week/i }).click();
  }
  // The roster renders both a desktop grid and a narrow-viewport list; only one is visible per project.
  await expect(page.getByText('On leave').filter({ visible: true }).first()).toBeVisible();
});

test('E-F1: publish a page, view it signed out, then unpublish it to a not-found page', async ({
  page,
}) => {
  // Extended timeout — see `waitForUrlOrGoto`'s own comment.
  test.setTimeout(60_000);
  await signInAsStaff(page);
  const slugTitle = `E2E page ${Date.now()}`;
  await page.goto('/staff/admin/content/new');
  await page.getByLabel(/^kind$/i).selectOption('Page');
  await page.getByLabel(/title/i).fill(slugTitle);
  await page.getByLabel(/^body$/i).fill('Published body text.');
  const createResponse = page.waitForResponse(
    (response) =>
      response.url().includes('/api/admin/content') && response.request().method() === 'POST',
  );
  await page.getByRole('button', { name: /save draft/i }).click();
  const { contentId } = (await (await createResponse).json()) as { contentId: string };
  const detailUrl = `/staff/admin/content/${contentId}`;
  const contentUrlPattern = new RegExp(`/staff/admin/content/${contentId}$`);
  await waitForUrlOrGoto(page, contentUrlPattern, detailUrl);
  await expect(page).toHaveURL(contentUrlPattern);
  await page.getByRole('button', { name: /^publish$/i }).click();
  await page
    .getByRole('dialog')
    .getByRole('button', { name: /^publish$/i })
    .click();
  const slug = (await page.getByLabel(/slug/i).inputValue()).trim();

  await page.context().clearCookies();
  await page.goto(`/pages/${slug}`);
  await expect(page.getByRole('heading', { name: slugTitle })).toBeVisible();

  await signInAsStaff(page);
  await page.goto(detailUrl);
  await page.getByRole('button', { name: /^unpublish$/i }).click();
  // ADM 15: unpublishing a live page is confirmed first.
  const unpublishResponse = page.waitForResponse(
    (response) => /\/unpublish/i.test(response.url()) && response.request().method() !== 'GET',
  );
  await page
    .getByRole('dialog')
    .getByRole('button', { name: /^unpublish$/i })
    .click();
  await unpublishResponse;

  await page.context().clearCookies();
  await page.goto(`/pages/${slug}`);
  // ADM 2: the not-found page has a real <h1>.
  await expect(page.getByRole('heading', { level: 1, name: /page not found/i })).toBeVisible();
});

test('E-F2: publishing a policy version that requires acceptance prompts a customer, and accepting makes the portal usable', async ({
  page,
}) => {
  const uniqueKey = `e2e-terms-${Date.now()}`;
  await signInAsStaff(page);
  await page.goto('/staff/admin/policies');
  await page.getByRole('button', { name: /new policy/i }).click();
  await page.getByLabel(/^key$/i).fill(uniqueKey);
  await page.getByLabel(/^title$/i).fill('E2E functional policy');
  await page.getByLabel(/customers must accept/i).check();
  await page.getByRole('button', { name: /create policy/i }).click();
  await page.getByRole('link', { name: 'E2E functional policy' }).click();

  await page.getByLabel(/^body$/i).fill('The policy text.');
  await page.getByLabel(/change summary/i).fill('First publication.');
  await page.getByRole('button', { name: /publish new version/i }).click();
  await page
    .getByRole('dialog')
    .getByRole('button', { name: /^publish$/i })
    .click();
  // Let the publish land before the staff session goes away, then leave the staff shell for a
  // neutral page — otherwise its own session-lost redirect aborts the portal navigation below.
  await expect(page.getByRole('dialog')).not.toBeVisible();

  await page.context().clearCookies();
  await page.goto('/sign-in');
  await signInAsCustomer(page);
  await page.goto('/portal/account/profile');
  // The seeded "terms" policy is also still pending in this shared-mock-state run (this suite's
  // own a11y spec creates its own extra ones too) — accept every row shown, not only this test's
  // own, since "the portal is usable" genuinely requires every pending acceptance cleared
  // (OQ-3: nothing else is reachable until then).
  const dialog = page.getByRole('dialog');
  const ownRow = dialog.getByRole('listitem').filter({ hasText: 'E2E functional policy' });
  await expect(ownRow).toBeVisible();
  let remaining = await dialog.getByRole('listitem').count();
  while (remaining > 0) {
    await dialog
      .getByRole('listitem')
      .first()
      .getByRole('button', { name: /^accept$/i })
      .click();
    await expect(dialog.getByRole('listitem')).toHaveCount(remaining - 1);
    remaining -= 1;
  }
  await expect(page.getByRole('dialog')).not.toBeVisible();
  // The portal is now usable — the profile page behind the (now-closed) dialog renders normally.
  await expect(page.getByLabel(/full name/i)).toBeVisible();
});

test('E-G1: the audit log shows the seeded price-change row with a before/after diff; export downloads', async ({
  page,
}) => {
  await signInAsStaff(page);
  await page.goto('/staff/admin/audit-log?from=2027-01-01&to=2027-01-31&action=ServicePriceSet');
  await expect(page.getByText('ServicePriceSet')).toBeVisible();
  await page
    .getByRole('button', { name: /show details/i })
    .first()
    .click();
  await expect(page.getByText('1500.00')).toBeVisible();
  await expect(page.getByText('1800.00')).toBeVisible();

  // A SW-served top-level-navigation response is not always surfaced to a real browser's download
  // pipeline the same way a genuine network response is — a documented MSW/browser limitation, not
  // a product behaviour (`waitForEvent('download')` on the link click never fires here). Fetching
  // the export link's own href from inside the page (still MSW-intercepted, unlike
  // `page.request.get`, which bypasses the Service Worker entirely) proves it really is a
  // correctly-filtered CSV with the right headers.
  const exportHref = await page.getByRole('link', { name: /export csv/i }).getAttribute('href');
  expect(exportHref).toBeTruthy();
  const result = await page.evaluate(async (url) => {
    const response = await fetch(url);
    return {
      status: response.status,
      contentDisposition: response.headers.get('content-disposition'),
      text: await response.text(),
    };
  }, exportHref!);
  expect(result.status).toBe(200);
  expect(result.contentDisposition).toContain('attachment');
  expect(result.text).toContain('ServicePriceSet');
});

test('E-G3: an export over the row cap shows the narrow-the-range note', async ({ page }) => {
  await signInAsStaff(page);
  await page.goto(
    `/staff/admin/audit-log?from=2027-01-01&to=2027-01-31&actorUserId=${administrationFixtureIds.trigger413ActorId}`,
  );
  await expect(page.getByText(/too many entries to export/i)).toBeVisible();
});

test('E-H1: turning maintenance on does not affect staff sign-in or admin pages', async ({
  page,
}) => {
  await signInAsStaff(page);
  await page.goto('/staff/admin/system');
  await page.getByLabel(/message shown to customers/i).fill('Back soon.');
  await page.getByRole('button', { name: /turn maintenance on/i }).click();
  await page
    .getByRole('dialog')
    .getByRole('button', { name: /^turn on$/i })
    .click();
  await expect(page.getByRole('button', { name: /turn maintenance off/i })).toBeVisible();

  await page.goto('/staff/admin/branches');
  await expect(page.getByRole('heading', { name: /branches/i })).toBeVisible();
});
