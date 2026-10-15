import { http, type HttpHandler } from 'msw';
import type { AuthoriseDietInput } from '@/lib/api/diet-authorisations';
import { findVisit } from '../visits/store';
import {
  callerRoles,
  noContent,
  notFound,
  ok,
  requireAction,
  staffNameFor,
  violation,
  type MockViolation,
} from './fixtures';
import { DIET_PRODUCTS, db, newId, persist, today, type MDiet } from './store';

/**
 * `mocks/handlers/pharmacy/diet.ts` — `StaffDietAuthorisationsController`. The status is the
 * EFFECTIVE one (`Expired` once `validTo` has passed, whether or not a sweep ran); authorising needs
 * an open visit and refuses a second active authorisation for the same food (extend it instead).
 */
const bad = (propertyName: string, errorCode: string, errorMessage: string): MockViolation => ({
  propertyName,
  errorCode,
  errorMessage,
});

export function dietStatus(d: MDiet): 'Active' | 'Expired' | 'Revoked' {
  if (d.revoked) return 'Revoked';
  return d.validTo < today() ? 'Expired' : 'Active';
}

export function dietView(d: MDiet) {
  return {
    id: d.id,
    catId: d.catId,
    productId: d.productId,
    productName: d.productName,
    validFrom: d.validFrom,
    validTo: d.validTo,
    status: dietStatus(d),
    authorisedBy: d.authorisedBy,
    visitId: d.visitId,
    notes: d.notes,
  };
}

export const dietHandlers: HttpHandler[] = [
  http.get('*/api/staff/diet-products', ({ request }) => {
    const denied = requireAction(request, 'DietAuthorisations.Issue');
    if (denied) return denied;
    const search = (new URL(request.url).searchParams.get('search') ?? '').toLowerCase();
    return ok(
      DIET_PRODUCTS.filter((p) => !search || `${p.name} ${p.brand}`.toLowerCase().includes(search)),
    );
  }),

  http.post('*/api/staff/cats/:catId/diet-authorisations', async ({ request, params }) => {
    const denied = requireAction(request, 'DietAuthorisations.Issue');
    if (denied) return denied;
    const catId = params.catId as string;
    const body = (await request.json()) as AuthoriseDietInput;
    const visit = findVisit(body.visitId);
    if (!visit)
      return violation(null, bad('VisitId', 'VisitNotFound', 'That visit could not be found.'));
    if (visit.catId !== catId)
      return violation(
        null,
        bad('VisitId', 'VisitCatMismatch', 'That visit is for a different cat.'),
      );
    if (visit.status !== 'Open')
      return violation(
        null,
        bad(
          'VisitId',
          'VisitNotOpen',
          'This visit has been finalised — authorisations can no longer be added to it.',
        ),
      );
    const product = DIET_PRODUCTS.find((p) => p.productId === body.productId);
    if (!product)
      return violation(
        null,
        bad(
          'ProductId',
          'ProductNotPrescriptionDiet',
          'This food does not need a vet’s authorisation.',
        ),
      );
    if (body.validTo < body.validFrom)
      return violation(
        null,
        bad('ValidTo', 'InvalidDietValidity', 'The end date cannot be before the start date.'),
      );
    if (
      db.diets.some(
        (d) => d.catId === catId && d.productId === body.productId && dietStatus(d) === 'Active',
      )
    )
      return violation(
        null,
        bad(
          'ProductId',
          'DietAuthorisationAlreadyActive',
          'This cat already has an active authorisation for this food — extend it instead.',
        ),
      );
    const created: MDiet = {
      id: newId('e5'),
      catId,
      productId: product.productId,
      productName: product.name,
      validFrom: body.validFrom,
      validTo: body.validTo,
      revoked: false,
      authorisedBy: staffNameFor(callerRoles(request)),
      visitId: body.visitId,
      notes: body.notes?.trim() ? body.notes : null,
    };
    db.diets.push(created);
    persist();
    return ok({ authorisationId: created.id }, 201);
  }),

  http.get('*/api/staff/cats/:catId/diet-authorisations', ({ request, params }) => {
    const denied = requireAction(request, 'DietAuthorisations.View');
    if (denied) return denied;
    const scope = new URL(request.url).searchParams.get('scope') ?? 'active';
    return ok(
      db.diets
        .filter((d) => d.catId === params.catId)
        .filter((d) => scope === 'all' || dietStatus(d) === 'Active')
        .sort((a, b) => b.validTo.localeCompare(a.validTo))
        .map(dietView),
    );
  }),

  http.post('*/api/staff/diet-authorisations/:id/extend', async ({ request, params }) => {
    const denied = requireAction(request, 'DietAuthorisations.Issue');
    if (denied) return denied;
    const d = db.diets.find((x) => x.id === params.id);
    if (!d) return notFound();
    const { validTo } = (await request.json()) as { validTo: string };
    if (d.revoked || validTo <= d.validTo)
      return violation(
        null,
        bad('ValidTo', 'DietAuthorisationNotExtendable', 'Choose a later end date.'),
      );
    d.validTo = validTo;
    persist();
    return noContent();
  }),

  http.post('*/api/staff/diet-authorisations/:id/revoke', async ({ request, params }) => {
    const denied = requireAction(request, 'DietAuthorisations.Issue');
    if (denied) return denied;
    const d = db.diets.find((x) => x.id === params.id);
    if (!d) return notFound();
    const { reason } = (await request.json()) as { reason: string };
    if (!reason?.trim())
      return violation(
        null,
        bad('Reason', 'RevocationReasonRequired', 'Give a reason for revoking.'),
      );
    d.revoked = true;
    persist();
    return noContent();
  }),
];
