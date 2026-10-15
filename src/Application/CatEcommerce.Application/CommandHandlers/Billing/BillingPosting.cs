using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Billing;

/// <summary>A root mutated alongside the primary one of a posting (ADR-184): already tracked by the
/// shared <c>DbContext</c>, so the primary's save commits it; this only carries what the follow-up call
/// needs to dispatch its events.</summary>
public interface IPostingParticipant
{
    Task PublishEventsAsync();
}

internal sealed class PostingParticipant<TAggregate>(IAggregateRootRepository<TAggregate> repository, TAggregate aggregate) : IPostingParticipant
    where TAggregate : AggregateRoot
{
    // F31(b): the repository dispatches only the aggregate it was handed. A no-change UpdateAsync on a
    // tracked root writes nothing (SaveChanges finds no pending changes) and dispatches its events.
    public Task PublishEventsAsync() => repository.UpdateAsync(aggregate, EventPublishOption.InMemoryPublishOnly);
}

/// <summary>
/// HO-BIL-001 §8.1 / ADR-184 -- the recorded exception to "one aggregate mutated per command". Issue,
/// allocation, approval, reversal, credit note, refund and loyalty operations commit several Billing
/// roots in <b>one</b> <c>SaveChangesAsync</c> (atomic, <c>xmin</c> on each); the secondary roots'
/// events are then dispatched by a follow-up no-change <c>UpdateAsync</c> per root (F31). A static
/// helper over repositories -- not a service layer.
/// </summary>
public static class BillingPosting
{
    public static IPostingParticipant Of<TAggregate>(IAggregateRootRepository<TAggregate> repository, TAggregate aggregate)
        where TAggregate : AggregateRoot => new PostingParticipant<TAggregate>(repository, aggregate);

    /// <summary>The primary root already exists (it is tracked and modified).</summary>
    public static async Task CommitAsync<TPrimary>(
        IAggregateRootRepository<TPrimary> primaryRepository, TPrimary primary, params IPostingParticipant[] secondaries)
        where TPrimary : AggregateRoot
    {
        await primaryRepository.UpdateAsync(primary, EventPublishOption.InMemoryPublishOnly);
        await PublishSecondariesAsync(secondaries);
    }

    /// <summary>The primary root is new (added, then saved with everything else tracked).</summary>
    public static async Task CommitNewAsync<TPrimary>(
        IAggregateRootRepository<TPrimary> primaryRepository, TPrimary primary, params IPostingParticipant[] secondaries)
        where TPrimary : AggregateRoot
    {
        await primaryRepository.SaveAsync(primary, EventPublishOption.InMemoryPublishOnly);
        await PublishSecondariesAsync(secondaries);
    }

    private static async Task PublishSecondariesAsync(IPostingParticipant[] secondaries)
    {
        foreach (var secondary in secondaries)
            await secondary.PublishEventsAsync();
    }
}
