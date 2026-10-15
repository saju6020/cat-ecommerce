import fs from 'node:fs';
import crypto from 'node:crypto';
import AxeBuilder from '@axe-core/playwright';
import { test as base, expect, type Browser, type BrowserContext, type Page } from '@playwright/test';

/**
 * HO-BIL-002 §16 — the E-cases re-run against the REAL API (HO-BIL-001 on master). Manual: needs the
 * API on :5007 (with `Cors__AllowedOrigins__0` = this app's origin), the web app on :3112 started with
 * `NEXT_PUBLIC_USE_MOCKS=false NEXT_PUBLIC_API_BASE_URL=http://localhost:5007`, and `BIL_SEED` pointing at
 * a JSON file written by the seeder (admin, reception and accountant mobiles, a branch with a billing
 * profile and a 15 % tax class, three walk-in customers). Skipped otherwise. Serial: the cases share
 * one branch, one till and one set of customers, exactly as a day at the clinic does.
 */
const SEED_PATH = process.env.BIL_SEED;
const enabled = process.env.BIL_REAL_API === '1' && !!SEED_PATH;
const seed = enabled
  ? (JSON.parse(fs.readFileSync(SEED_PATH!, 'utf8')) as {
      password: string; branchId: string; taxId: string; admin: string; reception: string; accountant: string;
      customers: Array<{ id: string; name: string }>;
    })
  : null;

const test = base;
test.describe.configure({ mode: 'serial', timeout: 180_000 });
test.skip(!enabled, 'real-API run only (BIL_REAL_API=1, BIL_SEED=<seed.json>)');

const today = () => new Date().toLocaleDateString('en-CA', { timeZone: 'Asia/Dhaka' });

async function signIn(context: BrowserContext, mobile: string): Promise<Page> {
  const page = await context.newPage();
  await page.goto('/staff/sign-in');
  await page.waitForFunction(() => (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl !== undefined);
  const status = await page.evaluate(async ({ mobile, password }) => {
    const base = (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl ?? '';
    const res = await fetch(`${base}/api/staff/auth/sign-in`, {
      method: 'POST', credentials: 'include', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ identifier: mobile, password }),
    });
    return res.status;
  }, { mobile, password: seed!.password });
  expect(status).toBe(200);
  return page;
}

async function api(page: Page, method: string, path: string, body?: unknown, headers: Record<string, string> = {}) {
  return page.evaluate(async ({ method, path, body, headers, branchId }) => {
    const base = (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl ?? '';
    const csrf = decodeURIComponent((document.cookie.split('; ').find((c) => c.startsWith('pc_csrf=')) ?? '').slice(8));
    const res = await fetch(`${base}${path}`, {
      method, credentials: 'include',
      headers: { 'Content-Type': 'application/json', 'X-CSRF-Token': csrf, 'X-Branch-Id': branchId, ...headers },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    const text = await res.text();
    return { status: res.status, json: text && text.startsWith('{') || text.startsWith('[') ? JSON.parse(text) : null, text };
  }, { method, path, body, headers, branchId: seed!.branchId });
}

async function charge(page: Page, customerId: string, line: 'Clinic' | 'Shop' | 'Boarding', description: string, price: string) {
  const r = await api(page, 'POST', '/api/staff/billing/charges', {
    customerId, description, unitPrice: price, taxClassId: seed!.taxId, quantity: '1', serviceDate: today(), businessLine: line,
  });
  expect(r.status, r.text).toBe(201);
  return r.json.chargeId as string;
}

async function issueInvoice(page: Page, customerId: string, chargeIds: string[]) {
  const d = await api(page, 'POST', '/api/staff/billing/invoices', { customerId, chargeIds });
  expect(d.status, d.text).toBe(201);
  const inv = await api(page, 'GET', `/api/staff/billing/invoices/${d.json.invoiceId}`);
  const i = await api(page, 'POST', `/api/staff/billing/invoices/${d.json.invoiceId}/issue`, { expectedVersion: inv.json.version }, { 'Idempotency-Key': crypto.randomUUID() });
  expect(i.status, i.text).toBe(200);
  return { id: d.json.invoiceId as string, number: i.json.number as string };
}

async function axeClean(page: Page) {
  const results = await new AxeBuilder({ page }).analyze();
  const bad = results.violations.filter((v) => v.impact === 'serious' || v.impact === 'critical');
  expect(bad, JSON.stringify(bad.map((b) => [b.id, b.nodes.map((n) => n.target)]))).toEqual([]);
}

let admin: Page;
let reception: Page;
let accountant: Page;
const state: Record<string, string> = {};

test.beforeAll(async ({ browser }: { browser: Browser }) => {
  if (!enabled) return;
  const baseURL = process.env.PORT ? `http://localhost:${process.env.PORT}` : 'http://localhost:3112';
  admin = await signIn(await browser.newContext({ baseURL }), seed!.admin);
  reception = await signIn(await browser.newContext({ baseURL }), seed!.reception);
  accountant = await signIn(await browser.newContext({ baseURL }), seed!.accountant);
});

test('E-A1: three business lines on one invoice, tax per line, a number in the branch series', async () => {
  const c = seed!.customers[0]!.id;
  const ids = [
    await charge(admin, c, 'Clinic', 'Consultation (real)', '1000.00'),
    await charge(admin, c, 'Shop', 'Cat food (real)', '500.00'),
    await charge(admin, c, 'Boarding', 'Boarding two nights (real)', '800.00'),
  ];
  expect(ids).toHaveLength(3);
  await admin.goto(`/staff/billing/invoices/new?customerId=${c}`);
  for (const line of ['Clinic', 'Shop', 'Boarding']) await admin.getByRole('button', { name: `Select all in ${line}` }).click();
  await expect(admin.getByText(/Estimate at today.s tax rates/)).toBeVisible();
  await expect(admin.getByRole('button', { name: 'Issue invoice' })).toBeEnabled({ timeout: 20_000 });
  await admin.getByRole('button', { name: 'Issue invoice' }).click();
  await admin.getByRole('dialog').getByRole('button', { name: 'Issue invoice' }).click();
  await expect(admin.getByRole('heading', { level: 1 })).toContainText(/INV-.+-\d{4}-\d{6}/, { timeout: 30_000 });
  const lines = admin.getByRole('table', { name: 'Invoice lines' });
  for (const text of ['Clinic', 'Shop', 'Boarding', '15.00%']) await expect(lines).toContainText(text);
  state.invoice1 = admin.url().split('/').pop()!;
  state.number1 = (await admin.getByRole('heading', { level: 1 }).textContent())!;
});

test('E-A2: issuing the same draft twice — one succeeds, the other is refused', async () => {
  const c = seed!.customers[0]!.id;
  const chargeId = await charge(admin, c, 'Clinic', 'Two-tab draft (real)', '200.00');
  const d = await api(admin, 'POST', '/api/staff/billing/invoices', { customerId: c, chargeIds: [chargeId] });
  const id = d.json.invoiceId as string;
  const version = (await api(admin, 'GET', `/api/staff/billing/invoices/${id}`)).json.version;
  const first = await api(admin, 'POST', `/api/staff/billing/invoices/${id}/issue`, { expectedVersion: version }, { 'Idempotency-Key': crypto.randomUUID() });
  const second = await api(admin, 'POST', `/api/staff/billing/invoices/${id}/issue`, { expectedVersion: version }, { 'Idempotency-Key': crypto.randomUUID() });
  expect(first.status).toBe(200);
  expect([400, 409]).toContain(second.status);
});

test('E-B1: split tender — cash 400, card slip 350, transfer 400 — clears the invoice (three receipts)', async () => {
  const c = seed!.customers[1]!.id;
  const ch = await charge(admin, c, 'Clinic', 'Split tender (real)', '1000.00');
  const inv = await issueInvoice(admin, c, [ch]);
  state.invoiceB1 = inv.id;
  await reception.goto(`/staff/billing/payments/new?customerId=${c}&invoiceId=${inv.id}`);
  const amount = reception.getByLabel(/^Amount \(BDT\)/);
  await expect(amount).toHaveValue('1150.00');
  const row = reception.getByLabel(new RegExp(`Amount for ${inv.number}`));
  await amount.fill('400'); await row.fill('400');
  await reception.getByRole('button', { name: 'Record payment' }).click();
  await expect(reception.getByText(/receipt R-\d+/)).toBeVisible({ timeout: 20_000 });
  await reception.getByRole('button', { name: 'Take another payment' }).click();
  await expect(amount).toHaveValue('750.00');
  await reception.getByRole('radio', { name: /Card in store/ }).check();
  await amount.fill('350'); await row.fill('350');
  await reception.getByLabel('Terminal slip number').fill('SLIP-REAL-1');
  await reception.getByRole('button', { name: 'Record payment' }).click();
  await reception.getByRole('button', { name: 'Take another payment' }).click();
  await expect(amount).toHaveValue('400.00');
  await reception.getByRole('radio', { name: /Bank transfer/ }).check();
  await reception.getByLabel('Transfer reference').fill('TRF-REAL-1');
  await reception.getByRole('button', { name: 'Record payment' }).click();
  await expect(reception.getByText('Invoice paid.')).toBeVisible({ timeout: 20_000 });
  const detail = await api(admin, 'GET', `/api/staff/billing/invoices/${inv.id}`);
  expect(detail.json.status).toBe('Paid');
  expect(detail.json.payments).toHaveLength(3);
});

test('E-B2: reception records 60 000; the accountant approves in another context', async () => {
  const c = seed!.customers[1]!.id;
  await reception.goto(`/staff/billing/payments/new?customerId=${c}`);
  await reception.getByLabel(/^Amount \(BDT\)/).fill('60000');
  await reception.getByRole('button', { name: 'Record payment' }).click();
  await expect(reception.getByText(/accountant must approve/i)).toBeVisible({ timeout: 20_000 });
  await accountant.goto('/staff/billing/approvals');
  const approve = accountant.getByRole('button', { name: /^Approve/ }).first();
  await expect(approve).toBeEnabled({ timeout: 20_000 });
  await approve.click();
  await expect(accountant.getByText(/approved\./)).toBeVisible({ timeout: 20_000 });
});

test('E-B3: a card number as reference is blocked in the browser (nothing sent) and refused by the server', async () => {
  const c = seed!.customers[1]!.id;
  const posts: string[] = [];
  reception.on('request', (r) => { if (r.method() === 'POST' && r.url().includes('/billing/payments')) posts.push(r.url()); });
  await reception.goto(`/staff/billing/payments/new?customerId=${c}`);
  await reception.getByRole('radio', { name: /Card in store/ }).check();
  await reception.getByLabel('Terminal slip number').fill('4111 1111 1111 1111');
  await expect(reception.getByText(/looks like a card number/)).toBeVisible();
  await reception.getByRole('button', { name: 'Record payment' }).click();
  await reception.waitForTimeout(800);
  expect(posts).toHaveLength(0);
  const direct = await api(reception, 'POST', '/api/staff/billing/payments', {
    customerId: c, method: 'CardInStore', amount: '10.00', receivedOn: today(), externalReference: '4111 1111 1111 1111', allocations: [],
  });
  expect(direct.status).toBe(400);
  expect(direct.text).toContain('ReferenceLooksLikeCardNumber');
  expect(direct.text).not.toContain('4111');
});

test('E-B4: reverse a payment and re-enter it correctly', async () => {
  const c = seed!.customers[2]!.id;
  const ch = await charge(admin, c, 'Clinic', 'Reverse me (real)', '400.00');
  const inv = await issueInvoice(admin, c, [ch]);
  const pay = await api(reception, 'POST', '/api/staff/billing/payments', {
    customerId: c, method: 'CashInStore', amount: 460, receivedOn: today(), allocations: [{ invoiceId: inv.id, amount: 460 }],
  }, { 'Idempotency-Key': crypto.randomUUID() });
  expect(pay.status, pay.text).toBe(201);
  await accountant.goto(`/staff/billing/payments/${pay.json.paymentId}`);
  await accountant.getByRole('button', { name: 'Reverse', exact: true }).click();
  await expect(accountant.getByRole('dialog')).toContainText('and the day’s cash-up');
  await accountant.getByRole('textbox', { name: 'Reason' }).fill('Entered against the wrong customer');
  await accountant.getByRole('button', { name: 'Reverse payment' }).click();
  await expect(accountant.getByText('Reversed').first()).toBeVisible({ timeout: 20_000 });
  // the server may still be releasing the invoice allocation when the payment already reads Reversed
  await expect.poll(async () => (await api(admin, 'GET', `/api/staff/billing/invoices/${inv.id}`)).json.status, { timeout: 15_000 }).toBe('Issued');
  const again = await api(reception, 'POST', '/api/staff/billing/payments', {
    customerId: c, method: 'CashInStore', amount: 460, receivedOn: today(), allocations: [{ invoiceId: inv.id, amount: 460 }],
  }, { 'Idempotency-Key': crypto.randomUUID() });
  expect(again.status, again.text).toBe(201);
  expect(again.json.receiptNumber).not.toBe(pay.json.receiptNumber);
  expect((await api(admin, 'GET', `/api/staff/billing/invoices/${inv.id}`)).json.status).toBe('Paid');
});

test('E-C1/E-C2/E-C3: credit note keeps credit, apply it elsewhere, corrected re-issue, refund cash', async () => {
  const c = seed!.customers[1]!.id;
  await accountant.goto(`/staff/billing/invoices/${state.invoiceB1}/credit`);
  await accountant.getByRole('textbox', { name: 'Reason' }).fill('Service not performed');
  await accountant.getByRole('button', { name: 'Continue' }).click();
  await accountant.getByRole('button', { name: 'Continue' }).click();
  await accountant.getByRole('button', { name: 'Issue credit note' }).click();
  await accountant.getByRole('dialog').getByRole('button', { name: 'Issue credit note' }).click();
  await expect(accountant.getByText(/Credit note CN-.+ issued/)).toBeVisible({ timeout: 30_000 });
  await expect(accountant.getByText('Released to customer credit')).toBeVisible();
  // apply the released credit to a new invoice
  const ch = await charge(admin, c, 'Clinic', 'Credit target (real)', '300.00');
  const target = await issueInvoice(admin, c, [ch]);
  await accountant.goto(`/staff/billing/customers/${c}`);
  await accountant.getByRole('button', { name: 'Apply credit' }).click();
  await accountant.getByLabel(/^Amount/).fill('100');
  await accountant.getByRole('dialog').getByRole('button', { name: 'Apply credit' }).click();
  await expect(accountant.getByText(/Free credit remaining/)).toBeVisible({ timeout: 20_000 });
  expect((await api(admin, 'GET', `/api/staff/billing/invoices/${target.id}`)).json.paid.amount).toBe('100.00');
  // corrected re-issue links both ways
  const re = await api(admin, 'GET', `/api/staff/billing/invoices/${state.invoiceB1}`);
  const noteId = re.json.creditNotes[0].creditNoteId as string;
  const drafted = await api(admin, 'POST', `/api/staff/billing/invoices/${state.invoiceB1}/corrected-draft`, { creditNoteId: noteId });
  expect(drafted.status, drafted.text).toBe(201);
  const orig = await api(admin, 'GET', `/api/staff/billing/invoices/${state.invoiceB1}`);
  expect(orig.json.correctedByInvoiceId).toBe(drafted.json.invoiceId);
  // refund cash from the card/cash payments' released credit
  const pays = await api(admin, 'GET', `/api/staff/billing/payments?customerId=${c}&status=Accepted`);
  const refundable = (pays.json.items as Array<{ paymentId: string; method: string; unallocated: { amount: string } }>).find((p) => p.method === 'CashInStore' && p.unallocated.amount !== '0.00');
  expect(refundable).toBeTruthy();
  await accountant.goto(`/staff/billing/payments/${refundable!.paymentId}`);
  await accountant.getByRole('button', { name: 'Refund', exact: true }).click();
  await accountant.getByLabel(/^Amount/).fill('50');
  await accountant.getByRole('textbox', { name: 'Reason' }).fill('Customer left early');
  await accountant.getByRole('dialog').getByRole('button', { name: 'Refund', exact: true }).click();
  await expect(accountant.getByText('Refund recorded.')).toBeVisible({ timeout: 20_000 });
});

test('E-D1: a short count needs an explanation before the day closes', async () => {
  await accountant.goto(`/staff/billing/cash-up?date=${today()}`);
  await expect(accountant.getByText(/Expected cash in the till:/)).toBeVisible({ timeout: 20_000 });
  if (await accountant.getByText(/closed and read-only/).isVisible()) return; // a re-run on a day this spec already closed
  const expectedText = await accountant.getByText(/Expected cash in the till:/).textContent();
  const figure = /BDT ([0-9,]+[.][0-9]{2})/.exec(expectedText!)![1]!;
  const cents = BigInt(figure.replace(/[^0-9]/g, '')); // test-side arithmetic only
  const counted = cents - 1000n;
  const asText = `${counted / 100n}.${String(counted % 100n).padStart(2, '0')}`;
  await accountant.getByLabel(/Counted cash/).fill(asText);
  await accountant.getByRole('button', { name: 'Save count' }).click();
  await expect(accountant.getByText(/Short BDT 10\.00/).first()).toBeVisible({ timeout: 20_000 });
  await expect(accountant.getByLabel(/^Explanation/)).toHaveAttribute('required', '');
  await accountant.getByRole('button', { name: 'Close day' }).click();
  await accountant.getByRole('dialog').getByRole('button', { name: 'Close day' }).click();
  await expect(accountant.getByText(/Enter an explanation before closing|waiting for approval/).first()).toBeVisible({ timeout: 20_000 });
  await accountant.getByLabel(/^Explanation/).fill('Coins miscounted');
  await accountant.getByRole('button', { name: 'Save count' }).click();
  await accountant.waitForTimeout(1000);
  await accountant.getByRole('button', { name: 'Close day' }).click();
  await accountant.getByRole('dialog').getByRole('button', { name: 'Close day' }).click();
  await expect(accountant.getByText(/closed and read-only/)).toBeVisible({ timeout: 20_000 });
});

test('E-D2: a COD remittance with a shortfall is listed', async () => {
  const c = seed!.customers[2]!.id;
  const day = new Date(Date.now() - 86_400_000).toLocaleDateString('en-CA', { timeZone: 'Asia/Dhaka' });
  const pay = await api(reception, 'POST', '/api/staff/billing/payments', {
    customerId: c, method: 'CashOnDelivery', amount: 700, receivedOn: day,
    collector: { kind: 'Courier', courierCode: 'REALCOURIER', courierName: 'Real Courier' }, allocations: [],
  }, { 'Idempotency-Key': crypto.randomUUID() });
  expect(pay.status, pay.text).toBe(201);
  await accountant.goto('/staff/billing/reconciliation/cod?courier=REALCOURIER');
  await accountant.getByLabel('Total received').fill('500');
  await accountant.getByLabel(/Collections covered/).first().waitFor({ timeout: 20_000 }).catch(() => {});
  const receipt = pay.json.receiptNumber as string;
  await accountant.getByLabel(new RegExp(receipt)).first().check();
  await accountant.getByLabel(new RegExp(`Remitted for ${receipt}`)).fill('500');
  await accountant.getByRole('button', { name: 'Record remittance' }).click();
  await expect(accountant.getByText(/Remittance recorded/)).toBeVisible({ timeout: 20_000 });
  await expect(accountant.getByText(/Shortfall lines/)).toBeVisible({ timeout: 20_000 });
});

test('E-D3: import the statement, auto-match, match the ambiguous line by hand', async () => {
  const c = seed!.customers[0]!.id;
  const mk = (amount: number, ref: string) => api(reception, 'POST', '/api/staff/billing/payments', {
    customerId: c, method: 'BankTransfer', amount, receivedOn: today(), externalReference: ref, allocations: [],
  }, { 'Idempotency-Key': crypto.randomUUID() });
  const run = Date.now() % 90000;
  const made: string[] = [];
  for (const [a, r] of [[777.25 + (run % 50), `TRF-UNIQ-${run}`], [500.5, `TRF-AMB-1-${run}`], [500.5, `TRF-AMB-2-${run}`]] as const) {
    const res = await mk(a, r);
    expect(res.status).toBe(201);
    made.push(res.json.receiptNumber as string);
  }
  const d = today();
  const csv = `date,amount,direction,reference,description,bankTransactionId\n${d},${(777.25 + (run % 50)).toFixed(2)},C,TRF-UNIQ-${run},Transfer,T1\n${d},500.50,C,TRF-AMB,Transfer,T2\n`;
  await accountant.goto('/staff/billing/reconciliation/bank');
  await accountant.getByLabel('Bank account').fill(`Real account ${Date.now()}`);
  await accountant.getByLabel('Period from').fill(d);
  await accountant.getByLabel('Period to').fill(d);
  await accountant.getByLabel(/Statement file/).setInputFiles({ name: 'real.csv', mimeType: 'text/csv', buffer: Buffer.from(csv) });
  await accountant.getByRole('button', { name: 'Import statement' }).click();
  await expect(accountant).toHaveURL(/reconciliation\/bank\/[0-9a-f-]+$/, { timeout: 30_000 });
  await accountant.getByRole('button', { name: 'Auto-match' }).click();
  await expect(accountant.getByText(/Auto-match finished: 1 matched/)).toBeVisible({ timeout: 20_000 });
  await accountant.getByRole('button', { name: /Select\s*statement line/ }).first().click();
  await accountant.getByRole('button', { name: /Select\s*payment/ }).filter({ hasText: /Select/ }).first().waitFor();
  await accountant.getByRole("button", { name: new RegExp("Select[ ]*payment " + made[1]) }).click();
  await accountant.getByRole('button', { name: /Match the selected pair/ }).click();
  await expect(accountant.getByText(/Matched line/)).toBeVisible({ timeout: 20_000 });
  await expect(accountant.getByText(/Unmatched statement lines \(0\)/)).toBeVisible({ timeout: 20_000 });
});

test('E-D4: the same export range twice gives identical bytes', async () => {
  const url = `${process.env.API_BASE ?? 'http://localhost:5007'}/api/staff/billing/accounting-exports/download?from=${today()}&to=${today()}`;
  const hash = async () => {
    const res = await accountant.context().request.get(url);
    expect(res.status()).toBe(200);
    return crypto.createHash('sha256').update(await res.body()).digest('hex') + ':' + res.headers()['x-export-sha256'];
  };
  const a = await hash();
  const b = await hash();
  expect(a).toBe(b);
  await accountant.goto('/staff/billing/exports');
  await expect(accountant.getByRole('table', { name: 'Accounting export runs' })).toBeVisible({ timeout: 20_000 });
});

test('E-X1/E-X2: axe and no horizontal scroll on real data', async () => {
  const c = seed!.customers[1]!.id;
  const routes = ['/staff/billing', `/staff/billing/customers/${c}`, '/staff/billing/invoices', `/staff/billing/invoices/${state.invoice1}`, '/staff/billing/payments', '/staff/billing/approvals', '/staff/billing/receivables', `/staff/billing/cash-up?date=${today()}`, '/staff/billing/reconciliation/cod', '/staff/billing/reconciliation/bank', '/staff/billing/exports', '/staff/admin/billing-profile'];
  for (const route of routes) {
    for (const width of [360, 1440]) {
      await accountant.setViewportSize({ width, height: 900 });
      await accountant.goto(route);
      await accountant.waitForLoadState('networkidle');
      await accountant.waitForTimeout(800);
      const over = await accountant.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
      expect(over, `${route} @${width}`).toBeLessThanOrEqual(0);
      await axeClean(accountant);
    }
  }
});

test('E-F1: a customer opens their invoice and downloads the PDF; another customer’s id is not found', async ({ browser }) => {
  const baseURL = process.env.PORT ? `http://localhost:${process.env.PORT}` : 'http://localhost:3112';
  const context = await browser.newContext({ baseURL });
  const page = await context.newPage();
  const unique = String(Date.now()).slice(-8);
  const mobile = `019${unique}`;
  const password = 'a-genuinely-long-password-1';
  await page.goto('/register');
  await page.getByLabel('Full name').fill('Portal Billing Customer');
  await page.getByLabel(/mobile number/i).fill(mobile);
  await page.getByLabel(/^password/i).fill(password);
  await expect(page.getByAltText(/security check/i)).toBeVisible({ timeout: 20_000 });
  await page.getByLabel(/security check/i).fill(process.env.E2E_CAPTCHA_ANSWER ?? 'TEST1');
  await page.getByRole('button', { name: 'Create account' }).click();
  await expect(page.getByRole('heading', { name: 'Your account is ready' })).toBeVisible({ timeout: 20_000 });
  await page.goto('/sign-in');
  await page.getByLabel(/mobile number or email/i).fill(mobile);
  await page.getByLabel(/^password/i).fill(password);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page).toHaveURL(/\/portal\//, { timeout: 20_000 });
  const customerId = await page.evaluate(async () => {
    const base = (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl ?? '';
    const res = await fetch(`${base}/api/auth/session`, { credentials: 'include' });
    return ((await res.json()) as { customerId: string }).customerId;
  });
  const ch = await charge(admin, customerId, 'Clinic', 'Portal invoice (real)', '600.00');
  const inv = await issueInvoice(admin, customerId, [ch]);
  await page.goto(`/portal/billing/invoices/${inv.id}`);
  await expect(page.getByRole('heading', { name: inv.number })).toBeVisible({ timeout: 20_000 });
  await expect(page.getByRole('heading', { name: 'How to pay' })).toBeVisible();
  const pdf = page.getByRole('link', { name: 'Download PDF' });
  const href = (await pdf.getAttribute('href'))!;
  const res = await context.request.get(href);
  expect(res.status()).toBe(200);
  expect(res.headers()['content-type']).toContain('application/pdf');
  await page.goto('/portal/billing');
  await expect(page.getByRole('link', { name: inv.number })).toBeVisible({ timeout: 20_000 });
  await axeClean(page);
  // another customer's invoice id is a not-found state, never an error
  await page.goto(`/portal/billing/invoices/${state.invoice1}`);
  await expect(page.getByText('Invoice not found')).toBeVisible({ timeout: 20_000 });
});

test('E-DL: staff PDF and CSV downloads carry X-Branch-Id and save a real file', async () => {
  await accountant.setViewportSize({ width: 1280, height: 900 });
  await accountant.goto(`/staff/billing/invoices/${state.invoice1}`);
  const pdfRequest = accountant.waitForRequest((r) => r.url().endsWith('.pdf'));
  const pdfDownload = accountant.waitForEvent('download');
  await accountant.getByRole('button', { name: 'Download PDF' }).click();
  const request = await pdfRequest;
  expect(request.headers()['x-branch-id']).toBe(seed!.branchId);
  const download = await pdfDownload;
  expect(download.suggestedFilename()).toMatch(/\.pdf$/);
  await accountant.goto('/staff/billing/payments');
  const csvRequest = accountant.waitForRequest((r) => r.url().includes('/payments/export.csv'));
  const csvDownload = accountant.waitForEvent('download');
  await accountant.getByRole('button', { name: 'Export CSV' }).click();
  expect((await csvRequest).headers()['x-branch-id']).toBe(seed!.branchId);
  expect((await csvDownload).suggestedFilename()).toMatch(/\.csv$/);
});
