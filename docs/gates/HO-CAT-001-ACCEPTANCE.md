# Second pass (delta 37d9ce9..430e922)

GATE: ACCEPTANCE
HANDOVER: HO-CAT-001
COMMITS: master(eae3d7b)..ho/CAT-001-gates(430e922). Delta since first pass (bbd005a): 0a3d028, d207d26, efae3ec, 47c9ba2, b673a0c, 2e18d05, c5cfdb2, 20225d3, ccb7b5a, 430e922
DATE: 2026-09-27
STATUS: PASS

All 25 §18 criteria are MET. Each is backed by a named test in the repo that passed during this
gate's run. A22 is fixed, and all six criteria that were previously probe-only (A1, A8, A9, A11,
A17, A24) now have real tests in the repo that assert them.

## Prior gates

| Gate | Record | Status |
|---|---|---|
| ARCHITECTURE | HO-CAT-001-ARCHITECTURE.md, 2nd pass | PASS |
| SECURITY | HO-CAT-001-SECURITY.md, 2nd pass | PASS |
| CODE_REVIEW | HO-CAT-001-CODE_REVIEW.md, 5th pass (bbd005a..20225d3) | PASS. Its 1 MEDIUM, userinfo in the URL guard, is fixed in 430e922 |
| TEST | HO-CAT-001-TEST.md + TC-HO-CAT-001.md (rows corrected in 2e18d05) | PASS |

No gate record covers 430e922; the integrator reviewed it. I read that commit: a one-line
`UserInfo` rejection plus 9 tests of the startup guard, and all 9 pass (below). This gate also
covers it.

ARCHITECTURE and SECURITY last ran at 67f5865. Every source change after that has had CODE_REVIEW
only. That includes the new host-wide `PublicSiteOptions` and its startup guard, which is new
configuration that a future Notifications area will share.

## Commands (head 430e922)

- Build: 0 warnings, 0 errors.
- `dotnet test CatEcommerce.sln` against catecommerce_cat: **733/733 passed** (Infrastructure 28,
  Domain 223, Application 93, Integration 389).
- Targeted run of `CatAcceptanceGateEndpointTests`, the two `GetCatCageCard*` tests and
  `HostCompositionTests`: 23/23 passed, each checked by name.
- `has-pending-model-changes`: "No changes have been made to the model since the last migration."
- `npx vitest run`: 37 files, **106/106 passed**.
- `git merge-tree --write-tree master ho/CAT-001-gates` (master = eae3d7b): exit 0, tree d39b0c2, no conflicts.
- Migrations: still exactly one, `20260926135821_WidenAuditEntryValues`. The delta adds none.

## Criteria re-verified

| # | Verdict | Evidence (assertions read) |
|---|---|---|
| A22 | MET | `CatProfileProjectionEndpointTests.GetCatCageCard_QrPayload_DecodesToTheAbsoluteProfileUrl` sets `PublicSite:WebBaseUrl=https://cat-ecommerce.test`, decodes the returned PNG with ZXing and asserts `result.Text == "{webBaseUrl}/cats/{catId}"`. In `GetCatCageCardQueryHandler`, the only inputs to that string are the config value and a database Guid. The owner decided A22 is judged on this backend payload. The `/cats/{id}` smart-redirect web page is recorded as pending in both §1 Amended rows |
| A1 | MET | `CatAcceptanceGateEndpointTests.RegisterCat_WithBreedColourSexAndDateOfBirth_AllRoundTripOnGet`: POST then GET, asserting ownerId, sex, precision, dateOfBirth, breed and colourAndMarkings. Name is covered by I4 |
| A8 | MET | `…GetCat_NoAllergiesEverRecorded_HasRecordedAllergiesFalseWithEmptyArray` covers false with `[]`. The projection test covers the true case |
| A9 | MET | `…GetCats_FlaggedHighRiskCat_ListItemHasIsHighRiskAndHandlingNote`: the list item has `isHighRisk` true and the exact `handlingNote`. The source change is in `GetCatsQueryHandler` and `CatListItemViewModel`, and §12.3 Q2 is amended to match §4.1 (owner decision c). The profile half is covered by the projection test |
| A11 | MET | U14 and I10 cover next-due. Three tests cover the required fields: `…EmptyBatchReference_Returns400OnBatchReference`, `…EmptyVetId_Returns400OnAdministeredByVetId`, `…NurseNamedAsVet_Returns400WithStaffAccountNotVeterinarian`. Date is a non-nullable `DateOnly` and future dates are rejected (U39 `RecordCatVaccinationCommandValidator_FutureDate_FailsOnAdministeredOn`) |
| A17 | MET | U24 and I14 cover history kept and actor/reason. `…TransferCatOwnership_Valid_AuditRowBeforeAndAfterValuesContainBothOwnerIds` asserts BeforeValue contains the previous owner and AfterValue contains the new owner |
| A24 | MET | `…ConcurrentProfileUpdate_ForcedRowLockRace_Returns409AndKeepsTheCompetingEdit`: a held row lock, the PUT asserted still blocked at 3 s, then a **409** asserted, and a GET that returns the competing edit. This is the first test in the repo that observes the ADR-016 409 |
| A2–A7, A10, A12–A16, A18–A21, A23, A25 | MET | Unchanged from the first pass (evidence table below). All of those tests passed again in the 733/733 run. A25 still holds: I1 passes, and the new hosted service starts under ValidateOnBuild |

The TC sheet (2e18d05) now cites these tests. The I21, I14, A1, A8, A9, A11, A17 and A24 rows match
what the tests assert. The I7 gap row is closed.

## Scope

- Out-of-scope work: none.
  - The PublicSite setting, the list `handlingNote` and the staff-list badge variant come from owner
    decisions (b) and (c) and the owner-authorised HO-CAT-002 change.
  - The web page for `/cats/{id}` was correctly **not** built. It waits on HO-ACC-002.
- Files outside §13 in the delta, all declared in the §1 Amended rows:
  - `docs/handover/HO-CAT-001.md` and `HO-CAT-002.md`
  - 5 web/** files, reviewed by CODE_REVIEW's 5th pass
  - `tests/CatEcommerce.IntegrationTests.csproj`, which adds the **ZXing.Net 0.16.10** package
    (Apache-2.0, test project only). §13 allows only two package changes, so this is a third one.
    It is declared in the Amended row, but no approval is recorded, so **the owner should
    acknowledge it**.
- §14 forbidden paths: none touched. No git-ignored appsettings were modified or committed. Only
  `appsettings.json` (empty value) and the `.example` file changed.

## Undeclared deviations

None found.

## Owner must know before merging

1. **Production will refuse to start** unless `PublicSite:WebBaseUrl` is set. It must be an
   absolute https URL with no path, query, fragment or userinfo. `appsettings.json` ships it
   empty, so the deployment configuration has to supply it.
2. **Cage-card QR codes printed before the web page exists lead to a 404.** They encode
   `{WebBaseUrl}/cats/{id}`, and that page is pending until HO-ACC-002 merges.
3. ZXing.Net is a new test-only package outside §13's package allowance. It is declared, but it
   needs your acknowledgement.
4. Notifications has not been switched to `PublicSite:WebBaseUrl` yet. That is an integrator task
   once both areas merge.
5. ARCHITECTURE and SECURITY have not re-run since 67f5865. Every later change was reviewed by
   CODE_REVIEW and, for 430e922, by the integrator.

---

# First pass (2026-09-27, head 37d9ce9, committed bbd005a) -- retained unchanged below

GATE: ACCEPTANCE
HANDOVER: HO-CAT-001
COMMITS: master(eae3d7b)..ho/CAT-001-gates(37d9ce9), 35 commits, 177232d … 37d9ce9 (the head also carries the owner-decided HO-CAT-002 web changes cb2ddd2, 24b3df4, 37d9ce9)
DATE: 2026-09-27
STATUS: FAIL

One criterion is not met: **A22, the cage-card QR does not resolve to the cat's profile.** The other
24 criteria are MET. But for six of them (A1, A8, A9, A11, A17, A24) the evidence the TC sheet names
does not actually assert the criterion. This gate supplied the missing evidence itself with scratch
probes (listed below). A9 also has a contradiction inside the handover that the owner has to settle.

## PRIOR GATES (from docs/gates/ and docs/test-cases/, not from commit messages)

| Gate | Record | Status |
|---|---|---|
| ARCHITECTURE | HO-CAT-001-ARCHITECTURE.md, 2nd pass at 67f5865 | PASS |
| SECURITY | HO-CAT-001-SECURITY.md, 2nd pass at 67f5865 | PASS |
| CODE_REVIEW | HO-CAT-001-CODE_REVIEW.md, 4th pass (delta 2b40542..7e22d6e) | PASS, 0 open findings |
| TEST | HO-CAT-001-TEST.md + docs/test-cases/TC-HO-CAT-001.md | PASS, 106/106 |
| HO-CAT-002 web delta | HO-CAT-002-DELTA-cb2ddd2.md | PASS |

Note: ARCHITECTURE and SECURITY last reviewed 67f5865. The later source commits were fixes to those
gates' own findings (PagingHelper, customer existence and Active-status lookups, list cap, PDF
exception port, NotEmpty, no-store). Only the CODE_REVIEW 3rd and 4th delta passes reviewed them.
24b3df4 and 37d9ce9 (web config only) have no gate record. The integrator reviewed them.

## Commands run by this gate (worktree CAT-001-gates, head 37d9ce9)

- `dotnet build CatEcommerce.sln`: 0 warnings, 0 errors.
- `dotnet test CatEcommerce.sln` with CATECOMMERCE_TEST_DB=catecommerce_cat: **715/715 passed**
  (Infrastructure 28, Domain 223, Application 93, Integration 371).
- `dotnet dotnet-ef migrations has-pending-model-changes`: "No changes have been made to the model
  since the last migration."
- `npx vitest run` (web/): **104/104 passed**.
- `git merge-tree --write-tree master ho/CAT-001-gates` (master = eae3d7b): exit 0, tree 998b64d,
  no conflicts.
- Migrations: the branch adds exactly one migration, `20260926135821_WidenAuditEntryValues`
  (plus its Designer and the snapshot update). It alters audit.audit_entry BeforeValue/AfterValue
  from varchar(2000) to text, with a symmetric Down. It sorts after master's latest migration
  (AddAdministration, 20260925185529).
- Scratch probes. These are xUnit tests in the session scratchpad that derive from the repo's own
  `CatIntegrationTestBase` and run against catecommerce_cat. None of them are in the repo.
  - P1 (A24): a second connection opens a transaction and UPDATEs the cat row. A PUT /profile is
    then issued. The PUT is still blocked after 3 s. After the probe commits, the PUT returns
    **409**, and GET shows the competing edit survived. **PASS.**
  - P2 (A22): the exact string Q7 hands to the QR encoder is `"/cats/{catId}"`. That is a relative
    path with no host (observed).
  - P3 (A1): POST with breed, colour, DOB, sex and precision, then GET. All of them round-trip,
    and ownerId matches. **PASS.**
  - P4 (A8): a cat with no allergies returns `hasRecordedAllergies: false` and `allergies: []`. **PASS.**
  - P5 (A9): GET /api/cats?ownerId= returns the flagged cat with `isHighRisk: true`, and **no
    `handlingNote` field**. **PASS** as written.
  - P6 (A11): each of these returns 400: empty batchReference (BatchReference), Guid.Empty vet
    (AdministeredByVetId), and a Nurse named as the vet (StaffAccountNotVeterinarian). **PASS.**
  - P7 (A17): the CatOwnershipTransferred audit row's before/after values contain both the
    previous and the new owner id. **PASS.**
  - I7 run 3 times with EF SQL logging: all 8 UPDATEs carry `WHERE "Id" = @p AND xmin = @p`, but
    **0 conflicts** happened in any run (8 serialised successes each time).

## CRITERIA

| # | Verdict | Evidence |
|---|---|---|
| A1 | MET | I2 (`CatCrudLifecycleEndpointTests.RegisterCat_Valid_Returns201WithCatIdAndPersistedRow`, row in clinic.cat) plus I4. Breed, colour, DOB and sex are asserted by **no** repo test (I2/I4 send and assert neither). This gate's P3 closes the gap |
| A2 | MET | U4 `Register_EstimatedPrecisionWithNoDateOfBirth_Succeeds`. `CatProfileProjectionEndpointTests.GetCat_FullProfile_ProjectsEveryFieldCorrectly` asserts `dateOfBirthPrecision` |
| A3 | MET | U8/U9 assert `InvalidPhotoContentType`/`PhotoTooLarge` error codes. U40 is the two `AddCatPhotoCommandValidator` cases |
| A4 | MET | U5, U6, U7 (`RecordNeuterStatus_*_Throws`) |
| A5 | MET | U30, U31, I6 (`RegisterVaccine_DuplicateRunUniqueCode_Returns400WithVaccineCodeAlreadyExists`) |
| A6 | MET | U32. I8 asserts 3 points in ascending order and `latestWeightKg` 4.1 |
| A7 | MET | U26. `GetCat_FullProfile_ProjectsEveryFieldCorrectly` asserts allergies, conditions, medications, dietaryNotes and behaviouralNotes |
| A8 | MET | Only the `true` case is asserted in the repo (projection test, I14). The "none recorded" `false` + `[]` case, which is the criterion itself, has no repo test. This gate's P4 closes the gap. Source: `GetCatProfileQueryHandler.cs:36` |
| A9 | MET as §12.3 specifies; **handover contradiction** | U18. Profile: the projection test asserts `isHighRisk` and `handlingNote`. List: the TC sheet cites I19/CatsPaginationEndpointTests, but **neither asserts `isHighRisk`**. P5 shows it is present. `CatListItemViewModel` has **no `handlingNote`**. §12.3 Q2 asks only for `isHighRisk`, but §4.1 (and HO-CAT-002 §3) require `isHighRisk` **+ `handlingNote`** on the list. The owner must decide which is correct |
| A10 | MET | U18, U19. I12/I13 read audit.audit_entry directly and assert a non-empty ActorUserId and the exact Reason for both set and clear |
| A11 | MET | U14, I10 (next-due = AddMonths(ValidityMonths)). Required batch and vet are not in the TC evidence: `RecordCatVaccinationCommandValidator` has NotEmpty on BatchReference and AdministeredByVetId, AdministeredOn is a non-nullable DateOnly, and P6 shows the three 400s |
| A12 | MET | U12 (ExpiredVaccineBatch), U13 (boundary), I9 |
| A13 | MET | U17, U41, I10a (`ParasiteTreatmentEndpointTests`, body `{treatmentId, nextDueOn}`) |
| A14 | MET | U34. I11 Current case (per-vaccine status. `overallStatus` deliberately not asserted because of the shared-catalogue race, which the TEST gate documented) |
| A15 | MET | U35, U38. I11 DueSoon case (per-vaccine) |
| A16 | MET | U36/U37 assert `BookingEligible == false`. I11 Overdue case asserts `overallStatus` Overdue and `bookingEligible` false |
| A17 | MET | U24. I14 asserts the audit row (actor and reason), the allergy and vaccination kept, and the new owner. I14 does not check "both owner ids in the audit row". P7 does |
| A18 | MET | I15 (`GetCatsByPreviousOwner_AfterTransfer_CatIsAbsent`, asserts absence of this cat id) |
| A19 | MET | I16 |
| A20 | MET | U20, I17 (GET by id plus includeDeceased list), I18 (400 CatIsDeceased) |
| A21 | MET | I20 (200, application/pdf, non-empty, no-store; Bengali-name case). The PDF text is not extracted. Source: `QuestPdfCatMedicalHistoryRenderer.cs:91-92` draws "CatEcommerce Clinic -- Medical History" and the cat's name, species and breed in the page header |
| A22 | **NOT MET** | Details below |
| A23 | MET | I19 (25 cats, pageSize 10, totalCount, page 3 = 5). `CatsPaginationEndpointTests` covers /weights and /vaccines |
| A24 | MET, but only by this gate's probe | I7 accepts an all-200 outcome, so it cannot fail under last-write-wins. It never observed a 409 in 3 runs by this gate, and no test in the repo ever observes one (`ConcurrencyExceptionFilter` has zero tests). Evidence: `CatConfiguration.cs:74` (xmin row version), the observed `WHERE … AND xmin = @p` SQL, `ConcurrencyExceptionFilter` registered in Program.cs:62, and **P1's deterministic 409** |
| A25 | MET | I1 `HostCompositionTests.Host_BuildsAndStartsWithNoException_EveryRegistrationResolves` (ValidateScopes and ValidateOnBuild are on). "No broker": no IBusMessageDispatcher, IEventPublisher, MassTransit or RabbitMQ reference in src/ outside comments (grep) |

### Why A22 is not met

The criterion is: "A cage card QR resolves to the cat's profile". The handover names I21 as the
evidence, and I21 is supposed to show that the QR "decodes to the profile URL".

- `GetCatCageCardQueryHandler.cs:30-33` builds `"/cats/{id}"`, prefixed by
  `CatProfile:PublicWebBaseUrl` if one is set. That value is set nowhere: not in appsettings.json,
  not in the .example file, not in the tests. P2 observed the payload `/cats/{catId}`.
- A relative path in a QR code does not resolve to anything when a phone scans it.
- Setting a base URL would not fix it either. The web app has no `/cats/{id}` route. Its profile
  pages are `/staff/cats/[catId]` and `/portal/cats/[catId]`, and next.config.ts has no
  rewrites or redirects.
- I21 (`GetCatCageCard_Returns200WithNoStoreCacheControl`) only asserts 200, image/png, no-store
  and non-empty bytes. It never decodes the QR. The TC sheet row I21 lists the expectation
  "decodes to the profile URL" with status PASS, which **overstates what the test proves**.

The fix needs an owner decision:
1. Which profile the QR opens. It is a staff cage card, so presumably `/staff/cats/{id}`.
2. Where the absolute base URL comes from (per-environment `PublicWebBaseUrl`, and what to do when
   it is empty).

After that, I21 needs a real assertion on the payload.

## SCOPE

- Out-of-scope work found: none that is unapproved. Everything below was declared and approved:
  - web/** changes (§4.1). These are the owner-decided HO-CAT-002 PagedResult adaptations
    (cb2ddd2), gated by HO-CAT-002-DELTA-cb2ddd2.md, plus 24b3df4/37d9ce9 (web config), which the
    integrator reviewed but no gate recorded.
  - Staff/customer existence and Active checks against `CustomerAccountAggregate`. §4.2 said
    "OwnerId is an unvalidated Guid", but this was required by SECURITY and CODE_REVIEW, and no
    Customer aggregate was added.
  - ADR-043 `[RequireAction]` authorization, which §4.2/§20.3 excluded. It came from the HO-ADM-001
    merge and the owner decided it.
- Files outside §13, all declared in §1 Amended rows or owner-decided:
  - `src/Infrastructure/CatEcommerce.Infrastructure/CatEcommerce.Infrastructure.csproj` (embedded
    font resources)
  - `docs/handover/HO-CAT-001.md`, `docs/handover/HO-CAT-002.md` (Amended rows)
  - `docs/gates/HO-CAT-002-DELTA-cb2ddd2.md`
  - `tests/CatEcommerce.IntegrationTests/AdministrationIntegrationTestBase.cs`,
    `AdminGlobalSettingsLiveEffectEndpointTests.cs`, `DuplicateCustomerGroupsEndpointTests.cs`.
    These are in §13's tests/ path but belong to other areas. They are merge and regression fixes,
    declared.
  - 15 web/** files (above)
  - The `WidenAuditEntryValues` migration, which is inside §13's path. The integrator authorised it.
- Forbidden scope (§14): nothing under .ai/**, .claude/**, SRS, USER_STORIES, instructions/**,
  NuGet.Config, global.json or the .sln is touched by master...HEAD.
- §20: no in-memory provider, and no TRUNCATE or DROP of the shared database. CatSchemaIndexTests
  creates and drops only its own uniquely named scratch database. appsettings.Development.json is
  git-ignored (.gitignore:26). QuestPDF licence: confirmed by the owner (§20.6 closed).

## UNDECLARED DEVIATIONS

- The QR payload is `/cats/{id}`, relative, and matches no web route (A22). It is self-documented
  only in a `CatProfileOptions` comment. No Amended row raises it.
- The Q2 list has no `handlingNote`, although §4.1 requires one (A9). The implementation follows
  §12.3, and the handover contradicts itself.
- Quality of the TC sheet evidence. These rows are marked PASS, but the tests they cite do not assert
  what the row claims:
  - I21: "decodes to the profile URL"
  - A9: "list has isHighRisk", via I19/CatsPaginationEndpointTests
  - A1: breed and colour
  - A8: "none recorded" case
  - I14: "audit row with both owner ids"
  - A24/I7: 409 is not asserted. The TEST gate did disclose this one as a gap, but still marked A24
    PASS.

## Owner must know before merging

1. **A22 blocks acceptance.** Decide the QR target route and base URL, then fix Q7 and give I21 a
   real assertion.
2. **A9: decide** whether the list read model carries `handlingNote` (§4.1/HO-CAT-002) or not
   (§12.3), and amend whichever section is wrong.
3. Consider adding this gate's probes P1 and P3–P7 to the suite. The repo currently has no test
   that ever observes a 409 (ADR-016) anywhere.
4. The merge is clean against eae3d7b, and the branch's only migration is WidenAuditEntryValues.
5. Already accepted by the owner, not findings: paging clamp/total/pageNumber cap, the C10 body,
   full personal data in audit, ADR-043, the QuestPDF Community licence, 4007225's extra files,
   photo StorageKey trust (moved to HO-FIL-001), read-auditing of clinical records (LOW).
