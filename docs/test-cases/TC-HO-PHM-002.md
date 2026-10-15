# TC-HO-PHM-002 — Pharmacy and Prescriptions (frontend) — test cases and results

Derived from `docs/handover/HO-PHM-002.md` §15 (component cases U-*) and §16 (end-to-end cases E-*),
with the changes recorded in that handover's §1 Amended rows. Written **before** the build; the Status
column is filled in from what was observed. Mock (MSW) runs and real-API runs are recorded separately.

Owner decisions 2026-10-10 that shape the cases: the controlled-drug witness must be a vet or nurse and
not the dispenser; reception may dispense; an administrator may reverse a dispensing but cannot correct
the register (vet only); remote renewal (no open visit) is allowed.

## Summary

| Layer | Where | Result |
|---|---|---|
| Component / unit (Vitest) | `web/tests/components/{common,prescriptions,pharmacy,controlled-drugs,formulary,diet-authorisations,my-prescriptions}/**` | **98 cases in 14 files, all pass**; the whole web suite is 1323 tests in 204 files, all pass (`--maxWorkers=2`) |
| E-X1 axe, every §8.1 route | `web/tests/e2e/pharmacy-a11y-responsive.spec.ts` | pass on `chromium` (28 cases) and `mobile-360` (28 cases) |
| E-X2 no horizontal scroll at 360/768/1024/1440 | same | pass on `chromium` (101 cases) |
| E-A…E-F flows | `web/tests/e2e/pharmacy-flows.spec.ts` | 8 cases pass on MSW, `chromium` only (they read tables by row; 360 px has its own spec) — final combined run: 137 passed |
| Real API re-run (§5.1) | `web/tests/e2e/pharmacy-real-api.spec.ts`, fresh DB `catecommerce_phm002e2e`, API :5013, web :3116, `--workers=1` | **20 pass** on a freshly seeded database (18 pass + 2 deliberate expected-failure cases R-A2o, R-A6o that pin backlog PHM 50 and 51), see "Real API" below |
| `tsc --noEmit`, `eslint . --max-warnings 0`, `next build` | `web/` | clean / clean / passes |

Notes on stability: a cold `next dev` plus the in-browser mock can lose a first sign-in, and changing the
role of the mock's nurse account makes a page that is still open sign itself out; `pharmacy-helpers.ts`
(`openAs`, `gotoSigned`) changes the role from the quiet sign-in page and retries a sign-in that did not
take. Routes are warmed before the first test (`warmRoutes`) so a test is not racing a compile.

## Component cases

| ID | Case | File | Status |
|---|---|---|---|
| U-A1 | Composer without `visitId` shows the open-a-visit message and no form; `400 VisitNotOpen` shows the finalised message | `prescriptions/composer.test.tsx` | PASS |
| U-A2 | Typing a dose runs one safety check per 400 ms pause; the previous result stays visible while checking | same | PASS |
| U-A3 | Above-range assessment: "Above range" with mg/kg and mg, units visible; acknowledgement required; submit without it shows the field error | same | PASS |
| U-A4 | `Stale` weight: refusal with latest weight and date; Record weight opens a dialog; a new check runs after save | same | PASS |
| U-A5 | Allergy conflict first, labelled Blocking; reason required; tick without reason errors | same | PASS |
| U-A6 | Major and Minor interactions both shown with severity words; only Major needs a tick | same | PASS |
| U-A7 | Fingerprint changes after ticks: ticks cleared, live region says "Warnings changed — review again" | same | PASS |
| U-A8 | Submit `409 PrescriptionWarningsChanged`: panel replaced with the returned assessment, form values kept | same | PASS |
| U-A9 | Safety check request fails: "The safety check could not run…", issue blocked | same | PASS |
| U-A10 | `NotCheckable` shows the warning text, no tick required | same | PASS |
| U-A11 | ADR-FE-068 sentence and "No allergies recorded" information present | same | PASS |
| U-A12 | Unit select for a tablet medicine offers tablet and mass units only, no default selected | same | PASS |
| U-A13 | Remote renewal (owner decision 2026-10-10): renew page needs no visit, says so, and sends no `visitId` | `prescriptions/renew.test.tsx` | PASS |
| U-B1 | Prescription detail renders only the actions in `allowedActions` | `prescriptions/detail.test.tsx` | PASS |
| U-B2 | Safety record shows weight, dose/kg, range, acknowledger and the quoted reason | same | PASS |
| U-B3 | Cancel confirm names the number and that an open repeat request is declined; reason required | same | PASS |
| U-C1 | Plan with suggestion: suggested batches preselected, expired row disabled with its date as text | `pharmacy/dispense.test.tsx` | PASS |
| U-C2 | Only expired stock: blocking notice, no dispense control in the DOM | same | PASS |
| U-C3 | Choosing a later-expiring batch reveals a required reason field | same | PASS |
| U-C4 | Totals "8 of 10 ml selected"; submit refused until equal | same | PASS |
| U-C5 | Typed + Enter lot barcode selects the matching batch; unknown lot says so | same | PASS |
| U-C6 | Controlled dispense opens the witness dialog before any request; fields `autocomplete="off"` | `pharmacy/witness.test.tsx` | PASS |
| U-C7 | `WitnessNotVerified`: server message in the dialog, password cleared, identifier focused | same | PASS |
| U-C8 | Dialog cancelled / unmounted / success: both fields empty after; nothing in storage, URL or query cache | same | PASS |
| U-C9 | `MedicineLabel` has every US-PHM-05 AC1 field; print CSS hides app chrome | `pharmacy/label.test.tsx` | PASS |
| U-C10 | Print calls `window.print` then exactly one `POST …/label-prints`; re-render posts nothing | same | PASS |
| U-C11 | Reverse confirm names quantity, batch and supply; controlled adds the witness step; administrator sees Reverse | `pharmacy/dispensing-detail.test.tsx` | PASS |
| U-C12 | Witness dialog names "Vet or nurse" and says the witness must not be the person dispensing | `pharmacy/witness.test.tsx` | PASS |
| U-C13 | Queue: both sections, compact cat line with high-risk text, "Updated" time | `pharmacy/queue.test.tsx` | PASS |
| U-D1 | Register table has no edit or delete control anywhere | `controlled-drugs/register.test.tsx` | PASS |
| U-D2 | Correction chain: original unchanged with corrections beneath, each with its reason | same | PASS |
| U-D3 | Correction form with non-zero quantity: stock-adjustment note; confirm names both entry numbers; vet-only (no Add correction for an administrator) | same | PASS |
| U-D4 | Integrity broken: error text with the position, not colour alone | same | PASS |
| U-D5 | Register at 360 px renders cards | same | PASS |
| U-E1 | Per-kg basis without range: field errors on min/max | `formulary/formulary.test.tsx` | PASS |
| U-E2 | Strength preview "1.5 mg per 1 ml" | same | PASS |
| U-E3 | Controlled toggle: separate confirm; schedule required on; reason revealed on `400` | same | PASS |
| U-E4 | `409` on save: reload message; values kept | same | PASS |
| U-E5 | SKU picker: an already-linked SKU is disabled with the other medicine's name | same | PASS |
| U-F1 | Portal active list: medicine, dose, frequency, remaining days, issuing vet per row | `my-prescriptions/portal.test.tsx` | PASS |
| U-F2 | `canRequest: false` ended validity: server reason verbatim and Book a consultation to `/book?catId=` | same | PASS |
| U-F3 | `canRequest: true` request: dialog, success text, row shows Repeat requested | same | PASS |
| U-F4 | `?action=repeat` opens the dialog when allowed, not when refused | same | PASS |
| U-F5 | Authorised foods: active and expired rows with their texts | same | PASS |
| U-F6 | Portal never renders a batch or lot number | same | PASS |
| U-G1 | Diet form: needs visit, quick picks, `DietAuthorisationAlreadyActive` message with link; cat table Extend / Revoke (reason, confirm) | `diet-authorisations/diet.test.tsx` | PASS |
| U-X1 | Every clinical screen with fixtures: each dose/quantity/weight/range sits in a `[data-clinical-value]` element | `common/quantity.test.tsx` and the per-screen files | PASS — asserted on the register, detail, safety panel, dispense totals, label and portal rows; not a generic sweep of every screen |
| U-X2 | Server `400` maps to its field; `errorCode` never in the DOM | `formulary/formulary.test.tsx` (field), composer `VisitNotOpen` | PASS for the formulary form; the composer maps by the same helper |
| U-X3 | Dirty composer / correction / formulary form plus navigation: unsaved-changes confirm | `formulary/formulary.test.tsx` | PASS for the formulary form; the composer and the correction form use the same `UnsavedChangesGuard` and are exercised only by the e2e flows (which navigate after a save) |
| U-X4 | Queue poll while a dialog is open keeps the dialog values | `pharmacy/queue.test.tsx` | PASS |
| U-X5 | Repository scan: no dose arithmetic and no client-side FEFO or expiry logic in the slice | `common/clinical-logic-scan.test.ts` | PASS |

## Playwright on MSW (port 3116, `--workers=1`, chromium then mobile-360)

| ID | Case | Result |
|---|---|---|
| E-A1 | AC-4: vet prescribes above range (acknowledged), amoxicillin for the penicillin-allergic cat (blocking, reason), issues; receptionist dispenses from the queue with the FEFO batch and prints the label; owner sees it under Active | PASS |
| E-A2 | Receptionist opens the composer URL: permission-denied state (AC-18) | PASS |
| E-A3 | Stale weight, record weight from the panel, issue | PASS |
| E-B1 | Only expired stock: no dispense button, the server's message | PASS |
| E-B2 | Controlled drug with a nurse witness; then with a witness who is neither vet nor nurse refused (the mock has no receptionist sign-in; the administrator's stands in) | PASS |
| E-B3 | Reprint a label: print count 2 | PASS |
| E-C1 | Vet adds a correction: new entry linked beneath the original, original unchanged | PASS |
| E-D1 | Owner requests a repeat; staff dispense it from Repeat requests | PASS |
| E-D2 | Owner opens an expired repeatable prescription: reason and Book a consultation | PASS |
| E-E1 | Vet authorises a diet; owner sees it under Authorised foods | PASS |
| E-F1 | Administrator creates a per-kg medicine with an interaction, links a SKU, marks it controlled | PASS |
| E-X1 | axe on every route in §8.1 incl. safety panel with warnings, witness dialog, label view | PASS |
| E-X2 | Every route at 360 / 768 / 1024 / 1440: no horizontal scroll | PASS |

## Real API

TEST gate 2026-10-11. API from this worktree on :5013 (Development, `ConnectionStrings__CatEcommerce` = `catecommerce_phm002e2e`, sign-in and
witness rate limits raised by env only), web on :3116 with `NEXT_PUBLIC_USE_MOCKS=false`, Playwright `--workers=1`. The database was dropped, migrated
and seeded with dev-only data by `PharmacyE2ESeed` (integration-test project, a no-op unless `E2E_SEED_FILE` is set; uses real endpoints plus the
repository staff-seeding path) so the run is from clean state. The seed is single-use per database (R-F1 links a SKU; R-A2 leaves a prescription).
Watched on the final run: **20 passed (3.3 min)**. Mapping to the handover's E-cases:

| ID | Case | Status | Evidence |
|---|---|---|---|
| R-A1 / E-A1 | Vet prescribes meloxicam above range from the composer; server records `AboveRange`; POSTs carry cookie + `X-CSRF-Token` | PASS | observed |
| R-A2 / E-A1b | Amoxicillin for the penicillin-allergic cat: blocking, reason required; Major interaction shown on Mitu | PASS | observed |
| R-A2o | Allergy listed before the dose note | PASS (expected failure, `test.fail`) | server order is `[DoseNotCheckable, AllergyConflict]`; backlog PHM 50 |
| R-A3 / E-A3 | Stale weight, record from the panel, issue | PASS | observed |
| R-A4 / E-A2 | Reception: permission-denied page and 403 from the server for check and issue (nurse 403 too) | PASS | observed |
| R-A5 | Real 400/409 assessment bodies; composer replaces the panel on 409, form values kept | PASS | observed |
| R-A6 | Remote renewal finds the medicine in the real formulary and issues (`isRemoteRenewal`) | PASS | observed |
| R-A6o | First renewal submit is not refused 409 | PASS (expected failure, `test.fail`) | live check and renew endpoint disagree; backlog PHM 51 |
| R-B1 / E-B1 | Only expired stock: no dispense control, server message, direct post refused, stock unchanged | PASS | observed (stock read from the IMP stock matrix) |
| R-B2 / E-A1c, E-B3 | Reception dispenses FEFO batch, stock reduces by 10, label printed twice (print count 2 on the server) | PASS | observed |
| R-B3 | Later-expiring batch: button disabled until a reason is given; override recorded | PASS | observed |
| R-B4 / E-B2 | Controlled drug: self, administrator and wrong-password witnesses refused with one generic message; vet witness accepted; nothing kept in storage/URL; register entry exists; nurse self-witness refused server-side | PASS | observed |
| R-B5 / E-C1 | Register read-only; administrator cannot correct (no control, 403); vet adds a witnessed correction; original unchanged, one corrected-by link | PASS | observed |
| R-B6 / E-B4 | Administrator reverses (stock to quarantine, +10); reception cannot (403); controlled reversal needs a witness | PASS | observed |
| R-D1 / E-D1 | Owner sees the prescription (no batch/lot), requests a repeat (customer cookie + CSRF), staff dispense it from Repeat requests | PASS | observed; the "ready" inbox message needs the worker, recorded as an annotation, not asserted |
| R-D2 / E-D2 | Repeats exhausted: reason and Book a consultation `/book?catId=` | PASS | observed |
| R-D3 / E-E1 | Vet authorises a diet; owner sees it under Authorised foods | PASS | observed |
| R-F1 / E-F1 | Administrator creates a per-kg medicine with interaction, links a SKU, marks it controlled; prescribable | PASS | observed |
| R-F2 | Concurrent edit: `409 StaleVersion` message, typed values kept | PASS | observed |
| R-X1 | Session cookie HttpOnly + SameSite=Lax, CSRF cookie readable; every new write refuses missing/wrong token | PASS | observed |

Not covered by the real run (still open): the notification worker's "ready to collect" message, the seal worker, refill reminders and time advance
(workers are not started by this run); printing on paper.

## Static checks

| Check | Result |
|---|---|
| `npx tsc --noEmit` | clean |
| `npx eslint . --max-warnings 0` | clean |
| `npx vitest run --maxWorkers=2` (whole suite) | 204 files, 1323 tests, all pass |
| `npx next build` | passes |

TEST gate 2026-10-11 re-run: tsc clean, eslint clean, `next build` passes, vitest 204 files / 1323 tests: 1321 pass, 2 fail under the parallel run (inventory timing tests, not pharmacy) and both pass alone (backlog PHM 52); Playwright on MSW: chromium 137 passed (20 skipped = the real-API spec), mobile-360 a11y/responsive 129 passed (one cold sign-in flake passed on rerun).

## Not verified

- The rendered result by eye (this build could not be looked at): the composer's two-column layout from
  1024 px, the label at print size, the warm customer layer on the portal screens.
- Printing: `window.print()` is stubbed in headless runs, so the label's `@media print` rules were checked
  as text and by the label-width test, not on paper.
- Workers (notification, pharmacy seal, refill reminders) against the real API.
