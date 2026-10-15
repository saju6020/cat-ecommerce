import { test, expect, signInAsStaff } from './fixtures';
import AxeBuilder from '@axe-core/playwright';
import { catProfilesFixtureIds } from '../../mocks/handlers/areas/cat-profiles';

/**
 * Staff cat photos page — upload then attach (HO-CAT-002 §1 Amended 2026-09-27 / 2026-10-09
 * follow-up). Runs on MSW in both Playwright projects (`chromium`, `mobile-360`).
 */
const biscuit = catProfilesFixtureIds.catNoAllergiesEstimatedDob; // no photos yet
const whiskers = catProfilesFixtureIds.catWithAllergiesExactDob; // two photos
const photosPath = (catId: string) => `/staff/cats/${catId}/photos`;

const TINY_PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=',
  'base64',
);

async function expectNoHorizontalScroll(page: import('@playwright/test').Page) {
  const { scrollWidth, clientWidth } = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
  }));
  expect(scrollWidth).toBeLessThanOrEqual(clientWidth + 1);
}

async function expectNoSeriousAxeViolations(page: import('@playwright/test').Page) {
  const results = await new AxeBuilder({ page }).analyze();
  const serious = results.violations.filter(
    (v) => v.impact === 'serious' || v.impact === 'critical',
  );
  expect(serious, JSON.stringify(serious, null, 2)).toEqual([]);
}

test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

test('uploads a photo, attaches it, and it appears in the list', async ({ page }) => {
  await page.goto(photosPath(biscuit));
  await expect(page.getByText('No photos on file yet.')).toBeVisible();

  await page
    .locator('input[type="file"]')
    .setInputFiles({ name: 'biscuit-loaf.png', mimeType: 'image/png', buffer: TINY_PNG });
  await expect(page.getByText('biscuit-loaf.png', { exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'Add photo' }).click();
  await expect(page.getByText('No photos on file yet.')).toHaveCount(0);
  await expect(page.getByRole('list').getByText('biscuit-loaf.png')).toBeVisible();
  await expect(page.getByText('Primary')).toBeVisible();
});

test('an oversized or disallowed file is refused with a reason and cannot be attached', async ({
  page,
}) => {
  await page.goto(photosPath(biscuit));
  const input = page.locator('input[type="file"]');

  await input.setInputFiles({
    name: 'huge.png',
    mimeType: 'image/png',
    buffer: Buffer.alloc(6 * 1024 * 1024),
  });
  await expect(page.getByText(/5 MB/).first()).toBeVisible();
  await expect(page.getByRole('button', { name: 'Add photo' })).toBeDisabled();

  await input.setInputFiles({ name: 'moving.gif', mimeType: 'image/gif', buffer: TINY_PNG });
  await expect(page.getByText(/JPEG, PNG or WebP/).first()).toBeVisible();
  await expect(page.getByRole('button', { name: 'Add photo' })).toBeDisabled();
});

test('a refused attach shows the reason inline and keeps the file', async ({ page }) => {
  await page.goto(photosPath(biscuit));
  await page.locator('input[type="file"]').setInputFiles({
    name: 'attach-rejected-cat.png',
    mimeType: 'image/png',
    buffer: TINY_PNG,
  });
  await page.getByRole('button', { name: 'Add photo' }).click();
  await expect(page.getByRole('alert').filter({ hasText: 'Upload it again' })).toBeVisible();
  await expect(page.getByText('attach-rejected-cat.png', { exact: true })).toBeVisible();
});

test('has no serious/critical axe violations: existing photos, chosen file, and attach error', async ({
  page,
}) => {
  await page.goto(photosPath(whiskers));
  await expect(page.getByText('whiskers-front.jpg')).toBeVisible();
  await page.waitForLoadState('networkidle');
  await expectNoSeriousAxeViolations(page);

  await page.locator('input[type="file"]').setInputFiles({
    name: 'attach-rejected-cat.png',
    mimeType: 'image/png',
    buffer: TINY_PNG,
  });
  await expect(page.getByText('attach-rejected-cat.png', { exact: true })).toBeVisible();
  await expectNoSeriousAxeViolations(page);

  await page.getByRole('button', { name: 'Add photo' }).click();
  await expect(page.getByRole('alert').filter({ hasText: 'Upload it again' })).toBeVisible();
  await expectNoSeriousAxeViolations(page);
});

test('has no horizontal scroll at 360px, with photos, a chosen file and an error', async ({
  page,
}) => {
  await page.setViewportSize({ width: 360, height: 780 });
  await page.goto(photosPath(whiskers));
  await expect(page.getByText('whiskers-front.jpg')).toBeVisible();
  await page.waitForLoadState('networkidle');
  await expectNoHorizontalScroll(page);

  await page.locator('input[type="file"]').setInputFiles({
    name: 'attach-rejected-a-rather-long-photo-file-name-for-overflow.png',
    mimeType: 'image/png',
    buffer: TINY_PNG,
  });
  await page.getByRole('button', { name: 'Add photo' }).click();
  await expect(page.getByRole('alert').filter({ hasText: 'Upload it again' })).toBeVisible();
  await expectNoHorizontalScroll(page);
});
