// Plain Playwright `test` (not ./fixtures): that wrapper waits for MSW's worker, which never
// exists when NEXT_PUBLIC_USE_MOCKS=false.
import { test, expect } from '@playwright/test';
import { IS_REAL_API, api } from './emr-helpers';

/**
 * Cat photos page against the REAL API (HO-CAT-002 2026-10-09 follow-up): upload through
 * `POST /api/staff/files?purpose=CatProfilePhoto`, attach `{ fileId, isPrimary }`, see it listed
 * with a signed url that really serves an image. Skipped on mocks. Credentials: `E2E_STAFF_IDENTIFIER`
 * (a Vet or Administrator — both hold Cats.EditProfile) and `E2E_STAFF_PASSWORD`; the cat id comes
 * from `E2E_CAT_ID` (the dev database's "E2E Test Cat").
 */
test.skip(!IS_REAL_API, 'real-API run only (NEXT_PUBLIC_USE_MOCKS=false)');

const identifier = process.env.E2E_STAFF_IDENTIFIER ?? '';
const password = process.env.E2E_STAFF_PASSWORD ?? '';
const catId = process.env.E2E_CAT_ID ?? '';
test.skip(!identifier || !password || !catId, 'E2E_STAFF_* / E2E_CAT_ID not set');

const TINY_PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=',
  'base64',
);

test('real: upload a PNG, attach it, and it is listed with a loadable image', async ({ page }) => {
  await page.goto('/staff/sign-in');
  await page.waitForFunction(() =>
    Boolean((window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl),
  );
  const signedIn = await api.post(page, '/api/staff/auth/sign-in', { identifier, password });
  expect(signedIn.status).toBeLessThan(300);

  await page.goto(`/staff/cats/${catId}/photos`);
  await expect(page.getByRole('heading', { name: 'Photos' })).toBeVisible({ timeout: 15_000 });
  // The cat holds at most 10 photos; a re-run on a full cat has nothing to add.
  test.skip(await page.getByText(/already has the maximum/).isVisible(), 'cat is at the photo limit');

  const name = `e2e-real-${Date.now()}.png`;
  await page.locator('input[type="file"]').setInputFiles({
    name,
    mimeType: 'image/png',
    buffer: TINY_PNG,
  });
  await expect(page.getByText(name, { exact: true })).toBeVisible({ timeout: 15_000 });
  await page.getByRole('button', { name: 'Add photo' }).click();

  const row = page.getByRole('listitem').filter({ hasText: name });
  await expect(row).toBeVisible({ timeout: 15_000 });
  const image = row.getByRole('img');
  await expect(image).toBeVisible();
  await expect
    .poll(() => image.evaluate((img: HTMLImageElement) => img.complete && img.naturalWidth > 0))
    .toBe(true);
  await expect(row).toHaveCount(1);
});
