// Plain Playwright `test`, not ./fixtures: that wrapper makes every page.goto wait up to 25 s for MSW's
// service worker, which never exists when NEXT_PUBLIC_USE_MOCKS=false (the whole point of this spec).
import { test, expect } from '@playwright/test';
import { IS_REAL_API, api } from './emr-helpers';

/**
 * HO-EMR-002 §16 — the E-case re-run against the REAL API (NEXT_PUBLIC_USE_MOCKS=false, API on
 * http://localhost:5005, catecommerce_dev). Skipped on mocks. Staff credentials come from
 * `E2E_STAFF_IDENTIFIER` / `E2E_STAFF_PASSWORD` (a Vet) — the same convention as
 * `real-api-verification.spec.ts`. The cases are READ/WRITE-light: they use whatever visit data
 * the dev database holds and create nothing outside a cat's own record.
 */
test.skip(!IS_REAL_API, 'real-API run only (NEXT_PUBLIC_USE_MOCKS=false)');

const identifier = process.env.E2E_STAFF_IDENTIFIER ?? '';
const password = process.env.E2E_STAFF_PASSWORD ?? '';
test.skip(!identifier || !password, 'E2E_STAFF_IDENTIFIER / E2E_STAFF_PASSWORD not set');

async function signIn(page: import('@playwright/test').Page) {
  await page.goto('/staff/sign-in');
  // The helpers read the API origin the app publishes on `window` once it has hydrated.
  await page.waitForFunction(() =>
    Boolean((window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl),
  );
  const result = await api.post(page, '/api/staff/auth/sign-in', { identifier, password });
  expect(result.status).toBeLessThan(300);
}

test('E-X real: the clinical routes load against the real API without a crash', async ({
  page,
}) => {
  await signIn(page);
  for (const [path, heading] of [
    ['/staff/visits', 'Open visits'],
    ['/staff/diagnostics', 'Pending diagnostics'],
    ['/staff/admin/clinical/diagnosis-codes', 'Diagnosis codes'],
    ['/staff/admin/clinical/lab-analytes', 'Lab analytes'],
    ['/staff/admin/clinical/note-templates', 'Note templates'],
  ] as const) {
    await page.goto(path);
    await expect(page.getByRole('heading', { name: heading })).toBeVisible({ timeout: 15_000 });
  }
});

test('E-A real: open an open visit, type, auto-save, reload and the text is restored', async ({
  page,
}) => {
  await signIn(page);
  const list = await api.get(page, '/api/staff/visits?status=Open');
  const visit = (list.body as Array<{ id: string }>)[0];
  test.skip(!visit, 'no open visit in the dev database');
  await page.goto(`/staff/visits/${visit!.id}`);
  const plan = page.getByLabel('Plan');
  await expect(plan).toBeVisible({ timeout: 15_000 });
  const text = `Real API draft ${Date.now()}`;
  await plan.fill(text);
  await expect(page.getByText(/Draft saved/)).toBeVisible({ timeout: 15_000 });
  await page.reload();
  await expect(page.getByLabel('Plan')).toHaveValue(text);
  await expect(page.getByText(/were restored from your working copy/)).toBeVisible();
});

test('E-B real: the history drawer renders the overview and the unavailable sections', async ({
  page,
}) => {
  await signIn(page);
  const list = await api.get(page, '/api/staff/visits?status=Open');
  const visit = (list.body as Array<{ id: string }>)[0];
  test.skip(!visit, 'no open visit in the dev database');
  await page.goto(`/staff/visits/${visit!.id}`);
  await page.getByRole('button', { name: 'History' }).click();
  const drawer = page.getByRole('dialog');
  await expect(drawer.getByRole('heading', { name: 'Active problems' })).toBeVisible({
    timeout: 15_000,
  });
  await drawer.getByRole('tab', { name: 'Prescriptions' }).click();
  await expect(drawer.getByText(/will appear here once the pharmacy module is live/)).toBeVisible();
});
