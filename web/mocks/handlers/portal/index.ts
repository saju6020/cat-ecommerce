import { http, HttpResponse, type HttpHandler, type JsonBodyType } from 'msw';
import { dashboardFor, portalIds, reorderResults, type PortalScenario } from './fixtures';

/**
 * `mocks/handlers/portal/index.ts` — HO-POR-002 §12. The portal dashboard and Orders' re-order, as
 * the merged backend sends them (HO-POR-001 `PortalController`; HO-ORD-001 C15
 * `CustomerOrdersController.Reorder`). `mocks/handlers/areas/portal.ts` stays the empty stub
 * HO-CAT-002 scaffolded (HO-ADM-002's precedent); `mocks/handlers/index.ts` adds this array.
 *
 * Scenario: which dashboard the mock answers with. In the browser a Playwright test sets the
 * `pc_mock_portal` cookie (the handlers run in the page, so a module variable would die on every
 * navigation); in Vitest a test calls `setPortalScenario`. Default `full`.
 *
 * Re-order mirrors the real contract: `Idempotency-Key` (UUID) is required, `400
 * IdempotencyKeyRequired` otherwise; the same key replays the first answer; an unknown order is
 * `404`; the body is only `{ addedCount, unavailable[{ productName, reason }] }`. A repeat with a
 * NEW key answers the fixture again, because the real endpoint re-adds (there is no "already in
 * basket" outcome).
 */
export const PORTAL_SCENARIO_COOKIE = 'pc_mock_portal';

let scenarioOverride: PortalScenario | null = null;
const replays = new Map<string, JsonBodyType>();
export const reorderCalls: Array<{ orderId: string; idempotencyKey: string | null }> = [];

export function setPortalScenario(scenario: PortalScenario | null): void {
  scenarioOverride = scenario;
}

export function resetPortalMock(): void {
  scenarioOverride = null;
  replays.clear();
  reorderCalls.length = 0;
}

function currentScenario(): PortalScenario {
  if (scenarioOverride) return scenarioOverride;
  if (typeof document !== 'undefined') {
    const match = document.cookie
      .split('; ')
      .find((row) => row.startsWith(`${PORTAL_SCENARIO_COOKIE}=`));
    if (match)
      return decodeURIComponent(match.slice(PORTAL_SCENARIO_COOKIE.length + 1)) as PortalScenario;
  }
  return 'full';
}

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function answerFor(orderId: string): { status: number; body?: JsonBodyType } {
  switch (orderId) {
    case portalIds.orders.mixed:
      return { status: 200, body: reorderResults.mixed };
    case portalIds.orders.nothing:
      return { status: 200, body: reorderResults.nothing };
    case portalIds.orders.notFound:
      return { status: 404 };
    case portalIds.orders.refused:
      return {
        status: 400,
        body: {
          isValid: false,
          errors: [
            {
              propertyName: 'OrderId',
              errorCode: 'OrderNotReorderable',
              errorMessage: '',
              resourceName: 'Order',
            },
          ],
        },
      };
    case portalIds.orders.conflict:
      return {
        status: 409,
        body: {
          isValid: false,
          errors: [
            {
              propertyName: 'Cart',
              errorCode: 'StaleVersion',
              errorMessage: '',
              resourceName: 'Cart',
            },
          ],
        },
      };
    default:
      return { status: 200, body: reorderResults.allAdded };
  }
}

export const portalAreaHandlers: HttpHandler[] = [
  http.get('*/api/portal/dashboard', () => {
    const scenario = currentScenario();
    if (scenario === 'failed') return new HttpResponse(null, { status: 500 });
    return HttpResponse.json(dashboardFor(scenario), { headers: { 'Cache-Control': 'no-store' } });
  }),
];

/**
 * Re-order scenario answers for the portal's own Vitest suite ONLY (`tests/components/portal/reorder.test.tsx`
 * calls `server.use(...portalReorderFixtureHandlers)`). Not registered in `mocks/handlers/index.ts`: the app's mock
 * re-order is HO-ORD-002's (`mocks/handlers/ordering/customer.ts`) -- POR backlog 31, deduped at merge 2026-10-13.
 */
export const portalReorderFixtureHandlers: HttpHandler[] = [
  http.post('*/api/customers/me/orders/:orderId/reorder', ({ params, request }) => {
    const orderId = params.orderId as string;
    const key = request.headers.get('Idempotency-Key');
    reorderCalls.push({ orderId, idempotencyKey: key });

    if (!key || !UUID.test(key)) {
      return HttpResponse.json(
        {
          isValid: false,
          errors: [
            {
              propertyName: 'Idempotency-Key',
              errorCode: 'IdempotencyKeyRequired',
              errorMessage: 'An Idempotency-Key header is required.',
              resourceName: 'Request',
            },
          ],
        },
        { status: 400 },
      );
    }

    const replayKey = `${orderId}:${key}`;
    if (replays.has(replayKey)) {
      return HttpResponse.json(replays.get(replayKey), {
        headers: { 'Idempotent-Replayed': 'true' },
      });
    }

    const answer = answerFor(orderId);
    if (answer.status === 200) {
      replays.set(replayKey, answer.body);
      return HttpResponse.json(answer.body);
    }
    return answer.body
      ? HttpResponse.json(answer.body, { status: answer.status })
      : new HttpResponse(null, { status: answer.status });
  }),
];

export { portalIds, reorderResults } from './fixtures';
