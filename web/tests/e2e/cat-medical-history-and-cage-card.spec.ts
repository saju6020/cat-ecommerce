import { test, expect, signInAsCustomer, signInAsStaff } from './fixtures';
import { catProfilesFixtureIds } from '../../mocks/handlers/areas/cat-profiles';

/** E14 (US-CAT-10) and E15 (US-CAT-11), HO-CAT-002 §16.
 *
 * ARCH gate MEDIUM (first pass) — this file's two tests are mixed audience (E14 is `/portal/**`,
 * E15 is `/staff/**`), so each signs in for its own single audience explicitly, never both. */

test('E14 — the medical history PDF downloads', async ({ page }) => {
  await signInAsCustomer(page);
  const catId = catProfilesFixtureIds.catWithAllergiesExactDob; // Whiskers
  await page.goto(`/portal/cats/${catId}/medical-history`);

  const link = page.getByRole('link', { name: /download medical history/i });
  const href = await link.getAttribute('href');
  expect(href).toBeTruthy();
  expect(href).toMatch(/medical-history\.pdf$/i);

  // A real `<a download>` click to a cross-origin URL doesn't reliably route through MSW's
  // service worker in every browser (a `fetch()` from page script does — the same path
  // `lib/api/cats.ts` uses everywhere else) — this proves the endpoint the link points to
  // actually serves a PDF, which is what E14 needs, without depending on that browser quirk.
  const response = await page.evaluate(async (url) => {
    const res = await fetch(url);
    return { status: res.status, contentType: res.headers.get('content-type') };
  }, href as string);

  expect(response.status).toBe(200);
  expect(response.contentType).toMatch(/application\/pdf/i);
});

test('E15 — the cage card renders a QR image and has a print layout', async ({ page }) => {
  await signInAsStaff(page);
  const catId = catProfilesFixtureIds.catHighRiskOverdue; // Tiger — also proves the warning shows
  await page.goto(`/staff/cats/${catId}/cage-card`);

  const qr = page.getByRole('img', { name: /qr code/i });
  await expect(qr).toBeVisible();
  // On screen, the warning shows once, from the page's CatProfileHeader (UI-7) — CageCard's own
  // copy is print-only (print:flex), so it exists in the DOM but isn't visible here.
  await expect(page.getByText('High risk — handle with care').first()).toBeVisible();

  // Print layout: the header/tab-nav chrome and the "Print" button are hidden under @media
  // print (Tailwind's print:hidden); the card supplies its own high-risk warning in their place.
  await page.emulateMedia({ media: 'print' });
  await expect(page.getByRole('button', { name: 'Print' })).toBeHidden();
  await expect(page.getByRole('navigation', { name: 'Cat sections' })).toBeHidden();
  await expect(qr).toBeVisible();
});
