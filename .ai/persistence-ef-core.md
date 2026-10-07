# Persistence — EF Core over PostgreSQL

Database: **PostgreSQL** via **EF Core** (`Npgsql` provider), in `src/Infrastructure/`.

> Replaces `.ai/mongodb.md`. See ADR-011.
>
> `IAggregateRootRepository<TAggregate>` is declared in **`Platform.Infrastructure.Core`**
> (verified, ADR-018). Only the MongoDB *implementation* is replaced — we implement the platform
> contract itself over EF Core, so command handlers are unchanged.

---

## 1. The repository contract

**Verified contract** (`Platform.Infrastructure.Core.Domain`, v1.0.34) — this is what we must
implement:

```csharp
public interface IAggregateRootRepository<T>
{
    Task<T>    GetByIdAsync(Guid id);
    Task<T>    GetByFilterAsync(Expression<Func<T, bool>> dataFilters);
    Task<bool> ExistAsync(Expression<Func<T, bool>> dataFilters);
    Task       SaveAsync(T aggregate, EventPublishOption eventPublishOption);
    Task       UpdateAsync(T aggregate, EventPublishOption eventPublishOption);
    Task       UpdateAsync(T aggregate, int expectedVersion, EventPublishOption eventPublishOption);
}
```

`EventPublishOption` is **retained**, and 1.0.32 added the in-memory members:

| Member | Value | Use |
|---|---|---|
| `DoNotPublish` | 0 | Suppress events entirely |
| **`InMemoryPublishOnly`** | 1 | **Our default** — publish domain events in-process |
| `InMemoryAndQueuePublish` | 2 | Both, if a broker is ever reintroduced |
| `QueuePublishOnly` | 3 | Queue only — **never use; there is no queue** |

Note the `UpdateAsync(aggregate, expectedVersion, option)` overload: the platform has
version-based optimistic concurrency built in, complementing the `xmin` row version in §6.

## 2. The EF Core implementation

One generic implementation covers every aggregate:

```csharp
namespace CatEcommerce.Infrastructure.Repositories;

public sealed class EfAggregateRootRepository<TAggregate>(
    CatEcommerceDbContext db,
    IServiceProvider serviceProvider)
    : IAggregateRootRepository<TAggregate> where TAggregate : AggregateRoot
{
    public async Task<TAggregate?> GetByIdAsync(Guid id) =>
        await db.Set<TAggregate>()
                .Where(a => a.Id == id && !a.IsMarkedToDelete)
                .FirstOrDefaultAsync();

    public async Task<TAggregate?> GetByFilterAsync(Expression<Func<TAggregate, bool>> filter) =>
        await db.Set<TAggregate>().Where(a => !a.IsMarkedToDelete).FirstOrDefaultAsync(filter);

    // NOTE: .Where(...).AnyAsync(filter), NOT AnyAsync(a => ... && filter.Compile()(a)).
    // Compiling the expression inside the predicate makes it opaque to the provider: it cannot
    // be translated to SQL and throws at runtime.
    public Task<bool> ExistAsync(Expression<Func<TAggregate, bool>> filter) =>
        db.Set<TAggregate>().Where(a => !a.IsMarkedToDelete).AnyAsync(filter);

    public async Task SaveAsync(TAggregate aggregate, EventPublishOption option)
    {
        db.Set<TAggregate>().Add(aggregate);
        await CommitAsync(aggregate, option);
    }

    public async Task UpdateAsync(TAggregate aggregate, EventPublishOption option)
    {
        db.Set<TAggregate>().Update(aggregate);
        await CommitAsync(aggregate, option);
    }

    public async Task UpdateAsync(TAggregate aggregate, int expectedVersion, EventPublishOption option)
    {
        if (aggregate.Version != expectedVersion)
            throw new DbUpdateConcurrencyException(
                $"Expected version {expectedVersion} but the aggregate is at {aggregate.Version}.");

        await UpdateAsync(aggregate, option);
    }

    private async Task CommitAsync(TAggregate aggregate, EventPublishOption option)
    {
        await db.SaveChangesAsync();

        if (option is EventPublishOption.DoNotPublish) return;
        if (option is EventPublishOption.QueuePublishOnly)
            throw new NotSupportedException("There is no queue. Use InMemoryPublishOnly.");

        // InMemoryPublishOnly / InMemoryAndQueuePublish → dispatch in-process.
        //
        // Resolve on the event's RUNTIME type. aggregate.Events is ReadOnlyCollection<Event>,
        // so anything generic in the loop variable binds to Event and finds no handler for the
        // concrete event type. This is the whole reason the loop looks like this.
        foreach (var domainEvent in aggregate.Events)
        {
            var handlerType = typeof(IEventHandlerAsync<>).MakeGenericType(domainEvent.GetType());
            var handleAsync = handlerType.GetMethod(nameof(IEventHandlerAsync<Event>.HandleAsync))!;

            foreach (var handler in serviceProvider.GetServices(handlerType))
                await (Task)handleAsync.Invoke(handler, [domainEvent])!;
        }

        aggregate.ClearEvents();
    }
}
```

> **There is no `IEventPublisher` and no `IBusMessageDispatcher`.** `AddInMemoryBusServices()` does
> not register either on Core 1.0.34, and the second is the RabbitMQ leg of a broker that is not
> deployed. In-process domain event dispatch is code this solution owns — the loop above is all of
> it. See ADR-020.
>
> A handler that throws propagates out of `SaveAsync`/`UpdateAsync` and fails the command **after**
> `SaveChangesAsync` has already committed. Decide per handler whether that is what you want, and
> say so in the handler's summary comment — `.ai/domain-events-and-outbox.md` §3.

Registered once, open-generic:

```csharp
builder.Services.AddScoped(typeof(IAggregateRootRepository<>), typeof(EfAggregateRootRepository<>));
```

**Loading the whole aggregate.** The generic `GetByIdAsync` above does not `Include` child
collections. Configure them as **owned types** (§4) and EF Core loads them automatically. Where an
aggregate has a child that is not owned, add a specific repository for that aggregate rather than
weakening the generic one — a partially loaded aggregate cannot enforce its own invariants.

---

## 3. The DbContext

One DbContext for the solution, in `src/Infrastructure/Persistence/`:

```csharp
public sealed class CatEcommerceDbContext(DbContextOptions<CatEcommerceDbContext> options)
    : DbContext(options)
{
    public DbSet<CatAggregate> Cats => Set<CatAggregate>();
    public DbSet<AppointmentAggregate> Appointments => Set<AppointmentAggregate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatEcommerceDbContext).Assembly);
}
```

Optionally group tables by bounded context with a schema (`clinic.*`, `boarding.*`) — useful for
readability and permissions, and it costs nothing now. It is **not** a boundary: this is one
solution with one DbContext.

---

## 4. Mapping stays out of the domain

The Domain project has no EF Core reference and no persistence attributes. `[BsonIgnoreExtraElements]`
is gone. All mapping is in `IEntityTypeConfiguration<T>` classes in Infrastructure.

```csharp
public sealed class CatConfiguration : IEntityTypeConfiguration<CatAggregate>
{
    public void Configure(EntityTypeBuilder<CatAggregate> builder)
    {
        builder.ToTable("cat", "clinic");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.MicrochipNumber).HasMaxLength(20);

        // value object → owned, no separate identity
        builder.OwnsOne(x => x.Weight, w =>
        {
            w.Property(p => p.Kilograms).HasColumnName("weight_kg").HasPrecision(5, 2);
            w.Property(p => p.RecordedOn).HasColumnName("weight_recorded_on");
        });

        // aggregate child collection → owned, loads and saves with the aggregate
        builder.OwnsMany(x => x.Vaccinations, v =>
        {
            v.ToTable("cat_vaccination", "clinic");
            v.WithOwner().HasForeignKey("cat_id");
            v.HasKey("Id");
            v.HasIndex(p => p.NextDueOn);      // drives the due/overdue report, SRS FR-CAT-7
        });

        // the aggregate exposes IReadOnlyList over a private backing field
        builder.Metadata
            .FindNavigation(nameof(CatAggregate.Vaccinations))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Property<uint>("xmin").IsRowVersion();     // optimistic concurrency, §6
        builder.Ignore(x => x.Events);                     // AggregateRoot.Events — never persisted
    }
}
```

### `OwnsMany`, not `HasMany`, for aggregate children

A vaccination has no life outside its cat. Mapping it as **owned** means EF Core loads, saves and
deletes it with the aggregate, and nothing can query or modify it independently — the persistence
model matches the aggregate boundary instead of quietly contradicting it.

Use `HasMany` only between **separate aggregates**, and then reference by id rather than
navigation.

> ADR-006 argued for one-document-per-entry child collections to dodge MongoDB's 16 MB document
> limit. In a relational database that concern disappears. The surviving rule is about aggregate
> size, not storage: **an aggregate must not load an unbounded collection.** Where history grows
> without limit — weight history, status history, care logs, stock ledger entries — keep it out of
> the aggregate and query it from the Read layer.

---

## 5. Domain events, without the queue

`EventPublishOption.QueuePublishOnly` handed the aggregate's domain events to RabbitMQ for
EventWorker. Core 1.0.32 added **`InMemoryPublishOnly`**, which is what we pass instead. With no
queue and no worker:

- the repository dispatches them **in-process** after `SaveChangesAsync`, resolving
  `IEventHandlerAsync<TEvent>` from DI by each event's **runtime type** (§2). The platform supplies
  no publisher for this — `IEventPublisher` is not registered on 1.0.34 and there is no bus
  dispatcher (ADR-020);
- `IEventHandlerAsync<TEvent>` implementations still handle them — registered explicitly in
  `Program.cs`, no `EventConsumerAdapter`;
- they are for **side effects**, not projections. See `.ai/domain-events-and-outbox.md`.

Verified: `AggregateRoot` exposes `ReadOnlyCollection<Event> Events` and a public
`ClearEvents()`, alongside `Id`, `Version`, `IsMarkedToDelete`, `CreatedBy/Date`,
`LastUpdatedBy/Date`, `TenantId`, `VerticalId`, `Metadata`, `Tags`, and the protected
`AddDomainEvent(Event)` / `UpdateVersion()`.

---

## 6. Concurrency

```csharp
builder.Property<uint>("xmin").IsRowVersion();
```

PostgreSQL's system column, so it costs no extra column and no application bookkeeping. Two staff
editing the same cat now produce a `DbUpdateConcurrencyException` on the second save, which the
Api maps to `409 Conflict` telling the user to reload.

The previous architecture had no concurrency guard at all — silent last-write-wins, which is not
acceptable for clinical records. `AggregateRoot.UpdateVersion()` remains the domain-level version;
`xmin` is the database-level guard beneath it.

---

## 7. Indexes

Declared in the EF configuration, applied by migrations — versioned, reviewed, deployed with the
code.

> This replaces the manual `scripts/create-mongo-indexes.js` run with `mongosh`, and closes the
> old open question about whether index creation should stay manual.

Index at minimum: every column filtered on in a list screen; every id column (`cat_id`,
`owner_id`); `expiry_date` on batches (FEFO is on the hot path of every order, SRS FR-IMP-7);
`next_due_on` on vaccinations; and natural keys that must be unique — microchip, invoice number,
SKU barcode.

```csharp
builder.HasIndex(x => x.MicrochipNumber)
       .IsUnique()
       .HasFilter("microchip_number IS NOT NULL");    // partial: nulls allowed
```

---

## 8. Types worth getting right

| Domain concept | Column type | Note |
|---|---|---|
| Money | `numeric(19,4)` + `char(3)` currency | SRS C-6 — never `float`/`double` |
| Timestamps | `timestamptz` | SRS C-7 — `timestamp` silently loses the offset |
| Dates without time | `date` → `DateOnly` | DOB, expiry, administered-on |
| `Enumeration` | `int` via value conversion on `Value` | `.ai/enumeration-pattern.md` |
| Ids | `uuid` | Minted in the controller, as they always were |
| Attachments | object storage key, not `bytea` | SRS NFR-SEC-5 |

---

## 9. Migrations

```bash
dotnet ef migrations add AddCatVaccinationIndex \
  --project src/Infrastructure/{Root}.Infrastructure \
  --startup-project src/Api/{Root}.Api \
  --output-dir Migrations
```

- **Review every generated migration before committing.** EF Core generates `DROP COLUMN` for a
  rename, which destroys data.
- Forward-only in production; a mistake is corrected by a new migration.
- Auto-applied in development; an explicit deployment step in production (SRS NFR-MNT-5).
