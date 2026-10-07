GATE: ACCEPTANCE
HANDOVER: HO-APT-002
COMMITS: 93fd85f 59d2660 34309a6 7b4db3c cd45fcf eba60c5 b2d0e4e a9df8a8 31d6e40 bb183b8 (code; branch ho/APT-002, range cffaa79..bb183b8); b3377df (owner decision); gate records e116c69 dfea47a eebb87b eeaa4f1 0e06838
DATE: 2026-10-05
STATUS: PASS

First pass. This gate read all of HO-APT-002, including §1 Amended items (1)-(14) and the 2026-10-04 owner decision on `CustomerSessionGuard.tsx`. It also read the four prior gate records, `docs/test-cases/TC-HO-APT-002.md`, and the tests and source cited for each §18 row.

Owner rule (2026-09-27): only BLOCKER and HIGH findings fail a gate. MEDIUM and LOW findings belong in `docs/backlog/APT.md`, which this gate has not edited.

MSW-only evidence is judged as in the HO-ADM-002, HO-FIL-002 and HO-NTF-002 ACCEPTANCE records:
- A criterion is MET on the MSW e2e plus unit evidence.
- The real-API half is carried as a condition. It is not counted as a pass.
- On the TC sheet, all 16 real-API cells of §16 read "not run — no API". This is declared in §1 Amended (14), and none of them is counted here.

PRIOR GATES (read from docs/gates/ and docs/test-cases/, not from commit messages)
- ARCHITECTURE: PASS on the second pass (dfea47a).
  - The first pass (e116c69) FAILED on 1 BLOCKER: `CustomerSessionGuard.tsx`, a §14 file.
  - The owner decision recorded in §1 Amended (b3377df) closed it.
  - 0 HIGH, 5 MEDIUM and 8 open LOW remain.
- SECURITY: PASS (eebb87b). 0 BLOCKER, 0 HIGH, 3 LOW.
- CODE_REVIEW: PASS (eeaa4f1). 0 BLOCKER, 0 HIGH, 6 MEDIUM, 9 LOW.
- TEST: PASS (0e06838), run at code head bb183b8.
  - The sheet has 76 rows: 76 PASS (several with notes), 0 FAIL, 0 BLOCKED, 0 NOT RUN.
  - The cat-vaccinations E9 failure also occurs on master cffaa79 (S7). It predates this branch.
- No product code has changed since bb183b8. Every commit after it touches only `docs/`.

VERIFICATION RUN BY THIS GATE (web/, worktree at 0e06838, code identical to bb183b8)
- `npx vitest run tests/components/{booking,appointments,front-desk,calendar} --maxWorkers=3`: 13 files, 74 tests, all passed.
- Every §15/§16 ID cited by §18 maps to a named test. The file:line references are given in the table below.
- Playwright was not re-run. The e2e evidence is from the TEST record:
  - chromium: 401/406 on the first full run, with the non-E9 failures green on rerun; appointments-a11y-responsive 73/73.
  - mobile-360: 13 passed, 1 skipped by design; a11y net 73/73.
- Checked against master by this gate:
  - `HostPipelineExtensions.cs:102-105`: the CORS policy has no `WithExposedHeaders`.
  - `web/README.md:55-62`: production is cross-origin by owner choice (two subdomains, `credentials: 'include'`).
  - HO-APT-001 §12.4: the contract itself gives `service` as a snapshot with no id.
  - master has `(portal)/portal/inbox` (NTF-002 merged).
  - master has no customer add-a-cat route. Cat registration exists only at `(staff)/staff/cats/new`.

CRITERIA (HO-APT-002 §18)

| # | Result | Evidence |
|---|---|---|
| A1 | MET (MSW; real-API half carried) | U-A3 (`format-appointment.test.ts:15`): London slots shown in London time from any viewer zone, labelled. U-A4 (`booking-flow.test.tsx:193`): a full day offers the waitlist and the next day with times. E-A1 (`appointments-booking.spec.ts:22`) PASS on both projects. Slots are rendered as the server returns them (`SlotStep`); ARCHITECTURE confirmed there is no client-side availability logic |
| A2 | MET as built and tested (MSW, same-origin); production half is an **integrator condition**, see C-1 | U-A6 (`hold-countdown.test.tsx:24`): server clock 90 s ahead, hold ends 100 s after server now. It reads 01:40 then 01:30; the browser-clock reading would be 03:10, so the test fails if the offset is ignored. `server-clock.test.ts:14`: median of the last five samples. U-A7 (`hold-countdown.test.tsx:35`): samples every second and asserts the announcements are exactly 5:00, 1:00 and 0:00. Source: `client.ts:278` records every response's `Date`, and `lib/hooks/useServerNow.ts` adds the median offset. The deployed topology judgement is under C-1 |
| A3 | MET (MSW) | U-A8 (`booking-flow.test.tsx:130`): at 0 the flow returns to the slots with the expiry message, and Confirm is gone. E-A3 (`appointments-booking.spec.ts:109`, page.clock +75 s): the expiry text is shown, `Confirm booking` count is 0, and the 11:00 slot is selectable again. PASS on both projects (TC V1). The server's `SlotHoldExpired` on confirm takes the same path (`ConfirmStep.tsx:30`) |
| A4 | MET (MSW race; genuine race carried) | U-A5 (`booking-flow.test.tsx:77`): message, slot refetch, focus on the list. E-A2 (`appointments-booking.spec.ts:69`) in two browser contexts: the exact text "Someone has just taken that time. Please choose another.", the list focused, the taken slot gone, and the customer then reaches a new hold. PASS on both projects (TC V2). The race is MSW's deterministic one; Amended (14) declares this |
| A5 | MET (MSW; inbox half carried) | U-B1 (`my-appointments.test.tsx:60,86`, `reschedule-flow.test.tsx:80`): Reschedule and Cancel when `canChange`; otherwise the contact card with a `tel:` link. U-B2 (`reschedule-flow.test.tsx:17`): a 409 gives the message, and the current time is unchanged. E-B1 (`:133`): moved before the cut-off, contact card after. E-B2 (`:167`): status Cancelled. All PASS. Two defects do not unmeet A5, see M-2 and M-3 |
| A6 | MET (MSW) | U-D1 (`queue-and-today.test.tsx:77`): server order, priority as text. U-D2 (`front-desk-view.test.tsx:23`): a poll keeps the StartDialog values. U-D3 (`:103`): keyboard only, focus returns to search. E-D1 (`appointments-frontdesk.spec.ts:13`): measured `< 60_000` ms, actual 8.6 s chromium and 7.4 s mobile-360. E-D2 (`:48`): the emergency goes first and later estimates rise on the next poll. All PASS |
| A7 | MET (MSW; real-API half carried) | Per vet and per room: calendar grid and toolbar (U-E5/U-E6 `calendar.test.tsx:154,89`). Server re-validation with a specific reason: U-E1 (`:210`) snaps back, with the reason in a toast and a live region; E-E1 drag-to-busy (`appointments-calendar.spec.ts:94`). Keyboard alternative: U-E4 (`:292`) sends the same request as a drop; E-E1 Move… (`:62`) is refused, then moved. U-E3 (`:173`): a poll during a drag is applied after the drop. All PASS. The drag tests failed once in a 29-minute full run and passed in 8 further executions (TC V3, MEDIUM, already on TEST's list). The stale-version dead end does not unmeet A7, see M-1 |
| A8 | MET (MSW) | U-C1 (`staff-appointments.test.tsx:68`): the no-show count as text at staff booking. E-C1 (`appointments-staff.spec.ts:15`): badge seen, then booked. On the customer page, source: `customers/[customerId]/page.tsx` renders `AppointmentSummaryPanel`, which shows the no-show count, and the E-X1 route list covers that page (`appointments-a11y-responsive.spec.ts:36`) |
| A9 | MET (MSW part; clinic pass carried) | U-B3 (`my-appointments.test.tsx:132,142,151`): the live offer shows the countdown and Confirm; an ended offer shows the offer-ended text. E-B3 MSW part (`appointments-booking.spec.ts:182`): full day, join the waitlist, claim the offered slot, Booked. The leg where a clinic pass frees a slot needs the backend (Amended (14)). The "claim from the inbox link" leg is now satisfiable on master and is carried with the real-API run |
| A10 | MET (MSW) | U-D4 (`queue-and-today.test.tsx:94,110,181`, `staff-appointments.test.tsx:154`): only the server's `allowedTransitions` are rendered, and nothing is offered while they load. E-D3 (`appointments-frontdesk.spec.ts:86`): check in, *My queue*, start, complete. PASS on both projects (TC V4) |
| A11 | MET (MSW; leave-approval trigger carried) | E-E2 (`appointments-staff.spec.ts:46`) shows four things. The flagged list shows "is on leave that day". The detail has the banner and only the allowed actions. Reschedule from the list opens Move…, which states the old and new times. The list then empties. PASS on both projects. It starts from an already flagged mock appointment; the HO-ADM-002 leave approval that flags it is a backend effect (Amended (14)) |
| A12 | MET | U-A10 (`booking-flow.test.tsx:166`, `leave-and-forms.test.tsx:145`) PASS. Source: the only payment wording in the flow is "Pay at the clinic" (`ServiceStep.tsx:72`, `ConfirmStep.tsx:113`, `BookingDone.tsx:77`). There is no payment control |
| A13 | MET for this slice (MSW); AC-1's add-a-cat and inbox legs carried, see C-2 | E-A1 (`appointments-booking.spec.ts:22`): a brand-new customer registers (with CAPTCHA), signs in, follows a vaccination reminder link, holds, confirms and sees the booking under Upcoming. `expect(Date.now() - started).toBeLessThan(3 * 60_000)` at `:25,59`, measured from before registration. 21.4 s on mobile-360. PASS on both projects |
| A14 | MET (MSW) | E-X1 and E-X2 (`appointments-a11y-responsive.spec.ts`): every §8.1 route (`:23-36`, including the staff customer page), at 360/768/1024/1440 (`:43-46`). Also the countdown (`:72`), the Move… dialog open (`:83`) and a refused move (`:97`). The serious/critical filter is at `:56-59`. chromium 73/73; mobile-360 72/73 on a cold server (a fixture 404 before MSW was ready), 73/73 after a warm rerun (TEST record) |

CARRIED CONDITIONS (not gate failures; each must be closed by its owner)
- **C-1 Integrator. A2 in the deployed topology.**
  - Production is cross-origin by owner choice (`web/README.md:55-62`), and master's CORS policy (`HostPipelineExtensions.cs:102-105`) has no `.WithExposedHeaders("Date")`.
  - So a deployed browser cannot read `Date`, the offset stays 0, and the countdown runs on the device clock.
  - **Why this is a condition, not a FAIL.** The frontend is complete and correct, and the evidence §18 names for A2 (U-A6, U-A7) asserts the behaviour.
    - The missing piece is a one-line change in a `src/**` file that §4 and §14 put outside this handover.
    - The builder reported it (Amended (10)) instead of shimming it, as §20.4 requires.
    - The fallback is safe: the server owns expiry. A confirm after a real expiry gets `SlotHoldExpired`, which goes down the A3 path, so nobody ends up booked on a lost hold.
    - What fails today is only the *display* on a phone whose clock is wrong.
  - **What closes it.** Add `.WithExposedHeaders("Date")` to the `HostAllowedOrigins` policy. Then run E-A3 against the real API with a skewed client clock.
  - **Until then, A2 does not hold in production.**
  - **Not tracked anywhere yet.** `docs/backlog/APT.md` has no row for it, and there is no integrator backlog file. CODE_REVIEW made the same observation. Record it at merge.
- **C-2 HO-CAT-002 / owner. The AC-1 "adds a cat" leg.**
  - Master has no customer-facing cat registration; it exists only at `(staff)/staff/cats/new`.
  - `CatStep.tsx:24-31` correctly says "Please ask the clinic to register your cat". §20.1 forbade building a parallel form, and none was built.
  - So E-A1 books with a mock cat. A13's budget is met for every step this slice owns, but a genuinely new customer cannot yet complete AC-1 online.
  - **No backlog row or handover tracks a portal add-a-cat screen.** The owner should assign it.
  - This is a cross-handover gap and is not counted against HO-APT-002.
- **C-3 Integrator. Real-API re-runs, now partly unblocked.**
  - Every E-case has to be re-run against the real API (§16, §17 step 7; Amended (14)).
  - Now that NTF-002 is merged to master, the inbox assertions are satisfiable: E-A1's confirmation in the inbox, E-B2's cancellation message, and E-B3's claim from the inbox link. Add them in the real-API run after this branch merges.
  - Also still owed: a genuine two-customer race (A4), the clinic pass (A9) and the leave approval that flags an appointment (A11).

FINDINGS
- No BLOCKER or HIGH findings.
- [MEDIUM] M-1 web/app/(staff)/staff/appointments/CalendarView.tsx:79,96,108 with web/components/calendar/MoveDialog.tsx — the stale calendar version (CODE_REVIEW MEDIUM, TEST V5 CONFIRMED).
  - **What happens.** After a `StaleCalendarVersion` 409 from the **Move… dialog**, the calendar is refetched (`useMoveAppointment.onSettled` invalidates, `useStaffAppointments.ts:160`). But `version` was captured when the dialog opened, so every resubmit sends the stale `expectedVersion` until the dialog is closed and reopened.
  - **Judged against §18, it unmeets no criterion.**
    - A7 asks for server re-validation with specific reasons and a keyboard alternative. The 409 is still re-validated, gives the specific message, and triggers the refetch §11.6 requires.
    - The drag path recovers by itself: each drop reads `appointment.version` from the refetched rows (`CalendarView.tsx:96`).
    - Reopening the dialog recovers the keyboard path.
    - A4 ("explained and recovers") is the customer slot race (CLN-03 AC3), not the calendar. It is met separately.
  - **Why it is still a real defect.** A keyboard-only user faces a dead end until they think to close the dialog.
  - **Related.** U-E2 (`calendar.test.tsx:259-290`) asserts only the message, so it passes over this defect (TEST MEDIUM). Fix the code and strengthen U-E2 together.
- [MEDIUM] M-2 web/app/(portal)/portal/appointments/[appointmentId]/MyAppointmentView.tsx:57 — `detail.startsOn.slice(0, 10)` is the UTC date, not the branch-local date that §8.4 requires (ARCHITECTURE and CODE_REVIEW MEDIUM).
  - **Effect.** The reschedule picker opens on the wrong day when the branch-local date differs from the UTC date: 00:00-06:00 in Asia/Dhaka, or evenings west of UTC. The clamp to `todayIso` hides it.
  - **Why A5 is not unmet.** Only the starting date is wrong. The picker lets the customer change the date (`:139-141`), the slots are labelled with their date, and the server validates the reschedule.
  - It is dormant for the current Dhaka opening hours, and live for the London mock branch.
  - **Fix.** Use `localDateOf(detail.startsOn, zone)`.
- [MEDIUM] M-3 web/lib/api/booking.ts:164-175 (`findServiceIdForSnapshot`) and web/components/calendar/MoveDialog.tsx — the serviceId gap.
  - **Why it is not a §20 work-around.**
    - HO-APT-001 §12.4 itself gives the service only as a snapshot.
    - The builder reported the gap (Amended (2)) and invented no mock field.
    - Reschedule bridges it with an exact name + category + duration match, and falls back to "call the clinic" on 0 or 2+ matches.
    - Move takes a typed time that the server judges, not §11.6's staff slot list. No availability is computed client-side.
  - **Effect on A5 and A7.** A5 holds whenever the snapshot matches; otherwise the customer gets the contact card that A5 already allows. A7 does not require the slot list.
  - **What closes it.** The APT backlog item: add `serviceId` to the Summary, Detail, Calendar/Day and Queue view models, then delete the heuristic and give Move… the slot list.
- Carried, not re-raised:
  - ARCHITECTURE: 5 MEDIUM, 8 LOW;
  - SECURITY: 3 LOW;
  - CODE_REVIEW: 6 MEDIUM, 9 LOW;
  - TEST: 3 MEDIUM, 5 LOW and its "gaps left deliberately".
  - None of them is BLOCKER or HIGH.

OUT-OF-SCOPE CHECK (§4, §14, §20)
- §4: none found.
  - No backend change: `git diff --stat cffaa79 HEAD -- src tests .ai .claude` is empty.
  - No payment or deposit step (A12).
  - No vitals, consultation notes or consultation screen: no such route or code in the diff. Start only changes status.
  - No portal dashboard and no offline day list.
  - No new dependency: `web/package.json`, `package-lock.json`, `tsconfig.json`, `next.config.ts` and `eslint.config.mjs` are unchanged.
- §20: no condition was hit and worked around without being reported.
  - The serviceId gap (§20.5) is reported in Amended (2), with no invented field.
  - The wire differences (§20.4) are reported in Amended (1) and (11), and follow HO-APT-001's code without a shim.
  - The Date-header CORS gap is reported in Amended (10).
  - The missing add-a-cat screen and the unmerged NTF-002 (§20.1) are reported in Amended (12), and no parallel form or inbox was built.

UNDECLARED DEVIATIONS
- [LOW] web/nav/areas/appointments.ts:21 — a second portal nav entry, **"Book a visit"** (`/book`).
  - §13 allows "one nav entry only" for the portal. Amended (13) records moving the entries into the registry, but not this extra item.
  - ARCHITECTURE raised it as a LOW. It links to an in-scope route and adds no functionality, so it is not §4 scope expansion.
  - Record it in §1 Amended, or drop it.
- No other undeclared deviation was found.
  - The `ApiConflictError` body is declared in Amended (1).
  - The typed Move time is declared in Amended (2).
  - The per-row detail reads are declared in Amended (3) and (5).
  - The `CustomerSessionGuard` edit is owner-approved (b3377df).
  - The customer-page change is the `AppointmentSummaryPanel` only, as §13 allows (+16/−1, read).
  - The cat-page link already existed (`cats/[catId]/page.tsx:62`, untouched), as Amended (13) says.

SCOPE CHECK
- files in allowed scope: 112/112. These are the 111 files in `git diff --name-only cffaa79 0e06838` plus this record. The 111 are 105 web files, `docs/handover/HO-APT-002.md`, `docs/test-cases/TC-HO-APT-002.md` and the four prior gate records.
  - 101 of the 105 web files are inside literal §13 globs.
  - `web/components/portal/CustomerSessionGuard.tsx` is in §13 by the owner decision of 2026-10-04 (+4/−1 at `:43-47`, read).
  - Three are declared in Amended (13): `web/nav/areas/appointments.ts`, `web/mocks/handlers/areas/appointments.ts` and `web/tests/e2e/appointments-helpers.ts`.
  - The handover diff is the Amended row and the two §13 lines the decision calls for (+3/−2).
- files outside allowed scope: none
