using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.DomainServices;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Notifications;

/// <summary>HO-NTF-001 §8.11/§11.4 C3. See <see cref="INotificationRequestStore"/>.</summary>
public sealed class EfNotificationRequestStore(IAggregateRootRepository<NotificationRequestAggregate> repository) : INotificationRequestStore
{
    public async Task<bool> TrySaveAsync(NotificationRequestAggregate request, CancellationToken ct = default)
    {
        try
        {
            await repository.SaveAsync(request, EventPublishOption.InMemoryPublishOnly);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ix_notification_request_dedup"
        })
        {
            return false;
        }
    }
}
