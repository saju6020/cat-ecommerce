GATE: ARCHITECTURE
HANDOVER: HO-RPT-002
COMMITS: 2f6d1d8a 60518928 e0f17f0e 5bd83b64 09556784 (range 5a187b6b..09556784, branch ho/RPT-002, 71 files)
DATE: 2026-10-14
STATUS: FAIL

The gate fails on one BLOCKER, and it is a one-file revert: `web/public/mockServiceWorker.js` was committed with formatting-only changes. It is outside §13 and is a HO-CAT-002 scaffold file under §14. There are no HIGH findings. Apart from that file, the slice complies with the approved frontend architecture.

Reviewed against:
- HO-RPT-002 §1 Amended (1)–(17), §3, §4, §6–§14, §19, §20;
- `web/docs/decisions.md`, `nav/types.ts`, `lib/api/types.ts`'s header, `lib/hooks/useIsNarrow.ts`, the `lib/api/inventory-download.ts` precedent;
- `docs/handover/README.md` (the ADR-FE allocation table);
- the merged HO-RPT-001 backend (`ReportSchedulePeriods.IsCompatible`, `ReportCatalogue.*`, `DashboardTileSources`).

This is a web-only diff, so the `.ai/` backend checklist has nothing to apply to (HO-RPT-002 §6 says so, as HO-CAT-002 and HO-ACC-002 do). No build, lint or full suite was run. One targeted vitest run was done: `tests/components/reports/source-scan.test.ts` and `format.test.ts`, 2 files, 94 tests, all pass.

Owner rule (2026-09-27): only BLOCKER and HIGH fail a gate. MEDIUM and LOW are proposed below as rows for `docs/backlog/RPT.md`, which this gate does not edit. Rows 67–73 are already proposed by this branch's CODE_REVIEW record (e4d4731c), so this gate numbers from 74. The integrator should renumber if needed.

FINDINGS
- [BLOCKER] web/public/mockServiceWorker.js (commit 2f6d1d8a): outside §13, and a HO-CAT-002 scaffold file, so forbidden by §14 ("every other HO-CAT-002 … file"). Amended (12) does not declare it.
  - What changed: `msw init` regenerated the file. Semicolons, trailing commas and line wrapping are gone; `PACKAGE_VERSION` 2.15.0 and `INTEGRITY_CHECKSUM` are unchanged.
  - Functionally a no-op: with whitespace, `;` and `,` stripped, the old and new contents are identical.
  - It still has an effect: it undoes the Prettier commit 1ae949ce for this file, and `web/.prettierignore` does not exclude `public/`, so `npm run format:check` will probably flag it.
  - Prior gates (BUGS-APT-WEB, BUGS-NTF, CAT-002-ADDCAT) left this same local regeneration uncommitted.
  - Fix: `git checkout 5a187b6b -- web/public/mockServiceWorker.js`, then commit. No owner decision is needed.
- [MEDIUM] web/lib/format/report.ts:200-216 `compatiblePeriods`. Ruling: duplicating `ReportSchedulePeriods.IsCompatible` is ACCEPTABLE here and is not a HIGH, for these reasons:
  - it filters options; it computes no figure, recurrence or date (ADR-FE-131 and ADR-FE-136 are not engaged);
  - the server re-validates and `InvalidReportSchedulePeriod` is mapped;
  - it is disclosed in Amended (2) and ADR-FE-136;
  - it is line-for-line faithful to `ReportScheduleCalculator.cs:71-80`;
  - U-E1 demands it while the API sends no allowed-period list;
  - the client Luhn guard (BIL ADR) is a precedent for a mirrored rule.
  The residual risk is drift: the rule exists in two languages with no contract between them. If the backend widens the rule, the editor hides a valid period and gives the user no way to choose it. Row 74.
- [MEDIUM] web/docs/decisions.md: 24 lines inside other handovers' ADRs (APT, EMR, BIL, IMP, ADM, PHM, ORD, POR) are reformatted by Prettier (`*x*` to `_x_`). §13 grants this file for ADR-FE-129…137 only. The change is cosmetic, but it adds merge-conflict surface for every open branch that edits those lines. Revert those hunks, or let the integrator take the reformat as a separate commit. Row 75.
- [MEDIUM] web/components/report-schedules/ScheduleForm.tsx (366 lines), components/reports/ReportFilterBar.tsx (211), ReportViewer.tsx (166) and ReportTable.tsx (160) are over §11's "each component under ~150 lines". Candidate splits:
  - ScheduleForm: the frequency fieldset and the branch/period rows into their own components;
  - ReportFilterBar: the date-range block.
  Row 76.
- [LOW] web/lib/download/reportExport.ts:43-96: the XHR status-to-error-class mapping is a near-verbatim third copy of `lib/api/inventory-download.ts` (and `files.ts`). It follows the accepted precedent, but it should be one shared helper in `lib/api`. Row 77 (integrator).
- [LOW] web/lib/api/errors.ts:1249 `REPORT_SCHEDULE_FIELD_MAP` is exported and never imported. `ScheduleForm.tsx:31` defines its own `FIELD_MAP`. Keep one. Row 78.
- [LOW] web/lib/hooks/useReports.ts:24 and useReportSchedules.ts:24: `retryTransient` is duplicated. Row 79.
- [LOW] web/components/reports/CellValue.tsx:38-51 and components/dashboard/TileFigure.tsx:34-44: the Mismatch/Matched badge markup is duplicated. Extract a `VarianceBadge`. Row 80.
- [LOW] web/docs/decisions.md ADR-FE-130 Status says "Tests that asserted the old landing were updated". No existing test file changed, and Amended (12) says none broke; correct the ADR. Separately, `web/app/page.tsx:33-38` (header comment) still says a staff member "belongs on `/staff/cats`". That comment is outside §13's "redirect target only", so it is recorded for the HO-ACC-002/HO-CAT-002 owner. Row 81.
- [LOW] docs/handover/README.md:99, the ADR-FE allocation row, says "(129–137 used)". The branch uses 129–138. The integrator should update it at merge. Row 82.
- [LOW] web/mocks/handlers/reports/dashboard.ts: the mock tile key `payments.today` does not exist in the backend's `DashboardTileSources` (the real keys include `billing.payment-received` and `billing.cash-up`). It is harmless because tiles are rendered generically, but the mock has drifted from the wire. Row 83.
- [LOW] web/components/reports/ReportChartCanvas.tsx:43-47 and ReportChart.tsx:47-58: the Y-axis tick labels are recharts-generated numbers from `toChartNumber`, not server text. The canvas is `aria-hidden` and the tooltip and hidden table use `formatCell`, so ADR-FE-134 holds. Two gaps: the visible ticks carry no money formatting, and the axis currency is taken from the first row only. Row 84.
- [LOW] docs/handover/HO-RPT-002.md §8.2 tells the builder to put types in `lib/api/types.ts`, which that file's own header forbids. This is a handover defect; the builder correctly followed the convention. No row needed; note for the handover author.
- Already covered, no new row: the variance word is keyed on the literal column/figure key `variance` (`lib/format/report.ts:137`). This is CODE_REVIEW's proposed row 71.

RULINGS REQUESTED BY THE COORDINATOR
- **Nav: compliant, no finding.**
  - The shell reads the `nav/` registry (`nav/types.ts`; web/README "Adding a functional area"). `(staff)/layout.tsx` holds no nav entries, and `nav/areas/reporting.ts` is the Reporting area's own file. Editing it is the correct substitute for §13's layout line.
  - Showing Reports to every role matches every other area: Billing, POS, Procurement and Controlled drugs are all unfiltered. Role hiding is presentation only (HO-ADM-002 §11.1, ADR-FE-009), and `NavItem` has no `roles` field.
  - The server's 403 renders `PermissionDeniedState` on `/staff/reports`, through `ReportErrorView`.
  - §11.1 / U-B2 / E-A2 "entry hidden" becoming "page refused" is a product deviation for ACCEPTANCE. The owner question (`NavItem.roles`) is recorded in Amended (9).
  - `adminNavItems.ts`: one entry plus one comment line. `roles: ['Administrator']` matches `Reports.Schedule`.
- **Types in `lib/api/{reports,dashboard,report-schedules}.ts`: compliant.** `types.ts:7-10` forbids domain types, and every area keeps its own.
- **640 px container instead of a 768 px viewport: compliant.** It reuses the shared `useNarrowContainer` / `NARROW_CONTENT_PX = 640` from HO-IMP-002 (`StackedTable`, `SkuStockMatrix`), and the handover-owned ADR-FE-133 is written to match. Whether there is actually no overflow is runtime, for TEST (U-C6, E-X2).
- **Extra files: all four accepted.**
  - `nav/areas/reporting.ts`: see Nav above.
  - `tests/e2e/reports-helpers.ts`: test support, not §14, disclosed in Amended (12). This follows the HO-APT-002 precedent for files declared in Amended.
  - `tests/e2e/reports-real-api.spec.ts`: literally matches §13's `reports-*.spec.ts`.
  - `lib/hooks/useBranchOptions.ts`: literally inside `web/lib/hooks/**` ("new hooks only"). It composes existing hooks and fetches nothing new.
  - `.env.local` is not in any commit.
- **IsCompatible duplication: acceptable** (see the MEDIUM above, row 74).
- **"Absent" rule: verified.** All 71 changed files were searched for:
  - the R21–R26 keys: `subscription-health`, `cross-line-conversion`, `cart-abandonment`, `boarding-occupancy`, `boarding-care-compliance`, `boarding-incidents`;
  - `boarding.*`, `subscription` and `R2[1-6]`.
  No added code matches. The only hits are pre-existing lines of `decisions.md` and `mocks/handlers/index.ts`.
  - The mock catalogue has 7 entries: clinic-daily-schedule, vaccination-due-list, stock-near-expiry, revenue-shop-by-category, revenue-by-business-line, payment-reconciliation, new-vs-returning. The mock dashboard has 7 tiles, none of them boarding or subscription.
  - "Boarding" appears only as a `ReportCategory` value and its label. `groupByCategory` drops empty groups, so no Boarding heading can render.
  - The R9 mock description's "boarding" is the backend's own description text (`ReportCatalogue.Financial.cs:23`).
- **ADR-FE-129..138: no collision.**
  - Master's ADR-FE headings stop at 123, 126, then 149–164 and 170–172. None is in 129–138.
  - Of all local `ho/*` branches, only `ho/RPT-002` contains ADR-FE-129..138.
  - README:99 allocates 129–138 to HO-RPT-002. SRG-002 (069–078) and XCT-002 (139–148) do not overlap.
- **Single `formatCell` path: yes.**
  - Table, card list and totals go through `CellValue` → `formatCell`.
  - The chart's hidden table and tooltip use `formatCell` text.
  - Tiles go through `TileFigure` → `formatFigure` → `formatCell`.
- **No domain logic in components: yes.** The only rule-like code is in `lib/format` (the `isNonZeroAmount` string test and `compatiblePeriods`). U-X3's source scan passes.
- **web/ conventions: met.**
  - No `useQuery`/`useMutation` in any new component or page; every hook is in `lib/hooks`.
  - No `fetch` outside `client.ts`. The export uses XHR in `lib/download`, following the `inventory-download.ts` precedent, with `withCredentials` and `X-Branch-Id`.
  - No `dangerouslySetInnerHTML`, `localStorage` or `sessionStorage`.
  - Filters live in the URL.
  - Money stays a string, except the chart's plot-only `toChartNumber`.
  - No new dependency; `package.json` is untouched.
  - `errors.ts` is append-only. The `app/page.tsx` and sign-in edits are the redirect target only.

FOR LATER GATES
- TEST:
  - horizontal overflow at 360/768/1024/1440 with the 640 px container switch;
  - polling paused while the tab is hidden;
  - the debounced preview;
  - the XHR export headers in a real browser.
- ACCEPTANCE: these deviations are product changes, not architecture:
  - Amended (1): one preset instead of OQ-2's list (A5);
  - Amended (9): nav entry not hidden (U-B2/E-A2);
  - Amended (7): no Over/Short words (A10);
  - Amended (6): the recipient is not named (U-E5).

PROPOSED BACKLOG ROWS (docs/backlog/RPT.md)
- 74 MEDIUM: `compatiblePeriods` mirrors `ReportSchedulePeriods.IsCompatible` across languages. Have the catalogue send `schedulablePeriods` per entry and drop the mirror. Owner: RPT-001/RPT-002.
- 75 MEDIUM: decisions.md: Prettier reformatted 24 lines of other handovers' ADRs (`*x*` to `_x_`). Revert those hunks or land the reformat separately. Owner: integrator.
- 76 MEDIUM: ScheduleForm (366), ReportFilterBar (211), ReportViewer (166) and ReportTable (160) exceed §11's ~150 lines. Split them.
- 77 LOW: XHR blob request and error mapping is copied three times (`reportExport.ts`, `inventory-download.ts`, `files.ts`). Extract one helper. Owner: integrator.
- 78 LOW: `REPORT_SCHEDULE_FIELD_MAP` (`errors.ts:1249`) is unused, and ScheduleForm has its own `FIELD_MAP`. Keep one.
- 79 LOW: `retryTransient` is duplicated in `useReports.ts` and `useReportSchedules.ts`.
- 80 LOW: the Mismatch/Matched badge is duplicated in CellValue and TileFigure. Extract `VarianceBadge`.
- 81 LOW: ADR-FE-130's "tests … were updated" is inaccurate, and the `app/page.tsx` header comment still names `/staff/cats` as the staff landing. Owner: ACC-002/CAT-002.
- 82 LOW: handover README:99 should read "129–138 used". Owner: integrator, at merge.
- 83 LOW: mock tile key `payments.today` is not a backend tile key. Align the mock dashboard with `DashboardTileSources`.
- 84 LOW: chart Y-axis ticks are unformatted client numbers, and the currency is taken from the first row only.

SCOPE CHECK
- files in allowed scope: 70/71
  - Explicitly in §13 or inside its globs: 64.
  - The handover's own Amended row (append-only, per the HO-ADM-002 and HO-APT-002 precedent): 1.
  - `adminNavItems.ts`, one entry: 1.
  - `nav/areas/reporting.ts` and `tests/e2e/reports-helpers.ts`, disclosed in Amended (12) and not §14: 2.
  - `reports-real-api.spec.ts` and `useBranchOptions.ts`, which Amended (12) also lists but which fall inside §13's own globs: 2.
- files outside allowed scope: web/public/mockServiceWorker.js (§14, undisclosed; revert)

---

## Combined re-check — 2026-10-14 (architecture, security and code-review findings)

GATE: ARCHITECTURE (combined re-check of the ARCHITECTURE, SECURITY and CODE_REVIEW findings)
HANDOVER: HO-RPT-002
COMMITS: 4bafe4f2 (fix round), on top of gate records e4d4731c 1b35cec9 5bbb9225 (range 5a187b6b..4bafe4f2, 76 files)
DATE: 2026-10-14
STATUS: PASS

The re-check passes: the BLOCKER is closed, and none of the new findings is a BLOCKER or HIGH. One targeted vitest run was done on `tests/components/report-schedules/schedules.test.tsx`, `reports/exports.test.tsx`, `reports/filenames.test.ts` and `reports/format.test.ts`: 4 files, 49 tests, all pass. No build, lint or full suite was run.

VERIFIED
1. **BLOCKER closed.** `git diff 5a187b6b 4bafe4f2 -- web/public/mockServiceWorker.js` is empty, so the file is byte-identical to master.
2. **decisions.md.** The diff against master removes no lines. It is one hunk appended after master's last line (the HO-POR-002 ADR-FE-124/125 status). The added headings are exactly `ADR-FE-129 … ADR-FE-138` and `### ADR-FE-129` … `### ADR-FE-138`. Row 75 is resolved. ADR-FE-130's Status now reads "No existing test asserted the old landing".
3. **Diffstat.** `5a187b6b..4bafe4f2` touches 76 files. Of these:
   - 71 are the files accepted in the first pass, with `mockServiceWorker.js` now dropping out as identical to master;
   - 3 are this branch's gate records;
   - 1 is the new `web/tests/components/reports/filenames.test.ts`, inside §13;
   - 1 is `docs/handover/README.md` (see the LOW below).

   Only 10 master files are modified. All of them were accepted in the first pass or are the README: `docs/backlog/RPT.md`, `docs/handover/HO-RPT-002.md`, `docs/handover/README.md`, `app/page.tsx`, `(auth)/staff/sign-in/page.tsx`, `components/admin/adminNavItems.ts`, `web/docs/decisions.md`, `lib/api/errors.ts`, `mocks/handlers/index.ts` and `nav/areas/reporting.ts`. No other area's file changes, and `package.json`, `.prettierignore` and the config files are untouched. None of the ~214 reformatted files the builder reported is left in the range.
4. **Fixes.**
   - CODE 67 (429 wording): `errors.ts describeRateLimited`. A wait of 60 seconds, the fallback in `client.ts:328` and `reportExport.ts:102`, now reads "a minute or so". Other values read "about N seconds" or "about N minutes". Tested.
   - CODE 68 (post-save timer): `new/page.tsx` keeps the timer in a ref and clears it on unmount. `ScheduleForm` disables submit when `isSubmitSuccessful`.
     - A failed save does not leave the button disabled. RHF 7.88 sets `isSubmitSuccessful = isEmptyObject(errors) && !onValidError`, and the catch in `submit` always calls `setError` (field errors or `root`).
     - The edit page remounts the form with `key={detail.version}`, so a successful edit resets the flag.
     - Tested: one `onSubmit` call.
   - CODE 69 (branch reset): `chooseReport` moves `''` to `branches[0]` when the new report is not `canUseAllBranches`. `branches` holds only real branches; "All branches" is a separate `<option value="">`. In `new/page.tsx`, `?branchId=all` is honoured only for a `canUseAllBranches` report. Tested.
   - SEC (export filenames):
     - `safeFilename` (`reportExport.ts:38`): try/catch around `decodeURIComponent`, keeps the basename only, an allow-list of `[A-Za-z0-9._-]`, strips leading dots, and returns `null` when the result is empty.
     - `exportBaseName` (`ReportViewer.tsx:28`): strips everything except `[A-Za-z0-9-]` and prefers the server-normalised `result.filters`.
     - `encodeURIComponent(id)` is on all six `scheduleId` routes in `lib/api/report-schedules.ts`.
     - Unit tests are in `filenames.test.ts`.
   - LOWs:
     - Variance edge values ("0", "0.00", "-0.00", "0.01", "-5.00", null) are pinned in `format.test.ts`. The check remains a string test with no arithmetic, so ADR-FE-131 holds.
     - In `reports-helpers.ts`, `waitForMswReady` replaces the retry loop and there is one attempt only. It is an e2e helper and was not run; it is for the TEST gate.
   - Row 84 (chart ticks): the Y-axis now goes through `formatCell` via `formatTick`. This is display formatting only, with no maths. The first-row currency gap is honestly left open.
   - **Row 83: WITHDRAWN. My first-pass finding was wrong.** `payments.today` is a real backend tile:
     - `src/Shared/CatEcommerce.Shared/Common/Constants/DashboardTiles.cs:30` (Accountant and Administrator, `Reports.FinancialView`);
     - `src/Read/CatEcommerce.Read/QueryHandlers/Reporting/Dashboard/DashboardTileSources.cs:232-234` (`PaymentsTodayTile`, `Key => "payments.today"`);
     - registered in `ReportingModule.cs:125`.

     The mock matches the wire, and "NOT A DEFECT" is the correct row text.
5. **Backlog.**
   - `docs/backlog/RPT.md` adds rows 67–88 under one numbering. There are no duplicate row numbers in the file, and no master line is removed.
   - The first-pass gate records' proposed numbers are remapped and marked: SECURITY's 67–70 became 85–88, and the ARCHITECTURE and CODE_REVIEW rows keep their numbers.
   - Every "RESOLVED" matches the code above, with one exception: row 81 is slightly overstated (see the LOW below). The "Open" and "PARTLY RESOLVED" rows (71, 72, 73, 74, 76, 77, 79, 80, 84, 88) are honest.
6. **Other worktrees (report only, nothing changed).**
   - `fix3.cjs` (in the scratchpad) targets `D:/my-repo/cat_ecommerce-wt/EMR-001-F/` only, not SRG-002 or XCT-002. EMR-001-F's `git status` is clean, so the stray run wrote nothing there.
   - **SRG-002** (`ho/SRG-002` at c2e70472): the working tree is clean.
   - **XCT-002** (`ho/XCT-002` at 39ea78ec): 34 modified and 4 untracked files, all of them XCT's own gate-fix work, and every one matches HO-XCT-002 Amended (22)–(27):
     - `lib/offline/**`, `components/offline/**`, observability, operations, `public/staff/sw.js`;
     - `ServiceWorkerCleanup.tsx` with its `app/layout.tsx` line;
     - the `client.ts` 4th change and `useIdleTimeout.ts`;
     - the XCT tests, the backlog and the handover.

     Nothing there looks stray or RPT-related.

FINDINGS
- [LOW] docs/handover/README.md:99. §14 forbids `docs/**` except §13's two paths, and this edit (row 82) changes `(129–137 used)` to `(129–138 used)`. It is accepted, not a BLOCKER, for three reasons:
  - it is this handover's own allocation cell;
  - it is the exact text the first pass asked for;
  - a handover branch updating its own README allocation row has precedent: `ho/ADM-002-FU1` 2653cfe8, merged in 587ea317.

  The integrator should expect it at merge. No new row is needed (row 82).
- [LOW] web/app/page.tsx:37-39. The header comment still says `/staff/cats` is the default that `(auth)/staff/sign-in` pushes to after sign-in. Since this branch's sign-in edit, that default is `/staff`. Row 81's "RESOLVED" covers only the line-34 edit. The integrator or the next owner can fix the sentence; append it to row 81.
- [LOW] web/lib/api/errors.ts:1288. The old one-line docblock ("A `429` carries `Retry-After` … — say when.") was left above the new docblock, so `describeRateLimited` has two stacked JSDoc comments and the first now contradicts the code. Delete line 1288. Append to row 67.
- First-pass ARCHITECTURE MEDIUMs 74 and 76 and LOWs 77, 79 and 80 are unchanged and remain backlog-only.

SCOPE CHECK
- files in allowed scope: 76/76
  - The first pass's 70 accepted files.
  - 3 gate records and 1 new test file, all inside §13.
  - `mockServiceWorker.js`, now identical to master, so no net change.
  - `docs/handover/README.md`, the own-row precedent (LOW above).
- files outside allowed scope: none
