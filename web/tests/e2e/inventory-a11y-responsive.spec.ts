import AxeBuilder from '@axe-core/playwright';
import type { Page } from '@playwright/test';
import { test, expect, signInAsStaff } from './fixtures';
import { INVENTORY_ROUTES, type InventoryRoute } from './inventory-routes';

/**
 * HO-IMP-002 §16 E-X1 / E-X2, run against MSW. Every route in §8.1 (dynamic segments resolved with
 * seeded ids): zero serious/critical axe violations, and no horizontal scroll at 360 / 768 / 1024 /
 * 1440. ACCEPTANCE checks this for EVERY screen.
 */
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

async function open(page: Page, route: InventoryRoute): Promise<void> {
  await signInAsStaff(page);
  await page.goto(route.path);
  await page.waitForLoadState('networkidle');
  await expect(page.getByRole('heading', { name: route.heading }).first()).toBeVisible();
}

test.describe('E-X1: axe — zero serious/critical violations on every HO-IMP-002 route', () => {
  for (const route of INVENTORY_ROUTES) {
    test(route.path, async ({ page }) => {
      await open(page, route);
      await expectNoSeriousOrCriticalViolations(page);
    });
  }
});

test.describe('E-X2: no horizontal scroll at 360/768/1024/1440', () => {
  for (const route of INVENTORY_ROUTES) {
    for (const viewport of VIEWPORTS) {
      test(`${route.path} at ${viewport.width}px`, async ({ page }) => {
        await signInAsStaff(page);
        await page.setViewportSize(viewport);
        await page.goto(route.path);
        await page.waitForLoadState('networkidle');
        await expect(page.getByRole('heading', { name: route.heading }).first()).toBeVisible();
        const overflows = await page.evaluate(
          () => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1,
        );
        expect(overflows, `${route.path} overflows at ${viewport.width}px`).toBe(false);
      });
    }
  }
});
