GATE: ACCEPTANCE
HANDOVER: HO-BIL-001 (Phase J only -- automatic billing from finalised visits, dispensing and surgery)
COMMITS: 081197e6 10c74551 10faf707 f946b7af 663e05c5 2409bedc 3f57bb5d ae926317 5c67ab4c (gate records 21894ec9 7eed9141 4118ebb0 3db400f9); branch ho/BIL-001-J, range 5a187b6b..5c67ab4c, 69 files
DATE: 2026-10-14
STATUS: PASS

Owner rule (2026-09-27): only BLOCKER and HIGH fail a gate. No BLOCKER or HIGH found. Six LOW findings are proposed as
`docs/backlog/BIL.md` rows 100 to 105 (the backlog stands at 99). This gate edits nothing but this file.

## Prior gates (from docs/gates and docs/test-cases, not from commit messages)

| Gate | Artifact | Status |
|---|---|---|
| ARCHITECTURE | `HO-BIL-001-J-ARCHITECTURE.md` | FAIL (1 BLOCKER, 1 HIGH) at 663e05c5, then **PASS** in the combined re-check (same file, section "Combined re-check", 3db400f9, fix range 663e05c5..3f57bb5d) |
| SECURITY | `HO-BIL-001-J-SECURITY.md` | **PASS** (1 MEDIUM, 4 LOW; all now Resolved or Open-LOW in rows 77, 85-88) |
| CODE_REVIEW | `HO-BIL-001-J-CODE_REVIEW.md` | FAIL (1 HIGH). The HIGH is closed by the combined re-check, which is headed "ARCHITECTURE + SECURITY + CODE_REVIEW findings" and is PASS (gate-scope rule: follow-ups get one combined review). See LOW 6: the CODE_REVIEW file itself still reads FAIL with no pointer |
| TEST | `HO-BIL-001-J-TEST.md`, `TC-HO-BIL-001.md` section "Phase J TEST gate" | **PASS** at ae926317 + its own test files (= 5c67ab4c). 49 rows: 47 PASS, 1 FAIL (JT-C1), 1 BLOCKED (OD-7b), 0 NOT RUN |

The fixes after the re-check (ae926317: R1 surgery list count, R3 owner-at-discharge freeze trigger) are MEDIUM/LOW
fixes; they were exercised by TEST (JT-A4..A7, JT-D8) at ae926317. I read both changes (the surgery arm of
`BillingChargeQueryHandlers.cs:130-175` and the second `Sql` block of `20261014033110_AddVisitBillingFacts.cs`).

## My own run (targeted, catecommerce_bilj, head 5c67ab4c, built after checkout; no full suite)

- `BillingPhaseJEndpointTests` + `BillingPhaseJGateTests`: **33 passed, 0 failed** (1 m 44 s).
- Infrastructure.Tests `VisitBillableItemSourceTests` + `VisitBillingReaderTests`: **12 passed**.
- Application.Tests `PhaseJCaptureTests` + `DiagnosticHandlerTests`: **27 passed**.
- Domain.Tests `PhaseJContractTests` + `SurgeryAggregateTests` + `VisitAggregateTests`: **79 passed**.

## Criteria

§18 has no Phase J row of its own: its "Deferred" note names "visit/dispense/surgery items (Phase J after
HO-EMR/PHM/SRG)". The Phase J checklist is therefore §17 row J, §8.13 (assumed table), §11.6, §12.2, §20.2, the §18 rows
Phase J extends (A3, A5, A19, A29), and the seven owner decisions. This is not a document defect: each of those is
evaluable as written.

| # | Criterion | Verdict | Evidence |
|---|---|---|---|
| PJ-1 | §17 J / §20.2: real EMR/PHM/SRG contracts compared with §8.13 and every difference recorded as an Amended row, nothing guessed | MET | BIL §1 "Amended (Phase J)" item (4) lists each mismatch (no `CustomerId`/`BoardingStayId` on `VisitFinalisedEvent`; no `SurgeryCompletedEvent`, `SurgeryDischargedEvent` used; `DispensingId`; unpriced diagnostics; EMR kinds; drug entries carry no price); corrected by "Amended (Phase J gate fix)" item (1). Each gap is closed by an owner decision, the coordinator's EMR-43 call or a published port (`ISkuPriceReader.PriceAtAsync`, Shop `SkuRows`). Row 75's pricing rule follows the precedent of CONTRACTS row 33 (EMR prices consumables the same way) |
| PJ-2 | §8.13/§12.2 `VisitBillableItemSource`: treatments, consumables, diagnostics as Service/Medicine/Product; not the consultation fee | MET | J1 (4 lines; free and cancelled items excluded; diagnostics `Service`, 350.00 and the default 0.00; consumable `Product` 2 x 120.00; no `Appointment` charge), JT-E1 (1,090.00 over 3 lines), `PhaseJContractTests.EmrKinds_MapToBillingKinds_ExplicitlyAsTheOwnerSpecified` and `AKindWithNoCase_Throws_...`; passed in my run |
| PJ-3 | `DispenseBillableItemSource`: Medicine line per batch at the price snapshot | MET | J6 (2 charges, `Medicine`, 120.00, quantities 4 and 6), J6b (`SkuNotPriced` reported, nothing guessed), JT-E2 (855.00; killed mutant JT-D3) |
| PJ-4 | `SurgeryBillableItemSource`: drugs used + procedure fee, one owning source | MET | J7 (fee 5,000.00 from the surgery's snapshot + priced drug; the appointment is refused `AppointmentManagedBySurgery` and absent from the list; mutants JT-D1a/b killed), J7b, J7c, J7f-h, JT-E3 (5,285.50, corrected drug at 1 not 3). The fee source differs from OQ-14's assumption and is recorded (gate-fix row (1), ADR-346 revision) |
| PJ-5 | §11.6 handlers: Phase B shape (fresh scope, swallow, ids only, never enqueue) | MET | `PhaseJCaptureTests.AVisitFinalisedCapture_ThatThrows_IsSwallowed_AndLoggedWithIdsOnly` and the three siblings (dispensed, surgery discharged, reversal void); the event path end to end in J1, J6, J7 (charges appear with no manual capture); my grep of added `src` lines: no `Enqueue(`, `SaveChangesAsync`, `BeginTransaction`, background service |
| PJ-6 | A5 extended: charges reach Billing without loss; failures listed and recoverable | MET | J4 (uncaptured visit listed, manual capture records 4, repeat 0), J4b, J4d (partly captured visit stays listed, `TaxClassNotFound` reported; mutant JT-D2 killed), J7f, J7g, J7h, J1b, `PhaseJCaptureTests.AReplayAfterAPartialCapture_RecordsOnlyTheMissingLines` |
| PJ-7 | Once per source line (ADR-178, `ix_charge_source`) under replay and real concurrency | MET | J3 (3 replays), J3b (barrier), J3c (concurrent finalise), JT-F1..F3 (asserted overlapping intervals; F3 callers report exactly 3), mutant JT-D3 killed |
| PJ-8 | A3/A19 extended: itemised, BDT, Clinic line, tax class per line, rounding at the line | MET | J1 (`businessLine` Clinic, `BDT`, `taxClassId` per line), J6, `PhaseJCaptureTests.EachPhaseJKind_IsCapturedOncePerLine_AReplayRecordsNone_AndEveryChargeIsClinicBDT`, `APhaseJLine_KeepsItsSnapshotUnitPriceAndQuantity_SoTheInvoiceRoundsOnceAtTheLine`. The invoice path is unchanged (no `InvoiceCalculator` or invoice file in the diff), so A3/A19's original evidence stands |
| PJ-9 | A29 extended: capture route scoped to the session branch | MET | J4c (another branch's non-billable visit is 404, not its state), J4b (unknown id 404) |
| PJ-10 | Service date is the branch-local (Asia/Dhaka) date | MET | J1c, J7e (20:00 UTC dated the next day), mutant JT-D4 killed |
| PJ-11 | §4.2: boarding stay link deferred, not stubbed | MET | My grep of added `src` lines for "boarding": comments only, no member, column or id. OD-3 below |

### Owner decisions (implemented, tested, recorded)

| Decision | Implemented | Tested (observed) | Recorded |
|---|---|---|---|
| "Cat's owner at finalising" | `VisitAggregate.Finalise` stores `CustomerId`; `VisitFinalisedEvent.CustomerId` | J2 (owner after a change of hands billed; later change does not move it), J2b, J2c, mutant JT-D6 killed; `VisitAggregateTests` | BIL Phase J (2a); EMR "Amended (HO-BIL-001 Phase J)" (1); CONTRACTS 37; ADR-344 |
| "Add a price, charge them" | five `clinical.diagnosticPrice*` settings, frozen on the order + DB guard | J1, J5, JT-G1 (CR001 on all three columns; kills mutant JT-D7), `PhaseJContractTests.EveryDiagnosticKind_HasItsOwnRegisteredPriceSetting_DefaultZero_AdminEditable` | BIL (2b); EMR (3) and gate-fix row; CONTRACTS 38; ADR-345 |
| "Skip until Boarding" | no stay link anywhere | J1 (visit is its own bill); grep (PJ-11) | BIL (2c); EMR (1); CONTRACTS 37; backlog 76 |
| Reversal void "Keep it" | `VoidChargesOnDispensingReversedHandler` | J6 (both un-invoiced charges Voided, re-capture `DispensingReversed`), `PhaseJCaptureTests.AReversalVoid_ThatThrows_...` | BIL gate fix (2a); PHM consumer note; CONTRACTS 39; ADR-346 revision; backlog 72, 80 |
| "Owner at discharge" | `SurgeryAggregate.OwnerAtDischargeId`, event field, freeze trigger | J7d, J7i, `SurgeryAggregateTests.Discharge_StoresTheOwnerAtDischarge_AndCarriesItOnTheEvent`, mutants JT-D5 and JT-D8 killed | BIL gate fix (2b); SRG gate-fix and re-check rows; CONTRACTS 40; ADR revision; backlog 88, 97 |
| Abandoned surgery "Drugs only" | fee only when `Outcome == Completed` (`SurgeryBillableItemSource.cs:53`) | J7b, J7g, J7h | BIL gate fix (2c); SRG Phase J consumer row; ADR revision; backlog 68. Not in CONTRACTS 39/40, which record contracts, not this internal rule (no finding) |
| Old records "Bill by hand" | no back-fill (the migration has no data UPDATE: read); owner-less visit and surgery are `NotBillable`, unpriced diagnostics excluded | visit: `VisitBillableItemSourceTests` (real source; fallback mutant fails). Surgery: OD-7b BLOCKED, see ruling 1 | BIL gate fix (2d); EMR gate-fix row; SRG gate-fix row; CONTRACTS 36, 38, 40; backlog 70 |

Coordinator calls: the EMR Domain read port is BIL (3), EMR (2), CONTRACTS 36, EMR backlog 43 Closed. The PHM/SRG
consumer notes are recorded as ratified in backlog 82, BIL gate-fix (6) and the SRG gate-fix row. The README pointer to
ADR-347 is recorded as ratified in backlog 96 and the BIL re-check row.

## Rulings asked for

1. **OD-7b (BLOCKED) does not block acceptance.** It is not counted as a pass. The criterion it serves (a surgery with no
   recorded owner is not auto-billed and is billed by hand) has concrete evidence of another acceptable kind. Source:
   `SurgeryBillableItemSource.cs:45-46` returns `NotBillable("SurgeryCustomerNotRecorded")` with no fallback;
   `customerId` comes only from `OwnerAtDischargeId` (`:47`); the list's surgery arm requires
   `OwnerAtDischargeId != null` (`BillingChargeQueryHandlers.cs:140`). For new data the state cannot arise, as tests
   show: Discharge always stores the owner (`SurgeryAggregateTests.Discharge_Stores...`) and the database refuses to
   change it (J7i). Only pre-migration rows can reach the path, and none exist before launch. Backlog 99 (LOW, Open)
   tracks the missing seam. The same applies to pre-migration diagnostic orders with no price
   (`VisitBillingReader.cs:41`, `UnitPriceAmount != null`), which row 99 does not name: LOW 5.
2. **Open owner questions block nothing.** Row 67 (per order or per analyte) refines an answered decision. The built
   default (one line per order, admin price default 0) is recorded in ADR-345, the EMR row and row 67, and is additive to
   change. Row 69 (diagnostic tax class) has no effect while every rate is 0% (owner, "tax starts at zero"). It must be
   answered **before the first non-zero rate**, alongside the held-transfer line-tax pin. Also open and non-blocking:
   row 75 (surgery drug priced at the SKU price when given, or a snapshot), plus the LOW records 71, 72, 74, 87 and 99,
   and row 92 (ADR-117 "narrowed by ADR-344" pointer, coordinator).
3. **Deferred boarding (row 76) is recorded correctly in Billing.** It is LOW and tied to owner decision 3, names
   FR-BRD-15 and FR-SRG-9 (both confirmed in SRS), states that nothing is stubbed and what HO-BRD-001 must add
   (`BoardingStayId` on the events and the visit, so the three sources set `BillingGroupKind/Id`). It agrees with BIL
   (2c), EMR (1) and CONTRACTS 37. One gap: nothing reaches HO-BRD-001 itself, whose contract row P2 (`HO-BRD-001.md:1327`) still assumes "clinic
   charges carrying `BoardingStayId`", and there is no BRD backlog yet (LOW 4).
4. **Out-of-§13 files and cross-area edits are recorded, with one exception.** Every EMR, SRG and Shared file in the
   diff is named in BIL gate-fix (6), the EMR row and the SRG gate-fix row. The tenth migration is in (5) and the
   re-check row. PHM `src` is untouched (confirmed). `.ai/flows` and `.ai/modules` have a zero diff. Six
   `docs/handover/**` files (§14) are covered by owner decisions or coordinator ratification. **Not recorded in any §1
   row:** `tests/CatEcommerce.Infrastructure.Tests/Billing/VisitBillableItemSourceTests.cs`, which the TEST gate added
   (it is named only in the TEST gate file and row 98; LOW 1).
5. **Merge checklist (integrator/coordinator):**
   - **`.ai/flows/clinic-visit-flow.md`.** Apply the text in BIL "Amended (Phase J gate fix)" item (7), with two
     corrections (LOW 2). (a) Say *Billing's capture* reads a visit only through `IVisitBillingReader`, and the
     Read-layer uncaptured list counts `clinic.*` rows (the narrowed ADR-346 (1) and CONTRACTS 36). As written, the
     text repeats the claim the re-check corrected. (b) Replace line 55's stale `VisitFinalisedIntegrationEvent` with
     `VisitFinalisedEvent`, which carries `CustomerId`. Optionally note in `surgery-flow.md:55` that the stay link is
     deferred (row 76) and that surgery is billed from `SurgeryDischargedEvent` to the owner at discharge.
   - **Migration re-sequencing.** None is needed today. Master's newest migration is
     `20261013180959_AttachRetentionGuardsToReportingTables`, and `20261014033110_AddVisitBillingFacts` sorts after it.
     No other unmerged local branch adds a migration (checked all local branches). If anything merges first, re-date
     it after the newest migration. **When regenerating, carry the hand-written SQL** (LOW 3): Up's two `Sql` blocks
     (replaced `clinic.diagnostic_order_guard()`; new `clinic.surgery_owner_at_discharge_frozen()` +
     `tr_surgery_owner_at_discharge_frozen`) and Down's two (drop trigger and function; restore the original guard).
     The first Phase J row's "no hand-written SQL needed" is superseded. Afterwards re-run J7i and JT_G1, which fail
     without the SQL.
   - Also at merge: record ADR-344..346 as assigned (README already says 347); optionally add the ADR-117 pointer
     (row 92); migrate catecommerce_dev/catecommerce_test after the merge; drop catecommerce_bilj afterwards.

## FINDINGS

- [LOW 1] tests/CatEcommerce.Infrastructure.Tests/Billing/VisitBillableItemSourceTests.cs -- outside §13's list (§13
  names Domain, Application and Integration tests only) and not named in any HO-BIL-001 §1 row. `VisitBillingReaderTests`
  was named (row 90); this one, added by the TEST gate, was not. Row 100.
- [LOW 2] docs/handover/HO-BIL-001.md:23 item (7) -- the flow text says "Billing reads a visit only through
  `IVisitBillingReader`", and the narrowed ADR-346 (1) and CONTRACTS 36 contradict that. Also,
  `.ai/flows/clinic-visit-flow.md:55` names a non-existent `VisitFinalisedIntegrationEvent`. Row 101.
- [LOW 3] src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/20261014033110_AddVisitBillingFacts.cs
  -- holds hand-written SQL, but HO-BIL-001.md:22 item (6) says "no hand-written SQL needed; the integrator
  regenerates". A regeneration that trusts that sentence drops the price freeze and the owner freeze. Row 102.
- [LOW 4] docs/handover/HO-BRD-001.md:1327 (contract row P2) -- not told that the three Phase J sources and their events carry no
  stay link (row 76), so FR-BRD-15 has no note in the handover that has to close it. Row 103.
- [LOW 5] src/Infrastructure/CatEcommerce.Infrastructure/Consultation/VisitBillingReader.cs:41 -- "an unpriced
  (pre-migration) diagnostic order is not billed" is evidenced by source only, like OD-7b. Row 99 does not name it.
  Row 104.
- [LOW 6] docs/gates/HO-BIL-001-J-CODE_REVIEW.md -- reads `STATUS: FAIL` with no pointer to the combined re-check
  (in `HO-BIL-001-J-ARCHITECTURE.md`) that closed its HIGH. A reader of the CODE_REVIEW file alone sees a failed gate.
  Row 105.
- [Note] JT-B1 narrowed "0 warnings" to "0 warnings in changed files" (32 warnings on a rebuild, all in files this
  branch did not touch). Accepted as baseline, not a Phase J regression.
- [Note] JT-C1 FAIL is three full-suite failures outside Phase J (`I_E11`, Reporting `I_G6`, and the newly seen `I_G5`),
  each passing alone. `I_G5` should get a row in `docs/backlog/RPT.md` (the Reporting owner's; not proposed here).

## Proposed docs/backlog/BIL.md rows

| # | Severity | Item | Source |
|---|---|---|---|
| 100 | LOW -- Coordinator | **Name `tests/CatEcommerce.Infrastructure.Tests/Billing/VisitBillableItemSourceTests.cs` in HO-BIL-001 §1.** Added by the Phase J TEST gate, outside §13's test projects, and not in any Amended row (row 90 named only `VisitBillingReaderTests`). | ACCEPTANCE J L1 |
| 101 | LOW -- Coordinator | **Correct the flow text before applying it.** In `.ai/flows/clinic-visit-flow.md` write that Billing's *capture* reads a visit only through `IVisitBillingReader` and that the Read-layer uncaptured list counts `clinic.*` rows (ADR-346 (1) narrowed, CONTRACTS 36). Replace line 55's `VisitFinalisedIntegrationEvent` with `VisitFinalisedEvent` (carries `CustomerId`). Optionally note in `surgery-flow.md:55` that the stay link is deferred (row 76). | ACCEPTANCE J L2 |
| 102 | LOW -- Integrator | **`AddVisitBillingFacts` is not regenerate-only.** Carry its four hand-written `Sql` blocks into the regenerated migration: Up replaces `clinic.diagnostic_order_guard()` and adds `tr_surgery_owner_at_discharge_frozen`; Down drops them and restores the guard. Then re-run J7i and JT_G1. The first Phase J row's "no hand-written SQL needed" is superseded. | ACCEPTANCE J L3 |
| 103 | LOW -- Coordinator | **Tell HO-BRD-001 about row 76.** Add a §1 note (or a BRD backlog row when that file exists): `VisitFinalisedEvent`, `PrescriptionDispensedEvent` and `SurgeryDischargedEvent` carry no `BoardingStayId`, and the Visit, Dispense and Surgery item sources set no billing group, so FR-BRD-15 / FR-SRG-9 / HO-BRD-001 P2 need them added when HO-BRD-001 is built. | ACCEPTANCE J L4 |
| 104 | LOW | **Extends row 99: no behavioural test that an unpriced (pre-migration) diagnostic order is not billed.** `VisitBillingReader` filters `UnitPriceAmount != null` (read, not run); the API always stores a price (default 0). Same seam as row 99. | ACCEPTANCE J L5 |
| 105 | LOW | **`HO-BIL-001-J-CODE_REVIEW.md` shows only its FAIL.** Its HIGH was closed in the combined re-check recorded in `HO-BIL-001-J-ARCHITECTURE.md`. Future combined re-checks should leave a one-line pointer (or a short record) in each gate file they close. | ACCEPTANCE J L6 |

## SCOPE CHECK

- out-of-scope work found: none. There is no gateway, frontend or boarding stub, and no §4.4 shape (my grep of added
  `src` lines: no `double`/`float`, `BeginTransaction`, `[Authorize]`, `202`, `FOR UPDATE`, `Enqueue(`). No csproj, sln,
  NuGet.Config, global.json or Program.cs change.
- files in allowed scope: 68/69. Every file is either in §13 or a §13/§14 file authorised by a 2026-10-14 owner decision
  or coordinator ratification **and** recorded in a §1 Amended row, CONTRACTS 36-40 or backlog 82/96.
- files outside allowed scope and unrecorded: `tests/CatEcommerce.Infrastructure.Tests/Billing/VisitBillableItemSourceTests.cs`
  (LOW 1; recorded in the TEST gate and row 98, not in §1).

## UNDECLARED DEVIATIONS

- None in behaviour. The only undeclared items are paperwork: LOW 1 (the unnamed test file) and LOW 3 (the
  superseded "no hand-written SQL" sentence, which a later row contradicts without saying so).
