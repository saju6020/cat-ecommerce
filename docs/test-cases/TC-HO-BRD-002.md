# TC-HO-BRD-002 -- Boarding frontend test cases

Source: `docs/handover/HO-BRD-002.md` section 15 (U) and section 16 (E). Two runs are recorded
separately (section 19 gate 4): **MSW** (the mock is the HO-BRD-001 contract as published in its section
1.1 Amended; fixtures in `web/mocks/handlers/boarding/seed.ts`) and **real API**.

## Run status

| Run | Status |
|---|---|
| Vitest (component, contract and adapter tests) | `web/tests/components/boarding/*` -- 61 tests (53 + 8 final-wire tests over JSON captured from the real API); 60 pass, 1 time-of-day failure (see the note below the table) |
| Playwright on MSW, `chromium` | `web/tests/e2e/boarding-{booking,portal,staff,a11y-responsive}.spec.ts` |
| Playwright on MSW, `mobile-360` | booking, portal and staff specs |
| **Real API (E-series)** | **RUN 2026-10-15** against `ho/BRD-001` 95a97bab (port 5117, DB `catecommerce_brd002e2e`, web on 3117): `web/tests/e2e/boarding-real-api.spec.ts`, `BRD_REAL_API=1`, `--workers=1` -- **9 of 9 pass** on a fresh stay (gate; booking, review quote and reserve; deposit as an earmarked Billing payment, auto-confirmed; check-in with an uploaded belongings photo; tasks, one-tap Done and an observation; an incident; the owner diary; early check-out with the server preview; daycare). Not run on the real API: E-B2, the E-A4 notice, E-F1, E-X1 on real data, E-X3 |

Note: `mock-api.test.ts` > task board > 'lists the day grouped by unit...' fails when the suite runs shortly after local midnight (the seeded overdue tasks are relative to now and fall on the previous day). It passes by day and fails identically on the earlier commit, so it is not caused by this change.

## U-series (component)

| ID | Case | Evidence |
|---|---|---|
| U-A1, U-A2, U-A3 | results per type, itemised quote with a separate discount, full range with alternatives and waitlist | `booking.test.tsx` |
| U-A4 | capacity: Continue blocked with the capacity stated; server message verbatim | `booking.test.tsx` |
| U-A5 | gate panel: plain-language reason, Book a vaccination link (site-relative only) | `booking.test.tsx` |
| U-A6 | blocked enquiry resumes at the gate step | `boarding-portal.spec.ts`, `boarding-booking.spec.ts` |
| U-A7 | care profile errors keep values | `booking.test.tsx` |
| U-A8 | reserve `409 UnitNoLongerAvailable` back to results | handled in `BookingFlow.doReserve`; **no automated case** (no way to force the race in the mock) |
| U-A9, U-A10 | reserved panel without payment control; countdown on the server clock, announcements at 24 h / 1 h / expiry | `booking.test.tsx` (clock skew 90 s) |
| U-B1 | deposit states read differently | `care-and-stay.test.tsx`, `boarding-portal.spec.ts` |
| U-B2, U-B3, U-B4, U-B5 | diary, incident acknowledge, vet approval, summary | `boarding-portal.spec.ts` |
| U-B6 | cancel confirm states the deposit outcome | **PARTIAL (the final backend does not provide it)**: no pre-cancel deposit outcome on the wire; the mock-fed sentence is tested in `boarding-portal.spec.ts`; against the real API the confirm cannot state it (section 1 Amended, final alignment) |
| U-C1, U-C2 | dashboard sections; Record call keeps typed text through a refresh | `boarding-staff.spec.ts` |
| U-C3, U-C4 | board distinguishable without colour; day list at 360 px, no horizontal scroll | `care-and-stay.test.tsx`, `boarding-staff.spec.ts` |
| U-C5, U-C6, U-C7 | unit table read-only rate; maintenance overlap with dates; rate cards Current/Scheduled/Past, backdating refused | `boarding-staff.spec.ts` |
| U-D1 | one pointer event completes a task; no dialog | `care-and-stay.test.tsx`, `boarding-staff.spec.ts` (E-C2) |
| U-D2 | refused completion rolls back with the server's reason | `care-and-stay.test.tsx`, `boarding-staff.spec.ts` |
| U-D3, U-D5, U-D6 | overdue as text; not-eating from the server flag; handling note first | `care-and-stay.test.tsx`; the not-eating flag and `minutesLate` are **mock-only** on the board (the final task list has neither; the dashboard and observations carry not-eating) |
| U-D4 | open observation sheet keeps values through a refresh | `care-and-stay.test.tsx`, `boarding-staff.spec.ts` |
| U-D7 | keyboard completion | `care-and-stay.test.tsx` |
| U-E1 | check-in checklist errors mapped to cat and field; one submission | `mock-api.test.ts`, `boarding-staff.spec.ts` |
| U-E2 | signature strokes as numeric JSON; typed path; no image | `care-and-stay.test.tsx`, `boarding-staff.spec.ts` |
| U-E3 | belongings without photos and the server refusing | **PARTIAL**: the mock refuses when its switch is on; the screen shows the message verbatim; not asserted in a spec |
| U-E4, U-E5 | extend refused with options; relocate needs a reason, confirm names both units | `boarding-staff.spec.ts` (E-E2) |
| U-E6, U-E7 | flag infectious names the unit; entry log | `boarding-staff.spec.ts` (E-E4); `NoIsolationUnitAvailable` shown in the dialog, **not asserted** |
| U-E8 | early check-out recalculation **before** submit | **MET**: `GET .../check-out-preview` is wired; `boarding-staff.spec.ts` (E-E3, MSW) and `boarding-real-api.spec.ts` (E-E3: '1 of 3 nights are charged', the Void/Record lines, then the result); `wire-final.test.ts` reads the real JSON |
| U-X1, U-X2 | server 400 mapped to fields; dirty form guard | care profile, unit and rate-card forms; `UnsavedChangesGuard` is wired, **not asserted per form** |
| U-X3 | actions only from `allowedTransitions[]`; `403` shows `PermissionDeniedState` | by construction in `StayWorkspace` / `QueryGate`; **no dedicated test** |
| U-X4 | no payment words, no upload in the boarding source | `care-and-stay.test.tsx` (source scan); the bare word "card" is allowed in the how-to-pay text |

## E-series

| ID | Case | MSW | Real API |
|---|---|---|---|
| E-A1 | blocked cat, vaccination, resume | `boarding-booking.spec.ts` | PASS (the link is rewritten to a site path) |
| E-A2 | 20 nights, discount itemised, review total matches | `boarding-booking.spec.ts` | PASS (a 3-night booking with the enquiry quote and reserve; the 20-night discount was not run) |
| E-A3 | two cats one family unit; four cats refused | capacity refusal only | pending |
| E-A4 | full range, waitlist | first half (join) | pending (the inbox notice needs the worker) |
| E-B1, E-B3 | record in Billing then Confirm; pending approval | `boarding-staff.spec.ts` (ledger simulated through `sessionStorage`) | PASS (Billing auto-confirms; the Confirm button path is MSW only) |
| E-B2 | unpaid reservation lapses | `boarding-portal.spec.ts` (70 s) | pending |
| E-C1 | check-in with belongings and signature | `boarding-staff.spec.ts` (typed name; drawn path asserts capture) | PASS (photo upload through FileUploader, typed signature) |
| E-C2 | caretaker completes a task in <= 2 taps (tablet viewport) | `boarding-staff.spec.ts` | PASS (one tap; worker-generated tasks) |
| E-C3, E-C4 | overdue on board and dashboard; two None observations | `boarding-staff.spec.ts` | PARTIAL: an incident was recorded and the owner saw it (real); the not-eating pair and dashboard were probed through the API only |
| E-D1, E-D2 | high incident / owner acknowledges | portal acknowledge (E-D2 first half); escalation timing | the owner diary (E-D1 of the real spec) PASS; escalation not run |
| E-E1 | refer a cat; approval | staff request + portal approve are built and tested separately | API-level: refer, request, owner approve (`{approve}`), staff answer and `referrals/{id}/return` all 2xx; not driven through the UI |
| E-E2, E-E3, E-E4 | extend/relocate; early check-out; flag infectious | `boarding-staff.spec.ts` | E-E3 PASS (preview, then the result); E-E2 and E-E4 not run on the real API |
| E-F1 | 60-night stay cycle invoice visible | `boarding-staff.spec.ts` (Charges) | pending |
| E-F2 | dashboard refresh keeps an open dialog | `boarding-staff.spec.ts` (U-C2) | pending |
| E-G1 | daycare | `boarding-staff.spec.ts` | PASS |
| E-X1 | axe, zero serious/critical | `boarding-a11y-responsive.spec.ts` -- 24 routes plus signature, task board states | pending (real data) |
| E-X2 | no horizontal scroll at 360/768/1024/1440 | `boarding-a11y-responsive.spec.ts` -- the heaviest five routes | n/a |
| E-X3 | NFR-PRF-3 dashboard and task board <= 1.5 s p95 | **not measured** (needs the real API) | pending |

---

# TEST gate, 2026-10-15 -- cases written BEFORE execution

Gate: TEST (fourth). Head under test: `ho/BRD-002` b1958d45. Backend for the real-API run: `ho/BRD-001` (cede66fe or later,
built in a clean detached worktree). Written 2026-10-15 before any command of this gate was run. Every row starts
`NOT RUN`; the Status column is updated one row at a time as each is observed. IDs prefixed `T-` are this gate's.
The handover's section 15/16 cases are tabulated above and are not edited in place, so the history stays visible.

> Statuses record what was observed, not inferred. `BLOCKED` marks a case that could not be executed or whose result was
> reasoned about rather than watched.

## Summary (TEST gate)

| | Count (T- rows, excluding the 5 owner-decision rows) |
|---|---|
| Total | 51 |
| PASS | 45 |
| FAIL | 4 (T-F1 known inventory flake; T-R18 View photo 404, **row 76 HIGH**; T-G13 and T-G14 known open LOW rows 74 and 75) |
| BLOCKED | 2 (T-F3 test not in this tree; T-G6 `overriddenOn` rendering) |
| NOT RUN | 0 |

Owner-decision rows: 4 PASS, 1 FAIL ("Yes, diary photos too": the photo uploads and lists, the owner's link does not open).

Tests added by this gate: 7 vitest tests in `web/tests/components/boarding/test-gate.test.tsx` (the mutation spot-checks showed the
original 77 Boarding tests did not pin M2, M1c, M4, M5 and M7) and 10 real-API tests in `boarding-real-api.spec.ts` (19 in all).

## Environment (TEST gate)

- Branch `ho/BRD-002` b1958d45, Windows 11, Node via the main checkout's `node_modules` (a junction, removed afterwards), `.env.local` = the example (mocks on) for the MSW runs.
- Static and MSW runs: `tsc`, `eslint`, `vitest --maxWorkers=2` (full), `next build`, Playwright chromium and mobile-360, `--workers=1`, PORT 3117.
- Real-API run: the API was built from a clean detached checkout of `ho/BRD-001` at 2216ff79 (its `src/` is byte-identical to cede66fe: `git diff cede66fe..2216ff79 -- src` is empty; only tests changed), Debug build, on port 5117, against the throwaway database `catecommerce_brd002e2e`
  (dropped, migrated with `dotnet ef database update --connection` from that checkout, seeded, re-created twice as seed gaps were found). Web on 3117 with `NEXT_PUBLIC_USE_MOCKS=false`. The Boarding worker ran (60 s). `catecommerce_dev` and `catecommerce_test` were never touched.
- The throwaway seeder was an xUnit class inside the detached checkout's integration-test project (not in this repo). It created a Rabies core vaccine, a branch, a 0% tax rate and a billing profile (without these the worker refuses invoices with `BillingProfileMissing`), three Standard units and one Deluxe unit with rate cards, a rostered caretaker, daycare capacity and rate card, one customer and 11 cats, and two direct SQL helpers that moved stays and their charges into the past (the only way to make a late check-out and a cycle invoice reachable with the real clock).
- Removed afterwards: the detached checkout, the seeder, the seed file `web/seed-brd002.json`, the API's temp store, the throwaway database, `.next`, `test-results`, the `node_modules` junction and `.env.local`.
- Not available: a performance run (E-X3 p95), the escalation timing (E-D1), the lapse and waitlist notice (E-B2, E-A4), SMS/email (no provider is configured; notification rendering warns in the API log because `PublicSite:WebBaseUrl` was `http://localhost:3117`, not https).

## T-S static and MSW runs

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| T-S1 | `tsc --noEmit` | exit 0 | PASS | `npx tsc --noEmit` exit 0 (re-run after this gate's test additions: still clean) |
| T-S2 | `eslint` (npm run lint) | exit 0 | PASS | `npm run lint` (eslint . --max-warnings 0) exit 0; re-run clean after the additions |
| T-S3 | `vitest --maxWorkers=2` full web suite | all pass; any failure outside Boarding re-run alone | PASS | `vitest run --maxWorkers=2`: 226 files, 1592 of 1594 tests pass; the 2 failures are `inventory/shared-pieces.test.tsx` U-barcode (outside Boarding), which passes alone 17/17 (T-F2). All 77 Boarding tests passed in the full run; with this gate's 7 new tests the Boarding folder is 84/84 |
| T-S4 | `next build` | exit 0 | PASS | `npx next build` exit 0, all routes listed |
| T-S5 | Boarding MSW Playwright suite, project chromium, `--workers=1`, PORT 3117 | all pass | PASS | chromium, workers=1, PORT=3117: boarding-booking, -portal, -staff, -a11y-responsive: 69 passed (5.4 min) |
| T-S6 | Boarding MSW Playwright suite, project mobile-360, `--workers=1`, PORT 3117 | all pass | PASS | mobile-360: 68 passed first run; 1 failed (a11y E-X2 `catCount=3`: `signInEstablished` waited 60 s for the MSW-ready flag, a setup race, not an assertion); the same test run alone passes (19.7 s) |

## T-F known flakes outside this area (each re-run alone if it fails)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| T-F1 | inventory goods-receipt-form | passes alone, or recorded as the known flake | FAIL | inventory `goods-receipt-form.test.tsx` alone, 5 runs: 3 passed, 2 failed (U-C4b, U-C6: `Unable to find role=listitem name /line 1: kitten dry food/`). It passed inside the full run. Known flake, outside Boarding; the branch has no change under `components/inventory` or `tests/components/inventory`. Not a gate failure |
| T-F2 | shared-pieces | passes alone, or recorded as the known flake | PASS | `shared-pieces.test.tsx` failed once in the full run, then 17/17 alone |
| T-F3 | SRG timeline after local midnight (U-C1) | passes alone, or recorded as the known flake | BLOCKED | The SRG timeline test is not in this branch's tree (no `tests/components/surgery`); it passed nowhere because it does not exist here. Unblock: run it on master |
| T-F4 | booking-slots | passes alone, or recorded as the known flake | PASS | `staff-booking-slots.test.tsx` alone: 2/2 pass (it did not fail in the full run) |

## T-R real-API run (ho/BRD-001 cede66fe+, port 5117, throwaway DB `catecommerce_brd002e2e`, web on 3117)

The existing nine flows (serial, one stay) then the new cases. A case needing a state the seed or API cannot reach is
`BLOCKED`, not skipped.

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| T-R1 | E-A1 vaccination gate: unvaccinated cat explained, booking kept, link is a site path | as spec | PASS | run 3 (fresh DB, 2026-10-15), `boarding-real-api.spec.ts`, chromium, workers=1: ok (2.7 s); the link is `/book?catId=`, and the gate names Rabies |
| T-R2 | E-A2 booking, care profile, review quote, reserve; no payment control | Reserved, deposit shown | PASS | run 3 (fresh DB, 2026-10-15), `boarding-real-api.spec.ts`, chromium, workers=1: ok (2.1 s) |
| T-R3 | E-B1 deposit in Billing, stay Confirmed | Confirmed | PASS | run 3 (fresh DB, 2026-10-15), `boarding-real-api.spec.ts`, chromium, workers=1: ok (1.0 s); the deposit was an earmarked Billing payment, the stay reached Confirmed |
| T-R4 | E-C1 check-in with uploaded belongings photo | In the care centre | PASS | run 3 (fresh DB, 2026-10-15), `boarding-real-api.spec.ts`, chromium, workers=1: ok (1.4 s); a PNG uploaded through FileUploader, the stay read In the care centre |
| T-R5 | E-C2 worker tasks, one-tap Done, observation | Done stamp, dialog closes | PASS | run 3 (fresh DB, 2026-10-15), `boarding-real-api.spec.ts`, chromium, workers=1: ok (46.5 s, mostly the worker's task generation) |
| T-R6 | E-C3 incident from the board, queue and owner view | visible in both | PASS | run 3 (fresh DB, 2026-10-15), `boarding-real-api.spec.ts`, chromium, workers=1: ok (3.6 s) |
| T-R7 | E-D1 owner diary shows the observation | text visible | PASS | run 3 (fresh DB, 2026-10-15), `boarding-real-api.spec.ts`, chromium, workers=1: ok (0.7 s) |
| T-R8 | E-E3 early check-out preview then result | "1 of 3 nights charged", then Checked out | PASS | run 3 (fresh DB, 2026-10-15), `boarding-real-api.spec.ts`, chromium, workers=1: ok (1.0 s); '1 of 3 nights are charged', recalculated lines, then Checked out |
| T-R9 | E-G1 daycare book, check in, check out, charge | charge shown | PASS | run 3 (fresh DB, 2026-10-15), `boarding-real-api.spec.ts`, chromium, workers=1: ok (3.4 s); charge shown after check-out |
| T-R10 | Staff vet-limit/authority edit on a Reserved stay with no reason | refused (reason required; no save) | PASS | run 3 (fresh DB, 2026-10-15), `boarding-real-api.spec.ts`, chromium, workers=1 (T-R10/11 test, ok 3.9 s): on `Reserved — not yet confirmed`, changing the limit 2000 to 2500 showed the reason box, Save showed 'Give a reason for changing the vet authorisation or the limit', NO `PUT care-profile` request was sent (request listener), and after a reload the limit was still 2000 |
| T-R11 | Same edit with a reason | saved; new value shown after reload | PASS | run 3 (fresh DB, 2026-10-15), `boarding-real-api.spec.ts`, chromium, workers=1 (same test): with the reason, Save went through ('care profile is saved'); a reload showed 2500 |
| T-R12 | Reload on the review step | the price breakdown is still shown (re-quote), Reserve enabled | PASS | run 3 (fresh DB, 2026-10-15), `boarding-real-api.spec.ts`, chromium, workers=1 (T-R12/13, rerun on cat Spare3 with the fixed spec: ok): after `page.reload()` on the review step the heading, the 'Price breakdown' and an enabled Reserve are still there |
| T-R13 | An Enquiry stay (staff workspace and My stays) | no balance text, no "0.00", no "Not available" balance line | PASS | same test: no text matching /balance/ on the owner review step, the Enquiry's My stays row, the owner stay page, or the staff workspace (overview, Charges, Care) and staff list row. FINDING (row 77): both stay pages state 'Deposit ৳0.00 / No deposit needed / Nothing to pay up front' for an Enquiry |
| T-R14 | Summary billing labels after a cycle or final invoice | "Not yet invoiced" / "Invoiced, still to pay", never "Balance due 0.00" | PASS | run 3 (fresh DB, 2026-10-15), `boarding-real-api.spec.ts`, chromium, workers=1 (T-R14 ok 46.4 s): after the worker's FINAL invoice the owner summary shows 'Your invoice', 'Not yet invoiced' and 'Invoiced, still to pay', and no 'Balance due'. LATE-RUN-3 (ok): a 45-night stay moved 35 days back got its CYCLE invoice from the worker; same labels, no 'Balance due' |
| T-R15 | Late check-out (stay past its booked departure): the preview | preview shown before submit, late wording, no "unit is free from" past-time clause | PASS | LATE-RUN-1 (ok 3.1 s): stay moved 3 days into the past; the wizard note reads 'This stay is past its booked departure', the preview heading 'If you check out now', '(2026-10-13). 3 nights are charged', the recalculated lines, and no 'the unit is free from' clause |
| T-R16 | Late check-out submit | not refused; extra nights charged (owner: charge extra nights, never block) | PASS | LATE-RUN-1: Check out was accepted (no refusal), heading 'Checked out'. The charged nights were seen in the preview; the billed amounts after the submit were not asserted |
| T-R17 | Next booking in the same unit after the late check-out | staff stay page shows the UnitOverstayed banner "previous cat has not left" | PASS | LATE-RUN-1/2 (ok): before the check-out the next booking showed no overstay text; after it, its staff page shows 'Needs attention' and 'The previous cat in this unit has not left yet: move this booking.' and the dashboard links the stay |
| T-R18 | Diary photo upload (staff, shared FileUploader, purpose BoardingMedia), then the owner diary | upload accepted; media record created; owner sees View photo | FAIL | Upload PASS (ok 2.3 s): staff 'Add a diary photo' uploaded a PNG through FileUploader (purpose BoardingMedia) and the entry shows in the owner diary with a 'View photo' link. Opening the photo FAILS (T-R18b): the link's href is `/api/files/{id}/content?exp=..&sig=..` (API-relative); the browser resolves it against the WEB origin and gets 404. The same path on the API origin returns 200 image/png. Row 76 HIGH |
| T-R19 | Customer sign-in with `?next=/boarding?...` | lands on /boarding with the search parameters intact | PASS | run 3 (fresh DB, 2026-10-15), `boarding-real-api.spec.ts`, chromium, workers=1: signed-out `/boarding?arrivalOn=..&departureOn=..&catCount=1` redirected to `/sign-in?next=`, and after signing in the URL is `/boarding?arrivalOn=<same>&departureOn=<same>` with 'Available units' |
| T-R20 | axe pass on real data (stay workspace, portal stay, diary, dashboard, tasks, booking review) | zero serious/critical | PASS | AXE-REAL (ok 34.5 s) on the checked-out real stay and live data: 6 owner pages (My stays, stay, diary, summary, daycare, review) and 10 staff pages (dashboard, board, stays, workspace x4 tabs, tasks, incidents, daycare): zero serious/critical |

## T-M mutation spot-checks (break production code, run the pinning test, expect FAIL, restore, expect clean `git status`)

| ID | Mutation | Pinning test expected to fail | Status | Evidence |
|---|---|---|---|---|
| T-M1 | null `balanceDue` rendered (prints a value instead of nothing) | gate-fixes / wire-adapters | PASS | adapter `?? null` -> zero money: 3 tests fail (gate-fixes, mock-api, wire-adapters). StayCharges: 2 fail (gate-fixes). ReservedPanel (L9) SURVIVED the original suite; killed by new `test-gate.test.tsx` M1c |
| T-M2 | drop the `vetAuthorityChangeReason` requirement | gate-fixes / care-and-stay | PASS | form requirement (`if (changed && !reason.trim())` disabled) and 'reason not sent' both SURVIVED the original 77 tests (the existing test only exercised `saveStaffCareProfile`, not the form, and no Playwright MSW spec touches it). Killed after strengthening by `test-gate.test.tsx` M2 (both variants fail). Also killed on the real API by T-R10 |
| T-M3 | remove the `UnitOverstayed` label | labels / stay test | PASS | `UnitOverstayed: ''`: 1 fails (gate-fixes). NOTE the pin is the label constant only; the banner rendering is pinned only by LATE-RUN-2 on the real API |
| T-M4 | early/late decided in the browser instead of the server preview | check-out wizard test | PASS | early/late from the browser clock vs `stay.departureOn`: SURVIVED the original suite; killed after strengthening by `test-gate.test.tsx` M4 (server says on time for a far-future departure; server says late) |
| T-M5 | signature not cleared after a successful submit | gate-fixes / care-and-stay | PASS | signature kept: check-in drafts not cleared and check-in mutation not reset, check-out mutation not reset: all SURVIVED originally; killed after strengthening (3 of them) by `test-gate.test.tsx`. One mutant still survives: check-out drafts not cleared (M5a). It is unobservable: the wizard swaps to the result panel on success, so the state is unreachable (equivalent mutant, no row) |
| T-M6 | `normaliseGate` returns the absolute link unchanged | gate / wire-adapters | PASS | `bookingLink: c.bookingLink`: 2 fail (gate-fixes, wire-final) |
| T-M7 | quote re-fetch on reload removed from BookingFlow | booking / gate-fixes | PASS | quote re-fetch guard turned into `return`: SURVIVED the original suite (no component test for the reload; Playwright MSW has none); killed after strengthening by `test-gate.test.tsx` M7 (BookingFlow on a stay id). Also T-R12 on the real API |

A surviving mutant (all tests still pass) is a weak assertion and becomes a row in the gate file.

## T-G regression rows from earlier gates

| ID | Gate | Finding | Case | Status | Evidence |
|---|---|---|---|---|---|
| T-G1 | ARCH | H1 absent rule (summary balance, null balanceDue, signature method, referredBy) | gate-fixes and wire-adapters tests; T-M1 | PASS | gate-fixes + wire-adapters pass; T-M1 kills both adapter and StayCharges mutants |
| T-G2 | ARCH | M1 fc6ef889 vet authorisation reason | T-R10, T-R11, T-M2 | PASS | T-R10/11 on the real API; T-M2 (originally unpinned, now killed) |
| T-G3 | ARCH | M6 UnitOverstayed label | T-R17, T-M3 | PASS | T-R17 on cede66fe-or-later; T-M3 |
| T-G4 | ARCH | L8 late preview omits the past "free from" clause | T-R15 | PASS | T-R15 (no 'free from' clause on the real late preview) + gate-fixes 'does not describe a past unitFreeFrom' |
| T-G5 | ARCH | L9 null balance on ReservedPanel draws nothing | T-M1 and a unit test | PASS | T-M1c killed by the new test |
| T-G6 | ARCH | L10 `overriddenOn` shown with the override reason | unit test | BLOCKED | `overriddenOn` is mapped and drawn (StayWorkspace.tsx:124) but no test renders it and no real-API stay had an override. Unblock: override a gate in the real run, or a component test over `built.staffStay` with `overridden: true, overriddenOn` |
| T-G7 | CODE | M1 quote on reload | T-R12, T-M7 | PASS | T-R12 (real API) and the new component test; T-M7 |
| T-G8 | CODE | M5 early/late from the preview; error with Try again | T-R15, T-M4, unit test | PASS | T-R15 and gate-fixes (error with Try again, late wording); T-M4 |
| T-G9 | CODE | M6 incident occurredOn field | `boarding-staff.spec.ts` incident case | PASS | E-C3 incident recorded on the real API with the default 'now' (occurredOn accepted). The date/time fields' future-time refusal has no test (row 80) |
| T-G10 | SEC | signature drafts cleared, `gcTime: 0` | T-M5 | PASS | T-M5 (check-in and check-out mutation cache; check-in summary signer) |
| T-G11 | SEC | gate link rewrite on daycare and reserve payloads | T-M6, unit test | PASS | T-M6; the daycare/reserve payloads share `normaliseGate` (no separate test) |
| T-G12 | SEC | `BRD_SEED` required; seed JSON git-ignored | `git check-ignore web/seed*.json` | PASS | `git check-ignore web/seed-brd002.json` printed the path (ignored); `BRD_REAL_API=1 playwright test --list` without BRD_SEED throws 'needs BRD_SEED=<path>' |
| T-G13 | ARCH | L11 summary charge kind from `lineRef`; `approvedIncrease` default dropped | unit test (else gap row) | FAIL | Not fixed at b1958d45: `my-boarding.ts:521` still sets the summary charge `kind: 'RateUnit'` and `staff-boarding.ts:627` still defaults `approvedIncrease`. LOW, open since the re-check (row 74); does not fail the gate |
| T-G14 | ARCH | L12 re-quote failure shows an error with Try again | unit test (else gap row) | FAIL | Not fixed at b1958d45: `BookingFlow.tsx:148` still ends the re-quote with `.catch(() => undefined)`, so a failed re-quote leaves Reserve disabled with no message. LOW, open since the re-check (row 75) |

## T-O owner decisions (acceptance coverage)

| Decision | Covered by | Status |
|---|---|---|
| "Add the uploader" (belongings photos via FileUploader, fileIds sent) | T-R4 | PASS |
| "Yes, diary photos too" | T-R18 | FAIL (the photo uploads and lists; the owner's View photo link 404s, row 76) |
| "Yes, allow it" (sign-in returns to /boarding) | T-R19 and the MSW booking spec | PASS |
| "Show before" (check-out preview) | T-R8, T-R15 | PASS |
| "Charge extra nights, never block" (late check-out) | T-R15, T-R16, T-R17 | PASS |

## T-X findings and gaps from the TEST gate (rows numbered from 76 for `docs/backlog/BRD.md`)

| # | Sev | Where | Item |
|---|---|---|---|
| 76 | **HIGH** | `web/lib/api/my-boarding.ts:371` | `fileUrl: (file?.url ...)` keeps the API's signed path (`/api/files/{id}/content?exp=..&disp=inline&sig=..`) as returned. `StayDiary.tsx:36` renders it as `href`, so the browser asks the WEB origin (404). `lib/api/files.ts` `resolveFileUrl` exists for exactly this ("the ONE place a signed file URL ... becomes something the browser can load"; cats and visits use it). Proven by T-R18b: href is `/api/files/b3864d53-...`; the same path on the API origin returned `200 image/png`. Fix: `fileUrl: resolveFileUrl(file?.url as string | undefined)`, then a wire test with an API-relative url. Without it the owner decision "Yes, diary photos too" gives the owner no photo |
| 77 | MEDIUM | `StayOverview`/`DepositSummary`/staff header; BE `deposit` for an Enquiry | The API sends `deposit.required 0.00`, `state NotRequired` for an Enquiry; the portal stay page prints 'Deposit ৳0.00 / No deposit needed / Nothing to pay up front' and 'Deposit received ৳0.00', and the staff header 'No deposit needed', for a booking whose deposit will normally be due once it is reserved. FE: for status Enquiry say the deposit is shown when you reserve (the UI-3 sentence already does). BE: send no deposit before reserving |
| 78 | MEDIUM (fixed in this gate) | `tests/components/boarding/test-gate.test.tsx` | The vitest tier did not pin: the reason requirement in the care-profile form (M2, both the check and the send), the reserved panel's null balance (M1c), the quote re-fetch on reload (M7), early/late from the server preview (M4), and the signature kept in the mutation cache or the check-in summary (M5b, M5c, M5d). Seven tests added; every mutant above now fails |
| 79 | LOW | `CheckOutWizard.tsx:84` | Clearing the check-out drafts after success is unobservable (the wizard swaps to the result panel): the mutant survives. Harmless defensive code; no test possible without exposing state |
| 80 | LOW | `IncidentForm.tsx:74-80` | The 'when did it happen' date/time, the 'Enter when it happened' and 'in the future' refusals have no test (the real run only used the default now) |
| 81 | LOW | tests | No test renders the `overriddenOn` timestamp (StayWorkspace.tsx:124) or the summary charge kind read from `lineRef` (rows 46, 74); the UnitOverstayed pin is the label constant only (the banner is pinned by the real-API LATE-RUN-2) |
| 82 | LOW | `boarding-real-api.spec.ts`, row 51 | The spec now needs a seeder with the shape documented in its header, plus two SQL helpers for the late and cycle cases. A committed seeder (the `SurgeryE2ESeed` precedent) is still owed; the throwaway one was deleted as instructed |
| 83 | LOW | E-X3 | NFR-PRF-3 (dashboard and task board, 20 loads, p95 <= 1.5 s) is still not measured: a dev server's compile time makes a number meaningless; needs `next build && next start` against the seeded API |
| 75 | LOW (still open) | `BookingFlow.tsx:148` | The re-quote still swallows its failure (T-G14) |
| 74 | LOW (still open) | `my-boarding.ts:521`, `staff-boarding.ts:627` | Summary charge kind and the `approvedIncrease` default (T-G13) |

Gaps left deliberately: E-A3 (family unit / four cats), E-A4 inbox notice, E-B2 lapse, E-D1 escalation, E-E1/E-E2/E-E4 through the UI on the real API, the 45-night stay's cycle invoice amount against the quote, and the staff dashboard polling timing (E-F2) on real data.
