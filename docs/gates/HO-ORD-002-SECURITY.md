GATE: SECURITY
HANDOVER: HO-ORD-002
COMMITS: 12543d76 5ef10b1a 1239ce15 62e1bac7 b22c41b9 3d4ea5be ef626a68 cc1c2d59 f4329fe4 05565241 91b6a09c 1da9a352 7eb7504b (a8761829..7eb7504b)
DATE: 2026-10-12
STATUS: PASS

FINDINGS
- [LOW] web/components/orders/StaffOrderPanels.tsx:162 — the staff order screen puts the server's
  `shipment.trackingUrl` straight into `<a href target="_blank">`. The customer screen runs the same value
  through `safeHttpUrl` first (web/components/orders/OrderPanels.tsx:13,66), which allows only http/https.
  The backend validator (src/Application/.../OrderConfigurationCommandValidators.cs:94,104) checks only the
  length and control characters of `TrackingUrlTemplate`. An Administrator/ShopManager could therefore save a
  courier template with a non-http scheme, and staff would see it as a link. Impact is small: only a privileged
  role sets the template, and React 19.3 blocks `javascript:` hrefs. Fix: use `safeHttpUrl` here as well, and
  have the backend accept only absolute http(s) templates. Proposed backlog ORD 63.

Checked, no finding:
- Open redirect: sign-in now also allows `/cart` and `/checkout`. The check still goes through
  `sanitizeNextPath` (web/lib/validation/nextPath.ts): it requires a single leading `/`, rejects
  backslashes and control characters, resolves the value with `new URL` on a fixed origin and compares
  prefixes only at a `/` boundary. The unit test (tests/unit/lib/ordering-next-path.test.ts) covers
  absolute, protocol-relative, backslash, lookalike-prefix and staff-route values. Every other new
  `router.push`/`replace` call uses a fixed path or a server-issued id.
- XSS: no `dangerouslySetInnerHTML`, `innerHTML`, `srcDoc`, `eval` or `new Function` in any changed source.
  Addresses, notes, return reasons, tracking status and bank details are rendered as React text.
  Customer-facing tracking links use the `safeHttpUrl` http/https allow-list.
- CSRF and cookies: every write goes through `apiFetch`. The CSRF and branch identity headers are spread
  last, so a caller cannot override them. The new `Idempotency-Key` header is a per-component
  `crypto.randomUUID()` held in a ref: nothing is stored or replayed. The guest cart identity is the API's
  HttpOnly `pc_cart` cookie and is never read in JavaScript. The manifest CSV download uses the single
  `downloadBillingFile` path. API paths encode their ids; query strings are built with a helper.
- Payment data: no card-number field exists. POS "Card in store" records the amount and the terminal slip
  reference only, and a guard test (tests/guards/no-card-ui.test.ts) enforces this. Bank-transfer details
  come from the server and are only displayed or copied.
- PII storage and logging: no `localStorage`, `sessionStorage`, `console.*`, `document.cookie` writes or
  analytics/telemetry calls in changed app code. Mock persistence lives only under `web/mocks/**`.
- Role gating: the admin nav role lists are presentation only (as commented). The pages handle
  `ApiForbiddenError` with `AccessRefusedState`, so the UI never stands in for server checks. Server
  authorization for these endpoints was reviewed in HO-ORD-001-SECURITY. Checkout and My Orders are under
  `(portal)`, inside `CustomerSessionGuard`.
- MSW cannot ship to production: the browser worker starts only when `NODE_ENV==='development' &&
  NEXT_PUBLIC_USE_MOCKS==='true'` (app/providers.tsx:112), and the Node server has the same condition
  (instrumentation.ts). `window.__orderingMock` is installed only when `NODE_ENV!=='production'`, and
  only from the mock handler module. No app, component, lib or nav file imports `@/mocks`.
  `public/mockServiceWorker.js` changed only in formatting (the generated MSW 2.15.0 file, same checksum)
  and is never registered outside mock mode.
- Dependencies: no `package.json` change.

PROPOSED BACKLOG (docs/backlog/ORD.md, highest existing row is 62)
| 63 | LOW | SEC gate HO-ORD-002: staff order Shipment panel renders `trackingUrl` without the `safeHttpUrl` http(s) allow-list the customer panel uses (StaffOrderPanels.tsx:162); backend `TrackingUrlTemplate` validator checks only length and control characters, so it should also require an absolute http(s) URL. |

SCOPE CHECK
- files in allowed scope: 163/163 (web/**, docs/test-cases/TC-HO-ORD-002.md, web/docs/decisions.md)
- files outside allowed scope: none

POINTER (2026-10-13, ACCEPTANCE row ORD-98): the findings above were closed in the gate fix round and re-checked in the combined re-check appended to `HO-ORD-002-ARCHITECTURE.md` (commit 64fa94ce, PASS). This record is the historical first-pass result; its STATUS line is not updated.
