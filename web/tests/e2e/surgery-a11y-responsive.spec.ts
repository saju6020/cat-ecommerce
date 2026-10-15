import AxeBuilder from '@axe-core/playwright';
import type { Page } from '@playwright/test';
import { test, expect } from './fixtures';
import { warmRoutes } from './inventory-helpers';
import { WITNESS_NURSE, ids, openAs } from './surgery-helpers';
import { SURGERY_ROUTES, type SurgeryRoute } from './surgery-routes';

/**
 * HO-SRG-002 §16 E-X2 and E-X3, run against MSW. Every route in §8.1 (dynamic segments resolved with the
 * seeded ids): zero serious/critical axe violations, and no horizontal scroll at 360 / 768 / 1024 / 1440.
 * The dialogs that need an interaction to appear — the consent override, the drug dialog with its witness,
 * a correction, the discharge confirm — are opened and checked too.
 */
test.setTimeout(120_000);

test.beforeAll(async ({ request }) => {
  test.setTimeout(900_000);
  await warmRoutes(
    request,
    SURGERY_ROUTES.map((route) => route.path.split('#')[0]!),
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

async function open(page: Page, route: SurgeryRoute, viewport?: { width: number; height: number }) {
  if (viewport) await page.setViewportSize(viewport);
  await openAs(page, route.role, route.path);
  await expect(route.ready(page)).toBeVisible();
}

test.describe('E-X2: axe — zero serious/critical violations on every HO-SRG-002 route', () => {
  for (const route of SURGERY_ROUTES) {
    test(route.path, async ({ page }) => {
      await open(page, route);
      await expectNoSeriousOrCriticalViolations(page);
    });
  }

  test('the consent override dialog, both steps', async ({ page }) => {
    await openAs(page, 'Vet', `/staff/surgeries/${ids.surgeryPreOpConsentOnlyId}`);
    await page.getByRole('button', { name: 'Start with emergency override' }).click();
    const dialog = page.getByRole('dialog');
    await expectNoSeriousOrCriticalViolations(page);
    await dialog
      .getByLabel('Reason for the emergency override')
      .fill('Cat collapsing, owner unreachable');
    await dialog.getByRole('button', { name: 'Continue' }).click();
    await expect(dialog).toContainText('recorded permanently');
    await expectNoSeriousOrCriticalViolations(page);
  });

  test('the drug dialog and its witness dialog', async ({ page }) => {
    await openAs(page, 'Vet', `/staff/surgeries/${ids.surgeryInTheatreId}#anaesthesia`);
    await page.getByRole('button', { name: 'Add drug' }).click();
    await page.getByRole('combobox', { name: /^medicine/i }).fill('Bupre');
    await page
      .getByRole('option', { name: /Buprenorphine/ })
      .first()
      .click();
    await page.getByLabel('Dose unit').selectOption('Mg');
    await page.getByRole('textbox', { name: 'Dose', exact: true }).fill('0.02');
    await page.getByLabel('Route').selectOption('Intramuscular');
    await page.getByRole('radio').first().check();
    await page.getByLabel(/^Quantity drawn/).fill('1');
    await page.getByLabel(/^Quantity wasted/).fill('0');
    await expectNoSeriousOrCriticalViolations(page);
    await page.getByRole('button', { name: 'Next: witness' }).click();
    await expect(page.getByRole('dialog', { name: /witness needed/i })).toBeVisible();
    await expectNoSeriousOrCriticalViolations(page);
    await page
      .getByRole('dialog', { name: /witness needed/i })
      .getByLabel(/witness mobile/i)
      .fill(WITNESS_NURSE.identifier);
    await page
      .getByRole('dialog', { name: /witness needed/i })
      .getByLabel(/witness password/i)
      .fill('wrong');
    await page.getByRole('button', { name: 'Confirm and save entry' }).click();
    await expect(page.getByText(/could not be verified/)).toBeVisible();
    await expectNoSeriousOrCriticalViolations(page);
  });

  test('a correction dialog and the vitals dialog', async ({ page }) => {
    await openAs(page, 'Vet', `/staff/surgeries/${ids.surgeryInTheatreId}#anaesthesia`);
    await page.getByRole('button', { name: 'Add vitals' }).click();
    await page.getByLabel(/^Heart rate/).fill('400');
    await page.getByRole('button', { name: 'Save vitals' }).click();
    await expect(page.getByText(/looks unlikely/)).toBeVisible();
    await expectNoSeriousOrCriticalViolations(page);
    await page.getByRole('button', { name: 'Cancel' }).click();
    await page
      .getByRole('button', { name: /^Correct the .* vitals entry/ })
      .first()
      .click();
    await expectNoSeriousOrCriticalViolations(page);
  });

  test('the discharge confirm and the cancel dialog', async ({ page }) => {
    await openAs(page, 'Vet', '/staff/surgeries/e5000000-0000-4000-8000-0000000000a2#discharge');
    await page.getByRole('button', { name: /^Discharge Noodle/ }).click();
    await expect(page.getByRole('dialog', { name: 'Discharge Noodle?' })).toBeVisible();
    await expectNoSeriousOrCriticalViolations(page);
    await page.getByRole('button', { name: 'Cancel' }).click();
    await openAs(page, 'Reception', `/staff/surgeries/${ids.surgeryTomorrowId}`);
    await page.getByRole('button', { name: 'Cancel surgery' }).click();
    await expect(page.getByRole('dialog', { name: 'Cancel this surgery?' })).toBeVisible();
    await expectNoSeriousOrCriticalViolations(page);
  });
});

test.describe('E-X3: no horizontal scroll or clipped control at 360 / 768 / 1024 / 1440', () => {
  for (const route of SURGERY_ROUTES) {
    test(route.path, async ({ page }) => {
      for (const viewport of VIEWPORTS) {
        await open(page, route, viewport);
        const overflow = await page.evaluate(() => ({
          scroll: document.documentElement.scrollWidth,
          client: document.documentElement.clientWidth,
        }));
        expect(overflow.scroll, `${route.path} at ${viewport.width}px`).toBeLessThanOrEqual(
          overflow.client,
        );
      }
    });
  }
});
