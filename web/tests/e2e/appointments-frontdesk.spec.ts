import { test, expect, signInAsStaff } from './fixtures';

/**
 * Front desk — HO-APT-002 §16 E-D series, against MSW. The queue is the Appointments mock's: a
 * waiting Urgent walk-in (Mittens), a booked Routine cat checked in (Mittens), one in consultation.
 */

test.beforeEach(async ({ page }) => {
  await signInAsStaff(page);
});

test.describe('Front desk — US-CLN-05, 10', () => {
  test('E-D1: a walk-in for a new customer is registered and queued in under 60 s (NFR-USA-2)', async ({
    page,
  }) => {
    await page.goto('/staff/front-desk?tab=queue');
    await expect(page.getByRole('heading', { name: 'Clinic queue' })).toBeVisible();
    const before = await page
      .getByRole('listitem')
      .filter({ hasText: /position/i })
      .count();

    const started = Date.now();
    await page.getByRole('button', { name: /new customer/i }).click();
    await page.getByRole('textbox', { name: /^full name/i }).fill('Nadia Hossain');
    await page.getByRole('textbox', { name: /^mobile number/i }).fill('01700000777');
    await page.keyboard.press('Enter');
    await expect(page.getByText('Nadia Hossain')).toBeVisible();

    await page.getByRole('button', { name: /add a cat/i }).click();
    await page.getByLabel(/cat’s name/i).fill('Pepper');
    await page.keyboard.press('Enter');
    await expect(page.getByLabel('Cat', { exact: true })).toHaveValue(/.+/);

    await page.getByLabel('Service').selectOption({ label: 'General consultation' });
    await page.getByRole('radio', { name: /urgent/i }).check();
    await page.getByLabel(/presenting complaint/i).fill('Limping');
    await page.getByRole('button', { name: /add to queue/i }).click();

    await expect(page.getByLabel(/find the customer/i)).toBeFocused();
    await expect(page.getByText('Pepper')).toBeVisible();
    expect(Date.now() - started).toBeLessThan(60_000);
    await expect(page.getByRole('listitem').filter({ hasText: /position/i })).toHaveCount(
      before + 1,
    );
  });

  test('E-D2: an emergency goes first, and later estimates rise on the next refresh', async ({
    page,
  }) => {
    test.setTimeout(90_000);
    await page.goto('/staff/front-desk?tab=queue');
    await expect(page.getByText(/^about [0-9]+ min/i).first()).toBeVisible();
    const beforeTexts = await page.getByText(/^about [0-9]+ min/i).allTextContents();
    const beforeMinutes = Math.max(...beforeTexts.map((t) => Number(t.match(/[0-9]+/)?.[0])));

    // Another terminal registers an emergency (the mock keeps one shared queue per browser tab).
    await page.evaluate(async () => {
      const base = (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl ?? '';
      await fetch(`${base}/api/staff/walk-ins`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          customerId: 'a1000000-0000-0000-0000-000000000003',
          catId: '20000000-0000-0000-0000-000000000003',
          serviceId: 's0000000-0000-0000-0000-00000000a001',
          triagePriority: 'Emergency',
          presentingComplaint: 'Hit by a car',
        }),
      });
    });
    // The 30 s poll replaces the list on its own.
    const first = page
      .getByRole('listitem')
      .filter({ hasText: /position/i })
      .first();
    await expect(first).toContainText('Emergency', { timeout: 45_000 });
    const largestEstimate = async () => {
      const texts = await page.getByText(/^about [0-9]+ min/i).allTextContents();
      return Math.max(...texts.map((t) => Number(t.match(/[0-9]+/)?.[0])));
    };
    await expect.poll(largestEstimate, { timeout: 45_000 }).toBeGreaterThan(beforeMinutes);
  });

  test('E-D3: a booked cat is checked in, started and completed; the card moves through its statuses', async ({
    page,
  }) => {
    await page.goto('/staff/front-desk');
    await page
      .getByRole('button', { name: /^check in farhana|check in cat|check in whiskers/i })
      .first()
      .click();
    await page
      .getByRole('dialog')
      .getByRole('button', { name: /^check in$/i })
      .click();
    await page.getByRole('tab', { name: 'Queue' }).click();
    await expect(page.getByText('Checked in').first()).toBeVisible();

    // Reception or a vet opens My queue (shown for vets and administrators).
    await page.getByLabel('My queue').click();
    await expect(page.getByRole('heading', { name: 'My queue' })).toBeVisible();
    const start = page.getByRole('button', { name: /^start /i }).first();
    await expect(start).toBeVisible();
    await start.click();
    const dialog = page.getByRole('dialog');
    if (await dialog.isVisible().catch(() => false)) {
      await dialog.getByLabel('Vet or nurse').selectOption({ label: 'Dr Rahman' });
      await dialog.getByLabel('Room').selectOption({ index: 1 });
      await dialog.getByRole('button', { name: /start visit/i }).click();
    }
    await expect(page.getByText('In consultation').first()).toBeVisible();
    await page
      .getByRole('button', { name: /^complete /i })
      .first()
      .click();
    await expect(page.getByRole('button', { name: /^complete /i })).toHaveCount(1);
  });
});
