# HO-CAT-001 — Cat Profiles (backend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-CAT-001** |
| Title | Cat Profiles — backend slice |
| Date | 2026-09-20 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) §2 — **US-CAT-01 … US-CAT-11** |
| SRS | [`docs/SRS.md`](../SRS.md) — **FR-CAT-1 … FR-CAT-11**, FR-POR-2, FR-POR-3, UI-4, UI-7, C-6, C-7, C-9, NFR-SEC-5 |
| Baseline commit | `5b260df` (Part A doc correction + Core 1.0.34) |
| Amended | 2026-09-20 — microchip identification removed from the product (SRS v2.0). No implementation had started <br><br>**2026-09-23 — appended by later handovers (owed rows, recorded now that all 18 pairs exist).** Everything below is an **append** inside the lines each handover lists in its §13; no existing behaviour changes. These are recorded here so this document is not read as the whole picture. **Appends:** HO-BRD-001 extracts the vaccination rule into `VaccinationStatusRule` and adds a read-only `CatAggregate.EvaluateVaccinationStatus` — which §12.3 of this document explicitly left to that handover. HO-EMR-001 creates `CatWeightRecordAggregate` rows alongside visit vitals (its ADR-121), using the aggregate this slice owns. Cat photos, when they arrive, go through HO-FIL-001 rather than this slice. |
| Amended | 2026-09-20 — dev **and** integration tests both use the local PostgreSQL `catecommerce_dev`; Testcontainers dropped; connection string in `appsettings.Development.json`. §5, §13, §16, §17, §20 updated |
| Amended | **2026-09-25 — owner decision, prompted by HO-CAT-002-ACCEPTANCE's BLOCKER 1 (A11 could not be evidenced under the original contract). Confirmed by the owner on 2026-09-25.** C10 (`RecordCatParasiteTreatment`) is amended so its `201` response body also returns the created treatment's `treatmentId` and the server-computed, **required** `nextDueOn` — the owner confirmed on 2026-09-25 that `nextDueOn` is required, not nullable, because `IntervalDays` is always validated (§11.3) and the aggregate therefore always computes a value — instead of no body at all. This lets HO-CAT-002's parasite-control screen show the next-due date the server actually computed, with no client-side reimplementation of the interval rule (which HO-CAT-002-ARCHITECTURE, fix `1a4cac4`, already rejected once for the same reason on vaccination status). The rule that computes `NextDueOn` (§8.2) is unchanged — this only exposes its result. This handover's implementation had already landed on master (`4007225`, 2026-09-21), so this amendment changes existing code — the handler and controller in `RecordCatParasiteTreatmentCommandHandler`/`CatsController` — rather than shaping a build that had not started yet. Implemented on `ho/CAT-001-c10`. §11.5, §12.4, §15, §16 and §18 are updated below |
| Amended | **2026-09-26 — HO-CAT-001 gate-fix pass (ARCHITECTURE/SECURITY/CODE_REVIEW findings from the first gate run on `7abc9e4`; implemented on `ho/CAT-001-gates`).** Three owner decisions, then the fixes they and the gates required. See `docs/gates/HO-CAT-001-ARCHITECTURE.md`, `-SECURITY.md`, `-CODE_REVIEW.md` (2026-09-26, all FAIL) for the findings this row closes. <br><br>**(a) QuestPDF licensing (§20.6) — CONFIRMED by the owner, 2026-09-26.** The Community licence applies. `QuestPdfCatMedicalHistoryRenderer`'s and `CatProfilesModule`'s "not resolved here" comments are updated; §20.6 is closed. <br><br>**(b) Commit 4007225's 27 non-implementation files — CONFIRMED by the owner, 2026-09-26** as the owner's own handover-authoring work (`.claude/agents/handover-writer.md`, 26 `docs/handover/HO-US-ACC-*.md` files), not part of this handover's implementation. The ARCHITECTURE gate's MEDIUM finding on this is cleared. <br><br>**(c) Paging — owner decision, 2026-09-26.** `GET /api/cats`, `GET /api/cats/{catId}/weights` and `GET /api/vaccines` now return `PagedResult<T>` (`{ items, pageNumber, pageSize, totalCount }`) — the same shape the Accounts read side already uses (`Read/ViewModels/PagedResult.cs`; see `GetStaffAccountsQueryHandler`), not a second type. `pageSize` is capped at 100 in each controller's paging helper: a larger value is **clamped**, not rejected; `pageSize <= 0` is a **400** (`BusinessViolationCodes.InvalidPageSize`). §12.3/§12.4 below are updated for the new response shape and the cap. <br><br>**Fixes, same commit set:** <br>- **[HIGH, ARCHITECTURE]** `GetCatProfileQueryHandler` (Q1) and `GetCatMedicalHistoryQueryHandler` (Q5) rewritten from aggregate rehydration to a database-side `Select` projection (two-phase, matching `GetCatsQueryHandler`'s existing pattern) — no behaviour change, same view model output; regression-tested in `tests/CatEcommerce.IntegrationTests/Endpoints/CatProfileProjectionEndpointTests.cs`. <br>- **[HIGH, SECURITY/CODE_REVIEW]** `Reason` gets `.MaximumLength(1000)` in `FlagCatHighRiskCommandValidator`, `ClearCatHighRiskFlagCommandValidator` and `TransferCatOwnershipCommandValidator`, matching `AuditEntryConfiguration`'s own column limit — a caller no longer gets a `500` (and an unaudited state change) from a `Reason` the audit write alone used to reject. <br>- **[MEDIUM, CODE_REVIEW]** `CatAggregate.Today()`, `CatWeightRecordAggregate.Record`'s inline `DateTime.UtcNow` and `CatMapper`'s `UploadedOn` now take `Today`/`uploadedOn` as DTO fields threaded from each command handler's injected `TimeProvider` — the same seam HO-ACC-001 already uses (`MergeCustomerCommandHandler`), not a wall-clock read inside the aggregate. `CatMapper`'s `ToDomainDto()` overloads for `RegisterCatCommand`, `UpdateCatProfileCommand`, `RecordCatNeuterStatusCommand`, `RecordCatWeightCommand`, `RecordCatVaccinationCommand`, `RecordCatParasiteTreatmentCommand`, `MarkCatDeceasedCommand` and `AddCatPhotoCommand` all gained a `today`/`uploadedOn` parameter accordingly. <br>- **[MEDIUM, SECURITY]** Identity fields no longer trust the request body. `RecordCatWeightCommand.RecordedByStaffId` is always `UserContext.UserId` (`CatsController`; `RecordCatWeightRequest` no longer carries the field at all) — its validator's `NotEmpty()` was dropped because `UserId` is `Guid.Empty` for an anonymous caller until HO-ADM-001's `[RequireAction]` lands on this controller (out of this fix's scope; see §20.3). `RecordCatVaccinationCommand.AdministeredByVetId` and `RecordCatParasiteTreatmentCommand.AdministeredByStaffId` still name a colleague, but `RecordCatVaccinationCommandHandler`/`RecordCatParasiteTreatmentCommandHandler` now resolve them through a second, read-only `IAggregateRootRepository<StaffAccountAggregate>` lookup — the same pattern `RecordCatVaccinationCommandHandler` already uses for `VaccineAggregate` (§11.5) — rather than the Read-side `GetStaffAccountQuery` (Application cannot reference `CatEcommerce.Read`; verified compliant by the ARCHITECTURE gate). A staff id that does not resolve to an existing, non-deleted `StaffAccountAggregate` is `BusinessViolationCodes.StaffAccountNotFound`; a vet field naming a staff account without the `Vet` role is the new `BusinessViolationCodes.StaffAccountNotVeterinarian`. No Accounts-owned file was edited — `IAggregateRootRepository<>` is already registered as an open generic (§12.5), so no new DI registration was needed either. <br>- **[LOW, SECURITY]** `QuestPdfCatMedicalHistoryRenderer` now registers Noto Sans Bengali (SIL Open Font License 1.1) as an embedded-resource fallback font (`Documents/Fonts/NotoSansBengali-Regular.ttf`), so a Bengali cat name/breed/note no longer crashes the medical-history PDF with a `DocumentDrawingException`. `CatsController.GetCatMedicalHistoryPdf` catches that exception and logs the cat id only — never the exception object or its message, which quotes the offending free text verbatim. <br>- **[Tests]** U26 broadened from one `FlagHighRisk` call to all eleven §8.2 mutation methods in sequence; two new calculator tests prove "worst status wins" (§12.3 step 4) across two core vaccines, not just one. <br>- **[LOW]** The stale `RecordClassification.CatProfiles.cs` comment (still describing `audit.audit_entry` as an "intentional, temporary gap") is corrected — Accounts classifies it, no gap exists. `CatsController.TryParseEnum`/`ApplyPaging` now return the standard `ValidationResponse` 400 shape (`BusinessViolationCodes.InvalidEnumValue`/`InvalidPageSize`) instead of a bespoke `{ error }` object — `web/lib/api/client.ts` parses every 400 body as `ValidationResponse` unconditionally, so the old shape would have thrown inside `ApiValidationError` handling had the web client ever hit either path. <br><br>**Amended rows for files this pass touched outside §13's literal list:** `src/Infrastructure/CatEcommerce.Infrastructure/CatEcommerce.Infrastructure.csproj` (adds the two embedded font resources for the Bengali fallback — not a package reference, but still outside §13(a)/(b)'s two named exceptions). `tests/CatEcommerce.Application.Tests/CatEcommerce.Application.Tests.csproj`'s `ProjectReference` to `CatEcommerce.Read` (ARCHITECTURE gate MEDIUM, pre-existing before this pass) — left as is; the calculator tests it enables (U34–U38 and the two new "worst status wins" cases) belong in this test project until a Read/Infrastructure test project exists to move them to. <br><br>**HO-ACC-001's own appends to this area, not previously listed here (ARCHITECTURE gate LOW):** `CatAggregate.ReassignOwnerOnCustomerMerge` (§8.9), `CatOwnerReassignedOnMergeEvent`, `CatOwnerMergeReassignmentDto`; the `IClientIpAccessor`/`TimeProvider`/`AuditCategory` additions to the three Cat audit domain-event handlers ((19)(b)); `SessionUserContextProvider` replacing `DevUserContextProvider` as the §12.5 seam. |
| Amended | **2026-09-26 — integrator merge of HO-ADM-001's authorization (ADR-043) into `ho/CAT-001-gates`, closing this document's own SECURITY BLOCKER (§20.3, `docs/gates/HO-CAT-001-SECURITY.md`).** `master` (`5200d75`) merged cleanly except two files, both reconciled to keep both sides' work rather than picking one: `CatsController.cs` — master's `[RequireAction]`/`OwnerScope`/`CurrentCaller()` (ADR-043) alongside this branch's own gate fixes (`InvalidPageSizeValidationResponse`, `ApplyPaging`'s clamp-at-100); the two controllers' duplicated local `MaxPageSize = 100` constants (this branch's own, predating ADM-001) are now a single `Shared.Common.Constants.PagingRules.MaxPageSize`, which HO-ADM-001 had already introduced for its own paged admin endpoints. `CatIntegrationTestBase.cs` — kept `RegisterCatAsync`'s `name` parameter (this branch, used by the Bengali-name PDF test) alongside master's staff sign-in flow (`CreateClient` override, `SignInAsFreshAdministratorAsync`, `RegisterCatAsync` now signs in before registering); kept both `SeedStaffAccountAsync` (this branch) and `SignInAsFreshAdministratorAsync` (master) since tests elsewhere use each independently. Two more test-only fixes were needed because HO-ADM-001's gate-authored tests were written against contracts this branch changed afterwards: `CatVaccineAuthorizationSecurityTests.cs` read `GET /api/cats`'s body as a bare array — now `.items` (this document's own `PagedResult<T>` amendment above); `AdminGlobalSettingsLiveEffectEndpointTests.cs` named an Administrator-only staff account as the administering vet — now also seeded with the `Vet` role (this pass's own `StaffAccountNotVeterinarian` check, above). `CatsPaginationEndpointTests.cs` needed the same treatment for a different reason: every cat/vaccine action now requires a signed-in caller, so its anonymous-caller cases (`pageSize=0`, negative, and the vaccine cases) now sign in a fresh Administrator first — otherwise the `[RequireAction]` filter's `401` pre-empted the paging validation the test was actually exercising. `AdministrationIntegrationTestBase.SeedCatAsync` (added by HO-ADM-001, before this branch's `Today` DTO field existed) needed a `Today` argument added to its `CatRegistrationDto` construction. Full suite (Domain/Infrastructure/Application/Integration, 665 tests) green, twice in a row, after applying the previously-pending `AddAdministration` migration to `catecommerce_cat`. `CatVaccineFullAuthorizationMatrixTests` (22 tests) confirmed green on its own. <br><br>**Integrator-authorised migration, same pass:** `AuditEntryConfiguration.BeforeValue`/`AfterValue` widened from `character varying(2000)` to `text` (`WidenAuditEntryValues`, `20260926135821`) — the SECURITY gate's MEDIUM finding above: `AuditChangeCaptureInterceptor` snapshots the whole cat root row into these columns, and the cat validators alone allow ~3,450 characters, comfortably over the old cap. No domain invariant enforces a length on either field (`AuditEntryAggregate.Record` validates `EntityType`/`Action`/`Reason` only), so `text` is unbounded by design, not a placeholder. `has-pending-model-changes` verified clean afterwards. <br><br>**Web follow-up checked, no change needed:** `web/components/cat/WeightEntryForm.tsx`, `web/lib/api/cats.ts`'s `RecordCatWeightRequest`/`CatWeightPoint` and `web/lib/hooks/useCatMutations.ts` already carry no "recorded by" field — removed in the `cb2ddd2` owner-decision commit that shipped alongside this pass's server-side `RecordedByStaffId` fix. `web/mocks/handlers/areas/cat-profiles.ts`'s internal `StoredWeight.recordedByStaffId` is mock-server-only bookkeeping, never read from the request body or exposed to a form — left as is. `npx vitest run` (104/104), `npx tsc --noEmit` and `npx eslint .` all clean. |
| Amended | **2026-09-26 — HO-CAT-001 second-pass ARCHITECTURE/CODE_REVIEW/SECURITY gate fixes (all three PASSED — `docs/gates/HO-CAT-001-ARCHITECTURE.md`, `-CODE_REVIEW.md`, `-SECURITY.md` — with 1 MEDIUM/4 LOW, 1 MEDIUM/4 LOW and 3 LOW respectively; closed on `ho/CAT-001-gates`), then a merge of `master` (`eae3d7b`, `ho/integrator-host`: host-wide MVC filters, staff-list paging caps, `BusinessViolationCodes.ContainsControlCharacter` moved to the base file — no collision with anything Cat-owned).** Six items: <br><br>**1. `pageNumber` had no upper bound (ARCHITECTURE MEDIUM / CODE_REVIEW MEDIUM / SECURITY LOW), proven live at `pageNumber=2147483647` → 500 on all three list endpoints** (`Query<T>.Skip`'s `(PageNumber-1)*PageSize` overflowing `int` to a negative `OFFSET`). New `src/Api/CatEcommerce.Api/Controllers/PagingHelper.cs` applies `PagingRules.MaxPageNumber` (400, new `BusinessViolationCodes.Cat.cs` code `InvalidPageNumber`, same shape as `InvalidPageSize`), the same policy `AdminAuditEntriesController`/`AdminLeaveRequestsController` already use. This also closes the two LOW findings on the duplicated paging logic itself (ARCHITECTURE LOW 2 / CODE_REVIEW LOW): `CatsController.ApplyPaging` and `VaccinesController.GetVaccines` both now call the one shared `PagingHelper.ApplyPaging` instead of `VaccinesController` reaching into `CatsController.InvalidPageSizeValidationResponse` directly. `PagingRules.cs`'s doc comment (ARCHITECTURE LOW 1 / CODE_REVIEW LOW) now states both of its callers' policies explicitly: admin endpoints reject an over-large `pageSize`; Cat/Vaccine endpoints clamp it (owner decision). Three new `CatsPaginationEndpointTests.cs` cases (`pageNumber=2147483647` → 400, one per endpoint). <br><br>**2. Staff-supplied owner ids were never checked against a real customer (SECURITY LOW), proven live: registering with a random `ownerId` orphaned the cat; transferring to a Nurse's staff-account id "worked".** `RegisterCatCommandHandler.OwnerId` and `TransferCatOwnershipCommandHandler.NewOwnerId` are now read-only `IAggregateRootRepository<CustomerAccountAggregate>` existence checks in the handler — the same shape `RecordCatVaccinationCommandHandler` already uses for the vet id — returning 400 `CustomerNotFound` (reused from `BusinessViolationCodes.Accounts.cs`) on no match. `CatIntegrationTestBase` gains `SeedCustomerAsync` (a real, walk-in-shaped `CustomerAccountAggregate`, the same pattern `SeedStaffAccountAsync` uses); `RegisterCatAsync` now seeds one whenever the caller does not pin a specific owner id, and the two call sites that passed a fabricated `Guid.NewGuid()` explicitly now seed one instead. <br><br>**3. No count cap on the medical-profile lists (SECURITY LOW), proven live: 3,000 allergies of 500 characters each (~1.5 MB) succeeded in ~1.7s** — the incidental cap the old `varchar(2000)` audit columns used to provide is gone now that they are `text` (this document's own prior Amended row). HO-CAT-001 does not specify a list-size limit (only per-item field lengths), so new `Shared.Common.Constants.CatMedicalProfileRules.MaxListItems` (50) is a sensible cap chosen here, applied to `Allergies`/`ChronicConditions`/`Medications` in `UpdateCatMedicalProfileCommandValidator`. Six new boundary tests (at-maximum succeeds / over-maximum fails, one pair per list). <br><br>**4. The Api layer referenced a QuestPDF type directly (ARCHITECTURE LOW 3 / CODE_REVIEW LOW).** `QuestPdfCatMedicalHistoryRenderer.Render` now catches its own library's `DocumentDrawingException` and translates it into a new port-level `CatMedicalHistoryRenderException`; `CatsController` catches that instead and no longer references any QuestPDF type. The exception's `Message` is deliberately generic (the SECURITY gate's own prior LOW: QuestPDF's message quotes the offending free text verbatim) — logging behaviour (cat id only) is unchanged. <br><br>**5. A stale comment in `RecordCatWeightCommandValidator.cs` (ARCHITECTURE LOW 4 / CODE_REVIEW LOW)** said `RecordedByStaffId.NotEmpty()` was dropped "until HO-ADM-001's `[RequireAction]` lands on this controller" — that landed at the `ac79f82` merge. `RecordClinical` is staff-only (not in `CustomerActions.Cat`), so a caller that reaches this validator always has a real `UserContext.UserId`; `NotEmpty()` is restored as defence in depth against a future regression in the controller's own assignment, comment corrected. <br><br>**6. Hardening note, not a finding (SECURITY): neither the medical-history PDF nor the cage-card PNG sent `Cache-Control: no-store`.** Both now do, matching the existing pattern (`AdminAuditEntriesController`'s CSV export, the auth/session endpoints). Two new test assertions. <br><br>Full suite (Domain 223 / Infrastructure 28 / Application 93 / Integration 339 = 683 tests) green twice in a row after the `master` merge; `has-pending-model-changes` clean (no migration in this pass); build 0 warnings. `CatVaccineFullAuthorizationMatrixTests` (22 tests) confirmed green in isolation — one isolated re-run hit a transient 500 on three roles, not reproduced on immediate re-run and not present in either full-suite pass, consistent with this document's own §5.1 note that `catecommerce_cat` is shared and not cleaned between runs. |
| Amended | **2026-09-26 — HO-CAT-001 CODE_REVIEW third-pass fix (PASSED with 1 new MEDIUM, `docs/gates/HO-CAT-001-CODE_REVIEW.md`).** The second pass's customer-must-exist check (row above, item 2) rejected a non-existent owner id but not one that exists and is merely `Merged` or `Anonymised` — `EfAggregateRootRepository.GetByIdAsync` only excludes a hard-deleted (`IsMarkedToDelete`) row, and neither status marks a `CustomerAccountAggregate` row deleted, so a staff caller could still register or transfer a cat onto a merged-away or anonymised customer id, orphaning it exactly the way the check was meant to prevent. `RegisterCatCommandHandler.OwnerId` and `TransferCatOwnershipCommandHandler.NewOwnerId` now also require `owner.Status == CustomerAccountStatus.Active`, returning 400 `BusinessViolationCodes.AccountNotActive` — reused from `BusinessViolationCodes.Accounts.cs`, the same code `MergeCustomerCommandHandler` already returns for an identical "record exists, not active" check on its own surviving customer, rather than `CustomerNotFound` (the record does exist) or a new, duplicate code. Four new integration tests in a new file, `tests/CatEcommerce.IntegrationTests/Endpoints/CatOwnerCustomerStatusEndpointTests.cs` (kept separate from other Cat test files to avoid colliding with the TEST gate's own concurrent unknown-id/staff-id additions in the same worktree): register with a `Merged` owner, register with an `Anonymised` owner, transfer to a `Merged` customer, transfer to an `Anonymised` customer — each seeds the customer directly through the repository (`RegisterWalkIn` then `MergeInto`/`Anonymise`) and asserts 400 with `errorCode` `AccountNotActive` on the expected property. <br><br>**Side effect found and fixed:** `DuplicateCustomerGroupsEndpointTests.ReSendingTheSameMerge_ReturnsIsReapplicationTrue_StateUnchanged` (HO-ACC-001, ADR-039's resend-recovery test, I-I3) used `POST /api/cats` to plant a "late" cat directly onto an already-`Merged` customer id, as the simplest way to put a cat in the state `ReassignCatsOnCustomerMergedHandler`'s resend is supposed to sweep up — this fix correctly closes exactly that HTTP path. `ReassignCatsOnCustomerMergedHandler` only ever queries `CatAggregate.OwnerId`, never the owning customer's `Status` (§11.7/ADR-039), so the test's actual intent (a cat whose `OwnerId` is the duplicate, still there when the merge is resent) is unaffected by this fix — only the means of creating that state changes. The test now seeds that cat directly through the repository (a new `SeedCatOwnedByAsync` helper in the same file, the same "bypass the command handler" pattern `AdministrationIntegrationTestBase.SeedCatAsync` already uses) instead of through the now-correctly-guarded HTTP endpoint. Build 0 warnings; full suite run once on `catecommerce_cat` (per this fix's own scope). |
| Amended | **2026-09-27 — HO-CAT-001 ACCEPTANCE gate first pass (FAIL on A22 only; `docs/gates/HO-CAT-001-ACCEPTANCE.md`, `bbd005a`). Three owner decisions, then the fixes they required.** <br><br>**(a) The cage-card QR opens a "smart redirect" page, `/cats/{id}`, on the web site — staff → staff profile, the owner → portal profile, anyone else → sign-in.** The page itself needs HO-ACC-002's session hooks, so it is **not built in this round** — it ships after HO-ACC-002 merges. This is a pending item for whoever picks up that page (also recorded in HO-CAT-002 §1 Amended). The QR **path** (`/cats/{id}`) is unchanged from what §12.3/`GetCatCageCardQueryHandler` already emitted; what changed is that it is now prefixed with an absolute base URL (b) instead of nothing. <br><br>**(b) One shared public web address setting for absolute links, `PublicSiteOptions` (section `PublicSite`, property `WebBaseUrl`), replaces `CatProfileOptions.PublicWebBaseUrl`** (Cat-Profiles-only, never set anywhere, so the QR payload was always a bare relative path — a QR code with no host does not resolve on a scanning phone, A22). Validated at real host startup by `PublicSiteStartupGuardHostedService` (`CatProfilesModule.cs`) — required and https in Production, may be empty in Development/Testing, and whenever present must be an absolute http(s) URL with no path/query/fragment. `appsettings.json` leaves it empty (Production must supply it); `appsettings.Development.json.example` defaults it to `http://localhost:3000`. Notifications will switch to it once both areas have merged — an **integrator task**, not done here. <br><br>**(c) The cat list read model (Q2) now carries `handlingNote`, alongside `isHighRisk`** — §4.1 (and HO-CAT-002 §3/§11.4) require both on the list, not only the profile; §12.3's Q2 row previously specified `isHighRisk` alone and is corrected above. `CatListItemViewModel`/`GetCatsQueryHandler` updated; `web/lib/api/cats.ts`'s `CatListItem` and the mock handler's `toListItem` carry it too; the staff cat list page passes the real value into `HighRiskBadge` instead of a hardcoded `null` (the compact variant still does not render the note text — HO-CAT-002 §11.4's own, unchanged, deliberate design). <br><br>**Work items, same pass:** <br>- **[A22 test]** I21 previously only asserted the response envelope (`200`/`image/png`/`no-store`/non-empty) — a new test decodes the actual PNG (`QrPngDecoder.cs`, a small BCL-only decoder scoped to exactly QRCoder's fixed 1-bit-grayscale output, **not** a general-purpose image library — every one evaluated, `SixLabors.ImageSharp`, carries the "Six Labors Split License", the same class of condition that needed an explicit owner decision for QuestPDF §20.6; avoided rather than requested here) and asserts the exact absolute URL, using `ZXing.Net` (Apache-2.0) and a new `CatApiFactory.WithPublicSiteWebBaseUrl` test-factory helper. Both packages are test-project-only (`tests/CatEcommerce.IntegrationTests.csproj`): `ZXing.Net`. <br>- **[Probes → tests]** Six of the gate's own scratch probes (P1, P3–P7), which the gate wrote itself because the TC sheet's cited evidence didn't actually assert what each row claimed (A1, A8, A9, A11, A17, A24), are now real tests in new `tests/CatEcommerce.IntegrationTests/Endpoints/CatAcceptanceGateEndpointTests.cs` — including a **deterministic** concurrent-409 test (P1/A24) that forces the race with a held Postgres row lock, the first test in the repo to ever observe a real `409` rather than only the black-box "never `500`" outcome set. <br>- **[TC sheet]** `docs/test-cases/TC-HO-CAT-001.md` rows I21, I14, A1, A8, A9, A11, A17, A24 corrected to cite the new tests and state in-line what gap each one closes. |
| Amended | **2026-09-27 — HO-CAT-001 CODE_REVIEW fifth-pass fix (PASSED with 1 new MEDIUM, `docs/gates/HO-CAT-001-CODE_REVIEW.md`).** `PublicSiteStartupGuardHostedService` (`CatProfilesModule.cs`, HO-CAT-001 §1 Amended, same date, item (b)) rejected a path/query/fragment but accepted a URL carrying **userinfo** — `Uri.TryCreate` parses `https://evil.example@trusted.example` as host `trusted.example` with `UserInfo` `evil.example`, which `Uri` considers well-formed but a browser (and most humans) read as the `evil.example` domain, so a QR built from that value would be phishing-shaped. The guard now also rejects any non-empty `Uri.UserInfo`, with the same message shape as the existing path/query/fragment check. Nine new tests in `tests/CatEcommerce.IntegrationTests/Endpoints/HostCompositionTests.cs` (previously zero coverage for this guard's own structural rules) cover the new userinfo case alongside the pre-existing but untested cases: not-absolute, wrong scheme, path, query, fragment (one theory), a well-formed value succeeding (the theory's own positive control), and the two Production-only rules (https required, non-empty required) each get their own test. Build 0 warnings; `HostCompositionTests` (13/13) and the cage-card tests re-run green. |
| Amended | **2026-09-27 — by [`HO-FIL-001`](HO-FIL-001.md), integrator-authorised (closes the carried `HO-CAT-002-SECURITY` item: `POST /api/cats/{id}/photos` trusted a client-supplied `StorageKey`/`ContentType`/`SizeInBytes`/`FileName`).** The photo contract this slice owns changes in three places, all under HO-FIL-001's own §1 Amended authorisation, not a decision made here: (1) `AddCatPhotoRequest`/`AddCatPhotoCommand` now carry only a server-issued `FileId` (minted by `POST /api/staff/files?purpose=CatProfilePhoto` or `POST /api/customers/me/files?purpose=CatProfilePhoto`), never a client-supplied storage key, content type or size — `AddCatPhotoCommandHandler` verifies eligibility (purpose, uploader, freshness) against `StoredFileAggregate` before adding the photo, and "one file, one cat" is enforced by a unique database index on `clinic.cat_photo.file_id`, not by state on the file. (2) `CatPhoto`/`clinic.cat_photo` now stores `{Id, FileId, IsPrimary, UploadedOn}` only — `ContentType`/`SizeInBytes`/`FileName`/`StorageKey` are `StoredFileAggregate`'s own state, looked up by `FileId` at read time. Any `cat_photo` row that existed before this migration was deleted, not migrated (see HO-FIL-001's own §1 Amended row) — it pointed at a storage key HO-FIL-001's serving path cannot resolve. (3) `Q1`'s (`GET /api/cats/{catId}`) `photos` shape is now the §12.4 signed-URL contract: `{ id, fileId, url, contentType, sizeInBytes, originalFileName, urlExpiresOn, isPrimary, uploadedOn }`, replacing the earlier `{ id, storageKey, contentType, sizeInBytes, fileName, isPrimary, uploadedOn }` — `url` is a short-lived (`files.signedUrlMinutes`), signed, directly-fetchable link (`GET /api/files/{fileId}/content`), not a key a client resolves itself. §11.6/§12.3/§12.4/§20.4 below describe the pre-HO-FIL-001 shape and are superseded by this row for the photo fields specifically; nothing else in this document changes. The consuming web page (`HO-CAT-002`) has its own, separate Amended row recording the same change from its side; updating the actual page is out of HO-FIL-001's scope and not done by this row. |
| Platform | `Platform.Infrastructure.Core` **1.0.34** — do not downgrade, see §7 |

---

## 2. Objective

Deliver the **Cat Profile** bounded context end to end on the server: aggregates, commands,
queries, persistence and HTTP API. `CatAggregate` is the record every other business line hangs
off — the clinic treats it, boarding gates on its vaccination status, the shop gates prescription
diets against it (`.ai/bounded-contexts.md`). Getting its boundary right matters more than getting
it done quickly.

This is also the **first slice with any code in it**. `src/` is a scaffold: `Program.cs` is TODO
comments, there is no `DbContext`, no repository, no aggregate. The foundation in §12.1 is
therefore part of this handover, and it is load-bearing for every slice after it.

---

## 3. In scope

### 3.1 Stories

All eleven stories in backlog §2. Each is listed with the server-side capability that satisfies it;
**§18 holds the testable acceptance criteria**.

| Story | Pri | Capability delivered here |
|---|---|---|
| US-CAT-01 | M | Register a cat; name, species, sex, DOB (exact or estimated), breed, colour; photo references with type/size validation |
| US-CAT-02 | M | Neuter status with date, weight recorded to an append-only history series |
| US-CAT-03 | M | Allergies, chronic conditions, current medications, dietary and behavioural notes; all exposed on the profile read model |
| US-CAT-04 | M | High-risk flag with handling note; set and clear both audited with actor and reason |
| US-CAT-05 | M | Vaccination records with vaccine, batch, date, administering vet; next-due computed from the catalogue; expired batch blocked |
| US-CAT-06 | M | Deworming / parasite control with next-due computed from the configured interval |
| US-CAT-07 | M | Vaccination status — `Current` / `DueSoon` / `Overdue` — computed read-side against the core vaccine catalogue |
| US-CAT-08 | S | Ownership transfer preserving history, with an ownership-change audit entry and owner-scoped reads |
| US-CAT-09 | M | Mark deceased with date; excluded from booking/ordering reads; record retained and still readable |
| US-CAT-10 | S | Medical history PDF over the records that exist today (§4.2 names what is not yet available to put in it) |
| US-CAT-11 | C | Printable cage card with a QR code resolving to the cat's profile |

### 3.2 Foundation (first code in the solution)

`Enumeration` base class · `BusinessViolationCodes` · `CatEcommerceDbContext` ·
`EfAggregateRootRepository<T>` · `DevUserContextProvider` · `Program.cs` composition ·
`appsettings` · the initial EF migration. Specified in §12.1.

---

## 4. Out of scope

Building anything in this section is a **FAIL** at the acceptance gate — it means scope was
expanded without approval.

### 4.1 Frontend — all of it

This slice is **backend only**, by explicit instruction. No Razor, no SPA, no view rendering.
The user interface for these same eleven stories is [`HO-CAT-002`](HO-CAT-002.md).

Four acceptance criteria in backlog §2 are written as UI behaviour. They are satisfied **at the API
contract level** here — the endpoint returns the data the screen needs — and the rendering is a
later frontend handover. §18 states each one in the form this slice can actually be judged on:

| Story AC | UI half — deferred | Server half — delivered here, judged in §18 |
|---|---|---|
| US-CAT-03 | Allergies shown in a persistent clinical header | `GET /api/cats/{catId}` returns `allergies[]` and `hasRecordedAllergies` on every profile read |
| US-CAT-03 | Header reads "none recorded" rather than blank | `hasRecordedAllergies: false` with `allergies: []` — the distinction is explicit in the contract, not inferred from an empty array |
| US-CAT-04 | Warning badge on clinic / boarding / order screens | `isHighRisk` + `handlingNote` on the profile **and** list read models, so no screen has to fetch extra |
| US-CAT-07 | "Due soon" shows a booking link | Status payload carries `nextDueOn` and `bookingEligible`; link construction is the frontend's |

### 4.2 Not buildable yet — named gaps, not silent omissions

| Gap | Why | Consequence for this slice |
|---|---|---|
| Owner identity / accounts | §1 Accounts is unbuilt; there is no `CustomerAggregate` | `OwnerId` is an unvalidated `Guid`. **Do not** add a Customer aggregate to validate it |
| Authorization | ADR-009's policy table needs §1 roles. Host runs `enableAuthorization: false` | No `[Authorize]`, no role checks. US-CAT-08's "previous owner can no longer view" ships as an owner-scoped **query filter**; see §20.3 |
| Photo binary upload | Object storage is not provisioned (NFR-SEC-5 wants signed expiring URLs + virus scan) | The API accepts a **validated storage key + content type + size**. No `IFormFile`, no bytes in a command, no filesystem writes |
| Reminder queueing (US-CAT-06) | No Notification context (§13), no outbox table | Next-due dates are computed, stored and indexed; the domain event is raised. Nothing subscribes yet — §20.4 |
| Reminder cancellation (US-CAT-09) | Same | `CatMarkedDeceasedEvent` is raised; no handler cancels anything yet |
| PDF clinical content | Visit (§4), Prescription (§6) and lab results are unbuilt | The PDF carries profile, vaccinations, parasite treatments and weight history. Diagnoses, prescriptions and lab results are **omitted with a printed note**, not faked — §20.5 |
| Audit log query/export | FR-ADM-6 belongs to §14 Administration | Audit entries are **written** (US-CAT-04, US-CAT-08 need that). No read endpoint |

### 4.3 Explicitly forbidden shapes

No `IBusMessageDispatcher`, `IEventPublisher`, `IDispatcher`, MassTransit, RabbitMQ, MongoDB, a
second host, an `Application/CommandServices` layer, a rejection event, or a `202 Accepted`.

---

## 5. Prerequisites

Nothing to wait for. The solution builds clean at `5b260df`; every project referenced below exists
and is empty. **Every prerequisite is available** — verified 2026-09-20, so nothing in §16 starts
life `BLOCKED`.

| Required | State |
|---|---|
| `Platform.Infrastructure.Core` 1.0.34 | ✅ on the feed, referenced by all six projects |
| `dotnet-ef` 9.0.0 | ✅ pinned in `.config/dotnet-tools.json`. Invoke as `dotnet dotnet-ef` — the global 10.0.0 cannot run on the installed runtimes |
| PostgreSQL | ✅ **PostgreSQL 17.5**, native Windows build, reachable at `localhost:5432` as `postgres` / `postgres`. Connection verified with Npgsql |
| Docker | ✅ Engine 29.5.3 running — but **not required**. Nothing in this slice uses a container (§5.1) |

### 5.1 One database, used for everything

**Development and the integration tests both use the local PostgreSQL 17.5 on `localhost:5432`,
database `catecommerce_dev`.** Decided by the business owner. Docker is running and
`Testcontainers.PostgreSql` is referenced by the test project, but **neither is used** — remove the
package reference (§13), because an unused package reference is an architecture-gate finding.

**The Postgres you connect to is not a container.** There *is* a `postgres:16.0-bookworm` container
running (`shahjahan-db-1`), but its port is **exposed, not published** — `5432/tcp` with no host
mapping — so it is unreachable from the host and belongs to an unrelated project. The instance
answering `localhost:5432` is a native Windows PostgreSQL **17.5**. Do not reconfigure that
container and do not point anything at it.

**The consequence you have to design around: the test database is not empty and is not yours
alone.** It holds whatever development left behind, and the suite does not get a clean slate
between runs. §16.1 is not optional advice — it is what stops this suite becoming intermittent.

---

## 6. Governing architecture

Read these before writing code. Where any of them disagrees with a `.ai/templates/*.cs` file, **the
markdown wins** — the templates are knowingly stale and describe the superseded architecture.

| Document | What you need from it |
|---|---|
| [`.ai/architecture.md`](../../.ai/architecture.md) | Layer responsibilities; §6 verified platform surface; **§6.2 the two rules that fail silently** |
| [`.ai/cqrs.md`](../../.ai/cqrs.md) | Why `IInMemoryDispatcher` and nothing else |
| [`.ai/bounded-contexts.md`](../../.ai/bounded-contexts.md) | Aggregate boundary rules — **rule 3 decides where weight history lives** |
| [`.ai/namespace-conventions.md`](../../.ai/namespace-conventions.md) | Every namespace in §11/§12. Do not invent sub-namespaces |
| [`.ai/di-registration-guide.md`](../../.ai/di-registration-guide.md) | What to register, what never to |
| [`.ai/command-handler-pattern.md`](../../.ai/command-handler-pattern.md) | Handler shape; §6 the rule-placement table |
| [`.ai/read-side-pattern.md`](../../.ai/read-side-pattern.md) | Query handler shape; paging; no rehydration |
| [`.ai/persistence-ef-core.md`](../../.ai/persistence-ef-core.md) | §2 the repository (**copy it exactly**), §4 owned mapping, §6 `xmin`, §8 column types |
| [`.ai/domain-events-and-outbox.md`](../../.ai/domain-events-and-outbox.md) | §2 runtime-type dispatch; §5 event vs outbox |
| [`.ai/enumeration-pattern.md`](../../.ai/enumeration-pattern.md) | `Enumeration` usage — but see §7, the base class is **ours** |
| [`.ai/host-composition.md`](../../.ai/host-composition.md) | `Program.cs`; §3 lifetimes; §5 never-register |
| [`.ai/feature-creation-checklist.md`](../../.ai/feature-creation-checklist.md) | File-by-file order |
| [`.ai/decisions/adr.md`](../../.ai/decisions/adr.md) | **ADR-010 … ADR-020.** ADR-019 and ADR-020 are the ones this slice turns on |

---

## 7. Verified platform facts

Established by reflection and by building a real `ServiceProvider` against the restored packages.
**Do not re-derive these, and do not trust older prose that contradicts them.**

| # | Fact | Consequence for your code |
|---|---|---|
| F1 | `AddInMemoryBusServices()` registers exactly `IResolver`, `IHandlerResolver`, `IInMemoryDispatcher`, `ICommandSender`, `IQueryProcessor`, `IValidationService`, `IValidationProvider` | Nothing else exists. No `IEventPublisher`, no `IDispatcher`, no broker type |
| F2 | `IInMemoryDispatcher` has **no** `PublishAsync` (removed 1.0.33) | Commands and queries only. Domain events never go through the dispatcher |
| F3 | **`Command.ValidateCommand` is `bool?`**, and `CommandSender` skips validation when it is null and `ValidationOptions.ValidateAllCommands` is false — the default | **Stamp `ValidateCommand = true` in the controller on every command.** Otherwise your registered `IValidator<T>` silently never runs and invalid data reaches the aggregate |
| F4 | Generic `PublishAsync<TEvent>` binds `TEvent` from the **static** type. `aggregate.Events` is `ReadOnlyCollection<Event>` | Resolve handlers on `domainEvent.GetType()`. The static type is always `Event` and finds nothing |
| F5 | `Enumeration` ships in **no** `Platform.Infrastructure.*` package | You must write the base class. §11.1, ADR-022 |
| F6 | `AggregateRoot : Entity` (`Platform.Infrastructure.Domain`) supplies `Id`, `Version`, `IsMarkedToDelete`, `CreatedBy/Date`, `LastUpdatedBy/Date`, `TenantId`, `VerticalId`, `Metadata`, `Tags`, `SetDefaultValue(UserContext)`, protected `AddDomainEvent(Event)` / `UpdateVersion()`, public `ClearEvents()` | Do not redeclare any of these |
| F7 | `ValidationResponse.AddError(errorMessage, propertyName, errorCode, resourceName)` — that argument order | Use named arguments |
| F8 | `Query<TResult>` already carries `PageNumber`, `PageSize`, `Skip`, `Take`, `DoCount`, `UserContext`, `CorrelationId`, `Vertical` | **Never redeclare paging** — it shadows the base and silently breaks |
| F9 | `EventPublishOption` lives in `Platform.Infrastructure.Core.Commands`: `DoNotPublish=0`, `InMemoryPublishOnly=1`, `InMemoryAndQueuePublish=2`, `QueuePublishOnly=3` | Required argument on every `SaveAsync`/`UpdateAsync`. Always `InMemoryPublishOnly`. It is **not** MongoDB residue |
| F10 | `Event : BusTopicMessage : Message` supplies `CorrelationId`, `UserContext`, `TimeStamp`, `AggregateRootId`, `AggregateRootVersion` | Do not redeclare |

---

## 8. Domain model

### 8.1 Aggregate boundaries — and the two that are easy to get wrong

Four aggregate roots. **One aggregate is mutated per command**, without exception.

```
CatAggregate  (root)                      clinic.cat
├── CatPhoto              [owned many]    clinic.cat_photo
├── CatAllergy            [owned many]    clinic.cat_allergy
├── CatChronicCondition   [owned many]    clinic.cat_chronic_condition
├── CatMedication         [owned many]    clinic.cat_medication
├── CatVaccination        [owned many]    clinic.cat_vaccination
└── CatParasiteTreatment  [owned many]    clinic.cat_parasite_treatment

CatWeightRecordAggregate  (root)          clinic.cat_weight_record   → references CatId
VaccineAggregate          (root)          clinic.vaccine
AuditEntryAggregate       (root)          audit.audit_entry
```

**Why weight history is its own aggregate and not an owned collection.**
`.ai/bounded-contexts.md` rule 3 and `.ai/persistence-ef-core.md` §4 both name weight history as
**unbounded** and forbid it inside the aggregate. A cat weighed at every visit for eighteen years
would force every unrelated `CatAggregate` load to drag the whole series. So `RecordCatWeight`
creates a `CatWeightRecordAggregate` and **does not touch `CatAggregate` at all** — which also
keeps "one aggregate per command" true. "Current weight" is the latest row, resolved read-side;
it is **not** denormalised onto the cat.

**Why vaccinations *are* owned.** Bounded by a cat's lifetime (tens of rows), and
`RecordVaccination` must enforce "not twice with the same vaccine on the same day" — an invariant
only the aggregate can see. `.ai/persistence-ef-core.md` §4 uses this exact case as its example.

**`VaccineAggregate` is a lookup, never mutated by a cat command.** `RecordCatVaccination` loads it
read-only to obtain `IsCore` and `ValidityMonths`, passes them into the DTO, and the **aggregate**
computes `NextDueOn`. The rule stays in the domain; the catalogue is just data.

### 8.2 `CatAggregate`

`src/Domain/CatEcommerce.Domain/Aggregates/Cat/CatAggregate.cs`
Namespace `CatEcommerce.Domain.Aggregates.Cat`

All setters **private**. Collections exposed as `IReadOnlyList<T>` over a private `List<T>` backing
field — never `private set` on a `List<T>`, which protects the reference but not the contents.

| Property | Type | Notes |
|---|---|---|
| `OwnerId` | `Guid` | FR-CAT-1. Unvalidated — §4.2 |
| `Name` | `string` | ≤ 100, required |
| `Species` | `string` | ≤ 50, defaults `"Cat"` |
| `Sex` | `CatSex` | |
| `DateOfBirth` | `DateOnly?` | `date` column |
| `DateOfBirthPrecision` | `DateOfBirthPrecision` | `Exact` \| `Estimated` — drives US-CAT-01 AC2 |
| `Breed` | `string?` | ≤ 100 |
| `ColourAndMarkings` | `string?` | ≤ 200 |
| `NeuterStatus` | `NeuterStatus` | |
| `NeuteredOn` | `DateOnly?` | Required when `Neutered` |
| `DietaryNotes` | `string?` | ≤ 1000 |
| `BehaviouralNotes` | `string?` | ≤ 1000 |
| `IsHighRisk` | `bool` | FR-CAT-4 |
| `HandlingNote` | `string?` | ≤ 500. Required when `IsHighRisk` |
| `LifeStatus` | `CatLifeStatus` | `Alive` \| `Deceased` |
| `DeceasedOn` | `DateOnly?` | |
| `DeceasedNote` | `string?` | ≤ 500 |
| `Photos` | `IReadOnlyList<CatPhoto>` | max 10, exactly one `IsPrimary` |
| `Allergies` | `IReadOnlyList<CatAllergy>` | |
| `ChronicConditions` | `IReadOnlyList<CatChronicCondition>` | |
| `Medications` | `IReadOnlyList<CatMedication>` | |
| `Vaccinations` | `IReadOnlyList<CatVaccination>` | |
| `ParasiteTreatments` | `IReadOnlyList<CatParasiteTreatment>` | |

**Mutation methods.** Every one ends with `AddDomainEvent(...)` then `UpdateVersion()`.

| Method | DTO | Invariants it enforces | Event raised |
|---|---|---|---|
| `Register` | `CatRegistrationDto` | Name required; DOB not future; DOB required when precision is `Exact`; calls `SetDefaultValue(userContext)` | `CatRegisteredEvent` |
| `UpdateProfile` | `CatProfileUpdateDto` | Same DOB rules; **not deceased** | `CatProfileUpdatedEvent` |
| `RecordNeuterStatus` | `CatNeuterStatusDto` | `NeuteredOn` required when `Neutered`, not future, not before DOB; **not deceased** | `CatNeuterStatusRecordedEvent` |
| `UpdateMedicalProfile` | `CatMedicalProfileDto` | Replaces all three collections + both note fields wholesale; **not deceased** | `CatMedicalProfileUpdatedEvent` |
| `AddPhoto` | `CatPhotoDto` | Content type allowed; size ≤ limit; ≤ 10 photos; first photo becomes primary; **not deceased** | `CatPhotoAddedEvent` |
| `FlagHighRisk` | `CatHighRiskFlagDto` | Handling note required; reason required | `CatFlaggedHighRiskEvent` |
| `ClearHighRiskFlag` | `CatHighRiskClearDto` | Must currently be flagged; reason required | `CatHighRiskFlagClearedEvent` |
| `RecordVaccination` | `CatVaccinationDto` | `AdministeredOn` not future, not before DOB; **`BatchExpiresOn >= AdministeredOn`** else `ExpiredVaccineBatch`; not the same vaccine on the same day; computes `NextDueOn = AdministeredOn.AddMonths(dto.ValidityMonths)`; **not deceased** | `CatVaccinationRecordedEvent` |
| `RecordParasiteTreatment` | `CatParasiteTreatmentDto` | `AdministeredOn` not future; computes `NextDueOn = AdministeredOn.AddDays(dto.IntervalDays)`; **not deceased** | `CatParasiteTreatmentRecordedEvent` |
| `MarkDeceased` | `CatDeceasedDto` | Not already deceased; `DeceasedOn` not future, not before DOB | `CatMarkedDeceasedEvent` |
| `TransferOwnership` | `CatOwnershipTransferDto` | New owner differs from current; **not deceased**; reason required | `CatOwnershipTransferredEvent` |

> **"Not deceased" is one private guard**, `EnsureAlive()`, throwing
> `DomainRuleViolationException(BusinessViolationCodes.CatIsDeceased, …)`. A deceased cat's record
> is read-only (FR-CAT-9): retained and viewable, never edited.

### 8.3 Owned child entities

`src/Domain/CatEcommerce.Domain/Entities/` · namespace `CatEcommerce.Domain.Entities`

| Entity | Properties |
|---|---|
| `CatPhoto` | `Id`, `StorageKey` (≤500), `ContentType` (≤100), `SizeInBytes` (`long`), `FileName` (≤255), `IsPrimary`, `UploadedOn` (`DateTimeOffset`) |
| `CatAllergy` | `Id`, `Allergen` (≤200), `Reaction` (≤500), `Severity` (`AllergySeverity`), `RecordedOn` (`DateOnly`) |
| `CatChronicCondition` | `Id`, `Condition` (≤200), `DiagnosedOn` (`DateOnly?`), `Notes` (≤1000) |
| `CatMedication` | `Id`, `MedicationName` (≤200), `Dosage` (≤100), `Frequency` (≤100), `StartedOn` (`DateOnly`), `EndsOn` (`DateOnly?`) |
| `CatVaccination` | `Id`, `VaccineId`, `VaccineCode` (≤50), `VaccineName` (≤200), `IsCore` (**snapshot at recording**), `BatchReference` (≤100), `BatchExpiresOn` (`DateOnly`), `AdministeredOn` (`DateOnly`), `AdministeredByVetId`, `NextDueOn` (`DateOnly`), `Notes` (≤500) |
| `CatParasiteTreatment` | `Id`, `TreatmentType` (`ParasiteTreatmentType`), `ProductName` (≤200), `BatchReference` (≤100), `AdministeredOn` (`DateOnly`), `AdministeredByStaffId`, `NextDueOn` (`DateOnly`), `Notes` (≤500) |

> `CatVaccination.IsCore` and `VaccineName` are **snapshots taken when the vaccination was
> recorded**, not lookups. A vaccine later reclassified must not retroactively change what a
> historical clinical record says. The read side in §12.3 joins the *current* catalogue for
> "which core vaccines are missing", and uses the snapshot for "what was given".

### 8.4 Other aggregates

**`CatWeightRecordAggregate`** — `Aggregates/Cat/`, namespace `CatEcommerce.Domain.Aggregates.Cat`
`CatId`, `Weight` (owned `Weight` VO), `RecordedOn` (`DateOnly`), `RecordedByStaffId`, `Notes` (≤500).
Method `Record(CatWeightRecordDto)` → `CatWeightRecordedEvent`. Invariants: `RecordedOn` not future;
weight within `0.10 … 30.00` kg.

**`VaccineAggregate`** — `Aggregates/Vaccination/`, namespace `CatEcommerce.Domain.Aggregates.Vaccination`
`Code` (≤50, unique), `Name` (≤200), `IsCore`, `ValidityMonths` (`int`), `IsActive`, `Description` (≤500).
Method `Register(VaccineRegistrationDto)` → `VaccineRegisteredEvent`. Invariant: `ValidityMonths` in `1 … 120`.

**`AuditEntryAggregate`** — `Aggregates/Audit/`, namespace `CatEcommerce.Domain.Aggregates.Audit`
`EntityType` (≤100), `EntityId`, `Action` (≤100), `Reason` (≤1000), `ActorUserId`,
`OccurredOn` (`DateTimeOffset`), `BeforeValue` (≤2000), `AfterValue` (≤2000).
Method `Record(AuditEntryDto)`. **Raises no domain event** — it is a leaf; an event would be an
audit of an audit. Append-only: no update or delete path exists (SRS D-5).

### 8.5 Value object

`Weight` — `ValueObjects/`, namespace `CatEcommerce.Domain.ValueObjects`.
`public sealed record Weight(decimal Kilograms)` with a `Create` factory enforcing the range.
Mapped `OwnsOne` → `weight_kg numeric(5,2)`.

### 8.6 Domain exception

`Exceptions/DomainRuleViolationException.cs`, namespace `CatEcommerce.Domain.Exceptions`

```csharp
public sealed class DomainRuleViolationException(string errorCode, string message, string propertyName)
    : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
    public string PropertyName { get; } = propertyName;
}
```

**This is how an aggregate invariant becomes a `400`, not a `500`.** The aggregate throws; the
command handler catches it and returns `CommandResponse { IsSuccess = false, ValidationResult }`.
The invariant stays in the domain (where the architecture gate requires it) *and* the caller gets a
usable error (where ADR-014 requires it). §11.5 has the handler shape.

### 8.7 No domain service

Nothing here spans aggregates. The one cross-entity lookup in this slice — vaccine code
uniqueness in `RegisterVaccineCommandHandler` — is a repository call in the handler, which
`.ai/command-handler-pattern.md` §5 explicitly classifies as *not* domain logic. **Do not create a
service that wraps a single repository call** — that is ADR-015 and a HIGH finding.

> `.ai/command-handler-pattern.md` §4 and §6 and `.ai/persistence-ef-core.md` §4/§7 use a cat's
> **microchip** as their worked example of a uniqueness check and a partial unique index. Those
> examples are stale as of SRS v2.0 — the field does not exist. The pattern they teach still
> applies; read `Code` on `VaccineAggregate` wherever they say `MicrochipNumber`.

---

## 9. ADRs to record during this build

Append to `.ai/decisions/adr.md` as part of the implementation commit.

| ADR | Decision | Why it must be recorded |
|---|---|---|
| **ADR-021** | `SaveChangesAsync` fires **per repository call**, not once per request behind a commit filter | `.ai/host-composition.md` §4 leaves this open "for the first slice". This is that slice. It is also what makes the audit write in §11.7 commit |
| **ADR-022** | `Enumeration` is defined in `Shared`, not supplied by the platform | `.ai/enumeration-pattern.md` claims the platform provides it. It does not (F5) |
| **ADR-023** | Weight history is a separate aggregate; current weight is derived read-side, never denormalised onto `CatAggregate` | The obvious "just cache it on the cat" change would violate `.ai/bounded-contexts.md` rule 3. Record the reasoning so it is not undone later |

---

## 10. Enumerations and violation codes

`src/Shared/CatEcommerce.Shared/Common/Enums/` · namespace `CatEcommerce.Shared.Common.Enums`
Values start at **1**. Primary constructor `(int value, string displayName)`, `nameof()` for the
display name, `static readonly` members. Persisted **by `Value`**, never `DisplayName`.

| Type | Members |
|---|---|
| `CatSex` | `Male`, `Female`, `Unknown` |
| `DateOfBirthPrecision` | `Exact`, `Estimated` |
| `NeuterStatus` | `Intact`, `Neutered`, `Unknown` |
| `CatLifeStatus` | `Alive`, `Deceased` |
| `AllergySeverity` | `Mild`, `Moderate`, `Severe` |
| `ParasiteTreatmentType` | `Deworming`, `FleaAndTick`, `Combined` |
| `VaccinationStatus` | `Current`, `DueSoon`, `Overdue` |

`Common/Constants/BusinessViolationCodes.cs` — `public const string X = nameof(X);`

`CatNotFound` · `VaccineNotFound` · `VaccineCodeAlreadyExists` ·
`ExpiredVaccineBatch` · `DuplicateVaccinationOnDate` · `CatIsDeceased` · `CatAlreadyDeceased` ·
`CatNotFlaggedHighRisk` · `OwnerUnchanged` · `PhotoLimitExceeded` · `InvalidPhotoContentType` ·
`PhotoTooLarge` · `InactiveVaccine`

`Common/Constants/CatPhotoRules.cs` — `MaxSizeInBytes = 5 * 1024 * 1024`;
`AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"]`; `MaxPhotosPerCat = 10`.

---

## 11. Specified changes — write side

### 11.1 `Shared`

`Common/Enums/Enumeration.cs` — the base class (F5). Must provide: `Value`, `DisplayName`,
`GetAll<T>()`, `FromValue<T>(int)`, `FromDisplayName<T>(string)`, `Equals`/`GetHashCode`,
`CompareTo`, `==`/`!=`, `ToString()` → `DisplayName`. Plus the enums and constants in §10.

### 11.2 Commands — `src/Application/.../Commands/[Feature]/`

Namespace `CatEcommerce.Application.Commands.Cat` (and `.Vaccination`). Extend
`Platform.Infrastructure.Core.Commands.Command`. `required` / `init`. **Data only.**

| # | Command | Payload |
|---|---|---|
| C1 | `RegisterCatCommand` | `CatId`, `OwnerId`, `Name`, `Species`, `Sex`, `DateOfBirth?`, `DateOfBirthPrecision`, `Breed?`, `ColourAndMarkings?` |
| C2 | `UpdateCatProfileCommand` | `CatId`, `Name`, `Sex`, `DateOfBirth?`, `DateOfBirthPrecision`, `Breed?`, `ColourAndMarkings?` |
| C3 | `RecordCatNeuterStatusCommand` | `CatId`, `NeuterStatus`, `NeuteredOn?` |
| C4 | `RecordCatWeightCommand` | `WeightRecordId`, `CatId`, `Kilograms`, `RecordedOn`, `RecordedByStaffId`, `Notes?` |
| C5 | `UpdateCatMedicalProfileCommand` | `CatId`, `Allergies[]`, `ChronicConditions[]`, `Medications[]`, `DietaryNotes?`, `BehaviouralNotes?` |
| C6 | `AddCatPhotoCommand` | `CatId`, `PhotoId`, `StorageKey`, `ContentType`, `SizeInBytes`, `FileName`, `IsPrimary` |
| C7 | `FlagCatHighRiskCommand` | `CatId`, `HandlingNote`, `Reason` |
| C8 | `ClearCatHighRiskFlagCommand` | `CatId`, `Reason` |
| C9 | `RecordCatVaccinationCommand` | `CatId`, `VaccinationId`, `VaccineId`, `BatchReference`, `BatchExpiresOn`, `AdministeredOn`, `AdministeredByVetId`, `Notes?` |
| C10 | `RecordCatParasiteTreatmentCommand` | `CatId`, `TreatmentId`, `TreatmentType`, `ProductName`, `BatchReference?`, `AdministeredOn`, `AdministeredByStaffId`, `IntervalDays`, `Notes?` |
| C11 | `MarkCatDeceasedCommand` | `CatId`, `DeceasedOn`, `Note?` |
| C12 | `TransferCatOwnershipCommand` | `CatId`, `NewOwnerId`, `Reason` |
| C13 | `RegisterVaccineCommand` | `VaccineId`, `Code`, `Name`, `IsCore`, `ValidityMonths`, `Description?` |

The nested payloads in C5 are `SharedDto` records in
`CatEcommerce.Shared.SharedDto.Cat` — `AllergyInput`, `ChronicConditionInput`, `MedicationInput`.

### 11.3 Validators — `CommandHandlers/Validators/` (**flat**)

One per command, `AbstractValidator<TCommand>`, namespace
`CatEcommerce.Application.CommandHandlers.Validators`. **Shape only** — nothing that needs a
database.

Must cover, at minimum:
- every id `NotEmpty()`; every required string `NotEmpty()` + `MaximumLength(...)` per §8.2
- `RegisterCatCommandValidator` — `DateOfBirth` not future; required when precision is `Exact`
- `RecordCatNeuterStatusCommandValidator` — `NeuteredOn` required when `NeuterStatus == Neutered`, and not in the future
- `AddCatPhotoCommandValidator` — **`ContentType` in `CatPhotoRules.AllowedContentTypes`;
  `SizeInBytes` in `1 … MaxSizeInBytes`.** This is what satisfies US-CAT-01 AC3
- `RecordCatWeightCommandValidator` — `Kilograms` in `0.10 … 30.00`; `RecordedOn` not future
- `RecordCatVaccinationCommandValidator` — `AdministeredOn` not future
- `RecordCatParasiteTreatmentCommandValidator` — `IntervalDays` in `1 … 730`
- `RegisterVaccineCommandValidator` — `ValidityMonths` in `1 … 120`
- `FlagCatHighRiskCommandValidator` / `ClearCatHighRiskFlagCommandValidator` — `Reason` required

> Registered as **`IValidator<TCommand>`**. Registering the concrete type or `AbstractValidator<T>`
> means it is never resolved and never runs — `.ai/di-registration-guide.md` §3.

### 11.4 Data mappers — `DataMappers/` (**flat**)

`CatMapper` and `VaccineMapper`, static classes of `ToDomainDto()` extension methods, namespace
`CatEcommerce.Application.DataMappers`. **Always pass `command.UserContext` and
`command.CorrelationId` through** — that is how the audit trail attributes a change (FR-ACC-12).

### 11.5 Command handlers — `CommandHandlers/[Feature]/`

Implement `ICommandHandlerAsync<TCommand>` directly. **Primary constructor.** Inject
`ILogger<T>` and `IAggregateRootRepository<TAggregate>` — nothing else.

Canonical shape, and the only place the `DomainRuleViolationException` catch belongs:

```csharp
public sealed class RecordCatVaccinationCommandHandler(
    ILogger<RecordCatVaccinationCommandHandler> logger,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<VaccineAggregate> vaccineRepository)
    : ICommandHandlerAsync<RecordCatVaccinationCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordCatVaccinationCommand command)
    {
        var cat = await catRepository.GetByIdAsync(command.CatId);
        if (cat is null)
            return Violation(BusinessViolationCodes.CatNotFound,
                             nameof(command.CatId),
                             $"Cat '{command.CatId}' was not found.");

        // VaccineAggregate is a read-only lookup. It is NOT mutated — one aggregate per command.
        var vaccine = await vaccineRepository.GetByIdAsync(command.VaccineId);
        if (vaccine is null)
            return Violation(BusinessViolationCodes.VaccineNotFound, nameof(command.VaccineId), …);
        if (!vaccine.IsActive)
            return Violation(BusinessViolationCodes.InactiveVaccine, nameof(command.VaccineId), …);

        try
        {
            // IsCore + ValidityMonths travel in the DTO; the AGGREGATE computes NextDueOn.
            cat.RecordVaccination(command.ToDomainDto(vaccine));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await catRepository.UpdateAsync(cat, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true, Result = cat.Id };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property,
                                    errorCode: code, resourceName: nameof(CatAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
```

Handler-specific notes:

| Handler | Extra |
|---|---|
| `RegisterCatCommandHandler` | `new CatAggregate()` → `Register(...)` → **`SaveAsync`** (not `UpdateAsync`) |
| `RecordCatWeightCommandHandler` | Load `CatAggregate` **only to confirm it exists**, then create and `SaveAsync` a `CatWeightRecordAggregate`. The cat is not mutated and not saved |
| `RegisterVaccineCommandHandler` | `ExistAsync(v => v.Code == command.Code)` → `VaccineCodeAlreadyExists` |
| `TransferCatOwnershipCommandHandler` | Compare against current owner → `OwnerUnchanged` |
| `RecordCatParasiteTreatmentCommandHandler` | After `cat.RecordParasiteTreatment(...)`, select the created entry by the **controller-minted id**: `var treatment = cat.ParasiteTreatments.Single(t => t.Id == command.TreatmentId);` (C10's command already carries `TreatmentId`, §11.2 line 406 — the same minted-by-the-controller pattern as every other id in this handover, not list order). `Result = new RecordCatParasiteTreatmentResult(treatment.Id, treatment.NextDueOn)` — **not** `cat.Id` — so the id and the aggregate-computed `NextDueOn` travel to the controller (owner decision 2026-09-25, §12.4) |

`RecordCatParasiteTreatmentResult(Guid TreatmentId, DateOnly NextDueOn)` is a `sealed record`
next to `RecordCatParasiteTreatmentCommand` in `Commands/Cat/` — the one `CommandResponse.Result`
shape in this handover that is not a single id, because C10 has no read endpoint for the API layer
to re-fetch `NextDueOn` from afterwards (owner decision 2026-09-25). `NextDueOn` is **required**,
not nullable (owner confirmed 2026-09-25) — `IntervalDays` is always validated (§11.3), so the
aggregate always computes a value, matching `CatParasiteTreatment.NextDueOn`'s own non-null
`DateOnly` (§8.3).

### 11.6 Domain events — `Events/[Feature]/`

`sealed class … : Event`, `init` properties, **past tense**. Namespace
`CatEcommerce.Domain.Events.Cat` / `.Vaccination`.

`CatRegisteredEvent` · `CatProfileUpdatedEvent` · `CatNeuterStatusRecordedEvent` ·
`CatMedicalProfileUpdatedEvent` · `CatPhotoAddedEvent` · `CatFlaggedHighRiskEvent` ·
`CatHighRiskFlagClearedEvent` · `CatVaccinationRecordedEvent` ·
`CatParasiteTreatmentRecordedEvent` · `CatMarkedDeceasedEvent` · `CatOwnershipTransferredEvent` ·
`CatWeightRecordedEvent` · `VaccineRegisteredEvent`

The three audited events **must** carry the actor and the reason:
`CatFlaggedHighRiskEvent { CatId, HandlingNote, Reason }`,
`CatHighRiskFlagClearedEvent { CatId, Reason }`,
`CatOwnershipTransferredEvent { CatId, PreviousOwnerId, NewOwnerId, Reason }`.
The actor comes from the inherited `UserContext`.

### 11.7 Domain event handlers — `DomainEventHandlers/Cat/`

Namespace `CatEcommerce.Application.DomainEventHandlers.Cat`. These satisfy **US-CAT-04 AC2** and
**US-CAT-08 AC1**.

| Handler | Handles | Does |
|---|---|---|
| `AuditCatHighRiskFlaggedHandler` | `CatFlaggedHighRiskEvent` | Writes an `AuditEntryAggregate` — action `CatFlaggedHighRisk`, actor from `UserContext`, the reason, before/after `IsHighRisk` |
| `AuditCatHighRiskFlagClearedHandler` | `CatHighRiskFlagClearedEvent` | Same, action `CatHighRiskFlagCleared` |
| `AuditCatOwnershipTransferredHandler` | `CatOwnershipTransferredEvent` | Same, action `CatOwnershipTransferred`, before/after owner ids |

Each injects `IAggregateRootRepository<AuditEntryAggregate>` and calls
`SaveAsync(entry, EventPublishOption.DoNotPublish)`.

> **Never call `SaveChangesAsync` in an event handler** — that is a BLOCKER. Going through the
> repository is what keeps this legal, and ADR-021 (per-call save) is what makes it commit.
>
> Each handler carries a summary comment stating that **a failure here fails the command after the
> cat has already been committed**, per `.ai/domain-events-and-outbox.md` §3. That is the intended
> behaviour: an unaudited flag change is not acceptable (SRS C-9), so it should fail loudly.

---

## 12. Specified changes — read side, Infrastructure, host, Api

### 12.1 Foundation — `Infrastructure`

**`Persistence/CatEcommerceDbContext.cs`** — `DbSet`s for `Cats`, `CatWeightRecords`, `Vaccines`,
`AuditEntries`; `OnModelCreating` → `ApplyConfigurationsFromAssembly`.

**`Repositories/EfAggregateRootRepository.cs`** — copy `.ai/persistence-ef-core.md` §2 **exactly**.
Two details that are the whole point of that section:
- `ExistAsync` is `.Where(a => !a.IsMarkedToDelete).AnyAsync(filter)`. Compiling the expression
  inside the predicate cannot be translated to SQL and throws at runtime.
- Event dispatch resolves `IEventHandlerAsync<>` on `domainEvent.GetType()` (F4).

**`Hosting/DevUserContextProvider.cs`** — fixed `UserId` / `TenantId` from configuration. The one
seam the platform needs (`.ai/architecture.md` §6.1).

**`Configuration/CatProfileOptions.cs`** — namespace `CatEcommerce.Infrastructure.Configuration`.
`VaccinationDueSoonWindowDays` (default **30**, FR-CAT-7), `DefaultParasiteIntervalDays`.
Bound from `appsettings.json` section `CatProfile`.

**`Persistence/Configurations/`** — `CatConfiguration`, `CatWeightRecordConfiguration`,
`VaccineConfiguration`, `AuditEntryConfiguration`.

Per `.ai/persistence-ef-core.md` §4/§6/§7/§8, every aggregate configuration must:
- `ToTable(name, schema)` — `clinic` for cat/vaccine, `audit` for audit entries
- map children with **`OwnsMany`** (never `HasMany` — that contradicts the boundary)
- `SetPropertyAccessMode(PropertyAccessMode.Field)` on each owned navigation
- `builder.Property<uint>("xmin").IsRowVersion()`
- `builder.Ignore(x => x.Events)`
- convert every `Enumeration` by `Value`
- `DateOnly` → `date`; `DateTimeOffset` → `timestamptz` (C-7); money would be `numeric(19,4)` (C-6)

Indexes — every one of these is filtered on by a query in §12.3:

| Index | Why |
|---|---|
| `cat.owner_id` | Owner's cat list |
| `cat.life_status` | Excluding deceased from booking/ordering reads |
| `cat_vaccination.next_due_on` | US-CAT-07, the due/overdue report |
| `cat_weight_record (cat_id, recorded_on)` | Weight series |
| `vaccine.code` **UNIQUE**, and `is_core` | Catalogue lookup + core-set query |
| `audit_entry (entity_type, entity_id, occurred_on)` | FR-ADM-6 later |

**Migration** — `dotnet dotnet-ef migrations add AddCatProfile …`. **Read the generated file before
committing**; EF emits `DROP COLUMN` for renames.

### 12.2 Documents — `Infrastructure/Documents/`

| Interface | Implementation | Used by |
|---|---|---|
| `ICatMedicalHistoryPdfRenderer` | `QuestPdfCatMedicalHistoryRenderer` | US-CAT-10 |
| `ICatQrCodeGenerator` | `QrCoderCatQrCodeGenerator` | US-CAT-11 |

Both take a ViewModel and return `byte[]`. Behind interfaces so the API layer never references a
rendering library, and so the renderer can be swapped without touching a controller.

New NuGet packages — **permitted by this handover, and only these two**:
`QuestPDF` and `QRCoder`, both in `CatEcommerce.Infrastructure` only. See §20.6 on QuestPDF
licensing before merging.

### 12.3 Read side — `src/Read/`

ViewModels are `sealed record`s with `init` properties, namespace `CatEcommerce.Read.ViewModels`
(**flat**). Query handlers inject `ILogger` + `CatEcommerceDbContext`, use `AsNoTracking()`, and
`Select` into the ViewModel **before** `ToListAsync`. A query never writes.

| # | Query → ViewModel | Endpoint | Notes |
|---|---|---|---|
| Q1 | `GetCatProfileQuery` → `CatProfileViewModel?` | `GET /api/cats/{catId}` | Full profile: identity, identification, `isHighRisk` + `handlingNote`, allergies/conditions/medications, photos, `latestWeightKg`, `hasRecordedAllergies` (§4.1). **Amended 2026-09-26**: handler now projects with `Select` (two-phase, matching Q2), rather than rehydrating `CatAggregate` — same output, no aggregate loaded |
| Q2 | `GetCatsQuery` → `PagedResult<CatListItemViewModel>` | `GET /api/cats` | Filter `OwnerId?`, `IncludeDeceased` (**default false**, US-CAT-09). **Paginated** (UI-4) — use the base `Skip`/`Take`, never redeclare (F8). Carries `isHighRisk` **and `handlingNote`** so no list screen needs a second call for the warning badge (§4.1/HO-CAT-002 §3). **Amended 2026-09-26**: returns `PagedResult<T>` (`{ items, pageNumber, pageSize, totalCount }`, `Read/ViewModels/PagedResult.cs` — the same type the Accounts read side uses), not a bare array. **Amended 2026-09-27 (ACCEPTANCE gate A9, owner decision (c))**: `handlingNote` added to `CatListItemViewModel` — this row previously specified `isHighRisk` alone, contradicting §4.1's own requirement for both fields on the list; §4.1 wins, this row is corrected |
| Q3 | `GetCatWeightHistoryQuery` → `PagedResult<CatWeightPointViewModel>` | `GET /api/cats/{catId}/weights` | `(RecordedOn, Kilograms)` ordered ascending — the series behind US-CAT-02 AC2's chart. Paginated. **Amended 2026-09-26**: returns `PagedResult<T>`, not a bare array |
| Q4 | `GetCatVaccinationStatusQuery` → `CatVaccinationStatusViewModel` | `GET /api/cats/{catId}/vaccination-status` | **US-CAT-07.** See below |
| Q5 | `GetCatMedicalHistoryQuery` → `CatMedicalHistoryViewModel` | (feeds Q-PDF) | Profile + vaccinations + parasite treatments + weight history. **Amended 2026-09-26**: handler now projects with `Select` rather than rehydrating `CatAggregate` — same output, the photo collection is never loaded |
| Q6 | `GetVaccinesQuery` → `PagedResult<VaccineListItemViewModel>` | `GET /api/vaccines` | Catalogue; filter `ActiveOnly`, `CoreOnly`. Paginated. **Amended 2026-09-26**: returns `PagedResult<T>`, not a bare array |
| Q7 | `GetCatCageCardQuery` → `CatCageCardViewModel` | (feeds the QR endpoint) | Name, owner, high-risk flag + handling note, profile URL |

**Q4 — the vaccination status rule (US-CAT-07, FR-CAT-7).** Computed entirely read-side; no
aggregate is rehydrated. Inject `IOptions<CatProfileOptions>` for the window.

1. Load the cat's vaccinations (`vaccineId`, `nextDueOn`, snapshot `isCore`).
2. Load the **currently active core** vaccines from the catalogue.
3. For each active core vaccine: find the cat's most recent vaccination of it.
   - none ever → **`Overdue`**, reason `NeverAdministered`
   - `nextDueOn < today` → **`Overdue`**
   - `nextDueOn <= today + windowDays` → **`DueSoon`**
   - otherwise → **`Current`**
4. Overall status is the **worst** across all core vaccines.
5. Return per-vaccine rows plus `overallStatus`, `earliestNextDueOn`, and `bookingEligible`
   (`overallStatus != Overdue` — the server-side fact behind BR-1 and the frontend's booking link).

> Deliberately **not** stored on `CatAggregate`. Status is a function of *today*: a cat drifts
> `Current → DueSoon → Overdue` with no command and no event to trigger a recompute, so a
> persisted column would silently go stale. When §7 Boarding needs this on the **write** path for
> BR-1, it will add an aggregate method taking the core set and `asOf` as parameters — that is
> that handover's work, not this one's.

### 12.4 Api — `src/Api/`

Controllers inject **`IInMemoryDispatcher`** and nothing else. No repository, no `DbContext`, no
aggregate, no handler. Each action: mint the id server-side, stamp `UserContext` / `CorrelationId`
/ **`ValidateCommand = true`** (F3), dispatch **one** command or query, shape the response.

```csharp
[HttpPost]
public async Task<IActionResult> RegisterCat([FromBody] RegisterCatRequest request)
{
    var command = new RegisterCatCommand
    {
        CatId = Guid.NewGuid(),                       // minted here, never by the client
        OwnerId = request.OwnerId,
        …,
        UserContext = userContextProvider.GetUserContext(),
        CorrelationId = HttpContext.TraceIdentifier.ToCorrelationId(),
        ValidateCommand = true                        // F3 — without this the validator never runs
    };

    var response = await dispatcher.SendAsync(command);

    return response.IsSuccess
        ? CreatedAtAction(nameof(GetCat), new { catId = response.Result }, new { catId = response.Result })
        : BadRequest(response.ValidationResult);      // never 202
}
```

**`CatsController`** — route `api/cats`, namespace `CatEcommerce.Api.Controllers`:

| Method | Route | Command / Query | Success |
|---|---|---|---|
| `POST` | `/api/cats` | C1 | `201` + `catId` |
| `GET` | `/api/cats` | Q2 | `200` |
| `GET` | `/api/cats/{catId}` | Q1 | `200` / `404` |
| `PUT` | `/api/cats/{catId}/profile` | C2 | `200` |
| `PUT` | `/api/cats/{catId}/neuter-status` | C3 | `200` |
| `PUT` | `/api/cats/{catId}/medical-profile` | C5 | `200` |
| `POST` | `/api/cats/{catId}/photos` | C6 | `201` |
| `PUT` | `/api/cats/{catId}/high-risk-flag` | C7 | `200` |
| `DELETE` | `/api/cats/{catId}/high-risk-flag` | C8 | `200` |
| `POST` | `/api/cats/{catId}/weights` | C4 | `201` |
| `GET` | `/api/cats/{catId}/weights` | Q3 | `200` |
| `POST` | `/api/cats/{catId}/vaccinations` | C9 | `201` |
| `GET` | `/api/cats/{catId}/vaccination-status` | Q4 | `200` |
| `POST` | `/api/cats/{catId}/parasite-treatments` | C10 | `201` + `treatmentId`, `nextDueOn` |
| `POST` | `/api/cats/{catId}/deceased` | C11 | `200` |
| `POST` | `/api/cats/{catId}/ownership-transfer` | C12 | `200` |
| `GET` | `/api/cats/{catId}/medical-history.pdf` | Q5 → renderer | `200` `application/pdf` |
| `GET` | `/api/cats/{catId}/cage-card` | Q7 → QR generator | `200` `image/png` |

**`VaccinesController`** — `POST /api/vaccines` (C13, `201`), `GET /api/vaccines` (Q6, `200`).

Response mapping, everywhere: `IsSuccess` → `200`/`201` with the real result;
`!IsSuccess` → `400` carrying `CommandResponse.ValidationResult`;
`DbUpdateConcurrencyException` → **`409`** (ADR-016, `xmin`) via the exception handler.
**Never `202`.**

Request DTOs live in `Api/Contracts/Cat/` and `Api/Contracts/Vaccination/`, namespace
`CatEcommerce.Api.Contracts.[Feature]`. They are **not** the commands — the client never supplies
an id, a `UserContext` or a `CorrelationId`.

**C10's success body is the one exception to "`{ <resource>Id }` only"** (owner decision
2026-09-25): it also returns `nextDueOn`, because — unlike vaccination status (Q4) — there is no
`GET` endpoint for a parasite-treatment history that HO-CAT-002's parasite-control screen could
otherwise re-fetch the date from. The body is exactly `{ treatmentId, nextDueOn }` — no `catId`;
the client already knows the cat's id, it's in the URL. Cast `response.Result` to
`RecordCatParasiteTreatmentResult` (§11.5) and shape it explicitly, the same as `RegisterCat`
shapes its own body from `response.Result`:

```csharp
[HttpPost("{catId:guid}/parasite-treatments")]
public async Task<IActionResult> RecordParasiteTreatment(
    Guid catId, [FromBody] RecordCatParasiteTreatmentRequest request)
{
    var command = new RecordCatParasiteTreatmentCommand
    {
        CatId = catId,
        TreatmentId = Guid.NewGuid(),                  // minted here, never by the client
        TreatmentType = request.TreatmentType,
        ProductName = request.ProductName,
        BatchReference = request.BatchReference,
        AdministeredOn = request.AdministeredOn,
        AdministeredByStaffId = request.AdministeredByStaffId,
        IntervalDays = request.IntervalDays,
        Notes = request.Notes,
        UserContext = userContextProvider.GetUserContext(),
        CorrelationId = HttpContext.TraceIdentifier.ToCorrelationId(),
        ValidateCommand = true                         // F3 — without this the validator never runs
    };

    var response = await dispatcher.SendAsync(command);
    if (!response.IsSuccess) return BadRequest(response.ValidationResult);   // never 202

    var result = (RecordCatParasiteTreatmentResult)response.Result!;
    return CreatedAtAction(nameof(GetCat), new { catId },
        new { treatmentId = result.TreatmentId, nextDueOn = result.NextDueOn });
}
```

`nextDueOn` is **required**, not nullable (owner confirmed 2026-09-25): every
`RecordCatParasiteTreatmentCommand` carries a validated `IntervalDays` (§11.3), so the aggregate
always computes one, matching `CatParasiteTreatment.NextDueOn`'s own non-null `DateOnly` (§8.3).

### 12.5 Host — `Program.cs`

Follow `.ai/host-composition.md` §1. `ValidateScopes` and `ValidateOnBuild` stay **on**.
`AddInMemoryBusServices()` is the only dispatch registration; `AddServiceBusProvider` must not
appear. Register, all `Scoped`:

- `IUserContextProvider` → `DevUserContextProvider` — the one platform seam
- `AddDbContext<CatEcommerceDbContext>` with Npgsql
- `AddScoped(typeof(IAggregateRootRepository<>), typeof(EfAggregateRootRepository<>))`
- **all 13** `ICommandHandlerAsync<T>` + **all 13** `IValidator<T>` (as `IValidator<T>`)
- **all 7** `IQueryHandlerAsync<TQuery, TResult>`
- **all 3** `IEventHandlerAsync<T>` audit handlers
- `ICatMedicalHistoryPdfRenderer`, `ICatQrCodeGenerator`
- `Configure<CatProfileOptions>(configuration.GetSection("CatProfile"))`

> **There is no assembly scanning.** A missing registration compiles fine and throws at dispatch.
> Count them against §11 and §12.3 before you finish.

Do **not** register: `IInMemoryDispatcher`, `IDispatcher`, `IResolver`, `IHandlerResolver`,
`ICommandSender`, `IQueryProcessor`, `IValidationService`, `IValidationProvider`,
`IEventPublisher`, `IBusMessageDispatcher`.

---

## 13. Allowed scope

```
src/Shared/CatEcommerce.Shared/**
src/Domain/CatEcommerce.Domain/**
src/Application/CatEcommerce.Application/**
src/Read/CatEcommerce.Read/**
src/Infrastructure/CatEcommerce.Infrastructure/**
src/Api/CatEcommerce.Api/**
tests/CatEcommerce.Domain.Tests/**
tests/CatEcommerce.Application.Tests/**
tests/CatEcommerce.IntegrationTests/**
docs/test-cases/TC-HO-CAT-001.md
docs/gates/HO-CAT-001-*.md
.ai/decisions/adr.md                 ← ADR-021/022/023 only (§9), append only
```

`csproj` edits allowed **only** to (a) add `QuestPDF` and `QRCoder` to
`CatEcommerce.Infrastructure` (§12.2), and (b) **remove** `Testcontainers.PostgreSql` from
`CatEcommerce.IntegrationTests`, which is unused under §5.1. Any other package change is outside
scope.

## 14. Forbidden scope

| Path | Why |
|---|---|
| `.ai/**` except `decisions/adr.md` | Corrected in `5b260df`. Found a defect? Report it as a finding |
| `.claude/**` | Agent and gate definitions |
| `docs/SRS.md`, `docs/USER_STORIES.md` | Requirements are an input. A conflict is a §20 stop, not an edit |
| `instructions/**` | Source brief |
| `NuGet.Config`, `global.json`, `CatEcommerce.sln` | Build/feed configuration |
| Any other bounded context | Appointment, Visit, Surgery, Boarding, Product, Order, Invoice… |

Touching anything here is an automatic **FAIL** at the architecture and acceptance gates.

---

## 15. Unit test cases

`tests/CatEcommerce.Domain.Tests` (no database) and `tests/CatEcommerce.Application.Tests`.
The test-engineer turns each row into a sheet row in `docs/test-cases/TC-HO-CAT-001.md`.

| ID | Case | Expected |
|---|---|---|
| U1 | `Register` with a complete valid DTO | Properties set; `CatRegisteredEvent` raised; `Version` incremented |
| U2 | `Register` with a future DOB | `DomainRuleViolationException` |
| U3 | `Register` with precision `Exact` and no DOB | `DomainRuleViolationException` |
| U4 | `Register` with precision `Estimated` and no DOB | Succeeds |
| U5 | `RecordNeuterStatus`, `Neutered` with no `NeuteredOn` | Violation |
| U6 | `RecordNeuterStatus`, `NeuteredOn` before DOB | Violation |
| U7 | `RecordNeuterStatus`, `NeuteredOn` in the future | Violation |
| U8 | `AddPhoto` of a disallowed content type | `InvalidPhotoContentType` |
| U9 | `AddPhoto` over `MaxSizeInBytes` | `PhotoTooLarge` |
| U10 | `AddPhoto` #11 | `PhotoLimitExceeded` |
| U11 | First photo added | `IsPrimary == true` |
| U12 | `RecordVaccination` with `BatchExpiresOn < AdministeredOn` | **`ExpiredVaccineBatch`** (US-CAT-05 AC2) |
| U13 | `RecordVaccination`, `BatchExpiresOn == AdministeredOn` | Succeeds — boundary |
| U14 | `RecordVaccination` valid, `ValidityMonths = 12` | `NextDueOn == AdministeredOn.AddMonths(12)` (US-CAT-05 AC1) |
| U15 | Same vaccine, same cat, same day, twice | `DuplicateVaccinationOnDate` |
| U16 | Same vaccine, different day | Succeeds |
| U17 | `RecordParasiteTreatment`, `IntervalDays = 90` | `NextDueOn == AdministeredOn.AddDays(90)` (US-CAT-06) |
| U18 | `FlagHighRisk` | `IsHighRisk`, note set, `CatFlaggedHighRiskEvent` carries the reason |
| U19 | `ClearHighRiskFlag` on an unflagged cat | `CatNotFlaggedHighRisk` |
| U20 | `MarkDeceased` then `UpdateProfile` | `CatIsDeceased` |
| U21 | `MarkDeceased` twice | `CatAlreadyDeceased` |
| U22 | `MarkDeceased` with a future date | Violation |
| U23 | `TransferOwnership` to the current owner | `OwnerUnchanged` |
| U24 | `TransferOwnership` valid | `OwnerId` changed; event carries previous **and** new owner; **all collections intact** (US-CAT-08 AC1) |
| U25 | `Weight.Create(0)` / `Create(31)` | Violation both |
| U26 | Every mutation method | Raises exactly one event and calls `UpdateVersion()` |
| U27 | `Enumeration.FromValue` / `FromDisplayName` / `GetAll` / equality | Correct for `CatSex` and `VaccinationStatus` |
| U28 | Handler: cat not found | `IsSuccess == false`, `CatNotFound`. **No exception thrown** |
| U29 | Handler: `DomainRuleViolationException` from the aggregate | Converted to `CommandResponse`, code and property preserved |
| U30 | `RegisterVaccine` with an existing code | `VaccineCodeAlreadyExists` |
| U31 | `RegisterVaccine` with a new code | Succeeds |
| U32 | `RecordCatWeightCommandHandler` | Saves `CatWeightRecordAggregate`; **`CatAggregate` is not saved** |
| U33 | `RecordCatVaccination` against an inactive vaccine | `InactiveVaccine` |
| U34 | Vaccination status: all core in date | `Current` |
| U35 | Vaccination status: one core due in 10 days, window 30 | `DueSoon` (US-CAT-07 AC2) |
| U36 | Vaccination status: one core past due | `Overdue`, `bookingEligible == false` (US-CAT-07 AC3) |
| U37 | Vaccination status: a core vaccine never administered | `Overdue`, reason `NeverAdministered` |
| U38 | Vaccination status: boundary at exactly `today + window` | `DueSoon`, not `Current` |
| U39 | Every validator, invalid input | Fails with the intended property name |
| U40 | `AddCatPhotoCommandValidator` | Rejects bad content type **and** oversize (US-CAT-01 AC3 at the API edge) |
| U41 | `RecordCatParasiteTreatmentCommandHandler` valid | `CommandResponse.Result` is a `RecordCatParasiteTreatmentResult` carrying the created treatment's `TreatmentId` and `NextDueOn == AdministeredOn.AddDays(IntervalDays)` — **not** `cat.Id` (owner decision 2026-09-25, HO-CAT-002 A11) |

## 16. Integration test cases

`tests/CatEcommerce.IntegrationTests` — real HTTP through `WebApplicationFactory`, against the
**shared local `catecommerce_dev` database** (§5.1). Every case below is expected to run; a
`BLOCKED` row needs a stated reason. `PASS` means you watched it pass — never inferred from a green
unit test.

### 16.1 Writing for a shared, non-empty database

The suite shares a database with development and with its own previous runs. Four rules; break any
of them and the suite passes today and fails intermittently next month, which is worse than no
suite at all.

1. **Every test mints its own ids and its own owner.** Generate a fresh `ownerId` per test and
   attach that test's cats to it. Never reuse a fixed literal id.
2. **Never assert on a global count or on emptiness.** `GET /api/cats` will return other tests'
   data and yesterday's manual poking. I15 and I16 assert *"this cat is absent from **this
   owner's** list"*, never *"the list is empty"*.
3. **Every natural key is unique per run.** I6 registers a vaccine code like `RABIES-{Guid:N}`;
   a fixed `"RABIES"` passes on a clean database and fails for ever afterwards.
4. **No test depends on another's data or on execution order.** Each arranges everything it needs.
   Do not disable xUnit parallelism to paper over a shared-state bug — fix the test.

I19 (pagination) needs a known population, so it must scope its query to its own `ownerId` and
assert against that, not against the table.

| ID | Case | Expected |
|---|---|---|
| I1 | Host starts with `ValidateScopes` + `ValidateOnBuild` on | No exception. **Proves every registration in §12.5 and that no `IBusMessageDispatcher` is needed** |
| I2 | `POST /api/cats` valid | `201`, `catId` in body, row in `clinic.cat` |
| I3 | `POST /api/cats` with an empty name | **`400`** with `ValidationResult`. If this returns `201`, F3 was not applied |
| I4 | `GET /api/cats/{id}` after I2 | `200`, same data **in the same request cycle** — proves read-after-write with no projection lag |
| I5 | `GET /api/cats/{unknown}` | `404` |
| I6 | `POST /api/vaccines` twice with the same **run-unique** code (§16.1 rule 3) | Second → `400` `VaccineCodeAlreadyExists` |
| I7 | Two concurrent updates to one cat | Second → **`409`** (`xmin`, ADR-016) |
| I8 | `POST .../weights` ×3, then `GET .../weights` | 3 points ascending by date; `latestWeightKg` on the profile matches the newest |
| I9 | `POST .../vaccinations` with an expired batch | `400` `ExpiredVaccineBatch` |
| I10 | `POST .../vaccinations` valid | `201`; `nextDueOn` persisted and matches the catalogue's `ValidityMonths` |
| I10a | `POST .../parasite-treatments` valid | `201`; **response body** has `treatmentId` and `nextDueOn == administeredOn.AddDays(intervalDays)` (owner decision 2026-09-25, HO-CAT-002 A11) |
| I11 | `GET .../vaccination-status` across the three states | Matches U34–U37 against real rows |
| I12 | `PUT .../high-risk-flag` | `200`; **a row appears in `audit.audit_entry`** with actor and reason (US-CAT-04 AC2). Proves the event handler committed |
| I13 | `DELETE .../high-risk-flag` | `200`; a second audit row |
| I14 | `POST .../ownership-transfer` | `200`; owner changed; audit row with both owner ids; **vaccinations and medical profile unchanged** (US-CAT-08 AC1) |
| I15 | `GET /api/cats?ownerId=<previous>` after I14 | The cat is **absent from that owner's list** (US-CAT-08 AC2). Assert on this cat's absence, not on an empty list |
| I16 | `POST .../deceased`, then `GET /api/cats?ownerId=<this test's owner>` | Cat absent by default (US-CAT-09 AC1) |
| I17 | Same, `GET /api/cats/{id}` and `?includeDeceased=true` | Both still return it (US-CAT-09 AC2) |
| I18 | `PUT .../profile` on a deceased cat | `400` `CatIsDeceased` |
| I19 | 25 cats under **this test's own `ownerId`**, `GET /api/cats?ownerId=…&pageSize=10` | 10 returned, paging headers correct (UI-4). Scoped to the owner — the table holds other data (§16.1 rule 2) |
| I20 | `GET .../medical-history.pdf` | `200`, `application/pdf`, non-empty, contains the cat's name (US-CAT-10) |
| I21 | `GET .../cage-card` | `200`, `image/png`, decodes to the profile URL (US-CAT-11) |
| I22 | Migration applied to an empty database | All tables and every index in §12.1 present, including the unique `vaccine.code` |

---

## 17. Implementation order and verification

Build in this order — each step compiles before the next.

1. `Shared` — `Enumeration`, enums, constants
2. `Domain` — value object, exception, entities, aggregates, events, DTOs
3. **Domain unit tests U1–U27** — these run with no database. Get them green before continuing
4. `Application` — commands, validators, mappers, handlers, event handlers
5. **Application unit tests U28–U40**
6. `Infrastructure` — DbContext, repository, configurations, `DevUserContextProvider`, options
7. `Read` — ViewModels, queries, query handlers
8. `Api` — contracts, controllers
9. `Program.cs` — every registration in §12.5
10. Migration — generate, **read it**, commit
11. `Infrastructure/Documents` — PDF and QR
12. Integration tests I1–I22

### 17.1 Environment setup — once, before step 6

The development database is the native PostgreSQL **17.5** on `localhost:5432` (§5.1), not the
`shahjahan-db-1` container. Create a database for this solution:

```bash
# credentials: postgres / postgres
psql -h localhost -U postgres -c "CREATE DATABASE catecommerce_dev;"
```

The connection string goes in **`src/Api/CatEcommerce.Api/appsettings.Development.json`**:

```jsonc
{
  "ConnectionStrings": {
    "CatEcommerce": "Host=localhost;Port=5432;Database=catecommerce_dev;Username=postgres;Password=postgres"
  }
}
```

**That file must be git-ignored before it is created** — `.gitignore` currently covers only
`appsettings.*.local.json`, so without this the password is committed, which SRS **NFR-SEC-7**
forbids. Add to `.gitignore`:

```
appsettings.Development.json
```

Commit a checked-in `appsettings.Development.json.example` carrying the same shape with the
password blanked, so the next developer knows what to create.

`appsettings.json` **is** committed, declares the shape and the options, and carries no credential:

```jsonc
{
  "ConnectionStrings": { "CatEcommerce": "" },   // real value in appsettings.Development.json
  "CatProfile": {
    "VaccinationDueSoonWindowDays": 30,          // FR-CAT-7
    "DefaultParasiteIntervalDays": 90
  },
  "Logging": { "LogLevel": { "Default": "Information" } }
}
```

The integration suite uses **this same database** (§5.1). Read the rules in §16.1 before writing
the first test — they are what keeps a shared database from producing an intermittent suite.

### 17.2 Commands

```bash
dotnet build CatEcommerce.sln                       # must be 0 warnings, 0 errors
dotnet test tests/CatEcommerce.Domain.Tests
dotnet test tests/CatEcommerce.Application.Tests
dotnet dotnet-ef migrations add AddCatProfile \
  --project src/Infrastructure/CatEcommerce.Infrastructure \
  --startup-project src/Api/CatEcommerce.Api \
  --output-dir Persistence/Migrations
dotnet dotnet-ef database update --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api
dotnet run --project src/Api/CatEcommerce.Api       # must start clean — this is I1
dotnet test tests/CatEcommerce.IntegrationTests     # hits catecommerce_dev; see §16.1
```

---

## 18. Acceptance criteria

The acceptance gate checks **each row against named evidence**. A row with no evidence is not met.
"UI" rows are the server half only — §4.1 explains why.

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | CAT-01 | A cat is created with name, sex, DOB, breed, colour and linked to an owner | I2, I4 |
| A2 | CAT-01 | DOB precision is stored and returned, so age can be shown as approximate | U4; `dateOfBirthPrecision` on Q1 |
| A3 | CAT-01 | A photo of a disallowed type or over the size limit is rejected with a clear reason | U8, U9, U40 |
| A4 | CAT-02 | Neuter status requires a date when neutered, and the date is validated | U5, U6, U7 |
| A5 | CAT-02 | A vaccine code cannot be duplicated in the catalogue | U30, U31, I6 |
| A6 | CAT-02 | A new weight is appended to history and the series is retrievable in order | U32, I8 |
| A7 | CAT-03 | Allergies, conditions, medications, dietary and behavioural notes are recorded | U26, Q1 fields |
| A8 | CAT-03 | The profile distinguishes "none recorded" from absent | `hasRecordedAllergies` on Q1; §4.1 |
| A9 | CAT-04 | A cat can be flagged high-risk with a handling note, exposed on profile **and** list reads | U18; Q1 + Q2 fields |
| A10 | CAT-04 | Setting **and** clearing the flag are audited with actor and reason | U18, U19, **I12, I13** |
| A11 | CAT-05 | A vaccination requires batch, date and administering vet; next-due is computed from the schedule | U14, I10 |
| A12 | CAT-05 | An expired batch is blocked | U12, U13, I9 |
| A13 | CAT-06 | A parasite treatment computes next-due from the configured interval, and the `201` response returns it | U17, U41, I10a |
| A14 | CAT-07 | All core vaccines in date → `Current` | U34, I11 |
| A15 | CAT-07 | Due within the window (default 30 days) → `DueSoon` with the date | U35, U38, I11 |
| A16 | CAT-07 | A core vaccine past due → `Overdue`, and `bookingEligible` is false | U36, U37, I11 |
| A17 | CAT-08 | Transfer preserves the full history and writes an ownership audit entry | U24, **I14** |
| A18 | CAT-08 | The previous owner no longer sees the cat | **I15** |
| A19 | CAT-09 | A deceased cat is excluded from booking/ordering reads | I16 |
| A20 | CAT-09 | The record is retained, still viewable, and not editable | U20, I17, I18 |
| A21 | CAT-10 | Medical history downloads as a PDF identifying the clinic and the cat | I20 |
| A22 | CAT-11 | A cage card QR resolves to the cat's profile | I21 |
| A23 | — | Every list endpoint is paginated (UI-4) | I19 |
| A24 | — | Concurrent edits produce `409`, not last-write-wins (ADR-016) | I7 |
| A25 | — | The host starts with scope validation on and no broker type registered | **I1** |

---

## 19. Required gates

In order. Each writes `docs/gates/HO-CAT-001-<GATE>.md` **before** reporting — a gate that leaves
no artifact did not happen.

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | Layer boundaries; aggregate discipline; §13/§14 scope; **no `IBusMessageDispatcher`/`IEventPublisher`/`IDispatcher`** (ADR-020) |
| 2 | SECURITY | `security-reviewer` | Photo metadata handling, owner scoping, the audit trail, and the §20.3 authorization gap |
| 3 | CODE_REVIEW | `code-reviewer` | Correctness and the conventions in `.ai/` |
| 4 | TEST | `test-engineer` | Writes `TC-HO-CAT-001.md` from §15/§16 **first**, then executes and marks each row as observed |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row against named evidence; all four prior gates recorded `PASS` |

Each gate needs the handover ID, the commits and the changed-file list; ACCEPTANCE also needs the
four prior results.

---

## 20. BLOCKED conditions — stop and report, do not work around

Working around any of these instead of reporting it is an automatic acceptance **FAIL**.

**20.1 Never substitute an in-memory or SQLite EF provider for the integration tests.** A real
PostgreSQL is configured and reachable (§5), so there is no excuse for it. The in-memory provider
implements none of `xmin` (I7), unique indexes (I6, I22), `timestamptz` (C-7) or `numeric`
precision — precisely the things those cases exist to prove. A suite that passes against it has
tested nothing that matters here.

**20.1a The integration tests share the development database. Do not `TRUNCATE`, do not
`DROP DATABASE`, and do not add a "clean the database" fixture.** It holds development data that is
not yours to delete, and a suite that wipes shared state will eventually run while someone is using
it. Isolate by data ownership instead — §16.1. If a test cannot be written without a clean slate,
**stop and report it** rather than adding the truncate.

**20.2 `appsettings.Development.json` must be in `.gitignore` before you create it.** It carries
the database password, `.gitignore` currently covers only `appsettings.*.local.json`, and SRS
**NFR-SEC-7** forbids credentials in repository configuration. Add the ignore rule in the **same
commit** that adds the file. `postgres`/`postgres` being a local default does not exempt it — the
next connection string will be a real one, and the security gate will flag it either way. Commit
`appsettings.Development.json.example` with the password blanked instead.

**20.3 Authorization does not exist.** `DevUserContextProvider` returns a fixed identity, and the
host runs `enableAuthorization: false`. US-CAT-08 AC2 (A18) is therefore enforced by a **query
filter**, not by authorization: any caller who supplies a different `ownerId` still sees the data.
**State this explicitly in the gate reports.** Do not implement roles or `[Authorize]` — that is §1
Accounts, and building it here is out-of-scope expansion.

**20.4 Reminders have nowhere to go.** US-CAT-06 says "a reminder is queued" and US-CAT-09 says
"pending reminders are cancelled". There is no Notification context and no outbox table. Compute
and store the next-due dates, raise the events, and stop. **Do not build an outbox** — it is
`.ai/domain-events-and-outbox.md` §4's design and belongs to its own slice.

**20.5 The PDF cannot contain clinical detail.** US-CAT-10 asks for visits, diagnoses,
prescriptions and lab results. None of those aggregates exist. Include what does exist, and print
an explicit note naming what is omitted and why. **Do not fabricate sections and do not stub a
`VisitAggregate`.**

**20.6 QuestPDF licensing must be confirmed before merge.** The Community licence is free only
below a revenue threshold. If it does not apply here, stop and report — do not silently swap in an
AGPL library such as iText, which would impose obligations on the whole solution.

**20.7 A requirement conflict is a stop, not an edit.** `docs/SRS.md` and `docs/USER_STORIES.md`
are forbidden scope (§14). Report the conflict as a finding.

**20.8 Do not downgrade `Platform.Infrastructure.Core` below 1.0.34.** On 1.0.32 the host cannot
start without registering `IBusMessageDispatcher`, which §4.3 forbids. If a restore resolves an
older version, stop.
