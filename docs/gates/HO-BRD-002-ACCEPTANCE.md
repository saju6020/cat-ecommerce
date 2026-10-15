GATE: ACCEPTANCE
HANDOVER: HO-BRD-002
COMMITS: 5a187b6b..7dfcbd10 on ho/BRD-002 (130 files, 123 under web/). Build ef4bba16 0e24d681 baf7f254 111c8cc2 bb1d3862 baf68c86 df3720a6 70b7936b 669744e6 c65bf03a 05233fd7 5d579280 8b4c8073 75cb8e85; fix rounds 2c23a1cd b1958d45 7dfcbd10; gate records 32ad0001 86bec810 b1671913 7a366775 36f4c248
DATE: 2026-10-15
STATUS: PASS (re-check of 77ee7555, at the end of this file; the first run at a41be7eb was FAIL)

One §18 criterion has no evidence: **A29 (NFR-PRF-3, the dashboard and task board within 1.5 s at p95 against the real API)**. E-X3 was never measured, on MSW or on the real API, and no owner decision defers it. My rules forbid a PASS while any criterion lacks evidence. The HO-XCT-002 ACCEPTANCE precedent grades an unevidenced §18 row HIGH, so this is FAIL, not a LOW. The TEST gate filed it as LOW row 83; this record raises it to HIGH (row 85).

The other 28 criteria are evidenced. Ten of them rest on MSW plus unit evidence, with the real-API half carried as a condition. That is the rule used in the HO-ADM-002, HO-APT-002, HO-FIL-002 and HO-NTF-002 ACCEPTANCE records.

The one HIGH can be closed without a code change. Either:
- record a measured E-X3, or
- get an explicit owner deferral of A29.

Either way the follow-up needs only an ACCEPTANCE re-check.

Owner rule (2026-09-27): only BLOCKER and HIGH findings fail a gate. MEDIUM and LOW rows go to `docs/backlog/BRD.md`. This gate did not edit that file.

## PRIOR GATES (read from docs/gates/ and docs/test-cases/)

- **ARCHITECTURE: PASS** on the combined re-check (7a366775, `HO-BRD-002-ARCHITECTURE.md:195-201`). That re-check covers the SECURITY and CODE_REVIEW findings. The first pass (b1671913) was FAIL on H1, the absent rule; the re-check closed it. The small fixes in b1958d45 are pinned by `gate-fixes.test.tsx` and were re-observed by TEST.
- **SECURITY: PASS** (32ad0001). 3 LOW, all fixed (rows 67-69).
- **CODE_REVIEW: PASS** (86bec810). 6 MEDIUM and 6 LOW (rows 54-66). Their status after the fix round is in that file.
- **TEST: FAIL at 36f4c248**, on one HIGH, row 76: the owner's diary View photo link resolved against the web origin and returned 404.
  - The fix is in 7dfcbd10. Under the gate-scope rule a tiny fix needs only a test confirmation, and this gate ran it (below).
  - With that confirmation, I treat TEST as closed for row 76.
  - The TEST record still reads `STATUS: FAIL`. Its row-status cells were edited by the fix commit, not by the gate (row 93).
  - The TC sheet: 51 T- rows. 45 PASS, 4 FAIL (T-F1 inventory flake outside Boarding; T-R18, which is row 76; T-G13 and T-G14, LOW rows 74 and 75), 2 BLOCKED (T-F3 not in the tree; T-G6, now covered by `gate-fixes.test.tsx` 'shows when a gate override was made'), 0 NOT RUN. None of the FAIL or BLOCKED rows is counted as a pass below.

## VERIFICATION RUN BY THIS GATE (worktree D:/my-repo/cat_ecommerce-wt/BRD-002 at 7dfcbd10)

`web/node_modules` was a temporary junction to the main checkout's copy. It was removed afterwards, and `git status` was clean.

- `npx vitest run tests/components/boarding --maxWorkers=2`: **7 files, 88 of 88 passed** (10.4 s, run at 03:41 local). This matches the builder's 88/88. It includes the four new tests in `gate-fixes.test.tsx:304-396`:
  - 'anchors a diary photo link on the API origin' (row 76);
  - 'does not say "No deposit needed" for an Enquiry' (row 77);
  - 'shows when a gate override was made' (row 81);
  - 'refuses an incident time in the future and sends nothing' (row 80).
- `npx tsc --noEmit`: exit 0.
- `npx eslint` on the six files changed by 7dfcbd10, `--max-warnings 0`: exit 0.
- Playwright was not re-run. The e2e evidence below comes from the TEST record:
  - T-S5: chromium MSW 69/69.
  - T-S6: mobile-360 68/69, then 1/1 alone.
  - T-R1 to T-R20: real API on `ho/BRD-001` 2216ff79, whose `src/` equals cede66fe.

## CRITERIA (HO-BRD-002 §18)

"MSW" means the MSW Playwright run (T-S5/T-S6, 69/69 chromium). "Real" names a T-R row of the TC sheet. File:line references are test declarations on 7dfcbd10.

| # | Result | Evidence |
|---|---|---|
| A1 | MET (MSW) | U-C5/U-C6, `boarding-staff.spec.ts:331`: the unit table shows the rate read-only, and maintenance over a stay is refused with its dates. PASS in T-S5 |
| A2 | MET | U-A1, `booking.test.tsx:93`: one card per type with capacity, free count and total. E-A2 on MSW, `boarding-booking.spec.ts:30`. Real T-R2: booking, quote and reserve, PASS. The 20-night discount was not run on the real API (carried) |
| A3 | MET (MSW; inbox notice carried) | U-A3, `booking.test.tsx:121`: nearest dates, each a one-tap search, plus the waitlist. E-A4 first half, `boarding-booking.spec.ts:94`. The second half of E-A4 (worker, then the inbox notice) is a backend effect and was not run (carried) |
| A4 | MET (MSW) | U-A2, `booking.test.tsx:114`: basis, units, the discount as its own line, the total as sent. U-C7, `boarding-staff.spec.ts:347`. E-A2, `boarding-booking.spec.ts:30` (20 nights, the review total matches) |
| A5 | MET | U-A5, `booking.test.tsx:192,225`. E-A1 on MSW, `boarding-booking.spec.ts:111`. Real T-R1: the gate names Rabies and the link is `/book?catId=` |
| A6 | MET (MSW; real resume carried) | U-A6, `boarding-portal.spec.ts:37` and `boarding-booking.spec.ts:135`. E-A1 on MSW resumes after the vaccination. Real T-R1 shows the booking kept; a resume after a real vaccination was not run |
| A7 | MET (source plus a shared-component test) | `StayWorkspace.tsx:315-335` and `StaffStayBookingForm.tsx:47-73` open `ReasonDialog` with "Reason (mandatory)" and "This will be recorded against you". `ReasonDialog.tsx:64-68` refuses a blank trimmed reason before calling `onConfirm`. That refusal is asserted through the same component by E-E2 (`boarding-staff.spec.ts:254`, 'Enter the reason for moving the cat.'). There is no override-specific test, and `canOverrideGate` is absent, so the control is offered to any staff member with a blocked cat and the server decides (row 90) |
| A8 | MET | U-A7, `booking.test.tsx:250`: field errors for a missing limit, seven feedings and a medicine without a time, with the typed values kept. `:289` saves a complete profile. Real T-R10/T-R11 (staff vet-limit reason) |
| A9 | MET | U-A9, `booking.test.tsx:346`: not yet confirmed, no payment control. U-A10, `:328`: 90 s clock skew; announcements at 24 h, 1 h and expiry. U-B1: `care-and-stay.test.tsx:257` and `boarding-portal.spec.ts:50,63`. Real T-R2: Reserved, with the deposit shown |
| A10 | MET (MSW; real lapse carried) | E-B1/E-B3, `boarding-staff.spec.ts:163`: DepositNotYetReceived, then the second-approval wait, then Confirmed. E-B2, `boarding-portal.spec.ts:131`: 70 s lapse. Real T-R3: Billing's earmarked deposit confirmed the stay. Billing auto-confirms, so the Confirm button path is MSW only. The real E-B2 lapse was not run (carried) |
| A11 | MET | U-X4, `care-and-stay.test.tsx:396`: a source scan of every boarding root finds no cvv, card number, pay now, gateway, token, FormData or file input. U-A9, `booking.test.tsx:346`. The two `FileUploader` uses (`CheckInWizard.tsx:284`, `MediaRecordDialog.tsx:91`) are the shared HO-FIL-002 component, owner-authorised, and not payment |
| A12 | MET (thin; row 88) | Over capacity: U-A4, `booking.test.tsx:170` and `boarding-booking.spec.ts:83` (two cats checked, "holds up to 1 cat", Continue disabled). `mock-api.test.ts:128`: the server message carries the capacity. Several cats: `CatStep.tsx:39-97` (multi-select up to capacity) and `CareProfileTabs.tsx:122` (one tab per cat). `BookingFlow.tsx:145,224` sends every `catId`. The MSW seed's two-cat in-house stay renders (`boarding-portal.spec.ts:28`, "Noodle and Pepper"). No test reserves two cats into a Family unit (E-A3 first half), on MSW or the real API |
| A13 | MET | E-C1, `boarding-staff.spec.ts:184`: an incomplete step is refused, then the typed-name path checks the stay in once. U-E1, `mock-api.test.ts:203`: missing items are named against cat and field. U-E2, `care-and-stay.test.tsx:220,241,246` and `boarding-staff.spec.ts:206`. Real T-R4: check-in with an uploaded belongings photo |
| A14 | MET | U-D1/U-D7, `care-and-stay.test.tsx:51`: one pointer event and then keyboard, no dialog. E-C2, `boarding-staff.spec.ts:14`: one tap on a tablet viewport, identity shown, 48 px box asserted. Real T-R5: one-tap Done on tasks the worker generated |
| A15 | MET (MSW; real dashboard card carried) | U-D3, `care-and-stay.test.tsx:70`. E-C3, `boarding-staff.spec.ts:91`: overdue on the board and in the dashboard card. On the real wire there is no `minutesLate`, so the chip reads "Overdue". It is still highlighted as text, which A15 asks for |
| A16 | MET | U-D5, `care-and-stay.test.tsx:142,175`: the server flag is rendered. `wire-adapters.test.ts:251`: dashboard `notEating[]` ids become per-cat flags. E-C4, `boarding-staff.spec.ts:91`. Real T-R5: an observation is recorded |
| A17 | MET (storage caveat superseded by owner decision; see Ruling 1) | U-B2, `boarding-portal.spec.ts:86`: the diary is grouped by day. Real T-R7: the owner diary shows the observation. Real T-R18: a staff photo upload appears in the owner diary with View photo. `gate-fixes.test.tsx:305`: the link is now anchored on the API origin |
| A18 | MET (MSW; real escalation and inbox carried) | U-B3, `boarding-portal.spec.ts:96`: I've seen this, then Acknowledged. §11.9, `boarding-staff.spec.ts:149`: the Escalated chip, Call owner, and Resolve needs a resolution. Incident form: `gate-fixes.test.tsx:368`. Real T-R6: the incident is recorded from the board and seen in the queue and by the owner. The real escalation timer and the inbox were not run (carried). On the real wire, Call owner has no name or phone (row 92) |
| A19 | MET (owner half tested; staff half by source and real API calls; row 87) | U-B4, `boarding-portal.spec.ts:104`: amount, limit and used, then Approve with a confirm, then "you approved this". `wire-final.test.ts:31,54`: referrals and approvals mapped from JSON captured on the real API. `ReferralPanel.tsx:23-29,43-46,107-158,180-251` implements refer, the headroom text with "the owner has not authorised treatment", request approval, Cat is back and the staff answer. The real API accepted refer, request, owner `{approve}`, staff answer and `referrals/{id}/return` (all 2xx; Amended final alignment; TC E-E1). None of the staff half is driven through the UI in any run |
| A20 | MET (MSW; real carried) | U-E4/U-E5 via E-E2, `boarding-staff.spec.ts:243`: options listed, Relocate and extend, reason required, confirm names both units |
| A21 | MET | U-E8 and E-E3, `boarding-staff.spec.ts:285`. Real T-R8: '1 of 3 nights are charged', Void/Record lines, then Checked out. Real T-R15/T-R16: a late check-out is previewed and not refused. `gate-fixes.test.tsx:176-240` and `test-gate.test.tsx:200`: early or late decided by the server |
| A22 | MET | U-B5, `boarding-portal.spec.ts:116`: weight change, belongings, signature, charges, no payment control. Real T-R14: summary after the final invoice. Printable: `StaySummaryView.tsx:149-166` (Print button, `print:hidden` chrome) |
| A23 | MET (MSW plus source; row 89) | U-E6/E-E4, `boarding-staff.spec.ts:261`: the confirm names Isolation 2, then "Suspected infectious". Entry log: `IsolationPanel.tsx:109-218` (Record entry, Record exit, staff name and time). U-E7 and `NoIsolationUnitAvailable` have no test |
| A24 | MET (MSW) | U-C1, `boarding-staff.spec.ts:91`: every section with counts. `mock-api.test.ts:282`. `wire-final.test.ts:67`: the lean dashboard rows from the real wire. Real LATE-RUN-2 and AXE-REAL rendered the dashboard on live data |
| A25 | MET (MSW) | U-C2, `boarding-staff.spec.ts:123`: Record call keeps its typed text through a refresh. U-D4, `care-and-stay.test.tsx:175` and `boarding-staff.spec.ts:62`. E-F2 on real data was not run (carried) |
| A26 | MET (HO-BIL-002 dependency recorded) | E-F1, `boarding-staff.spec.ts:278`: the cycle invoice on Charges (MSW). Real T-R14: the final invoice by the worker, 'Your invoice'. Real LATE-RUN-3: a 45-night stay's cycle invoice, 'Your invoice'. The invoice number, kind and total are not on the wire; links go to HO-BIL-002's views. The final invoice's coverage of the remaining nights was not asserted (carried) |
| A27 | MET | E-G1, `boarding-staff.spec.ts:367`: charge shown, no unit occupied (MSW). Real T-R9 |
| A28 | MET | U-D7 (`care-and-stay.test.tsx:51`), U-E2 (`:246`, typed path), U-C3/U-C4 (`care-and-stay.test.tsx:351,361`; `boarding-staff.spec.ts:309,321`). E-X1: `boarding-a11y-responsive.spec.ts:85-104`, 24 routes plus the signature and the task board. Real AXE-REAL: 16 pages, zero serious or critical. E-X2: `:120` at 360/768/1024/1440, but on the five heaviest routes only. The whole suite also ran on mobile-360 (T-S6) |
| **A29** | **NOT MET** | E-X3 has never been measured. TC E-X3: "not measured"; T-X row 83: "still not measured". No §1 Amended row or owner decision defers it. NFR-PRF-3 asks for ≤ 1.5 s at p95 over 20 loads against the seeded real API. The TEST gate had such an API (T-R*) and did not take the measurement. Row 85 |

## RULINGS ASKED FOR

**1. The diary photo, end to end ("Yes, diary photos too").**

The proof is enough to close HIGH row 76. It is not yet an observed end-to-end click, so that is carried as a condition, not a finding.

The chain is complete from observed facts:
- (a) Real T-R18 observed the staff upload through `FileUploader` (purpose `BoardingMedia`), the media record, and the owner diary's View photo link.
- (b) T-R18b observed that the exact href path, `/api/files/{id}/content?...`, returns `200 image/png` on the API origin.
- (c) 7dfcbd10 changes only the adapter (`my-boarding.ts:373`) to `resolveFileUrl`. That is the helper cat photos (`cats.ts:204`) and the clinical file reads use on master. `gate-fixes.test.tsx:305` asserts the API-relative path becomes `<API origin>/api/files/f1/content?...` and that a `//evil` URL becomes null.
- (d) `StayDiary.tsx:34-36` renders `fileUrl` as the href. That code is unchanged and was observed on the real API.

`T-R18b` is already written in `boarding-real-api.spec.ts:768`. It must pass at the next real-API run (row 91). That run is needed anyway, for A29.

**2. Open real-API gaps.**

None of these leaves a criterion without evidence. Each criterion is evidenced on MSW plus unit tests, and the real-API half is carried:
- E-A3 → A12 (thin, row 88)
- the E-A4 inbox notice → A3
- the E-B2 lapse → A10
- the E-D1 escalation and inbox → A18
- E-E1, E-E2 and E-E4 through the UI → A19, A20, A23
- E-F2 on real data → A24, A25

**E-X3 p95 is the exception.** It has no evidence on any run. It is the HIGH (A29).

Carried conditions for the integrator's real-API run after HO-BRD-001 merges:
- E-A2 at 20 nights;
- E-A3;
- the E-A4 notice;
- E-B2;
- E-D1 escalation and inbox;
- E-E1, E-E2 and E-E4 through the UI;
- the E-F1 final-invoice coverage;
- E-F2;
- T-R18b.

**3. Fields the backend does not send, shown as absent.**

None breaks a §18 criterion:
- **Stay invoices** (A26): the invoice ids come from the summary, and the invoice is visible on the real API.
- **Pre-cancel deposit outcome**: this is U-B6 and a UI-3 rule. No §18 row cites U-B6. It stays an owner/backend question.
- **canOverrideGate** (A7): the reason is still mandatory. The cost is that non-admins see a control the server refuses (row 90).
- **Enquiry deposit and payBy** (A9): A9 is about the reserved page, and there they are shown (T-R2). Only the UI-3 reserve sentence cannot state them.
- **Availability windows**: no §18 row covers them.
- **minutesLate** (A15): "Overdue" is still highlighted as text.
- **Task not-eating** (A16): the flag comes from the server's dashboard `notEating[]`.
- **Dashboard customer name and phone** (A24): the figures and links are present. The practical gap is Call owner with no number to call. It is MEDIUM and BE-owned (row 92), not a criterion failure. A18 asks that Call owner is *raised*, and it is.

**4. Row 77, partly fixed.** MEDIUM. Not blocking.

The owner side is fixed: the portal stay page (`DepositSummary`), and My stays, which lists an Enquiry through `EnquiryRow` with no deposit badge.

The staff side is not:
- `StayWorkspace.tsx:104` is the header badge, and row 77 names "StayWorkspace header" explicitly;
- `DepositPanel.tsx:63` on the Overview tab;
- `StayList.tsx:90`.

All three still read "No deposit needed" for an Enquiry. The TEST record calls row 77 "Fixed in the FE", which overstates it (row 86). No criterion depends on it: A9 concerns reserved stays. BE row 84 is the real fix.

**5. Deferred parts.**

Neither touches a §18 criterion.
- **The offline board's boarding instructions.** §4 places the offline task list with HO-XCT-002, and §18's deferred list names it. What is missing is BRD-001's `boardingTasks` snapshot source (row 52, now BRD-001-FU1).
- **The portal stays tile.** §4 places the portal home with HO-POR-002, and it was owner-skipped on 2026-10-10 (row 53).

**6. The merge checklist.** Integrator conditions, not findings against this handover.

- **ADR-FE-099..108** are free on master: master's series runs to ADR-FE-172 with no 099-108. They are appended in `web/docs/decisions.md:1529-1613`, with the 104/105 corrections at `:1614`. master's `decisions.md` grew by 256 lines since 5a187b6b, so expect an append-append conflict. Keep both sections.
- **The sign-in edit** (`app/(auth)/sign-in/page.tsx`, one token, `/boarding`) is owner-authorised ("Yes, allow it"). master has not touched the file since the base, so it should merge cleanly. The open-redirect guard (`sanitizeNextPath`) is unchanged, and SECURITY passed it.
- **`.gitignore`** (`web/seed*.json`) is unchanged on master since the base, so it should merge cleanly. It is a SECURITY LOW fix and outside §13 (see SCOPE).
- **Backlog rows.** `docs/backlog/BRD.md` exists only on `ho/BRD-001`, with rows 1-60. BRD-002's rows 45-84, and 85+ here, collide at 45-60. Renumber BRD-002's rows on merge (e.g. offset to 61+) and keep a mapping line, because the BRD-002 gate files and §1 Amended cite the original numbers (row 94).
- **Merge order.** HO-BRD-001 merges first. It owns the endpoints and `BRD.md`, and its ACCEPTANCE is currently FAIL (45bac6da).
- **Mixed trailers.** 16 commits carry the Opus 5.5 trailer and 6 carry Sonnet 5.5 (2c23a1cd 75cb8e85 8b4c8073 5d579280 05233fd7 c65bf03a). Every commit has a trailer. Do not rewrite published history; no action.

## FINDINGS

These rows are proposed for `docs/backlog/BRD.md` and continue BRD-002's numbering from 85; see row 94 on renumbering.

| # | Sev | Where | Item |
|---|---|---|---|
| 85 | **HIGH** | E-X3 / §18 A29; TC sheet | NFR-PRF-3 has never been measured, so A29 has no evidence. Supersedes LOW row 83. Close it **either** way: (a) measure it; (b) the owner explicitly defers A29, e.g. to the integrator's post-merge real-API run once the committed seeder exists (row 82). For (a): run `next build && next start` with mocks off against a seeded HO-BRD-001 API (head of `ho/BRD-001`), load `/staff/boarding` and `/staff/boarding/tasks` 20 times each, and record the first meaningful content (first section card or task group visible) and the p95 on the TC sheet. Then an ACCEPTANCE re-check |
| 86 | MEDIUM | `StayWorkspace.tsx:104`, `DepositPanel.tsx:63`, `StayList.tsx:90`; `docs/gates/HO-BRD-002-TEST.md` row 77 | Row 77 is only half fixed. Staff screens still show "No deposit needed" for an Enquiry; only `DepositSummary` (owner) was changed. Apply the same Enquiry wording to the staff badge sites, or hide the badge for an Enquiry. Correct row 77's status to "Partly fixed" |
| 87 | MEDIUM | `ReferralPanel.tsx`; tests | A19's staff half has no UI test on either run: refer, headroom, request approval, Cat is back and the staff answer. Add an MSW e2e for E-E1 (refer, request above the limit, the owner approves in the portal, the staff panel shows the decision) and drive it once on the real API |
| 88 | LOW | `boarding-booking.spec.ts` | E-A3's first half is untested. Add an MSW case reserving two cats into a Family unit with two care-profile tabs, then four cats refused |
| 89 | LOW | `IsolationPanel.tsx` | U-E7 (Record entry and Record exit with staff name and time) and U-E6's `NoIsolationUnitAvailable` refusal have no test |
| 90 | LOW (FE + BRD-001) | `StayWorkspace.tsx:180-186`, `StaffStayBookingForm.tsx:47` | Override vaccination is offered to any staff member with a blocked cat, because `canOverrideGate` is not on the wire. A non-admin learns it is not allowed only from the server's refusal. Ask BRD-001 for the flag (ADR-FE-107). Add an override-specific test of the mandatory reason |
| 91 | LOW (carried) | `boarding-real-api.spec.ts:768` | Re-run T-R18b at the next real-API run: the owner's View photo opens `200 image/*` from the API origin. The fix 7dfcbd10 is proven by the wire test and the observed API response, not by an observed click |
| 92 | MEDIUM (BRD-001) | dashboard `callOwner[]`, incident queue | The real wire carries no customer name or phone, so Call owner reads "call the owner" with no `tel:` link. That defeats §11.9's purpose for the person who must call. Add `customerName` and `customerPhone` to `callOwner[]` and the incident queue rows. Owner question below |
| 93 | LOW (process) | `docs/gates/HO-BRD-002-TEST.md` (edited by 7dfcbd10) | The fix commit rewrote the TEST record's status cells (rows 76, 77, 80, 81) and added row 84 outside the findings table, while the record's `STATUS` still reads FAIL. Gate records should be changed by their gate. Builders should report fixed rows in the handover §1 or the commit message |
| 94 | LOW (integrator) | `docs/backlog/BRD.md` | BRD-002 rows 45-84 (and 85-95 here) collide with BRD-001's rows 45-60. Renumber on merge and record the old-to-new mapping |
| 95 | LOW | `boarding-a11y-responsive.spec.ts:120` | E-X2 covers the five heaviest routes, not every §8.1 route. Extend the width loop to the route list E-X1 already uses |

Rows 74, 75, 79, 82 and 84 stay open as filed by earlier gates. Row 83 is superseded by 85.

## SCOPE

- Out-of-scope work found: **none unapproved.**
  - §4 excludes "Uploading files of any kind" and §14 forbids upload inputs. Exactly two upload sites exist: check-in belongings (`CheckInWizard.tsx:284`) and diary photos (`MediaRecordDialog.tsx:91`). They are covered by the owner decisions "Add the uploader" (2026-10-14) and "Yes, diary photos too" (2026-10-15).
  - Incident photos are not offered.
  - There is no payment control (U-X4).
  - There is no invoice rendering, only links.
  - No dependency was added: `web/package.json` is unchanged.
- Files outside §13: 10 of 130. Each is declared or owner-authorised:
  - `web/nav/areas/boarding.ts`: the shells read this registry instead of the three §13 layouts (§1 Amended (build); the APT/IMP/BIL precedent). It replaces the layout lines, not an expansion.
  - `web/tests/e2e/boarding-helpers.ts`: declared (§1 Amended (build); APT/EMR/POR precedent).
  - `web/app/(auth)/sign-in/page.tsx`: owner decision "Yes, allow it" (one token).
  - `.gitignore`: one line, the SECURITY LOW fix (row 68), declared in §1 Amended (gate fix round). It is not in §13 and has no owner approval. Record it in the approval Amended row.
  - `docs/handover/HO-BRD-002.md`: the §1 Amended rows, the project's amendment mechanism.
  - `docs/gates/HO-BRD-002-*.md` and `docs/test-cases/TC-HO-BRD-002.md`: §13-allowed.
- Inside the §13 globs, beyond their notes:
  - `lib/routing/boarding-links.ts` is beyond "allow-list only". It is declared in §1 Amended (gate fix round), on the `portal-links.ts` precedent.
  - `components/boarding/stay/GateOverrideNote.tsx` (new in 7dfcbd10) is inside `components/boarding/**`.

## UNDECLARED DEVIATIONS

- **Row 77 recorded as "Fixed in the FE" when three staff sites are untouched.** One of them is the StayWorkspace header the row names, and the commit message says only "rows 76, 77 (FE)" (row 86).
- **The fix commit 7dfcbd10 edited another gate's record** (row 93).

None of the other deviations is undeclared. The §1 Amended rows list the contract differences, the absent fields, the nav registry, the helper and the owner decisions, and match what I read in the source.

## OWNER QUESTIONS

1. **A29 / NFR-PRF-3.** Measure the dashboard and task board p95 now, against a re-seeded HO-BRD-001 API, or explicitly defer A29 to the integrator's real-API run after HO-BRD-001 merges with a committed seeder? Either answer lets this gate PASS on re-check.
2. **Call owner without a phone on real data** (row 92). Is it acceptable to ship Boarding while the escalation card cannot show whom to call, until HO-BRD-001 adds the customer's name and phone?
3. **The cancel confirm cannot state the deposit outcome** (U-B6, UI-3), and **the reserve confirm cannot state the deposit amount and pay-by**, because the backend does not send them before the action. Accept until HO-BRD-001 adds them?
4. **Override vaccination shown to every staff member** with a blocked cat, with the server refusing non-admins (row 90). Accept until `canOverrideGate` is on the wire?

---

## Re-check, 2026-10-15 (range a41be7eb..77ee7555)

GATE: ACCEPTANCE (re-check)
HANDOVER: HO-BRD-002
COMMITS: 77ee7555 (fix round 4 and the owner decisions in §1 Amended); first run a41be7eb
DATE: 2026-10-15
STATUS: PASS

**Why PASS.** The first run's only HIGH was row 85: A29 had no evidence. The owner has now deferred A29 explicitly, and that decision is recorded in the handover. No other criterion has changed state. Nothing in 77ee7555 removes evidence: it touches six deposit-badge sites, one test and two documents.

**What remains.** Row 77 is still not completely fixed: "Deposit received ৳0.00" reaches an Enquiry (row 96). This is MEDIUM. It goes to the backlog and does not block the gate.

### What changed in 77ee7555

- `DepositStateBadge` takes the stay's status. For an Enquiry it reads "Deposit set at reserve" (`StatusBadges.tsx:67-76`).
- That status is passed at five sites: the workspace header (`StayWorkspace.tsx:104`), `DepositPanel.tsx:70`, `StayList.tsx:90`, `MyStaysList.tsx:57` and `BoardingDashboard.tsx:76`.
- `DepositPanel.tsx:55-73` shows "Worked out when the stay is reserved" for an Enquiry instead of Required/Accepted/Outstanding 0.00.
- New test: `gate-fixes.test.tsx` 'ACCEPTANCE row 86 ... shows "Deposit set at reserve" for an Enquiry and the real state otherwise'.
- The handover's §1 has a new "Amended (acceptance decisions)" row. Rows 85-95 were copied into the TEST record with status columns, plus a note there.

### Verification run by this gate (worktree at 77ee7555)

`web/node_modules` was a temporary junction to the main checkout. It was removed afterwards, and `git status` was clean.

- `npx vitest run tests/components/boarding --maxWorkers=2`: **7 files, 89 of 89 passed**, which matches the builder's 89/89.
- `npx tsc --noEmit`: exit 0.
- `npx eslint` on the seven changed web files with `--max-warnings 0`: exit 0.

### The owner decisions are recorded in §1 Amended

Verified at `docs/handover/HO-BRD-002.md`, row "Amended (acceptance decisions)", quoting the owner verbatim:

- (1) **"Measure at merge"**: A29 is deferred to the integrator's real-API run after HO-BRD-001 merges (row 85).
- (2) **"Ship, follow-up fixes"** applies to four backend gaps:
  - Call owner has no name or phone;
  - the cancel confirm cannot state the deposit details;
  - the reserve confirm cannot state the deposit details;
  - Override vaccination is shown to all staff.

  These ship as they are. The follow-ups are HO-BRD-001-FU1 and a matching screens follow-up (rows 90, 92).
- (3) The row 77/86 fix is described. BE row 84 is still open.

### Ruling on A29

**A29 is DEFERRED by owner decision. It is not MET, and this gate counts no evidence for it.**

- The deferral is explicit, verbatim, recorded in §1, and names who closes it and when: the integrator, on the real-API run after HO-BRD-001 merges.
- That is the same basis on which the HO-POR-002 re-check passed with its own unmeasured performance row.
- NFR-PRF-3 stays unproven for Boarding until that run records the p95 over 20 loads of `/staff/boarding` and `/staff/boarding/tasks`.
- The measurement goes into the integrator's merge checklist alongside T-R18b (row 91), which needs the same seeded real-API environment.
- If the integrator's run exceeds 1.5 s, the finding goes to Boarding as a new row. It does not reopen this handover's other criteria.

### The grep for Enquiry deposit text

The search covered every `DepositStateBadge`, `DEPOSIT_STATE_LABEL`, `DEPOSIT_STATE_HELP`, "No deposit needed", "Nothing to pay up front" and `deposit.(required|accepted|outstanding)` site under `web/components`, `web/app` and `web/lib`.

**"No deposit needed" and "Nothing to pay up front" no longer reach an Enquiry.** The remaining uses are all guarded:

| Site | Why it cannot reach an Enquiry |
|---|---|
| `ReservedPanel.tsx:42,45` | Shown only after reserving |
| `DepositSummary.tsx:37-40,54` | Inside the non-Enquiry branch since 7dfcbd10 |
| `DepositPanel.tsx:63-70` | Inside the non-Enquiry branch |
| The other five badge sites | Pass `stayStatus` |

The dashboard's `DashboardStayRow` carries `status` (`types.ts:887`), so the dashboard site works too.

**One 0.00 deposit text still reaches an Enquiry.** `StayCharges` prints "Deposit received ৳0.00" whenever `depositReceived` is passed (`StayCharges.tsx:66-72`). Two callers pass `stay.deposit.accepted` unconditionally:
- the owner stay page, `StayOverview.tsx:179`;
- the staff Charges tab, `StayWorkspace.tsx:254`.

The API sends `accepted 0.00` for an Enquiry, so both screens show it. TEST row 77 named this string explicitly ("and 'Deposit received ৳0.00'"). It is factually true, since nothing has been received, so it does not mislead the way "No deposit needed" did. It is MEDIUM and does not block: no §18 criterion depends on it, and the owner's instruction covered "No deposit needed". The fix is one line at each caller: pass `depositReceived` only when `status !== 'Enquiry'` (row 96).

### Are rows 85-95 honest?

These are the status columns as copied into the TEST record by 77ee7555.

| Row | Status written | Honest? |
|---|---|---|
| 85 | Deferred by owner decision | Yes, matches §1 Amended (1) |
| 86 | Fixed | **Overstated.** The badges and `DepositPanel` are fixed. "Deposit received ৳0.00" still reaches an Enquiry on two screens. The same overstatement now sits on TEST row 77 ("Fixed ... in fix round 4"). Read both as "Fixed for the deposit badges and the staff panel; Deposit received 0.00 open (row 96)" |
| 87 | Open | Yes |
| 88 | Open | Yes |
| 89 | Open | Yes |
| 90 | Shipped by owner decision; follow-up | Yes, matches §1 Amended (2) |
| 91 | Carried | Yes |
| 92 | Shipped by owner decision; follow-up | Yes, matches §1 Amended (2) |
| 93 | Noted | Yes, but the pattern recurred: 77ee7555 again edited the TEST record, changing row 77's status and appending rows 85-95. It added an explicit note and left the record's STATUS line alone, which is better than 7dfcbd10, but it is still a builder editing a gate artifact (LOW, row 93 stands) |
| 94 | Open (integrator) | Yes |
| 95 | Open | Yes |

### Criteria after the re-check

- **A1-A28:** unchanged from the first run, all MET. The same ten rest on MSW plus unit evidence, with the real-API halves carried.
  - A7, A12, A18, A19 and A23 keep their thin-evidence notes and rows 87-90 and 92.
  - The owner decision accepts that A18's Call owner has no phone and A7's control is shown to all staff, both pending follow-up.
- **A29:** DEFERRED by owner decision, as ruled above.

### New row (continues BRD-002 numbering; renumber on merge per row 94)

| # | Sev | Where | Item |
|---|---|---|---|
| 96 | MEDIUM | **Resolved** (fix round 5: `depositReceivedOf` passes the received deposit only past Enquiry; tested) -- `StayOverview.tsx:179`, `StayWorkspace.tsx:254` (via `StayCharges.tsx:66-72`); TEST row 77 and row 86 status | An Enquiry still shows "Deposit received ৳0.00" on the owner stay page and the staff Charges tab: the remaining piece of row 77. Pass `depositReceived` only when the stay is past Enquiry, and add a test. Correct rows 77 and 86 to "Partly fixed" until then |

### Integrator conditions carried to merge

These are the first run's conditions plus A29:

1. HO-BRD-001 merges first. Its ACCEPTANCE is FAIL at 45bac6da.
2. Renumber the BRD-002 backlog rows (row 94).
3. Resolve the `decisions.md` append conflict by keeping both sections.
4. Record `.gitignore` in the approval Amended row.
5. On the post-merge real-API run, with a committed seeder (row 82):
   - E-X3 p95 (A29);
   - T-R18b (row 91);
   - E-A2 at 20 nights;
   - E-A3;
   - the E-A4 notice;
   - E-B2;
   - E-D1 escalation and inbox;
   - E-E1, E-E2 and E-E4 through the UI;
   - the E-F1 final-invoice coverage;
   - E-F2.

### Scope

- Files changed in a41be7eb..77ee7555: 9.
  - Seven are `web/components/boarding/**` and `web/tests/components/boarding/**`, inside §13.
  - Two are the handover §1 and the TEST record (see row 93).
- Out-of-scope work: none.
- Undeclared deviations: the row 86/77 "Fixed" status, which overstates the fix (row 96).
