# HO-ADM-001 — Administration and Configuration (backend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-ADM-001** |
| Title | Administration and Configuration — backend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §14 — **US-ADM-01 … US-ADM-08** |
| SRS | [`docs/SRS.md`](../SRS.md) v2.3 — **FR-ADM-1 … FR-ADM-8**, FR-CLN-1 (service fields), FR-CLN-3 (the inputs to availability), **NFR-SEC-2**, NFR-SCL-1, NFR-LEG-5, UI-4, C-7, C-9, A-1, Appendix D |
| Pairs with | [`HO-ADM-002`](HO-ADM-002.md) — the frontend. **§12.4 of this document is its contract** |
| Builds on | [`HO-ACC-001`](HO-ACC-001.md) (staff accounts, roles, sessions, `[RequireAction]`, `ActionRoles`, audit change capture) and [`HO-CAT-001`](HO-CAT-001.md) (foundation, Cat endpoints) |
| Delivery order | Section 3 of 17 in [`README.md`](README.md#delivery-order) |
| Baseline commit | `4007225` |
| Amended | **2026-09-22 — by [`HO-NTF-001`](HO-NTF-001.md), before implementation.** `notifications.quietHours` gains its consumer, and two keys are appended to the settings registry (§10.2): `notifications.vaccinationReminderLeadDays` (14) and `notifications.dewormingReminderLeadDays` (7). ADR-047's "no background job" still holds for Administration; HO-NTF-001 adds the outbox worker that `.ai/domain-events-and-outbox.md` §4 already names (ADR-060, ADR-065). **2026-09-22 — by [`HO-APT-001`](HO-APT-001.md), before implementation.** Seven settings are appended to the registry (HO-APT-001 §10.2), and `booking.slotIntervalMinutes` gets a "divides 60" bound. `booking.changeCutoffHours`, `booking.slotHoldMinutes` and `booking.waitlistClaimMinutes` gain their consumer. The leave and closure events are subscribed to as ADR-048 intended (HO-APT-001 §8.10). **2026-09-26 — self-amendment, this handover's own build.** (1) **Build order**: a parallel HO-CAT-001 SECURITY gate proved live, while this build was already in progress, that master had no authorization at all on any of the 19 Cat/Vaccine endpoints. Phase A (§17) was built, tested and committed first and in isolation (every later-phase file temporarily set aside, built and tested on its own, then restored) so that fix landed as its own reviewable commit ahead of Phases B-F; §17's own phase order is otherwise unchanged. (2) **Settings actually built now**: of this document's own Amended rows above, only the two HO-NTF-001 keys (`notifications.vaccinationReminderLeadDays` (14), `notifications.dewormingReminderLeadDays` (7)) are added to the registry in this build, because their key and default are given verbatim here. HO-APT-001's "seven settings" and every setting named in the 2026-09-23 "owed rows" paragraph below (billing, inventory, pharmacy, boarding, files, shop, surgery, subscription) name only a count or a group, never a key, type, bound or default -- inventing them would not be implementing this document, so they are left for each named handover to add itself, in its own change, when it lands. `BranchCalendar.LocalDateOf` (also named below) was likewise not added -- no caller of it exists yet in this build. (3) **§12.1's five migrations are built as one**, `AddAdministration`, covering Phases B-F's whole schema in a single migration -- an explicitly authorised simplification (the parallel-build plan's own instruction to this branch), not an oversight; the phase boundaries are still visible in the model (one root/config file per phase) even though the migration itself is not split. (4) **Out-of-§13 files, each because HO-XCT-001's own §5.2/§5.3 already assigns this handover the duty in the same change that adds the table it applies to, ahead of HO-XCT-001 itself landing**: `Shared/Common/Constants/RecordClassification.Administration.cs` (C-ALL-1, classifying every table §8.1 adds); `Domain/DomainServices/ISettingsCacheInvalidator.cs` (a small port so the Application-layer cache-invalidation handler never takes a forbidden Application -> Infrastructure reference to `CachedBusinessSettings`, the same reason `ISessionPolicy` exists); `Infrastructure/Retention/Erasers/AdministrationPersonalDataEraser.cs` and its `AddScoped<IPersonalDataEraser, ...>()` registration (C-ALL-2 -- `PolicyAcceptanceAggregate.IpAddress` is a customer's personal data beyond the id; `CONTRACTS.md`'s "Cross-cutting duties" section already names this exact eraser as owed to this handover -- **corrected 2026-09-26, gate fix**: this originally, wrongly, cited "HO-XCT-001 §5.3" for this; HO-XCT-001's own §5.3 amendment rows are for *other* handovers' §1 files, not a duty-assignment section in HO-XCT-001 itself, and CONTRACTS.md's "Cross-cutting duties" section is the real, correct authority; see the matching Amended row in `HO-XCT-001.md` §1). The eraser's retention period is a fixed constant (6 years, NFR-LEG-5's acceptance-evidence expectation), not `retention.financialYears`, because that setting is HO-XCT-001's own addition to this registry when it lands (§5.3) and does not exist yet -- rewiring it is HO-XCT-001's change, not this one. (5) `Infrastructure/Configuration/CachedBusinessSettings.cs` also carries `MemoryCacheSettingsCacheInvalidator` and `BusinessSettingsSessionPolicy` (the HO-ACC-001 §12.2 idle-timeout rewire) in the same file, rather than new files, since both are small and directly related to the class already named for that file in §13. `BusinessSettingsSessionPolicy` is registered as `ISessionPolicy` in `AdministrationModule`, which runs after `AccountsModule` in `Program.cs`'s existing module order -- the later registration wins the direct (non-`IEnumerable`) resolution, so `AccountsModule.cs` itself is never edited. (6) **Pre-existing HO-CAT-001/HO-ACC-001 integration test files** (`tests/CatEcommerce.IntegrationTests/{CatApiFactory.cs,CatIntegrationTestBase.cs,Endpoints/AuditImmutabilityTests.cs,Endpoints/DuplicateCustomerGroupsEndpointTests.cs,Endpoints/ParasiteTreatmentEndpointTests.cs,Endpoints/StaffEndpointTests.cs}`) were edited: the first four only to authenticate before calling a Cat endpoint that Phase A now guards (they previously called it anonymously, which is exactly the gap Phase A closes); `StaffEndpointTests`'s customer-calls-staff-endpoint case now expects `403` rather than `401`, per ADR-043's redesign (a customer is authenticated, merely not authorized, unlike an anonymous caller). No other assertion in any of the six files changed. **2026-09-26 — gate fix round (self-amendment, all three first-pass gates FAILED at `08e1dd2`; this row records the fix commits).** (7) **Two files edited in the original build (commits `85fa83b`…`621e537`), out of §13's explicit list, never previously recorded**: `src/Api/CatEcommerce.Api/Controllers/AdminPoliciesController.cs` (§13 lists `Policies` -- the public read-only controller -- in its controller set, but not `AdminPoliciesController`; the admin CRUD surface for policy documents/versions was built without ever being added to that list); `src/Infrastructure/CatEcommerce.Infrastructure/Fakes/Administration/InMemoryBusinessSettings.cs` (§13 does not name `Fakes/**` at all; `GetString` was added to it, alongside the real `IBusinessSettings.GetString` the interface itself already documents as HO-ADM-001's own addition for `SettingType.CurrencyCode`, ADR-054, so the fake would still compile against the widened interface). Neither changed any other handover's behaviour; both are recorded here as the gate's own MEDIUM finding asked for, rather than reverted. (8) **OWNER DECISION (a), audit redaction.** `GetAuditEntriesQueryHandler` redacts `beforeValue`/`afterValue` for a clinical entity (`CatAggregate`, `CatWeightRecordAggregate` -- every table HO-CAT-001's own `RecordClassification.CatProfiles` marks `RecordClass.Clinical`) unless the caller also holds `Cats.View`; an Accountant (`AuditLog.View`) sees who touched a cat record, when, from where, never its clinical content -- **corrected 2026-09-26, gate fix**: a plain Vet holds `Cats.View` but not `AuditLog.View` (Administrator and Accountant only), so a Vet cannot reach the audit log at all; only an Administrator, or a multi-role holder of both actions, sees the clinical content there. (9) **OWNER DECISION (b), policy publishing.** `PublishPolicyVersionCommandHandler` refuses (`ActionNotPermitted`) a non-Administrator publishing a version of a policy that `RequiresAcceptance` (Terms of Service, Privacy Policy) -- `Content.Manage` alone, which a Shop Manager also holds, still governs creating a policy document and every other content action. (10) **BLOCKER.** `BranchCalendar.OpenIntervals` folds every matching same-day partial closure (was `FirstOrDefault`, keeping only the first). (11) **HIGH.** The two HO-NTF-001 settings keys were removed from `SettingDefinitions.Administration.cs` again (item (2) above's "actually built now" is superseded -- on review, pre-building another area's own-key-and-default setting was itself the wrong call, not merely early). `BranchCalendar.LocalDateOf` was added (ADR-045) and is now used by `BranchClosureCommandHandlers`/`RosterCommandHandlers` in place of inline `TimeZoneInfo` conversions; `BranchCalendar.ToInstant` is now documented and DST-safe for the invalid/ambiguous local time cases. `IActiveBranchAccessor.GetActiveBranchId()` now returns `ActiveBranchResult` (a `BranchId`/`Refusal` pair), not a bare `Guid?`, so a bad or out-of-assignment `X-Branch-Id` header is refused instead of silently resolving to the primary branch -- see the matching `CONTRACTS.md` #20 edit. `GET /api/staff/branches/mine` now requires the new `Branches.ViewOwn` action (every staff role) instead of `[PublicEndpoint]`, and returns the caller's resolved `activeBranchId`. (12) **MEDIUM.** `MaintenanceModeFilter`/`[StorefrontEndpoint]` (both named in §12.5/§12.7/ADR-053, never actually built) now exist, registered once in `AdministrationModule`; ADR-053's text corrected to stop describing them as merely "named for later". `PolicyAcceptanceAggregate.Create` now raises `PolicyVersionAcceptedEvent` (never did, despite being declared) and its handler's save publishes it. `UniqueConstraintExceptionFilter` gained every §12.5 mapping (branch code, room name, service code, price/rate effective-date, content slug, the three policy indexes, the staff roster index, a settings-scope first-write race) plus a `23P01` mapping for a new database-level guarantee: a GiST exclusion constraint (`ex_leave_request_staff_no_overlap`, `btree_gist`) added to the `AddAdministration` migration stops two concurrent leave requests for the same staff member ever both committing with overlapping dates, closing a race the handler's own pre-check cannot. Audit search `pageSize` is capped at `AuditExportRules.MaxSearchPageSize` (200; export keeps 50,000). Six previously-unbounded admin lists (my-leave-requests; branch closures/roster exceptions with no date range; a service's price history; a tax class's rate history; the policy list with its version history) are capped at `AdministrationListCaps.MaxUnboundedListSize` (100) -- a hard cap rather than full `PagedResult` paging for all six, a judgment call given none is expected to legitimately exceed a few dozen rows. `GetAuditEntriesQueryHandler` converted from materializing every matching row then two extra dictionary queries to one SQL left-join projection. (13) **LOW, all fixed:** the CSV-injection guard also treats a leading tab/CR/LF/space as "leading" before checking for `=+-@`; the audit export's own audit-write result is checked (was ignored) and now records the caller's IP (was hardcoded `null`); `to < from` is `400` on both audit search and export; `RequireActionFilter`'s customer `OwnerScope.Cat` path now fails closed on a missing/unparsable `catId` (the two genuinely id-less actions, register and list, opt in explicitly via the new `OwnerScope.CatCollection`); `EndpointAccessConventionTests` (U-A1) now discovers controllers via ASP.NET Core's own `ControllerFeatureProvider`, not a hand-rolled `ControllerBase` filter, so a POCO controller would also be caught; an unrecognised enum query parameter (banner placement, audit category, leave status, content kind) now `400`s via the new `EnumQueryParameter.TryParse` helper instead of throwing its way to an unhandled `500`; `LimitedMarkup.Validate` rejects a NUL byte (or any other C0 control character besides tab/LF/CR); `SettingDefinitions.LoadAll` (renamed `BuildRegistry`, now testable against a throwaway `Type` like `ActionRoles.BuildTable`) now throws on a public field or a property of the settings-contribution shape instead of silently dropping it; `Actions.System` renamed to `Actions.SystemStatus` (was shadowing the `System` namespace); ADR-042 corrected from fourteen to the actual seventeen aggregate roots (each now named); `AuditLeaveDecisionHandler`'s row now actually carries the decision note (previously always `null` despite its own doc comment); `AdminBranchesController.CreateClosure` no longer parses each `TimeOnly` twice; `BranchCalendarTests`' two dead locals removed; ADR-050 documents `CachedBusinessSettings`' single-host assumption. (14) **RECORDS** (integrator-authorised): `CONTRACTS.md` #18 documents `GetString` and the fake/real unknown-key divergence; #20 documents the active-branch accessor's refusal contract. Matching Amended rows added to `HO-ACC-001.md` §1 (ADR-043's `401`→`403` redesign, correcting its own §12.8/I-F3 text) and `HO-XCT-001.md` §1 (the eraser/table-name correction above). **2026-09-26 — CODE_REVIEW gate second pass (self-amendment).** (15) **HIGH, completed.** The CODE_REVIEW HIGH finding named the whole set of Administration query handlers, not the one handler (12) above already fixed -- every handler under `src/Read/CatEcommerce.Read/QueryHandlers/Administration/` that materialised a full aggregate (or looped a query per row, N+1) before mapping now projects with `Select`/`AsNoTracking` in the same query instead: `BranchQueryHandlers` (`GetBranches`, `GetAdminBranches`, `GetAdminBranch` -- each projects the nested `WeeklyHours` owned collection too; `GetStaffBranchAssignment`/`GetMyBranches`); `ClinicServiceQueryHandlers` (`GetAdminServices`/`GetAdminService` -- the N+1 `isPriced` lookup becomes one correlated subquery per row; `GetServicePrices`); `RosterQueryHandlers` (`GetBranchRosters` -- both nested shift-slot collections, `CurrentPattern` and `NextPattern`, `AsSplitQuery()`'d to avoid the cross-product two sibling collections in one query would otherwise produce; `GetRosterExceptions`); `GetSettingsQueryHandler` (projects `BusinessSettingsAggregate.Values` directly, not the whole aggregate); `ContentQueryHandlers` (`GetAdminContent`/`GetAdminContentItem` via a shared raw-row type; `GetPublishedContent`/`GetPublishedBanners`/`GetPublishedFaqs`; `GetAdminPolicies` -- the per-document N+1 versions lookup becomes one correlated-subquery projection; `GetPublicPolicy`; `GetPendingPolicyAcceptances` -- the per-document N+1 `AnyAsync`+`FirstOrDefaultAsync` pair becomes one query; `GetSystemStatus`). `GetAuditEntriesQueryHandler` was already fixed in (12). Left unconverted, deliberately: `GetStaffAvailabilityQueryHandler` (§12.6: "load inputs, call the pure domain services... never re-implement the calendar in SQL" -- `StaffAvailability.Intervals` takes the actual aggregates as its domain-service input, not a ViewModel, so there is no aggregate-to-ViewModel mapping to project away); `GetBranchCalendarQueryHandler` (no database access at all, calls `IBranchCalendar`); `GetLeaveRequestsQueryHandler`/`GetMyLeaveRequestsQueryHandler` and the tax-class/room/tax-rate handlers already projected before this pass. Response shape is unchanged everywhere; the existing integration tests (which assert response content, not just status codes, per §16.1) are the proof, joined by one new test with actual nested-collection content, `AdminRosterLeaveEndpointTests.GetBranchRosters_MultipleShiftSlotsAcrossDays_ProjectionReturnsExactNestedContent` (three shift slots across three different days). (16) **The six `AdministrationListCaps`-capped lists (accepted, per (12) above)** now also signal a possible truncation with a response header, `X-Truncated: true`, set when the returned count equals the cap exactly (`TruncationHeader.ApplyIfAtCap`, wired into the five flat list endpoints: branch closures, roster exceptions, my-leave-requests, service prices, tax rates; and the outer document list of `GET /api/admin/policies`, though not its per-document nested version cap, which cannot be attributed to one document via a single header without changing the response shape) -- recorded in [`ADR-051`'s 2026-09-26 addendum](../../.ai/decisions/adr.md#adr-051--audit-log-read-and-export-are-read-only-and-self-auditing) -- **corrected 2026-09-26, gate fix**: this originally created a new `ADR-055` heading, but `docs/handover/README.md`'s ADR-numbering table allocates `ADR-055…066` to HO-NTF-001 and §13 limits this branch's `adr.md` edits to `ADR-042…054`, so the decision is folded into ADR-051 (the closest existing precedent for capping a read-side list) instead of a new heading. Proven by `AdminBranchEndpointTests.GetClosures_MoreThanTheCap_TruncatesAndSignalsWithHeader` (creates 101 closures, asserts the header and the 100-row cap). **2026-09-26 — ARCHITECTURE/SECURITY gate third pass (self-amendment).** (17) **BLOCKER, owner-authorised.** `src/Domain/CatEcommerce.Domain/CatEcommerce.Domain.csproj`'s `<InternalsVisibleTo Include="CatEcommerce.Domain.Tests" />` (added in commit `2e56210`) is a `*.csproj` edit, §14 forbidden scope, with no Amended row until now -- the owner authorised this line on 2026-09-26 so `BranchCalendarTests` can exercise the `internal` `BranchCalendar.ToInstant` directly; recorded here as that authorisation. (18) **SEC MEDIUM #3 / ARCH MEDIUM #3, closed.** The hard-coded, two-entry `ClinicalEntityTypes` list in `GetAuditEntriesQueryHandler` (item (8) above) is replaced by `Shared.Common.Constants.ClinicalAuditEntityTypes`, a reflection-aggregated registry (same `BuildRegistry(Type)`/throw-on-public-field-or-property discipline as `ActionRoles.BuildTable`/`SettingDefinitions.BuildRegistry`) so a later clinical area (HO-EMR-001, HO-PHM-001, HO-SRG-001) registers its own audited entity types in its own `ClinicalAuditEntityTypes.[Area].cs` file instead of needing this Administration-owned file edited. `ClinicalAuditEntityTypes.CatProfiles.cs` is written on HO-CAT-001's behalf (the same "ahead of that area's own change" precedent as the eraser), naming `CatAggregate`, `CatWeightRecordAggregate` and `Cat` — the last one closes a real gap: the four `Cat`-labelled `BusinessEvent` rows a high-risk flag, clear, ownership transfer or merge reassignment writes were never redacted, leaking a handling note and every free-text reason to an Accountant. **Decision recorded here per the gate's own request:** all four `Cat` actions are redacted uniformly, not split by `Action` — an Accountant has no legitimate need to read any per-cat operational reason, and a transfer's reason can incidentally carry clinical detail a split rule would need to know about in advance. `Reason` is now redacted alongside `BeforeValue`/`AfterValue`. CONTRACTS.md is not amended for this registry — it is a Shared-layer, ADR-341-style aggregation, not a cross-area port. **2026-09-26 — ARCHITECTURE/SECURITY gate third pass, remaining items (self-amendment).** (19) **ARCH MEDIUM.** `GET /api/staff/branches/mine`'s own payload never needed an active branch (ADR-046); it previously turned a refused `X-Branch-Id` into a hard `403`, breaking the one endpoint a stale header should let the frontend recover through. It now always returns `200` with `branchIds`/`primaryBranchId`, plus `activeBranchId` (`null` when refused) and `activeBranchRefusal` (the refusal code, or `null`) alongside — never a `403` for this reason. (20) **SEC LOW.** `EnumQueryParameter.TryParse`'s canonical `DisplayName` is now passed on to every query it feeds (content kind, banner placement, audit category, leave status) instead of the caller's raw-cased string — the handler's `Enumeration.FromDisplayName` is case-sensitive, so e.g. `?placement=home` previously still 500'd past the 400 guard. (21) **SEC LOW.** A NUL byte or other C0 control character in a content/policy title, a policy's change summary, a branch name or the maintenance message now 400s (`ValidationRuleExtensions.NoControlCharacters`, one shared FluentValidation rule) instead of reaching Postgres unfiltered; the audit log's free-text `entityType`/`action` filters get the same check via `ControlCharacterGuard` (no command validator applies to a query-string filter). `pageNumber` on the audit search is capped at 1,000,000 so `(pageNumber-1)*pageSize` cannot overflow `int` (a live `500` from `Skip` overflow). HO-ACC-001's `PUT /api/customers/me` `fullName` carries the same class of gap and is out of this handover's scope — not changed here, per the gate's own instruction. (22) **SEC LOW.** `EndpointAccessConventionTests` (U-A1) drops `BindingFlags.DeclaredOnly`, so an action inherited from an abstract base controller (declared on the base type, not the concrete one MVC routes) is checked too — previously invisible to the test while still live and answering anonymously. (23) **SEC LOW.** Creating or publishing a policy document under a reserved key (`terms-of-service…`, `privacy-policy…`, case-insensitive prefix, `Shared.Common.Constants.ReservedPolicyKeys`) is Administrator-only regardless of `RequiresAcceptance` — closes a bypass where a Shop Manager could create the real Terms/Privacy key with `RequiresAcceptance: false`, entirely skipping owner decision (b)'s check, publish it, and permanently squat the key (there is no endpoint to change a document's key or flag). (24) **SEC hardening.** The CSV export sets `Cache-Control: no-store`. (25) **ARCH LOW / CR LOW.** `MaintenanceModeFilter` now dispatches `GetSystemStatusQuery` via `IInMemoryDispatcher` (the same precedent `RequireActionFilter` set with `GetCatOwnerQuery`) instead of reading `CatEcommerceDbContext` directly. `GetStaffAvailabilityQueryHandler` now selects only `TimeZoneId` from `BranchAggregate` instead of the whole aggregate — the only field it uses. (26) **ARCH LOW.** A concurrent second acceptance of the same policy version now maps `ix_policy_acceptance_customer_key_number`'s race to `204` (`UniqueConstraintExceptionFilter`), matching §8.8's "accepting twice is a no-op" and the handler's own sequential-path result, instead of `400 PolicyAlreadyAccepted` disagreeing with it. (27) **ARCH LOW.** ADR-043 records `OwnerScope`'s third member, `CatCollection`. `docs/handover/README.md` gains an "Operational notes" section for `btree_gist`'s `CREATE` privilege requirement and the `EnsureCreated`-omits-the-constraint gap. §8.9's stale `IActiveBranchAccessor`/`IBusinessSettings` rows are marked superseded in place, pointing at Amended (11)/CONTRACTS.md rather than being rewritten (a body edit is not itself a scope or contract problem, per the gate). **2026-09-26 — HO-ADM-001 fourth fix round (self-amendment).** (28) **OWNER DECISION, supersedes decision (b) and item (23).** Every policy document's create AND publish is now Administrator-only, for **every** key, regardless of `RequiresAcceptance` — not just the keys owner decision (b) named (Terms/Privacy) and not just the `terms-of-service…`/`privacy-policy…` prefixes item (23)'s `ReservedPolicyKeys` reserved. The gate proved that prefix set did not match any key the spec actually uses (`terms`, `privacy`, `boarding-terms`, HO-EMR-001's `consent-*`), so a Shop Manager could squat every real policy key with `requiresAcceptance:false` and there is no edit/delete endpoint to undo it. `ReservedPolicyKeys.cs` is deleted (dead code) and the in-handler Administrator role check is deleted from `CreatePolicyDocumentCommandHandler`/`PublishPolicyVersionCommandHandler` — orchestration handlers do not decide authorization (rule 20). In its place, a new action, `Policies.Manage` (`Actions.Administration.cs`, `ActionRoles.Administration.cs` — Administrator only), guards `AdminPoliciesController.Create`/`PublishVersion` via `[RequireAction]`; `AdminPoliciesController.Get` (list) stays on `Content.Manage`, since the decision concerns only create/publish and a Shop Manager still needs to see the policy list to manage ordinary content alongside it. (29) **SEC LOW, NUL 500s, remaining fields.** The content `Slug`'s own format regex lived only inside `ContentItemAggregate.Create`, called *after* the handler's `ExistAsync` uniqueness pre-check already sent the NUL-containing slug to Postgres (22021, uncaught). Fixed the general way: the slug/key format check now runs in the FluentValidation validator itself (`Shared.Common.Helpers.SlugFormat`, the exact regex `ContentItemAggregate` already used, `^[a-z0-9]+(-[a-z0-9]+)*$`, ≤ 80 chars — the aggregate now calls the same shared predicate instead of its own private copy), which the platform's dispatch pipeline runs strictly before the handler, so no pre-check ever sees the malformed value. Applied to: the admin policy `Key` (create); the customer-reachable `POST /api/customers/me/policy-acceptances` `policyKey` (`AcceptPolicyVersionCommand`); the content `Slug` (create). A parallel `Shared.Common.Helpers.CodeFormat` (`^[A-Za-z0-9]+(?:[-_][A-Za-z0-9]+)*$` — ASCII letters/digits/hyphen/underscore, matching the uppercase-alphanumeric codes already in use, e.g. branch code `T1A2B3C4`) is applied the same way to `Branch.Code`, `TaxClass.Code` and `ClinicService.Code`. Free-text fields with no format of their own get `NoControlCharacters()`: `Branch.AddressLine`, `Room.Name`, `TaxClass.Name`, `ClinicService.Name`/`Description`. (30) **SEC LOW, pageNumber overflow.** `GET /api/admin/leave-requests?pageNumber=2147483647` 500'd the same way audit search once did — `(pageNumber-1)*pageSize` overflows `int`, producing a negative `OFFSET` Postgres rejects. The audit search's own local `MaxPageNumber` constant is replaced by one shared `Shared.Common.Constants.PagingRules.MaxPageNumber` (1,000,000), applied at the controller in both `AdminAuditEntriesController` and `AdminLeaveRequestsController` — the only two paged admin endpoints that accept a caller-supplied `pageNumber` (every other list in §12.4 is either unpaged, capped by `AdministrationListCaps`, or bounded by a date range). (31) **ARCH LOW, control-character scan consolidated.** The C0-control-character predicate existed three times (`Api.Controllers.ControlCharacterGuard`, `Application.CommandHandlers.Validators.ValidationRuleExtensions.ContainsDisallowedControlCharacter`, and `LimitedMarkup.Validate`'s own inline loop). One copy now lives in `Shared.Common.Helpers.ControlCharacters.ContainsDisallowed`, and all three call sites delegate to it. Also: `ClinicalAuditEntityTypes.cs`'s doc comment no longer names a Read-layer type via `<see cref>` (now plain `<c>` text, since Shared must never reference upward even in a comment); the ADR-051 addendum no longer names `ADR-055` (HO-NTF-001 will own that number); §8.8's "no-op `200`" is corrected to `204`; §12.4's `/mine` row now documents `activeBranchId`/`activeBranchRefusal`. **2026-09-26 — HO-ADM-001 fifth fix round, "stop the whack-a-mole" hardening (self-amendment).** (32) **SEC LOW, one API-wide NUL guard.** Rather than continue fixing one free-text field at a time (six more were found this pass: leave `note`, leave `decisionNote`, roster-exception `reason`, branch-closure `reason`, branch `phoneNumber`, content `linkUrl`), a single global action filter, `Api.Filters.NulByteRejectionFilter`, now rejects a NUL byte anywhere in a bound request (recursively, over every string reachable from a `[FromBody]` object, bounded to types in this solution's own namespace) with a `400` in the platform's normal `ValidationResponse` shape. It is registered as a global `MvcOptions` filter from `AdministrationModule` — the same precedent `MaintenanceModeFilter` already set — so it applies to every controller in the host, not only Administration's own, without touching `Program.cs`. The existing per-field `NoControlCharacters` rules are kept, not for a NUL byte specifically -- the filter is an action filter and always runs first, so for a NUL byte it is the per-field rule that never fires -- but for every *other* disallowed C0 control character (BEL, ESC, VT, FF and so on) that the filter does not check for; the six newly-named fields also gained their own rule for that reason (**corrected 2026-09-26, fifth-pass gate fix**: this originally, wrongly, said the per-field rule gives "a field-specific error" for a NUL byte). One pre-existing test's expected error code changed as a direct, documented consequence: `AdminContentPolicyEndpointTests.ContentWithNulByte_Returns400_ViaGlobalNulGuard` (renamed from `...ReturnsInvalidContentMarkup`) now expects the filter's generic `ContainsControlCharacter` code instead of `InvalidContentMarkup`, since the filter now intercepts before `LimitedMarkup`'s own check ever runs; every other markup violation (script tags, disallowed links) is unaffected and still produces `InvalidContentMarkup`. This is expected to also close HO-ACC-001's carried `PUT /api/customers/me` `fullName` NUL 500 (P13) as a side effect, since the filter is global — not verified here (out of this handover's file scope to test), flagged for HO-ACC-001 to confirm and drop its own carried item if so. (33) **SEC LOW, request-body enum values.** Six command fields (`CreateContentItemCommand.Kind`/`Placement`, `EditContentDraftCommand.Placement`, `CreateRoomCommand`/`UpdateRoomCommand.Kind`, `CreateBranchClosureCommand.Kind`, `CreateRosterExceptionCommand.Kind`, `CreateClinicServiceCommand`/`UpdateClinicServiceCommand.Category`/`RequiredRoomKind`, `RequestLeaveCommand.Kind`) went straight from the request body to `AdministrationMapper`'s `Enumeration.FromDisplayName`, which is case-sensitive and throws on no match — the query-string side of this exact bug was fixed in an earlier pass (`EnumQueryParameter`), the body side never was. Fixed with one shared implementation, `Shared.Common.Enums.EnumerationParsing.TryParse` (case-insensitive lookup by `DisplayName`), used from three places: `EnumQueryParameter` (refactored to delegate, was its own copy), a new FluentValidation rule `ValidationRuleExtensions.MustBeValidEnumeration<TCommand,TEnum>` added to each of the six commands' validators (unknown/wrongly-cased value now `400`s before the mapper ever runs), and `AdministrationMapper` itself (`ParseEnum`/`ParseNullableEnum`, replacing every `Enumeration.FromDisplayName` call), so a valid case variant (`"page"`, not `"Page"`) succeeds instead of still throwing past the validator. (34) **SEC LOW, format-regex anchor.** `SlugFormat`/`CodeFormat` (Amended (29)) used `^…$`, and in .NET `$` also matches just before a final `\n` — `"about\n"` passed the format check and reached the database as a distinct row from `"about"`. Both are now anchored `\A…\z`, matching only the true start/end of the string. (35) **CR LOW, leave-requests `pageSize`.** `AdminLeaveRequestsController.Get` capped `pageNumber` (Amended (30)) but not `pageSize`, so `pageSize=1_500_000_000` with `pageNumber=3` still overflowed `Skip` negative. `Shared.Common.Constants.PagingRules` gains `MaxPageSize` (100), rejecting with `400` above it; the audit search's own, higher, already-accepted cap (`AuditExportRules.MaxSearchPageSize`, 200, applied by clamping rather than rejecting) is unchanged. (36) **ARCH LOW.** The now-dead `ValidationRuleExtensions.ContainsDisallowedControlCharacter` compatibility wrapper (no callers left once `ControlCharacterGuard` was pointed at the shared `ControlCharacters.ContainsDisallowed` predicate directly, in the previous round) is deleted. **2026-09-26 — HO-ADM-001 sixth fix round (self-amendment), before the TEST gate.** (37) **SEC LOW, blank `placement` 500.** `AdministrationMapper.ParseNullableEnum` checked `value is null` only, so a blank-but-non-null `placement` (`""`, `" "`, `"\t"`) fell through to `ParseEnum`, which called `EnumerationParsing.TryParse` (correctly treating blank as "no value" and returning a null result with no error) and then threw on that null result -- an unhandled `500` on content create/edit. `ParseNullableEnum` now checks `string.IsNullOrWhiteSpace` and returns `null` directly, matching `EnumerationParsing.TryParse`'s own contract. There is no domain or validator rule requiring a `Banner` to carry a `Placement`, so a blank placement now succeeds (`201`) for both `Page` and `Banner`, the same as an absent one -- inventing a new "Banner requires placement" rule was out of scope for this fix (§20's "handlers orchestrate, they do not decide" reasoning extends to not inventing business rules the spec never named). **ARCH LOW correction.** Amended (32) and `ValidationRuleExtensions.cs`'s doc comment wrongly claimed the per-field `NoControlCharacters` rule gives "a field-specific error" for a NUL byte specifically -- the global `NulByteRejectionFilter` is an action filter and always runs before FluentValidation does, so for a NUL byte it is the per-field rule that never fires. Both are reworded: the per-field rule's real remaining purpose is every *other* C0 control character the filter does not check for. **2026-09-26 — HO-ADM-001 TEST gate seventh pass, closing all 17 BLOCKED rows (self-amendment).** (38) **I-E3 made testable (source change).** `AuditExportRules.MaxRows` (50,000) was a hard-coded `const int`, with no seam for a test factory to lower it -- exercising "export over the cap -> 413" for real meant generating 50,001 rows against Postgres. A new `Infrastructure.Configuration.AuditExportOptions.MaxRows` (defaulting to `AuditExportRules.MaxRows`, unchanged) is now bound from configuration (`appsettings.json`'s new `"AuditExport": { "MaxRows": 50000 }` section, registered in `AdministrationModule`); `AdminAuditEntriesController.Export` reads it via `IOptions<AuditExportOptions>` instead of the constant directly. `AdministrationApiFactory.WithLoweredAuditExportCap(int)` (test-only) overrides just this value for one factory instance, the same technique `AccountsApiFactory.WithTightRateLimits()` already uses for rate limits; `AdminAuditEntriesEndpointTests.ExportCsv_OverALoweredCap_Returns413` lowers the cap to 3 and creates 4 rows. The production default is unchanged. (39) **A11 (I-D3/I-D4) covered, isolated.** Both settings are global, non-branch-overridable, shared state in the one `catecommerce_adm` database every other integration test also uses -- a permanent mutation risked leaking into a concurrently-running test class that assumes the registry default. `AdminGlobalSettingsLiveEffectEndpointTests.cs` (new) puts both new tests in a dedicated `[Collection(AdministrationGlobalSettingsCollection.Name)]`, whose `[CollectionDefinition(DisableParallelization = true)]` is the standard xUnit v2 mechanism for a collection of tests that must never interleave with each other; each test also reads the setting's value before mutating it and restores it in `finally`, regardless of assertion outcome. `AdministrationApiFactory.WithFakeTimeProvider()` (test-only, mirrors `AccountsApiFactory`'s own) drives I-D4's idle-timeout advance without a real wait. (40) **The fourteen remaining unit-depth gaps, closed:** `ActionRolesCoverageTests.cs` (U-A2/U-A3, reflects over every real `[RequireAction]` action string and the real `ActionRoles` table, not synthetic fixtures); `StaffAvailabilityTests.cs` (U-C2, next-pattern-effective date switch; U-C4, `restrictToOpeningHours=false`); `LeaveCommandHandlersTests.cs` (U-C5, approve-twice/cancel-ended-yesterday; U-C7, nurse-requests-for-someone-else; U-C9, sole-administrator self-approval, plus a negative-control sibling); `StaffLeaveApprovedEventShapeTests.cs` (U-C6, exactly four declared properties, no free text); `ClinicServiceAggregateTests.cs` (U-D1, duration bounds 7/0/485, empty roles, unknown role); `AdminGlobalSettingsLiveEffectEndpointTests.SettingResolutionOrder_BranchOverride_ThenGlobal_ThenDefault_EachWinsInTurn` (U-D5); `MoneyTests.cs` (U-D7 -- **one claim corrected**: `Money`'s own doc comment says non-negativity is "each owning aggregate's job, not this type's", so "negative rejected" does not hold for this type as built; the test proves the actual, deliberate behaviour instead, and notes `SetServicePriceCommandValidator.Amount.GreaterThanOrEqualTo(0)` as where negative prices are actually refused); `AdminRosterLeaveEndpointTests.ScheduleNextPattern_AdvancePastEffectiveDate_NotPromotedByABareRead_PromotedByTheNextCommand` (I-C2 -- **one claim corrected**: §8.5's own table says promotion is "applied by the availability calculator on read", which is `StaffAvailabilityTests`, a pure computed selection with no persisted state; the raw roster *listing* read has no promotion call at all, and this test proves that explicitly, alongside proving the next roster-loading *command* does promote); `AdminContentPolicyEndpointTests.GetBanners_OutsideVisibilityWindow_IsExcluded_InsideWindow_IsReturned` (I-F3); `AdminLogCaptureEndpointTests.cs` (I-X1 -- **one claim corrected**: the TEST gate's own sixth-pass claim that "no log-capture harness exists anywhere in the tree for either HO-ACC-001 or this handover" was wrong; `CatEcommerceApiFactory.WithCapturedLogs()` already existed, already used by HO-ACC-001's own `SessionResolutionFailureTests` -- this is its first use for Administration's own free-text fields). (41) **Housekeeping.** `AdminAuditEntriesEndpointTests.ExportCsv_ReturnsCsvAndAuditsTheExport` renamed to `ExportCsv_ReturnsCsvResponseWithHeaderRow` -- the old name promised auditing the export, which its own body never checked (that half is `ExportCsv_WritesOneAuditLogExportedRow_FindableBySearchingForIt`, written the previous pass). The test-case sheet, `docs/test-cases/TC-HO-ADM-001.md`, is updated in place: all 17 BLOCKED rows are now PASS, both its "Cases I could not run" and "Gaps left deliberately" tables are trimmed to the two items genuinely still out of scope (I-A2's exhaustive matrix; `StaffAvailability` rules 2/5/7), and A6/A7/A9/A11/A13 in its acceptance-criteria table are updated to match. **2026-09-26 — HO-ADM-001 CODE_REVIEW seventh-pass findings closed (self-amendment).** (42) **[HIGH] Global-settings test isolation widened.** `AdministrationGlobalSettingsCollection` (Amended (39)) only covered its own new file's three tests. Grepping the whole `IntegrationTests` project for global-setting reads/writes (not just the gate's own named example) found two pre-existing, unrelated classes that hard-assert an ambient global-setting value with no capture/restore of their own, against the exact settings this handover's own tests mutate: `AdminCatalogueSettingsEndpointTests.GetSettings_ReturnsEveryRegistryKeyWithBoundsAndEffectiveValue` (`booking.slotHoldMinutes` == `"10"`) and, found in the same sweep, HO-ACC-001's own `StaffSessionEndpointTests.PassiveGet_IdleExpired_AsTheFirstRequestAfterIdling_ReturnsSessionIdleTimeoutNotIdentity` (`idleTimeoutMinutes` == `30`, i.e. `security.staffIdleTimeoutMinutes`) -- different collections run concurrently by default in xUnit v2, and both classes hit the same physical `catecommerce_adm` database row regardless of which factory type (`AdministrationApiFactory` vs `AccountsApiFactory`) each uses. Both classes now carry `[Collection(AdministrationGlobalSettingsCollection.Name)]` -- chosen over adding capture/restore to each individual assertion, as the "broadest, safest" fix the gate itself named; `StaffSessionEndpointTests.cs` is edited on HO-ACC-001's behalf (the same "ahead of that area's own change" precedent as the eraser/`ClinicalAuditEntityTypes.CatProfiles.cs`). (43) **[MEDIUM] `AuditExportOptions.MaxRows` validated at startup.** Nothing rejected a zero, negative, or unreasonably large configured value: zero happened to fail closed only by arithmetic accident, a negative value would reach the query pipeline unvalidated, and a very large value silently disabled the cap entirely, with `maxRows + 1` overflowing unchecked once near `int.MaxValue`. `AuditExportOptions.MaxRows` now carries `[Range(1, AuditExportRules.MaxRows)]`, and `AdministrationModule`'s registration switched from `services.Configure<T>()` to `services.AddOptions<T>().Bind(...).ValidateDataAnnotations().ValidateOnStart()` (the same pattern `AccountOptions` already uses) -- a misconfigured value now fails the host at boot. An operator may still lower the cap below the product default, never raise it past `AuditExportRules.MaxRows` itself. (44) **[LOW] U-D7's enforcement point given an end-to-end test.** The TEST gate's seventh-pass correction ("negative rejected" is `SetServicePriceCommandValidator`'s job, not `Money`'s) was true of the code but unproven by any test until now: `AdminCatalogueSettingsEndpointTests.SetServicePrice_NegativeAmount_Returns400` submits `amount: "-5.00"` through the real `POST /api/admin/services/{id}/prices` and asserts `400`. **2026-09-26 — ACCEPTANCE gate first pass fixes.** (45) **[HIGH, A3/I-A2] Full Cat/Vaccine authorization matrix.** `CatVaccineAuthorizationSecurityTests` covered anonymous fully plus about six individual role/endpoint cells -- not the full matrix §19 row 4 requires. New file `CatVaccineFullAuthorizationMatrixTests.cs` discovers every `[RequireAction]` action on `CatsController`/`VaccinesController` by reflection (so a new endpoint can't be silently skipped) and asserts each against an expected-outcome table typed by hand from this document's own §12.3, never from `ActionRoles.Administration.cs` (avoiding a tautological test that could never catch a bug in `ActionRoles` itself) -- one data-driven theory over every discovered endpoint x (anonymous, each of the 8 staff roles, customer-own, customer-other where owner-scoped). Building the endpoint list this way found a genuine pre-existing gap: `CatVaccineAuthorizationSecurityTests.Anonymous_EveryCatAndVaccineEndpoint_IsRefused`'s own hand-written probe list was missing `POST /api/cats/{id}/vaccinations`; left as-is (additive, not corrected in place) since the new file's own anonymous case now covers it independently. (46) **[HIGH, A6/U-C1/U-C3/I-C1] Leave and extra-shift effect on availability, actually observed.** `StaffAvailabilityTests` gains five unit tests: approved leave removes the whole day (rule 5) with the negative half (an un-passed, merely-Requested leave changes nothing) as a contrast; an `ExtraShift` exception adds an interval on a day with no pattern slot, and a `ShiftCancelled` exception shortens or fully removes one (rule 4); a fully-closed branch (`restrictToOpeningHours=true`, empty `branchOpenIntervals`) yields nothing for the day, proving rule 2's composition with `StaffAvailability` (the closure computation itself remains `BranchCalendar`'s own, already covered by `BranchCalendarTests`); an abutting pattern shift and `ExtraShift` merge into one interval (rule 7), and a fully-cancelled shift never leaves a zero-length one. Two new integration tests in `AdminRosterLeaveEndpointTests` close I-C1 end to end: `ApprovedLeaveDay_ThenGetAvailability_ExcludesThatDay` sets a pattern, confirms the shift via `/availability`, has the same staff member request and the admin approve leave over that day, and re-queries to confirm it is gone; `ExtraShiftException_ThenGetAvailability_IncludesTheExtraShift` confirms an empty day, adds an `ExtraShift` roster exception, and re-queries to confirm the interval now appears. (47) **[HIGH, A10/I-D5] Settings-change audit, watched not inferred.** Two new tests in `AdminGlobalSettingsLiveEffectEndpointTests` (already under `AdministrationGlobalSettingsCollection`): `ChangeGlobalSetting_AuditsADataChangeRowWithBeforeAndAfterValues` changes `booking.slotHoldMinutes` globally and searches `/api/admin/audit-entries` for the resulting `BusinessSettingsChangedEvent` / `DataChange` row on `BusinessSettingsAggregate` at `SettingScopes.GlobalId`, asserting both `beforeValue` and `afterValue` contain the old and new values; `ChangeBranchSettingOverride_AuditsADataChangeRowWithTheAfterValue` does the same for a branch override on a freshly-minted branch (asserting only `afterValue`, since the override's own aggregate is a first-write insert with no "before"). Both PASS as written: `BusinessSettingsAggregate.Change`/`ClearOverride` call `UpdateVersion()`, which increments the root's own mapped `Version` column, so EF's change tracker marks the root `Modified` even though the actual data changed lives in the owned `Values` collection -- confirmed directly in the executed SQL (`UPDATE admin.business_settings SET "Version" = ...` alongside `UPDATE admin.business_settings_value SET value = ...` in the same transaction). Settings changes were already correctly audited; **no source change was needed**, closing the gate's open question. (48) **[LOW, scope] `NullActiveBranchAccessor.cs`/`NullActiveBranchAccessorTests.cs` (commit `2e56210`), and `IActiveBranchAccessor.cs`.** `src/Infrastructure/CatEcommerce.Infrastructure/Fakes/Administration/NullActiveBranchAccessor.cs` and `tests/CatEcommerce.Infrastructure.Tests/Fakes/Administration/NullActiveBranchAccessorTests.cs` are outside §13's file list -- a mechanical consequence of the `IActiveBranchAccessor` contract change already recorded in Amended (11) and named by `CONTRACTS.md` #20, but never itself declared here. `src/Domain/CatEcommerce.Domain/DomainServices/IActiveBranchAccessor.cs` is likewise not in §13's `DomainServices/` list, though §8.3/§8.9 specify it. All three files are correctly in scope for this handover; this entry is the missing declaration, not a change. (49) **[LOW] `TC-HO-ADM-001.md` made honest.** The sheet's Summary previously counted `PASS (partial)`/`PASS (indirect)` rows as full `PASS`, overstating coverage against three criteria (A3, A6, A10) the ACCEPTANCE gate's own §18 marked NOT MET. U-A5 (A3), U-C1/U-C3/I-C1 (A6) and I-D5 (A10) rows are updated to `PASS` now that (45)-(47) close them; the Summary's counting rule is corrected so a `PASS (partial)`/`PASS (indirect)` row is tallied separately from a full `PASS`, and the remaining genuinely-partial rows (A5's unknown-cat-id probe, A7's I-C4 spy, A9's bounds-vs-default check) are left honestly marked rather than newly tested, since they are outside this round's five items. *(append further rows here)* <br><br>**2026-09-23 — appended by later handovers (owed rows, recorded now that all 18 pairs exist).** Everything below is an **append** inside the lines each handover lists in its §13; no existing behaviour changes. These are recorded here so this document is not read as the whole picture. **Appends to the settings registry:** `billing.*` (nine, HO-BIL-001), `inventory.*` (seven, HO-IMP-001), `pharmacy.*` (six, HO-PHM-001), `boarding.*`, `files.*` (three, HO-FIL-001), `shop.*` (five), surgery (five) and subscription settings, plus the `Shop` settings group. `inventory.defaultReorderPoint` gains its consumer. `BranchCalendar.LocalDateOf` is added if absent (HO-IMP-001). Delivery zones were routed to HO-ORD-001 by §4.2 and are built there. Content pages: `boarding-payment-instructions`. Consent form wording is authored here as policy documents (HO-EMR-001 OQ-15). **2026-09-26 — integrator (`ho/integrator-host`), after merge.** (50) **Tracked-for-after-merge items closed.** `NulByteRejectionFilter` and `UniqueConstraintExceptionFilter` are no longer registered as global `MvcOptions` filters from `AdministrationModule`/`AccountsModule` respectively; both move to `Program.cs`'s own `AddControllers(...)` call, alongside `ConcurrencyExceptionFilter`, since neither is area-specific — `NulByteRejectionFilter` always applies to every controller in the host, and `UniqueConstraintExceptionFilter` maps constraint violations for several areas, not just the one that first built it. Each remains registered exactly once, with unchanged order and behaviour. `BusinessViolationCodes.ContainsControlCharacter` moves with the filter, from `BusinessViolationCodes.Administration.cs` into the base `BusinessViolationCodes.cs`, for the same reason. HO-ACC-001's carried item, Amended (32)'s "P13" (`PUT /api/customers/me` `fullName` NUL 500), is confirmed closed: the global filter now returns `400 ContainsControlCharacter` for it, proven live by a new test, `ProfileEndpointTests.UpdateProfile_FullNameWithNulByte_Returns400`; see HO-ACC-001 §1 Amended (21), which drops that handover's own carried item. |
| Platform | `Platform.Infrastructure.Core` **1.0.34** — do not downgrade |

---

## 2. Objective

Give the business the **configuration every operational area depends on**, so that later areas are
built on data rather than on constants: where the clinic is and when it is open, which rooms it has,
who is working when, what services it sells at what price and tax, what its tunable business rules
are, and who may do what. Appointments cannot compute a single slot until branches, opening hours,
closures, rooms, rosters, leave and services exist — that is why this area is third in the delivery
order, ahead of Appointments.

It also **closes the authorization gap** HO-ACC-001 left open. After this slice **every endpoint in
the solution declares who may call it**, the Cat Profile endpoints enforce SRS Appendix D, and a
customer can reach only their own cats. A test fails the build if any endpoint is left undeclared.

Four things this slice is careful about:

1. **Time.** Opening hours and shifts are *local wall-clock* times in the branch's time zone; every
   stored instant is `timestamptz` (C-7). Converting between them happens in one domain service, not
   in every query (ADR-045).
2. **History.** A price or tax change applies to new transactions only (US-ADM-04). Prices and tax
   rates are **effective-dated and append-only**; nothing is overwritten (ADR-049).
3. **Contracts for later areas.** Approved leave must flag affected appointments, but appointments do
   not exist yet. This slice publishes the event and the availability calculation that Appointments
   and Boarding will consume (ADR-047, ADR-048).
4. **Audit.** Every configuration change is already audited with before/after values by HO-ACC-001's
   change capture. This slice adds the **search and export** of that trail (US-ADM-06).

§17 builds the slice in **six phases**; each compiles, migrates and passes its own tests first.

---

## 3. In scope

### 3.1 Stories

| Story | Pri | Pts | Capability delivered here | Phase |
|---|---|---|---|---|
| US-ADM-02 | M | 8 | Every endpoint declares its access; Appendix D enforced on every existing endpoint including Cat Profiles; customer ownership scoping; `403` + audit on every refusal | A |
| US-ADM-01 | M | 8 | Branches with time zone, weekly opening hours and dated closures/holidays; rooms; staff branch assignment; active-branch resolution; branch calendar query; multi-branch without code change | B |
| US-ADM-03 | M | 8 | Weekly shift patterns (current and next), roster exceptions, leave requests and decisions; staff availability calculation; the leave-approved integration event | C |
| US-ADM-04 | M | 8 | Clinic service catalogue (also FR-CLN-1's fields) with effective-dated prices; tax classes with effective-dated rates | D |
| US-ADM-05 | M | 8 | Typed, validated business settings, global with branch overrides; audited; the two settings with existing consumers rewired | D |
| US-ADM-06 | M | 5 | Audit-log search by user, entity, action, category and date range; paged; CSV export; the export itself audited | E |
| US-ADM-07 | S | 8 | Content pages, FAQs and banners; versioned policy documents; publish workflow; public read; customer acceptance of each policy version | F |
| US-ADM-08 | C | 3 | Maintenance-mode flag and public status endpoint; the back office is never gated by it | F |

### 3.2 Decisions already settled upstream — do not re-litigate

| Decision | Where |
|---|---|
| In-memory CQRS, one host, `IInMemoryDispatcher` only; never `202` | ADR-010, 013, 014, 019, 020 |
| PostgreSQL / EF Core, one `DbContext`, `xmin` → `409` | ADR-011, ADR-016 |
| No `CommandService` layer; per-call `SaveChangesAsync` | ADR-015, ADR-021 |
| `Enumeration` in `Shared` | ADR-022 |
| `[RequireAction]` + the one `ActionRoles` table; fail closed; denials audited | HO-ACC-001 ADR-036 |
| Caller resolution into `UserContext` (staff roles, customer id) | HO-ACC-001 ADR-032 |
| Every aggregate change audited in the same transaction | HO-ACC-001 ADR-040 |
| Staff role ids are the eight Appendix D columns | HO-ACC-001 §10.2 `StaffRoles` |

---

## 4. Out of scope

### 4.1 Frontend — all of it

Screens are [`HO-ADM-002`](HO-ADM-002.md). Criteria phrased as UI are met here at the contract level:

| Story AC (UI phrasing) | Server half — delivered here |
|---|---|
| US-ADM-02 "regardless of what the UI shows" | Every endpoint enforces its own access; the test in §15 proves no endpoint is undeclared |
| US-ADM-05 "the settings screen … all editable" | `GET/PUT /api/admin/settings` covers every setting in §10.2 with its type, bounds, unit and default |
| US-ADM-06 "listed and exportable" | Paged `GET` and a streamed CSV |
| US-ADM-07 "edit and publish" | Draft/publish endpoints; the public read returns published content only |
| US-ADM-08 "storefront shows a notice" | `GET /api/system/status` — the storefront (Shop area) renders the notice |

### 4.2 Deferred to a named later area — contracts delivered here

| Capability | Story / AC | Where it lands | What this slice delivers |
|---|---|---|---|
| Flag existing appointments when a vet's leave is approved | US-ADM-03 AC2 | **HO-APT-001** | `StaffLeaveApprovedEvent` (§11.5) — the binding contract Appointments subscribes to |
| Boarding task assignment follows rosters | US-ADM-03 AC1 | **HO-BRD-001** | `IStaffAvailabilityCalculator` and `GetStaffAvailabilityQuery` |
| "Appointment and boarding availability respect hours and holidays" | US-ADM-01 AC1 | HO-APT-001, HO-BRD-001 | `IBranchCalendar` — the one source of open intervals |
| Boarding rate cards | US-ADM-04 | **HO-BRD-001** | Nothing — rates depend on unit types that do not exist. Configured in the context that uses them |
| Delivery zones and charges | US-ADM-04 | **HO-ORD-001** | Nothing — same reason |
| Storefront rendering of pages, banners and the maintenance notice | US-ADM-07, 08 | **HO-SHP-002** | Public read endpoints |
| The storefront endpoints refusing work in maintenance mode | US-ADM-08 | HO-SHP-001, HO-ORD-001 | `[StorefrontEndpoint]` metadata and the `MaintenanceModeFilter` (§12.7), applied to nothing yet |
| Consumers of the settings that have none yet | US-ADM-05 | Each later area | The settings are stored, validated and exposed through `IBusinessSettings` (§8.9) |
| Policy acceptance at registration or checkout | US-ADM-07 AC2 | HO-ACC amendment / HO-ORD-001 | The acceptance command and the portal prompt (§12.4) |

### 4.3 Not buildable yet — named gaps

| Gap | Consequence |
|---|---|
| Administrator-editable permissions | US-ADM-02's criteria are met by the code-defined matrix (ADR-044). Editing is not built — OQ-2 |
| Base currency and tax regime (SRS Q-5) | Money is `decimal(12,2)` + ISO currency code; default `BDT`; tax rates are data, not a regime (ADR-054, OQ-5) |
| Branch-scoping of existing aggregates | Customers and cats are **not** branch-scoped (they are the same person and cat at any branch). Each later branch-scoped aggregate adds `BranchId` itself — §8.2 states the rule |
| Notifying staff of leave decisions | No Notification context yet (HO-NTF-001). The decision is recorded and visible in the leave list |

### 4.4 Explicitly forbidden shapes

No `IBusMessageDispatcher`, `IEventPublisher`, `IDispatcher`, MassTransit, RabbitMQ, MongoDB, a
second host, a `CommandService` layer, a rejection event, `202 Accepted`, `[Authorize]` or an auth
framework. **No HTML stored or served as content** (ADR-052). **No time-zone library** — `TimeZoneInfo`
with IANA ids is in the runtime. **No per-context copy of the role table.**

---

## 5. Prerequisites

| Required | State |
|---|---|
| HO-CAT-001 merged | ✅ at `4007225` |
| **HO-ACC-001 merged** — staff accounts, `ActionRoles`, `RequireActionFilter`, `SessionUserContextProvider`, audit change capture and triggers, `UniqueConstraintExceptionFilter` | ❌ **not implemented at baseline.** This slice extends those types and cannot start without them — §20.1 |
| PostgreSQL 17.5 `catecommerce_dev`, `dotnet-ef` 9.0.0 | ✅ |
| New NuGet packages | **None.** `TimeZoneInfo` (IANA ids via ICU on Linux, built-in on .NET 9 Windows) and `System.Text.Json` suffice |

The shared-database rules of HO-CAT-001 §16.1 and HO-ACC-001 §16.1 apply unchanged. Branch codes,
service codes, tax-class codes, content slugs and policy keys are **unique keys** — mint them per test.

---

## 6. Governing architecture documents

Identical to [HO-ACC-001 §6](HO-ACC-001.md#6-governing-architecture-documents). Of note here:

| Document | Why it matters in this slice |
|---|---|
| `.ai/bounded-contexts.md` rule 3 | Decides that closures, roster exceptions, leave, prices and tax rates are **their own roots** — each accumulates without bound |
| `.ai/bounded-contexts.md` rule 4 | "Appointment slot availability" is named as a **Domain Service**. The staff-availability and branch-calendar calculations are its inputs and are domain services too (ADR-047) |
| `.ai/decisions/adr.md` ADR-009 | The action-to-role table this slice completes |
| `HO-ACC-001` §12.8 | `RequireActionFilter` — extended, not rewritten |

---

## 7. Verified platform facts

HO-CAT-001 F1–F10 and HO-ACC-001 F11–F18 apply. Additionally, read from the code at `4007225`:

| # | Fact | Consequence |
|---|---|---|
| F19 | `CatsController` takes `ownerId` from the query string and `OwnerId` from the request body; no read handler scopes by caller | Customer calls must have the owner **forced to the caller** in the controller and checked by the filter (§12.3). A client-supplied owner id is ignored for customers |
| F20 | `GetCatVaccinationStatusQueryHandler` reads `IOptions<CatProfileOptions>.VaccinationDueSoonWindowDays`; `DefaultParasiteIntervalDays` is bound but read nowhere | Rewire the first to `IBusinessSettings` (§8.9); the second becomes a setting with no consumer yet |
| F21 | The Read project references Infrastructure (it injects `IOptions<CatProfileOptions>`) | `IBusinessSettings` in `Domain.DomainServices` is reachable from Read, Application and Api |
| F22 | .NET 9 resolves IANA ids (`Asia/Dhaka`) through `TimeZoneInfo.FindSystemTimeZoneById` on Windows and Linux | Store the IANA id; never a Windows id; never a fixed UTC offset (Bangladesh has no DST, but a second branch elsewhere may) |

---

## 8. Domain model

### 8.1 Context and aggregate boundaries

```
Administration context — schema `admin`
BranchAggregate                (root)                      admin.branch
└── WeeklyHours [owned many ≤ 21]  DayOfWeek, Opens, Closes     admin.branch_opening_interval
BranchClosureAggregate         (root)  → BranchId          admin.branch_closure
RoomAggregate                  (root)  → BranchId          admin.room
StaffBranchAssignmentAggregate (root, Id == StaffAccountId) admin.staff_branch_assignment
StaffRosterAggregate           (root)  → StaffAccountId, BranchId (unique pair)   admin.staff_roster
├── CurrentPattern [owned many ≤ 21] ShiftSlot
└── NextPattern?   [owned many ≤ 21] ShiftSlot + NextPatternEffectiveFrom
RosterExceptionAggregate       (root)  → StaffAccountId, BranchId   admin.roster_exception
LeaveRequestAggregate          (root)  → StaffAccountId    admin.leave_request
ClinicServiceAggregate         (root)                      admin.clinic_service
ServicePriceAggregate          (root, append-only) → ServiceId      admin.service_price
TaxClassAggregate              (root)                      admin.tax_class
TaxRateAggregate               (root, append-only) → TaxClassId     admin.tax_rate
BusinessSettingsAggregate      (root, Id == scope id)      admin.business_settings
└── Values [owned many ≤ |SettingDefinitions|] SettingValue
ContentItemAggregate           (root)                      admin.content_item
PolicyDocumentAggregate        (root)                      admin.policy_document
PolicyVersionAggregate         (root, append-only) → PolicyKey      admin.policy_version
PolicyAcceptanceAggregate      (root, append-only) → CustomerAccountId  admin.policy_acceptance
SystemStatusAggregate          (root, singleton)           admin.system_status
```

**One aggregate mutated per command**, with one recorded exception: publishing a policy version
(§8.8, ADR-052). Cross-aggregate reads —
"does this room belong to this branch", "is this staff member assigned here" — are repository calls
in the handler (`.ai/command-handler-pattern.md` §6).

### 8.2 Branches, closures, rooms — US-ADM-01

**`BranchAggregate`** — `Code` (≤ 20, upper-case, unique), `Name` (≤ 200), `TimeZoneId` (IANA,
validated by `TimeZoneInfo.FindSystemTimeZoneById`; default `Asia/Dhaka`), `AddressLine?` (≤ 500),
`PhoneNumber?` (normalised by HO-ACC-001's `MobileNumber` VO **or** a free landline string ≤ 30 —
OQ-7), `Email?`, `IsActive`, `WeeklyHours`.

`OpeningInterval` (owned): `DayOfWeek`, `Opens` (`TimeOnly`), `Closes` (`TimeOnly`). Invariants:
`Opens < Closes` (no interval crosses midnight — an overnight clinic is two intervals, OQ-8); at most
**3 intervals per day** (a lunch break is two); intervals on one day do not overlap and are stored
sorted. **A day with no interval is closed.**

| Method | Invariants | Event |
|---|---|---|
| `Create` | code, name, time zone valid; `WeeklyHours` empty (a new branch is closed until hours are set — safe) | `BranchCreatedEvent` |
| `UpdateDetails` | as above; code immutable | `BranchDetailsUpdatedEvent` |
| `SetWeeklyHours` | **replaces the whole week** (the screen edits the whole week); interval rules | `BranchWeeklyHoursChangedEvent` |
| `Deactivate` / `Reactivate` | cannot deactivate the **last active** branch → `LastActiveBranchCannotBeDeactivated` | `BranchActivationChangedEvent` |

**`BranchClosureAggregate`** — `BranchId`, `StartsOn` / `EndsOn` (`DateOnly`, inclusive, local),
`Kind` (`PublicHoliday` \| `Closure`), `Reason` (≤ 200), optional `PartialFrom` / `PartialTo`
(`TimeOnly`, only when `StartsOn == EndsOn` — a half-day closure). Methods `Create`, `Cancel` (only if
`StartsOn` is in the future in branch-local time → `PastClosureCannotBeCancelled`). Cancelled closures
are kept (`IsCancelled`) — the calendar ignores them.

**`RoomAggregate`** — `BranchId`, `Name` (≤ 100, unique per branch), `Kind` (`Consultation` \|
`Treatment` \| `Theatre` \| `Grooming`), `IsActive`. A room holds one appointment at a time
(capacity 1); multi-occupancy is not modelled.

**Seed.** The migration seeds **one branch** — code `MAIN`, name "Main clinic", `Asia/Dhaka`, no hours,
active — with a fixed id recorded in `AdministrationSeed.MainBranchId` (SRS A-1). It is closed until an
administrator sets hours.

**Branch-scoping rule for later areas** (NFR-SCL-1): an aggregate that exists *at a place* —
appointment, room booking, stock location, boarding unit, till — carries `BranchId`, set from the
active branch (§8.3) and validated against the caller's assignments. Customers and cats are **not**
branch-scoped. Each later handover applies this rule to its own aggregates.

### 8.3 Staff branch assignment and the active branch — ADR-046

**`StaffBranchAssignmentAggregate`** — `Id` is the staff account id (1:1, created on first write),
`BranchIds` (1…n, each an existing active branch), `PrimaryBranchId` (a member). Method `Assign`
replaces the set. **HO-ACC-001's `StaffAccountAggregate` is not edited.**

**Resolution** (`IActiveBranchAccessor`, Scoped, Api):
1. Staff caller with header `X-Branch-Id` → must be in their assignment → else `403 BranchNotAssigned`.
2. Staff caller without the header → `PrimaryBranchId`.
3. Staff caller with **no assignment** → the only active branch **if exactly one exists** (the
   single-branch launch needs no assignment step); otherwise `403 BranchAssignmentRequired`.
4. Customer or anonymous → no active branch; customer-facing endpoints that need one take `branchId`
   explicitly (e.g. a later booking chooses the branch).

The accessor is consumed only by endpoints that need a branch (none in this slice except the
leave-request list's default filter); admin endpoints take `branchId` in the route because an
administrator manages every branch.

### 8.4 Rosters and leave — US-ADM-03 — ADR-047, ADR-048

**`StaffRosterAggregate`** — one per `(StaffAccountId, BranchId)`, unique. `CurrentPattern` and an
optional `NextPattern` with `NextPatternEffectiveFrom` (`DateOnly`, strictly after today in branch
time). `ShiftSlot` (owned): `DayOfWeek`, `Starts`, `Ends` (`TimeOnly`, `Starts < Ends`, no midnight
crossing), `DefaultRoomId?`. At most 3 slots per day, non-overlapping.

| Method | Behaviour | Event |
|---|---|---|
| `SetCurrentPattern` | Replaces the current week pattern from today | `StaffRosterPatternChangedEvent` |
| `ScheduleNextPattern` | Sets `NextPattern` + effective date; replaces any scheduled one | same |
| `CancelNextPattern` | Clears it | same |
| `PromoteDuePattern(asOf)` | If `NextPatternEffectiveFrom ≤ asOf` → current = next, next cleared. **Called lazily** by any command that loads the roster, and applied by the availability calculator on read — no background job | same (only when promoted) |

The staff member must be a staff account with at least one role in
`StaffRosterRules.RosterableRoles` (`Vet`, `Nurse`, `Caretaker`, `Reception`) and assigned to the
branch → else `StaffNotRosterable` / `BranchNotAssigned`.

**`RosterExceptionAggregate`** — `StaffAccountId`, `BranchId`, `Date`, `Kind` (`ExtraShift` \|
`ShiftCancelled`), `Starts`, `Ends`, `Reason`. `Create` / `Cancel` (future only). Unbounded over time,
hence its own root.

**`LeaveRequestAggregate`** — `StaffAccountId`, `StartsOn`, `EndsOn` (`DateOnly`, inclusive),
`Kind` (`Annual` \| `Sick` \| `Training` \| `Other`), `Note?` (≤ 500), `Status`
(`Requested` → `Approved` \| `Rejected`; `Requested`/`Approved` → `Cancelled`), `DecidedBy?`,
`DecidedOn?`, `DecisionNote?`.

| Method | Rules | Event |
|---|---|---|
| `Request` | `EndsOn ≥ StartsOn`; span ≤ 90 days; **by the staff member for themselves, or by an administrator for anyone** (checked in the handler); overlapping an existing `Requested`/`Approved` request of the same person → `LeaveOverlapsExistingLeave` (handler pre-check) | `LeaveRequestedEvent` |
| `Approve` / `Reject` | `Status == Requested`; decider ≠ requester unless the decider is the only administrator (OQ-3) | `StaffLeaveApprovedEvent` / `LeaveRejectedEvent` |
| `Cancel` | `Requested` → by requester or administrator; `Approved` → administrator only, and only if `EndsOn` is today or later | `LeaveCancelledEvent` (`WasApproved`) |

> **`StaffLeaveApprovedEvent` is an integration contract** (ADR-048): `LeaveRequestId`,
> `StaffAccountId`, `StartsOn`, `EndsOn` — no note, no reason. HO-APT-001 subscribes to flag the
> staff member's appointments in that range for rescheduling; HO-BRD-001 to reassign tasks. Both
> handlers must be **idempotent**. `LeaveCancelledEvent` (`WasApproved = true`) is the matching
> un-flag contract.

### 8.5 `IStaffAvailabilityCalculator` and `IBranchCalendar` — domain services

`src/Domain/CatEcommerce.Domain/DomainServices/` — **pure functions over their inputs, no I/O**, so
Appointments can call them with pre-loaded data and tests need no database. These are domain services
in `.ai/bounded-contexts.md` rule 4's sense (they span branches, closures, rosters, exceptions and
leave).

```
BranchCalendar.OpenIntervals(branch, closures, fromLocalDate, toLocalDate)
  → IReadOnlyList<TimeInterval>      // instants (DateTimeOffset), sorted, merged

StaffAvailability.Intervals(roster, exceptions, approvedLeave, branchOpenIntervals, from, to,
                            restrictToOpeningHours)
  → IReadOnlyList<StaffAvailableInterval(StaffAccountId, BranchId, Start, End, DefaultRoomId?)>
```

Rules, each tested:
1. Local → instant conversion uses the **branch's** `TimeZoneId` for that date.
2. A full-day closure removes the day; a partial closure removes its interval.
3. Pattern for a date = `NextPattern` if its effective date ≤ date, else `CurrentPattern`.
4. `ExtraShift` adds an interval; `ShiftCancelled` subtracts one.
5. Approved leave removes whole days. Requested leave does not remove anything (it is not decided).
6. With `restrictToOpeningHours` (the booking view), availability is intersected with open intervals;
   without it (the roster view), a shift outside hours is shown and marked `OutsideOpeningHours`.
7. Result intervals are merged, sorted, and never zero-length.

### 8.6 Services and prices — US-ADM-04 — ADR-049

**`ClinicServiceAggregate`** — `Code` (unique, ≤ 30), `Name` (≤ 200), `Description` (≤ 2000),
`Category` (`ServiceCategory`: `Consultation`, `Vaccination`, `Spay`, `Neuter`, `Dental`, `Grooming`,
`Emergency`, `FollowUp` — FR-CLN-1's list), `DurationMinutes` (5…480, multiple of 5),
`QualifiedRoleIds` (1…n from `StaffRoles.All`, catalogue order — US-CLN-01 "roles permitted to
perform it"), `RequiredRoomKind` (`RoomKind`), `TaxClassId`, `IsBookableOnline`, `IsActive`.
Methods `Create`, `Update` (all but code), `Deactivate`/`Reactivate`. **No price on the aggregate.**

**`ServicePriceAggregate`** — append-only: `ServiceId`, `Amount` (`Money`: `decimal(12,2)` ≥ 0 +
`CurrencyCode`), `EffectiveFrom` (`DateTimeOffset`), `SetBy`. Created by `SetServicePriceCommand`;
**`EffectiveFrom` must be ≥ now** (no back-dating → `PriceCannotBeBackdated`); two prices for one
service may not share an `EffectiveFrom`. The **price at instant *t*** is the row with the greatest
`EffectiveFrom ≤ t`. A service with no current price is not bookable (`IsPriced = false` on reads).

**`TaxClassAggregate`** — `Code`, `Name`, `IsActive`. **`TaxRateAggregate`** — append-only
`TaxClassId`, `RatePercent` (0…100, 2 dp), `EffectiveFrom` (≥ now). Same "rate at *t*" rule.

> **The binding contract for every later area that charges money** (Appointments, Billing, Orders,
> Boarding): read the price and tax rate **at the moment of the transaction** and **store a snapshot**
> on the transaction. Never join to the price table to reprice something that already happened. That
> is what makes US-ADM-04's "confirmed bookings keep their agreed price" true.

### 8.7 Business settings — US-ADM-05 — ADR-050

**`SettingDefinitions`** (Shared, static) — the registry. Each definition: `Key`, `Group`, `Type`
(`Integer` \| `Decimal` \| `Boolean` \| `TimeRange`), `Min`, `Max`, `Unit`, `Default`,
`BranchOverridable`, `DisplayName`, `Description`. §10.2 lists them.

**`BusinessSettingsAggregate`** — `Id` = `SettingScopes.GlobalId` (a fixed Guid) or a branch id.
`Values`: owned `SettingValue(Key, Value)` (value as an invariant-culture string). Created on first
write.

| Method | Rules | Event |
|---|---|---|
| `Change(changes)` | Every key exists; branch scope → every key `BranchOverridable` → else `SettingNotBranchOverridable`; every value parses to its type and is within bounds → else `InvalidSettingValue` (property `Changes[i].Value`); **all-or-nothing**; no effective change → no event | `BusinessSettingsChangedEvent` (keys only — values are captured by the audit interceptor with before/after) |
| `ClearOverride(key)` | branch scope only | same |

**Resolution** (`IBusinessSettings`, §8.9): branch override → global value → the definition's default.

### 8.8 Content, policies, acceptance, maintenance — US-ADM-07, US-ADM-08 — ADR-052, ADR-053

**`ContentItemAggregate`** — `Kind` (`Page` \| `Faq` \| `Banner`), `Slug` (unique per kind,
`^[a-z0-9]+(-[a-z0-9]+)*$`, ≤ 80), `Title` (≤ 200), `DraftBody`, `PublishedBody?`, `PublishedOn?`,
`Status` (`Draft` \| `Published` \| `Unpublished`), and for banners `Placement` (`Home` \| `Shop` \|
`Portal`), `LinkUrl?` (**https** or a site-relative path only), `VisibleFrom?`, `VisibleTo?`; for FAQs
`SortOrder`. Methods `Create`, `EditDraft`, `Publish` (copies draft → published), `Unpublish`.

**Bodies are `LimitedMarkup`, never HTML** (≤ 50 000 chars): paragraphs; `## ` / `### ` headings;
`- ` bullet lines; `**bold**`; `[text](url)` links with https or site-relative urls. `LimitedMarkup.Validate`
(Shared) rejects anything else that looks like markup (`<`, `javascript:`, `data:`) → `InvalidContentMarkup`.
The server stores and serves the markup text; the client renders it into elements (HO-ADM-002).

**`PolicyDocumentAggregate`** — `Key` (`terms`, `privacy`, `boarding-terms`, … unique), `Title`,
`RequiresAcceptance`, `CurrentVersionNumber?`. **`PolicyVersionAggregate`** — append-only: `PolicyKey`,
`VersionNumber` (1, 2, 3… — assigned by the handler as current + 1; unique pair), `Body`
(`LimitedMarkup`), `PublishedOn`, `ChangeSummary` (≤ 500). Publishing a version is
`PublishPolicyVersionCommand`, which creates the version **and** (as a second aggregate write) the
document's `CurrentVersionNumber` — recorded as ADR-052's exception; see §11.4.

> **The one rule-1 exception in this slice.** `PublishPolicyVersionCommand` writes the version and
> then the document's `CurrentVersionNumber`. The pointer is a cache of "latest version" and could be
> derived; it is stored so the public read is one indexed lookup. If the second write fails, the
> version exists but the pointer lags — the read query uses `MAX(version_number)`, so the lag is never
> visible. ADR-052.

**`PolicyAcceptanceAggregate`** — append-only: `CustomerAccountId`, `PolicyKey`, `VersionNumber`,
`AcceptedOn`, `IpAddress?`. Unique `(customer, key, version)` — accepting twice is a no-op `204`
(gate fix 2026-09-26: corrected from `200` to match §12.4 (C14 → `204`) and the constraint-race
mapping actually implemented — see Amended).

**`SystemStatusAggregate`** — singleton (`SystemStatusIds.Singleton`): `MaintenanceMode` (bool),
`MaintenanceMessage?` (≤ 500, plain text), `ChangedOn`. Method `SetMaintenanceMode`.

### 8.9 Ports

`src/Domain/CatEcommerce.Domain/DomainServices/` (flat), implementations in Infrastructure.

| Port | Surface | Implementation |
|---|---|---|
| `IBusinessSettings` | `int GetInt(string key, Guid? branchId = null)`, `decimal GetDecimal(…)`, `bool GetBool(…)`, `TimeRange GetTimeRange(…)`, `string GetString(…)` *(stale below: this row is superseded by Amended (11)/CONTRACTS.md #18, which also documents the fake/real unknown-key divergence — not revised in place per the gate's own LOW finding, since a body edit here is not itself a scope or contract problem)* | `CachedBusinessSettings` — loads both scopes, caches 30 s in `IMemoryCache`, **invalidated by a `BusinessSettingsChangedEvent` handler** so a change is visible on the next request |
| `IBranchCalendar` | `Task<IReadOnlyList<TimeInterval>> OpenIntervalsAsync(Guid branchId, DateOnly from, DateOnly to, CancellationToken)` | Loads the branch and closures, calls the pure `BranchCalendar` (§8.5) |
| `IActiveBranchAccessor` | `Guid? GetActiveBranchId()` *(stale: superseded by Amended (11)/CONTRACTS.md #20 — returns `ActiveBranchResult { Guid? BranchId, ActiveBranchRefusal? Refusal }`, not a bare `Guid?`)* | Api, §8.3 |

### 8.10 Exceptions

Reuse `DomainRuleViolationException`. No new exception type.

---

## 9. ADRs to record during this build

Append to `.ai/decisions/adr.md`. HO-ACC-001 owns ADR-024…041; **this handover owns ADR-042…054**
(README ADR table).

| ADR | Decision | Why it must be recorded |
|---|---|---|
| **ADR-042** | **`Administration` bounded context**, namespace segment `Administration`, schema `admin`, owning the roots in §8.1 | Not in `.ai/bounded-contexts.md` (OQ-1); every later area reads from it |
| **ADR-043** | **Every endpoint declares its access**: `[RequireAction]`, `[RequireCustomerSession]`, or `[PublicEndpoint]`. `EndpointAccessConventionTests` enumerates every controller action and **fails** on an undeclared one. Customer ownership is enforced by `OwnerScope` on `[RequireAction]` (§12.3): the customer's allowed actions are listed in `CustomerActions`, and the filter confirms the route's resource belongs to the caller | US-ADM-02 AC2 says "regardless of what the UI shows". Fail-closed per action (HO-ACC-001) does not catch an endpoint someone forgot to attribute; a convention test does |
| **ADR-044** | **Permissions remain code-defined.** US-ADM-02's criteria are "permissions match Appendix D" and "403 + logged" — both met by `ActionRoles`. Administrator-editable permissions are not built. **Amends the note in HO-ACC-001 ADR-036** that deferred editing "to US-ADM-02" | An editable matrix lets one mis-click lock the clinic out of its own records; no AC asks for it |
| **ADR-045** | **Time model.** Hours, shifts and closures are branch-local `TimeOnly`/`DateOnly`; the branch stores an **IANA** time-zone id; every instant is `timestamptz`; conversion happens only in `BranchCalendar` / `StaffAvailability` | Converting in every query is how a DST or second-branch bug gets in |
| **ADR-046** | **Active branch** comes from `X-Branch-Id`, validated against `StaffBranchAssignmentAggregate`, defaulting to the primary branch, and to the only active branch when a staff member has no assignment and exactly one branch exists | Multi-branch without code change (NFR-SCL-1) while the single-branch launch (A-1) needs no set-up |
| **ADR-047** | **Availability is a pure domain service** over roster, exceptions, approved leave and branch calendar. Rosters hold a current and one scheduled next weekly pattern; due patterns are promoted lazily, never by a job | Rule 4 names availability as a domain service; a pure function is what Appointments needs to test slot generation without a database |
| **ADR-048** | **`StaffLeaveApprovedEvent` / `LeaveCancelledEvent` are integration contracts**; approving leave does **not** look at appointments. Consumers must be idempotent | US-ADM-03 AC2 is delivered in two halves across two handovers; the split must be deliberate |
| **ADR-049** | **Prices and tax rates are append-only, effective-dated roots; no back-dating; consumers snapshot at transaction time** | US-ADM-04's AC. Overwriting a price column silently reprices history |
| **ADR-050** | **Business settings** are a typed registry in `Shared`, stored per scope (global, branch override), read through `IBusinessSettings` with a short cache invalidated on change; appsettings values become the **defaults** in the registry | FR-ADM-5 "change without a release"; one registry stops each area inventing its own settings table |
| **ADR-051** | **Audit-log read and export.** A read-only query over `audit.audit_entry`; export streamed as CSV, capped at 50 000 rows, cells beginning `= + - @` prefixed with `'` (CSV-injection guard); **every search-export is itself audited** (`BusinessEvent`, `AuditLogExported`, with the filters) | Reading the audit trail is a sensitive act; spreadsheet formula injection is a real attack on exported logs |
| **ADR-052** | **Content is `LimitedMarkup`, never HTML**; policy versions and acceptances are append-only; publishing a policy version writes the version and the document pointer (a recorded rule-1 exception, safe because the pointer is derivable) | Stored HTML is stored XSS waiting for a renderer; NFR-LEG-5 needs the exact accepted text retained |
| **ADR-053** | **Maintenance mode never gates the back office, sign-in or the admin API.** It is exposed as status and enforced only by `[StorefrontEndpoint]`-marked actions | US-ADM-08's AC; an administrator must always be able to turn it off |
| **ADR-054** | **Money** is `decimal(12,2)` with an ISO-4217 code; default `BDT` from settings; no floating point anywhere | SRS Q-5 is open; the representation must not need to change when it closes |

### 9.1 Open questions

| # | Question | Owner | Default in this build |
|---|---|---|---|
| OQ-1 | `.ai/bounded-contexts.md` / namespace conventions need the `Administration` context | Architect (separate task) | ADR-042 records it |
| OQ-2 | Should administrators edit permissions? | Owner | No (ADR-044) |
| OQ-3 | Who approves leave, and can an administrator approve their own? | Owner | Administrators approve; self-approval only when they are the sole administrator (logged as such) |
| OQ-4 | Cat Profile action mapping — Appendix D has two rows ("own", "any"), the API has 19 actions | Owner + clinical lead | §12.3's mapping |
| OQ-5 | Base currency and tax regime (SRS Q-5) | Accountant | `BDT`; rates are data |
| OQ-6 | Who edits content — "marketing manager" is not an Appendix D role | Owner | `Administrator` and `ShopManager` |
| OQ-7 | Branch phone format | Owner | Free text ≤ 30 |
| OQ-8 | Overnight opening (emergency hours past midnight)? | Owner (SRS Q-10) | Two intervals; no interval crosses midnight |
| OQ-9 | Leave span limit | Owner | 90 days |

---

## 10. Enumerations, constants and violation codes

### 10.1 Enumerations — `Shared/Common/Enums/`, derived from `Enumeration`, persisted by `Value`

| Type | Members |
|---|---|
| `ClosureKind` | `PublicHoliday`, `Closure` |
| `RoomKind` | `Consultation`, `Treatment`, `Theatre`, `Grooming` |
| `RosterExceptionKind` | `ExtraShift`, `ShiftCancelled` |
| `LeaveKind` | `Annual`, `Sick`, `Training`, `Other` |
| `LeaveStatus` | `Requested`, `Approved`, `Rejected`, `Cancelled` |
| `ServiceCategory` | `Consultation`, `Vaccination`, `Spay`, `Neuter`, `Dental`, `Grooming`, `Emergency`, `FollowUp` |
| `SettingType` | `Integer`, `Decimal`, `Boolean`, `TimeRange` |
| `ContentKind` | `Page`, `Faq`, `Banner` |
| `ContentStatus` | `Draft`, `Published`, `Unpublished` |
| `BannerPlacement` | `Home`, `Shop`, `Portal` |

### 10.2 The settings registry — `Shared/Common/Constants/SettingDefinitions.cs`

| Key | Group | Type | Default | Bounds | Branch override | Consumer |
|---|---|---|---|---|---|---|
| `booking.changeCutoffHours` | Booking | Integer | 4 | 0–72 | ✅ | HO-APT (FR-CLN-5) |
| `booking.slotHoldMinutes` | Booking | Integer | 10 | 2–30 | ✅ | HO-APT (FR-CLN-4) |
| `booking.waitlistClaimMinutes` | Booking | Integer | 30 | 5–240 | ✅ | HO-APT (FR-CLN-10) |
| `boarding.depositPercent` | Boarding | Decimal | 30.00 | 0–100 | ✅ | HO-BRD (FR-BRD-7) |
| `inventory.expiryAlertDays` | Inventory | Integer | 60 | 1–365 | ✅ | HO-IMP |
| `inventory.defaultReorderPoint` | Inventory | Integer | 10 | 0–100 000 | ✅ | HO-IMP |
| `clinical.vaccinationDueSoonDays` | Clinical | Integer | 30 (seed: `CatProfileOptions`) | 1–180 | ❌ | **HO-CAT — rewired here** |
| `clinical.defaultParasiteIntervalDays` | Clinical | Integer | 90 (seed: `CatProfileOptions`) | 7–365 | ❌ | HO-CAT (no reader yet — F20) |
| `notifications.quietHours` | Notifications | TimeRange | 21:00–08:00 | any | ✅ | HO-NTF (FR-NTF-5) |
| `security.staffIdleTimeoutMinutes` | Security | Integer | 30 (seed: `AccountOptions`) | 5–240 | ❌ | **HO-ACC — rewired here** |
| `commerce.currencyCode` | Commerce | *(string, ISO-4217, 3 letters)* | `BDT` | — | ❌ | ADR-054 |

`commerce.currencyCode` uses a fifth type `CurrencyCode`, added to `SettingType`. Adding a setting
later is one registry entry plus its consumer — **no migration** (values are rows).

### 10.3 Other constants

`AdministrationSeed.MainBranchId` · `SettingScopes.GlobalId` · `SystemStatusIds.Singleton` ·
`StaffRosterRules` (`RosterableRoles`, `MaxSlotsPerDay = 3`) · `BranchRules` (`MaxIntervalsPerDay = 3`) ·
`LeaveRules.MaxSpanDays = 90` · `AuditExportRules.MaxRows = 50_000` · `LimitedMarkup` (validator) ·
`AdministrationMessages` (shared message constants).

**Actions** — appended to HO-ACC-001's `Actions` and **the same `ActionRoles` table** (§12.3). Never a
second table.

### 10.4 Violation codes — append to `BusinessViolationCodes`

`BranchCodeAlreadyExists` · `InvalidTimeZone` · `InvalidOpeningInterval` · `LastActiveBranchCannotBeDeactivated` ·
`BranchNotFound` · `PastClosureCannotBeCancelled` · `InvalidClosure` · `RoomNameAlreadyExists` ·
`RoomNotFound` · `BranchNotAssigned` · `BranchAssignmentRequired` · `StaffNotRosterable` ·
`InvalidShiftSlot` · `InvalidPatternEffectiveDate` · `InvalidLeavePeriod` · `LeaveOverlapsExistingLeave` ·
`LeaveNotPending` · `LeaveCannotBeCancelled` · `ServiceCodeAlreadyExists` · `InvalidServiceDuration` ·
`ServiceRolesRequired` · `ServiceNotFound` · `PriceCannotBeBackdated` · `DuplicatePriceEffectiveDate` ·
`TaxClassNotFound` · `UnknownSetting` · `SettingNotBranchOverridable` · `InvalidSettingValue` ·
`ContentSlugAlreadyExists` · `InvalidContentMarkup` · `InvalidContentLink` · `PolicyNotFound` ·
`PolicyVersionNotCurrent` · `CatNotOwnedByCaller`.

`CatNotOwnedByCaller` is **never returned** to a customer — ownership failures are a `404` (§12.3),
so a customer cannot probe which cat ids exist.

---

## 11. Specified changes — write side

### 11.1 Commands — `Application/Commands/Administration/`

Namespace `CatEcommerce.Application.Commands.Administration`. Data only; ids minted in the controller;
`ValidateCommand = true` everywhere.

| # | Command | Phase |
|---|---|---|
| C1 | `CreateBranchCommand`, `UpdateBranchDetailsCommand`, `SetBranchWeeklyHoursCommand`, `SetBranchActivationCommand` | B |
| C2 | `CreateBranchClosureCommand`, `CancelBranchClosureCommand` | B |
| C3 | `CreateRoomCommand`, `UpdateRoomCommand` | B |
| C4 | `AssignStaffBranchesCommand` | B |
| C5 | `SetRosterPatternCommand` (`StaffAccountId`, `BranchId`, `Slots[]`, `EffectiveFrom?` — null = current) , `CancelScheduledRosterPatternCommand` | C |
| C6 | `CreateRosterExceptionCommand`, `CancelRosterExceptionCommand` | C |
| C7 | `RequestLeaveCommand` (`LeaveRequestId`, `StaffAccountId?` — null = the caller), `ApproveLeaveCommand`, `RejectLeaveCommand`, `CancelLeaveCommand` | C |
| C8 | `CreateClinicServiceCommand`, `UpdateClinicServiceCommand`, `SetClinicServiceActivationCommand`, `SetServicePriceCommand` | D |
| C9 | `CreateTaxClassCommand`, `UpdateTaxClassCommand`, `SetTaxRateCommand` | D |
| C10 | `ChangeBusinessSettingsCommand` (`BranchId?`, `Changes[]`), `ClearBranchSettingOverrideCommand` | D |
| C11 | `RecordAuditLogExportCommand` (the filters) | E |
| C12 | `CreateContentItemCommand`, `EditContentDraftCommand`, `PublishContentItemCommand`, `UnpublishContentItemCommand` | F |
| C13 | `CreatePolicyDocumentCommand`, `PublishPolicyVersionCommand` | F |
| C14 | `AcceptPolicyVersionCommand` (`PolicyKey`, `VersionNumber` — customer from `UserContext`) | F |
| C15 | `SetMaintenanceModeCommand` | F |

### 11.2 Validators — `CommandHandlers/Validators/` (flat), registered as `IValidator<T>`

Shape only: lengths, required fields, `EndsOn ≥ StartsOn`, non-empty collections, slug pattern, amount
≥ 0 with ≤ 2 decimal places. **Time-zone validity, interval overlap, role membership, setting bounds,
markup validity and "is in the future in branch time" are aggregate or handler rules**, not validator
rules — they need the aggregate's state or the branch's time zone.

### 11.3 Mappers and DTOs

`AdministrationMapper` (static `ToDomainDto()` extensions, `UserContext`/`CorrelationId` passed
through). DTOs in `Domain/Models/` (flat), one per aggregate method.

### 11.4 Command handlers — `CommandHandlers/Administration/`

The `RegisterCatCommandHandler` shape. Rules that need other records, in the handler:

| Handler | Cross-record rule in the handler |
|---|---|
| Create branch | Code uniqueness pre-check (`BranchCodeAlreadyExists`), and the unique index behind it (`UniqueConstraintExceptionFilter` mapping added, §12.5) |
| Set activation | Deactivating: count other active branches; zero → `LastActiveBranchCannotBeDeactivated` |
| Cancel closure | Load branch for its time zone; "future" is judged in branch-local date |
| Create / update room | Branch exists and is active; name unique per branch |
| Assign staff branches | Staff account exists (HO-ACC-001 repository); every branch exists and is active |
| Set roster pattern | Staff account exists, has a rosterable role, is assigned to the branch (or the sole-branch rule applies); every `DefaultRoomId` belongs to the branch; `EffectiveFrom` strictly after today in branch time; load-or-create the roster by the unique pair |
| Request leave | `StaffAccountId` null → the caller's id; non-null and ≠ caller → caller must hold `Administrator` (`ActionNotPermitted` otherwise); overlap pre-check against the person's `Requested`/`Approved` leave |
| Approve / reject | Decider rule (OQ-3): decider ≠ requester, unless the decider is the **only** staff account holding `Administrator` — then allowed and `DecisionNote` is prefixed `"[self-approved: sole administrator]"` |
| Set service price / tax rate | Service / tax class exists; `EffectiveFrom ≥ now` (`TimeProvider`); no existing row with the same `EffectiveFrom` |
| Create / update service | `TaxClassId` exists; every role in `StaffRoles.All`; code unique |
| Change settings | Branch exists when scoped; load-or-create the scope's aggregate |
| Publish policy version | Next version number = current + 1 (`MAX` over versions); **two writes**: `SaveAsync(version)` then `UpdateAsync(document)` — ADR-052's recorded exception |
| Accept policy version | Version exists and **is the current version** (`PolicyVersionNotCurrent` otherwise — accepting a superseded text is meaningless); already accepted → success, no write |
| Record audit-log export | Writes one `AuditEntryAggregate` (`BusinessEvent`, `Action = "AuditLogExported"`, `AfterValue` = the filters JSON) directly, like HO-ACC-001 C11 |

### 11.5 Domain events — `Events/Administration/`

Ids, dates, enums and booleans only — **no notes, reasons, bodies or names** (the audit interceptor
already records the data change with before/after; events are for consumers). HO-ACC-001's reflection
test U-X1 covers this assembly-wide.

`BranchCreatedEvent`, `BranchDetailsUpdatedEvent`, `BranchWeeklyHoursChangedEvent`,
`BranchActivationChangedEvent`, `BranchClosureCreatedEvent` (`BranchId`, `StartsOn`, `EndsOn`, `Kind`),
`BranchClosureCancelledEvent`, `RoomCreatedEvent`, `RoomUpdatedEvent`, `StaffBranchesAssignedEvent`,
`StaffRosterPatternChangedEvent` (`StaffAccountId`, `BranchId`, `EffectiveFrom?`),
`RosterExceptionCreatedEvent`, `RosterExceptionCancelledEvent`, `LeaveRequestedEvent`,
**`StaffLeaveApprovedEvent`** (§8.4 — integration contract), `LeaveRejectedEvent`,
**`LeaveCancelledEvent`** (`WasApproved`), `ClinicServiceCreatedEvent`, `ClinicServiceUpdatedEvent`,
`ServicePriceSetEvent` (`ServiceId`, `EffectiveFrom`), `TaxRateSetEvent`, `BusinessSettingsChangedEvent`
(`ScopeId`, `Keys[]`), `ContentItemPublishedEvent`, `ContentItemUnpublishedEvent`,
`PolicyVersionPublishedEvent` (`PolicyKey`, `VersionNumber`, `RequiresAcceptance`),
`PolicyVersionAcceptedEvent`, `MaintenanceModeChangedEvent` (`MaintenanceMode`).

`BranchClosureCreatedEvent` is also a contract: HO-APT-001 flags appointments inside a new closure.

### 11.6 Domain event handlers — `DomainEventHandlers/Administration/`

| Handler | Handles | Does |
|---|---|---|
| `InvalidateSettingsCacheHandler` | `BusinessSettingsChangedEvent` | Evicts the cached scope in `CachedBusinessSettings` |
| `AuditLeaveDecisionHandler` | `StaffLeaveApprovedEvent`, `LeaveRejectedEvent`, `LeaveCancelledEvent` | `BusinessEvent` row with the decider and the decision note (the reason lives here, not in the event) |
| `AuditMaintenanceModeHandler` | `MaintenanceModeChangedEvent` | `BusinessEvent` row |

Data changes need no handler — HO-ACC-001's `AuditChangeCaptureInterceptor` records every one with
before/after, in the same transaction (US-ADM-05 AC2 is met by it: "audited with the previous and new
value").

---

## 12. Specified changes — Infrastructure, read side, Api, host

### 12.1 Persistence

Seventeen `DbSet`s; one configuration per root in `Persistence/Configurations/`, modelled on
HO-ACC-001's (§12.1 there): schema `admin`, `ValueGeneratedNever()` on roots and owned ids, the F15
lines, enumerations by `Value`, `TimeOnly` → `time`, `DateOnly` → `date`, instants → `timestamptz`,
`Money` as two columns (`amount numeric(12,2)`, `currency_code char(3)`).

| Index | Kind |
|---|---|
| `branch.code` | **UNIQUE** `ix_branch_code` |
| `branch_closure (branch_id, starts_on, ends_on)` | calendar range scan |
| `room (branch_id, name)` | **UNIQUE** `ix_room_branch_name` |
| `staff_roster (staff_account_id, branch_id)` | **UNIQUE** `ix_staff_roster_staff_branch` |
| `roster_exception (branch_id, date)`, `(staff_account_id, date)` | range scans |
| `leave_request (staff_account_id, starts_on, ends_on)`, `(status)` | overlap check, pending list |
| `clinic_service.code` | **UNIQUE** `ix_clinic_service_code` |
| `service_price (service_id, effective_from)` | **UNIQUE** `ix_service_price_service_effective` |
| `tax_class.code`; `tax_rate (tax_class_id, effective_from)` | **UNIQUE** each |
| `content_item (kind, slug)` | **UNIQUE** `ix_content_item_kind_slug` |
| `policy_document.key`; `policy_version (policy_key, version_number)` | **UNIQUE** each |
| `policy_acceptance (customer_account_id, policy_key, version_number)` | **UNIQUE** |
| `audit.audit_entry (actor_user_id, occurred_on)`, `(entity_type, occurred_on)`, `(action, occurred_on)`, `(occurred_on)` | **added here** for US-ADM-06's filters — the only change to `audit.*` |

**Migrations**, each read before commit: `AddAdministrationBranches` (B — includes the `MAIN` seed),
`AddAdministrationRosters` (C), `AddAdministrationCatalogueAndSettings` (D),
`AddAuditLogSearchIndexes` (E — indexes only; the triggers from HO-ACC-001 stay untouched),
`AddAdministrationContent` (F). Phase A has no migration. **In the delivered build, all five are one
combined migration, `AddAdministration`** (see Amended (3)). It also adds a GiST exclusion
constraint on `leave_request` (`ex_leave_request_staff_no_overlap`, gate fix 2026-09-26, Amended
(12)), raw SQL via `migrationBuilder.Sql`, not an EF-modelled index. Two operational notes: **(a)**
`CREATE EXTENSION IF NOT EXISTS btree_gist` runs in the migration's own `Up()`; the database role
that runs migrations needs `CREATE` on the target database for this to succeed (`btree_gist` is a
PostgreSQL-trusted extension on 13+, so no superuser is required, but the privilege itself still is).
**(b)** Because EF Core does not model exclusion constraints, `dotnet ef database ensure-created`
(as opposed to `dotnet ef database update`) will create every table `AddAdministration` describes
but **not** this constraint or the extension — always migrate, never `EnsureCreated`, once this
migration exists. A database that already applied a pre-2026-09-26 copy of `AddAdministration`
(amended in place, not a new migration) silently lacks the constraint and must be recreated; this
does not affect any shared environment, since the migration was never on `master`.

### 12.2 Infrastructure services

| Class | Notes |
|---|---|
| `Configuration/CachedBusinessSettings : IBusinessSettings` | §8.9; seeds defaults from `SettingDefinitions`, which in turn take `CatProfileOptions` / `AccountOptions` values for the three seeded keys at startup |
| `Scheduling/BranchCalendarService : IBranchCalendar` | Loads with `AsNoTracking`, calls `BranchCalendar` |

**Rewiring the two existing consumers (F20, HO-ACC-001):**
- `GetCatVaccinationStatusQueryHandler` — replace `options.Value.VaccinationDueSoonWindowDays` with
  `settings.GetInt(SettingKeys.VaccinationDueSoonDays)`. Nothing else in that file changes.
- HO-ACC-001's `SessionAuthenticationMiddleware` / C14 idle rule — read
  `security.staffIdleTimeoutMinutes` from `IBusinessSettings`; `AccountOptions.StaffIdleTimeoutMinutes`
  remains only as the registry's seed default.

### 12.3 Authorization — US-ADM-02 — ADR-043, ADR-044

**Extend HO-ACC-001's `RequireActionAttribute`** with an optional `OwnerScope` (`None` \| `Cat`) and
add `PublicEndpointAttribute`. `RequireActionFilter` becomes:

1. Staff caller → role check against `ActionRoles` (unchanged).
2. Customer caller and `OwnerScope == Cat` and the action is in **`CustomerActions`** → dispatch
   `GetCatOwnerQuery(catId)` (route value `catId`); owner == caller → allow; **not found or not
   owned → `404`** (not `403` — a customer must not learn that a cat id exists). No `catId` in the
   route (list, register) → allow; the controller forces `OwnerId = caller` (F19).
3. Anything else → `401` / `403` exactly as HO-ACC-001 §12.8, with the denial audited.

**`ActionRoles` additions** (the one table, `Shared/Common/Constants/ActionRoles.cs`):

| Action | Staff roles | Customer (own) | Endpoints |
|---|---|---|---|
| `Cats.View` | Reception, Nurse, Vet, Caretaker, Administrator | ✅ | `GET /api/cats`, `/{id}`, `/{id}/weights`, `/{id}/vaccination-status`, `/{id}/medical-history.pdf` |
| `Cats.Register` | Reception, Nurse, Vet, Administrator | ✅ | `POST /api/cats` |
| `Cats.EditProfile` | Reception, Nurse, Vet, Administrator | ✅ | `PUT /{id}/profile`, `POST /{id}/photos` |
| `Cats.RecordClinical` | Nurse, Vet, Administrator | — | `PUT /{id}/neuter-status`, `PUT /{id}/medical-profile`, `POST /{id}/weights`, `POST /{id}/vaccinations`, `POST /{id}/parasite-treatments` |
| `Cats.ManageHighRiskFlag` | Nurse, Vet, Administrator | — | `PUT`/`DELETE /{id}/high-risk-flag` |
| `Cats.MarkDeceased` | Vet, Administrator | — | `POST /{id}/deceased` |
| `Cats.TransferOwnership` | Reception, Administrator | — | `POST /{id}/ownership-transfer` |
| `Cats.PrintCageCard` | Reception, Nurse, Vet, Caretaker, Administrator | — | `GET /{id}/cage-card` |
| `Vaccines.View` | all eight staff roles | — | `GET /api/vaccines` |
| `Vaccines.Manage` | Vet, Administrator | — | `POST /api/vaccines` |
| `Branches.View` | all eight staff roles | — | admin branch/room/closure reads |
| `Branches.Manage` | Administrator | — | branch, hours, closures, rooms, staff assignment writes |
| `Rosters.View` | Reception, Nurse, Vet, Caretaker, Administrator | — | rosters, exceptions, availability |
| `Rosters.Manage` | Administrator | — | pattern and exception writes |
| `Leave.Request` | all eight staff roles | — | own leave request / list / cancel |
| `Leave.Decide` | Administrator | — | pending list, approve, reject, cancel approved |
| `Services.View` | all eight staff roles | — | admin service and price reads |
| `Services.Manage` | Administrator | — | service, price writes |
| `TaxClasses.View` | Administrator, Accountant, ShopManager | — | reads |
| `TaxClasses.Manage` | Administrator | — | writes |
| `Settings.View` / `Settings.Manage` | Administrator / Administrator | — | settings |
| `AuditLog.View` / `AuditLog.Export` | Administrator, Accountant | — | Appendix D "Audit log: Accountant R, Admin F" |
| `Content.Manage` | Administrator, ShopManager | — | OQ-6 |
| `Policies.Manage` | Administrator | — | owner decision 2026-09-26, Amended (28) |
| `System.ManageMaintenance` | Administrator | — | maintenance toggle |

The **Cat mapping is an inference** from Appendix D's two rows (OQ-4) — record it in ADR-043 and flag
it in the ACCEPTANCE report.

**`CatsController` changes (the only edits to HO-CAT-001 code):** add the attribute to each action as
above; in `RegisterCat` and `GetCats`, when the caller is a customer, **overwrite** `OwnerId` /
`ownerId` with `UserContext.UserId`. `VaccinesController` gets its two attributes. Nothing else.

**`[PublicEndpoint]`** goes on: HO-ACC-001's captcha, registration, sign-in, reset and session-read
endpoints; this slice's public reads (§12.4). **`EndpointAccessConventionTests`** (U-A1) reflects over
every `ControllerBase` action and fails unless it carries exactly one of the three markers.

### 12.4 Api — the contract HO-ADM-002 consumes

Controllers inject `IInMemoryDispatcher` and `IUserContextProvider` (plus `IActiveBranchAccessor` where
stated). JSON camelCase; times as `"HH:mm"`, dates as `"yyyy-MM-dd"`, instants ISO-8601 with offset;
money as `{ "amount": "1500.00", "currencyCode": "BDT" }` (amount as a **string**, never a JSON number).

| Method | Route | Action | Command / query → success |
|---|---|---|---|
| `GET` | `/api/branches` | public | active branches: `id`, `name`, `address`, `phone`, `email`, `timeZoneId`, weekly hours — the clinic contact details US-CLN-04 shows |
| `GET` | `/api/admin/branches` · `/{branchId}` | `Branches.View` | list / detail incl. hours |
| `POST` | `/api/admin/branches` | `Branches.Manage` | C1 → `201 { branchId }` |
| `PUT` | `/api/admin/branches/{branchId}` · `/{branchId}/hours` · `/{branchId}/activation` | `Branches.Manage` | C1 → `204` |
| `GET` / `POST` | `/api/admin/branches/{branchId}/closures?from&to` | `Branches.View` / `Manage` | list / C2 → `201` |
| `DELETE` | `/api/admin/branches/{branchId}/closures/{closureId}` | `Branches.Manage` | C2 cancel → `204` |
| `GET` / `POST` | `/api/admin/branches/{branchId}/rooms` | `Branches.View` / `Manage` | list / C3 → `201` |
| `PUT` | `/api/admin/branches/{branchId}/rooms/{roomId}` | `Branches.Manage` | C3 → `204` |
| `GET` | `/api/admin/branches/{branchId}/calendar?from&to` | `Branches.View` | open intervals as instants, ≤ 62 days |
| `GET` / `PUT` | `/api/admin/staff/{staffAccountId}/branches` | `Branches.View` / `Manage` | assignment / C4 → `204` |
| `GET` | `/api/staff/branches/mine` | any staff | the caller's assigned branches + primary — feeds the branch picker. Always `200` (gate fix 2026-09-26: never 403 on a missing/bad active-branch header). Also returns `activeBranchId` (the resolved active branch, `null` when refused) and `activeBranchRefusal` (the `ActiveBranchRefusal` reason code from CONTRACTS.md #20, `null` when resolved) — both additive fields, for the picker to recover without a second round trip |
| `GET` | `/api/admin/branches/{branchId}/rosters` | `Rosters.View` | every roster at the branch: current + next pattern |
| `PUT` | `/api/admin/branches/{branchId}/rosters/{staffAccountId}/pattern` | `Rosters.Manage` | C5 → `204` |
| `DELETE` | `/api/admin/branches/{branchId}/rosters/{staffAccountId}/next-pattern` | `Rosters.Manage` | `204` |
| `GET` / `POST` | `/api/admin/branches/{branchId}/roster-exceptions?from&to` | `Rosters.View` / `Manage` | list / C6 → `201` |
| `DELETE` | `/api/admin/branches/{branchId}/roster-exceptions/{id}` | `Rosters.Manage` | `204` |
| `GET` | `/api/admin/branches/{branchId}/availability?from&to&staffAccountId&restrictToOpeningHours` | `Rosters.View` | `StaffAvailability` intervals (≤ 31 days), with `outsideOpeningHours` flags |
| `GET` / `POST` | `/api/staff/leave-requests` | `Leave.Request` | the caller's requests / C7 → `201` |
| `POST` | `/api/staff/leave-requests/{id}/cancel` | `Leave.Request` | C7 → `204` (own, requested only) |
| `GET` | `/api/admin/leave-requests?status&staffAccountId&from&to` | `Leave.Decide` | paged |
| `POST` | `/api/admin/leave-requests` | `Leave.Decide` | C7 on behalf of someone → `201` |
| `POST` | `/api/admin/leave-requests/{id}/approve` · `/reject` · `/cancel` | `Leave.Decide` | `204` |
| `GET` | `/api/services` | public | active, `isBookableOnline`, priced services with **current** price and duration |
| `GET` / `POST` | `/api/admin/services` | `Services.View` / `Manage` | list incl. inactive and `isPriced` / C8 → `201` |
| `GET` / `PUT` | `/api/admin/services/{serviceId}` | `Services.View` / `Manage` | detail / C8 → `204` |
| `PUT` | `/api/admin/services/{serviceId}/activation` | `Services.Manage` | `204` |
| `GET` / `POST` | `/api/admin/services/{serviceId}/prices` | `Services.View` / `Manage` | history (current, scheduled, past) / C8 → `201` |
| `GET` / `POST` / `PUT` | `/api/admin/tax-classes` (· `/{id}`) | `TaxClasses.View` / `Manage` | list / C9 |
| `GET` / `POST` | `/api/admin/tax-classes/{id}/rates` | `TaxClasses.View` / `Manage` | history / C9 → `201` |
| `GET` | `/api/admin/settings?branchId` | `Settings.View` | **every** definition: key, group, type, bounds, unit, default, `globalValue`, `branchValue?`, `effectiveValue`, `branchOverridable` |
| `PUT` | `/api/admin/settings` (· `?branchId`) | `Settings.Manage` | C10 → `204` |
| `DELETE` | `/api/admin/settings/{key}?branchId` | `Settings.Manage` | clear override → `204` |
| `GET` | `/api/admin/audit-entries?actorUserId&entityType&entityId&action&category&from&to&pageNumber&pageSize` | `AuditLog.View` | paged `{ items, pageNumber, pageSize, totalCount }`; item: `occurredOn`, `actorUserId`, **`actorDisplayName`** (joined from staff or customer, `"Anonymous"` for `Guid.Empty`), `action`, `category`, `entityType`, `entityId`, `reason`, `beforeValue`, `afterValue`, `ipAddress` |
| `GET` | `/api/admin/audit-entries/export.csv?…same filters` | `AuditLog.Export` | C11 then a streamed `text/csv`; `413` with a message when the filter matches more than 50 000 rows |
| `GET` / `POST` / `PUT` | `/api/admin/content` (· `/{id}`) | `Content.Manage` | list / C12 |
| `POST` | `/api/admin/content/{id}/publish` · `/unpublish` | `Content.Manage` | `204` |
| `GET` | `/api/admin/policies` | `Content.Manage` | list with versions |
| `POST` | `/api/admin/policies` · `/{key}/versions` | `Policies.Manage` (Administrator only, owner decision 2026-09-26, Amended (28)) | C13 |
| `GET` | `/api/content/{kind}/{slug}` | public | published only; `404` otherwise |
| `GET` | `/api/content/banners?placement` | public | published, inside their visibility window |
| `GET` | `/api/content/faqs` | public | published, sorted |
| `GET` | `/api/policies/{key}` | public | current version: number, body, published on |
| `GET` | `/api/customers/me/policy-acceptances/pending` | customer | current versions of `RequiresAcceptance` policies the caller has not accepted |
| `POST` | `/api/customers/me/policy-acceptances` | customer | C14 → `204` |
| `GET` | `/api/system/status` | public | `{ maintenanceMode, message }` |
| `PUT` | `/api/admin/system/maintenance` | `System.ManageMaintenance` | C15 → `204` |

Every write uses the HO-ACC-001 `400` (`ValidationResponse`) / `409` / `403` / `401` shapes (R1–R8 there).

### 12.5 Filters

- `UniqueConstraintExceptionFilter` (HO-ACC-001) — add mappings for `ix_branch_code`,
  `ix_room_branch_name`, `ix_clinic_service_code`, `ix_service_price_service_effective`,
  `ix_content_item_kind_slug`, the tax and policy indexes → the matching `400`;
  `ix_staff_roster_staff_branch` and a first-write race on a settings scope → `409`. Unknown
  constraints still rethrow.
- **`MaintenanceModeFilter`** — applied only to actions marked `[StorefrontEndpoint]`; when on → `503`
  with `Retry-After` absent and body `{ "error": "maintenance", "message" }`. **In this slice no action
  carries the marker** (ADR-053). A test proves an admin endpoint still answers during maintenance.
- **`ActiveBranchFilter`** is not a filter — `IActiveBranchAccessor` is resolved on demand, so
  endpoints that do not need a branch never fail on a bad header.

### 12.6 Read side — `src/Read/`

Queries in `Queries/Administration/`, handlers in `QueryHandlers/Administration/`, ViewModels flat.
Explicit projections; `AsNoTracking()`. Of note:

- `GetBranchCalendarQuery`, `GetStaffAvailabilityQuery` — load inputs, call the pure domain services
  (§8.5). **Never re-implement the calendar in SQL.**
- `GetServicesQuery(publicOnly)` — current price via a lateral "greatest `effective_from ≤ now`" join
  **in SQL**.
- `GetAuditEntriesQuery` — filters in SQL on the indexed columns; `OccurredOn` range **required** and
  ≤ 366 days (unbounded scans of an append-only table only grow); actor names via left joins to
  `account.staff_account` and `account.customer_account`. **Read-only** — the handler has no
  repository for audit entries.
- `GetCatOwnerQuery(catId)` → `Guid?` — used by the authorization filter only.

### 12.7 Host — `Program.cs`

Register every handler, validator (as `IValidator<T>`), query handler and event handler in §11–§12.6;
`IBusinessSettings` (Singleton cache wrapper + Scoped loader), `IBranchCalendar` (Scoped),
`IActiveBranchAccessor` (Scoped); `MaintenanceModeFilter` (Scoped, per action). **No new package; no
background service.** `ValidateScopes`/`ValidateOnBuild` stay on; I-A1 proves the registrations.

---

## 13. Allowed scope

```
src/Shared/CatEcommerce.Shared/Common/Enums/{the ten enumerations in §10.1, plus SettingType's CurrencyCode member}.cs
src/Shared/CatEcommerce.Shared/Common/Constants/{SettingDefinitions,SettingKeys,SettingScopes,AdministrationSeed,SystemStatusIds,StaffRosterRules,BranchRules,LeaveRules,AuditExportRules,AdministrationMessages,CustomerActions}.cs
src/Shared/CatEcommerce.Shared/Common/Constants/{Actions,ActionRoles}.cs                 ← append only
src/Shared/CatEcommerce.Shared/Common/Constants/BusinessViolationCodes.cs               ← append only
src/Shared/CatEcommerce.Shared/Common/Helpers/LimitedMarkup.cs
src/Domain/CatEcommerce.Domain/Aggregates/Administration/**
src/Domain/CatEcommerce.Domain/Entities/{OpeningInterval,ShiftSlot,SettingValue}.cs
src/Domain/CatEcommerce.Domain/ValueObjects/{Money,TimeInterval,TimeRange}.cs
src/Domain/CatEcommerce.Domain/DomainServices/{IBusinessSettings,IBranchCalendar,BranchCalendar,StaffAvailability}.cs
src/Domain/CatEcommerce.Domain/Models/**                                                ← new DTOs only
src/Domain/CatEcommerce.Domain/Events/Administration/**
src/Application/CatEcommerce.Application/Commands/Administration/**
src/Application/CatEcommerce.Application/CommandHandlers/Administration/**
src/Application/CatEcommerce.Application/CommandHandlers/Validators/{one per command in §11.1}.cs
src/Application/CatEcommerce.Application/DomainEventHandlers/Administration/**
src/Application/CatEcommerce.Application/DataMappers/AdministrationMapper.cs
src/Read/CatEcommerce.Read/Queries/Administration/**
src/Read/CatEcommerce.Read/QueryHandlers/Administration/**
src/Read/CatEcommerce.Read/Queries/Cat/GetCatOwnerQuery.cs
src/Read/CatEcommerce.Read/QueryHandlers/Cat/GetCatOwnerQueryHandler.cs
src/Read/CatEcommerce.Read/QueryHandlers/Cat/GetCatVaccinationStatusQueryHandler.cs     ← the one setting read (§12.2)
src/Read/CatEcommerce.Read/ViewModels/{the ViewModels in §12.4}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/CatEcommerceDbContext.cs    ← DbSets only
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/{one per new root}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/AuditEntryConfiguration.cs ← the four indexes only
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/**                ← the five in §12.1 only
src/Infrastructure/CatEcommerce.Infrastructure/Configuration/CachedBusinessSettings.cs
src/Infrastructure/CatEcommerce.Infrastructure/Scheduling/**
src/Api/CatEcommerce.Api/Controllers/{Branches,AdminBranches,StaffBranches,AdminRosters,LeaveRequests,AdminLeaveRequests,Services,AdminServices,AdminTaxClasses,AdminSettings,AdminAuditEntries,Content,AdminContent,Policies,CustomerPolicyAcceptances,SystemStatus,AdminSystem}Controller.cs
src/Api/CatEcommerce.Api/Controllers/CatsController.cs                                  ← attributes + customer owner forcing only (§12.3)
src/Api/CatEcommerce.Api/Controllers/VaccinesController.cs                              ← attributes only
src/Api/CatEcommerce.Api/Controllers/{HO-ACC-001's controllers}.cs                      ← [PublicEndpoint] markers only
src/Api/CatEcommerce.Api/Contracts/Administration/**
src/Api/CatEcommerce.Api/Authorization/**                                               ← OwnerScope, PublicEndpoint, CustomerActions check
src/Api/CatEcommerce.Api/Sessions/SessionAuthenticationMiddleware.cs                    ← idle timeout read from settings only
src/Api/CatEcommerce.Api/Branches/**                                                    ← IActiveBranchAccessor
src/Api/CatEcommerce.Api/Filters/{UniqueConstraintExceptionFilter,MaintenanceModeFilter}.cs
src/Api/CatEcommerce.Api/Program.cs                                                     ← §12.7 only
tests/CatEcommerce.Domain.Tests/**
tests/CatEcommerce.Application.Tests/**
tests/CatEcommerce.IntegrationTests/**
docs/test-cases/TC-HO-ADM-001.md
docs/gates/HO-ADM-001-*.md
.ai/decisions/adr.md                                                                     ← ADR-042…054, append only
```

## 14. Forbidden scope

| Path | Why |
|---|---|
| `.ai/**` except `decisions/adr.md`, `.claude/**` | Governing docs and agents (OQ-1 is a separate task) |
| `docs/SRS.md`, `docs/USER_STORIES.md`, `docs/handover/**` | Inputs |
| `web/**` | HO-ADM-002 |
| `src/Domain/…/Aggregates/{Cat,Vaccination,Audit,Account}/**` | Other areas' aggregates — **`StaffAccountAggregate` is not edited** (§8.3) |
| Every other `Cat*` / `Vaccine*` file in Application and Read, except the three listed in §13 | HO-CAT-001's |
| `audit.audit_entry` columns and triggers | HO-ACC-001's — this slice adds indexes only |
| `src/Api/…/appsettings.Development.json`, `NuGet.Config`, `global.json`, `*.sln`, `*.csproj` | No package is added |
| Any other bounded context | Appointments, Boarding, Orders … consume the contracts here later |

---

## 15. Unit test cases

`tests/CatEcommerce.Domain.Tests` / `Application.Tests`; Infrastructure/Api-class rows in
`IntegrationTests` as plain unit tests.

| ID | Case | Expected |
|---|---|---|
| **A — access** | | |
| U-A1 | `EndpointAccessConventionTests`: every controller action in the Api assembly | Exactly one of `[RequireAction]`, `[RequireCustomerSession]`, `[PublicEndpoint]`. **Fails the build on an undeclared endpoint** |
| U-A2 | Every `[RequireAction]` action name | Present in `ActionRoles` (none fall through to fail-closed by accident) |
| U-A3 | `ActionRoles` against `StaffRoles.All` | Every role id referenced exists; no `Customer` in a staff list |
| U-A4 | Filter: customer, `Cats.View`, own cat | Allowed |
| U-A5 | Filter: customer, `Cats.View`, someone else's cat; unknown cat | Both `404`; **no** audit denial row distinguishes them |
| U-A6 | Filter: customer, `Cats.RecordClinical`, own cat | `403` (not in `CustomerActions`) |
| **B — branches** | | |
| U-B1 | `SetWeeklyHours` with overlapping, reversed, midnight-crossing and 4-per-day intervals | `InvalidOpeningInterval` each |
| U-B2 | `Create` with `"Europe/Nowhere"` | `InvalidTimeZone` |
| U-B3 | `BranchCalendar` for a week with a Friday closed day, a Tuesday half-day closure and a lunch break | Exact instant intervals in `Asia/Dhaka` (+06:00) |
| U-B4 | `BranchCalendar` for a branch in `Europe/London` across the October DST change | Intervals shift correctly in UTC; local wall-clock times constant |
| U-B5 | Cancelled closure | Ignored by the calendar |
| U-B6 | Active-branch resolution: header in assignment; header not in assignment; no header; no assignment with one branch; no assignment with two | primary / `BranchNotAssigned` / primary / the sole branch / `BranchAssignmentRequired` |
| **C — rosters & leave** | | |
| U-C1 | `StaffAvailability` rules 1–7 (§8.5), one row each | As stated |
| U-C2 | Next pattern effective on Wednesday | Mon–Tue from current, Wed on from next |
| U-C3 | Approved leave vs requested leave | Only approved removes days |
| U-C4 | `restrictToOpeningHours` false with a shift outside hours | Interval present, `OutsideOpeningHours = true` |
| U-C5 | Leave: approve twice; cancel approved leave that ended yesterday | `LeaveNotPending`; `LeaveCannotBeCancelled` |
| U-C6 | `StaffLeaveApprovedEvent` reflection | Only `LeaveRequestId`, `StaffAccountId`, `StartsOn`, `EndsOn` (+ inherited) |
| U-C7 | Handler: a nurse requests leave for someone else | `ActionNotPermitted` |
| U-C8 | Handler: overlapping request | `LeaveOverlapsExistingLeave` |
| U-C9 | Handler: sole administrator approves own leave | Allowed; decision note prefixed |
| **D — catalogue & settings** | | |
| U-D1 | Service duration 7, 0, 485; roles empty; unknown role | `InvalidServiceDuration` ×3; `ServiceRolesRequired`; `UnknownStaffRole` |
| U-D2 | Price "at *t*" with past, current and scheduled prices | The greatest `EffectiveFrom ≤ t` |
| U-D3 | `SetServicePrice` with `EffectiveFrom` one minute ago | `PriceCannotBeBackdated` |
| U-D4 | Settings: unknown key; out of bounds; branch override of a non-overridable key; mixed valid+invalid list | Each code; **nothing applied** on the mixed list |
| U-D5 | Resolution order: branch override → global → default | Each level wins in turn |
| U-D6 | Every `SettingDefinitions` default | Within its own bounds; keys unique |
| U-D7 | `Money` | No `double` anywhere; amount scale 2; negative rejected |
| **E — audit log** | | |
| U-E1 | CSV writer with a reason `=HYPERLINK(...)` and `+1`, `-1`, `@x` | Each cell prefixed `'` |
| U-E2 | Query validator: missing range; range 400 days | `400` each |
| **F — content** | | |
| U-F1 | `LimitedMarkup.Validate` with `<script>`, `<b>`, `javascript:` link, `data:` link, `http:` link | `InvalidContentMarkup` / `InvalidContentLink` |
| U-F2 | Valid markup with headings, bullets, bold, https and relative links | Passes |
| U-F3 | Publish copies draft; editing the draft afterwards does not change `PublishedBody` | As stated |
| U-F4 | Policy version numbering | 1, 2, 3; never reused |
| U-F5 | Accept a superseded version | `PolicyVersionNotCurrent` |

## 16. Integration test cases

Real HTTP, `Testing` environment, shared database (HO-ACC-001 §16.1 rules 1–8, including signing in
real staff through the cookie path). **A seeded administrator, nurse, receptionist and customer** per
test via the helpers HO-ACC-001 defines.

| ID | Case | Expected |
|---|---|---|
| I-A1 | Host starts, scope validation on | Every registration resolves |
| I-A2 | For **each** Cat and Vaccine endpoint: anonymous, customer (own), customer (other's cat), each staff role | Status matches §12.3's table for every cell — **US-ADM-02 AC1** |
| I-A3 | A nurse calls `POST /api/cats/{id}/deceased` | `403`; one `AuthorizationDenied` audit row — **US-ADM-02 AC2** |
| I-A4 | A customer `POST /api/cats` with another customer's `ownerId` in the body | `201`; the cat's owner is the **caller** |
| I-A5 | A customer `GET /api/cats?ownerId=<other>` | Only the caller's cats |
| I-A6 | HO-CAT-002's flows re-run with signed-in staff | Unchanged results |
| I-B1 | Migration on an empty database | `admin` schema, every table and index; `MAIN` branch seeded with no hours |
| I-B2 | Create branch, set hours, add a public holiday and a half-day closure, `GET` calendar for the week | Intervals exact; holiday absent; half-day shortened |
| I-B3 | Duplicate branch code; duplicate room name in a branch; same room name in another branch | `400`; `400`; `201` |
| I-B4 | Deactivate the only active branch | `400 LastActiveBranchCannotBeDeactivated` |
| I-B5 | Staff member assigned to two branches sends `X-Branch-Id` of a third | `403 BranchNotAssigned` on a branch-needing endpoint; unaffected elsewhere |
| I-B6 | `GET /api/branches` anonymous | Active branches with contact details and hours; no internal ids beyond `id` |
| I-C1 | Set a vet's pattern, add an extra shift, approve a leave day, `GET` availability for the week | Exact intervals; leave day empty; extra shift present |
| I-C2 | Schedule next pattern, advance `FakeTimeProvider` past its date, `GET` roster | Promoted (lazily, on read) |
| I-C3 | Nurse requests leave; admin approves | `201`; `204`; leave `Approved`; a `BusinessEvent` audit row with the decider |
| I-C4 | Event spy on approval | `StaffLeaveApprovedEvent` raised once with the right range |
| I-C5 | Nurse approves a request | `403` |
| I-C6 | Roster for a staff member with only `Accountant` | `400 StaffNotRosterable` |
| I-D1 | Create a service, set price today+0 and a scheduled price next month; `GET /api/services` | Current price only; `GET …/prices` shows both |
| I-D2 | Two prices with the same `EffectiveFrom` concurrently | One `201`, one `400` — never two rows |
| I-D3 | Change `clinical.vaccinationDueSoonDays` to 60; `GET` a cat's vaccination status whose next due is in 45 days | `DueSoon` — **the existing consumer reads the setting**, visible on the next request |
| I-D4 | Change `security.staffIdleTimeoutMinutes` to 5; idle 6 minutes | `401 session_idle_timeout` |
| I-D5 | Any settings change | `DataChange` audit row with before and after values — **US-ADM-05 AC2** |
| I-D6 | `GET /api/admin/settings` | Every registry key with bounds, unit, default and effective value — **US-ADM-05 AC1** |
| I-E1 | Search audit entries by actor, by entity, by action, by category, by range | Correct rows; `actorDisplayName` populated — **US-ADM-06** |
| I-E2 | Export CSV for the same filters | Streamed; header row; injection-guarded cells; an `AuditLogExported` audit row with the filters |
| I-E3 | Export over the cap (lower the cap in the factory) | `413` with a message |
| I-E4 | Accountant searches; receptionist searches | `200`; `403` |
| I-F1 | Create page, publish, edit draft, `GET /api/content/page/{slug}` | Returns the **published** body, not the draft |
| I-F2 | Unpublish | Public `404` |
| I-F3 | Banner outside its window | Not returned |
| I-F4 | Publish policy v1 (`RequiresAcceptance`), customer `GET` pending → accept → pending | v1 pending; `204`; empty |
| I-F5 | Publish v2 | v2 pending again; v1 acceptance **retained** with its version — **US-ADM-07 AC2** |
| I-F6 | Content with `<script>` | `400 InvalidContentMarkup` |
| I-F7 | Maintenance on; `GET /api/system/status`; any admin endpoint; staff sign-in | `maintenanceMode: true`; admin `200`; sign-in `200` — **US-ADM-08** |
| I-X1 | Log capture across all cases | No setting value that is a secret (none exist), no policy body, no note text at `Information` or above |

---

## 17. Implementation order

| Phase | Stories | Delivers |
|---|---|---|
| **A** | US-ADM-02 | `OwnerScope`, `PublicEndpoint`, `CustomerActions`, the `ActionRoles` additions for Cat and Vaccine, `GetCatOwnerQuery`, `CatsController`/`VaccinesController` attributes and owner forcing, markers on HO-ACC-001 controllers, U-A1 green |
| **B** | US-ADM-01 | Branch, closures, rooms, assignment, active branch, calendar; seed |
| **C** | US-ADM-03 | Rosters, exceptions, leave, availability |
| **D** | US-ADM-04, US-ADM-05 | Services, prices, tax; settings registry, cache, the two rewired consumers |
| **E** | US-ADM-06 | Audit search, export, indexes |
| **F** | US-ADM-07, US-ADM-08 | Content, policies, acceptance, maintenance |

Commands as HO-ACC-001 §17 (`restore` → `build` → unit tests → `migrations add` → read the migration →
`database update` → run → integration tests). Nothing to add to `appsettings.json` — settings live in
the database with registry defaults.

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | ADM-01 AC1 | Opening hours and holidays determine open time — *delivered as the calendar every availability consumer must use* | U-B3, U-B4, I-B2 |
| A2 | ADM-01 AC2 | A second branch is added and used with no code change; staff are branch-assigned | I-B2, I-B5, U-B6 |
| A3 | ADM-02 AC1 | Every endpoint's access matches Appendix D (Cat mapping per OQ-4) | U-A1, U-A2, I-A2 |
| A4 | ADM-02 AC2 | A disallowed call gets `403` regardless of UI, and is logged | I-A3 |
| A5 | ADM-02 | Customers reach only their own cats; ownership probes are indistinguishable | U-A5, I-A4, I-A5 |
| A6 | ADM-03 AC1 | Rosters and leave determine staff availability — *consumed by HO-APT / HO-BRD* | U-C1…U-C4, I-C1, I-C2 |
| A7 | ADM-03 AC2 | *(server half)* Approving a vet's leave publishes the event Appointments uses to flag bookings | U-C6, I-C4. **Flagging itself: HO-APT-001** |
| A8 | ADM-04 AC | A price change applies to new transactions only — prices are effective-dated, never back-dated, never overwritten | U-D2, U-D3, I-D1, I-D2 |
| A9 | ADM-05 AC1 | Every listed business rule is an editable, validated setting | U-D4, U-D6, I-D6 |
| A10 | ADM-05 AC2 | Each change is audited with previous and new value | I-D5 |
| A11 | ADM-05 | Changed settings take effect without a restart for existing consumers | I-D3, I-D4 |
| A12 | ADM-06 AC | Audit entries filterable by user, entity, action and date, and exportable | I-E1, I-E2, U-E1 |
| A13 | ADM-07 AC1 | Pages, banners, FAQs and policies can be edited and published | I-F1, I-F2, I-F3 |
| A14 | ADM-07 AC2 | The version each customer accepted is retained | I-F4, I-F5 |
| A15 | ADM-08 AC | Maintenance mode never blocks the back office; status is published for the storefront | I-F7 |
| A16 | — | No HTML content can be stored | U-F1, I-F6 |

**Deferred, named in §4.2**: appointment flagging on leave (HO-APT-001), boarding task assignment
(HO-BRD-001), rate cards (HO-BRD-001), delivery zones (HO-ORD-001), storefront rendering (HO-SHP-002).

---

## 19. Required gates

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | `Administration` context (ADR-042, OQ-1); pure domain services (ADR-047); the one rule-1 exception (ADR-052); no second role table; the scoped edits to HO-CAT-001 and HO-ACC-001 files match §13 exactly |
| 2 | SECURITY | `security-reviewer` | **U-A1 convention and I-A2 matrix**; customer ownership (`404` not `403`); active-branch validation; audit-log export audited and injection-guarded; `LimitedMarkup` with no HTML path; maintenance mode cannot lock admins out |
| 3 | CODE_REVIEW | `code-reviewer` | Time handling only in the domain services; money never floating point; explicit projections |
| 4 | TEST | `test-engineer` | `TC-HO-ADM-001.md` from §15/§16 first; I-A2 is a full matrix, not a sample |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row; A7's split and the §4.2 deferrals recorded as deferrals |

---

## 20. BLOCKED conditions — stop and report

**20.1 HO-ACC-001 is not merged.** This slice extends its filter, its role table, its session
middleware and its audit interceptor. Do not stub them.

**20.2 An endpoint cannot be classified.** If an action's audience is unclear, stop and ask — never
mark it `[PublicEndpoint]` to make U-A1 pass.

**20.3 Do not edit `StaffAccountAggregate`** or any other area's aggregate to add a branch or a roster.

**20.4 Do not build appointment flagging, boarding assignment, rate cards or delivery zones** — §4.2.

**20.5 Do not store or render HTML.** If a content requirement seems to need it, it is a new decision.

**20.6 Do not overwrite a price, rate, policy version or acceptance.** They are append-only.

**20.7 Do not convert local time outside `BranchCalendar` / `StaffAvailability`.** A second conversion
is a second place for a DST bug.

**20.8 Do not add a background job** for pattern promotion, closure expiry or anything else — ADR-047.

**20.9 A requirement conflict is a stop, not an edit** — SRS and backlog are forbidden scope.
