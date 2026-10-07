GATE: CODE_REVIEW (combined: architecture, security, code)
HANDOVER: BUGS-APT-WEB (HO-APT-002 backlog rows W5, W6, W7, W8, W10, W11, W15, W16, W18 product part)
COMMITS: 35f359c 75d57dc aa6b28b 5110015 c8069a0 396d876 3fcaf67 1b622b0 7404938 (range 7a742ec...HEAD)
DATE: 2026-10-07
STATUS: PASS

CHECKS RUN
- tsc --noEmit: clean. eslint --max-warnings 0: clean. vitest --maxWorkers=3: 142 files / 744 tests passed.
- Dry-run merge of master (ho/BUGS-APT-BE) into a throwaway worktree: textually clean (auto-merged
  BookingFlow.tsx and AppointmentDetail.tsx), tsc clean, appointments/booking/calendar tests 85/85.
  MyAppointmentView's detail.* fields (branchTimeZoneId, startsOn, performerId, service, canChange, ...)
  all exist on MyAppointmentDetail.

FINDINGS
- [MEDIUM] web/components/appointments/StaffBookingForm.tsx:156-158 — W18 now makes two vets' slots at the same start and room distinct options, but book() sends only roomId and startsOn from the chosen slot. performerId comes from the filter (possibly empty), so choosing the second vet's option can book whichever vet the server picks. Send chosen.performerId.
- [MEDIUM] web/app/(staff)/staff/appointments/CalendarView.tsx:108-110 — W6 reads the live version at submit time for every submit, not just retries. A background poll that brings in another staff member's change while the dialog is open is then sent silently, which defeats the optimistic-concurrency check on the state the user reviewed (test "W6 drop path: a version refreshed while the confirmation is open" locks this in). Safer: send the dialog's version, and adopt the refetched one only after a StaleCalendarVersion error. FlaggedView has the same pattern, so the owner may accept it; record the decision.
- [MEDIUM] web/components/appointments/FlaggedAppointmentsTable.tsx:64-71,130-139 / FlaggedView.tsx:42-46 — W16: if a row's detail fetch fails, the zone stays null, so the row shows "Loading…" with Reschedule disabled forever and no retry. Add an error state or a retry.
- [LOW] web/lib/format/appointment.ts:134-136 — zonedLocalToInstant, nonexistent local time (spring-forward gap) has no stated policy and is inconsistent: New York 02:30 on 2026-03-08 resolves forward to 03:30 EDT, but London 01:30 on 2026-03-29 resolves back to 00:30 GMT, an hour earlier than typed. Ambiguous local time (fall-back overlap) resolves to the earlier instant in New York but the later one in London. All of this is deterministic and the dialog's confirm sentence shows the resolved time, so it is not a defect, but state the policy in the doc comment (e.g. "compatible": gap forward, overlap earlier).
- [LOW] web/tests/components/booking/format-appointment.test.ts — the DST test covers only ordinary times around the transitions, not the nonexistent or ambiguous ones. Add cases for NY 02:30 / 01:30 and London 01:30 on both dates. The two NY cases (11-01 03:00, 03-08 03:30) do fail under the old single-correction code, so the fix is covered.
- [LOW] web/components/appointments/ActiveBranchZoneGate.tsx:31-36 — if GET /staff/branches/mine fails, mine.data is undefined and seedPending is false, so the gate shows "Choose a branch to continue" rather than an error. Handle mine.isError like branches.isError.
- [LOW] web/components/appointments/AppointmentDetail.tsx:97 — Reschedule is disabled while services load with no visible reason; aria-busy on a disabled button is not announced consistently. Consider a short visible "Checking…" hint.
- [LOW] web/tests/components/booking/booking-flow.test.tsx (W8) — relies on a real 2.5 s hold plus a 6 s wait; fine now, flaky under load. Use fake timers or a shorter hold.

CHECKED, NO FINDING
- W5, W15, W7, W8, W10, W11 each match their rows. W5, W6, W7 and W15 tests fail without the fix.
- W10: next = pathname + search, URI-encoded, still consumed by sanitizeNextPath at sign-in (prefix rule, "//" refused); the query string cannot change the path prefix.
- W11: backslash escaped first; CR/LF/NEL/LS/PS become literal \n; other C0 (tab kept), DEL and C1 stripped; no raw line break can reach a property boundary. The test asserts no ATTENDEE: line.
- W8: the reason lives in BookingFlow memory only, is cleared on Back, cat or service change and on confirm. It is never persisted and is rendered as text.
- DST algorithm: re-measuring at the candidate instant resolves the cases where the guess and the answer straddle a transition (NY 03:00 on 11-01, 03:30 on 03-08, London all-day cases verified by hand).
- No superseded-architecture constructs; frontend-only change, no backend layers touched.

MERGE FORECAST (against master 234ed7f, base 7a742ec)
- No textual conflicts. Auto-merged: BookingFlow.tsx, AppointmentDetail.tsx. Type check on the merged tree is clean.

SCOPE CHECK
- files in allowed scope: 23/23 (all under web/app, web/components, web/lib, web/tests)
- files outside allowed scope: none (web/public/mockServiceWorker.js is an uncommitted local change, untouched)
