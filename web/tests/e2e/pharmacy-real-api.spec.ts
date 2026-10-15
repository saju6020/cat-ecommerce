/* eslint-disable @typescript-eslint/no-explicit-any -- the real API's JSON bodies are read untyped in this spec */
// Plain Playwright `test`, not ./fixtures: that wrapper waits for MSW's service worker, which never
// exists when NEXT_PUBLIC_USE_MOCKS=false (the point of this spec).
import fs from 'node:fs';
import path from 'node:path';
import {
  test,
  expect as baseExpect,
  request as pwRequest,
  type APIRequestContext,
  type Page,
} from '@playwright/test';

/**
 * HO-PHM-002 §5.1 / §16 - the E-cases re-run against the REAL API (no MSW). Opt-in only, skipped in every
 * default (MSW) run. It needs: a live API (HO-PHM-001 as merged) on a database seeded with clearly named
 * test data, the web dev server with `NEXT_PUBLIC_USE_MOCKS=false`, and
 *
 *   E2E_REAL_PHARMACY=1
 *   E2E_SEED_FILE=<json written by the seeding script: staff, customer, cats, visits, skus, batches, meds, locs>
 *   E2E_API_URL=http://localhost:5013     (the API; also read from web/.env.e2e.local)
 *   E2E_STAFF_PASSWORD / E2E_CUSTOMER_PASSWORD  (git-ignored web/.env.e2e.local, loaded below)
 *
 *   PORT=3116 E2E_REAL_PHARMACY=1 E2E_SEED_FILE=... npx playwright test pharmacy-real-api --project=chromium --workers=1
 *
 * Staff are e2e.{vet,vet2,nurse,reception,admin}@purrcare.test on the main branch. API-side set-up and
 * verification use their own cookie-and-CSRF sessions (APIRequestContext); the behaviour under test goes
 * through the browser.
 */
const run = process.env.E2E_REAL_PHARMACY === '1';
test.skip(!run, 'Set E2E_REAL_PHARMACY=1 (see the header) to run against the real API.');

function loadEnvFile(): void {
  const file = path.join(process.cwd(), '.env.e2e.local');
  if (!fs.existsSync(file)) return;
  for (const line of fs.readFileSync(file, 'utf8').split(/\r?\n/)) {
    const m = /^([A-Z0-9_]+)=(.*)$/.exec(line.trim());
    if (m && process.env[m[1]!] === undefined) process.env[m[1]!] = m[2];
  }
}
loadEnvFile();

const expect = baseExpect.configure({ timeout: 20_000 });
const API = process.env.E2E_API_URL ?? 'http://localhost:5013';
const WEB_ORIGIN = `http://localhost:${process.env.PORT ?? '3116'}`;
const PASSWORD = process.env.E2E_STAFF_PASSWORD ?? '';
const seed = run && process.env.E2E_SEED_FILE ? JSON.parse(fs.readFileSync(process.env.E2E_SEED_FILE, 'utf8')) : ({} as any);
// the branch the seeding script made the e2e staff members of (a fresh database has no clinic branch to borrow)
const MAIN: string = seed.branchId ?? '';
const email = (k: string) => `e2e.${k}@purrcare.test`;
type Role = 'admin' | 'vet' | 'vet2' | 'nurse' | 'reception';

// Cases run in file order (--workers=1); a failure does not skip the rest.
test.setTimeout(120_000);

// ---- API-side sessions ------------------------------------------------------------------------

type Res = { status: number; body: any; headers: Record<string, string> };
class Session {
  constructor(private ctx: APIRequestContext) {}
  private async csrf(): Promise<string> {
    const state = await this.ctx.storageState();
    const c = state.cookies.find((x) => x.name === 'pc_csrf');
    return c ? decodeURIComponent(c.value) : '';
  }
  async call(method: string, url: string, data?: unknown, headers: Record<string, string> = {}): Promise<Res> {
    const csrf = await this.csrf();
    const res = await this.ctx.fetch(url, {
      method,
      data,
      headers: { ...(csrf ? { 'X-CSRF-Token': csrf } : {}), ...headers },
    });
    const text = await res.text();
    let body: any = null;
    try {
      body = text ? JSON.parse(text) : null;
    } catch {
      body = text;
    }
    return { status: res.status(), body, headers: res.headers() };
  }
  get = (u: string) => this.call('GET', u);
  post = (u: string, d: unknown = {}) => this.call('POST', u, d);
  put = (u: string, d: unknown = {}) => this.call('PUT', u, d);
  del = (u: string) => this.call('DELETE', u);
  dispose = () => this.ctx.dispose();
}

async function newSession(kind: 'staff' | 'customer', identifier: string, password: string = PASSWORD): Promise<Session> {
  const ctx = await pwRequest.newContext({
    baseURL: API,
    extraHTTPHeaders: { Origin: WEB_ORIGIN, ...(kind === 'staff' ? { 'X-Branch-Id': MAIN } : {}) },
  });
  const s = new Session(ctx);
  const r = await s.post(kind === 'staff' ? '/api/staff/auth/sign-in' : '/api/auth/sign-in', { identifier, password });
  if (r.status !== 200) throw new Error(`API sign-in ${identifier}: ${r.status} ${JSON.stringify(r.body)}`);
  return s;
}

const S = {} as Record<Role | 'customer', Session>;

test.beforeAll(async () => {
  for (const r of ['admin', 'vet', 'vet2', 'nurse', 'reception'] as Role[]) S[r] = await newSession('staff', email(r));
  S.customer = await newSession('customer', seed.customer?.mobile, seed.customer?.password);
});
test.afterAll(async () => {
  for (const s of Object.values(S)) await s?.dispose();
});

// ---- data helpers (API) -----------------------------------------------------------------------

const unwrap = (r: Res, want: number[], what: string) => {
  if (!want.includes(r.status)) throw new Error(`${what}: ${r.status} ${JSON.stringify(r.body)}`);
  return r.body;
};

function issueBody(visitId: string, medicineId: string, o: Record<string, unknown> = {}) {
  return {
    visitId,
    medicineId,
    dose: { amount: 2, unit: 'Ml' },
    route: 'Oral',
    frequency: 'OnceDaily',
    durationDays: 5,
    quantity: 10,
    instructions: 'Give with food.',
    isRepeatable: false,
    repeatsAuthorised: 0,
    validUntil: null,
    acknowledgedWarningsFingerprint: null,
    allergyAcknowledgementReason: null,
    doseAcknowledgementNote: null,
    ...o,
  };
}

/** A prescription issued by the vet through the API (fixed 2 ml unless overridden); acknowledges whatever the check asks to. */
async function issueRx(cat: string, med: string, o: Record<string, unknown> = {}, catId = seed.cats[cat], visitId = seed.visits[cat]) {
  const body = issueBody(visitId, seed.meds[med], o);
  const check = unwrap(
    await S.vet.post(`/api/staff/cats/${catId}/prescriptions/safety-check`, { medicineId: body.medicineId, dose: body.dose, route: body.route, frequency: body.frequency }),
    [200],
    `check ${med}`,
  );
  const warnings = check.warnings as any[];
  if (warnings.some((w) => w.requiresAcknowledgement)) {
    Object.assign(body, {
      acknowledgedWarningsFingerprint: check.fingerprint,
      allergyAcknowledgementReason: warnings.some((w) => w.kind === 'AllergyConflict') ? 'e2e seed' : null,
      doseAcknowledgementNote: 'e2e seed',
    });
  }
  const b = unwrap(await S.vet.post(`/api/staff/cats/${catId}/prescriptions`, body), [201], `issue ${med} for ${cat}`);
  return { id: b.prescriptionId as string, number: b.prescriptionNumber as string };
}

/** A new cat of the seeded owner whose only weight is `weightDaysAgo` days old (cases that change a weight need one per run), with an open visit. */
async function freshCat(weightDaysAgo: number, kilograms = 4.0): Promise<{ catId: string; visitId: string }> {
  const serviceId = seed.serviceId as string;
  const ago = (n: number) => new Date(Date.now() - n * 864e5).toISOString().slice(0, 10);
  const cat = unwrap(
    await S.admin.post('/api/cats', { ownerId: seed.customer.id, name: `E2E Cat ${Date.now().toString(36)}`, species: 'Cat', sex: 'Female', dateOfBirth: ago(700), dateOfBirthPrecision: 'Exact', breed: 'Domestic Shorthair', colourAndMarkings: 'Tabby' }),
    [201],
    'cat',
  );
  unwrap(await S.nurse.post(`/api/cats/${cat.catId}/weights`, { kilograms, recordedOn: ago(weightDaysAgo) }), [201], 'old weight');
  const walkIn = unwrap(await S.admin.post('/api/staff/walk-ins', { customerId: seed.customer.id, catId: cat.catId, serviceId, triagePriority: 'Routine' }), [201], 'walk-in');
  const visit = unwrap(await S.nurse.post('/api/staff/visits', { appointmentId: walkIn.appointmentId }), [200, 201], 'visit');
  return { catId: cat.catId, visitId: visit.visitId };
}

/** The newest prescription on a cat whose medicine name matches (read through the API; tests do not share module state). */
async function findRx(cat: string, name: RegExp, dose?: string): Promise<{ id: string; number: string }> {
  const list = unwrap(await S.vet.get(`/api/staff/cats/${seed.cats[cat]}/prescriptions?pageSize=50`), [200], 'list prescriptions').items as any[];
  const hit = list.filter((p) => name.test(p.medicine.name) && (!dose || p.dose.amount === dose))[0];
  if (!hit) throw new Error(`no prescription for ${cat} matching ${name}: ${JSON.stringify(list).slice(0, 400)}`);
  return { id: hit.id, number: hit.prescriptionNumber };
}

const futureDate = (days: number) => new Date(Date.now() + days * 864e5).toISOString().slice(0, 10);

// ---- browser helpers --------------------------------------------------------------------------

type UiRole = Role | 'customer';

async function signInUi(page: Page, role: UiRole): Promise<void> {
  await page.context().clearCookies();
  await page.goto(role === 'customer' ? '/sign-in' : '/staff/sign-in');
  await page.waitForFunction(() => Boolean((window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl));
  const status = await page.evaluate(
    async ({ role, identifier, password }) => {
      const base = (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl ?? '';
      const r = await fetch(`${base}${role === 'customer' ? '/api/auth/sign-in' : '/api/staff/auth/sign-in'}`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ identifier, password }),
      });
      return r.status;
    },
    { role, identifier: role === 'customer' ? seed.customer.mobile : email(role), password: role === 'customer' ? seed.customer.password : PASSWORD },
  );
  expect(status).toBe(200);
}

async function openAs(page: Page, role: UiRole, url: string): Promise<void> {
  if (!(page as any).__diag) {
    (page as any).__diag = true;
    // diagnostics for the report: every refused API write the browser makes is printed with its body
    page.on('response', async (r) => {
      if (r.request().method() !== 'GET' && r.status() >= 400 && r.url().startsWith(API))
        console.log(`[api ${r.status()}] ${r.request().method()} ${new URL(r.url()).pathname} ${(await r.text().catch(() => '')).slice(0, 400)}`);
    });
  }
  await signInUi(page, role);
  await page.goto(url);
  await page.waitForLoadState('networkidle');
}

/** Records every request the browser sends to the API (method, path, headers) for the CSRF/cookie assertions. */
function watchApi(page: Page) {
  const seen: { method: string; path: string; headers: Record<string, string> }[] = [];
  page.on('request', async (req) => {
    if (!req.url().startsWith(API)) return;
    seen.push({ method: req.method(), path: new URL(req.url()).pathname, headers: await req.allHeaders() });
  });
  return seen;
}

function expectCsrfOnPosts(seen: ReturnType<typeof watchApi>, pathRegex: RegExp, minimum = 1) {
  const posts = seen.filter((s) => s.method === 'POST' && pathRegex.test(s.path));
  expect(posts.length, `POSTs matching ${pathRegex}`).toBeGreaterThanOrEqual(minimum);
  for (const p of posts) {
    expect(p.headers['x-csrf-token'], `${p.path} X-CSRF-Token`).toBeTruthy();
    expect(p.headers['cookie'] ?? '', `${p.path} cookie`).toContain('pc_staff_session=');
    expect(p.headers['cookie'] ?? '', `${p.path} cookie`).toContain('pc_csrf=');
  }
}

async function pickMedicine(page: Page, term: string, name: RegExp): Promise<void> {
  await page.getByRole('combobox', { name: /^medicine/i }).fill(term);
  await page.getByRole('option', { name }).first().click();
}
async function fillComposer(page: Page, f: { frequency: string; duration: string; quantity: string; instructions: string }) {
  await page.getByLabel(/^frequency/i).selectOption(f.frequency);
  await page.getByRole('textbox', { name: /^duration/i }).fill(f.duration);
  await page.getByRole('textbox', { name: /^quantity per supply/i }).fill(f.quantity);
  await page.getByLabel(/^instructions/i).fill(f.instructions);
}
async function enterDose(page: Page, amount: string, unit: string) {
  await page.getByRole('combobox', { name: 'Dose unit' }).selectOption(unit);
  await page.getByRole('textbox', { name: /^dose/i }).fill(amount);
}
async function tickAll(page: Page) {
  for (const box of await page.getByRole('checkbox', { name: /I have reviewed/i }).all()) await box.check();
}
async function witnessWith(page: Page, who: Role | { identifier: string; password: string }, confirm: string | RegExp) {
  const dialog = page.getByRole('dialog', { name: /witness needed/i });
  await expect(dialog).toBeVisible();
  const c = typeof who === 'string' ? { identifier: email(who), password: PASSWORD } : who;
  await dialog.getByLabel(/witness mobile number or email/i).fill(c.identifier);
  await dialog.getByLabel(/witness password/i).fill(c.password);
  await dialog.getByRole('button', { name: confirm }).click();
}
const rxUrl = (cat: string, id = '') => `/staff/cats/${seed.cats[cat]}/prescriptions${id ? `/${id}` : ''}`;

// =================================================================================================
// A. Prescribing
// =================================================================================================


test('R-A1 / E-A1: vet prescribes meloxicam above range (acknowledged) from the composer; the server records it', async ({ page }) => {
  const seen = watchApi(page);
  await openAs(page, 'vet', `${rxUrl('mitu')}/new?visitId=${seed.visits.mitu}`);
  await expect(page.getByRole('form', { name: 'Prescription' })).toBeVisible();
  await expect(page.getByText('Visit open')).toBeVisible();
  await pickMedicine(page, 'E2E Melox', /E2E Meloxicam/);
  await fillComposer(page, { frequency: 'OnceDaily', duration: '7', quantity: '10', instructions: 'Give 0.3 ml by mouth once daily.' });
  await enterDose(page, '0.3', 'Ml');
  const panel = page.getByRole('complementary', { name: 'Safety check' });
  await expect(panel.getByText('Above range', { exact: true })).toBeVisible();
  await expect(panel.getByText(/mg\/kg/).first()).toBeVisible();
  await expect(panel.getByText(/4\.10\s*kg/).first()).toBeVisible();
  await page.getByRole('button', { name: 'Issue prescription' }).click();
  await expect(page.getByText('Tick to confirm you have reviewed this warning.').first()).toBeVisible();
  await tickAll(page);
  await page.getByRole('button', { name: 'Issue prescription' }).click();
  await expect(page).toHaveURL(/\/prescriptions\/[0-9a-f-]{36}$/, { timeout: 30_000 });
  const id = page.url().split('/').pop()!;
  const number = (await page.getByRole('heading', { name: /^RX-\d{4}-\d{6}$/ }).first().innerText()).trim();
  expect(number).toMatch(/^RX-\d{4}-\d{6}$/);

  const detail = (await S.vet.get(`/api/staff/prescriptions/${id}`)).body;
  expect(detail.safetyRecord.doseCheckOutcome).toBe('AboveRange');
  expect(detail.catId).toBe(seed.cats.mitu);
  expect(detail.visitId).toBe(seed.visits.mitu);
  await expect(page.getByText(/Issued with 4\.10\s*kg/)).toBeVisible();
  // the new POSTs carried cookies and the CSRF header
  expectCsrfOnPosts(seen, /\/prescriptions\/safety-check$/);
  expectCsrfOnPosts(seen, /\/cats\/[0-9a-f-]{36}\/prescriptions$/);
});

test('R-A2 / E-A1b: amoxicillin for the penicillin-allergic cat is a blocking warning needing a reason; then a Major interaction on Mitu', async ({ page }) => {
  await openAs(page, 'vet', `${rxUrl('penny')}/new?visitId=${seed.visits.penny}`);
  await pickMedicine(page, 'E2E Amox', /E2E Amoxicillin/);
  await fillComposer(page, { frequency: 'TwiceDaily', duration: '5', quantity: '10', instructions: 'Two ml twice daily.' });
  await enterDose(page, '2', 'Ml');
  const panel = page.getByRole('complementary', { name: 'Safety check' });
  await expect(panel.getByText('Blocking')).toBeVisible();
  await expect(panel.getByRole('listitem').filter({ hasText: /Allergy.*penicillin/i })).toHaveCount(1);
  await tickAll(page); // the penicillin allergy, and (on a re-run) the duplicate-active warning too
  await page.getByRole('button', { name: 'Issue prescription' }).click();
  await expect(page.getByText('Say why you are prescribing despite the allergy.')).toBeVisible();
  await page.getByLabel(/why are you prescribing despite the allergy/i).fill('Culture-proven; no alternative.');
  await page.getByRole('button', { name: 'Issue prescription' }).click();
  await expect(page).toHaveURL(/\/prescriptions\/[0-9a-f-]{36}$/, { timeout: 30_000 });
  await expect(page.getByText('Reason given despite the allergy: “Culture-proven; no alternative.”')).toBeVisible();

  // Major interaction: Mitu has an active meloxicam; prednisolone interacts (Major, ack needed).
  await page.goto(`${rxUrl('mitu')}/new?visitId=${seed.visits.mitu}`);
  await pickMedicine(page, 'E2E Pred', /E2E Prednisolone/);
  await fillComposer(page, { frequency: 'OnceDaily', duration: '5', quantity: '10', instructions: 'One tablet daily.' });
  await enterDose(page, '1', 'Tablet');
  const p2 = page.getByRole('complementary', { name: 'Safety check' });
  await expect(p2.getByText('Major', { exact: false }).first()).toBeVisible();
  await expect(p2).toContainText(/ulceration/);
  await expect(page.getByRole('checkbox', { name: /I have reviewed/ }).first()).toBeVisible();
});

test('R-A3 / E-A3: a stale weight is recorded from the panel and the prescription is issued', async ({ page }) => {
  const stale = await freshCat(90);
  await openAs(page, 'vet', `/staff/cats/${stale.catId}/prescriptions/new?visitId=${stale.visitId}`);
  await pickMedicine(page, 'E2E Melox', /E2E Meloxicam/);
  await fillComposer(page, { frequency: 'OnceDaily', duration: '5', quantity: '10', instructions: 'Give 0.3 ml once daily.' });
  await enterDose(page, '0.3', 'Ml');
  const panel = page.getByRole('complementary', { name: 'Safety check' });
  await expect(panel.getByText(/The latest weight is out of date/)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Issue prescription' })).toBeDisabled();
  await panel.getByRole('button', { name: 'Record weight' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel(/^weight/i).fill('5.9');
  await dialog.getByRole('button', { name: 'Record weight' }).click();
  await expect(panel.getByText(/Latest weight/)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Issue prescription' })).toBeEnabled();
  await page.getByRole('button', { name: 'Issue prescription' }).click();
  await expect(page).toHaveURL(/\/prescriptions\/[0-9a-f-]{36}$/, { timeout: 30_000 });
});

test('R-A4 / E-A2: reception cannot prescribe - the page says so, and the server refuses a direct post with 403', async ({ page }) => {
  await openAs(page, 'reception', `${rxUrl('penny')}/new?visitId=${seed.visits.penny}`);
  await expect(page.getByText(/You don.t have permission to do this/)).toBeVisible();
  await expect(page.getByRole('form', { name: 'Prescription' })).toHaveCount(0);
  const check = await S.reception.post(`/api/staff/cats/${seed.cats.penny}/prescriptions/safety-check`, {
    medicineId: seed.meds.melox, dose: { amount: 0.2, unit: 'Ml' }, route: 'Oral', frequency: 'OnceDaily',
  });
  expect(check.status).toBe(403);
  const issue = await S.reception.post(`/api/staff/cats/${seed.cats.penny}/prescriptions`, issueBody(seed.visits.penny, seed.meds.melox));
  expect(issue.status).toBe(403);
  const nurse = await S.nurse.post(`/api/staff/cats/${seed.cats.penny}/prescriptions`, issueBody(seed.visits.penny, seed.meds.melox));
  expect(nurse.status).toBe(403);
});

test('R-A5: safety-check 400/409 assessment bodies from the real API, and the composer replaces its panel on a 409 (form kept)', async ({ page }) => {
  const cat = await freshCat(1, 4.0); // the weight is a day old so the one recorded below is unambiguously the newest
  const url = `/api/staff/cats/${cat.catId}/prescriptions`;
  const check = (ml: number) =>
    S.vet.post(`${url}/safety-check`, { medicineId: seed.meds.melox, dose: { amount: ml, unit: 'Ml' }, route: 'Oral', frequency: 'OnceDaily' });
  // A fresh 4 kg cat each run (a slightly heavier weight is recorded below, which keeps the dose above range but changes the figures, so the fingerprint changes; a weight that clears every warning would let the old acknowledgement through, correctly), so the above-range dose is derived from its weight.
  const probe = await check(0.2);
  const kg = Number(probe.body.weight.latest.weight.amount);
  const high = Math.round(((kg * 0.1 * 1.3) / 1.5) * 100) / 100; // ml, 30% over the 0.10 mg/kg ceiling of a 1.5 mg/ml medicine
  const chk = await check(high);
  expect(chk.status).toBe(200);
  expect(chk.body.doseCheck.outcome).toBe('AboveRange');
  const fp: string = chk.body.fingerprint;
  const noAck = await S.vet.post(url, issueBody(cat.visitId, seed.meds.melox, { dose: { amount: high, unit: 'Ml' } }));
  expect(noAck.status).toBe(400);
  expect(JSON.stringify(noAck.body.errors)).toContain('PrescriptionWarningsNotAcknowledged');
  expect(noAck.body.assessment?.fingerprint, '400 body carries the assessment').toBe(fp);
  const stale = await S.vet.post(url, issueBody(cat.visitId, seed.meds.melox, { dose: { amount: high, unit: 'Ml' }, acknowledgedWarningsFingerprint: 'deadbeef' }));
  expect(stale.status).toBe(409);
  expect(JSON.stringify(stale.body.errors)).toContain('PrescriptionWarningsChanged');
  expect(stale.body.assessment?.fingerprint, '409 body carries the assessment').toBe(fp);

  // UI: the panel shows above range; the cat's weight changes under the vet; the submit carries the old fingerprint -> real 409.
  await openAs(page, 'vet', `/staff/cats/${cat.catId}/prescriptions/new?visitId=${cat.visitId}`);
  await pickMedicine(page, 'E2E Melox', /E2E Meloxicam/);
  await fillComposer(page, { frequency: 'OnceDaily', duration: '5', quantity: '10', instructions: 'Give a little once daily.' });
  await enterDose(page, String(high), 'Ml');
  const panel = page.getByRole('complementary', { name: 'Safety check' });
  await expect(panel.getByText('Above range', { exact: true })).toBeVisible();
  await tickAll(page);
  // The composer re-reads the check by itself after other changes; to make the SUBMIT carry the old fingerprint (so the
  // real server answers 409), later safety-check calls are answered with the check the vet already saw. The issue POST
  // itself goes to the real API untouched.
  await page.route('**/safety-check', (route) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(chk.body) }));
  unwrap(await S.nurse.post(`/api/cats/${cat.catId}/weights`, { kilograms: Math.round(kg * 1.1 * 10) / 10, recordedOn: futureDate(0) }), [201], 'weight');
  const issued = page.waitForResponse((r) => r.request().method() === 'POST' && r.url().endsWith('/prescriptions'));
  await page.getByRole('button', { name: 'Issue prescription' }).click();
  expect((await issued).status()).toBe(409);
  await page.unroute('**/safety-check');
  await expect(page.getByText('Something changed — please review the warnings again.')).toBeVisible();
  await expect(panel.getByText(/4\.40\s*kg/).first()).toBeVisible(); // the panel is the server's new assessment, at the new weight
  await expect(page.getByRole('textbox', { name: /^dose/i })).toHaveValue(new RegExp(String(high).replace('.', '\.')));
  await expect(page.getByLabel(/^instructions/i)).toHaveValue('Give a little once daily.');
  // the replaced panel is the server's new assessment; whatever it still asks for is acknowledged, then the vet can issue
  await tickAll(page);
  await page.getByRole('button', { name: 'Issue prescription' }).click();
  await expect(page).toHaveURL(/\/prescriptions\/[0-9a-f-]{36}$/, { timeout: 30_000 });
});

test('R-A2o (expected to fail while backlog PHM 50 is open): warnings are ordered allergy first, then interactions, then dose', async ({ page }) => {
  test.fail(true, 'The real API returns [DoseNotCheckable, AllergyConflict]; the handover (SafetyPanel, U-A5) requires the allergy first. Backlog PHM 50.');
  await openAs(page, 'vet', `${rxUrl('penny')}/new?visitId=${seed.visits.penny}`);
  await pickMedicine(page, 'E2E Amox', /E2E Amoxicillin/);
  await fillComposer(page, { frequency: 'TwiceDaily', duration: '5', quantity: '10', instructions: 'Two ml twice daily.' });
  await enterDose(page, '2', 'Ml');
  const panel = page.getByRole('complementary', { name: 'Safety check' });
  await expect(panel.getByText('Blocking')).toBeVisible();
  await expect(panel.getByRole('listitem').first()).toContainText(/Allergy.*penicillin/i);
});

test('R-A6: renewal finds the medicine in the real formulary (exact name + strength) and issues remotely', async ({ page }) => {
  const mituMeloxRx = await findRx('mitu', /Meloxicam/, '0.30');
  const seen = watchApi(page);
  await openAs(page, 'vet', `${rxUrl('mitu', mituMeloxRx.id)}/renew`);
  await expect(page.getByText('Remote renewal')).toBeVisible();
  await expect(page.getByText(/could not match/)).toHaveCount(0);
  await expect(page.getByRole('textbox', { name: /^dose/i })).toHaveValue(/0\.30/);
  await expect(page.getByRole('combobox', { name: 'Dose unit' })).toHaveValue('Ml');
  await page.getByLabel(/dispense by/i).fill(futureDate(20)); // the server allows at most 30 days after the start
  const panel = page.getByRole('complementary', { name: 'Safety check' });
  await expect(panel.getByText('Above range', { exact: true })).toBeVisible();
  await tickAll(page);
  const renewResponse = () => page.waitForResponse((r) => r.request().method() === 'POST' && /\/renew$/.test(r.url()));
  let posted = renewResponse();
  await page.getByRole('button', { name: 'Issue renewal' }).click();
  const first = (await posted).status();
  test.info().annotations.push({ type: 'first renew submit status', description: String(first) });
  if (first === 409) {
    // see R-A6o: the live check and the renew endpoint disagree about the prescription being renewed
    await expect(page.getByText('Something changed — please review the warnings again.')).toBeVisible();
    await tickAll(page);
    posted = renewResponse();
    await page.getByRole('button', { name: 'Issue renewal' }).click();
    expect((await posted).status()).toBe(201);
  }
  await expect(page).toHaveURL(/\/prescriptions\/[0-9a-f-]{36}$/, { timeout: 30_000 });
  expect(page.url()).not.toContain(mituMeloxRx.id);
  const renewed = (await S.vet.get(`/api/staff/prescriptions/${page.url().split('/').pop()}`)).body;
  expect(renewed.isRemoteRenewal).toBe(true);
  expectCsrfOnPosts(seen, /\/prescriptions\/[0-9a-f-]{36}\/renew$/);
});

test('R-A6o (expected to fail while backlog PHM 51 is open): the first submit of a renewal is not refused with 409 PrescriptionWarningsChanged', async ({ page }) => {
  test.fail(true, 'The live safety check lists "already has an active prescription" for the prescription being renewed; the renew endpoint leaves it out, so the fingerprints differ. Backlog PHM 51.');
  const rx = await issueRx('mitu', 'melox', { dose: { amount: 0.2, unit: 'Ml' } });
  await openAs(page, 'vet', `${rxUrl('mitu', rx.id)}/renew`);
  await expect(page.getByText('Remote renewal')).toBeVisible();
  const panel = page.getByRole('complementary', { name: 'Safety check' });
  await expect(panel.getByText('Within range', { exact: false }).first()).toBeVisible();
  await tickAll(page);
  const posted = page.waitForResponse((r) => r.request().method() === 'POST' && /\/renew$/.test(r.url()));
  await page.getByRole('button', { name: 'Issue renewal' }).click();
  expect((await posted).status()).toBe(201);
});

// =================================================================================================
// B. Dispensing, labels, controlled drugs, reversal
// =================================================================================================

/** Reception-side API dispense of a prescription following the server's own suggested lines. */
async function dispenseViaApi(who: Role, rxId: string, extra: Record<string, unknown> = {}) {
  const plan = unwrap(await S[who].get(`/api/staff/prescriptions/${rxId}/dispense-plan`), [200], 'plan').suggestedLines as any[];
  const lines = plan.map((l) => ({ skuId: l.skuId, batchId: l.batchId, quantity: Number(l.quantity.amount) }));
  return S[who].post(`/api/staff/prescriptions/${rxId}/dispensings`, { lines, fefoOverrideReason: null, repeatRequestId: null, witnessIdentifier: null, witnessPassword: null, ...extra });
}
/** On-hand of one batch at one location, read from the stock matrix of HO-IMP-001 (the figure is derived from the ledger). */
const skuOfBatch = (batchId: string): string => {
  const key = Object.keys(seed.batches).find((k) => seed.batches[k] === batchId)!;
  return seed.skus[key.startsWith('melox') ? 'melox' : key];
};
const onHand = async (batchId: string, locationId: string): Promise<number> => {
  const matrix = unwrap(await S.admin.get(`/api/staff/inventory/stock/${skuOfBatch(batchId)}`), [200], 'stock matrix');
  const batch = (matrix.batches as any[]).find((b) => b.batchId === batchId);
  return Number(batch?.locations.find((l: any) => l.locationId === locationId)?.onHand ?? 0);
};
const WITNESS_FAIL = 'The witness could not be verified. Ask them to enter their own sign-in details.';

test('R-B1 / E-B1: only expired stock - no dispense control, the server’s message; a direct post is refused', async ({ page }) => {
  const rx = await issueRx('penny', 'expd', { dose: { amount: 2, unit: 'Ml' } });
  const plan = (await S.reception.get(`/api/staff/prescriptions/${rx.id}/dispense-plan`)).body;
  expect(plan.onlyExpiredStockAvailable).toBe(true);
  expect(plan.canDispense).toBe(false);
  await openAs(page, 'reception', '/staff/pharmacy');
  await page.getByRole('link', { name: rx.number }).click();
  const notice = page.getByRole('alert', { name: 'This cannot be dispensed' });
  await expect(notice).toContainText(/expired/i);
  await expect(notice).toContainText('There is no way to override this here.');
  await expect(page.getByRole('button', { name: /dispense/i })).toHaveCount(0);
  await expect(page.getByRole('checkbox')).toHaveCount(0);
  const direct = await S.reception.post(`/api/staff/prescriptions/${rx.id}/dispensings`, {
    lines: [{ skuId: seed.skus.expd, batchId: seed.batches.expd, quantity: 10 }], fefoOverrideReason: 'try', repeatRequestId: null, witnessIdentifier: null, witnessPassword: null,
  });
  expect(direct.status).toBeGreaterThanOrEqual(400);
  expect(direct.status).toBeLessThan(500);
  expect(await onHand(seed.batches.expd, seed.locs.dispensary)).toBe(25);
});

test('R-B2 / E-A1c + E-B3: reception dispenses the FEFO batch, prints the label twice; the stock reduces; the exact batch shows on the dispensing', async ({ page }) => {
  const rx = await issueRx('mitu', 'melox', { dose: { amount: 0.2, unit: 'Ml' } });
  const before = await onHand(seed.batches.meloxNear, seed.locs.dispensary);
  const seen = watchApi(page);
  await openAs(page, 'reception', '/staff/pharmacy');
  await page.getByRole('link', { name: rx.number }).click();
  await expect(page.getByRole('heading', { name: 'Dispense' })).toBeVisible();
  await expect(page.getByRole('checkbox', { name: 'Use lot E2E-MELOX-NEAR' })).toBeChecked();
  await expect(page.getByRole('checkbox', { name: 'Use lot E2E-MELOX-FAR' })).not.toBeChecked();
  await expect(page.getByRole('checkbox', { name: 'Use lot E2E-MELOX-OLD' })).toBeDisabled();
  await expect(page.getByText(/1 Oct 2026/).first()).toBeVisible();
  await expect(page.getByText(/10\.00 of 10\.00\s*ml selected/)).toBeVisible();
  await page.getByRole('button', { name: 'Dispense' }).click();
  await expect(page.getByText('Dispensed — supply 1 of 1.')).toBeVisible();
  expect(await onHand(seed.batches.meloxNear, seed.locs.dispensary)).toBe(before - 10);

  await page.getByRole('link', { name: 'Print label' }).click();
  await expect(page.getByRole('article', { name: /Medicine label for E2E Mitu/ })).toBeVisible();
  await page.evaluate(() => {
    (window as unknown as { __printCount: number }).__printCount = 0;
    window.print = () => {
      (window as unknown as { __printCount: number }).__printCount += 1;
    };
  });
  await page.getByRole('button', { name: 'Print' }).click();
  await expect(page.getByText('Printed 1 time so far.')).toBeVisible();
  await page.getByRole('button', { name: 'Print' }).click();
  await expect(page.getByText('Printed 2 times so far.')).toBeVisible();
  expect(await page.evaluate(() => (window as unknown as { __printCount: number }).__printCount)).toBe(2);
  await page.getByRole('link', { name: 'Back' }).click();
  await expect(page.getByText('2 times')).toBeVisible();
  await expect(page.getByText(/E2E-MELOX-NEAR/).first()).toBeVisible();

  const detail = (await S.vet.get(`/api/staff/prescriptions/${rx.id}`)).body;
  const dispensingId = detail.dispensings[0].id;
  const d = (await S.reception.get(`/api/staff/dispensings/${dispensingId}`)).body;
  expect(JSON.stringify(d)).toContain('E2E-MELOX-NEAR');
  expect(JSON.stringify(d)).toMatch(/"(label)?[pP]rintCount":2/);
  expectCsrfOnPosts(seen, /\/prescriptions\/[0-9a-f-]{36}\/dispensings$/);
  expectCsrfOnPosts(seen, /\/dispensings\/[0-9a-f-]{36}\/label-prints$/, 2);
});

test('R-B3: choosing a later-expiring batch needs a reason; the override is recorded', async ({ page }) => {
  const rx = await issueRx('mitu', 'melox', { dose: { amount: 0.2, unit: 'Ml' } });
  await openAs(page, 'reception', '/staff/pharmacy');
  await page.getByRole('link', { name: rx.number }).click();
  await page.getByRole('checkbox', { name: 'Use lot E2E-MELOX-NEAR' }).uncheck();
  await page.getByRole('checkbox', { name: 'Use lot E2E-MELOX-FAR' }).check();
  const reason = page.getByLabel(/reason/i).first();
  await expect(reason).toBeVisible();
  // the screen does not let a later-expiring batch be dispensed until the reason is given: the button is disabled and says why
  await expect(page.getByRole('button', { name: 'Dispense' })).toBeDisabled();
  await expect(page.locator('#ds-blocked')).not.toBeEmpty();
  await reason.fill('Near-expiry lot is quarantined for inspection.');
  await page.getByRole('button', { name: 'Dispense' }).click();
  await expect(page.getByText('Dispensed — supply 1 of 1.')).toBeVisible();
  const detail = (await S.vet.get(`/api/staff/prescriptions/${rx.id}`)).body;
  const d = (await S.reception.get(`/api/staff/dispensings/${detail.dispensings[0].id}`)).body;
  expect(JSON.stringify(d)).toContain('E2E-MELOX-FAR');
  expect(JSON.stringify(d)).toContain('Near-expiry lot is quarantined');
});

test('R-B4 / E-B2: a controlled drug needs a vet or nurse witness who is not the dispenser; wrong witnesses are refused with one generic message', async ({ page }) => {
  const rx = await issueRx('mitu', 'bupre', { dose: { amount: 0.5, unit: 'Ml' }, quantity: 2 });
  const seen = watchApi(page);
  await openAs(page, 'reception', '/staff/pharmacy');
  await page.getByRole('link', { name: rx.number }).click();
  await page.getByRole('button', { name: /Dispense \(witness needed\)/ }).click();
  const dialog = page.getByRole('dialog', { name: /witness needed/i });
  await expect(dialog).toContainText(/vet or nurse/i);
  // the dispenser cannot witness themselves
  await witnessWith(page, 'reception', 'Confirm and dispense');
  await expect(dialog.getByText(WITNESS_FAIL)).toBeVisible();
  await expect(dialog.getByLabel(/witness password/i)).toHaveValue('');
  // an administrator is neither a vet nor a nurse
  await witnessWith(page, 'admin', 'Confirm and dispense');
  await expect(dialog.getByText(WITNESS_FAIL)).toBeVisible();
  // a vet with the wrong password
  await witnessWith(page, { identifier: email('vet2'), password: PASSWORD + 'x' }, 'Confirm and dispense');
  await expect(dialog.getByText(WITNESS_FAIL)).toBeVisible();
  expect((await S.vet.get(`/api/staff/prescriptions/${rx.id}`)).body.dispensings.length).toBe(0);
  // a vet witness (not the dispenser) is accepted
  await witnessWith(page, 'vet2', 'Confirm and dispense');
  await expect(page.getByText('Dispensed — supply 1 of 1.')).toBeVisible();
  // nothing of the witness credentials was kept
  await expect(page.getByLabel(/witness password/i)).toHaveCount(0);
  expect(await page.evaluate(() => JSON.stringify({ ...localStorage }) + JSON.stringify({ ...sessionStorage }))).not.toContain(PASSWORD);
  expect(page.url()).not.toContain(PASSWORD);
  expect(seen.filter((x) => x.method === 'POST' && /\/dispensings$/.test(x.path)).length).toBe(4);
  expectCsrfOnPosts(seen, /\/dispensings$/, 4);

  const detail = (await S.vet.get(`/api/staff/prescriptions/${rx.id}`)).body;
  expect(detail.dispensings.length).toBe(1);
  // the register has the entry (a vet reads it)
  const reg = (await S.vet.get('/api/staff/controlled-drugs/register?pageSize=50')).body;
  expect(JSON.stringify(reg), 'a register entry for the dispensing').toContain(rx.number);

  // a nurse dispensing with themselves as the witness is refused by the server, not just the UI
  const rx2 = await issueRx('mitu', 'bupre', { dose: { amount: 0.5, unit: 'Ml' }, quantity: 2 });
  const self = await dispenseViaApi('nurse', rx2.id, { witnessIdentifier: email('nurse'), witnessPassword: PASSWORD });
  expect(self.status).toBe(400);
  expect(JSON.stringify(self.body)).toContain('WitnessNotVerified');
  const none = await dispenseViaApi('nurse', rx2.id);
  expect(none.status).toBe(400);
});

test('R-B5 / E-C1: register entries are read-only; a vet adds a correction (witness: another vet) as a new linked entry; the original is unchanged', async ({ page }) => {
  const reg = (await S.vet.get('/api/staff/controlled-drugs/register?pageSize=50')).body;
  const original = (reg.items as any[]).find((e) => /dispens/i.test(JSON.stringify(e.kind ?? e.entryKind ?? e)));
  expect(original, `a dispensing entry exists (R-B4 first); register: ${JSON.stringify(reg).slice(0, 400)}`).toBeTruthy();
  const before = (await S.vet.get(`/api/staff/controlled-drugs/register/${original.id}`)).body;
  const seen = watchApi(page);

  // an administrator cannot correct the register (vet only): no control in the UI, 403 from the server
  await openAs(page, 'admin', `/staff/pharmacy/controlled-drugs/${original.id}`);
  await expect(page.getByRole('heading', { name: /Register entry/ })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Add correction' })).toHaveCount(0);
  const denied = await S.admin.post(`/api/staff/controlled-drugs/register/${original.id}/corrections`, {
    correctedDoseText: 'x', correctedCatId: null, quantityDelta: 0, notes: null, reason: 'x', witnessIdentifier: email('vet2'), witnessPassword: PASSWORD,
  });
  expect(denied.status).toBe(403);

  await openAs(page, 'vet', `/staff/pharmacy/controlled-drugs/${original.id}`);
  await expect(page.getByRole('heading', { name: new RegExp(`Register entry #${original.entryNumber}`) })).toBeVisible();
  await expect(page.getByRole('button', { name: /edit|delete/i })).toHaveCount(0);
  await page.getByRole('link', { name: 'Add correction' }).click();
  await page.getByLabel(/corrected dose text/i).fill('0.5 ml (corrected: given slowly)');
  await page.getByLabel(/reason for the correction/i).fill('Dose text was incomplete.');
  await page.getByRole('button', { name: 'Review and add correction' }).click();
  await expect(page.getByRole('dialog')).toContainText(new RegExp(`This adds entry #\\d+ correcting #${original.entryNumber}; the original stays as it is\\.`));
  await page.getByRole('button', { name: 'Continue to witness' }).click();
  await witnessWith(page, 'vet2', 'Confirm and add correction');
  await expect(page).toHaveURL(/controlled-drugs\/[0-9a-f-]{36}$/, { timeout: 30_000 });
  await expect(page.getByRole('region', { name: /Corrections \(1\)/ })).toBeVisible();
  await expect(page.getByText('Dose text was incomplete.')).toBeVisible();
  expectCsrfOnPosts(seen, /\/corrections$/);

  const after = (await S.vet.get(`/api/staff/controlled-drugs/register/${original.id}`)).body;
  const strip = (e: any) => JSON.stringify({ ...e.entry, correctedByEntryNumbers: undefined });
  expect(strip(after), 'the original entry is unchanged apart from its corrected-by link').toBe(strip(before));
  expect(after.entry.correctedByEntryNumbers.length).toBe(1);
  expect(before.entry.correctedByEntryNumbers.length).toBe(0);
  const ver = await S.vet.get('/api/staff/controlled-drugs/verification');
  expect(ver.status).toBe(200);
  test.info().annotations.push({ type: 'register-verification', description: JSON.stringify(ver.body).slice(0, 300) });
});

test('R-B6 / E-B4: an administrator reverses a dispensing (stock returns to quarantine); a controlled one needs a witness; reception cannot reverse', async ({ page }) => {
  // non-controlled
  const rx = await issueRx('mitu', 'melox', { dose: { amount: 0.2, unit: 'Ml' } });
  const disp = await dispenseViaApi('reception', rx.id);
  expect(disp.status).toBe(201);
  const dispensingId = (await S.vet.get(`/api/staff/prescriptions/${rx.id}`)).body.dispensings[0].id;
  const quarBefore = await onHand(seed.batches.meloxNear, seed.locs.quarantine);
  const reception = await S.reception.post(`/api/staff/dispensings/${dispensingId}/reverse`, { reason: 'nope', witnessIdentifier: null, witnessPassword: null });
  expect(reception.status).toBe(403);
  const seen = watchApi(page);
  await openAs(page, 'reception', `/staff/pharmacy/dispensings/${dispensingId}`);
  await expect(page.getByRole('link', { name: /Reprint label/ })).toBeVisible();
  await expect(page.getByRole('button', { name: /Reverse this supply/ })).toHaveCount(0);
  await openAs(page, 'admin', `/staff/pharmacy/dispensings/${dispensingId}`);
  await page.getByRole('button', { name: /Reverse this supply/ }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toContainText(/quarantine/i);
  await dialog.getByLabel(/reason for the reversal/i).fill('Wrong cat.');
  const reversed = page.waitForResponse((r) => r.request().method() === 'POST' && /\/reverse$/.test(r.url()));
  await dialog.getByRole('button', { name: /Reverse|Confirm/ }).last().click();
  expect((await reversed).status()).toBeLessThan(300);
  await expect(page.getByText(/Reversed/i).first()).toBeVisible();
  expect((await onHand(seed.batches.meloxNear, seed.locs.quarantine)) - quarBefore).toBe(10);
  expectCsrfOnPosts(seen, /\/reverse$/);

  // controlled: the witness step
  const rxC = await issueRx('mitu', 'bupre', { dose: { amount: 0.5, unit: 'Ml' }, quantity: 2 });
  expect((await dispenseViaApi('reception', rxC.id, { witnessIdentifier: email('vet2'), witnessPassword: PASSWORD })).status).toBe(201);
  const dispC = (await S.vet.get(`/api/staff/prescriptions/${rxC.id}`)).body.dispensings[0].id;
  const noWitness = await S.admin.post(`/api/staff/dispensings/${dispC}/reverse`, { reason: 'Wrong cat.', witnessIdentifier: null, witnessPassword: null });
  expect(noWitness.status).toBe(400);
  await openAs(page, 'admin', `/staff/pharmacy/dispensings/${dispC}`);
  await page.getByRole('button', { name: /Reverse this supply/ }).click();
  const d2 = page.getByRole('dialog');
  await d2.getByLabel(/reason for the reversal/i).fill('Wrong cat.');
  await d2.getByRole('button', { name: 'Continue to witness' }).click();
  await witnessWith(page, 'nurse', 'Confirm and reverse');
  await expect(page.getByText(/Reversed/i).first()).toBeVisible();
  const reg = (await S.vet.get('/api/staff/controlled-drugs/register?pageSize=50')).body;
  expect(JSON.stringify(reg)).toContain(rxC.number);
});

// =================================================================================================
// D. Portal: prescriptions, repeat requests, authorised foods
// =================================================================================================

test('R-D1 / E-D1: the owner sees active prescriptions, requests a repeat; staff dispense it from Repeat requests', async ({ page }) => {
  const rx = await issueRx('mitu', 'melox', { dose: { amount: 0.2, unit: 'Ml' }, isRepeatable: true, repeatsAuthorised: 2, validUntil: futureDate(60) });
  expect((await dispenseViaApi('reception', rx.id)).status).toBe(201);
  // the portal API: only the owner's, no batch or lot anywhere
  const list = await S.customer.get('/api/customers/me/prescriptions');
  expect(list.status).toBe(200);
  expect(JSON.stringify(list.body)).not.toMatch(/E2E-MELOX|lotNumber|batch/i);
  expect((await S.reception.get('/api/customers/me/prescriptions')).status).toBeGreaterThanOrEqual(401);

  const note = `After 5 pm please ${Date.now().toString(36)}`;
  const seen = watchApi(page);
  await openAs(page, 'customer', '/portal/prescriptions');
  const row = page.getByRole('listitem').filter({ hasText: '0.20' }).filter({ hasText: 'E2E Meloxicam' }).first();
  await expect(row).toContainText(/once daily/);
  await expect(row).toContainText(/Prescribed by: E2E vet/);
  await expect(row).toContainText(/days left/);
  await expect(page.locator('body')).not.toContainText(/E2E-MELOX|lot /i);
  await page.goto(`/portal/prescriptions/${rx.id}`);
  await page.getByRole('button', { name: /Request a repeat/ }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel(/collect from/i).selectOption(MAIN);
  await dialog.getByLabel(/note for the pharmacy/i).fill(note);
  await dialog.getByRole('button', { name: 'Request repeat' }).click();
  await expect(page.getByText('Requested. We’ll message you when it’s ready to collect.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Withdraw request' })).toBeVisible();
  const reqs = seen.filter((x) => x.method === 'POST' && /repeat-requests$/.test(x.path));
  expect(reqs.length).toBe(1);
  expect(reqs[0]!.headers['x-csrf-token']).toBeTruthy();
  expect(reqs[0]!.headers['cookie']).toContain('pc_session=');

  await openAs(page, 'reception', '/staff/pharmacy');
  const repeats = page.getByRole('region', { name: 'Repeat requests' });
  await expect(repeats.getByText(note)).toBeVisible();
  await repeats.getByRole('link', { name: rx.number }).click();
  await expect(page.getByText('Repeat request from the owner')).toBeVisible();
  await page.getByRole('button', { name: 'Dispense' }).click();
  await expect(page.getByText(/Dispensed — supply 2 of 3\./)).toBeVisible();
  // the owner is told it is ready (inbox) - needs the notification worker; recorded, not failed, when it has not run
  const inbox = await S.customer.get('/api/customers/me/inbox');
  test.info().annotations.push({ type: 'inbox', description: `${inbox.status} ${JSON.stringify(inbox.body).slice(0, 300)}` });
});

test('R-D2 / E-D2: a prescription that cannot be repeated says why and offers a consultation', async ({ page }) => {
  // one repeat authorised: dispense the first supply, request and fill the one repeat, then ask again
  const rx = await issueRx('mitu', 'melox', { dose: { amount: 0.2, unit: 'Ml' }, isRepeatable: true, repeatsAuthorised: 1, validUntil: futureDate(60) });
  expect((await dispenseViaApi('reception', rx.id)).status).toBe(201);
  const req = await S.customer.post(`/api/customers/me/prescriptions/${rx.id}/repeat-requests`, { preferredBranchId: MAIN, note: null });
  expect(req.status).toBe(201);
  const requestId = req.body.repeatRequestId ?? req.body.requestId ?? req.body.id;
  expect((await dispenseViaApi('reception', rx.id, { repeatRequestId: requestId })).status).toBe(201);
  const again = await S.customer.get(`/api/customers/me/prescriptions/${rx.id}`);
  expect(again.body.canRequest ?? again.body.repeat?.canRequest).toBe(false);
  await openAs(page, 'customer', `/portal/prescriptions/${rx.id}?action=repeat`);
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Book a consultation' })).toHaveAttribute('href', `/book?catId=${seed.cats.mitu}`);
});

test('R-D3 / E-E1: a vet authorises a diet from a visit and the owner sees it under Authorised foods', async ({ page }) => {
  const seen = watchApi(page);
  await openAs(page, 'vet', `/staff/cats/${seed.cats.mitu}/diet-authorisations/new?visitId=${seed.visits.mitu}`);
  await expect(page.getByText('Visit open')).toBeVisible();
  await page.getByRole('combobox', { name: /^food/i }).fill('E2E Urinary');
  await page.getByRole('option', { name: /E2E Urinary Support/ }).click();
  await page.getByRole('button', { name: '90 days' }).click();
  await page.getByRole('button', { name: 'Authorise diet' }).click();
  await expect(page).toHaveURL(/diet-authorisations$/, { timeout: 30_000 });
  await expect(page.getByRole('row', { name: /E2E Urinary Support/ }).first()).toContainText('Active');
  expectCsrfOnPosts(seen, /diet-authorisations$/);

  await openAs(page, 'customer', '/portal/prescriptions?tab=diets');
  const foods = page.getByRole('list', { name: 'Authorised foods' });
  await expect(foods.getByRole('listitem').filter({ hasText: 'E2E Urinary Support' }).first()).toContainText('Active');
});

// =================================================================================================
// F. Formulary
// =================================================================================================

test('R-F1 / E-F1: an administrator creates a per-kg medicine with an interaction, links a SKU and marks it controlled; it is prescribable', async ({ page }) => {
  const code = `E2E-TRAM-${Date.now().toString(36).toUpperCase()}`.slice(0, 30);
  const seen = watchApi(page);
  await openAs(page, 'admin', '/staff/admin/formulary/new');
  await page.getByLabel(/^code/i).fill(code);
  await page.getByRole('textbox', { name: /^name/i }).fill('E2E Tramadol tablets');
  await page.getByLabel(/^active ingredients/i).fill('tramadol');
  await page.getByLabel(/^active ingredients/i).press('Enter');
  await page.getByRole('combobox', { name: 'Form', exact: true }).selectOption('Tablet');
  await page.getByLabel(/dispensing unit/i).selectOption('Tablet');
  await page.getByRole('textbox', { name: /^strength/i }).fill('50');
  await page.getByRole('combobox', { name: 'Strength unit' }).selectOption('Mg');
  await page.getByRole('combobox', { name: 'Per unit' }).selectOption('Tablet');
  await expect(page.getByText(/Strength preview/)).toContainText('50 mg per 1 tablet');
  await page.getByRole('checkbox', { name: 'Oral' }).check();
  await page.getByLabel(/default route/i).selectOption('Oral');
  await page.getByRole('textbox', { name: /minimum dose/i }).fill('1');
  await page.getByRole('textbox', { name: /maximum dose/i }).fill('4');
  await page.getByRole('combobox', { name: /link a clinic sku/i }).fill('E2E Tramadol');
  await page.getByRole('option', { name: /E2E Tramadol stock/ }).click();
  await page.getByRole('button', { name: /Add interaction/ }).click();
  await page.getByRole('combobox', { name: /other medicine/i }).fill('E2E Melox');
  await page.getByRole('option', { name: /E2E Meloxicam/ }).click();
  await page.getByLabel('Severity').selectOption('Major');
  await page.getByRole('textbox', { name: 'Note', exact: true }).fill('Raises seizure risk with some NSAIDs.');
  await page.getByRole('button', { name: 'Create medicine' }).click();
  await expect(page).toHaveURL(/\/staff\/admin\/formulary\/[0-9a-f-]{36}$/, { timeout: 30_000 });
  const medicineId = page.url().split('/').pop()!;

  await page.getByRole('button', { name: /Mark as controlled/ }).click();
  const confirm = page.getByRole('dialog');
  await expect(confirm.getByRole('button', { name: 'Mark as controlled' })).toBeDisabled();
  await confirm.getByLabel(/schedule/i).fill('CD Schedule 3');
  await confirm.getByRole('button', { name: 'Mark as controlled' }).click();
  await expect(page.getByText('Controlled — CD Schedule 3')).toBeVisible();

  await page.goto('/staff/admin/formulary');
  const row = page.getByRole('row', { name: /E2E Tramadol tablets/ });
  await expect(row).toContainText('Controlled');
  await expect(row).toContainText('Stocked');
  const detail = (await S.admin.get(`/api/staff/formulary/${medicineId}`)).body;
  expect(detail.isControlled).toBe(true);
  expect(detail.interactions.length).toBe(1);
  expectCsrfOnPosts(seen, /\/api\/admin\/formulary$/);

  await openAs(page, 'vet', `${rxUrl('mitu')}/new?visitId=${seed.visits.mitu}`);
  await page.getByRole('combobox', { name: /^medicine/i }).fill('E2E Tramad');
  await expect(page.getByRole('option', { name: /E2E Tramadol tablets/ })).toContainText('Controlled');
});

test('R-F2: saving a formulary edit after someone else changed the medicine shows the 409 reload message and keeps the typed values', async ({ page }) => {
  await openAs(page, 'admin', `/staff/admin/formulary/${seed.meds.pred}`);
  const contra = page.getByLabel(/contraindications/i);
  await expect(contra).toBeVisible();
  // another administrator saves first (through the API, with the version the page loaded)
  const current = (await S.admin.get(`/api/staff/formulary/${seed.meds.pred}`)).body;
  const put = await S.admin.put(`/api/admin/formulary/${seed.meds.pred}`, {
    code: current.code, name: current.name, activeIngredients: current.activeIngredients, drugClasses: current.drugClasses, form: current.form, strength: { amount: Number(current.strength.amount.amount), unit: current.strength.amount.unit, perQuantity: Number(current.strength.per.amount), perUnit: current.strength.per.unit },
    defaultRoute: current.defaultRoute, allowedRoutes: current.allowedRoutes, dosingBasis: current.dosingBasis, doseRange: current.doseRange,
    contraindications: 'Changed elsewhere.',
    interactions: current.interactions.map((i: any) => ({ interactsWithMedicineId: i.interactsWithMedicineId, interactsWithTerm: i.interactsWithTerm, severity: i.severity, note: i.note })), dispensingUnit: current.dispensingUnit, linkedSkuIds: [seed.skus.pred], expectedVersion: current.version,
  });
  test.info().annotations.push({ type: 'concurrent-edit', description: `${put.status} ${JSON.stringify(put.body).slice(0, 200)}` });
  expect(put.status).toBeLessThan(300);
  await contra.fill('My typed contraindication text.');
  await page.getByRole('button', { name: /save changes|save/i }).first().click();
  await expect(page.getByText(/changed|reload|out of date/i).first()).toBeVisible();
  await expect(contra).toHaveValue('My typed contraindication text.');
});

// =================================================================================================
// X. Cookies and CSRF on the new POSTs (API level)
// =================================================================================================

test('R-X1: session cookies are HttpOnly + SameSite, the CSRF cookie is readable; every new POST refuses a missing or wrong X-CSRF-Token', async () => {
  const ctx = await pwRequest.newContext({ baseURL: API, extraHTTPHeaders: { Origin: WEB_ORIGIN, 'X-Branch-Id': MAIN } });
  const signIn = await ctx.post('/api/staff/auth/sign-in', { data: { identifier: email('vet'), password: PASSWORD } });
  expect(signIn.status()).toBe(200);
  const setCookies = signIn.headersArray().filter((h) => h.name.toLowerCase() === 'set-cookie').map((h) => h.value);
  const session = setCookies.find((c) => c.startsWith('pc_staff_session='))!;
  const csrf = setCookies.find((c) => c.startsWith('pc_csrf='))!;
  expect(session).toMatch(/httponly/i);
  expect(session).toMatch(/samesite=lax/i);
  expect(csrf).not.toMatch(/httponly/i);
  expect(csrf).toMatch(/samesite=lax/i);
  const token = decodeURIComponent(csrf.split(';')[0]!.split('=')[1]!);
  const rx = await findRx('mitu', /Meloxicam/);
  const targets: [string, string, unknown][] = [
    ['POST', `/api/staff/cats/${seed.cats.mitu}/prescriptions/safety-check`, { medicineId: seed.meds.melox, dose: { amount: 0.2, unit: 'Ml' }, route: 'Oral', frequency: 'OnceDaily' }],
    ['POST', `/api/staff/cats/${seed.cats.mitu}/prescriptions`, issueBody(seed.visits.mitu, seed.meds.melox)],
    ['POST', `/api/staff/prescriptions/${rx.id}/cancel`, { reason: 'x' }],
    ['POST', `/api/staff/prescriptions/${rx.id}/renew`, {}],
    ['POST', `/api/staff/prescriptions/${rx.id}/dispensings`, { lines: [], witnessIdentifier: null, witnessPassword: null }],
    ['POST', `/api/staff/dispensings/${rx.id}/label-prints`, {}],
    ['POST', `/api/staff/dispensings/${rx.id}/reverse`, { reason: 'x' }],
    ['POST', `/api/staff/controlled-drugs/register/${rx.id}/corrections`, { reason: 'x', witnessIdentifier: 'a', witnessPassword: 'b', quantityDelta: 0 }],
    ['PUT', `/api/admin/formulary/${seed.meds.pred}/active`, { isActive: true }],
  ];
  for (const [method, url, data] of targets) {
    const none = await ctx.fetch(url, { method, data });
    const wrong = await ctx.fetch(url, { method, data, headers: { 'X-CSRF-Token': 'not-the-token' } });
    expect(none.status(), `${method} ${url} without token`).toBe(403);
    expect(await none.text()).toContain('csrf_token_invalid');
    expect(wrong.status(), `${method} ${url} wrong token`).toBe(403);
    expect(await wrong.text()).toContain('csrf_token_invalid');
    const right = await ctx.fetch(url, { method, data, headers: { 'X-CSRF-Token': token } });
    expect(await right.text(), `${method} ${url} with the right token is past the CSRF check`).not.toContain('csrf_token_invalid');
  }
  // a customer-side POST too
  const cust = await pwRequest.newContext({ baseURL: API, extraHTTPHeaders: { Origin: WEB_ORIGIN } });
  const cs = await cust.post('/api/auth/sign-in', { data: { identifier: seed.customer.mobile, password: seed.customer.password } });
  expect(cs.status()).toBe(200);
  const noToken = await cust.post(`/api/customers/me/prescriptions/${rx.id}/repeat-requests`, { data: { preferredBranchId: MAIN, note: null } });
  expect(noToken.status()).toBe(403);
  expect(await noToken.text()).toContain('csrf_token_invalid');
  await ctx.dispose();
  await cust.dispose();
});
