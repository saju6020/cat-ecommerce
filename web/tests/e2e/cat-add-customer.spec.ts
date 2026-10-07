import { test, expect, signInAsCustomer } from './fixtures';
import { seedVaccinationService } from './appointments-helpers';

/**
 * HO-CAT-002-ADDCAT E-AC1 — a customer adds a cat from the portal, sees it, then books with it.
 * The shared cat mock is not owner-scoped (it lists every cat), so "no cats" cannot be staged
 * here; the empty-state link is covered by component test U-AC9 instead. The cat is named so it
 * sorts first in the list and stays on page 1.
 */
test.describe('Add a cat — customer portal (C-APT1)', () => {
  test.beforeEach(async ({ page }) => {
    await signInAsCustomer(page);
  });

  test('E-AC1: add a cat, see it in My cats, then book with it from /book', async ({ page }) => {
    await page.goto('/portal/cats');
    await seedVaccinationService(page);
    await page.getByRole('link', { name: 'Add a cat' }).click();
    await page.waitForURL('**/portal/cats/new');
    await expect(page.getByLabel('Owner customer ID')).toHaveCount(0);

    await page.getByLabel(/^Name/).fill('Aaa Biscuit');
    await page.getByLabel('Breed').fill('Ragdoll');
    await page.getByRole('button', { name: 'Add cat' }).click();
    await expect(page).toHaveURL(/\/portal\/cats\/[0-9a-f-]+$/);
    await expect(page.getByRole('heading', { level: 1, name: 'Aaa Biscuit' })).toBeVisible();

    // Navigate in the app (not page.goto): the mock's cat store lives in the page, so a reload
    // would forget the new cat.
    await page
      .getByRole('navigation', { name: 'Main' })
      .getByRole('link', { name: 'My cats' })
      .click();
    await expect(page.getByRole('link', { name: /Aaa Biscuit/ })).toBeVisible();

    await page
      .getByRole('navigation', { name: 'Main' })
      .getByRole('link', { name: 'Book a visit' })
      .click();
    await page.getByRole('button', { name: /choose aaa biscuit/i }).click();
    await expect(page.getByRole('heading', { name: 'Choose a service' })).toBeVisible();
  });

  test('E-AC2: an empty name keeps the page and the other typed values (NFR-USA-5)', async ({
    page,
  }) => {
    await page.goto('/portal/cats/new');
    await page.getByLabel('Breed').fill('Ragdoll');
    await page.getByRole('button', { name: 'Add cat' }).click();
    await expect(page.locator('#name-error')).toContainText(/enter a name/i);
    await page.waitForURL('**/portal/cats/new');
    await expect(page.getByLabel('Breed')).toHaveValue('Ragdoll');
  });

  test('E-AC3: from the booking flow, Add a cat returns to /book with the cat chosen', async ({
    page,
  }) => {
    await seedVaccinationService(page);
    await page.goto('/portal/cats/new?next=%2Fbook%3Fcategory%3DVaccination');
    await page.getByLabel(/^Name/).fill('Aaa Pepper');
    await page.getByRole('button', { name: 'Add cat' }).click();
    await expect(page).toHaveURL(/\/book\?category=Vaccination&catId=[0-9a-f-]+$/);
    await expect(page.getByRole('heading', { name: 'Vaccination services' })).toBeVisible();
  });
});
