GATE: COMBINED (ARCHITECTURE + SECURITY + CODE_REVIEW)
HANDOVER: HO-RPT-001-FU1
COMMITS: 23dda084 (range 5a187b6b..23dda084)
DATE: 2026-10-14
STATUS: PASS

FINDINGS
- [MEDIUM] docs/handover/HO-RPT-001.md:279-280 (R13/R14 rows) -- the definition rows were edited in place (new column, "(FU1)" markers) with no Amended row in section 1; the handover's own rule is append-only or an Amended row. The edit is explicit and traceable to FU1, so it does not block. Add a one-line Amended row at the merge.
- [LOW] docs/handover/CONTRACTS.md row 41 -- BIL-001-J takes rows 36-40 on its branch; expect a textual merge conflict at the table end. Integrator: keep both, renumber if BIL-001-J lands first. Same for backlog row FU1-1 (RPT-002 owns 67+): integrator renumbers.
- [LOW] VarianceDirectionTests -- R13 cash row with a cash-up that has ExpectedCash but CountedCash null (open day) is only covered through R14; add an R13 case asserting null (the code path is correct: Name(null, expected) returns null).
- [LOW] PaymentReportBuilders.cs cash row -- matched/mismatchesOpen derive from the stored Variance, direction from counted vs expected. They agree today because Billing writes counted - expected; if they ever diverge the row would read Matched in one cell and Over/Short in the other. No action now.

VERIFIED (no finding)
- Direction is computed from the two compared figures (received/counted vs expected), never from variance sign. Decimal comparison, so 0, 0.00, -0.00 are Matched; +/-0.01 Over/Short. Nullable overload returns null when either figure is missing, never Matched.
- R13 COD (received = remitted capped at amount) and bank transfer (matched credits): only Short or Matched, positive variance, correct. R13 cash: counted - expected, null when not counted. R13 card: always null. R14: Closed day uses CountedCash/ExpectedCash, NotClosed row null.
- Additive column: ReportColumn.Description is a trailing optional parameter; ReportColumnViewModel likewise; no existing call site changes. Column is unrestricted (no RestrictedToAction), so cost/margin hiding is unaffected. CSV/PDF render all visible columns generically; both reports were already above the 6-column portrait limit (landscape), a null Text cell is already handled (explanation). Schedules and scheduled attachments reuse the same result pipeline. Dashboard tile reads only mismatchesOpen/variance by key. RPT-002 reads by key, an extra column and an extra JSON property are tolerated; description is null on all other columns.
- Projected reads unchanged, views untouched, no migration, no handler/DI/controller change, no auth surface change, no new input. Scope: Shared, Read, tests plus docs only.
- Tests: no vacuous assertions; boundary theory is shared by R13 cash and R14 (9 x 2), COD/bank assert both direction and the unchanged positive variance, integration I-D5/I-D6 go through the real endpoint, catalogue and run result. Application Reporting filter re-run: 84/84 pass. Integration suite not re-run by this gate (author reports 109/109 on catecommerce_rptfu1).

SCOPE CHECK
- files in allowed scope: 13/13 (Shared, Read, tests, and the named docs)
- files outside allowed scope: none
