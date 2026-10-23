import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { randomUUID } from 'node:crypto';
import { server } from '../../mocks/server';

/**
 * Mock-vs-real contract check. The screens are tested against MSW mocks (`web/mocks/handlers/**`); if a mock's JSON
 * drifts from what the real API sends, every screen test still passes and the real screen breaks (e.g. the API sent
 * `policyKey`, the mock sent `key`, so the policy link went to `/policies/undefined`).
 *
 * For each GET the screens call, this fetches the response from the MSW handlers and from a real API and compares
 * SHAPES, never values: same property names (recursively), same JSON types, arrays by their element shape.
 *
 * Skipped unless CONTRACT_API_BASE_URL is set. Run it with `npm run test:contract` (see docs/runbooks/dev-seed.md):
 *   CONTRACT_API_BASE_URL=http://localhost:5098 \
 *   CONTRACT_CUSTOMER_EMAIL / CONTRACT_CUSTOMER_PASSWORD   (default JOURNEY_CUSTOMER_*)
 *   E2E_STAFF_ADMIN / E2E_STAFF_PASSWORD                    (the seeded e2e.admin)
 * The API must be on a scratch database (the check places one cash-on-delivery order for the seeded customer when she
 * has none, so the order shapes can be compared); it refuses catecommerce_dev / catecommerce_test.
 */

const REAL = (process.env.CONTRACT_API_BASE_URL ?? '').replace(/\/+$/, '');
const MOCK = 'https://mock.contract.test';

// ---- a tiny cookie-jar client --------------------------------------------------------------------------------

class Client {
  private jar = new Map<string, string>();
  constructor(readonly base: string) {}

  private absorb(response: Response) {
    for (const line of response.headers.getSetCookie?.() ?? []) {
      const [pair = '', ...attrs] = line.split(';');
      const eq = pair.indexOf('=');
      const name = pair.slice(0, eq).trim();
      const value = pair.slice(eq + 1).trim();
      const expired = attrs.some((a) => /^\s*max-age=0\s*$/i.test(a)) || value === '';
      if (expired) this.jar.delete(name);
      else this.jar.set(name, value);
    }
  }

  async request(method: string, path: string, body?: unknown, extra: Record<string, string> = {}) {
    const headers: Record<string, string> = { Accept: 'application/json', ...extra };
    if (this.jar.size) headers.Cookie = [...this.jar].map(([k, v]) => `${k}=${v}`).join('; ');
    if (method !== 'GET') {
      const csrf = this.jar.get('pc_csrf');
      if (csrf) headers['X-CSRF-Token'] = csrf;
    }
    if (body !== undefined) headers['Content-Type'] = 'application/json';
    const response = await fetch(`${this.base}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    this.absorb(response);
    const text = await response.text();
    let json: unknown;
    try {
      json = text ? JSON.parse(text) : undefined;
    } catch {
      json = undefined;
    }
    return { status: response.status, json, text };
  }
  get(path: string) {
    return this.request('GET', path);
  }
}

// ---- shapes --------------------------------------------------------------------------------------------------

interface Shape {
  types: Set<string>;
  props?: Map<string, Shape>;
  items?: Shape;
}

function typeOf(value: unknown): string {
  if (value === null) return 'null';
  if (Array.isArray(value)) return 'array';
  return typeof value; // string | number | boolean | object
}

function merge(a: Shape, b: Shape): Shape {
  const out: Shape = { types: new Set([...a.types, ...b.types]) };
  if (a.props || b.props) {
    out.props = new Map(a.props ?? []);
    for (const [k, v] of b.props ?? [])
      out.props.set(k, out.props.has(k) ? merge(out.props.get(k)!, v) : v);
  }
  if (a.items || b.items)
    out.items = a.items && b.items ? merge(a.items, b.items) : (a.items ?? b.items);
  return out;
}

function shapeOf(value: unknown): Shape {
  const t = typeOf(value);
  const shape: Shape = { types: new Set([t]) };
  if (t === 'object') {
    shape.props = new Map();
    for (const [k, v] of Object.entries(value as Record<string, unknown>))
      shape.props.set(k, shapeOf(v));
  } else if (t === 'array') {
    for (const item of value as unknown[]) {
      const s = shapeOf(item);
      shape.items = shape.items ? merge(shape.items, s) : s;
    }
  }
  return shape;
}

interface Diff {
  failures: string[];
  unverified: string[];
}

function compare(mock: Shape, real: Shape, path: string, diff: Diff) {
  const mt = [...mock.types].filter((t) => t !== 'null');
  const rt = [...real.types].filter((t) => t !== 'null');
  if (mt.length === 0 || rt.length === 0) {
    // One side only ever saw null here: nothing to compare the other side's type against.
    if (mt.length !== rt.length) diff.unverified.push(`${path} (null on one side only)`);
    return;
  }
  if (mt.sort().join() !== rt.sort().join()) {
    diff.failures.push(`${path} type: mock is ${mt.join('|')}, real is ${rt.join('|')}`);
    return;
  }
  if (mock.props && real.props) {
    for (const [k, v] of mock.props) {
      if (!real.props.has(k)) diff.failures.push(`${path}.${k} missing in real`);
      else compare(v, real.props.get(k)!, `${path}.${k}`, diff);
    }
    for (const k of real.props.keys())
      if (!mock.props.has(k)) diff.failures.push(`${path}.${k} missing in mock`);
  }
  if (mt.includes('array')) {
    if (mock.items && real.items) compare(mock.items, real.items, `${path}[]`, diff);
    else if (mock.items || real.items)
      diff.unverified.push(`${path}[] (${mock.items ? 'real' : 'mock'} array was empty)`);
  }
}

// ---- the endpoints -------------------------------------------------------------------------------------------

type Who = 'anon' | 'customer' | 'staff';
interface Ctx {
  who: Record<Who, Client>;
  slugs: string[];
  addressId: string;
  orderId: string | null;
  policyKeys: string[];
  zoneId: string | null;
  branchId: string | null;
  staffId: string | null;
  quotePath: string;
}
interface Endpoint {
  id: string;
  who: Who;
  paths: (ctx: Ctx) => string[];
}

const e = (
  id: string,
  who: Who,
  paths: (ctx: Ctx) => string[] = () => [id.replace(/^GET /, '')],
): Endpoint => ({ id, who, paths });

const ENDPOINTS: Endpoint[] = [
  // public shop
  e('GET /api/shop/products', 'anon', () => [
    '/api/shop/products',
    '/api/shop/products?category=CatFood',
    '/api/shop/products?brand=Whiskas&page=1',
  ]),
  e('GET /api/shop/filters', 'anon', () => [
    '/api/shop/filters',
    '/api/shop/filters?category=CatFood',
  ]),
  e('GET /api/shop/categories', 'anon'),
  e('GET /api/shop/origins', 'anon'),
  e('GET /api/shop/locales', 'anon'),
  e('GET /api/shop/products/{slug}', 'anon', (c) =>
    c.slugs.map((s) => `/api/shop/products/${encodeURIComponent(s)}`),
  ),
  e('GET /api/shop/products/{slug}/reviews', 'anon', (c) =>
    c.slugs.slice(0, 3).map((s) => `/api/shop/products/${encodeURIComponent(s)}/reviews`),
  ),
  e('GET /api/shop/products/{slug}/recommendations', 'anon', (c) =>
    c.slugs.slice(0, 3).map((s) => `/api/shop/products/${encodeURIComponent(s)}/recommendations`),
  ),
  e('GET /api/system/status', 'anon'),
  e('GET /api/branches', 'anon'),
  // public content
  e('GET /api/content/banners', 'anon', () => ['/api/content/banners?placement=Shop']),
  e('GET /api/content/faqs', 'anon'),
  e('GET /api/policies/{key}', 'anon', (c) =>
    c.policyKeys.map((k) => `/api/policies/${encodeURIComponent(k)}`),
  ),
  // customer
  e('GET /api/auth/session', 'customer'),
  e('GET /api/customers/me', 'customer'),
  e('GET /api/customers/me/addresses', 'customer'),
  e('GET /api/customers/me/notification-preferences', 'customer'),
  e('GET /api/customers/me/policy-acceptances/pending', 'customer'),
  e('GET /api/cart', 'customer'),
  e('GET /api/cats', 'customer', () => ['/api/cats?pageNumber=1&pageSize=10']),
  e('GET /api/customers/me/checkout/quote', 'customer', (c) => [c.quotePath]),
  e('GET /api/customers/me/orders', 'customer'),
  e('GET /api/customers/me/orders/{orderId}', 'customer', (c) =>
    c.orderId ? [`/api/customers/me/orders/${c.orderId}`] : [],
  ),
  e('GET /api/customers/me/wishlist', 'customer'),
  e('GET /api/customers/me/shop/products/{slug}/purchasability', 'customer', (c) =>
    c.slugs
      .slice(0, 2)
      .map((s) => `/api/customers/me/shop/products/${encodeURIComponent(s)}/purchasability`),
  ),
  e('GET /api/portal/dashboard', 'customer'),
  // staff
  e('GET /api/staff/session', 'staff'),
  e('GET /api/staff/branches/mine', 'staff'),
  e('GET /api/staff-roles', 'staff'),
  e('GET /api/staff-accounts', 'staff', () => ['/api/staff-accounts?pageNumber=1&pageSize=20']),
  e('GET /api/staff-accounts/{id}', 'staff', (c) =>
    c.staffId ? [`/api/staff-accounts/${c.staffId}`] : [],
  ),
  e('GET /api/admin/policies', 'staff'),
  e('GET /api/admin/content', 'staff'),
  e('GET /api/admin/delivery-zones', 'staff'),
  e('GET /api/admin/delivery-zones/{id}', 'staff', (c) =>
    c.zoneId ? [`/api/admin/delivery-zones/${c.zoneId}`] : [],
  ),
  e('GET /api/admin/couriers', 'staff'),
  e('GET /api/admin/discount-codes', 'staff'),
  e('GET /api/admin/bank-transfer-account', 'staff'),
  e('GET /api/admin/tax-classes', 'staff'),
  e('GET /api/admin/branches', 'staff'),
  e('GET /api/admin/branches/{id}', 'staff', (c) =>
    c.branchId ? [`/api/admin/branches/${c.branchId}`] : [],
  ),
  e('GET /api/staff/orders', 'staff'),
  e('GET /api/staff/orders/{orderId}', 'staff', (c) =>
    c.orderId ? [`/api/staff/orders/${c.orderId}`] : [],
  ),
  e('GET /api/staff/orders/awaiting-payment', 'staff'),
  e('GET /api/staff/inventory/stock', 'staff', () => [
    '/api/staff/inventory/stock?pageNumber=1&pageSize=20',
  ]),
  e('GET /api/staff/shop/products', 'staff'),
];

interface Collected {
  status: number;
  shapes: Shape[]; // one per path
  paths: string[];
}
type Side = Record<string, Collected>;

const idOf = (row: unknown, ...keys: string[]): string | null => {
  const r = row as Record<string, unknown> | undefined;
  for (const k of keys) if (r && typeof r[k] === 'string') return r[k] as string;
  return null;
};
const rowsOf = (body: unknown): unknown[] => {
  if (Array.isArray(body)) return body;
  const b = body as
    | { items?: unknown[]; results?: unknown[]; orders?: unknown[]; addresses?: unknown[] }
    | undefined;
  return b?.items ?? b?.results ?? b?.orders ?? b?.addresses ?? [];
};

const LATE = /orders|\/api\/cart|checkout\/quote|portal\/dashboard|policy-acceptances/;

/** The mock starts nearly empty for the signed-in customer (no address, no banner/FAQ, nothing pending), which would
 *  leave those shapes uncompared. Give it the same kind of data the seeded real database has, through its own
 *  admin/customer endpoints. */
async function seedMock(who: Record<Who, Client>) {
  await who.customer.request('POST', '/api/customers/me/addresses', {
    label: 'Home',
    line1: 'House 12, Road 5, Block C',
    line2: 'Mohammadpur',
    city: 'Dhaka',
    district: 'Dhaka',
    postalCode: '1207',
    deliveryInstructions: 'Ring twice',
    isDefault: true,
  });
  await who.staff.request('POST', '/api/admin/policies', {
    key: 'contract-check',
    title: 'Contract check',
    requiresAcceptance: true,
  });
  await who.staff.request('POST', '/api/admin/policies/contract-check/versions', {
    body: 'Body.',
    changeSummary: 'First.',
  });
  for (const [kind, slug, extra] of [
    ['Banner', 'contract-banner', { placement: 'Shop', linkUrl: '/shop' }],
    ['Faq', 'contract-faq', { sortOrder: 1 }],
  ] as const) {
    const made = await who.staff.request('POST', '/api/admin/content', {
      kind,
      slug,
      title: `Contract ${kind}`,
      draftBody: 'Body.',
      ...extra,
    });
    const id = (made.json as { contentId?: string } | undefined)?.contentId;
    if (id) await who.staff.request('POST', `/api/admin/content/${id}/publish`);
  }
}

async function collect(
  base: string,
  creds: { customer: [string, string]; staff: [string, string] },
  options: { seed: boolean },
): Promise<Side> {
  const who = { anon: new Client(base), customer: new Client(base), staff: new Client(base) };

  const cs = await who.customer.request('POST', '/api/auth/sign-in', {
    identifier: creds.customer[0],
    password: creds.customer[1],
  });
  if (cs.status !== 200) throw new Error(`customer sign-in at ${base} answered ${cs.status}`);
  const ss = await who.staff.request('POST', '/api/staff/auth/sign-in', {
    identifier: creds.staff[0],
    password: creds.staff[1],
  });
  if (ss.status !== 200) throw new Error(`staff sign-in at ${base} answered ${ss.status}`);
  if (options.seed) await seedMock(who);

  // products -> slugs (every product's detail is compared, so optional blocks are seen)
  const list = await who.anon.get('/api/shop/products?pageSize=48');
  const products = rowsOf(list.json) as Array<{ slug?: string }>;
  const slugs = products
    .map((p) => p.slug)
    .filter((s): s is string => !!s)
    .slice(0, 12);

  const firstSkuId = async () => {
    const detail = await who.anon.get(`/api/shop/products/${encodeURIComponent(slugs[0] ?? '')}`);
    return idOf(((detail.json as { skus?: unknown[] })?.skus ?? [])[0], 'skuId', 'id');
  };
  const ensureCartLine = async () => {
    const cart = await who.customer.get('/api/cart');
    if (((cart.json as { lines?: unknown[] })?.lines ?? []).length > 0) return;
    const skuId = await firstSkuId();
    if (skuId) await who.customer.request('POST', '/api/cart/lines', { skuId, quantity: 1 });
  };
  await ensureCartLine();

  const addresses = await who.customer.get('/api/customers/me/addresses');
  const addressId = idOf(rowsOf(addresses.json)[0], 'id', 'addressId') ?? '';
  const quotePath = `/api/customers/me/checkout/quote?fulfilment=Delivery&addressId=${addressId}&serviceLevel=Standard&paymentMethod=CashOnDelivery`;

  const zones = await who.staff.get('/api/admin/delivery-zones');
  const branches = await who.staff.get('/api/admin/branches');
  const staff = await who.staff.get('/api/staff-accounts?pageNumber=1&pageSize=20');
  const policies = await who.staff.get('/api/admin/policies');
  const policyKeys = rowsOf(policies.json)
    .map((p) => idOf(p, 'key', 'policyKey'))
    .filter((k): k is string => !!k);

  const ctx: Ctx = {
    who,
    slugs,
    addressId,
    orderId: null,
    policyKeys,
    zoneId: idOf(rowsOf(zones.json)[0], 'zoneId', 'id'),
    branchId: idOf(rowsOf(branches.json)[0], 'branchId', 'id'),
    staffId: idOf(rowsOf(staff.json)[0], 'staffAccountId', 'id'),
    quotePath,
  };

  const side: Side = {};
  const run = async (only?: RegExp) => {
    for (const ep of ENDPOINTS) {
      if (only && !only.test(ep.id)) continue;
      const paths = ep.paths(ctx);
      const collected: Collected = { status: 200, shapes: [], paths };
      for (const p of paths) {
        const r = await who[ep.who].get(p);
        if (r.status !== 200) collected.status = r.status;
        else if (r.json !== undefined) collected.shapes.push(shapeOf(r.json));
      }
      side[ep.id] = collected;
    }
  };

  // Phase 1: everything, with the customer as the seed left her (privacy not yet accepted, no orders).
  await run();

  // Phase 2: place one cash-on-delivery order if she has none, then re-read what depends on it.
  let orders = await who.customer.get('/api/customers/me/orders');
  if (rowsOf(orders.json).length === 0) {
    const pending = await who.customer.get('/api/customers/me/policy-acceptances/pending');
    for (const p of rowsOf(pending.json)) {
      const key = idOf(p, 'policyKey', 'key');
      const versionNumber = (p as { versionNumber?: number }).versionNumber;
      if (key)
        await who.customer.request('POST', '/api/customers/me/policy-acceptances', {
          policyKey: key,
          versionNumber,
        });
    }
    await ensureCartLine();
    // The fingerprint covers the chosen slot, so quote again with it (as the Review step does).
    const first = (await who.customer.get(quotePath)).json as {
      delivery?: {
        serviceLevels?: Array<{
          level: string;
          slots: Array<{ date: string; windowStart: string }>;
        }>;
      };
    };
    const slot0 = first?.delivery?.serviceLevels?.find((l) => l.level === 'Standard')?.slots?.[0];
    const slotQuery = slot0 ? `&slotDate=${slot0.date}&slotWindowStart=${slot0.windowStart}` : '';
    const quote = await who.customer.get(quotePath + slotQuery);
    const q = quote.json as {
      fingerprint?: string;
      delivery?: {
        serviceLevels?: Array<{
          level: string;
          slots: Array<{ date: string; windowStart: string }>;
        }>;
      };
    };
    const slot = q?.delivery?.serviceLevels?.find((l) => l.level === 'Standard')?.slots?.[0];
    await who.customer.request(
      'POST',
      '/api/customers/me/orders',
      {
        fulfilment: 'Delivery',
        deliveryAddressId: addressId,
        serviceLevel: 'Standard',
        slotDate: slot?.date,
        slotWindowStart: slot?.windowStart,
        paymentMethod: 'CashOnDelivery',
        quoteFingerprint: q?.fingerprint,
      },
      { 'Idempotency-Key': randomUUID() },
    );
    orders = await who.customer.get('/api/customers/me/orders');
    await ensureCartLine(); // placing emptied the cart: put a line back so cart and quote have content
  }
  ctx.orderId = idOf(rowsOf(orders.json)[0], 'orderId', 'id');
  await run(LATE);
  return side;
}

// Differences that are understood, reported to the owner, and deliberately NOT papered over by changing the mock. Each is
// reported on every run; the test passes only while the difference is exactly this one, and FAILS when it disappears
// (so the entry gets deleted once the real API / screen is fixed).
// Empty: the one entry (admin policy versions without `body`) was fixed in the API on 2026-10-15.
const KNOWN_DRIFT: Record<string, string[]> = {};

// ---- the tests -----------------------------------------------------------------------------------------------

describe.skipIf(!REAL)('mock-vs-real contract (GET responses)', () => {
  let mock: Side = {};
  let real: Side = {};
  const unverifiedReport: string[] = [];

  beforeAll(async () => {
    const customerEmail = process.env.CONTRACT_CUSTOMER_EMAIL ?? process.env.JOURNEY_CUSTOMER_EMAIL;
    const customerPassword =
      process.env.CONTRACT_CUSTOMER_PASSWORD ?? process.env.JOURNEY_CUSTOMER_PASSWORD;
    const staffId = process.env.E2E_STAFF_ADMIN;
    const staffPassword = process.env.E2E_STAFF_PASSWORD;
    if (!customerEmail || !customerPassword || !staffId || !staffPassword) {
      throw new Error(
        'Set JOURNEY_CUSTOMER_EMAIL/PASSWORD (or CONTRACT_CUSTOMER_*) and E2E_STAFF_ADMIN/E2E_STAFF_PASSWORD.',
      );
    }

    // 1. everything from the MSW handlers (the vitest setup file has the server listening).
    mock = await collect(
      MOCK,
      {
        customer: ['signed-in-customer@example.com', 'Password123!'],
        staff: ['admin@purrcare.test', 'AdminPass123!'],
      },
      { seed: true },
    );
    // 2. then stop MSW intercepting, and ask the real API.
    server.close();
    const dbCheck = await fetch(`${REAL}/api/dev/database`)
      .then((r) => (r.ok ? r.json() : null))
      .catch(() => null);
    const database = String(
      (dbCheck as { database?: string } | null)?.database ?? '',
    ).toLowerCase();
    if (!database)
      throw new Error(
        `Could not confirm which database ${REAL} uses (GET /api/dev/database); refusing to run.`,
      );
    if (database === 'catecommerce_dev' || database === 'catecommerce_test')
      throw new Error(
        `${REAL} is on ${database}; the contract check places an order and needs a scratch database.`,
      );

    real = await collect(
      REAL,
      { customer: [customerEmail, customerPassword], staff: [staffId, staffPassword] },
      { seed: false },
    );
  }, 120_000);

  afterAll(() => {
    if (unverifiedReport.length) {
      console.log(
        `Contract check - parts that could not be compared (one side had no data):\n  ${unverifiedReport.join('\n  ')}`,
      );
    }
  });

  it.each(ENDPOINTS.map((ep) => [ep.id]))(
    '%s has the same shape in the mock and the real API',
    (id) => {
      const m = mock[id];
      const r = real[id];
      expect(m, `no mock data collected for ${id}`).toBeDefined();
      expect(r, `no real data collected for ${id}`).toBeDefined();
      if (!m || !r) return;
      const failures: string[] = [];
      if (m.paths.length === 0 && r.paths.length === 0) return; // nothing to fetch (no id available on either side)
      if (m.status !== 200)
        failures.push(`mock answered ${m.status} for one of ${m.paths.join(', ')}`);
      if (r.status !== 200)
        failures.push(`real API answered ${r.status} for one of ${r.paths.join(', ')}`);
      if (m.shapes.length && r.shapes.length) {
        const mergedMock = m.shapes.reduce(merge);
        const mergedReal = r.shapes.reduce(merge);
        const diff: Diff = { failures: [], unverified: [] };
        compare(mergedMock, mergedReal, '$', diff);
        failures.push(...diff.failures.map((f) => `${id}: ${f}`));
        unverifiedReport.push(...diff.unverified.map((u) => `${id}: ${u}`));
      }
      const known = KNOWN_DRIFT[id] ?? [];
      const remaining = failures.filter((f) => !known.some((k) => f.endsWith(k)));
      for (const k of known) {
        expect(
          failures.some((f) => f.endsWith(k)),
          `known drift no longer present - remove it from KNOWN_DRIFT: ${id} ${k}`,
        ).toBe(true);
        unverifiedReport.push(`KNOWN DRIFT ${id}: ${k}`);
      }
      expect(remaining).toEqual([]);
    },
  );
});
