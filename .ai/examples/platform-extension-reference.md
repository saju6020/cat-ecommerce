# Platform Extension Reference — PARTLY SUPERSEDED

The `Platform.Infrastructure.*` libraries **are still the foundation of this solution** (ADR-017).
What changed is which extensions are called.

For what to register and with which lifetime, see **`.ai/di-registration-guide.md`** and
**`.ai/host-composition.md`**.

## Status of each extension

| Platform extension | Status |
|---|---|
| **`AddInMemoryBusServices()`** | ✅ **Keep — this is now the entire dispatch pipeline.** On Core 1.0.34 it registers exactly `IResolver`, `IHandlerResolver`, `IInMemoryDispatcher`, `ICommandSender`, `IQueryProcessor`, `IValidationService`, `IValidationProvider` — and nothing else. Never hand-register any of them. It registers **no** `IEventPublisher`, `IDispatcher` or `IBusMessageDispatcher`; do not add them (ADR-020) |
| **`UseHttpPipeline(...)`** (`Host.WebApi`) | ⛔ **Do not call (ADR-342).** Decompiled, it bundles routing, an exception handler that logs the full request body for a JSON `POST`, and a CORS policy that allows any origin with credentials into one call. The host wires the pipeline explicitly instead — see `.ai/host-composition.md` §1 |
| `AddServiceBusProvider(configuration, …)` | ⛔ **Remove.** It routed commands to RabbitMQ. Leaving it in is the most likely mistake when adapting an old `Program.cs` — commands go to a broker that is not there |
| `AddAggregateRootRepository()` | ⛔ Remove (MongoDB). The `IAggregateRootRepository<T>` **contract is kept** and implemented over EF Core — `.ai/persistence-ef-core.md` |
| `AddReadRepository()` | ⛔ Remove. Query handlers use the `DbContext` with `AsNoTracking()` |
| `AddEventRepository()` / `AddStateRepository()` | ⛔ Remove. No event store, no state store |
| `WorkerHostBuilder` / `HostBuilderOptions` | ⛔ Remove. `WebApplication.CreateBuilder` — one host |
| `CustomQueueConfig`, `CommandConsumerAdapter<T>`, `EventConsumerAdapter<T>` | ⛔ Remove. No queues |
| `IServiceRegistryProvider` + `dev-services.json` | ⛔ Remove. A connection string |

## The lifetime conflict is gone

ADR-008 recorded that `AddAggregateRootRepository()` registered `IAggregateRootRepository<>` as a
**Singleton** while `AddInMemoryBusServices()` registers `IDispatcher` as **Scoped**, which is why
scope validation had to be considered off. Our EF Core implementation is Scoped, so
`ValidateScopes` and `ValidateOnBuild` now stay **on** in every environment.
