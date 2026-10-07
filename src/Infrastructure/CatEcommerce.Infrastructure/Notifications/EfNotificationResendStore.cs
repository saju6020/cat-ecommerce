using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.DomainServices;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Notifications;

/// <summary>HO-NTF-001 §11.4 C7. See <see cref="INotificationResendStore"/>.</summary>
public sealed class EfNotificationResendStore(IAggregateRootRepository<NotificationAggregate> repository) : INotificationResendStore
{
    public async Task<bool> TrySaveAsync(NotificationAggregate resend, CancellationToken ct = default)
    {
        try
        {
            await repository.SaveAsync(resend, EventPublishOption.InMemoryPublishOnly);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ix_notification_resend_of_id_ordinal"
        })
        {
            return false;
        }
    }
}
