# Event Worker Host — SUPERSEDED

> **This host no longer exists.** There is no EventWorker and no projection pipeline.
>
> Query handlers read the same tables the write side committed. See
> **`.ai/read-side-pattern.md`**.
>
> Kept as a tombstone so an old link lands on the correction. See ADR-010 and ADR-012.

## Where each part went

| Was in EventWorker | Is now |
|---|---|
| `EventConsumerAdapter<T>` registration | Nothing — no event consumers |
| Projection `IEventHandlerAsync<T>` writing a ViewModel | Removed. Nothing projects |
| `IReadRepository.CreateAsync(view)` | Removed. Query handlers project from tables with `Select` |
| "Project by assignment, never accumulation, so redelivery converges" | Moot. Nothing is redelivered |
| Read database | Removed. One PostgreSQL database, read by query handlers directly |

The only surviving background worker is `OutboxDispatcher`, which delivers messages **out of**
the process (SMS, email, courier). It does not project anything. See
`.ai/domain-events-and-outbox.md` §3.
