GATE: CODE_REVIEW
HANDOVER: HO-BRD-002
COMMITS: 5a187b6b..75cb8e85
DATE: 2026-10-15
STATUS: PASS

No BLOCKER or HIGH finding. Per the gate blocking rule, the MEDIUM and LOW rows below go to
docs/backlog/BRD.md, not against the gate. Reviewed by reading the diff and a targeted vitest run
(mock-api, wire-final, wire-adapters: 31 pass, 1 fail, the known time-of-day failure in M2).
Checked and found correct: stayId in the URL with Resume and the gate; PayByCountdown on useServerNow;
single 48px Done tap (min-h-12) with optimistic update, rollback and abort-based Undo; the task list
sends date (branch today); approvals body {approve, note}; belongings fileIds through FileUploader;
check-out preview before submit with Keep/Void/Record lines, totals and credit; Extend/Relocate send
expectedVersion; "Cat is back"; dashboard renders 11 cards; the gate link keeps only path and query.
All dates are branch-local yyyy-MM-dd strings via todayInZone/addDaysIso/UTC arithmetic; no
browser-zone day bug found in components or lib/api. No MongoDB/MassTransit/Service Bus items apply (FE only).

FINDINGS
- [MEDIUM] web/components/boarding/booking/BookingFlow.tsx:93 (enquiryQuote state) and :222 (review step) — the enquiry quote is kept only in React state. The stay view has quote null while the stay is an Enquiry, so a reload on the review step, or Resume from My stays when the gate and profiles are already satisfied (firstStep returns 'review'), shows Review and Reserve with no quote and a Reserve confirmation with no total. Fix: when landing on 'review' with stay.quote null, re-call changeEnquiry with the stay's own values (as resume() does) to get the quote, or disable Reserve until a quote is known. Add a test that reloads at review.
- [MEDIUM] web/mocks/handlers/boarding/store.ts:274 — the mock stay view returns stay.quote for an Enquiry, but the built API returns null (BookingFlow.tsx comment, real-API spec line 162). The mock hides the M1 bug. Fix: return quote null for status Enquiry in the mock and add a mock-based reload-at-review test.
- [MEDIUM] web/mocks/handlers/boarding/seed.ts:747-831 with web/tests/components/boarding/mock-api.test.ts:213-217 — task fixtures are built from ago(n)/ahead(n) relative to Date.now(), but the day list filters on the Asia/Dhaka local date. Between 00:00 and about 03:20 Dhaka the Done task (ago(200)) lands on yesterday and the test fails (reproduced at 01:41 Dhaka); ahead(240) and ahead(120) spill into tomorrow late in the evening. Also affects the dev mock and e2e task board runs. Fix: anchor due times with at(T, 'HH:MM'), or use vi.setSystemTime pinned to midday Dhaka in the suite.
- [MEDIUM] web/lib/api/boarding-care.ts:60-96 (normaliseTaskDayList) — the built task day list has no per-cat not-eating flag, so cats[].notEating is always false against the real API. The task board and ObservationSheet (notEatingOf in TaskBoard.tsx) therefore never show the not-eating warning on real data, though the mock shows it (mock-api test asserts Noodle notEating true). The dashboard does carry it. Fix: take the flag from the dashboard/observations read, or ask BE for it; until then say so in the UI spec and add a real-API assertion.
- [MEDIUM] web/components/boarding/stay/EarlyCheckOutPreview.tsx:27-30 — when the preview query errors the component returns null, so staff can press Check out on an early check-out with no preview and no warning. Fix: show an inline error with Retry, and a note that charges will be recalculated on submit. Also CheckOutWizard.tsx:44 decides "early" from the browser clock; use the server's today (preview.today) or the server clock offset.
- [MEDIUM] web/components/boarding/incidents/IncidentForm.tsx and web/lib/api/boarding-care.ts:217 — the form never asks when the incident happened; occurredOn is always defaulted to submit time. A late report is therefore recorded with the wrong occurrence time. The field is sent (requirement met) but is not user-set. Fix: add an optional "When did it happen" time input in branch zone, defaulting to now.
- [LOW] web/lib/hooks/useBoardingCare.ts:107-144 / TaskBoard.tsx:76-95 — Undo only aborts the fetch. If the POST already reached the server the task is Done there while the live region says "Undone ... still to do" until the onSettled refetch corrects it. Concurrent completions share per-query snapshots, so a rollback of one can briefly revert another's optimistic Done. Fix: word the announcement as "Undo requested" and confirm after refetch; roll back per task.
- [LOW] web/tests/components/boarding/care-and-stay.test.tsx:59 — the 48px requirement is asserted as a class-name regex (min-h-12); jsdom has no layout. Fix: the a11y e2e should assert boundingBox().height >= 48 on a real Done button.
- [LOW] web/tests/components/boarding/wire-final.test.ts:111-124 and built-wire.json:755-811 — the captured occupancy has inTurnaround false in every cell and the test asserts only catNames[0], so the inTurnaround mapping (staff-boarding.ts:321) and day-cell merging are not tested against wire JSON. Fix: capture an occupancy with a turnaround cell and a multi-day stay, assert turnaroundDays and fromOn/toOn.
- [LOW] web/tests/e2e/boarding-staff.spec.ts:78,137 — waitForTimeout(800) used to wait for a refetch; flaky by design. Fix: wait on the request or a locator state.
- [LOW] web/components/boarding/stay/StaffStayBookingForm.tsx:96 and web/lib/api/staff-boarding.ts (occupancy branchTimeZoneId fallback) — Asia/Dhaka hard-coded instead of the branch zone; wrong for the London seed branch. Fix: use useBranchTimeZone().
- [LOW] web/lib/api/types.ts (CheckOutPreview) — no isLate in the wire or the type; the check-out preview is fetched only for early stays, so a late check-out has no preview. Nothing to fix now; if BE adds isLate, extend the preview condition.
- [LOW] Real-API coverage gaps (known): lapse, waitlist notice, 60-night invoice, axe on real data and two E-E cases are mock-only. Record in the backlog; booking-quote-on-resume (M1) should join the real-API flows.

SCOPE CHECK
- files in allowed scope: 122/122 (web/ only; nav/areas/boarding.ts edit is the BRD nav)
- files outside allowed scope: none

## Proposed rows for `docs/backlog/BRD.md` (numbered from 54, after the ARCHITECTURE rows 45-53; status after the 2026-10-15 fix round)

| # | Sev | Status | Where | Item |
|---|---|---|---|---|
| 54 | MEDIUM | Fixed (fix round) | `BookingFlow.tsx`, `StaffStayBookingForm.tsx` | Re-fetch the enquiry quote after a reload or resume at the review step; Reserve disabled without a quote. |
| 55 | MEDIUM | Fixed | `mocks/handlers/boarding/store.ts` | The mock returns `quote: null` and `balanceDue: null` for an Enquiry, like the real API. |
| 56 | MEDIUM | Fixed | `mock-api.test.ts` | The suite pins the clock to midday in Dhaka. |
| 57 | MEDIUM | Fixed (BE row 70 open) | `boarding-care.ts`, `TaskBoard.tsx` | Absent not-eating flag is `null` on the task list; the board takes it from the dashboard `notEating[]`. |
| 58 | MEDIUM | Fixed | `CheckOutWizard.tsx`, `EarlyCheckOutPreview.tsx` | Early/late from the server's preview; an error with Try again when it fails; the server still recalculates on submit. |
| 59 | MEDIUM | Fixed | `IncidentForm.tsx` | "When did it happen" date and time (branch time, default now). |
| 60 | LOW | Open | `useBoardingCare.ts`, `TaskBoard.tsx` | Undo only aborts the fetch: word the announcement as "Undo requested"; roll back per task. |
| 61 | LOW | Fixed | `boarding-staff.spec.ts` | The 48 px Done target is asserted on a real bounding box. |
| 62 | LOW | Open | `wire-final.test.ts`, `built-wire.json` | Capture an occupancy with a turnaround cell and a multi-day stay; assert `turnaroundDays` and bar `fromOn`/`toOn`. |
| 63 | LOW | Fixed | `boarding-staff.spec.ts` | `waitForTimeout(800)` replaced by a quiet-network wait. |
| 64 | LOW | Open (joins 49) | `StaffStayBookingForm.tsx`, `staff-boarding.ts` | Branch zone instead of a literal `Asia/Dhaka`. |
| 65 | LOW | Fixed | `types.ts`, `EarlyCheckOutPreview.tsx` | `isLate` is read; a late check-out shows the server's recalculation. |
| 66 | LOW | Open | real-API coverage | Lapse, waitlist notice, 60-night invoice, axe on real data, E-E2/E-E4 and the booking-quote-on-resume case are not yet in the real-API spec. |
| 70 | LOW | Open (BRD-001) | `GET /api/staff/boarding/tasks` | Add a per-cat not-eating flag (and `minutesLate`) to the task list so the board need not read the dashboard. |
