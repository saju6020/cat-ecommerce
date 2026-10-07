# Vendor Management Flow

## Status: In Progress

## What it does
Manages the supplier lifecycle: registration, classification (Local/International/Manufacturer/Distributor/ServiceProvider), document storage (trade license, VAT certificate, TIN, bank info, contracts), blacklisting, and performance rating (delivery time, quality, pricing, compliance). SRS §3.1 (FR-VM-001..005).

## Key aggregates / components
- `VendorAggregate` (src/Domain/Aggregates)

## Key files
- `src/Api/Controllers/VendorsController.cs` — all four POST actions (`register`, `blacklist`,
  `reinstate`, `ratings`) send their Command via `IBusMessageDispatcher.SendAsync` (202 Accepted);
  `GET /api/vendors/{id}`, `GET /api/vendors/{id}/ratings` and `GET /api/vendors` stay synchronous
  via `IDispatcher`
- `src/Application/Commands/Vendor/RegisterVendorCommand.cs` — carries the server-minted `VendorId`
- `src/Application/Commands/Vendor/BlacklistVendorCommand.cs`, `ReinstateVendorCommand.cs`,
  `RateVendorPerformanceCommand.cs` (HO-VM-001) — `RateVendorPerformanceCommand.RatingId` is
  server-minted in the controller, exactly like `VendorId` for registration
- `src/Application/CommandHandlers/Vendor/*CommandHandler.cs` — one handler per command, all
  registered and executed only in CommandWorker (ADR-004). Blacklist/Reinstate/RatePerformance
  pre-check `VendorStatus` before mutating; all four commands, including Register (HO-VM-004),
  publish `VendorCommandRejectedEvent` (ADR-005) on every failure path (`VendorNotFound`,
  `InvalidStatusTransition`, `DuplicateVendor`)
- `src/Application/CommandServices/VendorCommandService.cs` — trade licence uniqueness (Register
  only; Blacklist/Reinstate/RatePerformance load the aggregate directly, no new CommandService
  method needed)
- `src/Domain/Aggregates/VendorAggregate.cs` — `Register()`, `Blacklist()`, `Reinstate()`,
  `RatePerformance()`. Each guards its own precondition (throws `InvalidOperationException` if
  violated) as defense-in-depth behind the handler's pre-check
- `src/Domain/Events/Vendor/VendorRegisteredEvent.cs` — implements `INotifiedByEventHandler`
- `src/Domain/Events/Vendor/VendorBlacklistedEvent.cs`, `VendorReinstatedEvent.cs`,
  `VendorPerformanceRatedEvent.cs` (HO-VM-001) — plain `Event`, deliberately do NOT implement
  `INotifiedByEventHandler` (blacklist/reinstate/rating notifications are out of scope; §4)
- `src/Domain/Events/Vendor/VendorCommandRejectedEvent.cs` (HO-VM-001, ADR-005) — implements
  `INotifiedByEventHandler`; published, never sent; NOT projected (no `VendorViewModel` field owns
  it, no `EventConsumerAdapter` registered for it)
- `src/Read/EventHandlers/Vendor/VendorRegisteredEventHandler.cs`,
  `VendorBlacklistedEventHandler.cs`, `VendorReinstatedEventHandler.cs`,
  `VendorPerformanceRatedEventHandler.cs` — HO-VM-003: the latter three now write to two
  collections each time - a child document (`VendorStatusChangeViewModel`/`VendorRatingViewModel`,
  `Id` = the event's own `ChangeId`/`RatingId`, existence-checked before insert for redelivery
  idempotency) and the parent `VendorViewModel`'s scalar rollup (still version-checked CAS, per
  CODE_REVIEW Warning 1 on HO-VM-001)
- `src/Read/QueryHandlers/Vendor/GetVendorByIdQueryHandler.cs`, `GetVendorRatingsQueryHandler.cs`
  (HO-VM-003: pages `VendorRatingViewModel` - its own collection, keyed by `VendorId` - at the
  database via `IReadRepository.GetItemsAsync` skip/take, newest-first),
  `GetVendorStatusHistoryQueryHandler.cs` (HO-VM-003 SECURITY gate H1 fix, 2026-08-20: reads
  `VendorStatusChangeViewModel`'s own collection, **newest 50 only** (`OrderByDescending(ChangedOn)`,
  `take: MaxItems + 1` to answer `HasMore` in the same round trip), returning
  `VendorStatusHistoryResult(Items, HasMore)` instead of an unbounded `List<...>` - feeding
  `VendorsController.GetVendorById`'s `statusHistory[]` + new sibling `statusHistoryHasMore`),
  `GetVendorsQueryHandler.cs` (paged/searched/filtered list; `IReadRepository` has no async count,
  so total count uses `GetItems<T>(filter).Count()` synchronously against the same Mongo LINQ
  provider `GetItemsAsync` uses for the page)
- `src/Read/EventWorker/Program.cs` — `EventConsumerAdapter<T>` for `VendorRegisteredEvent`,
  `VendorBlacklistedEvent`, `VendorReinstatedEvent`, `VendorPerformanceRatedEvent`, all on queue
  `Bits.Procurement.EventWorkerHost`. `VendorCommandRejectedEvent` is deliberately NOT registered
  here.
- `src/Application/CommandWorker/Program.cs` — `CommandConsumerAdapter<T>` for
  `RegisterVendorCommand`, `BlacklistVendorCommand`, `ReinstateVendorCommand`,
  `RateVendorPerformanceCommand`, all on queue `Bits.Procurement.CommandWorkerHost`
- `src/Shared/Common/Constants/NotificationKeys.cs` — added `VendorCommandRejected` (HO-VM-001) as
  the `Topic` for `VendorCommandRejectedEvent`; `VendorBlacklisted` remains unused, as directed

## Decisions made
- 2026-08-19 — New vendors start `Active`; no approval step exists in this SRS slice.
- 2026-08-19 — Trade licence uniqueness is checked in `VendorCommandService`, not the aggregate
  (cross-entity lookup).
- 2026-08-19 — `EventWorker` requires an explicit `CustomQueueConfig(queueName: ...)`. Without it
  no queue binds to the event exchange and published events are silently dropped.
- 2026-08-19 (HO-VM-000) — The command path is now queued: `VendorsController` sends
  `RegisterVendorCommand` via `IBusMessageDispatcher.SendAsync`; `CommandWorker` consumes it and
  executes `RegisterVendorCommandHandler`, which is registered only there, not in the Api
  container. `POST /api/vendors` returns `202 Accepted` with a server-minted `VendorId` and
  `CorrelationId`; the aggregate id is no longer returned from an in-process handler result.
  Shape validation (`IValidationService.ValidateAsync`) still runs explicitly in the controller
  before the send, so `400` on bad input is unchanged; business rule violations (duplicate trade
  licence) now surface only in CommandWorker's logs and the notification path, not in the HTTP
  response. See ADR-004.
- 2026-08-19 (HO-VM-000), **corrected 2026-08-21** — CommandWorker registers its
  `DevUserContextProvider` after `AddServiceBusProvider` while EventWorker registers it before.
  **Both are correct and neither is a bug.** `AddServiceBusProvider`'s `RegisterServices` registers
  its default only when one is not already present —
  `if (services.All(x => x.ServiceType != typeof(IUserContextProvider)))`, a hand-rolled `TryAdd`.
  Registering Dev's first makes the platform skip its own; registering it last makes Dev's win on
  last-registration-wins. Either ordering resolves `DevUserContextProvider`.

  The original entry claimed the platform registered unconditionally and that EventWorker's ordering
  therefore let the default silently win, leaving its provider inert. That was wrong. The evidence
  behind it — "the assembly contains no `TryAdd`" — is true but does not support the conclusion,
  because the guard is written with `All()`. Runtime settles it: EventWorker's projections land in
  the correct tenant database, which cannot happen with a `Guid.Empty` context.
- 2026-08-19 (HO-VM-000) — `VendorRegisteredEvent` implements `INotifiedByEventHandler`
  (`Platform.Infrastructure.Core.Notification`) so a success notification can be delivered once
  EventWorker projects the event, keyed `NotificationKeys.VendorRegistered`. The **violation**
  half (duplicate trade licence) has no equivalent mechanism today — `RegisterVendorCommandHandler`'s
  failure path raises no domain event, only a `CommandResponse` — and remains open; see "Remaining
  work".

- 2026-08-20 (HO-VM-001) — Blacklist/Reinstate/RatePerformance follow ADR-004/ADR-005 exactly:
  controller sends via `IBusMessageDispatcher.SendAsync` after explicit `IValidationService`
  shape validation, handler pre-checks `VendorStatus` and publishes `VendorCommandRejectedEvent`
  on every failure path via `IBusMessageDispatcher.PublishAsync`, success path
  `UpdateAsync(aggregate, EventPublishOption.QueuePublishOnly)`. Verified end-to-end against a
  local RabbitMQ/Mongo (see "Verified end-to-end" below) — this closes decision D-VM-01 from
  HO-VM-000 for the three new commands. `RegisterVendorCommandHandler`'s own violation path
  (duplicate trade licence) was left unchanged per §14 (forbidden to modify Register) and still
  has no rejection event; see "Remaining work".
- 2026-08-20 (HO-VM-001) — `VendorRatingDto` carries `RatingId` even though HO-VM-001 §11's model
  list omitted it: the API contract mints the rating id in the controller (exactly like `VendorId`
  for registration), so it must round-trip through the DTO into the aggregate and onto
  `VendorPerformanceRatedEvent` for the read projection to expose the same id the client already
  has. Verified: the id returned by `POST .../ratings` is the same `ratingId` `GET .../ratings`
  returns.
- 2026-08-20 (HO-VM-001) — `VendorCommandRejectedEvent.ViolationMessage` (not `Message`) carries
  the violation text: `INotificationMessage` (via `INotifiedByEventHandler`) already declares a
  `Message` property of type `IEnumerable<KeyValuePair<string, object>>`, so ADR-005's sample
  `string Message` field would collide with it.
- 2026-08-20 (HO-VM-001) — `VendorAggregate.Blacklist`/`Reinstate` each guard their own precondition
  by throwing `InvalidOperationException`, but the command handlers pre-check `VendorStatus`
  before calling them and never rely on catching that exception - the throw exists purely as the
  aggregate's own invariant (domain tests call it directly), mirroring
  `PurchaseRequisitionAggregate.Submit`'s "caller guards" comment rather than contradicting it.
- 2026-08-20 (HO-VM-001) — No index was added for the vendor list's name/trade-licence search
  (§10 asked for one "if one does not already exist" and to document it in code). No index-
  creation mechanism exists anywhere in this codebase yet (no `Indexes.CreateOneAsync` call, no
  host-level bootstrap step) - inventing one here would be a new, undocumented pattern outside
  this handover's scope. Left as remaining work.
- 2026-08-20 (HO-VM-003) — `VendorAggregate.StatusHistory`/`PerformanceRatings` and
  `VendorViewModel`'s matching embedded lists are gone (SECURITY M2 on HO-VM-001: ~16,000 ratings
  hits Mongo's 16 MB document limit). Ratings and status changes are now one document per entry in
  `VendorRatingViewModel`/`VendorStatusChangeViewModel`, their own collections keyed by `VendorId`,
  `Id` = the raising event's own `RatingId`/`ChangeId`. The aggregate keeps `RatingCount` +
  `CompositeScoreSum` (+ `LatestCompositeScore`) instead of the rating list; `RatePerformance` is
  O(1). See ADR-006 for the full decision record, including why `RatingCount` is tracked
  independently on each side of the queue (write-side aggregate authoritative) and why
  `GET /api/vendors/{id}` still returns `statusHistory[]` (§20.3 option (a) - contract preserved,
  no frontend change, but the array itself stays unbounded in that one payload).
- 2026-08-20 (HO-VM-003) — Redelivery idempotency for the two new event handlers is a single
  `ExistAsync` check against the child collection by the event's own id, done before either the
  child insert or the parent update - simpler than the HO-VM-001 list-membership scan (`21eb0ee`)
  because the child document's `Id` now gives redelivery a stable identity to check against. The
  parent `VendorViewModel` scalar update keeps the HO-VM-001 version-checked CAS unchanged.
- 2026-08-20 (HO-VM-003) — Existing vendor documents in local dev Mongo still carry the old
  embedded `statusHistory`/`performanceRatings` arrays; nothing reads them after this change and no
  migration was run (§20.1 - no migration mechanism exists, dev data only, no evidence of real data
  in this environment). Their read-side effect is that any vendor rated/blacklisted before this
  change shows `ratingCount`/`statusHistory` as empty until rated/blacklisted again, since the
  historical entries never got backfilled into the new collections.
- 2026-08-20 (HO-VM-003 §11.4, fixed after the ARCHITECTURE gate on `e998868`) — the three
  projection handlers (`VendorPerformanceRatedEventHandler`, `VendorBlacklistedEventHandler`,
  `VendorReinstatedEventHandler`) had a real regression: the child-collection existence check keyed
  redelivery idempotency, but it gated the parent `VendorViewModel` CAS behind it in control flow. A
  child insert that succeeded followed by a parent CAS failure (any Mongo error, or the `throw`
  after 5 version-conflict retries) meant the parent scalars were **never applied by any future
  delivery** - the guard saw the child, logged "already projected", and returned. For blacklist that
  was permanent and business-visible (status-change document says blacklisted,
  `GET /api/vendors/{id}` keeps returning `Active`). Fixed by making every parent scalar update a
  pure assignment derived from the event (added `VendorPerformanceRatedEvent.RatingCount`, sourced
  from the aggregate's authoritative running total, so `VendorViewModel.Map` assigns instead of
  incrementing) and reordering each handler: apply the parent CAS unconditionally first, then insert
  the child if absent. The `ExistAsync` guard on the child insert is kept (belt-and-braces given the
  child's `_id` uniqueness) but no longer gates correctness of the parent write. ADR-006 amended
  (2026-08-20) to document the corrected pattern; `VendorRatingViewModel.FromEvent`/
  `VendorStatusChangeViewModel.FromEvent` static factories converted to instance `Map(...)` methods
  to match `VendorViewModel`'s existing convention (ARCHITECTURE LOW). Events already persisted
  before this fix have no `RatingCount` and deserialize it as `0` - accepted per §20.1 (no replay
  mechanism, dev data disposable), not migrated.

- 2026-08-20 (HO-VM-003 SECURITY gate: H1/M2/L1) — Three findings from the gate on the H2 fix
  (`a8a07de`), fixed in the same handover slot:
  - **H1 (unbounded `statusHistory[]`, amplification + permanent 500)**: legal blacklist/reinstate
    alternation could grow `VendorStatusChangeViewModels` for one vendor without limit (~1.1 KB/row,
    `Reason` capped at 1000 chars but that is a maximum, not a deterrent - ~7,200 rows/hour of
    scripted alternation ≈ 8 MB), and `GetVendorStatusHistoryQueryHandler`'s unindexed
    `OrderBy(ChangedOn)` had no bound - past Mongo's 32 MB in-memory-sort cap (~29,000 rows at this
    row size) the query throws and `GetVendorById` returns a permanent 500 for that vendor, with no
    exposed endpoint able to shrink the collection back under the cap. Fixed: bounded to the newest
    50 (`OrderByDescending(ChangedOn)`, `take: MaxItems + 1` in one query to also answer `HasMore`
    without a second round trip or an unindexed count). `GetVendorStatusHistoryQuery`'s `Result` type
    changed from `List<VendorStatusChangeViewModel>` to `VendorStatusHistoryResult(Items, HasMore)`.
    `GET /api/vendors/{id}` gains a new sibling field, `statusHistoryHasMore` - `statusHistory[]`
    itself is unchanged in shape, just bounded to 50 and now newest-first (was oldest-first before
    this fix; the Angular client already re-sorts client-side, so this reversal is safe, but is
    noted here since it is an observable ordering change on an existing field). DI registration in
    `Program.cs` updated to the new `IQueryHandlerAsync<GetVendorStatusHistoryQuery,
    VendorStatusHistoryResult>` signature.
  - **M2 (aggregate double-counts on command redelivery)**: `RateVendorPerformanceCommand` travels
    over RabbitMQ with a retry policy and had no idempotency check; a redelivery after a lost ack
    re-ran `RatePerformance` with the same `RatingId`, permanently inflating `RatingCount` and
    `CompositeScoreSum` (and therefore `AverageCompositeScore`, a vendor-selection input) with no
    evidence left behind - a regression in detectability from the old `PerformanceRatings.Add`,
    which at least left two visibly identical, findable list entries. Fixed per the handover's
    preferred option (a): `VendorAggregate` now keeps `RecentRatingIds` (`List<Guid>`), a bounded
    ring of the last 50 applied `RatingId`s (`MaxTrackedRatingIds = 50` - redelivery from a lost ack
    happens within the broker's retry window, seconds, not thousands of ratings later, so 50 is
    ample headroom without tracking meaningfully more history). `RatePerformance` no-ops (no
    mutation, no event, no `UpdateVersion()`) if the `RatingId` is already in the ring - a second
    line of defense. `RateVendorPerformanceCommandHandler` checks
    `aggregate.HasAlreadyAppliedRating(command.RatingId)` **before** calling `RatePerformance` or
    `UpdateAsync`, so a duplicate delivery persists nothing at all and returns
    `CommandResponse { IsSuccess = true }` without publishing `VendorCommandRejectedEvent` - this is
    a duplicate delivery, not a business rule violation.
  - **L1 (`bankAccountInfo` returned unmasked)**: `GET /api/vendors/{id}` returned the stored value
    verbatim while the list endpoint correctly omitted it entirely. Fixed with
    `VendorsController.MaskBankAccountInfo` - four bullet characters plus the last 4 characters of
    the stored value (e.g. `"••••2233"`), full-mask for values of 4 characters or fewer, null/empty
    passed through unchanged. The stored value itself is untouched; only the detail response is
    masked. No new role-gated endpoint - explicitly out of scope per the finding (no auth layer
    exists to gate on).
  - Regression tests added: `GetVendorStatusHistoryQueryHandlerTests` (60 entries → 50 + `HasMore`;
    10 entries → 10, no `HasMore`; query shape asserts `OrderByDescending`/`skip=0`/`take=51`),
    `VendorAggregateTests.RatePerformance_SameRatingIdTwice_...`/`HasAlreadyAppliedRating_...`/
    `RecentRatingIds_DoesNotGrowBeyondBound_...`,
    `RateVendorPerformanceCommandHandlerTests.HandleAsync_SameCommandTwice_...`,
    `VendorsControllerBankAccountMaskingTests` (reflection against the private static helper - no
    controller instantiation needed). `Bits.Procurement.Application.Tests` now references
    `Bits.Procurement.Api` for the last of these.

- 2026-08-20 (HO-VM-004) — `RegisterVendorCommandHandler` now injects `IBusMessageDispatcher` and
  publishes `VendorCommandRejectedEvent` (`ActionName = "Register"`,
  `ViolationCode = DuplicateVendor`) on the duplicate-trade-licence path, mirroring
  `BlacklistVendorCommandHandler` exactly. This closes the gap ADR-005 itself named first (its own
  example list leads with "duplicate trade licence") and that HO-VM-001 correctly left alone under
  its own §14 fence at the time. The returned `CommandResponse { IsSuccess = false }` and
  `BuildDuplicateVendorResponse` are unchanged — the event is additional, not a replacement — and
  the success path is untouched. No DI change was needed in `CommandWorker/Program.cs`:
  `IBusMessageDispatcher` is already registered there via `AddServiceBusProvider`, the same
  registration `BlacklistVendorCommandHandler` already relies on. All four vendor commands now
  comply with ADR-005.
- 2026-08-20 (HO-VM-004) — `VendorAggregate.RecentRatingIds` is now `IReadOnlyList<Guid>` over a
  private backing field (`_recentRatingIds`) carrying its own `[BsonElement("RecentRatingIds")]` so
  the element name on the wire is unchanged and the ring still round-trips through the same BSON
  shape it always did. `RatePerformance` mutates `_recentRatingIds` directly (the only place that
  may). Verified with an actual `BsonExtensionMethods.ToBson`/`BsonSerializer.Deserialize`
  round-trip in `VendorAggregateTests` (not just that the property compiles) — contents and order
  survive intact. Closes the same "`private set` on a `List<T>` protects only the reference" hole
  ADR-007 already names and HO-VM-003 removed twice elsewhere.
- 2026-08-20 (HO-VM-004) — `VendorsControllerBankAccountMaskingTests` (reflection against the
  private static `MaskBankAccountInfo`) is deleted. `VendorsControllerTests`'s
  `GetVendorById_MasksBankAccountInfo_InTheResponseBody` now covers the same behaviour through the
  real controller response, which also catches the failure mode reflection missed (a deleted call
  site passing the old test while `bankAccountInfo` goes out unmasked). The `ProjectReference` to
  `Bits.Procurement.Api` in `Bits.Procurement.Application.Tests` is kept — it supports this
  legitimate controller test. A dedicated Api test project remains open, see "Remaining work".

## Verified end-to-end (2026-08-21, HO-VM-005)
Ran `Bits.Procurement.Api` and `EventWorker` with `ASPNETCORE_ENVIRONMENT=Development`; ran
`CommandWorker` with `ASPNETCORE_ENVIRONMENT` unset (see the §11.2 finding above - it does not start
in Development once composed from `AddAggregateRootRepository()`), against local RabbitMQ/Mongo:
- I1/I2 - all three hosts started with no other startup exception; both queues bound with an active
  consumer (`Bits.Procurement.CommandWorkerHost`, `Bits.Procurement.EventWorkerHost`, confirmed via
  the RabbitMQ management API).
- I3 - `POST /api/vendors` → `202`, correlation id `25b4867f-8f73-4c69-b6cd-2ef7b61d40fd` traced
  through all three hosts: Api logged the send, CommandWorker's `RegisterVendorCommandHandler` logged
  "Registering vendor..." then "...registered successfully" and published `VendorRegisteredEvent`,
  EventWorker logged "Projecting VendorRegisteredEvent..." then "...projected to read store" for the
  same id.
- I4 - `GET /api/vendors/{id}` returned `200` with the projected vendor once EventWorker's log showed
  the projection complete (did not separately capture the pre-projection `404` this run, but the
  vendor did not exist before the `POST` and returned populated data with `status: "Active"`
  immediately after, consistent with I4).
- I5 - a second `POST /api/vendors` with the same trade licence number → `202`;
  `RegisterVendorCommandHandler` logged `Duplicate trade license number 'TL-HOVM005-001'` (correlation
  id `531defba-9997-491f-b01d-ba78717e77d5`) and published `VendorCommandRejectedEvent` -
  `IBusMessageDispatcher` resolves correctly in the worker.
- I6 - `POST /api/vendors/{id}/blacklist` on the registered vendor → `202`, correlation id
  `f0bf2bbf-4ff1-4de4-9b49-395f8bcc965e`; EventWorker projected `VendorBlacklistedEvent` to both the
  parent `VendorViewModel` and the `VendorStatusChangeViewModel` child collection; `GET .../{id}`
  confirmed `status: "Blacklisted"` with a populated `statusHistory[]` entry. This is the direct proof
  that `IAggregateRootRepository<VendorAggregate>` resolves and executes correctly as a Singleton at
  runtime for a full write path (Blacklist, not just Register) - the only failure mode found anywhere
  in this handover is the Development-only scope-validation check at container `Build()` time, not the
  repository's actual behaviour.
- Two `CommandWorker` processes were briefly running simultaneously mid-session (one from an earlier
  aborted Development-mode attempt whose shell wrapper timed out without the process itself exiting);
  confirmed via `Get-Process`/`Get-CimInstance` and RabbitMQ's `consumers: 2` on the command queue,
  then stopped the stale one before continuing - I6's result is unaffected since both were the same
  build. All hosts (Api, CommandWorker, EventWorker) confirmed stopped at the end of the session via
  `Get-Process`.
- `674469f`'s duplicated `BusConfig` in both `appsettings.Development.json` files: **still required**.
  Confirmed by running - both `CommandWorker/appsettings.Development.json` and
  `EventWorker/appsettings.Development.json` still each carry their own `BusConfig` section (not
  removed by this handover, since `WorkerHostBuilder` loads one file only and does not layer, exactly
  as `.ai/host-composition.md` documents), and the Development-mode Api/EventWorker runs above loaded
  it successfully from that file.

## Verified end-to-end (2026-08-20, HO-VM-003 §11.4 fix)
Ran `Bits.Procurement.Api`, `CommandWorker` and `EventWorker` against local RabbitMQ/Mongo (both
reachable at session start):
- Registered a vendor, posted 3 ratings → `GET /api/vendors/{id}` showed `ratingCount: 3`,
  `latestCompositeScore`/`averageCompositeScore: 4.0`; `GET .../ratings` returned all 3, newest
  first. Confirms `RatingCount` assignment from the event (not increment) is correct in a live run.
- Blacklisted then reinstated the same vendor → `GET /api/vendors/{id}` ended `status: "Active"`
  with `blacklistReason`/`blacklistedOn` cleared, and `statusHistory` held both entries in order
  (Active→Blacklisted, Blacklisted→Active).
- EventWorker's log confirmed the corrected order for all three handlers: the parent
  `VendorViewModel` CAS log line ("rating rollup (N) projected" / "blacklist status projected" /
  "reinstatement status projected") always preceded the child-document log line ("rating {id}
  projected" / "blacklist change {id} projected" / "reinstatement change {id} projected") for every
  event, matching §11.4's required ordering.
- Did not fabricate a genuine Mongo/broker failure mid-write to observe live redelivery-after-
  partial-failure (would require fault injection this environment doesn't support); that path is
  covered by the unit regression tests
  (`HandleAsync_ParentUpdateFailsAfterChildAlreadyInserted_RedeliveryStillAppliesParent*`), which
  drive the handler through exactly that sequence with mocked `ExistAsync`/`GetItemAsync`/
  `UpdateAsync`.
- Stopped all three hosts (Api, CommandWorker, EventWorker) after verification.

## Verified end-to-end (2026-08-20, HO-VM-001)
Ran `Bits.Procurement.Api`, `CommandWorker` and `EventWorker` against a local RabbitMQ/Mongo
(both reachable at session start):
- `POST .../blacklist` on an Active vendor → `202`; `GET .../{id}` reflected `Blacklisted` with a
  populated `statusHistory` once the projection landed.
- Repeating the same `POST .../blacklist` → still `202`; CommandWorker logged and published
  `VendorCommandRejectedEvent` with `ViolationCode = InvalidStatusTransition` and the request's
  `CorrelationId`; the vendor's status/history was unchanged (still one entry) and EventWorker
  never attempted to project the rejection event (confirmed via its log).
- `POST .../blacklist` against an unknown id → `202`; `VendorCommandRejectedEvent` with
  `ViolationCode = VendorNotFound` published; nothing persisted.
- `POST .../blacklist` with an empty `reason` → `400` synchronously
  (`{"errors":[{"field":"Reason","message":"Reason is required.","code":"NotEmptyValidator"}]}`),
  confirmed via CommandWorker's log that nothing reached the queue for that request.
- `POST .../reinstate` on the blacklisted vendor → `202`; `GET .../{id}` showed `Active`, cleared
  `blacklistReason`/`blacklistedOn`, and two ordered `statusHistory` entries.
- `POST .../ratings` with valid scores → `202` with a controller-minted rating id;
  `GET .../ratings` returned that same rating first; `GET .../{id}` showed
  `latestCompositeScore: 4.25` (scores 4/5/4/4) and `ratingCount: 1`.
- `GET /api/vendors?search=` matched on both name and trade licence number (case-insensitive,
  contains).
- `GET /api/vendors` returned vendors registered before this change with
  `latestCompositeScore: null` (never `0`), confirming the projection tolerates the new fields
  being absent on old documents.

## Remaining work
- Confirm `NotificationHandlerMiddleware` (`Platform.Infrastructure.Host.Worker`) actually delivers
  the success notification in a running environment with a notification sink configured — the
  worker logs showed no send failure, but no observable notification transport was available to
  confirm delivery end-to-end. Same caveat applies to `VendorCommandRejectedEvent`'s delivery.
- Vendor document management (FR-VM-003) — explicitly out of scope, needs its own handover once
  file storage and accepted-type/size decisions land.
- No Mongo index exists yet for the vendor list's name/trade-licence search - see the decision
  above.
- RBAC/authorization enforcement for these actions — no authorization layer exists yet.
- (HO-VM-003, §20.2) No Mongo index exists on `VendorRatingViewModels.VendorId` or
  `VendorStatusChangeViewModels.VendorId` either. Both collections are now queried by `VendorId` on
  every ratings/status-history request, and this handover makes them grow without bound by design -
  without an index that is a collection scan at scale. Same root cause as the vendor-list index gap
  above. **Addressed 2026-08-21 by `scripts/create-mongo-indexes.js`** — a mongosh script, run
  manually, creating `{VendorId:1, RatedOn:-1}` on `VendorRatingViewModels`,
  `{VendorId:1, ChangedOn:-1}` on `VendorStatusChangeViewModels`, and `CreatedDate`/status/type
  indexes on `VendorViewModels`. Idempotent and safe to re-run; it reports rather than drops when an
  existing index conflicts. The status-history index is the one that matters most: without it Mongo
  does a blocking in-memory sort capped at 32 MB, and past ~29,000 status changes for one vendor
  `GET /api/vendors/{id}` returns 500 permanently.

  Still open: the `?search=` filter cannot be indexed at all. It is
  `v.Name.ToLower().Contains(search)` — `ToLower()` on the *field* and an unanchored substring —
  so no B-tree index can serve it and adding one would look reassuring while changing nothing.
  Fixing it needs a code change (a pre-lowercased search field, or a text index with the query
  handler rewritten off LINQ `Contains`), which is a decision, not a script.
- (HO-VM-003, §20.3 - **superseded 2026-08-20 by the SECURITY gate H1 fix**) `GET /api/vendors/{id}`
  no longer returns an unbounded `statusHistory[]` array: it is now bounded to the newest 50 with a
  sibling `statusHistoryHasMore` flag (see the H1/M2/L1 decision entry above). A dedicated paginated
  `GET /api/vendors/{id}/status-history` (§20.3 option (b)) is still not built - if a vendor
  accumulates more than 50 status changes there is currently no way to see older ones through this
  API - but the amplification/permanent-500 defect that made bounding this urgent is fixed.
- (HO-VM-003, §20.1) No migration ran for the old embedded arrays already in local dev Mongo - see
  the decision entry above. If this codebase is ever pointed at real vendor data, migrating existing
  `statusHistory`/`performanceRatings` arrays into the new collections is separate, deliberate work.
- (HO-VM-004, §20.2) `VendorsControllerTests`/`VendorsControllerBankAccountMaskingTests` (now
  deleted) live in `Bits.Procurement.Application.Tests`, which references the composition root
  (`Bits.Procurement.Api`) to instantiate the controller. The ARCHITECTURE gate would prefer
  controller tests live in their own `Bits.Procurement.Api.Tests` project instead. Not done here
  (HO-VM-004 §20.2 explicitly excludes creating a fourth test project) - still open.

- 2026-08-21 (HO-VM-005) — Composed all three hosts from the platform's extension methods
  (`AddInMemoryBusServices()`, `AddAggregateRootRepository()`, narrower `AddReadRepository()` for
  EventWorker) per ADR-008/`.ai/host-composition.md`, removed the `Platform.Infrastructure.Host.Worker`
  package reference from both worker `.csproj` files (never a dependency of a bus worker), and
  aligned both workers' environment guard from `DOTNET_ENVIRONMENT ?? ASPNETCORE_ENVIRONMENT` to
  `ASPNETCORE_ENVIRONMENT` only, matching what `WorkerHostBuilder` itself reads. Build clean,
  126/126 backend tests green, no test file touched.
  - **The Singleton question (§11.2) did NOT resolve the way the handover expected.** Running
    `CommandWorker` with `ASPNETCORE_ENVIRONMENT=Development` throws at startup:
    `Cannot consume scoped service 'Platform.Infrastructure.Core.IDispatcher' from singleton
    'Platform.Infrastructure.Core.Domain.IAggregateRootRepository\`1[VendorAggregate]'` - i.e.
    `AggregateRootRepository<T>`'s own constructor takes the Scoped `IDispatcher`, and .NET's
    `ServiceProvider` validates that at `Build()` whenever something in the container actually closes
    the open generic with a concrete `T` (which `CommandWorker` does - every vendor command handler
    and `VendorCommandService` requests `IAggregateRootRepository<VendorAggregate>`). Root-caused by
    experiment, not assumption: the same build with `ASPNETCORE_ENVIRONMENT` **unset** starts cleanly
    (`Hosting environment: (null)`) - .NET's Generic Host only turns on `ValidateScopes`/
    `ValidateOnBuild` when the host environment name is `Development`, exactly the same rule
    `WebApplication` uses. `.ai/host-composition.md`'s claim that "`HostBuilder` does not validate
    scopes" is therefore wrong for `WorkerHostBuilder` - it does, under the identical condition as
    the web host, once the environment name is `Development`. `Bits.Procurement.Api` itself does
    **not** hit this: nothing in the Api container closes `IAggregateRootRepository<VendorAggregate>`
    (command handlers are registered only in `CommandWorker` per ADR-004), so the open-generic
    descriptor is never validated there and the Api starts fine in Development with the same
    `AddAggregateRootRepository()` call. Per §20.1, this was reported rather than worked around -
    `CommandWorker/Program.cs` and its `.csproj` are left exactly as this handover's steps 1-5
    specify (no manual Scoped registration was reinstated), and `ASPNETCORE_ENVIRONMENT=Development`
    was left unset only for the duration of the §17 integration run so all four §17 cases could be
    exercised against the real Singleton registration - see "Verified end-to-end (2026-08-21,
    HO-VM-005)" below. **RESOLVED 2026-08-21 by project decision: this project does not use DI scope
    validation.** The platform's lifetimes are taken as they come — `AddAggregateRootRepository()`
    stays Singleton, and `IAggregateRootRepository<>` is **not** re-registered Scoped to satisfy a
    validator. `CommandWorker/Program.cs` therefore stands exactly as HO-VM-005 composed it, with no
    manual registration reinstated, and HO-VM-005 §20.1 is closed. If a host is ever run with
    `ASPNETCORE_ENVIRONMENT=Development` and trips the validator, the answer is
    `UseDefaultServiceProvider(o => { o.ValidateScopes = false; o.ValidateOnBuild = false; })`, not a
    lifetime change. Recorded in ADR-008 and `.ai/host-composition.md`.
