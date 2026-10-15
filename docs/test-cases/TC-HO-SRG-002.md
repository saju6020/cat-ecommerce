# TC-HO-SRG-002 — Surgery: Spay, Neuter and Procedures (frontend) — test cases and results

Derived from `docs/handover/HO-SRG-002.md` §15 (component cases U-*) and §16 (end-to-end cases E-*), with the
changes recorded in that handover's §1 Amended row of 2026-10-14. Mock (MSW) runs and real-API runs are recorded
separately. The mock plays the server (`web/mocks/handlers/surgeries/**`): clashes, the checklist, the start gate,
the log and the report rules are decided there and nowhere in the app.

## Summary

| Layer | Where | Result |
|---|---|---|
| Component / unit (Vitest) | `web/tests/components/{surgery,anaesthesia,surgical-report,recovery,surgery-campaigns}/**`, `web/tests/unit/surgery-format-and-validation.test.ts` | **123 cases in 15 files, all pass** (the combined re-check fix added `surgical-report/remount.test.tsx`, which remounts the report tab in a cache-retaining client against a mock that enforces `expectedVersion`, plus a lost-response resend and a services-error case in `gatefix.test.tsx`) (the gate-fix round added `surgery/gatefix.test.tsx`: flush-on-unmount with fake timers, booking clears, idempotency fingerprint, midnight, reschedule state, strict prices, encoded ids, last vitals, day-list groups); the whole web suite is **1636 tests in 234 files; 1632 pass and 4 inventory tests failed once under load and pass alone** (`goods-receipt-form`, `shared-pieces`; not Surgery) (`--maxWorkers=2`) |
| E-X2 axe, every §8.1 route and the open dialogs | `web/tests/e2e/surgery-a11y-responsive.spec.ts` | pass on `chromium` (24 cases) and `mobile-360` (24 cases) |
| E-X3 no horizontal scroll at 360/768/1024/1440 | same | pass on `chromium` (20 cases) |
| E-A, E-B, E-C, E-D, E-E, E-F flows and E-X1 (AC-3) | `web/tests/e2e/surgery-booking.spec.ts`, `surgery-flow.spec.ts`, `surgery-owner.spec.ts` | **11 cases pass** on MSW, `chromium` only |
| Real API re-run (§5.1) | `web/tests/e2e/surgery-real-api.spec.ts` | **Done by the TEST gate 2026-10-14: 13 specs pass in one run on a fresh database** — see "TEST gate sheet" below. The text under "Real API" is the build's earlier statement and is superseded |
| `tsc --noEmit`, `eslint . --max-warnings 0`, `next build` | `web/` | clean / clean / passes |

Run with `PORT=3112`, `--workers=1`; no database was used (MSW). Stability: a cold `next dev` plus the in-browser
mock can lose a first sign-in; `surgery-helpers.ts` (`openAs`, `gotoSigned`) retries a sign-in that did not take.
Routes are warmed before the first axe case (`warmRoutes`).

## Component cases

| ID | Case | File | Status |
|---|---|---|---|
| U-A1 | Assistant list after choosing a surgeon: the surgeon is absent | `surgery/booking.test.tsx` | PASS |
| U-A2 | `BusyExplainer` lists each resource's busy times with the server's labels | same | PASS |
| U-A3 | Booking `400` with theatre, surgeon and assistant clashes: all three beside their fields and in the summary, focus on the summary | same | PASS |
| U-A4 | Booking `409`: race message, availability refetched | same | PASS |
| U-A5 | `CatAlreadyNeutered` → *Book anyway* resubmits with `confirmNeuterStatusDiscrepancy: true` | same | PASS |
| U-A6 | Campaign chosen without an eligibility note: field error, nothing posted | same | PASS |
| U-A7 | Price "for information — paid at the clinic"; no payment, deposit or card control | same | PASS |
| U-B1 | Day list row: *"3 outstanding: weight, consent, ASA grade"* as text; flags as words | `surgery/preop.test.tsx` | PASS |
| U-B2 | Checklist actions by role (nurse, vet, reception); Waive on blood work only, vets only, needs a reason | same | PASS (4 cases) |
| U-B3 | `StartPanel` with consent the only blocker: nurse sees no override, vet does; with the checklist also incomplete, no override for a vet either | same | PASS (3 cases) |
| U-B4 | Override reason: 9 characters keeps Continue disabled, counter shows the minimum; the final confirm names the vet and says "recorded permanently" | same | PASS |
| U-B5 | Start enabled only when `allowedTransitions` has `Start` and `startBlockers` is empty | same | PASS |
| U-B6 | A refetch (new object) while the override dialog has text: text kept | same | PASS |
| U-C1 | Timeline ordered by time; corrected entry struck through, labelled *Corrected*, linked both ways, with who/when/why | `anaesthesia/timeline.test.tsx` | PASS (2 cases) |
| U-C2 | No edit or delete control on any entry; **Correct…** only on entries not already corrected | same | PASS |
| U-C3 | Controlled drug: the primary button waits for the wastage and a witness; nothing is posted before the witness confirms (see §1 Amended: the witness goes in the same request) | `anaesthesia/drug.test.tsx` | PASS |
| U-C4 | Witness credential: not in the DOM, query cache, mutation cache, URL or storage after saving; cleared on cancel | same | PASS (2 cases) |
| U-C5 | Units on every dose, quantity, vital and weight, in entry and display; mg/kg labelled with weight and date | `anaesthesia/timeline.test.tsx`, `drug.test.tsx`, `unit/surgery-format-and-validation.test.ts` | PASS |
| U-C6 | `ImplausibleVitalValue`: the named field is marked with the server's message, *Save anyway* resends with the flag | `anaesthesia/drug.test.tsx` | PASS |
| U-C7 | `BatchNotUsable` shown by the batch with other values kept; a witness refusal stays in the witness dialog and clears the password | same | PASS (2 cases) |
| U-C8 | Correction: reason 10 to 500 characters; confirm says the original stays; prefilled with units; controlled drug needs a new witness | `anaesthesia/correct.test.tsx` | PASS (3 cases) |
| U-D1 | Report autosaves 5 s after the last change; a `409` shows a reload prompt and keeps the local text for copying | `surgical-report/report.test.tsx` | PASS (2 cases) |
| U-D2 | Finalised report: no editor, text + addenda, *Add addendum* for vets only | same | PASS (3 cases) |
| U-D3 | `ReportIncomplete`: missing parts listed (parsed from the one violation's message), the first focused | same | PASS |
| U-D4 | `neuterStatusApplied` true / false / not applicable: the notice, the repair action | same | PASS (3 cases) |
| U-E1 | Discharge disabled until *Ready for discharge*, with the reason in words; the confirm states what happens | `recovery/recovery.test.tsx` | PASS (2 cases) |
| U-E2 | Follow-up date outside the draft's window: field error before submit; the server's message on a `400`; suture removal can be dropped | same | PASS (3 cases) |
| U-E3 | Recovery board ordered Ready → Recovering → Stable; status as text with since/by; `tel:` link; empty state | same | PASS (2 cases) |
| U-E4 | Post-op sheet: instructions as text, follow-ups, contact; a print-only stylesheet hides everything else | same | PASS |
| U-F1 | Owner page before / during / after; another customer's id is a `404` | same | PASS (4 cases) |
| U-G1 | Workspace: default tab by status, URL hash chooses a tab, stepper in words, Reschedule/Cancel only when allowed and permitted, cancel confirm names what is affected and the owner, reception sees no clinical control | `surgery/workspace.test.tsx` | PASS (17 cases) |
| U-G2 | Reschedule: prefilled, availability asked with `excludeSurgeryId`, old → new confirm, `expectedVersion`, stale version keeps the dialog | `surgery/reschedule.test.tsx` | PASS (4 cases) |
| U-G3 | Protocols: *Using defaults*, window rule in days, no Edit without the action | `surgery/protocols.test.tsx` | PASS (4 cases) |
| U-G4 | Campaigns: list, editor, locked prices read-only, `CampaignPriceLocked` as a sentence, duplicate code, cases and server totals | `surgery-campaigns/campaigns.test.tsx` | PASS (6 cases) |
| U-X1 | Server `400`/`409`/`403` mapped to fields; `errorCode` never in the DOM | `unit/surgery-format-and-validation.test.ts`, the cases above | PASS |
| U-X2 | Dirty discharge form + a link: unsaved-changes confirm | `recovery/recovery.test.tsx` | PASS |
| U-X3 | Calendar surgery block: opens the surgery page, says *Surgery*, shows in the assistant's column, no *Move…*, not draggable | `surgery/calendar-block.test.tsx` | PASS (2 cases) |

## End-to-end cases (MSW)

| ID | Case | File | Status |
|---|---|---|---|
| E-A1 | Reception books a spay choosing surgeon, assistant and theatre; the surgery page opens | `surgery-booking.spec.ts` | PASS (the calendar block is real-API only) |
| E-A2 | Assistant busy with a vaccination: the busy interval is explained, the clashing starts are absent, another start books | same | PASS |
| E-A3 | A lost race (the mock's one-off `409` at 16:30): explained, and the time is gone after the refresh | same | PASS (two real browser contexts: real API only) |
| — | A day with no common start offers the next free day | same | PASS |
| E-B1 | Day list names the outstanding items; weight and consent recorded elsewhere; the items clear | `surgery-flow.spec.ts` (E-X1) | PASS |
| E-B2 | Start without consent: blocked with the consent blocker; a nurse has no override | `surgery-flow.spec.ts` | PASS |
| E-B3 | Vet starts with an override and a reason; the banner names the vet and the reason | same | PASS |
| E-B4 | Reception sees no start, drug, report or override control; a direct write is `403` | same | PASS (mock `403`; real API run pending) |
| E-C1 | Milestone, vitals, a controlled drug with a witness (second sign-in typed in the dialog): the timeline shows units and the witness | `surgery-flow.spec.ts` (E-X1) | PASS |
| E-C2 | Correct the controlled entry: original struck with *Corrected*, correction linked, a new witness required | same | PASS |
| E-D1 | End a spay *Completed*; neuter status set; finalise the report; no editor; an addendum | same | PASS |
| E-E1 | Recovery status set; the front desk's Recovery tab shows the card | same | PASS (the 30 s poll itself is covered by the hook tests) |
| E-E2 | Discharge with defaults; post-op sheet and Print; the owner's pages | `surgery-flow.spec.ts`, `surgery-owner.spec.ts` | PASS (the inbox message is real API only) |
| E-F1 | Administrator creates a campaign; reception books under it; the cases and totals list it | `surgery-flow.spec.ts` | PASS |
| E-X1 | AC-3: the whole spay through the UI in one run | same | PASS |
| E-X2 | axe on every §8.1 route and on the open dialogs (override, drug + witness, vitals, correction, discharge, cancel) | `surgery-a11y-responsive.spec.ts` | PASS, `chromium` and `mobile-360` |
| E-X3 | Every route at 360 / 768 / 1024 / 1440: no horizontal scroll | same | PASS |

Findings fixed during the runs: the stepper's future steps used `ink-faint` (3.65:1) — now `ink-muted`; the protocol and
campaign *Edit …* buttons overflowed 360 px — the visible label is now "Edit" with the procedure/code in the
accessible name; the booking URL lost quick successive choices (a late `router.replace` overwrote an earlier one) — now
merged the way `useFilterUrlUpdate` does; `isLoading` on a branch-gated query is false while the query waits for the
branch, which flashed the editor and an error state — pages now wait on `isPending`.

## Real API

> Superseded by the TEST gate sheet below (2026-10-14): the real-API run was done.

**Not run (at build time).** The §5.1 re-run needs a database seeded with a branch, two theatres, priced theatre services with
protocols, vets and nurses with rosters, a customer with an intact cat, consent forms, a formulary medicine with
clinic stock and four staff accounts. No surgery seeder exists (Pharmacy has `PharmacyE2ESeed.cs`), and a seeder lives
in `tests/**`, which HO-SRG-002 §14 forbids. Nothing was run against `catecommerce_dev` or `catecommerce_test`. The
cases that need it: E-A1's calendar block, E-A3 with two real browser contexts, E-B4's `403`, E-E2's inbox message,
and every flow once against the real exclusion constraints.
---

# TEST gate sheet (2026-10-14)

Handover: docs/handover/HO-SRG-002.md  ·  Commit under test: 5306904c (branch ho/SRG-002)  ·  Written: 2026-10-14 (every row NOT RUN before the first run)  ·  Executed: 2026-10-14

> Everything above this line is the BUILD's own record and is not re-stated here. This section is the TEST gate's sheet. Statuses record what was **observed**; `BLOCKED` is a case that could not be run. IDs were assigned by the gate (the handover's U-/E- rows keep their IDs; the real-API rows are `RA-*`, the suites `S*`/`M*`, mutation spot-checks `MU*`, regression rows `R*`). Owner decision recorded: DrugBatchChooser (ARCHITECTURE M1) is "Accept it". The handover's U-* component cases are not re-listed: they are the 123 build cases, and S3 re-ran them all (green).

## Summary

| | Count |
|---|---|
| Total | 49 |
| PASS | 45 |
| FAIL | 3 |
| BLOCKED | 1 |
| NOT RUN | 0 |

The 3 FAIL rows are: M4 (the whole-spay flow spec cannot drive the 360 px layout, a spec limitation) and MU4 / MU5 (two mutants SURVIVED the build's own tests; the gate wrote `gate-strengthening.test.tsx`, which kills both). None is a production defect. The 1 BLOCKED row is the 30 s refresh itself.

## Environment

- Branch `ho/SRG-002` at 5306904c, worktree `D:/my-repo/cat_ecommerce-wt/SRG-002`. A temporary `node_modules` junction to the main checkout (lockfiles identical) and a copy of `.env.local.example` were used and removed.
- Unit and static runs on `next dev`/MSW: node 20, `vitest --maxWorkers=2`, Playwright `--workers=1`, PORT 3112, one project at a time.
- Real API: `CatEcommerce.Api` (Development) on `http://localhost:5112` against the throwaway database `catecommerce_srg002e2e` (never `catecommerce_dev` or `catecommerce_test`), web `next dev` on 3112 with `NEXT_PUBLIC_USE_MOCKS=false`. The DB was created and migrated twice from this branch's migrations (91 migrations applied each time, which equals master's), the second time fresh so the full spec ran from empty in ONE invocation. Dropped at the end.
- API settings the run needed (environment, not product): `RateLimiting:SignIn:PermitLimit` raised (10 a minute is exhausted by the spec's own sessions), `PublicSite:WebBaseUrl` and `Account:WebBaseUrl` set to an https URL (an http base makes the notification renderer refuse every surgery message, see backlog SRG 85), CORS for 3112.
- Seeder: a throwaway xUnit fact (built on `SurgeryIntegrationTestBase`/`PharmacyE2ESeed`) seeded a branch with a Theatre room, six named staff (admin, two vets, nurse, rostered assistant, reception), the world's own vet/assistant, a customer, a spay/neuter/dental service set with prices, a spay protocol (wound check + suture removal), both consent forms, a controlled drug (ketamine, two batches, FEFO) and a non-controlled drug with a batch. Two more throwaway facts moved one surgery's `admitted_on` back (RA-N8's only direct write) and dropped the DB. All three were deleted from the repo afterwards.
- Hosts started: API (5112) and web (3112) only. Not started: the courier, payment and SMS/email providers (channels are not configured; in-app delivery is what was observed).

## Suites (static + unit + MSW)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| S1 | `tsc --noEmit` in web/ | clean | PASS | exit 0, no output (re-run after the gate's additions: clean) |
| S2 | `eslint . --max-warnings 0` | clean | PASS | exit 0, no output (the two files the gate added: clean) |
| S3 | `vitest run --maxWorkers=2`, full web suite | all pass; any red rerun alone | PASS | 235 files, 1643 tests, 0 failed, 586 s. The known inventory flakes did not fail in this run, so no rerun was needed. The gate's 2 added tests pass alone |
| S4 | `next build` | passes | PASS | exit 0, route table printed |
| M1 | Playwright MSW, chromium, `surgery-booking` + `surgery-flow` + `surgery-owner` | all pass | PASS | 11 passed (1.4 m) |
| M2 | Playwright MSW, chromium, `surgery-a11y-responsive` (E-X2 axe + E-X3 responsive) | all pass | PASS | 44 passed (24 axe + 20 responsive) |
| M3 | Playwright MSW, mobile-360, `surgery-a11y-responsive` | all pass | PASS | first run 43 passed, 1 failed ("could not sign in as Nurse", the documented cold-start MSW sign-in race, form still showing); that case rerun alone passed (2 passed). Reported as PASS after the rerun |
| M4 | Playwright MSW, mobile-360, the three flow specs | all pass | FAIL | 10 passed, 1 failed: `E-X1 / AC-3` waits for `getByRole('tab', { name: 'Anaesthesia' })`, but at 360 px the workspace shows a "Section" select (UI-1, by design; the snapshot shows the select with Anaesthesia selected). A spec limitation, not a product defect; the build's record says the flow cases are chromium only. Backlog SRG 86 |

## Real-API set-up

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| RA-0a | Create `catecommerce_srg002e2e`, migrate from this branch | DB at head | PASS | `dotnet ef database update --connection` applied 91 migrations from empty, twice; last `AttachRetentionGuardsToReportingTables` |
| RA-0b | Throwaway seeder | seed JSON written | PASS | xUnit fact passed (17 s), JSON written with branch, theatre, service, customer, drug and batch ids |
| RA-0c | API on 5112 against that DB only | answers | PASS | staff sign-in answered 200 on 5112; the connection string was the env var for `catecommerce_srg002e2e` |

## Real-API cases (handover §16, then the extra asks)

All 13 specs of `web/tests/e2e/surgery-real-api.spec.ts` passed in ONE invocation on the fresh database (13 passed, 2.8 m, chromium, `--workers=1`). Rows were also first observed in smaller runs while the spec was being built; the final single run is the record.

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| RA-A1 | E-A1: reception books a spay choosing surgeon, assistant, theatre; calendar block | Surgery page; calendar block for the surgeon, the assistant and the theatre, labelled Surgery, links to the page, no Move | PASS | URL became `/staff/surgeries/<id>`, heading names the cat, stepper says Scheduled; `/staff/appointments?date=` showed >= 2 links to that surgery ("Surgery · ... · Booked", "with Tanvir Assistant"), `view=room` showed the theatre's block, no Move button |
| RA-A2 | E-A2: busy resources explained, another start books | explainer names them; the clashing start is absent | PASS | Explainer read "THEATRE 18:00-19:00 Room ... is booked", "SURGEON ... Rahman Vet is operating on another surgery", "ASSISTANT ... Tanvir Assistant is assisting another surgery"; the booked start was absent from the starts; the first offered start booked. Deviation: the assistant was busy with another SURGERY, not a vaccination |
| RA-A3 | E-A3: two real browser contexts book the same theatre and time | one wins; the other is told | PASS | Both confirmed in the same instant; exactly one reached the surgery page; the other got a real `400 SurgeonBusy` ("just been booked elsewhere") from the exclusion constraint, shown as "Someone has just booked that time. Availability has been refreshed.", and the start was gone from the refreshed starts. Side finding: two staff sign-ins of ONE account at the same instant gave `409` (backlog SRG 87) |
| RA-B1 | E-B1: day list names the outstanding items; they clear after weight and consent | as stated | PASS | Day list row read "5 outstanding: fasting instructions, blood work, weight, consent, ASA grade"; after fasting/waive in the UI and weight + both consents through the API, the page showed "Everything is in place to start." (inside RA-X1) |
| RA-B2 | E-B2: admitted, no consent | Start disabled, consent blocker, no override for a nurse | PASS | Start disabled, "Consent has not been recorded", no override button for the nurse; a direct nurse override call was `403` |
| RA-B3 | E-B3: vet override with a reason | In theatre; banner names vet + reason | PASS | 9 characters kept Continue disabled; banner "Started without recorded consent - emergency override by Rahman Vet at hh:mm: Cat collapsing, owner unreachable"; API detail status `InTheatre` |
| RA-B4 | E-B4: reception sees no clinical write controls; real 403s | none of the buttons; each write 403 | PASS | no Add drug/vitals/milestone, Start, override, End procedure; 403 on milestone, vitals, drug, end-procedure, report PUT, override (reception) and drug (nurse); the log read stayed 200 |
| RA-C1 | E-C1: milestone, vitals, controlled drug with a witness | ordered, units, names vet + witness | PASS | "120 bpm", "38.6 C", "Controlled - witnessed by Akter Nurse"; log values show batch E2E-KET-NEAR, drawn 1, wasted 0.5, Vial, register entry id (RA-N4 and RA-X1) |
| RA-C2 | E-C2: correct the controlled entry, new witness | original struck + Corrected, linked | PASS | witness dialog appeared again, "Corrected" and "See the correction" shown, no edit/delete buttons (inside RA-X1) |
| RA-D1 | E-D1: end Completed, finalise, edit attempt, addendum | cat Neutered; read-only; addendum | PASS | `GET /api/cats/{id}` neuterStatus `Neutered`; "Finalised by Rahman Vet on"; no Findings field; a direct PUT after finalising answered `409 ReportNotEditable`; addendum listed |
| RA-E1 | E-E1: recovery status, front desk Recovery tab | card with status, since, by | PASS | "Now: Ready for discharge"; the front-desk card showed "Ready for discharge" and "since hh:mm - set by ..." |
| RA-E1b | E-E1: the card updates on the next 30 s poll with no reload | updates | BLOCKED | not executed: the environment was already torn down when the gap was noticed; the card was only observed on navigation. The poll is covered by the build's hook tests only. Unblock: hold the front desk open, change the status from a second session, wait 35 s |
| RA-E2 | E-E2: discharge with the protocol, print sheet, owner pages, inbox | instructions + 2 follow-ups + Book follow-up + inbox message | PASS | post-op sheet contained the protocol text; the owner's page showed "Keep her warm and quiet", "Wound check", "Suture removal", a `Book follow-up` link starting `/book?`, no pay control; the owner's inbox (read through `GET /api/customers/me/inbox`) held "Ready to go home" naming the cat and "Follow-ups: Wound check 17 Oct; Suture removal 24 Oct." |
| RA-F1 | E-F1: campaign, booking under it, cases + totals | campaign price on the surgery; totals | PASS | admin saved the campaign; booking required the eligibility note; the surgery said "Campaign price, for information"; the cases page listed the cat and the totals read "1 case ... 500 ... campaign" |
| RA-X1 | E-X1 / AC-3: the whole spay in one UI run against the real constraints | completes | PASS | one test: book, day list, admit, checklist, start, milestone, vitals, controlled drug + witness, correction + witness, end, report, finalise, addendum, recovery, front desk, discharge, sheet, owner page, inbox. 25 s |
| RA-N1 | Report autosave: type, wait, reload | text + version survive | PASS | "Draft saved hh:mm" shown, server version 1, text present after reload |
| RA-N2 | Autosave, tab switch inside 5 s, return BEFORE the flush answered (N2) | typed text shown, next save carries the new version, no conflict | PASS | the flush PUT was held 4 s by the test; on return "Loading the report..." was visible, then the editor held the typed technique; server version rose 1 to 2; the next edit saved ("Draft saved") with no "Someone else changed" banner; the server had all three fields |
| RA-N3 | Autosave when someone else saved first | StaleVersion prompt, local text kept | PASS | second vet's PUT answered 200 (version 4); the next autosave was `409 StaleVersion`; banner "Someone else changed this report, so your changes were not saved."; "Your text, for copying" held the typed text; the server still had the other vet's text; Reload showed it |
| RA-N4 | Drug entry with a correct witness | one log row, one deduction, register rows, names the witness | PASS | one Drug entry; ketamine NEAR batch on hand fell by exactly 1 (drawn 1); register gained 2 rows for the one entry (Administered 0.5 + Wastage 0.5); "witnessed by Akter Nurse" |
| RA-N5 | Drug entry with a wrong witness password | refused in the dialog, nothing written | PASS | real `400 WitnessNotVerified`, message shown in the witness dialog, password field cleared; log entries, stock and register unchanged |
| RA-N6 | Lost response, retried | same `entryId`, one entry, one deduction, one register set | PASS | the test let the POST reach the server (201) and aborted the response; the UI showed "Something went wrong. Please try again."; the retry re-sent the SAME `entryId` (compared in the two captured bodies); afterwards: +1 Drug entry, stock -2 once (drawn 2), register +2 rows once |
| RA-N7 | Same `entryId`, changed dose, via the API | `AnaesthesiaEntryIdMismatch`, nothing written; identical replay writes nothing | PASS | `409 AnaesthesiaEntryIdMismatch`; the identical body replayed `201` with the same entry id; log, stock and register unchanged by both |
| RA-N8 | Correction across midnight | lands minutes after the original, not a day away | PASS | one SQL write moved `admitted_on` back; a milestone was logged at 2026-10-12T17:55Z (23:55 Dhaka); the UI correction typed 00:05; the log's Correction entry has `occurredOn` 2026-10-12T18:05:00Z, exactly 10 minutes after the original |

## Mutation spot-checks (production file edited, tests run, file restored)

Each mutation was applied with a script, the named test files run with `--maxWorkers=1`, then `git checkout -- lib components`.

| ID | Mutation | Expected | Status | Evidence |
|---|---|---|---|---|
| MU1 | autosave unmount flush removed | a test fails | PASS | 3 failed: `gatefix` "flushes an unsaved change when the editor unmounts" and both `remount` N2 cases |
| MU2 | `setQueryData` patch on save removed | a test fails | PASS | 1 failed: `remount` N1 "shows the saved text and saves with the right expectedVersion" |
| MU3 | the in-flight wait removed (`pendingReportSave` returns null) | a test fails | PASS | 2 failed: both `remount` N2 cases |
| MU4 | `entryId` fingerprint widened (route, wastage added) | a test fails | FAIL | SURVIVED the build's tests (20/20 `gatefix` pass; the resend test's route edit sits behind `if (route.options.length > 2)`, and the Meloxicam mock has one route, so the route was never edited). Closed by the gate: `gate-strengthening.test.tsx` uses a multi-route medicine, asserts the route changed, and fails under this mutant. Backlog SRG 83 |
| MU5 | `instantFromTime`'s reference changed at the correction call site (NOW instead of the entry) | a test fails | FAIL | SURVIVED (20/20 pass): `instantFromTime`'s unit test passes the reference itself, so the call site was untested. Closed by the gate: `gate-strengthening.test.tsx` (23:55 on a past day corrected to 00:05 must POST the next day of the ENTRY) fails under this mutant. Also caught by RA-N8. Backlog SRG 83 |
| MU6 | `encodeURIComponent` removed from `lib/api/surgeries.ts` | a test fails | PASS | 1 failed: `gatefix` "a crafted id cannot change the API path" |
| MU7 | strict price regex loosened | a test fails | PASS | 3 failed: `gatefix` refuses "1e3", "800.505", "-5" |
| MU-clean | `git status` clean after the mutations | no changes | PASS | `git status` showed only the TC doc and the gate's new files |

## Regression cases from earlier gates

| ID | Gate | Finding | Case | Status | Evidence |
|---|---|---|---|---|---|
| R1 | ARCH H1 | other-area hook files reformatted | the diff 5a187b6b..5306904c touches none of the six hook files; `errors.ts` is append-only; no `src/**`, `tests/**`, config file touched | PASS | `git diff --stat` on the six paths empty; 0 removed lines in `errors.ts`; the name-only filter for `src/ tests/ .ai/ .claude/ package.json tsconfig next.config eslint.config` empty |
| R2 | CODE H1 / re-check R1, N1, N2 | report text lost on a tab switch / stale cache | MU1-MU3 plus RA-N2 | PASS | see those rows |
| R3 | CODE M / re-check R3 | double deduction after a lost response | MU4 plus RA-N6 | PASS | RA-N6 PASS on the real API (the real guarantee); MU4 closed by the added test |
| R4 | CODE L3 / re-check R6 | correction across midnight | MU5 plus RA-N8 | PASS | RA-N8 PASS on the real API; MU5 closed by the added test |
| R5 | SEC L1 | unencoded path ids | MU6 | PASS | killed by `gatefix` |
| R6 | CODE M5 | campaign price accepts blank / `1e3` / 3 decimals | MU7 | PASS | killed by `gatefix` |
| R7 | ARCH M6 | real-API E-series was not run | the RA-* set | PASS | 13 specs, one invocation, fresh DB |

## Acceptance-criteria coverage (handover §18)

| Criterion | Covered by | Status |
|---|---|---|
| A1 SRG-01 | U-A1..U-A4 (S3), RA-A1, RA-A2, RA-A3 | PASS |
| A2 SRG-02 | U-B1, U-B2 (S3), RA-B1 | PASS |
| A3 SRG-03 | U-B3..U-B6 (S3), RA-B2, RA-B3 | PASS |
| A4 SRG-04 | U-C1, U-C5, U-C6 (S3), RA-C1 | PASS |
| A5 SRG-05 | U-D1..U-D3 (S3), RA-D1, RA-N1..N3 | PASS |
| A6 SRG-06 | U-D4 (S3), RA-D1 | PASS |
| A7 SRG-07 | U-E1, U-E2, U-E4, U-F1 (S3), RA-E2 | PASS |
| A8 SRG-08 | U-C2, U-C3, U-C4, U-C7 (S3), RA-C1, RA-C2, RA-N4..N7 | PASS |
| A9 SRG-09 | deferred to HO-BRD-002 (§4); not tested | n/a (recorded as deferred, not passed) |
| A10 SRG-10 | U-E3 (S3), RA-E1 (card), RA-E1b (the 30 s refresh) | PARTIAL: the card is real-API verified; the refresh itself is BLOCKED |
| A11 SRG-11 | U-A6 (S3), RA-F1 | PASS |
| A12 ADR-170 | U-A7 (S3); RA-E2 and RA-F1 saw no payment control | PASS |
| A13 AC-3 / AC-18 | RA-X1, RA-B4 | PASS |
| A14 UI-6 / UI-1 / UI-8 | M2, M3 (after the rerun), U-C5; the full flow at 360 px is NOT exercised (M4) | PASS for axe and scroll; the whole-flow-at-360 gap is SRG 86 |

## Cases I could not run

| ID | Why | What would unblock it |
|---|---|---|
| RA-E1b | the 30 s refresh was not observed | hold the front desk open in one session, change the status from another, wait 35 s |

## Gaps left deliberately

| Gap | Why not covered | Suggested owner |
|---|---|---|
| The whole spay flow at 360 px (M4) | the E-X1 spec selects tabs by role; the narrow layout is a select | SRG-002 follow-up (SRG 86) |
| A vaccination (rather than a surgery) as the assistant's clash (E-A2) | needs an appointment booked for a nurse; the clash display was verified with a surgery | HO-APT/SRG follow-up, low value |
| Weight and consent recorded through their own screens (E-B1) | both are other areas' pages; they were recorded through their real endpoints | EMR-002 / CAT-002 e2e |
| Workers: SMS and email delivery; the pre-op reminder and follow-up-due sweeps | no provider is configured; only in-app delivery was observed | NTF |
| Mobile-360 and responsive runs against the real API | the real-API spec runs on chromium only | SRG-002 follow-up |
