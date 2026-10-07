using System.Linq.Expressions;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Infrastructure.Repositories;

/// <summary>
/// Copied from .ai/persistence-ef-core.md section 2 exactly -- HO-CAT-001 section 12.1.
/// One generic implementation covers every aggregate.
/// </summary>
public sealed class EfAggregateRootRepository<TAggregate>(
    CatEcommerceDbContext db,
    IServiceProvider serviceProvider)
    : IAggregateRootRepository<TAggregate> where TAggregate : AggregateRoot
{
    public async Task<TAggregate> GetByIdAsync(Guid id) =>
        (await db.Set<TAggregate>()
            .Where(a => a.Id == id && !a.IsMarkedToDelete)
            .FirstOrDefaultAsync())!;

    public async Task<TAggregate> GetByFilterAsync(Expression<Func<TAggregate, bool>> dataFilters) =>
        (await db.Set<TAggregate>().Where(a => !a.IsMarkedToDelete).FirstOrDefaultAsync(dataFilters))!;

    // NOTE: .Where(...).AnyAsync(filter), NOT AnyAsync(a => ... && filter.Compile()(a)).
    // Compiling the expression inside the predicate makes it opaque to the provider: it cannot
    // be translated to SQL and throws at runtime.
    public Task<bool> ExistAsync(Expression<Func<TAggregate, bool>> dataFilters) =>
        db.Set<TAggregate>().Where(a => !a.IsMarkedToDelete).AnyAsync(dataFilters);

    public async Task SaveAsync(TAggregate aggregate, EventPublishOption eventPublishOption)
    {
        db.Set<TAggregate>().Add(aggregate);
        await CommitAsync(aggregate, eventPublishOption);
    }

    public async Task UpdateAsync(TAggregate aggregate, EventPublishOption eventPublishOption)
    {
        // Deviation from .ai/persistence-ef-core.md section 2, which calls db.Set<TAggregate>()
        // .Update(aggregate) unconditionally. Verified against a real Postgres end to end: every
        // command handler in this slice loads the aggregate via this same repository's
        // GetByIdAsync on the SAME scoped DbContext, mutates it, then calls UpdateAsync -- so
        // the aggregate is already tracked. DbSet.Update() on an already-tracked aggregate walks
        // the whole navigation graph and marks every NOT-yet-tracked reachable entity as
        // Modified rather than Added -- including an owned-collection item just added in this
        // request (e.g. CatAggregate.AddPhoto's new CatPhoto). EF then emits an UPDATE for a row
        // that does not exist yet, "0 rows affected", and the xmin concurrency check throws
        // DbUpdateConcurrencyException on a perfectly ordinary first write. Only call Update()
        // when the aggregate is genuinely untracked (e.g. reconstructed outside this context);
        // otherwise SaveChangesAsync's own change detection already sees every scalar edit and
        // every Added/Modified/Removed owned-collection item correctly.
        if (db.Entry(aggregate).State == EntityState.Detached)
            db.Set<TAggregate>().Update(aggregate);

        await CommitAsync(aggregate, eventPublishOption);
    }

    public async Task UpdateAsync(TAggregate aggregate, int expectedVersion, EventPublishOption eventPublishOption)
    {
        if (aggregate.Version != expectedVersion)
            throw new DbUpdateConcurrencyException(
                $"Expected version {expectedVersion} but the aggregate is at {aggregate.Version}.");

        await UpdateAsync(aggregate, eventPublishOption);
    }

    private async Task CommitAsync(TAggregate aggregate, EventPublishOption option)
    {
        await db.SaveChangesAsync();

        if (option is EventPublishOption.DoNotPublish) return;
        if (option is EventPublishOption.QueuePublishOnly)
            throw new NotSupportedException("There is no queue. Use InMemoryPublishOnly.");

        // InMemoryPublishOnly / InMemoryAndQueuePublish -> dispatch in-process.
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
