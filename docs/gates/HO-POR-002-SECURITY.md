GATE: SECURITY
HANDOVER: HO-POR-002
COMMITS: 4c2cd1a2 00a0e164 e2c80176 f39f0fa8 c38e6bf4 0f3497b9 f38ebba3 (b12fd39d..f38ebba3)
DATE: 2026-10-13
STATUS: PASS

FINDINGS
- none (no BLOCKER, HIGH, MEDIUM or LOW). No proposed docs/backlog/POR.md rows; the highest row stays 28.

CHECKS (what was verified, so a later gate need not repeat it)
- Session guard: web/app/(portal)/portal/page.tsx sits under the unchanged (portal)/layout.tsx, whose
  CustomerSessionGuard wraps {children}. The data itself comes only from the cookie-authenticated
  GET /api/portal/dashboard, so the guard is UX; the server is the boundary.
- IDOR: the frontend sends no owner or customer id. The dashboard is caller-scoped
  (/api/portal/dashboard); re-order is /api/customers/me/orders/{orderId}/reorder, and the real-API
  spec pins 404 for an order belonging to someone else. Ids in URLs are encodeURIComponent'd
  (web/lib/api/portal.ts reorderOrder; web/lib/routing/portal-links.ts every *Href helper;
  bookVaccinationHref uses URLSearchParams).
- CSRF: reorderOrder goes through apiFetch (credentials: 'include', X-CSRF-Token from pc_csrf on
  unsafe methods, web/lib/api/client.ts:243-262). No raw fetch in app code.
- Idempotency-Key: web/lib/hooks/useReorder.ts uses the shared useIdempotencyKey (crypto.randomUUID()
  in a useRef). It is kept across a network/5xx retry, renewed after a success or a 4xx, and never
  written to storage, the URL, logs or a query key.
- XSS: every server string (cat, customer, product, service, vet, branch names; howToPay; order and
  invoice numbers) is rendered as a React text node. There is no dangerouslySetInnerHTML or innerHTML,
  and no server-supplied href. Every Link href is a constant or an encoded-id helper in
  portal-links.ts, so the portal links cannot be used for an open redirect.
- Storage, logs and analytics: there is no localStorage, sessionStorage, console call or analytics
  under components/portal/**, lib/api/portal.ts or the two hooks. The dashboard lives only in the
  TanStack Query cache.
- Clinical data: the tiles render only what the caller-scoped endpoint returns. The prescriptions
  tile shows a count and an end date, never a medicine or dose (ADR-262). The merged HO-POR-001
  readers exclude deceased cats server-side
  (src/Read/.../Portal/Sections/{Cats,Prescriptions,Visits}SectionReader.cs IncludeDeceased = false).
  The frontend correctly adds no filter of its own.
- No payment UI: PortalBalances has no URL/action field; howToPay is guidance text. The real-API spec
  asserts zero /pay/ buttons.
- MSW: web/mocks/handlers/portal/** is reached only via web/mocks/handlers/index.ts, which is loaded
  only by the dynamic import('@/mocks/browser') in app/providers.tsx. That import is gated on
  NODE_ENV === 'development' && NEXT_PUBLIC_USE_MOCKS === 'true', so it is not in a production
  bundle. No app/components/lib file imports from @/mocks.
- Real-API spec (web/tests/e2e/portal-real-api.spec.ts): it registers throwaway customers with a
  literal test password and an env-overridable CAPTCHA test answer. It contains no real credentials,
  tokens or connection strings. The one console.log prints a synthetic cat item.

SCOPE CHECK
- files in allowed scope (HO-POR-002 §13): 31/34
- files outside allowed scope: web/nav/areas/portal.ts (Home nav entry; §13 names layout.tsx, and
  the deviation is documented in the file comment), docs/backlog/POR.md, docs/handover/HO-POR-002.md
  (§1 Amended row). None of these has a security impact; scope ruling is left to ARCHITECTURE.
