#!/usr/bin/env node
// Dev sample-data seeder for PurrCare (docs/runbooks/dev-seed.md).
//
// Fills a database with one small, believable Dhaka clinic + shop by calling the REAL API, so every
// domain rule (validation, publication checklist, stock movements, audit) applies exactly as it does
// for a person clicking through the staff screens. It never talks to PostgreSQL itself.
//
//   SEED_STAFF_PASSWORD=... SEED_CUSTOMER_PASSWORD=... \
//     node web/scripts/seed-dev.mjs --api http://localhost:5099 --api-log <file the API's stdout goes to>
//
// Prerequisites (seed-dev-db.mjs does all of them for you):
//   - the API runs in Development with Notifications:Channels:{Email,Sms}:Sender=Development (the
//     development sender logs every message, including password-reset links, to the API's stdout) and
//     an https PublicSite/Account WebBaseUrl (a reset link must be an absolute https URL or it is
//     suppressed as RenderFailed);
//   - the API's stdout is redirected to the file passed as --api-log;
//
// Extra catalogue (2026-10-16): `--extra scripts/data/dry-food-catalogue.json --only-extra` adds only the products
// in that file (plus their origins, prices and opening stock) to an already-seeded database, e.g. the dev API on
// :5005. Food in the file carries life stage + protein only; manufacturer, ingredients and analysis stay empty.
// `scripts/data/amarpet-cat-catalogue.json` (2026-10-16) is the whole AmarPet cat section: each product names its
// own `category`/`subcategory` (non-food products have no food details and may have no origin).
//   - an Administrator staff row for e2e.admin@purrcare.test exists. The API cannot create the very
//     first staff account (every staff endpoint needs a signed-in Administrator), so seed-dev-db.mjs
//     inserts that one row without a password; this script then sets its password through the normal
//     reset flow.
//
// Idempotent: everything is looked up before it is created, so a second run changes nothing.
// Passwords come only from the environment and are never printed.

import { readFileSync, existsSync, statSync } from 'node:fs';

// ── arguments / environment ──────────────────────────────────────────────────────────────────────
function parseArgs(argv) {
  const out = {};
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (!a.startsWith('--')) continue;
    const key = a.slice(2);
    const next = argv[i + 1];
    if (next === undefined || next.startsWith('--')) out[key] = true;
    else {
      out[key] = next;
      i++;
    }
  }
  return out;
}

const args = parseArgs(process.argv.slice(2));
const API = String(args.api ?? '').replace(/\/+$/, '');
const API_LOG = args['api-log'] ? String(args['api-log']) : null;
const STAFF_PASSWORD = process.env.SEED_STAFF_PASSWORD ?? '';
const CUSTOMER_PASSWORD = process.env.SEED_CUSTOMER_PASSWORD ?? '';
const CAPTCHA_ANSWER = process.env.SEED_CAPTCHA_ANSWER ?? 'TEST1';

if (!API) fail('Pass --api <base url of the running API>, e.g. --api http://localhost:5099');
if (!STAFF_PASSWORD || !CUSTOMER_PASSWORD)
  fail('Set SEED_STAFF_PASSWORD and SEED_CUSTOMER_PASSWORD (at least 10 characters each).');
if (STAFF_PASSWORD.length < 10 || CUSTOMER_PASSWORD.length < 10)
  fail('SEED_STAFF_PASSWORD and SEED_CUSTOMER_PASSWORD must be at least 10 characters.');

function fail(message) {
  console.error(`seed-dev: ${message}`);
  process.exit(1);
}

// ── the sample world ─────────────────────────────────────────────────────────────────────────────
const MAIN_BRANCH_ID = '00000000-0000-0000-0000-00000000ba1c'; // inserted by the AddAdministration migration
const SHOP_FLOOR_LOCATION_ID = '00000000-0000-0000-0000-0000000051a1'; // "Shop floor" at Main clinic (migration)

const STAFF = [
  {
    key: 'admin',
    email: 'e2e.admin@purrcare.test',
    fullName: 'Farhana Akter',
    mobile: '01711000001',
    roles: ['Administrator'],
  },
  {
    key: 'nurse',
    email: 'e2e.nurse@purrcare.test',
    fullName: 'Nusrat Jahan',
    mobile: '01711000002',
    roles: ['Nurse'],
  },
  {
    key: 'reception',
    email: 'e2e.reception@purrcare.test',
    fullName: 'Tanvir Hossain',
    mobile: '01711000003',
    roles: ['Reception'],
  },
  {
    key: 'inventory',
    email: 'e2e.inventory@purrcare.test',
    fullName: 'Rafiq Islam',
    mobile: '01711000004',
    roles: ['Inventory'],
  },
  {
    key: 'approver',
    email: 'e2e.approver@purrcare.test',
    fullName: 'Shirin Sultana',
    mobile: '01711000005',
    roles: ['Administrator'],
  },
];

const CUSTOMER = {
  fullName: 'Ayesha Rahman',
  email: 'customer@purrcare.test',
  mobile: '01819123456',
  address: {
    label: 'Home',
    line1: 'House 12, Road 5, Block C',
    line2: 'Mohammadpur',
    city: 'Dhaka',
    district: 'Dhaka',
    postalCode: '1207',
    deliveryInstructions: 'Ring the bell twice; the guard is at the gate.',
  },
  cat: {
    name: 'Mishti',
    sex: 'Female',
    dateOfBirth: '2023-03-15',
    dateOfBirthPrecision: 'Estimated',
    breed: 'Domestic Shorthair',
    colourAndMarkings: 'Calico, white chest',
  },
};

const BRANCH = {
  name: 'Main clinic',
  timeZoneId: 'Asia/Dhaka',
  addressLine: 'House 34, Road 7, Dhanmondi, Dhaka 1209',
  phoneNumber: '+8801711000100',
  email: 'hello@purrcare.test',
  // Saturday to Thursday, 09:00-20:00; closed on Friday.
  hours: ['Saturday', 'Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday'].map((d) => ({
    dayOfWeek: d,
    opens: '09:00',
    closes: '20:00',
  })),
};

const FOOD_LABEL = {
  storageInstructions: 'Store in a cool, dry place. Reseal the bag after opening.',
};

// 12 products, 6 brands, every category. Prices in BDT.
const PRODUCTS = [
  {
    slug: 'whiskas-adult-ocean-fish-dry',
    name: 'Whiskas Adult Ocean Fish Dry Food',
    brand: 'Whiskas',
    category: 'CatFood',
    subcategory: 'Dry',
    description:
      'Complete dry food for adult cats with ocean fish. Crunchy pockets help keep teeth clean.',
    origin: 'TH',
    food: {
      manufacturer: 'Mars Petcare',
      lifeStage: 'Adult',
      proteinSource: 'Fish',
      analysis: [30, 10, 4, 10],
    },
    expiry: true,
    skus: [
      { code: 'WHI-OF-480G', size: '480 g', grams: 500, price: 450 },
      { code: 'WHI-OF-1K2', size: '1.2 kg', grams: 1250, price: 1050 },
      { code: 'WHI-OF-3KG', size: '3 kg', grams: 3100, price: 2450 },
    ],
  },
  {
    slug: 'whiskas-kitten-tuna-pouch',
    name: 'Whiskas Kitten Tuna in Jelly Pouch',
    brand: 'Whiskas',
    category: 'CatFood',
    subcategory: 'Wet',
    description: 'Wet food for kittens from 2 to 12 months, tender tuna pieces in jelly.',
    origin: 'TH',
    food: {
      manufacturer: 'Mars Petcare',
      lifeStage: 'Kitten',
      proteinSource: 'Tuna',
      analysis: [8, 3, 0.5, 84],
    },
    expiry: true,
    skus: [
      { code: 'WHI-KT-80G', size: '80 g', grams: 90, price: 150 },
      { code: 'WHI-KT-12X80', size: '12 x 80 g', grams: 1050, price: 1650, pack: 12 },
    ],
  },
  {
    slug: 'royal-canin-kitten-dry',
    name: 'Royal Canin Kitten Dry Food',
    brand: 'Royal Canin',
    category: 'CatFood',
    subcategory: 'Kitten',
    description:
      'Tailored nutrition for kittens up to 12 months, supporting a healthy immune system.',
    origin: 'FR',
    food: {
      manufacturer: 'Royal Canin SAS',
      lifeStage: 'Kitten',
      proteinSource: 'Chicken',
      analysis: [34, 16, 2.8, 5.5],
    },
    expiry: true,
    skus: [
      { code: 'RC-KIT-400G', size: '400 g', grams: 450, price: 950 },
      { code: 'RC-KIT-2KG', size: '2 kg', grams: 2100, price: 3900 },
    ],
  },
  {
    slug: 'royal-canin-indoor-27',
    name: 'Royal Canin Indoor 27 Adult',
    brand: 'Royal Canin',
    category: 'CatFood',
    subcategory: 'Adult',
    description: 'For indoor adult cats aged 1 to 7 years. Helps reduce stool odour and hairballs.',
    origin: 'FR',
    food: {
      manufacturer: 'Royal Canin SAS',
      lifeStage: 'Adult',
      proteinSource: 'Chicken',
      analysis: [27, 13, 5.6, 5.5],
    },
    expiry: true,
    healthFocus: ['Hairball'],
    skus: [
      { code: 'RC-IND-400G', size: '400 g', grams: 450, price: 900 },
      { code: 'RC-IND-2KG', size: '2 kg', grams: 2100, price: 3600 },
    ],
  },
  {
    slug: 'me-o-adult-seafood-dry',
    name: 'Me-O Adult Seafood Dry Food',
    brand: 'Me-O',
    category: 'CatFood',
    subcategory: 'Dry',
    description: 'Seafood recipe for adult cats with taurine for healthy eyes and a shiny coat.',
    origin: 'TH',
    food: {
      manufacturer: 'Perfect Companion Group',
      lifeStage: 'Adult',
      proteinSource: 'Fish',
      analysis: [30, 9, 4, 10],
    },
    expiry: true,
    healthFocus: ['SkinAndCoat'],
    skus: [
      { code: 'MEO-SF-1K2', size: '1.2 kg', grams: 1250, price: 750 },
      { code: 'MEO-SF-3KG', size: '3 kg', grams: 3100, price: 1750 },
    ],
  },
  {
    slug: 'me-o-creamy-treats-salmon',
    name: 'Me-O Creamy Treats Salmon',
    brand: 'Me-O',
    category: 'CatFood',
    subcategory: 'Treats',
    description: 'Lickable creamy salmon treat. Feed from the stick or as a topper.',
    origin: 'TH',
    food: {
      manufacturer: 'Perfect Companion Group',
      lifeStage: 'AllLifeStages',
      proteinSource: 'Salmon',
      analysis: [7, 1, 0.5, 89],
    },
    expiry: true,
    skus: [{ code: 'MEO-CT-4X15', size: '4 x 15 g', grams: 70, price: 180, pack: 4 }],
  },
  {
    slug: 'purina-friskies-seafood-sensations',
    name: 'Purina Friskies Seafood Sensations',
    brand: 'Purina Friskies',
    category: 'CatFood',
    subcategory: 'Dry',
    description: 'Crunchy bites with the flavours of salmon, tuna, shrimp and ocean fish.',
    origin: 'MY',
    food: {
      manufacturer: 'Nestle Purina PetCare',
      lifeStage: 'Adult',
      proteinSource: 'Mixed',
      analysis: [30, 11, 4, 12],
    },
    expiry: true,
    skus: [
      { code: 'FRI-SS-1KG', size: '1 kg', grams: 1050, price: 650 },
      { code: 'FRI-SS-3KG', size: '3 kg', grams: 3100, price: 1850 },
    ],
  },
  {
    slug: 'catsan-hygiene-plus-litter',
    name: 'Catsan Hygiene Plus Litter',
    brand: 'Catsan',
    category: 'Litter',
    subcategory: 'Silica',
    description: 'White hygiene granules that lock in moisture and odour. Does not clump.',
    origin: 'DE',
    expiry: false,
    skus: [
      { code: 'CAT-HP-5L', size: '5 L', grams: 2900, price: 850 },
      { code: 'CAT-HP-10L', size: '10 L', grams: 5600, price: 1500 },
    ],
  },
  {
    slug: 'kit-cat-soya-clump-litter',
    name: 'Kit Cat Soya Clump Litter',
    brand: 'Kit Cat',
    category: 'Litter',
    subcategory: 'Tofu',
    description: 'Plant-based tofu litter that clumps fast and can be flushed in small amounts.',
    origin: 'SG',
    expiry: false,
    skus: [{ code: 'KC-SOY-7L', size: '7 L', grams: 2600, price: 1100 }],
  },
  {
    slug: 'kit-cat-scoop-and-tray-set',
    name: 'Kit Cat Litter Tray with Scoop',
    brand: 'Kit Cat',
    category: 'LitterPotsAndAccessories',
    subcategory: 'Trays',
    description: 'High-sided litter tray with a matching scoop. Easy to wash.',
    origin: 'SG',
    expiry: false,
    skus: [{ code: 'KC-TRAY-GRY', size: '45 x 35 cm', colour: 'Grey', grams: 900, price: 1200 }],
  },
  {
    slug: 'kit-cat-feather-wand-toy',
    name: 'Kit Cat Feather Wand Toy',
    brand: 'Kit Cat',
    category: 'ToysAndFurniture',
    subcategory: null,
    description: 'Teaser wand with natural feathers and a bell for interactive play.',
    origin: 'SG',
    expiry: false,
    skus: [{ code: 'KC-WAND-1', size: '45 cm', grams: 80, price: 250 }],
  },
  {
    slug: 'royal-canin-hairball-care-gel',
    name: 'Royal Canin Hairball Care Gel',
    brand: 'Royal Canin',
    category: 'HealthAndGrooming',
    subcategory: null,
    description: 'Malt-flavoured gel that helps hairballs pass naturally. For cats over 10 months.',
    origin: 'FR',
    expiry: true,
    healthFocus: ['Hairball'],
    skus: [{ code: 'RC-HB-50G', size: '50 g', grams: 80, price: 1250 }],
  },
];

const EXTRA_PRODUCTS = args.extra
  ? JSON.parse(readFileSync(String(args.extra), 'utf8')).products.map((p) => ({
      slug: p.slug,
      name: p.name,
      brand: p.brand,
      category: p.category ?? 'CatFood',
      subcategory: p.subcategory === undefined ? 'Dry' : p.subcategory,
      description: p.description,
      origin: p.origin ?? null,
      // No made-up label facts: only the life stage and main protein the listing states (food only).
      food:
        (p.category ?? 'CatFood') === 'CatFood'
          ? { lifeStage: p.lifeStage, proteinSource: p.protein, labelUnknown: true }
          : undefined,
      expiry: (p.category ?? 'CatFood') === 'CatFood',
      skus: p.skus,
    }))
  : [];

const SUPPLIER = {
  code: 'PAWS-BD',
  name: 'Paws Distribution Ltd',
  countryCode: 'BD',
  contactName: 'Kamal Uddin',
  phone: '+8801711000200',
  email: 'orders@paws-distribution.test',
  address: 'Plot 18, Tejgaon Industrial Area, Dhaka 1208',
  currencyCode: 'BDT',
  leadTimeDays: 3,
  paymentTerms: 'Net',
  paymentTermDays: 30,
  notes: 'Sample supplier created by the dev seeder.',
};

const BANNER = {
  slug: 'free-delivery-dhaka',
  title: 'Free delivery in Dhaka on orders over ৳2,000',
  body: 'Order cat food, litter and care essentials today. Standard delivery anywhere in Dhaka is ৳60, and free on orders over ৳2,000.',
  placement: 'Shop',
  linkUrl: '/shop',
};

const POLICIES = [
  {
    key: 'privacy',
    title: 'Privacy Policy',
    requiresAcceptance: true,
    changeSummary: 'First published version.',
    body: markup([
      '## Privacy Policy',
      'PurrCare Veterinary Clinic & Shop ("PurrCare", "we") respects your privacy. This policy explains what we collect, why, and the choices you have.',
      '### What we collect',
      '- Your name, mobile number and email address when you create an account.',
      '- Delivery addresses you save for shop orders.',
      "- Your cats' profiles, appointments, treatments and medical records.",
      '- Orders, invoices and payments you make with us.',
      '### How we use it',
      '- To book appointments, treat your cats and keep their medical history.',
      '- To deliver your orders and send receipts, reminders and updates you asked for.',
      '- To meet our legal and accounting obligations in Bangladesh.',
      '### Sharing',
      "We share your details only with couriers delivering your order and with laboratories or specialists involved in your cat's care. We never sell your data.",
      '### Your choices',
      'You can update your details, change notification preferences, download your data or ask us to delete your account from your profile at any time.',
      '### Contact',
      'Questions about this policy: hello@purrcare.test or +880 1711-000100.',
    ]),
  },
  {
    key: 'terms',
    title: 'Terms of Service',
    requiresAcceptance: false,
    changeSummary: 'First published version.',
    body: markup([
      '## Terms of Service',
      'These terms apply when you use the PurrCare website, book appointments or buy from our shop.',
      '### Orders and prices',
      '- Prices are in Bangladeshi Taka (BDT) and include any applicable VAT.',
      '- We confirm every order by SMS or email. Stock is reserved when you place the order.',
      '- Payment is cash on delivery, cash or card in store, or bank transfer.',
      '### Delivery',
      '- We deliver within Dhaka, usually the next working day.',
      '- Standard delivery is ৳60 and free on orders over ৳2,000.',
      '### Returns',
      'Unopened products can be returned within 7 days. Opened food, litter and medicines cannot be returned unless they are faulty.',
      '### Appointments',
      'Please tell us at least 2 hours ahead if you need to cancel or move an appointment.',
    ]),
  },
];

// LimitedMarkup: blocks separated by a blank line; consecutive '- ' bullets stay one list.
function markup(lines) {
  const NL = '\n';
  return lines.reduce((out, line, i) => {
    if (i === 0) return line;
    const sep = line.startsWith('- ') && lines[i - 1].startsWith('- ') ? NL : NL + NL;
    return out + sep + line;
  }, '');
}

const FAQS = [
  {
    slug: 'delivery-areas',
    title: 'Where do you deliver?',
    body: 'We deliver anywhere in Dhaka district. Standard delivery is ৳60 and free on orders over ৳2,000. Most orders arrive the next day.',
    sortOrder: 1,
  },
  {
    slug: 'payment-methods',
    title: 'How can I pay?',
    body: 'You can pay cash on delivery, cash or card at the clinic, or by bank transfer. Bank details are shown at checkout when you choose bank transfer.',
    sortOrder: 2,
  },
  {
    slug: 'opening-hours',
    title: 'When is the clinic open?',
    body: 'Saturday to Thursday, 9:00 am to 8:00 pm. We are closed on Fridays. Call +880 1711-000100 to book or ask a question.',
    sortOrder: 3,
  },
];

const DELIVERY_ZONE = {
  name: 'Dhaka',
  district: 'Dhaka',
  standard: { baseCharge: 60, freeOver: 2000, leadTimeDays: 1 },
  windows: ['Saturday', 'Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday'].flatMap((d) => [
    { dayOfWeek: d, start: '10:00', end: '14:00' },
    { dayOfWeek: d, start: '15:00', end: '19:00' },
  ]),
};

const BANK_TRANSFER = {
  accountName: 'PurrCare Sample Account',
  accountNumber: '0000000000000',
  bankName: 'Sample Bank (not real)',
  branchName: 'Dhanmondi (sample)',
  routingNumber: '000000000',
};

const SERVICES = [
  {
    code: 'CONSULT-GEN',
    name: 'General consultation',
    description: 'A full check-up and consultation with one of our vets.',
    category: 'Consultation',
    durationMinutes: 20,
    roles: ['Vet'],
    roomKind: 'Consultation',
    price: 800,
    online: true,
  },
  {
    code: 'VACC-CORE',
    name: 'Vaccination',
    description: 'Core vaccination with a short health check.',
    category: 'Vaccination',
    durationMinutes: 15,
    roles: ['Vet', 'Nurse'],
    roomKind: 'Consultation',
    price: 1200,
    online: true,
  },
  {
    code: 'DENTAL-SCALE',
    name: 'Dental scaling',
    description: 'Scale and polish under general anaesthesia, with a pre-anaesthetic check.',
    category: 'Dental',
    durationMinutes: 60,
    roles: ['Vet'],
    roomKind: 'Theatre',
    price: 3500,
    online: false,
  },
];

const ROOMS = [
  { name: 'Consultation room 1', kind: 'Consultation' },
  { name: 'Treatment room', kind: 'Treatment' },
  { name: 'Theatre', kind: 'Theatre' },
];

const BOARDING_UNITS = [
  {
    code: 'STD-01',
    name: 'Standard suite 1',
    unitType: 'Standard',
    capacityCats: 1,
    block: 'A',
    sortOrder: 1,
  },
  {
    code: 'DLX-01',
    name: 'Deluxe suite 1',
    unitType: 'Deluxe',
    capacityCats: 2,
    block: 'A',
    sortOrder: 2,
  },
];
const BOARDING_RATES = [
  {
    unitType: 'Standard',
    nightlyRate: '800.00',
    weeklyRate: '5000.00',
    monthlyRate: '18000.00',
    additionalCatNightlyRate: null,
  },
  {
    unitType: 'Deluxe',
    nightlyRate: '1500.00',
    weeklyRate: '9500.00',
    monthlyRate: '34000.00',
    additionalCatNightlyRate: '600.00',
  },
];

const BILLING_PROFILE = {
  legalName: 'PurrCare Veterinary Clinic & Shop (sample)',
  taxRegistrationNumber: '000000000-0000',
  addressLines: ['House 34, Road 7, Dhanmondi', 'Dhaka 1209', 'Bangladesh'],
  phone: '+8801711000100',
  email: 'billing@purrcare.test',
  invoiceFooter:
    'Thank you for trusting PurrCare with your cat. Sample data - not a real business.',
};

// ── HTTP with a cookie jar and the CSRF header ───────────────────────────────────────────────────
class ApiError extends Error {
  constructor(method, path, status, body) {
    super(
      `${method} ${path} -> ${status}: ${typeof body === 'string' ? body : JSON.stringify(body)}`.slice(
        0,
        2000,
      ),
    );
    this.status = status;
    this.body = body;
  }
}

class Session {
  constructor(label) {
    this.label = label;
    this.cookies = new Map();
  }

  storeCookies(response) {
    const setCookies =
      typeof response.headers.getSetCookie === 'function' ? response.headers.getSetCookie() : [];
    for (const raw of setCookies) {
      const [pair, ...attrs] = raw.split(';');
      const eq = pair.indexOf('=');
      const name = pair.slice(0, eq).trim();
      const value = pair.slice(eq + 1).trim();
      const expired =
        attrs.some((a) => /^\s*expires=/i.test(a) && Date.parse(a.split('=')[1]) < Date.now()) ||
        attrs.some((a) => /^\s*max-age=0\s*$/i.test(a));
      if (!value || expired) this.cookies.delete(name);
      else this.cookies.set(name, value);
    }
  }

  async request(method, path, body, { allow = [] } = {}) {
    const headers = { Accept: 'application/json' };
    if (this.cookies.size)
      headers.Cookie = [...this.cookies].map(([k, v]) => `${k}=${v}`).join('; ');
    const csrf = this.cookies.get('pc_csrf');
    if (csrf && method !== 'GET') headers['X-CSRF-Token'] = csrf;
    if (body !== undefined) headers['Content-Type'] = 'application/json';

    for (let attempt = 0; ; attempt++) {
      const response = await fetch(API + path, {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
      });
      this.storeCookies(response);
      const text = await response.text();
      let parsed = text;
      try {
        parsed = text ? JSON.parse(text) : null;
      } catch {
        /* not JSON */
      }
      if (response.status === 429 && attempt < 5) {
        await sleep(3000 * (attempt + 1));
        continue;
      }
      if (response.ok || allow.includes(response.status))
        return { status: response.status, body: parsed };
      throw new ApiError(method, path, response.status, parsed);
    }
  }

  get(path, opts) {
    return this.request('GET', path, undefined, opts).then((r) => r.body);
  }
  post(path, body, opts) {
    return this.request('POST', path, body ?? {}, opts).then((r) => r.body);
  }
  put(path, body, opts) {
    return this.request('PUT', path, body ?? {}, opts).then((r) => r.body);
  }
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const anonymous = new Session('anonymous');
const counts = {};
function note(kind, created) {
  counts[kind] ??= { created: 0, existing: 0 };
  counts[kind][created ? 'created' : 'existing']++;
}
function log(message) {
  console.log(`seed-dev: ${message}`);
}
const items = (page) =>
  Array.isArray(page)
    ? page
    : (page?.items ?? page?.addresses ?? page?.results ?? page?.data ?? []);
async function allPages(session, path, pageSize) {
  const out = [];
  for (let page = 1; page < 1000; page++) {
    const sep = path.includes('?') ? '&' : '?';
    const body = await session.get(`${path}${sep}pageNumber=${page}&pageSize=${pageSize}`);
    const batch = items(body);
    out.push(...batch);
    const total = body?.totalCount;
    if (batch.length < pageSize || (typeof total === 'number' && out.length >= total)) break;
  }
  return out;
}
const ci = (a, b) => String(a ?? '').toLowerCase() === String(b ?? '').toLowerCase();

// ── password set via the reset flow, reading the link from the API log ───────────────────────────
function logSize() {
  if (!API_LOG || !existsSync(API_LOG)) return 0;
  return statSync(API_LOG).size;
}

async function waitForResetToken(destination, fromOffset) {
  if (!API_LOG)
    fail(
      `a password has to be set for ${destination}, which needs --api-log <file the API's stdout is redirected to>.`,
    );
  const deadline = Date.now() + 90_000;
  while (Date.now() < deadline) {
    if (existsSync(API_LOG)) {
      const text = readFileSync(API_LOG).subarray(fromOffset).toString('utf8');
      // DevelopmentChannelSender: "NOT SENT -- development sender. Channel: Email. Destination: <dest>. ... reset-password#token=<token> ..."
      const re =
        /NOT SENT -- development sender\. Channel: \w+\. Destination: ([^\s]+)\.[\s\S]*?reset-password#token=([A-Za-z0-9_\-]+)/g;
      let match;
      let token = null;
      while ((match = re.exec(text)) !== null) {
        if (ci(match[1], destination)) token = match[2];
      }
      if (token) return token;
    }
    await sleep(2000);
  }
  fail(
    `no password-reset message for ${destination} appeared in ${API_LOG} within 90 s. Is the API using the Development sender, an https PublicSite:WebBaseUrl, and writing its stdout to that file?`,
  );
}

async function setPasswordViaReset(kind, identifier, password) {
  const offset = logSize();
  const path =
    kind === 'staff'
      ? '/api/staff/auth/password-reset/request'
      : '/api/auth/password-reset/request';
  await anonymous.post(path, { identifier });
  const token = await waitForResetToken(identifier, offset);
  await anonymous.post('/api/auth/password-reset/confirm', { token, newPassword: password });
}

async function signIn(kind, identifier, password) {
  const session = new Session(identifier);
  const path = kind === 'staff' ? '/api/staff/auth/sign-in' : '/api/auth/sign-in';
  const r = await session.request('POST', path, { identifier, password }, { allow: [401] });
  return r.status === 200 ? session : null;
}

async function ensureStaffPassword(identifier) {
  let session = await signIn('staff', identifier, STAFF_PASSWORD);
  if (session) return { session, passwordSet: false };
  await setPasswordViaReset('staff', identifier, STAFF_PASSWORD);
  session = await signIn('staff', identifier, STAFF_PASSWORD);
  if (!session)
    fail(
      `could not sign in as ${identifier} after setting the password (locked out? see the API log).`,
    );
  return { session, passwordSet: true };
}

// ── steps ────────────────────────────────────────────────────────────────────────────────────────
let admin; // Session

async function seedStaff() {
  const adminSpec = STAFF[0];
  const first = await ensureStaffPassword(adminSpec.email);
  admin = first.session;
  log(
    `signed in as ${adminSpec.email}${first.passwordSet ? ' (password set through the reset flow)' : ''}`,
  );

  const ids = {};
  const existing = await allPages(admin, '/api/staff-accounts', 100); // `search` matches the name only
  for (const spec of STAFF) {
    const found = existing.find((s) => ci(s.emailAddress, spec.email));
    let id = found?.id ?? found?.staffAccountId;
    if (!id) {
      const created = await admin.post('/api/staff-accounts', {
        fullName: spec.fullName,
        mobileNumber: spec.mobile,
        emailAddress: spec.email,
        roleIds: spec.roles,
      });
      id = created.staffAccountId;
      note('staff accounts', true);
    } else {
      note('staff accounts', false);
      const roles = found.roleIds ?? found.roles ?? [];
      if (
        spec.key !== 'admin' &&
        (roles.length !== spec.roles.length || !spec.roles.every((r) => roles.includes(r)))
      ) {
        await admin.put(`/api/staff-accounts/${id}/roles`, { roleIds: spec.roles });
      }
    }
    ids[spec.key] = id;
    if (spec.key !== 'admin') await ensureStaffPassword(spec.email);
  }
  return ids;
}

async function seedBranch(staffIds) {
  const branch = await admin.get(`/api/admin/branches/${MAIN_BRANCH_ID}`);
  const needsDetails =
    branch.name !== BRANCH.name ||
    branch.addressLine !== BRANCH.addressLine ||
    branch.phoneNumber !== BRANCH.phoneNumber ||
    branch.email !== BRANCH.email;
  if (needsDetails) {
    await admin.put(`/api/admin/branches/${MAIN_BRANCH_ID}`, {
      name: BRANCH.name,
      timeZoneId: BRANCH.timeZoneId,
      addressLine: BRANCH.addressLine,
      phoneNumber: BRANCH.phoneNumber,
      email: BRANCH.email,
    });
  }
  note('branch details', needsDetails);
  const hours = branch.weeklyHours ?? branch.hours ?? [];
  const sameHours =
    hours.length === BRANCH.hours.length &&
    BRANCH.hours.every((h) =>
      hours.some(
        (x) => x.dayOfWeek === h.dayOfWeek && x.opens === h.opens && x.closes === h.closes,
      ),
    );
  if (!sameHours) {
    await admin.put(`/api/admin/branches/${MAIN_BRANCH_ID}/hours`, { intervals: BRANCH.hours });
    note('branch opening hours', true);
  } else note('branch opening hours', false);

  for (const [key, id] of Object.entries(staffIds)) {
    const current = await admin.get(`/api/admin/staff/${id}/branches`, { allow: [404] });
    const branchIds =
      current?.branchIds ?? (current?.branches ?? []).map((b) => b.branchId ?? b.id);
    if (!branchIds?.includes(MAIN_BRANCH_ID)) {
      await admin.put(`/api/admin/staff/${id}/branches`, {
        branchIds: [MAIN_BRANCH_ID],
        primaryBranchId: MAIN_BRANCH_ID,
      });
      note('staff branch assignments', true);
    } else note('staff branch assignments', false);
    void key;
  }
}

// Tax rates, service prices and boarding rate cards cannot be effective in the past (the API refuses
// a backdated one), so they start a few seconds from now. SKU prices allow 60 s of backdating.
const soon = () => new Date(Date.now() + 3000).toISOString();
const justNow = () => new Date(Date.now() - 30_000).toISOString();

async function seedTax() {
  const classes = await admin.get('/api/admin/tax-classes');
  let standard = classes.find((c) => ci(c.code, 'STANDARD'));
  if (!standard) {
    const created = await admin.post('/api/admin/tax-classes', {
      code: 'STANDARD',
      name: 'Standard',
    });
    standard = { id: created.taxClassId ?? created.id };
    note('tax classes', true);
  } else note('tax classes', false);

  const ensureZeroRate = async (taxClassId, label) => {
    const rates = items(await admin.get(`/api/admin/tax-classes/${taxClassId}/rates`));
    if (rates.length === 0) {
      await admin.post(`/api/admin/tax-classes/${taxClassId}/rates`, {
        ratePercent: 0,
        effectiveFrom: soon(),
      });
      note(`tax rates (${label})`, true);
      return true;
    }
    note(`tax rates (${label})`, false);
    return false;
  };
  const createdStandardRate = await ensureZeroRate(standard.id, 'Standard 0%');
  const delivery = classes.find((c) => ci(c.code, 'DELIVERY'));
  if (!delivery)
    fail('the DELIVERY tax class the migrations insert is missing - is the database migrated?');
  await ensureZeroRate(delivery.id, 'Delivery 0%');
  if (createdStandardRate) await sleep(3500);
  return { standardTaxClassId: standard.id, deliveryTaxClassId: delivery.id };
}

async function seedOrigins(list = PRODUCTS) {
  const wanted = [...new Set(list.map((p) => p.origin).filter(Boolean))];
  const notes = {
    TH: 'Thailand: Whiskas and Me-O dry and wet food.',
    FR: 'France: Royal Canin breed and health nutrition.',
    MY: 'Malaysia: Purina Friskies.',
    DE: 'Germany: Catsan hygiene litter.',
    SG: 'Singapore: Kit Cat litter and accessories.',
    CN: 'China: Haisenpet and Kitchen Flavor dry food.',
  };
  const existing = items(await admin.get('/api/staff/shop/origins'));
  let order = 1;
  for (const code of wanted) {
    if (existing.some((o) => ci(o.countryCode ?? o.code, code))) {
      note('sourcing origins', false);
      order++;
      continue;
    }
    await admin.post('/api/staff/shop/origins', {
      countryCode: code,
      sourcingNotes: notes[code] ?? null,
      isFeatured: true,
      sortOrder: order++,
    });
    note('sourcing origins', true);
  }
}

async function getProduct(id) {
  return admin.get(`/api/staff/shop/products/${id}`);
}

async function seedCatalogue(standardTaxClassId, list = PRODUCTS) {
  const all = await allPages(admin, '/api/staff/shop/products', 100);
  const skuIds = [];
  for (const spec of list) {
    // The staff list carries no slug; the name is unique within this seed (the detail is checked below).
    const summary = all.find((p) => ci(p.name, spec.name) && !ci(p.status, 'Archived'));
    let id = summary?.productId;
    if (!id) {
      const created = await admin.post('/api/staff/shop/products', {
        kind: 'Standard',
        name: spec.name,
        brand: spec.brand,
        category: spec.category,
        subcategory: spec.subcategory ?? null,
        taxClassId: standardTaxClassId,
        slug: spec.slug,
      });
      id = created.productId ?? created.id;
      note('products', true);
    } else note('products', false);

    let product = await getProduct(id);

    if (!product.description || (product.countryOfOriginCode ?? null) !== (spec.origin ?? null)) {
      await admin.put(`/api/staff/shop/products/${id}`, {
        expectedVersion: product.version,
        name: spec.name,
        brand: spec.brand,
        description: spec.description,
        category: spec.category,
        subcategory: spec.subcategory ?? null,
        tags: [],
        taxClassId: standardTaxClassId,
        countryOfOriginCode: spec.origin ?? null,
        listedOnStorefront: true,
        isExpiryTracked: !!spec.expiry,
        healthFocus: spec.healthFocus ?? [],
      });
      product = await getProduct(id);
    }

    if (spec.food && !product.food?.lifeStage) {
      const known = !spec.food.labelUnknown;
      const [protein, fat, fibre, moisture] = known ? spec.food.analysis : [];
      await admin.put(`/api/staff/shop/products/${id}/food`, {
        expectedVersion: product.version,
        manufacturer: known ? spec.food.manufacturer : null,
        lifeStage: spec.food.lifeStage,
        proteinSource: spec.food.proteinSource,
        ingredientList: known ? ingredientsFor(spec) : null,
        guaranteedAnalysis: known
          ? {
              proteinPercent: protein,
              fatPercent: fat,
              fibrePercent: fibre,
              moisturePercent: moisture,
            }
          : null,
        feedingGuide:
          'Follow the feeding table on the pack and adjust to keep your cat at a healthy weight. Always provide fresh water.',
        storageInstructions: FOOD_LABEL.storageInstructions,
      });
      product = await getProduct(id);
    }

    for (const sku of spec.skus) {
      let existing = (product.skus ?? []).find((s) => ci(s.code, sku.code));
      if (!existing) {
        await admin.post(`/api/staff/shop/products/${id}/skus`, {
          expectedVersion: product.version,
          code: sku.code,
          barcode: null,
          sizeOrWeight: sku.size ?? null,
          flavour: null,
          colour: sku.colour ?? null,
          packQuantity: sku.pack ?? null,
          shippingWeightGrams: sku.grams ?? null,
        });
        product = await getProduct(id);
        existing = product.skus.find((s) => ci(s.code, sku.code));
        note('SKUs (variants)', true);
      } else note('SKUs (variants)', false);
      const skuId = existing.id ?? existing.skuId;
      const history = await admin.get(`/api/staff/shop/skus/${skuId}/prices`);
      if (!history?.current && !(history?.scheduled ?? []).length) {
        await admin.post(`/api/staff/shop/skus/${skuId}/prices`, {
          amount: sku.price,
          currencyCode: 'BDT',
          effectiveFrom: justNow(),
        });
        note('SKU prices', true);
      } else note('SKU prices', false);
      skuIds.push({
        skuId,
        code: sku.code,
        expiry: !!spec.expiry,
        price: sku.price,
        productSlug: spec.slug,
      });
    }

    if (!ci(product.status, 'Published') && !ci(product.status, 'Active')) {
      await admin.post(`/api/staff/shop/products/${id}/publish`, {
        expectedVersion: product.version,
      });
      note('products published', true);
    } else note('products published', false);
  }
  return skuIds;
}

async function seedSupplier() {
  const suppliers = await allPages(admin, '/api/staff/procurement/suppliers', 100);
  const found = suppliers.find((s) => ci(s.code, SUPPLIER.code));
  if (found) {
    note('suppliers', false);
    return found.id ?? found.supplierId;
  }
  const created = await admin.post('/api/staff/procurement/suppliers', SUPPLIER);
  note('suppliers', true);
  return created.supplierId ?? created.id;
}

async function onHandAt(skuId) {
  const matrix = await admin.get(`/api/staff/inventory/stock/${skuId}`);
  return matrix;
}

const STOCK_QUANTITY = 24;
const LOT_PREFIX = 'SEED-2610';

async function seedStock(supplierId, skus, reference = 'PAWS-DN-0001', lotPrefix = LOT_PREFIX) {
  const missing = [];
  for (const sku of skus) {
    const matrix = await onHandAt(sku.skuId);
    const onHand = stockTotal(matrix);
    if (onHand > 0) note('SKUs with opening stock', false);
    else missing.push(sku);
  }
  if (missing.length === 0) return;
  // A large import (2026-10-16: the AmarPet cat section) is received in batches of 50 lines per order.
  for (let start = 0, n = 1; start < missing.length; start += 50, n++) {
    const batch = missing.slice(start, start + 50);
    await receiveOpeningStock(
      supplierId,
      batch,
      missing.length > 50 ? `${reference}-${n}` : reference,
      missing.length > 50 ? `${lotPrefix}-${n}` : lotPrefix,
    );
  }
}

async function receiveOpeningStock(supplierId, missing, reference, lotPrefix) {
  const today = new Date();
  const expected = new Date(today.getTime() + 24 * 3600 * 1000).toISOString().slice(0, 10);
  const po = await admin.post('/api/staff/procurement/purchase-orders', {
    supplierId,
    deliverToLocationId: SHOP_FLOOR_LOCATION_ID,
    currencyCode: 'BDT',
    expectedDeliveryOn: expected,
    notes: 'Opening stock (dev seed).',
    lines: missing.map((s) => ({
      skuId: s.skuId,
      orderedQuantity: STOCK_QUANTITY,
      unitCostAmount: Math.round(s.price * 0.7),
    })),
  });
  const poId = po.purchaseOrderId ?? po.id;
  note('purchase orders', true);
  await admin.post(`/api/staff/procurement/purchase-orders/${poId}/issue`);
  const detail = await admin.get(`/api/staff/procurement/purchase-orders/${poId}`);
  const lines = detail.lines ?? [];

  const receiptLines = missing.map((s, i) => {
    const line = lines.find((l) => (l.skuId ?? l.sku?.skuId) === s.skuId);
    return {
      skuId: s.skuId,
      purchaseOrderLineId: line?.lineId ?? line?.id ?? line?.purchaseOrderLineId ?? null,
      lotNumber: `${lotPrefix}-${String(i + 1).padStart(2, '0')}`,
      manufacturedOn: s.expiry ? '2026-09-01' : null,
      expiresOn: s.expiry ? '2027-09-30' : null,
      quantityAccepted: STOCK_QUANTITY,
      quantityRejected: 0,
      locationId: SHOP_FLOOR_LOCATION_ID,
      holdForInspection: false,
      unitCostAmount: Math.round(s.price * 0.7),
      unitCostCurrency: 'BDT',
    };
  });
  await admin.post('/api/staff/inventory/goods-receipts', {
    supplierId,
    purchaseOrderId: poId,
    deliveryReference: reference,
    lines: receiptLines,
  });
  note('goods receipts', true);
  for (let i = 0; i < missing.length; i++) note('SKUs with opening stock', true);
}

function stockTotal(matrix) {
  // The matrix endpoint returns per-location rows; sum whatever on-hand figure it carries.
  let total = 0;
  const visit = (node) => {
    if (Array.isArray(node)) {
      node.forEach(visit);
      return;
    }
    if (node && typeof node === 'object') {
      for (const [k, v] of Object.entries(node)) {
        if (/^onHand(Quantity)?$/i.test(k) && (typeof v === 'number' || typeof v === 'string'))
          total += Number(v) || 0;
        else if (typeof v === 'object') visit(v);
      }
    }
  };
  visit(matrix);
  return total;
}

const firstId = (obj, ...keys) => {
  if (typeof obj === 'string') return obj;
  for (const k of keys) if (obj?.[k]) return obj[k];
  return obj?.id;
};

async function seedContent() {
  // One published Shop banner.
  const banners = items(await admin.get('/api/admin/content?kind=Banner'));
  let banner = banners.find((b) => ci(b.slug, BANNER.slug));
  if (!banner) {
    const created = await admin.post('/api/admin/content', {
      kind: 'Banner',
      slug: BANNER.slug,
      title: BANNER.title,
      draftBody: BANNER.body,
      placement: BANNER.placement,
      linkUrl: BANNER.linkUrl,
      visibleFrom: null,
      visibleTo: null,
      sortOrder: 1,
    });
    banner = { id: firstId(created, 'contentItemId') };
    note('banners', true);
  } else note('banners', false);
  await publishContentIfNeeded(firstId(banner, 'contentItemId'), 'banners');

  // Three FAQs.
  const faqs = items(await admin.get('/api/admin/content?kind=Faq'));
  for (const spec of FAQS) {
    let faq = faqs.find((f) => ci(f.slug, spec.slug));
    if (!faq) {
      const created = await admin.post('/api/admin/content', {
        kind: 'Faq',
        slug: spec.slug,
        title: spec.title,
        draftBody: spec.body,
        placement: null,
        linkUrl: null,
        visibleFrom: null,
        visibleTo: null,
        sortOrder: spec.sortOrder,
      });
      faq = { id: firstId(created, 'contentItemId') };
      note('FAQs', true);
    } else note('FAQs', false);
    await publishContentIfNeeded(firstId(faq, 'contentItemId'), 'FAQs');
  }

  // Privacy (requires acceptance) and Terms (does not).
  const policies = items(await admin.get('/api/admin/policies'));
  for (const spec of POLICIES) {
    let policy = policies.find((p) => ci(p.key, spec.key));
    if (!policy) {
      await admin.post('/api/admin/policies', {
        key: spec.key,
        title: spec.title,
        requiresAcceptance: spec.requiresAcceptance,
      });
      note('policies', true);
      policy = { key: spec.key };
    } else note('policies', false);
    const published =
      policy.currentVersionNumber ??
      policy.currentVersion ??
      policy.latestVersionNumber ??
      (policy.versions ?? []).length;
    if (!published) {
      await admin.post(`/api/admin/policies/${encodeURIComponent(spec.key)}/versions`, {
        body: spec.body,
        changeSummary: spec.changeSummary,
      });
      note('policy versions published', true);
    } else note('policy versions published', false);
  }
}

async function publishContentIfNeeded(id, label) {
  const item = await admin.get(`/api/admin/content/${id}`);
  const isPublished =
    item.isPublished ?? (item.status ? ci(item.status, 'Published') : !!item.publishedBody);
  if (!isPublished) {
    await admin.post(`/api/admin/content/${id}/publish`);
    note(`${label} published`, true);
  } else note(`${label} published`, false);
}

async function seedDelivery() {
  const zones = items(await admin.get('/api/admin/delivery-zones'));
  const body = {
    branchId: MAIN_BRANCH_ID,
    name: DELIVERY_ZONE.name,
    priority: 1,
    isActive: true,
    codEnabled: true,
    codMaxOrderValue: null,
    coverage: [{ district: DELIVERY_ZONE.district, postalCodes: [] }],
    services: [
      {
        serviceLevel: 'Standard',
        isActive: true,
        baseCharge: DELIVERY_ZONE.standard.baseCharge,
        freeOverOrderValue: DELIVERY_ZONE.standard.freeOver,
        leadTimeDays: DELIVERY_ZONE.standard.leadTimeDays,
        weightBands: [],
        slotWindows: DELIVERY_ZONE.windows,
      },
    ],
  };
  if (zones.some((z) => ci(z.name, DELIVERY_ZONE.name))) {
    note('delivery zones', false);
  } else {
    await admin.post('/api/admin/delivery-zones', body);
    note('delivery zones', true);
  }

  const bank = await admin.request('GET', '/api/admin/bank-transfer-account', undefined, {
    allow: [404],
  });
  if (bank.status === 404 || !bank.body || !bank.body.accountNumber) {
    await admin.put('/api/admin/bank-transfer-account', { ...BANK_TRANSFER, isActive: true });
    note('bank-transfer account', true);
  } else note('bank-transfer account', false);
}

async function seedBillingProfile() {
  const current = await admin.request(
    'GET',
    `/api/admin/billing-profiles/${MAIN_BRANCH_ID}`,
    undefined,
    { allow: [404] },
  );
  if (current.status === 404 || !current.body?.legalName) {
    await admin.put(`/api/admin/billing-profiles/${MAIN_BRANCH_ID}`, {
      ...BILLING_PROFILE,
      bankAccounts: [],
    });
    note('branch billing profile', true);
  } else note('branch billing profile', false);
}

async function seedRoomsAndServices(standardTaxClassId) {
  const rooms = items(await admin.get(`/api/admin/branches/${MAIN_BRANCH_ID}/rooms`));
  for (const room of ROOMS) {
    if (rooms.some((r) => ci(r.name, room.name))) {
      note('rooms', false);
      continue;
    }
    await admin.post(`/api/admin/branches/${MAIN_BRANCH_ID}/rooms`, room);
    note('rooms', true);
  }

  const services = items(await admin.get('/api/admin/services'));
  for (const spec of SERVICES) {
    let service = services.find((s) => ci(s.code, spec.code));
    let id = firstId(service, 'serviceId');
    if (!service) {
      const created = await admin.post('/api/admin/services', {
        code: spec.code,
        name: spec.name,
        description: spec.description,
        category: spec.category,
        durationMinutes: spec.durationMinutes,
        qualifiedRoleIds: spec.roles,
        requiredRoomKind: spec.roomKind,
        taxClassId: standardTaxClassId,
        isBookableOnline: spec.online,
      });
      id = firstId(created, 'serviceId');
      note('clinic services', true);
    } else note('clinic services', false);
    const prices = items(await admin.get(`/api/admin/services/${id}/prices`));
    if (prices.length === 0) {
      await admin.post(`/api/admin/services/${id}/prices`, {
        amount: spec.price.toFixed(2),
        currencyCode: 'BDT',
        effectiveFrom: soon(),
      });
      note('service prices', true);
    } else note('service prices', false);
  }
}

async function seedBoarding(standardTaxClassId) {
  const units = items(await admin.get('/api/staff/boarding/units'));
  for (const unit of BOARDING_UNITS) {
    if (units.some((u) => ci(u.code, unit.code))) {
      note('boarding units', false);
      continue;
    }
    await admin.post('/api/staff/boarding/units', unit);
    note('boarding units', true);
  }
  for (const rate of BOARDING_RATES) {
    const cards = items(
      await admin.get(`/api/staff/boarding/rate-cards?unitType=${rate.unitType}`),
    );
    if (cards.length) {
      note('boarding rate cards', false);
      continue;
    }
    await admin.post('/api/staff/boarding/rate-cards', {
      ...rate,
      taxClassId: standardTaxClassId,
      effectiveFrom: soon(),
    });
    note('boarding rate cards', true);
  }
}

async function seedCustomer() {
  let session = await signIn('customer', CUSTOMER.email, CUSTOMER_PASSWORD);
  if (!session) {
    const captcha = await anonymous.post('/api/captcha');
    const r = await anonymous.request(
      'POST',
      '/api/customers',
      {
        fullName: CUSTOMER.fullName,
        mobileNumber: CUSTOMER.mobile,
        emailAddress: CUSTOMER.email,
        password: CUSTOMER_PASSWORD,
        captchaId: captcha.captchaId,
        captchaAnswer: CAPTCHA_ANSWER,
      },
      { allow: [400, 409] },
    );
    if (r.status === 201) note('customers', true);
    else {
      // Already registered with a different password: reset it.
      note('customers', false);
      await setPasswordViaReset('customer', CUSTOMER.email, CUSTOMER_PASSWORD);
    }
    session = await signIn('customer', CUSTOMER.email, CUSTOMER_PASSWORD);
    if (!session) fail(`could not sign in as ${CUSTOMER.email} after registering it.`);
  } else note('customers', false);

  const { customerId } = await session.get('/api/auth/session');

  const addresses = items(await session.get('/api/customers/me/addresses'));
  let address = addresses.find((a) => ci(a.label, CUSTOMER.address.label));
  if (!address) {
    const created = await session.post('/api/customers/me/addresses', CUSTOMER.address);
    address = { addressId: created.addressId };
    note('customer addresses', true);
  } else note('customer addresses', false);

  const cats = items(await session.get('/api/cats')); // a customer only ever sees their own cats
  if (!cats.some((c) => ci(c.name, CUSTOMER.cat.name))) {
    await session.post('/api/cats', { ownerId: customerId, species: 'Cat', ...CUSTOMER.cat });
    note('cats', true);
  } else note('cats', false);
  return { customerId, addressId: firstId(address, 'addressId') };
}

function ingredientsFor(spec) {
  switch (spec.food.proteinSource) {
    case 'Chicken':
      return 'Dehydrated poultry protein, rice, maize, animal fats, vegetable protein isolate, beet pulp, fish oil, minerals, vitamins, taurine.';
    case 'Tuna':
      return 'Tuna (min. 4%), meat and animal derivatives, cereals, minerals, sugars, vitamins, taurine.';
    case 'Salmon':
      return 'Salmon (min. 25%), chicken, tapioca starch, fish oil, cellulose, vitamins, taurine.';
    default:
      return 'Cereals, fish and fish derivatives (min. 4% ocean fish), meat and animal derivatives, vegetable protein extracts, oils and fats, minerals, vitamins, taurine.';
  }
}

// ── --verify: read-only checks through the API as the seeded people (the cart is emptied after) ──
const money = (m) => Number(m?.amount ?? m ?? NaN);

async function verify() {
  const results = [];
  const check = (name, ok, detail) => {
    results.push({ name, ok, detail });
    console.log(`  ${ok ? 'PASS' : 'FAIL'}  ${name}${detail ? ` - ${detail}` : ''}`);
  };
  log(`verifying against ${API}`);

  const staff = await signIn('staff', STAFF[0].email, STAFF_PASSWORD);
  check('staff sign-in as e2e.admin', !!staff);
  for (const s of STAFF.slice(1))
    check(`staff sign-in as ${s.email}`, !!(await signIn('staff', s.email, STAFF_PASSWORD)));

  const banners = await anonymous.get('/api/content/banners?placement=Shop');
  check(
    'exactly one Shop banner',
    Array.isArray(banners) && banners.length === 1,
    `${banners.length} banner(s): ${banners.map((b) => b.title).join(' | ')}`,
  );
  const faqs = await anonymous.get('/api/content/faqs');
  check('three FAQs', faqs.length === FAQS.length, `${faqs.length}`);
  const terms = await anonymous.request('GET', '/api/policies/terms', undefined, { allow: [404] });
  check('terms policy published', terms.status === 200, `v${terms.body?.versionNumber}`);

  const filters = await anonymous.get('/api/shop/filters');
  const brands = (filters.brands ?? []).map((b) => b.name).sort();
  const expectedBrands = [...new Set(PRODUCTS.map((p) => p.brand))].sort();
  check(
    'shop filter brands are exactly the seeded brands',
    JSON.stringify(brands) === JSON.stringify(expectedBrands),
    brands.join(', '),
  );
  const listing = await anonymous.get('/api/shop/products?pageSize=48');
  const listed = items(listing);
  check(
    'all seeded products listed in the shop',
    listed.length === PRODUCTS.length,
    `${listed.length} listed, availability: ${[...new Set(listed.map((p) => p.availability))].join(', ')}`,
  );

  const customer = await signIn('customer', CUSTOMER.email, CUSTOMER_PASSWORD);
  check('customer sign-in', !!customer);
  if (!customer) return finish(results);
  const pending = await customer.get('/api/customers/me/policy-acceptances/pending');
  check(
    // Nothing but privacy may be pending; on a database already used, she may have accepted it (then none is).
    'pending policies for the customer = privacy only (or none, once accepted)',
    pending.every((p) => p.policyKey === 'privacy'),
    pending.map((p) => p.policyKey).join(', ') || 'none (already accepted)',
  );

  const { addresses } = await customer.get('/api/customers/me/addresses');
  const address = addresses.find((a) => ci(a.label, CUSTOMER.address.label));
  check(
    'customer has the Dhaka address',
    !!address && address.district === 'Dhaka',
    address ? `${address.line1}, ${address.city} ${address.postalCode}` : 'missing',
  );
  const cats = items(await customer.get('/api/cats'));
  check(
    'customer has Mishti',
    cats.some((c) => c.name === CUSTOMER.cat.name),
    cats.map((c) => c.name).join(', '),
  );

  // Two products in the cart, then quotes below and above the free-delivery threshold.
  const pick = async (slug, code) => {
    const detail = await anonymous.get(`/api/shop/products/${slug}`);
    const sku = (detail.skus ?? []).find((s) => ci(s.code, code));
    return { skuId: sku?.skuId ?? sku?.id, price: money(sku?.price ?? sku?.currentPrice) };
  };
  const a = await pick('whiskas-adult-ocean-fish-dry', 'WHI-OF-1K2');
  const b = await pick('catsan-hygiene-plus-litter', 'CAT-HP-5L');
  await clearCart(customer);
  const lineA = await customer.post('/api/cart/lines', { skuId: a.skuId, quantity: 1 });
  await customer.post('/api/cart/lines', { skuId: b.skuId, quantity: 1 });

  const quote = async () =>
    customer.get(
      `/api/customers/me/checkout/quote?fulfilment=Delivery&addressId=${address.id}&serviceLevel=Standard&paymentMethod=CashOnDelivery`,
    );
  const describe = (q) => {
    const lines = q.lines ?? q.items ?? [];
    const excluded =
      q.excludedLines ?? lines.filter((l) => l.excluded || l.isExcluded || l.exclusionReason);
    return {
      lines,
      excluded,
      subtotal: money(q.subtotal ?? q.totals?.subtotal),
      delivery: money(q.deliveryCharge ?? q.delivery?.charge ?? q.totals?.deliveryCharge),
      tax: money(q.taxTotal ?? q.tax ?? q.totals?.tax ?? 0),
      total: money(q.grandTotal ?? q.total ?? q.totals?.grandTotal),
    };
  };

  const q1 = describe(await quote());
  const expectedSubtotal = a.price + b.price;
  check(
    'quote: 2 lines priced, none excluded',
    q1.lines.length === 2 && q1.excluded.length === 0,
    `${q1.lines.length} line(s), ${q1.excluded.length} excluded`,
  );
  check(
    `quote: subtotal ${expectedSubtotal}, delivery 60, total ${expectedSubtotal + 60}`,
    q1.subtotal === expectedSubtotal && q1.delivery === 60 && q1.total === expectedSubtotal + 60,
    `subtotal ${q1.subtotal}, delivery ${q1.delivery}, tax ${q1.tax}, total ${q1.total}`,
  );

  await customer.put(`/api/cart/lines/${lineA.lineId}`, { quantity: 2 });
  const q2 = describe(await quote());
  const expected2 = a.price * 2 + b.price;
  check(
    `quote over ৳2,000: subtotal ${expected2}, delivery 0, total ${expected2}`,
    q2.subtotal === expected2 && q2.delivery === 0 && q2.total === expected2,
    `subtotal ${q2.subtotal}, delivery ${q2.delivery}, tax ${q2.tax}, total ${q2.total}`,
  );
  await clearCart(customer);
  return finish(results);
}

async function clearCart(session) {
  const cart = await session.get('/api/cart');
  for (const line of cart.lines ?? [])
    await session.request('DELETE', `/api/cart/lines/${line.lineId ?? line.id}`, undefined, {
      allow: [404],
    });
}

function finish(results) {
  const failed = results.filter((r) => !r.ok);
  log(failed.length ? `${failed.length} check(s) FAILED` : `all ${results.length} checks passed`);
  if (failed.length) process.exitCode = 1;
}

// ── main ─────────────────────────────────────────────────────────────────────────────────────────
async function seedExtraOnly() {
  if (EXTRA_PRODUCTS.length === 0)
    fail('--only-extra needs --extra <file.json> with a "products" list.');
  const first = await ensureStaffPassword(STAFF[0].email);
  admin = first.session;
  log(`adding ${EXTRA_PRODUCTS.length} extra products through ${API}`);
  const { standardTaxClassId } = await seedTax();
  await seedOrigins(EXTRA_PRODUCTS);
  const skus = await seedCatalogue(standardTaxClassId, EXTRA_PRODUCTS);
  const supplierId = await seedSupplier();
  await seedStock(supplierId, skus, 'PAWS-DN-EXTRA-0001', 'EXTRA-2610');
  log('done. Summary (created / already there):');
  for (const [kind, c] of Object.entries(counts))
    console.log(`  ${kind.padEnd(32)} ${String(c.created).padStart(3)} / ${c.existing}`);
}

async function main() {
  if (args.verify) return verify();
  if (args['only-extra']) return seedExtraOnly();
  log(`seeding through ${API}`);
  const staffIds = await seedStaff();
  await seedBranch(staffIds);
  const { standardTaxClassId } = await seedTax();
  await seedOrigins();
  const skus = await seedCatalogue(standardTaxClassId);
  const supplierId = await seedSupplier();
  await seedStock(supplierId, skus);
  await seedContent();
  await seedDelivery();
  await seedBillingProfile();
  await seedRoomsAndServices(standardTaxClassId);
  await seedBoarding(standardTaxClassId);
  await seedCustomer();

  log('done. Summary (created / already there):');
  for (const [kind, c] of Object.entries(counts))
    console.log(`  ${kind.padEnd(32)} ${String(c.created).padStart(3)} / ${c.existing}`);
}

main().catch((err) => {
  console.error(`seed-dev: FAILED: ${err.message}`);
  process.exit(1);
});
