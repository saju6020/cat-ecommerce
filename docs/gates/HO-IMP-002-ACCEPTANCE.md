GATE: ACCEPTANCE
HANDOVER: HO-IMP-002
COMMITS: ho/IMP-002 head ef4315b3. The handover's own changes are `git diff 0b0ce849^2..HEAD` (175 files; 0b0ce849 is the merge of master d97e42c4, the merge-base). Code last changed at 824d756d/54ee1ceb. `git diff a301affb..HEAD --stat` touches only docs/backlog/IMP.md, docs/gates/HO-IMP-002-TEST.md and docs/test-cases/TC-HO-IMP-002.md, so everything after the R2 review is docs only.
DATE: 2026-10-09
STATUS: PASS

PRIOR GATES (from docs/gates/ and docs/test-cases/, not from commit messages)
- ARCHITECTURE: round 1 FAIL (HO-IMP-002-ARCHITECTURE.md: 1 HIGH, branch-scoped CSVs sent without X-Branch-Id). Closed by REVIEW-R2.
- SECURITY: PASS (HO-IMP-002-SECURITY.md, 3 LOW).
- CODE_REVIEW: PASS (HO-IMP-002-CODE_REVIEW.md, 5 MEDIUM and 3 LOW; the five mediums were closed in R2).
- REVIEW-R2 (combined follow-up under the gate scope rule): PASS at a301affb. It confirms the HIGH is closed: `ExportButton` → `downloadInventoryFile` sends X-Branch-Id and CSRF, and `gate-fixes.test.tsx` checks both headers.
- TEST: PASS (HO-IMP-002-TEST.md and TC-HO-IMP-002.md "TEST gate run"). Results: tsc, eslint and build clean; vitest 955/955; Playwright MSW 245 cases, all passing within 3 runs, and the failures were cold-compile timeouts, not assertions. Real API 12/12 including R-D2 (addendum ef4315b3, after the owner-approved second e2e admin was seeded in the dev database). No row is NOT RUN or BLOCKED.

CRITERIA (§18)
- A1 MET. E-A1 (`procurement-flows.spec.ts:22`) creates a supplier with country, currency and terms. Lead time comes from source (`SupplierForm.tsx:124`, default 14); no test asserts it (backlog 62). Real run: R-A1.
- A2 MET. U-B1 checks outstanding 40 bags; U-B2 checks cancel is disabled with an explanation. E-A1 shows `40 bags` on the PO after the partial receipt (`procurement-flows.spec.ts:72`). Real run: R-A1 (40 outstanding).
- A3 MET, with the document caveat recorded as §19 requires: **documents are metadata plus a storage key only, and there is no upload.** U-B3 (`procurement.test.tsx:191`) checks a superseded document is still listed, with no delete or remove button. U-B4 (`:207`) checks the text "File upload is not available yet", no `input[type=file]` and a "Stored at" field. No file input exists in components/{inventory,procurement,stock,recall}. U-B7 covers the inspection pack. E-B1 and E-B2 pass on MSW.
- A4 MET. U-C1…U-C6 (`goods-receipt-form.test.tsx`), E-A2 and E-A3 (wedge timing, reload, restore, post once), plus real R-A2. Declared deviation: the confirm names each line's quantity with its unit (U-C5 `:139`), not UI-3's "{total} units". A total would need client arithmetic, which ADR-FE-041 forbids. ARCHITECTURE accepted this; it is not written in §1 Amended (backlog 60).
- A5 MET. U-B5 (stale banner, batch count in the confirm, 4-dp strings), U-B6 (409, values kept), E-B1.
- A6 MET. U-D1 shows a table at desktop width and a per-batch list at 360 px. U-D2 checks the state labels. E-X2 covers the SKU page at 360/768/1024/1440.
- A7 MET. U-D3 checks the plan is shown in server order and never re-sorted, and that a shortfall is shown. U-D6 checks the override reason. E-C1 checks the preview moves to the next batch after a quarantine. ARCHITECTURE grep: no `.sort` on a plan.
- A8 MET. U-A5 checks the "Sellable excludes…" disclosure; U-D2; E-C1; E-G1 checks quarantined stock is excluded from sellable.
- A9 MET. U-A4 checks the expiry wording; U-D2 and U-D4 check that only `allowedActions` render and an unusable batch offers only write-off; E-C1.
- A10 MET. U-F1, U-F2 (the poll does not touch an open dialog), U-F3, E-E1. The badge is polled every 60 s (`useInventory.ts:264-269`) and is rendered in the Inventory section's tab row (`inventory/layout.tsx:18-22`), not the shell sidebar. This is declared and accepted by ARCHITECTURE; not in §1 Amended (backlog 60).
- A11 MET. U-F4 checks the button stays disabled until the exact lot is typed and that immediacy and irreversibility are stated. E-F1 (`inventory-flows.spec.ts:184`); real R-F1.
- A12 MET. U-F5 covers all four trace kinds, the merged survivor and the contact details. E-F1 covers the CSV link to `trace.csv`. Real R-F1.
- A13 MET. U-F6 covers the count in the confirm, the result text and the log link. E-F1 checks the first run, then the repeat run with "0 left". Caveat: the count is derived in the client (backlog 47, MEDIUM, ARCHITECTURE).
- A14 MET. U-B8 (formula sentence from the payload, one PO per supplier), U-F1, E-E2.
- A15 MET. U-E1…U-E4, U-E7, E-C2 (both ends of a transfer in the ledger), E-D1 (pending, requester refused, second person approves); real R-C2 and R-D1.
- A16 MET. U-E5 checks no book quantity appears in the DOM. U-E6 checks "Nothing has been written to stock yet." and the commit confirm. E-D2 checks `main` contains no book/variance/on-hand text while counting. Real R-D2 PASS (CNT-2026-000002 Committed).
- A17 MET. U-D6 checks no edit/delete control and the override reason; E-C2. Real R-E1 checks the ledger CSV is sent with X-Branch-Id and CSRF.
- A18 MET. U-C3 checks "Refrigerated 2–8 °C" on the line; U-F8; E-G1.
- A19 MET. U-A1, U-A2, U-X3 (no bare quantity), U-X4 (grep test for `Number(`/`parseFloat(`). The one display-only `Number` in `lib/format/inventory.ts:35` is outside U-X4 scope (backlog 55).
- A20 MET. U-X5 checks `PermissionDeniedState` and that nothing else is fetched. E-X3 and real R-X3 check that a nurse gets the server's 403 state on receiving and suppliers.
- A21 MET.
  - E-X1 (`inventory-a11y-responsive.spec.ts`) runs axe on 46 route cases. These cover all 44 `page.tsx` files; the one not listed, `/staff/procurement`, is a redirect to suppliers. Two extra cases are a second batch and the PO edit view.
  - E-X2 runs the same 46 routes × 360/768/1024/1440. Both pass on chromium and mobile-360.
  - Gap: the committed suite does not scan with dialogs open or errors shown, although §16 lists those. ACCEPTANCE closed the gap for this verdict by running a throwaway spec (MSW, PORT 3111, `--workers=1`, chromium and mobile-360). It covered:
    - the declare-recall dialog with a wrong lot typed;
    - the recall console after a declare;
    - the notify dialog;
    - the approve dialog;
    - the commit-count dialog;
    - the approve error ("recount");
    - the supplier form after a submit.
  - Result: 12 passed, axe violations of ANY impact = 0 in every state. The spec was deleted, the dev server stopped and the tree left clean. Making these cases permanent is backlog 61.

ADRs
- ADR-FE-039…048 are in web/docs/decisions.md only, and appear once each. A script compared each Decision and Why with the §9 table: 10/10 identical, word for word. `.ai/**` contains no ADR-FE-039…048, and `git diff 0b0ce849^2..HEAD -- .ai .claude src tests` is empty.
- The react-hook-form deviation ADR (§8.3: only SupplierForm uses RHF and zod; ten editors use local state) is still not in web/docs/decisions.md. MEDIUM, backlog 48. It does not block: values are kept across a 400, decimals stay strings, and the unsaved-changes guard is wired (U-X1, U-X2).

FINDINGS (no BLOCKER or HIGH)
- [MEDIUM] web/docs/decisions.md — the react-hook-form deviation ADR is owed. Existing backlog 48.
- [MEDIUM] web/components/recall/RecallConsole.tsx:46 — the notify count is derived in the client. Existing backlog 47 (backend field).
- [MEDIUM] HO-IMP-001 backend — `StockCountDetailViewModel.AllowedActions` lists `approve` for the counter, so the UI shows a button that can only get a 400. Existing backlog 46. A UI follow-up for ADR-FE-048 depends on it.
- [LOW] Scope and docs — files outside §13's literal list, not declared:
  - `web/lib/api/inventory-download.ts`: §13 enumerates twelve modules; this one is the HIGH fix, reviewed in R2.
  - `web/tests/e2e/inventory-helpers.ts` and `inventory-routes.ts`: §13 allows only `inventory-*.spec.ts`.
  - The nav registry `web/nav/areas/inventory.ts`: backlog 50.
  - Also not in §1 Amended: the per-line receipt confirm and the badge placement.
  - New backlog 60; §1 Amended still reads "(none)".
- [LOW] E-X1 dialog and error-state axe cases are not committed. ACCEPTANCE evidenced them with a throwaway run. New backlog 61.
- [LOW] TC-HO-IMP-002.md TEST-gate rows T-U-* repeat one boilerplate evidence sentence, and A1's lead time is not asserted. New backlog 62.
- Earlier open MEDIUM/LOW rows 49, 51-57 and 59 remain as recorded.

NOTE — merge into current master
- `git merge-tree master(2330aa40) HEAD` reports content conflicts in web/docs/decisions.md and web/lib/api/errors.ts. Both are append-on-append at the end of the file, with HO-BIL-002's blocks.
- web/mocks/handlers/index.ts auto-merges.
- Expect the same in the nav or registry files and errors.ts once HO-EMR-002 lands.
- Resolve by keeping both blocks in order. After resolving, re-run tsc, vitest and the inventory Playwright specs.

SCOPE CHECK
- files in allowed scope: 170/175 literally in §13 (branch-side diff `0b0ce849^2..HEAD`).
- files outside allowed scope:
  - web/nav/areas/inventory.ts: the convention-mandated equivalent of §13's `(staff)/layout.tsx` line. Accepted by ARCHITECTURE; backlog 50.
  - web/lib/api/inventory-download.ts, web/tests/e2e/inventory-helpers.ts, web/tests/e2e/inventory-routes.ts: support files for in-scope work. LOW, backlog 60.
  - docs/backlog/IMP.md: a gate record under the owner rule of 2026-09-27, not implementation.
- §4 out-of-scope work found: none. Specifically:
  - no backend change;
  - no upload route, object-storage client or file input;
  - no new dependency (package.json, lockfile, tsconfig.json, next.config.ts and eslint.config.mjs unchanged);
  - no product/SKU editing;
  - no order, POS or dispensing screens;
  - no ledger, receipt or stock edit control;
  - no client-side CSV.
- Forbidden paths (`src/**`, backend `tests/**`, `.ai/**`, `.claude/**`, `client.ts`, `(portal)`, `(auth)`, `(staff)/layout.tsx`): untouched. errors.ts is append-only (+110/−0).
- §20: no BLOCKED condition was worked around. The §20.4 gaps (TC wire findings 2, 4, 7 and 8) were reported, and missing data is left out rather than invented.

UNDECLARED DEVIATIONS
- web/lib/api/inventory-download.ts, tests/e2e/inventory-helpers.ts and tests/e2e/inventory-routes.ts are outside §13's literal list, with no declaration and no §1 Amended row (LOW, backlog 60).
- ARCHITECTURE found that four non-line forms (StorageBreachForm, StorageProfileForm, CountOpenForm, ConsignmentForm) also skip react-hook-form without a declaration (part of backlog 48).
