import AxeBuilder from '@axe-core/playwright';
import type { Page } from '@playwright/test';
import { test, expect, signInAsCustomer } from './fixtures';
import { ids, signInAsRole, type EmrRole } from './emr-helpers';

/**
 * HO-EMR-002 §16 E-X1 (axe) and E-X2 (no horizontal scroll / clipped control at 360, 768, 1024,
 * 1440), against MSW, for EVERY route §8.1 adds, plus the states the handover names: the workspace with
 * the history drawer open, the vitals confirm, the finalise dialog, the order drawer, the signature
 * screen. One audience per test (fixtures.ts).
 */
interface RouteCase {
  path: string;
  role: EmrRole | 'Customer';
}

const consent = `/staff/consents/new?catId=${ids.catMittensId}&appointmentId=${ids.queuedBookedAppointmentId}&kind=Surgery`;

const ROUTES: RouteCase[] = [
  { path: '/staff/visits', role: 'Vet' },
  { path: `/staff/visits/${ids.visitOpenId}`, role: 'Vet' },
  { path: `/staff/visits/${ids.visitDraftNewerId}`, role: 'Vet' },
  { path: `/staff/visits/${ids.visitTemplateId}`, role: 'Vet' },
  { path: `/staff/visits/${ids.visitOpenId}`, role: 'Nurse' },
  { path: `/staff/visits/${ids.visitOpenId}`, role: 'Administrator' },
  { path: `/staff/visits/${ids.visitOpenId}`, role: 'Reception' },
  { path: `/staff/visits/${ids.visitFinalisedId}`, role: 'Vet' },
  { path: `/staff/visits/${ids.visitFinalisedId}/summary`, role: 'Vet' },
  { path: '/staff/diagnostics', role: 'Vet' },
  { path: `/staff/diagnostics/${ids.orderResultedId}`, role: 'Vet' },
  { path: `/staff/diagnostics/${ids.orderImagingId}`, role: 'Vet' },
  { path: consent, role: 'Reception' },
  { path: `/staff/consents/${ids.consentRecordedId}`, role: 'Reception' },
  { path: `/staff/cats/${ids.catWhiskersId}/history`, role: 'Vet' },
  { path: `/staff/cats/${ids.catMittensId}/consents`, role: 'Reception' },
  { path: '/staff/admin/clinical/diagnosis-codes', role: 'Vet' },
  { path: '/staff/admin/clinical/lab-analytes', role: 'Vet' },
  { path: '/staff/admin/clinical/note-templates', role: 'Vet' },
  { path: '/staff/front-desk?tab=queue', role: 'Vet' },
  { path: `/staff/appointments/${ids.queuedBookedAppointmentId}`, role: 'Vet' },
  { path: `/portal/cats/${ids.portalCatId}/visits`, role: 'Customer' },
  { path: `/portal/visits/${ids.visitFinalisedId}`, role: 'Customer' },
  { path: `/portal/visits/${ids.visitOpenId}`, role: 'Customer' },
];

test.describe.configure({ timeout: 90_000 });

const VIEWPORTS = [
  { width: 360, height: 780 },
  { width: 768, height: 1024 },
  { width: 1024, height: 768 },
  { width: 1440, height: 900 },
];

async function signIn(page: Page, role: RouteCase['role']) {
  if (role === 'Customer') await signInAsCustomer(page);
  else await signInAsRole(page, role);
}

async function expectNoSeriousOrCriticalViolations(page: Page): Promise<void> {
  const results = await new AxeBuilder({ page }).analyze();
  const serious = results.violations.filter(
    (v) => v.impact === 'serious' || v.impact === 'critical',
  );
  expect(serious, JSON.stringify(serious, null, 2)).toEqual([]);
}

test.describe('E-X1: axe — zero serious/critical violations', () => {
  for (const { path, role } of ROUTES) {
    test(`${path} as ${role}`, async ({ page }) => {
      await signIn(page, role);
      await page.goto(path);
      await page.waitForLoadState('networkidle');
      await expectNoSeriousOrCriticalViolations(page);
    });
  }

  test('E-X1 (history drawer open over the workspace)', async ({ page }) => {
    await signIn(page, 'Vet');
    await page.goto(`/staff/visits/${ids.visitOpenId}`);
    await page.getByRole('button', { name: 'History' }).click();
    const drawer = page.getByRole('dialog');
    await expect(drawer.getByRole('heading', { name: 'Active problems' })).toBeVisible();
    await expectNoSeriousOrCriticalViolations(page);
    await drawer.getByRole('tab', { name: 'Weights' }).click();
    await expect(drawer.getByText(/Showing/)).toBeVisible();
    await expectNoSeriousOrCriticalViolations(page);
  });

  test('E-X1 (vitals confirm dialog)', async ({ page }) => {
    await signIn(page, 'Vet');
    await page.goto(`/staff/visits/${ids.visitEmptyId}`);
    await page.getByRole('button', { name: 'Record vitals' }).click();
    await page.getByLabel('Temperature, degrees Celsius').fill('43');
    await page.getByRole('button', { name: 'Save vitals' }).click();
    await expect(page.getByRole('dialog', { name: /outside the usual range/i })).toBeVisible();
    await expectNoSeriousOrCriticalViolations(page);
  });

  test('E-X1 (finalise dialog, blocked and ready)', async ({ page }) => {
    await signIn(page, 'Vet');
    await page.goto(`/staff/visits/${ids.visitEmptyId}`);
    await page.getByRole('button', { name: 'Finalise', exact: true }).click();
    await expect(page.getByRole('dialog', { name: /finalise/i })).toBeVisible();
    await expectNoSeriousOrCriticalViolations(page);
    await page.keyboard.press('Escape');
    await page.goto(`/staff/visits/${ids.visitOpenId}`);
    await page.getByRole('button', { name: 'Finalise', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Finalise visit' })).toBeEnabled();
    await expectNoSeriousOrCriticalViolations(page);
  });

  test('E-X1 (diagnostic order drawer, order dialog, quick vitals)', async ({ page }) => {
    await signIn(page, 'Vet');
    await page.goto(`/staff/visits/${ids.visitOpenId}`);
    await page
      .getByRole('button', { name: /Blood work/ })
      .first()
      .click();
    await expect(page.getByRole('dialog', { name: 'Diagnostic order' })).toBeVisible();
    await expectNoSeriousOrCriticalViolations(page);
    await page.keyboard.press('Escape');
    await page.getByRole('button', { name: 'Order test' }).click();
    await expect(page.getByRole('dialog', { name: /order a test/i })).toBeVisible();
    await expectNoSeriousOrCriticalViolations(page);
    await page.keyboard.press('Escape');
    await page.goto('/staff/front-desk?tab=queue');
    await page.getByRole('button', { name: 'Record vitals for Mittens' }).first().click();
    await expect(page.getByRole('dialog', { name: /record vitals/i })).toBeVisible();
    await expect(page.getByLabel('Weight, kilograms')).toBeVisible();
    await expectNoSeriousOrCriticalViolations(page);
  });

  test('E-X1 (consent: upload tab and the unpublished form)', async ({ page }) => {
    await signIn(page, 'Reception');
    await page.goto(consent);
    await page.getByRole('tab', { name: 'Upload signed form' }).click();
    await expectNoSeriousOrCriticalViolations(page);
    await page.goto(consent.replace('kind=Surgery', 'kind=Euthanasia'));
    await expect(page.getByText(/hasn’t been published yet/)).toBeVisible();
    await expectNoSeriousOrCriticalViolations(page);
  });

  test('E-X1 (catalogue editors open)', async ({ page }) => {
    await signIn(page, 'Vet');
    for (const [path, button] of [
      ['/staff/admin/clinical/diagnosis-codes', 'Add a code'],
      ['/staff/admin/clinical/lab-analytes', 'Add an analyte'],
      ['/staff/admin/clinical/note-templates', 'Add a template'],
    ] as const) {
      await page.goto(path);
      await page.getByRole('button', { name: button }).click();
      await expect(page.getByRole('dialog')).toBeVisible();
      await expectNoSeriousOrCriticalViolations(page);
      await page.keyboard.press('Escape');
    }
  });
});

test.describe('E-X2: no horizontal scroll, no clipped control at 360/768/1024/1440', () => {
  for (const { path, role } of ROUTES) {
    for (const viewport of VIEWPORTS) {
      test(`${path} as ${role} at ${viewport.width}px`, async ({ page }) => {
        await signIn(page, role);
        await page.setViewportSize(viewport);
        await page.goto(path);
        await page.waitForLoadState('networkidle');
        const overflow = await page.evaluate(
          () => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1,
        );
        expect(overflow, `${path} overflows at ${viewport.width}px`).toBe(false);
        const clipped = await page.evaluate((width) => {
          return [...document.querySelectorAll('button, a[href], input, select, textarea')]
            .filter((el) => {
              const rect = el.getBoundingClientRect();
              return rect.width > 0 && (rect.right > width + 1 || rect.left < -1);
            })
            .filter((el) => !el.closest('.overflow-x-auto, [role="region"][tabindex]'))
            .map((el) => el.outerHTML.slice(0, 80));
        }, viewport.width);
        expect(clipped, `${path} has clipped controls at ${viewport.width}px`).toEqual([]);
      });
    }
  }

  test('E-X2 (overlays): the history drawer and the dialogs fit a phone', async ({ page }) => {
    await signIn(page, 'Vet');
    await page.setViewportSize({ width: 360, height: 780 });
    await page.goto(`/staff/visits/${ids.visitOpenId}`);
    for (const open of [
      async () => page.getByRole('button', { name: 'History' }).click(),
      async () => page.getByRole('button', { name: 'Order test' }).click(),
      async () => page.getByRole('button', { name: 'Finalise', exact: true }).click(),
    ]) {
      await open();
      const dialog = page.getByRole('dialog');
      await expect(dialog).toBeVisible();
      const box = (await dialog.boundingBox())!;
      expect(box.x).toBeGreaterThanOrEqual(-1);
      expect(box.x + box.width).toBeLessThanOrEqual(361);
      const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1,
      );
      expect(overflow).toBe(false);
      await page.keyboard.press('Escape');
      await expect(dialog).toBeHidden();
    }
  });
});
