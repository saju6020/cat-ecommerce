# Namespace Conventions

All namespaces follow the `{{RootNamespace}}.*` root. Below are the exact patterns per layer.

> **Rule**: Always match the exact namespace pattern. Do NOT invent new sub-namespaces.
>
> These are unchanged from before, except for the three rows marked **REMOVED** at the bottom.
> Removing the worker hosts did not change how code is organised within the layers.

## Application Layer (Write Side)

| Artifact | Namespace | Folder Structure |
|----------|-----------|------------------|
| Commands | `{{RootNamespace}}.Application.Commands.[Feature]` | Per-feature subfolder |
| Command Handlers | `{{RootNamespace}}.Application.CommandHandlers.[Feature]` | Per-feature subfolder |
| Command Validators | `{{RootNamespace}}.Application.CommandHandlers.Validators` | **Flat** — all validators in one folder |
| DataMappers | `{{RootNamespace}}.Application.DataMappers` | **Flat** |

> `Application.CommandServices` and `Application.CommandServices.Abstractions` are **removed**.
> Command handlers load aggregates through `IAggregateRootRepository<T>` directly. Cross-aggregate
> domain logic goes to `Domain.DomainServices` instead — see `.ai/command-handler-pattern.md` §5.

## Domain Layer

| Artifact | Namespace | Folder Structure |
|----------|-----------|------------------|
| Aggregates (simple) | `{{RootNamespace}}.Domain.Aggregates` | Root of Aggregates/ |
| Aggregates (complex/partial) | `{{RootNamespace}}.Domain.Aggregates.[Feature]` | Per-feature subfolder |
| Aggregate Validators | `{{RootNamespace}}.Domain.Aggregates.Validators` | **Flat** |
| Domain Events | `{{RootNamespace}}.Domain.Events.[Feature]` | Per-feature subfolder |
| Domain Models (DTOs) | `{{RootNamespace}}.Domain.Models` | **Flat** |
| Value Objects | `{{RootNamespace}}.Domain.ValueObjects` | **Flat** |
| Domain Services | `{{RootNamespace}}.Domain.DomainServices` | **Flat** |
| Entities | `{{RootNamespace}}.Domain.Entities` | **Flat** |

## Read Layer

| Artifact | Namespace | Folder Structure |
|----------|-----------|------------------|
| Queries | `{{RootNamespace}}.Read.Queries.[Feature]` | Per-feature subfolder |
| Query Handlers | `{{RootNamespace}}.Read.QueryHandlers.[Feature]` | Per-feature subfolder |
| ViewModels | `{{RootNamespace}}.Read.ViewModels` | **Flat** |

## Shared Layer

| Artifact | Namespace | Folder Structure |
|----------|-----------|------------------|
| Constants | `{{RootNamespace}}.Shared.Common.Constants` | **Flat** |
| Enums | `{{RootNamespace}}.Shared.Common.Enums` | **Flat** |
| Extensions | `{{RootNamespace}}.Shared.Common.Extensions` | **Flat** |
| Helpers | `{{RootNamespace}}.Shared.Common.Helpers` | **Flat** |
| SharedDto | `{{RootNamespace}}.Shared.SharedDto.[Feature]` | Per-feature subfolder |

## Infrastructure Layer

| Artifact | Namespace | Folder Structure |
|----------|-----------|------------------|
| Persistence (DbContext) | `{{RootNamespace}}.Infrastructure.Persistence` | **Flat** |
| EF Configurations | `{{RootNamespace}}.Infrastructure.Persistence.Configurations` | **Flat** |
| Repositories | `{{RootNamespace}}.Infrastructure.Repositories` | **Flat** |
| Migrations | `{{RootNamespace}}.Infrastructure.Migrations` | **Flat** (EF-generated) |
| Other infrastructure | `{{RootNamespace}}.Infrastructure.[Area]` | One folder per area |

`Infrastructure.Hosting` (`DevUserContextProvider`, `HostEnvironment`) is still valid — it is now
shared by **one** host rather than three.

## Api Layer

| Artifact | Namespace | Folder Structure |
|----------|-----------|------------------|
| Controllers | `{{RootNamespace}}.Api.Controllers` | Flat, or per-feature |
| Request/response DTOs | `{{RootNamespace}}.Api.Contracts.[Feature]` | Per-feature subfolder |

---

## Feature names

For this project, derived from the SRS bounded contexts — see `.ai/bounded-contexts.md`:

`Cat`, `Appointment`, `Visit`, `Surgery`, `Prescription`, `Vaccination`, `BoardingUnit`,
`BoardingStay`, `CareTask`, `Incident`, `Product`, `Sku`, `Supplier`, `PurchaseOrder`,
`Consignment`, `Batch`, `Stock`, `Order`, `Subscription`, `Invoice`, `Payment`, `Notification`.

---

## REMOVED namespaces

If you find these in code or in an older note, they belong to the superseded architecture:

| Removed | Why |
|---|---|
| `{{RootNamespace}}.Application.CommandServices[.Abstractions]` | Handlers use the repository directly; cross-aggregate logic moved to `Domain.DomainServices` |
| `{{RootNamespace}}.Domain.Events.ErrorEvents` | Rejection events no longer exist (ADR-014) |
| `{{RootNamespace}}.Read.EventHandlers.[Feature]` | No projections — query handlers read the tables (ADR-012) |
| Any `*.CommandWorker` / `*.EventWorker` project | Hosts removed (ADR-010) |

> Domain **event** handlers still exist — they run in-process for side effects. Place them in
> `{{RootNamespace}}.Application.DomainEventHandlers.[Feature]`, not in the Read layer, because
> they no longer build read models.
