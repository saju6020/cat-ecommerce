import { http, type HttpHandler } from 'msw';
import type { MySurgeryDetail, MySurgerySummary } from '@/lib/api/my-surgeries';
import { appointmentsFixtureIds } from '../appointments/fixtures';
import { notFound, ok, surgeryFixtureIds as ids, zoneOfBranch } from './fixtures';
import { findCat, findService, getDb, type MSurgery } from './store';

/**
 * `mocks/handlers/surgeries/portal.ts` — `CustomerSurgeriesController`. Like the pharmacy and EMR mocks
 * it does not resolve a customer session: it answers for the signed-in customer (Sam Rahman), whose
 * surgeries are the ones booked for his cat. Another customer's surgery is a neutral `404`. The status
 * is the owner-facing one (`InCare` covers pre-op, in theatre and recovery) and no recovery progress or
 * clinical note is ever returned (HO-SRG-001 OQ-12).
 */
const OWNER = appointmentsFixtureIds.signedInCustomerId;

function ownerStatus(s: MSurgery): MySurgerySummary['status'] {
  if (s.status === 'PreOp' || s.status === 'InTheatre' || s.status === 'Recovery') return 'InCare';
  return s.status as MySurgerySummary['status'];
}

function summaryOf(s: MSurgery): MySurgerySummary {
  return {
    id: s.id,
    catName: findCat(s.catId)?.name ?? 'Cat',
    procedure: findService(s.serviceId)!.name,
    startsOn: s.startsOn,
    admitBy: s.admitBy,
    status: ownerStatus(s),
    branch: s.branchId === ids.londonBranchId ? 'London Road' : 'Uttara',
    branchTimeZoneId: zoneOfBranch(s.branchId),
  };
}

export const portalSurgeryHandlers: HttpHandler[] = [
  http.get('*/api/customers/me/surgeries', ({ request }) => {
    const url = new URL(request.url);
    const scope = url.searchParams.get('scope') ?? 'upcoming';
    const pageNumber = Math.max(1, Number(url.searchParams.get('pageNumber') ?? '1'));
    const pageSize = Math.min(100, Math.max(1, Number(url.searchParams.get('pageSize') ?? '20')));
    const upcoming = (s: MSurgery) => s.status === 'Scheduled' || ownerStatus(s) === 'InCare';
    const rows = getDb()
      .surgeries.filter((s) => s.customerId === OWNER)
      .filter((s) => (scope === 'upcoming' ? upcoming(s) : !upcoming(s)))
      .sort((a, b) =>
        scope === 'upcoming'
          ? a.startsOn.localeCompare(b.startsOn)
          : b.startsOn.localeCompare(a.startsOn),
      )
      .map(summaryOf);
    return ok({
      items: rows.slice((pageNumber - 1) * pageSize, pageNumber * pageSize),
      pageNumber,
      pageSize,
      totalCount: rows.length,
    });
  }),

  http.get('*/api/customers/me/surgeries/:id', ({ params }) => {
    const s = getDb().surgeries.find((x) => x.id === params.id);
    if (!s || s.customerId !== OWNER) return notFound();
    const detail: MySurgeryDetail = {
      ...summaryOf(s),
      catId: s.catId,
      fastingInstructions: s.fastingInstructions,
      postOpInstructions: s.status === 'Discharged' ? s.postOpInstructions : null,
      followUps: s.followUps.map((f) => ({ kind: f.kind, dueOn: f.dueOn, status: f.status })),
      bookFollowUpLink: `https://portal.example.com/book?catId=${s.catId}&category=FollowUp`,
    };
    return ok(detail);
  }),
];
