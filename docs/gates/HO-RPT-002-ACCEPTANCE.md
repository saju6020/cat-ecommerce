GATE: ACCEPTANCE
HANDOVER: HO-RPT-002
COMMITS: 2f6d1d8a 60518928 e0f17f0e 5bd83b64 09556784 4bafe4f2 cee3df9f 538f2fea (range 5a187b6b..538f2fea, branch ho/RPT-002, head 538f2fea)
DATE: 2026-10-14
STATUS: PASS

No BLOCKER or HIGH findings. All 16 section 18 criteria are MET, each with named evidence below. Two rulings overturn the TEST sheet's derived acceptance rows: A12 (BLOCKED there) and A15 (FAIL there). The reasons are given at each row. Medium and low findings are proposed as docs/backlog/RPT.md rows 96-102. Product deviations still need owner sign-off (OWNER QUESTIONS); none of them blocks this gate.

PRIOR GATES (from docs/gates and docs/test-cases, not from commit messages)
- ARCHITECTURE: FAIL (5bbb9225, one BLOCKER: mockServiceWorker.js), then PASS on the combined re-check (a5331c33, covering up to 4bafe4f2). The file is HO-RPT-002-ARCHITECTURE.md.
- SECURITY: PASS (1b35cec9), no BLOCKER or HIGH.
- CODE_REVIEW: PASS (e4d4731c), no BLOCKER or HIGH.
- TEST: FAIL (cee3df9f) on one HIGH only: home.spec.ts still expected /staff/cats after the ADR-FE-130 landing change. The TEST record itself still reads FAIL; nobody rewrote it. 538f2fea fixes the HIGH under the coordinator's scope amendment, recorded as Amended (19). Under the owner's gate-scope rule a tiny fix needs only a test confirmation, and this gate ran it (below). **The evidence that closes the TEST HIGH is in this file, not in HO-RPT-002-TEST.md.**
- TC sheet: docs/test-cases/TC-HO-RPT-002.md, 108 rows. PASS 97, FAIL 4 (L-1, REAL:E-X2, M-3, M-4), BLOCKED 7, NOT RUN 0.

TEST CONFIRMATION RUN BY THIS GATE (head 538f2fea, worktree D:/my-repo/cat_ecommerce-wt/RPT-002/web)
- `npx vitest run tests/components/dashboard tests/components/reports tests/components/report-schedules --maxWorkers=2`: 9 files, 169 tests passed.
- `PORT=3114 npx playwright test tests/e2e/home.spec.ts --workers=1` (MSW): 8 of 8 passed. This includes 'signed in as staff lands on /staff (the home dashboard, HO-RPT-002 ADR-FE-130)' in chromium and mobile-360.
- `PORT=3114 npx playwright test tests/e2e/reports-viewer.spec.ts --workers=1` (MSW): 14 of 14 passed. Run because 538f2fea also changed this spec (E-C2 exact match) and the chart. It includes 'E-X1/E-X2: the catalogue and a 12-column report are axe-clean, with the chart open, and never scroll the page sideways' in both projects.
- Nothing was listening on 3114 before the runs. git status was clean afterwards.

CRITERIA
- A1 MET. Named tests: dashboard.test.tsx 'U-A1: renders exactly the tiles sent, in the server order' and 'U-A7: money comes from the string and the date is the payload local date' (mutation M-1 kills it). Browser runs: MSW and REAL E-A1 (reception lands on /staff with tiles), and REAL E-A3 (admin switched A -> London -> A; each request carried that branch's X-Branch-Id; the zone label changed to Europe/London and back). Limit: both zones were on the same calendar date. The date comes from the payload's localDate, which U-A7 pins.
- A2 MET. Named tests: 'U-A2: a clinic-only payload has no money, stock or sales tile and no placeholder' and 'U-C5: restricted columns are absent for a shop manager, present for an accountant'. MSW and REAL E-A2: the accountant sees Payments today and Receivables; the nurse sees none of them. The nurse's "no Reports nav" became "Reports refused" under declared Amended (9). This is an owner question; the dashboard behaviour this criterion names holds.
- A3 MET. Pausing while hidden: B-POLL (real browser) recorded 1 request in 68 s visible, 0 in 130 s hidden, and a request on becoming visible; the useDashboard test asserts `refetchIntervalInBackground: false`. Refreshing: REAL E-A4, 'Updated HH:mm' changed after 65 s. Dialog and focus kept: **U-A5 alone does not prove this** (row 96). Its dialog is a sibling hard-coded `open`, and it asserts neither focus nor the announcement. I rest this half on source I read:
  - `app/(staff)/staff/page.tsx` renders the same `DashboardView` whenever `data` exists, including during a refetch and after a refetch error;
  - `useDashboard` uses `placeholderData: keepPreviousData`;
  - tiles are keyed by `tile.key` (DashboardView.tsx:58), so a poll never unmounts the tree or moves focus;
  - RefreshAnnouncer is `role="status" aria-live="polite"` and never moves focus.
- A4 MET. REAL E-B1 and B-DATA: clinic-daily-schedule 1 row, clinic-queue-performance 31 rows, vaccination-due-list 0 rows (empty state, no error). All 21 catalogue keys opened with HTTP 200, no alert and UI rows equal to API rows; `surgery-register` is one of the 21 (ReportCatalogue.Clinic.cs:10). Limit: surgery and vaccination were viewed only empty on the real API (row 102).
- A5 MET. Named tests: viewer.test.tsx 'U-C1: one control per parameter, typed and bounded', 'U-C2/U-C4/U-C7/U-C8: seeds from defaultRange...' plus source-scan.test.ts, and 'U-C3: Apply writes the filters to the URL'. MSW and REAL E-B2 (range limit then results) and E-B4 (second context, identical table). MSW E-B2 and E-B4 were re-run here and passed.
- A6 MET. Named tests in exports.test.tsx: U-D1, 'U-D2: a 413 on PDF shows the CSV advice, downloads nothing and returns to idle', and 'U-D3: ReportTimedOut shows the shorter-range advice'. MSW and REAL E-B3: both files non-empty. B-EXP: PDF starts `%PDF-` and the CSV headers equal the screen's 11 plus the server's Currency column. B-XHR: an XHR with the pc_staff_session cookie and X-Branch-Id, status 200. MSW E-B3 was re-run here and passed.
- A7 MET. REAL E-C1: stock-position 4 rows with the coverage note verbatim; stock-near-expiry 2 rows, with the unrecorded landed cost shown as a dash plus 'Not recorded'; stock-low-reorder 1 row.
- A8 MET. Named tests: U-C4, U-C5, and source-scan.test.ts (U-X3, which reads the real files). MSW E-C2 (re-run here, passed) and REAL E-C2: the accountant sees Margin and the coverage notes; the shop manager is refused.
- A9 MET. REAL E-C4: cohort-retention, customer-lifetime-value and customers-new-vs-returning opened with data and were axe-clean. Growth is Administrator-only on the server (Amended (17)).
- A10 MET. REAL E-C3: payment-reconciliation has 4 rows (CashOnDelivery, BankTransfer, CashInStore, CardInStore), and the variance shows the word 'Mismatch' beside the amount. MSW E-C3 was re-run here and passed. format.test.ts pins the edge values. The word is Mismatch or Matched, not Over or Short: declared Amended (7), owner question.
- A11 MET. Named tests in schedules.test.tsx: U-E1 (plus compatiblePeriods, which mutant M-2 kills), U-E2, 'U-E3/U-E4' and 'U-E3: typing a name does not call the preview again', U-E5, and the 'ScheduleActions U-E6' block. B-DEB (real): 6 edits gave 1 POST /preview. MSW and REAL E-D1 and E-D3.
- A12 MET. **This overrules the TEST sheet's BLOCKED.** schedules.test.tsx 'RunHistoryTable — U-E7' asserts Generated, Failed and Purged as text, the failure advice, 'Too large to attach — link sent', no error code in the DOM, and the per-recipient line 'Rezwan Ahmed: Sent'. The frontend type `deliveries: { recipientName, status }[]` matches the backend `ReportScheduleDeliveryViewModel(RecipientName, Status)`. B-SCHED (real) shows a Generated run with 4 rows and 39 KB. Why the real delivery rows were absent:
  - the backend builds them from Notification rows (ReportScheduleQueryHandlers.cs:159-170);
  - those rows come from outbox requests that ScheduledReportRunCommandHandlers.cs:91-115 enqueues, and the notification worker processes them;
  - the TEST host had no email provider.

  The missing half of REAL:E-D2 is therefore a host and backend observation owed to HO-RPT-001 (row 91), not a gap in what this slice renders. Recommend rerunning E-D2 on a host with the capturing sender before the area is called live.
- A13 MET. MSW and REAL E-D4: a shop manager on /staff/admin/report-schedules gets PermissionDeniedState.
- A14 MET. Named tests: U-C2, U-E3 (one debounced server preview), and source-scan.test.ts (no Date maths, no `Number(` over an amount, no recurrence). M-1 kills the money path. B-DATA: UI rows equal API rows on all 21 reports. ReportChart's `toChartNumber` and `groupDigits` are for plotting and display only, which ADR-FE-134 permits.
- A15 MET. **This overrules the TEST sheet's FAIL; ruling below.**
  - axe: MSW E-X1 passes in both projects (re-run here); REAL axe is clean on /staff, the schedule list and detail, all 21 reports, and the chart.
  - 360 px including a 12-column report: MSW E-X2 measures scrollWidth at 360/768/1024/1440 (re-run here, passed both projects); REAL B-OVF, B-DATA, B-CHART and B-SCHED show no overflow from any RPT-002 element.
  - Chart equivalent table: 'U-C9: the chart is hidden by default and its hidden table matches the rows', and B-CHART (2 rows = 2 API rows).
  - Ruling on row 89: the 10 px overflow comes from `web/components/branches/ActiveBranchPicker.tsx`, mounted only in `web/app/(staff)/layout.tsx`. Neither file is changed in 5a187b6b..538f2fea, so the overflow is on master today on every staff page (/staff/cats included) for a staff member with several long branch names. Section 14 forbids this handover to edit either file. It is a HO-ADM-002 defect inherited by RPT-002's routes, not an RPT-002 failure, and it stays MEDIUM row 89 with HO-ADM-002 as owner. See owner question 6.
- A16 MET. Named tests: 'U-C3: Apply writes the filters to the URL' (filters survive reload; E-B4 shows the same in a second context), U-D2, 'U-E5: a refused recipient lands on the recipients field and the rest keeps its values', and U-X2 in viewer.test.tsx and exports.test.tsx. B-429 (real): "You're running reports too quickly. Try again in a minute or so.", with no error code in the DOM and the UI recovering. U-E5 cannot name the person: declared Amended (6), owner question.

RULINGS ASKED FOR
1. TEST's BLOCKED rows. None leaves a criterion without evidence.
   - REAL:E-D2: see A12. The UI contract is evidenced (U-E7 plus the matching wire type); the missing real delivery rows are a host and backend matter (row 91).
   - MSW E-A3, E-A4, E-C1, E-C4 and E-D2 were run only on the real API. The handover's section 16 says "MSW first, then the real API", but the real run is the stronger evidence and every criterion that cites them has a real PASS.
   - L-3 (full 2678-test MSW suite) is not a section 18 criterion. Instead there are: the full Vitest suite at a5331c33 (227 files, 1674 tests); the 28/28 RPT MSW specs; the 164-pass landing-affected group; and this gate's re-runs at 538f2fea. Row 94 records the process gap.
2. Product deviations needing owner sign-off. All are declared in section 1 Amended, and none hits a section 20 stop that was worked around:
   - presets: only "Default: {period}" because the API sends no presets. Computing them would be the section 20.2 date arithmetic, so the builder stopped and asked instead;
   - nav: shown to every role, with a page refusal;
   - Mismatch or Matched instead of Over or Short;
   - recipients not named in refusals;
   - chart ticks with no currency glyph.

   Each meets the section 18 criterion it touches, as argued above. They are owner questions, not failures.
3. Row 89 does not fail RPT-002's own criterion (A15 ruling above). It does mean UI-1 is broken across the staff shell for real multi-branch data, so it should not wait long (owner question 6).
4. Backend follow-ups.
   - Recorded: CORS `Content-Disposition`, `Retry-After` and `ETag` (row 88); a typed variance flag (row 71, "Open").
   - Not recorded until this gate: server-resolved presets (Amended (1)), naming the refused recipient (Amended (6)) and `roles` on NavItem (Amended (9)). Rows 97-99 are proposed below.
   - Row 71 is filed as a frontend row. It should also be tagged as an HO-RPT-001 follow-up so the backend owner sees it (row 100).

SECTION 19 ACCEPTANCE FOCUS
- The landing-route change is recorded as a product decision: ADR-FE-130 in web/docs/decisions.md:1537 ("OQ-1 default taken (yes)"), as corrected by Amended (19). I found no explicit owner confirmation of OQ-1 in the repo; the handover's default was taken (owner question 1).
- Drill-down links omitted for areas without frontends (OQ-4), as checked in the source:
  - dashboard tiles link only to `/staff/reports/{reportKey}` (DashboardTile.tsx `drillDownHref`), or render no link for `drillDown: null`, tested by U-A4;
  - no component under components/{dashboard,reports,report-schedules} or the new routes links into the queue, boarding, reconciliation, cash-up, orders or import screens (HO-APT-002, HO-BRD-002, HO-BIL-002, HO-ORD-002, HO-IMP-002);
  - the only cross-area link is RunHistoryTable's `/staff/notifications`, and HO-NTF-002 is built.

  So the omitted set is "every area screen"; none is rendered dead.

SCOPE
- Out-of-scope work found (section 4): none.
  - No backend file is in the range.
  - No new dependency: package.json and the config files are unchanged.
  - No client aggregation: source-scan, plus my reading of ReportChart and lib/format/report.ts.
  - No per-report component (ADR-FE-129), no customer reporting, and no edits to the queue, boarding or billing screens.
- Files outside allowed scope: none unaccepted. The range changes 80 files. Those outside section 13's literal list are:
  - nav/areas/reporting.ts, lib/hooks/useBranchOptions.ts and tests/e2e/reports-helpers.ts, declared in Amended (12) and accepted by ARCHITECTURE;
  - docs/backlog/RPT.md, docs/handover/HO-RPT-002.md and docs/handover/README.md, accepted by ARCHITECTURE with precedent;
  - web/tests/e2e/home.spec.ts (one assertion and the test name), added by the coordinator's scope amendment, Amended (19). It is an HO-CAT-002/HO-ACC-002 file, so owner question 8 asks for ratification.
  - adminNavItems.ts is the one line section 13 allows.
  - web/app/(staff)/layout.tsx is not touched; the nav went to the registry instead (Amended (9)).

UNDECLARED DEVIATIONS
- U-A5 is recorded as PASS on the TC sheet, but the test cannot fail for the behaviour it names (row 96). This is a test-quality gap, not a product deviation. A3 is MET on source.
- 538f2fea undoes ARCHITECTURE row 84's "ticks through formatCell": ticks are now `groupDigits(String(value))`, with the currency only in the axis title. Row 90 declares this, and the coordinator named it. However:
  - Amended (19) mentions only the axis width;
  - ReportChartCanvas.tsx:40's docstring still says ticks go through `formatCell`;
  - TC rows RG-11 and B-CHART evidence the old tick text;
  - no test asserts the tick text or that the axis title carries the currency.

  LOW, row 101.
- docs/backlog/RPT.md:129, the unnumbered "(high)" row, still reads "Open (gate HIGH)", but row 95 resolves the same finding. It is stale (row 101).

FINDINGS
- [MEDIUM] web/tests/components/dashboard/dashboard.test.tsx:130-161. U-A5 renders ConfirmDialog as a sibling with `open` hard-coded, so it passes whatever the dashboard does. It asserts no focus retention and no polite announcement. Row 96.
- [MEDIUM] web/components/branches/ActiveBranchPicker.tsx (HO-ADM-002, not changed here). The existing row 89 is confirmed as pre-existing on master and owned by HO-ADM-002.
- [LOW] Backend and owner follow-ups not in the backlog: presets, the refused recipient's name, and NavItem roles. Rows 97-99.
- [LOW] Row 71 is not visible to the backend owner. Row 100.
- [LOW] web/components/reports/ReportChartCanvas.tsx:40: a stale docstring, no tick or axis-title assertion, and stale TC evidence. docs/backlog/RPT.md:129: a stale "(high)" row. Row 101.
- [LOW] The real-API A4 evidence views surgery-register and vaccination-due-list only empty. Row 102.

PROPOSED docs/backlog/RPT.md ROWS (the file is at 95)
96 | MEDIUM | web/tests/components/dashboard/dashboard.test.tsx:130 | U-A5 is decoration. The dialog is a sibling hard-coded `open`, with no focus or announcement assertion. Render the real `/staff` page with a focused control or an open dialog inside the page tree, advance the poll with fake timers, and assert three things: the dialog is still open, `document.activeElement` is unchanged, and role=status says "Dashboard updated". | ACCEPTANCE Open (RPT-002).
97 | LOW (backend follow-up) | HO-RPT-001 catalogue | Server-resolved date presets `presets: [{ label, from, to }]` per report, so the filter bar can offer OQ-2's Today, Yesterday and the rest without client date maths (Amended (1), ADR-FE-131). | ACCEPTANCE Open (RPT-001, owner decision).
98 | LOW (owner / HO-ACC-002 follow-up) | web/nav/types.ts, StaffShell | An optional `roles` on NavItem, so Reports is hidden for roles with no report category (section 11.1, U-B2, E-A2 "entry hidden"). Today every role sees it and gets the page refusal (Amended (9)). | ACCEPTANCE Open (owner decision).
99 | LOW (backend follow-up) | HO-RPT-001 BusinessViolationCodes.Reports | ReportRecipientNotPermitted, ReportRecipientHasNoEmail and ReportRecipientNotAssignedToBranch should identify the recipient (an id or an indexed propertyName), so the editor can say "{name} can't open this report" (section 11.7, U-E5, Amended (6)). | ACCEPTANCE Open (RPT-001).
100 | LOW (backend follow-up) | HO-RPT-001 R13/R14 | A typed variance direction (Over/Short) with one sign convention across R13 and R14, so the UI stops keying "Mismatch" on column key `variance` and a digit test (Amended (7), row 71). | ACCEPTANCE Open (RPT-001, owner decision).
101 | LOW | ReportChartCanvas.tsx:40, docs/backlog/RPT.md:129 | Three items. (a) The docstring says ticks go through formatCell, but since 538f2fea they are `groupDigits` with the currency in the axis title; add a test pinning the tick text and that the axis title names the currency. (b) TC rows RG-11 and B-CHART evidence the old tick text. (c) The unnumbered "(high)" row still reads Open but is superseded by row 95; mark it or remove it. | ACCEPTANCE Open (RPT-002 / integrator).
102 | LOW | reports-real-api.spec.ts / seeder | A4's real evidence opened surgery-register and vaccination-due-list only empty. Seed a surgery and a due vaccination and assert rows (fold into rows 73 and 93). | ACCEPTANCE Open.

OWNER QUESTIONS
1. OQ-1 / ADR-FE-130: confirm `/staff` as the staff landing. The handover's default was taken; I found no explicit owner record.
2. Amended (1): accept a single "Default: {period}" preset until the server sends presets (row 97)?
3. Amended (9): accept "Reports shown to every role, refused on the page" instead of "hidden", or approve `roles` on NavItem (row 98)?
4. Amended (7): accept "Mismatch" / "Matched" with an icon instead of Over / Short, or ask HO-RPT-001 for a direction (row 100)?
5. Amended (6): accept the server's generic sentence on the recipients field instead of naming the person (row 99)?
6. Row 89 (HO-ADM-002): the picker overflow breaks UI-1 on every staff page at 360 px for multi-branch staff with long branch names. Schedule the one-class fix before go-live?
7. Chart ticks: accept grouped numbers with the currency named only in the axis title?
8. Ratify the coordinator's scope amendment, Amended (19): one assertion in HO-CAT-002/HO-ACC-002's web/tests/e2e/home.spec.ts.
9. REAL:E-D2 per-recipient delivery is still unobserved. Rerun it on a host with the capturing email sender (row 91).

SCOPE CHECK
- files in allowed scope: 80/80. Section 13, plus Amended (12) and (19), plus the docs accepted by ARCHITECTURE; see SCOPE above.
- files outside allowed scope: none
- this gate wrote only docs/gates/HO-RPT-002-ACCEPTANCE.md
