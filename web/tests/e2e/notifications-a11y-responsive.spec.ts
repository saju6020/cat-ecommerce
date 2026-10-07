import AxeBuilder from '@axe-core/playwright';
import { test, expect, signInAsStaff, signInAsCustomer } from './fixtures';
import { notificationFixtureIds as ids } from '../../mocks/handlers/notifications';
import type { Locator, Page } from '@playwright/test';

/**
 * HO-NTF-002 §16 E-X1 / E-X2 against MSW — every route in §8.1's map, clean, plus the named
 * "with errors" and "editor preview open" states. Dynamic segments use seeded MSW ids so each route
 * renders real data (not a loading or 404 state). `/unsubscribe` is reached through its fragment
 * link; the page removes the fragment on load, so its URL is checked without it.
 */

interface RouteCase {
  path: string;
  audience: 'none' | 'customer' | 'staff';
  heading: RegExp;
  /** For a route with no heading of its own (the account tabs): the control that proves it rendered. */
  ready?: (page: Page) => Locator;
  /** The URL the page is expected to settle on (the unsubscribe fragment is removed). */
  settledUrl?: RegExp;
}

const ROUTES: RouteCase[] = [
  { path: '/portal/inbox', audience: 'customer', heading: /^inbox$/i },
  {
    path: '/portal/account/notifications',
    audience: 'customer',
    heading: /notifications/i,
    ready: (page) => page.getByRole('group', { name: 'Send important messages by' }),
  },
  {
    path: '/unsubscribe#token=valid-email-token',
    audience: 'none',
    heading: /stop marketing messages/i,
    settledUrl: /\/unsubscribe$/,
  },
  { path: '/unsubscribe', audience: 'none', heading: /this link isn.t valid/i },
  {
    path: '/staff/admin/notification-templates',
    audience: 'staff',
    heading: /message templates/i,
  },
  {
    path: `/staff/admin/notification-templates/${ids.vaccinationKey}`,
    audience: 'staff',
    heading: /vaccination due/i,
  },
  {
    path: `/staff/admin/notification-templates/${ids.passwordResetKey}`,
    audience: 'staff',
    heading: /password reset/i,
  },
  { path: '/staff/notifications', audience: 'staff', heading: /^messages$/i },
  {
    path: `/staff/notifications/${ids.failedSmsId}`,
    audience: 'staff',
    heading: /vaccination due/i,
  },
  {
    path: `/staff/notifications/${ids.redactedResetId}`,
    audience: 'staff',
    heading: /password reset/i,
  },
  { path: `/staff/customers/${ids.customerId}`, audience: 'staff', heading: /sam rahman/i },
];

const VIEWPORTS = [
  { width: 360, height: 780 },
  { width: 768, height: 1024 },
  { width: 1024, height: 768 },
  { width: 1440, height: 900 },
];

async function signInFor(page: Page, audience: RouteCase['audience']) {
  if (audience === 'customer') await signInAsCustomer(page);
  else if (audience === 'staff') await signInAsStaff(page);
}

async function expectNoSeriousOrCritical(page: Page) {
  // The email-preview iframe is `sandbox=""` (no scripts), so axe can never inject into it and its
  // frame handshake hangs until the test timeout. Its CONTENT is the server's inert rendering of the
  // email, not this app's UI; the frame itself (title, sandbox) is asserted by
  // notifications-templates.spec.ts and the SandboxedEmailPreview unit tests.
  const results = await new AxeBuilder({ page }).exclude('iframe[title="Email preview"]').analyze();
  const serious = results.violations.filter(
    (v) => v.impact === 'serious' || v.impact === 'critical',
  );
  expect(serious, JSON.stringify(serious, null, 2)).toEqual([]);
}

function renderedMarker(page: Page, route: RouteCase): Locator {
  return route.ready
    ? route.ready(page)
    : page.getByRole('heading', { name: route.heading }).first();
}

async function open(page: Page, route: RouteCase) {
  await signInFor(page, route.audience);
  await page.goto(route.path);
  await page.waitForLoadState('networkidle');
  if (route.settledUrl) await expect(page).toHaveURL(route.settledUrl);
  await expect(renderedMarker(page, route)).toBeVisible();
}

test.describe('E-X1: axe — zero serious/critical violations on every HO-NTF-002 route', () => {
  for (const route of ROUTES) {
    test(route.path, async ({ page }) => {
      await open(page, route);
      await expectNoSeriousOrCritical(page);
    });
  }
});

test.describe('E-X2: no horizontal scroll and no clipped control at 360/768/1024/1440', () => {
  for (const route of ROUTES) {
    for (const viewport of VIEWPORTS) {
      test(`${route.path} at ${viewport.width}px`, async ({ page }) => {
        await signInFor(page, route.audience);
        await page.setViewportSize(viewport);
        await page.goto(route.path);
        await page.waitForLoadState('networkidle');
        await expect(renderedMarker(page, route)).toBeVisible();
        const overflow = await page.evaluate(
          () => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1,
        );
        expect(overflow, `${route.path} overflows at ${viewport.width}px`).toBe(false);
      });
    }
  }
});

test('E-X1 (editor with the preview open on each channel): zero serious/critical', async ({
  page,
}) => {
  await signInAsStaff(page);
  await page.goto(`/staff/admin/notification-templates/${ids.vaccinationKey}`);
  await page.waitForLoadState('networkidle');
  await expect(page.getByTitle('Email preview')).toBeVisible();
  await expectNoSeriousOrCritical(page);

  await page.getByRole('tab', { name: /Text message/ }).click();
  await expect(page.getByTestId('sms-preview-text')).toBeVisible();
  await expectNoSeriousOrCritical(page);

  await page.getByRole('tab', { name: 'In-app' }).click();
  await page.getByText('Sample values for the preview').click();
  await expectNoSeriousOrCritical(page);
});

test('E-X1 (with errors shown): a preview error on the field and in the panel, zero serious/critical', async ({
  page,
}) => {
  await signInAsStaff(page);
  await page.goto(`/staff/admin/notification-templates/${ids.vaccinationKey}`);
  await page.getByRole('tab', { name: /Text message/ }).click();
  await page.getByLabel('SMS message').fill('Hello {{catname}} {{bookingLink}}');
  await expect(page.getByTestId('preview-error')).toBeVisible();
  await expect(page.locator('#tpl-1-body-error')).toBeVisible();
  await expectNoSeriousOrCritical(page);
});

test('E-X1 (with errors shown): an invalid unsubscribe link, zero serious/critical', async ({
  page,
}) => {
  await page.goto('/unsubscribe#token=invalid-token');
  await page.getByRole('button', { name: 'Unsubscribe' }).click();
  await expect(page.getByText(/This link isn.t valid any more/)).toBeVisible();
  await expectNoSeriousOrCritical(page);
});

test('E-X1 (with errors shown): the log with a refused date range, zero serious/critical', async ({
  page,
}) => {
  await signInAsStaff(page);
  await page.goto('/staff/notifications?from=2026-01-01&to=2026-06-30');
  await expect(page.getByRole('alert').filter({ hasText: '93' })).toBeVisible();
  await expectNoSeriousOrCritical(page);
});

test('E-X1 (resend confirm open): zero serious/critical', async ({ page }) => {
  await signInAsStaff(page);
  await page.goto(`/staff/notifications/${ids.failedSmsId}`);
  await page.getByRole('button', { name: 'Resend', exact: true }).click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await expectNoSeriousOrCritical(page);
});

test('E-X1 (inbox item open): zero serious/critical', async ({ page }) => {
  await signInAsCustomer(page);
  await page.goto('/portal/inbox');
  await page.getByRole('button', { name: /Momo is due a vaccination/ }).click();
  await expect(page.getByRole('link', { name: 'View', exact: true })).toBeVisible();
  await expectNoSeriousOrCritical(page);
});
