GATE: ARCHITECTURE
HANDOVER: HO-POR-002
COMMITS: 4c2cd1a2 00a0e164 e2c80176 f39f0fa8 c38e6bf4 0f3497b9 f38ebba3 (range b12fd39d..f38ebba3, branch ho/POR-002, 34 files)
DATE: 2026-10-13
STATUS: PASS

## Basis

- I reviewed the range with `git diff b12fd39d..f38ebba3` and read every changed source, mock and docs file. I read the tests only for scope and for fixture shape.
- I read the diff against these sources:
  - HO-POR-002 §1 (all three Amended rows), §2 and §11.5–§20;
  - HO-POR-001 §1 Amended (2026-10-09, 2026-10-10) and OQ-9;
  - HO-ACC-002 §8.2 and §8.5 (layering, unchanged from HO-CAT-002 §8.2);
  - HO-ORD-001 ADR-202 (idempotency, `409 RequestInProgress`);
  - `web/docs/decisions.md` ADR-FE-119…123 and 126;
  - `web/nav/index.ts` and `web/README.md` "Adding a functional area";
  - the `docs/design/portal/` boards.
- I applied the earlier frontend §13/§14 rulings: HO-ORD-002, HO-APT-002, HO-ADM-002 and HO-SHP-002 ARCHITECTURE.
- The diff is web-only and has no `src/**` change, so the `.ai/` backend checklist (aggregates, dispatch, EF Core, host) has nothing to apply to.
- This gate built nothing and ran nothing (no Playwright, no suites). Runtime claims are marked for TEST.
- Owner rule (2026-09-27): only BLOCKER and HIGH fail a gate. MEDIUM and LOW findings are proposed below as `docs/backlog/POR.md` rows. The highest row on this branch is 28, so the proposals start at 29. SECURITY and CODE_REVIEW run at the same time, so the integrator may renumber.

## Ruling: the handover has no §3–§11.4

**Acceptable, with a MEDIUM against the handover (row 29).** It does not make this gate BLOCKED.

The file has lacked §3–§11.4 since it was first written (`529ff4fd`, 220 lines, no §3–§11 heading), so the builder did not remove anything. The gap leaves out:
- §6, the governing architecture;
- §8, the layering that §19 points the gate at;
- §9, the ADR texts;
- §11, the screen specs.

The rules that decide this review are still recoverable without them:
- **Scope** is fully stated in §13 and §14.
- **Stop conditions** are in §20.1–§20.7.
- **The wire contract** is HO-POR-001 §12.4, which §1 makes authoritative, and the merged backend wins over it (§20.4, ADR-FE-126).
- **Layering** is inherited through "Builds on" from HO-CAT-002/HO-ACC-002 §8.2.
- **Behaviour** is pinned by §15, §16 and §18.
- **The design boards** exist under `docs/design/portal/`.

ADR-FE-119…123 had no handover text, so the builder wrote them. Each one restates a constraint that already exists elsewhere, and none of them loosens it:

| ADR | Constraint it restates |
|---|---|
| ADR-FE-119 | §19 "one dashboard request" |
| ADR-FE-120 | U-A2, U-A3 and A4 |
| ADR-FE-121 | U-A10 and §20.5 |
| ADR-FE-122 | U-A4, A11 and §20.3 |
| ADR-FE-123 | §20.7 |

ADR-FE-124 and ADR-FE-125 are retired consistently with README C-6 and the 2026-10-10 Amended row. I found no rule that I had to invent.

## What was verified (compliant)

**Layering (HO-CAT-002/HO-ACC-002 §8.2, §8.5)**
- The API is one module, `lib/api/portal.ts`.
- The queries and the mutation are in `lib/hooks/usePortalDashboard.ts` and `useReorder.ts`.
- There is no `useQuery`, `useMutation`, `useQueryClient` or `fetch(` in the new `components/portal/**` or `app/(portal)/portal/page.tsx`. The only `useQueryClient` hit is the pre-existing, unchanged `CustomerSessionGuard.tsx`. Query-key knowledge, including the `['cart']` invalidation, stays in the hook. This avoids the HO-ORD-002 MEDIUM 64 pattern.
- `page.tsx` is a thin shell.
- Nothing in the diff writes to `localStorage`, `sessionStorage`, `document.cookie` or IndexedDB. The only cookie read is the MSW scenario switch in `mocks/handlers/portal/index.ts`.
- There is no `dangerouslySetInnerHTML` and no `console.*`.
- No dependency was added. `package.json`, the lockfile, `tsconfig.json`, `next.config.ts` and the eslint config are untouched. The imports are `react`, `next/*`, `lucide-react` and `@tanstack/react-query` only.
- `Idempotency-Key` is passed through `apiFetch`'s `headers`. `client.ts:266-267` spreads the identity headers last, so a caller cannot override CSRF.

**One request, one shell (ADR-FE-119, ADR-FE-120)**
- `GET /api/portal/dashboard` feeds all six tiles, and every tile goes through `DashboardSection`.
- `DashboardSection` handles the three states as follows:
  - an absent key shows "Not available yet" with no retry;
  - `Unavailable` (or `data: null`) shows "couldn't load … just now" with **Try again**;
  - `Ready` renders the tile.

  This matches HO-POR-001 §1 Amended 2026-10-09: "treat a missing section key as not delivered yet, never as empty or unavailable".
- There is no stays tile and no subscriptions tile, not even as a placeholder, as the 2026-10-10 Amended row (1) requires.
- The single second request is `GET /api/branches`, used for the clinic phone numbers. HO-POR-001 OQ-9 sanctions it: "the FE shows branch contacts from HO-ADM-001 `GET /api/branches`". It is decorative (`retry: false`, and a failure hides the list), so it is not a §20.2 fill-in.

**No client-side workaround for missing backend data (the HO-ORD-002 HIGH)**
- **Re-order** goes through Orders' existing endpoint, as Amended 2026-10-10 (2) directs.
  - The dialog renders only `addedCount` and `unavailable[{ productName, reason }]`.
  - It does not invent an added list, quantities, a price-change note or "Already in your basket".
  - Eligibility is the server's `canReorder` alone (`ReorderButton.tsx:27`, `OrdersTile.tsx:56-60`).
  - The client checks no stock, prescription or basket rule (§20.7).
- **Gaps are recorded as backlog rows, not shimmed.** The missing added list, the open reason set, the unenforced `canReorder` and the count cap are POR rows 21–24. Absent data is shown as less, never computed.
- **The open-invoice cap** (`BalancesTile.tsx:22`, `OPEN_INVOICE_SCAN_CAP = 500`) is the one inference. At exactly 500 it prints "500 or more", which is provably true; otherwise it prints the server's count. It produces no wrong answer about someone else's data, and §1 Amended (4) and row 24 record it. This is LOW (row 33), not the HO-ORD-002 shape.

**ADR-FE-121 (links)**
- Every path the tiles emit comes from `lib/routing/portal-links.ts`. No component writes a path string of its own.
- These targets exist on the base:
  - `/portal/cats/**`
  - `/portal/appointments/[id]`
  - `/portal/billing/invoices/[id]`
  - `/portal/visits/[visitId]`
  - `/portal/cats/[catId]/visits`
  - `/book`
  - `/shop`
- `/portal/orders/**`, `/portal/prescriptions` and `/cart` do not exist yet. They are §20.5 constants, recorded in row 28 and the TC sheet. No other area's page was rebuilt.

**ADR-FE-122 (no payment surface)**
- The balances tile shows amounts per currency, never summed. It shows `howToPay` as server text, the clinic phone numbers and invoice links.
- There is no payment button, link or URL field.

**Warm customer layer**
- `/portal` sits inside `(portal)`, whose layout sets `data-theme="customer"` (`app/(portal)/layout.tsx:17`). The new components use the warm tokens (`bg-warm-soft`, `text-warm-ink`) and the shared surface tokens.
- No staff route or staff component was touched.

**Contract differences (§1 Amended 2026-10-13, ADR-FE-126)**
- Items (1)–(8) of ADR-FE-126 match the types in `lib/api/portal.ts` and the MSW fixtures:
  - no `stays` or `subscriptions` key;
  - `PortalSection<T> { state, data: T|null }`;
  - per-cat `vaccinationState`;
  - `followUpOn`, `trackingNumber` and `canReorder`;
  - re-order body `{ addedCount, unavailable[] }`;
  - a required UUID `Idempotency-Key`.
- §20.4's "report, don't shim" is honoured.
- The Amended row records contract facts and the builder's own scope restraint. It does **not** grant the builder any §14 file, and no §14 file was edited in reliance on it. So the HO-ORD-002 rule ("the builder's own say-so does not authorise a §14 edit") is not engaged.

**Not edited, by design:** `web/app/(auth)/sign-in/page.tsx` and `web/app/page.tsx` (HO-ACC-002) are untouched, which is correct under §14. As a result, customers still land on `/portal/cats`. That is an owner question and is already POR row 26. It is not a finding against the build.

## Scope rulings

| File | Ruling |
|---|---|
| `web/nav/areas/portal.ts` | **Accepted, LOW (row 27 already records it).** §13 names `(portal)/layout.tsx` "Home nav entry only", but the layout holds no links: `PortalHeader` renders `navRegistry`. The file's own header says it is "owned entirely by the My PurrCare area — the ONE file that area adds to", and `nav/index.ts` says areas populate `items` there. HO-APT-002 (LOW, `nav/areas/appointments.ts`) and HO-ORD-002 (accepted, `nav/areas/orders.ts`) set the precedent. The edit is one item (+1/−1 plus a comment). "First in the list" cannot be met without `nav/index.ts`, which is §14, and row 27 puts that to the owner. |
| `web/mocks/handlers/index.ts` | **In §13 literally** (`web/mocks/handlers/portal/** · web/mocks/handlers/index.ts`). +4 lines: one import and one spread. `areas/portal.ts` stays the scaffold stub, the same aliasing that `admin/`, `appointments/`, `billing/` and others use. |
| `web/lib/api/errors.ts` | **In §13 ("append").** It adds `ApiNotFoundError` to the import list, and everything else is appended at the end of the file. HO-ORD-002 accepted the same import-only touch. The §11.5 names (`OrderNotFound`, `OrderNotReorderable`, `StayNotFound`, `ReorderLineOutcome`) were superseded by Amended 2026-10-10 (2), and the appended `describeReorderReason`/`describeReorderError` follow Orders' contract as that row directs. |
| `docs/handover/HO-POR-002.md` | **Accepted.** It is a §1 Amended row, append-only (+1). The HO-ADM-002 and HO-APT-002 precedent applies: "covered by the §1 row itself". |
| `docs/backlog/POR.md` | **Accepted.** Rows 21–28 are appended under the owner rule of 2026-09-27. |

## FINDINGS

- [MEDIUM] docs/handover/HO-POR-002.md:24-42 (proposed row 29)
  - **The defect.** The handover has no §3–§11.4. That leaves no §6 (governing architecture), no §8 (layering, which §19 names as this gate's focus), no §9 ADR text and no §11 screen specs. §11.5 then begins mid-document.
  - **The consequences.** ADR-FE-119…123 were authored by the builder without a handover source. §19's "§8 layering" points at nothing.
  - **The ruling.** Acceptable for this build (see the ruling above).
  - **To fix.** Add an Amended row that points §6/§8 at HO-CAT-002/HO-ACC-002 §8.2, names `web/docs/decisions.md` as the ADR text, and names the `docs/design/portal/` boards as the screen spec. Also correct §1 "Approved design: None".
- [MEDIUM] web/lib/hooks/useReorder.ts:33-37 (proposed row 30; for CODE_REVIEW and TEST)
  - **What the code does.** `onError` renews the `Idempotency-Key` on any 4xx, including `409`. HO-ORD-001 ADR-202 and U-X2 define `409 RequestInProgress` as "the same key is still being processed", which is not a final answer.
  - **The path to a double add.**
    1. A first POST loses its response (status 0, so the key is kept).
    2. The retry with the same key gets `409 RequestInProgress`, and the key is renewed.
    3. The dialog says "Please try again" (`errors.ts` `describeReorderError`).
    4. The next click sends a new key and re-adds every line after the first request commits.
  - **Why it is a defect.** This contradicts ADR-FE-123's own "renewed after an answer" and §18 A7, which says re-ordering must not duplicate the basket.
  - **Why only MEDIUM.** It is narrow: the `inFlight` ref already blocks the ordinary double click.
  - **To fix.** Keep the key on `409 RequestInProgress`, or on every 409, and renew only on `200`, `400` and `404`.
- [LOW] web/lib/api/portal.ts:157-175, web/lib/hooks/useReorder.ts:20, web/mocks/handlers/portal/index.ts:100 (proposed row 31)
  - **The overlap.** `reorderOrder`, `CART_QUERY_KEY` and the `POST */api/customers/me/orders/:orderId/reorder` mock duplicate what the unmerged `ho/ORD-002-build` defines: `lib/api/my-orders.ts:189`, `useMyOrders.ts:93` and `mocks/handlers/ordering/customer.ts:620`.
  - **Why it is only LOW.** The portal module's comment acknowledges it ("only because HO-ORD-002's `lib/api/my-orders.ts` does not exist yet").
  - **The risk.** Once both branches are merged, two API functions call one endpoint, and two MSW handlers answer it. The one registered first wins, and the ordering handlers register before `portalAreaHandlers`.
  - **To fix.** Whichever branch merges second should delete the portal copy and import Orders'.
- [LOW] web/components/portal/reorder/ReorderButton.tsx:25 (proposed row 32)
  - **What the code does.** `WHY_NOT = 'Available once this order has been delivered or closed.'` paraphrases Orders' `CanReorder` rule on the client.
  - **Why it is only LOW.** It is an explanation, not a gate.
  - **The risk.** It will drift if Orders changes the set. HO-POR-001 recorded the set as `Cancelled`, `Delivered`, `Returned` and `Refunded`.
  - **To fix.** Use a neutral sentence ("This order can't be ordered again yet"), or have Orders send a reason.
- [LOW] web/components/portal/dashboard/BalancesTile.tsx:20-22,45-51 (proposed row 33; complements row 24)
  - **What the code does.** `OPEN_INVOICE_SCAN_CAP = 500` hard-codes the backend reader's `5 × 100` scan limit in the client.
  - **Why it is only LOW.** Its output is truthful and it is documented in §1 Amended (4) and ADR-FE-126 (6).
  - **To fix.** Delete the constant when row 24's `openInvoiceCountIsLowerBound` lands.

No template, `.ai/` or backend artifact is in the diff, so there are no template-defect or backend-checklist findings.

## Proposed backlog rows (docs/backlog/POR.md; the highest existing row is 28)

| # | Sev | Item |
|---|---|---|
| 29 | MEDIUM | HO-POR-002 has no §3–§11.4 (no §6, §8, §9 or §11) and says "Approved design: None" although `docs/design/portal/` exists. Amend §1 to point §6/§8 at HO-CAT-002/HO-ACC-002 §8.2, ADRs at `web/docs/decisions.md`, and screens at the design boards. |
| 30 | MEDIUM | `useReorder.ts:33-37` renews the `Idempotency-Key` on `409 RequestInProgress`, so a "Try again" after a lost response can re-add the order (ADR-202, A7). Keep the key on 409 and renew only on 200, 400 and 404. TEST: add a component case. |
| 31 | LOW | Reconcile with HO-ORD-002 on merge: drop `reorderOrder` and `CART_QUERY_KEY` from the portal modules and the duplicate `POST …/reorder` MSW handler in `mocks/handlers/portal/index.ts`, and import Orders' instead. |
| 32 | LOW | `ReorderButton.tsx:25`: the client paraphrases Orders' `CanReorder` rule. Use neutral text, or ask ORD for a reason field. |
| 33 | LOW | `BalancesTile.tsx:22`: drop `OPEN_INVOICE_SCAN_CAP` when row 24's lower-bound flag ships. |

## Marked for TEST (not observable here)

- Typecheck of the `errors.ts` import (`ApiNotFoundError` exported by `client.ts`).
- The re-order idempotency behaviour against the real API (row 30).
- The U-A9 layout shift.
- E-X1 and E-X2 at 360–1440 px.
- E-X3 LCP measured, not estimated.

## SCOPE CHECK

- files in allowed scope: 31/34 by §13's literal globs. The remaining 3 are accepted on recorded authority or precedent:
  - `web/nav/areas/portal.ts`: the area-owned nav registry (HO-APT-002/HO-ORD-002 precedent, row 27);
  - `docs/handover/HO-POR-002.md`: §1 Amended row, append-only;
  - `docs/backlog/POR.md`: owner rule 2026-09-27.
- files outside allowed scope: none. No §14 file was edited: no `src/**`, `tests/**`, `.ai/**`, HO-ACC-002, HO-CAT-002 or other-area file beyond §13, and no `package.json`, `tsconfig.json`, `next.config.ts` or eslint config.
