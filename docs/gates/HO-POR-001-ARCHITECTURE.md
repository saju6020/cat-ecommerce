GATE: ARCHITECTURE
HANDOVER: HO-POR-001
COMMITS: 68e0be0f 2cf203e0 9b1fd21b 05ebee45 55339e38 a0127b48 a45a85e8 (branch work on ho/POR-001 against merge-base master a8761829; master's own changes excluded)
DATE: 2026-10-10
STATUS: PASS

Judged as built through Phase D, per HO-POR-001 §1 Amended 2026-10-10 (finalise) and §17: Phases E/F (stays,
subscriptions) are deferred by the owner, and the portal re-order (C1, §8.7, ADR-263) is retired in favour of Orders'
`POST /api/customers/me/orders/{orderId}/reorder`. Those decisions were owner-delegated on 2026-10-10 and are
"owner to review". This gate accepts them as the governing amendment and does not re-decide them.

Docs read: HO-POR-001 (all of it), `.ai/architecture.md`, `.ai/cqrs.md`, `.ai/read-side-pattern.md`,
`.ai/bounded-contexts.md`, `.ai/namespace-conventions.md`, `.ai/di-registration-guide.md`,
`.ai/enumeration-pattern.md`, `.ai/decisions/adr.md` (ADR-257..268 as added on the branch).

WHAT WAS VERIFIED (no finding)
- The portal owns no data (ADR-257). No aggregate, DbSet, IEntityTypeConfiguration, migration, schema, event or
  event handler in `Portal`. No change under `src/Domain/**`, `src/Infrastructure/**` (including Migrations), or in any
  `*.csproj` / `*.sln`. No new package.
- Composition goes only through other modules' published query handlers (ADR-258). Every reader's constructor takes
  only `IQueryHandlerAsync<,>` of published customer-facing queries (Cat `GetCatsQuery` / `GetCatVaccinationStatusQuery`,
  APT `GetCustomerAppointmentsQuery`, ACC `GetCustomerProfileQuery`, EMR `GetCustomerCatVisitsQuery`, PHM
  `GetCustomerPrescriptionsQuery`, BIL `GetCustomerBillingSummaryQuery` / `GetCustomerInvoicesQuery`, ORD
  `GetCustomerOrdersQuery`), plus `ILogger` and `TimeProvider`. No `CatEcommerceDbContext`, no repository, no
  `IInMemoryDispatcher`, no module internals (no clock ports, stock readers, Billing aggregates, business-date helpers
  or other modules' DbSets). Each upstream ViewModel is mapped member by member into a `Portal*` type (ADR-259).
- Scoping to the signed-in customer (ADR-261). `GetPortalDashboardQuery` has no customer-id property; the caller is
  `UserContext.UserId`. Every upstream handler consumed filters by the `CustomerId` the reader passes (checked:
  appointments, orders, profile, prescriptions, Billing via `CustomerSet.Of`); cat ids for the vaccination and visit
  reads come only from the owner-filtered `GetCatsQuery` in the same request, and EMR re-checks ownership.
- Layering. Controller injects `IInMemoryDispatcher` + `IUserContextProvider` only, dispatches one query with
  `GetResultAsync`, carries `[RequireCustomerSession]`, returns `200` with `Cache-Control: no-store`. Read references
  no Application type. No `IDispatcher`, `IEventPublisher`, bus, MongoDB, `202`, `[Authorize]` or payment surface.
- Reads never write. Sequential composition (ADR-267, no `Task.WhenAll`). Single catch site in the composer plus the
  per-cat vaccination degradation §8.3 requires (recorded in ADR-260).
- Host. Registrations are in `Modules/PortalModule.cs` (the ADR-341 per-area file `Program.cs` already calls; it is
  the §12.5 "Program.cs registrations" in the current host shape). All Scoped, explicit, no hand-registered platform
  types. `ValidateScopes` / `ValidateOnBuild` remain on in `Program.cs`. Count: Q1 handler 1, composer 1, readers 7
  = the readers that exist; no stays/subscriptions/re-order registrations (deferred/retired).
- Conventions. Namespaces `Read.Queries.Portal`, `Read.QueryHandlers.Portal[.Sections]`, flat `Read.ViewModels`,
  `Shared.Common.Constants`, `Shared.Common.Enums`. `PortalSectionState` is an `Enumeration` (values from 1,
  `nameof`), not persisted.
- Approved cross-area edits (CONTRACTS rows 30, 31; Amended rows in HO-EMR-001 and HO-ORD-001) are additive only:
  `CustomerVisitListItemViewModel` gains trailing `FollowUpOn` (projection reads the existing `FollowUp.DueOn`);
  `CustomerOrderSummaryViewModel` gains trailing `TrackingNumber` and `CanReorder`; `CustomerOrderRules.CanReorder`
  is the single rule the order detail now also calls (same four statuses as before - behaviour unchanged). Each record
  has one constructor call site, its own handler. No rename, removal, schema change or migration.

FINDINGS
- [LOW] .ai/decisions/adr.md (ADR-263 "Update 2026-10-10" paragraph) — the status line says Retired, but the Update
  paragraph below it still says the re-order "stays unbuilt until the owner chooses"; the same stale wording is in
  `src/Shared/CatEcommerce.Shared/Common/Constants/PortalMessages.cs:3` ("per-ReorderLineOutcome messages arrive with
  Phase D"). Documentation drift only (the PortalModule/VisitsSectionReader comment drift is already POR backlog 4).
- [LOW] src/Read/CatEcommerce.Read/QueryHandlers/Portal/Sections/BalancesSectionReader.cs:53 — the reader re-derives
  Billing's "open invoice" definition (`Issued`/`PartPaid` with a positive balance) from Billing's status names,
  because Billing's customer invoice list has no status filter. A recorded §20.2 adaptation, already tracked as
  BIL backlog 46; noted here so the rule copy goes when Billing adds the filter.
- [LOW] .ai/bounded-contexts.md / .ai/namespace-conventions.md — `Portal` is still not listed as a context/feature
  name (OQ-1). §14 forbids this branch from editing them; recorded as §19 asks, for the architect's separate task.
- [LOW] docs/handover/HO-POR-001.md §13 — allows ".ai/decisions/adr.md ← ADR-257…267" while the branch also added
  ADR-268; §6 says the handover owns ADR-257…276, so it is within the handover's range. Handover text inconsistency.
- [LOW] Owner review pending — the scope of this PASS for six docs files (below) and the four EMR/ORD source files
  rests on owner-DELEGATED decisions of 2026-10-10 ("owner to review"). If the owner reverses any of them, this gate
  must be re-run for the affected files. ACCEPTANCE should confirm the owner's review.

SCOPE CHECK
- files in allowed scope: 43/43 (33 by §13's literal list, counting `Modules/PortalModule.cs` as §12.5's `Program.cs`
  registrations under ADR-341; 4 by the owner-delegated approval of additive EMR/ORD edits; 6 docs by HO-POR-001
  §1 Amended 2026-10-10 (finalise) items (7) and (8))
  - EMR/ORD source, approved additive (CONTRACTS 30, 31): src/Read/CatEcommerce.Read/ViewModels/DischargeSummaryViewModels.cs,
    src/Read/CatEcommerce.Read/QueryHandlers/Visit/DischargeSummaryQueryHandlers.cs,
    src/Read/CatEcommerce.Read/ViewModels/OrderViewModels.cs, src/Read/CatEcommerce.Read/QueryHandlers/Order/OrderQueryHandlers.cs
    (their tests, DischargeSummaryAndFollowUpEndpointTests.cs and OrderingFulfilmentEndpointTests.cs, fall under §13's
    tests/CatEcommerce.IntegrationTests/** glob)
  - docs under §14's docs/handover/** but authorised by the §1 Amended (finalise) row: docs/handover/HO-POR-001.md
    (Amended rows, retirement notes), docs/handover/HO-POR-002.md, docs/handover/HO-EMR-001.md, docs/handover/HO-ORD-001.md
    (Amended rows), docs/handover/CONTRACTS.md (rows 30, 31), docs/backlog/BIL.md (row 46, named in item (7))
- files outside allowed scope: none
- no migration on the branch; no Domain, Infrastructure, csproj, sln, web or .ai (other than adr.md) change.
  `.ai/decisions/adr.md` change is append-only (0 removed lines).

Medium/low findings → docs/backlog/POR.md rows 8-11 [now 10-13] (owner rule 2026-09-27: only BLOCKER/HIGH block).

Note: backlog rows renumbered 8-11 → 10-13 on 2026-10-10 (collision with SECURITY).
