# New Project Bootstrap

How to stand up a new solution on the `Platform.Infrastructure.*` packages with **one host**:
the web API. DDD aggregates, in-memory CQRS, PostgreSQL via EF Core.

Read `.ai/host-composition.md` first — it is the rule for how the host is composed. This file is
the recipe for everything around it: projects, packages, configuration and the first slice.

> **Superseded:** the previous version bootstrapped three hosts (Api + CommandWorker +
> EventWorker) on RabbitMQ and MongoDB. The workers and the broker are gone; the platform
> libraries and the layer structure are not. See ADR-010, ADR-011, ADR-017.

---

## 1. Prerequisites

| Need | Why |
|---|---|
| **PostgreSQL 14+** reachable | The only datastore |
| **.NET 9 SDK** | |
| **Private NuGet feed credentials** | The `Platform.Infrastructure.*` packages are internal — still required |
| **Docker** (recommended) | Postgres locally, Testcontainers for integration tests |
| ~~RabbitMQ~~ | **No longer needed.** Dispatch is in-memory |
| ~~MongoDB~~ | **No longer needed.** PostgreSQL instead |

`NuGet.Config` references the feed with **environment-variable placeholders**, never literal
credentials:

```xml
<packageSourceCredentials>
  <brac-package>
    <add key="Username" value="%BRAC_NUGET_USERNAME%" />
    <add key="ClearTextPassword" value="%BRAC_NUGET_TOKEN%" />
  </brac-package>
</packageSourceCredentials>
```

---

## 2. Solution layout

```
src/
  Domain/{Root}.Domain/            aggregates, domain events, value objects, domain DTOs,
                                   domain services
  Application/{Root}.Application/  commands, command handlers, validators, mappers,
                                   domain event handlers
  Read/{Root}.Read/                view models, queries, query handlers
  Infrastructure/{Root}.Infrastructure/
                                   DbContext, EF configurations, migrations,
                                   IAggregateRootRepository<T> implementation, outbox
  Api/{Root}.Api/                  ← THE host: controllers, request DTOs, DI wiring
  Shared/{Root}.Shared/            Enumeration types, violation codes, notification keys
tests/
  {Root}.Domain.Tests/
  {Root}.Application.Tests/
  {Root}.IntegrationTests/
docs/
  handover/                        the executable instruction set per slice
  test-cases/                      TC-<id>.md — cases written before execution
  gates/                           <id>-<GATE>.md — one verdict file per gate run
```

Removed from the old layout: `Application/CommandWorker/` and `Read/EventWorker/`, and
`config/dev-services.json`.

`docs/test-cases/` and `docs/gates/` are not optional. The TEST gate writes its case sheet into
the first before running anything; each of the first four gates writes a verdict into the second
before reporting, and ACCEPTANCE refuses to certify a handover whose earlier gates left no
artifact.

**Project references, and the boundaries they enforce:**

- `Domain` → `Shared` only. The innermost layer.
- `Application` → `Domain`, `Shared`. **Never `Read`.**
- `Read` → `Infrastructure`, `Shared`. **Never `Application`.**
- `Infrastructure` → `Domain`, `Shared`.
- `Api` → `Application`, `Read`, `Infrastructure`, `Shared`.

> `Read` → `Infrastructure` is new: query handlers use the `DbContext` now that there is no
> `IReadRepository`. The rule that matters is unchanged — the write and read models never
> reference each other.

---

## 3. Package references

**Every project that touches the platform:**
```xml
<PackageReference Include="Platform.Infrastructure.Core" Version="1.0.31" />
<PackageReference Include="Platform.Infrastructure.Common" Version="1.0.8" />
```

**Api host:**
```xml
<PackageReference Include="Platform.Infrastructure.Host.WebApi" Version="1.0.14" />
```

**Infrastructure and Read:**
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.*" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.*" />
```

**Infrastructure only (migrations):**
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.*">
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
```

**Pinned transitive** — Core's built-in `FluentValidationProvider` is compiled against a specific
FluentValidation. Keep the pin and its comment:
```xml
<!-- Core's built-in FluentValidationProvider is compiled against FluentValidation 8.5.1 -->
<PackageReference Include="FluentValidation" Version="8.5.1" />
```

**No longer referenced:**
```xml
<!-- REMOVED: Platform.Infrastructure.MassTransit.Bus.RabbitMQ  — no broker -->
<!-- REMOVED: Platform.Infrastructure.Repository.MongoDb        — PostgreSQL instead -->
<!-- REMOVED: MassTransit / MassTransit.RabbitMQ / MongoDB.Driver pins — gone with them -->
```

> `Platform.Infrastructure.Host.Worker` was never a bus-worker dependency and is not one now.
> Do not reference it.

⚠️ Do not compose the pipeline with `UseHttpPipeline(...)`. Decompiled (`Host.WebApi` 1.0.14), it
bundles routing, an exception handler that reads and logs the full request body for a JSON `POST`,
and a CORS policy that allows any origin with credentials into one call, with no seam to keep one
piece and drop another — see ADR-342. Wire the pipeline explicitly instead, per
`.ai/host-composition.md` §1.

Still **verify** that `Platform.Infrastructure.Host.WebApi` does not transitively require the
RabbitMQ package before referencing it. If it does, that is a decision to record — see
`.ai/architecture.md` §6, item 4.

---

## 4. Configuration

One connection string. No `BusConfig`, no `ServiceRegistrationsFilePath`, no
`config/dev-services.json`.

```jsonc
{
  "ConnectionStrings": {
    "{Root}": "Host=localhost;Port=5432;Database={root}_dev;Username=postgres;Password=postgres"
  },
  "Logging": { "LogLevel": { "Default": "Information" } }
}
```

> **The appsettings trap is gone with the workers.** `WorkerHostBuilder` read
> `ASPNETCORE_ENVIRONMENT` only and loaded exactly one `appsettings.{env}.json` without layering,
> which is why `BusConfig` had to be repeated in full in every environment file.
> `WebApplicationBuilder` layers normally. Configuration is written once.

**Never commit real credentials.** User-secrets locally; environment variables or a secret
manager elsewhere (SRS NFR-SEC-7).

---

## 5. The host

One `Program.cs`, composed per `.ai/host-composition.md`. Start from that document, not from
`.ai/templates/api-program.cs`, which still shows the three-host shape.

The one-line summary, because it is the thing most often got wrong:

| Call | Status |
|---|---|
| `AddInMemoryBusServices()` | **Keep** — this is the in-memory dispatch pipeline |
| `AddServiceBusProvider(configuration)` | **Remove** — it routes commands to RabbitMQ |
| `AddAggregateRootRepository()` / `AddReadRepository()` | **Remove** — MongoDB. Register the EF Core implementation instead |
| `UseHttpPipeline(...)` | **Do not call** — its bundled exception handler logs request bodies and its CORS policy allows any origin with credentials (ADR-342). Wire the pipeline explicitly per `.ai/host-composition.md` §1 |

---

## 6. First slice

Build one feature end to end before adding a second.

1. **Aggregate** — `AggregateRoot`, private setters, one mutation method that assigns state, calls
   `AddDomainEvent(...)`, then `UpdateVersion()`. **No `[BsonIgnoreExtraElements]`.** Put the
   invariants here.
2. **Domain event** — past tense, carrying what happened.
3. **EF configuration** — `ToTable`, `OwnsOne`, `OwnsMany`, `xmin` row version, indexes,
   `Ignore(x => x.DomainEvents)`.
4. **Migration** — generate it, then **read it** before committing.
5. **`IAggregateRootRepository<T>` implementation** over EF Core (`.ai/persistence-ef-core.md`).
6. **Command** — extends `Command`. Data only.
7. **Validator** — `AbstractValidator<TCommand>`, registered as `IValidator<TCommand>`.
8. **Handler** — `ICommandHandlerAsync<TCommand>`. Loads the aggregate from the repository
   **itself**, calls one mutation method, saves. No CommandService.
9. **ViewModel + query + query handler** — `AsNoTracking()`, `Select` into the ViewModel.
10. **Controller** — mint the id, stamp `UserContext`/`CorrelationId`, dispatch via `IInMemoryDispatcher`,
    return `201` with the real result or `400` with `ValidationResult`.
11. **Register** the handler, validator, query handler and any domain event handler in
    `Program.cs`. There is no assembly scanning.

Then **run the host** and exercise the slice. `POST` creates and returns the id; `GET` returns it
immediately.

---

## 7. Decisions to make early

Record each as an ADR in `.ai/decisions/adr.md`.

| Decision | Notes |
|---|---|
| **The four VERIFY items** in `.ai/architecture.md` §6 | In-memory dispatch member names, where `IAggregateRootRepository<T>` is declared, the in-process event publish path, and whether `Host.WebApi` drags in RabbitMQ. Settle all four in the first slice |
| **Where `SaveChangesAsync` fires** | In the repository per call, or once per request. Affects whether one command can be one transaction |
| **Column naming** | `snake_case` via a global convention, or explicit `HasColumnName`. Decide before the first migration |
| **HTTP-edge idempotency** | Synchronous dispatch cannot redeliver, but a user can double-submit (SRS FR-ORD-20) |
| **Auth and tenancy** | Until a real `IUserContextProvider` exists, a dev stand-in hands every caller the same identity |
| **Migration deployment** | Auto-apply in development; explicit step in production (SRS NFR-MNT-5) |

---

## 8. Sanity checklist

- [ ] Solution builds clean, and restores without the RabbitMQ or MongoDB packages
- [ ] The host **starts** — not just compiles
- [ ] `ValidateScopes` and `ValidateOnBuild` are on and the host still starts
- [ ] Migrations applied
- [ ] `POST` returns **`201` with the created id** — not `202`
- [ ] An invalid `POST` returns `400` naming the offending field, from the registered
      `IValidator<TCommand>` (not from a hand-written check in the controller)
- [ ] A business rule violation returns `400` with its `errorCode`, **and nothing was written**
- [ ] `GET` immediately after the `POST` returns the new record — no waiting, no polling
- [ ] Concurrent edits to one aggregate produce `409`, not silent last-write-wins
- [ ] A domain event handler ran in-process during the request
- [ ] No service from `.ai/host-composition.md` §5 "never hand-register" appears in `Program.cs`
- [ ] No `AddServiceBusProvider`, no `CustomQueueConfig`, no `BsonSerializer` call anywhere
