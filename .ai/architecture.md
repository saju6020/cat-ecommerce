# Project Architecture

This project follows **Domain Driven Design and CQRS on the `Platform.Infrastructure.*`
libraries**, running as a **single web host** with **in-memory dispatch**.

The platform libraries, the layer structure, the Command/CommandHandler and Query/QueryHandler
contracts, and the aggregate model are all unchanged. What changed is the **transport and the
hosting**:

| Change | From | To |
|---|---|---|
| Hosts | Api + CommandWorker + EventWorker | **Api only** — one host |
| Messaging | MassTransit over RabbitMQ | **In-memory** — `AddInMemoryBusServices()` |
| Command dispatch | `IBusMessageDispatcher.SendAsync` to a queue | `IInMemoryDispatcher`, in-process, synchronous |
| Response | `202 Accepted`, result via notification | The real result, or a `4xx` with the violation |
| Database | MongoDB | **PostgreSQL via EF Core** |

See ADR-010 to ADR-017 in `.ai/decisions/adr.md`.

---

## 1. Layers

Unchanged from before. The two worker hosts are gone; the layers are not.

- Api ← **the single host**
- Application
- Domain
- Infrastructure
- Read
- Shared

## 2. Responsibilities

**Api** — the one host
- Handles HTTP requests and integrates with the Application and Read layers.
- Injects `IInMemoryDispatcher` into controllers via constructor DI and dispatches Commands and Queries
  **in memory**. The handler runs in-process, inside the request.
- **The command handler now runs in this host.** ADR-004's rule that "the Api never touches an
  aggregate" is superseded — with no CommandWorker, this host owns the write path too. It
  composes both the aggregate repository and the read side.
- Controllers return the real result: `201`/`200` with the created id, or `4xx` carrying the
  `ValidationResponse`. Never `202`.

**Application (Write Side)**
- Contains Commands and Command Handlers implementing `ICommandHandlerAsync<TCommand>`.
- Command Handlers load and save aggregates **directly through
  `IAggregateRootRepository<TAggregate>`**. See §4.
- Validates Commands via a registered `IValidator<TCommand>` (FluentValidation), resolved by the
  platform's `IValidationService`.
- Contains **Domain Services only where domain logic genuinely spans aggregates** — not a
  Command Service layer for every feature. See §4.

**Read (Read Side)**
- Contains Queries, ViewModels, and Query Handlers (`IQueryHandlerAsync<TQuery, TRes>`).
- Query Handlers read from the database through EF Core — `AsNoTracking()`, projecting into a
  ViewModel.
- **No event-driven projections.** With one host and one relational database, a query reads the
  same tables the command just wrote. There is no EventWorker, no read store and no lag.

**Domain**
- Contains business logic encapsulated into Aggregate Roots (inheriting
  `Platform.Infrastructure.Core.Domain.AggregateRoot`).
- Defines Value Objects, Entities, Domain Services, Exceptions and Domain Events.
- Aggregates enforce their own invariants. Every mutation method raises a domain event with
  `AddDomainEvent(...)` and finishes with `UpdateVersion()`.
- No persistence concerns. `[BsonIgnoreExtraElements]` is gone with MongoDB — EF Core mapping
  lives in Infrastructure.

**Infrastructure**
- **EF Core over PostgreSQL**: `DbContext`, `IEntityTypeConfiguration<T>` mappings, migrations.
- **The `IAggregateRootRepository<TAggregate>` implementation.** The interface is declared in
  `Platform.Infrastructure.Core` (verified — ADR-018), so we implement the **platform contract
  itself** over EF Core and handlers look exactly as they always have. Only the MongoDB
  implementation behind `AddAggregateRootRepository()` is replaced. See
  `.ai/persistence-ef-core.md`.
- **`DevUserContextProvider`** (`Infrastructure/Hosting/`). `AddInMemoryBusServices()` needs an
  `IUserContextProvider` and ships none — see §6.1.
- **In-process domain event dispatch.** The EF repository resolves `IEventHandlerAsync<TEvent>`
  from DI by the event's **runtime type** and invokes them after `SaveChangesAsync`. There is no
  `IEventPublisher` and no bus dispatcher — see ADR-020.

**Shared**
- Core Kernel patterns, DTOs (`SharedDto`), structured enums, violation codes.
- **`Enumeration` is defined here, not by the platform.** It ships in no `Platform.Infrastructure.*`
  package — verified, ADR-020. `.ai/enumeration-pattern.md` describes how to use it.

---

## 3. What the platform still gives us

`AddInMemoryBusServices()` (`Platform.Infrastructure.Core.Extensions`) **is** the in-memory bus.
It was already being called in every host — it was just being shadowed by
`AddServiceBusProvider()`, which routed commands out to RabbitMQ. Remove the RabbitMQ call and
the platform dispatches in-process on its own.

Still used, unchanged:

| From `Platform.Infrastructure.Core` | Used for |
|---|---|
| `Command`, `CommandResponse`, `ValidationResponse` | Command contracts |
| `ICommandHandlerAsync<TCommand>` | Every command handler |
| `Query<TResult>`, `QueryResponse<T>`, `IQueryHandlerAsync<TQuery,TRes>` | Every query handler |
| `AggregateRoot`, `AddDomainEvent()`, `UpdateVersion()`, `SetDefaultValue()` | Every aggregate |
| `Event`, `IEventHandlerAsync<TEvent>` | Domain event handling, now in-process |
| **`IInMemoryDispatcher`** (1.0.34), `ICommandSender`, `IQueryProcessor` | In-memory dispatch — inject `IInMemoryDispatcher` (ADR-019) |
| `IValidationService`, `IValidationProvider` | FluentValidation integration |
| `AddInMemoryBusServices()` | Registers all of the above, and nothing else — see §6 |

**Not used: `Platform.Infrastructure.Host.WebApi` → `UseHttpPipeline(...)`.** Decompiled
(`Host.WebApi` 1.0.14), it bundles routing, an exception handler that reads and logs the full
request body for a JSON `POST`, and a CORS policy that allows any origin with credentials into one
call — see ADR-342. The Api host composes its pipeline explicitly instead: forwarded headers (only
when a trusted proxy/network is configured) → `UseRouting()` → the single
`SafeExceptionHandlerMiddleware` → `UseCors()` with an explicit allow-list → each area's own
`Use<Area>Module()` → rate limiting → `MapHealthChecks("/health")` → `MapControllers()`. See
`.ai/host-composition.md` §1 and `src/Api/CatEcommerce.Api/Program.cs`.

No longer referenced:

| Package | Why |
|---|---|
| `Platform.Infrastructure.MassTransit.Bus.RabbitMQ` | No broker. `AddServiceBusProvider`, `CustomQueueConfig`, `CommandConsumerAdapter`, `EventConsumerAdapter`, `WorkerHostBuilder` — and `IBusMessageDispatcher` — all go with it. **Nothing in this solution may reference `IBusMessageDispatcher`** (ADR-020) |
| `Platform.Infrastructure.Repository.MongoDb` | RDBMS instead of MongoDB. Its contracts are reimplemented over EF Core |

---

## 4. The command path — DDD as intended

A command handler **loads its aggregate from the repository itself**:

```csharp
public sealed class RecordVaccinationCommandHandler(
    ILogger<RecordVaccinationCommandHandler> logger,
    IAggregateRootRepository<CatAggregate> catRepository)
    : ICommandHandlerAsync<RecordVaccinationCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordVaccinationCommand command)
    {
        var cat = await catRepository.GetByIdAsync(command.CatId);
        if (cat is null)
            return Fail(BusinessViolationCodes.CatNotFound, nameof(command.CatId),
                        $"Cat '{command.CatId}' was not found.");

        cat.RecordVaccination(command.ToDomainDto());   // invariants live in the aggregate

        await catRepository.UpdateAsync(cat, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true, Result = cat.Id };
    }
}
```

**There is no Command Service.** The handler loads the aggregate, the aggregate enforces its own
rules, the handler saves it. That is the whole pattern.

### When a Domain Service is warranted

Only when the logic is **domain logic that does not belong to any single aggregate** — typically
because it needs to see several:

| Situation | Where it goes |
|---|---|
| "A cat cannot be vaccinated twice with the same vaccine on the same day" | **The `CatAggregate`.** It can see its own vaccinations |
| "This vaccination's next-due date is 12 months out" | **The `CatAggregate`.** Its own data |
| "Allocate stock from the batch expiring soonest (FEFO)" | **A Domain Service** — the rule is about the relationship between many `BatchAggregate`s, and belongs to no one of them |
| "Is this appointment slot free?" across vet roster, room and existing appointments | **A Domain Service** — spans three aggregates |
| "Does a cat with this microchip already exist?" | **The handler**, via a repository query. A uniqueness lookup is not domain logic |

A Domain Service lives in `Domain/DomainServices/`, takes and returns domain types, and has no
infrastructure dependency beyond repository interfaces.

> The previous architecture put existence and uniqueness checks in an `Application/CommandServices`
> layer, injected into every handler. That layer is removed. It added a file per feature to wrap a
> repository call, and it pulled aggregate loading out of the handler, which is where DDD expects
> it. Where a real cross-aggregate rule exists, it is a **Domain Service** in the Domain layer —
> not an application-layer service.

---

## 5. Events

Domain events are still raised by aggregates with `AddDomainEvent(...)` and still handled by
`IEventHandlerAsync<TEvent>` implementations — but they are now published **in-process** by the
platform's in-memory bus rather than queued to an EventWorker.

They are used for **side effects inside the same request**, not for building read models:

- ✅ recompute a cat's vaccination status when a vaccination is recorded
- ✅ write an outbox row so the owner gets an SMS
- ❌ project a `CatViewModel` — there is no read store; the query reads the same tables

Anything that must leave the process — SMS, email, courier API — is written to an **outbox** table
in the same transaction and delivered by a background service. That is the only asynchronous
element left, and it exists for delivery guarantees, not decoupling. See
`.ai/domain-events-and-outbox.md`.

---

## 6. ✅ Verified against the packages (Core 1.0.34, 2026-09-20)

Settled by restoring the real packages and probing them. See ADR-018 and **ADR-020**.

| # | Question | Answer |
|---|---|---|
| 1 | In-memory dispatch types | Inject **`IInMemoryDispatcher`**. On 1.0.34 it is **command and query only**: `Task<CommandResponse> SendAsync(Command)`, `Task<QueryResponse<TResult>> GetResultAsync<TResult>(IQuery<TResult>)`, `QueryResponse<TResult> GetResult<TResult>(...)`. `PublishAsync` was removed in 1.0.33. `IDispatcher` adds `SendBusMessageAsync`/`PublishBusMessageAsync`, which is exactly why nothing may use it (ADR-019) |
| 2 | Where `IAggregateRootRepository<T>` is declared | **`Platform.Infrastructure.Core`** — not Mongo-only. We implement the platform interface directly over EF Core, so command handlers need no change at all |
| 3 | The in-process event publish path | **The host owns it** (ADR-020). `IEventPublisher` is not registered on 1.0.34. The EF repository resolves `IEventHandlerAsync<TEvent>` from DI by the event's **runtime type** and invokes them after `SaveChangesAsync`. Handlers still pass **`EventPublishOption.InMemoryPublishOnly`** — the enum is retained: `DoNotPublish=0`, `InMemoryPublishOnly=1`, `InMemoryAndQueuePublish=2`, `QueuePublishOnly=3` |
| 4 | Does `Host.WebApi` drag in RabbitMQ? | **No.** The Api resolves only `Core`, `Common`, `CustomException`, `EndpointRoleFeatureMap`, `Host.WebApi` and `ServiceRegistration.Provider`. No MassTransit, no RabbitMQ, no MongoDB |
| 5 | Does `Enumeration` ship in the platform? | **No.** It is in no `Platform.Infrastructure.*` package. This solution defines it in `Shared` |

`AddInMemoryBusServices()` on 1.0.34 registers exactly these, all Scoped, and nothing else:

`IResolver` · `IHandlerResolver` · `IInMemoryDispatcher` · `IValidationService` ·
`IValidationProvider` · `ICommandSender` · `IQueryProcessor`

**No broker type is reachable from that graph.** There is no `IEventPublisher`, no
`IBusMessageDispatcher`, and nothing to register for either.

### 6.1 The one implementation the host must provide

`AddInMemoryBusServices()` registers the pipeline but does not satisfy one of its own dependencies.
With `ValidateOnBuild = true` the host refuses to start until it is registered — which is the
correct behaviour, and how it was found.

| Interface | Declared in | Why the host must implement it |
|---|---|---|
| `Platform.Infrastructure.Common.Security.IUserContextProvider` | `Core` / `Common` | Required by `CommandSender`. **No implementation ships.** Supplies the ambient identity and tenant. Until real auth exists, a dev stand-in hands every caller the same identity — `Infrastructure/Hosting/DevUserContextProvider` |

> **This used to list a second row, for `IBusMessageDispatcher`.** On Core 1.0.32 `EventPublisher`
> required it and the container would not build without it. 1.0.33 removed `PublishAsync` from
> `IInMemoryDispatcher` and dropped `IEventPublisher` from `InMemoryDispatcher`'s constructor;
> 1.0.34 removed the `IEventPublisher` registration altogether. The container now builds with
> `ValidateScopes` and `ValidateOnBuild` on and **no** `IBusMessageDispatcher` anywhere. See
> ADR-020.

### 6.2 Two rules that are easy to get wrong

**Publish on the runtime type.** Resolve handlers with `domainEvent.GetType()`, never with the
static type of the loop variable. `aggregate.Events` is `ReadOnlyCollection<Event>`, so a generic
`Publish<TEvent>(domainEvent)` binds `TEvent = Event` and finds no handler for the concrete event.

**Stamp `ValidateCommand = true` on every command.** `Command.ValidateCommand` is `bool?` and
`CommandSender` skips validation when it is null and `ValidationOptions.ValidateAllCommands` is
false — which is the default. A registered `IValidator<TCommand>` then silently never runs and
invalid data reaches the aggregate.

## 7. Where to go next

| You want to | Read |
|---|---|
| Understand in-memory CQRS here | `.ai/cqrs.md` |
| Write a command handler | `.ai/command-handler-pattern.md` |
| Write a query handler | `.ai/read-side-pattern.md` |
| Map an aggregate to tables | `.ai/persistence-ef-core.md` |
| Publish or handle an event | `.ai/domain-events-and-outbox.md` |
| Add a feature end to end | `.ai/feature-creation-checklist.md` |
| Compose the host | `.ai/host-composition.md` |
| Register something in DI | `.ai/di-registration-guide.md` |
| Name a namespace or folder | `.ai/namespace-conventions.md` |
| Know the bounded contexts | `.ai/bounded-contexts.md` |
| Start a new solution | `.ai/new-project-bootstrap.md` |
| Understand a decision | `.ai/decisions/adr.md` |
| See a business flow | `.ai/flows/` |
