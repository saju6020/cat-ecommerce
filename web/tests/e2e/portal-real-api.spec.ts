// Plain Playwright `test`, not ./fixtures: that wrapper waits for MSW's service worker, which never
// exists when NEXT_PUBLIC_USE_MOCKS=false (the whole point of this spec).
import { test, expect } from '@playwright/test';
import { IS_REAL_API, api } from './emr-helpers';

/**
 * HO-POR-002 §16 — the E-A cases re-run against the REAL API (NEXT_PUBLIC_USE_MOCKS=false, API on
 * http://localhost:5014, fresh database catecommerce_por002e2e). Skipped on mocks. It registers its
 * own customer, so it needs no seed data, and it also pins the dashboard wire contract the mocks
 * were written from (the key set, `Cache-Control: no-store`, section states). Re-order and the
 * order/visit/prescription/balance tiles need orders, finalised visits and invoices that a fresh
 * database does not hold; they are recorded as not run against the real API in TC-HO-POR-002.
 */
test.skip(!IS_REAL_API, 'real-API run only (NEXT_PUBLIC_USE_MOCKS=false)');
test.describe.configure({ timeout: 90_000 });

const CAPTCHA_ANSWER = process.env.E2E_CAPTCHA_ANSWER ?? 'TEST1';
const unique = Date.now().toString().slice(-8);

test('E-A1/E-A2 real: a new customer sees the no-cats invitation, then their cat and its vaccination state', async ({
  page,
}) => {
  const mobile = `019${unique}`;
  const password = 'a-genuinely-long-password-1';

  await page.goto('/register');
  await page.getByLabel('Full name').fill('Portal Real Customer');
  await page.getByLabel(/mobile number/i).fill(mobile);
  await page.getByLabel(/^password/i).fill(password);
  await expect(page.getByAltText(/security check/i)).toBeVisible();
  await page.getByLabel(/security check/i).fill(CAPTCHA_ANSWER);
  await page.getByRole('button', { name: 'Create account' }).click();
  // Owner, 2026-10-19: a new customer is signed in straight after registering (no second sign-in).
  await expect(page).toHaveURL(/\/portal\//, { timeout: 15_000 });

  // The wire: exactly the keys HO-POR-001 delivers; no stays, no subscriptions.
  await page.goto('/portal');
  await expect(page.getByRole('heading', { name: 'Welcome back, Portal' })).toBeVisible({
    timeout: 20_000,
  });
  const dashboard = await api.get(page, '/api/portal/dashboard');
  expect(dashboard.status).toBe(200);
  const body = dashboard.body as Record<string, { state: string; data: unknown }>;
  expect(Object.keys(body).sort()).toEqual(
    [
      'appointments',
      'balances',
      'cats',
      'customer',
      'generatedOn',
      'orders',
      'prescriptions',
      'visits',
    ].sort(),
  );
  for (const key of [
    'customer',
    'cats',
    'appointments',
    'balances',
    'visits',
    'prescriptions',
    'orders',
  ]) {
    expect(['Ready', 'Unavailable']).toContain(body[key]!.state);
  }

  // E-A1: nothing but a Ready, empty cats section shows the invitation.
  expect(body.cats!.state).toBe('Ready');
  await expect(page.getByText('Add your first cat')).toBeVisible();
  await expect(page.getByTestId('tile-appointments')).toContainText('no upcoming appointments');
  await expect(page.getByTestId('tile-orders')).toContainText('haven’t placed an order');
  await expect(page.getByTestId('tile-balances')).toContainText('Nothing to pay right now');
  await expect(page.getByRole('button', { name: /\bpay/i })).toHaveCount(0);

  // E-A2 (cats part): a registered cat appears with a vaccination state, never as "no cats".
  const session = await api.get(page, '/api/auth/session');
  const ownerId = (session.body as { customerId: string }).customerId;
  const created = await api.post(page, '/api/cats', {
    ownerId,
    name: 'Portal Cat',
    species: 'Cat',
    sex: 'Female',
    dateOfBirth: '2024-01-15',
    dateOfBirthPrecision: 'Exact',
    breed: null,
    colourAndMarkings: null,
  });
  expect(created.status).toBeLessThan(300);
  await page.reload();
  const cats = page.getByTestId('tile-cats');
  await expect(cats.getByRole('link', { name: 'Portal Cat' })).toBeVisible({ timeout: 20_000 });
  await expect(page.getByText('Add your first cat')).toHaveCount(0);
  const after = await api.get(page, '/api/portal/dashboard');
  const item = (
    after.body as { cats: { data: { hasCats: boolean; items: Array<Record<string, unknown>> } } }
  ).cats.data.items[0]!;
  expect(Object.keys(item).sort()).toEqual(
    ['catId', 'isHighRisk', 'name', 'vaccination', 'vaccinationState'].sort(),
  );
  console.log(`real-api cat item: ${JSON.stringify(item)}`);
});

test('E-B real: Orders’ re-order answers 404 for an order that is not the caller’s, and needs an Idempotency-Key', async ({
  page,
}) => {
  const mobile = `018${unique}`;
  const password = 'a-genuinely-long-password-1';
  await page.goto('/register');
  await page.getByLabel('Full name').fill('Portal Reorder Customer');
  await page.getByLabel(/mobile number/i).fill(mobile);
  await page.getByLabel(/^password/i).fill(password);
  await page.getByLabel(/security check/i).fill(CAPTCHA_ANSWER);
  await page.getByRole('button', { name: 'Create account' }).click();
  // Owner, 2026-10-19: a new customer is signed in straight after registering (no second sign-in).
  await expect(page).toHaveURL(/\/portal\//, { timeout: 15_000 });

  const unknown = '00000000-0000-0000-0000-0000000000aa';
  const withKey = await page.evaluate(async (orderId) => {
    const base = (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl ?? '';
    const csrf = document.cookie
      .split('; ')
      .find((r) => r.startsWith('pc_csrf='))
      ?.slice(8);
    const call = async (key: string | null) => {
      const response = await fetch(`${base}/api/customers/me/orders/${orderId}/reorder`, {
        method: 'POST',
        credentials: 'include',
        headers: {
          ...(csrf ? { 'X-CSRF-Token': decodeURIComponent(csrf) } : {}),
          ...(key ? { 'Idempotency-Key': key } : {}),
        },
      });
      return response.status;
    };
    return { withoutKey: await call(null), withKey: await call(crypto.randomUUID()) };
  }, unknown);
  // No key: the contract's 400 IdempotencyKeyRequired. With a key: the order is not theirs: 404.
  expect(withKey).toEqual({ withoutKey: 400, withKey: 404 });
});
