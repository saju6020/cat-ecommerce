# Examples

Worked examples for the current architecture: **single web host, in-memory CQRS on the platform
bus, DDD aggregates, EF Core over PostgreSQL**.

| File | Status | Contents |
|---|---|---|
| `host-web-api.md` | **Current** | A complete vertical slice: controller → command → handler → aggregate → EF Core → query. Start here |
| `host-command-worker.md` | ⛔ Superseded | Tombstone. The CommandWorker host no longer exists (ADR-010) |
| `host-event-worker.md` | ⛔ Superseded | Tombstone. The EventWorker host and projections no longer exist (ADR-010, ADR-012) |
| `platform-extension-reference.md` | ⚠️ Partly superseded | The MongoDB and RabbitMQ extensions are gone; `AddInMemoryBusServices()` remains and is now the whole pipeline (ADR-017) |

---

## The shape of every feature

```
Controller                 mint id → stamp context → dispatch ONE command or query → map result
   │  IDispatcher  (platform, in-memory)
   ▼
IValidationService         runs the registered IValidator<TCommand>
   │
   ▼
Command Handler            load the aggregate from IAggregateRootRepository<T>,
   │                       call ONE mutation method, save. No CommandService.
   ▼
Aggregate                  enforce invariants, AddDomainEvent(...), UpdateVersion()
   │
   ▼
Repository → DbContext     EF Core → PostgreSQL, then publish domain events in-process
```

Reads bypass the middle entirely: query handler → `DbContext` → `AsNoTracking()` → `Select` into a
ViewModel.

---

## The six mistakes worth naming

Each of these has a specific counter-example in `host-web-api.md`:

1. **A controller doing more than one thing.** Two dispatches in one action means a use case is
   sitting in the wrong layer, outside the transaction.
2. **Creating a service that only wraps a repository call.** The handler injects
   `IAggregateRootRepository<T>` and loads the aggregate itself. A service is justified only when
   it is a genuine **Domain Service** for logic spanning several aggregates.
3. **Business rules in the handler instead of the aggregate.** If the rule can be checked from
   the aggregate's own state, it belongs in the aggregate. Handlers orchestrate; they do not
   decide. This is what the removed CommandService layer kept getting wrong.
4. **Rehydrating an aggregate to display it.** Loading `CatAggregate` to render four columns.
   Project from the read context instead.
5. **Registering a validator as the concrete type** instead of `IValidator<TCommand>`. The
   platform resolves the interface, so the validator silently never runs.
6. **Throwing for an expected outcome.** "Cat not found" is a `CommandResponse.Failure`, not an
   exception. Exceptions are for the unexpected.

---

## If you are working from older notes

This repository previously documented CQRS over RabbitMQ and MongoDB with three hosts. The
architecture changed on 2026-09-19: the workers and the broker are gone, the datastore is
PostgreSQL, and the CommandService layer is removed. The `Platform.Infrastructure.*` libraries and
the layer structure are **retained**. Decisions: ADR-010 to ADR-017 in `.ai/decisions/adr.md`.

The two most consequential changes for anyone reading old code:
**a command is handled synchronously and returns its real result** (no `202`, no rejection event),
and **the handler loads its own aggregate** (no CommandService).
