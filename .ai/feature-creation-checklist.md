# Feature Creation Checklist

When creating a new feature/entity `[EntityName]` in `[Feature]` context, create all applicable
files in this order.

> **CRITICAL**: Do NOT skip the Registration steps at the bottom. Missing registrations cause
> silent runtime failures — there is still no assembly scanning.
>
> **What changed:** the CommandService steps are gone (the handler uses the repository directly),
> the rejection event is gone, the projection event handler is gone, and the three worker
> registrations have collapsed into registrations in the one host.

---

## Step 1 — Domain Layer (`src/Domain/`)

### 1.1 Domain DTO (record)
**File**: `src/Domain/Models/[EntityName]CreationDto.cs`
- Immutable `record` type.
- Always include `UserContext` and `CorrelationId` as last params.
- Namespace: `{{RootNamespace}}.Domain.Models`

### 1.2 Aggregate Root
**File**: `src/Domain/Aggregates/[EntityName]Aggregate.cs` (or `Aggregates/[Feature]/…`)
- Inherit `Platform.Infrastructure.Core.Domain.AggregateRoot`.
- **No `[BsonIgnoreExtraElements]`** — MongoDB is gone; EF mapping lives in Infrastructure.
- Private setters; state mutation via encapsulating methods only.
- Child collections as `IReadOnlyList<T>` over a private backing field.
- Every mutation raises a domain event with `AddDomainEvent(...)` and ends with `UpdateVersion()`.
- Reference other aggregates **by id**, never by navigation property.
- **Put the invariants here.** Anything checkable from the aggregate's own state belongs in the
  aggregate, not in the handler.
- Namespace: `{{RootNamespace}}.Domain.Aggregates[.Feature]`

### 1.3 Domain Events
**File**: `src/Domain/Events/[Feature]/[EntityName]CreatedEvent.cs`
- Extend `Platform.Infrastructure.Core.Events.Event`. `sealed class`, `init` properties.
- Past tense. Carries what happened.
- Namespace: `{{RootNamespace}}.Domain.Events.[Feature]`

### ~~1.4 Command Rejection Event~~ — **REMOVED**
Violations are returned in `CommandResponse.ValidationResult` and become the HTTP response body.
No `INotifiedByEventHandler`, no `Domain.Events.ErrorEvents`. See ADR-014.

### 1.5 Value Objects (if needed)
**File**: `src/Domain/ValueObjects/[ValueObjectName].cs` — `record` types.

### 1.6 Domain Service (**only** if the logic spans aggregates)
**File**: `src/Domain/DomainServices/I[Name]Service.cs` + implementation
- Only when the rule belongs to no single aggregate — FEFO allocation, slot availability.
- Takes and returns domain types. Depends only on repository interfaces.
- **If it would only wrap one repository call, do not create it.** Put the call in the handler.
- Namespace: `{{RootNamespace}}.Domain.DomainServices`

---

## Step 2 — Application Layer / Write Side (`src/Application/`)

### 2.1 Command
**File**: `src/Application/Commands/[Feature]/Create[EntityName]Command.cs`
- Extend `Platform.Infrastructure.Core.Commands.Command`. `required`/`init`. Data only.
- Namespace: `{{RootNamespace}}.Application.Commands.[Feature]`

### 2.2 Command Validator
**File**: `src/Application/CommandHandlers/Validators/Create[EntityName]CommandValidator.cs`
- Extend `AbstractValidator<Create[EntityName]Command>`.
- Shape validation only. Registered as `IValidator<T>` in the Api host.
- Namespace: `{{RootNamespace}}.Application.CommandHandlers.Validators` (flat)

### 2.3 DataMapper
**File**: `src/Application/DataMappers/[EntityName]Mapper.cs`
- Static class, extension methods: `ToDomainDto()`.
- Always pass `command.UserContext` and `command.CorrelationId`.
- Namespace: `{{RootNamespace}}.Application.DataMappers` (flat)

### ~~2.4 / 2.5 CommandService interface + implementation~~ — **REMOVED**
The handler injects `IAggregateRootRepository<[EntityName]Aggregate>` and loads the aggregate
itself. Cross-aggregate domain logic goes to a Domain Service (§1.6), in the Domain layer.

### 2.6 Command Handler
**File**: `src/Application/CommandHandlers/[Feature]/Create[EntityName]CommandHandler.cs`
- Implement `ICommandHandlerAsync<Create[EntityName]Command>` directly — no base class.
- **Primary constructor.** Inject `ILogger` and `IAggregateRootRepository<[EntityName]Aggregate>`.
- Load the aggregate, call **one** mutation method, persist with `SaveAsync`/`UpdateAsync`.
- On a violation, return `CommandResponse { IsSuccess = false, ValidationResult = … }` built with
  `ValidationResponse.AddError(...)`. Do not throw, do not publish an event.
- Namespace: `{{RootNamespace}}.Application.CommandHandlers.[Feature]`

### 2.7 Domain Event Handler (if the change has in-process side effects)
**File**: `src/Application/DomainEventHandlers/[Feature]/[EntityName]CreatedHandler.cs`
- Implement `IEventHandlerAsync<[EntityName]CreatedEvent>`.
- For side effects — recompute derived state, write an outbox row. **Not** for projections.
- Namespace: `{{RootNamespace}}.Application.DomainEventHandlers.[Feature]`

---

## Step 3 — Read Layer (`src/Read/`)

### 3.1 ViewModel
**File**: `src/Read/ViewModels/[EntityName]ViewModel.cs`
- `sealed record` with `init` properties, shaped for the screen.
- No `ViewModelBase`, no `[BsonIgnoreExtraElements]`, no public setters, no `Map(event)` method.
- Namespace: `{{RootNamespace}}.Read.ViewModels` (flat)

### ~~3.2 Event Handler (Read Projection)~~ — **REMOVED**
There is no read store to keep in sync. Query handlers read the same tables. See ADR-012.

### 3.3 Query
**File**: `src/Read/Queries/[Feature]/Get[EntityName]Query.cs`
- Extend `Query<[EntityName]ViewModel?>`. Filter criteria only; include paging for lists.
- Namespace: `{{RootNamespace}}.Read.Queries.[Feature]`

### 3.4 Query Handler
**File**: `src/Read/QueryHandlers/[Feature]/Get[EntityName]QueryHandler.cs`
- Implement `IQueryHandlerAsync<Get[EntityName]Query, [EntityName]ViewModel?>`.
- Inject `ILogger` and the `DbContext`. `AsNoTracking()`, `Select` into the ViewModel.
- Return `QueryResponse<…>`.
- Namespace: `{{RootNamespace}}.Read.QueryHandlers.[Feature]`

---

## Step 4 — Infrastructure (`src/Infrastructure/`)

### 4.1 EF Configuration
**File**: `src/Infrastructure/Persistence/Configurations/[EntityName]Configuration.cs`
- `IEntityTypeConfiguration<[EntityName]Aggregate>`.
- `ToTable`, key, `OwnsOne` for value objects, `OwnsMany` for aggregate children.
- `PropertyAccessMode.Field` for read-only collections; `Ignore(x => x.DomainEvents)`.
- `builder.Property<uint>("xmin").IsRowVersion();`
- Indexes for every filtered column.

### 4.2 DbSet
**File**: `src/Infrastructure/Persistence/[Root]DbContext.cs`
- **MODIFY**: add `public DbSet<[EntityName]Aggregate> [EntityName]s => Set<…>();`

### 4.3 Migration
```bash
dotnet ef migrations add Add[EntityName] \
  --project src/Infrastructure/{Root}.Infrastructure \
  --startup-project src/Api/{Root}.Api
```
**Read the generated migration before committing.** EF generates `DROP COLUMN` for renames.

---

## Step 5 — Shared Layer (`src/Shared/`)

### 5.1 BusinessViolationCodes
**File**: `src/Shared/Common/Constants/BusinessViolationCodes.cs`
- **MODIFY**: `public const string [EntityName]NotFound = nameof([EntityName]NotFound);`
- Still used — as the `errorCode` in `ValidationResponse.AddError(...)`, which now reaches the
  client directly in the response body.

### 5.2 NotificationKeys
**File**: `src/Shared/Common/Constants/NotificationKeys.cs`
- **MODIFY** only if the feature sends something through the outbox.

---

## Step 6 — Registration ⚠️ CRITICAL

All in **one** file now: `src/Api/{Root}.Api/Program.cs`.

```csharp
// Command handler + validator — these used to live in CommandWorker
builder.Services.AddScoped<ICommandHandlerAsync<Create[EntityName]Command>,
                           Create[EntityName]CommandHandler>();
builder.Services.AddScoped<IValidator<Create[EntityName]Command>,
                           Create[EntityName]CommandValidator>();

// Query handler
builder.Services.AddScoped<IQueryHandlerAsync<Get[EntityName]Query, [EntityName]ViewModel?>,
                           Get[EntityName]QueryHandler>();

// Domain event handler, if any — these used to live in EventWorker
builder.Services.AddScoped<IEventHandlerAsync<[EntityName]CreatedEvent>,
                           [EntityName]CreatedHandler>();

// Domain service, only if you created one
builder.Services.AddScoped<I[Name]Service, [Name]Service>();
```

### ~~6.x Register command consumer / event consumer / CommandService~~ — **REMOVED**
No `CommandConsumerAdapter`, no `EventConsumerAdapter`, no `CustomQueueConfig`, no queue name, no
CommandService.

---

## Step 7 — Controller

**File**: `src/Api/Controllers/[EntityName]Controller.cs`
- Inject `IInMemoryDispatcher`. Nothing else — no repository, no DbContext, no handler.
- Mint the id server-side, stamp `UserContext` and `CorrelationId`, dispatch, map the result.
- Return `201`/`200` with the real result, or `400` with `ValidationResult`. **Never `202`.**

---

## Step 8 — Tests

| Test | Asserts |
|---|---|
| Aggregate invariants | The rule holds; the illegal transition throws. **No database** |
| Handler happy path + each violation | `CommandResponse` shape, against a real Postgres |
| Query projection | Correct rows, correct shape, paginated |
| End to end | Through the Api, real HTTP, real Postgres |

---

## Quick Reference — File Count per Feature

| Type | Min Files | Notes |
|------|-----------|-------|
| Simple CRUD (Create only) | ~9 | All steps above |
| Full CRUD (Create/Update/Toggle) | ~14 | Add update command/handler/mapper/validator/event |
| Cross-aggregate rule | ~16 | Add a Domain Service and its tests |

> Down from ~14 / ~20 / ~25+, because the CommandService pair, the rejection event, the projection
> event handler and the three worker registrations are all gone.
