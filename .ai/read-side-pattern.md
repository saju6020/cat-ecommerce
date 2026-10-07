# Read Side Pattern

Queries, Query Handlers and ViewModels in `src/Read/`. The contracts are unchanged —
`Query<TResult>`, `IQueryHandlerAsync<TQuery, TResponse>`, `QueryResponse<T>` from
`Platform.Infrastructure.Core.Queries`. What changed is where the data comes from.

> **Superseded:** ViewModels are no longer documents in a read database maintained by projection
> event handlers. There is no `IReadRepository`, no EventWorker, no redelivery, no convergence
> problem and no lag. A query reads the same PostgreSQL tables the command just wrote. See ADR-012.

---

## 1. Where a query reads from

Through EF Core, with `AsNoTracking()`, projecting into a ViewModel with `Select`.

`src/Read/` references `src/Infrastructure/` for the `DbContext`. The rule that matters is
unchanged and still enforced: **`Application` never references `Read`, and `Read` never references
`Application`.** The write and read models stay separate; they simply share a database now.

```csharp
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Appointment;

public sealed class GetDayScheduleQueryHandler(
    ILogger<GetDayScheduleQueryHandler> logger,
    CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetDayScheduleQuery, IReadOnlyList<AppointmentListItem>>
{
    public async Task<QueryResponse<IReadOnlyList<AppointmentListItem>>> HandleAsync(
        GetDayScheduleQuery query)
    {
        var items = await db.Appointments
            .AsNoTracking()
            .Where(a => a.ScheduledOn == query.Day)
            .OrderBy(a => a.ScheduledFor)
            .Select(a => new AppointmentListItem(
                a.Id, a.CatName, a.ScheduledFor, a.Status.DisplayName))
            .Skip(query.Skip).Take(query.Take)
            .ToListAsync();

        return new QueryResponse<IReadOnlyList<AppointmentListItem>> { Result = items };
    }
}
```

---

## 2. Query

Filter criteria only. No logic, no defaults hiding a business rule.

```csharp
namespace CatEcommerce.Read.Queries.Appointment;

public sealed class GetDayScheduleQuery : Query<IReadOnlyList<AppointmentListItem>>
{
    public required DateOnly Day { get; init; }
    public Guid? VetId { get; init; }
}
```

> Do **not** redeclare paging. `Query<TResult>` already provides `PageNumber`, `PageSize`,
> `Skip`, `Take`, `DoCount`, `UserContext`, `CorrelationId` and `Vertical` (verified, Core
> 1.0.32). Redeclaring `Skip`/`Take` shadows the base and silently breaks paging.

---

## 3. ViewModel

Shaped for the screen, not for the domain.

```csharp
public sealed record AppointmentListItem(
    Guid Id, string CatName, DateTime ScheduledFor, string Status);
```

Gone with MongoDB and the projections: `ViewModelBase`, `[BsonIgnoreExtraElements]`, public
setters "for deserialization", and the `Map(SomeEvent e)` method. Nothing projects into a
ViewModel any more — the query handler builds it.

---

## 4. Rules

1. **Project, never rehydrate.** Loading a `CatAggregate` to render four columns pulls its whole
   object graph and its invariant machinery to produce four fields.
2. **`Select` before `ToListAsync`.** Projection happens in the database, not in memory.
3. **`AsNoTracking()` on every read.** A tracked read in a request that also writes can produce
   surprising saves.
4. **Paginate every list** (SRS UI-4). An unpaginated list of cats works fine for two years and
   then does not.
5. **A query never writes.** Not an audit row, not a "last viewed" stamp. If something must be
   recorded on read, dispatch a command.
6. **Index what you filter on** — in the EF configuration, so it lands in a migration
   (`.ai/persistence-ef-core.md` §7).
7. **Return `QueryResponse<T>`**, never a domain type or a tracked EF entity.

---

## 5. Reads that span aggregates

Reporting (SRS §8) legitimately spans contexts — revenue by business line needs appointments,
orders and boarding stays together. A query handler may join across them: it is one database and
one solution, and the write-side aggregate boundary does not constrain a read.

What it must not do is **bypass an aggregate to change something**. Reading widely is fine;
writing widely is not.

For heavy reports, prefer a **database view** created by a migration over a large LINQ join — it
keeps the query planner's job simple and the C# readable. Map it with `ToView(...)` and never
migrate it from the read side.
