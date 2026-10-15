import { http, type HttpHandler } from 'msw';
import { appointmentsFixtureIds } from '../appointments/fixtures';
import { emrFixtureIds as ids, notFound, ok } from './fixtures';
import { filePayload, findVisit, getDb, CAT_BASICS } from './store';
import { dischargeSummaryFor } from './summary';
import { pdfResponse } from './staff';

/**
 * `mocks/handlers/visits/portal.ts` — HO-EMR-002 §12, `CustomerVisitsController`. The mock does not
 * resolve a customer session (HO-ADM-002's precedent): it answers for the signed-in customer, whose
 * only cat here is `portalCatId`. A visit that is open, or on another customer's cat, is the SAME
 * neutral `404` as one that does not exist (FR-POR-3). Attachments exclude DICOM (the real query
 * does) and are signed URLs.
 */

const OWNED_CAT_IDS = new Set<string>([ids.portalCatId, appointmentsFixtureIds.catMomoId]);

function finalisedVisitsOf(catId: string) {
  return getDb().visits.filter((v) => v.catId === catId && v.status === 'Finalised');
}

export const portalVisitHandlers: HttpHandler[] = [
  http.get('*/api/customers/me/cats/:catId/visits', ({ params }) => {
    const catId = params.catId as string;
    if (!OWNED_CAT_IDS.has(catId) || !CAT_BASICS[catId]) return notFound();
    return ok(
      finalisedVisitsOf(catId)
        .sort((a, b) => b.openedOn.localeCompare(a.openedOn))
        .map((v) => ({
          id: v.id,
          date: v.openedOn.slice(0, 10),
          service: v.serviceName,
          vet: v.finalisedBy ?? v.openedBy,
          problems: v.problems.map(
            (p) => getDb().codes.find((c) => c.id === p.diagnosisCodeId)?.name ?? '',
          ),
          hasSummary: true,
        })),
    );
  }),

  http.get('*/api/customers/me/visits/:visitId/discharge-summary.pdf', ({ params }) => {
    const visit = findVisit(params.visitId as string);
    return visit && visit.status === 'Finalised' && OWNED_CAT_IDS.has(visit.catId)
      ? pdfResponse()
      : notFound();
  }),

  http.get('*/api/customers/me/visits/:visitId', ({ params }) => {
    const visit = findVisit(params.visitId as string);
    if (!visit || visit.status !== 'Finalised' || !OWNED_CAT_IDS.has(visit.catId))
      return notFound();
    return ok(dischargeSummaryFor(visit));
  }),

  http.get('*/api/customers/me/cats/:catId/lab-results', ({ params }) => {
    const catId = params.catId as string;
    if (!OWNED_CAT_IDS.has(catId) || !CAT_BASICS[catId]) return notFound();
    const visitIds = new Set(finalisedVisitsOf(catId).map((v) => v.id));
    return ok(
      getDb()
        .orders.filter((o) => visitIds.has(o.visitId))
        .map((o) => ({
          orderId: o.id,
          visitId: o.visitId,
          kind: o.kind,
          orderedOn: o.orderedOn,
          resultSets: o.resultSets,
          attachments: o.attachments
            .filter((a) => a.contentType !== 'application/dicom')
            .map((a) => ({
              id: a.id,
              resultDate: a.resultDate,
              note: a.note,
              attachedBy: a.attachedBy,
              attachedOn: a.attachedOn,
              file: filePayload(a.fileId, a.fileName, a.contentType),
            })),
        })),
    );
  }),
];
