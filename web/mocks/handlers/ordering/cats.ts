import { http, HttpResponse, type HttpHandler } from 'msw';
import { orderingIds as ids } from './fixtures';

/**
 * `mocks/handlers/ordering/cats.ts` - HO-ORD-002 §12/§13. The POS asks `GET /api/cats?ownerId=<customer>` for
 * the cats of the customer it attached. The accounts mock's customer is a different person from the
 * cat-profiles mock's owner, so without this the picker would have no cat to offer. This answers only for
 * that one owner id (the same two cats the Rx products are authorised for, under the cat-profiles ids) and
 * returns `undefined` - falling through to the cat-profiles handler - for every other request. It is
 * registered ahead of the cat-profiles handlers in `mocks/handlers/index.ts`; cat-profiles itself is untouched.
 */
const POS_CATS = [
  { id: '20000000-0000-0000-0000-000000000001', name: 'Whiskers' },
  { id: '20000000-0000-0000-0000-000000000004', name: 'Mittens' },
];

export const orderingCatsHandlers: HttpHandler[] = [
  http.get('*/api/cats', ({ request }) => {
    const url = new URL(request.url);
    if (url.searchParams.get('ownerId') !== ids.customerId) return undefined;
    const items = POS_CATS.map((cat) => ({
      id: cat.id,
      ownerId: ids.customerId,
      name: cat.name,
      species: 'Cat',
      breed: null,
      sex: 'Female',
      isHighRisk: false,
      handlingNote: null,
      lifeStatus: 'Alive',
    }));
    return HttpResponse.json({ items, pageNumber: 1, pageSize: 50, totalCount: items.length, totalPages: 1 });
  }),
];
