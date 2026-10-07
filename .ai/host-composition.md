# Host Composition

There is now **one host**: the Api. CommandWorker and EventWorker are removed (ADR-010).

This document is the rule for how its `Program.cs` is composed. The template is
`.ai/templates/api-program.cs` — which still shows the old three-host shape; the corrected
version is below.

---

## 1. `Program.cs`

```csharp
using CatEcommerce.Application.CommandHandlers.Cat;
using CatEcommerce.Application.Commands.Cat;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Cat;
using CatEcommerce.Read.QueryHandlers.Cat;
using CatEcommerce.Read.ViewModels;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Extensions;
using Platform.Infrastructure.Core.Queries;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// ── Platform: the in-memory bus. This is the whole dispatch pipeline. ──────────
// On Core 1.0.34 this registers exactly IResolver, IHandlerResolver, IInMemoryDispatcher,
// IValidationService, IValidationProvider, ICommandSender, IQueryProcessor — all Scoped,
// and nothing else. No IEventPublisher, no broker type. Do NOT hand-register any of
// them — see §5. Signature: AddInMemoryBusServices(params Type[] types)
builder.Services.AddInMemoryBusServices();

// ── REQUIRED: the one seam the platform declares but does not fill (ADR-018) ──
// CommandSender needs IUserContextProvider and no implementation ships. Without it
// the host will not start.
builder.Services.AddScoped<IUserContextProvider, DevUserContextProvider>();

// ── REMOVED: AddServiceBusProvider(builder.Configuration) ─────────────────────
// That call routed commands out to RabbitMQ. Without it, AddInMemoryBusServices()
// dispatches in-process, which is the entire point of the change. Do not re-add it.

// ── Persistence: EF Core over PostgreSQL, replacing the MongoDB repositories ──
builder.Services.AddDbContext<CatEcommerceDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("CatEcommerce")));

// Our EF Core implementation of the platform's aggregate repository contract.
// (Replaces AddAggregateRootRepository() / AddReadRepository() from
//  Platform.Infrastructure.Repository.MongoDb — see .ai/persistence-ef-core.md.)
builder.Services.AddScoped(typeof(IAggregateRootRepository<>), typeof(EfAggregateRootRepository<>));

// ── Your own: Command Handlers, Validators, Query Handlers, Domain Services ───
// Command handlers run in THIS host now — there is no CommandWorker.
builder.Services.AddScoped<ICommandHandlerAsync<RegisterCatCommand>, RegisterCatCommandHandler>();
builder.Services.AddScoped<IValidator<RegisterCatCommand>, RegisterCatCommandValidator>();

builder.Services.AddScoped<IQueryHandlerAsync<GetCatQuery, CatViewModel?>, GetCatQueryHandler>();

// Domain services — only where domain logic spans aggregates (architecture.md §4).
// builder.Services.AddScoped<IStockAllocationService, FefoStockAllocationService>();

// Domain event handlers — in-process now, no EventConsumerAdapter.
// builder.Services.AddScoped<IEventHandlerAsync<CatVaccinatedEvent>, RecalculateStatusHandler>();

var app = builder.Build();

// This host does not call the platform's UseHttpPipeline() (ADR-342) — decompiled, it bundles
// routing, its own body-reading GlobalExceptionHandlerMiddleware and an allow-any-origin CORS
// policy into one call, with no seam to keep one piece and drop another. The pipeline is wired
// explicitly instead, once, as a shared host-level block ahead of every area:
app.UseForwardedHeaders();                          // no-op unless a trusted proxy/network is configured
app.UseRouting();
app.UseMiddleware<SafeExceptionHandlerMiddleware>(); // the ONLY exception handler; never reads the body
app.UseCors(HostPipelineExtensions.CorsPolicyName);  // explicit allow-list, empty = fail closed

app.UseAccountsModule();          // each area's own Use<Area>Module() call goes here (ADR-341/ADR-342)

// UseRateLimiter() (HO-NTF-001 gate fix, 2026-09-27, carried from HO-ADM-001-ARCHITECTURE MEDIUM 1,
// integrator-authorised): host composition, not owned by any area, now that a second area
// (Notifications) contributes its own AddRateLimiter(...) policies. MUST run after
// app.UseAccountsModule() -- a customer/IP-partitioned policy reads ResolvedCaller from
// HttpContext.Items, written by SessionAuthenticationMiddleware inside UseAccountsModule(). The
// 429 response shape (OnRejected) is registered once, in AddHostPipelineServices, alongside the
// forwarded-headers/CORS setup; each area's own module adds only its own named policies.
app.UseRateLimiter();

app.MapHealthChecks("/health");   // UseHttpPipeline() used to map this internally
app.MapControllers();

app.Run();
```

> **`UseHttpPipeline()` is not called.** See ADR-342 in `.ai/decisions/adr.md`,
> `src/Api/CatEcommerce.Api/Program.cs` and
> `src/Api/CatEcommerce.Api/Hosting/HostPipelineExtensions.cs` for the exact, current pipeline and
> why it was dropped: the platform's own exception handler logs request bodies verbatim (a
> credential leak) and its CORS policy allows any origin with credentials.

### What changed from the previous Api host

| Was | Now |
|---|---|
| `AddServiceBusProvider(builder.Configuration)` | **Removed.** No broker |
| `AddReadRepository()` only — "the Api never touches an aggregate" (ADR-008) | The Api owns the write path too. It registers the aggregate repository |
| Command handlers registered in CommandWorker | Registered **here** |
| Query handlers registered here | **Unchanged** |
| `BsonSerializer.RegisterSerializer(...)` allow-list in the workers | Gone with MongoDB |
| `CustomQueueConfig`, `CommandConsumerAdapter<T>`, `EventConsumerAdapter<T>` | Gone with the queues |
| `app.UseHttpPipeline(enableAuthorization: false)` | **Removed (ADR-342).** Replaced by an explicit shared pipeline block — see above |

---

## 2. There is no assembly scanning

Unchanged from before, and still the most common cause of a silent failure: **every** Command
Handler, Query Handler, Validator, Domain Service and Event Handler must be registered explicitly
in `Program.cs`.

The failure mode is better than it used to be — with one container, a missing registration throws
at dispatch naming the type, rather than a message vanishing onto an unbound queue. But it still
will not fail at compile time.

---

## 3. Lifetimes

| Service | Lifetime | Why |
|---|---|---|
| `DbContext` | **Scoped** | One per request — the unit of work boundary |
| `IAggregateRootRepository<T>` (our EF implementation) | **Scoped** | Holds the DbContext |
| Command handlers, Query handlers | **Scoped** | Hold repositories |
| Validators | **Scoped** | May hold a repository for async rules |
| Domain services | **Scoped** | Hold repositories |
| Event handlers | **Scoped** | Run in the request |
| Outbox dispatcher | **Singleton** (hosted) | Creates its own scope per poll |

> The old Singleton-`IAggregateRootRepository`-over-Scoped-`IDispatcher` conflict (ADR-008) came
> from the MongoDB package registering its repository as a Singleton. Our EF Core implementation
> is Scoped, so the conflict is gone. **Run with `ValidateScopes` and `ValidateOnBuild` on** —
> the previous architecture had to consider turning them off, and this one does not.

```csharp
builder.Host.UseDefaultServiceProvider((_, o) =>
{
    o.ValidateScopes = true;
    o.ValidateOnBuild = true;
});
```

---

## 4. Transaction boundary

One command, one transaction:

1. The platform's `IValidationService` runs the registered `IValidator<TCommand>`.
2. `IInMemoryDispatcher.SendAsync(command)` resolves and runs `ICommandHandlerAsync<TCommand>`.
3. The handler loads the aggregate and calls
   `repository.UpdateAsync(aggregate, EventPublishOption.InMemoryPublishOnly)`.
4. Our EF repository calls `SaveChangesAsync()`, then dispatches the aggregate's domain events
   in-process — resolving `IEventHandlerAsync<TEvent>` from DI by each event's **runtime type**
   and invoking it — and clears them. There is no `IEventPublisher` and no bus (ADR-020).

**Open decision for the first slice:** whether `SaveChangesAsync` fires per repository call (as
above) or once per request behind a commit filter. Per-call is simpler and matches the platform
contract; once-per-request makes a multi-write command atomic. Record the choice as an ADR.

---

## 5. Never hand-register

`AddInMemoryBusServices()` covers the entire dispatch pipeline. Writing any of these by hand means
you have duplicated it:

`IInMemoryDispatcher` · `IResolver` · `IHandlerResolver` · `ICommandSender` · `IQueryProcessor` ·
`IValidationService` · `IValidationProvider`

Also never register, because they no longer exist here:

`AddServiceBusProvider` · `IBusMessageDispatcher` · `IDispatcher` · `IEventPublisher` ·
`AddAggregateRootRepository()` / `AddReadRepository()` (MongoDB) · `IReadRepository` ·
`IStateRepository` · `IEventRepository` · `IMongoDbConnectionCache` · `IServiceRegistryProvider`

> **`IBusMessageDispatcher` is not an exception to this — it is the clearest case of it.** An
> earlier version of this section said it was "now required" because `EventPublisher` depended on
> it. That was true only of Core 1.0.32. On **1.0.34** `AddInMemoryBusServices()` does not register
> `IEventPublisher` at all, the container builds with `ValidateScopes` and `ValidateOnBuild` on and
> nothing bound to `IBusMessageDispatcher`, and the type is the RabbitMQ leg of a broker that is
> not deployed. It must not appear anywhere in this solution. See ADR-020.

**Do** register explicitly: `IUserContextProvider`, handlers, validators, domain services, event
handlers, the DbContext, and the EF aggregate repository.

---

## 6. Configuration

One connection string. No `BusConfig`, no `config/dev-services.json`, no
`IServiceRegistryProvider`.

```jsonc
{
  "ConnectionStrings": {
    "CatEcommerce": "Host=localhost;Port=5432;Database=catecommerce_dev;Username=postgres;Password=..."
  },
  "Logging": { "LogLevel": { "Default": "Information" } }
}
```

> The `appsettings` layering trap from the worker hosts is gone with them.
> `WebApplicationBuilder` layers `appsettings.json` → `appsettings.{Environment}.json` normally
> and reads `ASPNETCORE_ENVIRONMENT` as expected. Configuration no longer has to be repeated in
> full in every environment file.
