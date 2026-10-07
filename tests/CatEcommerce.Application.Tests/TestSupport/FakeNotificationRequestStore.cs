using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.DomainServices;

namespace CatEcommerce.Application.Tests.TestSupport;

/// <summary>Hand-written test double -- always saves (no dedup-key race to simulate at the
/// Application layer; that race is Infrastructure/Npgsql-specific).</summary>
public sealed class FakeNotificationRequestStore(FakeAggregateRootRepository<NotificationRequestAggregate> repository) : INotificationRequestStore
{
    public bool NextSaveLosesRace { get; set; }

    public async Task<bool> TrySaveAsync(NotificationRequestAggregate request, CancellationToken ct = default)
    {
        if (NextSaveLosesRace) return false;

        await repository.SaveAsync(request, Platform.Infrastructure.Core.Commands.EventPublishOption.InMemoryPublishOnly);
        return true;
    }
}
