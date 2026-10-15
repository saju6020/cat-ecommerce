GATE: CODE_REVIEW
HANDOVER: HO-RPT-002
COMMITS: 5a187b6b..09556784
DATE: 2026-10-14
STATUS: PASS

No BLOCKER or HIGH findings. Medium and low findings are proposed as docs/backlog/RPT.md rows 67-73.

CHECKED AND CORRECT
- Filters: no Date arithmetic anywhere in the report/dashboard/schedule code (source-scan test enforces it); range seeded from the server's defaultRange; "Default: {period}" preset plus two yyyy-MM-dd fields labelled with the zone; "All branches" only when canUseAllBranches; groupBy only for timeBucketed and Day is the omitted default. A present-but-empty from= is kept so the server answers.
- compatiblePeriods mirrors ReportScheduleCalculator.IsCompatible line for line (forward/AsAt/Today rules). Preview is a server call (toPreviewInput returns null for an incomplete draft). Pause/Resume/Cancel/Run now hit the real routes (pause|resume|cancel|run-now, preview, runs, recipients); run-now body (runId, status, rowCount, failureCode), create 201 {scheduleId,nextRunOn} and 400 ReportScheduleCancelled match the controller. Cancelled schedules show no actions. Confirm dialogs name the schedule and recipient count.
- formatCell is the single path (table, cards, totals, chart hidden table, dashboard); dates are formatted by string slicing; instants use the named zone; null renders "—" + sr-only "Not recorded".

FINDINGS
- [MEDIUM] web/lib/api/client.ts:327, web/lib/download/reportExport.ts:85 — when Retry-After is absent or unreadable the error carries a guessed 60, and describeRateLimited states "Try again in 60 seconds" as fact. Hedge the wording when the value is the fallback (e.g. "Try again in a minute or so"), or expose the header. The only 429 test constructs ApiRateLimitedError(30) directly, so the fallback and the Run-now 429 (ReportRunNow limiter) are untested.
- [MEDIUM] web/app/(staff)/staff/admin/report-schedules/new/page.tsx:19,73 — the post-save navigation is a bare setTimeout(…, 400) working around UnsavedChangesGuard's history.back(). It is not cleared on unmount, so a user who navigates away within 400 ms is pulled to the new schedule, and on a slow device the 400 ms may be too short (the push is then aborted by the pop and the user stays on a saved form, where a second Submit creates a duplicate). Fix the guard to expose a "settled" signal, or clear the timer on unmount and disable submit once isSubmitSuccessful.
- [MEDIUM] web/components/report-schedules/ScheduleForm.tsx:chooseReport + sched-branch select, new/page.tsx:41 — branchId '' means "all branches" but the "All branches" option only renders when the chosen report canUseAllBranches. Prefill with ?branchId=all (or switching from an all-branches-capable report to one that is not) leaves the form value '' while the select visibly shows the first branch; the save then sends branchId null. Reset branchId to a real branch in chooseReport when !canUseAllBranches, and map 'all' only for reports that allow it.
- [LOW] web/tests/e2e/reports-helpers.ts:75-90 (openReportsAs) — the retry loop around sign-in and the shell is an MSW start-up race hack; it can hide a real sign-in or redirect regression by retrying 3 times and only throwing at the end. Wait for the MSW worker to be ready instead.
- [LOW] web/lib/format/report.ts:132 isNonZeroAmount — "Mismatch" is decided by /[1-9]/ on the amount text. It is correct for server decimal text ("0.00", "-0.00" are Matched; "0.01" Mismatch) and avoids arithmetic, but it is only keyed on column key === 'variance'; a renamed column silently loses the word. Add a unit test pinning "0", "0.00", "-0.00", "0.01", "-5.00", and ask the server for a typed variance flag.
- [LOW] web/tests/components/reports/viewer.test.tsx:77-79, dashboard.test.tsx:160 — several assertions are length > 0 on getAllByText ('Not recorded', 'Mismatch', '12'), which pass if the text is rendered anywhere on the page; assert on the specific cell/row. Fixtures are pinned to 2026-10-13/14 strings but are server-supplied data, not clock reads, so they are not time-dependent (no finding).
- [LOW] web/components/report-schedules/ScheduleForm.tsx — editing a schedule whose stored period is no longer in compatiblePeriods(report) renders a blank period select (no matching option) with no explanation; the server then answers InvalidReportSchedulePeriod on save. Keep the stored period as an extra option, labelled.
- [LOW] web/tests/e2e/reports-real-api.spec.ts — the real-API e2e ran in split groups and did not cover E-A3, the 429 limiter, or a chart with data. These are untested against the real backend (the chart path and the 429 header path are only mock-covered).

PROPOSED docs/backlog/RPT.md ROWS (highest existing: 66)
67 MEDIUM 429 copy states a guessed 60 s when Retry-After is absent; add hedged wording and a test of the fallback and of the Run-now 429.
68 MEDIUM New-schedule post-save navigation uses an uncancelled 400 ms setTimeout against UnsavedChangesGuard; race on slow devices / unmount; double-submit risk.
69 MEDIUM ScheduleForm branch '' vs report without canUseAllBranches: shown branch differs from submitted (all branches).
70 LOW E2E openReportsAs retry loop masks sign-in regressions; wait for MSW readiness.
71 LOW Variance Mismatch/Matched keyed on column key 'variance' and a digit regex; pin with a unit test, request a typed server flag.
72 LOW Weak length>0 assertions in viewer/dashboard component tests.
73 LOW Edit form with a stored incompatible period shows a blank select; E-A3, the real 429 limiter and a data-bearing chart have no real-API e2e coverage.

SCOPE CHECK
- files in allowed scope: 71/71 (web/ frontend, docs/handover and docs/test-cases for this handover; no backend, no .ai/)
- files outside allowed scope: none
