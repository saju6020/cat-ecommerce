import { http, type HttpHandler } from 'msw';
import { noContent, notFound, ok, quantity, violation } from './fixtures';
import { db, findMedicine, findPrescription, localDay, newId, persist } from './store';
import { OWNED_CAT_IDS, catNameOf, evaluateRepeat, isActive, myPrescription } from './views';
import { dietStatus } from './diet';

/**
 * `mocks/handlers/pharmacy/portal.ts` — `CustomerPrescriptionsController` +
 * `CustomerDietAuthorisationsController`. The mock does not resolve a customer session (the EMR
 * mock's precedent): it answers for the signed-in customer, who owns `OWNED_CAT_IDS`. A prescription
 * on anyone else's cat is a neutral `404`. No batch or lot number is ever returned. A refused repeat
 * is a `400` whose message says when it ended, with a `bookLink` (US-PHM-06 AC2). The lists are plain
 * arrays (the real endpoints are not paged).
 */
const owned = (catId: string | undefined) => !!catId && OWNED_CAT_IDS.has(catId);

export const portalPharmacyHandlers: HttpHandler[] = [
  http.get('*/api/customers/me/prescriptions', ({ request }) => {
    const url = new URL(request.url);
    const scope = url.searchParams.get('scope') ?? 'active';
    const catId = url.searchParams.get('catId');
    return ok(
      db.prescriptions
        .filter((p) => owned(p.catId))
        .filter((p) => !catId || p.catId === catId)
        .filter((p) => (scope === 'active' ? isActive(p) : !isActive(p)))
        .sort((a, b) => b.issuedOn.localeCompare(a.issuedOn))
        .map(myPrescription),
    );
  }),

  http.get('*/api/customers/me/prescriptions/:id', ({ params }) => {
    const p = findPrescription(params.id as string);
    if (!p || !owned(p.catId)) return notFound();
    const medicine = findMedicine(p.medicineId)!;
    return ok({
      ...myPrescription(p),
      instructions: p.instructions,
      dispensings: db.dispensings
        .filter((d) => d.prescriptionId === p.id && d.status === 'Dispensed')
        .map((d) => ({
          dispensedOn: localDay(d.dispensedOn),
          branchName: d.branchId.endsWith('a002') ? 'London Road' : 'Uttara',
          quantity: quantity(
            d.lines.reduce((s, l) => s + l.quantity, 0),
            medicine.dispensingUnit,
          ),
        })),
      bookLink: `https://portal.example.com/book?catId=${p.catId}`,
    });
  }),

  http.post('*/api/customers/me/prescriptions/:id/repeat-requests', async ({ params, request }) => {
    const p = findPrescription(params.id as string);
    if (!p || !owned(p.catId)) return notFound();
    const body = (await request.json()) as { preferredBranchId: string; note?: string | null };
    const eligibility = evaluateRepeat(p);
    if (!eligibility.ok) {
      return violation(
        { bookLink: `https://portal.example.com/book?catId=${p.catId}` },
        {
          propertyName: 'PrescriptionId',
          errorCode: eligibility.code ?? 'NotRepeatable',
          errorMessage: eligibility.reason ?? '',
        },
      );
    }
    const id = newId('e4');
    p.repeatRequests.push({
      id,
      requestedOn: new Date().toISOString(),
      preferredBranchId: body.preferredBranchId,
      note: body.note?.trim() ? body.note : null,
      status: 'Requested',
      decidedOn: null,
      declineReason: null,
      dispensingId: null,
    });
    persist();
    return ok({ repeatRequestId: id }, 201);
  }),

  http.delete('*/api/customers/me/prescriptions/:id/repeat-requests/:requestId', ({ params }) => {
    const p = findPrescription(params.id as string);
    if (!p || !owned(p.catId)) return notFound();
    const repeat = p.repeatRequests.find((r) => r.id === params.requestId);
    if (!repeat || repeat.status !== 'Requested') return notFound();
    repeat.status = 'Withdrawn';
    repeat.decidedOn = new Date().toISOString();
    persist();
    return noContent();
  }),

  http.get('*/api/customers/me/diet-authorisations', ({ request }) => {
    const url = new URL(request.url);
    const scope = url.searchParams.get('scope') ?? 'active';
    return ok(
      db.diets
        .filter((d) => owned(d.catId))
        .filter((d) =>
          scope === 'active' ? dietStatus(d) === 'Active' : dietStatus(d) !== 'Active',
        )
        .map((d) => ({
          id: d.id,
          catId: d.catId,
          catName: catNameOf(d.catId),
          productId: d.productId,
          productName: d.productName,
          validFrom: d.validFrom,
          validTo: d.validTo,
          status: dietStatus(d),
          authorisedByName: d.authorisedBy,
        })),
    );
  }),
];
