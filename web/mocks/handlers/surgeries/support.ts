import { http, type HttpHandler } from 'msw';
import type { CatProfile, CatVaccinationStatus } from '@/lib/api/cats';
import { db as pharmacyDb } from '../pharmacy/store';
import { getDb as getEmrDb, persist as persistEmr } from '../visits/store';
import { ok } from './fixtures';
import { CUSTOMERS, findCat, getDb, newId, persist, todayIn } from './store';
import { surgeryFixtureIds as ids } from './fixtures';

/**
 * `mocks/handlers/surgeries/support.ts` — look-ups the surgery screens make on OTHER areas' endpoints,
 * answered here ONLY for the ids this mock owns and falling through (`undefined`) for every other id, so
 * the cat, customer, inventory and formulary mocks keep answering for theirs (the ordering mock's
 * pattern). The cat and customer mocks have no intact female cat and no customer who owns one, which a
 * spay needs. Nothing here is a surgery endpoint.
 */
function profileOf(catId: string): CatProfile | null {
  const cat = findCat(catId);
  if (!cat) return null;
  const latest = [...cat.weights].sort((a, b) => a.recordedOn.localeCompare(b.recordedOn)).at(-1);
  return {
    id: cat.id,
    ownerId: cat.ownerId,
    name: cat.name,
    species: 'Cat',
    sex: cat.sex,
    dateOfBirth: '2023-04-01',
    dateOfBirthPrecision: 'Exact',
    breed: 'Domestic Shorthair',
    colourAndMarkings: null,
    neuterStatus: cat.neuterStatus,
    neuteredOn: cat.neuteredOn,
    isHighRisk: cat.isHighRisk,
    handlingNote: cat.isHighRisk ? 'Fractious: use a towel and two handlers.' : null,
    lifeStatus: 'Alive',
    deceasedOn: null,
    deceasedNote: null,
    dietaryNotes: null,
    behaviouralNotes: null,
    latestWeightKg: latest?.kilograms ?? null,
    hasRecordedAllergies: cat.allergies.length > 0,
    allergies: cat.allergies.map((allergen) => ({
      id: newId(),
      allergen,
      reaction: 'Severe reaction',
      severity: 'Severe',
      recordedOn: '2025-01-01',
    })) as CatProfile['allergies'],
    chronicConditions: [],
    medications: [],
    photos: [],
  } as CatProfile;
}

const vaccination = (catId: string): CatVaccinationStatus => ({
  catId,
  overallStatus: 'Current',
  earliestNextDueOn: null,
  bookingEligible: true,
  vaccines: [],
});

export const supportHandlers: HttpHandler[] = [
  http.get('*/api/cats/:catId/vaccination-status', ({ params }) =>
    findCat(params.catId as string) ? ok(vaccination(params.catId as string)) : undefined,
  ),

  http.get('*/api/cats/:catId/weights', ({ params }) => {
    const cat = findCat(params.catId as string);
    if (!cat) return undefined;
    const items = [...cat.weights]
      .sort((a, b) => a.recordedOn.localeCompare(b.recordedOn))
      .map((w) => ({ recordedOn: w.recordedOn, kilograms: w.kilograms }));
    return ok({ items, pageNumber: 1, pageSize: 100, totalCount: items.length });
  }),

  http.post('*/api/cats/:catId/weights', async ({ params, request }) => {
    const cat = findCat(params.catId as string);
    if (!cat) return undefined;
    const body = (await request.json()) as { kilograms: number; recordedOn: string };
    const weightRecordId = newId();
    cat.weights.push({
      id: weightRecordId,
      recordedOn: body.recordedOn,
      kilograms: body.kilograms,
    });
    persist();
    return ok({ catId: cat.id, weightRecordId }, 201);
  }),

  http.get('*/api/cats/:catId', ({ params }) => {
    const profile = profileOf(params.catId as string);
    return profile ? ok(profile) : undefined;
  }),

  http.get('*/api/staff/customers', ({ request }) => {
    const url = new URL(request.url);
    const mobile = url.searchParams.get('mobile');
    const match = CUSTOMERS.find((c) => c.mobileNumber === mobile);
    if (!match) return undefined;
    return ok([
      {
        id: match.id,
        fullName: match.fullName,
        mobileNumber: match.mobileNumber,
        emailAddress: null,
        recordStatus: 'Complete',
        status: 'Active',
        mobileMatchedOn: 'Primary',
      },
    ]);
  }),

  http.get('*/api/staff/customers/:customerId', ({ params }) => {
    const customer = CUSTOMERS.find((c) => c.id === params.customerId);
    if (!customer) return undefined;
    return ok({
      id: customer.id,
      fullName: customer.fullName,
      mobileNumber: customer.mobileNumber,
      emailAddress: null,
      recordStatus: 'Complete',
      registrationChannel: 'WalkIn',
      catCount: customer.catIds.length,
      cats: customer.catIds.map((catId) => ({
        catId,
        name: findCat(catId)?.name ?? 'Cat',
        isDeceased: false,
      })),
      status: 'Active',
      mergedIntoCustomerId: null,
    });
  }),

  // A consent recorded for a SURGERY's appointment. The EMR mock only knows appointments of its own
  // store, so it would refuse a surgery's: this stand-in records the consent in the EMR mock's own
  // database (where the start gate reads it) for a surgery's appointment, and falls through for any other.
  http.post('*/api/staff/consents', async ({ request }) => {
    const body = (await request
      .clone()
      .json()
      .catch(() => null)) as {
      appointmentId?: string;
      catId?: string;
      kind?: 'Surgery' | 'Anaesthesia' | 'Euthanasia';
      procedureDescription?: string;
      method?: 'OnScreenSignature' | 'SignedUpload';
      signedByName?: string;
      signerRelationship?: string;
      signedByCustomerId?: string;
      policyVersionNumber?: number;
    } | null;
    const surgery = body
      ? getDb().surgeries.find((s) => s.appointmentId === body.appointmentId)
      : undefined;
    if (!body || !surgery) return undefined;
    const form = getEmrDb().forms[body.kind ?? ''];
    if (!form || form.versionNumber !== body.policyVersionNumber) return undefined;
    const id = newId();
    getEmrDb().consents.push({
      id,
      version: 1,
      catId: surgery.catId,
      branchId: surgery.branchId,
      appointmentId: surgery.appointmentId,
      kind: body.kind as 'Surgery' | 'Anaesthesia',
      procedureDescription: body.procedureDescription ?? '',
      method: body.method ?? 'OnScreenSignature',
      signedByName: body.signedByName ?? '',
      signerRelationship: body.signerRelationship ?? 'Owner',
      signedByCustomerId: body.signedByCustomerId ?? null,
      signedOn: new Date().toISOString(),
      policyKey: form.policyKey,
      policyVersionNumber: form.versionNumber,
      capturedBy: 'Reception Desk',
      witness: null,
      status: 'Recorded',
      withdrawnOn: null,
      withdrawnBy: null,
      withdrawalReason: null,
      strokes: null,
      fileId: null,
      fileName: null,
      fileContentType: null,
    });
    persistEmr();
    return ok({ consentId: id }, 201);
  }),

  // The drug dialog's batch choice reads the SKU's batches from Inventory; for the formulary mock's
  // SKUs (which Inventory's mock does not know) this answers, otherwise it falls through.
  http.get('*/api/staff/inventory/batches', ({ request }) => {
    const skuId = new URL(request.url).searchParams.get('skuId');
    const rows = pharmacyDb.batches.filter((b) => b.skuId === skuId);
    if (!skuId || rows.length === 0) return undefined;
    const today = todayIn(ids.mainZone);
    const items = rows.map((b) => {
      const status = b.notDispensableReason ? 'Quarantined' : 'Available';
      return {
        id: b.batchId,
        skuId: b.skuId,
        skuCode: `SKU-${b.skuId.slice(-4)}`,
        displayName: `${b.productName} ${b.variantLabel}`.trim(),
        lotNumber: b.lotNumber,
        expiresOn: b.expiresOn,
        isExpired: b.expiresOn < today,
        daysToExpiry: Math.round(
          (new Date(b.expiresOn).getTime() - new Date(today).getTime()) / 86_400_000,
        ),
        branchTimeZoneId: ids.mainZone,
        status,
        receivedQuantity: String(b.available + 5),
        onHand: String(b.available),
        stockUnit: 'Millilitre',
        consignmentId: null,
      };
    });
    return ok({ items, pageNumber: 1, pageSize: 50, totalCount: items.length });
  }),
];
