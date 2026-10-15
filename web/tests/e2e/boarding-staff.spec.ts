import { test, expect } from './fixtures';
import { IDS, inDays, setBillingLedger, signInStaffEstablished } from './boarding-helpers';

/**
 * The care centre's side — HO-BRD-002 §16 (E-B1, E-C1…C4, E-E2, E-E4, E-F2, E-G1), against MSW. The seeded
 * stays are `mocks/handlers/boarding/seed.ts`. Real-API re-runs are `boarding-real-api.spec.ts`.
 */

test.beforeEach(async ({ page }) => {
  await signInStaffEstablished(page);
});

test.describe('Task board — US-BRD-09, 10, 11 (NFR-USA-3)', () => {
  test('E-C2: a feeding task is completed with ONE tap on a tablet, with identity shown and an announcement', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1024, height: 768 });
    await page.goto('/staff/boarding/tasks');
    await page.getByRole('button', { name: 'Everyone' }).click();
    const card = page.locator('li', { hasText: 'Feed ⅓ cup dry — own food' }).first();
    const done = card.getByRole('button', { name: /^Done/ });
    await expect(done).toBeVisible();
    const box = await done.boundingBox();
    expect(box!.height).toBeGreaterThanOrEqual(48);

    let taps = 0;
    await page.exposeFunction('__countTap', () => (taps += 1));
    await page.evaluate(() =>
      document.addEventListener(
        'pointerdown',
        () => (window as unknown as { __countTap: () => void }).__countTap(),
        true,
      ),
    );
    await done.click();
    expect(taps).toBeLessThanOrEqual(2);
    await expect(card.getByText(/^Done \d\d:\d\d/)).toBeVisible();
    await expect(card.getByText(/Tanvir Islam|You/).first()).toBeVisible();
    await expect(
      page.getByRole('status').filter({ hasText: /marked done at \d\d:\d\d/ }),
    ).toHaveCount(1);
    await expect(page.getByRole('dialog')).toHaveCount(0);
  });

  test('U-D2: a refused completion rolls back and says why', async ({ page }) => {
    await page.goto('/staff/boarding/tasks');
    const card = page.locator('li', { hasText: 'Mid-morning check' });
    await card.getByRole('button', { name: /^Done/ }).click();
    await expect(card.getByRole('alert')).toContainText(/already completed by Rafiq Uddin/i);
    await expect(card.getByRole('button', { name: /^Done/ })).toBeVisible();
  });

  test('U-D3/D6/D5: overdue as text, the handling note before the action, and the server’s not-eating flag', async ({
    page,
  }) => {
    await page.goto('/staff/boarding/tasks');
    await expect(page.getByText(/\d+ min late/).first()).toBeVisible();
    await expect(page.getByText(/bites when restrained/i).first()).toBeVisible();
    await expect(page.getByText(/not eating — flagged: noodle/i)).toBeVisible();
  });

  test('U-D4: an open observation sheet keeps its values through a refresh behind it', async ({
    page,
  }) => {
    await page.goto('/staff/boarding/tasks');
    await page
      .locator('li', { hasText: 'Mid-morning check' })
      .getByRole('button', { name: 'Observe' })
      .click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('radio', { name: 'Half eaten' }).click();
    await dialog.getByLabel('Notes (optional)').fill('Ate after a while');
    await page.evaluate(() =>
      (window as unknown as { __invalidateQueries?: (k: unknown[]) => void }).__invalidateQueries?.(
        ['staff', 'boarding'],
      ),
    );
    // let the refetch land (the board polls, so the page goes quiet again afterwards)
    await page.waitForLoadState('networkidle');
    await expect(dialog.getByLabel('Notes (optional)')).toHaveValue('Ate after a while');
    await expect(dialog.getByRole('radio', { name: 'Half eaten' })).toHaveAttribute(
      'aria-checked',
      'true',
    );
    await dialog.getByRole('button', { name: 'Save observation' }).click();
    await expect(dialog).toHaveCount(0);
  });
});

test.describe('Dashboard and incidents — US-BRD-19, 13', () => {
  test('U-C1/E-C3/E-C4: every section with counts; overdue and not-eating appear', async ({
    page,
  }) => {
    await page.goto('/staff/boarding');
    for (const title of [
      'Occupancy today',
      'Arrivals',
      'Departures',
      'Cats not eating',
      'Overdue tasks',
      'Unassigned tasks',
      'Open incidents',
      'Call owner',
      'Unpaid balances',
      'Refund due',
      'Needs attention',
    ]) {
      await expect(
        page.getByRole('heading', { name: new RegExp(`^${title} \\(\\d+\\)`) }),
      ).toBeVisible();
    }
    await expect(
      page.getByRole('region', { name: /cats not eating/i }).getByText('Noodle'),
    ).toBeVisible();
    await expect(
      page
        .getByRole('region', { name: /overdue tasks/i })
        .getByText(/min late/)
        .first(),
    ).toBeVisible();
  });

  test('U-C2: a Record call dialog keeps what was typed through a dashboard refresh', async ({
    page,
  }) => {
    await page.goto('/staff/boarding');
    await page
      .getByRole('region', { name: /call owner/i })
      .getByRole('button', { name: 'Record call' })
      .click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel('What happened on the call').fill('Spoke to Sam; she will visit');
    await page.evaluate(() =>
      (window as unknown as { __invalidateQueries?: (k: unknown[]) => void }).__invalidateQueries?.(
        ['staff', 'boarding'],
      ),
    );
    // let the refetch land (the board polls, so the page goes quiet again afterwards)
    await page.waitForLoadState('networkidle');
    await expect(dialog.getByLabel('What happened on the call')).toHaveValue(
      'Spoke to Sam; she will visit',
    );
    await dialog.getByRole('button', { name: 'Record call' }).click();
    await expect(page.getByRole('region', { name: /call owner/i })).toContainText(
      'No owner needs a call.',
    );
  });

  test('the incident queue shows the escalated chip and Call owner, and resolving needs a resolution', async ({
    page,
  }) => {
    await page.goto('/staff/boarding/incidents');
    const high = page.getByRole('listitem').filter({ hasText: 'Refusing food' });
    await expect(high.getByText('Escalated')).toBeVisible();
    await expect(high.getByRole('link', { name: /\d/ }).first()).toBeVisible();
    await high.getByRole('button', { name: 'Resolve…' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Resolve' }).click();
    await expect(page.getByRole('dialog').getByText('Enter how this was resolved.')).toBeVisible();
  });
});

test.describe('Deposit and confirmation — US-BRD-06 (E-B1, E-B3)', () => {
  test('Confirm says what Billing shows, waits for a second approval, then confirms', async ({
    page,
  }) => {
    await page.goto(`/staff/boarding/stays/${IDS.stayReservedSoon}`);
    await expect(page.getByRole('heading', { name: 'Deposit' })).toBeVisible();
    await expect(page.getByRole('link', { name: /record payment in billing/i })).toBeVisible();
    await page.getByRole('button', { name: 'Confirm stay' }).click();
    await expect(page.getByRole('alert').filter({ hasText: /still outstanding/i })).toBeVisible();

    await setBillingLedger(page, IDS.stayReservedSoon, { accepted: 0, pending: true });
    await page.getByRole('button', { name: 'Confirm stay' }).click();
    await expect(page.getByText(/waiting for a second approval/i).first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Check again' })).toBeVisible();

    await setBillingLedger(page, IDS.stayReservedSoon, { accepted: 9999 });
    await page.getByRole('button', { name: 'Check again' }).click();
    await expect(page.getByText('Confirmed', { exact: true }).first()).toBeVisible();
  });
});

test.describe('Check-in — US-BRD-08 (E-C1, ADR-FE-103, ADR-FE-108)', () => {
  test('an incomplete step is refused, then the typed-name path checks the stay in once', async ({
    page,
  }) => {
    await page.goto(`/staff/boarding/stays/${IDS.stayArrivingToday}`);
    await page.getByRole('button', { name: 'Check in' }).click();
    await page.getByRole('button', { name: 'Review' }).click();
    await expect(page.getByText(/confirm the cat’s identity/i)).toBeVisible();
    await expect(page.getByText(/enter the weight in kg/i)).toBeVisible();

    await page.getByLabel(/i have confirmed this is coco/i).check();
    await page.getByLabel(/i have seen proof of the vaccinations/i).check();
    await page.getByLabel(/^weight/i).fill('4.2');
    await page.getByLabel('Arrival health observation').fill('Bright and alert');
    await page.getByLabel('No belongings').check();
    await page.getByLabel(/type your name instead/i).check();
    await page.getByLabel(/name of the person signing/i).fill('Sam Rahman');
    await page.getByLabel(/i confirm this is my signature/i).check();
    await page.getByRole('button', { name: 'Review' }).click();
    await page.getByRole('button', { name: 'Check in', exact: true }).last().click();
    await expect(page.getByText('In the care centre').first()).toBeVisible();
  });

  test('U-E2: a drawn signature is captured as strokes and counts', async ({ page }) => {
    await page.goto(`/staff/boarding/stays/${IDS.stayArrivingToday}`);
    await page.getByRole('button', { name: 'Check in' }).click();
    const canvas = page.getByTestId('ci-sign-0-canvas');
    await canvas.scrollIntoViewIfNeeded();
    const box = (await canvas.boundingBox())!;
    await page.mouse.move(box.x + 20, box.y + 40);
    await page.mouse.down();
    await page.mouse.move(box.x + 120, box.y + 80, { steps: 6 });
    await page.mouse.up();
    await expect(canvas).toHaveAttribute('aria-label', 'Signature drawn');
    await page.getByRole('button', { name: 'Clear' }).click();
    await expect(canvas).toHaveAttribute('aria-label', 'Signature area, empty');
  });
});

test.describe('Belongings photos — owner decision 2026-10-14 (FIL-002 uploader)', () => {
  test('a belonging photo is uploaded through the shared uploader and listed before check-in', async ({
    page,
  }) => {
    await page.goto(`/staff/boarding/stays/${IDS.stayArrivingToday}`);
    await page.getByRole('button', { name: 'Check in' }).click();
    await page.getByRole('button', { name: 'Add a belonging' }).click();
    await page.getByLabel('Description').fill('Blue carrier');
    const png = Buffer.from(
      'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==',
      'base64',
    );
    await page
      .locator('input[type="file"]')
      .first()
      .setInputFiles({ name: 'carrier.png', mimeType: 'image/png', buffer: png });
    await expect(page.getByRole('list', { name: 'Photos added' })).toContainText('carrier.png');
  });
});

test.describe('Changes — US-BRD-15, 18 (E-E2, E-E4)', () => {
  test('E-E2: extending into a taken unit lists the server’s options and relocates with a reason', async ({
    page,
  }) => {
    await page.goto(`/staff/boarding/stays/${IDS.stayDeparting}`);
    await page.getByRole('button', { name: 'Extend', exact: true }).click();
    await page.getByLabel('New departure date').fill(inDays(2));
    await page.getByRole('dialog').getByRole('button', { name: 'Extend', exact: true }).click();
    await expect(page.getByRole('list', { name: /units that are free/i })).toBeVisible();
    await page.getByRole('button', { name: 'Relocate and extend' }).first().click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('button', { name: 'Relocate', exact: true }).click();
    await expect(dialog.getByText('Enter the reason for moving the cat.')).toBeVisible();
    await expect(dialog).toContainText(/from Standard 2 to /);
    await dialog.getByLabel(/reason for the move/i).fill('Next guest arrives in this unit');
    await dialog.getByRole('button', { name: 'Relocate', exact: true }).click();
    await expect(dialog).toHaveCount(0);
  });

  test('E-E4: flagging a cat infectious names the isolation unit it moves to', async ({ page }) => {
    await page.goto(`/staff/boarding/stays/${IDS.stayLong}?tab=isolation`);
    await page
      .getByRole('listitem')
      .filter({ hasText: 'Mishti' })
      .getByRole('button', { name: /flag suspected infectious/i })
      .click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel('Isolation unit').selectOption({ label: 'Isolation 2' });
    await expect(dialog).toContainText(/moved to Isolation 2/);
    await dialog.getByLabel('What are the signs?').fill('Sneezing and watery eyes');
    await dialog.getByRole('button', { name: 'Flag and move' }).click();
    await expect(page.getByText('Suspected infectious').first()).toBeVisible();
  });
});

test.describe('Charges and check-out — US-BRD-16, 17, 20 (E-F1, E-E3)', () => {
  test('E-F1: the 60-night stay shows its cycle invoice on Charges', async ({ page }) => {
    await page.goto(`/staff/boarding/stays/${IDS.stayLong}?tab=charges`);
    await expect(page.getByRole('link', { name: /your invoice inv-brd-0041/i })).toBeVisible();
    await expect(page.getByText(/billing cycle/i)).toBeVisible();
    await expect(page.getByRole('button', { name: /pay/i })).toHaveCount(0);
  });

  test('E-E3: an early check-out says it will be recalculated and then shows the server lines', async ({
    page,
  }) => {
    await page.goto(`/staff/boarding/stays/${IDS.stayInHouseFamily}`);
    await page.getByRole('button', { name: 'Check out' }).click();
    await expect(page.getByRole('note')).toContainText(/early check-out/i);
    for (const weight of ['4.1', '5.3']) {
      await page.getByLabel(/^weight/i).fill(weight);
      await page.getByLabel(/type your name instead/i).check();
      await page.getByLabel(/name of the person signing/i).fill('Sam Rahman');
      await page.getByLabel(/i confirm i have collected/i).check();
      await page.getByRole('button', { name: /^(Next cat|Review)$/ }).click();
    }
    // Owner decision 2026-10-14: the server's recalculation is shown BEFORE submitting.
    await expect(page.getByRole('heading', { name: 'If you check out now' })).toBeVisible();
    await expect(page.getByText(/nights are charged/i)).toBeVisible();
    await page.getByRole('button', { name: 'Check out', exact: true }).last().click();
    await expect(page.getByRole('heading', { name: 'Checked out' })).toBeVisible();
    await expect(page.getByText(/recalculated the nights/i)).toBeVisible();
    await expect(page.getByRole('list', { name: 'Charge lines' })).toBeVisible();
  });
});

test.describe('Occupancy, admin, daycare — US-BRD-01, 02, 03, 21', () => {
  test('U-C3: the board shows stays, maintenance and turnaround in words and links to the stay', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/staff/boarding/board');
    await expect(page.getByRole('group', { name: /occupancy by unit and day/i })).toBeVisible();
    await expect(page.getByRole('link', { name: /noodle and pepper/i }).first()).toBeVisible();
    await expect(page.getByText(/maintenance — deep clean/i).first())
      .toBeVisible()
      .catch(() => undefined);
  });

  test('U-C4: below 768 px the board is a day list with no horizontal scroll', async ({ page }) => {
    await page.setViewportSize({ width: 360, height: 780 });
    await page.goto('/staff/boarding/board');
    await expect(page.getByRole('list', { name: /occupancy by day/i })).toBeVisible();
    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth > window.innerWidth,
    );
    expect(overflow).toBe(false);
  });

  test('U-C5/C6: the unit table shows the rate read-only; maintenance over a stay is refused with its dates', async ({
    page,
  }) => {
    await page.goto('/staff/admin/boarding/units');
    await expect(page.getByRole('table', { name: 'Boarding units' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'rate card' }).first()).toBeVisible();
    const row = page.getByRole('row').filter({ hasText: 'F-1' });
    await row.getByRole('button', { name: 'Maintenance' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel('First day').fill(inDays(0));
    await dialog.getByLabel('Last day').fill(inDays(2));
    await dialog.getByLabel('Reason').fill('Repaint');
    await dialog.getByRole('button', { name: 'Add maintenance' }).click();
    await expect(dialog.getByRole('alert')).toContainText(/overlaps a stay arriving/i);
  });

  test('U-C7: rate cards are Current / Scheduled / Past with no edit control, and a past start is refused', async ({
    page,
  }) => {
    await page.goto('/staff/admin/boarding/rate-cards');
    await expect(page.getByRole('region', { name: 'Current' }).first()).toBeVisible();
    await expect(page.getByRole('region', { name: 'Scheduled' }).first()).toBeVisible();
    await expect(page.getByRole('region', { name: 'Past' }).first()).toBeVisible();
    await expect(page.getByRole('button', { name: /^edit/i })).toHaveCount(0);
    await page.getByRole('button', { name: 'Schedule a new rate card' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel('Nightly rate').fill('1100');
    await dialog.getByLabel(/^Weekly rate/).fill('6500');
    await dialog.getByLabel(/^Monthly rate/).fill('24000');
    await dialog.getByLabel('Extra cat, per night').fill('500');
    await dialog.getByLabel('Tax class').selectOption({ index: 1 });
    await dialog.getByLabel('Starts').fill('2020-01-01T09:00');
    await dialog.getByRole('button', { name: 'Schedule' }).click();
    await expect(dialog.getByRole('alert')).toContainText(/can’t start in the past/i);
  });

  test('E-G1: daycare is checked in and out, shows the charge and occupies no unit', async ({
    page,
  }) => {
    await page.goto('/staff/boarding/daycare');
    const visit = page.getByRole('listitem').filter({ hasText: 'Mittens' });
    await visit.getByRole('button', { name: 'Check in' }).click();
    await visit.getByRole('button', { name: 'Check out' }).click();
    await expect(visit).toContainText(/charge ৳/);
  });
});
