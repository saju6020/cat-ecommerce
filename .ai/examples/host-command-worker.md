# Command Worker Host — SUPERSEDED

> **This host no longer exists.** There is no CommandWorker, no RabbitMQ consumer and no queued
> command path.
>
> Commands are dispatched **in memory** by the platform's `IDispatcher` inside the HTTP request,
> in the single Api host. See **`.ai/host-composition.md`** and **`.ai/cqrs.md`**.
>
> Kept as a tombstone so an old link lands on the correction. See ADR-010 and ADR-013.

## Where each part went

| Was in CommandWorker | Is now |
|---|---|
| `CommandConsumerAdapter<T>` registration | Nothing — handlers resolve from DI by assembly scan |
| `AddServiceBusProvider(...)` + `CustomQueueConfig` | Removed. No broker, no queue |
| `AddAggregateRootRepository()` (MongoDB) | The same `IAggregateRootRepository<T>` contract, implemented over EF Core and registered in `Program.cs` |
| `BsonSerializer` allow-list | Removed. No BSON |
| Handler execution after dequeue | Handler execution inside the request, inside a transaction |
| Retry on message redelivery | No redelivery. Failures return a `4xx`/`5xx` to the caller |
