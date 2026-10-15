# HO-EMR-001 — Clinic Consultation and Medical Records (backend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-EMR-001** |
| Title | Clinic — Consultation and Medical Records — backend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §4 — **US-CLN-11 … US-CLN-22** |
| SRS | [`docs/SRS.md`](../SRS.md) v2.3 — **FR-CLN-12 … FR-CLN-24**, FR-IMP-9, FR-IMP-15 (clinical stock movements), FR-POR-3, **BR-3, BR-4, BR-5, BR-6, BR-11**, D-3, **D-5**, D-6, D-8, C-2, C-3, C-7, C-9, **NFR-SEC-5**, **NFR-SEC-8**, **NFR-PRF-4**, NFR-LEG-4, UI-8, AC-2, AC-18, AC-20, Appendix B.1, Appendix D ("Clinical notes") |
| Pairs with | [`HO-EMR-002`](HO-EMR-002.md) — the frontend. **§12.4 of this document is its contract** |
| Builds on | [`HO-APT-001`](HO-APT-001.md) (appointments, check-in, the `Clinic` context and schema), [`HO-CAT-001`](HO-CAT-001.md) (cats, weight history, vaccinations, QuestPDF), [`HO-ADM-001`](HO-ADM-001.md) (branches, services, prices, settings, policy documents, `ActionRoles`, `OwnerScope`), [`HO-NTF-001`](HO-NTF-001.md) (outbox, templates, guards, `SendAfter`/`SubjectStamp`), [`HO-ACC-001`](HO-ACC-001.md) (staff, sessions, audit capture and triggers) |
| Consumes | **`HO-IMP-001`** (batches, dispensary location, `IStockLedger` — Phase F only), **`HO-SHP-001`** (SKUs, SKU price — Phase F only). Both are now written; §8.12 is reconciled against them |
| Provides to | `HO-PHM-001` (visit link for prescriptions, history section slot), `HO-SRG-001` (consent status, visit link), `HO-BIL-001` (`VisitFinalisedEvent`, charge lines), `HO-BRD-001` / `HO-ORD-001` (history section slots), `HO-POR-001` (portal reads) |
| Delivery order | Section 8 of 17 in [`README.md`](README.md#delivery-order) |
| Baseline commit | `4007225` |
| Amended | **2026-09-22 — README C-2 (stock port).** §8.12's IMP contracts, §8.1's companion-commit note and §8.6 steps 5, 7 and 9 were rewritten against HO-IMP-001 as written: the assumed `IStockAvailabilityReader`, `IStockLocationReader` and `StockLedgerEntryAggregate.Record` become the one published port `IStockLedger` (`PlanAsync` / `DeductAsync` / `ReceiveReturnAsync` / `RecordShortfall`). Enlist-without-save, the shape this slice depended on, is unchanged. New OQ-19. <br><br>**2026-09-23 — README C-5 (file storage).** §8.10 is **moved to [`HO-FIL-001`](HO-FIL-001.md)**, which extracts it as the shared facility for the five areas that needed it. This slice consumes `IFileStore` / `IMalwareScanner` / `IFileUrlSigner` and keeps only its three clinical `FilePurpose` values and their `FileRules` rows. ADR-125 is narrowed accordingly. OQ-6 closed by ADR-319 (ClamAV); storage is GCS per ADR-318. <br><br>**2026-10-07 — integrator, recorded at the merge of `ho/EMR-001` (`d3527166`) from `docs/gates/HO-EMR-001-*.md` (ARCHITECTURE, SECURITY, CODE_REVIEW, REVIEW-R2, TEST, ACCEPTANCE: all PASS after the follow-up round). Medium/low findings are in [`docs/backlog/EMR.md`](../backlog/EMR.md).** (1) **Phase F is deferred by owner decision.** US-CLN-15 (treatments, consumables, stock; §8.6, §8.12, phase F) is not built: no `TreatmentLine`, treatments route, dispensary controller, `GetVisitChargeLinesQuery` or `ClinicalStockShortfallRecordedEvent`; criteria A10 and A11 and cases U-F1…U-F9 and I-F1…I-F8 are DEFERRED, not failed. The visit payload's `treatments` and the summary's `treatments` / `medicinesAdministered` are empty lists until it lands. No HO-IMP-001 or HO-SHP-001 file was touched and the contracts they provide are unchanged. (2) **Seven files outside §13's literal list**, none in §14 forbidden scope: `src/Api/CatEcommerce.Api/Controllers/ClinicalResponses.cs` (the status mapping helper; §13's controller list names no helper); `src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/ClinicalConfigurationExtensions.cs` (a configuration helper; §13 lists one file per root); `src/Domain/CatEcommerce.Domain/DomainServices/FollowUpSchedule.cs` (not in §13's DomainServices list); `src/Shared/CatEcommerce.Shared/Common/Constants/FilePurposeAccess.cs` (HO-FIL-001's file — the OQ-7 extension point "named by the consuming handover", precedent HO-SHP-001's `ProductImage`; it now maps `LabResult` and `ClinicalPhoto` to `Visits.WriteNote` (Vet) and `ConsentForm` to `Consents.Record`); and three test files under `tests/CatEcommerce.Infrastructure.Tests/` — `Auditing/AuditCaptureExclusionsTests.cs`, `Documents/QuestPdfDischargeSummaryRendererTests.cs`, `Notifications/Guards/VisitFollowUpGuardTests.cs`. The per-area partials (`*.Consultation.cs`, ADR-341), the C-ALL files (`RecordClassification`, `ErasureExemptions`, `ClinicalAuditEntityTypes`, `ConsultationRetentionReviewSource`) and the ADR-133 guard lines, which sit in the APT files that actually hold the Complete and Cancel handlers (`AppointmentVisitCommandHandlers.cs`, `AppointmentChangeCommandHandlers.cs`) rather than the file names §13 lists, were counted in scope by the ARCHITECTURE gate. (3) **Edits to HO-APT-001's test files, which §14 reserves** (allowed by §13's `tests/**` globs, recorded for that reason): `tests/CatEcommerce.IntegrationTests/AppointmentsApiFactory.cs` is unsealed so the consultation factory can derive from it, and the two HO-APT handler-test call sites of the Complete and Cancel handlers take the new visit-repository constructor parameter (ADR-133). HO-FIL-001's `FilesEndpointTests` were also updated for the new `LabResult` access (staff upload needs `Visits.WriteNote`). (4) **Cross-branch returns `404`, not §11.4's `403 VisitBelongsToAnotherBranch`.** A visit, diagnostic order or consent at another branch than the active one answers `404` (`ClinicalResponses.cs`, every handler's `BranchId` check), matching the HO-APT-001 backlog #8 ruling (by-id staff actions scoped to the active branch; the response does not reveal whether the id exists). `VisitBelongsToAnotherBranch` is therefore declared (`BusinessViolationCodes.Consultation.cs`, `ClinicalMessages.cs`) and **unused**. This changes the §12.4 contract that [`HO-EMR-002`](HO-EMR-002.md) consumes: it must treat that `403` as `404`. (5) **The developer's other declared deviations, as the ARCHITECTURE gate judged them** (the gate's numbering; (5) and (8) filled in by the integrator from the developer's hand-back): (5) diagnostic result values gained a `position` column, because EF reads owned collections unordered — every read orders by it (accepted); (8) the I-B6 forced vitals+weight failure uses a real database trigger scoped to the test's own cat instead of a hook in the weight configuration (test-only, accepted); (1) is item (2) above; (2) problems are **withdrawn, never deleted**, stricter than §8.4 and consistent with ADR-118 and C-ALL-4 — side effect: `visit_problem` is no longer bounded at 20 (a save that omits problem ids withdraws the old rows and inserts new ones; backlog EMR 6); (3) `files.stored_file` carries a **delete guard** (`clinic.stored_file_referenced`, `AddDiagnostics`, replaced in `AddConsents` to include consents) instead of ADR-118's append-only trigger, because the table belongs to HO-FIL-001, which legitimately redacts and purges unreferenced rows — the guard does not stop the eraser deleting the object first (backlog EMR 1, owner decision; ADR-118 carries an amendment); (4) is item (4) above; (6) `FollowUpSchedule` converts zone to instant with `TimeZoneInfo` directly and returns UTC, where §11.4 C18 says to use HO-ADM-001's calendar helpers; (7) the access attribute takes the `ClinicalRecordKind` **name** as a string, because an attribute argument cannot be an `Enumeration` (an unknown name throws); (9) `thumbnailUrl` is the full file's signed URL; (10) two catalogue mappings were added beyond §12.5's list; (11) there is no `DiagnosticMapper` or `ConsentMapper` — Visit handlers use HO-APT-001's `AppointmentMapper.ParseEnum`; (12) `.ai/bounded-contexts.md` was left alone (§14 forbids `.ai/**` except `adr.md`), so the "two recorded exceptions" §6 cites are still unrecorded there. (6) **Gate fix round** (`2f2e597`, `729393f`; round-1 ARCHITECTURE BLOCKER and HIGH, CODE_REVIEW two HIGHs, closed in `HO-EMR-001-REVIEW-R2.md`): (a) the draft first-write race is no longer retried in `StaffVisitsController.SaveDraft` (which resolved the `DbContext` and dispatched twice); `UniqueConstraintExceptionFilter.TryRetryDraftRaceAsync` now answers `ix_consultation_draft_visit_author` (SQLSTATE 23505 with a `ConsultationDraftAggregate` among the entries) with one re-dispatch as an overwrite — the filter mapping §12.5 specified; (b) the Visit, Diagnostic, DischargeSummary and Consent query handlers and the EMR history section providers no longer rehydrate aggregates: `AsNoTracking` plus row projections, with total orderings (Id tie-breaks; result values by `Position` then Id), the branch, finalised, `IsMarkedToDelete` and current-owner filters preserved; (c) `RecordVitalsCommand.MeasuredOn` is normalised to UTC in `VisitMapper` and the `VitalsEntry` constructor, so a client offset such as `+06:00` no longer fails at Npgsql as a 500 (same defect class as HO-APT-001 Amended (5)). A finalised visit now answers `VisitFinalised`, never `StaleVisitVersion`, on a note save. |
| Amended (HO-POR-001) | **2026-10-10 -- owner-delegated decision 2026-10-10, owner to review.** `CustomerVisitListItemViewModel` (`GET /api/customers/me/cats/{catId}/visits`, §12.4) gains a trailing `FollowUpOn` (`DateOnly?`, the visit's follow-up due date, null when none) so the customer portal dashboard can show it. Additive only: no rename, removal or migration; the query projection reads the visit's existing `FollowUp.DueOn`. Test: `DischargeSummaryAndFollowUpEndpointTests` I-G1 asserts it. CONTRACTS row 30. |
| Amended | **2026-10-11 -- INTEGRATOR DECISION (integrator; the owner may review), closing the ARCHITECTURE re-check BLOCKER on `ho/SRG-001` fix round 8719c96a (SRG backlog 35, 40).** (a) The additive overload `ConsentRules.IsValidFor(status, catId, appointmentId, kind, ...)` in `src/Domain/CatEcommerce.Domain/DomainServices/ConsentRules.cs` (a HO-EMR-001 section 13 file, HO-SRG-001 section 14 forbidden scope) is RATIFIED: additive only, the aggregate overload delegates to it, behaviour unchanged, and it is needed so `SurgeryConsentReader` can project consent columns instead of rehydrating EMR's aggregate (ADR-012). No other edit to an EMR file is authorised by this row. (b) The HO-APT-001 section 1 Amended row written by that fix round (2026-10-11, staff-time table, surgery columns and methods, G4, G5, G7, G8) is RATIFIED as the coordinator's row under HO-SRG-001 OQ-4. Mirrored here from HO-SRG-001 section 1 so this handover's owner sees the edit to `ConsentRules.cs`. |
| Amended | **2026-10-13 -- INTEGRATOR, on the OWNER'S DECISIONS of 2026-10-13 (the §14 edit to this file is authorised by them), closing ARCHITECTURE HIGH 1 of `ho/EMR-001-F` (`docs/gates/HO-EMR-001-F-ARCHITECTURE.md`).** Phase F (US-CLN-15) is built on `ho/EMR-001-F`. **(A) "Ratify the adapter":** (a) §8.12 SHP-1 is realised as `IClinicalSkuReader` (Domain port, `DomainServices/IClinicalSkuReader.cs`, model `Models/ClinicalStockModels.cs`) and `ClinicalSkuReader` (`Infrastructure/Consultation/`), because HO-SHP-001 published no `ISkuReader`; the **seven files outside §13** are authorised: `DomainServices/IClinicalSkuReader.cs`, `DomainServices/DispensaryBatchPicker.cs`, `Infrastructure/Consultation/ClinicalSkuReader.cs`, `Persistence/Migrations/20261013110937_AddVisitTreatments.cs` and its `.Designer.cs` (an eighth migration; §12.1 said Phase F adds none, but the owned table was never created in Phase B; the integrator regenerates it at merge), `Controllers/ClinicalResponses.cs` (already outside §13 since the first gate) and `docs/backlog/EMR.md`; the additive default member `IStockLedger.GetDispensarySkuIdsAsync` (CONTRACTS row 34) and the new codes/messages in the per-area partials are counted in scope. (b) **OQ-12 is re-answered:** the controlled flag comes from HO-PHM-001's `IControlledDrugDirectory.FindBySkuAsync` (a formulary medicine flagged controlled and linked to the SKU), not from a SKU flag; **(B) "Yes, formulary flag"**: kept as built -- a controlled SKU with no formulary link is not refused (the rule Inventory applies); the report of unlinked dispensary SKUs is backlog EMR row 55 (LOW). (c) HO-EMR-001 is a **consumer** of `ISkuDirectory` (HO-IMP-001), `ISkuPriceReader` (HO-PHM-001), `IBillingPricingInputs.GetServicePriceAsync` (HO-BIL-001) and `IControlledDrugDirectory` (HO-PHM-001), ratified by the owner (CONTRACTS row 33). (d) The stock unit snapshot is Inventory's `each`, in **whole units** (HO-IMP-001 OQ-19); IMP-4 and IMP-7 use `StockReferenceKind.Visit` (no `VisitTreatment` / `VisitTreatmentReversal`, D-2); **no ledger-entry ids are stored** (`StockEntryId` / `ReversalStockEntryId` dropped); a reversal lands in Quarantine (OQ-19). **(C) "Auto-split across batches" -- replaces the earlier "one line is one batch" note:** one record-treatment request draws from batches in Inventory's FEFO order and creates **one `TreatmentLine` per batch used** (each keeps its own lot, expiry and location); the warning (`400 InsufficientStock`) and the shortfall use the **whole dispensary's** usable, unreserved stock of the SKU; with `acceptShortfall` everything available is deducted across the batches and the remainder is the shortfall (recorded on the last line, one `RecordShortfall`); an expired batch is still refused (BR-3) whether named or not; a **named** batch is drawn first (BR-4: a reason when it is not the FEFO first) and the draw then **continues in FEFO order over the remaining batches, skipping the named one**; the request is **one** `DeductAsync` whose reference line is the first line's id. The client may send the first line id (`lineId`) as its idempotency key: a retry returns the lines already made (`200`) and deducts nothing; the other lines' ids are derived from it. Also from the gates: the charge-lines `ServiceDate` is the branch-local date of `FinalisedOn` (BIL's appointment source uses `CompletedOn`) and a missing branch fails rather than defaulting to UTC; the dispensary item list is paged (`pageNumber`, `pageSize` <= 100) and reads Inventory only through `IStockLedger`; a procedure quantity is bounded at 999,999,999.999. Backlog rows 41-62 in `docs/backlog/EMR.md`. The `IStockLedger.GetDispensarySkuIdsAsync` edit is ratified by the integrator in HO-IMP-001 §1 (2026-10-13), mirrored here; §12.4 now shows the `201` `lines[]` body, the `200` replay and the paged list. |
| Amended (HO-BIL-001 Phase J) | **2026-10-14 -- OWNER DECISIONS of 2026-10-14, additive cross-area edits authorised by them (integrator to review at merge); closes backlog EMR 43.** (1) **Owner at finalising (decision 1):** `VisitAggregate.CustomerId` (nullable `uuid` `clinic.visit.customer_id`) is set once by `Finalise` from the cat's owner at that moment (the handler reads the cat; a visit whose cat is missing is refused `CatNotFound`; `VisitFinalisationDto` gained a trailing `CustomerId`), and `VisitFinalisedEvent` carries `CustomerId`. This **narrows ADR-117** ("the visit holds no customer id", ADR-344): an open visit holds none and every owner-facing read still resolves the cat's current owner; `VisitAggregateTests.Open_HoldsNoCustomerId_UntilFinalise` was updated to say so. The event still has no `BoardingStayId` (decision 3: deferred to HO-BRD-001). (2) **Read port (the owner's architecture call):** `IVisitBillingReader` / `VisitBillingFacts` / `VisitBillingLine` (`Domain/DomainServices`, `Domain/Models/VisitBillingModels.cs`), implemented by `Infrastructure/Consultation/VisitBillingReader` and registered in `ConsultationModule`: typed `Money` / `decimal` / `VisitBillableLineKind` (`Shared/Common/Enums`, Procedure / Medicine / Consumable / Diagnostic) values for the billable, not reversed, priced treatment lines and the non-cancelled priced diagnostic orders of a finalised visit. `GetVisitChargeLinesQuery` is unchanged and not used by Billing. (3) **Diagnostic price (decision 2, ADR-345):** each `DiagnosticKind` has an admin-set price in the settings registry (`clinical.diagnosticPriceBloodWork`, `...Urinalysis`, `...FelvFivTest`, `...Imaging`, `...Other`; Decimal, default `0.00`, in `SettingDefinitions.Consultation.cs`, edited through the existing `/api/admin/settings`); `DiagnosticOrderAggregate` stores `unit_price_amount`, `unit_price_currency_code` and `tax_class_id` (nullable) frozen when the order is made, the tax class being the visit's own service's; `OrderDiagnosticCommandHandler` takes `IBusinessSettings` and `IBillingPricingInputs`. An order made before this has no price and is not billed. A diagnostic test is one *order*, not one analyte (owner question). (4) **Migration** `AddVisitBillingFacts` (three nullable columns on existing tables; no new table, so no `RecordClassification` entry is owed -- `clinic.visit` and `clinic.diagnostic_order` are already `Clinical` / `Forbidden`). The frozen-visit trigger allows the finalising UPDATE (the row is `Open` when it fires). (5) Tests: `VisitAggregateTests` (owner stored, refused without one, never changes), `DiagnosticHandlerTests` (price frozen, default 0, tax class null kept), `PhaseJContractTests`, and `BillingPhaseJEndpointTests` J1 to J7. CONTRACTS rows 36 to 38. |
| Amended (HO-BIL-001 Phase J gate fix) | **2026-10-14 -- corrections to the row above.** The migration `AddVisitBillingFacts` adds four nullable columns on EMR's tables (`clinic.visit.customer_id`, `clinic.diagnostic_order.{unit_price_amount, unit_price_currency_code, tax_class_id}`; the row above said three) and replaces `clinic.diagnostic_order_guard()` so the price and tax class cannot be changed after the order is made (additive SQL, integrator regenerates). The Read-layer uncaptured list (Billing's `GetUncapturedSourcesQuery`) counts a visit's billable treatments and priced, non-cancelled diagnostic orders against its recorded charges: a cross-context list over `clinic.*`, whereas Billing's capture reads a visit only through `IVisitBillingReader`. A visit finalised before `customer_id` existed, and a diagnostic order made before the price columns, are billed by hand (owner decision 2026-10-14; no back-fill). |
| Platform | `Platform.Infrastructure.Core` **1.0.34** — do not downgrade |

---

## 2. Objective

Give the clinic an **electronic medical record** that a vet can write during a consultation and that
the practice can defend afterwards. A checked-in cat gets a **visit**; a nurse records vitals; the vet
writes a SOAP note with coded problems, orders diagnostics, records treatments and the consumables
they used, sets a follow-up and **finalises** the visit. From then on the record is read-only;
corrections are **addenda**. The owner gets a discharge summary on paper and in the portal. Consent
for procedures is captured and stored, ready for Surgery to enforce.

Five things this slice is careful about:

1. **Immutability is enforced three times.** A finalised visit is refused by the aggregate, has no
   update path in the API, and is frozen by PostgreSQL triggers — the same "someone at a `psql`
   prompt" standard HO-ACC-001 set for the audit log (BR-11, D-5, ADR-118).
2. **Only vets finalise.** Appendix D gives *Administrator* read-only access to clinical notes, so the
   finalise action belongs to `Vet` alone — enforced at the API and re-checked in the domain (BR-6,
   ADR-119).
3. **Stock follows the treatment, in the same transaction.** Recording a consumable deducts the exact
   batch through Inventory's ledger in the commit that records the treatment. An expired batch is
   refused with no override (BR-3); a shortfall is recorded, never hidden (ADR-126).
4. **Every read of a clinical record is logged** (NFR-SEC-8), before the data is returned, fail closed
   (ADR-129).
5. **Files are handled safely.** Lab PDFs, DICOM files, photos and signed consent forms are validated by
   content, scanned before storage, kept outside the web root and served only through short-lived
   signed URLs (NFR-SEC-5, ADR-125). Files are stored and scanned by **HO-FIL-001** (ADR-318, ADR-319); **uploads are
   refused** until one is configured (OQ-6) — the same posture HO-NTF-001 took for SMS.

§17 builds the slice in **eight phases**; each compiles, migrates and passes its own tests first. Only
Phase F needs Inventory; A–E, G and H do not.

---

## 3. In scope

### 3.1 Stories

| Story | Pri | Pts | Capability delivered here | Phase |
|---|---|---|---|---|
| US-CLN-11 | M | 5 | Visit opened for a checked-in appointment; vitals (weight, temperature, heart rate, respiratory rate, body condition score, pain score) with units; two-tier ranges — outside *plausible* warns and needs confirmation, outside *absolute* is refused; weight appended to the cat's history in the same commit | B |
| US-CLN-12 | M | 8 | SOAP note + coded problem list saved as a draft on the open visit; a per-author **working copy** auto-saved and returned on the next load | C |
| US-CLN-20 | C | 5 | Note templates per service, returned with an empty note for the client to pre-fill; the applied template recorded | C |
| US-CLN-17 | M | 5 | Finalisation makes the visit read-only; addenda are separate, timestamped, attributed, append-only; the API refuses edits; DB triggers refuse them too | C |
| US-CLN-22 | M | 3 | Finalising (diagnoses) is `Vet`-only at the API and in the domain; refusals `403` and audited. *Prescribing half → HO-PHM-001 (§4.2)* | C |
| US-CLN-14 | M | 8 | Diagnostic orders (blood work, urinalysis, FeLV/FIV, imaging, other) recorded as pending; structured results with reference ranges and computed flags; PDF / JPEG / PNG / DICOM-as-file attachments with date and note; results append-only | D |
| US-CLN-19 | S | 3 | Clinical photos with date and note on an open visit; on the cat's clinical timeline | D |
| US-CLN-13 | M | 8 | Clinical history for a cat in one summary call plus paged sections; composed from section providers so later areas plug in; NFR-PRF-4 budget proved against a 10-year fixture | E |
| US-CLN-15 | M | 5 | Treatments (procedure, medicine, consumable); consumables and medicines deduct the exact batch from the branch dispensary with a ledger entry, FEFO by default, override with reason; expired refused; shortfall recorded and published for reordering; reversal writes a compensating entry | F |
| US-CLN-16 | M | 5 | Discharge summary derived from the finalised visit (diagnosis, treatment, medicines administered, home-care instructions, follow-up date) as JSON and PDF, for staff and — finalised only — for the owner in the portal; an owner message when it is ready | G |
| US-CLN-18 | M | 3 | Follow-up from the consultation: either link an appointment booked through HO-APT-001, or queue a reminder to the owner with a send-time guard | G |
| US-CLN-21 | M | 5 | Consent (surgery, anaesthesia, euthanasia) by on-screen signature or signed upload, linked to cat, appointment (the procedure booking), kind, date and the exact consent-form version; withdrawal; the consent-status contract for Surgery. *"Cannot be marked started" → HO-SRG-001 (§4.2)* | H |

Also delivered: the **shared stored-file facility** (§8.10) — unless HO-IMP-001 has already built one
that meets the same contract (§20.3); the **clinical-record access log** (§8.11).

### 3.2 Decisions already settled upstream — do not re-litigate

| Decision | Where |
|---|---|
| In-memory CQRS, one host, `IInMemoryDispatcher` only; never `202` | ADR-010, 013, 014, 019, 020 |
| PostgreSQL / EF Core, one `DbContext`, `xmin` → `409`; `SaveChangesAsync` per repository call | ADR-011, 016, 021 |
| `Clinic` context, schema `clinic`, shared with this slice | HO-APT-001 ADR-067 |
| Appointment status machine; `AppointmentStartedEvent` / `AppointmentCompletedEvent`; "HO-EMR-001 may add invariants to `Complete`" | HO-APT-001 §8.3, §4.2 |
| Weight history is its own root; current weight derived read-side | ADR-023 |
| Every aggregate change audited in the same transaction; `AuditCaptureExclusions` is data | HO-ACC-001 ADR-040, HO-NTF-001 ADR-066 |
| `[RequireAction]` + the one `ActionRoles` table; every endpoint declares access; customer ownership is `404` | HO-ACC-001 ADR-036, HO-ADM-001 ADR-043 |
| Branch-scoping rule: an aggregate at a place carries `BranchId` | HO-ADM-001 §8.2 |
| Prices snapshotted at transaction time | HO-ADM-001 ADR-049 |
| Policy documents are versioned `LimitedMarkup`, never HTML | HO-ADM-001 ADR-052 |
| Messages only via `INotificationOutbox.Enqueue` in a command handler; never from an event handler; `SendAfter` + `SubjectStamp` + guards for timed messages | HO-NTF-001 ADR-056, 057; HO-APT-001 ADR-074 |
| No payment gateway; charges reach Billing as records, paid at the clinic | SRS A-2, HO-APT-001 ADR-073 |

---

## 4. Out of scope

### 4.1 Frontend — all of it

Screens are [`HO-EMR-002`](HO-EMR-002.md). Criteria phrased as UI are met here at the contract level:

| Story AC (UI phrasing) | Server half — delivered here |
|---|---|
| US-CLN-11 "warned before saving but may confirm" | `400 VitalsOutsidePlausibleRange` listing each field with its plausible range; the same request with `confirmedOutOfRange` saves and records the confirmation |
| US-CLN-12 "restored on return" | `PUT …/draft` stores the author's working copy; `GET` of the visit returns it when newer than the saved note |
| US-CLN-13 "without losing my draft" | History is a set of read endpoints on the cat; nothing about reading it touches the visit |
| US-CLN-14 "flagged" | Every result value carries `flag` (`Normal`, `Low`, `High`, `Abnormal`, `NotAssessed`) and the range it was judged against |
| US-CLN-16 "available to print" | `GET …/discharge-summary.pdf` |
| US-CLN-20 "pre-fills and remains fully editable" | `template` in the visit payload while the note is empty; the client pre-fills; saving is an ordinary save |
| UI-8 "units always visible" | Every measurement travels as `{ value, unit }`; no bare number in a clinical payload |

### 4.2 Deferred to a named later area — contracts delivered here

| Capability | Story / AC | Where it lands | What this slice delivers |
|---|---|---|---|
| Refuse "procedure started" without consent; vet override | US-CLN-21 AC2 (→ US-SRG-03) | **HO-SRG-001** | `ConsentRules.IsValidFor` (Domain) and `GetConsentStatusQuery` (§8.9) — the rule Surgery calls |
| Refuse **prescribing** by a non-vet | US-CLN-22 (prescribe half), AC-18 | **HO-PHM-001** | The pattern: `Vet`-only action, domain re-check (ADR-119). HO-PHM-001 adds `Prescriptions.Issue` |
| Take-home **prescriptions** on the discharge summary | US-CLN-16 ("medicines", dispensed part) | **HO-PHM-001** | `IDischargeSummaryContributor` slot (§8.8); until registered the summary lists medicines administered in the clinic and prints *"Medicines to take home are on the prescription label."* |
| History panels: **prescriptions**, **boarding stays**, **food purchases** | US-CLN-13 AC1 | **HO-PHM-001**, **HO-BRD-001**, **HO-ORD-001** | `IClinicalHistorySectionProvider` (§8.7); the three keys are declared and report `available: false` with the owning area until a provider is registered |
| Invoice lines for the visit | FR-BIL-2 | **HO-BIL-001** | `VisitFinalisedEvent` and `GetVisitChargeLinesQuery` with snapshotted prices (ADR-127) |
| Reorder suggestions from clinical shortfalls | US-CLN-15 AC2 ("for reordering") | **HO-IMP-001** | `ClinicalStockShortfallRecordedEvent` and the shortfall on the treatment line |
| Recall tracing through clinic dispensing | FR-IMP-11 | **HO-IMP-001** | Ledger entries referencing `VisitTreatment` + `clinic.visit_treatment (batch_id)` index |
| Controlled-drug register entries | FR-SRG-8, FR-PHM-7 | **HO-SRG-001 / HO-PHM-001** | Controlled SKUs are **refused** as treatments here (ADR-126) so none is ever used without a register entry |
| Visit opened from a boarding referral | FR-BRD-15, AC-13 | HO-BRD-001 | `VisitAggregate.Open` takes an origin; only `Appointment` exists now (OQ-9) |
| Portal dashboard | FR-POR-1 | HO-POR-001 | The customer reads in §12.4 |
| Medical-history PDF gaining diagnoses and results | US-CAT-10 | HO-CAT amendment (OQ-17) | Nothing — HO-CAT-001's PDF is not edited here |
| Retention enforcement, orphaned-file purge | US-XCT-06 | HO-XCT-001 | No delete path exists for any clinical record or attached file |

### 4.3 Not buildable yet — named gaps

| Gap | Consequence |
|---|---|
| **No malware scanner chosen** (OQ-6) | `UnconfiguredMalwareScanner` refuses every upload outside Development with `FileScanUnavailable`. Lab attachments, photos and signed-form uploads work in Development and Testing only. **On-screen consent signatures still work everywhere** — they are data, not files (ADR-132) |
| **No object storage provisioned** (OQ-7) | `LocalFileStore` writes under a configured root outside the content root; an S3-compatible adapter is one class behind `IFileStore` |
| **No SMS or email provider** (HO-NTF-001 OQ-1) | Summary-ready and follow-up messages are enqueued and logged; outside Development only the in-app copy is delivered |
| Coded diagnosis vocabulary undecided (OQ-2) | In-house catalogue with an optional external code; one system code seeded |
| Vital-sign ranges and scales need clinical sign-off (OQ-3) | Constants in `VitalSignRules`, clearly marked; changing them is a one-file edit |
| HO-IMP-001 / HO-SHP-001 not written at baseline | Phase F cannot start until both are merged and match §8.12 — §20.2 |
| Regulatory jurisdiction and retention (SRS Q-1) | Nothing is ever deleted; retention periods are HO-XCT's |

### 4.4 Explicitly forbidden shapes

No `IBusMessageDispatcher`, MassTransit, second host, `CommandService` layer, `202 Accepted`,
`[Authorize]`. **No update or delete path for a finalised visit, an addendum, a result set, a
consent or a stored file.** **No `IFormFile` or bytes in a command** — the upload endpoint registers a
file and commands carry a `FileId`. **No file served from a static-files middleware or a public
path.** **No HTML stored or rendered from clinical text.** **No enqueue from an event handler.** **No
stock quantity stored on any clinical table** — stock is Inventory's ledger (D-3). **No batch
expiry override.** **No payment on a visit.**

---

## 5. Prerequisites

| Required | State |
|---|---|
| HO-CAT-001 merged (incl. QuestPDF) | ✅ at `4007225` |
| **HO-ACC-001, HO-ADM-001, HO-NTF-001, HO-APT-001 merged** | ❌ **not implemented at baseline** — §20.1 |
| **HO-SHP-001 and HO-IMP-001 merged, matching §8.12** | ❌ **not written at baseline** — blocks **Phase F only** (§20.2) |
| PostgreSQL 17.5 `catecommerce_dev`, `dotnet-ef` 9.0.0 | ✅ |
| New NuGet packages | **None.** HMAC (`System.Security.Cryptography`), multipart (`Microsoft.AspNetCore.WebUtilities`) and QuestPDF are already available |
| A writable directory outside the content root for `FileStorage:RootPath` | Developer machine: any local path; the test factory uses a per-run temp directory |

The shared-database rules of HO-CAT-001 §16.1, HO-ACC-001 §16.1 and HO-APT-001 §16.1 apply. Diagnosis
codes, analyte codes and template-per-service are **unique keys** — mint them per test.

---

## 6. Governing architecture documents

Identical to [HO-ACC-001 §6](HO-ACC-001.md#6-governing-architecture-documents) — `.ai/architecture.md`,
`.ai/cqrs.md`, `.ai/bounded-contexts.md`, `.ai/namespace-conventions.md`, `.ai/decisions/adr.md`,
`.ai/command-handler-pattern.md`, `.ai/read-side-pattern.md`, `.ai/persistence-ef-core.md`,
`.ai/domain-events-and-outbox.md`, `.ai/enumeration-pattern.md`, `.ai/di-registration-guide.md`,
`.ai/host-composition.md`, `.ai/feature-creation-checklist.md`. Of note here:

| Document | Why it matters in this slice |
|---|---|
| [`.ai/bounded-contexts.md`](../../.ai/bounded-contexts.md) | Names **`Visit`** with `VisitAggregate` (vitals, SOAP, diagnoses, treatments). **Rule 1**: two recorded exceptions (ADR-121, ADR-126). **Rule 3**: why addenda, diagnostic orders, drafts and consents are their own roots, and why per-visit children are owned (each bounded). **Rule 4**: FEFO is a domain service — Inventory's, consumed here |
| [`.ai/namespace-conventions.md`](../../.ai/namespace-conventions.md) | `Visit` is a listed feature name — used for every namespace in this slice. `StoredFile` is **not** listed — ADR-125 records it |
| [`.ai/flows/clinic-visit-flow.md`](../../.ai/flows/clinic-visit-flow.md) | The flow and its invariants: cannot finalise twice; addenda after; **at least one diagnosis to finalise**; vet-only re-checked in the handler; Clinic never writes `billing.*` |
| [`.ai/persistence-ef-core.md`](../../.ai/persistence-ef-core.md) | §4 owned collections (nested `OwnsMany` for result values); §6 `xmin`; §8 column types. Triggers are raw SQL in migrations |
| [`.ai/domain-events-and-outbox.md`](../../.ai/domain-events-and-outbox.md) | §3 a failing handler fails the request after commit — why stock and weight writes are **in** the command, not in handlers; §4–§5 outbox for the two messages |
| [`.ai/decisions/adr.md`](../../.ai/decisions/adr.md) | ADR-009 (single policy table), ADR-015 (no service wrapping a repository call), ADR-016, ADR-021 (F23-style commits), ADR-023 |
| [`HO-APT-001`](HO-APT-001.md) §8.3, §12.3 | The appointment states a visit may open in; the action naming style |
| [`HO-NTF-001`](HO-NTF-001.md) §8.2, §8.4, §8.7, §8.8 | Template catalogue rules, producer rules, guard extension point, link builder |
| [`HO-ADM-001`](HO-ADM-001.md) §8.6, §8.7, §8.8, §12.3 | Service prices, settings registry, policy versions (consent text), `OwnerScope` / `CustomerActions` |

---

## 7. Verified platform facts

HO-CAT-001 F1–F10, HO-ACC-001 F11–F18, HO-ADM-001 F19–F22, HO-NTF-001 F23–F27 and HO-APT-001
F28–F30 apply. **F23 is load-bearing here**: a repository save commits every tracked entity in the
scoped context.

**No new fact was established for this slice.** The following are **to be verified in Phase A step 1**
and recorded as F31–F33 in the test sheet before any code depends on them:

| # | To verify | If it does not hold |
|---|---|---|
| V1 | A PL/pgSQL `BEFORE UPDATE OR DELETE` row trigger on an EF-mapped table raises through Npgsql as `PostgresException` with the trigger's `SQLSTATE` (`P0001` or a custom one) and rolls back the whole `SaveChangesAsync` | Stop (§20.5); the immutability design depends on it |
| V2 | Nested `OwnsMany` (result values inside result sets) maps and round-trips on EF Core 9 / Npgsql with `ValueGeneratedNever()` on both levels (ADR-021) | Flatten values into one owned collection with a `ResultSetId` column — a local change, record it |
| V3 | Kestrel's `[RequestSizeLimit]` + `[RequestFormLimits(MultipartBodyLengthLimit)]` on one action override the global limit, and a larger body is refused with `413` before the action runs | Enforce the limit while streaming and refuse with `400 FileTooLarge` |

---

## 8. Domain model

### 8.1 Context and aggregate boundaries

```
Clinic context — schema `clinic` (HO-APT-001 ADR-067), namespace segment `Visit` (ADR-117)
VisitAggregate               (root)  → BranchId, AppointmentId (unique), CatId        clinic.visit
├── Note         [owned one]   SoapNote                                                 (columns on visit)
├── Problems     [owned many ≤ 20]   VisitProblem                                       clinic.visit_problem
├── Vitals       [owned many ≤ 20]   VitalsEntry                                        clinic.visit_vitals
├── Treatments   [owned many ≤ 100]  TreatmentLine                                      clinic.visit_treatment
├── Photos       [owned many ≤ 20]   ClinicalPhoto                                      clinic.visit_photo
└── FollowUp     [owned one, opt]    FollowUp                                           (columns on visit)
VisitAddendumAggregate       (root, append-only) → VisitId, CatId                       clinic.visit_addendum
ConsultationDraftAggregate   (root)  → VisitId + AuthorStaffId (unique)                 clinic.consultation_draft
DiagnosticOrderAggregate     (root)  → VisitId, CatId, BranchId                         clinic.diagnostic_order
├── RequestedAnalytes [owned many ≤ 50]                                                 clinic.diagnostic_requested_analyte
├── ResultSets        [owned many ≤ 10]  DiagnosticResultSet                            clinic.diagnostic_result_set
│   └── Values        [owned many ≤ 100] DiagnosticResultValue                          clinic.diagnostic_result_value
└── Attachments       [owned many ≤ 20]  DiagnosticAttachment                           clinic.diagnostic_attachment
ConsentRecordAggregate       (root)  → CatId, AppointmentId, BranchId                   clinic.consent_record
DiagnosisCodeAggregate       (root, catalogue)                                          clinic.diagnosis_code
LabAnalyteAggregate          (root, catalogue)                                          clinic.lab_analyte
NoteTemplateAggregate        (root, catalogue) → ServiceId                              clinic.note_template

Files — schema `files`, namespace segment `StoredFile` (ADR-125)
StoredFileAggregate          (root)                                                     files.stored_file

Existing roots written (created, never mutated) by this slice
CatWeightRecordAggregate     (HO-CAT-001)  — created with vitals (ADR-121)
Inventory stock ledger rows (HO-IMP-001)  — enlisted via IStockLedger with treatments (ADR-126)
AuditEntryAggregate          (HO-ACC-001)  — clinical-record access rows (ADR-129)
```

**Why these boundaries.** Everything a vet writes *during* the visit and that freezes *with* it is
owned by the visit — each collection is bounded per visit, and finalisation must freeze all of it at
once. Things that legitimately continue **after** finalisation are separate roots: addenda
(corrections), diagnostic orders (results come back days later), consent (belongs to a procedure, not
a note). Drafts are separate because they are a per-author working copy, not the record (ADR-122).

**One aggregate mutated per command**, with two recorded exceptions, each a single commit:
- **Recording vitals** that include a weight also **creates** a `CatWeightRecordAggregate` (ADR-121).
- **Recording or reversing a treatment** that moves stock also **enlists Inventory's ledger rows**
  through `IStockLedger` (ADR-126, README C-2).

The mechanism is the same for both and is written once (§11.4 "companion commit"): mutate the visit
in memory; `SaveAsync` the new companion root — per **F23** that one `SaveChangesAsync` commits the
tracked visit changes with it, and the visit's `xmin` is checked in that statement; then
`UpdateAsync` the visit, which finds nothing left to write and dispatches the visit's events. I-B4 and
I-F6 prove atomicity by forcing a failure.

Cross-aggregate reads — the appointment's status and cat, the cat's life status, the service's price,
the SKU, the current policy version — are repository or query calls in the handler.

### 8.2 `VisitAggregate`

| Field | Type | Notes |
|---|---|---|
| `BranchId` | Guid | From the appointment |
| `Origin` | `VisitOrigin` | `Appointment` only in this slice (OQ-9) |
| `AppointmentId` | Guid | **Unique** — one visit per appointment |
| `CatId` | Guid | From the appointment. **No customer id** — owner-facing reads resolve the cat's *current* owner (FR-CAT-8), and a frozen row cannot follow a merge |
| `ServiceId`, `ServiceName`, `ServiceCategory` | Guid, string ≤ 200, `ServiceCategory` | Snapshot from the appointment's `ServiceSnapshot` — drives the template lookup |
| `Status` | `VisitStatus` | `Open` → `Finalised` |
| `OpenedOn`, `OpenedBy` | | |
| `Note` | owned `SoapNote` | `Subjective`, `Objective`, `Assessment`, `Plan` (each ≤ 20 000), `DischargeInstructions` (≤ 10 000) — plain text; `NoteSavedOn?`, `NoteSavedBy?`, `AppliedTemplateId?`, `AppliedTemplateVersion?` |
| `Problems` | owned | `Id`, `DiagnosisCodeId`, `Code` + `Name` (**snapshot**), `Certainty` (`Provisional` \| `Confirmed`), `Note?` ≤ 500 |
| `Vitals` | owned | `Id` (also the weight record's id — ADR-121), `MeasuredOn`, `WeightKg?` (5,2), `TemperatureC?` (4,1), `HeartRateBpm?`, `RespiratoryRatePerMin?`, `BodyConditionScore?`, `PainScore?`, `ConfirmedOutOfRange` (`VitalSign[]`, `int[]`), `Note?` ≤ 500, `RecordedBy` |
| `Treatments` | owned | §8.6 |
| `Photos` | owned | `Id`, `FileId`, `TakenOn` (`DateOnly`), `Note?` ≤ 500, `AddedOn`, `AddedBy` |
| `FollowUp?` | owned | `Kind` (`Appointment` \| `Reminder`), `DueOn` (`DateOnly`), `AppointmentId?`, `Stamp` (string — changed by every set), `SetOn`, `SetBy` |
| `FinalisedOn?`, `FinalisedBy?` | | `FinalisedBy` is the attending vet of record |

| Method | Rules | Event |
|---|---|---|
| `Open(dto)` | Appointment in `CheckedIn` or `InConsultation` (the handler passes its status) else `AppointmentNotReadyForVisit`; cat alive else `CatIsDeceased` | `VisitOpenedEvent` |
| `RecordVitals(dto)` | **`EnsureOpen()`**; at least one value → `VitalsRequired`; each value inside its **absolute** range → `VitalSignOutOfRange` (refused, no confirm); each value outside its **plausible** range must be in `dto.ConfirmedOutOfRange` → else `VitalsOutsidePlausibleRange` with every such field (§8.3); `MeasuredOn` not in the future; ≤ 20 entries | `VisitVitalsRecordedEvent` |
| `SaveNote(dto)` | `EnsureOpen()`; lengths; problems ≤ 20, each code **active** (handler supplies snapshots) → `DiagnosisCodeInactive`; no duplicate code → `DuplicateProblem`; replaces note and problems wholesale | `ConsultationNoteSavedEvent` |
| `RecordTreatment(dto)` · `ReverseTreatment(lineId, reason)` | `EnsureOpen()`; §8.6 | `TreatmentRecordedEvent` · `TreatmentReversedEvent` (+ `ClinicalStockShortfallRecordedEvent` when a shortfall is recorded) |
| `AttachPhoto(dto)` | `EnsureOpen()`; ≤ 20; `TakenOn` not future; the file's purpose is `ClinicalPhoto` (handler) | `ClinicalPhotoAttachedEvent` |
| `SetFollowUp(dto)` · `ClearFollowUp()` | `EnsureOpen()`; `DueOn` after today (branch-local, handler) → `FollowUpDateInPast`; `Appointment` kind needs `AppointmentId` of a `Booked`, future appointment for **this cat** (handler) → `FollowUpAppointmentInvalid`; new `Stamp` each time | `FollowUpSetEvent` · `FollowUpClearedEvent` |
| `Finalise(dto)` | `EnsureOpen()` (a second finalise → `VisitFinalised`); `dto.FinaliserRoles` contains `Vet` → else `OnlyVeterinarianMayFinalise` (ADR-119); ≥ 1 problem → `ProblemRequired`; `Assessment` and `Plan` non-blank → `AssessmentAndPlanRequired` (ADR-123) | `VisitFinalisedEvent` |

`EnsureOpen()` is one private guard throwing `DomainRuleViolationException(VisitFinalised, "This visit
has been finalised and can no longer be changed. Add an addendum instead.", …)`. **There is no
method that changes a finalised visit — not even for administrators.**

### 8.3 Vitals and ranges — US-CLN-11, UI-8 — ADR-120

`VitalSignRules` (Shared, static — **clinical lead to confirm, OQ-3**):

| Sign | Unit | Absolute (refused outside) | Plausible (confirm outside) | Precision |
|---|---|---|---|---|
| `Weight` | kg | 0.10 – 30.00 (HO-CAT-001 `Weight`) | 0.30 – 12.00 | 2 dp |
| `Temperature` | °C | 25.0 – 45.0 | 35.0 – 41.5 | 1 dp |
| `HeartRate` | bpm | 20 – 400 | 100 – 260 | integer |
| `RespiratoryRate` | breaths/min | 4 – 150 | 12 – 80 | integer |
| `BodyConditionScore` | /9 | 1 – 9 | — | integer (9-point scale, OQ-3) |
| `PainScore` | /10 | 0 – 10 | — | integer (0–10 scale, OQ-3) |

`VitalRangeCheck.Evaluate(values) → (absoluteViolations[], plausibleWarnings[])` is **pure** and is
called by the aggregate; the read side returns the same table (`GET /api/staff/clinical/vital-sign-rules`)
so the client warns early from the server's numbers — the server remains the decision.

The warning response (`400`, code `VitalsOutsidePlausibleRange`) carries
`outOfRange: [{ sign, value, unit, plausibleMin, plausibleMax }]` in addition to the usual validation
body. Re-sending with those signs in `confirmedOutOfRange` saves the entry and **stores which values
were confirmed**, so the record shows that the vet or nurse saw the warning.

**Weight goes to the cat's history** (ADR-121): when `WeightKg` is present, the handler creates a
`CatWeightRecordAggregate` with **`Id = VitalsEntry.Id`**, `RecordedOn` = the branch-local date of
`MeasuredOn`, `RecordedByStaffId` = the caller, `Notes = null`, in the same commit. The shared id is
the link; HO-CAT-001's aggregate is not edited.

### 8.4 Notes, drafts, templates, finalisation, addenda — US-CLN-12, 17, 20, 22

**The note on the visit is the draft** while the visit is `Open` (US-CLN-12 AC1): an explicit
`SaveConsultationNoteCommand` with `ExpectedVersion` replaces it (a concurrent save by another vet →
`409`). The change-capture interceptor audits every explicit save.

**`ConsultationDraftAggregate`** — the auto-saved working copy (US-CLN-12 AC2, ADR-122): unique
`(VisitId, AuthorStaffId)`, created on first write, then `Overwrite(content, savedOn)`. Content is the
same shape as the note plus problems as `jsonb` (code ids only, ≤ 20). Rules: the visit must be `Open`
and the caller must be allowed to write the note; size limits as the note. It is **excluded from audit
change capture** (appended to `AuditCaptureExclusions`) — it is overwritten every few seconds and is
not the record; the record's changes are audited when saved or finalised. It is never deleted and
never read by anyone but its author. The visit read returns the caller's draft **only when**
`draft.SavedOn > note.NoteSavedOn` (or no note has been saved) **and** the visit is `Open`.

**Templates** (US-CLN-20, ADR-134): **`NoteTemplateAggregate`** — `ServiceId` (one **active** template
per service, filtered unique index), `Name` ≤ 200, the five text fields as defaults, `Version` (1, 2 …
bumped on every edit), `IsActive`. The visit read includes `template` (id, version, texts) when the
visit is `Open`, no note has ever been saved, and an active template exists for `ServiceId`. The client
pre-fills; `SaveNote` records `AppliedTemplateId`/`Version` when supplied.

**Finalisation** (US-CLN-17 AC1, US-CLN-22): `FinaliseVisitCommand(VisitId, ExpectedVersion)`. The
handler (1) loads the visit, (2) confirms the active branch is the visit's branch, (3) calls
`Finalise` with the caller's roles, (4) enqueues `visit.summary-available` (§8.8), (5) saves.
Pending diagnostic orders do **not** block finalisation — results arrive later on their own root.
Finalising **does not complete the appointment**; the vet completes it through HO-APT-001, which now
refuses while a visit is open (ADR-133).

**Addenda** (US-CLN-17 AC2): **`VisitAddendumAggregate`** — `VisitId`, `CatId`, `Text` (1…4000,
plain), `Reason?` ≤ 500, `AuthorStaffId`, `AddedOn`. `Add(dto)` requires the visit to be `Finalised`
(handler passes status) → else `AddendumRequiresFinalisedVisit` (*"This visit is still open — edit the
note instead."*). No other method. The original text stays exactly as finalised; the read returns
addenda in `AddedOn` order beneath it.

**Direct edit through the API** (US-CLN-17 AC3): every write on a finalised visit returns `400
VisitFinalised`. There is no `PUT` that could bypass the aggregate.

**Database layer** (ADR-118) — migration `AddClinicalRecordImmutability`, functions in schema
`clinic`, raising `SQLSTATE 'CR001'` with a message naming the table:

| Trigger | Table | Refuses |
|---|---|---|
| `tr_visit_frozen` | `visit` | `UPDATE` when `OLD.status = Finalised`; **every** `DELETE` |
| `tr_visit_child_frozen` | `visit_problem`, `visit_vitals`, `visit_treatment`, `visit_photo` | `INSERT`/`UPDATE`/`DELETE` when the parent visit is `Finalised` |
| `tr_append_only` | `visit_addendum`, `diagnostic_result_set`, `diagnostic_result_value`, `diagnostic_attachment`, `files.stored_file` | every `UPDATE`, `DELETE` |
| `tr_no_delete` | `diagnostic_order`, `consent_record`, `visit_problem`/`visit_vitals`/`visit_treatment`/`visit_photo` rows of an open visit **except** problems | `DELETE` (problems are replaced wholesale while open, so their deletes are allowed while open) |
| `BEFORE TRUNCATE` | every table above | always |

The finalised status value in the trigger is `VisitStatus.Finalised.Value`, written from the
enumeration in migration code (U-X3), never a literal.

### 8.5 Diagnostics — US-CLN-14 — ADR-124

**`DiagnosticOrderAggregate`** — `VisitId`, `CatId`, `BranchId`, `Kind` (`BloodWork`, `Urinalysis`,
`FelvFivTest`, `Imaging`, `Other`), `Description` (≤ 500; required for `Other` and `Imaging`),
`RequestedAnalytes` (`AnalyteId`, `Code`, `Name` snapshot), `OrderedBy`, `OrderedOn`, `Status`
(`Pending` → `Resulted`; `Pending` → `Cancelled`), `CancelledReason?`, `ResultSets`, `Attachments`.

| Method | Rules | Event |
|---|---|---|
| `Order(dto)` | Visit `Open` (handler) → else `VisitFinalised`; kind rules | `DiagnosticOrderedEvent` — **pending** (AC1) |
| `RecordResults(dto)` | Not `Cancelled`; ≤ 10 sets; 1…100 values; `SupersedesResultSetId` (if given) is an existing set of this order; each value classified (below); sets `Status = Resulted` | `DiagnosticResultsRecordedEvent` |
| `AttachFile(dto)` | Not `Cancelled`; ≤ 20; file purpose `LabResult` (handler); `ResultDate` not future; `Note?` ≤ 500 | `DiagnosticFileAttachedEvent` |
| `Cancel(reason)` | `Pending` with no results or attachments → else `DiagnosticOrderHasResults` | `DiagnosticOrderCancelledEvent` |

Results and attachments are **allowed after the visit is finalised** — the lab report arrives after
the consultation. They are **append-only**: a correction is a new result set that names the one it
supersedes; the earlier set stays visible (triggers, §8.4).

**`DiagnosticResultValue`** — `AnalyteId?`, `Name` ≤ 100, `ValueKind` (`Numeric` \| `Qualitative`),
`NumericValue?` (`numeric(14,4)`), `TextValue?` ≤ 100, `Unit?` ≤ 30 (required for numeric), 
`ReferenceLow?`, `ReferenceHigh?`, `NormalText?` ≤ 50, `Flag`. When `AnalyteId` is given, name, unit
and range default from `LabAnalyteAggregate` **and are stored on the value** (the lab report's range
wins when entered — FR-CLN-16 "results shall record reference ranges").

`ReferenceRangeClassifier` (pure): numeric — `< low` → `Low`, `> high` → `High`, inside → `Normal`,
no range → `NotAssessed`; qualitative — `NormalText` set → equal (ordinal-ignore-case, trimmed) →
`Normal` else `Abnormal`; unset → `NotAssessed`. Bounds inclusive.

**`LabAnalyteAggregate`** — `Code` (unique, ≤ 30), `Name`, `ValueKind`, `Unit?`, `ReferenceLow?` ≤
`ReferenceHigh?`, `NormalText?`, `DiagnosticKind`, `IsActive`. No seed (clinical content, OQ-3).

### 8.6 Treatments and stock — US-CLN-15 — ADR-126, ADR-127

**`TreatmentLine`** (owned): `Id`, `Kind` (`Procedure` \| `Medicine` \| `Consumable`), `Description` ≤
300, `ServiceId?` (a billable clinic service), `SkuId?`, `SkuName?` + `StockUnit?` (snapshot),
`BatchId?`, `LotNumber?`, `BatchExpiresOn?`, `LocationId?`, `Quantity` (`numeric(12,3)` > 0),
`DeductedQuantity`, `ShortfallQuantity`, `Dose?` ≤ 100, `Route?` ≤ 50, `IsBillable`,
`UnitPriceSnapshot?` (`Money`) + `TaxClassId?`, `BatchOverrideReason?` ≤ 300, `StockEntryId?`,
`Status` (`Recorded` \| `Reversed`), `RecordedOn`, `RecordedBy`, `ReversedOn?`, `ReversedBy?`,
`ReversalReason?`, `ReversalStockEntryId?`.

`RecordTreatmentCommand` handler sequence:

1. Load the visit; `EnsureOpen`; active branch = visit branch.
2. `Procedure`: `ServiceId` optional; price snapshot from HO-ADM-001's price at *now* when billable.
3. `Medicine` / `Consumable`: `SkuId` required. Load the SKU through HO-SHP-001's read port; **controlled
   drug → `ControlledDrugNotSupportedHere`** (ADR-126). Resolve the branch's **clinic dispensary**
   location (HO-IMP-001). Read issuable batches (non-expired as of the branch-local date, available,
   not quarantined/recalled, FEFO order).
4. Batch: none given → the **FEFO** first batch. Given and not the FEFO first → `BatchOverrideReason`
   required → else `BatchOverrideReasonRequired` (BR-4). Given and **expired** → `BatchExpired` —
   **no override exists** (BR-3, FR-IMP-9). Given and not issuable for another reason →
   `BatchNotIssuable`.
5. Quantity check: `available ≥ Quantity` → deduct all. Otherwise, **without** `AcceptShortfall` → `400
   InsufficientStock` with `availableQuantity` and the unit (AC2 "I am warned"); **with** it →
   `DeductedQuantity = available` (may be 0), `ShortfallQuantity = Quantity − available`, and a
   `IStockLedger.RecordShortfall(StockShortfall(SkuId, LocationId, Quantity, VisitId, LineId))` is
   called for HO-IMP-001's reorder list (AC2 "record the shortfall") — enlisted, not an event.
6. Billable SKU → unit price snapshot from HO-SHP-001's price at *now* (ADR-127).
7. `visit.RecordTreatment(dto)`; when `DeductedQuantity > 0`, call
   `IStockLedger.DeductAsync(StockDemand(BranchId, SkuId, DeductedQuantity, Purpose: ClinicalUse),
   StockReference(VisitTreatment, LineId), StockParty(CustomerId, CatId))` and **companion-commit**
   (§8.1) — the port enlists without saving, so one `SaveChangesAsync` commits visit and movement.
   The line stores the returned `StockMovementLine` ids.
8. A concurrency refusal from Inventory (another issue took the last units in between) → `409
   InsufficientStock`; nothing committed; the client re-reads batches.

**Reversal** (`ReverseTreatmentLineCommand`, reason required, open visit only): the line becomes
`Reversed`; when stock was deducted, a compensating ledger entry (`+DeductedQuantity`, `ReferenceKind =
VisitTreatmentReversal`) is companion-committed. **An expired batch is still returned to stock as
expired** — Inventory's sellable view already excludes it. Lines are never removed.

### 8.7 Clinical history — US-CLN-13, NFR-PRF-4 — ADR-128

**`IClinicalHistorySectionProvider`** (Read, one implementation per section):

```csharp
string Key { get; }                       // "visits", "lab-results", …
Task<ClinicalHistorySectionSummary> GetSummaryAsync(Guid catId, CancellationToken ct);
Task<PagedResult<ClinicalHistoryItem>> GetPageAsync(Guid catId, int pageNumber, int pageSize, CancellationToken ct);
```

`ClinicalHistorySections.All` (Shared) declares every section and its owner:

| Key | Owner | Provider in this slice |
|---|---|---|
| `timeline` | HO-EMR-001 | ✅ merges visits, photos, result sets, vaccinations, parasite treatments, weights by date |
| `visits` | HO-EMR-001 | ✅ date, service, vet, status, problems, addendum count |
| `diagnoses` | HO-EMR-001 | ✅ distinct problems across finalised visits, first/last seen, certainty |
| `lab-results` | HO-EMR-001 | ✅ orders with latest result set and flags |
| `photos` | HO-EMR-001 | ✅ with signed thumbnail URLs |
| `vaccinations`, `parasite-treatments`, `weights` | HO-CAT-001 (data) | ✅ read-only providers over `clinic.cat_*` |
| `prescriptions` | **HO-PHM-001** | ❌ `available: false` |
| `boarding-stays` | **HO-BRD-001** | ❌ `available: false` |
| `food-purchases` | **HO-ORD-001** | ❌ `available: false` |

`GetClinicalHistorySummaryQuery` returns: the cat header data needed by the drawer, **active
problems** (latest certainty per code over the last 12 months), **last 5 visits**, **last vitals**,
**full weight series** (points only), vaccination status (HO-CAT-001's calculator), **pending diagnostic
orders**, and per section `{ key, available, owner, count }`. Every provider uses `AsNoTracking`,
projects before materialising and is served by an index in §12.1.

**Budget.** NFR-PRF-4 is 2 s p95 for the rendered screen. The server's share is **≤ 600 ms p95 for the
summary and ≤ 300 ms p95 for a 20-item section page** against the 10-year fixture (§16.1 rule 4): 60
visits × (3 problems, 2 vitals, 3 treatments, 1 photo), 250 weights, 40 vaccinations, 40 parasite
treatments, 80 diagnostic orders × 12 values. I-E4 measures it; HO-EMR-002 E-D2 measures the render.

### 8.8 Discharge summary — US-CLN-16 — ADR-130

**Derived, never stored.** `GetDischargeSummaryQuery(visitId)` builds, from a **finalised** visit:
branch (name, address, phone), cat (name, sex, age, weight at visit), date, attending vet, problems
(code name + certainty), treatments (non-reversed lines: description, dose, route, quantity + unit),
**medicines administered** (the `Medicine` lines), `DischargeInstructions` (home-care), follow-up
(`DueOn`, and the appointment's local time when booked), addenda (a "Corrections" section). Registered
`IDischargeSummaryContributor`s (HO-PHM-001 adds prescriptions) append sections; until one exists the
summary carries `prescriptionsAvailable: false` and the PDF prints *"Medicines to take home are on the
prescription label."*

`IDischargeSummaryPdfRenderer` → `QuestPdfDischargeSummaryRenderer` (Infrastructure/Documents), A4,
text only (no clinical free text is interpreted as markup), branch letterhead.

A non-finalised visit → `404` on every summary route (staff and customer): a draft is not a summary.

**Message** — on finalisation the handler enqueues **`visit.summary-available`** to the cat's current
owner (Transactional, Normal, `Sms`/`Email`/`InApp`, variables `catName`, `visitDate` (Date),
`summaryLink` (Url, required on every channel)). **No clinical content in the message** — the link
carries the reader to the portal, where access is authenticated and logged. `summaryLink` =
`INotificationLinkBuilder.VisitSummaryLink(visitId)` → `{WebBaseUrl}/portal/visits/{visitId}` (new
method).

### 8.9 Follow-up and consent — US-CLN-18, US-CLN-21 — ADR-131, ADR-132

**Follow-up** (`SetFollowUpCommand`):
- **`Appointment`**: the client books through HO-APT-001's `POST /api/staff/appointments` (the vet
  holds `Appointments.Book`), then sets the follow-up with the new `AppointmentId`. Nothing is
  enqueued here — the booking already sent its confirmation and reminders.
- **`Reminder`**: the handler enqueues **`visit.follow-up-due`** (Transactional, Normal,
  `Sms`/`Email`/`InApp`, variables `catName`, `dueOn` (Date), `bookingLink` = `CatCareLink(catId,
  FollowUp)`), with `SendAfter` = 09:00 branch-local on `DueOn − clinical.followUpReminderLeadDays`
  (default 3; if already past, now), `NotAfter` = end of `DueOn` local, subject
  **`VisitFollowUp`** (`SubjectId = VisitId`), `SubjectStamp = FollowUp.Stamp`.
- **`VisitFollowUpGuard`** — valid while the visit's `FollowUp.Stamp` equals the request's stamp, the
  cat is alive and owned by the recipient, and the cat has **no `Booked` appointment** on or after
  today (the owner already booked). A changed or cleared follow-up fails the guard.
- No reason text is sent — a follow-up reason is clinical information in an SMS.

**Consent** — **`ConsentRecordAggregate`**: `CatId`, `AppointmentId` (the procedure booking — surgeries
are appointments on theatre rooms, HO-APT-001 §4.2), `BranchId`, `Kind` (`Surgery`, `Anaesthesia`,
`Euthanasia`), `ProcedureDescription` (1…500), `Method` (`OnScreenSignature` \| `SignedUpload`),
`Signature?` (`SignatureStrokes` VO, `jsonb`), `FileId?`, `SignedByName` (1…200),
`SignerRelationship` (`Owner` \| `AuthorisedAgent`), `SignedByCustomerId?`, `SignedOn`, `PolicyKey` +
`PolicyVersionNumber` (the exact consent-form text shown), `CapturedBy`, `WitnessStaffId?`, `Status`
(`Recorded` → `Withdrawn`), `WithdrawnOn?`, `WithdrawnBy?`, `WithdrawalReason?`.

| Method | Rules | Event |
|---|---|---|
| `Record(dto)` | Method ⇔ exactly one of signature / file → `ConsentEvidenceMismatch`; appointment belongs to the cat and is not `Cancelled` (handler) → `ConsentAppointmentInvalid`; cat alive → `CatIsDeceased`; policy key is `ConsentForms.KeyFor(kind)` and version is the **current** published version (handler, HO-ADM-001) → `ConsentFormVersionNotCurrent`; file purpose `ConsentForm` | `ConsentRecordedEvent` |
| `Withdraw(reason)` | `Recorded`; reason 1…500 | `ConsentWithdrawnEvent` |

**Consent text** comes from HO-ADM-001 policy documents with keys `consent-surgery`,
`consent-anaesthesia`, `consent-euthanasia` (`RequiresAcceptance = false`), authored by the owner
(OQ-15). `GET /api/staff/consent-forms/{kind}` returns the current version's `LimitedMarkup` body and
number; the consent stores the number, so the exact wording signed is always retrievable.

**`SignatureStrokes`** (VO): `strokes: number[][][]` — 1…200 strokes, each ≥ 2 points `[x, y]`,
coordinates in `[0, 1]` with ≤ 4 decimals, 10…5000 points in total → else `InvalidSignature`. It is
data, not an image: it needs no upload and no malware scan, and the server renders it to SVG from
numbers alone (`GET …/signature.svg`, polylines only).

**`ConsentRules.IsValidFor(consent, catId, appointmentId, kind)`** (Domain, pure): `Recorded`, same
cat, same appointment, same kind. **This is HO-SRG-001's contract** for US-CLN-21 AC2 / US-SRG-03;
`GetConsentStatusQuery(catId, appointmentId, kind)` exposes it to the client.

### 8.10 Stored files — NFR-SEC-5 — **moved to HO-FIL-001** (README C-5)

> **Reconciled 2026-09-23.** This section designed the upload pipeline, `FileRules`, the ports
> (`IFileStore`, `IMalwareScanner`, `IFileUrlSigner`) and the signed-URL serving path — and four other
> areas needed exactly that. The design was right; owning it here was not. It is **extracted verbatim
> into [`HO-FIL-001`](HO-FIL-001.md)** (§8.1–§8.6), which is now section 6 in the delivery order, ahead
> of Shop. This slice **consumes** it and builds no upload path of its own (its §14 forbids a second
> one).
>
> **What this slice still owns:** the three clinical purposes and their rows in `FileRules`
> (`LabResult`, `ClinicalPhoto`, `ConsentForm` — HO-FIL-001 §8.3), which files attach to which visit,
> diagnostic order or consent, and who may see them. **ADR-125 is narrowed** to that: the `StoredFile`
> namespace segment and the aggregate now belong to HO-FIL-001.
>
> **OQ-6 is closed.** The scanner is ClamAV (`clamd` over TCP, `nClam`), fail-closed — **ADR-319**.
> Uploads are no longer "refused outside Development" in principle; they are refused when the scanner
> is unreachable, which is the same rule applied for real.
>
> **Storage is Google Cloud Storage** behind `IFileStore` (**ADR-318**). Nothing in this slice changes
> if that adapter changes.

### 8.11 Clinical-record access log — NFR-SEC-8 — ADR-129

Every endpoint that returns clinical data carries **`[ClinicalRecordAccess(ClinicalRecordKind.X)]`**.
`ClinicalRecordAccessFilter : IAsyncActionFilter` runs **after** authorization and **before** the
action: it dispatches `RecordClinicalRecordAccessCommand(CatId?, Kind, RecordId?, Path)` (cat id from
the route, or resolved by the record id), which writes one `AuditEntryAggregate` with the new category
**`ClinicalRecordAccess`** (`EntityType` = kind, `EntityId` = record or cat, `Action` =
`"ClinicalRecordRead"`, actor = staff or customer, IP). **If the write fails, the action does not run
and the caller gets `500`** — an unlogged read is the thing NFR-SEC-8 forbids. The signed file URL
itself is not re-logged (it was issued by a logged read and lives five minutes).

`ClinicalRecordKind`: `Visit`, `ClinicalHistory`, `DiagnosticOrder`, `DischargeSummary`, `Consent`,
`VisitList`.

### 8.12 Contracts assumed from areas not yet written

**HO-IMP-001 — Imports, Batches and Inventory** (consumed in Phase F; nothing here edits it):

| # | Assumed | Used for |
|---|---|---|
> **Reconciled 2026-09-22 (README C-2).** HO-IMP-001 is now written. It publishes **one** movement
> port, `IStockLedger` (its §8.8, ADR-103), which replaces the `IStockAvailabilityReader`,
> `IStockLocationReader` and `StockLedgerEntryAggregate.Record` this section originally assumed. The
> *shape* assumed here was right — validate, then enlist without saving, so the visit and the
> movement commit together — only the names change.

| # | Assumed | Used for |
|---|---|---|
| IMP-1 | **Exactly one `ClinicDispensary` stock location per branch** (HO-IMP-001 §8.3). This slice does **not** look the location up: it leaves `StockDemand.LocationIds = null`, and the ledger picks every active location of the branch that permits the purpose | §8.6 step 3 |
| IMP-2 | `BatchAggregate`: `SkuId`, `LotNumber`, `ExpiresOn` (`DateOnly`), `Status` (Appendix C) | Snapshot on the line |
| IMP-3 | `IStockLedger.PlanAsync(StockDemand, ct)` → `StockPlan` of `(BatchId, LotNumber, ExpiresOn, LocationId, Quantity)`, FEFO-ordered, excluding expired, quarantined, recalled and reserved quantities. **Plan methods write nothing**, so the batch picker calls it freely | §8.6 steps 3–5 |
| IMP-4 | `IStockLedger.DeductAsync(demand, reference, party, ct)` with `StockReference(ReferenceKind = VisitTreatment, ReferenceId = LineId)` and `StockParty(CustomerId, CatId)` — **both required** for a `ClinicalUse`/`Dispense` purpose (HO-IMP-001 §8.8 rule 2), which is what makes the recall trace complete (C-3). It enlists the ledger rows without saving; this slice's `SaveChangesAsync` commits them with the visit | §8.6 step 7 |
| IMP-5 | Rule 6 of the port: not enough usable, unreserved quantity → `InsufficientStock`; a race after planning is caught by the `CHECK` at **this slice's** commit and mapped to `409 InsufficientStock` | §8.6 step 8 |
| IMP-6 | `IStockLedger.RecordShortfall(StockShortfall)` — enlisted, not an event | US-CLN-15 AC2 |
| IMP-7 | **Reversal** uses `IStockLedger.ReceiveReturnAsync(...)` with `ReferenceKind = VisitTreatmentReversal`. Note HO-IMP-001 §8.8 rule 8 writes a return into the branch's **`Quarantine`** location, not back to the dispensary — see OQ-19 | §8.6 step 9 |

**HO-SHP-001 — Shop catalogue** (Phase F):

| # | Assumed | Used for |
|---|---|---|
| SHP-1 | `ISkuReader.GetAsync(skuId)` → `Name`, `StockUnit`, `IsActive`, **`IsControlledDrug`**, `TaxClassId` | Snapshot; controlled refusal |
| SHP-2 | SKU price at an instant (effective-dated, HO-ADM-001 ADR-049's contract applied to SKUs) → `Money` | ADR-127 snapshot |
| SHP-3 | A dispensary item search (SKUs with stock at a location) — or IMP-3 by SKU plus a SKU name search | `GET /api/staff/dispensary/items` |

**HO-PHM-001, HO-SRG-001, HO-BIL-001, HO-BRD-001, HO-ORD-001** consume what §4.2 lists. If any of
IMP-1…6 or SHP-1…3 differs in the merged code, **stop Phase F and report** (§20.2) — do not adapt
Inventory's code from here.

### 8.13 Ports and domain services — summary

| Name | Kind |
|---|---|
| `VitalRangeCheck`, `ReferenceRangeClassifier`, `ConsentRules`, `VisitAllowedActions` | pure domain services |
| `IMalwareScanner`, `IFileStore`, `IFileUrlSigner` | ports (§8.10) |
| `IDischargeSummaryPdfRenderer` | port, Infrastructure/Documents |
| `IClinicalHistorySectionProvider`, `IDischargeSummaryContributor` | Read extension points |
| `VisitFollowUpGuard` | `INotificationSubjectGuard` |

`VisitAllowedActions.For(visit status, caller roles, appointment status)` → the action list the visit
read returns (`recordVitals`, `editNote`, `recordTreatment`, `orderDiagnostic`, `attachPhoto`,
`setFollowUp`, `finalise`, `addAddendum`), computed from the same `ActionRoles` table and aggregate
rules — the client renders only these.

### 8.14 Exceptions

Reuse `DomainRuleViolationException`. No new exception type besides Infrastructure's
`FileStorageException` (programming/IO error → `500`, logged without paths from user input).

---

## 9. ADRs to record during this build

Append to `.ai/decisions/adr.md`. **This handover owns ADR-117…136**; it uses ADR-117…134.

| ADR | Decision | Why it must be recorded |
|---|---|---|
| **ADR-117** | **Visit roots** live in the `Clinic` context (schema `clinic`, HO-APT-001 ADR-067) under namespace segment **`Visit`** (a listed feature name). **One visit per appointment**, opened by an explicit command while the appointment is `CheckedIn` or `InConsultation`; the visit holds no customer id | `.ai/bounded-contexts.md` lists `Visit` as its own context; HO-APT-001 put appointments in `Clinic`. Recording the split stops a third naming appearing |
| **ADR-118** | **A finalised visit is frozen at three layers** — `EnsureOpen()` in the aggregate, no API path, PostgreSQL triggers on the visit and its child tables; addenda, result sets, attachments and stored files are append-only by trigger; no clinical row is ever deleted | BR-11, D-5, C-3; the same database-authoritative standard as HO-ACC-001's audit triggers |
| **ADR-119** | **Finalising is `Vet`-only** — `Visits.Finalise` maps to `Vet` alone (Administrator excluded, per Appendix D "Clinical notes: Admin R"), and `VisitAggregate.Finalise` re-checks the caller's roles | BR-6, FR-CLN-24, AC-18; an "Administrator can do everything" assumption would violate the SRS |
| **ADR-120** | **Vital-sign ranges are two-tier and server-authoritative**: outside *absolute* is refused; outside *plausible* is refused until the request confirms those signs, and the confirmation is stored | US-CLN-11 AC2 "warned but may confirm"; a client-only warning is not evidence |
| **ADR-121** | **Vitals with a weight create the cat's weight record in the same commit**, sharing the vitals entry's id — a recorded rule-1 exception using the F23 companion commit | Weight history must never disagree with the vitals that produced it; an event handler would fail after commit |
| **ADR-122** | **Auto-save is a per-author working copy** (`ConsultationDraftAggregate`), excluded from audit capture, returned only to its author when newer than the saved note; the note on the visit changes only on explicit save | US-CLN-12 AC2; auto-saving into the record would flood the audit trail with keystroke-level rows |
| **ADR-123** | **Coded diagnoses** come from an in-house catalogue (`DiagnosisCodeAggregate`) with optional `ExternalSystem`/`ExternalCode` for a later mapping (VeNom or SNOMED, OQ-2); problem code and name are snapshotted; one system code `SYS-NAD` ("Routine visit — no abnormality detected") is seeded; finalising needs ≥ 1 problem and non-blank Assessment and Plan | FR-CLN-13 "coded"; `.ai/flows/clinic-visit-flow.md` "cannot be finalised without at least one diagnosis" — the system code keeps well visits finalisable |
| **ADR-124** | **Diagnostics are their own root**; results are append-only result sets with superseding links; flags come from a pure classifier against the range stored on the value; results and files may arrive after finalisation | Lab reports arrive days later; a correction must not overwrite what a vet acted on |
| **ADR-125** | **Stored-file facility** (`files` schema, `StoredFile` segment, shared with later areas): content-sniffed type validation, size per purpose, **fail-closed** malware scan port, local store outside the web root, HMAC-signed 5-minute URLs, hardened response headers. If HO-IMP-001 ships an equivalent first, this slice consumes it (§20.3) | NFR-SEC-5 / US-XCT-07 applied for the first time a slice truly stores files; no scanner chosen yet |
| **ADR-126** | **Treatments move stock through Inventory in the same commit** (rule-1 exception, companion commit); FEFO by default, override with reason; **expired is refused with no override**; shortfall recorded and published; mistakes reversed by a compensating entry; **controlled-drug SKUs are refused** until the register exists (HO-SRG/HO-PHM) | BR-3, BR-4, FR-IMP-15, C-2; an after-commit handler could record a treatment whose stock never moved |
| **ADR-127** | **Charges are snapshotted on treatment lines** (service or SKU price at recording, tax class); Billing receives `VisitFinalisedEvent` and reads `GetVisitChargeLinesQuery`; Clinic never writes `billing.*` | HO-ADM-001 ADR-049's contract; `.ai/flows/clinic-visit-flow.md` cross-module rule |
| **ADR-128** | **Clinical history is composed from section providers**; unbuilt sections report `available: false` with their owner; server budget 600 ms / 300 ms p95 on a 10-year fixture | Later areas add panels without editing this slice; NFR-PRF-4 must be measured, not assumed |
| **ADR-129** | **Every clinical-record read is logged before it runs**, by a filter dispatching a command, new `AuditCategory.ClinicalRecordAccess`; a failed log fails the request | NFR-SEC-8 "every access … shall be logged"; a query never writes, so logging cannot live in the query |
| **ADR-130** | **The discharge summary is derived, not stored**; finalised visits only; PDF via QuestPDF; the owner gets a link-only message | A stored copy could disagree with the immutable record and its addenda |
| **ADR-131** | **Follow-up** is a link to an appointment booked through HO-APT-001, or a reminder enqueued with `SendAfter`, subject `VisitFollowUp` and a stamp guard that also stands down once the cat has a booked appointment | US-CLN-18; a reminder for a follow-up that changed or was already booked must never send |
| **ADR-132** | **Consent** is its own root, linked to cat, appointment and kind, recording the exact consent-form policy version; on-screen signatures are **stroke data rendered to SVG**, not uploaded images; `ConsentRules.IsValidFor` is Surgery's contract | NFR-LEG-4, US-CLN-21; stroke data needs no scanner, so signing works where uploads are refused |
| **ADR-133** | **Amendment to HO-APT-001**: `CompleteAppointment` is refused while the appointment's visit is `Open` (`VisitNotFinalised`); staff `Cancel` of a `CheckedIn` appointment is refused once a visit exists (`VisitInProgress`). Finalising does not complete the appointment | HO-APT-001 §4.2 reserved this invariant for this slice; auto-completing from an event handler would fail after commit |
| **ADR-134** | **Note templates** are one active template per service, versioned, delivered in the visit payload for the client to pre-fill; the applied id and version are recorded on the note | US-CLN-20 "remains fully editable"; server-side copying would blur what the vet wrote |

### 9.1 Open questions

| # | Question | Owner | Default in this build |
|---|---|---|---|
| OQ-1 | `.ai/bounded-contexts.md` / namespace conventions: `Visit` inside the `Clinic` schema; the new `StoredFile` segment | Architect | ADR-117, ADR-125 record them |
| OQ-2 | Coded diagnosis vocabulary — VeNom (licence registration), SNOMED CT vet extension, or in-house? | Owner + clinical lead | In-house catalogue with an external-code column (ADR-123) |
| OQ-3 | Vital-sign plausible/absolute ranges; body condition 9-point vs 5-point; pain scale (0–10 grimace vs Glasgow 0–20); lab analyte catalogue content | Clinical lead | §8.3 values; 9-point; 0–10; no analyte seed |
| OQ-4 | Appendix D gives nurses **W (vitals)** only. Should nurses also record treatments, attach photos or enter lab values from a report? | Owner + clinical lead | **No** — Appendix D as written (§12.3) |
| OQ-5 | May reception print a discharge summary at checkout? NFR-SEC-8 limits clinical records to clinical roles | Owner | **No** — Nurse, Vet, Administrator |
| ~~OQ-6~~ | **Closed 2026-09-23 — ADR-319.** ClamAV (`clamd` over TCP, `nClam` client), scanning **before** the object reaches the bucket; `Unavailable` fails closed. Owned by HO-FIL-001 §8.5 | — | — |
| OQ-7 | File storage — local disk vs S3-compatible object storage; encryption at rest (NFR-SEC-3) | Owner / ops | `LocalFileStore` on an encrypted volume |
| OQ-8 | Must every completed appointment have a finalised visit (e.g. nurse-only vaccination appointments)? | Owner | **No** — only an appointment *with* an open visit is blocked (ADR-133) |
| OQ-9 | Visits not tied to an appointment (boarding referral, emergency before check-in)? | Owner | Appointment only; HO-BRD-001 adds its origin |
| OQ-10 | A visit opened for the wrong cat — discard, or finalise with an addendum? | Owner + clinical lead | No discard; finalise with `SYS-NAD` and an addendum (weight records already written stay) |
| OQ-11 | HO-IMP-001 confirmation of IMP-1…6 (one dispensary per branch, quantity type, concurrency guarantee, reason names) | HO-IMP-001 author | As §8.12 |
| OQ-12 | Source of the controlled-drug flag (HO-SHP-001 SKU vs HO-PHM-001 formulary) | HO-SHP / HO-PHM authors | SKU flag (SHP-1); refused here (ADR-126) |
| OQ-13 | SKU price source for consumables billed on a visit | HO-SHP / HO-BIL authors | SHP-2 snapshot at recording |
| OQ-14 | Should owners see lab results only after a vet reviews them, rather than on finalisation? | Owner + clinical lead | Visible for finalised visits as soon as recorded (only vets record them — OQ-4) |
| OQ-15 | Wording of the three consent forms; SRS Q-1 jurisdiction | Owner / legal | Authored as HO-ADM-001 policy documents; consent refused until published |
| OQ-16 | Follow-up reminder lead days and send hour | Owner | 3 days, 09:00 branch-local |
| OQ-17 | Amended rows on HO-APT-001, HO-ADM-001, HO-NTF-001, HO-ACC-001 and HO-CAT-001 (for the future PDF) — this handover was written without editing them | Coordinator | Append the rows listed in §13's note when this handover is approved |
| OQ-18 | Consent for procedures without an appointment (euthanasia decided mid-visit) | Owner | Linked to the current appointment; no appointment → not recordable |
| OQ-19 | **Reversing a treatment returns stock to `Quarantine`, not the dispensary** (HO-IMP-001 §8.8 rule 8). For a mis-keyed clinical deduction the units never left the dispensary, so quarantining them takes usable stock out of circulation until someone restocks it | HO-IMP-001 author + Inventory lead | Accept the quarantine round-trip for now — it is auditable and safe. If it proves noisy in practice, HO-IMP-001 adds a `ReturnToSource` reason rather than this slice writing ledger rows itself |

---

## 10. Enumerations, constants and violation codes

### 10.1 Enumerations — `Shared/Common/Enums/`, derived from `Enumeration`, persisted by `Value`

| Type | Members |
|---|---|
| `VisitStatus` | `Open`, `Finalised` |
| `VisitOrigin` | `Appointment` |
| `VitalSign` | `Weight`, `Temperature`, `HeartRate`, `RespiratoryRate`, `BodyConditionScore`, `PainScore` |
| `ProblemCertainty` | `Provisional`, `Confirmed` |
| `DiagnosticKind` | `BloodWork`, `Urinalysis`, `FelvFivTest`, `Imaging`, `Other` |
| `DiagnosticOrderStatus` | `Pending`, `Resulted`, `Cancelled` |
| `ResultValueKind` | `Numeric`, `Qualitative` |
| `ResultFlag` | `Normal`, `Low`, `High`, `Abnormal`, `NotAssessed` |
| `TreatmentKind` | `Procedure`, `Medicine`, `Consumable` |
| `TreatmentLineStatus` | `Recorded`, `Reversed` |
| `FollowUpKind` | `Appointment`, `Reminder` |
| `ConsentKind` | `Surgery`, `Anaesthesia`, `Euthanasia` |
| `ConsentMethod` | `OnScreenSignature`, `SignedUpload` |
| `ConsentStatus` | `Recorded`, `Withdrawn` |
| `SignerRelationship` | `Owner`, `AuthorisedAgent` |
| `FilePurpose` | `LabResult`, `ClinicalPhoto`, `ConsentForm` |
| `ClinicalRecordKind` | §8.11 |

Appended (append only): HO-ACC-001's **`AuditCategory`** gains `ClinicalRecordAccess`; HO-NTF-001's
**`NotificationSubjectKind`** gains `VisitFollowUp`.

### 10.2 Settings — appended to HO-ADM-001's `SettingDefinitions`

| Key | Group | Type | Default | Bounds | Branch override |
|---|---|---|---|---|---|
| `clinical.followUpReminderLeadDays` | Clinical | Integer | 3 | 0–30 | ❌ |

### 10.3 Other constants

`VitalSignRules` (§8.3) · `ClinicalNoteRules` (lengths, `MaxProblems = 20`, `MaxVitalsEntries = 20`,
`MaxTreatmentLines = 100`, `MaxPhotos = 20`, `MaxAddendumLength = 4000`) · `DiagnosticRules` ·
`FileRules` (§8.10) · `SignatureRules` · `ConsentForms` (`KeyFor(kind)`) · `ClinicalHistorySections`
(§8.7) · `DiagnosisCodeSeed.RoutineNoAbnormality` (fixed id, code `SYS-NAD`) · `ClinicalMessages` ·
the two template definitions (§8.8, §8.9) appended to `NotificationTemplateCatalogue`.

### 10.4 Violation codes — append to `BusinessViolationCodes`

`VisitNotFound` · `VisitAlreadyExists` *(never returned — an existing visit is returned as success)* ·
`AppointmentNotReadyForVisit` · `VisitBelongsToAnotherBranch` · `VisitFinalised` · `VitalsRequired` ·
`VitalSignOutOfRange` · `VitalsOutsidePlausibleRange` · `DiagnosisCodeInactive` · `DiagnosisCodeNotFound` ·
`DuplicateProblem` · `ProblemRequired` · `AssessmentAndPlanRequired` · `OnlyVeterinarianMayFinalise` ·
`AddendumRequiresFinalisedVisit` · `DiagnosisCodeAlreadyExists` · `SystemDiagnosisCodeReadOnly` ·
`LabAnalyteAlreadyExists` · `InvalidReferenceRange` · `NoteTemplateAlreadyExistsForService` ·
`DiagnosticOrderNotFound` · `DiagnosticOrderHasResults` · `DiagnosticOrderCancelled` ·
`InvalidDiagnosticResult` · `ResultSetNotFound` · `TreatmentLineNotFound` · `TreatmentAlreadyReversed` ·
`SkuNotFound` · `ControlledDrugNotSupportedHere` · `DispensaryNotConfigured` · `BatchExpired` ·
`BatchNotIssuable` · `BatchOverrideReasonRequired` · `InsufficientStock` · `FollowUpDateInPast` ·
`FollowUpAppointmentInvalid` · `ConsentEvidenceMismatch` · `ConsentAppointmentInvalid` ·
`ConsentFormVersionNotCurrent` · `ConsentNotFound` · `ConsentAlreadyWithdrawn` · `InvalidSignature` ·
`FileNotFound` · `FilePurposeMismatch` · `FileAlreadyAttached` · `FileTooLarge` · `FileTypeNotAllowed` ·
`FileContentMismatch` · `FileRejected` · `FileScanUnavailable` · `VisitNotFinalised` ·
`VisitInProgress`.

Messages state what to do next (NFR-USA-4), e.g. `BatchExpired`: *"Batch {lot} expired on {date} and
cannot be used. Choose another batch."*; `InsufficientStock`: *"Only {n} {unit} of {item} are in the
dispensary. Record anyway to note the shortfall for reordering, or change the quantity."*

---

## 11. Specified changes — write side

### 11.1 Commands — `Application/Commands/Visit/` (and `Commands/StoredFile/`)

Namespace `CatEcommerce.Application.Commands.Visit`. Data only; ids minted in the controller;
`ValidateCommand = true` everywhere (F3).

| # | Command | Sent by | Phase |
|---|---|---|---|
| C1 | `CreateDiagnosisCodeCommand` (`DiagnosisCodeId`, `Code`, `Name`, `Category?`, `ExternalSystem?`, `ExternalCode?`) · `UpdateDiagnosisCodeCommand` · `SetDiagnosisCodeActiveCommand` | Vet, Admin | A |
| C2 | `CreateLabAnalyteCommand` · `UpdateLabAnalyteCommand` · `SetLabAnalyteActiveCommand` | Vet, Admin | A |
| C3 | `CreateNoteTemplateCommand` (`NoteTemplateId`, `ServiceId`, `Name`, five texts) · `UpdateNoteTemplateCommand` · `SetNoteTemplateActiveCommand` | Vet, Admin | A |
| C4 | `RecordClinicalRecordAccessCommand` (`AuditEntryId`, `CatId?`, `Kind`, `RecordId?`, `Path`) | Filter | A |
| C5 | `OpenVisitCommand` (`VisitId`, `AppointmentId`) | Nurse, Vet | B |
| C6 | `RecordVitalsCommand` (`VisitId`, `VitalsEntryId`, `MeasuredOn`, `WeightKg?`, `TemperatureC?`, `HeartRateBpm?`, `RespiratoryRatePerMin?`, `BodyConditionScore?`, `PainScore?`, `ConfirmedOutOfRange[]`, `Note?`) | Nurse, Vet | B |
| C7 | `SaveConsultationDraftCommand` (`VisitId`, `DraftId`, note texts, `ProblemCodeIds[]`) | Vet | C |
| C8 | `SaveConsultationNoteCommand` (`VisitId`, `ExpectedVersion`, note texts, `Problems[{ProblemId, DiagnosisCodeId, Certainty, Note?}]`, `AppliedTemplateId?`, `AppliedTemplateVersion?`) | Vet | C |
| C9 | `FinaliseVisitCommand` (`VisitId`, `ExpectedVersion`) | Vet | C |
| C10 | `AddVisitAddendumCommand` (`AddendumId`, `VisitId`, `Text`, `Reason?`) | Vet | C |
| C11 | `RegisterStoredFileCommand` (`FileId`, `Purpose`, `ContentType`, `SizeInBytes`, `Sha256`, `OriginalFileName`, `StorageKey`, `ScannerName`) | Upload endpoint | D |
| C12 | `OrderDiagnosticCommand` (`DiagnosticOrderId`, `VisitId`, `Kind`, `Description?`, `AnalyteIds[]`) | Vet | D |
| C13 | `RecordDiagnosticResultsCommand` (`DiagnosticOrderId`, `ResultSetId`, `ReportedOn`, `Values[]`, `Note?`, `SupersedesResultSetId?`) | Vet | D |
| C14 | `AttachDiagnosticFileCommand` (`DiagnosticOrderId`, `AttachmentId`, `FileId`, `ResultDate`, `Note?`) · `CancelDiagnosticOrderCommand` (`Reason`) | Vet | D |
| C15 | `AttachClinicalPhotoCommand` (`VisitId`, `PhotoId`, `FileId`, `TakenOn`, `Note?`) | Vet | D |
| C16 | `RecordTreatmentCommand` (`VisitId`, `LineId`, `Kind`, `Description`, `ServiceId?`, `SkuId?`, `BatchId?`, `Quantity`, `Dose?`, `Route?`, `IsBillable`, `AcceptShortfall`, `BatchOverrideReason?`) | Vet | F |
| C17 | `ReverseTreatmentLineCommand` (`VisitId`, `LineId`, `Reason`) | Vet | F |
| C18 | `SetFollowUpCommand` (`VisitId`, `Kind`, `DueOn`, `AppointmentId?`) · `ClearFollowUpCommand` | Vet | G |
| C19 | `RecordConsentCommand` (`ConsentId`, `CatId`, `AppointmentId`, `Kind`, `ProcedureDescription`, `Method`, `Signature?`, `FileId?`, `SignedByName`, `SignerRelationship`, `SignedByCustomerId?`, `PolicyVersionNumber`, `WitnessStaffId?`) — `SignedOn` = server now | Reception, Nurse, Vet, Admin | H |
| C20 | `WithdrawConsentCommand` (`ConsentId`, `Reason`) | Reception, Nurse, Vet, Admin | H |

### 11.2 Validators — `CommandHandlers/Validators/` (flat), registered as `IValidator<T>`

Shape only: ids non-empty, text lengths, enum membership, numbers finite and to the stated precision,
`Quantity > 0`, `DueOn`/`ReportedOn`/`TakenOn` present, signature array shape (not its bounds, which are
the VO's), `Values` non-empty. Ranges, finalisation, stock and policy-version rules are aggregate or
handler rules.

### 11.3 Mappers and DTOs

`VisitMapper`, `DiagnosticMapper`, `ConsentMapper`, `ClinicalCatalogueMapper` (static
`ToDomainDto()` extensions). DTOs in `Domain/Models/`: `VisitOpeningDto`, `VitalsDto`, `SoapNoteDto`,
`VisitFinalisationDto` (incl. `FinaliserRoles`), `TreatmentDto`, `FollowUpDto`, `DiagnosticOrderDto`,
`DiagnosticResultSetDto`, `ConsentRecordingDto`, `StoredFileDto`, catalogue DTOs.

### 11.4 Command handlers — `CommandHandlers/Visit/` (and `CommandHandlers/StoredFile/`)

**Companion commit** (C6 with weight, C16, C17 with stock) — implemented as a documented sequence in
each handler, not a service (ADR-015): mutate the visit → `companionRepository.SaveAsync(companion,
InMemoryPublishOnly)` → `visitRepository.UpdateAsync(visit, InMemoryPublishOnly)`. A comment in each
handler cites F23 and ADR-121/126.

**Branch rule** for every visit write: `IActiveBranchAccessor` branch must equal `visit.BranchId` →
else `VisitBelongsToAnotherBranch` (`403`).

| Handler | Specifics |
|---|---|
| **C4 Access** | Builds one `AuditEntryAggregate` (`ClinicalRecordAccess`, IP from `IClientIpAccessor`), `SaveAsync(…, DoNotPublish)` |
| **C5 Open** | Load the appointment (`404` if absent); **existing visit for the appointment → success returning its id** (idempotent — two nurses clicking at once get the same visit; the unique index turns a race into the same answer, §12.5); branch from the appointment and must equal the active branch; cat alive; `Open`; save |
| **C6 Vitals** | §8.3; when weight present, companion-commit a `CatWeightRecordAggregate` (id = entry id) |
| **C7 Draft** | Load visit (must be `Open`); load-or-create the draft for `(visit, caller)`; `Overwrite`; save. **No `ExpectedVersion`** — last write wins within one author's copy |
| **C8 Note** | Load active diagnosis codes for the ids (`DiagnosisCodeNotFound` / inactive); `SaveNote` with snapshots; `UpdateAsync(visit, ExpectedVersion)` → stale `409` |
| **C9 Finalise** | §8.4; enqueue `visit.summary-available` to the cat's **current owner** (read the cat) before saving |
| **C10 Addendum** | Load the visit (read); `VisitAddendumAggregate.Add`; save |
| **C11 Register file** | `StoredFileAggregate.Create`; save |
| **C12–C14 Diagnostics** | C12 requires the visit `Open` and same branch; C13/C14 load analytes for defaults; C14 checks the file's purpose (`FilePurposeMismatch`) and that it is not already referenced by any attachment, photo or consent (`FileAlreadyAttached` — a query across the three, backed by unique indexes, §12.1) |
| **C15 Photo** | File purpose `ClinicalPhoto`; not already attached; `AttachPhoto` |
| **C16 / C17 Treatment** | §8.6 |
| **C18 Follow-up** | Appointment kind: load the appointment (cat match, `Booked`, future); reminder kind: compute `SendAfter` in the branch zone through HO-ADM-001's calendar helpers, enqueue (§8.9) |
| **C19 Consent** | Load appointment and cat; read the current policy version for `ConsentForms.KeyFor(kind)`; `SignedUpload` → file purpose `ConsentForm`, not already attached; `Record`; save |
| **C20 Withdraw** | `Withdraw`; save |

**Amendment to HO-APT-001 handlers (ADR-133)** — lines only:
- `CompleteAppointmentCommandHandler`: before `Complete`, query `VisitAggregate` by `AppointmentId`;
  present and `Open` → `VisitNotFinalised` (*"Finalise the consultation before completing the
  appointment."*).
- `CancelAppointmentCommandHandler`: staff cancel of a `CheckedIn` appointment with a visit →
  `VisitInProgress`.

### 11.5 Domain events — `Events/Visit/` (and `Events/StoredFile/`)

Ids, instants, enums, numbers and booleans only — **no clinical text, names or file names**.

`VisitOpenedEvent` · `VisitVitalsRecordedEvent` · `ConsultationNoteSavedEvent` · **`VisitFinalisedEvent`**
(`VisitId`, `AppointmentId`, `CatId`, `BranchId`, `FinalisedBy`, `FinalisedOn` — **HO-BIL-001
contract**) · `VisitAddendumAddedEvent` · `ClinicalPhotoAttachedEvent` · `FollowUpSetEvent` ·
`FollowUpClearedEvent` · `DiagnosticOrderedEvent` · `DiagnosticResultsRecordedEvent` (+ `HasAbnormal`)
· `DiagnosticFileAttachedEvent` · `DiagnosticOrderCancelledEvent` · `TreatmentRecordedEvent` ·
`TreatmentReversedEvent` · **`ClinicalStockShortfallRecordedEvent`** (**HO-IMP-001 contract**) ·
`ConsentRecordedEvent` · `ConsentWithdrawnEvent` · catalogue events · `StoredFileRegisteredEvent`.

### 11.6 Domain event handlers — `DomainEventHandlers/Visit/`

| Handler | Handles | Does |
|---|---|---|
| `AuditVisitFinalisedHandler` | `VisitFinalisedEvent` | `BusinessEvent` audit row (who finalised, when) |
| `AuditVisitAddendumHandler` | `VisitAddendumAddedEvent` | `BusinessEvent` row |
| `AuditTreatmentReversedHandler` | `TreatmentReversedEvent` | `BusinessEvent` row (stock was returned) |
| `AuditConsentHandler` | `ConsentRecordedEvent`, `ConsentWithdrawnEvent` | `BusinessEvent` rows |

All `SaveAsync(entry, DoNotPublish)`, **never enqueue**, never mutate a clinical aggregate. Data changes
are audited by HO-ACC-001's interceptor. There is **no** `CustomerMergedEvent` handler: no clinical
row stores a customer id except `ConsentRecord.SignedByCustomerId`, which is a frozen fact read through
HO-ACC-001's merge chain.

---

## 12. Specified changes — Infrastructure, read side, Api, host

### 12.1 Persistence

`DbSet`s for the ten roots; one configuration per root (`clinic` / `files`); the HO-ACC-001 §12.1
conventions and ADR-021 (`ValueGeneratedNever` on every root **and nested owned** id; `Tags`,
`Language`, `ServiceId` optional; `xmin`; `Ignore(Events)`); enumerations by `Value`; `Money` as
`amount numeric(12,2)` + `currency_code char(3)`; measurements `numeric` at §8.3 precision; `jsonb` for
`consultation_draft.problem_code_ids`, `consent_record.signature`; `int[]` for
`visit_vitals.confirmed_out_of_range`.

| Index / constraint | Kind |
|---|---|
| `visit (appointment_id)` | **UNIQUE** `ix_visit_appointment` |
| `visit (cat_id, opened_on DESC)` | history, timeline |
| `visit (branch_id, status, opened_on)` | open visits list |
| `visit_problem (diagnosis_code_id)`, `(visit_id)` | diagnoses section |
| `visit_treatment (batch_id)`, `(sku_id)` | recall tracing (HO-IMP), charge lines |
| `visit_photo (file_id)` **UNIQUE**, `diagnostic_attachment (file_id)` **UNIQUE**, `consent_record (file_id)` **UNIQUE filtered not null** | a file attached once |
| `visit_addendum (visit_id, added_on)` | |
| `consultation_draft (visit_id, author_staff_id)` | **UNIQUE** |
| `diagnostic_order (cat_id, ordered_on DESC)`, `(branch_id, status)`, `(visit_id)` | history, pending list |
| `consent_record (cat_id, appointment_id, kind, status)` | consent status |
| `diagnosis_code (code)` **UNIQUE**; `lab_analyte (code)` **UNIQUE** | catalogues |
| `note_template (service_id)` **UNIQUE filtered `WHERE is_active`** | one active per service |
| `files.stored_file (sha256)` | duplicate investigation |
| `cat_weight_record (cat_id, recorded_on)` | exists (HO-CAT-001) |
| `audit.audit_entry (entity_type, entity_id)` | exists (HO-ACC-001) |

**Migrations**, each read before commit:
- `AddClinicalCatalogues` (A) — codes, analytes, templates; seed `SYS-NAD`.
- `AddVisits` (B) — visit, vitals and the other owned tables.
- `AddConsultationNotes` (C) — addendum, draft.
- `AddClinicalRecordImmutability` (C) — §8.4 triggers (raw SQL, status value from the enumeration);
  `Down` drops them.
- `AddStoredFiles` (D) — `files` schema, stored_file, its trigger.
- `AddDiagnostics` (D) — order and children, their triggers.
- `AddConsents` (H) — consent_record and its trigger.

Phases E, F and G add no migration (F writes to Inventory's table through its aggregate).

**Audit exclusion**: `ConsultationDraftAggregate` appended to `AuditCaptureExclusions` (HO-NTF-001
ADR-066's list) — one line.

### 12.2 Infrastructure services

| Class | Notes |
|---|---|
| `Files/LocalFileStore`, `Files/HmacFileUrlSigner`, `Files/{DevelopmentNoOpScanner,EicarTestScanner,UnconfiguredMalwareScanner}`, `Files/FileContentSniffer` | §8.10. `FileStorageOptions` (`RootPath`, `TempPath`, `UrlSigningKey`, `UrlLifetimeSeconds` = 300, `Scanner`), `ValidateOnStart` |
| `Documents/QuestPdfDischargeSummaryRenderer` | §8.8 |
| `Notifications/Guards/VisitFollowUpGuard` | §8.9 |
| `Notifications/NotificationLinkBuilder` | `VisitSummaryLink` added only |

### 12.3 Authorization

Appendix D "Clinical notes": Customer **R (own, finalised)**, Reception **—**, Nurse **W (vitals)**, Vet
**F**, Admin **R**. Appended to the one `ActionRoles` table:

| Action | Staff roles | Customer (own) | Endpoints |
|---|---|---|---|
| `Visits.View` | Nurse, Vet, Administrator | — | visit detail, open-visit list, history, diagnostics reads, vital-sign rules |
| `Visits.Open` | Nurse, Vet | — | open |
| `Visits.RecordVitals` | Nurse, Vet | — | vitals |
| `Visits.WriteNote` | Vet | — | draft, note, photos, follow-up, diagnostics order/results/attach/cancel, treatments, dispensary reads |
| `Visits.Finalise` | **Vet** | — | finalise — **no Administrator** (ADR-119) |
| `Visits.AddAddendum` | Vet | — | addenda |
| `Visits.PrintSummary` | Nurse, Vet, Administrator | — | staff discharge summary JSON/PDF (OQ-5) |
| `Visits.ViewOwn` | — | ✅ (`OwnerScope.Cat` / handler ownership) | portal visits, summary, PDF, lab results |
| `ClinicalCatalogue.View` | Nurse, Vet, Administrator | — | code, analyte, template reads |
| `ClinicalCatalogue.Manage` | Vet, Administrator | — | catalogue writes |
| `Consents.View` · `Consents.Record` | Reception, Nurse, Vet, Administrator | — | consent reads / record, withdraw, consent forms |
| `Files.Upload` | Reception, Vet | — | upload (Reception for signed consent forms; Vet for lab files and photos) |

`Visits.ViewOwn` is added to HO-ADM-001's `CustomerActions`. Customer visit routes by `visitId` resolve
the visit's cat and its **current** owner in the handler: not the caller, or not finalised → **`404`**.
The file-content route is `[PublicEndpoint]` (§8.10). Every clinical read carries
`[ClinicalRecordAccess]` (§8.11). `EndpointAccessConventionTests` (HO-ADM-001) must stay green.

### 12.4 Api — the contract HO-EMR-002 consumes

JSON camelCase; instants ISO-8601 with offset; local dates `yyyy-MM-dd`; money as HO-ADM-001 §12.4;
**measurements as `{ "value": "4.20", "unit": "kg" }`** (value a string at the stated precision —
UI-8). Every visit view carries `branchTimeZoneId`. Text fields are plain text.

**Staff — visits**

| Method | Route | Access | Command / query → success |
|---|---|---|---|
| `POST` | `/api/staff/visits` `{ appointmentId }` | `Visits.Open` | C5 → `201 { visitId }`, or `200 { visitId }` when it already existed |
| `GET` | `/api/staff/appointments/{appointmentId}/visit` | `Visits.View` | `{ visitId? , status? }` |
| `GET` | `/api/staff/visits?status=Open&date` | `Visits.View` · access-logged `VisitList` | visits at the active branch: id, cat (name, `isHighRisk`), service, opened, status, `hasVitals`, `hasNote` |
| `GET` | `/api/staff/visits/{visitId}` | `Visits.View` · logged `Visit` | `{ id, version, status, catId, appointment { id, status, startsOn, performerName }, service, openedOn, openedBy, branchTimeZoneId, allowedActions[], note { subjective, objective, assessment, plan, dischargeInstructions, savedOn?, savedBy? }, problems[], vitals[] (each sign as a measurement + `confirmedOutOfRange[]`), treatments[], photos[] (with signed `url` + `thumbnailUrl`), followUp?, addenda[], diagnostics[] (summary), template?, draft? (the caller's, §8.4), finalisedOn?, finalisedBy? }` |
| `GET` | `/api/staff/clinical/vital-sign-rules` | `Visits.View` | §8.3 table |
| `POST` | `/api/staff/visits/{visitId}/vitals` | `Visits.RecordVitals` | C6 → `201 { vitalsEntryId }`; `400 VitalsOutsidePlausibleRange` + `outOfRange[]`; `400 VitalSignOutOfRange` |
| `PUT` | `/api/staff/visits/{visitId}/draft` | `Visits.WriteNote` | C7 → `204` |
| `PUT` | `/api/staff/visits/{visitId}/note` | `Visits.WriteNote` | C8 → `200 { version, savedOn }`; `409` stale |
| `POST` | `/api/staff/visits/{visitId}/finalise` `{ expectedVersion }` | `Visits.Finalise` | C9 → `204`; `400 ProblemRequired` / `AssessmentAndPlanRequired` / `VisitFinalised`; `403` non-vet (audited) |
| `POST` | `/api/staff/visits/{visitId}/addenda` | `Visits.AddAddendum` | C10 → `201 { addendumId }` |
| `POST` | `/api/staff/visits/{visitId}/photos` | `Visits.WriteNote` | C15 → `201` |
| `PUT` / `DELETE` | `/api/staff/visits/{visitId}/follow-up` | `Visits.WriteNote` | C18 → `204` |
| `POST` | `/api/staff/visits/{visitId}/treatments` | `Visits.WriteNote` | C16 (optional body `lineId` = the client's idempotency key; send a fresh one per NEW treatment, not per click) → `201 { lineId, deductedQuantity, shortfallQuantity, unit, batch { id, lotNumber, expiresOn }?, lines[ { lineId, quantity, deductedQuantity, shortfallQuantity, batch? } ] }` (quantities are strings; one `lines[]` entry per batch used -- the top-level `lineId` and `batch` are the first); a retry with a `lineId` already recorded on this visit → `200` with the same body and nothing deducted again (the retried body is ignored); a `lineId` used on another visit → `400 TreatmentLineAlreadyRecorded`; `400 InsufficientStock { availableQuantity, unit }` (the whole dispensary's); `400 BatchExpired`; `400 BatchOverrideReasonRequired`; `400 ControlledDrugNotSupportedHere`; `409 InsufficientStock` |
| `POST` | `/api/staff/visits/{visitId}/treatments/{lineId}/reverse` | `Visits.WriteNote` | C17 → `204` |
| `GET` | `/api/staff/dispensary/items?search&pageNumber&pageSize` | `Visits.WriteNote` | paged (`pageSize` default 30, max 100): `{ items[ { skuId, name, stockUnit, availableQuantity, isControlledDrug } ], pageNumber, pageSize, totalCount }`; no `search` = SKUs with stock at the active branch's dispensary, `search` = active SKUs matching it (first 200), in stock or not |
| `GET` | `/api/staff/dispensary/items/{skuId}/batches` | `Visits.WriteNote` | issuable batches FEFO: `batchId`, `lotNumber`, `expiresOn`, `availableQuantity`, `isFefoDefault` |
| `GET` | `/api/staff/visits/{visitId}/discharge-summary` · `.pdf` | `Visits.PrintSummary` · logged `DischargeSummary` | §8.8 JSON / `application/pdf`; `404` if not finalised |

**Staff — diagnostics**

| Method | Route | Access | → success |
|---|---|---|---|
| `POST` | `/api/staff/visits/{visitId}/diagnostics` | `Visits.WriteNote` | C12 → `201 { diagnosticOrderId }` |
| `GET` | `/api/staff/diagnostics/{orderId}` | `Visits.View` · logged | order, requested analytes, result sets (values with `flag`, range, unit; `supersedesResultSetId`), attachments (signed URLs) |
| `GET` | `/api/staff/diagnostics?status=Pending` | `Visits.View` · logged `VisitList` | pending orders at the active branch |
| `POST` | `/api/staff/diagnostics/{orderId}/results` | `Visits.WriteNote` | C13 → `201 { resultSetId, values[] with flags }` |
| `POST` | `/api/staff/diagnostics/{orderId}/attachments` | `Visits.WriteNote` | C14 → `201` |
| `POST` | `/api/staff/diagnostics/{orderId}/cancel` | `Visits.WriteNote` | C14 cancel → `204` |

**Staff — history, consent, files, catalogues**

| Method | Route | Access | → success |
|---|---|---|---|
| `GET` | `/api/staff/cats/{catId}/clinical-history` | `Visits.View` · logged `ClinicalHistory` | §8.7 summary |
| `GET` | `/api/staff/cats/{catId}/clinical-history/{section}?pageNumber&pageSize` | `Visits.View` · logged | paged items, or `{ available: false, owner }` |
| `GET` | `/api/staff/consent-forms/{kind}` | `Consents.View` | `{ policyKey, versionNumber, title, body (LimitedMarkup) }`; `404` if unpublished |
| `POST` | `/api/staff/consents` | `Consents.Record` | C19 → `201 { consentId }` |
| `GET` | `/api/staff/consents/{consentId}` · `/api/staff/cats/{catId}/consents` | `Consents.View` · logged `Consent` | detail (with `signatureUrl` to the SVG or the file's signed URL) / list |
| `GET` | `/api/staff/consents/{consentId}/signature.svg` | `Consents.View` · logged | `image/svg+xml`, polylines only |
| `GET` | `/api/staff/consents/status?catId&appointmentId&kind` | `Consents.View` | `{ hasValidConsent, consentId?, signedOn? }` — **HO-SRG-001 contract** |
| `POST` | `/api/staff/consents/{consentId}/withdraw` | `Consents.Record` | C20 → `204` |
| `POST` | `/api/staff/files?purpose=` (multipart) | `Files.Upload` | `201 { fileId, contentType, sizeInBytes }`; `400 FileTooLarge` / `FileTypeNotAllowed` / `FileContentMismatch` / `FileRejected` / `FileScanUnavailable` |
| `GET` | `/api/files/{fileId}/content?exp&disp&sig` | public (signed) | the file with §8.10 headers; `403` bad or expired signature |
| `GET`/`POST`/`PUT` | `/api/staff/diagnosis-codes` (`?search&activeOnly`), `/{id}` | View / Manage | C1 |
| `GET`/`POST`/`PUT` | `/api/staff/lab-analytes`, `/{id}` | View / Manage | C2 |
| `GET`/`POST`/`PUT` | `/api/staff/note-templates`, `/{id}` | View / Manage | C3 |

**Customer (portal — FR-POR-3)**

| Method | Route | Access | → success |
|---|---|---|---|
| `GET` | `/api/customers/me/cats/{catId}/visits` | `Visits.ViewOwn` · logged | finalised visits only: date, service, vet, problems (names), `hasSummary` |
| `GET` | `/api/customers/me/visits/{visitId}` | `Visits.ViewOwn` · logged | the discharge summary JSON; `404` if not the caller's current cat or not finalised |
| `GET` | `/api/customers/me/visits/{visitId}/discharge-summary.pdf` | `Visits.ViewOwn` · logged | PDF |
| `GET` | `/api/customers/me/cats/{catId}/lab-results` | `Visits.ViewOwn` · logged | result sets of orders on **finalised** visits, with flags and ranges; attachments (not DICOM) as signed URLs |

Every write uses HO-ACC-001's `400` / `401` / `403` / `409` shapes (R1–R8).

### 12.5 Filters

- **`ClinicalRecordAccessFilter`** + `[ClinicalRecordAccess]` (§8.11), resolved per action, never global.
- Extend HO-ACC-001's `UniqueConstraintExceptionFilter`: `ix_visit_appointment` (`23505`) on the open
  route → re-query and return `200 { visitId }` (the race winner's); `consultation_draft` unique →
  retry once as an overwrite; the three file-attachment uniques → `400 FileAlreadyAttached`;
  `note_template` active unique → `400 NoteTemplateAlreadyExistsForService`; HO-IMP-001's named
  stock constraint (IMP-5) → `409 InsufficientStock`. Unknown constraints rethrow.
- **`ClinicalRecordFrozenFilter`**: a `PostgresException` with `SQLSTATE CR001` → `409` with
  *"This record is finalised and cannot be changed."*, logged at `Error` (reaching it means the
  aggregate guard was bypassed — a defect).

### 12.6 Read side — `src/Read/`

Queries in `Queries/Visit/`, handlers in `QueryHandlers/Visit/`, ViewModels flat; explicit projections,
`AsNoTracking()`; **a query never writes** (access logging is the filter's command).

`GetVisitDetailQuery` (with `allowedActions` from `VisitAllowedActions`, signed URLs from
`IFileUrlSigner`, the caller's draft, the template) · `GetOpenVisitsQuery` · `GetVisitByAppointmentQuery` ·
`GetVitalSignRulesQuery` · `GetDiagnosticOrderQuery` · `GetPendingDiagnosticsQuery` ·
`GetClinicalHistorySummaryQuery` · `GetClinicalHistorySectionQuery` (dispatching to the providers) ·
`GetDischargeSummaryQuery` (+ contributors) · `GetDispensaryItemsQuery` · `GetIssuableBatchesQuery` ·
`GetConsentFormQuery` · `GetConsentQuery` · `GetCatConsentsQuery` · `GetConsentStatusQuery` ·
catalogue list/detail queries · `GetCustomerCatVisitsQuery` · `GetCustomerVisitSummaryQuery` ·
`GetCustomerLabResultsQuery` · **`GetVisitChargeLinesQuery`** (HO-BIL-001 contract: per non-reversed
billable line — kind, description, quantity, unit, unit price snapshot, tax class, service or SKU id) ·
`GetStoredFileQuery` (for the content endpoint).

### 12.7 Host — `Program.cs`

Register every handler, validator (as `IValidator<T>`), query handler, event handler and history
section provider in §11–§12.6 — **count them**; the file ports by environment (the Development and
Testing scanners refuse other environments; `UnconfiguredMalwareScanner` otherwise unless a scanner is
configured); `IFileStore`, `IFileUrlSigner` (Singleton); `IDischargeSummaryPdfRenderer` (Scoped);
`VisitFollowUpGuard` into `SubjectGuardResolver`; the two templates via the catalogue;
`FileStorageOptions` with `ValidateOnStart` (root outside the content root; signing key ≥ 32 bytes
outside Development/Testing); `ClinicalRecordAccessFilter` and `ClinicalRecordFrozenFilter` (Scoped;
the frozen filter global alongside the concurrency filter). `ValidateScopes`/`ValidateOnBuild` stay on.

`appsettings.json` gains `FileStorage` **without** `UrlSigningKey` or paths; the git-ignored
`appsettings.Development.json` (and its `.example`) carries local values.

---

## 13. Allowed scope

```
src/Shared/CatEcommerce.Shared/Common/Enums/{the seventeen enumerations in §10.1}.cs
src/Shared/CatEcommerce.Shared/Common/Enums/AuditCategory.cs                                    ← ClinicalRecordAccess only
src/Shared/CatEcommerce.Shared/Common/Enums/NotificationSubjectKind.cs                          ← VisitFollowUp only
src/Shared/CatEcommerce.Shared/Common/Constants/{VitalSignRules,ClinicalNoteRules,DiagnosticRules,FileRules,SignatureRules,ConsentForms,ClinicalHistorySections,DiagnosisCodeSeed,ClinicalMessages}.cs
src/Shared/CatEcommerce.Shared/Common/Constants/{Actions,ActionRoles,CustomerActions,BusinessViolationCodes}.cs  ← append only
src/Shared/CatEcommerce.Shared/Common/Constants/SettingDefinitions.cs                           ← the one key in §10.2 only
src/Shared/CatEcommerce.Shared/Common/Constants/NotificationTemplateCatalogue.cs               ← the two definitions only
src/Domain/CatEcommerce.Domain/Aggregates/Visit/**
src/Domain/CatEcommerce.Domain/Aggregates/StoredFile/**
src/Domain/CatEcommerce.Domain/Entities/{VisitProblem,VitalsEntry,TreatmentLine,ClinicalPhoto,DiagnosticRequestedAnalyte,DiagnosticResultSet,DiagnosticResultValue,DiagnosticAttachment}.cs
src/Domain/CatEcommerce.Domain/ValueObjects/{SoapNote,FollowUp,SignatureStrokes,Measurement}.cs
src/Domain/CatEcommerce.Domain/DomainServices/{VitalRangeCheck,ReferenceRangeClassifier,ConsentRules,VisitAllowedActions,IMalwareScanner,IFileStore,IFileUrlSigner,IDischargeSummaryPdfRenderer}.cs
src/Domain/CatEcommerce.Domain/DomainServices/INotificationLinkBuilder.cs                        ← VisitSummaryLink only
src/Domain/CatEcommerce.Domain/Models/**                                                        ← new DTOs only
src/Domain/CatEcommerce.Domain/Events/Visit/** · Events/StoredFile/**
src/Application/CatEcommerce.Application/Commands/Visit/** · Commands/StoredFile/**
src/Application/CatEcommerce.Application/CommandHandlers/Visit/** · CommandHandlers/StoredFile/**
src/Application/CatEcommerce.Application/CommandHandlers/Clinic/{CompleteAppointmentCommandHandler,CancelAppointmentCommandHandler}.cs  ← ADR-133 guard lines only
src/Application/CatEcommerce.Application/CommandHandlers/Validators/{one per command in §11.1}.cs
src/Application/CatEcommerce.Application/DomainEventHandlers/Visit/**
src/Application/CatEcommerce.Application/DataMappers/{VisitMapper,DiagnosticMapper,ConsentMapper,ClinicalCatalogueMapper}.cs
src/Read/CatEcommerce.Read/Queries/Visit/** · QueryHandlers/Visit/** · ClinicalHistory/**       ← providers and extension interfaces
src/Read/CatEcommerce.Read/ViewModels/{the ViewModels in §12.4}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/CatEcommerceDbContext.cs            ← DbSets only
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/{one per root in §8.1}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/**                        ← the seven migrations in §12.1 only
src/Infrastructure/CatEcommerce.Infrastructure/Auditing/AuditCaptureExclusions.cs               ← ConsultationDraftAggregate only
src/Infrastructure/CatEcommerce.Infrastructure/Files/**
src/Infrastructure/CatEcommerce.Infrastructure/Configuration/FileStorageOptions.cs
src/Infrastructure/CatEcommerce.Infrastructure/Documents/QuestPdfDischargeSummaryRenderer.cs
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/Guards/VisitFollowUpGuard.cs
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/NotificationLinkBuilder.cs         ← VisitSummaryLink only
src/Api/CatEcommerce.Api/Controllers/{StaffVisits,StaffDiagnostics,StaffDispensary,StaffClinicalHistory,StaffConsents,StaffFiles,FileContent,StaffClinicalCatalogue,CustomerVisits}Controller.cs
src/Api/CatEcommerce.Api/Contracts/Visit/** · Contracts/StoredFile/**
src/Api/CatEcommerce.Api/Filters/{ClinicalRecordAccessFilter,ClinicalRecordAccessAttribute,ClinicalRecordFrozenFilter}.cs
src/Api/CatEcommerce.Api/Filters/UniqueConstraintExceptionFilter.cs                             ← §12.5 mappings only
src/Api/CatEcommerce.Api/appsettings.json · appsettings.Development.json.example               ← FileStorage section only (no secret, no path)
src/Api/CatEcommerce.Api/Program.cs                                                             ← §12.7 only
tests/CatEcommerce.Domain.Tests/**
tests/CatEcommerce.Application.Tests/**
tests/CatEcommerce.IntegrationTests/**
docs/test-cases/TC-HO-EMR-001.md
docs/gates/HO-EMR-001-*.md
.ai/decisions/adr.md                                                                            ← ADR-117…134, append only
```

**Edits to other areas' files** are the lines marked above and nothing else. When this handover is
approved the coordinator appends §1 Amended rows (OQ-17) to: **HO-APT-001** (ADR-133 guards),
**HO-ADM-001** (`clinical.followUpReminderLeadDays`, `Visits.ViewOwn` in `CustomerActions`, three
consent policy keys), **HO-NTF-001** (two templates, `VisitFollowUp` subject kind and guard,
`VisitSummaryLink`), **HO-ACC-001** (`AuditCategory.ClinicalRecordAccess`, one audit exclusion).

## 14. Forbidden scope

| Path | Why |
|---|---|
| `.ai/**` except `decisions/adr.md`, `.claude/**` | Governing docs and agents (OQ-1) |
| `docs/SRS.md`, `docs/USER_STORIES.md`, `docs/handover/**` | Inputs |
| `web/**` | HO-EMR-002 |
| Every file of HO-ACC-001, HO-ADM-001, HO-CAT-001, HO-NTF-001, HO-APT-001 beyond the lines in §13 | Their areas. **`CatAggregate` and `CatWeightRecordAggregate` are not edited** — weight records are created through the existing aggregate |
| **Any HO-IMP-001 or HO-SHP-001 file** | Consumed through §8.12; a mismatch is §20.2, not a patch |
| Prescription, formulary, dispensing-label, controlled-drug register types | HO-PHM-001, HO-SRG-001 |
| Surgery, anaesthesia log, checklist types | HO-SRG-001 |
| Invoice, payment or balance types; any `billing.*` table | HO-BIL-001 (ADR-127) |
| `NuGet.Config`, `global.json`, `*.sln`, `*.csproj` | No package is added |

---

## 15. Unit test cases

| ID | Case | Expected |
|---|---|---|
| **A — catalogues, access** | | |
| U-A1 | `DiagnosisCode` create with a lower-case or blank code; duplicate code (handler) | Refused; `DiagnosisCodeAlreadyExists` |
| U-A2 | Deactivate `SYS-NAD` | `SystemDiagnosisCodeReadOnly` |
| U-A3 | `LabAnalyte` with low > high; numeric without unit | `InvalidReferenceRange`; refused |
| U-A4 | `NoteTemplate.Update` | `Version` + 1 |
| U-A5 | `RecordClinicalRecordAccessCommandHandler` | One `AuditEntryAggregate`, category `ClinicalRecordAccess`, `DoNotPublish` |
| **B — visit and vitals** | | |
| U-B1 | `Open` with appointment `Booked`; `Completed`; `CheckedIn`; `InConsultation` | Refused, refused, opened, opened |
| U-B2 | `Open` for a deceased cat | `CatIsDeceased` |
| U-B3 | Vitals with no value | `VitalsRequired` |
| U-B4 | Temperature 46.0; 385.0 | `VitalSignOutOfRange` — no confirmation can save it |
| U-B5 | Temperature 42.0 unconfirmed; confirmed | `VitalsOutsidePlausibleRange` naming `Temperature` with 35.0–41.5; saved with `ConfirmedOutOfRange = [Temperature]` — **US-CLN-11 AC2** |
| U-B6 | Two plausible warnings, one confirmed | Refused, listing only the unconfirmed one |
| U-B7 | `VitalRangeCheck` boundaries (35.0, 41.5, 0.30, 12.00) | Inclusive — no warning |
| U-B8 | Vitals handler with weight | `CatWeightRecordAggregate` saved with `Id == VitalsEntry.Id`, date = branch-local date of `MeasuredOn`; **`CatAggregate` not saved**; visit updated after — **US-CLN-11 AC1** |
| U-B9 | Vitals handler without weight | No weight record |
| **C — note, finalise, addenda** | | |
| U-C1 | `SaveNote` with an inactive code; the same code twice | `DiagnosisCodeInactive`; `DuplicateProblem` |
| U-C2 | `SaveNote` snapshots code and name | Stored; renaming the code later does not change the visit |
| U-C3 | `Finalise` with no problem; blank Plan | `ProblemRequired`; `AssessmentAndPlanRequired` |
| U-C4 | `Finalise` with roles `[Administrator]`; `[Nurse]`; `[Vet]` | `OnlyVeterinarianMayFinalise` ×2; finalised — **US-CLN-22** |
| U-C5 | Every mutation method on a finalised visit | `VisitFinalised`, message pointing to addenda — **US-CLN-17 AC1, AC3** |
| U-C6 | `Finalise` twice | Second `VisitFinalised` |
| U-C7 | Reflection over `VisitAggregate` public methods | Each (except `Open`) calls `EnsureOpen` first — no method changes a finalised visit |
| U-C8 | `VisitAddendum.Add` on an open visit; on a finalised one | `AddendumRequiresFinalisedVisit`; added with author and time — **US-CLN-17 AC2** |
| U-C9 | Reflection over `VisitAddendumAggregate` | No public method besides `Add` |
| U-C10 | Draft returned by the read | Only when newer than the note and the visit is open; never another author's |
| U-C11 | `VisitAllowedActions` for Nurse / Vet / Admin × Open / Finalised | Nurse: vitals only on open; Vet: all on open, addendum on finalised; Admin: none |
| U-C12 | Template in the read | Present only while no note has been saved and an active template exists for the service — **US-CLN-20** |
| U-C13 | Finalise handler | Enqueues `visit.summary-available` to the cat's current owner, before the save |
| **D — diagnostics, files, photos** | | |
| U-D1 | `ReferenceRangeClassifier`: 4.9 in 5–10; 10.0; 10.1; no range; qualitative "negative" vs normal "Negative"; "Positive" | Low; Normal; High; NotAssessed; Normal; Abnormal — **US-CLN-14 AC2** |
| U-D2 | Result value with an analyte and no entered range | Range and unit copied from the analyte onto the value |
| U-D3 | `RecordResults` superseding a set of another order | `ResultSetNotFound` |
| U-D4 | `Cancel` with results | `DiagnosticOrderHasResults` |
| U-D5 | `RecordResults` after the visit is finalised | Allowed |
| U-D6 | `FileContentSniffer`: PDF bytes declared `image/png`; DICOM with `DICM` at 128; WebP | `FileContentMismatch`; accepted; accepted |
| U-D7 | `HmacFileUrlSigner`: valid; expired; `disp` changed; one signature byte flipped | Valid; refused; refused; refused (fixed-time compare) |
| U-D8 | Scanner guards | Development scanner in `Production` throws at construction; Testing scanner in `Development` throws |
| U-D9 | `OriginalFileName` `..\..\evil.pdf` with a control char | Stored as `evil.pdf` |
| U-D10 | `AttachPhoto` #21; `TakenOn` tomorrow | Refused ×2 |
| **F — treatments** | | |
| U-F1 | FEFO default | First issuable batch chosen |
| U-F2 | Non-FEFO batch without reason; with reason | `BatchOverrideReasonRequired`; recorded with reason — BR-4 |
| U-F3 | Expired batch, with and without a reason | `BatchExpired` both — **no override** (BR-3) |
| U-F4 | 5 requested, 3 available, no `AcceptShortfall` | `InsufficientStock` with 3 and the unit — **US-CLN-15 AC2** |
| U-F5 | Same with `AcceptShortfall` | Deducted 3, shortfall 2, ledger −3, `ClinicalStockShortfallRecordedEvent` |
| U-F6 | 0 available with `AcceptShortfall` | Deducted 0, no ledger entry, shortfall event |
| U-F7 | Controlled SKU | `ControlledDrugNotSupportedHere` |
| U-F8 | Reverse a line with deduction; reverse twice | Compensating +entry, `Reversed`; `TreatmentAlreadyReversed` |
| U-F9 | Billable service line; billable SKU line | Price snapshots equal the prices at *now* — ADR-127 |
| **G — summary, follow-up** | | |
| U-G1 | `SendAfter` for due 2026-10-20, lead 3, Dhaka | 2026-10-17 09:00 +06:00; due tomorrow → now |
| U-G2 | `VisitFollowUpGuard`: same stamp; changed; cleared; cat has a booked future appointment; cat deceased | Valid; invalid ×4 |
| U-G3 | Both template definitions | Pass HO-NTF-001 `TemplateRules`; transactional; no clinical variable |
| U-G4 | Summary for an open visit | Not produced (`null` → `404`) |
| U-G5 | Summary excludes reversed treatment lines and includes addenda | As stated |
| **H — consent** | | |
| U-H1 | `SignatureStrokes`: 1 point; 6000 points; x = 1.2; valid | `InvalidSignature` ×3; accepted |
| U-H2 | On-screen method with a file; upload method with strokes | `ConsentEvidenceMismatch` |
| U-H3 | `ConsentRules.IsValidFor` — withdrawn; other appointment; other kind; matching | false ×3; true |
| U-H4 | Signature SVG from strokes | Only `<svg>`, `<polyline>` elements with numeric points |
| **X — cross-cutting** | | |
| U-X1 | Reflection over `Events/Visit/**` and `Events/StoredFile/**` | No string properties |
| U-X2 | `AuditCaptureExclusions` | Contains `ConsultationDraftAggregate`; not `VisitAggregate` |
| U-X3 | Migration trigger SQL | Uses `VisitStatus.Finalised.Value`; covers every table in §8.4 |
| U-X4 | `ActionRoles` | `Visits.Finalise` = `[Vet]` exactly; no clinical write action includes `Reception` or `Administrator` |
| U-X5 | Every controller action returning clinical data | Carries `[ClinicalRecordAccess]` (reflection, with an explicit list of non-clinical exceptions) |

## 16. Integration test cases

Real HTTP, `Testing` environment, shared database (HO-ACC-001 §16.1). Workers off; tests call
HO-NTF-001's `NotificationWorkCycle` with `FakeTimeProvider`.

### 16.1 Additional shared-database rules

1. Each test creates its own branch, rooms, rostered vet and nurse, customer, cat, and checked-in
   appointment through HO-ADM-001 / HO-APT-001 endpoints (HO-APT-001 §16.1).
2. Diagnosis and analyte codes are minted per test (`T-{guid:N}` truncated to 30).
3. `FileStorage:RootPath` and `TempPath` point at a per-run temp directory; `UrlSigningKey` is a
   per-run random key; the scanner is `EicarTestScanner`.
4. **The 10-year fixture** (I-E4) is built once per run through repositories (not HTTP), on its own
   cat, and asserted only on that cat.
5. Phase F tests create their own SKU, batches and dispensary stock through HO-SHP-001 / HO-IMP-001's
   endpoints or seed helpers — never by writing `inventory.*` rows directly.
6. The immutability triggers are real; a test never disables them.

| ID | Case | Expected |
|---|---|---|
| I-A1 | Migrations on an empty database | Tables, uniques, `files` schema, every trigger in §8.4 present; `SYS-NAD` seeded |
| I-A2 | Host starts, scope validation on | Every registration resolves; startup fails outside Development/Testing with a short signing key or a root inside the content root |
| I-A3 | Vet creates a diagnosis code; nurse tries | `201`; `403` + `AuthorizationDenied` audit |
| I-B1 | Nurse opens a visit for a checked-in appointment; opens again | `201 { visitId }`; `200` same id |
| I-B2 | Two concurrent opens | Both return the same id; one row |
| I-B3 | Open for a `Booked` (not checked-in) appointment | `400 AppointmentNotReadyForVisit` |
| I-B4 | Nurse records vitals 4.2 kg, 38.6 °C, 180 bpm, 30/min, BCS 5, pain 1; `GET /api/cats/{catId}/weights` | `201`; the weight is the newest point with the entry's id; the visit shows each sign with its unit — **US-CLN-11 AC1**, UI-8 |
| I-B5 | Vitals 42.5 °C; then confirmed | `400 VitalsOutsidePlausibleRange` with the range; `201` with `confirmedOutOfRange: ["Temperature"]` — **AC2** |
| I-B6 | Weight record insert forced to fail (test hook on the weight configuration) | `500`; **neither** vitals nor weight committed — ADR-121 atomicity |
| I-B7 | Receptionist `GET`s the visit | `403` |
| I-C1 | Vet saves S/O/A/P + two problems | `200`, version increments; visit still `Open` — **US-CLN-12 AC1** |
| I-C2 | Vet `PUT`s a draft, then `GET`s the visit | `draft` returned with the text; another vet's `GET` has no draft — **AC2** |
| I-C3 | Save note after the draft | Draft no longer returned (older than the note) |
| I-C4 | Service with a template, new visit | `template` present; after the first save, absent; `appliedTemplateId` stored — **US-CLN-20** |
| I-C5 | Two vets save with the same `expectedVersion` | One `200`, one `409` |
| I-C6 | **Receptionist, nurse and administrator `POST …/finalise`** | `403` each, one `AuthorizationDenied` audit row each — **US-CLN-22**, AC-18 |
| I-C7 | Vet finalises without a problem; with `SYS-NAD` | `400 ProblemRequired`; `204` |
| I-C8 | After finalisation: `PUT …/note`, `POST …/vitals`, `POST …/photos`, `PUT …/follow-up` | `400 VisitFinalised` each; nothing changed — **US-CLN-17 AC1, AC3** |
| I-C9 | **`UPDATE clinic.visit SET assessment = 'x'`, `DELETE FROM clinic.visit_problem`, `INSERT INTO clinic.visit_vitals` for the finalised visit, `DELETE` / `UPDATE` / `TRUNCATE` of `clinic.visit_addendum`, all through Npgsql** | Each raises `CR001`; rows unchanged — ADR-118 |
| I-C10 | Vet adds an addendum; `GET` | `201`; the original assessment unchanged and the addendum listed with author and time — **AC2** |
| I-C11 | Addendum on an open visit | `400 AddendumRequiresFinalisedVisit` |
| I-C12 | Complete the appointment while the visit is open; after finalising | `400 VisitNotFinalised`; `204` — ADR-133 |
| I-C13 | Staff cancel of the checked-in appointment with a visit | `400 VisitInProgress` |
| I-C14 | Finalise; run the notification pass | `visit.summary-available` in-app copy for the owner with a `/portal/visits/{id}` link and no clinical text |
| I-C15 | Audit rows after C1–C10 | `DataChange` rows for visit saves; **none** for drafts; `BusinessEvent` for finalise and addendum — AC-20 |
| I-D1 | Order blood work | `201`; `Pending` — **US-CLN-14 AC1** |
| I-D2 | Record values (one low, one high, one normal, one qualitative abnormal) | Flags returned and persisted with the ranges — **AC2** |
| I-D3 | Upload a PDF as `LabResult`; attach with date and note; `GET` the order; fetch the signed URL | `201`; attached; `200` with `application/pdf`, `nosniff`, sandbox CSP, `no-store` — **AC3** |
| I-D4 | Upload DICOM; fetch | `Content-Disposition: attachment` |
| I-D5 | Upload a PNG renamed `.pdf` declared `application/pdf`; 51 MB PDF; EICAR text as PDF-declared | `400 FileContentMismatch`; `413` or `400 FileTooLarge`; `400 FileRejected`, nothing stored, temp dir empty |
| I-D6 | Signed URL after 5 min (`FakeTimeProvider`); tampered `sig` | `403` both |
| I-D7 | `FileStorage:Scanner` unset in a `Staging`-named host | Upload `400 FileScanUnavailable` — OQ-6 posture |
| I-D8 | Record results after the visit is finalised; supersede the first set | Both `201`; both sets returned, the second naming the first |
| I-D9 | `UPDATE` a result value through Npgsql | Raises |
| I-D10 | Attach a photo with date and note; history `timeline` | The photo appears at its date — **US-CLN-19** |
| I-D11 | Attach the same file twice | `400 FileAlreadyAttached` |
| I-E1 | History summary for a cat with visits, labs, vaccinations, weights | Every section declared; `prescriptions`, `boarding-stays`, `food-purchases` `available: false` with owners — **US-CLN-13 AC1** (built sections) |
| I-E2 | Section pages | Paged, newest first |
| I-E3 | Each history call | One `ClinicalRecordAccess` audit row with actor and IP — NFR-SEC-8 |
| I-E4 | **10-year fixture: 50 summary calls and 50 section pages** | p95 ≤ 600 ms and ≤ 300 ms — NFR-PRF-4 server share |
| I-E5 | Access-log write forced to fail | `500`; no clinical data in the body — ADR-129 |
| I-F1 | Record a consumable with FEFO | `201`; one `Dispense` ledger entry −qty for the FEFO batch referencing the line — **US-CLN-15 AC1** |
| I-F2 | Choose the expired batch | `400 BatchExpired`; no ledger entry |
| I-F3 | Quantity above stock; then with `acceptShortfall` | `400 InsufficientStock { availableQuantity }`; `201` with shortfall and the event — **AC2** |
| I-F4 | Two concurrent treatments for the last unit | One `201`, one `409 InsufficientStock` (IMP-5) |
| I-F5 | Reverse a line | Compensating ledger entry; line `Reversed` |
| I-F6 | Ledger save forced to fail | Neither line nor entry committed — ADR-126 atomicity |
| I-F7 | Controlled SKU | `400 ControlledDrugNotSupportedHere` |
| I-F8 | `GetVisitChargeLinesQuery` after finalisation | Billable, non-reversed lines with snapshots — HO-BIL contract |
| I-G1 | Staff and customer summary for a finalised visit; PDF | JSON with diagnosis, treatments, medicines, home-care, follow-up date; `application/pdf` — **US-CLN-16** |
| I-G2 | Summary of an open visit, staff and customer | `404` both |
| I-G3 | Customer requests another customer's visit; the previous owner after an ownership transfer | `404` both |
| I-G4 | Set a follow-up reminder 10 days out; advance to lead time; run the pass | `visit.follow-up-due` delivered once — **US-CLN-18** |
| I-G5 | Change the follow-up before it sends; book an appointment for the cat before it sends | Old reminder cancelled by the guard; stood down |
| I-G6 | Book via HO-APT-001, set a follow-up with that appointment; set with another cat's appointment | `204`; `400 FollowUpAppointmentInvalid` |
| I-H1 | `GET` the surgery consent form before publication; after | `404`; body and version |
| I-H2 | Receptionist records on-screen consent for a theatre appointment | `201`; linked to cat, appointment, kind, date, version — **US-CLN-21 AC1** |
| I-H3 | Upload a signed form and record | `201` |
| I-H4 | Record with an out-of-date form version | `400 ConsentFormVersionNotCurrent` |
| I-H5 | Consent status before, after recording, after withdrawal | false; true; false — the HO-SRG contract for **AC2** |
| I-H6 | `DELETE FROM clinic.consent_record` via Npgsql | Raises |
| I-X1 | Log capture across all cases at `Information`+ | No SOAP text, problem names, addendum text, file names, signature data or storage paths |
| I-X2 | Every clinical `GET` in this suite | Matched by an access audit row |

---

## 17. Implementation order and verification

| Phase | Stories | Delivers |
|---|---|---|
| **A** | — | V1–V3 verified and recorded; enumerations, constants, codes, setting, `ActionRoles`; catalogues (codes, analytes, templates) + `SYS-NAD`; access-log filter and command; migration `AddClinicalCatalogues` |
| **B** | US-CLN-11 | Visit, open, vitals, ranges, companion weight commit, visit read, open list; migration `AddVisits` |
| **C** | US-CLN-12, 17, 20, 22 | Note, drafts, template delivery, finalise, addenda, triggers, ADR-133 guards, summary-ready message; migrations `AddConsultationNotes`, `AddClinicalRecordImmutability` |
| **D** | US-CLN-14, 19 | Stored files (or HO-IMP-001's — §20.3), diagnostics, photos; migrations `AddStoredFiles`, `AddDiagnostics` |
| **E** | US-CLN-13 | History providers, summary, sections, 10-year fixture and budget |
| **F** | US-CLN-15 | Treatments, dispensary reads, stock companion commit, reversal, charge-lines query — **only after HO-SHP-001 and HO-IMP-001 are merged and match §8.12** |
| **G** | US-CLN-16, 18 | Discharge summary JSON/PDF, customer reads, follow-up, guard, templates |
| **H** | US-CLN-21 | Consent forms, record, withdraw, status, signature SVG; migration `AddConsents` |

Each phase: code → unit tests green → migration generated **and read** (triggers are hand-written SQL)
→ integration tests green → commit recorded in the test sheet. Phases G and H do not depend on F.

```bash
dotnet restore CatEcommerce.sln
dotnet build CatEcommerce.sln            # 0 warnings, 0 errors
dotnet test tests/CatEcommerce.Domain.Tests
dotnet test tests/CatEcommerce.Application.Tests
dotnet dotnet-ef migrations add <PhaseMigration> --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api --output-dir Persistence/Migrations
dotnet dotnet-ef database update --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api
dotnet run --project src/Api/CatEcommerce.Api
dotnet test tests/CatEcommerce.IntegrationTests
```

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | CLN-11 AC1 | Vitals attach to the visit with units and the weight updates the cat's history atomically | U-B8, I-B4, I-B6 |
| A2 | CLN-11 AC2 | Out-of-plausible values warn and save only when confirmed; absurd values are refused | U-B4…U-B7, I-B5 |
| A3 | CLN-12 AC1 | SOAP plus coded problems save as a draft on the open visit | U-C1, U-C2, I-C1, I-C5 |
| A4 | CLN-12 AC2 | An auto-saved working copy is returned to its author on return | U-C10, I-C2, I-C3 |
| A5 | CLN-13 AC1 | Visits, diagnoses, vaccinations, lab results, weight trend and photos reachable from one history API — *prescriptions, boarding stays, food purchases deferred to HO-PHM-001, HO-BRD-001, HO-ORD-001 (§4.2)* | I-E1, I-E2 |
| A6 | CLN-13 AC2 | 10-year cat within the server budget (render measured in HO-EMR-002) | I-E4 |
| A7 | CLN-14 AC1 | Diagnostic orders recorded as pending | I-D1 |
| A8 | CLN-14 AC2 | Structured results flagged against reference ranges | U-D1, U-D2, I-D2 |
| A9 | CLN-14 AC3 | PDF, JPEG and DICOM-as-file attachable with date and note, safely stored and served | U-D6, U-D7, I-D3…I-D7 |
| A10 | CLN-15 AC1 | Exact batch deducted with a ledger entry in the same commit; expired refused | U-F1…U-F3, I-F1, I-F2, I-F6 |
| A11 | CLN-15 AC2 | Insufficient stock warns; shortfall recordable for reordering | U-F4…U-F6, I-F3, I-F4 |
| A12 | CLN-16 AC | Summary with diagnosis, treatment, medicines, home care and follow-up date, printable and in the portal — *take-home prescriptions → HO-PHM-001* | U-G4, U-G5, I-G1…I-G3, I-C14 |
| A13 | CLN-17 AC1 | A finalised note is read-only | U-C5…U-C7, I-C8 |
| A14 | CLN-17 AC2 | Corrections are timestamped, attributed addenda; the original remains visible | U-C8, U-C9, I-C10, I-C11 |
| A15 | CLN-17 AC3 | Direct edits through the API are refused — and through the database | I-C8, **I-C9**, I-D9, I-H6 |
| A16 | CLN-18 AC | A follow-up is an appointment link or a queued, guarded reminder | U-G1, U-G2, I-G4…I-G6 |
| A17 | CLN-19 AC | A dated, noted photo appears on the clinical timeline | U-D10, I-D10, I-D11 |
| A18 | CLN-20 AC | A service template is offered for pre-fill and stays editable | U-C12, I-C4 |
| A19 | CLN-21 AC1 | Consent by on-screen signature or upload, linked to cat, procedure and date, with the form version | U-H1, U-H2, U-H4, I-H1…I-H4 |
| A20 | CLN-21 AC2 | Consent status contract for "cannot start" — *enforcement delivered by HO-SRG-001 (US-SRG-03)* | U-H3, I-H5 |
| A21 | CLN-22 AC | Non-vets (incl. administrators) refused finalisation server-side and logged — *prescribing → HO-PHM-001* | U-C4, U-X4, I-C6 |
| A22 | NFR-SEC-8 | Every clinical read is logged, fail closed | U-A5, U-X5, I-E3, I-E5, I-X2 |
| A23 | ADR-133 | An appointment cannot complete while its visit is open | I-C12, I-C13 |

**Deferred, named in §4.2**: US-CLN-13 prescriptions / boarding / food panels; US-CLN-16 take-home
prescriptions; US-CLN-21 AC2 enforcement; US-CLN-22 prescribing; billing lines; reorder list.

---

## 19. Required gates

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | `Visit` roots in `Clinic` (ADR-117, OQ-1); the two companion-commit exceptions and nothing else crossing rule 1; drafts, addenda, diagnostics, consents as separate roots; section providers and contributors as the only extension points; **no enqueue from any event handler**; edits to HO-APT/ADM/NTF/ACC files confined to §13's lines; HO-IMP/HO-SHP consumed, never edited |
| 2 | SECURITY | `security-reviewer` | **Immutability at three layers (I-C9)**; `Visits.Finalise` = Vet only (U-X4, I-C6); access logging fail-closed (I-E5); upload validation by content, fail-closed scanning, storage outside web root, signing-key handling, fixed-time signature check, response headers (I-D3…I-D7); customer `404`s incl. after ownership transfer; no clinical text in events, logs or SMS |
| 3 | CODE_REVIEW | `code-reviewer` | `EnsureOpen` everywhere (U-C7); companion-commit comments cite F23; time through HO-ADM-001's helpers; temp files always deleted; no service wrapping a repository call |
| 4 | TEST | `test-engineer` | `TC-HO-EMR-001.md` from §15/§16 first; triggers exercised through Npgsql, not only the API; I-B2 / I-F4 with real parallelism; the 10-year fixture measured, not estimated |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row; the deferrals in §4.2 recorded as deferrals, not passes; the scanner and provider caveats (OQ-6, HO-NTF-001 OQ-1) stated |

---

## 20. BLOCKED conditions — stop and report

**20.1 HO-ACC-001, HO-ADM-001, HO-NTF-001 or HO-APT-001 is not merged.** This slice consumes their
sessions, roles, settings, policy documents, outbox, guards and appointments. Do not stub them.

**20.2 HO-SHP-001 or HO-IMP-001 is not merged, or differs from §8.12 (IMP-1…6, SHP-1…3).** Stop
**Phase F** only and report the difference. Do not write `inventory.*` rows from this slice, do not
add a stock table here, do not edit Inventory's code to fit. Phases A–E, G and H continue.

**20.3 Two file facilities.** If HO-IMP-001 has already built a stored-file facility, use it when it
meets ADR-125's contract (content sniffing, fail-closed scan, storage outside the web root, signed
expiring URLs); if it does not meet it, stop and report — never ship two upload paths.

**20.4 A request to let administrators finalise, or to edit a finalised visit "just this once".**
Refuse: BR-6, BR-11, Appendix D. It needs an SRS change.

**20.5 The immutability triggers cannot be made to work (V1 fails).** Stop. Do not rely on the
aggregate alone.

**20.6 A malware scanner seems to be needed to finish.** Do not add a package or call an external
service; the unconfigured scanner refusing uploads is the specified behaviour (OQ-6).

**20.7 Do not deduct stock from an event handler, and do not allow an expired batch "with a reason".**
ADR-126, BR-3.

**20.8 Do not enqueue from an event handler** — HO-NTF-001 ADR-057.

**20.9 A requirement conflict is a stop, not an edit** — SRS, backlog and other handovers are
forbidden scope.
