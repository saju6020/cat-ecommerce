# DI Registration Guide

What to register, where, and with which lifetime — now that there is **one host** and **one
container**.

Companion to `.ai/host-composition.md`, which shows the full `Program.cs`.

---

## 1. The two platform calls

```csharp
builder.Services.AddInMemoryBusServices();   // KEEP — this is the in-memory bus
// builder.Services.AddServiceBusProvider(builder.Configuration);   // REMOVED — RabbitMQ
```

`AddInMemoryBusServices()` (`Platform.Infrastructure.Core.Extensions`) registers the whole
dispatch pipeline:

`IResolver` · `IHandlerResolver` · `IInMemoryDispatcher` · `ICommandSender` · `IQueryProcessor` ·
`IValidationService` · `IValidationProvider`

That is the complete list on Core 1.0.34. **Never hand-register any of them** — if one appears in
`Program.cs`, you have duplicated the extension.

It registers **no** `IEventPublisher`, `IDispatcher` or `IBusMessageDispatcher`. Do not register
those either: the first two are gone and the third is the RabbitMQ leg of a broker that is not
deployed (ADR-020). Domain events are dispatched by the EF aggregate repository.

This call was already present in all three old hosts — it was simply being bypassed on the write
path by `AddServiceBusProvider()`, which routed commands to a broker. Removing the second call is
what makes dispatch in-memory.

---

## 2. There is still no assembly scanning

Every Command Handler, Query Handler, Validator, Domain Service and Event Handler is registered
**explicitly** in `Program.cs`. This has not changed.

What *has* improved: with one container, a handler can no longer be registered in the wrong host.
A missing registration now throws at dispatch naming the type, instead of a message vanishing onto
an unbound queue with no error anywhere.

---

## 3. Registration cheat sheet

| What you added | Register | Lifetime |
|---|---|---|
| Command | — | Nothing to register. It is data |
| Command handler | `AddScoped<ICommandHandlerAsync<XCommand>, XCommandHandler>()` | Scoped |
| Command validator | `AddScoped<IValidator<XCommand>, XCommandValidator>()` | Scoped |
| Query | — | Nothing to register |
| Query handler | `AddScoped<IQueryHandlerAsync<XQuery, XViewModel?>, XQueryHandler>()` | Scoped |
| Domain event handler | `AddScoped<IEventHandlerAsync<XEvent>, XEventHandler>()` | Scoped |
| **Domain service** | `AddScoped<IXDomainService, XDomainService>()` | Scoped |
| DbContext | `AddDbContext<CatEcommerceDbContext>(...)` | Scoped |
| Aggregate repository | `AddScoped(typeof(IAggregateRootRepository<>), typeof(EfAggregateRootRepository<>))` | Scoped |
| Aggregate / value object / entity | — | Never registered |
| External client (SMS, courier) | `AddHttpClient<IX, X>()` | Typed client |
| Outbox dispatcher | `AddHostedService<OutboxDispatcher>()` | Singleton |

> **`ICommandService` is not on this list.** That layer is removed — handlers load aggregates
> through `IAggregateRootRepository<T>` directly. A service is registered only when it is a real
> **Domain Service** for cross-aggregate domain logic (`.ai/command-handler-pattern.md` §5).

### Validator registration — the one that fails silently

```csharp
services.AddScoped<IValidator<RegisterCatCommand>, RegisterCatCommandValidator>();   // ✅
services.AddScoped<RegisterCatCommandValidator>();                                   // ❌ never runs
```

The platform's `IValidationService` resolves `IValidator<TCommand>`. Registered as the concrete
type or as `AbstractValidator<T>`, it is never found, never runs, and invalid data reaches the
aggregate with no error.

---

## 4. Lifetimes

Everything in the request path is **Scoped**: DbContext, repository, handlers, validators, domain
services, event handlers.

The reason matters. If a command handler and its domain event handler resolved **different**
DbContext instances, the event handler's writes would not join the handler's transaction — and
that failure is silent until a rollback fails to undo something. Scoped everywhere removes the
question.

```csharp
builder.Host.UseDefaultServiceProvider((_, o) =>
{
    o.ValidateScopes = true;
    o.ValidateOnBuild = true;
});
```

Keep both on, in every environment. The old reason for considering them off — the MongoDB package
registering `IAggregateRootRepository<>` as a Singleton over a Scoped `IDispatcher` (ADR-008) — is
gone: our EF Core implementation is Scoped.

The one legitimate Singleton that needs a Scoped service is the outbox dispatcher, and it resolves
correctly because it creates its own scope per poll rather than injecting one.

---

## 5. Common failures

| Symptom | Cause |
|---|---|
| Handler not found at dispatch | Not registered in `Program.cs`. There is no scanning |
| Validator silently not running | Registered as the concrete type or `AbstractValidator<T>` instead of `IValidator<T>` |
| Domain event handler's writes vanish | It resolved a different DbContext — check for a Transient in the chain |
| Startup: "Cannot consume scoped service from singleton" | Correct behaviour. Inject `IServiceScopeFactory` and create a scope |
| Command reaches no handler and no error appears | You still have `AddServiceBusProvider(...)` registered — it is routing to a broker that is not there |
| Two DbContext instances in one request | A Transient registration somewhere in the graph |
