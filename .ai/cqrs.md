# CQRS Pattern — In-Memory

## Overview

This project does NOT use MediatR. It uses the internal platform libraries
(`Platform.Infrastructure.Core`), exactly as before. The physical separation between the Write
(`src/Application`) and Read (`src/Read`) layers is unchanged.

**What changed is the transport.** Commands and events no longer travel over RabbitMQ to separate
worker hosts. `AddInMemoryBusServices()` — which every host was already calling — dispatches them
**in-process**, inside the request, in the single Api host.

> **Superseded:** the previous version of this document described commands sent with
> `IBusMessageDispatcher.SendAsync` to a queue, executed in CommandWorker, with results arriving
> asynchronously as notifications and a read side built from projections in a separate database.
> See ADR-010, ADR-013, ADR-014.

---

## 1. The one line that changed the architecture

```csharp
builder.Services.AddInMemoryBusServices();      // ← was always here
builder.Services.AddServiceBusProvider(config); // ← REMOVED
```

`AddInMemoryBusServices(params Type[] types)` on Core 1.0.34 registers exactly
**`IInMemoryDispatcher`** → `InMemoryDispatcher`, plus `IResolver`, `IHandlerResolver`,
`ICommandSender`, `IQueryProcessor`, `IValidationService` and `IValidationProvider` — all
**Scoped**, and nothing else. It is the in-memory dispatch pipeline, and it was always present;
`AddServiceBusProvider()` simply routed the write path out to a broker instead.

Remove the second call and the platform dispatches in-process on its own.

### Inject `IInMemoryDispatcher`, not `IDispatcher`

`IDispatcher` is **not registered** by `AddInMemoryBusServices()` on 1.0.34, so injecting it fails
at resolution. It is documented here only so the two are not confused:

| | `IInMemoryDispatcher` | `IDispatcher` |
|---|---|---|
| `SendAsync(Command)` | ✅ | ✅ |
| `GetResultAsync<TResult>(IQuery<TResult>)` | ✅ | ✅ |
| `PublishAsync<TEvent>(TEvent)` | ❌ removed in 1.0.33 | ✅ |
| `SendBusMessageAsync<TMessage>` | ❌ | ⚠️ yes |
| `PublishBusMessageAsync<TMessage>` | ❌ | ⚠️ yes |

`IInMemoryDispatcher` has **no bus methods at all**, so a controller physically cannot reach a
broker that is not there. Injecting it makes the mistake impossible rather than merely
discouraged (ADR-019).

**Commands and queries dispatch through it; domain events do not.** `PublishAsync` was removed in
1.0.33, and the EF aggregate repository dispatches domain events itself by runtime type — see
`.ai/domain-events-and-outbox.md` §2 and ADR-020. Verified against Core 1.0.34.

---

## 2. Commands (write operations)

- **Commands**
  - Represent write intents.
  - Must extend `Platform.Infrastructure.Core.Commands.Command`.
  - Data/state only, no logic.

- **Command dispatch (controller → handler, in memory)**
  - Controllers inject **`IInMemoryDispatcher`** through constructor DI.
  - The controller **awaits** the dispatch; the handler runs in-process, in the same request.
  - The HTTP response carries the **real result**: `201`/`200` with the id, or `4xx` with the
    `ValidationResponse`. Never `202`.
  - There is no `QueueName` to stamp, no send-versus-publish distinction to get wrong, and no
    `CustomQueueConfig` trap. A command has exactly one handler, resolved from DI.

  ```csharp
  [HttpPost]
  public async Task<IActionResult> RegisterCat([FromBody] RegisterCatRequest request)
  {
      var command = request.ToCommand(catId: Guid.NewGuid(), userContext: UserContext);

      var response = await _dispatcher.SendAsync(command);

      return response.IsSuccess
          ? CreatedAtAction(nameof(GetCat), new { id = response.Result }, response.Result)
          : BadRequest(response.ValidationResult);
  }
  ```

  `SendAsync` takes the **`Command` base type**, not a generic parameter — the handler is
  resolved from the command's runtime type.

- **Command Handlers**
  - Implement `ICommandHandlerAsync<TCommand>`, returning `Task<CommandResponse>`.
  - The FluentValidator is a separate `IValidator<TCommand>` registered in `Program.cs`; the
    platform's `IValidationService` resolves it before `HandleAsync` runs. There is no
    `GetValidator()` to override.
  - **Responsibilities:**
    1. Load the aggregate via `IAggregateRootRepository<TAggregate>` — **directly, in the
       handler**. There is no CommandService layer.
    2. Invoke one aggregate mutation method. The aggregate enforces its own invariants.
    3. Persist via the repository.
    4. On a business rule violation, return `CommandResponse { IsSuccess = false, ValidationResult
       = ... }`. **No rejection event** — ADR-005 is superseded.

  Full pattern: `.ai/command-handler-pattern.md`.

---

## 3. Queries (read operations)

Unchanged in shape:

- **Queries** — request data models (`ViewModels`) without altering state. Handled in
  `src/Read/QueryHandlers`.
- **Query Handlers** — implement `IQueryHandlerAsync<TQuery, TResponse>`, return
  `QueryResponse<TResponse>`.
- **Dispatched with `GetResultAsync<TResult>(query)`** — not `QueryAsync`. There is also a
  synchronous `GetResult<TResult>(query)`; prefer the async one.
- `Query<TResult>` already carries `PageNumber`, `PageSize`, `Skip`, `Take`, `DoCount`,
  `UserContext`, `CorrelationId` and `Vertical` — do not redeclare paging on your query.

What changed is the source. There is no read database and no `IReadRepository`: query handlers
read the **same PostgreSQL tables the command just wrote**, through EF Core with `AsNoTracking()`,
projecting into a ViewModel.

A read issued after a write sees that write. No projection, no lag, no convergence problem.

Full pattern: `.ai/read-side-pattern.md`.

---

## 4. Events

Domain events are still raised by aggregates with `AddDomainEvent(...)` and still handled by
`IEventHandlerAsync<TEvent>` — but the platform now publishes them **in-process** rather than
queueing them for an EventWorker.

They are for **side effects**, not for building read models:

- ✅ recompute derived state after a mutation
- ✅ write an outbox row so an SMS goes out
- ❌ project a ViewModel — there is nothing to project into

See `.ai/domain-events-and-outbox.md`.

---

## 5. Rules

1. **A controller dispatches exactly one command or one query.** Two dispatches means a use case
   is sitting in the wrong layer.
2. **`Application` never references `Read`.** The physical separation stands.
3. **Commands return `CommandResponse`, queries return `QueryResponse<T>`.** Never a domain type
   or an EF entity.
4. **No `202 Accepted` for a command.** The result is real and available now.
5. **Register every handler and validator explicitly.** `AddInMemoryBusServices` accepts
   `params Type[]` assembly-marker types; until that is proven to auto-register handlers in this
   codebase, register each one by hand and treat a missing registration as the first suspect when
   a command reaches no handler.
