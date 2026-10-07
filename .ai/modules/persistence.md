# Persistence Module

## Status: Not Started

## What it covers
EF Core over PostgreSQL in `src/Infrastructure/`: the `DbContext`, `IEntityTypeConfiguration<T>`
mappings, migrations, index strategy, and **the EF Core implementation of the platform's
`IAggregateRootRepository<TAggregate>` contract**.

See `.ai/persistence-ef-core.md` for the full pattern.

> Superseded: this module previously covered MongoDB via
> `Platform.Infrastructure.Repository.MongoDb` — `AddAggregateRootRepository()`,
> `IReadRepository`, collection naming and a manual `mongosh` index script. The package is
> dropped; its aggregate-repository **contract is kept**. See ADR-011 and the tombstone at
> `.ai/mongodb.md`.

## Key components
- `{{RootNamespace}}.Infrastructure.Persistence.{Root}DbContext`
- `EfAggregateRootRepository<TAggregate>` — implements the platform contract over EF Core
- `IEntityTypeConfiguration<T>` per aggregate
- `EnumerationConverter<T>` value converter
- Outbox table and `OutboxDispatcher`

## Key files
-

## Decisions made
- PostgreSQL + EF Core with the Npgsql provider (ADR-011)
- The platform's `IAggregateRootRepository<TAggregate>` contract is retained and reimplemented
- Concurrency via PostgreSQL `xmin` mapped as a row version (ADR-016)
- Indexes declared in EF configuration and applied by migrations, never by a manual script
- Soft delete is the default; clinical and financial records are never hard-deleted (SRS C-3, D-6)

## Remaining work
- **VERIFY** where `IAggregateRootRepository<TAggregate>` is declared — `Core` or
  `Repository.MongoDb` (`.ai/architecture.md` §6, item 2). It decides whether we implement the
  platform interface or mirror it
- **VERIFY** the in-process domain event publish path that replaces
  `EventPublishOption.QueuePublishOnly` (item 3)
- Decide where `SaveChangesAsync` fires — per repository call, or once per request
- Decide column naming: `snake_case` via a global convention, or explicit `HasColumnName`
- Establish the migration review checklist (the `DROP COLUMN`-on-rename trap)
- Decide the integration-test database strategy (Testcontainers per class, or one shared container)
