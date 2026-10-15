# HO-PHM-001 — Pharmacy and Prescriptions (backend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-PHM-001** |
| Title | Pharmacy and Prescriptions — backend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §6 — **US-PHM-01 … US-PHM-08** |
| SRS | [`docs/SRS.md`](../SRS.md) v2.3 — **FR-PHM-1 … FR-PHM-9**, FR-CLN-24, FR-IMP-7, FR-IMP-9, FR-IMP-15, FR-SHP-11 (the gate half), FR-SUB-6 (the event half), FR-NTF-3 (prescription refill reminders), FR-POR-3, **BR-2, BR-3, BR-4, BR-6, BR-7**, **C-2**, C-3, C-7, C-9, D-5, D-6, NFR-SEC-2, NFR-SEC-8, NFR-LEG-1, UI-8, **AC-4**, AC-7 (gate half), AC-18, Appendix C ("Prescription"), Appendix D ("Prescribe", "Dispense medicine", "Controlled drug register") |
| Pairs with | [`HO-PHM-002`](HO-PHM-002.md) — the frontend. **§12.4 of this document is its contract** |
| Builds on | [`HO-ACC-001`](HO-ACC-001.md) (staff, sessions, password hasher, login throttle, audit, `ActionRoles`, merge contract), [`HO-CAT-001`](HO-CAT-001.md) (cats, allergies, current medications, weight history), [`HO-ADM-001`](HO-ADM-001.md) (branches, settings, time model, `Money`, price-snapshot contract), [`HO-NTF-001`](HO-NTF-001.md) (outbox, templates, subject guards, link builder), [`HO-APT-001`](HO-APT-001.md) (worker shape, constraint-to-code filter pattern) |
| **Depends on — not yet written** | **`HO-EMR-001`** (visits), **`HO-IMP-001`** (batches, dispensary location, stock ledger), **`HO-SHP-001`** (SKUs, products, prescription-required flag, SKU price). §8.12 states the exact contract assumed from each |
| Depended on by | **`HO-SRG-001`** (controlled-drug register, witness verification, formulary reads — §8.9 is its contract), `HO-EMR-001` (visit prescriptions for the discharge summary and history, controlled drugs used in treatment), `HO-IMP-001` (controlled receipts and adjustments, recall trace), `HO-ORD-001` / `HO-SUB-001` (diet-authorisation gate and events), `HO-BIL-001` (dispensing charges) |
| Delivery order | Section 9 of 17 in [`README.md`](README.md#delivery-order) |
| Baseline commit | `4007225` |
| Amended | **2026-09-22 — README C-2, C-3, C-4.** §8.12 was rewritten against HO-IMP-001 as written: `IDispensaryStock` + `IStockMovementEnlister` become the one published port `IStockLedger` (`PlanAsync` / `DeductAsync` / `ReceiveReturnAsync`), and `IBatchConsumptionSource` is **deleted** — recall tracing is now a ledger read over the party stamp (HO-IMP-001 ADR-104), so this slice builds no trace implementation. G1 is confirmed met. This slice's `IDietAuthorisationGate` is confirmed the one name for the diet port; HO-SHP-001, HO-ORD-001 and HO-SUB-001 were renamed to it. New OQ-16, OQ-17. |
| Amended (build) | **2026-10-09 — implementation, branch `ho/PHM-001`.** Deviations from the text above, each decided at build time and recorded here: (1) **Ports.** `ICatalogueReader` and `ISkuPriceReader` are Pharmacy-owned read adapters over HO-SHP-001's published `SkuRows()`/`PriceRows()` projections (a clinic-only SKU is one with `ListedOnStorefront = false`). Batch offers come from HO-IMP-001's published `IStockLedger.GetDispensaryBatchesAsync` and the dispensary location from `IStockLedger.GetClinicDispensaryAsync` (both added to HO-IMP-001 §8.8 on 2026-10-09 by owner decision A, after the architecture gate; the first build read Inventory's internal `IStockPositionReader` and `StockLocationAggregate`, which was deviation (1) as first written and is withdrawn), because `IStockLedger.PlanAsync` returns lines only. `CatalogueReader` now reads product and SKU facts only through `SkuRows()` (which gained a `Brand` column), no longer `db.Products`. Dispensing deducts with **one `IStockLedger.DeductAsync` per SKU carrying that SKU's explicit lines** (the ledger's FEFO comparison is per demand); controlled-drug corrections reference `StockReferenceKind.ControlledDrugCorrection`. (2) **Reversal** returns stock through `ReceiveReturnAsync`, which lands in the **Quarantine** location, not back in the dispensary batch location (text of §8.7/I-E11 said "the same batch"); the register `Returned` entry and the supply count are as specified; reversal does not reopen the owner's repeat request. `DispensingLine` has no `LedgerEntryId` (the ledger port does not expose entry ids; the ledger is traced by the dispensing id). `ix_dispensing_supply` is filtered to `Dispensed` status so a reversed supply frees its number. (3) **Register.** Seals are appended by `IRegisterChain.TryAppendAsync` (one save; SQLSTATE 23505 = lost race, retry next pass) rather than a repository save of the last seal. The append-only triggers raise SQLSTATE `PH001` (unmapped, so a bypass attempt is a `500`, never a `4xx`). `IControlledDrugDirectory` gained `HasRegisterEntriesAsync`. The seal read shows `chainPosition` and `entryHash` only (I-X2: no `previousHash` in any response). (4) **Options and limits.** Settings live in `PharmacyOptions` (`Pharmacy:Worker`, `Pharmacy:Witness`, `Pharmacy:RepeatRequest`) instead of `RateLimitingOptions`; the rate limits are keyed per staff account (no session id is available to the limiter). (5) **Codes and constants added:** `PrescribingNotPermitted` (→ `403`), `InvalidDose`, `InvalidInstructions`, `InvalidRegisterEntry`; `PharmacySystemActors` (the worker's actor id); `FormularyMedicineAggregate.ControlledChangeReason` (one extra column, `controlled_change_reason`). (6) **Files.** The owned entities share one file (`PharmacyEntities.cs`); `DispensedLineFact` lives in `Models`, not the events namespace (an event carries no strings). The `DenyAllDietAuthorisationGate` fake and its Infrastructure test are left in place; only the registration was replaced by the real `DietAuthorisationGate` (with a set-based `CheckManyAsync`). (7) **Behaviour decisions.** A renewal excludes the renewed prescription from the active-duplicate interaction check; a `Completed` prescription is not renewable (§8.4 table); revoked authorisations are excluded from `AuthorisationExpired`; the customer "past" scope means "not active" (cancelled included). (8) **OQ-12 adopted:** HO-EMR-001's `[ClinicalRecordAccess]` filter (kinds `ClinicalHistory`/`Visit`) and an explicit `TryLogClinicalAccessAsync` for id-routed reads. The EMR discharge/history section provider and discharge contributor were **not** built (they belong to HO-EMR-001's extension points; `GetVisitPrescriptionsQuery`, `GetVisitDietAuthorisationsQuery` and `GetCatPrescriptionHistoryQuery` are the contract). (9) **Migrations.** Five, as planned (`AddPharmacyFormulary`, `AddPharmacyPrescriptions`, `AddControlledDrugRegister`, `AddPharmacyDispensing`, `AddDietAuthorisations`); the integrator regenerates on merge. The two filtered-index predicates are interpolated from the enumeration `.Value` in the migration files (U-X3). (10) **Tests.** Multi-day time passing in integration tests ages the prescription's or authorisation's own dates in SQL instead of advancing the host clock (staff and customer sessions are validated against the same fake clock and would expire). U-G1 and U-W1/U-W2 are covered at integration level (the gate and the verifier need the real database; no mocking library is in the solution). (11) **Fix round 2026-10-09.** `GET .../dispense-plan` writes a fail-closed clinical-access row (500 if the log fails); the pharmacy queue and register list/entry reads are deliberately NOT access-logged (owner decision B). A dispensing line gets a fresh id per ledger movement. |
| Amended (owner) | **2026-10-10 — owner confirmations.** The defaults this build was made on are confirmed: **ADR-153** (authorization mapping) as written; **OQ-1** the controlled-drug witness must be a Vet or Nurse and not the dispenser, and Reception may dispense; **OQ-8** an Administrator may reverse a dispensing but cannot correct the register (Vet only); **OQ-3** remote renewal (no open visit, flagged `IsRemoteRenewal`, fully re-checked) is allowed. No code change; closes `docs/backlog/PHM.md` row 33. |
| Amended (HO-BIL-001 Phase J) | **2026-10-14 -- consumer note, no change to this area's files.** HO-BIL-001 Phase J (owner decisions 2026-10-14) captures a `Dispensed` supply: one Medicine charge per `DispensingLine` at its `PriceSnapshot` (a line with no price is skipped and reported `SkuNotPriced`), tax class from Shop's `SkuRows`, service date `DispensedOnLocal`. It subscribes to `PrescriptionDispensedEvent` (source id `DispensingId`) and to `DispensingReversedEvent`, which voids the supply's charges not yet invoiced. It also calls `ISkuPriceReader.PriceAtAsync` for surgery drugs (a consumer use of this area's port). See HO-BIL-001 §1 Amended (Phase J), CONTRACTS row 39. |
| Platform | `Platform.Infrastructure.Core` **1.0.34** — do not downgrade |

---

## 2. Objective

Give the clinic a **pharmacy it can defend to an inspector**: vets prescribe against a formulary with
dose, allergy and interaction checks; pharmacy staff dispense the exact batch and print a label;
controlled drugs are witnessed and written to a register nobody can edit; repeats are honoured only
inside the window the vet set; and therapeutic diets are unlocked in the shop only for the cat a vet
authorised, for as long as the vet said.

Five things this slice is careful about:

1. **The server decides what is safe to ask about, the vet decides what to do.** Dose, allergy and
   interaction checks are one **pure** domain service (`PrescriptionSafetyAssessor`) run twice — once
   for the screen, once again inside the issue command. A warning the vet has not acknowledged cannot
   be skipped by calling the API directly, and an acknowledgement made against *different* warnings
   (the weight changed in between) is refused (ADR-139).
2. **Stock only moves through Inventory.** Dispensing never writes `inventory.*`. It asks HO-IMP-001's
   ledger port to enlist the movement in the same unit of work, so the dispensing record, the
   prescription's supply count, the ledger entries and any controlled-drug register entries commit in
   **one** `SaveChangesAsync` — or none of them do (ADR-143). An expired batch is refused with **no
   override anywhere** (BR-3).
3. **The controlled-drug register is append-only at three layers** — the aggregate has no mutators,
   an EF interceptor refuses a modified or deleted entry, and PostgreSQL triggers refuse `UPDATE`,
   `DELETE` and `TRUNCATE` (C-2, D-5). Entries are **sealed into a hash chain** so a change made
   around all three is detectable (ADR-144, ADR-145).
4. **A witness is a person, not a dropdown.** The second staff member authenticates with **their own
   identifier and password** in the same request. A name picked from a list proves nothing (ADR-146).
5. **The diet gate is evaluated when it is asked, not when a job runs.** An authorisation locks the
   product again the moment its date passes, whether or not the expiry sweep has run; the sweep only
   records the expiry, raises the event and tells the owner (ADR-149).

§17 builds the slice in **seven phases**; each compiles, migrates and passes its own tests first.

---

## 3. In scope

### 3.1 Stories

| Story | Pri | Pts | Capability delivered here | Phase |
|---|---|---|---|---|
| US-PHM-04 | S | 8 | **AC1**: administrator-maintained feline formulary — name, active ingredients, drug classes, form, strength, default route, mg/kg dosing range, contraindications, interactions, dispensing unit, linked SKUs. **AC2**: the controlled flag, which makes dispensing require a verified witness and write a register entry (phase E consumes it) | B, E |
| US-PHM-01 | M | 5 | A vet issues a prescription — medicine, strength, dose, route, frequency, duration, quantity, instructions — linked to the cat and to an **open** visit; non-vets refused server-side and the denial audited (US-CLN-22, AC-18) | C |
| US-PHM-02 | S | 8 | Dose converted to mg/kg against the cat's **latest** weight and compared with the formulary range; outside it → warning with the expected range that must be acknowledged; no weight within `pharmacy.doseWeightMaxAgeDays` → refused until one is recorded | C |
| US-PHM-03 | S | 8 | Allergy conflict → **blocking** warning requiring explicit acknowledgement with a reason; interaction with an active medication (active prescriptions and the profile's current medications) → shown with its severity; `Major` and `Contraindicated` interactions also require acknowledgement | C |
| — (FR-PHM-7, C-2, BR-7) | M | — | The **controlled-drug register**: append-only, witnessed, sealed, correctable only by new linked entries, exportable for inspection, reconciled against stock. **The contract HO-SRG-001, HO-EMR-001 and HO-IMP-001 write through** | D |
| US-PHM-05 | M | 5 | Dispense against a prescription: batches offered FEFO from the branch's clinic dispensary, the **exact** batch(es) deducted through HO-IMP-001's ledger, a frozen label (cat name, owner, medicine, dose, instructions, date, expiry); expired-only stock blocked with no override; reprint counted; same-day reversal | E |
| US-PHM-06 | S | 5 | Repeatable prescriptions with a vet-set validity; the owner requests a repeat in the portal and staff dispense it **without a new consultation**; outside the window → refused with the reason and what to do; a vet may **renew** | F |
| US-PHM-07 | M | 3 | The owner's active prescriptions per cat: medicine, dose, frequency, remaining duration, issuing vet | F |
| US-PHM-08 | M | 5 | A vet authorises a named therapeutic food for a cat with a validity period from an open visit; the **gate** HO-ORD-001 calls says yes for that cat and product until the period ends; the expiry sweep marks it expired, raises the event and notifies the owner | G |

Also delivered, from other areas' stories:

| Story / FR | What this slice delivers |
|---|---|
| **US-NTF-03 AC1** — "prescription refills" | Refill reminders ahead of the refill-due date with a direct link to request the repeat; a `PrescriptionRefillDue` subject kind and guard (HO-NTF-001 §4.2 reserved this for HO-PHM-001) |
| US-CLN-13, US-CLN-16 | `GET /api/staff/visits/{visitId}/prescriptions` and the cat's prescription history, which HO-EMR-001's consultation screen and discharge summary read |
| US-IMP-11 AC (dispensing half) | `DispensingBatchConsumptionSource` — every cat and customer dispensed a batch, for HO-IMP-001's recall trace |
| US-SRG-08 | The register, the witness verifier and the `Administered` entry kind with wastage — HO-SRG-001 records the surgery half through them |

### 3.2 Decisions already settled upstream — do not re-litigate

| Decision | Where |
|---|---|
| In-memory CQRS, one host, `IInMemoryDispatcher` only; never `202` | ADR-010, 013, 014, 019, 020 |
| PostgreSQL / EF Core, one `DbContext`, `xmin` → `409` | ADR-011, ADR-016, ADR-021 |
| Money is `decimal(12,2)` + ISO code, default `BDT` | HO-ADM-001 ADR-054 |
| Prices are snapshotted at the transaction, never re-read | HO-ADM-001 ADR-049 |
| Branch-local dates via IANA zones; conversion only through HO-ADM-001's time model | HO-ADM-001 ADR-045 |
| Active branch from `X-Branch-Id` for staff | HO-ADM-001 ADR-046 |
| Every endpoint declares its access; permissions are code-defined in one `ActionRoles` table | HO-ADM-001 ADR-043, ADR-044; HO-ACC-001 ADR-036 |
| Messages only through `INotificationOutbox.Enqueue` in the command handler, before the save; timed producers deduplicate | HO-NTF-001 ADR-056, ADR-057 |
| `CustomerMergedEvent` is binding on every context holding a customer id | HO-ACC-001 ADR-039 |
| Identifier + password is the only credential; no OTP, no 2FA | SRS v2.2 FR-ACC-3, FR-ACC-8 withdrawn |
| **No payment gateway.** Nothing here takes money; dispensing snapshots a price for Billing | SRS §1.2, A-2 |
| Weight history is its own aggregate; latest weight is derived read-side | ADR-023 |

---

## 4. Out of scope

### 4.1 Frontend — all of it

Screens are [`HO-PHM-002`](HO-PHM-002.md). Criteria phrased as UI are met here at the contract level:

| Story AC (UI phrasing) | Server half — delivered here |
|---|---|
| US-PHM-02 "I am warned with the expected range and must confirm" | The safety check returns `expectedRange` (mg/kg and the absolute mg for this weight); issue without `acknowledgedWarningsFingerprint` → `400 PrescriptionWarningsNotAcknowledged` |
| US-PHM-02 "I am prompted to record one" | `400 RecentWeightRequired` naming the latest weight and its date; weight is recorded through HO-CAT-001's existing endpoint |
| US-PHM-03 "a blocking warning is shown" | Allergy conflicts are `blocking: true` in the assessment and need a reason in the acknowledgement |
| US-PHM-05 "a label prints" | The dispensing response carries the frozen `label`; `POST …/label-prints` counts prints. Printing is the browser's |
| US-PHM-06 "the owner is told why" | `400 PrescriptionValidityEnded` whose message says when it ended and that a vet must authorise a new prescription |
| US-PHM-08 "the product becomes purchasable" | `IDietAuthorisationGate` answers `Authorised`; the storefront button is HO-SHP-002 / HO-ORD-002 |

### 4.2 Deferred to a named later area — contracts delivered here

| Capability | Where it lands | What this slice delivers |
|---|---|---|
| Add-to-cart and checkout gating of prescription diets, the cat recorded on the order line (US-SHP-10, FR-SHP-11, AC-7) | **HO-ORD-001** (and HO-SHP-001 for the product page) | `IDietAuthorisationGate` (§8.10) — `CheckAsync` and `GetAuthorisedCatsAsync` |
| Suspending and resuming prescription-diet subscriptions (US-SUB-05, FR-SUB-6) | **HO-SUB-001** | `DietAuthorisationExpiredEvent`, `DietAuthorisationRevokedEvent`, `DietAuthorisationIssuedEvent`, `DietAuthorisationExtendedEvent` |
| Controlled drugs used in surgery, with wastage (US-SRG-08) | **HO-SRG-001** | `IControlledDrugRegister`, `IStaffWitnessVerifier`, `IFormularyReader` (§8.9) |
| Controlled drugs administered as treatment (US-CLN-15) | **HO-EMR-001** | Same ports |
| Controlled receipts, adjustments, wastage and opening stock (US-IMP-04, US-IMP-13) | **HO-IMP-001** | Same register port, entry kinds `Receipt`, `Adjustment`, `Wastage`, `OpeningBalance`; `IControlledDrugDirectory.FindBySkuAsync` |
| Recall trace across dispensing (US-IMP-11) | **HO-IMP-001** | **Nothing to build** (README C-3): the trace is a ledger read over the `StockParty(CustomerId, CatId)` stamp this slice passes to `DeductAsync` |
| Charging dispensed medicines (FR-BIL-2) | **HO-BIL-001** | `PrescriptionDispensedEvent` with a `PriceSnapshot` per line; `DispensingReversedEvent` |
| Prescriptions in the consultation history and discharge summary (US-CLN-13, US-CLN-16) | **HO-EMR-001** | `GetVisitPrescriptionsQuery`, `GetCatPrescriptionHistoryQuery` and their endpoints |
| Prescriptions in the medical-history PDF (US-CAT-10) | HO-CAT amendment or HO-POR-001 | `GetCatPrescriptionHistoryQuery` |
| Portal dashboard tile (FR-POR-1) | HO-POR-001 | `GET /api/customers/me/prescriptions?scope=active` |
| Clinical and controlled-drug reports (§8.1 "clinical") | HO-RPT-001 | The tables; the register export |
| Retention enforcement (C-3, D-8) | HO-XCT-001 | Nothing here is ever hard-deleted |

### 4.3 Not buildable yet — named gaps

| Gap | Consequence |
|---|---|
| **No SMS or email provider** (HO-NTF-001 OQ-1) | Refill reminders, repeat and diet notices are enqueued, rendered and logged; outside Development only the **in-app copy** is delivered |
| **Controlled-drug jurisdiction** (SRS Q-1) | Schedules are a free-text label (`ControlledSchedule`, ≤ 30); the register records every field FR-PHM-7 and FR-SRG-8 name. A statutory register format or regulator return is not built (OQ-2) |
| **Coded allergens** | HO-CAT-001 stores allergens as free text (≤ 200). Allergy matching is a normalised term match (ADR-140) — it warns on likely matches and cannot promise to catch a misspelling. Recorded, tested, and stated on the screen (HO-PHM-002) |
| NFR-SEC-8 "every access to a clinical record shall be logged" | No access-log mechanism exists yet. If HO-EMR-001 delivers one, prescription detail reads call it by an Amended row here (OQ-12) |
| Partial supply when the dispensary is short | A supply is dispensed **in full** (split across up to five batches) or not at all (OQ-7) |

### 4.4 Explicitly forbidden shapes

No `IBusMessageDispatcher`, MassTransit, a second host, a `CommandService` layer, `202 Accepted`,
`[Authorize]`. **No write to `inventory.*`, `catalogue.*` or `clinic.visit*` tables** — those areas'
ports only (ADR-151). **No `UPDATE` or `DELETE` of a controlled-drug register entry or seal, from any
code path, for any reason** — including a data fix, a migration or a merge (ADR-144). **No override
of an expired batch** (BR-3). **No witness chosen without their credentials** (ADR-146). **No enqueue
from an event handler** (HO-NTF-001 ADR-057). **No medical dosing library or drug database** — the
formulary is the practice's data. **No time-zone library.**

---

## 5. Prerequisites

| Required | State |
|---|---|
| HO-CAT-001 merged | ✅ at `4007225` |
| **HO-ACC-001, HO-ADM-001, HO-NTF-001, HO-APT-001 merged** | ❌ **not implemented at baseline** — §20.1 |
| **HO-EMR-001 written and merged** — `VisitAggregate` with an open/finalised status | ❌ **not written** — phases C and G need it (§8.12, §20.2) |
| **HO-SHP-001 written and merged** — products, SKUs, `RequiresPrescription`, SKU price, clinic-only (non-storefront) SKUs for medicines | ❌ **not written** — phases B (SKU links), E (prices) and G need it (§20.2, OQ-5) |
| **HO-IMP-001 written and merged** — batches, the `ClinicDispensary` location per branch, the ledger enlistment port | ❌ **not written** — phase E needs it; phase D's reconciliation needs it (§20.2, §20.3) |
| PostgreSQL 17.5 `catecommerce_dev`, `dotnet-ef` 9.0.0 | ✅ |
| New NuGet packages | **None.** `System.Security.Cryptography.SHA256`, `System.Text.Json`, `BackgroundService`, `TimeProvider` are in the runtime |

**Phases A, B (without SKU validation), D (without reconciliation) and the pure domain services of C
and E can be built and unit-tested before the three upstream areas exist.** Nothing that calls their
ports is merged until the port has a real implementation — no stubs in `src/` (§20.2).

The shared-database rules of HO-CAT-001 §16.1 and HO-ACC-001 §16.1 apply. Formulary codes and
prescription numbers are **unique keys** — mint codes per test (§16.1).

---

## 6. Governing architecture documents

Where any of these disagrees with a `.ai/templates/*.cs` file, **the markdown wins**.

| Document | What you need from it |
|---|---|
| [`.ai/architecture.md`](../../.ai/architecture.md) | Layer responsibilities and the reference direction — every port in §8.9 and §8.12 is declared in `Domain.DomainServices` and implemented in Infrastructure; §6 the verified platform surface |
| [`.ai/cqrs.md`](../../.ai/cqrs.md) | `IInMemoryDispatcher` only. **A witness password on a command is acceptable only because dispatch is in-process** (the same reasoning as HO-ACC-001's sign-in) — it never reaches an event, a log or the outbox |
| [`.ai/bounded-contexts.md`](../../.ai/bounded-contexts.md) | Names the **`Prescription`** context (`PrescriptionAggregate`, `DietAuthorisation`) — this slice uses that segment and widens it (ADR-137). Rule 1 one aggregate per command — two recorded exceptions (ADR-143, ADR-148); rule 2 by id; rule 3 decides that dispensing records and register entries are their own roots; **rule 4 decides that the safety assessment and FEFO batch planning are domain services**. Its cross-context table places BR-2 in `DietAuthorisation` with the order handler checking it — exactly §8.10 |
| [`.ai/namespace-conventions.md`](../../.ai/namespace-conventions.md) | **`Prescription`** is already a listed feature name — no new segment is invented. Every namespace in §11/§12 |
| [`.ai/decisions/adr.md`](../../.ai/decisions/adr.md) | **ADR-009 … ADR-023 are binding.** ADR-009 (one action-to-role table), ADR-014 (violations in the response), ADR-016 (`xmin`), ADR-021 (repository corrections), ADR-023 (latest weight read-side) are the ones this slice turns on |
| [`.ai/command-handler-pattern.md`](../../.ai/command-handler-pattern.md) | §3 handler shape; §4 the uniqueness pre-check; **§6 the rule-placement table** — dosing and allergy rules in the domain service, the "open visit" and "cat alive" checks in the handler |
| [`.ai/read-side-pattern.md`](../../.ai/read-side-pattern.md) | `AsNoTracking()`; project before materialising; a query never writes — **the safety-check endpoint is a query** and writes nothing |
| [`.ai/persistence-ef-core.md`](../../.ai/persistence-ef-core.md) | §2 the repository (F23: one `SaveChangesAsync` commits every tracked entity — the basis of ADR-143); §4 owned mapping; §6 `xmin`; §8 column types; ADR-021's `ValueGeneratedNever()` rule on every root **and** owned id |
| [`.ai/domain-events-and-outbox.md`](../../.ai/domain-events-and-outbox.md) | §3 a failing event handler fails the request after commit — why nothing safety-relevant happens in an event handler here; §4 the outbox; §5 event vs outbox |
| [`.ai/enumeration-pattern.md`](../../.ai/enumeration-pattern.md) | The enumerations in §10.1 — base class ours, in `Shared` (ADR-022) |
| [`.ai/di-registration-guide.md`](../../.ai/di-registration-guide.md) | Validators as `IValidator<T>`; `ValidateScopes`/`ValidateOnBuild` stay on; the worker creates a scope per unit of work |
| [`.ai/host-composition.md`](../../.ai/host-composition.md) | `Program.cs` shape; lifetimes; the never-register list |
| [`.ai/feature-creation-checklist.md`](../../.ai/feature-creation-checklist.md) | File order within each phase |
| [`.ai/flows/prescription-diet-flow.md`](../../.ai/flows/prescription-diet-flow.md) | The reference cross-module flow for §8.10: Clinic owns the authorisation, Ordering calls `IsAuthorisedAsync(catId, skuId)`, checked twice, expiry announced by event. **One refinement recorded in ADR-149** (the authorisation names the product; the gate still takes a SKU) |
| [`.ai/flows/batch-recall-flow.md`](../../.ai/flows/batch-recall-flow.md) | "Each module answers for itself" — `DispensingBatchConsumptionSource` |
| [`.ai/flows/surgery-flow.md`](../../.ai/flows/surgery-flow.md), [`.ai/flows/clinic-visit-flow.md`](../../.ai/flows/clinic-visit-flow.md) | Where the register and prescribing sit in the flows HO-SRG-001 and HO-EMR-001 build |
| [`HO-CAT-001`](HO-CAT-001.md) | §7 platform facts; §8 `CatAllergy`, `CatMedication`, `CatWeightRecordAggregate`; §12.1 EF rules. **The reference implementation — match it** |
| [`HO-ACC-001`](HO-ACC-001.md) | §11.9 audit capture and the immutability-trigger pattern reused for the register; §12.8 `RequireActionFilter` and C11 denial audit; the password hasher and login throttle reused by the witness verifier |
| [`HO-NTF-001`](HO-NTF-001.md) | §8.2 catalogue extension rule; §8.4 producer rules; §8.7 subject guards; §8.8 link builder |
| [`HO-APT-001`](HO-APT-001.md) | §12.2 the worker shape copied by `PharmacyWorker`; §12.5 constraint-to-code mapping |

---

## 7. Verified platform facts

HO-CAT-001 F1–F10, HO-ACC-001 F11–F18, HO-ADM-001 F19–F22, HO-NTF-001 F23–F27 and HO-APT-001
F28–F30 apply. **F23 and F24 carry this slice**: an entity enlisted in the scoped context before a
repository save commits atomically with it, and events dispatch after that commit.

This handover was written without new access to the code, so it adds **no new verified fact**. Three
things it relies on are marked for verification in Phase A step 1. If any is false, **stop** (§20.9):

| # | To verify | How | If false |
|---|---|---|---|
| V1 | A PostgreSQL **sequence** used as a column default (`HasDefaultValueSql("nextval('pharmacy.cd_register_entry_number_seq')")` with `ValueGeneratedOnAdd`) coexists with application-minted `Guid` ids and is read back after `SaveChangesAsync` | A spike test in `IntegrationTests` that inserts two entries and reads their numbers | Assign the number through a `SELECT nextval` allocator in the handler instead (the `IPrescriptionNumberAllocator` shape) |
| V2 | A `BEFORE UPDATE OR DELETE` row trigger and a `BEFORE TRUNCATE` statement trigger created by `migrationBuilder.Sql` behave on `pharmacy.*` as HO-ACC-001's do on `audit.audit_entry` | I-D1 | Report — the immutability guarantee is unmet |
| V3 | HO-ACC-001's `IPasswordHasher.Verify` and login-throttle state can be called from a second verifier without creating a session | Read HO-ACC-001's merged code | Report — ADR-146 needs HO-ACC-001's owner to expose a verify-only path |

---

## 8. Domain model

### 8.1 Context and aggregate boundaries

```
Prescription context — namespace segment `Prescription`, schema `pharmacy`
FormularyMedicineAggregate       (root)                              pharmacy.formulary_medicine
├── Interactions [owned many ≤ 50]  FormularyInteraction              pharmacy.formulary_interaction
└── LinkedSkus   [owned many ≤ 10]  LinkedSku                         pharmacy.formulary_medicine_sku
PrescriptionAggregate            (root) → BranchId, CatId, CustomerId, VisitId, PrescriberId, MedicineId
├── SafetyRecord [owned one]        what the vet saw and acknowledged pharmacy.prescription (columns)
├── Warnings [owned many ≤ 30]      PrescriptionWarning               pharmacy.prescription_warning
└── RepeatRequests [owned many ≤ 30] RepeatRequest                    pharmacy.repeat_request
DispensingAggregate              (root) → PrescriptionId, BranchId, CatId, CustomerId
├── Lines [owned many 1…5]          DispensingLine (sku, batch, qty, ledger entry id)   pharmacy.dispensing_line
└── Label [owned one]               MedicineLabelSnapshot             pharmacy.dispensing (columns)
ControlledDrugRegisterEntryAggregate (root, append-only) → BranchId, MedicineId, BatchId   pharmacy.cd_register_entry
ControlledDrugRegisterSeal       (root, append-only, Id == EntryId)  pharmacy.cd_register_seal
DietAuthorisationAggregate       (root) → CatId, CustomerId, ProductId, VisitId          pharmacy.diet_authorisation
```

**One aggregate mutated per command**, with two recorded exceptions, each committed in **one**
`SaveChangesAsync`:

- **Dispensing** creates a `DispensingAggregate`, updates the `PrescriptionAggregate` (supply count,
  status, the repeat request it fulfils), and enlists HO-IMP-001 ledger entries and — for a controlled
  medicine — register entries (ADR-143). The prescription's `xmin` is what stops two people dispensing
  the last authorised supply at once.
- **Renewing** a prescription creates the new prescription and marks the old one renewed-linked
  (ADR-148).

Register entries enlisted by another area (surgery, treatment, receipt) ride **that** area's save
through `IControlledDrugRegister.Enlist` — the same enlistment pattern as HO-NTF-001's outbox (F23),
so an entry exists if and only if the thing it records was committed.

Cross-aggregate reads — the visit's status, the cat's allergies and weight, the owner's name, the
SKU's product, the batch list — are repository calls, HO-CAT-001 queries, or the ports in §8.12.

### 8.2 `FormularyMedicineAggregate` — US-PHM-04

| Field | Type | Rules |
|---|---|---|
| `Code` | string ≤ 30 | `^[A-Z0-9-]+$`, unique (`ix_formulary_medicine_code`), immutable |
| `Name` | string ≤ 200 | e.g. *Meloxicam oral suspension* |
| `ActiveIngredients` | `text[]` 1…5, each ≤ 100 | Stored normalised (§8.6) |
| `DrugClasses` | `text[]` 0…10, each ≤ 100 | e.g. `nsaid`, `penicillin` — used by allergy matching |
| `Form` | `MedicineForm` | |
| `Strength` | owned `Strength` VO | `Amount` (> 0, 4 dp), `Unit` (`StrengthUnit`: `Mg`, `Mcg`, `G`, `Iu`, `Percent`), `PerQuantity` (> 0, default 1), `PerUnit` (`DispensingUnit`) — *50 mg per 1 Tablet*, *1.5 mg per 1 Millilitre* |
| `DefaultRoute` | `AdministrationRoute` | |
| `AllowedRoutes` | `AdministrationRoute[]` 1…n | Contains `DefaultRoute` |
| `DosingBasis` | `DosingBasis` | `PerKg` or `Fixed` |
| `DoseRange` | owned VO, nullable | `MinMgPerKg`, `MaxMgPerKg` (> 0, 4 dp, min ≤ max). **Required when `DosingBasis = PerKg`**, absent otherwise |
| `Contraindications` | string ≤ 2000 | Plain text, shown to the prescriber on every check (never matched automatically) |
| `Interactions` | owned many ≤ 50 | `FormularyInteraction`: `Id`, `InteractsWithMedicineId?` **or** `InteractsWithTerm?` (≤ 100, normalised — an ingredient or class), `Severity` (`InteractionSeverity`), `Note` (≤ 500). Exactly one of the two targets |
| `IsControlled` | bool | US-PHM-04 AC2 |
| `ControlledSchedule?` | string ≤ 30 | Required when controlled (SRS Q-1 — free text) |
| `DispensingUnit` | `DispensingUnit` | The unit a prescription's quantity and the stock ledger use |
| `LinkedSkus` | owned many 0…10 | `LinkedSku(Id, SkuId)` — HO-SHP-001 SKUs that **are** this medicine. **Dispensing needs at least one** (`MedicineNotStocked`) |
| `IsActive` | bool | Inactive → not prescribable; existing prescriptions still dispensable |

| Method | Rules | Event |
|---|---|---|
| `Create(dto)` | All the above | `FormularyMedicineCreatedEvent` |
| `Update(dto)` | Everything but `Code`; `ExpectedVersion` from the client (two admins editing → `409`) | `FormularyMedicineUpdatedEvent` |
| `SetControlled(isControlled, schedule, reason?)` | A separate method and command so the change is a deliberate act with its own audit action; **clearing** the flag on a medicine that has register entries requires a reason (≤ 500) | `FormularyMedicineControlledStatusChangedEvent` |
| `Deactivate()` / `Reactivate()` | | `FormularyMedicineActivationChangedEvent` |

Linked SKUs are validated in the handler through HO-SHP-001's catalogue reader (exist, active); a SKU
may be linked to **one** medicine only (`SkuAlreadyLinkedToMedicine` — handler pre-check plus the
unique index on `formulary_medicine_sku.sku_id`, §12.1).

**Why interactions are listed per medicine and checked in both directions.** The formulary is
maintained by people; an interaction entered on meloxicam against `corticosteroid` and not on
prednisolone against `nsaid` must still fire when prednisolone is prescribed to a cat on meloxicam. The
detector (§8.6) looks at both medicines' lists.

### 8.3 `PrescriptionAggregate` — US-PHM-01, 06, 07

| Field | Type | Notes |
|---|---|---|
| `PrescriptionNumber` | string | `RX-{yyyy}-{000001}` from sequence `pharmacy.prescription_number_seq`, allocated by `IPrescriptionNumberAllocator` in the handler; unique; printed on the label |
| `BranchId` | Guid | The visit's branch (HO-ADM-001 §8.2 rule) |
| `CatId`, `CustomerId` | Guid | The cat's owner **at issue**; reassigned on merge (§11.6); access follows the cat's **current** owner (§12.3) |
| `VisitId` | Guid | Must be an **open** visit (§8.12, HO-EMR-001) |
| `IsRemoteRenewal` | bool | A renewal issued without an open visit (§8.8, OQ-3) |
| `PrescriberId` | Guid | The issuing vet |
| `MedicineId` | Guid | |
| `MedicineSnapshot` | owned VO | `Code`, `Name`, `Form`, `StrengthText`, `IsControlled`, `ControlledSchedule?`, `DispensingUnit`, `LinkedSkuIds` — frozen at issue; a later formulary edit never changes a prescription |
| `Dose` | owned VO | `Amount` (> 0, 4 dp) + `Unit` (`DoseUnit`) |
| `Route` | `AdministrationRoute` | One of the medicine's `AllowedRoutes` (`RouteNotAllowedForMedicine`) |
| `Frequency` | `DoseFrequency` | |
| `DurationDays` | int 1…365 | Per supply |
| `Quantity` | decimal(10,2) > 0 | Per supply, in `DispensingUnit` |
| `Instructions` | string 1…1000 | Plain text; goes on the label |
| `IssuedOn` | `timestamptz` | |
| `StartsOn` | `DateOnly` | Default today (branch-local); not before today − 1 |
| `IsRepeatable`, `RepeatsAuthorised` | bool, int 0…12 | `RepeatsAuthorised > 0` iff repeatable |
| `ValidUntil` | `DateOnly` | Repeatable: vet-set, `StartsOn < ValidUntil ≤ StartsOn + pharmacy.maxRepeatValidityDays`. Not repeatable: `StartsOn + pharmacy.defaultDispenseWindowDays` unless the vet sets an earlier date. **The last day any supply may be dispensed** |
| `SuppliesDispensed` | int | 0 … `1 + RepeatsAuthorised` |
| `LastDispensedOn?` | `DateOnly?` | Branch-local date of the latest supply |
| `Status` | `PrescriptionStatus` | §8.4 |
| `CancelledOn?`, `CancelledBy?`, `CancellationReason?` (≤ 500) | | |
| `RenewedFromPrescriptionId?`, `RenewedByPrescriptionId?` | Guid? | ADR-148 |
| `SafetyRecord` | owned VO | `WeightKg?`, `WeightRecordedOn?`, `DoseMgPerKg?`, `ExpectedMinMgPerKg?`, `ExpectedMaxMgPerKg?`, `DoseCheckOutcome`, `WarningsFingerprint`, `AcknowledgedBy?`, `AcknowledgedOn?`, `AllergyAcknowledgementReason?` (≤ 500), `DoseAcknowledgementNote?` (≤ 500) |
| `Warnings` | owned many | `Kind`, `Blocking`, `Severity?`, `Subject` (≤ 200 — the allergen or the other medicine), `Detail` (≤ 500) — **exactly what was on the vet's screen**, kept forever with the prescription |
| `RepeatRequests` | owned many | §8.8 |

### 8.4 The status machine — Appendix C — ADR-142

```
Issued ──first Dispense──► Dispensed ──last authorised supply──► Completed
  │                            │
  │                            └──ValidUntil passes with supplies left──► Expired
  └──ValidUntil passes undispensed──► Expired
Issued / Dispensed ──Cancel (vet)──► Cancelled
```

| Method | From | Rules | Event |
|---|---|---|---|
| `Issue(dto, assessment)` | — | §8.5 assessment passed or acknowledged; fingerprint matches | `PrescriptionIssuedEvent` |
| `RecordSupply(dispensingId, localDate, repeatRequestId?)` | `Issued`, `Dispensed` | `localDate ≤ ValidUntil` else **`PrescriptionValidityEnded`**; `SuppliesDispensed < 1 + RepeatsAuthorised` else `NoSuppliesRemaining`; → `Dispensed` or `Completed` | — *(the dispensing aggregate raises the event)* |
| `ReverseSupply(dispensingId)` | `Dispensed`, `Completed` | Only the latest supply; `SuppliesDispensed − 1`; status recomputed | — |
| `Cancel(by, reason)` | `Issued`, `Dispensed` | Reason required; an open repeat request is declined with the same reason | `PrescriptionCancelledEvent` |
| `Expire(today)` | `Issued`, `Dispensed` | `today > ValidUntil` and supplies remain | `PrescriptionExpiredEvent` |
| `MarkRenewed(newId)` | `Issued`, `Dispensed`, `Expired` | ADR-148 | — |

Any other transition → **`InvalidPrescriptionTransition`**, naming both states.

**"Active" is derived, never stored** (`PrescriptionActivityRules`, Shared, pure — the portal query,
the interaction detector and the refill sweep all call it):

```
CourseEndsOn = (LastDispensedOn ?? StartsOn) + DurationDays − 1
SuppliesLeft = 1 + RepeatsAuthorised − SuppliesDispensed
IsActive(today) = Status ∉ {Cancelled}
                  and ( CourseEndsOn ≥ today
                        or (SuppliesLeft > 0 and ValidUntil ≥ today and Status ≠ Expired) )
RemainingDays(today) = max(0, CourseEndsOn − today + 1)
```

`Expired` is set by the worker (§12.2) **and** treated as expired by every read and command the moment
`ValidUntil` passes — a late sweep never makes a prescription dispensable.

### 8.5 The safety assessment — US-PHM-02, US-PHM-03 — ADR-139, ADR-140, ADR-141

`PrescriptionSafetyAssessor` (Domain.DomainServices, **pure**):

```
Assess(SafetyInput) → SafetyAssessment
SafetyInput:
  Medicine        (snapshot incl. Strength, DosingBasis, DoseRange, ActiveIngredients, DrugClasses,
                   Interactions, Contraindications)
  Dose, Route, Frequency
  LatestWeight?   (Kilograms, RecordedOn)                   ← HO-CAT-001 cat_weight_record, newest row
  Today, WeightMaxAgeDays                                   ← pharmacy.doseWeightMaxAgeDays
  Allergies       [(Allergen, Reaction, Severity)]           ← HO-CAT-001 CatAllergy
  HasRecordedAllergies
  ActiveMedicines [(MedicineId, Name, Ingredients, Classes, Interactions, Source, PrescriptionNumber?)]
                  Source = Prescription (active per §8.4) | ProfileMedication (CatMedication with
                  EndsOn null or ≥ today, resolved to a formulary medicine by name — §8.6)
  UnresolvedProfileMedications [name]                        ← profile medications no formulary entry matched
SafetyAssessment:
  WeightRequirement   Satisfied | Missing | Stale(latest, ageDays)
  DoseCheck           (Outcome, DoseMg?, DoseMgPerKg?, ExpectedMinMgPerKg?, ExpectedMaxMgPerKg?,
                       ExpectedMinMg?, ExpectedMaxMg?)
  Warnings[]          (Kind, Blocking, RequiresAcknowledgement, Severity?, Subject, Detail)
  Information[]       (contraindication text, "no allergies recorded", unresolved profile medications)
  Fingerprint         hex SHA-256 over the sorted (Kind, Subject, Severity, rounded DoseMgPerKg,
                      WeightRecordedOn) tuples of every warning that requires acknowledgement
```

**Weight — US-PHM-02 AC2.** When `DosingBasis = PerKg` and the latest weight is missing or older than
`pharmacy.doseWeightMaxAgeDays` (30), the requirement is `Missing`/`Stale` and **issue is refused**
with `RecentWeightRequired`: *"Record Mitu's weight before prescribing — the latest was 4.10 kg on
2 Aug 2026 (51 days ago)."* There is no acknowledgement path; recording a weight is one call
(HO-CAT-001 C4). `Fixed`-basis medicines need no weight (OQ-4).

**Dose conversion — ADR-141** (`DoseConverter`, pure). `DoseMg` from the entered dose:

| Dose unit | Medicine strength | `DoseMg` |
|---|---|---|
| `Mg` / `Mcg` / `G` | any | converted to mg (`Mcg ÷ 1000`, `G × 1000`) |
| `Ml` | strength `Mg`/`Mcg`/`G` per `Millilitre` | `Amount × strengthMg ÷ PerQuantity` |
| `Tablet` / `Capsule` | strength per `Tablet` / `Capsule` | same |
| anything else (`Drop`, `Application`, `Iu`), or a unit that does not match the strength's `PerUnit`, or a `Percent`/`Iu` strength | — | **not convertible** |

`DoseMgPerKg = DoseMg ÷ WeightKg`, rounded to 4 dp. Outcome `WithinRange` when
`Min ≤ DoseMgPerKg ≤ Max` (inclusive, compared after rounding both to 4 dp); `BelowRange` /
`AboveRange` otherwise; `NotCheckable` when the dose is not convertible or the medicine has no range.
**`NotCheckable` is a warning, never a silent pass**, but it does not require acknowledgement — the
screen says *"This dose could not be checked against a mg/kg range."* The expected range is also
returned as absolute mg for this cat (`ExpectedMinMg = Min × WeightKg`), because a vet thinks in
"0.4 mg", not "0.1 mg/kg".

**Warnings and what must be acknowledged:**

| Kind | Raised when | Blocking | Requires acknowledgement |
|---|---|---|---|
| `DoseBelowRange` / `DoseAboveRange` | Outcome outside the range | No | **Yes** — US-PHM-02 AC1 "must confirm" (note optional, ≤ 500) |
| `DoseNotCheckable` | As above | No | No |
| `AllergyConflict` | §8.6 matches a recorded allergy | **Yes** | **Yes, with a reason** (≤ 500) — US-PHM-03 AC1 "blocking … explicit acknowledgement" |
| `Interaction` | §8.6 finds an interaction with an active medicine | No | Only for `Major` and `Contraindicated` (ADR-139); `Minor`/`Moderate` shown with severity — US-PHM-03 AC2 |

**Acknowledgement is bound to what was seen.** The issue command carries
`AcknowledgedWarningsFingerprint`, `AllergyAcknowledgementReason?` and `DoseAcknowledgementNote?`. The
handler re-runs the assessment on current data:

1. Nothing requires acknowledgement → issue (a fingerprint sent anyway is ignored).
2. Something does and no fingerprint was sent → **`400 PrescriptionWarningsNotAcknowledged`**, the
   body carrying the assessment.
3. A fingerprint was sent and differs → **`409 PrescriptionWarningsChanged`** with the new assessment
   (a new weight was recorded, an allergy added, another prescription issued meanwhile).
4. An allergy conflict without a reason → `400 AllergyAcknowledgementReasonRequired`.

The warnings, the fingerprint, the weight used and who acknowledged are stored on the prescription
(`SafetyRecord`, `Warnings`) — the record of what the vet was told.

### 8.6 Allergy and interaction matching — ADR-140

`MedicineTermNormaliser` (Shared, pure): lower-case invariant; strip diacritics; replace every
non-letter/digit run with one space; trim; singularise a trailing `s` on words of ≥ 5 letters
(`penicillins` → `penicillin`, not `gas` → `ga`).

`AllergyConflictDetector` (pure). **Match terms** of a medicine = its normalised name, every active
ingredient and every drug class. An allergy **conflicts** when, after normalisation, the allergen
equals a match term, or the allergen contains a match term as whole words, or a match term contains
the allergen as whole words and the allergen is ≥ 4 letters. `"Penicillin (hives)"` conflicts with
amoxicillin whose classes include `penicillin`; `"Pollen"` conflicts with nothing. Each conflict names
the allergen, the matched term and the allergy's severity.

`InteractionDetector` (pure). For the new medicine *N* and each active medicine *A*: an interaction
exists if *N* lists *A*'s id, or lists a term matching one of *A*'s ingredients or classes; or *A*
lists *N*'s id or a term matching *N*'s. Duplicates collapse to the **highest** severity with every
note joined. The same medicine already active (a duplicate course) is reported as an `Interaction`
of severity `Major` with *"Mitu already has an active prescription for Meloxicam (RX-2026-000123)."*

**Profile medications** (`CatMedication.MedicationName`, free text) resolve to a formulary medicine
when the normalised name equals a medicine's name or one of its ingredients. Unresolved names are
listed as **information** — *"Also taking: 'fish oil' — not in the formulary, not checked"* — never
silently ignored.

**Known limitation, stated to the user:** a misspelt allergen (`"penicilin"`) is not matched. The
screen says allergy checks match recorded allergy names against the medicine's ingredients and
classes (HO-PHM-002 ADR-FE-068).

### 8.7 Dispensing — US-PHM-05 — ADR-143, ADR-147

**`DispensingBatchPlanner`** (Domain.DomainServices, **pure** — `.ai/bounded-contexts.md` rule 4
names FEFO as a domain service):

```
Plan(quantity, batches[(SkuId, BatchId, LotNumber, ExpiresOn, Available, IsDispensable, NotDispensableReason?)], today)
  → DispensePlan(SuggestedLines[], OnlyExpiredStockAvailable, TotalDispensable, Shortfall)
Validate(requestedLines[], batches, today, fefoOverrideReason?)
  → violations
```

Rules, each tested:

1. **Expired** (`ExpiresOn < today`, branch-local) → never suggested and never accepted —
   **`BatchExpired`**, *"Batch L2231 expired on 12 Mar 2026 and cannot be dispensed."* **No override,
   no role, no flag** (BR-3, FR-IMP-9).
2. Quarantined, recalled or otherwise not dispensable per HO-IMP-001 → `BatchNotDispensable` with
   Inventory's reason.
3. **Only expired stock** — the linked SKUs have stock at the dispensary but every unit is expired →
   `OnlyExpiredStockAvailable = true`; dispensing is refused with **`OnlyExpiredStockAvailable`**:
   *"The only Meloxicam in the dispensary is expired (batch L2231, 12 Mar 2026). It cannot be
   dispensed — ask Inventory to write it off and restock."* — **US-PHM-05 AC2**.
4. Suggestion = **FEFO**: dispensable batches by `ExpiresOn`, then `LotNumber`, filled greedily, at
   most `DispensingRules.MaxLinesPerDispensing` (5) lines.
5. Requested lines differ from the FEFO suggestion (a different batch while an earlier-expiring one
   could have covered the line) → `FefoOverrideReasonRequired` unless `FefoOverrideReason` (≤ 500) is
   given — BR-4, FR-IMP-7. *The physical box in the dispenser's hand is the truth; the reason records
   why.*
6. Σ requested quantity must equal the prescription's `Quantity` exactly (`DispenseQuantityMismatch`)
   — a supply is full, not partial (OQ-7). Total dispensable below it → `InsufficientDispensaryStock`
   naming what is available.
7. Every line's SKU is one of the medicine snapshot's `LinkedSkuIds` (`SkuNotLinkedToMedicine`).

**`DispensePrescriptionCommand` handler** — one save:

1. Load the prescription; branch from `IActiveBranchAccessor`; cat alive (`CatNotTreatable`);
   `RecordSupply` preconditions (validity, supplies left, not cancelled or expired).
2. If `RepeatRequestId` is given, that request is `Requested` and belongs to this prescription.
3. Load candidate batches through `IStockLedger.PlanAsync` (§8.12); `DispensingBatchPlanner.Validate`.
4. **Controlled medicine** → `WitnessIdentifier` and `WitnessPassword` required
   (`WitnessRequired`); verify through `IStaffWitnessVerifier` (§8.9) **before any write**; witness ≠
   dispenser (`WitnessMustDifferFromDispenser`).
5. For each line: `IStockLedger.DeductAsync(StockDemand(…, Purpose: Dispense), StockReference(Dispensing, dispensingId, lineId), StockParty(customerId, catId))`; a
   violation from Inventory (a race took the stock) → that code, nothing saved.
6. Controlled → one `IControlledDrugRegister.Enlist` per line, kind `Dispensed`, with the cat, the
   customer, the dispenser, the witness, the dose text and the dispensing id.
7. Price per line through `ISkuPriceReader.PriceAt(skuId, now)` (HO-SHP-001) → `PriceSnapshot`
   (`UnitPrice`, `LineTotal`); a SKU with no price → snapshot null and `unpricedLines` in the
   response (OQ-6).
8. Compose the label (`MedicineLabelComposer`, below); create the `DispensingAggregate`;
   `prescription.RecordSupply(…)`; mark the repeat request `Dispensed` and enqueue
   `pharmacy.repeat-ready` (§8.11) if it came from the portal.
9. `SaveAsync(dispensing)` — the tracked prescription, ledger entries, register entries and
   notification request commit with it (F23). `xmin` on the prescription → `409`.

**`DispensingAggregate`**

| Field | Notes |
|---|---|
| `PrescriptionId`, `PrescriptionNumber`, `BranchId`, `CatId`, `CustomerId` | |
| `SupplyNumber` | 1 … `1 + RepeatsAuthorised` |
| `IsRepeat`, `RepeatRequestId?` | |
| `DispensedBy`, `DispensedOn` (`timestamptz`), `DispensedOnLocal` (`DateOnly`) | |
| `WitnessId?` | Controlled only |
| `FefoOverrideReason?` | |
| `Lines` | `Id`, `SkuId`, `BatchId`, `LotNumber`, `ExpiresOn`, `Quantity`, `LedgerEntryId`, `RegisterEntryId?`, `PriceSnapshot?` |
| `Label` | `MedicineLabelSnapshot` — below |
| `LabelPrintCount` | 0 at creation; the first print counts |
| `Status` | `Dispensed` → `Reversed` |
| `ReversedOn?`, `ReversedBy?`, `ReversalReason?` | |

Methods: `Create` → **`PrescriptionDispensedEvent`** (ids, lines with SKU, batch, quantity and
`PriceSnapshot` — HO-BIL-001's contract); `RecordLabelPrint(by)` → `LabelPrintedEvent` (a
`BusinessEvent` audit row: reprints of a controlled-drug label are worth knowing about);
`Reverse(by, reason, now)` → `DispensingReversedEvent`.

**`MedicineLabelComposer`** (pure) freezes, at dispensing:

| Label field | Source | US-PHM-05 AC1 |
|---|---|---|
| `CatName` | `CatAggregate.Name` | cat name |
| `OwnerName` | customer full name (HO-ACC-001 read) | owner |
| `MedicineLine` | *"Meloxicam oral suspension 1.5 mg/ml"* | medicine |
| `DoseLine` | *"Give 0.3 ml by mouth once daily for 5 days"* — `DoseFormatter` from dose, route, frequency, duration | dose |
| `Instructions` | The prescription's text | instructions |
| `DispensedOnLocal` | Branch-local date | date |
| `ExpiresOn` | The **earliest** `ExpiresOn` of the lines | expiry |
| `QuantityLine` | *"Quantity: 10 ml"* | — |
| `PrescriptionNumber`, `PrescriberName`, `ClinicName`, `ClinicPhone`, `ClinicAddress?` | Prescription, staff name, branch | — |
| `ControlledNotice?` | *"Controlled drug"* when controlled | — |
| `StandardWarnings` | `LabelRules.StandardWarnings` — *"For animal treatment only. Keep out of reach of children."* (OQ-2) | — |

A reprint prints the **same** snapshot — a label never changes after the medicine leaves the counter.

**Reversal** — `ReverseDispensingCommand` (Vet, Administrator): only the latest supply of its
prescription, within `pharmacy.dispensingReversalHours` (24) of `DispensedOn`, reason required
(≤ 500); controlled → a verified witness. It enlists a `Return` movement per line to the **same
batch** at the dispensary (Inventory refuses if the batch is now expired — then the stock is wasted
through Inventory instead, §20.6), a register `Returned` entry per controlled line linked to the
original entry, `prescription.ReverseSupply`, and `dispensing.Reverse` — one save (within ADR-143).

### 8.8 Repeats and renewal — US-PHM-06 — ADR-148

**`RepeatRequest`** (owned by the prescription, ≤ `RepeatRules.MaxRequestsPerPrescription` 30): `Id`,
`RequestedBy` (customer), `RequestedOn`, `PreferredBranchId`, `Note?` (≤ 500, plain text), `Status`
(`Requested` → `Dispensed` | `Declined` | `Withdrawn`), `DecidedBy?`, `DecidedOn?`,
`DeclineReason?` (≤ 500), `DispensingId?`.

| Method | Rules | Event |
|---|---|---|
| `RequestRepeat(dto, today)` | Repeatable (`NotRepeatable`); status `Dispensed` (the first supply was dispensed at the clinic — `FirstSupplyNotDispensed`); `SuppliesLeft > 0` (`NoRepeatsRemaining`); **`today ≤ ValidUntil`** else **`PrescriptionValidityEnded`**; no other `Requested` (`RepeatRequestAlreadyOpen`) | `RepeatRequestedEvent` |
| `WithdrawRepeatRequest(id)` | `Requested`, by the requester | — |
| `DeclineRepeatRequest(id, by, reason)` | `Requested`; vet only (a clinical judgement); reason required | `RepeatRequestDeclinedEvent` |
| *(dispensing)* | `Requested` → `Dispensed` inside the dispense save | — |

The refusal message for **US-PHM-06 AC2** is fixed text with the date: *"This prescription could be
repeated until 30 Sep 2026. A vet needs to authorise a new prescription — please book a consultation
or call the clinic."* The customer response also carries `bookLink` (HO-APT-002's `/book?catId=…`).

**Dispensing a repeat needs no visit** (AC1): the dispense command never looks at a visit. Staff may
also dispense a repeat at the counter without a portal request — the same rules apply.

**Renewal** — `RenewPrescriptionCommand` (vet only, BR-6): from a prescription in `Issued`,
`Dispensed` or `Expired` (not `Cancelled`, not already renewed — `PrescriptionAlreadyRenewed`), issue
a **new** prescription with the same medicine and the vet's (possibly edited) dose, frequency,
duration, quantity, instructions, repeats and validity; the **full safety assessment runs again** on
today's weight, allergies and medications; `VisitId` is the open visit if one is given, else the
original's visit with `IsRemoteRenewal = true` (OQ-3). The old prescription gets
`RenewedByPrescriptionId` and, if still `Issued` or `Dispensed`, is cancelled with reason *"Renewed as
RX-…"*. One save (ADR-148's exception). The new one is what the owner then repeats against.

### 8.9 The controlled-drug register — FR-PHM-7, C-2, BR-7, US-PHM-04 AC2 — ADR-144, ADR-145, ADR-146

**`ControlledDrugRegisterEntryAggregate`** — append-only; **no method mutates an existing entry**.

| Field | Notes |
|---|---|
| `EntryNumber` | `bigint`, from `pharmacy.cd_register_entry_number_seq` (V1) — the order entries were recorded in |
| `BranchId` | The register is kept per branch |
| `MedicineId`, `MedicineSnapshot` | `Name`, `StrengthText`, `Form`, `ControlledSchedule`, `DispensingUnit` |
| `SkuId`, `BatchId`, `LotNumber`, `BatchExpiresOn` | |
| `Kind` | `ControlledDrugEntryKind`: `OpeningBalance`, `Receipt`, `Dispensed`, `Administered`, `Wastage`, `Returned`, `Adjustment`, `Correction` |
| `QuantityDelta` | decimal(10,2), signed, in `DispensingUnit`; outflows negative. Sign checked per kind (`InvalidRegisterQuantity`) |
| `DoseText?` | ≤ 200 — `Dispensed`, `Administered` |
| `WastageQuantity?` | ≥ 0 — `Administered` (FR-SRG-8 "the wastage"); a standalone `Wastage` entry uses `QuantityDelta` |
| `CatId?`, `CustomerId?` | Required for `Dispensed` and `Administered` |
| `ActorId` | The dispensing / administering / receiving staff member |
| `WitnessId?` | **Required** for `Dispensed`, `Administered`, `Wastage`, `Returned`, `Correction`; must differ from `ActorId` |
| `SourceType`, `SourceId` | `ControlledDrugSourceType`: `Dispensing`, `DispensingReversal`, `Treatment`, `Surgery`, `GoodsReceipt`, `StockAdjustment`, `Correction` — and the id of that record |
| `CorrectsEntryId?`, `CorrectionReason?` (≤ 500) | `Correction` only; `ReturnedEntryId?` for `Returned` |
| `OccurredOn`, `RecordedOn` | `timestamptz`; `RecordedOn` = now, `OccurredOn` ≤ `RecordedOn` and ≥ `RecordedOn − 24 h` for enlisted entries |
| `Notes?` | ≤ 500, plain text |

Single factory `Record(dto)` enforcing the table above → `ControlledDrugRegisterEntryRecordedEvent`
(ids and kind only). **There is no `Update`, no `Delete`, no `Void`.**

**Three layers of append-only** (ADR-144):

1. The aggregate has no mutator.
2. `ControlledDrugRegisterImmutabilityInterceptor` (Infrastructure, `SaveChangesInterceptor`, beside
   HO-ACC-001's audit one) throws `ControlledDrugRegisterImmutableException` (programming error) on any
   `Modified` or `Deleted` entry **or seal** before SQL is sent.
3. PostgreSQL: `BEFORE UPDATE OR DELETE` row triggers and `BEFORE TRUNCATE` statement triggers on
   `pharmacy.cd_register_entry` and `pharmacy.cd_register_seal` that `RAISE EXCEPTION` (V2). "Anyone"
   in US-SRG-08 AC2 includes someone at a `psql` prompt.

**Corrections — "corrections are new, linked entries".** `RecordControlledDrugCorrectionCommand`
(vet only — Appendix D gives only the Vet **W** on the register): `CorrectsEntryId` (an entry of the
same branch and medicine — `CorrectionTargetMismatch` otherwise; any number of corrections may chain),
the corrected values (dose text, cat, notes), `QuantityDelta` (may be 0), `Reason` (required), witness
verified. A non-zero delta **also** enlists an HO-IMP-001 `Adjustment` movement with reason *"CD
register correction #1234"* so stock and register stay in step. The original entry stays exactly as it
was; reads show it with its corrections beneath.

**Tamper evidence — sealing** (ADR-145). A correction is visible, but a change made *around* all three
layers (a superuser disabling the trigger) would not be. So entries are sealed into a per-branch hash
chain:

- `ControlledDrugRegisterSeal` — `Id` = `EntryId`, `BranchId`, `ChainPosition` (1, 2, 3 … per branch,
  unique pair), `PreviousHash` (64 hex, 64 zeros at position 1), `EntryHash` =
  `SHA-256(PreviousHash ‖ ControlledDrugEntryCanonicalForm(entry))`, `SealedOn`. Append-only like the
  entries.
- `ControlledDrugEntryCanonicalForm` (Shared, pure) — every field of §8.9's table in a fixed order,
  invariant culture, `\u001F`-separated, instants in UTC round-trip form, decimals with 2 dp. Its field
  list is pinned by a test (U-D6): adding a field to the entry without adding it here fails the build.
- The worker's sealing pass (§12.2) seals, per branch, entries with no seal and `RecordedOn < now −
  30 s` (so an in-flight transaction with a lower number is not skipped for long), in `EntryNumber`
  order, one `SealControlledDrugEntriesCommand` per branch per pass (≤ 500 entries). Two worker
  instances racing produce a `23505` on `(branch_id, chain_position)` → the pass stops and retries
  next time.
- `VerifyControlledDrugRegisterQuery` recomputes the chain for a branch and returns `IsIntact`,
  `EntriesChecked`, the first mismatching `ChainPosition`, and entries **unsealed for more than 10
  minutes** (a stuck worker is itself a finding).

**The witness — ADR-146.** `IStaffWitnessVerifier` (Domain port, Infrastructure implementation over
HO-ACC-001's staff accounts, password hasher and login throttle — V3):

```
Task<WitnessVerification> VerifyAsync(string identifier, string password,
                                      Guid actingStaffId, ControlledDrugSourceType source,
                                      CancellationToken ct)
WitnessVerification(bool IsVerified, Guid? WitnessStaffId)
```

- The identifier is the witness's mobile number or email (FR-ACC-3), the password theirs.
- Verified only if: the account exists, is active, is **not** locked, the password verifies, the
  account holds a role in **`ControlledDrugRules.WitnessRoles`** (`Vet`, `Nurse` — OQ-1), and it is
  not `actingStaffId`.
- **Every** failure returns the same `WitnessNotVerified` — *"The witness could not be verified. Ask
  them to enter their own sign-in details."* — never which part failed.
- A wrong password **counts toward the witness account's lockout** exactly as a sign-in failure does
  (HO-ACC-001 `LoginThrottleRules`), and writes an `AuthenticationEvent` audit row
  `WitnessVerificationFailed` with the **masked** identifier; success writes `WitnessVerified` with the
  witness id and the source (`Dispensing`, `Correction`, `Surgery` …).
- No session is created for the witness. The password is on the command only (in-process dispatch,
  `.ai/cqrs.md`); it is **excluded from logs, events, audit capture and the notification outbox**, and
  the command's `ToString()` is overridden to omit it.
- Rate limit `witness` (10 per minute per staff session) on every endpoint that accepts witness
  credentials.

**`IControlledDrugRegister` — the contract HO-SRG-001, HO-EMR-001 and HO-IMP-001 write through:**

```csharp
public interface IControlledDrugRegister
{
    // Adds the entry to the scoped DbContext without saving. The caller's own repository save
    // commits it with the business change (F23). Throws DomainRuleViolationException on a rule
    // in §8.9's table (missing witness, wrong sign, same actor and witness).
    Guid Enlist(ControlledDrugRegisterEntryDto entry);
}

public interface IControlledDrugDirectory
{
    Task<ControlledMedicineRef?> FindBySkuAsync(Guid skuId, CancellationToken ct);      // null = not controlled
    Task<ControlledMedicineRef?> FindByMedicineAsync(Guid medicineId, CancellationToken ct);
}

public interface IFormularyReader
{
    Task<FormularyMedicineSnapshot?> GetAsync(Guid medicineId, CancellationToken ct);
    Task<FormularyMedicineSnapshot?> FindBySkuAsync(Guid skuId, CancellationToken ct);
}
```

The caller is responsible for verifying the witness through `IStaffWitnessVerifier` **before**
enlisting (the register checks that a witness id is present and differs from the actor; it cannot
check a password it was never given). HO-SRG-001's anaesthesia log therefore records a controlled drug
as: verify witness → enlist the Inventory movement → enlist the register entry (`Administered`, dose,
wastage) → save its own aggregate.

**Balances and reconciliation.** `GetControlledDrugBalancesQuery` — per branch, per medicine, per
batch: the register balance (Σ `QuantityDelta`) and the **stock balance** at the branch for the linked
SKUs and that batch from `IStockLedger.GetBranchBalancesAsync` (all locations of the branch, not only the dispensary;
OQ-10); a difference is returned as `Mismatch` with both figures. Nothing is auto-corrected.

**Export — for inspection.** `GET …/controlled-drugs/register/export?medicineId&from&to` streams CSV
(HO-ADM-001 ADR-051's rules: ≤ 50 000 rows, cells beginning `= + - @` prefixed with `'`), one row per
entry with its running balance per medicine and batch, its seal position and hash, and its corrections'
entry numbers. Every export writes a `BusinessEvent` audit row `ControlledDrugRegisterExported` with the
filters.

### 8.10 Diet authorisations — US-PHM-08 — ADR-149

**`DietAuthorisationAggregate`**

| Field | Notes |
|---|---|
| `CatId`, `CustomerId` | The owner at issue |
| `ProductId`, `ProductNameSnapshot` | An HO-SHP-001 product with `RequiresPrescription = true` (`ProductNotPrescriptionDiet`) and active |
| `VisitId` | An **open** visit of this cat (US-PHM-08 AC1 "given an open visit") |
| `AuthorisedBy` | Vet |
| `ValidFrom`, `ValidTo` | `DateOnly`, inclusive, **main-branch local** dates (ADR-045); `ValidFrom ≥ today − 1`, `ValidFrom ≤ ValidTo ≤ ValidFrom + pharmacy.maxDietAuthorisationDays` (365) |
| `Notes?` | ≤ 500 |
| `Status` | `Active` → `Expired` \| `Revoked` |
| `ExpiredOn?`, `RevokedOn?`, `RevokedBy?`, `RevocationReason?` | |
| `ExtensionCount` | |

| Method | Rules | Event |
|---|---|---|
| `Authorise(dto)` | No other `Active` authorisation for the same cat and product (`DietAuthorisationAlreadyActive` — extend it instead; filtered unique index `ix_diet_authorisation_active` as the race guard) | `DietAuthorisationIssuedEvent` (`AuthorisationId`, `CatId`, `CustomerId`, `ProductId`, `ValidFrom`, `ValidTo`) |
| `Extend(newValidTo, by, today)` | `Active`, or `Expired` within the last 30 days (a renewal after a lapse re-activates it); vet only; `newValidTo ≤ today + pharmacy.maxDietAuthorisationDays` | `DietAuthorisationExtendedEvent` |
| `Expire(today)` | `Active` and `today > ValidTo` | `DietAuthorisationExpiredEvent` |
| `Revoke(by, reason)` | `Active`; vet only; reason required | `DietAuthorisationRevokedEvent` |

**The gate — `IDietAuthorisationGate`** (Domain port; implementation `DietAuthorisationGate` in
Infrastructure, read-only):

```csharp
Task<DietGateResult> CheckAsync(Guid customerId, Guid catId, Guid skuId, CancellationToken ct);
Task<IReadOnlyList<AuthorisedCat>> GetAuthorisedCatsAsync(Guid customerId, Guid skuId, CancellationToken ct);

DietGateResult(DietGateOutcome Outcome, Guid? AuthorisationId, DateOnly? ValidTo, string Message)
// Outcome: NotRequired | Authorised | NoAuthorisation | AuthorisationExpired | CatNotOwned | CatDeceased
```

Evaluation, in order: the SKU's product is not `RequiresPrescription` → `NotRequired`; the cat is not
owned by the customer → `CatNotOwned`; deceased → `CatDeceased`; an authorisation for the cat and the
SKU's product with `Status = Active` and `ValidFrom ≤ today ≤ ValidTo` → `Authorised`; one that
exists but whose `ValidTo < today` (whatever its stored status) → `AuthorisationExpired`; else
`NoAuthorisation`. **"Today" is computed on every call** — the product locks at midnight whether or
not the sweep has run (**US-PHM-08 AC2** "locks again automatically"). Messages are owner-facing and
contain no clinical detail: *"This food needs a vet's authorisation for your cat. Book a consultation
to get one."* / *"Mitu's authorisation for this food ended on 30 Sep 2026."*

HO-ORD-001 calls `CheckAsync` at add-to-cart **and** at checkout (the flow doc's "checked twice");
this slice never learns what a cart is.

**Expiry sweep** (worker, §12.2): for `Active` authorisations with `ValidTo < today`, dispatch
`ExpireDietAuthorisationCommand` — `Expire`, enqueue `pharmacy.diet-authorisation-expired` to the
cat's **current** owner (if alive), `UpdateAsync`. The command is a normal command with a business
change, so the enqueue rides it (HO-NTF-001 rule 1), and the change is idempotent (already expired →
success, nothing written).

### 8.11 Messages — ADR-150

Templates appended to HO-NTF-001's catalogue (all **Transactional** — medical, FR-NTF-4 — `Normal`
urgency, channels `Sms`, `Email`, `InApp`):

| Key | Enqueued by | Producer variables |
|---|---|---|
| `pharmacy.refill-due` | Refill sweep via `RequestNotificationCommand` (timed producer) | `catName`, `medicineName`, `refillDueOn` (Date), `validUntil` (Date), `repeatLink` (Url; every channel — "a direct reorder link", US-NTF-03) |
| `pharmacy.repeat-ready` | Dispense of a portal repeat request | `catName`, `medicineName`, `branchName`, `prescriptionLink` (Url) |
| `pharmacy.repeat-declined` | `DeclineRepeatRequestCommand`, and `CancelPrescriptionCommand` when it declines an open request | `catName`, `medicineName`, `declineReason` (Text), `bookLink` (Url) |
| `pharmacy.diet-authorised` | `AuthoriseDietCommand` | `catName`, `productName`, `validTo` (Date), `authorisationsLink` (Url) |
| `pharmacy.diet-authorisation-expired` | `ExpireDietAuthorisationCommand` | `catName`, `productName`, `expiredOn` (Date), `bookLink` (Url) |

`declineReason` is the vet's plain-text reason; the renderer inserts it as a literal text node
(HO-NTF-001 ADR-062). No template carries a dose, a diagnosis or a batch number.

**Refill reminders** (US-NTF-03 AC1, refill half). `RefillScheduleCalculator` (pure): for a
prescription with `IsRepeatable`, `SuppliesLeft > 0`, `LastDispensedOn` set, and not cancelled/expired:
`RefillDueOn = LastDispensedOn + DurationDays`; remind when
`RefillDueOn − pharmacy.refillReminderLeadDays ≤ today ≤ RefillDueOn` **and** `RefillDueOn ≤ ValidUntil`
(no reminder to refill something that can no longer be refilled). The sweep dispatches
`RequestNotificationCommand` with `SubjectKind = PrescriptionRefillDue`, `SubjectId` = prescription,
`SubjectDueOn = RefillDueOn`, `DeduplicationKey = "pharmacy:refill:{rxId:N}:{supplyNumber}"`.

**`PrescriptionRefillDueGuard`** (HO-NTF-001 §8.7): valid while the prescription is not cancelled or
expired, `SuppliesLeft > 0`, today ≤ `ValidUntil`, the recomputed `RefillDueOn == SubjectDueOn` (a
repeat dispensed in the meantime moves it), no `Requested` repeat request is open, and the cat is
alive and owned by the recipient.

**Link builder** — HO-NTF-001 amendment (to be recorded by a §1 Amended row there):
`PrescriptionLink(prescriptionId)` → `{WebBaseUrl}/portal/prescriptions/{id}`;
`RepeatRequestLink(prescriptionId)` → `{WebBaseUrl}/portal/prescriptions/{id}?action=repeat`;
`DietAuthorisationsLink()` → `{WebBaseUrl}/portal/prescriptions?tab=diets`. `bookLink` uses
HO-APT-001's `CatCareLink(catId)`.

### 8.12 Contracts assumed from areas not yet written

These are the **exact** shapes this slice codes against. Each owning handover defines the port; if its
names differ, this slice adapts by an Amended row and nothing else changes. **If a guarantee below is
not given, stop (§20.2, §20.3).**

**From HO-EMR-001 — visits**

| Assumed | Shape | Used for |
|---|---|---|
| `VisitAggregate` (clinic context) | `Id`, `CatId`, `BranchId`, `AppointmentId?`, `AttendingVetId`, `Status` with at least **`Open`** and **`Finalised`** | Loaded read-only by id in the issue, renew and authorise handlers: exists (`VisitNotFound`), belongs to the cat (`VisitCatMismatch`), is `Open` (**`VisitNotOpen`**). Never mutated here |
| HO-EMR-001 reads PHM | `GetVisitPrescriptionsQuery`, `GetVisitDietAuthorisationsQuery`, `GetCatPrescriptionHistoryQuery` (§12.6) | Discharge summary, consultation history |

**From HO-IMP-001 — batches, the dispensary, the ledger**

> **Reconciled 2026-09-22 (README C-2, C-3).** HO-IMP-001 is written. It publishes **one** movement
> port, `IStockLedger` (its §8.8, ADR-103), and **no** `IBatchConsumptionSource` — ADR-104 replaced
> per-module recall fan-out with a party stamp taken at movement time. The block below is the real
> contract; the enlist-without-save shape this slice depends on is unchanged.

```csharp
// HO-IMP-001 §8.8 — the ONLY way any context moves stock.
public interface IStockLedger
{
    // Plan writes nothing: the dispensing screen's batch picker calls it freely.
    Task<StockResult<StockPlan>> PlanAsync(StockDemand demand, CancellationToken ct);

    // Validates, then ENLISTS rows in the scoped DbContext WITHOUT saving (F23), so the dispensing
    // record and the movement commit in one SaveChangesAsync.
    Task<StockResult<IReadOnlyList<StockMovementLine>>> DeductAsync(
        StockDemand demand, StockReference reference, StockParty party, CancellationToken ct);
    Task<StockResult<IReadOnlyList<StockMovementLine>>> ReceiveReturnAsync(
        StockReturn stockReturn, StockReference reference, StockParty party, CancellationToken ct);
}

StockDemand(Guid BranchId, Guid SkuId, decimal Quantity, MovementKind Purpose /* Dispense */,
            IReadOnlyList<Guid>? LocationIds /* null → every active location permitting the purpose */,
            IReadOnlyList<(Guid BatchId, Guid LocationId, decimal Quantity)>? ExplicitLines,
            string? OverrideReason)
StockReference(ReferenceKind Kind /* Dispensing */, Guid ReferenceId, Guid? ReferenceLineId, string? Label)
StockParty(Guid? CustomerId, Guid? CatId)   // BOTH required for Dispense — HO-IMP-001 §8.8 rule 2
StockMovementLine(Guid BatchId, string LotNumber, DateOnly? ExpiresOn, Guid LocationId,
                  decimal Quantity, bool FefoOverridden)   // enough for a medicine label (US-PHM-05)
StockResult<T>(bool IsSuccess, T? Value, string? ViolationCode, string? Message, string? PropertyName)
```

**What changed for this slice.** `IDispensaryStock.GetBatchesAsync` becomes `PlanAsync` (FEFO lines,
no write). `IStockMovementEnlister.Enlist` becomes `DeductAsync`. Register **reconciliation** needs
branch-wide balances, which `IStockLedger` does not expose — see OQ-16. Choosing a batch other than
FEFO's is still allowed, but through `ExplicitLines` + `OverrideReason` (HO-IMP-001 rule 5), which
audits the override; this slice no longer needs its own override record.

```

| Guarantee | Why |
|---|---|
| **G1** — concurrent enlistments can never drive a batch-location balance below zero | Two dispensers on the last vial. **Met as written**: HO-IMP-001 D-3 keeps `stock_position` as a trigger-maintained projection with `CHECK (on_hand >= 0 AND reserved >= 0 AND reserved <= on_hand)` inside the trigger's row lock, so the loser's `SaveChangesAsync` fails `23514`, mapped here to `409` |
| **G2** — Inventory refuses an expired batch in `Enlist` itself, not only in a read | BR-3 must hold for every consumer, including ones that skip `GetBatchesAsync` |
| **G3** — a `ClinicDispensary` location exists per branch | Dispensing always takes from the active branch's dispensary |

**From HO-SHP-001 — catalogue and prices**

| Assumed | Shape | Used for |
|---|---|---|
| `ICatalogueReader` | `GetSkusAsync(ids)` → `(SkuId, ProductId, ProductName, VariantLabel, Barcode?, IsActive)`; `GetProductAsync(id)` → `(ProductId, Name, Brand, Category, RequiresPrescription, IsActive, SkuIds)`; `SearchSkusAsync(term, includeClinicOnly)`; `SearchPrescriptionProductsAsync(term)` | Formulary SKU links and their search; diet authorisation product checks and picker; the gate's SKU → product resolution |
| `ISkuPriceReader` | `Money? PriceAt(Guid skuId, DateTimeOffset at)` | The dispensing `PriceSnapshot` (HO-ADM-001 ADR-049) |
| **Clinic-only SKUs** | A product/SKU may exist for stock and pricing without appearing in the storefront | Medicines must be SKUs to have batches (FR-IMP-4) but must never be sold online — **OQ-5** |

**From HO-ORD-001 / HO-SUB-001 / HO-BIL-001 — consumers only.** They consume §8.10's gate, the diet
events and `PrescriptionDispensedEvent`; this slice depends on none of them.

### 8.13 Ports and domain services

| Name | Kind | Notes |
|---|---|---|
| `PrescriptionSafetyAssessor`, `DoseConverter`, `AllergyConflictDetector`, `InteractionDetector` | pure domain services | §8.5, §8.6 |
| `DispensingBatchPlanner` | pure | §8.7 |
| `MedicineLabelComposer`, `DoseFormatter` | pure | §8.7 |
| `RefillScheduleCalculator` | pure | §8.11 |
| `PrescriptionActivityRules`, `PrescriptionActionRules`, `MedicineTermNormaliser`, `ControlledDrugEntryCanonicalForm` | Shared, pure | §8.4, §8.6, §8.9 |
| `ISafetyInputLoader` | port, Infrastructure | Loads, for one cat: latest weight, allergies, active prescriptions (with their formulary data), profile medications resolved against the formulary. **One place**, used by the check query, issue and renew |
| `IPrescriptionNumberAllocator` | port, Infrastructure | `SELECT nextval('pharmacy.prescription_number_seq')` |
| `IStaffWitnessVerifier` | port, Infrastructure | §8.9 |
| `IControlledDrugRegister`, `IControlledDrugDirectory`, `IFormularyReader` | ports, Infrastructure | §8.9 — **exported contracts** |
| `IDietAuthorisationGate` | port, Infrastructure | §8.10 — **exported contract** |
| `PrescriptionRefillDueGuard` | `INotificationSubjectGuard` | §8.11 |

### 8.14 Exceptions

Reuse `DomainRuleViolationException`. One new programming-error type,
`ControlledDrugRegisterImmutableException` (Infrastructure), mirroring HO-ACC-001's
`AuditEntryImmutableException`.

---

## 9. ADRs to record during this build

Append to `.ai/decisions/adr.md`. **This handover owns ADR-137 … ADR-156** (reserved by the
coordinator; the README ADR table is updated by its owner, not by this build). ADR-155 and ADR-156 are
left unused.

| ADR | Decision | Why it must be recorded |
|---|---|---|
| **ADR-137** | **`Prescription` bounded context**, namespace segment `Prescription` (already in `.ai/namespace-conventions.md`), schema `pharmacy`, owning the formulary, prescriptions, dispensing, the controlled-drug register and diet authorisations | `.ai/bounded-contexts.md` lists only `PrescriptionAggregate` and `DietAuthorisation`; the widening must be deliberate (OQ-11) |
| **ADR-138** | **The formulary is practice data**, administrator-maintained; prescriptions **snapshot** the medicine at issue; the controlled flag has its own command and audit action; a SKU links to at most one medicine | FR-PHM-4; a formulary edit must never rewrite a past prescription |
| **ADR-139** | **Safety assessment is one pure domain service run twice** (query and command). Out-of-range dose, allergy conflict and `Major`/`Contraindicated` interactions require acknowledgement; allergy acknowledgement needs a reason; acknowledgement is **bound by a fingerprint** to the warnings seen; a stale fingerprint is `409`. A missing or stale weight for a per-kg medicine has **no** acknowledgement path | US-PHM-02/03 must hold against direct API calls; an acknowledgement of yesterday's warnings is not consent to today's |
| **ADR-140** | **Allergy and interaction matching** by normalised term match over name, ingredients and classes, both directions for interactions; unresolved profile medications are listed, never ignored; the free-text limitation is stated on screen | HO-CAT-001 stores allergens as free text; the gap must be visible, not discovered |
| **ADR-141** | **Dose conversion** to mg/kg only where units are provably convertible; `NotCheckable` is always reported; the expected range is returned in mg/kg **and** absolute mg | A silent pass on an unconvertible dose is the dangerous failure |
| **ADR-142** | **Prescription lifecycle** = Appendix C (`issued → dispensed → completed`, `expired`, `cancelled`) with repeats counted as supplies; `ValidUntil` is the last dispensable day for every supply; "active" is derived read-side and never stored; every read treats a passed `ValidUntil` as expired regardless of the sweep | Appendix C has no repeat concept; the mapping must be fixed once |
| **ADR-143** | **Dispensing is its own root.** Dispense (and reversal) commit the dispensing record, the prescription's supply count, HO-IMP-001 ledger entries and controlled-drug register entries in **one** `SaveChangesAsync` — a recorded rule-1 exception, atomic by F23; FEFO suggested, deviation needs a reason; **expired stock has no override**; a supply is full, never partial | Stock, authorisation budget and the register must never disagree |
| **ADR-144** | **Controlled-drug register**: append-only root with no mutator, an EF immutability interceptor and PostgreSQL triggers; corrections are new linked entries that also enlist an Inventory adjustment when the quantity changes; branch-scoped; **exposed as `IControlledDrugRegister` for Surgery, Treatment and Inventory**, which enlist in their own save | C-2, D-5, FR-PHM-7; HO-SRG-001 depends on it |
| **ADR-145** | **Tamper evidence** by a per-branch SHA-256 hash chain in a separate append-only seal table, written by a worker pass after a 30 s settle delay; a verification query reports the first break and stale unsealed entries | C-2 says *tamper-evident*, not only append-only; triggers alone do not detect a superuser |
| **ADR-146** | **A witness authenticates with their own identifier and password in the same request**; must hold a witness role and differ from the actor; failures are generic, audited and count toward the witness account's lockout; the password never leaves the command | BR-7 "second staff witness"; identifier + password is the only credential the product has (no OTP, no 2FA) |
| **ADR-147** | **Labels are frozen at dispensing** into a snapshot on the dispensing record; printing is the browser's; reprints print the same snapshot and are counted and audited | A label must say what was handed over, forever |
| **ADR-148** | **Repeats** are owned requests on the prescription, dispensable without a visit inside `ValidUntil`; outside it, refused with the date and the next step; **renewal** is a vet act that issues a new, fully re-checked prescription and links the old one — a recorded rule-1 exception (two prescriptions, one save) | FR-PHM-6 "new vet authorisation"; the link keeps the history readable |
| **ADR-149** | **Diet authorisations name the product** (the vet prescribes "Renal diet X", not the 2 kg bag) for one cat, with branch-local dates; the **gate takes a SKU** as `.ai/flows/prescription-diet-flow.md` says and resolves its product; validity is evaluated on every call so locking needs no job; the sweep only records expiry, raises the event and notifies | The flow doc says "specific SKU"; this refines it and must say so |
| **ADR-150** | **Pharmacy messages**: five transactional templates; refill reminders are a timed producer with a deduplication key and a `PrescriptionRefillDue` guard; every other message is enqueued by the command that changed state | HO-NTF-001 §4.2 reserved the refill contract for this slice |
| **ADR-151** | **Pharmacy never reads or writes another area's tables.** Visits are loaded read-only through the aggregate repository; Inventory, Catalogue and Accounts through their ports; the assumed contracts of §8.12 are recorded so their owners can confirm them | The README delivery order builds IMP and SHP first precisely so this slice never grows its own stock code |
| **ADR-152** | **Customer merge**: prescriptions, dispensings and diet authorisations are reassigned to the survivor; **register entries and seals are never rewritten** — reads resolve merged customer ids. Anonymisation leaves clinical records (labels included) intact under C-3 | An append-only register cannot be "fixed up" by a merge |
| **ADR-153** | **Authorization mapping** from Appendix D: prescribe, renew, cancel, decline repeats, authorise diets and correct the register are **Vet only**; dispensing is Reception, Nurse, Vet, Administrator; reversal Vet, Administrator; the register is read by Nurse, Vet, Inventory, Administrator; the formulary is managed by Administrator (FR-PHM-4) | Appendix D is indicative; the mapping is an inference to be confirmed (OQ-1, OQ-8) |
| **ADR-154** | **Dispensing snapshots a price per line** for Billing; an unpriced SKU does not block care — the line's snapshot is null and the response says so | ADR-049's contract; blocking a medicine over a missing price is the wrong failure (OQ-6) |

### 9.1 Open questions

| # | Question | Owner | Default in this build |
|---|---|---|---|
| OQ-1 | Who may **witness** a controlled drug, and who may **dispense** one? Appendix D lets Reception dispense | Owner + clinical lead (SRS Q-1) | Any dispensing role may dispense; the witness must be **Vet or Nurse** and not the dispenser — **confirmed by the owner 2026-10-10** |
| OQ-2 | Statutory register format, label wording and any regulator return (SRS Q-1) | Owner / legal | Register fields per FR-PHM-7/FR-SRG-8; CSV export; label warnings *"For animal treatment only. Keep out of reach of children."* |
| OQ-3 | May a vet renew a prescription **without** an open visit (remote renewal)? | Owner + clinical lead | Yes, flagged `IsRemoteRenewal` and fully re-checked — **confirmed by the owner 2026-10-10** |
| OQ-4 | Should a recent weight be required for **every** prescription, not only per-kg medicines? | Clinical lead | Per-kg medicines only |
| OQ-5 | **HO-SHP-001 must support clinic-only SKUs** (stocked and priced, never on the storefront) for medicines | HO-SHP-001 author | Assumed; §20.2 if not |
| OQ-6 | May a medicine with no SKU price be dispensed? | Owner + accountant | Yes; snapshot null, flagged to Billing |
| OQ-7 | Partial supply when the dispensary is short | Owner + clinical lead | No — full supply only; staff reorder through Inventory |
| OQ-8 | Should the Administrator be able to **correct** the register? Appendix D gives Admin **R** | Owner | No — Vet only — **confirmed by the owner 2026-10-10** |
| OQ-9 | Recent-weight window (`pharmacy.doseWeightMaxAgeDays`) — kittens change weekly | Clinical lead | 30 days, one value |
| OQ-10 | Register reconciliation against **branch** stock (all locations) or the dispensary only? | Owner + inventory | Branch |
| OQ-11 | `.ai/bounded-contexts.md` should list the widened `Prescription` context | Architect | ADR-137 records it |
| OQ-12 | NFR-SEC-8 clinical-record access logging | Architect + HO-EMR-001 | Adopt HO-EMR-001's mechanism by an Amended row when it exists |
| OQ-13 | Diet authorisation "expiring soon" reminder to the owner? | Owner | No — only on expiry |
| OQ-14 | Where does an owner collect a repeat — any branch, or the prescribing branch? | Owner | The branch they choose on the request; any branch may dispense |
| OQ-15 | HO-EMR-002 needs a "cat's open visits" read to launch the composer; who provides it? | HO-EMR-001 author | HO-EMR-001 (HO-PHM-002 OQ-5) |
| ~~OQ-16~~ | **Closed 2026-09-23.** HO-IMP-001 §8.8 adds `IStockLedger.GetBranchBalancesAsync(branchId, skuIds)` → per-batch `OnHand` and `Reserved`. US-PHM-07's reconciliation is unblocked and can distinguish "missing" from "reserved" | — | — |
| ~~OQ-17~~ | **Closed 2026-09-23.** HO-IMP-001 §8.8 adds `IStockLedger.AdjustAsync(adjustment, reference, party)`, enlisting like every other method, so C7 writes the register correction and the stock movement in one save. See HO-IMP-001 OQ-21: whether a pharmacy correction needs the Inventory approver **as well as** the witnessing vet is an explicit owner decision, not an assumption either side may make | — | — |

---

## 10. Enumerations, constants and violation codes

### 10.1 Enumerations — `Shared/Common/Enums/`, derived from `Enumeration`, persisted by `Value`

| Type | Members |
|---|---|
| `MedicineForm` | `Tablet`, `Capsule`, `OralLiquid`, `OralPaste`, `Powder`, `Injection`, `Topical`, `EyeDrops`, `EarDrops`, `Transdermal`, `Other` |
| `StrengthUnit` | `Mg`, `Mcg`, `G`, `Iu`, `Percent` |
| `DispensingUnit` | `Tablet`, `Capsule`, `Millilitre`, `Gram`, `Vial`, `Ampoule`, `Tube`, `Bottle`, `Patch`, `Pipette`, `Dose` |
| `DoseUnit` | `Mg`, `Mcg`, `G`, `Ml`, `Tablet`, `Capsule`, `Drop`, `Application`, `Iu` |
| `AdministrationRoute` | `Oral`, `Subcutaneous`, `Intramuscular`, `Intravenous`, `Topical`, `Ophthalmic`, `Otic`, `Transdermal`, `Inhaled`, `Rectal` |
| `DoseFrequency` | `Once`, `OnceDaily`, `TwiceDaily`, `ThreeTimesDaily`, `FourTimesDaily`, `EveryOtherDay`, `Weekly`, `AsNeeded` — each with a label (*"twice daily"*) used by `DoseFormatter` |
| `DosingBasis` | `PerKg`, `Fixed` |
| `InteractionSeverity` | `Minor` (1), `Moderate` (2), `Major` (3), `Contraindicated` (4) — ordered by `Value` |
| `DoseCheckOutcome` | `WithinRange`, `BelowRange`, `AboveRange`, `NotCheckable` |
| `SafetyWarningKind` | `DoseBelowRange`, `DoseAboveRange`, `DoseNotCheckable`, `AllergyConflict`, `Interaction` |
| `PrescriptionStatus` | `Issued`, `Dispensed`, `Completed`, `Expired`, `Cancelled` |
| `RepeatRequestStatus` | `Requested`, `Dispensed`, `Declined`, `Withdrawn` |
| `DispensingStatus` | `Dispensed`, `Reversed` |
| `ControlledDrugEntryKind` | `OpeningBalance`, `Receipt`, `Dispensed`, `Administered`, `Wastage`, `Returned`, `Adjustment`, `Correction` |
| `ControlledDrugSourceType` | `Dispensing`, `DispensingReversal`, `Treatment`, `Surgery`, `GoodsReceipt`, `StockAdjustment`, `Correction` |
| `DietAuthorisationStatus` | `Active`, `Expired`, `Revoked` |
| `DietGateOutcome` | `NotRequired`, `Authorised`, `NoAuthorisation`, `AuthorisationExpired`, `CatNotOwned`, `CatDeceased` |

HO-NTF-001's `NotificationSubjectKind` gains **`PrescriptionRefillDue`** (append only).
`StockLocationKind` and `StockMovementKind` are **HO-IMP-001's** — consumed, never duplicated.

### 10.2 Settings — appended to HO-ADM-001's `SettingDefinitions`

| Key | Group | Type | Default | Bounds | Branch override |
|---|---|---|---|---|---|
| `pharmacy.doseWeightMaxAgeDays` | Pharmacy | Integer | 30 | 1–365 | ❌ |
| `pharmacy.defaultDispenseWindowDays` | Pharmacy | Integer | 30 | 1–365 | ❌ |
| `pharmacy.maxRepeatValidityDays` | Pharmacy | Integer | 180 | 7–730 | ❌ |
| `pharmacy.refillReminderLeadDays` | Pharmacy | Integer | 3 | 0–30 (0 = off) | ❌ |
| `pharmacy.maxDietAuthorisationDays` | Pharmacy | Integer | 365 | 1–730 | ❌ |
| `pharmacy.dispensingReversalHours` | Pharmacy | Integer | 24 | 0–168 (0 = off) | ✅ |

`Pharmacy` is a new group value if HO-ADM-001 models groups as an enumeration; no migration (values
are rows).

### 10.3 Other constants

`PrescriptionRules` (`MaxRepeats = 12`, `MaxDurationDays = 365`, `MaxInstructionsLength = 1000`,
`NumberFormat = "RX-{0:yyyy}-{1:000000}"`) · `DispensingRules.MaxLinesPerDispensing = 5` ·
`RepeatRules.MaxRequestsPerPrescription = 30` · `ControlledDrugRules` (`WitnessRoles = [Vet, Nurse]`,
`SealSettleSeconds = 30`, `SealBatchSize = 500`, `UnsealedAlertMinutes = 10`) · `FormularyRules`
(`MaxInteractions = 50`, `MaxLinkedSkus = 10`) · `LabelRules.StandardWarnings` · `PharmacyMessages`
(shared message text) · the five template definitions of §8.11 appended to
`NotificationTemplateCatalogue`.

### 10.4 Violation codes — append to `BusinessViolationCodes`

Formulary: `FormularyCodeAlreadyExists` · `FormularyMedicineNotFound` · `InvalidStrength` ·
`DoseRangeRequired` · `InvalidDoseRange` · `InvalidInteraction` · `ControlledScheduleRequired` ·
`ControlledFlagChangeReasonRequired` · `SkuNotFound` · `SkuAlreadyLinkedToMedicine` ·
`MedicineInactive`.

Prescribing: `VisitNotFound` · `VisitCatMismatch` · `VisitNotOpen` · `CatNotTreatable` ·
`RouteNotAllowedForMedicine` · `InvalidDuration` · `InvalidQuantity` · `InvalidValidity` ·
`RepeatsRequireValidity` · **`RecentWeightRequired`** · **`PrescriptionWarningsNotAcknowledged`** ·
**`PrescriptionWarningsChanged`** · `AllergyAcknowledgementReasonRequired` ·
`PrescriptionNotFound` · `InvalidPrescriptionTransition` · `CancellationReasonRequired` ·
`PrescriptionAlreadyRenewed`.

Dispensing: `MedicineNotStocked` · `SkuNotLinkedToMedicine` · **`BatchExpired`** ·
`BatchNotDispensable` · **`OnlyExpiredStockAvailable`** · `InsufficientDispensaryStock` ·
`DispenseQuantityMismatch` · `FefoOverrideReasonRequired` · `TooManyDispensingLines` ·
**`PrescriptionValidityEnded`** · `NoSuppliesRemaining` · `DispensingNotFound` ·
`DispensingNotReversible` · `ReversalWindowPassed` · `ReversalReasonRequired`.

Witness and register: **`WitnessRequired`** · **`WitnessNotVerified`** ·
`WitnessMustDifferFromDispenser` · `InvalidRegisterQuantity` · `RegisterEntryNotFound` ·
`CorrectionReasonRequired` · `CorrectionTargetMismatch`.

Repeats: `NotRepeatable` · `FirstSupplyNotDispensed` · `NoRepeatsRemaining` ·
`RepeatRequestAlreadyOpen` · `RepeatRequestNotFound` · `DeclineReasonRequired`.

Diet: `ProductNotFound` · `ProductNotPrescriptionDiet` · `DietAuthorisationAlreadyActive` ·
`DietAuthorisationNotFound` · `InvalidDietValidity` · `DietAuthorisationNotExtendable` ·
`RevocationReasonRequired`.

`InsufficientBatchStock` is **HO-IMP-001's** code, passed through.

---

## 11. Specified changes — write side

### 11.1 Commands — `Application/Commands/Prescription/`

Namespace `CatEcommerce.Application.Commands.Prescription`. Data only; ids minted in the controller;
`ValidateCommand = true` everywhere (F3).

| # | Command | Sent by | Story | Phase |
|---|---|---|---|---|
| C1 | `CreateFormularyMedicineCommand` (`MedicineId`, `Code`, `Name`, `ActiveIngredients[]`, `DrugClasses[]`, `Form`, `Strength`, `DefaultRoute`, `AllowedRoutes[]`, `DosingBasis`, `DoseRange?`, `Contraindications`, `Interactions[]`, `DispensingUnit`, `LinkedSkuIds[]`) | Admin | PHM-04 AC1 | B |
| C2 | `UpdateFormularyMedicineCommand` (same minus `Code`, plus `ExpectedVersion`) | Admin | PHM-04 AC1 | B |
| C3 | `SetFormularyMedicineControlledCommand` (`MedicineId`, `IsControlled`, `ControlledSchedule?`, `Reason?`) | Admin | PHM-04 AC2 | B |
| C4 | `SetFormularyMedicineActiveCommand` (`MedicineId`, `IsActive`) | Admin | PHM-04 | B |
| C5 | `IssuePrescriptionCommand` (`PrescriptionId`, `CatId`, `VisitId`, `MedicineId`, `Dose`, `Route`, `Frequency`, `DurationDays`, `Quantity`, `Instructions`, `StartsOn?`, `IsRepeatable`, `RepeatsAuthorised`, `ValidUntil?`, `AcknowledgedWarningsFingerprint?`, `AllergyAcknowledgementReason?`, `DoseAcknowledgementNote?`) | Vet | PHM-01, 02, 03 | C |
| C6 | `CancelPrescriptionCommand` (`PrescriptionId`, `Reason`) | Vet | PHM-01 | C |
| C7 | `RecordControlledDrugCorrectionCommand` (`EntryId`, `CorrectsEntryId`, `CorrectedDoseText?`, `CorrectedCatId?`, `QuantityDelta`, `Notes?`, `Reason`, `WitnessIdentifier`, `WitnessPassword`) | Vet | FR-PHM-7 | D |
| C8 | `SealControlledDrugEntriesCommand` (`BranchId`) | Worker | C-2 | D |
| C9 | `DispensePrescriptionCommand` (`DispensingId`, `PrescriptionId`, `Lines[]` (`SkuId`, `BatchId`, `Quantity`), `FefoOverrideReason?`, `RepeatRequestId?`, `WitnessIdentifier?`, `WitnessPassword?`) — branch from the active branch | Dispenser | PHM-05, 04 AC2, 06 AC1 | E |
| C10 | `RecordLabelPrintCommand` (`DispensingId`) | Dispenser | PHM-05 | E |
| C11 | `ReverseDispensingCommand` (`DispensingId`, `Reason`, `WitnessIdentifier?`, `WitnessPassword?`) | Vet, Admin | PHM-05 | E |
| C12 | `RequestRepeatCommand` (`PrescriptionId`, `RepeatRequestId`, `PreferredBranchId`, `Note?`) | Customer | PHM-06 | F |
| C13 | `WithdrawRepeatRequestCommand` (`PrescriptionId`, `RepeatRequestId`) | Customer | PHM-06 | F |
| C14 | `DeclineRepeatRequestCommand` (`PrescriptionId`, `RepeatRequestId`, `Reason`) | Vet | PHM-06 | F |
| C15 | `RenewPrescriptionCommand` (`NewPrescriptionId`, `FromPrescriptionId`, `VisitId?`, the editable fields of C5, the acknowledgement fields) | Vet | PHM-06 AC2 | F |
| C16 | `ExpirePrescriptionCommand` (`PrescriptionId`) | Worker | ADR-142 | F |
| C17 | `AuthoriseDietCommand` (`AuthorisationId`, `CatId`, `VisitId`, `ProductId`, `ValidFrom`, `ValidTo`, `Notes?`) | Vet | PHM-08 AC1 | G |
| C18 | `ExtendDietAuthorisationCommand` (`AuthorisationId`, `ValidTo`) · `RevokeDietAuthorisationCommand` (`AuthorisationId`, `Reason`) | Vet | PHM-08 | G |
| C19 | `ExpireDietAuthorisationCommand` (`AuthorisationId`) | Worker | PHM-08 AC2 | G |

**Every command carrying `WitnessPassword` overrides `ToString()`** to omit it, and its validator
never echoes it (U-X2).

### 11.2 Validators — `CommandHandlers/Validators/` (flat), registered as `IValidator<T>`

Shape only: ids non-empty; text lengths; enumeration membership; decimals positive with their scale;
`DurationDays` 1…365; `RepeatsAuthorised` 0…12 and > 0 iff `IsRepeatable`; `ValidUntil` present when
repeatable; `Lines` 1…5 with distinct `BatchId`s; `ValidTo ≥ ValidFrom`; witness fields both present
or both absent; `Interactions` each with exactly one target. **Dose ranges, weight recency, visit
state, validity windows, FEFO and stock are aggregate, domain-service or handler rules.**

### 11.3 Mappers and DTOs

`FormularyMapper`, `PrescriptionMapper`, `DispensingMapper`, `ControlledDrugRegisterMapper`,
`DietAuthorisationMapper` (static `ToDomainDto()` extensions). DTOs in `Domain/Models/`:
`FormularyMedicineDto`, `StrengthInput`, `DoseRangeInput`, `InteractionInput`, `PrescriptionIssueDto`,
`SafetyInput`, `SafetyAssessment`, `DispenseLineInput`, `DispensePlan`, `MedicineLabelSnapshotDto`,
`ControlledDrugRegisterEntryDto`, `DietAuthorisationDto`, plus the §8.12 port records.

### 11.4 Command handlers — `CommandHandlers/Prescription/`

Every prescribing handler (C5, C15) follows one sequence, implemented once in `PrescribingSteps`
(Application, a static helper over the ports — **not a service layer**):

1. Caller holds `Vet` (defence in depth behind `RequireActionFilter`, ADR-009) → else the C11 denial
   path of HO-ACC-001 (`403` + `AuthorizationDenied` audit) — **US-CLN-22**.
2. Cat exists and alive (`CatNotTreatable`); visit rules (§8.12) — for C15, only when a visit is given.
3. Medicine exists and active; route allowed.
4. Load `SafetyInput` through `ISafetyInputLoader`; `PrescriptionSafetyAssessor.Assess`.
5. Weight requirement not satisfied → `RecentWeightRequired`.
6. Acknowledgement rules (§8.5 steps 1–4).
7. Allocate the number; `Issue`; save.

| Handler | Specifics |
|---|---|
| **C1–C4 Formulary** | Code uniqueness pre-check + index; SKU links through `ICatalogueReader` (exist, active, not linked elsewhere); C3 with `IsControlled = false` on a medicine having register entries needs `Reason` |
| **C5 Issue** | `PrescribingSteps`; `ValidUntil` defaulted for non-repeatable prescriptions from `pharmacy.defaultDispenseWindowDays` |
| **C6 Cancel** | Vet; open repeat request declined with the reason and `pharmacy.repeat-declined` enqueued |
| **C7 Correction** | Vet; target entry exists in the active branch; witness verified; `ControlledDrugRegisterEntryAggregate.Record(Correction)`; non-zero delta → `IStockLedger.AdjustAsync(StockAdjustment(…, Reason, ApprovedByStaffId?), StockReference(ControlledDrugCorrection, entryId), StockParty(…))` **in the same save** |
| **C8 Seal** | §8.9; loads unsealed entries in order, computes the chain from the branch's last seal, **adds** every seal to the scoped context and commits them with **one** repository `SaveAsync` of the last seal (F23 commits all tracked seals together); a `23505` on the chain index → success with nothing written (the other worker won) |
| **C9 Dispense** | §8.7 in full |
| **C10 Label print** | `RecordLabelPrint`; `UpdateAsync` |
| **C11 Reverse** | §8.7 reversal |
| **C12 Request repeat** | Customer; prescription's cat owned by the caller **now** (`404` otherwise); cat alive; `RequestRepeat`; `PreferredBranchId` an active branch |
| **C13 Withdraw** | Own request only; anything else → success, nothing written |
| **C14 Decline** | Vet; enqueue `pharmacy.repeat-declined` to the cat's current owner |
| **C15 Renew** | `PrescribingSteps`, then `MarkRenewed` (+ cancel) on the old one; one save |
| **C16 Expire** | Idempotent: not expirable → success, nothing written |
| **C17 Authorise diet** | Vet; visit rules; product via `ICatalogueReader`; enqueue `pharmacy.diet-authorised`; `23505` on `ix_diet_authorisation_active` → `400 DietAuthorisationAlreadyActive` |
| **C18 Extend / revoke** | Vet |
| **C19 Expire diet** | §8.10; idempotent |

### 11.5 Domain events — `Events/Prescription/`

**Ids, dates, instants, enumerations, decimals and booleans only — no instructions, reasons, allergen
text or names** (U-X1).

`FormularyMedicineCreatedEvent` · `FormularyMedicineUpdatedEvent` ·
`FormularyMedicineControlledStatusChangedEvent` (`MedicineId`, `IsControlled`) ·
`FormularyMedicineActivationChangedEvent` · `PrescriptionIssuedEvent` (`PrescriptionId`, `CatId`,
`CustomerId`, `VisitId`, `MedicineId`, `IsControlled`, `HadAcknowledgedWarnings`) ·
`PrescriptionCancelledEvent` · `PrescriptionExpiredEvent` · **`PrescriptionDispensedEvent`**
(`DispensingId`, `PrescriptionId`, `BranchId`, `CustomerId`, `CatId`, `VisitId`, `SupplyNumber`,
`Lines[]` of `SkuId`, `BatchId`, `Quantity`, `UnitPrice?`, `CurrencyCode?`) · **`DispensingReversedEvent`**
· `LabelPrintedEvent` · `RepeatRequestedEvent` · `RepeatRequestDeclinedEvent` ·
`ControlledDrugRegisterEntryRecordedEvent` (`EntryId`, `BranchId`, `MedicineId`, `Kind`) ·
**`DietAuthorisationIssuedEvent`** · **`DietAuthorisationExtendedEvent`** ·
**`DietAuthorisationExpiredEvent`** · **`DietAuthorisationRevokedEvent`** (each `AuthorisationId`,
`CatId`, `CustomerId`, `ProductId`, `ValidTo`).

Bold events are **integration contracts** for HO-BIL-001, HO-SUB-001 and HO-ORD-001.

### 11.6 Domain event handlers — `DomainEventHandlers/Prescription/`

| Handler | Handles | Does |
|---|---|---|
| `ReassignPharmacyRecordsOnCustomerMergedHandler` | HO-ACC-001 `CustomerMergedEvent` | Prescriptions, dispensings and diet authorisations of the duplicate → the survivor; idempotent; **register entries untouched** (ADR-152) |
| `AuditPrescriptionAcknowledgementHandler` | `PrescriptionIssuedEvent` with `HadAcknowledgedWarnings` | `BusinessEvent` audit row `PrescriptionWarningsAcknowledged` — acknowledging a dose or allergy warning is a clinical decision worth a line of its own, joined by `CorrelationId` to the `DataChange` row |
| `AuditLabelPrintedHandler` | `LabelPrintedEvent` | `BusinessEvent` row with the print count |

All loop with one `UpdateAsync` per row where they mutate, and **never enqueue** (HO-NTF-001 ADR-057).
Data changes are audited by HO-ACC-001's interceptor; **`pharmacy.cd_register_entry` inserts are
audited normally** (an insert row is harmless and makes the register visible in the audit search).

---

## 12. Specified changes — Infrastructure, read side, Api, host

### 12.1 Persistence

Six `DbSet`s (`FormularyMedicines`, `Prescriptions`, `Dispensings`, `ControlledDrugRegisterEntries`,
`ControlledDrugRegisterSeals`, `DietAuthorisations`); one configuration per root; schema `pharmacy`;
HO-ACC-001 §12.1 conventions (the five F15 lines, `ValueGeneratedNever()` on every root **and owned**
id — ADR-021, enumerations by `Value`, `DateTimeOffset` → `timestamptz`, `DateOnly` → `date`);
quantities `numeric(10,2)`; strengths and mg/kg `numeric(12,4)`; `Money` as HO-ADM-001.

| Index / constraint | Kind |
|---|---|
| `formulary_medicine.code` | **UNIQUE** `ix_formulary_medicine_code` |
| `formulary_medicine_sku.sku_id` | **UNIQUE** `ix_formulary_medicine_sku` — the owned `LinkedSkus` table, so the one-SKU-one-medicine rule has a database guard |
| `formulary_medicine (is_active, name)` | picker search |
| `prescription.prescription_number` | **UNIQUE** |
| `prescription (cat_id, status)`, `(visit_id)`, `(branch_id, status, issued_on)`, `(customer_id)` | cat list, visit list, pharmacy queue, merge |
| `prescription (status, valid_until)` | expiry sweep |
| `repeat_request (status, preferred_branch_id)` | pharmacy queue |
| `dispensing (prescription_id, supply_number)` | **UNIQUE** `ix_dispensing_supply` — a supply number is used once |
| `dispensing_line (batch_id)` | recall trace |
| `cd_register_entry.entry_number` | **UNIQUE**; default `nextval('pharmacy.cd_register_entry_number_seq')` (V1) |
| `cd_register_entry (branch_id, medicine_id, entry_number)`, `(branch_id, batch_id)`, `(source_type, source_id)`, `(corrects_entry_id)` | register views, reconciliation, corrections |
| `cd_register_seal (branch_id, chain_position)` | **UNIQUE** `ix_cd_register_seal_chain` |
| `diet_authorisation (cat_id, product_id)` **UNIQUE** filtered `WHERE status = Active` | `ix_diet_authorisation_active` |
| `diet_authorisation (status, valid_to)`, `(customer_id)` | sweep, portal, merge |

Status values in filtered predicates are **`Enumeration.Value` integers written from the enumeration**
in the migration (U-X3), never literals.

**Migrations**, each read before commit:
- `AddPharmacyFormulary` (B) — schema, tables, indexes.
- `AddPharmacyPrescriptions` (C) — prescriptions, warnings, repeat requests, `prescription_number_seq`.
- `AddControlledDrugRegister` (D) — entries, seals, `cd_register_entry_number_seq`, **the four
  triggers** (`migrationBuilder.Sql`, one function raising *"controlled-drug register entries are
  append-only"*). The `Down` drops the triggers and tables — and is **never run** against a database
  holding real entries (§20.8).
- `AddPharmacyDispensing` (E).
- `AddDietAuthorisations` (G).

Phases A and F add no table (F adds no column: repeat requests arrive in C's migration).

**Interceptors.** Register `ControlledDrugRegisterImmutabilityInterceptor` beside HO-ACC-001's two in
the `AddDbContext` call. HO-ACC-001's `AuditRedactionRules` gains `WitnessPassword` explicitly (it is
already caught by the `/password/i` pattern; the explicit entry documents it).

### 12.2 Infrastructure services and the worker

| Class | Notes |
|---|---|
| `Pharmacy/SafetyInputLoader` | §8.13; `AsNoTracking`; latest weight is the newest `cat_weight_record` row (ADR-023); active prescriptions via `PrescriptionActivityRules` |
| `Pharmacy/PrescriptionNumberAllocator` | `nextval` over the context's connection, inside the current transaction |
| `Pharmacy/StaffWitnessVerifier` | §8.9; reuses HO-ACC-001's hasher, staff repository filter and throttle; writes the two audit actions |
| `Pharmacy/EfControlledDrugRegister` | Builds the aggregate and **adds it to the scoped context without saving** — exactly `EfNotificationOutbox`'s shape |
| `Pharmacy/ControlledDrugDirectory`, `Pharmacy/FormularyReader` | Read-only |
| `Pharmacy/DietAuthorisationGate` | §8.10; read-only; "today" from HO-ADM-001's main-branch zone |
| `Pharmacy/DispensingBatchConsumptionSource` | §8.13 |
| `Auditing/ControlledDrugRegisterImmutabilityInterceptor` | §8.9 layer 2 |
| `Notifications/Guards/PrescriptionRefillDueGuard` | §8.11 |
| `Notifications/NotificationLinkBuilder` | The three methods of §8.11 added |

**`PharmacyWorker : BackgroundService`** (Api, `Background/`) — the same shape and rules as HO-APT-001's
`ClinicWorker` (a scope per unit of work, never throws out of `ExecuteAsync`, disabled in `Testing`,
`PharmacyWorkCycle` callable by tests). One pass every `Pharmacy:Worker:IntervalSeconds` (60):

1. **Seal** the register for every active branch (C8).
2. **Expire** diet authorisations with `ValidTo < today` (C19, one per authorisation, ≤ 200 per pass).
3. **Expire** prescriptions with `ValidUntil < today` and supplies left (C16, ≤ 200 per pass).
4. **Refill reminders** — at most once per `Pharmacy:Worker:RefillSweepIntervalMinutes` (60):
   `GetDueRefillRemindersQuery(today, leadDays)` → `RequestNotificationCommand` per row (§8.11).

"Today" is the main branch's local date (HO-NTF-001 §8.12's rule). Configuration
`Pharmacy:Worker:Enabled`, `IntervalSeconds`, `RefillSweepIntervalMinutes`. **Locking a diet and
refusing an expired prescription never wait for this worker** (ADR-142, ADR-149).

### 12.3 Authorization

Appended to the one `ActionRoles` table (HO-ADM-001 §12.3):

| Action | Staff roles | Customer (own) | Endpoints |
|---|---|---|---|
| `Formulary.View` | Reception, Nurse, Vet, Inventory, Administrator | — | formulary list, detail, picker |
| `Formulary.Manage` | Administrator | — | C1–C4, SKU search |
| `Prescriptions.View` | Reception, Nurse, Vet, Administrator | — | cat and visit prescriptions, detail |
| `Prescriptions.Issue` | **Vet** | — | safety check, C5, C6, C14, C15 — **US-CLN-22, AC-18** |
| `Dispensing.View` | Reception, Nurse, Vet, Inventory, Administrator | — | queue, lookup, dispensing detail |
| `Dispensing.Dispense` | Reception, Nurse, Vet, Administrator | — | dispense plan, C9, C10 |
| `Dispensing.Reverse` | Vet, Administrator | — | C11 |
| `ControlledDrugs.View` | Nurse, Vet, Inventory, Administrator | — | register, entry, balances, verification, export |
| `ControlledDrugs.Correct` | **Vet** | — | C7 |
| `DietAuthorisations.View` | Reception, Nurse, Vet, ShopManager, Administrator | — | cat authorisations |
| `DietAuthorisations.Issue` | **Vet** | — | C17, C18, product picker |

Customer endpoints carry `[RequireCustomerSession]`; each handler resolves the prescription or
authorisation **through its cat's current owner**: anything not the caller's is **`404`**
(HO-ADM-001 §12.3's rule). Every endpoint carries exactly one access marker
(`EndpointAccessConventionTests`, HO-ADM-001 ADR-043). The mapping is an inference from Appendix D
(ADR-153) — flag it in the ACCEPTANCE report.

### 12.4 Api — the contract HO-PHM-002 consumes

JSON camelCase; instants ISO-8601 with offset; dates `yyyy-MM-dd`; money as HO-ADM-001 §12.4 (amount
as a string); **every quantity, dose and weight is returned with its unit** as
`{ "amount": "0.30", "unit": "Ml" }` (UI-8 — the client never has to guess a unit).

**Formulary**

| Method | Route | Access | Command / query → success |
|---|---|---|---|
| `GET` | `/api/staff/formulary?search&isControlled&isActive&pageNumber&pageSize` | `Formulary.View` | paged `FormularyMedicineSummary`: `id`, `code`, `name`, `form`, `strengthText`, `dosingBasis`, `isControlled`, `isActive`, `isStocked` (has linked SKUs) |
| `GET` | `/api/staff/formulary/{medicineId}` | `Formulary.View` | full detail incl. `strength`, `doseRange`, `interactions[]` (with the other medicine's name), `linkedSkus[]` (`skuId`, `productName`, `variantLabel`), `version` |
| `GET` | `/api/admin/formulary/sku-search?term` | `Formulary.Manage` | SKUs (clinic-only included) through `ICatalogueReader.SearchSkusAsync`: `skuId`, `productName`, `variantLabel`, `barcode?`, `linkedMedicineId?` — for the editor's linked-SKU picker |
| `POST` | `/api/admin/formulary` | `Formulary.Manage` | C1 → `201 { medicineId }` |
| `PUT` | `/api/admin/formulary/{medicineId}` | `Formulary.Manage` | C2 → `204`; `409` stale version |
| `PUT` | `/api/admin/formulary/{medicineId}/controlled` | `Formulary.Manage` | C3 → `204` |
| `PUT` | `/api/admin/formulary/{medicineId}/active` | `Formulary.Manage` | C4 → `204` |

**Prescribing** (staff)

| Method | Route | Access | Command / query → success |
|---|---|---|---|
| `POST` | `/api/staff/cats/{catId}/prescriptions/safety-check` | `Prescriptions.Issue` | `CheckPrescriptionSafetyQuery` (body = the C5 fields without acknowledgements) → `200 SafetyAssessmentView`: `weight` (`{ requirement: Satisfied\|Missing\|Stale, latest?: { kg, recordedOn, ageDays } }`), `doseCheck` (`outcome`, `doseMg?`, `doseMgPerKg?`, `expectedRange?`: `{ minMgPerKg, maxMgPerKg, minMg, maxMg }`), `warnings[]` (`kind`, `blocking`, `requiresAcknowledgement`, `severity?`, `subject`, `detail`), `information[]`, `contraindications`, `hasRecordedAllergies`, **`fingerprint`**. **Writes nothing** |
| `POST` | `/api/staff/cats/{catId}/prescriptions` | `Prescriptions.Issue` | C5 → `201 { prescriptionId, prescriptionNumber }`; `400 RecentWeightRequired`; `400 PrescriptionWarningsNotAcknowledged` (+ `assessment`); `409 PrescriptionWarningsChanged` (+ `assessment`); `400 VisitNotOpen` |
| `GET` | `/api/staff/cats/{catId}/prescriptions?scope=active\|all&pageNumber&pageSize` | `Prescriptions.View` | paged `PrescriptionSummary`: `id`, `prescriptionNumber`, `medicine` (name, strength, form, `isControlled`), `dose`, `route`, `frequency`, `durationDays`, `quantity`, `status`, `isActive`, `courseEndsOn`, `remainingDays`, `suppliesDispensed`, `suppliesAuthorised`, `validUntil`, `prescriberName`, `issuedOn`, `hasAcknowledgedWarnings` |
| `GET` | `/api/staff/prescriptions/{prescriptionId}` | `Prescriptions.View` | detail: summary + `instructions`, `visitId`, `isRemoteRenewal`, `safetyRecord` (weight used, dose/kg, range, outcome, acknowledged by/on, reasons), `warnings[]`, `dispensings[]` (id, supply, date, by, lines with lot and expiry, status), `repeatRequests[]`, `renewedFrom?`, `renewedBy?`, **`allowedActions[]`** (`Dispense`, `Cancel`, `Renew`, `DeclineRepeat`) computed from the caller's roles and the state |
| `POST` | `/api/staff/prescriptions/{prescriptionId}/cancel` | `Prescriptions.Issue` | C6 → `204` |
| `POST` | `/api/staff/prescriptions/{prescriptionId}/renew` | `Prescriptions.Issue` | C15 → `201 { prescriptionId, prescriptionNumber }`; same `400`/`409` as C5 |
| `POST` | `/api/staff/prescriptions/{prescriptionId}/repeat-requests/{requestId}/decline` | `Prescriptions.Issue` | C14 → `204` |
| `GET` | `/api/staff/visits/{visitId}/prescriptions` | `Prescriptions.View` | `PrescriptionSummary[]` + the visit's diet authorisations — **HO-EMR-001's contract** |

**Dispensing** (staff; branch from `X-Branch-Id`)

| Method | Route | Access | Command / query → success |
|---|---|---|---|
| `GET` | `/api/staff/pharmacy/queue` | `Dispensing.View` | `{ generatedOn, awaitingFirstSupply: [PrescriptionSummary + catName, catIsHighRisk, customerName], repeatRequests: [{ requestId, prescriptionId, prescriptionNumber, catName, customerName, medicine, requestedOn, note }] }` for the active branch (repeat requests by `preferredBranchId`) |
| `GET` | `/api/staff/pharmacy/lookup?number` | `Dispensing.View` | `{ prescriptionId }` or `404` — the scanned or typed `RX-…` number |
| `GET` | `/api/staff/prescriptions/{prescriptionId}/dispense-plan` | `Dispensing.Dispense` | `{ prescription, cat (header fields), quantity, isControlled, witnessRoles, canDispense, blockingReason?, batches: [{ skuId, productName, variantLabel, batchId, lotNumber, barcode?, expiresOn, available, isExpired, isDispensable, notDispensableReason? }], suggestedLines[], onlyExpiredStockAvailable, shortfall? }` — **the server's FEFO; the client adds nothing** |
| `POST` | `/api/staff/prescriptions/{prescriptionId}/dispensings` | `Dispensing.Dispense` · `witness` limit | C9 → `201 { dispensingId, supplyNumber, label, unpricedLines[] }`; `400 BatchExpired` / `OnlyExpiredStockAvailable` / `FefoOverrideReasonRequired` / `InsufficientDispensaryStock` / `WitnessRequired` / `WitnessNotVerified` / `PrescriptionValidityEnded`; `409` race |
| `GET` | `/api/staff/dispensings/{dispensingId}` | `Dispensing.View` | detail incl. `label` (`MedicineLabelView`: every §8.7 field), `labelPrintCount`, `allowedActions[]` (`Reprint`, `Reverse`) |
| `POST` | `/api/staff/dispensings/{dispensingId}/label-prints` | `Dispensing.Dispense` | C10 → `204` |
| `POST` | `/api/staff/dispensings/{dispensingId}/reverse` | `Dispensing.Reverse` · `witness` limit | C11 → `204`; `400 ReversalWindowPassed` / `DispensingNotReversible` |

**Controlled-drug register** (staff; branch from `X-Branch-Id`)

| Method | Route | Access | Command / query → success |
|---|---|---|---|
| `GET` | `/api/staff/controlled-drugs/register?medicineId&batchId&kind&from&to&pageNumber&pageSize` | `ControlledDrugs.View` | paged entries, newest first: `entryNumber`, `id`, `recordedOn`, `occurredOn`, `kind`, `medicine`, `lotNumber`, `quantityDelta` (with unit), `runningBalance` (per medicine and batch, as of that entry), `doseText?`, `wastage?`, `catName?`, `customerName?`, `actorName`, `witnessName?`, `source` (`type`, `id`, `label` e.g. *"Dispensing RX-2026-000123 #1"*), `correctsEntryNumber?`, `correctedByEntryNumbers[]`, `isSealed` |
| `GET` | `/api/staff/controlled-drugs/register/{entryId}` | `ControlledDrugs.View` | the entry, its correction chain, its seal (`chainPosition`, `entryHash`) |
| `POST` | `/api/staff/controlled-drugs/register/{entryId}/corrections` | `ControlledDrugs.Correct` · `witness` limit | C7 → `201 { entryId, entryNumber }` |
| `GET` | `/api/staff/controlled-drugs/balances` | `ControlledDrugs.View` | per medicine and batch: `registerBalance`, `stockBalance`, `isMismatch` |
| `GET` | `/api/staff/controlled-drugs/verification` | `ControlledDrugs.View` | `{ isIntact, entriesChecked, firstBrokenPosition?, staleUnsealed: [{ entryNumber, recordedOn }], checkedOn }` |
| `GET` | `/api/staff/controlled-drugs/register/export?medicineId&from&to` | `ControlledDrugs.View` | `text/csv` attachment, `no-store`; audited |

**Diet authorisations** (staff)

| Method | Route | Access | Command / query → success |
|---|---|---|---|
| `GET` | `/api/staff/diet-products?search` | `DietAuthorisations.Issue` | active products with `RequiresPrescription`: `productId`, `name`, `brand` (via `ICatalogueReader`) |
| `POST` | `/api/staff/cats/{catId}/diet-authorisations` | `DietAuthorisations.Issue` | C17 → `201 { authorisationId }`; `400 VisitNotOpen` / `ProductNotPrescriptionDiet` / `DietAuthorisationAlreadyActive` |
| `GET` | `/api/staff/cats/{catId}/diet-authorisations?scope=active\|all` | `DietAuthorisations.View` | `id`, `product`, `validFrom`, `validTo`, `status` (**effective** — `Expired` once `validTo` has passed), `authorisedBy`, `visitId`, `notes?` |
| `POST` | `/api/staff/diet-authorisations/{id}/extend` · `/revoke` | `DietAuthorisations.Issue` | C18 → `204` |

**Customer**

| Method | Route | Access | Command / query → success |
|---|---|---|---|
| `GET` | `/api/customers/me/prescriptions?scope=active\|past&catId` | customer | per the caller's cats: `id`, `catId`, `catName`, `medicine` (name, strength, form), `dose`, `route`, `frequency` (label), `remainingDays`, `courseEndsOn`, `issuingVetName`, `issuedOn`, `status`, `repeat` (`isRepeatable`, `suppliesLeft`, `validUntil`, `canRequest`, `cannotRequestReason?`, `openRequest?`) — **US-PHM-07** |
| `GET` | `/api/customers/me/prescriptions/{id}` | customer | the above + `instructions`, `dispensings[]` (date, branch, quantity — **no batch numbers**), `bookLink`; `404` if the cat is not the caller's |
| `POST` | `/api/customers/me/prescriptions/{id}/repeat-requests` | customer · `repeat-request` limit (10/hour) | C12 → `201 { repeatRequestId }`; **`400 PrescriptionValidityEnded`** with the explanatory message and `bookLink`; `400 NoRepeatsRemaining` / `RepeatRequestAlreadyOpen` |
| `DELETE` | `/api/customers/me/prescriptions/{id}/repeat-requests/{requestId}` | customer | C13 → `204` |
| `GET` | `/api/customers/me/diet-authorisations?catId&scope=active\|past` | customer | `id`, `catId`, `catName`, `productId`, `productName`, `validFrom`, `validTo`, `status` (effective), `authorisedByName` |

`canRequest` and `cannotRequestReason` come from the **same** rule function `RequestRepeat` uses — the
portal never shows a button the command would refuse. Every write uses HO-ACC-001's `400` / `401` /
`403` / `409` shapes (R1–R8).

### 12.5 Filters

Extend HO-ACC-001's `UniqueConstraintExceptionFilter`: `ix_diet_authorisation_active` (`23505`) →
`400 DietAuthorisationAlreadyActive`; `ix_formulary_medicine_code` → `400 FormularyCodeAlreadyExists`;
`ix_formulary_medicine_sku` → `400 SkuAlreadyLinkedToMedicine`; `ix_dispensing_supply` → `409` (a
concurrent dispense of the same supply). The seal-chain index is never surfaced to HTTP (worker only).
A PostgreSQL `RAISE EXCEPTION` from a register trigger is **not** mapped — it is a programming error
and returns `500` after logging (the interceptor should have stopped it first). Unknown constraints
rethrow.

### 12.6 Read side — `src/Read/`

Queries in `Queries/Prescription/`, handlers in `QueryHandlers/Prescription/`, ViewModels flat;
explicit projections, `AsNoTracking()`; active/remaining computed by `PrescriptionActivityRules`,
never in SQL alone.

- `GetFormularyMedicinesQuery`, `GetFormularyMedicineQuery`, `SearchFormularySkusQuery`.
- `CheckPrescriptionSafetyQuery` — loads through `ISafetyInputLoader`, calls the assessor. **Never
  writes.**
- `GetCatPrescriptionsQuery`, `GetPrescriptionDetailQuery` (with `allowedActions` from
  `PrescriptionActionRules`, shared with the aggregate), **`GetVisitPrescriptionsQuery`**,
  **`GetVisitDietAuthorisationsQuery`**, **`GetCatPrescriptionHistoryQuery`** (HO-EMR-001 contracts).
- `GetPharmacyQueueQuery`, `LookupPrescriptionByNumberQuery`, `GetDispensePlanQuery` (through
  `IStockLedger.PlanAsync` and `DispensingBatchPlanner`), `GetDispensingQuery`.
- `GetControlledDrugRegisterQuery` (running balance via a window sum over `entry_number` per medicine
  and batch), `GetControlledDrugEntryQuery`, `GetControlledDrugBalancesQuery`,
  `VerifyControlledDrugRegisterQuery`, `ExportControlledDrugRegisterQuery` (streamed).
- `GetCatDietAuthorisationsQuery`, `SearchDietProductsQuery`.
- `GetCustomerPrescriptionsQuery`, `GetCustomerPrescriptionDetailQuery`,
  `GetCustomerDietAuthorisationsQuery`.
- Worker queries: `GetUnsealedRegisterEntriesQuery`, `GetExpirableDietAuthorisationsQuery`,
  `GetExpirablePrescriptionsQuery`, `GetDueRefillRemindersQuery`.

Customer and register names (cat, customer, staff) are joined read-side; a **merged** customer id in a
register entry is followed to the survivor for display (ADR-152).

### 12.7 Host — `Program.cs`

Register every handler, validator (as `IValidator<T>`), query handler and event handler in §11–§12.6 —
**count them**; there is no assembly scanning. Ports → implementations (§12.2), Scoped;
`PrescriptionRefillDueGuard` into HO-NTF-001's `SubjectGuardResolver`; the five templates via the
catalogue; `ControlledDrugRegisterImmutabilityInterceptor` in `AddDbContext`; `PharmacyWorkCycle`
(Scoped); **`AddHostedService<PharmacyWorker>()`**; rate-limit policies `witness` (10 per minute per
staff session) and `repeat-request` (10 per hour per customer). `IDietAuthorisationGate`,
`IControlledDrugRegister`, `IControlledDrugDirectory`, `IFormularyReader`, `IStaffWitnessVerifier` are
registered here for their consumers in later areas. `ValidateScopes`/`ValidateOnBuild` stay on.
`appsettings.json` gains the `Pharmacy:Worker` section.

---

## 13. Allowed scope

```
src/Shared/CatEcommerce.Shared/Common/Enums/{the seventeen enumerations in §10.1}.cs
src/Shared/CatEcommerce.Shared/Common/Enums/NotificationSubjectKind.cs                             ← PrescriptionRefillDue only
src/Shared/CatEcommerce.Shared/Common/Constants/{PrescriptionRules,DispensingRules,RepeatRules,ControlledDrugRules,FormularyRules,LabelRules,PharmacyMessages}.cs
src/Shared/CatEcommerce.Shared/Common/Constants/{Actions,ActionRoles,BusinessViolationCodes}.cs     ← append only
src/Shared/CatEcommerce.Shared/Common/Constants/SettingDefinitions.cs                               ← the six keys of §10.2 only
src/Shared/CatEcommerce.Shared/Common/Constants/NotificationTemplateCatalogue.cs                   ← the five definitions of §8.11 only
src/Shared/CatEcommerce.Shared/Common/Constants/AuditRedactionRules.cs                             ← WitnessPassword entry only
src/Shared/CatEcommerce.Shared/Common/Helpers/{PrescriptionActivityRules,PrescriptionActionRules,MedicineTermNormaliser,ControlledDrugEntryCanonicalForm}.cs
src/Domain/CatEcommerce.Domain/Aggregates/Prescription/**
src/Domain/CatEcommerce.Domain/Entities/{FormularyInteraction,LinkedSku,PrescriptionWarning,RepeatRequest,DispensingLine}.cs
src/Domain/CatEcommerce.Domain/ValueObjects/{Strength,DoseRange,Dose,SafetyRecord,MedicineSnapshot,MedicineLabelSnapshot,PriceSnapshot}.cs   ← PriceSnapshot only if HO-SHP/HO-ADM have not shipped one
src/Domain/CatEcommerce.Domain/DomainServices/{PrescriptionSafetyAssessor,DoseConverter,AllergyConflictDetector,InteractionDetector,DispensingBatchPlanner,MedicineLabelComposer,DoseFormatter,RefillScheduleCalculator}.cs
src/Domain/CatEcommerce.Domain/DomainServices/{ISafetyInputLoader,IPrescriptionNumberAllocator,IStaffWitnessVerifier,IControlledDrugRegister,IControlledDrugDirectory,IFormularyReader,IDietAuthorisationGate}.cs
src/Domain/CatEcommerce.Domain/DomainServices/INotificationLinkBuilder.cs                           ← three methods added only
src/Domain/CatEcommerce.Domain/Models/**                                                           ← new DTOs only
src/Domain/CatEcommerce.Domain/Events/Prescription/**
src/Application/CatEcommerce.Application/Commands/Prescription/**
src/Application/CatEcommerce.Application/CommandHandlers/Prescription/**                           ← incl. PrescribingSteps
src/Application/CatEcommerce.Application/CommandHandlers/Validators/{one per command in §11.1}.cs
src/Application/CatEcommerce.Application/DomainEventHandlers/Prescription/**
src/Application/CatEcommerce.Application/DataMappers/{FormularyMapper,PrescriptionMapper,DispensingMapper,ControlledDrugRegisterMapper,DietAuthorisationMapper}.cs
src/Read/CatEcommerce.Read/Queries/Prescription/**
src/Read/CatEcommerce.Read/QueryHandlers/Prescription/**
src/Read/CatEcommerce.Read/ViewModels/{the ViewModels in §12.4}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/CatEcommerceDbContext.cs               ← DbSets and the interceptor registration only
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/{FormularyMedicine,Prescription,Dispensing,ControlledDrugRegisterEntry,ControlledDrugRegisterSeal,DietAuthorisation}Configuration.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/**                           ← the five migrations of §12.1 only
src/Infrastructure/CatEcommerce.Infrastructure/Pharmacy/**
src/Infrastructure/CatEcommerce.Infrastructure/Auditing/ControlledDrugRegisterImmutabilityInterceptor.cs
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/Guards/PrescriptionRefillDueGuard.cs
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/NotificationLinkBuilder.cs            ← §8.11 methods only
src/Api/CatEcommerce.Api/Controllers/{Formulary,AdminFormulary,StaffPrescriptions,StaffPharmacy,StaffDispensings,StaffControlledDrugs,StaffDietAuthorisations,CustomerPrescriptions,CustomerDietAuthorisations}Controller.cs
src/Api/CatEcommerce.Api/Contracts/Prescription/**
src/Api/CatEcommerce.Api/Background/{PharmacyWorker,PharmacyWorkCycle}.cs
src/Api/CatEcommerce.Api/Filters/UniqueConstraintExceptionFilter.cs                                ← §12.5 mappings only
src/Api/CatEcommerce.Api/appsettings.json                                                          ← Pharmacy:Worker section only
src/Api/CatEcommerce.Api/Program.cs                                                                ← §12.7 only
tests/CatEcommerce.Domain.Tests/**
tests/CatEcommerce.Application.Tests/**
tests/CatEcommerce.IntegrationTests/**
docs/test-cases/TC-HO-PHM-001.md
docs/gates/HO-PHM-001-*.md
.ai/decisions/adr.md                                                                               ← ADR-137…154, append only
```

The HO-NTF-001 amendment (one subject kind, three link-builder methods, five catalogue entries) and the
HO-ADM-001 amendment (six settings) are small but in other areas' files: they are named line by line
above, and each owner's §1 Amended row records them when the build lands.

## 14. Forbidden scope

| Path | Why |
|---|---|
| `.ai/**` except `decisions/adr.md`, `.claude/**` | Governing docs and agents (OQ-11) |
| `docs/SRS.md`, `docs/USER_STORIES.md`, `docs/handover/**` | Inputs — a conflict is a stop (§20.10) |
| `web/**` | HO-PHM-002 |
| Every file of HO-ACC-001, HO-ADM-001, HO-CAT-001, HO-NTF-001 and HO-APT-001 beyond the lines in §13 | Their areas. **`CatAggregate` and `CatWeightRecordAggregate` are read, never edited**; the password hasher and throttle are called, never changed |
| Any file of HO-EMR-001, HO-IMP-001, HO-SHP-001 (`clinic.visit*`, `inventory.*`, `catalogue.*` code and tables) | Consumed through their ports only (ADR-151). A missing port is a stop (§20.2) |
| Order, cart, subscription, invoice or payment types | HO-ORD, HO-SUB, HO-BIL — they consume this slice |
| Any migration that `UPDATE`s, `DELETE`s or disables a trigger on `pharmacy.cd_register_*` | ADR-144 |
| `NuGet.Config`, `global.json`, `*.sln`, `*.csproj` | No package is added |

---

## 15. Unit test cases

| ID | Case | Expected |
|---|---|---|
| **A — foundation** | | |
| U-A1 | `MedicineTermNormaliser` on `"Penicillins (Amoxicillin)"`, `"GAS"`, `"Méloxicam"` | `"penicillin amoxicillin"`, `"gas"`, `"meloxicam"` |
| U-A2 | `PrescriptionActivityRules` over a table of (status, dates, supplies, today) | `IsActive` and `RemainingDays` per §8.4, incl. a `Completed` prescription whose course still runs (active) and a `Dispensed` one past `ValidUntil` (inactive) |
| U-A3 | Every template of §8.11 | Passes HO-NTF-001 `TemplateRules`; transactional; `Normal` |
| **B — formulary** | | |
| U-B1 | `Create` with `PerKg` and no range; `Fixed` with a range | `DoseRangeRequired`; `InvalidDoseRange` (range not allowed) |
| U-B2 | Min > max | `InvalidDoseRange` |
| U-B3 | Controlled without schedule | `ControlledScheduleRequired` |
| U-B4 | Interaction with both a medicine id and a term; with neither | `InvalidInteraction` both |
| U-B5 | `AllowedRoutes` without `DefaultRoute` | Violation |
| **C — safety** | | |
| U-C1 | Meloxicam 1.5 mg/ml, range 0.05–0.1 mg/kg, cat 4.00 kg, dose 0.2 ml | 0.3 mg → 0.075 mg/kg → `WithinRange`; expected 0.2–0.4 mg |
| U-C2 | Same, dose 0.4 ml | 0.15 mg/kg → `AboveRange`, `DoseAboveRange` requires acknowledgement — **US-PHM-02 AC1** |
| U-C3 | Dose exactly at min and at max (after 4 dp rounding) | `WithinRange` both |
| U-C4 | Dose in `Drop`; strength in `Iu`; tablet dose against a per-ml strength | `NotCheckable` each, warning present, **not** requiring acknowledgement |
| U-C5 | Per-kg medicine, no weight; weight 31 days old (max 30); weight 30 days old | `Missing`; `Stale` with age 31; `Satisfied` — **US-PHM-02 AC2** |
| U-C6 | `Fixed` medicine with no weight | `Satisfied` |
| U-C7 | Allergy `"Penicillin"` vs amoxicillin (class `penicillin`) | `AllergyConflict`, blocking, requires reason — **US-PHM-03 AC1** |
| U-C8 | Allergy `"Pollen"`, `"chicken"` vs amoxicillin; allergy `"pen"` | No conflict; no conflict (< 4 letters) |
| U-C9 | Allergy text `"penicilin"` (misspelt) | **No** conflict — the documented limitation (ADR-140) |
| U-C10 | New prednisolone; active meloxicam whose interactions list `corticosteroid` (Major); prednisolone lists nothing | `Interaction`, `Major`, requires acknowledgement — both-direction rule — **US-PHM-03 AC2** |
| U-C11 | Interaction listed on both sides as `Moderate` and `Major` | One warning, `Major`, both notes |
| U-C12 | `Minor` interaction | Shown with severity; not requiring acknowledgement |
| U-C13 | Same medicine already active | `Interaction` `Major` naming the existing prescription number |
| U-C14 | Profile medication `"Meloxicam"` resolves; `"fish oil"` does not | Checked; listed as information |
| U-C15 | Fingerprint stable for the same warnings in any order; changes when the weight date changes | As stated |
| U-C16 | Assessment with no allergies recorded | Information *"No allergies recorded"*, no warning |
| **P — prescription aggregate** | | |
| U-P1 | Transition table over every `(status, action)` | Only §8.4's arrows; others `InvalidPrescriptionTransition` naming both states |
| U-P2 | `RecordSupply` on `ValidUntil` exactly; the day after | Allowed; `PrescriptionValidityEnded` |
| U-P3 | 2 repeats: three supplies; a fourth | `Dispensed`, `Dispensed`, `Completed`; `NoSuppliesRemaining` |
| U-P4 | Repeatable with `ValidUntil` beyond `maxRepeatValidityDays`; non-repeatable default | `InvalidValidity`; `StartsOn + 30` |
| U-P5 | `ReverseSupply` of a non-latest supply | Violation |
| U-P6 | `Cancel` with an open repeat request | Request `Declined` with the reason |
| **R — repeats** | | |
| U-R1 | `RequestRepeat` inside the window with supplies left | `Requested` — **US-PHM-06 AC1** |
| U-R2 | Outside the window | `PrescriptionValidityEnded`, message with the date and *"a vet needs to authorise a new prescription"* — **US-PHM-06 AC2** |
| U-R3 | Not repeatable; no supplies left; first supply not dispensed; a second open request | `NotRepeatable`; `NoRepeatsRemaining`; `FirstSupplyNotDispensed`; `RepeatRequestAlreadyOpen` |
| U-R4 | `RefillScheduleCalculator`: last dispensed 1 Sep, 30 days, lead 3, valid until 31 Dec | Remind 28 Sep … 1 Oct; none when `RefillDueOn > ValidUntil`; none with lead 0 |
| U-R5 | `PrescriptionRefillDueGuard`: unchanged; repeat dispensed since; cancelled; cat deceased; open request | Valid; invalid ×4 |
| **E — dispensing** | | |
| U-E1 | Planner: batches expiring Nov (5), Dec (10), expired Mar (20); quantity 8 | Suggests Nov 5 + Dec 3; expired never suggested |
| U-E2 | Only expired stock | `OnlyExpiredStockAvailable = true` — **US-PHM-05 AC2** |
| U-E3 | Validate a request naming the expired batch | `BatchExpired`; **no parameter, role or flag changes it** (reflection over the planner's API finds no override input) |
| U-E4 | Request Dec batch while Nov could cover it; with and without a reason | `FefoOverrideReasonRequired`; valid |
| U-E5 | Σ quantity ≠ prescription quantity; > 5 lines; SKU not linked | `DispenseQuantityMismatch`; `TooManyDispensingLines`; `SkuNotLinkedToMedicine` |
| U-E6 | Quarantined batch | `BatchNotDispensable` with Inventory's reason |
| U-E7 | `MedicineLabelComposer` | Every US-PHM-05 AC1 field present; `ExpiresOn` = earliest line expiry; dose line *"Give 0.3 ml by mouth once daily for 5 days"* |
| U-E8 | `DoseFormatter` for each frequency | Label wording per `DoseFrequency` |
| **D — register** | | |
| U-D1 | `Record(Dispensed)` without witness; witness == actor; positive delta | `WitnessRequired`; `WitnessMustDifferFromDispenser`; `InvalidRegisterQuantity` |
| U-D2 | `Record(Receipt)` without witness | Allowed |
| U-D3 | Reflection over `ControlledDrugRegisterEntryAggregate` | **No public or internal method other than the factory; no public setter** |
| U-D4 | Interceptor with a modified entry; a deleted seal | `ControlledDrugRegisterImmutableException`; nothing sent |
| U-D5 | Hash chain over three entries; alter one field of the second | Verification reports position 2 |
| U-D6 | `ControlledDrugEntryCanonicalForm` field list vs the aggregate's persisted properties | Equal — a new field not added here fails |
| U-D7 | Correction: target of another branch; missing reason | `CorrectionTargetMismatch`; `CorrectionReasonRequired` |
| **W — witness** | | |
| U-W1 | Verifier: unknown identifier; wrong password; locked; no witness role; same as actor; inactive | Each `WitnessNotVerified`, **identical** result shape |
| U-W2 | Wrong password | Throttle counter for that account incremented; masked identifier in the audit DTO |
| **G — diet** | | |
| U-G1 | Gate order: not-required product; other owner's cat; deceased cat; active in-date; `ValidTo` yesterday with `Status = Active` (sweep not run) | `NotRequired`; `CatNotOwned`; `CatDeceased`; `Authorised`; **`AuthorisationExpired`** — **US-PHM-08 AC2** |
| U-G2 | `Authorise` with `ValidTo` beyond 365 days | `InvalidDietValidity` |
| U-G3 | `Extend` an authorisation expired 10 days ago; 40 days ago | Re-activated; `DietAuthorisationNotExtendable` |
| U-G4 | Gate messages | Contain no diagnosis, no dose, no vet notes |
| **X — cross-cutting** | | |
| U-X1 | Reflection over `Events/Prescription/**` | No string properties |
| U-X2 | `ToString()` and validator messages of every command with `WitnessPassword` | Never contain the password |
| U-X3 | Migration predicates | Use `DietAuthorisationStatus.Active.Value`; no literal |
| U-X4 | `PharmacyWorker` with a throwing pass | Logged; next pass runs |
| U-X5 | `EndpointAccessConventionTests` over the new controllers | Every action has one marker |

## 16. Integration test cases

Real HTTP, `Testing` environment, shared database (HO-ACC-001 §16.1). Workers off; tests call
`PharmacyWorkCycle` and HO-NTF-001's `NotificationWorkCycle`, advancing `FakeTimeProvider`.

### 16.1 Additional shared-database rules

1. **Each test creates its own branch** (HO-ADM-001 endpoints), its own staff (vet, nurse, receptionist,
   inventory, admin — with passwords set, for witness verification), its own customer, cat and weight.
2. **Formulary codes are minted per test** (`T-{Guid:N}` truncated to 30).
3. Visits, SKUs, batches and dispensary stock are created through **HO-EMR-001, HO-SHP-001 and
   HO-IMP-001's own endpoints or repositories** — never by inserting into their tables.
4. Register entries are **never cleaned up** — the triggers refuse it. Assertions filter by the test's
   branch.

| ID | Case | Expected |
|---|---|---|
| **A — foundation** | | |
| I-A1 | Migrations on an empty database | `pharmacy` schema; tables, indexes, both sequences; four triggers present (`pg_trigger`) |
| I-A2 | Host starts, scope validation on | Every registration resolves |
| I-A3 | V1 spike: two register entries saved | Distinct increasing `entry_number`s read back |
| **B — formulary** | | |
| I-B1 | Admin creates a medicine with range, interactions, controlled flag, two linked SKUs; `GET` it | `201`; detail as sent — **US-PHM-04 AC1** |
| I-B2 | Nurse creates one | `403`; `AuthorizationDenied` audit row |
| I-B3 | Link a SKU already linked elsewhere | `400 SkuAlreadyLinkedToMedicine` |
| I-B4 | Two admins update with the same version | One `204`, one `409` |
| I-B5 | Clear the controlled flag on a medicine with register entries, without and with a reason | `400 ControlledFlagChangeReasonRequired`; `204`; audit row with before/after |
| I-B6 | SKU search for a clinic-only SKU | Returned, with `linkedMedicineId` when linked |
| **C — prescribing** | | |
| I-C1 | Vet issues within range, open visit | `201`; number `RX-yyyy-…`; linked to cat and visit; `GET /api/staff/visits/{visitId}/prescriptions` lists it — **US-PHM-01** |
| I-C2 | **Receptionist, nurse and administrator** `POST` a prescription | `403` each; one `AuthorizationDenied` audit row each — **US-CLN-22, AC-18** |
| I-C3 | Visit finalised; visit of another cat | `400 VisitNotOpen`; `400 VisitCatMismatch` |
| I-C4 | Safety check with a dose above range | `200`, `doseAboveRange`, `expectedRange` in mg/kg and mg, a fingerprint; **no row written** |
| I-C5 | Issue that dose without a fingerprint; with it | `400 PrescriptionWarningsNotAcknowledged` + assessment; `201`; `safetyRecord` stores weight, dose/kg, acknowledgement — **US-PHM-02 AC1** |
| I-C6 | Check, then record a new weight, then issue with the old fingerprint | `409 PrescriptionWarningsChanged` with the new assessment |
| I-C7 | No weight in 30 days | `400 RecentWeightRequired` naming the latest weight and date; record a weight (HO-CAT-001); issue succeeds — **US-PHM-02 AC2** |
| I-C8 | Cat allergic to `Penicillin`; prescribe amoxicillin; acknowledge without reason; with reason | `400 AllergyAcknowledgementReasonRequired`; `201`; warning stored — **US-PHM-03 AC1** |
| I-C9 | Active meloxicam; check prednisolone | `interaction` with `Major` severity and the note — **US-PHM-03 AC2** |
| I-C10 | Issue with acknowledged warnings | A `BusinessEvent` `PrescriptionWarningsAcknowledged` row, joined by correlation id |
| I-C11 | Deceased cat | `400 CatNotTreatable` |
| **D — register and witness** | | |
| I-D1 | `UPDATE`, `DELETE`, `TRUNCATE` on `pharmacy.cd_register_entry` and `cd_register_seal` through Npgsql | Each raises (V2) |
| I-D2 | `UpdateAsync` of a loaded entry | `ControlledDrugRegisterImmutableException`; nothing sent |
| I-D3 | Correction by a vet with a verified witness, delta −1 | `201`; new entry linked; original unchanged; an Inventory adjustment entry committed in the same transaction — **FR-PHM-7** |
| I-D4 | Correction by an administrator | `403` (Appendix D — OQ-8) |
| I-D5 | Witness with the wrong password ×3 | `400 WitnessNotVerified` each; the witness account then locked (sign-in `401`); `WitnessVerificationFailed` audit rows with masked identifiers |
| I-D6 | Run the worker; `GET …/verification` | Every entry sealed; `isIntact: true` |
| I-D7 | Break the chain in a test-only way (disable the trigger inside a transaction, alter a quantity, verify inside the same transaction, roll back) | `isIntact: false`, `firstBrokenPosition` of that entry; after rollback the trigger is enabled and the data unchanged |
| I-D8 | Export for a medicine and range | CSV with running balance and hashes; a note `=SUM(1)` exported as `'=SUM(1)`; export audit row |
| I-D9 | Balances after dispensing (phase E) | Register and stock balances equal; an Inventory-only adjustment → `isMismatch: true` |
| **E — dispensing** | | |
| I-E1 | Dispense the FEFO suggestion | `201`; the **exact** batches reduced in HO-IMP-001's ledger with reference = dispensing id; `label` has cat, owner, medicine, dose, instructions, date, expiry; prescription `Dispensed` — **US-PHM-05 AC1** |
| I-E2 | Only expired stock at the dispensary | Plan `onlyExpiredStockAvailable: true`; dispense `400 OnlyExpiredStockAvailable`; no ledger entry — **US-PHM-05 AC2** |
| I-E3 | Dispense naming an expired batch while good stock exists, as an **administrator** | `400 BatchExpired` — no role overrides (BR-3) |
| I-E4 | Non-FEFO batch without reason; with reason | `400 FefoOverrideReasonRequired`; `201` with the reason stored |
| I-E5 | Controlled medicine without witness; nurse witness verified | `400 WitnessRequired`; `201`, one `Dispensed` register entry per line with dispenser, witness, cat and dose — **US-PHM-04 AC2** |
| I-E6 | Receptionist witnessing (not a witness role) | `400 WitnessNotVerified` |
| I-E7 | **Two dispensers dispense the last supply concurrently** | One `201`, one `409`; one dispensing row; ledger reduced once |
| I-E8 | Two prescriptions dispensing the last units of one batch concurrently | One `201`; the other `400 InsufficientBatchStock` or `409` — never a negative balance (G1) |
| I-E9 | Inventory enlistment fails after the register entry was enlisted | Nothing committed: no dispensing, no ledger entry, no register entry |
| I-E10 | Label print ×2 | `labelPrintCount` 2; two `BusinessEvent` rows |
| I-E11 | Reverse within 24 h (vet); after 24 h; as a nurse | `204`, stock returned to the same batch, register `Returned` entry, supply count back; `400 ReversalWindowPassed`; `403` |
| I-E12 | `PrescriptionDispensedEvent` captured | Lines with SKU, batch, quantity and price snapshot; unpriced SKU → null and `unpricedLines` in the response |
| I-E13 | Recall trace: HO-IMP-001's ledger trace for the batch | Returns the cat and customer of I-E1 from the party stamp this slice wrote; a reversed dispensing nets to zero |
| **F — repeats and portal** | | |
| I-F1 | Owner requests a repeat inside the window; staff dispense it with no visit | Request `201`; dispensing `201` carrying the `repeatRequestId`; request `Dispensed`; `pharmacy.repeat-ready` in the inbox — **US-PHM-06 AC1** |
| I-F2 | Owner requests after `ValidUntil` | `400 PrescriptionValidityEnded` with the date, the new-authorisation text and `bookLink` — **US-PHM-06 AC2** |
| I-F3 | Staff dispense a supply after `ValidUntil` before the sweep ran | `400 PrescriptionValidityEnded` |
| I-F4 | Vet renews an expired prescription; the owner repeats the new one | New number; old `renewedBy`; full safety re-run (a changed weight surfaces a new warning) |
| I-F5 | Vet declines a request | `pharmacy.repeat-declined` with the reason text |
| I-F6 | `GET /api/customers/me/prescriptions?scope=active` | Medicine, dose, frequency, remaining duration and issuing vet for each active prescription; completed-but-running included; cancelled excluded — **US-PHM-07** |
| I-F7 | Another customer's prescription id | `404` on detail and on repeat request |
| I-F8 | Refill: last dispensed 27 days ago, 30-day course, lead 3; run the sweep twice | One `pharmacy.refill-due` request (deduplicated) with `repeatLink` — **US-NTF-03 AC1** |
| I-F9 | A repeat is dispensed before the reminder is delivered | Reminder `Cancelled SubjectNoLongerApplies` |
| I-F10 | Worker expiry pass | Past-validity prescription `Expired`; `canRequest: false` |
| **G — diet** | | |
| I-G1 | Vet authorises a prescription diet product for a cat from an open visit | `201`; gate `CheckAsync(owner, cat, sku of that product)` → `Authorised`; `pharmacy.diet-authorised` enqueued — **US-PHM-08 AC1** |
| I-G2 | Gate for another of the owner's cats; another customer | `NoAuthorisation`; `CatNotOwned` |
| I-G3 | Advance past `ValidTo` **without** running the worker | Gate `AuthorisationExpired` — **US-PHM-08 AC2 "locks automatically"** |
| I-G4 | Run the worker | Status `Expired`; `DietAuthorisationExpiredEvent` raised; `pharmacy.diet-authorisation-expired` in the owner's inbox — **US-PHM-08 AC2 "owner notified"** |
| I-G5 | Authorise a product without `RequiresPrescription`; a second active one for the same cat and product; concurrent duplicates | `400 ProductNotPrescriptionDiet`; `400 DietAuthorisationAlreadyActive`; exactly one row |
| I-G6 | Nurse authorises | `403` (BR-6) |
| I-G7 | Merge the owner | Prescriptions, dispensings and authorisations under the survivor; register entries unchanged; register view shows the survivor's name |
| **X — cross-cutting** | | |
| I-X1 | Log capture across every case at every level | No witness password, no instructions text, no allergen text, no owner names |
| I-X2 | Every response body | No field matching `/password|hash/i` except `entryHash` on register reads |
| I-X3 | **AC-4 end to end**: issue out of range (warned), allergy conflict (warned), dispense (batch deducted, label), owner sees it in the portal | Every step passes on one cat in one test |

---

## 17. Implementation order and verification

| Phase | Stories | Delivers |
|---|---|---|
| **A** | — | Verify V1–V3 (§7) — stop on failure; enumerations, settings, constants, codes; VOs; `MedicineTermNormaliser`, `PrescriptionActivityRules`; the HO-NTF-001 amendment (subject kind, link methods, templates) |
| **B** | US-PHM-04 AC1 | Formulary aggregate, admin and read endpoints, SKU search; migration `AddPharmacyFormulary`. SKU validation wired when HO-SHP-001's reader exists |
| **C** | US-PHM-01, 02, 03 | Safety assessor, converter, detectors, `SafetyInputLoader`, prescription aggregate, number allocator, check/issue/cancel, lists, visit query; migration `AddPharmacyPrescriptions`. **Needs HO-EMR-001** |
| **D** | FR-PHM-7, C-2 | Register aggregate, seals, interceptor, triggers, `EfControlledDrugRegister`, `StaffWitnessVerifier`, corrections, reads, export, verification, sealing pass; migration `AddControlledDrugRegister` |
| **E** | US-PHM-05, US-PHM-04 AC2 | Batch planner, label composer, dispense plan, dispense, label print, reversal, consumption source, dispensed event; migration `AddPharmacyDispensing`. **Needs HO-IMP-001 and HO-SHP-001** |
| **F** | US-PHM-06, 07, US-NTF-03 (refills) | Repeat requests, decline, renew, prescription expiry pass, portal endpoints, refill sweep and guard |
| **G** | US-PHM-08 | Diet authorisation aggregate, gate, events, sweep, notices, merge handler; migration `AddDietAuthorisations`. **Needs HO-SHP-001 and HO-EMR-001** |

Commands as HO-ACC-001 §17:

```bash
dotnet restore CatEcommerce.sln
dotnet build CatEcommerce.sln            # 0 warnings, 0 errors
dotnet test tests/CatEcommerce.Domain.Tests
dotnet test tests/CatEcommerce.Application.Tests
dotnet dotnet-ef migrations add <PhaseMigration> --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api --output-dir Persistence/Migrations
dotnet dotnet-ef database update --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api
dotnet test tests/CatEcommerce.IntegrationTests
```

**Read `AddControlledDrugRegister` before applying it** — the trigger SQL is hand-written and its
`Down` must never meet real data.

---

## 18. Acceptance criteria

Each row is checked against its evidence. **A row with no evidence is not met.**

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | PHM-01 AC1 | Given an open visit, a vet's prescription with medicine, strength, dose, route, frequency, duration, quantity and instructions is created and linked to the cat and the consultation | U-P1, I-C1, I-C3 |
| A2 | PHM-02 AC1 | A dose outside the formulary mg/kg range for the latest weight warns with the expected range and must be confirmed | U-C1, U-C2, U-C3, U-C15, I-C4, I-C5, I-C6 |
| A3 | PHM-02 AC2 | With no recent weight, prescribing is refused until one is recorded | U-C5, U-C6, I-C7 |
| A4 | PHM-03 AC1 | A recorded allergy matching the medicine gives a blocking warning requiring explicit acknowledgement | U-C7, U-C8, U-C9, I-C8 |
| A5 | PHM-03 AC2 | An interaction with an active medication listed in the formulary is shown with its severity | U-C10…U-C14, I-C9 |
| A6 | PHM-04 AC1 | An administrator adds a medicine with form, strength, dosing range and contraindications, and it becomes prescribable | U-B1…U-B5, I-B1, I-B2, I-C1 |
| A7 | PHM-04 AC2 | A controlled medicine's dispensing enforces the witness and register rules | U-D1, U-W1, I-E5, I-E6, I-D5 |
| A8 | PHM-05 AC1 | Dispensing deducts the specific batch, writes a ledger entry and produces a label with cat name, owner, medicine, dose, instructions, date and expiry | U-E1, U-E7, I-E1, I-E10, I-E12 |
| A9 | PHM-05 AC2 | When the only available batch is expired, dispensing is blocked with no override | U-E2, U-E3, I-E2, I-E3 |
| A10 | PHM-06 AC1 | A repeatable prescription inside its window is dispensable on request without a new consultation | U-R1, I-F1 |
| A11 | PHM-06 AC2 | Outside the window a new vet authorisation is required and the owner is told why | U-R2, I-F2, I-F3, I-F4 |
| A12 | PHM-07 AC1 | Active prescriptions list medicine, dose, frequency, remaining duration and issuing vet | U-A2, I-F6, I-F7 |
| A13 | PHM-08 AC1 | From an open visit, a vet authorises a named therapeutic food for a cat with a validity period, and the gate unlocks it for that cat — *the storefront button is HO-SHP-002/HO-ORD-002 (§4.2)* | U-G1, I-G1, I-G2, I-G5 |
| A14 | PHM-08 AC2 | On expiry the product locks automatically and the owner is notified | U-G1, **I-G3**, I-G4 |
| A15 | CLN-22 / AC-18 / BR-6 | Non-vets are refused prescribing and diet authorisation server-side, and the refusal is logged | I-C2, I-G6 |
| A16 | FR-PHM-7 / C-2 / D-5 | Register entries cannot be edited or deleted by any layer; corrections are new linked entries | U-D3, U-D4, I-D1, I-D2, I-D3 |
| A17 | C-2 "tamper-evident" | A change to a sealed entry is detected | U-D5, U-D6, I-D6, I-D7 |
| A18 | NTF-03 AC1 (refills) | Refill reminders go ahead of the due date with a reorder link, once, and never after the refill happened | U-R4, U-R5, I-F8, I-F9 |
| A19 | IMP-11 (dispensing half) | Every cat dispensed a batch is traceable | I-E13 |
| A20 | AC-4 | Prescription issued and dispensed end to end | **I-X3** |
| A21 | BR-4 | FEFO by default; deviation recorded with a reason | U-E4, I-E4 |

**Deferred, named in §4.2**: storefront and checkout gating (HO-ORD-001, HO-SHP-001), subscription
suspension (HO-SUB-001), surgery and treatment register entries (HO-SRG-001, HO-EMR-001), charging
(HO-BIL-001), portal dashboard (HO-POR-001), reports (HO-RPT-001).

---

## 19. Required gates

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | `Prescription` context (ADR-137, OQ-11); the pure assessor, planner and composer; **the two rule-1 exceptions** (ADR-143, ADR-148) and nothing else crossing roots; **no read or write of `inventory.*`, `catalogue.*`, `clinic.visit*`** (ADR-151); enlistment ports shaped like the outbox; no enqueue from an event handler; amendments to HO-NTF-001 and HO-ADM-001 confined to §13's lines |
| 2 | SECURITY | `security-reviewer` | **Vet-only prescribing at the API** (I-C2); **the witness verifier** — generic failures, lockout, audit, the password absent from logs, events, audit capture and `ToString()` (U-X2, I-X1); **register immutability at three layers** and the hash chain; `404` for another customer's prescription; CSV-injection guard on export; no clinical detail in gate messages or templates |
| 3 | CODE_REVIEW | `code-reviewer` | `PrescribingSteps` used by both prescribing handlers; the safety check writes nothing; `PrescriptionActivityRules` the only definition of "active"; idempotent worker commands; units carried on every quantity |
| 4 | TEST | `test-engineer` | `TC-HO-PHM-001.md` from §15/§16 first; §16.1's per-test branch and staff; I-E7/I-E8 with real parallelism; I-D7's rollback technique never leaves a disabled trigger |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row; A13's storefront half recorded as HO-ORD/HO-SHP's; the Appendix D inference (ADR-153) and OQ-1 flagged, not passed silently |

---

## 20. BLOCKED conditions — stop and report

**20.1 HO-ACC-001, HO-ADM-001, HO-NTF-001 or HO-APT-001 is not merged.** This slice consumes their
sessions, roles, hasher, throttle, audit, settings, time model, outbox, guards and worker pattern. Do
not stub them.

**20.2 A port of §8.12 does not exist, or its merged shape differs materially** (HO-EMR-001 has no
open/finalised visit status; HO-SHP-001 has no `RequiresPrescription` flag or no clinic-only SKUs;
HO-IMP-001 has no dispensary location or no enlistment port). Build the phases that do not need it;
**never** write to that area's tables, read them directly, or ship a fake implementation in `src/`.
Report the gap to that area's owner.

**20.3 HO-IMP-001 cannot guarantee G1** (no negative balance under concurrent enlistment) or G2 (expiry
refused inside `Enlist`). Stop phase E. Do not add a lock, an advisory lock or a balance table in this
slice — the guarantee belongs to Inventory.

**20.4 Someone asks for an override of an expired batch**, for "emergency use". BR-3 and FR-IMP-9 say
there is none. Report it; do not add a flag.

**20.5 Someone asks to edit or delete a register entry**, or a migration, data fix or merge would need
to. Never. Corrections are new entries (ADR-144). Report it.

**20.6 A reversal's return is refused because the batch has since expired.** Do not bypass Inventory;
the medicine is wasted through HO-IMP-001's wastage path and recorded in the register as `Wastage`.
Report the case in the test sheet.

**20.7 The witness step seems to need a lighter mechanism** (select a colleague, a PIN, a checkbox).
It does not — ADR-146. A PIN or card would be a new credential type and an owner decision.

**20.8 Never run `AddControlledDrugRegister`'s `Down`** against a database holding real entries.

**20.9 V1, V2 or V3 is false** (§7). Stop and report with the observed behaviour.

**20.10 A requirement conflict is a stop, not an edit** — SRS and backlog are forbidden scope.

**20.11 Do not enqueue from an event handler** — HO-NTF-001 ADR-057.
