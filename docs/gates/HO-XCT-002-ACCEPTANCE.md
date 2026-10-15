GATE: ACCEPTANCE
HANDOVER: HO-XCT-002
COMMITS: 5a187b6b..0d2a4f15 (branch ho/XCT-002, 111 files). Build 3e52c323 f9cf3644 6fb9566a 61765229 a574b268 68814f65 66c58c4a; fix rounds 77d4d418 a9956a12; gate records 85d7b8ce 1e957066 39ea78ec 1c8ee5be 0b69692a; TEST 0d2a4f15 (test-only: no production file changed)
DATE: 2026-10-14
STATUS: PASS on re-check of 7280ef85 (section at the end of this file). The first run at 0d2a4f15 was FAIL (see below).

Two HIGH findings mean two §18 criteria have no evidence: A8/E-B1 (the lab LCP was never measured, and the owner deferred /portal's LCP to this handover) and A15 (axe never saw the offline board's own states). One record-only BLOCKER stops a PASS even after those are fixed: the §5.3 amendment rows (row 61) are still missing from the target handovers, and §19 makes their presence an ACCEPTANCE check. One owner decision is also owed before PASS: A3 on the front desk (row 65). Everything else is evidenced, as set out below.

PRIOR GATES (from docs/gates/ and docs/test-cases/, not from commit messages)
- ARCHITECTURE: FAIL (39ea78ec, H1 + H2). Combined re-check FAIL (1c8ee5be, new HIGH). Re-check 2 PASS (0b69692a, range 1c8ee5be..a9956a12).
- SECURITY: the record is FAIL (85d7b8ce, the board reload/cold-load bypass). It was closed by the combined re-checks recorded in HO-XCT-002-ARCHITECTURE.md: VERIFIED item 1 of the first re-check, and the residual HIGH closed in re-check 2. That follows the owner's gate-scope rule (one combined review for follow-ups). HO-XCT-002-SECURITY.md itself was not updated.
- CODE_REVIEW: the record is FAIL (1e957066, stale recoveredAt). Closed in the combined re-check (VERIFIED item 2, mutation useRef(null) fails the second-outage test), and carried by re-check 2 PASS.
- TEST: PASS (0d2a4f15). TC-HO-XCT-002.md has 107 rows: 101 PASS, 1 FAIL (E-B2), 5 BLOCKED (U-X1, E-B1, E-C4 x2, E-X2), 0 NOT RUN.
- Commits after re-check 2 (0d2a4f15) change tests, the sheet, the backlog and the TEST record only. No production code changed, so re-check 2 still covers the code.

INDEPENDENT RUN BY THIS GATE
- `npx vitest run tests/components/offline tests/components/retention tests/components/operations tests/components/observability --maxWorkers=2` at 0d2a4f15: 12 files, 131/131 passed. This matches the sheet.
- No Playwright, perf or full suite was run (as instructed).

CRITERIA (§18)
- A1 MET.
  - U-A10 and U-A12 pass (board-and-banner.test; observed by this gate).
  - E-A2 MSW passes ("E-A1/E-A2/E-A6": both headings, Luna, `main` has zero buttons, High risk).
  - Real API, production build: "E-A1/E-A4/E-A5/E-A6" shows the appointments heading on /staff/offline and zero buttons.
  - The boarding half is deferred by §18 to HO-BRD-002.
- A2 MET. Real API, production build (TC E-A4, E-A5):
  - the worker is registered and the shell is cached;
  - offline, /staff/front-desk lands on /staff/offline?from=;
  - an offline reload still renders "Today's appointments" and "Saved at hh:mm".
- A3 NOT MET AS WRITTEN. This is a declared deviation that needs an owner decision (see the OWNER QUESTIONS).
  - Met:
    - Announced: U-A9, 6 tests.
    - Shared controls disabled with a visible reason and `aria-describedby`: U-A7 (WriteButton) and ConfirmDialog.tsx (diff read).
    - Nothing typed is lost: U-A7.
    - Refused before fetch, nothing queued: U-A8, and E-A3 MSW (`OfflineError`, zero POSTs before or after recovery).
  - Not met: the front desk's own check-in and walk-in buttons (HO-APT-002 files) are not disabled. They fail fast instead (Amended 13, ADR-FE-143 Status, row 65).
  - Also, the TC sheet marks E-A1 PASS, but neither spec asserts E-A1's "the front desk keeps showing today's list; every action is disabled with a reason". The MSW test goes straight to the board. That PASS overstates the evidence (row 110).
- A4 MET.
  - U-A15: 3 tests (routes to ?from=, announces, invalidates queries).
  - E-A6 MSW passes (serverBack, then URL /staff/front-desk).
  - E-A6 real passes: setOffline(false) -> /staff/front-desk.
- A5 MET.
  - U-A11: 4 tests.
  - U-A14, and the board-security idle and persisted-lock cases.
  - U-A16: 8 wipe-path tests.
  - E-A7 MSW passes, and E-A7 real "lock, reload offline, still locked; signing in lifts it" passes.
  - E-A8 passes on MSW and real.
  - E-A9 passes on MSW, and on real when run alone. The 429 sign-in limit is row 99.
  - E-A10 MSW passes.
  - Regression R5/R9 and mutations M1/M2/M9 are caught.
- A6 MET.
  - sw-register.test (6) and sw-navigation.test (4) pass: no registration in mock mode, development or with the flag off; an existing worker is removed; `/api/` is never touched; scope is /staff/.
  - Source: web/public/staff/sw.js scope and `/api/` early return; ServiceWorkerCleanup in the root layout.
- A7 MET.
  - U-X3: /shop/abc-123 -> /shop/[slug]; only the specified keys.
  - E-B4 MSW and real pass: samples reach the API, and the performance page lists them with "Not enough data" below 50 samples.
- A8 MET for the mechanism; E-B1's measurement is owed (HIGH-1 below).
  - E-B2 was observed by TEST: `check-js-budget.mjs` exits non-zero, with /shop/products/[slug] 203.6, /cart 207.3, /checkout 241.9 and /portal 194.2 kB against 170 kB. The budget fails the build, as ADR-FE-147 intends.
  - Source: tests/perf/lcp.spec.ts:105 asserts `median <= budgets.lcpMs`, and fails (does not skip) on a budget route missing from the inventory.
  - perf-budgets.json is not relaxed.
  - The E-B2 FAIL row is correct behaviour, not an XCT-002 defect (ADR-FE-148, §20.7).
- A9 MET. E-B3 / T8: 235 routes x 360/768/1024/1440 px, 0 failed, observed by TEST.
- A10 MET.
  - U-X4: credentials omit, keepalive, no CSRF or branch header.
  - U-X5, and the reporter component test (sends nothing on /staff).
  - E-B5 MSW and real: no cookie, no CSRF, no URL, id or user in the body.
- A11 MET.
  - U-B5, 2 tests: erased and kept with periods (7 and 10 years); no textbox or reason box; channel and identity check required; the dialog names the customer once and says "This cannot be undone"; it sends exactly {channel, identityCheck}.
  - U-B6, 2 tests.
  - E-C1/E-C2 MSW and real pass: placeholder; the ledger lists the request with no personal data.
  - The audit-log cross-check is not in A11's text (row 104).
  - "Erased on {date}" is not shown because the API sends no date (Amended 4, declared; row 111).
- A12 MET.
  - U-B1: the four classes show "Review only"; 7 years from the schedule payload; "Nothing is deleted automatically"; a link to settings.
  - U-B3: codes glossed, never shown; "never deleted by a run".
  - U-B2: the confirm says it will not delete clinical, financial, regulatory or audit records.
  - E-C3 MSW and real pass.
  - E-C4 BLOCKED does not leave A12 without evidence. A12 asks that the screens make clear nothing clinical is deleted, and U-B1 and U-B3 assert exactly that. E-C4 adds the live round-trip of a setting change. MEDIUM, row 101.
- A13 MET.
  - U-B10: Stale as text plus icon, and four "Up to date" items including WAL archived until; the objectives sentence.
  - Source: BackupStatusCard.tsx:84-87 renders the last test's achieved RPO/RTO beside the objectives.
  - E-D1 MSW and real pass: the overdue alert is in the viewport; the banner shows on /staff/admin/branches.
  - The test does not assert the achieved-values sentence (row 114, LOW).
- A14 MET.
  - U-B8: 3 tests. U-B9: 3 tests, no RPO/RTO/outcome input; "a failing drill shows Failed". U-B11.
  - The H2 tests: paste required; the original text is sent.
  - E-D2 MSW passes; E-D2 real gets 201, and the panel shows the server's achievedRpoMinutes and outcome, and Failed for a failing drill.
  - E-D1: overdue is unmissable.
- A15 NOT MET (HIGH-2). E-X1 / T9 (235 routes, zero serious or critical) is real evidence for every route except the one A15 names: "including the offline board".
  - The sweep loads /staff/offline as audience `none` under MSW.
  - The ping answers there, so OfflineBoard renders only "Checking the connection…" (OfflineBoard.tsx:144-145) and then `router.replace`s away.
  - The board with data, the locked board, the refusal states and the OfflineBanner's offline, degraded and recovered states (all of which E-X1 names) were never given to axe.
  - No axe call exists in xct-offline.spec.ts or in any component test.
  - E-X2 (keyboard) BLOCKED does not on its own leave A15's text without evidence, because A15 is worded as an axe criterion. It stays MEDIUM (row 102). It can be written alongside the HIGH-2 fix at little cost.
- A16 MET.
  - U-A8: the OfflineError sentence.
  - U-X1 for RecordRestoreTestForm (InvalidRestoreTestTimes shown as a sentence).
  - Source, lib/api/errors.ts:1261-1274: `describeOperationsError` returns the server message, a mapped sentence or the generic message, never the code. The erase panel (:81), the reapply form (:79) and RunRetentionNow (:33) use it.
  - OfflineDeviceSettings makes no API call, so a server 400 cannot occur there.
  - T9 sweep: no GUID or errorCode in any `[role=alert]` on 235 routes.
  - U-X1 BLOCKED (no field-mapping test on four forms) is a test-strength gap. It does not leave A16 unevidenced, because A16 does not require field mapping. MEDIUM, row 98.
- A17 MET.
  - U-X2: exactly `snapshot` and `device`; no secret.
  - E-X3 MSW and real: localStorage and sessionStorage hold nothing of this feature, one IndexedDB database, the session cookie is unreadable.
  - R7/M6: customerPhone is stripped on write and on read.

RULINGS ASKED FOR
1. E-B2 and E-B1.
   - E-B2: no XCT-002 criterion fails. A8 asks that a lab budget fail the build, and it does.
     - The over-budget routes belong to SHP, ORD and POR (ADR-FE-148, §20.7). They are correctly not fixed here, and the budget was not relaxed.
     - Reporting: only a pointer exists (XCT row 68). No row exists in SHP.md, ORD.md or POR.md on master or on this branch (checked by grep).
     - A pointer is acceptable for this gate. The rows themselves should be filed at merge (row 109), because row 68 is an instruction that nobody has carried out.
   - E-B1: HIGH, see HIGH-1. It is not "correctly reported elsewhere".
     - HO-POR-002's A13 (/portal LCP) was deferred to XCT-002 by the owner ("Defer to XCT-002", 2026-10-13). POR.md row 43 is marked RESOLVED on that basis.
     - §18's own deferral of E-B1 ("until HO-SHP-002 exists") has lapsed: SHP-002 merged at b65f6181, before this branch started.
     - The TEST row's reason ("skipped for RAM/time") is not a blocking condition.
     - The builder's figures at 0b69692a were never observed by a gate. For /portal they were also taken with a customer who has no cats, orders or invoices, which is not the profile the amendment requires.
     - Accepting now would leave NFR-PRF-1's lab evidence nowhere, since this is the last handover in the delivery order.
     - Over-budget results from the run go to their owners as findings. They are not an XCT-002 failure. What is owed here is the observed measurement.
2. The BLOCKED rows.
   - U-X1: does not leave a criterion without evidence (A16 is evidenced, see above). MEDIUM, row 98.
   - E-C4 (MSW and real): does not leave a criterion without evidence (A12 is evidenced by U-B1 and U-B3). MEDIUM, row 101.
   - E-X2: does not leave A15's literal text without evidence. MEDIUM, row 102.
   - A15 is still NOT MET, for a different reason that the TEST gate did not flag: E-X1 never scanned the board's states (HIGH-2).
3. Merge preconditions.
   - Row 61 is still open.
     - Checked on master and on the branch: HO-ACC-002 §1 has no IndexedDB carve-out row. Its line 110 still lists IndexedDB among forbidden stores.
     - HO-APT-002, HO-BRD-002 and HO-CAT-002 have no XCT-002 §5.3 row.
     - Row 61's text names the HO-ACC-002 IndexedDB carve-out (with U-X4/E-X3 extended to IndexedDB), the erase panel, the §8.4 wipe, the APT/BRD banner and `useConnectivity()`, and the HO-CAT-002 headers, reporter and `<ServiceWorkerCleanup />` root-layout line. It is complete.
     - §19 makes "the §5.3 amendments confirmed present in the target handovers" an ACCEPTANCE check.
     - Under the HO-XCT-001 ACCEPTANCE precedent (BLOCKED until c70180d6 added the rows on the branch), this is a record-only BLOCKER for this gate. It is not a code defect, and it is declared (Amended 17), unlike in XCT-001.
     - To clear it: the integrator appends the dated rows to HO-ACC-002, HO-APT-002, HO-BRD-002 and HO-CAT-002 on ho/XCT-002 before the merge. A re-check then only confirms them.
   - Amended (24)/(25) edited in place:
     - The text is accurate. It matches the code and ADR-FE-142/144 (see re-check 2).
     - The pre-edit text is in git at 77d4d418. The edits remove three fragments (`s.`, `only`, `for the same person`).
     - A full restatement is not needed. The integrator should append one line, (29): "(24) and (25) were corrected in place on 2026-10-14 (a9956a12): the board's 5 s exception; any successful sync lifts the lock; prior text at 77d4d418". That restores the append-only trail without duplicating the text. LOW, row 112.
4. Phone removal: fully implemented in the frontend.
   - Source: snapshot.ts:40-58 `withoutCustomerPhones` runs before `dbPut` (:117), and `cleaned()` strips old records on read (:68-74).
   - OfflineAppointmentsList shows no phone and has no `tel:` link.
   - The opt-in text says only "names" (OfflineDeviceSettings.tsx:69).
   - ADR-FE-141 is amended (decisions.md:1553).
   - Tests: E-A1 MSW asserts no `8801711` and no `tel:` in `main`; R7/M6 caught.
   - The API still sends the field. The backend follow-up is row 90 (open, HO-XCT-001), and it is correctly recorded.
   - The field still exists in the in-memory TanStack cache while the page is open. It is not at rest, and row 90 removes it.
5. Backend follow-ups. All are recorded as open HO-XCT-001 rows in docs/backlog/XCT.md:
   - restore-test 500: row 66;
   - CORS ETag: row 67;
   - runs status filter: row 69;
   - 403 `{ error }` shape: row 70;
   - customerPhone in the payload: row 90.
   - Not recorded anywhere: Amended (4), the customer read has no "erased on" date, and (5), restore tests carry no performer name. Amended (9), no build id in telemetry, needs a `next.config.ts` edit outside §13. Proposed row 111.
6. Front-desk buttons (row 65): yes, this is a deviation from A3 (and from E-A1's "every action is disabled with a reason") that needs owner acceptance.
   - It is declared, not hidden: Amended 13, ADR-FE-143 Status, row 65.
   - It is also not the builder's to fix. §11.3 makes it depend on the HO-APT-002 §5.3 amendment, which was never recorded, and §14 forbids the APT files.
   - The behaviour is not silent: `client.ts` refuses with the offline sentence, nothing is queued, and typed input survives.
   - It is not "disabled" either, as A3 requires.
   - Owner choice: (a) accept fail-fast with the visible explanation as meeting A3 for the APT-owned buttons, or (b) require an APT follow-up that wraps check-in and walk-in in `WriteButton` / `useWriteEnabled()` (row 65) before or after merge.
   - Until one of these is chosen, A3 lacks evidence and PASS is not available.

FINDINGS
- [HIGH] HIGH-1 docs/test-cases/TC-HO-XCT-002.md:104 (E-B1), web/tests/perf/lcp.spec.ts — the NFR-PRF-1 lab LCP was not measured under any gate.
  - The owner deferred HO-POR-002 A13 (/portal) to this handover (§1 Amended 2026-10-13: "measured against the real API with a customer who has cats, orders and invoices").
  - The §18 "until HO-SHP-002" deferral has lapsed.
  - Fix: run `npm run perf:lcp` on the real-API production build, with E2E_CUSTOMER_IDENTIFIER/PASSWORD for a seeded customer who has cats, orders and invoices. Record the six medians and their runs on the sheet.
  - Over-budget routes go to their owners (ADR-FE-148). Do not relax the budget.
- [HIGH] HIGH-2 web/tests/a11y/app-sweep.spec.ts, web/tests/e2e/xct-offline.spec.ts — A15 / E-X1: axe has never scanned the offline board with data, the locked board, a refusal state, or OfflineBanner offline/degraded/recovered.
  - The sweep's /staff/offline visit sees only the "Checking the connection…" loader, because the MSW ping answers.
  - Fix: AxeBuilder checks in xct-offline.spec.ts at the points where those states are already reached: after openBoardFromBanner, after the E-A7 lock, the banner while serverDown, and one refusal. Zero serious or critical.
  - Correct E-X1 on the sheet, whose PASS omits these states.
- [BLOCKER, record-only, integrator] docs/handover/HO-ACC-002.md, HO-APT-002.md, HO-BRD-002.md, HO-CAT-002.md §1 — the §5.3 amendment rows (row 61) are absent. §19 requires them present at ACCEPTANCE, and §20.2 names the HO-ACC-002 carve-out as the feature's licence. No code change is needed.
- [OWNER] A3, front desk check-in and walk-in not disabled offline (row 65). An owner decision is needed, see ruling 6.
- [MEDIUM] Row 68 is a pointer only. No E-B2 rows exist in SHP.md, ORD.md or POR.md (row 109).
- [MEDIUM] U-X1 (row 98), E-C4 (row 101), E-X2 (row 102): existing rows, unchanged.
- [LOW] TC sheet E-A1 MSW and real are marked PASS, but neither test asserts that the front desk keeps its list or that its actions are disabled (row 110).
- [LOW] The declared backend gaps in Amended (4), (5) and (9) have no backlog row (row 111).
- [LOW] Amended (24)/(25) were edited in place (row 112).
- [LOW] Row 88 still says "only a sync for the same person lifts it". Row 92 superseded that; reword it (row 113).
- [LOW] U-B10 does not assert the last test's achieved RPO/RTO sentence. The source renders it (row 114).

PROPOSED ROWS FOR docs/backlog/XCT.md (highest existing: 104)
| 105 | HIGH / XCT-002 (blocks ACCEPTANCE) | E-B1 lab LCP unmeasured under any gate. Run `npm run perf:lcp` on the real-API production build with a seeded customer (cats, orders, invoices) over `/`, `/shop`, `/shop/products/[slug]`, `/cart`, `/checkout`, `/portal`. Record medians and runs in TC-HO-XCT-002.md. `/portal` is the owner's deferral from HO-POR-002 A13 (POR row 43). Over-budget routes go to their owners; do not relax the budget. | XCT-002 ACCEPTANCE |
| 106 | HIGH / XCT-002 (blocks ACCEPTANCE) | A15/E-X1: no axe scan of the offline board with data, the locked board, a refusal state, or OfflineBanner offline/degraded/recovered. The 235-route sweep sees only the "Checking the connection…" loader at /staff/offline. Add AxeBuilder checks in xct-offline.spec.ts at those states; zero serious or critical. Correct the E-X1 row. | XCT-002 ACCEPTANCE |
| 107 | BLOCKER record-only / INTEGRATOR | Row 61 is a precondition of the XCT-002 ACCEPTANCE gate (§19), not only of the merge. Append the §5.3 rows to HO-ACC-002, HO-APT-002, HO-BRD-002 and HO-CAT-002 §1 on ho/XCT-002 (precedent: c70180d6 for XCT-001), then re-check ACCEPTANCE. | XCT-002 ACCEPTANCE |
| 108 | OWNER (blocks ACCEPTANCE) | A3: the front desk's check-in and walk-in buttons fail fast offline with the offline sentence, but are not disabled (row 65). Accept fail-fast as meeting A3 for HO-APT-002's buttons, or require the APT follow-up (`WriteButton` / `useWriteEnabled()`). | XCT-002 ACCEPTANCE |
| 109 | MEDIUM / INTEGRATOR | Row 68 is a pointer only. File the first-load JS breaches as rows in SHP.md (`/shop/products/[slug]` 203.6 kB), ORD.md (`/cart` 207.3, `/checkout` 241.9 kB) and POR.md (`/portal` 194.2 kB), plus any E-B1 breach from row 105. | XCT-002 ACCEPTANCE |
| 110 | LOW / XCT-002 TEST | TC sheet: E-A1 (MSW, real) is marked PASS, but neither spec asserts that the front desk keeps today's list or that its actions are disabled with a reason. E-X1 is PASS without the board states. Correct both rows (E-A1 to PARTIAL with row 65). | XCT-002 ACCEPTANCE |
| 111 | LOW / HO-XCT-001 + owner | Declared gaps with no row: (4) the customer read has no "erased on" date (U-B6 / §11.5 show no date); (5) restore tests carry no performer name; (9) telemetry build id is `unknown` (needs a `next.config.ts` edit outside §13). | XCT-002 ACCEPTANCE |
| 112 | LOW / INTEGRATOR | HO-XCT-002 Amended (24)/(25) were edited in place at a9956a12 (prior text at 77d4d418). Append a (29) saying so. No restatement is needed. | XCT-002 ACCEPTANCE |
| 113 | LOW | Row 88 says "only a sync for the same person lifts it". Row 92 superseded that ("a sync by anyone"); reword row 88. | XCT-002 ACCEPTANCE |
| 114 | LOW / XCT-002 test | U-B10 does not assert the "Last restore test: N minutes of data lost, restored in M minutes" sentence (BackupStatusCard.tsx:86-87). Add the assertion. | XCT-002 ACCEPTANCE |

WHAT CLEARS THIS GATE
- Rows 105 and 106: one perf run and one small spec change. No production code is expected to change.
- Row 107: integrator records.
- Row 108: an owner answer.
- Then a re-check of this gate. Every other criterion above is already evidenced.

SCOPE CHECK
- files in allowed scope: 111/111, counting precedent-cured files.
  - `docs/handover/HO-XCT-002.md`: §1 Amended only. The first edit was append-only; the second edited (24)/(25) in place (LOW, row 112).
  - `docs/backlog/XCT.md`.
  - `web/components/admin/adminNavItems.ts`: three entries, Amended 18 (BIL/NTF/EMR precedent).
  - `web/lib/operations/drill.ts`, `web/lib/retention/labels.ts`, `tests/components/observability/**`, `<OverdueRestoreBanner />` in `(staff)/layout.tsx`, and `<ServiceWorkerCleanup />` in `app/layout.tsx`: named in Amended (22), with ratification owed in row 61.
  - Scoped diffs read: `app/layout.tsx` (reporter and clean-up), `next.config.ts` (sw.js headers only), `package.json` (four scripts, no dependency), `ConfirmDialog.tsx` (`useWriteEnabled` only), `customers/[customerId]/page.tsx` (panel only), `mocks/handlers/index.ts` (one import, one spread).
- files outside allowed scope: none uncured. No §14 path was touched: nothing under src/**, tests/**, deploy/**, .ai/** or .claude/**, no dependency change, and no storefront, portal or checkout route file.
- out-of-scope work (§4): none found. No offline write or queue, nothing offline for customers, no PWA manifest or push, no storefront fix, no new package, and no delete control on the retention screens.

UNDECLARED DEVIATIONS
- None in the code. Every deviation found is in §1 Amended (1)-(28) or ADR Status text.
- Two test-sheet records overstate the evidence. E-A1 is marked PASS for front-desk actions "disabled with reason". E-X1 is marked PASS, but the sheet narrowed it to "every route" and dropped the board, locked-board and banner states that the handover names. These are evidence defects rather than implementation deviations (rows 106, 110).


---

## Re-check — 2026-10-14 (gap work c37d180c..7280ef85)

GATE: ACCEPTANCE (re-check)
HANDOVER: HO-XCT-002
COMMITS: 7280ef85 (range c37d180c..7280ef85, 13 files: docs/backlog/{APT,ORD,POR,SHP,XCT}.md, docs/handover/HO-{ACC,APT,BRD,CAT,XCT}-002.md, docs/test-cases/TC-HO-XCT-002.md, web/tests/components/operations/operations.test.tsx, web/tests/e2e/xct-offline-a11y.spec.ts)
DATE: 2026-10-14
STATUS: PASS

Correction note: commit 9cfad3db wrongly appended an unrelated HO-XCT-001 re-check text (a stale scratch file) under this file. The commit that carries this section removes it. That text was never a verdict on HO-XCT-002.

No production file changed: the diff touches docs and two test files only, so ARCHITECTURE re-check 2 (0b69692a) still covers the code. The prior gates stand as recorded above. The TC sheet now has 108 rows: 100 PASS, 2 FAIL (E-B1, E-B2; both are other owners' routes, ADR-FE-148), 2 PARTIAL (E-A1, owner-accepted), 4 BLOCKED (U-X1, E-C4 x2, E-X2; all MEDIUM, rows 98/101/102), 0 NOT RUN.

Independent run by this gate: `npx vitest run tests/components/operations` at 7280ef85 gave 2 files and 26/26 passed. That is operations.test 18, including the new U-B10 assertions, plus permission-denied 8. No Playwright or perf run was repeated by this gate. E-B1 and the a11y spec rest on the recorded command output on the sheet.

RULINGS
- **E-B1 / A8 MET; HIGH-1 (row 105) closed.**
  - The sheet now records an executed `npm run perf:lcp` with full configuration:
    - `next build` against the real API on 5113, served by `next start`;
    - DB catecommerce_xct002e2e;
    - Slow 4G + 4x CPU;
    - five runs per route, each run listed.
  - `/portal` median 1436 ms (1436, 1308, 1560, 1740, 1328), signed in as a customer with 3 cats, 3 orders and 2 invoices. That is the profile §1 Amended 2026-10-13 requires, so the owner's HO-POR-002 A13 deferral is discharged. POR.md 44 says so.
  - `/` (2964 ms, re-run 2736) and `/shop/products/[slug]` (2548 ms) are over budget. Both are SHP's routes, filed as SHP.md 73 with their runs. Reporting and not fixing is correct (ADR-FE-148, §20.7). The budget was not relaxed. The spec exits non-zero, which is A8's mechanism working.
  - One residual, MEDIUM (proposed row 115). The product-page budget row measures the not-found page: the inventory's MSW sample slug `royal-canin-indoor-adult` does not exist in a real database. The seeded real product page (860 ms) was measured outside the spec. The gate does not fail on this, because the mechanism is shown and the real page is within budget. Until it is fixed, though, the lab budget for that route watches the wrong page.
- **A15 MET; HIGH-2 (row 106) closed.**
  - `web/tests/e2e/xct-offline-a11y.spec.ts` (read in full) runs AxeBuilder with the sweep's tags. It requires zero serious or critical findings in each of seven states, and asserts each state before scanning:
    - banner offline with a saved copy;
    - board with data ("Offline · read-only", both headings, Luna);
    - locked board (clock +31 min, the locked message, Luna gone);
    - refusal, with the copy re-attributed to another staffAccountId ("This copy was saved by someone else.");
    - banner offline with no saved copy;
    - banner recovered ("Back online");
    - banner degraded.
  - Recorded result: 4 tests passed, 0 serious or critical.
  - The expired and no-copy refusals were not scanned. They are wording variants of the scanned refusal, and I do not hold the criterion on them.
  - E-X2 (keyboard) stays BLOCKED as a MEDIUM (row 102). A15 is an axe criterion, as ruled in the first run.
- **A3 MET by owner acceptance (row 108).**
  - Recorded as "Owner decision 2026-10-14: 'Accept as built'" in HO-APT-002 §1 Amended (15), XCT rows 65 and 108, and APT.md W21 (the follow-up to grey the buttons out).
  - The rest of A3 stands on U-A7, U-A8, U-A9 and E-A3.
  - E-A1 is honestly PARTIAL on the sheet (row 110).
  - Basis: the owner's acceptance is taken from those repository records. This gate did not hear the owner's words itself. If the owner did not say this, A3 reverts to NOT MET.
- **§5.3 records complete and append-only; BLOCKER (row 107) closed.**
  - Word-diff of each target handover shows additions only. The single "deleted" token in each is the cell's last word, re-emitted with the appended text.
  - Coverage against §5.3:
    - HO-ACC-002 (26): §4.3 / ADR-FE-002 IndexedDB carve-out, limited to `web/lib/offline/**`, with U-X4/E-X3 extended to IndexedDB.
    - HO-ACC-002 (27): the erase panel on the customer page.
    - HO-ACC-002 (28): the §8.4 wipe on sign-out, `session_invalid` and person change.
    - HO-APT-002 (15): banner and `useConnectivity()` / shared controls, plus the owner's A3 decision.
    - HO-BRD-002: banner and read-only state.
    - HO-CAT-002: the `next.config.ts` headers, `<WebVitalsReporter />` and `<ServiceWorkerCleanup />`.
  - HO-XCT-002 (29) records the in-place (24)/(25) correction, with the prior text at 77d4d418 (row 112).
  - LOW wording, no row needed:
    - The CAT row says (c) is "ratified here" because §13 did not allow it, then closes with the boilerplate "each ... lies inside HO-XCT-002 §13". The specific sentence is the accurate one.
    - The APT row has "they match ... lies inside".
- **Rows 109-114 done.**
  - 109: SHP.md 73, ORD.md 105 and POR.md 44 filed, each with figures and a pointer to row 68.
  - 110: the sheet is corrected (E-A1 PARTIAL; E-X1 annotated, plus the board-states row).
  - 111: filed. It stays open as a HO-XCT-001 / owner item, which is what it is.
  - 112: (29) appended.
  - 113: row 88 reworded.
  - 114: U-B10 asserts "Last restore test: 9 minutes of data lost, restored in 140 minutes (passed)." and the no-test sentence. Observed passing by this gate.

CRITERIA (re-check)
- A1, A2, A4-A7, A9-A14, A16, A17: MET, unchanged from the first run (evidence above).
- A3: MET by recorded owner acceptance (row 108), plus U-A7, U-A8, U-A9 and E-A3.
- A8: MET (E-B2 and E-B1 both exit non-zero on other owners' routes; /portal measured within budget on the required profile).
- A15: MET (E-X1 over 235 routes, plus E-X1 board states, 7 states with 0 serious or critical).

FINDINGS
- [MEDIUM] web/scripts/route-inventory.mjs / web/tests/perf/lcp.spec.ts: the `/shop/products/[slug]` lab-LCP sample uses an MSW fixture slug that a real database does not have, so E-B1 measures the not-found page. Proposed row 115.
- [LOW] docs/handover/HO-CAT-002.md, HO-APT-002.md §1: boilerplate wording in the integrator rows, as noted above. Cosmetic; no row.
- No BLOCKER or HIGH.

PROPOSED ROW FOR docs/backlog/XCT.md (highest existing: 114)
| 115 | MEDIUM / XCT-002 tooling | `tests/perf/lcp.spec.ts` takes `/shop/products/[slug]`'s sample URL from the route inventory's MSW fixture slug (`royal-canin-indoor-adult`), which a real database does not have, so the lab LCP budget measures the not-found page (2548 ms) instead of a product page (860 ms, measured outside the spec). Let the spec take a real slug (for example an `E2E_PRODUCT_SLUG` env, or the first product from the API) when it runs against the real API. Correct SHP.md 73's product-page LCP half once it has been re-measured. | XCT-002 ACCEPTANCE re-check |

SCOPE CHECK
- files in allowed scope: 13/13.
  - The four target-handover edits are integrator §1 Amended rows. Integrator records are not limited by HO-XCT-002 §14; the precedent is c70180d6.
  - The two test files are inside §13 (`web/tests/components/operations/**`, `web/tests/e2e/xct-*.spec.ts`).
  - The backlog and TC files are gate and integrator records.
- files outside allowed scope: none.
- out-of-scope work (§4): none. No production code changed.

UNDECLARED DEVIATIONS
- None.
