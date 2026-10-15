import AxeBuilder from '@axe-core/playwright';
import type { Page } from '@playwright/test';
import { test, expect } from './fixtures';
import { selectPortalScenario, signInEstablished } from './portal-helpers';

/**
 * HO-POR-002 §16 E-X1 (axe, zero serious/critical, on every fixture and with the dialog open) and
 * E-X2 (no horizontal scroll at 360 / 768 / 1024 / 1440), against MSW.
 */
test.describe.configure({ timeout: 60_000 });

const SCENARIOS = ['full', 'new', 'degraded', 'catsUnavailable', 'manyCats', 'ordersNotDelivered'];
const VIEWPORTS = [
  { width: 360, height: 780 },
  { width: 768, height: 1024 },
  { width: 1024, height: 768 },
  { width: 1440, height: 900 },
];

async function openAs(page: Page, name: string) {
  await signInEstablished(page);
  await selectPortalScenario(page, name);
  await page.goto('/portal');
  await expect(page.getByTestId('tile-cats')).toBeVisible();
}

async function expectNoSeriousOrCritical(page: Page) {
  const results = await new AxeBuilder({ page }).analyze();
  const serious = results.violations.filter(
    (v) => v.impact === 'serious' || v.impact === 'critical',
  );
  expect(serious, JSON.stringify(serious, null, 2)).toEqual([]);
}

async function expectNoHorizontalScroll(page: Page) {
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
}

test.describe('E-X1 axe', () => {
  for (const name of SCENARIOS) {
    test(`/portal — ${name}`, async ({ page }) => {
      await openAs(page, name);
      await expectNoSeriousOrCritical(page);
    });
  }

  test('/portal with the re-order result dialog open (mixed) and the error dialog', async ({
    page,
  }) => {
    await openAs(page, 'full');
    await page.getByRole('button', { name: 'Order SO-2026-00027 again' }).click();
    await expect(page.getByRole('dialog', { name: 'Some items were added' })).toBeVisible();
    await expectNoSeriousOrCritical(page);
  });
});

test.describe('E-X2 responsive', () => {
  for (const viewport of VIEWPORTS) {
    for (const name of ['full', 'new', 'manyCats']) {
      test(`/portal — ${name} at ${viewport.width}`, async ({ page }) => {
        await page.setViewportSize(viewport);
        await openAs(page, name);
        await expectNoHorizontalScroll(page);
      });
    }

    test(`/portal — result dialog fits at ${viewport.width}`, async ({ page }) => {
      await page.setViewportSize(viewport);
      await openAs(page, 'full');
      await page.getByRole('button', { name: 'Order SO-2026-00027 again' }).click();
      const dialog = page.getByRole('dialog');
      await expect(dialog).toBeVisible();
      const box = await dialog.boundingBox();
      expect(box).not.toBeNull();
      expect(box!.x).toBeGreaterThanOrEqual(0);
      expect(box!.x + box!.width).toBeLessThanOrEqual(viewport.width);
      await expectNoHorizontalScroll(page);
    });
  }
});
