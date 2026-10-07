import { http, HttpResponse } from 'msw';
import { addDays, addMonths, format, subMonths } from 'date-fns';
import type {
  CatProfile,
  CatListItem,
  CatVaccinationStatus,
  CatVaccinationStatusItem,
  VaccinationStatus,
  ParasiteTreatmentType,
} from '@/lib/api/cats';
import type { VaccineListItem } from '@/lib/api/vaccines';
import { CAT_PHOTO_RULES } from '@/lib/catPhotoRules';

/**
 * Cat Profiles MSW request handlers — all 20 endpoints in HO-CAT-001 §12.4 (HO-CAT-002 §12.5).
 * Derived from that contract as verified against the real implementation in `src/` (query
 * handlers, controllers, validators) — never invented (§5.1, §20.2). An in-memory store backs
 * every handler so a register/update/record flow behaves like the real API across a test run;
 * `resetCatProfilesMockData()` puts it back to the seeded fixtures for test isolation.
 *
 * Fixture IDs are exported so unit and e2e specs can navigate straight to a known scenario
 * (e.g. the high-risk cat, the deceased cat) without re-deriving them.
 *
 * Every path pattern below starts with a leading `*` before `/api/...` — MSW's wildcard-origin
 * form — rather than a bare `/api/...` path, so a request matches regardless of
 * `NEXT_PUBLIC_API_BASE_URL`. MSW resolves a bare relative path against the current
 * `location.href`, which is fine in a browser but not in Vitest's jsdom environment or a
 * Playwright test hitting a real dev server on a different origin.
 */

// -------------------------------------------------------------------------------------------
// Fixture identifiers
// -------------------------------------------------------------------------------------------

export const catProfilesFixtureIds = {
  ownerWithCats: '10000000-0000-0000-0000-000000000001',
  paginationOwner: '10000000-0000-0000-0000-000000000002',
  otherOwner: '10000000-0000-0000-0000-000000000003',

  catWithAllergiesExactDob: '20000000-0000-0000-0000-000000000001', // Whiskers
  catNoAllergiesEstimatedDob: '20000000-0000-0000-0000-000000000002', // Biscuit — DueSoon
  catHighRiskOverdue: '20000000-0000-0000-0000-000000000003', // Tiger
  catCurrentPlain: '20000000-0000-0000-0000-000000000004', // Mittens
  catDeceased: '20000000-0000-0000-0000-000000000005', // Ash
  catConflict: '20000000-0000-0000-0000-0000000000c0', // always 409s once, then succeeds
  catServerError: '20000000-0000-0000-0000-000000000500', // GET always 500s

  vaccineRabies: '30000000-0000-0000-0000-000000000001', // core, 36 months
  vaccineFvrcp: '30000000-0000-0000-0000-000000000002', // core, 12 months
  vaccineFelv: '30000000-0000-0000-0000-000000000003', // non-core
} as const;

// -------------------------------------------------------------------------------------------
// Store shapes — a superset of the read-side view models, since the store also has to answer
// GET .../weights, GET .../vaccination-status and feed the (fake) PDF/QR binary endpoints.
// -------------------------------------------------------------------------------------------

interface StoredVaccination {
  id: string;
  vaccineId: string;
  vaccineCode: string;
  vaccineName: string;
  isCore: boolean;
  batchReference: string;
  batchExpiresOn: string;
  administeredOn: string;
  administeredByVetId: string;
  nextDueOn: string;
  notes: string | null;
}

interface StoredParasiteTreatment {
  id: string;
  treatmentType: ParasiteTreatmentType;
  productName: string;
  batchReference: string | null;
  administeredOn: string;
  administeredByStaffId: string;
  nextDueOn: string;
  notes: string | null;
}

interface StoredWeight {
  id: string;
  recordedOn: string;
  kilograms: number;
  recordedByStaffId: string;
  notes: string | null;
}

type StoredCat = Omit<CatProfile, 'latestWeightKg' | 'hasRecordedAllergies'> & {
  vaccinations: StoredVaccination[];
  parasiteTreatments: StoredParasiteTreatment[];
  weights: StoredWeight[];
};

const today = () => new Date();
const iso = (date: Date) => format(date, 'yyyy-MM-dd');

function dateOnlyAddMonths(dateOnly: string, months: number): string {
  return iso(addMonths(new Date(`${dateOnly}T00:00:00`), months));
}

function seedVaccineCatalog(): VaccineListItem[] {
  return [
    {
      id: catProfilesFixtureIds.vaccineRabies,
      code: 'RABIES',
      name: 'Rabies',
      isCore: true,
      validityMonths: 36,
      isActive: true,
    },
    {
      id: catProfilesFixtureIds.vaccineFvrcp,
      code: 'FVRCP',
      name: 'Feline Viral Rhinotracheitis, Calicivirus & Panleukopenia',
      isCore: true,
      validityMonths: 12,
      isActive: true,
    },
    {
      id: catProfilesFixtureIds.vaccineFelv,
      code: 'FELV',
      name: 'Feline Leukaemia',
      isCore: false,
      validityMonths: 12,
      isActive: true,
    },
    // 20 more so the vaccine catalogue's `GET /api/vaccines` (pageSize 20) has a real second page
    // to demonstrate pagination (CODE_REVIEW gate MEDIUM finding on HO-CAT-002 — `VaccineListPage`
    // wasn't reading the URL's `page` param). Named to sort after the three real vaccines above
    // (`Zz` beats every real vaccine name), same convention as `seedCats`'s 25 pagination cats.
    ...Array.from({ length: 20 }, (_, index) => {
      const n = index + 1;
      return {
        id: `30000000-0000-0000-0000-${String(100 + n).padStart(12, '0')}`,
        code: `ZZPAG${String(n).padStart(2, '0')}`,
        name: `Zz Pagination Vaccine ${String(n).padStart(2, '0')}`,
        isCore: false,
        validityMonths: 12,
        isActive: true,
      };
    }),
  ];
}

function vaccination(
  vaccine: VaccineListItem,
  administeredMonthsAgo: number,
  batchReference: string,
  extraDaysAgo = 0,
): StoredVaccination {
  const administeredOn = iso(addDays(subMonths(today(), administeredMonthsAgo), -extraDaysAgo));
  return {
    id: crypto.randomUUID(),
    vaccineId: vaccine.id,
    vaccineCode: vaccine.code,
    vaccineName: vaccine.name,
    isCore: vaccine.isCore,
    batchReference,
    batchExpiresOn: iso(addMonths(today(), 12)),
    administeredOn,
    administeredByVetId: catProfilesFixtureIds.ownerWithCats, // any staff-shaped guid — display only
    nextDueOn: dateOnlyAddMonths(administeredOn, vaccine.validityMonths),
    notes: null,
  };
}

function seedCats(vaccineCatalog: VaccineListItem[]): Map<string, StoredCat> {
  const rabies = vaccineCatalog[0]!;
  const fvrcp = vaccineCatalog[1]!;
  const cats = new Map<string, StoredCat>();

  // Whiskers — allergies, exact DOB, several photos, Current vaccination status.
  cats.set(catProfilesFixtureIds.catWithAllergiesExactDob, {
    id: catProfilesFixtureIds.catWithAllergiesExactDob,
    ownerId: catProfilesFixtureIds.ownerWithCats,
    name: 'Whiskers',
    species: 'Cat',
    sex: 'Female',
    dateOfBirth: '2021-03-14',
    dateOfBirthPrecision: 'Exact',
    breed: 'British Shorthair',
    colourAndMarkings: 'Blue, white chest patch',
    neuterStatus: 'Neutered',
    neuteredOn: '2021-09-01',
    isHighRisk: false,
    handlingNote: null,
    lifeStatus: 'Alive',
    deceasedOn: null,
    deceasedNote: null,
    dietaryNotes: 'Prescription renal diet only.',
    behaviouralNotes: 'Hides at the back of the carrier; give her a minute before handling.',
    allergies: [
      {
        id: crypto.randomUUID(),
        allergen: 'Chicken protein',
        reaction: 'Vomiting and loose stool within a few hours',
        severity: 'Moderate',
        recordedOn: iso(subMonths(today(), 8)),
      },
    ],
    chronicConditions: [
      {
        id: crypto.randomUUID(),
        condition: 'Chronic kidney disease (stage 2)',
        diagnosedOn: iso(subMonths(today(), 6)),
        notes: 'Rechecked every 3 months.',
      },
    ],
    medications: [
      {
        id: crypto.randomUUID(),
        medicationName: 'Benazepril',
        dosage: '2.5 mg',
        frequency: 'Once daily',
        startedOn: iso(subMonths(today(), 6)),
        endsOn: null,
      },
    ],
    photos: [
      {
        id: crypto.randomUUID(),
        storageKey: 'dev/whiskers-1.jpg',
        contentType: 'image/jpeg',
        sizeInBytes: 812_000,
        fileName: 'whiskers-front.jpg',
        isPrimary: true,
        uploadedOn: subMonths(today(), 4).toISOString(),
      },
      {
        id: crypto.randomUUID(),
        storageKey: 'dev/whiskers-2.jpg',
        contentType: 'image/jpeg',
        sizeInBytes: 640_000,
        fileName: 'whiskers-side.jpg',
        isPrimary: false,
        uploadedOn: subMonths(today(), 2).toISOString(),
      },
    ],
    vaccinations: [vaccination(rabies, 6, 'RB-2201'), vaccination(fvrcp, 2, 'FV-9910')],
    parasiteTreatments: [
      {
        id: crypto.randomUUID(),
        treatmentType: 'Combined',
        productName: 'Broadline',
        batchReference: 'BL-771',
        administeredOn: iso(subMonths(today(), 1)),
        administeredByStaffId: catProfilesFixtureIds.ownerWithCats,
        nextDueOn: iso(addDays(subMonths(today(), 1), 90)),
        notes: null,
      },
    ],
    weights: [
      {
        id: crypto.randomUUID(),
        recordedOn: iso(subMonths(today(), 6)),
        kilograms: 3.9,
        recordedByStaffId: 'staff',
        notes: null,
      },
      {
        id: crypto.randomUUID(),
        recordedOn: iso(subMonths(today(), 3)),
        kilograms: 4.1,
        recordedByStaffId: 'staff',
        notes: null,
      },
      {
        id: crypto.randomUUID(),
        recordedOn: iso(subMonths(today(), 1)),
        kilograms: 4.2,
        recordedByStaffId: 'staff',
        notes: null,
      },
    ],
  });

  // Biscuit — no allergies (US-CAT-03 AC2 "None recorded"), estimated DOB, no photos, DueSoon.
  cats.set(catProfilesFixtureIds.catNoAllergiesEstimatedDob, {
    id: catProfilesFixtureIds.catNoAllergiesEstimatedDob,
    ownerId: catProfilesFixtureIds.ownerWithCats,
    name: 'Biscuit',
    species: 'Cat',
    sex: 'Male',
    dateOfBirth: iso(subMonths(today(), 30)),
    dateOfBirthPrecision: 'Estimated',
    breed: 'Domestic Shorthair',
    colourAndMarkings: 'Ginger tabby',
    neuterStatus: 'Neutered',
    neuteredOn: iso(subMonths(today(), 24)),
    isHighRisk: false,
    handlingNote: null,
    lifeStatus: 'Alive',
    deceasedOn: null,
    deceasedNote: null,
    dietaryNotes: null,
    behaviouralNotes: null,
    allergies: [],
    chronicConditions: [],
    medications: [],
    photos: [],
    // 35 months + 14 days ago with 36-month validity: due in ~16 days on any date. Plain
    // "35 months ago" fell due one calendar month out (28-31 days), so Biscuit was Current, not
    // DueSoon, whenever that month had 31 days (the 30-day window) — e2e E9 failed by date.
    vaccinations: [vaccination(rabies, 35, 'RB-1180', 14), vaccination(fvrcp, 1, 'FV-3321')],
    parasiteTreatments: [],
    weights: [
      {
        id: crypto.randomUUID(),
        recordedOn: iso(subMonths(today(), 1)),
        kilograms: 5.1,
        recordedByStaffId: 'staff',
        notes: null,
      },
    ],
  });

  // Tiger — high-risk, one core vaccine never administered, one overdue. Overall Overdue.
  cats.set(catProfilesFixtureIds.catHighRiskOverdue, {
    id: catProfilesFixtureIds.catHighRiskOverdue,
    ownerId: catProfilesFixtureIds.ownerWithCats,
    name: 'Tiger',
    species: 'Cat',
    sex: 'Male',
    dateOfBirth: '2019-06-01',
    dateOfBirthPrecision: 'Exact',
    breed: 'Domestic Longhair',
    colourAndMarkings: 'Brown tabby',
    neuterStatus: 'Intact',
    neuteredOn: null,
    isHighRisk: true,
    handlingNote: 'Bites and scratches when restrained. Two handlers and a muzzle required.',
    lifeStatus: 'Alive',
    deceasedOn: null,
    deceasedNote: null,
    dietaryNotes: null,
    behaviouralNotes: 'Extremely fearful of carriers.',
    allergies: [],
    chronicConditions: [],
    medications: [],
    photos: [
      {
        id: crypto.randomUUID(),
        storageKey: 'dev/tiger-1.jpg',
        contentType: 'image/png',
        sizeInBytes: 1_240_000,
        fileName: 'tiger.png',
        isPrimary: true,
        uploadedOn: subMonths(today(), 5).toISOString(),
      },
    ],
    // Rabies never administered -> Overdue/NeverAdministered. FVRCP administered 14 months ago
    // with 12-month validity -> also Overdue (past its own next-due date).
    vaccinations: [vaccination(fvrcp, 14, 'FV-0042')],
    parasiteTreatments: [],
    weights: [
      {
        id: crypto.randomUUID(),
        recordedOn: iso(subMonths(today(), 2)),
        kilograms: 5.8,
        recordedByStaffId: 'staff',
        notes: null,
      },
    ],
  });

  // Mittens — plain cat, Current, used as the general read/write flow fixture in e2e specs.
  cats.set(catProfilesFixtureIds.catCurrentPlain, {
    id: catProfilesFixtureIds.catCurrentPlain,
    ownerId: catProfilesFixtureIds.ownerWithCats,
    name: 'Mittens',
    species: 'Cat',
    sex: 'Female',
    dateOfBirth: '2022-01-20',
    dateOfBirthPrecision: 'Exact',
    breed: 'Domestic Shorthair',
    colourAndMarkings: 'Black and white',
    neuterStatus: 'Neutered',
    neuteredOn: '2022-07-20',
    isHighRisk: false,
    handlingNote: null,
    lifeStatus: 'Alive',
    deceasedOn: null,
    deceasedNote: null,
    dietaryNotes: null,
    behaviouralNotes: null,
    allergies: [],
    chronicConditions: [],
    medications: [],
    photos: [],
    vaccinations: [vaccination(rabies, 1, 'RB-5501'), vaccination(fvrcp, 1, 'FV-5502')],
    parasiteTreatments: [],
    weights: [
      {
        id: crypto.randomUUID(),
        recordedOn: iso(subMonths(today(), 1)),
        kilograms: 3.6,
        recordedByStaffId: 'staff',
        notes: null,
      },
    ],
  });

  // Ash — deceased (US-CAT-09): excluded from the default list, still viewable, not editable.
  cats.set(catProfilesFixtureIds.catDeceased, {
    id: catProfilesFixtureIds.catDeceased,
    ownerId: catProfilesFixtureIds.ownerWithCats,
    name: 'Ash',
    species: 'Cat',
    sex: 'Male',
    dateOfBirth: '2010-05-02',
    dateOfBirthPrecision: 'Exact',
    breed: 'Domestic Shorthair',
    colourAndMarkings: 'Grey',
    neuterStatus: 'Neutered',
    neuteredOn: '2010-11-02',
    isHighRisk: false,
    handlingNote: null,
    lifeStatus: 'Deceased',
    deceasedOn: iso(subMonths(today(), 2)),
    deceasedNote: 'Passed peacefully at home, kidney failure.',
    dietaryNotes: null,
    behaviouralNotes: null,
    allergies: [],
    chronicConditions: [],
    medications: [],
    photos: [],
    vaccinations: [],
    parasiteTreatments: [],
    weights: [
      {
        id: crypto.randomUUID(),
        recordedOn: iso(subMonths(today(), 4)),
        kilograms: 3.2,
        recordedByStaffId: 'staff',
        notes: null,
      },
    ],
  });

  // A cat that always 409s on its first profile edit this "session" (E20), then behaves normally.
  cats.set(catProfilesFixtureIds.catConflict, {
    id: catProfilesFixtureIds.catConflict,
    ownerId: catProfilesFixtureIds.ownerWithCats,
    name: 'Clover',
    species: 'Cat',
    sex: 'Female',
    dateOfBirth: '2020-02-02',
    dateOfBirthPrecision: 'Exact',
    breed: 'Domestic Shorthair',
    colourAndMarkings: 'Calico',
    neuterStatus: 'Neutered',
    neuteredOn: '2020-08-02',
    isHighRisk: false,
    handlingNote: null,
    lifeStatus: 'Alive',
    deceasedOn: null,
    deceasedNote: null,
    dietaryNotes: null,
    behaviouralNotes: null,
    allergies: [],
    chronicConditions: [],
    medications: [],
    photos: [],
    vaccinations: [],
    parasiteTreatments: [],
    weights: [],
  });

  // 25 cats for the same owner so the list can demonstrate real pagination (E22). Named to sort
  // after every other fixture cat (`Zz` beats every real cat name used above) so those cats stay
  // reachable on the list's first page in the unfiltered (all-owners) staff view rather than
  // being crowded out by 25 placeholder rows.
  for (let index = 1; index <= 25; index += 1) {
    const id = `40000000-0000-0000-0000-${String(index).padStart(12, '0')}`;
    cats.set(id, {
      id,
      ownerId: catProfilesFixtureIds.paginationOwner,
      name: `Zz Pagination Cat ${String(index).padStart(2, '0')}`,
      species: 'Cat',
      sex: 'Unknown',
      dateOfBirth: null,
      dateOfBirthPrecision: 'Estimated',
      breed: null,
      colourAndMarkings: null,
      neuterStatus: 'Unknown',
      neuteredOn: null,
      isHighRisk: false,
      handlingNote: null,
      lifeStatus: 'Alive',
      deceasedOn: null,
      deceasedNote: null,
      dietaryNotes: null,
      behaviouralNotes: null,
      allergies: [],
      chronicConditions: [],
      medications: [],
      photos: [],
      vaccinations: [],
      parasiteTreatments: [],
      weights: [],
    });
  }

  return cats;
}

let vaccineCatalog = seedVaccineCatalog();
let cats = seedCats(vaccineCatalog);
const consumedConflicts = new Set<string>();

/** Restores the seeded fixtures — call from a test's `beforeEach` for a clean slate. */
export function resetCatProfilesMockData(): void {
  vaccineCatalog = seedVaccineCatalog();
  cats = seedCats(vaccineCatalog);
  consumedConflicts.clear();
  forceNextCatQuery401 = false;
  // `__forceCatQueryNotOwned` (consumed by `consumeForceCatQueryNotOwned`) is a plain `window`
  // boolean an e2e test sets via `page.addInitScript()` for exactly one upcoming request — there
  // is no module-level state of its own here to reset between tests.
}

// ARCHITECTURE gate fourth pass, HIGH — this module's own endpoints never checked staff-session
// state at all (HO-CAT-001 predates HO-ACC-001's session system), so
// `accounts.ts`'s own `__forceStaffIdleExpired` escape hatch (which marks the STAFF SESSION
// itself as expired) has no effect on `GET /api/cats/{catId}`. E2E E-G2 ("type into a cat form,
// let the session go idle, re-sign-in") needs this endpoint to independently 401 on a
// focus-triggered refetch, the same way a real API's `RequireActionFilter` would once the staff
// session it depends on has actually ended — this dedicated, one-shot flag is the mock-side
// equivalent, consumed by the very next `GET /api/cats/{catId}` call and never sticky afterward
// (a real ended session stays ended, but this flag only needs to model ONE such read for the test
// that uses it; leaving it sticky would 401 the page's own recovery-after-re-sign-in read too).
let forceNextCatQuery401 = false;

/** Real-browser-only test hook, exposed on `window` the same way `accounts.ts`'s own
 *  `__forceStaffIdleExpired` is — this module runs in the page's own JS realm in browser MSW
 *  mode, so a Playwright e2e spec cannot `import` this function directly. Guarded the same way
 *  `canPersist`/`__forceStaffIdleExpired` are, so it does not exist in Vitest's jsdom. */
export function forceNextCatQueryToReportSessionInvalid(): void {
  forceNextCatQuery401 = true;
}

if (typeof window !== 'undefined') {
  (
    window as unknown as { __forceCatQuerySessionInvalid?: () => void }
  ).__forceCatQuerySessionInvalid = forceNextCatQueryToReportSessionInvalid;
}

// HO-CAT-002 §1 Amended (2026-09-27, owner decision on `/cats/{id}`'s ownership check) — the same
// class of problem as `forceNextCatQuery401`/`forceNextCatQueryToReportSessionInvalid` above, for
// the same reason: this module has never read what the customer session cookie `pc_session` maps
// to (that mapping is `accounts.ts`'s own private module state, and `README.md`'s "don't edit
// another area's mocks/handlers/areas/*.ts" rule is precisely why this module doesn't reach into
// it), so `GET /api/cats/{catId}` has no real way to compute "does the signed-in customer actually
// own this cat" — the real HO-CAT-001 API does, server-side (§20.3's query filter).
//
// Solved differently from `forceNextCatQuery401` above, though, because of WHEN it's needed:
// `/cats/{id}`'s ownership check fires on first mount, immediately after the FULL page navigation
// that lands on it — there is no earlier point on that same page where an e2e test could call an
// exported `window.*` function the way `__forceCatQuerySessionInvalid` is called (that hook is set
// on an already-loaded page, before an in-place refetch with no navigation in between; a full
// navigation to `/cats/{id}` tears down that window and this module's state along with it, so
// anything set on the OLD window is gone by the time the NEW page's fresh module instance runs).
// Instead, this reads a plain boolean directly off `window` on every request, which an e2e test
// sets via Playwright's `page.addInitScript()` — evaluated in the new document BEFORE any of the
// page's own scripts (including this module's) run, so the flag is already sitting on the fresh
// `window` by the time this handler's module re-initialises and the first (and, being one-shot,
// only) `GET /api/cats/{catId}` call reads it. Not `localStorage`/a cookie (no web storage) —
// `addInitScript` is a Playwright test-harness mechanism, scoped to that one browser context, not
// app state.
function consumeForceCatQueryNotOwned(): boolean {
  if (typeof window === 'undefined') return false; // Vitest's jsdom — never forced
  const flagHolder = window as unknown as { __forceCatQueryNotOwned?: boolean };
  if (!flagHolder.__forceCatQueryNotOwned) return false;
  flagHolder.__forceCatQueryNotOwned = false; // one-shot, same as forceNextCatQuery401
  return true;
}

// -------------------------------------------------------------------------------------------
// Derivation helpers — mirror the read-side query handlers (HO-CAT-001 §12.3), not reinvented.
// -------------------------------------------------------------------------------------------

const VACCINATION_DUE_SOON_WINDOW_DAYS = 30; // CatProfileOptions default (HO-CAT-001 §17.1)
const STATUS_RANK: Record<VaccinationStatus, number> = { Current: 1, DueSoon: 2, Overdue: 3 };

function toProfileViewModel(cat: StoredCat): CatProfile {
  const latestWeight = [...cat.weights].sort((a, b) => b.recordedOn.localeCompare(a.recordedOn))[0];
  return {
    id: cat.id,
    ownerId: cat.ownerId,
    name: cat.name,
    species: cat.species,
    sex: cat.sex,
    dateOfBirth: cat.dateOfBirth,
    dateOfBirthPrecision: cat.dateOfBirthPrecision,
    breed: cat.breed,
    colourAndMarkings: cat.colourAndMarkings,
    neuterStatus: cat.neuterStatus,
    neuteredOn: cat.neuteredOn,
    isHighRisk: cat.isHighRisk,
    handlingNote: cat.handlingNote,
    lifeStatus: cat.lifeStatus,
    deceasedOn: cat.deceasedOn,
    deceasedNote: cat.deceasedNote,
    dietaryNotes: cat.dietaryNotes,
    behaviouralNotes: cat.behaviouralNotes,
    latestWeightKg: latestWeight?.kilograms ?? null,
    hasRecordedAllergies: cat.allergies.length > 0,
    allergies: cat.allergies,
    chronicConditions: cat.chronicConditions,
    medications: cat.medications,
    photos: cat.photos,
  };
}

function toListItem(cat: StoredCat): CatListItem {
  return {
    id: cat.id,
    ownerId: cat.ownerId,
    name: cat.name,
    species: cat.species,
    breed: cat.breed,
    sex: cat.sex,
    isHighRisk: cat.isHighRisk,
    handlingNote: cat.handlingNote,
    lifeStatus: cat.lifeStatus,
  };
}

function calculateVaccinationStatus(cat: StoredCat): CatVaccinationStatus {
  const asOf = today();
  const activeCoreVaccines = vaccineCatalog.filter((v) => v.isCore && v.isActive);

  const items: CatVaccinationStatusItem[] = activeCoreVaccines.map((vaccine) => {
    const mostRecent = cat.vaccinations
      .filter((v) => v.vaccineId === vaccine.id)
      .sort((a, b) => b.administeredOn.localeCompare(a.administeredOn))[0];

    if (!mostRecent) {
      return {
        vaccineId: vaccine.id,
        vaccineCode: vaccine.code,
        vaccineName: vaccine.name,
        status: 'Overdue' as const,
        nextDueOn: null,
        reason: 'NeverAdministered',
      };
    }

    const nextDue = new Date(`${mostRecent.nextDueOn}T00:00:00`);
    const status: VaccinationStatus =
      nextDue < asOf
        ? 'Overdue'
        : nextDue <= addDays(asOf, VACCINATION_DUE_SOON_WINDOW_DAYS)
          ? 'DueSoon'
          : 'Current';

    return {
      vaccineId: vaccine.id,
      vaccineCode: vaccine.code,
      vaccineName: vaccine.name,
      status,
      nextDueOn: mostRecent.nextDueOn,
      reason: null,
    };
  });

  const overallStatus =
    items.length === 0
      ? 'Current'
      : items.reduce<VaccinationStatus>(
          (worst, item) => (STATUS_RANK[item.status] > STATUS_RANK[worst] ? item.status : worst),
          'Current',
        );

  const dueDates = items
    .map((i) => i.nextDueOn)
    .filter((d): d is string => d !== null)
    .sort();

  return {
    catId: cat.id,
    overallStatus,
    earliestNextDueOn: dueDates[0] ?? null,
    bookingEligible: overallStatus !== 'Overdue',
    vaccines: items,
  };
}

// -------------------------------------------------------------------------------------------
// Error helpers — HO-CAT-001 §10/§12.4 shape
//
// Two distinct 400 shapes exist in the real backend, and this mock keeps them distinct rather
// than inventing one generic error code per case (§20.2):
//
//  1. Command-level FluentValidation shape rules (RegisterCatCommandValidator etc., §11.3) —
//     "shape only", run automatically before the handler via F3's ValidateCommand=true. None of
//     these validators call .WithErrorCode(...), so the errorCode the API actually returns is
//     FluentValidation's own default (the validator class name), and resourceName is empty.
//     Verified empirically against the exact Platform.Infrastructure.Core 1.0.34 +
//     FluentValidation 8.5.1 assemblies this repo pins (FluentValidationProvider.Validate, run
//     against a stand-in validator built from the same rule calls: .NotEmpty() →
//     "NotEmptyValidator", .NotNull() → "NotNullValidator", .InclusiveBetween(...) →
//     "InclusiveBetweenValidator") — not guessed, and not FluentValidation's docs, since a
//     library's documented defaults can drift from what a pinned version actually emits.
//
//  2. Aggregate-level business-rule violations (HO-CAT-001 §10 BusinessViolationCodes) — thrown
//     as a DomainRuleViolationException and converted by the owning command handler's own
//     Violation() helper (§11.5), which hardcodes resourceName to the aggregate the *handler*
//     lives on (nameof(CatAggregate) for every CatsController handler, including the two vaccine
//     checks inside RecordCatVaccinationCommandHandler; nameof(VaccineAggregate) only for
//     RegisterVaccineCommandHandler's own uniqueness check).
// -------------------------------------------------------------------------------------------

type FieldValidationCode =
  'NotEmptyValidator' | 'NotNullValidator' | 'InclusiveBetweenValidator' | 'MaximumLengthValidator';

/** Case 1 above — a command-level shape rule. */
function fieldError(propertyName: string, errorCode: FieldValidationCode, errorMessage: string) {
  return HttpResponse.json(
    { isValid: false, errors: [{ propertyName, errorCode, errorMessage, resourceName: '' }] },
    { status: 400 },
  );
}

/** Case 2 above — a genuine §10 business-rule violation. */
function domainViolation(
  propertyName: string,
  errorCode: string,
  errorMessage: string,
  resourceName: 'CatAggregate' | 'VaccineAggregate' = 'CatAggregate',
) {
  return HttpResponse.json(
    { isValid: false, errors: [{ propertyName, errorCode, errorMessage, resourceName }] },
    { status: 400 },
  );
}

function requireNotEmpty(value: string | undefined | null, propertyName: string) {
  if (!value || !value.trim()) {
    return fieldError(propertyName, 'NotEmptyValidator', `Enter a value for ${propertyName}.`);
  }
  return null;
}

// -------------------------------------------------------------------------------------------
// Paging — owner decision 2026-09-26 (HO-CAT-001 §1 Amended): pageSize is capped at 100 (a
// larger value is clamped, not rejected); pageSize <= 0 is a 400. Every list endpoint returns
// { items, pageNumber, pageSize, totalCount }.
// -------------------------------------------------------------------------------------------

const MAX_PAGE_SIZE = 100;

/** Returns a 400 response, or the resolved { pageNumber, pageSize } when valid. */
function resolvePaging(
  url: URL,
  defaultPageSize: number,
): { pageNumber: number; pageSize: number } | Response {
  const pageNumber = Number(url.searchParams.get('pageNumber') ?? '1');
  const rawPageSize = url.searchParams.get('pageSize');
  if (rawPageSize === null) return { pageNumber, pageSize: defaultPageSize };

  const pageSize = Number(rawPageSize);
  if (pageSize <= 0) {
    return domainViolation('pageSize', 'InvalidPageSize', 'pageSize must be greater than zero.');
  }
  return { pageNumber, pageSize: Math.min(pageSize, MAX_PAGE_SIZE) };
}

function pagedResponse<T>(items: T[], allItems: T[], pageNumber: number, pageSize: number) {
  return HttpResponse.json({ items, pageNumber, pageSize, totalCount: allItems.length });
}

// -------------------------------------------------------------------------------------------
// Handlers
// -------------------------------------------------------------------------------------------

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';
const CUSTOMER_CALLER_ID = 'c0000000-0000-0000-0000-000000000001'; // accountsFixtureIds.customerId

export const catProfilesHandlers = [
  // GET /api/cats — Q2
  http.get('*/api/cats', ({ request }) => {
    const url = new URL(request.url);
    const ownerId = url.searchParams.get('ownerId');
    const includeDeceased = url.searchParams.get('includeDeceased') === 'true';

    const paging = resolvePaging(url, 10);
    if (paging instanceof Response) return paging;
    const { pageNumber, pageSize } = paging;

    let rows = [...cats.values()];
    if (ownerId) rows = rows.filter((c) => c.ownerId === ownerId);
    if (!includeDeceased) rows = rows.filter((c) => c.lifeStatus === 'Alive');
    rows.sort((a, b) => a.name.localeCompare(b.name));

    const start = (pageNumber - 1) * pageSize;
    const page = rows.slice(start, start + pageSize);

    return pagedResponse(page.map(toListItem), rows, pageNumber, pageSize);
  }),

  // POST /api/cats — C1, US-CAT-01
  http.post('*/api/cats', async ({ request }) => {
    const body = (await request.json()) as {
      ownerId: string;
      name: string;
      species: string;
      sex: CatProfile['sex'];
      dateOfBirth: string | null;
      dateOfBirthPrecision: CatProfile['dateOfBirthPrecision'];
      breed: string | null;
      colourAndMarkings: string | null;
    };

    const nameError = requireNotEmpty(body.name, 'Name');
    if (nameError) return nameError;
    if (body.dateOfBirthPrecision === 'Exact' && !body.dateOfBirth) {
      return fieldError(
        'DateOfBirth',
        'NotNullValidator',
        'Enter a date of birth, or switch to an estimated age.',
      );
    }

    if (body.name.trim().length > 100) {
      return fieldError('Name', 'MaximumLengthValidator', 'Name must be 100 characters or fewer.');
    }
    // F19 — for a customer caller the real API forces the owner to the caller. The mock cannot
    // see the caller, so the customer portal's empty-GUID owner stands for "the signed-in customer".
    const ownerId =
      !body.ownerId || body.ownerId === EMPTY_GUID ? CUSTOMER_CALLER_ID : body.ownerId;

    const id = crypto.randomUUID();
    const newCat: StoredCat = {
      id,
      ownerId,
      name: body.name,
      species: body.species || 'Cat',
      sex: body.sex,
      dateOfBirth: body.dateOfBirth,
      dateOfBirthPrecision: body.dateOfBirthPrecision,
      breed: body.breed,
      colourAndMarkings: body.colourAndMarkings,
      neuterStatus: 'Unknown',
      neuteredOn: null,
      isHighRisk: false,
      handlingNote: null,
      lifeStatus: 'Alive',
      deceasedOn: null,
      deceasedNote: null,
      dietaryNotes: null,
      behaviouralNotes: null,
      allergies: [],
      chronicConditions: [],
      medications: [],
      photos: [],
      vaccinations: [],
      parasiteTreatments: [],
      weights: [],
    };
    cats.set(id, newCat);

    return HttpResponse.json({ catId: id }, { status: 201 });
  }),

  // GET /api/cats/{catId} — Q1
  http.get('*/api/cats/:catId', ({ params }) => {
    const catId = params.catId as string;
    if (catId === catProfilesFixtureIds.catServerError) {
      return HttpResponse.json({ error: 'internal' }, { status: 500 });
    }
    if (forceNextCatQuery401) {
      forceNextCatQuery401 = false;
      return HttpResponse.json({ error: 'session_invalid' }, { status: 401 });
    }
    if (consumeForceCatQueryNotOwned()) {
      return new HttpResponse(null, { status: 404 });
    }
    const cat = cats.get(catId);
    if (!cat) return new HttpResponse(null, { status: 404 });
    return HttpResponse.json(toProfileViewModel(cat));
  }),

  // PUT /api/cats/{catId}/profile — C2, US-CAT-01
  http.put('*/api/cats/:catId/profile', async ({ params, request }) => {
    const catId = params.catId as string;
    const cat = cats.get(catId);
    if (!cat) return new HttpResponse(null, { status: 404 });
    if (cat.lifeStatus === 'Deceased') {
      return domainViolation('CatId', 'CatIsDeceased', "This cat's record is read-only.");
    }
    if (catId === catProfilesFixtureIds.catConflict && !consumedConflicts.has('profile')) {
      consumedConflicts.add('profile');
      return new HttpResponse(null, { status: 409 });
    }

    const body = (await request.json()) as {
      name: string;
      sex: CatProfile['sex'];
      dateOfBirth: string | null;
      dateOfBirthPrecision: CatProfile['dateOfBirthPrecision'];
      breed: string | null;
      colourAndMarkings: string | null;
    };
    const nameError = requireNotEmpty(body.name, 'Name');
    if (nameError) return nameError;

    Object.assign(cat, body);
    return HttpResponse.json({ catId });
  }),

  // PUT /api/cats/{catId}/neuter-status — C3, US-CAT-02
  http.put('*/api/cats/:catId/neuter-status', async ({ params, request }) => {
    const catId = params.catId as string;
    const cat = cats.get(catId);
    if (!cat) return new HttpResponse(null, { status: 404 });

    const body = (await request.json()) as {
      neuterStatus: CatProfile['neuterStatus'];
      neuteredOn: string | null;
    };
    if (body.neuterStatus === 'Neutered' && !body.neuteredOn) {
      return fieldError('NeuteredOn', 'NotNullValidator', 'Enter the date this cat was neutered.');
    }

    cat.neuterStatus = body.neuterStatus;
    cat.neuteredOn = body.neuterStatus === 'Neutered' ? body.neuteredOn : null;
    return HttpResponse.json({ catId });
  }),

  // PUT /api/cats/{catId}/medical-profile — C5, US-CAT-03
  http.put('*/api/cats/:catId/medical-profile', async ({ params, request }) => {
    const catId = params.catId as string;
    const cat = cats.get(catId);
    if (!cat) return new HttpResponse(null, { status: 404 });

    const body = (await request.json()) as {
      allergies: Array<{
        allergen: string;
        reaction: string;
        severity: CatProfile['allergies'][number]['severity'];
        recordedOn: string;
      }>;
      chronicConditions: Array<{
        condition: string;
        diagnosedOn: string | null;
        notes: string | null;
      }>;
      medications: Array<{
        medicationName: string;
        dosage: string;
        frequency: string;
        startedOn: string;
        endsOn: string | null;
      }>;
      dietaryNotes: string | null;
      behaviouralNotes: string | null;
    };

    // A dedicated, documented trigger for the "400 with an unmatched propertyName" fixture
    // §12.5 asks for — a server-side field no form on this screen maps, so it must surface as
    // a root/form-level error rather than being silently dropped (HO-CAT-002 §11.8).
    if (body.dietaryNotes === 'TRIGGER_UNMATCHED_FIELD') {
      return domainViolation(
        'SomeServerOnlyField',
        'UnexpectedServerRule',
        'This record could not be saved because of a rule this screen does not show. Contact support.',
      );
    }

    cat.allergies = body.allergies.map((a) => ({ id: crypto.randomUUID(), ...a }));
    cat.chronicConditions = body.chronicConditions.map((c) => ({ id: crypto.randomUUID(), ...c }));
    cat.medications = body.medications.map((m) => ({ id: crypto.randomUUID(), ...m }));
    cat.dietaryNotes = body.dietaryNotes;
    cat.behaviouralNotes = body.behaviouralNotes;

    return HttpResponse.json({ catId });
  }),

  // POST /api/cats/{catId}/photos — C6, US-CAT-01 AC3
  http.post('*/api/cats/:catId/photos', async ({ params, request }) => {
    const catId = params.catId as string;
    const cat = cats.get(catId);
    if (!cat) return new HttpResponse(null, { status: 404 });

    const body = (await request.json()) as {
      storageKey: string;
      contentType: string;
      sizeInBytes: number;
      fileName: string;
      isPrimary: boolean;
    };

    if (!(CAT_PHOTO_RULES.allowedContentTypes as readonly string[]).includes(body.contentType)) {
      return domainViolation(
        'ContentType',
        'InvalidPhotoContentType',
        "That file type isn't supported. Use JPEG, PNG or WebP.",
      );
    }
    if (body.sizeInBytes > CAT_PHOTO_RULES.maxSizeInBytes) {
      return domainViolation(
        'SizeInBytes',
        'PhotoTooLarge',
        `That image is too large. The maximum is ${CAT_PHOTO_RULES.maxSizeInBytes / (1024 * 1024)} MB.`,
      );
    }
    if (cat.photos.length >= CAT_PHOTO_RULES.maxPhotosPerCat) {
      return domainViolation(
        'PhotoId',
        'PhotoLimitExceeded',
        'This cat already has the maximum of 10 photos.',
      );
    }

    const photoId = crypto.randomUUID();
    cat.photos.push({
      id: photoId,
      storageKey: body.storageKey,
      contentType: body.contentType,
      sizeInBytes: body.sizeInBytes,
      fileName: body.fileName,
      isPrimary: cat.photos.length === 0 ? true : body.isPrimary,
      uploadedOn: new Date().toISOString(),
    });

    return HttpResponse.json({ catId, photoId }, { status: 201 });
  }),

  // PUT /api/cats/{catId}/high-risk-flag — C7, US-CAT-04
  http.put('*/api/cats/:catId/high-risk-flag', async ({ params, request }) => {
    const catId = params.catId as string;
    const cat = cats.get(catId);
    if (!cat) return new HttpResponse(null, { status: 404 });

    const body = (await request.json()) as { handlingNote: string; reason: string };
    const reasonError = requireNotEmpty(body.reason, 'Reason');
    if (reasonError) return reasonError;
    const noteError = requireNotEmpty(body.handlingNote, 'HandlingNote');
    if (noteError) return noteError;

    cat.isHighRisk = true;
    cat.handlingNote = body.handlingNote;
    return HttpResponse.json({ catId });
  }),

  // DELETE /api/cats/{catId}/high-risk-flag — C8, US-CAT-04
  http.delete('*/api/cats/:catId/high-risk-flag', async ({ params, request }) => {
    const catId = params.catId as string;
    const cat = cats.get(catId);
    if (!cat) return new HttpResponse(null, { status: 404 });

    const body = (await request.json()) as { reason: string };
    const reasonError = requireNotEmpty(body.reason, 'Reason');
    if (reasonError) return reasonError;
    if (!cat.isHighRisk) {
      return domainViolation(
        'CatId',
        'CatNotFlaggedHighRisk',
        'This cat is not flagged high-risk.',
      );
    }

    cat.isHighRisk = false;
    cat.handlingNote = null;
    return HttpResponse.json({ catId });
  }),

  // POST /api/cats/{catId}/weights — C4, US-CAT-02
  http.post('*/api/cats/:catId/weights', async ({ params, request }) => {
    const catId = params.catId as string;
    const cat = cats.get(catId);
    if (!cat) return new HttpResponse(null, { status: 404 });

    const body = (await request.json()) as {
      kilograms: number;
      recordedOn: string;
      notes: string | null;
    };
    if (body.kilograms < 0.1 || body.kilograms > 30) {
      return fieldError(
        'Kilograms',
        'InclusiveBetweenValidator',
        'Enter a weight between 0.10 and 30.00 kg.',
      );
    }

    // SECURITY gate, MEDIUM (2026-09-26): the real server always takes the caller's own identity
    // here (RecordedByStaffId is not client-suppliable) — the mock store still tracks a
    // recordedByStaffId internally (StoredWeight), but it is never read from the request body.
    const weightRecordId = crypto.randomUUID();
    cat.weights.push({ id: weightRecordId, recordedByStaffId: 'mock-caller', ...body });
    return HttpResponse.json({ catId, weightRecordId }, { status: 201 });
  }),

  // GET /api/cats/{catId}/weights — Q3
  http.get('*/api/cats/:catId/weights', ({ params, request }) => {
    const catId = params.catId as string;
    const cat = cats.get(catId);
    if (!cat) return new HttpResponse(null, { status: 404 });

    const url = new URL(request.url);
    const paging = resolvePaging(url, 10);
    if (paging instanceof Response) return paging;
    const { pageNumber, pageSize } = paging;

    const rows = [...cat.weights].sort((a, b) => a.recordedOn.localeCompare(b.recordedOn));
    const start = (pageNumber - 1) * pageSize;
    const page = rows
      .slice(start, start + pageSize)
      .map((w) => ({ recordedOn: w.recordedOn, kilograms: w.kilograms }));

    return pagedResponse(page, rows, pageNumber, pageSize);
  }),

  // POST /api/cats/{catId}/vaccinations — C9, US-CAT-05
  http.post('*/api/cats/:catId/vaccinations', async ({ params, request }) => {
    const catId = params.catId as string;
    const cat = cats.get(catId);
    if (!cat) return new HttpResponse(null, { status: 404 });

    const body = (await request.json()) as {
      vaccineId: string;
      batchReference: string;
      batchExpiresOn: string;
      administeredOn: string;
      administeredByVetId: string;
      notes: string | null;
    };

    const vaccine = vaccineCatalog.find((v) => v.id === body.vaccineId);
    if (!vaccine) {
      return domainViolation(
        'VaccineId',
        'VaccineNotFound',
        'That vaccine was not found in the catalogue.',
      );
    }
    if (!vaccine.isActive) {
      return domainViolation('VaccineId', 'InactiveVaccine', 'That vaccine is no longer active.');
    }
    if (body.batchExpiresOn < body.administeredOn) {
      return domainViolation(
        'BatchExpiresOn',
        'ExpiredVaccineBatch',
        'This batch expired before the date given — check the batch reference and expiry date.',
      );
    }
    const duplicate = cat.vaccinations.some(
      (v) => v.vaccineId === body.vaccineId && v.administeredOn === body.administeredOn,
    );
    if (duplicate) {
      return domainViolation(
        'AdministeredOn',
        'DuplicateVaccinationOnDate',
        'This vaccine was already recorded for this cat on this date.',
      );
    }

    const vaccinationId = crypto.randomUUID();
    cat.vaccinations.push({
      id: vaccinationId,
      vaccineId: vaccine.id,
      vaccineCode: vaccine.code,
      vaccineName: vaccine.name,
      isCore: vaccine.isCore,
      batchReference: body.batchReference,
      batchExpiresOn: body.batchExpiresOn,
      administeredOn: body.administeredOn,
      administeredByVetId: body.administeredByVetId,
      nextDueOn: dateOnlyAddMonths(body.administeredOn, vaccine.validityMonths),
      notes: body.notes,
    });

    return HttpResponse.json({ catId, vaccinationId }, { status: 201 });
  }),

  // GET /api/cats/{catId}/vaccination-status — Q4, US-CAT-07
  http.get('*/api/cats/:catId/vaccination-status', ({ params }) => {
    const catId = params.catId as string;
    const cat = cats.get(catId);
    if (!cat) return new HttpResponse(null, { status: 404 });
    return HttpResponse.json(calculateVaccinationStatus(cat));
  }),

  // POST /api/cats/{catId}/parasite-treatments — C10, US-CAT-06
  http.post('*/api/cats/:catId/parasite-treatments', async ({ params, request }) => {
    const catId = params.catId as string;
    const cat = cats.get(catId);
    if (!cat) return new HttpResponse(null, { status: 404 });

    const body = (await request.json()) as {
      treatmentType: ParasiteTreatmentType;
      productName: string;
      batchReference: string | null;
      administeredOn: string;
      administeredByStaffId: string;
      intervalDays: number;
      notes: string | null;
    };
    if (body.intervalDays < 1 || body.intervalDays > 730) {
      return fieldError(
        'IntervalDays',
        'InclusiveBetweenValidator',
        'Enter an interval between 1 and 730 days.',
      );
    }

    // Stands in for HO-CAT-001 §8.2's aggregate rule (owner decision 2026-09-25) — this
    // computation belongs only here, in the mock, never in any app code under app/, components/
    // or lib/ (HO-CAT-002 §20.2). The real server is the single source of truth; the mock exists
    // only to give this handler something to return.
    const treatmentId = crypto.randomUUID();
    const nextDueOn = iso(addDays(new Date(`${body.administeredOn}T00:00:00`), body.intervalDays));
    cat.parasiteTreatments.push({
      id: treatmentId,
      treatmentType: body.treatmentType,
      productName: body.productName,
      batchReference: body.batchReference,
      administeredOn: body.administeredOn,
      administeredByStaffId: body.administeredByStaffId,
      nextDueOn,
      notes: body.notes,
    });

    // Exactly { treatmentId, nextDueOn } — HO-CAT-001 §12.4, no catId (ARCHITECTURE gate HIGH,
    // fifth pass, HO-CAT-002 §20.2: never invent a field the API will not return).
    return HttpResponse.json({ treatmentId, nextDueOn }, { status: 201 });
  }),

  // POST /api/cats/{catId}/deceased — C11, US-CAT-09
  http.post('*/api/cats/:catId/deceased', async ({ params, request }) => {
    const catId = params.catId as string;
    const cat = cats.get(catId);
    if (!cat) return new HttpResponse(null, { status: 404 });
    if (cat.lifeStatus === 'Deceased') {
      return domainViolation(
        'CatId',
        'CatAlreadyDeceased',
        'This cat is already recorded as deceased.',
      );
    }

    const body = (await request.json()) as { deceasedOn: string; note: string | null };
    cat.lifeStatus = 'Deceased';
    cat.deceasedOn = body.deceasedOn;
    cat.deceasedNote = body.note;

    return HttpResponse.json({ catId });
  }),

  // POST /api/cats/{catId}/ownership-transfer — C12, US-CAT-08
  http.post('*/api/cats/:catId/ownership-transfer', async ({ params, request }) => {
    const catId = params.catId as string;
    const cat = cats.get(catId);
    if (!cat) return new HttpResponse(null, { status: 404 });

    const body = (await request.json()) as { newOwnerId: string; reason: string };
    const reasonError = requireNotEmpty(body.reason, 'Reason');
    if (reasonError) return reasonError;
    if (body.newOwnerId === cat.ownerId) {
      return domainViolation(
        'NewOwnerId',
        'OwnerUnchanged',
        'This cat already belongs to that owner.',
      );
    }

    cat.ownerId = body.newOwnerId;
    return HttpResponse.json({ catId });
  }),

  // GET /api/cats/{catId}/medical-history.pdf — Q5, US-CAT-10
  http.get('*/api/cats/:catId/medical-history.pdf', ({ params }) => {
    const catId = params.catId as string;
    const cat = cats.get(catId);
    if (!cat) return new HttpResponse(null, { status: 404 });

    // A minimal, non-empty PDF-shaped payload — enough to prove a PDF downloads (E14). The real
    // rendering (QuestPDF, clinical content, the §20.5 omission note) is HO-CAT-001 scope.
    const body = `%PDF-1.4\n% Mock medical history for ${cat.name}\n%%EOF`;
    return new HttpResponse(body, {
      status: 200,
      headers: {
        'Content-Type': 'application/pdf',
        'Content-Disposition': `attachment; filename="${cat.name}-medical-history.pdf"`,
      },
    });
  }),

  // GET /api/cats/{catId}/cage-card — Q7, US-CAT-11
  http.get('*/api/cats/:catId/cage-card', ({ params }) => {
    const catId = params.catId as string;
    const cat = cats.get(catId);
    if (!cat) return new HttpResponse(null, { status: 404 });

    // 1x1 transparent PNG — enough to prove an image/png response (E15); the real QR encoding
    // (QRCoder, resolving to the profile URL) is HO-CAT-001 scope.
    const onePixelPng = Buffer.from(
      'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=',
      'base64',
    );
    return new HttpResponse(onePixelPng, { status: 200, headers: { 'Content-Type': 'image/png' } });
  }),

  // GET /api/vaccines — Q6
  http.get('*/api/vaccines', ({ request }) => {
    const url = new URL(request.url);
    const activeOnly = url.searchParams.get('activeOnly') === 'true';
    const coreOnly = url.searchParams.get('coreOnly') === 'true';

    const paging = resolvePaging(url, 20);
    if (paging instanceof Response) return paging;
    const { pageNumber, pageSize } = paging;

    let rows = [...vaccineCatalog];
    if (activeOnly) rows = rows.filter((v) => v.isActive);
    if (coreOnly) rows = rows.filter((v) => v.isCore);
    rows.sort((a, b) => a.name.localeCompare(b.name));

    const start = (pageNumber - 1) * pageSize;
    return pagedResponse(rows.slice(start, start + pageSize), rows, pageNumber, pageSize);
  }),

  // POST /api/vaccines — C13
  http.post('*/api/vaccines', async ({ request }) => {
    const body = (await request.json()) as {
      code: string;
      name: string;
      isCore: boolean;
      validityMonths: number;
      description: string | null;
    };

    const codeError = requireNotEmpty(body.code, 'Code');
    if (codeError) return codeError;
    if (vaccineCatalog.some((v) => v.code.toLowerCase() === body.code.toLowerCase())) {
      return domainViolation(
        'Code',
        'VaccineCodeAlreadyExists',
        'A vaccine with that code already exists.',
        'VaccineAggregate', // RegisterVaccineCommandHandler's own Violation() helper, not CatsController's
      );
    }
    if (body.validityMonths < 1 || body.validityMonths > 120) {
      return fieldError(
        'ValidityMonths',
        'InclusiveBetweenValidator',
        'Enter a validity between 1 and 120 months.',
      );
    }

    const vaccineId = crypto.randomUUID();
    vaccineCatalog.push({
      id: vaccineId,
      code: body.code,
      name: body.name,
      isCore: body.isCore,
      validityMonths: body.validityMonths,
      isActive: true,
    });

    return HttpResponse.json({ vaccineId }, { status: 201 });
  }),
];
