import { http, HttpResponse } from 'msw';
import { server } from '../../../mocks/server';
import { appointmentsFixtureIds as ids } from '../../../mocks/handlers/appointments';

/**
 * The HO-ACC-002 customer mock refuses a request with no staff session cookie, and a Vitest `fetch`
 * carries none — so tests that exercise the customer lookup serve the three customer endpoints
 * themselves (same shapes as `StaffCustomersController`). Sam Rahman has one cat, Momo.
 */
export function serveStaffCustomers() {
  const created = new Map<string, { id: string; fullName: string; mobileNumber: string }>();
  server.use(
    http.get('*/api/staff/customers', ({ request }) => {
      const mobile = new URL(request.url).searchParams.get('mobile');
      if (mobile !== '01799999999') return HttpResponse.json([]);
      return HttpResponse.json([
        {
          id: ids.signedInCustomerId,
          fullName: ids.signedInCustomerName,
          mobileNumber: '01799999999',
          emailAddress: null,
          recordStatus: 'Complete',
          status: 'Active',
          mobileMatchedOn: 'Primary',
        },
      ]);
    }),
    http.post('*/api/staff/customers', async ({ request }) => {
      const body = (await request.json()) as { fullName: string; mobileNumber: string };
      const id = crypto.randomUUID();
      created.set(id, { id, fullName: body.fullName, mobileNumber: body.mobileNumber });
      return HttpResponse.json({ customerId: id }, { status: 201 });
    }),
    http.get('*/api/staff/customers/:customerId', ({ params }) => {
      const id = params.customerId as string;
      const isSam = id === ids.signedInCustomerId;
      const fresh = created.get(id);
      if (!isSam && !fresh) return new HttpResponse(null, { status: 404 });
      return HttpResponse.json({
        id,
        fullName: isSam ? ids.signedInCustomerName : fresh!.fullName,
        mobileNumber: isSam ? '01799999999' : fresh!.mobileNumber,
        emailAddress: null,
        recordStatus: 'Complete',
        registrationChannel: 'Reception',
        catCount: isSam ? 1 : 0,
        cats: isSam ? [{ catId: ids.catMomoId, name: 'Momo', isDeceased: false }] : [],
        status: 'Active',
        mergedIntoCustomerId: null,
      });
    }),
  );
}
