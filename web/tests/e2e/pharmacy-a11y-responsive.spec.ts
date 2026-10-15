import AxeBuilder from '@axe-core/playwright';
import type { Page } from '@playwright/test';
import { test, expect } from './fixtures';
import { warmRoutes } from './inventory-helpers';
import { enterDose, fillComposer, ids, openAs, pickMedicine, stubPrint } from './pharmacy-helpers';
import { PHARMACY_ROUTES, type PharmacyRoute } from './pharmacy-routes';

/**
 * HO-PHM-002 §16 E-X1 / E-X2, run against MSW. Every route in §8.1 (dynamic segments resolved with
 * seeded ids): zero serious/critical axe violations, and no horizontal scroll at 360 / 768 / 1024 /
 * 1440. The states that need an interaction to appear — the safety panel with warnings and the
 * acknowledgement block, the witness dialog, the register correction confirmation — are exercised too.
 */
test.setTimeout(90_000);

test.beforeAll(async ({ request }) => {
  test.setTimeout(600_000);
  await warmRoutes(
    request,
    PHARMACY_ROUTES.map((route) => route.path),
  );
});

const VIEWPORTS = [
  { width: 360, height: 780 },
  { width: 768, height: 1024 },
  { width: 1024, height: 768 },
  { width: 1440, height: 900 },
];

async function expectNoSeriousOrCriticalViolations(page: Page): Promise<void> {
  const results = await new AxeBuilder({ page }).analyze();
  const serious = results.violations.filter(
    (v) => v.impact === 'serious' || v.impact === 'critical',
  );
  expect(serious, JSON.stringify(serious, null, 2)).toEqual([]);
}

async function open(
  page: Page,
  route: PharmacyRoute,
  viewport?: { width: number; height: number },
): Promise<void> {
  if (viewport) await page.setViewportSize(viewport);
  await openAs(page, route.role, route.path);
  await expect(route.ready(page)).toBeVisible();
}

test.describe('E-X1: axe — zero serious/critical violations on every HO-PHM-002 route', () => {
  for (const route of PHARMACY_ROUTES) {
    test(route.path, async ({ page }) => {
      await open(page, route);
      await expectNoSeriousOrCriticalViolations(page);
    });
  }

  test('the composer with an above-range dose, warnings and the acknowledgement block', async ({
    page,
  }) => {
    await openAs(
      page,
      'Vet',
      `/staff/cats/${ids.catWhiskersId}/prescriptions/new?visitId=${ids.visitOpenId}`,
    );
    await pickMedicine(page, 'melox', /Meloxicam oral suspension/);
    await fillComposer(page, {
      frequency: 'OnceDaily',
      duration: '7',
      quantity: '10',
      instructions: 'Give 0.4 ml once daily.',
    });
    await enterDose(page, '0.4', 'Ml');
    await expect(
      page
        .getByRole('complementary', { name: 'Safety check' })
        .getByText('Above range', { exact: true }),
    ).toBeVisible();
    await page.getByRole('button', { name: 'Issue prescription' }).click();
    await expect(
      page.getByText('Tick to confirm you have reviewed this warning.').first(),
    ).toBeVisible();
    await expectNoSeriousOrCriticalViolations(page);
  });

  test('the witness dialog and its refusal', async ({ page }) => {
    await openAs(page, 'Reception', `/staff/pharmacy/dispense/${ids.rxTigerControlledQueueId}`);
    await page.getByRole('button', { name: /Dispense \(witness needed\)/ }).click();
    const dialog = page.getByRole('dialog', { name: /witness needed/i });
    await expect(dialog).toBeVisible();
    await expectNoSeriousOrCriticalViolations(page);
    await dialog.getByLabel(/witness mobile number or email/i).fill('admin@purrcare.test');
    await dialog.getByLabel(/witness password/i).fill('AdminPass123!');
    await dialog.getByRole('button', { name: 'Confirm and dispense' }).click();
    await expect(dialog.getByText(/could not be verified/)).toBeVisible();
    await expectNoSeriousOrCriticalViolations(page);
  });

  test('the label print view after a print', async ({ page }) => {
    await openAs(
      page,
      'Reception',
      `/staff/pharmacy/dispensings/${ids.dispensingControlledId}/label`,
    );
    await stubPrint(page);
    await page.getByRole('button', { name: 'Print' }).click();
    await expect(page.getByText('Printed 1 time so far.')).toBeVisible();
    await expectNoSeriousOrCriticalViolations(page);
  });
});

test.describe('E-X2: no horizontal scroll at 360/768/1024/1440', () => {
  for (const route of PHARMACY_ROUTES) {
    for (const viewport of VIEWPORTS) {
      test(`${route.path} at ${viewport.width}px`, async ({ page }) => {
        await open(page, route, viewport);
        const overflows = await page.evaluate(
          () => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1,
        );
        expect(overflows, `${route.path} overflows at ${viewport.width}px`).toBe(false);
      });
    }
  }

  test('the label print view is the same at every width', async ({ page }) => {
    const widths: number[] = [];
    for (const viewport of VIEWPORTS) {
      await page.setViewportSize(viewport);
      await openAs(
        page,
        'Reception',
        `/staff/pharmacy/dispensings/${ids.dispensingControlledId}/label`,
      );
      await expect(page.getByRole('article', { name: /medicine label/i })).toBeVisible();
      widths.push(
        await page
          .getByRole('article', { name: /medicine label/i })
          .evaluate((node) => Math.round(node.getBoundingClientRect().width)),
      );
    }
    // 62 mm at 96 dpi is ~234 px; below 360 px the card never needs to shrink either.
    expect(new Set(widths).size).toBe(1);
  });
});
