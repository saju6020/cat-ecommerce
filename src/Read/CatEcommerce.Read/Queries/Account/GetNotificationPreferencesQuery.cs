using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Account;

/// <summary>Q-E2 -- GET /api/customers/me/notification-preferences.</summary>
public sealed class GetNotificationPreferencesQuery : Query<NotificationPreferencesViewModel>
{
    public required Guid CustomerId { get; init; }
}
