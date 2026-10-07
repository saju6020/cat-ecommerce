using System.Linq.Expressions;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.Tests.TestSupport;

/// <summary>
/// Hand-written in-memory test double for IAggregateRootRepository&lt;T&gt; -- no mocking
/// library is in the allowed scope for this slice (HO-CAT-001 section 13). Records every
/// Save/Update call so handler tests can assert which aggregates were persisted.
/// </summary>
public sealed class FakeAggregateRootRepository<TAggregate> : IAggregateRootRepository<TAggregate>
    where TAggregate : AggregateRoot
{
    private readonly Dictionary<Guid, TAggregate> _store = [];

    public List<TAggregate> Saved { get; } = [];
    public List<TAggregate> Updated { get; } = [];

    public void Seed(TAggregate aggregate) => _store[aggregate.Id] = aggregate;

    public Task<TAggregate> GetByIdAsync(Guid id) =>
        Task.FromResult(_store.TryGetValue(id, out var value) ? value : null!);

    public Task<TAggregate> GetByFilterAsync(Expression<Func<TAggregate, bool>> dataFilters) =>
        Task.FromResult(_store.Values.AsQueryable().FirstOrDefault(dataFilters)!);

    public Task<bool> ExistAsync(Expression<Func<TAggregate, bool>> dataFilters) =>
        Task.FromResult(_store.Values.AsQueryable().Any(dataFilters));

    public Task SaveAsync(TAggregate aggregate, EventPublishOption eventPublishOption)
    {
        _store[aggregate.Id] = aggregate;
        Saved.Add(aggregate);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TAggregate aggregate, EventPublishOption eventPublishOption)
    {
        _store[aggregate.Id] = aggregate;
        Updated.Add(aggregate);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TAggregate aggregate, int expectedVersion, EventPublishOption eventPublishOption) =>
        UpdateAsync(aggregate, eventPublishOption);
}
