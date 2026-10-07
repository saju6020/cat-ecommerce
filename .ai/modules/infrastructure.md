# Infrastructure Module

## Status: Not Started

## What it covers
In-process dispatch through the platform bus, domain event publication, and everything that
crosses the process edge: the transactional outbox and its background dispatcher, and typed HTTP
clients for external services.

See `.ai/di-registration-guide.md` and `.ai/domain-events-and-outbox.md`.

> Superseded: this module previously covered MassTransit + RabbitMQ configuration, message bus
> setup, Saga state machine hosting, consumer registration and retry/fault queue policies. There
> is no broker and no saga. See ADR-010 and ADR-013.

## Key components
- `AddInMemoryBusServices()` — the platform's in-memory dispatch pipeline. **Not ours; never
  hand-registered**
- `EfAggregateRootRepository<T>` — dispatches aggregate domain events in-process after save, by
  resolving `IEventHandlerAsync<TEvent>` on the event's **runtime type**. The platform registers no
  `IEventPublisher` and there is no bus dispatcher (ADR-020)
- `OutboxMessage` + `OutboxDispatcher` (`BackgroundService`) — the only background worker
- Typed `HttpClient` wrappers: SMS gateway, email, courier API
- Exception handling mapping `DbUpdateConcurrencyException` to `409`

## Key files
-

## Decisions made
- In-memory dispatch via the platform bus; `AddServiceBusProvider()` removed (ADR-013)
- Commands are synchronous and return their real result; no `202`, no rejection events (ADR-014)
- Everything leaving the process goes through the outbox, at-least-once
- Scope validation (`ValidateScopes`, `ValidateOnBuild`) stays on in every environment

## Remaining work
- ~~**VERIFY** the in-memory dispatch member names~~ — done, Core 1.0.34. See `.ai/architecture.md`
  §6 and ADR-020
- ~~**VERIFY** that `Platform.Infrastructure.Host.WebApi` does not transitively require the RabbitMQ
  package~~ — done, it does not (item 4)
- Decide the outbox retry and dead-letter policy: attempt cap, backoff curve, who is alerted
- Add `Polly` policies to the external typed clients
- Define correlation-id propagation from HTTP header to `Command` to logs to outbox payload
