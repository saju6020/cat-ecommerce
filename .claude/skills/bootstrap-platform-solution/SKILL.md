---
name: bootstrap-platform-solution
description: Scaffold a new .NET solution on the internal Platform.Infrastructure packages with a single web API host, DDD aggregates and in-memory CQRS over PostgreSQL/EF Core. Use when starting a new project of this shape, or adding the skeleton to an empty repo.
---

# Bootstrap a platform solution (single host, in-memory CQRS)

Stands up a new solution on the `Platform.Infrastructure.*` packages: **one web API host** that
owns the domain, the write path and the read path, dispatching commands and queries **in memory**.

> **Updated 2026-09-19.** This skill previously scaffolded three hosts — web API, command worker,
> event worker — on RabbitMQ and MongoDB. The workers and the broker are gone, and the datastore
> is PostgreSQL. **The platform libraries and the layer structure are unchanged.** See ADR-010 to
> ADR-017 in `.ai/decisions/adr.md`.

## Read before doing anything

In this order. They are the authority; this skill is the procedure.

1. **`.ai/architecture.md`** — the layers, what the platform still provides, and the four API
   details that must be verified against the packages. The rule.
2. **`.ai/new-project-bootstrap.md`** — projects, package versions, configuration, the first
   slice. The recipe.
3. **`.ai/host-composition.md`** — how the one `Program.cs` is composed.
4. **`.ai/examples/host-web-api.md`** — a complete vertical slice to copy the shape of.

If those files are missing, this repo has not been set up for this workflow — say so rather than
improvising a solution shape from memory.

## Ask before scaffolding

Do not guess these. Wrong answers are expensive to unwind:

- **Root namespace and solution name** (e.g. `CatEcommerce`) — it lands in every namespace and every
  `.csproj`.
- **First bounded context and its first aggregate.** Building one feature end to end proves the
  wiring; an empty skeleton proves nothing.
- **PostgreSQL connection details**, and whether it is actually running.
- **NuGet feed credentials** — the `Platform.Infrastructure.*` packages are internal and restore
  fails without them, confusingly.
- **Column naming convention** — `snake_case` via a global EF convention, or explicit
  `HasColumnName`. Decide before the first migration; changing it later rewrites every table.

## Order of work

1. Solution and projects per `.ai/new-project-bootstrap.md` §2, with the project references that
   enforce the layer boundaries. `Application` must not reference `Read`.
2. Package references per §3. **Keep** `Platform.Infrastructure.Core`, `Common` and
   `Host.WebApi`, and the pinned `FluentValidation 8.5.1`. **Do not add**
   `Platform.Infrastructure.MassTransit.Bus.RabbitMQ` or `Platform.Infrastructure.Repository.MongoDb`.
   Do not add `Platform.Infrastructure.Host.Worker` — it was never a dependency of this shape.
3. **Settle the four VERIFY items** in `.ai/architecture.md` §6 against the actual assemblies
   before writing the first handler. They are the in-memory dispatch member names, where
   `IAggregateRootRepository<TAggregate>` is declared, the in-process event publish path, and
   whether `Host.WebApi` drags in RabbitMQ. Record the answers as an ADR.
4. Configuration per §4 — one connection string, no `BusConfig`, no `dev-services.json`. Create
   `docs/handover/`, `docs/test-cases/` and `docs/gates/` at the same time. The TEST gate writes
   its case sheet into the second before running anything, and each of the other gates writes its
   verdict into the third before reporting. Neither habit starts if the folder is missing.
5. `Program.cs` per `.ai/host-composition.md` §1. Keep `AddInMemoryBusServices()`; do **not** add
   `AddServiceBusProvider(...)`.
6. The EF Core implementation of the aggregate repository contract
   (`.ai/persistence-ef-core.md` §2).
7. The first slice per `.ai/new-project-bootstrap.md` §6, in the order given.
8. Run the host and exercise the slice.

## Things that will bite you

- **Leaving `AddServiceBusProvider(...)` in.** Commands route to a broker that is not running and
  nothing obvious fails. This is the single most likely mistake when adapting an old `Program.cs`.
- **Registering a validator as the concrete type** instead of `IValidator<TCommand>`. The
  platform's `IValidationService` resolves the interface — registered any other way, the validator
  silently never runs and invalid data reaches the aggregate.
- **Forgetting a registration.** There is still no assembly scanning. Every handler, validator,
  query handler, domain event handler and domain service is registered by hand in `Program.cs`.
- **Reaching for a CommandService.** The handler injects `IAggregateRootRepository<T>` and loads
  the aggregate itself. A service is justified only when it is a real **Domain Service** for logic
  spanning aggregates.
- **Putting an invariant in the handler.** If the rule can be checked from the aggregate's own
  state, it belongs in the aggregate. This is how a model turns anaemic.
- **A Transient in the request path.** A handler and its domain event handler then resolve
  different `DbContext` instances, and the event handler's writes never join the transaction.
- **EF Core generating `DROP COLUMN` for a rename.** Read every migration before committing it.
- **Money as `double`, timestamps as `timestamp` without a zone.** Both are silent data
  corruption (SRS C-6, C-7).

## Never hand-register

`AddInMemoryBusServices()` covers the entire dispatch pipeline: `IResolver`, `IHandlerResolver`,
`IDispatcher`, `ICommandSender`, `IEventPublisher`, `IQueryProcessor`, `IValidationService`,
`IValidationProvider`. If your `Program.cs` names any of them, you have duplicated the extension.

Also never register, because they are gone: `IBusMessageDispatcher`, `AddServiceBusProvider`,
`AddAggregateRootRepository()` / `AddReadRepository()` (MongoDB), `IReadRepository`,
`IStateRepository`, `IEventRepository`, `IMongoDbConnectionCache`, `IServiceRegistryProvider`.

## Finish by proving it, not by building it

Report the solution as working only after:

- the host **starts** with `ValidateScopes` and `ValidateOnBuild` on
- the solution restores with **no** RabbitMQ and **no** MongoDB package
- migrations applied
- `POST` returns **`201` with the created id** — not `202`
- an invalid `POST` returns `400` naming the offending field, produced by the registered
  `IValidator<TCommand>` and not by a hand-written check in the controller
- a business rule violation returns `400` with its `errorCode`, **and nothing was written**
- `GET` immediately after the `POST` returns the new record — no waiting, no polling
- a domain event handler ran in-process during the request
- concurrent edits to one aggregate produce `409`, not silent last-write-wins

**Record those checks as a test-case sheet in `docs/test-cases/`, not as prose in a reply** —
cases written first, each row marked as you observe it. `.claude/agents/test-engineer.md` carries
the format and is self-contained. A first slice signed off with "it works" and no sheet starts the
project with the habit already broken.

Then write the decisions from `.ai/new-project-bootstrap.md` §7 into `.ai/decisions/adr.md` while
the reasoning is fresh — especially the four VERIFY answers, where `SaveChangesAsync` fires, and
column naming. They are painful to retrofit.
