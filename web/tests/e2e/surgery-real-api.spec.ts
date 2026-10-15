/* eslint-disable @typescript-eslint/no-explicit-any -- the real API's JSON bodies are read untyped in this spec */
// Plain Playwright `test`, not ./fixtures: that wrapper waits for MSW's service worker, which never
// exists when NEXT_PUBLIC_USE_MOCKS=false (the point of this spec).
import fs from 'node:fs';
import {
  test,
  expect as baseExpect,
  request as pwRequest,
  type APIRequestContext,
  type Browser,
  type Page,
} from '@playwright/test';

/**
 * HO-SRG-002 §5.1 / §16: the E-series re-run against the REAL API (no MSW), written by the TEST gate
 * (2026-10-14). Opt-in only; skipped in every default (MSW) run. It needs a live API on a database seeded
 * with the surgery world (branch, theatre, rostered vets and nurses, a customer, priced theatre services,
 * consent forms, a controlled and a non-controlled drug with batches), the web dev server started with
 * `NEXT_PUBLIC_USE_MOCKS=false`, and
 *
 *   E2E_REAL_SURGERY=1  E2E_SEED_FILE=<seed json>  E2E_API_URL=http://localhost:5112  E2E_STAFF_PASSWORD=...
 *   PORT=3112 npx playwright test surgery-real-api --project=chromium --workers=1
 *
 * Staff are e2e.{admin,vet,vet2,nurse,assistant,reception}@purrcare.test. API-side set-up and verification
 * use their own cookie-and-CSRF sessions; the behaviour under test goes through the browser.
 *
 * NOT RE-RUNNABLE FROM THIS REPO ALONE (HO-SRG-002 §14 keeps `tests/**` out of this slice): the seeder
 * (a throwaway xUnit fact built on `SurgeryIntegrationTestBase` and `PharmacyE2ESeed`, which wrote the seed
 * JSON read below) and RA-N8's one SQL write (it moves `clinic.surgery.admitted_on` back so an entry at 23:55
 * on 12 Oct is allowed) were deleted after the gate run. See docs/gates/HO-SRG-002-TEST.md for how each was
 * built and for the API settings the run needed (`PublicSite:WebBaseUrl` must be https, or every surgery
 * notification is suppressed as RenderFailed; the sign-in rate limit raised for the run). Cross-test state
 * (`srg002-test-slot.txt`, `srg002-test-state.json`) is written into the current directory: delete both
 * between runs on a fresh database.
 */
const run = process.env.E2E_REAL_SURGERY === '1';
test.skip(!run, 'Set E2E_REAL_SURGERY=1 (see the header) to run against the real API.');

const expect = baseExpect.configure({ timeout: 20_000 });
const API = process.env.E2E_API_URL ?? 'http://localhost:5112';
const WEB_ORIGIN = `http://localhost:${process.env.PORT ?? '3112'}`;
const PASSWORD = process.env.E2E_STAFF_PASSWORD ?? '';
const seed: any =
  run && process.env.E2E_SEED_FILE
    ? JSON.parse(fs.readFileSync(process.env.E2E_SEED_FILE, 'utf8'))
    : {};
const BRANCH: string = seed.branchId ?? '';
const ZONE = 'Asia/Dhaka';
const email = (k: string) => `e2e.${k}@purrcare.test`;
type Role = 'admin' | 'vet' | 'vet2' | 'nurse' | 'assistant' | 'reception';
const ROLES: Role[] = ['admin', 'vet', 'vet2', 'nurse', 'assistant', 'reception'];

test.setTimeout(240_000);

// ---- API-side sessions --------------------------------------------------------------------------

type Res = { status: number; body: any };
class Session {
  constructor(private ctx: APIRequestContext) {}
  private async csrf(): Promise<string> {
    const state = await this.ctx.storageState();
    const c = state.cookies.find((x) => x.name === 'pc_csrf');
    return c ? decodeURIComponent(c.value) : '';
  }
  async call(method: string, url: string, data?: unknown): Promise<Res> {
    const csrf = await this.csrf();
    const res = await this.ctx.fetch(url, {
      method,
      data,
      headers: csrf ? { 'X-CSRF-Token': csrf } : {},
    });
    const text = await res.text();
    let body: any = null;
    try {
      body = text ? JSON.parse(text) : null;
    } catch {
      body = text;
    }
    return { status: res.status(), body };
  }
  get = (u: string) => this.call('GET', u);
  post = (u: string, d: unknown = {}) => this.call('POST', u, d);
  put = (u: string, d: unknown = {}) => this.call('PUT', u, d);
  dispose = () => this.ctx.dispose();
}

async function newSession(
  kind: 'staff' | 'customer',
  identifier: string,
  password = PASSWORD,
): Promise<Session> {
  const ctx = await pwRequest.newContext({
    baseURL: API,
    extraHTTPHeaders: {
      Origin: WEB_ORIGIN,
      ...(kind === 'staff' ? { 'X-Branch-Id': BRANCH } : {}),
    },
  });
  const s = new Session(ctx);
  const r = await s.post(kind === 'staff' ? '/api/staff/auth/sign-in' : '/api/auth/sign-in', {
    identifier,
    password,
  });
  if (r.status !== 200)
    throw new Error(`API sign-in ${identifier}: ${r.status} ${JSON.stringify(r.body)}`);
  return s;
}

const S = {} as Record<Role | 'customer', Session>;
const IDS = {} as Record<string, string>; // staff ids by role and by name
const STATE_FILE = 'srg002-test-state.json';
function readStateFile(): Record<string, any> {
  try {
    return JSON.parse(fs.readFileSync(STATE_FILE, 'utf8'));
  } catch {
    return {};
  }
}
let THEATRE2 = '';
let SURGEONS: any[] = [];
const names: Record<Role, string> = {
  admin: 'Ahsan Admin',
  vet: 'Rahman Vet',
  vet2: 'Sultana Vet',
  nurse: 'Akter Nurse',
  assistant: 'Tanvir Assistant',
  reception: 'Mitu Reception',
};

test.beforeAll(async () => {
  for (const r of ROLES) S[r] = await newSession('staff', email(r));
  S.customer = await newSession('customer', seed.customer.mobile, seed.customer.password);
  const list = await S.admin.get(`/api/staff/surgeries/staff?serviceId=${seed.serviceIds.spay}`);
  SURGEONS = list.body.assistants;
  for (const r of ROLES) IDS[r] = SURGEONS.find((p: any) => p.name === names[r])?.id ?? '';
  // a second theatre and a rostered second assistant, so cases that need their own slot do not collide with the first theatre's day
  const days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
  unwrap(
    await S.admin.put(`/api/admin/branches/${BRANCH}/rosters/${IDS.nurse}/pattern`, {
      slots: days.map((d) => ({ dayOfWeek: d, starts: '00:00', ends: '23:45' })),
    }),
    [200, 204],
    'roster nurse',
  );
  const kept = readStateFile();
  if (!kept.theatre2) {
    const room = unwrap(
      await S.admin.post(`/api/admin/branches/${BRANCH}/rooms`, {
        name: 'E2E Theatre 2',
        kind: 'Theatre',
      }),
      [201],
      'second theatre',
    );
    fs.writeFileSync(STATE_FILE, JSON.stringify({ ...kept, theatre2: room.roomId }));
  }
  THEATRE2 = readStateFile().theatre2;
});
test.afterAll(async () => {
  for (const s of Object.values(S)) await s?.dispose();
});

const unwrap = (r: Res, want: number[], what: string) => {
  if (!want.includes(r.status))
    throw new Error(`${what}: ${r.status} ${JSON.stringify(r.body).slice(0, 600)}`);
  return r.body;
};

// ---- clock and slots ----------------------------------------------------------------------------

const localDate = (d: Date) => new Intl.DateTimeFormat('en-CA', { timeZone: ZONE }).format(d);
const hhmm = (d: Date) =>
  new Intl.DateTimeFormat('en-GB', {
    timeZone: ZONE,
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(d);
/** Whole-hour starts: `k` hours after the next full hour + 1 (Asia/Dhaka has no DST, so the hour grid is UTC's). */
// The counter lives in a file so a worker restart after a failure never re-uses a slot.
const SLOT_BASE = new Date(process.env.E2E_SLOT_BASE ?? '2026-10-14T12:00:00.000Z').getTime();
const SLOT_FILE = process.env.E2E_SLOT_FILE ?? 'srg002-test-slot.txt';
function nextSlot(): { start: Date; date: string; label: RegExp; iso: string } {
  const used = fs.existsSync(SLOT_FILE) ? Number(fs.readFileSync(SLOT_FILE, 'utf8')) : 0;
  fs.writeFileSync(SLOT_FILE, String(used + 1));
  const start = new Date(SLOT_BASE + used * 3_600_000);
  const end = new Date(start.getTime() + 3_600_000);
  return {
    start,
    date: localDate(start),
    iso: start.toISOString(),
    label: new RegExp(`^${hhmm(start)}–${hhmm(end)}`),
  };
}
function slotAt(iso: string): ReturnType<typeof nextSlot> {
  const start = new Date(iso);
  const end = new Date(start.getTime() + 3_600_000);
  return {
    start,
    date: localDate(start),
    iso: start.toISOString(),
    label: new RegExp(`^${hhmm(start)}–${hhmm(end)}`),
  };
}
const utcToday = () => new Date().toISOString().slice(0, 10);

// ---- data helpers (API) -------------------------------------------------------------------------

let catSeq = 0;
async function newCat(
  label: string,
  sex: 'Female' | 'Male' = 'Female',
): Promise<{ id: string; name: string }> {
  catSeq += 1;
  const name = `${label}${Date.now().toString(36).slice(-4)}${catSeq}`;
  const ago = new Date(Date.now() - 700 * 864e5).toISOString().slice(0, 10);
  const cat = unwrap(
    await S.admin.post('/api/cats', {
      ownerId: seed.customer.id,
      name,
      species: 'Cat',
      sex,
      dateOfBirth: ago,
      dateOfBirthPrecision: 'Exact',
      breed: 'Domestic Shorthair',
      colourAndMarkings: 'Tabby',
    }),
    [201],
    'cat',
  );
  return { id: cat.catId, name };
}

type Booked = {
  surgeryId: string;
  appointmentId: string;
  catId: string;
  catName: string;
  slot: ReturnType<typeof nextSlot>;
};
async function bookViaApi(opts: {
  label: string;
  surgeon?: Role;
  assistant?: Role;
  serviceId?: string;
  sex?: 'Female' | 'Male';
  slot?: ReturnType<typeof nextSlot>;
  theatreId?: string;
}): Promise<Booked> {
  const cat = await newCat(opts.label, opts.sex);
  const slot = opts.slot ?? nextSlot();
  const body = unwrap(
    await S.reception.post('/api/staff/surgeries', {
      customerId: seed.customer.id,
      catId: cat.id,
      serviceId: opts.serviceId ?? seed.serviceIds.spay,
      startsOn: slot.iso,
      theatreId: opts.theatreId ?? seed.theatreId,
      surgeonId: IDS[opts.surgeon ?? 'vet'],
      assistantId: IDS[opts.assistant ?? 'assistant'],
      confirmNeuterStatusDiscrepancy: false,
    }),
    [201],
    `book ${opts.label}`,
  );
  const detail = unwrap(
    await S.vet.get(`/api/staff/surgeries/${body.surgeryId}`),
    [200],
    'surgery detail',
  );
  return {
    surgeryId: body.surgeryId,
    appointmentId: detail.appointmentId,
    catId: cat.id,
    catName: cat.name,
    slot,
  };
}

async function recordConsents(appointmentId: string, catId: string) {
  for (const kind of ['Surgery', 'Anaesthesia']) {
    const form = unwrap(
      await S.reception.get(`/api/staff/consent-forms/${kind}`),
      [200],
      'consent form',
    );
    unwrap(
      await S.reception.post('/api/staff/consents', {
        catId,
        appointmentId,
        kind,
        procedureDescription: 'Spay under general anaesthesia',
        method: 'OnScreenSignature',
        signature: [
          [
            [0.1, 0.2],
            [0.2, 0.3],
            [0.3, 0.25],
            [0.4, 0.4],
            [0.5, 0.35],
            [0.6, 0.5],
          ],
          [
            [0.15, 0.6],
            [0.35, 0.62],
            [0.55, 0.58],
            [0.75, 0.65],
            [0.85, 0.6],
          ],
        ],
        signedByName: 'Test Customer',
        signerRelationship: 'Owner',
        signedByCustomerId: seed.customer.id,
        policyVersionNumber: form.versionNumber,
      }),
      [201],
      `consent ${kind}`,
    );
  }
}

async function checklistExceptConsent(
  b: Booked,
  o: { asa?: boolean; weight?: boolean; fasting?: boolean; bloods?: boolean } = {},
) {
  const u = `/api/staff/surgeries/${b.surgeryId}`;
  if (o.fasting !== false)
    unwrap(
      await S.nurse.post(`${u}/checklist/FastingInstructionsIssued/complete`, {
        note: 'Told the owner by phone',
      }),
      [200, 204],
      'fasting',
    );
  if (o.bloods !== false)
    unwrap(
      await S.vet.post(`${u}/checklist/PreAnaestheticBloodWork/waive`, {
        reason: 'Young healthy cat',
      }),
      [200, 204],
      'bloods',
    );
  if (o.weight !== false)
    unwrap(
      await S.nurse.post(`/api/cats/${b.catId}/weights`, {
        kilograms: 3.4,
        recordedOn: utcToday(),
      }),
      [201],
      'weight',
    );
  if (o.asa !== false)
    unwrap(
      await S.vet.put(`${u}/asa-grade`, { grade: 'I', isEmergency: false }),
      [200, 204],
      'asa',
    );
}

/** Book (API) → checklist → both consents → admit → start by the vet: InTheatre. */
async function inTheatre(
  label: string,
  o: {
    surgeon?: Role;
    assistant?: Role;
    slot?: ReturnType<typeof nextSlot>;
    theatreId?: string;
  } = {},
): Promise<Booked> {
  const b = await bookViaApi({
    label,
    surgeon: o.surgeon,
    assistant: o.assistant,
    slot: o.slot,
    theatreId: o.theatreId,
  });
  await checklistExceptConsent(b);
  await recordConsents(b.appointmentId, b.catId);
  unwrap(await S.reception.post(`/api/staff/surgeries/${b.surgeryId}/admit`), [200, 204], 'admit');
  unwrap(await S.vet.post(`/api/staff/surgeries/${b.surgeryId}/start`), [200, 204], 'start');
  return b;
}

async function stockOnHand(skuId: string, lot: string): Promise<number> {
  const m = unwrap(await S.admin.get(`/api/staff/inventory/stock/${skuId}`), [200], 'stock');
  const batch = m.batches.find((x: any) => x.lotNumber === lot);
  return batch.locations.reduce((a: number, l: any) => a + Number(l.onHand), 0);
}
async function registerRows(medicineId: string): Promise<any[]> {
  const r = unwrap(
    await S.admin.get(`/api/staff/controlled-drugs/register?medicineId=${medicineId}&pageSize=100`),
    [200],
    'register',
  );
  return r.items;
}
async function logOf(surgeryId: string): Promise<any> {
  return unwrap(await S.vet.get(`/api/staff/surgeries/${surgeryId}/anaesthesia-log`), [200], 'log');
}

// ---- browser helpers ----------------------------------------------------------------------------

type UiRole = Role | 'customer';

async function signInUi(page: Page, role: UiRole): Promise<void> {
  await page.context().clearCookies();
  const errors: string[] = [];
  const onError = (e: Error) => errors.push(`pageerror: ${e.message}`);
  const onConsole = (m: import('@playwright/test').ConsoleMessage) =>
    m.type() === 'error' && errors.push(`console: ${m.text().slice(0, 300)}`);
  page.on('pageerror', onError);
  page.on('console', onConsole);
  await page.goto(role === 'customer' ? '/sign-in' : '/staff/sign-in');
  const hydrated = () =>
    page.waitForFunction(
      () => Boolean((window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl),
      undefined,
      { timeout: 15_000 },
    );
  try {
    await hydrated();
  } catch {
    console.log(
      '[signInUi] page not hydrated after 15 s; browser errors:',
      JSON.stringify(errors),
      'at',
      page.url(),
      '- reloading once',
    );
    await page.reload();
    await hydrated();
  }
  page.off('pageerror', onError);
  page.off('console', onConsole);
  const status = await page.evaluate(
    async ({ role, identifier, password }) => {
      const base = (window as unknown as { __apiBaseUrl?: string }).__apiBaseUrl ?? '';
      const r = await fetch(
        `${base}${role === 'customer' ? '/api/auth/sign-in' : '/api/staff/auth/sign-in'}`,
        {
          method: 'POST',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ identifier, password }),
        },
      );
      return r.status;
    },
    {
      role,
      identifier: role === 'customer' ? seed.customer.mobile : email(role),
      password: role === 'customer' ? seed.customer.password : PASSWORD,
    },
  );
  expect(status).toBe(200);
}

async function openAs(page: Page, role: UiRole, url: string): Promise<void> {
  if (!(page as any).__diag) {
    (page as any).__diag = true;
    page.on('response', async (r) => {
      if (r.request().method() !== 'GET' && r.status() >= 400 && r.url().startsWith(API))
        console.log(
          `[api ${r.status()}] ${r.request().method()} ${new URL(r.url()).pathname} ${(await r.text().catch(() => '')).slice(0, 500)}`,
        );
    });
  }
  await signInUi(page, role);
  await page.goto(url);
  await page.waitForLoadState('networkidle');
}

async function witnessWith(page: Page, who: Role, confirm: string | RegExp, password = PASSWORD) {
  const dialog = page.getByRole('dialog', { name: /witness needed/i });
  await expect(dialog).toBeVisible();
  await dialog.getByLabel(/witness mobile number or email/i).fill(email(who));
  await dialog.getByLabel(/witness password/i).fill(password);
  await dialog.getByRole('button', { name: confirm }).click();
}

const progress = (page: Page) =>
  page.getByRole('list', { name: 'Progress' }).getByText('Current').locator('..');

function bookingUrl(
  b: { catId: string; slot: { date: string } },
  o: { surgeon?: Role; assistant?: Role; serviceId?: string; theatreId?: string } = {},
) {
  const q = new URLSearchParams({
    customerId: seed.customer.id,
    catId: b.catId,
    serviceId: o.serviceId ?? seed.serviceIds.spay,
    date: b.slot.date,
    surgeonId: IDS[o.surgeon ?? 'vet']!,
    assistantId: IDS[o.assistant ?? 'assistant']!,
    theatreId: o.theatreId ?? seed.theatreId,
  });
  return `/staff/surgeries/new?${q.toString()}`;
}

/** Fills the drug dialog for the seeded controlled drug up to (not including) the witness step. */
async function fillKetamine(
  page: Page,
  o: { dose?: string; drawn?: string; wasted?: string } = {},
) {
  await page.getByRole('combobox', { name: /^medicine/i }).fill('Ketamine');
  await page
    .getByRole('option', { name: /E2E Ketamine/ })
    .first()
    .click();
  const unit = page.getByLabel('Dose unit');
  await unit.selectOption({ index: 1 });
  await page.getByRole('textbox', { name: 'Dose', exact: true }).fill(o.dose ?? '0.5');
  await page.getByLabel('Route').selectOption('Intravenous');
  await page.getByRole('radio').first().check();
  await page.getByLabel(/^Quantity drawn/).fill(o.drawn ?? '1');
  await page.getByLabel(/^Quantity wasted/).fill(o.wasted ?? '0.5');
}

// =================================================================================================
// A: booking
// =================================================================================================

let a1: {
  surgeryId: string;
  catName: string;
  catId: string;
  slot: ReturnType<typeof nextSlot>;
} | null = null;

test('RA-A1: reception books a spay choosing surgeon, assistant and theatre; the calendar shows the block', async ({
  page,
}) => {
  const cat = await newCat('Luna');
  const slot = nextSlot();
  await openAs(page, 'reception', '/staff/surgeries/new');
  await page.getByLabel(/find the customer/i).fill(seed.customer.mobile);
  await page.keyboard.press('Enter');
  await page
    .getByRole('button', { name: /test customer/i })
    .first()
    .click();
  await page.getByLabel('Cat', { exact: true }).selectOption({ value: cat.id });
  await page.getByLabel('Procedure', { exact: true }).selectOption({ value: seed.serviceIds.spay });
  await page.getByLabel(/^date/i).fill(slot.date);
  await page.getByLabel('Theatre').selectOption({ value: seed.theatreId });
  await page.getByLabel('Surgeon').selectOption({ value: IDS.vet! });
  await page.getByLabel('Assistant').selectOption({ value: IDS.assistant! });
  const starts = page.getByRole('group', { name: /available starts/i });
  await starts.getByRole('button', { name: slot.label }).click();
  await expect(page.getByText(/for information — paid at the clinic/).first()).toBeVisible();
  await page.getByRole('button', { name: 'Book surgery' }).click();
  const confirm = page.getByRole('dialog');
  await expect(confirm).toContainText(/Book .*’s spay .* on .*Rahman Vet with Tanvir Assistant\?/);
  await confirm.getByRole('button', { name: 'Book surgery' }).click();
  await expect(page).toHaveURL(/\/staff\/surgeries\/[0-9a-f-]{36}$/, { timeout: 30_000 });
  const surgeryId = page.url().split('/').pop()!;
  await expect(
    page.getByRole('heading', { level: 1, name: new RegExp(`${cat.name}`) }),
  ).toBeVisible();
  await expect(page.getByRole('list', { name: 'Progress' })).toContainText('Scheduled');
  a1 = { surgeryId, catName: cat.name, catId: cat.id, slot };

  // The calendar: by vet, the surgeon's and the assistant's column; by room, the theatre's.
  await page.goto(`/staff/appointments?date=${slot.date}`);
  const links = page.locator(`a[href="/staff/surgeries/${surgeryId}"]`);
  await expect(links.first()).toBeVisible();
  expect(await links.count()).toBeGreaterThanOrEqual(2);
  await expect(page.getByText(/Surgery · .* · Booked/).first()).toBeVisible();
  await expect(page.getByText(/with Tanvir Assistant/).first()).toBeVisible();
  await expect(page.getByRole('button', { name: /Move/ })).toHaveCount(0);
  await page.goto(`/staff/appointments?date=${slot.date}&view=room`);
  await expect(page.locator(`a[href="/staff/surgeries/${surgeryId}"]`).first()).toBeVisible();
  await page.locator(`a[href="/staff/surgeries/${surgeryId}"]`).first().click();
  await expect(page).toHaveURL(new RegExp(`/staff/surgeries/${surgeryId}$`));
});

test('RA-A2: the surgeon, assistant and theatre are busy at the first booking; the explainer says so and another start books', async ({
  page,
}) => {
  expect(a1, 'RA-A1 must have run').not.toBeNull();
  const second = await newCat('Nala');
  await openAs(page, 'reception', bookingUrl({ catId: second.id, slot: a1!.slot }));
  const explainer = page.getByRole('heading', { name: 'Why some times are missing' }).locator('..');
  await expect(explainer).toBeVisible();
  console.log('[explainer]', (await explainer.innerText()).replace(/\n+/g, ' | '));
  await expect(explainer).toContainText(/Rahman Vet/);
  await expect(explainer).toContainText(/Tanvir Assistant/);
  const starts = page.getByRole('group', { name: /available starts/i });
  await expect(starts.getByRole('button', { name: a1!.slot.label })).toHaveCount(0);
  const buttons = await starts.getByRole('button').count();
  expect(buttons).toBeGreaterThan(0);
  const free = starts.getByRole('button').first();
  console.log('[offered start]', await free.innerText());
  await free.click();
  await page.getByRole('button', { name: 'Book surgery' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Book surgery' }).click();
  await expect(page).toHaveURL(/\/staff\/surgeries\/[0-9a-f-]{36}$/, { timeout: 30_000 });
});

test('RA-A3: two browser contexts book the same theatre and time; one wins and the other is told why', async ({
  browser,
}) => {
  const slot = nextSlot();
  const catA = await newCat('Mimi');
  const catB = await newCat('Coco');
  const [pa, pb] = await twoPages(browser);
  // Sequential: two sign-ins of one account at the same instant answer 409 (observed, see the gate file).
  await openAs(pa, 'reception', bookingUrl({ catId: catA.id, slot }));
  await openAs(pb, 'reception', bookingUrl({ catId: catB.id, slot }));
  for (const p of [pa, pb]) {
    await p
      .getByRole('group', { name: /available starts/i })
      .getByRole('button', { name: slot.label })
      .click();
    await p.getByRole('button', { name: 'Book surgery' }).click();
    await expect(p.getByRole('dialog')).toBeVisible();
  }
  // both confirm at the same moment
  await Promise.all([
    pa.getByRole('dialog').getByRole('button', { name: 'Book surgery' }).click(),
    pb.getByRole('dialog').getByRole('button', { name: 'Book surgery' }).click(),
  ]);
  await Promise.race([
    pa.waitForURL(/\/staff\/surgeries\/[0-9a-f-]{36}$/, { timeout: 30_000 }).catch(() => null),
    pb.waitForURL(/\/staff\/surgeries\/[0-9a-f-]{36}$/, { timeout: 30_000 }).catch(() => null),
  ]);
  await pa.waitForTimeout(3000);
  const wonA = /\/staff\/surgeries\/[0-9a-f-]{36}$/.test(pa.url());
  const wonB = /\/staff\/surgeries\/[0-9a-f-]{36}$/.test(pb.url());
  console.log(`[race] A won: ${wonA}  B won: ${wonB}`);
  expect(Number(wonA) + Number(wonB), 'exactly one context books the slot').toBe(1);
  const loser = wonA ? pb : pa;
  const text = await loser.locator('main').innerText();
  console.log('[race loser sees]', text.replace(/\n+/g, ' | ').slice(0, 700));
  await expect(
    loser
      .getByRole('alert')
      .filter({ hasText: /Someone has just booked that time|busy|already|not free|booked/i })
      .first(),
  ).toBeVisible();
  // and the time is gone from the refreshed availability
  await expect(
    loser
      .getByRole('group', { name: /available starts/i })
      .getByRole('button', { name: slot.label }),
  ).toHaveCount(0, { timeout: 20_000 });
  await pa.context().close();
  await pb.context().close();
});

async function twoPages(browser: Browser): Promise<[Page, Page]> {
  const ca = await browser.newContext({ baseURL: WEB_ORIGIN });
  const cb = await browser.newContext({ baseURL: WEB_ORIGIN });
  return [await ca.newPage(), await cb.newPage()];
}

// =================================================================================================
// B / N: a surgery started with the consent override, then the controlled drug with its witness
// =================================================================================================

// Cross-test state lives in a file so a worker restart after a failure does not lose it.
const readState = readStateFile;
const ST = {
  get s5(): Booked | null {
    return readState().s5 ?? null;
  },
  set s5(v: Booked | null) {
    fs.writeFileSync(STATE_FILE, JSON.stringify({ ...readState(), s5: v }));
  },
};

test('RA-B2: admitted but with no consent, Start is blocked and a nurse has no override', async ({
  page,
}) => {
  const b = await bookViaApi({ label: 'Pepper' });
  await checklistExceptConsent(b);
  unwrap(await S.reception.post(`/api/staff/surgeries/${b.surgeryId}/admit`), [200, 204], 'admit');
  ST.s5 = b;
  // direct API: a nurse may not override (AC-18)
  const nurseOverride = await S.nurse.post(
    `/api/staff/surgeries/${b.surgeryId}/start-with-consent-override`,
    { reason: 'Cat collapsing, owner unreachable' },
  );
  expect(nurseOverride.status, JSON.stringify(nurseOverride.body)).toBe(403);
  await openAs(page, 'nurse', `/staff/surgeries/${b.surgeryId}`);
  await expect(page.getByRole('button', { name: 'Start surgery' })).toBeDisabled();
  await expect(page.getByText(/Consent has not been recorded/)).toBeVisible();
  await expect(page.getByRole('button', { name: /emergency override/i })).toHaveCount(0);
});

test('RA-B3: a vet starts with an emergency override; the banner names the vet and the reason', async ({
  page,
}) => {
  expect(ST.s5, 'RA-B2 must have run').not.toBeNull();
  await openAs(page, 'vet', `/staff/surgeries/${ST.s5!.surgeryId}`);
  await page.getByRole('button', { name: 'Start with emergency override' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('Reason for the emergency override').fill('123456789');
  await expect(dialog.getByRole('button', { name: 'Continue' })).toBeDisabled();
  await dialog
    .getByLabel('Reason for the emergency override')
    .fill('Cat collapsing, owner unreachable');
  await dialog.getByRole('button', { name: 'Continue' }).click();
  await expect(dialog).toContainText('recorded permanently');
  await dialog.getByRole('button', { name: 'Start surgery without consent' }).click();
  await expect(
    page.getByText(
      /Started without recorded consent — emergency override by Rahman Vet at \d\d:\d\d: Cat collapsing, owner unreachable/,
    ),
  ).toBeVisible();
  await expect(progress(page)).toContainText('In theatre');
  const detail = unwrap(
    await S.vet.get(`/api/staff/surgeries/${ST.s5!.surgeryId}`),
    [200],
    'detail',
  );
  expect(detail.status).toBe('InTheatre');
});

test('RA-B4: reception sees no clinical write control and every direct write is a real 403', async ({
  page,
}) => {
  expect(ST.s5, 'RA-B3 must have run').not.toBeNull();
  const id = ST.s5!.surgeryId;
  await openAs(page, 'reception', `/staff/surgeries/${id}#anaesthesia`);
  await expect(page.getByText('Anaesthesia log')).toBeVisible();
  for (const name of [
    /Add drug/,
    /Add vitals/,
    /Add milestone/,
    /Start surgery/,
    /override/i,
    /End procedure/,
  ]) {
    await expect(page.getByRole('button', { name })).toHaveCount(0);
  }
  const u = `/api/staff/surgeries/${id}`;
  const now = new Date().toISOString();
  const drug = {
    medicineId: seed.propofol.medicineId,
    doseAmount: 1,
    doseUnit: 'Ml',
    route: 'Intravenous',
    occurredOn: now,
    administeredById: IDS.vet,
  };
  const attempts: Array<[string, Res]> = [
    [
      'milestone',
      await S.reception.post(`${u}/anaesthesia-log/milestones`, {
        milestone: 'Induction',
        occurredOn: now,
      }),
    ],
    [
      'vitals',
      await S.reception.post(`${u}/anaesthesia-log/vitals`, { occurredOn: now, heartRateBpm: 120 }),
    ],
    ['drug', await S.reception.post(`${u}/anaesthesia-log/drugs`, drug)],
    ['end-procedure', await S.reception.post(`${u}/end-procedure`, { outcome: 'Completed' })],
    [
      'report',
      await S.reception.put(`${u}/report`, {
        procedurePerformed: 'x',
        findings: 'x',
        technique: 'x',
        sutureMaterial: 'x',
        hadComplications: false,
        surgeonId: IDS.vet,
        assistantId: IDS.assistant,
      }),
    ],
    [
      'override',
      await S.reception.post(`${u}/start-with-consent-override`, {
        reason: 'Cat collapsing, owner unreachable',
      }),
    ],
    ['nurse drug', await S.nurse.post(`${u}/anaesthesia-log/drugs`, drug)],
  ];
  for (const [what, r] of attempts)
    expect(r.status, `${what}: ${JSON.stringify(r.body)}`).toBe(403);
  // and the reads still work for reception
  expect((await S.reception.get(`${u}/anaesthesia-log`)).status).toBe(200);
});

test('RA-N4 / RA-C1: a controlled drug with a witness is saved once, deducts the stock once and names the witness', async ({
  page,
}) => {
  expect(ST.s5, 'RA-B3 must have run').not.toBeNull();
  const id = ST.s5!.surgeryId;
  const before = await stockOnHand(seed.ketamine.skuId, 'E2E-KET-NEAR');
  const regBefore = (await registerRows(seed.ketamine.medicineId)).length;
  await openAs(page, 'vet', `/staff/surgeries/${id}#anaesthesia`);
  // milestone and vitals first (units shown)
  await page.getByRole('button', { name: 'Add milestone' }).click();
  await page.getByLabel('Milestone', { exact: true }).selectOption('Premedication');
  await page.getByRole('button', { name: 'Save milestone' }).click();
  await expect(page.getByText('Premedication', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Add vitals' }).click();
  await page.getByLabel(/^Heart rate/).fill('120');
  await page.getByLabel(/^Temperature/).fill('38.6');
  await page.getByRole('button', { name: 'Save vitals' }).click();
  await expect(page.getByText(/120\s?bpm/)).toBeVisible();
  await expect(page.getByText(/38\.6\s?°C/)).toBeVisible();

  await page.getByRole('button', { name: 'Add drug' }).click();
  await fillKetamine(page);
  await expect(page.getByRole('button', { name: 'Next: witness' })).toBeEnabled();
  await page.getByRole('button', { name: 'Next: witness' }).click();
  await witnessWith(page, 'nurse', 'Confirm and save entry');
  await expect(page.getByText(/Controlled — witnessed by Akter Nurse/)).toBeVisible({
    timeout: 30_000,
  });
  const log = await logOf(id);
  const drugs = log.entries.filter((e: any) => e.kind === 'Drug');
  expect(drugs.length).toBe(1);
  console.log('[drug entry values]', JSON.stringify(drugs[0].values));
  expect(await stockOnHand(seed.ketamine.skuId, 'E2E-KET-NEAR')).toBe(before - 1);
  const regAfter = await registerRows(seed.ketamine.medicineId);
  console.log(
    '[register rows]',
    regAfter.length,
    JSON.stringify(regAfter.map((r: any) => [r.entryNumber, r.kind, r.quantityDelta?.amount])),
  );
  // drawn 1 = 0.5 given + 0.5 wasted: the register holds an administration row and a wastage row for the one entry
  expect(regAfter.length).toBe(regBefore + 2);
});

test('RA-N5: a wrong witness password is refused in the witness dialog; nothing is written', async ({
  page,
}) => {
  expect(ST.s5, 'RA-B3 must have run').not.toBeNull();
  const id = ST.s5!.surgeryId;
  const stock = await stockOnHand(seed.ketamine.skuId, 'E2E-KET-NEAR');
  const reg = (await registerRows(seed.ketamine.medicineId)).length;
  const entries = (await logOf(id)).entries.length;
  await openAs(page, 'vet', `/staff/surgeries/${id}#anaesthesia`);
  await page.getByRole('button', { name: 'Add drug' }).click();
  await fillKetamine(page);
  await page.getByRole('button', { name: 'Next: witness' }).click();
  await witnessWith(page, 'nurse', 'Confirm and save entry', 'definitely-not-the-password');
  const dialog = page.getByRole('dialog', { name: /witness needed/i });
  await expect(dialog.getByRole('alert').first()).toBeVisible({ timeout: 30_000 });
  console.log(
    '[wrong witness shows]',
    (await dialog.innerText()).replace(/\n+/g, ' | ').slice(0, 400),
  );
  await expect(dialog.getByLabel(/witness password/i)).toHaveValue('');
  expect((await logOf(id)).entries.length).toBe(entries);
  expect(await stockOnHand(seed.ketamine.skuId, 'E2E-KET-NEAR')).toBe(stock);
  expect((await registerRows(seed.ketamine.medicineId)).length).toBe(reg);
});

test('RA-N6 / RA-N7: a lost response is retried with the same entryId and nothing is deducted twice', async ({
  page,
}) => {
  expect(ST.s5, 'RA-B3 must have run').not.toBeNull();
  const id = ST.s5!.surgeryId;
  const stock = await stockOnHand(seed.ketamine.skuId, 'E2E-KET-NEAR');
  const reg = (await registerRows(seed.ketamine.medicineId)).length;
  const entries = (await logOf(id)).entries.filter((e: any) => e.kind === 'Drug').length;
  const posted: any[] = [];
  let dropped = 0;
  await page.route(`**/api/staff/surgeries/${id}/anaesthesia-log/drugs`, async (route) => {
    if (route.request().method() !== 'POST') return route.continue();
    posted.push(route.request().postDataJSON());
    if (dropped === 0) {
      dropped += 1;
      const response = await route.fetch(); // the server has processed it
      console.log('[lost response] server answered', response.status());
      await route.abort('connectionreset'); // ...and the browser never sees the answer
      return;
    }
    await route.continue();
  });
  await openAs(page, 'vet', `/staff/surgeries/${id}#anaesthesia`);
  await page.getByRole('button', { name: 'Add drug' }).click();
  await fillKetamine(page, { dose: '0.4', drawn: '2', wasted: '0.5' });
  await page.getByRole('button', { name: 'Next: witness' }).click();
  await witnessWith(page, 'nurse', 'Confirm and save entry');
  // the first answer is lost: the user is left on an error and tries again
  await expect.poll(() => dropped, { timeout: 30_000 }).toBe(1);
  await page.waitForTimeout(1500);
  // the server DID write it once
  expect(await stockOnHand(seed.ketamine.skuId, 'E2E-KET-NEAR')).toBe(stock - 2);
  await page.screenshot({ path: 'test-results/srg002-test-lost-response.png' });
  console.log(
    '[after the lost response]',
    (
      await page
        .getByRole('dialog')
        .first()
        .innerText()
        .catch(() => '(no dialog)')
    )
      .replace(/\n+/g, ' | ')
      .slice(0, 600),
  );
  // retry from the UI
  const witnessDialog = page.getByRole('dialog', { name: /witness needed/i });
  if (!(await witnessDialog.isVisible().catch(() => false))) {
    await page.getByRole('button', { name: 'Next: witness' }).click();
  }
  await witnessWith(page, 'nurse', 'Confirm and save entry');
  await expect(page.getByText(/drew 2\s?/).first()).toBeVisible({ timeout: 30_000 });
  expect(posted.length).toBe(2);
  expect(posted[0].entryId).toBeTruthy();
  expect(posted[1].entryId, 'the retry carries the same entryId').toBe(posted[0].entryId);
  // exactly one more drug entry, one deduction, one register entry
  const after = (await logOf(id)).entries.filter((e: any) => e.kind === 'Drug');
  expect(after.length).toBe(entries + 1);
  expect(await stockOnHand(seed.ketamine.skuId, 'E2E-KET-NEAR')).toBe(stock - 2);
  expect((await registerRows(seed.ketamine.medicineId)).length).toBe(reg + 2); // administration + wastage

  // N7 control, API-side: the same entryId with a different dose is refused and writes nothing
  const bad = await S.vet.post(`/api/staff/surgeries/${id}/anaesthesia-log/drugs`, {
    ...posted[0],
    doseAmount: 0.9,
    witnessIdentifier: email('nurse'),
    witnessPassword: PASSWORD,
  });
  console.log('[mismatch replay]', bad.status, JSON.stringify(bad.body).slice(0, 300));
  expect([400, 409]).toContain(bad.status);
  expect(JSON.stringify(bad.body)).toContain('AnaesthesiaEntryIdMismatch');
  // ...and the identical body replays the first result, again with no second deduction
  const same = await S.vet.post(`/api/staff/surgeries/${id}/anaesthesia-log/drugs`, {
    ...posted[0],
    witnessIdentifier: email('nurse'),
    witnessPassword: PASSWORD,
  });
  console.log('[identical replay]', same.status, JSON.stringify(same.body).slice(0, 200));
  expect(same.status).toBe(201);
  expect((await logOf(id)).entries.filter((e: any) => e.kind === 'Drug').length).toBe(entries + 1);
  expect(await stockOnHand(seed.ketamine.skuId, 'E2E-KET-NEAR')).toBe(stock - 2);
  expect((await registerRows(seed.ketamine.medicineId)).length).toBe(reg + 2);
});

// =================================================================================================
// D / N1-N3: the report editor and its autosave, against the real report endpoints
// =================================================================================================

async function inRecovery(label: string): Promise<Booked> {
  const b = await inTheatre(label);
  unwrap(
    await S.vet.post(`/api/staff/surgeries/${b.surgeryId}/end-procedure`, { outcome: 'Completed' }),
    [200, 204],
    'end procedure',
  );
  return b;
}
const reportOf = async (id: string) =>
  unwrap(await S.vet.get(`/api/staff/surgeries/${id}/report`), [200], 'report');

test('RA-N1 / RA-N2 / RA-N3: report autosave saves, survives a tab switch and a quick return, and reports a stale version', async ({
  page,
}) => {
  const b = await inRecovery('Poppy');
  const id = b.surgeryId;
  const findings = page.getByLabel('Findings', { exact: true });
  const technique = page.getByLabel('Technique', { exact: true });
  const suture = page.getByLabel('Suture material', { exact: true });
  await openAs(page, 'vet', `/staff/surgeries/${id}#report`);

  // N1: type, wait for the autosave, reload
  await findings.fill('N1 findings: uterus and ovaries normal');
  await expect(page.getByText(/Draft saved \d\d:\d\d/)).toBeVisible({ timeout: 20_000 });
  const v1 = (await reportOf(id)).version;
  console.log('[N1] server version after the first autosave', v1);
  await page.reload();
  await expect(findings).toHaveValue('N1 findings: uterus and ovaries normal');

  // N2: type, switch tab inside the 5 s window, come back BEFORE the flush has answered (the PUT is held 4 s)
  await page.route(`**/api/staff/surgeries/${id}/report`, async (route) => {
    if (route.request().method() !== 'PUT') return route.continue();
    const response = await route.fetch();
    await new Promise((resolve) => setTimeout(resolve, 4000));
    await route.fulfill({ response });
  });
  await technique.fill('N2 technique: midline, three-clamp');
  await page.getByRole('tab', { name: 'Pre-op' }).click();
  await page.getByRole('tab', { name: 'Report' }).click();
  const sawLoading = await page
    .getByText('Loading the report…')
    .isVisible()
    .catch(() => false);
  console.log('[N2] loading state visible on the quick return:', sawLoading);
  await expect(technique).toHaveValue('N2 technique: midline, three-clamp', { timeout: 30_000 });
  await expect(findings).toHaveValue('N1 findings: uterus and ovaries normal');
  const v2 = (await reportOf(id)).version;
  console.log('[N2] server version after the flush', v2);
  expect(v2).toBeGreaterThan(v1);
  // the next keystrokes save from the NEW version: no conflict
  await suture.fill('N2 suture: 3-0 Monocryl');
  await expect(page.getByText(/Draft saved \d\d:\d\d/)).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText(/Someone else changed this report/)).toHaveCount(0);
  const server = await reportOf(id);
  expect(server.technique).toBe('N2 technique: midline, three-clamp');
  expect(server.sutureMaterial).toBe('N2 suture: 3-0 Monocryl');
  expect(server.findings).toBe('N1 findings: uterus and ovaries normal');
  expect(server.version).toBeGreaterThan(v2);
  await page.unroute(`**/api/staff/surgeries/${id}/report`);

  // N3: someone else saves first; this editor's next autosave is stale
  const cur = await reportOf(id);
  const other = await S.vet2.put(`/api/staff/surgeries/${id}/report`, {
    procedurePerformed: cur.procedurePerformed,
    findings: 'N3 other vet wrote this',
    technique: cur.technique,
    sutureMaterial: cur.sutureMaterial,
    hadComplications: false,
    complications: null,
    surgeonId: cur.surgeonId ?? IDS.vet,
    assistantId: cur.assistantId ?? IDS.assistant,
    expectedVersion: cur.version,
  });
  console.log('[N3] second vet saved', other.status, JSON.stringify(other.body).slice(0, 200));
  expect([200, 204]).toContain(other.status);
  await findings.fill('N3 mine: written after the other vet saved');
  const banner = page.getByText(
    /Someone else changed this report, so your changes were not saved\./,
  );
  await expect(banner).toBeVisible({ timeout: 30_000 });
  await expect(page.getByLabel('Your text, for copying')).toHaveValue(
    /N3 mine: written after the other vet saved/,
  );
  expect((await reportOf(id)).findings).toBe('N3 other vet wrote this');
  await page.getByRole('button', { name: 'Reload the latest report' }).click();
  await expect(findings).toHaveValue('N3 other vet wrote this', { timeout: 20_000 });
});

// =================================================================================================
// X1: the whole spay through the UI against the real exclusion constraints
// =================================================================================================

test('RA-X1 / AC-3: a spay from booking to the owner’s page (E-A1 → E-E2 in one run)', async ({
  page,
}) => {
  test.setTimeout(420_000);
  const cat = await newCat('Daisy');
  const slot = nextSlot();
  const priorCats = { id: cat.id };

  // E-A1: reception books through the form (deep-linked choices; the start and the confirm are clicked)
  await openAs(page, 'reception', bookingUrl({ catId: priorCats.id, slot }));
  await page
    .getByRole('group', { name: /available starts/i })
    .getByRole('button', { name: slot.label })
    .click();
  await page.getByRole('button', { name: 'Book surgery' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Book surgery' }).click();
  await expect(page).toHaveURL(/\/staff\/surgeries\/[0-9a-f-]{36}$/, { timeout: 30_000 });
  const id = page.url().split('/').pop()!;

  // E-B1: the day list names what is outstanding
  await page.goto(`/staff/surgeries?date=${slot.date}`);
  const row = page
    .getByRole('listitem')
    .filter({ hasText: cat.name })
    .or(page.getByRole('row').filter({ hasText: cat.name }))
    .first();
  await expect(row).toContainText(
    /5 outstanding: fasting instructions, blood work, weight, consent, ASA grade/,
  );

  // the vet takes over: admit, and try to start with nothing done (E-B2)
  await signInUi(page, 'vet');
  await page.goto(`/staff/surgeries/${id}`);
  await expect(page.getByRole('heading', { level: 1 })).toContainText(cat.name);
  await page.getByRole('button', { name: 'Admit' }).click();
  await expect(progress(page)).toContainText('Pre-op');
  await expect(page.getByRole('button', { name: 'Start surgery' })).toBeDisabled();
  await expect(page.getByText(/Consent has not been recorded/)).toBeVisible();
  await expect(page.getByText(/pre-op checklist is not complete/)).toBeVisible();

  await page.getByRole('button', { name: 'Mark issued' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Mark issued' }).click();
  await page.getByRole('button', { name: 'Waive' }).click();
  const waive = page.getByRole('dialog', { name: /Waive the blood work/ });
  await waive.getByLabel(/Reason for waiving/).fill('Young healthy cat, owner declined bloods');
  await waive.getByRole('button', { name: 'Waive blood work' }).click();
  await expect(
    page.getByText('Waived because: Young healthy cat, owner declined bloods'),
  ).toBeVisible();
  unwrap(
    await S.nurse.post(`/api/cats/${cat.id}/weights`, { kilograms: 3.8, recordedOn: utcToday() }),
    [201],
    'weight',
  );
  const appt = unwrap(await S.vet.get(`/api/staff/surgeries/${id}`), [200], 'detail').appointmentId;
  await recordConsents(appt, cat.id);
  await page.reload();
  await page.getByRole('radio', { name: 'III' }).check();
  await page.getByRole('button', { name: 'Save ASA grade' }).click();
  await expect(page.getByText('Everything is in place to start.')).toBeVisible();
  await page.getByRole('button', { name: 'Start surgery' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Start surgery' }).click();
  await expect(progress(page)).toContainText('In theatre');

  // E-C1: milestone, vitals, the controlled drug with its witness
  await page.getByRole('tab', { name: 'Anaesthesia' }).click();
  await page.getByRole('button', { name: 'Add milestone' }).click();
  await page.getByLabel('Milestone', { exact: true }).selectOption('Induction');
  await page.getByRole('button', { name: 'Save milestone' }).click();
  await expect(page.getByText('Induction', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Add vitals' }).click();
  await page.getByLabel(/^Heart rate/).fill('120');
  await page.getByLabel(/^Temperature/).fill('38.6');
  await page.getByRole('button', { name: 'Save vitals' }).click();
  await expect(page.getByText(/120\s?bpm/)).toBeVisible();
  await page.getByRole('button', { name: 'Add drug' }).click();
  await fillKetamine(page);
  await page.getByRole('button', { name: 'Next: witness' }).click();
  await witnessWith(page, 'nurse', 'Confirm and save entry');
  await expect(page.getByText(/Controlled — witnessed by Akter Nurse/)).toBeVisible({
    timeout: 30_000,
  });

  // E-C2: correct it; a new witness is required
  await page.getByRole('button', { name: /^Correct the .* drug entry/ }).click();
  await page.getByLabel('Reason for the correction').fill('Dose was entered in the wrong unit');
  await page.getByRole('button', { name: 'Review correction' }).click();
  await page.getByRole('button', { name: 'Continue to the witness' }).click();
  await witnessWith(page, 'nurse', 'Confirm and add correction');
  await expect(page.getByText('Corrected', { exact: true })).toBeVisible({ timeout: 30_000 });
  await expect(page.getByRole('link', { name: 'See the correction' })).toBeVisible();
  await expect(page.getByRole('button', { name: /edit|delete/i })).toHaveCount(0);

  // E-D1: end the procedure, the neuter status, the report, the addendum
  await page.getByRole('tab', { name: 'Report' }).click();
  await page.getByRole('button', { name: 'End procedure' }).click();
  await expect(page.getByRole('dialog')).toContainText(/neuter status/);
  await page.getByRole('dialog').getByRole('button', { name: 'End procedure' }).click();
  await expect(page.getByText(/Neuter status set to Neutered on /)).toBeVisible({
    timeout: 30_000,
  });
  const catRecord = unwrap(await S.vet.get(`/api/cats/${cat.id}`), [200], 'cat');
  console.log('[cat after the spay]', JSON.stringify(catRecord).slice(0, 300));
  expect(JSON.stringify(catRecord)).toMatch(/Neutered/);
  await page.getByLabel('Findings', { exact: true }).fill('Routine. Uterus and ovaries normal.');
  await page
    .getByLabel('Technique', { exact: true })
    .fill('Midline approach, three-clamp technique');
  await page.getByLabel('Suture material', { exact: true }).fill('3-0 Monocryl, intradermal');
  await page.getByRole('button', { name: 'Finalise report' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Finalise report' }).click();
  await expect(page.getByText(/Finalised by Rahman Vet on /)).toBeVisible({ timeout: 30_000 });
  await expect(page.getByLabel('Findings', { exact: true })).toHaveCount(0);
  const edit = await S.vet.put(`/api/staff/surgeries/${id}/report`, {
    procedurePerformed: 'x',
    findings: 'changed',
    technique: 'x',
    sutureMaterial: 'x',
    hadComplications: false,
    surgeonId: IDS.vet,
    assistantId: IDS.assistant,
    expectedVersion: 1,
  });
  console.log('[edit of a finalised report]', edit.status, JSON.stringify(edit.body).slice(0, 200));
  expect([400, 409]).toContain(edit.status);
  await page.getByLabel('Add addendum').fill('Owner phoned at 17:00: eating well.');
  await page.getByRole('button', { name: 'Add addendum' }).click();
  await expect(page.getByText('Owner phoned at 17:00: eating well.')).toBeVisible();

  // E-E1: recovery and the front desk
  await page.getByRole('tab', { name: 'Recovery' }).click();
  await page
    .getByRole('radio', { name: 'Ready for discharge', exact: true })
    .check({ force: true });
  await page.getByRole('button', { name: 'Set status' }).click();
  await expect(page.getByText('Now: Ready for discharge')).toBeVisible();
  await page.goto('/staff/front-desk?tab=recovery');
  const card = page.getByRole('listitem').filter({ hasText: cat.name });
  await expect(card).toContainText('Ready for discharge');
  await expect(card).toContainText(/since \d\d:\d\d · set by /);

  // E-E2: discharge with the protocol's defaults, the print sheet, the owner
  await page.goto(`/staff/surgeries/${id}#discharge`);
  await page.getByRole('button', { name: /^Discharge / }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Discharge' }).click();
  await expect(
    page.getByText(/Discharged\. The post-op instructions have been sent to/),
  ).toBeVisible({ timeout: 30_000 });
  await page.getByRole('link', { name: 'Print post-op sheet' }).click();
  await expect(
    page.getByRole('article', { name: new RegExp(`Post-op sheet for ${cat.name}`) }),
  ).toContainText(/Keep her warm and quiet/);

  await signInUi(page, 'customer');
  await page.goto('/portal/surgeries?tab=past');
  await page
    .getByRole('link', { name: new RegExp(cat.name) })
    .first()
    .click();
  await expect(page.getByText(/Keep her warm and quiet/)).toBeVisible();
  await expect(page.getByText(/Wound check — /)).toBeVisible();
  await expect(page.getByText(/Suture removal — /)).toBeVisible();
  await expect(page.getByRole('link', { name: 'Book follow-up' }).first()).toHaveAttribute(
    'href',
    /^\/book\?/,
  );
  await expect(page.getByRole('button', { name: /pay/i })).toHaveCount(0);

  // the inbox message (the worker is on in the API host): poll the owner's inbox through the API
  let inbox: any[] = [];
  for (let attempt = 0; attempt < 18; attempt += 1) {
    const r = await S.customer.get('/api/customers/me/inbox?pageSize=50');
    inbox = r.body?.items ?? [];
    if (
      inbox.some(
        (m) =>
          /ready to go home/i.test(m.title ?? '') &&
          JSON.stringify(m).toLowerCase().includes(cat.name.toLowerCase()),
      )
    )
      break;
    await page.waitForTimeout(5000);
  }
  console.log('[inbox]', inbox.length, JSON.stringify(inbox.slice(0, 3)).slice(0, 700));
  expect(
    inbox.some(
      (m) =>
        /ready to go home/i.test(m.title ?? '') &&
        JSON.stringify(m).toLowerCase().includes(cat.name.toLowerCase()),
    ),
    'a post-op message for the cat is in the owner’s inbox',
  ).toBe(true);
});

// =================================================================================================
// N8: a correction across midnight
// =================================================================================================

test('RA-N8: correcting a 23:55 entry to 00:05 lands on the next day, not a day away', async ({
  page,
}) => {
  test.setTimeout(420_000);
  const b = await inTheatre('Midge', {
    slot: slotAt('2026-10-14T11:00:00.000Z'),
    surgeon: 'vet2',
    assistant: 'nurse',
    theatreId: THEATRE2,
  });
  const id = b.surgeryId;
  // The only direct write of the run: move the admission back so an entry at 23:55 on 12 October (Dhaka) is allowed.
  const { execSync } = await import('node:child_process');
  execSync(
    'dotnet test tests/CatEcommerce.IntegrationTests --no-build --filter "FullyQualifiedName~Srg002TestBackdate"',
    {
      cwd: 'D:/my-repo/cat_ecommerce-wt/SRG-002',
      env: {
        ...process.env,
        CATECOMMERCE_TEST_DB:
          'Host=localhost;Port=5432;Database=catecommerce_srg002e2e;Username=postgres;Password=postgres',
        SRG002_BACKDATE_ID: id,
        SRG002_BACKDATE_TO: '2026-10-12T14:00:00+00:00',
      },
      stdio: 'pipe',
    },
  );
  const late = '2026-10-12T17:55:00.000Z'; // 23:55 Asia/Dhaka on 12 Oct
  const made = await S.nurse.post(`/api/staff/surgeries/${id}/anaesthesia-log/milestones`, {
    milestone: 'Induction',
    occurredOn: late,
    note: 'N8 before midnight',
  });
  console.log('[N8] the 23:55 entry', made.status, JSON.stringify(made.body).slice(0, 200));
  expect(made.status).toBe(201);
  await openAs(page, 'vet', `/staff/surgeries/${id}#anaesthesia`);
  await page.getByRole('button', { name: /^Correct the 23:55 / }).click();
  await page.getByLabel('Reason for the correction').fill('The induction was after midnight');
  await page.getByLabel(/^Time \(/).fill('00:05');
  await page.getByRole('button', { name: 'Review correction' }).click();
  await page.getByRole('button', { name: 'Add correction' }).click();
  await expect(page.getByText('Corrected', { exact: true })).toBeVisible({ timeout: 30_000 });
  const log = await logOf(id);
  const entries = log.entries.filter((e: any) => e.kind === 'Milestone');
  console.log(
    '[N8] milestones',
    JSON.stringify(
      entries.map((e: any) => [e.occurredOn, e.isCorrected, e.correctsEntryId ?? null]),
    ),
  );
  const correction = log.entries.find((e: any) => e.kind === 'Correction');
  expect(new Date(correction.occurredOn).toISOString()).toBe('2026-10-12T18:05:00.000Z'); // 00:05 on 13 Oct, five minutes after the original
});

// =================================================================================================
// F1: a campaign
// =================================================================================================

test('RA-F1: an administrator creates a campaign; reception books under it; the cases and totals list it', async ({
  page,
}) => {
  test.setTimeout(300_000);
  const code = `E2E${Date.now().toString(36).toUpperCase().slice(-6)}`;
  await openAs(page, 'admin', '/staff/surgeries/campaigns');
  await page.getByRole('button', { name: 'New campaign' }).click();
  const form = page.getByRole('dialog');
  await form.getByLabel('Code').fill(code);
  await form.getByLabel('Name').fill('E2E neuter drive');
  await form.getByLabel('First day').fill(localDate(new Date(Date.now() - 864e5)));
  await form.getByLabel('Last day').fill(localDate(new Date(Date.now() + 30 * 864e5)));
  await form.locator(`#campaign-price-${seed.serviceIds.neuter}`).fill('500');
  await form.getByRole('button', { name: 'Save campaign' }).click();
  await expect(page.getByText(code).first()).toBeVisible({ timeout: 20_000 });

  const cat = await newCat('Tom', 'Male');
  const slot = nextSlot();
  await signInUi(page, 'reception');
  await page.goto(
    bookingUrl(
      { catId: cat.id, slot },
      {
        serviceId: seed.serviceIds.neuter,
        surgeon: 'vet2',
        assistant: 'nurse',
        theatreId: THEATRE2,
      },
    ),
  );
  await page
    .getByRole('group', { name: /available starts/i })
    .getByRole('button')
    .first()
    .click();
  await page.getByLabel('Price').selectOption({ label: `E2E neuter drive (${code})` });
  await page.getByRole('button', { name: 'Book surgery' }).click();
  await expect(page.getByText('Note why this cat is eligible for the campaign.')).toBeVisible();
  await page.getByLabel('Why is this cat eligible?').fill('Community cat from the Mirpur colony');
  await page.getByRole('button', { name: 'Book surgery' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Book surgery' }).click();
  await expect(page).toHaveURL(/\/staff\/surgeries\/[0-9a-f-]{36}$/, { timeout: 30_000 });
  await expect(page.getByText(/Campaign price, for information/)).toBeVisible();

  await signInUi(page, 'admin');
  await page.goto('/staff/surgeries/campaigns');
  await page.getByRole('link', { name: `Cases for ${code}` }).click();
  await expect(page.getByText(cat.name).first()).toBeVisible();
  await expect(page.getByLabel('Totals')).toContainText(/1 case · .*500.* campaign/);
});
