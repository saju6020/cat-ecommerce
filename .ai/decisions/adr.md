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

## HO-EMR-001 — Consultation and Medical Records (ADR-117 … ADR-134)

Recorded word-for-word from `docs/handover/HO-EMR-001.md` §9 (the handover owns ADR-117…136 and uses 117…134). Dated 2026-10-07 at build; the decisions were taken at handover (2026-09-22).

## ADR-117 — Visit roots live in the Clinic context

**Date:** 2026-09-22 (HO-EMR-001), recorded 2026-10-07
**Status:** Active
**Decision:** **Visit roots** live in the `Clinic` context (schema `clinic`, HO-APT-001 ADR-067) under namespace segment **`Visit`** (a listed feature name). **One visit per appointment**, opened by an explicit command while the appointment is `CheckedIn` or `InConsultation`; the visit holds no customer id
**Reason:** `.ai/bounded-contexts.md` lists `Visit` as its own context; HO-APT-001 put appointments in `Clinic`. Recording the split stops a third naming appearing

---

## ADR-118 — A finalised visit is frozen at three layers

**Date:** 2026-09-22 (HO-EMR-001), recorded 2026-10-07
**Status:** Active
**Decision:** **A finalised visit is frozen at three layers** — `EnsureOpen()` in the aggregate, no API path, PostgreSQL triggers on the visit and its child tables; addenda, result sets, attachments and stored files are append-only by trigger; no clinical row is ever deleted
**Reason:** BR-11, D-5, C-3; the same database-authoritative standard as HO-ACC-001's audit triggers

**Amended 2026-10-07 (HO-EMR-001 integrator post-merge; ARCHITECTURE gate deviation 3, SECURITY M1):** "stored files are append-only by trigger" is **not what was built**. `files.stored_file` belongs to HO-FIL-001 (README C-5, ADR-125's narrowing), which legitimately redacts rows and purges unreferenced files, so the table is **not** append-only. HO-EMR-001 instead installed a **delete guard**, `clinic.stored_file_referenced` (migration `AddDiagnostics`, replaced by `AddConsents`): a `BEFORE DELETE` row trigger that raises `SQLSTATE 'CR001'` when the file is referenced by `clinic.visit_photo`, `clinic.diagnostic_attachment` or `clinic.consent_record`. An unreferenced file stays deletable; a referenced file's **row** cannot be deleted. Everything else in this ADR stands as written (visit and child-table freezing, append-only addenda, result sets and attachments, no clinical row ever deleted). **Known gap, owner decision pending (`docs/backlog/EMR.md` row 1):** the guard protects the row, not the content. HO-FIL-001's `StoredFileEraser` deletes the stored object *before* the row delete that fires the trigger, and a `StoredFilePurgeSource` is registered for every `FilePurpose`, so a purge of an attached lab report, photo or signed consent form would destroy the object and then be refused on the row, leaving a clinical record pointing at nothing. Latent until HO-XCT-001 wires `IRetentionPurgeSource`; which area closes it is undecided.

**Amended 2026-10-09 (integrator, migration `HardenClinicalAndBillingTriggers`; `docs/backlog/EMR.md` rows 4–5):** the freeze is stricter than the text above. A child row's `visit_id` **never changes** (`clinic.visit_child_frozen` refuses any change, open or finalised parent), and the soft-delete flag `IsMarkedToDelete` is **refused outright** on `clinic.visit`, `clinic.consent_record` and `clinic.diagnostic_order` — setting it would hide the record from every read, i.e. delete it. `clinic.diagnostic_order` also freezes `cat_id`, `visit_id` and `branch_id`. All raise `SQLSTATE 'CR001'`. No application path does any of these.

---

## ADR-119 — Finalising is Vet-only

**Date:** 2026-09-22 (HO-EMR-001), recorded 2026-10-07
**Status:** Active
**Decision:** **Finalising is `Vet`-only** — `Visits.Finalise` maps to `Vet` alone (Administrator excluded, per Appendix D "Clinical notes: Admin R"), and `VisitAggregate.Finalise` re-checks the caller's roles
**Reason:** BR-6, FR-CLN-24, AC-18; an "Administrator can do everything" assumption would violate the SRS

---

## ADR-120 — Vital-sign ranges are two-tier and server-authoritative

**Date:** 2026-09-22 (HO-EMR-001), recorded 2026-10-07
**Status:** Active
**Decision:** **Vital-sign ranges are two-tier and server-authoritative**: outside *absolute* is refused; outside *plausible* is refused until the request confirms those signs, and the confirmation is stored
**Reason:** US-CLN-11 AC2 "warned but may confirm"; a client-only warning is not evidence

---

## ADR-121 — Vitals with a weight create the weight record in the same commit

**Date:** 2026-09-22 (HO-EMR-001), recorded 2026-10-07
**Status:** Active
**Decision:** **Vitals with a weight create the cat's weight record in the same commit**, sharing the vitals entry's id — a recorded rule-1 exception using the F23 companion commit
**Reason:** Weight history must never disagree with the vitals that produced it; an event handler would fail after commit

---

## ADR-122 — Auto-save is a per-author working copy

**Date:** 2026-09-22 (HO-EMR-001), recorded 2026-10-07
**Status:** Active
**Decision:** **Auto-save is a per-author working copy** (`ConsultationDraftAggregate`), excluded from audit capture, returned only to its author when newer than the saved note; the note on the visit changes only on explicit save
**Reason:** US-CLN-12 AC2; auto-saving into the record would flood the audit trail with keystroke-level rows

---

## ADR-123 — Coded diagnoses from an in-house catalogue

**Date:** 2026-09-22 (HO-EMR-001), recorded 2026-10-07
**Status:** Active
**Decision:** **Coded diagnoses** come from an in-house catalogue (`DiagnosisCodeAggregate`) with optional `ExternalSystem`/`ExternalCode` for a later mapping (VeNom or SNOMED, OQ-2); problem code and name are snapshotted; one system code `SYS-NAD` ("Routine visit — no abnormality detected") is seeded; finalising needs ≥ 1 problem and non-blank Assessment and Plan
**Reason:** FR-CLN-13 "coded"; `.ai/flows/clinic-visit-flow.md` "cannot be finalised without at least one diagnosis" — the system code keeps well visits finalisable

---

## ADR-124 — Diagnostics are their own root

**Date:** 2026-09-22 (HO-EMR-001), recorded 2026-10-07
**Status:** Active
**Decision:** **Diagnostics are their own root**; results are append-only result sets with superseding links; flags come from a pure classifier against the range stored on the value; results and files may arrive after finalisation
**Reason:** Lab reports arrive days later; a correction must not overwrite what a vet acted on

---

## ADR-125 — Stored-file facility (narrowed to HO-FIL-001)

**Date:** 2026-09-22 (HO-EMR-001), recorded 2026-10-07
**Status:** Active
**Decision:** **Stored-file facility** (`files` schema, `StoredFile` segment, shared with later areas): content-sniffed type validation, size per purpose, **fail-closed** malware scan port, local store outside the web root, HMAC-signed 5-minute URLs, hardened response headers. If HO-IMP-001 ships an equivalent first, this slice consumes it (§20.3)
**Reason:** NFR-SEC-5 / US-XCT-07 applied for the first time a slice truly stores files; no scanner chosen yet

**Narrowed 2026-09-23 (README C-5, HO-EMR-001 §1 Amended):** the stored-file facility, `files` schema and `StoredFile` namespace segment are owned by HO-FIL-001 (ADR-318 storage, ADR-319 ClamAV scanner); this handover consumes `IFileStore` / `IMalwareScanner` / `IFileUrlSigner` and keeps only its three clinical `FilePurpose` values and their `FileRules` rows. The §20.3 "if HO-IMP-001 ships an equivalent first" branch is superseded by that extraction.

---

## ADR-126 — Treatments move stock in the same commit

**Date:** 2026-09-22 (HO-EMR-001), recorded 2026-10-07
**Status:** Active
**Decision:** **Treatments move stock through Inventory in the same commit** (rule-1 exception, companion commit); FEFO by default, override with reason; **expired is refused with no override**; shortfall recorded and published; mistakes reversed by a compensating entry; **controlled-drug SKUs are refused** until the register exists (HO-SRG/HO-PHM)
**Reason:** BR-3, BR-4, FR-IMP-15, C-2; an after-commit handler could record a treatment whose stock never moved

---

## ADR-127 — Charges are snapshotted on treatment lines

**Date:** 2026-09-22 (HO-EMR-001), recorded 2026-10-07
**Status:** Active
**Decision:** **Charges are snapshotted on treatment lines** (service or SKU price at recording, tax class); Billing receives `VisitFinalisedEvent` and reads `GetVisitChargeLinesQuery`; Clinic never writes `billing.*`
**Reason:** HO-ADM-001 ADR-049's contract; `.ai/flows/clinic-visit-flow.md` cross-module rule

---

## ADR-128 — Clinical history is composed from section providers

**Date:** 2026-09-22 (HO-EMR-001), recorded 2026-10-07
**Status:** Active
**Decision:** **Clinical history is composed from section providers**; unbuilt sections report `available: false` with their owner; server budget 600 ms / 300 ms p95 on a 10-year fixture
**Reason:** Later areas add panels without editing this slice; NFR-PRF-4 must be measured, not assumed

---

## ADR-129 — Every clinical-record read is logged before it runs

**Date:** 2026-09-22 (HO-EMR-001), recorded 2026-10-07
**Status:** Active
**Decision:** **Every clinical-record read is logged before it runs**, by a filter dispatching a command, new `AuditCategory.ClinicalRecordAccess`; a failed log fails the request
**Reason:** NFR-SEC-8 "every access … shall be logged"; a query never writes, so logging cannot live in the query

---

## ADR-130 — The discharge summary is derived, not stored

**Date:** 2026-09-22 (HO-EMR-001), recorded 2026-10-07
**Status:** Active
**Decision:** **The discharge summary is derived, not stored**; finalised visits only; PDF via QuestPDF; the owner gets a link-only message
**Reason:** A stored copy could disagree with the immutable record and its addenda

---

## ADR-131 — Follow-up is an appointment link or a guarded reminder

**Date:** 2026-09-22 (HO-EMR-001), recorded 2026-10-07
**Status:** Active
**Decision:** **Follow-up** is a link to an appointment booked through HO-APT-001, or a reminder enqueued with `SendAfter`, subject `VisitFollowUp` and a stamp guard that also stands down once the cat has a booked appointment
**Reason:** US-CLN-18; a reminder for a follow-up that changed or was already booked must never send

---

## ADR-132 — Consent is its own root; signatures are stroke data

**Date:** 2026-09-22 (HO-EMR-001), recorded 2026-10-07
**Status:** Active
**Decision:** **Consent** is its own root, linked to cat, appointment and kind, recording the exact consent-form policy version; on-screen signatures are **stroke data rendered to SVG**, not uploaded images; `ConsentRules.IsValidFor` is Surgery's contract
**Reason:** NFR-LEG-4, US-CLN-21; stroke data needs no scanner, so signing works where uploads are refused

---

## ADR-133 — Amendment to HO-APT-001: completion and cancel guards

**Date:** 2026-09-22 (HO-EMR-001), recorded 2026-10-07
**Status:** Active
**Decision:** **Amendment to HO-APT-001**: `CompleteAppointment` is refused while the appointment's visit is `Open` (`VisitNotFinalised`); staff `Cancel` of a `CheckedIn` appointment is refused once a visit exists (`VisitInProgress`). Finalising does not complete the appointment
**Reason:** HO-APT-001 §4.2 reserved this invariant for this slice; auto-completing from an event handler would fail after commit

---

## ADR-134 — Note templates are one active template per service

**Date:** 2026-09-22 (HO-EMR-001), recorded 2026-10-07
**Status:** Active
**Decision:** **Note templates** are one active template per service, versioned, delivered in the visit payload for the client to pre-fill; the applied id and version are recorded on the note
**Reason:** US-CLN-20 "remains fully editable"; server-side copying would blur what the vet wrote

---

## ADR-097 — `Inventory` Bounded Context

**Date:** 2026-10-07 (HO-IMP-001)
**Status:** Active
**Decision:** **`Inventory` bounded context**, schema `inventory`, owning suppliers, POs, consignments, receipts, batches, the ledger and stock control — folding `.ai/bounded-contexts.md`'s `Supplier`, `Consignment` and `Batch` rows into one context
**Reason:** Deviates from the doc's table (OQ-2); one transaction boundary at receipt and FKs within one module (ADR-011)

---

## ADR-098 — Ledger and Reservation Entries Are Append-Only and Immutable

**Date:** 2026-10-07 (HO-IMP-001)
**Status:** Active
**Decision:** **The stock ledger and reservation entries are append-only and immutable** in the application (no mutation methods) and the database (triggers refuse `UPDATE`/`DELETE`/`TRUNCATE`); they are excluded from HO-ACC-001 change capture because each row is already an attributable record
**Reason:** FR-IMP-15 AC3; D-3; auditing every ledger row twice doubles the busiest table

---

## ADR-099 — Current Stock Is a Trigger-Maintained Projection Guarded by a CHECK

**Date:** 2026-10-07 (HO-IMP-001)
**Status:** Active
**Decision:** **Current stock is `stock_position`, a projection maintained only by ledger/reservation triggers**, guarded against direct writes; **`ck_stock_position_balance` inside the trigger's row lock is the control against overselling and negative stock**; `23514` → `409 InsufficientStock`. No application or advisory lock
**Reason:** US-IMP-08 AC2 and D-3 together: stock must be derived *and* the last unit must have one winner. A read-then-write check races; a `SUM` at read time cannot enforce anything (FI-1)

---

## ADR-100 — Reservations Are Entries, Not Ledger Movements

**Date:** 2026-10-07 (HO-IMP-001)
**Status:** Active
**Decision:** **Reservations are reservation entries, not ledger movements**; a consuming `Sale` decrements on-hand and reserved in **one statement**; remaining reservations are derived; **sellable** = on-hand − reserved over `Available`, unexpired batches in retail-selling locations
**Reason:** FR-IMP-8; putting reserved in a second statement would let the `CHECK` fail spuriously depending on EF's statement order

---

## ADR-101 — "Expired" Is Derived, Never Stored, and Cannot Be Bypassed

**Date:** 2026-10-07 (HO-IMP-001)
**Status:** Active
**Decision:** **"Expired" is derived** (`branchLocalToday > ExpiresOn`), never stored; enforced in the application and by `tg_stock_batch_usable` on every consuming movement and reservation; **no parameter, role or setting can bypass it**. Appendix C's `in_transit`/`received`/`expired` mapped as in §8.6.1
**Reason:** BR-3 "no override anywhere"; a stored status needs a job that can fail; Appendix C deviation must be visible

---

## ADR-102 — FEFO Is the Pure `FefoAllocator`

**Date:** 2026-10-07 (HO-IMP-001)
**Status:** Active
**Decision:** **FEFO is the pure `FefoAllocator`**: expiry ascending, nulls last, then received, then location priority; an explicit plan that differs from FEFO requires a reason, flags the entries and enlists a `BusinessEvent` audit entry in the same commit (a recorded rule-1 exception)
**Reason:** BR-4, US-IMP-07 AC2; `.ai/bounded-contexts.md` rule 4

---

## ADR-103 — `IStockLedger` Is the Only Way Any Context Moves Stock

**Date:** 2026-10-07 (HO-IMP-001)
**Status:** Active
**Decision:** **`IStockLedger` is the only way any context moves stock**; it enlists rows without saving (HO-NTF-001's outbox pattern) so the consumer's aggregate and the movement commit together — a recorded rule-1 exception for every stock-moving command; consumers must stamp reference, customer and cat
**Reason:** "Inventory is the only module that allocates or deducts stock" (`.ai/flows/import-to-shelf-flow.md`); designing it now avoids building clinical deduction twice (README)

---

## ADR-104 — Recall Is One Row Update; the Trace Is a Ledger Read

**Date:** 2026-10-07 (HO-IMP-001)
**Status:** Active
**Decision:** **Recall** = one batch-row status change (atomic across locations, serialised against consumption by `FOR SHARE`); the **trace is a ledger read** with customers resolved through merges and cats through current ownership at read time — no `CustomerMergedEvent` handler rewrites immutable rows; notices go one per distinct customer with a dedup key and a subject guard. **Replaces** `.ai/flows/batch-recall-flow.md`'s per-module `IBatchConsumptionQuery`
**Reason:** Fan-out needs every future module to remember a contract; the stamp is enforced by the port at write time. The merge-contract deviation must be judged by ARCHITECTURE, not discovered

---

## ADR-105 — Goods Receipt Is Posted in One Command

**Date:** 2026-10-07 (HO-IMP-001)
**Status:** Active
**Decision:** **Goods receipt is posted in one command** that creates the receipt, one batch per accepted line (enlisted) and their ledger entries, in one commit (a recorded rule-1 exception); receipts are immutable; corrections by adjustment
**Reason:** A receipt half-posted is stock without a batch or a batch without stock

---

## ADR-106 — Landed Cost Allocation and `UnitCost`

**Date:** 2026-10-07 (HO-IMP-001)
**Status:** Active
**Decision:** **Landed cost** is allocated by accepted quantity, in base currency using manual exchange rates on the consignment, with largest-remainder rounding at 2 dp; landed unit cost is a 4-dp `UnitCost`, not `Money`; stored on the consignment only; valuation and margin use landed ?? purchase via `v_batch_unit_cost`
**Reason:** FR-IMP-5; the flow doc left the basis open (OQ-3); 4 dp would break ADR-054's `Money` if not a separate type

---

## ADR-107 — Adjustments Need a Reason and a Note; Approval Above the Threshold

**Date:** 2026-10-07 (HO-IMP-001)
**Status:** Active
**Decision:** **Adjustments** need a reason category **and** a note; above `inventory.adjustmentApprovalThreshold` they wait for a different approver holding `Stock.ApproveAdjustment` before any ledger entry is written; counts use the same threshold
**Reason:** FR-IMP-13; a count must not be a way round adjustment approval

---

## ADR-108 — Counts Are Blind; Variances Are Frozen at Submission

**Date:** 2026-10-07 (HO-IMP-001)
**Status:** Active
**Decision:** **Counts are blind; variances are frozen at submission; commit posts exactly the reported variance** (not "set to counted")
**Reason:** US-IMP-14 AC1/AC2; sales during counting are real movements

---

## ADR-109 — Stock Locations and the Fixed Movement Table

**Date:** 2026-10-07 (HO-IMP-001)
**Status:** Active
**Decision:** **Stock locations** are an Inventory root per branch with a fixed kind → permitted-movement table in `Shared`; one quarantine location per branch; five seeded for `MAIN`
**Reason:** FR-IMP-6's four locations plus FR-ORD-16's quarantine; a fixed table is testable exhaustively (OQ-5)

---

## ADR-110 — Stock Alerts Are Rows Raised by `InventoryWorker` Timed Passes

**Date:** 2026-10-07 (HO-IMP-001)
**Status:** Active
**Decision:** **Alerts are `StockAlertAggregate` rows** raised by `InventoryWorker` timed passes with dedup keys; expiry thresholds raise only the tightest crossed; no staff message in v1
**Reason:** FR-IMP-10/12; HO-NTF-001 gives staff no inbox and no provider exists

---

## ADR-111 — SKUs Come From HO-SHP-001 Through `ISkuDirectory`

**Date:** 2026-10-07 (HO-IMP-001)
**Status:** Active
**Decision:** **SKUs come from HO-SHP-001 through `ISkuDirectory`** and one adapter; quantities are `numeric(12,3)` in the SKU's stock unit, whole unless the SKU allows fractions
**Reason:** Written in parallel with HO-SHP-001; clinical use needs fractions (ml), retail does not

---

## ADR-112 — PO Receipt Progress Is Derived

**Date:** 2026-10-07 (HO-IMP-001)
**Status:** Active
**Decision:** **PO receipt progress is derived** from posted receipts; outstanding = ordered − accepted
**Reason:** Rule 1 (receipt must not also edit the PO); a stored counter drifts

---

## ADR-113 — Consignment Documents Are Metadata Plus Storage Key

**Date:** 2026-10-07 (HO-IMP-001)
**Status:** Active
**Decision:** **Consignment documents are metadata + storage key**, never removed (superseded instead)
**Reason:** HO-CAT-001's upload pattern; NFR-LEG-2 retention

---

## ADR-114 — Document Numbers Come From PostgreSQL Sequences

**Date:** 2026-10-07 (HO-IMP-001)
**Status:** Active
**Decision:** **Document numbers** (`PO-`, `CN-`, `GRN-`, `TRF-`, `ADJ-`, `CNT-`, `RCL-`) come from PostgreSQL sequences, `{prefix}-{yyyy}-{seq:D6}`, not gapless
**Reason:** Human references on paperwork; gaplessness is an invoice requirement (FR-BIL-1), not a stock one

---

## ADR-115 — Storage-Breach Quarantine Is Location-Level

**Date:** 2026-10-07 (HO-IMP-001)
**Status:** Active
**Decision:** **Storage-breach quarantine is location-level** (stock transferred to the branch's quarantine location), distinct from batch-level `Quarantined`
**Reason:** A breach affects the stock *there*, not the same batch elsewhere

---

## ADR-116 — `inventory.v_sellable_stock` and `inventory.v_batch_unit_cost` Are Published Read Contracts

**Date:** 2026-10-07 (HO-IMP-001)
**Status:** Active
**Decision:** **`inventory.v_sellable_stock`** (per SKU per branch: sellable quantity and the FEFO-first expiry) and **`inventory.v_batch_unit_cost`** are the published read contracts for HO-SHP, HO-ORD and HO-RPT
**Reason:** NFR-PRF-2 catalogue filtering needs a join, not a ledger sum per request

---

## HO-IMP-001 build amendments (2026-10-07)

Deviations and clarifications found while building HO-IMP-001. They refine ADR-097 to ADR-116; none reverses one. The integrator folds them into `docs/handover/HO-IMP-001.md`.

- **Alert aggregate is `InventoryAlertAggregate`** (DbSet `InventoryAlerts`), not `StockAlertAggregate`: that name is Shop's back-in-stock alert. Table `inventory.stock_alert`.
- **`inventory.batch-recall` template variable `expiresOn` is `Text`** (`yyyy-MM-dd` or "no expiry date"), not `Date`: an undated batch has no date to render.
- **`StockReferenceKind.ControlledDrugCorrection` exists** for HO-PHM-001's `IStockLedger.AdjustAsync`.
- **FI-1** (the literal DB-fact shape in section 16) is false as written; an equivalent shape is documented and verified in `InventoryDatabaseFactsTests`.
- **`MovementKind` has 13 members**; ledger dates are UTC instants and the ledger search takes inclusive UTC days.
- **`IStockPositionReader` gained reader methods** beyond section 12.2's list (locations, batch facts, batch positions, receipts per PO line, consignment batches) -- all read-only.
- **A transfer has no pre-check** that the stock is there: the projection's CHECK is the control (ADR-099) and surfaces as `409 InsufficientStock`.
- **`IStockLedger.RecordShortfall` makes one synchronous primary-key lookup** (the location's branch): the published port is `void`, so it cannot await. It enlists a `Shortfall` alert and never fails the caller.
- **The fail-closed `FakeStockLedger` is retained** in `Infrastructure/Fakes/Inventory`; the real `EfStockLedger` replaces its registration.
- **Landed cost** is written to `consignment_landed_cost_line` only; `inventory.v_batch_unit_cost` is `COALESCE(landed, purchase)` and `EfStockPositionReader.GetUnitCostsAsync` reads it. Staleness is `CostsChangedSinceAllocation` OR the received-batch set differing from the allocated set.
- **Low stock excludes `Quarantine`-kind locations** (they never hold usable stock, so they would alert forever); a pair is every position row plus every pair that has a policy.
- **The worker cycle accepts an optional branch** (`RunAsync(ct, onlyBranchId)`) so a test on the shared database scans its own branch; the daily reconciliation runs once per 24 h of process uptime (`InventoryWorkerState`).
- **`IInventoryAlertEnlister`** (same shape as `IBatchRegistry`) lets a breach report and its alert commit together.
- **A storage-breach quarantine moves the FREE stock** (`on_hand - reserved`) of each affected batch; stock reserved for an order stays put.
- **`RecordRecallTraceExportCommand`** (not in section 11.1) writes the `RecallTraceExported` `BusinessEvent`; the trace CSV is refused if that audit write fails.
- **Reorder suggestions** are served at `/api/staff/procurement/reorder-suggestions` (section 12.4) and filter on the preferred supplier.
- **Integration tests use expiry offsets 100/75/45/15 days** instead of 91/89/59/29, because a branch's local date can differ from UTC's by a day.
- **The Inventory test factory refuses to run** unless `CATECOMMERCE_TEST_DB` is set and does not name `catecommerce_dev`.

### Integrator amendment, recorded at the merge of `ho/IMP-001` (2026-10-07)

Folded in from `docs/gates/HO-IMP-001-ARCHITECTURE.md` and `-ACCEPTANCE.md`; the items above stand. Medium/low findings are in `docs/backlog/IMP.md`.

- **The alert enum is `InventoryAlertStatus`**, not `StockAlertStatus` (Shop owns `Enums/StockAlertStatus`). ADR-110 and §8.13/§10.1 of the handover name `StockAlertAggregate` / `StockAlertStatus`; read them as `InventoryAlertAggregate` / `InventoryAlertStatus`.
- **Three further Domain ports beyond the build list above.** `IInventoryAuditEnlister` (enlists a `BusinessEvent` audit row into the caller's unit of work; the recall notify run uses it so the audit row commits with the batch and the outbox, SRS C-9) and `IInventoryAlertWriter` (saves a worker-raised alert behind Infrastructure and treats a unique violation on `ix_stock_alert_dedup_key` or `ix_stock_alert_one_live_per_pair`, and only those, as "already raised"; it saves directly, so `StockAlertRaisedEvent` is not dispatched) both live in `DomainServices/IInventoryAuditEnlister.cs`; the implementations are `EfInventoryAuditEnlister` and `EfInventoryAlertWriter`. `IInventoryAlertEnlister` (above) is the sibling in its own file. Open reservations for the recall trace and recipients are netted by the Infrastructure helper `OpenReservations` per (reference kind, reference, line), taking the party from the Reserve entries, because a Release entry carries none.
- **The alert-dedup unique indexes are named `ix_stock_alert_dedup_key` and `ix_stock_alert_one_live_per_pair`** (the handover's §12.1 says `ix_stock_alert_dedup` and `ix_stock_alert_open_pair`).
- **Two further rule-1 exceptions**, both creating only new rows beside the one aggregate they save: a storage-breach report enlists its `StorageBreach` alert, and `IStockLedger.RecordShortfall` enlists a `Shortfall` alert.
- **The FI-1 two-statement body is still awaiting ratification** (HO-IMP-001 §20.3): behaviour is verified on the real trigger, the decision is the architect's or owner's (`docs/backlog/IMP.md` row 4).

---

## ADR-177 — `Billing` bounded context

**Date:** 2026-10-07 (HO-BIL-001)
**Status:** Active
**Decision:** **`Billing` bounded context**, namespace segment `Billing`, schema `billing`, owning the roots of §8.1. It is `.ai/bounded-contexts.md`'s "Invoice" context widened to reconciliation, cash-up, export and loyalty; `.ai/namespace-conventions.md`'s `Invoice`/`Payment` segments collapse into one
**Reason:** Not in the governing docs as named (OQ-1); a reviewer would otherwise split it

---

## ADR-178 — Charges are the only input to invoices

**Date:** 2026-10-07 (HO-BIL-001)
**Status:** Active
**Decision:** **Charges are the only input to invoices.** Push: `IBillableChargeRecorder` enlists a charge in the producer's unit of work (areas built after Billing). Pull: thin event handlers dispatch `CaptureSourceChargesCommand` in a **fresh DI scope**, swallow and log failures; `GetUncapturedSourcesQuery` + manual re-capture recover. Idempotent by `(source_kind, source_id, source_line_ref)`. Charges are immutable except `Void`; "billed" is derived and made at-most-once by a filtered unique index on `invoice_line.charge_id`
**Reason:** Delivery order puts EMR/PHM/SRG before Billing and ORD/BRD after; two mechanisms are the honest answer. The fresh scope follows from `.ai/domain-events-and-outbox.md` §3

---

## ADR-179 — Gapless numbering

**Date:** 2026-10-07 (HO-BIL-001)
**Status:** Active
**Decision:** **Gapless numbering** per `(branch, financial year, document kind)` via a series row taken and committed in the same `SaveChangesAsync` as the document, `xmin`-guarded, with ≤ 5 retries that reload only the series row. No PostgreSQL sequence, lock or explicit transaction. Format `INV|CN-{branch}-{fy}-{000000}`; FY start from `billing.financialYearStartMonth`
**Reason:** FR-BIL-1 "no gaps, no reuse" under concurrency (US-BIL-01 AC2); a sequence gaps on rollback; the retry looks like a hack unless recorded
**Amended 2026-10-09 (ADR-215, accepted by the owner; HO-ORD-001 architecture-gate fix):** the rule above holds for every caller that is **not inside a transaction it opened itself**. A producer that joins its own transaction through Billing's published `IBillingTransactionScope` (HO-BIL-001 §8.13 Amended) cannot use the retry (a failed statement aborts the whole transaction and EF's savepoints are off inside the scope), so **`DocumentNumbering` takes the series row `FOR UPDATE` before it reads the number, only when a transaction is open**: any other issuer, inside a scope or not, waits on that row and then reads the next number; the in-process gate is skipped there (a gate holder waiting on a row lock and a row-lock holder waiting on the gate would deadlock). The lock is Billing's, per (branch, financial year, kind), released with the caller's commit or rollback; no caller names or takes it. Outside a transaction nothing changes: still no sequence, no lock, no explicit transaction.

---

## ADR-180 — Issued financial documents are immutable

**Date:** 2026-10-07 (HO-BIL-001)
**Status:** Active
**Decision:** **Issued financial documents are immutable** in the aggregate **and** by PostgreSQL triggers (invoice content columns, invoice lines, credit notes, refunds, allocation entries, payment core columns); status is derived from totals; `Discarded` added for drafts only
**Reason:** BR-11, D-5; "anyone" includes a `psql` prompt (same reasoning as HO-ACC-001 ADR-040); the extra status looks like a deviation from Appendix C

**Amended 2026-10-09 (integrator, migration `HardenClinicalAndBillingTriggers`; `docs/backlog/BIL.md` row 6):** row triggers do not fire for `TRUNCATE`, so every table in the `billing` schema now also has a `BEFORE TRUNCATE … FOR EACH STATEMENT` trigger (`trg_no_truncate`, `billing.fn_no_truncate()`) raising `P0001`. The migration attaches it to the tables that exist then; **a future billing table must add it in its own migration** — TEST XI-7 lists the schema's tables at run time and fails if one is missing.

---

## ADR-181 — Tax

**Date:** 2026-10-07 (HO-BIL-001)
**Status:** Active
**Decision:** **Tax**: prices are **tax-exclusive** (FR-ORD-5's formula); the rate is resolved from HO-ADM-001's effective-dated rates **at issue** (tax point = issue, OQ-4) and frozen on the line; tax per line = `round(net × rate / 100, 2, AwayFromZero)`; invoice tax = Σ line tax; invoice-level discounts apportioned pro-rata before tax, largest remainder to the largest line; one currency
**Reason:** US-BIL-07 AC2; rounding choices change totals and must be one decision

---

## ADR-182 — Payment = a record of money received, method-independent

**Date:** 2026-10-07 (HO-BIL-001)
**Status:** Active
**Decision:** **Payment = a record of money received, method-independent.** `PaymentMethod` carries capability flags; only `PaymentMethodRules` reads identities (U-X4). No online member, token or provider column in v1. The Phase-5 extension plan (§8.8.1). **C-5 guard**: references that look like card numbers are refused and never logged
**Reason:** Owner: gateway later "without rework"; SRS A-2; `.ai/enumeration-pattern.md` forbids a premature online member

---

## ADR-183 — Attributable manual payments

**Date:** 2026-10-07 (HO-BIL-001)
**Status:** Active
**Decision:** **Attributable manual payments**: recorder, reference, received date mandatory by method; `> billing.paymentApprovalThreshold` → `PendingApproval` until a **different** staff member with `Payments.Approve` approves; no edit — **reversal and re-entry**; reversal releases allocations; a refunded payment cannot be reversed
**Reason:** FR-BIL-12, BR-17, US-BIL-11; the only substitute for a gateway's record

---

## ADR-184 — Posting exception to rule 1

**Date:** 2026-10-07 (HO-BIL-001)
**Status:** Active
**Decision:** **Posting exception to rule 1**: issue, allocation, approval, reversal, credit note, refund and loyalty operations commit several Billing roots in **one** `SaveChangesAsync` (atomic, `xmin` on each); secondary roots' events are dispatched by a follow-up no-change `UpdateAsync` (F31) through `BillingPosting.CommitAsync`
**Reason:** Money moving between records must be atomic; rule 1 says "raise it" — this raises it

---

## ADR-185 — Customer credit = unallocated amounts of accepted payments

**Date:** 2026-10-07 (HO-BIL-001)
**Status:** Active
**Decision:** **Customer credit = unallocated amounts of accepted payments.** Deposits are earmarked payments; credit-note excess is released from allocations newest-first and becomes credit ("refund as store credit"); cash/transfer refunds draw down a payment; allocation entries are append-only `Allocate`/`Release`
**Reason:** One model for deposits, overpayments and store credit instead of three ledgers; US-BIL-03, US-BIL-08, FR-ORD-17

---

## ADR-186 — Credit notes

**Date:** 2026-10-07 (HO-BIL-001)
**Status:** Active
**Decision:** **Credit notes** have their own gapless series, reverse tax at the invoice line's frozen rate, cap per line, and are followed (when a correction is needed) by a corrected draft linked both ways
**Reason:** FR-BIL-5/6, US-BIL-05 AC2

---

## ADR-187 — Idempotency at the HTTP edge for money-moving commands

**Date:** 2026-10-07 (HO-BIL-001)
**Status:** Active
**Decision:** **Idempotency at the HTTP edge for money-moving commands**: `Idempotency-Key` header (≤ 100) on record payment, issue invoice (both forms), credit note and refund; unique with the caller; a repeat returns the original result with `200`
**Reason:** ADR-014 moved idempotency to the edge; a double-clicked "Record payment" must not record twice (COM-3, FR-ORD-20 spirit)

---

## ADR-188 — Cash-up

**Date:** 2026-10-07 (HO-BIL-001)
**Status:** Active
**Decision:** **Cash-up** per branch and business date; business date = local date of acceptance/issue; close requires an explanation for any variance and no pending approvals; closed is terminal; late items carry into the next cash-up
**Reason:** US-BIL-12, US-BIL-06 AC3; the race must be answered, not ignored

---

## ADR-189 — Reconciliation

**Date:** 2026-10-07 (HO-BIL-001)
**Status:** Active
**Decision:** **Reconciliation**: COD remittances with lines per collection (shortfall = collected − Σ remitted); bank statements imported from a canonical CSV, parsed in memory, never stored as files, duplicate imports refused by SHA-256; matches are 1:1 roots with filtered unique indexes, auto-matched only when exactly one candidate has equal amount, date within tolerance and a reference token match; unmatched listed both ways
**Reason:** FR-BIL-7; the only control over money received without a settlement file

---

## ADR-190 — Accounting export

**Date:** 2026-10-07 (HO-BIL-001)
**Status:** Active
**Decision:** **Accounting export**: a deterministic ZIP of CSVs (fixed entry order and timestamp, invariant culture, UTF-8 without BOM, `\n`), containing only immutable facts dated in the range; every row has `recordType` and a stable `documentId`; each run is recorded (who, range, counts, SHA-256) by a separate command; CSV-injection guard as HO-ADM-001 ADR-051
**Reason:** FR-BIL-10, US-BIL-09 AC2 "identical output" — current status would change between runs

---

## ADR-191 — Loyalty

**Date:** 2026-10-07 (HO-BIL-001)
**Status:** Active
**Decision:** **Loyalty**: disabled by default; accrue on `Paid`, reverse on credit, redeem on drafts as a pre-tax discount; account + append-only entries; handlers idempotent by unique keys
**Reason:** US-BIL-10 with the fewest moving parts

---

## ADR-192 — Merge and anonymisation

**Date:** 2026-10-07 (HO-BIL-001)
**Status:** Active
**Decision:** **Merge and anonymisation**: immutable financial records keep the original customer id and reads fold in customers merged into the survivor; mutable records (unbilled charges, drafts, loyalty balance) are moved; the invoice's `BillToSnapshot` is retained under D-9 after anonymisation
**Reason:** HO-ACC-001 ADR-039 is binding, but rewriting an issued invoice would break ADR-180

---

## ADR-193 — Billing messages

**Date:** 2026-10-07 (HO-BIL-001)
**Status:** Active
**Decision:** **Billing messages** (three templates) are enqueued in command handlers only; none for staff-internal states
**Reason:** HO-NTF-001 ADR-057

---

## ADR-194 — Money

**Date:** 2026-10-07 (HO-BIL-001)
**Status:** Active
**Decision:** **Money** follows HO-ADM-001 ADR-054 (`numeric(12,2)` + code) — exact in minor units, so SRS C-6's intent is met; `.ai/persistence-ef-core.md`'s `numeric(19,4)` is not adopted; all arithmetic in `InvoiceCalculator` with explicit rounding
**Reason:** Three documents disagree (F33); the builder must not pick one silently

---

## HO-BIL-001 build amendments (2026-10-07)

How the Billing build refines ADR-179, ADR-182, ADR-184, ADR-187, ADR-188, ADR-190 and ADR-191. None reverses one; the ADR-177 to ADR-194 text above is unchanged (ACCEPTANCE compared it with the handover's §9: 18 of 18 word for word) and ADR-195 and ADR-196 stay free. No gate asked for this section as such; it is recorded because the ADR text alone would mislead a maintainer about the numbering retry, the card-number guard and the loyalty keys. The integrator folds the same items into `docs/handover/HO-BIL-001.md` §1 Amended.

### Integrator amendment, recorded at the merge of `ho/BIL-001` (2026-10-07)

Folded in from `docs/gates/HO-BIL-001-ARCHITECTURE.md`, `-SECURITY.md`, `-REVIEW-R2.md` and `-ACCEPTANCE.md`. Medium/low findings are in `docs/backlog/BIL.md`.

- **Numbering retries (ADR-179).** `DocumentNumbering` retries up to 5 times on a unique violation of `ix_invoice_number`, `ix_invoice_sequence`, `ix_credit_note_number` or `ix_credit_note_sequence`: that is the same stale-series race, seen when EF sends the document INSERT before the series UPDATE. The retry reloads only the series row and detaches the failed attempt's message and audit rows; a genuine numbering bug reloads the same number, uses up its 5 attempts and rethrows, so it stays loud (the filter does not map these indexes). A static in-process `SemaphoreSlim` per (branch, financial year, kind) reduces collisions. It is an optimisation, not a lock: correctness across processes rests on `xmin` and the unique indexes, and "no PostgreSQL sequence, lock or explicit transaction" still holds. Verified with two hosts on one database (TEST XI-1, XI-1b: contiguous 1..40, retries seen). Under heavy cross-instance contention five attempts can run out, which gives a failed request, never a gap or a duplicate.
- **Receipt numbers are not gapless.** The receipt number is drawn before the save with `nextval('billing.receipt_number_seq')` (`EfBillingFinder`); the column keeps its database default and gaps are allowed (§8.8). ADR-179 applies to invoices and credit notes only.
- **Names that Inventory owns.** Billing's document kind enumeration is `BillingDocumentKind` (`Invoice` = 1, `CreditNote` = 2; `DocumentKind` is HO-IMP-001's inventory document kinds), and Billing's command-to-actor extension is `ToBillingActor` (Inventory's is `ToActor`). `InvalidQuantity` and `ApproverMustDiffer` are declared once, in `BusinessViolationCodes.Inventory.cs`. Read ADR-179 and the handover's §8.7 accordingly.
- **The C-5 guard (ADR-182) is wider and has two exemptions.** `CardNumberGuard` is applied to every free-text money field through `BillingFreeText.RefuseCardNumber` (reasons, explanations, notes, labels, descriptions, the billing profile's text fields, charge `ReportingTag`, statement `BankAccountLabel`, the courier code and name): a match is refused with `ReferenceLooksLikeCardNumber`, and the value is never echoed or logged. Bank-statement Reference, Description, `BankTransactionId` and `SourceFileName` are **masked** instead (masked first, then truncated), because the text is a bank's or an uploaded file's, not the user's to choose. The guard normalises before matching: any Unicode decimal digit counts as a digit; whitespace, `-`, `.`, `/`, `_`, `, : ; | * ( ) [ ]`, Unicode hyphens and dashes and zero-width and soft-hyphen characters count as separators. **Two exemptions narrow §8.8.2's literal rule** ("a run of 13 to 19 digits passing Luhn is a card number"): a Bangladeshi mobile number with country code (`^8801[3-9]\d{8}$`, applied only to a window of exactly 13 digits, so a longer card window next to a mobile is still caught) and the system's own `INV-` / `CN-` document numbers (at most 14 digits, so they cannot hide a 15 to 19 digit card). **Letters between digit groups deliberately do not join them** (joining letters would refuse ordinary mixed references). Bank account, tax registration and routing numbers are unguarded on purpose (§11.4 C1). Known gaps: the mask can pick the wrong digit window when another digit group precedes the card number in the same run (`docs/backlog/BIL.md` row 1, top priority); a card split across two fields is not detectable per field.
- **Idempotency has two layers (ADR-187).** The handler pre-check answers a sequential repeat; `BillingIdempotencyReplayFilter`, a global exception filter added to `MvcOptions` from `BillingModule` after `UniqueConstraintExceptionFilter` with equal order, sees the `ix_*_idempotency` unique violation first and answers the parallel loser `200` with the winner's result (TEST XI-2, XI-3). It does not check that the stored record matches the replayed request, so `IdempotencyKeyConflict` is enforced only on the sequential path (backlog BIL 10).
- **Loyalty (ADR-184, ADR-191).** The merge transfer commits both accounts and both ledger entries in **one** save: `ILoyaltyAccountStore.Stage` adds the `MergeTransferIn` entry to the shared context without saving (an unsaved add on a Domain port; the caller must save), `CommitNewAsync` then saves the `MergeTransferOut` entry, and idempotency is keyed on the Out entry by the duplicate's id. The `loyalty_entry` unique indexes are filtered on `idempotent_source`, and `ix_loyalty_entry_kind_invoice` additionally on `invoice_id IS NOT NULL`; the accrual **reversal** carries `credit_note_id` and a NULL `invoice_id` and is unique per credit note (`ix_loyalty_entry_kind_credit_note`), so every partial credit note on a paid invoice takes its own points back. Reversing a payment does **not** reverse the accrual its `Paid` transition earned (owner decision pending, backlog BIL 4).
- **The late-item window bounds ADR-188.** A late item enters a cash-up only within 60 days (`CashUpRows.LateItemWindowDays`); an item no cash-up picks up in that time is not in any expected total. Accepted by the owner for the merge (backlog BIL 14).
- **The accounting export is built in memory (ADR-190).** The whole ZIP is built, up to `MaxExportRows` (200,000 rows), before the response, because its SHA-256 is recorded first; it is not streamed. The export and its run list are not limited to the caller's assigned branches (owner decision pending, backlog BIL 2).
- **The read side projects (ADR-012).** No Billing query handler loads an aggregate; `BillingConventionTests` `U_X6` guards two statement shapes only (backlog BIL 22).

---

## ADR-116 amendment — `inventory.v_sku_sellable` and `inventory.sku_sellable_summary` (Inventory contract amendment, authorised by the owner 2026-10-07)

**Date:** 2026-10-07 (HO-SHP-001-FU2)
**Status:** Active. Amends ADR-116 without changing its text: `inventory.v_sellable_stock` stays a published contract, unchanged.
**Why:** with `v_sellable_stock` as the only contract, the catalogue availability filter at 20,000 SKUs cost ~85 ms of database time per statement and I-E11 failed (p95 ~700-960 ms against 500; HO-SHP-001 OQ-21, backlog SHP 62).
**Decision:**
- **`inventory.sku_sellable_summary`** is an Inventory-owned table kept by triggers (refreshed at the writer's commit), keyed (sku_id, branch_id, expires_on), holding `sellable_quantity` (on-hand minus reserved over Available batches at active shop-floor and warehouse locations), `last_changed_on`. `expires_on = 'infinity'` is undated stock. It is written only by `inventory.fn_refresh_sku_sellable` (guarded like `stock_position`); it is recomputed (delete and re-insert under a per-(sku, branch) advisory lock), never incrementally adjusted, so no eligibility change can leave drift.
- **Triggers:** `stock_position` insert/update/delete (the rows the ledger and reservation apply triggers write: receipt, sale, transfer, adjustment, reserve, release), `batch` status or `expires_on` change (quarantine, release, recall, write-off, expiry edit), `stock_location` `is_active`/`kind`/`branch_id` change. A migration backfills from current positions.
- **Expiry is applied at read time, not by a worker.** The expiry date is part of the key, and **`inventory.v_sku_sellable`** (the new published read contract: branch_id, sku_id, expires_on (null = undated), sellable_quantity, last_changed_on) hides buckets whose date has passed in the branch's local date (`BatchExpiryRules`, OQ-20: usable through the whole expiry date). Nothing is written when a branch's midnight passes, so the contract equals `v_sellable_stock` at every instant rather than within a worker's interval.
- **Equivalence is a tested invariant:** summing `v_sku_sellable` by (branch, sku) gives `v_sellable_stock.sellable_quantity`, and its earliest dated bucket with quantity gives `next_expires_on` (`InventorySellableSummaryEquivalenceTests`).
- **Consumers:** HO-SHP-001 reads `v_sku_sellable` for the catalogue filter, page labels, detail, staff stock and the due stock-alert prefilter. `v_sellable_stock` and `GetSellableStockQuery` remain for HO-ORD-001 and others.
- **Locking (fix round 1, review HIGH):** the refresh is deferred to commit and runs in one global order. The triggers only MARK the touched (sku, branch) in `inventory.sku_sellable_dirty`; a deferred constraint trigger refreshes every marked pair of the transaction ordered by (sku_id, branch_id) under a per-pair advisory lock, after all of the transaction's row locks. The earlier immediate refresh took those locks in the writer's order, which the position row locks did not make safe: writers sort by (BatchId, LocationId), batch ids are random, so two transactions moving different batches of the same two SKUs in opposite orders share no row lock yet could take the two SKU locks in opposite orders and deadlock (reproduced, 40P01, then fixed; `InventorySellableSummaryConcurrencyTests`). Cost: the summary is stale inside the writing transaction (nothing reads it there) and concurrent transactions on one (sku, branch) serialise briefly at commit.
- **Shop side:** `product.storefront_catalogue_entry.in_stock` is renamed `is_on_sale` (it only ever meant "an Active SKU is priced"); availability is read from Inventory. `Shop:StorefrontBranchId` defaults to the seeded main branch.


---

## HO-POR-001 — Customer Portal (backend), ADR-257 … ADR-267

> **Source note (2026-10-09).** HO-POR-001 says it "recorded" ADR-257 … ADR-267 "for the ARCHITECTURE gate to judge" (§1, §6), but the handover contains no §9 decision block, only §9.1 Open questions. The decisions below are therefore taken **sentence by sentence from the handover's own text** (the section is cited on each); nothing was added to them. ADR-257 … ADR-262, ADR-264, ADR-266 and ADR-267 are recorded now because Phases A, B (visits) and C are built (2026-10-09). **ADR-263 (re-order) is pending** until Phase D (HO-ORD-001, HO-SHP-001, HO-IMP-001) is built. **ADR-265 is withdrawn** (README C-6, handover §8.8).

## ADR-257 — The Portal is a context that owns nothing

**Date:** 2026-10-09 (HO-POR-001)
**Status:** Active
**Decision:** **No aggregate, no table, no schema, no migration, no domain event, no event handler** (§2 point 1, §4.4). No `AggregateRoot`, `DbSet`, `IEntityTypeConfiguration`, migration, schema, `Event` or `IEventHandlerAsync` in the `Portal` context. `Portal` is a namespace segment so that its code, its decisions and the gates' folder check have one place (§8.1): "A namespace segment costs nothing; a schema would invite someone to 'just cache' a dashboard in it." `.ai/bounded-contexts.md` does not list `Portal` (OQ-1, an architect task).
**Reason:** The portal is a window, not a module: every fact it shows is owned by another area (§2).

---

## ADR-258 — Composition only through other modules' published query handlers

**Date:** 2026-10-09 (HO-POR-001)
**Status:** Active
**Decision:** **Composition only through other modules' published query handlers — never their tables** (§2 point 2). Readers take upstream `IQueryHandlerAsync<TQuery, TResult>` **by constructor** and call `HandleAsync` directly; **not the dispatcher** — "a nested dispatch hides the dependency from the constructor ... and turns a missing registration into a runtime failure the fault-isolation policy would then swallow as `Unavailable`. Constructor injection makes a missing upstream a startup failure" (§8.2, F34). No `CatEcommerceDbContext` in any portal query handler or section reader, no nested `IInMemoryDispatcher` call, "not even 'just one join'" (§4.4, §20.3). Every upstream query carries the portal query's `UserContext` and `CorrelationId` and the caller's id in the upstream's owner/customer filter.
**Reason:** If Billing changes how it stores invoices, the portal does not notice. `.ai/read-side-pattern.md` §5 permits cross-context joins; this slice deliberately takes the narrower path.

---

## ADR-259 — One anti-corruption reader per upstream module

**Date:** 2026-10-09 (HO-POR-001)
**Status:** Active
**Decision:** **One anti-corruption adapter per upstream module** (a *section reader*, `IPortalSectionReader<T>`) is the only file that knows an upstream ViewModel's shape (§2 point 3, §8.3). Each maps upstream ViewModels **member by member** into the portal's own section ViewModel — "never passes an upstream type through, so a new upstream field (possibly staff-only) cannot leak".
**Reason:** An upstream contract change is absorbed in one reader (OQ-7, §20.2).

---

## ADR-260 — Fault isolation: a section is Ready or Unavailable on its own

**Date:** 2026-10-09 (HO-POR-001)
**Status:** Active
**Decision:** `PortalSectionComposer.RunAsync` is the **only** `try/catch` in the dashboard path (§8.4): `OperationCanceledException` is re-thrown; any other exception is logged at `Error` with the section name, the exception and the correlation id — **no customer data, no upstream payload** (I-X1) — and the section is `Unavailable` with `data = null`, "never an empty list". `Unavailable` carries no data: `null`, not `[]`, not `0`. `hasCats` is `null`/absent when the cats section is `Unavailable` (never a false "you have no cats"). Sections run in the fixed order of §8.2; "the order is not a priority, it is deterministic for tests".
**Reason:** One broken module must not blank the dashboard, and an unavailable section must never pretend to be empty (§2). Implementation note (2026-10-09): `RunAsync` also takes the correlation id as a parameter, because the log line must carry it; and the cats reader catches per cat for the vaccination read, as §8.3 requires ("A failing Q4 for one cat → that cat's `vaccination = null`").

---

## ADR-261 — Owner scoping by construction

**Date:** 2026-10-09 (HO-POR-001)
**Status:** Active
**Decision:** **The customer id comes only from the session** (§2, §8.5). The dashboard query has no customer-id property a client could set. Ids handed to an upstream handler come only from an owner-scoped upstream result in the same request, or after an owner check; someone else's resource is `404`, never `403`. "Calling an upstream **handler** directly bypasses that module's **controller filter** (HO-ADM-001 §12.3 `OwnerScope`). The rows above are what replace it."
**Reason:** NFR-SEC-2; a customer must not learn another customer's resource id exists (HO-ADM-001 ADR-043).

---

## ADR-262 — The clinical-content line

**Date:** 2026-10-09 (HO-POR-001)
**Status:** Active
**Decision:** The dashboard carries **only what identifies a record** (§8.6): visits — `visitId`, `catId`, `catName`, `visitedOn`, `serviceName`, `vetName`, `hasDischargeSummary`, `followUpOn?`; prescriptions — `catId`, `catName`, `activeCount`, `earliestEndsOn?`; cats — name, `isHighRisk`, vaccination `overallStatus`, `earliestNextDueOn`, `bookingEligible`. Never diagnosis, SOAP text, treatments, medicines, lab values, notes, dose, route, frequency, instructions, allergies, conditions, medications or the handling note. "The owning module's detail page — which can log — shows the content."
**Update 2026-10-10 (owner-delegated decision 2026-10-10, owner to review):** OQ-4 is closed -- an owner reading their own cat's visits or prescriptions is NOT logged. `followUpOn` (the follow-up due date only, `DateOnly?`) IS shown on the visits item: EMR added it additively to its list item; the follow-up kind, text and everything else clinical stay out.
**Reason:** NFR-SEC-8 requires every access to clinical records to be logged; "A query cannot log (read-side rule 5), and a controller cannot dispatch a query and a command (cqrs rule 1)." Whether an owner's read of their own record counts as an access is OQ-4; this line is safe under either answer. See the dated amendment in HO-POR-001 §1 for `followUpOn`.

---

## ADR-263 — Re-order mutates only Orders' cart through Orders' own eligibility

**Date:** 2026-10-09 (HO-POR-001)
**Status:** **Retired 2026-10-10 (owner-delegated decision 2026-10-10, owner to review)** -- the portal builds no re-order; the customer uses Orders' `POST /api/customers/me/orders/{id}/reorder`, so Orders' eligibility is used by construction and the portal mutates no cart. (Was: Pending -- recorded at Phase D, HO-ORD-001, HO-SHP-001, HO-IMP-001 not merged at 2026-10-09.) The portal "never re-implements stock or prescription gating" (§2 point 4, §8.7, §20.4).
**Update 2026-10-10:** ORD, SHP and IMP are on master, but Orders publishes no reusable cart-line eligibility (its checks live inside `ReorderCommandHandler`, C15, which already serves `POST /api/customers/me/orders/{id}/reorder`). Under §20.4 the portal re-order stays **unbuilt** until the owner chooses between reusing Orders' endpoint and asking Orders for a reusable eligibility service (see ADR-268).
**Status note (2026-10-10):** the "stays unbuilt until the owner chooses" wording above is historical; the Retired status line governs. Re-order is retired, not pending (POR backlog 4/10).

---

## ADR-264 — No payment anywhere

**Date:** 2026-10-09 (HO-POR-001)
**Status:** Active
**Decision:** **No payment, pay-link, gateway, card or wallet type or endpoint** (§4.4). Balances show what is owed and how to pay offline (`howToPay = PortalMessages.HowToPayOffline`); there is no pay-online button or endpoint.
**Reason:** The gateway is SRS Phase 5 (SRS §1.2); payments are COD, cash, in-store card and bank transfer.

---

## ADR-265 — Withdrawn

**Date:** 2026-10-09 (HO-POR-001)
**Status:** **Withdrawn** — "ADR-265 is withdrawn; the acceptance row for US-POR-03 is evidenced in HO-BRD-001, not here" (§8.8, README C-6). The stay diary is HO-BRD-001's `GET /api/customers/me/boarding/stays/{id}/diary`.

---

## ADR-266 — Money is never summed across currencies

**Date:** 2026-10-09 (HO-POR-001)
**Status:** Active
**Decision:** Balances show outstanding and overdue **per currency, never summed across currencies** (§8.3, U-B11). Zero outstanding → `hasOutstanding = false`, `outstanding = []`. Reconciled with Billing's real contract (HO-POR-001 §1 Amended 2026-10-09): Billing publishes a single commerce-currency amount, so each Money becomes at most one list entry.
**Reason:** A total across currencies is meaningless.

---

## ADR-267 — Sections run sequentially

**Date:** 2026-10-09 (HO-POR-001)
**Status:** Active
**Decision:** "Sections and per-cat status calls run **sequentially**. `Task.WhenAll` over upstream handlers is a defect" (F36, §8.3).
**Reason:** The shared `CatEcommerceDbContext` is Scoped and not thread-safe; nested handlers resolved in the request scope share it.

---

## ADR-197 — `Order` bounded context

**Date:** 2026-10-09 (HO-ORD-001)
**Status:** Active
**Decision:** **`Order` bounded context**, namespace segment `Order`, schema `ordering`, owning the roots of §8.1. Order statuses are **exactly Appendix C's**: expiry is `Cancelled` + `PaymentWindowExpired`; pickup reuses `Dispatched` (ready for collection) and `Delivered` (collected)
**Reason:** Adding states would diverge from Appendix C and from every report built on it; the mapping must be deliberate

---

## ADR-198 — Money is computed in `long` minor units

**Date:** 2026-10-09 (HO-ORD-001)
**Status:** Active
**Decision:** **Money is computed in `long` minor units** in the pure pricing services and converted exactly to ADR-054's `decimal(12,2)` `Money` at the boundary; rounding half away from zero at the line; apportionment by largest remainder
**Reason:** US-ORD-04 AC2 and C-6 say *integer minor units*; ADR-054 fixed storage as decimal. This ADR satisfies both without amending ADR-054 (OQ-5 recommends ADR-054 cite it)

---

## ADR-199 — Carts are server-side.

**Date:** 2026-10-09 (HO-ORD-001)
**Status:** Active
**Decision:** **Carts are server-side.** Guests get an opaque 32-byte token in an `HttpOnly; Secure; SameSite=Lax` cookie `pc_cart` (hash stored); one `Active` cart per customer; merge is an explicit, idempotent `POST …/cart/merge` that takes the larger quantity per `(SKU, cat)` key; carts reserve nothing
**Reason:** US-ORD-01 AC2 "switch device" needs a server cart; HO-ACC-002 forbids storing tokens or drafts in browser storage; a `GET` must not merge

---

## ADR-200 — Quote-and-fingerprint acknowledgement.

**Date:** 2026-10-09 (HO-ORD-001)
**Status:** Active
**Decision:** **Quote-and-fingerprint acknowledgement.** The quote is computed, never stored; placement recomputes it and refuses `409 CheckoutQuoteChanged` unless the submitted fingerprint matches
**Reason:** FR-ORD-3 "must acknowledge"; a client-side "I agree" flag proves nothing about what was shown

---

## ADR-201 — Enlisting ports and multi-root saves.

**Date:** 2026-10-09 (HO-ORD-001)
**Status:** Active
**Decision:** **Enlisting ports and multi-root saves.** Placement, confirmation, approval, cancellation, expiry, delivery-with-payment, return receipt/decision, refund and counter sale commit the order (or refund/return) plus tracked Order-context roots and entities staged by Inventory, Billing and Notification ports in **one** `SaveChangesAsync`. Staged roots raise no domain events anyone depends on. Recorded exception to `.ai/bounded-contexts.md` rule 1
**Reason:** US-ORD-08 "all atomically"; the alternative (event handlers after commit) fails *after* the order is durable (`.ai/domain-events-and-outbox.md` §3)

---

## ADR-202 — HTTP idempotency

**Date:** 2026-10-09 (HO-ORD-001)
**Status:** Active
**Decision:** **HTTP idempotency** via `[Idempotent]` and `ops.idempotency_record` (scope, key, request hash, replayed response, 24 h), inserted in its own short transaction; `InProgress` → `409`; different body → `422`; failure frees the key. Plus the unique `(customer_id, idempotency_key)` on orders
**Reason:** ADR-014 moved idempotency to the edge; FR-ORD-20; the filter is reusable by Billing and Boarding

---

## ADR-203 — Payment methods and holds.

**Date:** 2026-10-09 (HO-ORD-001)
**Status:** Active
**Decision:** **Payment methods and holds.** COD and pay-on-collection confirm at placement; bank transfer holds `PendingPayment` with stock reserved until `orders.bankTransferWindowHours`, then `OrderWorker` expires it through a command. **No gateway, no card data, no token; in-store card = method + amount + slip reference**
**Reason:** C-5, A-2, BR-16, FR-ORD-21/22; the pay-on-collection reading of FR-ORD-9 is a choice (OQ-2)

---

## ADR-204 — Second approval of received money

**Date:** 2026-10-09 (HO-ORD-001)
**Status:** Active
**Decision:** **Second approval of received money** above Billing's threshold is held on the order and completed through this slice's approve endpoint by a different person; Billing is written only when final; held orders do not expire
**Reason:** FR-BIL-12 / BR-17 without a Billing-to-Order event that would have to confirm the order from an event handler

---

## ADR-205 — Reconciliation issues.

**Date:** 2026-10-09 (HO-ORD-001)
**Status:** Active
**Decision:** **Reconciliation issues.** A non-business failure after staff entered payment details writes an issue in a fresh DI scope, then rethrows; accountants resolve it
**Reason:** US-ORD-08 AC2 "surfaced … not silently lost" when the one transaction has rolled back

---

## ADR-206 — Delivery zones

**Date:** 2026-10-09 (HO-ORD-001)
**Status:** Active
**Decision:** **Delivery zones** by district plus optional postal codes, priority-ordered; two service levels with weight bands and free-over thresholds; COD toggle and ceiling per zone; slots are capacity-free preference windows; zones edited in place (audited) and **snapshotted on the order**
**Reason:** FR-ORD-7, BR-15; HO-ADM-001 §4.2 routed zones here; ADR-049's snapshot rule is what protects history, not append-only zone rows

---

## ADR-207 — Discount codes

**Date:** 2026-10-09 (HO-ORD-001)
**Status:** Active
**Decision:** **Discount codes** evaluated by one pure rule list with a fixed order of failure reasons; usage counted on the code (`xmin` serialises) and per customer from orders; scoped → line discounts, unscoped → order discount; released on cancel/expiry before dispatch
**Reason:** FR-ORD-6; a redemption table would be one more root in the placement save for no gain

---

## ADR-208 — Stock only through `IStockLedger`.

**Date:** 2026-10-09 (HO-ORD-001)
**Status:** Active
**Decision:** **Stock only through `IStockLedger`.** Reserve FEFO at placement for every method, `ConsumeReservationAsync` at dispatch (delivery) or collection (pickup), `ReleaseAsync` on cancel/expiry; BR-14 checked at dispatch; every call carries the party and reference stamp that feeds Inventory's recall trace (README C-3)
**Reason:** `.ai/flows/order-fulfilment-flow.md` "Ordering never picks a batch itself"; D-4

---

## ADR-209 — Couriers.

**Date:** 2026-10-09 (HO-ORD-001)
**Status:** Active
**Decision:** **Couriers.** Manual courier, manual tracking and CSV manifest now; `ICourierGateway` declared for an API adapter that will run as an outbox-driven worker pass (never inside a request); inbound webhooks HMAC-verified per configured provider and deduplicated by `(provider, event_id)`
**Reason:** SRS Q-4 open; COM-3; `.ai/domain-events-and-outbox.md` §4 "courier API goes through the outbox"

---

## ADR-210 — Returns are their own root

**Date:** 2026-10-09 (HO-ORD-001)
**Status:** Active
**Decision:** **Returns are their own root**; BR-10 eligibility from Shop's category class and Inventory's batch status, with staff-confirmed defects; delivery failures use the same path; returned stock always enters quarantine first
**Reason:** FR-ORD-15/16/21 in one path the gates can follow

---

## ADR-211 — Refunds are recorded by this slice

**Date:** 2026-10-09 (HO-ORD-001)
**Status:** Active
**Decision:** **Refunds are recorded by this slice** (`OrderRefundAggregate`) with a Billing credit note in the same save; methods by how the customer paid; `Refunded` status only on full refund of a delivered or returned order; a cancelled paid order stays `Cancelled` with `RefundDue`
**Reason:** US-ORD-15 is an order story; FR-BIL-5 puts the credit note on the invoice. If HO-BIL-001 owns refund records, this is amended (OQ-15)

---

## ADR-212 — The counter sale is an order

**Date:** 2026-10-09 (HO-ORD-001)
**Status:** Active
**Decision:** **The counter sale is an order** with channel `Counter`, completed in one command through the same pricing, ledger and invoice paths; receipt is data plus a 48-column text rendering; no server-side printer driver
**Reason:** FR-ORD-18 "same inventory ledger"; HW-1 printing belongs to the browser and OS

---

## ADR-213 — Order messages

**Date:** 2026-10-09 (HO-ORD-001)
**Status:** Active
**Decision:** **Order messages** are enqueued in command handlers with the change (HO-NTF-001 ADR-057); confirmation, awaiting-transfer, cancellation and refund are `Urgent`; the rest `Normal`; no external URL in any variable
**Reason:** FR-ORD-13; the customer who just acted should not wait for quiet hours

---

## ADR-214 — Orders are financial records.

**Date:** 2026-10-09 (HO-ORD-001)
**Status:** Active
**Decision:** **Orders are financial records.** Merge reassigns carts, orders and returns; anonymisation redacts the recipient and address snapshot of terminal orders and flags open ones for redaction at their terminal transition; nothing is deleted
**Reason:** D-6, D-9, D-11; HO-ACC-001 ADR-039

---

## ADR-215 — The Billing seam: charges, published commands, one database transaction

**Date:** 2026-10-09 (HO-ORD-001, build amendment; **accepted by the owner 2026-10-09** after the ARCHITECTURE gate; reworked the same day)
**Status:** Accepted by the owner on 2026-10-09
**Decision:** HO-ORD-001 §8.14 assumed three **enlisting** Billing ports (`IInvoiceIssuer`, `IPaymentRecorder`, `ICreditNoteIssuer`). HO-BIL-001 published a different contract (its §8.13): the enlisting **charge** recorder `IBillableChargeRecorder` and commands that save on their own (`IssueInvoiceFromChargesCommand`, `RecordPaymentCommand`, `IssueCreditNoteCommand`, `IssueRefundCommand`). Ordering uses the contract as published and keeps **one atomic transaction** — "order, batch reservation, confirmation and invoice happen atomically" (US-ORD-08 AC1, ADR-201) — but **Billing publishes the entry point**, not Ordering:
- **`IBillingTransactionScope`** (Domain, implemented in Infrastructure/Billing; HO-BIL-001 §8.13 Amended 2026-10-09) lets a producer run its own change and Billing's published commands in one database transaction (`RunAsync`, `FlushAsync`). It owns the transaction, the savepoint rule (EF automatic savepoints are off inside it because Billing's frozen-invoice trigger needs the invoice row written by the top-level transaction) and — through `DocumentNumbering` — **the per-branch numbering lock** (see the ADR-179 amendment: inside an open transaction the series row is read `FOR UPDATE`).
- Ordering's `ITransactionalScope` is that scope plus one thing that is Ordering's own: a commit-time stock race (`23514` on `ck_stock_position_balance`, `tg_stock_batch_expired`, `tg_stock_batch_not_usable`) becomes the business refusal `StockNoLongerAvailable`. Ordering takes **no lock**, names no Billing series and reads no trigger internals.
- Ordering consumes only Billing's published surface: `IBillableChargeRecorder`, `IssueInvoiceFromChargesCommand`, `RecordPaymentCommand`, `ApprovePaymentCommand`, `RejectPaymentCommand`, `IssueCreditNoteCommand`, `IssueRefundCommand` (the last four published by the same amendment) and the planning port `IBillingRefundPlanner` (credit-note line plan, refund draws). It no longer loads Billing's `InvoiceAggregate` / `PaymentAggregate` or injects `IBillingFinder`.
- `HO-BRD-001` and `HO-SUB-001` use the same entry point for their invoices and payments; neither writes its own transaction code or lock.
`IStoreCreditLedger` does not exist, so store credit stays refused (OQ-8).
**Reason:** §20.2 forbids compensation logic and an adapter that hides a behavioural gap; a transaction around Billing's own published commands adds neither. The alternative sanctioned by HO-BIL-001 §8.13 (commit the order, then issue the invoice, leave unbilled charges for staff) would make I-D8 and A21 false. The ARCHITECTURE gate (HIGH) objected to the first version because Ordering reached into Billing's numbering (an advisory lock Ordering named) and trigger internals, and contradicted ADR-179; the owner approved "keep one atomic transaction, Billing publishes it", so the transaction, the savepoint rule and the numbering lock moved into Billing. The precedent for a transactional multi-step handler is `ICustomerHoldSerializer` (HO-APT-001, ADR-070).
**Consequence:** inside the scope a numbering collision cannot occur: a second issuer waits on the series row. A staff-issued invoice racing an Ordering-issued one at the same branch waits and then takes the next number (proved by the Ordering concurrency test). A failure anywhere rolls the whole unit back and leaves an ADR-205 reconciliation issue when a staff member had entered money.

## ADR-216 — The counter sale's buyer is a real customer, found or created by mobile number

**Date:** 2026-10-09 (HO-ORD-001, build amendment; **accepted by the owner 2026-10-09**, replacing the first version, a shared "Walk-in customer" account)
**Status:** Accepted by the owner on 2026-10-09
**Decision:** Billing needs a customer on every charge, invoice and payment (`IssueInvoiceFromChargesCommand` refuses an unknown or inactive customer). A counter sale therefore **records the customer's name and mobile number, both required**. The handler:
1. normalises the mobile (`MobileNumber.Create`; an invalid number is a `400`);
2. looks for an **Active customer whose primary number matches** through HO-ACC-001's published lookup `FindCustomersQuery` (Q-H2; an additional number is an unverified claim and never identifies a buyer) — adapted by `AccountsCounterCustomerFinder` in the Api project because the Application layer does not reference the Read project;
3. if there is none, creates a passwordless customer through HO-ACC-001's existing `RegisterWalkInCustomerCommand` (§8.8, ADR-038: `RegistrationChannel = Reception`, Incomplete until a password is set) **inside the sale's transaction**, after every check has passed, so a refused sale leaves no account behind;
4. carries that `CustomerId` on **the order and the invoice** — purchase history, loyalty and the recall-trace party now belong to a real person.
The link to the web is HO-ACC-001's own: if the person later registers online with the same mobile, Accounts answers `MobileNumberAlreadyRegistered` with the password-reset offer, which completes **the same account**. An integration test proves it (counter sale creates the walk-in; self-registration with that mobile gives the reset offer).
There is no shared account, no reserved number (`01000000000` is an ordinary number again) and nothing that seeds or special-cases one. This closes SECURITY backlog MEDIUM 1 (ORD 15) and LOW 7 (ORD 21).
**Reason:** a shared account was a squattable, takeover-prone placeholder that hid every anonymous invoice behind one record (SECURITY MEDIUM 1, LOW 7); the owner chose to identify the buyer. Accounts already has the lookup, the passwordless registration and the claim path, so nothing new is added to Accounts.
**Consequence:** a cashier must take a name and a mobile for every sale (the POS request fields are `customerName` and `customerMobile`; `customerId` is gone from the sale, it stays on the quote). A prescription food in a counter sale is refused for a brand-new customer (a cat belongs to an existing customer). `PerCustomerLimit` / `FirstOrderOnly` discount rules now see the real customer (closes SECURITY backlog LOW 17 for found customers; a brand-new customer has no history, which is the truth).

## ADR-179 -- Amended 2026-10-10 (lock rule; TEST gate F-1, backlog ORD 41)

**Status:** Active (append-only amendment)
**Finding:** a scoped issuer (Ordering's counter sale / cancellation, inside `IBillingTransactionScope`) and an unscoped one (staff invoice or credit note) at the same branch, year and kind could deadlock (PostgreSQL 40P01). The scoped side took `FOR UPDATE` on the series row first, then inserted its document. The unscoped side read the row without a lock and EF sent its document INSERT before the series UPDATE: it held the unique-index entry for number N while the scoped side, holding the row lock, waited on that very entry, and the unscoped UPDATE waited on the row lock. Two orders, one cycle.
**Rule (final):** **every issuer takes the series row lock (`SELECT ... FOR UPDATE` on branch + financial year + kind) before it writes the document.** A scoped issuer does it inside the caller's transaction (`LockSeriesRowAsync`). An unscoped issuer now opens its own short transaction in `SaveWithRetryAsync`, locks the row, reloads the series (so the number is the committed one; the document is renumbered if it moved), saves and commits. Lock order is therefore always series row, then document. A deadlock victim or a collision in the unscoped path rolls back its transaction, including the series update, so no number is consumed or skipped; it is retried (40P01 counts as a collision, only in the unscoped path -- inside a caller's transaction the whole transaction is aborted and nothing can retry). A second hole was closed in the same change: a scoped issuer that locked a series row not yet visible (its creator had not committed) held nothing, then read a number that was stale by the time it used it; `EnsureSeriesAsync` now re-locks and reloads the row when the first lock matched nothing. The `xmin` check and the unique backstops stay as the correctness guard across processes. The in-process gate stays (a gate holder waits on the row; a row holder never waits on the gate).

## ADR-204 -- Amended (owner decision 4) 2026-10-09

**Status:** Active (append-only amendment; backlog ORD 37)
**Amendment:** "Billing is written only when final" no longer holds. Since gate fix 4 (C11) the order's Billing **invoice and a pending payment (`PendingApproval`) are written at hold time**, and the second approval then finalises that payment (or the rejection voids it). The original reason (no Billing-to-Order event) stands only in part: the open gap is the one in backlog row 33.


## ADR-216 -- Amended 2026-10-10 (TEST gate F-2, F-3; backlog ORD 42, 43)

**Status:** Active (append-only amendment)
**Amendment:** the buyer is found or registered in its **own short transaction before the sale's transaction opens** (`ICounterBuyerRegistrar`, implemented over Accounts' `FindCustomersQuery` and `RegisterWalkInCustomerCommand`), no longer inside the sale's scope. Registering inside the scope could not recover from the unique-mobile race (23505 aborts the transaction, savepoints are off), so concurrent cashiers lost their sale and a false `CounterSaleFailed` issue was opened. Now the loser re-finds the winner's account and every concurrent sale proceeds. A sale refused after registration leaves the registered walk-in behind (a real person at the counter; harmless, and found by their mobile next time).
**Merged / Anonymised holder (F-3):** the mobile stays held by that account. The cashier gets `400 AccountNotActive` with a cashier-facing message (find the customer's current record under Customers) and no reconciliation issue is opened. Following a merge to the surviving account was not settled by HO-ACC-001 or this handover, so it is not done; it is an owner question.

---

## HO-PHM-001 — Pharmacy and Prescriptions (ADR-137 … ADR-154)

---

## ADR-137 — `Prescription` bounded context

**Date:** 2026-10-09 (HO-PHM-001)
**Status:** Active
**Decision:** **`Prescription` bounded context**, namespace segment `Prescription` (already in `.ai/namespace-conventions.md`), schema `pharmacy`, owning the formulary, prescriptions, dispensing, the controlled-drug register and diet authorisations
**Reason:** `.ai/bounded-contexts.md` lists only `PrescriptionAggregate` and `DietAuthorisation`; the widening must be deliberate (OQ-11)

---

## ADR-138 — The formulary is practice data

**Date:** 2026-10-09 (HO-PHM-001)
**Status:** Active
**Decision:** **The formulary is practice data**, administrator-maintained; prescriptions **snapshot** the medicine at issue; the controlled flag has its own command and audit action; a SKU links to at most one medicine
**Reason:** FR-PHM-4; a formulary edit must never rewrite a past prescription

---

## ADR-139 — Safety assessment is one pure domain service run twice

**Date:** 2026-10-09 (HO-PHM-001)
**Status:** Active
**Decision:** **Safety assessment is one pure domain service run twice** (query and command). Out-of-range dose, allergy conflict and `Major`/`Contraindicated` interactions require acknowledgement; allergy acknowledgement needs a reason; acknowledgement is **bound by a fingerprint** to the warnings seen; a stale fingerprint is `409`. A missing or stale weight for a per-kg medicine has **no** acknowledgement path
**Reason:** US-PHM-02/03 must hold against direct API calls; an acknowledgement of yesterday's warnings is not consent to today's

---

## ADR-140 — Allergy and interaction matching

**Date:** 2026-10-09 (HO-PHM-001)
**Status:** Active
**Decision:** **Allergy and interaction matching** by normalised term match over name, ingredients and classes, both directions for interactions; unresolved profile medications are listed, never ignored; the free-text limitation is stated on screen
**Reason:** HO-CAT-001 stores allergens as free text; the gap must be visible, not discovered

---

## ADR-141 — Dose conversion

**Date:** 2026-10-09 (HO-PHM-001)
**Status:** Active
**Decision:** **Dose conversion** to mg/kg only where units are provably convertible; `NotCheckable` is always reported; the expected range is returned in mg/kg **and** absolute mg
**Reason:** A silent pass on an unconvertible dose is the dangerous failure

---

## ADR-142 — Prescription lifecycle

**Date:** 2026-10-09 (HO-PHM-001)
**Status:** Active
**Decision:** **Prescription lifecycle** = Appendix C (`issued → dispensed → completed`, `expired`, `cancelled`) with repeats counted as supplies; `ValidUntil` is the last dispensable day for every supply; "active" is derived read-side and never stored; every read treats a passed `ValidUntil` as expired regardless of the sweep
**Reason:** Appendix C has no repeat concept; the mapping must be fixed once

---

## ADR-143 — Dispensing is its own root.

**Date:** 2026-10-09 (HO-PHM-001)
**Status:** Active
**Decision:** **Dispensing is its own root.** Dispense (and reversal) commit the dispensing record, the prescription's supply count, HO-IMP-001 ledger entries and controlled-drug register entries in **one** `SaveChangesAsync` — a recorded rule-1 exception, atomic by F23; FEFO suggested, deviation needs a reason; **expired stock has no override**; a supply is full, never partial
**Reason:** Stock, authorisation budget and the register must never disagree

---

## ADR-144 — Controlled-drug register

**Date:** 2026-10-09 (HO-PHM-001)
**Status:** Active
**Decision:** **Controlled-drug register**: append-only root with no mutator, an EF immutability interceptor and PostgreSQL triggers; corrections are new linked entries that also enlist an Inventory adjustment when the quantity changes; branch-scoped; **exposed as `IControlledDrugRegister` for Surgery, Treatment and Inventory**, which enlist in their own save
**Reason:** C-2, D-5, FR-PHM-7; HO-SRG-001 depends on it

---

## ADR-145 — Tamper evidence

**Date:** 2026-10-09 (HO-PHM-001)
**Status:** Active
**Decision:** **Tamper evidence** by a per-branch SHA-256 hash chain in a separate append-only seal table, written by a worker pass after a 30 s settle delay; a verification query reports the first break and stale unsealed entries
**Reason:** C-2 says *tamper-evident*, not only append-only; triggers alone do not detect a superuser

---

## ADR-146 — A witness authenticates with their own identifier and password in the same request

**Date:** 2026-10-09 (HO-PHM-001)
**Status:** Active
**Decision:** **A witness authenticates with their own identifier and password in the same request**; must hold a witness role and differ from the actor; failures are generic, audited and count toward the witness account's lockout; the password never leaves the command
**Reason:** BR-7 "second staff witness"; identifier + password is the only credential the product has (no OTP, no 2FA)

---

## ADR-147 — Labels are frozen at dispensing

**Date:** 2026-10-09 (HO-PHM-001)
**Status:** Active
**Decision:** **Labels are frozen at dispensing** into a snapshot on the dispensing record; printing is the browser's; reprints print the same snapshot and are counted and audited
**Reason:** A label must say what was handed over, forever

---

## ADR-148 — Repeats

**Date:** 2026-10-09 (HO-PHM-001)
**Status:** Active
**Decision:** **Repeats** are owned requests on the prescription, dispensable without a visit inside `ValidUntil`; outside it, refused with the date and the next step; **renewal** is a vet act that issues a new, fully re-checked prescription and links the old one — a recorded rule-1 exception (two prescriptions, one save)
**Reason:** FR-PHM-6 "new vet authorisation"; the link keeps the history readable

---

## ADR-149 — Diet authorisations name the product

**Date:** 2026-10-09 (HO-PHM-001)
**Status:** Active
**Decision:** **Diet authorisations name the product** (the vet prescribes "Renal diet X", not the 2 kg bag) for one cat, with branch-local dates; the **gate takes a SKU** as `.ai/flows/prescription-diet-flow.md` says and resolves its product; validity is evaluated on every call so locking needs no job; the sweep only records expiry, raises the event and notifies
**Reason:** The flow doc says "specific SKU"; this refines it and must say so

---

## ADR-150 — Pharmacy messages

**Date:** 2026-10-09 (HO-PHM-001)
**Status:** Active
**Decision:** **Pharmacy messages**: five transactional templates; refill reminders are a timed producer with a deduplication key and a `PrescriptionRefillDue` guard; every other message is enqueued by the command that changed state
**Reason:** HO-NTF-001 §4.2 reserved the refill contract for this slice

---

## ADR-151 — Pharmacy never reads or writes another area's tables.

**Date:** 2026-10-09 (HO-PHM-001)
**Status:** Active
**Decision:** **Pharmacy never reads or writes another area's tables.** Visits are loaded read-only through the aggregate repository; Inventory, Catalogue and Accounts through their ports; the assumed contracts of §8.12 are recorded so their owners can confirm them
**Reason:** The README delivery order builds IMP and SHP first precisely so this slice never grows its own stock code

---

## ADR-152 — Customer merge

**Date:** 2026-10-09 (HO-PHM-001)
**Status:** Active
**Decision:** **Customer merge**: prescriptions, dispensings and diet authorisations are reassigned to the survivor; **register entries and seals are never rewritten** — reads resolve merged customer ids. Anonymisation leaves clinical records (labels included) intact under C-3
**Reason:** An append-only register cannot be "fixed up" by a merge

---

## ADR-153 — Authorization mapping

**Date:** 2026-10-09 (HO-PHM-001)
**Status:** Active
**Decision:** **Authorization mapping** from Appendix D: prescribe, renew, cancel, decline repeats, authorise diets and correct the register are **Vet only**; dispensing is Reception, Nurse, Vet, Administrator; reversal Vet, Administrator; the register is read by Nurse, Vet, Inventory, Administrator; the formulary is managed by Administrator (FR-PHM-4)
**Reason:** Appendix D is indicative; the mapping is an inference to be confirmed (OQ-1, OQ-8)
**Status note (2026-10-10, append-only):** Confirmed by the owner on 2026-10-10. The mapping stands as written: the controlled-drug witness must be a Vet or Nurse and not the dispenser (OQ-1); Reception may dispense (OQ-1); an Administrator may reverse a dispensing but cannot correct the register (OQ-8); remote renewal without an open visit is allowed (OQ-3). Closes docs/backlog/PHM.md row 33.

---

## ADR-154 — Dispensing snapshots a price per line

**Date:** 2026-10-09 (HO-PHM-001)
**Status:** Active
**Decision:** **Dispensing snapshots a price per line** for Billing; an unpriced SKU does not block care — the line's snapshot is null and the response says so
**Reason:** ADR-049's contract; blocking a medicine over a missing price is the wrong failure (OQ-6)

## ADR-216 -- Amended 2026-10-10 (owner decisions D1, D2; HO-ORD-001-FU1; backlog ORD 43, 47)

**Status:** Active (append-only amendment)
**Amendment D1:** a mobile held by a **Merged** account is no longer refused. `AccountsCounterCustomerFinder` follows `MergedIntoCustomerId` (chains, at most 10 hops, loops refused) through Accounts' published `FindCustomersQuery` + `GetStaffCustomerQuery` to the surviving account, and the sale belongs to it. `400 AccountNotActive` (cashier-facing message) remains only when the surviving account is itself not Active (Anonymised) or none is recorded.
**Amendment D2:** the buyer is found or registered **before every business check** (right after the branch and the mobile shape are validated), not after them. A walk-in account **intentionally outlives a refused sale** (stock, prescription, tenders, card-number text): the person's name and mobile were given to us, the cashier resubmits, and the retry reuses the account (ORD 47). A request that is not shape-valid (blank name, invalid mobile) stores nobody. The sentence "a refused sale leaves no account behind" in the original decision is withdrawn. A later online registration with that mobile still gets Accounts' `MobileNumberAlreadyRegistered` + reset offer, which completes the same account -- no duplicate.

## ADR-204 -- Amended 2026-10-10 (owner decision D4; HO-ORD-001-FU1; OQ-7)

**Status:** Active (append-only amendment)
**Amendment:** the second approval applies to **bank-transfer confirmations only**. Cash-on-delivery collections and counter tenders above Billing's `billing.paymentApprovalThreshold` are accepted at once: Ordering sets the additive `RecordPaymentCommand.ApprovalNotRequired` (in-process only; `StaffPaymentsController` never binds it, so payments staff record in Billing keep BR-17). Cash already in the till or in the courier's hands cannot be "held"; it is reconciled against remittances and the till (US-BIL-06).

## ADR-217 -- The delivery charge's tax is configured, default 0 %

**Date:** 2026-10-10 (HO-ORD-001-FU1, owner decision D3)
**Status:** Accepted by the owner on 2026-10-10
**Decision:** The delivery charge is invoiced under the tax class with code `DELIVERY`, seeded at 0 % by the data-only migration `SeedDeliveryTaxClass`. An Administrator changes the delivery tax with the existing effective-dated tax-rate endpoint (`TaxClasses.Manage`); no new setting, endpoint or permission. `CheckoutPricing` adds `round-half-away(delivery x rate)` to the order's `TaxTotal` and `GrandTotal`; the invoice's delivery line carries the same tax. `DeliveryTaxClassNotConfigured` is removed; a missing `DELIVERY` class falls back to any active 0 % class, and with none a delivery order is a configuration fault (5xx, nothing written), not a customer refusal.
**Reason:** the earlier fail-closed `400` made delivery depend on an admin creating a class; the owner wants it to work at 0 % and be tunable later. A tax class already carries the effective-dated rate history ADR-049 requires and Billing already reads it for the invoice, so a parallel setting would have had to be kept in step with it.
**Consequence:** the order does not store the delivery tax separately (`PlaceOrderDto.DeliveryTaxAmount` is validation only; the invoice carries it). A held transfer confirmed after a rate change is invoiced at the rate in force at confirmation, as for line tax.

**Amendment (2026-10-10, FU1 fix round, owner approved; append-only -- supersedes the fallback and consequence sentences above):**
- There is **no fallback**: delivery is always invoiced under the `DELIVERY` class. The class cannot be deactivated (`DeliveryTaxClassRequired`; tax classes cannot be deleted). If it is missing, inactive or rate-less anyway, a delivery quote with a charge above zero is blocked with `DeliveryTaxNotConfigured` (placement refuses the same way, `400`); `BuildCharges` keeps a 5xx guard; `/health` reports it as Degraded.
- The delivery tax is **stored on the order** at placement (`sales_order.delivery_tax_amount`, bounded `0 <= tax <= delivery charge`). A held transfer confirmed later is invoiced with the `DELIVERY` rate **as of the order's `PlacedOn`** (`IssueInvoiceFromChargesCommand.PinnedTaxClassId` / `PinnedTaxRateAsOf`), i.e. the delivery tax the customer was quoted, not the rate in force at confirmation.
- **Line tax is unchanged**: Billing still prices it at issue at the rate in force then, so a held transfer confirmed after a *product* tax-rate change can differ from the order total. Owner question: pin line tax to `PlacedOn` the same way?

**Owner tax intent (2026-10-10, append-only):** "Tax starts at 0 % for everything; the business will raise rates later as it grows — rates must stay configurable by an Administrator." This answers the line-tax question above for now: pinning product line tax to `PlacedOn` is deferred (backlog `docs/backlog/ORD.md` row 62) because it only matters once a non-zero product tax rate exists; it must be done before any product rate is raised. Rates stay changeable only through the existing effective-dated tax-rate endpoint (`TaxClasses.Manage`).

---

## ADR-268 — Orders and prescriptions readers: Orders' `canReorder` rule wins; no tracking number; prescriptions are counts and dates

**Date:** 2026-10-10 (HO-POR-001)
**Status:** Active
**Decision:** (1) `OrdersSectionReader` reads Orders' `GetCustomerOrdersQuery` only. `canReorder` is true for `Cancelled`, `Delivered`, `Returned`, `Refunded` -- the same statuses Orders' order-detail `canReorder` uses -- because OQ-2 said Orders' own rule wins over its "every status except `pending_payment`" default; the set is named once, in the reader, and is removed if Orders adds the flag to its list item. (2) The order item carries no `trackingNumber`: Orders' list item lacks it and its detail query also holds bank-transfer instructions (not pulled per order into every dashboard read). (3) `PrescriptionsSectionReader` reads Pharmacy's `GetCustomerPrescriptionsQuery { Scope = "active" }` and outputs `catId`, `catName`, `activeCount`, `earliestEndsOn` only (ADR-262). (4) Re-order is not built (ADR-263, §20.4).
**Reason:** One reader is the only place that knows an upstream shape (ADR-259); a dashboard that offers an action its owning page withholds is a defect; the clinical-content line (ADR-262) is enforced by the output type having no property that could hold a medicine or a dose.
**Update 2026-10-10 (owner-delegated decision 2026-10-10, owner to review):** items (1) and (2) are superseded: Orders added `trackingNumber` and `canReorder` (its own `CustomerOrderRules.CanReorder`) to its list item; the reader passes both through and the portal's four-status set is deleted. Item (4) is superseded: re-order is retired (ADR-263), not blocked.

## ADR-157 — `Surgery` context in schema `clinic`

**Date:** 2026-10-11 (HO-SRG-001)
**Status:** Active
**Decision:** Namespace segment `Surgery` (not `Clinic`), schema `clinic` shared with Appointments and Consultation. Statuses are Appendix C's: `Scheduled -> PreOp -> InTheatre -> Recovery -> Discharged`, plus `Cancelled`. An abandoned procedure is `Outcome = Abandoned`, not a separate state.
**Reason:** HO-APT-001 put appointments under a `Clinic` segment (ADR-067); `.ai/bounded-contexts.md` lists Surgery as its own context.
**Build note:** the shared enumeration for the follow-up kind is `SurgeryFollowUpKind` because HO-EMR-001 already owns `FollowUpKind`.

## ADR-158 — A surgery's time is an appointment row

**Date:** 2026-10-11 (HO-SRG-001)
**Status:** Active
**Decision:** The surgeon is the appointment's performer and the theatre its room. The surgery and its appointment are created and changed in ONE save (`BookForSurgery`, `MoveForSurgery`, `CancelForSurgery`, `CompleteSurgerySlot`): a recorded rule-1 exception for this pair only. Every other appointment transition refuses an appointment that has a `SurgeryId` (`AppointmentManagedBySurgery`); HO-APT-001's staff and customer endpoints cannot change a surgery behind its back.
**Reason:** reuses HO-APT-001's race-free exclusion constraints (ADR-068) instead of building a second mechanism, and puts surgeries on the calendar.

## ADR-159 — Staff occupancy table `clinic.appointment_staff_time`

**Date:** 2026-10-11 (HO-SRG-001)
**Status:** Active
**Decision:** One row per occupying staff member of an occupying appointment (the performer, and the assistant of a surgery), kept by `AppointmentAggregate.SyncStaffTime()` (called from every status/time/performer/assistant change; U-A8 checks every method). Exclusion constraint `ex_appointment_staff_time` (`staff_account_id WITH =`, `tstzrange(starts_on, ends_on, '[)') WITH &&`, `WHERE occupies`). The migration backfills existing appointments (`BackfillSql`, I-A3).
**Reason:** an exclusion constraint compares one column against itself; assistant against performer needs both in one column. On an appointment route a hit means the performer is busy; on a surgery route it is `StaffBusy`.

## ADR-160 — Pre-op checklist

**Date:** 2026-10-11 (HO-SRG-001)
**Status:** Active
**Decision:** Five fixed owned items are created at booking. Weight and consent complete from the cat context weight event and EMR consent events; fasting is completed by a nurse or vet; blood work may be waived by a vet with a reason; consent is never waivable. Start needs every item `Done` or `Waived`. A cat's weight older than the configured window reopens the item on reschedule.
**Reason:** AC-3 "checklist enforced"; weight and consent facts live in other contexts and copying them by hand drifts.

## ADR-161 — Consent is re-read at start; the override is vet-only and permanent

**Date:** 2026-10-11 (HO-SRG-001)
**Status:** Active
**Decision:** Start re-reads consent through `ISurgeryConsentReader` (EMR's published `ConsentRules.IsValidFor`) for the kinds `Surgery` and `Anaesthesia` against the surgery's appointment; the checklist flag is not trusted. `start-with-consent-override` is vet-only, needs a 10 to 1000 character reason, is stored on the aggregate and audited as a `BusinessEvent` (`SurgeryConsentOverridden`).
**Reason:** BR-5. A flag can be stale; the override must be attributable for good.
**Open point (owner):** this build assumes consent of BOTH kinds (Surgery and Anaesthesia) is required (backlog SRG).

## ADR-162 — Anaesthesia log is an append-only root

**Date:** 2026-10-11 (HO-SRG-001)
**Status:** Active
**Decision:** One row per entry (`AnaesthesiaLogEntryAggregate`); a correction is a new linked entry (`CorrectsEntryId`), never an edit. No `PUT`, `PATCH` or `DELETE` route exists (I-D5). A `BEFORE UPDATE OR DELETE` trigger and a `BEFORE TRUNCATE` trigger refuse changes with SQLSTATE `P0001` and a message starting `immutable:`; the Api filter maps that to `409 RecordIsImmutable`.
**Reason:** concurrent theatre logging without `409`s; D-5-grade immutability for a clinical record.

## ADR-163 — Drug entry, stock issue and register entry commit in one save

**Date:** 2026-10-11 (HO-SRG-001)
**Status:** Active
**Decision:** The handler enlists HO-IMP-001's `IStockLedger.DeductAsync` and HO-PHM-001's `IControlledDrugRegister.Enlist` without saving; the single repository save commits the log entry, the ledger rows and the register entries together (F23). An expired batch is refused with no override; an out-of-FEFO batch needs a reason; a stock shortfall is recorded on the entry (`StockShortfall`), not refused.
**Reason:** FR-SRG-8 needs all or none. Refusing to document a drug already given would falsify the clinical record.
**Gap (backlog SRG):** the ledger accepts whole units only in v1 (HO-IMP-001 OQ-19), so fractional vial draws are refused.

## ADR-164 — Witness

**Date:** 2026-10-11 (HO-SRG-001)
**Status:** Active
**Decision:** A witness is a second staff member who types their OWN identifier and password into the request (`IStaffWitnessVerifier`, HO-PHM-001's rule); witness differs from the administering vet and from the caller. The PHM witness rate-limit policy is reused.
**Reason:** BR-7 with identifier and password only (no OTP).

## ADR-165 — Surgical report is its own root

**Date:** 2026-10-11 (HO-SRG-001)
**Status:** Active
**Decision:** `SurgicalReportAggregate` with `Id = SurgeryId`. Draft is editable (`ExpectedVersion`); finalisation is vet-only and names every missing field (`ReportIncomplete`); after finalisation a `BEFORE UPDATE OR DELETE` trigger on the report and on its addenda refuses changes. An addendum inserts a row and never touches the report row (the version and `xmin` stay put).
**Reason:** FR-SRG-5, BR-11, D-5.

## ADR-166 — Neuter status is set by a Cat-context handler

**Date:** 2026-10-11 (HO-SRG-001)
**Status:** Active
**Decision:** `SterilisationCompletedEvent` (ids and the branch-local date only) is handled by `SetNeuterStatusOnSterilisationHandler` in the Cat context. It is idempotent, never fails the request, and writes a `NeuterStatusNotApplied` audit row when the cat is deceased or the cat's own rule refuses the date. `ApplySterilisationOutcomeCommand` is the repair path (`CatIsDeceased` is refused plainly). The surgery never writes `clinic.cat`.
**Reason:** FR-SRG-6 without crossing the aggregate boundary.

## ADR-167 — Per-service protocols with shipped defaults

**Date:** 2026-10-11 (HO-SRG-001)
**Status:** Active
**Decision:** `SurgeryProtocolAggregate` (one per surgical service): fasting text and hours, post-op text, follow-up rules (kind, day offset, window). Defaults ship in `SurgeryProtocolDefaults` (the SRS examples). The text is snapshotted into the surgery (fasting at booking, post-op at discharge).
**Reason:** a later edit must not rewrite what an owner was given.

## ADR-168 — Recovery status is a sub-state of Recovery; the board is polled

**Date:** 2026-10-11 (HO-SRG-001)
**Status:** Active
**Decision:** `Recovering`, `Stable`, `ReadyForDischarge` with history. `GET /api/staff/surgeries/recovery-board` is polled every 30 s and costs at most two SQL commands (I-F2). Discharge requires `ReadyForDischarge`, except for a deceased cat.
**Reason:** FR-SRG-10 "near real time" meets UI-5 without a push channel.

## ADR-169 — Surgery messages

**Date:** 2026-10-11 (HO-SRG-001)
**Status:** Active
**Decision:** Six templates, all Transactional. Booked, rescheduled, cancelled and post-op are `Urgent`; the pre-op reminder and the follow-up reminder are `Normal` and timed (`SendAfter`/`NotAfter`/`SubjectStamp`) with the guards `SurgeryPreOp` (the `ScheduleStamp`) and `SurgeryFollowUp` (the follow-up still `Pending`, the cat alive, the owner unchanged). Messages are enqueued by the command handler through `INotificationOutbox` in the same save, never from an event handler.
**Reason:** FR-NTF-3; the recipient is acting at that moment.

## ADR-170 — No deposit and no payment on a surgery

**Date:** 2026-10-11 (HO-SRG-001)
**Status:** Active
**Decision:** The surgery holds a `PriceSnapshot` (amount, basis `Standard` or `Campaign`, campaign id, eligibility note). `GetSurgeryChargeableItemsQuery` exposes it with the effective drug entries to HO-BIL-001.
**Reason:** the SRS asks for none; v1 has no gateway; consistent with ADR-073.

## ADR-171 — Campaign price lists

**Date:** 2026-10-11 (HO-SRG-001)
**Status:** Active
**Decision:** `SurgeryCampaignAggregate`: unique code, branch-local inclusive dates, branches (none = all), one fixed price per sterilisation service, optional `MaxCases`. Staff apply it at booking with a mandatory eligibility note; each refusal is `CampaignNotApplicable` with its own reason. Prices lock once a non-cancelled surgery is booked under it (`CampaignPriceLocked`). `GET .../{id}/cases` lists the active branch's cases with the standard price in force when each was booked.
**Reason:** FR-SRG-11 "distinct price list with its own reporting".
**Known limit (backlog SRG):** the cap is judged by a count at booking, so two simultaneous bookings for the last place can both pass.

## ADR-172 — Theatre services are scheduled only as surgeries

**Date:** 2026-10-11 (HO-SRG-001)
**Status:** Active
**Decision:** HO-APT-001's booking paths refuse services whose `RequiredRoomKind == Theatre` (`ServiceNotBookable`, G6).
**Reason:** otherwise a spay could be booked as an ordinary appointment with no assistant, checklist or consent gate.

## ADR-173 — Surgery permissions

**Date:** 2026-10-11 (HO-SRG-001)
**Status:** Active
**Decision:** Inferred from Appendix D. View: Reception, Nurse, Vet, Administrator. Schedule: Reception, Vet, Administrator. Admit: Reception, Nurse, Vet. Checklist and start: Nurse, Vet. Risk assessment, consent override, drug logging, ending the procedure and the report: Vet only. Vitals: Nurse, Vet. Recovery and discharge: Nurse, Vet. Protocols: Vet, Administrator. Campaigns: Administrator. The Administrator has no clinical write.
**Reason:** the mapping is a judgement and must be visible (OQ-6: nurses log vitals and milestones, not drugs).

---

## HO-XCT-001 -- ADR-297 to ADR-316 (Cross-cutting and Technical)

Recorded on `ho/XCT-001-build` (2026-10-12). The full reasoning is in `docs/handover/HO-XCT-001.md` section 9; this is the binding summary, appended phase by phase (A: 297-300, 316; B: 302, 314; C: 301, 303-305; D: 306-308; E: 309-310; F: 311-313, 315).

## ADR-297 -- The `Operations` Bounded Context
**Date:** 2026-10-12 · **Status:** Accepted (handover)
**Decision:** folder and namespace segment `Operations`, schema `ops`, owning `ErasureRequestAggregate`, `RetentionRunAggregate`, `RestoreTestRecordAggregate`, `WebVitalBatchAggregate` (and the read model `BackupRunReadModel`). Retention infrastructure lives in `Infrastructure.Retention`, backup reads in `Infrastructure.Operations`.
**Consequence:** not yet in `.ai/bounded-contexts.md` / `.ai/namespace-conventions.md` (OQ-2, a separate architect task).

## ADR-298 -- Every Table Is Classified
**Decision:** every table in the EF model is classified in `RecordClassification` as one `RecordClass` with one `HardDeletePolicy` under the ordered rules of section 8.2. `RecordClassificationConventionTests` enumerates the model (owned tables included) and fails on any unclassified table, and on any stale entry.

## ADR-299 -- Hard Deletes of `Forbidden` Tables Are Refused at Three Layers
**Decision:** (1) the database: `retention.refuse_hard_delete()` behind a `BEFORE DELETE` row trigger and a `BEFORE TRUNCATE` statement trigger on every `Forbidden` table; (2) EF: `HardDeleteGuardInterceptor`; (3) the build: a source scan allowing `ExecuteDelete` and raw `DELETE`/`TRUNCATE` only in `RetentionPurger` and migrations. `AggregateOnly` owned tables get no trigger.

## ADR-300 -- Production Runs as a Non-Owner, Non-Superuser Role
**Decision:** `catecommerce_migrator` owns the schemas and migrates; `catecommerce_app` has DML only (no `TRUNCATE`, no DDL, no ownership); `catecommerce_backup_reporter` has `INSERT` on `ops.backup_run` only. Outside Development/Testing the host refuses to start if the current role is a superuser or owns a solution table (`DatabaseRoleGuard`). `deploy/database/roles.sql`.

## ADR-301 -- Retention Periods Are Settings; the End of a Statutory Period Deletes Nothing
**Decision:** the nine settings of section 10.2 (Administrator-only, audited). The end of a clinical, financial, regulatory or audit period produces a *review-eligible* count only; held personal data inside those records is anonymised when its hold ends. A wrong default cannot destroy anything.

## ADR-302 -- The Erasure Ledger
**Decision:** `CustomerAccountAnonymisedEvent` opens an `ErasureRequestAggregate` (id = customer id) with one scope per registered `IPersonalDataEraser`; the retention worker executes due scopes (Pending, Failing past its back-off, Held past its date). Erasers save their own context's aggregates and the ledger records the outcome: a **recorded rule-1 exception**, safe because erasers are idempotent.

## ADR-303 -- `RetentionWorker`
**Decision:** one `BackgroundService`; erasure pass every 15 minutes; daily passes at `retention.sweepHour` in the main branch's time zone; a run is a `RetentionRunAggregate` with one pass result per pass and one `BusinessEvent` summary (excluded from change capture). Bounded batches, one command per unit, an error in a unit never stops the pass. Off in Testing; tests call `RetentionWorkCycle`.
**Amendment (2026-10-12):** a purge source with no retention rule here is **not scheduled** (the `stored-file:<purpose>` sources HO-FIL-001 registers include clinical purposes); only `boarding-media` has a rule (D-10).

## ADR-304 -- Purge Is Hard Delete of `Purgeable` Classes Only
**Decision:** by `RetentionPurger` methods with fixed tables (sessions, reset tokens, web-vital batches, backup-run log) or by registered `IRetentionPurgeSource`s. Each batch writes one `BusinessEvent` row with the pass, cutoff and count; rows are not audited individually.

## ADR-305 -- Notification Content Is Redacted, Not Deleted
**Decision:** `retention.notificationContentMonths` after creation, via `NotificationAggregate.Redact(Retention)` (also nulls the destination and the unsubscribe link); channel, template, status, timestamps and recipient id are kept. It never overrides an anonymisation and is idempotent.

## ADR-306 -- The Offline Snapshot Is One Composite, Permission-Filtered Read
**Decision:** `GET /api/staff/offline-snapshot` for the active branch's local day; minimal fields; `Cache-Control: no-store, private`; strong ETag over the content (excluding `generatedOn`); `304` on match; `validUntil` = end of the local day + 2 h; sections the caller may not see, or that have no source, are listed as omitted (`NotPermitted` / `NotAvailable`). `[PassiveSession]`.

## ADR-307 -- Issuing Snapshot Content Is Audited
**Decision:** the controller runs the query and, on a non-`304` result, dispatches `RecordOfflineSnapshotIssuedCommand` (one `BusinessEvent` row: branch, local date, section counts, ETag). A `304` writes nothing. A query never writes.

## ADR-308 -- `GET /api/system/ping`
**Decision:** public, `[PassiveSession]`, `204`, `Cache-Control: no-store`, no body, 120/min/IP. The browser's server-reachability probe.

## ADR-309 -- Web-Vitals Ingestion Is Anonymous Telemetry
**Decision:** `POST /api/telemetry/web-vitals`: public, CSRF-exempt (a beacon cannot send the header; a stale session is treated as anonymous), 30/min/IP, at most 10 samples and 4 KB, JSON or `text/plain`. It stores no IP, user id, session, user agent or full URL; the handler replaces any identity with the anonymous context. Excluded from change capture; purged after `retention.webVitalsDays`. One generic error code so the endpoint is not an oracle.

## ADR-310 -- p75 Is Computed in PostgreSQL
**Decision:** `percentile_cont(0.75) WITHIN GROUP (ORDER BY value)` per route template, metric and viewport class over at most 93 days; a verdict needs at least 50 samples; budgets in `WebVitalBudgets` (LCP 2500 ms, INP 200 ms, CLS 0.1). Only the budgeted metrics are judged (FCP and TTFB are collected but have no budget).

## ADR-311 -- Backup Is pgBackRest With Continuous WAL Archiving
**Decision:** `archive_timeout = 60 s`; weekly full, daily differential; two repositories: `repo1` local, `repo2` off-site S3-compatible encrypted with `aes-256-cbc` (passphrase from the secret store). `deploy/backup/`, provider-neutral (NFR-CMP-3). **Measured** by the drill: data loss after an unclean kill 27 s.
**Limit (2026-10-12):** the S3 transport was not exercised (no S3-compatible server image could be pulled); the drill uses a posix `repo2` with the same cipher.

## ADR-312 -- `ops.backup_run` Is Written Only by the Backup Job
**Decision:** through `catecommerce_backup_reporter` (`INSERT` only); read by the API as a read model. A recorded exception to "every write goes through an aggregate": the job has no application identity and the row describes the job.

## ADR-313 -- A Restore Test Is an Append-Only, Attributed Record
**Decision:** entered by an Administrator from the drill JSON; **RPO, RTO and the outcome are computed by the aggregate** (passed only if every check passed, RPO <= 15 min, RTO <= 240 min). The next test is due `operations.restoreTestIntervalDays` (92) after the last *passed* test; a failed test does not reset it. A correction is a new record naming the one it supersedes.

## ADR-314 -- Restores and Erasure
**Decision:** each night the backup job exports an erasure journal (customer ids and requested-on only) beside both repositories; after any restore the runbook requires `POST /api/admin/retention/erasures/reapply`, which re-anonymises each account idempotently with source `RestoreReconciliation` (a recorded rule-1 exception). Backups expire within 35 days.
**Note (2026-10-12):** pgBackRest 2.59 has no command that stores a file, so the export script copies the file beside repo1 and uploads an encrypted copy beside repo2 itself (`curl --aws-sigv4` for S3).

## ADR-315 -- Actions
**Decision:** `Offline.FrontDeskSnapshot`, `Retention.View`, `Retention.Run`, `Customers.Erase`, `Operations.View`, `Operations.RecordRestoreTest`, `Telemetry.View`, in the one `ActionRoles` table. "Compliance officer" and "security officer" map to Administrator (plus Accountant for read-only retention views).

## ADR-316 -- The Erasure and Classification Contracts Bind Every Future Context
**Decision:** C-ALL-1..4. U-A1 (every table classified) and U-B6 (every personal-data context has an eraser or an exemption) are the enforcement; a context cannot merge while either fails.


---

## HO-XCT-001 -- gate fix round amendments (2026-10-12, append-only)

Owner decisions of 2026-10-12 (integrator-authorised): "Reclassify them properly" (ARCHITECTURE HIGH); "Fix both before merge" (the two SECURITY mediums). Each line below amends the ADR it names; the originals above are not edited.

**ADR-297 amendment (2026-10-12).** The schema `ops` is **shared**: Ordering's `ops.idempotency_record` and `ops.inbound_webhook_receipt` already live there (`RecordClassification.Operations.cs`). The Operations context owns the four roots, `BackupRunReadModel` and the tables of `ops` named in section 8.1; it does not own the whole schema.

**ADR-298 amendment (2026-10-12) -- classification and enforcement are one statement.** `billing.invoice_line`, `inventory.purchase_order_line`, `inventory.consignment_cost`, `inventory.consignment_exchange_rate`, `inventory.consignment_landed_cost_line` and `inventory.consignment_hs_code` are **`CurrentState` / `AggregateOnly`** in `RecordClassification.Billing.cs` and `RecordClassification.Inventory.cs`, edited under integrator authority (owner: "Reclassify them properly"). Their areas' shipped aggregates replace these rows while a document is a draft or being re-costed (`InvoiceAggregate.SetDraftLines`, `PurchaseOrderAggregate.ReplaceDraft`, `ConsignmentAggregate.ReplaceCostsAndRates` / `RecordLandedCostAllocation` / `UpdateDetails`); the issued-document immutability stays with their areas' own triggers (for example `trg_invoice_line_immutable`). `RetentionGuardExemptions` and every use of it are deleted: no table may be `Forbidden` in the registry and deletable in enforcement. `GET /api/admin/retention/schedule` now reports them as `CurrentState`. `ClassificationEnforcementAgreementTests` pins the six.

**ADR-299 amendments (2026-10-12).**
- *The six tables above* carry no blanket retention trigger (they are `AggregateOnly`); the EF guard allows their delete only through a tracked, non-deleted owner.
- *`files.stored_file` change-tracked delete.* `HardDeleteGuardInterceptor.ChangeTrackedPurgeAllowList` allows a change-tracked delete of this one `Purgeable` root, against the rule that a Purgeable root is deleted by `ExecuteDelete` in `RetentionPurger` only. Reason: HO-FIL-001 section 8.7 / ADR-318 and README C-5 make File Storage the purge source for that table, and `IStoredFileEraser` must delete the stored object FIRST and the row SECOND, honouring `LegalHold`, in one place shared by the delete command and `StoredFilePurgeSource`. It is limited to that table; every other Purgeable root still throws.
- *`DedicatedlyGuardedTables`.* `audit.audit_entry` (HO-ACC-001 F33) and the two controlled-drug register tables `pharmacy.cd_register_entry` / `cd_register_seal` (HO-PHM-001 ADR-144) are `Forbidden` and carry their own complete append-only DELETE and TRUNCATE triggers, so `AddRetentionGuards` does not attach a second pair. I-A3 accepts the area's triggers for these three tables.
- *The `AddRetentionGuards` table list is frozen inside that migration* (`AddRetentionGuards.TablesAtMigration`, 113 tables), not in a shared class, so a later edit of any list cannot change what a historical migration did. `ClassificationEnforcementAgreementTests.TheFrozenMigrationList_HasNotBeenEdited` pins its count and hash. Later tables attach their triggers in their own migrations with `RetentionGuardSql.AttachTo`.
- *Interceptor order.* `HardDeleteGuardInterceptor` is registered third, after `AuditEntryImmutabilityInterceptor` and `ControlledDrugRegisterImmutabilityInterceptor`, which were moved from after change capture to before it. The intent (the guard runs before change capture, so a refused delete is never audited as if it happened) holds.

**ADR-301 amendment (2026-10-12).** Q3 reports the end-of-period action of `Regulatory` as `ReviewOnly` (not `Redact`): only held personal data inside a regulatory record (an acceptance IP) is blanked when its hold ends.

**ADR-302 amendments (2026-10-12).**
- The ledger-opening event handler dispatches C1 in a **new scope**, not the same one (the anonymisation has already committed, ADR-021; precedent: Billing's `CaptureChargesOnAppointmentCompletedHandler`). C1 itself, when its insert loses a race to a concurrent opener, "treats it as exists, reloads and retries once" in a fresh scope.
- *Identity check on a staff-taken erasure (C3) is a fixed list of codes* (`ErasureIdentityCheck`: `RegisteredMobileCallback`, `RegisteredEmailReply`, `InPersonPhotoId`, `WrittenRequestSigned`), and C3 has **no free-text field**: the audit row (immutable, and so never erasable) holds the channel and the code only, so a member of staff cannot type the identifiers whose erasure the row records. (SECURITY gate medium.)

**ADR-314 amendments (2026-10-12) -- no re-exposure through a restore.** (1) The erasure journal is exported **every 10 minutes** (was daily), so the gap matches the RPO. (2) The runbook's first restore step is to export the journal from the CURRENT database, if readable, before `--delta` overwrites it. (3) After a restore the application is started with its background workers OFF (`deploy/backup/restore-mode.env`), the reapply runs through the API, and only then are the workers turned on; the former "reconcile before the application is started" wording is removed because the reapply is an API call. (4) The rendered `pgbackrest.conf` is created `0600` (`render-config.sh`) and S3 credentials reach `curl` on stdin, not on its command line. (5) `DatabaseRoleGuard` tests `pg_has_role(..., 'MEMBER')`, so a login role that is a member of the owning role without inheriting it is also refused.

**ADR-298 amendment correction (2026-10-12, ARCHITECTURE re-check finding 43; append-only).** The 2026-10-12 amendment above says the issued-document immutability of the six reclassified tables "stays with their areas' own triggers". That is true only for `billing.invoice_line` (`trg_invoice_line_immutable` allows a DELETE only for a draft, non-frozen line). The five **inventory** tables (`purchase_order_line`, `consignment_cost`, `consignment_exchange_rate`, `consignment_landed_cost_line`, `consignment_hs_code`) have **no trigger** of their own and never had one: their protection is the aggregate methods, the EF guard (`AggregateOnly`: a delete only through a tracked, non-deleted owner) and `DataChange` capture, so a raw SQL DELETE under the application role is not refused for them. Enforcement is unchanged from before the reclassification; only the wording overstated it. Status-conditional DELETE triggers for those five tables (modelled on `fn_invoice_line_immutable`) are an owner / HO-IMP-001 decision (backlog `docs/backlog/XCT.md` row 49). Note also that `ConsignmentAggregate.RecordLandedCostAllocation` re-allocates "at any time", so C-ALL-4 holds for these tables only while their parent is a draft.

---

## ADR-277 — The `Reporting` bounded context

**Date:** 2026-10-13 (HO-RPT-001)
**Status:** Active
**Decision:** Reporting is a bounded context (namespace segment `Reporting`, schema `report`). It owns two tables (`report.report_schedule`, `report.scheduled_report_run`) and the `report.rv_*` views. It writes nothing outside `report` except audit entries (`AuditEntryAggregate`), notification requests (`INotificationOutbox`) and, for a scheduled report file, a stored file through HO-FIL-001's `IFileStore` and `RegisterStoredFileCommand`. The one Reporting migration that touches another schema is `AddNotificationAttachments` (four nullable columns in `notify`, the HO-NTF-001 amendment).
**Reason:** the area that reads everything must be the one area that provably writes nothing elsewhere. U-X5 reads every Reporting migration.

## ADR-278 — Reports read migration-owned SQL views

**Date:** 2026-10-13 (HO-RPT-001)
**Status:** Active
**Decision:** Each `report.rv_*` view is one embedded SQL file `Persistence/ReportingViews/Sql/rv_x.vN.sql`, run by a migration; changing a view is a new file version and a new migration. Enumeration values are substituted into the SQL from the enumerations (`{{TOKEN}}`), never typed as literals. Keyless entities use `HasNoKey().ToView(name, "report")`. A view's column list is the reporting contract. Views added beyond the handover's list: `rv_branch`, `rv_staff_branch`, `rv_credit_note`, `rv_refund`, `rv_order_return`. `rv_reorder_rule` was not built (R7 reuses HO-IMP-001's own reorder calculation).
**Reason:** ADR-012 removed projections; a copy of the ledger or the invoices would drift from them.

## ADR-279 — Read-only execution

**Date:** 2026-10-13 (HO-RPT-001)
**Status:** Active
**Decision:** Every report and dashboard tile runs inside `IReportingReadScope`: a `READ ONLY` transaction, `SET LOCAL statement_timeout`, always rolled back. A statement timeout (57014) becomes `ReportTimedOutException` and a `400 ReportTimedOut`; a write attempt fails in the database (25006) and is logged, never mapped. U-A3 scans the source for a write path; the only Reporting write port is `EfScheduledReportRunStore`, which saves Reporting's own run row.
**Reason:** "reports never write to another module's tables" becomes a database guarantee.

## ADR-280 — One catalogue, one pipeline

**Date:** 2026-10-13 (HO-RPT-001)
**Status:** Active
**Decision:** `ReportCatalogue` (Shared) defines every report from per-area `*Definitions` fields discovered by reflection (ADR-341). One generic `RunReportQueryHandler` applies the branch scope, the filter validator, restricted-column removal, sort, paging and the export caps; one `IReportSource` per key (U-A2 enforces 1:1); one CSV writer, one PDF renderer. Twenty-one definitions are delivered (R1-R20 and R27). R21 and the subscription part of R22 (HO-SUB-001), the cross-line part of R22 (needs the boarding line), R23 (HO-ORD-001 has no cart reminders) and R24-R26 (HO-BRD-001) are not delivered yet.
**Reason:** FR-RPT-1 "every report" is then structural.

## ADR-281 — Report access

**Date:** 2026-10-13 (HO-RPT-001)
**Status:** Active
**Decision:** One action per report category (`Reports.ClinicOperations/Clinical/Boarding/Stock/Sales/Financial/Growth.View`), a `Reports.Any` marker on every report route, `Reports.Schedule` and `Dashboard.View`. The report's own category action is enforced in the handler with `ReportAccessPolicy`, answering the same `403 ActionNotPermitted` body as the filter and writing the same denial audit. The Vet also gets clinic operations (OQ-7). Cost and margin columns need `Reports.Financial.View` and are removed, not blanked. Branch scope: the active branch; assigned branches only; `all` for Administrator and Accountant. The Sales category (R10, R11, R27) is held by ShopManager and Administrator only, so the Accountant cannot open R10 on screen or receive it on a schedule (as specified; recorded in backlog RPT).
**Reason:** NFR-SEC-2; "nothing I am not permitted to see" also applies inside a report.

## ADR-282 — Report time

**Date:** 2026-10-13 (HO-RPT-001)
**Status:** Active
**Decision:** `from`/`to` are inclusive branch-local dates in the report branch's zone; `all` uses the main branch's zone and says so in a note. Relative periods and schedule times come from pure domain services (`ReportPeriodResolver`, `ReportScheduleCalculator`). A local day is converted to instants with the zone's rules: a skipped local time moves to the first valid instant, a repeated one takes the first occurrence.
**Reason:** ADR-045; a report that shifts a day at midnight UTC is wrong for a clinic six hours ahead.

## ADR-283 — Revenue

**Date:** 2026-10-13 (HO-RPT-001)
**Status:** Active
**Decision:** Revenue is recognised on invoice issue, net of line and allocated discounts, excluding tax, with credit notes negative on their own issue date; never summed across currencies. The business line comes from HO-BIL-001's own `business_line` and `item_kind` on each invoice line through `BusinessLineRules` (an unknown value is `Other`, never dropped). The handover assumed an `InvoiceLineKind` enumeration; none exists. Visit, dispense and surgery revenue is not on invoices until HO-BIL-001 Phase J, and the R9 and R12 notes say so.
**Reason:** one definition shared by every revenue report, or R9 and R10 will not reconcile.

## ADR-284 — Cost and margin

**Date:** 2026-10-13 (HO-RPT-001)
**Status:** Active
**Decision:** Cost of goods comes from sale-type stock-ledger entries linked to invoice lines by the shared source-line reference, at the batch's **landed** unit cost (`consignment_landed_cost_line`). No purchase-cost fallback. A line is costed only if every contributing batch has a landed cost; margin is null when coverage is zero; coverage % is always shown.
**Reason:** a blended guess would make margin look complete when it is not.

## ADR-285 — Exports

**Date:** 2026-10-13 (HO-RPT-001)
**Status:** Active
**Decision:** CSV (UTF-8 with BOM, CRLF, the shared injection guard, at most 50 000 rows) and PDF (QuestPDF, at most 5 000 rows) are rendered from the same `ReportResult` as the screen, for an interactive export and for a scheduled attachment alike. `413` beyond the caps. Every interactive export is audited (`ReportExported`: filters, row count, column keys, never row data) and the audit write must succeed before the first byte is streamed.
**Reason:** exported data leaves the system; who took what must be known.

## ADR-286 — Role dashboards are server-composed

**Date:** 2026-10-13 (HO-RPT-001)
**Status:** Active
**Decision:** `DashboardTiles` names 18 tiles with the roles they serve; each is gated by an existing action and is not computed when the gate fails. Today at the active branch, live, uncached. The three `boarding.*` tiles have no source (HO-BRD-001 is not merged): they are absent, not zero.
**Reason:** a client-side role map drifts and leaks.

## ADR-287 — Schedules

**Date:** 2026-10-13 (HO-RPT-001)
**Status:** Active
**Decision:** `ReportScheduleAggregate` with `NextRunOn` from `ReportScheduleCalculator`; a unique index `ix_scheduled_report_run_slot` on (schedule, due time) for `Schedule` runs that are `Generated` or `Purged` makes a slot run exactly once (the filter is written from the enumerations). A 23505 on it is success with nothing written and the duplicate file is deleted. After downtime the one run is for the **latest** due slot and completing it moves the schedule past now. Three failed runs of a slot advance it anyway; `ReportTooLargeForPdf` advances at once. Administrators only. A run-now run is `Manual`, outside the slot index, and does not advance the schedule.
**Reason:** a restart must never send ten copies, and a clock-change night must never send none.

## ADR-288 — Scheduled delivery

**Date:** 2026-10-13 (HO-RPT-001)
**Status:** Active
**Decision:** Recipients are staff accounts only, each checked at creation (an email address, the report's category action, an assignment to the branch) and again at send time by `ScheduledReportGuard`. Restricted columns are included only if **every** recipient may see them. The file is attached **by reference** (`AttachmentKind = ReportRun`, `AttachmentId` = the run id) through the HO-NTF-001 amendment, loaded from its owner immediately before sending, for the Email channel only; a missing file cancels the notification (`SubjectNoLongerApplies`). No attachment bytes enter `notify.*`. A staff account has no inactive flag in this codebase; deactivation removes its roles, which fails the same check.
**Reason:** the attachment is the data; sending it to someone who has lost the role bypasses every control in ADR-281.

## ADR-289 — Report files (amended: HO-FIL-001)

**Date:** 2026-10-13 (HO-RPT-001)
**Status:** Active (amends the handover's `bytea` store, withdrawn 2026-09-23, README C-5)
**Decision:** A generated report file is stored through `IFileStore.PutAsync` and registered with HO-FIL-001's `RegisterStoredFileCommand` (purpose `ReportExport`, `RetainUntil` = today + `reporting.exportRetentionDays`). There is no `report_file` table and no `IReportFileStore`. A file over `reporting.maxAttachmentMegabytes` is not stored; the email says so and carries the link. The seven-day purge pass and its command (C8) are not delivered: they need HO-XCT-001's retention sweep, which is not merged.
**Reason:** one place a file is stored for the whole solution.

## ADR-290 — Protecting interactive work (NFR-PRF-7)

**Date:** 2026-10-13 (HO-RPT-001)
**Status:** Active
**Decision:** One host (ADR-010), so reports are isolated by bounded concurrency: the `reports` limiter (default 4 in flight, queue 8, then `429` with `Retry-After: 5`), a statement timeout, and scheduled runs executed sequentially by `ReportingWorker`. The `run-now` route carries the `report-run-now` limiter (5 per 10 minutes per client address) only: ASP.NET Core applies one named policy per endpoint.
**Reason:** a deliberate reading of NFR-PRF-7 under ADR-010.

## ADR-291 — Customer definitions for growth reports

**Date:** 2026-10-13 (HO-RPT-001)
**Status:** Active
**Decision:** An *active* customer in a period has at least one issued invoice in it; *new* = the first issued invoice ever falls in the period; *returning* = had one before. Acquisition source = `RegistrationChannel`. HO-BIL-001 keeps issued invoices on the customer id they were issued to, so `rv_customer_activity` resolves a merged customer to the survivor itself. Anonymised customers count but are never named beyond the stored placeholder.
**Reason:** SRS 8.3 names the reports but not their definitions.

## ADR-292 — Vaccination due list branch attribution

**Date:** 2026-10-13 (HO-RPT-001)
**Status:** Active
**Decision:** Cats are not branch-scoped, so a cat belongs to the branch of its most recent appointment (`rv_cat_last_branch`). Cats never seen are "Unattributed", included for `all` or with `includeUnattributed=true`.
**Reason:** US-RPT-01 AC1 needs a branch filter on a list whose subject has no branch.

## ADR-277 … ADR-292 — revisions (HO-RPT-001 gate fix round)

**Date:** 2026-10-13 (HO-RPT-001)
**Status:** Active; appended, nothing above is edited.
**Revisions:**
- **ADR-277 / ADR-289 (corrected, not widened).** A command changes one aggregate. Reporting writes a stored file only as a **producer** of HO-FIL-001's published port and command, in its own step (`IFileStore.PutAsync`, then `RegisterStoredFileCommand`, the two steps the upload endpoint takes), and sends C5 with the resulting `FileId`. C5 changes only the run (and enlists the notification requests). A file whose run was not kept is deleted in a further `DeleteStoredFileCommand` step. The earlier text that had C5 store and register the file itself is withdrawn.
- **ADR-278.** R7 reads views (`rv_stock_position`, `rv_reorder_policy`, `rv_on_order`), not Inventory tables; HO-IMP-001's pure `StockUsability` and `ReorderSuggestionCalculator` are still called. `rv_customer_activity` is at version 2.
- **ADR-281.** `Reports.Sales.View` is held by ShopManager, **Accountant** and Administrator (owner, 2026-10-13). A Clinical-category report (R3) writes a clinical-access row on every view, export and scheduled recipient (NFR-SEC-8, fail closed).
- **ADR-288.** At send time `ScheduledReportGuard` re-checks the recipient's branch rights as well as roles: a cross-branch role for an all-branches schedule, a current assignment for a branch schedule; the same rule (`ReportAccessPolicy.CanReceive`) applies when the schedule is saved.
- **ADR-290.** A per-account concurrency limit sits in front of the host-wide limit for reports and run-now, and covers the dashboard; run-now is 5 per 10 minutes per account (owner, 2026-10-13).
- **ADR-291.** A merged customer is followed through the whole merge chain to the final survivor.

---

## ADR-344 — A finalised visit stores the customer to bill (narrows ADR-117)

**Date:** 2026-10-14 (HO-BIL-001 Phase J, owner decision 1)
**Status:** Active
**Decision:** `VisitAggregate.Finalise` stores `CustomerId` = **the cat's owner at the moment of finalising** (read by the handler from the cat, passed in the finalisation DTO; a visit whose cat has no owner is refused `CatNotFound`), and `VisitFinalisedEvent` carries it. It is set once, never changes, and is the customer Billing invoices. A visit finalised before this column existed has none (`customer_id` is nullable) and is **not billable** (`VisitCustomerNotRecorded`): Billing does not fall back to the appointment's customer, which is a different fact. If the stored customer has since been merged into another (ADR-039), Billing bills the survivor by following `merged_into_customer_id`; the clinical record itself is not rewritten.
**Narrows ADR-117** ("the visit holds no customer id"): an *open* visit still holds none, and every owner-facing read keeps resolving the cat's **current** owner. The one stored id exists for billing and nothing else.
**Reason:** the owner chose "cat's owner at finalising" over the appointment's customer and the current owner; a bill must name the customer it was raised for even if the cat later changes hands.

---

## ADR-345 — A diagnostic test has an admin-set price, frozen on the order

**Date:** 2026-10-14 (HO-BIL-001 Phase J, owner decision 2)
**Status:** Active
**Decision:** Each `DiagnosticKind` has a price in the settings registry (`clinical.diagnosticPrice{BloodWork|Urinalysis|FelvFivTest|Imaging|Other}`, Decimal, default `0.00`, group Clinical, edited through the existing administration settings endpoint). Ordering a diagnostic reads the kind's price once and stores it on the order (`unit_price_amount`, `unit_price_currency_code`) with a tax class (the visit's own service's class, also frozen), as a treatment line does (ADR-127). The visit's bill carries **one line per non-cancelled order**; a zero price is a zero line. An order made before this decision has no price recorded and is **not billed** — nothing is invented. A test is one *order*, not one analyte.
**Reason:** the owner chose "add a price, charge them". Freezing on the order keeps a later price change from repricing a visit already ordered (HO-ADM-001 ADR-049).

---

## ADR-346 — Phase J: how Billing reads the three sources

**Date:** 2026-10-14 (HO-BIL-001 Phase J)
**Status:** Active
**Decision:** (1) **EMR** exposes `IVisitBillingReader` (Domain port, `Infrastructure/Consultation/VisitBillingReader`) returning typed values (`Money`, `decimal`, `VisitBillableLineKind`); Billing reads a visit only through it, never through the Read-layer display query `GetVisitChargeLinesQuery` and never from `clinic.*` tables (supersedes the `GetVisitChargeLinesQuery` hand-off in ADR-127 for Billing). EMR's kinds map to Billing's in one explicit, exhaustively tested table, `VisitBillableLineKindMap`: Procedure→Service, Medicine→Medicine, Consumable→Product, Diagnostic→Service. (2) **Pharmacy and Surgery** publish no typed billing port; as the appointment source does, Billing reads the dispensing's and the surgery's own rows with untracked projections. (3) The real events are captured, not §8.13's assumed ones: `VisitFinalisedEvent`, `PrescriptionDispensedEvent` (source = `DispensingId`), and `SurgeryDischargedEvent` (there is no `SurgeryCompletedEvent`). (4) A reversed dispensing voids its charges that are not yet invoiced; an invoiced one is left to a credit note. (5) Boarding: every visit has its own bill; no stay link exists and none is stubbed (owner decision 3, deferred to HO-BRD-001).
**Reason:** the owner's architecture call on backlog EMR 43; HO-BIL-001 §20.2 (the real contracts differ from the assumed ones, so the fix is an Amended row, not an adapter that guesses).

---

## ADR-344 … ADR-346 — revisions (HO-BIL-001 Phase J gate fix round)

**Date:** 2026-10-14 (HO-BIL-001 Phase J)
**Status:** Active; appended, nothing above is edited.
**Revisions:**
- **ADR-346 (corrected).** A surgery's appointment is **Completed** by the surgery (`CompleteSurgerySlot`) although it raises no `AppointmentCompletedEvent`. The appointment source therefore refuses an appointment with a `SurgeryId` (`AppointmentManagedBySurgery`) and the uncaptured list leaves it out: the surgery source owns the surgery fee, one fee one source.
- **ADR-346 (1) narrowed.** "Never reads `clinic.*`" is true of the **capture** path. The Read-layer uncaptured list counts the same tables with the same rules, as a cross-context list query (`.ai/bounded-contexts.md`). It lists a source while it has fewer recorded charges than billable lines.
- **ADR-346 (4) ratified by the owner** ("Keep it"): a reversed dispensing voids its not-yet-invoiced charges.
- **ADR-344 extended to surgery (owner, "Owner at discharge").** `SurgeryAggregate.OwnerAtDischargeId` is stored by `Discharge` and carried on `SurgeryDischargedEvent`; Billing bills that customer (merged-survivor chain followed). Records made before these columns existed are billed by hand; there is no back-fill.
- **ADR-345.** The database guard `clinic.diagnostic_order_guard()` now also freezes the price, currency and tax class.
- **Abandoned surgery (owner, "Drugs only").** The procedure fee is charged only for a procedure that ended `Completed`.

## HO-BRD-001 -- Boarding (ADR-350 ... ADR-369)

The handover reserved ADR-217 ... ADR-236, which other areas had already taken on master; the block was renumbered **ADR-350 = old 217 ... ADR-369 = old 236** (checked free on master and on every open `ho/*` branch; the highest id used anywhere was 347). Each entry states the decision **as built**; where the build departs from the handover the departure is named.

- **ADR-350 (old 217).** One `Boarding` bounded context, schema `boarding`. Tasks, observations, incidents, media, isolation entries, waitlist entries and daycare visits are separate roots, not stay children.
- **ADR-351 (old 218).** Stay states are Appendix C's. A lapse is `Cancelled/ReservationLapsed`; an `Enquiry` holds no unit. One transition table (`StayTransitionRules`) serves the aggregate and `allowedTransitions[]`.
- **ADR-352 (old 219).** Occupancy by GiST exclusion constraints (`ex_unit_maintenance`, `ex_stay_allocation_unit`, `ex_stay_cat`) over `is_active` rows the aggregate maintains; a 23P01 or a 40P01 deadlock on a `[BoardingClaimEndpoint]` is a 409. Past stays stay active so the turnaround after a departure is still enforced. **Correction 2026-10-15 (ACCEPTANCE gate):** an in-process application lock briefly added for these claims was removed again; section 4.4 forbids an application lock for occupancy. A claim that loses the exclusion constraint (23P01, 40P01) or `ix_daycare_slot` is retried in a fresh DI scope with the next free unit or slot (bounded, 4 attempts), then answered 409. The constraints stay the control and I-C12 proves them.
- **ADR-353 (old 220).** Availability is computed by the pure `BoardingAvailabilityFinder`, never in SQL; deterministic unit selection; alternatives by shifting the range.
- **ADR-354 (old 221).** Pricing: effective-dated rate cards, **per branch** (a card names a branch-owned tax class). The pure calculator emits month/week/night units with the discount **per unit**; consecutive nights are ONE `Night` unit with a quantity (the stay keeps at most 40 units). Card and settings are snapshotted at reserve and reused for extension and early check-out.
- **ADR-355 (old 222).** Vaccination gate: `VaccinationStatusRule` extracted from HO-CAT-001; a cat passes when not Overdue as of departure; evaluated at enquiry, reserve, confirm, check-in and extend; Administrator override per cat per stay with a reason and an audit row.
- **ADR-356 (old 223).** Deposit without a gateway: staff record the money in Billing as a payment **earmarked** `{BoardingStay, stayId}`; Boarding confirms only from Billing's accepted payments (`IBoardingDepositLedger`); a payment awaiting a second approver suspends the lapse. There is no `BoardingStayDeposit` reference.
- **ADR-357 (old 224).** Care tasks are their own root, generated idempotently per stay, cat, local date and source key (`ix_care_task_generation`) for today and tomorrow by the worker; a profile change voids pending tasks; assignment to rostered caretakers; overdue is derived.
- **ADR-358 (old 225).** Observations are their own root; "not eating" is derived from the last two, never stored.
- **ADR-359 (old 226).** Incidents: medium is a Normal template, high an Urgent one; acknowledgement by the owner or staff; an unacknowledged high incident is escalated by the worker to a phone-call follow-up kept on the incident.
- **ADR-360 (old 227).** Isolation: a flagged cat puts the stay in an isolation unit (relocated in the same command); isolation units are never offered online.
- **ADR-361 (old 228).** Media are **HO-FIL-001 stored files** (purpose `BoardingMedia`, attach window) referenced by id; retention is HO-XCT-001's `retention.boardingMediaMonths` through the registered `boarding-media` purge source, which erases the file and keeps the row as `Purged`; incident media are never purged.
- **ADR-362 (old 229).** Referral and spending limit: Boarding owns the referral, the per-cat limit, commitments (from `BoardingVetChargeRecordedEvent`, idempotent by charge id) and approvals. `VetVisitAuthorised = false` means zero headroom. The contract events carry **no description** (U-X1 forbids text in events).
- **ADR-363 (old 230).** Charges under the **push model** of merged Billing: one charge per rate unit, recorded through `IBillableChargeRecorder` in Boarding's own handlers at check-in, key `r{revision}.{kind}.{from}-{to}`, group `BoardingStay`. An extension or early check-out re-derives by `BoardingChargePlanner`: identical charges kept, uninvoiced ones voided, new ones recorded, an invoiced charge that no longer applies becomes `CreditDue`. Cycle (30 nights) and final invoices are issued by the worker through `IssueInvoiceFromChargesCommand` with a billing-group selector.
- **ADR-364 (old 231).** `BoardingWorker`: one `BackgroundService`, one command per item through `IInMemoryDispatcher`, never throws, off in `Testing`; passes: confirm, tasks, escalate, invoices, daily updates, waitlist notices and closing, lapse, expire.
- **ADR-365 (old 232).** Cross-context writes by Boarding command handlers are limited to Billing's published seams (`IBillableChargeRecorder`, `IssueInvoiceFromChargesCommand`); everything else reaches other contexts through read ports and event handlers that never enqueue messages.
- **ADR-366 (old 233).** Roles: "boarding manager" is `Administrator` (+ `Reception` for booking changes); check-in/out also `Caretaker` (a recorded deviation from Appendix D).
- **ADR-367 (old 234).** Daycare is a separate aggregate that never allocates a unit; capacity is `boarding.daycareDailyCapacity` visits per branch per day, raced by the unique slot row `ix_daycare_slot`; charged as source `DaycareVisit` at check-out.
- **ADR-368 (old 235).** The waitlist notifies, never holds: the oldest waiting entry with a free unit, once; a unit is not announced twice. Quiet hours are the notification service's (a Normal message is deferred), not a worker check.
- **ADR-369 (old 236).** Signatures are bounded vector data or a typed confirmation, never files.
- **Owner-authorised addition (2026-10-14).** Early check-out "Show before": `GET /api/staff/boarding/stays/{id}/check-out-preview` runs `EarlyCheckout.Compute` and `BoardingChargePlanner.Plan` -- the same code as the check-out handler -- and changes nothing; pinned by I-G2b.
