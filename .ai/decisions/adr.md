# Architecture Decision Records (ADR)

A running log of key architecture and design decisions made in this project.
Never delete entries — append with date if a decision was revised.

> **2026-09-19 — architecture change.** The project moved from distributed CQRS (RabbitMQ +
> MongoDB + three hosts) to **in-memory CQRS with DDD in a single web host, on PostgreSQL/EF
> Core**. The `Platform.Infrastructure.*` libraries, the layer structure, and the
> Command/CommandHandler and Query/QueryHandler contracts are all **retained** — what changed is
> the transport, the hosting and the datastore. ADR-001 to ADR-008 are superseded and marked in
> place, per the never-delete rule above. Their replacements are **ADR-010 to ADR-017** at the end
> of this file. ADR-009 (authorization at the API boundary) survives unchanged.

---

## ADR-001 — CQRS Physical Separation
**Date:** <!-- Add date -->
**Status:** ⛔ **SUPERSEDED** by ADR-010, ADR-012 (2026-09-19)
**Superseded because:** CQRS is retained, but as an in-memory, code-level separation inside one host. The physical Write/Read split into separately deployed projects with separate databases is gone.
**Decision:** Write and Read sides are physically separated into different projects/layers. Commands go through CommandHandlers and AggregateRoots. Queries go directly through QueryHandlers to the read database.
**Reason:** Enforce strict separation of concerns, allow independent scaling of read/write paths.

---

## ADR-002 — MongoDB as Primary Database
**Date:** <!-- Add date -->
**Status:** ⛔ **SUPERSEDED** by ADR-011 (2026-09-19)
**Superseded because:** MongoDB is replaced by PostgreSQL accessed through EF Core.
**Decision:** MongoDB (Driver 2.x) is used for both read and write stores. `ITransactionalRepository` for writes, `IReadRepository` for reads.
**Reason:** Document model flexibility for evolving bounded contexts.

---

## ADR-003 — Event-Driven via MassTransit + RabbitMQ
**Date:** <!-- Add date -->
**Status:** ⛔ **SUPERSEDED** by ADR-010, ADR-013 (2026-09-19)
**Superseded because:** There is no MassTransit and no RabbitMQ. Domain events dispatch in-process; integration events dispatch in-process after commit; anything leaving the process goes through a transactional outbox.
**Decision:** All cross-aggregate and cross-service communication is done via domain events published through MassTransit over RabbitMQ.
**Reason:** Decouples bounded contexts; enables saga orchestration for complex workflows.

---

<!-- Add project-specific ADRs below -->

## ADR-004 — Commands Are Dispatched to the Bus from Controllers
**Date:** 2026-08-19
**Status:** ⛔ **SUPERSEDED** by ADR-013, ADR-014 (2026-09-19)
**Superseded because:** Controllers dispatch through the platform's `IInMemoryDispatcher` in memory and await the real result. There is no bus, no queue, no `SendAsync`/`PublishAsync` distinction and no `202 Accepted`.
**Decision:** Controllers inject `IBusMessageDispatcher` via constructor DI and **send** the Command
to RabbitMQ with `SendAsync`. Commands are sent, events are published: a Command has
exactly one handler and routes point-to-point to an endpoint queue, whereas an Event fans out to an
exchange for N subscribers. `IBusMessageDispatcher` (`Platform.Infrastructure.Core.Bus`, concrete
`BusMessageDispatcher`) exposes exactly `SendAsync<TMessage>(TMessage)` and
`PublishAsync<TMessage>(TMessage)`. Note the similarly named `SendBusMessageAsync` /
`PublishBusMessageAsync` live on `IDispatcher`, not on `IBusMessageDispatcher` — do not confuse
them. The Command Handler executes in CommandWorker, which
consumes the Command via `CommandConsumerAdapter<TCommand>`. Controllers never resolve or invoke
`ICommandHandlerAsync<TCommand>` directly, and command handlers are registered in the CommandWorker
container — not the Api container.
**Reason:** Completes the asynchronous CQRS loop (Api → queue → CommandWorker → domain → event
queue → EventWorker → projection → notification). Keeps the write path independently scalable and
consistent with the already-queued event path.
**Consequences:**
- The HTTP response acknowledges acceptance, not completion — `202 Accepted` with the correlation
  id, not `201 Created` with the entity id.
- Business rule violations cannot be returned in the response body; they surface asynchronously via
  the notification path. Only shape validation fails synchronously at the controller.
- Read-after-write is eventually consistent on both hops, not one.
**Known deviation (2026-08-19), resolved 2026-08-19 (HO-VM-000):** the Vendor Management slice
previously bypassed this — the controller used `IDispatcher.SendAsync`, the handler ran
in-process inside the Api, and `CommandWorker/Program.cs` was an empty stub consuming nothing.
HO-VM-000 closed this: `VendorsController` now sends `RegisterVendorCommand` via
`IBusMessageDispatcher.SendAsync`, `CommandWorker` is a real consumer host
(`CommandConsumerAdapter<RegisterVendorCommand>` on queue `Bits.Procurement.CommandWorkerHost`),
and `RegisterVendorCommandHandler`/`IVendorCommandService` are registered only in CommandWorker,
not the Api container. Verified end-to-end against a local RabbitMQ/Mongo: `202 Accepted` with a
server-minted id and correlation id, the same correlation id observed in CommandWorker's log, the
command still queued and processed after a worker restart (I6), and a duplicate trade licence
logged as a violation in the worker rather than returned synchronously. See
`.ai/flows/_archive-procurement/vendor-management-flow.md` for what remains open (violation-path notification).

---

## ADR-005 — Business Rule Violations Surface as Rejection Events
**Date:** 2026-08-20
**Status:** ⛔ **SUPERSEDED** by ADR-014 (2026-09-19)
**Superseded because:** Business rule violations are returned in `CommandResponse.ValidationResult` and become the HTTP response body. Rejection events existed only because a `202` had already been returned; that constraint is gone.
**Context:** ADR-004 made the command path asynchronous, so `POST` returns `202 Accepted` before the
handler runs. A business rule violation discovered inside CommandWorker — duplicate trade licence,
vendor not found, invalid status transition — has no response body left to travel in. HO-VM-000
delivered the success-path notification but returned BLOCKED on this half (decision D-VM-01),
because both platform notification interfaces (`INotifiedByEventHandler`,
`INotifiedByAggregateRoot`) need an event or aggregate to attach to, and the failure path raises
neither — it returns only a `CommandResponse` carrying a `ValidationResponse`.

**Decision:** A command handler that rejects a command **publishes a rejection event** describing
the violation, using `IBusMessageDispatcher.PublishAsync` (events are published; commands are sent —
ADR-004). The rejection event implements `INotifiedByEventHandler`, so it reaches the client through
exactly the same declarative notification mechanism as the success path.

Shape, one per aggregate context:

```csharp
namespace Bits.Procurement.Domain.Events.ErrorEvents;

public sealed class VendorCommandRejectedEvent : Event, INotifiedByEventHandler
{
    public Guid VendorId { get; init; }
    public string ActionName { get; init; }      // "Register", "Blacklist", "Reinstate", "RatePerformance"
    public string ViolationCode { get; init; }   // from BusinessViolationCodes
    public string ViolationMessage { get; init; }
    public string? PropertyName { get; init; }
}
```

**The violation text field must NOT be called `Message`.** `INotifiedByEventHandler` inherits
`INotificationMessage.Message`, which is `IEnumerable<KeyValuePair<string, object>>`; a
`string Message` collides with it and will not compile. Use `ViolationMessage`.

**Namespace:** `Domain.Events.ErrorEvents`, per `.ai/namespace-conventions.md`, which maps
Error/Violation Events there (flat) rather than to a per-feature folder.

**Not `BusinessRuleViolatedEvent`.** `Platform.Infrastructure.Core.Events.BusinessRuleViolatedEvent`
is real — unlike the phantom types in `.ai/templates/` — and carries its own Topic/UserIds/Messages.
We do not extend it, because a rejection here must implement `INotifiedByEventHandler` to reach the
declarative notification path ADR-004 established for the success case, and using one mechanism for
both halves is worth more than reusing a base class. Revisit if the platform's own violation path
ever gains a notification transport.

The handler returns its `CommandResponse { IsSuccess = false }` as before — publishing the rejection
event is additional, not a replacement. Nothing about the write path or the aggregate changes.

**Why not the alternatives:**
- *Raise the rejection from the aggregate* — impossible for `VendorNotFound`, where no aggregate was
  loaded, and wrong for uniqueness checks, which are cross-entity and belong to the CommandService.
- *Persist rejections and let the client poll* — invents a store and a contract the platform already
  provides through the notification subsystem.
- *Keep violations in the HTTP response* — contradicts ADR-004 outright.

**Consequences:**
- A rejection event is **not projected**. It carries no state a read model needs; it exists to carry
  a notification. Do not write a `VendorViewModel` projection for it.
- Rejection events are published, so they fan out. Any future subscriber sees them — acceptable, and
  useful for audit later.
- Shape validation still fails synchronously at the controller with `400`, unchanged. Only
  *business rule* violations take this path.
- The client must correlate on `CorrelationId`, which the `202` body already returns.

**Known limitation:** `NotificationHandlerMiddleware` delivery has never been observed end to end —
no notification sink exists in the solution. This ADR settles *how a violation is expressed*, not
that it is delivered. Delivery remains unproven for both the success and rejection paths, and is
recorded as remaining work in `.ai/flows/_archive-procurement/vendor-management-flow.md`.

**Amendment (2026-08-20, HO-VM-004) — Register now complies.** `RegisterVendorCommandHandler` did
not inject `IBusMessageDispatcher` and returned only `CommandResponse { IsSuccess = false }` on a
duplicate trade licence — the exact gap this ADR's example list named first but which HO-VM-001 §14
had fenced off at the time. It now publishes `VendorCommandRejectedEvent` on that path too
(`ActionName = "Register"`, `ViolationCode = DuplicateVendor`), mirroring
`BlacklistVendorCommandHandler`. All four vendor commands now comply with this ADR. The success path
and `CommandResponse` shape are unchanged; the event is additional, not a replacement.

---

## ADR-006 — Ratings and Status History Are One-Document-Per-Entry Child Collections
**Date:** 2026-08-20
**Status:** ⛔ **SUPERSEDED** by ADR-011 (2026-09-19)
**Superseded because:** The unbounded-embedded-array problem is a document-database concern. The surviving rule — an aggregate must not load an unbounded collection — is recorded in ADR-011 and `.ai/persistence-ef-core.md` §2.
**Context:** `VendorAggregate.StatusHistory`/`PerformanceRatings` and the mirrored
`VendorViewModel.StatusHistory`/`PerformanceRatings` were unbounded embedded lists. Ratings are
append-only by design (HO-VM-001 §4 excludes edit/delete) and each carries a 1000-character
comment, so roughly 16,000 ratings reaches Mongo's 16 MB BSON document limit — after which every
subsequent write *and* every projection update for that vendor fails permanently, with no exposed
endpoint able to shrink the document. `RatePerformance` also recomputed
`PerformanceRatings.Average(...)` over the whole list on every write (write cost linear, total cost
quadratic). `StatusHistory` has the same defect at much lower velocity (SECURITY M2 on HO-VM-001).

**Decision:** Unbounded embedded arrays are replaced by one-document-per-entry child collections —
`VendorRatingViewModel` (`VendorRatingViewModels`) and `VendorStatusChangeViewModel`
(`VendorStatusChangeViewModels`), both keyed by `VendorId` and both extending `ViewModelBase` with
`[BsonIgnoreExtraElements]`. `VendorAggregate` keeps running totals instead of full history:
`RatingCount`, `CompositeScoreSum` and `LatestCompositeScore`. `RatePerformance` is O(1) —
increment, add to sum, raise the event with `AverageCompositeScore = CompositeScoreSum /
RatingCount` (BR-VM-08) — because the event stream already *is* the history; the aggregate does not
need a second copy of it. `Blacklist`/`Reinstate` stop appending to a list for the same reason —
the event they raise already carries `ChangeId`, `PreviousStatus`, `VendorStatus`, `Reason`,
`EffectiveDate` and who/when.

Each child document's `Id` is the event's own id (`RatingId` for ratings, `ChangeId` for status
changes), not a separately generated key. That gives redelivery idempotency a stable identity to
check against — a redelivered event always targets the same document. The parent `VendorViewModel`
document's scalar fields (`RatingCount`/`LatestCompositeScore`/`AverageCompositeScore`/
`VendorStatus`/`BlacklistReason`/`BlacklistedOn`) keep the version-checked CAS from HO-VM-001
(CODE_REVIEW Warning 1) — that remains a read-modify-write and the child inserts do not.

**Read-side `RatingCount` is a derived projection, not a re-count.** `VendorAggregate.RatingCount`
(write side) is authoritative — it is the value the business decision (the mean on the raised event)
was computed from. The projection's count is assigned from the event, not computed by querying
`VendorRatingViewModels`, and is expected to converge with the write side via the same event
delivery guarantee everything else in this projection relies on.

---

**Amendment (2026-08-20, after the ARCHITECTURE gate on `e998868`) — projection ordering and
idempotency corrected.**

The original text above prescribed: insert the child first, guarded by `ExistAsync` on the child;
only then apply the parent CAS, effectively gated behind that same guard by control flow. It also
described `VendorViewModel.Map` as *incrementing* `RatingCount` by 1 per event. Both are wrong and
together they produce a permanent, business-visible bug:

1. `RatingCount += 1` is not idempotent. Reapplying the same event twice inflates the count, so the
   *only* thing preventing that was the child-insert guard returning early before the parent update
   ever ran a second time.
2. That made the parent CAS **depend on never running twice for the same event** — but the child
   insert and the parent CAS are two separate, non-atomic Mongo writes. If the child insert succeeds
   and the parent CAS then fails (any Mongo error, or the `throw` after 5 version-conflict retries),
   the message is redelivered, the guard sees the child already exists, logs "already projected",
   and returns *without ever applying the parent scalars*. For blacklist this is permanent: the
   `VendorStatusChangeViewModel` says the vendor was blacklisted while `GET /api/vendors/{id}` keeps
   returning `Active`, and no future redelivery of that event can fix it, because the guard forecloses
   redelivery from ever reaching the parent write again.

**Corrected decision:** every parent scalar update this projection performs must be a **pure
assignment derived from the event**, never an accumulation, so that reapplying an event any number
of times converges to the same state instead of compounding. Concretely:

- `VendorBlacklistedEvent`/`VendorReinstatedEvent` already assigned `VendorStatus`/
  `BlacklistReason`/`BlacklistedOn` — nothing about those events or that assignment changes.
- `VendorPerformanceRatedEvent` gained a new field, `RatingCount` — the aggregate's authoritative
  running total at the point the rating was recorded (§20.4 of HO-VM-003 already declared the
  aggregate authoritative; this makes that fact reach the read side). `VendorViewModel.Map` now
  assigns `RatingCount = @event.RatingCount` instead of incrementing.

With every parent field now an assignment, the version-checked CAS is safe to repeat under
redelivery and no longer needs to be gated behind anything. **Handler order is now**: apply the
parent CAS unconditionally first, then insert the child if absent. The `ExistAsync` check on the
child is kept — the gate confirmed it is benign belt-and-braces given the child's `_id` uniqueness
underneath — but it no longer gates the parent write and is not what correctness depends on.
Ordering no longer matters for correctness (a crash between the two steps converges on redelivery
either way); parent-then-child is chosen so the parent is never in a worse state than "reflects this
event or an earlier retry of it" for any observable interval.

**Consequence for events already persisted:** they predate the `RatingCount` field and will
deserialize it as `0` (the CLR default for `int`). There is no replay mechanism in this solution and
dev data is disposable (§20.1 of HO-VM-003), so this is accepted rather than migrated — but it means
any pre-existing `VendorPerformanceRatedEvent` message still in flight or replayed from a dead-letter
queue would zero out `RatingCount` on the parent document if redelivered. Not a concern for this
codebase's current state; would need addressing before this pattern is trusted with real data and a
message store with actual replay.

**`GET /api/vendors/{id}` keeps returning `statusHistory[]` (§20.3 option (a)).** The endpoint's
contract does not change: `VendorsController.GetVendorById` now issues a second query
(`GetVendorStatusHistoryQuery`) against `VendorStatusChangeViewModels` and reassembles the same
array shape it always returned, so the Angular `status-history.component` needs no change. This
knowingly puts an unbounded array back into that one response payload — trading the document-size
defect this ADR fixes for a same-shaped-but-now-separately-queried payload-size cost on a single
endpoint. A future paginated `GET /api/vendors/{id}/status-history` (§20.3 option (b)) would remove
that cost but requires a documented frontend follow-up; not taken here without one.

**Why not the alternatives:**
- *Keep counting from the list on the read side* — defeats the point; the projection would still
  need to load the full history to know its own length.
- *Catch a Mongo duplicate-key exception on the child insert instead of `ExistAsync` first* — the
  handover's stated preference, and structurally sound, but the platform's `IRepository.CreateAsync`
  return type and exception contract are not documented and could not be verified against the
  referenced `Platform.Infrastructure.Repository.MongoDb` build without decompiling it. `ExistAsync`
  before `CreateAsync` is a small, auditable simplification of the same idea (the child's `Id`
  is the redelivery guard) that does not depend on an unverified exception type. Revisit if the
  platform's insert contract is ever confirmed.
- *Migrate existing embedded documents* — out of scope (§20.1); no migration mechanism exists in the
  solution and this is dev data only.

**Consequences:**
- `VendorAggregate` and `VendorViewModel` no longer hold `List<VendorStatusChange>` /
  `List<VendorPerformanceRating>` in any form. `VendorPerformanceRating.cs` and
  `VendorStatusChange.cs` (domain models) are deleted — nothing else referenced them.
- `VendorRatingViewModels`/`VendorStatusChangeViewModels` will be queried by `VendorId` on every
  ratings/status-history request. No index exists for either (no index-creation mechanism anywhere
  in this codebase — HO-VM-001 deviation 6) — this converts the fixed document-size problem into an
  unindexed collection-scan-at-scale problem instead. Recorded as remaining work; not solved here.
- `GET /api/vendors/{id}/ratings` is now paged at the database (`IReadRepository.GetItemsAsync`
  skip/take), not by slicing an in-memory list, and no longer needs to load the parent
  `VendorViewModel` at all to serve a page.

---

**Amendment (2026-08-20, after the SECURITY gate on `a8a07de`) — H1/M2/L1 fixed; §20.3 option (a)
superseded.**

The SECURITY gate found the §20.3 option (a) choice above (`GET /api/vendors/{id}` reassembling the
full, unbounded `statusHistory[]` from its own collection) was itself exploitable, and found a
second, independent gap in `RatePerformance`'s command-redelivery handling that this ADR's own
context section had already named as the motivating defect for M2 on HO-VM-001 but which the
one-document-per-entry refactor did not itself close.

**H1 — unbounded `statusHistory[]`, now bounded.** `POST /{id}/blacklist` and `/reinstate` alternate
legally forever; each write is a full document (~1.1 KB with a maxed-out `Reason`), so ~7,200 rows of
scripted alternation in an hour is ~8 MB, and `GetVendorStatusHistoryQueryHandler`'s
`OrderBy(ChangedOn)` had no supporting index and no take limit - an in-memory sort past Mongo's 32 MB
cap (~29,000 rows at this row size) throws, and `GetVendorById` then returns a permanent 500 for that
vendor with no exposed endpoint able to shrink the collection back under the cap. Fixed: bounded to
the newest 50 (`OrderByDescending(ChangedOn)`, fetching `MaxItems + 1` so a single query also answers
`HasMore`). `GetVendorStatusHistoryQuery`'s result type changed from `List<VendorStatusChangeViewModel>`
to `VendorStatusHistoryResult(Items, HasMore)`; `GET /api/vendors/{id}` gained a sibling
`statusHistoryHasMore` field. `statusHistory[]` itself keeps its element shape but is now newest-first
instead of oldest-first - a deliberate reversal, safe because the Angular client already re-sorts
client-side, but an observable change on an existing field and recorded here for that reason. §20.3
option (a) ("no externally visible change... stops returning `statusHistory[]`... or keeps it
unbounded") is superseded: the endpoint keeps the field but it is no longer unbounded, which neither
original option anticipated.

**M2 — `RatePerformance` was not idempotent on redelivery.** `RateVendorPerformanceCommand` travels
over RabbitMQ with a retry policy; nothing before this fix stopped a redelivery after a lost ack from
re-running `RatePerformance` with the same `RatingId` and permanently inflating `RatingCount`/
`CompositeScoreSum` (and therefore `AverageCompositeScore`, a vendor-selection input) - a regression
in *detectability* specifically, since the old embedded `PerformanceRatings.Add` at least left two
visibly identical, findable entries, while a running total leaves no evidence at all. Fixed with the
handover's preferred option: `VendorAggregate.RecentRatingIds`, a bounded ring (`List<Guid>`,
`MaxTrackedRatingIds = 50`) of the most recently applied `RatingId`s. N = 50 because redelivery from a
lost ack happens within the broker's retry window - seconds, not thousands of ratings later - so 50
is ample headroom against any plausible burst of concurrent ratings for one vendor between a write
and its ack, without recreating the unbounded-history problem this ADR exists to fix.
`RatePerformance` no-ops (no mutation, no event, no `UpdateVersion()`) if the `RatingId` is already
tracked; `RateVendorPerformanceCommandHandler` checks `HasAlreadyAppliedRating` *before* calling
`RatePerformance` or `UpdateAsync`, so a duplicate delivery persists nothing and returns
`CommandResponse { IsSuccess = true }` - not a `VendorCommandRejectedEvent`, because a duplicate
delivery is not a business rule violation.

**L1 — `bankAccountInfo` unmasked in the detail response.** `GET /api/vendors/{id}` returned the
stored value verbatim while the list endpoint correctly omitted it. Fixed by masking to the last 4
characters (`"••••2233"`) in the response only; the stored value is untouched. No new role-gated
endpoint - there is no authorization layer in this codebase to gate on, and building one is a larger,
separate decision.

**Why not the alternatives (M2):** dedupe at the command-handler level against the event store
(option (b) in the finding) was available but rejected in favor of keeping the guard inside the
aggregate, where the invariant (`RatingId` has already been applied) actually lives - consistent with
this ADR's own principle that the aggregate, not the projection, is authoritative for its running
totals.

**Consequences:**
- `VendorAggregate` gains one more piece of bounded state (`RecentRatingIds`, capped at 50 entries)
  beyond `RatingCount`/`CompositeScoreSum`/`LatestCompositeScore` - still O(1)-bounded, not a return
  to the unbounded list this ADR replaced.
- `GetVendorStatusHistoryQuery`'s `Result` type is a breaking change for any caller other than
  `VendorsController` (there are none in this codebase) - `List<VendorStatusChangeViewModel>` became
  `VendorStatusHistoryResult`.
- The Mongo index gap recorded in the original ADR-006 text (`VendorRatingViewModels`/
  `VendorStatusChangeViewModels` queried by `VendorId` with no index) is unchanged by this amendment -
  still open, still not solved here, same reasoning as before (no index-creation mechanism exists in
  this codebase).

---

## ADR-007 — Command Idempotency via a Bounded Applied-Id Ring on the Aggregate
**Date:** 2026-08-20
**Status:** ⛔ **SUPERSEDED** by ADR-014 (2026-09-19)
**Superseded because:** Command idempotency via an applied-id ring existed because a queued command could be redelivered. Synchronous in-process dispatch cannot redeliver. Idempotency is still required at the HTTP edge (SRS FR-ORD-20) but is handled there, not on the aggregate.
**Context:** Commands are delivered over RabbitMQ with a retry policy (ADR-004), so a redelivery
after a lost ack re-executes the handler with the same command. Where a handler mutates *absolute*
state that is fully described by the event, redelivery is harmless — ADR-006 makes the read-side
projections idempotent exactly this way, by assignment.

Accumulating state is different. `VendorAggregate.RatePerformance` keeps running totals
(`RatingCount`, `CompositeScoreSum`) rather than a list, so a redelivered `RateVendorPerformanceCommand`
inflates both permanently, skewing `AverageCompositeScore` — a vendor-selection input. The read side
dedupes by `RatingId`; the write side had no equivalent. Worse, it leaves no evidence: the embedded
list this replaced would at least have shown two identical entries.

**Decision:** An aggregate whose command handler mutates accumulating state keeps a **bounded ring of
recently applied command ids** and no-ops on a repeat. For vendor ratings: `RecentRatingIds`, N = 50,
evict-oldest, exposed as `IReadOnlyList<Guid>` over a private backing field. The handler tests
membership before mutating or persisting and treats a repeat as **success** — a duplicate delivery
is not a business rule violation and must not publish a `VendorCommandRejectedEvent` (ADR-005).

**Why on the aggregate rather than the handler boundary.** The dedup check must be transactional with
the state it protects. The aggregate document is the only thing written atomically here, so moving
the check to the handler just relocates the same race somewhere with weaker guarantees. Persisted
applied-message-id state on an aggregate is a standard event-sourcing idiom.

**Why bounded, and why the bound is correctness.** An unbounded set of applied ids would recreate the
exact defect ADR-006 exists to remove. N = 50 is sized against the broker's retry window: a
redelivery lands within seconds, not thousands of ratings later. **This is not a performance knob.**
If more than 50 distinct ratings for one vendor are ever in flight concurrently, the ring evicts an
id before its redelivery arrives and the guard silently stops working. Raise N if that ever becomes
plausible; do not treat it as tunable-downward.

**Scope of this pattern.** It applies where a command mutates accumulating state. It is *not* needed
where every mutation is an absolute assignment derived from the command — `Blacklist` and `Reinstate`
set status fields outright and are naturally idempotent, so neither carries a ring. Prefer making a
mutation assignment-shaped over adding a ring; reach for the ring only when the state genuinely
accumulates.

**Consequences:**
- New aggregate state exists purely for infrastructure idempotency, not for a business invariant.
  That is accepted deliberately; the alternative is a weaker guarantee.
- The ring is persisted with the aggregate, so it survives restarts — which is the point.
- Aggregates predating this ADR have no ring; their commands remain vulnerable to redelivery
  double-counting wherever they accumulate. `RegisterVendor` is unaffected (it creates rather than
  accumulates). Purchase Requisition has no handlers yet and should adopt this from the start.
- Expose the ring read-only. `private set` on a `List<T>` protects only the reference and would leave
  the collection publicly mutable, breaching the "state changes only inside aggregate mutation
  methods" non-negotiable.

---

## ADR-008 — Hosts Are Composed From Platform Extension Methods
**Date:** 2026-08-21
**Status:** ⛔ **SUPERSEDED** by ADR-010 (2026-09-19)
**Superseded because:** There is one host, composed from per-module registration extensions. The three-host composition rule no longer applies.
**Decision:** All three hosts (Api, CommandWorker, EventWorker) compose their `Program.cs` from the
platform's own extension methods rather than hand-registering the services those methods already
provide: `AddInMemoryBusServices()` (`Platform.Infrastructure.Core.Extensions`) for the in-process
dispatch pipeline (`IResolver`, `IHandlerResolver`, `IDispatcher`, `IValidationService`,
`IValidationProvider`, `ICommandSender`, `IEventPublisher`, `IQueryProcessor` — all Scoped),
`AddAggregateRootRepository()` (`Platform.Infrastructure.Repository.MongoDb`) for
`IAggregateRootRepository<>` and the repository interfaces it chains (all Singleton), and
`AddServiceBusProvider(configuration, ...)` (`Platform.Infrastructure.MassTransit.Bus.RabbitMQ`) for
MassTransit/RabbitMQ wiring and `IBusMessageDispatcher`. Workers use `WorkerHostBuilder` /
`HostBuilderOptions` from `Platform.Infrastructure.MassTransit.Bus.RabbitMQ` — **not** from
`Platform.Infrastructure.Host.Worker`, which is a separate middleware abstraction
(`ProcessingContext`, `IMessageConsumer`, `MiddlewareBase`) and is not a dependency of a bus worker.
Environment selection reads `ASPNETCORE_ENVIRONMENT` only (not `DOTNET_ENVIRONMENT`), and
`appsettings.{env}.json` is loaded as the **only** configuration file — it does not layer over
`appsettings.json`, so every section a host needs (`BusConfig`, `ServiceRegistrationsFilePath`, etc.)
must be duplicated into each environment file. Full detail, the exact registration tables, and the
three host shapes are in `.ai/host-composition.md` — this ADR records the decision to use the
extension methods at all, not the mechanics.

**Reason:** The codebase had hand-rolled roughly thirty individual service registrations across the
three hosts that two or three extension-method calls already provide, and no gate caught it because
`.ai/` had nothing at all to say about `Program.cs` composition — every agent that touched a host
improvised its own shape. This surfaced concretely in commit `674469f`, where CommandWorker failed
at startup with "No BusConfig section is found in your appsettings.{env}.json file" because the
Development environment file was missing a section `AddServiceBusProvider` requires and nothing in
`.ai/` said environment files do not layer.

This ADR also corrects a belief recorded during that same investigation: that
`AddAggregateRootRepository()` could not be used here because a Singleton
`IAggregateRootRepository<>` cannot legally consume the Scoped `IDispatcher` its constructor takes
under ASP.NET Core's default scope validation. That reasoning is wrong — the platform's own
reference host, `ProductExample.CommandWorkerHost`, ships exactly that combination and runs. The
extension method is safe to use as documented; do not reinstate a manual Scoped registration on the
strength of that argument.

**Consequences:**
- New hosts should start from `.ai/templates/command-worker-program.cs`,
  `event-worker-program.cs` and `api-program.cs`, not from a prior host's `Program.cs` copied and
  trimmed — the prior hosts are exactly the improvised, non-canonical shape this ADR replaces.
- Every environment's `appsettings.{env}.json` must independently carry every section a host's
  registered extensions require; there is no shared base file to fall back to.
- `IBusMessageDispatcher`, `IAggregateRootRepository<>` and the eight services
  `AddInMemoryBusServices()` registers must never appear as a hand-written
  `services.AddScoped<...>`/`AddSingleton<...>` line in any `Program.cs` — see the "never
  hand-register" list in `.ai/host-composition.md`.

**Supersedes ADR-002's repository-type claim.** ADR-002 says writes use `ITransactionalRepository`.
That type does not exist in the referenced platform packages and never did; ADR-002 is left
unedited per this log's append-only rule, but the actual write-side type, confirmed against
`AddAggregateRootRepository()` and the Vendor Management slice, is `IAggregateRootRepository<T>`
(`SaveAsync`/`UpdateAsync`/`GetByIdAsync`/`GetByFilterAsync`), backed by `IStateRepository` for
plain state reads. Reads use `IReadRepository`, as ADR-002 already says correctly.

**Scope validation is not used in this project (2026-08-21).** `AddAggregateRootRepository()`
registers `IAggregateRootRepository<>` as Singleton while `AddInMemoryBusServices()` registers the
`IDispatcher` it consumes as Scoped. .NET's Generic Host turns on `ValidateScopes`/`ValidateOnBuild`
whenever the host environment name is `Development` — for `HostBuilder` as well as `WebApplication` —
and a host that closes the open generic then fails at startup with *"Cannot consume scoped service
IDispatcher from singleton IAggregateRootRepository`1"*.

The decision is to take the platform's lifetimes as they are. They work at runtime, and the platform's
own reference hosts ship this exact combination. **Do not re-register `IAggregateRootRepository<>`
Scoped to satisfy the validator** — that is the divergence this ADR exists to remove, and an earlier
version of this codebase used precisely that reasoning to justify hand-rolling the whole container.
If a host must run under a `Development` environment name, disable validation explicitly with
`UseDefaultServiceProvider(o => { o.ValidateScopes = false; o.ValidateOnBuild = false; })`.

**Host responsibility boundary (2026-08-21).** Composition follows from what a host is *for*, and the
repository call is the visible consequence:

| Host | Talks to | Owns | Repository call |
|---|---|---|---|
| Api / web host | the frontend | HTTP, sending commands, reading view models | `AddReadRepository()` |
| CommandWorker | the domain | aggregates, invariants, domain events | `AddAggregateRootRepository()` |
| EventWorker | the read side | events in, view models out | `AddReadRepository()` |

**The Api must not call `AddAggregateRootRepository()`.** It sends commands to the bus and shapes
responses; it never loads, mutates or saves an aggregate. `ProductExample.CommandWebHost` calls no
repository extension at all — our Api needs `AddReadRepository()` only because it also serves
queries, which ProductExample splits into a separate `QueryHost`.

This makes ADR-004's "controllers dispatch and shape responses, nothing more" enforceable by
composition rather than by convention: a host that cannot resolve an aggregate repository cannot
quietly begin writing through one. It also removed the web host's access to the event and state
stores, which it had no reason to hold.

An earlier version of this ADR and of `.ai/host-composition.md` prescribed
`AddAggregateRootRepository()` for every host. That was wrong, and `src/Api/.../Program.cs` carried
the same error until it was corrected alongside this amendment.

---

## ADR-009 — Authorization Is Enforced at the Api Boundary, From a Single Policy Table

**Status**: Accepted (2026-09-07, HO-VM-006)
**Supersedes nothing. Constrains**: ADR-004, ADR-005

### Context

SRS §4.2 restricts vendor actions by role. Four acceptance criteria said so and none was enforced —
no `[Authorize]` anywhere, and the host runs `UseHttpPipeline(enableAuthorization: false)`.

Under ADR-004 a command is *sent to the bus*, and the controller returns `202 Accepted` before any
handler runs. ADR-005 then makes the only failure channel an asynchronous rejection event. So the
placement of an authorization check is not a style question: enforce it only in the handler and the
system necessarily answers "you are not allowed to do this" with `202 Accepted` followed by a
notification, having already put the unauthorized command on the queue.

### Decision

**Authorization is decided at the Api boundary, before dispatch, and denied synchronously with
`403`.** A caller's roles are in hand at request time; nothing about the decision is eventually
consistent, so nothing about it should be asynchronous.

It is enforced by a declarative `IAuthorizationFilter` attribute rather than an `if` in each
action, so `CLAUDE.md`'s "controllers dispatch and shape responses" stays literally true.

The attribute reads `IUserContextProvider`, **not** `ClaimsPrincipal`. ASP.NET's built-in
`[Authorize(Roles = ...)]` resolves roles from a `ClaimsPrincipal` that nothing populates while this
host runs without authentication middleware — it would deny every request in Development. Reading
the platform's user context makes the rule identical whether the context came from
`DevUserContextProvider` or a JWT-backed provider.

**The action-to-role policy lives in exactly one table** (`ProcurementActionRoles` in `Shared`),
which also owns the comparison semantics (ordinal, exact). Both enforcement points resolve it. The
attribute therefore takes an *action*, not a role list — an attribute argument must be a
compile-time constant, so a shared role array cannot be passed directly, and passing the action is
what makes single-sourcing possible at all.

A defence-in-depth guard remains in each command handler, and its value must not be overstated: it
reads `UserContext` off the message, so anyone who can publish to the command queue simply claims
the role. It catches legitimately-produced commands redelivered from a build predating the filter,
and any future in-process dispatcher. It does not make the queue a trusted boundary.

### Consequences

- An authorization failure is the one non-shape failure that is synchronous. ADR-004's "only shape
  validation fails synchronously at the controller" now has this second exception, deliberately.
- Unmapped actions fail closed, which is silent — a test asserts every declared action is mapped.
- Widening a gate requires changing a table pinned by a test, not editing an attribute in passing.
- **Authorization is not authentication.** This makes the rule correct and enforced; it does not
  make identity trustworthy. Until authentication is wired the API is reachable without a
  credential. See HO-VM-006 §20.

  **Corrected 2026-09-08 (SECURITY gate S8 on HO-VM-005):** this consequence originally said the
  CORS policy "reflects any origin with credentials in every environment". That is stale —
  `UseCorsToAllowAll()` sits inside the `IsDevelopment()` block. The warning still holds for the
  authentication work, but it is a Development-only exposure today, not an every-environment one.
  Do not cite the old wording as evidence of a production CORS hole.

### Rejected alternatives

- **Handler-only enforcement** — answers a denial with `202`, per the Context above.
- **Built-in `[Authorize(Roles = ...)]`** — depends on middleware this host does not run.
- **Roles as `Enumeration`** — the platform's `UserContext.Roles` is `string[]` and the spelling is
  owned by an external IdP; no aggregate branches on a role. `const string` in `Shared/Constants`,
  beside `BusinessViolationCodes`.
- **A `SystemAdmin` blanket override** — easy to add later, hard to remove once assumed. Its absence
  is asserted by test.

---

# Current architecture — ADR-010 onward

These replace ADR-001 to ADR-008. All dated 2026-09-19 and decided together; they are separate
records because they can be revisited independently.

---

## ADR-010 — Single Host: CommandWorker and EventWorker Removed
**Date:** 2026-09-19
**Status:** Active
**Decision:** The CommandWorker and EventWorker hosts are removed. The **Api is the single host**
and now runs command handlers, query handlers and domain event handlers in-process. The layer
structure (Api, Application, Domain, Read, Infrastructure, Shared) and the
`Platform.Infrastructure.*` libraries are unchanged.
**Reason:** The four business lines in `docs/SRS.md` share one cat profile and must stay
consistent (vaccination-gated boarding BR-1, prescription-diet gating BR-2, batch recall tracing
FR-IMP-11). One database and one transaction give that for free; three hosts made it a
distributed-consistency problem for a business running from a single premises. One deployable is
also proportionate to the team size.
**Consequences:** Read and write paths cannot scale independently. Accepted — it is visible long
before it hurts, and the layer separation means a worker could be reintroduced later without
restructuring the code. ADR-008's rule that "the Api never touches an aggregate" is superseded:
with no CommandWorker, the Api composes the aggregate repository too.

---

## ADR-011 — PostgreSQL via EF Core as the Only Database
**Date:** 2026-09-19
**Status:** Active
**Decision:** PostgreSQL, accessed through EF Core with the Npgsql provider. One database, one
`DbContext` in `src/Infrastructure/`, optionally grouped into schemas per bounded context for
readability. `Platform.Infrastructure.Repository.MongoDb` is removed, but its
**`IAggregateRootRepository<TAggregate>` contract is kept** and implemented over EF Core, so
command handlers are unchanged.
**Reason:** The domain is highly relational — orders to batches to cats to visits to invoices —
and the SRS is full of invariants that need real transactions and real foreign keys within a
module (stock ledger D-3, FEFO allocation BR-4, invoice immutability BR-11). The previous stack
had no transaction API at all, which forced aggregate boundaries to be drawn around what Mongo
could write atomically rather than around what the business means.
**Consequences:** Multi-statement transactions now exist; the rule remains **one aggregate per
command** regardless. Indexes move into migrations, replacing the manual `mongosh` script. The
ADR-006 concern about unbounded embedded arrays becomes the rule *an aggregate must not load an
unbounded collection* — history tables stay outside the aggregate. `EventPublishOption` loses its
meaning: with no queue, the repository publishes domain events in-process via `IEventPublisher`.

---

## ADR-012 — CQRS Is Retained, In Memory, Against One Database
**Date:** 2026-09-19
**Status:** Active
**Decision:** Commands, `ICommandHandlerAsync<T>`, Queries, `IQueryHandlerAsync<TQ,TR>` and the
`Application` / `Read` project split are all retained. What is removed is the physical
separation: there is no read database, no event-sourced projection, no `IReadRepository` and no
projection event handler. Query handlers read the same tables the write side committed, through
the `DbContext` with `AsNoTracking()`, projecting into a ViewModel with `Select`.
**Reason:** The value of CQRS here is the modelling separation — an aggregate shaped to protect
invariants, a ViewModel shaped for a screen. That is worth keeping. The projection pipeline was
paying for eventual consistency the business never asked for: a receptionist who books an
appointment expects to see it in the list immediately.
**Consequences:** No projection lag, no redelivery convergence problem, no read/write drift. A
read issued after a write sees that write. `src/Read` now references `src/Infrastructure` for the
`DbContext`; the rule that `Application` and `Read` never reference each other is unchanged.

---

## ADR-013 — In-Memory Dispatch via the Platform Bus
**Date:** 2026-09-19
**Status:** Active
**Decision:** Dispatch uses the **platform's own in-memory bus**: `AddInMemoryBusServices()`
(`Platform.Infrastructure.Core.Extensions`), which registers `IResolver`, `IHandlerResolver`,
`IDispatcher`, `ICommandSender`, `IEventPublisher`, `IQueryProcessor`, `IValidationService` and
`IValidationProvider`. `AddServiceBusProvider(...)` is removed. Controllers dispatch through
`IDispatcher`. No MediatR, and no hand-written dispatcher.
**Reason:** `AddInMemoryBusServices()` was **already being called in all three hosts** — it was
simply bypassed on the write path by `AddServiceBusProvider()`, which routed commands to RabbitMQ.
Removing the broker call is therefore the smallest change that achieves in-memory dispatch, and it
keeps every existing contract, template, reviewer checklist and team habit valid. Writing a
dispatcher by hand would have reimplemented a platform capability that already exists.
**Consequences:** Handler registration stays **explicit** — the platform has no assembly scanning,
and that has not changed. What improved is the failure mode: with one container a missing
registration throws at dispatch naming the type, instead of a message vanishing onto an unbound
queue. The exact in-memory member names on `IDispatcher` / `ICommandSender` / `IQueryProcessor`
must be confirmed against the package — see `.ai/architecture.md` §6.

---

## ADR-014 — Commands Are Synchronous and Return Their Real Result
**Date:** 2026-09-19
**Status:** Active
**Decision:** A controller awaits the platform's `IInMemoryDispatcher` and returns `201`/`200` with
the result, or `4xx` carrying `CommandResponse.ValidationResult`. `202 Accepted` is no longer
used. Business rule violations are returned, never published: rejection events,
`INotifiedByEventHandler` violation delivery, and the aggregate-level applied-id idempotency ring
are all removed.
**Reason:** ADR-004, ADR-005 and ADR-007 all existed to work around one constraint — a `202` had
already been sent, so there was no response body left to carry a violation, and a queued command
could be redelivered. In-process dispatch removes the constraint, and with it three pieces of
machinery. It also directly serves SRS NFR-USA-4: an error must say what went wrong and what to
do next, which is far harder through a notification channel.
**Consequences:** Idempotency is still required at the HTTP edge for duplicate submits and
retries (SRS FR-ORD-20), implemented with an idempotency key on the request rather than on the
aggregate. A long-running command now holds the request open — anything genuinely slow belongs in
the outbox, not in the handler.

---

## ADR-015 — Command Handlers Load Aggregates Directly; No CommandService Layer
**Date:** 2026-09-19
**Status:** Active
**Decision:** The `Application/CommandServices` layer is removed. A command handler injects
`IAggregateRootRepository<TAggregate>` and loads, mutates and saves the aggregate itself.
Existence and uniqueness checks are repository calls in the handler. Logic that genuinely spans
aggregates — FEFO allocation, appointment slot availability — becomes a **Domain Service** in
`Domain/DomainServices`, not an application-layer service.
**Reason:** The CommandService layer added a file pair per feature to wrap a repository call, and
it pulled aggregate loading out of the handler — which is where DDD expects it. Worse, it became
the natural home for rules that belong **inside** the aggregate, which is how a domain model turns
anaemic. A service should appear because the domain needs one, not because every feature has one.
**Consequences:** Handlers get shorter and the aggregate gets richer. The namespaces
`Application.CommandServices[.Abstractions]` are removed. Where a check repeats across three
handlers, extract a repository method — not a service layer. A service that would only wrap one
repository call must not be created.

---

## ADR-016 — Optimistic Concurrency via PostgreSQL `xmin`
**Date:** 2026-09-19
**Status:** Active
**Decision:** Every aggregate table maps the PostgreSQL system column `xmin` as a row version
(`builder.Property<uint>("xmin").IsRowVersion()`). A conflicting save raises
`DbUpdateConcurrencyException`, which the host maps to `409 Conflict` with a message telling the
user to reload. Aggregates keep their own `Version` / `UpdateVersion()` for domain-level checks
beneath it.
**Reason:** Two staff editing the same cat, or two receptionists claiming the last appointment
slot, must not silently last-write-wins. The previous architecture had no concurrency guard at
all. `xmin` costs no extra column and no application bookkeeping.
**Consequences:** Clients must handle `409` and reload. Appointment and boarding-unit booking
additionally need a transactional hold (SRS FR-CLN-3, FR-CLN-4), because optimistic concurrency
alone does not prevent two people being offered the same slot — it only prevents both saving it.

---

## ADR-017 — Platform.Infrastructure Libraries Are Retained
**Date:** 2026-09-19 (revised same day)
**Status:** Active
**Decision:** The internal `Platform.Infrastructure.*` libraries **remain the foundation of this
solution**. Retained and used: `Core` (`Command`, `CommandResponse`, `ValidationResponse`,
`Query`, `QueryResponse`, `ICommandHandlerAsync`, `IQueryHandlerAsync`, `IEventHandlerAsync`,
`AggregateRoot`, `Enumeration`, `IDispatcher`, `IEventPublisher`, `AddInMemoryBusServices()`),
`Common`, and `Host.WebApi` (`UseHttpPipeline`). The private NuGet feed is still required.

Two packages are dropped, each for a specific reason:
- `Platform.Infrastructure.MassTransit.Bus.RabbitMQ` — there is no broker (ADR-010, ADR-013).
- `Platform.Infrastructure.Repository.MongoDb` — the datastore is PostgreSQL (ADR-011). Its
  `IAggregateRootRepository<TAggregate>` **contract is kept** and implemented over EF Core, so
  command handlers are unchanged.

**Reason:** The platform libraries provide the CQRS and DDD kernel this project is built on, and
they are datastore-agnostic. An earlier revision of this ADR proposed replacing them with
in-solution building blocks; that was **rejected** — it would have reimplemented working, mandated
infrastructure and invalidated every existing template, reviewer checklist and team habit, for no
benefit.
**Consequences:** `AddInMemoryBusServices()` is the dispatch pipeline and must never be
hand-registered or duplicated. The only bespoke infrastructure this solution owns is the EF Core
implementation of the aggregate repository contract, plus the outbox. Four platform API details
need confirming against the assemblies before the first slice — see `.ai/architecture.md` §6.

---

## ADR-018 — Platform API Verification Results
**Date:** 2026-09-19
**Status:** Active
**Context:** ADR-010 to ADR-017 were written before the `Platform.Infrastructure.*` packages could
be restored, so four API details were recorded as unverified rather than guessed. The solution was
then scaffolded, restored against the private feed, built, and started.

**Findings:**

1. **`IAggregateRootRepository<TAggregate>` is declared in `Platform.Infrastructure.Core`**, not
   only in `Repository.MongoDb`. We therefore implement the **platform interface itself** over EF
   Core. Command handlers are unchanged from the previous architecture — the contract in
   `.ai/command-handler-pattern.md` is exactly right.
2. **`Platform.Infrastructure.Host.WebApi` does not depend on RabbitMQ or MongoDB.** The Api
   resolves only `Core`, `Common`, `CustomException`, `EndpointRoleFeatureMap`, `Host.WebApi` and
   `ServiceRegistration.Provider`. Dropping the bus and Mongo packages costs the host nothing.
3. **`AddInMemoryBusServices()` registers `IDispatcher`, `ICommandSender`, `IQueryProcessor`,
   `IEventPublisher`, `IResolver` and `IHandlerResolver` as Scoped** — the in-memory pipeline is
   real and complete, and needs no broker.
4. **It does not satisfy two of its own dependencies**, and with `ValidateOnBuild = true` the host
   refuses to start until they are registered:
   - `IUserContextProvider` (needed by `CommandSender`) — no implementation ships.
   - `IBusMessageDispatcher` (needed by `EventPublisher`).

**Decision:** The host owns both implementations, in `src/Infrastructure/Hosting/`:
- `DevUserContextProvider` — a development stand-in returning a fixed identity and tenant, to be
  replaced when real authentication lands.
- An **in-process `IBusMessageDispatcher`** that resolves `IEventHandlerAsync<TEvent>` from DI and
  invokes them synchronously.

**Reason:** These are not platform gaps to work around — they are the platform's extension points.

**Consequences:** `ValidateOnBuild = true` must stay on — it is what surfaced both gaps at startup
instead of at first use.

> ### ⚠️ Finding 4 and its Decision are SUPERSEDED by ADR-020 (2026-09-20)
>
> Two statements above were wrong even on 1.0.32, and the rest was overtaken by 1.0.33/1.0.34:
>
> - "no concrete `BusMessageDispatcher` ships in any platform package" — **false.** It ships in
>   `Core` as `BusMessageDispatcher(IBusProvider)`. It is unusable here only because no
>   `IBusProvider` is registered, there being no broker.
> - "the single most important piece of infrastructure this solution owns" — **false, and
>   actively harmful as direction.** `EventPublisher` on 1.0.32 resolved and invoked
>   `IEventHandlerAsync<TEvent>` itself *and then* called `IBusMessageDispatcher`. A host
>   implementation that also resolved handlers fired every domain event **twice**, silently.
>
> On **Core 1.0.34** `AddInMemoryBusServices()` registers no `IEventPublisher` at all, so
> `IBusMessageDispatcher` is not required, not registered, and must not appear anywhere in this
> solution. Only the `IUserContextProvider` half of finding 4 still stands. See **ADR-020**.

---

## ADR-019 — Controllers Inject `IInMemoryDispatcher`, Not `IDispatcher`
**Date:** 2026-09-19
**Status:** Active
**Context:** `Platform.Infrastructure.Core` **1.0.32** adds `IInMemoryDispatcher` and its
implementation `InMemoryDispatcher`, and extends `EventPublishOption` with in-memory members.
`AddInMemoryBusServices()` registers both `IInMemoryDispatcher` and `IDispatcher` as Scoped.

**Decision:** Controllers and any other caller inject **`IInMemoryDispatcher`**. `IDispatcher` is
not used in this solution.

Verified surface (Core 1.0.32):

| Member | `IInMemoryDispatcher` | `IDispatcher` |
|---|---|---|
| `Task<CommandResponse> SendAsync(Command)` | ✅ | ✅ |
| `Task PublishAsync<TEvent>(TEvent)` | ✅ | ✅ |
| `Task<QueryResponse<TResult>> GetResultAsync<TResult>(IQuery<TResult>)` | ✅ | ✅ |
| `QueryResponse<TResult> GetResult<TResult>(IQuery<TResult>)` | ✅ | ✅ |
| `SendBusMessageAsync<TMessage>` | ❌ | ⚠️ present |
| `PublishBusMessageAsync<TMessage>` | ❌ | ⚠️ present |

**Reason:** The two interfaces are identical except that `IDispatcher` can reach the broker. With
no RabbitMQ deployed, a call to `SendBusMessageAsync` would fail at runtime in a way nothing
catches at compile time. Injecting `IInMemoryDispatcher` removes the possibility entirely —
the mistake becomes unrepresentable rather than merely discouraged, which is the whole point of
choosing the narrower interface.

**Consequences:**
- Queries dispatch with **`GetResultAsync<TResult>(query)`**, not `QueryAsync` — an earlier draft
  of the docs invented that name; it does not exist.
- `SendAsync` takes the **`Command` base type**, not a generic parameter; the handler is resolved
  from the runtime type.
- `Query<TResult>` already carries `PageNumber`, `PageSize`, `Skip`, `Take`, `DoCount`,
  `UserContext`, `CorrelationId` and `Vertical`. Queries must not redeclare paging.
- **`EventPublishOption` is not gone** — a claim in an earlier draft that has been corrected
  throughout. It is `DoNotPublish=0`, `InMemoryPublishOnly=1`, `InMemoryAndQueuePublish=2`,
  `QueuePublishOnly=3`, and `SaveAsync`/`UpdateAsync` require it as an argument. Handlers pass
  **`InMemoryPublishOnly`**.
- The review gates treat `IDispatcher`, `SendBusMessageAsync` or `PublishBusMessageAsync` in
  application or Api code as a finding.

---

## ADR-020 — In-Memory Dispatch Is `IInMemoryDispatcher`; There Is No Bus Dispatcher
**Date:** 2026-09-20
**Status:** Active
**Supersedes:** ADR-018 finding 4 and its Decision (the `IBusMessageDispatcher` half only)

### Context

ADR-018 directed the host to implement an in-process `IBusMessageDispatcher` that resolves
`IEventHandlerAsync<TEvent>` from DI, and called it "the single most important piece of
infrastructure this solution owns". That was the wrong direction on two counts, and the packages
have since been changed to remove the constraint that produced it.

`Platform.Infrastructure.*` is maintained in-house. Versions **1.0.33** and **1.0.34** were
released on 2026-09-20 specifically to separate in-memory dispatch from the broker leg.

Verified by reflection and by building a real `ServiceProvider`:

| | 1.0.32 | **1.0.34** |
|---|---|---|
| `IInMemoryDispatcher.PublishAsync<TEvent>` | present | **removed** (1.0.33) |
| `InMemoryDispatcher` ctor | `(ICommandSender, IEventPublisher, IQueryProcessor, IUserContextProvider)` | `(ICommandSender, IQueryProcessor, IUserContextProvider)` |
| `IEventPublisher` in `AddInMemoryBusServices()` | registered → `EventPublisher(IBusMessageDispatcher, IHandlerResolver)` | **not registered** |
| Builds with `ValidateScopes`+`ValidateOnBuild` and no `IBusMessageDispatcher` | ❌ fails | ✅ **builds** |

Two further observations from 1.0.32, recorded because they explain why the old direction was
dangerous rather than merely redundant:

- `EventPublisher.PublishAsync<TEvent>` resolved and invoked `IEventHandlerAsync<TEvent>` **itself**,
  and *then* called `IBusMessageDispatcher.PublishAsync`. A host implementation that also resolved
  handlers therefore fired every domain event **twice**, with no error and no log.
- `PublishAsync<TEvent>` binds `TEvent` from the **static** type of the argument. Publishing from
  `aggregate.Events` (a `ReadOnlyCollection<Event>`) bound `TEvent = Event` and threw
  `No handler found that implements IEventHandlerAsync<Event>`.

`BusMessageDispatcher` **does** ship in `Core`, as `BusMessageDispatcher(IBusProvider)` — contrary
to ADR-018. It is simply unusable here, because no `IBusProvider` is registered and no broker is
deployed.

### Decision

1. **`IInMemoryDispatcher` is the only dispatch path.** It carries commands and queries:
   `SendAsync(Command)`, `GetResultAsync<TResult>(IQuery<TResult>)`, `GetResult<TResult>(...)`.
2. **`IBusMessageDispatcher` must not appear anywhere in this solution** — not injected, not
   implemented, not registered. It is the RabbitMQ leg of a broker that is not deployed. The
   ARCHITECTURE and CODE_REVIEW gates treat any occurrence as a BLOCKER, and that rule is now
   simply correct rather than in tension with `.ai/host-composition.md`.
3. **In-process domain event dispatch is code this solution owns.** `EfAggregateRootRepository<T>`,
   after `SaveChangesAsync`, resolves `IEventHandlerAsync<TEvent>` from DI **on each event's
   runtime type** and invokes them, then calls `ClearEvents()`. There is no `IEventPublisher` to
   delegate to.
4. **Resolve on the runtime type, never the static one.** This is not a style preference; the
   static type is always `Event` and finds nothing.

### Reason

The two interfaces differed in exactly one respect — whether they could reach a broker — and the
architecture depends on that broker being unreachable. Keeping a host-owned `IBusMessageDispatcher`
preserved a type whose only purpose was to reach RabbitMQ, in a solution with no RabbitMQ, while
duplicating handler resolution the platform already did. Removing the dependency in the package was
the correct fix, and it is available because the package is ours.

### Consequences

- `.ai/architecture.md` §6.1 lists **one** host-supplied seam (`IUserContextProvider`), not two.
- `.ai/persistence-ef-core.md` §2 owns the dispatch loop; it is the single place event dispatch
  semantics are defined.
- **A failing event handler propagates out of `SaveAsync`/`UpdateAsync` after the commit has
  already happened.** Same-request, synchronous, exceptions propagate. Decide per handler whether
  that is acceptable and say so in the handler's summary comment.
- `EventPublishOption` is unaffected and still required on every `SaveAsync`/`UpdateAsync`;
  handlers pass `InMemoryPublishOnly`. It is **not** MongoDB residue (ADR-019).
- The minimum supported `Platform.Infrastructure.Core` is now **1.0.34**. On 1.0.32 the host cannot
  start without registering `IBusMessageDispatcher`; do not downgrade.

### Rejected alternatives

- **A no-op `IBusMessageDispatcher` registration** — would have unblocked 1.0.32 immediately, but
  leaves a dead broker type in `Program.cs` that every reader has to be told to ignore, and that
  the gates correctly flag. Fixing the package removes the question.
- **Turning off `ValidateOnBuild`** — hides the missing registration instead of resolving it, and
  with no assembly scanning in this platform, build-time validation is the only thing that catches
  an unregistered handler before a request hits it.

---

## ADR-021 — `SaveChangesAsync` Per Repository Call; Two Corrections to the EF Repository Copied From `.ai/persistence-ef-core.md` §2
**Date:** 2026-09-20 (HO-CAT-001)
**Status:** Active

### Decision

`EfAggregateRootRepository<T>.CommitAsync` calls `SaveChangesAsync()` once per repository call
(`SaveAsync`/`UpdateAsync`), not once per request behind a commit filter —
`.ai/host-composition.md` §4 left this open "for the first slice"; this is that slice, and
per-call is what was built. It is also what makes the audit write in
`Application/DomainEventHandlers/Cat/` commit as its own unit of work, immediately after the
event that triggered it, rather than waiting on a request-scoped filter that does not exist here.

Building and running this slice against a real PostgreSQL end to end (§17, §20.1) surfaced two
defects in the `EfAggregateRootRepository<T>` shape that `.ai/persistence-ef-core.md` §2 says to
"copy exactly." Both reproduced on the very first ordinary write of their kind — not a race, not
an edge case — so every future slice copying that section needs these two corrections too.

**Correction 1 — `UpdateAsync` must not call `.Update()` unconditionally.** Every command handler
in this codebase loads the aggregate through this same repository's `GetByIdAsync` on the same
scoped `DbContext`, mutates it, then calls `UpdateAsync` — so the aggregate is already tracked.
`DbSet<T>.Update(aggregate)` on an already-tracked root walks the whole reachable graph and marks
every not-yet-tracked entity **Modified** rather than **Added** — including an owned-collection
item just added in this request (e.g. `CatAggregate.AddPhoto`'s new `CatPhoto`). EF then emits an
`UPDATE` for a child row that does not exist yet, "0 rows affected", and the `xmin` concurrency
check (ADR-016) throws `DbUpdateConcurrencyException` on a perfectly ordinary first write — not a
conflict at all. Fixed: `UpdateAsync` only calls `.Update()` when
`db.Entry(aggregate).State == EntityState.Detached`; when the aggregate is already tracked (the
common case here), `SaveChangesAsync`'s own change detection already sees every scalar edit and
every Added/Modified/Removed owned-collection item correctly, with no explicit call needed.

**Correction 2 — every `Id` needs `ValueGeneratedNever()`.** `.ai/persistence-ef-core.md` §8 already
says ids are "minted in the controller, as they always were" — but no configuration in that
section or its worked example actually says to disable EF's default value generation for the key.
Left at EF Core 9's default (`ValueGeneratedOnAdd` for a `Guid` key), a **newly added owned-collection
child** with a non-default, application-minted `Id` is misread by EF's Added-vs-Modified heuristic
as an existing row ("the key is set to something, and the database should have generated it, so
this must already be there") and produces the same spurious `UPDATE`-of-a-nonexistent-row failure
as Correction 1 — independently of whether `.Update()` is called. Both were required together;
fixing only one left the bug reproducing. Fixed: `builder.Property(x => x.Id).ValueGeneratedNever()`
on every aggregate root **and every owned entity type** across all four configurations
(`CatConfiguration`, `CatWeightRecordConfiguration`, `VaccineConfiguration`, `AuditEntryConfiguration`).

A third, smaller gap in the same section: `AggregateRoot`/`Entity`'s `Tags` (`string[]`),
`Language` (`string`) and `ServiceId` (`string`) are never set by any aggregate in this slice or by
`SetDefaultValue(UserContext)`, and EF Core 9 treats an unconfigured `string[]` as a **required**
primitive collection by convention (`Language`/`ServiceId` are required strings by the same
convention). Left alone, the very first `SaveChangesAsync` for any aggregate throws
`InvalidOperationException` before a row is ever written. Fixed: all three marked
`.IsRequired(false)` in every configuration, alongside the `Id` fix above.

**Reason these were correctable here rather than a stop-and-report.** All three are
`IEntityTypeConfiguration<T>` details — Infrastructure-owned, inside this slice's allowed scope
(§13) — not a defect in the platform packages themselves, and not a conflict with `docs/SRS.md` or
`docs/USER_STORIES.md` (§20.7). `.ai/persistence-ef-core.md` is authoritative per the handover's
own reading order, but "copy exactly" was written and never run against a real Postgres end to
end; this ADR is that verification, and the two corrections are what running it actually requires.

### Consequences

- No migration change: both corrections are EF metadata, not schema (`ValueGeneratedNever()` does
  not touch a `uuid` column's DDL on PostgreSQL; `IsRequired(false)` changed the *generated*
  migration's column nullability before the first migration was ever applied — confirmed by
  regenerating `AddCatProfile` clean, so no `ALTER COLUMN` was needed either).
- Any future aggregate configuration added to this solution must include
  `builder.Property(x => x.Id).ValueGeneratedNever()` for its root **and every owned entity**, and
  `Tags`/`Language`/`ServiceId` optional, or its first `SaveChangesAsync` will fail exactly this
  way. Worth promoting into `.ai/persistence-ef-core.md` itself in a future documentation pass —
  flagged here rather than edited there, since `.ai/**` is forbidden scope for this handover
  except this file (§13, §14).
- `EfAggregateRootRepository<T>.UpdateAsync(aggregate, expectedVersion, option)` (the
  explicit-version overload) is unaffected — it still delegates to the corrected `UpdateAsync`.

---

## ADR-022 — `Enumeration` Is Defined in `Shared`, Not Supplied by the Platform
**Date:** 2026-09-20 (HO-CAT-001)
**Status:** Active

### Decision

`Enumeration` (`CatEcommerce.Shared.Common.Enums.Enumeration`) is defined in this solution's
`Shared` layer. It ships in **no** `Platform.Infrastructure.*` package on Core 1.0.34 — verified
by building against the restored packages and finding no such type (HO-CAT-001 §7, fact F5).

### Reason

`.ai/enumeration-pattern.md`'s "Base Class" section claims `Enumeration` is "Provided by
`Platform.Infrastructure.Core`". That is incorrect for the packages this solution actually
restores. Rather than depend on a type that does not exist, this slice implements the base class
itself: `Value`, `DisplayName`, `GetAll<T>()`, `FromValue<T>(int)`, `FromDisplayName<T>(string)`,
`Equals`/`GetHashCode`, `CompareTo`, `==`/`!=`, and `ToString() => DisplayName` — the full surface
`.ai/enumeration-pattern.md`'s "Available API" section describes, just owned here instead of by
the platform.

### Consequences

- `.ai/enumeration-pattern.md`'s "Base Class" line is a documentation defect — flagged here rather
  than edited there, since `.ai/**` is forbidden scope for this handover except this file. A future
  documentation pass should correct it to say the base class is solution-owned, in `Shared`.
- Every other bounded context that needs a rich enum (`AppointmentStatus`, `SurgeryStatus`, etc. —
  `.ai/enumeration-pattern.md`'s "Existing Enumerations" table) can now derive from this same
  `CatEcommerce.Shared.Common.Enums.Enumeration`; there is exactly one base class in the solution,
  not one per context.
- Persisted the same way regardless of where the base class lives: by `Value`, via
  `HasConversion`, never by `DisplayName` — unaffected by this ADR.

---

## ADR-023 — Weight History Is a Separate Aggregate; Current Weight Is Derived Read-Side
**Date:** 2026-09-20 (HO-CAT-001)
**Status:** Active

### Decision

`CatWeightRecordAggregate` is its own aggregate root (`clinic.cat_weight_record`, referencing
`CatId` by id), not an owned collection on `CatAggregate`. `RecordCatWeightCommandHandler` loads
`CatAggregate` **only to confirm it exists**, then creates and `SaveAsync`s a
`CatWeightRecordAggregate` directly — the cat itself is not mutated and not saved.
"Current weight" (`CatProfileViewModel.LatestWeightKg`) is resolved **read-side**, as the newest
row in `cat_weight_record` for that cat, in `GetCatProfileQueryHandler`. It is never denormalised
onto `CatAggregate`.

### Reason

`.ai/bounded-contexts.md` rule 3 states an aggregate must not load an unbounded collection, and
names weight history as exactly that: a cat weighed at every visit for eighteen years would force
every unrelated read of `CatAggregate` to drag the whole series. `.ai/persistence-ef-core.md` §4
uses the same example to distinguish owned children (bounded — vaccinations, tens of rows over a
cat's lifetime) from history that must stay outside the aggregate. This also keeps "one aggregate
mutated per command" true for `RecordCatWeight`: it mutates only `CatWeightRecordAggregate`,
never `CatAggregate`.

The obvious alternative — cache `LatestWeightKg` on `CatAggregate` and update it on every
`RecordCatWeight` — was rejected specifically because it is the change most likely to look like a
harmless optimisation later and quietly reintroduce the coupling this ADR exists to prevent. It
would also turn `RecordCatWeight` into a two-aggregate command, which `.ai/bounded-contexts.md`
rule 1 forbids without a documented boundary exception.

### Consequences

- `GetCatProfileQueryHandler` and `GetCatMedicalHistoryQueryHandler` each issue a second query
  against `cat_weight_record` to resolve current/most-recent weight — an accepted read-side cost
  for keeping `CatAggregate` bounded. Both already query through the same `CatEcommerceDbContext`
  in the same request, so this adds one indexed lookup, not a second round trip to a different
  store.
- `cat_weight_record (cat_id, recorded_on)` is indexed for exactly this access pattern
  (`CatWeightRecordConfiguration`, HO-CAT-001 §12.1's index table).
- U32 (HO-CAT-001 §15) is the regression guard: `RecordCatWeightCommandHandler` must save the
  `CatWeightRecordAggregate` and must **not** save `CatAggregate`. A future change that starts
  calling `catRepository.UpdateAsync` from that handler breaks this ADR and should fail that test.

---

> **Recording note (2026-09-25, corrected 2026-09-25 on the second ARCHITECTURE gate pass).**
> ADR-024 through ADR-041 below were specified by
> [`HO-ACC-001`](../../docs/handover/HO-ACC-001.md) §9 ("ADRs to record during this build") to be
> appended "in the implementation commit of the phase that needs each one" (phases A–J,
> 2026-09-22 onward). The implementation commits never did this — the ARCHITECTURE gate on
> `aa53249` found the file still went straight from ADR-023 to ADR-317 — so all eighteen are
> appended together here, in the ARCHITECTURE-gate fix commit, dated to the handover's own date
> (2026-09-22) rather than today, since that is when each decision was actually taken. **Every
> Decision below follows HO-ACC-001 §9's Decision column closely and does not go beyond what §9
> and the sections of the handover it names actually state** — but the wording is paraphrased and
> expanded, not a verbatim copy, and an earlier version of this note incorrectly claimed otherwise
> ("taken verbatim... nothing below is invented"). §9's table gives full decision and reasoning
> text for every one of the eighteen — none is a bare title — so none is flagged as "thin source
> text" below.

## ADR-024 — Accounts and Identity Is the `Account` Bounded Context
**Date:** 2026-09-22 (HO-ACC-001, recorded 2026-09-25 per the note above)
**Status:** Active
**Decision:** Accounts and Identity is the **`Account`** bounded context: folder/namespace segment
`Account`, schema `account`. It owns the seven roots of HO-ACC-001 §8.1 (`CustomerAccountAggregate`,
`CustomerSessionAggregate`, `PasswordResetTokenAggregate`, `CustomerAddressBookAggregate`,
`CustomerNotificationPreferencesAggregate`, `StaffAccountAggregate`, `StaffSessionAggregate`).
Customers and staff are **separate account aggregates**, sharing only their identifier and
credential value objects (`MobileNumber`, `EmailAddress`, `PasswordCredential`, `LoginThrottle`).
**Reason:** `.ai/bounded-contexts.md` and `.ai/namespace-conventions.md` list neither the context
nor the segment (HO-ACC-001 OQ-4) — a gap left for a separate, authorised task. The retired
per-story handovers this slice supersedes named the context three different ways. Without this
entry, the next reviewer has no governing-document basis for the `Account` namespace segment or
for two separate aggregates instead of one `User` with a type column (HO-ACC-001 §8.1 explains the
rejection of the single-table shape: different creators, different contents, different readers,
and FR-ACC-3's "a customer credential never grants a staff session" rule, which two tables make
structural rather than a filter someone can forget).

---

## ADR-025 — Security Ports Live in `Domain.DomainServices`
**Date:** 2026-09-22 (HO-ACC-001)
**Status:** Active
**Decision:** The six security ports (`IPasswordHasher`, `IBreachedPasswordChecker`,
`ISessionTokenIssuer`, `IPasswordResetTokenGenerator`, `IPasswordResetTokenDispatcher`,
`IClientIpAccessor`, HO-ACC-001 §8.10) live in `src/Domain/CatEcommerce.Domain/DomainServices/`,
with their implementations in `Infrastructure` (`Security/` or `Hosting/`, §12.2). Aggregates never
see a plaintext password, token or CAPTCHA answer — handlers call these ports and pass the aggregate
an already-built value object or a boolean outcome.
**Reason:** The project's layering (`Application` references only `Domain` and `Shared`) forces the
ports into a namespace `Application` can reach, and `Domain.DomainServices` is the only such
namespace. But that namespace is documented elsewhere for cross-aggregate domain logic (FEFO
allocation, appointment slot availability), so a reviewer applying that definition literally would
"fix" this by moving the ports into `Infrastructure` — which does not compile from `Application`,
per HO-ACC-001 §8.10's own note. Recording the decision here heads that off.

---

## ADR-026 — Identifier Normalisation and Two-Layer Uniqueness
**Date:** 2026-09-22 (HO-ACC-001)
**Status:** Active
**Decision:** Mobile numbers are normalised to `+8801XXXXXXXXX`; email addresses are lower-cased.
Every account (customer or staff) must carry **at least one** identifier. Each identifier is
**unique per account kind**, and that uniqueness is enforced **twice**: a handler pre-check (for
the friendly, disclosure-controlled message) and a **filtered** unique database index
(`WHERE mobile_number IS NOT NULL` / `WHERE email_address IS NOT NULL`, for correctness under
concurrency). The resulting `23505` is mapped to the same `400` the handler pre-check would have
returned, by `UniqueConstraintExceptionFilter`.
**Reason:** Two-layer enforcement looks redundant on first read and is the kind of thing a later
change "simplifies" down to one layer — but the handler check alone races (two simultaneous
registrations can both pass it), and the index alone cannot produce a friendly disclosure-controlled
message. The **filtered** part of the index (`WHERE ... IS NOT NULL`) is what lets anonymisation
(ADR-041) free a mobile number or email address for a later registrant to use: an anonymised
account's identifier is set to `NULL`, and a filtered index only enforces uniqueness among the rows
it actually covers — those where the column `IS NOT NULL` — so a `NULL`, anonymised row is excluded
from the check entirely and can never block a new registration from taking that value.

---

## ADR-027 — Argon2id Password Hashing With Dummy-Hash Timing Defence
**Date:** 2026-09-22 (HO-ACC-001)
**Status:** Active
**Decision:** Passwords are hashed with **Argon2id** (`Konscious.Security.Cryptography.Argon2`),
parameters `m=19456 KiB, t=2, p=1`, a 16-byte salt and 32-byte output, PHC-encoded. Verification
against an unknown identifier still runs a fixed **dummy hash** so response timing does not reveal
whether the account exists. Breached passwords are checked against a **locally shipped list**;
checking against the HIBP service is deferred behind the same `IBreachedPasswordChecker` port.
**Reason:** NFR-SEC-1 permits either of two named hashing algorithms, and changing the choice later
means rehashing every stored credential — a decision worth pinning down once. The per-story
handovers this slice supersedes disagreed with each other (bcrypt vs Argon2id); this settles it in
the consolidated slice's favour of Argon2id.

---

## ADR-028 — Disclosure Rules: Registration vs Sign-in/Reset
**Date:** 2026-09-22 (HO-ACC-001)
**Status:** Active
**Decision:** **Registration** discloses that the submitted **mobile** (or **email**) is already
registered — US-ACC-01's acceptance criteria require it — and the message for one identifier never
mentions the other. **Sign-in and password-reset-request never disclose** whether an account
exists: sign-in returns one generic `401 invalid_credentials` regardless of cause, and reset-request
always returns the same neutral `200`. Enumeration of registered identifiers through the
registration endpoint is an accepted residual risk, mitigated by CAPTCHA (ADR-029) and per-IP rate
limits (ADR-030).
**Reason:** A security reviewer will flag the registration endpoint as an identifier-enumeration
oracle on sight. Without a decision naming the acceptance criterion that requires the disclosure and
the two mitigations that bound the residual risk, that finding either blocks the slice outright or
gets waved through with no record of why it was accepted.

---

## ADR-029 — Self-Hosted Image CAPTCHA on Registration
**Date:** 2026-09-22 (HO-ACC-001)
**Status:** Active
**Decision:** Registration is protected by a **self-hosted image CAPTCHA**: `POST /api/captcha`
issues a random 5-character code rendered as a noisy PNG (SkiaSharp); the answer is held in a
size-bounded in-process `MemoryCache` with a 120-second TTL and is **single-use** — removed on the
first attempt against it, whether right or wrong. It is checked by an Api action filter
(`CaptchaValidationFilter`) **before** the command handler runs. No vendor, no persisted table, no
per-challenge audit row. `CaptchaController` injects the CAPTCHA services directly — a **recorded
exception** to the rule that a controller injects only the dispatcher and the user context, because
issuing a CAPTCHA challenge is not a domain command.
**Reason:** Owner instruction of 2026-09-22 (no phone/email/OTP verification, so CAPTCHA is one of
the four controls that must carry the resulting security weight, HO-ACC-001 §2). Both the
controller's direct injection and the filter's placement ahead of the handler look like rule
violations against `.ai/command-handler-pattern.md` and the "controllers dispatch only" rule unless
recorded as deliberate here.

---

## ADR-030 — Built-in ASP.NET Core Rate Limiter, Per-IP, Per-Action Policies
**Date:** 2026-09-22 (HO-ACC-001)
**Status:** Active
**Decision:** Rate limiting uses the **built-in ASP.NET Core rate limiter**
(`Microsoft.AspNetCore.RateLimiting`, part of the shared framework — no package reference), fixed
windows partitioned by client IP, `QueueLimit = 0`, rejecting with `429` and `Retry-After`. Five
named policies apply per action, never globally: `captcha-issue`, `registration`, `sign-in`,
`password-reset`, `account-sensitive`. A null remote address partitions as the literal key
`"unknown"` — it never bypasses the limiter.
**Reason:** NFR-SEC-6 names per-IP rate limiting for sign-in and extends it to registration in SRS
v2.1. The account lockout (`LoginThrottle`, HO-ACC-001 §8.2) protects one account against many
attacking IPs; the rate limiter protects many accounts against one attacking IP. Neither control
substitutes for the other, which is why both exist.

---

## ADR-031 — Opaque Session Cookies, Not JWT; Double-Submit CSRF
**Date:** 2026-09-22 (HO-ACC-001)
**Status:** Active
**Decision:** Sessions are **opaque 256-bit tokens**, stored server-side only as SHA-256 hashes,
delivered in `HttpOnly; Secure; SameSite=Lax; Path=/` cookies — `pc_session` for a customer,
`pc_staff_session` for staff — each with an absolute expiry and a copy of the account's security
stamp at issue. **Not JWT.** Unsafe HTTP methods additionally require a **double-submit CSRF
token**: a non-`HttpOnly` `pc_csrf` cookie whose value must be echoed in the `X-CSRF-Token` header.
Explicit sign-out ends the session server-side.
**Reason:** NFR-SEC-4 requires both signed short-lived tokens and CSRF protection. Server-side
sessions make "invalidate every session on password reset" a single security-stamp rotation
(ADR-034); a JWT design cannot revoke an already-issued token without an additional denylist, which
this design avoids entirely. A cookie survives a page reload; the retired proposal of an in-memory
bearer token would sign everyone out on every refresh.

---

## ADR-032 — Caller Resolution Is One Middleware and One Provider
**Date:** 2026-09-22 (HO-ACC-001)
**Status:** Active
**Decision:** Caller resolution has exactly one entry point: `SessionAuthenticationMiddleware`
resolves whichever session cookie is present into a `ResolvedCaller` stored in
`HttpContext.Items`; `SessionUserContextProvider` (Scoped) builds the platform's `UserContext` from
that `ResolvedCaller` — staff: `UserId = StaffAccountId`, `Roles` = the account's role ids;
customer: `UserId = CustomerAccountId`, `Roles = ["Customer"]`; no session: anonymous
(`Guid.Empty`, no roles). `DevUserContextProvider` is kept **only** as the Development fallback when
no session cookie is presented at all, `DevUser:Enabled` is `true`, and the environment is
`Development`; the host refuses to start with it enabled anywhere else.
**Reason:** Every audit writer and every authorization check reads `UserContext`. Resolving it in
exactly one place means they cannot disagree about who the caller is. Keeping the Development
fallback lets HO-CAT-001/HO-CAT-002's already-shipped screens keep working in local development
before browser-side staff sign-in exists.

---

## ADR-033 — Sign-in Touches Two Aggregates (Documented Exception to Rule 1)
**Date:** 2026-09-22 (HO-ACC-001)
**Status:** Active
**Decision:** `SignInCustomerCommandHandler` / `SignInStaffCommandHandler` mutate **two**
aggregates in one handler: the account (`RecordSuccessfulLogin`/`RecordFailedLogin`, resetting or
advancing the login throttle) and — on success — a newly created session aggregate. This is a
**documented exception** to `.ai/bounded-contexts.md` rule 1, "one aggregate mutated per command."
**Reason:** The alternative — issuing the session from a domain event handler reacting to a
successful-sign-in event — splits "the throttle was reset" from "a session was created" across two
separate commits, and would force the session token's plaintext (which must reach the HTTP response
as a cookie) out of the one handler that can safely hold it and into an event payload, which
`Event`'s `UserContext` and audit-capture make an unsafe place for any secret to ride (HO-ACC-001
§11.1's plaintext-secret rule).

---

## ADR-034 — Password Reset Tokens: Stamp-Bound, Single-Use by Rotation
**Date:** 2026-09-22 (HO-ACC-001)
**Status:** Active
**Decision:** Password reset tokens are 256 CSPRNG bits, persisted only as a SHA-256 hash, valid
for 15 minutes, and bound to the account's `SecurityStamp` at issue. A successful password set
**rotates the stamp**, which invalidates every outstanding reset token and every active session for
that account in the same single write — there is no separate `UsedOn`/`Status` flag. The
reset-request endpoint returns **one neutral `200`** regardless of outcome; the confirm endpoint
collapses every possible token failure (unknown, expired, superseded, account gone) into the single
code `InvalidPasswordResetToken`. One mechanism serves customer reset, a staff member's first
password, and a walk-in customer's first password (distinguished only by `AccountKind`).
**Reason:** The neutral response and the collapsed error code both read as bugs to a reviewer who
has not read this decision. Binding tokens to the security stamp rather than a stored `UsedOn` flag
is the design most likely to be "simplified" back into a flag later — recording why it is not one
(a flag cannot also invalidate every session in the same write, and a stored "expired" state can go
stale, HO-ACC-001 §8.5) is the point of this entry.

---

## ADR-035 — Address Book and Notification Preferences: Separate Roots Keyed by Customer Id
**Date:** 2026-09-22 (HO-ACC-001)
**Status:** Active
**Decision:** `CustomerAddressBookAggregate` and `CustomerNotificationPreferencesAggregate` are
**separate aggregate roots**, each taking the **customer id as its own id** (a 1:1 shared key),
created on first write. The address book's default address is a single `Guid?` on the root, not an
`IsDefault` flag on each address. Notification preferences store **only marketing consent cells**;
there is no stored representation of "transactional: off".
**Reason:** Keeping these two collections off `CustomerAccountAggregate` keeps two joins off the
sign-in path, the hottest write path in the context, and both collections have invariants (at most
one default address, at most one consent cell per channel) that need nothing from the account
itself. A single `Guid?` default makes "two defaults" unrepresentable instead of merely disallowed;
storing no "transactional: off" cell makes an always-on transactional message impossible to
suppress by construction, which is what NFR-LEG-4/FR-NTF-4 require.

---

## ADR-036 — Staff Roles Are a Code-Defined Catalogue; Authorization Is a Single Action-to-Role Table
**Date:** 2026-09-22 (HO-ACC-001)
**Status:** Active
**Decision:** Staff roles are a **code-defined catalogue** of the eight staff columns in SRS
Appendix D (`StaffRoles`); the full Appendix D capability matrix is served read-only at
`GET /api/staff-roles`. Authorization on a staff endpoint is declared with
`[RequireAction(action)]`, an `IAsyncAuthorizationFilter` that resolves the action against
**`ActionRoles`, the one action-to-role table for the whole solution** (ordinal, exact-match
lookup). An action absent from the table **fails closed** — denied, not allowed. Every denial
writes one audit entry and returns `403` regardless of whether that audit write itself succeeds.
This applies ADR-009 (authorization decided at the Api boundary, before dispatch) inside the
present, single-host architecture.
**Reason:** A code-defined role catalogue looks like a shortcut against FR-ADM-2 (administrator-
editable permissions) unless it is recorded as a deliberate interim decision. Fail-closed-on-unmapped
and audit-on-denial are both the kind of default that erodes silently if not pinned down here.

**Corrected 2026-09-25 (second ARCHITECTURE gate pass) — two overstatements removed.** This Reason
originally said the catalogue was "the deliberate step that precedes US-ADM-02, which is what makes
permissions editable later" — HO-ACC-001 §1 Amended (2) already records that HO-ADM-001 ADR-044
supersedes that framing: permissions remain code-defined, and no administrator-editable roles
feature is planned. It also cited "the test HO-ACC-001 §12.8 requires, asserting every declared
action is mapped" — §12.8 specifies no such test; none exists in this codebase today (the
ARCHITECTURE gate's own second-pass LOW finding on `ActionRolesAggregationTests` notes this as a
worthwhile guard to add later, not something already required or built).

---

## ADR-037 — Staff Idle Expiry Is Derived and Made Terminal on Detection
**Date:** 2026-09-22 (HO-ACC-001)
**Status:** Active
**Decision:** A staff session's idle state is **derived** from `LastActivityOn` at the moment it is
checked (`SessionStateRules.Classify`), not swept by a background process. Detecting idle expiry
makes it **terminal** (`EndForIdleTimeout`), audited exactly once. Activity writes are throttled to
at most one per 60 seconds, so idle expiry is never detected late, and at most 60 seconds early. A
request refused for a session reason (idle or otherwise) has **executed nothing** and may be safely
replayed once the caller re-authenticates. `GET /api/staff/session` (`[PassiveSession]`) never
extends the idle window.
**Reason:** US-ACC-08 AC2 depends on the "refused request had no side effect" guarantee holding
exactly, and the 60-second activity-write throttle is a deliberate accuracy trade the frontend must
be told about explicitly.

**Corrected 2026-09-25 (second ARCHITECTURE gate pass).** This Reason originally said a session
"could theoretically be treated as active for up to 60 seconds after its last real activity write,
though never idle-expired early" — backwards, and contradicting this ADR's own Decision and
HO-ACC-001 §8.4/§9 ("never late, at most 60 s early"). Because `LastActivityOn` only advances once
per 60 seconds even under continuous activity, the *stored* value can lag up to 60 seconds behind
the account's true last request. Classifying idle state from that lagging value can therefore only
make a session look **more** idle than it truly is, never less — so idle expiry can be detected up
to 60 seconds **earlier** than a perfectly precise clock would call it, and never later, exactly as
the Decision states. Recording "derived, not swept" heads off a later "let's add a background
sweep" change that `.ai/` alone would not forbid (HO-ACC-001 §4.3 explicitly excludes a proactive
sweep — no story requires one).

---

## ADR-038 — Walk-in "Incomplete" Status Is Derived From Password Presence
**Date:** 2026-09-22 (HO-ACC-001)
**Status:** Active
**Decision:** A walk-in is a customer account created with `RegistrationChannel = Reception` and no
password. `CustomerRecordStatus.Incomplete` is defined as exactly `PasswordCredential is null`,
computed by one function (`CustomerRecordStatusRules.Derive`) called identically by the aggregate
and by every read-side query — it is **never persisted**.
**Reason:** Verification no longer exists as a mechanism (the owner's 2026-09-22 instruction
removed it), so there is nothing else "incomplete" could mean. A stored `IsIncomplete`/
`VerificationStatus` flag would go stale on every path that sets a password (self-service set,
reset-driven completion), which is exactly the kind of drift a derived, single-function rule
prevents by construction.

---

## ADR-039 — Merge Is a Single-Aggregate Command; Dependent Data Moves in Event Handlers
**Date:** 2026-09-22 (HO-ACC-001)
**Status:** Active
**Decision:** `MergeCustomerCommand` mutates **only the duplicate** customer account
(`Status = Merged`, `MergedIntoCustomerId` set); the record is retained, never deleted; the survivor
is loaded read-only and never modified. `CustomerMergedEvent` is the **binding integration
contract** every present and future context that holds customer-owned data subscribes to,
idempotently. `ReassignCatsOnCustomerMergedHandler` moves cats one at a time in a domain event
handler, querying for cats still owned by the duplicate, so the operation is **not atomic** — a
part-way failure leaves some cats moved and some not — and **re-sending the identical merge command
is the documented recovery**, not a special-cased retry path.
**Reason:** N+1 separate commits inside what a user perceives as one action is a real, visible
departure from the "one command, one transaction" default elsewhere in this solution, and the
ARCHITECTURE gate must be able to judge it as a considered decision rather than discover it as an
undocumented gap. This also settles, for every later handover that comes to hold customer data
(orders, invoices, appointments — HO-ACC-001 §4.3), that subscribing to `CustomerMergedEvent`
idempotently is the contract, not a bespoke per-context merge hook.

---

## ADR-040 — Audit Integrity: Same-Transaction Capture, Three-Layer Immutability
**Date:** 2026-09-22 (HO-ACC-001)
**Status:** Active
**Decision:** Every aggregate change anywhere in the solution is audited **in the same database
transaction** as the change itself, by `AuditChangeCaptureInterceptor` (an EF Core
`SaveChangesInterceptor`: before/after diff, secrets redacted fail-closed by property-name pattern,
`Category = DataChange`). Authentication events and business events (sign-in, sign-out, merge,
anonymisation, the three shipped Cat audit handlers) keep their existing event-handler writers, now
categorised `AuthenticationEvent`/`BusinessEvent`. Audit entries are immutable at **three
independent layers**: the aggregate (`AuditEntryAggregate.Record` is one-shot, no
`Correct`/`Amend`/`Redact`/`Void` method exists), `AuditEntryImmutabilityInterceptor` (throws before
SQL is sent for any `Modified`/`Deleted` audit entry), and PostgreSQL triggers on
`audit.audit_entry` that `RAISE EXCEPTION` on `UPDATE`, `DELETE` or `TRUNCATE`. The client IP
recorded on every entry is server-derived via `ForwardedHeadersMiddleware` reading only
`X-Forwarded-For` with `ForwardLimit = 1`, and trusted proxies come **only** from configuration,
empty by default.
**Reason:** `.ai/domain-events-and-outbox.md` §3 establishes that a handler-written audit entry
fails *after* the audited change has already committed — exactly what SRS C-9 forbids for an
auditable change. Moving audit capture into the same transaction as the change, via an interceptor
rather than a handler, is what closes that gap, and it changes where audit writes happen for the
**whole solution**, not just for Accounts — worth a decision, not a silent convention. The
three-layer immutability is deliberately redundant: US-ACC-12 AC2's "immutable to anyone" includes
someone connected directly to the database with `psql`, which only the database-level trigger layer
actually stops.

---

## ADR-041 — Account Deletion Is In-Place Anonymisation, With Step-Up Confirmation
**Date:** 2026-09-22 (HO-ACC-001)
**Status:** Active
**Decision:** Self-service account "deletion" never removes the row. `Anonymise` overwrites every
personal field (name → a fixed placeholder, mobile/email/credential → null, additional numbers →
empty, throttle reset), rotates the security stamp, and sets `Status = Anonymised`. The unique
indexes on mobile/email are filtered on not-null (ADR-026), so the freed identifier can be
registered again by someone else. Self-service deletion requires **step-up confirmation with the
caller's current password**, which shares the same lockout as sign-in. **The personal-data export
is `POST`** (it writes an access record — `RecordPersonalDataExport`, an `AuditEntryAggregate`
change like any other) **and returns versioned JSON** (`schemaVersion: 1`).
**Reason:** Clinical and financial records elsewhere in the solution hold the customer account id
by reference (`CatAggregate.OwnerId` today; orders/invoices/appointments later); removing the row
outright would dangle every one of those references. The step-up confirmation, sharing sign-in's
lockout rather than having its own, closes off using repeated deletion attempts as a way to probe a
password without it costing the attacker anything extra.

---

## ADR-317 — Money Is Computed in Integer Minor Units and Stored as `decimal(12,2)`

**Status:** Accepted · 2026-09-22 · resolves README C-8 for every handover

### Decision

Money is **computed** in `long` minor units (paisa) and **stored** as `numeric(12,2)` mapped to
.NET `decimal`, with an explicit currency code alongside every stored amount. Rounding happens once,
at the boundary where a computed figure becomes a stored one, using banker's rounding to two
decimal places. No `float` or `double` touches money anywhere.

### Reason

Three sources disagreed, and two handovers resolved the disagreement independently and identically
before anyone noticed they conflicted:

| Source | Says |
|---|---|
| SRS constraint **C-6** | Money is stored as integer minor units with an explicit currency code |
| **ADR-054** (HO-ADM-001) | `decimal(12,2)` |
| `.ai/persistence-ef-core.md` | `numeric(19,4)` |

C-6's intent is to forbid floating-point money, and that intent is honoured: arithmetic — line
totals, tax, discount apportionment, split invoices — runs in integer minor units, where it cannot
lose a paisa. C-6's *storage* clause is the part not followed literally. `decimal(12,2)` is already
the column type in written migrations, reads naturally in SQL for reporting and reconciliation, and
`numeric` is exact, so storing it this way introduces none of the error C-6 exists to prevent.
`numeric(19,4)` is the platform's generic default and is wrong for a currency with two decimal
places: the extra scale invites amounts that cannot be paid or invoiced.

HO-ORD-001 (its ADR-198) and HO-BIL-001 (its OQ-17) each reached this answer alone. That is the
evidence for it, and this ADR is where it now lives so no later handover has to re-derive it.

### Consequences

- Every handover that handles money follows this without restating it. HO-BIL-001 OQ-17 and
  HO-ORD-001 ADR-198 are closed by reference to this ADR.
- **SRS constraint C-6 still reads "stored as integer minor units" and now differs from the code.**
  That is a spec change, not an architecture one: it needs an owner-approved SRS revision with a
  version bump and a §0 revision-history row. Until then, C-6 and this ADR disagree in writing and
  this ADR is what the gates enforce.
- `.ai/persistence-ef-core.md`'s `numeric(19,4)` default does not apply to money columns.
- A money column typed anything other than `numeric(12,2)`, or arithmetic done in `decimal` where
  minor units would round differently, is a review finding.

---

## ADR-318 — Files Are Stored in Google Cloud Storage Behind an `IFileStore` Port

**Status:** Accepted · 2026-09-23 · resolves README C-5 · owner's choice of provider

### Decision

Uploaded files — product images, clinical photos, lab results, consent forms, boarding belongings and
diary media, generated report exports — are stored as objects in **Google Cloud Storage**, reached
through **one port, `IFileStore`**, implemented by `GcsFileStore` using the official .NET client
**`Google.Cloud.Storage.V1`** (Apache-2.0).

```csharp
public interface IFileStore
{
    Task<FileStoreResult<string>> PutAsync(FilePutRequest request, CancellationToken ct); // → storage key
    Task<FileStoreResult<Stream>> OpenReadAsync(string storageKey, CancellationToken ct);
    Task<FileStoreResult<Unit>>   DeleteAsync(string storageKey, CancellationToken ct);
}
```

Bucket configuration, all of it asserted at startup and by an infrastructure test:

| Setting | Value | Why |
|---|---|---|
| Uniform bucket-level access | **On** | Per-object ACLs cannot be audited as a set |
| Public access prevention | **Enforced** | No object is ever world-readable; NFR-SEC-5 |
| Object versioning | **Off** | A retained old version would survive an erasure (XCT's erasure ledger) |
| Soft-delete retention | **0 / disabled** | New buckets default to a 7-day soft-delete window; a deleted clinical photo must be gone when the retention worker says it is gone |
| Encryption | Google-managed keys | CMEK is a later change behind the same port |
| Lifecycle rules | Only on the `tmp/` prefix (abandoned uploads, 1 day) | Retention is enforced by the retention worker, not by the bucket, because incident-attached media is **held past** its normal retention |
| Credentials | Application Default Credentials — Workload Identity in GCP, no key file in the repository or the image | A service-account JSON key on disk is the thing that leaks |

**Serving stays app-proxied.** Reads keep the existing signed-URL path
(`GET /api/files/{fileId}/content`, HMAC, 5-minute expiry, `nosniff`, sandbox CSP, `no-store`), rather
than handing the browser a GCS V4 signed URL. GCS signed URLs would let bytes bypass the application,
which is cheaper — and also bypasses the one place that sets those response headers, applies
`Content-Disposition` rules per type, and can refuse a file whose record the caller may no longer
read. Clinical media is not worth that trade at this size. Direct V4 signing is a later optimisation,
per purpose, behind its own ADR; the port does not change when it happens.

**Development and Testing keep `LocalFileStore`** (filesystem, outside the content root). No GCS
emulator, no cloud dependency, no credentials to run the test suite.

### Reason

Five handovers — Shop (product images), Consultation (clinical photos, lab results, consent forms),
Boarding (belongings, diary, incident media), Reporting (`bytea` export store) and Cross-cutting
(backup coverage) — each needed file storage and none owned it. HO-EMR-001 §8.10 had already designed
the right shape behind ports, so the shape is kept and only the adapter is decided. The owner chose
GCP, and `Google.Cloud.Storage.V1` is Google's own supported client for it.

Storing files in PostgreSQL (`bytea`) was the alternative, and is what HO-RPT-001 fell back to. It
does not survive the sizes here — a 50 MB DICOM study and a boarding diary of photos would sit in the
same rows the clinic reads all day, and would be copied into every backup and every restore test.

### Consequences

- **HO-FIL-001 / HO-FIL-002 own this**, extracted from HO-EMR-001 §8.10 and moved ahead of Shop in the
  delivery order. HO-EMR-001, HO-SHP-001, HO-BRD-001, HO-RPT-001 and HO-XCT-001 consume it; none of
  them builds an upload path, and HO-EMR-001 ADR-125 is narrowed to its clinical purposes.
- **HO-RPT-001's `bytea` export store is withdrawn** in favour of the same port.
- A GCS outage makes uploads and file reads fail. Records, appointments, dispensing and orders do not
  depend on it, so the clinic keeps working; that is why file rows carry their metadata in PostgreSQL
  and only the bytes live in the bucket.
- Egress is paid on every file read while serving is app-proxied. Reads are behind 5-minute signed
  URLs on staff and owner pages, not a public catalogue CDN, so the volume is small. Product images
  (`Public` purpose, no personal data) are the first candidate for direct signing if it ever matters.
- One bucket per environment, named in configuration. A test run must never see the production bucket:
  startup refuses a `Production` bucket name outside `Production`.

---

## ADR-319 — Uploads Are Scanned by ClamAV Before They Reach the Bucket

**Status:** Accepted · 2026-09-23 · resolves HO-EMR-001 OQ-6

### Decision

Every uploaded file is scanned by **ClamAV** (`clamd`, reached over TCP with the **`nClam`** client)
**before** `IFileStore.PutAsync` is called. Infected content never reaches the bucket.

`IMalwareScanner` keeps the contract HO-EMR-001 §8.10 defined — `Clean` / `Infected` / `Unavailable` —
and gains one real adapter, `ClamAvScanner`. The existing behaviour is unchanged and now enforced
everywhere, not only in the clinic:

| Result | Response |
|---|---|
| `Clean` | The upload proceeds |
| `Infected` | `400 FileRejected`; the temp file is deleted; one audit entry records the signature name; **no content is logged** |
| `Unavailable` | `400 FileScanUnavailable` — **fail closed**. A scanner that is down stops uploads; it never waves them through |

`clamd` runs as its own container beside the API (Compose locally, a sidecar or small deployment in
GCP), with `freshclam` keeping signatures current. Timeout, host and port are configuration.
`DevelopmentNoOpScanner` still throws in its constructor outside `Development`, and the `Testing`
adapter still recognises the EICAR test string, so the fail-closed path is exercised by tests.

### Reason

The alternative was Cloud-native scanning (upload first, scan the object, quarantine after). That
leaves infected bytes in the bucket between the two steps and needs a second bucket and an event
pipeline to move them out. Scanning in the request keeps a single trust boundary: nothing enters the
bucket unscanned.

ClamAV is free, self-hosted, has no per-file cost and no data leaving the environment — which matters,
because the files being scanned are clinical records.

### Consequences

- Upload latency includes the scan. Sizes are capped per purpose (15 MB, 50 MB for DICOM), so this is
  hundreds of milliseconds, and it is on upload only, never on read.
- `clamd` needs roughly 1–2 GB of memory for the signature database. That is a real deployment line
  item, not a free dependency.
- **A scanner outage stops uploads.** That is the intended trade: HO-BRD-001's diary and HO-EMR-001's
  clinical photos are refused with a clear message rather than stored unscanned.
- ClamAV catches known signatures, not a targeted attack. It is one layer; the type allow-list, the
  magic-byte check, the size cap, `nosniff`, the sandbox CSP and `attachment`-only DICOM are the
  others.

---

## ADR-320 — Email Goes Out Over SMTP (MailKit); SMS Goes Through a Config-Driven Gateway Adapter

**Status:** Accepted · 2026-09-23 · partially resolves HO-NTF-001 OQ-1 · **the SMS account is still owner-supplied**

### Decision

HO-NTF-001 built the delivery machinery (outbox, templates, guards, quiet hours, dedup) and left the
transports unimplemented. They are decided as follows:

**Email — SMTP through MailKit.** `IEmailSender` → `SmtpEmailSender` using **MailKit** (MIT), with
host, port, credentials and the from-address in configuration. SMTP is the one interface every
provider offers, so Google Workspace, Amazon SES, Mailgun or a hosted mail server are a configuration
change, not a code change. No provider SDK is taken as a dependency, and no provider is named in the
code.

**SMS — one HTTP adapter driven by configuration.** `ISmsGateway` → `HttpSmsGateway`, which posts to a
configured URL with a configured parameter mapping (recipient field, message field, static credential
fields) and reads success from a configured status path. Bangladeshi aggregators all expose this
shape. Adding a provider is configuration; a provider that cannot be expressed this way gets its own
adapter class behind the same port.

Development and Testing keep `LoggingSmsGateway` / `LoggingEmailSender`, which record the message and
send nothing.

### Reason

The gap was blocking acceptance rows in Notifications, Subscriptions and Reporting (US-RPT-06 delivers
nothing without email). Both halves can be built and tested now without committing to a vendor, so
they are.

Choosing the SMS **aggregator** is a commercial decision — an account, a contract, a sender ID and a
per-message price — and it is not an architectural one. The code does not need it; the business does.

### Consequences

- **Still owner-supplied, and still blocking those acceptance rows in production:** an SMTP account
  and an SMS aggregator account with a registered sender ID. Until they exist, the adapters are
  configured with the logging transports and the affected rows stay unmet, which is what the
  handovers already say.
- Delivery receipts differ per aggregator and are **not** modelled. `NotificationRequest` records
  "handed to the gateway", not "delivered to the handset".
- SMTP credentials and the SMS credential fields are secrets: configuration outside the repository,
  never in the image, never logged. Startup refuses a real transport with a missing credential.

---

## ADR-341 — Per-Area Partial Registries and Module Registration for Parallel Delivery
**Date:** 2026-09-25
**Status:** Active

### Context

Only Cat Profiles (HO-CAT-001) is implemented. The next step runs up to 4 agents in parallel,
each in its own git worktree, implementing different functional areas from
`docs/handover/HO-<AREA>-001.md`. Before this ADR, four files were shared, hand-edited touch
points that every area would have had to modify:

- `src/Infrastructure/CatEcommerce.Infrastructure/Persistence/CatEcommerceDbContext.cs` — every
  area's `DbSet` properties lived on the one sealed class.
- `src/Shared/CatEcommerce.Shared/Common/Constants/BusinessViolationCodes.cs` — every area's
  violation codes lived in the one static class.
- `src/Api/CatEcommerce.Api/Program.cs` — every command handler, validator, query handler,
  domain event handler and infrastructure service for every area was hand-registered directly in
  this file (lines ~98–126 for Cat alone).
- `Shared/Common/Constants/SettingDefinitions.cs` (HO-ADM-001 §10.2) — not yet built, but
  specified as a single static registry that every area with a business setting (Booking,
  Boarding, Inventory, Notifications, Security, Commerce, …) would need to append to.

Two agents editing the same file in parallel worktrees is a merge conflict on every area, even
when their actual features touch nothing in common.

### Decision

**Split every shared, append-only registry into one file per functional area, using C# `partial`
types; keep exactly one call site per area in `Program.cs`, itself delegating to a
one-file-per-area DI module.**

1. **`CatEcommerceDbContext`** is now `partial`. Cat's `DbSet` properties moved out of the base
   file into `CatEcommerceDbContext.Cat.cs`. The base file keeps only the constructor and
   `OnModelCreating`, which already discovers every `IEntityTypeConfiguration<T>` in the
   assembly via `ApplyConfigurationsFromAssembly` — unchanged, so a new area adds its own
   `CatEcommerceDbContext.[Area].cs` (DbSets) plus its own `IEntityTypeConfiguration` classes and
   needs **no** change to the base file or to `OnModelCreating`.
2. **`BusinessViolationCodes`** is now `static partial`. Cat's codes moved to
   `BusinessViolationCodes.Cat.cs`. A new area adds `BusinessViolationCodes.[Area].cs` with its
   own `const string` codes.
3. **`SettingDefinitions`** (HO-ADM-001 §10.2) is created now as an empty `static partial` shell
   at `src/Shared/CatEcommerce.Shared/Common/Constants/SettingDefinitions.cs`, with no settings.
   HO-ADM-001 defines the registry's shape (`SettingDefinition`'s fields) and its own entries in
   `SettingDefinitions.Administration.cs`; every other area with a setting (HO-APT-001,
   HO-BRD-001, HO-IMP-001, HO-NTF-001, HO-ACC-001, per §10.2's Consumer column) adds its own
   `SettingDefinitions.[Area].cs`.
4. **DI registration** moves out of `Program.cs` into one `Add<Area>Module(this IServiceCollection
   services, IConfiguration configuration)` extension per area, in
   `src/Api/CatEcommerce.Api/Modules/<Area>Module.cs`. This layer was chosen — not Application,
   Read or Infrastructure — because the Api project is the only project that already references
   all four layers (Application, Read, Infrastructure, Shared) and is where every registration in
   the pre-existing `Program.cs` already lived; a module in a lower layer could not see the other
   layers' types without adding project references those layers do not otherwise need. All 18
   areas (ACC, CAT, ADM, NTF, APT, FIL, SHP, IMP, EMR, PHM, SRG, BIL, ORD, BRD, SUB, POR, RPT,
   XCT — `docs/handover/README.md`'s delivery order) have a module file; 17 are empty stubs
   returning `services` unchanged; `CatProfilesModule.cs` carries everything Cat Profiles already
   had registered. `Program.cs` calls all 18 exactly once, in one fluent chain, and needs no
   further edit when a stub area is implemented — only that area's own module file changes.
5. **Only the integrator creates EF migrations.** An area PR adds `DbSet`s and
   `IEntityTypeConfiguration` classes; it does not run `dotnet ef migrations add`. The
   integrator, merging multiple areas' schema changes together, runs one migration per merge.
   This is necessary because two areas' partial `DbSet` files both changing the model
   independently would each generate a migration that only sees its own change, and applying
   both out of order against a shared `__EFMigrationsHistory` table is exactly the kind of
   conflict this ADR exists to avoid at the file level.

**Verification.** `dotnet ef migrations has-pending-model-changes` was run before and after this
change and reports no pending changes both times — the EF model is byte-for-byte identical; only
the file each mapping lives in changed. `dotnet build` and `dotnet test` are unchanged (Domain 38,
Application 26 passing; IntegrationTests has no tests yet). The host was also started once
end-to-end with `ValidateScopes`/`ValidateOnBuild` on (ADR-018) to confirm the module chain
resolves the full DI graph with no missing registration.

### Consequences

- A future area's PR touches only its own new files: `CatEcommerceDbContext.[Area].cs`,
  `BusinessViolationCodes.[Area].cs`, optionally `SettingDefinitions.[Area].cs`, its own
  `IEntityTypeConfiguration` classes, and `Modules/[Area]Module.cs` — plus exactly one new line
  in `Program.cs` the *first* time that area is implemented, replacing its stub call with itself
  (the call site does not change name or position, only the module file behind it gains bodies).
- `partial` on a `static` registry class means every partial file must repeat the `static
  partial` modifiers exactly; a mismatched modifier is a compile error, not a silent bug — an
  acceptable trade for the merge-conflict removal.
- The two-agent race on the *same* area is not solved by this ADR and is not in scope — only the
  cross-area race is.
- `Modules/<Area>Module.cs` is the one place in a lower-numbered ADR's "never hand-register" list
  (ADR-013, ADR-018 §5, `.ai/host-composition.md` §5) that still applies unchanged: a module must
  never register anything `AddInMemoryBusServices()` already provides. Moving the call site does
  not change what may be registered there.
- `IConfiguration` is threaded through every module's signature, even the 17 stubs that do not
  use it yet, so a future area needing configuration (SMTP/SMS credentials for HO-NTF-001, GCS
  credentials for HO-FIL-001, business-setting defaults for HO-ADM-001) never has to change the
  signature or the call site in `Program.cs` — only fill in its own stub's body.

### Rejected alternatives

- **Assembly scanning for DI registration** — forbidden outright by ADR-013 and
  `.ai/host-composition.md` §2/§5: the platform has no scanning, and the project's stated failure
  mode (a missing registration throws at dispatch, naming the type) depends on registration
  staying explicit.
- **One `DbContext` per area** — contradicts ADR-011 (one database, one `DbContext`) and would
  reintroduce the multi-store consistency problem ADR-010 removed the three-host architecture to
  avoid; `CatAggregate` alone is read by several areas (`.ai/bounded-contexts.md`), which a
  per-area context cannot express without cross-context joins.
- **Each area creates its own EF migration** — two areas' migrations generated independently
  against the same historical baseline are not composable without the integrator reconciling
  them by hand at merge time regardless; making that reconciliation the integrator's explicit,
  sole job (point 5 above) is simpler than pretending per-area migrations are additive.

---

## ADR-342 — A Module's Pipeline Hook Runs Before `UseHttpPipeline()`, With Its Own Copy of the Platform's Exception Handler
**Date:** 2026-09-25
**Status:** Active
**Amends:** ADR-341 (the `Use<Area>Module` pipeline-hook pattern), HO-ACC-001 §12.9 (the literal
pipeline order)

### Context

HO-ACC-001 §12.9 specifies the pipeline order `UseForwardedHeaders() → (platform pipeline) →
UseRouting() → SessionAuthenticationMiddleware → UseRateLimiter() → MapControllers()`. Following it
literally does not work against this solution's actual platform package. Decompiling
`Platform.Infrastructure.Host.WebApi.Extensions.ApplicationBuilderExtensions.UseHttpPipeline`
(`Platform.Infrastructure.Host.WebApi` 1.0.14) shows it is not a thin wrapper around a single
concern: it calls `UseRouting()`, `GlobalExceptionHandlerMiddleware`, CORS and, unconditionally,
`UseEndpoints(endpoints => { endpoints.MapControllerRoute(...); endpoints.MapHealthChecks("/health"); })`
-- i.e. it performs the actual routing **and** endpoint dispatch itself, inside one call.

`WebApplication` (the minimal-hosting model) backs every `Map...()` call anywhere in `Program.cs`
with the same, single, continuously-updated endpoint data source. Endpoint dispatch -- the step
that actually matches a request to an endpoint and invokes it -- happens at whichever position in
the middleware pipeline first performs it, and it dispatches against the **complete** data source
as it stands once `Build()` has run, regardless of where in the source file each `Map...()` call
appears. Since `UseHttpPipeline()`'s internal `UseEndpoints(...)` is the only such position in this
host's pipeline, it is where every endpoint dispatches -- including attribute-routed controllers
registered later by this file's own outer `app.MapControllers()` call. Concretely, this was
confirmed live: placing `Use<Area>Module()`'s middleware **after** `app.UseHttpPipeline(...)` and
before `app.MapControllers()` (the literal §12.9 order) made `SessionAuthenticationMiddleware` and
`UseRateLimiter()` silently never run for any request that matched a real controller route -- they
ran only for genuinely unmatched (404) requests, because a matched request had already been fully
dispatched and returned by the time control reached that position.

Session resolution and rate limiting therefore have to run **before** `UseHttpPipeline()` is
invoked at all. But `GlobalExceptionHandlerMiddleware` -- the platform's sanitised, no-stack-trace
error handler -- is registered *inside* `UseHttpPipeline()`, so anything positioned before that
call runs **outside** it: an exception thrown in session resolution or in the rate limiter would
reach the caller unhandled instead of through the platform's error shape.

### Decision

A module's pipeline hook (`Use<Area>Module()`, ADR-341) that must run before `UseHttpPipeline()` --
because it needs to see every request, matched or not -- also calls the platform's own, separately
published `UseGlobalExceptionHandler()` extension (same assembly, same
`GlobalExceptionHandlerMiddleware` type `UseHttpPipeline()` uses internally) immediately before its
own middleware. `AccountsModule.UseAccountsModule()` is the first instance:

```
app.UseAccountsModule();          // UseForwardedHeaders -> UseRouting -> UseGlobalExceptionHandler
                                   // -> SessionAuthenticationMiddleware -> UseRateLimiter
app.UseHttpPipeline(enableAuthorization: false);   // UseRouting (again) -> GlobalExceptionHandlerMiddleware
                                   // (again) -> Cors -> UseEndpoints (actual dispatch)
app.MapControllers();
```

Decompiling `GlobalExceptionHandlerMiddleware` confirms running it twice in one pipeline is safe:
it is a plain `try { await next(context); } catch (Exception ex) { ...write the sanitised body... }`
wrapper that never rethrows. The inner (second, `UseHttpPipeline()`-owned) instance's own `catch`
is therefore unreachable in the ordinary case -- the outer (first, module-owned) instance already
handled and terminated the exception -- so the duplication is inert, not a behavioural change to
the platform's own pipeline. Likewise, calling `UseRouting()` twice (once explicitly here, once
again inside `UseHttpPipeline()`) is harmless: both match against the same, already-complete
endpoint data source and produce the same result: the second call simply reconfirms it.

This is recorded as an **amendment to ADR-341**, not a new, freestanding pattern: the module still
owns exactly one pipeline-hook extension method, called once from `Program.cs`, per ADR-341's
`Use<Area>Module()` shape. What changes is only what that one hook must contain when its
middleware needs to run ahead of `UseHttpPipeline()` -- it must also carry its own copy of the
platform's exception handler. Recorded as an Amended row in HO-ACC-001 §1.

### Consequences

- `UseRouting()` running before `UseHttpPipeline()` is also what makes per-endpoint metadata
  (`[EnableRateLimiting(...)]`, `[PassiveSession]`) available to `UseRateLimiter()` and
  `SessionAuthenticationMiddleware`, both of which read `context.GetEndpoint()`. Without an
  explicit early `UseRouting()`, that metadata would not exist yet at the point those middleware
  run, and rate limiting would silently fall back to no per-endpoint policy.
- Any future module whose middleware also needs to see every request (not just matched ones) --
  and therefore also has to run before `UseHttpPipeline()` -- follows the same shape: call
  `UseRouting()` then `UseGlobalExceptionHandler()` before its own middleware. A module whose
  middleware only needs to run for matched, controller-bound requests does not need this and can
  rely on `UseHttpPipeline()`'s own exception handler and dispatch as originally intended.
- This is a statement about how `Platform.Infrastructure.Host.WebApi` 1.0.14 is actually built, not
  a guess from its documentation. If a future package version changes `UseHttpPipeline()` to not
  bundle routing and dispatch together, this amendment should be revisited -- the workaround it
  describes may no longer be necessary.

### Rejected alternatives

- **Follow §12.9 literally** (session/rate-limiting between `UseHttpPipeline()` and
  `MapControllers()`) — confirmed live to silently skip session resolution, CSRF and rate limiting
  for every matched request; the exact defect this ADR exists to avoid reintroducing.
- **Ask the platform team to split `UseHttpPipeline()` into separate routing/exception/CORS/dispatch
  calls** — correct long-term fix, but out of scope for an application-level ARCHITECTURE gate fix;
  the package is versioned and consumed as-is (ADR-017).
- **Re-implement a local exception handler instead of reusing the platform's** — would risk a
  second, differently-shaped error body for the same failure class depending on which part of the
  pipeline caught it; reusing `UseGlobalExceptionHandler()` guarantees one shape everywhere.

---

> ### ⚠️ The Decision, Consequences and "re-implement a local exception handler" rejection above are
> SUPERSEDED by the amendment below (2026-09-25, second ARCHITECTURE gate pass on HO-ACC-001)
>
> The Context above is still accurate. The Decision it led to is not: it introduces a **plaintext
> credential leak**.

**Amendment (2026-09-25, second ARCHITECTURE gate pass) — `UseGlobalExceptionHandler()` is the
platform's own `GlobalExceptionHandlerMiddleware`, which reads and logs the request body.**

Decompiling `GlobalExceptionHandlerMiddleware` (the same type both instances above use) further
than the original Decision did shows `HandleExceptionAsync` calls `GetPayloadIfApplicable`, which
reads `context.Request.Body` to the end and logs it verbatim (`"Payload: {body}"`, Error level) for
any `POST` whose `Content-Type` is exactly `application/json` -- what `web/lib/api/client.ts` sends
on every request. Placed ahead of model binding, as the original Decision placed it, nothing has
consumed the body yet. `SessionAuthenticationMiddleware` dispatches `GetCurrentCustomerSessionQuery`
/ `GetStaffSessionStatusQuery` / `RecordStaffSessionActivityCommand` for any request carrying a
session cookie, and `Platform.Infrastructure.Core`'s `QueryProcessor` (decompiled) catches nothing,
so a database fault or timeout during session resolution propagates straight into this handler. The
result: a `POST` to sign-in (sent with a stale cookie), change-password, the account-deletion
step-up, staff sign-in or reset confirmation, hitting a database fault at exactly the wrong moment,
logs the caller's plaintext password or reset token -- HO-ACC-001 §4.4/§20.2, an explicitly
forbidden shape ("no plaintext password, token or CAPTCHA answer in any ... log line"). The
first-pass rejection of "re-implement a local exception handler" traded a real, if unlikely,
security defect for a hypothetical inconsistency in error-body shape between failure sites --
worth revisiting once the trade was understood correctly, per HO-ACC-001 §9's own instinct that
`.ai/decisions/adr.md` records are revised, not treated as final on first write.

**Amended decision.** Do not place any request-body-reading exception handler ahead of model
binding, at any pipeline position. The **one** host-level exception boundary is now a small,
purpose-written `SafeExceptionHandlerMiddleware` (`Api/Hosting/`) that never reads
`HttpContext.Request` beyond `Method`/`Path` for its log line, and matches the platform handler's
response shape for the configured `ErrorVerbosity` (unset, as in the committed `appsettings.json`,
means an empty body -- identical to what the platform handler already produces for that setting).
It is registered **exactly once**, in `Program.cs`, as the single shared pipeline prefix every area
runs behind:

```
app.UseForwardedHeaders();
app.UseRouting();
app.UseMiddleware<SafeExceptionHandlerMiddleware>();   // ours; never reads the body

app.UseAccountsModule();          // adds ONLY SessionAuthenticationMiddleware + UseRateLimiter
// a later Use<Area>Module() call would add ONLY that area's own middleware, here, in the same way

app.UseHttpPipeline(enableAuthorization: false);   // UseRouting (again, harmless) -> GlobalExceptionHandlerMiddleware
                                                    // (again, now genuinely unreachable) -> Cors -> UseEndpoints (dispatch)
app.MapControllers();
```

`Use<Area>Module()` no longer calls `UseForwardedHeaders()`, `UseRouting()` or any exception
handler itself -- it adds only that area's own middleware. This is the fix to the pattern-scaling
problem the original Decision created: with the shared prefix run once, host-level, a second and
subsequent area's own `Use<Area>Module()` hook never needs to repeat `UseRouting()` or an exception
handler, so N areas never stack N handlers, and `Program.cs` still needs no edit when a new area's
`Add<Area>Module()` call is filled in -- only the one line adding that area's own `Use<Area>Module()`
call, in the same position, the first time that area actually has pipeline middleware to add.

**Correcting the ADR-341 citation.** The original Decision said this "amends ADR-341's
`Use<Area>Module()` shape" -- ADR-341 defines no such thing. Re-reading ADR-341's own Decision text,
it specifies exactly one hook per area: `Add<Area>Module(this IServiceCollection, IConfiguration)`,
for DI registration (point 4). `Use<Area>Module()` -- an *application*-pipeline hook, not a
*registration* one -- is a convention HO-ACC-001 introduced on top of ADR-341 while building the
session/rate-limiting middleware, not something ADR-341 already specified and this ADR amends. This
amendment corrects the record: **ADR-342 defines the `Use<Area>Module()` pipeline-hook convention**
(one shared host-level prefix, each area's own hook adding only its own middleware after it) as a
companion to ADR-341's `Add<Area>Module()` DI-registration hook, not as an amendment to it.

**This is a cross-cutting decision recorded from an area branch and needs ratification.** Like the
HO-ACC-001 §1 Amended rows for the three out-of-scope files, this ADR affects every future area
that ever needs its own pipeline middleware, not only Accounts. The integrator or the architecture
owner should ratify it (or replace it) when the next such area lands; until then it governs how
this host's pipeline is actually built, because it is what the code in this branch does.

**Rejected alternatives, revisited:**
- **Keep `UseGlobalExceptionHandler()`, restricting its use to a context where the body is already
  safe** -- rejected: there is no such position for a handler that must also protect
  `SessionAuthenticationMiddleware`, which by definition runs ahead of model binding for every
  request, matched or not.
- **Wrap only `SessionAuthenticationMiddleware` itself in a local `try`/`catch`, leaving no
  host-level boundary** -- considered and rejected in favour of the single host-level middleware:
  the rate limiter's own middleware (`UseRateLimiter()`) is platform code this application cannot
  wrap in a local `try`/`catch` from inside `SessionAuthenticationMiddleware`, and a per-middleware
  `try`/`catch` in every area that ever adds pipeline middleware reintroduces the "N areas, N
  handlers" problem this amendment removes, just moved from module-hook code into every
  middleware's own body instead.
- **The original rejection, "re-implement a local exception handler... would risk a second,
  differently-shaped error body"** -- rejected in turn: `SafeExceptionHandlerMiddleware` reads the
  same `ErrorVerbosity` configuration key and reproduces the same three response shapes, so there
  is no second shape in practice; and even a genuine shape difference would be a smaller defect
  than the credential leak the original decision produced.

---

> ### ⚠️ The code block and "now genuinely unreachable" claim in the amendment above are corrected
> below (2026-09-25, SECURITY gate + ARCHITECTURE third pass on HO-ACC-001)
>
> The second-pass amendment's own diagnosis (the platform handler still guards
> `UseCors`/`UseEndpoints`, so it is nested *inside*, not made unreachable by, an earlier
> `SafeExceptionHandlerMiddleware`) is correct elsewhere in `.ai/`'s review history for this
> handover, but the code block above and its inline comment ("now genuinely unreachable") say the
> opposite, and are wrong. The ARCHITECTURE third-pass gate caught this directly: the inner platform
> instance is nested inside the outer one, wraps `UseCors`/`UseEndpoints`, and therefore still
> catches -- and could still log the body of -- any exception that reaches it without first passing
> through model binding (a resource or authorization filter, controller activation). The SECURITY
> gate then found this live and exploitable, not merely mis-described: a resource filter thrown
> before binding, with `Content-Type: application/json`, still logged the plaintext password.

**Second amendment (2026-09-25) -- `UseHttpPipeline()` is removed from this pipeline entirely.**

There is no seam inside `UseHttpPipeline()` a caller can reach to remove only its exception handler
while keeping its CORS policy or its health-check mapping; decompiled, all four (routing, exception
handling, CORS, endpoint dispatch) are one call. Keeping `UseHttpPipeline()` at all therefore keeps
its nested `GlobalExceptionHandlerMiddleware` reachable for anything MVC throws ahead of model
binding, and its CORS policy (`SetIsOriginAllowed(_ => true).AllowCredentials()` -- a SECURITY gate
MEDIUM in its own right once this handover adds cookie sessions: any same-site origin, not just any
origin, can send the session cookies under `SameSite=Lax`) stays live regardless of what runs ahead
of it. The amended decision is to stop calling it:

```
app.UseForwardedHeaders();
app.UseRouting();
app.UseMiddleware<SafeExceptionHandlerMiddleware>();   // the ONLY exception handler anywhere now
app.UseCors(AccountsModule.CorsPolicyName);            // explicit allow-list, empty = fail closed

app.UseAccountsModule();          // adds ONLY SessionAuthenticationMiddleware + UseRateLimiter

app.MapHealthChecks("/health");   // UseHttpPipeline() used to map this internally
app.MapControllers();
```

What was kept from `UseHttpPipeline(enableAuthorization: false)`, and what was not, decompiled
(`Platform.Infrastructure.Host.WebApi` 1.0.14):

| Piece | Kept? | Replacement |
|---|---|---|
| `ServiceIdHeaderMiddleware` / `TenantIdHeaderMiddleware` | Never active (gated on flags this call never passed `true`) | Nothing -- dropped, no behaviour change |
| `UseRouting()` | Yes | Called explicitly, once, earlier in the same block |
| `GlobalExceptionHandlerMiddleware` | No | `SafeExceptionHandlerMiddleware` -- never reads the body, a header or the query string, for an exception from ANY position now, not only session resolution and the rate limiter |
| `UseCors(SetIsOriginAllowed(_ => true).AllowCredentials()...)` | No | `UseCors(AccountsModule.CorsPolicyName)`, an explicit origin allow-list from `Cors:AllowedOrigins`, empty by default (fail closed) |
| `UseAuthentication()` / `UseAuthorization()` | Never active (gated on `enableAuthorization`, passed `false`) | Nothing -- this solution has no ASP.NET Core Identity scheme (ADR-024); authorization is `[RequireAction]` (ADR-036) |
| `UseEndpoints(... MapControllerRoute("Default", ...); MapHealthChecks("/health"); ...)` | `MapHealthChecks("/health")` only | Called explicitly. `MapControllerRoute` (conventional routing) is dropped -- every controller in this solution uses attribute routing; nothing depended on the conventional template |

This also finally makes the second-pass amendment's own stated rule true rather than aspirational:
"do not place any request-body-reading exception handler ahead of model binding, at **any** pipeline
position" -- with `UseHttpPipeline()` gone, there is no second, nested handler left at any position,
ahead of or behind anything.

**CSRF is also now bound to the session, closing a second, related gap.** The double-submit value
in `pc_csrf` was an unrelated random value, compared only against the `X-CSRF-Token` header's own
string equality. Combined with the CORS gap above (a same-site sibling origin could send the session
cookies), an attacker page on such an origin could plant its own `pc_csrf` cookie (cookie tossing)
and echo that same, attacker-chosen value in the header -- both sides attacker-controlled, so the
check passed regardless of the real session. `CsrfTokenBinder` (`Api/Sessions/`) computes the
`pc_csrf` value as `HMAC-SHA256(processKey, sessionTokenHash)`; `SessionAuthenticationMiddleware`
recomputes the same value from the ACTUAL resolved session (found via the `HttpOnly` session
cookie, not attacker-settable) and compares the header against that -- the request's own `pc_csrf`
cookie value is never read or trusted for the security decision, only used as the mechanism by
which legitimate client-side JavaScript learns what to put in the header. The cookie and header
names (`pc_csrf` / `X-CSRF-Token`) are unchanged, since the web client already depends on them.

**Consequences, revised:**
- `SafeExceptionHandlerMiddleware` is now the single exception boundary for the entire request
  pipeline, not only the segment ahead of `UseHttpPipeline()`. A later area's own middleware, and
  every controller action and MVC filter in this solution, all run inside it.
- CORS and health-check mapping move from platform-owned, implicit behaviour to explicit,
  application-owned lines in `Program.cs`/`AccountsModule.cs` -- a future area adding its own
  pipeline middleware still adds only that, per ADR-342's original point.
- `CsrfTokenBinder`'s key is process-lifetime, matching the CAPTCHA cache's and the rate limiter's
  own already-accepted "single host, resets on restart" threat model (ADR-010) -- no configuration,
  no distributed state.

---

> ### ⚠️ The "CsrfTokenBinder is also now bound to the session" section above, and its
> "process-lifetime key ... matching the CAPTCHA cache's ... threat model" consequence, are
> WRONG and are corrected below (2026-09-25, SECURITY gate second pass + ARCHITECTURE fourth pass
> on HO-ACC-001, owner-ratified the same day)
>
> The claim that a process-lifetime CSRF key is equivalent to the CAPTCHA cache's or the rate
> limiter's "resets on restart" threat model is false: a CAPTCHA cache or a rate-limiting counter
> resetting on restart is benign (a challenge or a count is simply forgotten; nothing already
> issued becomes unusable). A CSRF key resetting on restart is not benign, because the value it
> protects -- the session -- does **not** reset with it: sessions are durable rows in Postgres,
> living up to 7 days (customer) or 12 hours (staff), while `CsrfTokenBinder`'s key lived only as
> long as the process. The doc comment's own claim that "only an in-flight unsafe request racing a
> restart would need to re-fetch a page" was also false -- it understated the blast radius to a
> single in-flight request, when in fact **every** signed-in caller was affected, for the **rest of
> their session's lifetime**, not just a moment during the restart itself.

**Third amendment (2026-09-25) -- CSRF moves from a process-lifetime key to a per-session, DB-durable
token (owner decision, 2026-09-25).**

Proven live (SECURITY gate, second pass): sign in on host A, then send the same `pc_session`/
`pc_csrf` cookies to a second `WebApplicationFactory` host B against the *same* database (simulating
a restart, since the session row itself is unaffected by the process boundary). On host B: `GET
/api/customers/me` still succeeds (no CSRF check on a safe method), but `PUT /api/customers/me`,
`POST /api/auth/sign-out` and even `POST /api/auth/sign-in` (with or without the header) all
returned `403 csrf_token_invalid` -- because host B's `CsrfTokenBinder` has a freshly generated,
different key, so it can never recompute the value host A's key produced. `pc_session` is
`HttpOnly`, so no client-side script can clear it either; the caller is stuck until the session
itself expires (up to 7 days) or they clear cookies by hand. This happens on **every** deploy,
crash or process recycle -- not a rare race.

**Amended decision.** The CSRF value is no longer derived from anything process-lifetime. Each
session aggregate (`CustomerSessionAggregate`, `StaffSessionAggregate`) gains a `CsrfTokenHash`
column, populated at `Issue()` from a **second, independent** call to the same
`ISessionTokenIssuer.Issue()` the session token itself uses (32 bytes CSPRNG, Base64Url-encoded;
hash = hex SHA-256) -- no new cryptographic mechanism, just the existing one used twice. Only the
hash is persisted, on the session row itself; the raw value is returned once, alongside the session
token, in `SignInResult.CsrfToken`, and the controller writes it to `pc_csrf` the same way it always
wrote `pc_session`. Because the hash lives in Postgres next to the session it protects, it survives
a restart exactly as durably as the session does -- the failure mode above cannot occur, by
construction, regardless of which process (or how many) is serving requests against that database.

`SessionAuthenticationMiddleware` hashes the incoming `X-CSRF-Token` header and compares it, in
constant time, against the resolved session's stored `CsrfTokenHash` -- exposed on
`CustomerSessionViewModel`/`StaffSessionStatusViewModel` (both `[JsonIgnore]`d, since both records
are also serialised directly to a response body elsewhere) so no second query is needed. The
`pc_csrf` **cookie's own value is still never trusted as proof of anything** -- that part of the
original design was correct and is unchanged -- but it is now also read, on a **safe** (GET)
request only, to decide whether to opportunistically re-issue a fresh one: if it is missing or its
hash does not match the stored value, `RefreshCustomerSessionCsrfTokenCommand` /
`RefreshStaffSessionCsrfTokenCommand` mint a new token+hash pair (best-effort; a concurrency
conflict or a resolution failure here is swallowed, never blocks the underlying safe request) and
the middleware writes the fresh cookie. This is what lets a legitimate browser recover on its own
from a missing or stale `pc_csrf` cookie, for reasons unrelated to a restart (a cleared cookie
store, a pre-migration session, a bug) without waiting for the session to expire.

**Sign-in is now explicitly CSRF-exempt.** Both `AuthController.SignIn` and
`StaffAuthController.SignIn` carry a new `[CsrfExempt]` marker; `SessionAuthenticationMiddleware`
skips the CSRF check entirely for an endpoint carrying it, regardless of whether a stale or
otherwise-resolvable session cookie is still present. Re-authenticating is how a legitimate caller
recovers from exactly that situation -- a session that still resolves but should no longer be
trusted -- so it must never itself be blocked by the very check it exists to bypass. This is a
belt-and-braces addition on top of the DB-durable fix above, not a substitute for it: the DB-durable
hash alone already makes the two-host restart scenario succeed end-to-end (sign in on host 1;
`PUT /me`, sign-out and re-sign-in on host 2 all succeed), because both hosts read and write the
same row.

**Correction (2026-09-25, ARCHITECTURE gate fifth pass, MEDIUM): `[CsrfExempt]` skips session
resolution entirely, not only the CSRF check.** The paragraph above understates what the code does.
`SessionAuthenticationMiddleware` does not merely skip comparing the header against the stored hash
for a `[CsrfExempt]` endpoint -- it skips resolving either session cookie at all, so both sign-in
actions run as anonymous regardless of what `pc_session`/`pc_staff_session` contains. This was
discovered live: the customer branch already falls through to anonymous when a session does not
resolve (no refusal, see `else cookieWriter.ExpireCustomerSession(context)` above), so the
two-host restart test passed for the customer path on the CSRF fix alone. The STAFF branch does
not share that shape -- `TryResolveStaffAsync` hard-refuses (R5) whenever the session does not
resolve, for every OTHER staff endpoint, by design -- so the staff half of the same test failed
until session resolution itself, not just the CSRF check, was skipped for `[CsrfExempt]`.

Consequences of skipping resolution, not merely the CSRF comparison:
- A stale or otherwise-unresolvable staff session cookie no longer triggers the R5 refusal at
  sign-in. Every other staff endpoint is unaffected -- R5 still applies to all of them.
- An idle-expired staff session is no longer ended through `EndForIdleTimeout` and audited at the
  moment a stale cookie happens to be presented to sign-in. That row is made terminal and audited
  only when it is next presented to a non-exempt endpoint -- exactly the same as an abandoned or
  simply-cleared cookie already behaves. ADR-037's "audited once, on detection" still holds; only
  the detection moment can move later.
- The session row a caller replaces by signing in again survives the sign-in unended. This is
  unchanged from before this fix in either direction -- neither the pre-CSRF-fix code nor this
  code ever ends the PREVIOUS session as a side effect of a fresh sign-in, so whoever already holds
  that token can still use it until it expires on its own. Session fixation is still not possible:
  the session token is always server-generated at `Issue()`, so a planted `pc_session` value is
  never adopted, and a fresh, independent CSRF token is minted alongside it every time.

**Deployment note.** Do not set the `ASPNETCORE_FORWARDEDHEADERS_ENABLED` environment variable on
this host. That flag makes the ASP.NET Core framework itself call `UseForwardedHeaders()` a second
time, with its own default options -- which clear `KnownProxies`/`KnownNetworks` -- ahead of
`HostPipelineExtensions`' own configuration, reopening the pass-1 SECURITY gate HIGH
(`X-Forwarded-For` honoured from any peer). Trust a proxy only via
`RequestOrigin:TrustedProxies`/`TrustedNetworks` configuration.

**Consequences, revised again:**
- The CSRF value is durable across a restart by construction (it lives in the same row, and the
  same database, as the session it protects) -- there is no longer a "single host" or "resets on
  restart" threat model for it at all, unlike the CAPTCHA cache and the rate limiter, which
  genuinely do reset on restart and remain fine with that (ADR-010).
- `CsrfTokenBinder` (`Api/Sessions/`) is deleted. `SessionCookieWriter.WriteCsrf` no longer computes
  anything; it writes whatever raw token it is given.
- The host-wide CORS policy and `ForwardedHeadersOptions` configuration also move out of
  `AccountsModule` into `src/Api/CatEcommerce.Api/Hosting/HostPipelineExtensions.cs`, called once
  from `Program.cs`'s shared block -- both are host-level concerns applied ahead of every area, not
  an Accounts-owned policy (ARCHITECTURE gate, fourth pass, MEDIUM; owner decision, 2026-09-25). The
  CORS policy is renamed from `AccountsAllowedOrigins` to `HostAllowedOrigins`.
- Dropping `UseHttpPipeline()` (the second amendment above) is ratified by the owner as the
  permanent design, not a provisional self-amendment awaiting integrator review. The `.ai/` guide
  docs that still prescribe `UseHttpPipeline()` will be corrected by the integrator after wave A.

**Rejected alternatives:**
- **Persist the process key itself** (a configured secret, or an ASP.NET Data Protection key ring)
  -- rejected in favour of a per-session token: a shared key still couples every session's CSRF
  validity to one piece of external configuration/infrastructure, and a compromised key invalidates
  or forges CSRF for every session at once. A per-session value's blast radius on compromise is one
  session, matching the session token's own already-accepted model.
- **Skip the CSRF check on the two sign-in endpoints only, without also fixing the key's
  durability** -- rejected: this would have papered over the symptom the two-host probe exercises
  (re-authentication) while leaving every OTHER unsafe request -- `PUT /me`, sign-out, any staff
  write -- still broken for the life of the session after a restart. `[CsrfExempt]` is additive to
  the durable-storage fix, not a replacement for it.

**Amendment (2026-09-27, ACCEPTANCE gate fix on HO-NTF-001) -- the two code sketches above still say
`UseAccountsModule()` "adds ONLY SessionAuthenticationMiddleware + UseRateLimiter". That is stale.**

Once Notifications became a second area contributing its own rate-limit policies
(`NotificationRateLimitPolicies.cs`), `UseRateLimiter()` itself moved out of
`AccountsModule.UseAccountsModule()` into `Program.cs`'s own shared host-composition block, called
once, directly, after `UseAccountsModule()` (HO-NTF-001-ARCHITECTURE second pass MEDIUM,
integrator-authorised -- `.ai/host-composition.md` was updated at the time, but this ADR's own two
code sketches were not). The corrected shape, as shipped:

```
app.UseAccountsModule();          // adds ONLY SessionAuthenticationMiddleware
app.UseRateLimiter();             // must run AFTER UseAccountsModule(): the customer/IP-partitioned
                                   // policies read ResolvedCaller, written by SessionAuthenticationMiddleware
app.MapHealthChecks("/health");
app.MapControllers();
```

`UseAccountsModule()` still adds only its own middleware (`SessionAuthenticationMiddleware`);
`Use<Area>Module()` remains this ADR's convention for every other area. `UseRateLimiter()` is host
composition, not something either area owns, so it is called exactly once, in `Program.cs`, the
same way `UseForwardedHeaders()`/`UseRouting()`/`UseCors()` already are.
**Reason:** Two areas' `AddRateLimiter()` calls compose correctly against one `RateLimiterOptions`
(ASP.NET Core's own extension does `services.Configure<RateLimiterOptions>`, stacking every
configure delegate), but `UseRateLimiter()` itself can only run once; leaving it inside
`AccountsModule` would have made Notifications (and every later area with its own rate-limit
policies) depend on Accounts for something neither area actually owns.

---

## ADR-042 — The `Administration` Bounded Context

**Date:** 2026-09-26 (HO-ADM-001)
**Status:** Active
**Decision:** A new bounded context, `Administration`, namespace segment `Administration`, schema
`admin`, owns branches, closures, rooms, staff branch assignment, rosters, roster exceptions, leave
requests, the clinic service catalogue, prices, tax classes and rates, business settings, content,
policy documents/versions/their acceptances, and system status (§8.1's seventeen aggregate roots:
`Branch`, `BranchClosure`, `Room`, `StaffBranchAssignment`, `StaffRoster`, `RosterException`,
`LeaveRequest`, `ClinicService`, `ServicePrice`, `TaxClass`, `TaxRate`, `BusinessSettings`,
`ContentItem`, `PolicyDocument`, `PolicyVersion`, `PolicyAcceptance`, `SystemStatus` -- corrected
2026-09-26, gate fix: this originally undercounted at fourteen).
**Reason:** `.ai/bounded-contexts.md` did not name this context (OQ-1) even though every later area
in the delivery order reads from it -- branches, rosters and settings are inputs appointment slot
computation, boarding task assignment and every priced transaction all depend on. Recording it here
is the authoritative answer OQ-1 asked for; `.ai/bounded-contexts.md` itself is corrected by the
architect track, not this branch (§14 forbids editing it directly).

---

## ADR-043 — Every Endpoint Declares Its Access; Customer Ownership Is `OwnerScope`

**Date:** 2026-09-26 (HO-ADM-001)
**Status:** Active
**Decision:** Every controller action carries exactly one of `[RequireAction]`,
`[RequireCustomerSession]` or `[PublicEndpoint]` -- enforced by `EndpointAccessConventionTests`
(U-A1), which reflects over the whole Api assembly and fails the build on an undeclared action.
`RequireActionAttribute` gains an `OwnerScope` (`None` | `Cat` | `CatCollection` -- `CatCollection`
added 2026-09-26, gate fix: the explicit opt-in for the two genuinely id-less actions, register and
list, so a missing or unparsable `catId` on an `OwnerScope.Cat` action fails closed instead of being
read as "no id, so allow"): a customer caller on an action with `OwnerScope.Cat` in
`CustomerActions.Cat` is checked against the resource's *current* owner via a dispatched query
(`GetCatOwnerQuery`) -- match allows, anything else is a 404, never a 403 (a customer must not be
able to tell "not yours" from "does not exist" apart). A customer calling an action outside their
scope, or a staff caller failing the existing `ActionRoles` check, both get 403 plus an audited
denial -- the same path, not two different ones. An anonymous caller always gets 401, with no audit
(there is no identity to attribute a denial to).
**Reason:** US-ADM-02 AC1 is "every endpoint's access matches Appendix D, regardless of what the UI
shows"; fail-closed-by-role alone (HO-ACC-001 ADR-036) does not catch an endpoint someone forgot to
attribute at all -- only a convention test does. The Cat/Vaccine mapping onto Appendix D's two rows
("own", "any") is an inference (OQ-4): `Cats.View`, `Cats.Register`, `Cats.EditProfile` are the
customer's "own" actions (`CustomerActions.Cat`); every other Cat/Vaccine action is staff-only.

---

## ADR-044 — Permissions Remain Code-Defined

**Date:** 2026-09-26 (HO-ADM-001)
**Status:** Active
**Decision:** US-ADM-02's criteria ("permissions match Appendix D", "403 + logged") are met entirely
by the code-defined `ActionRoles` table. An administrator-editable permissions screen is not built.
This amends HO-ACC-001 ADR-036's note that editing was deferred "to US-ADM-02" -- it is not built by
this handover either.
**Reason:** An editable matrix lets one mis-click lock the clinic out of its own records; no
acceptance criterion asks for it (OQ-2, owner default: no).

---

## ADR-045 — Time Model: Branch-Local Wall-Clock In, Instants Out, Conversion In One Place

**Date:** 2026-09-26 (HO-ADM-001)
**Status:** Active
**Decision:** Opening hours, shifts and closures are stored as branch-local `TimeOnly`/`DateOnly`.
Every branch stores an IANA time-zone id (`TimeZoneInfo.FindSystemTimeZoneById`, never a Windows id
or a fixed UTC offset). Every stored instant is `timestamptz`. Local-to-instant conversion happens in
exactly two places: `BranchCalendar.OpenIntervals` and `StaffAvailability.Intervals` (both pure,
side-effect-free domain services) -- nowhere else in the solution converts a branch-local time.
**Reason:** A second conversion site is a second place for a DST or second-branch bug to hide; §20.7
forbids it explicitly. `TimeZoneInfo` with IANA ids is in the .NET 9 runtime on both Windows and
Linux (F22) -- no time-zone library is needed.

---

## ADR-046 — Active Branch Resolution

**Date:** 2026-09-26 (HO-ADM-001)
**Status:** Active
**Decision:** `IActiveBranchAccessor` resolves a staff caller's active branch: the `X-Branch-Id`
header if present and the caller is assigned to it (else `403 BranchNotAssigned`); otherwise the
caller's `PrimaryBranchId`; if the caller has no assignment at all, the sole active branch if exactly
one exists, else `403 BranchAssignmentRequired`. A customer or anonymous caller always resolves to no
active branch. Endpoints that need a branch take it explicitly (a route segment); the accessor is
resolved on demand, never as a pipeline filter, so an endpoint that does not need a branch never
fails on a missing or bad header.
**Reason:** NFR-SCL-1 requires a second branch to work with no code change, while SRS A-1's
single-branch launch must need no assignment step at all -- the sole-active-branch fallback is what
makes both true at once.

---

## ADR-047 — Staff Availability Is a Pure Domain Service; Patterns Promote Lazily

**Date:** 2026-09-26 (HO-ADM-001)
**Status:** Active
**Decision:** `StaffAvailability.Intervals` is a pure function over a pre-loaded roster, its
exceptions, approved leave and the branch's open intervals (from `BranchCalendar`) -- no I/O, so
Appointments and Boarding can generate slots, and this can be tested, without a database. A roster
holds a current weekly pattern and at most one scheduled next pattern with an effective date; a due
next pattern is promoted to current lazily, by any command that loads the roster (`PromoteDuePattern`)
and by this same read-side calculation -- never by a background job.
**Reason:** `.ai/bounded-contexts.md` rule 4 names appointment/availability calculation as a domain
service; a pure function is exactly what a slot-generation algorithm needs to be unit-tested without
a database. §20.8 forbids a background job for pattern promotion.

---

## ADR-048 — `StaffLeaveApprovedEvent` / `LeaveCancelledEvent` Are Integration Contracts

**Date:** 2026-09-26 (HO-ADM-001)
**Status:** Active
**Decision:** Approving or cancelling leave raises `StaffLeaveApprovedEvent` /
`LeaveCancelledEvent(WasApproved)` -- ids, dates and a boolean only, no note or reason. Approving
leave does **not** look at appointments or boarding tasks; HO-APT-001 and HO-BRD-001 subscribe to
these events to flag/reassign their own records when they land. Both events' consumers must be
idempotent.
**Reason:** US-ADM-03 AC2 ("flag affected appointments") is delivered in two halves across two
handovers that do not exist together yet; the split has to be a deliberate, named contract, not an
implicit assumption.

---

## ADR-049 — Prices and Tax Rates Are Append-Only, Effective-Dated Roots

**Date:** 2026-09-26 (HO-ADM-001)
**Status:** Active
**Decision:** `ServicePriceAggregate` and `TaxRateAggregate` are created once and never mutated again.
`EffectiveFrom` must be at or after the handler's own `TimeProvider.GetUtcNow()` (never back-dated);
two rows for the same service/tax class may not share an `EffectiveFrom` (a unique index enforces
it). The value "at instant *t*" is the row with the greatest `EffectiveFrom <= t`. Every later area
that charges money reads the price/rate at the moment of the transaction and stores a snapshot on the
transaction -- it never joins back to reprice something that already happened.
**Reason:** US-ADM-04's AC is "a price change applies to new transactions only"; overwriting a price
column would silently reprice history, and back-dating would let a price apply to a transaction that
already happened.

---

## ADR-050 — Business Settings: a Typed Registry, Two Scopes, a Short Cache

**Date:** 2026-09-26 (HO-ADM-001)
**Status:** Active
**Decision:** `SettingDefinitions` (Shared, reflection-aggregated per-area partials, same pattern as
`ActionRoles`/`RecordClassification`) is the registry: key, group, type, bounds, unit, default,
branch-overridability. `BusinessSettingsAggregate` stores values per scope (global, or a branch id)
as invariant-culture strings; `Change` is all-or-nothing across every key in one call. Resolution
order is branch override -> global value -> the definition's default. `CachedBusinessSettings`
caches each scope for 30 seconds and is invalidated immediately on a `BusinessSettingsChangedEvent`
via the `ISettingsCacheInvalidator` port (kept in Domain so the Application-layer invalidation
handler never needs a forbidden reference to Infrastructure).
**Reason:** FR-ADM-5 needs a setting change without a release; one registry stops every later area
from inventing its own settings table. The 30-second cache with event-driven invalidation is what
makes US-ADM-05's "takes effect without a restart" true without hitting the database on every read.
**Single-host assumption (documented 2026-09-26, gate fix):** `CachedBusinessSettings` is an
in-process `IMemoryCache`, not a distributed cache -- correct for this solution's single web host
(`.ai/host-composition.md`: "There is one host"), where invalidation via
`ISettingsCacheInvalidator` reaches the one process holding the cache directly. If this ever became
multiple host instances behind a load balancer, the 30-second TTL would still bound staleness on any
instance that missed the invalidation (broadcast, not targeted), but a stricter "no restart" reading
of US-ADM-05 across instances would need a distributed cache or a pub/sub invalidation broadcast --
out of scope while ADR-010's single host stands.

---

## ADR-051 — Audit Log Read and Export Are Read-Only and Self-Auditing

**Date:** 2026-09-26 (HO-ADM-001)
**Status:** Active
**Decision:** `GetAuditEntriesQuery` is a read-only projection over `audit.audit_entry` -- no
repository, filtered on the indexed columns this handover adds (`(actor_user_id, occurred_on)`,
`(entity_type, occurred_on)`, `(action, occurred_on)`, `(occurred_on)`). CSV export is capped at
50,000 rows (`413` above the cap) and prefixes any cell beginning `= + - @` with `'` (CSV-injection
guard). Every export is itself audited as a `BusinessEvent` (`AuditLogExported`) carrying the filters,
written directly by the handler (no aggregate mutation to hang it on).
**Reason:** Reading the audit trail is itself a sensitive act (who looked at what, and when) --
US-ADM-06 requires it searchable and exportable, and a spreadsheet formula in an exported reason or
note is a real, documented attack on exported logs.

**Addendum 2026-09-26: list caps (gate fix; supersedes a draft heading withdrawn before merge).**
`docs/handover/README.md`'s ADR-numbering table allocates `ADR-055…066` to HO-NTF-001, so an
`ADR-055` heading written by this handover would collide with it once HO-NTF-001 lands; the decision
is folded in here instead, as this ADR is the closest existing precedent for "capping a read-side
list rather than paginating it". Six admin read lists that had no natural bound and were never
paginated -- branch closures and roster exceptions when no date range is given, a service's price
history, a tax class's rate history, a staff member's own leave requests, and the policy list with
its per-document version history -- are capped at `AdministrationListCaps.MaxUnboundedListSize`
(100) rather than converted to full `PagedResult` paging, for the same reason this ADR's own audit
export is capped rather than paged: none of these lists is expected to legitimately exceed a few
dozen rows in real operation (a service does not accumulate hundreds of prices; a staff member does
not accumulate hundreds of open leave requests). Each of the five flat endpoints, and the outer
document list of `GET /api/admin/policies` (not its per-document nested version cap, which cannot be
attributed to one document via a single response header without changing the response shape), sets
`X-Truncated: true` when the returned count equals the cap exactly (`TruncationHeader.ApplyIfAtCap`)
-- a cheap signal (one comparison, no extra query) that can rarely false-positive when the real total
happens to equal the cap exactly, but never false-negatives, so the frontend can show "showing first
100". Proven by `AdminBranchEndpointTests.GetClosures_MoreThanTheCap_TruncatesAndSignalsWithHeader`
(creates 101 closures, asserts both the cap and the header).

---

## ADR-052 — Content Is Limited Markup, Never HTML; Policies and Acceptances Are Append-Only

**Date:** 2026-09-26 (HO-ADM-001)
**Status:** Active
**Decision:** `ContentItemAggregate.DraftBody`/`PublishedBody` and `PolicyVersionAggregate.Body` are
`LimitedMarkup` -- paragraphs, `##`/`###` headings, `- ` bullets, `**bold**`, and `[text](url)` links
that are https or site-relative; anything that looks like a tag, `javascript:` or `data:` is rejected
outright. No HTML is ever stored or served. `PolicyVersionAggregate` and `PolicyAcceptanceAggregate`
are append-only. Publishing a policy version is the one recorded exception to "one aggregate mutated
per command" (`.ai/bounded-contexts.md` rule 1): it creates the version, then updates the document's
`CurrentVersionNumber` pointer as a second write -- safe because the pointer is a derivable cache (the
public read uses `MAX(version_number)`, so a lag between the two writes is never visible) and its own
mutation raises no event.
**Reason:** Stored HTML is stored XSS waiting for a renderer (§20.5 forbids it explicitly). NFR-LEG-5
needs the exact text a customer accepted retained forever, which append-only versions and acceptances
guarantee structurally rather than by convention.

---

## ADR-053 — Maintenance Mode Never Gates the Back Office

**Date:** 2026-09-26 (HO-ADM-001)
**Status:** Active
**Decision:** `SystemStatusAggregate` (a singleton) exposes `MaintenanceMode`/`MaintenanceMessage`
through a public `GET /api/system/status`. No admin endpoint, and no sign-in endpoint, checks it.
`[StorefrontEndpoint]` (a marker attribute) and `MaintenanceModeFilter` (a global MVC action filter,
registered once in `AdministrationModule`, that no-ops on any action not carrying the marker) are
both built now, in this slice -- not merely named for later -- so a later storefront-owning
handover only has to add the attribute to its own actions, never register or write the filter
itself. This slice applies the marker to nothing (there is no storefront endpoint here yet); a
test proves an admin endpoint still answers while maintenance mode is on (gate fix 2026-09-26 --
the filter and attribute did not exist in the original build despite this ADR and §12.5 already
specifying them).
**Reason:** US-ADM-08's AC is a storefront notice; an administrator must always be able to turn
maintenance mode off, which is only true if nothing administrative is ever gated by it.

---

## ADR-054 — Money Is `decimal(12,2)` With an ISO-4217 Code; `BDT` Is the Seeded Default

**Date:** 2026-09-26 (HO-ADM-001)
**Status:** Active
**Decision:** `Money` (Domain.ValueObjects, shared by every later area that prices something) is a
`decimal(12,2)` amount plus an upper-case three-letter ISO-4217 currency code -- never a floating
point type. `commerce.currencyCode` is a business setting (registry type `CurrencyCode`, a fifth
`SettingType` member added by this handover), seeded `BDT`. Tax rates and regimes are data
(`TaxClassAggregate`/`TaxRateAggregate`), not a hard-coded regime.
**Reason:** SRS Q-5 (base currency and tax regime) is an open question; representing money as a
decimal amount plus an explicit code means the representation does not need to change whenever that
question closes.

---

## ADR-055 — `Notification` Bounded Context

**Date:** 2026-09-27 (HO-NTF-001)
**Status:** Active
**Decision:** A new `Notification` bounded context (namespace segment `Notification`, schema
`notify`) owns three aggregate roots -- `NotificationRequestAggregate`, `NotificationAggregate` and
`NotificationTemplateAggregate` (HO-NTF-001 §8.1). Consent (marketing opt-in/opt-out per channel)
and a customer's preferred delivery channel stay on `Account`'s own preferences aggregate;
`Notification` only reads them, through `INotificationRecipientDirectory` -- it never owns them.
**Reason:** `.ai/bounded-contexts.md` had no `Notification` context (OQ-2); without this record, the
consent/delivery split (consent in `Account`, everything else in `Notification`) reads as an
oversight rather than the deliberate boundary it is.

---

## ADR-056 — Two-Stage Outbox: Request, Then Per-Channel Notification

**Date:** 2026-09-27 (HO-NTF-001)
**Status:** Active
**Decision:** A producer enlists a `NotificationRequestAggregate` in its own unit of work (F23) --
one row, no channel decided yet. The `Notification` worker later expands that request into one
`NotificationAggregate` per channel (the actual outbox item, delivery log and inbox item in one
row); `ExpandNotificationRequestCommandHandler` commits the request and every notification it
produces in **one** `SaveChangesAsync`. The same exception applies to
`DeliverNotificationCommandHandler` (C5): recording a failure that triggers a fallback commits the
now-`Undeliverable` notification and the new fallback `NotificationAggregate` it enlists in that
same save. Both -- expansion and fallback -- are a recorded exception to
`.ai/bounded-contexts.md` rule 1 ("one aggregate mutated per command"), atomic by construction
rather than by convention.

**Amended 2026-09-27 (SECURITY/CODE_REVIEW gate fix, third pass).** A save that fails partway
through that commit is not retried forever unconditionally. `NotificationRequestAggregate
.RecordFailedAttempt` moves the request to a terminal `PersistenceFailed` state after
`NotificationRequestRules.MaxExpansionAttempts` (5) consecutive failed commits, recorded from a
brand-new scope (`RecordNotificationExpansionFailureCommand`, dispatched by
`NotificationWorkCycle`) since the scope whose `SaveChangesAsync` just failed is left unusable. The
backstop is scoped to `DbUpdateException` only -- the one type `EfAggregateRootRepository
.CommitAsync`'s `SaveChangesAsync` call throws -- so a transient failure from any OTHER dependency
the expansion reads (the recipient directory, a subject guard, the branch repository, the
quiet-hours calendar) is never counted, and keeps retrying indefinitely; only a genuine save failure
ever arms the counter.
**Reason:** `.ai/architecture.md`'s command-handler-pattern doc assumes one root per command; this
ADR is why Notification's expansion step is different. The amendment records why "retry forever"
and "give up after N" had to be two different rules for two different failure classes, not one --
an earlier, broader version of the backstop counted both and silently dropped requests on a
transient database blip.

---

## ADR-057 — Producer Rules: `INotificationOutbox.Enqueue`, `RequestNotificationCommand` for Timed Producers Only

**Date:** 2026-09-27 (HO-NTF-001)
**Status:** Active
**Decision:** A producer that wants a notification sent calls `INotificationOutbox.Enqueue
(NotificationRequestDto)` (Domain.DomainServices) in its own command handler. `EfNotificationOutbox`
builds the `NotificationRequestAggregate` and adds it to the scoped `DbContext` **without saving**;
the producer's own repository save then commits it together with the business change it describes
(F23). If the producer's handler throws before saving, the scope is discarded and the request never
existed. §8.4's four producer rules (checked by the ARCHITECTURE gate):
1. Call `Enqueue` in the command handler, before the aggregate's `SaveAsync`/`UpdateAsync`.
2. Never from a domain event handler -- events dispatch after the commit (F24), so a failure there
   would fail a request whose change is already durable.
3. A timed producer (the care-reminder sweep, ADR-065) has no business change to ride on. It
   dispatches `RequestNotificationCommand` with a `DeduplicationKey` instead, which saves the
   request on its own and treats a duplicate key as success: the request repository's `ExistAsync`
   is checked before insert, and a `23505` from the store's own unique index is translated to
   success too (the race lost cleanly).
4. Never pass a business decision in `Variables`. If a message should not be sent, do not enqueue
   it.
**Reason:** An enqueue from a domain event handler is the one shape every later area's ARCHITECTURE
gate is told to check against (F24) -- it would let a notification's intent and the business change
it describes commit in different transactions, so one could exist without the other.

---

## ADR-058 — Routing Policy (R1-R9)

**Date:** 2026-09-27 (HO-NTF-001)
**Status:** Active
**Decision:** `NotificationRoutingPolicy` is one pure function deciding, from a template definition
and a recipient profile: the preferred channel (read from the Account preferences aggregate,
defaulting to whichever identifier the customer registered with); that a Transactional template
ignores marketing consent entirely; that a Marketing template needs consent on that specific
channel; an always-on inbox copy for customer recipients, except for a template that carries a
secret; fallback to the other viable external channel on permanent failure, walking the whole
`FallbackFromId` chain back to the root (never just the immediately-failing notification's own
channel, so two channels permanently failing terminates rather than alternating forever, one new
row per poll); no push channel in v1; a request for a merged customer routes to the survivor.
**Reason:** FR-NTF-1 (channel preference) and FR-NTF-4 (fallback) become one function every gate can
read and test directly, rather than logic scattered across the delivery handler.

---

## ADR-059 — Quiet Hours

**Date:** 2026-09-27 (HO-NTF-001)
**Status:** Active
**Decision:** A `Normal`-urgency external notification is held to the branch's own configured
quiet-hours window (in the branch's IANA time zone), checked both at expansion (a request created
inside quiet hours is queued deferred, never sent immediately) and again before every delivery
attempt (a long retry backoff can carry a notification back into a later quiet-hours window).
`Urgent` templates (only `account.password-reset` today) and inbox copies are never held. A hold
that would still be waiting past the request's own `NotAfter` expires the notification rather than
sending it late.
**Reason:** FR-NTF-5 requires configured quiet hours; "held until the window opens" and "sent after
the window already closed" are different failures with different consequences (an SMS at 3am vs. a
reminder for an appointment that already happened) -- only a recorded decision distinguishes them.

---

## ADR-060 — Delivery Worker: Claim-by-Lease, Backoff, At-Least-Once

**Date:** 2026-09-27 (HO-NTF-001)
**Status:** Active
**Decision:** One `BackgroundService` (`NotificationWorker`, in the Api host -- the only
asynchronous element in the solution) polls every `Worker:PollIntervalSeconds` (default 5s). Each
due item is dispatched from its own fresh DI scope, one command per item, so one bad row cannot
block or corrupt another's transaction. A notification is claimed by setting a lease
(`LeaseExpiresOn`), committed via `xmin` optimistic concurrency BEFORE the provider is ever called
-- two workers racing for the same row can never both send it; a crash between the claim commit and
the provider call is recovered once the lease expires (a live lease is never re-claimed). A
transient send failure retries at 1/5/15/60/240-minute backoff, 6 attempts total, after which the
notification becomes `Undeliverable`. Delivery is **at-least-once**: the notification's own id is
the idempotency reference a provider integration must de-duplicate on if it cannot tolerate a rare
double send. `Sent` means the provider accepted the message, not that the recipient received it.
**Reason:** This is the only place in the solution with a background loop and real concurrency; its
guarantees (what happens on a crash, on a race, on a provider outage) must be a recorded decision,
not something inferred from reading `DeliverNotificationCommandHandler` under pressure.

---

## ADR-061 — Provider-Neutral Sending Port; Fail Safe and Loud by Default

**Date:** 2026-09-27 (HO-NTF-001)
**Status:** Active
**Decision:** `INotificationChannelSender` is the one port every channel sends through. §8.9's
six-point adapter contract, recorded here so a future provider adapter is reviewed against it
(OQ-1): (1) HTTPS only; credentials from environment or a secret store, never a committed file. (2)
A timeout (`SendTimeoutSeconds`, default 10) counts as a `TransientFailure`. (3) Classify: network
errors, `429` and `5xx` are transient; an invalid destination, a rejected sender or a `4xx` other
than `429` are permanent. (4) Pass `NotificationId` as the provider's client reference or
idempotency key where supported. (5) Never log the body, the subject or the full destination -- log
the notification id and the outcome. (6) Store the provider's response text truncated to 1000
characters, never the request. `DevelopmentChannelSender` and `CapturingChannelSender` refuse to
construct outside `Development`/`Testing` respectively. Outside those two environments, with no
real provider chosen yet, the default is `UnconfiguredChannelSender`: every send is a permanent
failure, and `NotificationWorker` logs a startup warning once per missing channel.

**Amended 2026-09-27 (integrator decision, HO-CAT-001 merge).** `NotificationLinkBuilder`'s three
links (password reset, care, unsubscribe) read the shared `PublicSiteOptions.WebBaseUrl` --
originally a Notifications-only `Notifications:WebBaseUrl` setting, changed once Cat Profiles'
cage-card QR code needed the same base URL. `PublicSiteStartupGuardHostedService` requires and
validates that shared value in Production, so a deployment that forgets it fails to start loud,
rather than silently sending dead links.
**Reason:** The owner explicitly left the SMS/email provider choice open; a default that silently
pretended to work (or silently discarded messages) would hide that decision instead of forcing it.
HO-NTF-001 §1 Amended (2026-09-23, the ADR-320 row) records that ADR-320's SMTP and HTTP-SMS
transports meet this six-point contract -- the citation runs from the handover to this ADR, not
from ADR-320's own text.

---

## ADR-062 — Template Rendering: Parse First, Substitute Second, Never a Template Engine

**Date:** 2026-09-27 (HO-NTF-001)
**Status:** Active
**Decision:** The template catalogue is code (`NotificationTemplateCatalogue`); the wording is data,
optionally customised per template on `NotificationTemplateAggregate`. `NotificationRenderer`
recognises `{{name}}` placeholders (validated against the definition's declared variables) and every
markup construct (headings, bullets, bold, links) in the raw, admin-authored text FIRST, replacing
each placeholder with an opaque sentinel token; only THEN are the real values spliced back in,
HTML-encoded for the email body. A value can therefore never introduce new markup, re-trigger
placeholder expansion, or be mistaken for admin-authored structure. Email bodies render through
`LimitedMarkup` (paragraphs, headings, bullets, bold, https/site-relative links only); every other
channel is plain text. There is no templating library and no reflection-based property access
anywhere in the pipeline.
**Reason:** FR-NTF-2 needs data-defined wording; a cat's name or any other user-supplied value
reaching the renderer is exactly the injection surface a templating engine would open, and "parse
first, substitute second" closes it structurally rather than by escaping discipline alone.

---

## ADR-063 — Secret-Bearing Messages: Redact, Never Resend, Bounded Plaintext Window

**Date:** 2026-09-27 (HO-NTF-001)
**Status:** Active
**Decision:** A template that carries a secret (only `account.password-reset` today) gets no inbox
copy, can never be resent, is excluded from audit capture entirely (ADR-066), and is never logged
outside `Development`. Its body, subject and title are redacted to `"[redacted]"` as soon as the
notification reaches a terminal state (`Sent`, `Undeliverable`, `Expired`, `Cancelled`). The
request-level variables are a separate, independent mechanism: they are redacted **unconditionally**
once the request's own `NotAfter` (15 minutes) has passed -- the worker's sweep
(`GetRequestsDueForVariableRedactionAsync`, §12.2) does not check the notification's status first,
so the variables are cleared at `NotAfter` whether or not the notification already reached a
terminal state.

**Amended 2026-09-27 (gate fix).** `Redact` takes a `NotificationRedactionReason` (`Secret` |
`Anonymisation`), not a plain bool as first shipped, so a customer's later anonymisation can still
null `Destination`/`UnsubscribeLink` on a row a prior `Secret` redaction already touched --
idempotent per reason, on its own separate `AnonymisedOn` marker, never re-running a reason that
already applied. The reset link's own base URL is the shared `PublicSiteOptions.WebBaseUrl`
(ADR-061).
**Reason:** A reset link is a bearer credential -- HO-ACC-001 stores only its hash, never the
plaintext. Notification necessarily holds the plaintext link to send it at all; the residual window
(at most 15 minutes, at rest only, never over an API response -- SECURITY gate HIGH 1) must be a
recorded decision (OQ-3), not something a reader discovers by accident while debugging a leak.

---

## ADR-064 — Unsubscribe: Random Token, One-Click, Revokes Through the Account Aggregate

**Date:** 2026-09-27 (HO-NTF-001)
**Status:** Active
**Decision:** Each marketing-category notification (none shipped in v1's own catalogue; the
mechanism exists for later areas) carries a 256-bit random per-notification token; only its
SHA-256 hash is stored (`UnsubscribeTokenHash`), looked up on unsubscribe -- the token itself
cannot be enumerated or reversed. The unsubscribe page accepts the token with no sign-in, and also
answers an RFC 8058 `List-Unsubscribe-Post` one-click request from the mail client itself. Either
path revokes marketing consent for that one channel through the `Account` aggregate's own
preferences (`Source = "unsubscribe-link"`) -- the same aggregate ADR-058's routing policy already
reads consent from; `Notification` never owns consent state itself.
**Reason:** NFR-LEG-4 requires an unsubscribe mechanism; major mail providers require (and downgrade
sender reputation without) one-click support, and revoking through the `Account` aggregate rather
than a Notification-owned flag keeps consent in the one place ADR-058 already reads it from.

---

## ADR-065 — Care-Reminder Sweep: Hourly, Window-Based, Deduplicated by Owner

**Date:** 2026-09-27 (HO-NTF-001)
**Status:** Active
**Decision:** A second timed producer in the same worker (`CareReminderSweep`, run hourly) finds
every alive cat's next-due vaccination or deworming/combined treatment inside its configured
lead-time window (`NextDueOn - leadDays <= today <= NextDueOn`; overdue items get no reminder,
OQ-5) and requests one reminder per item, deduplicated by
`"care:{kind}:{catId:N}:{refId:N}:{ownerId:N}:{dueOn:yyyyMMdd}"`. Validity is re-checked by a
subject guard (`CatVaccinationDueGuard`/`CatDewormingDueGuard`) **at expansion and again
immediately before every delivery attempt** (§8.7), rather than by subscribing to a
`CatMarkedDeceasedEvent`/ownership-transfer event -- a cat that dies or changes hands between the
sweep and expansion, or between expansion and a later delivery attempt, is simply
suppressed/cancelled (`SubjectNoLongerApplies`), never sent.

**Amended 2026-09-27 (TEST gate fix, I-F5/A6).** The key was first shipped without `{ownerId:N}`.
After a cat's ownership transfer, the original owner's request is correctly suppressed by the
guard, but the identical key (a transfer changes neither the vaccination/treatment record nor its
`NextDueOn`) was then read as "already requested" for the NEW owner too -- silently and
permanently, until the underlying record next changed, which can be up to a year away for an
annual vaccine. Including the owner in the key means a transfer computes a genuinely different key,
so the new owner's next sweep is never blocked by the original owner's now-suppressed request.
**This does not close every gap:** a cat transferred A→B→A within the same due window computes
the SAME key for the second A-request as the original (now-suppressed) A-request already
consumed, so nothing is ever sent twice, but the original owner's reminder for that due date is
silently missed rather than re-requested -- a known, tracked residual, not fixed by this
amendment (`docs/backlog/NTF.md` #1).
**Reason:** A guard re-checked at send time avoids an event handler that would have to touch every
pending reminder for a cat whenever it dies or changes hands; the amendment records why the key
needed the owner, not just the subject, once ownership itself turned out to be part of "is this
still the right thing to send".

---

## ADR-066 — Audit Excludes the Notification Log; Template Edits and Resends Are Audited Normally

**Date:** 2026-09-27 (HO-NTF-001)
**Status:** Active
**Decision:** `notify.notification_request` and `notify.notification` are added to
`AuditCaptureExclusions` (data, not an inline `if`) -- excluded from
`AuditChangeCaptureInterceptor` entirely, because the notification log already IS the record of
what was sent, and it holds secrets (ADR-063) an audit trail must never duplicate. Template
customisation (`SaveNotificationTemplateCommand`/`ResetNotificationTemplateCommand`) is audited
normally, since it changes admin-authored wording, not delivery state. A resend writes an explicit
`BusinessEvent` audit row (`NotificationResent`) naming the new notification id and channel. An
unsubscribe is audited through the `Account` preferences change it produces (ADR-064), not as a
Notification-owned event.
**Reason:** Amends HO-ACC-001 §11.9 step 7's assumption that every aggregate is captured by
default; Notification is the first context that needs a standing exception, and it needs to be a
recorded one so the next area that also handles secrets knows the pattern exists.

---

## ADR-077 — `Product` Bounded Context

**Date:** 2026-09-28 (HO-SHP-001)
**Status:** Active
**Decision:** `Product` bounded context, namespace segment `Product`, schema `product`, owning
products, SKUs, prices, origins, stock and sales copies, reviews, wishlists and stock alerts. The
`Sku` feature name in `.ai/namespace-conventions.md` is **not used**.
**Reason:** Reviews, wishlists and alerts are not named in `.ai/bounded-contexts.md` (OQ-1); an
unused listed segment looks like an omission.

---

## ADR-078 — Category Taxonomy Is Code-Defined

**Date:** 2026-09-28 (HO-SHP-001)
**Status:** Active
**Decision:** Category taxonomy is code-defined, two levels, FR-SHP-1 verbatim, as enumerations
with a parent reference; subcategory optional.
**Reason:** US-SHP-01 AC3 names the set; a data-managed taxonomy lets the storefront drift from the
SRS.

---

## ADR-079 — SKUs Are Owned by `ProductAggregate`

**Date:** 2026-09-28 (HO-SHP-001)
**Status:** Active
**Decision:** SKUs are owned by `ProductAggregate` (≤ 100); a SKU id is stable, never reused and
never deleted (discontinued instead); SKU code and barcode unique across all products; GTIN-length
numeric barcodes check-digit validated.
**Reason:** Other contexts (Inventory, Orders, Pharmacy) hold SKU ids for years; POS and receiving
scan barcodes.

---

## ADR-080 — SKU Prices Are Append-Only, Effective-Dated Roots

**Date:** 2026-09-28 (HO-SHP-001)
**Status:** Active
**Decision:** SKU prices are append-only, effective-dated roots (HO-ADM-001 ADR-049 applied to
goods): no back-dating, > 0, in `commerce.currencyCode`; consumers snapshot through
`GetSkuSaleFactsQuery`; `commerce.pricesIncludeTax` tells them whether tax is inside.
**Reason:** A price column silently reprices history and makes FR-ORD-3's "price changed since you
added it" unanswerable.

---

## ADR-081 — Country of Origin and the Prescription Flag Live on the Product

**Date:** 2026-09-28 (HO-SHP-001)
**Status:** Active
**Decision:** Country of origin and the prescription flag live on the product, not the SKU — FR-SHP-4
and FR-SHP-11 say "product"; SRS §5.1 and `.ai/flows/*` say SKU. Origins are data
(`SourcingOriginAggregate`); an origin is listed when it has an active food product.
**Reason:** The SRS disagrees with itself; the choice decides the filter, the gate and the
revenue-by-origin report.

---

## ADR-082 — Shop Never Computes Stock or Sales Figures

**Date:** 2026-09-28 (HO-SHP-001)
**Status:** Active
**Decision:** Shop never computes stock or sales figures. Availability is read live from
`inventory.v_sellable_stock` (HO-IMP-001 ADR-116) — no copy, no push, no staleness (amended
2026-09-22, README C-1). Popularity remains `ProductSalesStatsAggregate`, a copy pushed by
HO-ORD-001 through `RecordProductSalesStatsCommand`: monotonic by `AsOf`, no HTTP route, never
editable, display only. Checkout still re-validates with Inventory.
**Reason:** The stock half no longer needs an ADR-012 exception, which is why it was dropped. The
sales half keeps one: HO-ORD-001 publishes no view, the owning context is the only writer, and no
decision depends on the number.

---

## ADR-083 — Catalogue Search Is PostgreSQL

**Date:** 2026-09-28 (HO-SHP-001)
**Status:** Active
**Decision:** Catalogue search is PostgreSQL: `pg_trgm` word-similarity and `ILIKE` over an
aggregate-maintained `search_text`, one query over the `product.storefront_catalogue_entry` view,
stable tie-breaks; the 500 ms p95 budget is proven by a 20,000-SKU performance test; no search
engine.
**Reason:** NFR-PRF-2 and NFR-CMP-3 (no proprietary service); a second store would be a projection.

---

## ADR-084 — Prescription Gating Reads Pharmacy Through `IDietAuthorisationGate`

**Date:** 2026-09-28 (HO-SHP-001)
**Status:** Active
**Decision:** Prescription gating reads Pharmacy through `IDietAuthorisationGate`; the shipped stub
denies everyone (fail-closed); `PrescriptionPurchaseGate` is pure; `CheckPrescriptionEligibilityQuery`
is the one contract HO-ORD-001 calls at add-to-cart and checkout; authorised cats must be alive and
still owned.
**Reason:** BR-2 must hold before Pharmacy exists; two call sites must not drift.

---

## ADR-085 — Bundles

**Date:** 2026-09-28 (HO-SHP-001)
**Status:** Active
**Decision:** Bundles are products of kind `Bundle` with one priced SKU and component SKUs;
availability is the minimum over components; components may not be bundles, prescription products
or discontinued; deduction is exploded by Orders/Inventory through `GetBundleCompositionQuery`.
**Reason:** FR-SHP-13 "priced as a unit while deducting each component" spans three contexts.

---

## ADR-086 — Reviews

**Date:** 2026-09-28 (HO-SHP-001)
**Status:** Active
**Decision:** Reviews: verified purchase through `IPurchaseHistoryReader` (HO-ORD-001; stub knows
no purchases), one per customer per product, plain text, published only after moderation, rating
summary as a view.
**Reason:** FR-SHP-10; a stored average drifts.

---

## ADR-087 — Wishlist Per SKU and Back-In-Stock Alerts

**Date:** 2026-09-28 (HO-SHP-001)
**Status:** Active
**Decision:** Wishlist per SKU and back-in-stock alerts notified exactly once by a worker pass that
dispatches one command per alert (enqueue in the command handler); the alert response states
deliverability computed by HO-NTF-001's own routing policy; template `shop.back-in-stock` is
**Marketing** per HO-NTF-001 §4.2.
**Reason:** FR-SHP-14; F33 means some customers will not be told — the API must say so rather than
hide it.

---

## ADR-088 — Cross-Sell and Up-Sell Are Curated

**Date:** 2026-09-28 (HO-SHP-001)
**Status:** Active
**Decision:** Cross-sell and up-sell are curated by the shop manager; the life-stage block is
computed; every block excludes out-of-stock items and prescription products the caller cannot buy.
**Reason:** No order history exists to compute "frequently bought together" (OQ-7).

---

## ADR-089 — Food Recommendation Is a Pure Policy

**Date:** 2026-09-28 (HO-SHP-001)
**Status:** Active
**Decision:** Food recommendation is a pure policy over cat facts and candidates: allergen
token-match exclusion, life stage from date of birth with configurable thresholds, reason codes, no
prescription product without this cat's authorisation.
**Reason:** Clinical-adjacent logic must be explainable and testable; thresholds await clinical
sign-off (OQ-9).

---

## ADR-090 — Product Content Localisation

**Date:** 2026-09-28 (HO-SHP-001)
**Status:** Active
**Decision:** Product content localisation: owned translations per enabled locale from
`Localisation:ContentLocales`, per-field fallback, `fieldLocales` returned; UI chrome is out of
scope.
**Reason:** C-8 / FR-SHP-15; SRS Q-12 open.

---

## ADR-091 — Images Are Storage Key Plus Metadata

**Date:** 2026-09-28 (HO-SHP-001)
**Status:** Active
**Decision:** Images are storage key + metadata (HO-CAT-001's approach); public URL from
`Media:PublicBaseUrl`, else null; alt text required.
**Reason:** NFR-SEC-5 upload path is not provisioned; UI-6.
**Amended 2026-09-23 (README C-5):** Product images now go through HO-FIL-001. This slice
contributes the `ProductImage` purpose and its `FileRules` row and stores `fileId` values.
`Media:PublicBaseUrl` and the placeholder-for-missing-storage path are withdrawn — images are
served through HO-FIL-001's signed URLs like every other file.

---

## ADR-092 — Contract Queries Are the Only Way Other Contexts Read Catalogue Facts

**Date:** 2026-09-28 (HO-SHP-001)
**Status:** Active
**Decision:** Contract queries (`GetSkuSaleFactsQuery`, `GetSkuCatalogueFactsQuery`,
`FindSkuByBarcodeQuery`, `CheckPrescriptionEligibilityQuery`, `GetBundleCompositionQuery`) are the
only way other contexts read catalogue facts in their command handlers; they return facts, never
decisions other than the gate's.
**Reason:** Keeps Orders, Inventory and Pharmacy off `product.*` tables in write paths.

---

## ADR-093 — Storefront Endpoints Are `[StorefrontEndpoint]`

**Date:** 2026-09-28 (HO-SHP-001)
**Status:** Active
**Decision:** Storefront endpoints are `[StorefrontEndpoint]` (public reads and customer shop
writes) and answer `503` in maintenance mode; staff endpoints never; anonymous catalogue reads are
`Cache-Control: public, max-age=60`, personal reads `no-store`; a `catalogue` rate limit protects
search.
**Reason:** HO-ADM-001 ADR-053's first consumer; NFR-SCL-2 wants cacheable catalogue traffic.

---

## ADR-094 — `ListedOnStorefront`

**Date:** 2026-09-28 (HO-SHP-001)
**Status:** Active
**Decision:** `ListedOnStorefront` lets a product be an active, stockable SKU that is never shown
or sold online — for clinic consumables and dispensed items that HO-EMR-001 / HO-PHM-001 deduct.
**Reason:** The delivery order exists because clinical areas deduct batches of catalogue SKUs
(README); without the flag those items would appear in the shop (OQ-5).

---

## ADR-095 — Product Lifecycle

**Date:** 2026-09-28 (HO-SHP-001)
**Status:** Active
**Decision:** Product lifecycle: `Draft → Active → Archived`, `Active → Draft`, `Archived →
Active`; never deleted; slug locked after first publication; category locked while active;
publication rules are one pure checklist shared by the command and the staff read.
**Reason:** SEO links and order history must not break; the screen must show what the server
enforces.

---

## ADR-096 — Customer-Data Obligations

**Date:** 2026-09-28 (HO-SHP-001)
**Status:** Active
**Decision:** Customer-data obligations: `CustomerMergedEvent` moves wishlist items (union), alerts
and reviews to the survivor idempotently, closing duplicates on collision;
`CustomerAccountAnonymisedEvent` clears the wishlist and closes pending alerts; published reviews
stay, credited to "A verified buyer".
**Reason:** HO-ACC-001 ADR-039 / ADR-041 bind every context holding a customer id.

## ADR-067 — Clinic Bounded Context and the Two Pre-Booking States

**Date:** 2026-09-27 (HO-APT-001)
**Status:** Active
**Decision:** **Clinic** bounded context, schema `clinic`, shared with HO-EMR-001. Appointment
statuses are Appendix C's plus two **pre-booking** states, `Held` and `HoldExpired`/`HoldReleased`,
which never appear as appointments.
**Reason:** Not in `.ai/bounded-contexts.md` (OQ-1); the extra states look like a deviation from
Appendix C.

---

## ADR-068 — Double Booking Is Prevented by PostgreSQL Exclusion Constraints, Not an Application Lock

**Date:** 2026-09-27 (HO-APT-001)
**Status:** Active
**Decision:** **Double booking is prevented by three PostgreSQL exclusion constraints** (performer,
room, cat) over `tstzrange(starts_on, ends_on, '[)')` for occupying statuses, using `btree_gist`.
`23P01` → `409 SlotNoLongerAvailable`. No application lock.
**Reason:** FR-CLN-3 "prevented by a transactional hold"; a read-then-write check has a race; the
constraint has none (F28).

---

## ADR-069 — Slots Are Computed, Never Stored

**Date:** 2026-09-27 (HO-APT-001)
**Status:** Active
**Decision:** **Slots are computed** by the pure `AppointmentSlotFinder` from HO-ADM-001's calendar
and availability, service, rooms and bookings, on a branch-local grid; no slot table.
**Reason:** `.ai/bounded-contexts.md` rule 4; a stored slot table must be regenerated on every
roster, leave, closure or service change.

---

## ADR-070 — Holds: One Per Customer, Released Lazily

**Date:** 2026-09-27 (HO-APT-001)
**Status:** Active
**Decision:** **Holds** are `Held` appointments, 10 min, **one per customer**; stale holds are
released lazily by the next write that overlaps them, in the same save — a recorded rule-1
exception.
**Reason:** Without one-per-customer a script can hold a morning; without lazy release a stale hold
blocks a slot shown as free.

**Amended 2026-09-28 (owner decision, ARCHITECTURE gate second pass).** A stale hold's expiry no
longer commits "in the same save" as the caller's own claim. `SlotClaim.ExpireOverlappingStaleHoldsAsync`
commits each lapsed hold's expiry first, in its own save, because EF Core does not order two
`Modified` rows of one table by which was mutated first in C#, and Postgres checks an exclusion
constraint per statement -- without this, the caller's own claim could still 23P01 against a hold
whose expiry had not yet reached the database. Expiring a hold that has, in fact, lapsed is
idempotent and harmless whatever the caller does next, so committing it early is safe even though
it is no longer atomic with the caller's own write. Rule: no caller may have pending tracked
changes on its `DbContext` before calling the helper -- a caller that mutated first would have half
its own unit of work committed early by the helper's save.
**Decision:** Owner, 2026-09-28.

---

## ADR-071 — The Queue Is a Read; the Estimated Wait Is Recomputed, Never Stored

**Date:** 2026-09-27 (HO-APT-001)
**Status:** Active
**Decision:** **The queue is a read**, ordered by triage then due time; the estimated wait is
computed on every read from durations and performers on shift, and labelled an estimate.
**Reason:** A stored wait goes stale the moment an emergency arrives.

---

## ADR-072 — Staff Bookings and Moves Skip the Hold

**Date:** 2026-09-27 (HO-APT-001)
**Status:** Active
**Decision:** **Staff bookings and moves skip the hold** and are validated by the same slot rules,
except the online-only rules (notice, horizon, opening hours, `IsBookableOnline`); the same
constraints protect them.
**Reason:** Reception on the phone should not wait on a countdown; an after-hours emergency needs a
shift, not opening hours.

---

## ADR-073 — No Deposit or Payment at Booking

**Date:** 2026-09-27 (HO-APT-001)
**Status:** Active
**Decision:** **No deposit, no payment** at booking; the appointment carries a `PriceSnapshot` for
Billing.
**Reason:** Owner instruction; the snapshot keeps HO-ADM-001 ADR-049's promise.

---

## ADR-074 — Appointment Messages Ride the Booking; Reminders Use SendAfter/SubjectStamp

**Date:** 2026-09-27 (HO-APT-001)
**Status:** Active
**Decision:** **Appointment messages** are enqueued in the command handler with the change;
reminders use HO-NTF-001's new `SendAfter` and `SubjectStamp`, so a moved or cancelled
appointment's reminders fail their guard. Confirmation, reschedule and cancellation notices are
`Urgent`.
**Reason:** FR-CLN-7 "immediately" against FR-NTF-5 quiet hours — the recipient is the one acting.

---

## ADR-075 — Waitlist Offers: Worker Pass, Outside Quiet Hours, Oldest First

**Date:** 2026-09-27 (HO-APT-001)
**Status:** Active
**Decision:** **Waitlist** offers are made by a worker pass outside quiet hours, oldest entry
first; an offer is a hold with the claim window; one save for hold + entry + message — a recorded
rule-1 exception.
**Reason:** Offering from the cancel handler would couple cancellation to waitlist logic and could
not respect quiet hours.

---

## ADR-076 — Flagging, Not Cancelling

**Date:** 2026-09-27 (HO-APT-001)
**Status:** Active
**Decision:** **Flagging, not cancelling**: leave, closure, death and ownership events flag
affected future appointments; handlers are idempotent and never enqueue.
**Reason:** ADR-048's contract; an automatic cancellation would message a customer about a decision
nobody made.

---

## HO-SHP-001 build amendments — 2026-10-04

Dated, append-only notes on ADR-077…096 recorded while building Phases F–H. The detail and the
reasons are in `docs/handover/HO-SHP-001.md` §1 "Amended" (rows dated 2026-10-04).

- **ADR-084 (prescription gate).** The port Shop consumes is HO-PHM-001's `IDietAuthorisationGate`
  (`CheckAsync` / `GetAuthorisedCatsAsync`, ADR-149), registered by `PharmacyModule`, not the
  `GetValidAsync` draft in §8.9. Expiry, liveness and ownership are the gate's rules; Shop's pure
  `PrescriptionPurchaseGate` keeps the guest rule and the outcome-to-reason mapping.
  `CheckPrescriptionEligibilityQuery` has no `AsOf` — the gate evaluates "today" on every call.
- **ADR-082 (2026-10-04, integrator recommendation, confirmed by the owner 2026-10-04).** Production availability is FAIL-CLOSED per §4.3: `Shop:InterimAvailabilityFromPrice` (default false) gates the interim price-based approximation, which only the Testing environment enables.
- **ADR-082 / ADR-085 (availability, bundles).** Availability for every storefront read, the
  stock-alert handlers and the worker is answered at one seam (`SkuSellability` behind
  `ISkuAvailabilityReader`), the Phase E approximation until HO-IMP-001's `inventory.v_sellable_stock`
  exists. A bundle is available only when its own SKU and every component is.
- **ADR-087 (wishlist and alerts).** `StockAlertGuard` applies exactly §8.11's rule with no owner
  check; the merge handler writes one audit row with the counts it moved, dropped, closed or withdrew.
- **ADR-090 (localisation).** Enabled locales reach command handlers through the Domain port
  `IContentLocaleSettings` (Application cannot reference Infrastructure's options). `?locale=`
  applies to every public read that shows a name.
- **ADR-089 (food recommendation).** A product with no reason is not a recommendation; the allergy
  count covers only products that would otherwise have been recommended.

---
