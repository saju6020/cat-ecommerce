GATE: ACCEPTANCE
HANDOVER: HO-SHP-001-FU1
COMMITS: af7304b, 67a8158, 748eb1b, becc18f (code head becc18f; range 578dc79...becc18f), gate records to 3d7fa7a
DATE: 2026-10-06
STATUS: PASS

PRIOR GATES (verified from docs/gates/ and docs/test-cases/, not from commit messages)
- ARCHITECTURE: PASS (HO-SHP-001-FU1-ARCHITECTURE.md; reviewed to 748eb1b, its MEDIUM findings fixed in becc18f)
- SECURITY: PASS (HO-SHP-001-FU1-SECURITY.md; reviewed to 748eb1b, no findings)
- CODE_REVIEW: PASS (HO-SHP-001-FU1-CODE_REVIEW.md; covers becc18f)
- TEST: PASS (HO-SHP-001-FU1-TEST.md; sheet TC-HO-SHP-001-FU1.md: 31 cases, 30 PASS, 1 FAIL, 0 BLOCKED, 0 NOT RUN)

CRITERIA (handover §5)
- A1 MET. The handler returns `food` (public FoodDetailViewModel, built from FoodDetails; null when FoodDetails is null), `images` (new StaffProductImageViewModel with fileId, url=null, altText, sortOrder, isPrimary, skuId; ordered by SortOrder), `healthFocus` (DisplayName list) and `skus[].label` via `s.Label(product.Name)`, which is `Sku.LabelFor` (Sku.cs:27,32). Source read: GetStaffProductDetailQueryHandler.cs diff. Tests: FU1_T1 asserts every food field, the four analysis values, both health focuses, the image's fileId/altText/sortOrder/isPrimary/skuId=null, the SKU attributes and the label; FU1_T1b asserts food null, images [] and healthFocus []. Rerun by this gate on the branch at 3d7fa7a: 3/3 passed. Web: the type (staff-shop.ts) carries all four as required, and staff-shop.test.tsx:219-276 (U8, U9, U11) show the forms load them.
- A2 MET. The StaffProductDetailViewModel.cs and handler diff appends trailing optional parameters only (`Label` on StaffSkuViewModel; `Food`, `Images`, `HealthFocus` on StaffProductDetailViewModel). No existing parameter was renamed, removed or retyped (diff read). The existing staff-detail integration tests pass in I-FULL.
- A3 MET. `git diff --stat 578dc79 becc18f` shows src changes only in src/Read (2 files). No file under src/Domain, src/Application, src/Infrastructure or src/Api changed, and there is no migration.
- A4 MET. FU1_T2 reads the product, re-saves Details with the returned healthFocus, then food and images from the returned objects, and asserts health focus, food and image are preserved (passed in this gate's rerun). TC R1: removing the fields makes FU1_T1 and FU1_T2 fail. Web side: U10 "S1 regression" (staff-shop.test.tsx:257), killed by mutation R2.
- A5 MET. I-FULL: 690/691. The one failure, WaitlistFlowTests.Close_NonUtcBranchNearLocalMidnight_EntryNotYetPast_StaysWaiting (session_invalid), is not caused by this slice. The slice touches no clinic, session or test-infrastructure code. The branch base 578dc79 predates the master test fixes 9644a8e and 0c41fa6. This gate confirmed both fixes are ancestors of master 9ddd057, and ran `dotnet test tests/CatEcommerce.IntegrationTests --filter FullyQualifiedName~WaitlistFlowTests` on master: 7/7 passed. I-E11 p95 is 295.5 ms against the 500 ms budget.

EARLIER-GATE OBLIGATIONS
- HO-SHP-002 ACCEPTANCE C-1: CLOSED. C-1 required the staff detail to return `food`, `images`, `healthFocus` and the SKU attributes. All are returned (A1). The SKU attributes were already returned before this slice and FU1_T1 now asserts them. The forms load the values, so a re-save no longer replaces unloaded data (A4, U8-U11).
- SHP backlog S1: CLOSED. Row S1 is marked Resolved in docs/backlog/SHP.md, citing af7304b and 67a8158. Both the backend half and the web half (forms load the values; a Details save sends health focus back) are evidenced above.
- HO-SHP-001 §12.4 staff-read promise (line 1110, "full detail"): CLOSED for food, images, health focus and SKU label. Per-SKU `stock` remains outstanding by design. It is excluded by §2 and §6 and tracked as SHP backlog row 51 (HO-IMP-001 follow-up), so it is not a gap of this slice.

FINDINGS (none block; owner rule: only BLOCKER/HIGH block)
- [LOW] docs/handover/HO-SHP-001-FU1.md:113 — undeclared deviation. The handover says "VariantsTable shows `skus[].label` under the SKU code". After becc18f, VariantsTable.tsx shows it only when the SKU has no size, flavour, colour or pack quantity (it would otherwise repeat the row header, ARCHITECTURE LOW). The behaviour is reasonable, but the slice document was not updated to match it.
- [LOW] ARCHITECTURE and SECURITY recorded their range to 748eb1b; becc18f came after. becc18f changes only the web mock label format, a conditional JSX text render, a type comment, test fixtures and docs. It adds no new sink, endpoint or data. CODE_REVIEW and TEST covered becc18f. Noted for traceability only.
- [LOW] FU1_T1 checks `skus[].label` with Contains("2 kg") and Contains("Chicken"), not the exact Sku.LabelFor output. The exact format is evidenced by source (handler delegates to Sku.Label), not by the test.
- Carried, already in the earlier records for the backlog: the CODE_REVIEW MEDIUM (multi-image ordering and a SKU-level image not asserted) and its LOWs (nullable view-model tail, mock food null when empty, no Reload/not-dirty test).
- Integrator: after merging into master, confirm the integration suite is green (TEST MEDIUM; this gate verified only the WaitlistFlowTests class on master).

SCOPE CHECK
- files in allowed scope: 19/19 (backend §2: view model, handler, integration test, handover; web §6: staff-shop.ts, staff.ts, fixtures.ts, 4 components, staff-shop.test.tsx, decisions.md (ADR-FE-034 status only), backlog SHP.md; gate records and the TC sheet)
- files outside allowed scope: none. web/public/mockServiceWorker.js is modified in the worktree but uncommitted, as §6 requires.
- out-of-scope work (§2, §6 forbidden list): none. No new route, endpoint, library or state approach; no image upload; no per-SKU stock; no migration; no write-side change.
