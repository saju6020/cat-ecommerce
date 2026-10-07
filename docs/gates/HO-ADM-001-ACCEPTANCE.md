GATE: ACCEPTANCE
HANDOVER: HO-ADM-001
COMMITS: master 7abc9e4 .. ho/ADM-001 902e65f (second pass; delta over first pass dd495c5: 8bc27ac tests, 517d22f docs, 902e65f CODE_REVIEW ninth pass). No source changed since c3dee41.
DATE: 2026-09-26
STATUS: PASS

# Second pass (supersedes the first pass below)

## Prior gates (read from docs/gates/ and docs/test-cases/)

| Gate | Recorded status | Covers |
|---|---|---|
| ARCHITECTURE | PASS (fifth pass) | through 8495e32 |
| SECURITY | PASS (fifth pass) | through 8495e32 |
| CODE_REVIEW | PASS (ninth pass, delta) | dd495c5..517d22f, i.e. exactly this gate's fix delta; earlier delta passes cover 8495e32..f108c5b |
| TEST | PASS; TC-HO-ADM-001.md updated to 90 full / 10 partial, no FAIL/BLOCKED/NOT RUN | sheet amended through 517d22f |

By integrator decision, everything after 8495e32 was reviewed by CODE_REVIEW delta passes only. Every later change is either test/doc-only or was covered by those delta passes.

## Commands run by this gate (head 902e65f, clean worktree)

- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test --no-build` with `CATECOMMERCE_TEST_DB=Host=localhost;Port=5432;Database=catecommerce_adm;...`:
  Infrastructure 28/28, Domain 221/221, Application 73/73, Integration 315/315. That is 637/637, with 0 failed and 0 skipped.
  The integration count rose by 26 over the first pass: 20 matrix theory cases, 2 matrix guard facts, 2 availability tests and 2 settings-audit tests.
- `dotnet dotnet-ef migrations has-pending-model-changes` (Infrastructure project, Api startup): "No changes have been made to the model since the last migration."
- Migration `20260925185529_AddAdministration` is present and applied. `MigrationsOnAnEmptyDatabaseTests` is green.
- `git merge-tree --write-tree master ho/ADM-001`: exit 0, tree ceced316..., conflict-free.

## Re-verified criteria

| # | Verdict | Evidence |
|---|---|---|
| A3 | MET | `CatVaccineFullAuthorizationMatrixTests.Endpoint_MatchesTheSpecTableForEveryCallerType` runs one theory case per reflection-discovered `[RequireAction]` action on CatsController/VaccinesController; 20 cases ran green. Each case checks anonymous -> 401, then each of `StaffRoles.All` (8), where an allowed role must not get 401/403 and any other role must get exactly 403. For customers it checks own-cat allowed and other's-cat 404 on owner-scoped actions, or 403 on staff-only actions. I compared the hand-typed `SpecRolesByAction` line by line against §12.3 (HO-ADM-001.md:677-686). All ten Cat/Vaccine rows match, as does the Customer (own) column (View/Register/EditProfile). Two guard facts make drift fail the build: `EveryDiscoveredAction_IsInTheHandTypedSpecTable` and `EveryDiscoveredEndpoint_HasABodyEntryIfItNeedsOne`. Together with U-A1 and U-A2 (first pass) this is the full I-A2 matrix §19 requires. |
| A6 | MET | Domain: `StaffAvailabilityTests.Intervals_ApprovedLeaveCoveringTheDate_RemovesTheWholeDay` (rule 5), `_ExtraShiftException_AddsAnIntervalOnADayWithNoPatternSlot` and `_ShiftCancelledException_ShortensOrRemovesTheShift` (rule 4), `_BranchClosedWholeDay_RestrictToOpeningHoursTrue_ReturnsNoIntervalThatDay` (rule 2), and `_AbuttingPatternShiftAndExtraShift_MergeIntoOneInterval` (rule 7). These add to U-C2 and U-C4 from the first pass. Integration: `AdminRosterLeaveEndpointTests.ApprovedLeaveDay_ThenGetAvailability_ExcludesThatDay` (one interval before, none after approval) and `ExtraShiftException_ThenGetAvailability_IncludesTheExtraShift` (none before, one after), plus I-C2. For "requested leave does not remove days" (U-C3), I read RosterQueryHandlers.cs:93-94: the availability handler loads only `Status == LeaveStatus.Approved` leave, and StaffAvailability.cs:32-34 removes days only for leave it is given. |
| A10 | MET | `AdminGlobalSettingsLiveEffectEndpointTests.ChangeGlobalSetting_AuditsADataChangeRowWithBeforeAndAfterValues` changes a global setting through `PUT /api/admin/settings`. It then searches the audit log for the settings aggregate (entityId = global scope id) and asserts `beforeValue` contains the original value and `afterValue` the new one, restoring the value in `finally`. `ChangeBranchSettingOverride_AuditsADataChangeRowWithTheAfterValue` covers the branch-override path (an insert, so after only). Both run inside the non-parallel settings collection. This replaces the first pass's inference with observation. |

## Other criteria, re-checked briefly

A1, A2, A4, A5, A7, A8, A9, A11-A16 remain MET on the evidence in the first-pass table below. All the named tests still exist and passed in this run. No source changed since the first pass.

Residual partials the TC sheet honestly records remain. None of them is load-bearing for its criterion, since each criterion has other direct evidence:
- A5: no test probes an unminted cat id. RequireActionFilter.cs:75-87 returns one `NotFoundResult` branch for both "not found" and "not owned".
- A7: there is no "exactly once" event spy. The `LeaveApproved` audit row is written only by the StaffLeaveApprovedEvent handler.
- A9: no test checks each setting's default against its own bounds.
- A16: only `<script>` is probed. LimitedMarkup.cs:52 rejects every `<`, `javascript:` and `data:`.

The owner may want the A9 bounds-vs-default check added as cheap debt.

## FINDINGS (second pass)

- none blocking. The three first-pass HIGH findings (A3, A6, A10) are closed by 8bc27ac.
- First-pass LOW (scope): the NullActiveBranchAccessor files are now declared in Amended (48). Closed.
- First-pass LOW (TC count): the Summary now separates 90 full from 10 partial/indirect rows (Amended (49)). Closed.

## SCOPE CHECK (second pass)

- Delta dd495c5..902e65f contains 7 files: 4 test files (tests/** in §13), TC-HO-ADM-001.md, docs/gates/HO-ADM-001-CODE_REVIEW.md (both §13), and docs/handover/HO-ADM-001.md (the Amended rows, the integrator-authorised self-amendment practice). All 7 are in allowed scope; none is outside it.
- Out-of-scope (§4) work: none. There is no appointment flagging, boarding assignment, rate cards, delivery zones or background job, and `[StorefrontEndpoint]` is applied to nothing.

## Owner must know before merging

1. **OQ-4 flag (required by §12.3):** the Cat/Vaccine role mapping is an inference from SRS Appendix D's two rows (ADR-043), not a stated requirement. A3 proves the code matches §12.3's table. It cannot prove the table matches the owner's intent, so confirm it.
2. Tracked for after merge: move NulByteRejectionFilter's global registration from AdministrationModule to Program.cs. HO-ACC-001 should confirm the filter closes its carried `PUT /api/customers/me` fullName NUL item.
3. Operational: the migration needs `btree_gist` (CREATE privilege). `EnsureCreated` omits the leave exclusion constraint.
4. ARCHITECTURE and SECURITY were last run at 8495e32. Later commits were reviewed by CODE_REVIEW delta passes only, by your decision.
5. Accepted residual test debt, none of it criterion-blocking: the A5 unminted-id probe, the A7 event spy, the A9 bounds-vs-default check, and the A16 remaining markup shapes.

---

# First pass (superseded by the above; STATUS was FAIL on A3, A6, A10)

GATE: ACCEPTANCE
HANDOVER: HO-ADM-001
COMMITS: master 7abc9e4 .. ho/ADM-001 31788ce (head; last source/test commit c3dee41, last doc commit f108c5b)
DATE: 2026-09-26
STATUS: FAIL

## Prior gates (read from docs/gates/ and docs/test-cases/, not from commit messages)

| Gate | Artifact | Recorded status | Covers |
|---|---|---|---|
| ARCHITECTURE | HO-ADM-001-ARCHITECTURE.md | PASS (fifth pass) | through 8495e32 |
| SECURITY | HO-ADM-001-SECURITY.md | PASS (fifth pass) | through 8495e32 |
| CODE_REVIEW | HO-ADM-001-CODE_REVIEW.md | PASS (eighth pass, delta) | 86d2e45..f108c5b; sixth/seventh passes cover 8495e32..86d2e45 |
| TEST | HO-ADM-001-TEST.md + TC-HO-ADM-001.md | PASS | f87a02a; sheet amended through f108c5b (Amended 38-44) |

All four gates left artifacts and record PASS. Commits after 8495e32 were reviewed by CODE_REVIEW
delta passes only (ARCHITECTURE/SECURITY not re-run); accepted as the integrator's stated decision.

## Commands run by this gate (worktree D:/my-repo/cat_ecommerce-wt/ADM-001, head 31788ce)

- `dotnet build` -- Build succeeded, 0 warnings, 0 errors.
- `dotnet test --no-build` with `CATECOMMERCE_TEST_DB=Host=localhost;Port=5432;Database=catecommerce_adm;...`:
  Infrastructure.Tests 28/28, Domain.Tests 216/216, Application.Tests 73/73, IntegrationTests 289/289
  -- 606/606 passed, 0 failed, 0 skipped.
- `dotnet dotnet-ef migrations has-pending-model-changes` (Infrastructure project, Api startup) --
  "No changes have been made to the model since the last migration."
- `dotnet dotnet-ef migrations list` against catecommerce_adm -- the one migration this branch adds,
  `20260925185529_AddAdministration`, is present and applied (none pending).
  `MigrationsOnAnEmptyDatabaseTests` (applies every migration to a scratch database) passed.
- `git merge-tree --write-tree master ho/ADM-001` -- exit 0, tree 91fa5749..., no conflicts.

## Criteria (§18)

| # | Verdict | Evidence |
|---|---|---|
| A1 | MET | BranchCalendarTests.OpenIntervals_ClosedDayHasNoInterval / _LunchBreakProducesTwoIntervals / _HalfDayClosure_ShortensInterval (U-B3); ToInstant_LondonSpringForwardGap_* / _LondonFallBackAmbiguity_* (U-B4, DST edges rather than a literal October week); AdminBranchEndpointTests.CreateBranch_SetHours_AddClosure_CalendarReflectsBoth (I-B2). All green in this gate's run. |
| A2 | MET | I-B2 above creates and uses a new branch through the API only; ActiveBranchAccessEndpointTests Mine_* (U-B6/I-B5) prove branch assignment and refusal resolution. |
| A3 | **NOT MET** | U-A1 (EndpointAccessConventionTests.EveryControllerAction_HasExactlyOneAccessMarker) and U-A2 (ActionRolesCoverageTests.EveryRequireActionAttribute_NamesAnActionPresentInActionRoles) prove every endpoint is *declared* and every action name *exists* in ActionRoles. Neither proves the roles *match Appendix D*. That is I-A2's job, and §19 row 4 says "I-A2 is a full matrix, not a sample". TC-HO-ADM-001 marks I-A2 "PASS (large sample, not the literal full matrix)" and its own A3 row says "Partially covered". CatVaccineAuthorizationSecurityTests has 10 tests: anonymous x all endpoints plus about six individual role/endpoint cells. No test covers most role x endpoint cells (e.g. Vet, Nurse, Receptionist, Groomer, Boarding staff on each Cat/Vaccine endpoint). The TEST gate handed this to "ACCEPTANCE / owner" to decide. The handover already decided it, so this is an evidence gap, not a document defect. |
| A4 | MET | AdminAuthorizationDenialAndNulFilterCrossAreaEndpointTests.NurseMarksCatDeceased_Returns403_AndWritesOneAuthorizationDeniedAuditRow (I-A3), plus ShopManagerPublishesPolicyVersion_Returns403_AndWritesOneAuthorizationDeniedAuditRow. |
| A5 | MET | CustomerB_CannotReadCustomerAsCat_Returns404 (other's cat -> 404); CustomerB_RegisteringACat_OwnerIdInBodyIsIgnored_CatBelongsToCaller (I-A4; also lists `?ownerId=<A>` as B and gets B's own cat, so the query owner is forced, I-A5). Indistinguishability: I read RequireActionFilter.cs:75-87. "Not found" and "owned by someone else" fall through the same `NotFoundResult` branch, with no DenyAsync/audit call on either. The unknown-id probe itself is never run by a test (TC U-A5 "partial"). |
| A6 | **NOT MET** | §18 names U-C1…U-C4, I-C1, I-C2 as evidence. U-C2/U-C4 (StaffAvailabilityTests, 2 tests) and I-C2 exist and pass. **Leave's effect on availability is never observed anywhere.** U-C3 (approved vs requested leave) is "PASS (indirect)": the TC row says the availability-side effect "is not watched". I-C1 is "PASS (pattern + leave halves only)": no test requests availability after an approved leave day or with an extra shift. StaffAvailabilityTests contains no leave case. The TC also records U-C1 rules 2, 5 and 7 as having no test at any level. The criterion is "Rosters **and leave** determine staff availability", so the leave half has no evidence. |
| A7 | MET (server half) | StaffLeaveApprovedEventShapeTests.HasExactlyFourDeclaredProperties_NoFreeText (U-C6). AdminRosterLeaveEndpointTests.ApproveLeave_WithDecisionNote_AuditRowIncludesTheNote finds the `LeaveApproved` row, which only AuditLeaveDecisionHandler writes, and only as `IEventHandlerAsync<StaffLeaveApprovedEvent>`, so the event is proven published. LeaveRequestAggregate.Approve (read) populates StartsOn/EndsOn from the aggregate. There is no dedicated "raised once" spy (I-C4 indirect). Flagging itself is correctly deferred to HO-APT-001 (§4.2). Nothing was built for it. |
| A8 | MET | CreateServiceAndPrice_PublicServicesShowsCurrentPriceOnly (U-D2/I-D1); SetServicePrice_Backdated_Returns400 (U-D3); TwoPricesWithSameEffectiveDate_SentConcurrently_OneCreatedOneRejected_NeverTwoRows (I-D2). |
| A9 | MET (with gap) | GetSettings_ReturnsEveryRegistryKeyWithBoundsAndEffectiveValue (I-D6); ChangeSettings_InvalidValue_NothingApplied (U-D4 mixed list); All_RealAdministrationRegistry_HasNoDuplicateKeys. Gap: no test checks each default against its own bounds (U-D6), and U-D4's unknown-key, out-of-bounds and non-overridable-override codes are not individually tested. |
| A10 | **NOT MET** | The named evidence is I-D5, and TC-HO-ADM-001 records it as "PASS (indirect)… not directly re-verified… not independently watched for a settings change". A search confirms that no test changes a setting and then looks for its DataChange row or before/after values. BusinessSettingsAggregate stores values as an owned collection. AuditChangeCaptureInterceptor only snapshots a root in state Added/Modified. Whether a settings change audits correctly therefore depends on UpdateVersion() marking the root Modified. That is a plausible inference, not evidence. US-ADM-05 AC2 is unproven. |
| A11 | MET | AdminGlobalSettingsLiveEffectEndpointTests.ChangeVaccinationDueSoonDays_CatVaccinationStatus_ReflectsTheNewValueImmediately (I-D3); ChangeStaffIdleTimeoutMinutes_IdleSession_TimesOutAtTheNewValue (I-D4, asserts `idleTimeoutMinutes: 5`). |
| A12 | MET | SearchByActor_FindsTheChangeItMade, SearchByCategory_LowerCase_*, the entityType/entityId/action/from/to search in ApproveLeave_WithDecisionNote_* (I-E1); ExportCsv_ReturnsCsvResponseWithHeaderRow, ExportCsv_WritesOneAuditLogExportedRow_FindableBySearchingForIt, ExportCsv_ReasonBeginningWithAFormulaCharacter_IsEscapedWithALeadingApostrophe x4 (I-E2/U-E1). |
| A13 | MET | CreatePublishEditDraft_PublicReadReturnsPublishedNotDraft (I-F1), Unpublish_PublicReadReturns404 (I-F2), GetBanners_OutsideVisibilityWindow_IsExcluded_InsideWindow_IsReturned (I-F3), FAQ create/publish/read in AdminContentPolicyEndpointTests (lines 56-63), PublishPolicyVersion_* (policies). |
| A14 | MET | PublishPolicyVersion_CustomerAcceptsThenPendingIsEmpty_LaterVersionReopensPending (I-F4/I-F5), AcceptPolicyVersion_NoLongerCurrent_Returns400PolicyVersionNotCurrent (U-F5). |
| A15 | MET | MaintenanceMode_NeverBlocksAdminApiOrStaffSignIn (I-F7). |
| A16 | MET | ContentWithScriptTag_Returns400InvalidContentMarkup (I-F6). LimitedMarkup.cs:52 (read) rejects any `<`, `javascript:` or `data:` anywhere in the body. That one rule covers every HTML shape, so the untested `<b>` case goes through the same branch. |

Three criteria (A3, A6, A10) lack concrete evidence, so PASS is forbidden.

## FINDINGS

- [HIGH] tests/CatEcommerce.IntegrationTests/Endpoints/CatVaccineAuthorizationSecurityTests.cs -- A3 / §19 row 4: I-A2 must be the full Cat/Vaccine endpoint x (anonymous, customer-own, customer-other, each of the 8 staff roles) matrix. It is a sample. Fix: one data-driven theory over the §12.3 table asserting every cell.
- [HIGH] tests (none exist) -- A6: no test observes leave (approved vs requested) changing staff availability, or an extra-shift exception adding to it (U-C1 rules 2/5/7, U-C3, I-C1). Fix: add the missing StaffAvailabilityTests cases plus an I-C1 test that approves a leave day and re-queries `/availability`.
- [HIGH] tests (none exist) -- A10: I-D5 was never executed. Fix: change a setting via `PUT /api/admin/settings`, then search the audit log for the DataChange row on the settings aggregate and assert before/after values. The test must join AdministrationGlobalSettingsCollection, or change a branch override on a test-minted branch.
- [LOW] TC-HO-ADM-001.md "Summary" counts `PASS (partial)` / `PASS (indirect)` rows as PASS. That makes the 100/100 figure hide three criterion-level gaps, and the sheet's own §18 table admits them ("Partially covered"). TEST's PASS verdict and this sheet disagree.
- [LOW] Scope: src/Infrastructure/CatEcommerce.Infrastructure/Fakes/Administration/NullActiveBranchAccessor.cs and tests/CatEcommerce.Infrastructure.Tests/Fakes/Administration/NullActiveBranchAccessorTests.cs (commit 2e56210) are outside §13 and have no Amended row. They are the mechanical consequence of the declared IActiveBranchAccessor contract change (Amended (11), CONTRACTS.md #20 names this fake), but they are undeclared. src/Domain/.../DomainServices/IActiveBranchAccessor.cs is also not in §13's DomainServices list, though §8.3/§8.9 specify it. Record them in an Amended row.

## SCOPE CHECK

- out-of-scope (§4) work found: none. No appointment flagging, boarding assignment, rate cards, delivery zones or background job. `[StorefrontEndpoint]` is applied to no endpoint (AdministrationModule.cs:74).
- files outside §13 declared in Amended rows or integrator-accepted: RecordClassification.Administration.cs, ISettingsCacheInvalidator.cs, AdministrationPersonalDataEraser.cs, AdminPoliciesController.cs, InMemoryBusinessSettings.cs, CatEcommerce.Domain.csproj (owner-authorised), the pre-existing HO-CAT/HO-ACC test files incl. StaffSessionEndpointTests.cs, Filters/NulByteRejectionFilter.cs, Controllers/{ControlCharacterGuard,EnumQueryParameter,TruncationHeader}.cs, Shared helpers/constants named in Amended (29)-(35), AuditExportOptions.cs + appsettings.json (38), docs/handover/{CONTRACTS,README,HO-ACC-001,HO-XCT-001,HO-ADM-002}.md (integrator-authorised, (14)/(27)).
- files outside §13 treated as integrator-convention equivalents: Modules/AdministrationModule.cs (for Program.cs §12.7), Persistence/CatEcommerceDbContext.Administration.cs (for CatEcommerceDbContext.cs "DbSets only"). Both follow the per-area file pattern already on master.
- files outside §13 and undeclared: NullActiveBranchAccessor.cs, NullActiveBranchAccessorTests.cs (LOW above).
- HO-ACC-001 controllers (Auth, Customers, StaffAuth, StaffSession, Captcha): diff is `[PublicEndpoint]` markers, `using` lines and doc comments only, as §13 permits.

## Owner must know before merging

1. Do not merge on this verdict. A3, A6 and A10 each need tests (above). No source change is implied unless the A10 test finds that settings changes are not actually audited.
2. If the owner decides a sample is acceptable for I-A2, that is an amendment to §19 row 4. Record it in the handover. This gate cannot waive it.
3. Tracked for after merge (integrator): move NulByteRejectionFilter's global registration from AdministrationModule to Program.cs. HO-ACC-001 should confirm the filter closes its carried `PUT /api/customers/me` fullName NUL item (Amended (32)).
4. Operational: the AddAdministration migration needs `btree_gist` (CREATE privilege). `EnsureCreated` omits the leave exclusion constraint (docs/handover/README.md operational notes).
5. ARCHITECTURE and SECURITY were last run at 8495e32. Everything after that was reviewed by CODE_REVIEW delta passes only, by the integrator's decision.
