# MongoDB Usage — SUPERSEDED

> **This document is obsolete. MongoDB is not used in this project.**
>
> Persistence is **PostgreSQL via EF Core**. See **`.ai/persistence-ef-core.md`**.
>
> This file is kept as a tombstone so that an old link, an old note, or an agent working from a
> stale memory lands on the correction rather than on nothing. Do not follow anything below as
> current guidance. See ADR-011 in `.ai/decisions/adr.md`.

---

## What replaced what

| Was (MongoDB) | Is now (PostgreSQL + EF Core) |
|---|---|
| `Platform.Infrastructure.Repository.MongoDb` | Our own EF Core implementation of the same contract, in `src/Infrastructure` |
| `IAggregateRootRepository<T>` (Mongo implementation, Singleton) | **The same contract**, implemented over EF Core, registered Scoped |
| `SaveAsync(agg, EventPublishOption.QueuePublishOnly)` | `SaveAsync(agg)` / `UpdateAsync(agg)` — the repository commits and publishes domain events in-process |
| `IReadRepository.GetItemsAsync` | The `DbContext` with `AsNoTracking()` + `Select`, in a Query Handler |
| `IStateRepository`, `IEventRepository` | Gone. One database, no event store |
| Three databases: read / state / event | One PostgreSQL database, optionally grouped into schemas per bounded context |
| Collections named `$"{typeof(T).Name}s"` implicitly | Explicit `ToTable("cat")` in an `IEntityTypeConfiguration<T>` |
| `[BsonIgnoreExtraElements]` on every class, not inherited | Nothing. Mapping is external to the domain |
| `Expression<Func<T,bool>>` passed to a repository | LINQ against `DbSet<T>` inside the repository |
| "No transaction API exists — do not reach for `ITransactionalRepository`" | Real transactions exist via EF Core |
| `scripts/create-mongo-indexes.js`, run manually with `mongosh` | Indexes declared in EF configuration, applied by migrations |
| `config/dev-services.json` mapping tenant → Mongo database | A standard connection string per environment |
| `BsonSerializer.RegisterSerializer` allow-list at host startup | Gone — nothing polymorphic is serialized to the database |

## The one rule that survived, in better form

The old guidance warned against unbounded embedded arrays in a document (ADR-006). The
relational equivalent of that concern is not about storage but about aggregate size:

**An aggregate must not load an unbounded collection.** Where history grows without limit —
weight history, status history, boarding care logs, stock ledger entries — keep it out of the
aggregate and query it from the read side.

See `.ai/persistence-ef-core.md` §2.
