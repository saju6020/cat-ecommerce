import { test, expect } from './fixtures';
import { ids, signInAsRole } from './emr-helpers';

/**
 * Clinical history and diagnostics — HO-EMR-002 §16 E-B1, E-D2 (NFR-PRF-4) and E-C1, against MSW.
 */
test.describe('Clinical history — US-CLN-13', () => {
  test('E-B1: the vet opens the history from the workspace, reads three sections, closes it; the draft is untouched', async ({
    page,
  }) => {
    test.setTimeout(60_000);
    await signInAsRole(page, 'Vet');
    await page.goto(`/staff/visits/${ids.visitOpenId}`);
    const url = page.url();
    const plan = page.getByLabel('Plan');
    await plan.fill('Typed before opening the history.');

    await page.getByRole('button', { name: 'History' }).click();
    const drawer = page.getByRole('dialog', { name: /whiskers’s clinical history/i });
    await expect(drawer.getByRole('heading', { name: 'Active problems' })).toBeVisible();

    await drawer.getByRole('tab', { name: 'Visits' }).click();
    await expect(
      drawer.getByRole('button', { name: /open the general consultation visit/i }).first(),
    ).toBeVisible();
    await drawer.getByRole('tab', { name: 'Lab results' }).click();
    await expect(drawer.getByText(/Blood work|BloodWork/).first()).toBeVisible();
    await drawer.getByRole('tab', { name: 'Prescriptions' }).click();
    await expect(
      drawer.getByText('Prescriptions will appear here once the pharmacy module is live.'),
    ).toBeVisible();

    await page.keyboard.press('Escape');
    await expect(drawer).toBeHidden();
    expect(page.url()).toBe(url);
    await expect(plan).toHaveValue('Typed before opening the history.');
    await expect(page.getByRole('button', { name: 'History' })).toBeFocused();
  });

  test('E-D2: the 10-year fixture — the overview renders within 2 s at the 95th percentile (NFR-PRF-4)', async ({
    page,
  }) => {
    test.setTimeout(240_000);
    await signInAsRole(page, 'Vet');
    const timings: number[] = [];
    for (let run = 0; run < 20; run += 1) {
      // A reload empties the query cache, so every run is a cold summary call.
      await page.goto(`/staff/visits/${ids.visitOpenId}`);
      await expect(page.getByRole('button', { name: 'History' })).toBeVisible();
      const started = Date.now();
      await page.getByRole('button', { name: 'History' }).click();
      await page
        .getByRole('dialog')
        .getByRole('heading', { name: 'Active problems' })
        .waitFor({ state: 'visible' });
      timings.push(Date.now() - started);
    }
    timings.sort((a, b) => a - b);
    const p95 = timings[Math.ceil(timings.length * 0.95) - 1]!;
    console.log(`E-D2 timings (ms): ${timings.join(', ')} · p95 ${p95} ms`);
    expect(p95).toBeLessThan(2_000);
  });

  test('the full history page shows the sections and Load more pages through the 10-year weights', async ({
    page,
  }) => {
    await signInAsRole(page, 'Vet');
    await page.goto(`/staff/cats/${ids.catWhiskersId}/history`);
    await page.getByRole('tab', { name: 'Weights' }).click();
    await expect(page.getByText(/Showing 20 of \d+/)).toBeVisible();
    await page.getByRole('button', { name: 'Load more' }).click();
    await expect(page.getByText(/Showing 40 of \d+/)).toBeVisible();
  });
});

test.describe('Diagnostics — US-CLN-14', () => {
  test('E-C1: a lab PDF is uploaded, attached and opened from its signed URL; DICOM is download-only', async ({
    page,
  }) => {
    test.setTimeout(60_000);
    await signInAsRole(page, 'Vet');
    await page.goto(`/staff/diagnostics/${ids.orderPendingId}`);
    await expect(page.getByRole('heading', { name: /blood work for whiskers/i })).toBeVisible();

    await page.locator('input[type="file"]').setInputFiles({
      name: 'lab-report.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('%PDF-1.4 test'),
    });
    await expect(page.getByText('lab-report.pdf').first()).toBeVisible();
    await page.getByRole('button', { name: 'Attach file' }).click();
    const open = page.getByRole('link', { name: /open lab-report\.pdf in a new tab/i });
    await expect(open).toBeVisible();
    // A new tab from the signed URL (noopener). The mock file server is cross-origin, so the link
    // itself is asserted here; the real-API run (emr-real-api.spec.ts) opens it.
    await expect(open).toHaveAttribute('target', '_blank');
    await expect(open).toHaveAttribute('rel', /noopener/);
    await expect(open).toHaveAttribute('href', /\/api\/files\/.+\/content\?/);

    await page.goto(`/staff/diagnostics/${ids.orderImagingId}`);
    await expect(page.getByRole('link', { name: /download stifle-study\.dcm/i })).toBeVisible();
    await expect(page.getByRole('link', { name: /open stifle-study\.dcm/i })).toHaveCount(0);
  });

  test('a lab file refused by the scanner shows the plain-language message', async ({ page }) => {
    await signInAsRole(page, 'Vet');
    await page.goto(`/staff/diagnostics/${ids.orderPendingId}`);
    await page.locator('input[type="file"]').setInputFiles({
      name: 'scan-unavailable-report.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('%PDF-1.4 test'),
    });
    await expect(page.getByText(/virus scanner is unavailable/i).first()).toBeVisible();
  });

  test('the pending list shows the waiting orders and links to each', async ({ page }) => {
    await signInAsRole(page, 'Nurse');
    await page.goto('/staff/diagnostics');
    await expect(page.getByRole('heading', { name: 'Pending diagnostics' })).toBeVisible();
    await page.getByRole('link', { name: 'Whiskers' }).first().click();
    await expect(page.getByRole('heading', { name: 'Diagnostic order' })).toBeVisible();
  });
});
